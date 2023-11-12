//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.ViewModels.Gallery
{
    public class GalleryChatPhoto : GalleryMedia
    {
        private readonly object _from;
        private readonly ChatPhoto _photo;

        private readonly long _messageId;

        public GalleryChatPhoto(IClientService clientService, object from, ChatPhoto photo, long messageId = 0, bool isPersonal = false, bool isPublic = false)
            : base(clientService)
        {
            _from = from;
            _photo = photo;
            _messageId = messageId;

            IsPersonal = isPersonal;
            IsPublic = isPublic;
        }

        public long Id => _photo.Id;

        public long MessageId => _messageId;

        public override File GetFile()
        {
            if (_photo?.Animation != null)
            {
                return _photo.Animation.File;
            }

            return _photo?.GetBig()?.Photo;
        }

        public override File GetThumbnail()
        {
            return _photo?.GetSmall()?.Photo;
        }

        public ChatPhotoSticker Sticker => _photo.Sticker;

        public override bool IsVideo => _photo.Animation != null;
        public override bool IsLoop => _photo.Animation != null;

        public override bool HasStickers => _photo.Sticker != null;

        public override int Duration => 1;

        public override string MimeType => "video/mp4";

        public override object From => _from;

        public override object Constraint => _photo;

        public override int Date => _photo.AddedDate;

        public override bool CanCopy => true;
        public override bool CanSave => true;
        public override bool CanShare => _photo.Animation == null;

        public override InputMessageContent ToInput()
        {
            var big = _photo.GetBig();
            var small = _photo.GetSmall();

            if (_photo.Animation != null)
            {
                //return new InputMessageAnimation(new InputFileId(_photo.Animation.File.Id), small?.ToInputThumbnail(), Array.Empty<int>(), ?, _photo.Animation.Length, _photo.Animation.Length, null);
            }

            return new InputMessagePhoto(new InputFileId(big.Photo.Id), small?.ToInputThumbnail(), Array.Empty<int>(), big.Width, big.Height, null, null, false);
        }
    }
}
