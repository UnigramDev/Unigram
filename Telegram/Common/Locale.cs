//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Native;
using Telegram.Services;
using Windows.ApplicationModel.Resources;
using Windows.System.UserProfile;

namespace Telegram.Common
{
    public static class Locale
    {
        private const int QUANTITY_OTHER = 0x0000;
        private const int QUANTITY_ZERO = 0x0001;
        private const int QUANTITY_ONE = 0x0002;
        private const int QUANTITY_TWO = 0x0004;
        private const int QUANTITY_FEW = 0x0008;
        private const int QUANTITY_MANY = 0x0010;

        private static readonly Dictionary<string, CurrencyNumberFormatter> _currencyCache = new Dictionary<string, CurrencyNumberFormatter>();

        private static readonly Dictionary<string, PluralRules> _allRules = new Dictionary<string, PluralRules>();
        private static readonly ResourceLoader _loader;

        private static PluralRules _currentRules;

        static Locale()
        {
            _loader = ResourceLoader.GetForViewIndependentUse("Resources");

            AddRules(new string[]{"bem", "brx", "da", "de", "el", "en", "eo", "es", "et", "fi", "fo", "gl", "he", "iw", "it", "nb",
                "nl", "nn", "no", "sv", "af", "bg", "bn", "ca", "eu", "fur", "fy", "gu", "ha", "is", "ku",
                "lb", "ml", "mr", "nah", "ne", "om", "or", "pa", "pap", "ps", "so", "sq", "sw", "ta", "te",
                "tk", "ur", "zu", "mn", "gsw", "chr", "rm", "pt", "an", "ast"}, new PluralRules_One());
            AddRules(new string[] { "cs", "sk" }, new PluralRules_Czech());
            AddRules(new string[] { "ff", "fr", "kab" }, new PluralRules_French());
            AddRules(new string[] { "ru", "uk", "be" }, new PluralRules_Balkan());
            AddRules(new string[] { "sr", "hr", "bs", "sh" }, new PluralRules_Serbian());
            AddRules(new string[] { "lv" }, new PluralRules_Latvian());
            AddRules(new string[] { "lt" }, new PluralRules_Lithuanian());
            AddRules(new string[] { "pl" }, new PluralRules_Polish());
            AddRules(new string[] { "ro", "mo" }, new PluralRules_Romanian());
            AddRules(new string[] { "sl" }, new PluralRules_Slovenian());
            AddRules(new string[] { "ar" }, new PluralRules_Arabic());
            AddRules(new string[] { "mk" }, new PluralRules_Macedonian());
            AddRules(new string[] { "cy" }, new PluralRules_Welsh());
            AddRules(new string[] { "br" }, new PluralRules_Breton());
            AddRules(new string[] { "lag" }, new PluralRules_Langi());
            AddRules(new string[] { "shi" }, new PluralRules_Tachelhit());
            AddRules(new string[] { "mt" }, new PluralRules_Maltese());
            AddRules(new string[] { "ga", "se", "sma", "smi", "smj", "smn", "sms" }, new PluralRules_Two());
            AddRules(new string[] { "ak", "am", "bh", "fil", "tl", "guw", "hi", "ln", "mg", "nso", "ti", "wa" }, new PluralRules_Zero());
            AddRules(new string[]{"az", "bm", "fa", "ig", "hu", "ja", "kde", "kea", "ko", "my", "ses", "sg", "to",
                "tr", "vi", "wo", "yo", "zh", "bo", "dz", "id", "jv", "jw", "ka", "km", "kn", "ms", "th", "in"}, new PluralRules_None());
        }

        private static void AddRules(string[] languages, PluralRules rules)
        {
            foreach (var language in languages)
            {
                _allRules[language] = rules;
            }
        }

        public static void SetRules(string code)
        {
            if (_allRules.TryGetValue(code, out PluralRules rules))
            {
                _currentRules = rules;
            }
        }

        public static string GetString(string key)
        {
            return _loader.GetString(key);
        }

        public static string Declension(string key, long count)
        {
            return Declension(key, count, true);
        }

        public static string Declension(string key, long count, bool format)
        {
            _currentRules ??= _allRules["en"];

            if (format)
            {
                return string.Format(LocaleService.Current.GetString(key, _currentRules.QuantityForNumber(count)), count.ToString("N0"));
            }
            else
            {
                return LocaleService.Current.GetString(key, _currentRules.QuantityForNumber(count));
            }
        }

        public static string Declension(string key, long count, params object[] args)
        {
            _currentRules ??= _allRules["en"];

            var newArgs = new object[args.Length + 1];
            newArgs[0] = count.ToString("N0");

            if (args.Length > 0)
            {
                Array.Copy(args, 0, newArgs, 1, args.Length);
            }

            return string.Format(LocaleService.Current.GetString(key, _currentRules.QuantityForNumber(count)), newArgs);
        }

        public static CurrencyNumberFormatter GetCurrencyFormatter(string currency)
        {
            if (_currencyCache.TryGetValue(currency, out CurrencyNumberFormatter formatter) == false)
            {
                var culture = NativeUtils.GetCurrentCulture();
                var languages = new[] { culture }.Union(GlobalizationPreferences.Languages);
                var region = GlobalizationPreferences.HomeGeographicRegion;

                formatter = new CurrencyNumberFormatter(currency, languages, region);
                _currencyCache[currency] = formatter;
            }

            return formatter;
        }

        public static string FormatCurrency(long amount, string currency)
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
                    doubleAmount = amount / 10000.0d;
                    break;
                case "BHD":
                case "IQD":
                case "JOD":
                case "KWD":
                case "LYD":
                case "OMR":
                case "TND":
                    customFormat = " {0:N3}";
                    doubleAmount = amount / 1000.0d;
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
                    doubleAmount = amount;
                    break;
                case "MRO":
                    customFormat = " {0:N1}";
                    doubleAmount = amount / 10.0d;
                    break;
                case "TON":
                    customFormat = " {0:N0}";
                    doubleAmount = amount / 1000000000.0d;
                    break;
                case "XTR":
                    customFormat = Icons.Premium + "\u200A{0:N0}";
                    doubleAmount = amount;
                    break;
                default:
                    customFormat = " {0:N2}";
                    doubleAmount = amount / 100.0d;
                    break;
            }

            if (currency == "XTR")
            {
                return (discount ? "-" : string.Empty) + string.Format(customFormat, doubleAmount);
            }

            var formatter = GetCurrencyFormatter(currency);
            if (formatter != null)
            {
                return (discount ? "-" : string.Empty) + formatter.Format(doubleAmount);
            }

            return (discount ? "-" : string.Empty) + string.Format(currency + customFormat, doubleAmount);
        }

        public static string FormatCallDuration(int duration)
        {
            if (duration > 3600)
            {
                var result = Declension(Strings.R.Hours, duration / 3600);
                var minutes = duration % 3600 / 60;
                if (minutes > 0)
                {
                    result += ", " + Declension(Strings.R.Minutes, minutes);
                }
                return result;
            }
            else if (duration > 60)
            {
                return Declension(Strings.R.Minutes, duration / 60);
            }
            else
            {
                return Declension(Strings.R.Seconds, duration);
            }
        }

        public static string FormatRelativeShort(int date)
        {
            var diff = DateTime.Now.ToTimestamp() - date;
            if (diff <= 0)
            {
                return Strings.ShortTimeNow;
            }
            else if (diff < 60)
            {
                return diff + "s";
            }
            else if (diff < 60 * 60)
            {
                return diff / 60 + "m";
            }
            else if (diff < 60 * 60 * 24)
            {
                return diff / 60 / 60 + "h";
            }
            else if (diff < 60 * 60 * 24 * 7)
            {
                return diff / 60 / 60 / 24 + "d";
            }

            return diff / 60 / 60 / 24 / 7 + "w";
        }

        public static string FormatLivePeriod(int livePeriod, int date)
        {
            if (livePeriod == int.MaxValue)
            {
                return "∞";
            }

            var diff = date + livePeriod - DateTime.Now.ToTimestamp();
            if (diff < 60 * 60)
            {
                return Math.Round(diff / 60d) + "" /*+ "m"*/;
            }

            return Math.Round(diff / 60d / 60d) + "h";
        }

        public static string FormatTtl(int ttl, bool shorter = false)
        {
            if (shorter)
            {
                if (ttl < 60)
                {
                    return ttl + "s";
                }
                else
                {
                    return ttl / 60 + "m";
                }
            }

            if (ttl < 60)
            {
                return Declension(Strings.R.Seconds, ttl);
            }
            else if (ttl < 60 * 60)
            {
                return Declension(Strings.R.Minutes, ttl / 60);
            }
            else if (ttl < 60 * 60 * 24)
            {
                return Declension(Strings.R.Hours, ttl / 60 / 60);
            }
            else if (ttl < 60 * 60 * 24 * 7)
            {
                return Declension(Strings.R.Days, ttl / 60 / 60 / 24);
            }
            else if (ttl < 60 * 60 * 24 * 31)
            {
                int days = ttl / 60 / 60 / 24;
                if (ttl % 7 == 0)
                {
                    return Declension(Strings.R.Weeks, days / 7);
                }
                else
                {
                    return string.Format("{0} {1}", Declension(Strings.R.Weeks, days / 7), Declension(Strings.R.Days, days % 7));
                }
            }
            else if (ttl < 60 * 60 * 24 * 365)
            {
                return Declension(Strings.R.Months, ttl / 60 / 60 / 24 / 31);
            }
            else
            {
                return Declension(Strings.R.Years, ttl / 60 / 60 / 24 / 365);
            }
        }

        public static string FormatLocationUpdateDate(long date)
        {
            try
            {
                var rightNow = DateTime.Now;
                var day = rightNow.DayOfYear;
                var year = rightNow.Year;

                var online = Formatter.ToLocalTime(date);
                int dateDay = online.DayOfYear;
                int dateYear = online.Year;

                if (dateDay == day && year == dateYear)
                {
                    //int diff = (int)(ConnectionsManager.getInstance(UserConfig.selectedAccount).getCurrentTime() - date / 1000) / 60;
                    var diff = (int)(DateTime.Now.ToTimestamp() / 1000 - date) / 60;
                    if (diff < 1)
                    {
                        return Strings.LocationUpdatedJustNow;
                    }
                    else if (diff < 60)
                    {
                        return Declension(Strings.R.UpdatedMinutes, diff);
                    }

                    var format = string.Format(Strings.TodayAtFormatted, Formatter.Time(online)); //getInstance().formatterDay.format(new Date(date)));
                    return string.Format(Strings.LocationUpdatedFormatted, format);
                }
                else if (dateDay + 1 == day && year == dateYear)
                {
                    var format = string.Format(Strings.YesterdayAtFormatted, Formatter.Time(online)); //getInstance().formatterDay.format(new Date(date)));
                    return string.Format(Strings.LocationUpdatedFormatted, format);
                }
                else if (Math.Abs(DateTime.Now.ToTimestamp() / 1000 - date) < 31536000000L)
                {
                    //getInstance().formatterMonth.format(new Date(date)), getInstance().formatterDay.format(new Date(date)));
                    return string.Format(Strings.LocationUpdatedFormatted, Formatter.DateAt(online));
                }
                else
                {
                    //getInstance().formatterYear.format(new Date(date)), getInstance().formatterDay.format(new Date(date)));
                    return string.Format(Strings.LocationUpdatedFormatted, Formatter.DateAt(online));
                }
            }
            catch (Exception)
            {
                //FileLog.e(e);
            }
            return "LOC_ERR";
        }

        public static string FormatDateAudio(long date)
        {
            try
            {
                var rightNow = DateTime.Now;
                int day = rightNow.DayOfYear;
                int year = rightNow.Year;

                var online = Formatter.ToLocalTime(date);
                int dateDay = online.DayOfYear;
                int dateYear = online.Year;
                if (dateDay == day && year == dateYear)
                {
                    return string.Format(Strings.TodayAtFormattedWithToday, Formatter.Time(online));
                }
                else if (dateDay + 1 == day && year == dateYear)
                {
                    return string.Format(Strings.YesterdayAtFormatted, Formatter.Time(online));
                }
                else if (Math.Abs(DateTime.Now.ToTimestamp() / 1000 - date) < 31536000000L)
                {
                    return Formatter.DateAt(online);
                }
                else
                {
                    return Formatter.DateAt(online);
                }
            }
            catch (Exception)
            {
                //FileLog.m27e(e);
            }
            return "LOC_ERR";
        }

        public static string FormatAutoLock(int timeout)
        {
            if (timeout == 0)
            {
                return Strings.AutoLockDisabled;
            }
            else if (timeout < 60 * 60)
            {
                return string.Format(Strings.AutoLockInTime, Declension(Strings.R.Minutes, timeout / 60));
            }
            else /*if (timeout < 60 * 60 * 24)*/
            {
                return string.Format(Strings.AutoLockInTime, Declension(Strings.R.Hours, timeout / 60 / 60));
            }
        }

        public static string FormatMuteFor(int timeout)
        {
            if (timeout < 60 * 60 * 24)
            {
                return Declension(Strings.R.Hours, timeout / 60 / 60);
            }

            return Declension(Strings.R.Days, timeout / 60 / 60 / 24);
        }

        private static Dictionary<string, string> _translitChars;
        public static string[] GetQuery(string src)
        {
            var translit = Transliterate(src);
            if (translit.Equals(src, StringComparison.OrdinalIgnoreCase))
            {
                translit = null;
            }

            var query = new string[translit == null ? 1 : 2];
            query[0] = src;

            if (translit != null)
            {
                query[1] = translit;
            }

            return query;
        }

        public static string Transliterate(string src)
        {
            _translitChars ??= new Dictionary<string, string>(520)
            {
                ["ȼ"] = "c",
                ["ᶇ"] = "n",
                ["ɖ"] = "d",
                ["ỿ"] = "y",
                ["ᴓ"] = "o",
                ["ø"] = "o",
                ["ḁ"] = "a",
                ["ʯ"] = "h",
                ["ŷ"] = "y",
                ["ʞ"] = "k",
                ["ừ"] = "u",
                ["ꜳ"] = "aa",
                ["ĳ"] = "ij",
                ["ḽ"] = "l",
                ["ɪ"] = "i",
                ["ḇ"] = "b",
                ["ʀ"] = "r",
                ["ě"] = "e",
                ["ﬃ"] = "ffi",
                ["ơ"] = "o",
                ["ⱹ"] = "r",
                ["ồ"] = "o",
                ["ǐ"] = "i",
                ["ꝕ"] = "p",
                ["ý"] = "y",
                ["ḝ"] = "e",
                ["ₒ"] = "o",
                ["ⱥ"] = "a",
                ["ʙ"] = "b",
                ["ḛ"] = "e",
                ["ƈ"] = "c",
                ["ɦ"] = "h",
                ["ᵬ"] = "b",
                ["ṣ"] = "s",
                ["đ"] = "d",
                ["ỗ"] = "o",
                ["ɟ"] = "j",
                ["ẚ"] = "a",
                ["ɏ"] = "y",
                ["л"] = "l",
                ["ʌ"] = "v",
                ["ꝓ"] = "p",
                ["ﬁ"] = "fi",
                ["ᶄ"] = "k",
                ["ḏ"] = "d",
                ["ᴌ"] = "l",
                ["ė"] = "e",
                ["ё"] = "yo",
                ["ᴋ"] = "k",
                ["ċ"] = "c",
                ["ʁ"] = "r",
                ["ƕ"] = "hv",
                ["ƀ"] = "b",
                ["ṍ"] = "o",
                ["ȣ"] = "ou",
                ["ǰ"] = "j",
                ["ᶃ"] = "g",
                ["ṋ"] = "n",
                ["ɉ"] = "j",
                ["ǧ"] = "g",
                ["ǳ"] = "dz",
                ["ź"] = "z",
                ["ꜷ"] = "au",
                ["ǖ"] = "u",
                ["ᵹ"] = "g",
                ["ȯ"] = "o",
                ["ɐ"] = "a",
                ["ą"] = "a",
                ["õ"] = "o",
                ["ɻ"] = "r",
                ["ꝍ"] = "o",
                ["ǟ"] = "a",
                ["ȴ"] = "l",
                ["ʂ"] = "s",
                ["ﬂ"] = "fl",
                ["ȉ"] = "i",
                ["ⱻ"] = "e",
                ["ṉ"] = "n",
                ["ï"] = "i",
                ["ñ"] = "n",
                ["ᴉ"] = "i",
                ["ʇ"] = "t",
                ["ẓ"] = "z",
                ["ỷ"] = "y",
                ["ȳ"] = "y",
                ["ṩ"] = "s",
                ["ɽ"] = "r",
                ["ĝ"] = "g",
                ["в"] = "v",
                ["ᴝ"] = "u",
                ["ḳ"] = "k",
                ["ꝫ"] = "et",
                ["ī"] = "i",
                ["ť"] = "t",
                ["ꜿ"] = "c",
                ["ʟ"] = "l",
                ["ꜹ"] = "av",
                ["û"] = "u",
                ["æ"] = "ae",
                ["и"] = "i",
                ["ă"] = "a",
                ["ǘ"] = "u",
                ["ꞅ"] = "s",
                ["ᵣ"] = "r",
                ["ᴀ"] = "a",
                ["ƃ"] = "b",
                ["ḩ"] = "h",
                ["ṧ"] = "s",
                ["ₑ"] = "e",
                ["ʜ"] = "h",
                ["ẋ"] = "x",
                ["ꝅ"] = "k",
                ["ḋ"] = "d",
                ["ƣ"] = "oi",
                ["ꝑ"] = "p",
                ["ħ"] = "h",
                ["ⱴ"] = "v",
                ["ẇ"] = "w",
                ["ǹ"] = "n",
                ["ɯ"] = "m",
                ["ɡ"] = "g",
                ["ɴ"] = "n",
                ["ᴘ"] = "p",
                ["ᵥ"] = "v",
                ["ū"] = "u",
                ["ḃ"] = "b",
                ["ṗ"] = "p",
                ["ь"] = "",
                ["å"] = "a",
                ["ɕ"] = "c",
                ["ọ"] = "o",
                ["ắ"] = "a",
                ["ƒ"] = "f",
                ["ǣ"] = "ae",
                ["ꝡ"] = "vy",
                ["ﬀ"] = "ff",
                ["ᶉ"] = "r",
                ["ô"] = "o",
                ["ǿ"] = "o",
                ["ṳ"] = "u",
                ["ȥ"] = "z",
                ["ḟ"] = "f",
                ["ḓ"] = "d",
                ["ȇ"] = "e",
                ["ȕ"] = "u",
                ["п"] = "p",
                ["ȵ"] = "n",
                ["ʠ"] = "q",
                ["ấ"] = "a",
                ["ǩ"] = "k",
                ["ĩ"] = "i",
                ["ṵ"] = "u",
                ["ŧ"] = "t",
                ["ɾ"] = "r",
                ["ƙ"] = "k",
                ["ṫ"] = "t",
                ["ꝗ"] = "q",
                ["ậ"] = "a",
                ["н"] = "n",
                ["ʄ"] = "j",
                ["ƚ"] = "l",
                ["ᶂ"] = "f",
                ["д"] = "d",
                ["ᵴ"] = "s",
                ["ꞃ"] = "r",
                ["ᶌ"] = "v",
                ["ɵ"] = "o",
                ["ḉ"] = "c",
                ["ᵤ"] = "u",
                ["ẑ"] = "z",
                ["ṹ"] = "u",
                ["ň"] = "n",
                ["ʍ"] = "w",
                ["ầ"] = "a",
                ["ǉ"] = "lj",
                ["ɓ"] = "b",
                ["ɼ"] = "r",
                ["ò"] = "o",
                ["ẘ"] = "w",
                ["ɗ"] = "d",
                ["ꜽ"] = "ay",
                ["ư"] = "u",
                ["ᶀ"] = "b",
                ["ǜ"] = "u",
                ["ẹ"] = "e",
                ["ǡ"] = "a",
                ["ɥ"] = "h",
                ["ṏ"] = "o",
                ["ǔ"] = "u",
                ["ʎ"] = "y",
                ["ȱ"] = "o",
                ["ệ"] = "e",
                ["ế"] = "e",
                ["ĭ"] = "i",
                ["ⱸ"] = "e",
                ["ṯ"] = "t",
                ["ᶑ"] = "d",
                ["ḧ"] = "h",
                ["ṥ"] = "s",
                ["ë"] = "e",
                ["ᴍ"] = "m",
                ["ö"] = "o",
                ["é"] = "e",
                ["ı"] = "i",
                ["ď"] = "d",
                ["ᵯ"] = "m",
                ["ỵ"] = "y",
                ["я"] = "ya",
                ["ŵ"] = "w",
                ["ề"] = "e",
                ["ứ"] = "u",
                ["ƶ"] = "z",
                ["ĵ"] = "j",
                ["ḍ"] = "d",
                ["ŭ"] = "u",
                ["ʝ"] = "j",
                ["ж"] = "zh",
                ["ê"] = "e",
                ["ǚ"] = "u",
                ["ġ"] = "g",
                ["ṙ"] = "r",
                ["ƞ"] = "n",
                ["ъ"] = "",
                ["ḗ"] = "e",
                ["ẝ"] = "s",
                ["ᶁ"] = "d",
                ["ķ"] = "k",
                ["ᴂ"] = "ae",
                ["ɘ"] = "e",
                ["ợ"] = "o",
                ["ḿ"] = "m",
                ["ꜰ"] = "f",
                ["а"] = "a",
                ["ẵ"] = "a",
                ["ꝏ"] = "oo",
                ["ᶆ"] = "m",
                ["ᵽ"] = "p",
                ["ц"] = "ts",
                ["ữ"] = "u",
                ["ⱪ"] = "k",
                ["ḥ"] = "h",
                ["ţ"] = "t",
                ["ᵱ"] = "p",
                ["ṁ"] = "m",
                ["á"] = "a",
                ["ᴎ"] = "n",
                ["ꝟ"] = "v",
                ["è"] = "e",
                ["ᶎ"] = "z",
                ["ꝺ"] = "d",
                ["ᶈ"] = "p",
                ["м"] = "m",
                ["ɫ"] = "l",
                ["ᴢ"] = "z",
                ["ɱ"] = "m",
                ["ṝ"] = "r",
                ["ṽ"] = "v",
                ["ũ"] = "u",
                ["ß"] = "ss",
                ["т"] = "t",
                ["ĥ"] = "h",
                ["ᵵ"] = "t",
                ["ʐ"] = "z",
                ["ṟ"] = "r",
                ["ɲ"] = "n",
                ["à"] = "a",
                ["ẙ"] = "y",
                ["ỳ"] = "y",
                ["ᴔ"] = "oe",
                ["ы"] = "i",
                ["ₓ"] = "x",
                ["ȗ"] = "u",
                ["ⱼ"] = "j",
                ["ẫ"] = "a",
                ["ʑ"] = "z",
                ["ẛ"] = "s",
                ["ḭ"] = "i",
                ["ꜵ"] = "ao",
                ["ɀ"] = "z",
                ["ÿ"] = "y",
                ["ǝ"] = "e",
                ["ǭ"] = "o",
                ["ᴅ"] = "d",
                ["ᶅ"] = "l",
                ["ù"] = "u",
                ["ạ"] = "a",
                ["ḅ"] = "b",
                ["ụ"] = "u",
                ["к"] = "k",
                ["ằ"] = "a",
                ["ᴛ"] = "t",
                ["ƴ"] = "y",
                ["ⱦ"] = "t",
                ["з"] = "z",
                ["ⱡ"] = "l",
                ["ȷ"] = "j",
                ["ᵶ"] = "z",
                ["ḫ"] = "h",
                ["ⱳ"] = "w",
                ["ḵ"] = "k",
                ["ờ"] = "o",
                ["î"] = "i",
                ["ģ"] = "g",
                ["ȅ"] = "e",
                ["ȧ"] = "a",
                ["ẳ"] = "a",
                ["щ"] = "sch",
                ["ɋ"] = "q",
                ["ṭ"] = "t",
                ["ꝸ"] = "um",
                ["ᴄ"] = "c",
                ["ẍ"] = "x",
                ["ủ"] = "u",
                ["ỉ"] = "i",
                ["ᴚ"] = "r",
                ["ś"] = "s",
                ["ꝋ"] = "o",
                ["ỹ"] = "y",
                ["ṡ"] = "s",
                ["ǌ"] = "nj",
                ["ȁ"] = "a",
                ["ẗ"] = "t",
                ["ĺ"] = "l",
                ["ž"] = "z",
                ["ᵺ"] = "th",
                ["ƌ"] = "d",
                ["ș"] = "s",
                ["š"] = "s",
                ["ᶙ"] = "u",
                ["ẽ"] = "e",
                ["ẜ"] = "s",
                ["ɇ"] = "e",
                ["ṷ"] = "u",
                ["ố"] = "o",
                ["ȿ"] = "s",
                ["ᴠ"] = "v",
                ["ꝭ"] = "is",
                ["ᴏ"] = "o",
                ["ɛ"] = "e",
                ["ǻ"] = "a",
                ["ﬄ"] = "ffl",
                ["ⱺ"] = "o",
                ["ȋ"] = "i",
                ["ᵫ"] = "ue",
                ["ȡ"] = "d",
                ["ⱬ"] = "z",
                ["ẁ"] = "w",
                ["ᶏ"] = "a",
                ["ꞇ"] = "t",
                ["ğ"] = "g",
                ["ɳ"] = "n",
                ["ʛ"] = "g",
                ["ᴜ"] = "u",
                ["ф"] = "f",
                ["ẩ"] = "a",
                ["ṅ"] = "n",
                ["ɨ"] = "i",
                ["ᴙ"] = "r",
                ["ǎ"] = "a",
                ["ſ"] = "s",
                ["у"] = "u",
                ["ȫ"] = "o",
                ["ɿ"] = "r",
                ["ƭ"] = "t",
                ["ḯ"] = "i",
                ["ǽ"] = "ae",
                ["ⱱ"] = "v",
                ["ɶ"] = "oe",
                ["ṃ"] = "m",
                ["ż"] = "z",
                ["ĕ"] = "e",
                ["ꜻ"] = "av",
                ["ở"] = "o",
                ["ễ"] = "e",
                ["ɬ"] = "l",
                ["ị"] = "i",
                ["ᵭ"] = "d",
                ["ﬆ"] = "st",
                ["ḷ"] = "l",
                ["ŕ"] = "r",
                ["ᴕ"] = "ou",
                ["ʈ"] = "t",
                ["ā"] = "a",
                ["э"] = "e",
                ["ḙ"] = "e",
                ["ᴑ"] = "o",
                ["ç"] = "c",
                ["ᶊ"] = "s",
                ["ặ"] = "a",
                ["ų"] = "u",
                ["ả"] = "a",
                ["ǥ"] = "g",
                ["р"] = "r",
                ["ꝁ"] = "k",
                ["ẕ"] = "z",
                ["ŝ"] = "s",
                ["ḕ"] = "e",
                ["ɠ"] = "g",
                ["ꝉ"] = "l",
                ["ꝼ"] = "f",
                ["ᶍ"] = "x",
                ["х"] = "h",
                ["ǒ"] = "o",
                ["ę"] = "e",
                ["ổ"] = "o",
                ["ƫ"] = "t",
                ["ǫ"] = "o",
                ["i̇"] = "i",
                ["ṇ"] = "n",
                ["ć"] = "c",
                ["ᵷ"] = "g",
                ["ẅ"] = "w",
                ["ḑ"] = "d",
                ["ḹ"] = "l",
                ["ч"] = "ch",
                ["œ"] = "oe",
                ["ᵳ"] = "r",
                ["ļ"] = "l",
                ["ȑ"] = "r",
                ["ȭ"] = "o",
                ["ᵰ"] = "n",
                ["ᴁ"] = "ae",
                ["ŀ"] = "l",
                ["ä"] = "a",
                ["ƥ"] = "p",
                ["ỏ"] = "o",
                ["į"] = "i",
                ["ȓ"] = "r",
                ["ǆ"] = "dz",
                ["ḡ"] = "g",
                ["ṻ"] = "u",
                ["ō"] = "o",
                ["ľ"] = "l",
                ["ẃ"] = "w",
                ["ț"] = "t",
                ["ń"] = "n",
                ["ɍ"] = "r",
                ["ȃ"] = "a",
                ["ü"] = "u",
                ["ꞁ"] = "l",
                ["ᴐ"] = "o",
                ["ớ"] = "o",
                ["ᴃ"] = "b",
                ["ɹ"] = "r",
                ["ᵲ"] = "r",
                ["ʏ"] = "y",
                ["ᵮ"] = "f",
                ["ⱨ"] = "h",
                ["ŏ"] = "o",
                ["ú"] = "u",
                ["ṛ"] = "r",
                ["ʮ"] = "h",
                ["ó"] = "o",
                ["ů"] = "u",
                ["ỡ"] = "o",
                ["ṕ"] = "p",
                ["ᶖ"] = "i",
                ["ự"] = "u",
                ["ã"] = "a",
                ["ᵢ"] = "i",
                ["ṱ"] = "t",
                ["ể"] = "e",
                ["ử"] = "u",
                ["í"] = "i",
                ["ɔ"] = "o",
                ["с"] = "s",
                ["й"] = "i",
                ["ɺ"] = "r",
                ["ɢ"] = "g",
                ["ř"] = "r",
                ["ẖ"] = "h",
                ["ű"] = "u",
                ["ȍ"] = "o",
                ["ш"] = "sh",
                ["ḻ"] = "l",
                ["ḣ"] = "h",
                ["ȶ"] = "t",
                ["ņ"] = "n",
                ["ᶒ"] = "e",
                ["ì"] = "i",
                ["ẉ"] = "w",
                ["б"] = "b",
                ["ē"] = "e",
                ["ᴇ"] = "e",
                ["ł"] = "l",
                ["ộ"] = "o",
                ["ɭ"] = "l",
                ["ẏ"] = "y",
                ["ᴊ"] = "j",
                ["ḱ"] = "k",
                ["ṿ"] = "v",
                ["ȩ"] = "e",
                ["â"] = "a",
                ["ş"] = "s",
                ["ŗ"] = "r",
                ["ʋ"] = "v",
                ["ₐ"] = "a",
                ["ↄ"] = "c",
                ["ᶓ"] = "e",
                ["ɰ"] = "m",
                ["е"] = "e",
                ["ᴡ"] = "w",
                ["ȏ"] = "o",
                ["č"] = "c",
                ["ǵ"] = "g",
                ["ĉ"] = "c",
                ["ю"] = "yu",
                ["ᶗ"] = "o",
                ["ꝃ"] = "k",
                ["ꝙ"] = "q",
                ["г"] = "g",
                ["ṑ"] = "o",
                ["ꜱ"] = "s",
                ["ṓ"] = "o",
                ["ȟ"] = "h",
                ["ő"] = "o",
                ["ꜩ"] = "tz",
                ["ẻ"] = "e",
                ["о"] = "o"
            };

            src = src.ToLower();

            var dst = new StringBuilder(src.Length);
            var len = src.Length;
            for (int a = 0; a < len; a++)
            {
                var ch = src.Substring(a, 1);
                if (_translitChars.TryGetValue(ch, out string tch))
                {
                    dst.Append(tch);
                }
                else
                {
                    dst.Append(ch);
                }
            }
            return dst.ToString();
        }

        private abstract class PluralRules
        {
            public abstract int QuantityForNumber(long n);
        }

        private class PluralRules_Zero : PluralRules
        {
            public override int QuantityForNumber(long count)
            {
                if (count is 0 or 1)
                {
                    return QUANTITY_ONE;
                }
                else
                {
                    return QUANTITY_OTHER;
                }
            }
        }

        private class PluralRules_Welsh : PluralRules
        {
            public override int QuantityForNumber(long count)
            {
                if (count == 0)
                {
                    return QUANTITY_ZERO;
                }
                else if (count == 1)
                {
                    return QUANTITY_ONE;
                }
                else if (count == 2)
                {
                    return QUANTITY_TWO;
                }
                else if (count == 3)
                {
                    return QUANTITY_FEW;
                }
                else if (count == 6)
                {
                    return QUANTITY_MANY;
                }
                else
                {
                    return QUANTITY_OTHER;
                }
            }
        }

        private class PluralRules_Two : PluralRules
        {
            public override int QuantityForNumber(long count)
            {
                if (count == 1)
                {
                    return QUANTITY_ONE;
                }
                else if (count == 2)
                {
                    return QUANTITY_TWO;
                }
                else
                {
                    return QUANTITY_OTHER;
                }
            }
        }

        private class PluralRules_Tachelhit : PluralRules
        {
            public override int QuantityForNumber(long count)
            {
                if (count is >= 0 and <= 1)
                {
                    return QUANTITY_ONE;
                }
                else if (count is >= 2 and <= 10)
                {
                    return QUANTITY_FEW;
                }
                else
                {
                    return QUANTITY_OTHER;
                }
            }
        }

        private class PluralRules_Slovenian : PluralRules
        {
            public override int QuantityForNumber(long count)
            {
                long rem100 = count % 100;
                if (rem100 == 1)
                {
                    return QUANTITY_ONE;
                }
                else if (rem100 == 2)
                {
                    return QUANTITY_TWO;
                }
                else if (rem100 is >= 3 and <= 4)
                {
                    return QUANTITY_FEW;
                }
                else
                {
                    return QUANTITY_OTHER;
                }
            }
        }

        private class PluralRules_Romanian : PluralRules
        {
            public override int QuantityForNumber(long count)
            {
                long rem100 = count % 100;
                if (count == 1)
                {
                    return QUANTITY_ONE;
                }
                else if (count == 0 || (rem100 >= 1 && rem100 <= 19))
                {
                    return QUANTITY_FEW;
                }
                else
                {
                    return QUANTITY_OTHER;
                }
            }
        }

        private class PluralRules_Polish : PluralRules
        {
            public override int QuantityForNumber(long count)
            {
                long rem100 = count % 100;
                long rem10 = count % 10;
                if (count == 1)
                {
                    return QUANTITY_ONE;
                }
                else if (rem10 >= 2 && rem10 <= 4 && !(rem100 >= 12 && rem100 <= 14))
                {
                    return QUANTITY_FEW;
                }
                else if (rem10 >= 0 && rem10 <= 1 || rem10 >= 5 && rem10 <= 9 || rem100 >= 12 && rem100 <= 14)
                {
                    return QUANTITY_MANY;
                }
                else
                {
                    return QUANTITY_OTHER;
                }
            }
        }

        private class PluralRules_One : PluralRules
        {
            public override int QuantityForNumber(long count)
            {
                return count == 1 ? QUANTITY_ONE : QUANTITY_OTHER;
            }
        }

        private class PluralRules_None : PluralRules
        {
            public override int QuantityForNumber(long count)
            {
                return QUANTITY_OTHER;
            }
        }

        private class PluralRules_Maltese : PluralRules
        {
            public override int QuantityForNumber(long count)
            {
                long rem100 = count % 100;
                if (count == 1)
                {
                    return QUANTITY_ONE;
                }
                else if (count == 0 || (rem100 >= 2 && rem100 <= 10))
                {
                    return QUANTITY_FEW;
                }
                else if (rem100 is >= 11 and <= 19)
                {
                    return QUANTITY_MANY;
                }
                else
                {
                    return QUANTITY_OTHER;
                }
            }
        }

        private class PluralRules_Macedonian : PluralRules
        {
            public override int QuantityForNumber(long count)
            {
                if (count % 10 == 1 && count != 11)
                {
                    return QUANTITY_ONE;
                }
                else
                {
                    return QUANTITY_OTHER;
                }
            }
        }

        private class PluralRules_Lithuanian : PluralRules
        {
            public override int QuantityForNumber(long count)
            {
                long rem100 = count % 100;
                long rem10 = count % 10;
                if (rem10 == 1 && !(rem100 >= 11 && rem100 <= 19))
                {
                    return QUANTITY_ONE;
                }
                else if (rem10 >= 2 && rem10 <= 9 && !(rem100 >= 11 && rem100 <= 19))
                {
                    return QUANTITY_FEW;
                }
                else
                {
                    return QUANTITY_OTHER;
                }
            }
        }

        private class PluralRules_Latvian : PluralRules
        {
            public override int QuantityForNumber(long count)
            {
                if (count == 0)
                {
                    return QUANTITY_ZERO;
                }
                else if (count % 10 == 1 && count % 100 != 11)
                {
                    return QUANTITY_ONE;
                }
                else
                {
                    return QUANTITY_OTHER;
                }
            }
        }

        private class PluralRules_Langi : PluralRules
        {
            public override int QuantityForNumber(long count)
            {
                if (count == 0)
                {
                    return QUANTITY_ZERO;
                }
                else if (count == 1)
                {
                    return QUANTITY_ONE;
                }
                else
                {
                    return QUANTITY_OTHER;
                }
            }
        }

        private class PluralRules_French : PluralRules
        {
            public override int QuantityForNumber(long count)
            {
                if (count is >= 0 and < 2)
                {
                    return QUANTITY_ONE;
                }
                else
                {
                    return QUANTITY_OTHER;
                }
            }
        }

        private class PluralRules_Czech : PluralRules
        {
            public override int QuantityForNumber(long count)
            {
                if (count == 1)
                {
                    return QUANTITY_ONE;
                }
                else if (count is >= 2 and <= 4)
                {
                    return QUANTITY_FEW;
                }
                else
                {
                    return QUANTITY_OTHER;
                }
            }
        }

        private class PluralRules_Breton : PluralRules
        {
            public override int QuantityForNumber(long count)
            {
                if (count == 0)
                {
                    return QUANTITY_ZERO;
                }
                else if (count == 1)
                {
                    return QUANTITY_ONE;
                }
                else if (count == 2)
                {
                    return QUANTITY_TWO;
                }
                else if (count == 3)
                {
                    return QUANTITY_FEW;
                }
                else if (count == 6)
                {
                    return QUANTITY_MANY;
                }
                else
                {
                    return QUANTITY_OTHER;
                }
            }
        }

        private class PluralRules_Balkan : PluralRules
        {
            public override int QuantityForNumber(long count)
            {
                long rem100 = count % 100;
                long rem10 = count % 10;
                if (rem10 == 1 && rem100 != 11)
                {
                    return QUANTITY_ONE;
                }
                else if (rem10 >= 2 && rem10 <= 4 && !(rem100 >= 12 && rem100 <= 14))
                {
                    return QUANTITY_FEW;
                }
                else if (rem10 == 0 || (rem10 >= 5 && rem10 <= 9) || (rem100 >= 11 && rem100 <= 14))
                {
                    return QUANTITY_MANY;
                }
                else
                {
                    return QUANTITY_OTHER;
                }
            }
        }

        private class PluralRules_Serbian : PluralRules
        {
            public override int QuantityForNumber(long count)
            {
                long rem100 = count % 100;
                long rem10 = count % 10;
                if (rem10 == 1 && rem100 != 11)
                {
                    return QUANTITY_ONE;
                }
                else if (rem10 >= 2 && rem10 <= 4 && !(rem100 >= 12 && rem100 <= 14))
                {
                    return QUANTITY_FEW;
                }
                else
                {
                    return QUANTITY_OTHER;
                }
            }
        }

        private class PluralRules_Arabic : PluralRules
        {
            public override int QuantityForNumber(long count)
            {
                long rem100 = count % 100;
                if (count == 0)
                {
                    return QUANTITY_ZERO;
                }
                else if (count == 1)
                {
                    return QUANTITY_ONE;
                }
                else if (count == 2)
                {
                    return QUANTITY_TWO;
                }
                else if (rem100 is >= 3 and <= 10)
                {
                    return QUANTITY_FEW;
                }
                else if (rem100 is >= 11 and <= 99)
                {
                    return QUANTITY_MANY;
                }
                else
                {
                    return QUANTITY_OTHER;
                }
            }
        }
    }
}
