//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using Telegram.Common;
using Telegram.Td.Api;

namespace Telegram.Services
{
    public partial class MessageAlbumLastMessageService
    {
        private readonly IClientService _clientService;
        private readonly IEventAggregator _aggregator;

        private readonly Chat _chat;
        private readonly UniqueList<long, Message> _messages = new(x => x.Id, Comparer<long>.Create((x, y) => y.CompareTo(x)));

        private readonly object _lock = new();

        private readonly static FormattedText _emptyText = new(string.Empty, Array.Empty<TextEntity>());

        private Queue<long> _queue = new();
        private bool _loading = true;

        private bool _hasOldestMessage;
        private bool _hasNewestMessage;

        public long MediaAlbumId { get; }

        public int PhotosCount { get; private set; }

        public int VideosCount { get; private set; }

        public MessageContent LastMessage { get; private set; }

        public FormattedText Caption { get; private set; } = _emptyText;

        public MessageAlbumLastMessageService(IClientService clientService, IEventAggregator aggregator, Chat chat, Message fromMessage)
        {
            _clientService = clientService;
            _aggregator = aggregator;

            _chat = chat;

            MediaAlbumId = fromMessage.MediaAlbumId;

            Initialize(fromMessage);
        }

        private async void Initialize(Message fromMessage)
        {
            var response = await _clientService.SendAsync(new GetChatHistory(_chat.Id, fromMessage.Id, 0, 10, false));
            if (response is Messages album && album.MessagesValue.Count > 0)
            {
                UpdateLastMessage(album, fromMessage, true);
            }

            _loading = false;
            Dequeue();
        }

        public async void LoadMore(long fromMessageId)
        {
            if (_hasOldestMessage && _hasNewestMessage)
            {
                return;
            }

            if (_loading)
            {
                if (!_queue.Contains(fromMessageId) && !_messages.ContainsKey(fromMessageId))
                {
                    _queue.Enqueue(fromMessageId);
                }

                return;
            }

            if (_messages.Empty())
            {
                return;
            }

            _loading = true;

            var count = 10 - _messages.Count;
            var fromMessage = _messages[0];

            var response = await _clientService.SendAsync(new GetChatHistory(_chat.Id, fromMessage.Id, -count, count, false));
            if (response is Messages album && album.MessagesValue.Count > 0)
            {
                UpdateLastMessage(album, fromMessage, false);
            }

            _loading = false;
            Dequeue();
        }

        private void UpdateLastMessage(Messages album, Message fromMessage, bool needFromMessage)
        {
            var hasOldestMessage = false;
            var hasNewestMessage = false;
            var found = false;

            var photosCount = 0;
            var videosCount = 0;

            void AddMessage(Message message)
            {
                if (_messages.Add(message))
                {
                    if (message.Content is MessagePhoto)
                    {
                        photosCount++;
                    }
                    else
                    {
                        videosCount++;
                    }
                }
            }

            for (int i = 0; i < album.MessagesValue.Count; i++)
            {
                var message = album.MessagesValue[i];
                if (message.MediaAlbumId == MediaAlbumId)
                {
                    if (message.Id == fromMessage.Id && !needFromMessage)
                    {
                        continue;
                    }

                    AddMessage(message);
                    found = true;
                }
                else if (found)
                {
                    hasOldestMessage = true;
                    break;
                }
                else
                {
                    hasNewestMessage = true;
                }
            }

            if (needFromMessage)
            {
                AddMessage(fromMessage);
            }

            lock (_lock)
            {
                PhotosCount += photosCount;
                VideosCount += videosCount;

                UpdateInfo();
            }

            _hasOldestMessage = hasOldestMessage || _messages.Count == 10;
            _hasNewestMessage = hasNewestMessage || _messages.Count == 10;

            if (_messages.Count > 0 && _chat.LastMessage?.MediaAlbumId == MediaAlbumId)
            {
                _aggregator.Publish(new UpdateChatLastMessage(_chat.Id, _chat.LastMessage, _chat.Positions));
            }
        }

        private void Dequeue()
        {
            if (_hasOldestMessage && _hasNewestMessage)
            {
                _queue.Clear();
            }

            while (_queue.TryDequeue(out long fromMessageId))
            {
                if (_messages.ContainsKey(fromMessageId))
                {
                    continue;
                }

                LoadMore(fromMessageId);
                break;
            }
        }

        public void MessageSendSucceeded(long oldMessageId, Message message)
        {
            if (_messages.TryRemove(oldMessageId, out _))
            {
                _messages.Add(message);
            }
        }

        public void MessageSendFailed(long oldMessageId, Message message)
        {
            if (_messages.TryRemove(oldMessageId, out _))
            {
                _messages.Add(message);
            }
        }

        public void DeleteMessages(IList<long> messageIds)
        {
            var found = false;

            var photosCount = 0;
            var videosCount = 0;

            foreach (var messageId in messageIds)
            {
                if (_messages.TryRemove(messageId, out Message message))
                {
                    found = true;

                    if (message.Content is MessagePhoto)
                    {
                        photosCount++;
                    }
                    else
                    {
                        videosCount++;
                    }
                }
            }

            lock (_lock)
            {
                PhotosCount -= photosCount;
                VideosCount -= videosCount;

                UpdateInfo();
            }

            if (found && _messages.Count > 0 && _chat.LastMessage?.MediaAlbumId == MediaAlbumId)
            {
                _aggregator.Publish(new UpdateChatLastMessage(_chat.Id, _chat.LastMessage, _chat.Positions));
            }
        }

        private void UpdateInfo()
        {
            if (_messages.Count > 1)
            {
                var first = _messages[^1].GetCaption();
                var last = _messages[0].GetCaption();

                if (first?.Text.Length > 0)
                {
                    Caption = first;
                }
                else
                {
                    Caption = last;
                }

                LastMessage = _messages[0].Content;
            }
            else if (_messages.Count > 0)
            {
                Caption = _messages[0].GetCaption();
                LastMessage = _messages[0].Content;
            }
            else
            {
                Caption = _emptyText;
                LastMessage = null;
            }
        }

        public MessageAlbumLastMessage Info()
        {
            lock (_lock)
            {
                return new MessageAlbumLastMessage(PhotosCount, VideosCount, LastMessage, Caption);
            }
        }
    }
}
