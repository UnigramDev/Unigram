//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.ViewModels.Gallery
{
    public partial class GalleryAnimation : GalleryMedia
    {
        private readonly Animation _animation;
        private readonly FormattedText _caption;

        public GalleryAnimation(IClientService clientService, Animation animation, FormattedText caption = null)
            : base(clientService)
        {
            _animation = animation;
            _caption = caption ?? new FormattedText(string.Empty, Array.Empty<TextEntity>());

            File = _animation.AnimationValue;

            if (_animation.Thumbnail is { Format: ThumbnailFormatJpeg })
            {
                Thumbnail = _animation.Thumbnail.File;
            }
        }

        public override object Constraint => _animation;

        public override FormattedText Caption => _caption;

        public override bool IsVideo => true;
        public override bool IsLoopingEnabled => true;

        public override bool CanBeSaved => true;
        public override bool CanBeShared => true;

        public override int Duration => _animation.Duration;

        public override InputMessageContent ToInput()
        {
            return new InputMessageAnimation(new InputFileId(_animation.AnimationValue.Id), _animation.Thumbnail?.ToInput(), Array.Empty<int>(), _animation.Duration, _animation.Width, _animation.Height, null, false, false);
        }
    }
}
