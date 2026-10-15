//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Numerics;
using Telegram.Controls;
using Windows.Foundation;
using Windows.UI;

namespace Telegram.Entities
{
    public enum ImageRotation
    {
        None,
        Clockwise90Degrees,
        Clockwise180Degrees,
        Clockwise270Degrees
    }

    public enum ImageFlip
    {
        None,
        Horizontal,
        Vertical
    }

    public enum PaintEntityType
    {
        Text
    }

    /// <summary>
    /// The looks a text can take, in the order its frame button cycles through them.
    /// </summary>
    public enum PaintTextFrame
    {
        Filled,
        Translucent,
        Solid,
        None
    }

    public enum PaintTextAlignment
    {
        Left,
        Center,
        Right
    }

    public enum PaintTextFont
    {
        Regular,
        Italic,
        Serif,
        Condensed,
        Mono,
        Display
    }

    /// <summary>
    /// An object placed over the image. Like strokes it is stored against the upright image, so
    /// cropping or turning the image later never moves it.
    /// </summary>
    public partial class PaintEntity
    {
        public PaintEntityType Type { get; set; }

        /// <summary>
        /// The centre, as fractions of the upright image's size.
        /// </summary>
        public Vector2 Position { get; set; }

        public float Scale { get; set; } = 1;

        /// <summary>
        /// Clockwise, in radians, against the upright image.
        /// </summary>
        public float Rotation { get; set; }

        public string Text { get; set; }

        public Color Color { get; set; }

        public PaintTextFrame Frame { get; set; }

        public PaintTextAlignment Alignment { get; set; }

        public PaintTextFont Font { get; set; }

        /// <summary>
        /// A multiple of the default size, from 0.5 to 2. Unlike <see cref="Scale"/> it changes
        /// where the text wraps.
        /// </summary>
        public float TextSize { get; set; } = 1;

        public PaintEntity Clone()
        {
            return (PaintEntity)MemberwiseClone();
        }
    }

    public partial class ImageGeneration
    {
        public Rect Rectangle { get; set; } = new Rect(0, 0, 1, 1);
        public BitmapProportions Proportions { get; set; } = BitmapProportions.Custom;

        public int MinimumSize { get; set; }

        public IReadOnlyList<SmoothPathBuilder> Strokes { get; set; }

        public IReadOnlyList<PaintEntity> Entities { get; set; }

        public ImageRotation Rotation { get; set; }
        public ImageFlip Flip { get; set; }

        /// <summary>
        /// Free rotation in degrees, clockwise, applied after <see cref="Rotation"/> and
        /// <see cref="Flip"/>. <see cref="Rectangle"/> is then the crop before this rotation,
        /// turning about its own centre, so it may reach outside 0..1.
        /// </summary>
        public double Angle { get; set; }

        /// <summary>
        /// Whether the photo has to be drawn by Win2D, which alone renders a free angle and
        /// objects.
        /// </summary>
        public bool RequiresRender => Angle != 0 || Entities is { Count: > 0 };

        public TimeSpan TrimStartTime { get; set; }
        public TimeSpan TrimStopTime { get; set; }

        public bool IsEmpty
        {
            get => (Rectangle.IsEmpty || (Rectangle.X == 0 && Rectangle.Y == 0 && Rectangle.Width == 1 && Rectangle.Height == 1))
                && Strokes == null
                && Entities == null
                && Rotation == ImageRotation.None
                && Flip == ImageFlip.None
                && Angle == 0;
        }
    }
}
