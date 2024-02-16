//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Services;
using Windows.UI.Xaml.Data;

namespace Telegram.Converters
{
    public class MeUrlPrefixConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return Convert(null, value as string, parameter != null);
        }

        public static string Convert(IClientService clientService, string value, bool shorty = false)
        {
            value ??= string.Empty;

            var linkPrefix = "https://t.me/"; // config.MeUrlPrefix;

            if (clientService != null)
            {
                var option = clientService.Options.TMeUrl;
                if (!string.IsNullOrEmpty(option))
                {
                    linkPrefix = option;
                }
            }

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
