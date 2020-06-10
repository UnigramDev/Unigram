using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls.Messages.Content
{
    public sealed partial class CallContent : UserControl, IContent
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        public CallContent(MessageViewModel message)
        {
            InitializeComponent();
            UpdateMessage(message);
        }

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;
            var call = message.Content as MessageCall;
            if (call == null)
            {
                return;
            }

            var outgoing = message.IsOutgoing;
            var missed = call.DiscardReason is CallDiscardReasonMissed || call.DiscardReason is CallDiscardReasonDeclined;

            TitleLabel.Text = missed ? (outgoing ? Strings.Resources.CallMessageOutgoingMissed : Strings.Resources.CallMessageIncomingMissed) : (outgoing ? Strings.Resources.CallMessageOutgoing : Strings.Resources.CallMessageIncoming);
            ReasonGlyph.Text = outgoing ? "\uE60B\u00A0" : "\uE60C\u00A0";
            DateLabel.Text = BindConvert.Current.Date(message.Date);

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

        public bool IsValid(MessageContent content, bool primary)
        {
            return content is MessageCall;
        }

        private void ToolTip_Opened(object sender, RoutedEventArgs e)
        {
            var tooltip = sender as ToolTip;
            if (tooltip != null && _message != null)
            {
                var date = BindConvert.Current.DateTime(_message.Date);
                var text = $"{BindConvert.Current.LongDate.Format(date)} {BindConvert.Current.LongTime.Format(date)}";

                tooltip.Content = text;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            _message.Delegate.Call(_message);
        }
    }
}
