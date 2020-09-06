using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;

namespace Unigram.Services
{
    public partial class ProtoService
    {
        private readonly NewDictionary<int, SortedSet<OrderedChat>> _chatList = new NewDictionary<int, SortedSet<OrderedChat>>();
        private readonly DefaultDictionary<int, bool> _haveFullChatList = new DefaultDictionary<int, bool>();

        private void SetChatPositions(Chat chat, IList<ChatPosition> positions)
        {
            Monitor.Enter(_chatList);
            //Monitor.Enter(chat);

            foreach (var position in chat.Positions)
            {
                var chatList = _chatList[position.List.ToId()];
                if (chatList != null)
                {
                    chatList.Remove(new OrderedChat(chat.Id, position));
                }
            }

            chat.Positions = positions;

            foreach (var position in chat.Positions)
            {
                var chatList = _chatList[position.List.ToId()];
                if (chatList != null)
                {
                    chatList.Add(new OrderedChat(chat.Id, position));
                }
            }

            //Monitor.Exit(chat);
            Monitor.Exit(_chatList);
        }

        public async Task<Chats> GetChatListAsync(ChatList chatList, int offset, int limit)
        {
            Monitor.Enter(_chatList);

            var index = GetIdFromChatList(chatList);

            var count = offset + limit;
            var sorted = _chatList[index];

            if (!_haveFullChatList[index] && count > sorted.Count)
            {
                // have enough chats in the chat list or chat list is too small
                long offsetOrder = long.MaxValue;
                long offsetChatId = 0;
                if (sorted.Count > 0)
                {
                    OrderedChat last = sorted.Max;
                    offsetOrder = last.Position.Order;
                    offsetChatId = last.ChatId;
                }

                Monitor.Exit(_chatList);

                var response = await _client.SendAsync(new GetChats(chatList, offsetOrder, offsetChatId, count - sorted.Count));
                if (response is Chats chats)
                {
                    if (chats.ChatIds.Count == 0)
                    {
                        _haveFullChatList[index] = true;
                    }

                    // chats had already been received through updates, let's retry request
                    return await GetChatListAsync(chatList, offset, limit);
                }

                return null;
            }

            // have enough chats in the chat list to answer request
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
                        result[pos++] = iter.Current.ChatId;
                    }
                }
            }

            Monitor.Exit(_chatList);
            return new Chats(result);
        }

        private struct OrderedChat : IComparable<OrderedChat>
        {
            public readonly long ChatId;
            public readonly ChatPosition Position;

            public OrderedChat(long chatId, ChatPosition position)
            {
                ChatId = chatId;
                Position = position;
            }

            public int CompareTo(OrderedChat o)
            {
                if (Position.Order != o.Position.Order)
                {
                    return o.Position.Order < Position.Order ? -1 : 1;
                }

                if (ChatId != o.ChatId)
                {
                    return o.ChatId < ChatId ? -1 : 1;
                }

                return 0;
            }

            public override bool Equals(object obj)
            {
                OrderedChat o = (OrderedChat)obj;
                return ChatId == o.ChatId && Position.Order == o.Position.Order;
            }

            public override int GetHashCode()
            {
                return ChatId.GetHashCode() ^
                    Position.Order.GetHashCode();
            }
        }
    }

    class NewDictionary<TKey, TValue> : Dictionary<TKey, TValue> where TValue : new()
    {
        public new TValue this[TKey key]
        {
            get
            {
                if (TryGetValue(key, out TValue value))
                {
                    return value;
                }

                value = new TValue();
                base[key] = value;

                return value;
            }
            set
            {
                base[key] = value;
            }
        }
    }

    class DefaultDictionary<TKey, TValue> : Dictionary<TKey, TValue>
    {
        public new TValue this[TKey key]
        {
            get
            {
                if (TryGetValue(key, out TValue value))
                {
                    return value;
                }

                value = default;
                base[key] = value;

                return value;
            }
            set
            {
                base[key] = value;
            }
        }
    }
}
