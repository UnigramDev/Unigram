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
using Telegram.Td.Api;

namespace Telegram.Services
{
    public partial class ClientService
    {
        private readonly NewDictionary<int, SortedSet<OrderedActiveStories>> _storyList = new();
        private readonly DefaultDictionary<int, bool> _haveFullStoryList = new();

        private void SetActiveStoriesPositions(ChatActiveStories next, ChatActiveStories prev)
        {
            Monitor.Enter(_storyList);
            //Monitor.Enter(chat);

            int prevIndex = -1;
            int nextIndex;

            if (prev != null)
            {
                prevIndex = prev.List switch
                {
                    StoryListArchive => 1,
                    StoryListMain or _ => 0
                };

                var storyList = _storyList[prevIndex];
                storyList?.Remove(new OrderedActiveStories(prev.ChatId, prev.Order));
            }

            {
                nextIndex = next.List switch
                {
                    StoryListArchive => 1,
                    StoryListMain or _ => 0
                };

                var storyList = _storyList[nextIndex];
                storyList?.Add(new OrderedActiveStories(next.ChatId, next.Order));
            }

            // TODO: remove when this is added to TDLib.
            if (prevIndex != nextIndex && prevIndex != -1)
            {
                _aggregator.Publish(new UpdateChatActiveStories(new ChatActiveStories(prev.ChatId, prev.List, 0, prev.MaxReadStoryId, prev.Stories)));
            }

            //Monitor.Exit(chat);
            Monitor.Exit(_storyList);
        }

        public async Task<Chats> GetStoryListAsync(StoryList storyList, int offset, int limit)
        {
            Monitor.Enter(_storyList);

            var index = storyList switch
            {
                StoryListArchive => 1,
                StoryListMain or _ => 0
            };

            var count = offset + limit;
            var sorted = _storyList[index];

#if MOCKUP
            _haveFullStoryList[index] = true;
#else
            if (!_haveFullStoryList[index] && count > sorted.Count)
            {
                Monitor.Exit(_storyList);

                var response = await SendAsync(new LoadActiveStories(storyList));
                if (response is Ok or Error)
                {
                    if (response is Error error)
                    {
                        if (error.Code == 404)
                        {
                            _haveFullStoryList[index] = true;
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
}
