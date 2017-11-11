using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
                var years = days / 365;
                return Language.Declension(years, Strings.Resources.YearNominativeSingular, Strings.Resources.YearNominativePlural, Strings.Resources.YearGenitiveSingular, Strings.Resources.YearGenitivePlural, null, null);
            }

            var months = days / 30;
            return Language.Declension(months, Strings.Resources.MonthNominativeSingular, Strings.Resources.MonthNominativePlural, Strings.Resources.MonthGenitiveSingular, Strings.Resources.MonthGenitivePlural, null, null);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
