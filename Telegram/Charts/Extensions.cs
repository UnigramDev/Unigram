//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Globalization;
using Windows.Foundation;
using Windows.UI;

namespace Telegram.Charts
{
    public static class D2D1Extensions
    {
        public static void DrawText(this CanvasDrawingSession session, string text, float x, float y, Paint paint, CanvasTextFormat textFormat = null)
        {
            // The format is the paint's, or the caller's - never a new one per call. It is a DWrite
            // object, and where the family names a packaged font, building one costs about a
            // millisecond in font resolution: once per label per frame here.
            //
            // And nothing is disposed: this used to dispose whatever the caller passed in, so a
            // format held across frames was dead after its first use.
            textFormat ??= paint.TextFormat;

            if (paint.TextSize is float textSize && textFormat.FontSize != textSize)
            {
                textFormat.FontSize = textSize;
            }

            if (paint.TextAlignment is CanvasHorizontalAlignment textAlignmnet && textFormat.HorizontalAlignment != textAlignmnet)
            {
                textFormat.HorizontalAlignment = textAlignmnet;
            }

            session.DrawText(text, x, y, paint.Color, textFormat);
        }

        public static void DrawLine(this CanvasDrawingSession session, float x0, float y0, float x1, float y1, Paint paint)
        {
            // The paint's own stroke style, configured only where it differs: a new one per call
            // is a D2D object built for a single line, and the charts draw a great many of them.
            CanvasStrokeStyle strokeStyle = null;
            if (paint.StrokeCap is CanvasCapStyle capStyle)
            {
                var lineJoin = capStyle == CanvasCapStyle.Round ? CanvasLineJoin.Round : CanvasLineJoin.Miter;

                strokeStyle = paint.StrokeStyle;

                if (strokeStyle.StartCap != capStyle) strokeStyle.StartCap = capStyle;
                if (strokeStyle.EndCap != capStyle) strokeStyle.EndCap = capStyle;
                if (strokeStyle.LineJoin != lineJoin) strokeStyle.LineJoin = lineJoin;
            }

            session.DrawLine(x0, y0, x1, y1, paint.Color, paint.StrokeWidth, strokeStyle);
        }

        public static void DrawGeometry(this CanvasDrawingSession session, CanvasGeometry geometry, Paint paint)
        {
            // The paint's own stroke style, configured only where it differs: a new one per call
            // is a D2D object built for a single line, and the charts draw a great many of them.
            CanvasStrokeStyle strokeStyle = null;
            if (paint.StrokeCap is CanvasCapStyle capStyle)
            {
                var lineJoin = capStyle == CanvasCapStyle.Round ? CanvasLineJoin.Round : CanvasLineJoin.Miter;

                strokeStyle = paint.StrokeStyle;

                if (strokeStyle.StartCap != capStyle) strokeStyle.StartCap = capStyle;
                if (strokeStyle.EndCap != capStyle) strokeStyle.EndCap = capStyle;
                if (strokeStyle.LineJoin != lineJoin) strokeStyle.LineJoin = lineJoin;
            }

            session.DrawGeometry(geometry, paint.Color, paint.StrokeWidth, strokeStyle);
        }

        public static void FillCircle(this CanvasDrawingSession session, float x, float y, Paint paint)
        {
            session.FillCircle(x, y, paint.StrokeWidth / 2, paint.Color);
        }

        //public static void FillRectangle(this CanvasDrawingSession session, Rect rect, Paint paint)
        //{

        //}
        public static void DrawLines(this CanvasDrawingSession session, float[] points, int start, int length, Paint paint)
        {
            for (int i = start; i < length - 3; i += 4)
            {
                session.DrawLine(points[i], points[i + 1], points[i + 2], points[i + 3], paint.Color, paint.StrokeWidth);
            }
        }

    }

    public static class ChartExtensions
    {
        public static Color blendARGB(this Color color1, Color color2, float ratio)
        {
            float inverseRatio = 1 - ratio;
            float a = color1.A * inverseRatio + color2.A * ratio;
            float r = color1.R * inverseRatio + color2.R * ratio;
            float g = color1.G * inverseRatio + color2.G * ratio;
            float b = color1.B * inverseRatio + color2.B * ratio;
            return Color.FromArgb((byte)a, (byte)r, (byte)g, (byte)b);
        }

        public static int centerX(this Rect rect)
        {
            return (int)(rect.Left + rect.Right) >> 1;
        }

        /**
         * @return the vertical center of the rectangle. If the computed value
         *         is fractional, this method returns the largest integer that is
         *         less than the computed value.
         */
        public static int centerY(this Rect rect)
        {
            return (int)(rect.Top + rect.Bottom) >> 1;
        }

        public static int HighestOneBit(this int i)
        {
            i |= i >> 1;
            i |= i >> 2;
            i |= i >> 4;
            i |= i >> 8;
            i |= i >> 16;
            return i - (i >> 1);
        }

        public static Color ToColor(this string color)
        {
            color = color.Trim('#');
            if (int.TryParse(color, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hexValue))
            {
                byte r = (byte)((hexValue & 0x00ff0000) >> 16);
                byte g = (byte)((hexValue & 0x0000ff00) >> 8);
                byte b = (byte)(hexValue & 0x000000ff);

                return Color.FromArgb(255, r, g, b);
            }

            return default;
        }

        public static long ToTimestamp(this DateTime dateTime)
        {
            var dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            DateTime.SpecifyKind(dtDateTime, DateTimeKind.Utc);

            return (long)(dateTime.ToUniversalTime() - dtDateTime).TotalSeconds;
        }
    }
}
