using System;
using Unigram.Common;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class ChatTtlConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (parameter == null)
            {
                var which = (int)value;
                if (which > 0 && which < 16)
                {
                    return which;
                }
                else if (which == 30)
                {
                    return 16;
                }
                else if (which == 60)
                {
                    return 17;
                }
                else if (which == 60 * 60)
                {
                    return 18;
                }
                else if (which == 60 * 60 * 24)
                {
                    return 19;
                }
                else if (which == 60 * 60 * 24 * 7)
                {
                    return 20;
                }
                else if (which == 0)
                {
                    return 0;
                }
            }

            return Format(System.Convert.ToInt32(value));
        }

        public static string Format(int value)
        {
            if (value == 0)
            {
                return Strings.Resources.ShortMessageLifetimeForever;
            }
            else if (value >= 1 && value < 16)
            {
                return Locale.FormatTtl(value);
            }
            else if (value == 16)
            {
                return Locale.FormatTtl(30);
            }
            else if (value == 17)
            {
                return Locale.FormatTtl(60);
            }
            else if (value == 18)
            {
                return Locale.FormatTtl(60 * 60);
            }
            else if (value == 19)
            {
                return Locale.FormatTtl(60 * 60 * 24);
            }
            else if (value == 20)
            {
                return Locale.FormatTtl(60 * 60 * 24 * 7);
            }

            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            var which = (int)value;
            if (which >= 0 && which < 16)
            {
                return which;
            }
            else if (which == 16)
            {
                return 30;
            }
            else if (which == 17)
            {
                return 60;
            }
            else if (which == 18)
            {
                return 60 * 60;
            }
            else if (which == 19)
            {
                return 60 * 60 * 24;
            }
            else if (which == 20)
            {
                return 60 * 60 * 24 * 7;
            }

            return 0;
        }
    }
}
