//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Telegram.Common
{
    public partial class ConcurrentQueueWorker
    {
        private readonly ConcurrentQueue<Func<Task>> taskQueue = new();
        private readonly ManualResetEvent mre = new(true);
        private readonly object o = new();
        private int _concurrentCount = 1;

        /// <summary>
        /// Max Task Count we can run concurrently
        /// </summary>
        public int MaxConcurrentCount { get; private set; }

        public ConcurrentQueueWorker(int maxConcurrentCount)
        {
            MaxConcurrentCount = maxConcurrentCount;
        }

        /// <summary>
        /// Add task into the queue and run it.
        /// </summary>
        /// <param name="tasks"></param>
        public Task Enqueue(Func<Task> task)
        {
            taskQueue.Enqueue(task);

            mre.WaitOne();

            return Task.Run(async () =>
            {
                while (true)
                {
                    if (taskQueue.Count > 0 && MaxConcurrentCount >= _concurrentCount)
                    {
                        if (taskQueue.Count > 1)
                        {
                            Logger.Info(taskQueue.Count);
                        }

                        if (taskQueue.TryDequeue(out Func<Task> nextTaskAction))
                        {
                            Interlocked.Increment(ref _concurrentCount);

                            await nextTaskAction();

                            lock (o)
                            {
                                mre.Reset();
                                Interlocked.Decrement(ref _concurrentCount);
                                mre.Set();
                            }

                            break;
                        }
                    }
                }
            });
        }
    }

    public partial class LifoActionWorker
    {
        private readonly ConcurrentStack<Action> taskQueue = new();
        private int _concurrentCount = 0;

        public void Run(Action task)
        {
            taskQueue.Push(task);

            if (0 != Interlocked.Exchange(ref _concurrentCount, 1))
            {
                return;
            }

            ThreadPool.UnsafeQueueUserWorkItem(static state =>
            {
                var self = (LifoActionWorker)state!;
                try
                {
                    do
                    {
                        while (self.taskQueue.TryPop(out var next))
                        {
                            next();
                        }

                        Interlocked.Exchange(ref self._concurrentCount, 0);

                        if (self.taskQueue.IsEmpty)
                        {
                            return;
                        }

                    } while (0 == Interlocked.Exchange(ref self._concurrentCount, 1));
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                    Interlocked.Exchange(ref self._concurrentCount, 0);
                }
            }, this);
        }
    }

    public partial class FifoActionWorker
    {
        private readonly ConcurrentQueue<Action> taskQueue = new();
        private int _concurrentCount = 0;

        public void Run(Action task)
        {
            taskQueue.Enqueue(task);

            if (0 != Interlocked.Exchange(ref _concurrentCount, 1))
            {
                return;
            }

            ThreadPool.UnsafeQueueUserWorkItem(static state =>
            {
                var self = (FifoActionWorker)state!;
                try
                {
                    do
                    {
                        while (self.taskQueue.TryDequeue(out var next))
                        {
                            next();
                        }

                        Interlocked.Exchange(ref self._concurrentCount, 0);

                        if (self.taskQueue.IsEmpty)
                        {
                            return;
                        }

                    } while (0 == Interlocked.Exchange(ref self._concurrentCount, 1));
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                    Interlocked.Exchange(ref self._concurrentCount, 0);
                }
            }, this);
        }
    }

    /// <summary>
    /// A stack of actions drained by up to <see cref="MaxConcurrency"/> threads at once, for work
    /// that is independent per item and too expensive to serialise - decoding a panel's worth of
    /// stickers, where one at a time turns a few milliseconds each into seconds of them arriving
    /// one by one.
    /// </summary>
    /// <remarks>
    /// A stack rather than a queue: the most recently pushed item is the one nearest what the user
    /// is looking at, so taking from the back follows a scroll instead of trailing it.
    ///
    /// A failing action is logged and the drain continues. Abandoning the rest of the queue
    /// because one item threw would strand everything behind it.
    /// </remarks>
    public partial class ParallelActionWorker
    {
        private readonly ConcurrentStack<Action> taskQueue = new();
        private int _concurrentCount = 0;

        public int MaxConcurrency { get; }

        public ParallelActionWorker(int maxConcurrency)
        {
            MaxConcurrency = Math.Max(1, maxConcurrency);
        }

        public void Run(Action task)
        {
            taskQueue.Push(task);
            TryStartDrain();
        }

        private void TryStartDrain()
        {
            var count = Volatile.Read(ref _concurrentCount);

            while (count < MaxConcurrency)
            {
                var previous = Interlocked.CompareExchange(ref _concurrentCount, count + 1, count);
                if (previous == count)
                {
                    ThreadPool.UnsafeQueueUserWorkItem(static state => ((ParallelActionWorker)state!).Drain(), this);
                    return;
                }

                count = previous;
            }
        }

        private void Drain()
        {
            try
            {
                while (taskQueue.TryPop(out var next))
                {
                    try
                    {
                        next();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex);
                    }
                }
            }
            finally
            {
                Interlocked.Decrement(ref _concurrentCount);

                // A push that landed between the pop that failed and the decrement above would
                // otherwise sit in the stack with nobody left to drain it.
                if (!taskQueue.IsEmpty)
                {
                    TryStartDrain();
                }
            }
        }
    }
}
