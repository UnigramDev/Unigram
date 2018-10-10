using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;

namespace Unigram.ViewModels.Gallery
{
    public class GalleryMessageItem : GalleryItem
    {
        protected readonly Message _message;

        public GalleryMessageItem(IProtoService protoService, Message message)
            : base(protoService)
        {
            _message = message;
        }

        public long ChatId => _message.ChatId;
        public long Id => _message.Id;

        public bool IsHot => _message.IsSecret();

        public override File GetFile()
        {
            var file = _message.GetFile();
            if (file == null)
            {
                var photo = _message.GetPhoto();
                if (photo != null)
                {
                    file = photo.GetBig()?.Photo;
                }
            }

            return file;
        }

        public override File GetThumbnail()
        {
            var file = _message.GetThumbnail();
            if (file == null)
            {
                var photo = _message.GetPhoto();
                if (photo != null)
                {
                    file = photo.GetSmall()?.Photo;
                }
            }

            return file;
        }

        public override (File File, string FileName) GetFileAndName()
        {
            return _message.GetFileAndName(true);
        }

        public override bool UpdateFile(File file)
        {
            return _message.UpdateFile(file);
        }

        public override object Constraint => _message.Content;

        public override object From
        {
            get
            {
                if (_message.ForwardInfo != null)
                {
                    // TODO: ...
                }

                if (_message.IsChannelPost)
                {
                    return _protoService.GetChat(_message.ChatId);
                }

                return _protoService.GetUser(_message.SenderUserId);
            }
        }

        public override string Caption => _message.GetCaption()?.Text;
        public override int Date => _message.Date;

        public override bool IsVideo
        {
            get
            {
                if (_message.Content is MessageVideo || _message.Content is MessageAnimation)
                {
                    return true;
                }
                else if (_message.Content is MessageText text)
                {
                    return text.WebPage?.Video != null;
                }

                return false;
            }
        }

        public override bool IsLoop
        {
            get
            {
                if (_message.Content is MessageAnimation)
                {
                    return true;
                }
                else if (_message.Content is MessageText text)
                {
                    return text.WebPage?.Animation != null;
                }

                return false;
            }
        }

        public override bool HasStickers
        {
            get
            {
                if (_message.Content is MessagePhoto photo)
                {
                    return photo.Photo.HasStickers;
                }
                else if (_message.Content is MessageVideo video)
                {
                    return video.Video.HasStickers;
                }

                return false;
            }
        }



        public override bool CanView => true;
        public override bool CanCopy => !_message.IsSecret() && IsPhoto;
        public override bool CanSave => !_message.IsSecret();
    }
}
