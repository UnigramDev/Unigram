using Unigram.Charts.Data;
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace Unigram.Charts.DataView
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

            updateColors();
        }

        public virtual void updateColors()
        {
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
