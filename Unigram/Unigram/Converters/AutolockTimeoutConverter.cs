using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class AutolockTimeoutConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return LocaleHelper.FormatAutoLock(System.Convert.ToInt32(value));
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
