//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.ViewModels.Gallery
{
    public class GalleryMessage : GalleryMedia
    {
        protected readonly Message _message;
        protected readonly bool _hasProtectedContent;

        public GalleryMessage(IClientService clientService, Message message)
            : base(clientService)
        {
            _message = message;

            var chat = clientService.GetChat(message.ChatId);
            if (chat != null)
            {
                _hasProtectedContent = chat.Type is ChatTypeSecret || chat.HasProtectedContent;
            }
        }

        public Message Message => _message;

        public long ChatId => _message.ChatId;
        public long Id => _message.Id;

        public override File GetFile()
        {
            return _message.GetFile();
        }

        public override File GetThumbnail()
        {
            var thumbnail = _message.GetThumbnail();
            if (thumbnail == null)
            {
                var photo = _message.GetPhoto();
                if (photo != null)
                {
                    return photo.GetSmall()?.Photo;
                }
            }

            if (thumbnail?.Format is ThumbnailFormatJpeg)
            {
                return thumbnail.File;
            }

            return null;
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

                if (_message.SenderId is MessageSenderChat senderChat)
                {
                    return _clientService.GetChat(senderChat.ChatId);
                }
                else if (_message.SenderId is MessageSenderUser senderUser)
                {
                    return _clientService.GetUser(senderUser.UserId);
                }

                return null;
            }
        }

        public override FormattedText Caption => _message.GetCaption();
        public override int Date => _message.Date;

        public override bool IsVideo
        {
            get
            {
                if (_message.Content is MessageVideo or MessageAnimation or MessageVideoNote)
                {
                    return true;
                }
                else if (_message.Content is MessageGame game)
                {
                    return game.Game.Animation != null;
                }
                else if (_message.Content is MessageInvoice invoice)
                {
                    return invoice.ExtendedMedia is MessageExtendedMediaVideo;
                }
                else if (_message.Content is MessageText text)
                {
                    return text.WebPage?.Video != null
                        || text.WebPage?.Animation != null
                        || text.WebPage?.VideoNote != null;
                }

                return false;
            }
        }

        public override bool IsLoop
        {
            get
            {
                if (_message.Content is MessageAnimation or MessageVideoNote)
                {
                    return true;
                }
                else if (_message.Content is MessageGame game)
                {
                    return game.Game.Animation != null;
                }
                else if (_message.Content is MessageText text)
                {
                    return text.WebPage?.Animation != null
                        || text.WebPage?.VideoNote != null;
                }

                return false;
            }
        }

        public override bool IsVideoNote
        {
            get
            {
                if (_message.Content is MessageVideoNote)
                {
                    return true;
                }
                else if (_message.Content is MessageText text)
                {
                    return text.WebPage?.VideoNote != null;
                }

                return false;
            }
        }

        public override bool HasStickers => _message.Content switch
        {
            MessageAnimation animation => animation.Animation.HasStickers,
            MessagePhoto photo => photo.Photo.HasStickers,
            MessageVideo video => video.Video.HasStickers,
            _ => false
        };



        public override bool CanView => true;
        public override bool CanCopy => CanSave && IsPhoto;
        public override bool CanSave => !_hasProtectedContent && _message.Content switch
        {
            MessageAnimation animation => !animation.IsSecret,
            MessagePhoto photo => !photo.IsSecret,
            MessageVideo video => !video.IsSecret,
            MessageVideoNote videoNote => !videoNote.IsSecret,
            _ => true
        };

        public override bool CanShare => CanSave;

        public override bool IsProtected => _hasProtectedContent || _message.Content switch
        {
            MessageAnimation animation => animation.IsSecret,
            MessagePhoto photo => photo.IsSecret,
            MessageVideo video => video.IsSecret,
            MessageVideoNote videoNote => videoNote.IsSecret,
            _ => false
        };

        public MessageSelfDestructType SelfDestructType => _message.SelfDestructType;

        public override int Duration
        {
            get
            {
                if (_message.Content is MessageVideo video)
                {
                    return video.Video.Duration;
                }
                else if (_message.Content is MessageAnimation animation)
                {
                    return animation.Animation.Duration;
                }
                else if (_message.Content is MessageVideoNote videoNote)
                {
                    return videoNote.VideoNote.Duration;
                }
                else if (_message.Content is MessageGame game)
                {
                    return game.Game.Animation?.Duration ?? 0;
                }
                else if (_message.Content is MessageInvoice invoice)
                {
                    if (invoice.ExtendedMedia is MessageExtendedMediaVideo extendedVideo)
                    {
                        return extendedVideo.Video.Duration;
                    }
                }
                else if (_message.Content is MessageText text)
                {
                    if (text.WebPage?.Video != null)
                    {
                        return text.WebPage.Video.Duration;
                    }
                    else if (text.WebPage?.Animation != null)
                    {
                        return text.WebPage.Video.Duration;
                    }
                    else if (text.WebPage?.VideoNote != null)
                    {
                        return text.WebPage.VideoNote.Duration;
                    }
                }

                return 0;
            }
        }

        public override string MimeType
        {
            get
            {
                if (_message.Content is MessageVideo video)
                {
                    return video.Video.MimeType;
                }
                else if (_message.Content is MessageAnimation animation)
                {
                    return animation.Animation.MimeType;
                }
                else if (_message.Content is MessageVideoNote videoNote)
                {
                    return "video/mp4"; // videoNote.VideoNote.MimeType;
                }
                else if (_message.Content is MessageGame game)
                {
                    return game.Game.Animation?.MimeType;
                }
                else if (_message.Content is MessageInvoice invoice)
                {
                    if (invoice.ExtendedMedia is MessageExtendedMediaVideo extendedVideo)
                    {
                        return extendedVideo.Video.MimeType;
                    }
                }
                else if (_message.Content is MessageText text)
                {
                    if (text.WebPage?.Video != null)
                    {
                        return text.WebPage.Video.MimeType;
                    }
                    else if (text.WebPage?.Animation != null)
                    {
                        return text.WebPage.Video.MimeType;
                    }
                }

                return null;
            }
        }
    }
}
