//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Threading.Tasks;

namespace Telegram.Common
{
    public static partial class SystemTray
    {
        // Nothing of its own: the icon belongs to Telegram.Stub, and the bridge is how it is asked
        // for anything. Deliberately thin, so that the stub keeps behaving exactly as it did.
        public static partial Task ShowAsync()
        {
            return BridgeApplicationContext.LaunchAsync();
        }

        public static partial Task HideAsync()
        {
            return BridgeApplicationContext.ExitAsync();
        }

        public static partial void SetUnreadCount(int unreadCount, int unreadUnmutedCount)
        {
            BridgeApplicationContext.SendUnreadCount(unreadCount, unreadUnmutedCount);
        }
    }
}
