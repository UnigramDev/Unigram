//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Telegram.Navigation;
using static Telegram.Common.NotifyIcon;

namespace Telegram.Common
{
    /// <summary>
    /// The notification area icon, in process.
    ///
    /// The icon itself is <see cref="NotifyIcon"/>, the same class Telegram.Stub draws for the UWP
    /// flavour. What the app service used to carry - the unread count in, open and exit out - is a
    /// method call and two events here, so the bridge, the app service and the second process all
    /// fall away on this host.
    /// </summary>
    public static partial class SystemTray
    {
        private static NotifyIcon _icon;

        public static partial bool IsShowing()
        {
            return _icon != null;
        }

        public static partial Task ShowAsync()
        {
            if (_icon == null)
            {
                _icon = new NotifyIcon(ResolveIcon, Strings.AppName);
                _icon.Click += OnClick;
                _icon.Exit += OnExit;

                _icon.UpdateOpenText(Strings.NotifyIconOpen);
                _icon.UpdateExitText(Strings.NotifyIconExit);
            }

            return Task.CompletedTask;
        }

        public static partial Task HideAsync()
        {
            if (_icon != null)
            {
                _icon.Click -= OnClick;
                _icon.Exit -= OnExit;
                _icon.Dispose();
                _icon = null;
            }

            return Task.CompletedTask;
        }

        public static partial void SetUnreadCount(int unreadCount, int unreadUnmutedCount)
        {
            if (_icon != null)
            {
                // Unmuted wins: an unmuted chat is the one worth colouring the icon for. The same
                // rule the stub applies on the other side of the bridge.
                _icon.Icon = unreadCount > 0
                    ? unreadUnmutedCount > 0 ? NotifyIconIcon.Unmuted : NotifyIconIcon.Muted
                    : NotifyIconIcon.Default;
            }
        }

        // The stub has these three as resources of its own executable; the app host carries only
        // one icon, so they ship as files beside it instead.
        private static IntPtr ResolveIcon(NotifyIconIcon icon)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Tray", icon + ".ico");

            // Handed over as a pointer: runtime marshalling is off, so nothing marshals a string
            // on our behalf.
            var buffer = Marshal.StringToHGlobalUni(path);

            try
            {
                return NativeMethods.LoadImage(IntPtr.Zero, buffer, 1 /* IMAGE_ICON */, 0, 0,
                    0x00000010 /* LR_LOADFROMFILE */ | 0x00000040 /* LR_DEFAULTSIZE */);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static void OnClick(object sender, EventArgs e)
        {
            WindowContext.Main?.Activate();
        }

        private static async void OnExit(object sender, EventArgs e)
        {
            await HideAsync();
            await BootStrapper.ConsolidateAsync();
        }
    }
}
