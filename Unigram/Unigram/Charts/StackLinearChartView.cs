using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Numerics;
using Unigram.Charts.Data;
using Unigram.Charts.DataView;
using Windows.Foundation;

namespace Unigram.Charts
{
    public class StackLinearChartView : StackLinearChartView<StackLinearViewData>
    {

    }

    public abstract class StackLinearChartView<T> : BaseChartView<StackLinearChartData, T> where T : StackLinearViewData
    {
        public StackLinearChartView()
        {
            superDraw = true;
            useAlphaSignature = true;
            drawPointOnSelection = false;
        }

        public override T createLineViewData(ChartData.Line line)
        {
            return (T)new StackLinearViewData(line);
        }

        CanvasPathBuilder ovalPath;
        bool[] skipPoints;

        protected override void drawChart(CanvasDrawingSession canvas)
        {
            if (chartData != null)
            {
                float fullWidth = (chartWidth / (pickerDelegate.pickerEnd - pickerDelegate.pickerStart));
                float offset = fullWidth * (pickerDelegate.pickerStart) - HORIZONTAL_PADDING;

                for (int k = 0; k < lines.Count; k++)
                {
                    lines[k].chartPath = null;
                    lines[k].chartPathPicker = null;
                }

                //canvas.save();
                if (skipPoints == null || skipPoints.Length < chartData.lines.Count)
                {
                    skipPoints = new bool[chartData.lines.Count];
                }

                int transitionAlpha = 255;
                if (transitionMode == TRANSITION_MODE_PARENT)
                {

                    transitionAlpha = (int)((1f - transitionParams.progress) * 255);
                    ovalPath = new CanvasPathBuilder(canvas);

                    int radiusStart = (int)(chartArea.Width > chartArea.Height ? chartArea.Width : chartArea.Height);
                    int radiusEnd = (int)((chartArea.Width > chartArea.Height ? chartArea.Height : chartArea.Width) / 2f);
                    float radius = radiusEnd + ((radiusStart - radiusEnd) / 2) * (1 - transitionParams.progress);

                    radius *= 1f - transitionParams.progress;
                    Rect rectF = createRect(
                        chartArea.centerX() - radius,
                        chartArea.centerY() - radius,
                        chartArea.centerX() + radius,
                        chartArea.centerY() + radius
                    );
                    //ovalPath.addRoundRect(
                    //        rectF, radius, radius, Path.Direction.CW
                    //);
                    //canvas.clipPath(ovalPath);
                }
                else if (transitionMode == TRANSITION_MODE_ALPHA_ENTER)
                {
                    transitionAlpha = (int)(transitionParams.progress * 255);
                }

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
                int localStart = Math.Max(0, startXIndex - additionalPoints - 1);
                int localEnd = Math.Min(chartData.xPercentage.Length - 1, endXIndex + additionalPoints + 1);

                float startXPoint = 0;
                float endXPoint = 0;
                for (int i = localStart; i <= localEnd; i++)
                {
                    float stackOffset = 0;
                    float sum = 0;
                    float xPoint = chartData.xPercentage[i] * fullWidth - offset;

                    int drawingLinesCount = 0;
                    for (int k = 0; k < lines.Count; k++)
                    {
                        LineViewData line = lines[k];
                        if (!line.enabled && line.alpha == 0) continue;
                        if (line.line.y[i] > 0)
                        {
                            sum += line.line.y[i] * line.alpha;
                            drawingLinesCount++;
                        }
                    }

                    for (int k = 0; k < lines.Count; k++)
                    {
                        LineViewData line = lines[k];
                        if (!line.enabled && line.alpha == 0) continue;
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
                            else
                            {
                                yPercentage = y[i] * line.alpha / sum;
                            }
                        }


                        float height = (yPercentage) * (getMeasuredHeight() - chartBottom - SIGNATURE_TEXT_HEIGHT);
                        float yPoint = getMeasuredHeight() - chartBottom - height - stackOffset;

                        if (i == localStart)
                        {
                            line.chartPath = new CanvasPathBuilder(canvas);
                            line.chartPath.BeginFigure(new Vector2(0, getMeasuredHeight()));
                            startXPoint = xPoint;
                            skipPoints[k] = false;
                        }

                        if (yPercentage == 0 && (i > 0 && y[i - 1] == 0) && (i < localEnd && y[i + 1] == 0))
                        {
                            if (!skipPoints[k])
                            {
                                line.chartPath.AddLine(new Vector2(xPoint, getMeasuredHeight() - chartBottom));
                            }
                            skipPoints[k] = true;
                        }
                        else
                        {
                            if (skipPoints[k])
                            {
                                line.chartPath.AddLine(new Vector2(xPoint, getMeasuredHeight() - chartBottom));
                            }
                            line.chartPath.AddLine(new Vector2(xPoint, yPoint));
                            skipPoints[k] = false;
                        }

                        if (i == localEnd)
                        {
                            line.chartPath.AddLine(new Vector2(getMeasuredWidth(), getMeasuredHeight()));
                            endXPoint = xPoint;
                        }

                        stackOffset += height;
                    }
                }


                //canvas.save();
                //canvas.clipRect(startXPoint, SIGNATURE_TEXT_HEIGHT, endXPoint, getMeasuredHeight() - chartBottom);
                var clip = canvas.CreateLayer(1, createRect(startXPoint, SIGNATURE_TEXT_HEIGHT, endXPoint, getMeasuredHeight() - chartBottom));
                for (int k = lines.Count - 1; k >= 0; k--)
                {
                    LineViewData line = lines[k];
                    line.paint.A = (byte)transitionAlpha;

                    if (line.chartPath != null)
                    {
                        line.chartPath.EndFigure(CanvasFigureLoop.Open);
                        canvas.FillGeometry(CanvasGeometry.CreatePath(line.chartPath), line.paint.Color);
                    }

                    line.paint.A = 255;
                }
                //canvas.restore();
                clip.Dispose();
                //canvas.restore();
            }
        }

        protected override void drawPickerChart(CanvasDrawingSession canvas)
        {
            if (chartData != null)
            {
                int nl = lines.Count;
                for (int k = 0; k < nl; k++)
                {
                    lines[k].chartPathPicker = null;
                }

                int n = chartData.simplifiedSize;

                if (skipPoints == null || skipPoints.Length < chartData.lines.Count)
                {
                    skipPoints = new bool[chartData.lines.Count];
                }

                for (int i = 0; i < n; i++)
                {
                    float stackOffset = 0;
                    float sum = 0;


                    int drawingLinesCount = 0;
                    for (int k = 0; k < lines.Count; k++)
                    {
                        LineViewData line = lines[k];
                        if (!line.enabled && line.alpha == 0) continue;
                        if (chartData.simplifiedY[k][i] > 0)
                        {
                            sum += chartData.simplifiedY[k][i] * line.alpha;
                            drawingLinesCount++;
                        }
                    }

                    float xPoint = i / (float)(n - 1) * pickerWidth;

                    for (int k = 0; k < lines.Count; k++)
                    {
                        LineViewData line = lines[k];
                        if (!line.enabled && line.alpha == 0) continue;
                        float yPercentage;

                        if (drawingLinesCount == 1)
                        {
                            if (chartData.simplifiedY[k][i] == 0)
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
                            else
                            {
                                yPercentage = (chartData.simplifiedY[k][i] * line.alpha) / sum;
                            }
                        }

                        float height = (yPercentage) * (pikerHeight);
                        float yPoint = pikerHeight - height - stackOffset;

                        if (i == 0)
                        {
                            line.chartPathPicker = new CanvasPathBuilder(canvas);
                            line.chartPathPicker.BeginFigure(new Vector2(0, pikerHeight));
                            skipPoints[k] = false;
                        }

                        if (chartData.simplifiedY[k][i] == 0 && (i > 0 && chartData.simplifiedY[k][i - 1] == 0) && (i < n - 1 && chartData.simplifiedY[k][i + 1] == 0))
                        {
                            if (!skipPoints[k])
                            {
                                line.chartPathPicker.AddLine(new Vector2(xPoint, pikerHeight));
                            }
                            skipPoints[k] = true;
                        }
                        else
                        {
                            if (skipPoints[k])
                            {
                                line.chartPathPicker.AddLine(new Vector2(xPoint, pikerHeight));
                            }
                            line.chartPathPicker.AddLine(new Vector2(xPoint, yPoint));
                            skipPoints[k] = false;
                        }

                        if (i == n - 1)
                        {
                            line.chartPathPicker.AddLine(new Vector2(pickerWidth, pikerHeight));
                        }


                        stackOffset += height;

                    }
                }

                for (int k = lines.Count - 1; k >= 0; k--)
                {
                    LineViewData line = lines[k];

                    if (line.chartPathPicker != null)
                    {
                        line.chartPathPicker.EndFigure(CanvasFigureLoop.Open);
                        canvas.FillGeometry(CanvasGeometry.CreatePath(line.chartPathPicker), line.paint.Color);
                    }
                }
            }
        }

        protected override void onDraw(CanvasDrawingSession canvas)
        {
            tick();
            drawChart(canvas);
            drawBottomLine(canvas);
            int tmpN = horizontalLines.Count;
            for (int tmpI = 0; tmpI < tmpN; tmpI++)
            {
                drawHorizontalLines(canvas, horizontalLines[tmpI]);
                drawSignaturesToHorizontalLines(canvas, horizontalLines[tmpI]);
            }
            drawBottomSignature(canvas);
            drawPicker(canvas);
            drawSelection(canvas);

            base.onDraw(canvas);
        }

        public override int findMaxValue(int startXIndex, int endXIndex)
        {
            return 100;
        }

        protected override float getMinDistance()
        {
            return 0.1f;
        }

    }
}
