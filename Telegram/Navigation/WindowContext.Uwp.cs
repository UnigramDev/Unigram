//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Native;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Keyboard;
using Telegram.Views.Host;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Security.Credentials.UI;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.WindowManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
#if NET9_0_OR_GREATER
using WinRT;
#endif

namespace Telegram.Navigation
{
    /// <summary>
    /// The UWP half of <see cref="WindowContext"/>: everything that names a <c>Window</c>,
    /// a <c>CoreWindow</c> or an <c>ApplicationView</c>. A Win32 host supplies the same
    /// members from an HWND and a <c>DesktopWindowXamlSource</c> instead, so the two never
    /// meet in one build and nothing here needs an interface or a preprocessor branch.
    /// </summary>
    public partial class WindowContext
    {
        private readonly Window _window;

        private bool _consolidated;

        public CoreWindow CoreWindow => _window.CoreWindow;

        private string _persistedId;
        public string PersistedId
        {
            get => _persistedId;
            set => ApplicationView.GetForCurrentView().PersistedStateId = _persistedId = value;
        }

        public WindowContext(Window window)
        {
            _window = window;
            _current = this;

            if (AppSettings.Diagnostics.DisableXamlGcCollect)
            {
                GarbageCollectionMonitor.StartMonitoring(window.CoreWindow);
            }

            //Current = this;
            Dispatcher = DispatcherContext.Current;
            Id = ApplicationView.GetApplicationViewIdForWindow(window.CoreWindow);
            //Bounds = window.Bounds;

            var scaling = AppSettings.Appearance.Scaling;
            if (scaling is >= 100 and <= 250 && !AppSettings.Appearance.UseDefaultScaling)
            {
                NativeUtils.OverrideScaleForCurrentView(scaling);
            }

            if (CoreApplication.MainView == CoreApplication.GetCurrentView())
            {
                Main = this;
                IsInMainView = true;
            }

            lock (_allLock)
            {
                All.Add(this);
            }

            _inputListener = new InputListener(this);

            window.Activated += OnActivated;
            window.VisibilityChanged += OnVisibilityChanged;
            window.SizeChanged += OnSizeChanged;
            window.Closed += OnClosed;
            window.CoreWindow.ResizeStarted += OnResizeStarted;
            window.CoreWindow.ResizeCompleted += OnResizeCompleted;

#if NET9_0_OR_GREATER
            window.CoreWindow.DispatcherQueue.ShutdownStarting += OnShutdownStarting;
#endif
            window.CoreWindow.DispatcherQueue.ShutdownCompleted += OnShutdownCompleted;

            #region Legacy code

            ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size(320, 500));
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;

            UpdateTitleBar();

            #endregion

            if (LifetimeService.Current.Passcode.IsLockscreenRequired)
            {
                Lock(true);
            }

            ApplicationView.GetForCurrentView().VisibleBoundsChanged += OnVisibleBoundsChanged;
            ApplicationView.GetForCurrentView().Consolidated += OnConsolidated;

            // WARNING: this is used by Xbox (and some Windows users)
            SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;
        }

        public long Handle
        {
            get
            {
                var window = _window.CoreWindow;
                var interop = (ICoreWindowInterop)(object)window;
#if NET9_0_OR_GREATER
                var hWnd = interop.get_WindowHandle();
#else
                var hWnd = interop.WindowHandle;
#endif

                return hWnd.ToInt64();
            }
        }

        public void Activate()
        {
            _window.Activate();
        }

        public IAsyncOperation<UserConsentVerificationResult> RequestUserConsentAsync(string message)
        {
            return UserConsentVerifier.RequestVerificationAsync(message);
        }

        private void OnVisibleBoundsChanged(ApplicationView sender, object args)
        {
            Logger.Debug(sender.VisibleBounds);
            VisibleBoundsChanged?.Invoke(this, args);
        }

        public void Close()
        {
            _ = ConsolidateAsync();
        }

        public async Task ConsolidateAsync()
        {
            if (_consolidated)
            {
                return;
            }

            _consolidated = true;

            var sender = ApplicationView.GetForCurrentView();
            if (await sender.TryConsolidateAsync())
            {
                return;
            }

            OnConsolidated(sender, null);
        }

        private void OnConsolidated(ApplicationView sender, ApplicationViewConsolidatedEventArgs args)
        {
            if (IsInMainView)
            {
                return;
            }

            _consolidated = true;
            _inputListener.Release();
            sender.VisibleBoundsChanged -= OnVisibleBoundsChanged;
            sender.Consolidated -= OnConsolidated;

#if NET9_0_OR_GREATER
            // An open ContentDialog hangs off the popup root, not off Window.Content, so dropping
            // the content below leaves it rooted by the native tree: the drain in
            // OnShutdownStarting collects nothing and the finalizer destroys it after this view's
            // XAML core is gone, where the still-open teardown path faults. Close it while the
            // core is up - and before OnClosed, which detaches the content and with it XamlRoot.
            if (XamlRoot != null)
            {
                foreach (var popup in Windows.UI.Xaml.Media.VisualTreeHelper.GetOpenPopupsForXamlRoot(XamlRoot))
                {
                    if (popup.Child is ContentDialog dialog)
                    {
                        dialog.Hide();
                    }
                }
            }
#endif

            // TODO: since we can't call Close directly,
            // Closed event will be never fired.
            OnClosed(null, null);
            ClearTitleBar(sender);

#if NET9_0_OR_GREATER
            // Unroot the tree here rather than leaving it to the framework: until the content is
            // dropped every element in it is still reachable, so the collect in OnShutdownStarting
            // would have nothing to hand back and the releases would fall past the XAML core.
            _window.Content = null;
#endif

            // TODO: needed? From some tests, this prevented the whole Window root from being garbage collected
            if (SynchronizationContext.Current is SecondaryViewSynchronizationContextDecorator decorator)
            {
                SynchronizationContext.SetSynchronizationContext(decorator.Context);
            }
        }

        private void OnClosed(object sender, CoreWindowEventArgs e)
        {
            Detach();

            _window.Activated -= OnActivated;
            _window.VisibilityChanged -= OnVisibilityChanged;
            _window.SizeChanged -= OnSizeChanged;
            _window.Closed -= OnClosed;
            _window.CoreWindow.ResizeStarted -= OnResizeStarted;
            _window.CoreWindow.ResizeCompleted -= OnResizeCompleted;

            SystemNavigationManager.GetForCurrentView().BackRequested -= OnBackRequested;
        }

        /// <summary>
        /// The window is activated, foreground or not. Every caller of the old
        /// <c>ActivationMode</c> but one was asking this.
        /// </summary>
        public bool IsActive => _window.CoreWindow.ActivationMode != CoreWindowActivationMode.Deactivated;

        /// <summary>
        /// The window is the foreground one. Distinct from <see cref="IsActive"/>: a window can be
        /// activated without being in the foreground, and that middle state is deliberately
        /// neither - see ChatView.Window_Activated, the only caller that needs the difference.
        /// </summary>
        public bool IsForeground => _window.CoreWindow.ActivationMode == CoreWindowActivationMode.ActivatedInForeground;

        // The UWP args stop here: everything downstream sees Telegram.Navigation's own.
        private void OnActivated(object sender, Windows.UI.Core.WindowActivatedEventArgs e)
        {
            var isActive = e.WindowActivationState != CoreWindowActivationState.Deactivated;

            if (_content != null)
            {
                _content.IsActive = isActive;
            }

            Activated?.Invoke(this, new WindowActivatedEventArgs(isActive));

            lock (_activeLock)
            {
                if (isActive)
                {
                    Active = this;
                }
                else if (Active == this)
                {
                    Active = null;
                }
            }
        }

        private void OnVisibilityChanged(object sender, VisibilityChangedEventArgs e)
        {
            VisibilityChanged?.Invoke(this, new WindowVisibilityEventArgs(e.Visible));
        }

        private void OnSizeChanged(object sender, Windows.UI.Core.WindowSizeChangedEventArgs e)
        {
            //Bounds = _window.CoreWindow.Bounds;
            SizeChanged?.Invoke(this, new WindowSizeChangedEventArgs(e.Size));
        }

        private void OnResizeStarted(CoreWindow sender, object args)
        {
            Logger.Debug(sender.Bounds);
            //Bounds = sender.Bounds;

            if (AppSettings.Diagnostics.WindowResizeDebug)
            {
                return;
            }

            if (_window.Content is FrameworkElement element)
            {
                element.Width = sender.Bounds.Width;
                element.Height = sender.Bounds.Height;
                element.HorizontalAlignment = HorizontalAlignment.Left;
                element.VerticalAlignment = VerticalAlignment.Top;
            }
        }

        private void OnResizeCompleted(CoreWindow sender, object args)
        {
            Logger.Debug(sender.Bounds);
            //Bounds = sender.Bounds;

            if (AppSettings.Diagnostics.WindowResizeDebug)
            {
                return;
            }

            if (_window.Content is FrameworkElement element)
            {
                element.Width = double.NaN;
                element.Height = double.NaN;
                element.HorizontalAlignment = HorizontalAlignment.Stretch;
                element.VerticalAlignment = VerticalAlignment.Stretch;
            }
        }

        public INavigationService GetNavigationService()
        {
            return GetNavigationService(_window);
        }

        public static INavigationService GetNavigationService(Window window)
        {
            var content = window.Content;
            if (content is WindowPresenter contentControl)
            {
                content = contentControl.Content as UIElement;
            }

            if (content is RootWindow rootPage && rootPage.NavigationService != null)
            {
                return rootPage.NavigationService;
            }
            else if (content is StandaloneWindow standalonePage && standalonePage.NavigationService != null)
            {
                return standalonePage.NavigationService;
            }
            else if (content is Page { DataContext: ViewModelBase viewModel })
            {
                return viewModel.NavigationService;
            }

            return null;
        }

        #region Helper methods

        public string Title
        {
            get => ApplicationView.GetForCurrentView().Title;
            set => ApplicationView.GetForCurrentView().Title = value;
        }

        public Rect Bounds => _window.CoreWindow.Bounds;

        // Must be used only by BootStrapper
        public Compositor Compositor => _window.Compositor;

        /// <summary>
        /// Pointer position in window coordinates. Screen-relative on desktop, so callers
        /// subtract <see cref="Bounds"/> themselves.
        /// </summary>
        public Point PointerPosition => _window.CoreWindow.PointerPosition;

        /// <summary>
        /// The window area not obscured by system chrome. Distinct from <see cref="Bounds"/>,
        /// which is the whole window.
        /// </summary>
        public Rect VisibleBounds => ApplicationView.GetForCurrentView().VisibleBounds;

        /// <summary>
        /// The size a newly launched window starts at. Genuinely process-wide rather than
        /// per-window, which is why it is static.
        /// </summary>
        public static Size PreferredLaunchViewSize
        {
            get => ApplicationView.PreferredLaunchViewSize;
            set => ApplicationView.PreferredLaunchViewSize = value;
        }

        public bool TryResizeView(Size size)
        {
            return ApplicationView.GetForCurrentView().TryResizeView(size);
        }

        /// <summary>
        /// Brings this window to the foreground.
        /// </summary>
        public IAsyncAction SwitchToAsync()
        {
            return ApplicationViewSwitcher.SwitchAsync(Id);
        }

        public bool IsFullScreenMode => ApplicationView.GetForCurrentView().IsFullScreenMode;

        public void ExitFullScreenMode()
        {
            ApplicationView.GetForCurrentView().ExitFullScreenMode();
        }

        public bool TryEnterFullScreenMode()
        {
            return ApplicationView.GetForCurrentView().TryEnterFullScreenMode();
        }

        public void SetTitleBar(UIElement titleBar)
        {
            _window.SetTitleBar(titleBar);
        }

        /// <summary>
        /// The shell draws minimize, maximize and close for every view, and it is all three or
        /// none - there is no asking it for a subset. So the app's own buttons exist only for the
        /// subsets, and asking for all three means letting the shell do it.
        /// </summary>
        public static bool HasSystemCaptionButtons => true;

        /// <summary>
        /// Undocumented, and the only way a UWP view can be rid of the shell's caption buttons
        /// while keeping the window: ExtendViewIntoTitleBar draws under them but does not remove
        /// them. Reached through the CoreWindow's navigation client.
        /// </summary>
        partial void SetHostCaptionButtons(CaptionButtons buttons)
        {
            // Default, not an AlwaysVisible: the enum has only the two values, and Default is what
            // a view that never asked for anything has.
            var visibility = buttons == CaptionButtons.All
                ? AppWindowTitleBarVisibility.Default
                : AppWindowTitleBarVisibility.AlwaysHidden;

#if NET9_0_OR_GREATER
            var coreWindow = _window.CoreWindow.As<IInternalCoreWindowPhone>();
            var navigationClient = coreWindow.get_NavigationClient().As<IApplicationWindowTitleBarNavigationClient>();

            navigationClient.set_TitleBarPreferredVisibilityMode(visibility);
#else
            var coreWindow = (IInternalCoreWindowPhone)(object)_window.CoreWindow;
            var navigationClient = (IApplicationWindowTitleBarNavigationClient)coreWindow.NavigationClient;

            navigationClient.TitleBarPreferredVisibilityMode = visibility;
#endif
        }

        #endregion

        #region Legacy code

        /// <summary>
        /// Update the Title and Status Bars colors.
        /// </summary>
        public void UpdateTitleBar()
        {
            //Color background;
            Color foreground;
            Color buttonHover;
            Color buttonPressed;

            // Apply buttons feedback based on Light or Dark theme
            var theme = NightModeService.Current.GetCalculatedApplicationTheme();
            if (theme == ApplicationTheme.Dark)
            {
                //background = Color.FromArgb(255, 43, 43, 43);
                foreground = Colors.White;
                buttonHover = Color.FromArgb(25, 255, 255, 255);
                buttonPressed = Color.FromArgb(51, 255, 255, 255);
            }
            else
            {
                //background = Color.FromArgb(255, 230, 230, 230);
                foreground = Colors.Black;
                buttonHover = Color.FromArgb(25, 0, 0, 0);
                buttonPressed = Color.FromArgb(51, 0, 0, 0);
            }

            // Desktop Title Bar
            var titleBar = Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().TitleBar;
            CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;

            // Background
            //titleBar.BackgroundColor = background;
            //titleBar.InactiveBackgroundColor = background;

            // Foreground
            titleBar.ForegroundColor = foreground;
            titleBar.ButtonForegroundColor = foreground;
            titleBar.ButtonHoverForegroundColor = foreground;

            // Buttons
            //titleBar.ButtonBackgroundColor = background;
            //titleBar.ButtonInactiveBackgroundColor = background;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            // Buttons feedback
            titleBar.ButtonPressedBackgroundColor = buttonPressed;
            titleBar.ButtonHoverBackgroundColor = buttonHover;
        }

        private void ClearTitleBar(ApplicationView view)
        {
            var titleBar = view.TitleBar;
            titleBar.ForegroundColor = null;
            titleBar.ButtonForegroundColor = null;
            titleBar.ButtonHoverForegroundColor = null;
            titleBar.ButtonBackgroundColor = null;
            titleBar.ButtonInactiveBackgroundColor = null;
            titleBar.ButtonPressedBackgroundColor = null;
            titleBar.ButtonHoverBackgroundColor = null;
        }

        #endregion

        public static bool IsKeyDown(VirtualKey key)
        {
            //return (InputKeyboardSource.GetKeyStateForCurrentThread(key) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
            return (Window.Current.CoreWindow.GetAsyncKeyState(key) & CoreVirtualKeyStates.Down) != 0;
        }

        public static bool IsKeyDownAsync(VirtualKey key)
        {
            //return (InputKeyboardSource.GetKeyStateForCurrentThread(key) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
            return (Window.Current.CoreWindow.GetAsyncKeyState(key) & CoreVirtualKeyStates.Down) != 0;
        }

        public static VirtualKeyModifiers KeyModifiers()
        {
            //return (InputKeyboardSource.GetKeyStateForCurrentThread(key) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;

            var modifiers = VirtualKeyModifiers.None;
            var coreWindow = Window.Current.CoreWindow;

            if ((coreWindow.GetAsyncKeyState(VirtualKey.Control) & CoreVirtualKeyStates.Down) != 0)
            {
                modifiers |= VirtualKeyModifiers.Control;
            }

            if ((coreWindow.GetAsyncKeyState(VirtualKey.Menu) & CoreVirtualKeyStates.Down) != 0)
            {
                modifiers |= VirtualKeyModifiers.Menu;
            }

            if ((coreWindow.GetAsyncKeyState(VirtualKey.Shift) & CoreVirtualKeyStates.Down) != 0)
            {
                modifiers |= VirtualKeyModifiers.Shift;
            }

            return modifiers;
        }

        public static bool KeyModifiers(VirtualKeyModifiers compare)
        {
            //return (InputKeyboardSource.GetKeyStateForCurrentThread(key) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;

            var modifiers = VirtualKeyModifiers.None;
            var coreWindow = Window.Current.CoreWindow;

            if ((coreWindow.GetAsyncKeyState(VirtualKey.Control) & CoreVirtualKeyStates.Down) != 0)
            {
                modifiers |= VirtualKeyModifiers.Control;
            }

            if ((coreWindow.GetAsyncKeyState(VirtualKey.Menu) & CoreVirtualKeyStates.Down) != 0)
            {
                modifiers |= VirtualKeyModifiers.Menu;
            }

            if ((coreWindow.GetAsyncKeyState(VirtualKey.Shift) & CoreVirtualKeyStates.Down) != 0)
            {
                modifiers |= VirtualKeyModifiers.Shift;
            }

            return modifiers == compare;
        }

        public static async void Activate(string persistedId)
        {
            var oldViewId = WindowContext.Current.Id;

            var already = WindowContext.All.FirstOrDefault(x => x.PersistedId == persistedId);
            if (already != null)
            {
                await already.Dispatcher.DispatchAsync(() => ApplicationViewSwitcher.SwitchAsync(WindowContext.Current.Id, oldViewId).AsTask());
            }
        }

        private void OnShutdownCompleted(DispatcherQueue sender, object args)
        {
            sender.ShutdownCompleted -= OnShutdownCompleted;

            // DIAGNOSTIC: timestamps the far side of the drain, so the tail in a crash report
            // says how much of the teardown ran after it.
            Logger.Info();

            _current = null;

            Theme.Current = null;

            Direct2D.Release();
            MessageBubbleBrush.Release();

            // TODO: needed? From some tests, this prevented the whole Window root from being garbage collected
            if (SynchronizationContext.Current is SecondaryViewSynchronizationContextDecorator decorator)
            {
                SynchronizationContext.SetSynchronizationContext(decorator.Context);
            }
        }

        /// <summary>
        /// The one member of this class with no Win32 counterpart. A thread-static "the window on
        /// this thread" answers something only while a thread hosts exactly one window, which is a
        /// UWP guarantee and not an islands one - see item 0.10 of the notes.
        /// </summary>
        [ThreadStatic]
        private static WindowContext _current;

        public static WindowContext Current
        {
            get
            {
                if (_current == null)
                {
                    Logger.Info(Environment.StackTrace);
                }

                return _current;
            }
        }

        partial void SetBackdropMaterial(WindowPresenter content)
        {
            Microsoft.UI.Xaml.Controls.BackdropMaterial.SetApplyToRootOrPageBackground(content, true);
        }

        /// <summary>
        /// Nothing to do: a UWP picker is owned by the view that opens it, and the app model knows
        /// which that is. Only a desktop host has to say so - see the Win32 half.
        /// </summary>
        internal static void InitializeWithWindow(object target, XamlRoot xamlRoot)
        {
        }

        partial void SetHostContent(UIElement content)
        {
            _window.Content = content;
        }

        partial void SetScreenCaptureEnabled(bool enabled)
        {
            ApplicationView.GetForCurrentView().IsScreenCaptureEnabled = enabled;
        }

        private void OnBackRequested(object sender, BackRequestedEventArgs args)
        {
            Logger.Info();

            //var handled = false;
            //if (ApiInformation.IsApiContractPresent(nameof(Windows.Phone.PhoneContract), 1, 0))
            //{
            //    if (NavigationService?.CanGoBack == true)
            //    {
            //        handled = true;
            //    }
            //}
            //else
            //{
            //    handled = (NavigationService?.CanGoBack == false);
            //}
            var navigationService = NavigationServices.FirstOrDefault();
            var handled = navigationService?.CanGoBack == false;

            RaiseBackRequested(VirtualKey.GoBack, ref handled);
            args.Handled = handled;
        }
    }
}
