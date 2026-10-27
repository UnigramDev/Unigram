//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Threading.Tasks;
using Microsoft.Windows.AppLifecycle;

namespace Telegram.Compat
{
    /// <summary>
    /// The piece of the UWP application model that is neither window-bound nor XAML, and so has
    /// nowhere in WindowContext to live. The shared sources call it under #if WINUI3.
    /// </summary>
    static class AppModel
    {
        /// <summary>
        /// CoreApplication.RequestRestartAsync, which is UWP's and answers a failure reason. The
        /// Windows App SDK's own restart is synchronous and does not come back at all when it
        /// succeeds, so the task this returns only ever completes on failure - which is also true
        /// of the one it replaces, just less obviously.
        /// </summary>
        public static Task RequestRestartAsync(string arguments)
        {
            AppInstance.Restart(arguments ?? string.Empty);
            return Task.CompletedTask;
        }
    }
}
