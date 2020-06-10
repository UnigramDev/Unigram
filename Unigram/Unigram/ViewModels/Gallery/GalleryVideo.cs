using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;

namespace Unigram.ViewModels.Gallery
{
    public class GalleryVideo : GalleryContent
    {
        private readonly Video _video;
        private readonly string _caption;

        public GalleryVideo(IProtoService protoService, Video video)
            : base(protoService)
        {
            _video = video;
        }

        public GalleryVideo(IProtoService protoService, Video video, string caption)
            : base(protoService)
        {
            _video = video;
            _caption = caption;
        }

        //public long Id => _video.Id;

        public override File GetFile()
        {
            return _video.VideoValue;
        }

        public override File GetThumbnail()
        {
            if (_video.Thumbnail?.Format is ThumbnailFormatJpeg)
            {
                return _video.Thumbnail?.File;
            }

            return null;
        }

        public override (File File, string FileName) GetFileAndName()
        {
            return (_video.VideoValue, _video.FileName);
        }

        public override bool UpdateFile(File file)
        {
            return _video.UpdateFile(file);
        }

        public override object Constraint => _video;

        public override string Caption => _caption;

        public override bool HasStickers => _video.HasStickers;

        public override bool IsVideo => true;

        public override bool CanSave => true;

        public override int Duration => _video.Duration;

        public override string MimeType => _video.MimeType;
    }
}
