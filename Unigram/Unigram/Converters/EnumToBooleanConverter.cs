using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return parameter.Equals(value);
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return ((bool)value) == true ? parameter : DependencyProperty.UnsetValue;
        }
    }
}
