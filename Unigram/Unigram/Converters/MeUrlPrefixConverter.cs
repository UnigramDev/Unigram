using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Services.Cache;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class MeUrlPrefixConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return Convert(value, parameter != null);
        }

        public static string Convert(object value, bool shorty = false)
        {
            if (value as string == null)
            {
                value = string.Empty;
            }

            var config = InMemoryCacheService.Current.GetConfig();
            if (config == null)
            {
                if (shorty)
                {
                    return "t.me/" + value;
                }

                return "https://t.me/" + value;
            }

            var linkPrefix = config.MeUrlPrefix;
            if (linkPrefix.EndsWith("/"))
            {
                linkPrefix = linkPrefix.Substring(0, linkPrefix.Length - 1);
            }
            if (linkPrefix.StartsWith("https://"))
            {
                linkPrefix = linkPrefix.Substring(8);
            }
            else if (linkPrefix.StartsWith("http://"))
            {
                linkPrefix = linkPrefix.Substring(7);
            }

            if (shorty)
            {
                return $"{linkPrefix}/{value}";
            }

            return $"https://{linkPrefix}/{value}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
