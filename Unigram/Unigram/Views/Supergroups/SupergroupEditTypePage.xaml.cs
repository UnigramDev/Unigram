//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Navigation;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Settings;
using Unigram.ViewModels.Supergroups;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Unigram.Views.Supergroups
{
    public sealed partial class SupergroupEditTypePage : HostedPage, ISupergroupEditDelegate
    {
        public SupergroupEditTypeViewModel ViewModel => DataContext as SupergroupEditTypeViewModel;

        public SupergroupEditTypePage()
        {
            InitializeComponent();
            Title = Strings.Resources.ChannelSettings;
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

            var popup = new TeachingTip();
            popup.Title = username.IsActive
                ? Strings.Resources.UsernameDeactivateLink
                : Strings.Resources.UsernameActivateLink;
            popup.Subtitle = username.IsActive
                ? Strings.Resources.UsernameDeactivateLinkProfileMessage
                : Strings.Resources.UsernameActivateLinkProfileMessage;
            popup.ActionButtonContent = username.IsActive ? Strings.Resources.Hide : Strings.Resources.Show;
            popup.ActionButtonStyle = BootStrapper.Current.Resources["AccentButtonStyle"] as Style;
            popup.CloseButtonContent = Strings.Resources.Cancel;
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

        #region Recycle

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            
            if (args.Item is string username)
            {
                var active = ViewModel.Usernames != null
                    && ViewModel.Usernames.ActiveUsernames.Contains(username);

                var badge = content.Children[0] as Border;
                var title = content.Children[1] as TextBlock;
                var subtitle = content.Children[2] as TextBlock;
                var handle = content.Children[3] as TextBlock;

                badge.Style = BootStrapper.Current.Resources[active ? "AccentCaptionBorderStyle" : "InfoCaptionBorderStyle"] as Style;
                subtitle.Style = BootStrapper.Current.Resources[active ? "AccentCaptionTextBlockStyle" : "InfoCaptionTextBlockStyle"] as Style;

                title.Text = MeUrlPrefixConverter.Convert(ViewModel.ClientService, username, true);
                subtitle.Text = active
                    ? Strings.Resources.UsernameLinkActive
                    : Strings.Resources.UsernameLinkInactive;

                handle.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
            }
            else if (args.Item is Chat chat)
            {
                if (args.Phase == 0)
                {
                    var title = content.Children[1] as TextBlock;
                    title.Text = ViewModel.ClientService.GetTitle(chat);
                }
                else if (args.Phase == 1)
                {
                    if (chat.Type is ChatTypeSupergroup super)
                    {
                        var supergroup = ViewModel.ClientService.GetSupergroup(super.SupergroupId);
                        if (supergroup != null)
                        {
                            var subtitle = content.Children[2] as TextBlock;
                            subtitle.Text = MeUrlPrefixConverter.Convert(ViewModel.ClientService, supergroup.ActiveUsername(), true);
                        }
                    }
                }
                else if (args.Phase == 2)
                {
                    var photo = content.Children[0] as ProfilePicture;
                    photo.SetChat(ViewModel.ClientService, chat, 36);
                }

                if (args.Phase < 2)
                {
                    args.RegisterUpdateCallback(OnContainerContentChanging);
                }
            }

            args.Handled = true;
        }

        #endregion

        #region Delegate

        public void UpdateSupergroup(Chat chat, Supergroup group)
        {
            Title = group.IsChannel ? Strings.Resources.ChannelSettingsTitle : Strings.Resources.GroupSettingsTitle;
            Subheader.Header = group.IsChannel ? Strings.Resources.ChannelTypeHeader : Strings.Resources.GroupTypeHeader;
            Subheader.Footer = group.Usernames?.EditableUsername.Length > 0
                ? group.IsChannel
                ? Strings.Resources.ChannelPublicInfo
                : Strings.Resources.MegaPublicInfo
                : group.IsChannel
                ? Strings.Resources.ChannelPrivateInfo
                : Strings.Resources.MegaPrivateInfo;

            Public.Content = group.IsChannel ? Strings.Resources.ChannelPublic : Strings.Resources.MegaPublic;
            Private.Content = group.IsChannel ? Strings.Resources.ChannelPrivate : Strings.Resources.MegaPrivate;

            UsernameHelp.Footer = group.IsChannel ? Strings.Resources.ChannelUsernameHelp : Strings.Resources.MegaUsernameHelp;
            PrivateLinkHelp.Footer = group.IsChannel ? Strings.Resources.ChannelPrivateLinkHelp : Strings.Resources.MegaPrivateLinkHelp;

            JoinToSendMessages.Visibility = group.IsChannel ? Visibility.Collapsed : Visibility.Visible;
            RestrictSavingContent.Footer = group.IsChannel ? Strings.Resources.RestrictSavingContentInfoChannel : Strings.Resources.RestrictSavingContentInfoGroup;

            ViewModel.Username = group.Usernames?.EditableUsername ?? string.Empty;
            ViewModel.IsPublic = group.Usernames?.EditableUsername.Length > 0;
        }

        public void UpdateSupergroupFullInfo(Chat chat, Supergroup group, SupergroupFullInfo fullInfo)
        {
            ViewModel.InviteLink = fullInfo.InviteLink?.InviteLink;

            if (fullInfo.InviteLink == null && string.IsNullOrEmpty(group.Usernames?.EditableUsername))
            {
                ViewModel.ClientService.Send(new CreateChatInviteLink(chat.Id, string.Empty, 0, 0, false));
            }
        }

        public void UpdateBasicGroup(Chat chat, BasicGroup group)
        {
            Title = Strings.Resources.GroupSettingsTitle;
            Subheader.Header = Strings.Resources.GroupTypeHeader;
            Subheader.Footer = Strings.Resources.MegaPrivateInfo;

            Public.Content = Strings.Resources.MegaPublic;
            Private.Content = Strings.Resources.MegaPrivate;

            UsernameHelp.Footer = Strings.Resources.MegaUsernameHelp;
            PrivateLinkHelp.Footer = Strings.Resources.MegaPrivateLinkHelp;

            JoinToSendMessages.Visibility = Visibility.Visible;
            RestrictSavingContent.Footer = Strings.Resources.RestrictSavingContentInfoGroup;

            ViewModel.Username = string.Empty;
            ViewModel.IsPublic = false;
        }

        public void UpdateBasicGroupFullInfo(Chat chat, BasicGroup group, BasicGroupFullInfo fullInfo)
        {
            ViewModel.InviteLink = fullInfo.InviteLink?.InviteLink;

            if (fullInfo.InviteLink == null)
            {
                ViewModel.ClientService.Send(new CreateChatInviteLink(chat.Id, string.Empty, 0, 0, false));
            }
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
            return string.Format(Strings.Resources.LinkAvailable, username);
        }

        private string ConvertFooter(bool pubblico)
        {
            if (ViewModel.Chat?.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel)
            {
                return pubblico ? Strings.Resources.ChannelPublicInfo : Strings.Resources.ChannelPrivateInfo;
            }

            return pubblico ? Strings.Resources.MegaPublicInfo : Strings.Resources.MegaPrivateInfo;
        }

        private string ConvertJoinToSendMessages(bool joinToSendMessages)
        {
            return joinToSendMessages ? Strings.Resources.ChannelSettingsJoinRequestInfo : Strings.Resources.ChannelSettingsJoinToSendInfo;
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
