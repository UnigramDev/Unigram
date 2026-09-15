//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Numerics;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Wallet;
using Telegram.Td.Api;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Wallet
{
    // IHandle, or Subscribe is never called: ViewModelBase only wires an aggregator subscription
    // for view models that carry the marker, and without it the wallet never updates itself.
    public class WalletViewModel : ViewModelBase, IIncrementalCollectionOwner, IHandle
    {
        private readonly IWalletService _wallet;

        /// <summary>
        /// Which of the service's histories <see cref="Items"/> is following.
        /// </summary>
        private int _generation;

        public WalletViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, IWalletService wallet)
            : base(clientService, settingsService, aggregator)
        {
            _wallet = wallet;

            Items = new IncrementalCollection<TonWalletTransaction>(this);
        }

        /// <summary>
        /// The history, as the list sees it.
        /// </summary>
        /// <remarks>
        /// A mirror, not a second copy of the truth: the service owns the history and this follows
        /// it, row for row. The list asking for more is what drives the paging, through
        /// <see cref="LoadMoreItemsAsync"/>.
        /// </remarks>
        public IncrementalCollection<TonWalletTransaction> Items { get; }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateWalletState>(this, Handle);
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            // No refresh here. Attaching already starts one, and a second refresh supersedes the
            // first and cancels its requests - so refreshing on navigation would throw away the
            // load that is already in flight. A deliberate refresh gesture can call RefreshAsync.
            //
            // Apply what is already known before waiting on anything: a wallet that was restored
            // earlier in the session has its balance in hand, and the page should not blank out.
            Apply(_wallet.State);
            Apply(await _wallet.RestoreAsync());

            _wallet.StartWatching();
        }

        protected override void OnNavigatedFrom(NavigationState suspensionState, bool suspending)
        {
            _wallet.StopWatching();
        }

        // Published from the service, which is not on any one window's thread, so the hop happens
        // here, where there is a dispatcher to hop to.
        public void Handle(UpdateWalletState update)
        {
            BeginOnUIThread(() => Apply(update.State));
        }

        public async Task<IncrementalLoadResult> LoadMoreItemsAsync(uint count)
        {
            var before = Items.Count;

            await _wallet.LoadMoreActivityAsync();

            // Mirrored here rather than waited for: the service announces the new page through the
            // aggregator, which arrives after this returns, and a load that reported adding nothing
            // three times running is one the collection stops believing.
            Mirror(_wallet.State);

            return new IncrementalLoadResult((uint)(Items.Count - before), _wallet.State.HasMoreActivity);
        }

        private void Apply(WalletState state)
        {
            HasWallet = state.HasWallet;
            Address = state.Address;
            Balance = state.BalanceNanograms;
            IsSynchronized = state.IsSynchronized;
            Currency = state.Currency;
            CurrencyRate = state.CurrencyRate;

            Mirror(state);

            // Only once the history is in: an empty list that is still loading is not an empty
            // wallet, and the empty state would flash on the way to the first page.
            IsEmpty = state.HasWallet
                && state.Activity.Count == 0
                && state.ActivityResource.Phase == WalletResourcePhase.Ready;
        }

        /// <summary>
        /// Brings <see cref="Items"/> in line with the service's history.
        /// </summary>
        /// <remarks>
        /// Aligned by id rather than followed by count. The service puts a transfer this device
        /// sent at the top before the account has heard of it, and swaps it for the transaction it
        /// becomes; a mirror that appended by index would answer either of those by starting over,
        /// which takes the user's place in the list with it and re-pages what it already had.
        /// </remarks>
        private void Mirror(WalletState state)
        {
            var activity = state.Activity;

            // A different history, not a longer one: the wallet changed, or a refresh could not
            // reach what it held. Restart rather than Clear, so the list also believes there is
            // something to page again.
            if (_generation != state.ActivityGeneration)
            {
                _generation = state.ActivityGeneration;
                Items.Restart();
            }

            for (int i = 0; i < activity.Count; i++)
            {
                var item = activity[i];

                if (i >= Items.Count)
                {
                    Items.Add(item);
                    continue;
                }

                if (string.Equals(Items[i].Id, item.Id, StringComparison.Ordinal))
                {
                    // The same row carrying something new - a transfer that settled, or one that
                    // ran out of time.
                    if (!ReferenceEquals(Items[i], item))
                    {
                        Items[i] = item;
                    }

                    continue;
                }

                var found = IndexOf(item.Id, i + 1);
                if (found < 0)
                {
                    Items.Insert(i, item);
                    continue;
                }

                // It is further down, so everything between here and there is gone.
                for (int j = found - 1; j >= i; j--)
                {
                    Items.RemoveAt(j);
                }

                if (!ReferenceEquals(Items[i], item))
                {
                    Items[i] = item;
                }
            }

            for (int i = Items.Count - 1; i >= activity.Count; i--)
            {
                Items.RemoveAt(i);
            }

        }

        private int IndexOf(string id, int start)
        {
            for (int i = start; i < Items.Count; i++)
            {
                if (string.Equals(Items[i].Id, id, StringComparison.Ordinal))
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

        /// <summary>
        /// The account has a wallet and it has never been used. What the view shows instead of the
        /// history.
        /// </summary>
        private bool _isEmpty;
        public bool IsEmpty
        {
            get => _isEmpty;
            set => Set(ref _isEmpty, value);
        }

        private string _address;
        public string Address
        {
            get => _address;
            set => Set(ref _address, value);
        }

        /// <summary>
        /// The balance in nanograms. See <see cref="WalletState.BalanceNanograms"/> for why this is
        /// not a <see cref="long"/>.
        /// </summary>
        private BigInteger _balance;
        public BigInteger Balance
        {
            get => _balance;
            set => Set(ref _balance, value);
        }

        /// <summary>
        /// The currency the balance is priced in beside the grams, and what one of it is worth in
        /// USD. Both come from the state so that a change of currency reaches the card the same
        /// way a change of balance does.
        /// </summary>
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

        /// <summary>
        /// False means the balance is not known yet, which the view must not render as zero.
        /// </summary>
        private bool _isSynchronized;
        public bool IsSynchronized
        {
            get => _isSynchronized;
            set => Set(ref _isSynchronized, value);
        }
    }
}
