//
// Copyright Fela Ameghino 2015-2025
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

        public void Enqueue(Action task)
        {
            taskQueue.Push(task);

            if (0 != Interlocked.Exchange(ref _concurrentCount, 1))
            {
                return;
            }

            _ = Task.Run(() =>
            {
                while (taskQueue.TryPop(out Action nextTaskAction))
                {
                    taskQueue.Clear();
                    nextTaskAction();
                }

                Interlocked.Exchange(ref _concurrentCount, 0);
            });
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

            _ = Task.Run(() =>
            {
                while (taskQueue.TryDequeue(out Action nextTaskAction))
                {
                    nextTaskAction();
                }

                Interlocked.Exchange(ref _concurrentCount, 0);
            });
        }
    }
}
