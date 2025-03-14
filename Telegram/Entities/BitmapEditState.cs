//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using Telegram.Controls;
using Windows.Foundation;
using Windows.Graphics.Imaging;

namespace Telegram.Entities
{
    public partial class BitmapEditState
    {
        public Rect Rectangle { get; set; } = new Rect(0, 0, 1, 1);
        public BitmapProportions Proportions { get; set; } = BitmapProportions.Custom;

        public int MinimumSize { get; set; }

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
