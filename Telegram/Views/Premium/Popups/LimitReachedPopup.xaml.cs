//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Telegram.Views.Premium.Popups
{
    public sealed partial class LimitReachedPopup : ContentPopup
    {
        private readonly INavigationService _navigationService;
        private readonly IClientService _clientService;

        public LimitReachedPopup(INavigationService navigationService, IClientService clientService, PremiumLimitType type)
        {
            _navigationService = navigationService;
            _clientService = clientService;

            InitializeComponent();
            InitializeLimit(clientService, type);

            Title = Strings.LimitReached;
        }

        private async void InitializeLimit(IClientService clientService, PremiumLimitType type)
        {
            var limit = await GetPremiumLimitAsync(clientService, type) as PremiumLimit;
            if (limit != null)
            {
                var iconValue = string.Empty;
                var freeValue = string.Empty;
                var lockedValue = string.Empty;
                var premiumValue = string.Empty;

                var animatedValue = new Uri("ms-appx:///Assets/Animations/Double.json");

                switch (type)
                {
                    case PremiumLimitTypeChatFilterChosenChatCount:
                        iconValue = Icons.ChatFilled;
                        freeValue = Strings.LimitReachedChatInFolders;
                        lockedValue = Strings.LimitReachedChatInFoldersLocked;
                        premiumValue = Strings.LimitReachedChatInFoldersPremium;
                        break;
                    case PremiumLimitTypeChatFilterCount:
                        iconValue = Icons.FolderFilled;
                        freeValue = Strings.LimitReachedFolders;
                        lockedValue = Strings.LimitReachedFoldersLocked;
                        premiumValue = Strings.LimitReachedFoldersPremium;
                        break;
                    case PremiumLimitTypeCreatedPublicChatCount:
                        iconValue = Icons.LinkFilled;
                        freeValue = Strings.LimitReachedPublicLinks;
                        lockedValue = Strings.LimitReachedPublicLinksLocked;
                        premiumValue = Strings.LimitReachedPublicLinksPremium;
                        break;
                    case PremiumLimitTypePinnedArchivedChatCount:
                    case PremiumLimitTypePinnedChatCount:
                        iconValue = Icons.PinFilled;
                        freeValue = Strings.LimitReachedPinDialogs;
                        lockedValue = Strings.LimitReachedPinDialogsLocked;
                        premiumValue = Strings.LimitReachedPinDialogsPremium;
                        break;
                    case PremiumLimitTypeSupergroupCount:
                        iconValue = Icons.PeopleFilled;
                        freeValue = Strings.LimitReachedCommunities;
                        lockedValue = Strings.LimitReachedCommunitiesLocked;
                        premiumValue = Strings.LimitReachedCommunitiesPremium;
                        break;
                    case PremiumLimitTypeConnectedAccounts:
                        iconValue = Icons.PersonFilled;
                        freeValue = Strings.LimitReachedAccounts;
                        lockedValue = Strings.LimitReachedAccountsPremium;
                        premiumValue = Strings.LimitReachedAccountsPremium;

                        animatedValue = new Uri("ms-appx:///Assets/Animations/AddOne.json");
                        break;
                }

                if (clientService.IsPremium)
                {
                    TextBlockHelper.SetMarkdown(Subtitle, string.Format(premiumValue, limit.PremiumValue));

                    Icon.Text = iconValue;
                    Limit.Text = limit.PremiumValue.ToString();
                    LimitBubble.CornerRadius = new CornerRadius(14, 14, 0, 14);
                    LimitHeader.HorizontalAlignment = HorizontalAlignment.Right;

                    PrevArrow.Visibility = Visibility.Collapsed;

                    PrevLimit.Text = limit.DefaultValue.ToString();
                    NextLimit.Text = string.Empty;

                    PurchaseIcon.ColorReplacements = new Dictionary<int, int>
                    {
                        { 0x000000, 0xffffff }
                    };

                    PurchaseIcon.Source = animatedValue;
                    PurchaseLabel.Text = Strings.IncreaseLimit;
                }
                else if (clientService.IsPremiumAvailable)
                {
                    TextBlockHelper.SetMarkdown(Subtitle, string.Format(freeValue, limit.DefaultValue, limit.PremiumValue));

                    Icon.Text = iconValue;
                    Limit.Text = limit.DefaultValue.ToString();
                    LimitBubble.CornerRadius = new CornerRadius(14, 14, 14, 14);
                    LimitHeader.HorizontalAlignment = HorizontalAlignment.Center;

                    NextArrow.Visibility = Visibility.Collapsed;

                    PrevLimit.Text = string.Empty;
                    NextLimit.Text = limit.PremiumValue.ToString();

                    PurchaseIcon.ColorReplacements = new Dictionary<int, int>
                    {
                        { 0x000000, 0xffffff }
                    };

                    PurchaseIcon.Source = animatedValue;
                    PurchaseLabel.Text = Strings.IncreaseLimit;
                }
                else
                {
                    TextBlockHelper.SetMarkdown(Subtitle, string.Format(lockedValue, limit.DefaultValue));

                    LimitHeader.Visibility = Visibility.Collapsed;
                    LimitPanel.Visibility = Visibility.Collapsed;

                    PurchaseCommand.Style = App.Current.Resources["AccentButtonStyle"] as Style;
                    PurchaseIcon.Visibility = Visibility.Collapsed;
                    PurchaseLabel.Text = Strings.OK;
                }

                if (type is PremiumLimitTypeCreatedPublicChatCount)
                {
                    LoadAdminedPublicChannels();
                }
            }
        }

        private async void LoadAdminedPublicChannels()
        {
            var response = await _clientService.SendAsync(new GetCreatedPublicChats());
            if (response is Telegram.Td.Api.Chats chats)
            {
                var result = new List<Chat>();

                foreach (var id in chats.ChatIds)
                {
                    var chat = _clientService.GetChat(id);
                    if (chat != null)
                    {
                        result.Add(chat);
                    }
                }

                Header.Visibility = Visibility.Visible;
                ScrollingHost.ItemsSource = result;
            }
            else if (response is Error error)
            {
                Logs.Logger.Error(Logs.LogTarget.API, "channels.getAdminedPublicChannels error " + error);
            }
        }

        private Task<BaseObject> GetPremiumLimitAsync(IClientService clientService, PremiumLimitType type)
        {
            if (type is PremiumLimitTypeConnectedAccounts)
            {
                return Task.FromResult<BaseObject>(new PremiumLimit(type, 3, 4));
            }

            if (clientService.IsPremiumAvailable)
            {
                return clientService.SendAsync(new GetPremiumLimit(type));
            }

            static Task<BaseObject> CreateLimit(PremiumLimitType type, long value)
            {
                return Task.FromResult<BaseObject>(new PremiumLimit(type, (int)value, (int)value));
            }

            switch (type)
            {
                case PremiumLimitTypeChatFilterChosenChatCount:
                    return CreateLimit(type, clientService.Options.ChatFilterChosenChatCountMax);
                case PremiumLimitTypeChatFilterCount:
                    return CreateLimit(type, clientService.Options.ChatFilterCountMax);
                case PremiumLimitTypeCreatedPublicChatCount:
                    return CreateLimit(type, 500);
                case PremiumLimitTypePinnedArchivedChatCount:
                    return CreateLimit(type, clientService.Options.PinnedArchivedChatCountMax);
                case PremiumLimitTypePinnedChatCount:
                    return CreateLimit(type, clientService.Options.PinnedChatCountMax);
                case PremiumLimitTypeSupergroupCount:
                    return CreateLimit(type, 10);
                case PremiumLimitTypeConnectedAccounts:
                    return CreateLimit(type, 4);
            }

            return null;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void PurchaseShadow_Loaded(object sender, RoutedEventArgs e)
        {
            DropShadowEx.Attach(PurchaseShadow);
        }

        private void Purchase_Click(object sender, RoutedEventArgs e)
        {
            Hide();

            if (_clientService.IsPremiumAvailable && !_clientService.IsPremium)
            {
                _navigationService.ShowPromo(new PremiumSourceLimitExceeded());
            }
        }

        #region Recycle

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            var chat = args.Item as Chat;

            if (args.Phase == 0)
            {
                var title = content.Children[1] as TextBlock;
                title.Text = _clientService.GetTitle(chat);
            }
            else if (args.Phase == 1)
            {
                if (chat.Type is ChatTypeSupergroup super)
                {
                    var supergroup = _clientService.GetSupergroup(super.SupergroupId);
                    if (supergroup != null)
                    {
                        var subtitle = content.Children[2] as TextBlock;
                        subtitle.Text = MeUrlPrefixConverter.Convert(_clientService, supergroup.ActiveUsername(), true);
                    }
                }
            }
            else if (args.Phase == 2)
            {
                var photo = content.Children[0] as ProfilePicture;
                photo.SetChat(_clientService, chat, 36);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(OnContainerContentChanging);
            }

            args.Handled = true;
        }

        #endregion

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            var container = ScrollingHost.ContainerFromItem(e.ClickedItem) as SelectorItem;
            if (container == null || e.ClickedItem is not Chat chat)
            {
                return;
            }

            var supergroup = _clientService.GetSupergroup(chat);
            if (supergroup == null)
            {
                return;
            }

            var popup = new TeachingTip();
            popup.Title = Strings.AppName;
            popup.Subtitle = string.Format(supergroup.IsChannel ? Strings.RevokeLinkAlertChannel : Strings.RevokeLinkAlert, MeUrlPrefixConverter.Convert(_clientService, supergroup.ActiveUsername(), true), chat.Title);
            popup.ActionButtonContent = Strings.RevokeButton;
            popup.ActionButtonStyle = BootStrapper.Current.Resources["AccentButtonStyle"] as Style;
            popup.CloseButtonContent = Strings.Cancel;
            popup.PreferredPlacement = TeachingTipPlacementMode.Top;
            popup.Width = popup.MinWidth = popup.MaxWidth = 314;
            popup.Target = /*badge ??*/ container;
            popup.IsLightDismissEnabled = true;
            popup.ShouldConstrainToRootBounds = true;

            popup.ActionButtonClick += async (s, args) =>
            {
                popup.IsOpen = false;

                var response = await _clientService.SendAsync(new SetSupergroupUsername(supergroup.Id, string.Empty));
                if (response is Ok)
                {
                    Hide();
                }
            };

            if (Window.Current.Content is FrameworkElement element)
            {
                element.Resources["TeachingTip"] = popup;
            }
            else
            {
                container.Resources["TeachingTip"] = popup;
            }

            popup.IsOpen = true;
        }
    }
}
