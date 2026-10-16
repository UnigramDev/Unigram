//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using Telegram.Controls;
using Windows.Foundation;

namespace Telegram.Entities
{
    public partial class VideoGeneration
    {
        public bool Transcode { get; set; }
        public bool Mute { get; set; }
        public uint Width { get; set; }
        public uint Height { get; set; }
        public uint VideoBitrate { get; set; }
        public uint AudioBitrate { get; set; }

        public TimeSpan? TrimStartTime { get; set; }
        public TimeSpan? TrimStopTime { get; set; }

        public bool Transform { get; set; }
        public ImageRotation Rotation { get; set; }
        public ImageFlip Flip { get; set; }

        /// <summary>
        /// The crop as fractions of the frame once turned and flipped, as a photo's edit has it, or
        /// empty for none. Turned by <see cref="Angle"/> about its own centre.
        /// </summary>
        public Rect Rectangle { get; set; }

        /// <summary>
        /// Free rotation in degrees, clockwise, after <see cref="Rotation"/> and <see cref="Flip"/>.
        /// </summary>
        public double Angle { get; set; }

        /// <summary>
        /// A fixed size to encode at, for video notes and profile videos; otherwise the crop's,
        /// fitted to the quality picked or else to the longest side allowed.
        /// </summary>
        public Size OutputSize { get; set; }

        /// <summary>
        /// The crop in pixels of the frame once turned and flipped, for video notes, which crop
        /// by hand rather than through the editor.
        /// </summary>
        public Rect CropRectangle { get; set; }

        /// <summary>
        /// The longest side of the cropped output at the quality picked; 0 for none.
        /// </summary>
        public int MaxSide { get; set; }

        /// <summary>
        /// Drawn over every frame through the frame's own transform: the strokes, then the objects
        /// in the order they were made.
        /// </summary>
        public IReadOnlyList<SmoothPathBuilder> Strokes { get; set; }

        public IReadOnlyList<PaintEntity> Entities { get; set; }

        public bool HasOverlay => Strokes is { Count: > 0 } || Entities is { Count: > 0 };

        public bool HasRectangle => Rectangle.Width > 0 && Rectangle.Height > 0;

        public bool HasOutputSize => HasRectangle || MaxSide > 0;
    }
}
