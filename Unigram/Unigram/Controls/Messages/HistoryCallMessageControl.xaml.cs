using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Converters;
using Unigram.Strings;
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
    public sealed partial class HistoryCallMessageControl : PhoneCallMessageBubbleBase
    {
        public HistoryCallMessageControl()
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

        private ImageSource ConvertFromPhoto(TLMessageService message)
        {
            if (message.IsOut)
            {
                var user = InMemoryCacheService.Current.GetUser(message.ToId.Id);
                if (user != null)
                {
                    return DefaultPhotoConverter.Convert(user, true) as ImageSource;
                }
            }

            return DefaultPhotoConverter.Convert(message.From, true) as ImageSource;
        }

        private string ConvertFromName(TLMessageService message)
        {
            if (message.IsOut)
            {
                var user = InMemoryCacheService.Current.GetUser(message.ToId.Id);
                if (user != null)
                {
                    return user.DisplayName;
                }
            }

            return message.From?.DisplayName;
        }

        private string ConvertTitle(TLMessageService message)
        {
            if (message.Action is TLMessageActionPhoneCall phoneCallAction)
            {
                var loader = ResourceLoader.GetForCurrentView("Resources");
                var text = string.Empty;

                var outgoing = message.IsOut;
                var missed = phoneCallAction.Reason is TLPhoneCallDiscardReasonMissed || phoneCallAction.Reason is TLPhoneCallDiscardReasonBusy;

                var type = loader.GetString(missed ? (outgoing ? "CallCanceled" : "CallMissed") : (outgoing ? "CallOutgoing" : "CallIncoming"));
                var duration = string.Empty;

                if (!missed)
                {
                    duration = base.Convert.CallDuration(phoneCallAction.Duration ?? 0);
                }

                return missed ? type : string.Format(Unigram.Strings.AppResources.CallTimeFormat, type, duration);
            }

            return string.Empty;
        }
    }
}
