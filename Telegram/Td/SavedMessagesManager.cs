using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.Td
{
    internal class SavedMessagesManager
    {
        private readonly IClientService _clientService;
        private readonly IEventAggregator _aggregator;

        private readonly List<long> _pinnedIds = new();

        private readonly Dictionary<long, SavedMessagesChat> _topics = new();
        private readonly SortedSet<OrderedTopic> _order = new();

        private readonly Dictionary<long, long> _messageToTopics = new();

        private readonly object _lock = new();
        private bool _haveFullList;

        public SavedMessagesManager(IClientService clientService, IEventAggregator aggregator)
        {
            _clientService = clientService;
            _aggregator = aggregator;
        }

        public void UpdateNewMessage(Message message)
        {
            if (message.SavedMessagesTopic != null && _order.Count > 0)
            {
                var id = Id(message.SavedMessagesTopic);
                var order = Order(id, message);

                var pinned = _pinnedIds.Contains(id);

                if (order > _order.Max.Order)
                {

                    UpdateSavedMessagesTopicOrder(new SavedMessagesChat(id, message, pinned, order));
                }
                else
                {
                    UpdateSavedMessagesTopicOrder(new SavedMessagesChat(id, message, pinned, 0));
                }
            }
        }

        public void UpdateDeleteMessages(IList<long> messageIds)
        {
            foreach (var id in messageIds)
            {
                if (_messageToTopics.TryGetValue(id, out var topicId) && _topics.TryGetValue(topicId, out SavedMessagesChat topic))
                {
                    // TODO: UpdateSavedMessagesTopicOrder zero or UpdateSavedMessagesTopicLastMessage null

                    UpdateSavedMessagesTopicOrder(new SavedMessagesChat(topicId, null, topic.IsPinned, topic.Order));

                    SavedMessagesTopic item = topicId switch
                    {
                        1 => new SavedMessagesTopicMyNotes(),
                        2 => new SavedMessagesTopicAuthorHidden(),
                        _ => new SavedMessagesTopicSavedFromChat(topicId)
                    };

                    _clientService.Send(new GetSavedMessagesTopicHistory(item, 0, 0, 1), UpdateSavedMessagesTopicLastMessage);
                }
            }
        }

        private void UpdateSavedMessagesTopicLastMessage(BaseObject result)
        {
            if (result is Messages messages)
            {
                UpdateNewMessage(messages.MessagesValue[0]);
            }
        }

        public void Handle(UpdatePinnedSavedMessagesTopics update)
        {
            if (_order.Count > 0)
            {
                _clientService.Send(new GetPinnedSavedMessagesTopics(), UpdatePinnedSavedMessagesTopics);
            }
        }

        private void UpdatePinnedSavedMessagesTopics(BaseObject result)
        {
            if (result is not FoundSavedMessagesTopics topics)
            {
                return;
            }

            var toBeRemoved = new List<OrderedTopic>(_pinnedIds.Count);
            var toBeAdded = new List<OrderedTopic>(topics.Topics.Count);

            var messages = new Dictionary<long, Message>(topics.Topics.Count);

            for (int i = 0; i < _pinnedIds.Count; i++)
            {
                toBeRemoved.Add(new OrderedTopic(_pinnedIds[i], long.MaxValue - i));
            }

            _pinnedIds.Clear();

            for (int i = 0; i < topics.Topics.Count; i++)
            {
                var id = Id(topics.Topics[i].Topic);
                toBeAdded.Add(new OrderedTopic(id, long.MaxValue - i));
                messages.Add(id, topics.Topics[i].LastMessage);

                _pinnedIds.Add(id);
            }

            foreach (var item in toBeAdded)
            {
                toBeRemoved.Remove(item);
            }

            foreach (var item in toBeRemoved)
            {
                toBeAdded.Remove(item);
            }

            foreach (var item in toBeRemoved)
            {
                if (_topics.TryGetValue(item.TopicId, out var topic))
                {
                    UpdateSavedMessagesTopicOrder(new SavedMessagesChat(topic.Id, topic.LastMessage, false, topic.LastMessage.Id));
                }
            }

            foreach (var item in toBeAdded)
            {
                if (messages.TryGetValue(item.TopicId, out var message))
                {
                    UpdateSavedMessagesTopicOrder(new SavedMessagesChat(item.TopicId, message, true, item.Order));
                }
            }
        }

        private void UpdateSavedMessagesTopics(BaseObject result)
        {
            if (result is not FoundSavedMessagesTopics topics)
            {
                return;
            }

            foreach (var topic in topics.Topics)
            {
                var id = Id(topic.Topic);
                var order = Order(id, topic.LastMessage);

                UpdateSavedMessagesTopicOrder(new SavedMessagesChat(id, topic.LastMessage, false, order));
            }
        }

        private void UpdateSavedMessagesTopicOrder(SavedMessagesChat topic)
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
                    _aggregator.Publish(new UpdateSavedMessagesChatLastMessage(cached, cached.LastMessage, prev));
                    return;
                }
            }
            else
            {
                _messageToTopics[topic.LastMessage.Id] = id;
                _topics.Add(id, topic);
            }

            _order.Add(new OrderedTopic(id, order));
            _aggregator.Publish(new UpdateSavedMessagesChatOrder(topic, order));
        }

        private long Id(SavedMessagesTopic topic)
        {
            return topic switch
            {
                SavedMessagesTopicSavedFromChat savedFromChat => savedFromChat.ChatId,
                SavedMessagesTopicMyNotes => 1,
                SavedMessagesTopicAuthorHidden => 2,
                _ => 2
            };
        }

        private long Order(long id, Message lastMessage)
        {
            var index = _pinnedIds.IndexOf(id);
            if (index != -1)
            {
                return long.MaxValue - index;
            }

            return lastMessage?.Id ?? 0;
        }


        public Task<IList<SavedMessagesChat>> GetSavedMessagesChatsAsync(int offset, int limit)
        {
            return GetSavedMessagesTopicsAsyncImpl(offset, limit, false);
        }

        public async Task<IList<SavedMessagesChat>> GetSavedMessagesTopicsAsyncImpl(int offset, int limit, bool reentrancy)
        {
            Monitor.Enter(_lock);

            var count = offset + limit;
            var sorted = _order;

#if MOCKUP
            _haveFullChatList[index] = true;
#else
            if (!_haveFullList && count > sorted.Count && !reentrancy)
            {
                Monitor.Exit(_lock);

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
#endif

            // Have enough chats in the chat list to answer request
            var result = new SavedMessagesChat[Math.Max(0, Math.Min(limit, sorted.Count - offset))];
            var pos = 0;

            using (var iter = sorted.GetEnumerator())
            {
                int max = Math.Min(count, sorted.Count);

                for (int i = 0; i < max; i++)
                {
                    iter.MoveNext();

                    if (i >= offset && _topics.TryGetValue(iter.Current.TopicId, out SavedMessagesChat topic))
                    {
                        result[pos++] = topic;
                    }
                }
            }

            Monitor.Exit(_lock);
            return result;
        }

        private string _nextOffset = string.Empty;

        private async Task<BaseObject> LoadTopicsAsync(int count)
        {
            if (_order.Count == 0)
            {
                var response = await _clientService.SendAsync(new GetPinnedSavedMessagesTopics());
                if (response is FoundSavedMessagesTopics found)
                {
                    count -= found.Topics.Count;
                    UpdatePinnedSavedMessagesTopics(response);
                }
                else
                {
                    return response;
                }
            }

            {
                var response = await _clientService.SendAsync(new GetSavedMessagesTopics(_nextOffset, count));
                if (response is FoundSavedMessagesTopics found)
                {
                    UpdateSavedMessagesTopics(response);

                    _nextOffset = found.NextOffset;

                    if (string.IsNullOrEmpty(found.NextOffset))
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
}
