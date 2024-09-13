using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;

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
