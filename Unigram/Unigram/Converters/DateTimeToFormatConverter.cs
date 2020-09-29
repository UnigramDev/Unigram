using System;
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
                        return BindConvert.MonthGrouping((DateTime)value);
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

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
