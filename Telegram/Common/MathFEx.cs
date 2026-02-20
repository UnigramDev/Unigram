//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Numerics;

namespace Telegram.Common
{
    public static class MathFEx
    {
        public static float ToRadians(float degrees)
        {
            float radians = MathF.PI / 180 * degrees;
            return radians;
        }

        public static float ToDegrees(float radians)
        {
            float degrees = 180 / MathF.PI * radians;
            return degrees;
        }

        public static float Lerp(float a, float b, float f)
        {
            return a + f * (b - a);
        }

        public static float DistanceToFarthestCorner(Vector2 point, Vector2 size)
        {
            float dist1 = Distance(point.X, point.Y, 0, 0);
            float dist2 = Distance(point.X, point.Y, size.X, 0);
            float dist3 = Distance(point.X, point.Y, size.X, size.Y);
            float dist4 = Distance(point.X, point.Y, 0, size.Y);

            return Math.Max(Math.Max(dist1, dist2), Math.Max(dist3, dist4));
        }

        public static float Distance(float x1, float y1, float x2, float y2)
        {
            return MathF.Sqrt(MathF.Pow(x2 - x1, 2) + MathF.Pow(y2 - y1, 2));
        }
    }
}
