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
    public partial class ClientService
    {
        private readonly SortedSet<OrderedTopic> _savedMessages = new();
        private bool _haveFullSavedMessages;

        private void SetSavedMessagesTopicOrder(SavedMessagesTopic topic, long order)
        {
            Monitor.Enter(_savedMessages);

            _savedMessages.Remove(new OrderedTopic(topic.Id, topic.Order));

            topic.Order = order;

            if (order != 0)
            {
                _savedMessages.Add(new OrderedTopic(topic.Id, order));
            }

            Monitor.Exit(_savedMessages);
        }

        public Task<IList<SavedMessagesTopic>> GetSavedMessagesChatsAsync(int offset, int limit)
        {
            return GetSavedMessagesChatsAsyncImpl(offset, limit, false);
        }

        public async Task<IList<SavedMessagesTopic>> GetSavedMessagesChatsAsyncImpl(int offset, int limit, bool reentrancy)
        {
            Monitor.Enter(_savedMessages);

            var count = offset + limit;
            var sorted = _savedMessages;

#if MOCKUP
            _haveFullChatList[index] = true;
#else
            if (!_haveFullSavedMessages && count > sorted.Count && !reentrancy)
            {
                Monitor.Exit(_savedMessages);

                var response = await SendAsync(new LoadSavedMessagesTopics(count - sorted.Count));
                if (response is Error error)
                {
                    if (error.Code == 404)
                    {
                        _haveFullSavedMessages = true;
                    }
                    else
                    {
                        return Array.Empty<SavedMessagesTopic>();
                    }
                }

                // Chats have already been received through updates, let's retry request
                return await GetSavedMessagesChatsAsyncImpl(offset, limit, true);
            }
#endif

            // Have enough chats in the chat list to answer request
            var result = new SavedMessagesTopic[Math.Max(0, Math.Min(limit, sorted.Count - offset))];
            var pos = 0;

            using (var iter = sorted.GetEnumerator())
            {
                int max = Math.Min(count, sorted.Count);

                for (int i = 0; i < max; i++)
                {
                    iter.MoveNext();

                    if (i >= offset)
                    {
                        if (_savedMessagesTopics.TryGetValue(iter.Current.TopicId, out var topic))
                        {
                            result[pos++] = topic;
                        }
                        else
                        {
                            pos++;
                        }
                    }
                }
            }

            Monitor.Exit(_savedMessages);
            return result;
        }

        private readonly struct OrderedTopic : IComparable<OrderedTopic>
        {
            public readonly long TopicId;
            public readonly long Order;

            public OrderedTopic(long chatId, long order)
            {
                TopicId = chatId;
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
