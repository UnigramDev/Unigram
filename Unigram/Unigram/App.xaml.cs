using Microsoft.HockeyApp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Template10.Common;
using Template10.Services.NavigationService;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Unigram.Services.Settings;
using Unigram.ViewModels;
using Unigram.ViewModels.BasicGroups;
using Unigram.ViewModels.Channels;
using Unigram.ViewModels.Chats;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Dialogs;
using Unigram.ViewModels.Payments;
using Unigram.ViewModels.SecretChats;
using Unigram.ViewModels.Settings;
using Unigram.ViewModels.Settings.Privacy;
using Unigram.ViewModels.SignIn;
using Unigram.ViewModels.Supergroups;
using Unigram.ViewModels.Users;
using Unigram.Views;
using Unigram.Views.BasicGroups;
using Unigram.Views.Channels;
using Unigram.Views.Chats;
using Unigram.Views.Dialogs;
using Unigram.Views.Host;
using Unigram.Views.Payments;
using Unigram.Views.SecretChats;
using Unigram.Views.Settings;
using Unigram.Views.Settings.Privacy;
using Unigram.Views.SignIn;
using Unigram.Views.Supergroups;
using Unigram.Views.Users;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.Contacts;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.ShareTarget;
using Windows.ApplicationModel.ExtendedExecution;
using Windows.ApplicationModel.VoiceCommands;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Media;
using Windows.Media.Playback;
using Windows.Media.SpeechRecognition;
using Windows.System.Profile;
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
        public static Window ShareWindow { get; set; }

        public static ConcurrentDictionary<long, DataPackageView> DataPackages { get; } = new ConcurrentDictionary<long, DataPackageView>();

        public static AppServiceConnection Connection { get; private set; }

        private readonly UISettings _uiSettings;

        public UISettings UISettings => _uiSettings;

        private ExtendedExecutionSession _extendedSession;

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
#if DEBUG
            Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = "en";
#endif

            Locator.Configure(/*session*/);

            if (!SettingsService.Current.Appearance.RequestedTheme.HasFlag(TelegramTheme.Default))
            {
                RequestedTheme = SettingsService.Current.Appearance.GetCalculatedApplicationTheme();
            }

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

            string deviceFamilyVersion = AnalyticsInfo.VersionInfo.DeviceFamilyVersion;
            ulong version = ulong.Parse(deviceFamilyVersion);
            ulong major = (version & 0xFFFF000000000000L) >> 48;
            ulong minor = (version & 0x0000FFFF00000000L) >> 32;
            ulong build = (version & 0x00000000FFFF0000L) >> 16;

            HockeyClient.Current.TrackEvent($"{major}.{minor}.{build}");
            HockeyClient.Current.TrackEvent(AnalyticsInfo.VersionInfo.DeviceFamily);

#endif
        }

        protected override void OnWindowCreated(WindowCreatedEventArgs args)
        {
            //var flowDirectionSetting = Windows.ApplicationModel.Resources.Core.ResourceContext.GetForCurrentView().QualifierValues["LayoutDirection"];

            //args.Window.CoreWindow.FlowDirection = flowDirectionSetting == "RTL" ? CoreWindowFlowDirection.RightToLeft : CoreWindowFlowDirection.LeftToRight;

            CustomXamlResourceLoader.Current = new XamlResourceLoader();
            base.OnWindowCreated(args);
        }

        protected override WindowContext CreateWindowWrapper(Window window)
        {
            return new TLWindowContext(window, 0);
        }

        private void Inactivity_Detected(object sender, EventArgs e)
        {
            WindowContext.Default().Dispatcher.Dispatch(() =>
            {
                var passcode = TLContainer.Current.Resolve<IPasscodeService>();
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

        private void Window_Activated(object sender, WindowActivatedEventArgs e)
        {
            HandleActivated(e.WindowActivationState != CoreWindowActivationState.Deactivated);
            SettingsService.Current.Appearance.UpdateTimer();
        }

        private void Window_VisibilityChanged(object sender, VisibilityChangedEventArgs e)
        {
            HandleActivated(e.Visible);

            if (e.Visible && TLContainer.Current.Passcode.IsLockscreenRequired)
            {
                ShowPasscode();
            }
            else
            {
                if (UIViewSettings.GetForCurrentView().UserInteractionMode == UserInteractionMode.Touch)
                {
                    TLContainer.Current.Passcode.CloseTime = DateTime.Now;
                }
                else
                {
                    TLContainer.Current.Passcode.CloseTime = DateTime.MaxValue;
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
            var aggregator = TLContainer.Current.Resolve<IEventAggregator>();
            if (aggregator != null)
            {
                aggregator.Publish(active ? "Window_Activated" : "Window_Deactivated");
            }

            var cacheService = TLContainer.Current.Resolve<ICacheService>();
            if (cacheService != null)
            {
                cacheService.Options.Online = active;
            }
        }

        protected override async void OnBackgroundActivated(BackgroundActivatedEventArgs args)
        {
            base.OnBackgroundActivated(args);

            if (args.TaskInstance.TriggerDetails is ToastNotificationActionTriggerDetail triggerDetail)
            {
                var data = Toast.GetData(triggerDetail);
                if (data == null)
                {
                    return;
                }

                var session = TLContainer.Current.Lifetime.ActiveItem.Id;
                if (data.TryGetValue("session", out string value) && int.TryParse(value, out int result))
                {
                    session = result;
                }

                var deferral = args.TaskInstance.GetDeferral();
                await TLContainer.Current.Resolve<INotificationsService>(session).ProcessAsync(data);
                deferral.Complete();
            }
        }

        public override Task OnInitializeAsync(IActivatedEventArgs args)
        {
            //Locator.Configure();
            //UnigramContainer.Current.ResolveType<IGenerationService>();

            if (TLContainer.Current.Passcode.IsEnabled)
            {
                TLContainer.Current.Passcode.Lock();
                InactivityHelper.Initialize(TLContainer.Current.Passcode.AutolockTimeout);
            }

            if (Window.Current != null)
            {
                Window.Current.Activated -= Window_Activated;
                Window.Current.Activated += Window_Activated;
                Window.Current.VisibilityChanged -= Window_VisibilityChanged;
                Window.Current.VisibilityChanged += Window_VisibilityChanged;

                TLWindowContext.GetForCurrentView().UpdateTitleBar();

                Theme.Current.Update();
                //NotifyThemeChanged();
            }

            return base.OnInitializeAsync(args);
        }

        public override async Task OnStartAsync(StartKind startKind, IActivatedEventArgs args)
        {
            if (startKind == StartKind.Activate)
            {
                var lifetime = TLContainer.Current.Lifetime;
                var sessionId = lifetime.ActiveItem.Id;

                var id = Toast.GetSession(args);
                if (id != null)
                {
                    lifetime.ActiveItem = lifetime.Items.FirstOrDefault(x => x.Id == id.Value) ?? lifetime.ActiveItem;
                }

                if (sessionId != TLContainer.Current.Lifetime.ActiveItem.Id)
                {
                    var root = Window.Current.Content as RootPage;
                    root.Switch(lifetime.ActiveItem);
                }
            }

            var navService = WindowContext.GetForCurrentView().NavigationServices.GetByFrameId($"{TLContainer.Current.Lifetime.ActiveItem.Id}");
            var service = TLContainer.Current.Resolve<IProtoService>();
            if (service == null)
            {
                return;
            }

            var state = service.GetAuthorizationState();
            if (state == null)
            {
                return;
            }

            TLWindowContext.GetForCurrentView().SetActivatedArgs(args, navService);
            TLWindowContext.GetForCurrentView().UpdateTitleBar();

            Window.Current.Activated -= Window_Activated;
            Window.Current.Activated += Window_Activated;
            Window.Current.VisibilityChanged -= Window_VisibilityChanged;
            Window.Current.VisibilityChanged += Window_VisibilityChanged;

            ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size(320, 500));
            //SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;

            //Theme.Current.Update();
            //NotifyThemeChanged();

            var dispatcher = Window.Current.Dispatcher;
            Task.Run(() => OnStartSync(dispatcher));
            //return Task.CompletedTask;
        }

        public override UIElement CreateRootElement(IActivatedEventArgs e)
        {
            var id = Toast.GetSession(e);
            if (id != null)
            {
                TLContainer.Current.Lifetime.ActiveItem = TLContainer.Current.Lifetime.Items.FirstOrDefault(x => x.Id == id.Value) ?? TLContainer.Current.Lifetime.ActiveItem;
            }

            var sessionId = TLContainer.Current.Lifetime.ActiveItem.Id;

            if (e is ContactPanelActivatedEventArgs /*|| (e is ProtocolActivatedEventArgs protocol && protocol.Uri.PathAndQuery.Contains("domain=telegrampassport", StringComparison.OrdinalIgnoreCase))*/)
            {
                var navigationFrame = new Frame { FlowDirection = ApiInfo.FlowDirection };
                var navigationService = NavigationServiceFactory(BackButton.Ignore, ExistingContent.Include, navigationFrame, sessionId, $"Main{sessionId}", false) as NavigationService;
                navigationService.SerializationService = TLSerializationService.Current;

                return navigationFrame;
            }
            else
            {
                var navigationFrame = new Frame();
                var navigationService = NavigationServiceFactory(BackButton.Ignore, ExistingContent.Include, navigationFrame, sessionId, $"{sessionId}", true) as NavigationService;
                navigationService.SerializationService = TLSerializationService.Current;

                return new RootPage(navigationService) { FlowDirection = ApiInfo.FlowDirection };
            }
        }

        public override UIElement CreateRootElement(INavigationService navigationService)
        {
            return new StandalonePage(navigationService) { FlowDirection = ApiInfo.FlowDirection };
        }

        protected override INavigationService CreateNavigationService(Frame frame, int session, string id, bool root)
        {
            if (root)
            {
                return new TLRootNavigationService(TLContainer.Current.Resolve<ISessionService>(session), frame, session, id);
            }

            return new TLNavigationService(TLContainer.Current.Resolve<IProtoService>(session), frame, session, id);
        }

        private async void OnStartSync(CoreDispatcher dispatcher)
        {
            //#if DEBUG
            //await VoIPConnection.Current.ConnectAsync();
            //#endif

            await Toast.RegisterBackgroundTasks();

            try
            {
                TileUpdateManager.CreateTileUpdaterForApplication("App").Clear();
            }
            catch { }

            try
            {
                ToastNotificationManager.History.Clear("App");
            }
            catch { }

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

            return;

            if (_extendedSession == null && AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Desktop")
            {
                var session = new ExtendedExecutionSession { Reason = ExtendedExecutionReason.Unspecified };
                var result = await session.RequestExtensionAsync();

                if (result == ExtendedExecutionResult.Allowed)
                {
                    _extendedSession = session;

                    Logs.Logger.Info(Logs.Target.Lifecycle, "ExtendedExecutionResult.Allowed");
                }
                else
                {
                    session.Dispose();

                    Logs.Logger.Warning(Logs.Target.Lifecycle, "ExtendedExecutionResult.Denied");
                }
            }
        }

        public override async void OnResuming(object s, object e, AppExecutionState previousExecutionState)
        {
            Logs.Logger.Info(Logs.Target.Lifecycle, "OnResuming");

            //#if DEBUG
            //await VoIPConnection.Current.ConnectAsync();
            //#endif

            base.OnResuming(s, e, previousExecutionState);
        }

        public override Task OnSuspendingAsync(object s, SuspendingEventArgs e, bool prelaunchActivated)
        {
            Logs.Logger.Info(Logs.Target.Lifecycle, "OnSuspendingAsync");

            return base.OnSuspendingAsync(s, e, prelaunchActivated);
        }


        //private void Window_Activated(object sender, WindowActivatedEventArgs e)
        //{
        //    ((SolidColorBrush)Resources["TelegramTitleBarBackgroundBrush"]).Color = e.WindowActivationState != CoreWindowActivationState.Deactivated ? ((SolidColorBrush)Resources["TelegramTitleBarBackgroundBrushBase"]).Color : ((SolidColorBrush)Resources["TelegramTitleBarBackgroundBrushDeactivated"]).Color;
        //}

        //public static void NotifyThemeChanged()
        //{
        //    var frame = Window.Current.Content as Frame;
        //    if (frame == null)
        //    {
        //        return;
        //    }

        //    var current = App.Current as App;
        //    var theme = current.UISettings.GetColorValue(UIColorType.Background);

        //    frame.RequestedTheme = SettingsService.Current.Appearance.CurrentTheme.HasFlag(TelegramTheme.Dark) || (SettingsService.Current.Appearance.CurrentTheme.HasFlag(TelegramTheme.Default) && theme.R == 0 && theme.G == 0 && theme.B == 0) ? ElementTheme.Light : ElementTheme.Dark;
        //    //frame.RequestedTheme = ApplicationSettings.Current.CurrentTheme;

        //    //var dark = (bool)App.Current.Resources["IsDarkTheme"];

        //    //frame.RequestedTheme = dark ? ElementTheme.Light : ElementTheme.Dark;
        //    //frame.RequestedTheme = ElementTheme.Default;
        //}
    }
}
