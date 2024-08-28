//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Telegram.Services;
using Telegram.Services.Factories;
using Telegram.Services.ViewService;
using Telegram.ViewModels;
using Telegram.ViewModels.Authorization;
using Telegram.ViewModels.BasicGroups;
using Telegram.ViewModels.Business;
using Telegram.ViewModels.Channels;
using Telegram.ViewModels.Chats;
using Telegram.ViewModels.Drawers;
using Telegram.ViewModels.Folders;
using Telegram.ViewModels.Payments;
using Telegram.ViewModels.Premium;
using Telegram.ViewModels.Profile;
using Telegram.ViewModels.Settings;
using Telegram.ViewModels.Settings.Privacy;
using Telegram.ViewModels.Stars;
using Telegram.ViewModels.Supergroups;
using Telegram.ViewModels.Users;

namespace Telegram
{
    public partial class TypeContainerGenerator
    {
        private static List<Type> _globals;
        private static List<(Type Key, Type Value)> _singletons;
        private static List<(Type Key, Type Value)> _lazySingletons;
        private static List<Type> _instances;

        [Conditional("DEBUG")]
        public static void Generate()
        {
            _globals = new List<Type>
            {
                typeof(ILifetimeService),
                typeof(ILocaleService),
                typeof(IPasscodeService),
                typeof(IPlaybackService)
            };

            _singletons = new List<(Type, Type)>
            {
                ( typeof(IClientService), typeof(ClientService) ),
                ( typeof(INotificationsService), typeof(NotificationsService) ),
                ( typeof(IGenerationService), typeof(GenerationService) ),
                ( typeof(INetworkService), typeof(NetworkService) ),
                ( typeof(IVoipService), typeof(VoipService) ),
            };

            _lazySingletons = new List<(Type, Type)>
            {
                ( typeof(ISettingsService), typeof(SettingsService) ),
                ( typeof(ISettingsSearchService), typeof(SettingsSearchService) ),
                ( typeof(ICloudUpdateService), typeof(CloudUpdateService) ),
                ( typeof(IShortcutsService), typeof(ShortcutsService) ),
                ( typeof(IVoipGroupService), typeof(VoipGroupService) ),
                ( typeof(IDeviceInfoService), typeof(DeviceInfoService) ),
                ( typeof(IEventAggregator), typeof(EventAggregator) ),
                ( typeof(IContactsService), typeof(ContactsService) ),
                ( typeof(ILocationService), typeof(LocationService) ),
                ( typeof(IThemeService), typeof(ThemeService) ),
                ( typeof(IMessageFactory), typeof(MessageFactory) ),
                ( typeof(IViewService), typeof(ViewService) ),
                ( typeof(ISessionService), typeof(SessionService) ),
                ( typeof(IStorageService), typeof(StorageService) ),
                ( typeof(ITranslateService), typeof(TranslateService) ),
                ( typeof(IProfilePhotoService), typeof(ProfilePhotoService) ),
            };

            _instances = new List<Type>
            {
                typeof(AuthorizationViewModel),
                typeof(AuthorizationRegistrationViewModel),
                typeof(AuthorizationCodeViewModel),
                typeof(AuthorizationPasswordViewModel),
                typeof(AuthorizationRecoveryViewModel),
                typeof(AuthorizationEmailAddressViewModel),
                typeof(AuthorizationEmailCodeViewModel),
                typeof(MainViewModel),
                typeof(ChooseChatsViewModel),
                typeof(SendLocationViewModel),
                typeof(DialogViewModel),
                typeof(DialogThreadViewModel),
                typeof(DialogBusinessRepliesViewModel),
                typeof(DialogSavedViewModel),
                typeof(DialogPinnedViewModel),
                typeof(DialogScheduledViewModel),
                typeof(DialogEventLogViewModel),
                typeof(AnimationDrawerViewModel),
                typeof(StickerDrawerViewModel),
                typeof(EmojiDrawerViewModel),
                typeof(EffectDrawerViewModel),
                typeof(CreateChatPhotoViewModel),
                typeof(ProfileViewModel),
                typeof(ProfileStoriesTabViewModel),
                typeof(ProfileMembersTabViewModel),
                typeof(ProfileGroupsTabViewModel),
                typeof(ProfileChannelsTabViewModel),
                typeof(ProfileSavedChatsTabViewModel),
                typeof(UserCreateViewModel),
                typeof(UserEditViewModel),
                typeof(SupergroupEditViewModel),
                typeof(SupergroupEditTypeViewModel),
                typeof(SupergroupEditStickerSetViewModel),
                typeof(SupergroupEditAdministratorViewModel),
                typeof(SupergroupEditRestrictedViewModel),
                typeof(SupergroupEditLinkedChatViewModel),
                typeof(SupergroupChooseMemberViewModel),
                typeof(ChatInviteLinkViewModel),
                typeof(SupergroupAdministratorsViewModel),
                typeof(SupergroupBannedViewModel),
                typeof(SupergroupPermissionsViewModel),
                typeof(SupergroupMembersViewModel),
                typeof(SupergroupReactionsViewModel),
                typeof(ChatStatisticsViewModel),
                typeof(ChatBoostsViewModel),
                typeof(ChatRevenueViewModel),
                typeof(MessageStatisticsViewModel),
                typeof(ChannelCreateStep1ViewModel),
                typeof(ChannelCreateStep2ViewModel),
                typeof(BasicGroupCreateStep1ViewModel),
                typeof(InstantViewModel),
                typeof(LogOutViewModel),
                typeof(DiagnosticsViewModel),
                typeof(ChatStoriesViewModel),
                typeof(SettingsViewModel),
                typeof(SettingsAdvancedViewModel),
                typeof(SettingsStorageViewModel),
                typeof(SettingsNetworkViewModel),
                typeof(SettingsUsernameViewModel),
                typeof(SettingsSessionsViewModel),
                typeof(SettingsWebSessionsViewModel),
                typeof(SettingsBlockedChatsViewModel),
                typeof(SettingsNotificationsViewModel),
                typeof(SettingsNotificationsExceptionsViewModel),
                typeof(SettingsDataAndStorageViewModel),
                typeof(SettingsDataAutoViewModel),
                typeof(SettingsProxyViewModel),
                typeof(SettingsQuickReactionViewModel),
                typeof(SettingsPrivacyAndSecurityViewModel),
                typeof(SettingsPrivacyAllowCallsViewModel),
                typeof(SettingsPrivacyAllowP2PCallsViewModel),
                typeof(SettingsPrivacyAllowChatInvitesViewModel),
                typeof(SettingsPrivacyShowForwardedViewModel),
                typeof(SettingsPrivacyPhoneViewModel),
                typeof(SettingsPrivacyShowPhoneViewModel),
                typeof(SettingsPrivacyAllowFindingByPhoneNumberViewModel),
                typeof(SettingsPrivacyAllowPrivateVoiceAndVideoNoteMessagesViewModel),
                typeof(SettingsPrivacyShowPhotoViewModel),
                typeof(SettingsPrivacyShowStatusViewModel),
                typeof(SettingsPrivacyShowBioViewModel),
                typeof(SettingsPrivacyShowBirthdateViewModel),
                typeof(SettingsPrivacyNewChatViewModel),
                typeof(SettingsAutoDeleteViewModel),
                typeof(SettingsProfileViewModel),
                typeof(SettingsProfileColorViewModel),
                typeof(SupergroupProfileColorViewModel),
                typeof(SettingsPasswordViewModel),
                typeof(SettingsPasscodeViewModel),
                typeof(SettingsStickersViewModel),
                typeof(SettingsLanguageViewModel),
                typeof(SettingsAppearanceViewModel),
                typeof(SettingsThemesViewModel),
                typeof(SettingsThemeViewModel),
                typeof(SettingsNightModeViewModel),
                typeof(SettingsBackgroundsViewModel),
                typeof(SettingsShortcutsViewModel),
                typeof(SettingsPowerSavingViewModel),
                typeof(BackgroundViewModel),
                typeof(StickersViewModel),
                typeof(PaymentAddressViewModel),
                typeof(PaymentCredentialsViewModel),
                typeof(PaymentFormViewModel),
                typeof(InteractionsViewModel),
                typeof(StoryInteractionsViewModel),
                typeof(ChatsNearbyViewModel),
                typeof(FoldersViewModel),
                typeof(FolderViewModel),
                typeof(ShareFolderViewModel),
                typeof(AddFolderViewModel),
                typeof(RemoveFolderViewModel),
                typeof(DownloadsViewModel),
                typeof(ChooseSoundViewModel),
                typeof(ChatNotificationsViewModel),
                typeof(PromoViewModel),
                typeof(StarsViewModel),
                typeof(BuyViewModel),
                typeof(PayViewModel),
                typeof(BusinessViewModel),
                typeof(BusinessLocationViewModel),
                typeof(BusinessHoursViewModel),
                typeof(BusinessRepliesViewModel),
                typeof(BusinessGreetViewModel),
                typeof(BusinessAwayViewModel),
                typeof(BusinessBotsViewModel),
                typeof(BusinessIntroViewModel),
                typeof(BusinessChatLinksViewModel),
                typeof(RevenueViewModel)
            };

            // Preprocess: find out lazy singletons used by singletons to promote
            for (int i = 0; i < _singletons.Count; i++)
            {
                var singleton = _singletons[i];
                var ctor = singleton.Value.GetConstructors().FirstOrDefault();
                var args = ctor?.GetParameters();

                if (args == null || args.Length < 1)
                {
                    continue;
                }

                for (int j = 0; j < args.Length; j++)
                {
                    var lazy = _lazySingletons.FirstOrDefault(x => x.Key == args[j].ParameterType);
                    if (lazy != default)
                    {
                        _singletons.Insert(0, lazy);
                        _lazySingletons.Remove(lazy);

                        i++;
                    }
                }
            }

            // Sort singletons by dependency
            _singletons.Sort((x, y) =>
            {
                var ctor = x.Value.GetConstructors().FirstOrDefault();
                var args = ctor?.GetParameters();

                if (args.Any(k => k.ParameterType == y.Key))
                {
                    return 1;
                }

                return -1;
            });

            var singletonBucket = new Dictionary<Type, Type>();

            var builder = new FormattedBuilder();
            builder.AppendLine("namespace Telegram.Views");
            builder.AppendLine("{");
            builder.AppendLine("public partial class TypeLocator");
            builder.AppendLine("{");
            builder.AppendLine("private readonly int _session;");
            builder.AppendLine();

            for (int i = 0; i < _globals.Count; i++)
            {
                var singleton = _globals.ElementAt(i);
                builder.AppendLine("private readonly " + singleton.FullName + " " + GetSingletonName(singleton) + ";");
            }

            builder.AppendLine();

            for (int i = 0; i < _singletons.Count; i++)
            {
                var singleton = _singletons[i];
                builder.AppendLine("private readonly " + singleton.Key.FullName + " " + GetSingletonName(singleton.Key) + ";");
            }

            builder.AppendLine();

            for (int i = 0; i < _lazySingletons.Count; i++)
            {
                var singleton = _lazySingletons[i];
                builder.AppendLine("private " + singleton.Key.FullName + " " + GetSingletonName(singleton.Key) + ";");
            }

            builder.AppendLine();

            builder.AppendIndent("public TypeLocator(");

            for (int i = 0; i < _globals.Count; i++)
            {
                var singleton = _globals[i];
                builder.Append(singleton.FullName + " " + GetSingletonName(singleton, false) + ", ");
            }

            builder.Append("int session, bool active)\r\n");
            builder.AppendLine("{");

            builder.AppendLine("_session = session;");
            builder.AppendLine();

            for (int i = 0; i < _globals.Count; i++)
            {
                var singleton = _globals[i];
                builder.AppendLine(GetSingletonName(singleton) + " = " + GetSingletonName(singleton, false) + ";");
            }

            builder.AppendLine();

            for (int i = 0; i < _singletons.Count; i++)
            {
                var singleton = _singletons[i];
                if (singletonBucket.TryGetValue(singleton.Value, out Type baseSingleton))
                {
                    builder.AppendLine(GetSingletonName(singleton.Key) + " = " + GetSingletonName(baseSingleton) + ";");
                }
                else
                {
                    builder.AppendLine(GetSingletonName(singleton.Key) + " = " + GenerateConstructor(singleton.Value, 3) + ";");
                }

                singletonBucket[singleton.Value] = singleton.Key;
            }

            builder.AppendLine("}");
            builder.AppendLine();

            builder.AppendLine("public T Resolve<T>()");
            builder.AppendLine("{");
            builder.AppendLine("switch (typeof(T).FullName)");
            builder.AppendLine("{");

            for (int i = 0; i < _instances.Count; i++)
            {
                builder.AppendLine("case \"" + _instances[i].FullName + "\":");
                builder.AppendLine("return (T)(object)" + GenerateConstructor(_instances[i], 4) + "; ");
            }

            for (int i = 0; i < _singletons.Count; i++)
            {
                builder.AppendLine("case \"" + _singletons[i].Key.FullName + "\":");
                builder.AppendLine("return (T)" + GetSingletonName(_singletons[i].Key) + "; ");
            }

            for (int i = 0; i < _lazySingletons.Count; i++)
            {
                builder.AppendLine("case \"" + _lazySingletons[i].Key.FullName + "\":");
                builder.AppendLine("return (T)(" + GetSingletonName(_lazySingletons[i].Key) + " ??= " + GenerateConstructor(_lazySingletons[i].Value, 4) + ");");
            }

            builder.AppendLine("default:");
            builder.AppendLine("return default;");
            builder.AppendLine();

            builder.AppendLine("}");
            builder.AppendLine("}");
            builder.AppendLine("}");

            var test = builder.ToString();
        }

        private static string GenerateConstructor(Type type, int depth)
        {
            var singleton = _lazySingletons.FirstOrDefault(x => x.Key == type);
            if (singleton != default)
            {
                type = singleton.Value;
            }

            var builder = new FormattedBuilder("new " + type.FullName + "(", depth + 1);

            var ctor = type.GetConstructors().FirstOrDefault();
            var args = ctor?.GetParameters().Where(x => !x.HasDefaultValue).ToArray();

            if (args == null || args.Length < 1)
            {
                builder.Append(")");
                return builder.ToString();
            }
            else if (args.Length > 1)
            {
                builder.Append("\r\n");
            }


            for (int j = 0; j < args.Length; j++)
            {
                var param = args[j];
                var getter = "Resolve<" + param.ParameterType.FullName + ">()";

                if (_globals.Contains(param.ParameterType) || _singletons.Any(x => x.Key == param.ParameterType))
                {
                    getter = GetSingletonName(param.ParameterType);
                }
                else if (_lazySingletons.Any(x => x.Key == param.ParameterType))
                {
                    getter = GetSingletonName(param.ParameterType) + " ??= " + GenerateConstructor(param.ParameterType, depth + 1);
                }
                else if (param.Name == "session" && param.ParameterType == typeof(int))
                {
                    getter = "_session";
                }
                else if (param.Name == "selected" || param.Name == "online")
                {
                    getter = "active";
                }

                if (j == args.Length - 1)
                {
                    if (args.Length > 1)
                    {
                        builder.AppendIndent(getter + ")");
                    }
                    else
                    {
                        builder.Append(getter + ")");
                    }
                }
                else
                {
                    builder.AppendLine(getter + ",");
                }
            }

            return builder.ToString();
        }

        private static string GetSingletonName(Type type, bool underscore = true)
        {
            var name = type.Name;
            if (type.IsInterface)
            {
                name = name.Substring(1);
            }

            return (underscore ? "_" : "") + name.Substring(0, 1).ToLowerInvariant() + name.Substring(1);
        }
    }
}
