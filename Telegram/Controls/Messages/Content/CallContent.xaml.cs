//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;

namespace Telegram.Controls.Messages.Content
{
    public sealed class CallContent : Control, IContent
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        public CallContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(CallContent);
        }

        #region InitializeComponent

        private Border Texture;
        private GlyphHyperlinkButton Button;
        private TextBlock TitleLabel;
        private ToolTip Tip;
        private Run ReasonGlyph;
        private Run DateLabel;
        private Run DurationLabel;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            Texture = GetTemplateChild(nameof(Texture)) as Border;
            Button = GetTemplateChild(nameof(Button)) as GlyphHyperlinkButton;
            TitleLabel = GetTemplateChild(nameof(TitleLabel)) as TextBlock;
            Tip = GetTemplateChild(nameof(Tip)) as ToolTip;
            ReasonGlyph = GetTemplateChild(nameof(ReasonGlyph)) as Run;
            DateLabel = GetTemplateChild(nameof(DateLabel)) as Run;
            DurationLabel = GetTemplateChild(nameof(DurationLabel)) as Run;

            Button.Click += Button_Click;
            Tip.Opened += ToolTip_Opened;

            _templateApplied = true;

            if (_message != null)
            {
                UpdateMessage(_message);
            }
        }

        #endregion

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;

            var call = message.Content as MessageCall;
            if (call == null || !_templateApplied)
            {
                return;
            }

            var outgoing = message.IsOutgoing;
            var missed = call.DiscardReason is CallDiscardReasonMissed or CallDiscardReasonDeclined;

            Button.Glyph = call.IsVideo ? Icons.VideoFilled24 : Icons.PhoneFilled24;
            //Button.FontSize = call.IsVideo ? 24 : 20;

            TitleLabel.Text = call.ToOutcomeText(message.IsOutgoing);
            ReasonGlyph.Text = outgoing ? "\uE60B\u00A0" : "\uE60C\u00A0";
            DateLabel.Text = Formatter.Date(message.Date);

            if (call.Duration > 0 && !missed)
            {
                DurationLabel.Text = ", " + Locale.FormatCallDuration(call.Duration);
            }
            else
            {
                DurationLabel.Text = string.Empty;
            }

            VisualStateManager.GoToState(this, missed ? "Missed" : "Default", false);
        }

        public void Recycle()
        {
            _message = null;
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            return content is MessageCall;
        }

        private void ToolTip_Opened(object sender, RoutedEventArgs e)
        {
            var tooltip = sender as ToolTip;
            if (tooltip != null && _message != null)
            {
                var date = Formatter.ToLocalTime(_message.Date);
                var text = $"{Formatter.LongDate.Format(date)} {Formatter.LongTime.Format(date)}";

                tooltip.Content = text;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var call = _message?.Content as MessageCall;
            if (call == null)
            {
                return;
            }

            _message.Delegate.Call(_message, call.IsVideo);
        }
    }
}
