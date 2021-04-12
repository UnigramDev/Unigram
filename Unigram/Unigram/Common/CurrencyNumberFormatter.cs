using System.Collections.Generic;
using System.Linq;
using Windows.Globalization.NumberFormatting;

namespace Unigram.Common
{
    public class CurrencyNumberFormatter : INumberFormatter2, INumberParser
    {
        private readonly CurrencyFormatter _formatter;
        private readonly string _currencySymbol;

        public CurrencyNumberFormatter(string currencyCode, IEnumerable<string> languages, string geographicRegion)
        {
            var formatter = new CurrencyFormatter(currencyCode, languages, geographicRegion);
            var formatted = formatter.Format(0);
            var splitted = formatted.Split('\u00A0');

            for (int i = 0; i < splitted.Length; i++)
            {
                if (splitted[i].Any(x => char.IsDigit(x)))
                {
                    continue;
                }

                _currencySymbol = splitted[i];
            }

            _formatter = formatter;
        }

        public string Format(double value) => _formatter.Format(value);

        public string FormatInt(long value) => _formatter.FormatInt(value);

        public string FormatUInt(ulong value) => _formatter.FormatUInt(value);

        public string FormatDouble(double value) => _formatter.FormatDouble(value);

        public long? ParseInt(string text) => _formatter.ParseInt(ValidateInput(text));

        public ulong? ParseUInt(string text) => _formatter.ParseUInt(ValidateInput(text));

        public double? ParseDouble(string text) => _formatter.ParseDouble(ValidateInput(text));

        private string ValidateInput(string text)
        {
            var trim = text.Trim();
            if (trim.Length > 0 && char.IsDigit(trim[trim.Length - 1]))
            {
                return $"{trim} {_currencySymbol}";
            }

            return text;
        }
    }
}
