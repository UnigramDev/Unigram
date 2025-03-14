//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;

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
    }
}
