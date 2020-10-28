using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Navigation.Services;
using Unigram.Services.ViewService;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.ExtendedExecution;
using Windows.System.Profile;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Unigram.Navigation
{
    public abstract class BootStrapper : Application, INotifyPropertyChanged
    {
        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected void Set<T>(ref T storage, T value, [CallerMemberName] String propertyName = null)
        {
            if (!object.Equals(storage, value))
            {
                storage = value;
                RaisePropertyChanged(propertyName);
            }
        }

        protected void RaisePropertyChanged([CallerMemberName] String propertyName = null) =>
           PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        #endregion

        /// <summary>
        /// If a developer overrides this method, the developer can resolve DataContext or unwrap DataContext 
        /// available for the Page object when using a MVVM pattern that relies on a wrapped/porxy around ViewModels
        /// </summary>
        public virtual INavigable ResolveForPage(Page page, INavigationService navigationService) => null;

        public static new BootStrapper Current { get; private set; }

        public IDictionary<string, object> SessionState { get; set; } = new Dictionary<string, object>();

        internal volatile bool IsMainWindowCreated;

        private UISettings _uiSettings;
        public UISettings UISettings => _uiSettings ??= new UISettings();

        #region Debug

        [Conditional("DEBUG")]
        static void DebugWrite(string text = null, Unigram.Services.Logging.Severities severity = Unigram.Services.Logging.Severities.Template10, [CallerMemberName] string caller = null) =>
            Unigram.Services.Logging.LoggingService.WriteLine(text, severity, caller: $"BootStrapper.{caller}");

        #endregion

        public BootStrapper()
        {
            DebugWrite("base.Constructor");

            Current = this;
            Resuming += CallResuming;
            Suspending += CallHandleSuspendingAsync;
        }

        private void Loaded()
        {
            DebugWrite();

            // Hook up keyboard and mouse Back handler
            var keyboard = Unigram.Services.Keyboard.KeyboardService.GetForCurrentView();
            keyboard.AfterBackGesture = (key) =>
            {
                DebugWrite(caller: nameof(keyboard.AfterBackGesture));

                var handled = false;
                RaiseBackRequested(key, ref handled);
            };

            keyboard.AfterForwardGesture = () =>
            {
                DebugWrite(caller: nameof(keyboard.AfterForwardGesture));

                RaiseForwardRequested();
            };

            // Hook up the default Back handler
            SystemNavigationManager.GetForCurrentView().BackRequested += BackHandler;
        }

        protected override void OnWindowCreated(WindowCreatedEventArgs args)
        {
            DebugWrite();

            IsMainWindowCreated = true;
            //should be called to initialize and set new SynchronizationContext
            //if (!WindowWrapper.ActiveWrappers.Any())
            // handle window
            var window = CreateWindowWrapper(args.Window);
            Loaded();
            ViewService.OnWindowCreated();
            base.OnWindowCreated(args);
        }

        protected virtual WindowContext CreateWindowWrapper(Window window)
        {
            return new WindowContext(window);
        }

        #region properties

        public INavigationService NavigationService => WindowContext.GetForCurrentView().NavigationServices.FirstOrDefault();

        /// <summary>
        /// CacheMaxDuration indicates the maximum TimeSpan for which cache data
        /// will be preserved. If Template 10 determines cache data is older than
        /// the specified MaxDuration it will automatically be cleared during start.
        /// </summary>
        public TimeSpan CacheMaxDuration { get; set; } = TimeSpan.MaxValue;
        private const string CacheDateKey = "Setting-Cache-Date";

        /// <summary>
        /// ShowShellBackButton is used to show or hide the shell-drawn back button that
        /// is new to Windows 10. A developer can do this manually, but using this property
        /// is important during navigation because Template 10 manages the visibility
        /// of the shell-drawn back button at that time.
        /// </summary>
        public bool ShowShellBackButton { get; set; } = true;

        public bool ForceShowShellBackButton { get; set; } = false;

        #endregion

        #region activated

        // it is the intent of Template 10 to no longer require Launched/Activated overrides, only OnStartAsync()

        protected override sealed void OnActivated(IActivatedEventArgs e) { DebugWrite(); CallInternalActivatedAsync(e); }
        protected override sealed void OnCachedFileUpdaterActivated(CachedFileUpdaterActivatedEventArgs args) { DebugWrite(); CallInternalActivatedAsync(args); }
        protected override sealed void OnFileActivated(FileActivatedEventArgs args) { DebugWrite(); CallInternalActivatedAsync(args); }
        protected override sealed void OnFileOpenPickerActivated(FileOpenPickerActivatedEventArgs args) { DebugWrite(); CallInternalActivatedAsync(args); }
        protected override sealed void OnFileSavePickerActivated(FileSavePickerActivatedEventArgs args) { DebugWrite(); CallInternalActivatedAsync(args); }
        protected override sealed void OnSearchActivated(SearchActivatedEventArgs args) { DebugWrite(); CallInternalActivatedAsync(args); }
        protected override sealed void OnShareTargetActivated(ShareTargetActivatedEventArgs args) { DebugWrite(); CallInternalActivatedAsync(args); }

        public IActivatedEventArgs OriginalActivatedArgs { get; private set; }
        public bool PrelaunchActivated { get; private set; }

        private async void CallInternalActivatedAsync(IActivatedEventArgs e)
        {
            CurrentState = States.BeforeActivate;
            await InternalActivatedAsync(e);
            CurrentState = States.AfterActivate;
        }

        /// <summary>
        /// This handles all the prelimimary stuff unique to Activated before calling OnStartAsync()
        /// This is private because it is a specialized prelude to OnStartAsync().
        /// OnStartAsync will not be called if state restore is determined.
        /// </summary>
        private async Task InternalActivatedAsync(IActivatedEventArgs e)
        {
            DebugWrite();

            OriginalActivatedArgs = e;

            // sometimes activate requires a frame to be built
            if (Window.Current.Content == null)
            {
                DebugWrite("Calling", caller: nameof(InternalActivatedAsync));
                await InitializeFrameAsync(e);
            }

            // onstart is shared with activate and launch
            await CallOnStartAsync(e, true, StartKind.Activate);

            // ensure active (this will hide any custom splashscreen)
            CallActivateWindow(WindowLogic.ActivateWindowSources.Activating);
        }

        #endregion

        #region launch

        // it is the intent of Template 10 to no longer require Launched/Activated overrides, only OnStartAsync()

        protected sealed override void OnLaunched(LaunchActivatedEventArgs e)
        {
            DebugWrite();
            CallInternalLaunchAsync(e);
        }

        async void CallInternalLaunchAsync(LaunchActivatedEventArgs e)
        {
            CurrentState = States.BeforeLaunch;
            await InternalLaunchAsync(e);
            CurrentState = States.AfterLaunch;
        }

        /// <summary>
        /// This handles all the preliminary stuff unique to Launched before calling OnStartAsync().
        /// This is private because it is a specialized prelude to OnStartAsync().
        /// OnStartAsync will not be called if state restore is determined
        /// </summary>
        private async Task InternalLaunchAsync(LaunchActivatedEventArgs e)
        {
            DebugWrite($"Previous:{e.PreviousExecutionState}");

            OriginalActivatedArgs = e;
            PrelaunchActivated = e.PrelaunchActivated;

            if (e.PreviousExecutionState != ApplicationExecutionState.Running || Window.Current.Content == null)
            {
                try
                {
                    await InitializeFrameAsync(e);
                }
                catch (Exception)
                {
                    // nothing
                }
            }

            // okay, now handle launch
            bool restored = false;
            switch (e.PreviousExecutionState)
            {
                case ApplicationExecutionState.Suspended:
                case ApplicationExecutionState.Terminated:
                    {
                        OnResuming(this, null, AppExecutionState.Terminated);

                        /*
                            Restore state if you need to/can do.
                            Remember that only the primary tile or when user has
                            switched to the app (for instance via the task switcher)
                            should restore. (this includes toast with no data payload)
                            The rest are already providing a nav path.

                            In the event that the cache has expired, attempting to restore
                            from state will fail because of missing values. 
                            This is okay & by design.
                        */


                        restored = await CallAutoRestoreAsync(e, restored);
                        break;
                    }
                case ApplicationExecutionState.ClosedByUser:
                case ApplicationExecutionState.NotRunning:
                default:
                    break;
            }

            // handle pre-launch
            if (e.PrelaunchActivated)
            {
                var runOnStartAsync = false;
                _HasOnPrelaunchAsync = true;
                await OnPrelaunchAsync(e, out runOnStartAsync);
                if (!runOnStartAsync)
                    return;
            }

            if (!restored)
            {
                var kind = e.PreviousExecutionState == ApplicationExecutionState.Running ? StartKind.Activate : StartKind.Launch;
                await CallOnStartAsync(e, true, kind);
            }

            CallActivateWindow(WindowLogic.ActivateWindowSources.Launching);
        }

        private void BackHandler(object sender, BackRequestedEventArgs args)
        {
            DebugWrite();

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
            var handled = NavigationService?.CanGoBack == false;

            RaiseBackRequested(Windows.System.VirtualKey.GoBack, ref handled);
            args.Handled = handled;
        }

        #endregion

        public void RaiseBackRequested()
        {
            var handled = false;
            RaiseBackRequested(Windows.System.VirtualKey.GoBack, ref handled);
        }

        /// <summary>
        /// Default Hardware/Shell Back handler overrides standard Back behavior 
        /// that navigates to previous app in the app stack to instead cause a backward page navigation.
        /// Views or Viewodels can override this behavior by handling the BackRequested 
        /// event and setting the Handled property of the BackRequestedEventArgs to true.
        /// </summary>
        private void RaiseBackRequested(Windows.System.VirtualKey key, ref bool handled)
        {
            DebugWrite();

            var args = new HandledEventArgs();
            BackRequested?.Invoke(null, args);
            if (handled = args.Handled)
                return;

            var popups = VisualTreeHelper.GetOpenPopups(Window.Current);
            foreach (var popup in popups)
            {
                if (popup.Child is INavigablePage page)
                {
                    page.OnBackRequested(args);

                    if (handled = args.Handled)
                        return;
                }
                else if (popup.Child is ContentDialog dialog)
                {
                    dialog.Hide();
                    return;
                }
                else if (key == Windows.System.VirtualKey.Escape)
                {
                    handled = args.Handled = true;
                    return;
                }
            }

            foreach (var frame in WindowContext.GetForCurrentView().NavigationServices.Select(x => x.FrameFacade).Reverse())
            {
                frame.RaiseBackRequested(args);

                if (handled = args.Handled)
                    return;
            }

            if (NavigationService?.CanGoBack ?? false)
            {
                NavigationService?.GoBack();
            }
        }

        // this event precedes the in-frame event by the same name
        public static event EventHandler<HandledEventArgs> BackRequested;

        private void RaiseForwardRequested()
        {
            DebugWrite();

            var args = new HandledEventArgs();
            ForwardRequested?.Invoke(null, args);
            if (args.Handled)
                return;
            foreach (var frame in WindowContext.GetForCurrentView().NavigationServices.Select(x => x.FrameFacade))
            {
                frame.RaiseForwardRequested(args);
                if (args.Handled)
                    return;
            }

            NavigationService?.GoForward();
        }

        public void UpdateShellBackButton()
        {
            DebugWrite();

            // show the shell back only if there is anywhere to go in the default frame
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                (ShowShellBackButton && (NavigationService.CanGoBack || ForceShowShellBackButton))
                    ? AppViewBackButtonVisibility.Visible
                    : AppViewBackButtonVisibility.Collapsed;
            ShellBackButtonUpdated?.Invoke(this, EventArgs.Empty);
        }
        public event EventHandler ShellBackButtonUpdated;

        // this event precedes the in-frame event by the same name
        public static event EventHandler<HandledEventArgs> ForwardRequested;

        #region overrides

        public enum StartKind { Launch, Activate }

        /// <summary>
        /// Prelaunch may never occur. However, it's possible that it will. It is a Windows mechanism
        /// to launch apps in the background and quickly suspend them. Because of this, developers need to
        /// handle Prelaunch scenarios if their typical launch is expensive or requires user interaction.
        /// </summary>
        /// <param name="args">IActivatedEventArgs from startup</param>
        /// <param name="runOnStartAsync">A developer can force the typical startup pipeline. Default should be false.</param>
        /// <remarks>
        /// For Prelaunch Template 10 does not continue the typical startup pipeline by default. 
        /// OnActivated will occur if the application has been prelaunched.
        /// </remarks>
        public virtual Task OnPrelaunchAsync(IActivatedEventArgs args, out bool runOnStartAsync)
        {
            DebugWrite("Virtual");

            runOnStartAsync = false;
            return Task.CompletedTask;
        }

        /// <summary>
        /// This indicates if OnPreLaunch has EVER been called.
        /// </summary>
        bool _HasOnPrelaunchAsync = false;

        /// <summary>
        /// OnStartAsync is the one-stop-show override to handle when your app starts
        /// Template 10 will not call OnStartAsync if the app is restored from state.
        /// An app restores from state when the app was suspended and then terminated (PreviousExecutionState terminated).
        /// </summary>
        public abstract Task OnStartAsync(StartKind startKind, IActivatedEventArgs args);

        /// <summary>
        /// OnInitializeAsync is where your app will do must-have up-front operations
        /// OnInitializeAsync will be called even if the application is restoring from state.
        /// An app restores from state when the app was suspended and then terminated (PreviousExecutionState terminated).
        /// </summary>
        public virtual Task OnInitializeAsync(IActivatedEventArgs args)
        {
            DebugWrite($"Virtual {nameof(IActivatedEventArgs)}:{args.Kind}");

            return Task.CompletedTask;
        }

        /// <summary>
        /// OnSuspendingAsync will be called when the application is suspending, but this override
        /// should only be used by applications that have application-level operations that must
        /// be completed during suspension. 
        /// Using OnSuspendingAsync is a little better than handling the Suspending event manually
        /// because the asunc operations are in a single, global deferral created when the suspension
        /// begins and completed automatically when the last viewmodel has been called (including this method).
        /// </summary>
        public virtual Task OnSuspendingAsync(object s, SuspendingEventArgs e, bool prelaunchActivated)
        {
            DebugWrite($"Virtual {nameof(SuspendingEventArgs)}:{e.SuspendingOperation} {nameof(prelaunchActivated)}:{prelaunchActivated}");

            return Task.CompletedTask;
        }

        public enum AppExecutionState { Suspended, Terminated, Prelaunch }

        /// <summary>
        /// The application is returning from a suspend state of some kind.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="e"></param>
        /// <param name="previousExecutionState"></param>
        /// <remarks>
        /// previousExecutionState can be Terminated, which typically does not raise OnResume.
        /// This is important because the resume model changes a little in Mobile.
        /// </remarks>
        public virtual void OnResuming(object s, object e, AppExecutionState previousExecutionState)
        {
            DebugWrite($"Virtual, {nameof(previousExecutionState)}:{previousExecutionState}");
        }

        #endregion

        #region pipeline

        /// <summary>
        /// Creates a new Frame and adds the resulting NavigationService to the 
        /// WindowWrapper collection. In addition, it optionally will setup the 
        /// shell back button to react to the nav of the Frame.
        /// A developer should call this when creating a new/secondary frame.
        /// The shell back button should only be setup one time.
        /// </summary>
        public INavigationService NavigationServiceFactory(BackButton backButton, ExistingContent existingContent, int session, string id, bool root)
        {
            DebugWrite($"{nameof(backButton)}:{backButton} {nameof(ExistingContent)}:{existingContent}");

            return NavigationServiceFactory(backButton, existingContent, new Frame(), session, id, root);
        }

        /// <summary>
        /// Creates the NavigationService instance for given Frame.
        /// </summary>
        protected virtual INavigationService CreateNavigationService(Frame frame, int session, string id, bool root)
        {
            DebugWrite($"Frame:{frame}");

            return new NavigationService(frame, session, id);
        }

        /// <summary>
        /// Creates a new NavigationService from the gived Frame to the 
        /// WindowWrapper collection. In addition, it optionally will setup the 
        /// shell back button to react to the nav of the Frame.
        /// A developer should call this when creating a new/secondary frame.
        /// The shell back button should only be setup one time.
        /// </summary>
        public INavigationService NavigationServiceFactory(BackButton backButton, ExistingContent existingContent, Frame frame, int session, string id, bool root)
        {
            DebugWrite($"{nameof(backButton)}:{backButton} {nameof(existingContent)}:{existingContent} {nameof(frame)}:{frame}");

            frame.Content = (existingContent == ExistingContent.Include) ? Window.Current.Content : null;

            // if the service already exists for this frame, use the existing one.
            foreach (var nav in WindowContext.ActiveWrappers.SelectMany(x => x.NavigationServices))
            {
                if (nav.FrameFacade.Frame.Equals(frame))
                    return nav as INavigationService;
            }

            var navigationService = CreateNavigationService(frame, session, id, root);
            navigationService.FrameFacade.BackButtonHandling = backButton;
            WindowContext.GetForCurrentView().NavigationServices.Add(navigationService);

            if (backButton == BackButton.Attach)
            {
                // TODO: unattach others

                // update shell back when backstack changes
                // only the default frame in this case because secondary should not dismiss the app
                //frame.RegisterPropertyChangedCallback(Frame.BackStackDepthProperty, (s, args) => UpdateShellBackButton());

                // update shell back when navigation occurs
                // only the default frame in this case because secondary should not dismiss the app
                //frame.Navigated += (s, args) => UpdateShellBackButton();
            }

            // this is always okay to check, default or not
            // expire any state (based on expiry)
            DateTime cacheDate;
            // default the cache age to very fresh if not known
            var otherwise = DateTime.MinValue.ToString();
            if (DateTime.TryParse(navigationService.FrameFacade.GetFrameState(CacheDateKey, otherwise), out cacheDate))
            {
                var cacheAge = DateTime.Now.Subtract(cacheDate);
                if (cacheAge >= CacheMaxDuration)
                {
                    // clear state in every nav service in every view
                    foreach (var nav in WindowContext.ActiveWrappers.SelectMany(x => x.NavigationServices))
                    {
                        nav.FrameFacade.ClearFrameState();
                    }
                }
            }
            else
            {
                // no date, that's okay
            }

            return navigationService;
        }

        public enum States
        {
            None,
            Running,
            BeforeInit,
            AfterInit,
            BeforeLaunch,
            AfterLaunch,
            BeforeActivate,
            AfterActivate,
            BeforeStart,
            AfterStart,
        }
        private States _currentState = States.None;
        public States CurrentState
        {
            get { return _currentState; }
            set
            {
                DebugWrite($"CurrenstState changed to {value}");
                CurrentStateHistory.Add($"{DateTime.Now}-{Guid.NewGuid()}", value);
                _currentState = value;
            }
        }
        Dictionary<string, States> CurrentStateHistory = new Dictionary<string, States>();

        private async Task InitializeFrameAsync(IActivatedEventArgs e)
        {
            /*
                InitializeFrameAsync creates a default Frame preceeded by the optional 
                splash screen, then OnInitialzieAsync, then the new frame (if necessary).
                This is private because there's no reason for the developer to call this.
            */

            DebugWrite($"{nameof(IActivatedEventArgs)}:{e.Kind}");

            await CallOnInitializeAsync(false, e);

            if (Window.Current.Content == null)
            {
                Window.Current.Content = CreateRootElement(e);
            }
            else
            {
                // if there's custom content then do nothing
            }
        }

        #endregion

        WindowLogic _WindowLogic = new WindowLogic();
        private void CallActivateWindow(WindowLogic.ActivateWindowSources source)
        {
            _WindowLogic.ActivateWindow(source);
            CurrentState = States.Running;
        }

        #region Workers

        /// <summary>
        ///  By default, Template 10 will setup the root element to be a Template 10
        ///  Modal Dialog control. If you desire something different, you can set it here.
        /// </summary>
        public abstract UIElement CreateRootElement(IActivatedEventArgs e);

        public abstract UIElement CreateRootElement(INavigationService navigationService);

        private async Task CallOnInitializeAsync(bool canRepeat, IActivatedEventArgs e)
        {
            DebugWrite();

            if (!canRepeat && CurrentStateHistory.ContainsValue(States.BeforeInit))
                return;

            CurrentState = States.BeforeInit;
            await OnInitializeAsync(e);
            CurrentState = States.AfterInit;
        }

        private async Task CallOnStartAsync(IActivatedEventArgs args, bool canRepeat, StartKind startKind)
        {
            DebugWrite();

            if (!canRepeat && CurrentStateHistory.ContainsValue(States.BeforeStart))
                return;

            CurrentState = States.BeforeStart;
            while (!CurrentStateHistory.ContainsValue(States.AfterInit))
            {
                // this could happen if app is activated before previous init completes
                await Task.Delay(500);
            }
            await OnStartAsync(startKind, OriginalActivatedArgs);
            CurrentState = States.AfterStart;
        }

        #endregion

        #region lifecycle logic

        public bool AutoRestoreAfterTerminated { get; set; } = true;
        public bool AutoExtendExecutionSession { get; set; } = true;
        public bool AutoSuspendAllFrames { get; set; } = true;
        LifecycleLogic _LifecycleLogic = new LifecycleLogic();

        private async void CallResuming(object sender, object e)
        {
            DebugWrite(caller: nameof(Resuming));

            try
            {
                var args = OriginalActivatedArgs as LaunchActivatedEventArgs;
                if (args?.PrelaunchActivated ?? true)
                {
                    OnResuming(sender, e, AppExecutionState.Prelaunch);
                    var kind = args?.PreviousExecutionState == ApplicationExecutionState.Running ? StartKind.Activate : StartKind.Launch;
                    await CallOnStartAsync(args, false, kind);
                    CallActivateWindow(WindowLogic.ActivateWindowSources.Resuming);
                }
                else
                {
                    OnResuming(sender, e, AppExecutionState.Suspended);

                    //var services = WindowContext.ActiveWrappers.SelectMany(x => x.NavigationServices).Where(x => x.IsInMainView);
                    //foreach (INavigationService nav in services)
                    //{
                    //    try
                    //    {
                    //        // call view model suspend (OnNavigatedfrom)
                    //        // date the cache (which marks the date/time it was suspended)
                    //        DebugWrite($"Nav.FrameId:{nav.FrameFacade.FrameId}");
                    //        await (nav as INavigationService).GetDispatcherWrapper().DispatchAsync(() => nav.Resuming());
                    //    }
                    //    catch (Exception ex)
                    //    {
                    //        DebugWrite($"FrameId: [{nav.FrameFacade.FrameId}] {ex} {ex.Message}", caller: nameof(Resuming));
                    //    }
                    //}
                }
            }
            catch { }
        }

        private async Task<bool> CallAutoRestoreAsync(ILaunchActivatedEventArgs e, bool restored)
        {
            if (!AutoRestoreAfterTerminated)
                return false;
            return await _LifecycleLogic.AutoRestoreAsync(e, NavigationService);
        }

        async void CallHandleSuspendingAsync(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            try
            {
                if (AutoSuspendAllFrames)
                {
                    await _LifecycleLogic.AutoSuspendAllFramesAsync(sender, e, AutoExtendExecutionSession);
                }
                await OnSuspendingAsync(sender, e, PrelaunchActivated);
            }
            finally
            {
                deferral.Complete();
            }
        }

        #endregion

        public enum BackButton { Attach, Ignore }

        public enum ExistingContent { Include, Exclude }

        public const string DefaultTileID = "App";

        public enum AdditionalKinds { Primary, Toast, SecondaryTile, Other, JumpListItem }

        /// <summary>
        /// This determines the simplest case for starting. This should handle 80% of common scenarios. 
        /// When Other is returned the developer must determine start manually using IActivatedEventArgs.Kind
        /// </summary>
        public static AdditionalKinds DetermineStartCause(IActivatedEventArgs args)
        {
            DebugWrite($"{nameof(IActivatedEventArgs)}:{args.Kind}");

            if (args is ToastNotificationActivatedEventArgs)
            {
                return AdditionalKinds.Toast;
            }
            var e = args as ILaunchActivatedEventArgs;
            if (e?.TileId == DefaultTileID && string.IsNullOrEmpty(e?.Arguments))
            {
                return AdditionalKinds.Primary;
            }
            else if (e?.TileId == DefaultTileID && !string.IsNullOrEmpty(e?.Arguments))
            {
                return AdditionalKinds.JumpListItem;
            }
            else if (!string.IsNullOrEmpty(e?.TileId) && e?.TileId != DefaultTileID)
            {
                return AdditionalKinds.SecondaryTile;
            }
            else
            {
                return AdditionalKinds.Other;
            }
        }

        public class LifecycleLogic
        {
            public async Task<bool> AutoRestoreAsync(ILaunchActivatedEventArgs e, INavigationService nav)
            {
                var restored = false;
                var launchedEvent = e as ILaunchActivatedEventArgs;
                if (DetermineStartCause(e) == AdditionalKinds.Primary || launchedEvent?.TileId == "")
                {
                    restored = await nav.LoadAsync();
                    DebugWrite($"{nameof(restored)}:{restored}", caller: nameof(nav.LoadAsync));
                }
                return restored;
            }

            public async Task AutoSuspendAllFramesAsync(object sender, SuspendingEventArgs e, bool autoExtendExecutionSession)
            {
                DebugWrite($"autoExtendExecutionSession: {autoExtendExecutionSession}");

                if (autoExtendExecutionSession && AnalyticsInfo.VersionInfo.DeviceFamily != "Windows.Desktop")
                {
                    using (var session = new ExtendedExecutionSession { Reason = ExtendedExecutionReason.SavingData })
                    {
                        await SuspendAllFramesAsync();
                    }
                }
                else
                {
                    await SuspendAllFramesAsync();
                }
            }

            private async Task SuspendAllFramesAsync()
            {
                DebugWrite();

                //allow only main view NavigationService as others won't be able to use Dispatcher and processing will stuck
                var services = WindowContext.ActiveWrappers.SelectMany(x => x.NavigationServices).Where(x => x.IsInMainView);
                foreach (INavigationService nav in services.ToArray())
                {
                    try
                    {
                        // call view model suspend (OnNavigatedfrom)
                        // date the cache (which marks the date/time it was suspended)
                        nav.FrameFacade.SetFrameState(CacheDateKey, DateTime.Now.ToString());
                        DebugWrite($"Nav.FrameId:{nav.FrameFacade.FrameId}");
                        await nav.GetDispatcherWrapper().DispatchAsync(async () => await nav.SuspendingAsync());
                    }
                    catch (Exception ex)
                    {
                        DebugWrite($"FrameId: [{nav.FrameFacade.FrameId}] {ex} {ex.Message}", caller: nameof(AutoSuspendAllFramesAsync));
                    }
                }
            }
        }

        private class WindowLogic
        {
            public enum ActivateWindowSources { Launching, Activating, Resuming }
            /// <summary>
            /// Override this method only if you (the developer) wants to programmatically
            /// control the means by which and when the Core Window is activated by Template 10.
            /// One scenario might be a delayed activation for Splash Screen.
            /// </summary>
            /// <param name="source">Reason for the call from Template 10</param>
            public void ActivateWindow(ActivateWindowSources source)
            {
                DebugWrite($"source:{source}");

                Window.Current.Activate();
            }
        }
    }

    public interface INavigablePage
    {
        void OnBackRequested(HandledEventArgs args);
    }

    public interface INavigatingPage : INavigablePage
    {
        void OnBackRequesting(HandledEventArgs args);
    }

    public interface ISearchablePage
    {
        void Search();
    }

    public interface IActivablePage : IDisposable
    {
        void Activate();
    }
}
