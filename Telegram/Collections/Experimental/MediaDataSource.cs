//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace Telegram.Collections
{
    //********************************************************************************************
    //*
    //* Note: This sample uses a custom compiler constant to enable tracing. If you add
    //* TRACE_DATASOURCE to the Conditional compilation symbols of the Build tab of the
    //* Project Properties window, then the application will spit out trace data to the
    //* Output window while debugging.
    //*
    //********************************************************************************************


    /// <summary>
    /// A virtualized list of the messages in a chat that match a filter, continuing into the
    /// basic group the chat was upgraded from.
    /// </summary>
    /// <remarks>
    /// Each chat owns a contiguous run of indexes, a segment, the newest chat first. Message ids
    /// only order messages within one chat, so nothing here compares them across segments.
    /// </remarks>
    public partial class MediaDataSource : INotifyCollectionChanged, System.Collections.IList, IItemsRangeInfo, ISupportIncrementalLoading
    {
        private sealed class Segment
        {
            public readonly long ChatId;
            public readonly long SavedMessagesTopicId;
            public readonly MessageTopic Topic;

            /// <summary>
            /// The index of the segment's first message in the whole list.
            /// </summary>
            public int Offset;
            public int Count;

            /// <summary>
            /// Keyed by position within the segment.
            /// </summary>
            public SortedList<int, MessagePosition> Positions;

            /// <summary>
            /// The position of each message in <see cref="Positions"/>, by id.
            /// </summary>
            public Dictionary<long, int> Indexes;

            public void Reset(int capacity)
            {
                Positions = new SortedList<int, MessagePosition>(capacity);
                Indexes = new Dictionary<long, int>(capacity);
            }

            public bool Add(MessagePosition position)
            {
                if (Positions.TryAdd(position.Position, position))
                {
                    Indexes[position.MessageId] = position.Position;
                    return true;
                }

                return false;
            }

            public Segment(long chatId, long savedMessagesTopicId)
            {
                ChatId = chatId;
                SavedMessagesTopicId = savedMessagesTopicId;
                Topic = savedMessagesTopicId != 0
                    ? new MessageTopicSavedMessages(savedMessagesTopicId)
                    : null;
            }
        }

        private readonly IClientService _clientService;
        private readonly Segment[] _segments;

        private SearchMessagesFilter _filter;
        private int _count = 0;

        private ItemCacheManager<MessageWithOwner> _itemCache;

        private readonly SemaphoreSlim _gettingPositions = new(1);

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public MediaDataSource(IClientService clientService, long chatId, long savedMessagesTopicId, SearchMessagesFilter filter, long upgradedFromChatId = 0)
        {
            _clientService = clientService;
            _filter = filter;

            _segments = upgradedFromChatId != 0
                ? new[] { new Segment(chatId, savedMessagesTopicId), new Segment(upgradedFromChatId, 0) }
                : new[] { new Segment(chatId, savedMessagesTopicId) };

            _itemCache = CreateItemCache();
        }

        private ItemCacheManager<MessageWithOwner> CreateItemCache()
        {
            // The ItemCacheManager does most of the heavy lifting. We pass it a callback that it will use to actually fetch data, and the max size of a request
            var itemCache = new ItemCacheManager<MessageWithOwner>(FetchDataCallback, 50);
            itemCache.CacheChanged += ItemCache_CacheChanged;
            itemCache.Seam = GetSeam();
            return itemCache;
        }

        private void StopItemCache()
        {
            _itemCache.Stop();
            _itemCache.CacheChanged -= ItemCache_CacheChanged;
        }

        private int GetSeam()
        {
            return _segments.Length > 1 ? _segments[1].Offset : 0;
        }

        public async void SetFilter(SearchMessagesFilter filter)
        {
            if (_itemCache == null)
            {
                return;
            }

            StopItemCache();

            _filter = filter;
            await LoadPositionsAsync(false);

            _itemCache = CreateItemCache();
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        /// <summary>
        /// Reloads when any of <paramref name="messageIds"/> is in the list, so deleted messages
        /// leave it the way Android drops them, without shifting every later index by hand.
        /// </summary>
        public void Delete(long chatId, HashSet<long> messageIds)
        {
            if (_itemCache == null)
            {
                return;
            }

            foreach (var segment in _segments)
            {
                if (segment.ChatId != chatId)
                {
                    continue;
                }

                foreach (var messageId in messageIds)
                {
                    if (segment.Indexes?.ContainsKey(messageId) is true)
                    {
                        SetFilter(_filter);
                        return;
                    }
                }

                if (_itemCache.FindIndex(x => x.ChatId == chatId && messageIds.Contains(x.Id)) != -1)
                {
                    SetFilter(_filter);
                    return;
                }
            }
        }

        private async Task LoadPositionsAsync(bool raise)
        {
            await _gettingPositions.WaitAsync();

            try
            {
                var offset = 0;

                foreach (var segment in _segments)
                {
                    var response = await _clientService.SendAsync(new GetChatSparseMessagePositions(segment.ChatId, _filter, 0, 2000, segment.SavedMessagesTopicId));
                    if (response is MessagePositions positions)
                    {
                        segment.Reset(positions.Positions.Count);
                        segment.Count = positions.TotalCount;

                        foreach (var item in positions.Positions)
                        {
                            segment.Add(item);
                        }
                    }

                    segment.Offset = offset;
                    offset += segment.Count;
                }

                _count = offset;
                _itemCache?.Seam = GetSeam();
            }
            finally
            {
                _gettingPositions.Release();
            }

            if (raise)
            {
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }

        private Segment GetSegment(int index)
        {
            for (int i = _segments.Length - 1; i > 0; i--)
            {
                if (index >= _segments[i].Offset)
                {
                    return _segments[i];
                }
            }

            return _segments[0];
        }

        public bool HasPositions
        {
            get
            {
                int known = 0, first = 0, last = 0;

                foreach (var segment in _segments)
                {
                    if (segment.Positions == null || segment.Positions.Count == 0)
                    {
                        continue;
                    }

                    if (known == 0)
                    {
                        first = segment.Offset + segment.Positions.Keys[0];
                    }

                    last = segment.Offset + segment.Positions.Keys[^1];
                    known += segment.Positions.Count;
                }

                return known >= 2 && last - first >= 50;
            }
        }

        /// <summary>
        /// The date of the closest known message at or before <paramref name="offset"/>, a
        /// fraction of the list, or -1.
        /// </summary>
        /// <remarks>
        /// Asked on every scroll step while the date indicator shows, hence the binary search.
        /// </remarks>
        public int GetDateByOffset(double offset)
        {
            var target = (int)(Math.Clamp(offset, 0, 1) * _count);

            for (int i = _segments.Length - 1; i >= 0; i--)
            {
                var segment = _segments[i];
                if (target < segment.Offset || segment.Positions == null)
                {
                    continue;
                }

                var keys = segment.Positions.Keys;
                var local = target - segment.Offset;

                int left = 0, right = keys.Count - 1, result = -1;

                while (left <= right)
                {
                    int mid = left + (right - left) / 2;

                    if (keys[mid] <= local)
                    {
                        result = mid;
                        left = mid + 1;
                    }
                    else
                    {
                        right = mid - 1;
                    }
                }

                if (result >= 0)
                {
                    return segment.Positions.Values[result].Date;
                }
            }

            return -1;
        }

        /// <summary>
        /// The index of the oldest known message sent at or after <paramref name="date"/>, or -1.
        /// </summary>
        public int GetIndexByDate(int date)
        {
            for (int i = _segments.Length - 1; i >= 0; i--)
            {
                var segment = _segments[i];
                if (segment.Positions == null)
                {
                    continue;
                }

                // Positions count from the newest message, so dates only go down.
                var values = segment.Positions.Values;

                int left = 0, right = values.Count - 1, result = -1;

                while (left <= right)
                {
                    int mid = left + (right - left) / 2;

                    if (values[mid].Date >= date)
                    {
                        result = mid;
                        left = mid + 1;
                    }
                    else
                    {
                        right = mid - 1;
                    }
                }

                if (result >= 0)
                {
                    return segment.Offset + values[result].Position;
                }
            }

            return -1;
        }

        private static MessagePosition GetByIndex(SortedList<int, MessagePosition> positions, int targetIndex, out int index)
        {
            if (positions == null || positions.Count == 0)
            {
                index = -1;
                return null;
            }

            int left = 0, right = positions.Count - 1;
            int result = -1;

            while (left <= right)
            {
                int mid = left + (right - left) / 2;

                if (positions.Values[mid].Position >= targetIndex)
                {
                    result = mid;
                    right = mid - 1;  // Keep looking for smaller valid index
                }
                else
                {
                    left = mid + 1;
                }
            }

            index = result;
            return result >= 0 ? positions.Values[result] : positions.Values[^1];
        }

        readonly struct MessagePositionRange
        {
            public readonly long FromMessageId;
            public readonly int Offset;
            public readonly int Limit;
            public readonly int FirstIndex;

            public MessagePositionRange(long fromMessageId, int offset, int limit, int firstIndex)
            {
                FromMessageId = fromMessageId;
                Offset = offset;
                Limit = limit;
                FirstIndex = firstIndex;
            }
        }

        /// <summary>
        /// Works out the searchChatMessages request that covers <paramref name="length"/> messages
        /// from <paramref name="firstIndex"/>, both within <paramref name="segment"/>.
        /// </summary>
        private async Task<MessagePositionRange> GetPositionAsync(Segment segment, int firstIndex, int length, bool retry)
        {
            var position = GetByIndex(segment.Positions, firstIndex, out int index);
            if (segment.Positions == null || (position?.Position < firstIndex && index == -1 && retry))
            {
                await _gettingPositions.WaitAsync();

                try
                {
                    var response = await _clientService.SendAsync(new GetChatSparseMessagePositions(segment.ChatId, _filter, position?.MessageId ?? 0, 2000, segment.SavedMessagesTopicId));
                    if (response is MessagePositions positions)
                    {
                        if (segment.Positions == null)
                        {
                            segment.Reset(positions.Positions.Count);
                        }

                        foreach (var item in positions.Positions)
                        {
                            segment.Add(item);
                        }

                        return await GetPositionAsync(segment, firstIndex, length, false);
                    }
                }
                finally
                {
                    _gettingPositions.Release();
                }
            }

            if (position == null)
            {
                position = new MessagePosition(0, 0, 0);
            }

            var offset = firstIndex - position.Position;
            var limit = length;

            if (offset <= -100 && index > 0)
            {
                position = segment.Positions.Values[index - 1];
                offset = firstIndex - position.Position;
            }

            var first = position.Position;

            if (offset > 0)
            {
                first = position.Position;

                limit += offset + 1;
                offset = -1;
            }
            else
            {
                first = firstIndex;

                limit -= offset;
                offset--;
            }

            if (limit == -offset)
            {
                limit++;
            }

            return new MessagePositionRange(position.MessageId, offset, limit, first);
        }

        #region IList Implementation

        public bool Contains(object value)
        {
            return IndexOf(value) != -1;
        }

        public int IndexOf(object value)
        {
            return (value != null && _itemCache != null) ? _itemCache.IndexOf((MessageWithOwner)value) : -1;
        }

        public object this[int index]
        {
            get
            {
                // The cache will return null if it doesn't have the item. Once the item is fetched it will fire a changed event so that we can inform the list control
                return _itemCache?[index];
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public int Count => _count;

        #endregion

        //Required for the IItemsRangeInfo interface
        public void Dispose()
        {
            if (_itemCache != null)
            {
                StopItemCache();
                _itemCache = null;
            }
        }

        /// <summary>
        /// Primary method for IItemsRangeInfo interface
        /// Is called when the list control's view is changed
        /// </summary>
        /// <param name="visibleRange">The range of items that are actually visible</param>
        /// <param name="trackedItems">Additional set of ranges that the list is using, for example the buffer regions and focussed element</param>
        public void RangesChanged(ItemIndexRange visibleRange, IReadOnlyList<ItemIndexRange> trackedItems)
        {
#if TRACE_DATASOURCE
            string s = string.Format("* RangesChanged fired: Visible {0}->{1}", visibleRange.FirstIndex, visibleRange.LastIndex);
            foreach (ItemIndexRange r in trackedItems) { s += string.Format(" {0}->{1}", r.FirstIndex, r.LastIndex); }
            Debug.WriteLine(s);
#endif
            // We know that the visible range is included in the broader range so don't need to hand it to the UpdateRanges call
            // Update the cache of items based on the new set of ranges. It will callback for additional data if required
            _itemCache?.UpdateRanges(visibleRange, trackedItems.ToArray());
        }

        // Callback from itemcache that it needs items to be retrieved
        // Using this callback model abstracts the details of this specific datasource from the cache implementation
        private async Task<ItemCacheRange<MessageWithOwner>> FetchDataCallback(ItemIndexRange batch, CancellationToken ct)
        {
            await _gettingPositions.WaitAsync();
            _gettingPositions.Release();

            // The cache never asks across a seam, so the whole batch is in this segment.
            var segment = GetSegment(batch.FirstIndex);
            var position = await GetPositionAsync(segment, batch.FirstIndex - segment.Offset, (int)batch.Length, true);

            // Check if request has been cancelled, if so abort getting additional data
            if (ct.IsCancellationRequested)
            {
                return null;
            }

            var response = await _clientService.SendAsync(new SearchChatMessages(segment.ChatId, segment.Topic, string.Empty, null, position.FromMessageId, position.Offset, position.Limit, _filter));
            if (response is FoundChatMessages foundChatMessages)
            {
                var found = foundChatMessages.Messages;

                // Past the end of a segment are the next one's indexes. Past the end of the last
                // one is a message the count missed, which ItemCache_CacheChanged reloads for.
                var end = segment != _segments[^1] ? segment.Count : int.MaxValue;

                // Each message goes at its own known position. Counting from any single point is
                // wrong: TDLib leaves out of the results messages the server's positions include
                // (content it does not index under the filter, self-destructing ones), and every
                // message after one of those would land one index early. A message between known
                // positions follows the one before it; any before the first known one lead up to it.
                var next = position.FirstIndex;

                if (segment.Indexes != null)
                {
                    for (int i = 0; i < found.Count; i++)
                    {
                        if (segment.Indexes.TryGetValue(found[i].Id, out int exact))
                        {
                            next = exact - i;
                            break;
                        }
                    }
                }

                var first = -1;
                var messages = new List<MessageWithOwner>(found.Count);

                for (int i = 0; i < found.Count; i++)
                {
                    // Check if request has been cancelled, if so abort getting additional data
                    if (ct.IsCancellationRequested)
                    {
                        return null;
                    }

                    var index = next;
                    if (segment.Indexes != null && segment.Indexes.TryGetValue(found[i].Id, out int exact))
                    {
                        index = exact;
                    }

                    next = index + 1;

                    if (index < 0 || index >= end || (first >= 0 && index < first + messages.Count))
                    {
                        continue;
                    }

                    if (first < 0)
                    {
                        first = index;
                    }

                    // The messages TDLib left out stay empty.
                    while (first + messages.Count < index)
                    {
                        messages.Add(null);
                    }

                    messages.Add(new MessageWithOwner(_clientService, found[i]));
                }

                // Widened to the whole batch, so the cells the search did not reach get an answer
                // too: an index nothing settles is asked for again on every range change.
                var batchFirst = batch.FirstIndex - segment.Offset;
                var batchEnd = Math.Min(batchFirst + (int)batch.Length, end);

                if (first < 0)
                {
                    first = batchFirst;
                }

                if (first > batchFirst)
                {
                    messages.InsertRange(0, new MessageWithOwner[first - batchFirst]);
                    first = batchFirst;
                }

                while (first + messages.Count < batchEnd)
                {
                    messages.Add(null);
                }

                List<long> missing = null;

                if (segment.Positions != null)
                {
                    for (int i = 0; i < messages.Count; i++)
                    {
                        if (messages[i] == null && segment.Positions.TryGetValue(first + i, out MessagePosition known))
                        {
                            (missing ??= new List<long>()).Add(known.MessageId);
                        }
                    }
                }

                if (missing != null)
                {
                    // The search leaves out what TDLib does not index under the filter, which the
                    // server's positions still count. Asked for by id, those come back as something
                    // else and stay empty, like on Android; one the search merely did not reach,
                    // as when TDLib answers from its database and stops where that ends, fills in.
                    var response2 = await _clientService.SendAsync(new GetMessages(segment.ChatId, missing.ToVector()));
                    if (ct.IsCancellationRequested)
                    {
                        return null;
                    }

                    if (response2 is Messages byId)
                    {
                        foreach (var message in byId.MessagesValue)
                        {
                            if (message != null && Matches(_filter, message) && segment.Indexes.TryGetValue(message.Id, out int exact) && exact >= first && exact < first + messages.Count && messages[exact - first] == null)
                            {
                                messages[exact - first] = new MessageWithOwner(_clientService, message);
                            }
                        }
                    }
                }

                // An empty cell is settled for good, which only an answer for its known message
                // justifies. Edges with no known position stay out of the range and pending.
                int lead = 0, trail = messages.Count;

                while (lead < trail && messages[lead] == null && segment.Positions?.ContainsKey(first + lead) is not true)
                {
                    lead++;
                }

                while (trail > lead && messages[trail - 1] == null && segment.Positions?.ContainsKey(first + trail - 1) is not true)
                {
                    trail--;
                }

                if (lead > 0 || trail < messages.Count)
                {
                    messages = messages.GetRange(lead, trail - lead);
                    first += lead;
                }

                return new ItemCacheRange<MessageWithOwner>(segment.Offset + first, messages.Count, messages);
            }
            else if (response is Error error)
            {
                Logger.Info(string.Format("{0} {1}", error.Code, error.Message));
            }

            return new ItemCacheRange<MessageWithOwner>(segment.Offset + position.FirstIndex, 0, Array.Empty<MessageWithOwner>());
        }

        /// <summary>
        /// Whether TDLib indexes <paramref name="message"/> under <paramref name="filter"/>, so
        /// that a search with it would have returned the message.
        /// </summary>
        private static bool Matches(SearchMessagesFilter filter, Message message)
        {
            if (message.SelfDestructType != null)
            {
                return false;
            }

            return filter switch
            {
                SearchMessagesFilterPhotoAndVideo => message.Content is MessagePhoto or MessageVideo,
                SearchMessagesFilterPhoto => message.Content is MessagePhoto,
                SearchMessagesFilterVideo => message.Content is MessageVideo,
                SearchMessagesFilterDocument => message.Content is MessageDocument,
                SearchMessagesFilterAudio => message.Content is MessageAudio,
                SearchMessagesFilterVoiceAndVideoNote => message.Content is MessageVoiceNote or MessageVideoNote,
                SearchMessagesFilterVoiceNote => message.Content is MessageVoiceNote,
                SearchMessagesFilterVideoNote => message.Content is MessageVideoNote,
                SearchMessagesFilterAnimation => message.Content is MessageAnimation,
                _ => false
            };
        }

        // Event fired when items are inserted in the cache
        // Used to fire our collection changed event
        private void ItemCache_CacheChanged(object sender, CacheChangedEventArgs<MessageWithOwner> args)
        {
            if (args.ItemIndex >= _count)
            {
                SetFilter(_filter);
            }
            else if (CollectionChanged != null)
            {
                CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, args.OldItem, args.NewItem, args.ItemIndex));
            }
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return IncrementalLoading.Run(async token =>
            {
                HasMoreItems = false;

                await LoadPositionsAsync(true);
                return new LoadMoreItemsResult();
            });
        }

        public bool HasMoreItems { get; private set; } = true;

        #region Parts of IList Not Implemented

        public int Add(object value)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public void Insert(int index, object value)
        {
            throw new NotImplementedException();
        }

        public bool IsFixedSize
        {
            get { return false; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public void Remove(object value)
        {
            throw new NotImplementedException();
        }

        public void RemoveAt(int index)
        {
            throw new NotImplementedException();
        }
        public void CopyTo(Array array, int index)
        {
            throw new NotImplementedException();
        }

        public bool IsSynchronized
        {
            get { throw new NotImplementedException(); }
        }

        public object SyncRoot
        {
            get { throw new NotImplementedException(); }
        }

        public System.Collections.IEnumerator GetEnumerator()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
