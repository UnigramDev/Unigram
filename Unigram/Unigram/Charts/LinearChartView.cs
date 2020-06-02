using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Numerics;
using Unigram.Charts.Data;
using Unigram.Charts.DataView;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Charts
{
    public class LinearChartView : BaseChartView<ChartData, LineViewData>
    {
        protected bool drawSteps = false;

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


                for (int k = 0; k < lines.Count; k++)
                {
                    LineViewData line = lines[k];
                    if (!line.enabled && line.alpha == 0) continue;

                    int j = 0;

                    float p;
                    if (chartData.xPercentage.Length < 2)
                    {
                        p = 0f;
                    }
                    else
                    {
                        p = chartData.xPercentage[1] * fullWidth;
                    }
                    int[] y = line.line.y;
                    int additionalPoints = (int)(HORIZONTAL_PADDING / p) + 1;

                    line.chartPath = new CanvasPathBuilder(canvas);
                    bool first = true;

                    int localStart = Math.Max(0, startXIndex - additionalPoints);
                    int localEnd = Math.Min(chartData.xPercentage.Length - 1, endXIndex + additionalPoints);
                    for (int i = localStart; i <= localEnd; i++)
                    {
                        if (y[i] < 0) continue;
                        float xPoint = chartData.xPercentage[i] * fullWidth - offset;
                        float yPercentage = ((float)y[i] - currentMinHeight) / (currentMaxHeight - currentMinHeight);
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
                                //line.chartPath.moveTo(xPoint, yPoint);
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
                                //line.chartPath.lineTo(xPoint, yPoint);
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

                    //canvas.save();
                    float transitionAlpha = 1f;
                    if (transitionMode == TRANSITION_MODE_PARENT)
                    {
                        transitionAlpha = transitionParams.progress > 0.5f ? 0 : 1f - transitionParams.progress * 2f;
                        //canvas.scale(
                        //        1 + 2 * transitionParams.progress, 1f,
                        //        transitionParams.pX, transitionParams.pY
                        //);
                        canvas.Transform = Matrix3x2.CreateScale(
                            new Vector2(1 + 2 * transitionParams.progress, 1f),
                            new Vector2(transitionParams.pX, transitionParams.pY)
                        );
                    }
                    else if (transitionMode == TRANSITION_MODE_CHILD)
                    {
                        transitionAlpha = transitionParams.progress < 0.3f ? 0 : transitionParams.progress;
                        //canvas.save();
                        //canvas.scale(
                        //        transitionParams.progress, transitionParams.needScaleY ? transitionParams.progress : 1f,
                        //        transitionParams.pX, transitionParams.pY
                        //);
                    }
                    else if (transitionMode == TRANSITION_MODE_ALPHA_ENTER)
                    {
                        transitionAlpha = transitionParams.progress;
                    }
                    line.paint.A = (byte)(255 * line.alpha * transitionAlpha);
                    if (endXIndex - startXIndex > 100)
                    {
                        line.paint.StrokeCap = CanvasCapStyle.Square;
                    }
                    else
                    {
                        line.paint.StrokeCap = CanvasCapStyle.Round;
                    }
                    if (!USE_LINES) canvas.DrawGeometry(CanvasGeometry.CreatePath(line.chartPath), line.paint);
                    //else canvas.DrawLines(line.linesPath, 0, j, line.paint);

                    //canvas.restore();
                }
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
                        p = 0f;
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
                        float hMin = ANIMATE_PICKER_SIZES ? pickerMinHeight : chartData.minValue;
                        float yPercentage = (y[i] - hMin) / (h - hMin);
                        float yPoint = (1f - yPercentage) * pikerHeight;

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
                                    line.bottomLinePath.BeginFigure(new Vector2(xPoint - (p / 2), yPoint));
                                    line.bottomLinePath.AddLine(new Vector2(xPoint + (p / 2), yPoint));
                                }
                                else
                                {
                                    line.bottomLinePath.BeginFigure(new Vector2(xPoint, yPoint));
                                }
                            }
                            else
                            {
                                if (drawSteps)
                                {
                                    line.bottomLinePath.AddLine(new Vector2(xPoint - (p / 2), yPoint));
                                    line.bottomLinePath.AddLine(new Vector2(xPoint + (p / 2), yPoint));
                                }
                                else
                                {
                                    line.bottomLinePath.AddLine(new Vector2(xPoint, yPoint));
                                }
                            }
                        }
                    }
                    line.bottomLinePath.EndFigure(CanvasFigureLoop.Open);

                    line.linesPathBottomSize = j;

                    if (!line.enabled && line.alpha == 0) continue;
                    line.bottomLinePaint.A = (byte)(255 * line.alpha);
                    //if (USE_LINES)
                    //    canvas.DrawLines(line.linesPathBottom, 0, line.linesPathBottomSize, line.bottomLinePaint);
                    //else
                    canvas.DrawGeometry(CanvasGeometry.CreatePath(line.bottomLinePath), line.bottomLinePaint);
                }
            }
        }

        public override LineViewData createLineViewData(ChartData.Line line)
        {
            return new LineViewData(line);
        }
    }
}
