//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.ViewModels.Gallery
{
    public class GalleryVideo : GalleryMedia
    {
        private readonly Video _video;
        private readonly FormattedText _caption;
        private readonly bool _protect;

        public GalleryVideo(IClientService clientService, Video video, bool protect = false)
            : base(clientService)
        {
            _video = video;
            _protect = protect;
        }

        public GalleryVideo(IClientService clientService, Video video, FormattedText caption, bool protect = false)
            : base(clientService)
        {
            _video = video;
            _caption = caption;
            _protect = protect;
        }

        //public long Id => _video.Id;

        public override File GetFile()
        {
            return _video.VideoValue;
        }

        public override File GetThumbnail()
        {
            if (_video.Thumbnail?.Format is ThumbnailFormatJpeg)
            {
                return _video.Thumbnail?.File;
            }

            return null;
        }

        public override object Constraint => _video;

        public override FormattedText Caption => _caption;

        public override bool HasStickers => _video.HasStickers;

        public override bool IsVideo => true;

        public override bool CanSave => !_protect;
        public override bool CanShare => !_protect;

        public override int Duration => _video.Duration;

        public override InputMessageContent ToInput()
        {
            return new InputMessageVideo(new InputFileId(_video.VideoValue.Id), _video.Thumbnail?.ToInput(), Array.Empty<int>(), _video.Duration, _video.Width, _video.Height, _video.SupportsStreaming, null, false, null, false);
        }
    }
}
