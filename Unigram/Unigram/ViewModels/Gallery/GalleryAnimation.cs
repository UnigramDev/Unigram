using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;

namespace Unigram.ViewModels.Gallery
{
    public class GalleryAnimation : GalleryContent
    {
        private readonly Animation _animation;
        private readonly string _caption;

        public GalleryAnimation(IClientService clientService, Animation animation)
            : base(clientService)
        {
            _animation = animation;
        }

        public GalleryAnimation(IClientService clientService, Animation animation, string caption)
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

        public override string Caption => _caption;

        public override bool IsVideo => true;
        public override bool IsLoop => true;

        public override bool CanSave => true;
        public override bool CanShare => true;

        public override int Duration => _animation.Duration;

        public override string MimeType => _animation.MimeType;

        public override InputMessageContent ToInput()
        {
            return new InputMessageAnimation(new InputFileId(_animation.AnimationValue.Id), _animation.Thumbnail?.ToInput(), new int[0], _animation.Duration, _animation.Width, _animation.Height, null, false);
        }
    }
}
