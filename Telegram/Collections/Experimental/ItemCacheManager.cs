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
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Telegram.Collections
{
    // EventArgs class for the CacheChanged event 
    public class CacheChangedEventArgs<T> : EventArgs
    {
        public T OldItem { get; set; }
        public T NewItem { get; set; }
        public int ItemIndex { get; set; }
    }

    public class ItemCacheRange<T>
    {
        public ItemIndexRange Range { get; }

        public IList<T> Items { get; }

        public ItemCacheRange(int firstIndex, int length, IList<T> items)
        {
            Range = new ItemIndexRange(firstIndex, (uint)length);
            Items = items;
        }
    }

    // Implements a relatively simple cache for items based on a set of ranges
    class ItemCacheManager<T>
    {
        // data structure to hold all the items that are in the ranges the cache manager is looking after
        private List<CacheEntryBlock> _cacheBlocks;

        // List of ranges for items that are not present in the cache
        internal ItemIndexRangeList _requests;

        private ItemIndexRange[] _trackedRanges;

        // list of ranges for items that are present in the cache
        private ItemIndexRangeList _cachedResults;
        // Range of items that is currently being requested
        private ItemIndexRange _requestInProgress;
        // Used to be able to cancel outstanding requests
        private CancellationTokenSource _cancelTokenSource;
        // Callback that will be used to request data
        private fetchDataCallbackHandler _fetchDataCallback;
        // Maximum number of items that can be fetched in one batch
        private int _maxBatchFetchSize;
        // Timer to optimize the the fetching of data so we throttle requests if the list is still changing
        private DispatcherTimer _timer;
        private bool _stopped;

#if DEBUG
        // Name for trace messages, and when debugging so you know which instance of the cache manager you are dealing with
        string _debugName = string.Empty;
#endif
        public ItemCacheManager(fetchDataCallbackHandler callback, int batchsize = 50, string debugName = "ItemCacheManager")
        {
            _cacheBlocks = new List<CacheEntryBlock>();
            _requests = new ItemIndexRangeList();
            _cachedResults = new ItemIndexRangeList();
            _fetchDataCallback = callback;
            _maxBatchFetchSize = batchsize;
            //set up a timer that is used to delay fetching data so that we can catch up if the list is scrolling fast
            _timer = new Windows.UI.Xaml.DispatcherTimer();
            _timer.Tick += (sender, args) =>
            {
                FetchData();
            };
            _timer.Interval = new TimeSpan(20 * 10000);

#if DEBUG
            _debugName = debugName;
#endif
#if TRACE_DATASOURCE
            Debug.WriteLine(debugName + "* Cache initialized/reset");
#endif
        }

        public event TypedEventHandler<object, CacheChangedEventArgs<T>> CacheChanged;

        public void Stop()
        {
            _stopped = true;
            _cancelTokenSource?.Cancel();
            _timer.Stop();
        }

        /// <summary>
        /// Indexer for access to the item cache
        /// </summary>
        /// <param name="index">Item Index</param>
        /// <returns></returns>
        public T this[int index]
        {
            get
            {
                // iterates through the cache blocks to find the item
                foreach (CacheEntryBlock block in _cacheBlocks)
                {
                    if (index >= block.FirstIndex && index <= block.LastIndex)
                    {
                        return block.Items[index - block.FirstIndex];
                    }
                }
                return default(T);
            }
            set
            {
                // iterates through the cache blocks to find the right block
                for (int i = 0; i < _cacheBlocks.Count; i++)
                {
                    CacheEntryBlock block = _cacheBlocks[i];
                    if (index >= block.FirstIndex && index <= block.LastIndex)
                    {
                        block.Items[index - block.FirstIndex] = value;
                        //register that we have the result in the cache
                        if (value != null) { _cachedResults.Add((uint)index, 1); }
                        return;
                    }
                    // We have moved past the block where the item is supposed to live
                    if (block.FirstIndex > index)
                    {
                        AddOrExtendBlock(index, value, i);
                        return;
                    }
                }

                // No blocks exist, so creating a new block
                AddOrExtendBlock(index, value, _cacheBlocks.Count);
            }
        }

        // Extends an existing block if the item fits at the end, or creates a new block
        private void AddOrExtendBlock(int index, T value, int insertBeforeBlock)
        {
            if (insertBeforeBlock > 0)
            {
                CacheEntryBlock block = _cacheBlocks[insertBeforeBlock - 1];
                if (block.LastIndex == index - 1)
                {
                    T[] newItems = new T[block.Length + 1];
                    Array.Copy(block.Items, newItems, (int)block.Length);
                    newItems[block.Length] = value;
                    _cacheBlocks[insertBeforeBlock - 1] = new CacheEntryBlock(block.FirstIndex, newItems);
                    return;
                }
            }

            CacheEntryBlock newBlock = new(index, new T[] { value });
            _cacheBlocks.Insert(insertBeforeBlock, newBlock);
        }

        /// <summary>
        /// Updates the desired item range of the cache, discarding items that are not needed, and figuring out which items need to be requested. It will then kick off a fetch if required.
        /// </summary>
        /// <param name="ranges">New set of ranges the cache should hold</param>
        public void UpdateRanges(ItemIndexRange visibleRange, ItemIndexRange[] ranges)
        {
            //Normalize ranges to get a unique set of discontinuous ranges
            ranges = NormalizeRanges(ranges);

            // Fail fast if the ranges haven't changed
            if (!HasRangesChanged(ranges)) { return; }

            //figure out what items need to be fetched because we don't have them in the cache
            _requests = new ItemIndexRangeList(ranges);
            _trackedRanges = ranges;

            foreach (CacheEntryBlock cached in _cacheBlocks)
            {
                _requests.Subtract(new ItemIndexRange(cached.FirstIndex, cached.Length));
            }

            StartFetchData();

#if TRACE_DATASOURCE
            s = "└ Pending requests: ";
            foreach (ItemIndexRange range in requests)
            {
                s += range.FirstIndex + "->" + range.LastIndex + " ";
            }
            Debug.WriteLine(s);
#endif 
        }

        // Compares the new ranges against the previous ones to see if they have changed
        private bool HasRangesChanged(ItemIndexRange[] ranges)
        {
            if (_trackedRanges?.Length != ranges.Length)
            {
                return true;
            }

            for (int i = 0; i < ranges.Length; i++)
            {
                ItemIndexRange r = ranges[i];
                ItemIndexRange block = _trackedRanges[i];
                if (r.FirstIndex != block.FirstIndex || r.LastIndex != block.LastIndex)
                {
                    return true;
                }
            }

            return false;
        }

        // Gets the first block of items that we don't have values for
        public ItemIndexRange GetFirstRequestBlock(int maxsize = 50)
        {
            if (_requests.Count > 0)
            {
                var range = _requests[0];
                if (range.Length > maxsize)
                {
                    range = new ItemIndexRange(range.FirstIndex, (uint)maxsize);
                }
                return range;
            }
            return null;
        }

        // Throttling function for fetching data. Forces a wait of 20ms before making the request.
        // If another fetch is requested in that time, it will reset the timer, so we don't fetch data if the view is actively scrolling
        public void StartFetchData()
        {
            if (_stopped)
            {
                return;
            }

            // Verify if an active request is still needed
            if (_requestInProgress != null)
            {
                if (_requests.Intersects(_requestInProgress))
                {
                    return;
                }
                else
                {
                    //cancel the existing request
#if TRACE_DATASOURCE
                    Debug.WriteLine("> " + debugName + " Cancelling request: " + requestInProgress.FirstIndex + "->" + requestInProgress.LastIndex);
#endif
                    _cancelTokenSource.Cancel();
                }
            }

            //Using a timer to delay fetching data by 20ms, if another range comes in that time, then the timer is reset.
            _timer.Stop();
            _timer.Start();
        }

        public delegate Task<ItemCacheRange<T>> fetchDataCallbackHandler(ItemIndexRange range, CancellationToken ct);

        // Called by the timer to make a request for data
        public async void FetchData()
        {
            //Stop the timer so we don't get fired again unless data is requested
            _timer.Stop();

            if (_stopped)
            {
                return;
            }

            if (_requestInProgress != null)
            {
                // Verify if an active request is still needed
                if (_requests.Intersects(_requestInProgress))
                {
                    return;
                }
                else
                {
                    // Cancel the existing request
#if TRACE_DATASOURCE
                    Debug.WriteLine(">" + debugName + " Cancelling request: " + requestInProgress.FirstIndex + "->" + requestInProgress.LastIndex);
#endif
                    _cancelTokenSource.Cancel();
                }
            }

            ItemIndexRange nextRequest = GetFirstRequestBlock(_maxBatchFetchSize);
            if (nextRequest != null)
            {
                _cancelTokenSource = new CancellationTokenSource();
                CancellationToken ct = _cancelTokenSource.Token;
                _requestInProgress = nextRequest;
                ItemCacheRange<T> data = null;
                try
                {
#if TRACE_DATASOURCE
                    Debug.WriteLine(">" + debugName + " Fetching items " + nextRequest.FirstIndex + "->" + nextRequest.LastIndex);
#endif
                    // Use the callback to get the data, passing in a cancellation token
                    data = await _fetchDataCallback(nextRequest, ct);

                    if (data != null && !ct.IsCancellationRequested)
                    {
#if TRACE_DATASOURCE
                        Debug.WriteLine(">" + debugName + " Inserting items into cache at: " + nextRequest.FirstIndex + "->" + (nextRequest.FirstIndex + data.Length - 1));
#endif
                        for (int i = 0; i < data.Items.Count; i++)
                        {
                            int cacheIndex = (int)(data.Range.FirstIndex + i);

                            T oldItem = this[cacheIndex];
                            T newItem = data.Items[i];

                            if (!newItem.Equals(oldItem))
                            {
                                this[cacheIndex] = newItem;

                                // Fire CacheChanged so that the datasource can fire its INCC event, and do other work based on the item having data
                                if (CacheChanged != null)
                                {
                                    CacheChanged(this, new CacheChangedEventArgs<T>() { OldItem = oldItem, NewItem = newItem, ItemIndex = cacheIndex });
                                }
                            }
                        }

                        _requests.Subtract(data.Range);
                    }
                }
                // Try/Catch is needed as cancellation is via an exception
                catch (OperationCanceledException) { }
                finally
                {
                    _requestInProgress = null;
                    // Start another request if required
                    FetchData();
                }
            }
        }


        /// <summary>
        /// Merges a set of ranges to form a new set of non-contiguous ranges
        /// </summary>
        /// <param name="ranges">The list of ranges to merge</param>
        /// <returns>A smaller set of merged ranges</returns>
        private ItemIndexRange[] NormalizeRanges(ItemIndexRange[] ranges)
        {
            List<ItemIndexRange> results = new();
            foreach (ItemIndexRange range in ranges)
            {
                bool handled = false;
                for (int i = 0; i < results.Count; i++)
                {
                    ItemIndexRange existing = results[i];
                    if (range.ContiguousOrOverlaps(existing))
                    {
                        results[i] = existing.Combine(range);
                        handled = true;
                        break;
                    }
                    else if (range.FirstIndex < existing.FirstIndex)
                    {
                        results.Insert(i, range);
                        handled = true;
                        break;
                    }
                }
                if (!handled) { results.Add(range); }
            }
            return results.ToArray();
        }


        // Sees if the value is in our cache if so it returns the index
        public int IndexOf(T value)
        {
            foreach (CacheEntryBlock entry in _cacheBlocks)
            {
                int index = Array.IndexOf<T>(entry.Items, value);
                if (index != -1) return index + entry.FirstIndex;
            }
            return -1;
        }

        // Type for the cache blocks
        class CacheEntryBlock
        {
            public T[] Items { get; }

            public int FirstIndex { get; }

            public int LastIndex { get; }

            public uint Length { get; }

            public CacheEntryBlock(int firstIndex, T[] items)
            {
                Items = items;
                FirstIndex = firstIndex;
                LastIndex = firstIndex + items.Length - 1;
                Length = (uint)items.Length;
            }
        }
    }
}
