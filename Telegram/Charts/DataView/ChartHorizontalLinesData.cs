//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Telegram.Charts.Data;
using Telegram.Converters;

namespace Telegram.Charts.DataView
{
    public partial class ChartHorizontalLinesData
    {

        public long[] values;
        public string[] valuesStr;
        public string[] valuesStr2;
        public int alpha;

        public int fixedAlpha = 255;

        public ChartHorizontalLinesData(long newMaxHeight, long newMinHeight, bool useMinHeight, int formatter)
            : this(newMaxHeight, newMinHeight, useMinHeight, formatter, 0)
        {
        }

        // The step arithmetic is double rather than float: a TON chart's values are nanograms, and
        // past 16.7M a float cannot tell consecutive integers apart - which would quantise the step
        // itself, and the step is what every label is built from.
        public ChartHorizontalLinesData(long newMaxHeight, long newMinHeight, bool useMinHeight, int formatter, double k)
        {
            if (!useMinHeight)
            {
                long v = newMaxHeight;
                if (newMaxHeight > 100)
                {
                    v = round(newMaxHeight);
                }

                long step = Math.Max(1, (long)Math.Ceiling(v / 5d));

                int n;
                if (v < 6)
                {
                    n = (int)Math.Max(2, v + 1);
                }
                else if (v / 2 < 6)
                {
                    n = (int)(v / 2 + 1);
                    if (v % 2 != 0)
                    {
                        n++;
                    }
                }
                else
                {
                    n = 6;
                }

                values = new long[n];
                valuesStr = new string[n];
                if (k > 0)
                {
                    valuesStr2 = new string[n];
                }

                bool skipFloatValues = step / k < 1;
                for (int i = 1; i < n; i++)
                {
                    values[i] = i * step;
                    valuesStr[i] = Format(0, values[i], formatter);

                    if (k > 0)
                    {
                        double v2 = values[i] / k;
                        if (skipFloatValues && v2 - (long)v2 >= 0.01d && formatter == ChartData.FORMATTER_DEFAULT)
                        {
                            valuesStr2[i] = string.Empty;
                        }
                        else
                        {
                            valuesStr2[i] = Format(1, (long)v2, formatter);
                        }
                    }
                }
            }
            else
            {
                int n;
                long dif = newMaxHeight - newMinHeight;
                double step;
                if (dif == 0)
                {
                    newMinHeight--;
                    n = 3;
                    step = 1d;
                }
                else if (dif < 6)
                {
                    n = (int)Math.Max(2, dif + 1);
                    step = 1d;
                }
                else if (dif / 2 < 6)
                {
                    n = (int)(dif / 2 + dif % 2 + 1);
                    step = 2d;
                }
                else
                {
                    step = (newMaxHeight - newMinHeight) / 5d;
                    if (step <= 0)
                    {
                        step = 1;
                        n = (int)Math.Max(2, newMaxHeight - newMinHeight + 1);
                    }
                    else
                    {
                        n = 6;
                    }
                }

                values = new long[n];
                valuesStr = new string[n];
                if (k > 0)
                {
                    valuesStr2 = new string[n];
                }

                bool skipFloatValues = step / k < 1;
                for (int i = 0; i < n; i++)
                {
                    values[i] = newMinHeight + (long)(i * step);
                    valuesStr[i] = Format(0, values[i], formatter, dif);

                    if (k > 0)
                    {
                        double v = values[i] / k;
                        if (skipFloatValues && v - (long)v >= 0.01d && formatter == ChartData.FORMATTER_DEFAULT)
                        {
                            valuesStr2[i] = string.Empty;
                        }
                        else
                        {
                            valuesStr2[i] = Format(1, (long)v, formatter, (long)(dif / k));
                        }
                    }
                }
            }
        }

        public static long lookupHeight(long maxValue)
        {
            long v = maxValue;
            if (maxValue > 100)
            {
                v = round(maxValue);
            }

            long step = (long)Math.Ceiling(v / 5d);
            return step * 5;
        }

        public static readonly string[] s = { "", "K", "M", "G", "T", "P" };

        /// <summary>
        /// Formats one axis label. <paramref name="a"/> is the column: 0 is the value in its own
        /// currency on the left, 1 is the same value converted to USD on the right.
        /// </summary>
        /// <remarks>
        /// Toncoin is formatted here rather than through Formatter.FormatAmount, which renders TON
        /// with no decimals at all - every label on a revenue axis would read the same. Android
        /// shows two decimals above one TON and up to six below, which is what this mirrors.
        /// </remarks>
        public static string Format(int a, long v, int formatter, long dif = 0)
        {
            if (formatter == ChartData.FORMATTER_TON)
            {
                if (a == 1)
                {
                    return "\u2248" + Formatter.FormatAmount(v, "USD");
                }

                var amount = v / Constants.ToncoinMin;
                return "TON " + amount.ToString(v > Constants.ToncoinMin ? "0.00" : "0.00####", CultureInfo.InvariantCulture);
            }
            else if (formatter == ChartData.FORMATTER_XTR)
            {
                if (a == 1)
                {
                    return "\u2248" + Formatter.FormatAmount(v, "USD");
                }

                return Formatter.FormatAmount(v, "XTR");
            }

            return formatWholeNumber(v, dif);
        }

        private static string formatWholeNumber(long v, long dif)
        {
            if (v == 0)
            {
                return "0";
            }
            double num_ = v;
            int count = 0;
            if (dif == 0)
            {
                dif = v;
            }

            if (dif < 1000)
            {
                return formatCount(v);
            }
            while (dif >= 1000 && count < s.Length - 1)
            {
                dif /= 1000;
                num_ /= 1000;
                count++;
            }
            if (num_ < 0.1)
            {
                return "0";
            }
            else
            {
                if (num_ == (long)num_)
                {
                    //return String.Format(Locale.ENGLISH, "%s%s", formatCount((int)num_), s[count]);
                    return string.Format(CultureInfo.InvariantCulture, "{0}{1}", formatCount((long)num_), s[count]);
                }
                else
                {
                    //return String.Format(Locale.ENGLISH, "%.1f%s", num_, s[count]);
                    return string.Format(CultureInfo.InvariantCulture, "{0:F1}{1}", num_, s[count]);
                }
            }
        }

        private static long round(long maxValue)
        {
            long k = maxValue / 5;
            if (k % 10 == 0)
            {
                return maxValue;
            }
            else
            {
                return (maxValue / 10 + 1) * 10;
            }
        }

        public static string formatCount(long count)
        {
            if (count < 1000)
            {
                return count.ToString();
            }

            List<string> strings = new();
            while (count != 0)
            {
                long mod = count % 1000;
                count /= 1000;
                if (count > 0)
                {
                    //strings.Add(String.format(Locale.ENGLISH, "%03d", mod));
                    strings.Add(string.Format(CultureInfo.InvariantCulture, "{0:D3}", mod));
                }
                else
                {
                    strings.Add(mod.ToString());
                }
            }
            StringBuilder stringBuilder = new();
            for (int i = strings.Count - 1; i >= 0; i--)
            {
                stringBuilder.Append(strings[i]);
                if (i != 0)
                {
                    stringBuilder.Append(",");
                }
            }

            return stringBuilder.ToString();
        }


    }
}
