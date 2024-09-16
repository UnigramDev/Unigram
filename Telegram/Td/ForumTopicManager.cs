using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.Td
{
    internal class ForumTopicsManager
    {
        private readonly IClientService _clientService;
        private readonly IEventAggregator _aggregator;

        private readonly long _chatId;

        private readonly List<long> _pinnedIds = new();

        private readonly Dictionary<long, ForumTopicImpl> _topics = new();
        private readonly SortedSet<OrderedTopic> _order = new();

        private readonly Dictionary<long, long> _messageToTopics = new();

        private readonly object _syncRoot = new();
        private readonly object _syncList = new();
        private bool _haveFullList;

        public ForumTopicsManager(IClientService clientService, IEventAggregator aggregator, long chatId)
        {
            _clientService = clientService;
            _aggregator = aggregator;
            _chatId = chatId;
        }

        public void UpdateNewMessage(Message message)
        {
            lock (_syncRoot)
            {
                if (message.MessageThreadId != 0 && _order.Count > 0)
                {
                    var id = message.MessageThreadId;
                    var order = Order(id, message);

                    var pinned = _pinnedIds.Contains(id);

                    UpdateForumTopicInfo

                    if (order > _order.Max.Order)
                    {
                        UpdateSavedMessagesTopicOrder(new ForumTopicImpl(id, message, pinned, order)
                        {
                            LastMessage = message,
                        });
                    }
                    else
                    {
                        UpdateSavedMessagesTopicOrder(new SavedMessagesChat(id, message, pinned, 0));
                    }
                }
            }
        }

        public void UpdateDeleteMessages(IList<long> messageIds)
        {
            lock (_syncRoot)
            {
                foreach (var id in messageIds)
                {
                    if (_messageToTopics.TryGetValue(id, out var topicId) && _topics.TryGetValue(topicId, out ForumTopicImpl topic))
                    {
                        UpdateSavedMessagesTopicOrder(new ForumTopicImpl(topic)
                        {
                            LastMessage = null
                        });

                        _clientService.Send(new GetMessageThreadHistory(_chatId, topicId, 0, 0, 1), result =>
                        {
                            if (result is Messages messages && messages.MessagesValue.Count > 0)
                            {
                                UpdateNewMessage(messages.MessagesValue[0]);
                            }
                            else if (!topic.IsPinned)
                            {
                                UpdateSavedMessagesTopicOrder(new ForumTopicImpl(topic)
                                {
                                    LastMessage = null,
                                    Order = 0
                                });
                            }
                        });
                    }
                }
            }
        }

        //public void Handle(UpdatePinnedSavedMessagesTopics update)
        //{
        //    lock (_syncRoot)
        //    {
        //        if (_order.Count > 0)
        //        {
        //            _clientService.Send(new GetPinnedSavedMessagesTopics(), UpdatePinnedSavedMessagesTopics);
        //        }
        //    }
        //}

        //private void UpdatePinnedSavedMessagesTopics(BaseObject result)
        //{
        //    if (result is not FoundSavedMessagesTopics topics)
        //    {
        //        return;
        //    }

        //    lock (_syncRoot)
        //    {
        //        var toBeRemoved = new List<OrderedTopic>(_pinnedIds.Count);
        //        var toBeAdded = new List<OrderedTopic>(topics.Topics.Count);

        //        var messages = new Dictionary<long, Message>(topics.Topics.Count);

        //        for (int i = 0; i < _pinnedIds.Count; i++)
        //        {
        //            toBeRemoved.Add(new OrderedTopic(_pinnedIds[i], long.MaxValue - i));
        //        }

        //        _pinnedIds.Clear();

        //        for (int i = 0; i < topics.Topics.Count; i++)
        //        {
        //            var id = Id(topics.Topics[i].Topic);
        //            toBeAdded.Add(new OrderedTopic(id, long.MaxValue - i));
        //            messages.Add(id, topics.Topics[i].LastMessage);

        //            _pinnedIds.Add(id);
        //        }

        //        foreach (var item in toBeAdded)
        //        {
        //            toBeRemoved.Remove(item);
        //        }

        //        foreach (var item in toBeRemoved)
        //        {
        //            toBeAdded.Remove(item);
        //        }

        //        foreach (var item in toBeRemoved)
        //        {
        //            if (_topics.TryGetValue(item.TopicId, out var topic))
        //            {
        //                UpdateSavedMessagesTopicOrder(new ForumTopicImpl(topic.Id, topic.LastMessage, false, topic.LastMessage.Id));
        //            }
        //        }

        //        foreach (var item in toBeAdded)
        //        {
        //            if (messages.TryGetValue(item.TopicId, out var message))
        //            {
        //                UpdateSavedMessagesTopicOrder(new SavedMessagesChat(item.TopicId, message, true, item.Order));
        //            }
        //        }
        //    }
        //}

        private void UpdateSavedMessagesTopics(BaseObject result)
        {
            if (result is not ForumTopics topics)
            {
                return;
            }

            lock (_syncRoot)
            {
                foreach (var topic in topics.Topics)
                {
                    var impl = new ForumTopicImpl(topic);
                    var id = impl.Id;
                    var order = Order(id, topic.LastMessage);

                    impl.Order = order;

                    UpdateSavedMessagesTopicOrder(impl);
                }
            }
        }

        private void UpdateSavedMessagesTopicOrder(ForumTopicImpl topic)
        {
            lock (_syncRoot)
            {
                var id = topic.Id;
                var order = topic.Order;

                if (_topics.TryGetValue(id, out var cached))
                {
                    var prev = cached.Order;

                    if (cached.LastMessage != null)
                    {
                        _messageToTopics.Remove(cached.LastMessage.Id);
                    }

                    if (topic.LastMessage != null)
                    {
                        _messageToTopics[topic.LastMessage.Id] = id;
                    }

                    cached.LastMessage = topic.LastMessage;
                    cached.IsPinned = topic.IsPinned;
                    cached.Order = order;

                    if (prev != order)
                    {
                        _order.Remove(new OrderedTopic(id, prev));
                        topic = cached;
                    }
                    else
                    {
                        //_aggregator.Publish(new UpdateSavedMessagesChatLastMessage(cached, cached.LastMessage, prev));
                        return;
                    }
                }
                else
                {
                    if (topic.LastMessage != null)
                    {
                        _messageToTopics[topic.LastMessage.Id] = id;
                    }

                    _topics.Add(id, topic);
                }

                _order.Add(new OrderedTopic(id, order));
                //_aggregator.Publish(new UpdateSavedMessagesChatOrder(topic, order));
            }
        }

        private long Order(long id, Message lastMessage)
        {
            //var index = _pinnedIds.IndexOf(id);
            //if (index != -1)
            //{
            //    return long.MaxValue - index;
            //}

            return lastMessage?.Id ?? id;
        }


        public Task<IList<ForumTopicImpl>> GetSavedMessagesChatsAsync(int offset, int limit)
        {
            return GetSavedMessagesTopicsAsyncImpl(offset, limit, false);
        }

        public async Task<IList<ForumTopicImpl>> GetSavedMessagesTopicsAsyncImpl(int offset, int limit, bool reentrancy)
        {
            Monitor.Enter(_syncList);

            var count = offset + limit;
            var sorted = _order;

            if (!_haveFullList && count > sorted.Count && !reentrancy)
            {
                Monitor.Exit(_syncList);

                var response = await LoadTopicsAsync(count - sorted.Count);
                if (response is Ok or Error)
                {
                    if (response is Error error)
                    {
                        if (error.Code == 404)
                        {
                            _haveFullList = true;
                        }
                        else
                        {
                            return null;
                        }
                    }

                    // Chats have already been received through updates, let's retry request
                    return await GetSavedMessagesTopicsAsyncImpl(offset, limit, true);
                }

                return null;
            }

            // Have enough chats in the chat list to answer request
            var result = new ForumTopicImpl[Math.Max(0, Math.Min(limit, sorted.Count - offset))];
            var pos = 0;

            using (var iter = sorted.GetEnumerator())
            {
                int max = Math.Min(count, sorted.Count);

                for (int i = 0; i < max; i++)
                {
                    iter.MoveNext();

                    if (i >= offset && _topics.TryGetValue(iter.Current.TopicId, out ForumTopicImpl topic))
                    {
                        result[pos++] = topic;
                    }
                }
            }

            Monitor.Exit(_syncList);
            return result;
        }

        private long _nextOffsetMessageThreadId;
        private long _nextOffsetMessageId;
        private int _nextOffsetDate;

        private async Task<BaseObject> LoadTopicsAsync(int count)
        {
            var pinned = false;
            lock (_syncRoot)
            {
                pinned = _order.Count == 0;
            }

            //if (pinned)
            //{
            //    var response = await _clientService.SendAsync(new GetPinnedSavedMessagesTopics());
            //    if (response is FoundSavedMessagesTopics found)
            //    {
            //        count -= found.Topics.Count;
            //        UpdatePinnedSavedMessagesTopics(response);
            //    }
            //    else
            //    {
            //        return response;
            //    }
            //}

            {
                var response = await _clientService.SendAsync(new GetForumTopics(_chatId, string.Empty, _nextOffsetDate, _nextOffsetMessageId, _nextOffsetMessageThreadId, 100));
                if (response is ForumTopics found)
                {
                    UpdateSavedMessagesTopics(response);

                    _nextOffsetMessageThreadId = found.NextOffsetMessageThreadId;
                    _nextOffsetMessageId = found.NextOffsetMessageId;
                    _nextOffsetDate = found.NextOffsetDate;

                    if (found.Topics.Empty())
                    {
                        return new Error(404, string.Empty);
                    }

                    return new Ok();
                }
                else
                {
                    return response;
                }
            }
        }

        private readonly struct OrderedTopic : IComparable<OrderedTopic>
        {
            public readonly long TopicId;
            public readonly long Order;

            public OrderedTopic(long topicId, long order)
            {
                TopicId = topicId;
                Order = order;
            }

            public int CompareTo(OrderedTopic o)
            {
                if (Order != o.Order)
                {
                    return o.Order < Order ? -1 : 1;
                }

                if (TopicId != o.TopicId)
                {
                    return o.TopicId < TopicId ? -1 : 1;
                }

                return 0;
            }

            public override bool Equals(object obj)
            {
                OrderedTopic o = (OrderedTopic)obj;
                return TopicId == o.TopicId && Order == o.Order;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(TopicId, Order);
            }
        }
    }

    public class ForumTopicImpl
    {
        public ForumTopicImpl(ForumTopic topic)
        {
            DraftMessage = topic.DraftMessage;
            NotificationSettings = topic.NotificationSettings;
            UnreadReactionCount = topic.UnreadReactionCount;
            UnreadMentionCount = topic.UnreadMentionCount;
            LastReadOutboxMessageId = topic.LastReadOutboxMessageId;
            LastReadInboxMessageId = topic.LastReadInboxMessageId;
            UnreadCount = topic.UnreadCount;
            IsPinned = topic.IsPinned;
            LastMessage = topic.LastMessage;
            Info = topic.Info;
        }

        public ForumTopicImpl(ForumTopicImpl topic)
        {
            Order = topic.Order;
            DraftMessage = topic.DraftMessage;
            NotificationSettings = topic.NotificationSettings;
            UnreadReactionCount = topic.UnreadReactionCount;
            UnreadMentionCount = topic.UnreadMentionCount;
            LastReadOutboxMessageId = topic.LastReadOutboxMessageId;
            LastReadInboxMessageId = topic.LastReadInboxMessageId;
            UnreadCount = topic.UnreadCount;
            IsPinned = topic.IsPinned;
            LastMessage = topic.LastMessage;
            Info = topic.Info;
        }

        public long Id => Info.MessageThreadId;

        public long Order { get; set; }

        //
        // Summary:
        //     A draft of a message in the topic; may be null if none.
        public DraftMessage DraftMessage { get; set; }

        //
        // Summary:
        //     Notification settings for the topic.
        public ChatNotificationSettings NotificationSettings { get; set; }

        //
        // Summary:
        //     Number of messages with unread reactions in the topic.
        public int UnreadReactionCount { get; set; }

        //
        // Summary:
        //     Number of unread messages with a mention/reply in the topic.
        public int UnreadMentionCount { get; set; }

        //
        // Summary:
        //     Identifier of the last read outgoing message.
        public long LastReadOutboxMessageId { get; set; }

        //
        // Summary:
        //     Identifier of the last read incoming message.
        public long LastReadInboxMessageId { get; set; }

        //
        // Summary:
        //     Number of unread messages in the topic.
        public int UnreadCount { get; set; }

        //
        // Summary:
        //     True, if the topic is pinned in the topic list.
        public bool IsPinned { get; set; }

        //
        // Summary:
        //     Last message in the topic; may be null if unknown.
        public Message LastMessage { get; set; }

        //
        // Summary:
        //     Basic information about the topic.
        public ForumTopicInfo Info { get; set; }
    }
}