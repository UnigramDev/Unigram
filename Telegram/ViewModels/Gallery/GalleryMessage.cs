//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.ViewModels.Gallery
{
    public partial class GalleryMessage : GalleryMedia
    {
        protected readonly Message _message;
        protected readonly bool _hasProtectedContent;

        public GalleryMessage(IClientService clientService, Message message)
            : base(clientService)
        {
            _message = message;

            if (clientService.TryGetChat(message.ChatId, out Chat chat))
            {
                _hasProtectedContent = chat.Type is ChatTypeSecret || chat.HasProtectedContent;
            }

            File = _message.GetFile();

            var thumbnail = _message.GetThumbnail();
            if (thumbnail == null)
            {
                var photo = _message.GetPhoto();
                if (photo != null)
                {
                    Thumbnail = photo.GetSmall()?.Photo;
                }
            }
            else if (thumbnail?.Format is ThumbnailFormatJpeg)
            {
                Thumbnail = thumbnail.File;
            }
        }

        public GalleryMessage(IClientService clientService, MessageWithOwner message)
            : this(clientService, message.Get())
        {
        }

        public long ChatId => _message.ChatId;
        public long Id => _message.Id;

        public override bool IsHls()
        {
            if (_message.Content is MessageVideo video)
            {
                return video.IsHls();
            }

            return false;
        }

        public override IList<AlternativeVideo> AlternativeVideos
        {
            get
            {
                if (_message.Content is MessageVideo video)
                {
                    return video.AlternativeVideos;
                }

                return Array.Empty<AlternativeVideo>();
            }
        }

        public override object Constraint => _message.Content;

        public override object From
        {
            get
            {
                if (_message.SchedulingState != null)
                {
                    return null;
                }

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
                    return invoice.PaidMedia is PaidMediaVideo;
                }
                else if (_message.Content is MessageText text)
                {
                    return text.LinkPreview?.Type is LinkPreviewTypeVideo
                        || text.LinkPreview?.Type is LinkPreviewTypeAnimation
                        || text.LinkPreview?.Type is LinkPreviewTypeVideoNote;
                }

                return false;
            }
        }

        public override bool IsLoopingEnabled
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
                    return text.LinkPreview?.Type is LinkPreviewTypeAnimation
                        || text.LinkPreview?.Type is LinkPreviewTypeVideoNote;
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
                    return text.LinkPreview?.Type is LinkPreviewTypeVideoNote;
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



        public override bool CanBeViewed => true;
        public override bool CanBeCopied => CanBeSaved && IsPhoto;
        public override bool CanBeSaved => !_hasProtectedContent && _message.Content switch
        {
            MessageAnimation animation => !animation.IsSecret,
            MessagePhoto photo => !photo.IsSecret,
            MessageVideo video => !video.IsSecret,
            MessageVideoNote videoNote => !videoNote.IsSecret,
            _ => true
        };

        public override bool CanBeShared => CanBeSaved;

        public override bool HasProtectedContent => _hasProtectedContent || _message.Content switch
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
                    if (invoice.PaidMedia is PaidMediaVideo extendedVideo)
                    {
                        return extendedVideo.Video.Duration;
                    }
                }
                else if (_message.Content is MessageText text)
                {
                    return text.LinkPreview?.Type switch
                    {
                        LinkPreviewTypeVideo previewVideo => previewVideo.Video.Duration,
                        LinkPreviewTypeAnimation previewAnimation => previewAnimation.Animation.Duration,
                        LinkPreviewTypeVideoNote previewVideoNote => previewVideoNote.VideoNote.Duration,
                        _ => 0
                    };
                }

                return 0;
            }
        }
    }
}
