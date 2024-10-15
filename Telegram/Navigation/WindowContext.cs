//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Chats;
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
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Navigation
{
    public partial class WindowContext
    {
        private readonly ILifetimeService _lifetime;

        private readonly Window _window;

        private bool _consolidated;

        private readonly InputListener _inputListener;
        public InputListener InputListener => _inputListener;

        public CoreWindow CoreWindow => _window.CoreWindow;

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

            Current = this;
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

            _lifetime = TypeResolver.Current.Lifetime;
            _inputListener = new InputListener(window);

            window.Activated += OnActivated;
            window.VisibilityChanged += OnVisibilityChanged;
            window.SizeChanged += OnSizeChanged;
            window.Closed += OnClosed;
            window.CoreWindow.ResizeStarted += OnResizeStarted;
            window.CoreWindow.ResizeCompleted += OnResizeCompleted;

            window.CoreWindow.DispatcherQueue.ShutdownCompleted += OnShutdownCompleted;

            #region Legacy code

            ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size(320, 500));
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;

            UpdateTitleBar();

            #endregion

            if (TypeResolver.Current.Passcode.IsLockscreenRequired)
            {
                Lock(true);
            }

            ApplicationView.GetForCurrentView().VisibleBoundsChanged += OnVisibleBoundsChanged;
            ApplicationView.GetForCurrentView().Consolidated += OnConsolidated;
        }

        public void Activate()
        {
            _window.Activate();
        }

        private void OnVisibleBoundsChanged(ApplicationView sender, object args)
        {
            Logger.Debug(sender.VisibleBounds);
        }

        private void OnShutdownCompleted(Windows.System.DispatcherQueue sender, object args)
        {
            sender.ShutdownCompleted -= OnShutdownCompleted;
            Current = null;

            Theme.Current = null;

            ThemeIncoming.Release();
            ThemeOutgoing.Release();

            AnimatedImageLoader.Release();
            ChatRecordButton.Recorder.Release();

            // TODO: needed? From some tests, this prevented the whole Window root from being garbage collected
            if (SynchronizationContext.Current is SecondaryViewSynchronizationContextDecorator decorator)
            {
                SynchronizationContext.SetSynchronizationContext(decorator.Context);
            }
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

            OnConsolidated(sender);
        }

        private void OnConsolidated(ApplicationView sender, ApplicationViewConsolidatedEventArgs args)
        {
            OnConsolidated(sender);
        }

        private void OnConsolidated(ApplicationView sender)
        {
            _consolidated = true;
            _inputListener.Release();
            sender.Consolidated -= OnConsolidated;

            // TODO: since we can't call Close directly,
            // Closed event will be never fired.
            //OnClosed(null, null);
            ClearTitleBar(sender);
        }

        private void OnClosed(object sender, CoreWindowEventArgs e)
        {
            lock (_allLock)
            {
                All.Remove(this);
            }

            NavigationServices.ForEach(x => x.Suspend());
            NavigationServices.Clear();

            _window.Activated -= OnActivated;
            _window.VisibilityChanged -= OnVisibilityChanged;
            _window.SizeChanged -= OnSizeChanged;
            _window.Closed -= OnClosed;
            _window.CoreWindow.ResizeStarted -= OnResizeStarted;
            _window.CoreWindow.ResizeCompleted -= OnResizeCompleted;
        }

        public bool IsInMainView { get; }

        public bool IsCallInProgress { get; private set; }

        public UIElement Content
        {
            get => _window.Content;
            set
            {
                _window.Content = value;

                if (value != null)
                {
                    IsCallInProgress = value is VoipPage or GroupCallPage or LiveStreamPage;

                    if (_locked != null)
                    {
                        value.Visibility = Visibility.Collapsed;
                    }

                    if (value is FrameworkElement element)
                    {
                        element.Loading += OnLoading;
                        element.Loaded += OnLoaded;
                    }
                }
            }
        }

        private void OnLoading(FrameworkElement sender, object args)
        {
            sender.Loading -= OnLoading;

            lock (_allLock)
            {
                _mapping[sender.UIContext] = this;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                element.Loaded -= OnLoaded;
            }

            ViewService.OnWindowLoaded();
        }

        public ElementTheme ActualTheme => _window.Content is FrameworkElement element
            ? element.ActualTheme
            : SettingsService.Current.Appearance.GetCalculatedElementTheme();

        public ElementTheme RequestedTheme
        {
            get => _window.Content is FrameworkElement element
                ? element.RequestedTheme
                : ElementTheme.Default;
            set
            {
                if (_window.Content is FrameworkElement element)
                {
                    element.RequestedTheme = value;
                }
            }
        }

        public double RasterizationScale => _window.Content?.XamlRoot?.RasterizationScale ?? 1;

        public CoreWindowActivationMode ActivationMode => _window.CoreWindow.ActivationMode;

        public event EventHandler<WindowActivatedEventArgs> Activated;

        private void OnActivated(object sender, WindowActivatedEventArgs e)
        {
            Activated?.Invoke(sender, e);

            lock (_activeLock)
            {
                if (e.WindowActivationState != CoreWindowActivationState.Deactivated)
                {
                    Active = this;
                }
                else if (Active == this)
                {
                    Active = null;
                }
            }
        }

        public event EventHandler<VisibilityChangedEventArgs> VisibilityChanged;

        private void OnVisibilityChanged(object sender, VisibilityChangedEventArgs e)
        {
            VisibilityChanged?.Invoke(sender, e);
        }

        public event EventHandler<WindowSizeChangedEventArgs> SizeChanged;

        private void OnSizeChanged(object sender, WindowSizeChangedEventArgs e)
        {
            Bounds = _window.Bounds;
            SizeChanged?.Invoke(sender, e);
        }

        private void OnResizeStarted(CoreWindow sender, object args)
        {
            Logger.Debug(sender.Bounds);
            Bounds = sender.Bounds;

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
            if (window.Content is RootPage rootPage && rootPage.NavigationService != null)
            {
                return rootPage.NavigationService;
            }
            else if (window.Content is StandalonePage standalonePage && standalonePage.NavigationService != null)
            {
                return standalonePage.NavigationService;
            }
            else if (window.Content is Page { DataContext: ViewModelBase viewModel })
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
            _screenCaptureDisabled.Add(hash);

            if (_screenCaptureDisabled.Count == 1 && _screenCaptureEnabled)
            {
                _screenCaptureEnabled = false;
                ApplicationView.GetForCurrentView().IsScreenCaptureEnabled = false;
            }
        }

        public void EnableScreenCapture(int hash)
        {
            _screenCaptureDisabled.Remove(hash);

            if (_screenCaptureDisabled.Count == 0 && !_screenCaptureEnabled)
            {
                _screenCaptureEnabled = true;
                ApplicationView.GetForCurrentView().IsScreenCaptureEnabled = true;
            }
        }

        #endregion

        #region Lock

        private PasscodePage _locked;

        public async void Lock(bool biometrics)
        {
            if (_locked != null)
            {
                return;
            }

            if (_window.Content != null)
            {
                _window.Content.Visibility = Visibility.Collapsed;
            }

            _locked = new PasscodePage(biometrics && IsInMainView);

            void handler(ContentDialog s, ContentDialogClosingEventArgs args)
            {
                s.Closing -= handler;

                if (_window.Content != null)
                {
                    _window.Content.Visibility = Visibility.Visible;
                }
            }

            // TODO: WinUI - most likely XamlRoot is going to be null at this stage.
            // As well, Content may be null too.

            _locked.Closing += handler;
            await _locked.ShowQueuedAsync(Content?.XamlRoot);

            _locked = null;
        }

        public void Unlock()
        {
            _locked?.Update();
        }

        #endregion

        #region Helper methods

        public string Title
        {
            get => ApplicationView.GetForCurrentView().Title;
            set => ApplicationView.GetForCurrentView().Title = value;
        }

        public Rect Bounds { get; private set; }

        public void SetTitleBar(UIElement element)
        {
            _window.SetTitleBar(element);
        }

        public bool IsFullScreenMode => ApplicationView.GetForCurrentView().IsFullScreenMode;

        public void ExitFullScreenMode()
        {
            ApplicationView.GetForCurrentView().ExitFullScreenMode();
        }

        public void TryEnterFullScreenMode()
        {
            ApplicationView.GetForCurrentView().TryEnterFullScreenMode();
        }

        #endregion

        #region Legacy code

        public async void Activate(IActivatedEventArgs args, INavigationService service, AuthorizationState state)
        {
            try
            {
                if (args is ShareTargetActivatedEventArgs shareTarget && state is not AuthorizationStateReady)
                {
                    var options = new Windows.System.LauncherOptions();
                    options.TargetApplicationPackageFamilyName = Package.Current.Id.FamilyName;

                    try
                    {
                        await Windows.System.Launcher.LaunchUriAsync(new Uri("tg:"), options);
                    }
                    catch
                    {
                        // It's too early?
                    }
                    finally
                    {
                        shareTarget.ShareOperation.ReportCompleted();
                    }

                    return;
                }

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

        private async void Activate(IActivatedEventArgs args, INavigationService service)
        {
            service ??= Current.NavigationServices.FirstOrDefault();

            if (service == null || args == null)
            {
                return;
            }

            if (args is ShareTargetActivatedEventArgs share)
            {
                WatchDog.TrackEvent("ShareTarget");
                var package = new DataPackage();

                try
                {
                    var operation = share.ShareOperation.Data;
                    if (operation.AvailableFormats.Contains(StandardDataFormats.ApplicationLink))
                    {
                        package.SetApplicationLink(await operation.GetApplicationLinkAsync());
                    }
                    if (operation.AvailableFormats.Contains(StandardDataFormats.Bitmap))
                    {
                        package.SetBitmap(await operation.GetBitmapAsync());
                    }
                    //if (operation.Contains(StandardDataFormats.Html))
                    //{
                    //    package.SetHtmlFormat(await operation.GetHtmlFormatAsync());
                    //}
                    //if (operation.Contains(StandardDataFormats.Rtf))
                    //{
                    //    package.SetRtf(await operation.GetRtfAsync());
                    //}
                    if (operation.AvailableFormats.Contains(StandardDataFormats.StorageItems))
                    {
                        package.SetStorageItems(await operation.GetStorageItemsAsync());
                    }
                    if (operation.AvailableFormats.Contains(StandardDataFormats.Text))
                    {
                        package.SetText(await operation.GetTextAsync());
                    }
                    //if (operation.Contains(StandardDataFormats.Uri))
                    //{
                    //    package.SetUri(await operation.GetUriAsync());
                    //}
                    if (operation.AvailableFormats.Contains(StandardDataFormats.WebLink))
                    {
                        package.SetWebLink(await operation.GetWebLinkAsync());
                    }
                }
                catch { }

                var query = "tg://";
                var chatId = 0L;

                try
                {
                    var contactId = await ContactsService.GetContactIdAsync(share.ShareOperation.Contacts.FirstOrDefault());
                    if (contactId is long userId)
                    {
                        var response = await _lifetime.ActiveItem.ClientService.SendAsync(new CreatePrivateChat(userId, false));
                        if (response is Chat chat)
                        {
                            query = $"ms-contact-profile://meh?ContactRemoteIds=u" + userId;
                            chatId = chat.Id;
                        }
                    }
                }
                catch
                {
                    // ShareOperation.Contacts can throw an InvalidCastException
                }

                App.DataPackages[chatId] = package.GetView();

                App.ShareOperation = share.ShareOperation;
                App.ShareWindow = _window;

                var options = new Windows.System.LauncherOptions();
                options.TargetApplicationPackageFamilyName = Package.Current.Id.FamilyName;

                try
                {
                    await Windows.System.Launcher.LaunchUriAsync(new Uri(query), options);
                }
                catch
                {
                    // It's too early?
                }
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

                if (App.ShareOperation != null)
                {
                    try
                    {
                        App.ShareOperation.ReportCompleted();
                        App.ShareOperation = null;
                    }
                    catch { }
                }

                if (App.ShareWindow != null)
                {
                    try
                    {
                        await App.ShareWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            App.ShareWindow.Close();
                            App.ShareWindow = null;
                        });
                    }
                    catch { }
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

                    await new ThemePreviewPopup(item).ShowQueuedAsync(Content?.XamlRoot);
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
            else if (theme == ApplicationTheme.Light)
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

        public static bool IsKeyDown(Windows.System.VirtualKey key)
        {
            //return (InputKeyboardSource.GetKeyStateForCurrentThread(key) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
            return (Window.Current.CoreWindow.GetKeyState(key) & CoreVirtualKeyStates.Down) != 0;
        }

        public static bool IsKeyDownAsync(Windows.System.VirtualKey key)
        {
            //return (InputKeyboardSource.GetKeyStateForCurrentThread(key) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
            return (Window.Current.CoreWindow.GetAsyncKeyState(key) & CoreVirtualKeyStates.Down) != 0;
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

        private static readonly Dictionary<UIContext, WindowContext> _mapping = new();

        public static WindowContext ForXamlRoot(XamlRoot xamlRoot)
        {
            WindowContext context;
            lock (_allLock)
            {
                _mapping.TryGetValue(xamlRoot.UIContext, out context);
            }

            return context;
        }

        public static WindowContext ForXamlRoot(UIElement element)
        {
            WindowContext context;
            lock (_allLock)
            {
                _mapping.TryGetValue(element.UIContext, out context);
            }

            return context;
        }

        private static readonly object _allLock = new();
        public static readonly List<WindowContext> All = new();

        private static readonly object _activeLock = new();
        public static WindowContext Active;

        public static WindowContext Main;

        [ThreadStatic]
        public static WindowContext Current;

        #endregion
    }
}
