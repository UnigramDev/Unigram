using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Helpers;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.TL;
using Unigram.Strings;
using Windows.Globalization.DateTimeFormatting;
using Windows.Globalization.NumberFormatting;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.System.UserProfile;
using Windows.Globalization;
using Unigram.Common;

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

        public List<SolidColorBrush> PlaceholderColors { get; private set; }

        private BindConvert()
        {
            //var region = new GeographicRegion();
            //var code = region.CodeTwoLetter;

            ShortDate = new DateTimeFormatter("shortdate", GlobalizationPreferences.Languages, GlobalizationPreferences.HomeGeographicRegion, GlobalizationPreferences.Calendars.FirstOrDefault(), GlobalizationPreferences.Clocks.FirstOrDefault());
            ShortTime = new DateTimeFormatter("shorttime", GlobalizationPreferences.Languages, GlobalizationPreferences.HomeGeographicRegion, GlobalizationPreferences.Calendars.FirstOrDefault(), GlobalizationPreferences.Clocks.FirstOrDefault());
            LongDate = new DateTimeFormatter("longdate", GlobalizationPreferences.Languages, GlobalizationPreferences.HomeGeographicRegion, GlobalizationPreferences.Calendars.FirstOrDefault(), GlobalizationPreferences.Clocks.FirstOrDefault());
            LongTime = new DateTimeFormatter("longtime", GlobalizationPreferences.Languages, GlobalizationPreferences.HomeGeographicRegion, GlobalizationPreferences.Calendars.FirstOrDefault(), GlobalizationPreferences.Clocks.FirstOrDefault());

            PlaceholderColors = new List<SolidColorBrush>();

            for (int i = 0; i < 6; i++)
            {
                PlaceholderColors.Add((SolidColorBrush)Application.Current.Resources[$"Placeholder{i}Brush"]);
            }
        }

        public SolidColorBrush Bubble(int uid)
        {
            return PlaceholderColors[(uid + SettingsHelper.UserId) % PlaceholderColors.Count];
        }

        public string PhoneNumber(string number)
        {
            if (number == null)
            {
                return null;
            }

            return Telegram.Helpers.PhoneNumber.Format(number);
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
            return LocaleHelper.FormatCurrency(amount, currency);
        }

        public string ShippingOption(TLShippingOption option, string currency)
        {
            var amount = 0L;
            foreach (var price in option.Prices)
            {
                amount += price.Amount;
            }

            return $"{FormatAmount(amount, currency)} - {option.Title}";
        }

        public string DateExtended(int value)
        {
            var clientDelta = MTProtoService.Current.ClientTicksDelta;
            var utc0SecsInt = value - clientDelta / 4294967296.0;
            var dateTime = Utils.UnixTimestampToDateTime(utc0SecsInt);

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
            var clientDelta = MTProtoService.Current.ClientTicksDelta;
            var utc0SecsInt = value - clientDelta / 4294967296.0;
            var dateTime = Utils.UnixTimestampToDateTime(utc0SecsInt);

            return dateTime;
        }

        public string State(TLMessageState value)
        {
            switch (value)
            {
                case TLMessageState.Sending:
                    return "\uE600";
                case TLMessageState.Confirmed:
                    return "\uE602";
                case TLMessageState.Read:
                    return "\uE601";
                default:
                    return "\uFFFD";
            }
        }

        public string Views(TLMessage message, int? views)
        {
            var number = string.Empty;

            if (message.HasViews)
            {
                number = ShortNumber(views ?? 0);

                if (message.IsPost && message.HasFromId && message.From != null)
                {
                    number += $"   {message.From.FullName},";
                }
            }

            return number;
        }

        public string ShortNumber(int number)
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
