using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace Telegram.Controls.Cells
{
    public sealed partial class ForumTopicShareCell : GridEx
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
            }
            else
            {
                Animated.Source = null;
            }

            TitleLabel.Text = topic.Info.Name;
        }
    }
}
