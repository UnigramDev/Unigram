//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Windows.UI.Xaml;

namespace Telegram.Collections
{
    public interface ISynchronizedList
    {
        void Disconnect();
    }

    public interface ISynchronizedListDelegate<T>
    {
        /// <summary>
        /// Raised just before the removal reaches the list, while the rows are still realized, so
        /// this is where index bookkeeping belongs. The index is the one in this collection, not
        /// the one the source raised: the two differ when the mirror is reversed, and this is the
        /// space the panel indexes in.
        /// </summary>
        void Removing(int index, IList items);

        /// <summary>
        /// Raised just before the insertion reaches the list, for the same reason as
        /// <see cref="Removing"/>: index bookkeeping has to run on the pass the rows move on.
        /// </summary>
        void Inserting(int index, IList items);

        /// <summary>
        /// Raised while the row is still realized. True means the removal is worth holding back a
        /// frame for, and that whatever the delegate needed from the row has been taken.
        /// </summary>
        bool Capturing(T item);

        /// <summary>
        /// Raised once the list has caught up and the rows are gone.
        /// </summary>
        void Captured(IList<T> items);

        /// <summary>
        /// Raised when what was captured will never be used.
        /// </summary>
        void Discard();
    }

    /// <summary>
    /// Mirrors an <see cref="ObservableCollection{T}"/>, optionally one frame behind it.
    /// </summary>
    /// <remarks>
    /// The delay exists for one reason: a snapshot of a row can only be taken while the row is
    /// still realized, and a <c>CompositionVisualSurface</c> captures at the commit that follows,
    /// so the list has to be told about the removal one frame after the view model applies it.
    /// The source is never delayed, so nothing about the order updates arrive in changes.
    ///
    /// The invariant is that this collection converges to the source and never applies an event
    /// whose index was translated against a source that has since moved:
    ///
    /// <list type="bullet">
    /// <item>indices are translated on arrival — the source is read in the handler and nowhere else;</item>
    /// <item>nothing overtakes what is owed, whatever its action;</item>
    /// <item>the queue is flushed by whichever comes first of a frame, a timer, a reset, a
    /// disconnect, or an explicit <see cref="Flush"/>;</item>
    /// <item>the count is checked on every flush, and a mismatch resyncs instead of carrying the
    /// divergence forward.</item>
    /// </list>
    /// </remarks>
    public partial class SynchronizedList<T> : RangeObservableCollection<T>, ISynchronizedList
    {
        // Past this the list stops holding anything back: a pathological run of updates degrades to
        // the undeferred behaviour rather than growing without bound.
        private const int MaxQueue = 64;

        private static readonly TimeSpan Fallback = TimeSpan.FromMilliseconds(100);

        private readonly Queue<Pending> _queue = new();
        private readonly List<T> _captured = new();

        private ObservableCollection<T> _source;
        private ISynchronizedListDelegate<T> _delegate;
        private DispatcherTimer _timer;

        private bool _reverse;
        private bool _armed;
        private bool _flushing;

        /// <summary>
        /// Without one, or with <see cref="ISynchronizedListDelegate{T}.Capturing"/> refusing every
        /// row, this collection behaves exactly as it did before: applied on arrival, no delay.
        /// </summary>
        public ISynchronizedListDelegate<T> Delegate
        {
            get => _delegate;
            set
            {
                Flush();
                _delegate = value;
            }
        }

        public void UpdateSource(ObservableCollection<T> source, bool reverse)
        {
            Drop();

            if (_source != null)
            {
                _source.CollectionChanged -= OnCollectionChanged;
            }

            _source = source;
            _reverse = reverse;

            if (_source != null)
            {
                _source.CollectionChanged += OnCollectionChanged;
                ReplaceWith(reverse ? _source.Reverse() : _source);
            }
            else
            {
                Clear();
            }
        }

        // TODO: this is needed because DialogViewModel may keep loading messages
        // after the view is already unloaded, causing CollectionChanged handling to fail.
        public void Disconnect()
        {
            Drop();

            if (_source != null)
            {
                _source.CollectionChanged -= OnCollectionChanged;
            }

            _source = null;
            Clear();
        }

        /// <summary>
        /// Applies everything that is owed, now. For the exits that are not a frame: unloaded,
        /// navigated away from, suspending.
        /// </summary>
        public void Flush()
        {
            if (_flushing)
            {
                return;
            }

            Disarm();

            if (_queue.Count == 0)
            {
                return;
            }

            _flushing = true;

            while (_queue.Count > 0)
            {
                Apply(_queue.Dequeue());
            }

            // O(1), and it runs on every flush: a divergence caused by holding a frame back is
            // detectable here and recoverable, which is what makes holding one defensible.
            if (_source != null && Count != _source.Count)
            {
                Logger.Error(string.Format("SynchronizedList diverged: {0} against {1}", Count, _source.Count));
                Resync();
            }

            _flushing = false;

            if (_captured.Count > 0)
            {
                var captured = _captured.ToArray();
                _captured.Clear();

                _delegate?.Captured(captured);
            }
        }

        /// <summary>
        /// Drops what is owed <i>without</i> applying it, and everything captured for it. Only
        /// valid where whatever the queue described has been superseded outright.
        /// </summary>
        private void Drop()
        {
            Disarm();

            _queue.Clear();
            _captured.Clear();

            _delegate?.Discard();
        }

        private void Resync()
        {
            _queue.Clear();
            _captured.Clear();

            _delegate?.Discard();

            if (_source != null)
            {
                ReplaceWith(_reverse ? _source.Reverse() : _source);
            }
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                // A reset supersedes the queue outright: every index in it was translated against a
                // source that no longer exists.
                Drop();
                ReplaceWith(_reverse ? _source.Reverse() : _source);
                return;
            }

            Pending pending;

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    pending = new Pending(e.Action, _reverse ? _source.Count - e.NewStartingIndex - e.NewItems.Count : e.NewStartingIndex, e.NewItems);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    pending = new Pending(e.Action, _reverse ? _source.Count - e.OldStartingIndex : e.OldStartingIndex, e.OldItems);
                    break;
                default:
                    return;
            }

            if (e.Action == NotifyCollectionChangedAction.Remove && _delegate != null && _queue.Count < MaxQueue && Capturing(e.OldItems))
            {
                _queue.Enqueue(pending);
                Arm();
                return;
            }

            if (_queue.Count > 0)
            {
                // Nothing overtakes what is owed: a mirror that is one row stale while the next
                // event applies straight through has diverged.
                if (_queue.Count >= MaxQueue)
                {
                    Flush();
                }
                else
                {
                    _queue.Enqueue(pending);
                    return;
                }
            }

            Apply(pending);
        }

        private bool Capturing(IList items)
        {
            var any = false;

            foreach (T item in items)
            {
                if (_delegate.Capturing(item))
                {
                    _captured.Add(item);
                    any = true;
                }
            }

            return any;
        }

        private void Apply(Pending pending)
        {
            if (pending.Action == NotifyCollectionChangedAction.Add)
            {
                if (pending.Index < 0 || pending.Index > Count)
                {
                    Logger.Error(string.Format("SynchronizedList insert at {0} of {1}", pending.Index, Count));
                    Resync();
                    return;
                }

                _delegate?.Inserting(pending.Index, pending.Items);
                InsertRangeT(pending.Index, pending.Items);
            }
            else
            {
                if (pending.Index < 0 || pending.Index + pending.Items.Count > Count)
                {
                    Logger.Error(string.Format("SynchronizedList remove {0} at {1} of {2}", pending.Items.Count, pending.Index, Count));
                    Resync();
                    return;
                }

                _delegate?.Removing(pending.Index, pending.Items);
                RemoveRange(pending.Index, pending.Items.Count);
            }
        }

        private void Arm()
        {
            if (_armed)
            {
                return;
            }

            _armed = true;

            // Rendered is the intended trigger; the timer is the one that matters. A minimized or
            // occluded window renders no frames at all, and without it the list would owe that
            // frame for as long as the window stays hidden.
            CompositionTarget.Rendered += OnRendered;

            _timer ??= new DispatcherTimer { Interval = Fallback };
            _timer.Tick += OnTick;
            _timer.Start();
        }

        private void Disarm()
        {
            if (!_armed)
            {
                return;
            }

            _armed = false;

            // A static event: this has to run on every exit, including the ones that are not a flush.
            CompositionTarget.Rendered -= OnRendered;

            _timer.Tick -= OnTick;
            _timer.Stop();
        }

        private void OnRendered(object sender, object e)
        {
            Flush();
        }

        private void OnTick(object sender, object e)
        {
            Flush();
        }

        private readonly struct Pending
        {
            public readonly NotifyCollectionChangedAction Action;

            /// <summary>
            /// Where it lands here, translated when the event arrived.
            /// </summary>
            public readonly int Index;

            public readonly IList Items;

            public Pending(NotifyCollectionChangedAction action, int index, IList items)
            {
                Action = action;
                Index = index;
                Items = items;
            }
        }
    }
}
