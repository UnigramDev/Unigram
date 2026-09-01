//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.ViewModels.Gallery
{
    public partial class GalleryMessage : GalleryMedia
    {
        protected readonly Message _message;
        protected readonly MessageProperties _properties;

        public GalleryMessage(IClientService clientService, Message message, MessageProperties properties)
            : base(clientService)
        {
            // Create a copy so that content doesn't get updated while the gallery is open
            _message = new(message.Id, message.SenderId, message.ReceiverId, message.ChatId, message.SendingState, message.SchedulingState, message.IsOutgoing, message.IsPinned, message.IsFromOffline, message.CanBeSaved, message.HasTimestampedMedia, message.IsChannelPost, message.IsPaidStarSuggestedPost, message.IsPaidGramSuggestedPost, message.ContainsUnreadMention, message.ContainsUnreadPollVotes, message.Date, message.EditDate, message.ForwardInfo, message.ImportInfo, message.InteractionInfo, message.UnreadReactions, message.FactCheck, message.SuggestedPostInfo, message.ReplyTo, message.TopicId, message.SelfDestructType, message.SelfDestructIn, message.AutoDeleteIn, message.ViaBotUserId, message.GuestBotCallerId, message.SenderBusinessBotUserId, message.SenderBoostCount, message.SenderTag, message.PaidMessageStarCount, message.AuthorSignature, message.MediaAlbumId, message.EffectId, message.RestrictionInfo, message.SummaryLanguageCode, message.Content, message.EphemeralContent, message.ReplyMarkup);
            _properties = properties;

            var content = _message.Content;

            var protectedChat = clientService.TryGetChat(message.ChatId, out Chat chat)
                && (chat.Type is ChatTypeSecret || chat.HasProtectedContent);

            File = _message.GetFile();
            Constraint = content;

            if (content is MessageDocument document)
            {
                Constraint = null;
                IsMedia = document.IsPhoto();
            }

            var thumbnail = _message.GetThumbnail();
            if (thumbnail == null)
            {
                Thumbnail = content.GetPhoto()?.GetSmall()?.Photo;
            }
            else if (thumbnail.Format is ThumbnailFormatJpeg)
            {
                Thumbnail = thumbnail.File;
            }

            Minithumbnail = _message.GetMinithumbnail();

            if (content is MessageVideo video)
            {
                IsHls = video.IsHls();
                AlternativeVideos = video.AlternativeVideos;
            }

            From = GetFrom(clientService, _message);
            Caption = _message.GetCaption();
            Date = _message.Date;

            IsVideo = GetIsVideo(content);
            IsLoopingEnabled = GetIsLoopingEnabled(content);
            IsVideoNote = content is MessageVideoNote
                || content is MessageText { LinkPreview.Type: LinkPreviewTypeVideoNote };
            Duration = GetDuration(content);

            HasStickers = content switch
            {
                MessageAnimation animation => animation.Animation.HasStickers,
                MessagePhoto photo => photo.Photo.HasStickers,
                MessageVideo messageVideo => messageVideo.Video.HasStickers,
                _ => false
            };

            var secret = content switch
            {
                MessageAnimation animation => animation.IsSecret,
                MessagePhoto photo => photo.IsSecret,
                MessageVideo messageVideo => messageVideo.IsSecret,
                MessageVideoNote videoNote => videoNote.IsSecret,
                _ => false
            };

            HasProtectedContent = protectedChat || secret;

            CanBeViewed = true;
            CanBeSaved = !protectedChat && !secret;
            CanBeShared = CanBeSaved;
            CanBeCopied = CanBeSaved && IsPhoto;
        }

        public GalleryMessage(IClientService clientService, MessageWithOwner message, MessageProperties properties)
            : this(clientService, message.Get(), properties)
        {
        }

        private static object GetFrom(IClientService clientService, Message message)
        {
            if (message.SchedulingState != null)
            {
                return null;
            }

            if (message.ForwardInfo != null)
            {
                // TODO: ...
            }

            if (message.SenderId is MessageSenderChat senderChat)
            {
                return clientService.GetChat(senderChat.ChatId);
            }
            else if (message.SenderId is MessageSenderUser senderUser)
            {
                return clientService.GetUser(senderUser.UserId);
            }

            return null;
        }

        private static bool GetIsVideo(MessageContent content)
        {
            if (content is MessageVideo or MessageAnimation or MessageVideoNote)
            {
                return true;
            }
            else if (content is MessageGame game)
            {
                return game.Game.Animation != null;
            }
            else if (content is MessageInvoice invoice)
            {
                return invoice.PaidMedia is PaidMediaVideo;
            }
            else if (content is MessageText text)
            {
                return text.LinkPreview?.Type is LinkPreviewTypeVideo or LinkPreviewTypeAnimation or LinkPreviewTypeVideoNote or LinkPreviewTypeEmbeddedAnimationPlayer { Animation: not null } or LinkPreviewTypeEmbeddedVideoPlayer { Video: not null };
            }
            else if (content is MessageSponsored sponsored)
            {
                return sponsored.Content is MessageAnimation or MessageVideo;
            }

            return false;
        }

        private static bool GetIsLoopingEnabled(MessageContent content)
        {
            if (content is MessageAnimation or MessageVideoNote)
            {
                return true;
            }
            else if (content is MessageGame game)
            {
                return game.Game.Animation != null;
            }
            else if (content is MessageText text)
            {
                return text.LinkPreview?.Type is LinkPreviewTypeAnimation or LinkPreviewTypeVideoNote or LinkPreviewTypeEmbeddedAnimationPlayer { Animation: not null };
            }

            return false;
        }

        private static int GetDuration(MessageContent content)
        {
            if (content is MessageVideo video)
            {
                return video.Video.Duration;
            }
            else if (content is MessageAnimation animation)
            {
                return animation.Animation.Duration;
            }
            else if (content is MessageVideoNote videoNote)
            {
                return videoNote.VideoNote.Duration;
            }
            else if (content is MessageGame game)
            {
                return game.Game.Animation?.Duration ?? 0;
            }
            else if (content is MessageInvoice invoice)
            {
                if (invoice.PaidMedia is PaidMediaVideo extendedVideo)
                {
                    return extendedVideo.Video.Duration;
                }
            }
            else if (content is MessageText text)
            {
                return text.LinkPreview?.Type switch
                {
                    LinkPreviewTypeVideo previewVideo => previewVideo.Video.Duration,
                    LinkPreviewTypeAnimation previewAnimation => previewAnimation.Animation.Duration,
                    LinkPreviewTypeVideoNote previewVideoNote => previewVideoNote.VideoNote.Duration,
                    LinkPreviewTypeEmbeddedAnimationPlayer embeddedAnimationPlayer => embeddedAnimationPlayer.Animation?.Duration ?? 0,
                    LinkPreviewTypeEmbeddedVideoPlayer embeddedVideoPlayer => embeddedVideoPlayer.Video?.Duration ?? 0,
                    _ => 0
                };
            }
            else if (content is MessageSponsored sponsored)
            {
                return sponsored.Content switch
                {
                    MessageAnimation sponsoredAnimation => sponsoredAnimation.Animation.Duration,
                    MessageVideo sponsoredVideo => sponsoredVideo.Video.Duration,
                    _ => 0
                };
            }

            return 0;
        }

        public Message Message => _message;

        public MessageContent Content => _message.Content;

        public MessageForwardInfo ForwardInfo => _message.ForwardInfo;

        public MessageSelfDestructType SelfDestructType => _message.SelfDestructType;

        public long ChatId => _message.ChatId;
        public long Id => _message.Id;

        public bool CanGetVideoAdvertisements => _properties?.CanGetVideoAdvertisements ?? false;

        public VideoMessageAdvertisements Advertisements { get; set; }

        public int AdvertisementsSelectedIndex { get; set; }

        public VideoMessageAdvertisement GetNextAdvertisement()
        {
            var index = AdvertisementsSelectedIndex++;
            if (index < Advertisements.Advertisements.Count)
            {
                return Advertisements.Advertisements[index % Advertisements.Advertisements.Count];
            }

            return null;
        }
    }
}
