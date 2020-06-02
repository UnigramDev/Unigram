using System;
using Unigram.Common;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class MessageTtlConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var seconds = (int)value;
            if (seconds == 0)
            {
                return parameter == null ? (object)seconds ?? 0 : Strings.Resources.ShortMessageLifetimeForever;
            }
            else if (seconds >= 1 && seconds < 21)
            {
                return parameter == null ? (object)seconds : GetString(seconds, parameter);
            }
            else
            {
                return parameter == null ? (object)((seconds / 5) + 16) : GetString(seconds, parameter);
            }
        }

        public static string Convert(int index)
        {
            if (index == 0)
            {
                return Strings.Resources.ShortMessageLifetimeForever;
            }
            else if (index >= 1 && index < 21)
            {
                return GetString(index, null);
            }
            else
            {
                return GetString(index, null);
            }
        }

        public static int ConvertSeconds(int seconds)
        {
            if (seconds == 0)
            {
                return 0;
            }
            else if (seconds >= 1 && seconds < 21)
            {
                return seconds;
            }
            else
            {
                return (seconds / 5) + 16;
            }
        }

        private static string GetString(int seconds, object parameter)
        {
            var param = parameter as string;
            if (param != null && param.Equals("short"))
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
                return Locale.FormatTtl(seconds);
            }

            return Locale.FormatTtl((seconds - 16) * 5);
        }

        public static int ConvertBack(int seconds)
        {
            if (seconds == 0)
            {
                return 0;
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

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            var seconds = (int)value;
            if (seconds == 0)
            {
                return 0;
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
