using Autofac;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unigram.Services;
using Unigram.Services.Factories;
using Unigram.Services.ViewService;
using Unigram.ViewModels;
using Unigram.ViewModels.BasicGroups;
using Unigram.ViewModels.Channels;
using Unigram.ViewModels.Chats;
using Unigram.ViewModels.Drawers;
using Unigram.ViewModels.Folders;
using Unigram.ViewModels.Payments;
using Unigram.ViewModels.Settings;
using Unigram.ViewModels.Settings.Password;
using Unigram.ViewModels.Settings.Privacy;
using Unigram.ViewModels.SignIn;
using Unigram.ViewModels.Supergroups;
using Unigram.ViewModels.Users;
using Unigram.Views;
using Windows.Storage;

namespace Unigram
{
    public class ViewModelLocator
    {
        private TLContainer _container;

        public ViewModelLocator()
        {
            _container = TLContainer.Current;
        }

        public void Configure()
        {
            var fail = true;
            var first = 0;

            foreach (var session in GetSessions())
            {
                if (first < 1 || session == SettingsService.Current.PreviousSession)
                {
                    first = session;
                }

                fail = false;
                Configure(session);
            }

            if (fail)
            {
                Configure(first);
            }

            _container.Lifetime.Update();
        }

        private IEnumerable<int> GetSessions()
        {
            var folders = Directory.GetDirectories(ApplicationData.Current.LocalFolder.Path);
            foreach (var folder in folders)
            {
                if (int.TryParse(Path.GetFileName(folder), out int session))
                {
                    var container = ApplicationData.Current.LocalSettings.CreateContainer($"{session}", ApplicationDataCreateDisposition.Always);
                    if (container.Values.ContainsKey("UserId"))
                    {
                        yield return session;
                    }
                    else
                    {
                        Task.Factory.StartNew((path) =>
                        {
                            try
                            {
                                Directory.Delete((string)path, true);
                            }
                            catch { }
                        }, folder);
                    }
                }
            }
        }

        public IContainer Configure(int id)
        {
            return _container.Build(id, (builder, session) =>
            {
                builder.RegisterType<ProtoService>()
                    .WithParameter("session", session)
                    .WithParameter("online", session == SettingsService.Current.ActiveSession)
                    .As<IProtoService, ICacheService>()
                    .SingleInstance();
                builder.RegisterType<EncryptionService>()
                    .WithParameter("session", session)
                    .As<IEncryptionService>()
                    .SingleInstance();
                builder.RegisterType<SettingsService>()
                    .WithParameter("session", session)
                    .As<ISettingsService>()
                    .SingleInstance();
                builder.RegisterType<SettingsSearchService>()
                    .As<ISettingsSearchService>()
                    .SingleInstance();
                builder.RegisterType<NotificationsService>()
                    .As<INotificationsService>()
                    .SingleInstance()
                    .AutoActivate();
                builder.RegisterType<GenerationService>()
                    .As<IGenerationService>()
                    .SingleInstance()
                    .AutoActivate();
                builder.RegisterType<NetworkService>()
                    .As<INetworkService>()
                    .SingleInstance()
                    .AutoActivate();
                builder.RegisterType<EmojiSetService>()
                    .As<IEmojiSetService>()
                    .SingleInstance();
                builder.RegisterType<CloudUpdateService>()
                    .As<ICloudUpdateService>()
                    .SingleInstance();

                builder.RegisterType<ShortcutsService>()
                    .As<IShortcutsService>()
                    .SingleInstance();
                //builder.RegisterType<OptionsService>()
                //    .As<IOptionsService>()
                //    .SingleInstance()
                //    .AutoActivate();

                builder.RegisterType<VoipService>().As<IVoipService>().SingleInstance();

                //builder.RegisterType<MTProtoService>().WithParameter("account", account).As<IMTProtoService>().SingleInstance();
                builder.RegisterType<DeviceInfoService>().As<IDeviceInfoService>().SingleInstance();
                builder.RegisterType<EventAggregator>().As<IEventAggregator>().SingleInstance();

                builder.RegisterType<ContactsService>().As<IContactsService>().SingleInstance();
                builder.RegisterType<LocationService>().As<ILocationService>().SingleInstance();
                //builder.RegisterType<HardwareService>().As<IHardwareService>().SingleInstance();
                builder.RegisterType<ThemeService>().As<IThemeService>().SingleInstance();

                builder.RegisterType<MessageFactory>().As<IMessageFactory>().SingleInstance();

                //if (ApiInfo.IsMediaSupported)
                {
                    builder.RegisterType<PlaybackService>().As<IPlaybackService>().SingleInstance();
                }
                //else
                //{
                //    builder.RegisterType<DummyPlaybackService>().As<IPlaybackService>().SingleInstance();
                //}

                builder.RegisterType<SessionService>().As<ISessionService>()
                    .WithParameter("session", session)
                    .WithParameter("selected", session == SettingsService.Current.ActiveSession)
                    .SingleInstance();

                builder.RegisterType<ViewService>().As<IViewService>().SingleInstance();

                // ViewModels
                builder.RegisterType<SignInViewModel>();
                builder.RegisterType<SignUpViewModel>();
                builder.RegisterType<SignInSentCodeViewModel>();
                builder.RegisterType<SignInPasswordViewModel>();
                builder.RegisterType<SignInRecoveryViewModel>();
                builder.RegisterType<MainViewModel>();
                builder.RegisterType<ShareViewModel>();
                builder.RegisterType<SendLocationViewModel>().SingleInstance();
                builder.RegisterType<ChatsViewModel>();
                builder.RegisterType<DialogViewModel>(); //.WithParameter((a, b) => a.Name == "dispatcher", (a, b) => WindowWrapper.Current().Dispatcher);
                builder.RegisterType<DialogThreadViewModel>();
                builder.RegisterType<DialogScheduledViewModel>();
                builder.RegisterType<DialogEventLogViewModel>();
                builder.RegisterType<AnimationDrawerViewModel>();
                builder.RegisterType<StickerDrawerViewModel>();
                builder.RegisterType<ProfileViewModel>();
                builder.RegisterType<UserCommonChatsViewModel>();
                builder.RegisterType<UserCreateViewModel>();
                builder.RegisterType<SupergroupEditViewModel>();
                builder.RegisterType<SupergroupEditTypeViewModel>();
                builder.RegisterType<SupergroupEditStickerSetViewModel>();
                builder.RegisterType<SupergroupEditAdministratorViewModel>();
                builder.RegisterType<SupergroupEditRestrictedViewModel>();
                builder.RegisterType<SupergroupEditLinkedChatViewModel>();
                builder.RegisterType<SupergroupAddAdministratorViewModel>();
                builder.RegisterType<SupergroupAddRestrictedViewModel>();
                builder.RegisterType<LiveLocationViewModel>();
                builder.RegisterType<ChatInviteLinkViewModel>();
                builder.RegisterType<SupergroupAdministratorsViewModel>();
                builder.RegisterType<SupergroupBannedViewModel>();
                builder.RegisterType<SupergroupPermissionsViewModel>();
                builder.RegisterType<SupergroupMembersViewModel>();
                builder.RegisterType<ChatSharedMediaViewModel>();
                builder.RegisterType<ChatStatisticsViewModel>();
                builder.RegisterType<ChannelCreateStep1ViewModel>();
                builder.RegisterType<ChannelCreateStep2ViewModel>();
                builder.RegisterType<BasicGroupCreateStep1ViewModel>();
                builder.RegisterType<InstantViewModel>();
                builder.RegisterType<LogOutViewModel>();
                builder.RegisterType<DiagnosticsViewModel>();
                builder.RegisterType<SettingsViewModel>();
                builder.RegisterType<SettingsAdvancedViewModel>();
                builder.RegisterType<SettingsPhoneIntroViewModel>();
                builder.RegisterType<SettingsPhoneViewModel>();
                builder.RegisterType<SettingsPhoneSentCodeViewModel>();
                builder.RegisterType<SettingsStorageViewModel>();
                builder.RegisterType<SettingsNetworkViewModel>();
                builder.RegisterType<SettingsUsernameViewModel>();
                builder.RegisterType<SettingsSessionsViewModel>();
                builder.RegisterType<SettingsWebSessionsViewModel>();
                builder.RegisterType<SettingsBlockedChatsViewModel>();
                builder.RegisterType<SettingsNotificationsViewModel>();
                builder.RegisterType<SettingsNotificationsExceptionsViewModel>();
                builder.RegisterType<SettingsDataAndStorageViewModel>();
                builder.RegisterType<SettingsDataAutoViewModel>();
                builder.RegisterType<SettingsProxiesViewModel>();
                builder.RegisterType<SettingsPrivacyAndSecurityViewModel>();
                builder.RegisterType<SettingsPrivacyAllowCallsViewModel>();
                builder.RegisterType<SettingsPrivacyAllowP2PCallsViewModel>();
                builder.RegisterType<SettingsPrivacyAllowChatInvitesViewModel>();
                builder.RegisterType<SettingsPrivacyShowForwardedViewModel>();
                builder.RegisterType<SettingsPrivacyPhoneViewModel>();
                builder.RegisterType<SettingsPrivacyShowPhoneViewModel>();
                builder.RegisterType<SettingsPrivacyAllowFindingByPhoneNumberViewModel>();
                builder.RegisterType<SettingsPrivacyShowPhotoViewModel>();
                builder.RegisterType<SettingsPrivacyShowStatusViewModel>();
                builder.RegisterType<SettingsPasswordViewModel>();
                builder.RegisterType<SettingsPasswordIntroViewModel>();
                builder.RegisterType<SettingsPasswordCreateViewModel>();
                builder.RegisterType<SettingsPasswordHintViewModel>();
                builder.RegisterType<SettingsPasswordEmailViewModel>();
                builder.RegisterType<SettingsPasswordConfirmViewModel>();
                builder.RegisterType<SettingsPasswordDoneViewModel>();
                builder.RegisterType<SettingsPasscodeViewModel>();
                builder.RegisterType<SettingsStickersViewModel>();
                builder.RegisterType<SettingsStickersTrendingViewModel>();
                builder.RegisterType<SettingsStickersArchivedViewModel>();
                builder.RegisterType<SettingsMasksViewModel>();
                builder.RegisterType<SettingsMasksArchivedViewModel>();
                builder.RegisterType<SettingsLanguageViewModel>();
                builder.RegisterType<SettingsAppearanceViewModel>();
                builder.RegisterType<SettingsThemesViewModel>();
                builder.RegisterType<SettingsNightModeViewModel>();
                builder.RegisterType<SettingsBackgroundsViewModel>();
                builder.RegisterType<SettingsVoIPViewModel>();
                builder.RegisterType<SettingsShortcutsViewModel>();
                builder.RegisterType<BackgroundViewModel>();
                builder.RegisterType<AttachedStickersViewModel>();
                builder.RegisterType<ViewModels.StickerSetViewModel>();
                builder.RegisterType<PaymentFormStep1ViewModel>();
                builder.RegisterType<PaymentFormStep2ViewModel>();
                builder.RegisterType<PaymentFormStep3ViewModel>();
                builder.RegisterType<PaymentFormStep4ViewModel>();
                builder.RegisterType<PaymentFormStep5ViewModel>();
                builder.RegisterType<PaymentReceiptViewModel>();
                builder.RegisterType<InviteViewModel>();
                builder.RegisterType<ChatsNearbyViewModel>();
                builder.RegisterType<FoldersViewModel>();
                builder.RegisterType<FolderViewModel>();

                return builder.Build();
            });
        }
    }
}
