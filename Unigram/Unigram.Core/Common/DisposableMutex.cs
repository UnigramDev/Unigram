using System;
using System.Threading;
using System.Threading.Tasks;

namespace Unigram.Common
{
    public class DisposableMutex : IDisposable
    {
        public DisposableMutex()
        {
            _semaphore = new SemaphoreSlim(1, 1);
        }

        public void Dispose()
        {
            _semaphore.Release();
        }

        public async Task<IDisposable> WaitAsync()
        {
            await _semaphore.WaitAsync();
            return this;
        }

        private readonly SemaphoreSlim _semaphore;
    }
}
