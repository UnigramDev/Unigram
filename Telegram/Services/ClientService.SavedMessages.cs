//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Td.Api;

namespace Telegram.Services
{
    public partial interface ICacheService
    {
        int SavedMessagesTopicCount { get; }

        Task<Topics> GetSavedMessagesTopicsAsync(int offset, int limit);

        bool TryGetSavedMessagesTopic(long savedMessagesTopicId, out SavedMessagesTopic topic);

        IEnumerable<SavedMessagesTopic> GetSavedMessagesTopics(IEnumerable<long> ids);
        SavedMessagesTopic GetSavedMessagesTopic(long savedMessagesTopicId);

        string GetTitle(SavedMessagesTopic topic);
    }

    public partial class ClientService
    {
        private readonly ReaderWriterDictionary<long, SavedMessagesTopic> _savedMessagesTopics = new(100);
        private readonly SortedSet<OrderedItem> _savedMessages = new();
        private bool _haveFullSavedMessages;

        private void SetSavedMessagesTopicOrder(SavedMessagesTopic topic, long order)
        {
            Monitor.Enter(_savedMessages);

            _savedMessages.Remove(new OrderedItem(topic.Id, topic.Order));

            topic.Order = order;

            if (order != 0)
            {
                _savedMessages.Add(new OrderedItem(topic.Id, order));
            }

            Monitor.Exit(_savedMessages);
        }

        public int SavedMessagesTopicCount { get; private set; }

        public bool TryGetSavedMessagesTopic(long savedMessagesTopicId, out SavedMessagesTopic topic)
        {
            return _savedMessagesTopics.TryGetValue(savedMessagesTopicId, out topic);
        }

        public IEnumerable<SavedMessagesTopic> GetSavedMessagesTopics(IEnumerable<long> ids)
        {
            foreach (var id in ids)
            {
                var topic = GetSavedMessagesTopic(id);
                if (topic != null)
                {
                    yield return topic;
                }
            }
        }

        public SavedMessagesTopic GetSavedMessagesTopic(long savedMessagesTopicId)
        {
            if (_savedMessagesTopics.TryGetValue(savedMessagesTopicId, out SavedMessagesTopic value))
            {
                return value;
            }

            return null;
        }

        public string GetTitle(SavedMessagesTopic topic)
        {
            if (topic?.Type is SavedMessagesTopicTypeMyNotes)
            {
                return Strings.MyNotes;
            }
            else if (topic?.Type is SavedMessagesTopicTypeAuthorHidden)
            {
                return Strings.AnonymousForward;
            }
            else if (topic?.Type is SavedMessagesTopicTypeSavedFromChat savedFromChat && TryGetChat(savedFromChat.ChatId, out Chat chat))
            {
                return GetTitle(chat);
            }

            return Strings.AnonymousForward;
        }

        public Task<Topics> GetSavedMessagesTopicsAsync(int offset, int limit)
        {
            return GetSavedMessagesChatsAsyncImpl(offset, limit, false);
        }

        public async Task<Topics> GetSavedMessagesChatsAsyncImpl(int offset, int limit, bool reentrancy)
        {
            Monitor.Enter(_savedMessages);

            var count = offset + limit;
            var sorted = _savedMessages;

            var haveFullList = _haveFullSavedMessages;

#if MOCKUP
            _haveFullChatList[index] = true;
#else
            if (count > sorted.Count && !haveFullList && !reentrancy)
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
                        return new Topics(0, Array.Empty<long>());
                    }
                }

                // Chats have already been received through updates, let's retry request
                return await GetSavedMessagesChatsAsyncImpl(offset, limit, true);
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

            Monitor.Exit(_savedMessages);
            return new Topics(haveFullList ? -1 : 0, result);
        }
    }
}

namespace Telegram.Td.Api
{

}
