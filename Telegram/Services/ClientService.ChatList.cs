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
        // One per chat list, created on demand and kept for the session. Each owns the order of
        // its own list; the chats themselves live in _chats and are shared between them.
        private readonly Dictionary<ChatList, ChatListService> _chatLists = new(ChatListEqualityComparer.Instance);

        public ChatListService GetChatList(ChatList chatList)
        {
            lock (_chatLists)
            {
                if (!_chatLists.TryGetValue(chatList, out var service))
                {
                    _chatLists[chatList] = service = new ChatListService(this, chatList);
                }

                return service;
            }
        }

        private void ClearChatLists()
        {
            lock (_chatLists)
            {
                foreach (var service in _chatLists.Values)
                {
                    service.Clear();
                }

                _pendingDeleteChats.Clear();
            }
        }

        /// <summary>
        /// Routes a chat's new positions to the list each one belongs to, and tells the lists it
        /// is leaving that it is gone.
        /// </summary>
        /// <remarks>
        /// Called with the chat's own lock held, so the orders it hands out cannot be overtaken
        /// by another update to the same chat.
        /// </remarks>
        private void SetChatPositions(Chat chat, Vector<ChatPosition> positions, bool lastMessage = false)
        {
            var previous = chat.Positions;
            chat.Positions = positions;

            // A pending delete has already taken it out of every list, and only the undo puts it
            // back: an update in the meantime must not.
            lock (_chatLists)
            {
                if (_pendingDeleteChats.ContainsKey(chat.Id))
                {
                    return;
                }
            }

            // Walked rather than gathered into a set: a chat is in a handful of lists at most, and
            // this runs on every position, last message and draft update in the account.
            foreach (var position in previous)
            {
                if (!Contains(positions, position.List))
                {
                    GetChatList(position.List).SetOrder(chat, 0, lastMessage);
                }
            }

            foreach (var position in positions)
            {
                GetChatList(position.List).SetOrder(chat, position.Order, lastMessage);
            }
        }

        private static bool Contains(Vector<ChatPosition> positions, ChatList chatList)
        {
            for (int i = 0; i < positions.Count; i++)
            {
                if (ChatListEqualityComparer.Instance.Equals(positions[i].List, chatList))
                {
                    return true;
                }
            }

            return false;
        }

        public Task<Chats> GetChatListAsync(ChatList chatList, int offset, int limit)
        {
            return GetChatList(chatList).GetChatsAsync(offset, limit);
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
