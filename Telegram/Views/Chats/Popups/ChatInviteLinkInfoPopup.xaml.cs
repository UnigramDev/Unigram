//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Telegram.ViewModels.Chats;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;

namespace Telegram.Views.Chats.Popups
{
    public sealed partial class ChatInviteLinkInfoPopup : ContentPopup, IIncrementalCollectionOwner
    {
        private readonly ChatInviteLinksViewModel _viewModel;
        private readonly long _chatId;
        private readonly ChatInviteLink _inviteLink;

        public ChatInviteLinkInfoPopup(ChatInviteLinksViewModel viewModel, long chatId, ChatInviteLink inviteLink)
        {
            InitializeComponent();

            _viewModel = viewModel;
            _chatId = chatId;
            _inviteLink = inviteLink;

            Items = new IncrementalCollection<ChatInviteLinkMember>(this);
            ScrollingHost.ItemsSource = Items;

            Title = inviteLink.Name.Length > 0
                ? inviteLink.Name
                : Strings.InviteLink;

            InviteLink.Content = inviteLink.InviteLink.Replace("https://", string.Empty);

            if (inviteLink.IsRevoked)
            {
                More.Visibility = Visibility.Collapsed;
                CopyLink.Visibility = Visibility.Collapsed;
                ShareLink.Visibility = Visibility.Collapsed;
            }
            else
            {
                DeleteLink.Visibility = Visibility.Collapsed;
            }

            if (viewModel.ClientService.TryGetUser(inviteLink.CreatorUserId, out User user))
            {
                Creator.UpdateUser(viewModel.ClientService, user, 36);
            }

            if (inviteLink.MemberCount > 0)
            {
                if (inviteLink.MemberLimit > 0)
                {
                    SubtitleText.Text = string.Format("{0}, {1}", Locale.Declension(Strings.R.PeopleJoined, inviteLink.MemberCount), Locale.Declension(Strings.R.PeopleJoinedRemaining, inviteLink.MemberLimit - inviteLink.MemberCount));
                }
                else
                {
                    SubtitleText.Text = Locale.Declension(Strings.R.PeopleJoined, inviteLink.MemberCount);
                }
            }
            else if (inviteLink.MemberLimit > 0)
            {
                SubtitleText.Text = Locale.Declension(Strings.R.CanJoin, inviteLink.MemberLimit);
            }
            else
            {
                SubtitleText.Visibility = Visibility.Collapsed;
            }
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is ProfileCell cell && args.Item is ChatInviteLinkMember member)
            {
                cell.UpdateChatInviteLinkMember(_viewModel.ClientService, args, OnContainerContentChanging);
            }
        }

        private void More_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(EditLink, Strings.EditLink, Icons.Edit);
            flyout.CreateFlyoutItem(RevokeLink, Strings.RevokeLink, Icons.Delete, destructive: true);
            flyout.ShowAt(sender as UIElement, FlyoutPlacementMode.BottomEdgeAlignedRight);
        }

        private void EditLink()
        {
            Hide();
            _viewModel.EditLink(_inviteLink);
        }

        private void RevokeLink()
        {
            Hide();
            _viewModel.RevokeLink(_inviteLink);
        }

        private void CopyLink_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.CopyLink(_inviteLink);
        }

        private void ShareLink_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            _viewModel.ShareLink(_inviteLink);
        }

        private void DeleteLink_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            _viewModel.DeleteLink(_inviteLink);
        }

        private ChatInviteLinkMember _offsetMember;

        public IncrementalCollection<ChatInviteLinkMember> Items { get; }

        public async Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            var total = 0u;

            var response = await _viewModel.ClientService.SendAsync(new GetChatInviteLinkMembers(_chatId, _inviteLink.InviteLink, false, _offsetMember, 100));
            if (response is ChatInviteLinkMembers members)
            {
                foreach (var member in members.Members)
                {
                    _offsetMember = member;
                    Items.Add(member);
                }
            }

            HasMoreItems = total > 0;

            return new LoadMoreItemsResult
            {
                Count = total
            };
        }

        public bool HasMoreItems { get; private set; } = true;

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ChatInviteLinkMember member)
            {
                Hide();
                _viewModel.NavigationService.NavigateToUser(member.UserId);
            }
        }
    }
}
