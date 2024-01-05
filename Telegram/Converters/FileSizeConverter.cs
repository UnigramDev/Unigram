//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Globalization;
using Windows.UI.Xaml.Data;

namespace Telegram.Converters
{
    public class FileSizeConverter : IValueConverter
    {
        public static string Convert(long bytesCount, bool allowGb = false)
        {
            if (bytesCount < 1024L)
            {
                return string.Format("{0} B", bytesCount);
            }
            if (bytesCount < 1048576L)
            {
                return string.Format("{0} KB", (bytesCount / 1024.0).ToString("0.0", CultureInfo.InvariantCulture));
            }
            if (bytesCount >= 1073741824L && allowGb)
            {
                return string.Format("{0} GB", (bytesCount / 1024.0 / 1024.0 / 1024.0).ToString("0.0", CultureInfo.InvariantCulture));
            }

            return string.Format("{0} MB", (bytesCount / 1024.0 / 1024.0).ToString("0.0", CultureInfo.InvariantCulture));
        }

        public static string Convert(long bytesCount, long total, bool allowGb = false)
        {
            if (total < 1024L)
            {
                return string.Format("{0}", bytesCount);
            }
            if (total < 1048576L)
            {
                return string.Format("{0} ", (bytesCount / 1024.0).ToString("0.0", CultureInfo.InvariantCulture));
            }
            if (total >= 1073741824L && allowGb)
            {
                return string.Format("{0}", (bytesCount / 1024.0 / 1024.0 / 1024.0).ToString("0.0", CultureInfo.InvariantCulture));
            }

            return string.Format("{0}", (bytesCount / 1024.0 / 1024.0).ToString("0.0", CultureInfo.InvariantCulture));
        }

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is long)
            {
                return Convert((long)value);
            }
            if (value is int)
            {
                return Convert((int)value);
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
