//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.UI.Xaml.Controls;
using System;
using Telegram.Services.Updates;
using Telegram.Streams;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Messages.Content
{
    public sealed partial class DiceContent : HyperlinkButton, IContentWithFile, IPlayerView
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        public DiceContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(DiceContent);
            Click += Button_Click;
        }

        #region InitializeComponent

        private AnimatedImage Player;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            Player = GetTemplateChild(nameof(Player)) as AnimatedImage;

            Player.Ready += OnReady;

            _templateApplied = true;

            if (_message != null)
            {
                UpdateMessage(_message);
            }
        }

        #endregion

        public void UpdateMessage(MessageViewModel message)
        {
            var previous = _message;
            _message = message;

            var dice = message.Content as MessageDice;
            if (dice == null || !_templateApplied)
            {
                return;
            }

            var zoom = message.ClientService.Config.GetNamedNumber("emojies_animated_zoom", 0.625f);

            Width = Player.Width = 180 * zoom;
            Height = Player.Height = 180 * zoom;

            // Reference equality, and deliberately not the message id: a dice that has just been
            // sent comes back with a new id on the same view model, and that is exactly the case
            // whose roll has to carry on rather than start over. Handing the result to the source
            // lets the animation take it at its next loop; a new source would restart it.
            if (previous != message || Player.Source is not DiceFileSource source)
            {
                source = new DiceFileSource(message.ClientService, dice.InitialState, dice.FinalState);

                using (Player.BeginBatchUpdate())
                {
                    Player.FrameSize = new Size(180 * zoom, 180 * zoom);
                    Player.Source = source;
                }
            }

            source.IsContentUnread = message.GeneratedContentUnread;
            source.SetFinalState(dice.FinalState);

            Player.PositionChanged -= OnPositionChanged;

            if (message.IsOutgoing &&
                message.GeneratedContentUnread &&
                dice.IsFinalState() &&
                dice.SuccessAnimationFrameNumber != 0)
            {
                Player.PositionChanged += OnPositionChanged;
            }

            if (dice.GetState().IsDownloadingCompleted())
            {
                message.Delegate.ViewVisibleMessages();
            }
        }

        public void UpdateMessageContentOpened(MessageViewModel message) { }

        /// <summary>
        /// The first frame is on screen, so the dice is ready to play - which it will not do until
        /// the chat counts it among the visible messages and hands it a viewport. Asked for here
        /// and not only in <see cref="UpdateMessage"/>, because a sticker that still had to be
        /// downloaded becomes ready long after the message was laid out.
        /// </summary>
        private void OnReady(object sender, EventArgs e)
        {
            _message?.Delegate.ViewVisibleMessages();
        }

        private void OnPositionChanged(object sender, AnimatedImagePositionChangedEventArgs e)
        {
            if (_message?.Content is MessageDice dice && dice.SuccessAnimationFrameNumber == e.Position)
            {
                _message.Delegate.Aggregator.Publish(new UpdateConfetti());
                Player.PositionChanged -= OnPositionChanged;
            }
        }

        public void Recycle()
        {
            _message = null;
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            if (content is MessageDice)
            {
                return true;
            }

            return false;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var dice = _message?.Content as MessageDice;
            if (dice == null)
            {
                return;
            }

            string text;
            switch (dice.Emoji)
            {
                case "\uD83C\uDFB2":
                    text = Strings.DiceInfo2;
                    break;
                case "\uD83C\uDFAF":
                    text = Strings.DartInfo;
                    break;
                default:
                    text = string.Format(Strings.DiceEmojiInfo, dice.Emoji);
                    break;
            }

            var formatted = ClientEx.ParseMarkdown(text);
            ToastPopup.Show(this, formatted, _message.IsOutgoing && !_message.IsChannelPost ? TeachingTipPlacementMode.TopLeft : TeachingTipPlacementMode.TopRight);
        }

        #region IPlaybackView

        // 0 while the dice is still rolling, which is what tells the chat it may pause this one
        // when it scrolls out of view. A result that is playing out is a one-shot and is left to
        // finish.
        public int LoopCount => Player?.Source is DiceFileSource { IsLooping: true } ? 0 : 1;

        public void ViewportChanged(bool within)
        {
            // Handed over whole rather than turned into Play/Pause here: the player tracks the
            // viewport itself, and defers building the outline placeholder until it is told the
            // control is actually on screen.
            Player?.ViewportChanged(within);
        }

        #endregion
    }
}
