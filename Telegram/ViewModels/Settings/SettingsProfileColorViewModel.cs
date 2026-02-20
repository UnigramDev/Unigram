//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Telegram.Views.Settings;
using Telegram.Views.Stars.Popups;
using Windows.Foundation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace Telegram.ViewModels.Settings
{
    public partial class SettingsProfileColorViewModel : MultiViewModelBase
    {
        private SettingsProfileColorProfileViewModel _profileColor;
        private SettingsProfileColorNameViewModel _nameColor;

        public SettingsProfileColorViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            _profileColor = new SettingsProfileColorProfileViewModel(clientService, settingsService, aggregator);
            _nameColor = new SettingsProfileColorNameViewModel(clientService, settingsService, aggregator);

            SelectedTab = _profileColor;

            Children.Add(_profileColor);
            Children.Add(_nameColor);
        }

        private SettingsProfileColorTabViewModelBase _selectedTab;
        public SettingsProfileColorTabViewModelBase SelectedTab
        {
            get => _selectedTab;
            set => Set(ref _selectedTab, value);
        }

        public SettingsProfileColorProfileViewModel ProfileColor => _profileColor;
        public SettingsProfileColorNameViewModel NameColor => _nameColor;
    }

    public abstract partial class SettingsProfileColorTabViewModelBase : ViewModelBase
    {
        protected SettingsProfileColorTabViewModelBase(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }

        public string Name { get; protected set; }

        public Type Type { get; protected set; }

        public abstract string PrimaryButtonText { get; }

        public abstract void Continue();

        public partial class AvailableGiftsCollection : ObservableCollection<GiftForResale>, IIncrementalCollection
        {
            private readonly IClientService _clientService;
            private readonly AvailableGift _gift;

            private string _nextOffset = string.Empty;
            private bool _hasMoreItems = true;

            public AvailableGiftsCollection(IClientService clientService, AvailableGift gift)
            {
                _clientService = clientService;
                _gift = gift;

                _hasMoreItems = gift != null;
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(LoadMoreItemsAsync);
            }

            private async Task<LoadMoreItemsResult> LoadMoreItemsAsync(CancellationToken token)
            {
                var totalCount = 0u;

                var response = await _clientService.SendAsync(new SearchGiftsForResale(_gift.Gift.Id, new GiftForResaleOrderPrice(), false, Array.Empty<UpgradedGiftAttributeId>(), _nextOffset, 24));
                if (response is GiftsForResale gifts)
                {
                    foreach (var gift in gifts.Gifts)
                    {
                        Add(gift);
                        totalCount++;
                    }

                    _nextOffset = gifts.NextOffset;
                    _hasMoreItems = gifts.NextOffset.Length > 0;
                }

                return new LoadMoreItemsResult
                {
                    Count = totalCount
                };
            }

            public bool HasMoreItems => _hasMoreItems;

            public AvailableGift Gift => _gift;
        }

        public override string ToString()
        {
            return Name;
        }
    }

    public abstract partial class SettingsProfileColorTabViewModelBase<T> : SettingsProfileColorTabViewModelBase, IIncrementalCollectionOwner
    {
        protected SettingsProfileColorTabViewModelBase(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, bool hasColors)
            : base(clientService, settingsService, aggregator)
        {
            Items = new IncrementalCollection<T>(this);
            Collections = new ObservableCollection<AvailableGiftsCollection>();

            ItemsView = new IncrementalCollectionView(Items);

            InitializeAvailableGifts(hasColors);
            SelectedCollection = Collections[0];
        }

        public override string PrimaryButtonText => SelectedItem != null ? Strings.UserColorApplyCollectible : Strings.UserColorApply;

        public IncrementalCollectionView ItemsView { get; private set; }

        public IncrementalCollection<T> Items { get; private set; }

        protected T _selectedItem;
        public T SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (value == null && Set(ref _selectedItem, value))
                {
                    Set(ref _selectedItemView, null, nameof(SelectedItemView));
                    RaisePropertyChanged(nameof(PrimaryButtonText));
                }
            }
        }

        protected object _selectedItemView;
        public abstract object SelectedItemView { get; set; }

        private bool _isEmpty;
        public bool IsEmpty
        {
            get => _isEmpty;
            set
            {
                if (Set(ref _isEmpty, value))
                {
                    RaisePropertyChanged(nameof(IsEmptyView));
                }
            }
        }

        public bool IsEmptyView => IsEmpty && SelectedCollection.Gift == null;

        public ObservableCollection<AvailableGiftsCollection> Collections { get; private set; }

        private AvailableGiftsCollection _selectedCollection;
        public AvailableGiftsCollection SelectedCollection
        {
            get => _selectedCollection;
            set
            {
                if (Set(ref _selectedCollection, value))
                {
                    if (value.Gift != null)
                    {
                        ItemsView.SetSource(value);
                    }
                    else
                    {
                        ItemsView.SetSource(Items);
                    }

                    RaisePropertyChanged(nameof(SelectedItemView));
                    RaisePropertyChanged(nameof(IsEmptyView));
                }
            }
        }

        public abstract Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count);

        public bool HasMoreItems { get; protected set; } = true;

        public void Browse()
        {
            if (Collections.Count > 1)
            {
                SelectedCollection = Collections[1];
            }
        }

        public override async void Continue()
        {
            if (!IsPremium)
            {
                ToastPopup.ShowFeaturePromo(NavigationService, new PremiumFeatureAccentColor());
                return;
            }

            if (SelectedItemView is GiftForResale giftForResale)
            {
                var confirm = await TransferGiftPopup.ShowAsync(XamlRoot, ClientService, giftForResale, null);
                if (confirm == ContentDialogResult.Primary)
                {
                    var response = await ClientService.SendPaymentAsync(giftForResale.Gift.ResaleParameters.StarCount, new SendResoldGift(giftForResale.Gift.Name, ClientService.MyId, new GiftResalePriceStar(giftForResale.Gift.ResaleParameters.StarCount)));
                    if (response is GiftResaleResultOk)
                    {
                        ContinueImpl(giftForResale, null);
                    }
                    else if (response is Error error)
                    {
                        ToastPopup.ShowError(XamlRoot, error);
                    }
                    else if (response is ErrorStarsNeeded)
                    {
                        NavigationService.ShowPopup(new BuyPopup(), BuyStarsArgs.ForChannel(giftForResale.Gift.ResaleParameters.StarCount, 0));
                    }
                }
            }
            else
            {
                ContinueImpl(SelectedItemView, null);
            }
        }

        protected abstract void ContinueImpl(object value, NavigatingEventArgs args);

        private async void InitializeAvailableGifts(bool hasColors)
        {
            Collections.Add(new AvailableGiftsCollection(ClientService, null));

            var response = await ClientService.SendAsync(new GetAvailableGifts());
            if (response is AvailableGifts gifts)
            {
                foreach (var gift in gifts.Gifts)
                {
                    if (string.IsNullOrEmpty(gift.Title))
                    {
                        continue;
                    }

                    if (hasColors)
                    {
                        if (gift.Gift.HasColors)
                        {
                            Collections.Add(new AvailableGiftsCollection(ClientService, gift));
                        }
                    }
                    else
                    {
                        Collections.Add(new AvailableGiftsCollection(ClientService, gift));
                    }
                }
            }
        }
    }

    public partial class SettingsProfileColorNameViewModel : SettingsProfileColorTabViewModelBase<UpgradedGift>, IIncrementalCollectionOwner
    {
        private string _nextOffset = string.Empty;

        public SettingsProfileColorNameViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator, true)
        {
            Name = Strings.UserColorTabName;
            Type = typeof(SettingsProfileColorNameTabPage);

            if (clientService.TryGetUser(clientService.Options.MyId, out User user))
            {
                SelectedAccentColor = clientService.GetAccentColor(user.AccentColorId);
                SelectedCustomEmojiId = user.BackgroundCustomEmojiId;
            }
        }

        public override object SelectedItemView
        {
            get => _selectedItemView;
            set
            {
                if (value != null && Set(ref _selectedItemView, value))
                {
                    if (value is UpgradedGift upgradedGift)
                    {
                        Set(ref _selectedItem, upgradedGift, nameof(SelectedItem));
                    }
                    else if (value is GiftForResale giftForResale)
                    {
                        upgradedGift = giftForResale.Gift;

                        Set(ref _selectedItem, upgradedGift, nameof(SelectedItem));
                    }

                    RaisePropertyChanged(nameof(PrimaryButtonText));
                }
            }
        }

        private NameColor _selectedAccentColor;
        public NameColor SelectedAccentColor
        {
            get => _selectedAccentColor;
            set => Set(ref _selectedAccentColor, value);
        }

        private long _selectedCustomEmojiId;
        public long SelectedCustomEmojiId
        {
            get => _selectedCustomEmojiId;
            set => Set(ref _selectedCustomEmojiId, value);
        }

        public override async Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            var totalCount = 0u;

            var response = await ClientService.SendAsync(new GetReceivedGifts(ClientService.MyId, 0, false, false, false, false, false, false, true, false, false, _nextOffset, 50));
            if (response is ReceivedGifts gifts)
            {
                var user = ClientService.GetUser(ClientService.Options.MyId);

                foreach (var gift in gifts.Gifts)
                {
                    if (gift.Gift is SentGiftUpgraded upgraded)
                    {
                        Items.Add(upgraded.Gift);
                        totalCount++;

                        if (upgraded.Gift.Id == user.UpgradedGiftColors?.Id)
                        {
                            SelectedItemView = upgraded.Gift;
                        }
                    }
                }

                _nextOffset = gifts.NextOffset;
                HasMoreItems = gifts.NextOffset.Length > 0;
            }
            else
            {
                HasMoreItems = false;
            }

            IsEmpty = Items.Empty();

            return new LoadMoreItemsResult
            {
                Count = totalCount
            };
        }

        protected override void ContinueImpl(object value, NavigatingEventArgs args)
        {
            if (value is UpgradedGift upgradedGift)
            {
                ClientService.Send(new SetUpgradedGiftColors(upgradedGift.Id));
            }
            else if (value is GiftForResale giftForResale)
            {
                ClientService.Send(new SetUpgradedGiftColors(giftForResale.Gift.Id));
            }
            else
            {
                ClientService.Send(new SetAccentColor(SelectedAccentColor.Id, SelectedCustomEmojiId));
            }

            ShowToast(Strings.UserColorApplied, ToastPopupIcon.Success);
        }
    }

    public partial class SettingsProfileColorProfileViewModel : SettingsProfileColorTabViewModelBase<EmojiStatusTypeUpgradedGift>, IIncrementalCollectionOwner
    {
        public SettingsProfileColorProfileViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator, false)
        {
            Name = Strings.UserColorTabProfile;
            Type = typeof(SettingsProfileColorProfileTabPage);

            if (clientService.TryGetUser(clientService.Options.MyId, out User user))
            {
                SelectedAccentColor = clientService.GetProfileColor(user.ProfileAccentColorId);
                SelectedCustomEmojiId = user.ProfileBackgroundCustomEmojiId;
            }
        }

        public override object SelectedItemView
        {
            get => _selectedItemView;
            set
            {
                if (value != null && Set(ref _selectedItemView, value))
                {
                    if (value is EmojiStatusTypeUpgradedGift upgradedGift)
                    {
                        Set(ref _selectedItem, upgradedGift, nameof(SelectedItem));
                    }
                    else if (value is GiftForResale giftForResale)
                    {
                        var model = giftForResale.Gift.Model.Sticker.FullType as StickerFullTypeCustomEmoji;
                        var symbol = giftForResale.Gift.Symbol.Sticker.FullType as StickerFullTypeCustomEmoji;

                        if (model == null || symbol == null)
                        {
                            return;
                        }

                        upgradedGift = new EmojiStatusTypeUpgradedGift(giftForResale.Gift.Id, giftForResale.Gift.Title, giftForResale.Gift.Name, model.CustomEmojiId, symbol.CustomEmojiId, giftForResale.Gift.Backdrop.Colors);

                        Set(ref _selectedItem, upgradedGift, nameof(SelectedItem));
                    }

                    RaisePropertyChanged(nameof(PrimaryButtonText));
                }
            }
        }

        private ProfileColor _selectedAccentColor;
        public ProfileColor SelectedAccentColor
        {
            get => _selectedAccentColor;
            set => Set(ref _selectedAccentColor, value);
        }

        private long _selectedCustomEmojiId;
        public long SelectedCustomEmojiId
        {
            get => _selectedCustomEmojiId;
            set => Set(ref _selectedCustomEmojiId, value);
        }

        public override async Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            var totalCount = 0u;

            var response = await ClientService.SendAsync(new GetUpgradedGiftEmojiStatuses());
            if (response is EmojiStatuses gifts)
            {
                var user = ClientService.GetUser(ClientService.Options.MyId);
                var status = user.EmojiStatus?.Type as EmojiStatusTypeUpgradedGift;

                foreach (var gift in gifts.EmojiStatusesValue)
                {
                    if (gift.Type is EmojiStatusTypeUpgradedGift upgradedGift)
                    {
                        Items.Add(upgradedGift);
                        totalCount++;

                        if (upgradedGift.UpgradedGiftId == status?.UpgradedGiftId)
                        {
                            SelectedItemView = upgradedGift;
                        }
                    }
                }
            }

            HasMoreItems = false;
            IsEmpty = Items.Empty();

            return new LoadMoreItemsResult
            {
                Count = totalCount
            };
        }

        protected override void ContinueImpl(object value, NavigatingEventArgs args)
        {
            if (value is EmojiStatusTypeUpgradedGift upgradedGift)
            {
                ClientService.Send(new SetEmojiStatus(new EmojiStatus(upgradedGift, 0)));
            }
            else if (value is GiftForResale giftForResale)
            {
                var model = giftForResale.Gift.Model.Sticker.FullType as StickerFullTypeCustomEmoji;
                var symbol = giftForResale.Gift.Symbol.Sticker.FullType as StickerFullTypeCustomEmoji;

                if (model == null || symbol == null)
                {
                    return;
                }

                ClientService.Send(new SetEmojiStatus(new EmojiStatus(new EmojiStatusTypeUpgradedGift(giftForResale.Gift.Id, giftForResale.Gift.Title, giftForResale.Gift.Name, model.CustomEmojiId, symbol.CustomEmojiId, giftForResale.Gift.Backdrop.Colors), 0)));
            }
            else
            {
                ClientService.Send(new SetProfileAccentColor(SelectedAccentColor?.Id ?? -1, SelectedCustomEmojiId));
            }

            NavigationService.GoBack(args);
            ShowToast(Strings.UserColorApplied, ToastPopupIcon.Success);
        }
    }
}
