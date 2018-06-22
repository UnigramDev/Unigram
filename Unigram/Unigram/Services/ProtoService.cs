using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Entities;
using Windows.Storage;

namespace Unigram.Services
{
    public interface IProtoService : ICacheService
    {
        BaseObject Execute(Function function);

        void Send(Function function);
        void Send(Function function, ClientResultHandler handler);
        void Send(Function function, Action<BaseObject> handler);
        Task<BaseObject> SendAsync(Function function);

        int SessionId { get; }
    }

    public interface ICacheService
    {
        int GetMyId();
        T GetOption<T>(string key) where T : OptionValue;
        bool TryGetOption<T>(string key, out T value) where T : OptionValue;

        AuthorizationState GetAuthorizationState();
        ConnectionState GetConnectionState();

        string GetTitle(Chat chat, bool tiny = false);
        Chat GetChat(long id);
        IList<Chat> GetChats(IList<long> ids);
        IList<Chat> GetChats(int count);

        bool IsChatPromoted(Chat chat);

        bool TryGetChatFromUser(int userId, out Chat chat);
        bool TryGetChatFromSecret(int secretId, out Chat chat);

        SecretChat GetSecretChat(int id);
        SecretChat GetSecretChatForUser(int id);

        User GetUser(Chat chat);
        User GetUser(int id);
        UserFullInfo GetUserFull(int id);
        IList<User> GetUsers(IList<int> ids);

        BasicGroup GetBasicGroup(int id);
        BasicGroupFullInfo GetBasicGroupFull(int id);
        Supergroup GetSupergroup(int id);
        SupergroupFullInfo GetSupergroupFull(int id);

        bool IsStickerFavorite(int id);
        bool IsStickerSetInstalled(long id);

        AutoDownloadPreferences Preferences { get; }
        void SetPreferences(AutoDownloadPreferences preferences);

        int UnreadCount { get; }
        int UnreadUnmutedCount { get; }
    }

    public class ProtoService : IProtoService, ClientResultHandler
    {
        private Client _client;
        private readonly int _session;
        private readonly IDeviceInfoService _deviceInfoService;
        private readonly ISettingsService _settings;
        private readonly IEventAggregator _aggregator;

        private readonly Dictionary<string, object> _options = new Dictionary<string, object>();

        private readonly Dictionary<long, Chat> _chats = new Dictionary<long, Chat>();

        private readonly Dictionary<int, SecretChat> _secretChats = new Dictionary<int, SecretChat>();

        private readonly Dictionary<int, User> _users = new Dictionary<int, User>();
        private readonly Dictionary<int, UserFullInfo> _usersFull = new Dictionary<int, UserFullInfo>();

        private readonly Dictionary<int, BasicGroup> _basicGroups = new Dictionary<int, BasicGroup>();
        private readonly Dictionary<int, BasicGroupFullInfo> _basicGroupsFull = new Dictionary<int, BasicGroupFullInfo>();

        private readonly Dictionary<int, Supergroup> _supergroups = new Dictionary<int, Supergroup>();
        private readonly Dictionary<int, SupergroupFullInfo> _supergroupsFull = new Dictionary<int, SupergroupFullInfo>();

        private readonly SimpleFileContext<long> _chatsMap = new SimpleFileContext<long>();
        private readonly SimpleFileContext<int> _usersMap = new SimpleFileContext<int>();

        private AutoDownloadPreferences _preferences;

        private long _promotedChatId;

        private IList<int> _favoriteStickers;
        private IList<long> _installedStickerSets;
        private IList<long> _installedMaskSets;

        private AuthorizationState _authorizationState;
        private ConnectionState _connectionState;

        public ProtoService(int session, IDeviceInfoService deviceInfoService, ISettingsService settings, IEventAggregator aggregator)
        {
            Log.SetVerbosityLevel(ApplicationSettings.Current.VerbosityLevel);
            Log.SetFilePath(Path.Combine(ApplicationData.Current.LocalFolder.Path, $"{session}", "log"));

            _session = session;
            _deviceInfoService = deviceInfoService;
            _settings = settings;
            _aggregator = aggregator;

            _preferences = new AutoDownloadPreferences(ApplicationData.Current.LocalSettings.CreateContainer("autoDownload", ApplicationDataCreateDisposition.Always));

            Initialize();
        }

        private void Initialize()
        {
            _client = Client.Create(this);

            var parameters = new TdlibParameters
            {
                DatabaseDirectory = Path.Combine(ApplicationData.Current.LocalFolder.Path, $"{_session}"),
                UseSecretChats = true,
                UseMessageDatabase = true,
                ApiId = Constants.ApiId,
                ApiHash = Constants.ApiHash,
                SystemLanguageCode = "en",
                DeviceModel = _deviceInfoService.DeviceModel,
                SystemVersion = _deviceInfoService.SystemVersion,
                ApplicationVersion = _deviceInfoService.AppVersion,
                UseTestDc = false
            };

#if MOCKUP
            ProfilePhoto ProfilePhoto(string name)
            {
                return new ProfilePhoto(0, new Telegram.Td.Api.File(0, 0, 0, new LocalFile(System.IO.Path.Combine(Package.Current.InstalledLocation.Path, "Assets\\Mockup\\", name), true, true, false, true, 0, 0), null), null);
            }

            ChatPhoto ChatPhoto(string name)
            {
                return new ChatPhoto(new Telegram.Td.Api.File(0, 0, 0, new LocalFile(System.IO.Path.Combine(Package.Current.InstalledLocation.Path, "Assets\\Mockup\\", name), true, true, false, true, 0, 0), null), null);
            }

            _users[ 0] = new User( 0, "Jane",                   string.Empty, string.Empty, string.Empty, null, null, null, null, false, string.Empty, true, new UserTypeRegular(), string.Empty);
            _users[ 1] = new User( 1, "Tyrion", "Lannister",    string.Empty, string.Empty, null, null, null, null, false, string.Empty, true, new UserTypeRegular(), string.Empty);
            _users[ 2] = new User( 2, "Alena", "Shy",           string.Empty, string.Empty, null, null, null, null, false, string.Empty, true, new UserTypeRegular(), string.Empty);
            _users[ 3] = new User( 3, "Heisenberg",             string.Empty, string.Empty, string.Empty, null, null, null, null, false, string.Empty, true, new UserTypeRegular(), string.Empty);
            _users[ 4] = new User( 4, "Bender",                 string.Empty, string.Empty, string.Empty, null, null, null, null, false, string.Empty, true, new UserTypeRegular(), string.Empty);
            _users[ 5] = new User( 5, "EVE",                    string.Empty, string.Empty, string.Empty, null, null, null, null, false, string.Empty, true, new UserTypeRegular(), string.Empty);
            _users[16] = new User(16, "Nick",                   string.Empty, string.Empty, string.Empty, null, null, null, null, false, string.Empty, true, new UserTypeRegular(), string.Empty);
            _users[ 7] = new User( 7, "Eileen", "Lockhard \uD83D\uDC99", string.Empty, string.Empty, new UserStatusOnline(int.MaxValue), ProfilePhoto("a5.png"), null, null, false, string.Empty, true, new UserTypeRegular(), string.Empty);
            _users[11] = new User(11, "Thomas",                 string.Empty, string.Empty, string.Empty, null, ProfilePhoto("a3.png"), null, null, false, string.Empty, true, new UserTypeRegular(), string.Empty);
            _users[ 9] = new User( 9, "Daenerys",               string.Empty, string.Empty, string.Empty, null, ProfilePhoto("a2.png"), null, null, false, string.Empty, true, new UserTypeRegular(), string.Empty);
            _users[13] = new User(13, "Angela", "Merkel",       string.Empty, string.Empty, null, ProfilePhoto("a1.png"), null, null, false, string.Empty, true, new UserTypeRegular(), string.Empty);
            _users[10] = new User(10, "Julian", "Assange",      string.Empty, string.Empty, null, null, null, null, false, string.Empty, true, new UserTypeRegular(), string.Empty);
            _users[ 8] = new User( 8, "Pierre",                 string.Empty, string.Empty, string.Empty, null, null, null, null, false, string.Empty, true, new UserTypeRegular(), string.Empty);
            _users[17] = new User(17, "Alexmitter",             string.Empty, string.Empty, string.Empty, null, null, null, null, false, string.Empty, true, new UserTypeRegular(), string.Empty);
            _users[18] = new User(18, "Jaina", "Moore",         string.Empty, string.Empty, null, null, null, null, false, string.Empty, true, new UserTypeRegular(), string.Empty);

            _secretChats[1] = new SecretChat(1, 7, new SecretChatStateReady(), false, 15, new byte[0], 75);

            _supergroups[0] = new Supergroup(0, string.Empty, 0, new ChatMemberStatusMember(), 0, false, false, true, true, string.Empty);
            _supergroups[1] = new Supergroup(1, string.Empty, 0, new ChatMemberStatusMember(), 0, false, false, true, false, string.Empty);
            _supergroups[2] = new Supergroup(2, string.Empty, 0, new ChatMemberStatusMember(), 7, false, false, false, false, string.Empty);
            _supergroups[3] = new Supergroup(3, string.Empty, 0, new ChatMemberStatusMember(), 0, false, false, false, false, string.Empty);

            int TodayDate(int hour, int minute)
            {
                return (int)Utils.DateTimeToUnixTimestamp(DateTime.Now.Date.AddHours(hour).AddMinutes(minute));
            }

            int TuesdayDate()
            {
                var last = DateTime.Now;
                do
                {
                    last = last.AddDays(-1);
                }
                while (last.DayOfWeek != DayOfWeek.Tuesday);

                return (int)Utils.DateTimeToUnixTimestamp(last);
            }

            var lastMessage0  = new Message(long.MaxValue, 0,  0,  null, false, false, false, false, false, false, false, TodayDate(17, 07), 0, null, 0, 0, 0, 0, string.Empty, 0, 0, new MessageText(new FormattedText("Great news everyone! Unigram X is now available in the Microsoft Store", new TextEntity[0]), null), null);
            var lastMessage1  = new Message(long.MaxValue, 1,  1,  null, false, false, false, false, false, false, false, TodayDate(15, 34), 0, null, 0, 0, 0, 0, string.Empty, 0, 0, new MessageText(new FormattedText("Well I do help animals. Maybe I'll have a few cats in my new luxury apartment. 😊", new TextEntity[0]), null), null);
            var lastMessage2  = new Message(long.MaxValue, 2,  2,  null, false, false, false, false, false, false, false, TodayDate(18, 12), 0, null, 0, 0, 0, 0, string.Empty, 0, 0, new MessageText(new FormattedText("Sometimes possession is an abstract concept. They took my purse, but the...", new TextEntity[0]), null), null);
            var lastMessage3  = new Message(long.MaxValue, 3,  3,  null, false, false, false, false, false, false, false, TodayDate(18, 00), 0, null, 0, 0, 0, 0, string.Empty, 0, 0, new MessageSticker(new Sticker(0, 0, 0, "😍", false, null, null, null)), null);
            var lastMessage4  = new Message(long.MaxValue, 4,  4,  null, false, false, false, false, false, false, false, TodayDate(17, 23), 0, null, 0, 0, 0, 0, string.Empty, 0, 0, new MessageText(new FormattedText("Thanks, Telegram helps me a lot. You have my financial support if you need more servers.", new TextEntity[0]), null), null);
            var lastMessage5  = new Message(long.MaxValue, 5,  5,  null, false, false, false, false, false, false, false, TodayDate(15, 10), 0, null, 0, 0, 0, 0, string.Empty, 0, 0, new MessageText(new FormattedText("I looove new Surfaces! If fact, they invited me to a focus group.", new TextEntity[0]), null), null);
            var lastMessage6  = new Message(long.MaxValue, 6,  6,  null, false, false, false, false, false, false, false, TodayDate(12, 53), 0, null, 0, 0, 0, 0, string.Empty, 0, 0, new MessageText(new FormattedText("Telegram just updated their iOS app!", new TextEntity[0]), null), null);
            var lastMessage7  = new Message(long.MaxValue, 7,  7,  null, false, false, false, false, false, false, false, TuesdayDate(), 0, null, 0, 0, 0, 0, string.Empty, 0, 0, new MessageDocument(new Document("LaserBlastSafetyGuide.pdf", string.Empty, null, null), new FormattedText(string.Empty, new TextEntity[0])), null);
            var lastMessage8  = new Message(long.MaxValue, 8,  8,  null, false, false, false, false, false, false, false, TuesdayDate(), 0, null, 0, 0, 0, 0, string.Empty, 0, 0, new MessageText(new FormattedText("It's impossible.", new TextEntity[0]), null), null);
            var lastMessage9  = new Message(long.MaxValue, 9,  9,  null, false, false, false, false, false, false, false, TuesdayDate(), 0, null, 0, 0, 0, 0, string.Empty, 0, 0, new MessageText(new FormattedText("Hola!", new TextEntity[0]), null), null);
            var lastMessage10 = new Message(long.MaxValue, 17, 12, null, false, false, false, false, false, false, false, TuesdayDate(), 0, null, 0, 0, 0, 0, string.Empty, 0, 0, new MessageText(new FormattedText("Let's design more robust memes", new TextEntity[0]), null), null);
            var lastMessage11 = new Message(long.MaxValue, 18, 13, null, false, false, false, false, false, false, false, TuesdayDate(), 0, null, 0, 0, 0, 0, string.Empty, 0, 0, new MessageText(new FormattedText("What?! 😱", new TextEntity[0]), null), null);

            _chats[ 0] = new Chat( 0, new ChatTypeSupergroup(0, true),      "Unigram News",     ChatPhoto("a0.png"),  lastMessage0,  long.MaxValue - 0,  true,  0, 0, 0, 0, new NotificationSettings(0, string.Empty, true), 0, null, string.Empty);
            _chats[ 1] = new Chat( 1, new ChatTypePrivate(0),               "Jane",             ChatPhoto("a6.png"),  lastMessage1,  long.MaxValue - 1,  true,  0, 0, 0, 0, new NotificationSettings(0, string.Empty, true), 0, null, string.Empty);
            _chats[ 2] = new Chat( 2, new ChatTypePrivate(1),               "Tyrion Lannister", null,                 lastMessage2,  long.MaxValue - 2,  false, 1, 0, 0, 0, new NotificationSettings(0, string.Empty, true), 0, null, string.Empty);
            _chats[ 3] = new Chat( 3, new ChatTypePrivate(2),               "Alena Shy",        ChatPhoto("a7.png"),  lastMessage3,  long.MaxValue - 3,  false, 0, 0, 0, 0, new NotificationSettings(0, string.Empty, true), 0, null, string.Empty);
            _chats[ 4] = new Chat( 4, new ChatTypeSecret(0, 3),             "Heisenberg",       ChatPhoto("a8.png"),  lastMessage4,  long.MaxValue - 4,  false, 0, 0, 0, 0, new NotificationSettings(0, string.Empty, true), 0, null, string.Empty);
            _chats[ 5] = new Chat( 5, new ChatTypePrivate(4),               "Bender",           ChatPhoto("a9.png"),  lastMessage5,  long.MaxValue - 5,  false, 0, 0, 0, 0, new NotificationSettings(0, string.Empty, true), 0, null, string.Empty);
            _chats[ 6] = new Chat( 6, new ChatTypeSupergroup(1, true),      "World News Today", ChatPhoto("a10.png"), lastMessage6,  long.MaxValue - 6,  false, 1, 0, 0, 0, new NotificationSettings(int.MaxValue, string.Empty, true), 0, null, string.Empty);
            _chats[ 7] = new Chat( 7, new ChatTypePrivate(5),               "EVE",              ChatPhoto("a11.png"), lastMessage7,  long.MaxValue - 7,  false, 0, 0, 0, 0, new NotificationSettings(0, string.Empty, true), 0, null, string.Empty);
            _chats[ 8] = new Chat( 8, new ChatTypePrivate(16),              "Nick",             null,                 lastMessage8,  long.MaxValue - 8,  false, 0, 0, 0, 0, new NotificationSettings(0, string.Empty, true), 0, null, string.Empty);
            _chats[11] = new Chat(11, new ChatTypePrivate(16),              "Kate Rodriguez",   ChatPhoto("a13.png"), lastMessage9,  long.MaxValue - 9,  false, 0, 0, 0, 0, new NotificationSettings(0, string.Empty, true), 0, null, string.Empty);
            _chats[12] = new Chat(12, new ChatTypeSupergroup(3, false),     "Meme Factory",     ChatPhoto("a14.png"), lastMessage10, long.MaxValue - 10, false, 0, 0, 0, 0, new NotificationSettings(0, string.Empty, true), 0, null, string.Empty);
            _chats[13] = new Chat(13, new ChatTypePrivate(18),              "Jaina Moore",      null,                 lastMessage11, long.MaxValue - 11, false, 0, 0, 0, 0, new NotificationSettings(0, string.Empty, true), 0, null, string.Empty);

            _chats[ 9] = new Chat( 9, new ChatTypeSupergroup(2, false),        "Weekend Plans", ChatPhoto("a4.png"),  null, 0, false, 0, 0, long.MaxValue, 0, new NotificationSettings(0, string.Empty, true), 0, null, string.Empty);
            _chats[10] = new Chat(10, new ChatTypeSecret(1, 7), "Eileen Lockhard \uD83D\uDC99", ChatPhoto("a5.png"),  null, 0, false, 0, 0, long.MaxValue, 0, new NotificationSettings(0, string.Empty, true), 0, null, string.Empty);
#endif

            Task.Run(() =>
            {
                _client.Send(new SetTdlibParameters(parameters));
                _client.Send(new CheckDatabaseEncryptionKey(new byte[0]));
                _client.Run();

                var ttl = _settings.FilesTtl;
                if (ttl > 0)
                {
                    _client.Send(new OptimizeStorage(long.MaxValue, ttl * 60 * 60 * 24, int.MaxValue, 0, new FileType[0], new long[0], new long[0], 0));
                }
            });
        }

        private async void UpdateVersion()
        {
            if (_settings.Version < ApplicationSettings.CurrentVersion)
            {
                var response = await SendAsync(new CreatePrivateChat(777000, false));
                if (response is Chat chat)
                {
                    Send(new AddLocalMessage(chat.Id, 777000, 0, false, new InputMessageText(new FormattedText(ApplicationSettings.CurrentChangelog, new TextEntity[0]), true, false)));
                }
            }

            _settings.UpdateVersion();
        }

        public void CleanUp()
        {
            _options.Clear();

            _chats.Clear();

            _secretChats.Clear();

            _users.Clear();
            _usersFull.Clear();

            _basicGroups.Clear();
            _basicGroupsFull.Clear();

            _supergroups.Clear();
            _supergroupsFull.Clear();

            _chatsMap.Clear();
            _usersMap.Clear();

            _promotedChatId = 0;

            _favoriteStickers?.Clear();
            _installedStickerSets?.Clear();
            _installedMaskSets?.Clear();

            _authorizationState = null;
            _connectionState = null;
        }



        public BaseObject Execute(Function function)
        {
            return _client.Execute(function);
        }



        public void Send(Function function)
        {
            _client.Send(function);
        }

        public void Send(Function function, ClientResultHandler handler)
        {
            _client.Send(function, handler);
        }

        public void Send(Function function, Action<BaseObject> handler)
        {
            _client.Send(function, handler);
        }

        public Task<BaseObject> SendAsync(Function function)
        {
            return _client.SendAsync(function);
        }



        public int SessionId => _session;

        #region Cache

        public int UnreadCount { get; private set; }
        public int UnreadUnmutedCount { get; private set; }

        private bool TryGetChatForFileId(int fileId, out Chat chat)
        {
            if (_chatsMap.TryGetValue(fileId, out long chatId))
            {
                chat = GetChat(chatId);
                return true;
            }

            chat = null;
            return false;
        }

        private bool TryGetUserForFileId(int fileId, out User user)
        {
            if (_usersMap.TryGetValue(fileId, out int userId))
            {
                user = GetUser(userId);
                return true;
            }

            user = null;
            return false;
        }

        public bool IsChatPromoted(Chat chat)
        {
            if (_promotedChatId == chat.Id && chat.Type is ChatTypeSupergroup type)
            {
                var supergroup = GetSupergroup(type.SupergroupId);
                if (supergroup != null)
                {
                    return !supergroup.IsMember();
                }
            }

            return false;
        }



        public AutoDownloadPreferences Preferences
        {
            get
            {
                return _preferences;
            }
        }

        public void SetPreferences(AutoDownloadPreferences preferences)
        {
            _preferences = preferences ?? AutoDownloadPreferences.Default;
            _preferences.Save(ApplicationData.Current.LocalSettings.CreateContainer("autoDownload", ApplicationDataCreateDisposition.Always));
        }

        public AuthorizationState GetAuthorizationState()
        {
            return _authorizationState;
        }

        public ConnectionState GetConnectionState()
        {
            return _connectionState;
        }

        public int GetMyId()
        {
            var option = GetOption<OptionValueInteger>("my_id");
            if (option != null)
            {
                return option.Value;
            }

            return 0;
        }

        public T GetOption<T>(string key) where T : OptionValue
        {
            if (_options.TryGetValue(key, out object value))
            {
                if (value is OptionValueEmpty)
                {
                    return default(T);
                }

                return (T)value;
            }

            return default(T);
        }

        public bool TryGetOption<T>(string key, out T result) where T : OptionValue
        {
            if (_options.TryGetValue(key, out object value))
            {
                if (value is OptionValueEmpty)
                {
                    result = default(T);
                    return false;
                }

                result = (T)value;
                return true;
            }

            result = default(T);
            return false;
        }

        public string GetTitle(Chat chat, bool tiny = false)
        {
            if (chat == null)
            {
                return string.Empty;
            }

            var user = GetUser(chat);
            if (user != null)
            {
                if (user.Type is UserTypeDeleted)
                {
                    return Strings.Resources.HiddenName;
                }
                else if (user.Id == GetMyId())
                {
                    return Strings.Resources.SavedMessages;
                }
                else if (tiny)
                {
                    return user.FirstName;
                }
            }

            return chat.Title;
        }

        public Chat GetChat(long id)
        {
            if (_chats.TryGetValue(id, out Chat value))
            {
                return value;
            }

            return null;
        }

        public bool TryGetChatFromUser(int userId, out Chat chat)
        {
            chat = _chats.Values.FirstOrDefault(x => x.Type is ChatTypePrivate privata && privata.UserId == userId);
            return chat != null;
        }

        public bool TryGetChatFromSecret(int secretId, out Chat chat)
        {
            chat = _chats.Values.FirstOrDefault(x => x.Type is ChatTypeSecret secret && secret.SecretChatId == secretId);
            return chat != null;
        }


        public IList<Chat> GetChats(IList<long> ids)
        {
            var result = new List<Chat>(ids.Count);

            foreach (var id in ids)
            {
                var chat = GetChat(id);
                if (chat != null)
                {
                    result.Add(chat);
                }
            }

            return result;
        }

        public IList<User> GetUsers(IList<int> ids)
        {
            var result = new List<User>(ids.Count);

            foreach (var id in ids)
            {
                var user = GetUser(id);
                if (user != null)
                {
                    result.Add(user);
                }
            }

            return result;
        }

        public IList<Chat> GetChats(int count)
        {
            return _chats.Values.Where(x => x.Order != 0).OrderByDescending(x => x.Order).Take(count).ToList();
        }

        public SecretChat GetSecretChat(int id)
        {
            if (_secretChats.TryGetValue(id, out SecretChat value))
            {
                return value;
            }

            return null;
        }

        public SecretChat GetSecretChatForUser(int id)
        {
            return _secretChats.FirstOrDefault(x => x.Value.UserId == id).Value;
        }

        public User GetUser(Chat chat)
        {
            if (chat?.Type is ChatTypePrivate privata)
            {
                return GetUser(privata.UserId);
            }
            else if (chat?.Type is ChatTypeSecret secret)
            {
                return GetUser(secret.UserId);
            }

            return null;
        }

        public User GetUser(int id)
        {
            if (_users.TryGetValue(id, out User value))
            {
                return value;
            }

            return null;
        }

        public UserFullInfo GetUserFull(int id)
        {
            if (_usersFull.TryGetValue(id, out UserFullInfo value))
            {
                return value;
            }

            return null;
        }

        public BasicGroup GetBasicGroup(int id)
        {
            if (_basicGroups.TryGetValue(id, out BasicGroup value))
            {
                return value;
            }

            return null;
        }

        public BasicGroupFullInfo GetBasicGroupFull(int id)
        {
            if (_basicGroupsFull.TryGetValue(id, out BasicGroupFullInfo value))
            {
                return value;
            }

            return null;
        }

        public Supergroup GetSupergroup(int id)
        {
            if (_supergroups.TryGetValue(id, out Supergroup value))
            {
                return value;
            }

            return null;
        }

        public SupergroupFullInfo GetSupergroupFull(int id)
        {
            if (_supergroupsFull.TryGetValue(id, out SupergroupFullInfo value))
            {
                return value;
            }

            return null;
        }



        public bool IsStickerFavorite(int id)
        {
            if (_favoriteStickers != null)
            {
                return _favoriteStickers.Contains(id);
            }

            return false;
        }

        public bool IsStickerSetInstalled(long id)
        {
            if (_installedStickerSets != null)
            {
                return _installedStickerSets.Contains(id);
            }

            return false;
        }

        #endregion



        public void OnResult(BaseObject update)
        {
            if (update is UpdateAuthorizationState updateAuthorizationState)
            {
                switch (updateAuthorizationState.AuthorizationState)
                {
                    case AuthorizationStateClosed closed:
                        CleanUp();
                        Initialize();
                        break;
                    case AuthorizationStateReady ready:
                        UpdateVersion();
                        break;
                }

                _authorizationState = updateAuthorizationState.AuthorizationState;
            }
            else if (update is UpdateBasicGroup updateBasicGroup)
            {
                _basicGroups[updateBasicGroup.BasicGroup.Id] = updateBasicGroup.BasicGroup;
            }
            else if (update is UpdateBasicGroupFullInfo updateBasicGroupFullInfo)
            {
                _basicGroupsFull[updateBasicGroupFullInfo.BasicGroupId] = updateBasicGroupFullInfo.BasicGroupFullInfo;
            }
            else if (update is UpdateCall updateCall)
            {

            }
            else if (update is UpdateChatDraftMessage updateChatDraftMessage)
            {
                if (_chats.TryGetValue(updateChatDraftMessage.ChatId, out Chat value))
                {
                    value.Order = updateChatDraftMessage.Order;
                    value.DraftMessage = updateChatDraftMessage.DraftMessage;
                }
            }
            else if (update is UpdateChatIsPinned updateChatIsPinned)
            {
                if (_chats.TryGetValue(updateChatIsPinned.ChatId, out Chat value))
                {
                    value.Order = updateChatIsPinned.Order;
                    value.IsPinned = updateChatIsPinned.IsPinned;
                }
            }
            else if (update is UpdateChatLastMessage updateChatLastMessage)
            {
                if (_chats.TryGetValue(updateChatLastMessage.ChatId, out Chat value))
                {
                    value.Order = updateChatLastMessage.Order;
                    value.LastMessage = updateChatLastMessage.LastMessage;
                }
            }
            else if (update is UpdateChatOrder updateChatOrder)
            {
                if (_chats.TryGetValue(updateChatOrder.ChatId, out Chat value))
                {
                    value.Order = updateChatOrder.Order;
                }
            }
            else if (update is UpdateChatPhoto updateChatPhoto)
            {
                if (_chats.TryGetValue(updateChatPhoto.ChatId, out Chat value))
                {
                    value.Photo = updateChatPhoto.Photo;
                }

                if (updateChatPhoto.Photo != null)
                {
                    _chatsMap[updateChatPhoto.Photo.Small.Id] = updateChatPhoto.ChatId;
                    _chatsMap[updateChatPhoto.Photo.Big.Id] = updateChatPhoto.ChatId;
                }
            }
            else if (update is UpdateChatReadInbox updateChatReadInbox)
            {
                if (_chats.TryGetValue(updateChatReadInbox.ChatId, out Chat value))
                {
                    value.UnreadCount = updateChatReadInbox.UnreadCount;
                    value.LastReadInboxMessageId = updateChatReadInbox.LastReadInboxMessageId;
                }
            }
            else if (update is UpdateChatReadOutbox updateChatReadOutbox)
            {
                if (_chats.TryGetValue(updateChatReadOutbox.ChatId, out Chat value))
                {
                    value.LastReadOutboxMessageId = updateChatReadOutbox.LastReadOutboxMessageId;
                }
            }
            else if (update is UpdateChatReplyMarkup updateChatReplyMarkup)
            {
                if (_chats.TryGetValue(updateChatReplyMarkup.ChatId, out Chat value))
                {
                    value.ReplyMarkupMessageId = updateChatReplyMarkup.ReplyMarkupMessageId;
                }
            }
            else if (update is UpdateChatTitle updateChatTitle)
            {
                if (_chats.TryGetValue(updateChatTitle.ChatId, out Chat value))
                {
                    value.Title = updateChatTitle.Title;
                }
            }
            else if (update is UpdateChatUnreadMentionCount updateChatUnreadMentionCount)
            {
                if (_chats.TryGetValue(updateChatUnreadMentionCount.ChatId, out Chat value))
                {
                    value.UnreadMentionCount = updateChatUnreadMentionCount.UnreadMentionCount;
                }
            }
            else if (update is UpdateConnectionState updateConnectionState)
            {
                _connectionState = updateConnectionState.State;
            }
            else if (update is UpdateDeleteMessages updateDeleteMessages)
            {

            }
            else if (update is UpdateFavoriteStickers updateFavoriteStickers)
            {
                _favoriteStickers = updateFavoriteStickers.StickerIds;
            }
            else if (update is UpdateFile updateFile)
            {
                if (TryGetChatForFileId(updateFile.File.Id, out Chat chat))
                {
                    chat.UpdateFile(updateFile.File);

                    if (updateFile.File.Local.IsDownloadingCompleted && updateFile.File.Remote.IsUploadingCompleted)
                    {
                        _chatsMap.Remove(updateFile.File.Id);
                    }
                }

                if (TryGetUserForFileId(updateFile.File.Id, out User user))
                {
                    user.UpdateFile(updateFile.File);

                    if (updateFile.File.Local.IsDownloadingCompleted && updateFile.File.Remote.IsUploadingCompleted)
                    {
                        _usersMap.Remove(updateFile.File.Id);
                    }
                }
            }
            else if (update is UpdateFileGenerationStart updateFileGenerationStart)
            {

            }
            else if (update is UpdateFileGenerationStop updateFileGenerationStop)
            {

            }
            else if (update is UpdateInstalledStickerSets updateInstalledStickerSets)
            {
                if (updateInstalledStickerSets.IsMasks)
                {
                    _installedMaskSets = updateInstalledStickerSets.StickerSetIds;
                }
                else
                {
                    _installedStickerSets = updateInstalledStickerSets.StickerSetIds;
                }
            }
            else if (update is UpdateMessageContent updateMessageContent)
            {

            }
            else if (update is UpdateMessageContentOpened updateMessageContentOpened)
            {

            }
            else if (update is UpdateMessageEdited updateMessageEdited)
            {

            }
            else if (update is UpdateMessageMentionRead updateMessageMentionRead)
            {
                if (_chats.TryGetValue(updateMessageMentionRead.ChatId, out Chat value))
                {
                    value.UnreadMentionCount = updateMessageMentionRead.UnreadMentionCount;
                }
            }
            else if (update is UpdateMessageSendAcknowledged updateMessageSendAcknowledged)
            {

            }
            else if (update is UpdateMessageSendFailed updateMessageSendFailed)
            {

            }
            else if (update is UpdateMessageSendSucceeded updateMessageSendSucceeded)
            {

            }
            else if (update is UpdateMessageViews updateMessageViews)
            {

            }
            else if (update is UpdateNewChat updateNewChat)
            {
                _chats[updateNewChat.Chat.Id] = updateNewChat.Chat;

                if (updateNewChat.Chat.Photo != null)
                {
                    if (!(updateNewChat.Chat.Photo.Small.Local.IsDownloadingCompleted && updateNewChat.Chat.Photo.Small.Remote.IsUploadingCompleted))
                    {
                        _chatsMap[updateNewChat.Chat.Photo.Small.Id] = updateNewChat.Chat.Id;
                    }

                    if (!(updateNewChat.Chat.Photo.Big.Local.IsDownloadingCompleted && updateNewChat.Chat.Photo.Big.Remote.IsUploadingCompleted))
                    {
                        _chatsMap[updateNewChat.Chat.Photo.Big.Id] = updateNewChat.Chat.Id;
                    }
                }
            }
            else if (update is UpdateNewMessage updateNewMessage)
            {

            }
            else if (update is UpdateChatNotificationSettings updateNotificationSettings)
            {
                if (_chats.TryGetValue(updateNotificationSettings.ChatId, out Chat value))
                {
                    value.NotificationSettings = updateNotificationSettings.NotificationSettings;
                }
            }
            else if (update is UpdateOption updateOption)
            {
                _options[updateOption.Name] = updateOption.Value;
            }
            else if (update is UpdatePromotedChat updatePromotedChat)
            {
                _promotedChatId = updatePromotedChat.ChatId;
            }
            else if (update is UpdateRecentStickers updateRecentStickers)
            {

            }
            else if (update is UpdateSavedAnimations updateSavedAnimations)
            {

            }
            else if (update is UpdateSecretChat updateSecretChat)
            {
                _secretChats[updateSecretChat.SecretChat.Id] = updateSecretChat.SecretChat;
            }
            else if (update is UpdateServiceNotification updateServiceNotification)
            {

            }
            else if (update is UpdateSupergroup updateSupergroup)
            {
                _supergroups[updateSupergroup.Supergroup.Id] = updateSupergroup.Supergroup;
            }
            else if (update is UpdateSupergroupFullInfo updateSupergroupFullInfo)
            {
                _supergroupsFull[updateSupergroupFullInfo.SupergroupId] = updateSupergroupFullInfo.SupergroupFullInfo;
            }
            else if (update is UpdateTermsOfService updateTermsOfService)
            {

            }
            else if (update is UpdateTrendingStickerSets updateTrendingStickerSets)
            {

            }
            else if (update is UpdateUnreadMessageCount updateUnreadMessageCount)
            {
                UnreadCount = updateUnreadMessageCount.UnreadCount;
                UnreadUnmutedCount = updateUnreadMessageCount.UnreadUnmutedCount;
            }
            else if (update is UpdateUser updateUser)
            {
                _users[updateUser.User.Id] = updateUser.User;

                if (updateUser.User.ProfilePhoto != null)
                {
                    if (!(updateUser.User.ProfilePhoto.Small.Local.IsDownloadingCompleted && updateUser.User.ProfilePhoto.Small.Remote.IsUploadingCompleted))
                    {
                        _usersMap[updateUser.User.ProfilePhoto.Small.Id] = updateUser.User.Id;
                    }

                    if (!(updateUser.User.ProfilePhoto.Big.Local.IsDownloadingCompleted && updateUser.User.ProfilePhoto.Big.Remote.IsUploadingCompleted))
                    {
                        _usersMap[updateUser.User.ProfilePhoto.Big.Id] = updateUser.User.Id;
                    }
                }
            }
            else if (update is UpdateUserChatAction updateUserChatAction)
            {

            }
            else if (update is UpdateUserFullInfo updateUserFullInfo)
            {
                _usersFull[updateUserFullInfo.UserId] = updateUserFullInfo.UserFullInfo;
            }
            else if (update is UpdateUserPrivacySettingRules updateUserPrivacySettingRules)
            {

            }
            else if (update is UpdateUserStatus updateUserStatus)
            {
                if (_users.TryGetValue(updateUserStatus.UserId, out User value))
                {
                    value.Status = updateUserStatus.Status;
                }
            }

            _aggregator.Publish(update);
        }
    }

    public class FileContext<T> : Dictionary<int, List<T>>
    {
        public new List<T> this[int id]
        {
            get
            {
                if (TryGetValue(id, out List<T> items))
                {
                    return items;
                }

                return this[id] = new List<T>();
            }
            set
            {
                base[id] = value;
            }
        }
    }

    public class SimpleFileContext<T> : Dictionary<int, T>
    {
        //public new T this[int id]
        //{
        //    get
        //    {
        //        if (TryGetValue(id, out T item))
        //        {
        //            return item;
        //        }

        //        return this[id] = new List<T>();
        //    }
        //    set
        //    {
        //        base[id] = value;
        //    }
        //}
    }

    static class TdExtensions
    {
        public static void Send(this Client client, Function function, Action<BaseObject> handler)
        {
            client.Send(function, new TdHandler(handler));
        }

        public static void Send(this Client client, Function function)
        {
            client.Send(function, null);
        }

        public static Task<BaseObject> SendAsync(this Client client, Function function)
        {
            var tsc = new TdCompletionSource();
            client.Send(function, tsc);

            return tsc.Task;
        }



        public static bool CodeEquals(this Error error, ErrorCode code)
        {
            if (error == null)
            {
                return false;
            }

            if (Enum.IsDefined(typeof(ErrorCode), error.Code))
            {
                return (ErrorCode)error.Code == code;
            }

            return false;
        }

        public static bool TypeEquals(this Error error, ErrorType type)
        {
            if (error == null || error.Message == null)
            {
                return false;
            }

            var strings = error.Message.Split(':');
            var typeString = strings[0];
            if (Enum.IsDefined(typeof(ErrorType), typeString))
            {
                var value = (ErrorType)Enum.Parse(typeof(ErrorType), typeString, true);

                return value == type;
            }

            return false;
        }
    }

    class TdCompletionSource : TaskCompletionSource<BaseObject>, ClientResultHandler
    {
        public void OnResult(BaseObject result)
        {
            SetResult(result);
        }
    }

    class TdHandler : ClientResultHandler
    {
        private Action<BaseObject> _callback;

        public TdHandler(Action<BaseObject> callback)
        {
            _callback = callback;
        }

        public void OnResult(BaseObject result)
        {
            _callback(result);
        }
    }
}
