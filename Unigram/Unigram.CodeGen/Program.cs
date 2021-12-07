using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
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

namespace Unigram.CodeGen
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            //TypeContainerGenerator.Generate();
            TypeCrosserGenerator.Generate();
            Console.ReadLine();
        }
    }

    public class TypeCrosserGenerator
    {
        public static async void Generate()
        {
            var schemeInfo = new FileInfo("scheme.tl");
            if (schemeInfo.Exists is false)
            {
                using var client = new HttpClient();
                var response = await client.GetStringAsync("https://raw.githubusercontent.com/tdlib/td/master/td/generate/scheme/td_api.tl");

                File.WriteAllText("scheme.tl", response);
            }

            var scheme = File.ReadAllLines(schemeInfo.FullName);
            var functions = false;

            //var types = new Dictionary<string, string>();
            var types = new List<KeyValuePair<string, string>>();

            foreach (var line in scheme)
            {
                if (string.IsNullOrEmpty(line) || line.StartsWith("//"))
                {
                    continue;
                }
                else if (line.Equals("---functions---"))
                {
                    functions = true;
                    continue;
                }

                var split = line.Split('=');
                var type = split[1].Trim(' ', ';');

                if (functions)
                {

                }
                else
                {
                }
                types.Add(new KeyValuePair<string, string>(type, split[0]));
            }

            var typesToCross = new List<string>();
            var typesToCrossMap = new List<KeyValuePair<string, Dictionary<string, string>>>();
            var addedSomething = true;

            var vectorRegex = new Regex("vector<(.*?)>", RegexOptions.Compiled);

            while (addedSomething)
            {
                addedSomething = false;

                foreach (var type in types)
                {
                    var split = type.Value.Split(' ');
                    if (split.Length <= 1)
                    {
                        continue;
                    }

                    var targets = new Dictionary<string, string>();

                    foreach (var item in split[1..])
                    {
                        var pair = item.Split(':');
                        if (pair.Length < 2)
                        {
                            continue;
                        }

                        var match = vectorRegex.Match(pair[1]);
                        if (match.Success)
                        {
                            pair[1] = match.Groups[1].Value;
                        }

                        var pair1 = pair[1].CamelCase();
                        if (pair[1] == "file" || typesToCross.Contains(pair[1]))
                        {
                            targets[pair[0]] = pair[1];
                        }
                    }

                    if (targets.Count > 0)
                    {
                        var split0 = split[0].CamelCase();
                        if (!typesToCross.Contains(split0))
                        {
                            typesToCrossMap.Add(new KeyValuePair<string, Dictionary<string, string>>(split0, targets));

                            typesToCross.Add(split0);
                            addedSomething = true;
                        }

                        var key = type.Key[..1].CamelCase();
                        if (!typesToCross.Contains(key))
                        {
                            typesToCross.Add(key);
                            addedSomething = true;
                        }
                    }
                }
            }

            var builder = new FormattedBuilder();
            builder.AppendLine("public void MapFiles(object target)");
            builder.AppendLine("{");

            var first = true;

            foreach (var type in typesToCrossMap)
            {
                var key = type.Key.TitleCase();
                var name = type.Key.CamelCase();

                if (first)
                {
                    builder.AppendLine($"if (target is {key} {name})");
                    first = false;
                }
                else
                {
                    builder.AppendLine($"else if (target is {key} {name})");
                }

                builder.AppendLine("{");

                foreach (var property in type.Value)
                {
                    var propertyKey = property.Key.TitleCase();
                    if (property.Key == name)
                    {
                        propertyKey += "Value";
                    }

                    builder.AppendLine($"if ({name}.{propertyKey} != null)");
                    builder.AppendLine("{");

                    if (property.Value == "file")
                    {
                        builder.AppendLine($"_filesMap[{name}.{propertyKey}.Id] = {name}.{propertyKey};");
                    }
                    else
                    {
                        builder.AppendLine($"MapFiles({name}.{propertyKey});");
                    }

                    builder.AppendLine("}");
                }

                builder.AppendLine("}");
            }

            builder.AppendLine("}");
            var c = builder.ToString();

            var b = string.Join(", ", typesToCross);
            var a = 2+3;
        }
    }

    public class TypeContainerGenerator
    {
        private static List<Type> _globals;
        private static List<(Type Key, Type Value)> _singletons;
        private static List<(Type Key, Type Value)> _lazySingletons;
        private static List<Type> _instances;

        public static void Generate()
        {
            _globals = new List<Type>
            {
                typeof(ILifetimeService),
                typeof(ILocaleService),
                typeof(IPasscodeService)
            };

            _singletons = new List<(Type, Type)>
            {
                ( typeof(ICacheService), typeof(ProtoService) ),
                ( typeof(IProtoService), typeof(ProtoService) ),
                ( typeof(INotificationsService), typeof(NotificationsService) ),
                ( typeof(IGenerationService), typeof(GenerationService) ),
                ( typeof(INetworkService), typeof(NetworkService) )
            };

            _lazySingletons = new List<(Type, Type)>
            {
                ( typeof(ISettingsService), typeof(SettingsService) ),
                ( typeof(ISettingsSearchService), typeof(SettingsSearchService) ),
                ( typeof(IEmojiSetService), typeof(EmojiSetService) ),
                ( typeof(ICloudUpdateService), typeof(CloudUpdateService) ),
                ( typeof(IShortcutsService), typeof(ShortcutsService) ),
                ( typeof(IVoipService), typeof(VoipService) ),
                ( typeof(IGroupCallService), typeof(GroupCallService) ),
                ( typeof(IDeviceInfoService), typeof(DeviceInfoService) ),
                ( typeof(IEventAggregator), typeof(EventAggregator) ),
                ( typeof(IContactsService), typeof(ContactsService) ),
                ( typeof(ILocationService), typeof(LocationService) ),
                ( typeof(IThemeService), typeof(ThemeService) ),
                ( typeof(IMessageFactory), typeof(MessageFactory) ),
                ( typeof(IPlaybackService), typeof(PlaybackService) ),
                ( typeof(IViewService), typeof(ViewService) ),
                ( typeof(ISessionService), typeof(SessionService) ),
                ( typeof(IStorageService), typeof(StorageService) ),
            };

            _instances = new List<Type>
            {
                typeof(SignInViewModel),
                typeof(SignUpViewModel),
                typeof(SignInSentCodeViewModel),
                typeof(SignInPasswordViewModel),
                typeof(SignInRecoveryViewModel),
                typeof(MainViewModel),
                typeof(ShareViewModel),
                typeof(SendLocationViewModel),
                typeof(DialogViewModel),
                typeof(DialogThreadViewModel),
                typeof(DialogPinnedViewModel),
                typeof(DialogScheduledViewModel),
                typeof(DialogEventLogViewModel),
                typeof(AnimationDrawerViewModel),
                typeof(StickerDrawerViewModel),
                typeof(ProfileViewModel),
                typeof(UserCommonChatsViewModel),
                typeof(UserCreateViewModel),
                typeof(SupergroupEditViewModel),
                typeof(SupergroupEditTypeViewModel),
                typeof(SupergroupEditStickerSetViewModel),
                typeof(SupergroupEditAdministratorViewModel),
                typeof(SupergroupEditRestrictedViewModel),
                typeof(SupergroupEditLinkedChatViewModel),
                typeof(SupergroupAddAdministratorViewModel),
                typeof(SupergroupAddRestrictedViewModel),
                typeof(LiveLocationViewModel),
                typeof(ChatInviteLinkViewModel),
                typeof(SupergroupAdministratorsViewModel),
                typeof(SupergroupBannedViewModel),
                typeof(SupergroupPermissionsViewModel),
                typeof(SupergroupMembersViewModel),
                typeof(ChatSharedMediaViewModel),
                typeof(ChatStatisticsViewModel),
                typeof(MessageStatisticsViewModel),
                typeof(ChannelCreateStep1ViewModel),
                typeof(ChannelCreateStep2ViewModel),
                typeof(BasicGroupCreateStep1ViewModel),
                typeof(InstantViewModel),
                typeof(LogOutViewModel),
                typeof(DiagnosticsViewModel),
                typeof(SettingsViewModel),
                typeof(SettingsAdvancedViewModel),
                typeof(SettingsPhoneIntroViewModel),
                typeof(SettingsPhoneViewModel),
                typeof(SettingsPhoneSentCodeViewModel),
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
                typeof(SettingsProxiesViewModel),
                typeof(SettingsPrivacyAndSecurityViewModel),
                typeof(SettingsPrivacyAllowCallsViewModel),
                typeof(SettingsPrivacyAllowP2PCallsViewModel),
                typeof(SettingsPrivacyAllowChatInvitesViewModel),
                typeof(SettingsPrivacyShowForwardedViewModel),
                typeof(SettingsPrivacyPhoneViewModel),
                typeof(SettingsPrivacyShowPhoneViewModel),
                typeof(SettingsPrivacyAllowFindingByPhoneNumberViewModel),
                typeof(SettingsPrivacyShowPhotoViewModel),
                typeof(SettingsPrivacyShowStatusViewModel),
                typeof(SettingsPasswordViewModel),
                typeof(SettingsPasswordIntroViewModel),
                typeof(SettingsPasswordCreateViewModel),
                typeof(SettingsPasswordHintViewModel),
                typeof(SettingsPasswordEmailViewModel),
                typeof(SettingsPasswordConfirmViewModel),
                typeof(SettingsPasswordDoneViewModel),
                typeof(SettingsPasscodeViewModel),
                typeof(SettingsStickersViewModel),
                typeof(SettingsLanguageViewModel),
                typeof(SettingsAppearanceViewModel),
                typeof(SettingsThemesViewModel),
                typeof(SettingsThemeViewModel),
                typeof(SettingsNightModeViewModel),
                typeof(SettingsBackgroundsViewModel),
                typeof(SettingsShortcutsViewModel),
                typeof(BackgroundViewModel),
                typeof(AttachedStickersViewModel),
                typeof(ViewModels.StickerSetViewModel),
                typeof(PaymentAddressViewModel),
                typeof(PaymentCredentialsViewModel),
                typeof(PaymentFormViewModel),
                typeof(InviteViewModel),
                typeof(ChatsNearbyViewModel),
                typeof(FoldersViewModel),
                typeof(FolderViewModel),
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

                if (args.Any(x => x.ParameterType == y.Key))
                {
                    return 1;
                }

                return -1;
            });

            var singletonBucket = new Dictionary<Type, Type>();

            var builder = new FormattedBuilder();
            builder.AppendLine("namespace Unigram.Views");
            builder.AppendLine("{");
            builder.AppendLine("public class TLLocator");
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

            builder.AppendIndent("public TLLocator(");

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
            builder.AppendLine("var type = typeof(T);");

            for (int i = 0; i < _instances.Count; i++)
            {
                if (i == 0)
                {
                    builder.AppendLine("if (type == typeof(" + _instances[i].FullName + "))");
                }
                else
                {
                    builder.AppendLine("else if (type == typeof(" + _instances[i].FullName + "))");
                }

                builder.AppendLine("{");
                builder.AppendLine("return (T)(object)" + GenerateConstructor(_instances[i], 4) + "; ");
                builder.AppendLine("}");
            }

            for (int i = 0; i < _singletons.Count; i++)
            {
                builder.AppendLine("else if (type == typeof(" + _singletons[i].Key.FullName + "))");
                builder.AppendLine("{");
                builder.AppendLine("return (T)" + GetSingletonName(_singletons[i].Key) + "; ");
                builder.AppendLine("}");
            }

            for (int i = 0; i < _lazySingletons.Count; i++)
            {
                builder.AppendLine("else if (type == typeof(" + _lazySingletons[i].Key.FullName + "))");
                builder.AppendLine("{");
                builder.AppendLine("return (T)(" + GetSingletonName(_lazySingletons[i].Key) + " ??= " + GenerateConstructor(_lazySingletons[i].Value, 4) + ");");
                builder.AppendLine("}");
            }

            builder.AppendLine();
            builder.AppendLine("return default;");

            builder.AppendLine("}");
            builder.AppendLine("}");
            builder.AppendLine("}");

            var test = builder.ToString();
            File.WriteAllText("TLLocator.cs", builder.ToString());
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

    public class FormattedBuilder
    {
        private readonly StringBuilder _builder;
        private int _indent;

        public FormattedBuilder(string text, int indent)
        {
            _builder = new StringBuilder(text);
            _indent = indent;
        }

        public FormattedBuilder()
        {
            _builder = new StringBuilder();
        }

        public void Append(string text)
        {
            _builder.Append(text);
        }

        public void AppendLine(string text)
        {
            if (text == "}")
            {
                _indent--;
            }

            AppendIndent();
            _builder.Append(text);
            _builder.AppendLine();

            if (text == "{")
            {
                _indent++;
            }
        }

        public void AppendIndent(string text)
        {
            if (text == "}")
            {
                _indent--;
            }

            AppendIndent();
            _builder.Append(text);

            if (text == "{")
            {
                _indent++;
            }
        }

        public void AppendLine()
        {
            _builder.AppendLine();
        }

        private void AppendIndent()
        {
            for (int i = 0; i < _indent; i++)
            {
                _builder.Append("    ");
            }
        }

        public override string ToString()
        {
            return _builder.ToString();
        }
    }

    public static class Extensions
    {
        public static string TitleCase(this string str)
        {
            var split = str.Split('_');
            return string.Join("", split.Select(x => x[..1].ToUpperInvariant() + x[1..]));
        }

        public static string CamelCase(this string str)
        {
            return str[..1].ToLowerInvariant() + str[1..];
        }
    }
}
