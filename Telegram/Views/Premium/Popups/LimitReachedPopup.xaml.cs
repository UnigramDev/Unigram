//
// Copyright (c) Fela Ameghino 2015-2026
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
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.Views.Host;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Telegram.Views.Premium.Popups
{
    public partial record PremiumLimitValue(long DefaultValue, long PremiumValue);

    public sealed partial class LimitReachedPopup : ContentPopup
    {
        private readonly INavigationService _navigationService;
        private readonly IClientService _clientService;
        private readonly PremiumLimitType _type;

        public LimitReachedPopup(INavigationService navigationService, IClientService clientService, PremiumLimitType type)
        {
            _navigationService = navigationService;
            _clientService = clientService;
            _type = type;

            InitializeComponent();
            InitializeLimit(clientService, type);

            Title = Strings.LimitReached;
        }

        private async void InitializeLimit(IClientService clientService, PremiumLimitType type)
        {
            var limit = await GetPremiumLimitAsync(clientService, type);
            if (limit != null)
            {
                var iconValue = string.Empty;
                var freeValue = string.Empty;
                var lockedValue = string.Empty;
                var premiumValue = string.Empty;

                var formatValue = new Func<long, string>(value => value.ToString());

                var animatedValue = new LocalFileSource("ms-appx:///Assets/Animations/Double.json");

                switch (type)
                {
                    case PremiumLimitTypeChatFolderChosenChatCount:
                        iconValue = Icons.ChatEmptyFilled;
                        freeValue = Strings.LimitReachedChatInFolders;
                        lockedValue = Strings.LimitReachedChatInFoldersLocked;
                        premiumValue = Strings.LimitReachedChatInFoldersPremium;
                        break;
                    case PremiumLimitTypeChatFolderCount:
                        iconValue = Icons.FolderFilled;
                        freeValue = Strings.LimitReachedFolders;
                        lockedValue = Strings.LimitReachedFoldersLocked;
                        premiumValue = Strings.LimitReachedFoldersPremium;
                        break;
                    case PremiumLimitTypeShareableChatFolderCount:
                        iconValue = Icons.FolderFilled;
                        freeValue = Strings.LimitReachedSharedFolders;
                        lockedValue = Strings.LimitReachedSharedFoldersLocked;
                        premiumValue = Strings.LimitReachedSharedFoldersPremium;
                        break;
                    case PremiumLimitTypeCreatedPublicChatCount:
                        iconValue = Icons.LinkFilled;
                        freeValue = Strings.LimitReachedPublicLinks;
                        lockedValue = Strings.LimitReachedPublicLinksLocked;
                        premiumValue = Strings.LimitReachedPublicLinksPremium;
                        break;
                    case PremiumLimitTypeChatFolderInviteLinkCount:
                        iconValue = Icons.LinkFilled;
                        freeValue = Strings.LimitReachedFolderLinks;
                        lockedValue = Strings.LimitReachedFolderLinksLocked;
                        premiumValue = Strings.LimitReachedFolderLinksPremium;
                        break;
                    case PremiumLimitTypePinnedSavedMessagesTopicCount:
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
                    case PremiumLimitTypeFileSize:
                        iconValue = Icons.DocumentFilled;
                        freeValue = Strings.LimitReachedFileSize;
                        lockedValue = Strings.LimitReachedFileSizeLocked;
                        premiumValue = Strings.LimitReachedFileSizePremium;

                        formatValue = new Func<long, string>(value => FileSizeConverter.Convert(value, true));
                        break;
                    case PremiumLimitTypeConnectedAccounts:
                        iconValue = Icons.PersonFilled;
                        freeValue = Strings.LimitReachedAccounts;
                        lockedValue = Strings.LimitReachedAccountsPremium;
                        premiumValue = Strings.LimitReachedAccountsPremium;

                        animatedValue = new LocalFileSource("ms-appx:///Assets/Animations/AddOne.json");
                        break;
                }

                if (clientService.IsPremium)
                {
                    TextBlockHelper.SetMarkdown(Subtitle, string.Format(premiumValue, formatValue(limit.PremiumValue)));

                    Icon.Text = iconValue;
                    Limit.Text = formatValue(limit.PremiumValue);
                    LimitBubble.CornerRadius = new CornerRadius(14, 14, 0, 14);
                    LimitHeader.HorizontalAlignment = HorizontalAlignment.Right;

                    PrevArrow.Visibility = Visibility.Collapsed;

                    PrevLimit.Text = formatValue(limit.DefaultValue);
                    NextLimit.Text = string.Empty;

                    animatedValue.ColorReplacements = new Dictionary<int, int>
                    {
                        { 0x000000, 0xffffff }
                    };

                    PurchaseIcon.Source = animatedValue;
                    PurchaseLabel.Text = Strings.IncreaseLimit;
                }
                else if (clientService.IsPremiumAvailable)
                {
                    TextBlockHelper.SetMarkdown(Subtitle, string.Format(freeValue, formatValue(limit.DefaultValue), formatValue(limit.PremiumValue)));

                    Icon.Text = iconValue;
                    Limit.Text = formatValue(limit.DefaultValue);
                    LimitBubble.CornerRadius = new CornerRadius(14, 14, 14, 14);
                    LimitHeader.HorizontalAlignment = HorizontalAlignment.Center;

                    NextArrow.Visibility = Visibility.Collapsed;

                    PrevLimit.Text = string.Empty;
                    NextLimit.Text = formatValue(limit.PremiumValue);

                    animatedValue.ColorReplacements = new Dictionary<int, int>
                    {
                        { 0x000000, 0xffffff }
                    };

                    PurchaseIcon.Source = animatedValue;
                    PurchaseLabel.Text = Strings.IncreaseLimit;
                    PrimaryButtonText = Strings.IncreaseLimit;
                }
                else
                {
                    TextBlockHelper.SetMarkdown(Subtitle, string.Format(lockedValue, formatValue(limit.DefaultValue)));

                    LimitHeader.Visibility = Visibility.Collapsed;
                    LimitPanel.Visibility = Visibility.Collapsed;

                    PrimaryButtonStyle = BootStrapper.Current.Resources["AccentButtonStyle"] as Style;
                    PurchaseIcon.Visibility = Visibility.Collapsed;
                    PurchaseLabel.Text = Strings.OK;
                    PrimaryButtonText = Strings.OK;
                }

                if (type is PremiumLimitTypeCreatedPublicChatCount)
                {
                    LoadAdminedPublicChannels();
                }
                else if (type is PremiumLimitTypeSupergroupCount)
                {
                    LoadInactiveSupergroupChats();
                }
                else
                {
                    Header.Visibility = Visibility.Collapsed;
                }
            }
        }

        private async void LoadAdminedPublicChannels()
        {
            Header.Text = Strings.YourPublicCommunities;
            ScrollingHost.SelectionMode = ListViewSelectionMode.None;
            ScrollingHost.IsItemClickEnabled = true;

            var response = await _clientService.SendAsync(new GetCreatedPublicChats(null));
            if (response is Telegram.Td.Api.Chats chats)
            {
                var result = new List<Chat>();

                foreach (var chat in _clientService.GetChats(chats.ChatIds))
                {
                    result.Add(chat);
                }

                Header.Visibility = Visibility.Visible;
                ScrollingHost.ItemsSource = result;
            }
            else if (response is Error error)
            {
                Logger.Error(error.Message);
            }
        }

        private async void LoadInactiveSupergroupChats()
        {
            Header.Text = Strings.LastActiveCommunities;
            ScrollingHost.SelectionMode = ListViewSelectionMode.Multiple;
            ScrollingHost.IsItemClickEnabled = false;

            var response = await _clientService.SendAsync(new GetInactiveSupergroupChats());
            if (response is Telegram.Td.Api.Chats chats)
            {
                var result = new List<Chat>();

                foreach (var chat in _clientService.GetChats(chats.ChatIds))
                {
                    result.Add(chat);
                }

                Header.Visibility = Visibility.Visible;
                ScrollingHost.ItemsSource = result;
            }
            else if (response is Error error)
            {
                Logger.Error(error.Message);
            }
        }

        private async Task<PremiumLimitValue> GetPremiumLimitAsync(IClientService clientService, PremiumLimitType type)
        {
            if (type is PremiumLimitTypeConnectedAccounts)
            {
                return new PremiumLimitValue(3, 4);
            }
            else if (type is PremiumLimitTypeFileSize)
            {
                return new PremiumLimitValue(2048L << 20, 4096L << 20);
            }

            if (clientService.IsPremiumAvailable)
            {
                var response = await clientService.SendAsync(new GetPremiumLimit(type));
                if (response is PremiumLimit limit)
                {
                    return new PremiumLimitValue(limit.DefaultValue, limit.PremiumValue);
                }
            }

            static PremiumLimitValue CreateLimit(long value)
            {
                return new PremiumLimitValue(value, value);
            }

            switch (type)
            {
                case PremiumLimitTypeChatFolderChosenChatCount:
                    return CreateLimit(clientService.Options.ChatFolderChosenChatCountMax);
                case PremiumLimitTypeChatFolderCount:
                    return CreateLimit(clientService.Options.ChatFolderCountMax);
                case PremiumLimitTypePinnedSavedMessagesTopicCount:
                    return CreateLimit(clientService.Options.PinnedSavedMessagesTopicCountMax);
                case PremiumLimitTypePinnedArchivedChatCount:
                    return CreateLimit(clientService.Options.PinnedArchivedChatCountMax);
                case PremiumLimitTypePinnedChatCount:
                    return CreateLimit(clientService.Options.PinnedChatCountMax);
                case PremiumLimitTypeShareableChatFolderCount:
                    return CreateLimit(clientService.Options.AddedShareableChatFolderCountMax);
                case PremiumLimitTypeCreatedPublicChatCount:
                    return CreateLimit(10);
                case PremiumLimitTypeSupergroupCount:
                    return CreateLimit(500);
                case PremiumLimitTypeConnectedAccounts:
                    return CreateLimit(4);
            }

            return null;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
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

                        if (_type is PremiumLimitTypeCreatedPublicChatCount)
                        {
                            subtitle.Text = MeUrlPrefixConverter.Convert(_clientService, supergroup.ActiveUsername(), true);
                        }
                        else if (_type is PremiumLimitTypeSupergroupCount)
                        {
                            // don't need to use result->dates_, because chat.last_message.date is more reliable
                            if (chat.LastMessage != null)
                            {
                                var currentDate = DateTime.Now.ToTimestamp();
                                int date = chat.LastMessage.Date;
                                int daysDif = (currentDate - date) / 86400;

                                String dateFormat;
                                if (daysDif < 30)
                                {
                                    dateFormat = Locale.Declension(Strings.R.Days, daysDif);
                                }
                                else if (daysDif < 365)
                                {
                                    dateFormat = Locale.Declension(Strings.R.Months, daysDif / 30);
                                }
                                else
                                {
                                    dateFormat = Locale.Declension(Strings.R.Years, daysDif / 365);
                                }

                                if (supergroup.IsChannel || supergroup.IsBroadcastGroup)
                                {
                                    subtitle.Text = string.Format(Strings.InactiveChannelSignature, dateFormat);
                                }
                                else
                                {
                                    String members = Locale.Declension(Strings.R.Members, _clientService.GetMembersCount(chat));
                                    subtitle.Text = string.Format(Strings.InactiveChatSignature, members, dateFormat);
                                }
                            }
                            else if (_clientService.HasActiveUsername(chat, out _))
                            {
                                if (supergroup.IsChannel || supergroup.IsBroadcastGroup)
                                {
                                    subtitle.Text = Strings.ChannelPublic.ToLower();
                                }
                                else
                                {
                                    subtitle.Text = Strings.MegaPublic.ToLower();
                                }
                            }
                            else if (supergroup.IsChannel || supergroup.IsBroadcastGroup)
                            {
                                subtitle.Text = Strings.ChannelPrivate.ToLower();
                            }
                            else
                            {
                                subtitle.Text = Strings.MegaPrivate.ToLower();
                            }
                        }
                    }
                }
            }
            else if (args.Phase == 2)
            {
                var photo = content.Children[0] as ProfilePicture;
                photo.Source = ProfilePictureSource.Chat(_clientService, chat);
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

            var popup = new TeachingTipEx();
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

            if (XamlRoot.Content is IToastHost host)
            {
                void handler(object sender, object e)
                {
                    host.ToastClosed(popup);
                    popup.Closed -= handler;
                }

                host.ToastOpened(popup);
                popup.Closed += handler;
            }

            popup.IsOpen = true;
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ScrollingHost.SelectedItems.Count > 0)
            {
                PrimaryButtonText = string.Empty;
                SecondaryButtonText = Locale.Declension(Strings.R.LeaveCommunities, ScrollingHost.SelectedItems.Count);
            }
            else
            {
                PrimaryButtonText = PurchaseLabel.Text;
                SecondaryButtonText = string.Empty;
            }
        }

        private async void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var deferral = args.GetDeferral();

            string title = Locale.Declension(Strings.R.LeaveCommunities, ScrollingHost.SelectedItems.Count);
            string message;

            if (ScrollingHost.SelectedItems.Count == 1 && ScrollingHost.SelectedItems[0] is Chat selectedChat)
            {
                if (selectedChat.Type is ChatTypeSupergroup { IsChannel: true })
                {
                    message = string.Format(Strings.ChannelLeaveAlertWithName, selectedChat.Title);
                }
                else
                {
                    message = string.Format(Strings.MegaLeaveAlertWithName, selectedChat.Title);
                }
            }
            else
            {
                message = Strings.ChatsLeaveAlert;
            }

            var confirm = await _navigationService.ShowPopupAsync(message, title, Strings.VoipGroupLeave, Strings.Cancel, destructive: true);
            if (confirm != ContentDialogResult.Primary)
            {
                args.Cancel = true;
                deferral.Complete();
                return;
            }

            deferral.Complete();

            foreach (var item in ScrollingHost.SelectedItems)
            {
                if (item is not Chat chat)
                {
                    continue;
                }

                _clientService.Send(new LeaveChat(chat.Id));
            }
        }
    }
}
