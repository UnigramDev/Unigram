using Microsoft.Graphics.Canvas;
using System.Numerics;
using Unigram.Charts.Data;
using Unigram.Charts.DataView;

namespace Unigram.Charts
{
    public class BarChartView : BaseChartView<ChartData, BarViewData>
    {

        public BarChartView()
        {
            superDraw = true;
            useAlphaSignature = true;
        }

        protected override void drawChart(CanvasDrawingSession canvas)
        {
            if (chartData != null)
            {
                float fullWidth = (chartWidth / (pickerDelegate.pickerEnd - pickerDelegate.pickerStart));
                float offset = fullWidth * (pickerDelegate.pickerStart) - HORIZONTAL_PADDING;

                int start = startXIndex - 1;
                if (start < 0) start = 0;
                int end = endXIndex + 1;
                if (end > chartData.lines[0].y.Length - 1)
                    end = chartData.lines[0].y.Length - 1;

                //canvas.save();
                //canvas.clipRect(chartStart, 0, chartEnd, getMeasuredHeight() - chartBottom);
                var transform = canvas.Transform;
                var clip = canvas.CreateLayer(1, createRect(chartStart, 0, chartEnd, getMeasuredHeight() - chartBottom));

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


                for (int k = 0; k < lines.Count; k++)
                {
                    BarViewData line = lines[k];
                    if (!line.enabled && line.alpha == 0) continue;

                    float p;
                    if (chartData.xPercentage.Length < 2)
                    {
                        p = 1f;
                    }
                    else
                    {
                        p = chartData.xPercentage[1] * fullWidth;
                    }
                    int[] y = line.line.y;
                    int j = 0;

                    float selectedX = 0f;
                    float selectedY = 0f;
                    bool selected = false;
                    float a = line.alpha;
                    for (int i = start; i <= end; i++)
                    {
                        float xPoint = p / 2 + chartData.xPercentage[i] * fullWidth - offset;
                        float yPercentage = y[i] / currentMaxHeight * a;

                        float yPoint = getMeasuredHeight() - chartBottom - (yPercentage) * (getMeasuredHeight() - chartBottom - SIGNATURE_TEXT_HEIGHT);

                        if (i == selectedIndex && legendShowing)
                        {
                            selected = true;
                            selectedX = xPoint;
                            selectedY = yPoint;
                            continue;
                        }

                        line.linesPath[j++] = xPoint;
                        line.linesPath[j++] = yPoint;

                        line.linesPath[j++] = xPoint;
                        line.linesPath[j++] = getMeasuredHeight() - chartBottom;
                    }

                    Paint paint = selected || postTransition ? line.unselectedPaint : line.paint;
                    paint.StrokeWidth = p;
                    //paint.setStrokeWidth(p);


                    if (selected)
                    {
                        line.unselectedPaint.Color = Extensions.blendARGB(
                            line.line.color, line.blendColor, 1f - selectionA);
                    }

                    if (postTransition)
                    {
                        line.unselectedPaint.Color = Extensions.blendARGB(
                                line.line.color, line.blendColor, 0);
                    }

                    paint.A = (byte)(transitionAlpha * 255);
                    //canvas.drawLines(line.linesPath, 0, j, paint);
                    canvas.DrawLines(line.linesPath, 0, j, paint);

                    if (selected)
                    {
                        //line.paint.setStrokeWidth(p);
                        line.paint.StrokeWidth = p;
                        line.paint.A = (byte)(transitionAlpha * 255);
                        //canvas.drawLine(selectedX, selectedY,
                        //        selectedX, getMeasuredHeight() - chartBottom,
                        //        line.paint
                        //);
                        canvas.DrawLine(selectedX, selectedY, selectedX, getMeasuredHeight() - chartBottom, line.paint);
                        line.paint.A = 255;
                    }


                }

                //canvas.restore();
                //canvas.restore();
                clip.Dispose();
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
                    BarViewData line = lines[k];
                    if (!line.enabled && line.alpha == 0) continue;

                    //line.bottomLinePath.reset();

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

                    float a = line.alpha;

                    for (int i = 0; i < n; i++)
                    {
                        if (y[i] < 0) continue;
                        float xPoint = chartData.xPercentage[i] * pickerWidth;
                        float h = ANIMATE_PICKER_SIZES ? pickerMaxHeight : chartData.maxValue;
                        float yPercentage = (float)y[i] / h * a;
                        float yPoint = (1f - yPercentage) * (bottom - top);

                        line.linesPath[j++] = xPoint;
                        line.linesPath[j++] = yPoint;

                        line.linesPath[j++] = xPoint;
                        line.linesPath[j++] = getMeasuredHeight() - chartBottom;
                    }

                    //line.paint.setStrokeWidth(p + 2);
                    //canvas.drawLines(line.linesPath, 0, j, line.paint);
                    line.paint.StrokeWidth = p + 2;
                    canvas.DrawLines(line.linesPath, 0, j, line.paint);
                }
            }
        }

        protected override void drawSelection(CanvasDrawingSession canvas)
        {

        }

        public override BarViewData createLineViewData(ChartData.Line line)
        {
            return new BarViewData(line);
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

        protected override float getMinDistance()
        {
            return 0.1f;
        }
    }
}
