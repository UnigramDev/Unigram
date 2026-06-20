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
    public partial class GalleryPhoto : GalleryMedia
    {
        private readonly Photo _photo;
        private readonly FormattedText _caption;

        private readonly bool _protect;

        public GalleryPhoto(IClientService clientService, Photo photo, FormattedText caption = null, bool protect = false)
            : base(clientService)
        {
            _photo = photo;
            _caption = caption ?? string.Empty.AsFormattedText();
            _protect = protect;

            File = _photo.GetBig()?.Photo;
            Thumbnail = _photo?.GetSmall()?.Photo;
            Minithumbnail = _photo.Minithumbnail;
        }

        public override object Constraint => _photo;

        public override FormattedText Caption => _caption;

        public override bool HasStickers => _photo.HasStickers;

        public override bool CanBeCopied => !_protect;
        public override bool CanBeSaved => !_protect;
        public override bool CanBeShared => !_protect;

        public override InputMessageContent ToInput()
        {
            var big = _photo.GetBig();
            var small = _photo.GetSmall();

            return new InputMessagePhoto(new InputPhoto(new InputFileId(big.Photo.Id), small?.ToInputThumbnail(), null, Array.Empty<int>(), big.Width, big.Height), null, false, null, false);
        }
    }
}
