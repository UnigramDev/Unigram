//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;

namespace Telegram.Common
{
    public static class MathEx
    {
        public static double ToRadians(double degrees)
        {
            double radians = Math.PI / 180 * degrees;
            return radians;
        }

        public static double ToDegrees(double radians)
        {
            double degrees = 180 / Math.PI * radians;
            return degrees;
        }

        public static double Lerp(double a, double b, double f)
        {
            return a + f * (b - a);
        }
    }
}
