//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Telegram.ViewModels.Chats;

namespace Telegram.Views.Chats
{
    public partial class ChatInviteLinksArgs
    {
        public ChatInviteLinksArgs(long chatId, long creatorUserId)
        {
            ChatId = chatId;
            CreatorUserId = creatorUserId;
        }

        public long ChatId { get; }

        public long CreatorUserId { get; }
    }

    public sealed partial class ChatInviteLinksPage : HostedPage
    {
        public ChatInviteLinksViewModel ViewModel => DataContext as ChatInviteLinksViewModel;

        public ChatInviteLinksPage()
        {
            InitializeComponent();
            Title = Strings.InviteLinks;
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ChatInviteLinkCount inviteLinkCount)
            {
                ViewModel.OpenInviteLinkCount(inviteLinkCount);
            }
            else if (e.ClickedItem is ChatInviteLink inviteLink)
            {
                ViewModel.OpenInviteLink(inviteLink);
            }
        }

        #region Context menu

        private void OnContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var scrollingHost = sender.GetParent<Selector>();
            var inviteLink = scrollingHost?.ItemFromContainer(sender) as ChatInviteLink;

            if (inviteLink == null)
            {
                return;
            }

            var flyout = new MenuFlyout();

            if (inviteLink.IsRevoked)
            {
                flyout.CreateFlyoutItem(ViewModel.DeleteLink, inviteLink, Strings.Delete, Icons.Delete, destructive: true);
            }
            else
            {
                flyout.CreateFlyoutItem(ViewModel.CopyLink, inviteLink, Strings.CopyLink, Icons.DocumentCopy);
                flyout.CreateFlyoutItem(ViewModel.ShareLink, inviteLink, Strings.ShareLink, Icons.Share);
                flyout.CreateFlyoutItem(ViewModel.EditLink, inviteLink, Strings.EditLink, Icons.Edit);
                flyout.CreateFlyoutItem(ViewModel.RevokeLink, inviteLink, Strings.RevokeLink, Icons.Delete, destructive: true);
            }

            flyout.ShowAt(sender, args);
        }

        #endregion

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TableListViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.ContextRequested += OnContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is ChatInviteLinkCell chatInviteLinkCell)
            {
                if (args.Item is ChatInviteLink inviteLink)
                {
                    chatInviteLinkCell.UpdateInviteLink(ViewModel.ClientService, inviteLink);
                }
                else if (args.Item is ChatInviteLinkCount inviteLinkCount)
                {
                    chatInviteLinkCell.UpdateInviteLinkCount(ViewModel.ClientService, inviteLinkCount);
                }
            }
        }

        #endregion

        #region Binding

        private string ConvertHeadline(bool channel)
        {
            return channel
                ? Strings.PrimaryLinkHelpChannel
                : Strings.PrimaryLinkHelp;
        }

        private string ConvertInviteLinkFooter(ChatInviteLink inviteLink)
        {
            if (inviteLink?.MemberCount > 0)
            {
                return string.Format("**{0}**", Locale.Declension(Strings.R.PeopleJoined, inviteLink.MemberCount));
            }

            return string.Empty;
        }

        private string ConvertInviteLink(ChatInviteLink inviteLink)
        {
            return inviteLink?.InviteLink.Replace("https://", string.Empty);
        }

        private string ConvertNewLinkFooter(int count, bool channel)
        {
            if (count > 0)
            {
                return string.Empty;
            }

            return channel
                ? Strings.ManageLinksInfoHelpPaid
                : Strings.ManageLinksInfoHelp;
        }

        #endregion

        private void More_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(RevokeLink, Strings.RevokeLink, Icons.Delete, destructive: true);
            flyout.ShowAt(sender as UIElement, FlyoutPlacementMode.BottomEdgeAlignedRight);
        }

        private void RevokeLink()
        {
            ViewModel.RevokeLink(ViewModel.InviteLink);
        }

        private void CopyLink_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.CopyLink(ViewModel.InviteLink);
        }

        private void ShareLink_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ShareLink(ViewModel.InviteLink);
        }

        private void MemberCount_Click(object sender, TextUrlClickEventArgs e)
        {
            ViewModel.OpenInviteLink(ViewModel.InviteLink);
        }
    }
}
