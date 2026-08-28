//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;

namespace Telegram.Services
{
    public partial class ClientService
    {
        private readonly NewDictionary<ChatList, SortedSet<OrderedItem>> _chatList = new(ChatListEqualityComparer.Instance);
        private readonly DefaultDictionary<ChatList, bool> _haveFullChatList = new(ChatListEqualityComparer.Instance);

        private void SetChatPositions(Chat chat, Vector<ChatPosition> positions)
        {
            lock (_chatList)
            {
                foreach (var position in chat.Positions)
                {
                    _chatList[position.List].Remove(new OrderedItem(chat.Id, position.Order));
                }

                chat.Positions = positions;

                foreach (var position in chat.Positions)
                {
                    if (position.Order != 0)
                    {
                        _chatList[position.List].Add(new OrderedItem(chat.Id, position.Order));
                    }
                }
            }
        }

        public Task<Chats> GetChatListAsync(ChatList chatList, int offset, int limit)
        {
            return GetChatListAsyncImpl(chatList, offset, limit, false);
        }

        public async Task<Chats> GetChatListAsyncImpl(ChatList chatList, int offset, int limit, bool reentrancy)
        {
            var count = offset + limit;

            // How many chats are still to be loaded, 0 when the cache can answer on its own.
            // Decided under the lock, acted on outside it: awaiting is not allowed in there.
            int missing;

            lock (_chatList)
            {
                var sorted = _chatList[chatList];

                var haveFullList = _haveFullChatList[chatList];

                missing = count > sorted.Count && !haveFullList && !reentrancy
                    ? count - sorted.Count
                    : 0;

                if (missing == 0)
                {
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
                    return new Chats(haveFullList ? -1 : 0, result);
                }
            }

            var response = await SendAsync(new LoadChats(chatList, missing));
            if (response is Error error)
            {
                if (error.Code == 404)
                {
                    _haveFullChatList[chatList] = true;
                }
                else
                {
                    return new Chats(0, Array.Empty<long>());
                }
            }

            // Chats have already been received through updates, let's retry request
            return await GetChatListAsyncImpl(chatList, offset, limit, true);
        }
    }

    class ChatListEqualityComparer : IEqualityComparer<ChatList>
    {
        public static readonly ChatListEqualityComparer Instance = new();

        public bool Equals(ChatList x, ChatList y)
        {
            return x.AreTheSame(y);
        }

        public int GetHashCode(ChatList obj)
        {
            if (obj is ChatListMain or null)
            {
                return 0;
            }
            else if (obj is ChatListArchive)
            {
                return 1;
            }
            else if (obj is ChatListFolder folder)
            {
                return folder.ChatFolderId;
            }

            return -1;
        }
    }

    class NewDictionary<TKey, TValue> : Dictionary<TKey, TValue> where TValue : new()
    {
        public NewDictionary(IEqualityComparer<TKey> comparer)
            : base(comparer)
        {

        }

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
        public DefaultDictionary(IEqualityComparer<TKey> comparer)
            : base(comparer)
        {

        }

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
