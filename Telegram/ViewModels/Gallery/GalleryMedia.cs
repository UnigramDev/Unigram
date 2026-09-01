//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.ViewModels.Gallery
{
    public partial class GalleryMedia
    {
        protected readonly IClientService _clientService;

        // Photo, Video or Animation for the three media constructors, null for the
        // subclasses, which build their own input.
        private readonly object _source;

        protected GalleryMedia(IClientService clientService)
        {
            _clientService = clientService;
        }

        public GalleryMedia(IClientService clientService, Photo photo, FormattedText caption = null, bool protect = false)
        {
            _clientService = clientService;
            _source = photo;

            File = photo.GetBig()?.Photo;
            Thumbnail = photo.GetSmall()?.Photo;
            Minithumbnail = photo.Minithumbnail;

            Constraint = photo;
            Caption = caption ?? string.Empty.AsFormattedText();
            HasStickers = photo.HasStickers;

            CanBeCopied = !protect;
            CanBeSaved = !protect;
            CanBeShared = !protect;
        }

        public GalleryMedia(IClientService clientService, Video video, FormattedText caption = null, bool protect = false)
        {
            _clientService = clientService;
            _source = video;

            File = video.VideoValue;

            if (video.Thumbnail is { Format: ThumbnailFormatJpeg })
            {
                Thumbnail = video.Thumbnail.File;
            }

            Minithumbnail = video.Minithumbnail;

            Constraint = video;
            Caption = caption ?? string.Empty.AsFormattedText();
            HasStickers = video.HasStickers;

            IsVideo = true;
            Duration = video.Duration;

            CanBeSaved = !protect;
            CanBeShared = !protect;
        }

        public GalleryMedia(IClientService clientService, Animation animation, FormattedText caption = null)
        {
            _clientService = clientService;
            _source = animation;

            File = animation.AnimationValue;

            if (animation.Thumbnail is { Format: ThumbnailFormatJpeg })
            {
                Thumbnail = animation.Thumbnail.File;
            }

            Minithumbnail = animation.Minithumbnail;

            Constraint = animation;
            Caption = caption ?? string.Empty.AsFormattedText();

            IsVideo = true;
            IsLoopingEnabled = true;
            Duration = animation.Duration;

            CanBeSaved = true;
            CanBeShared = true;
        }

        public IClientService ClientService => _clientService;

        public RotationAngle RotationAngle { get; set; }

        public File File { get; protected init; }

        public File Thumbnail { get; protected init; }

        public Minithumbnail Minithumbnail { get; protected init; }

        public bool IsHls { get; protected init; }

        public Vector<AlternativeVideo> AlternativeVideos { get; protected init; } = Array.Empty<AlternativeVideo>();

        public object Constraint { get; protected init; }

        public object From { get; protected init; }

        public FormattedText Caption { get; protected init; }

        public int Date { get; protected init; }

        public int Duration { get; protected init; }

        public bool IsPhoto => !IsVideo;

        public bool IsMedia { get; protected init; } = true;

        public bool IsVideo { get; protected init; }
        public bool IsLoopingEnabled { get; protected init; }
        public bool IsVideoNote { get; protected init; }

        public bool HasStickers { get; protected init; }

        public bool CanBeShared { get; protected init; }
        public bool CanBeViewed { get; protected init; }

        public bool CanBeSaved { get; protected init; }
        public bool CanBeCopied { get; protected init; }

        public bool HasProtectedContent { get; protected init; }

        public bool IsPublic { get; protected init; }
        public bool IsPersonal { get; protected init; }

        public bool CanRecognizeText => IsPhoto && !HasProtectedContent;

        public virtual InputMessageContent ToInput()
        {
            switch (_source)
            {
                case Photo photo:
                    var big = photo.GetBig();
                    var small = photo.GetSmall();

                    return new InputMessagePhoto(new InputPhoto(new InputFileId(big.Photo.Id), small?.ToInputThumbnail(), null, Array.Empty<int>(), big.Width, big.Height), null, false, null, false);
                case Video video:
                    return new InputMessageVideo(new InputVideo(new InputFileId(video.VideoValue.Id), video.Thumbnail?.ToInput(), null, 0, Array.Empty<int>(), video.Duration, video.Width, video.Height, video.SupportsStreaming), null, false, null, false);
                case Animation animation:
                    return new InputMessageAnimation(new InputAnimation(new InputFileId(animation.AnimationValue.Id), animation.Thumbnail?.ToInput(), Array.Empty<int>(), animation.Duration, animation.Width, animation.Height), null, false, false);
                default:
                    return null;
            }
        }
    }
}
