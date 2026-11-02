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
    /// Position and size only, deliberately: a window that was closed maximized opens restored.
    /// That is why the placement is read rather than the window rect - <c>rcNormalPosition</c> is
    /// the restored bounds whatever state the window is in, so there is nothing to special-case.
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
        /// </summary>
        public static void Restore(IntPtr hwnd, string persistedId)
        {
            if (string.IsNullOrEmpty(persistedId) || !TryReadRect(persistedId, out var rect))
            {
                return;
            }

            // Only where it was, for a window that cannot be resized: the size saved may be one it
            // was dragged to before it stopped being resizable, and it could never leave it again.
            var current = new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
            var style = (long)Win32.GetWindowLongPtrW(hwnd, Win32.GWL_STYLE);

            if ((style & Win32.WS_THICKFRAME) == 0 && Win32.GetWindowPlacement(hwnd, ref current))
            {
                rect.right = rect.left + current.rcNormalPosition.right - current.rcNormalPosition.left;
                rect.bottom = rect.top + current.rcNormalPosition.bottom - current.rcNormalPosition.top;
            }

            // The monitor it was on may be gone, and a window restored onto no monitor at all is
            // unreachable. MONITOR_DEFAULTTONULL is what says so rather than guessing a nearest.
            var monitor = Win32.MonitorFromRect(ref rect, Win32.MONITOR_DEFAULTTONULL);
            if (monitor == IntPtr.Zero)
            {
                return;
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

            Store.SetValue(persistedId, string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3}",
                rect.left, rect.top, width, height));
        }

        private static bool TryReadRect(string persistedId, out RECT rect)
        {
            rect = default;

            if (!Store.TryGetValue(persistedId, out var value) || value is not string text)
            {
                return false;
            }

            var parts = text.Split(',');
            if (parts.Length != 4
                || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)
                || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y)
                || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var width)
                || !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var height)
                || width <= 0 || height <= 0)
            {
                return false;
            }

            rect = new RECT { left = x, top = y, right = x + width, bottom = y + height };
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
