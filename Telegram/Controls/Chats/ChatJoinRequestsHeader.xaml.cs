//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Linq;
using Telegram.Common;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.Views;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Chats
{
    public sealed partial class ChatJoinRequestsHeader : HyperlinkButton
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        private ChatView _chatView;
        private Chat _chat;

        public ChatJoinRequestsHeader()
        {
            InitializeComponent();

            _collapsed = new SlidePanel.SlideState(this, false, 40);
        }

        public float AnimatedHeight => _collapsed ? 0 : 40;

        public void InitializeParent(ChatView chatView)
        {
            _chatView = chatView;
        }

        private void RecentUsers_RecentUserHeadChanged(ProfilePicture sender, MessageSender messageSender)
        {
            sender.Source = ProfilePictureSource.MessageSender(ViewModel.ClientService, messageSender);
        }

        public bool UpdateChat(Chat chat)
        {
            var visible = true;
            var channel = chat.Type is ChatTypeSupergroup super && super.IsChannel;

            if (chat.PendingJoinRequests?.TotalCount > 0)
            {
                ShowHide(true);

                if (chat.PendingJoinRequests.UserIds.Count < 3
                    && chat.PendingJoinRequests.UserIds.Count < chat.PendingJoinRequests.TotalCount)
                {
                    ViewModel.ClientService.Send(new GetChatJoinRequests(chat.Id, string.Empty, string.Empty, null, 3));
                }

                Label.Text = Locale.Declension(Strings.R.JoinRequests, chat.PendingJoinRequests.TotalCount);

                var destination = RecentUsers.Items;
                var origin = chat.PendingJoinRequests.UserIds;

                if (destination.Count > 0 && _chat?.Id == chat.Id)
                {
                    destination.ReplaceDiff(origin.Select(x => new MessageSenderUser(x)));
                }
                else
                {
                    destination.ReplaceWith(origin.Select(x => new MessageSenderUser(x)));
                }
            }
            else
            {
                ShowHide(false);
                visible = false;
            }

            _chat = chat;
            return visible;
        }

        private SlidePanel.SlideState _collapsed;

        public void ShowHide(bool show)
        {
            if (_collapsed != show)
            {
                return;
            }

            _collapsed.IsVisible = show;
            _chatView.UpdateMessagesHeaderPadding();
        }
    }
}
