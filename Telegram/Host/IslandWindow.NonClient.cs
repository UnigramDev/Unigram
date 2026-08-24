using System;
using System.Runtime.InteropServices;
using Telegram.Navigation;
using Windows.Foundation;

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
        // Logical pixels; the strip the app paints its own caption into. 40 because that is what
        // every title bar in this app is, and the strip has to be exactly the buttons the
        // WindowPresenter template draws into it - the drag bar is what hit-tests them.
        public const int CaptionHeight = 40;

        // Each button in the strip, right to left: close, maximize, minimize.
        private const int CaptionButtonWidth = 46;

        private bool _nonClient;
        private IntPtr _dragBar;

        private CaptionButtons _captionButtons = CaptionButtons.All;

        // Logical pixels, relative to the client area. Three states, and they differ: null is a
        // window that has not named a title bar, where the whole strip drags as it always did;
        // an empty rect is one that took its title bar away, where only the buttons still answer.
        private Rect? _dragArea;
        private CaptionButtons _hotButton;
        private bool _hotPressed;
        private bool _tracking;

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
            DragBars[_dragBar] = this;
        }

        private static IntPtr DragBarProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (!DragBars.TryGetValue(hWnd, out var window))
            {
                return Win32.DefWindowProcW(hWnd, msg, wParam, lParam);
            }

            switch (msg)
            {
                case Win32.WM_NCHITTEST:
                    return (IntPtr)window.DragBarHitTest(lParam);

                case Win32.WM_NCMOUSELEAVE:
                    window._tracking = false;
                    window.SetHotButton(CaptionButtons.None, false);
                    return IntPtr.Zero;

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

            // The buttons are drawn by XAML and hit-tested here, so their hover and pressed states
            // have to be pushed across. Terminal does the same, for the same reason.
            if (msg == Win32.WM_NCMOUSEMOVE)
            {
                TrackMouseLeave();
                SetHotButton(FromHitTest(hit), false);
            }
            else if (msg == Win32.WM_NCLBUTTONDOWN)
            {
                SetHotButton(FromHitTest(hit), true);
            }
            else if (msg == Win32.WM_NCLBUTTONUP)
            {
                SetHotButton(FromHitTest(hit), false);
            }

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

        private static CaptionButtons FromHitTest(int hit)
        {
            return hit switch
            {
                Win32.HTMINBUTTON => CaptionButtons.Minimize,
                Win32.HTMAXBUTTON => CaptionButtons.Maximize,
                Win32.HTCLOSE => CaptionButtons.Close,
                _ => CaptionButtons.None
            };
        }

        /// <summary>
        /// Pointer sample rate, so it early-outs on an unchanged state rather than crossing into
        /// XAML for every mouse move over the caption.
        /// </summary>
        private void SetHotButton(CaptionButtons button, bool pressed)
        {
            if (_hotButton == button && _hotPressed == pressed)
            {
                return;
            }

            _hotButton = button;
            _hotPressed = pressed;

            if (Content is WindowPresenter presenter)
            {
                presenter.SetCaptionButtonState(button, pressed);
            }
        }

        /// <summary>
        /// Armed once per visit: without it there is no WM_NCMOUSELEAVE and a button stays lit
        /// after the pointer has left the window.
        /// </summary>
        private void TrackMouseLeave()
        {
            if (_tracking)
            {
                return;
            }

            _tracking = true;

            var track = new TRACKMOUSEEVENT
            {
                cbSize = Marshal.SizeOf<TRACKMOUSEEVENT>(),
                dwFlags = Win32.TME_LEAVE | Win32.TME_NONCLIENT,
                hwndTrack = _dragBar
            };

            Win32.TrackMouseEvent(ref track);
        }

        /// <summary>
        /// A window that cannot maximize cannot be resized either: the two are one affordance, and
        /// a resize border on a window with no maximize button is a window that can be dragged to
        /// any size but never back. Dropping the styles is also what stops the double-click on the
        /// caption from zooming it.
        /// </summary>
        public void SetCaptionButtons(CaptionButtons buttons)
        {
            _captionButtons = buttons;

            var style = (long)Win32.GetWindowLongPtrW(_hwnd, Win32.GWL_STYLE);
            var mask = (long)(Win32.WS_THICKFRAME | Win32.WS_MAXIMIZEBOX);

            var updated = buttons.HasFlag(CaptionButtons.Maximize)
                ? style | mask
                : style & ~mask;

            if (updated == style)
            {
                return;
            }

            Win32.SetWindowLongPtrW(_hwnd, Win32.GWL_STYLE, (IntPtr)updated);

            // The frame is what WM_NCCALCSIZE measures, so it has to be recomputed rather than
            // left over from the styles the window was created with.
            Win32.SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, 0, 0,
                Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_FRAMECHANGED);
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

            // No maximize means no resizing, so there is no top border to put back either.
            if (point.y < border && _captionButtons.HasFlag(CaptionButtons.Maximize))
            {
                return Win32.HTTOP;
            }

            // Right to left, and only the buttons this window actually draws: the strip is laid out
            // by the WindowPresenter template and the two have to agree on where each one is.
            var button = Scale(CaptionButtonWidth);
            var fromRight = client.right - point.x;
            var edge = 0;

            if (_captionButtons.HasFlag(CaptionButtons.Close))
            {
                edge += button;

                if (fromRight < edge)
                {
                    return Win32.HTCLOSE;
                }
            }

            if (_captionButtons.HasFlag(CaptionButtons.Maximize))
            {
                edge += button;

                if (fromRight < edge)
                {
                    return Win32.HTMAXBUTTON;
                }
            }

            if (_captionButtons.HasFlag(CaptionButtons.Minimize))
            {
                edge += button;

                if (fromRight < edge)
                {
                    return Win32.HTMINBUTTON;
                }
            }

            return Win32.HTCAPTION;
        }

        /// <summary>
        /// The window's draggable region, in logical pixels relative to the client area. An empty
        /// rect means the root wants no draggable region; null means it has not said.
        /// </summary>
        public void SetDragArea(Rect? area)
        {
            _dragArea = area;
            LayoutDragBar();
        }

        private int CaptionButtonsWidth()
        {
            var count = 0;

            if (_captionButtons.HasFlag(CaptionButtons.Minimize))
            {
                count++;
            }

            if (_captionButtons.HasFlag(CaptionButtons.Maximize))
            {
                count++;
            }

            if (_captionButtons.HasFlag(CaptionButtons.Close))
            {
                count++;
            }

            return Scale(CaptionButtonWidth) * count;
        }

        /// <summary>
        /// From the drag area's left edge to the right of the window, deliberately: the caption
        /// buttons must stay under this window, because answering HTMAXBUTTON for them is what
        /// earns the snap layouts flyout. Terminal extends its own sink the same way and for the
        /// same reason (GH#9443), having started with one that stopped short of them.
        ///
        /// What is to the LEFT of the area belongs to the island, and that is the point of this:
        /// a full-width strip makes every pixel of the app's own title bar unclickable.
        /// </summary>
        private void LayoutDragBar()
        {
            if (_dragBar == IntPtr.Zero || !Win32.GetClientRect(_hwnd, out var client))
            {
                return;
            }

            int left, top, height;

            if (_dragArea is Rect area && area.Width > 0)
            {
                left = Scale((int)area.X);
                top = Scale((int)area.Y);

                // At least the caption strip: a title bar shorter than the buttons would leave
                // their lower edge outside this window, where nothing answers for them.
                height = Math.Max(Scale((int)area.Height), Scale(CaptionHeight));
            }
            else if (_dragArea != null)
            {
                // No draggable region, but the buttons are still the app's to answer for - a popup
                // takes the title bar away and must not take the close button with it.
                left = client.right - client.left - CaptionButtonsWidth();
                top = 0;
                height = Scale(CaptionHeight);
            }
            else
            {
                left = 0;
                top = 0;
                height = Scale(CaptionHeight);
            }

            var width = client.right - client.left - left;

            if (width <= 0 || height <= 0)
            {
                Win32.SetWindowPos(_dragBar, IntPtr.Zero, 0, 0, 0, 0,
                    Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_HIDEWINDOW);
                return;
            }

            Win32.SetWindowPos(_dragBar, IntPtr.Zero, left, top, width, height, Win32.SWP_SHOWWINDOW);
        }
    }
}
