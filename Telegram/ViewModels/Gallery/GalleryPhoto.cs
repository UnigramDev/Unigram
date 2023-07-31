//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.ViewModels.Gallery
{
    public class GalleryPhoto : GalleryMedia
    {
        private readonly Photo _photo;
        private readonly string _caption;

        public GalleryPhoto(IClientService clientService, Photo photo)
            : base(clientService)
        {
            _photo = photo;
        }

        public GalleryPhoto(IClientService clientService, Photo photo, string caption)
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

        public override string Caption => _caption;

        public override bool HasStickers => _photo.HasStickers;

        public override bool CanCopy => true;
        public override bool CanSave => true;
        public override bool CanShare => true;

        public override InputMessageContent ToInput()
        {
            var big = _photo.GetBig();
            var small = _photo.GetSmall();

            return new InputMessagePhoto(new InputFileId(big.Photo.Id), small?.ToInputThumbnail(), new int[0], big.Width, big.Height, null, 0, false);
        }
    }
}
