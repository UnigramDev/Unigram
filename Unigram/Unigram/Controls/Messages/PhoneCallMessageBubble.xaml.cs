using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.TL;
using Windows.ApplicationModel.Resources;
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

namespace Unigram.Controls.Messages
{
    public sealed partial class PhoneCallMessageBubble : PhoneCallMessageBubbleBase
    {
        public PhoneCallMessageBubble()
        {
            InitializeComponent();
            _layoutRoot = LayoutRoot;

            DataContextChanged += (s, args) =>
            {
                if (ViewModel != null && ViewModel != _oldValue) Bindings.Update();
                if (ViewModel == null) Bindings.StopTracking();

                _oldValue = ViewModel;
            };
        }

        private string ConvertTitle(TLMessageService message)
        {
            if (message.Action is TLMessageActionPhoneCall phoneCallAction)
            {
                var loader = ResourceLoader.GetForCurrentView("Resources");

                var outgoing = message.IsOut;
                var missed = phoneCallAction.Reason is TLPhoneCallDiscardReasonMissed || phoneCallAction.Reason is TLPhoneCallDiscardReasonBusy;

                return loader.GetString(missed ? (outgoing ? "CallCanceled" : "CallMissed") : (outgoing ? "CallOutgoing" : "CallIncoming"));
            }

            return string.Empty;
        }

        private string ConvertDuration(TLMessageActionBase action)
        {
            if (action is TLMessageActionPhoneCall phoneCallAction)
            {
                var missed = phoneCallAction.Reason is TLPhoneCallDiscardReasonMissed || phoneCallAction.Reason is TLPhoneCallDiscardReasonBusy;
                if (!missed)
                {
                    var duration = Convert.CallDuration(phoneCallAction.Duration ?? 0);
                    return $", {duration}";
                }
            }

            return string.Empty;
        }
    }
}
