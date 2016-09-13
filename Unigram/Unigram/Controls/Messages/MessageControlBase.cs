using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.TL;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls.Messages
{
    public class MessageControlBase : UserControl
    {
        public TLMessage ViewModel => DataContext as TLMessage;

        public MessageControlBase()
        {
            DataContextChanged += (s, args) =>
            {
                if (ViewModel != null)
                {
                    Loading(s, null);
                }
            };
        }

        #region Convert methods
        protected string ConvertDate(int value)
        {
            var clientDelta = MTProtoService.Instance.ClientTicksDelta;
            var utc0SecsLong = value * 4294967296 - clientDelta;
            var utc0SecsInt = utc0SecsLong / 4294967296.0;
            var dateTime = Utils.UnixTimestampToDateTime(utc0SecsInt);

            var cultureInfo = (CultureInfo)CultureInfo.CurrentUICulture.Clone();
            var shortTimePattern = Utils.GetShortTimePattern(ref cultureInfo);

            return dateTime.ToString(string.Format("{0}", shortTimePattern), cultureInfo);
        }

        protected string ConvertState(TLMessageState value)
        {
            switch (value)
            {
                case TLMessageState.Sending:
                    return "\uE600";
                case TLMessageState.Confirmed:
                    return "\uE601";
                case TLMessageState.Read:
                    return "\uE602";
                default:
                    return "\uFFFD";
            }
        }
        #endregion

        /// <summary>
        /// x:Bind hack
        /// </summary>
        public new event TypedEventHandler<FrameworkElement, object> Loading;
    }
}
