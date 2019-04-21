using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;

namespace Unigram.ViewModels.Gallery
{
    public class GalleryPhoto : GalleryContent
    {
        private readonly Photo _photo;
        private readonly string _caption;

        public GalleryPhoto(IProtoService protoService, Photo photo)
            : base(protoService)
        {
            _photo = photo;
        }

        public GalleryPhoto(IProtoService protoService, Photo photo, string caption)
            : base(protoService)
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
            return _photo?.GetSmall().Photo;
        }

        public override (File File, string FileName) GetFileAndName()
        {
            var big = _photo.GetBig();
            if (big != null)
            {
                return (big.Photo, null);
            }

            return (null, null);
        }

        public override bool UpdateFile(File file)
        {
            return _photo.UpdateFile(file);
        }

        public override object Constraint => _photo;

        public override string Caption => _caption;

        public override bool HasStickers => _photo.HasStickers;

        public override bool CanCopy => true;
        public override bool CanSave => true;
    }
}
