//
// Copyright Fela Ameghino 2015-2024
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
        private readonly NewDictionary<StoryList, SortedSet<OrderedActiveStories>> _storyList = new(StoryListEqualityComparer.Instance);
        private readonly DefaultDictionary<StoryList, bool> _haveFullStoryList = new(StoryListEqualityComparer.Instance);

        private void SetActiveStoriesPositions(ChatActiveStories next, ChatActiveStories prev)
        {
            Monitor.Enter(_storyList);

            if (prev != null)
            {
                _storyList[prev.List].Remove(new OrderedActiveStories(prev.ChatId, prev.Order));
            }

            if (next.Order != 0)
            {
                _storyList[next.List].Add(new OrderedActiveStories(next.ChatId, next.Order));
            }

            Monitor.Exit(_storyList);
        }

        public async Task<Chats> GetStoryListAsync(StoryList storyList, int offset, int limit)
        {
            Monitor.Enter(_storyList);

            var count = offset + limit;
            var sorted = _storyList[storyList];

#if MOCKUP
            _haveFullStoryList[index] = true;
#else
            if (!_haveFullStoryList[storyList] && count > sorted.Count)
            {
                Monitor.Exit(_storyList);

                var response = await SendAsync(new LoadActiveStories(storyList));
                if (response is Ok or Error)
                {
                    if (response is Error error)
                    {
                        if (error.Code == 404)
                        {
                            _haveFullStoryList[storyList] = true;
                        }
                    }

                    // Chats have already been received through updates, let's retry request
                    return await GetStoryListAsync(storyList, offset, limit);
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

            Monitor.Exit(_storyList);
            return new Chats(0, result);
        }

        private readonly struct OrderedActiveStories : IComparable<OrderedActiveStories>
        {
            public readonly long ChatId;
            public readonly long Order;

            public OrderedActiveStories(long chatId, long order)
            {
                ChatId = chatId;
                Order = order;
            }

            public int CompareTo(OrderedActiveStories o)
            {
                if (Order != o.Order)
                {
                    return o.Order < Order ? -1 : 1;
                }

                if (ChatId != o.ChatId)
                {
                    return o.ChatId < ChatId ? -1 : 1;
                }

                return 0;
            }

            public override bool Equals(object obj)
            {
                OrderedActiveStories o = (OrderedActiveStories)obj;
                return ChatId == o.ChatId && Order == o.Order;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(ChatId, Order);
            }
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
