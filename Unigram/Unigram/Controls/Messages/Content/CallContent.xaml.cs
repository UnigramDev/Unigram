using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TdWindows;
using Telegram.Api.Helpers;
using Unigram.Common;
using Unigram.Converters;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls.Messages.Content
{
    public sealed partial class CallContent : UserControl, IContent
    {
        public CallContent(MessageViewModel message)
        {
            InitializeComponent();
            UpdateMessage(message);
        }

        public void UpdateMessage(MessageViewModel message)
        {
            var call = message.Content as MessageCall;
            if (call == null)
            {
                return;
            }

            var outgoing = message.IsOutgoing;
            var missed = call.DiscardReason is CallDiscardReasonMissed || call.DiscardReason is CallDiscardReasonDeclined;

            TitleLabel.Text = missed ? (outgoing ? Strings.Android.CallMessageOutgoingMissed : Strings.Android.CallMessageIncomingMissed) : (outgoing ? Strings.Android.CallMessageOutgoing : Strings.Android.CallMessageIncoming);
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

        }
    }
}
