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
    /// A custom datasource over the file system that supports data virtualization
    /// </summary>
    public partial class MediaDataSource : INotifyCollectionChanged, System.Collections.IList, IItemsRangeInfo, ISupportIncrementalLoading
    {
        private readonly IClientService _clientService;
        private readonly long _chatId;
        private readonly long _savedMessagesTopicId;

        private SearchMessagesFilter _filter;
        private int _count = 0;

        private ItemCacheManager<MessageWithOwner> _itemCache;
        private SortedList<int, MessagePosition> _positions;

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public MediaDataSource(IClientService clientService, long chatId, long savedMessagesTopicId, SearchMessagesFilter filter)
        {
            _clientService = clientService;
            _chatId = chatId;
            _savedMessagesTopicId = savedMessagesTopicId;
            _filter = filter;

            // The ItemCacheManager does most of the heavy lifting. We pass it a callback that it will use to actually fetch data, and the max size of a request
            _itemCache = new ItemCacheManager<MessageWithOwner>(FetchDataCallback, 50);
            _itemCache.CacheChanged += ItemCache_CacheChanged;
        }

        // Factory method to create the datasource
        // Requires async work which is why it needs a factory rather than being part of the constructor
        public static async Task<MediaDataSource> Create(IClientService clientService, long chatId, long savedMessagesTopicId, SearchMessagesFilter filter)
        {
            MediaDataSource ds = new(clientService, chatId, savedMessagesTopicId, filter);
            await ds.UpdateCount(false, false);
            return ds;
        }

        public async void SetFilter(SearchMessagesFilter filter)
        {
            if (_itemCache != null)
            {
                _itemCache.Stop();
                _itemCache.CacheChanged -= ItemCache_CacheChanged;
            }

            _filter = filter;
            await UpdateCount(true, false);

            _itemCache = new ItemCacheManager<MessageWithOwner>(FetchDataCallback, 50);
            _itemCache.CacheChanged += ItemCache_CacheChanged;

            if (CollectionChanged != null)
            {
                CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }

        private async Task UpdateCount(bool getSparseMessages, bool raise)
        {
            await _gettingPositions.WaitAsync();

            if (getSparseMessages)
            {
                var response = await _clientService.SendAsync(new GetChatSparseMessagePositions(_chatId, _filter, 0, 2000, _savedMessagesTopicId));
                if (response is MessagePositions positions)
                {
                    _positions = new SortedList<int, MessagePosition>(positions.Positions.ToDictionary(x => x.Position));
                    _count = positions.TotalCount;
                }
            }
            else
            {
                var topic = _savedMessagesTopicId != 0
                    ? new MessageTopicSavedMessages(_savedMessagesTopicId)
                    : null;

                var response = await _clientService.SendAsync(new GetChatMessageCount(_chatId, topic, _filter, false)) as Count;
                if (response is Count count)
                {
                    _positions = null;
                    _count = count.CountValue;
                }
            }

            _gettingPositions.Release();

            if (CollectionChanged != null && raise)
            {
                CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }

        public bool HasPositions
        {
            get
            {
                if (_positions == null || _positions.Count < 2)
                {
                    return false;
                }

                return _positions.Values[^1].Position - _positions.Values[0].Position >= 50;
            }
        }

        public MessagePosition GetByDate(int targetDate)
        {
            return _positions?.Values.LastOrDefault(x => x.Date >= targetDate);
        }

        public MessagePosition GetByOffset(double offset)
        {
            offset = Math.Clamp(offset, 0, 1);
            return _positions?.Values.LastOrDefault(x => x.Position <= offset * _count);
        }

        public MessagePosition GetByIndex(int targetIndex, out int index)
        {
            if (_positions == null || _positions.Count == 0)
            {
                index = -1;
                return null;
            }

            int left = 0, right = _positions.Count - 1;
            int result = -1;

            while (left <= right)
            {
                int mid = left + (right - left) / 2;

                if (_positions.Values[mid].Position >= targetIndex)
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
            return result >= 0 ? _positions.Values[result] : _positions.Values[^1];
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

        private SemaphoreSlim _gettingPositions = new(1);

        private async Task<MessagePositionRange> GetPositionAsync(ItemIndexRange batch, bool retry)
        {
            var position = GetByIndex(batch.FirstIndex, out int index);
            if (_positions == null || (position?.Position < batch.FirstIndex && index == -1 && retry))
            {
                await _gettingPositions.WaitAsync();

                try
                {
                    var response = await _clientService.SendAsync(new GetChatSparseMessagePositions(_chatId, _filter, position?.MessageId ?? 0, 2000, _savedMessagesTopicId));
                    if (response is MessagePositions positions)
                    {
                        if (_positions == null)
                        {
                            _positions = new SortedList<int, MessagePosition>(positions.Positions.ToDictionary(x => x.Position));
                        }
                        else
                        {
                            foreach (var item in positions.Positions)
                            {
                                _positions.TryAdd(item.Position, item);
                            }
                        }

                        return await GetPositionAsync(batch, false);
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

            var offset = batch.FirstIndex - position.Position;
            var limit = (int)batch.Length;

            if (offset <= -100 && index > 0)
            {
                position = _positions.Values[index - 1];
                offset = batch.FirstIndex - position.Position;
            }

            var firstIndex = position.Position;

            if (offset > 0)
            {
                firstIndex = position.Position;

                limit += offset + 1;
                offset = -1;
            }
            else
            {
                firstIndex = batch.FirstIndex;

                limit -= offset;
                offset--;
            }

            if (limit == -offset)
            {
                limit++;
            }

            return new MessagePositionRange(position.MessageId, offset, limit, firstIndex);
        }

        #region IList Implementation

        public bool Contains(object value)
        {
            return IndexOf(value) != -1;
        }

        public int IndexOf(object value)
        {
            return (value != null) ? _itemCache.IndexOf((MessageWithOwner)value) : -1;
        }

        public object this[int index]
        {
            get
            {
                // The cache will return null if it doesn't have the item. Once the item is fetched it will fire a changed event so that we can inform the list control
                return _itemCache[index];
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
            _itemCache = null;
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
            _itemCache.UpdateRanges(visibleRange, trackedItems.ToArray());
        }

        // Callback from itemcache that it needs items to be retrieved
        // Using this callback model abstracts the details of this specific datasource from the cache implementation
        private async Task<ItemCacheRange<MessageWithOwner>> FetchDataCallback(ItemIndexRange batch, CancellationToken ct)
        {
            List<MessageWithOwner> messages = new();

            await _gettingPositions.WaitAsync();
            _gettingPositions.Release();

            var position = await GetPositionAsync(batch, true);

            // Check if request has been cancelled, if so abort getting additional data
            if (ct.IsCancellationRequested)
            {
                return null;
            }

            MessageTopic messageTopic = null;
            if (_savedMessagesTopicId != 0)
            {
                messageTopic = new MessageTopicSavedMessages(_savedMessagesTopicId);
            }

            var response = await _clientService.SendAsync(new SearchChatMessages(_chatId, messageTopic, string.Empty, null, position.FromMessageId, position.Offset, position.Limit, _filter));
            if (response is FoundChatMessages foundChatMessages)
            {
                for (int i = 0; i < foundChatMessages.Messages.Count; i++)
                {
                    // Check if request has been cancelled, if so abort getting additional data
                    if (ct.IsCancellationRequested)
                    {
                        return null;
                    }

                    messages.Add(new MessageWithOwner(_clientService, foundChatMessages.Messages[i]));
                }
            }
            else
            {
                Logger.Info(response);
            }

            return new ItemCacheRange<MessageWithOwner>(position.FirstIndex, messages.Count, messages);
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
            return AsyncInfo.Run(async token =>
            {
                HasMoreItems = false;

                await UpdateCount(true, true);
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
