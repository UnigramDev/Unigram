//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;

namespace Telegram.Host
{
    /// <summary>
    /// A thread that hosts XAML islands still gets a <c>CoreWindow</c> - a 1x1 invisible stub, a
    /// child of the top-level window. Nothing draws in it, but parts of the framework still listen
    /// to it, and in this host nothing ever tells it anything.
    ///
    /// microsoft/microsoft-ui-xaml#3577 is the known case: a ContentDialog's smoke layer keeps the
    /// size it had when it opened, because the resize never reaches the CoreWindow. The workaround
    /// posted there is to forward WM_SIZE to it by hand.
    ///
    /// EXPERIMENT: gate 1.13 measured that a TextBox inside a ContentDialog receives KeyDown and
    /// never a CharacterReceived, while a Popup in the same island types perfectly. If the dialog's
    /// input is bound to the same CoreWindow its layout is, forwarding the keyboard should fix that
    /// too. If it does not, this file goes and the popup host has to be rebuilt instead.
    /// </summary>
    internal static class CoreWindowBridge
    {
        private const string CoreWindowClass = "Windows.UI.Core.CoreWindow";

        private static IntPtr _hwnd;
        private static bool _resolved;

        /// <summary>
        /// Found by class name rather than through ICoreWindowInterop: the stub is a child of the
        /// island's own window, so there is no COM to do.
        /// </summary>
        public static IntPtr Resolve(IntPtr parent)
        {
            if (_resolved)
            {
                return _hwnd;
            }

            _resolved = true;
            _hwnd = Win32.FindWindowExW(parent, IntPtr.Zero, CoreWindowClass, null);

            return _hwnd;
        }

        public static void Forward(IntPtr parent, uint message, IntPtr wParam, IntPtr lParam)
        {
            var hwnd = Resolve(parent);
            if (hwnd != IntPtr.Zero)
            {
                Win32.PostMessageW(hwnd, message, wParam, lParam);
            }
        }
    }
}
