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

        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class.
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            if (SettingsService.Current.Diagnostics.LastUpdateVersion < Package.Current.Id.Version.Build)
            {
                SettingsService.Current.Diagnostics.LastUpdateTime = DateTime.Now.ToTimestamp();
                SettingsService.Current.Diagnostics.LastUpdateVersion = Package.Current.Id.Version.Build;
                SettingsService.Current.Diagnostics.UpdateCount++;
            }

            TLContainer.Current.Configure(out int count);

            RequestedTheme = SettingsService.Current.Appearance.GetCalculatedApplicationTheme();
            InitializeComponent();

            InactivityHelper.Detected += Inactivity_Detected;

            UnhandledException += (s, args) =>
            {
                args.Handled = args.Exception is not LayoutCycleException;
                Client.Execute(new AddLogMessage(1, "Unhandled exception:\n" + args.Exception.ToString()));
            };

#if !DEBUG
            Microsoft.AppCenter.AppCenter.Start(Constants.AppCenterId,
                typeof(Microsoft.AppCenter.Analytics.Analytics),
                typeof(Microsoft.AppCenter.Crashes.Crashes));

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

                var exception = TdException.FromMessage(lastMessage);
                if (exception.IsUnhandled)
                {
                    Microsoft.AppCenter.Crashes.Crashes.TrackError(exception,
                        SettingsService.Current.Diagnostics.LastErrorProperties.Split(';').Select(x => x.Split('=')).ToDictionary(x => x[0], y => y[1]));
                }
            }
#endif

            EnteredBackground += OnEnteredBackground;
            LeavingBackground += OnLeavingBackground;
        }

        private void FatalErrorCallback(int verbosityLevel, string message)
        {
            if (verbosityLevel == 0)
            {
                var next = DateTime.Now.ToTimestamp();
                var prev = SettingsService.Current.Diagnostics.LastUpdateTime;

                SettingsService.Current.Diagnostics.LastErrorMessage = message;
                SettingsService.Current.Diagnostics.LastErrorVersion = Package.Current.Id.Version.Build;
                SettingsService.Current.Diagnostics.LastErrorProperties = string.Join(';', new[]
                {
                    $"CurrentVersion={SettingsPage.GetVersion()}",
                    $"LastUpdate={next - prev}s",
                    $"UpdateCount={SettingsService.Current.Diagnostics.UpdateCount}"
                });
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
            //args.Window.CoreWindow.FlowDirection = LocaleService.Current.FlowDirection == FlowDirection.RightToLeft
            //    ? CoreWindowFlowDirection.RightToLeft
            //    : CoreWindowFlowDirection.LeftToRight;

            CustomXamlResourceLoader.Current = new XamlResourceLoader();
            base.OnWindowCreated(args);

#if !DEBUG
            var capabilities = Windows.UI.Composition.CompositionCapabilities.GetForCurrentView();

            Microsoft.AppCenter.Analytics.Analytics.TrackEvent("Capabilities",
                new System.Collections.Generic.Dictionary<string, string>
                {
                    { "Supported", capabilities.AreEffectsSupported().ToString() },
                    { "Fast", capabilities.AreEffectsFast().ToString() }
                });
#endif
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
            HandleActivated(e.WindowActivationState != CoreWindowActivationState.Deactivated);
            SettingsService.Current.Appearance.UpdateTimer();
        }

        private void HandleActivated(bool active)
        {
            var aggregator = TLContainer.Current.Resolve<IEventAggregator>();
            if (aggregator != null)
            {
                aggregator.Publish(new UpdateWindowActivated(active));
            }

            var clientService = TLContainer.Current.Resolve<IClientService>();
            if (clientService != null)
            {
                clientService.Options.Online = active;
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

            var navService = WindowContext.Current.NavigationServices.GetByFrameId($"{TLContainer.Current.Lifetime.ActiveItem.Id}");
            var service = TLContainer.Current.Resolve<IClientService>();
            if (service == null)
            {
                return;
            }

            var state = service.GetAuthorizationState();
            if (state == null)
            {
                return;
            }

            TLWindowContext.Current.SetActivatedArgs(args, navService);
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
                var navigationFrame = new Frame { FlowDirection = LocaleService.Current.FlowDirection };
                var navigationService = NavigationServiceFactory(BackButton.Ignore, ExistingContent.Include, navigationFrame, sessionId, $"Main{sessionId}", false) as NavigationService;

                return navigationFrame;
            }
            else
            {
                var navigationFrame = new Frame();
                var navigationService = NavigationServiceFactory(BackButton.Ignore, ExistingContent.Include, navigationFrame, sessionId, $"{sessionId}", true) as NavigationService;

                return new RootPage(navigationService) { FlowDirection = LocaleService.Current.FlowDirection };
            }
        }

        public override UIElement CreateRootElement(INavigationService navigationService)
        {
            return new StandalonePage(navigationService) { FlowDirection = LocaleService.Current.FlowDirection };
        }

        protected override INavigationService CreateNavigationService(Frame frame, int session, string id, bool root)
        {
            if (root)
            {
                return new TLRootNavigationService(TLContainer.Current.Resolve<ISessionService>(session), frame, session, id);
            }

            return new TLNavigationService(TLContainer.Current.Resolve<IClientService>(session), TLContainer.Current.Resolve<IViewService>(session), frame, session, id);
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

#if !DEBUG
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
            SettingsService.Current.Appearance.UpdateNightMode(null);

            await RequestExtendedExecutionSessionAsync();
        }

        public override Task OnSuspendingAsync(object s, SuspendingEventArgs e, bool prelaunchActivated)
        {
            Logs.Logger.Info(Logs.LogTarget.Lifecycle, "OnSuspendingAsync");

            TLContainer.Current.Passcode.CloseTime = DateTime.UtcNow;

            return Task.CompletedTask;
        }

        public override INavigable ViewModelForPage(UIElement page, int sessionId)
        {
            return page switch
            {
                Unigram.Views.ChatsNearbyPage => TLContainer.Current.Resolve<Unigram.ViewModels.ChatsNearbyViewModel>(sessionId),
                Unigram.Views.DiagnosticsPage => TLContainer.Current.Resolve<Unigram.ViewModels.DiagnosticsViewModel>(sessionId),
                Unigram.Views.LogOutPage => TLContainer.Current.Resolve<Unigram.ViewModels.LogOutViewModel>(sessionId),
                Unigram.Views.ProfilePage profile => TLContainer.Current.Resolve<Unigram.ViewModels.ProfileViewModel, Unigram.ViewModels.Delegates.IProfileDelegate>(profile, sessionId),
                Unigram.Views.InstantPage => TLContainer.Current.Resolve<Unigram.ViewModels.InstantViewModel>(sessionId),
                //
                Unigram.Views.MainPage => TLContainer.Current.Resolve<Unigram.ViewModels.MainViewModel>(sessionId),
                Unigram.Views.SettingsPage settings => TLContainer.Current.Resolve<Unigram.ViewModels.SettingsViewModel, Unigram.ViewModels.Delegates.ISettingsDelegate>(settings, sessionId),
                Unigram.Views.Users.UserCommonChatsPage => TLContainer.Current.Resolve<Unigram.ViewModels.Users.UserCommonChatsViewModel>(sessionId),
                Unigram.Views.Users.UserCreatePage => TLContainer.Current.Resolve<Unigram.ViewModels.Users.UserCreateViewModel>(sessionId),
                //
                Unigram.Views.Supergroups.SupergroupAddAdministratorPage => TLContainer.Current.Resolve<Unigram.ViewModels.Supergroups.SupergroupAddAdministratorViewModel>(sessionId),
                Unigram.Views.Supergroups.SupergroupAddRestrictedPage => TLContainer.Current.Resolve<Unigram.ViewModels.Supergroups.SupergroupAddRestrictedViewModel>(sessionId),
                Unigram.Views.Supergroups.SupergroupAdministratorsPage supergroupAdministrators => TLContainer.Current.Resolve<Unigram.ViewModels.Supergroups.SupergroupAdministratorsViewModel, Unigram.ViewModels.Delegates.ISupergroupDelegate>(supergroupAdministrators, sessionId),
                Unigram.Views.Supergroups.SupergroupBannedPage supergroupBanned => TLContainer.Current.Resolve<Unigram.ViewModels.Supergroups.SupergroupBannedViewModel, Unigram.ViewModels.Delegates.ISupergroupDelegate>(supergroupBanned, sessionId),
                Unigram.Views.Supergroups.SupergroupEditAdministratorPage supergroupEditAdministrator => TLContainer.Current.Resolve<Unigram.ViewModels.Supergroups.SupergroupEditAdministratorViewModel, Unigram.ViewModels.Delegates.IMemberDelegate>(supergroupEditAdministrator, sessionId),
                Unigram.Views.Supergroups.SupergroupEditLinkedChatPage supergroupEditLinkedChat => TLContainer.Current.Resolve<Unigram.ViewModels.Supergroups.SupergroupEditLinkedChatViewModel, Unigram.ViewModels.Delegates.ISupergroupDelegate>(supergroupEditLinkedChat, sessionId),
                Unigram.Views.Supergroups.SupergroupEditRestrictedPage supergroupEditRestricted => TLContainer.Current.Resolve<Unigram.ViewModels.Supergroups.SupergroupEditRestrictedViewModel, Unigram.ViewModels.Delegates.IMemberDelegate>(supergroupEditRestricted, sessionId),
                Unigram.Views.Supergroups.SupergroupEditStickerSetPage => TLContainer.Current.Resolve<Unigram.ViewModels.Supergroups.SupergroupEditStickerSetViewModel>(sessionId),
                Unigram.Views.Supergroups.SupergroupEditTypePage supergroupEditType => TLContainer.Current.Resolve<Unigram.ViewModels.Supergroups.SupergroupEditTypeViewModel, Unigram.ViewModels.Delegates.ISupergroupEditDelegate>(supergroupEditType, sessionId),
                Unigram.Views.Supergroups.SupergroupEditPage supergroupEdit => TLContainer.Current.Resolve<Unigram.ViewModels.Supergroups.SupergroupEditViewModel, Unigram.ViewModels.Delegates.ISupergroupEditDelegate>(supergroupEdit, sessionId),
                Unigram.Views.Supergroups.SupergroupMembersPage supergroupMembers => TLContainer.Current.Resolve<Unigram.ViewModels.Supergroups.SupergroupMembersViewModel, Unigram.ViewModels.Delegates.ISupergroupDelegate>(supergroupMembers, sessionId),
                Unigram.Views.Supergroups.SupergroupPermissionsPage supergroupPermissions => TLContainer.Current.Resolve<Unigram.ViewModels.Supergroups.SupergroupPermissionsViewModel, Unigram.ViewModels.Delegates.ISupergroupDelegate>(supergroupPermissions, sessionId),
                Unigram.Views.Supergroups.SupergroupReactionsPage => TLContainer.Current.Resolve<Unigram.ViewModels.Supergroups.SupergroupReactionsViewModel>(sessionId),
                //
                Unigram.Views.Authorization.AuthorizationRecoveryPage => TLContainer.Current.Resolve<Unigram.ViewModels.Authorization.AuthorizationRecoveryViewModel>(sessionId),
                Unigram.Views.Authorization.AuthorizationRegistrationPage => TLContainer.Current.Resolve<Unigram.ViewModels.Authorization.AuthorizationRegistrationViewModel>(sessionId),
                Unigram.Views.Authorization.AuthorizationPasswordPage => TLContainer.Current.Resolve<Unigram.ViewModels.Authorization.AuthorizationPasswordViewModel>(sessionId),
                Unigram.Views.Authorization.AuthorizationCodePage => TLContainer.Current.Resolve<Unigram.ViewModels.Authorization.AuthorizationCodeViewModel>(sessionId),
                Unigram.Views.Authorization.AuthorizationEmailAddressPage => TLContainer.Current.Resolve<Unigram.ViewModels.Authorization.AuthorizationEmailAddressViewModel>(sessionId),
                Unigram.Views.Authorization.AuthorizationEmailCodePage => TLContainer.Current.Resolve<Unigram.ViewModels.Authorization.AuthorizationEmailCodeViewModel>(sessionId),
                Unigram.Views.Authorization.AuthorizationPage signIn => TLContainer.Current.Resolve<Unigram.ViewModels.Authorization.AuthorizationViewModel, Unigram.ViewModels.Delegates.ISignInDelegate>(signIn, sessionId),
                //
                Unigram.Views.Folders.FoldersPage => TLContainer.Current.Resolve<Unigram.ViewModels.Folders.FoldersViewModel>(sessionId),
                Unigram.Views.Folders.FolderPage => TLContainer.Current.Resolve<Unigram.ViewModels.Folders.FolderViewModel>(sessionId),
                Unigram.Views.Channels.ChannelCreateStep1Page => TLContainer.Current.Resolve<Unigram.ViewModels.Channels.ChannelCreateStep1ViewModel>(sessionId),
                Unigram.Views.Channels.ChannelCreateStep2Page channelCreateStep2 => TLContainer.Current.Resolve<Unigram.ViewModels.Channels.ChannelCreateStep2ViewModel, Unigram.ViewModels.Delegates.ISupergroupEditDelegate>(channelCreateStep2, sessionId),
                Unigram.Views.BasicGroups.BasicGroupCreateStep1Page => TLContainer.Current.Resolve<Unigram.ViewModels.BasicGroups.BasicGroupCreateStep1ViewModel>(sessionId),
                Unigram.Views.Settings.SettingsBlockedChatsPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsBlockedChatsViewModel>(sessionId),
                Unigram.Views.Settings.SettingsStickersPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsStickersViewModel>(sessionId),
                //
                Unigram.Views.Settings.SettingsThemePage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsThemeViewModel>(sessionId),
                Unigram.Views.Settings.SettingsPhoneSentCodePage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsPhoneSentCodeViewModel>(sessionId),
                Unigram.Views.Settings.SettingsPhonePage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsPhoneViewModel>(sessionId),
                //
                Unigram.Views.Settings.SettingsAdvancedPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsAdvancedViewModel>(sessionId),
                Unigram.Views.Settings.SettingsAppearancePage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsAppearanceViewModel>(sessionId),
                Unigram.Views.Settings.SettingsAutoDeletePage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsAutoDeleteViewModel>(sessionId),
                Unigram.Views.Settings.SettingsBackgroundsPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsBackgroundsViewModel>(sessionId),
                Unigram.Views.Settings.SettingsDataAndStoragePage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsDataAndStorageViewModel>(sessionId),
                Unigram.Views.Settings.SettingsLanguagePage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsLanguageViewModel>(sessionId),
                Unigram.Views.Settings.SettingsNetworkPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsNetworkViewModel>(sessionId),
                Unigram.Views.Settings.SettingsNightModePage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsNightModeViewModel>(sessionId),
                Unigram.Views.Settings.SettingsNotificationsExceptionsPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsNotificationsExceptionsViewModel>(sessionId),
                Unigram.Views.Settings.SettingsPasscodePage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsPasscodeViewModel>(sessionId),
                Unigram.Views.Settings.SettingsPasswordPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsPasswordViewModel>(sessionId),
                Unigram.Views.Settings.SettingsPrivacyAndSecurityPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsPrivacyAndSecurityViewModel>(sessionId),
                Unigram.Views.Settings.SettingsProxiesPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsProxiesViewModel>(sessionId),
                Unigram.Views.Settings.SettingsShortcutsPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsShortcutsViewModel>(sessionId),
                Unigram.Views.Settings.SettingsThemesPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsThemesViewModel>(sessionId),
                Unigram.Views.Settings.SettingsWebSessionsPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsWebSessionsViewModel>(sessionId),
                Unigram.Views.Settings.SettingsNotificationsPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsNotificationsViewModel>(sessionId),
                Unigram.Views.Settings.SettingsSessionsPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsSessionsViewModel>(sessionId),
                Unigram.Views.Settings.SettingsStoragePage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsStorageViewModel>(sessionId),
                Unigram.Views.Settings.SettingsProfilePage settingsProfilePage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsProfileViewModel, Unigram.ViewModels.Delegates.IUserDelegate>(settingsProfilePage, sessionId),
                Unigram.Views.Settings.SettingsQuickReactionPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsQuickReactionViewModel>(sessionId),
                Unigram.Views.Settings.Privacy.SettingsPrivacyAllowCallsPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.Privacy.SettingsPrivacyAllowCallsViewModel>(sessionId),
                Unigram.Views.Settings.Privacy.SettingsPrivacyAllowChatInvitesPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.Privacy.SettingsPrivacyAllowChatInvitesViewModel>(sessionId),
                Unigram.Views.Settings.Privacy.SettingsPrivacyAllowP2PCallsPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.Privacy.SettingsPrivacyAllowP2PCallsViewModel>(sessionId),
                Unigram.Views.Settings.Privacy.SettingsPrivacyAllowPrivateVoiceAndVideoNoteMessagesPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.Privacy.SettingsPrivacyAllowPrivateVoiceAndVideoNoteMessagesViewModel>(sessionId),
                Unigram.Views.Settings.Privacy.SettingsPrivacyShowForwardedPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.Privacy.SettingsPrivacyShowForwardedViewModel>(sessionId),
                Unigram.Views.Settings.Privacy.SettingsPrivacyPhonePage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.Privacy.SettingsPrivacyPhoneViewModel>(sessionId),
                Unigram.Views.Settings.Privacy.SettingsPrivacyShowPhotoPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.Privacy.SettingsPrivacyShowPhotoViewModel>(sessionId),
                Unigram.Views.Settings.Privacy.SettingsPrivacyShowStatusPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.Privacy.SettingsPrivacyShowStatusViewModel>(sessionId),
                Unigram.Views.Settings.Password.SettingsPasswordConfirmPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.Password.SettingsPasswordConfirmViewModel>(sessionId),
                Unigram.Views.Settings.Password.SettingsPasswordCreatePage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.Password.SettingsPasswordCreateViewModel>(sessionId),
                Unigram.Views.Settings.Password.SettingsPasswordDonePage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.Password.SettingsPasswordDoneViewModel>(sessionId),
                Unigram.Views.Settings.Password.SettingsPasswordEmailPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.Password.SettingsPasswordEmailViewModel>(sessionId),
                Unigram.Views.Settings.Password.SettingsPasswordHintPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.Password.SettingsPasswordHintViewModel>(sessionId),
                Unigram.Views.Settings.Password.SettingsPasswordIntroPage => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.Password.SettingsPasswordIntroViewModel>(sessionId),

                Unigram.Views.Payments.PaymentFormPage => TLContainer.Current.Resolve<Unigram.ViewModels.Payments.PaymentFormViewModel>(sessionId),
                Unigram.Views.Chats.MessageStatisticsPage => TLContainer.Current.Resolve<Unigram.ViewModels.Chats.MessageStatisticsViewModel>(sessionId),
                Unigram.Views.Chats.ChatInviteLinkPage => TLContainer.Current.Resolve<Unigram.ViewModels.Chats.ChatInviteLinkViewModel>(sessionId),
                Unigram.Views.Chats.ChatStatisticsPage => TLContainer.Current.Resolve<Unigram.ViewModels.Chats.ChatStatisticsViewModel>(sessionId),

                // Popups
                Unigram.Views.Settings.Popups.SettingsUsernamePopup => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsUsernameViewModel>(sessionId),
                Unigram.Views.Settings.Popups.SettingsDataAutoPopup => TLContainer.Current.Resolve<Unigram.ViewModels.Settings.SettingsDataAutoViewModel>(sessionId),
                Unigram.Views.Popups.ChooseSoundPopup => TLContainer.Current.Resolve<Unigram.ViewModels.ChooseSoundViewModel>(sessionId),
                Unigram.Views.Popups.CreateChatPhotoPopup => TLContainer.Current.Resolve<Unigram.ViewModels.CreateChatPhotoViewModel>(sessionId),
                Unigram.Views.Premium.Popups.PromoPopup => TLContainer.Current.Resolve<Unigram.ViewModels.Premium.PromoViewModel>(sessionId),
                _ => null
            };
        }
    }
}
