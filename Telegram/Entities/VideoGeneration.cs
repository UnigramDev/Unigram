//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
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
        public Size OutputSize { get; set; }
        public Rect CropRectangle { get; set; }
    }
}
