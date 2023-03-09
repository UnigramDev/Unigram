//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.Graphics.Canvas.Geometry;
using System.Collections.Generic;
using Telegram.Charts.Data;
using Telegram.Common;
using Windows.UI;
using Windows.UI.Xaml;

namespace Telegram.Charts.DataView
{
    public class LineViewData
    {

        public readonly ChartData.Line line;
        public readonly Paint bottomLinePaint = new Paint();
        public readonly Paint paint = new Paint();
        public readonly Paint selectionPaint = new Paint();

        public CanvasPathBuilder bottomLinePath;
        public CanvasPathBuilder chartPath;
        public CanvasPathBuilder chartPathPicker;
        public ValueAnimator animatorIn;
        public ValueAnimator animatorOut;
        public int linesPathBottomSize;

        public float[] linesPath;
        public float[] linesPathBottom;

        public Color lineColor;

        public bool enabled = true;

        public float alpha = 1f;

        public LineViewData(ChartData.Line line)
        {
            this.line = line;

            lineColor = line.color;

            //paint.setStrokeWidth(AndroidUtilities.dpf2(2));
            //paint.setStyle(Paint.Style.STROKE);
            //if (!BaseChartView.USE_LINES)
            //{
            //    paint.setStrokeJoin(Paint.Join.ROUND);
            //}
            //paint.setColor(line.color);
            paint.StrokeWidth = 2;
            paint.StrokeCap = CanvasCapStyle.Round;
            paint.Color = line.color;

            bottomLinePaint.StrokeWidth = 1;
            //bottomLinePaint.setStyle(Paint.Style.STROKE);
            bottomLinePaint.Color = line.color;

            selectionPaint.StrokeWidth = 10;
            //selectionPaint.setStyle(Paint.Style.STROKE);
            selectionPaint.StrokeCap = CanvasCapStyle.Round;
            selectionPaint.Color = line.color;


            linesPath = new float[line.y.Length << 2];
            linesPathBottom = new float[line.y.Length << 2];

            UpdateColors(ElementTheme.Default);
        }

        private static readonly Dictionary<string, Color> _colorsLight = new Dictionary<string, Color>
        {
            { "StatisticChartLine_blue", ColorEx.FromHex(0xff327FE5) },
            { "StatisticChartLine_green", ColorEx.FromHex(0xff61C752) },
            { "StatisticChartLine_red", ColorEx.FromHex(0xffE05356) },
            { "StatisticChartLine_golden", ColorEx.FromHex(0xffDEBA08) },
            { "StatisticChartLine_lightblue", ColorEx.FromHex(0xff58A8ED) },
            { "StatisticChartLine_lightgreen", ColorEx.FromHex(0xff8FCF39) },
            { "StatisticChartLine_orange", ColorEx.FromHex(0xffE3B727) },
            { "StatisticChartLine_indigo", ColorEx.FromHex(0xff7F79F3) },
            { "StatisticChartLineEmpty", ColorEx.FromHex(0xFFEEEEEE) },

        };

        private static readonly Dictionary<string, Color> _colorsDark = new Dictionary<string, Color>
        {
            { "StatisticChartLine_blue", ColorEx.FromHex(0xFF529FFF) },
            { "StatisticChartLine_green", ColorEx.FromHex(0xFF3DC23F) },
            { "StatisticChartLine_red", ColorEx.FromHex(0xFFF34C44) },
            { "StatisticChartLine_golden", ColorEx.FromHex(0xFFDEAC1F) },
            { "StatisticChartLine_lightblue", ColorEx.FromHex(0xFF3C78EC) },
            { "StatisticChartLine_lightgreen", ColorEx.FromHex(0xFF8FCF39) },
            { "StatisticChartLine_orange", ColorEx.FromHex(0xFFE9C41A) },
            { "StatisticChartLine_indigo", ColorEx.FromHex(0xFF875CE5) },
            { "StatisticChartLineEmpty", ColorEx.FromHex(0xFFEEEEEE) },
        };

        public virtual void UpdateColors(ElementTheme theme)
        {
            IDictionary<string, Color> colors;
            if (theme == ElementTheme.Dark)
            {
                colors = _colorsDark;
            }
            else
            {
                colors = _colorsLight;
            }

            if (line.colorKey != null && colors.TryGetValue(line.colorKey, out Color color))
            {
                lineColor = color;
            }
            else
            {
                lineColor = line.color;
            }

            paint.Color = lineColor;
            bottomLinePaint.Color = lineColor;
            selectionPaint.Color = lineColor;

            //if (line.colorKey != null && Theme.hasThemeKey(line.colorKey))
            //{
            //    lineColor = Theme.getColor(line.colorKey);
            //}
            //else
            //{
            //    int color = Theme.getColor(Theme.key_windowBackgroundWhite);
            //    bool darkBackground = ColorUtils.calculateLuminance(color) < 0.5f;
            //    lineColor = darkBackground ? line.colorDark : line.color;
            //}
            //paint.setColor(lineColor);
            //bottomLinePaint.setColor(lineColor);
            //selectionPaint.setColor(lineColor);
        }
    }
}
