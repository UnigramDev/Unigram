using Microsoft.Graphics.Canvas.UI.Xaml;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Services;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls.Cells
{
    public sealed partial class GroupCallParticipantGridCell : UserControl
    {
        private GroupCallParticipant _participant;

        public GroupCallParticipantGridCell(ICacheService cacheService, GroupCallParticipant participant)
        {
            InitializeComponent();
            UpdateGroupCallParticipant(cacheService, participant);
        }

        public CanvasControl Surface
        {
            get => CanvasRoot.Child as CanvasControl;
            set => CanvasRoot.Child = value;
        }

        public GroupCallParticipant Participant => _participant;
        public MessageSender ParticipantId => _participant.ParticipantId;

        public void UpdateGroupCallParticipant(ICacheService cacheService, GroupCallParticipant participant)
        {
            _participant = participant;

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

        public void RemoveFromVisualTree()
        {
            if (Parent is Panel panel)
            {
                panel.Children.Remove(this);
            }

            CanvasRoot.Child = null;
        }
    }
}
