//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Telegram.Services.Wallet
{
    /// <summary>
    /// The loads of one paged list, one at a time.
    /// </summary>
    /// <remarks>
    /// A page asked for while the list is being refreshed waits for the refresh and then pages on
    /// from where it left, rather than being dropped: the list asking for it reads the answer as
    /// whether there is more, and a dropped load read as the end stops it paging for good.
    ///
    /// Refreshes coalesce, one running and one waiting covering any number asked for meanwhile.
    /// The one waiting is still needed, because a change asked about may postdate the request the
    /// running one has already sent.
    /// </remarks>
    internal sealed class WalletPager
    {
        // How recent a refresh has to be for a catch-up to count it as its own.
        private static readonly TimeSpan CatchUpWindow = TimeSpan.FromSeconds(2);

        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly Func<bool, Task> _load;

        private int _refreshWaiting;
        private int _refreshRunning;
        private long _refreshedAt;

        /// <param name="load">One load: a refresh from the top when given true, the next page
        /// otherwise.</param>
        public WalletPager(Func<bool, Task> load)
        {
            _load = load;
        }

        public Task LoadMoreAsync()
        {
            return RunAsync(false);
        }

        /// <summary>
        /// A refresh for something that has happened: runs after any that is already running.
        /// </summary>
        public Task RefreshAsync()
        {
            return RunAsync(true);
        }

        /// <summary>
        /// A refresh for nothing in particular - the window opening, the stream connecting - which
        /// any refresh running, waiting or just finished already answers.
        /// </summary>
        public Task CatchUpAsync()
        {
            if (Volatile.Read(ref _refreshWaiting) == 1 || Volatile.Read(ref _refreshRunning) == 1)
            {
                return Task.CompletedTask;
            }

            var refreshedAt = new DateTime(Interlocked.Read(ref _refreshedAt), DateTimeKind.Utc);
            if (DateTime.UtcNow - refreshedAt < CatchUpWindow)
            {
                return Task.CompletedTask;
            }

            return RunAsync(true);
        }

        /// <summary>
        /// Forgets the last refresh, for a list that has just started over.
        /// </summary>
        public void Reset()
        {
            Interlocked.Exchange(ref _refreshedAt, 0);
        }

        private async Task RunAsync(bool refresh)
        {
            if (refresh && Interlocked.Exchange(ref _refreshWaiting, 1) == 1)
            {
                return;
            }

            await _gate.WaitAsync();
            try
            {
                if (refresh)
                {
                    Volatile.Write(ref _refreshWaiting, 0);
                    Volatile.Write(ref _refreshRunning, 1);
                }

                await _load(refresh);
            }
            finally
            {
                if (refresh)
                {
                    Volatile.Write(ref _refreshRunning, 0);
                    Interlocked.Exchange(ref _refreshedAt, DateTime.UtcNow.Ticks);
                }

                _gate.Release();
            }
        }
    }
}
