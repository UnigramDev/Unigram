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
using Telegram.Views.Supergroups.Popups;
using Telegram.Views.Users;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.ShareTarget;
using Windows.ApplicationModel.ExtendedExecution;
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
            if (SettingsService.Current.Diagnostics.LastUpdateVersion < Constants.BuildNumber)
            {
                SettingsService.Current.Diagnostics.LastUpdateTime = DateTime.Now.ToTimestamp();
                SettingsService.Current.Diagnostics.LastUpdateVersion = Constants.BuildNumber;
                SettingsService.Current.Diagnostics.UpdateCount++;
            }

            WatchDog.Initialize();
            TypeResolver.Current.Configure();

            RequestedTheme = SettingsService.Current.Appearance.GetCalculatedApplicationTheme();
            InitializeComponent();
        }

        protected override void OnWindowActivated(bool active)
        {
            SettingsService.Current.Appearance.UpdateTimer();

            var aggregator = TypeResolver.Current.Resolve<IEventAggregator>();
            aggregator?.Publish(new UpdateWindowActivated(active));

            var clientService = TypeResolver.Current.Resolve<IClientService>();
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
                NotifyIcon.Connect(appService.AppServiceConnection, args.TaskInstance.GetDeferral());

                args.TaskInstance.Canceled += (s, e) =>
                {
                    NotifyIcon.Cancel();
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

                    var session = TypeResolver.Current.Lifetime.ActiveItem.Id;
                    if (data.TryGetValue("session", out string value) && int.TryParse(value, out int result))
                    {
                        session = result;
                    }

                    if (TypeResolver.Current.TryResolve(session, out INotificationsService service))
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

            if (TypeResolver.Current.Passcode.IsEnabled)
            {
                TypeResolver.Current.Passcode.Lock();
                InactivityHelper.Initialize(TypeResolver.Current.Passcode.AutolockTimeout);
            }
        }

        public override async void OnStart(StartKind startKind, IActivatedEventArgs args)
        {
#if DEBUG
            DebugSettings.EnableFrameRateCounter = false;
#endif

            if (startKind == StartKind.Activate)
            {
                var lifetime = TypeResolver.Current.Lifetime;
                var sessionId = lifetime.ActiveItem.Id;

                var id = Toast.GetSession(args);
                if (id != null)
                {
                    lifetime.ActiveItem = lifetime.Items.FirstOrDefault(x => x.Id == id.Value) ?? lifetime.ActiveItem;
                }

                if (sessionId != TypeResolver.Current.Lifetime.ActiveItem.Id)
                {
                    var root = Window.Current.Content as RootPage;
                    root?.Switch(lifetime.ActiveItem);
                }
            }

            var navService = WindowContext.Current.NavigationServices.GetByFrameId($"{TypeResolver.Current.Lifetime.ActiveItem.Id}");
            var service = TypeResolver.Current.Resolve<IClientService>();

            if (service?.AuthorizationState != null)
            {
                WindowContext.Current.Activate(args, navService, service.AuthorizationState);
            }

            var context = WindowContext.Current.Dispatcher;
            _ = Task.Run(() => OnStartSync(startKind, context));

            if (startKind != StartKind.Launch)
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
                TypeResolver.Current.Lifetime.ActiveItem = TypeResolver.Current.Lifetime.Items.FirstOrDefault(x => x.Id == id.Value) ?? TypeResolver.Current.Lifetime.ActiveItem;
            }

            var sessionId = TypeResolver.Current.Lifetime.ActiveItem.Id;

            if (e is ContactPanelActivatedEventArgs /*|| (e is ProtocolActivatedEventArgs protocol && protocol.Uri.PathAndQuery.Contains("domain=telegrampassport", StringComparison.OrdinalIgnoreCase))*/)
            {
                var navigationFrame = new Frame { FlowDirection = LocaleService.Current.FlowDirection };
                var navigationService = NavigationServiceFactory(BackButton.Ignore, navigationFrame, sessionId, $"Main{sessionId}", false) as NavigationService;

                return navigationFrame;
            }
            else
            {
                var navigationFrame = new Frame();
                var navigationService = NavigationServiceFactory(BackButton.Ignore, navigationFrame, sessionId, $"{sessionId}", true) as NavigationService;

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
                return new TLRootNavigationService(TypeResolver.Current.Resolve<ISessionService>(session), frame, session, id);
            }

            return new TLNavigationService(TypeResolver.Current.Resolve<IClientService>(session), TypeResolver.Current.Resolve<IViewService>(session), frame, session, id);
        }

        private async void OnStartSync(StartKind startKind, IDispatcherContext context)
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

            if (Constants.RELEASE && startKind == StartKind.Launch)
            {
                if (await CloudUpdateService.LaunchAsync(context, true))
                {
                    return;
                }
            }

            if (SettingsService.Current.IsTrayVisible)
            {
                await NotifyIcon.LaunchAsync();
            }

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
            Logger.Warning(args.Reason);

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
            foreach (var network in TypeResolver.Current.ResolveAll<INetworkService>())
            {
                network.Reconnect();
            }

            //foreach (var client in TLContainer.Current.ResolveAll<IClientService>())
            //{
            //    client.TryInitialize();
            //}

            // #2034: Will this work? No one knows.
            SettingsService.Current.Appearance.UpdateNightMode(null);

            OnStartSync(StartKind.Activate, WindowContext.Current.Dispatcher);
        }

        public override Task OnSuspendingAsync(object s, SuspendingEventArgs e)
        {
            Logger.Info("OnSuspendingAsync");

            TypeResolver.Current.Passcode.CloseTime = DateTime.UtcNow;

            return Task.WhenAll(TypeResolver.Current.ResolveAll<IVoipService>().Select(x => x.DiscardAsync()));
            //await Task.WhenAll(TLContainer.Current.ResolveAll<IClientService>().Select(x => x.CloseAsync()));
        }

        public override INavigable ViewModelForPage(UIElement page, int sessionId)
        {
            return page switch
            {
                ChatsNearbyPage => TypeResolver.Current.Resolve<ChatsNearbyViewModel>(sessionId),
                DiagnosticsPage => TypeResolver.Current.Resolve<DiagnosticsViewModel>(sessionId),
                LogOutPage => TypeResolver.Current.Resolve<LogOutViewModel>(sessionId),
                ProfilePage profile => TypeResolver.Current.Resolve<ProfileViewModel, IProfileDelegate>(profile, sessionId),
                InstantPage => TypeResolver.Current.Resolve<InstantViewModel>(sessionId),
                //
                SettingsPage settings => TypeResolver.Current.Resolve<SettingsViewModel, ISettingsDelegate>(settings, sessionId),
                UserCreatePage => TypeResolver.Current.Resolve<UserCreateViewModel>(sessionId),
                UserEditPage userEdit => TypeResolver.Current.Resolve<UserEditViewModel, IUserDelegate>(userEdit, sessionId),
                //
                SupergroupChooseMemberPopup => TypeResolver.Current.Resolve<SupergroupChooseMemberViewModel>(sessionId),
                SupergroupAdministratorsPage supergroupAdministrators => TypeResolver.Current.Resolve<SupergroupAdministratorsViewModel, ISupergroupDelegate>(supergroupAdministrators, sessionId),
                SupergroupBannedPage supergroupBanned => TypeResolver.Current.Resolve<SupergroupBannedViewModel, ISupergroupDelegate>(supergroupBanned, sessionId),
                SupergroupEditAdministratorPopup supergroupEditAdministrator => TypeResolver.Current.Resolve<SupergroupEditAdministratorViewModel, IMemberPopupDelegate>(supergroupEditAdministrator, sessionId),
                SupergroupEditLinkedChatPage supergroupEditLinkedChat => TypeResolver.Current.Resolve<SupergroupEditLinkedChatViewModel, ISupergroupDelegate>(supergroupEditLinkedChat, sessionId),
                SupergroupEditRestrictedPopup supergroupEditRestricted => TypeResolver.Current.Resolve<SupergroupEditRestrictedViewModel, IMemberPopupDelegate>(supergroupEditRestricted, sessionId),
                SupergroupEditStickerSetPage => TypeResolver.Current.Resolve<SupergroupEditStickerSetViewModel>(sessionId),
                SupergroupEditTypePage supergroupEditType => TypeResolver.Current.Resolve<SupergroupEditTypeViewModel, ISupergroupEditDelegate>(supergroupEditType, sessionId),
                SupergroupEditPage supergroupEdit => TypeResolver.Current.Resolve<SupergroupEditViewModel, ISupergroupEditDelegate>(supergroupEdit, sessionId),
                SupergroupMembersPage supergroupMembers => TypeResolver.Current.Resolve<SupergroupMembersViewModel, ISupergroupDelegate>(supergroupMembers, sessionId),
                SupergroupPermissionsPage supergroupPermissions => TypeResolver.Current.Resolve<SupergroupPermissionsViewModel, ISupergroupDelegate>(supergroupPermissions, sessionId),
                SupergroupReactionsPopup => TypeResolver.Current.Resolve<SupergroupReactionsViewModel>(sessionId),
                ChatBoostsPage => TypeResolver.Current.Resolve<ChatBoostsViewModel>(sessionId),
                //
                AuthorizationRecoveryPage => TypeResolver.Current.Resolve<AuthorizationRecoveryViewModel>(sessionId),
                AuthorizationRegistrationPage => TypeResolver.Current.Resolve<AuthorizationRegistrationViewModel>(sessionId),
                AuthorizationPasswordPage => TypeResolver.Current.Resolve<AuthorizationPasswordViewModel>(sessionId),
                AuthorizationCodePage => TypeResolver.Current.Resolve<AuthorizationCodeViewModel>(sessionId),
                AuthorizationEmailAddressPage => TypeResolver.Current.Resolve<AuthorizationEmailAddressViewModel>(sessionId),
                AuthorizationEmailCodePage => TypeResolver.Current.Resolve<AuthorizationEmailCodeViewModel>(sessionId),
                AuthorizationPage signIn => TypeResolver.Current.Resolve<AuthorizationViewModel, ISignInDelegate>(signIn, sessionId),
                //
                FoldersPage => TypeResolver.Current.Resolve<FoldersViewModel>(sessionId),
                FolderPage => TypeResolver.Current.Resolve<FolderViewModel>(sessionId),
                ShareFolderPopup => TypeResolver.Current.Resolve<ShareFolderViewModel>(sessionId),
                AddFolderPopup => TypeResolver.Current.Resolve<AddFolderViewModel>(sessionId),
                RemoveFolderPopup => TypeResolver.Current.Resolve<RemoveFolderViewModel>(sessionId),
                //
                ChannelCreateStep1Page => TypeResolver.Current.Resolve<ChannelCreateStep1ViewModel>(sessionId),
                ChannelCreateStep2Page channelCreateStep2 => TypeResolver.Current.Resolve<ChannelCreateStep2ViewModel, ISupergroupEditDelegate>(channelCreateStep2, sessionId),
                BasicGroupCreateStep1Page => TypeResolver.Current.Resolve<BasicGroupCreateStep1ViewModel>(sessionId),
                SettingsBlockedChatsPage => TypeResolver.Current.Resolve<SettingsBlockedChatsViewModel>(sessionId),
                SettingsStickersPage => TypeResolver.Current.Resolve<SettingsStickersViewModel>(sessionId),
                //
                SettingsThemePage => TypeResolver.Current.Resolve<SettingsThemeViewModel>(sessionId),
                //
                SettingsAdvancedPage => TypeResolver.Current.Resolve<SettingsAdvancedViewModel>(sessionId),
                SettingsAppearancePage => TypeResolver.Current.Resolve<SettingsAppearanceViewModel>(sessionId),
                SettingsAutoDeletePage => TypeResolver.Current.Resolve<SettingsAutoDeleteViewModel>(sessionId),
                SettingsBackgroundsPage => TypeResolver.Current.Resolve<SettingsBackgroundsViewModel>(sessionId),
                SettingsDataAndStoragePage => TypeResolver.Current.Resolve<SettingsDataAndStorageViewModel>(sessionId),
                SettingsLanguagePage => TypeResolver.Current.Resolve<SettingsLanguageViewModel>(sessionId),
                SettingsNetworkPage => TypeResolver.Current.Resolve<SettingsNetworkViewModel>(sessionId),
                SettingsNightModePage => TypeResolver.Current.Resolve<SettingsNightModeViewModel>(sessionId),
                SettingsNotificationsExceptionsPage => TypeResolver.Current.Resolve<SettingsNotificationsExceptionsViewModel>(sessionId),
                SettingsPasscodePage => TypeResolver.Current.Resolve<SettingsPasscodeViewModel>(sessionId),
                SettingsPasswordPage => TypeResolver.Current.Resolve<SettingsPasswordViewModel>(sessionId),
                SettingsPrivacyAndSecurityPage => TypeResolver.Current.Resolve<SettingsPrivacyAndSecurityViewModel>(sessionId),
                SettingsProxyPage => TypeResolver.Current.Resolve<SettingsProxyViewModel>(sessionId),
                SettingsShortcutsPage => TypeResolver.Current.Resolve<SettingsShortcutsViewModel>(sessionId),
                SettingsThemesPage => TypeResolver.Current.Resolve<SettingsThemesViewModel>(sessionId),
                SettingsWebSessionsPage => TypeResolver.Current.Resolve<SettingsWebSessionsViewModel>(sessionId),
                SettingsNotificationsPage => TypeResolver.Current.Resolve<SettingsNotificationsViewModel>(sessionId),
                SettingsSessionsPage => TypeResolver.Current.Resolve<SettingsSessionsViewModel>(sessionId),
                SettingsStoragePage => TypeResolver.Current.Resolve<SettingsStorageViewModel>(sessionId),
                SettingsProfilePage settingsProfilePage => TypeResolver.Current.Resolve<SettingsProfileViewModel, IUserDelegate>(settingsProfilePage, sessionId),
                SettingsQuickReactionPage => TypeResolver.Current.Resolve<SettingsQuickReactionViewModel>(sessionId),
                SettingsPowerSavingPage => TypeResolver.Current.Resolve<SettingsPowerSavingViewModel>(sessionId),
                SettingsPrivacyAllowCallsPage => TypeResolver.Current.Resolve<SettingsPrivacyAllowCallsViewModel>(sessionId),
                SettingsPrivacyAllowChatInvitesPage => TypeResolver.Current.Resolve<SettingsPrivacyAllowChatInvitesViewModel>(sessionId),
                SettingsPrivacyAllowP2PCallsPage => TypeResolver.Current.Resolve<SettingsPrivacyAllowP2PCallsViewModel>(sessionId),
                SettingsPrivacyAllowPrivateVoiceAndVideoNoteMessagesPage => TypeResolver.Current.Resolve<SettingsPrivacyAllowPrivateVoiceAndVideoNoteMessagesViewModel>(sessionId),
                SettingsPrivacyShowForwardedPage => TypeResolver.Current.Resolve<SettingsPrivacyShowForwardedViewModel>(sessionId),
                SettingsPrivacyPhonePage => TypeResolver.Current.Resolve<SettingsPrivacyPhoneViewModel>(sessionId),
                SettingsPrivacyShowPhotoPage privacyShowPhotoPage => TypeResolver.Current.Resolve<SettingsPrivacyShowPhotoViewModel, IUserDelegate>(privacyShowPhotoPage, sessionId),
                SettingsPrivacyShowStatusPage => TypeResolver.Current.Resolve<SettingsPrivacyShowStatusViewModel>(sessionId),
                SettingsPrivacyShowBioPage => TypeResolver.Current.Resolve<SettingsPrivacyShowBioViewModel>(sessionId),
                SettingsPasswordConfirmPage => TypeResolver.Current.Resolve<SettingsPasswordConfirmViewModel>(sessionId),
                SettingsPasswordCreatePage => TypeResolver.Current.Resolve<SettingsPasswordCreateViewModel>(sessionId),
                SettingsPasswordDonePage => TypeResolver.Current.Resolve<SettingsPasswordDoneViewModel>(sessionId),
                SettingsPasswordEmailPage => TypeResolver.Current.Resolve<SettingsPasswordEmailViewModel>(sessionId),
                SettingsPasswordHintPage => TypeResolver.Current.Resolve<SettingsPasswordHintViewModel>(sessionId),
                SettingsPasswordIntroPage => TypeResolver.Current.Resolve<SettingsPasswordIntroViewModel>(sessionId),

                PaymentFormPage => TypeResolver.Current.Resolve<PaymentFormViewModel>(sessionId),
                MessageStatisticsPage => TypeResolver.Current.Resolve<MessageStatisticsViewModel>(sessionId),
                ChatInviteLinkPage => TypeResolver.Current.Resolve<ChatInviteLinkViewModel>(sessionId),
                ChatStatisticsPage => TypeResolver.Current.Resolve<ChatStatisticsViewModel>(sessionId),
                ChatStoriesPage => TypeResolver.Current.Resolve<ChatStoriesViewModel>(sessionId),

                // Popups
                SettingsUsernamePopup => TypeResolver.Current.Resolve<SettingsUsernameViewModel>(sessionId),
                SettingsDataAutoPopup => TypeResolver.Current.Resolve<SettingsDataAutoViewModel>(sessionId),
                ChooseChatsPopup => TypeResolver.Current.Resolve<ChooseChatsViewModel>(sessionId),
                ChooseSoundPopup => TypeResolver.Current.Resolve<ChooseSoundViewModel>(sessionId),
                ChatNotificationsPopup => TypeResolver.Current.Resolve<ChatNotificationsViewModel>(sessionId),
                CreateChatPhotoPopup => TypeResolver.Current.Resolve<CreateChatPhotoViewModel>(sessionId),
                PromoPopup => TypeResolver.Current.Resolve<PromoViewModel>(sessionId),
                InteractionsPopup => TypeResolver.Current.Resolve<InteractionsViewModel>(sessionId),
                StoryInteractionsPopup => TypeResolver.Current.Resolve<StoryInteractionsViewModel>(sessionId),
                BackgroundsPopup => TypeResolver.Current.Resolve<SettingsBackgroundsViewModel>(sessionId),
                BackgroundPopup backgroundPopup => TypeResolver.Current.Resolve<BackgroundViewModel, IBackgroundDelegate>(backgroundPopup, sessionId),
                _ => null
            };
        }
    }
}
