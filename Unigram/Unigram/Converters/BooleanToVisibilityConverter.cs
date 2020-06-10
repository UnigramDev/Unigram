using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (parameter != null)
            {
                return System.Convert.ToBoolean(value) ? Visibility.Collapsed : Visibility.Visible;
            }

            return System.Convert.ToBoolean(value) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
