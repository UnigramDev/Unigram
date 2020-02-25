using Autofac;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Telegram.Td;
using Template10.Services.ViewService;
using Unigram.Common;
using Unigram.Services;
using Unigram.Services.Factories;
using Unigram.ViewModels;
using Unigram.ViewModels.BasicGroups;
using Unigram.ViewModels.Channels;
using Unigram.ViewModels.Chats;
using Unigram.ViewModels.Dialogs;
using Unigram.ViewModels.Filters;
using Unigram.ViewModels.Passport;
using Unigram.ViewModels.Payments;
using Unigram.ViewModels.Settings;
using Unigram.ViewModels.Settings.Password;
using Unigram.ViewModels.Settings.Privacy;
using Unigram.ViewModels.SignIn;
using Unigram.ViewModels.Supergroups;
using Unigram.ViewModels.Users;
#if INCLUDE_WALLET
using Unigram.ViewModels.Wallet;
#endif
using Unigram.Views;
using Windows.Foundation.Metadata;
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
#if INCLUDE_WALLET
                builder.RegisterType<TonService>()
                    .WithParameter("session", session)
                    .As<ITonService>()
                    .SingleInstance();
#endif
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
                    .SingleInstance()
                    .AutoActivate();
                //builder.RegisterType<OptionsService>()
                //    .As<IOptionsService>()
                //    .SingleInstance()
                //    .AutoActivate();

                builder.RegisterType<VoIPService>().As<IVoIPService>().SingleInstance();

                //builder.RegisterType<MTProtoService>().WithParameter("account", account).As<IMTProtoService>().SingleInstance();
                builder.RegisterType<DeviceInfoService>().As<IDeviceInfoService>().SingleInstance();
                builder.RegisterType<EventAggregator>().As<IEventAggregator>().SingleInstance();

                builder.RegisterType<ContactsService>().As<IContactsService>().SingleInstance();
                builder.RegisterType<LiveLocationService>().As<ILiveLocationService>().SingleInstance();
                builder.RegisterType<LocationService>().As<ILocationService>().SingleInstance();
                //builder.RegisterType<HardwareService>().As<IHardwareService>().SingleInstance();
                builder.RegisterType<PlaybackService>().As<IPlaybackService>().SingleInstance();
                builder.RegisterType<HockeyUpdateService>().As<IHockeyUpdateService>().SingleInstance();
                builder.RegisterType<ThemeService>().As<IThemeService>().SingleInstance();

                builder.RegisterType<MessageFactory>().As<IMessageFactory>().SingleInstance();

                // Disabled due to crashes on Mobile: 
                // The RPC server is unavailable.
                //if (ApiInformation.IsTypePresent("Windows.Devices.Haptics.VibrationDevice") || ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 4))
                //{
                //    // Introduced in Creators Update
                //    container.ContainerBuilder.RegisterType<VibrationService>().As<IVibrationService>().SingleInstance();
                //}
                //else
                //if (ApiInformation.IsTypePresent("Windows.Phone.Devices.Notification.VibrationDevice"))
                //{
                //    // To keep vibration compatibility with Anniversary Update
                //    builder.RegisterType<WindowsPhoneVibrationService>().As<IVibrationService>().SingleInstance();
                //}
                //else
                {
                    builder.RegisterType<FakeVibrationService>().As<IVibrationService>().SingleInstance();
                }

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
                builder.RegisterType<MainViewModel>();//.SingleInstance();
                builder.RegisterType<ShareViewModel>();//.SingleInstance();
                builder.RegisterType<SendLocationViewModel>().SingleInstance();
                builder.RegisterType<ChatsViewModel>();//.SingleInstance();
                builder.RegisterType<DialogViewModel>(); //.WithParameter((a, b) => a.Name == "dispatcher", (a, b) => WindowWrapper.Current().Dispatcher);
                builder.RegisterType<DialogScheduledViewModel>();
                builder.RegisterType<ProfileViewModel>();
                builder.RegisterType<UserCommonChatsViewModel>();
                builder.RegisterType<UserCreateViewModel>();
                builder.RegisterType<SupergroupEventLogViewModel>();
                builder.RegisterType<SupergroupEditViewModel>();// .SingleInstance();
                builder.RegisterType<SupergroupEditTypeViewModel>();// .SingleInstance();
                builder.RegisterType<SupergroupEditStickerSetViewModel>();// .SingleInstance();
                builder.RegisterType<SupergroupEditAdministratorViewModel>();
                builder.RegisterType<SupergroupEditRestrictedViewModel>();
                builder.RegisterType<SupergroupEditLinkedChatViewModel>();
                builder.RegisterType<SupergroupAddAdministratorViewModel>();
                builder.RegisterType<SupergroupAddRestrictedViewModel>();
                builder.RegisterType<LiveLocationViewModel>();
                builder.RegisterType<ChatInviteLinkViewModel>();// .SingleInstance();
                builder.RegisterType<SupergroupAdministratorsViewModel>();// .SingleInstance();
                builder.RegisterType<SupergroupBannedViewModel>();// .SingleInstance();
                builder.RegisterType<SupergroupPermissionsViewModel>();// .SingleInstance();
                builder.RegisterType<SupergroupMembersViewModel>();// .SingleInstance();
                builder.RegisterType<ChatSharedMediaViewModel>(); // .SingleInstance();
                builder.RegisterType<ChannelCreateStep1ViewModel>(); //.SingleInstance();
                builder.RegisterType<ChannelCreateStep2ViewModel>(); //.SingleInstance();
                builder.RegisterType<BasicGroupCreateStep1ViewModel>(); //.SingleInstance();
                builder.RegisterType<InstantViewModel>(); //.SingleInstance();
                builder.RegisterType<LogOutViewModel>().SingleInstance();
                builder.RegisterType<SettingsViewModel>();//.SingleInstance();
                builder.RegisterType<SettingsAdvancedViewModel>().SingleInstance();
                builder.RegisterType<SettingsPhoneIntroViewModel>().SingleInstance();
                builder.RegisterType<SettingsPhoneViewModel>().SingleInstance();
                builder.RegisterType<SettingsPhoneSentCodeViewModel>().SingleInstance();
                builder.RegisterType<SettingsStorageViewModel>().SingleInstance();
                builder.RegisterType<SettingsNetworkViewModel>().SingleInstance();
                builder.RegisterType<SettingsUsernameViewModel>().SingleInstance();
                builder.RegisterType<SettingsSessionsViewModel>();
                builder.RegisterType<SettingsWebSessionsViewModel>();
                builder.RegisterType<SettingsBlockedUsersViewModel>();
                builder.RegisterType<SettingsNotificationsViewModel>().SingleInstance();
                builder.RegisterType<SettingsNotificationsExceptionsViewModel>();
                builder.RegisterType<SettingsDataAndStorageViewModel>().SingleInstance();
                builder.RegisterType<SettingsDataAutoViewModel>().SingleInstance();
                builder.RegisterType<SettingsProxiesViewModel>().SingleInstance();
                builder.RegisterType<SettingsPrivacyAndSecurityViewModel>().SingleInstance();
                builder.RegisterType<SettingsPrivacyAllowCallsViewModel>(); //.SingleInstance();
                builder.RegisterType<SettingsPrivacyAllowP2PCallsViewModel>(); //.SingleInstance();
                builder.RegisterType<SettingsPrivacyAllowChatInvitesViewModel>(); //.SingleInstance();
                builder.RegisterType<SettingsPrivacyShowForwardedViewModel>(); //.SingleInstance();
                builder.RegisterType<SettingsPrivacyPhoneViewModel>(); //.SingleInstance();
                builder.RegisterType<SettingsPrivacyShowPhoneViewModel>(); //.SingleInstance();
                builder.RegisterType<SettingsPrivacyAllowFindingByPhoneNumberViewModel>(); //.SingleInstance();
                builder.RegisterType<SettingsPrivacyShowPhotoViewModel>(); //.SingleInstance();
                builder.RegisterType<SettingsPrivacyShowStatusViewModel>(); //.SingleInstance();
                builder.RegisterType<SettingsPasswordViewModel>(); //.SingleInstance();
                builder.RegisterType<SettingsPasswordIntroViewModel>(); //.SingleInstance();
                builder.RegisterType<SettingsPasswordCreateViewModel>(); //.SingleInstance();
                builder.RegisterType<SettingsPasswordHintViewModel>(); //.SingleInstance();
                builder.RegisterType<SettingsPasswordEmailViewModel>(); //.SingleInstance();
                builder.RegisterType<SettingsPasswordConfirmViewModel>(); //.SingleInstance();
                builder.RegisterType<SettingsPasswordDoneViewModel>(); //.SingleInstance();
                builder.RegisterType<SettingsPasscodeViewModel>().SingleInstance();
                builder.RegisterType<SettingsStickersViewModel>().SingleInstance();
                builder.RegisterType<SettingsStickersTrendingViewModel>().SingleInstance();
                builder.RegisterType<SettingsStickersArchivedViewModel>();
                builder.RegisterType<SettingsMasksViewModel>().SingleInstance();
                builder.RegisterType<SettingsMasksArchivedViewModel>();
                builder.RegisterType<SettingsLanguageViewModel>();//.SingleInstance();
                builder.RegisterType<SettingsAppearanceViewModel>().SingleInstance();
                builder.RegisterType<SettingsThemesViewModel>().SingleInstance();
                builder.RegisterType<SettingsNightModeViewModel>().SingleInstance();
                builder.RegisterType<SettingsBackgroundsViewModel>();//.SingleInstance();
                builder.RegisterType<SettingsVoIPViewModel>();
                builder.RegisterType<BackgroundViewModel>();
                builder.RegisterType<AttachedStickersViewModel>();
                builder.RegisterType<ViewModels.StickerSetViewModel>();
                builder.RegisterType<PaymentFormStep1ViewModel>();
                builder.RegisterType<PaymentFormStep2ViewModel>();
                builder.RegisterType<PaymentFormStep3ViewModel>();
                builder.RegisterType<PaymentFormStep4ViewModel>();
                builder.RegisterType<PaymentFormStep5ViewModel>();
                builder.RegisterType<PaymentReceiptViewModel>();
                builder.RegisterType<PassportViewModel>();
                builder.RegisterType<PassportDocumentViewModelBase>();
                builder.RegisterType<PassportAddressViewModel>();
                builder.RegisterType<InviteViewModel>();
                builder.RegisterType<ChatsNearbyViewModel>();
                builder.RegisterType<FiltersViewModel>();
                builder.RegisterType<FilterViewModel>();

#if INCLUDE_WALLET
                builder.RegisterType<WalletViewModel>();
                builder.RegisterType<WalletSettingsViewModel>();
                builder.RegisterType<WalletCreateViewModel>();
                builder.RegisterType<WalletTestViewModel>();
                builder.RegisterType<WalletImportViewModel>();
                builder.RegisterType<WalletExportViewModel>();
                builder.RegisterType<WalletReceiveViewModel>();
                builder.RegisterType<WalletInvoiceViewModel>();
                builder.RegisterType<WalletSendViewModel>();
                builder.RegisterType<WalletSendingViewModel>();
                builder.RegisterType<WalletTransactionViewModel>();
                builder.RegisterType<WalletInfoViewModel>();
#endif

                return builder.Build();
            });
        }
    }
}
