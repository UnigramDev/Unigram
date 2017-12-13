using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class GeoLiveToLabelConverter : IValueConverter
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

            if (parameter != null)
            {
                var expires = BindConvert.Current.DateTime(message.Date + geoLiveMedia.Period);
                return (expires > DateTime.Now) ? Strings.Android.AttachLiveLocation : "Location Sharing Ended";
            }
            else
            {
                return "Updated at " + BindConvert.Current.Date(message.EditDate ?? message.Date);
                //var expires = BindConvert.Current.DateTime(message.EditDate ?? message.Date);
                //return (DateTime.Now - expires).ToString("updated mm minutes ago");
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
