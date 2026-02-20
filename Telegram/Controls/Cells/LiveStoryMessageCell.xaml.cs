//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Cells
{
    // TODO: Proper position for star count in text messages
    //       Trim sender name
    public sealed partial class LiveStoryMessageCell : UserControl
    {
        private GroupCallMessage _message;

        private SpriteVisual _highlight;

        public LiveStoryMessageCell()
        {
            InitializeComponent();
        }

        public void Update(IClientService clientService, GroupCallMessage message, int topDonor)
        {
            _message = message;

            if (message.PaidMessageStarCount > 0 && clientService.TryGetGroupCallMessageLevel(message.PaidMessageStarCount, out GroupCallMessageLevel level))
            {
                StarCount.Text = string.Format("{0}  {1:N0}", Icons.Premium, message.PaidMessageStarCount);
                StarCountRoot.Visibility = Visibility.Visible;
                RootGrid.Background = new SolidColorBrush(level.SecondColor.ToColor(0.6));

                if (message.Text.Text.Length > 0)
                {
                    StarCountRoot.Background = null;
                }
                else
                {
                    StarCountRoot.Background = new SolidColorBrush(level.SecondColor.ToColor(0.6));
                }
            }
            else
            {
                StarCountRoot.Visibility = Visibility.Collapsed;
                RootGrid.Background = new SolidColorBrush(Colors.Transparent);
            }

            bool reaction = string.IsNullOrEmpty(message.Text.Text);
            var title = clientService.GetTitle(message.SenderId, true);
            var formatted = title.AsFormattedText(new TextEntityTypeBold());
            var crown = topDonor switch
            {
                1 => "\uEAEB",
                2 => "\uEAEC",
                3 => "\uEAED",
                _ => null
            };

            if (crown != null)
            {
                formatted = ClientEx.Format("{2} {0} {1}", formatted, reaction ? Icons.ZWNJ : message.Text, ClientEx.CustomEmoji(crown));
            }
            else
            {
                formatted = ClientEx.Format("{0}{2}{1}", formatted, reaction ? Icons.ZWNJ : message.Text, message.IsFromOwner ? "\n" : " ");
            }

            Photo.Source = ProfilePictureSource.MessageSender(clientService, message.SenderId);
            Text.SetText(clientService, formatted);

            if (_highlight != null)
            {
                _highlight.StopAnimation("Opacity");
                _highlight.Opacity = 0;
            }
        }

        public void Highlight()
        {
            if (_highlight == null)
            {
                _highlight = BootStrapper.Current.Compositor.CreateSpriteVisual();
                ElementComposition.SetElementChildVisual(Expiration, _highlight);
            }

            if (RootGrid.Background is not SolidColorBrush brush)
            {
                return;
            }

            var animation = _highlight.Compositor.CreateScalarKeyFrameAnimation();
            animation.Duration = TimeSpan.FromSeconds(2);
            animation.InsertKeyFrame(300f / 2000f, 1f);
            animation.InsertKeyFrame(1700f / 2000f, 1f);
            animation.InsertKeyFrame(1, 0);

            _highlight.StartAnimation("Opacity", animation);

            _highlight.Brush = BootStrapper.Current.Compositor.CreateColorBrush(brush.Color);
            _highlight.Size = ActualSize;
        }
    }

    public partial class LiveStoryMessagePanel : Panel
    {
        private bool _horizontal;

        protected override Size MeasureOverride(Size availableSize)
        {
            var text = Children[0];
            var reaction = Children[1];

            text.Measure(availableSize);
            reaction.Measure(availableSize);

            if (text.DesiredSize.Width + reaction.DesiredSize.Width > availableSize.Width)
            {
                _horizontal = false;
                return new Size(text.DesiredSize.Width, text.DesiredSize.Height + reaction.DesiredSize.Height);
            }

            _horizontal = true;
            return new Size(text.DesiredSize.Width + reaction.DesiredSize.Width, Math.Max(text.DesiredSize.Height, reaction.DesiredSize.Height));
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var text = Children[0];
            var reaction = Children[1];

            if (_horizontal)
            {
                text.Arrange(new(0, 0, text.DesiredSize.Width, text.DesiredSize.Height));
                reaction.Arrange(new(text.DesiredSize.Width, 0, reaction.DesiredSize.Width, reaction.DesiredSize.Height));
            }
            else
            {
                text.Arrange(new(0, 0, text.DesiredSize.Width, text.DesiredSize.Height));
                reaction.Arrange(new(0, text.DesiredSize.Height, reaction.DesiredSize.Width, reaction.DesiredSize.Height));
            }

            return finalSize;
        }
    }
}
