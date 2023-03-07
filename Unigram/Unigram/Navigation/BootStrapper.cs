//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Unigram.Logs;
using Unigram.Navigation.Services;
using Unigram.Services.ViewService;
using Windows.ApplicationModel;
using Windows.ApplicationModel.ExtendedExecution;
using Windows.System.Profile;
using Windows.UI.Core;
using Windows.UI.ViewManagement;

namespace Unigram.Navigation
{
#warning TODO: this class needs refactoring
    public abstract class BootStrapper : Application
    {
        public Microsoft.UI.Composition.Compositor Compositor { get; private set; }

        /// <summary>
        /// If a developer overrides this method, the developer can resolve DataContext or unwrap DataContext 
        /// available for the Page object when using a MVVM pattern that relies on a wrapped/porxy around ViewModels
        /// </summary>
        public virtual INavigable ViewModelForPage(UIElement page, int sessionId) => null;

        public static new BootStrapper Current { get; private set; }

        public IDictionary<string, object> SessionState { get; set; } = new Dictionary<string, object>();

        internal volatile bool IsMainWindowCreated;

        private UISettings _uiSettings;
        public UISettings UISettings => _uiSettings ??= new UISettings();

        public BootStrapper()
        {
            Current = this;
        }

        protected virtual void OnWindowCreated(Window window)
        {

        }

        private void Loaded()
        {
            Logger.Info();

            // Hook up keyboard and mouse Back handler
            var keyboard = Unigram.Services.Keyboard.KeyboardService.GetForCurrentView();
            keyboard.AfterBackGesture = (key) =>
            {
                Logger.Info(member: nameof(keyboard.AfterBackGesture));

                var handled = false;
#warning TODO: missing parameter
                RaiseBackRequested(null, key, ref handled);
            };

            keyboard.AfterForwardGesture = () =>
            {
                Logger.Info(member: nameof(keyboard.AfterForwardGesture));

                RaiseForwardRequested();
            };

            // Hook up the default Back handler
            //SystemNavigationManager.GetForCurrentView().BackRequested += BackHandler;
        }

        //protected override void OnWindowCreated(WindowCreatedEventArgs args)
        //{
        //    Logger.Info();

        //    IsMainWindowCreated = true;
        //    //should be called to initialize and set new SynchronizationContext
        //    //if (!WindowWrapper.ActiveWrappers.Any())
        //    // handle window
        //    CreateWindowWrapper(args.Window);
        //    Loaded();
        //    ViewService.OnWindowCreated();
        //    base.OnWindowCreated(args);
        //}

        protected virtual WindowContext CreateWindowWrapper(Window window)
        {
            return new WindowContext(window);
        }

        #region properties

        public INavigationService NavigationService => WindowContext.Current.NavigationServices.FirstOrDefault();

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

        public bool PrelaunchActivated { get; private set; }

        private void CallInternalActivated(LaunchActivatedEventArgs e)
        {
            CurrentState = States.BeforeActivate;
            InternalActivated(e);
            CurrentState = States.AfterActivate;
        }

        /// <summary>
        /// This handles all the prelimimary stuff unique to Activated before calling OnStartAsync()
        /// This is private because it is a specialized prelude to OnStartAsync().
        /// OnStartAsync will not be called if state restore is determined.
        /// </summary>
        private void InternalActivated(LaunchActivatedEventArgs e)
        {
            Logger.Info();

            // sometimes activate requires a frame to be built
            if (Window.Current.Content == null)
            {
                Logger.Info("Calling", member: nameof(InternalActivated));
                InitializeFrame(e);
            }

            // onstart is shared with activate and launch
            CallOnStart(e, true, StartKind.Activate);

            // ensure active (this will hide any custom splashscreen)
            CallActivateWindow(WindowLogic.ActivateWindowSources.Activating);
        }

        #endregion

        #region launch

        // it is the intent of Template 10 to no longer require Launched/Activated overrides, only OnStartAsync()

        protected sealed override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Logger.Info();
            CallInternalLaunchAsync(e);
        }

        private void CallInternalLaunchAsync(LaunchActivatedEventArgs e)
        {
            CurrentState = States.BeforeLaunch;
            InternalLaunch(e);
            CurrentState = States.AfterLaunch;
        }

        /// <summary>
        /// This handles all the preliminary stuff unique to Launched before calling OnStartAsync().
        /// This is private because it is a specialized prelude to OnStartAsync().
        /// OnStartAsync will not be called if state restore is determined
        /// </summary>
        private void InternalLaunch(LaunchActivatedEventArgs e)
        {
            {
                try
                {
                    InitializeFrame(e);
                }
                catch (Exception)
                {
                    // nothing
                }
            }

            // okay, now handle launch
            bool restored = false;
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


            //restored = await CallAutoRestoreAsync(e, restored);

            if (!restored)
            {
                var kind = StartKind.Launch;
                CallOnStart(e, true, kind);
            }

            CallActivateWindow(WindowLogic.ActivateWindowSources.Launching);
        }

        private void BackHandler(object sender, BackRequestedEventArgs args)
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
            var handled = NavigationService?.CanGoBack == false;

            RaiseBackRequested(null, Windows.System.VirtualKey.GoBack, ref handled);
            args.Handled = handled;
        }

        #endregion

        public void RaiseBackRequested(XamlRoot xamlRoot)
        {
            var handled = false;
            RaiseBackRequested(xamlRoot, Windows.System.VirtualKey.GoBack, ref handled);
        }

        /// <summary>
        /// Default Hardware/Shell Back handler overrides standard Back behavior 
        /// that navigates to previous app in the app stack to instead cause a backward page navigation.
        /// Views or Viewodels can override this behavior by handling the BackRequested 
        /// event and setting the Handled property of the BackRequestedEventArgs to true.
        /// </summary>
        private void RaiseBackRequested(XamlRoot xamlRoot, Windows.System.VirtualKey key, ref bool handled)
        {
            Logger.Info();

            var args = new BackRequestedRoutedEventArgs(key);
            BackRequested?.Invoke(null, args);
            if (handled = args.Handled)
            {
                return;
            }

            var popups = VisualTreeHelper.GetOpenPopupsForXamlRoot(xamlRoot);
            foreach (var popup in popups)
            {
                if (popup.Child is INavigablePage page)
                {
                    page.OnBackRequested(args);

                    if (handled = args.Handled)
                    {
                        return;
                    }
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

            foreach (var frame in WindowContext.Current.NavigationServices.Select(x => x.FrameFacade).Reverse())
            {
                frame.RaiseBackRequested(args);

                if (handled = args.Handled)
                {
                    return;
                }
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
            Logger.Info();

            var args = new HandledEventArgs();
            ForwardRequested?.Invoke(null, args);
            if (args.Handled)
            {
                return;
            }

            foreach (var frame in WindowContext.Current.NavigationServices.Select(x => x.FrameFacade))
            {
                frame.RaiseForwardRequested(args);
                if (args.Handled)
                {
                    return;
                }
            }

            NavigationService?.GoForward();
        }

        public void UpdateShellBackButton()
        {
            Logger.Info();

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
        /// OnStartAsync is the one-stop-show override to handle when your app starts
        /// Template 10 will not call OnStartAsync if the app is restored from state.
        /// An app restores from state when the app was suspended and then terminated (PreviousExecutionState terminated).
        /// </summary>
        public abstract void OnStart(StartKind startKind, LaunchActivatedEventArgs args);

        /// <summary>
        /// OnInitializeAsync is where your app will do must-have up-front operations
        /// OnInitializeAsync will be called even if the application is restoring from state.
        /// An app restores from state when the app was suspended and then terminated (PreviousExecutionState terminated).
        /// </summary>
        public virtual void OnInitialize(LaunchActivatedEventArgs args)
        {
            Logger.Info($"Virtual {nameof(LaunchActivatedEventArgs)}:{args}");
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
            Logger.Info($"Virtual {nameof(SuspendingEventArgs)}:{e.SuspendingOperation} {nameof(prelaunchActivated)}:{prelaunchActivated}");

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
            Logger.Info($"Virtual, {nameof(previousExecutionState)}:{previousExecutionState}");
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
            Logger.Info($"{nameof(backButton)}:{backButton} {nameof(ExistingContent)}:{existingContent}");

            return NavigationServiceFactory(backButton, existingContent, new Frame(), session, id, root);
        }

        /// <summary>
        /// Creates the NavigationService instance for given Frame.
        /// </summary>
        protected virtual INavigationService CreateNavigationService(Frame frame, int session, string id, bool root)
        {
            Logger.Info($"Frame:{frame}");

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
            Logger.Info($"{nameof(backButton)}:{backButton} {nameof(existingContent)}:{existingContent} {nameof(frame)}:{frame}");

            //frame.Content = (existingContent == ExistingContent.Include) ? Window.Current.Content : null;

            // if the service already exists for this frame, use the existing one.
            foreach (var nav in WindowContext.ActiveWrappers.SelectMany(x => x.NavigationServices))
            {
                if (nav.FrameFacade.Frame.Equals(frame))
                {
                    return nav;
                }
            }

            var navigationService = CreateNavigationService(frame, session, id, root);
            navigationService.FrameFacade.BackButtonHandling = backButton;
            WindowContext.Current.NavigationServices.Add(navigationService);

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
            // default the cache age to very fresh if not known
            var otherwise = DateTime.MinValue.ToString();
            if (DateTime.TryParse(navigationService.FrameFacade.GetFrameState(CacheDateKey, otherwise), out DateTime cacheDate))
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
            get => _currentState;
            set
            {
                Logger.Info($"CurrenstState changed to {value}");
                CurrentStateHistory.Add($"{DateTime.Now}-{Guid.NewGuid()}", value);
                _currentState = value;
            }
        }

        private readonly Dictionary<string, States> CurrentStateHistory = new Dictionary<string, States>();

        private void InitializeFrame(LaunchActivatedEventArgs e)
        {
            /*
                InitializeFrameAsync creates a default Frame preceeded by the optional 
                splash screen, then OnInitialzieAsync, then the new frame (if necessary).
                This is private because there's no reason for the developer to call this.
            */

            Logger.Info($"{nameof(LaunchActivatedEventArgs)}:{e}");

            CallOnInitialize(false, e);

#warning TODO: melma
            var window = new Window();
            window.Activate();
            OnWindowCreated(window);
            Logger.Info();

            Compositor ??= window.Compositor;
            IsMainWindowCreated = true;
            //should be called to initialize and set new SynchronizationContext
            //if (!WindowWrapper.ActiveWrappers.Any())
            // handle window
            var wrapper = CreateWindowWrapper(window);
            Loaded();
            ViewService.OnWindowCreated();
            window.Content = CreateRootElement(e);

            ((FrameworkElement)window.Content).Loading += (s, args) =>
            {
                wrapper.TrySetMicaBackdrop();
                WindowContext.AddWindow(wrapper);
            };
            return;

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

        private readonly WindowLogic _WindowLogic = new WindowLogic();
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
        public abstract UIElement CreateRootElement(LaunchActivatedEventArgs e);

        public abstract UIElement CreateRootElement(INavigationService navigationService);

        private void CallOnInitialize(bool canRepeat, LaunchActivatedEventArgs e)
        {
            Logger.Info();

            if (!canRepeat && CurrentStateHistory.ContainsValue(States.BeforeInit))
            {
                return;
            }

            CurrentState = States.BeforeInit;
            OnInitialize(e);
            CurrentState = States.AfterInit;
        }

        private void CallOnStart(LaunchActivatedEventArgs args, bool canRepeat, StartKind startKind)
        {
            Logger.Info();

            if (!canRepeat && CurrentStateHistory.ContainsValue(States.BeforeStart))
            {
                return;
            }

            CurrentState = States.BeforeStart;
            OnStart(startKind, args);
            CurrentState = States.AfterStart;
        }

        #endregion

        public enum BackButton { Attach, Ignore }

        public enum ExistingContent { Include, Exclude }

        public const string DefaultTileID = "App";

        public enum AdditionalKinds { Primary, Toast, SecondaryTile, Other, JumpListItem }

        public class LifecycleLogic
        {
            //public async Task<bool> AutoRestoreAsync(LaunchActivatedEventArgs e, INavigationService nav)
            //{
            //    var restored = false;
            //    var launchedEvent = e;
            //    if (DetermineStartCause(e) == AdditionalKinds.Primary || launchedEvent?.TileId == "")
            //    {
            //        restored = await nav.LoadAsync();
            //        Logger.Info($"{nameof(restored)}:{restored}", member: nameof(nav.LoadAsync));
            //    }
            //    return restored;
            //}

            public async Task AutoSuspendAllFramesAsync(object sender, SuspendingEventArgs e, bool autoExtendExecutionSession)
            {
                Logger.Info($"autoExtendExecutionSession: {autoExtendExecutionSession}");

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
                Logger.Info();

                //allow only main view NavigationService as others won't be able to use Dispatcher and processing will stuck
                var services = WindowContext.ActiveWrappers.SelectMany(x => x.NavigationServices).Where(x => x.IsInMainView);
                foreach (INavigationService nav in services.ToArray())
                {
                    try
                    {
                        // call view model suspend (OnNavigatedfrom)
                        // date the cache (which marks the date/time it was suspended)
                        nav.FrameFacade.SetFrameState(CacheDateKey, DateTime.Now.ToString());
                        Logger.Info($"Nav.FrameId:{nav.FrameFacade.FrameId}");
                        await nav.Dispatcher.DispatchAsync(nav.SuspendingAsync);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"FrameId: [{nav.FrameFacade.FrameId}] {ex} {ex.Message}", member: nameof(AutoSuspendAllFramesAsync));
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
                Logger.Info($"source:{source}");

                //Window.Current.Activate();
            }
        }
    }

    public class BackRequestedRoutedEventArgs : HandledEventArgs
    {
        public BackRequestedRoutedEventArgs(Windows.System.VirtualKey key = Windows.System.VirtualKey.None)
        {
            Key = key;
        }

        public Windows.System.VirtualKey Key { get; }
    }

    public interface INavigablePage
    {
        void OnBackRequested(BackRequestedRoutedEventArgs args);
    }

    public interface INavigatingPage : INavigablePage
    {
        void OnBackRequesting(HandledEventArgs args);
    }

    public interface ISearchablePage
    {
        void Search();
    }

    public interface IActivablePage
    {
        void Activate(int sessionId);
        void Deactivate(bool navigating);

        void PopupOpened();
        void PopupClosed();
    }
}
