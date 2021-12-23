﻿using System;
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
using Unigram.Services.ViewService;
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

        public bool IsBackground { get; private set; }

        public static ConcurrentDictionary<long, DataPackageView> DataPackages { get; } = new ConcurrentDictionary<long, DataPackageView>();

        public static AppServiceConnection Connection { get; private set; }
        public static BackgroundTaskDeferral Deferral { get; private set; }

        private ExtendedExecutionSession _extendedSession;
        private readonly MediaExtensionManager _mediaExtensionManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class.
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            TLContainer.Current.Configure(out int count);

            RequestedTheme = SettingsService.Current.Appearance.GetCalculatedApplicationTheme();
            InitializeComponent();

            try
            {
                //_mediaExtensionManager = new MediaExtensionManager();
                //_mediaExtensionManager.RegisterByteStreamHandler("Unigram.Native.Media.OpusByteStreamHandler", ".ogg", "audio/ogg");
                //_mediaExtensionManager.RegisterByteStreamHandler("Unigram.Native.Media.OpusByteStreamHandler", ".oga", "audio/ogg");
            }
            catch
            {
                // User won't be able to play and record voice messages, but it still better than not being able to use the app at all.
            }

            InactivityHelper.Detected += Inactivity_Detected;

            UnhandledException += (s, args) =>
            {
                args.Handled = true;

                if (args.Exception is not NotSupportedException)
                {
                    Client.Execute(new AddLogMessage(1, "Unhandled exception:\n" + args.Exception.ToString()));
                }
            };

#if !DEBUG
            Microsoft.AppCenter.AppCenter.Start(Constants.AppCenterId,
                typeof(Microsoft.AppCenter.Analytics.Analytics),
                typeof(Microsoft.AppCenter.Crashes.Crashes));

            Microsoft.AppCenter.Crashes.Crashes.ShouldProcessErrorReport = error =>
            {
#pragma warning disable CS0618 // Type or member is obsolete
                return error.Exception is not NotSupportedException;
#pragma warning restore CS0618 // Type or member is obsolete
            };

            Microsoft.AppCenter.Analytics.Analytics.TrackEvent("Windows",
                new System.Collections.Generic.Dictionary<string, string>
                {
                    { "DeviceFamily", AnalyticsInfo.VersionInfo.DeviceFamily },
                    { "Architecture", Package.Current.Id.Architecture.ToString() }
                });

            Microsoft.AppCenter.Analytics.Analytics.TrackEvent("Instance",
                new System.Collections.Generic.Dictionary<string, string>
                {
                    { "ActiveSessions", $"{count}" },
                });

            Client.SetLogMessageCallback(0, FatalErrorCallback);

            var lastMessage = SettingsService.Current.Diagnostics.LastErrorMessage;
            if (lastMessage != null && lastMessage.Length > 0 && SettingsService.Current.Diagnostics.LastErrorVersion == Package.Current.Id.Version.Build)
            {
                SettingsService.Current.Diagnostics.LastErrorMessage = null;
                SettingsService.Current.Diagnostics.IsLastErrorDiskFull = TdException.IsDiskFullError(lastMessage);
                Microsoft.AppCenter.Crashes.Crashes.TrackError(TdException.FromMessage(lastMessage));
            }
#endif

            EnteredBackground += OnEnteredBackground;
            LeavingBackground += OnLeavingBackground;
        }

        private void FatalErrorCallback(int verbosityLevel, string message)
        {
            if (verbosityLevel == 0)
            {
                message += Environment.NewLine;
                message += "Application version: " + SettingsPage.GetVersion();
                message += Environment.NewLine;
                message += "Entered background: " + IsBackground;

                SettingsService.Current.Diagnostics.LastErrorMessage = message;
                SettingsService.Current.Diagnostics.LastErrorVersion = Package.Current.Id.Version.Build;
            }
        }

        private void OnEnteredBackground(object sender, EnteredBackgroundEventArgs e)
        {
            IsBackground = true;
        }

        private void OnLeavingBackground(object sender, LeavingBackgroundEventArgs e)
        {
            IsBackground = false;
        }

        protected override void OnWindowCreated(WindowCreatedEventArgs args)
        {
            args.Window.Activated += Window_Activated;

            //var flowDirectionSetting = Windows.ApplicationModel.Resources.Core.ResourceContext.GetForCurrentView().QualifierValues["LayoutDirection"];

            //args.Window.CoreWindow.FlowDirection = flowDirectionSetting == "RTL" ? CoreWindowFlowDirection.RightToLeft : CoreWindowFlowDirection.LeftToRight;

            //Theme.Current.Initialize();
            CustomXamlResourceLoader.Current = new XamlResourceLoader();
            base.OnWindowCreated(args);
        }

        protected override WindowContext CreateWindowWrapper(Window window)
        {
            return new TLWindowContext(window, ApplicationView.GetApplicationViewIdForWindow(window.CoreWindow));
        }

        private void Inactivity_Detected(object sender, EventArgs e)
        {
            WindowContext.Default().Dispatcher.Dispatch(() =>
            {
                var passcode = TLContainer.Current.Passcode;
                if (passcode != null)
                {
                    passcode.Lock();
                    ShowPasscode(false);
                }
            });
        }

        [ThreadStatic]
        private static bool _passcodeShown;
        public static async void ShowPasscode(bool biometrics)
        {
            if (_passcodeShown)
            {
                return;
            }

            _passcodeShown = true;

            // This is a rare case, but it can happen.
            var content = Window.Current.Content;
            if (content != null)
            {
                content.Visibility = Visibility.Collapsed;
            }

            var dialog = new PasscodePage(biometrics);
            void handler(ContentDialog s, ContentDialogClosingEventArgs args)
            {
                dialog.Closing -= handler;

                // This is a rare case, but it can happen.
                var content = Window.Current.Content;
                if (content != null)
                {
                    content.Visibility = Visibility.Visible;
                }
            }

            dialog.Closing += handler;
            var result = await dialog.ShowQueuedAsync();

            _passcodeShown = false;
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs e)
        {
            HandleActivated(Window.Current.CoreWindow.ActivationMode == CoreWindowActivationMode.ActivatedInForeground);
            SettingsService.Current.Appearance.UpdateTimer();
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

                    if (TLContainer.Current.TryResolve(session, out INotificationsService service))
                    {
                        await service.ProcessAsync(data);
                    }
                }
                else if (args.TaskInstance.TriggerDetails is RawNotification notification)
                {
                    static int? GetSession(long id)
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

                    if (TLContainer.Current.TryResolve(session.Value, out IProtoService service))
                    {
                        var response = await service.SendAsync(new ProcessPushNotification(notification.Content));
                        if (response is Error error && error.Code == 406)
                        {
                            // xd memes
                            await Task.Delay(5000);
                        }
                    }
                }

                deferral.Complete();
            }
        }

        private void AppServiceConnection_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            if (args.Request.Message.TryGetValue("Exit", out _))
            {
                Application.Current.Exit();
            }
        }

        public override void OnInitialize(IActivatedEventArgs args)
        {
            //Locator.Configure();
            //UnigramContainer.Current.ResolveType<IGenerationService>();

            if (TLContainer.Current.Passcode.IsEnabled)
            {
                TLContainer.Current.Passcode.Lock();
                InactivityHelper.Initialize(TLContainer.Current.Passcode.AutolockTimeout);
            }
        }

        public override void OnStart(StartKind startKind, IActivatedEventArgs args)
        {
            if (TLContainer.Current.Passcode.IsLockscreenRequired)
            {
                ShowPasscode(true);
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
            ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size(320, 500));
            //SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;

            Task.Run(() => OnStartSync());
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

            return new TLNavigationService(TLContainer.Current.Resolve<IProtoService>(session), TLContainer.Current.Resolve<IViewService>(session), frame, session, id);
        }

        private async void OnStartSync()
        {
            await RequestExtendedExecutionSessionAsync();

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

#if !DESKTOP_BRIDGE
            if (SettingsService.Current.IsTrayVisible
                && Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.ApplicationModel.FullTrustProcessLauncher"))
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

            Windows.ApplicationModel.Core.CoreApplication.EnablePrelaunch(true);
        }

        private async Task RequestExtendedExecutionSessionAsync()
        {
            if (_extendedSession == null && AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Desktop")
            {
                var session = new ExtendedExecutionSession();
                session.Reason = ExtendedExecutionReason.Unspecified;
                session.Revoked += ExtendedExecutionSession_Revoked;

                var result = await session.RequestExtensionAsync();
                if (result == ExtendedExecutionResult.Allowed)
                {
                    _extendedSession = session;

                    Logs.Logger.Info(Logs.LogTarget.Lifecycle, "ExtendedExecutionResult.Allowed");
                }
                else
                {
                    session.Revoked -= ExtendedExecutionSession_Revoked;
                    session.Dispose();

                    Logs.Logger.Warning(Logs.LogTarget.Lifecycle, "ExtendedExecutionResult.Denied");
                }
            }
        }

        private void ExtendedExecutionSession_Revoked(object sender, ExtendedExecutionRevokedEventArgs args)
        {
            Logs.Logger.Warning(Logs.LogTarget.Lifecycle, "ExtendedExecutionSession.Revoked");

            if (_extendedSession != null)
            {
                _extendedSession.Dispose();
                _extendedSession = null;
            }
        }

        public override async void OnResuming(object s, object e, AppExecutionState previousExecutionState)
        {
            Logs.Logger.Info(Logs.LogTarget.Lifecycle, "OnResuming");

            // #1225: Will this work? No one knows.
            foreach (var network in TLContainer.Current.ResolveAll<INetworkService>())
            {
                network.Reconnect();
            }

            // #2034: Will this work? No one knows.
            SettingsService.Current.Appearance.UpdateNightMode();

            await RequestExtendedExecutionSessionAsync();
        }

        public override Task OnSuspendingAsync(object s, SuspendingEventArgs e, bool prelaunchActivated)
        {
            Logs.Logger.Info(Logs.LogTarget.Lifecycle, "OnSuspendingAsync");

            TLContainer.Current.Passcode.CloseTime = DateTime.UtcNow;

            return Task.CompletedTask;
        }
    }
}
