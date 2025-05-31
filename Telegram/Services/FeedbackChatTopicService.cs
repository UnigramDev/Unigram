//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Td.Api;

namespace Telegram.Services
{
    public partial class FeedbackChatTopicService
    {
        private readonly IClientService _clientService;
        private readonly IEventAggregator _aggregator;

        private readonly long _chatId;

        private readonly Dictionary<long, FeedbackChatTopic> _topics = new();

        private readonly SortedSet<OrderedItem> _order = new();
        private bool _haveFullList;

        public FeedbackChatTopicService(IClientService clientService, IEventAggregator aggregator, long chatId)
        {
            _clientService = clientService;
            _aggregator = aggregator;

            _chatId = chatId;
        }

        public void UpdateFeedbackChatTopic(FeedbackChatTopic newTopic)
        {
            if (_topics.TryGetValue(newTopic.Id, out FeedbackChatTopic topic))
            {
                topic.DraftMessage = newTopic.DraftMessage;
                topic.LastMessage = newTopic.LastMessage;
                topic.IsMarkedAsUnread = newTopic.IsMarkedAsUnread;

                UpdateLastReadOutboxMessageId(topic, newTopic.LastReadOutboxMessageId);
                UpdateLastReadInboxMessageId(topic, newTopic.LastReadInboxMessageId, newTopic.UnreadCount);

                if (topic.UnreadReactionCount != newTopic.UnreadReactionCount)
                {
                    _aggregator.Publish(new UpdateFeedbackChatTopicUnreadReactionCount(_chatId, topic.Id, topic.UnreadReactionCount = newTopic.UnreadReactionCount));
                }

                if (topic.Order != newTopic.Order)
                {
                    UpdateTopicOrder(topic, newTopic.Order, true);
                }
            }
            else
            {
                _topics[newTopic.Id] = newTopic;
                UpdateTopicOrder(newTopic, newTopic.Order, false);
            }
        }

        private void UpdateLastReadOutboxMessageId(FeedbackChatTopic topic, long lastReadOutboxMessageId)
        {
            if (topic.LastReadOutboxMessageId < lastReadOutboxMessageId)
            {
                topic.LastReadOutboxMessageId = lastReadOutboxMessageId;
                _aggregator.Publish(new UpdateFeedbackChatTopicReadOutbox(_chatId, topic.Id, lastReadOutboxMessageId));
            }
        }

        private void UpdateLastReadInboxMessageId(FeedbackChatTopic topic, long lastReadInboxMessageId, long unreadCount)
        {
            if (topic.LastReadInboxMessageId < lastReadInboxMessageId || topic.UnreadCount != unreadCount)
            {
                topic.LastReadInboxMessageId = lastReadInboxMessageId;
                topic.UnreadCount = unreadCount;
                _aggregator.Publish(new UpdateFeedbackChatTopicReadInbox(_chatId, topic.Id, lastReadInboxMessageId, unreadCount));
            }
        }

        private void UpdateTopicOrder(FeedbackChatTopic topic, long order, bool publish)
        {
            Monitor.Enter(_order);

            _order.Remove(new OrderedItem(topic.Id, topic.Order));

            topic.Order = order;

            if (order != 0)
            {
                _order.Add(new OrderedItem(topic.Id, order));
            }

            Monitor.Exit(_order);

            if (publish)
            {
                _aggregator.Publish(new UpdateFeedbackChatTopicLastMessage(_chatId, topic));
            }
        }

        public IEnumerable<FeedbackChatTopic> GetTopics(IEnumerable<long> ids)
        {
            foreach (var id in ids)
            {
                if (id == long.MaxValue)
                {
                    // TODO: translate
                    yield return new FeedbackChatTopic(_chatId, 0, null, long.MaxValue, false, 0, 0, 0, 0, null, null);
                }

                var topic = GetTopic(id);
                if (topic != null)
                {
                    yield return topic;
                }
            }
        }

        public FeedbackChatTopic GetTopic(long id)
        {
            if (_topics.TryGetValue(id, out FeedbackChatTopic value))
            {
                return value;
            }

            return null;
        }

        public Task<Topics> GetFeedbackChatTopicsAsync(int offset, int limit)
        {
            return GetFeedbackChatTopicsAsyncImpl(offset, limit, false);
        }

        private async Task<Topics> GetFeedbackChatTopicsAsyncImpl(int offset, int limit, bool reentrancy)
        {
            Monitor.Enter(_order);

            var count = offset + limit;
            var sorted = _order;

            var haveFullList = _haveFullList;

#if MOCKUP
            _haveFullChatList[index] = true;
#else
            if (count > sorted.Count && !haveFullList && !reentrancy)
            {
                Monitor.Exit(_order);

                var response = await _clientService.SendAsync(new LoadFeedbackChatTopics(_chatId, count - sorted.Count));
                if (response is Error error)
                {
                    if (error.Code is 404 or 400)
                    {
                        _haveFullList = true;
                    }
                    else
                    {
                        return new Topics(0, Array.Empty<long>());
                    }
                }

                // Chats have already been received through updates, let's retry request
                return await GetFeedbackChatTopicsAsyncImpl(offset, limit, true);
            }
#endif

            // Have enough chats in the chat list to answer request
            var result = new long[Math.Max(0, Math.Min(limit, sorted.Count - offset))];
            var pos = 0;

            using (var iter = sorted.GetEnumerator())
            {
                int max = Math.Min(count, sorted.Count);

                for (int i = 0; i < max; i++)
                {
                    iter.MoveNext();

                    if (i >= offset)
                    {
                        result[pos++] = iter.Current.Id;
                    }
                }
            }

            haveFullList &= count >= sorted.Count;

            Monitor.Exit(_order);
            return new Topics(haveFullList ? -1 : 0, result);
        }
    }
}

namespace Telegram.Td.Api
{
    public sealed partial class UpdateFeedbackChatTopicLastMessage
    {
        public UpdateFeedbackChatTopicLastMessage(long chatId, long topicId, long order, Message lastMessage)
        {
            ChatId = chatId;
            TopicId = topicId;
            Order = order;
            LastMessage = lastMessage;
        }

        public UpdateFeedbackChatTopicLastMessage(long chatId, FeedbackChatTopic topic)
        {
            ChatId = chatId;
            TopicId = topic.Id;
            Order = topic.Order;
            LastMessage = topic.LastMessage;
        }

        public long ChatId { get; set; }

        public long TopicId { get; set; }

        public long Order { get; set; }

        public Message LastMessage { get; set; }
    }

    public sealed partial class UpdateFeedbackChatTopicPosition
    {
        public UpdateFeedbackChatTopicPosition(long chatId, long topicId, long order)
        {
            ChatId = chatId;
            TopicId = topicId;
            Order = order;
        }

        public long ChatId { get; set; }

        public long TopicId { get; set; }

        public long Order { get; set; }
    }

    public sealed partial class UpdateFeedbackChatTopicReadInbox
    {
        public UpdateFeedbackChatTopicReadInbox(long chatId, long topicId, long lastReadInboxMessageId, long unreadCount)
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

    public sealed partial class UpdateFeedbackChatTopicReadOutbox
    {
        public UpdateFeedbackChatTopicReadOutbox(long chatId, long topicId, long lastReadOutboxMessageId)
        {
            ChatId = chatId;
            TopicId = topicId;
            LastReadOutboxMessageId = lastReadOutboxMessageId;
        }

        public long ChatId { get; set; }

        public long TopicId { get; set; }

        public long LastReadOutboxMessageId { get; set; }
    }

    public sealed partial class UpdateFeedbackChatTopicUnreadReactionCount
    {
        public UpdateFeedbackChatTopicUnreadReactionCount(long chatId, long topicId, long unreadReactionCount)
        {
            ChatId = chatId;
            TopicId = topicId;
            UnreadReactionCount = unreadReactionCount;
        }

        public long ChatId { get; set; }

        public long TopicId { get; set; }

        public long UnreadReactionCount { get; set; }
    }

    public sealed partial class UpdateFeedbackChatTopicUnreadMentionCount
    {
        public UpdateFeedbackChatTopicUnreadMentionCount(long chatId, long topicId, long unreadMentionCount)
        {
            ChatId = chatId;
            TopicId = topicId;
            UnreadMentionCount = unreadMentionCount;
        }

        public long ChatId { get; set; }

        public long TopicId { get; set; }

        public long UnreadMentionCount { get; set; }
    }
}
