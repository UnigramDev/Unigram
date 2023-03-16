//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Updates;
using Telegram.Services.ViewService;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.Views;
using Telegram.Views.Host;
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

namespace Telegram
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

        public override async void OnStart(StartKind startKind, IActivatedEventArgs args)
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
                    root?.Switch(lifetime.ActiveItem);
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

            if (startKind == StartKind.Activate)
            {
                var view = ApplicationView.GetForCurrentView();
                await ApplicationViewSwitcher.TryShowAsStandaloneAsync(view.Id);
                view.TryResizeView(WindowContext.Current.Size);
            }
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
                Telegram.Views.ChatsNearbyPage => TLContainer.Current.Resolve<Telegram.ViewModels.ChatsNearbyViewModel>(sessionId),
                Telegram.Views.DiagnosticsPage => TLContainer.Current.Resolve<Telegram.ViewModels.DiagnosticsViewModel>(sessionId),
                Telegram.Views.LogOutPage => TLContainer.Current.Resolve<Telegram.ViewModels.LogOutViewModel>(sessionId),
                Telegram.Views.ProfilePage profile => TLContainer.Current.Resolve<Telegram.ViewModels.ProfileViewModel, Telegram.ViewModels.Delegates.IProfileDelegate>(profile, sessionId),
                Telegram.Views.InstantPage => TLContainer.Current.Resolve<Telegram.ViewModels.InstantViewModel>(sessionId),
                //
                Telegram.Views.MainPage => TLContainer.Current.Resolve<Telegram.ViewModels.MainViewModel>(sessionId),
                Telegram.Views.SettingsPage settings => TLContainer.Current.Resolve<Telegram.ViewModels.SettingsViewModel, Telegram.ViewModels.Delegates.ISettingsDelegate>(settings, sessionId),
                Telegram.Views.Users.UserCommonChatsPage => TLContainer.Current.Resolve<Telegram.ViewModels.Users.UserCommonChatsViewModel>(sessionId),
                Telegram.Views.Users.UserCreatePage => TLContainer.Current.Resolve<Telegram.ViewModels.Users.UserCreateViewModel>(sessionId),
                Telegram.Views.Users.UserEditPage userEdit => TLContainer.Current.Resolve<Telegram.ViewModels.Users.UserEditViewModel, Telegram.ViewModels.Delegates.IUserDelegate>(userEdit, sessionId),
                //
                Telegram.Views.Supergroups.SupergroupAddAdministratorPage => TLContainer.Current.Resolve<Telegram.ViewModels.Supergroups.SupergroupAddAdministratorViewModel>(sessionId),
                Telegram.Views.Supergroups.SupergroupAddRestrictedPage => TLContainer.Current.Resolve<Telegram.ViewModels.Supergroups.SupergroupAddRestrictedViewModel>(sessionId),
                Telegram.Views.Supergroups.SupergroupAdministratorsPage supergroupAdministrators => TLContainer.Current.Resolve<Telegram.ViewModels.Supergroups.SupergroupAdministratorsViewModel, Telegram.ViewModels.Delegates.ISupergroupDelegate>(supergroupAdministrators, sessionId),
                Telegram.Views.Supergroups.SupergroupBannedPage supergroupBanned => TLContainer.Current.Resolve<Telegram.ViewModels.Supergroups.SupergroupBannedViewModel, Telegram.ViewModels.Delegates.ISupergroupDelegate>(supergroupBanned, sessionId),
                Telegram.Views.Supergroups.SupergroupEditAdministratorPage supergroupEditAdministrator => TLContainer.Current.Resolve<Telegram.ViewModels.Supergroups.SupergroupEditAdministratorViewModel, Telegram.ViewModels.Delegates.IMemberDelegate>(supergroupEditAdministrator, sessionId),
                Telegram.Views.Supergroups.SupergroupEditLinkedChatPage supergroupEditLinkedChat => TLContainer.Current.Resolve<Telegram.ViewModels.Supergroups.SupergroupEditLinkedChatViewModel, Telegram.ViewModels.Delegates.ISupergroupDelegate>(supergroupEditLinkedChat, sessionId),
                Telegram.Views.Supergroups.SupergroupEditRestrictedPage supergroupEditRestricted => TLContainer.Current.Resolve<Telegram.ViewModels.Supergroups.SupergroupEditRestrictedViewModel, Telegram.ViewModels.Delegates.IMemberDelegate>(supergroupEditRestricted, sessionId),
                Telegram.Views.Supergroups.SupergroupEditStickerSetPage => TLContainer.Current.Resolve<Telegram.ViewModels.Supergroups.SupergroupEditStickerSetViewModel>(sessionId),
                Telegram.Views.Supergroups.SupergroupEditTypePage supergroupEditType => TLContainer.Current.Resolve<Telegram.ViewModels.Supergroups.SupergroupEditTypeViewModel, Telegram.ViewModels.Delegates.ISupergroupEditDelegate>(supergroupEditType, sessionId),
                Telegram.Views.Supergroups.SupergroupEditPage supergroupEdit => TLContainer.Current.Resolve<Telegram.ViewModels.Supergroups.SupergroupEditViewModel, Telegram.ViewModels.Delegates.ISupergroupEditDelegate>(supergroupEdit, sessionId),
                Telegram.Views.Supergroups.SupergroupMembersPage supergroupMembers => TLContainer.Current.Resolve<Telegram.ViewModels.Supergroups.SupergroupMembersViewModel, Telegram.ViewModels.Delegates.ISupergroupDelegate>(supergroupMembers, sessionId),
                Telegram.Views.Supergroups.SupergroupPermissionsPage supergroupPermissions => TLContainer.Current.Resolve<Telegram.ViewModels.Supergroups.SupergroupPermissionsViewModel, Telegram.ViewModels.Delegates.ISupergroupDelegate>(supergroupPermissions, sessionId),
                Telegram.Views.Supergroups.SupergroupReactionsPage => TLContainer.Current.Resolve<Telegram.ViewModels.Supergroups.SupergroupReactionsViewModel>(sessionId),
                //
                Telegram.Views.Authorization.AuthorizationRecoveryPage => TLContainer.Current.Resolve<Telegram.ViewModels.Authorization.AuthorizationRecoveryViewModel>(sessionId),
                Telegram.Views.Authorization.AuthorizationRegistrationPage => TLContainer.Current.Resolve<Telegram.ViewModels.Authorization.AuthorizationRegistrationViewModel>(sessionId),
                Telegram.Views.Authorization.AuthorizationPasswordPage => TLContainer.Current.Resolve<Telegram.ViewModels.Authorization.AuthorizationPasswordViewModel>(sessionId),
                Telegram.Views.Authorization.AuthorizationCodePage => TLContainer.Current.Resolve<Telegram.ViewModels.Authorization.AuthorizationCodeViewModel>(sessionId),
                Telegram.Views.Authorization.AuthorizationEmailAddressPage => TLContainer.Current.Resolve<Telegram.ViewModels.Authorization.AuthorizationEmailAddressViewModel>(sessionId),
                Telegram.Views.Authorization.AuthorizationEmailCodePage => TLContainer.Current.Resolve<Telegram.ViewModels.Authorization.AuthorizationEmailCodeViewModel>(sessionId),
                Telegram.Views.Authorization.AuthorizationPage signIn => TLContainer.Current.Resolve<Telegram.ViewModels.Authorization.AuthorizationViewModel, Telegram.ViewModels.Delegates.ISignInDelegate>(signIn, sessionId),
                //
                Telegram.Views.Folders.FoldersPage => TLContainer.Current.Resolve<Telegram.ViewModels.Folders.FoldersViewModel>(sessionId),
                Telegram.Views.Folders.FolderPage => TLContainer.Current.Resolve<Telegram.ViewModels.Folders.FolderViewModel>(sessionId),
                Telegram.Views.Channels.ChannelCreateStep1Page => TLContainer.Current.Resolve<Telegram.ViewModels.Channels.ChannelCreateStep1ViewModel>(sessionId),
                Telegram.Views.Channels.ChannelCreateStep2Page channelCreateStep2 => TLContainer.Current.Resolve<Telegram.ViewModels.Channels.ChannelCreateStep2ViewModel, Telegram.ViewModels.Delegates.ISupergroupEditDelegate>(channelCreateStep2, sessionId),
                Telegram.Views.BasicGroups.BasicGroupCreateStep1Page => TLContainer.Current.Resolve<Telegram.ViewModels.BasicGroups.BasicGroupCreateStep1ViewModel>(sessionId),
                Telegram.Views.Settings.SettingsBlockedChatsPage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.SettingsBlockedChatsViewModel>(sessionId),
                Telegram.Views.Settings.SettingsStickersPage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.SettingsStickersViewModel>(sessionId),
                //
                Telegram.Views.Settings.SettingsThemePage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.SettingsThemeViewModel>(sessionId),
                Telegram.Views.Settings.SettingsPhoneSentCodePage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.SettingsPhoneSentCodeViewModel>(sessionId),
                Telegram.Views.Settings.SettingsPhonePage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.SettingsPhoneViewModel>(sessionId),
                //
                Telegram.Views.Settings.SettingsAdvancedPage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.SettingsAdvancedViewModel>(sessionId),
                Telegram.Views.Settings.SettingsAppearancePage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.SettingsAppearanceViewModel>(sessionId),
                Telegram.Views.Settings.SettingsAutoDeletePage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.SettingsAutoDeleteViewModel>(sessionId),
                Telegram.Views.Settings.SettingsBackgroundsPage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.SettingsBackgroundsViewModel>(sessionId),
                Telegram.Views.Settings.SettingsDataAndStoragePage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.SettingsDataAndStorageViewModel>(sessionId),
                Telegram.Views.Settings.SettingsLanguagePage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.SettingsLanguageViewModel>(sessionId),
                Telegram.Views.Settings.SettingsNetworkPage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.SettingsNetworkViewModel>(sessionId),
                Telegram.Views.Settings.SettingsNightModePage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.SettingsNightModeViewModel>(sessionId),
                Telegram.Views.Settings.SettingsNotificationsExceptionsPage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.SettingsNotificationsExceptionsViewModel>(sessionId),
                Telegram.Views.Settings.SettingsPasscodePage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.SettingsPasscodeViewModel>(sessionId),
                Telegram.Views.Settings.SettingsPasswordPage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.SettingsPasswordViewModel>(sessionId),
                Telegram.Views.Settings.SettingsPrivacyAndSecurityPage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.SettingsPrivacyAndSecurityViewModel>(sessionId),
                Telegram.Views.Settings.SettingsProxiesPage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.SettingsProxiesViewModel>(sessionId),
                Telegram.Views.Settings.SettingsShortcutsPage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.SettingsShortcutsViewModel>(sessionId),
                Telegram.Views.Settings.SettingsThemesPage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.SettingsThemesViewModel>(sessionId),
                Telegram.Views.Settings.SettingsWebSessionsPage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.SettingsWebSessionsViewModel>(sessionId),
                Telegram.Views.Settings.SettingsNotificationsPage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.SettingsNotificationsViewModel>(sessionId),
                Telegram.Views.Settings.SettingsSessionsPage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.SettingsSessionsViewModel>(sessionId),
                Telegram.Views.Settings.SettingsStoragePage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.SettingsStorageViewModel>(sessionId),
                Telegram.Views.Settings.SettingsProfilePage settingsProfilePage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.SettingsProfileViewModel, Telegram.ViewModels.Delegates.IUserDelegate>(settingsProfilePage, sessionId),
                Telegram.Views.Settings.SettingsQuickReactionPage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.SettingsQuickReactionViewModel>(sessionId),
                Telegram.Views.Settings.SettingsPowerSavingPage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.SettingsPowerSavingViewModel>(sessionId),
                Telegram.Views.Settings.Privacy.SettingsPrivacyAllowCallsPage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.Privacy.SettingsPrivacyAllowCallsViewModel>(sessionId),
                Telegram.Views.Settings.Privacy.SettingsPrivacyAllowChatInvitesPage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.Privacy.SettingsPrivacyAllowChatInvitesViewModel>(sessionId),
                Telegram.Views.Settings.Privacy.SettingsPrivacyAllowP2PCallsPage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.Privacy.SettingsPrivacyAllowP2PCallsViewModel>(sessionId),
                Telegram.Views.Settings.Privacy.SettingsPrivacyAllowPrivateVoiceAndVideoNoteMessagesPage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.Privacy.SettingsPrivacyAllowPrivateVoiceAndVideoNoteMessagesViewModel>(sessionId),
                Telegram.Views.Settings.Privacy.SettingsPrivacyShowForwardedPage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.Privacy.SettingsPrivacyShowForwardedViewModel>(sessionId),
                Telegram.Views.Settings.Privacy.SettingsPrivacyPhonePage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.Privacy.SettingsPrivacyPhoneViewModel>(sessionId),
                Telegram.Views.Settings.Privacy.SettingsPrivacyShowPhotoPage privacyShowPhotoPage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.Privacy.SettingsPrivacyShowPhotoViewModel, Telegram.ViewModels.Delegates.IUserDelegate>(privacyShowPhotoPage, sessionId),
                Telegram.Views.Settings.Privacy.SettingsPrivacyShowStatusPage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.Privacy.SettingsPrivacyShowStatusViewModel>(sessionId),
                Telegram.Views.Settings.Password.SettingsPasswordConfirmPage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.Password.SettingsPasswordConfirmViewModel>(sessionId),
                Telegram.Views.Settings.Password.SettingsPasswordCreatePage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.Password.SettingsPasswordCreateViewModel>(sessionId),
                Telegram.Views.Settings.Password.SettingsPasswordDonePage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.Password.SettingsPasswordDoneViewModel>(sessionId),
                Telegram.Views.Settings.Password.SettingsPasswordEmailPage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.Password.SettingsPasswordEmailViewModel>(sessionId),
                Telegram.Views.Settings.Password.SettingsPasswordHintPage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.Password.SettingsPasswordHintViewModel>(sessionId),
                Telegram.Views.Settings.Password.SettingsPasswordIntroPage => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.Password.SettingsPasswordIntroViewModel>(sessionId),

                Telegram.Views.Payments.PaymentFormPage => TLContainer.Current.Resolve<Telegram.ViewModels.Payments.PaymentFormViewModel>(sessionId),
                Telegram.Views.Chats.MessageStatisticsPage => TLContainer.Current.Resolve<Telegram.ViewModels.Chats.MessageStatisticsViewModel>(sessionId),
                Telegram.Views.Chats.ChatInviteLinkPage => TLContainer.Current.Resolve<Telegram.ViewModels.Chats.ChatInviteLinkViewModel>(sessionId),
                Telegram.Views.Chats.ChatStatisticsPage => TLContainer.Current.Resolve<Telegram.ViewModels.Chats.ChatStatisticsViewModel>(sessionId),

                // Popups
                Telegram.Views.Settings.Popups.SettingsUsernamePopup => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.SettingsUsernameViewModel>(sessionId),
                Telegram.Views.Settings.Popups.SettingsDataAutoPopup => TLContainer.Current.Resolve<Telegram.ViewModels.Settings.SettingsDataAutoViewModel>(sessionId),
                Telegram.Views.Popups.ChooseSoundPopup => TLContainer.Current.Resolve<Telegram.ViewModels.ChooseSoundViewModel>(sessionId),
                Telegram.Views.Popups.CreateChatPhotoPopup => TLContainer.Current.Resolve<Telegram.ViewModels.CreateChatPhotoViewModel>(sessionId),
                Telegram.Views.Premium.Popups.PromoPopup => TLContainer.Current.Resolve<Telegram.ViewModels.Premium.PromoViewModel>(sessionId),
                _ => null
            };
        }
    }
}
