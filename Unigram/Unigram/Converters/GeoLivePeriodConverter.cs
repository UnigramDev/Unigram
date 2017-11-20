using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Unigram.Common;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class GeoLivePeriodConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return LocaleHelper.FormatTTLString(System.Convert.ToInt32(value));
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
