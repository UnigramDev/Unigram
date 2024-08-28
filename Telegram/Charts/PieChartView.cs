//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Numerics;
using Telegram.Charts.Data;
using Telegram.Charts.DataView;
using Telegram.Common;
using Windows.Foundation;
using Windows.UI;

namespace Telegram.Charts
{
    public partial class PieChartView : StackLinearChartView<PieChartViewData>
    {

        float[] values;
        float[] darawingValuesPercentage;
        float sum;

        bool isEmpty;
        int currentSelection = -1;

        Rect rectF = new Rect();

        //TextPaint textPaint;

        float MIN_TEXT_SIZE = 9;
        float MAX_TEXT_SIZE = 13;
        readonly string[] lookupTable = new string[101];

        //PieLegendView pieLegendView;

        float emptyDataAlpha = 1f;

        public PieChartView()
        {
            for (int i = 1; i <= 100; i++)
            {
                lookupTable[i] = i + "%";
            }

            //textPaint = new TextPaint(Paint.ANTI_ALIAS_FLAG);
            //textPaint.setTextAlign(Paint.Align.CENTER);
            //textPaint.setColor(Color.WHITE);
            //textPaint.setTypeface(Typeface.create("sans-serif-medium", Typeface.NORMAL));
            canCaptureChartSelection = true;
        }


        protected override void DrawChart(CanvasDrawingSession canvas)
        {
            if (chartData == null)
            {
                return;
            }

            int transitionAlpha = 255;

            if (canvas != null)
            {
                //canvas.save();
            }
            if (transitionMode == TRANSITION_MODE_CHILD)
            {
                transitionAlpha = (int)(transitionParams.progress * transitionParams.progress * 255);
            }

            if (isEmpty)
            {
                if (emptyDataAlpha != 0)
                {
                    emptyDataAlpha -= 0.12f;
                    if (emptyDataAlpha < 0)
                    {
                        emptyDataAlpha = 0;
                    }
                    Invalidate();
                }
            }
            else
            {
                if (emptyDataAlpha != 1f)
                {
                    emptyDataAlpha += 0.12f;
                    if (emptyDataAlpha > 1f)
                    {
                        emptyDataAlpha = 1f;
                    }
                    Invalidate();
                }
            }

            transitionAlpha = (int)(transitionAlpha * emptyDataAlpha);
            float sc = 0.4f + emptyDataAlpha * 0.6f;
            if (canvas != null)
            {
                canvas.Transform = Matrix3x2.CreateScale(
                    new Vector2(sc, sc),
                    new Vector2(chartArea.centerX(), chartArea.centerY())
                );
            }

            int radius = (int)((chartArea.Width > chartArea.Height ? chartArea.Height : chartArea.Width) * 0.45f);
            rectF = CreateRect(
                chartArea.centerX() - radius,
                chartArea.centerY() + 16 - radius,
                chartArea.centerX() + radius,
                chartArea.centerY() + 16 + radius
            );


            float a = 0f;
            float rText;

            int n = lines.Count;

            float localSum = 0f;
            for (int i = 0; i < n; i++)
            {
                float v = lines[i].drawingPart * lines[i].alpha;
                localSum += v;
            }
            if (localSum == 0)
            {
                if (canvas != null)
                {
                    //canvas.restore();
                }
                return;
            }
            for (int i = 0; i < n; i++)
            {
                if (lines[i].alpha <= 0 && !lines[i].enabled)
                {
                    continue;
                }

                lines[i].paint.A = (byte)transitionAlpha;

                float currentPercent = lines[i].drawingPart / localSum * lines[i].alpha;
                darawingValuesPercentage[i] = currentPercent;

                if (currentPercent == 0)
                {
                    continue;
                }

                if (canvas != null)
                {
                    //canvas.save();
                }

                float textAngle = -90 + a + currentPercent / 2f * 360f;

                if (lines[i].selectionA > 0f)
                {
                    float ai = INTERPOLATOR.getInterpolation(lines[i].selectionA);
                    if (canvas != null)
                    {
                        canvas.Transform = Matrix3x2.CreateTranslation(
                            MathF.Cos(MathFEx.ToRadians(textAngle)) * 8 * ai,
                            MathF.Sin(MathFEx.ToRadians(textAngle)) * 8 * ai
                        );
                    }
                }

                //lines[i].paint.setStyle(Paint.Style.FILL_AND_STROKE);
                lines[i].paint.StrokeWidth = 1;
                //lines[i].paint.setAntiAlias(!USE_LINES);

                if (canvas != null && transitionMode != TRANSITION_MODE_CHILD)
                {
                    //canvas.drawArc(
                    //        rectF,
                    //        a,
                    //        (currentPercent) * 360f,
                    //        true,
                    //        lines[i].paint);

                    var b = a + currentPercent * 360f;

                    using var builder = new CanvasPathBuilder(canvas);
                    var center = new Vector2((float)rectF.X + (float)rectF.Width / 2, (float)rectF.Y + (float)rectF.Height / 2);
                    builder.BeginFigure(center);
                    builder.AddLine(
                        new Vector2(
                            (float)(center.X + Math.Sin(a * Math.PI / 180) * (float)rectF.Width / 2),
                            (float)(center.Y - Math.Cos(a * Math.PI / 180) * (float)rectF.Height / 2)));

                    builder.AddArc(
                        new Vector2(
                            (float)(center.X + Math.Sin(b * Math.PI / 180) * (float)rectF.Width / 2),
                            (float)(center.Y - Math.Cos(b * Math.PI / 180) * (float)rectF.Height / 2)),
                        (float)rectF.Width / 2,
                        (float)rectF.Height / 2,
                        0, CanvasSweepDirection.Clockwise,
                        (b - a) >= 180.0 ? CanvasArcSize.Large : CanvasArcSize.Small);

                    builder.EndFigure(CanvasFigureLoop.Closed);
                    canvas.FillGeometry(CanvasGeometry.CreatePath(builder), lines[i].paint.Color);

                    //lines[i].paint.setStyle(Paint.Style.STROKE);

                    //canvas.restore();
                    canvas.Transform = Matrix3x2.Identity;
                }

                lines[i].paint.A = 255;
                a += currentPercent * 360f;
            }

            a = 0f;

            if (canvas != null)
            {
                var textFormat = new CanvasTextFormat();
                var textLayout = new CanvasTextLayout(canvas, "100%", textFormat, float.PositiveInfinity, float.PositiveInfinity);

                for (int i = 0; i < n; i++)
                {
                    if (lines[i].alpha <= 0 && !lines[i].enabled)
                    {
                        continue;
                    }

                    float currentPercent = lines[i].drawingPart * lines[i].alpha / localSum;

                    //canvas.save();
                    float textAngle = -90 + a + currentPercent / 2f * 360f;

                    if (lines[i].selectionA > 0f)
                    {
                        float ai = INTERPOLATOR.getInterpolation(lines[i].selectionA);
                        canvas.Transform = Matrix3x2.CreateTranslation(
                            MathF.Cos(MathFEx.ToRadians(textAngle)) * 8 * ai,
                            MathF.Sin(MathFEx.ToRadians(textAngle)) * 8 * ai
                        );
                    }

                    int percent = (int)(100f * currentPercent);
                    if (currentPercent >= 0.02f && percent > 0 && percent <= 100)
                    {
                        rText = (float)(rectF.Width * 0.42f * Math.Sqrt(1f - currentPercent));
                        //textPaint.setTextSize(MIN_TEXT_SIZE + currentPercent * MAX_TEXT_SIZE);
                        //textPaint.setAlpha((int)(transitionAlpha * lines[i].alpha));
                        //canvas.drawText(
                        //        lookupTable[percent],
                        //        (float)(rectF.centerX() + rText * Math.Cos(MathEx.ToRadians(textAngle))),
                        //        (float)(rectF.centerY() + rText * Math.Sin(MathEx.ToRadians(textAngle))) - ((textPaint.descent() + textPaint.ascent()) / 2),
                        //        textPaint);

                        var regions = textLayout.GetCharacterRegions(0, lookupTable[percent].Length);

                        canvas.DrawText(
                            lookupTable[percent],
                            rectF.centerX() + rText * MathF.Cos(MathFEx.ToRadians(textAngle)) - (float)regions[0].LayoutBounds.Width / 2,
                            rectF.centerY() + rText * MathF.Sin(MathFEx.ToRadians(textAngle)) - (float)regions[0].LayoutBounds.Height / 2 /*- ((textPaint.descent() + textPaint.ascent()) / 2)*/,
                            Color.FromArgb((byte)(transitionAlpha * lines[i].alpha), 255, 255, 255)
                        );
                    }

                    //canvas.restore();
                    canvas.Transform = Matrix3x2.Identity;

                    lines[i].paint.A = 255;
                    a += currentPercent * 360f;
                }

                //canvas.restore();
                canvas.Transform = Matrix3x2.Identity;
            }
        }

        protected override void DrawPickerChart(CanvasDrawingSession canvas)
        {
            if (chartData != null)
            {
                int n = chartData.xPercentage.Length;
                int nl = lines.Count;
                for (int k = 0; k < lines.Count; k++)
                {
                    LineViewData line = lines[k];
                    line.linesPathBottomSize = 0;
                }

                float p = 1f / chartData.xPercentage.Length * pickerWidth;

                for (int i = 0; i < n; i++)
                {
                    float stackOffset = 0;
                    float xPoint = p / 2 + chartData.xPercentage[i] * (pickerWidth - p);

                    float sum = 0;
                    int drawingLinesCount = 0;
                    bool allDisabled = true;
                    for (int k = 0; k < nl; k++)
                    {
                        LineViewData line = lines[k];
                        if (!line.enabled && line.alpha == 0)
                        {
                            continue;
                        }

                        float v = line.line.y[i] * line.alpha;
                        sum += v;
                        if (v > 0)
                        {
                            drawingLinesCount++;
                            if (line.enabled)
                            {
                                allDisabled = false;
                            }
                        }
                    }

                    for (int k = 0; k < nl; k++)
                    {
                        LineViewData line = lines[k];
                        if (!line.enabled && line.alpha == 0)
                        {
                            continue;
                        }

                        int[] y = line.line.y;

                        float yPercentage;
                        if (drawingLinesCount == 1)
                        {
                            if (y[i] == 0)
                            {
                                yPercentage = 0;
                            }
                            else
                            {
                                yPercentage = line.alpha;
                            }
                        }
                        else
                        {
                            if (sum == 0)
                            {
                                yPercentage = 0;
                            }
                            else if (allDisabled)
                            {
                                yPercentage = y[i] / sum * line.alpha * line.alpha;
                            }
                            else
                            {
                                yPercentage = y[i] / sum * line.alpha;
                            }
                        }

                        float yPoint = yPercentage * pickerHeight;


                        line.linesPath[line.linesPathBottomSize++] = xPoint;
                        line.linesPath[line.linesPathBottomSize++] = pickerHeight - yPoint - stackOffset;

                        line.linesPath[line.linesPathBottomSize++] = xPoint;
                        line.linesPath[line.linesPathBottomSize++] = pickerHeight - stackOffset;

                        stackOffset += yPoint;
                    }
                }

                for (int k = 0; k < nl; k++)
                {
                    LineViewData line = lines[k];
                    line.paint.StrokeWidth = p;
                    line.paint.A = 255;
                    //line.paint.setAntiAlias(false);
                    canvas.DrawLines(line.linesPath, 0, line.linesPathBottomSize, line.paint);
                }
            }
        }

        protected override void DrawBottomLine(CanvasDrawingSession canvas)
        {

        }

        protected override void DrawSelection(CanvasDrawingSession canvas)
        {

        }

        protected override void DrawHorizontalLines(CanvasDrawingSession canvas, ChartHorizontalLinesData a)
        {

        }

        protected override void DrawSignaturesToHorizontalLines(CanvasDrawingSession canvas, ChartHorizontalLinesData a)
        {

        }

        protected override void DrawBottomSignature(CanvasDrawingSession canvas)
        {

        }

        public override void SetData(StackLinearChartData chartData)
        {
            base.SetData(chartData);

            if (chartData != null)
            {
                values = new float[chartData.lines.Count];
                darawingValuesPercentage = new float[chartData.lines.Count];
                OnPickerDataChanged(false, true, false);
            }
        }

        public override PieChartViewData CreateLineViewData(ChartData.Line line)
        {
            return new PieChartViewData(line);
        }


        protected override void SelectXOnChart(int x, int y)
        {
            if (chartData == null || isEmpty)
            {
                return;
            }

            double theta = Math.Atan2(chartArea.centerY() + 16 - y, chartArea.centerX() - x);

            float a = (float)(MathEx.ToDegrees(theta) - 90);
            if (a < 0)
            {
                a += 360f;
            }

            a /= 360;

            float p = 0;
            int newSelection = -1;

            float selectionStartA = 0f;
            float selectionEndA = 0f;
            for (int i = 0; i < lines.Count; i++)
            {
                if (!lines[i].enabled && lines[i].alpha == 0)
                {
                    continue;
                }
                if (a > p && a < p + darawingValuesPercentage[i])
                {
                    newSelection = i;
                    selectionStartA = p;
                    selectionEndA = p + darawingValuesPercentage[i];
                    break;
                }
                p += darawingValuesPercentage[i];
            }
            if (currentSelection != newSelection && newSelection >= 0)
            {
                currentSelection = newSelection;
                Invalidate();
                //pieLegendView.setVisibility(Visibility.Visible);
                LineViewData l = lines[newSelection];

                //pieLegendView.setData(l.line.name, (int)values[currentSelection], l.lineColor);

                float r = (float)rectF.Width / 2;
                int xl = (int)Math.Min(
                    rectF.centerX() + r * Math.Cos(MathEx.ToRadians(selectionEndA * 360f - 90f)),
                    rectF.centerX() + r * Math.Cos(MathEx.ToRadians(selectionStartA * 360f - 90f))
                );

                if (xl < 0)
                {
                }

                int yl = (int)Math.Min(
                    rectF.centerY() + r * Math.Sin(MathEx.ToRadians(selectionStartA * 360f - 90f)),
                    rectF.centerY() + r * Math.Sin(MathEx.ToRadians(selectionEndA * 360f - 90f))
                );

                yl = Math.Min(rectF.centerY(), yl);

                // if (yl < 0) yl = 0;

                //pieLegendView.setTranslationX(xl);
                //pieLegendView.setTranslationY(yl);

                //bool v = false;
                //if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O_MR1)
                //{
                //    v = performHapticFeedback(HapticFeedbackConstants.TEXT_HANDLE_MOVE, HapticFeedbackConstants.FLAG_IGNORE_GLOBAL_SETTING);
                //}
                //if (!v)
                //{
                //    performHapticFeedback(HapticFeedbackConstants.KEYBOARD_TAP, HapticFeedbackConstants.FLAG_IGNORE_GLOBAL_SETTING);
                //}

            }
            MoveLegend();
        }

        protected override void OnDraw(CanvasDrawingSession canvas)
        {
            if (chartData != null)
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    if (i == currentSelection)
                    {
                        if (lines[i].selectionA < 1f)
                        {
                            lines[i].selectionA += 0.1f;
                            if (lines[i].selectionA > 1f)
                            {
                                lines[i].selectionA = 1f;
                            }

                            Invalidate();
                        }
                    }
                    else
                    {
                        if (lines[i].selectionA > 0)
                        {
                            lines[i].selectionA -= 0.1f;
                            if (lines[i].selectionA < 0)
                            {
                                lines[i].selectionA = 0;
                            }

                            Invalidate();
                        }
                    }
                }
            }
            base.OnDraw(canvas);
        }

        protected override void OnActionUp()
        {
            currentSelection = -1;
            //pieLegendView.setVisibility(Visibility.Collapsed);
            Invalidate();
        }

        int oldW = 0;

        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            base.OnMeasure(widthMeasureSpec, heightMeasureSpec);
            if (MeasuredWidth != oldW)
            {
                oldW = MeasuredWidth;
                int r = (int)((chartArea.Width > chartArea.Height ? chartArea.Height : chartArea.Width) * 0.45f);
                MIN_TEXT_SIZE = r / 13;
                MAX_TEXT_SIZE = r / 7;
            }
        }

        public override void UpdatePicker(ChartData chartData, long d)
        {
            int n = chartData.x.Length;
            long startOfDay = d - d % 86400000L;
            int startIndex = 0;

            for (int i = 0; i < n; i++)
            {
                if (startOfDay >= chartData.x[i])
                {
                    startIndex = i;
                }
            }

            float p;
            if (chartData.xPercentage.Length < 2)
            {
                p = 0.5f;
            }
            else
            {
                p = 1f / chartData.x.Length;
            }

            if (startIndex == 0)
            {
                pickerDelegate.pickerStart = 0;
                pickerDelegate.pickerEnd = p;
                return;
            }

            if (startIndex >= chartData.x.Length - 1)
            {
                pickerDelegate.pickerStart = 1f - p;
                pickerDelegate.pickerEnd = 1f;
                return;
            }

            pickerDelegate.pickerStart = p * startIndex;
            pickerDelegate.pickerEnd = pickerDelegate.pickerStart + p;
            if (pickerDelegate.pickerEnd > 1f)
            {
                pickerDelegate.pickerEnd = 1f;
            }

            OnPickerDataChanged(true, true, false);
        }

        //protected override LegendSignatureView createLegendView()
        //{
        //    return pieLegendView = new PieLegendView();
        //}

        int lastStartIndex = -1;
        int lastEndIndex = -1;

        public override void OnPickerDataChanged(bool animated, bool force, bool useAnimator)
        {
            base.OnPickerDataChanged(animated, force, useAnimator);
            if (chartData == null || chartData.xPercentage == null)
            {
                return;
            }
            float startPercentage = pickerDelegate.pickerStart;
            float endPercentage = pickerDelegate.pickerEnd;
            updateCharValues(startPercentage, endPercentage, force);
        }

        private void updateCharValues(float startPercentage, float endPercentage, bool force)
        {
            if (values == null)
            {
                return;
            }
            int n = chartData.xPercentage.Length;
            int nl = lines.Count;


            int startIndex = -1;
            int endIndex = -1;
            for (int j = 0; j < n; j++)
            {
                if (chartData.xPercentage[j] >= startPercentage && startIndex == -1)
                {
                    startIndex = j;
                }
                if (chartData.xPercentage[j] <= endPercentage)
                {
                    endIndex = j;
                }
            }
            if (endIndex < startIndex)
            {
                startIndex = endIndex;
            }


            if (!force && lastEndIndex == endIndex && lastStartIndex == startIndex)
            {
                return;
            }
            lastEndIndex = endIndex;
            lastStartIndex = startIndex;

            isEmpty = true;
            sum = 0;
            for (int i = 0; i < nl; i++)
            {
                values[i] = 0;
            }

            for (int j = startIndex; j <= endIndex; j++)
            {
                for (int i = 0; i < nl; i++)
                {
                    values[i] += chartData.lines[i].y[j];
                    sum += chartData.lines[i].y[j];
                    if (isEmpty && lines[i].enabled && chartData.lines[i].y[j] > 0)
                    {
                        isEmpty = false;
                    }
                }
            }
            if (!force)
            {
                for (int i = 0; i < nl; i++)
                {
                    PieChartViewData line = lines[i];
                    line.animator?.Cancel();

                    float animateTo;
                    if (sum == 0)
                    {
                        animateTo = 0;
                    }
                    else
                    {
                        animateTo = values[i] / sum;
                    }
                    ValueAnimator animator = CreateAnimator(line.drawingPart, animateTo, new AnimatorUpdateListener(animation =>
                    {
                        line.drawingPart = (float)animation.GetAnimatedValue();
                        Invalidate();
                    }));
                    line.animator = animator;
                    animator.Start();
                }
            }
            else
            {
                for (int i = 0; i < nl; i++)
                {
                    if (sum == 0)
                    {
                        lines[i].drawingPart = 0;
                    }
                    else
                    {
                        lines[i].drawingPart = values[i] / sum;
                    }
                }
            }
        }

        public override void OnPickerJumpTo(float start, float end, bool force)
        {
            if (chartData == null)
            {
                return;
            }

            if (force)
            {
                updateCharValues(start, end, false);
            }
            else
            {
                UpdateIndexes();
                Invalidate();
            }
        }

        public override void FillTransitionParams(TransitionParams param)
        {
            DrawChart(null);

            float p = 0;
            for (int i = 0; i < darawingValuesPercentage.Length; i++)
            {
                p += darawingValuesPercentage[i];
                param.angle[i] = p * 360 - 180;
            }
        }
    }
}
