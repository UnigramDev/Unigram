using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Unigram.Common;
using Unigram.Strings;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class AccountTTLConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var days = System.Convert.ToInt32(value);
            if (days >= 365)
            {
                return LocaleHelper.Declension("Years", days / 365);
            }

            return LocaleHelper.Declension("Months", days / 30);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
