//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

// Shared verbatim with Telegram.Stub, which links this file: the stub draws the notification icon
// for the UWP flavour, and the Win32 flavour draws the same one in process. Two copies of a tray
// icon would drift, and the behaviour has to be identical either way.
//
// Nullable is off because the two projects disagree about it.
#nullable disable

using System;
using System.Runtime.InteropServices;

namespace Telegram.Common
{
    /// <summary>
    /// The three icons, by the resource id the stub's Resources.rc gives them. The app resolves the
    /// same names to files instead, which is why the values still matter to only one of the two.
    /// </summary>
    public enum NotifyIconIcon : int
    {
        Default = 1001,
        Muted = 1002,
        Unmuted = 1003
    }

    public unsafe partial class NotifyIcon
    {
        private const int WM_DESTROY = 0x0002;
        private const int WM_USER = 0x0400;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_COMMAND = 0x0111;

        // Disposing can come from any thread and everything here belongs to the one that made the
        // window, so it goes back through the queue whoever owns the loop is already pumping.
        private const uint WM_DISPOSE = 0x0400 + 2;

        private const int MENU_OPEN = 1001;
        private const int MENU_EXIT = 1002;

        const int WM_USER_CALLBACK = 0x0400 + 1;

        private IntPtr _hwnd;
        private uint _taskbarRestart;
        private IntPtr _menu;
        private readonly WndProc _wndProcDelegate;


        private readonly Func<NotifyIconIcon, IntPtr> _resolve;
        private readonly string _tooltip;

        /// <param name="resolve">Turns one of the three icons into an HICON. The stub loads them
        /// from its own resources, the app from files beside it - the only real difference between
        /// the two hosts.</param>
        public NotifyIcon(Func<NotifyIconIcon, IntPtr> resolve, string tooltip)
        {
            _resolve = resolve;
            _tooltip = tooltip;

            _wndProcDelegate = WndProc2;
            _icon = NotifyIconIcon.Default;

            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

            CreateMessageWindow();
            CreateTrayIcon();
            CreateContextMenu();

            OnCreated();
        }

        private void OnProcessExit(object sender, EventArgs e)
        {
            RemoveTrayIcon();
        }

        private void CreateMessageWindow()
        {
            // Allocated for the life of the process: the class outlives every window made from it.
            var className = Marshal.StringToHGlobalUni("TelegramNotifyIconWindow");

            WNDCLASS wc = new WNDCLASS
            {
                lpszClassName = className,
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
                hInstance = IntPtr.Zero
            };

            ushort classAtom = NativeMethods.RegisterClass(ref wc);
            if (classAtom == 0)
            {
                throw new Exception($"RegisterClass failed with error: {Marshal.GetLastWin32Error()}");
            }

            _hwnd = NativeMethods.CreateWindowEx(
                0, className, IntPtr.Zero,
                0, 0, 0, 0, 0,
                IntPtr.Zero, IntPtr.Zero, wc.hInstance, IntPtr.Zero);

            if (_hwnd == IntPtr.Zero)
            {
                throw new Exception($"CreateWindowEx failed with error: {Marshal.GetLastWin32Error()}");
            }

            _taskbarRestart = NativeMethods.RegisterWindowMessage("TaskbarCreated");
        }

        private void CreateTrayIcon()
        {
            _iconHandle = _resolve(_icon);

            NOTIFYICONDATA data = new NOTIFYICONDATA
            {
                cbSize = sizeof(NOTIFYICONDATA),
                hWnd = _hwnd,
                uID = 1,
                uFlags = 0x00000004 | 0x00000002 | 0x00000001, // NIF_TIP | NIF_ICON | NIF_MESSAGE
                hIcon = _iconHandle,
                uCallbackMessage = WM_USER
            };

            // Not in the initializer: a fixed size buffer has to be written through a pointer.
            data.SetTip(_tooltip);

            NativeMethods.Shell_NotifyIcon(0x00000000, ref data); // NIM_ADD
        }

        private void CreateContextMenu()
        {
            _menu = NativeMethods.CreatePopupMenu();

            NativeMethods.AppendMenu(_menu, 0, MENU_OPEN, "Open");
            NativeMethods.AppendMenu(_menu, 0, MENU_EXIT, "Exit");
        }

        private IntPtr WndProc2(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == _taskbarRestart)
            {
                CreateTrayIcon();
            }

            switch (msg)
            {
                case WM_DISPOSE:
                    RemoveTrayIcon();
                    Closed?.Invoke(this, EventArgs.Empty);
                    break;

                case WM_USER:
                    if ((int)lParam == WM_RBUTTONUP)
                    {
                        ShowContextMenu();
                    }
                    else if ((int)lParam == WM_LBUTTONUP)
                    {
                        OnOpen();
                    }
                    break;

                case WM_COMMAND:
                    // Handle menu item clicks
                    int menuId = (int)wParam;
                    switch (menuId)
                    {
                        case MENU_OPEN:
                            OnOpen();
                            break;
                        case MENU_EXIT:
                            OnExit();
                            break;
                    }
                    break;

                case WM_DESTROY:
                    Dispose();
                    break;
            }

            return NativeMethods.DefWindowProc(hwnd, msg, wParam, lParam);
        }

        private void OnOpen()
        {
            Click?.Invoke(this, EventArgs.Empty);
        }

        private void OnExit()
        {
            Exit?.Invoke(this, EventArgs.Empty);
        }

        const uint MF_BYCOMMAND = 0x00000000;
        const uint MF_STRING = 0x00000000;

        public void UpdateOpenText(string text)
        {
            NativeMethods.ModifyMenu(_menu, MENU_OPEN, MF_BYCOMMAND | MF_STRING, MENU_OPEN, text);
        }

        public void UpdateExitText(string text)
        {
            NativeMethods.ModifyMenu(_menu, MENU_EXIT, MF_BYCOMMAND | MF_STRING, MENU_EXIT, text);
        }

        public event EventHandler Click;

        public event EventHandler Exit;

        const uint IMAGE_ICON = 1;
        const uint LR_DEFAULTCOLOR = 0x0;
        const uint LR_DEFAULTSIZE = 0x40;

        private IntPtr _iconHandle;

        private NotifyIconIcon _icon;
        public NotifyIconIcon Icon
        {
            get => _icon;
            set
            {
                if (_icon != value)
                {
                    NativeMethods.DestroyIcon(_iconHandle);

                    _icon = value;
                    _iconHandle = _resolve(value);

                    var data = new NOTIFYICONDATA();
                    data.cbSize = sizeof(NOTIFYICONDATA);
                    data.hWnd = _hwnd;
                    data.uID = 1;
                    data.uFlags = 0x00000002; // NIF_ICON | NIF_TIP;
                    data.hIcon = _iconHandle;
                    //data.szTip = "My tooltip";

                    // NIM_MODIFY
                    NativeMethods.Shell_NotifyIcon(0x00000001, ref data);
                }
            }
        }

        public void Dispose()
        {
            var sameThread = NativeMethods.GetWindowThreadProcessId(_hwnd, out _) == NativeMethods.GetCurrentThreadId();
            if (sameThread)
            {
                RemoveTrayIcon();
                Closed?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                NativeMethods.PostMessage(_hwnd, WM_DISPOSE, IntPtr.Zero, IntPtr.Zero);
            }
        }


        /// <summary>
        /// The icon is gone and whatever the host wants to do about it is the host's business - the
        /// stub ends its message loop, the app carries on with its windows.
        /// </summary>
        public event EventHandler Closed;

        /// <summary>
        /// Runs once the window, the icon and the menu exist. The stub starts its message loop and
        /// its bridge here; the app has both already.
        /// </summary>
        partial void OnCreated();

        private void ShowContextMenu()
        {
            POINT pt;
            NativeMethods.GetCursorPos(out pt);

            NativeMethods.SetForegroundWindow(_hwnd);

            // TPM_RETURNCMD (0x0100) returns the selected menu item ID
            // TPM_RIGHTBUTTON (0x0002) allows right-click to select
            int cmd = NativeMethods.TrackPopupMenu(_menu, 0x0100 | 0x0002, pt.X, pt.Y, 0, _hwnd, IntPtr.Zero);

            // If TPM_RETURNCMD is used, handle the command directly
            if (cmd > 0)
            {
                HandleMenuCommand(cmd);
            }

            NativeMethods.PostMessage(_hwnd, 0, IntPtr.Zero, IntPtr.Zero); // Dismiss menu properly
        }

        private void HandleMenuCommand(int menuId)
        {
            switch (menuId)
            {
                case MENU_OPEN:
                    OnOpen();
                    break;
                case MENU_EXIT:
                    OnExit();
                    break;
            }
        }

        private void RemoveTrayIcon()
        {
            if (_hwnd != IntPtr.Zero)
            {
                NOTIFYICONDATA data = new NOTIFYICONDATA
                {
                    cbSize = sizeof(NOTIFYICONDATA),
                    hWnd = _hwnd,
                    uID = 1
                };
                NativeMethods.Shell_NotifyIcon(0x00000001, ref data); // NIM_DELETE
            }
        }

        // Nested rather than beside the class: POINT, MSG, WNDCLASS and WndProc are names
        // Telegram.Host uses too, and in a shared namespace they made every file that
        // imports both ambiguous. They are this icon's business.
        // LibraryImport rather than DllImport, and nothing here needs the runtime to marshal anything:
        // the app disables runtime marshalling, so a classic DllImport with SetLastError or a string
        // parameter throws MarshalDirectiveException the moment it is called. Strings are handed over
        // as pointers the caller owns, and the one struct with inline text uses fixed buffers.
        internal static unsafe partial class NativeMethods
        {
            [LibraryImport("user32.dll", SetLastError = true)]
            public static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

            [LibraryImport("kernel32.dll")]
            public static partial uint GetCurrentThreadId();

            [LibraryImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static partial bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

            public static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

            [LibraryImport("user32.dll", EntryPoint = "RegisterClassW", SetLastError = true)]
            public static partial ushort RegisterClass(ref WNDCLASS lpWndClass);

            [LibraryImport("user32.dll", EntryPoint = "CreateWindowExW", SetLastError = true)]
            public static partial IntPtr CreateWindowEx(
                int dwExStyle,
                IntPtr lpClassName,
                IntPtr lpWindowName,
                int dwStyle,
                int x, int y, int nWidth, int nHeight,
                IntPtr hWndParent,
                IntPtr hMenu,
                IntPtr hInstance,
                IntPtr lpParam);

            [LibraryImport("user32.dll", EntryPoint = "GetMessageW")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static partial bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

            [LibraryImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static partial bool TranslateMessage(ref MSG lpMsg);

            [LibraryImport("user32.dll", EntryPoint = "DispatchMessageW")]
            public static partial IntPtr DispatchMessage(ref MSG lpMsg);

            [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")]
            public static partial IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

            [LibraryImport("user32.dll")]
            public static partial void PostQuitMessage(int nExitCode);

            [LibraryImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static partial bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

            [LibraryImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static partial bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA pnid);

            [LibraryImport("user32.dll", EntryPoint = "LoadImageW", SetLastError = true)]
            public static partial IntPtr LoadImage(IntPtr hInst, IntPtr name, uint type, int cx, int cy, uint fuLoad);

            [LibraryImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static partial bool DestroyIcon(IntPtr hIcon);

            [LibraryImport("user32.dll", SetLastError = true)]
            public static partial IntPtr CreatePopupMenu();

            [LibraryImport("user32.dll", EntryPoint = "AppendMenuW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static partial bool AppendMenu(IntPtr hMenu, uint uFlags, int uIDNewItem, string lpNewItem);

            [LibraryImport("user32.dll", EntryPoint = "ModifyMenuW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static partial bool ModifyMenu(IntPtr hMenu, uint uPosition, uint uFlags, uint uIDNewItem, string lpNewItem);

            [LibraryImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static partial bool GetCursorPos(out POINT lpPoint);

            [LibraryImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static partial bool SetForegroundWindow(IntPtr hWnd);

            [LibraryImport("user32.dll", SetLastError = true)]
            public static partial int TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int reserved, IntPtr hWnd, IntPtr rect);

            [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
            public static partial uint RegisterWindowMessage(string lpString);

            [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", SetLastError = true)]
            public static partial IntPtr GetModuleHandle(IntPtr lpModuleName);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSG
        {
            public IntPtr hWnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public int pt_x;
            public int pt_y;
        }

        // Pointers rather than a delegate and strings: blittable, so nothing has to be marshalled.
        [StructLayout(LayoutKind.Sequential)]
        public struct WNDCLASS
        {
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
        }

        public delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uID;
            public int uFlags;
            public int uCallbackMessage;
            public IntPtr hIcon;
            public fixed char szTip[128];
            public int dwState;
            public int dwStateMask;
            public fixed char szInfo[256];
            public int uTimeoutOrVersion;
            public fixed char szInfoTitle[64];
            public int dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;

            public void SetTip(string value)
            {
                fixed (char* buffer = szTip)
                {
                    var length = Math.Min(value.Length, 127);
                    value.AsSpan(0, length).CopyTo(new Span<char>(buffer, 127));
                    buffer[length] = '\0';
                }
            }
        }
    }
}
