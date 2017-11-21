using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Common;
using Windows.UI.Xaml.Data;
using Telegram.Api.Helpers;

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
                return parameter == null ? (object)seconds : GetString(seconds ?? 0, parameter);
            }
            else
            {
                return parameter == null ? (object)(((seconds ?? 0) / 5) + 16) : GetString(seconds ?? 0, parameter);
            }
        }

        private string GetString(int seconds, object parameter)
        {
            var param = parameter as string;
            if (param.Equals("short"))
            {
                //if (seconds >= 1 && seconds < 21)
                //{
                //    return seconds.ToString();
                //}

                //return ((seconds / 5) + 16).ToString();

                return seconds.ToString();
            }

            if (seconds >= 1 && seconds < 21)
            {
                return LocaleHelper.FormatTTLString(seconds);
            }

            return LocaleHelper.FormatTTLString((seconds - 16) * 5);
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
