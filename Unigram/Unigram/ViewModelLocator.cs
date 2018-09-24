using Autofac;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Telegram.Td;
using Template10.Services.ViewService;
using Unigram.Common;
using Unigram.Core.Services;
using Unigram.Services;
using Unigram.ViewModels;
using Unigram.ViewModels.BasicGroups;
using Unigram.ViewModels.Channels;
using Unigram.ViewModels.Chats;
using Unigram.ViewModels.Dialogs;
using Unigram.ViewModels.Passport;
using Unigram.ViewModels.Payments;
using Unigram.ViewModels.SecretChats;
using Unigram.ViewModels.Settings;
using Unigram.ViewModels.Settings.Privacy;
using Unigram.ViewModels.SignIn;
using Unigram.ViewModels.Supergroups;
using Unigram.ViewModels.Users;
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
            Log.SetVerbosityLevel(SettingsService.Current.VerbosityLevel);
            Log.SetFilePath(Path.Combine(ApplicationData.Current.LocalFolder.Path, "log"));

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
                        Task.Factory.StartNew((path) => Directory.Delete((string)path, true), folder);
                    }
                }
            }
        }

        public IContainer Configure(int id)
        {
            return _container.Build(id, (builder, session) =>
            {
                builder.RegisterType<ProtoService>().WithParameter("session", session).As<IProtoService, ICacheService>().SingleInstance();
                builder.RegisterType<SettingsService>().WithParameter("session", session).As<ISettingsService>().SingleInstance();
                builder.RegisterType<NotificationsService>().As<INotificationsService>().SingleInstance().AutoActivate();
                builder.RegisterType<GenerationService>().As<IGenerationService>().SingleInstance().AutoActivate();
                builder.RegisterType<OptionsService>().As<IOptionsService>().SingleInstance().AutoActivate();

                builder.RegisterType<VoIPService>().As<IVoIPService>().SingleInstance();

                //builder.RegisterType<MTProtoService>().WithParameter("account", account).As<IMTProtoService>().SingleInstance();
                builder.RegisterType<DeviceInfoService>().As<IDeviceInfoService>().SingleInstance();
                builder.RegisterType<EventAggregator>().As<IEventAggregator>().SingleInstance();

                builder.RegisterType<ContactsService>().As<IContactsService>().SingleInstance();
                builder.RegisterType<LiveLocationService>().As<ILiveLocationService>().SingleInstance();
                builder.RegisterType<LocationService>().As<ILocationService>().SingleInstance();
                builder.RegisterType<HardwareService>().As<IHardwareService>().SingleInstance();
                builder.RegisterType<PlaybackService>().As<IPlaybackService>().SingleInstance();
                builder.RegisterType<PasscodeService>().As<IPasscodeService>().SingleInstance();
                builder.RegisterType<HockeyAppUpdateService>().As<IHockeyAppUpdateService>().SingleInstance();

                // Disabled due to crashes on Mobile: 
                // The RPC server is unavailable.
                //if (ApiInformation.IsTypePresent("Windows.Devices.Haptics.VibrationDevice") || ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 4))
                //{
                //    // Introduced in Creators Update
                //    container.ContainerBuilder.RegisterType<VibrationService>().As<IVibrationService>().SingleInstance();
                //}
                //else
                if (ApiInformation.IsTypePresent("Windows.Phone.Devices.Notification.VibrationDevice"))
                {
                    // To keep vibration compatibility with Anniversary Update
                    builder.RegisterType<WindowsPhoneVibrationService>().As<IVibrationService>().SingleInstance();
                }
                else
                {
                    builder.RegisterType<FakeVibrationService>().As<IVibrationService>().SingleInstance();
                }

                builder.RegisterType<SessionService>().As<ISessionService>()
                    .WithParameter("session", session)
                    .WithParameter("selected", session == SettingsService.Current.ActiveSession).SingleInstance();

                builder.RegisterType<ViewService>().As<IViewService>();

                // ViewModels
                builder.RegisterType<SignInViewModel>();
                builder.RegisterType<SignUpViewModel>();
                builder.RegisterType<SignInSentCodeViewModel>();
                builder.RegisterType<SignInPasswordViewModel>();
                builder.RegisterType<MainViewModel>().SingleInstance();
                builder.RegisterType<PlaybackViewModel>().SingleInstance();
                builder.RegisterType<ShareViewModel>().SingleInstance();
                builder.RegisterType<DialogShareLocationViewModel>().SingleInstance();
                builder.RegisterType<ChatsViewModel>().SingleInstance();
                builder.RegisterType<DialogViewModel>(); //.WithParameter((a, b) => a.Name == "dispatcher", (a, b) => WindowWrapper.Current().Dispatcher);
                builder.RegisterType<ProfileViewModel>();
                builder.RegisterType<UserCommonChatsViewModel>();
                builder.RegisterType<UserCreateViewModel>();
                builder.RegisterType<SupergroupEventLogViewModel>();
                builder.RegisterType<SupergroupEditViewModel>();// .SingleInstance();
                builder.RegisterType<SupergroupEditStickerSetViewModel>();// .SingleInstance();
                builder.RegisterType<SupergroupEditAdministratorViewModel>();
                builder.RegisterType<SupergroupEditRestrictedViewModel>();
                builder.RegisterType<SupergroupAddAdministratorViewModel>();
                builder.RegisterType<SupergroupAddRestrictedViewModel>();
                builder.RegisterType<BasicGroupEditViewModel>();// .SingleInstance();
                builder.RegisterType<ChatInviteViewModel>();// .SingleInstance();
                builder.RegisterType<ChatInviteLinkViewModel>();// .SingleInstance();
                builder.RegisterType<SupergroupAdministratorsViewModel>();// .SingleInstance();
                builder.RegisterType<SupergroupBannedViewModel>();// .SingleInstance();
                builder.RegisterType<SupergroupRestrictedViewModel>();// .SingleInstance();
                builder.RegisterType<SupergroupMembersViewModel>();// .SingleInstance();
                builder.RegisterType<DialogSharedMediaViewModel>(); // .SingleInstance();
                builder.RegisterType<UsersSelectionViewModel>(); //.SingleInstance();
                builder.RegisterType<ChannelCreateStep1ViewModel>(); //.SingleInstance();
                builder.RegisterType<ChannelCreateStep2ViewModel>(); //.SingleInstance();
                builder.RegisterType<ChannelCreateStep3ViewModel>(); //.SingleInstance();
                builder.RegisterType<ChatCreateStep1ViewModel>(); //.SingleInstance();
                builder.RegisterType<ChatCreateStep2ViewModel>(); //.SingleInstance();
                builder.RegisterType<SecretChatCreateViewModel>();
                builder.RegisterType<InstantViewModel>(); //.SingleInstance();
                builder.RegisterType<SettingsViewModel>().SingleInstance();
                builder.RegisterType<SettingsGeneralViewModel>().SingleInstance();
                builder.RegisterType<SettingsPhoneIntroViewModel>().SingleInstance();
                builder.RegisterType<SettingsPhoneViewModel>().SingleInstance();
                builder.RegisterType<SettingsPhoneSentCodeViewModel>().SingleInstance();
                builder.RegisterType<SettingsStorageViewModel>().SingleInstance();
                builder.RegisterType<SettingsNetworkViewModel>().SingleInstance();
                builder.RegisterType<SettingsUsernameViewModel>().SingleInstance();
                builder.RegisterType<SettingsSessionsViewModel>().SingleInstance();
                builder.RegisterType<SettingsWebSessionsViewModel>().SingleInstance();
                builder.RegisterType<SettingsBlockedUsersViewModel>().SingleInstance();
                builder.RegisterType<SettingsBlockUserViewModel>();
                builder.RegisterType<SettingsNotificationsViewModel>().SingleInstance();
                builder.RegisterType<SettingsDataAndStorageViewModel>().SingleInstance();
                builder.RegisterType<SettingsDataAutoViewModel>().SingleInstance();
                builder.RegisterType<SettingsProxiesViewModel>().SingleInstance();
                builder.RegisterType<SettingsPrivacyAndSecurityViewModel>().SingleInstance();
                builder.RegisterType<SettingsPrivacyAllowCallsViewModel>(); //.SingleInstance();
                builder.RegisterType<SettingsPrivacyAllowChatInvitesViewModel>(); //.SingleInstance();
                builder.RegisterType<SettingsPrivacyShowStatusViewModel>(); //.SingleInstance();
                builder.RegisterType<SettingsPrivacyNeverAllowCallsViewModel>();
                builder.RegisterType<SettingsPrivacyNeverAllowChatInvitesViewModel>();
                builder.RegisterType<SettingsPrivacyNeverShowStatusViewModel>();
                builder.RegisterType<SettingsPrivacyAlwaysAllowCallsViewModel>();
                builder.RegisterType<SettingsPrivacyAlwaysAllowChatInvitesViewModel>();
                builder.RegisterType<SettingsPrivacyAlwaysShowStatusViewModel>();
                builder.RegisterType<SettingsSecurityChangePasswordViewModel>(); //.SingleInstance();
                builder.RegisterType<SettingsSecurityPasscodeViewModel>().SingleInstance();
                builder.RegisterType<SettingsStickersViewModel>().SingleInstance();
                builder.RegisterType<SettingsStickersTrendingViewModel>().SingleInstance();
                builder.RegisterType<SettingsStickersArchivedViewModel>().SingleInstance();
                builder.RegisterType<SettingsMasksViewModel>().SingleInstance();
                builder.RegisterType<SettingsMasksArchivedViewModel>().SingleInstance();
                builder.RegisterType<SettingsWallPaperViewModel>().SingleInstance();
                builder.RegisterType<SettingsAppearanceViewModel>().SingleInstance();
                builder.RegisterType<SettingsLanguageViewModel>().SingleInstance();
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

                return builder.Build();
            });
        }
    }
}
