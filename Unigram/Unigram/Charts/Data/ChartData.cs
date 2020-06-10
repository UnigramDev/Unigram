using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Unigram.Common;
using Unigram.Converters;
using Windows.Data.Json;
using Windows.Globalization.DateTimeFormatting;
using Windows.UI;

namespace Unigram.Charts.Data
{
    public class ChartData
    {
        public long[] x;
        public float[] xPercentage;
        public String[] daysLookup;
        public List<Line> lines = new List<Line>();
        public int maxValue = 0;
        public int minValue = int.MaxValue;

        public float oneDayPercentage = 0f;

        protected ChartData()
        {
        }

        protected long timeStep;

        public ChartData(JsonObject jsonObject)
        {
            JsonArray columns = jsonObject.GetNamedArray("columns");

            int n = columns.Count;
            for (uint i = 0; i < columns.Count; i++)
            {
                JsonArray a = columns.GetArrayAt(i);
                if (a.GetStringAt(0).Equals("x"))
                {
                    int len = a.Count - 1;
                    x = new long[len];
                    for (uint j = 0; j < len; j++)
                    {
                        x[j] = (long)a.GetNumberAt(j + 1);
                    }
                }
                else
                {
                    Line l = new Line();
                    lines.Add(l);
                    int len = a.Count - 1;
                    l.id = a.GetStringAt(0);
                    l.y = new int[len];
                    for (uint j = 0; j < len; j++)
                    {
                        l.y[j] = (int)a.GetNumberAt(j + 1);
                        if (l.y[j] > l.maxValue) l.maxValue = l.y[j];
                        if (l.y[j] < l.minValue) l.minValue = l.y[j];
                    }
                }

                if (x.Length > 1)
                {
                    timeStep = x[1] - x[0];
                }
                else
                {
                    timeStep = 86400000L;
                }
                measure();
            }

            JsonObject colors = jsonObject.GetNamedObject("colors");
            JsonObject names = jsonObject.GetNamedObject("names");

            Regex colorPattern = new Regex("(.*)(#.*)", RegexOptions.Compiled);
            for (int i = 0; i < lines.Count; i++)
            {
                ChartData.Line line = lines[i];

                if (colors != null)
                {
                    var matcher = colorPattern.Match(colors.GetNamedString(line.id));
                    if (matcher.Success)
                    {
                        String key = matcher.Groups[1].Value;
                        if (key != null)
                        {
                            line.colorKey = "statisticChartLine_" + matcher.Groups[1].Value;
                        }

                        line.color = matcher.Groups[2].Value.ToColor();
                        //line.colorDark = ColorUtils.blendARGB(Color.WHITE, line.color, 0.85f);
                    }
                }

                if (names != null)
                {
                    line.name = names.GetNamedString(line.id);
                }

            }
        }


        protected virtual void measure()
        {
            int n = x.Length;
            if (n == 0)
            {
                return;
            }
            long start = x[0];
            long end = x[n - 1];

            xPercentage = new float[n];
            if (n == 1)
            {
                xPercentage[0] = 1;
            }
            else
            {
                for (int i = 0; i < n; i++)
                {
                    xPercentage[i] = (float)(x[i] - start) / (float)(end - start);
                }
            }

            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].maxValue > maxValue) maxValue = lines[i].maxValue;
                if (lines[i].minValue < minValue) minValue = lines[i].minValue;

                lines[i].segmentTree = new SegmentTree(lines[i].y);
            }


            daysLookup = new String[(int)((end - start) / timeStep) + 10];
            DateTimeFormatter formatter;
            if (timeStep == 1)
            {
                formatter = null;
            }
            else if (timeStep < 86400000L)
            {
                formatter = BindConvert.Current.ShortTime;
            }
            else
            {
                //formatter = new SimpleDateFormat("MMM d");
                formatter = BindConvert.Current.MonthAbbreviatedDay;
            }

            for (int i = 0; i < daysLookup.Length; i++)
            {
                if (timeStep == 1)
                {
                    //daysLookup[i] = String.Format(Locale.ENGLISH, "%02d:00", i);
                    daysLookup[i] = string.Format(CultureInfo.InvariantCulture, "{0:D2}:00", i);
                }
                else
                {
                    //daysLookup[i] = formatter.format(new Date(start + (i * timeStep)));
                    daysLookup[i] = formatter.Format(Utils.UnixTimestampToDateTime((start + (i * timeStep)) / 1000));
                }
            }

            oneDayPercentage = timeStep / (float)(x[x.Length - 1] - x[0]);
        }

        public String getDayString(int i)
        {
            return daysLookup[(int)((x[i] - x[0]) / timeStep)];
        }

        public int findStartIndex(float v)
        {
            if (v == 0) return 0;
            int n = xPercentage.Length;

            if (n < 2)
            {
                return 0;
            }
            int left = 0;
            int right = n - 1;


            while (left <= right)
            {
                int middle = (right + left) >> 1;
                if (v < xPercentage[middle] && (middle == 0 || v > xPercentage[middle - 1]))
                {
                    return middle;
                }
                if (v == xPercentage[middle])
                {
                    return middle;
                }
                if (v < xPercentage[middle])
                {
                    right = middle - 1;
                }
                else if (v > xPercentage[middle])
                {
                    left = middle + 1;
                }
            }
            return left;
        }

        public int findEndIndex(int left, float v)
        {
            int n = xPercentage.Length;
            if (v == 1f) return n - 1;
            int right = n - 1;

            while (left <= right)
            {
                int middle = (right + left) >> 1;
                if (v > xPercentage[middle] && (middle == n - 1 || v < xPercentage[middle + 1]))
                {
                    return middle;
                }
                if (v == xPercentage[middle])
                {
                    return middle;
                }
                if (v < xPercentage[middle])
                {
                    right = middle - 1;
                }
                else if (v > xPercentage[middle])
                {
                    left = middle + 1;
                }
            }
            return right;
        }


        public int findIndex(int left, int right, float v)
        {

            int n = xPercentage.Length;

            if (v <= xPercentage[left])
            {
                return left;
            }
            if (v >= xPercentage[right])
            {
                return right;
            }

            while (left <= right)
            {
                int middle = (right + left) >> 1;
                if (v > xPercentage[middle] && (middle == n - 1 || v < xPercentage[middle + 1]))
                {
                    return middle;
                }

                if (v == xPercentage[middle])
                {
                    return middle;
                }
                if (v < xPercentage[middle])
                {
                    right = middle - 1;
                }
                else if (v > xPercentage[middle])
                {
                    left = middle + 1;
                }
            }
            return right;
        }

        public class Line
        {
            public int[] y;

            public SegmentTree segmentTree;
            public String id;
            public String name;
            public int maxValue = 0;
            public int minValue = int.MaxValue;
            public String colorKey;
            public Color color = Colors.Black;
            public Color colorDark = Colors.White;
        }
    }
}
