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
using Microsoft.UI.Composition;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Host;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Keyboard;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Security.Credentials.UI;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Telegram.Navigation
{
    /// <summary>
    /// The WinUI 3 half of <see cref="WindowContext"/>, against a <c>Microsoft.UI.Xaml.Window</c>
    /// and its <c>AppWindow</c>. Only one host half is ever in a build, so every member here is the
    /// twin of one in <c>WindowContext.Uwp.cs</c> and <c>WindowContext.Win32.cs</c>, and no call
    /// site has to know which is compiled. What is here is what the framework answers outright;
    /// nothing is invented past that.
    ///
    /// It is much the shorter of the two desktop halves, and that is the point of the flavour: full
    /// screen, the backdrop, the drag region and the caption buttons are framework properties here,
    /// where the island host has to build each of them out of an HWND. What still goes through Win32
    /// is only what has no XAML surface at all - the cursor, the client rect, screen capture
    /// affinity - and it goes through the same declarations the island host uses.
    /// </summary>
    public partial class WindowContext
    {
        private readonly Window _window;

        /// <summary>
        /// Cached rather than asked for per call: the pointer is fixed for the window's life, and
        /// <see cref="Bounds"/> is read from layout code.
        /// </summary>
        private readonly IntPtr _handle;

        private bool _consolidated;

        internal WindowContext(Window window)
        {
            _window = window;
            _handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
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

            // The app draws its own chrome in every flavour. On this host that is one property
            // rather than a custom non-client area: the framework keeps the caption buttons and
            // hands the rest of the window to the content.
            _window.ExtendsContentIntoTitleBar = true;

            _window.Activated += OnActivated;
            _window.VisibilityChanged += OnVisibilityChanged;
            _window.SizeChanged += OnSizeChanged;
            _window.AppWindow.Closing += OnClosing;
            _window.AppWindow.Changed += OnAppWindowChanged;

            _inputListener = new InputListener(this);
        }

        private static int _nextId;

        public long Handle => _handle.ToInt64();

        // A desktop process has no window of its own for the consent dialog to parent on, so it
        // shows nothing until it is given one. The HWND overload is what puts it over our window.
        public IAsyncOperation<UserConsentVerificationResult> RequestUserConsentAsync(string message)
        {
            return UserConsentVerifierInterop.RequestVerificationForWindowAsync(_handle, message);
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
            // notification area icon is up, and this is how it comes back. Every step is a
            // different state it can be in, and none of them covers the others - a hidden window
            // is not a minimized one, and neither is the same as one that is simply behind.
            var appWindow = _window.AppWindow;

            if (!appWindow.IsVisible)
            {
                appWindow.Show(true);
            }

            if (appWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter)
            {
                presenter.Restore();
            }

            _window.Activate();

            // The foreground is a Win32 matter that neither Show nor Activate asks for: a window
            // can be visible, restored and still behind whatever the user was looking at. This is
            // allowed here because the click that brought us in was the last input event.
            Win32.SetForegroundWindow(_handle);
        }

        public void Close()
        {
            _ = ConsolidateAsync();
        }

        /// <summary>
        /// The window was closed from the outside - the caption button, Alt+F4, the system menu.
        /// Always cancelled: the root may have something to ask first and the answer is awaited, so
        /// the window is destroyed by ConsolidateAsync rather than by the framework.
        /// </summary>
        private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
        {
            // Except when Windows is ending the session, which is not a close to negotiate:
            // cancelling one is a veto, and the shutdown waits on it. SystemTray has already let
            // go of the icon by then, so nothing is keeping the app alive either.
            if (SystemTray.IsSessionEnding)
            {
                return;
            }

            args.Cancel = true;
            CloseRequestedAsync();
        }

        /// <summary>
        /// The static Active goes with it, exactly as the other two halves do: it is what
        /// NotificationsService asks which window to show a toast over.
        /// </summary>
        private void OnActivated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
        {
            var active = args.WindowActivationState != WindowActivationState.Deactivated;

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

        private void OnVisibilityChanged(object sender, Microsoft.UI.Xaml.WindowVisibilityChangedEventArgs args)
        {
            VisibilityChanged?.Invoke(this, new WindowVisibilityEventArgs(args.Visible));
        }

        /// <summary>
        /// VisibleBoundsChanged goes with it: on UWP the visible bounds move whenever the window
        /// does, and the gallery's chrome is laid out from that event rather than from SizeChanged.
        /// </summary>
        private void OnSizeChanged(object sender, Microsoft.UI.Xaml.WindowSizeChangedEventArgs args)
        {
            SizeChanged?.Invoke(this, new WindowSizeChangedEventArgs(args.Size));
            VisibleBoundsChanged?.Invoke(this, null);
        }

        private async void CloseRequestedAsync()
        {
            // Every window closes for real, the icon notwithstanding. Hiding it would keep the
            // whole tree, its images and its caches alive for a process the user can only see as
            // an icon; what keeps the app running instead is DispatcherShutdownMode, and the icon
            // builds a new window when it is clicked. Exit on its menu is what ends the app.
            if (!await RequestCloseAsync())
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

            _window.Activated -= OnActivated;
            _window.VisibilityChanged -= OnVisibilityChanged;
            _window.SizeChanged -= OnSizeChanged;
            _window.AppWindow.Closing -= OnClosing;
            _window.AppWindow.Changed -= OnAppWindowChanged;

            _inputListener.Release();

            // Closing is subscribed above and cancels, so the handler has to be off before this or
            // the window would refuse to close and ask the root a second time.
            _window.Close();

            // DispatcherShutdownMode is OnExplicitShutdown, so the framework will not end the
            // process when the last window goes - which is the point while the notification area
            // icon is up, and a process nobody can see or quit when it is not.
            if (All.Count == 0 && !SystemTray.IsShowing())
            {
                Application.Current.Exit();
            }

            return Task.CompletedTask;
        }

        public bool IsActive => Win32.GetActiveWindow() == _handle;

        public bool IsForeground => Win32.GetForegroundWindow() == _handle;

        public INavigationService GetNavigationService()
        {
            return ResolveNavigationService(_content?.Content as UIElement);
        }

        #region Helper methods

        public string Title
        {
            get => _window.Title;
            set => _window.Title = value ?? string.Empty;
        }

        /// <summary>
        /// Logical pixels, screen-relative, and the client area rather than the window rect - the
        /// same contract the Win32 half documents, and through the same calls. AppWindow answers in
        /// physical pixels and from the window origin, which is neither.
        ///
        /// Getting the units wrong here is invisible at 100% and wrong everywhere else:
        /// TransformToPointerPosition subtracts this from PointerPosition and then subtracts a
        /// XAML transform, so the three have to be in the same space.
        /// </summary>
        public Rect Bounds
        {
            get
            {
                if (!Win32.GetClientRect(_handle, out var client))
                {
                    return default;
                }

                var origin = new POINT();
                Win32.ClientToScreen(_handle, ref origin);

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
                var dpi = Win32.GetDpiForWindow(_handle);
                return dpi > 0 ? dpi / 96.0 : 1.0;
            }
        }

        /// <summary>
        /// Must be used only by BootStrapper - the same rule the other halves carry. This is the
        /// compositor XAML composes this window with, and a window is the only thing that has one.
        /// </summary>
        public Compositor Compositor => _window.Compositor;

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
        /// The window area not obscured by system chrome. The content is extended into the title
        /// bar, so that is the client rect.
        /// </summary>
        public Rect VisibleBounds
        {
            get
            {
                if (!Win32.GetClientRect(_handle, out var rect))
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
        /// Logical in, physical out, and the client size either way: the caller is asking for room
        /// to put content in, which is what ResizeClient takes.
        /// </summary>
        public bool TryResizeView(Size size)
        {
            var scale = Scale;

            _window.AppWindow.ResizeClient(new SizeInt32(
                (int)(size.Width * scale),
                (int)(size.Height * scale)));

            return true;
        }

        /// <summary>
        /// Brings this window to the foreground. Nothing to switch to: a window here is not a UWP
        /// view, so activating it is the whole operation - the async signature is the shared one's.
        /// </summary>
        public IAsyncAction SwitchToAsync()
        {
            Activate();
            return Task.CompletedTask.AsAsyncAction();
        }

        public bool IsFullScreenMode => _window.AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen;

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
        /// Nothing to show: a window here is already a window of its own, where a UWP secondary
        /// view starts out hosted inside the one that launched it. Activating is the whole
        /// operation, and the result is what the callers branch on.
        /// </summary>
        public Task<bool> TryShowAsStandaloneAsync()
        {
            Activate();
            return Task.FromResult(true);
        }

        // Not until the gallery's overlay moves from a UWP AppWindow to this window's CompactOverlay presenter.
        public bool IsCompactOverlaySupported => false;

        public bool IsCompactOverlay => _window.AppWindow.Presenter.Kind == AppWindowPresenterKind.CompactOverlay;

        public Task<bool> TryExitCompactOverlayAsync()
        {
            _window.AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
            return Task.FromResult(true);
        }

        public bool IsMouseInteraction => UIViewSettingsInterop.GetForWindow(_handle).UserInteractionMode == UserInteractionMode.Mouse;

        /// <summary>
        /// The title bar's own colours, for a window that puts something dark behind it. Null puts
        /// each one back to the system's.
        /// </summary>
        public void SetTitleBarForeground(Color? foreground)
        {
            var titlebar = _window.AppWindow.TitleBar;
            titlebar.ForegroundColor = foreground;
            titlebar.ButtonForegroundColor = foreground;
        }

        public bool ExtendViewIntoTitleBar
        {
            get => _window.ExtendsContentIntoTitleBar;
            set => _window.ExtendsContentIntoTitleBar = value;
        }

        /// <summary>
        /// Physical in, logical out: AppWindowTitleBar measures in pixels where the UWP title bar
        /// this stands in for measured in the effective units the caller lays out in.
        /// </summary>
        public double SystemOverlayLeftInset => _window.AppWindow.TitleBar.LeftInset / Scale;

        public double SystemOverlayRightInset => _window.AppWindow.TitleBar.RightInset / Scale;

        public double TitleBarHeight => _window.AppWindow.TitleBar.Height / Scale;

        /// <summary>
        /// The media keys and the volume flyout's now-playing panel. They belong to a window here,
        /// not to the thread every window shares, and go with it: the main window's, which
        /// outlives the others.
        /// </summary>
        public static Windows.Media.SystemMediaTransportControls GetSystemMediaTransportControls()
        {
            var window = Main ?? _current;
            return window == null ? null : Windows.Media.SystemMediaTransportControlsInterop.GetForWindow(window._handle);
        }

        private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
        {
            // The insets follow the size and the scale, and there is no event for them alone.
            if (args.DidSizeChange || args.DidVisibilityChange)
            {
                TitleBarChanged?.Invoke(this, args);
            }
        }

        /// <summary>
        /// VisibleBoundsChanged afterwards, because that is what the callers watch: UWP raises it
        /// when a view enters or leaves full screen, and the gallery re-lays its chrome from there
        /// rather than from the call it just made.
        /// </summary>
        private void SetFullScreenMode(bool value)
        {
            if (IsFullScreenMode == value)
            {
                return;
            }

            _window.AppWindow.SetPresenter(value
                ? AppWindowPresenterKind.FullScreen
                : AppWindowPresenterKind.Overlapped);

            VisibleBoundsChanged?.Invoke(this, null);
        }

        /// <summary>
        /// The framework's own drag region, which is what this host has instead of the island's
        /// hand-computed one: the element is tracked by XAML, so it needs neither a size handler
        /// nor a transform of its own.
        ///
        /// It must not throw: pages call this while they are being constructed, and an exception
        /// there is swallowed by the navigation and shows up only as a Frame with nothing in it.
        /// </summary>
        public void SetTitleBar(UIElement titleBar)
        {
            try
            {
                _window.SetTitleBar(titleBar);
            }
            catch
            {
                // Not in the tree yet, or the window is going away.
            }
        }

        /// <summary>
        /// The framework keeps drawing them, exactly as the shell does on UWP - only their layout
        /// is ours. <see cref="SetHostCaptionButtons"/> is what takes individual buttons away.
        /// </summary>
        public static bool HasSystemCaptionButtons => true;

        public static bool HasSystemTabNavigation => false;

        partial void SetHostCaptionButtons(CaptionButtons buttons)
        {
            // Close has no switch of its own: a window that cannot be closed is not a state this
            // presenter has, and every caller that hides buttons keeps Close anyway.
            if (_window.AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsMinimizable = buttons.HasFlag(CaptionButtons.Minimize);
                presenter.IsMaximizable = buttons.HasFlag(CaptionButtons.Maximize);
            }
        }

        /// <summary>
        /// The window's AppWindow, which is what replaces every view-bound thing the UWP flavour
        /// reaches through CoreApplication.GetCurrentView() or ApplicationView.GetForCurrentView().
        /// </summary>
        internal AppWindow AppWindow => _window?.AppWindow;

        /// <summary>
        /// The WinUI 3 cursor for one of the app's cursor types, for a control to assign to its
        /// own <c>ProtectedCursor</c> - which is the only way to set a cursor here, and the reason
        /// <see cref="SetPointerCursor"/> can do nothing. Null for Hidden, which is what
        /// ProtectedCursor takes to mean "no cursor".
        ///
        /// Cached like the CoreCursor array it replaces: InputSystemCursor.Create is a factory
        /// call, and these are assigned from pointer-move handlers.
        ///
        /// Hidden is the one that does not survive the move: null means the element states no
        /// cursor of its own, so the pointer falls back to the arrow rather than disappearing.
        /// There is no shape for "none" and no way to hide the pointer over content short of a
        /// blank Win32 cursor resource, so the gallery keeps its arrow during playback.
        /// </summary>
        internal static InputCursor GetInputCursor(PointerCursorType cursor)
        {
            if (cursor == PointerCursorType.Hidden)
            {
                return null;
            }

            // UI thread only, so a lost race would just allocate one extra cursor.
            return _inputCursors[(int)cursor] ??= InputSystemCursor.Create(cursor switch
            {
                PointerCursorType.Hand => InputSystemCursorShape.Hand,
                PointerCursorType.IBeam => InputSystemCursorShape.IBeam,
                PointerCursorType.SizeWestEast => InputSystemCursorShape.SizeWestEast,
                PointerCursorType.SizeNorthSouth => InputSystemCursorShape.SizeNorthSouth,
                PointerCursorType.SizeNorthwestSoutheast => InputSystemCursorShape.SizeNorthwestSoutheast,
                PointerCursorType.SizeNortheastSouthwest => InputSystemCursorShape.SizeNortheastSouthwest,
                _ => InputSystemCursorShape.Arrow
            });
        }

        private static readonly InputCursor[] _inputCursors = new InputCursor[(int)PointerCursorType.Hidden];

        /// <summary>
        /// The AppWindow an element is actually in, straight off its XamlRoot.
        ///
        /// This exists so that nothing has to reach for <see cref="Current"/>, which is
        /// thread-static and therefore meaningless on a host that puts every window on one thread -
        /// it answers whichever window was created last. It also needs no <c>_mapping</c> entry, so
        /// it works during a page's Loaded whether or not the presenter has registered its
        /// XamlRoot yet.
        /// </summary>
        internal static AppWindow GetAppWindow(UIElement element)
        {
            var environment = element?.XamlRoot?.ContentIslandEnvironment;
            if (environment == null)
            {
                return null;
            }

            return AppWindow.GetFromWindowId(environment.AppWindowId);
        }

        /// <summary>
        /// Its title bar, for roots that colour the caption buttons themselves -
        /// WindowContent.UseDarkCaptionButtons is the one caller. It stands in for
        /// ApplicationView.GetForCurrentView().TitleBar, which is view-bound and gone.
        /// </summary>
        internal AppWindowTitleBar AppWindowTitleBar => AppWindow?.TitleBar;

        /// <summary>
        /// The same colours the UWP half applies to ApplicationView.TitleBar, against the
        /// AppWindow's. Backgrounds stay transparent because the content is extended underneath.
        /// </summary>
        public void UpdateTitleBar()
        {
            Color foreground;
            Color buttonHover;
            Color buttonPressed;

            var theme = NightModeService.Current.GetCalculatedApplicationTheme();
            if (theme == ApplicationTheme.Dark)
            {
                foreground = Colors.White;
                buttonHover = Color.FromArgb(25, 255, 255, 255);
                buttonPressed = Color.FromArgb(51, 255, 255, 255);
            }
            else
            {
                foreground = Colors.Black;
                buttonHover = Color.FromArgb(25, 0, 0, 0);
                buttonPressed = Color.FromArgb(51, 0, 0, 0);
            }

            var titleBar = _window.AppWindow.TitleBar;

            titleBar.ForegroundColor = foreground;
            titleBar.ButtonForegroundColor = foreground;
            titleBar.ButtonHoverForegroundColor = foreground;

            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            titleBar.ButtonPressedBackgroundColor = buttonPressed;
            titleBar.ButtonHoverBackgroundColor = buttonHover;
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

        public static bool IsKeyDownForEvent(VirtualKey key)
        {
            return IsKeyDown(key);
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
        /// TEMPORARY, and inherited from the Win32 half along with its problem: a thread-static "the
        /// window on this thread" answers something only while a thread hosts exactly one window,
        /// and this host puts every window on one thread by design, so it is even less true here
        /// than there.
        /// </summary>
        [ThreadStatic]
        private static WindowContext _current;

        public static WindowContext Current => _current;

        #endregion

        /// <summary>
        /// One property, where the island host has to ask DWM against the HWND and keep the
        /// fallback rules itself: the framework picks the backdrop it can actually draw and falls
        /// back on its own when the machine cannot.
        /// </summary>
        partial void SetBackdropMaterial(WindowPresenter content)
        {
            _window.SystemBackdrop ??= new MicaBackdrop();
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
            _window.Content = content;

            // The root is where this host's pointer input has to be picked up, exactly as on the
            // island host: this is the only moment the root is known.
            _inputListener.Attach(content);
        }

        partial void SetScreenCaptureEnabled(bool enabled)
        {
            // What ApplicationView.IsScreenCaptureEnabled compiles down to anyway.
            Win32.SetWindowDisplayAffinity(_handle,
                enabled ? Win32.WDA_NONE : Win32.WDA_EXCLUDEFROMCAPTURE);
        }
    }
}
