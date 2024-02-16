//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Converters;
using Windows.Foundation;
using Windows.Media.Effects;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace Telegram.Entities
{
    public class StorageVideo : StorageMedia
    {
        public StorageVideo(StorageFile file, BasicProperties basic, VideoProperties props, MediaEncodingProfile profile)
            : base(file, basic)
        {
            Properties = props;
            Profile = profile;

            videoDuration = props.Duration.TotalMilliseconds;

            originalSize = (long)basic.Size;
            originalWidth = (int)props.GetWidth();
            originalHeight = (int)props.GetHeight();
            originalBitrate = bitrate = (int)props.Bitrate; //(trackBitrate / 100000 * 100000);

            if (bitrate > 900000)
            {
                bitrate = 900000;
            }

            LoadPreview();

            if (profile.Video.Width > 0 && profile.Video.Height > 0)
            {
                Width = profile.Video.Width;
                Height = profile.Video.Height;
            }
            else
            {
                Width = props.GetWidth();
                Height = props.GetHeight();
            }
        }

        public override uint Width { get; }
        public override uint Height { get; }

        public static async Task<StorageVideo> CreateAsync(StorageFile file, BasicProperties basic)
        {
            try
            {
                if (!file.IsAvailable)
                {
                    return null;
                }

                var profile = await MediaEncodingProfile.CreateFromFileAsync(file);
                if (profile.Video == null)
                {
                    return null;
                }

                var video = await file.Properties.GetVideoPropertiesAsync();
                if ((video.Width > 0 && video.Height > 0) || (profile.Video.Width > 0 && profile.Video.Height > 0))
                {
                    return new StorageVideo(file, basic, video, profile);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        public VideoProperties Properties { get; private set; }

        public MediaEncodingProfile Profile { get; private set; }

        public override void Refresh()
        {
            base.Refresh();
            LoadPreview();
        }

        public string Duration
        {
            get
            {
                var duration = Properties.Duration;
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

        private int _compression;
        public int Compression
        {
            get => _compression;
            set => Set(ref _compression, value);
        }

        private int _maxCompression = 1;
        public int MaxCompression
        {
            get => _maxCompression;
            set
            {
                Set(ref _maxCompression, value);
                RaisePropertyChanged(nameof(CanCompress));
            }
        }

        private bool _isMuted;
        public bool IsMuted
        {
            get => _isMuted;
            set
            {
                Set(ref _isMuted, value);
                Set(ref _compression, 0, nameof(Compression));

                RaisePropertyChanged(nameof(CanCompress));
            }
        }

        public bool CanCompress
        {
            get
            {
                return !IsMuted && MaxCompression > 1;
            }
        }

        private void LoadPreview()
        {
            //_preview = _thumbnail;
            //_preview = await ImageHelper.GetPreviewBitmapAsync(File);
            //RaisePropertyChanged(() => Preview);

            int originalWidth = this.originalWidth;
            int originalHeight = this.originalHeight;

            if (_editState is BitmapEditState state && state.Rectangle is Rect rectangle)
            {
                originalWidth = (int)rectangle.Width;
                originalHeight = (int)rectangle.Height;
            }

            Compression = 6;

            if (originalWidth > 1280 || originalHeight > 1280)
            {
                MaxCompression = 5;
            }
            else if (originalWidth > 848 || originalHeight > 848)
            {
                MaxCompression = 4;
            }
            else if (originalWidth > 640 || originalHeight > 640)
            {
                MaxCompression = 3;
            }
            else if (originalWidth > 480 || originalHeight > 480)
            {
                MaxCompression = 2;
            }
            else
            {
                MaxCompression = 1;
            }

            if (Compression >= MaxCompression)
            {
                Compression = MaxCompression - 1;
            }
        }

        private readonly int originalWidth;
        private readonly int originalHeight;

        private readonly long originalSize;
        private readonly int originalBitrate;

        private int resultWidth;
        private int resultHeight;

        private readonly int rotationValue;

        private int bitrate;
        private readonly long audioFramesSize;
        private long videoFramesSize;
        private readonly double videoDuration;

        private int estimatedSize;
        private long estimatedDuration;

        private long startTime;
        private long endTime;

        public override string ToString()
        {
            return ToString(_compression);
        }

        public async Task<MediaEncodingProfile> GetEncodingAsync()
        {
            int originalWidth = (int)Properties.Width;
            int originalHeight = (int)Properties.Height;

            if (_editState is BitmapEditState state && state.Rectangle is Rect rectangle)
            {
                originalWidth = (int)rectangle.Width;
                originalHeight = (int)rectangle.Height;
            }

            int bitrate = originalBitrate;
            double videoDuration = 0;

            var selectedCompression = _compression;
            var compressionsCount = _maxCompression;

            if (selectedCompression != compressionsCount - 1)
            {
                float maxSize;
                int targetBitrate;
                switch (selectedCompression)
                {
                    case 0:
                        maxSize = 432.0f;
                        targetBitrate = 400000;
                        break;
                    case 1:
                        maxSize = 640.0f;
                        targetBitrate = 900000;
                        break;
                    case 2:
                        maxSize = 848.0f;
                        targetBitrate = 1100000;
                        break;
                    case 3:
                    default:
                        targetBitrate = 1600000;
                        maxSize = 1280.0f;
                        break;
                }

                float scale = originalWidth > originalHeight ? maxSize / originalWidth : maxSize / originalHeight;
                int resultWidth = (int)Math.Round(originalWidth * scale / 2) * 2;
                int resultHeight = (int)Math.Round(originalHeight * scale / 2) * 2;
                if (bitrate != 0)
                {
                    bitrate = Math.Min(targetBitrate, (int)(originalBitrate / scale));
                    long videoFramesSize = (long)(bitrate / 8 * videoDuration / 1000);
                }
            }

            try
            {
                var profile = await MediaEncodingProfile.CreateFromFileAsync(File);
                //profile.Video.Width = (uint)resultWidth;
                //profile.Video.Height = (uint)resultHeight;
                //profile.Video.Bitrate = (uint)bitrate;

                if (_isMuted)
                {
                    profile.Audio = null;
                }

                return profile;
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block

                // TODO: what is the user wanted to mute the audio?
                return null;
            }
        }

        public string ToString(int compression)
        {
            UpdateWidthHeightBitrateForCompression(compression);

            var selectedCompression = compression;
            var compressionsCount = _maxCompression;

            //estimatedDuration = (long)Math.ceil((videoTimelineView.getRightProgress() - videoTimelineView.getLeftProgress()) * videoDuration);
            estimatedDuration = (long)videoDuration;

            int width;
            int height;

            if (/*compressItem.getTag() == null ||*/ selectedCompression == compressionsCount - 1)
            {
                width = rotationValue is 90 or 270 ? originalHeight : originalWidth;
                height = rotationValue is 90 or 270 ? originalWidth : originalHeight;
                estimatedSize = (int)(originalSize * ((float)estimatedDuration / videoDuration));
            }
            else
            {
                width = rotationValue is 90 or 270 ? resultHeight : resultWidth;
                height = rotationValue is 90 or 270 ? resultWidth : resultHeight;

                estimatedSize = (int)((audioFramesSize + videoFramesSize) * ((float)estimatedDuration / videoDuration));
                estimatedSize += estimatedSize / (32 * 1024) * 16;
            }

            //if (videoTimelineView.getLeftProgress() == 0)
            {
                startTime = -1;
            }
            //else
            //{
            //    startTime = (long)(videoTimelineView.getLeftProgress() * videoDuration) * 1000;
            //}
            //if (videoTimelineView.getRightProgress() == 1)
            {
                endTime = -1;
            }
            //else
            //{
            //    endTime = (long)(videoTimelineView.getRightProgress() * videoDuration) * 1000;
            //}

            var videoDimension = string.Format("{0}x{1}", width, height);
            int minutes = (int)(estimatedDuration / 1000 / 60);
            int seconds = (int)Math.Ceiling(estimatedDuration / 1000d) - minutes * 60;
            var videoTimeSize = string.Format("{0}:{1:D2}, ~{2}", minutes, seconds, FileSizeConverter.Convert(estimatedSize));
            return string.Format("{0}, {1}", videoDimension, videoTimeSize);
        }

        private void UpdateWidthHeightBitrateForCompression(int compression)
        {
            var selectedCompression = compression;
            var compressionsCount = _maxCompression;

            if (selectedCompression != compressionsCount - 1)
            {
                float maxSize;
                int targetBitrate;
                switch (selectedCompression)
                {
                    case 0:
                        maxSize = 432.0f;
                        targetBitrate = 400000;
                        break;
                    case 1:
                        maxSize = 640.0f;
                        targetBitrate = 900000;
                        break;
                    case 2:
                        maxSize = 848.0f;
                        targetBitrate = 1100000;
                        break;
                    case 3:
                    default:
                        targetBitrate = 1600000;
                        maxSize = 1280.0f;
                        break;
                }

                float scale = originalWidth > originalHeight ? maxSize / originalWidth : maxSize / originalHeight;
                resultWidth = (int)Math.Round(originalWidth * scale / 2) * 2;
                resultHeight = (int)Math.Round(originalHeight * scale / 2) * 2;
                if (bitrate != 0)
                {
                    bitrate = Math.Min(targetBitrate, (int)(originalBitrate / scale));
                    videoFramesSize = (long)(bitrate / 8 * videoDuration / 1000);
                }
            }
        }

        public VideoTransformEffectDefinition GetTransform()
        {
            var crop = _editState?.Rectangle ?? Rect.Empty;
            if (!(crop.IsEmpty || (crop.X == 0 && crop.Y == 0 && crop.Width == 1 && crop.Height == 1)))
            {
                var transform = new VideoTransformEffectDefinition();
                transform.CropRectangle = crop;

                return transform;
            }

            return null;
        }
    }
}
