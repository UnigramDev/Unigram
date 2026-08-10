//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml;

namespace Telegram.Controls.Messages.Service
{
    // Shared by the message and by its event log counterpart, which has no action.
    public sealed partial class MessageChatSetBackgroundContent : MessageService
    {
        public MessageChatSetBackgroundContent()
        {
            InitializeComponent();
        }

        protected override void UpdateContent(MessageViewModel message)
        {
            if (message.Content is MessageChatSetBackground chatSetBackground)
            {
                Photo.UpdateSource(message.ClientService, chatSetBackground.Background.Background, true);
                View.Visibility = message.IsOutgoing || message.Chat.Type is not ChatTypePrivate
                    ? Visibility.Collapsed
                    : Visibility.Visible;

                if (message.IsOutgoing)
                {
                    return;
                }

                var userFull = message.ClientService.GetUserFull(message.Chat);
                var sameBackground = chatSetBackground.Background.Background.Id == message.Chat.Background?.Background.Id;

                ViewLabel.Text = sameBackground && (userFull == null || userFull.SetChatBackground)
                    ? Strings.RemoveWallpaperAction
                    : Strings.ViewWallpaperAction;
            }
            else if (message.Content is MessageChatEvent { Action: ChatEventBackgroundChanged backgroundChanged })
            {
                if (backgroundChanged.NewBackground == null)
                {
                    return;
                }

                Photo.UpdateSource(message.ClientService, backgroundChanged.NewBackground.Background, true);
                View.Visibility = Visibility.Collapsed;
            }
        }

        private void Service_Click(object sender, RoutedEventArgs e)
        {
            if (Message?.Delegate != null)
            {
                Message.Delegate.ExecuteServiceMessage(Message);
            }
        }
    }
}
