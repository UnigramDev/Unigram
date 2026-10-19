//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Windows.UI;

namespace Telegram.Controls.Wallet
{
    /// <summary>
    /// What the wallet card wears: its lacquer, its lettering, and a gift's symbols across it.
    /// </summary>
    public sealed class WalletCardStyle
    {
        public WalletCardStyle(Color background, Color sheen, Color accent, Color pattern, float patternOpacity, Color glint, bool isGift)
        {
            Background = background;
            Sheen = sheen;
            Accent = accent;
            Pattern = pattern;
            PatternOpacity = patternOpacity;
            Glint = glint;
            IsGift = isGift;
        }

        // WalletCardSheen's three: the card's colour, what the light picks out of it, and what is
        // written on it.
        public Color Background { get; }
        public Color Sheen { get; }
        public Color Accent { get; }

        // The symbols: a deeper shade of the card, or white on a card too dark to go deeper, and
        // the shade they take where the light catches them.
        public Color Pattern { get; }
        public float PatternOpacity { get; }
        public Color Glint { get; }

        public bool IsGift { get; }
    }

    /// <summary>
    /// The card's colours, for the blue card and for any gift backdrop.
    /// </summary>
    /// <remarks>
    /// The prototype's StickerBoard.paletteFor and drawCardStyle, applied to this card's own
    /// colours instead of its: every tone of the blue card keeps its perceived lightness and
    /// colourfulness (OKLCH L and C) and takes the gift's hue, so a gift's card reads like the
    /// blue one in its own tint, with the white text exactly as readable. Dark backdrops keep their
    /// own depth rather than being lifted to the blue card's lightness.
    ///
    /// The prototype snaps a backdrop to the nearest of a table of Telegram's backdrops first,
    /// because its gifts are made up; real backdrops are used as they are.
    /// </remarks>
    public static class WalletCardMaterials
    {
        // WalletCardSheen's stated colours.
        private const uint CardBackground = 0xFF0079FF;
        private const uint CardSheen = 0xFF169AF9;
        private const uint CardAccent = 0xFF6DDCFF;

        // The prototype's stock card, which the tones are fitted against: its dark and light
        // surface.
        private const uint StockDark = 0xFF088FEE;
        private const uint StockLight = 0xFF16ABF9;

        public static WalletCardStyle Blue { get; } = new WalletCardStyle(
            ToColor(CardBackground), ToColor(CardSheen), ToColor(CardAccent), default, 0, default, false);

        public static WalletCardStyle ForBackdrop(int centerColor, int edgeColor)
        {
            var center = 0xFF000000 | (uint)centerColor;
            var edge = 0xFF000000 | (uint)edgeColor;

            var gift = OklchOf(Blend(center, edge, 0.5));
            var hueDegrees = (gift.H * 180 / Math.PI + 360) % 360;

            // Violets and lilacs read paler than blue at the same chroma: a little more of it, and
            // a touch less lightness, around 300 degrees.
            var violet = Math.Exp(-Math.Pow((hueDegrees - 300) / 38, 2));

            // Muted backdrops stay muted: colourfulness follows the gift's own chroma, times 1.5
            // for the card's glow, up to the stock card's.
            var chroma = Math.Clamp(gift.C * 1.5 / 0.155, 0.15, 1.0) * (1 + 0.18 * violet);
            var lightnessShift = -0.025 * violet;

            var c = OklchOf(center);
            var e = OklchOf(edge);
            var darkness = Math.Clamp((0.6 - (c.L + e.L) / 2) / 0.15, 0, 1);

            var stockDark = OklchOf(StockDark).L;
            var stockLight = OklchOf(StockLight).L;

            uint Tint(uint stock)
            {
                var reference = OklchOf(stock);

                // This role's lightness: where the stock tone sits between the stock dark and light
                // surface, mapped onto the backdrop's edge to centre, a little lifted.
                var position = Math.Clamp((reference.L - stockDark) / (stockLight - stockDark), -2, 3);
                var own = e.L - 0.02 + position * (c.L - e.L);
                var lightness = reference.L + (own - reference.L) * darkness + lightnessShift;

                // Never far more colourful than the backdrop itself: pinks especially read much
                // stronger when pushed.
                var cap = c.C * 1.2 * (reference.C / 0.173);
                var colorfulness = Math.Min(reference.C * chroma, cap) * (1 - darkness) + c.C * 1.1 * darkness;

                return OklchArgb(Math.Clamp(lightness, 0.05, 0.97), colorfulness, gift.H);
            }

            // The light accent must stay clearly light on every card, dark ones included.
            static uint AccentOf(uint color)
            {
                var o = OklchOf(color);
                return o.L >= 0.8 ? color : OklchArgb(0.8, o.C, o.H);
            }

            // drawCardStyle: the symbols a step darker than the surface and a little richer; only a
            // truly black backdrop, with nothing darker to go to, gets white ones, faint.
            var tone = OklchOf(Tint(StockDark));
            var darkCard = e.L < 0.2;
            var pattern = darkCard ? 0xFFFFFFFF : OklchArgb(tone.L * 0.7, tone.C * 1.4, tone.H);
            var glint = darkCard ? 0xFFFFFFFF : OklchArgb(tone.L * 0.86, tone.C * 1.6, tone.H);

            return new WalletCardStyle(
                ToColor(Tint(CardBackground)),
                ToColor(Tint(CardSheen)),
                ToColor(AccentOf(Tint(CardAccent))),
                ToColor(pattern),
                darkCard ? 0.32f : 1,
                ToColor(glint),
                true);
        }

        private readonly struct Oklch
        {
            public Oklch(double l, double c, double h)
            {
                L = l;
                C = c;
                H = h;
            }

            public double L { get; }
            public double C { get; }
            public double H { get; }
        }

        private static double Decode(double v)
        {
            return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }

        private static double Encode(double v)
        {
            return v <= 0.0031308 ? 12.92 * v : 1.055 * Math.Pow(v, 1 / 2.4) - 0.055;
        }

        // Math.Cbrt is not in .NET Native. Sign-preserving, though in-gamut colours never need it.
        private static double Cbrt(double x)
        {
            return x < 0 ? -Math.Pow(-x, 1.0 / 3) : Math.Pow(x, 1.0 / 3);
        }

        private static Oklch OklchOf(uint argb)
        {
            var r = Decode(((argb >> 16) & 0xFF) / 255.0);
            var g = Decode(((argb >> 8) & 0xFF) / 255.0);
            var b = Decode((argb & 0xFF) / 255.0);

            var l = Cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
            var m = Cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
            var s = Cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);

            var lightness = 0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s;
            var a = 1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s;
            var bb = 0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s;

            return new Oklch(lightness, Math.Sqrt(a * a + bb * bb), Math.Atan2(bb, a));
        }

        private static void ToLinear(double lightness, double chroma, double hue, Span<double> rgb)
        {
            var a = chroma * Math.Cos(hue);
            var b = chroma * Math.Sin(hue);

            var l = lightness + 0.3963377774 * a + 0.2158037573 * b;
            var m = lightness - 0.1055613458 * a - 0.0638541728 * b;
            var s = lightness - 0.0894841775 * a - 1.2914855480 * b;

            l = l * l * l;
            m = m * m * m;
            s = s * s * s;

            rgb[0] = 4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s;
            rgb[1] = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s;
            rgb[2] = -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s;
        }

        /// <summary>
        /// To sRGB, lowering chroma until it fits the gamut, which keeps lightness and hue.
        /// </summary>
        private static uint OklchArgb(double lightness, double chroma, double hue)
        {
            Span<double> rgb = stackalloc double[3];
            ToLinear(lightness, chroma, hue, rgb);

            for (int guard = 0; guard < 40 && OutOfGamut(rgb); guard++)
            {
                chroma *= 0.95;
                ToLinear(lightness, chroma, hue, rgb);
            }

            uint result = 0xFF000000;
            for (int i = 0; i < 3; i++)
            {
                var encoded = Encode(Math.Clamp(rgb[i], 0, 1));
                result |= (uint)Math.Clamp((int)(encoded * 255 + 0.5), 0, 255) << (16 - 8 * i);
            }

            return result;
        }

        private static bool OutOfGamut(ReadOnlySpan<double> rgb)
        {
            return rgb[0] < -0.001 || rgb[0] > 1.001 || rgb[1] < -0.001 || rgb[1] > 1.001 || rgb[2] < -0.001 || rgb[2] > 1.001;
        }

        // ColorUtils.blendARGB: each channel truncated, alpha opaque.
        private static uint Blend(uint from, uint to, double ratio)
        {
            uint result = 0xFF000000;
            for (int shift = 16; shift >= 0; shift -= 8)
            {
                var a = (from >> shift) & 0xFF;
                var b = (to >> shift) & 0xFF;
                result |= (uint)(int)(a * (1 - ratio) + b * ratio) << shift;
            }

            return result;
        }

        private static Color ToColor(uint argb)
        {
            return Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
        }
    }
}
