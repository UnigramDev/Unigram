using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class MessageShortTTLConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var seconds = (int?)value;
            var left = seconds ?? 0;

            if (left < 60)
            {
                return left + "s";
            }
            else
            {
                return left / 60 + "m";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
