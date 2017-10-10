using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class GeoLiveToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var message = value as TLMessage;
            if (message == null)
            {
                return null;
            }

            var geoLiveMedia = message.Media as TLMessageMediaGeoLive;
            if (geoLiveMedia == null)
            {
                return null;
            }

            var expires = BindConvert.Current.DateTime(message.Date + geoLiveMedia.Period);
            return (expires > DateTime.Now) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
