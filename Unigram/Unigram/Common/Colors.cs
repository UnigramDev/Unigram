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
            this._r = r;
            this._g = g;
            this._b = b;
        }

        public byte R
        {
            get { return this._r; }
            set { this._r = value; }
        }

        public byte G
        {
            get { return this._g; }
            set { this._g = value; }
        }

        public byte B
        {
            get { return this._b; }
            set { this._b = value; }
        }

        public bool Equals(RGB rgb)
        {
            return (this.R == rgb.R) && (this.G == rgb.G) && (this.B == rgb.B);
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
                s = 0;
            else
                s = delta / v;

            if (s == 0)
                h = 0.0;

            else
            {
                if (rgb.R == v)
                    h = (rgb.G - rgb.B) / delta;
                else if (rgb.G == v)
                    h = 2 + (rgb.B - rgb.R) / delta;
                else if (rgb.B == v)
                    h = 4 + (rgb.R - rgb.G) / delta;

                h *= 60;

                if (h < 0.0)
                    h = h + 360;
            }

            return new HSV(h, s, (v / 255));
        }

        public HSL ToHSL()
        {
            RGB rgb = this;
            HSL hsl = new HSL();

            float r = (rgb.R / 255.0f);
            float g = (rgb.G / 255.0f);
            float b = (rgb.B / 255.0f);

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
                    hue = ((g - b) / 6) / delta;
                }
                else if (g == max)
                {
                    hue = (1.0f / 3) + ((b - r) / 6) / delta;
                }
                else
                {
                    hue = (2.0f / 3) + ((r - g) / 6) / delta;
                }

                if (hue < 0)
                    hue += 1;
                if (hue > 1)
                    hue -= 1;

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
            this._h = h;
            this._s = s;
            this._v = v;
        }

        public double H
        {
            get { return this._h; }
            set { this._h = value; }
        }

        public double S
        {
            get { return this._s; }
            set { this._s = value; }
        }

        public double V
        {
            get { return this._v; }
            set { this._v = value; }
        }

        public bool Equals(HSV hsv)
        {
            return (this.H == hsv.H) && (this.S == hsv.S) && (this.V == hsv.V);
        }

        public RGB ToRGB()
        {
            HSV hsv = this;
            double r = 0, g = 0, b = 0;

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
                    hsv.H = 0;
                else
                    hsv.H = hsv.H / 60;

                i = (int)Math.Truncate(hsv.H);
                f = hsv.H - i;

                p = hsv.V * (1.0 - hsv.S);
                q = hsv.V * (1.0 - (hsv.S * f));
                t = hsv.V * (1.0 - (hsv.S * (1.0 - f)));

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
            this._h = h;
            this._s = s;
            this._l = l;
        }

        public int H
        {
            get { return this._h; }
            set { this._h = value; }
        }

        public double S
        {
            get { return this._s; }
            set { this._s = value; }
        }

        public double L
        {
            get { return this._l; }
            set { this._l = value; }
        }

        public bool Equals(HSL hsl)
        {
            return (this.H == hsl.H) && (this.S == hsl.S) && (this.L == hsl.L);
        }

        public RGB ToRGB()
        {
            var hsl = this;
            byte r = 0;
            byte g = 0;
            byte b = 0;

            if (hsl.S == 0)
            {
                r = g = b = (byte)(hsl.L * 255);
            }
            else
            {
                double v1, v2;
                double hue = (float)hsl.H / 360;

                v2 = (hsl.L < 0.5) ? (hsl.L * (1 + hsl.S)) : ((hsl.L + hsl.S) - (hsl.L * hsl.S));
                v1 = 2 * hsl.L - v2;

                r = (byte)(255 * HueToRGB(v1, v2, hue + (1.0f / 3)));
                g = (byte)(255 * HueToRGB(v1, v2, hue));
                b = (byte)(255 * HueToRGB(v1, v2, hue - (1.0f / 3)));
            }

            return new RGB(r, g, b);
        }

        private static double HueToRGB(double v1, double v2, double vH)
        {
            if (vH < 0)
                vH += 1;

            if (vH > 1)
                vH -= 1;

            if ((6 * vH) < 1)
                return (v1 + (v2 - v1) * 6 * vH);

            if ((2 * vH) < 1)
                return v2;

            if ((3 * vH) < 2)
                return (v1 + (v2 - v1) * ((2.0f / 3) - vH) * 6);

            return v1;
        }
    }
    public static class ColorEx
    {
        public static Color FromHex(uint hexValue)
        {
            byte a = (byte)((hexValue & 0xff000000) >> 24);
            byte r = (byte)((hexValue & 0x00ff0000) >> 16);
            byte g = (byte)((hexValue & 0x0000ff00) >> 8);
            byte b = (byte)(hexValue & 0x000000ff);

            return Color.FromArgb(a, r, g, b);
        }

        public static Color FromHex(int hexValue)
        {
            byte a = (byte)((hexValue & 0xff000000) >> 24);
            byte r = (byte)((hexValue & 0x00ff0000) >> 16);
            byte g = (byte)((hexValue & 0x0000ff00) >> 8);
            byte b = (byte)(hexValue & 0x000000ff);

            return Color.FromArgb(a, r, g, b);
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
                s = 0;
            else
                s = delta / v;

            if (s == 0)
                h = 0.0;

            else
            {
                if (rgb.R == v)
                    h = (rgb.G - rgb.B) / delta;
                else if (rgb.G == v)
                    h = 2 + (rgb.B - rgb.R) / delta;
                else if (rgb.B == v)
                    h = 4 + (rgb.R - rgb.G) / delta;

                h *= 60;

                if (h < 0.0)
                    h = h + 360;
            }

            return new HSV(h, s, (v / 255));
        }

        public static double GetLightness(this Color color)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;
            double v;
            double m;
            double vm;
            double r2, g2, b2;

            var h = 0d; // default to black
            var s = 0d;
            var l = 0d;
            v = Math.Max(r, g);
            v = Math.Max(v, b);
            m = Math.Min(r, g);
            m = Math.Min(m, b);

            l = (m + v) / 2.0;

            if (l <= 0.0)
            {
                return l;
            }

            vm = v - m;
            s = vm;

            if (s > 0.0)
            {
                s /= (l <= 0.5) ? (v + m) : (2.0 - v - m);
            }
            else
            {
                return l;
            }

            r2 = (v - r) / vm;
            g2 = (v - g) / vm;
            b2 = (v - b) / vm;

            if (r == v)
            {
                h = (g == m ? 5.0 + b2 : 1.0 - g2);
            }
            else if (g == v)
            {
                h = (b == m ? 1.0 + r2 : 3.0 - b2);
            }
            else
            {
                h = (r == m ? 3.0 + g2 : 5.0 - r2);
            }

            h /= 6.0;

            return l;
        }

        public static Color FromHSL(double h, double sl, double l)
        {
            double v;
            double r, g, b;

            r = l;   // default to gray
            g = l;
            b = l;
            v = (l <= 0.5) ? (l * (1.0 + sl)) : (l + sl - l * sl);

            if (v > 0)
            {
                double m;
                double sv;
                int sextant;
                double fract, vsf, mid1, mid2;

                m = l + l - v;
                sv = (v - m) / v;
                h *= 6.0;
                sextant = (int)h;
                fract = h - sextant;
                vsf = v * sv * fract;
                mid1 = m + vsf;
                mid2 = v - vsf;

                switch (sextant)
                {
                    case 0:
                        r = v;
                        g = mid1;
                        b = m;
                        break;
                    case 1:
                        r = mid2;
                        g = v;
                        b = m;
                        break;
                    case 2:
                        r = m;
                        g = v;
                        b = mid1;
                        break;
                    case 3:
                        r = m;
                        g = mid2;
                        b = v;
                        break;
                    case 4:
                        r = mid1;
                        g = m;
                        b = v;
                        break;
                    case 5:
                        r = v;
                        g = m;
                        b = mid2;
                        break;
                }
            }

            return Color.FromArgb(0xFF,
                Convert.ToByte(r * 255.0f),
                Convert.ToByte(g * 255.0f),
                Convert.ToByte(b * 255.0f));
        }
    }
}
