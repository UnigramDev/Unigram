using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;

namespace Unigram.ViewModels.Gallery
{
    public class GalleryUserProfilePhoto : GalleryContent
    {
        private readonly User _user;
        private readonly UserProfilePhoto _photo;
        private readonly string _caption;

        public GalleryUserProfilePhoto(IProtoService protoService, User user, UserProfilePhoto photo)
            : base(protoService)
        {
            _user = user;
            _photo = photo;
        }

        public long Id => _photo.Id;

        public override File GetFile()
        {
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

        public override object From => _user;

        public override object Constraint => _photo;

        public override string Caption => _caption;

        public override int Date => _photo.AddedDate;

        public override bool CanCopy => true;
        public override bool CanSave => true;
    }
}
