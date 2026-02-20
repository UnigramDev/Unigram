//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Converters;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Cells
{
    public sealed partial class ForumTopicShareCell : Grid
    {
        public ForumTopicShareCell()
        {
            InitializeComponent();
        }

        public void UpdateCell(IClientService clientService, ForumTopic topic)
        {
            if (topic.Info.Icon.CustomEmojiId != 0)
            {
                Animated.Source = new CustomEmojiFileSource(clientService, topic.Info.Icon.CustomEmojiId);
                IconRoot.Visibility = Visibility.Collapsed;
                General.Visibility = Visibility.Collapsed;
            }
            else if (topic.Info.IsGeneral)
            {
                Animated.Source = null;
                IconRoot.Visibility = Visibility.Collapsed;
                General.Visibility = Visibility.Visible;
            }
            else
            {
                Animated.Source = null;
                IconRoot.Visibility = Visibility.Visible;
                General.Visibility = Visibility.Collapsed;

                var brush = ForumTopicCell.GetIconGradient(topic.Info.Icon);

                IconPath.Fill = brush;
                IconPath.Stroke = new SolidColorBrush(brush.GradientStops[1].Color);
                IconText.Text = InitialNameStringConverter.Convert(topic.Info.Name);
            }

            TitleLabel.Text = topic.Info.Name;
        }

        public void UpdateCell(IClientService clientService, DirectMessagesChatTopic topic)
        {
            Animated.Source = null;
            IconRoot.Visibility = Visibility.Collapsed;
            General.Visibility = Visibility.Collapsed;

            TitleLabel.Text = clientService.GetTitle(topic.SenderId);
            Photo.Source = ProfilePictureSource.MessageSender(clientService, topic.SenderId);
        }
    }
}
