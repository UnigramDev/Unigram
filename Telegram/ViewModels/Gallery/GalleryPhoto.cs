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

        public GalleryPhoto(IClientService clientService, Photo photo)
            : base(clientService)
        {
            _photo = photo;
        }

        public GalleryPhoto(IClientService clientService, Photo photo, FormattedText caption)
            : base(clientService)
        {
            _photo = photo;
            _caption = caption;
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

        public override bool CanCopy => true;
        public override bool CanSave => true;
        public override bool CanShare => true;

        public override InputMessageContent ToInput()
        {
            var big = _photo.GetBig();
            var small = _photo.GetSmall();

            return new InputMessagePhoto(new InputFileId(big.Photo.Id), small?.ToInputThumbnail(), Array.Empty<int>(), big.Width, big.Height, null, null, false);
        }
    }
}
