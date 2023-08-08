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
using Telegram.ViewModels;
using Telegram.ViewModels.Authorization;
using Telegram.ViewModels.BasicGroups;
using Telegram.ViewModels.Channels;
using Telegram.ViewModels.Chats;
using Telegram.ViewModels.Delegates;
using Telegram.ViewModels.Folders;
using Telegram.ViewModels.Payments;
using Telegram.ViewModels.Premium;
using Telegram.ViewModels.Settings;
using Telegram.ViewModels.Settings.Password;
using Telegram.ViewModels.Settings.Privacy;
using Telegram.ViewModels.Supergroups;
using Telegram.ViewModels.Users;
using Telegram.Views;
using Telegram.Views.Authorization;
using Telegram.Views.BasicGroups;
using Telegram.Views.Channels;
using Telegram.Views.Chats;
using Telegram.Views.Folders;
using Telegram.Views.Folders.Popups;
using Telegram.Views.Host;
using Telegram.Views.Payments;
using Telegram.Views.Popups;
using Telegram.Views.Premium.Popups;
using Telegram.Views.Settings;
using Telegram.Views.Settings.Password;
using Telegram.Views.Settings.Popups;
using Telegram.Views.Settings.Privacy;
using Telegram.Views.Stories.Popups;
using Telegram.Views.Supergroups;
using Telegram.Views.Users;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.ShareTarget;
using Windows.ApplicationModel.ExtendedExecution;
using Windows.UI.Core;
using Windows.UI.Notifications;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram
{
    sealed partial class App : BootStrapper
    {
        public static ShareOperation ShareOperation { get; set; }
        public static Window ShareWindow { get; set; }

        public static ConcurrentDictionary<long, DataPackageView> DataPackages { get; } = new ConcurrentDictionary<long, DataPackageView>();

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

            WatchDog.Initialize();
            TLContainer.Current.Configure();

            RequestedTheme = SettingsService.Current.Appearance.GetCalculatedApplicationTheme();
            InitializeComponent();

            UnhandledException += (s, args) =>
            {
                args.Handled = args.Exception is not LayoutCycleException;
            };

            EnteredBackground += OnEnteringBackground;
        }

        private void OnEnteringBackground(object sender, EnteredBackgroundEventArgs e)
        {
            SystemTray.EnteringBackground(e);
        }

        protected override void OnWindowCreated(WindowCreatedEventArgs args)
        {
            args.Window.Activated += Window_Activated;
            //args.Window.CoreWindow.FlowDirection = LocaleService.Current.FlowDirection == FlowDirection.RightToLeft
            //    ? CoreWindowFlowDirection.RightToLeft
            //    : CoreWindowFlowDirection.LeftToRight;

            base.OnWindowCreated(args);
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs e)
        {
            HandleActivated(e.WindowActivationState != CoreWindowActivationState.Deactivated);
            SettingsService.Current.Appearance.UpdateTimer();
        }

        private void HandleActivated(bool active)
        {
            var aggregator = TLContainer.Current.Resolve<IEventAggregator>();
            aggregator?.Publish(new UpdateWindowActivated(active));

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
                SystemTray.Connect(appService.AppServiceConnection, args.TaskInstance.GetDeferral());

                args.TaskInstance.Canceled += (s, e) =>
                {
                    SystemTray.Cancel();
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
#if DEBUG
            DebugSettings.EnableFrameRateCounter = true;
#endif

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

            if (service?.AuthorizationState != null)
            {
                WindowContext.Current.Activate(args, navService, service.AuthorizationState);
            }

            var context = WindowContext.Current.Dispatcher;
            _ = Task.Run(() => OnStartSync(context));
            //return Task.CompletedTask;

            if (args is not LaunchActivatedEventArgs)
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

        private async void OnStartSync(IDispatcherContext context)
        {
            await RequestExtendedExecutionSessionAsync();
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
            if (await CloudUpdateService.LaunchAsync(context))
            {
                return;
            }

            if (SettingsService.Current.IsTrayVisible)
            {
                await SystemTray.LaunchAsync();
            }
#endif

            Windows.ApplicationModel.Core.CoreApplication.EnablePrelaunch(true);
        }

        private async Task RequestExtendedExecutionSessionAsync()
        {
            if (_extendedSession == null && ApiInfo.IsDesktop)
            {
                var session = new ExtendedExecutionSession();
                session.Reason = ExtendedExecutionReason.Unspecified;
                session.Revoked += ExtendedExecutionSession_Revoked;

                var result = await session.RequestExtensionAsync();
                if (result == ExtendedExecutionResult.Allowed)
                {
                    _extendedSession = session;

                    Logger.Info("ExtendedExecutionResult.Allowed");
                }
                else
                {
                    session.Revoked -= ExtendedExecutionSession_Revoked;
                    session.Dispose();

                    Logger.Warning("ExtendedExecutionResult.Denied");
                }
            }
        }

        private void ExtendedExecutionSession_Revoked(object sender, ExtendedExecutionRevokedEventArgs args)
        {
            Logger.Warning("ExtendedExecutionSession.Revoked");

            if (_extendedSession != null)
            {
                _extendedSession.Dispose();
                _extendedSession = null;
            }
        }

        public override void OnResuming(object s, object e, AppExecutionState previousExecutionState)
        {
            Logger.Info("OnResuming");

            // #1225: Will this work? No one knows.
            foreach (var network in TLContainer.Current.ResolveAll<INetworkService>())
            {
                network.Reconnect();
            }

            //foreach (var client in TLContainer.Current.ResolveAll<IClientService>())
            //{
            //    client.TryInitialize();
            //}

            // #2034: Will this work? No one knows.
            SettingsService.Current.Appearance.UpdateNightMode(null);

            OnStartSync(WindowContext.Current.Dispatcher);
        }

        public override Task OnSuspendingAsync(object s, SuspendingEventArgs e)
        {
            Logger.Info("OnSuspendingAsync");

            TLContainer.Current.Passcode.CloseTime = DateTime.UtcNow;

            return Task.WhenAll(TLContainer.Current.ResolveAll<IVoipService>().Select(x => x.DiscardAsync()));
            //await Task.WhenAll(TLContainer.Current.ResolveAll<IClientService>().Select(x => x.CloseAsync()));
        }

        public override INavigable ViewModelForPage(UIElement page, int sessionId)
        {
            return page switch
            {
                ChatsNearbyPage => TLContainer.Current.Resolve<ChatsNearbyViewModel>(sessionId),
                DiagnosticsPage => TLContainer.Current.Resolve<DiagnosticsViewModel>(sessionId),
                LogOutPage => TLContainer.Current.Resolve<LogOutViewModel>(sessionId),
                ProfilePage profile => TLContainer.Current.Resolve<ProfileViewModel, IProfileDelegate>(profile, sessionId),
                InstantPage => TLContainer.Current.Resolve<InstantViewModel>(sessionId),
                //
                MainPage => TLContainer.Current.Resolve<MainViewModel>(sessionId),
                SettingsPage settings => TLContainer.Current.Resolve<SettingsViewModel, ISettingsDelegate>(settings, sessionId),
                UserCommonChatsPage => TLContainer.Current.Resolve<UserCommonChatsViewModel>(sessionId),
                UserCreatePage => TLContainer.Current.Resolve<UserCreateViewModel>(sessionId),
                UserEditPage userEdit => TLContainer.Current.Resolve<UserEditViewModel, IUserDelegate>(userEdit, sessionId),
                //
                SupergroupAddAdministratorPage => TLContainer.Current.Resolve<SupergroupAddAdministratorViewModel>(sessionId),
                SupergroupAddRestrictedPage => TLContainer.Current.Resolve<SupergroupAddRestrictedViewModel>(sessionId),
                SupergroupAdministratorsPage supergroupAdministrators => TLContainer.Current.Resolve<SupergroupAdministratorsViewModel, ISupergroupDelegate>(supergroupAdministrators, sessionId),
                SupergroupBannedPage supergroupBanned => TLContainer.Current.Resolve<SupergroupBannedViewModel, ISupergroupDelegate>(supergroupBanned, sessionId),
                SupergroupEditAdministratorPage supergroupEditAdministrator => TLContainer.Current.Resolve<SupergroupEditAdministratorViewModel, IMemberDelegate>(supergroupEditAdministrator, sessionId),
                SupergroupEditLinkedChatPage supergroupEditLinkedChat => TLContainer.Current.Resolve<SupergroupEditLinkedChatViewModel, ISupergroupDelegate>(supergroupEditLinkedChat, sessionId),
                SupergroupEditRestrictedPage supergroupEditRestricted => TLContainer.Current.Resolve<SupergroupEditRestrictedViewModel, IMemberDelegate>(supergroupEditRestricted, sessionId),
                SupergroupEditStickerSetPage => TLContainer.Current.Resolve<SupergroupEditStickerSetViewModel>(sessionId),
                SupergroupEditTypePage supergroupEditType => TLContainer.Current.Resolve<SupergroupEditTypeViewModel, ISupergroupEditDelegate>(supergroupEditType, sessionId),
                SupergroupEditPage supergroupEdit => TLContainer.Current.Resolve<SupergroupEditViewModel, ISupergroupEditDelegate>(supergroupEdit, sessionId),
                SupergroupMembersPage supergroupMembers => TLContainer.Current.Resolve<SupergroupMembersViewModel, ISupergroupDelegate>(supergroupMembers, sessionId),
                SupergroupPermissionsPage supergroupPermissions => TLContainer.Current.Resolve<SupergroupPermissionsViewModel, ISupergroupDelegate>(supergroupPermissions, sessionId),
                SupergroupReactionsPage => TLContainer.Current.Resolve<SupergroupReactionsViewModel>(sessionId),
                //
                AuthorizationRecoveryPage => TLContainer.Current.Resolve<AuthorizationRecoveryViewModel>(sessionId),
                AuthorizationRegistrationPage => TLContainer.Current.Resolve<AuthorizationRegistrationViewModel>(sessionId),
                AuthorizationPasswordPage => TLContainer.Current.Resolve<AuthorizationPasswordViewModel>(sessionId),
                AuthorizationCodePage => TLContainer.Current.Resolve<AuthorizationCodeViewModel>(sessionId),
                AuthorizationEmailAddressPage => TLContainer.Current.Resolve<AuthorizationEmailAddressViewModel>(sessionId),
                AuthorizationEmailCodePage => TLContainer.Current.Resolve<AuthorizationEmailCodeViewModel>(sessionId),
                AuthorizationPage signIn => TLContainer.Current.Resolve<AuthorizationViewModel, ISignInDelegate>(signIn, sessionId),
                //
                FoldersPage => TLContainer.Current.Resolve<FoldersViewModel>(sessionId),
                FolderPage => TLContainer.Current.Resolve<FolderViewModel>(sessionId),
                ShareFolderPopup => TLContainer.Current.Resolve<ShareFolderViewModel>(sessionId),
                AddFolderPopup => TLContainer.Current.Resolve<AddFolderViewModel>(sessionId),
                RemoveFolderPopup => TLContainer.Current.Resolve<RemoveFolderViewModel>(sessionId),
                //
                ChannelCreateStep1Page => TLContainer.Current.Resolve<ChannelCreateStep1ViewModel>(sessionId),
                ChannelCreateStep2Page channelCreateStep2 => TLContainer.Current.Resolve<ChannelCreateStep2ViewModel, ISupergroupEditDelegate>(channelCreateStep2, sessionId),
                BasicGroupCreateStep1Page => TLContainer.Current.Resolve<BasicGroupCreateStep1ViewModel>(sessionId),
                SettingsBlockedChatsPage => TLContainer.Current.Resolve<SettingsBlockedChatsViewModel>(sessionId),
                SettingsStickersPage => TLContainer.Current.Resolve<SettingsStickersViewModel>(sessionId),
                //
                SettingsThemePage => TLContainer.Current.Resolve<SettingsThemeViewModel>(sessionId),
                //
                SettingsAdvancedPage => TLContainer.Current.Resolve<SettingsAdvancedViewModel>(sessionId),
                SettingsAppearancePage => TLContainer.Current.Resolve<SettingsAppearanceViewModel>(sessionId),
                SettingsAutoDeletePage => TLContainer.Current.Resolve<SettingsAutoDeleteViewModel>(sessionId),
                SettingsBackgroundsPage => TLContainer.Current.Resolve<SettingsBackgroundsViewModel>(sessionId),
                SettingsDataAndStoragePage => TLContainer.Current.Resolve<SettingsDataAndStorageViewModel>(sessionId),
                SettingsLanguagePage => TLContainer.Current.Resolve<SettingsLanguageViewModel>(sessionId),
                SettingsNetworkPage => TLContainer.Current.Resolve<SettingsNetworkViewModel>(sessionId),
                SettingsNightModePage => TLContainer.Current.Resolve<SettingsNightModeViewModel>(sessionId),
                SettingsNotificationsExceptionsPage => TLContainer.Current.Resolve<SettingsNotificationsExceptionsViewModel>(sessionId),
                SettingsPasscodePage => TLContainer.Current.Resolve<SettingsPasscodeViewModel>(sessionId),
                SettingsPasswordPage => TLContainer.Current.Resolve<SettingsPasswordViewModel>(sessionId),
                SettingsPrivacyAndSecurityPage => TLContainer.Current.Resolve<SettingsPrivacyAndSecurityViewModel>(sessionId),
                SettingsProxyPage => TLContainer.Current.Resolve<SettingsProxyViewModel>(sessionId),
                SettingsShortcutsPage => TLContainer.Current.Resolve<SettingsShortcutsViewModel>(sessionId),
                SettingsThemesPage => TLContainer.Current.Resolve<SettingsThemesViewModel>(sessionId),
                SettingsWebSessionsPage => TLContainer.Current.Resolve<SettingsWebSessionsViewModel>(sessionId),
                SettingsNotificationsPage => TLContainer.Current.Resolve<SettingsNotificationsViewModel>(sessionId),
                SettingsSessionsPage => TLContainer.Current.Resolve<SettingsSessionsViewModel>(sessionId),
                SettingsStoragePage => TLContainer.Current.Resolve<SettingsStorageViewModel>(sessionId),
                SettingsProfilePage settingsProfilePage => TLContainer.Current.Resolve<SettingsProfileViewModel, IUserDelegate>(settingsProfilePage, sessionId),
                SettingsQuickReactionPage => TLContainer.Current.Resolve<SettingsQuickReactionViewModel>(sessionId),
                SettingsPowerSavingPage => TLContainer.Current.Resolve<SettingsPowerSavingViewModel>(sessionId),
                SettingsPrivacyAllowCallsPage => TLContainer.Current.Resolve<SettingsPrivacyAllowCallsViewModel>(sessionId),
                SettingsPrivacyAllowChatInvitesPage => TLContainer.Current.Resolve<SettingsPrivacyAllowChatInvitesViewModel>(sessionId),
                SettingsPrivacyAllowP2PCallsPage => TLContainer.Current.Resolve<SettingsPrivacyAllowP2PCallsViewModel>(sessionId),
                SettingsPrivacyAllowPrivateVoiceAndVideoNoteMessagesPage => TLContainer.Current.Resolve<SettingsPrivacyAllowPrivateVoiceAndVideoNoteMessagesViewModel>(sessionId),
                SettingsPrivacyShowForwardedPage => TLContainer.Current.Resolve<SettingsPrivacyShowForwardedViewModel>(sessionId),
                SettingsPrivacyPhonePage => TLContainer.Current.Resolve<SettingsPrivacyPhoneViewModel>(sessionId),
                SettingsPrivacyShowPhotoPage privacyShowPhotoPage => TLContainer.Current.Resolve<SettingsPrivacyShowPhotoViewModel, IUserDelegate>(privacyShowPhotoPage, sessionId),
                SettingsPrivacyShowStatusPage => TLContainer.Current.Resolve<SettingsPrivacyShowStatusViewModel>(sessionId),
                SettingsPrivacyShowBioPage => TLContainer.Current.Resolve<SettingsPrivacyShowBioViewModel>(sessionId),
                SettingsPasswordConfirmPage => TLContainer.Current.Resolve<SettingsPasswordConfirmViewModel>(sessionId),
                SettingsPasswordCreatePage => TLContainer.Current.Resolve<SettingsPasswordCreateViewModel>(sessionId),
                SettingsPasswordDonePage => TLContainer.Current.Resolve<SettingsPasswordDoneViewModel>(sessionId),
                SettingsPasswordEmailPage => TLContainer.Current.Resolve<SettingsPasswordEmailViewModel>(sessionId),
                SettingsPasswordHintPage => TLContainer.Current.Resolve<SettingsPasswordHintViewModel>(sessionId),
                SettingsPasswordIntroPage => TLContainer.Current.Resolve<SettingsPasswordIntroViewModel>(sessionId),

                PaymentFormPage => TLContainer.Current.Resolve<PaymentFormViewModel>(sessionId),
                MessageStatisticsPage => TLContainer.Current.Resolve<MessageStatisticsViewModel>(sessionId),
                ChatInviteLinkPage => TLContainer.Current.Resolve<ChatInviteLinkViewModel>(sessionId),
                ChatStatisticsPage => TLContainer.Current.Resolve<ChatStatisticsViewModel>(sessionId),
                MyStoriesPage => TLContainer.Current.Resolve<MyStoriesViewModel>(sessionId),

                // Popups
                SettingsUsernamePopup => TLContainer.Current.Resolve<SettingsUsernameViewModel>(sessionId),
                SettingsDataAutoPopup => TLContainer.Current.Resolve<SettingsDataAutoViewModel>(sessionId),
                ChooseChatsPopup => TLContainer.Current.Resolve<ChooseChatsViewModel>(sessionId),
                ChooseSoundPopup => TLContainer.Current.Resolve<ChooseSoundViewModel>(sessionId),
                ChatNotificationsPopup => TLContainer.Current.Resolve<ChatNotificationsViewModel>(sessionId),
                CreateChatPhotoPopup => TLContainer.Current.Resolve<CreateChatPhotoViewModel>(sessionId),
                PromoPopup => TLContainer.Current.Resolve<PromoViewModel>(sessionId),
                InteractionsPopup => TLContainer.Current.Resolve<InteractionsViewModel>(sessionId),
                StoryInteractionsPopup => TLContainer.Current.Resolve<StoryInteractionsViewModel>(sessionId),
                BackgroundsPopup => TLContainer.Current.Resolve<SettingsBackgroundsViewModel>(sessionId),
                BackgroundPopup backgroundPopup => TLContainer.Current.Resolve<BackgroundViewModel, IBackgroundDelegate>(backgroundPopup, sessionId),
                _ => null
            };
        }
    }
}
