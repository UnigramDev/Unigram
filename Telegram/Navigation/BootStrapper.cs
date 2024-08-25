//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Resources;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation.Services;
using Telegram.Services.ViewService;
using Windows.System;
using Windows.UI.ViewManagement;

namespace Telegram.Navigation
{
    public abstract class BootStrapper : Application
    {
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

        public double TextScaleFactor { get; private set; }

        // This is just a wrapper around current window's compositor.
        // This is done to simplify the porting to WinUI 3 (as there's no concept of current window)
        public Compositor Compositor { get; private set; }

        public BootStrapper()
        {
            Current = this;
            //Resuming += OnResuming;
            //Suspending += OnSuspending;

            UISettings.TextScaleFactorChanged += OnTextScaleFactorChanged;
            TextScaleFactor = UISettings.TextScaleFactor;
        }

        private void OnTextScaleFactorChanged(UISettings sender, object args)
        {
            TextScaleFactor = sender.TextScaleFactor;
        }

        public static Task ConsolidateAsync()
        {
            return WindowContext.ForEachAsync(window => WindowContext.Current.ConsolidateAsync());
        }

        protected virtual void OnWindowCreated(Window window)
        {
            Logger.Info();

            IsMainWindowCreated = true;
            //should be called to initialize and set new SynchronizationContext
            //if (!WindowWrapper.ActiveWrappers.Any())
            // handle window

            // Hook up the default Back handler
            // WARNING: this is used by Xbox (and some Windows users)
            //SystemNavigationManager.GetForCurrentView().BackRequested += BackHandler;

            CustomXamlResourceLoader.Current = new XamlResourceLoader();
            CreateWindowWrapper(window);
            ViewService.OnWindowCreated();

            window.Activated += OnActivated;
            window.Closed += OnClosed;
        }

        private void OnActivated(object sender, WindowActivatedEventArgs e)
        {
            if (sender is Window window)
            {
                OnWindowActivated(window, e.WindowActivationState != WindowActivationState.Deactivated);
            }
        }

        private void OnClosed(object sender, WindowEventArgs e)
        {
            //SystemNavigationManager.GetForCurrentView().BackRequested -= BackHandler;

            if (sender is Window window)
            {
                window.Activated -= OnActivated;
                window.Closed -= OnClosed;

                OnWindowClosed(window);
            }
        }

        protected virtual void OnWindowClosed(Window window)
        {

        }

        protected virtual void OnWindowActivated(Window window, bool active)
        {

        }

        private WindowContext CreateWindowWrapper(Window window)
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
            CallActivateWindow(ActivateWindowSources.Activating);
        }

        #endregion

        #region launch

        // it is the intent of Template 10 to no longer require Launched/Activated overrides, only OnStartAsync()

        protected sealed override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Logger.Info();
            WatchDog.Launch(Windows.ApplicationModel.Activation.ApplicationExecutionState.NotRunning);
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

            CallActivateWindow(ActivateWindowSources.Launching);
        }

        //private void BackHandler(object sender, BackRequestedEventArgs args)
        //{
        //    Logger.Info();

        //    //var handled = false;
        //    //if (ApiInformation.IsApiContractPresent(nameof(Windows.Phone.PhoneContract), 1, 0))
        //    //{
        //    //    if (NavigationService?.CanGoBack == true)
        //    //    {
        //    //        handled = true;
        //    //    }
        //    //}
        //    //else
        //    //{
        //    //    handled = (NavigationService?.CanGoBack == false);
        //    //}
        //    var handled = NavigationService?.CanGoBack == false;

        //    RaiseBackRequested(null, VirtualKey.GoBack, ref handled);
        //    args.Handled = handled;
        //}

        #endregion

        public void RaiseBackRequested(XamlRoot xamlRoot)
        {
            var handled = false;
            RaiseBackRequested(xamlRoot, VirtualKey.GoBack, ref handled);
        }

        public void RaiseBackRequested(XamlRoot xamlRoot, VirtualKey key)
        {
            var handled = false;
            RaiseBackRequested(xamlRoot, key, ref handled);
        }

        /// <summary>
        /// Default Hardware/Shell Back handler overrides standard Back behavior 
        /// that navigates to previous app in the app stack to instead cause a backward page navigation.
        /// Views or Viewodels can override this behavior by handling the BackRequested 
        /// event and setting the Handled property of the BackRequestedEventArgs to true.
        /// </summary>
        private void RaiseBackRequested(XamlRoot xamlRoot, VirtualKey key, ref bool handled)
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
                else if (popup.Child is ToolTip toolTip)
                {
                    toolTip.IsOpen = false;
                }
                else if (popup.Child is TeachingTip teachingTip)
                {
                    if (teachingTip.IsLightDismissEnabled)
                    {
                        teachingTip.IsOpen = false;
                    }
                }
                else if (key == VirtualKey.Escape)
                {
                    if (popup.Child is not Grid)
                    {
                        handled = args.Handled = true;
                        return;
                    }
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

        public void RaiseForwardRequested(XamlRoot xamlRoot)
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
        public INavigationService NavigationServiceFactory(WindowContext window, BackButton backButton, int session, string id, bool root)
        {
            Logger.Info($"{nameof(backButton)}: {backButton}");

            return NavigationServiceFactory(window, backButton, new Frame(), session, id, root);
        }

        /// <summary>
        /// Creates the NavigationService instance for given Frame.
        /// </summary>
        protected virtual INavigationService CreateNavigationService(WindowContext window, Frame frame, int session, string id, bool root)
        {
            Logger.Info($"Frame:{frame}");

            return new NavigationService(window, frame, session, id);
        }

        /// <summary>
        /// Creates a new NavigationService from the gived Frame to the 
        /// WindowWrapper collection. In addition, it optionally will setup the 
        /// shell back button to react to the nav of the Frame.
        /// A developer should call this when creating a new/secondary frame.
        /// The shell back button should only be setup one time.
        /// </summary>
        public INavigationService NavigationServiceFactory(WindowContext window, BackButton backButton, Frame frame, int session, string id, bool root)
        {
            Logger.Info($"{nameof(backButton)}: {backButton} {nameof(frame)}: {frame}");

            frame.Content = null;

            // if the service already exists for this frame, use the existing one.
            foreach (var nav in WindowContext.All.SelectMany(x => x.NavigationServices))
            {
                if (nav.FrameFacade.Frame.Equals(frame))
                {
                    return nav;
                }
            }

            var navigationService = CreateNavigationService(window, frame, session, id, root);
            navigationService.FrameFacade.BackButtonHandling = backButton;
            WindowContext.Current.NavigationServices.Add(navigationService);

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
                Logger.Info($"CurrentState changed to {value}");
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
            //var wrapper = CreateWindowWrapper(window);
            window.Content = CreateRootElement(e);

            //wrapper.Initialize();

            //((FrameworkElement)window.Content).Loading += (s, args) =>
            //{
            //    wrapper.TrySetMicaBackdrop();
            //    WindowContext.AddWindow(wrapper);
            //};
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

        private void CallActivateWindow(ActivateWindowSources source)
        {
            CurrentState = States.Running;
        }

        #region Workers

        /// <summary>
        ///  By default, Template 10 will setup the root element to be a Template 10
        ///  Modal Dialog control. If you desire something different, you can set it here.
        /// </summary>
        public abstract UIElement CreateRootElement(LaunchActivatedEventArgs e, WindowContext window);

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

        public const string DefaultTileID = "App";

        public enum AdditionalKinds { Primary, Toast, SecondaryTile, Other, JumpListItem }


        public enum ActivateWindowSources
        {
            Launching,
            Activating,
            Resuming
        }
    }

    public class BackRequestedRoutedEventArgs : HandledEventArgs
    {
        public BackRequestedRoutedEventArgs(VirtualKey key = VirtualKey.None)
        {
            Key = key;
        }

        public VirtualKey Key { get; }
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
        void Activate(INavigationService navigationService);
        void Deactivate(bool navigating);

        void PopupOpened();
        void PopupClosed();
    }
}
