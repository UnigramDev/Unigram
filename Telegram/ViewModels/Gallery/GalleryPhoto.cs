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
    public class GalleryPhoto : GalleryMedia
    {
        private readonly Photo _photo;
        private readonly FormattedText _caption;

        private readonly bool _protect;

        public GalleryPhoto(IClientService clientService, Photo photo, bool protect = false)
            : base(clientService)
        {
            _photo = photo;
            _protect = protect;
        }

        public GalleryPhoto(IClientService clientService, Photo photo, FormattedText caption, bool protect = false)
            : base(clientService)
        {
            _photo = photo;
            _caption = caption;
            _protect = protect;
        }

        public override File GetFile()
        {
            return _photo.GetBig()?.Photo;
        }

        public override File GetThumbnail()
        {
            return _photo?.GetSmall()?.Photo;
        }

        public override object Constraint => _photo;

        public override FormattedText Caption => _caption;

        public override bool HasStickers => _photo.HasStickers;

        public override bool CanCopy => !_protect;
        public override bool CanSave => !_protect;
        public override bool CanShare => !_protect;

        public override InputMessageContent ToInput()
        {
            var big = _photo.GetBig();
            var small = _photo.GetSmall();

            return new InputMessagePhoto(new InputFileId(big.Photo.Id), small?.ToInputThumbnail(), Array.Empty<int>(), big.Width, big.Height, null, false, null, false);
        }
    }
}
