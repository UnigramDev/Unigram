using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.TL;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace Unigram.Converters
{
    public class BindConvert
    {
        private static BindConvert _current;
        public static BindConvert Current
        {
            get
            {
                if (_current == null)
                    _current = new BindConvert();

                return _current;
            }
        }

        private BindConvert()
        {

        }

        public SolidColorBrush Bubble(int? value)
        {
            switch (Utils.GetColorIndex(value ?? 0))
            {
                case 0:
                    return Application.Current.Resources["RedBrush"] as SolidColorBrush;
                case 1:
                    return Application.Current.Resources["GreenBrush"] as SolidColorBrush;
                case 2:
                    return Application.Current.Resources["YellowBrush"] as SolidColorBrush;
                case 3:
                    return Application.Current.Resources["BlueBrush"] as SolidColorBrush;
                case 4:
                    return Application.Current.Resources["PurpleBrush"] as SolidColorBrush;
                case 5:
                    return Application.Current.Resources["PinkBrush"] as SolidColorBrush;
                case 6:
                    return Application.Current.Resources["CyanBrush"] as SolidColorBrush;
                case 7:
                    return Application.Current.Resources["OrangeBrush"] as SolidColorBrush;
                default:
                    return Application.Current.Resources["ListViewItemPlaceholderBackgroundThemeBrush"] as SolidColorBrush;
            }
        }

        public string Date(int value)
        {
            var clientDelta = MTProtoService.Current.ClientTicksDelta;
            var utc0SecsLong = value * 4294967296 - clientDelta;
            var utc0SecsInt = utc0SecsLong / 4294967296.0;
            var dateTime = Utils.UnixTimestampToDateTime(utc0SecsInt);

            var cultureInfo = (CultureInfo)CultureInfo.CurrentUICulture.Clone();
            var shortTimePattern = Utils.GetShortTimePattern(ref cultureInfo);

            return dateTime.ToString(string.Format("{0}", shortTimePattern), cultureInfo);
        }

        public string State(TLMessageState value)
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
    }
}
