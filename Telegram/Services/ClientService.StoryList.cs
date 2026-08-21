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
    public partial interface ICacheService
    {
        Task<Chats> GetStoryListAsync(StoryList storyList, int offset, int limit);

        bool TryGetActiveStories(long chatId, out ChatActiveStories activeStories);

        IEnumerable<ChatActiveStories> GetActiveStorieses(IEnumerable<long> ids);
        ChatActiveStories GetActiveStories(long id);
    }

    public partial class ClientService
    {
        private readonly NewDictionary<StoryList, SortedSet<OrderedItem>> _storyList = new(StoryListEqualityComparer.Instance);
        private readonly DefaultDictionary<StoryList, bool> _haveFullStoryList = new(StoryListEqualityComparer.Instance);

        private readonly ReaderWriterDictionary<long, ChatActiveStories> _activeStories = new(100);

        private void SetActiveStoriesPositions(ChatActiveStories next, ChatActiveStories prev)
        {
            lock (_storyList)
            {
                if (prev?.List != null)
                {
                    _storyList[prev.List].Remove(new OrderedItem(prev.ChatId, prev.Order));
                }

                if (next.Order != 0)
                {
                    _storyList[next.List].Add(new OrderedItem(next.ChatId, next.Order));
                }
            }
        }

        public bool TryGetActiveStories(long id, out ChatActiveStories value)
        {
            return _activeStories.TryGetValue(id, out value);
        }

        public IEnumerable<ChatActiveStories> GetActiveStorieses(IEnumerable<long> ids)
        {
            foreach (var id in ids)
            {
                var activeStories = GetActiveStories(id);
                if (activeStories != null)
                {
                    yield return activeStories;
                }
            }
        }

        public ChatActiveStories GetActiveStories(long id)
        {
            if (_activeStories.TryGetValue(id, out ChatActiveStories value))
            {
                return value;
            }

            return null;
        }

        public Task<Chats> GetStoryListAsync(StoryList storyList, int offset, int limit)
        {
            return GetStoryListAsyncImpl(storyList, offset, limit, false);
        }

        public async Task<Chats> GetStoryListAsyncImpl(StoryList storyList, int offset, int limit, bool reentrancy)
        {
            var count = offset + limit;

            // Whether the list is still to be loaded, decided under the lock and acted on
            // outside it: awaiting is not allowed in there.
            bool load;

            lock (_storyList)
            {
                var sorted = _storyList[storyList];

                var haveFullList = _haveFullStoryList[storyList];

                load = count > sorted.Count && !haveFullList && !reentrancy;

                if (!load)
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

            var response = await SendAsync(new LoadActiveStories(storyList));
            if (response is Error error)
            {
                if (error.Code == 404)
                {
                    _haveFullStoryList[storyList] = true;
                }
                else
                {
                    return new Chats(0, Array.Empty<long>());
                }
            }

            // Chats have already been received through updates, let's retry request
            return await GetStoryListAsyncImpl(storyList, offset, limit, true);
        }
    }

    class StoryListEqualityComparer : IEqualityComparer<StoryList>
    {
        public static readonly StoryListEqualityComparer Instance = new();

        public bool Equals(StoryList x, StoryList y)
        {
            return x.AreTheSame(y);
        }

        public int GetHashCode(StoryList obj)
        {
            if (obj is StoryListMain)
            {
                return 0;
            }
            else if (obj is StoryListArchive)
            {
                return 1;
            }

            return -1;
        }
    }

}
