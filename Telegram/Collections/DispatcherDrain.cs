//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Windows.System;

namespace Telegram.Collections
{
    /// <summary>
    /// Collects work arriving off a dispatcher and applies it there in one pass.
    /// </summary>
    /// <remarks>
    /// For a stream of updates rather than a one-off: queued instead of captured one closure at a
    /// time, and a burst costs a single post rather than one per item.
    /// <para/>
    /// The filtering that decides whether an update is worth applying stays with the caller, on
    /// the thread the update arrived on. Only accepted work belongs in here - queueing everything
    /// would wake the dispatcher for updates that were going to be discarded anyway.
    /// </remarks>
    public sealed partial class DispatcherDrain<T>
    {
        private readonly ConcurrentQueue<T> _pending = new();

        private readonly Action<DispatcherQueueHandler> _post;
        private readonly Action<List<T>> _apply;

        // Reused: the batch is handed over to be read, not kept.
        private readonly List<T> _batch = new();

        // Held rather than built per post, which is half the point of queueing.
        private readonly DispatcherQueueHandler _drain;

        private int _draining;
        private bool _disposed;

        /// <param name="post">
        /// Puts the drain on the dispatcher this belongs to. A view model passes BeginOnUIThread.
        /// </param>
        /// <param name="apply">
        /// Runs on that dispatcher with everything queued since the last pass, in order. The
        /// batch is reused between passes, so it is to be read and not retained - and handing
        /// the whole of it over is what lets a caller collapse work that supersedes itself.
        /// </param>
        public DispatcherDrain(Action<DispatcherQueueHandler> post, Action<List<T>> apply)
        {
            _post = post;
            _apply = apply;
            _drain = Drain;
        }

        /// <summary>
        /// Queues an item, from any thread.
        /// </summary>
        public void Enqueue(T item)
        {
            _pending.Enqueue(item);

            // Only the item that finds no drain scheduled posts one; the drain already on its way
            // picks up everything queued behind it.
            if (Interlocked.CompareExchange(ref _draining, 1, 0) == 0)
            {
                _post(_drain);
            }
        }

        /// <summary>
        /// Drops whatever is queued, for a caller starting over: what was collected was
        /// reported for something it no longer holds.
        /// </summary>
        public void Clear()
        {
            while (_pending.TryDequeue(out _))
            {
            }
        }

        /// <summary>
        /// Stops applying. Whatever is queued, or already posted, is abandoned.
        /// </summary>
        public void Dispose()
        {
            _disposed = true;
        }

        private void Drain()
        {
            if (_disposed)
            {
                return;
            }

            _batch.Clear();

            // Collected to exhaustion first, and applied once: an item arriving while the
            // queue is being emptied joins this batch rather than earning a second pass over
            // the same work. Dequeuing is cheap next to applying, so this settles quickly.
            do
            {
                while (_pending.TryDequeue(out var item))
                {
                    _batch.Add(item);
                }

                // Cleared before the re-check, not after: an item queued in between would
                // otherwise see a drain scheduled that is about to stop looking.
                Volatile.Write(ref _draining, 0);
            }
            while (!_pending.IsEmpty && Interlocked.CompareExchange(ref _draining, 1, 0) == 0);

            // Anything arriving from here on finds no drain scheduled and posts its own,
            // which the dispatcher runs after this one.
            if (_batch.Count > 0)
            {
                _apply(_batch);
            }
        }
    }
}
