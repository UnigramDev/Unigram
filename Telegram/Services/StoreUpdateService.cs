//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Services.Updates;
using Windows.Services.Store;

namespace Telegram.Services
{
    /// <summary>
    /// The <see cref="ICloudUpdateService"/> a Store release registers. Everything happens through
    /// <see cref="StoreContext"/>: there is no file to fetch or hand to the package manager, so a
    /// detected update is offered right away and installing it is one call.
    /// </summary>
    public partial class StoreUpdateService : ICloudUpdateService
    {
        private readonly IEventAggregator _aggregator;

        private static readonly SemaphoreSlim _updateLock = new(1, 1);

        private CloudUpdate _nextUpdate;

        private ulong _lastCheck;

        public StoreUpdateService(IEventAggregator aggregator)
        {
            _aggregator = aggregator;
        }

        public CloudUpdate NextUpdate => _nextUpdate;

        public async Task UpdateAsync(bool force)
        {
            if (!_updateLock.Wait(0))
            {
                Logger.Info("Can't acquire lock");
                return;
            }

            var diff = Logger.TickCount - _lastCheck;
            var skip = diff < 5 * 60 * 1000;

            if (skip && !force)
            {
                _updateLock.Release();
                return;
            }

            _lastCheck = Logger.TickCount;

            try
            {
                var context = StoreContext.GetDefault();
                var updates = await context.GetAppAndOptionalStorePackageUpdatesAsync();

                if (updates != null && updates.Count > 0)
                {
                    var version = updates.Max(x => x.Package.Id.Version.ToVersion());
                    if (_nextUpdate == null || _nextUpdate.Version != version)
                    {
                        _nextUpdate = new CloudUpdate
                        {
                            Version = version,
                            IsStorePackage = true
                        };

                        _aggregator.Publish(new UpdateAppVersion(_nextUpdate));
                    }
                }
            }
            catch (Exception ex)
            {
                // All the remote procedure calls must be wrapped in a try-catch block
                Logger.Error(ex);
            }

            _updateLock.Release();
        }

        public async Task<bool> LaunchAsync()
        {
            if (_nextUpdate == null)
            {
                return false;
            }

            var navigation = WindowContext.Main?.GetNavigationService();
            if (navigation == null)
            {
                return false;
            }

            var installed = false;

            // The request shows the Store's own consent and progress UI, so it needs a view. The updates
            // are queried again in here rather than carried over from the check: a StoreContext and the
            // StorePackageUpdate objects it hands out belong to the thread that created them, and the
            // check can have run on any thread.
            await navigation.Dispatcher.DispatchAsync(async () =>
            {
                try
                {
                    var context = StoreContext.GetDefault();
                    var updates = await context.GetAppAndOptionalStorePackageUpdatesAsync();

                    if (updates == null || updates.Count == 0)
                    {
                        return;
                    }

                    // The notify icon is left alone: unlike the sideload path, nothing here force-closes
                    // the app, and the request is the user's last chance to decline.
                    //
                    // Deliberately not TrySilentDownloadAndInstall: silent means download as well as
                    // install, over the network and with nothing to dismiss, and it reports a refusal
                    // (no consent, metered, low battery) as a bare failure the Store words far better.
                    var result = await context.RequestDownloadAndInstallStorePackageUpdatesAsync(updates);

                    installed = result?.OverallState == StorePackageUpdateState.Completed;
                }
                catch (Exception ex)
                {
                    // All the remote procedure calls must be wrapped in a try-catch block
                    Logger.Error(ex);
                }
            });

            return installed;
        }
    }
}
