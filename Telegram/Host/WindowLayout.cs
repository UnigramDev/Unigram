//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Globalization;
using System.Runtime.InteropServices;
using Telegram.Services.Settings;

namespace Telegram.Host
{
    /// <summary>
    /// Where a window was, per <c>PersistedId</c>. UWP gets this from the shell for nothing -
    /// <c>ApplicationView.PersistedStateId</c> - so this exists only to bring the WinUI flavour
    /// back to parity: the framework keeps no window placement of its own.
    ///
    /// The restored bounds, the DPI of the monitor they were measured on, and whether it was
    /// maximized. The placement is read rather than the window rect because
    /// <c>rcNormalPosition</c> is the restored bounds whatever state the window is in, so a window
    /// reopened maximized still has its own size to restore down to.
    ///
    /// Windows sharing an id share a slot and the last one closed wins - Fela, 2026-08-24. Two web
    /// app windows are both "WebApp", and that is fine: they are the same window to the user.
    /// </summary>
    internal static class WindowLayout
    {
        private const string ContainerName = "WindowLayout";

        private static ISettingsStore _store;
        private static ISettingsStore Store => _store ??= SettingsStore.Local.GetContainer(ContainerName);

        /// <summary>
        /// Call between creating the window and showing it, so it opens where it belongs rather
        /// than moving once it is on screen - and after its content has said which caption buttons
        /// it wants, since that decides whether it is resizable.
        ///
        /// Returns whether it should open maximized, which is left to the caller's first show: a
        /// placement that maximized it here would also show it.
        /// </summary>
        public static bool Restore(IntPtr hwnd, string persistedId)
        {
            if (string.IsNullOrEmpty(persistedId) || !TryRead(persistedId, out var rect, out var maximized, out var savedDpi))
            {
                return false;
            }

            // Only where it was, for a window that cannot be resized: the size saved may be one it
            // was dragged to before it stopped being resizable, and it could never leave it again.
            // Nor maximized, which it would then have no way out of.
            var current = new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
            var style = (long)Win32.GetWindowLongPtrW(hwnd, Win32.GWL_STYLE);
            var resizable = (style & Win32.WS_THICKFRAME) != 0;

            if (!resizable)
            {
                maximized = false;

                if (Win32.GetWindowPlacement(hwnd, ref current))
                {
                    rect.right = rect.left + current.rcNormalPosition.right - current.rcNormalPosition.left;
                    rect.bottom = rect.top + current.rcNormalPosition.bottom - current.rcNormalPosition.top;
                }
            }

            // The monitor it was on may be gone, and a window restored onto no monitor at all is
            // unreachable. MONITOR_DEFAULTTONULL is what says so rather than guessing a nearest.
            // Still maximized if it was: on whichever monitor the default bounds land.
            var monitor = Win32.MonitorFromRect(ref rect, Win32.MONITOR_DEFAULTTONULL);
            if (monitor == IntPtr.Zero)
            {
                return maximized;
            }

            // The size is in the pixels of the monitor it was saved on. The same desktop position
            // can be a different monitor at a different scale - a laptop moved between a 150%
            // screen at work and a 100% one at home - and the window should keep its size in
            // effective pixels, not in physical ones.
            if (resizable && savedDpi != 0)
            {
                var dpi = GetMonitorDpi(monitor);
                if (dpi != 0 && dpi != savedDpi)
                {
                    var width = (int)Math.Round((rect.right - rect.left) * (double)dpi / savedDpi);
                    var height = (int)Math.Round((rect.bottom - rect.top) * (double)dpi / savedDpi);

                    rect.right = rect.left + width;
                    rect.bottom = rect.top + height;
                }
            }

            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (Win32.GetMonitorInfoW(monitor, ref info))
            {
                Clamp(ref rect, info.rcWork);
            }

            // SW_HIDE, not SW_SHOWNORMAL: this runs before the window is first shown, and asking
            // for a normal show here would put it on screen before its caller does. The restored
            // bounds are recorded either way, and the caller's show uses them.
            var placement = new WINDOWPLACEMENT
            {
                length = Marshal.SizeOf<WINDOWPLACEMENT>(),
                showCmd = Win32.SW_HIDE,
                rcNormalPosition = rect
            };

            // SetWindowPlacement rather than SetWindowPos, to stay in the same coordinate space
            // the rect was read from: rcNormalPosition is in workspace coordinates, which differ
            // from screen ones when the taskbar is docked left or top. Symmetry avoids the
            // conversion entirely - and with it the window that creeps a little on every restart.
            Win32.SetWindowPlacement(hwnd, ref placement);

            // A window created on a monitor at one scale and placed on one at another is sent
            // WM_DPICHANGED, and the framework's handler scales the size it was just given a second
            // time. Placed again, it is already on that monitor, so the second one sticks.
            if (Win32.GetWindowPlacement(hwnd, ref current) && !SameRect(current.rcNormalPosition, rect))
            {
                Win32.SetWindowPlacement(hwnd, ref placement);
            }

            return maximized;
        }

        private static uint GetMonitorDpi(IntPtr monitor)
        {
            return Win32.GetDpiForMonitor(monitor, Win32.MDT_EFFECTIVE_DPI, out var dpi, out _) == 0 ? dpi : 0;
        }

        private static bool SameRect(RECT x, RECT y)
        {
            return x.left == y.left && x.top == y.top && x.right == y.right && x.bottom == y.bottom;
        }

        public static void Save(IntPtr hwnd, string persistedId)
        {
            if (string.IsNullOrEmpty(persistedId))
            {
                return;
            }

            var placement = new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
            if (!Win32.GetWindowPlacement(hwnd, ref placement))
            {
                return;
            }

            var rect = placement.rcNormalPosition;
            var width = rect.right - rect.left;
            var height = rect.bottom - rect.top;

            if (width <= 0 || height <= 0)
            {
                return;
            }

            // Minimized from maximized is still maximized: closed from the taskbar, it should come
            // back the way it would have been restored.
            var maximized = placement.showCmd == Win32.SW_SHOWMAXIMIZED
                || placement.showCmd == Win32.SW_SHOWMINIMIZED && (placement.flags & Win32.WPF_RESTORETOMAXIMIZED) != 0;

            var dpi = GetMonitorDpi(Win32.MonitorFromRect(ref rect, Win32.MONITOR_DEFAULTTONEAREST));

            Store.SetValue(persistedId, string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4},{5}",
                rect.left, rect.top, width, height, maximized ? 1 : 0, dpi));
        }

        private static bool TryRead(string persistedId, out RECT rect, out bool maximized, out uint dpi)
        {
            rect = default;
            maximized = false;
            dpi = 0;

            if (!Store.TryGetValue(persistedId, out var value) || value is not string text)
            {
                return false;
            }

            // Four fields from before the maximized state was kept, five from before the DPI was.
            // Without a DPI the size is restored as it was saved.
            var parts = text.Split(',');
            if (parts.Length is not (4 or 5 or 6)
                || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)
                || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y)
                || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var width)
                || !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var height)
                || width <= 0 || height <= 0)
            {
                return false;
            }

            rect = new RECT { left = x, top = y, right = x + width, bottom = y + height };
            maximized = parts.Length >= 5 && parts[4] == "1";

            if (parts.Length == 6)
            {
                uint.TryParse(parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out dpi);
            }

            return true;
        }

        /// <summary>
        /// A monitor can come back smaller than it was - a resolution change, or a laptop screen
        /// standing in for the dock it was last docked to.
        /// </summary>
        private static void Clamp(ref RECT rect, RECT work)
        {
            var width = Math.Min(rect.right - rect.left, work.right - work.left);
            var height = Math.Min(rect.bottom - rect.top, work.bottom - work.top);

            var left = Math.Min(Math.Max(rect.left, work.left), work.right - width);
            var top = Math.Min(Math.Max(rect.top, work.top), work.bottom - height);

            rect = new RECT { left = left, top = top, right = left + width, bottom = top + height };
        }
    }
}
