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
using Telegram.Common;
using Telegram.Controls;
using Telegram.Host;
using Telegram.Navigation.Services;
using Telegram.Services.Keyboard;
using Windows.Foundation;
using Windows.Security.Credentials.UI;
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
    public partial class WindowContext : IIslandOwner
    {
        private readonly IslandWindow _island;

        private bool _consolidated;

        internal WindowContext(IslandWindow island)
        {
            _island = island;
            _island.Owner = this;
            _current = this;

            Dispatcher = DispatcherContext.Current;

            // An HWND is 64-bit and Id is an int, so it cannot be the handle. Nothing outside the
            // app reads it any more now that ViewService returns WindowContext, so a counter is
            // enough - which is also what makes it meaningful on a host with no view ids.
            Id = Interlocked.Increment(ref _nextId);

            lock (_allLock)
            {
                // The first window is the main one. UWP asks CoreApplication which view it is in;
                // here the answer is simply whichever came first, and ViewService needs it - the
                // chat-already-open search skips the main window.
                if (Main == null)
                {
                    Main = this;
                    IsInMainView = true;
                }

                All.Add(this);
            }

            _inputListener = new InputListener(this);
            _island.Filter = _inputListener;
        }

        private static int _nextId;

        public long Handle => _island.Handle.ToInt64();

        // The plain UserConsentVerifier.RequestVerificationAsync does work here, because the island
        // host still has a CoreWindow - but it parents the dialog on that hidden window, so it
        // lands in the wrong place on screen. The HWND overload is what puts it over our window.
        public IAsyncOperation<UserConsentVerificationResult> RequestUserConsentAsync(string message)
        {
            return UserConsentVerifierInterop.RequestVerificationForWindowAsync(_island.Handle, message);
        }

        private string _persistedId;
        public string PersistedId
        {
            get => _persistedId;
            set => _persistedId = value;
        }

        public void Activate()
        {
            // Show as well as raise: the main window is hidden rather than closed while the
            // notification area icon is up, and this is how it comes back.
            _island.Show();
        }

        public void Close()
        {
            _ = ConsolidateAsync();
        }

        /// <summary>
        /// The window was closed from the outside - the caption button, Alt+F4, the system menu.
        /// Always false: the root may have something to ask first and the answer is awaited, so
        /// the window is destroyed by ConsolidateAsync rather than by the default handler.
        /// </summary>
        bool IIslandOwner.CloseRequested()
        {
            CloseRequestedAsync();
            return false;
        }

        /// <summary>
        /// The static Active goes with it, exactly as the UWP half does from its own Activated
        /// handler: it is what NotificationsService asks which window to show a toast over.
        /// </summary>
        void IIslandOwner.ActivationChanged(bool active)
        {
            if (_content != null)
            {
                _content.IsActive = active;
            }

            Activated?.Invoke(this, new WindowActivatedEventArgs(active));

            lock (_activeLock)
            {
                if (active)
                {
                    Active = this;
                }
                else if (Active == this)
                {
                    Active = null;
                }
            }
        }

        void IIslandOwner.VisibilityChanged(bool visible)
        {
            VisibilityChanged?.Invoke(this, new WindowVisibilityEventArgs(visible));
        }

        /// <summary>
        /// Logical pixels, because that is what the UWP event carries and what every consumer
        /// measures in. VisibleBoundsChanged goes with it: on UWP the visible bounds move whenever
        /// the window does, and the gallery's chrome is laid out from that event.
        /// </summary>
        void IIslandOwner.SizeChanged()
        {
            var bounds = Bounds;
            SizeChanged?.Invoke(this, new WindowSizeChangedEventArgs(new Size(bounds.Width, bounds.Height)));
            VisibleBoundsChanged?.Invoke(this, null);
        }

        private async void CloseRequestedAsync()
        {
            // Closing the main window with the icon showing hides it instead: the icon lives in
            // this process, so quitting would take it with us, and there would be nothing left to
            // open the app from. Every other window closes for real, and Exit on the icon's menu
            // is what actually ends the app.
            if (IsInMainView && SystemTray.IsShowing())
            {
                _island.Hide();
                return;
            }

            if (Content is WindowContent root && !await root.RequestCloseAsync())
            {
                return;
            }

            await ConsolidateAsync();
        }

        public Task ConsolidateAsync()
        {
            if (_consolidated)
            {
                return Task.CompletedTask;
            }

            _consolidated = true;

            lock (_activeLock)
            {
                if (Active == this)
                {
                    Active = null;
                }
            }

            Detach();

            _island.Filter = null;
            _inputListener.Release();

            _backdrop?.Release();
            _backdrop = null;
            _island.Close();

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
            return GetNavigationService(_content?.Content as UIElement);
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

        /// <summary>
        /// Logical pixels, screen-relative, like the CoreWindow bounds this stands in for - and the
        /// client area rather than the window rect, because a CoreWindow has no non-client area to
        /// exclude and every consumer is comparing this against XAML coordinates.
        ///
        /// Getting the units wrong here is invisible at 100% and wrong everywhere else:
        /// TransformToPointerPosition subtracts this from PointerPosition and then subtracts a
        /// XAML transform, so the three have to be in the same space.
        /// </summary>
        public Rect Bounds
        {
            get
            {
                if (!Win32.GetClientRect(_island.Handle, out var client))
                {
                    return default;
                }

                var origin = new POINT();
                Win32.ClientToScreen(_island.Handle, ref origin);

                var scale = Scale;

                return new Rect(origin.x / scale, origin.y / scale,
                    (client.right - client.left) / scale, (client.bottom - client.top) / scale);
            }
        }

        /// <summary>
        /// What a logical pixel is worth on this window's monitor. Per window rather than per
        /// process: two windows can sit on displays with different scales.
        /// </summary>
        private double Scale
        {
            get
            {
                // Zero once the window is gone, and dividing by it would hand out infinities to
                // layout code that has no way to notice.
                var dpi = Win32.GetDpiForWindow(_island.Handle);
                return dpi > 0 ? dpi / 96.0 : 1.0;
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
                if (!Win32.GetCursorPos(out var point))
                {
                    return default;
                }

                var scale = Scale;
                return new Point(point.x / scale, point.y / scale);
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
                if (!Win32.GetClientRect(_island.Handle, out var rect))
                {
                    return default;
                }

                var scale = Scale;

                return new Rect(rect.left / scale, rect.top / scale,
                    (rect.right - rect.left) / scale, (rect.bottom - rect.top) / scale);
            }
        }

        /// <summary>
        /// The size a newly launched window starts at. Genuinely process-wide rather than
        /// per-window, which is why it is static.
        /// </summary>
        public static Size PreferredLaunchViewSize { get; set; }

        /// <summary>
        /// Logical in, physical out - and the client size in, the window size out, since the caller
        /// is asking for room to put content in. TryResizeView on UWP takes the view's size, and a
        /// view is all client area.
        /// </summary>
        public bool TryResizeView(Size size)
        {
            var scale = Scale;

            var frame = new RECT
            {
                right = (int)(size.Width * scale),
                bottom = (int)(size.Height * scale)
            };

            Win32.AdjustWindowRectExForDpi(ref frame, Win32.WS_OVERLAPPEDWINDOW, false, 0,
                Win32.GetDpiForWindow(_island.Handle));

            return Win32.SetWindowPos(_island.Handle, IntPtr.Zero, 0, 0,
                frame.right - frame.left, frame.bottom - frame.top,
                Win32.SWP_NOMOVE | Win32.SWP_NOZORDER);
        }

        /// <summary>
        /// Brings this window to the foreground.
        /// </summary>
        public IAsyncAction SwitchToAsync()
        {
            throw new NotImplementedException();
        }

        public bool IsFullScreenMode => _island.IsFullScreen;

        public void ExitFullScreenMode()
        {
            SetFullScreenMode(false);
        }

        public bool TryEnterFullScreenMode()
        {
            SetFullScreenMode(true);
            return true;
        }

        /// <summary>
        /// VisibleBoundsChanged afterwards, because that is what the callers watch: UWP raises it
        /// when a view enters or leaves full screen, and the gallery re-lays its chrome from there
        /// rather than from the call it just made.
        /// </summary>
        private void SetFullScreenMode(bool value)
        {
            if (_island.IsFullScreen == value)
            {
                return;
            }

            _island.SetFullScreen(value);

            VisibleBoundsChanged?.Invoke(this, null);
        }

        /// <summary>
        /// Weak, and deliberately: the element is rooted by the window's own content tree, so a
        /// strong reference here would not keep it alive - but it would keep a Page alive that set
        /// the title bar and navigated away without clearing it. The subscription runs the other
        /// way round, the element holding this, which pins nothing.
        ///
        /// Null means no root has named one yet, which is not the same as one having taken its
        /// title bar away - see LayoutDragBar.
        /// </summary>
        private WeakReference<FrameworkElement> _titleBar;

        /// <summary>
        /// UWP hands the framework an element and lets it drive the caption; here it drives the
        /// drag bar of gate 1.7 instead. It must not throw: pages call this while they are being
        /// constructed, before there is a tree to transform against, and an exception there is
        /// swallowed by the navigation and shows up only as a Frame with nothing in it.
        /// </summary>
        public void SetTitleBar(UIElement titleBar)
        {
            if (_titleBar != null && _titleBar.TryGetTarget(out var previous))
            {
                previous.SizeChanged -= OnTitleBarSizeChanged;
            }

            var element = titleBar as FrameworkElement;

            // Allocated on the first call whatever it carries, so that "never asked" and "asked
            // for none" stay distinguishable.
            _titleBar ??= new WeakReference<FrameworkElement>(null);
            _titleBar.SetTarget(element);

            if (element != null)
            {
                element.SizeChanged += OnTitleBarSizeChanged;
            }

            _island.SetDragArea(GetDragArea());
        }

        /// <summary>
        /// The element is the sender, so this needs nothing held. It also covers the window being
        /// resized: every title bar in this app stretches, so its width changes with the window.
        /// </summary>
        private void OnTitleBarSizeChanged(object sender, SizeChangedEventArgs e)
        {
            _island.SetDragArea(GetDragArea());
        }

        private Rect? GetDragArea()
        {
            if (_titleBar == null)
            {
                return null;
            }

            if (!_titleBar.TryGetTarget(out var element) || _content == null || element.ActualWidth == 0)
            {
                return new Rect();
            }

            try
            {
                return element.TransformToVisual(_content)
                    .TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
            }
            catch
            {
                // Not in the tree yet. SizeChanged brings us back once it is.
                return new Rect();
            }
        }

        /// <summary>
        /// There is no shell caption here, so the setting is honoured as given rather than
        /// deferred to as on UWP.
        /// </summary>
        public static bool HasSystemCaptionButtons => false;

        partial void SetHostCaptionButtons(CaptionButtons buttons)
        {
            _island.SetCaptionButtons(buttons);
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

        private WindowBackdrop _backdrop;

        /// <summary>
        /// WinUI 2's BackdropMaterial targets a Window this host does not have, so the backdrop is
        /// asked of DWM against the HWND instead - see gate 1.10. The fallback rules are its own,
        /// though, and WindowBackdrop keeps them.
        /// </summary>
        partial void SetBackdropMaterial(WindowPresenter content)
        {
            _backdrop ??= new WindowBackdrop(_island.Handle, content);
        }

        /// <summary>
        /// A picker in a desktop process has no owning window of its own, and shows nothing until
        /// it is given one: every FileOpenPicker, FileSavePicker and FolderPicker has to be
        /// initialized with the HWND it should be modal to.
        /// </summary>
        internal static void InitializeWithWindow(object target, XamlRoot xamlRoot)
        {
            WinRT.Interop.InitializeWithWindow.Initialize(target, GetWindowHandle(xamlRoot));
        }

        partial void SetHostContent(UIElement content)
        {
            _island.Content = content;

            // The root is where this host's pointer input has to be picked up: an island feeds
            // pointer messages through its InputSite rather than the thread's message queue, so the
            // filter never sees them. This is the only moment the root is known.
            _inputListener.Attach(content);
        }

        partial void SetScreenCaptureEnabled(bool enabled)
        {
            // What ApplicationView.IsScreenCaptureEnabled compiles down to anyway.
            Win32.SetWindowDisplayAffinity(_island.Handle,
                enabled ? Win32.WDA_NONE : Win32.WDA_EXCLUDEFROMCAPTURE);
        }
    }
}
