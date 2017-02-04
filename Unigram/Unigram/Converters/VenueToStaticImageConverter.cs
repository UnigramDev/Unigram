using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class VenueToStaticImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var geopoint = value as TLGeoPoint;
            if (geopoint == null)
            {
                return null;
            }

            var zoom = 15;
            var size = (string)parameter;
            if (size == null)
            {
                zoom = 13;
                size = "320,240";
            }

            var latitude = geopoint.Lat.ToString(CultureInfo.InvariantCulture);
            var longitude = geopoint.Long.ToString(CultureInfo.InvariantCulture);
            //return string.Format("https://maps.googleapis.com/maps/api/staticmap?center={0},{1}&zoom={2}&size={3}&sensor=false&format=jpg&maptype=roadmap", latitude, longitude, zoom, size);
            return string.Format("http://dev.virtualearth.net/REST/v1/Imagery/Map/Road/{0},{1}/{2}?mapSize={3}&key=FgqXCsfOQmAn9NRf4YJ2~61a_LaBcS6soQpuLCjgo3g~Ah_T2wZTc8WqNe9a_yzjeoa5X00x4VJeeKH48wAO1zWJMtWg6qN-u4Zn9cmrOPcL", latitude, longitude, zoom, size);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
