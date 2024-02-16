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
    public class GalleryAnimation : GalleryMedia
    {
        private readonly Animation _animation;
        private readonly FormattedText _caption;

        public GalleryAnimation(IClientService clientService, Animation animation)
            : base(clientService)
        {
            _animation = animation;
        }

        public GalleryAnimation(IClientService clientService, Animation animation, FormattedText caption)
            : base(clientService)
        {
            _animation = animation;
            _caption = caption;
        }

        //public long Id => _animation.Id;

        public override File GetFile()
        {
            return _animation.AnimationValue;
        }

        public override File GetThumbnail()
        {
            if (_animation.Thumbnail?.Format is ThumbnailFormatJpeg)
            {
                return _animation.Thumbnail.File;
            }

            return null;
        }

        public override object Constraint => _animation;

        public override FormattedText Caption => _caption;

        public override bool IsVideo => true;
        public override bool IsLoop => true;

        public override bool CanSave => true;
        public override bool CanShare => true;

        public override int Duration => _animation.Duration;

        public override string MimeType => _animation.MimeType;

        public override InputMessageContent ToInput()
        {
            return new InputMessageAnimation(new InputFileId(_animation.AnimationValue.Id), _animation.Thumbnail?.ToInput(), Array.Empty<int>(), _animation.Duration, _animation.Width, _animation.Height, null, false);
        }
    }
}
