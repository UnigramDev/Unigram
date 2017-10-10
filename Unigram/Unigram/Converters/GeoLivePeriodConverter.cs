using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class GeoLivePeriodConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var period = System.Convert.ToInt32(value);
            return BindConvert.Current.FormatTTLString(period);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
