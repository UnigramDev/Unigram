using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;

namespace Unigram.Collections
{
    public class MediaCollection : IncrementalCollection<Message>
    {
        private readonly IProtoService _protoService;
        private readonly SearchMessagesFilter _filter;
        private readonly long _chatId;
        private readonly string _query;

        private long _lastMaxId;
        private bool _hasMore;

        private FileContext<Message> _filesMap = new FileContext<Message>();

        public MediaCollection(IProtoService protoService, long chatId, SearchMessagesFilter filter, string query = null)
        {
            _protoService = protoService;
            _chatId = chatId;
            _filter = filter;
            _query = query ?? string.Empty;
        }

        public override async Task<IList<Message>> LoadDataAsync()
        {
            try
            {
                IsLoading = true;

                var response = await _protoService.SendAsync(new SearchChatMessages(_chatId, _query, 0, _lastMaxId, 0, 50, _filter));
                if (response is Messages messages)
                {
                    if (messages.MessagesValue.Count > 0)
                    {
                        _lastMaxId = messages.MessagesValue.Min(x => x.Id);
                        _hasMore = true;
                    }
                    else
                    {
                        _hasMore = false;
                    }

                    var result = messages.MessagesValue.ToArray();
                    ProcessFiles(result);

                    IsLoading = false;

                    return messages.MessagesValue;
                }
            }
            catch { }

            IsLoading = false;

            return new Message[0];
        }

        protected override bool GetHasMoreItems()
        {
            return _hasMore;
        }

        protected override void InsertItem(int index, Message item)
        {
            base.InsertItem(index, item);

            var previous = index > 0 ? this[index - 1] : null;
            var next = index < Count - 1 ? this[index + 1] : null;

            UpdateSeparatorOnInsert(item, next, index);
            UpdateSeparatorOnInsert(previous, item, index - 1);
        }

        private static Message ShouldUpdateSeparatorOnInsert(Message item, Message previous, int index)
        {
            if (item != null && previous != null)
            {
                var itemDate = Utils.UnixTimestampToDateTime(item.Date);
                var previousDate = Utils.UnixTimestampToDateTime(previous.Date);
                if (previousDate.Year != itemDate.Year || previousDate.Month != itemDate.Month)
                {
                    var service = new Message(0, previous.SenderUserId, previous.ChatId, null, previous.IsOutgoing, false, false, true, false, previous.IsChannelPost, false, previous.Date, 0, null, 0, 0, 0, 0, string.Empty, 0, 0, new MessageHeaderDate(), null);
                    return service;
                }
            }
            else if (item == null && previous != null)
            {
                var service = new Message(0, previous.SenderUserId, previous.ChatId, null, previous.IsOutgoing, false, false, true, false, previous.IsChannelPost, false, previous.Date, 0, null, 0, 0, 0, 0, string.Empty, 0, 0, new MessageHeaderDate(), null);
                return service;
            }

            return null;
        }

        private void UpdateSeparatorOnInsert(Message item, Message previous, int index)
        {
            var service = ShouldUpdateSeparatorOnInsert(item, previous, index);
            if (service != null)
            {
                base.InsertItem(index + 1, service);
            }
        }



        private void ProcessFiles(IList<Message> messages, Message parent = null)
        {
            foreach (var message in messages)
            {
                var target = parent ?? message;
                var content = message.Content as object;
                if (content is MessageAnimation animationMessage)
                {
                    content = animationMessage.Animation;
                }
                else if (content is MessageAudio audioMessage)
                {
                    content = audioMessage.Audio;
                }
                else if (content is MessageDocument documentMessage)
                {
                    content = documentMessage.Document;
                }
                else if (content is MessageGame gameMessage)
                {
                    if (gameMessage.Game.Animation != null)
                    {
                        content = gameMessage.Game.Animation;
                    }
                    else if (gameMessage.Game.Photo != null)
                    {
                        content = gameMessage.Game.Photo;
                    }
                }
                else if (content is MessageInvoice invoiceMessage)
                {
                    content = invoiceMessage.Photo;
                }
                else if (content is MessageLocation locationMessage)
                {
                    content = locationMessage.Location;
                }
                else if (content is MessagePhoto photoMessage)
                {
                    content = photoMessage.Photo;
                }
                else if (content is MessageSticker stickerMessage)
                {
                    content = stickerMessage.Sticker;
                }
                else if (content is MessageText textMessage)
                {
                    if (textMessage.WebPage?.Animation != null)
                    {
                        content = textMessage.WebPage.Animation;
                    }
                    else if (textMessage.WebPage?.Audio != null)
                    {
                        content = textMessage.WebPage.Audio;
                    }
                    else if (textMessage.WebPage?.Document != null)
                    {
                        content = textMessage.WebPage.Document;
                    }
                    else if (textMessage.WebPage?.Sticker != null)
                    {
                        content = textMessage.WebPage.Sticker;
                    }
                    else if (textMessage.WebPage?.Video != null)
                    {
                        content = textMessage.WebPage.Video;
                    }
                    else if (textMessage.WebPage?.VideoNote != null)
                    {
                        content = textMessage.WebPage.VideoNote;
                    }
                    else if (textMessage.WebPage?.VoiceNote != null)
                    {
                        content = textMessage.WebPage.VoiceNote;
                    }
                    // PHOTO SHOULD ALWAYS BE AT THE END!
                    else if (textMessage?.WebPage?.Photo != null)
                    {
                        content = textMessage?.WebPage?.Photo;
                    }
                }
                else if (content is MessageVideo videoMessage)
                {
                    content = videoMessage.Video;
                }
                else if (content is MessageVideoNote videoNoteMessage)
                {
                    content = videoNoteMessage.VideoNote;
                }
                else if (content is MessageVoiceNote voiceNoteMessage)
                {
                    content = voiceNoteMessage.VoiceNote;
                }

                if (content is Animation animation)
                {
                    if (animation.Thumbnail != null)
                    {
                        _filesMap[animation.Thumbnail.Photo.Id].Add(target);
                    }

                    _filesMap[animation.AnimationValue.Id].Add(target);
                }
                else if (content is Audio audio)
                {
                    if (audio.AlbumCoverThumbnail != null)
                    {
                        _filesMap[audio.AlbumCoverThumbnail.Photo.Id].Add(target);
                    }

                    _filesMap[audio.AudioValue.Id].Add(target);
                }
                else if (content is Document document)
                {
                    if (document.Thumbnail != null)
                    {
                        _filesMap[document.Thumbnail.Photo.Id].Add(target);
                    }

                    _filesMap[document.DocumentValue.Id].Add(target);
                }
                else if (content is Photo photo)
                {
                    foreach (var size in photo.Sizes)
                    {
                        _filesMap[size.Photo.Id].Add(target);
                    }
                }
                else if (content is Sticker sticker)
                {
                    if (sticker.Thumbnail != null)
                    {
                        _filesMap[sticker.Thumbnail.Photo.Id].Add(target);
                    }

                    _filesMap[sticker.StickerValue.Id].Add(target);
                }
                else if (content is Video video)
                {
                    if (video.Thumbnail != null)
                    {
                        _filesMap[video.Thumbnail.Photo.Id].Add(target);
                    }

                    _filesMap[video.VideoValue.Id].Add(target);
                }
                else if (content is VideoNote videoNote)
                {
                    if (videoNote.Thumbnail != null)
                    {
                        _filesMap[videoNote.Thumbnail.Photo.Id].Add(target);
                    }

                    _filesMap[videoNote.Video.Id].Add(target);
                }
                else if (content is VoiceNote voiceNote)
                {
                    _filesMap[voiceNote.Voice.Id].Add(target);
                }
            }
        }

        public bool TryGetMessagesForFileId(int fileId, out IList<Message> items)
        {
            if (_filesMap.TryGetValue(fileId, out List<Message> messages))
            {
                items = messages;
                return true;
            }

            items = null;
            return false;
        }
    }
}
