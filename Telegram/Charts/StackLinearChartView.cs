//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Numerics;
using Telegram.Charts.Data;
using Telegram.Charts.DataView;
using Telegram.Common;
using Windows.Foundation;

namespace Telegram.Charts
{
    public partial class StackLinearChartView : StackLinearChartView<StackLinearViewData>
    {

    }

    public abstract class StackLinearChartView<T> : BaseChartView<StackLinearChartData, T> where T : StackLinearViewData
    {
        private Vector2 mapPoints = new Vector2();

        public StackLinearChartView()
        {
            superDraw = true;
            useAlphaSignature = true;
            drawPointOnSelection = false;
        }

        public override T CreateLineViewData(ChartData.Line line)
        {
            return (T)new StackLinearViewData(line);
        }

        CanvasActiveLayer ovalPath;
        bool[] skipPoints;
        float[] startFromY;

        protected override void DrawChart(CanvasDrawingSession canvas)
        {
            if (chartData != null)
            {
                float fullWidth = chartWidth / (pickerDelegate.pickerEnd - pickerDelegate.pickerStart);
                float offset = fullWidth * pickerDelegate.pickerStart - HORIZONTAL_PADDING;

                float cX = chartArea.centerX();
                float cY = chartArea.centerY() + 16;

                for (int k = 0; k < lines.Count; k++)
                {
                    lines[k].chartPath = null;
                    lines[k].chartPathPicker = null;
                }

                //canvas.save();
                if (skipPoints == null || skipPoints.Length < chartData.lines.Count)
                {
                    skipPoints = new bool[chartData.lines.Count];
                    startFromY = new float[chartData.lines.Count];
                }

                int transitionAlpha = 255;
                float transitionProgressHalf = 0;
                if (transitionMode == TRANSITION_MODE_PARENT)
                {
                    transitionProgressHalf = transitionParams.progress / 0.6f;
                    if (transitionProgressHalf > 1f)
                    {
                        transitionProgressHalf = 1f;
                    }
                    // transitionAlpha = (int) ((1f - transitionParams.progress) * 255);
                    //ovalPath.reset();

                    float radiusStart = (float)(chartArea.Width > chartArea.Height ? chartArea.Width : chartArea.Height);
                    float radiusEnd = (float)(chartArea.Width > chartArea.Height ? chartArea.Height : chartArea.Width) * 0.45f;
                    float radius = radiusEnd + (radiusStart - radiusEnd) / 2 * (1 - transitionParams.progress);

                    Rect rectF = CreateRect(
                        cX - radius,
                        cY - radius,
                        cX + radius,
                        cY + radius
                    );
                    ovalPath = canvas.CreateLayer(1, CanvasGeometry.CreateRoundedRectangle(canvas, rectF, radius, radius));
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
                    int lastEnabled = 0;

                    int drawingLinesCount = 0;
                    for (int k = 0; k < lines.Count; k++)
                    {
                        LineViewData line = lines[k];
                        if (!line.enabled && line.alpha == 0)
                        {
                            continue;
                        }

                        if (line.line.y[i] > 0)
                        {
                            sum += line.line.y[i] * line.alpha;
                            drawingLinesCount++;
                        }
                        lastEnabled = k;
                    }

                    for (int k = 0; k < lines.Count; k++)
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
                            else
                            {
                                yPercentage = y[i] * line.alpha / sum;
                            }
                        }

                        float xPoint = chartData.xPercentage[i] * fullWidth - offset;
                        float nextXPoint;
                        if (i == localEnd)
                        {
                            nextXPoint = MeasuredWidth;
                        }
                        else
                        {
                            nextXPoint = chartData.xPercentage[i + 1] * fullWidth - offset;
                        }

                        if (yPercentage == 0 && k == lastEnabled)
                        {
                        }
                        float height = yPercentage * (MeasuredHeight - chartBottom - SIGNATURE_TEXT_HEIGHT);
                        float yPoint = MeasuredHeight - chartBottom - height - stackOffset;
                        startFromY[k] = yPoint;

                        float angle = 0;
                        float yPointZero = MeasuredHeight - chartBottom;
                        float xPointZero = xPoint;
                        if (i == localEnd)
                        {
                            endXPoint = xPoint;
                        }
                        else if (i == localStart)
                        {
                            startXPoint = xPoint;
                        }

                        float dX;
                        float dY;
                        float x1;
                        float y1;
                        if (transitionMode == TRANSITION_MODE_PARENT && k != lastEnabled)
                        {
                            if (xPoint < cX)
                            {
                                x1 = transitionParams.startX[k];
                                y1 = transitionParams.startY[k];
                            }
                            else
                            {
                                x1 = transitionParams.endX[k];
                                y1 = transitionParams.endY[k];
                            }

                            dX = cX - x1;
                            dY = cY - y1;
                            float yTo = dY * (xPoint - x1) / dX + y1;

                            yPoint = yPoint * (1f - transitionProgressHalf) + yTo * transitionProgressHalf;
                            yPointZero = yPointZero * (1f - transitionProgressHalf) + yTo * transitionProgressHalf;

                            float angleK = dY / dX;
                            if (angleK > 0)
                            {
                                angle = (float)MathEx.ToDegrees(-Math.Atan(angleK));
                            }
                            else
                            {
                                angle = (float)MathEx.ToDegrees(Math.Atan(Math.Abs(angleK)));
                            }
                            angle -= 90;

                            if (xPoint >= cX)
                            {
                                mapPoints.X = xPoint;
                                mapPoints.Y = yPoint;
                                mapPoints = PostRotate(transitionParams.progress * angle, cX, cY, mapPoints);

                                xPoint = mapPoints.X;
                                yPoint = mapPoints.Y;
                                if (xPoint < cX)
                                {
                                    xPoint = cX;
                                }

                                mapPoints.X = xPointZero;
                                mapPoints.Y = yPointZero;
                                mapPoints = PostRotate(transitionParams.progress * angle, cX, cY, mapPoints);
                                yPointZero = mapPoints.Y;
                                if (xPointZero < cX)
                                {
                                    xPointZero = cX;
                                }
                            }
                            else
                            {
                                if (nextXPoint >= cX)
                                {
                                    xPointZero = xPoint = xPoint * (1f - transitionProgressHalf) + cX * transitionProgressHalf;
                                    yPointZero = yPoint = yPoint * (1f - transitionProgressHalf) + cY * transitionProgressHalf;
                                }
                                else
                                {
                                    mapPoints.X = xPoint;
                                    mapPoints.Y = yPoint;
                                    mapPoints = PostRotate(transitionParams.progress * angle + transitionParams.progress * transitionParams.angle[k], cX, cY, mapPoints);
                                    xPoint = mapPoints.X;
                                    yPoint = mapPoints.Y;

                                    if (nextXPoint >= cX)
                                    {
                                        mapPoints.X = xPointZero * (1f - transitionParams.progress) + cX * transitionParams.progress;
                                    }
                                    else
                                    {
                                        mapPoints.X = xPointZero;
                                    }
                                    mapPoints.Y = yPointZero;
                                    mapPoints = PostRotate(transitionParams.progress * angle + transitionParams.progress * transitionParams.angle[k], cX, cY, mapPoints);

                                    xPointZero = mapPoints.X;
                                    yPointZero = mapPoints.Y;
                                }
                            }
                        }

                        if (i == localStart)
                        {
                            float localX = 0;
                            float localY = MeasuredHeight;
                            if (transitionMode == TRANSITION_MODE_PARENT && k != lastEnabled)
                            {
                                mapPoints.X = localX - cX;
                                mapPoints.Y = localY;
                                mapPoints = PostRotate(transitionParams.progress * angle + transitionParams.progress * transitionParams.angle[k], cX, cY, mapPoints);
                                localX = mapPoints.X;
                                localY = mapPoints.Y;
                            }
                            line.chartPath = new CanvasPathBuilder(canvas);
                            line.chartPath.BeginFigure(localX, localY);
                            skipPoints[k] = false;
                        }

                        float transitionProgress = transitionParams == null ? 0f : transitionParams.progress;
                        if (yPercentage == 0 && i > 0 && y[i - 1] == 0 && i < localEnd && y[i + 1] == 0 && transitionMode != TRANSITION_MODE_PARENT)
                        {
                            if (!skipPoints[k])
                            {
                                if (k == lastEnabled)
                                {
                                    line.chartPath.AddLine(xPointZero, yPointZero * (1f - transitionProgress));
                                }
                                else
                                {
                                    line.chartPath.AddLine(xPointZero, yPointZero);
                                }
                            }
                            skipPoints[k] = true;
                        }
                        else
                        {
                            if (skipPoints[k])
                            {
                                if (k == lastEnabled)
                                {
                                    line.chartPath.AddLine(xPointZero, yPointZero * (1f - transitionProgress));
                                }
                                else
                                {
                                    line.chartPath.AddLine(xPointZero, yPointZero);
                                }
                            }
                            if (k == lastEnabled)
                            {
                                line.chartPath.AddLine(xPoint, yPoint * (1f - transitionProgress));
                            }
                            else
                            {
                                line.chartPath.AddLine(xPoint, yPoint);
                            }
                            skipPoints[k] = false;
                        }

                        if (i == localEnd)
                        {
                            float localX = MeasuredWidth;
                            float localY = MeasuredHeight;
                            if (transitionMode == TRANSITION_MODE_PARENT && k != lastEnabled)
                            {
                                mapPoints.X = localX + cX;
                                mapPoints.Y = localY;
                                mapPoints = PostRotate(transitionParams.progress * transitionParams.angle[k], cX, cY, mapPoints);
                                localX = mapPoints.X;
                                localY = mapPoints.Y;
                            }
                            else
                            {
                                line.chartPath.AddLine(localX, localY);
                            }

                            if (transitionMode == TRANSITION_MODE_PARENT && k != lastEnabled)
                            {

                                x1 = transitionParams.startX[k];
                                y1 = transitionParams.startY[k];

                                dX = cX - x1;
                                dY = cY - y1;
                                float angleK = dY / dX;
                                if (angleK > 0)
                                {
                                    angle = (float)MathEx.ToDegrees(-Math.Atan(angleK));
                                }
                                else
                                {
                                    angle = (float)MathEx.ToDegrees(Math.Atan(Math.Abs(angleK)));
                                }
                                angle -= 90;

                                localX = transitionParams.startX[k];
                                localY = transitionParams.startY[k];
                                mapPoints.X = localX;
                                mapPoints.Y = localY;
                                mapPoints = PostRotate(transitionParams.progress * angle + transitionParams.progress * transitionParams.angle[k], cX, cY, mapPoints);
                                localX = mapPoints.X;
                                localY = mapPoints.Y;

                                // 0 right_top
                                // 1 right_bottom
                                // 2 left_bottom
                                // 3 left_top
                                int endQuarter;
                                int startQuarter;

                                if (Math.Abs(xPoint - localX) < 0.001 && ((localY < cY && yPoint < cY) || (localY > cY && yPoint > cY)))
                                {
                                    if (transitionParams.angle[k] == -180f)
                                    {
                                        endQuarter = 0;
                                        startQuarter = 0;
                                    }
                                    else
                                    {
                                        endQuarter = 0;
                                        startQuarter = 3;
                                    }
                                }
                                else
                                {
                                    endQuarter = QuarterForPoint(xPoint, yPoint);
                                    startQuarter = QuarterForPoint(localX, localY);
                                }

                                for (int q = endQuarter; q <= startQuarter; q++)
                                {
                                    if (q == 0)
                                    {
                                        line.chartPath.AddLine(MeasuredWidth, 0);
                                    }
                                    else if (q == 1)
                                    {
                                        line.chartPath.AddLine(MeasuredWidth, MeasuredHeight);
                                    }
                                    else if (q == 2)
                                    {
                                        line.chartPath.AddLine(0, MeasuredHeight);
                                    }
                                    else
                                    {
                                        line.chartPath.AddLine(0, 0);
                                    }
                                }
                            }
                        }

                        stackOffset += height;
                    }
                }

                //canvas.save();

                //canvas.clipRect(startXPoint, SIGNATURE_TEXT_HEIGHT, endXPoint, MeasuredHeight - chartBottom);
                var clip = canvas.CreateLayer(1, CreateRect(startXPoint, SIGNATURE_TEXT_HEIGHT, endXPoint, MeasuredHeight - chartBottom));

                //if (hasEmptyPoint)
                //{
                //    canvas.drawColor(Theme.getColor(Theme.key_statisticChartLineEmpty));
                //}
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

                if (ovalPath != null)
                {
                    ovalPath.Dispose();
                    ovalPath = null;
                }
            }
        }

        private Vector2 PostRotate(float degree, float px, float py, Vector2 points)
        {
            var matrix = Matrix3x2.CreateRotation(MathFEx.ToRadians(degree), new Vector2(px, py));
            return Vector2.Transform(points, matrix);
        }

        private int QuarterForPoint(float x, float y)
        {
            float cX = chartArea.centerX();
            float cY = chartArea.centerY() + 16;

            if (x >= cX && y <= cY)
            {
                return 0;
            }
            if (x >= cX && y >= cY)
            {
                return 1;
            }
            if (x < cX && y >= cY)
            {
                return 2;
            }
            return 3;
        }

        protected override void DrawPickerChart(CanvasDrawingSession canvas)
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
                        if (!line.enabled && line.alpha == 0)
                        {
                            continue;
                        }

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
                        if (!line.enabled && line.alpha == 0)
                        {
                            continue;
                        }

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
                                yPercentage = chartData.simplifiedY[k][i] * line.alpha / sum;
                            }
                        }

                        float height = yPercentage * pickerHeight;
                        float yPoint = pickerHeight - height - stackOffset;

                        if (i == 0)
                        {
                            line.chartPathPicker = new CanvasPathBuilder(canvas);
                            line.chartPathPicker.BeginFigure(new Vector2(0, pickerHeight));
                            skipPoints[k] = false;
                        }

                        if (chartData.simplifiedY[k][i] == 0 && i > 0 && chartData.simplifiedY[k][i - 1] == 0 && i < n - 1 && chartData.simplifiedY[k][i + 1] == 0)
                        {
                            if (!skipPoints[k])
                            {
                                line.chartPathPicker.AddLine(new Vector2(xPoint, pickerHeight));
                            }
                            skipPoints[k] = true;
                        }
                        else
                        {
                            if (skipPoints[k])
                            {
                                line.chartPathPicker.AddLine(new Vector2(xPoint, pickerHeight));
                            }
                            line.chartPathPicker.AddLine(new Vector2(xPoint, yPoint));
                            skipPoints[k] = false;
                        }

                        if (i == n - 1)
                        {
                            line.chartPathPicker.AddLine(new Vector2(pickerWidth, pickerHeight));
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

        protected override void OnDraw(CanvasDrawingSession canvas)
        {
            Tick();
            DrawChart(canvas);
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

        public override int FindMaxValue(int startXIndex, int endXIndex)
        {
            return 100;
        }

        protected override float GetMinDistance()
        {
            return 0.1f;
        }

        public override void FillTransitionParams(TransitionParams param)
        {
            if (chartData == null)
            {
                return;
            }
            float fullWidth = chartWidth / (pickerDelegate.pickerEnd - pickerDelegate.pickerStart);
            float offset = fullWidth * pickerDelegate.pickerStart - HORIZONTAL_PADDING;

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


            transitionParams.startX = new float[chartData.lines.Count];
            transitionParams.startY = new float[chartData.lines.Count];
            transitionParams.endX = new float[chartData.lines.Count];
            transitionParams.endY = new float[chartData.lines.Count];
            transitionParams.angle = new float[chartData.lines.Count];


            for (int j = 0; j < 2; j++)
            {
                int i = localStart;
                if (j == 1)
                {
                    i = localEnd;
                }
                int stackOffset = 0;
                float sum = 0;
                int drawingLinesCount = 0;
                for (int k = 0; k < lines.Count; k++)
                {
                    LineViewData line = lines[k];
                    if (!line.enabled && line.alpha == 0)
                    {
                        continue;
                    }

                    if (line.line.y[i] > 0)
                    {
                        sum += line.line.y[i] * line.alpha;
                        drawingLinesCount++;
                    }
                }

                for (int k = 0; k < lines.Count; k++)
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
                        else
                        {
                            yPercentage = y[i] * line.alpha / sum;
                        }
                    }

                    float xPoint = chartData.xPercentage[i] * fullWidth - offset;
                    float height = yPercentage * (MeasuredHeight - chartBottom - SIGNATURE_TEXT_HEIGHT);
                    float yPoint = MeasuredHeight - chartBottom - height - stackOffset;
                    stackOffset += (int)height;

                    if (j == 0)
                    {
                        transitionParams.startX[k] = xPoint;
                        transitionParams.startY[k] = yPoint;
                    }
                    else
                    {
                        transitionParams.endX[k] = xPoint;
                        transitionParams.endY[k] = yPoint;
                    }
                }
            }
        }
    }
}
