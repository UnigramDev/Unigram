//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.Graphics.Canvas;
using System;
using System.Numerics;
using Telegram.Charts.Data;
using Telegram.Charts.DataView;

namespace Telegram.Charts
{
    public partial class StackBarChartView : BaseChartView<StackBarChartData, StackBarViewData>
    {

        private int[] yMaxPoints;

        public StackBarChartView()
        {
            superDraw = true;
            useAlphaSignature = true;
        }

        public override StackBarViewData CreateLineViewData(ChartData.Line line)
        {
            return new StackBarViewData(line);
        }

        protected void drawChart(CanvasDrawingSession canvas)
        {
            if (chartData == null)
            {
                return;
            }

            float fullWidth = chartWidth / (pickerDelegate.pickerEnd - pickerDelegate.pickerStart);
            float offset = fullWidth * pickerDelegate.pickerStart - HORIZONTAL_PADDING;

            float p;
            float lineWidth;
            if (chartData.xPercentage.Length < 2)
            {
                p = 1f;
                lineWidth = 1f;
            }
            else
            {
                p = chartData.xPercentage[1] * fullWidth;
                lineWidth = chartData.xPercentage[1] * (fullWidth - p);
            }
            int additionalPoints = (int)(HORIZONTAL_PADDING / p) + 1;
            int localStart = Math.Max(0, startXIndex - additionalPoints - 2);
            int localEnd = Math.Min(chartData.xPercentage.Length - 1, endXIndex + additionalPoints + 2);

            for (int k = 0; k < lines.Count; k++)
            {
                LineViewData line = lines[k];
                line.linesPathBottomSize = 0;
            }

            float transitionAlpha = 1f;
            //canvas.save();
            if (transitionMode == TRANSITION_MODE_PARENT)
            {
                postTransition = true;
                selectionA = 0f;
                transitionAlpha = 1f - transitionParams.progress;

                canvas.Transform = Matrix3x2.CreateScale(
                    new Vector2(1 + 2 * transitionParams.progress, 1f),
                    new Vector2(transitionParams.pX, transitionParams.pY)
                );

            }
            else if (transitionMode == TRANSITION_MODE_CHILD)
            {

                transitionAlpha = transitionParams.progress;

                canvas.Transform = Matrix3x2.CreateScale(
                    new Vector2(transitionParams.progress, 1f),
                    new Vector2(transitionParams.pX, transitionParams.pY)
                );
            }
            else if (transitionMode == TRANSITION_MODE_ALPHA_ENTER)
            {
                transitionAlpha = transitionParams.progress;
            }

            bool selected = selectedIndex >= 0 && legendShowing;

            for (int i = localStart; i <= localEnd; i++)
            {
                float stackOffset = 0;
                if (selectedIndex == i && selected)
                {
                    continue;
                }

                for (int k = 0; k < lines.Count; k++)
                {
                    LineViewData line = lines[k];
                    if (!line.enabled && line.alpha == 0)
                    {
                        continue;
                    }

                    int[] y = line.line.y;


                    float xPoint = p / 2 + chartData.xPercentage[i] * (fullWidth - p) - offset;
                    float yPercentage = y[i] / currentMaxHeight;

                    float height = yPercentage * (MeasuredHeight - chartBottom - SIGNATURE_TEXT_HEIGHT) * line.alpha;
                    float yPoint = MeasuredHeight - chartBottom - height;

                    line.linesPath[line.linesPathBottomSize++] = xPoint;
                    line.linesPath[line.linesPathBottomSize++] = yPoint - stackOffset;

                    line.linesPath[line.linesPathBottomSize++] = xPoint;
                    line.linesPath[line.linesPathBottomSize++] = MeasuredHeight - chartBottom - stackOffset;

                    stackOffset += height;
                }
            }

            for (int k = 0; k < lines.Count; k++)
            {
                StackBarViewData line = lines[k];

                Paint paint = selected || postTransition ? line.unselectedPaint : line.paint;
                if (selected)
                {
                    line.unselectedPaint.Color = ChartExtensions.blendARGB(line.lineColor, line.blendColor, selectionA);
                }

                if (postTransition)
                {
                    line.unselectedPaint.Color = ChartExtensions.blendARGB(line.lineColor, line.blendColor, 1f);
                }

                paint.A = (byte)(255 * transitionAlpha);
                paint.StrokeWidth = lineWidth;
                paint.StrokeCap = Microsoft.Graphics.Canvas.Geometry.CanvasCapStyle.Flat;
                canvas.DrawLines(line.linesPath, 0, line.linesPathBottomSize, paint);
            }

            if (selected)
            {
                float stackOffset = 0;
                for (int k = 0; k < lines.Count; k++)
                {
                    LineViewData line = lines[k];
                    if (!line.enabled && line.alpha == 0)
                    {
                        continue;
                    }

                    int[] y = line.line.y;


                    float xPoint = p / 2 + chartData.xPercentage[selectedIndex] * (fullWidth - p) - offset;
                    float yPercentage = y[selectedIndex] / currentMaxHeight;

                    float height = yPercentage * (MeasuredHeight - chartBottom - SIGNATURE_TEXT_HEIGHT) * line.alpha;
                    float yPoint = MeasuredHeight - chartBottom - height;

                    line.paint.StrokeWidth = lineWidth;
                    line.paint.A = (byte)(255 * transitionAlpha);
                    canvas.DrawLine(xPoint, yPoint - stackOffset,
                            xPoint, MeasuredHeight - chartBottom - stackOffset, line.paint);

                    stackOffset += height;
                }
            }
            //canvas.restore();

        }

        protected override void SelectXOnChart(int x, int y)
        {
            if (chartData == null)
            {
                return;
            }

            float offset = chartFullWidth * pickerDelegate.pickerStart - HORIZONTAL_PADDING;
            float p;
            if (chartData.xPercentage.Length < 2)
            {
                p = 1f;
            }
            else
            {
                p = chartData.xPercentage[1] * chartFullWidth;
            }
            float xP = (offset + x) / (chartFullWidth - p);
            selectedCoordinate = xP;
            if (xP < 0)
            {
                selectedIndex = 0;
                selectedCoordinate = 0f;
            }
            else if (xP > 1)
            {
                selectedIndex = chartData.x.Length - 1;
                selectedCoordinate = 1f;
            }
            else
            {
                selectedIndex = chartData.FindIndex(startXIndex, endXIndex, xP);
                if (selectedIndex > endXIndex)
                {
                    selectedIndex = endXIndex;
                }

                if (selectedIndex < startXIndex)
                {
                    selectedIndex = startXIndex;
                }
            }

            legendShowing = true;
            AnimateLegend(true);
            MoveLegend(offset);
            dateSelectionListener?.OnDateSelected(GetSelectedDate());
            Invalidate();
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

                int step = (int)Math.Max(1, Math.Round(n / 200f));

                if (yMaxPoints == null || yMaxPoints.Length < nl)
                {
                    yMaxPoints = new int[nl];
                }

                for (int i = 0; i < n; i++)
                {
                    float stackOffset = 0;
                    float xPoint = chartData.xPercentage[i] * pickerWidth;

                    for (int k = 0; k < nl; k++)
                    {
                        LineViewData line = lines[k];
                        if (!line.enabled && line.alpha == 0)
                        {
                            continue;
                        }

                        int y = line.line.y[i];
                        if (y > yMaxPoints[k])
                        {
                            yMaxPoints[k] = y;
                        }
                    }

                    if (i % step == 0)
                    {
                        for (int k = 0; k < nl; k++)
                        {
                            LineViewData line = lines[k];
                            if (!line.enabled && line.alpha == 0)
                            {
                                continue;
                            }

                            float h = ANIMATE_PICKER_SIZES ? pickerMaxHeight : chartData.maxValue;
                            float yPercentage = yMaxPoints[k] / h * line.alpha;
                            float yPoint = yPercentage * pickerHeight;


                            line.linesPath[line.linesPathBottomSize++] = xPoint;
                            line.linesPath[line.linesPathBottomSize++] = pickerHeight - yPoint - stackOffset;

                            line.linesPath[line.linesPathBottomSize++] = xPoint;
                            line.linesPath[line.linesPathBottomSize++] = pickerHeight - stackOffset;

                            stackOffset += yPoint;

                            yMaxPoints[k] = 0;
                        }
                    }
                }

                float p;
                if (chartData.xPercentage.Length < 2)
                {
                    p = 1f;
                }
                else
                {
                    p = chartData.xPercentage[1] * pickerWidth;
                }

                for (int k = 0; k < nl; k++)
                {
                    LineViewData line = lines[k];
                    line.paint.StrokeWidth = p * step;
                    line.paint.A = 255;
                    canvas.DrawLines(line.linesPath, 0, line.linesPathBottomSize, line.paint);
                }
            }
        }

        public override void OnCheckChanged()
        {
            int n = chartData.lines[0].y.Length;
            int k = chartData.lines.Count;

            chartData.ySum = new int[n];
            for (int i = 0; i < n; i++)
            {
                chartData.ySum[i] = 0;
                for (int j = 0; j < k; j++)
                {
                    if (lines[j].enabled)
                    {
                        chartData.ySum[i] += chartData.lines[j].y[i];
                    }
                }
            }

            chartData.ySumSegmentTree = new SegmentTree(chartData.ySum);
            base.OnCheckChanged();
        }

        protected override void DrawSelection(CanvasDrawingSession canvas)
        {

        }

        public override int FindMaxValue(int startXIndex, int endXIndex)
        {
            return chartData.FindMax(startXIndex, endXIndex);
        }


        protected override void UpdatePickerMinMaxHeight()
        {
            if (!ANIMATE_PICKER_SIZES)
            {
                return;
            }

            int max = 0;

            int n = chartData.x.Length;
            int nl = lines.Count;
            for (int i = 0; i < n; i++)
            {
                int h = 0;
                for (int k = 0; k < nl; k++)
                {
                    StackBarViewData l = lines[k];
                    if (l.enabled)
                    {
                        h += l.line.y[i];
                    }
                }
                if (h > max)
                {
                    max = h;
                }
            }


            if (max > 0 && max != animatedToPickerMaxHeight)
            {
                animatedToPickerMaxHeight = max;
                pickerAnimator?.Cancel();

                pickerAnimator = CreateAnimator(pickerMaxHeight, animatedToPickerMaxHeight, new AnimatorUpdateListener(animation =>
                {
                    pickerMaxHeight = (float)animation.GetAnimatedValue();
                    invalidatePickerChart = true;
                    Invalidate();
                }));
                pickerAnimator.Start();
            }


        }

        protected override void InitPickerMaxHeight()
        {
            base.InitPickerMaxHeight();
            pickerMaxHeight = 0;
            int n = chartData.x.Length;
            int nl = lines.Count;
            for (int i = 0; i < n; i++)
            {
                int h = 0;
                for (int k = 0; k < nl; k++)
                {
                    StackBarViewData l = lines[k];
                    if (l.enabled)
                    {
                        h += l.line.y[i];
                    }
                }
                if (h > pickerMaxHeight)
                {
                    pickerMaxHeight = h;
                }
            }
        }

        protected override void OnDraw(CanvasDrawingSession canvas)
        {
            Tick();
            drawChart(canvas);
            DrawBottomLine(canvas);
            int tmpN = horizontalLines.Count;
            for (int tmpI = 0; tmpI < tmpN; tmpI++)
            {
                DrawHorizontalLines(canvas, horizontalLines[tmpI]);
                DrawSignaturesToHorizontalLines(canvas, horizontalLines[tmpI]);
            }
            DrawBottomSignature(canvas);
            DrawPicker(canvas);
            DrawSelection(canvas);

            base.OnDraw(canvas);
        }

        protected override float GetMinDistance()
        {
            return 0.1f;
        }
    }
}
