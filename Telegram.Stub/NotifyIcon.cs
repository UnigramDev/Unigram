using System.Runtime.InteropServices;

namespace Telegram.Stub
{
    internal class NotifyIconSynchronizationContext : SynchronizationContext
    {
        private readonly IntPtr _hwnd;

        const int WM_USER_CALLBACK = 0x0400 + 1;

        public NotifyIconSynchronizationContext(IntPtr hwnd)
        {
            _hwnd = hwnd;
        }

        public override void Post(SendOrPostCallback d, object? state)
        {
            var handle = GCHandle.Alloc(Tuple.Create(d, state));
            NativeMethods.PostMessage(_hwnd, WM_USER_CALLBACK, IntPtr.Zero, GCHandle.ToIntPtr(handle));
        }
    }

    internal enum NotifyIconIcon : int
    {
        Default = 1001,
        Muted = 1002,
        Unmuted = 1003
    }

    internal class NotifyIcon
    {
        private const int WM_DESTROY = 0x0002;
        private const int WM_USER = 0x0400;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_COMMAND = 0x0111;

        private const int MENU_OPEN = 1001;
        private const int MENU_EXIT = 1002;

        const int WM_USER_CALLBACK = 0x0400 + 1;

        private IntPtr _hwnd;
        private uint _taskbarRestart;
        private IntPtr _menu;
        private readonly WndProc _wndProcDelegate;

        private readonly BridgeApplicationContext _context;
        private readonly NotifyIconSynchronizationContext _synchronization;

        public NotifyIcon()
        {
            AppDomain.CurrentDomain.ProcessExit += (_, _) => RemoveTrayIcon();

            _wndProcDelegate = WndProc2;
            _icon = NotifyIconIcon.Default;

            NativeMethods.SetProcessDpiAwarenessContext(NativeMethods.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
            CreateMessageWindow();
            CreateTrayIcon();
            CreateContextMenu();

            _context = new BridgeApplicationContext(this);
            _synchronization = new NotifyIconSynchronizationContext(_hwnd);

            SynchronizationContext.SetSynchronizationContext(_synchronization);

            MSG msg;
            while (NativeMethods.GetMessage(out msg, IntPtr.Zero, 0, 0))
            {
                if (msg.message == WM_USER_CALLBACK)
                {
                    var handle = GCHandle.FromIntPtr(msg.lParam);
                    var callback = handle.Target as Tuple<SendOrPostCallback, object?>;
                    handle.Free();
                    callback?.Item1.Invoke(callback.Item2);
                }

                NativeMethods.TranslateMessage(ref msg);
                NativeMethods.DispatchMessage(ref msg);
            }
        }

        private void CreateMessageWindow()
        {
            string className = "MyHiddenTrayWindowClass";

            WNDCLASS wc = new WNDCLASS
            {
                lpszClassName = className,
                lpfnWndProc = _wndProcDelegate,
                hInstance = IntPtr.Zero //Marshal.GetHINSTANCE(typeof(Program).Module)
            };

            ushort classAtom = NativeMethods.RegisterClass(ref wc);
            if (classAtom == 0)
            {
                throw new Exception($"RegisterClass failed with error: {Marshal.GetLastWin32Error()}");
            }

            _hwnd = NativeMethods.CreateWindowEx(
                0, className, "",
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
            var hModule = NativeMethods.GetModuleHandle(null);

            _iconHandle = NativeMethods.LoadImage(hModule, new IntPtr((int)_icon), IMAGE_ICON, 0, 0, LR_DEFAULTSIZE | LR_DEFAULTCOLOR);

            NOTIFYICONDATA data = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = 1,
                uFlags = 0x00000004 | 0x00000002 | 0x00000001, // NIF_TIP | NIF_ICON | NIF_MESSAGE
                szTip = "Unigram",
                hIcon = _iconHandle,
                uCallbackMessage = WM_USER
            };

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

        public event EventHandler? Click;

        public event EventHandler? Exit;

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

                    var hModule = NativeMethods.GetModuleHandle(null);

                    _icon = value;
                    _iconHandle = NativeMethods.LoadImage(hModule, new IntPtr((int)value), IMAGE_ICON, 0, 0, LR_DEFAULTSIZE | LR_DEFAULTCOLOR);

                    var data = new NOTIFYICONDATA();
                    data.cbSize = Marshal.SizeOf<NOTIFYICONDATA>();
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
                NativeMethods.PostQuitMessage(0);
            }
            else
            {
                _synchronization.Post(DisposeAsync, null);
            }
        }

        private void DisposeAsync(object? state)
        {
            RemoveTrayIcon();
            NativeMethods.PostQuitMessage(0);
        }

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
                    cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                    hWnd = _hwnd,
                    uID = 1
                };
                NativeMethods.Shell_NotifyIcon(0x00000001, ref data); // NIM_DELETE
            }
        }
    }

    public static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        public static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

        public static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern ushort RegisterClass([In] ref WNDCLASS lpWndClass);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateWindowEx(
            int dwExStyle,
            string lpClassName,
            string lpWindowName,
            int dwStyle,
            int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam
        );

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

        [DllImport("user32.dll")]
        public static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        public static extern IntPtr DispatchMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        public static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool PostQuitMessage(int nExitCode);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA pnid);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr iconName);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr LoadImage(IntPtr hInst, IntPtr name, uint type, int cx, int cy, uint fuLoad);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool AppendMenu(IntPtr hMenu, uint uFlags, int uIDNewItem, string lpNewItem);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool ModifyMenu(IntPtr hMenu, uint uPosition, uint uFlags, uint uIDNewItem, string lpNewItem);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int reserved, IntPtr hWnd, IntPtr rect);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern uint RegisterWindowMessage(string lpString);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string? lpModuleName);
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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct WNDCLASS
    {
        public uint style;
        public WndProc lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string lpszMenuName;
        public string lpszClassName;
    }

    public delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public int uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }
}
