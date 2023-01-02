//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;

namespace Unigram.ViewModels.Gallery
{
    public class GalleryVideo : GalleryContent
    {
        private readonly Video _video;
        private readonly string _caption;

        public GalleryVideo(IClientService clientService, Video video)
            : base(clientService)
        {
            _video = video;
        }

        public GalleryVideo(IClientService clientService, Video video, string caption)
            : base(clientService)
        {
            _video = video;
            _caption = caption;
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

        public override string Caption => _caption;

        public override bool HasStickers => _video.HasStickers;

        public override bool IsVideo => true;

        public override bool CanSave => true;
        public override bool CanShare => true;

        public override int Duration => _video.Duration;

        public override string MimeType => _video.MimeType;

        public override InputMessageContent ToInput()
        {
            return new InputMessageVideo(new InputFileId(_video.VideoValue.Id), _video.Thumbnail?.ToInput(), new int[0], _video.Duration, _video.Width, _video.Height, _video.SupportsStreaming, null, 0, false);
        }
    }
}
