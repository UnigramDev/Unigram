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
            if (_semaphore.CurrentCount < 1)
            {
                _semaphore.Release();
            }
        }

        public async Task<IDisposable> WaitAsync()
        {
            await _semaphore.WaitAsync();
            return this;
        }

        public async Task<IDisposable> WaitAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _semaphore.WaitAsync(cancellationToken);
            }
            catch { }
            return this;
        }

        private readonly SemaphoreSlim _semaphore;
    }
}
