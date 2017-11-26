using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.Globalization.NumberFormatting;
using Windows.System.UserProfile;

namespace Telegram.Api.Helpers
{
    public static class LocaleHelper
    {
        private const int QUANTITY_OTHER = 0x0000;
        private const int QUANTITY_ZERO = 0x0001;
        private const int QUANTITY_ONE = 0x0002;
        private const int QUANTITY_TWO = 0x0004;
        private const int QUANTITY_FEW = 0x0008;
        private const int QUANTITY_MANY = 0x0010;

        private static readonly Dictionary<string, CurrencyFormatter> _currencyCache = new Dictionary<string, CurrencyFormatter>();

        private static readonly Dictionary<string, PluralRules> _allRules = new Dictionary<string, PluralRules>();
        private static readonly ResourceLoader _loader;

        private static PluralRules _currentRules;

        static LocaleHelper()
        {
            _loader = ResourceLoader.GetForViewIndependentUse("Android");

            AddRules(new String[]{"bem", "brx", "da", "de", "el", "en", "eo", "es", "et", "fi", "fo", "gl", "he", "iw", "it", "nb",
                "nl", "nn", "no", "sv", "af", "bg", "bn", "ca", "eu", "fur", "fy", "gu", "ha", "is", "ku",
                "lb", "ml", "mr", "nah", "ne", "om", "or", "pa", "pap", "ps", "so", "sq", "sw", "ta", "te",
                "tk", "ur", "zu", "mn", "gsw", "chr", "rm", "pt", "an", "ast"}, new PluralRules_One());
            AddRules(new String[] { "cs", "sk" }, new PluralRules_Czech());
            AddRules(new String[] { "ff", "fr", "kab" }, new PluralRules_French());
            AddRules(new String[] { "hr", "ru", "sr", "uk", "be", "bs", "sh" }, new PluralRules_Balkan());
            AddRules(new String[] { "lv" }, new PluralRules_Latvian());
            AddRules(new String[] { "lt" }, new PluralRules_Lithuanian());
            AddRules(new String[] { "pl" }, new PluralRules_Polish());
            AddRules(new String[] { "ro", "mo" }, new PluralRules_Romanian());
            AddRules(new String[] { "sl" }, new PluralRules_Slovenian());
            AddRules(new String[] { "ar" }, new PluralRules_Arabic());
            AddRules(new String[] { "mk" }, new PluralRules_Macedonian());
            AddRules(new String[] { "cy" }, new PluralRules_Welsh());
            AddRules(new String[] { "br" }, new PluralRules_Breton());
            AddRules(new String[] { "lag" }, new PluralRules_Langi());
            AddRules(new String[] { "shi" }, new PluralRules_Tachelhit());
            AddRules(new String[] { "mt" }, new PluralRules_Maltese());
            AddRules(new String[] { "ga", "se", "sma", "smi", "smj", "smn", "sms" }, new PluralRules_Two());
            AddRules(new String[] { "ak", "am", "bh", "fil", "tl", "guw", "hi", "ln", "mg", "nso", "ti", "wa" }, new PluralRules_Zero());
            AddRules(new String[]{"az", "bm", "fa", "ig", "hu", "ja", "kde", "kea", "ko", "my", "ses", "sg", "to",
                "tr", "vi", "wo", "yo", "zh", "bo", "dz", "id", "jv", "ka", "km", "kn", "ms", "th"}, new PluralRules_None());
        }

        private static void AddRules(String[] languages, PluralRules rules)
        {
            foreach (var language in languages)
            {
                _allRules[language] = rules;
            }
        }

        public static string GetString(string key)
        {
            return _loader.GetString(key);
        }

        public static string Declension(string key, int count)
        {
            if (_currentRules == null)
            {
                _currentRules = _allRules["en"];
            }

            return string.Format(_loader.GetString(key + StringForQuantity(_currentRules.QuantityForNumber(count))), count);
        }

        private static string StringForQuantity(int quantity)
        {
            switch (quantity)
            {
                case QUANTITY_ZERO:
                    return "Zero";
                case QUANTITY_ONE:
                    return "One";
                case QUANTITY_TWO:
                    return "Two";
                case QUANTITY_FEW:
                    return "Few";
                case QUANTITY_MANY:
                    return "Many";
                default:
                    return "Other";
            }
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

        public static string FormatCallDuration(int duration)
        {
            if (duration > 3600)
            {
                var result = Declension("Hours", duration / 3600);
                var minutes = duration % 3600 / 60;
                if (minutes > 0)
                {
                    result += ", " + Declension("Minutes", minutes);
                }
                return result;
            }
            else if (duration > 60)
            {
                return Declension("Minutes", duration / 60);
            }
            else
            {
                return Declension("Seconds", duration);
            }
        }

        public static string FormatTTLString(int ttl)
        {
            if (ttl < 60)
            {
                return Declension("Seconds", ttl);
            }
            else if (ttl < 60 * 60)
            {
                return Declension("Minutes", ttl / 60);
            }
            else if (ttl < 60 * 60 * 24)
            {
                return Declension("Hours", ttl / 60 / 60);
            }
            else if (ttl < 60 * 60 * 24 * 7)
            {
                return Declension("Days", ttl / 60 / 60 / 24);
            }
            else
            {
                int days = ttl / 60 / 60 / 24;
                if (ttl % 7 == 0)
                {
                    return Declension("Weeks", days / 7);
                }
                else
                {
                    return string.Format("{0} {1}", Declension("Weeks", days / 7), Declension("Days", days % 7));
                }
            }
        }

        public static string FormatAutoLock(int timeout)
        {
            if (timeout == 0)
            {
                return GetString("AutoLockDisabled");
            }
            else if (timeout < 60 * 60)
            {
                return string.Format(GetString("AutoLockInTime"), Declension("Minutes", timeout / 60));
            }
            else /*if (timeout < 60 * 60 * 24)*/
            {
                return string.Format(GetString("AutoLockInTime"), Declension("Hours", timeout / 60 / 60));
            }
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
            if (_translitChars == null)
            {
                _translitChars = new Dictionary<string, string>(520);
                _translitChars["ȼ"] = "c";
                _translitChars["ᶇ"] = "n";
                _translitChars["ɖ"] = "d";
                _translitChars["ỿ"] = "y";
                _translitChars["ᴓ"] = "o";
                _translitChars["ø"] = "o";
                _translitChars["ḁ"] = "a";
                _translitChars["ʯ"] = "h";
                _translitChars["ŷ"] = "y";
                _translitChars["ʞ"] = "k";
                _translitChars["ừ"] = "u";
                _translitChars["ꜳ"] = "aa";
                _translitChars["ĳ"] = "ij";
                _translitChars["ḽ"] = "l";
                _translitChars["ɪ"] = "i";
                _translitChars["ḇ"] = "b";
                _translitChars["ʀ"] = "r";
                _translitChars["ě"] = "e";
                _translitChars["ﬃ"] = "ffi";
                _translitChars["ơ"] = "o";
                _translitChars["ⱹ"] = "r";
                _translitChars["ồ"] = "o";
                _translitChars["ǐ"] = "i";
                _translitChars["ꝕ"] = "p";
                _translitChars["ý"] = "y";
                _translitChars["ḝ"] = "e";
                _translitChars["ₒ"] = "o";
                _translitChars["ⱥ"] = "a";
                _translitChars["ʙ"] = "b";
                _translitChars["ḛ"] = "e";
                _translitChars["ƈ"] = "c";
                _translitChars["ɦ"] = "h";
                _translitChars["ᵬ"] = "b";
                _translitChars["ṣ"] = "s";
                _translitChars["đ"] = "d";
                _translitChars["ỗ"] = "o";
                _translitChars["ɟ"] = "j";
                _translitChars["ẚ"] = "a";
                _translitChars["ɏ"] = "y";
                _translitChars["л"] = "l";
                _translitChars["ʌ"] = "v";
                _translitChars["ꝓ"] = "p";
                _translitChars["ﬁ"] = "fi";
                _translitChars["ᶄ"] = "k";
                _translitChars["ḏ"] = "d";
                _translitChars["ᴌ"] = "l";
                _translitChars["ė"] = "e";
                _translitChars["ё"] = "yo";
                _translitChars["ᴋ"] = "k";
                _translitChars["ċ"] = "c";
                _translitChars["ʁ"] = "r";
                _translitChars["ƕ"] = "hv";
                _translitChars["ƀ"] = "b";
                _translitChars["ṍ"] = "o";
                _translitChars["ȣ"] = "ou";
                _translitChars["ǰ"] = "j";
                _translitChars["ᶃ"] = "g";
                _translitChars["ṋ"] = "n";
                _translitChars["ɉ"] = "j";
                _translitChars["ǧ"] = "g";
                _translitChars["ǳ"] = "dz";
                _translitChars["ź"] = "z";
                _translitChars["ꜷ"] = "au";
                _translitChars["ǖ"] = "u";
                _translitChars["ᵹ"] = "g";
                _translitChars["ȯ"] = "o";
                _translitChars["ɐ"] = "a";
                _translitChars["ą"] = "a";
                _translitChars["õ"] = "o";
                _translitChars["ɻ"] = "r";
                _translitChars["ꝍ"] = "o";
                _translitChars["ǟ"] = "a";
                _translitChars["ȴ"] = "l";
                _translitChars["ʂ"] = "s";
                _translitChars["ﬂ"] = "fl";
                _translitChars["ȉ"] = "i";
                _translitChars["ⱻ"] = "e";
                _translitChars["ṉ"] = "n";
                _translitChars["ï"] = "i";
                _translitChars["ñ"] = "n";
                _translitChars["ᴉ"] = "i";
                _translitChars["ʇ"] = "t";
                _translitChars["ẓ"] = "z";
                _translitChars["ỷ"] = "y";
                _translitChars["ȳ"] = "y";
                _translitChars["ṩ"] = "s";
                _translitChars["ɽ"] = "r";
                _translitChars["ĝ"] = "g";
                _translitChars["в"] = "v";
                _translitChars["ᴝ"] = "u";
                _translitChars["ḳ"] = "k";
                _translitChars["ꝫ"] = "et";
                _translitChars["ī"] = "i";
                _translitChars["ť"] = "t";
                _translitChars["ꜿ"] = "c";
                _translitChars["ʟ"] = "l";
                _translitChars["ꜹ"] = "av";
                _translitChars["û"] = "u";
                _translitChars["æ"] = "ae";
                _translitChars["и"] = "i";
                _translitChars["ă"] = "a";
                _translitChars["ǘ"] = "u";
                _translitChars["ꞅ"] = "s";
                _translitChars["ᵣ"] = "r";
                _translitChars["ᴀ"] = "a";
                _translitChars["ƃ"] = "b";
                _translitChars["ḩ"] = "h";
                _translitChars["ṧ"] = "s";
                _translitChars["ₑ"] = "e";
                _translitChars["ʜ"] = "h";
                _translitChars["ẋ"] = "x";
                _translitChars["ꝅ"] = "k";
                _translitChars["ḋ"] = "d";
                _translitChars["ƣ"] = "oi";
                _translitChars["ꝑ"] = "p";
                _translitChars["ħ"] = "h";
                _translitChars["ⱴ"] = "v";
                _translitChars["ẇ"] = "w";
                _translitChars["ǹ"] = "n";
                _translitChars["ɯ"] = "m";
                _translitChars["ɡ"] = "g";
                _translitChars["ɴ"] = "n";
                _translitChars["ᴘ"] = "p";
                _translitChars["ᵥ"] = "v";
                _translitChars["ū"] = "u";
                _translitChars["ḃ"] = "b";
                _translitChars["ṗ"] = "p";
                _translitChars["ь"] = "";
                _translitChars["å"] = "a";
                _translitChars["ɕ"] = "c";
                _translitChars["ọ"] = "o";
                _translitChars["ắ"] = "a";
                _translitChars["ƒ"] = "f";
                _translitChars["ǣ"] = "ae";
                _translitChars["ꝡ"] = "vy";
                _translitChars["ﬀ"] = "ff";
                _translitChars["ᶉ"] = "r";
                _translitChars["ô"] = "o";
                _translitChars["ǿ"] = "o";
                _translitChars["ṳ"] = "u";
                _translitChars["ȥ"] = "z";
                _translitChars["ḟ"] = "f";
                _translitChars["ḓ"] = "d";
                _translitChars["ȇ"] = "e";
                _translitChars["ȕ"] = "u";
                _translitChars["п"] = "p";
                _translitChars["ȵ"] = "n";
                _translitChars["ʠ"] = "q";
                _translitChars["ấ"] = "a";
                _translitChars["ǩ"] = "k";
                _translitChars["ĩ"] = "i";
                _translitChars["ṵ"] = "u";
                _translitChars["ŧ"] = "t";
                _translitChars["ɾ"] = "r";
                _translitChars["ƙ"] = "k";
                _translitChars["ṫ"] = "t";
                _translitChars["ꝗ"] = "q";
                _translitChars["ậ"] = "a";
                _translitChars["н"] = "n";
                _translitChars["ʄ"] = "j";
                _translitChars["ƚ"] = "l";
                _translitChars["ᶂ"] = "f";
                _translitChars["д"] = "d";
                _translitChars["ᵴ"] = "s";
                _translitChars["ꞃ"] = "r";
                _translitChars["ᶌ"] = "v";
                _translitChars["ɵ"] = "o";
                _translitChars["ḉ"] = "c";
                _translitChars["ᵤ"] = "u";
                _translitChars["ẑ"] = "z";
                _translitChars["ṹ"] = "u";
                _translitChars["ň"] = "n";
                _translitChars["ʍ"] = "w";
                _translitChars["ầ"] = "a";
                _translitChars["ǉ"] = "lj";
                _translitChars["ɓ"] = "b";
                _translitChars["ɼ"] = "r";
                _translitChars["ò"] = "o";
                _translitChars["ẘ"] = "w";
                _translitChars["ɗ"] = "d";
                _translitChars["ꜽ"] = "ay";
                _translitChars["ư"] = "u";
                _translitChars["ᶀ"] = "b";
                _translitChars["ǜ"] = "u";
                _translitChars["ẹ"] = "e";
                _translitChars["ǡ"] = "a";
                _translitChars["ɥ"] = "h";
                _translitChars["ṏ"] = "o";
                _translitChars["ǔ"] = "u";
                _translitChars["ʎ"] = "y";
                _translitChars["ȱ"] = "o";
                _translitChars["ệ"] = "e";
                _translitChars["ế"] = "e";
                _translitChars["ĭ"] = "i";
                _translitChars["ⱸ"] = "e";
                _translitChars["ṯ"] = "t";
                _translitChars["ᶑ"] = "d";
                _translitChars["ḧ"] = "h";
                _translitChars["ṥ"] = "s";
                _translitChars["ë"] = "e";
                _translitChars["ᴍ"] = "m";
                _translitChars["ö"] = "o";
                _translitChars["é"] = "e";
                _translitChars["ı"] = "i";
                _translitChars["ď"] = "d";
                _translitChars["ᵯ"] = "m";
                _translitChars["ỵ"] = "y";
                _translitChars["я"] = "ya";
                _translitChars["ŵ"] = "w";
                _translitChars["ề"] = "e";
                _translitChars["ứ"] = "u";
                _translitChars["ƶ"] = "z";
                _translitChars["ĵ"] = "j";
                _translitChars["ḍ"] = "d";
                _translitChars["ŭ"] = "u";
                _translitChars["ʝ"] = "j";
                _translitChars["ж"] = "zh";
                _translitChars["ê"] = "e";
                _translitChars["ǚ"] = "u";
                _translitChars["ġ"] = "g";
                _translitChars["ṙ"] = "r";
                _translitChars["ƞ"] = "n";
                _translitChars["ъ"] = "";
                _translitChars["ḗ"] = "e";
                _translitChars["ẝ"] = "s";
                _translitChars["ᶁ"] = "d";
                _translitChars["ķ"] = "k";
                _translitChars["ᴂ"] = "ae";
                _translitChars["ɘ"] = "e";
                _translitChars["ợ"] = "o";
                _translitChars["ḿ"] = "m";
                _translitChars["ꜰ"] = "f";
                _translitChars["а"] = "a";
                _translitChars["ẵ"] = "a";
                _translitChars["ꝏ"] = "oo";
                _translitChars["ᶆ"] = "m";
                _translitChars["ᵽ"] = "p";
                _translitChars["ц"] = "ts";
                _translitChars["ữ"] = "u";
                _translitChars["ⱪ"] = "k";
                _translitChars["ḥ"] = "h";
                _translitChars["ţ"] = "t";
                _translitChars["ᵱ"] = "p";
                _translitChars["ṁ"] = "m";
                _translitChars["á"] = "a";
                _translitChars["ᴎ"] = "n";
                _translitChars["ꝟ"] = "v";
                _translitChars["è"] = "e";
                _translitChars["ᶎ"] = "z";
                _translitChars["ꝺ"] = "d";
                _translitChars["ᶈ"] = "p";
                _translitChars["м"] = "m";
                _translitChars["ɫ"] = "l";
                _translitChars["ᴢ"] = "z";
                _translitChars["ɱ"] = "m";
                _translitChars["ṝ"] = "r";
                _translitChars["ṽ"] = "v";
                _translitChars["ũ"] = "u";
                _translitChars["ß"] = "ss";
                _translitChars["т"] = "t";
                _translitChars["ĥ"] = "h";
                _translitChars["ᵵ"] = "t";
                _translitChars["ʐ"] = "z";
                _translitChars["ṟ"] = "r";
                _translitChars["ɲ"] = "n";
                _translitChars["à"] = "a";
                _translitChars["ẙ"] = "y";
                _translitChars["ỳ"] = "y";
                _translitChars["ᴔ"] = "oe";
                _translitChars["ы"] = "i";
                _translitChars["ₓ"] = "x";
                _translitChars["ȗ"] = "u";
                _translitChars["ⱼ"] = "j";
                _translitChars["ẫ"] = "a";
                _translitChars["ʑ"] = "z";
                _translitChars["ẛ"] = "s";
                _translitChars["ḭ"] = "i";
                _translitChars["ꜵ"] = "ao";
                _translitChars["ɀ"] = "z";
                _translitChars["ÿ"] = "y";
                _translitChars["ǝ"] = "e";
                _translitChars["ǭ"] = "o";
                _translitChars["ᴅ"] = "d";
                _translitChars["ᶅ"] = "l";
                _translitChars["ù"] = "u";
                _translitChars["ạ"] = "a";
                _translitChars["ḅ"] = "b";
                _translitChars["ụ"] = "u";
                _translitChars["к"] = "k";
                _translitChars["ằ"] = "a";
                _translitChars["ᴛ"] = "t";
                _translitChars["ƴ"] = "y";
                _translitChars["ⱦ"] = "t";
                _translitChars["з"] = "z";
                _translitChars["ⱡ"] = "l";
                _translitChars["ȷ"] = "j";
                _translitChars["ᵶ"] = "z";
                _translitChars["ḫ"] = "h";
                _translitChars["ⱳ"] = "w";
                _translitChars["ḵ"] = "k";
                _translitChars["ờ"] = "o";
                _translitChars["î"] = "i";
                _translitChars["ģ"] = "g";
                _translitChars["ȅ"] = "e";
                _translitChars["ȧ"] = "a";
                _translitChars["ẳ"] = "a";
                _translitChars["щ"] = "sch";
                _translitChars["ɋ"] = "q";
                _translitChars["ṭ"] = "t";
                _translitChars["ꝸ"] = "um";
                _translitChars["ᴄ"] = "c";
                _translitChars["ẍ"] = "x";
                _translitChars["ủ"] = "u";
                _translitChars["ỉ"] = "i";
                _translitChars["ᴚ"] = "r";
                _translitChars["ś"] = "s";
                _translitChars["ꝋ"] = "o";
                _translitChars["ỹ"] = "y";
                _translitChars["ṡ"] = "s";
                _translitChars["ǌ"] = "nj";
                _translitChars["ȁ"] = "a";
                _translitChars["ẗ"] = "t";
                _translitChars["ĺ"] = "l";
                _translitChars["ž"] = "z";
                _translitChars["ᵺ"] = "th";
                _translitChars["ƌ"] = "d";
                _translitChars["ș"] = "s";
                _translitChars["š"] = "s";
                _translitChars["ᶙ"] = "u";
                _translitChars["ẽ"] = "e";
                _translitChars["ẜ"] = "s";
                _translitChars["ɇ"] = "e";
                _translitChars["ṷ"] = "u";
                _translitChars["ố"] = "o";
                _translitChars["ȿ"] = "s";
                _translitChars["ᴠ"] = "v";
                _translitChars["ꝭ"] = "is";
                _translitChars["ᴏ"] = "o";
                _translitChars["ɛ"] = "e";
                _translitChars["ǻ"] = "a";
                _translitChars["ﬄ"] = "ffl";
                _translitChars["ⱺ"] = "o";
                _translitChars["ȋ"] = "i";
                _translitChars["ᵫ"] = "ue";
                _translitChars["ȡ"] = "d";
                _translitChars["ⱬ"] = "z";
                _translitChars["ẁ"] = "w";
                _translitChars["ᶏ"] = "a";
                _translitChars["ꞇ"] = "t";
                _translitChars["ğ"] = "g";
                _translitChars["ɳ"] = "n";
                _translitChars["ʛ"] = "g";
                _translitChars["ᴜ"] = "u";
                _translitChars["ф"] = "f";
                _translitChars["ẩ"] = "a";
                _translitChars["ṅ"] = "n";
                _translitChars["ɨ"] = "i";
                _translitChars["ᴙ"] = "r";
                _translitChars["ǎ"] = "a";
                _translitChars["ſ"] = "s";
                _translitChars["у"] = "u";
                _translitChars["ȫ"] = "o";
                _translitChars["ɿ"] = "r";
                _translitChars["ƭ"] = "t";
                _translitChars["ḯ"] = "i";
                _translitChars["ǽ"] = "ae";
                _translitChars["ⱱ"] = "v";
                _translitChars["ɶ"] = "oe";
                _translitChars["ṃ"] = "m";
                _translitChars["ż"] = "z";
                _translitChars["ĕ"] = "e";
                _translitChars["ꜻ"] = "av";
                _translitChars["ở"] = "o";
                _translitChars["ễ"] = "e";
                _translitChars["ɬ"] = "l";
                _translitChars["ị"] = "i";
                _translitChars["ᵭ"] = "d";
                _translitChars["ﬆ"] = "st";
                _translitChars["ḷ"] = "l";
                _translitChars["ŕ"] = "r";
                _translitChars["ᴕ"] = "ou";
                _translitChars["ʈ"] = "t";
                _translitChars["ā"] = "a";
                _translitChars["э"] = "e";
                _translitChars["ḙ"] = "e";
                _translitChars["ᴑ"] = "o";
                _translitChars["ç"] = "c";
                _translitChars["ᶊ"] = "s";
                _translitChars["ặ"] = "a";
                _translitChars["ų"] = "u";
                _translitChars["ả"] = "a";
                _translitChars["ǥ"] = "g";
                _translitChars["р"] = "r";
                _translitChars["ꝁ"] = "k";
                _translitChars["ẕ"] = "z";
                _translitChars["ŝ"] = "s";
                _translitChars["ḕ"] = "e";
                _translitChars["ɠ"] = "g";
                _translitChars["ꝉ"] = "l";
                _translitChars["ꝼ"] = "f";
                _translitChars["ᶍ"] = "x";
                _translitChars["х"] = "h";
                _translitChars["ǒ"] = "o";
                _translitChars["ę"] = "e";
                _translitChars["ổ"] = "o";
                _translitChars["ƫ"] = "t";
                _translitChars["ǫ"] = "o";
                _translitChars["i̇"] = "i";
                _translitChars["ṇ"] = "n";
                _translitChars["ć"] = "c";
                _translitChars["ᵷ"] = "g";
                _translitChars["ẅ"] = "w";
                _translitChars["ḑ"] = "d";
                _translitChars["ḹ"] = "l";
                _translitChars["ч"] = "ch";
                _translitChars["œ"] = "oe";
                _translitChars["ᵳ"] = "r";
                _translitChars["ļ"] = "l";
                _translitChars["ȑ"] = "r";
                _translitChars["ȭ"] = "o";
                _translitChars["ᵰ"] = "n";
                _translitChars["ᴁ"] = "ae";
                _translitChars["ŀ"] = "l";
                _translitChars["ä"] = "a";
                _translitChars["ƥ"] = "p";
                _translitChars["ỏ"] = "o";
                _translitChars["į"] = "i";
                _translitChars["ȓ"] = "r";
                _translitChars["ǆ"] = "dz";
                _translitChars["ḡ"] = "g";
                _translitChars["ṻ"] = "u";
                _translitChars["ō"] = "o";
                _translitChars["ľ"] = "l";
                _translitChars["ẃ"] = "w";
                _translitChars["ț"] = "t";
                _translitChars["ń"] = "n";
                _translitChars["ɍ"] = "r";
                _translitChars["ȃ"] = "a";
                _translitChars["ü"] = "u";
                _translitChars["ꞁ"] = "l";
                _translitChars["ᴐ"] = "o";
                _translitChars["ớ"] = "o";
                _translitChars["ᴃ"] = "b";
                _translitChars["ɹ"] = "r";
                _translitChars["ᵲ"] = "r";
                _translitChars["ʏ"] = "y";
                _translitChars["ᵮ"] = "f";
                _translitChars["ⱨ"] = "h";
                _translitChars["ŏ"] = "o";
                _translitChars["ú"] = "u";
                _translitChars["ṛ"] = "r";
                _translitChars["ʮ"] = "h";
                _translitChars["ó"] = "o";
                _translitChars["ů"] = "u";
                _translitChars["ỡ"] = "o";
                _translitChars["ṕ"] = "p";
                _translitChars["ᶖ"] = "i";
                _translitChars["ự"] = "u";
                _translitChars["ã"] = "a";
                _translitChars["ᵢ"] = "i";
                _translitChars["ṱ"] = "t";
                _translitChars["ể"] = "e";
                _translitChars["ử"] = "u";
                _translitChars["í"] = "i";
                _translitChars["ɔ"] = "o";
                _translitChars["с"] = "s";
                _translitChars["й"] = "i";
                _translitChars["ɺ"] = "r";
                _translitChars["ɢ"] = "g";
                _translitChars["ř"] = "r";
                _translitChars["ẖ"] = "h";
                _translitChars["ű"] = "u";
                _translitChars["ȍ"] = "o";
                _translitChars["ш"] = "sh";
                _translitChars["ḻ"] = "l";
                _translitChars["ḣ"] = "h";
                _translitChars["ȶ"] = "t";
                _translitChars["ņ"] = "n";
                _translitChars["ᶒ"] = "e";
                _translitChars["ì"] = "i";
                _translitChars["ẉ"] = "w";
                _translitChars["б"] = "b";
                _translitChars["ē"] = "e";
                _translitChars["ᴇ"] = "e";
                _translitChars["ł"] = "l";
                _translitChars["ộ"] = "o";
                _translitChars["ɭ"] = "l";
                _translitChars["ẏ"] = "y";
                _translitChars["ᴊ"] = "j";
                _translitChars["ḱ"] = "k";
                _translitChars["ṿ"] = "v";
                _translitChars["ȩ"] = "e";
                _translitChars["â"] = "a";
                _translitChars["ş"] = "s";
                _translitChars["ŗ"] = "r";
                _translitChars["ʋ"] = "v";
                _translitChars["ₐ"] = "a";
                _translitChars["ↄ"] = "c";
                _translitChars["ᶓ"] = "e";
                _translitChars["ɰ"] = "m";
                _translitChars["е"] = "e";
                _translitChars["ᴡ"] = "w";
                _translitChars["ȏ"] = "o";
                _translitChars["č"] = "c";
                _translitChars["ǵ"] = "g";
                _translitChars["ĉ"] = "c";
                _translitChars["ю"] = "yu";
                _translitChars["ᶗ"] = "o";
                _translitChars["ꝃ"] = "k";
                _translitChars["ꝙ"] = "q";
                _translitChars["г"] = "g";
                _translitChars["ṑ"] = "o";
                _translitChars["ꜱ"] = "s";
                _translitChars["ṓ"] = "o";
                _translitChars["ȟ"] = "h";
                _translitChars["ő"] = "o";
                _translitChars["ꜩ"] = "tz";
                _translitChars["ẻ"] = "e";
                _translitChars["о"] = "o";
            }

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
            public abstract int QuantityForNumber(int n);
        }

        private class PluralRules_Zero : PluralRules
        {
            public override int QuantityForNumber(int count)
            {
                if (count == 0 || count == 1)
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
            public override int QuantityForNumber(int count)
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
            public override int QuantityForNumber(int count)
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
            public override int QuantityForNumber(int count)
            {
                if (count >= 0 && count <= 1)
                {
                    return QUANTITY_ONE;
                }
                else if (count >= 2 && count <= 10)
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
            public override int QuantityForNumber(int count)
            {
                int rem100 = count % 100;
                if (rem100 == 1)
                {
                    return QUANTITY_ONE;
                }
                else if (rem100 == 2)
                {
                    return QUANTITY_TWO;
                }
                else if (rem100 >= 3 && rem100 <= 4)
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
            public override int QuantityForNumber(int count)
            {
                int rem100 = count % 100;
                if (count == 1)
                {
                    return QUANTITY_ONE;
                }
                else if ((count == 0 || (rem100 >= 1 && rem100 <= 19)))
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
            public override int QuantityForNumber(int count)
            {
                int rem100 = count % 100;
                int rem10 = count % 10;
                if (count == 1)
                {
                    return QUANTITY_ONE;
                }
                else if (rem10 >= 2 && rem10 <= 4 && !(rem100 >= 12 && rem100 <= 14) && !(rem100 >= 22 && rem100 <= 24))
                {
                    return QUANTITY_FEW;
                }
                else
                {
                    return QUANTITY_OTHER;
                }
            }
        }

        private class PluralRules_One : PluralRules
        {
            public override int QuantityForNumber(int count)
            {
                return count == 1 ? QUANTITY_ONE : QUANTITY_OTHER;
            }
        }

        private class PluralRules_None : PluralRules
        {
            public override int QuantityForNumber(int count)
            {
                return QUANTITY_OTHER;
            }
        }

        private class PluralRules_Maltese : PluralRules
        {
            public override int QuantityForNumber(int count)
            {
                int rem100 = count % 100;
                if (count == 1)
                {
                    return QUANTITY_ONE;
                }
                else if (count == 0 || (rem100 >= 2 && rem100 <= 10))
                {
                    return QUANTITY_FEW;
                }
                else if (rem100 >= 11 && rem100 <= 19)
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
            public override int QuantityForNumber(int count)
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
            public override int QuantityForNumber(int count)
            {
                int rem100 = count % 100;
                int rem10 = count % 10;
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
            public override int QuantityForNumber(int count)
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
            public override int QuantityForNumber(int count)
            {
                if (count == 0)
                {
                    return QUANTITY_ZERO;
                }
                else if (count > 0 && count < 2)
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
            public override int QuantityForNumber(int count)
            {
                if (count >= 0 && count < 2)
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
            public override int QuantityForNumber(int count)
            {
                if (count == 1)
                {
                    return QUANTITY_ONE;
                }
                else if (count >= 2 && count <= 4)
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
            public override int QuantityForNumber(int count)
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
            public override int QuantityForNumber(int count)
            {
                int rem100 = count % 100;
                int rem10 = count % 10;
                if (rem10 == 1 && rem100 != 11)
                {
                    return QUANTITY_ONE;
                }
                else if (rem10 >= 2 && rem10 <= 4 && !(rem100 >= 12 && rem100 <= 14))
                {
                    return QUANTITY_FEW;
                }
                else if ((rem10 == 0 || (rem10 >= 5 && rem10 <= 9) || (rem100 >= 11 && rem100 <= 14)))
                {
                    return QUANTITY_MANY;
                }
                else
                {
                    return QUANTITY_OTHER;
                }
            }
        }

        private class PluralRules_Arabic : PluralRules
        {
            public override int QuantityForNumber(int count)
            {
                int rem100 = count % 100;
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
                else if (rem100 >= 3 && rem100 <= 10)
                {
                    return QUANTITY_FEW;
                }
                else if (rem100 >= 11 && rem100 <= 99)
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
