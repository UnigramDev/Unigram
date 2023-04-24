//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Td.Api;

namespace Telegram.Services
{
    public partial class ClientService
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
                chatList?.Remove(new OrderedChat(chat.Id, position));
            }

            chat.Positions = positions;

            foreach (var position in chat.Positions)
            {
                var chatList = _chatList[position.List.ToId()];
                chatList?.Add(new OrderedChat(chat.Id, position));
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

#if MOCKUP
            _haveFullChatList[index] = true;
#else
            if (!_haveFullChatList[index] && count > sorted.Count)
            {
                Monitor.Exit(_chatList);

                var response = await SendAsync(new LoadChats(chatList, count - sorted.Count));
                if (response is Ok or Error)
                {
                    if (response is Error error)
                    {
                        if (error.Code == 404)
                        {
                            _haveFullChatList[index] = true;
                        }
                        else if (error.Code == 400 && chatList is ChatListFolder chatListFolder)
                        {
                            // TODO: this is a workaround to try to recover the chat folder.
                            // Figure out the exact error here.

                            var folder = await SendAsync(new GetChatFolder(chatListFolder.ChatFolderId)) as ChatFolder;
                            if (folder != null)
                            {
                                await SendAsync(new EditChatFolder(chatListFolder.ChatFolderId, folder));
                            }
                        }
                    }

                    // Chats have already been received through updates, let's retry request
                    return await GetChatListAsync(chatList, offset, limit);
                }

                return null;
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
                        result[pos++] = iter.Current.ChatId;
                    }
                }
            }

            Monitor.Exit(_chatList);
            return new Chats(0, result);
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
            set => base[key] = value;
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
            set => base[key] = value;
        }
    }
}
