using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;

namespace Unigram.ViewModels.Gallery
{
    public class GalleryAnimation : GalleryContent
    {
        private readonly Animation _animation;
        private readonly string _caption;

        public GalleryAnimation(IProtoService protoService, Animation animation)
            : base(protoService)
        {
            _animation = animation;
        }

        public GalleryAnimation(IProtoService protoService, Animation animation, string caption)
            : base(protoService)
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
                return _animation.Thumbnail?.File;
            }

            return null;
        }

        public override (File File, string FileName) GetFileAndName()
        {
            return (_animation.AnimationValue, _animation.FileName);
        }

        public override bool UpdateFile(File file)
        {
            return _animation.UpdateFile(file);
        }

        public override object Constraint => _animation;

        public override string Caption => _caption;

        public override bool IsVideo => true;
        public override bool IsLoop => true;

        public override bool CanSave => true;

        public override int Duration => _animation.Duration;

        public override string MimeType => _animation.MimeType;
    }
}
