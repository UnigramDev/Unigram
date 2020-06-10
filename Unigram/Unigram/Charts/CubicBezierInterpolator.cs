using System;
using System.Numerics;

namespace Unigram.Charts
{
    public class CubicBezierInterpolator //implements Interpolator
    {
        public static readonly CubicBezierInterpolator DEFAULT = new CubicBezierInterpolator(0.25, 0.1, 0.25, 1);
        public static readonly CubicBezierInterpolator EASE_OUT = new CubicBezierInterpolator(0, 0, .58, 1);
        public static readonly CubicBezierInterpolator EASE_OUT_QUINT = new CubicBezierInterpolator(.23, 1, .32, 1);
        public static readonly CubicBezierInterpolator EASE_IN = new CubicBezierInterpolator(.42, 0, 1, 1);
        public static readonly CubicBezierInterpolator EASE_BOTH = new CubicBezierInterpolator(.42, 0, .58, 1);

        protected Vector2 start;
        protected Vector2 end;
        protected Vector2 a = new Vector2();
        protected Vector2 b = new Vector2();
        protected Vector2 c = new Vector2();

        public CubicBezierInterpolator(Vector2 start, Vector2 end)
        {
            if (start.X < 0 || start.X > 1)
            {
                throw new ArgumentOutOfRangeException("startX value must be in the range [0, 1]");
            }
            if (end.X < 0 || end.X > 1)
            {
                throw new ArgumentOutOfRangeException("endX value must be in the range [0, 1]");
            }
            this.start = start;
            this.end = end;
        }

        public CubicBezierInterpolator(float startX, float startY, float endX, float endY)
            : this(new Vector2(startX, startY), new Vector2(endX, endY))
        {
        }

        public CubicBezierInterpolator(double startX, double startY, double endX, double endY)
            : this((float)startX, (float)startY, (float)endX, (float)endY)
        {
        }

        //@Override
        public float getInterpolation(float time)
        {
            return getBezierCoordinateY(getXForTime(time));
        }

        protected float getBezierCoordinateY(float time)
        {
            c.Y = 3 * start.Y;
            b.Y = 3 * (end.Y - start.Y) - c.Y;
            a.Y = 1 - c.Y - b.Y;
            return time * (c.Y + time * (b.Y + time * a.Y));
        }

        protected float getXForTime(float time)
        {
            float x = time;
            float z;
            for (int i = 1; i < 14; i++)
            {
                z = getBezierCoordinateX(x) - time;
                if (Math.Abs(z) < 1e-3)
                {
                    break;
                }
                x -= z / getXDerivate(x);
            }
            return x;
        }

        private float getXDerivate(float t)
        {
            return c.X + t * (2 * b.X + 3 * a.X * t);
        }

        private float getBezierCoordinateX(float time)
        {
            c.X = 3 * start.X;
            b.X = 3 * (end.X - start.X) - c.X;
            a.X = 1 - c.X - b.X;
            return time * (c.X + time * (b.X + time * a.X));
        }
    }
}
