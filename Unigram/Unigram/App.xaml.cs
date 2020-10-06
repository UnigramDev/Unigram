using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Navigation;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.Services.Updates;
using Unigram.Views;
using Unigram.Views.Host;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.ShareTarget;
using Windows.ApplicationModel.ExtendedExecution;
using Windows.Foundation;
using Windows.Media;
using Windows.Networking.PushNotifications;
using Windows.Storage;
using Windows.System.Profile;
using Windows.UI.Core;
using Windows.UI.Notifications;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Resources;

namespace Unigram
{
    sealed partial class App : BootStrapper
    {
        public static ShareOperation ShareOperation { get; set; }
        public static Window ShareWindow { get; set; }

        public static ConcurrentDictionary<long, DataPackageView> DataPackages { get; } = new ConcurrentDictionary<long, DataPackageView>();

        public static AppServiceConnection Connection { get; private set; }
        public static BackgroundTaskDeferral Deferral { get; private set; }

        private ExtendedExecutionSession _extendedSession;
        private MediaExtensionManager _mediaExtensionManager;

        public ViewModelLocator Locator { get; } = new ViewModelLocator();

        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class.
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            Locator.Configure(/*session*/);

            RequestedTheme = SettingsService.Current.Appearance.GetCalculatedApplicationTheme();
            InitializeComponent();

            try
            {
                if (!string.Equals(AnalyticsInfo.VersionInfo.DeviceFamily, "Windows.Desktop"))
                {
                    _mediaExtensionManager = new MediaExtensionManager();
                    _mediaExtensionManager.RegisterByteStreamHandler("Unigram.Native.Media.OpusByteStreamHandler", ".ogg", "audio/ogg");
                    _mediaExtensionManager.RegisterByteStreamHandler("Unigram.Native.Media.OpusByteStreamHandler", ".oga", "audio/ogg");
                }
            }
            catch
            {
                // User won't be able to play and record voice messages, but it still better than not being able to use the app at all.
            }

            InactivityHelper.Detected += Inactivity_Detected;

            UnhandledException += (s, args) =>
            {
                if (args.Exception is NotSupportedException)
                {
                    args.Handled = true;
                }
                else
                {
                    Client.Execute(new AddLogMessage(0, "Unhandled exception:\n" + args.Exception.ToString()));
                }

                //try
                //{
                //    await new MessagePopup(args.Exception?.ToString() ?? string.Empty, "Unhandled exception").ShowQueuedAsync();
                //}
                //catch { }
            };

#if !DEBUG
            Microsoft.AppCenter.AppCenter.Start(Constants.AppCenterId,
                typeof(Microsoft.AppCenter.Analytics.Analytics),
                typeof(Microsoft.AppCenter.Crashes.Crashes));

            string deviceFamilyVersion = AnalyticsInfo.VersionInfo.DeviceFamilyVersion;
            ulong version = ulong.Parse(deviceFamilyVersion);
            ulong major = (version & 0xFFFF000000000000L) >> 48;
            ulong minor = (version & 0x0000FFFF00000000L) >> 32;
            ulong build = (version & 0x00000000FFFF0000L) >> 16;

            Microsoft.AppCenter.Analytics.Analytics.TrackEvent($"{major}.{minor}.{build}");
            Microsoft.AppCenter.Analytics.Analytics.TrackEvent(AnalyticsInfo.VersionInfo.DeviceFamily);
#endif
        }

        protected override void OnWindowCreated(WindowCreatedEventArgs args)
        {
            //var flowDirectionSetting = Windows.ApplicationModel.Resources.Core.ResourceContext.GetForCurrentView().QualifierValues["LayoutDirection"];

            //args.Window.CoreWindow.FlowDirection = flowDirectionSetting == "RTL" ? CoreWindowFlowDirection.RightToLeft : CoreWindowFlowDirection.LeftToRight;

            Theme.Current.Initialize();
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

        [ThreadStatic]
        private static bool _passcodeShown;
        public static async void ShowPasscode()
        {
            if (_passcodeShown)
            {
                return;
            }

            _passcodeShown = true;

            Window.Current.Content.Visibility = Visibility.Collapsed;

            var dialog = new PasscodePage();
            TypedEventHandler<ContentDialog, ContentDialogClosingEventArgs> handler = null;
            handler = (s, args) =>
            {
                dialog.Closing -= handler;
                Window.Current.Content.Visibility = Visibility.Visible;
            };

            dialog.Closing += handler;
            var result = await dialog.ShowQueuedAsync();

            _passcodeShown = false;
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs e)
        {
            HandleActivated(Window.Current.CoreWindow.ActivationMode == CoreWindowActivationMode.ActivatedInForeground);
            SettingsService.Current.Appearance.UpdateTimer();
        }

        private void Window_VisibilityChanged(object sender, VisibilityChangedEventArgs e)
        {
            //HandleActivated(e.Visible);

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
        }

        private void HandleActivated(bool active)
        {
            var aggregator = TLContainer.Current.Resolve<IEventAggregator>();
            if (aggregator != null)
            {
                aggregator.Publish(new UpdateWindowActivated(active));
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

            if (args.TaskInstance.TriggerDetails is AppServiceTriggerDetails appService && string.Equals(appService.CallerPackageFamilyName, Package.Current.Id.FamilyName))
            {
                Connection = appService.AppServiceConnection;
                Deferral = args.TaskInstance.GetDeferral();

                appService.AppServiceConnection.RequestReceived += AppServiceConnection_RequestReceived;
                args.TaskInstance.Canceled += (s, e) =>
                {
                    Deferral.Complete();
                };
            }
            else
            {
                var deferral = args.TaskInstance.GetDeferral();

                if (args.TaskInstance.TriggerDetails is ToastNotificationActionTriggerDetail triggerDetail)
                {
                    var data = Toast.GetData(triggerDetail);
                    if (data == null)
                    {
                        deferral.Complete();
                        return;
                    }

                    var session = TLContainer.Current.Lifetime.ActiveItem.Id;
                    if (data.TryGetValue("session", out string value) && int.TryParse(value, out int result))
                    {
                        session = result;
                    }

                    await TLContainer.Current.Resolve<INotificationsService>(session).ProcessAsync(data);
                }
                else if (args.TaskInstance.TriggerDetails is RawNotification notification)
                {
                    int? GetSession(long id)
                    {
                        if (ApplicationData.Current.LocalSettings.Values.TryGet($"User{id}", out int value))
                        {
                            return value;
                        }

                        return null;
                    }

                    var receiver = Client.Execute(new GetPushReceiverId(notification.Content)) as PushReceiverId;
                    if (receiver == null)
                    {
                        deferral.Complete();
                        return;
                    }

                    var session = GetSession(receiver.Id);
                    if (session == null)
                    {
                        deferral.Complete();
                        return;
                    }

                    var service = TLContainer.Current.Resolve<IProtoService>(session.Value);
                    if (service == null)
                    {
                        deferral.Complete();
                        return;
                    }

                    await service.SendAsync(new ProcessPushNotification(notification.Content));

                    foreach (var item in TLContainer.Current.ResolveAll<IProtoService>())
                    {
                        await item.SendAsync(new Close());
                    }
                }

                deferral.Complete();
            }
        }

        private void AppServiceConnection_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            if (args.Request.Message.TryGetValue("Exit", out object exit))
            {
                Application.Current.Exit();
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
            }

            return base.OnInitializeAsync(args);
        }

        public override async Task OnStartAsync(StartKind startKind, IActivatedEventArgs args)
        {
            if (TLContainer.Current.Passcode.IsLockscreenRequired)
            {
                ShowPasscode();
            }

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

                return navigationFrame;
            }
            else
            {
                var navigationFrame = new Frame();
                var navigationService = NavigationServiceFactory(BackButton.Ignore, ExistingContent.Include, navigationFrame, sessionId, $"{sessionId}", true) as NavigationService;

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

#if DESKTOP_BRIDGE
            if (ApiInformation.IsTypePresent("Windows.ApplicationModel.FullTrustProcessLauncher"))
            {
                try
                {
                    await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
                }
                catch
                {
                    // The app has been compiled without desktop bridge
                }
            }
#endif

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

        public override void OnResuming(object s, object e, AppExecutionState previousExecutionState)
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
    }
}
