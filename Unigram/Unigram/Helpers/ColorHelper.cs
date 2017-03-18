using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace Unigram.Helpers
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

        public static HSL RGB2HSL(Color colorToChange)
        {
            double h = 0, s = 0, l = 0;

            double r = colorToChange.R / 255.0;
            double g = colorToChange.G / 255.0;
            double b = colorToChange.B / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));

            if (max == min)
            {
                h = 0;
            }
            else if (max == r && g >= b)
            {
                h = 60.0 * (g - b) / (max - min);
            }
            else if (max == r && g < b)
            {
                h = (60.0 * (g - b) / (max - min)) + 360.0;
            }
            else if (max == g)
            {
                h = (60.0 * (b - r) / (max - min)) + 120.0;
            }
            else if (max == b)
            {
                h = (60.0 * (r - g) / (max - min)) + 240.0;
            }

            l = (max + min) / 2.0;

            if (l == 0 || max == min)
            {
                s = 0;
            }
            else if (l > 0 && l <= 0.5)
            {
                s = (max - min) / (max + min);
            }
            else if (l > 0.5)
            {
                s = (max - min) / (2 - (max + min)); // (max-min > 0)?
            }

            return new HSL(
                double.Parse(string.Format("{0:0.##}", h)),
                double.Parse(string.Format("{0:0.##}", s)),
                double.Parse(string.Format("{0:0.##}", l))
                );
        }

        public static Color HSL2RGB(HSL hsl)
        {
            if (hsl.Saturation == 0)
            {
                return default(Color);
            }
            else
            {
                double h = hsl.Hue;
                double s = hsl.Saturation;
                double l = hsl.Lightness;

                h = Math.Max(0, Math.Min(360, h));
                s = Math.Max(0, Math.Min(1, s));
                l = Math.Max(0, Math.Min(1, l));
                {
                    double q = (l < 0.5) ? (l * (1.0 + s)) : (l + s - (l * s));
                    double p = (2.0 * l) - q;

                    double hk = h / 360.0;
                    double[] t = new double[3];
                    t[0] = hk + (1.0 / 3.0);
                    t[1] = hk;
                    t[2] = hk - (1.0 / 3.0);

                    for (int i = 0; i < 3; i++)
                    {
                        if (t[i] < 0)
                        {
                            t[i] += 1.0;
                        }

                        if (t[i] > 1)
                        {
                            t[i] -= 1.0;
                        }

                        if ((t[i] * 6) < 1)
                        {
                            t[i] = p + ((q - p) * 6.0 * t[i]);
                        }
                        else if ((t[i] * 2.0) < 1)
                        {
                            t[i] = q;
                        }
                        else if ((t[i] * 3.0) < 2)
                        {
                            t[i] = p + ((q - p) * ((2.0 / 3.0) - t[i]) * 6.0);
                        }
                        else
                        {
                            t[i] = p;
                        }
                    }

                    return Color.FromArgb(
                        0xFF,
                        (byte)Convert.ToInt32(double.Parse(string.Format("{0:0.00}", t[0] * 255.0))),
                        (byte)Convert.ToInt32(double.Parse(string.Format("{0:0.00}", t[1] * 255.0))),
                        (byte)Convert.ToInt32(double.Parse(string.Format("{0:0.00}", t[2] * 255.0))));
                }
            }
        }

        // Change the shade of a given color by adding a value to the lightness.
        public static Color ChangeShade(Color colorToChange, float value)
        {
            // Convert color to HSL
            HSL temp = RGB2HSL(colorToChange);

            // Change the lightness value
            temp.Lightness += value;

            // Check that light it still in range [0,1],
            // else correct it
            if (temp.Lightness > 1)
            {
                temp.Lightness = 1;
            }
            else if (temp.Lightness < 0)
            {
                temp.Lightness = 0;
            }

            // Return the new RGB Color
            return HSL2RGB(temp);
        }

        public struct HSL
        {
            public double Hue;
            public double Saturation;
            public double Lightness;

            public HSL(double hue, double saturation, double lightness)
            {
                System.Diagnostics.Contracts.Contract.Assert(hue >= 0 && hue <= 360, "hue range");
                System.Diagnostics.Contracts.Contract.Assert(saturation >= 0 && saturation <= 255, "saturation range");
                System.Diagnostics.Contracts.Contract.Assert(lightness >= 0 && lightness <= 255, "lightness range");

                Hue = hue;
                Saturation = saturation;
                Lightness = lightness;
            }
        }

        public struct RGB
        {
            public float Red;
            public float Green;
            public float Blue;

            public RGB(float red, float green, float blue)
            {
                System.Diagnostics.Contracts.Contract.Assert(red >= 0 && red <= 255, "red range");
                System.Diagnostics.Contracts.Contract.Assert(green >= 0 && green <= 255, "greeen range");
                System.Diagnostics.Contracts.Contract.Assert(blue >= 0 && blue <= 255, "blue range");

                Red = red;
                Green = green;
                Blue = blue;
            }
        }
    }
}