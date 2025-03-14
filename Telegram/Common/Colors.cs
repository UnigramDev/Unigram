//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Globalization;
using Windows.UI;

namespace Telegram.Common
{
    public struct HSV
    {
        private double _h;
        private double _s;
        private double _v;

        public HSV(double h, double s, double v)
        {
            _h = h;
            _s = s;
            _v = v;
        }

        public double H
        {
            get => _h;
            set => _h = value;
        }

        public double S
        {
            get => _s;
            set => _s = value;
        }

        public double V
        {
            get => _v;
            set => _v = value;
        }

        public bool Equals(HSV hsv)
        {
            return (H == hsv.H) && (S == hsv.S) && (V == hsv.V);
        }

        public Color ToRGB(byte alpha = 255)
        {
            HSV hsv = this;
            double r;
            double g;
            double b;

            if (hsv.S == 0)
            {
                r = hsv.V;
                g = hsv.V;
                b = hsv.V;
            }
            else
            {
                int i;
                double f, p, q, t;

                if (hsv.H == 360)
                {
                    hsv.H = 0;
                }
                else
                {
                    hsv.H /= 60;
                }

                i = (int)Math.Truncate(hsv.H);
                f = hsv.H - i;

                p = hsv.V * (1.0 - hsv.S);
                q = hsv.V * (1.0 - hsv.S * f);
                t = hsv.V * (1.0 - hsv.S * (1.0 - f));

                switch (i)
                {
                    case 0:
                        r = hsv.V;
                        g = t;
                        b = p;
                        break;

                    case 1:
                        r = q;
                        g = hsv.V;
                        b = p;
                        break;

                    case 2:
                        r = p;
                        g = hsv.V;
                        b = t;
                        break;

                    case 3:
                        r = p;
                        g = q;
                        b = hsv.V;
                        break;

                    case 4:
                        r = t;
                        g = p;
                        b = hsv.V;
                        break;

                    default:
                        r = hsv.V;
                        g = p;
                        b = q;
                        break;
                }

            }

            return Color.FromArgb(alpha, (byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
        }
    }

    public struct HSL
    {
        private int _h;
        private double _s;
        private double _l;

        public HSL(int h, double s, double l)
        {
            _h = h;
            _s = s;
            _l = l;
        }

        public int H
        {
            get => _h;
            set => _h = value;
        }

        public double S
        {
            get => _s;
            set => _s = value;
        }

        public double L
        {
            get => _l;
            set => _l = value;
        }

        public bool Equals(HSL hsl)
        {
            return (H == hsl.H) && (S == hsl.S) && (L == hsl.L);
        }

        public Color ToRGB(byte alpha = 255)
        {
            var hsl = this;
            byte r;
            byte g;
            byte b;

            if (hsl.S == 0)
            {
                r = g = b = (byte)(hsl.L * 255);
            }
            else
            {
                double v1, v2;
                double hue = (float)hsl.H / 360;

                v2 = (hsl.L < 0.5) ? (hsl.L * (1 + hsl.S)) : (hsl.L + hsl.S - hsl.L * hsl.S);
                v1 = 2 * hsl.L - v2;

                r = (byte)(255 * HueToRGB(v1, v2, hue + 1.0f / 3));
                g = (byte)(255 * HueToRGB(v1, v2, hue));
                b = (byte)(255 * HueToRGB(v1, v2, hue - 1.0f / 3));
            }

            return Color.FromArgb(255, r, g, b);
        }

        private static double HueToRGB(double v1, double v2, double vH)
        {
            if (vH < 0)
            {
                vH += 1;
            }

            if (vH > 1)
            {
                vH -= 1;
            }

            if ((6 * vH) < 1)
            {
                return v1 + (v2 - v1) * 6 * vH;
            }

            if ((2 * vH) < 1)
            {
                return v2;
            }

            if ((3 * vH) < 2)
            {
                return v1 + (v2 - v1) * (2.0f / 3 - vH) * 6;
            }

            return v1;
        }
    }
    public static class ColorEx
    {
        public static Color WithAlpha(this Color color, byte alpha)
        {
            return Color.FromArgb(alpha, color.R, color.G, color.B);
        }

        public static bool TryParse(string hex, out Color color)
        {
            if (int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int value))
            {
                color = FromHex(value, false);
                return true;
            }

            color = default;
            return false;
        }

        public static Color FromHex(uint hexValue, bool allowDefault = false)
        {
            byte a = (byte)((hexValue & 0xff000000) >> 24);
            byte r = (byte)((hexValue & 0x00ff0000) >> 16);
            byte g = (byte)((hexValue & 0x0000ff00) >> 8);
            byte b = (byte)(hexValue & 0x000000ff);

            if (a == 0 && r == 0 && g == 0 && b == 0 && allowDefault)
            {
                return default;
            }

            return Color.FromArgb(a > 0 ? a : (byte)0xFF, r, g, b);
        }

        public static Color FromHex(int hexValue, bool allowDefault = false)
        {
            byte a = (byte)((hexValue & 0xff000000) >> 24);
            byte r = (byte)((hexValue & 0x00ff0000) >> 16);
            byte g = (byte)((hexValue & 0x0000ff00) >> 8);
            byte b = (byte)(hexValue & 0x000000ff);

            if (a == 0 && r == 0 && g == 0 && b == 0 && allowDefault)
            {
                return default;
            }

            return Color.FromArgb(a > 0 ? a : (byte)0xFF, r, g, b);
        }

        public static int ToHex(Color color)
        {
            return (color.A << 24) + (color.R << 16) + (color.G << 8) + color.B;
        }

        public static HSV ToHSV(this Color rgb)
        {
            double delta, min;
            double h = 0, s, v;

            min = Math.Min(Math.Min(rgb.R, rgb.G), rgb.B);
            v = Math.Max(Math.Max(rgb.R, rgb.G), rgb.B);
            delta = v - min;

            if (v == 0.0)
            {
                s = 0;
            }
            else
            {
                s = delta / v;
            }

            if (s == 0)
            {
                h = 0.0;
            }
            else
            {
                if (rgb.R == v)
                {
                    h = (rgb.G - rgb.B) / delta;
                }
                else if (rgb.G == v)
                {
                    h = 2 + (rgb.B - rgb.R) / delta;
                }
                else if (rgb.B == v)
                {
                    h = 4 + (rgb.R - rgb.G) / delta;
                }

                h *= 60;

                if (h < 0.0)
                {
                    h += 360;
                }
            }

            return new HSV(h, s, v / 255);
        }


        public static HSL ToHSL(this Color rgb)
        {
            HSL hsl = new HSL();

            float r = rgb.R / 255.0f;
            float g = rgb.G / 255.0f;
            float b = rgb.B / 255.0f;

            float min = Math.Min(Math.Min(r, g), b);
            float max = Math.Max(Math.Max(r, g), b);
            float delta = max - min;

            hsl.L = (max + min) / 2;

            if (delta == 0)
            {
                hsl.H = 0;
                hsl.S = 0.0f;
            }
            else
            {
                hsl.S = (hsl.L <= 0.5) ? (delta / (max + min)) : (delta / (2 - max - min));

                float hue;

                if (r == max)
                {
                    hue = (g - b) / 6 / delta;
                }
                else if (g == max)
                {
                    hue = 1.0f / 3 + (b - r) / 6 / delta;
                }
                else
                {
                    hue = 2.0f / 3 + (r - g) / 6 / delta;
                }

                if (hue < 0)
                {
                    hue += 1;
                }

                if (hue > 1)
                {
                    hue -= 1;
                }

                hsl.H = (int)(hue * 360);
            }

            return hsl;
        }

        public static Color GetPatternColor(Color rgb, bool alwaysDark = false)
        {
            var hsb = rgb.ToHSV();
            if (hsb.S > 0.0f || (hsb.V < 1.0f && hsb.V > 0.0f))
            {
                hsb.S = Math.Min(1.0f, hsb.S + (alwaysDark ? 0.15f : 0.05f) + 0.1f * (1.0f - hsb.S));
            }
            if (alwaysDark || hsb.V > 0.5f)
            {
                hsb.V = Math.Max(0.0f, hsb.V * 0.65f);
            }
            else
            {
                hsb.V = Math.Max(0.0f, Math.Min(1.0f, 1.0f - hsb.V * 0.65f));
            }

            var result = hsb.ToRGB();
            return Color.FromArgb(0x64, result.R, result.G, result.B);
        }

        public static Color GetAverageColor(Color color1, Color color2)
        {
            double r1 = color1.R;
            double r2 = color2.R;
            double g1 = color1.G;
            double g2 = color2.G;
            double b1 = color1.B;
            double b2 = color2.B;

            return Color.FromArgb(255, (byte)(r1 / 2d + r2 / 2d), (byte)(g1 / 2d + g2 / 2d), (byte)(b1 / 2d + b2 / 2d));
        }

        public static Color GetAverageColor(int color1, int color2)
        {
            return GetAverageColor(color1.ToColor(), color2.ToColor());
        }

        public static Color GetAverageColor(Color color1, int color2)
        {
            return GetAverageColor(color1, color2.ToColor());
        }

        public static Color GetAverageColor(int color1, Color color2)
        {
            return GetAverageColor(color1.ToColor(), color2);
        }

        public static bool IsDark(int color1, int color2, int color3, int color4)
        {
            Color averageColor = GetAverageColor(color1, color2);
            if (color3 != 0)
            {
                averageColor = GetAverageColor(averageColor, color3);
            }
            if (color4 != 0)
            {
                averageColor = GetAverageColor(averageColor, color4);
            }
            HSV hsb = averageColor.ToHSV();
            return hsb.V < 0.3f;
        }
    }
}
