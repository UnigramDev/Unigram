using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Services;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls.Cells
{
    public sealed partial class GroupCallParticipantGridCell : UserControl
    {
        private GroupCallParticipant _participant;
        private GroupCallParticipantVideoInfo _videoInfo;

        public GroupCallParticipantGridCell(ICacheService cacheService, GroupCallParticipant participant, GroupCallParticipantVideoInfo videoInfo)
        {
            InitializeComponent();
            UpdateGroupCallParticipant(cacheService, participant, videoInfo);
        }

        public bool IsMatch(GroupCallParticipant participant, GroupCallParticipantVideoInfo videoInfo)
        {
            return participant != null && participant.ParticipantId.IsEqual(ParticipantId) && _videoInfo.EndpointId == _videoInfo.EndpointId;
        }

        public CanvasControl Surface
        {
            get => CanvasRoot.Child as CanvasControl;
            set => CanvasRoot.Child = value;
        }

        public GroupCallParticipant Participant => _participant;
        public MessageSender ParticipantId => _participant.ParticipantId;

        public GroupCallParticipantVideoInfo VideoInfo => _videoInfo;
        public string EndpointId => _videoInfo.EndpointId;

        public bool IsPinned
        {
            get => Pin.IsChecked == true;
            set => Pin.IsChecked = value;
        }

        public event EventHandler TogglePinned;

        public void UpdateGroupCallParticipant(ICacheService cacheService, GroupCallParticipant participant, GroupCallParticipantVideoInfo videoInfo)
        {
            _participant = participant;
            _videoInfo = videoInfo;

            if (cacheService.TryGetUser(participant.ParticipantId, out User user))
            {
                Title.Text = user.GetFullName();
            }
            else if (cacheService.TryGetChat(participant.ParticipantId, out Chat chat))
            {
                Title.Text = cacheService.GetTitle(chat);
            }

            if (participant.IsHandRaised)
            {
                LayoutRoot.BorderBrush = null;
                Glyph.Text = Icons.EmojiHand;
            }
            else if (participant.IsSpeaking)
            {
                LayoutRoot.BorderBrush = new SolidColorBrush { Color = Color.FromArgb(0xFF, 0x33, 0xc6, 0x59) };
                Glyph.Text = Icons.MicOn;
            }
            else if (participant.IsCurrentUser)
            {
                LayoutRoot.BorderBrush = null;
                Glyph.Text = participant.IsMutedForAllUsers || participant.IsMutedForCurrentUser ? Icons.MicOff : Icons.MicOn;
            }
            else
            {
                LayoutRoot.BorderBrush = null;
                Glyph.Text = participant.IsMutedForAllUsers || participant.IsMutedForCurrentUser ? Icons.MicOff : Icons.MicOn;
            }
        }

        private bool _collapsed;

        public void ShowHideInfo(bool show)
        {
            if (_collapsed == !show)
            {
                return;
            }

            _collapsed = !show;

            var anim = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            anim.InsertKeyFrame(0, show ? 0 : 1);
            anim.InsertKeyFrame(1, show ? 1 : 0);

            var info = ElementCompositionPreview.GetElementVisual(Info);
            var pin = ElementCompositionPreview.GetElementVisual(PinRoot);

            info.StartAnimation("Opacity", anim);
            pin.StartAnimation("Opacity", anim);
        }

        private void Pin_Click(object sender, RoutedEventArgs e)
        {
            TogglePinned?.Invoke(this, EventArgs.Empty);
        }
    }
}
