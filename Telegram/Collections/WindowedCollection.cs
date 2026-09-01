//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using Windows.System;

namespace Telegram.Collections
{
    /// <summary>
    /// An observable window over the top of an ordered source, in order, down to the last item
    /// paged in.
    /// </summary>
    /// <remarks>
    /// The source holds everything and decides the order; this holds what a view has asked for and
    /// keeps it sorted. Anything sorting below the window is left to paging, since there is nothing
    /// up here to place it against.
    /// <para/>
    /// The order each item was placed with is kept here rather than read back from the item: the
    /// source mutates those as updates land, so a list sorted by them would be sorted by something
    /// that moves. Sorted by what it placed, the order is true by construction.
    /// </remarks>
    /// <typeparam name="TItem">What the view binds to.</typeparam>
    /// <typeparam name="TKey">Identifies an item across updates.</typeparam>
    /// <typeparam name="TOrder">What the source sorts by, paired with the key to break ties.</typeparam>
    /// <typeparam name="TArgs">What the source reports a change with, and what is queued.</typeparam>
    public abstract partial class WindowedCollection<TItem, TKey, TOrder, TArgs> : IncrementalCollection<TItem>
    {
        private readonly Dictionary<TKey, TOrder> _orders;

        private readonly DispatcherDrain<TArgs> _drain;

        // Where each key's last change sits in the batch being applied, and what it amounts to
        // once the ones it supersedes are folded in. Reused, so a burst costs no allocation.
        private readonly Dictionary<TKey, (int Index, TArgs Args)> _coalesced;

        // The last item in the window, or none while the window is still open at the bottom.
        // Recomputed after every mutation rather than tracked, so that an item leaving the
        // boundary cannot leave it pointing at an order nothing holds any more.
        private TOrder _lastOrder;
        private TKey _lastKey;
        private bool _windowed;

        protected WindowedCollection(Action<DispatcherQueueHandler> post, IEqualityComparer<TKey> comparer = null)
        {
            _orders = comparer != null ? new Dictionary<TKey, TOrder>(comparer) : new Dictionary<TKey, TOrder>();
            _coalesced = comparer != null ? new Dictionary<TKey, (int, TArgs)>(comparer) : new Dictionary<TKey, (int, TArgs)>();
            _drain = new DispatcherDrain<TArgs>(post, Apply);
        }

        protected abstract TKey GetKey(TItem item);

        protected abstract TItem GetItem(TArgs args);

        protected abstract TOrder GetOrder(TArgs args);

        /// <summary>
        /// Whether the source holds a place for something at this order, as opposed to reporting
        /// that it has none.
        /// </summary>
        protected abstract bool IsPlaced(TOrder order);

        /// <summary>
        /// Ranks one item against another: positive when the first comes first.
        /// </summary>
        protected abstract int Compare(TOrder order, TKey key, TOrder otherOrder, TKey otherKey);

        /// <summary>
        /// Queues a change, from whichever thread the source reported it on.
        /// </summary>
        public void Enqueue(TArgs args)
        {
            _drain.Enqueue(args);
        }

        /// <summary>
        /// Starting over drops what the drain has collected as well: it was reported for the
        /// list being replaced, and placing it against the one replacing it is nonsense.
        /// </summary>
        public override void Restart()
        {
            _drain.Clear();

            base.Restart();
        }

        public virtual void Dispose()
        {
            _drain.Dispose();
        }

        private void Apply(List<TArgs> batch)
        {
            // Placing is a function of the item and its order alone, so within one pass only
            // the last change for a key is worth applying: the earlier ones would each be
            // undone by the next, moving a row - or realising and dropping a container - for
            // an arrangement nothing ever sees.
            _coalesced.Clear();

            for (int i = 0; i < batch.Count; i++)
            {
                var key = GetKey(GetItem(batch[i]));

                _coalesced[key] = _coalesced.TryGetValue(key, out var previous)
                    ? (i, Merge(previous.Args, batch[i]))
                    : (i, batch[i]);
            }

            for (int i = 0; i < batch.Count; i++)
            {
                var args = batch[i];
                var item = GetItem(args);

                var coalesced = _coalesced[GetKey(item)];
                if (coalesced.Index == i)
                {
                    Place(coalesced.Args, GetItem(coalesced.Args), GetOrder(coalesced.Args), HasMoreItems);
                }

                // Never coalesced: each change carries its own delta, and something watching
                // for those needs all of them whether or not a row moved.
                OnApplied(args);
            }
        }

        /// <summary>
        /// Folds a change into the one it supersedes, for the single placement the two of them
        /// come to. The default keeps the newer one, which is right whenever nothing but the
        /// order decides where the item goes.
        /// </summary>
        protected virtual TArgs Merge(TArgs previous, TArgs next)
        {
            return next;
        }

        /// <summary>
        /// Every change, whether or not it moved a row - including one for something that
        /// sorts outside the window entirely, which none of the hooks above see.
        /// </summary>
        protected virtual void OnApplied(TArgs args)
        {
        }

        /// <summary>
        /// Puts an item where the order says it belongs, or takes it out when the source no longer
        /// holds a place for it.
        /// </summary>
        /// <param name="hasMoreItems">
        /// Passed rather than read: during a load the collection has not been told yet, and the
        /// window has to be computed against what the load found.
        /// </param>
        protected void Place(TArgs args, TItem item, TOrder order, bool hasMoreItems)
        {
            var key = GetKey(item);

            if (IsPlaced(order) && IsWithinWindow(order, key, hasMoreItems))
            {
                var next = NextIndexOf(item, key, order, out int previousIndex);

                if (next == previousIndex)
                {
                    _orders[key] = order;

                    // Recomputed even here: the item that did not move can be the last one,
                    // and the boundary is its order, which just changed.
                    UpdateWindow(hasMoreItems);
                    OnUnchanged(args, previousIndex);
                    return;
                }

                if (previousIndex >= 0)
                {
                    RemoveAt(previousIndex);
                }

                // After the removal, which takes the order out with the row.
                _orders[key] = order;

                var index = Math.Min(Count, next);
                Insert(index, item);

                UpdateWindow(hasMoreItems);
                OnPlaced(args, previousIndex, index);
            }
            // The map first: most of what a source reports sorts below the window, and that has to
            // cost a lookup rather than a scan.
            else if (ContainsKey(key))
            {
                // By key rather than by item: the source hands over a replaced instance often
                // enough, and the row to take out is the one holding the key.
                var index = IndexOfKey(key);
                if (index < 0)
                {
                    return;
                }

                RemoveAt(index);

                UpdateWindow(hasMoreItems);
                OnRemoved(args, index);
            }
        }

        /// <param name="previousIndex">Where it was, or -1 when it is new to the window.</param>
        protected virtual void OnPlaced(TArgs args, int previousIndex, int index)
        {
        }

        protected virtual void OnRemoved(TArgs args, int index)
        {
        }

        /// <summary>
        /// Already where it belongs, so only the row itself has anything new to show.
        /// </summary>
        protected virtual void OnUnchanged(TArgs args, int index)
        {
        }

        /// <summary>
        /// Whether the item sorts inside what has been paged in.
        /// </summary>
        /// <param name="hasMoreItems">
        /// Passed rather than read, for the same reason <see cref="Place"/> takes it.
        /// </param>
        protected bool IsWithinWindow(TOrder order, TKey key, bool hasMoreItems)
        {
            // Nothing left to page means the window is the whole source: an item sinking to the
            // bottom has to stay, since nothing would bring it back.
            return !hasMoreItems
                || !_windowed
                || Compare(order, key, _lastOrder, _lastKey) >= 0;
        }

        protected void UpdateWindow(bool hasMoreItems)
        {
            if (hasMoreItems && Count > 0)
            {
                var last = this[Count - 1];
                var key = GetKey(last);

                _windowed = _orders.TryGetValue(key, out _lastOrder);
                _lastKey = key;
            }
            else
            {
                _lastOrder = default;
                _lastKey = default;
                _windowed = false;
            }
        }

        /// <summary>
        /// Where the item belongs, counted over the window without it, so that
        /// <paramref name="previousIndex"/> and the result compare directly: equal means it is
        /// already in place and nothing has to move.
        /// </summary>
        protected int NextIndexOf(TItem item, TKey key, TOrder order, out int previousIndex)
        {
            previousIndex = -1;

            var next = 0;
            var index = -1;

            for (int i = 0; i < Count; i++)
            {
                var other = this[i];
                var otherKey = GetKey(other);

                if (_orders.Comparer.Equals(otherKey, key))
                {
                    previousIndex = i;
                    continue;
                }

                // A missing order means it was inserted without SetOrder, which is a bug in the
                // subclass rather than a reason to throw on the dispatcher: it sorts last.
                _orders.TryGetValue(otherKey, out var otherOrder);

                if (index < 0 && Compare(order, key, otherOrder, otherKey) >= 0)
                {
                    index = next;
                }

                next++;
            }

            return index < 0 ? next : index;
        }

        /// <summary>
        /// Whether the window holds a place for this key.
        /// </summary>
        protected bool ContainsKey(TKey key)
        {
            return _orders.ContainsKey(key);
        }

        /// <summary>
        /// Where the key sits in the window, or -1.
        /// </summary>
        protected int IndexOfKey(TKey key)
        {
            for (int i = 0; i < Count; i++)
            {
                if (_orders.Comparer.Equals(GetKey(this[i]), key))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Records the order an item was inserted with by something other than
        /// <see cref="Place"/> - a page of the source, which arrives already in order.
        /// </summary>
        protected void SetOrder(TKey key, TOrder order)
        {
            _orders[key] = order;
        }

        // So that a row taken out from anywhere - a view model deleting one optimistically,
        // as much as Place - takes its order with it, and the map holds the window and nothing
        // else. The range operations on the base write straight into Items and are the one way
        // past this: a windowed collection places items one at a time.
        protected override void RemoveItem(int index)
        {
            _orders.Remove(GetKey(this[index]));

            base.RemoveItem(index);
        }

        protected override void ClearItems()
        {
            _orders.Clear();

            _lastOrder = default;
            _lastKey = default;
            _windowed = false;

            base.ClearItems();
        }
    }

    /// <summary>
    /// What a source reports a change with, where the order alone decides where the item goes.
    /// </summary>
    /// <param name="LastMessage">
    /// The change also carried a new last message or draft, so a view showing the item has
    /// something to redraw even when it did not move.
    /// </param>
    public record OrderChangedEventArgs<T>(T Item, long Order, bool LastMessage);
}
