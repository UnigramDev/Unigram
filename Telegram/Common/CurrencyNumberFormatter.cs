//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using System.Linq;
using Windows.Globalization.NumberFormatting;

namespace Telegram.Common
{
    public partial class CurrencyNumberFormatter : INumberFormatter2, INumberParser
    {
        private readonly CurrencyFormatter _formatter;
        private readonly string _currencySymbol;

        public CurrencyNumberFormatter(string currencyCode, IEnumerable<string> languages, string geographicRegion)
        {
            if (string.IsNullOrEmpty(currencyCode))
            {
                currencyCode = "USD";
            }

#if NET9_0_OR_GREATER
            languages = languages.ToList();
#endif

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

        /// <summary>
        /// The currency's own decoration around a number that has already been written out - the
        /// symbol where this currency puts it, and whatever separates the two.
        /// </summary>
        /// <remarks>
        /// For an amount that has to be formatted elsewhere: one with more decimals than the
        /// currency has, or one standing beside a number in the app's own language, which is not
        /// always the language this formatter speaks.
        ///
        /// The layout is read off a formatted zero rather than assembled, so a currency that
        /// trails its symbol, or spaces it differently, still comes out right.
        /// </remarks>
        public string Format(string number)
        {
            var formatted = _formatter.Format(0);

            var first = -1;
            var last = -1;

            for (int i = 0; i < formatted.Length; i++)
            {
                if (char.IsDigit(formatted[i]))
                {
                    if (first < 0)
                    {
                        first = i;
                    }

                    last = i;
                }
            }

            // Everything from the first digit to the last is the number, separator included.
            return first < 0
                ? number
                : formatted.Substring(0, first) + number + formatted.Substring(last + 1);
        }

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
