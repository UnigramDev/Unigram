using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unigram.Charts.DataView
{
    public class ChartHorizontalLinesData
    {

        public int[] values;
        public String[] valuesStr;
        public String[] valuesStr2;
        public int alpha;

        public int fixedAlpha = 255;

        public ChartHorizontalLinesData(int newMaxHeight, int newMinHeight, bool useMinHeight)
            : this(newMaxHeight, newMinHeight, useMinHeight, 0)
        {
        }

        public ChartHorizontalLinesData(int newMaxHeight, int newMinHeight, bool useMinHeight, float k)
        {
            if (!useMinHeight)
            {
                int v = newMaxHeight;
                if (newMaxHeight > 100)
                {
                    v = round(newMaxHeight);
                }

                int step = Math.Max(1, (int)Math.Ceiling(v / 5f));

                int n;
                if (v < 6)
                {
                    n = Math.Max(2, v + 1);
                }
                else if (v / 2 < 6)
                {
                    n = v / 2 + 1;
                    if (v % 2 != 0)
                    {
                        n++;
                    }
                }
                else
                {
                    n = 6;
                }

                values = new int[n];
                valuesStr = new String[n];

                for (int i = 1; i < n; i++)
                {
                    values[i] = i * step;
                    valuesStr[i] = formatWholeNumber(values[i], 0);
                }
            }
            else
            {
                int n;
                int dif = newMaxHeight - newMinHeight;
                float step;
                if (dif == 0)
                {
                    newMinHeight--;
                    n = 3;
                    step = 1f;
                }
                else if (dif < 6)
                {
                    n = Math.Max(2, dif + 1);
                    step = 1f;
                }
                else if (dif / 2 < 6)
                {
                    n = dif / 2 + dif % 2 + 1;
                    step = 2f;
                }
                else
                {
                    step = (newMaxHeight - newMinHeight) / 5f;
                    if (step <= 0)
                    {
                        step = 1;
                        n = Math.Max(2, newMaxHeight - newMinHeight + 1);
                    }
                    else
                    {
                        n = 6;
                    }
                }

                values = new int[n];
                valuesStr = new String[n];
                if (k > 0) valuesStr2 = new String[n];
                bool skipFloatValues = step / k < 1;
                for (int i = 0; i < n; i++)
                {
                    values[i] = newMinHeight + (int)(i * step);
                    valuesStr[i] = formatWholeNumber(values[i], dif);
                    if (k > 0)
                    {
                        float v = (values[i] / k);
                        if (skipFloatValues)
                        {
                            if (v - ((int)v) < 0.01f)
                            {
                                valuesStr2[i] = formatWholeNumber((int)v, (int)(dif / k));
                            }
                            else
                            {
                                valuesStr2[i] = "";
                            }
                        }
                        else
                        {
                            valuesStr2[i] = formatWholeNumber((int)v, (int)(dif / k));
                        }
                    }
                }
            }
        }

        public static int lookupHeight(int maxValue)
        {
            int v = maxValue;
            if (maxValue > 100)
            {
                v = round(maxValue);
            }

            int step = (int)Math.Ceiling(v / 5f);
            return step * 5;
        }

        public static readonly String[] s = { "", "K", "M", "G", "T", "P" };

        public static String formatWholeNumber(int v, int dif)
        {
            if (v == 0)
            {
                return "0";
            }
            float num_ = v;
            int count = 0;
            if (dif == 0) dif = v;
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
                if (num_ == (int)num_)
                {
                    //return String.Format(Locale.ENGLISH, "%s%s", formatCount((int)num_), s[count]);
                    return String.Format(CultureInfo.InvariantCulture, "{0}{1}", formatCount((int)num_), s[count]);
                }
                else
                {
                    //return String.Format(Locale.ENGLISH, "%.1f%s", num_, s[count]);
                    return String.Format(CultureInfo.InvariantCulture, "{0:F1}{1}", num_, s[count]);
                }
            }
        }

        private static int round(int maxValue)
        {
            float k = maxValue / 5;
            if (k % 10 == 0) return maxValue;
            else return ((maxValue / 10 + 1) * 10);
        }

        public static String formatCount(int count)
        {
            if (count < 1000) return count.ToString();

            List<String> strings = new List<String>();
            while (count != 0)
            {
                int mod = count % 1000;
                count /= 1000;
                if (count > 0)
                {
                    //strings.Add(String.format(Locale.ENGLISH, "%03d", mod));
                    strings.Add(String.Format(CultureInfo.InvariantCulture, "{0:D3}", mod));
                }
                else
                {
                    strings.Add(mod.ToString());
                }
            }
            StringBuilder stringBuilder = new StringBuilder();
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
