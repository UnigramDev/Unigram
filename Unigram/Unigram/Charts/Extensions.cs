using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Globalization;
using Windows.Foundation;
using Windows.UI;

namespace Unigram.Charts
{
    public static class D2D1Extensions
    {
        public static void DrawText(this CanvasDrawingSession session, string text, float x, float y, Paint paint)
        {
            CanvasTextFormat textFormat = null;
            if (paint.TextSize is float textSize)
            {
                textFormat = new CanvasTextFormat();
                textFormat.FontSize = textSize;
            }
            if (paint.TextAlignment is CanvasHorizontalAlignment textAlignmnet)
            {
                textFormat = textFormat ?? new CanvasTextFormat();
                textFormat.HorizontalAlignment = textAlignmnet;
            }

            session.DrawText(text, x, y, paint.Color, textFormat);
            textFormat?.Dispose();
        }

        public static void DrawLine(this CanvasDrawingSession session, float x0, float y0, float x1, float y1, Paint paint)
        {
            CanvasStrokeStyle strokeStyle = null;
            if (paint.StrokeCap is CanvasCapStyle capStyle)
            {
                strokeStyle = new CanvasStrokeStyle();
                strokeStyle.StartCap = capStyle;
                strokeStyle.EndCap = capStyle;
                strokeStyle.LineJoin = capStyle == CanvasCapStyle.Round ? CanvasLineJoin.Round : CanvasLineJoin.Miter;
            }

            session.DrawLine(x0, y0, x1, y1, paint.Color, paint.StrokeWidth, strokeStyle);
            strokeStyle?.Dispose();
        }

        public static void DrawGeometry(this CanvasDrawingSession session, CanvasGeometry geometry, Paint paint)
        {
            CanvasStrokeStyle strokeStyle = null;
            if (paint.StrokeCap is CanvasCapStyle capStyle)
            {
                strokeStyle = new CanvasStrokeStyle();
                strokeStyle.StartCap = capStyle;
                strokeStyle.EndCap = capStyle;
                strokeStyle.LineJoin = capStyle == CanvasCapStyle.Round ? CanvasLineJoin.Round : CanvasLineJoin.Miter;
            }

            session.DrawGeometry(geometry, paint.Color, paint.StrokeWidth, strokeStyle);
            strokeStyle?.Dispose();
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

    public static class MathEx
    {
        public static double ToRadians(double degrees)
        {
            double radians = (Math.PI / 180) * degrees;
            return radians;
        }

        public static double ToDegrees(double radians)
        {
            double degrees = (180 / Math.PI) * radians;
            return degrees;
        }
    }

    public static class MathFEx
    {
        public static float ToRadians(float degrees)
        {
            float radians = (MathF.PI / 180) * degrees;
            return radians;
        }

        public static float ToDegrees(float radians)
        {
            float degrees = (180 / MathF.PI) * radians;
            return degrees;
        }
    }

    public static class Extensions
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
            i |= (i >> 1);
            i |= (i >> 2);
            i |= (i >> 4);
            i |= (i >> 8);
            i |= (i >> 16);
            return i - (i >> 1);
        }

        public static Color ToColor(this int color)
        {
            return Color.FromArgb(0xFF, (byte)((color >> 16) & 0xFF), (byte)((color >> 8) & 0xFF), (byte)(color & 0xFF));
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

        public static int ToValue(this Color color)
        {
            return (color.R << 16) + (color.G << 8) + color.B;
        }

        public static int ToTimestamp(this DateTime dateTime)
        {
            var dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            DateTime.SpecifyKind(dtDateTime, DateTimeKind.Utc);

            return (int)(dateTime.ToUniversalTime() - dtDateTime).TotalSeconds;
        }
    }
}
