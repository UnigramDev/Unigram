using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Unigram.Common
{
    public class ConcurrentQueueWorker
    {
        private readonly ConcurrentQueue<Func<Task>> taskQueue = new ConcurrentQueue<Func<Task>>();
        private readonly ManualResetEvent mre = new ManualResetEvent(true);
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
}
