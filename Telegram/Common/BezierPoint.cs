//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;

namespace Telegram.Common
{
    public partial class BezierPoint
    {
        private static float A(float a1, float a2)
        {
            return 1.0f - 3.0f * a2 + 3.0f * a1;
        }

        private static float B(float a1, float a2)
        {
            return 3.0f * a2 - 6.0f * a1;
        }

        private static float C(float a1)
        {
            return 3.0f * a1;
        }

        private static float CalcBezier(float t, float a1, float a2)
        {
            return ((A(a1, a2) * t + B(a1, a2)) * t + C(a1)) * t;
        }

        private static float CalcSlope(float t, float a1, float a2)
        {
            return 3.0f * A(a1, a2) * t * t + 2.0f * B(a1, a2) * t + C(a1);
        }

        private static float GetTForX(float x, float x1, float x2)
        {
            float t = x;
            int i = 0;

            while (i < 4)
            {
                float currentSlope = CalcSlope(t, x1, x2);

                if (Math.Abs(currentSlope) < float.Epsilon)
                {
                    return t;
                }
                else
                {
                    float currentX = CalcBezier(t, x1, x2) - x;
                    t -= currentX / currentSlope;
                }

                i++;
            }

            return t;
        }

        public static float Calculate(float x1, float y1, float x2, float y2, float x)
        {
            float value = CalcBezier(GetTForX(x, x1, x2), y1, y2);

            if (value >= 0.997f)
            {
                value = 1.0f;
            }

            return value;
        }
    }
}
