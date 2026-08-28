using System;
using System.Runtime.InteropServices;

namespace Telegram.Host
{
    internal delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    internal struct WNDCLASSEXW
    {
        public int cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public IntPtr lpszMenuName;
        public IntPtr lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WINDOWPLACEMENT
    {
        public int length;
        public int flags;
        public int showCmd;
        public POINT ptMinPosition;
        public POINT ptMaxPosition;
        public RECT rcNormalPosition;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct TRACKMOUSEEVENT
    {
        public int cbSize;
        public uint dwFlags;
        public IntPtr hwndTrack;
        public uint dwHoverTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int left, top, right, bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int x, y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NCCALCSIZE_PARAMS
    {
        public RECT rgrc0;
        public RECT rgrc1;
        public RECT rgrc2;
        public IntPtr lppos;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MARGINS
    {
        public int cxLeftWidth, cxRightWidth, cyTopHeight, cyBottomHeight;
    }

    // DISPATCHERQUEUE_THREAD_TYPE / _APARTMENTTYPE, from dispatcherqueue.h.
    [StructLayout(LayoutKind.Sequential)]
    internal struct DispatcherQueueOptions
    {
        public int dwSize;
        public int threadType;
        public int apartmentType;
    }

    internal static partial class Win32
    {
        public const int CW_USEDEFAULT = unchecked((int)0x80000000);

        public const uint WM_DESTROY = 0x0002;
        public const uint WM_SIZE = 0x0005;
        public const uint WM_CLOSE = 0x0010;
        public const int SIZE_MINIMIZED = 1;
        public const int SIZE_MAXIMIZED = 2;
        public const int SIZE_RESTORED = 0;
        public const uint WM_SETFOCUS = 0x0007;
        public const uint WM_ERASEBKGND = 0x0014;
        public const uint WM_ACTIVATE = 0x0006;
        public const uint WM_SHOWWINDOW = 0x0018;
        public const uint WM_WINDOWPOSCHANGED = 0x0047;
        public const uint WM_DPICHANGED = 0x02E0;

        public const int WA_INACTIVE = 0;
        public const uint WM_NCCALCSIZE = 0x0083;
        public const uint WM_NCHITTEST = 0x0084;
        public const uint WM_NCLBUTTONDOWN = 0x00A1;
        public const uint WM_NCLBUTTONUP = 0x00A2;
        public const uint WM_NCMOUSEMOVE = 0x00A0;
        public const uint WM_NCMOUSELEAVE = 0x02A2;
        public const uint WM_NCLBUTTONDBLCLK = 0x00A3;
        public const uint WM_NCRBUTTONDOWN = 0x00A4;
        public const uint WM_NCRBUTTONUP = 0x00A5;
        public const uint WM_SYSCOMMAND = 0x0112;

        // The keyboard and mouse messages the window filter looks at, before TranslateMessage.
        // WM_SYSKEYDOWN is the Alt-modified one, which is what CoreDispatcher called SystemKeyDown.
        public const uint WM_KEYDOWN = 0x0100;
        public const uint WM_SYSKEYDOWN = 0x0104;
        public const uint WM_XBUTTONDOWN = 0x020B;

        // Button state in the low word of wParam on a mouse message.
        public const int MK_LBUTTON = 0x0001;
        public const int MK_RBUTTON = 0x0002;
        public const int MK_MBUTTON = 0x0010;
        public const int MK_XBUTTON1 = 0x0020;
        public const int MK_XBUTTON2 = 0x0040;

        public const int SC_MINIMIZE = 0xF020;
        public const int SC_MAXIMIZE = 0xF030;
        public const int SC_CLOSE = 0xF060;
        public const int SC_RESTORE = 0xF120;
        public const int SC_SIZE = 0xF000;
        public const int SC_MOVE = 0xF010;

        // Alt+Space arrives as SC_KEYMENU with a space, and is the keyboard way into the same menu.
        public const int SC_KEYMENU = 0xF100;

        public const uint MF_BYCOMMAND = 0x00000000;
        public const uint MF_ENABLED = 0x00000000;
        public const uint MF_GRAYED = 0x00000001;

        // Return the command rather than posting it: the menu would send WM_SYSCOMMAND to a window
        // whose caption does not exist, and the caller wants to post it deliberately.
        public const uint TPM_RETURNCMD = 0x0100;
        public const uint TPM_RIGHTBUTTON = 0x0002;

        // Hit-test results. Returning one of the button codes is what makes Windows draw the
        // snap layouts flyout and handle the click itself, on a window that has no real caption.
        public const int HTCLIENT = 1;
        public const int HTCAPTION = 2;
        public const int HTMINBUTTON = 8;
        public const int HTMAXBUTTON = 9;
        public const int HTTOP = 12;
        public const int HTCLOSE = 20;

        public const int SM_CXPADDEDBORDER = 92;
        public const int SM_CYSIZEFRAME = 33;

        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_FRAMECHANGED = 0x0020;
        public const uint SWP_NOACTIVATE = 0x0010;

        public const uint WS_CHILD = 0x40000000;
        public const uint WS_EX_LAYERED = 0x00080000;
        public const uint LWA_ALPHA = 0x00000002;

        // dwmapi.h. 38 is the documented 22H2+ attribute; 1029 is the undocumented one that
        // shipped first in 22000 and is the only way to get Mica on the original Windows 11.
        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        public const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
        public const int DWMWA_MICA_EFFECT = 1029;

        // DWM_SYSTEMBACKDROP_TYPE
        public const int DWMSBT_AUTO = 0;
        public const int DWMSBT_NONE = 1;
        public const int DWMSBT_MAINWINDOW = 2;      // Mica
        public const int DWMSBT_TRANSIENTWINDOW = 3; // Acrylic
        public const int DWMSBT_TABBEDWINDOW = 4;    // Mica Alt

        /// <summary>
        /// Mica needs Windows 11. DWMWA_SYSTEMBACKDROP_TYPE is documented from 22621; on 22000 it
        /// is accepted and does nothing, and DWMWA_MICA_EFFECT is the only route - which Terminal
        /// never bothered to implement.
        /// </summary>
        public static bool IsBackdropSupported => Environment.OSVersion.Version.Build >= 22000;

        /// <summary>
        /// Which tint DWM draws the backdrop in, and which colours it paints the frame with. It has
        /// to be told: left alone it follows the system theme, so an app forced to dark against a
        /// light system gets a light Mica behind a dark window.
        /// </summary>
        public static bool SetDarkMode(IntPtr hwnd, bool dark)
        {
            if (hwnd == IntPtr.Zero)
            {
                return false;
            }

            var value = dark ? 1 : 0;
            return DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int)) >= 0;
        }

        public static bool SetBackdrop(IntPtr hwnd, bool enabled)
        {
            if (hwnd == IntPtr.Zero || !IsBackdropSupported)
            {
                return false;
            }

            if (Environment.OSVersion.Version.Build >= 22621)
            {
                var backdrop = enabled ? DWMSBT_MAINWINDOW : DWMSBT_NONE;
                return DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int)) >= 0;
            }

            var legacy = enabled ? 1 : 0;
            return DwmSetWindowAttribute(hwnd, DWMWA_MICA_EFFECT, ref legacy, sizeof(int)) >= 0;
        }

        public const uint WS_OVERLAPPEDWINDOW = 0x00CF0000;
        public const uint WS_THICKFRAME = 0x00040000;
        public const uint WS_MAXIMIZEBOX = 0x00010000;
        public const uint WS_MINIMIZEBOX = 0x00020000;
        public const uint WS_POPUP = 0x80000000;

        public const int GWL_STYLE = -16;

        public const int SW_HIDE = 0;
        public const int SW_SHOWMAXIMIZED = 3;

        // Null rather than a nearest monitor: a window restored onto a monitor that is gone should
        // fall back to the default placement, not be dragged to whatever is closest.
        public const uint MONITOR_DEFAULTTONULL = 0;
        public const uint MONITOR_DEFAULTTONEAREST = 2;

        public const uint WM_EXITSIZEMOVE = 0x0232;

        // TRACKMOUSEEVENT flags. NONCLIENT is the one that matters here: the drag bar answers the
        // hit test with caption codes, so its mouse messages are all non-client ones.
        public const uint TME_LEAVE = 0x00000002;
        public const uint TME_NONCLIENT = 0x00000010;
        public const uint WS_VISIBLE = 0x10000000;
        public const uint WS_OVERLAPPED = 0x00000000;
        public const uint WS_EX_NOREDIRECTIONBITMAP = 0x00200000;
        public const uint WS_EX_TOOLWINDOW = 0x00000080;

        public const int SW_SHOW = 5;
        public const uint SWP_SHOWWINDOW = 0x0040;
        public const uint SWP_HIDEWINDOW = 0x0080;

        public static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new(-4);

        [LibraryImport("kernel32.dll")]
        public static partial IntPtr GetCurrentProcess();

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        [LibraryImport("user32.dll", SetLastError = true)]
        public static partial ushort RegisterClassExW(ref WNDCLASSEXW lpwcx);

        [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        public static partial IntPtr GetWindowLongPtrW(IntPtr hWnd, int nIndex);

        [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        public static partial IntPtr SetWindowLongPtrW(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool TrackMouseEvent(ref TRACKMOUSEEVENT lpEventTrack);

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [LibraryImport("user32.dll")]
        public static partial IntPtr MonitorFromRect(ref RECT lprc, uint dwFlags);

        [LibraryImport("user32.dll")]
        public static partial IntPtr MonitorFromWindow(IntPtr hWnd, uint dwFlags);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetMonitorInfoW(IntPtr hMonitor, ref MONITORINFO lpmi);

        [LibraryImport("user32.dll", SetLastError = true)]
        public static partial IntPtr CreateWindowExW(uint dwExStyle, IntPtr lpClassName, IntPtr lpWindowName,
            uint dwStyle, int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [LibraryImport("user32.dll")]
        public static partial IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [LibraryImport("user32.dll", SetLastError = true)]
        public static partial IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool DestroyWindow(IntPtr hWnd);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool UpdateWindow(IntPtr hWnd);

        [LibraryImport("user32.dll")]
        public static partial int GetMessageW(out MSG lpMsg, IntPtr hWnd, uint min, uint max);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool TranslateMessage(ref MSG lpMsg);

        [LibraryImport("user32.dll")]
        public static partial IntPtr DispatchMessageW(ref MSG lpMsg);

        [LibraryImport("user32.dll")]
        public static partial void PostQuitMessage(int nExitCode);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetFocus(IntPtr hWnd);

        [LibraryImport("user32.dll")]
        public static partial IntPtr LoadCursorW(IntPtr hInstance, int lpCursorName);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetProcessDpiAwarenessContext(IntPtr value);

        [LibraryImport("kernel32.dll")]
        public static partial IntPtr GetModuleHandleW(IntPtr lpModuleName);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool AdjustWindowRectExForDpi(ref RECT lpRect, uint dwStyle, [MarshalAs(UnmanagedType.Bool)] bool bMenu, uint dwExStyle, uint dpi);

        [LibraryImport("user32.dll")]
        public static partial uint GetDpiForWindow(IntPtr hWnd);

        [LibraryImport("user32.dll")]
        public static partial uint GetDpiForSystem();

        [LibraryImport("user32.dll")]
        public static partial int GetSystemMetricsForDpi(int nIndex, uint dpi);

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool IsZoomed(IntPtr hWnd);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool IsIconic(IntPtr hWnd);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool IsWindowVisible(IntPtr hWnd);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [LibraryImport("user32.dll")]
        public static partial IntPtr SendMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [LibraryImport("dwmapi.dll")]
        public static partial int DwmSetWindowAttribute(IntPtr hWnd, int attribute, ref int value, int size);

        [LibraryImport("dwmapi.dll")]
        public static partial int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS margins);

        [LibraryImport("coremessaging.dll", EntryPoint = "CreateDispatcherQueueController")]
        public static partial int CreateDispatcherQueueController(DispatcherQueueOptions options, out IntPtr controller);

        [LibraryImport("user32.dll")]
        public static partial IntPtr GetActiveWindow();

        [LibraryImport("user32.dll")]
        public static partial IntPtr GetForegroundWindow();

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetForegroundWindow(IntPtr hWnd);

        // GetKeyState is the synchronous one - it answers for the message being dispatched, which
        // is what CoreWindow.GetKeyStateForCurrentThread did. GetAsyncKeyState reads the hardware.
        [LibraryImport("user32.dll")]
        public static partial short GetKeyState(int virtualKey);

        [LibraryImport("user32.dll")]
        public static partial short GetAsyncKeyState(int virtualKey);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetCursorPos(out POINT lpPoint);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        // IntPtr rather than string: DisableRuntimeMarshalling is on, so the caller hands over a
        // pointer from Marshal.StringToHGlobalUni and frees it itself.
        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetWindowTextW(IntPtr hWnd, IntPtr lpString);

        [LibraryImport("user32.dll")]
        public static partial int GetWindowTextW(IntPtr hWnd, IntPtr lpString, int nMaxCount);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

        [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
        public static partial IntPtr FindWindowExW(IntPtr parent, IntPtr after, string className, string windowName);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool PostMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [LibraryImport("user32.dll", EntryPoint = "GetSystemMenu")]
        public static partial IntPtr GetSystemMenu(IntPtr hWnd, [MarshalAs(UnmanagedType.Bool)] bool bRevert);

        [LibraryImport("user32.dll")]
        public static partial int EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetMenuDefaultItem(IntPtr hMenu, uint uItem, [MarshalAs(UnmanagedType.Bool)] bool fByPos);

        [LibraryImport("user32.dll")]
        public static partial int TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved,
            IntPtr hWnd, IntPtr prcRect);

        public const uint WDA_NONE = 0x00000000;
        public const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;
    }
}
