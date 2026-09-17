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

        public ChartHorizontalLinesData(long newMaxHeight, long newMinHeight, bool useMinHeight, string currency)
            : this(newMaxHeight, newMinHeight, useMinHeight, currency, 0)
        {
        }

        // The step arithmetic is double rather than float: a TON chart's values are nanograms, and
        // past 16.7M a float cannot tell consecutive integers apart - which would quantise the step
        // itself, and the step is what every label is built from.
        public ChartHorizontalLinesData(long newMaxHeight, long newMinHeight, bool useMinHeight, string currency, double k)
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

                for (int i = 1; i < n; i++)
                {
                    values[i] = i * step;
                    valuesStr[i] = formatWholeNumber(values[i], 0, currency);
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
                    valuesStr[i] = formatWholeNumber(values[i], dif, currency);
                    if (k > 0)
                    {
                        double v = values[i] / k;
                        if (skipFloatValues)
                        {
                            if (v - (long)v < 0.01d)
                            {
                                valuesStr2[i] = formatWholeNumber((long)v, (long)(dif / k), currency);
                            }
                            else
                            {
                                valuesStr2[i] = "";
                            }
                        }
                        else
                        {
                            valuesStr2[i] = formatWholeNumber((long)v, (long)(dif / k), currency);
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

        private static string formatWholeNumber(long v, long dif, string currency)
        {
            if (currency != null)
            {
                return Formatter.FormatAmount(v, currency);
            }

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
