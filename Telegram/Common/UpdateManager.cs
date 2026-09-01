//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace Telegram.Common
{
    /// <summary>
    /// The file update bus: every updateFile TDLib sends arrives here and goes to whoever is
    /// showing that file.
    ///
    /// This used to be the token path of the event aggregator, which spent a closure, a dispatcher
    /// work item and a Delegate.DynamicInvoke per update per subscriber. A chat auto-downloading
    /// twenty files is a few hundred of those a second, each waking the UI thread on its own.
    ///
    /// Two things make it cheaper without dropping anything a subscriber could observe:
    ///
    /// - ClientService parses every file into the instance it already holds (see GetOrCreateFile),
    ///   so a handler never sees the state its update carried, only what the object holds by the
    ///   time it runs. Queueing a second update for a file already queued would hand over the very
    ///   same object twice.
    /// - The handler type is known here, so there is nothing left to invoke reflectively.
    ///
    /// A burst for one file therefore collapses into one delivery, and a burst across files into
    /// one dispatcher hop per UI thread. The hop is taken on the first update rather than after a
    /// delay, so nothing waits that did not wait before.
    /// </summary>
    public static class UpdateManager
    {
        // Set on the token of a subscriber that only wants the update saying the file is
        // there. Placed above the session, and clear of the sign bit.
        public const long CompletionOnly = 1L << 62;

        /// <summary>
        /// Routing key for the updates of one file.
        ///
        /// The session is part of it because a file id only identifies a file within one.
        /// Ids are handles TDLib hands out one after another for every photo, thumbnail,
        /// sticker and document a session touches, and nothing bounds them, so they are
        /// given the full lower half of the token. Packed any tighter, the id of a session
        /// left running long enough reaches into the session field, and the updates for a
        /// file on one account start arriving at whoever subscribed to a file on another.
        /// </summary>
        public static long CreateToken(int sessionId, int fileId, bool completionOnly = false)
        {
            var token = ((long)sessionId << 32) | (uint)fileId;

            if (completionOnly)
            {
                token |= CompletionOnly;
            }

            return token;
        }

        #region Subscribe by ref

        public static void Subscribe(object subscriber, MessageWithOwner message, File file, ref long token, UpdateHandler<File> handler, bool completionOnly = false)
        {
            Subscribe(subscriber, message.ClientService.SessionId, file, ref token, handler, completionOnly);
        }

        public static void Subscribe(object subscriber, IClientService clientService, File file, ref long token, UpdateHandler<File> handler, bool completionOnly = false)
        {
            Subscribe(subscriber, clientService.SessionId, file, ref token, handler, completionOnly);
        }

        public static void Subscribe(object subscriber, int sessionId, File file, ref long token, UpdateHandler<File> handler, bool completionOnly = false)
        {
            var value = CreateToken(sessionId, file.Id, completionOnly);

            if (value == token)
            {
                return;
            }
            else if (token != 0)
            {
                Unsubscribe(subscriber, token);
            }

            token = value;

            while (true)
            {
                var subscription = _subscriptions.GetOrAdd(value, static _ => new Subscription());
                subscription.Subscribe(subscriber, handler);

                // A publish or an unsubscribe can drop an empty subscription between the lookup and
                // the line above, and a subscriber left on an instance the dictionary no longer
                // holds would never hear anything again - the early-out above means it would not
                // even try to subscribe a second time. Once the line above has run the instance
                // cannot be dropped any more, so seeing it still there settles it.
                if (_subscriptions.TryGetValue(value, out var current) && current == subscription)
                {
                    return;
                }
            }
        }

        #endregion

        public static void Unsubscribe(object subscriber, ref long token)
        {
            if (token != 0)
            {
                Unsubscribe(subscriber, token);
                token = 0;
            }
        }

        private static void Unsubscribe(object subscriber, long token)
        {
            if (_subscriptions.TryGetValue(token, out var subscription) && subscription.Unsubscribe(subscriber))
            {
                _subscriptions.TryRemove(token, out _);
            }
        }

        #region Publish

        private static readonly ConcurrentDictionary<long, Subscription> _subscriptions = new();

        /// <summary>
        /// Called on the TDLib thread, once per updateFile.
        /// </summary>
        public static void Publish(int sessionId, File file)
        {
            Interlocked.Increment(ref _publishes);

            var token = CreateToken(sessionId, file.Id);

            // Completion-only subscribers are a separate set on a separate token, so this is a
            // second route for the one update rather than a second update.
            if (file.Local.IsDownloadingCompleted)
            {
                Publish(token | CompletionOnly, file);
            }

            Publish(token, file);
        }

        private static void Publish(long token, File file)
        {
            if (!_subscriptions.TryGetValue(token, out var subscription))
            {
                return;
            }

            subscription.InvokeImmediate(file);

            var drains = subscription.Drains;
            if (drains == 0)
            {
                // Nobody left, and no drain that would ever find out.
                if (subscription.IsEmpty)
                {
                    _subscriptions.TryRemove(token, out _);
                }

                return;
            }

            var all = Volatile.Read(ref _drains);

            for (int i = 0; i < all.Length; i++)
            {
                if ((drains & all[i].Mask) != 0)
                {
                    all[i].Enqueue(token, file);
                }
            }
        }

        #endregion

        #region Drains

        private static readonly ConcurrentDictionary<CoreDispatcher, Drain> _drainsByDispatcher = new();
        private static readonly object _drainsLock = new();

        private static Drain[] _drains = Array.Empty<Drain>();

        // The drain of whichever UI thread this is - see GetDrain, which is the only thing allowed
        // to trust it.
        [ThreadStatic]
        private static Drain _threadDrain;

        /// <summary>
        /// The thread a subscriber has to be called on, or null for one that is called where the
        /// update arrives.
        ///
        /// A control and a view model each know a different half of the platform - a CoreDispatcher
        /// and an IDispatcherContext - and neither can be turned into the other, so a thread with
        /// both ends up with two drains and one extra work item a frame. Two call sites in the app
        /// subscribe a view model, so that is theory rather than a cost.
        /// </summary>
        private static CoreDispatcher DispatcherOf(object subscriber)
        {
            if (subscriber is FrameworkElement element)
            {
                return element.Dispatcher;
            }

            return null;
        }

        /// <summary>
        /// The same, for a subscriber that may not be there any more - false when asking threw.
        ///
        /// A control outlives its window as a managed object, but the projection behind it is
        /// disposed with the window and every call across it throws from then on, Dispatcher
        /// included. The aggregator never met this because it only ever read the dispatcher inside
        /// BeginOnUIThread, which swallows; reading it here to route an update put it in the open.
        /// </summary>
        private static bool TryGetDispatcher(object subscriber, out CoreDispatcher dispatcher)
        {
            try
            {
                dispatcher = DispatcherOf(subscriber);
                return true;
            }
            // Narrow enough to be sure of what it means: the only call in there is one projected
            // property read, so nothing else can be the thing that was disposed.
            catch (Exception ex) when (ex.IsInvalidComObject())
            {
                dispatcher = null;
                return false;
            }
        }

        /// <summary>
        /// The drain a new subscriber belongs to, or null for one to be called inline.
        ///
        /// Only for subscribing. A control subscribes from its own thread and a thread has one
        /// dispatcher, so the drain is the calling thread's - taken from a thread-static rather
        /// than by reading <see cref="DependencyObject.Dispatcher"/>, which is a projected property
        /// and this runs per cell, several times per cell, for as long as a list is scrolling.
        ///
        /// Should that ever not hold, nothing breaks: the first delivery finds the subscriber is
        /// not on its thread and routes it to the one it is on (see InvokeDeferred). That is why
        /// the guess is allowed to be a guess, and why the reroute may not use this.
        ///
        /// The cache is on the control branch alone, deliberately. A view model's dispatcher is an
        /// ordinary property rather than a projected one, so there is nothing to save by caching it,
        /// and it is an IDispatcherContext where a control's is a CoreDispatcher - one thread-static
        /// cannot stand for both, and handing a view model the control's drain would only send it
        /// round the reroute. Keying both on DispatcherQueue would make the two branches one; it
        /// would also buy nothing. Subscribing runs in the thousands a second while a list scrolls,
        /// delivering in the tens.
        /// </summary>
        private static Drain GetDrain(object subscriber)
        {
            if (subscriber is FrameworkElement element)
            {
                if (_threadDrain != null)
                {
                    return _threadDrain;
                }

                // Guarded for the same reason as the delivery side, not because it is likely: only
                // the first control to subscribe on a thread reads the property at all, and that
                // one is being built rather than torn down. A subscriber this fails for is already
                // gone, so where it lands afterwards does not matter.
                return TryGetDispatcher(element, out var dispatcher)
                    ? _threadDrain = GetOrCreateDrain(dispatcher)
                    : null;
            }

            return null;
        }

        private static Drain GetOrCreateDrain(CoreDispatcher dispatcher)
        {
            if (_drainsByDispatcher.TryGetValue(dispatcher, out var drain))
            {
                return drain;
            }

            lock (_drainsLock)
            {
                if (_drainsByDispatcher.TryGetValue(dispatcher, out drain))
                {
                    return drain;
                }

                // One bit per drain, so a publish can tell which UI threads to wake without walking
                // the subscribers on the TDLib thread. Past 32 the last bit is shared and the
                // drains behind it wake each other; nothing opens that many windows, and one pass
                // over an empty queue is what it would cost if something did.
                drain = new Drain(dispatcher, 1 << Math.Min(_drains.Length, 31));

                var updated = new Drain[_drains.Length + 1];
                Array.Copy(_drains, updated, _drains.Length);
                updated[_drains.Length] = drain;

                // Into the array before the dictionary: whoever finds the drain may set its bit
                // straight away, and a publish that then matches that bit has to find it here.
                Volatile.Write(ref _drains, updated);
                _drainsByDispatcher[dispatcher] = drain;

                return drain;
            }
        }

        /// <summary>
        /// One UI thread's queue of files with an update waiting.
        ///
        /// Tokens rather than deliveries, because a queued delivery would hold its subscriber
        /// strongly and keep a control alive for as long as its file keeps downloading.
        /// </summary>
        private sealed class Drain
        {
            private readonly CoreDispatcher _dispatcher;
            private readonly DispatchedHandler _coreHandler;

            public readonly int Mask;

            private readonly object _lock = new();

            private Dictionary<long, File> _pending = new();
            private List<long> _order = new();

            private Dictionary<long, File> _sparePending = new();
            private List<long> _spareOrder = new();

            private bool _posted;

            public Drain(CoreDispatcher dispatcher, int mask)
            {
                _dispatcher = dispatcher;
                _coreHandler = new DispatchedHandler(OnDrain);

                Mask = mask;
            }

            public void Enqueue(long token, File file)
            {
                lock (_lock)
                {
                    if (_pending.ContainsKey(token))
                    {
                        // The one number that says what the bus is worth: every update counted here
                        // is a delivery, a closure and a dispatcher work item the old path spent.
                        Interlocked.Increment(ref _collapsed);
                        return;
                    }

                    _pending[token] = file;
                    _order.Add(token);

                    if (_posted)
                    {
                        return;
                    }

                    _posted = true;
                }

                Post();
            }

            private void Post()
            {
                try
                {
                    _ = _dispatcher.RunAsync(CoreDispatcherPriority.Normal, _coreHandler);
                    Interlocked.Increment(ref _hops);
                }
                catch
                {
                    // The window is gone, so what is queued is queued for nobody - and the flag has
                    // to come back down, or nothing would ever post here again.
                    lock (_lock)
                    {
                        _pending.Clear();
                        _order.Clear();
                        _posted = false;
                    }
                }
            }

            private void OnDrain()
            {
                List<long> order;
                Dictionary<long, File> pending;

                lock (_lock)
                {
                    order = _order;
                    pending = _pending;

                    _order = _spareOrder;
                    _pending = _sparePending;

                    _spareOrder = order;
                    _sparePending = pending;

                    // Down before the batch runs rather than after: an update that arrives while a
                    // handler is running belongs to the next hop, and the queue it lands in is
                    // already the empty one.
                    _posted = false;
                }

                var delivered = 0;

                for (int i = 0; i < order.Count; i++)
                {
                    var token = order[i];

                    if (_subscriptions.TryGetValue(token, out var subscription))
                    {
                        delivered += subscription.InvokeDeferred(token, pending[token], _dispatcher);

                        if (subscription.IsEmpty)
                        {
                            _subscriptions.TryRemove(token, out _);
                        }
                    }
                }

                order.Clear();
                pending.Clear();

                if (delivered > 0)
                {
                    Interlocked.Add(ref _deliveries, delivered);
                }
            }
        }

        #endregion

        #region Subscriptions

        /// <summary>
        /// Everyone watching one file.
        /// </summary>
        private sealed class Subscription
        {
            // Weak on the subscriber, so a control that goes away with its message takes its
            // subscription with it. Nothing outside this table may hold one strongly, which is why
            // a drain queues the token and looks the subscribers up again when it runs.
            private readonly ConditionalWeakTable<object, UpdateHandler<File>> _deferred = new();

            // Subscribers with no UI thread to be dispatched to, called where the update arrives.
            // RemoteFileSource is the one that matters: it unblocks a pending read, and a frame of
            // latency there is a frame of video. Allocated only when there is one, which for all
            // but a handful of files there never is.
            private ConditionalWeakTable<object, UpdateHandler<File>> _immediate;

            // Counted for the explicit unsubscribe, which teardown does in pairs and which is
            // therefore exact. A subscriber collected without unsubscribing leaves it too high, so
            // emptiness is also decided by what a pass over the tables actually finds - never by
            // recomputing this, which would race a subscribe into nonexistence.
            private int _count;

            // What the last pass over each table found. Read together, and only right after the
            // pass that wrote them: a subscription is dropped when nothing is left in either.
            private volatile bool _deferredEmpty = true;
            private volatile bool _immediateEmpty = true;

            private int _drainMask;

            public int Drains => Volatile.Read(ref _drainMask);

            public bool IsEmpty => _deferredEmpty && _immediateEmpty;

            public void Subscribe(object subscriber, UpdateHandler<File> handler)
            {
                Interlocked.Increment(ref _count);

                var drain = GetDrain(subscriber);

                if (drain == null)
                {
                    if (_immediate == null)
                    {
                        Interlocked.CompareExchange(ref _immediate, new ConditionalWeakTable<object, UpdateHandler<File>>(), null);
                    }

                    _immediate.AddOrUpdate(subscriber, handler);
                    return;
                }

                _deferred.AddOrUpdate(subscriber, handler);

                Route(drain);
            }

            /// <summary>
            /// Marks a drain as one this file has to be queued on, and says whether that was news.
            ///
            /// Set, never cleared: a weak subscriber leaves nothing behind when it dies, so the bit
            /// cannot be retired with it. A stale one costs one pass over an empty queue.
            /// </summary>
            private bool Route(Drain drain)
            {
                int original, updated;

                do
                {
                    original = _drainMask;
                    updated = original | drain.Mask;

                    if (original == updated)
                    {
                        return false;
                    }
                }
                while (Interlocked.CompareExchange(ref _drainMask, updated, original) != original);

                return true;
            }

            public bool Unsubscribe(object subscriber)
            {
                if (_deferred.Remove(subscriber) || (_immediate != null && _immediate.Remove(subscriber)))
                {
                    return Interlocked.Decrement(ref _count) <= 0;
                }

                return false;
            }

            public void InvokeImmediate(File file)
            {
                var immediate = _immediate;
                if (immediate == null)
                {
                    return;
                }

                var empty = true;

                foreach (var entry in immediate)
                {
                    empty = false;
                    Invoke(immediate, entry.Key, entry.Value, file);
                }

                _immediateEmpty = empty;
            }

            /// <param name="dispatcher">
            /// The thread the drain that is running belongs to. Which one a subscriber is on is
            /// asked here rather than remembered, because a view model can be handed a dispatcher
            /// after it has subscribed, and is then owed its updates on that one.
            /// </param>
            public int InvokeDeferred(long token, File file, CoreDispatcher dispatcher)
            {
                var empty = true;
                var delivered = 0;

                foreach (var entry in _deferred)
                {
                    if (!TryGetDispatcher(entry.Key, out var owner))
                    {
                        // Gone with its window. Nothing will ever reach it again, and leaving it
                        // here would throw once per update for as long as the file keeps changing.
                        _deferred.Remove(entry.Key);
                        continue;
                    }

                    empty = false;

                    if (owner == dispatcher)
                    {
                        if (Invoke(_deferred, entry.Key, entry.Value, file))
                        {
                            delivered++;
                        }
                    }
                    else if (owner != null)
                    {
                        // Not on this thread. Hand the update over only if the file was not queued
                        // on its thread at all - a subscriber that moved after it subscribed, or
                        // one whose thread the subscribe guessed wrong. Doing it unconditionally
                        // would have two drains on one thread queue each other for ever, since
                        // neither sees the other's subscribers as its own.
                        var drain = GetOrCreateDrain(owner);

                        if (Route(drain))
                        {
                            drain.Enqueue(token, file);
                        }
                    }
                }

                _deferredEmpty = empty;
                return delivered;
            }

            private static bool Invoke(ConditionalWeakTable<object, UpdateHandler<File>> table, object subscriber, UpdateHandler<File> handler, File file)
            {
                try
                {
                    handler(file);
                    return true;
                }
                catch (Exception ex) when (ex.IsInvalidComObject())
                {
                    // The subscriber went with the window it belonged to. Nothing will ever reach
                    // it again, so drop it rather than throwing on every update for the file.
                    table.Remove(subscriber);
                    return false;
                }
                catch
                {
                    return true;
                }
            }
        }

        #endregion

        #region Counters

        // Not behind a switch the way TdThroughput is: that counts every payload TDLib parses, this
        // counts updateFile alone, and at a few hundred a second the meter costs less than the
        // readout that reports it.
        private static long _publishes;
        private static long _deliveries;
        private static long _collapsed;
        private static long _hops;
        private static long _since = Stopwatch.GetTimestamp();

        /// <summary>updateFile events the bus was handed.</summary>
        public static long Publishes => Interlocked.Read(ref _publishes);

        /// <summary>Handler calls that came out of them on a UI thread.</summary>
        public static long Deliveries => Interlocked.Read(ref _deliveries);

        /// <summary>
        /// Updates absorbed by one already queued for the same file, which is what the old path
        /// would have delivered on top of <see cref="Deliveries"/>. Reading both in one run is what
        /// makes this measurable without building the old path to compare against.
        /// </summary>
        public static long Collapsed => Interlocked.Read(ref _collapsed);

        /// <summary>Dispatcher work items posted to carry those calls.</summary>
        public static long Hops => Interlocked.Read(ref _hops);

        /// <summary>
        /// Time since the last reset. Counts alone do not say whether any of this is worth doing -
        /// the same total is nothing spread over ten minutes and a lot spread over ten seconds.
        /// </summary>
        public static double WallSeconds => (Stopwatch.GetTimestamp() - Interlocked.Read(ref _since)) / (double)Stopwatch.Frequency;

        public static void ResetCounters()
        {
            Interlocked.Exchange(ref _publishes, 0);
            Interlocked.Exchange(ref _deliveries, 0);
            Interlocked.Exchange(ref _collapsed, 0);
            Interlocked.Exchange(ref _hops, 0);
            Interlocked.Exchange(ref _since, Stopwatch.GetTimestamp());
        }

        #endregion
    }
}
