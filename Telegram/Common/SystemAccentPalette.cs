//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Services.Settings;
using Windows.UI;

namespace Telegram.Common
{
    /// <summary>
    /// Derives the six shades Windows exposes as SystemAccentColorLight1-3 and Dark1-3 from an
    /// arbitrary accent, so the theme's accent can replace the system one.
    /// </summary>
    /// <remarks>
    /// This is Windows' own algorithm, reversed from
    /// SettingsHandlers_nt!palette_formula::get_palette_from_color: CIELab places the ends of the
    /// ramp, plain RGB walks it, HSL puts back the saturation the walk washed out. Reproduced
    /// bit-exact against the shipping function over 5000 colors, bar 15 rounding ties.
    ///
    /// These are not <see cref="ThemeColorizer"/>'s shades and can't be swapped for them: the
    /// framework brushes reach much further from the accent than Telegram's do (in dark theme
    /// AccentFillColorDefault is Light2 and AccentTextFillColorPrimary is Light3), so Telegram's
    /// Light3 sits roughly a third of the way to where the framework expects it.
    /// </remarks>
    public static class SystemAccentPalette
    {
        private readonly struct Step
        {
            /// <summary>
            /// Whether the ramp runs towards white or towards black.
            /// </summary>
            public readonly bool Light;

            /// <summary>
            /// How far along it to stop, as Windows counts it - towards the endpoint for a light
            /// shade, and away from it for a dark one.
            /// </summary>
            public readonly double Weight;

            public Step(bool light, double weight)
            {
                Light = light;
                Weight = weight;
            }
        }

        private static readonly Step[] _shades = new Step[]
        {
            new Step(true, 0.00), // Default, never used
            new Step(true, 0.16), // Light1
            new Step(true, 0.58), // Light2
            new Step(true, 0.82), // Light3
            new Step(false, 0.78), // Dark1
            new Step(false, 0.50), // Dark2
            new Step(false, 0.18), // Dark3
        };

        public static Color GetShade(Color accent, AccentShade shade)
        {
            if (shade == AccentShade.Default)
            {
                return accent;
            }

            var step = _shades[(int)shade];

            RgbToLab(accent, out _, out var a, out var b);

            // The ends of the ramp keep the accent's a and b and pin only lightness, so every
            // accent gets its own, rather than every accent aiming at one pair of fixed tones.
            // That is what keeps the ramp ordered for an accent that is already very light.
            //
            // Windows clamps the accent's own lightness into [49, 50] first and ramps from that,
            // which is why it hands back a darker color than the one you picked. Omitted here:
            // the accent is the theme's and has to survive.
            var endpoint = LabToRgb(step.Light ? 100 : 0, a, b);

            // Windows passes the dark steps to its lerp the other way round, which makes the
            // weight the accent's share rather than the endpoint's. Folding that into 1 - weight
            // would shift the last bit, and with it the rounding.
            var mixed = step.Light
                ? Interpolate(accent, endpoint, step.Weight)
                : Interpolate(endpoint, accent, step.Weight);

            return SaturateMatch(accent, mixed);
        }

        private static Color Interpolate(Color x, Color y, double weight)
        {
            var inverse = 1 - weight;

            return Color.FromArgb(x.A,
                (byte)(x.R * inverse + y.R * weight + 0.5),
                (byte)(x.G * inverse + y.G * weight + 0.5),
                (byte)(x.B * inverse + y.B * weight + 0.5));
        }

        /// <summary>
        /// Takes the produced color's hue and lightness, but keeps whichever of the two
        /// saturations is higher: interpolating towards white or black bleeds saturation out, and
        /// this only ever puts it back.
        /// </summary>
        private static Color SaturateMatch(Color reference, Color color)
        {
            RgbToHsl(reference, out _, out var referenceSaturation, out _);
            RgbToHsl(color, out var h, out var s, out var l);

            return HslToRgb(h, Math.Max(referenceSaturation, s), l, color.A);
        }

        // Windows' constants throughout: XYZ on a 0..100 scale against D65, the four-decimal sRGB
        // matrix, and the old 0.008856 / 7.787 form of the Lab transfer rather than 216/24389.
        // Matching them matters - the modern constants move entries by a unit.

        private static void RgbToLab(Color color, out double l, out double a, out double b)
        {
            var r = ToLinear(color.R) * 100;
            var g = ToLinear(color.G) * 100;
            var v = ToLinear(color.B) * 100;

            var x = Pivot((0.4124 * r + 0.3576 * g + 0.1805 * v) / 95.047);
            var y = Pivot((0.2126 * r + 0.7152 * g + 0.0722 * v) / 100.0);
            var z = Pivot((0.0193 * r + 0.1192 * g + 0.9505 * v) / 108.883);

            l = 116 * y - 16;
            a = 500 * (x - y);
            b = 200 * (y - z);
        }

        private static Color LabToRgb(double l, double a, double b)
        {
            var y = (l + 16) / 116;
            var x = y + a / 500;
            var z = y - b / 200;

            x = PivotInverse(x) * 0.95047;
            y = PivotInverse(y);
            z = PivotInverse(z) * 1.08883;

            return Color.FromArgb(255,
                FromLinear(3.2406 * x - 1.5372 * y - 0.4986 * z),
                FromLinear(-0.9689 * x + 1.8758 * y + 0.0415 * z),
                FromLinear(0.0557 * x - 0.2040 * y + 1.0570 * z));
        }

        private static double Pivot(double value)
        {
#if NET9_0_OR_GREATER
            return value > 0.008856
                ? Math.Cbrt(value)
                : 7.787 * value + 16.0 / 116;
#else
            return value > 0.008856
                ? Cbrt(value)
                : 7.787 * value + 16.0 / 116;
        }

        private const double MinNormal = 2.2250738585072014e-308;
        private const double TwoPow54 = 1.8014398509481984e16;
        private const double TwoPowM18 = 1.0 / 262144.0;   // cbrt(2^-54)

        public static double Cbrt(double x)
        {
            // NaN, +/-Infinity and +/-0 all map to themselves.
            if (x == 0.0 || double.IsNaN(x) || double.IsInfinity(x))
            {
                return x;
            }

            var sign = 1.0;
            if (x < 0.0)
            {
                sign = -1.0;
                x = -x;
            }

            // Subnormals have no implicit leading 1, so scale them up first
            // and undo it at the end.
            var post = 1.0;
            if (x < MinNormal)
            {
                x *= TwoPow54;
                post = TwoPowM18;
            }

            var bits = BitConverter.DoubleToInt64Bits(x);
            var exp = (int)((bits >> 52) & 0x7FF) - 1023;

            // Mantissa forced back into [1, 2).
            var m = BitConverter.Int64BitsToDouble(
                (bits & 0x000FFFFFFFFFFFFFL) | 0x3FF0000000000000L);

            // exp = 3q + r with 0 <= r < 3, so cbrt(x) = 2^q * cbrt(m * 2^r).
            var q = exp / 3;
            var r = exp - 3 * q;
            if (r < 0)
            {
                r += 3;
                q--;
            }

            var t = r == 0 ? m : (r == 1 ? m * 2.0 : m * 4.0);   // t in [1, 8)

            // Quadratic seed, |relative error| < 2.5% over the whole range.
            var y = 0.7400935 + t * (0.2745377 - t * 0.01463117);

            // Newton squares the relative error each pass:
            // 2.5e-2 -> 6e-4 -> 4e-7 -> 2e-13 -> below the rounding floor.
            y -= (y - t / (y * y)) / 3.0;
            y -= (y - t / (y * y)) / 3.0;
            y -= (y - t / (y * y)) / 3.0;
            y -= (y - t / (y * y)) / 3.0;

            // 2^q, built directly. q is in [-341, 341] here, so this is always
            // a normal double and the multiply below is exact.
            var p2 = BitConverter.Int64BitsToDouble((long)(q + 1023) << 52);

            return sign * y * p2 * post;
#endif
        }

        private static double PivotInverse(double value)
        {
            var cubed = value * value * value;
            return cubed > 0.008856
                ? cubed
                : (value - 16.0 / 116) / 7.787;
        }

        private static void RgbToHsl(Color color, out double h, out double s, out double l)
        {
            var r = color.R / 255.0;
            var g = color.G / 255.0;
            var b = color.B / 255.0;

            var max = Math.Max(r, Math.Max(g, b));
            var min = Math.Min(r, Math.Min(g, b));

            l = (max + min) / 2;

            if (max == min)
            {
                h = 0;
                s = 0;
                return;
            }

            var delta = max - min;

            s = l > 0.5
                ? delta / (2 - max - min)
                : delta / (max + min);

            if (max == r)
            {
                h = (g - b) / delta + (g < b ? 6 : 0);
            }
            else if (max == g)
            {
                h = (b - r) / delta + 2;
            }
            else
            {
                h = (r - g) / delta + 4;
            }

            h /= 6;
        }

        private static Color HslToRgb(double h, double s, double l, byte alpha)
        {
            if (s == 0)
            {
                var gray = (byte)(l * 255 + 0.5);
                return Color.FromArgb(alpha, gray, gray, gray);
            }

            var q = l < 0.5
                ? l * (1 + s)
                : l + s - l * s;
            var p = 2 * l - q;

            return Color.FromArgb(alpha,
                FromHue(p, q, h + 1.0 / 3),
                FromHue(p, q, h),
                FromHue(p, q, h - 1.0 / 3));
        }

        private static byte FromHue(double p, double q, double t)
        {
            if (t < 0)
            {
                t += 1;
            }
            else if (t > 1)
            {
                t -= 1;
            }

            double value;
            if (t < 1.0 / 6)
            {
                value = p + (q - p) * 6 * t;
            }
            else if (t < 1.0 / 2)
            {
                value = q;
            }
            else if (t < 2.0 / 3)
            {
                value = p + (q - p) * (2.0 / 3 - t) * 6;
            }
            else
            {
                value = p;
            }

            return (byte)(Math.Clamp(value, 0, 1) * 255 + 0.5);
        }

        private static double ToLinear(byte value)
        {
            var c = value / 255.0;
            return c <= 0.04045
                ? c / 12.92
                : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        private static byte FromLinear(double value)
        {
            var c = Math.Clamp(value, 0, 1);
            c = c <= 0.0031308
                ? c * 12.92
                : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055;

            return (byte)(c * 255 + 0.5);
        }
    }
}
