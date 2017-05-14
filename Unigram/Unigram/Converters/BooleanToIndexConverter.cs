using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class BooleanToIndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (parameter != null)
            {
                return System.Convert.ToInt32(!(bool)value);
            }

            return System.Convert.ToInt32(value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (parameter != null)
            {
                return !System.Convert.ToBoolean(value);
            }

            return System.Convert.ToBoolean(value);
        }
    }
}
