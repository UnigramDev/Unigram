using System;
using System.Globalization;
using Telegram.Api.TL;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class StateToGlyphConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (!(value is TLMessageState)) return null;

            var status = (TLMessageState)value;
            if (status == TLMessageState.Sending)
                return "\uE600";

            if (status == TLMessageState.Confirmed)
                return "\uE602";

            if (status == TLMessageState.Read)
                return "\uE601";

            if (status == TLMessageState.Broadcast)
            {
                //return new Uri(string.Format("ms-appx:///Assets/Messages{0}/MessageStateBroadcast.png", relative));
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
