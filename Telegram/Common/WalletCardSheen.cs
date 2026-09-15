//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Numerics;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Common
{
    /// <summary>
    /// The conic gradient the wallet card is lacquered with.
    /// </summary>
    /// <remarks>
    /// A surface rather than a brush because the gradient is not merely rotated as the card
    /// tilts - see <see cref="Render"/> for why that cannot be a transform.
    ///
    /// Somewhere that only needs the card to sit there, <see cref="Update"/> alone is the whole
    /// job: it paints the resting gradient as soon as it has a size, and nothing has to call
    /// <see cref="Render"/> again unless something moves.
    /// </remarks>
    public sealed partial class WalletCardSheen
    {
        // The surface is generated at a fixed width and stretched, rather than at the card's own
        // pixel size, because the tilt swings it: the sheen rectangle AttachTilt clips is about
        // twice the card so no rotation can swing an empty corner in.
        //
        // A gradient tolerates magnification, having no detail to lose, and the dither
        // below moves each texel by well under one output level, so magnifying it
        // cannot turn the Bayer pattern into a visible weave. Resolution is traded away
        // because the surface is rebuilt every frame the card is held, at a per-texel
        // cost.
        private const int SheenMaxWidth = 360;

        // How far the bands breathe at the card's edge, in octaves of the shaping
        // exponent. Around 1 the widest arms are roughly twice the narrowest, which
        // matches a flat lacquered surface. Much beyond that the dark arms pinch shut.
        private const float SheenBandWidth = 1.1f;

        // Bayer offsets are +/-0.47, and one level of the widest channel (green, 33
        // levels across the ramp) is just under 8 steps of the 256-entry index, placing
        // the dither at about +/-0.5 of an output level.
        private const float SheenDither = 8f;

        // The two ends of the ramp: the card's own colour, and the one the light picks out of it.
        private Vector3 _from;
        private Vector3 _delta;

        // The last tilt this was painted at, so that a colour set mid-gesture repaints where the
        // card is rather than snapping the bands back to rest.
        private Vector2 _pointer;
        private float _amount;

        private byte[] _angles;
        private uint[] _colors;
        private unsafe uint* _pixels;

        /// <summary>
        /// The card as it has always been: #0079FF lacquered with #169AF9, written on in #6DDCFF.
        /// </summary>
        public WalletCardSheen()
            : this(Color.FromArgb(0xFF, 0x00, 0x79, 0xFF), Color.FromArgb(0xFF, 0x16, 0x9A, 0xF9), Color.FromArgb(0xFF, 0x6D, 0xDC, 0xFF))
        {
        }

        /// <summary>
        /// A card of any colour, with its lacquer and its lettering derived from it.
        /// </summary>
        public WalletCardSheen(Color background)
            : this(background, DeriveSheen(background), DeriveAccent(background))
        {
        }

        private WalletCardSheen(Color background, Color sheen, Color accent)
        {
            Apply(background, sheen, accent);
        }

        /// <summary>
        /// The card's own colour, which the gradient starts at. Setting it derives the other two
        /// and repaints, so a card can change colour without being rebuilt - and gives up the
        /// stated pair the parameterless constructor starts with, since from here they are
        /// measured from whatever it is set to.
        /// </summary>
        public Color Background
        {
            get => _background;
            set
            {
                if (value == _background)
                {
                    return;
                }

                Apply(value, DeriveSheen(value), DeriveAccent(value));
            }
        }
        private Color _background;

        /// <summary>What the light picks out of it, which the gradient reaches.</summary>
        public Color Sheen { get; private set; }

        /// <summary>What is written on the card: the currency, and the converted amount.</summary>
        public Color Accent { get; private set; }

        private void Apply(Color background, Color sheen, Color accent)
        {
            _background = background;
            Sheen = sheen;
            Accent = accent;

            _from = new Vector3(background.R, background.G, background.B);
            _delta = new Vector3(sheen.R - background.R, sheen.G - background.G, sheen.B - background.B);

            // Nothing to repaint before there is a surface, and Update paints one as soon as it has
            // a size, so a colour set before then is not lost.
            Render(_pointer, _amount);
        }

        /// <summary>
        /// The lacquer for a background: a few degrees towards cyan, slightly less saturated,
        /// slightly lighter.
        /// </summary>
        /// <remarks>
        /// The shifts are not invented - they are what separates the card's own colours, measured
        /// from them, and in full precision they turn #0079FF into #169AF9 exactly. The hue here is
        /// a whole number, which lands a couple of units off in green, and is why the parameterless
        /// constructor states the colours rather than deriving them.
        /// </remarks>
        private static Color DeriveSheen(Color background)
        {
            var hsl = background.ToHSL();

            hsl.H = (hsl.H + 360 - 6) % 360;
            hsl.S = Math.Max(0, hsl.S - 0.0502);
            hsl.L = Math.Min(1, hsl.L + 0.0314);

            return hsl.ToRGB(background.A);
        }

        /// <summary>
        /// The lettering for a background: the same colour, turned towards cyan and lifted.
        /// </summary>
        /// <remarks>
        /// Measured the same way, from #0079FF to #6DDCFF - and what the measurement says is that
        /// the saturation does not move at all. The card's three colours are one colour at three
        /// lightnesses along a short arc, which is why the accent reads as the same blue rather
        /// than as a second one.
        /// </remarks>
        private static Color DeriveAccent(Color background)
        {
            var hsl = background.ToHSL();

            hsl.H = (hsl.H + 360 - 17) % 360;
            hsl.L = Math.Min(1, hsl.L + 0.2137);

            return hsl.ToRGB(background.A);
        }

        /// <summary>
        /// The surface, or null until <see cref="Update"/> has been given a size. The same
        /// instance for as long as the size holds, so a brush pointed at it stays pointed at it.
        /// </summary>
        public WriteableBitmap Source { get; private set; }

        /// <summary>
        /// Rebuilds the angle map behind the sheen, at the card's own aspect ratio, and paints it
        /// at rest. Returns whether <see cref="Source"/> is a new surface, which is when a brush
        /// has to be pointed at it again.
        ///
        /// Holds the expensive half of the gradient: one Atan2 per texel, reduced to a
        /// position along the ramp. It depends only on the geometry, so it survives
        /// every frame. The tilt varies the shape of the ramp, which is 256 values
        /// rebuilt in <see cref="Render"/>.
        ///
        /// The aspect matters because the map is stretched to fill the card: generated
        /// square, every arm comes out sheared.
        /// </summary>
        public bool Update(Size size)
        {
            if (size.Width <= 0 || size.Height <= 0)
            {
                return false;
            }

            var width = (int)Math.Min(size.Width, SheenMaxWidth);
            var height = (int)(width * (size.Height / size.Width));

            // The card is laid out against a constraint, so it settles at one size
            // and then gets the same size again on every reflow of the list.
            if (width <= 0 || height <= 0 || (Source != null
                && Source.PixelWidth == width && Source.PixelHeight == height))
            {
                return false;
            }

            _angles = new byte[width * height];
            FillConicAngles(_angles, width, height);

            Source = new WriteableBitmap(width, height);

            // Taken once. The blit below runs every frame the card is held, and going
            // back through PixelBuffer would put a WinRT cast, and its allocation, on
            // that path. The buffer belongs to the bitmap and is stable for its
            // lifetime.
            unsafe
            {
                Source.Buffer(out byte* buffer);
                _pixels = (uint*)buffer;
            }

            // At rest, so the card is not blank until the first press - and so that a caller with
            // nothing to animate never has to ask for a frame at all.
            Render(Vector2.Zero, 0);
            return true;
        }

        /// <summary>
        /// Repaints the sheen for one frame of the tilt.
        ///
        /// This part must be a redraw. Rotating the gradient is a rigid transform and
        /// belongs on the compositor, where AttachTilt already performs it at no cost;
        /// redrawing a conic at an angle would produce identical pixels, since a conic
        /// gradient is rotation invariant. Bands that widen and close are not a
        /// transform: the ramp between the two blues changes shape, which requires a
        /// new surface.
        ///
        /// The cost is kept off the per-texel loop rather than off the frame. Every
        /// Atan2 is already in the angle map, so a frame costs 256 calls to Pow plus one
        /// indexed read and one write per texel.
        /// </summary>
        public void Render(Vector2 pointer, float amount)
        {
            _pointer = pointer;
            _amount = amount;

            if (_angles == null)
            {
                return;
            }

            // The same signed horizontal lean, under the same radial clamp, that the
            // sheen's rotation uses, so the bands open towards the side the card is
            // turning rather than drifting out of step with the sweep.
            var lean = pointer.X / MathF.Max(pointer.Length(), 1) * amount;

            // f^k over the triangle ramp. Below 1 the ramp bows up and the light arms
            // spread into the dark ones; above 1 it sags and they close around their
            // peaks. Octaves, so the two directions travel equally.
            var exponent = MathF.Pow(2, SheenBandWidth * lean);

            var colors = _colors ??= new uint[256];

            for (int i = 0; i < 256; i++)
            {
                var f = MathF.Pow(i * (1f / 255f), exponent);

                // Along the ramp between the two. Alpha is opaque throughout, so premultiplied
                // BGRA is just the channels in little-endian order.
                var r = (uint)Math.Clamp(_from.X + _delta.X * f + 0.5f, 0, 255);
                var g = (uint)Math.Clamp(_from.Y + _delta.Y * f + 0.5f, 0, 255);
                var b = (uint)Math.Clamp(_from.Z + _delta.Z * f + 0.5f, 0, 255);

                colors[i] = 0xFF000000u | (r << 16) | (g << 8) | b;
            }

            var angles = _angles;

            unsafe
            {
                var pixels = _pixels;

                for (int i = 0; i < angles.Length; i++)
                {
                    pixels[i] = colors[angles[i]];
                }
            }

            Source.Invalidate();
        }

        private static readonly float[] Bayer4x4 =
        {
            -0.46875f,  0.03125f, -0.34375f,  0.15625f,
             0.28125f, -0.21875f,  0.40625f, -0.09375f,
            -0.28125f,  0.21875f, -0.40625f,  0.09375f,
             0.46875f, -0.03125f,  0.34375f, -0.15625f,
        };

        /// <summary>
        /// Where each texel sits along the ramp of
        ///
        /// conic-gradient(from 20.99deg at 50% 50%, #0079FF 0deg, #169AF9 90deg,
        /// #0079FF 180deg, #169AF9 270deg, #0079FF 360deg)
        ///
        /// quantized to the 256 entries <see cref="Render"/> colours it through.
        /// 0 is #0079FF and 255 is #169AF9. The arms follow from the triangle over the
        /// doubled angle, giving two of each per turn.
        ///
        /// The dither is applied here rather than at colouring time: it belongs to the
        /// position along the ramp, not to the colours, so it survives the per-frame
        /// shaping at no recurring cost.
        /// </summary>
        private static void FillConicAngles(Span<byte> destination, int width, int height)
        {
            if (destination.Length < width * height)
                throw new ArgumentException("Destination too small.", nameof(destination));

            const float PiInv = 1f / MathF.PI;
            const float Phase = 0.38338889f;   // 0.5f - 41.98f / 360f

            float cx = width * 0.5f;
            float cy = height * 0.5f;

            for (int y = 0; y < height; y++)
            {
                float dy = y + 0.5f - cy;
                int bayerRow = (y & 3) << 2;
                int row = y * width;

                for (int x = 0; x < width; x++)
                {
                    float dx = x + 0.5f - cx;

                    float s = MathF.Atan2(dy, dx) * PiInv + Phase;
                    s -= MathF.Floor(s);                    // [0, 1)
                    float f = 1f - MathF.Abs(s * 2f - 1f);  // 0 = #0079FF, 1 = #169AF9

                    float v = f * 255f + Bayer4x4[bayerRow | (x & 3)] * SheenDither;
                    destination[row + x] = (byte)Math.Clamp(v + 0.5f, 0f, 255f);
                }
            }
        }
    }
}
