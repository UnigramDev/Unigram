//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Telegram.Common;
using Telegram.Native;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.Globalization.DateTimeFormatting;
using Windows.System.UserProfile;

namespace Telegram.Converters
{
    public static class Formatter
    {
        public static DateTimeFormatter ShortDate { get; private set; }
        public static DateTimeFormatter ShortTime { get; private set; }
        public static DateTimeFormatter LongDate { get; private set; }
        public static DateTimeFormatter LongTime { get; private set; }

        public static DateTimeFormatter MonthFull { get; private set; }
        public static DateTimeFormatter MonthAbbreviatedDay { get; private set; }
        public static DateTimeFormatter MonthFullYear { get; private set; }
        public static DateTimeFormatter DayMonthFull { get; private set; }
        public static DateTimeFormatter DayMonthFullYear { get; private set; }
        public static DateTimeFormatter MonthAbbreviatedYear { get; private set; }
        public static DateTimeFormatter DayMonthAbbreviated { get; private set; }
        public static DateTimeFormatter DayMonthAbbreviatedYear { get; private set; }
        public static DateTimeFormatter DayOfWeekAbbreviated { get; private set; }

        public static IList<string> Languages { get; }

        static Formatter()
        {
            var culture = NativeUtils.GetCurrentCulture();
            var languages = GlobalizationPreferences.Languages.ToList();
            var region = GlobalizationPreferences.HomeGeographicRegion;
            var calendar = GlobalizationPreferences.Calendars.FirstOrDefault();
            var clock = GlobalizationPreferences.Clocks.FirstOrDefault();

            if (Windows.Globalization.Language.IsWellFormed(culture))
            {
                languages.Insert(0, culture);
            }

            Languages = languages;

            ShortDate = new DateTimeFormatter("shortdate", languages, region, calendar, clock);
            ShortTime = new DateTimeFormatter("shorttime", languages, region, calendar, clock);
            LongDate = new DateTimeFormatter("longdate", languages, region, calendar, clock);
            LongTime = new DateTimeFormatter("longtime", languages, region, calendar, clock);
            MonthFull = new DateTimeFormatter("month.full", languages, region, calendar, clock);
            MonthAbbreviatedDay = new DateTimeFormatter("month.abbreviated day", languages, region, calendar, clock);
            MonthFullYear = new DateTimeFormatter("month.full year", languages, region, calendar, clock);
            DayMonthFull = new DateTimeFormatter("day month.full", languages, region, calendar, clock);
            DayMonthFullYear = new DateTimeFormatter("day month.full year", languages, region, calendar, clock);
            MonthAbbreviatedYear = new DateTimeFormatter("month.abbreviated year", languages, region, calendar, clock);
            DayMonthAbbreviated = new DateTimeFormatter("day month.abbreviated", languages, region, calendar, clock);
            DayMonthAbbreviatedYear = new DateTimeFormatter("day month.abbreviated year", languages, region, calendar, clock);
            DayOfWeekAbbreviated = new DateTimeFormatter("dayofweek.abbreviated", languages, region, calendar, clock);
        }

        public static string UtcTimeOffset(int value)
        {
            if (value == 0)
            {
                return "UTC";
            }

            var span = TimeSpan.FromSeconds(value);
            if (span > TimeSpan.Zero)
            {
                return string.Format("UTC+{0:hh\\:mm}", span);
            }

            return string.Format("UTC-{0:hh\\:mm}", span);
        }

        public static string MonthGrouping(int value)
        {
            var date = ToLocalTime(value);
            var now = DateTime.Now;

            var difference = Math.Abs(date.Month - now.Month + 12 * (date.Year - now.Year));
            if (difference >= 12)
            {
                return MonthFullYear.Format(date);
            }

            return MonthFull.Format(date);
        }

        public static string DayGrouping(int value)
        {
            //if (SettingsService.Current.Diagnostics.NativeTimeFormatter)
            //{
            //    return NativeUtils.FormatDate(value);
            //}

            var date = ToLocalTime(value);
            var now = DateTime.Now;

            var difference = Math.Abs(date.Month - now.Month + 12 * (date.Year - now.Year));
            if (difference >= 12)
            {
                return DayMonthFullYear.Format(date);
            }
            else if (date.Date == now.Date)
            {
                return Strings.MessageScheduleToday;
            }

            return DayMonthFull.Format(date);
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
                    return string.Format(away ? Strings.FootsAway : Strings.FootsShort, string.Format("{0}", (int)Math.Max(1, distance)));
                }
                else
                {
                    string arg;
                    if (distance % 5280 == 0)
                    {
                        arg = string.Format("{0}", (int)(distance / 5280));
                    }
                    else
                    {
                        arg = string.Format("{0:0.00}", distance / 5280.0f);
                    }

                    return string.Format(away ? Strings.MilesAway : Strings.MilesShort, arg);
                }
            }
            else
            {
                if (distance < 1000)
                {
                    return string.Format(away ? Strings.MetersAway2 : Strings.MetersShort, string.Format("{0}", (int)Math.Max(1, distance)));
                }
                else
                {
                    string arg;
                    if (distance % 1000 == 0)
                    {
                        arg = string.Format("{0}", (int)(distance / 1000));
                    }
                    else
                    {
                        arg = string.Format("{0:0.00}", distance / 1000.0f);
                    }

                    return string.Format(away ? Strings.KMetersAway2 : Strings.KMetersShort, arg);
                }
            }
        }

        public static string PhoneNumber(string number)
        {
            if (number == null)
            {
                return null;
            }

            return Common.PhoneNumber.Format(number);
        }

        public static string BannedUntil(int date)
        {
            var banned = ToLocalTime(date);
            return ShortDate.Format(banned) + ", " + ShortTime.Format(banned);

            //try
            //{
            //    date *= 1000;
            //    var rightNow = System.DateTime.Now;
            //    var year = rightNow.Year;
            //    var banned = Converter.DateTime(date);
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

        public static string FormatAmount(long amount, string currency)
        {
            return Locale.FormatCurrency(amount, currency);
        }

        public static double Amount(long amount, string currency)
        {
            return amount / GetAmountFraction(currency);
        }

        public static long AmountBack(double amount, string currency)
        {
            return (long)(amount * GetAmountFraction(currency));
        }

        public static double GetAmountFraction(string currency)
        {
            if (currency == null)
            {
                return 1;
            }

            switch (currency.ToUpper())
            {
                case "CLF":
                    return 10000.0d;
                case "BHD":
                case "IQD":
                case "JOD":
                case "KWD":
                case "LYD":
                case "OMR":
                case "TND":
                    return 1000.0d;
                case "BIF":
                case "BYR":
                case "CLP":
                case "CVE":
                case "DJF":
                case "GNF":
                case "ISK":
                case "JPY":
                case "KMF":
                case "KRW":
                case "MGA":
                case "PYG":
                case "RWF":
                case "UGX":
                case "UYI":
                case "VND":
                case "VUV":
                case "XAF":
                case "XOF":
                case "XPF":
                case "XTR":
                    return 1.0d;
                case "MRO":
                    return 10.0d;
                case "TON":
                    return 1000000000.0d;
                default:
                    return 100.0d;
            }
        }

        public static string ShippingOption(ShippingOption option, string currency)
        {
            var amount = 0L;
            foreach (var price in option.PriceParts)
            {
                amount += price.Amount;
            }

            return $"{option.Title} - {FormatAmount(amount, currency)}";
        }

        public static string ReadDate(int value)
        {
            var dateTime = ToLocalTime(value);

            if (dateTime.Date == DateTime.Now.Date)
            {
                return string.Format(Strings.PmReadTodayAt, ShortTime.Format(dateTime));
            }
            else if (dateTime.Date == DateTime.Now.Date.AddDays(-1))
            {
                return string.Format(Strings.PmReadYesterdayAt, ShortTime.Format(dateTime));
            }

            return string.Format(Strings.PmReadDateTimeAt, ShortDate.Format(dateTime), ShortTime.Format(dateTime));
        }

        public static string DateExtended(int value)
        {
            var dateTime = ToLocalTime(value);

            //Today
            if (dateTime.Date == DateTime.Now.Date)
            {
                //TimeLabel.Text = dateTime.ToString(string.Format("{0}", shortTimePattern), cultureInfo);
                return ShortTime.Format(dateTime);
            }

            //Week
            if (dateTime.Date.AddDays(6) >= DateTime.Now.Date)
            {
                return DayOfWeekAbbreviated.Format(dateTime);
            }

            //Year
            if (dateTime.Date.Year == DateTime.Now.Year)
            {
                // TODO: no idea about how to get a short date without year
            }

            //Long long time ago
            //TimeLabel.Text = dateTime.ToString(string.Format("d.MM.yyyy", shortTimePattern), cultureInfo);
            return ShortDate.Format(dateTime);
        }

        public static string Duration(int value)
        {
            var duration = TimeSpan.FromSeconds(value);
            if (duration.TotalHours >= 1)
            {
                return duration.ToString("h\\:mm\\:ss");
            }
            else
            {
                return duration.ToString("mm\\:ss");
            }
        }

        public static string Time(int value)
        {
            return NativeUtils.FormatTime(value);
        }

        public static string Time(DateTime value)
        {
            return ShortTime.Format(value);
        }

        public static string Date(int value)
        {
            return ShortDate.Format(ToLocalTime(value));
        }

        public static DateTime ToLocalTime(long value)
        {
            // From UTC0 UnixTime to local DateTime

            var dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            DateTime.SpecifyKind(dtDateTime, DateTimeKind.Utc);

            dtDateTime = dtDateTime.AddSeconds(value).ToLocalTime();
            return dtDateTime;
        }

        public static string DateAt(int value)
        {
            var date = ToLocalTime(value);
            return string.Format(Strings.formatDateAtTime, ShortDate.Format(date), ShortTime.Format(date));
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

        public static string Birthdate(Birthdate birthdate)
        {
            if (birthdate == null)
            {
                return string.Empty;
            }

            if (birthdate.Year == 0)
            {
                return DayMonthFull.Format(new DateTime(2020, birthdate.Month, birthdate.Day));
            }

            return DayMonthAbbreviatedYear.Format(new DateTime(birthdate.Year, birthdate.Month, birthdate.Day));
        }
    }
}
