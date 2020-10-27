using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Native;
using Unigram.Services;
using Windows.Globalization.DateTimeFormatting;
using Windows.System.UserProfile;

namespace Unigram.Converters
{
    public class BindConvert
    {
        private static BindConvert _current;
        public static BindConvert Current
        {
            get
            {
                if (_current == null)
                    _current = new BindConvert();

                return _current;
            }
        }

        public DateTimeFormatter ShortDate { get; private set; }
        public DateTimeFormatter ShortTime { get; private set; }
        public DateTimeFormatter LongDate { get; private set; }
        public DateTimeFormatter LongTime { get; private set; }

        public DateTimeFormatter MonthFull { get; private set; }
        public DateTimeFormatter MonthAbbreviatedDay { get; private set; }
        public DateTimeFormatter MonthFullYear { get; private set; }
        public DateTimeFormatter DayMonthFull { get; private set; }
        public DateTimeFormatter DayMonthFullYear { get; private set; }
        public DateTimeFormatter MonthAbbreviatedYear { get; private set; }
        public DateTimeFormatter DayMonthAbbreviatedYear { get; private set; }

        private BindConvert()
        {
            //var region = new GeographicRegion();
            //var code = region.CodeTwoLetter;

            var culture = NativeUtils.GetCurrentCulture();
            var languages = new[] { culture }.Union(GlobalizationPreferences.Languages);

            ShortDate = new DateTimeFormatter("shortdate", languages, GlobalizationPreferences.HomeGeographicRegion, GlobalizationPreferences.Calendars.FirstOrDefault(), GlobalizationPreferences.Clocks.FirstOrDefault());
            ShortTime = new DateTimeFormatter("shorttime", languages, GlobalizationPreferences.HomeGeographicRegion, GlobalizationPreferences.Calendars.FirstOrDefault(), GlobalizationPreferences.Clocks.FirstOrDefault());
            LongDate = new DateTimeFormatter("longdate", languages, GlobalizationPreferences.HomeGeographicRegion, GlobalizationPreferences.Calendars.FirstOrDefault(), GlobalizationPreferences.Clocks.FirstOrDefault());
            LongTime = new DateTimeFormatter("longtime", languages, GlobalizationPreferences.HomeGeographicRegion, GlobalizationPreferences.Calendars.FirstOrDefault(), GlobalizationPreferences.Clocks.FirstOrDefault());
            MonthFull = new DateTimeFormatter("month.full", languages, GlobalizationPreferences.HomeGeographicRegion, GlobalizationPreferences.Calendars.FirstOrDefault(), GlobalizationPreferences.Clocks.FirstOrDefault());
            MonthAbbreviatedDay = new DateTimeFormatter("month.abbreviated day", languages, GlobalizationPreferences.HomeGeographicRegion, GlobalizationPreferences.Calendars.FirstOrDefault(), GlobalizationPreferences.Clocks.FirstOrDefault());
            MonthFullYear = new DateTimeFormatter("month.full year", languages, GlobalizationPreferences.HomeGeographicRegion, GlobalizationPreferences.Calendars.FirstOrDefault(), GlobalizationPreferences.Clocks.FirstOrDefault());
            DayMonthFull = new DateTimeFormatter("day month.full", languages, GlobalizationPreferences.HomeGeographicRegion, GlobalizationPreferences.Calendars.FirstOrDefault(), GlobalizationPreferences.Clocks.FirstOrDefault());
            DayMonthFullYear = new DateTimeFormatter("day month.full year", languages, GlobalizationPreferences.HomeGeographicRegion, GlobalizationPreferences.Calendars.FirstOrDefault(), GlobalizationPreferences.Clocks.FirstOrDefault());
            MonthAbbreviatedYear = new DateTimeFormatter("month.abbreviated year", languages, GlobalizationPreferences.HomeGeographicRegion, GlobalizationPreferences.Calendars.FirstOrDefault(), GlobalizationPreferences.Clocks.FirstOrDefault());
            DayMonthAbbreviatedYear = new DateTimeFormatter("day month.abbreviated year", languages, GlobalizationPreferences.HomeGeographicRegion, GlobalizationPreferences.Calendars.FirstOrDefault(), GlobalizationPreferences.Clocks.FirstOrDefault());
        }

        public static string MonthGrouping(DateTime date)
        {
            var now = System.DateTime.Now;

            var difference = Math.Abs((date.Month - now.Month) + 12 * (date.Year - now.Year));
            if (difference >= 12)
            {
                return Current.MonthFullYear.Format(date);
            }

            return Current.MonthFull.Format(date);
        }

        public static string DayGrouping(DateTime date)
        {
            var now = System.DateTime.Now;

            var difference = Math.Abs((date.Month - now.Month) + 12 * (date.Year - now.Year));
            if (difference >= 12)
            {
                return Current.DayMonthFullYear.Format(date);
            }
            else if (date.Date == now.Date)
            {
                return Strings.Resources.MessageScheduleToday;
            }

            return Current.DayMonthFull.Format(date);
        }

        public static string Grams(long value, bool gem)
        {
            var sign = value < 0 ? "-" : string.Empty;
            var builder = new StringBuilder(string.Format("{0}{1}.{2:000000000}", sign, Math.Abs(value / 1000000000L), Math.Abs(value % 1000000000)));
            while (builder.Length > 1 && builder[builder.Length - 1] == '0' && builder[builder.Length - 2] != '.')
            {
                builder.Remove(builder.Length - 1, 1);
            }

            if (gem)
            {
                var culture = NativeUtils.GetCurrentCulture();
                var info = new CultureInfo(culture);
                if (info.NumberFormat.CurrencyPositivePattern == 0 || info.NumberFormat.CurrencyPositivePattern == 2)
                {
                    return string.Format("\uD83D\uDC8E {0}", builder);
                }

                return string.Format("{0} \uD83D\uDC8E", builder);
            }

            return builder.ToString();
        }

        public static string Distance(float distance, bool away = true)
        {
            var useImperialSystemType = false;

            switch (SettingsService.Current.DistanceUnits)
            {
                case DistanceUnits.Automatic:
                    var culture = NativeUtils.GetCurrentCulture();
                    var info = new RegionInfo(culture);
                    useImperialSystemType = !info.IsMetric;
                    break;
                case DistanceUnits.Kilometers:
                    useImperialSystemType = false;
                    break;
                case DistanceUnits.Miles:
                    useImperialSystemType = true;
                    break;
            }

            if (useImperialSystemType)
            {
                distance *= 3.28084f;
                if (distance < 1000)
                {
                    return string.Format(away ? Strings.Resources.FootsAway : Strings.Resources.Foots, string.Format("{0}", (int)Math.Max(1, distance)));
                }
                else
                {
                    String arg;
                    if (distance % 5280 == 0)
                    {
                        arg = string.Format("{0}", (int)(distance / 5280));
                    }
                    else
                    {
                        arg = string.Format("{0:0.00}", distance / 5280.0f);
                    }

                    return string.Format(away ? Strings.Resources.MilesAway : Strings.Resources.Miles, arg);
                }
            }
            else
            {
                if (distance < 1000)
                {
                    return string.Format(away ? Strings.Resources.MetersAway2 : Strings.Resources.Meters, string.Format("{0}", (int)Math.Max(1, distance)));
                }
                else
                {
                    String arg;
                    if (distance % 1000 == 0)
                    {
                        arg = string.Format("{0}", (int)(distance / 1000));
                    }
                    else
                    {
                        arg = string.Format("{0:0.00}", distance / 1000.0f);
                    }

                    return string.Format(away ? Strings.Resources.KMetersAway2 : Strings.Resources.KMeters, arg);
                }
            }
        }

        public string PhoneNumber(string number)
        {
            if (number == null)
            {
                return null;
            }

            return Common.PhoneNumber.Format(number);
        }

        public string BannedUntil(long date)
        {
            var banned = Utils.UnixTimestampToDateTime(date);
            return ShortDate.Format(banned) + ", " + ShortTime.Format(banned);

            //try
            //{
            //    date *= 1000;
            //    var rightNow = System.DateTime.Now;
            //    var year = rightNow.Year;
            //    var banned = Utils.UnixTimestampToDateTime(date);
            //    int dateYear = banned.Year;

            //    if (year == dateYear)
            //    {
            //        //formatterBannedUntil = createFormatter(locale, is24HourFormat ? getStringInternal("formatterBannedUntil24H", R.string.formatterBannedUntil24H) : getStringInternal("formatterBannedUntil12H", R.string.formatterBannedUntil12H), is24HourFormat ? "MMM dd yyyy, HH:mm" : "MMM dd yyyy, h:mm a");
            //        //formatterBannedUntilThisYear = createFormatter(locale, is24HourFormat ? getStringInternal("formatterBannedUntilThisYear24H", R.string.formatterBannedUntilThisYear24H) : getStringInternal("formatterBannedUntilThisYear12H", R.string.formatterBannedUntilThisYear12H), is24HourFormat ? "MMM dd, HH:mm" : "MMM dd, h:mm a");

            //        return getInstance().formatterBannedUntilThisYear.format(new Date(date));
            //    }
            //    else
            //    {
            //        return getInstance().formatterBannedUntil.format(new Date(date));
            //    }
            //}
            //catch (Exception e)
            //{
            //    //FileLog.e(e);
            //}

            //return "LOC_ERR";
        }

        //private SolidColorBrush BubbleInternal(int? value)
        //{
        //    return Application.Current.Resources[$"Placeholder{Utils.GetColorIndex(value ?? 0)}Brush"] as SolidColorBrush;

        //    switch (Utils.GetColorIndex(value ?? 0))
        //    {
        //        case 0:
        //            return Application.Current.Resources["PlaceholderRedBrush"] as SolidColorBrush;
        //        case 1:
        //            return Application.Current.Resources["PlaceholderGreenBrush"] as SolidColorBrush;
        //        case 2:
        //            return Application.Current.Resources["PlaceholderYellowBrush"] as SolidColorBrush;
        //        case 3:
        //            return Application.Current.Resources["PlaceholderBlueBrush"] as SolidColorBrush;
        //        case 4:
        //            return Application.Current.Resources["PlaceholderPurpleBrush"] as SolidColorBrush;
        //        case 5:
        //            return Application.Current.Resources["PlaceholderPinkBrush"] as SolidColorBrush;
        //        case 6:
        //            return Application.Current.Resources["PlaceholderCyanBrush"] as SolidColorBrush;
        //        case 7:
        //            return Application.Current.Resources["PlaceholderOrangeBrush"] as SolidColorBrush;
        //        default:
        //            return Application.Current.Resources["ListViewItemPlaceholderBackgroundThemeBrush"] as SolidColorBrush;
        //    }
        //}


        private Dictionary<string, DateTimeFormatter> _formatterCache = new Dictionary<string, DateTimeFormatter>();

        public string FormatAmount(long amount, string currency)
        {
            return Locale.FormatCurrency(amount, currency);
        }

        public string ShippingOption(ShippingOption option, string currency)
        {
            var amount = 0L;
            foreach (var price in option.PriceParts)
            {
                amount += price.Amount;
            }

            return $"{FormatAmount(amount, currency)} - {option.Title}";
        }

        public string DateExtended(int value)
        {
            var dateTime = Utils.UnixTimestampToDateTime(value);

            //Today
            if (dateTime.Date == System.DateTime.Now.Date)
            {
                //TimeLabel.Text = dateTime.ToString(string.Format("{0}", shortTimePattern), cultureInfo);
                return ShortTime.Format(dateTime);
            }

            //Week
            if (dateTime.Date.AddDays(6) >= System.DateTime.Now.Date)
            {
                if (_formatterCache.TryGetValue("dayofweek.abbreviated", out DateTimeFormatter formatter) == false)
                {
                    //var region = new GeographicRegion();
                    //var code = region.CodeTwoLetter;

                    formatter = new DateTimeFormatter("dayofweek.abbreviated", GlobalizationPreferences.Languages, GlobalizationPreferences.HomeGeographicRegion, GlobalizationPreferences.Calendars.FirstOrDefault(), GlobalizationPreferences.Clocks.FirstOrDefault());
                    _formatterCache["dayofweek.abbreviated"] = formatter;
                }

                return formatter.Format(dateTime);
            }

            //Long long time ago
            //TimeLabel.Text = dateTime.ToString(string.Format("d.MM.yyyy", shortTimePattern), cultureInfo);
            return ShortDate.Format(dateTime);
        }

        public string Date(int value)
        {
            return ShortTime.Format(DateTime(value));
        }

        public DateTime DateTime(int value)
        {
            return Utils.UnixTimestampToDateTime(value);
        }

        public static string ShortNumber(int number)
        {
            var K = string.Empty;
            var lastDec = 0;

            while (number / 1000 > 0)
            {
                K += "K";
                lastDec = (number % 1000) / 100;
                number /= 1000;
            }

            if (lastDec != 0 && K.Length > 0)
            {
                if (K.Length == 2)
                {
                    return string.Format("{0}.{1}M", number, lastDec);
                }
                else
                {
                    return string.Format("{0}.{1}{2}", number, lastDec, K);
                }
            }

            if (K.Length == 2)
            {
                return string.Format("{0}M", number);
            }
            else
            {
                return string.Format("{0}{1}", number, K);
            }
        }
    }
}
