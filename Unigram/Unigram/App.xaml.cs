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

        public override INavigable ResolveForPage(Page page, INavigationService navigationService)
        {
            var id = navigationService is UnigramNavigationService ex ? ex.SessionId : 0;
            switch (page)
            {
                case BasicGroupEditPage basicGroupEdit:
                    return UnigramContainer.Current.ResolveType<BasicGroupEditViewModel, IBasicGroupDelegate>(basicGroupEdit, id);
                case ChannelCreateStep1Page channelCreateStep1:
                    return UnigramContainer.Current.ResolveType<ChannelCreateStep1ViewModel>(id);
                case ChannelCreateStep2Page channelCreateStep2:
                    return UnigramContainer.Current.ResolveType<ChannelCreateStep2ViewModel>(id);
                case ChannelCreateStep3Page channelCreateStep3:
                    return UnigramContainer.Current.ResolveType<ChannelCreateStep3ViewModel>(id);
                case ChatCreateStep1Page chatCreateStep1:
                    return UnigramContainer.Current.ResolveType<ChatCreateStep1ViewModel>(id);
                case ChatCreateStep2Page chatCreateStep2:
                    return UnigramContainer.Current.ResolveType<ChatCreateStep2ViewModel>(id);
                case ChatInviteLinkPage chatInviteLink:
                    return UnigramContainer.Current.ResolveType<ChatInviteLinkViewModel>(id);
                case ChatInvitePage chatInvite:
                    return UnigramContainer.Current.ResolveType<ChatInviteViewModel>(id);
                case DialogSharedMediaPage dialogSharedMedia:
                    return UnigramContainer.Current.ResolveType<DialogSharedMediaViewModel, IFileDelegate>(dialogSharedMedia, id);
                case DialogShareLocationPage dialogShareLocation:
                    return UnigramContainer.Current.ResolveType<DialogShareLocationViewModel>(id);
                case InstantPage instant:
                    return UnigramContainer.Current.ResolveType<InstantViewModel>(id);
                case MainPage main:
                    return UnigramContainer.Current.ResolveType<MainViewModel>(id);
                case PaymentFormStep1Page paymentFormStep1:
                    return UnigramContainer.Current.ResolveType<PaymentFormStep1ViewModel>(id);
                case PaymentFormStep2Page paymentFormStep2:
                    return UnigramContainer.Current.ResolveType<PaymentFormStep2ViewModel>(id);
                case PaymentFormStep3Page paymentFormStep3:
                    return UnigramContainer.Current.ResolveType<PaymentFormStep3ViewModel>(id);
                case PaymentFormStep4Page paymentFormStep4:
                    return UnigramContainer.Current.ResolveType<PaymentFormStep4ViewModel>(id);
                case PaymentFormStep5Page paymentFormStep5:
                    return UnigramContainer.Current.ResolveType<PaymentFormStep5ViewModel>(id);
                case PaymentReceiptPage paymentReceipt:
                    return UnigramContainer.Current.ResolveType<PaymentReceiptViewModel>(id);
                case ProfilePage profile:
                    return UnigramContainer.Current.ResolveType<ProfileViewModel, IProfileDelegate>(profile, id);
                case SecretChatCreatePage secretChatCreate:
                    return UnigramContainer.Current.ResolveType<SecretChatCreateViewModel>(id);
                case SettingsPhoneIntroPage settingsPhoneIntro:
                    return UnigramContainer.Current.ResolveType<SettingsPhoneIntroViewModel>(id);
                case SettingsPhonePage settingsPhone:
                    return UnigramContainer.Current.ResolveType<SettingsPhoneViewModel>(id);
                case SettingsPhoneSentCodePage settingsPhoneSentCode:
                    return UnigramContainer.Current.ResolveType<SettingsPhoneSentCodeViewModel>(id);
                case SettingsPrivacyAllowCallsPage settingsPrivacyAllowCalls:
                    return UnigramContainer.Current.ResolveType<SettingsPrivacyAllowCallsViewModel>(id);
                case SettingsPrivacyAllowChatInvitesPage settingsPrivacyAllowChatInvites:
                    return UnigramContainer.Current.ResolveType<SettingsPrivacyAllowChatInvitesViewModel>(id);
                case SettingsPrivacyAlwaysAllowCallsPage settingsPrivacyAlwaysAllowCalls:
                    return UnigramContainer.Current.ResolveType<SettingsPrivacyAlwaysAllowCallsViewModel>(id);
                case SettingsPrivacyAlwaysAllowChatInvitesPage settingsPrivacyAlwaysAllowChatInvites:
                    return UnigramContainer.Current.ResolveType<SettingsPrivacyAlwaysAllowChatInvitesViewModel>(id);
                case SettingsPrivacyAlwaysShowStatusPage settingsPrivacyAlwaysShowStatus:
                    return UnigramContainer.Current.ResolveType<SettingsPrivacyAlwaysShowStatusViewModel>(id);
                case SettingsPrivacyNeverAllowCallsPage settingsPrivacyNeverAllowCalls:
                    return UnigramContainer.Current.ResolveType<SettingsPrivacyNeverAllowCallsViewModel>(id);
                case SettingsPrivacyNeverAllowChatInvitesPage settingsPrivacyNeverAllowChatInvites:
                    return UnigramContainer.Current.ResolveType<SettingsPrivacyNeverAllowChatInvitesViewModel>(id);
                case SettingsPrivacyNeverShowStatusPage settingsPrivacyNeverShowStatus:
                    return UnigramContainer.Current.ResolveType<SettingsPrivacyNeverShowStatusViewModel>(id);
                case SettingsPrivacyShowStatusPage settingsPrivacyShowStatus:
                    return UnigramContainer.Current.ResolveType<SettingsPrivacyShowStatusViewModel>(id);
                case SettingsAppearancePage settingsAppearance:
                    return UnigramContainer.Current.ResolveType<SettingsAppearanceViewModel>(id);
                case SettingsBlockedUsersPage settingsBlockedUsers:
                    return UnigramContainer.Current.ResolveType<SettingsBlockedUsersViewModel, IFileDelegate>(settingsBlockedUsers, id);
                case SettingsBlockUserPage settingsBlockUser:
                    return UnigramContainer.Current.ResolveType<SettingsBlockUserViewModel>(id);
                case SettingsDataAndStoragePage settingsDataAndStorage:
                    return UnigramContainer.Current.ResolveType<SettingsDataAndStorageViewModel>(id);
                case SettingsDataAutoPage settingsDataAuto:
                    return UnigramContainer.Current.ResolveType<SettingsDataAutoViewModel>(id);
                case SettingsGeneralPage settingsGeneral:
                    return UnigramContainer.Current.ResolveType<SettingsGeneralViewModel>(id);
                case SettingsLanguagePage settingsLanguage:
                    return UnigramContainer.Current.ResolveType<SettingsLanguageViewModel>(id);
                case SettingsMasksArchivedPage settingsMasksArchived:
                    return UnigramContainer.Current.ResolveType<SettingsMasksArchivedViewModel>(id);
                case SettingsMasksPage settingsMasks:
                    return UnigramContainer.Current.ResolveType<SettingsMasksViewModel>(id);
                case SettingsNotificationsPage settingsNotifications:
                    return UnigramContainer.Current.ResolveType<SettingsNotificationsViewModel>(id);
                case SettingsPrivacyAndSecurityPage settingsPrivacyAndSecurity:
                    return UnigramContainer.Current.ResolveType<SettingsPrivacyAndSecurityViewModel>(id);
                case SettingsSecurityChangePasswordPage settingsSecurityChangePassword:
                    return UnigramContainer.Current.ResolveType<SettingsSecurityChangePasswordViewModel>(id);
                case SettingsSecurityEnterPasswordPage settingsSecurityEnterPassword:
                    return UnigramContainer.Current.ResolveType<SettingsSecurityEnterPasswordViewModel>(id);
                case SettingsSecurityPasscodePage settingsSecurityPasscode:
                    return UnigramContainer.Current.ResolveType<SettingsSecurityPasscodeViewModel>(id);
                case SettingsSessionsPage settingsSessions:
                    return UnigramContainer.Current.ResolveType<SettingsSessionsViewModel>(id);
                case SettingsStatsPage settingsNetwork:
                    return UnigramContainer.Current.ResolveType<SettingsNetworkViewModel>(id);
                case SettingsStickersArchivedPage settingsStickersArchived:
                    return UnigramContainer.Current.ResolveType<SettingsStickersArchivedViewModel>(id);
                case SettingsStickersFeaturedPage settingsStickersTrending:
                    return UnigramContainer.Current.ResolveType<SettingsStickersTrendingViewModel>(id);
                case SettingsStickersPage settingsStickers:
                    return UnigramContainer.Current.ResolveType<SettingsStickersViewModel>(id);
                case SettingsStoragePage settingsStorage:
                    return UnigramContainer.Current.ResolveType<SettingsStorageViewModel>(id);
                case SettingsUsernamePage settingsUsername:
                    return UnigramContainer.Current.ResolveType<SettingsUsernameViewModel>(id);
                case SettingsWallPaperPage settingsWallPaper:
                    return UnigramContainer.Current.ResolveType<SettingsWallPaperViewModel>(id);
                case SettingsWebSessionsPage settingsWebSessions:
                    return UnigramContainer.Current.ResolveType<SettingsWebSessionsViewModel>(id);
                case SettingsPage settings:
                    return UnigramContainer.Current.ResolveType<SettingsViewModel, IUserDelegate>(settings, id);
                case SignInPage signIn:
                    return UnigramContainer.Current.ResolveType<SignInViewModel>(id);
                case SignInPasswordPage signInPassword:
                    return UnigramContainer.Current.ResolveType<SignInPasswordViewModel>(id);
                case SignInSentCodePage signInSentCode:
                    return UnigramContainer.Current.ResolveType<SignInSentCodeViewModel>(id);
                case SignUpPage signUp:
                    return UnigramContainer.Current.ResolveType<SignUpViewModel>(id);
                case SupergroupAddAdministratorPage supergroupAddAdministrator:
                    return UnigramContainer.Current.ResolveType<SupergroupAddAdministratorViewModel>(id);
                case SupergroupAddRestrictedPage supergroupAddRestricted:
                    return UnigramContainer.Current.ResolveType<SupergroupAddRestrictedViewModel>(id);
                case SupergroupAdministratorsPage supergroupAdministrators:
                    return UnigramContainer.Current.ResolveType<SupergroupAdministratorsViewModel, ISupergroupDelegate>(supergroupAdministrators, id);
                case SupergroupBannedPage supergroupBanned:
                    return UnigramContainer.Current.ResolveType<SupergroupBannedViewModel, ISupergroupDelegate>(supergroupBanned, id);
                case SupergroupEditAdministratorPage supergroupEditAdministrator:
                    return UnigramContainer.Current.ResolveType<SupergroupEditAdministratorViewModel, IMemberDelegate>(supergroupEditAdministrator, id);
                case SupergroupEditPage supergroupEdit:
                    return UnigramContainer.Current.ResolveType<SupergroupEditViewModel, ISupergroupDelegate>(supergroupEdit, id);
                case SupergroupEditRestrictedPage supergroupEditRestricted:
                    return UnigramContainer.Current.ResolveType<SupergroupEditRestrictedViewModel, IMemberDelegate>(supergroupEditRestricted, id);
                case SupergroupEditStickerSetPage supergroupEditStickerSet:
                    return UnigramContainer.Current.ResolveType<SupergroupEditStickerSetViewModel>(id);
                case SupergroupEventLogPage supergroupEventLog:
                    return UnigramContainer.Current.ResolveType<SupergroupEventLogViewModel>(id);
                case SupergroupMembersPage supergroupMembers:
                    return UnigramContainer.Current.ResolveType<SupergroupMembersViewModel, ISupergroupDelegate>(supergroupMembers, id);
                case SupergroupRestrictedPage supergroupRestricted:
                    return UnigramContainer.Current.ResolveType<SupergroupRestrictedViewModel, ISupergroupDelegate>(supergroupRestricted, id);
                case UserCommonChatsPage userCommonChats:
                    return UnigramContainer.Current.ResolveType<UserCommonChatsViewModel>(id);
                case UserCreatePage userCreate:
                    return UnigramContainer.Current.ResolveType<UserCreateViewModel>(id);
            }

            return base.ResolveForPage(page, navigationService);
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
