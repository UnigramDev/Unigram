using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace Unigram.Core.Helpers
{
    // ColorHelper is a set of color conversion utilities
    public static class ColorsHelper
    {
        public static Color AlphaBlend(Color color1, Color color2)
        {
            return AlphaBlend(color1, color2, color2.A);
        }

        public static Color AlphaBlend(Color color1, Color color2, byte alpha)
        {
            float factor = alpha / 255f;
            byte red = (byte)(color1.R * (1 - factor) + color2.R * factor);
            byte green = (byte)(color1.G * (1 - factor) + color2.G * factor);
            byte blue = (byte)(color1.B * (1 - factor) + color2.B * factor);
            return Color.FromArgb(0xFF, red, green, blue);
        }

        public static double[] RgbToHsv(byte r, byte g, byte b)
        {
            double rf = r / 255.0;
            double gf = g / 255.0;
            double bf = b / 255.0;
            double max = (rf > gf && rf > bf) ? rf : (gf > bf) ? gf : bf;
            double min = (rf < gf && rf < bf) ? rf : (gf < bf) ? gf : bf;
            double h, s;
            double d = max - min;
            s = max == 0 ? 0 : d / max;
            if (max == min)
            {
                h = 0;
            }
            else
            {
                if (rf > gf && rf > bf)
                {
                    h = (gf - bf) / d + (gf < bf ? 6 : 0);
                }
                else if (gf > bf)
                {
                    h = (bf - rf) / d + 2;
                }
                else
                {
                    h = (rf - gf) / d + 4;
                }
                h /= 6;
            }
            return new double[] { h, s, max };
        }

        public static byte[] HsvToRgb(double h, double s, double v)
        {
            double r = 0, g = 0, b = 0;
            double i = (int)Math.Floor(h * 6);
            double f = h * 6 - i;
            double p = v * (1 - s);
            double q = v * (1 - f * s);
            double t = v * (1 - (1 - f) * s);
            switch ((int)i % 6)
            {
                case 0:
                    r = v;
                    g = t;
                    b = p;
                    break;
                case 1:
                    r = q;
                    g = v;
                    b = p;
                    break;
                case 2:
                    r = p;
                    g = v;
                    b = t;
                    break;
                case 3:
                    r = p;
                    g = q;
                    b = v;
                    break;
                case 4:
                    r = t;
                    g = p;
                    b = v;
                    break;
                case 5:
                    r = v;
                    g = p;
                    b = q;
                    break;
            }
            return new byte[] { (byte)(r * 255), (byte)(g * 255), (byte)(b * 255) };
        }
    }
}