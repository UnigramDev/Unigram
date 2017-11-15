﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Converters;
using Unigram.Core.Helpers;
using Windows.Foundation;
using Windows.Media.Effects;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml.Media;

namespace Unigram.Models
{
    public class StorageVideo : StorageMedia
    {
        private BasicProperties _basic;

        public StorageVideo(StorageFile file, BasicProperties basic, VideoProperties props, MediaEncodingProfile profile)
            : base(file, basic)
        {
            _fullRectangle = new Rect(0, 0, props.Width, props.Height);

            _basic = basic;
            Properties = props;
            Profile = profile;

            videoDuration = props.Duration.TotalMilliseconds;

            originalSize = (long)basic.Size;
            originalWidth = (int)props.Width;
            originalHeight = (int)props.Height;
            originalBitrate = bitrate = (int)props.Bitrate; //(trackBitrate / 100000 * 100000);

            if (bitrate > 900000)
            {
                bitrate = 900000;
            }

            LoadPreview();
        }

        public override uint Width => Properties.Width;
        public override uint Height => Properties.Height;

        public new static async Task<StorageVideo> CreateAsync(StorageFile file, bool selected)
        {
            try
            {
                var profile = await MediaEncodingProfile.CreateFromFileAsync(file);
                if (profile.Video == null)
                {
                    return null;
                }

                var basic = await file.GetBasicPropertiesAsync();
                var video = await file.Properties.GetVideoPropertiesAsync();

                return new StorageVideo(file, basic, video, profile) { IsSelected = selected };
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
            get
            {
                return _compression;
            }
            set
            {
                Set(ref _compression, value);
            }
        }

        private int _maxCompression = 1;
        public int MaxCompression
        {
            get
            {
                return _maxCompression;
            }
            set
            {
                Set(ref _maxCompression, value);
                RaisePropertyChanged(() => CanCompress);
            }
        }

        private bool _isMuted;
        public bool IsMuted
        {
            get
            {
                return _isMuted;
            }
            set
            {
                Set(ref _isMuted, value);
                Set(() => Compression, ref _compression, 0);

                RaisePropertyChanged(() => CanCompress);
            }
        }

        public bool CanCompress
        {
            get
            {
                return !IsMuted && MaxCompression > 1;
            }
        }

        private async void LoadPreview()
        {
            //_preview = _thumbnail;
            //_preview = await ImageHelper.GetPreviewBitmapAsync(File);
            //RaisePropertyChanged(() => Preview);

            int originalWidth = this.originalWidth;
            int originalHeight = this.originalHeight;

            if (CropRectangle.HasValue)
            {
                originalWidth = (int)CropRectangle.Value.Width;
                originalHeight = (int)CropRectangle.Value.Height;
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

        public override StorageMedia Clone()
        {
            var item = new StorageVideo(File, _basic, Properties, Profile);
            item._thumbnail = _thumbnail;
            item._preview = _preview;

            return item;
        }

        public override void Reset()
        {
            base.Reset();
            Refresh();
        }

        private int originalWidth;
        private int originalHeight;

        private long originalSize;
        private int originalBitrate;

        private int resultWidth;
        private int resultHeight;

        private int rotationValue;

        private int bitrate;
        private long audioFramesSize;
        private long videoFramesSize;
        private double videoDuration;

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
            int originalWidth = this.originalWidth;
            int originalHeight = this.originalHeight;

            if (CropRectangle.HasValue)
            {
                originalWidth = (int)CropRectangle.Value.Width;
                originalHeight = (int)CropRectangle.Value.Height;
            }

            int resultWidth = originalWidth;
            int resultHeight = originalHeight;

            int bitrate = this.originalBitrate;
            long videoFramesSize = 0;
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
                resultWidth = (int)Math.Round(originalWidth * scale / 2) * 2;
                resultHeight = (int)Math.Round(originalHeight * scale / 2) * 2;
                if (bitrate != 0)
                {
                    bitrate = Math.Min(targetBitrate, (int)(originalBitrate / scale));
                    videoFramesSize = (long)(bitrate / 8 * videoDuration / 1000);
                }
            }

            var profile = await MediaEncodingProfile.CreateFromFileAsync(File);
            profile.Video.Width = (uint)resultWidth;
            profile.Video.Height = (uint)resultHeight;
            profile.Video.Bitrate = (uint)bitrate;

            if (_isMuted)
            {
                profile.Audio = null;
            }

            return profile;
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
                width = rotationValue == 90 || rotationValue == 270 ? originalHeight : originalWidth;
                height = rotationValue == 90 || rotationValue == 270 ? originalWidth : originalHeight;
                estimatedSize = (int)(originalSize * ((float)estimatedDuration / videoDuration));
            }
            else
            {
                width = rotationValue == 90 || rotationValue == 270 ? resultHeight : resultWidth;
                height = rotationValue == 90 || rotationValue == 270 ? resultWidth : resultHeight;

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

            String videoDimension = string.Format("{0}x{1}", width, height);
            int minutes = (int)(estimatedDuration / 1000 / 60);
            int seconds = (int)Math.Ceiling(estimatedDuration / 1000d) - minutes * 60;
            String videoTimeSize = string.Format("{0}:{1:D2}, ~{2}", minutes, seconds, FileSizeConverter.Convert(estimatedSize));
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
            var crop = CropRectangle;
            if (crop.HasValue)
            {
                var transform = new VideoTransformEffectDefinition();
                transform.CropRectangle = crop.Value;

                return transform;
            }

            return null;
        }
    }
}
