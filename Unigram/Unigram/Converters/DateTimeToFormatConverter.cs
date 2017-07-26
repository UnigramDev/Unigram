using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Globalization.DateTimeFormatting;
using Windows.System.UserProfile;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class DateTimeToFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) value = DateTime.Now; // TEST;
            if (value is DateTimeOffset) value = ((DateTimeOffset)value).DateTime;

            var format = (string)parameter;
            if (format.StartsWith("unigram"))
            {
                switch (format)
                {
                    case "unigram.monthgrouping":
                        return ConvertMonthGrouping((DateTime)value);
                }
            }
            else
            {
                var formatted = new DateTimeFormatter(format, GlobalizationPreferences.Languages).Format((DateTime)value).Trim('\u200E', '\u200F');
                if (format.Contains("full"))
                {
                    return formatted.Substring(0, 1).ToUpper() + formatted.Substring(1);
                }

                return formatted;
            }

            return value;
        }

        public static string ConvertMonthGrouping(DateTime date)
        {
            var formatted = new DateTimeFormatter("month.full", GlobalizationPreferences.Languages).Format(date).Trim('\u200E', '\u200F');
            formatted = formatted.Substring(0, 1).ToUpper() + formatted.Substring(1);

            if (date.Year != DateTime.Now.Year)
            {
                formatted += $" {date.Year}";
            }

            return formatted;
        }

        public static string ConvertDayGrouping(DateTime date)
        {
            var formatted = new DateTimeFormatter("day month.full", GlobalizationPreferences.Languages).Format(date);

            if (date.Year != DateTime.Now.Year)
            {
                formatted += $" {date.Year}";
            }

            return formatted;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
