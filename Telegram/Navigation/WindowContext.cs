//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Native;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Keyboard;
using Telegram.Td.Api;
using Telegram.Views;
using Telegram.Views.Authorization;
using Telegram.Views.Calls;
using Telegram.Views.Host;
using Telegram.Views.Popups;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.WindowManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
#if NET9_0_OR_GREATER
using WinRT;
#endif

namespace Telegram.Navigation
{
    public class PopupActivatedEventArgs : EventArgs
    {
        public bool IsActive { get; }

        public PopupActivatedEventArgs(bool isActive)
        {
            IsActive = isActive;
        }
    }

    /// <summary>
    /// Raised by <see cref="WindowContext.Activated"/>. Deliberately not the UWP
    /// <c>Windows.UI.Core.WindowActivatedEventArgs</c>: that one carries a
    /// <c>CoreWindowActivationState</c>, and an island host has no CoreWindow. Every consumer
    /// only ever asked whether the state was Deactivated, so the args carry the answer.
    /// </summary>
    public class WindowActivatedEventArgs : EventArgs
    {
        public bool IsActive { get; }

        public WindowActivatedEventArgs(bool isActive)
        {
            IsActive = isActive;
        }
    }

    public class WindowSizeChangedEventArgs : EventArgs
    {
        public Size Size { get; }

        public WindowSizeChangedEventArgs(Size size)
        {
            Size = size;
        }
    }

    public class WindowVisibilityEventArgs : EventArgs
    {
        public bool IsVisible { get; }

        public WindowVisibilityEventArgs(bool isVisible)
        {
            IsVisible = isVisible;
        }
    }

    /// <summary>
    /// The cursors the app actually uses, so call sites do not name <c>CoreCursorType</c>.
    /// <see cref="Hidden"/> is the gallery's chrome-less mode.
    /// </summary>
    public enum PointerCursorType
    {
        Arrow,
        Hand,
        IBeam,
        SizeWestEast,
        SizeNorthSouth,
        SizeNorthwestSoutheast,
        SizeNortheastSouthwest,
        Hidden
    }

    public partial class WindowControl : Page, IPopupHost, IToastHost
    {
        private readonly WindowContext _context;

        public WindowControl(WindowContext window)
        {
            _context = window;
        }

        public void PopupOpened()
        {
            _context.RaisePopupActivated(true);

            if (OverlayWindow.Current != null)
            {
                OverlayWindow.Current.PopupOpened();
            }
            else if (Content is IPopupHost content)
            {
                content.PopupOpened();
            }
        }

        public void PopupClosed()
        {
            _context.RaisePopupActivated(false);

            if (OverlayWindow.Current != null)
            {
                OverlayWindow.Current.PopupClosed();
            }
            else if (Content is IPopupHost content)
            {
                content.PopupClosed();
            }
        }

        public void ToastOpened(TeachingTip toast)
        {
            Resources.Remove("TeachingTip");
            Resources.Add("TeachingTip", toast);
        }

        public void ToastClosed(TeachingTip toast)
        {
            if (Resources.TryGetValue("TeachingTip", out object cached))
            {
                if (cached == toast)
                {
                    Resources.Remove("TeachingTip");
                }
            }
        }
    }

    public partial class WindowContext
    {
        private readonly Window _window;

        private bool _consolidated;

        private readonly InputListener _inputListener;
        public InputListener InputListener => _inputListener;

        public CoreWindow CoreWindow => _window.CoreWindow;

        #region Pointer cursor

        // One CoreCursor per type, shared. Every call site used to allocate a fresh one, and
        // FormattedTextBlock does this at pointer sample rate; CoreCursor is immutable and its
        // id argument only means anything for CoreCursorType.Custom, so sharing is safe.
        // Indexed by PointerCursorType, which is why Hidden - the null cursor - is last.
        private static readonly CoreCursor[] _cursors = new CoreCursor[(int)PointerCursorType.Hidden];

        public static void SetPointerCursor(PointerCursorType cursor)
        {
            Window.Current.CoreWindow.PointerCursor = GetPointerCursor(cursor);
        }

        private static CoreCursor GetPointerCursor(PointerCursorType cursor)
        {
            if (cursor == PointerCursorType.Hidden)
            {
                return null;
            }

            // UI thread only, so a lost race would just allocate one extra cursor.
            return _cursors[(int)cursor] ??= new CoreCursor(cursor switch
            {
                PointerCursorType.Hand => CoreCursorType.Hand,
                PointerCursorType.IBeam => CoreCursorType.IBeam,
                PointerCursorType.SizeWestEast => CoreCursorType.SizeWestEast,
                PointerCursorType.SizeNorthSouth => CoreCursorType.SizeNorthSouth,
                PointerCursorType.SizeNorthwestSoutheast => CoreCursorType.SizeNorthwestSoutheast,
                PointerCursorType.SizeNortheastSouthwest => CoreCursorType.SizeNortheastSouthwest,
                _ => CoreCursorType.Arrow
            }, 0);
        }

        #endregion

        public int Id { get; }

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

            if (SettingsService.Current.Diagnostics.DisableXamlGcCollect)
            {
                GarbageCollectionMonitor.StartMonitoring(window.CoreWindow);
            }

            //Current = this;
            Dispatcher = new DispatcherContext(window.CoreWindow.DispatcherQueue);
            Id = ApplicationView.GetApplicationViewIdForWindow(window.CoreWindow);
            Bounds = window.Bounds;

            var scaling = SettingsService.Current.Appearance.Scaling;
            if (scaling is >= 100 and <= 250 && !SettingsService.Current.Appearance.UseDefaultScaling)
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

            _inputListener = new InputListener(window);

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

        private void OnVisibleBoundsChanged(ApplicationView sender, object args)
        {
            Logger.Debug(sender.VisibleBounds);
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
            lock (_allLock)
            {
                if (_xamlRoot != null)
                {
                    _mapping.Remove(_xamlRoot);
                }

                All.Remove(this);
            }

            NavigationServices.ForEach(x => x.Suspend());
            NavigationServices.Clear();

            _content = null;

            _window.Activated -= OnActivated;
            _window.VisibilityChanged -= OnVisibilityChanged;
            _window.SizeChanged -= OnSizeChanged;
            _window.Closed -= OnClosed;
            _window.CoreWindow.ResizeStarted -= OnResizeStarted;
            _window.CoreWindow.ResizeCompleted -= OnResizeCompleted;
        }

#if NET9_0_OR_GREATER
        // A stuck finalizer must not keep a closed window alive, so the drain is bounded.
        private static readonly TimeSpan ShutdownDrainTimeout = TimeSpan.FromSeconds(2);

        private Task _drain;

        private void OnShutdownStarting(DispatcherQueue sender, Windows.System.DispatcherQueueShutdownStartingEventArgs args)
        {
            sender.ShutdownStarting -= OnShutdownStarting;

            // This view's RCWs are context bound. Released from the finalizer thread they marshal
            // back here through IContextCallback, and once the XAML core is gone unparenting one
            // faults on a null CCoreServices - the access violation seen a moment after closing a
            // window that held a FormattedTextBlock, whose runs are XamlDirect objects.
            //
            // So collect while this thread still pumps and XAML is still up. The deferral is what
            // keeps it pumping; the wait has to run off-thread, because blocking here would block
            // the very apartment the finalizer needs to call into.
            var deferral = args.GetDeferral();

            _drain = Task.Run(Drain);

            Task.WhenAny(_drain, Task.Delay(ShutdownDrainTimeout))
                .ContinueWith(OnDrained, deferral, TaskScheduler.Default);
        }

        // DIAGNOSTIC: the releases still fault after this drain, and the two explanations want
        // opposite fixes - either the drain lost its race against a few hundred blocking
        // cross-apartment releases, or the peers were still reference-tracked by the native tree
        // and there was nothing here to collect. A pass that finishes in milliseconds means the
        // second; one that hits the timeout means the first. Logger's tail ships with the crash.
        private static void Drain()
        {
            var started = Logger.TickCount;
            var before = GC.GetTotalMemory(false);

            GC.Collect();
            GC.WaitForPendingFinalizers();

            var first = Logger.TickCount - started;

            // A release freed by the first pass can unroot the next wrapper along.
            GC.Collect();
            GC.WaitForPendingFinalizers();

            Logger.Info($"first pass {first}ms, second {Logger.TickCount - started - first}ms, heap {before >> 10}K -> {GC.GetTotalMemory(false) >> 10}K");
        }

        private void OnDrained(Task task, object state)
        {
            Logger.Info(_drain.IsCompleted ? "drained" : $"timed out after {ShutdownDrainTimeout.TotalMilliseconds}ms");

            (state as Deferral)?.Complete();
        }
#endif

        private void OnShutdownCompleted(DispatcherQueue sender, object args)
        {
            sender.ShutdownCompleted -= OnShutdownCompleted;

            // DIAGNOSTIC: timestamps the far side of the drain, so the tail in a crash report
            // says how much of the teardown ran after it.
            Logger.Info();

            _current = null;

            Theme.Current = null;

            ThemeIncoming.Release();
            ThemeOutgoing.Release();

            PlaceholderHelper.Release();
            MessageBubbleBrush.Release();
            AnimatedImageLoader.Release();
            ProfilePicture.Loader.Release();

            // TODO: needed? From some tests, this prevented the whole Window root from being garbage collected
            if (SynchronizationContext.Current is SecondaryViewSynchronizationContextDecorator decorator)
            {
                SynchronizationContext.SetSynchronizationContext(decorator.Context);
            }
        }

        public bool IsInMainView { get; }

        public bool IsCallInProgress { get; private set; }

        public XamlRoot XamlRoot => _content?.XamlRoot;

        private bool _contentMaterial;

        private WindowControl _content;
        /// <summary>
        /// Characters typed into this window that nothing in the tree consumed. Raised from the
        /// window's root element, so it replaces <c>CoreWindow.CharacterReceived</c> without
        /// needing a CoreWindow - which a XAML island does not have.
        /// </summary>
        public event TypedEventHandler<WindowContext, CharacterReceivedRoutedEventArgs> CharacterReceived;

        private void OnContentCharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs args)
        {
            CharacterReceived?.Invoke(this, args);
        }

        public UIElement Content
        {
            get => _locked != null ? _lockedContent : _content?.Content;
            set
            {
                if (_locked != null)
                {
                    _lockedContent = value;
                }
                else
                {
                    SetContent(value);
                }

                IsCallInProgress = value is VoipWindow or GroupCallWindow or LiveStreamWindow;
            }
        }

        private void SetContent(UIElement content)
        {
            if (_content != null)
            {
                _content.Content = content;
            }
            else
            {
                _content = new WindowControl(this)
                {
                    RequestedTheme = SettingsService.Current.Appearance.GetCalculatedElementTheme(),
                    Content = content,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalContentAlignment = VerticalAlignment.Stretch
                };

                _content.Loading += OnLoading;
                _content.Loaded += OnLoaded;

                // Deliberately +=, not AddHandler(handledEventsToo): a focused TextBox or
                // RichEditBox marks the character handled, so it never reaches here - which is
                // what the type-to-search sites used to approximate with a FocusManager check.
                _content.CharacterReceived += OnContentCharacterReceived;

                _window.Content = _content;
            }

            if (!_contentMaterial && content is RootWindow or StandaloneWindow or TabbedWindow or WebAppWindow)
            {
                _contentMaterial = true;
                BackdropMaterial.SetApplyToRootOrPageBackground(_content, true);
            }
        }

        private void OnLoading(FrameworkElement sender, object args)
        {
            sender.Loading -= OnLoading;

            lock (_allLock)
            {
                _xamlRoot = sender.XamlRoot;
                _mapping[sender.XamlRoot] = this;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is Control control)
            {
                control.Loaded -= OnLoaded;
            }

            ViewService.OnWindowLoaded();
        }

        public ElementTheme ActualTheme => _content?.ActualTheme ?? SettingsService.Current.Appearance.GetCalculatedElementTheme();

        public ElementTheme RequestedTheme
        {
            get => _content?.RequestedTheme ?? ElementTheme.Default;
            set => _content?.RequestedTheme = value;
        }

        public double RasterizationScale => _content?.XamlRoot?.RasterizationScale ?? 1;

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

        public bool IsPopupOpened { get; private set; }

        public event EventHandler<PopupActivatedEventArgs> PopupActivated;

        public void RaisePopupActivated(bool opened)
        {
            IsPopupOpened = opened;
            PopupActivated?.Invoke(this, new PopupActivatedEventArgs(opened));
        }

        public event EventHandler<WindowActivatedEventArgs> Activated;

        // The UWP args stop here: everything downstream sees Telegram.Navigation's own.
        private void OnActivated(object sender, Windows.UI.Core.WindowActivatedEventArgs e)
        {
            var isActive = e.WindowActivationState != CoreWindowActivationState.Deactivated;
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

        public event EventHandler<WindowVisibilityEventArgs> VisibilityChanged;

        private void OnVisibilityChanged(object sender, VisibilityChangedEventArgs e)
        {
            VisibilityChanged?.Invoke(this, new WindowVisibilityEventArgs(e.Visible));
        }

        public event EventHandler<WindowSizeChangedEventArgs> SizeChanged;

        private void OnSizeChanged(object sender, Windows.UI.Core.WindowSizeChangedEventArgs e)
        {
            Bounds = _window.Bounds;
            SizeChanged?.Invoke(this, new WindowSizeChangedEventArgs(e.Size));
        }

        private void OnResizeStarted(CoreWindow sender, object args)
        {
            Logger.Debug(sender.Bounds);
            Bounds = sender.Bounds;

            if (SettingsService.Current.Diagnostics.WindowResizeDebug)
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
            Bounds = sender.Bounds;

            if (SettingsService.Current.Diagnostics.WindowResizeDebug)
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

        public DispatcherContext Dispatcher { get; }
        public NavigationServiceList NavigationServices { get; } = new NavigationServiceList();

        public INavigationService GetNavigationService()
        {
            return GetNavigationService(_window);
        }

        public static INavigationService GetNavigationService(UIElement element)
        {
            var context = ForXamlRoot(element);
            if (context != null)
            {
                return context.GetNavigationService();
            }

            return null;
        }

        public static INavigationService GetNavigationService(XamlRoot xamlRoot)
        {
            var context = ForXamlRoot(xamlRoot);
            if (context != null)
            {
                return context.GetNavigationService();
            }

            return null;
        }

        public static INavigationService GetNavigationService(Window window)
        {
            var content = window.Content;
            if (content is WindowControl contentControl)
            {
                content = contentControl.Content;
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

        #region Screen capture

        private readonly HashSet<int> _screenCaptureDisabled = new();
        private bool _screenCaptureEnabled = true;

        public void DisableScreenCapture(int hash)
        {
            if (Constants.DEBUG)
            {
                return;
            }

            _screenCaptureDisabled.Add(hash);

            if (_screenCaptureDisabled.Count == 1 && _screenCaptureEnabled)
            {
                _screenCaptureEnabled = false;
                ApplicationView.GetForCurrentView().IsScreenCaptureEnabled = false;
            }
        }

        public void EnableScreenCapture(int hash)
        {
            if (Constants.DEBUG)
            {
                return;
            }

            _screenCaptureDisabled.Remove(hash);

            if (_screenCaptureDisabled.Count == 0 && !_screenCaptureEnabled)
            {
                _screenCaptureEnabled = true;
                ApplicationView.GetForCurrentView().IsScreenCaptureEnabled = true;
            }
        }

        #endregion

        #region Lock

        private UIElement _lockedContent;
        private PasscodeWindow _locked;

        public void Lock(bool biometrics)
        {
            if (_locked != null)
            {
                return;
            }

            if (_content?.Content is IPopupHost popupHost)
            {
                popupHost.PopupOpened();
            }

            Logger.Info("Showing passcode lock");

            // TODO: Transition from splash screen to passcode
            _locked = new PasscodeWindow(this, biometrics && IsInMainView);
            _lockedContent = _content?.Content;

            SetContent(_locked);
        }

        public void Unlock()
        {
            if (_locked == null)
            {
                return;
            }

            Logger.Info("Hiding passcode lock");

            SetContent(_lockedContent);

            _locked = null;
            _lockedContent = null;

            if (_content.Content is IPopupHost popupHost)
            {
                popupHost.PopupClosed();
            }

            if (_content.Content is Control control)
            {
                control.Focus(FocusState.Programmatic);
            }
        }

        #endregion

        #region Helper methods

        public string Title
        {
            get => ApplicationView.GetForCurrentView().Title;
            set => ApplicationView.GetForCurrentView().Title = value;
        }

        public Rect Bounds { get; private set; }

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

        public void SetTitleBar(UIElement titleBar, bool collapsed = false)
        {
            _window.SetTitleBar(titleBar);

            if (collapsed)
            {
#if NET9_0_OR_GREATER
                var coreWindow = _window.CoreWindow.As<IInternalCoreWindowPhone>();
                var navigationClient = coreWindow.get_NavigationClient().As<IApplicationWindowTitleBarNavigationClient>();

                navigationClient.set_TitleBarPreferredVisibilityMode(AppWindowTitleBarVisibility.AlwaysHidden);
#else
                var coreWindow = (IInternalCoreWindowPhone)(object)_window.CoreWindow;
                var navigationClient = (IApplicationWindowTitleBarNavigationClient)coreWindow.NavigationClient;

                navigationClient.TitleBarPreferredVisibilityMode = AppWindowTitleBarVisibility.AlwaysHidden;
#endif
            }
        }

        #endregion

        #region Legacy code

        public async void Activate(IActivatedEventArgs args, INavigationService service, AuthorizationState state)
        {
            try
            {
                switch (state)
                {
                    case AuthorizationStateReady:
                        //App.Current.NavigationService.Navigate(typeof(Views.MainPage));
                        Activate(args, service);
                        break;
                    case AuthorizationStateWaitPhoneNumber:
                    case AuthorizationStateWaitOtherDeviceConfirmation:
                        service.Navigate(typeof(AuthorizationPage));
                        break;
                    case AuthorizationStateWaitCode:
                        service.Navigate(typeof(AuthorizationCodePage), navigationStackEnabled: false);
                        break;
                    case AuthorizationStateWaitEmailAddress:
                        service.Navigate(typeof(AuthorizationEmailAddressPage), navigationStackEnabled: false);
                        break;
                    case AuthorizationStateWaitEmailCode:
                        service.Navigate(typeof(AuthorizationEmailCodePage), navigationStackEnabled: false);
                        break;
                    case AuthorizationStateWaitRegistration:
                        service.Navigate(typeof(AuthorizationRegistrationPage), navigationStackEnabled: false);
                        break;
                    case AuthorizationStateWaitPassword waitPassword:
                        if (!string.IsNullOrEmpty(waitPassword.RecoveryEmailAddressPattern))
                        {
                            await service.ShowPopupAsync(string.Format(Strings.RestoreEmailSent, waitPassword.RecoveryEmailAddressPattern), Strings.AppName, Strings.OK);
                        }

                        service.Navigate(typeof(AuthorizationPasswordPage), navigationStackEnabled: false);
                        break;
                }
            }
            catch { }
        }

        private void Activate(IActivatedEventArgs args, INavigationService service)
        {
            service ??= Current.NavigationServices.FirstOrDefault();

            if (service == null || args == null)
            {
                return;
            }

            if (args is ShareTargetActivatedEventArgs share)
            {
            }
            else if (args is ProtocolActivatedEventArgs protocol)
            {
                if (service?.Frame?.Content is MainPage page)
                {
                    page.Activate(protocol.Uri.ToString());
                }
                else
                {
                    service.NavigateToMain(protocol.Uri.ToString());
                }
            }
            else if (args is FileActivatedEventArgs file)
            {
                if (service?.Frame?.Content is MainPage page)
                {
                    //page.Activate(launch);
                }
                else
                {
                    service.NavigateToMain(string.Empty);
                }

                if (file.Files[0] is StorageFile item)
                {
                    // TODO: WinUI - most likely XamlRoot is going to be null at this stage.
                    // As well, Content may be null too.

                    _ = new ThemePreviewPopup(item).ShowQueuedAsync(XamlRoot);
                }
            }
            else if (args is CommandLineActivatedEventArgs commandLine)
            {
                Activate(commandLine.Operation.Arguments, service);
            }
            else if (args is ToastNotificationActivatedEventArgs toastNotificationActivated)
            {
                Activate(toastNotificationActivated.Argument, service);
            }
            else
            {
                var launch = args as LaunchActivatedEventArgs;
                Activate(launch?.Arguments, service);
            }
        }

        private void Activate(string arguments, INavigationService service)
        {
            if (service?.Frame?.Content is MainPage page)
            {
                page.Activate(arguments);
            }
            else
            {
                service.NavigateToMain(arguments);
            }
        }

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
            var theme = SettingsService.Current.Appearance.GetCalculatedApplicationTheme();
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

        #region Static code

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

        public static void ForEach(Action<WindowContext> action)
        {
            lock (_allLock)
            {
                foreach (var window in All)
                {
                    window.Dispatcher.Dispatch(() => action(window));
                }
            }
        }

        public static Task ForEachAsync(Func<WindowContext, Task> action)
        {
            var tasks = new List<Task>();

            lock (_allLock)
            {
                foreach (var window in All)
                {
                    tasks.Add(window.Dispatcher.DispatchAsync(() => action(window)));
                }
            }

            return Task.WhenAll(tasks);
        }

        public static Task ForEachAsync(Action<WindowContext> action)
        {
            var tasks = new List<Task>();

            lock (_allLock)
            {
                foreach (var window in All)
                {
                    tasks.Add(window.Dispatcher.DispatchAsync(() => action(window)));
                }
            }

            return Task.WhenAll(tasks);
        }

        private static readonly Dictionary<XamlRoot, WindowContext> _mapping = new();
        private XamlRoot _xamlRoot;

        public static WindowContext ForXamlRoot(XamlRoot xamlRoot)
        {
            WindowContext context;
            lock (_allLock)
            {
                _mapping.TryGetValue(xamlRoot, out context);
            }

            return context;
        }

        public static WindowContext ForXamlRoot(UIElement element)
        {
            WindowContext context;
            lock (_allLock)
            {
                _mapping.TryGetValue(element.XamlRoot, out context);
            }

            return context;
        }

        private static readonly object _allLock = new();
        public static readonly List<WindowContext> All = new();

        private static readonly object _activeLock = new();
        public static WindowContext Active;

        public static WindowContext Main;

        [ThreadStatic]
        private static WindowContext _current;

        public static WindowContext Current
        {
            get
            {
                if (_current == null)
                {
                    //if (Window.Current != null)
                    //{
                    //    _current = new WindowContext(Window.Current);
                    //}

                    Logger.Info(Environment.StackTrace);
                }

                return _current;
            }
        }

        #endregion
    }
}
