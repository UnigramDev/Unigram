//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Wallet;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Wallet
{
    public class WalletViewModel : ViewModelBase, IIncrementalCollectionOwner, IHandle, IDelegable<IWalletDelegate>
    {
        private readonly IWalletService _wallet;

        public IWalletDelegate Delegate { get; set; }

        private int _generation;
        private int _collectiblesGeneration;

        // Whether the page is shown, and whether it started the stream: navigating away while the
        // restore is out would otherwise stop a stream that is then started with nobody to stop it.
        private bool _shown;
        private bool _watching;

        public WalletViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, IWalletService wallet)
            : base(clientService, settingsService, aggregator)
        {
            _wallet = wallet;

            Transactions = new IncrementalCollection<TonWalletTransaction>(this);
            Collectibles = new IncrementalCollection<TonNft>(new CollectiblesLoader(this));

            Items = new IncrementalCollectionView(Transactions);
        }

        public IncrementalCollection<TonWalletTransaction> Transactions { get; }

        public IncrementalCollection<TonNft> Collectibles { get; }

        public IncrementalCollectionView Items { get; }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateWalletState>(this, Handle)
                .Subscribe<UpdateOwnedGramCount>(Handle);
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            _shown = true;

            Apply(_wallet.State);
            Apply(await _wallet.RestoreAsync());

            if (_shown && !_watching)
            {
                _watching = true;
                _wallet.StartWatching();
            }
        }

        protected override void OnNavigatedFrom(NavigationState suspensionState, bool suspending)
        {
            _shown = false;

            if (_watching)
            {
                _watching = false;
                _wallet.StopWatching();
            }
        }

        public void Handle(UpdateWalletState update)
        {
            BeginOnUIThread(() => Apply(update.State));
        }

        private void Handle(UpdateOwnedGramCount update)
        {
            BeginOnUIThread(() =>
            {
                RaisePropertyChanged(nameof(EarnedGramCount));
                RaisePropertyChanged(nameof(HasEarnedGrams));
            });
        }

        public long EarnedGramCount => ClientService.OwnedGramCount;

        public bool HasEarnedGrams => ClientService.OwnedGramCount > 0 || ClientService.HasGramTransactions;

        public async Task<IncrementalLoadResult> LoadMoreItemsAsync(uint count)
        {
            var before = Transactions.Count;

            await _wallet.LoadMoreActivityAsync();

            // Not left to the update, which arrives after this returns: the collection stops
            // paging after three loads that report nothing added.
            Mirror(_wallet.State);

            return new IncrementalLoadResult((uint)(Transactions.Count - before), _wallet.State.HasMoreActivity);
        }

        private async Task<IncrementalLoadResult> LoadMoreCollectiblesAsync()
        {
            var before = Collectibles.Count;

            await _wallet.LoadMoreCollectiblesAsync();
            Mirror(_wallet.State);

            return new IncrementalLoadResult((uint)(Collectibles.Count - before), _wallet.State.HasMoreCollectibles);
        }

        // A failed first page leaves the collection believing there is nothing more to load.
        public void RetryCollectibles()
        {
            Collectibles.Restart();
        }

        // This view model is already the owner of Items.
        private sealed class CollectiblesLoader : IIncrementalCollectionOwner
        {
            private readonly WalletViewModel _owner;

            public CollectiblesLoader(WalletViewModel owner)
            {
                _owner = owner;
            }

            public Task<IncrementalLoadResult> LoadMoreItemsAsync(uint count)
            {
                return _owner.LoadMoreCollectiblesAsync();
            }
        }

        private void Apply(WalletState state)
        {
            HasWallet = state.HasWallet;
            Address = state.Address;
            Balance = state.BalanceNanograms;
            IsSynchronized = state.IsSynchronized;
            Currency = state.Currency;
            CurrencyRate = state.CurrencyRate;
            ArchivedBalance = state.ArchivedBalanceNanograms;

            Mirror(state);

            // Not while loading, or the empty state flashes on the way to the first page.
            IsEmpty = state.HasWallet
                && state.Activity.Count == 0
                && state.ActivityResource.Phase is WalletResourcePhase.Ready or WalletResourcePhase.Failed;

            HasCollectibles = state.HasWallet
                && state.Collectibles.Count > 0;
        }

        private void Mirror(WalletState state)
        {
            // Restart rather than Clear, so the collection believes there is something to page again.
            if (_generation != state.ActivityGeneration)
            {
                _generation = state.ActivityGeneration;
                Transactions.Restart();
            }

            Mirror(Transactions, state.Activity, _activityKey ??= ActivityKey, Update, _transactionUpdated ??= TransactionUpdated);

            if (_collectiblesGeneration != state.CollectiblesGeneration)
            {
                _collectiblesGeneration = state.CollectiblesGeneration;
                Collectibles.Restart();
            }

            Mirror(Collectibles, state.Collectibles, static item => item.Address, Update, _collectibleUpdated ??= CollectibleUpdated);
        }

        // By key rather than by count: pending transfers are inserted at the top and later
        // replaced, and following by count would restart the list each time.
        //
        // A new version of an item already shown is copied onto the one the list holds, rather than
        // put in its place: a replaced item gets a new container, and a pending row is in the middle
        // of its own animation when its transfer settles. The view updates the container it has.
        private static void Mirror<T>(IncrementalCollection<T> items, IReadOnlyList<T> source, Func<T, string> key, Func<T, T, bool> update, Action<T> updated)
        {
            for (int i = 0; i < source.Count; i++)
            {
                var item = source[i];

                if (i >= items.Count)
                {
                    items.Add(item);
                    continue;
                }

                if (string.Equals(key(items[i]), key(item), StringComparison.Ordinal))
                {
                    if (!ReferenceEquals(items[i], item))
                    {
                        if (update(items[i], item))
                        {
                            updated(items[i]);
                        }
                    }

                    continue;
                }

                var found = IndexOf(items, key(item), key, i + 1);
                if (found < 0)
                {
                    items.Insert(i, item);
                    continue;
                }

                for (int j = found - 1; j >= i; j--)
                {
                    items.RemoveAt(j);
                }

                if (!ReferenceEquals(items[i], item))
                {
                    if (update(items[i], item))
                    {
                        updated(items[i]);
                    }
                }
            }

            for (int i = items.Count - 1; i >= source.Count; i--)
            {
                items.RemoveAt(i);
            }
        }

        // A transaction that took a pending row's place is keyed as that row, so it reaches the view
        // as a new version of the row - which is what lets the row settle where it is - rather than
        // as one row removed and another added. Cached: Mirror runs on every state change.
        private Func<TonWalletTransaction, string> _activityKey;

        private string ActivityKey(TonWalletTransaction item)
        {
            return _wallet.PredecessorOf(item.Id) ?? item.Id;
        }

        private Action<TonWalletTransaction> _transactionUpdated;
        private Action<TonNft> _collectibleUpdated;

        private void TransactionUpdated(TonWalletTransaction transaction)
        {
            Delegate?.UpdateTransaction(transaction);
        }

        private void CollectibleUpdated(TonNft collectible)
        {
            Delegate?.UpdateCollectible(collectible);
        }

        // Copies a new version onto the one shown, and says whether anything changed. The nested
        // objects by reference: the service hands over the same ones again until it has new ones.
        private static bool Update(TonWalletTransaction shown, TonWalletTransaction item)
        {
            if (shown.Id == item.Id
                && shown.PeerAddress == item.PeerAddress
                && shown.PeerUserId == item.PeerUserId
                && shown.PeerDomain == item.PeerDomain
                && shown.Date == item.Date
                && shown.FeeAmount == item.FeeAmount
                && ReferenceEquals(shown.State, item.State)
                && ReferenceEquals(shown.Type, item.Type))
            {
                return false;
            }

            shown.Id = item.Id;
            shown.PeerAddress = item.PeerAddress;
            shown.PeerUserId = item.PeerUserId;
            shown.PeerDomain = item.PeerDomain;
            shown.Date = item.Date;
            shown.FeeAmount = item.FeeAmount;
            shown.State = item.State;
            shown.Type = item.Type;
            return true;
        }

        private static bool Update(TonNft shown, TonNft item)
        {
            if (shown.CollectionAddress == item.CollectionAddress
                && shown.OwnerAddress == item.OwnerAddress
                && shown.Index == item.Index
                && shown.Name == item.Name
                && shown.Description == item.Description
                && ReferenceEquals(shown.Image, item.Image)
                && ReferenceEquals(shown.Thumbnail, item.Thumbnail)
                && ReferenceEquals(shown.Content, item.Content)
                && ReferenceEquals(shown.Sticker, item.Sticker)
                && ReferenceEquals(shown.Attributes, item.Attributes)
                && ReferenceEquals(shown.Extra, item.Extra))
            {
                return false;
            }

            shown.CollectionAddress = item.CollectionAddress;
            shown.OwnerAddress = item.OwnerAddress;
            shown.Index = item.Index;
            shown.Name = item.Name;
            shown.Description = item.Description;
            shown.Image = item.Image;
            shown.Thumbnail = item.Thumbnail;
            shown.Content = item.Content;
            shown.Sticker = item.Sticker;
            shown.Attributes = item.Attributes;
            shown.Extra = item.Extra;
            return true;
        }

        private static int IndexOf<T>(IncrementalCollection<T> items, string id, Func<T, string> key, int start)
        {
            for (int i = start; i < items.Count; i++)
            {
                if (string.Equals(key(items[i]), id, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private bool _hasWallet;
        public bool HasWallet
        {
            get => _hasWallet;
            set => Set(ref _hasWallet, value);
        }

        private bool _isEmpty;
        public bool IsEmpty
        {
            get => _isEmpty;
            set => Set(ref _isEmpty, value);
        }

        private bool _hasCollectibles;
        public bool HasCollectibles
        {
            get => _hasCollectibles;
            set => Set(ref _hasCollectibles, value);
        }

        private string _address;
        public string Address
        {
            get => _address;
            set => Set(ref _address, value);
        }

        private BigInteger _balance;
        public BigInteger Balance
        {
            get => _balance;
            set => Set(ref _balance, value);
        }

        private BigInteger _archivedBalance;
        public BigInteger ArchivedBalance
        {
            get => _archivedBalance;
            set => Set(ref _archivedBalance, value);
        }

        private string _currency;
        public string Currency
        {
            get => _currency;
            set => Set(ref _currency, value);
        }

        private double _currencyRate;
        public double CurrencyRate
        {
            get => _currencyRate;
            set => Set(ref _currencyRate, value);
        }

        // False is not zero: the balance is not known yet.
        private bool _isSynchronized;
        public bool IsSynchronized
        {
            get => _isSynchronized;
            set => Set(ref _isSynchronized, value);
        }
    }
}
