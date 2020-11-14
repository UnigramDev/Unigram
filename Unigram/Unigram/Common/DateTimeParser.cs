using System;
using System.Collections.Generic;
using System.Globalization;
using Telegram.Td.Api;
using Unigram.Native;

namespace Unigram.Common
{
    public class DateTimeParser
    {
        private enum DateRangeFormat
        {
            Day,
            Month,
            Year
        }

        private readonly static Dictionary<string, DateRangeFormat> _formats = new Dictionary<string, DateRangeFormat>
        {
            { "dd MMM yyyy", DateRangeFormat.Day },
            { "dd MMM", DateRangeFormat.Day },
            { "MMM yyyy", DateRangeFormat.Month },
            { "MMM", DateRangeFormat.Month },
            { "yyyy", DateRangeFormat.Year }
        };

        public static IList<DateRange> Parse(string query)
        {
            var monthEngl = new string[12];
            var monthInLocal = new string[12];

            var cultureEngl = new CultureInfo("en");
            var cultureInLocal = new CultureInfo(NativeUtils.GetCurrentCulture());

            for (int i = 1; i <= 12; i++)
            {
                monthEngl[i - 1] = cultureEngl.DateTimeFormat.GetMonthName(i).ToLower();
                monthInLocal[i - 1] = cultureInLocal.DateTimeFormat.GetMonthName(i).ToLower();
            }

            var split = query.ToLower().Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < split.Length; i++)
            {
                if (split[i].Length > 3)
                {
                    for (int j = 0; j < 12; j++)
                    {
                        if (monthEngl[j].StartsWith(split[i]) || monthInLocal[j].StartsWith(split[i]))
                        {
                            split[i] = split[i].Substring(0, 3);
                            break;
                        }
                    }
                }
            }

            var text = string.Join(' ', split);
            var results = new List<DateRange>();

            foreach (var format in _formats)
            {
                if (TryParseExact(text, format.Key, cultureEngl, cultureInLocal, out DateTime result))
                {
                    DateTime start = result.Date;
                    DateTime end = result.Date;

                    switch (format.Value)
                    {
                        case DateRangeFormat.Day:
                            end = result.Date.AddDays(1);
                            break;
                        case DateRangeFormat.Month:
                            end = result.Date.AddMonths(1);
                            break;
                        case DateRangeFormat.Year:
                            end = result.Date.AddYears(1);
                            break;
                    }

                    for (int i = start.Year; i >= 2010; i--)
                    {
                        results.Add(new DateRange
                        {
                            StartDate = start.AddYears(-(start.Year - i)).ToTimestamp(),
                            EndDate = end.AddYears(-(start.Year - i)).ToTimestamp() - 1
                        });
                    }

                    break;
                }
            }

            return results;
        }

        static bool TryParseExact(string text, string format, CultureInfo cultureEngl, CultureInfo cultureInLocal, out DateTime result)
        {
            if (DateTime.TryParseExact(text, format, cultureEngl, DateTimeStyles.None, out result))
            {
                return true;
            }

            return DateTime.TryParseExact(text, format, cultureInLocal, DateTimeStyles.None, out result);
        }
    }
}
