using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class UsernameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (string.IsNullOrEmpty((string)value))
            {
                return parameter == null || (parameter != null && parameter.Equals("username")) ? "None" : null;
            }

            return parameter != null ? $"@{value}" : value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
