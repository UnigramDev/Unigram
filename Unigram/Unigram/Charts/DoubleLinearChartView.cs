using Unigram.Charts.Data;
using Unigram.Charts.DataView;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Unigram.Common;
using Windows.UI;

namespace Unigram.Charts
{
    public class DoubleLinearChartView : BaseChartView<DoubleLinearChartData, LineViewData>
    {
        protected bool drawSteps = true;

        public DoubleLinearChartView()
        {
        }

        protected override void init()
        {
            useMinHeight = true;
            base.init();
        }

        protected override void drawChart(CanvasDrawingSession canvas)
        {
            if (chartData != null)
            {
                float fullWidth = (chartWidth / (pickerDelegate.pickerEnd - pickerDelegate.pickerStart));
                float offset = fullWidth * (pickerDelegate.pickerStart) - HORIZONTAL_PADDING;

                //canvas.save();
                var transform = canvas.Transform;
                float transitionAlpha = 1f;
                if (transitionMode == TRANSITION_MODE_PARENT)
                {

                    transitionAlpha = transitionParams.progress > 0.5f ? 0 : 1f - transitionParams.progress * 2f;

                    canvas.Transform = Matrix3x2.CreateScale(
                        new Vector2(1 + 2 * transitionParams.progress, 1f),
                        new Vector2(transitionParams.pX, transitionParams.pY)
                    );

                }
                else if (transitionMode == TRANSITION_MODE_CHILD)
                {

                    transitionAlpha = transitionParams.progress < 0.3f ? 0 : transitionParams.progress;

                    //canvas.save();
                    canvas.Transform = Matrix3x2.CreateScale(
                        new Vector2(transitionParams.progress, transitionParams.progress),
                        new Vector2(transitionParams.pX, transitionParams.pY)
                    );
                }
                else if (transitionMode == TRANSITION_MODE_ALPHA_ENTER)
                {
                    transitionAlpha = transitionParams.progress;
                }

                for (int k = 0; k < lines.Count; k++)
                {
                    LineViewData line = lines[k];
                    if (!line.enabled && line.alpha == 0) continue;

                    int j = 0;

                    int[] y = line.line.y;

                    line.chartPath = new CanvasPathBuilder(canvas);
                    bool first = true;

                    float p;
                    if (chartData.xPercentage.Length < 2)
                    {
                        p = 1f;
                    }
                    else
                    {
                        p = chartData.xPercentage[1] * fullWidth;
                    }

                    int additionalPoints = (int)(HORIZONTAL_PADDING / p) + 1;
                    int localStart = Math.Max(0, startXIndex - additionalPoints);
                    int localEnd = Math.Min(chartData.xPercentage.Length - 1, endXIndex + additionalPoints);

                    for (int i = localStart; i <= localEnd; i++)
                    {
                        if (y[i] < 0) continue;
                        float xPoint = chartData.xPercentage[i] * fullWidth - offset;
                        float yPercentage = ((float)y[i] * chartData.linesK[k] - currentMinHeight) / (currentMaxHeight - currentMinHeight);
                        float padding = line.paint.StrokeWidth / 2f;
                        float yPoint = getMeasuredHeight() - chartBottom - padding - (yPercentage) * (getMeasuredHeight() - chartBottom - SIGNATURE_TEXT_HEIGHT - padding);

                        if (USE_LINES)
                        {
                            if (j == 0)
                            {
                                line.linesPath[j++] = xPoint;
                                line.linesPath[j++] = yPoint;
                            }
                            else
                            {
                                line.linesPath[j++] = xPoint;
                                line.linesPath[j++] = yPoint;
                                line.linesPath[j++] = xPoint;
                                line.linesPath[j++] = yPoint;
                            }
                        }
                        else
                        {
                            if (first)
                            {
                                first = false;

                                if (drawSteps)
                                {
                                    line.chartPath.BeginFigure(xPoint - (p / 2), yPoint);
                                    line.chartPath.AddLine(xPoint + (p / 2), yPoint);
                                }
                                else
                                {
                                    line.chartPath.BeginFigure(xPoint, yPoint);
                                }
                            }
                            else
                            {
                                if (drawSteps)
                                {
                                    line.chartPath.AddLine(xPoint - (p / 2), yPoint);
                                    line.chartPath.AddLine(xPoint + (p / 2), yPoint);
                                }
                                else
                                {
                                    line.chartPath.AddLine(xPoint, yPoint);
                                }
                            }
                        }
                    }
                    line.chartPath.EndFigure(CanvasFigureLoop.Open);

                    if (endXIndex - startXIndex > 100)
                    {
                        line.paint.StrokeCap = CanvasCapStyle.Square;
                    }
                    else
                    {
                        line.paint.StrokeCap = CanvasCapStyle.Round;
                    }
                    line.paint.A = (byte)(255 * line.alpha * transitionAlpha);
                    if (!USE_LINES) canvas.DrawGeometry(CanvasGeometry.CreatePath(line.chartPath), line.paint);
                    //else canvas.drawLines(line.linesPath, 0, j, line.paint);
                }

                //canvas.restore();
                canvas.Transform = transform;
            }

        }

        protected override void drawPickerChart(CanvasDrawingSession canvas)
        {
            int bottom = getMeasuredHeight() - PICKER_PADDING;
            int top = getMeasuredHeight() - pikerHeight - PICKER_PADDING;

            int nl = lines.Count;
            if (chartData != null)
            {
                for (int k = 0; k < nl; k++)
                {
                    LineViewData line = lines[k];
                    if (!line.enabled && line.alpha == 0) continue;

                    line.bottomLinePath = new CanvasPathBuilder(canvas);

                    int n = chartData.xPercentage.Length;
                    int j = 0;

                    float p;
                    if (chartData.xPercentage.Length < 2)
                    {
                        p = 1f;
                    }
                    else
                    {
                        p = chartData.xPercentage[1] * pickerWidth;
                    }

                    int[] y = line.line.y;

                    //line.chartPath.reset();
                    for (int i = 0; i < n; i++)
                    {
                        if (y[i] < 0) continue;

                        float xPoint = chartData.xPercentage[i] * pickerWidth;
                        float h = ANIMATE_PICKER_SIZES ? pickerMaxHeight : chartData.maxValue;

                        float yPercentage = (float)y[i] * chartData.linesK[k] / h;
                        float yPoint = (1f - yPercentage) * (bottom - top);

                        if (USE_LINES)
                        {
                            if (j == 0)
                            {
                                line.linesPathBottom[j++] = xPoint;
                                line.linesPathBottom[j++] = yPoint;
                            }
                            else
                            {
                                line.linesPathBottom[j++] = xPoint;
                                line.linesPathBottom[j++] = yPoint;
                                line.linesPathBottom[j++] = xPoint;
                                line.linesPathBottom[j++] = yPoint;
                            }
                        }
                        else
                        {
                            if (i == 0)
                            {
                                if (drawSteps)
                                {
                                    line.bottomLinePath.BeginFigure(xPoint - (p / 2), yPoint);
                                    line.bottomLinePath.AddLine(xPoint + (p / 2), yPoint);
                                }
                                else
                                {
                                    line.bottomLinePath.BeginFigure(xPoint, yPoint);
                                }
                            }
                            else
                            {
                                if (drawSteps)
                                {
                                    line.bottomLinePath.AddLine(xPoint - (p / 2), yPoint);
                                    line.bottomLinePath.AddLine(xPoint + (p / 2), yPoint);
                                }
                                else
                                {
                                    line.bottomLinePath.AddLine(xPoint, yPoint);
                                }
                            }
                        }
                    }
                    line.bottomLinePath.EndFigure(CanvasFigureLoop.Open);

                    line.linesPathBottomSize = j;


                    if (!line.enabled && line.alpha == 0) continue;
                    line.bottomLinePaint.A = (byte)(255 * line.alpha);
                    //if (USE_LINES)
                    //    canvas.drawLines(line.linesPathBottom, 0, line.linesPathBottomSize, line.bottomLinePaint);
                    //else
                    canvas.DrawGeometry(CanvasGeometry.CreatePath(line.bottomLinePath), line.bottomLinePaint);

                }
            }
        }

        protected override void drawSelection(CanvasDrawingSession canvas)
        {
            if (selectedIndex < 0 || !legendShowing) return;

            byte alpha = (byte)(chartActiveLineAlpha * selectionA);

            float fullWidth = (chartWidth / (pickerDelegate.pickerEnd - pickerDelegate.pickerStart));
            float offset = fullWidth * (pickerDelegate.pickerStart) - HORIZONTAL_PADDING;

            float xPoint = chartData.xPercentage[selectedIndex] * fullWidth - offset;


            selectedLinePaint.A = alpha;
            canvas.DrawLine(xPoint, 0, xPoint, (float)chartArea.Bottom, selectedLinePaint);

            int tmpN = lines.Count;
            for (int tmpI = 0; tmpI < tmpN; tmpI++)
            {
                LineViewData line = lines[tmpI];
                if (!line.enabled && line.alpha == 0) continue;
                float yPercentage = ((float)line.line.y[selectedIndex] * chartData.linesK[tmpI] - currentMinHeight) / (currentMaxHeight - currentMinHeight);
                float yPoint = getMeasuredHeight() - chartBottom - (yPercentage) * (getMeasuredHeight() - chartBottom - SIGNATURE_TEXT_HEIGHT);

                line.selectionPaint.A = (byte)(255 * line.alpha * selectionA);
                selectionBackgroundPaint.A = (byte)(255 * line.alpha * selectionA);

                canvas.FillCircle(xPoint, yPoint, line.selectionPaint);
                canvas.FillCircle(xPoint, yPoint, selectionBackgroundPaint);
            }
        }

        protected override void drawSignaturesToHorizontalLines(CanvasDrawingSession canvas, ChartHorizontalLinesData a)
        {
            int n = a.values.Length;
            int rightIndex = chartData.linesK[0] == 1 ? 1 : 0;
            int leftIndex = (rightIndex + 1) % 2;

            float additionalOutAlpha = 1f;
            if (n > 2)
            {
                float v = (a.values[1] - a.values[0]) / (float)(currentMaxHeight - currentMinHeight);
                if (v < 0.1)
                {
                    additionalOutAlpha = v / 0.1f;
                }
            }

            float transitionAlpha = 1f;
            if (transitionMode == TRANSITION_MODE_PARENT)
            {
                transitionAlpha = 1f - transitionParams.progress;
            }
            else if (transitionMode == TRANSITION_MODE_CHILD)
            {
                transitionAlpha = transitionParams.progress;
            }
            else if (transitionMode == TRANSITION_MODE_ALPHA_ENTER)
            {
                transitionAlpha = transitionParams.progress;
            }


            linePaint.A = (byte)(a.alpha * 0.1f * transitionAlpha);
            int chartHeight = getMeasuredHeight() - chartBottom - SIGNATURE_TEXT_HEIGHT;

            var format = new CanvasTextFormat { FontSize = signaturePaint.TextSize ?? 0 };
            var layout = new CanvasTextLayout(canvas, "0", format, 0, 0);

            int textOffset = (int)(4 + layout.DrawBounds.Bottom);
            //int textOffset = (int)(SIGNATURE_TEXT_HEIGHT - signaturePaintFormat.FontSize);
            format.Dispose();
            layout.Dispose();
            for (int i = 0; i < n; i++)
            {
                int y = (int)((getMeasuredHeight() - chartBottom) - chartHeight * ((a.values[i] - currentMinHeight) / (currentMaxHeight - currentMinHeight)));
                if (a.valuesStr != null && lines.Count > 0)
                {
                    if (a.valuesStr2 == null || lines.Count < 2)
                    {
                        signaturePaint.Color = _colors["key_statisticChartSignature"];
                        signaturePaint.A = (byte)(a.alpha * signaturePaintAlpha * transitionAlpha * additionalOutAlpha);
                    }
                    else
                    {
                        signaturePaint.Color = lines[leftIndex].lineColor;
                        signaturePaint.A = (byte)(a.alpha * lines[leftIndex].alpha * transitionAlpha * additionalOutAlpha);
                    }

                    canvas.DrawText(a.valuesStr[i], HORIZONTAL_PADDING, y - textOffset, signaturePaint);
                }
                if (a.valuesStr2 != null && lines.Count > 1)
                {
                    signaturePaint2.Color = lines[rightIndex].lineColor;
                    signaturePaint2.A = (byte)(a.alpha * lines[rightIndex].alpha * transitionAlpha * additionalOutAlpha);
                    canvas.DrawText(a.valuesStr2[i], getMeasuredWidth() - HORIZONTAL_PADDING, y - textOffset, signaturePaint2);
                }
            }
        }

        public override LineViewData createLineViewData(ChartData.Line line)
        {
            return new LineViewData(line);
        }

        public override int findMaxValue(int startXIndex, int endXIndex)
        {
            if (lines.Count < 1)
            {
                return 0;
            }
            int n = lines.Count;
            int max = 0;
            for (int i = 0; i < n; i++)
            {
                int localMax = lines[i].enabled ? (int)(chartData.lines[i].segmentTree.rMaxQ(startXIndex, endXIndex) * chartData.linesK[i]) : 0;
                if (localMax > max) max = localMax;
            }
            return max;
        }

        public override int findMinValue(int startXIndex, int endXIndex)
        {
            if (lines.Count < 1)
            {
                return 0;
            }
            int n = lines.Count;
            int min = int.MaxValue;
            for (int i = 0; i < n; i++)
            {
                int localMin = lines[i].enabled ? (int)(chartData.lines[i].segmentTree.rMinQ(startXIndex, endXIndex) * chartData.linesK[i]) : int.MaxValue;
                if (localMin < min) min = localMin;
            }
            return min;
        }

        protected override void updatePickerMinMaxHeight()
        {
            if (!ANIMATE_PICKER_SIZES) return;
            if (lines[0].enabled)
            {
                base.updatePickerMinMaxHeight();
                return;
            }

            int max = 0;
            foreach (LineViewData l in lines)
            {
                if (l.enabled && l.line.maxValue > max) max = l.line.maxValue;
            }
            if (lines.Count > 1)
            {
                max = (int)(max * chartData.linesK[1]);
            }

            if (max > 0 && max != animatedToPickerMaxHeight)
            {
                animatedToPickerMaxHeight = max;
                if (pickerAnimator != null) pickerAnimator.cancel();

                pickerAnimator = createAnimator(pickerMaxHeight, animatedToPickerMaxHeight, new AnimatorUpdateListener(animation =>
                {
                    pickerMaxHeight = (float)animation.getAnimatedValue();
                    invalidatePickerChart = true;
                    invalidate();
                }));
                pickerAnimator.start();
            }
        }

        protected override ChartHorizontalLinesData createHorizontalLinesData(int newMaxHeight, int newMinHeight)
        {
            float k;
            if (chartData.linesK.Length < 2)
            {
                k = 1;
            }
            else
            {
                int rightIndex = chartData.linesK[0] == 1 ? 1 : 0;
                k = chartData.linesK[rightIndex];
            }
            return new ChartHorizontalLinesData(newMaxHeight, newMinHeight, useMinHeight, k);
        }
    }
}
