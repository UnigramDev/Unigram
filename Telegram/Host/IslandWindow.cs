using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Telegram.Navigation;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Host
{
    /// <summary>
    /// One Win32 HWND with one DesktopWindowXamlSource inside it.
    ///
    /// Deliberately has no thread affinity of its own: WindowsXamlManager is initialized once per
    /// THREAD, and any number of islands can live on that thread. That is the point of gate 1.8 -
    /// UWP's one-thread-per-view came from CoreApplication.CreateNewView(), not from XAML, and
    /// islands are not bound by it.
    /// </summary>
    /// <summary>
    /// Sees a message before the island does. This is the Win32 counterpart of
    /// <c>CoreDispatcher.AcceleratorKeyActivated</c>: it runs ahead of TranslateMessage, so it
    /// still sees Alt-modified keys and keys a focused control would swallow.
    /// </summary>
    internal interface IMessageFilter
    {
        bool PreTranslateMessage(ref MSG message);
    }

    internal sealed partial class IslandWindow : IDisposable
    {
        // Top-level windows only. The drag bars live in their own map: they are children of
        // these, and putting them here counted every window twice - the message loop pre-translated
        // each message once per entry, and Windows.Count could never reach zero, so closing the
        // last window never quit the process.
        private static readonly Dictionary<IntPtr, IslandWindow> Windows = new();

        internal static readonly Dictionary<IntPtr, IslandWindow> DragBars = new();
        private static WndProc _wndProc;   // field, not a lambda: the thunk must outlive every window
        private static ushort _classAtom;
        private static IntPtr _classNamePtr;

        private IntPtr _hwnd;
        private string _persistedId;
        private bool _transparent;
        private IntPtr _islandHwnd;
        private DesktopWindowXamlSource _source;
        private IslandNative _native;

        public IntPtr Handle => _hwnd;

        /// <summary>
        /// The island's root element. Assigning it is what <c>Window.Content</c> does on UWP, and
        /// the XamlRoot becomes available the moment it is set - see item 0.17.
        /// </summary>
        public UIElement Content
        {
            get => _source?.Content;
            set
            {
                if (_source != null)
                {
                    _source.Content = value;

                    // The island is sized when it is created, which is before there is any content
                    // to size - the app builds its root only once the WindowContext exists. Setting
                    // content afterwards raises nothing, so without this the tree is live and
                    // measured to zero: visible in the Live Visual Tree, invisible on screen.
                    Layout();
                }
            }
        }

        // The concrete ValueCollection, not IEnumerable: the message loop walks this for every
        // message the thread pumps, and an interface-typed foreach boxes the enumerator each time.
        // Thousands of allocations a second just moving the mouse.
        public static Dictionary<IntPtr, IslandWindow>.ValueCollection All => Windows.Values;

        public static IslandWindow Create(string title, int x, int y, int width, int height, UIElement content,
            bool transparent = false, bool nonClient = false, string persistedId = null)
        {
            EnsureClass();

            var window = new IslandWindow { _transparent = transparent };
            var windowName = Marshal.StringToHGlobalUni(title);

            // The caller asks in logical pixels, the way it would of a UWP view, and asks for the
            // room it gets to put content in. CreateWindowEx wants physical, and the whole window.
            //
            // The system DPI rather than the target monitor's: there is no window yet to ask. If it
            // opens somewhere else, WM_DPICHANGED arrives immediately with a suggested rect that
            // preserves the logical size, and the handler applies it.
            if (width > 0 && height > 0)
            {
                var dpi = Win32.GetDpiForSystem();
                var frame = new RECT
                {
                    right = (int)(width * dpi / 96.0),
                    bottom = (int)(height * dpi / 96.0)
                };

                Win32.AdjustWindowRectExForDpi(ref frame, Win32.WS_OVERLAPPEDWINDOW, false, 0, dpi);

                width = frame.right - frame.left;
                height = frame.bottom - frame.top;
            }

            // WS_EX_NOREDIRECTIONBITMAP is what Windows Terminal creates its host window with
            // (IslandWindow.cpp:151, "for vintage style opacity, GH#603"). Without the redirection
            // surface there is nothing opaque between the DWM backdrop and the island's own
            // composition content.
            var exStyle = transparent ? Win32.WS_EX_NOREDIRECTIONBITMAP : 0u;

            window._hwnd = Win32.CreateWindowExW(exStyle, _classNamePtr, windowName,
                Win32.WS_OVERLAPPEDWINDOW, x, y, width, height,
                IntPtr.Zero, IntPtr.Zero, Win32.GetModuleHandleW(IntPtr.Zero), IntPtr.Zero);

            if (window._hwnd == IntPtr.Zero)
            {
                throw new InvalidOperationException("CreateWindowEx failed: " + Marshal.GetLastWin32Error());
            }

            Windows[window._hwnd] = window;
            window._persistedId = persistedId;

            // Before the window is shown, so it opens where it belongs rather than jumping once
            // it is already on screen.
            WindowLayout.Restore(window._hwnd, persistedId);

            window._source = new DesktopWindowXamlSource();
            window._native = IslandNative.From(window._source);
            window._native.AttachToWindow(window._hwnd);
            window._islandHwnd = window._native.GetWindowHandle();
            window._source.Content = content;

            // A window can open behind another, and the first WM_ACTIVATE would then never come.
            if (content is WindowPresenter presenter)
            {
                presenter.IsActive = Win32.GetActiveWindow() == window._hwnd;
            }

            // XAML creates the thread's CoreWindow with the first island and parents it there.
            // Destroying that window would destroy it, and it cannot be recreated - so it is moved
            // to a window of ours that never closes.
            if (Windows.Count == 1)
            {
                CoreWindowBridge.Adopt(window._hwnd);
            }

            if (nonClient)
            {
                // The flag has to go on after creation - WM_NCCALCSIZE is sent from inside
                // CreateWindowEx, before there is an instance to consult - so the frame is
                // recalculated here instead.
                window._nonClient = true;
                Win32.SetWindowPos(window._hwnd, IntPtr.Zero, 0, 0, 0, 0,
                    Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_FRAMECHANGED);

                window.CreateDragBar();
            }

            window.Layout();

            // WM_SIZE is what normally places it, and that does not arrive until the window is
            // first resized - so without this the caption is dead until you minimize and restore.
            window.LayoutDragBar();

            Win32.ShowWindow(window._hwnd, Win32.SW_SHOW);
            Win32.UpdateWindow(window._hwnd);

            return window;
        }

        /// <summary>
        /// Consulted before the island's own accelerator handling, so that the app sees a key
        /// first. Set by the window's <c>WindowContext</c>.
        /// </summary>
        public IMessageFilter Filter { get; set; }

        /// <summary>
        /// What the window tells its owner. One seam rather than a delegate per message, because
        /// every one of these has a UWP counterpart the WindowContext has to raise, and they are
        /// only correct together - activation, visibility and size all change in the same handful
        /// of messages.
        /// </summary>
        public IIslandOwner Owner { get; set; }



        /// <summary>
        /// Out of sight but still alive, which is what closing to the notification area means: the
        /// island, its XAML and the thread's CoreWindow all stay exactly as they were.
        /// </summary>
        public void Hide()
        {
            Win32.ShowWindow(_hwnd, Win32.SW_HIDE);
        }

        public void Show()
        {
            Win32.ShowWindow(_hwnd, Win32.SW_SHOW);
            Win32.SetForegroundWindow(_hwnd);
        }

        public bool PreTranslateMessage(ref MSG message)
        {
            if (Filter != null && Filter.PreTranslateMessage(ref message))
            {
                return true;
            }

            return _native != null && _native.PreTranslateMessage(ref message);
        }

        private static void EnsureClass()
        {
            if (_classAtom != 0)
            {
                return;
            }

            _classNamePtr = Marshal.StringToHGlobalUni("XamlIslandSpikeWindow");
            _wndProc = WindowProc;

            var wc = new WNDCLASSEXW
            {
                cbSize = Marshal.SizeOf<WNDCLASSEXW>(),
                style = 0x0002 | 0x0001, // CS_HREDRAW | CS_VREDRAW
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
                hInstance = Win32.GetModuleHandleW(IntPtr.Zero),
                hCursor = Win32.LoadCursorW(IntPtr.Zero, 32512), // IDC_ARROW
                hbrBackground = (IntPtr)(1 + 5),                 // COLOR_WINDOW + 1
                lpszClassName = _classNamePtr
            };

            _classAtom = Win32.RegisterClassExW(ref wc);
            if (_classAtom == 0)
            {
                throw new InvalidOperationException("RegisterClassEx failed: " + Marshal.GetLastWin32Error());
            }
        }

        private static IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            Windows.TryGetValue(hWnd, out var window);

            switch (msg)
            {
                case Win32.WM_ERASEBKGND:
                    // The class brush would paint COLOR_WINDOW over the extended frame before the
                    // island composites, which is exactly the opaque sheet Mica has to show through.
                    if (window != null && window._transparent)
                    {
                        return (IntPtr)1;
                    }

                    break;
                case Win32.WM_NCCALCSIZE:
                    if (window != null && window._nonClient)
                    {
                        return window.OnNcCalcSize(wParam, lParam);
                    }

                    break;
                case Win32.WM_NCHITTEST:
                    if (window != null && window._nonClient)
                    {
                        return window.OnNcHitTest(lParam);
                    }

                    break;
                case Win32.WM_SYSCOMMAND:
                    // Alt+Space. DefWindowProc would open the menu against a caption this window
                    // does not have, so it is raised at the window's own corner instead.
                    if (window != null && ((int)wParam & 0xFFF0) == Win32.SC_KEYMENU && (long)lParam == ' ')
                    {
                        if (Win32.GetWindowRect(hWnd, out var rect))
                        {
                            window.OpenSystemMenu(rect.left, rect.top);
                            return IntPtr.Zero;
                        }
                    }

                    break;

                case Win32.WM_ACTIVATE:
                    window?.SetActive(((int)wParam & 0xFFFF) != Win32.WA_INACTIVE);
                    break;

                case Win32.WM_SHOWWINDOW:
                case Win32.WM_WINDOWPOSCHANGED:
                    // Neither says "visible" on its own: WM_SHOWWINDOW is not sent for a minimize,
                    // and a position change can be anything. Both are recomputed from the window.
                    window?.UpdateVisibility();
                    break;

                case Win32.WM_DPICHANGED:
                    // The suggested rect is the whole point of this message - it keeps the window
                    // the same physical size across a monitor with a different scale.
                    if (window != null)
                    {
                        var suggested = Marshal.PtrToStructure<RECT>(lParam);

                        Win32.SetWindowPos(hWnd, IntPtr.Zero,
                            suggested.left, suggested.top,
                            suggested.right - suggested.left, suggested.bottom - suggested.top,
                            Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);

                        window.LayoutDragBar();
                        window.Owner?.SizeChanged();
                    }

                    return IntPtr.Zero;

                case Win32.WM_SIZE:
                    // The CoreWindow stub never hears about the resize on its own, and a
                    // ContentDialog's smoke layer keeps whatever size it had when it opened -
                    // microsoft/microsoft-ui-xaml#3577. Forwarding it is that issue's workaround.
                    if (window != null)
                    {
                        CoreWindowBridge.Forward(hWnd, msg, wParam, lParam);
                    }

                    // A minimize reports a 0x0 client rect, and sizing the island to that throws
                    // the whole tree away and animates it back on restore. Nothing needs laying
                    // out while there is nothing to see.
                    // Minimizing is a visibility change, not a size one - which is also how UWP
                    // reports it, and why the size is not published for a 0x0 client rect.
                    window?.UpdateVisibility();

                    if ((int)wParam != Win32.SIZE_MINIMIZED)
                    {
                        window?.Layout();
                        window?.LayoutDragBar();
                        window?.Owner?.SizeChanged();

                        // The maximize button draws a restore glyph while zoomed, and this is the
                        // only place that knows which it is.
                        if (window?.Content is WindowPresenter presenter)
                        {
                            presenter.IsMaximized = (int)wParam == Win32.SIZE_MAXIMIZED;
                        }
                    }

                    return IntPtr.Zero;
                case Win32.WM_SETFOCUS:
                    if (window != null && window._islandHwnd != IntPtr.Zero)
                    {
                        Win32.SetFocus(window._islandHwnd);
                    }
                    return IntPtr.Zero;
                case Win32.WM_CLOSE:
                    // The caption button never reaches XAML on this host: the drag bar answers the
                    // hit test and turns the click into SC_CLOSE, which arrives here. So this is
                    // the one place a user-initiated close can be intercepted.
                    if (window?.Owner != null && !window.Owner.CloseRequested())
                    {
                        return IntPtr.Zero;
                    }

                    break;

                case Win32.WM_EXITSIZEMOVE:
                    // The end of a drag or a resize, rather than every position it passed through.
                    WindowLayout.Save(hWnd, window?._persistedId);
                    break;

                case Win32.WM_DESTROY:
                    if (window != null)
                    {
                        WindowLayout.Save(hWnd, window._persistedId);

                        Windows.Remove(hWnd);
                        window.Dispose();
                    }

                    // Only the last window standing ends the message loop - otherwise closing a
                    // secondary window would take the whole process with it.
                    if (Windows.Count == 0)
                    {
                        Win32.PostQuitMessage(0);
                    }

                    return IntPtr.Zero;
            }

            return Win32.DefWindowProcW(hWnd, msg, wParam, lParam);
        }

        /// <summary>
        /// Terminal's recipe, IslandWindow.cpp:1847 - UseMica is nothing but this one attribute.
        /// It is documented from 22621; on 22000 it fails silently and DWMWA_MICA_EFFECT (1029) is
        /// the only route, which Terminal never implemented.
        /// </summary>
        public int SetBackdrop(int type)
        {
            var value = type;
            var hr = Win32.DwmSetWindowAttribute(_hwnd, Win32.DWMWA_SYSTEMBACKDROP_TYPE, ref value, sizeof(int));

            if (hr < 0 && type == Win32.DWMSBT_MAINWINDOW)
            {
                var legacy = 1;
                hr = Win32.DwmSetWindowAttribute(_hwnd, Win32.DWMWA_MICA_EFFECT, ref legacy, sizeof(int));
            }

            return hr;
        }

        public int ExtendFrame(int left, int top, int right, int bottom)
        {
            var margins = new MARGINS
            {
                cxLeftWidth = left,
                cyTopHeight = top,
                cxRightWidth = right,
                cyBottomHeight = bottom
            };

            return Win32.DwmExtendFrameIntoClientArea(_hwnd, ref margins);
        }

        public int UseDarkTheme(bool dark)
        {
            var value = dark ? 1 : 0;
            return Win32.DwmSetWindowAttribute(_hwnd, Win32.DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
        }

        private void Layout()
        {
            if (_islandHwnd == IntPtr.Zero || !Win32.GetClientRect(_hwnd, out var rect))
            {
                return;
            }

            // SWP_NOZORDER matters: without it this raises the island to the top of the sibling
            // order, above the drag bar, and the caption stops answering WM_NCHITTEST. That is why
            // the title bar only came alive after a minimize and restore - the WM_SIZE path happens
            // to re-raise the drag bar afterwards, and nothing else did.
            Win32.SetWindowPos(_islandHwnd, IntPtr.Zero, 0, 0,
                rect.right - rect.left, rect.bottom - rect.top,
                Win32.SWP_SHOWWINDOW | Win32.SWP_NOZORDER);
        }

        /// <summary>
        /// Tears the island down and then the window itself. Dispose alone leaves the HWND standing
        /// with nothing in it - an empty window the user has to close by hand.
        ///
        /// DestroyWindow sends WM_DESTROY, which calls Dispose again and drops this from Windows;
        /// both are safe to run twice.
        /// </summary>
        private bool _active;
        private bool _visible;

        /// <summary>
        /// Edge-triggered: Windows sends WM_ACTIVATE for every focus change in the process, and the
        /// UWP event this stands in for does not repeat itself.
        /// </summary>
        private void SetActive(bool value)
        {
            if (_active == value)
            {
                return;
            }

            _active = value;
            Owner?.ActivationChanged(value);
        }

        /// <summary>
        /// Recomputed rather than read off a message: no single message means "visible". A minimize
        /// arrives as WM_SIZE, a hide as WM_SHOWWINDOW, and a restore as either - so this asks the
        /// window and publishes only the changes.
        /// </summary>
        private void UpdateVisibility()
        {
            if (_hwnd == IntPtr.Zero)
            {
                return;
            }

            var visible = Win32.IsWindowVisible(_hwnd) && !Win32.IsIconic(_hwnd);

            if (_visible == visible)
            {
                return;
            }

            _visible = visible;
            Owner?.VisibilityChanged(visible);
        }

        public void Close()
        {
            Dispose();

            if (_hwnd != IntPtr.Zero)
            {
                Win32.DestroyWindow(_hwnd);
            }
        }

        /// <summary>
        /// Order matters. Destroying the HWND while the DesktopWindowXamlSource still holds
        /// content takes the process down - the XAML core is left pointing at a dead window.
        /// Detach the content and close the source first, then release the native side.
        /// </summary>
        public void Dispose()
        {
            if (_source != null)
            {
                _source.Content = null;
                _source.Dispose();
                _source = null;
            }

            _native?.Dispose();
            _native = null;
            _islandHwnd = IntPtr.Zero;
        }
    }
}
