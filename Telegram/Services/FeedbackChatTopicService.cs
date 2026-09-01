//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Td.Api;

namespace Telegram.Services
{
    /// <summary>
    /// Every topic of one direct messages chat, in order.
    /// </summary>
    public partial class DirectMessagesChatTopicService : OrderedSourceService<DirectMessagesChatTopic>
    {
        private readonly IClientService _clientService;
        private readonly IEventAggregator _aggregator;

        private readonly long _chatId;

        private readonly ReaderWriterDictionary<long, DirectMessagesChatTopic> _topics = new(100);

        public DirectMessagesChatTopicService(IClientService clientService, IEventAggregator aggregator, long chatId)
        {
            _clientService = clientService;
            _aggregator = aggregator;

            _chatId = chatId;
        }

        public void UpdateDirectMessagesChatTopic(DirectMessagesChatTopic newTopic)
        {
            if (_topics.TryGetValue(newTopic.Id, out DirectMessagesChatTopic topic))
            {
                // Read before the assignment below: the row shows the last message, so a new
                // one has to redraw it even when the topic did not move.
                var lastMessage = topic.LastMessage?.Id != newTopic.LastMessage?.Id;

                topic.LastMessage = newTopic.LastMessage;
                topic.IsMarkedAsUnread = newTopic.IsMarkedAsUnread;

                UpdateLastReadOutboxMessageId(topic, newTopic.LastReadOutboxMessageId);
                UpdateLastReadInboxMessageId(topic, newTopic.LastReadInboxMessageId, newTopic.UnreadCount);

                if (topic.UnreadReactionCount != newTopic.UnreadReactionCount)
                {
                    _aggregator.Publish(new UpdateDirectMessagesChatTopicUnreadReactionCount(_chatId, topic.Id, topic.UnreadReactionCount = newTopic.UnreadReactionCount));
                }

                if (topic.DraftMessage?.Date != newTopic.DraftMessage?.Date)
                {
                    _aggregator.Publish(new UpdateDirectMessagesChatDraftMessage(_chatId, topic.Id, topic.DraftMessage = newTopic.DraftMessage));
                }

                // An update carrying neither is one of the counts, which the row draws from
                // its own update: reporting it would rebuild the cell for nothing.
                if (lastMessage || topic.Order != newTopic.Order)
                {
                    UpdateTopicOrder(topic, newTopic.Order, lastMessage);
                }
            }
            else
            {
                _topics[newTopic.Id] = newTopic;
                UpdateTopicOrder(newTopic, newTopic.Order, false);
            }
        }

        private void UpdateLastReadOutboxMessageId(DirectMessagesChatTopic topic, long lastReadOutboxMessageId)
        {
            if (topic.LastReadOutboxMessageId < lastReadOutboxMessageId)
            {
                topic.LastReadOutboxMessageId = lastReadOutboxMessageId;
                _aggregator.Publish(new UpdateDirectMessagesChatTopicReadOutbox(_chatId, topic.Id, lastReadOutboxMessageId));
            }
        }

        private void UpdateLastReadInboxMessageId(DirectMessagesChatTopic topic, long lastReadInboxMessageId, long unreadCount)
        {
            if (topic.LastReadInboxMessageId < lastReadInboxMessageId || topic.UnreadCount != unreadCount)
            {
                topic.LastReadInboxMessageId = lastReadInboxMessageId;
                topic.UnreadCount = unreadCount;
                _aggregator.Publish(new UpdateDirectMessagesChatTopicReadInbox(_chatId, topic.Id, lastReadInboxMessageId, unreadCount));
            }
        }

        private void UpdateTopicOrder(DirectMessagesChatTopic topic, long order, bool lastMessage)
        {
            lock (SyncRoot)
            {
                topic.Order = order;
                SetOrder(topic.Id, order);
            }

            RaiseChanged(topic, order, lastMessage);
        }

        public IEnumerable<DirectMessagesChatTopic> GetTopics(IEnumerable<long> ids)
        {
            foreach (var id in ids)
            {
                if (id == long.MaxValue)
                {
                    yield return new DirectMessagesChatTopic(_chatId, 0, null, long.MaxValue, true, false, 0, 0, 0, 0, null, null);

                    // The synthetic row is not a topic. Harmless here today only because this
                    // GetTopic is cache-only, unlike the forum one that would fetch.
                    continue;
                }

                var topic = GetTopic(id);
                if (topic != null)
                {
                    yield return topic;
                }
            }
        }

        public DirectMessagesChatTopic GetTopic(long id)
        {
            if (_topics.TryGetValue(id, out DirectMessagesChatTopic value))
            {
                return value;
            }

            return null;
        }

        public async Task<Topics> GetDirectMessagesChatTopicsAsync(int offset, int limit)
        {
            var page = await GetItemsAsync(offset, limit);
            return new Topics(page.HaveFullList ? -1 : 0, page.Ids);
        }

        protected override Task<Object> LoadMoreItemsAsync(int count)
        {
            return _clientService.SendAsync(new LoadDirectMessagesChatTopics(_chatId, count));
        }

        // 400 as well: the request is refused outright for a chat with no topics to page.
        protected override bool IsExhausted(Error error)
        {
            return error.Code is 404 or 400;
        }
    }
}

namespace Telegram.Td.Api
{
    public sealed partial class UpdateDirectMessagesChatTopicReadInbox
    {
        public UpdateDirectMessagesChatTopicReadInbox(long chatId, long topicId, long lastReadInboxMessageId, long unreadCount)
        {
            ChatId = chatId;
            TopicId = topicId;
            LastReadInboxMessageId = lastReadInboxMessageId;
        }

        public long ChatId { get; set; }

        public long TopicId { get; set; }

        public long LastReadInboxMessageId { get; set; }

        public long UnreadCount { get; set; }
    }

    public sealed partial class UpdateDirectMessagesChatTopicReadOutbox
    {
        public UpdateDirectMessagesChatTopicReadOutbox(long chatId, long topicId, long lastReadOutboxMessageId)
        {
            ChatId = chatId;
            TopicId = topicId;
            LastReadOutboxMessageId = lastReadOutboxMessageId;
        }

        public long ChatId { get; set; }

        public long TopicId { get; set; }

        public long LastReadOutboxMessageId { get; set; }
    }

    public sealed partial class UpdateDirectMessagesChatTopicUnreadReactionCount
    {
        public UpdateDirectMessagesChatTopicUnreadReactionCount(long chatId, long topicId, long unreadReactionCount)
        {
            ChatId = chatId;
            TopicId = topicId;
            UnreadReactionCount = unreadReactionCount;
        }

        public long ChatId { get; set; }

        public long TopicId { get; set; }

        public long UnreadReactionCount { get; set; }
    }

    public sealed partial class UpdateDirectMessagesChatTopicUnreadMentionCount
    {
        public UpdateDirectMessagesChatTopicUnreadMentionCount(long chatId, long topicId, long unreadMentionCount)
        {
            ChatId = chatId;
            TopicId = topicId;
            UnreadMentionCount = unreadMentionCount;
        }

        public long ChatId { get; set; }

        public long TopicId { get; set; }

        public long UnreadMentionCount { get; set; }
    }

    public sealed partial class UpdateDirectMessagesChatDraftMessage
    {
        public UpdateDirectMessagesChatDraftMessage(long chatId, long topicId, DraftMessage draftMessage)
        {
            ChatId = chatId;
            TopicId = topicId;
            DraftMessage = draftMessage;
        }

        public long ChatId { get; set; }

        public long TopicId { get; set; }

        public DraftMessage DraftMessage { get; set; }
    }
}
