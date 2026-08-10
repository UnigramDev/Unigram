//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Controls.Cells;
using Telegram.Converters;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Messages.Service
{
    public sealed partial class MessageHeaderMessageTopicContent : MessageService
    {
        public MessageHeaderMessageTopicContent()
        {
            InitializeComponent();
        }

        protected override void UpdateContent(MessageViewModel message)
        {
            UpdateMessageTopic();
        }

        /// <summary>
        /// Refreshes the header from the topic itself, for when the topic is renamed or
        /// its icon changes while the header is on screen.
        /// </summary>
        public void UpdateMessageTopic()
        {
            if (Message is not MessageViewModel message)
            {
                return;
            }

            if (message.ClientService.TryGetForumTopic(message.ChatId, message.TopicId, out ForumTopic topic))
            {
                TitleLabel.Text = topic.Info.Name;
                Photo.Source = null;

                if (topic.Info.IsGeneral || topic.Info.Icon.CustomEmojiId != 0)
                {
                    TypeIcon.SetStatus(message.ClientService, topic.Info.Icon);
                    IconRoot.Visibility = Visibility.Collapsed;
                }
                else
                {
                    TypeIcon.ClearStatus();
                    IconRoot.Visibility = Visibility.Visible;

                    var brush = ForumTopicCell.GetIconGradient(topic.Info.Icon);

                    IconPath.Fill = brush;
                    IconPath.Stroke = new SolidColorBrush(brush.GradientStops[1].Color);
                    IconText.Text = InitialNameStringConverter.Convert(topic.Info.Name);
                }
            }
            else if (message.ClientService.TryGetDirectMessagesChatTopic(message.ChatId, message.TopicId, out DirectMessagesChatTopic directMessagesChatTopic))
            {
                TitleLabel.Text = message.ClientService.GetTitle(directMessagesChatTopic.SenderId);
                Photo.Source = ProfilePictureSource.MessageSender(message.ClientService, directMessagesChatTopic.SenderId);

                TypeIcon.ClearStatus();
                IconRoot.Visibility = Visibility.Collapsed;
            }

            AutomationProperties.SetName(this, TitleLabel.Text);
        }

        public override void Recycle()
        {
            base.Recycle();

            Photo.Source = null;
            TypeIcon.ClearStatus();
        }

        private void Service_Click(object sender, RoutedEventArgs e)
        {
            if (Message?.Delegate != null)
            {
                // force: false, so tapping the header of the topic already being viewed
                // doesn't push a second navigation.
                Message.Delegate.NavigationService.NavigateToChat(Message.Chat, topic: Message.TopicId, force: false);
            }
        }
    }
}
