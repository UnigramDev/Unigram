using System;
using System.Runtime.InteropServices;

namespace Telegram.Host
{
    /// <summary>
    /// Gate 1.7 - the custom caption, which is the one thing UWP cannot do: a UWP app with
    /// ExtendViewIntoTitleBar can hide, minimize and close, but never maximize, and it loses the
    /// Windows 11 snap layouts flyout with the system caption.
    ///
    /// Three pieces, following Windows Terminal's NonClientIslandWindow:
    ///
    /// 1. WM_NCCALCSIZE keeps the frame on three sides and removes it from the top, so the client
    ///    area - and therefore the island - reaches the top edge of the window.
    /// 2. WM_NCHITTEST puts back the top resize border that removing the frame took away.
    /// 3. A drag-bar HWND laid over the caption strip. This is the part that is not obvious: the
    ///    island is a child window and swallows WM_NCHITTEST, so the top-level window never sees
    ///    the caption and dragging would not work at all. A small child window above the island
    ///    answers the hit test instead - and then has to forward the caption messages to the
    ///    parent, because a child answering HTCAPTION drags nothing.
    ///
    /// Returning HTMAXBUTTON is what earns the Windows 11 snap layouts flyout, which a UWP custom
    /// caption never gets. It does not earn the click: the buttons are driven by hand in
    /// OnDragBarMouse. Terminal says the same - "the buttons won't work as you'd expect".
    /// </summary>
    internal sealed partial class IslandWindow
    {
        // Logical pixels; the strip the app paints its own caption into.
        public const int CaptionHeight = 32;

        // Each button in the strip, right to left: close, maximize, minimize.
        private const int CaptionButtonWidth = 46;

        private bool _nonClient;
        private IntPtr _dragBar;

        private static WndProc _dragBarProc;
        private static ushort _dragBarAtom;
        private static IntPtr _dragBarClassName;

        private uint Dpi => Win32.GetDpiForWindow(_hwnd);

        private int Scale(int value)
        {
            return (int)(value * Dpi / 96.0);
        }

        /// <summary>
        /// The frame Windows would have added, which WM_NCCALCSIZE has to subtract by hand.
        /// </summary>
        private RECT GetFrame()
        {
            var frame = new RECT();
            Win32.AdjustWindowRectExForDpi(ref frame, Win32.WS_OVERLAPPEDWINDOW, false, 0, Dpi);
            return frame;
        }

        private IntPtr OnNcCalcSize(IntPtr wParam, IntPtr lParam)
        {
            if (wParam == IntPtr.Zero)
            {
                return Win32.DefWindowProcW(_hwnd, Win32.WM_NCCALCSIZE, wParam, lParam);
            }

            var parameters = Marshal.PtrToStructure<NCCALCSIZE_PARAMS>(lParam);
            var top = parameters.rgrc0.top;

            // Let the default proc compute the frame, then put the top back: everything it did on
            // the other three sides is what we want, and only the caption has to go.
            var result = Win32.DefWindowProcW(_hwnd, Win32.WM_NCCALCSIZE, wParam, lParam);

            parameters = Marshal.PtrToStructure<NCCALCSIZE_PARAMS>(lParam);
            parameters.rgrc0.top = top;

            if (Win32.IsZoomed(_hwnd))
            {
                // A maximized window is deliberately larger than the monitor by the frame width,
                // so the top has to come back in by that much or the caption is off-screen.
                parameters.rgrc0.top += Win32.GetSystemMetricsForDpi(Win32.SM_CYSIZEFRAME, Dpi)
                    + Win32.GetSystemMetricsForDpi(Win32.SM_CXPADDEDBORDER, Dpi);
            }

            Marshal.StructureToPtr(parameters, lParam, false);
            return result;
        }

        private IntPtr OnNcHitTest(IntPtr lParam)
        {
            var result = Win32.DefWindowProcW(_hwnd, Win32.WM_NCHITTEST, IntPtr.Zero, lParam);
            if ((int)result != Win32.HTCLIENT)
            {
                return result;
            }

            // Removing the top of the frame took the top resize border with it. The bottom and
            // sides still come from the default proc above.
            var point = new POINT { x = (short)((long)lParam & 0xFFFF), y = (short)(((long)lParam >> 16) & 0xFFFF) };
            Win32.ScreenToClient(_hwnd, ref point);

            var border = Win32.GetSystemMetricsForDpi(Win32.SM_CYSIZEFRAME, Dpi)
                + Win32.GetSystemMetricsForDpi(Win32.SM_CXPADDEDBORDER, Dpi);

            if (point.y >= 0 && point.y < border && !Win32.IsZoomed(_hwnd))
            {
                return (IntPtr)Win32.HTTOP;
            }

            return (IntPtr)Win32.HTCLIENT;
        }

        private void CreateDragBar()
        {
            if (_dragBarAtom == 0)
            {
                _dragBarClassName = Marshal.StringToHGlobalUni("XamlIslandSpikeDragBar");
                _dragBarProc = DragBarProc;

                var wc = new WNDCLASSEXW
                {
                    cbSize = Marshal.SizeOf<WNDCLASSEXW>(),
                    lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_dragBarProc),
                    hInstance = Win32.GetModuleHandleW(IntPtr.Zero),
                    hCursor = Win32.LoadCursorW(IntPtr.Zero, 32512),
                    lpszClassName = _dragBarClassName
                };

                _dragBarAtom = Win32.RegisterClassExW(ref wc);
            }

            // Layered so it is invisible, no redirection bitmap so it composes with the island
            // beneath it. It exists only to answer WM_NCHITTEST.
            _dragBar = Win32.CreateWindowExW(Win32.WS_EX_LAYERED | Win32.WS_EX_NOREDIRECTIONBITMAP,
                _dragBarClassName, IntPtr.Zero, Win32.WS_CHILD | Win32.WS_VISIBLE,
                0, 0, 0, 0, _hwnd, IntPtr.Zero, Win32.GetModuleHandleW(IntPtr.Zero), IntPtr.Zero);

            Win32.SetLayeredWindowAttributes(_dragBar, 0, 255, Win32.LWA_ALPHA);
            Windows[_dragBar] = this;
        }

        private static IntPtr DragBarProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (!Windows.TryGetValue(hWnd, out var window))
            {
                return Win32.DefWindowProcW(hWnd, msg, wParam, lParam);
            }

            switch (msg)
            {
                case Win32.WM_NCHITTEST:
                    return (IntPtr)window.DragBarHitTest(lParam);

                case Win32.WM_NCMOUSEMOVE:
                case Win32.WM_NCLBUTTONDOWN:
                case Win32.WM_NCLBUTTONDBLCLK:
                case Win32.WM_NCLBUTTONUP:
                case Win32.WM_NCRBUTTONDOWN:
                case Win32.WM_NCRBUTTONUP:
                    return window.OnDragBarMouse(msg, wParam, lParam);
            }

            return Win32.DefWindowProcW(hWnd, msg, wParam, lParam);
        }

        /// <summary>
        /// A child window cannot drag its parent by answering HTCAPTION, so caption messages are
        /// handed to the parent instead - that is what makes dragging, double-click maximize, the
        /// system menu and the top resize border work. The buttons have to be driven by hand: the
        /// hit test earns the snap layouts flyout, but not the click.
        /// </summary>
        private IntPtr OnDragBarMouse(uint msg, IntPtr wParam, IntPtr lParam)
        {
            var hit = (int)wParam;

            if (hit == Win32.HTCAPTION || hit == Win32.HTTOP)
            {
                return Win32.SendMessageW(_hwnd, msg, wParam, lParam);
            }

            if (msg == Win32.WM_NCLBUTTONUP)
            {
                var command = hit switch
                {
                    Win32.HTMINBUTTON => Win32.SC_MINIMIZE,
                    Win32.HTCLOSE => Win32.SC_CLOSE,
                    Win32.HTMAXBUTTON => Win32.IsZoomed(_hwnd) ? Win32.SC_RESTORE : Win32.SC_MAXIMIZE,
                    _ => 0
                };

                if (command != 0)
                {
                    Win32.SendMessageW(_hwnd, Win32.WM_SYSCOMMAND, (IntPtr)command, IntPtr.Zero);
                }
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Answers for the caption strip. The button codes are the interesting ones: Windows
        /// draws the snap layouts flyout for HTMAXBUTTON and turns a click into the command,
        /// which is exactly what a UWP custom caption never gets.
        /// </summary>
        private int DragBarHitTest(IntPtr lParam)
        {
            var point = new POINT { x = (short)((long)lParam & 0xFFFF), y = (short)(((long)lParam >> 16) & 0xFFFF) };
            Win32.ScreenToClient(_hwnd, ref point);

            if (!Win32.GetClientRect(_hwnd, out var client))
            {
                return Win32.HTCAPTION;
            }

            var border = Win32.IsZoomed(_hwnd)
                ? 0
                : Win32.GetSystemMetricsForDpi(Win32.SM_CYSIZEFRAME, Dpi)
                    + Win32.GetSystemMetricsForDpi(Win32.SM_CXPADDEDBORDER, Dpi);

            if (point.y < border)
            {
                return Win32.HTTOP;
            }

            var button = Scale(CaptionButtonWidth);
            var fromRight = client.right - point.x;

            if (fromRight < button)
            {
                return Win32.HTCLOSE;
            }
            else if (fromRight < button * 2)
            {
                return Win32.HTMAXBUTTON;
            }
            else if (fromRight < button * 3)
            {
                return Win32.HTMINBUTTON;
            }

            return Win32.HTCAPTION;
        }

        private void LayoutDragBar()
        {
            if (_dragBar == IntPtr.Zero || !Win32.GetClientRect(_hwnd, out var client))
            {
                return;
            }

            Win32.SetWindowPos(_dragBar, IntPtr.Zero, 0, 0,
                client.right - client.left, Scale(CaptionHeight), Win32.SWP_SHOWWINDOW);
        }
    }
}
