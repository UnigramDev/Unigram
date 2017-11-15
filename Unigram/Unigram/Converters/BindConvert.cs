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

        public string FormatTTLString(int ttl)
        {
            return CallDuration(ttl);

            // TODO:
            //if (ttl < 60)
            //{
            //    return LocaleController.formatPluralString("Seconds", ttl);
            //}
            //else if (ttl < 60 * 60)
            //{
            //    return LocaleController.formatPluralString("Minutes", ttl / 60);
            //}
            //else if (ttl < 60 * 60 * 24)
            //{
            //    return LocaleController.formatPluralString("Hours", ttl / 60 / 60);
            //}
            //else if (ttl < 60 * 60 * 24 * 7)
            //{
            //    return LocaleController.formatPluralString("Days", ttl / 60 / 60 / 24);
            //}
            //else
            //{
            //    int days = ttl / 60 / 60 / 24;
            //    if (ttl % 7 == 0)
            //    {
            //        return LocaleController.formatPluralString("Weeks", days / 7);
            //    }
            //    else
            //    {
            //        return String.format("%s %s", LocaleController.formatPluralString("Weeks", days / 7), LocaleController.formatPluralString("Days", days % 7));
            //    }
            //}
        }

        private Dictionary<string, CurrencyFormatter> _currencyCache = new Dictionary<string, CurrencyFormatter>();
        private Dictionary<string, DateTimeFormatter> _formatterCache = new Dictionary<string, DateTimeFormatter>();

        public string FormatAmount(long amount, string currency)
        {
            if (currency == null)
            {
                return string.Empty;
            }

            bool discount;
            string customFormat;
            double doubleAmount;

            currency = currency.ToUpper();

            if (amount < 0)
            {
                discount = true;
            }
            else
            {
                discount = false;
            }

            amount = Math.Abs(amount);

            switch (currency)
            {
                case "CLF":
                    customFormat = " {0:N4}";
                    doubleAmount = ((double)amount) / 10000.0d;
                    break;
                case "BHD":
                case "IQD":
                case "JOD":
                case "KWD":
                case "LYD":
                case "OMR":
                case "TND":
                    customFormat = " {0:N3}";
                    doubleAmount = ((double)amount) / 1000.0d;
                    break;
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
                    customFormat = " {0:N0}";
                    doubleAmount = (double)amount;
                    break;
                case "MRO":
                    customFormat = " {0:N1}";
                    doubleAmount = ((double)amount) / 10.0d;
                    break;
                default:
                    customFormat = " {0:N2}";
                    doubleAmount = ((double)amount) / 100.0d;
                    break;
            }

            if (_currencyCache.TryGetValue(currency, out CurrencyFormatter formatter) == false)
            {
                formatter = new CurrencyFormatter(currency, GlobalizationPreferences.Languages, GlobalizationPreferences.HomeGeographicRegion);
                _currencyCache[currency] = formatter;
            }

            if (formatter != null)
            {
                return (discount ? "-" : string.Empty) + formatter.Format(doubleAmount);
            }

            return (discount ? "-" : string.Empty) + string.Format(currency + customFormat, doubleAmount);
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

        public string CallDuration(int seconds)
        {
            if (seconds < 60)
            {
                var format = AppResources.CallSeconds_any;
                var number = seconds;
                if (number == 1)
                {
                    format = AppResources.CallSeconds_1;
                }
                else if (number == 2)
                {
                    format = AppResources.CallSeconds_2;
                }
                else if (number == 4)
                {
                    format = AppResources.CallSeconds_3_10;
                }

                return string.Format(format, number);
            }
            else if (seconds < 60 * 60)
            {
                var format = AppResources.CallMinutes_any;
                var number = seconds / 60;
                if (number == 1)
                {
                    format = AppResources.CallMinutes_1;
                }
                else if (number == 2)
                {
                    format = AppResources.CallMinutes_2;
                }
                else if (number == 4)
                {
                    format = AppResources.CallMinutes_3_10;
                }

                return string.Format(format, number);
            }
            else
            {
                var format = "{0} hours";
                var number = seconds / (60 * 60);
                if (number == 1)
                {
                    format = "{0} hours";
                }
                else if (number == 2)
                {
                    format = "{0} hours";
                }
                else if (number == 4)
                {
                    format = "{0} hours";
                }

                return string.Format(format, number);
            }
        }

        public string CallShortDuration(int seconds)
        {
            if (seconds < 60)
            {
                var format = AppResources.CallShortSeconds_any;
                var number = seconds;
                if (number == 1)
                {
                    format = AppResources.CallShortSeconds_1;
                }
                else if (number == 2)
                {
                    format = AppResources.CallShortSeconds_2;
                }
                else if (number == 4)
                {
                    format = AppResources.CallShortSeconds_3_10;
                }

                return string.Format(format, number);
            }
            else
            {
                var format = AppResources.CallShortMinutes_any;
                var number = seconds / 60;
                if (number == 1)
                {
                    format = AppResources.CallShortMinutes_1;
                }
                else if (number == 2)
                {
                    format = AppResources.CallShortMinutes_2;
                }
                else if (number == 4)
                {
                    format = AppResources.CallShortMinutes_3_10;
                }

                return string.Format(format, number);
            }
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
