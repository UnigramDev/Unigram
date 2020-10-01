using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;

namespace Unigram.ViewModels.Gallery
{
    public class GalleryChatPhoto : GalleryContent
    {
        private readonly object _from;
        private readonly ChatPhoto _photo;

        public GalleryChatPhoto(IProtoService protoService, object from, ChatPhoto photo)
            : base(protoService)
        {
            _from = from;
            _photo = photo;
        }

        public long Id => _photo.Id;

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

        public override bool IsVideo => _photo.Animation != null;
        public override bool IsLoop => _photo.Animation != null;

        public override int Duration => 1;

        public override string MimeType => "video/mp4";

        public override object From => _from;

        public override object Constraint => _photo;

        public override int Date => _photo.AddedDate;

        public override bool CanCopy => true;
        public override bool CanSave => true;
    }
}
