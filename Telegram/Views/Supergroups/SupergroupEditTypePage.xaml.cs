//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.UI.Xaml.Controls;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.ViewModels.Settings;
using Telegram.ViewModels.Supergroups;
using Telegram.Views.Host;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Telegram.Views.Supergroups
{
    public partial class SupergroupEditTypeArgs
    {
        public SupergroupEditTypeArgs(long chatId, bool isNewChat)
        {
            ChatId = chatId;
            IsNewChat = isNewChat;
        }

        public long ChatId { get; }

        public bool IsNewChat { get; }
    }

    public sealed partial class SupergroupEditTypePage : HostedPage, ISupergroupEditDelegate
    {
        public SupergroupEditTypeViewModel ViewModel => DataContext as SupergroupEditTypeViewModel;

        public SupergroupEditTypePage()
        {
            InitializeComponent();
            Title = Strings.ChannelSettings;
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var container = ScrollingHost.ContainerFromItem(e.ClickedItem) as SelectorItem;
            if (container == null || e.ClickedItem is not UsernameInfo username)
            {
                return;
            }

            if (username.Value == ViewModel.Username)
            {
                Username.Focus(FocusState.Keyboard);
                return;
            }

            var popup = new TeachingTipEx();
            popup.Title = username.IsActive
                ? Strings.UsernameDeactivateLink
                : Strings.UsernameActivateLink;
            popup.Subtitle = username.IsActive
                ? Strings.UsernameDeactivateLinkProfileMessage
                : Strings.UsernameActivateLinkProfileMessage;
            popup.ActionButtonContent = username.IsActive ? Strings.Hide : Strings.Show;
            popup.ActionButtonStyle = BootStrapper.Current.Resources["AccentButtonStyle"] as Style;
            popup.CloseButtonContent = Strings.Cancel;
            popup.PreferredPlacement = TeachingTipPlacementMode.Top;
            popup.Width = popup.MinWidth = popup.MaxWidth = 314;
            popup.Target = /*badge ??*/ container;
            popup.IsLightDismissEnabled = true;
            popup.ShouldConstrainToRootBounds = true;

            popup.ActionButtonClick += (s, args) =>
            {
                popup.IsOpen = false;
                ViewModel.ToggleUsername(username);
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

        #region Delegate

        public void UpdateSupergroup(Chat chat, Supergroup group, SupergroupFullInfo fullInfo)
        {
            if (fullInfo != null)
            {
                ViewModel.InviteLink = fullInfo.InviteLink?.InviteLink;

                if (fullInfo.InviteLink == null && string.IsNullOrEmpty(group.Usernames?.EditableUsername))
                {
                    ViewModel.ClientService.Send(new CreateChatInviteLink(chat.Id, string.Empty, 0, 0, false));
                }
            }

            Title = group.IsChannel ? Strings.ChannelSettingsTitle : Strings.GroupSettingsTitle;
            Subheader.Header = group.IsChannel ? Strings.ChannelTypeHeader : Strings.GroupTypeHeader;
            Subheader.Footer = group.Usernames?.EditableUsername.Length > 0
                ? group.IsChannel
                ? Strings.ChannelPublicInfo
                : Strings.MegaPublicInfo
                : group.IsChannel
                ? Strings.ChannelPrivateInfo
                : Strings.MegaPrivateInfo;

            Public.Content = group.IsChannel ? Strings.ChannelPublic : Strings.MegaPublic;
            Private.Content = group.IsChannel ? Strings.ChannelPrivate : Strings.MegaPrivate;

            UsernameHelp.Footer = group.IsChannel ? Strings.ChannelUsernameHelp : Strings.MegaUsernameHelp;
            PrivateLinkHelp.Footer = group.IsChannel ? Strings.ChannelPrivateLinkHelp : Strings.MegaPrivateLinkHelp;

            JoinToSendMessages.Visibility = group.IsChannel ? Visibility.Collapsed : Visibility.Visible;
            RestrictSavingContent.Footer = group.IsChannel ? Strings.RestrictSavingContentInfoChannel : Strings.RestrictSavingContentInfoGroup;

            ViewModel.Username = group.Usernames?.EditableUsername ?? string.Empty;
            ViewModel.IsPublic = group.Usernames?.EditableUsername.Length > 0;
        }

        public void UpdateBasicGroup(Chat chat, BasicGroup group, BasicGroupFullInfo fullInfo)
        {
            if (fullInfo != null)
            {
                ViewModel.InviteLink = fullInfo.InviteLink?.InviteLink;

                if (fullInfo.InviteLink == null)
                {
                    ViewModel.ClientService.Send(new CreateChatInviteLink(chat.Id, string.Empty, 0, 0, false));
                }
            }

            Title = Strings.GroupSettingsTitle;
            Subheader.Header = Strings.GroupTypeHeader;
            Subheader.Footer = Strings.MegaPrivateInfo;

            Public.Content = Strings.MegaPublic;
            Private.Content = Strings.MegaPrivate;

            UsernameHelp.Footer = Strings.MegaUsernameHelp;
            PrivateLinkHelp.Footer = Strings.MegaPrivateLinkHelp;

            JoinToSendMessages.Visibility = Visibility.Visible;
            RestrictSavingContent.Footer = Strings.RestrictSavingContentInfoGroup;

            ViewModel.Username = string.Empty;
            ViewModel.IsPublic = false;
        }

        public void UpdateChat(Chat chat)
        {
            Username.Prefix = MeUrlPrefixConverter.Convert(ViewModel.ClientService, string.Empty);
        }

        public void UpdateChatTitle(Chat chat) { }
        public void UpdateChatPhoto(Chat chat) { }

        #endregion

        #region Binding

        private bool ConvertHeaderLoad(int count)
        {
            return count != 0;
        }

        private string ConvertAvailable(string username)
        {
            return string.Format(Strings.LinkAvailable, username);
        }

        private string ConvertFooter(bool pubblico)
        {
            if (ViewModel.Chat?.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel)
            {
                return pubblico ? Strings.ChannelPublicInfo : Strings.ChannelPrivateInfo;
            }

            return pubblico ? Strings.MegaPublicInfo : Strings.MegaPrivateInfo;
        }

        private string ConvertJoinToSendMessages(bool joinToSendMessages)
        {
            return joinToSendMessages ? Strings.ChannelSettingsJoinRequestInfo : Strings.ChannelSettingsJoinToSendInfo;
        }

        #endregion

        private void OnDragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            if (e.Items.Count == 1 && e.Items[0] is UsernameInfo username && (username.IsActive || username.IsEditable))
            {
                ScrollingHost.CanReorderItems = true;
                e.Cancel = false;
            }
            else
            {
                ScrollingHost.CanReorderItems = false;
                e.Cancel = true;
            }
        }

        private void OnDragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            ScrollingHost.CanReorderItems = false;

            if (args.DropResult == DataPackageOperation.Move && args.Items.Count == 1 && args.Items[0] is UsernameInfo username)
            {
                ViewModel.ReorderUsernames(username);
            }
        }
    }
}
