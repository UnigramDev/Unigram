using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class FileSizeConverter : IValueConverter
    {
        public static string Convert(long bytesCount)
        {
            if (bytesCount < 1024L)
            {
                return string.Format("{0} B", bytesCount);
            }
            if (bytesCount < 1048576L)
            {
                return string.Format("{0} KB", ((double)bytesCount / 1024.0).ToString("0.0", CultureInfo.InvariantCulture));
            }
            if (bytesCount < 1073741824L)
            {
                return string.Format("{0} MB", ((double)bytesCount / 1024.0 / 1024.0).ToString("0.0", CultureInfo.InvariantCulture));
            }

            return string.Format("{0} GB", ((double)bytesCount / 1024.0 / 1024.0 / 1024.0).ToString("0.0", CultureInfo.InvariantCulture));
        }

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is long)
            {
                return Convert((long)value);
            }
            if (value is int)
            {
                return Convert((long)((int)value));
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
