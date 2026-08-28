//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Threading.Tasks;

namespace Telegram.Common
{
    /// <summary>
    /// The notification area icon, as the app asks for it rather than as either host provides it.
    ///
    /// On UWP it cannot be drawn in process at all - the app is in a container with no Win32 shell
    /// surface - so it lives in the Telegram.Stub full trust process and everything here forwards
    /// to <see cref="BridgeApplicationContext"/> and its app service. On Win32 there is no bridge
    /// and no second process: the icon is ours, in this process, and the implementation is the real
    /// one.
    ///
    /// The seam is here rather than at the call sites so that the app keeps asking the same
    /// question - show it, hide it, this is the unread count - and neither host leaks into the
    /// callers.
    /// </summary>
    public static partial class SystemTray
    {
        /// <summary>
        /// Whether the icon is actually in the notification area. Asked before hiding a window to
        /// it: the setting says what was wanted, this says whether there is a way back.
        /// </summary>
        public static partial bool IsShowing();

        /// <summary>
        /// Put the icon in the notification area, if it is not there already.
        /// </summary>
        public static partial Task ShowAsync();

        /// <summary>
        /// Take it away. On UWP this also ends the stub process, which is what owns it.
        /// </summary>
        public static partial Task HideAsync();

        /// <summary>
        /// Which of the three icons to show. Unmuted wins over muted, and no unread at all is the
        /// plain one - the same rule the stub has always applied.
        /// </summary>
        public static partial void SetUnreadCount(int unreadCount, int unreadUnmutedCount);
    }
}
