//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Navigation.Services;
using Telegram.Services.Keyboard;
using Telegram.Host;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Composition;
using Windows.UI.Xaml;

namespace Telegram.Navigation
{
    /// <summary>
    /// The Win32 half of <see cref="WindowContext"/>, against an HWND and a
    /// <c>DesktopWindowXamlSource</c> instead of a <c>Window</c>. Only one host half is ever in a
    /// build, so every member here is the twin of one in <c>WindowContext.Uwp.cs</c> and no call
    /// site has to know which is compiled.
    ///
    /// FIRST CUT. Everything the spike already answered is real; the rest throws, deliberately, so
    /// that running the app finds them in the order they actually matter rather than in the order
    /// they were written. What is absent matters as much as what throws: members that only make
    /// sense on UWP - <c>CoreWindow</c>, <c>GetNavigationService(Window)</c>, the
    /// <c>IActivatedEventArgs</c> overloads - have no twin, so their callers are compile errors,
    /// which is the point of building this at all.
    /// </summary>
    public partial class WindowContext
    {
        private readonly IslandWindow _island;

        private bool _consolidated;

        internal WindowContext(IslandWindow island)
        {
            _island = island;
            _current = this;

            Dispatcher = DispatcherContext.Current;

            // An HWND is 64-bit and Id is an int, so it cannot be the handle. Nothing outside the
            // app reads it any more now that ViewService returns WindowContext, so a counter is
            // enough - which is also what makes it meaningful on a host with no view ids.
            Id = Interlocked.Increment(ref _nextId);

            lock (_allLock)
            {
                All.Add(this);
            }

            _inputListener = new InputListener(this);
            _island.Filter = _inputListener;
        }

        private static int _nextId;

        public long Handle => _island.Handle.ToInt64();

        #region Pointer cursor

        // One HCURSOR per type, shared, for the same reason the UWP half caches CoreCursor: this
        // is called at pointer sample rate. LoadCursorW hands back a shared system cursor that
        // must not be destroyed.
        private static readonly IntPtr[] _cursors = new IntPtr[(int)PointerCursorType.Hidden];

        public static void SetPointerCursor(PointerCursorType cursor)
        {
            // Hidden has no HCURSOR - a null one is how a Win32 window hides the pointer.
            if (cursor == PointerCursorType.Hidden)
            {
                Win32.SetCursor(IntPtr.Zero);
                return;
            }

            var index = (int)cursor;
            if (_cursors[index] == IntPtr.Zero)
            {
                _cursors[index] = Win32.LoadCursorW(IntPtr.Zero, cursor switch
                {
                    PointerCursorType.Hand => Win32.IDC_HAND,
                    PointerCursorType.IBeam => Win32.IDC_IBEAM,
                    PointerCursorType.SizeWestEast => Win32.IDC_SIZEWE,
                    PointerCursorType.SizeNorthSouth => Win32.IDC_SIZENS,
                    PointerCursorType.SizeNorthwestSoutheast => Win32.IDC_SIZENWSE,
                    PointerCursorType.SizeNortheastSouthwest => Win32.IDC_SIZENESW,
                    _ => Win32.IDC_ARROW
                });
            }

            Win32.SetCursor(_cursors[index]);
        }

        #endregion

        private string _persistedId;
        public string PersistedId
        {
            get => _persistedId;
            set => _persistedId = value;
        }

        public void Activate()
        {
            Win32.SetForegroundWindow(_island.Handle);
        }

        public void Close()
        {
            _ = ConsolidateAsync();
        }

        public Task ConsolidateAsync()
        {
            if (_consolidated)
            {
                return Task.CompletedTask;
            }

            _consolidated = true;

            Detach();

            _island.Filter = null;
            _inputListener.Release();
            _island.Dispose();

            return Task.CompletedTask;
        }

        /// <summary>
        /// Gate 1.7 established that the island swallows the caption, so activation state comes
        /// from the top-level HWND rather than from anything XAML knows.
        /// </summary>
        public bool IsActive => Win32.GetActiveWindow() == _island.Handle;

        public bool IsForeground => Win32.GetForegroundWindow() == _island.Handle;

        public INavigationService GetNavigationService()
        {
            return GetNavigationService(_content?.Content);
        }

        #region Helper methods

        public string Title
        {
            get
            {
                var buffer = Marshal.AllocHGlobal(512 * sizeof(char));

                try
                {
                    var length = Win32.GetWindowTextW(_island.Handle, buffer, 512);
                    return length > 0 ? Marshal.PtrToStringUni(buffer, length) : string.Empty;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            set
            {
                var text = Marshal.StringToHGlobalUni(value ?? string.Empty);

                try
                {
                    Win32.SetWindowTextW(_island.Handle, text);
                }
                finally
                {
                    Marshal.FreeHGlobal(text);
                }
            }
        }

        public Rect Bounds
        {
            get
            {
                Win32.GetWindowRect(_island.Handle, out var rect);
                return new Rect(rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top);
            }
        }

        /// <summary>
        /// Must be used only by BootStrapper. Window.Current is not null inside an island - it is
        /// a per-thread stub, which gate 1.10 found while chasing Mica - and its Compositor is the
        /// one XAML composes this thread's islands with. Legitimate here for the same reason
        /// Current is legitimate in the UWP half: it answers a per-thread question on the host
        /// that owns the assumption.
        /// </summary>
        public Compositor Compositor => Window.Current.Compositor;

        /// <summary>
        /// Pointer position in window coordinates. Screen-relative on desktop, so callers
        /// subtract <see cref="Bounds"/> themselves.
        /// </summary>
        public Point PointerPosition
        {
            get
            {
                Win32.GetCursorPos(out var point);
                return new Point(point.x, point.y);
            }
        }

        /// <summary>
        /// The window area not obscured by system chrome. With the custom caption of gate 1.7
        /// there is none, so this is the client rect.
        /// </summary>
        public Rect VisibleBounds
        {
            get
            {
                Win32.GetClientRect(_island.Handle, out var rect);
                return new Rect(rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top);
            }
        }

        /// <summary>
        /// The size a newly launched window starts at. Genuinely process-wide rather than
        /// per-window, which is why it is static.
        /// </summary>
        public static Size PreferredLaunchViewSize { get; set; }

        public bool TryResizeView(Size size)
        {
            return Win32.SetWindowPos(_island.Handle, IntPtr.Zero, 0, 0, (int)size.Width, (int)size.Height,
                Win32.SWP_NOMOVE | Win32.SWP_NOZORDER);
        }

        /// <summary>
        /// Brings this window to the foreground.
        /// </summary>
        public IAsyncAction SwitchToAsync()
        {
            throw new NotImplementedException();
        }

        public bool IsFullScreenMode => throw new NotImplementedException();

        public void ExitFullScreenMode()
        {
            throw new NotImplementedException();
        }

        public bool TryEnterFullScreenMode()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// UWP hands the framework an element and lets it drive the caption. Here the drag bar
        /// HWND of gate 1.7 already covers the caption strip, so this is a no-op for now - and it
        /// must not throw: pages call it while they are being constructed, where an exception is
        /// swallowed by the navigation and shows up only as a Frame with nothing in it.
        ///
        /// TODO: drive the drag bar's rect from the element's bounds instead of a constant height.
        /// </summary>
        public void SetTitleBar(UIElement titleBar, bool collapsed = false)
        {
        }

        /// <summary>
        /// Nothing to colour: this window has no system caption, by design. See gate 1.7.
        /// </summary>
        public void UpdateTitleBar()
        {
        }

        #endregion

        #region Static code

        public static bool IsKeyDown(VirtualKey key)
        {
            return (Win32.GetKeyState((int)key) & 0x8000) != 0;
        }

        public static bool IsKeyDownAsync(VirtualKey key)
        {
            return (Win32.GetAsyncKeyState((int)key) & 0x8000) != 0;
        }

        public static VirtualKeyModifiers KeyModifiers()
        {
            var modifiers = VirtualKeyModifiers.None;

            if (IsKeyDown(VirtualKey.Control))
            {
                modifiers |= VirtualKeyModifiers.Control;
            }

            if (IsKeyDown(VirtualKey.Menu))
            {
                modifiers |= VirtualKeyModifiers.Menu;
            }

            if (IsKeyDown(VirtualKey.Shift))
            {
                modifiers |= VirtualKeyModifiers.Shift;
            }

            return modifiers;
        }

        public static bool KeyModifiers(VirtualKeyModifiers compare)
        {
            return KeyModifiers() == compare;
        }

        public static void Activate(string persistedId)
        {
            var already = All.Find(x => x.PersistedId == persistedId);
            already?.Activate();
        }

        /// <summary>
        /// TEMPORARY, and the one member that must not survive - see item 0.10. A thread-static
        /// "the window on this thread" answers something only while a thread hosts exactly one
        /// window, and gate 1.8a showed islands do not guarantee that. It is here only so the
        /// first Win32 build reports the work nobody has catalogued yet, instead of forty-odd
        /// copies of a site that is already on the list.
        /// </summary>
        [ThreadStatic]
        private static WindowContext _current;

        public static WindowContext Current => _current;

        #endregion

        /// <summary>
        /// Deliberately empty for now: DWM paints this window's backdrop, and WinUI 2's
        /// BackdropMaterial is a UWP window's way of asking for the same thing.
        /// </summary>
        partial void SetBackdropMaterial(WindowControl content)
        {
        }

        partial void SetHostContent(UIElement content)
        {
            _island.Content = content;
        }

        partial void SetScreenCaptureEnabled(bool enabled)
        {
            // What ApplicationView.IsScreenCaptureEnabled compiles down to anyway.
            Win32.SetWindowDisplayAffinity(_island.Handle,
                enabled ? Win32.WDA_NONE : Win32.WDA_EXCLUDEFROMCAPTURE);
        }
    }
}
