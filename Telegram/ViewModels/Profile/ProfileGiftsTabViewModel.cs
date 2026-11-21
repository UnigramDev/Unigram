//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Telegram.Views.Stars.Popups;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Profile
{
    public partial class ProfileGiftsTabViewModel : ViewModelBase, IHandle, IDiffHandler<ReceivedGift>
    {
        private readonly ConcurrentDictionary<string, ReceivedGift> _cache = new();

        private MessageSender _senderId;

        // The profile's pinned gifts in order, whichever collection or filter is on screen;
        // null until a pin operation needs it.
        private List<ReceivedGift> _pinned;

        public ProfileGiftsTabViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Collections = new ObservableCollection<GiftCollectionViewModel>();
            Collections.Add(new GiftCollectionViewModel(this, new GiftCollection(0, Strings.GiftsCollectionNameAllGifts, null, 0)));
            Collections.CollectionChanged += Collections_CollectionChanged;

            ItemsView = new IncrementalCollectionView<ReceivedGift, IncrementalCollectionView<ReceivedGift, ReceivedGiftsCollection>>(Collections[0].Items);
            SelectedCollection = Collections[0];
        }

        public ReceivedGift GetOrCreate(ReceivedGift gift)
        {
            if (string.IsNullOrEmpty(gift.ReceivedGiftId))
            {
                return gift;
            }

            if (_cache.TryGetValue(gift.ReceivedGiftId, out ReceivedGift cached))
            {
                Refresh(cached, gift);
                return cached;
            }

            _cache[gift.ReceivedGiftId] = gift;
            return gift;
        }

        /// <summary>
        /// Every field but the id: the lists share one instance per gift, and the diff that swaps
        /// in a reloaded list keeps the old instance wherever the gift did not move.
        /// </summary>
        private static void Refresh(ReceivedGift cached, ReceivedGift gift)
        {
            cached.SenderId = gift.SenderId;
            cached.Text = gift.Text;
            cached.UniqueGiftNumber = gift.UniqueGiftNumber;
            cached.IsPrivate = gift.IsPrivate;
            cached.IsSaved = gift.IsSaved;
            cached.IsPinned = gift.IsPinned;
            cached.CanBeUpgraded = gift.CanBeUpgraded;
            cached.CanBeTransferred = gift.CanBeTransferred;
            cached.CanBeDeleted = gift.CanBeDeleted;
            cached.WasRefunded = gift.WasRefunded;
            cached.Date = gift.Date;
            cached.Gift = gift.Gift;
            cached.CollectionIds = gift.CollectionIds;
            cached.SellStarCount = gift.SellStarCount;
            cached.PrepaidUpgradeStarCount = gift.PrepaidUpgradeStarCount;
            cached.IsUpgradeSeparate = gift.IsUpgradeSeparate;
            cached.TransferStarCount = gift.TransferStarCount;
            cached.DropOriginalDetailsStarCount = gift.DropOriginalDetailsStarCount;
            cached.NextTransferDate = gift.NextTransferDate;
            cached.NextResaleDate = gift.NextResaleDate;
            cached.ExportDate = gift.ExportDate;
            cached.PrepaidUpgradeHash = gift.PrepaidUpgradeHash;
            cached.CraftDate = gift.CraftDate;
        }

        private static int IndexOf(IList<ReceivedGift> list, string receivedGiftId)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].ReceivedGiftId == receivedGiftId)
                {
                    return i;
                }
            }

            return -1;
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is long chatId)
            {
                var chat = ClientService.GetChat(chatId);
                if (chat == null)
                {
                    return Task.CompletedTask;
                }

                var user = ClientService.GetUser(chat);
                if (user == null)
                {
                    _senderId = new MessageSenderChat(chat.Id);
                }
                else
                {
                    _senderId = new MessageSenderUser(user.Id);
                }

                Reload(false);

                if (!_collectionsLoaded)
                {
                    _collectionsLoaded = true;
                    InitializeCollections();
                }
            }

            return Task.CompletedTask;
        }

        public bool HasCollections => Collections.Count > 1 || IsOwned;

        private bool _collectionsLoaded;

        public ObservableCollection<GiftCollectionViewModel> Collections { get; private set; }

        private GiftCollectionViewModel _selectedCollection;
        public GiftCollectionViewModel SelectedCollection
        {
            get => _selectedCollection;
            set
            {
                if (Set(ref _selectedCollection, value ?? Collections.FirstOrDefault()))
                {
                    ItemsView.SetSource(_selectedCollection.Items);
                }
            }
        }

        private void Collections_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RaisePropertyChanged(nameof(HasCollections));
        }

        private async void InitializeCollections()
        {
            var response = await ClientService.SendAsync(new GetGiftCollections(_senderId));
            if (response is GiftCollections collections)
            {
                foreach (var collection in collections.Collections)
                {
                    Collections.Add(new GiftCollectionViewModel(this, collection));
                }
            }
        }

        public void ShareCollection(GiftCollectionViewModel collection)
        {
            if (ClientService.HasActiveUsername(_senderId, out string username))
            {
                ShowPopup(new ChooseChatsPopup(), new ChooseChatsConfigurationPostLink(new InternalLinkTypeGiftCollection(username, collection.Id)));
            }
        }

        public async void RenameCollection(GiftCollectionViewModel collection)
        {
            var popup = new InputPopup(InputPopupType.Text)
            {
                Title = Strings.GiftsCollectionMenuEditName,
                Header = Strings.GiftsCollectionRenameHint,
                PlaceholderText = Strings.GiftsCollectionTitleInputHint,
                PrimaryButtonText = Strings.Rename,
                PrimaryButtonStyle = BootStrapper.Current.Resources["AccentButtonStyle"] as Style,
                SecondaryButtonText = Strings.Cancel,
                Text = collection.Name,
                MinLength = 1,
                MaxLength = 12
            };

            var result = await popup.ShowQueuedAsync(XamlRoot);

            var confirm = new InputPopupResult(result, popup.Text, popup.Value);
            if (confirm.Result == ContentDialogResult.Primary)
            {
                collection.Name = confirm.Text;
                ClientService.Send(new SetGiftCollectionName(_senderId, collection.Id, confirm.Text));
            }
        }

        public async void DeleteCollection(GiftCollectionViewModel collection)
        {
            var confirm = await ShowPopupAsync(string.Format(Strings.GiftsCollectionMenuDeleteCollectionAsk, collection.Name), Strings.GiftsCollectionMenuDeleteCollection, Strings.Delete, Strings.Cancel, destructive: true);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var response = await ClientService.SendAsync(new DeleteGiftCollection(_senderId, collection.Id));
            if (response is not Ok)
            {
                return;
            }

            foreach (var gift in _cache.Values)
            {
                if (gift.CollectionIds.Contains(collection.Id))
                {
                    gift.CollectionIds = gift.CollectionIds.Without(collection.Id);
                }
            }

            Collections.Remove(collection);
            SelectedCollection = Collections[0];
        }

        public async void CreateCollection(ReceivedGift gift)
        {
            var popup = new InputPopup(InputPopupType.Text)
            {
                Title = Strings.GiftsCollectionCreateNew,
                Header = Strings.GiftsCollectionAddHint,
                PlaceholderText = Strings.GiftsCollectionTitleInputHint,
                PrimaryButtonText = Strings.Create,
                PrimaryButtonStyle = BootStrapper.Current.Resources["AccentButtonStyle"] as Style,
                SecondaryButtonText = Strings.Cancel,
                MinLength = 1,
                MaxLength = 12
            };

            var result = await popup.ShowQueuedAsync(XamlRoot);

            var confirm = new InputPopupResult(result, popup.Text, popup.Value);
            if (confirm.Result == ContentDialogResult.Primary)
            {
                var receivedGiftIds = new MutableVector<string>();
                if (gift != null)
                {
                    receivedGiftIds.Add(gift.ReceivedGiftId);
                }

                var response = await ClientService.SendAsync(new CreateGiftCollection(_senderId, popup.Text, receivedGiftIds));
                if (response is GiftCollection collection)
                {
                    var viewModel = new GiftCollectionViewModel(this, collection);

                    gift?.CollectionIds = gift.CollectionIds.With(collection.Id);

                    Collections.Add(viewModel);
                    SelectedCollection = viewModel;
                }
            }
        }

        public async void AddGiftsToCollection(GiftCollectionViewModel collection)
        {
            var popup = new ChooseGiftsPopup(this);

            var confirm = await ShowPopupAsync(popup);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var gifts = new List<ReceivedGift>();
            var receivedGiftIds = new MutableVector<string>();

            foreach (var gift in popup.SelectedItems)
            {
                if (gift.CollectionIds.Contains(collection.Id))
                {
                    continue;
                }

                gifts.Add(gift);
                receivedGiftIds.Add(gift.ReceivedGiftId);
            }

            // addGiftCollectionGifts takes at least one gift.
            if (gifts.Count == 0)
            {
                return;
            }

            var response = await ClientService.SendAsync(new AddGiftCollectionGifts(_senderId, collection.Id, receivedGiftIds));
            if (response is not GiftCollection)
            {
                return;
            }

            // A list that has not loaded yet gets them from the server instead, and would hold them twice.
            var items = collection.Items.Source;

            foreach (var gift in gifts)
            {
                gift.CollectionIds = gift.CollectionIds.With(collection.Id);

                if (items.IsLoaded)
                {
                    items.Insert(0, gift);
                }
            }

            ShowToast(Locale.Declension(Strings.R.GiftAddedToCollectionTitle, gifts.Count, collection.Name), ToastPopupIcon.Info);
        }

        public async void AddGiftToCollection((ReceivedGift gift, GiftCollectionViewModel collection) param)
        {
            var gift = param.gift;
            var collection = param.collection;
            var items = collection.Items.Source;

            if (gift.CollectionIds.Contains(collection.Id))
            {
                var response = await ClientService.SendAsync(new RemoveGiftCollectionGifts(_senderId, collection.Id, new[] { gift.ReceivedGiftId }));
                if (response is not GiftCollection)
                {
                    return;
                }

                gift.CollectionIds = gift.CollectionIds.Without(collection.Id);

                var index = IndexOf(items, gift.ReceivedGiftId);
                if (index >= 0)
                {
                    items.RemoveAt(index);
                }

                ShowToast(string.Format(Strings.GiftRemovedFromCollectionX, collection.Name), ToastPopupIcon.Info);
            }
            else
            {
                var response = await ClientService.SendAsync(new AddGiftCollectionGifts(_senderId, collection.Id, new[] { gift.ReceivedGiftId }));
                if (response is not GiftCollection)
                {
                    return;
                }

                gift.CollectionIds = gift.CollectionIds.With(collection.Id);

                if (items.IsLoaded)
                {
                    items.Insert(0, gift);
                }

                ShowToast(string.Format(Strings.GiftAddedToCollectionX, collection.Name), ToastPopupIcon.Info);
            }
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateGiftIsSaved>(this, Handle)
                .Subscribe<UpdateGiftIsSold>(Handle)
                .Subscribe<UpdateGiftUpgraded>(Handle);
        }

        private void Handle(UpdateGiftIsSaved update)
        {
            BeginOnUIThread(() => UpdateIsSaved(update.ReceivedGiftId, update.IsSaved));
        }

        private void UpdateIsSaved(string receivedGiftId, bool isSaved)
        {
            // The server drops a gift from the pinned list when it leaves the profile page.
            var unpinned = !isSaved && IsPinned(receivedGiftId);
            if (unpinned && _pinned != null)
            {
                _pinned = Without(_pinned, receivedGiftId);
            }

            foreach (var collection in Collections)
            {
                var items = collection.Items.Source;

                var index = IndexOf(items, receivedGiftId);
                if (index < 0)
                {
                    continue;
                }

                var gift = items[index];
                gift.IsSaved = isSaved;

                if (unpinned)
                {
                    gift.IsPinned = false;
                }

                items.RemoveAt(index);

                if (!items.Accepts(gift))
                {
                    continue;
                }
                else if (unpinned && items.KeepsPinnedFirst)
                {
                    if (items.PlaceUnpinned(gift) is false)
                    {
                        collection.Reload(collection == SelectedCollection);
                    }
                }
                else
                {
                    items.Insert(index, gift);
                }
            }
        }

        private void Handle(UpdateGiftIsSold update)
        {
            BeginOnUIThread(() =>
            {
                if (_pinned != null)
                {
                    _pinned = Without(_pinned, update.ReceivedGiftId);
                }

                foreach (var collection in Collections)
                {
                    var items = collection.Items.Source;

                    var index = IndexOf(items, update.ReceivedGiftId);
                    if (index >= 0)
                    {
                        items.RemoveAt(index);
                    }
                }

                _cache.TryRemove(update.ReceivedGiftId, out _);
            });
        }

        private void Handle(UpdateGiftUpgraded update)
        {
            BeginOnUIThread(() =>
            {
                _cache.TryRemove(update.OldReceivedGiftId, out _);

                var gift = GetOrCreate(update.Gift);

                foreach (var collection in Collections)
                {
                    var items = collection.Items.Source;

                    var index = IndexOf(items, update.OldReceivedGiftId);
                    if (index < 0)
                    {
                        index = IndexOf(items, update.ReceivedGiftId);
                    }

                    if (index >= 0)
                    {
                        items.RemoveAt(index);
                        items.Insert(index, gift);
                    }
                }
            });
        }

        private bool IsPinned(string receivedGiftId)
        {
            if (_pinned != null)
            {
                return IndexOf(_pinned, receivedGiftId) >= 0;
            }

            return _cache.TryGetValue(receivedGiftId, out ReceivedGift gift) && gift.IsPinned;
        }

        private static List<ReceivedGift> Without(List<ReceivedGift> gifts, string receivedGiftId)
        {
            var index = IndexOf(gifts, receivedGiftId);
            if (index < 0)
            {
                return gifts;
            }

            // A pin operation awaiting the server may still hold the old list.
            var copy = new List<ReceivedGift>(gifts);
            copy.RemoveAt(index);
            return copy;
        }

        private void Reload(bool preload = true)
        {
            _pinned = null;

            // The filters apply to every collection, so every list starts over; only the visible one loads now.
            foreach (var collection in Collections)
            {
                collection.Reload(preload && SelectedCollection == collection);
            }
        }

        private bool _excludeUnsaved;
        public bool ExcludeUnsaved
        {
            get => _excludeUnsaved;
            set
            {
                if (value && _excludeSaved)
                {
                    value = false;
                    _excludeSaved = false;

                    RaisePropertyChanged(nameof(ExcludeSaved));

                    Reload();
                }
                else if (Set(ref _excludeUnsaved, value))
                {
                    Reload();
                }
            }
        }

        private bool _excludeSaved;
        public bool ExcludeSaved
        {
            get => _excludeSaved;
            set
            {
                if (_excludeUnsaved && value)
                {
                    _excludeUnsaved = false;
                    value = false;

                    RaisePropertyChanged(nameof(ExcludeUnsaved));

                    Reload();
                }
                else if (Set(ref _excludeSaved, value))
                {
                    Reload();
                }
            }
        }

        private bool _excludeUnlimited;
        public bool ExcludeUnlimited
        {
            get => _excludeUnlimited;
            set
            {
                if (value && _excludeUpgradable && _excludeNonUpgradable && _excludeUpgraded)
                {
                    value = false;
                    _excludeUpgradable = false;
                    _excludeNonUpgradable = false;
                    _excludeUpgraded = false;

                    RaisePropertyChanged(nameof(ExcludeUpgradable));
                    RaisePropertyChanged(nameof(ExcludeNonUpgradable));
                    RaisePropertyChanged(nameof(ExcludeUpgraded));

                    Reload();
                }
                else if (Set(ref _excludeUnlimited, value))
                {
                    Reload();
                }
            }
        }

        private bool _excludeUpgradable;
        public bool ExcludeUpgradable
        {
            get => _excludeUpgradable;
            set
            {
                if (_excludeUnlimited && value && _excludeNonUpgradable && _excludeUpgraded)
                {
                    _excludeUnlimited = false;
                    value = false;
                    _excludeNonUpgradable = false;
                    _excludeUpgraded = false;

                    RaisePropertyChanged(nameof(ExcludeUnlimited));
                    RaisePropertyChanged(nameof(ExcludeNonUpgradable));
                    RaisePropertyChanged(nameof(ExcludeUpgraded));

                    Reload();
                }
                else if (Set(ref _excludeUpgradable, value))
                {
                    Reload();
                }
            }
        }

        private bool _excludeNonUpgradable;
        public bool ExcludeNonUpgradable
        {
            get => _excludeNonUpgradable;
            set
            {
                if (_excludeUnlimited && _excludeUpgradable && value && _excludeUpgraded)
                {
                    _excludeUnlimited = false;
                    _excludeUpgradable = false;
                    value = false;
                    _excludeUpgraded = false;

                    RaisePropertyChanged(nameof(ExcludeUnlimited));
                    RaisePropertyChanged(nameof(ExcludeUpgradable));
                    RaisePropertyChanged(nameof(ExcludeUpgraded));

                    Reload();
                }
                else if (Set(ref _excludeNonUpgradable, value))
                {
                    Reload();
                }
            }
        }

        private bool _excludeUpgraded;
        public bool ExcludeUpgraded
        {
            get => _excludeUpgraded;
            set
            {
                if (_excludeUnlimited && _excludeUpgradable && _excludeNonUpgradable && value)
                {
                    _excludeUnlimited = false;
                    _excludeUpgradable = false;
                    _excludeNonUpgradable = false;
                    value = false;

                    RaisePropertyChanged(nameof(ExcludeUnlimited));
                    RaisePropertyChanged(nameof(ExcludeUpgradable));
                    RaisePropertyChanged(nameof(ExcludeNonUpgradable));

                    Reload();
                }
                else if (Set(ref _excludeUpgraded, value))
                {
                    Reload();
                }
            }
        }

        private bool _sortByPrice;
        public bool SortByPrice
        {
            get => _sortByPrice;
            set
            {
                if (Set(ref _sortByPrice, value))
                {
                    Reload();
                }
            }
        }

        /// <summary>
        /// Limits the list to gifts that can still be upgraded, as the type filters would; takes
        /// effect on the next load.
        /// </summary>
        public void ShowUpgradableOnly()
        {
            _excludeUnlimited = true;
            _excludeUpgradable = false;
            _excludeNonUpgradable = true;
            _excludeUpgraded = true;

            RaisePropertyChanged(nameof(ExcludeUnlimited));
            RaisePropertyChanged(nameof(ExcludeUpgradable));
            RaisePropertyChanged(nameof(ExcludeNonUpgradable));
            RaisePropertyChanged(nameof(ExcludeUpgraded));

            Reload(false);
        }

        public void Preload()
        {
            if (Items.Empty())
            {
                Reload();
            }
        }

        public event EventHandler ItemsReady;

        protected void OnItemsReady()
        {
            ItemsReady?.Invoke(this, EventArgs.Empty);
            ItemsReady = null;
        }

        public ReceivedGiftsCollection CreateItemsSource(GiftCollectionViewModel collection)
        {
            return new ReceivedGiftsCollection(this, _senderId, collection, _excludeUnsaved, _excludeSaved, _excludeUnlimited, _excludeUpgradable, _excludeNonUpgradable, _excludeUpgraded, _sortByPrice);
        }

        public bool CompareItems(ReceivedGift oldItem, ReceivedGift newItem)
        {
            if (oldItem.Date != newItem.Date)
            {
                return false;
            }

            if (oldItem.Gift is SentGiftRegular oldRegular && newItem.Gift is SentGiftRegular newRegular)
            {
                return oldRegular.Gift.Id == newRegular.Gift.Id;
            }
            else if (oldItem.Gift is SentGiftUpgraded oldUpgraded && newItem.Gift is SentGiftUpgraded newUpgraded)
            {
                return oldUpgraded.Gift.Id == newUpgraded.Gift.Id;
            }

            return false;
        }

        public void UpdateItem(ReceivedGift oldItem, ReceivedGift newItem)
        {

        }

        public IncrementalCollectionView<ReceivedGift, IncrementalCollectionView<ReceivedGift, ReceivedGiftsCollection>> ItemsView { get; }

        //public SearchCollection<ReceivedGift, ReceivedGiftsCollection> ItemsView { get; private set; }
        public ReceivedGiftsCollection Items => ItemsView.Source.Source;

        public partial class ReceivedGiftsCollection : ObservableCollection<ReceivedGift>, IIncrementalCollection<ReceivedGift>
        {
            private readonly ProfileGiftsTabViewModel _viewModel;
            private readonly MessageSender _ownerId;
            private readonly GiftCollectionViewModel _collection;
            private readonly bool _excludeUnsaved;
            private readonly bool _excludeSaved;
            private readonly bool _excludeUnlimited;
            private readonly bool _excludeUpgradable;
            private readonly bool _excludeNonUpgradable;
            private readonly bool _excludeUpgraded;
            private readonly bool _sortByPrice;

            private string _nextOffsetId = string.Empty;
            private bool _loading;

            public ReceivedGiftsCollection(ProfileGiftsTabViewModel viewModel, MessageSender ownerId, GiftCollectionViewModel collection, bool excludeUnsaved, bool excludeSaved, bool excludeUnlimited, bool excludeUpgradable, bool excludeNonUpgradable, bool excludeUpgraded, bool sortByPrice)
            {
                _viewModel = viewModel;
                _ownerId = ownerId;
                _collection = collection;
                _excludeUnsaved = excludeUnsaved;
                _excludeSaved = excludeSaved;
                _excludeUnlimited = excludeUnlimited;
                _excludeUpgradable = excludeUpgradable;
                _excludeNonUpgradable = excludeNonUpgradable;
                _excludeUpgraded = excludeUpgraded;
                _sortByPrice = sortByPrice;
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return IncrementalLoading.Run(async token =>
                {
                    if (_loading)
                    {
                        return new LoadMoreItemsResult
                        {
                            Count = 0
                        };
                    }

                    _loading = true;

                    var total = 0u;
                    var limit = count == 3 ? 3 : 50;

                    var response = await _viewModel.ClientService.SendAsync(new GetReceivedGifts(_ownerId, _collection.Id, _excludeUnsaved, _excludeSaved, _excludeUnlimited, _excludeUpgradable, _excludeNonUpgradable, _excludeUpgraded, false, false, _sortByPrice, _nextOffsetId, limit));
                    if (response is ReceivedGifts gifts)
                    {
                        _nextOffsetId = gifts.NextOffset;

                        foreach (var gift in gifts.Gifts)
                        {
                            Add(_viewModel.GetOrCreate(gift));
                            total++;
                        }
                    }

                    _viewModel.OnItemsReady();

                    _collection.IsEmpty = Items.Count == 0;

                    HasMoreItems = !string.IsNullOrEmpty(_nextOffsetId);
                    IsLoaded = true;

                    _loading = false;
                    return new LoadMoreItemsResult
                    {
                        Count = total
                    };
                });
            }

            public bool HasMoreItems { get; private set; } = true;

            public bool IsLoaded { get; private set; }

            /// <summary>
            /// The full list puts the pinned gifts first; a collection keeps the order its owner gave it.
            /// </summary>
            public bool KeepsPinnedFirst => _collection.Id == 0;

            public bool SortByPrice => _sortByPrice;

            /// <summary>
            /// Whether the filters still let the gift in. Only the ones a gift can move across while
            /// the list is open are checked: being saved, and being upgraded.
            /// </summary>
            public bool Accepts(ReceivedGift gift)
            {
                if (gift.IsSaved ? _excludeSaved : _excludeUnsaved)
                {
                    return false;
                }

                return !_excludeUpgraded || gift.Gift is not SentGiftUpgraded;
            }

            /// <summary>
            /// Moves a gift that just lost its pin back among the others, by date. Returns false when
            /// the list is sorted by value, which only the server can place.
            /// </summary>
            public bool PlaceUnpinned(ReceivedGift gift)
            {
                var index = ProfileGiftsTabViewModel.IndexOf(this, gift.ReceivedGiftId);
                if (index >= 0)
                {
                    // The search has to follow the removal: a position found in the longer list
                    // is one slot too far down, and Count when the gift sorts last.
                    RemoveAt(index);
                }

                if (_sortByPrice)
                {
                    return false;
                }

                index = this.BinarySearch(gift, UnpinnedComparison);
                if (index < 0)
                {
                    index = ~index;
                }

                // Past the end of a list that has more to load, the gift arrives with the next page.
                if (index < Count || !HasMoreItems)
                {
                    Insert(index, gift);
                }

                return true;
            }

            private static int UnpinnedComparison(ReceivedGift gift, ReceivedGift item)
            {
                if (item.IsPinned)
                {
                    return 1;
                }

                return item.Date.CompareTo(gift.Date);
            }
        }

        public void OpenGift(ReceivedGift receivedGift)
        {
            if (receivedGift == null)
            {
                return;
            }

            ShowPopup(new ReceivedGiftPopup(ClientService, NavigationService, receivedGift, _senderId, null, this));
        }

        public bool IsOwned
        {
            get
            {
                if (_senderId.IsUser(ClientService.Options.MyId))
                {
                    return true;
                }
                else if (ClientService.TryGetSupergroup(_senderId, out Supergroup supergroup))
                {
                    return supergroup.CanPostMessages();
                }

                return false;
            }
        }

        public MessageSender OwnerId => _senderId;

        public async void PinGift(ReceivedGift gift)
        {
            await PinGiftAsync(gift);
        }

        /// <summary>
        /// setPinnedGifts replaces the whole list, so it is built from the profile's pins and never
        /// from the list on screen, which a collection or a filter may leave without some of them.
        /// </summary>
        private async Task<List<ReceivedGift>> GetPinnedAsync()
        {
            if (_pinned != null)
            {
                return _pinned;
            }

            // Pinned gifts are upgraded and saved, and come first when sorted by date.
            var limit = (int)Math.Min(ClientService.Options.PinnedGiftCountMax, 100);

            var response = await ClientService.SendAsync(new GetReceivedGifts(_senderId, 0, true, false, true, true, true, false, false, false, false, string.Empty, limit));
            if (response is not ReceivedGifts gifts)
            {
                return null;
            }

            var pinned = new List<ReceivedGift>(gifts.Gifts.Count);

            foreach (var gift in gifts.Gifts)
            {
                if (!gift.IsPinned)
                {
                    break;
                }

                pinned.Add(GetOrCreate(gift));
            }

            return _pinned = pinned;
        }

        public async Task<bool> IsPinLimitReachedAsync(ReceivedGift gift)
        {
            if (gift.IsPinned)
            {
                return false;
            }

            var pinned = await GetPinnedAsync();
            return pinned != null && pinned.Count >= ClientService.Options.PinnedGiftCountMax;
        }

        public async Task PinGiftAsync(ReceivedGift gift)
        {
            var pinned = await GetPinnedAsync();
            if (pinned == null)
            {
                return;
            }

            var pin = IndexOf(pinned, gift.ReceivedGiftId) < 0;
            if (pin && pinned.Count >= ClientService.Options.PinnedGiftCountMax)
            {
                await ReplacePinnedGiftAsync(gift, pinned);
                return;
            }

            if (pin && await SaveForPinAsync(gift) is false)
            {
                return;
            }

            var updated = pin
                ? new List<ReceivedGift>(pinned.Count + 1) { gift }
                : new List<ReceivedGift>(pinned.Count);

            foreach (var item in pinned)
            {
                if (item.ReceivedGiftId != gift.ReceivedGiftId)
                {
                    updated.Add(item);
                }
            }

            if (await SetPinnedAsync(pinned, updated) is false)
            {
                return;
            }

            if (pin)
            {
                ShowToast(string.Format("**{0}**\n{1}", Strings.Gift2PinnedTitle, Strings.Gift2PinnedSubtitle), ToastPopupIcon.Pin);
            }
            else
            {
                ShowToast(Strings.Gift2Unpinned, ToastPopupIcon.Unpin);
            }
        }

        /// <summary>
        /// setPinnedGifts refuses a gift that is not on the profile page, so one is put there first.
        /// </summary>
        private async Task<bool> SaveForPinAsync(ReceivedGift gift)
        {
            if (gift.IsSaved)
            {
                return true;
            }

            var saved = await ClientService.SendAsync(new ToggleGiftIsSaved(gift.ReceivedGiftId, true));
            if (saved is not Ok)
            {
                return false;
            }

            gift.IsSaved = true;
            Aggregator.Publish(new UpdateGiftIsSaved(gift.ReceivedGiftId, true));

            return true;
        }

        /// <summary>
        /// With every pin taken, the user picks the pinned gift the new one replaces, in place.
        /// </summary>
        private async Task ReplacePinnedGiftAsync(ReceivedGift gift, List<ReceivedGift> pinned)
        {
            var popup = new ChooseGiftsPopup(ClientService, pinned);

            var confirm = await ShowPopupAsync(popup);
            if (confirm != ContentDialogResult.Primary || popup.SelectedItem is not ReceivedGift replaced)
            {
                return;
            }

            var index = IndexOf(pinned, replaced.ReceivedGiftId);
            if (index < 0 || await SaveForPinAsync(gift) is false)
            {
                return;
            }

            var updated = new List<ReceivedGift>(pinned);
            updated[index] = gift;

            if (await SetPinnedAsync(pinned, updated))
            {
                ShowToast(string.Format("**{0}**\n{1}", string.Format(Strings.Gift2ReplacedPinTitle, GetName(gift)), string.Format(Strings.Gift2ReplacedPinSubtitle, GetName(replaced))), ToastPopupIcon.Pin);
            }
        }

        private static string GetName(ReceivedGift gift)
        {
            return gift.Gift is SentGiftUpgraded upgraded ? upgraded.Gift.ToName() : string.Empty;
        }

        /// <summary>
        /// Nothing local changes until the server accepts the list, so a failure leaves the lists as they were.
        /// </summary>
        private async Task<bool> SetPinnedAsync(List<ReceivedGift> pinned, List<ReceivedGift> updated)
        {
            var receivedGiftIds = new MutableVector<string>(updated.Count);

            foreach (var gift in updated)
            {
                receivedGiftIds.Add(gift.ReceivedGiftId);
            }

            var response = await ClientService.SendAsync(new SetPinnedGifts(_senderId, receivedGiftIds));
            if (response is not Ok)
            {
                return false;
            }

            ApplyPinned(pinned, updated);
            return true;
        }

        private void ApplyPinned(List<ReceivedGift> pinned, List<ReceivedGift> updated)
        {
            _pinned = updated;

            foreach (var gift in pinned)
            {
                gift.IsPinned = false;
            }

            foreach (var gift in updated)
            {
                gift.IsPinned = true;
            }

            foreach (var collection in Collections)
            {
                var items = collection.Items.Source;
                if (!items.IsLoaded)
                {
                    continue;
                }

                if (items.KeepsPinnedFirst && items.SortByPrice)
                {
                    collection.Reload(collection == SelectedCollection);
                }
                else if (items.KeepsPinnedFirst)
                {
                    foreach (var gift in pinned)
                    {
                        var index = IndexOf(items, gift.ReceivedGiftId);
                        if (index >= 0)
                        {
                            items.RemoveAt(index);
                        }
                    }

                    var position = 0;

                    foreach (var gift in updated)
                    {
                        var index = IndexOf(items, gift.ReceivedGiftId);
                        if (index >= 0)
                        {
                            items.RemoveAt(index);
                        }

                        if (items.Accepts(gift))
                        {
                            items.Insert(position++, gift);
                        }
                    }

                    foreach (var gift in pinned)
                    {
                        if (!gift.IsPinned && items.Accepts(gift))
                        {
                            items.PlaceUnpinned(gift);
                        }
                    }
                }
                else
                {
                    // The order stays, only the pin badge changes.
                    Redraw(items, pinned);
                    Redraw(items, updated);
                }
            }
        }

        private static void Redraw(ReceivedGiftsCollection items, List<ReceivedGift> gifts)
        {
            foreach (var gift in gifts)
            {
                var index = IndexOf(items, gift.ReceivedGiftId);
                if (index >= 0)
                {
                    items.RemoveAt(index);
                    items.Insert(index, gift);
                }
            }
        }

        /// <summary>
        /// Called after a pinned gift is dragged: the pinned gifts at the top of the list, in their
        /// new order, become the profile's pins. A drop that leaves any of them below the others is
        /// undone.
        /// </summary>
        public async void SetPinnedItems()
        {
            var order = new List<ReceivedGift>();

            // The drag moved the items in the view only, not in the lists behind it.
            foreach (var item in ItemsView)
            {
                if (item.IsPinned)
                {
                    order.Add(item);
                }
                else
                {
                    break;
                }
            }

            var pinned = await GetPinnedAsync();
            if (pinned == null)
            {
                return;
            }

            var valid = order.Count == pinned.Count;

            for (int i = 0; valid && i < order.Count; i++)
            {
                valid = IndexOf(pinned, order[i].ReceivedGiftId) >= 0;
            }

            if (valid is false || await SetPinnedAsync(pinned, order) is false)
            {
                ApplyPinned(pinned, pinned);
            }

            // The lists forward their changes by index, which the drag left pointing at other items
            // in the view, so it is matched to them again.
            ItemsView.SetSource(_selectedCollection.Items);
        }

        public void CopyGift(ReceivedGift gift)
        {
            if (gift.Gift is SentGiftUpgraded upgraded)
            {
                MessageHelper.CopyLink(ClientService, XamlRoot, new InternalLinkTypeUpgradedGift(upgraded.Gift.Name));
            }
        }

        public void ShareGift(ReceivedGift gift)
        {
            if (gift.Gift is SentGiftUpgraded upgraded)
            {
                NavigationService.ShowPopup(new ChooseChatsPopup(), new ChooseChatsConfigurationPostLink(new InternalLinkTypeUpgradedGift(upgraded.Gift.Name)));
            }
        }

        public async void ToggleGift(ReceivedGift gift)
        {
            var response = await ClientService.SendAsync(new ToggleGiftIsSaved(gift.ReceivedGiftId, !gift.IsSaved));
            if (response is Ok)
            {
                gift.IsSaved = !gift.IsSaved;
                Aggregator.Publish(new UpdateGiftIsSaved(gift.ReceivedGiftId, gift.IsSaved));

                if (_senderId is MessageSenderChat)
                {
                    ToastPopup.Show(XamlRoot, gift.IsSaved
                        ? string.Format("**{0}**\n{1}", Strings.Gift2ChannelMadePublicTitle, Strings.Gift2ChannelMadePublic)
                        : string.Format("**{0}**\n{1}", Strings.Gift2ChannelMadePrivateTitle, Strings.Gift2ChannelMadePrivate), new DelayedFileSource(ClientService, gift.GetSticker()));
                }
                else if (gift.IsSaved)
                {
                    ToastPopup.Show(XamlRoot, string.Format("**{0}**\n{1}", Strings.Gift2MadePublicTitle, Strings.Gift2MadePublic), new DelayedFileSource(ClientService, gift.GetSticker()));
                }
                else
                {
                    ToastPopup.Show(XamlRoot, string.Format("**{0}**\n{1}", Strings.Gift2MadePrivateTitle, Strings.Gift2MadePrivate), new DelayedFileSource(ClientService, gift.GetSticker()));
                }
            }
        }

        public void ToggleWear(ReceivedGift gift)
        {
            if (gift.Gift is SentGiftUpgraded upgraded)
            {
                _ = ReceivedGiftPopup.ToggleWearAsync(ClientService, NavigationService, XamlRoot, upgraded.Gift);
            }
        }

        public void TransferGift(ReceivedGift gift)
        {
            NavigationService.ShowPopup(new ChooseChatsPopup(), new ChooseChatsConfigurationTransferGift(gift));
        }
    }

    public partial class GiftCollectionViewModel : ViewModelBase
    {
        private readonly ProfileGiftsTabViewModel _viewModel;
        private readonly ProfileGiftsTabViewModel.ReceivedGiftsCollection _items;

        private int _fromStoryId;

        public GiftCollectionViewModel(ProfileGiftsTabViewModel viewModel, GiftCollection collection)
            : base(viewModel.ClientService, viewModel.Settings, viewModel.Aggregator)
        {
            _viewModel = viewModel;

            Name = collection.Name;
            Id = collection.Id;

            Items = new IncrementalCollectionView<ReceivedGift, ProfileGiftsTabViewModel.ReceivedGiftsCollection>(viewModel.CreateItemsSource(this));
            Items.CollectionChanged += OnCollectionChanged;

            //if (collection.Id == 0)
            //{
            //    Items = _viewModel.Items;
            //}
            //else
            //{
            //    Items = new IncrementalCollection<ReceivedGift>(this);
            //    Items.CollectionChanged += OnCollectionChanged;
            //}
        }

        public async void Reload(bool preload)
        {
            if (preload)
            {
                await Items.SetSourceAsync(_viewModel.CreateItemsSource(this));
            }
            else
            {
                Items.SetSource(_viewModel.CreateItemsSource(this));
            }
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            IsEmpty = Items.Empty();
        }

        private string _name;
        public string Name
        {
            get => _name;
            set => Set(ref _name, value);
        }

        public int Id { get; }

        public IncrementalCollectionView<ReceivedGift, ProfileGiftsTabViewModel.ReceivedGiftsCollection> Items { get; }

        //public async Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        //{
        //    var totalCount = 0u;

        //    var response = await ClientService.SendAsync(new GetStoryAlbumStories(_viewModel.Chat.Id, Id, _fromStoryId, 50));
        //    if (response is Td.Api.Stories stories)
        //    {
        //        foreach (var story in stories.StoriesValue)
        //        {
        //            _fromStoryId = story.Id;

        //            Items.Add(_viewModel.GetOrCreate(story));
        //            totalCount++;
        //        }

        //        //Items.TotalCount = stories.TotalCount;
        //    }

        //    IsEmpty = Items.Count == 0;
        //    HasMoreItems = totalCount > 0;
        //    HasLoadedItems = true;

        //    return new LoadMoreItemsResult
        //    {
        //        Count = totalCount
        //    };
        //}

        private bool _isEmpty;
        public bool IsEmpty
        {
            get => _isEmpty;
            set
            {
                if (Set(ref _isEmpty, value))
                {
                    RaisePropertyChanged(nameof(ShowEmptyState));
                }
            }
        }

        /// <summary>
        /// The empty state invites the owner to fill one of their collections; it is not what an
        /// empty or filtered-out profile shows.
        /// </summary>
        public bool ShowEmptyState => _isEmpty && Id != 0 && _viewModel.IsOwned;

        //public void ReorderStories()
        //{
        //    if (Id != 0)
        //    {
        //        ClientService.Send(new ReorderStoryAlbumStories(_viewModel.Chat.Id, Id, Items.Select(x => x.StoryId).ToList()));
        //    }
        //}

        public override string ToString()
        {
            return Name;
        }
    }
}
