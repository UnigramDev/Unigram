using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Unigram.Common
{
    public class ConcurrentQueueWorker
    {
        private ConcurrentQueue<Func<Task>> taskQueue = new ConcurrentQueue<Func<Task>>();
        private ManualResetEvent mre = new ManualResetEvent(true);
        private int _concurrentCount = 1;
        private readonly object o = new object();

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
                        var nextTaskAction = default(Func<Task>);
                        if (taskQueue.TryDequeue(out nextTaskAction))
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
}
