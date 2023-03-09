//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Telegram.Common
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

        public async Task<IDisposable> WaitAsync(CancellationToken cancellationToken)
        {
            await _semaphore.WaitAsync(cancellationToken);
            return this;
        }

        private readonly SemaphoreSlim _semaphore;
    }
}
