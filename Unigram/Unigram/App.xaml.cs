using Microsoft.HockeyApp;
using Mono.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Template10.Common;
using Template10.Services.NavigationService;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Core.Notifications;
using Unigram.Core.Services;
using Unigram.Services;
using Unigram.ViewModels;
using Unigram.Views;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.Contacts;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.ShareTarget;
using Windows.ApplicationModel.VoiceCommands;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Media;
using Windows.Media.Playback;
using Windows.Media.SpeechRecognition;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Notifications;
using Windows.UI.StartScreen;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Resources;

namespace Unigram
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : BootStrapper
    {
        public static ShareOperation ShareOperation { get; set; }
        public static DataPackageView DataPackage { get; set; }

        public static AppServiceConnection Connection { get; private set; }

        public static AppInMemoryState InMemoryState { get; } = new AppInMemoryState();

        private readonly UISettings _uiSettings;

        public UISettings UISettings => _uiSettings;

        //public ViewModelLocator Locator
        //{
        //    get
        //    {
        //        return Resources["Locator"] as ViewModelLocator;
        //    }
        //}

        public ViewModelLocator Locator { get; } = new ViewModelLocator();

        public static MediaPlayer Playback { get; } = new MediaPlayer();

        private BackgroundTaskDeferral appServiceDeferral = null;

        private MediaExtensionManager m_mediaExtensionManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class.
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            if (ApplicationSettings.Current.RequestedTheme != ElementTheme.Default)
            {
                RequestedTheme = ApplicationSettings.Current.RequestedTheme == ElementTheme.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light;
            }

#if DEBUG
            Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = "en";
#endif

            Locator.Configure();
            UnigramContainer.Current.ResolveType<IGenerationService>();
            CustomXamlResourceLoader.Current = new XamlResourceLoader();

            InitializeComponent();

            _uiSettings = new UISettings();

            m_mediaExtensionManager = new MediaExtensionManager();
            m_mediaExtensionManager.RegisterByteStreamHandler("Unigram.Native.OpusByteStreamHandler", ".ogg", "audio/ogg");

            InactivityHelper.Detected += Inactivity_Detected;

            UnhandledException += async (s, args) =>
            {
                args.Handled = true;

                try
                {
                    await new TLMessageDialog(args.Exception?.ToString() ?? string.Empty, "Unhandled exception").ShowQueuedAsync();
                }
                catch { }
            };

#if !DEBUG

            HockeyClient.Current.Configure(Constants.HockeyAppId,
                new TelemetryConfiguration()
                {
                    EnableDiagnostics = true,
                    Collectors = WindowsCollectors.Metadata |
                                 WindowsCollectors.Session |
                                 WindowsCollectors.UnhandledException
                });

#endif
        }

        protected override void OnWindowCreated(WindowCreatedEventArgs args)
        {
            CustomXamlResourceLoader.Current = new XamlResourceLoader();
            WindowContext.GetForCurrentView();
            base.OnWindowCreated(args);
        }

        private void Inactivity_Detected(object sender, EventArgs e)
        {
            Execute.BeginOnUIThread(() =>
            {
                var passcode = UnigramContainer.Current.ResolveType<IPasscodeService>();
                if (passcode != null && UIViewSettings.GetForCurrentView().UserInteractionMode == UserInteractionMode.Mouse)
                {
                    passcode.Lock();
                    ShowPasscode();
                }
            });
        }

        private static volatile bool _passcodeShown;
        public static async void ShowPasscode()
        {
            if (_passcodeShown)
            {
                return;
            }

            _passcodeShown = true;

            var dialog = new PasscodePage();
            var result = await dialog.ShowQueuedAsync();

            _passcodeShown = false;
        }

        public static bool IsActive { get; private set; }
        public static bool IsVisible { get; private set; }

        private void Window_Activated(object sender, WindowActivatedEventArgs e)
        {
            IsActive = e.WindowActivationState != CoreWindowActivationState.Deactivated;
            HandleActivated(e.WindowActivationState != CoreWindowActivationState.Deactivated);
        }

        private void Window_VisibilityChanged(object sender, VisibilityChangedEventArgs e)
        {
            IsVisible = e.Visible;
            HandleActivated(e.Visible);

            var passcode = UnigramContainer.Current.ResolveType<IPasscodeService>();
            if (passcode != null)
            {
                if (e.Visible && passcode.IsLockscreenRequired)
                {
                    ShowPasscode();
                }
                else
                {
                    if (UIViewSettings.GetForCurrentView().UserInteractionMode == UserInteractionMode.Touch)
                    {
                        passcode.CloseTime = DateTime.Now;
                    }
                    else
                    {
                        passcode.CloseTime = DateTime.Now.AddYears(1);
                    }
                }
            }

#if !DEBUG && !PREVIEW
            if (e.Visible)
            {
                var dispatcher = Window.Current.Dispatcher;
                Execute.BeginOnThreadPool(async () =>
                {
                    await new HockeyAppUpdateService().CheckForUpdatesAsync(Constants.HockeyAppId, dispatcher);
                });
            }
#endif
        }

        private void HandleActivated(bool active)
        {
            var aggregator = UnigramContainer.Current.ResolveType<IEventAggregator>();
            aggregator.Publish(active ? "Window_Activated" : "Window_Deactivated");

            var protoService = UnigramContainer.Current.ResolveType<IProtoService>();
            protoService.Send(new SetOption("online", new OptionValueBoolean(active)));
        }

        /////// <summary>
        /////// Initializes the app service on the host process 
        /////// </summary>
        ////protected override void OnBackgroundActivated(BackgroundActivatedEventArgs args)
        ////{
        ////    base.OnBackgroundActivated(args);

        ////    if (args.TaskInstance.TriggerDetails is AppServiceTriggerDetails)
        ////    {
        ////        appServiceDeferral = args.TaskInstance.GetDeferral();
        ////        AppServiceTriggerDetails details = args.TaskInstance.TriggerDetails as AppServiceTriggerDetails;
        ////        Connection = details.AppServiceConnection;
        ////    }
        ////    else if (args.TaskInstance.TriggerDetails is RawNotification)
        ////    {
        ////        var task = new NotificationTask();
        ////        task.Run(args.TaskInstance);
        ////    }
        ////    else if (args.TaskInstance.TriggerDetails is ToastNotificationActionTriggerDetail)
        ////    {
        ////        // TODO: upgrade the task to take advanges from in-process execution.
        ////        var task = new InteractiveTask();
        ////        task.Run(args.TaskInstance);
        ////    }
        ////}

        public override UIElement CreateRootElement(IActivatedEventArgs e)
        {
            var navigationFrame = new Frame();
            var navigationService = NavigationServiceFactory(BackButton.Ignore, ExistingContent.Include, navigationFrame) as NavigationService;
            navigationService.SerializationService = TLSerializationService.Current;

            return navigationFrame;
        }

        protected override INavigationService CreateNavigationService(Frame frame)
        {
            return new UnigramNavigationService(UnigramContainer.Current.ResolveType<IProtoService>(), frame);
        }

        public override Task OnInitializeAsync(IActivatedEventArgs args)
        {
            //Locator.Configure();
            //UnigramContainer.Current.ResolveType<IGenerationService>();

            var passcode = UnigramContainer.Current.ResolveType<IPasscodeService>();
            if (passcode != null && passcode.IsEnabled)
            {
                passcode.Lock();
                InactivityHelper.Initialize(passcode.AutolockTimeout);
            }

            if (Window.Current != null)
            {
                Execute.Initialize();

                Window.Current.Activated -= Window_Activated;
                Window.Current.Activated += Window_Activated;
                Window.Current.VisibilityChanged -= Window_VisibilityChanged;
                Window.Current.VisibilityChanged += Window_VisibilityChanged;

                WindowContext.GetForCurrentView().UpdateTitleBar();
                ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size(320, 500));
                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;

                Theme.Current.Update();
                NotifyThemeChanged();
            }

            return base.OnInitializeAsync(args);
        }

        public override async Task OnStartAsync(StartKind startKind, IActivatedEventArgs args)
        {
            var service = UnigramContainer.Current.ResolveType<IProtoService>();

            var state = service.GetAuthorizationState();
            if (state == null)
            {
                return;
            }

            CustomXamlResourceLoader.Current = new XamlResourceLoader();

            //var service = UnigramContainer.Current.ResolveType<IProtoService>();
            //var response = await service.SendAsync(new GetAuthorizationState());
            //if (response is AuthorizationStateReady)
            WindowContext.GetForCurrentView().SetActivatedArgs(args, NavigationService);
            WindowContext.GetForCurrentView().UpdateTitleBar();

            Window.Current.Activated -= Window_Activated;
            Window.Current.Activated += Window_Activated;
            Window.Current.VisibilityChanged -= Window_VisibilityChanged;
            Window.Current.VisibilityChanged += Window_VisibilityChanged;

            ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size(320, 500));
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;

            Theme.Current.Update();
            NotifyThemeChanged();

            var dispatcher = Window.Current.Dispatcher;
            Task.Run(() => OnStartSync(dispatcher));
            //return Task.CompletedTask;
        }

        private bool TryParseCommandLine(CommandLineActivatedEventArgs args, out int id, out bool test)
        {
#if !DEBUG
            if (args.PreviousExecutionState != ApplicationExecutionState.Terminated)
            {
                id = 0;
                test = false;
                return false;
            }
#endif

            try
            {
                var v_id = 0;
                var v_test = false;

                var p = new OptionSet()
                {
                    { "i|id=", (int v) => v_id = v },
                    { "t|test", v => v_test = v != null },
                };

                var extra = p.Parse(args.Operation.Arguments.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));

                id = v_id;
                test = v_test;
                return true;
            }
            catch
            {
                id = 0;
                test = false;
                return false;
            }
        }

        private async void OnStartSync(CoreDispatcher dispatcher)
        {
            //#if DEBUG
            await VoIPConnection.Current.ConnectAsync();
            //#endif

            await Toast.RegisterBackgroundTasks();

            BadgeUpdateManager.CreateBadgeUpdaterForApplication("App").Clear();
            TileUpdateManager.CreateTileUpdaterForApplication("App").Clear();
            ToastNotificationManager.History.Clear("App");

            //if (SettingsHelper.UserId > 0 && ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 2) && JumpList.IsSupported())
            //{
            //    var current = await JumpList.LoadCurrentAsync();
            //    current.SystemGroupKind = JumpListSystemGroupKind.None;
            //    current.Items.Clear();

            //    var cloud = JumpListItem.CreateWithArguments(string.Format("from_id={0}", SettingsHelper.UserId), Strings.Resources.SavedMessages);
            //    cloud.Logo = new Uri("ms-appx:///Assets/JumpList/SavedMessages/SavedMessages.png");

            //    current.Items.Add(cloud);

            //    await current.SaveAsync();
            //}

#if !DEBUG && !PREVIEW
            Execute.BeginOnThreadPool(async () =>
            {
                await new HockeyAppUpdateService().CheckForUpdatesAsync(Constants.HockeyAppId, dispatcher);
            });
#endif

            //if (ApiInformation.IsTypePresent("Windows.ApplicationModel.FullTrustProcessLauncher"))
            //{
            //    await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
            //}

            try
            {
                // Prepare stuff for Cortana
                var localVoiceCommands = await Windows.Storage.StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///VoiceCommands/VoiceCommands.xml"));
                await VoiceCommandDefinitionManager.InstallCommandDefinitionsFromStorageFileAsync(localVoiceCommands);
            }
            catch { }
        }

        public override async void OnResuming(object s, object e, AppExecutionState previousExecutionState)
        {
            Logs.Log.Write("OnResuming");

            //#if DEBUG
            await VoIPConnection.Current.ConnectAsync();
            //#endif

            base.OnResuming(s, e, previousExecutionState);
        }

        public override Task OnSuspendingAsync(object s, SuspendingEventArgs e, bool prelaunchActivated)
        {
            Logs.Log.Write("OnSuspendingAsync");

            return base.OnSuspendingAsync(s, e, prelaunchActivated);
        }


        //private void Window_Activated(object sender, WindowActivatedEventArgs e)
        //{
        //    ((SolidColorBrush)Resources["TelegramTitleBarBackgroundBrush"]).Color = e.WindowActivationState != CoreWindowActivationState.Deactivated ? ((SolidColorBrush)Resources["TelegramTitleBarBackgroundBrushBase"]).Color : ((SolidColorBrush)Resources["TelegramTitleBarBackgroundBrushDeactivated"]).Color;
        //}

        public static void NotifyThemeChanged()
        {
            var frame = Window.Current.Content as Frame;
            if (frame == null)
            {
                return;
            }

            var current = App.Current as App;
            var theme = current.UISettings.GetColorValue(UIColorType.Background);

            frame.RequestedTheme = ApplicationSettings.Current.CurrentTheme == ElementTheme.Dark || (ApplicationSettings.Current.CurrentTheme == ElementTheme.Default && theme.R == 0 && theme.G == 0 && theme.B == 0) ? ElementTheme.Light : ElementTheme.Dark;
            frame.RequestedTheme = ApplicationSettings.Current.CurrentTheme;

            //var dark = (bool)App.Current.Resources["IsDarkTheme"];

            //frame.RequestedTheme = dark ? ElementTheme.Light : ElementTheme.Dark;
            //frame.RequestedTheme = ElementTheme.Default;
        }
    }

    public class AppInMemoryState
    {
        public InlineKeyboardButtonTypeSwitchInline SwitchInline { get; set; }
        public User SwitchInlineBot { get; set; }

        public string SendMessage { get; set; }
        public bool SendMessageUrl { get; set; }

        public int? NavigateToMessage { get; set; }
        public string NavigateToAccessToken { get; set; }
    }
}
