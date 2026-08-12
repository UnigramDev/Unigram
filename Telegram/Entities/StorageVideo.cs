//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading.Tasks;
using Telegram.Native;
using Windows.Foundation;
using Windows.Storage;

namespace Telegram.Entities
{
    public partial class StorageVideo : StorageMedia
    {
        private StorageVideo(StorageFile file, ulong fileSize, double totalMilliseconds, int width, int height)
            : base(file, fileSize)
        {
            TotalSeconds = (int)Math.Floor(totalMilliseconds / 1000);

            Width = width;
            Height = height;
        }

        public override int Width { get; }
        public override int Height { get; }

        public static async Task<StorageVideo> CreateAsync(StorageFile file, ulong fileSize)
        {
            try
            {
                using var stream = await file.OpenReadAsync();
                using var animation = await Task.Run(() => VideoAnimation.LoadFromFile(new VideoAnimationStreamSource(stream), true, false, true));

                if (animation != null && animation.Duration > 0 && animation.PixelWidth > 0 && animation.PixelHeight > 0)
                {
                    var width = animation.PixelWidth;
                    var height = animation.PixelHeight;

                    switch (animation.Rotation)
                    {
                        case 90:
                        case 270:
                            width = animation.PixelHeight;
                            height = animation.PixelWidth;
                            break;
                    }

                    return new StorageVideo(file, fileSize, animation.Duration, width, height);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        public int TotalSeconds { get; }

        public string Duration
        {
            get
            {
                var duration = TimeSpan.FromSeconds(TotalSeconds);
                if (duration.TotalHours >= 1)
                {
                    return duration.ToString("h\\:mm\\:ss");
                }
                else
                {
                    return duration.ToString("mm\\:ss");
                }
            }
        }

        private bool _isMuted;
        public bool IsMuted
        {
            get => _isMuted;
            set => Set(ref _isMuted, value);
        }

        public VideoGeneration GetGeneration()
        {
            var conversion = new VideoGeneration();
            conversion.Mute = IsMuted;

            var state = _editState;
            if (state == null)
            {
                return conversion;
            }

            if (state.TrimStartTime != default)
            {
                conversion.TrimStartTime = state.TrimStartTime;
                conversion.Transcode = true;
            }

            if (state.TrimStopTime != default)
            {
                conversion.TrimStopTime = state.TrimStopTime;
                conversion.Transcode = true;
            }

            var crop = state.Rectangle;
            if (crop.X != 0 || crop.Y != 0 || crop.Right != 1 || crop.Bottom != 1)
            {
                var x = Math.Floor(crop.X * Width);
                var y = Math.Floor(crop.Y * Height);
                var width = Math.Ceiling(crop.Width * Width);
                var height = Math.Ceiling(crop.Height * Height);

                width -= (width % 4);
                height -= (height % 4);

                conversion.CropRectangle = new Rect(x, y, width, height);
                conversion.OutputSize = new Size(width, height);
                conversion.Transcode = true;
                conversion.Transform = true;
            }

            return conversion;
        }
    }
}
