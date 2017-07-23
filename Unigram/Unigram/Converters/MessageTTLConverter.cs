using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class MessageTTLConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var seconds = (int?)value;
            if (seconds == 0 || seconds == null)
            {
                return parameter == null ? (object)seconds ?? 0 : "Off";
            }
            else if (seconds >= 1 && seconds < 21)
            {
                return parameter == null ? (object)seconds : BindConvert.Current.FormatTTLString(seconds ?? 0);
            }
            else
            {
                return parameter == null ? (object)(((seconds ?? 0) / 5) + 16) : BindConvert.Current.FormatTTLString(((seconds ?? 0) - 16) * 5);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            var seconds = (int)value;
            if (seconds == 0)
            {
                return null;
            }
            else if (seconds >= 1 && seconds < 21)
            {
                return seconds;
            }
            else
            {
                //return (seconds / 5) + 16;
                return (seconds - 16) * 5;
            }
        }
    }
}
