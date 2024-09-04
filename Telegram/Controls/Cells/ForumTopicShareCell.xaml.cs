using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

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
