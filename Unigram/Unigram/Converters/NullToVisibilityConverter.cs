using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (parameter != null)
            {
                if (value is string)
                {
                    return string.IsNullOrWhiteSpace((string)value) ? Visibility.Visible : Visibility.Collapsed;
                }

                return value != null ? Visibility.Collapsed : Visibility.Visible;
            }

            if (value is string)
            {
                return string.IsNullOrWhiteSpace((string)value) ? Visibility.Collapsed : Visibility.Visible;
            }

            return value == null ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
