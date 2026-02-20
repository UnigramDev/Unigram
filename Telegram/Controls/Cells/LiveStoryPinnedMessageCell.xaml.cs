//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Services;
using Telegram.Td;
using Telegram.Td.Api;
using Windows.UI.Composition;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Cells
{
    // TODO: Trim sender name
    public sealed partial class LiveStoryPinnedMessageCell : HyperlinkButton
    {
        private GroupCallMessage _message;
        public GroupCallMessage Message => _message;

        public LiveStoryPinnedMessageCell()
        {
            InitializeComponent();
        }

        private CompositionPropertySet _props;

        public void Update(IClientService clientService, GroupCallMessage message, int topDonor)
        {
            _message = message;

            if (clientService.TryGetGroupCallMessageLevel(message.PaidMessageStarCount, out GroupCallMessageLevel level))
            {
                RootGrid.Background = new SolidColorBrush(level.SecondColor.ToColor(0.6));
                Expiration.Background = new SolidColorBrush(level.SecondColor.ToColor());
                UpdateExpiration(clientService, message.Date, level.PinDuration);
            }

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
                formatted = ClientEx.Format("{1} {0}", formatted, ClientEx.CustomEmoji(crown));
            }

            Photo.Source = ProfilePictureSource.MessageSender(clientService, message.SenderId);
            Text.SetText(clientService, formatted);
        }

        private void UpdateExpiration(IClientService clientService, int date, int expiration)
        {
            var visual = ElementComposition.GetElementVisual(Expiration);
            var compositor = visual.Compositor;

            var clip = (visual.Clip ??= compositor.CreateInsetClip()) as InsetClip;

            if (_props == null)
            {
                _props = compositor.CreatePropertySet();
                _props.InsertScalar("Progress", 0);
            }

            var minimum = date;
            var maximum = date + expiration;
            var now = clientService.UnixTime;
            var value = Math.Clamp(((float)now - minimum) / (maximum - minimum), 0, 1);

            var duration = TimeSpan.FromSeconds(maximum - now);
            if (duration > TimeSpan.Zero)
            {
                var linearEasing = compositor.CreateLinearEasingFunction();
                var animation = compositor.CreateScalarKeyFrameAnimation();
                animation.Duration = duration;
                animation.InsertKeyFrame(0, value, linearEasing);
                animation.InsertKeyFrame(1, 1, linearEasing);

                _props.StartAnimation("Progress", animation);
            }

            var progressAnimation = compositor.CreateExpressionAnimation($"visual.Size.X - ((1 - _.Progress) * visual.Size.X)");
            progressAnimation.SetReferenceParameter("_", _props);
            progressAnimation.SetReferenceParameter("visual", visual);

            clip.StartAnimation("RightInset", progressAnimation);
        }
    }
}
