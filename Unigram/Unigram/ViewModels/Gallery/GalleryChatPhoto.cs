using Telegram.Td.Api;
using Unigram.Services;

namespace Unigram.ViewModels.Gallery
{
    public class GalleryChatPhoto : GalleryContent
    {
        private readonly ChatPhotoInfo _photo;
        private readonly string _caption;

        public GalleryChatPhoto(IProtoService protoService, ChatPhotoInfo photo)
            : base(protoService)
        {
            _photo = photo;
        }

        public GalleryChatPhoto(IProtoService protoService, ChatPhotoInfo photo, string caption)
            : base(protoService)
        {
            _photo = photo;
            _caption = caption;
        }

        public override File GetFile()
        {
            return _photo.Big;
        }

        public override File GetThumbnail()
        {
            return _photo.Small;
        }

        public override (File File, string FileName) GetFileAndName()
        {
            var big = _photo.Big;
            if (big != null)
            {
                return (big, null);
            }

            return (null, null);
        }

        public override bool UpdateFile(File file)
        {
            if (_photo.Big.Id == file.Id)
            {
                _photo.Big = file;
                return true;
            }

            if (_photo.Small.Id == file.Id)
            {
                _photo.Small = file;
                return true;
            }

            return false;
        }

        public override object Constraint => new PhotoSize(string.Empty, null, 640, 640);

        public override string Caption => _caption;

        public override bool CanCopy => true;
        public override bool CanSave => true;
    }
}
