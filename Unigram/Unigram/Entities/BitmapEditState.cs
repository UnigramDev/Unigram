using System;
using System.Collections.Generic;
using Unigram.Controls;
using Windows.Foundation;
using Windows.Graphics.Imaging;

namespace Unigram.Entities
{
    public class BitmapEditState
    {
        public Rect Rectangle { get; set; } = new Rect(0, 0, 1, 1);
        public BitmapProportions Proportions { get; set; } = BitmapProportions.Custom;

        public IReadOnlyList<SmoothPathBuilder> Strokes { get; set; }

        public BitmapRotation Rotation { get; set; } = BitmapRotation.None;
        public BitmapFlip Flip { get; set; }

        public TimeSpan TrimStartTime { get; set; }
        public TimeSpan TrimStopTime { get; set; }

        public bool IsEmpty
        {
            get => (Rectangle.IsEmpty || (Rectangle.X == 0 && Rectangle.Y == 0 && Rectangle.Width == 1 && Rectangle.Height == 1))
                && Strokes == null
                && Rotation == BitmapRotation.None
                && Flip == BitmapFlip.None;
        }
    }
}
