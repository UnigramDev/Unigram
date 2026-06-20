//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.ViewModels.Gallery
{
    public partial class GalleryVideo : GalleryMedia
    {
        private readonly Video _video;
        private readonly FormattedText _caption;
        private readonly bool _protect;

        public GalleryVideo(IClientService clientService, Video video, FormattedText caption = null, bool protect = false)
            : base(clientService)
        {
            _video = video;
            _caption = caption ?? string.Empty.AsFormattedText();
            _protect = protect;

            File = _video.VideoValue;

            if (_video.Thumbnail is { Format: ThumbnailFormatJpeg })
            {
                Thumbnail = _video.Thumbnail.File;
            }

            Minithumbnail = _video.Minithumbnail;
        }

        public override object Constraint => _video;

        public override FormattedText Caption => _caption;

        public override bool HasStickers => _video.HasStickers;

        public override bool IsVideo => true;

        public override bool CanBeSaved => !_protect;
        public override bool CanBeShared => !_protect;

        public override int Duration => _video.Duration;

        public override InputMessageContent ToInput()
        {
            return new InputMessageVideo(new InputVideo(new InputFileId(_video.VideoValue.Id), _video.Thumbnail?.ToInput(), null, 0, Array.Empty<int>(), _video.Duration, _video.Width, _video.Height, _video.SupportsStreaming), null, false, null, false);
        }
    }
}
