//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.ViewModels.Gallery
{
    public partial class GalleryChatPhoto : GalleryMedia
    {
        private readonly ChatPhoto _photo;

        public GalleryChatPhoto(IClientService clientService, object from, ChatPhoto photo, long messageId = 0, bool isPersonal = false, bool isPublic = false)
            : base(clientService)
        {
            _photo = photo;

            MessageId = messageId;

            IsPersonal = isPersonal;
            IsPublic = isPublic;

            if (photo?.Animation != null)
            {
                File = photo.Animation.File;
            }
            else
            {
                File = photo?.GetBig()?.Photo;
            }

            Thumbnail = photo?.GetSmall()?.Photo;
            Minithumbnail = photo.Minithumbnail;

            From = from;
            Constraint = photo;
            Date = photo.AddedDate;

            IsVideo = photo.Animation != null;
            IsLoopingEnabled = photo.Animation != null;
            Duration = 1;

            HasStickers = photo.Sticker != null;

            CanBeCopied = true;
            CanBeSaved = true;
            CanBeShared = photo.Animation == null;
        }

        public long Id => _photo.Id;

        public long MessageId { get; }

        public ChatPhotoSticker Sticker => _photo.Sticker;

        public override InputMessageContent ToInput()
        {
            var big = _photo.GetBig();
            var small = _photo.GetSmall();

            if (_photo.Animation != null)
            {
                //return new InputMessageAnimation(new InputFileId(_photo.Animation.File.Id), small?.ToInputThumbnail(), Array.Empty<int>(), ?, _photo.Animation.Length, _photo.Animation.Length, null);
            }

            return new InputMessagePhoto(new InputPhoto(new InputFileId(big.Photo.Id), small?.ToInputThumbnail(), null, Array.Empty<int>(), big.Width, big.Height), null, false, null, false);
        }
    }
}
