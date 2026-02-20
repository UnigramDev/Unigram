//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Rg.DiffUtils;
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
                return cached;
            }

            _cache[gift.ReceivedGiftId] = gift;
            return gift;
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
            if (confirm == ContentDialogResult.Primary)
            {
                ClientService.Send(new DeleteGiftCollection(_senderId, collection.Id));

                Collections.Remove(collection);
                SelectedCollection = Collections[0];
            }
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
                var receivedGiftIds = new List<string>();
                if (gift != null)
                {
                    receivedGiftIds.Add(gift.ReceivedGiftId);
                }

                var response = await ClientService.SendAsync(new CreateGiftCollection(_senderId, popup.Text, receivedGiftIds));
                if (response is GiftCollection collection)
                {
                    var viewModel = new GiftCollectionViewModel(this, collection);

                    gift?.CollectionIds.Add(collection.Id);

                    Collections.Add(viewModel);
                    SelectedCollection = viewModel;
                }
            }
        }

        public async void AddGiftsToCollection(GiftCollectionViewModel collection)
        {
            var popup = new ChooseGiftsPopup(this);

            var confirm = await ShowPopupAsync(popup);
            if (confirm == ContentDialogResult.Primary)
            {
                var receivedGiftIds = new List<string>();

                foreach (var gift in popup.SelectedItems)
                {
                    if (gift.CollectionIds.Contains(collection.Id) /*|| album.Items.Contains(gift)*/)
                    {
                        continue;
                    }

                    if (collection.HasLoadedItems)
                    {
                        collection.Items.Insert(0, gift);
                    }

                    gift.CollectionIds.Add(collection.Id);
                    receivedGiftIds.Add(gift.ReceivedGiftId);
                }

                ClientService.Send(new AddGiftCollectionGifts(_senderId, collection.Id, receivedGiftIds));
                ShowToast(Locale.Declension(Strings.R.GiftAddedToCollectionTitle, receivedGiftIds.Count, collection.Name), ToastPopupIcon.Info);
            }
        }

        public void AddGiftToCollection((ReceivedGift gift, GiftCollectionViewModel collection) param)
        {
            if (param.gift.CollectionIds.Contains(param.collection.Id))
            {
                ClientService.Send(new RemoveGiftCollectionGifts(_senderId, param.collection.Id, new[] { param.gift.ReceivedGiftId }));

                param.gift.CollectionIds.Remove(param.collection.Id);

                if (param.collection.HasLoadedItems)
                {
                    param.collection.Items.Remove(param.gift);
                }

                ShowToast(string.Format(Strings.GiftRemovedFromCollectionX, param.collection.Name), ToastPopupIcon.Info);
            }
            else
            {
                ClientService.Send(new AddGiftCollectionGifts(_senderId, param.collection.Id, new[] { param.gift.ReceivedGiftId }));

                param.gift.CollectionIds.Add(param.collection.Id);

                if (param.collection.HasLoadedItems)
                {
                    param.collection.Items.Insert(0, param.gift);
                }

                ShowToast(string.Format(Strings.GiftAddedToCollectionX, param.collection.Name), ToastPopupIcon.Info);
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
            BeginOnUIThread(() =>
            {
                var receivedGift = Items.FirstOrDefault(x => x.ReceivedGiftId == update.ReceivedGiftId);
                if (receivedGift == null)
                {
                    return;
                }

                receivedGift.IsSaved = update.IsSaved;

                var index = Items.IndexOf(receivedGift);
                Items.Remove(receivedGift);
                Items.Insert(index, receivedGift);
            });
        }

        private void Handle(UpdateGiftIsSold update)
        {
            BeginOnUIThread(() =>
            {
                var receivedGift = Items.FirstOrDefault(x => x.ReceivedGiftId == update.ReceivedGiftId);
                if (receivedGift == null)
                {
                    return;
                }

                if (receivedGift.IsPinned)
                {
                    Items.Pinned.Remove(receivedGift.ReceivedGiftId);
                }

                Items.Remove(receivedGift);
            });
        }

        private void Handle(UpdateGiftUpgraded update)
        {
            BeginOnUIThread(() =>
            {
                var receivedGift = Items.FirstOrDefault(x => x.ReceivedGiftId == update.ReceivedGiftId || x.ReceivedGiftId == update.OldReceivedGiftId);
                if (receivedGift == null)
                {
                    return;
                }

                var index = Items.IndexOf(receivedGift);
                Items.Remove(receivedGift);
                Items.Insert(index, update.Gift);
            });
        }

        private void Reload(bool preload = true)
        {
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

        private ReceivedGiftsCollection UpdateItems(object arg1, string arg2)
        {
            return new ReceivedGiftsCollection(this, _senderId, _selectedCollection, _excludeUnsaved, _excludeSaved, _excludeUnlimited, _excludeUpgradable, _excludeNonUpgradable, _excludeUpgraded, _sortByPrice);
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

        public IncrementalCollectionView<ReceivedGift, IncrementalCollectionView<ReceivedGift, ProfileGiftsTabViewModel.ReceivedGiftsCollection>> ItemsView { get; }

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

            private readonly List<string> _pinnedGifts = new();

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
                return AsyncInfo.Run(async token =>
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
                            if (gift.IsPinned && gift.ReceivedGiftId.Length > 0)
                            {
                                _pinnedGifts.Add(gift.ReceivedGiftId);
                            }

                            Add(_viewModel.GetOrCreate(gift));
                            total++;
                        }
                    }

                    _viewModel.OnItemsReady();

                    _collection.IsEmpty = Items.Count == 0;
                    _collection.HasMoreItems = !string.IsNullOrEmpty(_nextOffsetId);
                    _collection.HasLoadedItems = true;

                    HasMoreItems = !string.IsNullOrEmpty(_nextOffsetId);

                    _loading = false;
                    return new LoadMoreItemsResult
                    {
                        Count = total
                    };
                });
            }

            public bool HasMoreItems { get; private set; } = true;

            // This is only valid for owned gifts
            public IList<string> Pinned => _pinnedGifts;
        }

        public void OpenGift(ReceivedGift receivedGift)
        {
            if (receivedGift == null)
            {
                return;
            }

            ShowPopup(new ReceivedGiftPopup(ClientService, NavigationService, receivedGift, _senderId, null));
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
            if (gift.IsPinned)
            {
                Items.Pinned.Remove(gift.ReceivedGiftId);
            }
            else
            {
                if (Items.Pinned.Count == ClientService.Options.PinnedGiftCountMax)
                {
                    ShowToast(Locale.Declension(Strings.R.GiftsPinLimit, ClientService.Options.PinnedGiftCountMax), ToastPopupIcon.Info);
                    return;
                }

                Items.Pinned.Insert(0, gift.ReceivedGiftId);
            }

            var response = await ClientService.SendAsync(new SetPinnedGifts(_senderId, Items.Pinned));
            if (response is Ok)
            {
                gift.IsPinned = !gift.IsPinned;

                if (gift.IsPinned)
                {
                    Items.Remove(gift);
                    Items.Insert(0, gift);
                }
                else
                {
                    var index = Items.BinarySearch(gift, (x, y) => Items.Pinned.Contains(y.ReceivedGiftId) ? 1 : y.Date.CompareTo(x.Date));
                    if (index < 0 && (~index < Items.Count || !Items.HasMoreItems))
                    {
                        Items.Remove(gift);
                        Items.Insert(~index, gift);
                    }
                }

                if (gift.IsPinned)
                {
                    ShowToast(string.Format("**{0}**\n{1}", Strings.Gift2PinnedTitle, Strings.Gift2PinnedSubtitle), ToastPopupIcon.Pin);
                }
            }
        }

        private int UnpinGiftComparison(ReceivedGift x, ReceivedGift y)
        {
            if (y.IsPinned)
            {
                return 1;
            }

            return y.Date.CompareTo(x.Date);
        }

        public void SetPinnedItems()
        {
            var receivedGiftIds = new List<string>();

            foreach (var item in Items)
            {
                if (item.IsPinned)
                {
                    receivedGiftIds.Add(item.ReceivedGiftId);
                }
                else
                {
                    break;
                }
            }

            if (receivedGiftIds.Count != Items.Pinned.Count)
            {
                return;
            }

            ClientService.Send(new SetPinnedGifts(_senderId, receivedGiftIds));
        }

        public void SetPinnedItem(ReceivedGift gift)
        {
            var index = Items.Pinned.IndexOf(gift.ReceivedGiftId);
            if (index >= 0 && index < Items.Count)
            {
                Items.Remove(gift);
                Items.Insert(index, gift);
            }
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

                if (gift.IsSaved)
                {
                    ToastPopup.Show(XamlRoot, string.Format("**{0}**\n{1}", Strings.Gift2MadePublicTitle, Strings.Gift2MadePublic), new DelayedFileSource(ClientService, gift.GetSticker()));
                }
                else
                {
                    ToastPopup.Show(XamlRoot, string.Format("**{0}**\n{1}", Strings.Gift2MadePrivateTitle, Strings.Gift2MadePrivate), new DelayedFileSource(ClientService, gift.GetSticker()));
                }
            }
        }

        public void TransferGift(ReceivedGift gift)
        {
            NavigationService.ShowPopup(new ChooseChatsPopup(), new ChooseChatsConfigurationTransferGift(gift));
        }
    }

    public partial class GiftCollectionViewModel : ServiceBase
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

        public bool HasMoreItems { get; set; } = true;

        public bool HasLoadedItems { get; set; }

        private bool _isEmpty;
        public bool IsEmpty
        {
            get => _isEmpty;
            set => Set(ref _isEmpty, value);
        }

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
