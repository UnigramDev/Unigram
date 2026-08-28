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
    /// It also has to be adopted, once, by a window of ours. XAML parents it to the FIRST island on
    /// the thread, and destroying that island destroys the CoreWindow with it - which cannot be
    /// recreated. Our first island is the main window, so closing it while a chat window is open,
    /// or closing to tray and opening a window later, would take XAML down for the rest of the
    /// process. On Windows 10 it also shows up on the taskbar. Both are WinUI bugs, both are what
    /// Windows Terminal's WindowEmperor works around the same way.
    ///
    /// (Gate 1.13's input failure was once blamed on this CoreWindow. It was not: ContentDialog
    /// marks every key handled to look modal, and the exemption that normally lets characters
    /// through does not hold in an island - see ContentPopup.Win32.cs.)
    /// </summary>
    internal static class CoreWindowBridge
    {
        private const string CoreWindowClass = "Windows.UI.Core.CoreWindow";

        private static IntPtr _hwnd;
        private static bool _resolved;

        private static WndProc _ownerProc;   // field, not a lambda: the thunk outlives the window
        private static IntPtr _owner;

        /// <summary>
        /// Call once the first island exists - there is no CoreWindow before that.
        /// </summary>
        public static void Adopt(IntPtr parent)
        {
            var hwnd = Resolve(parent);
            if (hwnd != IntPtr.Zero)
            {
                Win32.SetParent(hwnd, EnsureOwner());
            }
        }

        private static IntPtr EnsureOwner()
        {
            if (_owner != IntPtr.Zero)
            {
                return _owner;
            }

            _ownerProc = Win32.DefWindowProcW;

            var className = System.Runtime.InteropServices.Marshal.StringToHGlobalUni("TelegramCoreWindowOwner");
            var wc = new WNDCLASSEXW
            {
                cbSize = System.Runtime.InteropServices.Marshal.SizeOf<WNDCLASSEXW>(),
                lpfnWndProc = System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(_ownerProc),
                hInstance = Win32.GetModuleHandleW(IntPtr.Zero),
                lpszClassName = className
            };

            Win32.RegisterClassExW(ref wc);

            // Not HWND_MESSAGE - a message-only window cannot parent the CoreWindow. Never shown,
            // never destroyed, and WS_EX_TOOLWINDOW keeps it out of alt-tab. Terminal additionally
            // strips SWP_SHOWWINDOW in its own WndProc, because the window it uses for this has
            // other duties and something does try to show it; this one has none.
            _owner = Win32.CreateWindowExW(Win32.WS_EX_TOOLWINDOW, className, IntPtr.Zero,
                Win32.WS_OVERLAPPED, 0, 0, 0, 0,
                IntPtr.Zero, IntPtr.Zero, Win32.GetModuleHandleW(IntPtr.Zero), IntPtr.Zero);

            return _owner;
        }

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
