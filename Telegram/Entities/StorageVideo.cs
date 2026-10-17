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

        /// <summary>
        /// The longest side of the cropped output at each quality level, lowest first. No level
        /// scales the crop up.
        /// </summary>
        public static readonly int[] QualitySides = { 480, 854, 1280, 1920 };

        /// <summary>
        /// The crop in pixels of the frame once turned by <paramref name="rotation"/>.
        /// </summary>
        public static Size GetCropSize(double width, double height, Rect rectangle, ImageRotation rotation)
        {
            var quarter = rotation is ImageRotation.Clockwise90Degrees or ImageRotation.Clockwise270Degrees;
            var orientedWidth = quarter ? height : width;
            var orientedHeight = quarter ? width : height;

            if (rectangle.Width <= 0 || rectangle.Height <= 0)
            {
                return new Size(orientedWidth, orientedHeight);
            }

            return new Size(rectangle.Width * orientedWidth, rectangle.Height * orientedHeight);
        }

        public static int GetQualityLevels(Size crop)
        {
            var side = Math.Max(crop.Width, crop.Height);
            return side > 1280 ? 4 : side > 854 ? 3 : side > 640 ? 2 : 1;
        }

        /// <summary>
        /// The output at a level: the crop fitted to its side, in multiples of 4.
        /// </summary>
        public static Size GetQualitySize(Size crop, int level)
        {
            var scale = Math.Min(1, QualitySides[level] / Math.Max(crop.Width, crop.Height));

            return new Size(
                Math.Max(4, Math.Floor(crop.Width * scale / 4) * 4),
                Math.Max(4, Math.Floor(crop.Height * scale / 4) * 4));
        }

        /// <summary>
        /// The level picked in the editor, or null to send the video at its own size. A crop too
        /// small for it takes the highest it offers instead.
        /// </summary>
        public int? Quality { get; set; }

        private const int ProfileSide = 800;
        private const int ProfileBitrate = 1560000;

        private static readonly TimeSpan ProfileDuration = TimeSpan.FromSeconds(9.6);

        /// <summary>
        /// The video as a profile or chat photo: silent, square at a fixed size, and no longer than
        /// a profile video may be. The editor's ellipse keeps the crop square.
        /// </summary>
        public VideoGeneration GetProfileGeneration()
        {
            var state = _editState;
            var conversion = new VideoGeneration
            {
                Mute = true,
                Transcode = true,
                OutputSize = new Size(ProfileSide, ProfileSide),
                VideoBitrate = ProfileBitrate,
                Rectangle = state.Rectangle,
                Angle = state.Angle,
                Rotation = state.Rotation,
                Flip = state.Flip
            };

            var start = state.TrimStartTime;
            var length = state.TrimStopTime > start
                ? state.TrimStopTime - start
                : TimeSpan.FromSeconds(TotalSeconds) - start;

            conversion.TrimStartTime = start;

            if (length > ProfileDuration || state.TrimStopTime > start)
            {
                conversion.TrimStopTime = start + (length < ProfileDuration ? length : ProfileDuration);
            }

            return conversion;
        }

        public VideoGeneration GetGeneration()
        {
            var conversion = new VideoGeneration();
            conversion.Mute = IsMuted;

            var state = _editState;

            if (Quality is int picked)
            {
                var cropSize = state != null
                    ? GetCropSize(Width, Height, state.Rectangle, state.Rotation)
                    : new Size(Width, Height);

                var level = Math.Min(picked, GetQualityLevels(cropSize) - 1);
                if (QualitySides[level] < Math.Max(cropSize.Width, cropSize.Height))
                {
                    conversion.MaxSide = QualitySides[level];
                    conversion.Transcode = true;
                }
            }
            if (state == null)
            {
                return conversion;
            }

            if (state.TrimStartTime != default)
            {
                conversion.TrimStartTime = state.TrimStartTime;
                conversion.Transcode = true;
            }

            if (state.Strokes is { Count: > 0 } || state.Entities is { Count: > 0 })
            {
                conversion.Strokes = state.Strokes;
                conversion.Entities = state.Entities;
                conversion.Transcode = true;
            }

            if (state.TrimStopTime != default)
            {
                conversion.TrimStopTime = state.TrimStopTime;
                conversion.Transcode = true;
            }

            var crop = state.Rectangle;
            if (crop.X != 0 || crop.Y != 0 || crop.Right != 1 || crop.Bottom != 1 || state.Angle != 0 || state.Rotation != ImageRotation.None || state.Flip != ImageFlip.None)
            {
                conversion.Rectangle = crop;
                conversion.Angle = state.Angle;
                conversion.Rotation = state.Rotation;
                conversion.Flip = state.Flip;
                conversion.Transcode = true;
            }

            return conversion;
        }
    }
}
