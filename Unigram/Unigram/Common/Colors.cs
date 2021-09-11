using System;
using Windows.UI;

namespace Unigram.Common
{
    public struct RGB
    {
        private byte _r;
        private byte _g;
        private byte _b;

        public RGB(byte r, byte g, byte b)
        {
            _r = r;
            _g = g;
            _b = b;
        }

        public byte R
        {
            get => _r;
            set => _r = value;
        }

        public byte G
        {
            get => _g;
            set => _g = value;
        }

        public byte B
        {
            get => _b;
            set => _b = value;
        }

        public bool Equals(RGB rgb)
        {
            return (R == rgb.R) && (G == rgb.G) && (B == rgb.B);
        }

        public static implicit operator Color(RGB rhs)
        {
            return Color.FromArgb(255, rhs.R, rhs.G, rhs.B);
        }

        public static implicit operator RGB(Color lhs)
        {
            return new RGB(lhs.R, lhs.G, lhs.B);
        }

        public HSV ToHSV()
        {
            RGB rgb = this;
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

        public HSL ToHSL()
        {
            RGB rgb = this;
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
    }

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

        public RGB ToRGB()
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

            return new RGB((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
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

        public RGB ToRGB()
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

            return new RGB(r, g, b);
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

        public static Color FromHex(uint hexValue)
        {
            byte a = (byte)((hexValue & 0xff000000) >> 24);
            byte r = (byte)((hexValue & 0x00ff0000) >> 16);
            byte g = (byte)((hexValue & 0x0000ff00) >> 8);
            byte b = (byte)(hexValue & 0x000000ff);

            return Color.FromArgb(a > 0 ? a : (byte)0xFF, r, g, b);
        }

        public static Color FromHex(int hexValue)
        {
            byte a = (byte)((hexValue & 0xff000000) >> 24);
            byte r = (byte)((hexValue & 0x00ff0000) >> 16);
            byte g = (byte)((hexValue & 0x0000ff00) >> 8);
            byte b = (byte)(hexValue & 0x000000ff);

            return Color.FromArgb(a > 0 ? a : (byte)0xFF, r, g, b);
        }

        public static int ToHex(Color color)
        {
            return (color.A << 24) + (color.R << 16) + (color.G << 8) + color.B;
        }

        public static HSV ToHSV(this Color color)
        {
            RGB rgb = color;
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


        public static HSL ToHSL(this Color color)
        {
            RGB rgb = color;
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

        public static Color GetPatternColor(Color color)
        {
            var rgb = (RGB)color;
            var hsb = rgb.ToHSV();
            if (hsb.S > 0.0f || (hsb.V < 1.0f && hsb.V > 0.0f))
            {
                hsb.S = Math.Min(1.0f, hsb.S + 0.05f + 0.1f * (1.0f - hsb.S));
            }
            if (hsb.V > 0.5f)
            {
                hsb.V = Math.Max(0.0f, hsb.V * 0.65f);
            }
            else
            {
                hsb.V = Math.Max(0.0f, Math.Min(1.0f, 1.0f - hsb.V * 0.65f));
            }

            var result = hsb.ToRGB();
            return Color.FromArgb(0x66, result.R, result.G, result.B);
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
    }
}
