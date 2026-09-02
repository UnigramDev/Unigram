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
using Windows.Foundation;

namespace Telegram.Services
{
    /// <summary>
    /// One list TDLib pages - chats, forum topics, saved messages - in order, with an event for
    /// every change of order.
    /// </summary>
    /// <remarks>
    /// The authoritative copy: it holds everything TDLib has told us about for its list, whatever
    /// any view has paged in. A view keeps a
    /// <see cref="Collections.WindowedCollection{TItem, TKey, TOrder, TArgs}"/> over the top of it
    /// and is told, through <see cref="Changed"/>, of everything - including what sorts below its
    /// window, which it is free to ignore.
    /// <para/>
    /// The items themselves belong to their cache and are shared with everything else showing them;
    /// what this owns is the order. It is kept here rather than read back off the item, because the
    /// item moves as updates land: a stale read would leave the sorted set holding an entry nothing
    /// can find again.
    /// </remarks>
    public abstract partial class OrderedSourceService<TItem>
    {
        // Guards everything below. Orders arrive on TDLib's update thread while a window's UI
        // thread can be paging.
        private readonly object _lock;

        private readonly SortedSet<OrderedItem> _order = new();

        // The order each item was placed with, so that taking it out does not depend on reading
        // the item back.
        private readonly Dictionary<long, long> _orders = new();

        private bool _haveFullList;

        /// <param name="syncRoot">
        /// A lock to share, for a service holding more state that has to move with the order.
        /// </param>
        protected OrderedSourceService(object syncRoot = null)
        {
            _lock = syncRoot ?? new object();
        }

        /// <summary>
        /// The lock the order is kept under. Reentrant, so a subclass deciding an order under it
        /// can hold it across <see cref="SetOrder(long, long)"/>.
        /// </summary>
        protected object SyncRoot => _lock;

        /// <summary>
        /// Raised once per item whose order in this list changed.
        /// </summary>
        /// <remarks>
        /// Raised on whichever thread the update arrived on, so a handler has to marshal to its
        /// own. The order is carried in the arguments rather than read back from the item, because
        /// by the time a handler runs it may have moved again.
        /// </remarks>
        public event TypedEventHandler<OrderedSourceService<TItem>, OrderChangedEventArgs<TItem>> Changed;

        /// <summary>
        /// Places the item at <paramref name="order"/>, or takes it out of the list when that is
        /// zero, and reports it.
        /// </summary>
        /// <param name="lastMessage">
        /// The update also carried a new last message or draft, so a view showing the item has
        /// something to redraw even when it did not move.
        /// </param>
        protected void SetOrder(TItem item, long id, long order, bool lastMessage)
        {
            SetOrder(id, order);
            RaiseChanged(item, order, lastMessage);
        }

        /// <summary>
        /// Records the order without reporting it, for a subclass with more to settle before it
        /// can raise <see cref="Changed"/>.
        /// </summary>
        protected void SetOrder(long id, long order)
        {
            lock (_lock)
            {
                if (_orders.TryGetValue(id, out var previous))
                {
                    _order.Remove(new OrderedItem(id, previous));
                }

                if (order != 0)
                {
                    _orders[id] = order;
                    _order.Add(new OrderedItem(id, order));
                }
                else
                {
                    _orders.Remove(id);
                }
            }
        }

        protected void RaiseChanged(TItem item, long order, bool lastMessage)
        {
            Changed?.Invoke(this, new OrderChangedEventArgs<TItem>(item, order, lastMessage));
        }

        /// <summary>
        /// The order this list last placed the item at, or zero when it holds no place for it.
        /// </summary>
        public long GetOrder(long id)
        {
            lock (_lock)
            {
                return _orders.TryGetValue(id, out var order) ? order : 0;
            }
        }

        /// <summary>
        /// How many items the list holds, whatever any view has paged in.
        /// </summary>
        public int ItemCount
        {
            get
            {
                lock (_lock)
                {
                    return _order.Count;
                }
            }
        }

        public virtual void Clear()
        {
            lock (_lock)
            {
                _order.Clear();
                _orders.Clear();

                _haveFullList = false;
            }
        }

        /// <summary>
        /// A page of the list, loading from the server whatever it does not hold yet.
        /// </summary>
        protected Task<OrderedPage> GetItemsAsync(int offset, int limit)
        {
            return GetItemsAsyncImpl(offset, limit, false);
        }

        private async Task<OrderedPage> GetItemsAsyncImpl(int offset, int limit, bool reentrancy)
        {
            var count = offset + limit;

            // How many items are still to be loaded, 0 when the cache can answer on its own.
            // Decided under the lock, acted on outside it: awaiting is not allowed in there.
            int missing;

            lock (_lock)
            {
                var haveFullList = _haveFullList;

                missing = count > _order.Count && !haveFullList && !reentrancy
                    ? count - _order.Count
                    : 0;

                if (missing == 0 || (offset == 0 && _order.Count > 0))
                {
                    var result = new long[Math.Max(0, Math.Min(limit, _order.Count - offset))];
                    var pos = 0;

                    using (var iter = _order.GetEnumerator())
                    {
                        int max = Math.Min(count, _order.Count);

                        for (int i = 0; i < max; i++)
                        {
                            iter.MoveNext();

                            if (i >= offset)
                            {
                                result[pos++] = iter.Current.Id;
                            }
                        }
                    }

                    // Exhausted only once this page reaches the end of what is held: the caller
                    // asks again for the next one, and there is more of the cache to hand it.
                    return new OrderedPage(result, haveFullList && count >= _order.Count);
                }
            }

            var response = await LoadMoreItemsAsync(missing);
            if (response is Error error)
            {
                if (IsExhausted(error))
                {
                    lock (_lock)
                    {
                        _haveFullList = true;
                    }
                }
                else
                {
                    return OrderedPage.Empty;
                }
            }

            // Whatever arrived came in through updates rather than in the response, so the answer
            // is in the cache now: ask it again.
            return await GetItemsAsyncImpl(offset, limit, true);
        }

        /// <summary>
        /// Asks the server for at least <paramref name="count"/> more items, which arrive as
        /// updates rather than in the response.
        /// </summary>
        protected abstract Task<Object> LoadMoreItemsAsync(int count);

        /// <summary>
        /// Whether the error means the list is complete, as opposed to a request that failed.
        /// </summary>
        protected virtual bool IsExhausted(Error error)
        {
            return error.Code == 404;
        }
    }

    /// <param name="HaveFullList">
    /// The list holds everything, and this page reaches the end of it: there is nothing left to
    /// page.
    /// </param>
    public readonly record struct OrderedPage(long[] Ids, bool HaveFullList)
    {
        public static readonly OrderedPage Empty = new(Array.Empty<long>(), false);
    }
}
