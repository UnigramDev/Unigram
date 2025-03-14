//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Telegram.Common
{
    // TODO: rename to CriticalSection because it sounds cooler
    public partial class DisposableMutex : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;

        public DisposableMutex()
        {
            _semaphore = new SemaphoreSlim(1, 1);
        }

        public void Dispose()
        {
            _semaphore.Release();
        }

        public IDisposable Wait()
        {
            _semaphore.Wait();
            return this;
        }

        public async Task<IDisposable> WaitAsync()
        {
            await _semaphore.WaitAsync();
            return this;
        }

        public async Task<IDisposable> WaitAsync(CancellationToken cancellationToken)
        {
            await _semaphore.WaitAsync(cancellationToken);
            return this;
        }
    }
}
