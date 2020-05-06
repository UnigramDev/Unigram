using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Controls;
using Windows.Foundation;
using Windows.Graphics.Imaging;

namespace Unigram.Entities
{
    public class BitmapEditState
    {
        public Rect? Rectangle { get; set; }
        public BitmapProportions Proportions { get; set; } = BitmapProportions.Custom;

        public IReadOnlyList<SmoothPathBuilder> Strokes { get; set; }

        public BitmapRotation Rotation { get; set; } = BitmapRotation.None;
        public BitmapFlip Flip { get; set; }

        public bool IsEmpty
        {
            get => Rectangle == null
                && Strokes == null
                && Rotation == BitmapRotation.None
                && Flip == BitmapFlip.None;
        }
    }
}
