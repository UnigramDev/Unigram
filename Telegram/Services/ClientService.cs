//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Entities;
using Telegram.Services.Updates;
using Telegram.Td;
using Telegram.Td.Api;
using Windows.Storage;

namespace Telegram.Services
{
    public interface IClientService : ICacheService
    {
        bool TryInitialize();
        void Close(bool restart);

        //void Send(Function function);
        //void Send(Function function, ClientResultHandler handler);
        void Send(Function function, Action<BaseObject> handler = null);
        Task<BaseObject> SendAsync(Function function);

        Task<BaseObject> CheckChatInviteLinkAsync(string inviteLink);

        Task<StorageFile> GetFileAsync(File file, bool completed = true);
        Task<StorageFile> GetPermanentFileAsync(File file);

        void DownloadFile(int fileId, int priority, int offset = 0, int limit = 0, bool synchronous = false);
        Task<File> DownloadFileAsync(File file, int priority, int offset = 0, int limit = 0);

        void AddFileToDownloads(File file, long chatId, long messageId, int priority = 30);
        void CancelDownloadFile(File file, bool onlyIfPending = false);
        bool IsDownloadFileCanceled(int fileId);

        Task<Chats> GetChatListAsync(ChatList chatList, int offset, int limit);

        int SessionId { get; }

        Client Client { get; }
    }

    public interface ICacheService
    {
        bool IsPremium { get; }
        bool IsPremiumAvailable { get; }

        IOptionsService Options { get; }
        JsonValueObject Config { get; }

        ReactionType DefaultReaction { get; }

        IList<ChatFolderInfo> ChatFolders { get; }
        int MainChatListPosition { get; }

        IList<string> AnimationSearchEmojis { get; }
        string AnimationSearchProvider { get; }

        Background GetSelectedBackground(bool darkTheme);
        Background SelectedBackground { get; }

        AuthorizationState GetAuthorizationState();
        AuthorizationState AuthorizationState { get; }
        ConnectionState GetConnectionState();

        string GetTitle(Chat chat, bool tiny = false);
        string GetTitle(MessageForwardInfo info);

        bool TryGetCachedReaction(string emoji, out EmojiReaction value);
        Task<IDictionary<string, EmojiReaction>> GetAllReactionsAsync();

        Chat GetChat(long id);
        IList<Chat> GetChats(IList<long> ids);

        IDictionary<MessageSender, ChatAction> GetChatActions(long id, long threadId = 0);

        bool IsSavedMessages(MessageSender sender);
        bool IsSavedMessages(User user);
        bool IsSavedMessages(Chat chat);

        bool IsRepliesChat(Chat chat);
        bool IsForum(Chat chat);

        bool IsChatAccessible(Chat chat);

        bool CanPostMessages(Chat chat);

        BaseObject GetMessageSender(MessageSender sender);

        bool TryGetChat(long chatId, out Chat chat);
        bool TryGetChat(MessageSender sender, out Chat value);

        bool TryGetChatFromUser(long userId, out long value);

        SecretChat GetSecretChat(int id);
        SecretChat GetSecretChat(Chat chat);
        SecretChat GetSecretChatForUser(long id);

        User GetUser(Chat chat);
        User GetUser(long id);
        bool TryGetUser(long id, out User value);
        bool TryGetUser(Chat chat, out User value);
        bool TryGetUser(MessageSender sender, out User value);

        UserFullInfo GetUserFull(long id);
        UserFullInfo GetUserFull(Chat chat);
        bool TryGetUserFull(long id, out UserFullInfo value);
        bool TryGetUserFull(Chat chat, out UserFullInfo value);

        IList<User> GetUsers(IEnumerable<long> ids);

        BasicGroup GetBasicGroup(long id);
        BasicGroup GetBasicGroup(Chat chat);
        bool TryGetBasicGroup(long id, out BasicGroup value);
        bool TryGetBasicGroup(Chat chat, out BasicGroup value);

        BasicGroupFullInfo GetBasicGroupFull(long id);
        BasicGroupFullInfo GetBasicGroupFull(Chat chat);
        bool TryGetBasicGroupFull(long id, out BasicGroupFullInfo value);
        bool TryGetBasicGroupFull(Chat chat, out BasicGroupFullInfo value);

        Supergroup GetSupergroup(long id);
        Supergroup GetSupergroup(Chat chat);
        bool TryGetSupergroup(long id, out Supergroup value);
        bool TryGetSupergroup(Chat chat, out Supergroup value);

        SupergroupFullInfo GetSupergroupFull(long id);
        SupergroupFullInfo GetSupergroupFull(Chat chat);
        bool TryGetSupergroupFull(long id, out SupergroupFullInfo value);
        bool TryGetSupergroupFull(Chat chat, out SupergroupFullInfo value);

        ForumTopicInfo GetTopicInfo(long chatId, long messageThreadId);
        bool TryGetTopicInfo(long chatId, long messageThreadId, out ForumTopicInfo value);

        bool IsAnimationSaved(int id);
        bool IsStickerRecent(int id);
        bool IsStickerFavorite(int id);
        bool IsStickerSetInstalled(long id);

        ChatListUnreadCount GetUnreadCount(ChatList chatList);
        void SetUnreadCount(ChatList chatList, UpdateUnreadChatCount chatCount = null, UpdateUnreadMessageCount messageCount = null);

        ChatTheme GetChatTheme(string themeName);
        IList<ChatTheme> GetChatThemes();

        bool IsDiceEmoji(string text, out string dice);

        Settings.NotificationsSettings Notifications { get; }
    }

    public partial class ClientService : IClientService, ClientResultHandler
    {
        readonly struct ChatMessageId
        {
            public readonly long ChatId;
            public readonly long MessageId;

            public ChatMessageId(long chatId, long messageId)
            {
                ChatId = chatId;
                MessageId = messageId;
            }
        }

        private Client _client;

        private readonly int _session;

        private readonly IDeviceInfoService _deviceInfoService;
        private readonly ISettingsService _settings;
        private readonly IOptionsService _options;
        private readonly ILocaleService _locale;
        private readonly IEventAggregator _aggregator;

        private readonly Action<BaseObject> _processFilesDelegate;

        private readonly Dictionary<long, Chat> _chats = new Dictionary<long, Chat>();
        private readonly ConcurrentDictionary<long, ConcurrentDictionary<MessageSender, ChatAction>> _chatActions = new ConcurrentDictionary<long, ConcurrentDictionary<MessageSender, ChatAction>>();
        private readonly ConcurrentDictionary<ChatMessageId, ConcurrentDictionary<MessageSender, ChatAction>> _topicActions = new ConcurrentDictionary<ChatMessageId, ConcurrentDictionary<MessageSender, ChatAction>>();

        private readonly Dictionary<int, SecretChat> _secretChats = new Dictionary<int, SecretChat>();

        private readonly Dictionary<long, long> _usersToChats = new Dictionary<long, long>();

        private readonly Dictionary<long, User> _users = new Dictionary<long, User>();
        private readonly Dictionary<long, UserFullInfo> _usersFull = new Dictionary<long, UserFullInfo>();

        private readonly Dictionary<long, BasicGroup> _basicGroups = new Dictionary<long, BasicGroup>();
        private readonly Dictionary<long, BasicGroupFullInfo> _basicGroupsFull = new Dictionary<long, BasicGroupFullInfo>();

        private readonly Dictionary<long, Supergroup> _supergroups = new Dictionary<long, Supergroup>();
        private readonly Dictionary<long, SupergroupFullInfo> _supergroupsFull = new Dictionary<long, SupergroupFullInfo>();

        private readonly Dictionary<ChatMessageId, ForumTopicInfo> _topics = new Dictionary<ChatMessageId, ForumTopicInfo>();

        private readonly Dictionary<int, ChatListUnreadCount> _unreadCounts = new Dictionary<int, ChatListUnreadCount>();

        private readonly Dictionary<int, File> _files = new Dictionary<int, File>();

        private IList<string> _diceEmojis;

        private IList<int> _savedAnimations;
        private IList<int> _recentStickers;
        private IList<int> _favoriteStickers;
        private IList<long> _installedStickerSets;
        private IList<long> _installedMaskSets;
        private IList<long> _installedEmojiSets;

        private ReactionType _defaultReaction;

        private IList<ChatFolderInfo> _chatFolders = new ChatFolderInfo[0];
        private int _mainChatListPosition = 0;

        private IList<string> _reactions = new List<string>();

        private UpdateAnimationSearchParameters _animationSearchParameters;

        private UpdateChatThemes _chatThemes;

        private AuthorizationState _authorizationState;
        private ConnectionState _connectionState;

        private JsonValueObject _config;

        private Background _selectedBackground;
        private Background _selectedBackgroundDark;

        private bool _initializeAfterClose;

        private static volatile Task _longRunningTask;
        private static readonly object _longRunningLock = new object();

        public ClientService(int session, bool online, IDeviceInfoService deviceInfoService, ISettingsService settings, ILocaleService locale, IEventAggregator aggregator)
        {
            _session = session;
            _deviceInfoService = deviceInfoService;
            _settings = settings;
            _locale = locale;
            _options = new OptionsService(this);
            _aggregator = aggregator;

            _processFilesDelegate = new Action<BaseObject>(ProcessFiles);

            Initialize(online);
        }

        public bool TryInitialize()
        {
            if (_authorizationState is null or AuthorizationStateClosed)
            {
                Initialize();
                return true;
            }

            return false;
        }

        public void Close(bool restart)
        {
            _initializeAfterClose = restart;
            _client.Send(new Close());
        }

        private void Initialize(bool online = true)
        {
            lock (_longRunningLock)
            {
                if (_longRunningTask == null)
                {
                    InitializeDiagnostics();
                    _longRunningTask = Task.Factory.StartNew(Client.Run, TaskCreationOptions.LongRunning);
                }
            }

            _client = Client.Create(this);

#if MOCKUP
            ProfilePhoto ProfilePhoto(string name)
            {
                return new ProfilePhoto(0, new Telegram.Td.Api.File(0, 0, 0, new LocalFile(System.IO.Path.Combine(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Assets\\Mockup\\", name), true, true, false, true, 0, 0, 0), null), null, null, false, false);
            }

            ChatPhotoInfo ChatPhoto(string name)
            {
                return new ChatPhotoInfo(new Telegram.Td.Api.File(0, 0, 0, new LocalFile(System.IO.Path.Combine(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Assets\\Mockup\\", name), true, true, false, true, 0, 0, 0), null), null, null, false, false);
            }

            _users[00] = new User(00, "Alicia",   "Torreaux",   null, string.Empty, null,                               ProfilePhoto("Avatar1.png"),  null, false, false, false, false, false, string.Empty, false, false, false, new UserTypeRegular(), string.Empty, false);
            _users[01] = new User(01, "Roberto",  string.Empty, null, string.Empty, null,                               ProfilePhoto("Avatar2.png"),  null, false, false, false, true,  false, string.Empty, false, false, false, new UserTypeRegular(), string.Empty, false);
            _users[02] = new User(02, "Veronica", string.Empty, null, string.Empty, new UserStatusOnline(int.MaxValue), ProfilePhoto("Avatar4.png"),  null, false, false, false, false, false, string.Empty, false, false, false, new UserTypeRegular(), string.Empty, false);
            _users[03] = new User(03, "Little",   "Sister",     null, string.Empty, null,                               ProfilePhoto("Avatar5.png"),  null, false, false, false, false, false, string.Empty, false, false, false, new UserTypeRegular(), string.Empty, false);
            _users[04] = new User(04, "Lucy",     "Garner",     null, string.Empty, null,                               ProfilePhoto("Avatar7.png"),  null, false, false, false, true,  false, string.Empty, false, false, false, new UserTypeRegular(), string.Empty, false);
            _users[05] = new User(05, "James",    string.Empty, null, string.Empty, null,                               ProfilePhoto("Avatar8.png"),  null, false, false, false, false, false, string.Empty, false, false, false, new UserTypeRegular(), string.Empty, false);
            _users[06] = new User(06, "James",    string.Empty, null, string.Empty, null,                               ProfilePhoto("Avatar12.png"), null, false, false, false, true,  false, string.Empty, false, false, false, new UserTypeRegular(), string.Empty, false);
            _users[07] = new User(07, "Y",        string.Empty, null, string.Empty, null,                               ProfilePhoto("Avatar11.png"), null, false, false, false, false, false, string.Empty, false, false, false, new UserTypeRegular(), string.Empty, false);
            _users[08] = new User(08, "Roxanne",  "\U0001F3AE", null, string.Empty, null,                               ProfilePhoto("Avatar10.png"), null, false, false, false, false, false, string.Empty, false, false, false, new UserTypeRegular(), string.Empty, false);
            _users[09] = new User(09, "Jennie",   string.Empty, null, string.Empty, null,                               ProfilePhoto("Avatar9.png"),  null, false, false, false, true,  false, string.Empty, false, false, false, new UserTypeRegular(), string.Empty, false);
            _users[10] = new User(10, "Alex",     "Hunter",     null, string.Empty, null,                               ProfilePhoto("Avatar13.png"), null, false, false, false, false, false, string.Empty, false, false, false, new UserTypeRegular(), string.Empty, false);
            _users[11] = new User(11, "X",        string.Empty, null, string.Empty, null,                               ProfilePhoto("Avatar14.png"), null, false, false, false, false, false, string.Empty, false, false, false, new UserTypeRegular(), string.Empty, false);

            _secretChats[1] = new SecretChat(1, 3, new SecretChatStateReady(), false, new byte[0], 75);

            _supergroups[0 ] = new Supergroup(0,  null, 0, new ChatMemberStatusMember(), 2503, false, false, false, false, false, false, false, false, false, false, string.Empty, false, false);
            _supergroups[1 ] = new Supergroup(1,  null, 0, new ChatMemberStatusMember(), 0,    false, false, false, false, false, false, true,  false, false, false, string.Empty, false, false);
            _supergroups[2 ] = new Supergroup(2,  null, 0, new ChatMemberStatusMember(), 7,    false, false, false, false, false, false, false, false, false, false, string.Empty, false, false);
            _supergroups[3 ] = new Supergroup(3,  null, 0, new ChatMemberStatusMember(), 0,    false, false, false, false, false, false, true,  false, false, true,  string.Empty, false, false);
            _supergroups[4 ] = new Supergroup(4,  null, 0, new ChatMemberStatusMember(), 0,    false, false, false, false, false, false, true,  false, false, true,  string.Empty, false, false);
            _supergroups[5 ] = new Supergroup(5,  null, 0, new ChatMemberStatusMember(), 0,    false, false, false, false, false, false, true,  false, false, true,  string.Empty, false, false);
            _supergroups[6 ] = new Supergroup(6,  null, 0, new ChatMemberStatusMember(), 0,    false, false, false, false, false, false, true,  false, false, true,  string.Empty, false, false);
            _supergroups[7 ] = new Supergroup(7,  null, 0, new ChatMemberStatusMember(), 0,    false, false, false, false, false, false, true,  false, false, false, string.Empty, false, false);
            _supergroups[8 ] = new Supergroup(8,  null, 0, new ChatMemberStatusMember(), 0,    false, false, false, false, false, false, true,  false, false, true,  string.Empty, false, false);
            _supergroups[9 ] = new Supergroup(9,  null, 0, new ChatMemberStatusMember(), 0,    false, false, false, false, false, false, true,  false, false, false, string.Empty, false, false);
            _supergroups[10] = new Supergroup(10, null, 0, new ChatMemberStatusMember(), 0,    false, false, false, false, false, false, true,  false, false, true,  string.Empty, false, false);

            int TodayDate(int hour, int minute)
            {
                var dateTime = DateTime.Now.Date.AddHours(hour).AddMinutes(minute);

                var dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                DateTime.SpecifyKind(dtDateTime, DateTimeKind.Utc);

                return (int)(dateTime.ToUniversalTime() - dtDateTime).TotalSeconds;
            }

            int TuesdayDate()
            {
                var last = DateTime.Now;
                do
                {
                    last = last.AddDays(-1);
                }
                while (last.DayOfWeek != DayOfWeek.Tuesday);

                var dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                DateTime.SpecifyKind(dtDateTime, DateTimeKind.Utc);

                return (int)(last.ToUniversalTime() - dtDateTime).TotalSeconds;
            }

            var lastMessage0  = new Message(long.MaxValue, new MessageSenderUser(0),  0,  null, null, true, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(21, 41),  0, null, null, null, 0, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("Bob says hi.", new TextEntity[0]), null), null);
            var lastMessage1  = new Message(long.MaxValue, new MessageSenderUser(1),  1,  null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(21, 41),  0, null, null, null, 0, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("Say hello to Alice.", new TextEntity[0]), null), null);
            var lastMessage2  = new Message(long.MaxValue, new MessageSenderUser(9),  2,  null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(21, 41),  0, null, null, null, 0, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("Sometimes possession is an abstract concept. They took my purse, but the...", new TextEntity[0]), null), null);
            var lastMessage3  = new Message(long.MaxValue, new MessageSenderUser(3),  3,  null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(21, 22),  0, null, null, null, 0, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageVideo(new Sticker(0, 0, 0, "😍", null, null, null, null, null), new FormattedText("Moar ct videos in this channel?", new TextEntity[0]), false, false), null);
            var lastMessage4  = new Message(long.MaxValue, new MessageSenderUser(4),  4,  null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(21, 12),  0, null, null, null, 0, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("Don't tell mom yet, but I got the job! I'm going to ROME!", new TextEntity[0]), null), null);
            var lastMessage5  = new Message(long.MaxValue, new MessageSenderUser(5),  5,  null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(20, 28),  0, null, null, null, 0, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageDocument(new FormattedText("I looove new Surfaces! If fact, they invited me to a focus group.", new TextEntity[0]), null), null);
            var lastMessage6  = new Message(long.MaxValue, new MessageSenderUser(6),  6,  null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(19, 36),  0, null, null, null, 0, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageVideoNote(null, false, false), null);
            var lastMessage7  = new Message(long.MaxValue, new MessageSenderUser(7),  7,  null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TuesdayDate(),      0, null, null, null, 0, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessagePhoto(new Document("LaserBlastSafetyGuide.pdf", string.Empty, null, null, null), new FormattedText(string.Empty, new TextEntity[0])), null);
            var lastMessage8  = new Message(long.MaxValue, new MessageSenderUser(8),  8,  null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TuesdayDate(),      0, null, null, null, 0, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("It's impossible.", new TextEntity[0]), null), null);
            var lastMessage9  = new Message(long.MaxValue, new MessageSenderUser(9),  9,  null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TuesdayDate(),      0, null, null, null, 0, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("Hola!", new TextEntity[0]), null), null);
            var lastMessage10 = new Message(long.MaxValue, new MessageSenderUser(17), 12, null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TuesdayDate(),      0, null, null, null, 0, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("Let's design more robust memes", new TextEntity[0]), null), null);
            var lastMessage11 = new Message(long.MaxValue, new MessageSenderUser(18), 13, null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TuesdayDate(),      0, null, null, null, 0, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("What?! 😱", new TextEntity[0]), null), null);
            var lastMessage12 = new Message(long.MaxValue, new MessageSenderUser(8),  9,  null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(15, 30),  0, null, null, null, 0, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("Wait, we could have made so much money on this!", new TextEntity[0]), null), null);

            var permissions = new ChatPermissions(true, true, true, true, true, true, true, true);

            _chats[ 0] = new Chat( 0, new ChatTypeSupergroup(0, true),      "Unigram News",     ChatPhoto("a0.png"),    permissions, lastMessage0,     new[] { new ChatPosition(new ChatListMain(), long.MaxValue - 0,  true,  null) }, null, false, false, false, false, false, false, false, false, 0, 0, 0, 0, 0, new ChatNotificationSettings(false, 0, false, 0, false, true, true, true, true, true),             null, 0, string.Empty, null, new VideoChat(), null, 0, null, string.Empty);
            _chats[ 1] = new Chat( 1, new ChatTypePrivate(0),               "Jane",             ChatPhoto("a6.png"),    permissions, lastMessage1,     new[] { new ChatPosition(new ChatListMain(), long.MaxValue - 1,  true,  null) }, null, false, false, false, false, false, false, false, false, 0, 0, 0, 0, 0, new ChatNotificationSettings(false, 0, false, 0, false, true, true, true, true, true),             null, 0, string.Empty, null, new VideoChat(), null, 0, null, string.Empty);
            _chats[ 2] = new Chat( 2, new ChatTypePrivate(1),               "Tyrion Lannister", null,                   permissions, lastMessage2,     new[] { new ChatPosition(new ChatListMain(), long.MaxValue - 2,  false, null) }, null, false, false, false, false, false, false, false, false, 1, 0, 0, 0, 0, new ChatNotificationSettings(false, 0, false, 0, false, true, true, true, true, true),             null, 0, string.Empty, null, new VideoChat(), null, 0, null, string.Empty);
            _chats[ 3] = new Chat( 3, new ChatTypePrivate(2),               "Alena Shy",        ChatPhoto("a7.png"),    permissions, lastMessage3,     new[] { new ChatPosition(new ChatListMain(), long.MaxValue - 3,  false, null) }, null, false, false, false, false, false, false, false, false, 0, 0, 0, 0, 0, new ChatNotificationSettings(false, 0, false, 0, false, true, true, true, true, true),             null, 0, string.Empty, null, new VideoChat(), null, 0, null, string.Empty);
            _chats[ 4] = new Chat( 4, new ChatTypeSecret(0, 3),             "Heisenberg",       ChatPhoto("a8.png"),    permissions, lastMessage4,     new[] { new ChatPosition(new ChatListMain(), long.MaxValue - 4,  false, null) }, null, false, false, false, false, false, false, false, false, 0, 0, 0, 0, 0, new ChatNotificationSettings(false, 0, false, 0, false, true, true, true, true, true),             null, 0, string.Empty, null, new VideoChat(), null, 0, null, string.Empty);
            _chats[ 5] = new Chat( 5, new ChatTypePrivate(4),               "Bender",           ChatPhoto("a9.png"),    permissions, lastMessage5,     new[] { new ChatPosition(new ChatListMain(), long.MaxValue - 6,  false, null) }, null, false, false, false, false, false, false, false, false, 0, 0, 0, 0, 0, new ChatNotificationSettings(false, 0, false, 0, false, true, true, true, true, true),             null, 0, string.Empty, null, new VideoChat(), null, 0, null, string.Empty);
            _chats[ 6] = new Chat( 6, new ChatTypeSupergroup(1, true),      "World News Today", ChatPhoto("a10.png"),   permissions, lastMessage6,     new[] { new ChatPosition(new ChatListMain(), long.MaxValue - 7,  false, null) }, null, false, false, false, false, false, false, false, false, 1, 0, 0, 0, 0, new ChatNotificationSettings(false, int.MaxValue, false, 0, false, true, true, true, true, true),  null, 0, string.Empty, null, new VideoChat(), null, 0, null, string.Empty);
            _chats[ 7] = new Chat( 7, new ChatTypePrivate(5),               "EVE",              ChatPhoto("a11.png"),   permissions, lastMessage7,     new[] { new ChatPosition(new ChatListMain(), long.MaxValue - 8,  false, null) }, null, false, false, false, false, false, false, false, false, 0, 0, 0, 0, 0, new ChatNotificationSettings(false, 0, false, 0, false, true, true, true, true, true),             null, 0, string.Empty, null, new VideoChat(), null, 0, null, string.Empty);
            _chats[ 8] = new Chat( 8, new ChatTypePrivate(16),              "Nick",             null,                   permissions, lastMessage8,     new[] { new ChatPosition(new ChatListMain(), long.MaxValue - 9,  false, null) }, null, false, false, false, false, false, false, false, false, 0, 0, 0, 0, 0, new ChatNotificationSettings(false, 0, false, 0, false, true, true, true, true, true),             null, 0, string.Empty, null, new VideoChat(), null, 0, null, string.Empty);
            _chats[11] = new Chat(11, new ChatTypePrivate(16),              "Kate Rodriguez",   ChatPhoto("a13.png"),   permissions, lastMessage9,     new[] { new ChatPosition(new ChatListMain(), long.MaxValue - 10,  false, null) }, null, false, false, false, false, false, false, false, false, 0, 0, 0, 0, 0, new ChatNotificationSettings(false, 0, false, 0, false, true, true, true, true, true),             null, 0, string.Empty, null, new VideoChat(), null, 0, null, string.Empty);
            _chats[12] = new Chat(12, new ChatTypeSupergroup(3, false),     "Meme Factory",     ChatPhoto("a14.png"),   permissions, lastMessage10,    new[] { new ChatPosition(new ChatListMain(), long.MaxValue - 11, false, null) }, null, false, false, false, false, false, false, false, false, 0, 0, 0, 0, 0, new ChatNotificationSettings(false, 0, false, 0, false, true, true, true, true, true),             null, 0, string.Empty, null, new VideoChat(), null, 0, null, string.Empty);
            _chats[13] = new Chat(13, new ChatTypePrivate(18),              "Jaina Moore",      null,                   permissions, lastMessage11,    new[] { new ChatPosition(new ChatListMain(), long.MaxValue - 12, false, null) }, null, false, false, false, false, false, false, false, false, 0, 0, 0, 0, 0, new ChatNotificationSettings(false, 0, false, 0, false, true, true, true, true, true),             null, 0, string.Empty, null, new VideoChat(), null, 0, null, string.Empty);

            _chats[ 9] = new Chat( 9, new ChatTypeSupergroup(2, false),        "Weekend Plans", ChatPhoto("a4.png"),    permissions, lastMessage12,             new [] { new ChatPosition(new ChatListMain(), long.MaxValue - 5, false, null) },                 null, false, false, false, false, false, false, false, false, 0, 0, long.MaxValue, 0, 0, new ChatNotificationSettings(false, 0, false, 0, false, true, true, true, true, true), null, 0, string.Empty, null, new VideoChat(), null, 0, null, string.Empty);
            _chats[10] = new Chat(10, new ChatTypeSecret(1, 7), "Eileen Lockhard \uD83D\uDC99", ChatPhoto("a5.png"),    permissions, null,             new [] { new ChatPosition(new ChatListMain(), 0, false, null) },                 null, false, false, false, false, false, false, false, false, 0, 0, long.MaxValue, 0, 0, new ChatNotificationSettings(false, 0, false, 0, false, true, true, true, true, true), null, 0, string.Empty, null, new VideoChat(), null, 0, null, string.Empty);

            _chatList[0].Add(new OrderedChat( 0, new ChatPosition(new ChatListMain(), int.MaxValue -  0, false, null)));
            _chatList[0].Add(new OrderedChat( 1, new ChatPosition(new ChatListMain(), int.MaxValue -  1, false, null)));
            _chatList[0].Add(new OrderedChat( 2, new ChatPosition(new ChatListMain(), int.MaxValue -  2, false, null)));
            _chatList[0].Add(new OrderedChat( 3, new ChatPosition(new ChatListMain(), int.MaxValue -  3, false, null)));
            _chatList[0].Add(new OrderedChat( 4, new ChatPosition(new ChatListMain(), int.MaxValue -  4, false, null)));
            _chatList[0].Add(new OrderedChat( 9, new ChatPosition(new ChatListMain(), int.MaxValue -  5, false, null)));
            _chatList[0].Add(new OrderedChat( 5, new ChatPosition(new ChatListMain(), int.MaxValue -  6, false, null)));
            _chatList[0].Add(new OrderedChat( 6, new ChatPosition(new ChatListMain(), int.MaxValue -  7, false, null)));
            _chatList[0].Add(new OrderedChat( 7, new ChatPosition(new ChatListMain(), int.MaxValue -  8, false, null)));
            _chatList[0].Add(new OrderedChat( 8, new ChatPosition(new ChatListMain(), int.MaxValue -  9, false, null)));
            _chatList[0].Add(new OrderedChat(10, new ChatPosition(new ChatListMain(), int.MaxValue - 10, false, null)));
            _chatList[0].Add(new OrderedChat(11, new ChatPosition(new ChatListMain(), int.MaxValue - 11, false, null)));
            _chatList[0].Add(new OrderedChat(12, new ChatPosition(new ChatListMain(), int.MaxValue - 12, false, null)));
            _chatList[0].Add(new OrderedChat(13, new ChatPosition(new ChatListMain(), int.MaxValue - 13, false, null)));
#endif

            Task.Factory.StartNew(() =>
            {
                var useMessageDatabase = true;

                if (_settings.Diagnostics.DisableDatabase)
                {
                    // ¯\_(ツ)_/¯
                    useMessageDatabase = false;
                }

                var deviceModel = SettingsService.Current.Diagnostics.DeviceName;
                if (deviceModel.Length == 0)
                {
                    deviceModel = _deviceInfoService.DeviceModel;
                }

                InitializeDiagnostics();
                InitializeFlush();

                _client.Send(new SetOption("ignore_background_updates", new OptionValueBoolean(_settings.Diagnostics.DisableDatabase)));
                _client.Send(new SetOption("language_pack_database_path", new OptionValueString(System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, "langpack"))));
                _client.Send(new SetOption("localization_target", new OptionValueString(LocaleService.LANGPACK)));
                _client.Send(new SetOption("language_pack_id", new OptionValueString(SettingsService.Current.LanguagePackId)));
                //_client.Send(new SetOption("online", new OptionValueBoolean(online)));
                _client.Send(new SetOption("online", new OptionValueBoolean(false)));
                _client.Send(new SetOption("use_pfs", new OptionValueBoolean(true)));
                _client.Send(new SetOption("notification_group_count_max", new OptionValueInteger(25)));
                _client.Send(new SetOption("storage_max_time_from_last_access", new OptionValueInteger(SettingsService.Current.Diagnostics.StorageMaxTimeFromLastAccess)));
                _client.Send(new SetTdlibParameters
                {
                    DatabaseDirectory = System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, $"{_session}"),
                    UseSecretChats = true,
                    UseMessageDatabase = useMessageDatabase,
                    ApiId = Constants.ApiId,
                    ApiHash = Constants.ApiHash,
                    ApplicationVersion = _deviceInfoService.ApplicationVersion,
                    SystemVersion = _deviceInfoService.SystemVersion,
                    SystemLanguageCode = _deviceInfoService.SystemLanguageCode,
                    DeviceModel = deviceModel,
                    UseTestDc = _settings.UseTestDC,
                    EnableStorageOptimizer = SettingsService.Current.Diagnostics.UseStorageOptimizer
                });
                _client.Send(new GetApplicationConfig(), UpdateConfig);
            });
        }

        private void InitializeDiagnostics()
        {
            Client.Execute(new SetLogStream(new LogStreamFile(System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, "tdlib_log.txt"), 100 * 1024 * 1024, false)));
            Client.Execute(new SetLogVerbosityLevel(SettingsService.Current.VerbosityLevel));

            var tags = Client.Execute(new GetLogTags()) as LogTags;
            if (tags == null)
            {
                return;
            }

            foreach (var tag in tags.Tags)
            {
                var level = Client.Execute(new GetLogTagVerbosityLevel(tag)) as LogVerbosityLevel;

                var saved = _settings.Diagnostics.GetValueOrDefault(tag, -1);
                if (tag == "td_init")
                {
                    saved = 1;
                }

                if (saved != level.VerbosityLevel && saved > -1)
                {
                    Client.Execute(new SetLogTagVerbosityLevel(tag, saved));
                }
            }
        }

        private void InitializeReady()
        {
            Send(new LoadChats(new ChatListMain(), 20));

            UpdateVersion();

            //InitializeStickers();
        }

        private void InitializeFlush()
        {
            // Flush animated stickers cache files that have not been accessed in three days
            Task.Factory.StartNew(() =>
            {
                var now = DateTime.Now;
                var path = System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, $"{_session}", "stickers");

                if (System.IO.Directory.Exists(path))
                {
                    var files = System.IO.Directory.GetFiles(path, "*.cache");

                    foreach (var file in files)
                    {
                        var date = System.IO.File.GetLastAccessTime(file);

                        var diff = now - date;
                        if (diff.TotalDays >= 3)
                        {
                            System.IO.File.Delete(file);
                        }
                    }
                }
            });
        }

        private void UpdateConfig(BaseObject value)
        {
            if (value is JsonValueObject obj)
            {
                _config = obj;
            }
        }

        private void UpdateVersion()
        {
            ulong major = (_settings.Version & 0xFFFF000000000000L) >> 48;
            ulong minor = (_settings.Version & 0x0000FFFF00000000L) >> 32;
            ulong revision = (_settings.Version & 0x00000000FFFF0000L) >> 16;

            _settings.UpdateVersion();
            Send(new AddApplicationChangelog($"{major}.{minor}.{revision}"));
        }

        public void CleanUp()
        {
            _options.Clear();

            _files.Clear();

            _reactions.Clear();

            _chats.Clear();
            _chatActions.Clear();

            _secretChats.Clear();

            _usersToChats.Clear();

            _users.Clear();
            _usersFull.Clear();

            _basicGroups.Clear();
            _basicGroupsFull.Clear();

            _supergroups.Clear();
            _supergroupsFull.Clear();

            _settings.Notifications.Scope.Clear();

            _unreadCounts.Clear();

            _diceEmojis = null;

            _savedAnimations = null;
            _favoriteStickers = null;
            _installedStickerSets = null;
            _installedMaskSets = null;
            _installedEmojiSets = null;

            _chatFolders = new ChatFolderInfo[0];

            _animationSearchParameters = null;

            _authorizationState = null;
            _connectionState = null;

            if (_initializeAfterClose)
            {
                _initializeAfterClose = false;
                Initialize();
            }
        }



        public void Send(Function function, Action<BaseObject> handler = null)
        {
            if (handler != null)
            {
                _client.Send(function, _processFilesDelegate + handler);
            }
            else
            {
                _client.Send(function, _processFilesDelegate);
            }
        }

        public Task<BaseObject> SendAsync(Function function)
        {
            return _client.SendAsync(function, ProcessFiles);
        }



        private readonly Dictionary<long, DateTime> _chatAccessibleUntil = new Dictionary<long, DateTime>();

        public async Task<BaseObject> CheckChatInviteLinkAsync(string inviteLink)
        {
            var response = await SendAsync(new CheckChatInviteLink(inviteLink));
            if (response is ChatInviteLinkInfo info)
            {
                if (info.ChatId != 0 && info.AccessibleFor != 0)
                {
                    _chatAccessibleUntil[info.ChatId] = DateTime.Now.AddSeconds(info.AccessibleFor);
                }
                else
                {
                    _chatAccessibleUntil.Remove(info.ChatId);
                }
            }

            return response;
        }



        public void DownloadFile(int fileId, int priority, int offset = 0, int limit = 0, bool synchronous = false)
        {
            Send(new DownloadFile(fileId, priority, offset, limit, synchronous));
        }

        public async Task<File> DownloadFileAsync(File file, int priority, int offset = 0, int limit = 0)
        {
            var response = await SendAsync(new DownloadFile(file.Id, priority, offset, limit, true));
            if (response is File updated)
            {
                return ProcessFile(updated);
            }

            return file;
        }



        public int SessionId => _session;

        public Client Client => _client;

        #region Cache

        public ChatListUnreadCount GetUnreadCount(ChatList chatList)
        {
            var id = GetIdFromChatList(chatList);
            if (_unreadCounts.TryGetValue(id, out ChatListUnreadCount value))
            {
                return value;
            }

            return _unreadCounts[id] = new ChatListUnreadCount
            {
                ChatList = chatList ?? new ChatListMain(),
                UnreadChatCount = new UpdateUnreadChatCount(),
                UnreadMessageCount = new UpdateUnreadMessageCount()
            };
        }

        public void SetUnreadCount(ChatList chatList, UpdateUnreadChatCount chatCount = null, UpdateUnreadMessageCount messageCount = null)
        {
            var id = GetIdFromChatList(chatList);
            if (_unreadCounts.TryGetValue(id, out ChatListUnreadCount value))
            {
                value.UnreadChatCount = chatCount ?? value.UnreadChatCount;
                value.UnreadMessageCount = messageCount ?? value.UnreadMessageCount;

                return;
            }

            _unreadCounts[id] = new ChatListUnreadCount
            {
                ChatList = chatList ?? new ChatListMain(),
                UnreadChatCount = chatCount ?? new UpdateUnreadChatCount(),
                UnreadMessageCount = messageCount ?? new UpdateUnreadMessageCount()
            };
        }

        private int GetIdFromChatList(ChatList chatList)
        {
            if (chatList is ChatListMain or null)
            {
                return 0;
            }
            else if (chatList is ChatListArchive)
            {
                return 1;
            }
            else if (chatList is ChatListFolder folder)
            {
                return folder.ChatFolderId;
            }

            return -1;
        }



        public AuthorizationState GetAuthorizationState()
        {
            return _authorizationState;
        }

        public AuthorizationState AuthorizationState => _authorizationState;

        public Settings.NotificationsSettings Notifications => _settings.Notifications;

        public ConnectionState GetConnectionState()
        {
            return _connectionState;
        }

        public bool IsPremium => _options.IsPremium;

        public bool IsPremiumAvailable => _options.IsPremium || _options.IsPremiumAvailable;

        public IOptionsService Options => _options;

        public JsonValueObject Config => _config;

        public ReactionType DefaultReaction => _defaultReaction;

        public IList<ChatFolderInfo> ChatFolders => _chatFolders;

        public int MainChatListPosition => _mainChatListPosition;

        public IList<string> AnimationSearchEmojis => _animationSearchParameters?.Emojis ?? new string[0];

        public string AnimationSearchProvider => _animationSearchParameters?.Provider;

        public Background SelectedBackground => GetSelectedBackground(_settings.Appearance.IsDarkTheme());

        public Background GetSelectedBackground(bool darkTheme)
        {
            if (darkTheme)
            {
                return _selectedBackgroundDark;
            }

            return _selectedBackground;
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
                    return Strings.HiddenName;
                }
                else if (user.Id == _options.MyId)
                {
                    return Strings.SavedMessages;
                }
                else if (chat.Id == _options.RepliesBotChatId)
                {
                    return Strings.RepliesTitle;
                }
                else if (tiny)
                {
                    return user.FirstName;
                }
            }

            return chat.Title;
        }

        public string GetTitle(MessageForwardInfo info)
        {
            if (info?.Origin is MessageForwardOriginUser fromUser)
            {
                return GetUser(fromUser.SenderUserId)?.FullName();
            }
            else if (info?.Origin is MessageForwardOriginChat fromChat)
            {
                return GetTitle(GetChat(fromChat.SenderChatId));
            }
            else if (info?.Origin is MessageForwardOriginChannel fromChannel)
            {
                return GetTitle(GetChat(fromChannel.ChatId));
            }
            else if (info?.Origin is MessageForwardOriginMessageImport fromImport)
            {
                return fromImport.SenderName;
            }
            else if (info?.Origin is MessageForwardOriginHiddenUser fromHiddenUser)
            {
                return fromHiddenUser.SenderName;
            }

            return null;
        }

        public bool TryGetCachedReaction(string emoji, out EmojiReaction value)
        {
            value = null;
            return false;
        }

        public async Task<IDictionary<string, EmojiReaction>> GetAllReactionsAsync()
        {
            var result = new Dictionary<string, EmojiReaction>();

            foreach (var emoji in _reactions)
            {
                var response = await SendAsync(new GetEmojiReaction(emoji));
                if (response is EmojiReaction reaction)
                {
                    result[emoji] = reaction;
                }
            }

            return result;
        }

        public Chat GetChat(long id)
        {
            if (_chats.TryGetValue(id, out Chat value))
            {
                return value;
            }

            return null;
        }

        public IDictionary<MessageSender, ChatAction> GetChatActions(long id, long threadId = 0)
        {
            if (threadId != 0)
            {
                if (_topicActions.TryGetValue(new ChatMessageId(id, threadId), out ConcurrentDictionary<MessageSender, ChatAction> value))
                {
                    return value;
                }
            }
            else if (_chatActions.TryGetValue(id, out ConcurrentDictionary<MessageSender, ChatAction> value))
            {
                return value;
            }

            return null;
        }

        public bool IsSavedMessages(MessageSender sender)
        {
            if (sender is MessageSenderUser user)
            {
                return user.UserId == _options.MyId;
            }
            else if (sender is MessageSenderChat chat)
            {
                return chat.ChatId == _options.MyId;
            }

            return false;
        }

        public bool IsSavedMessages(User user)
        {
            return user?.Id == _options.MyId;
        }

        public bool IsSavedMessages(Chat chat)
        {
            if (chat?.Type is ChatTypePrivate privata && privata.UserId == _options.MyId)
            {
                return true;
            }

            return false;
        }

        public bool IsRepliesChat(Chat chat)
        {
            return chat.Id == _options.RepliesBotChatId;
        }

        public bool IsForum(Chat chat)
        {
            if (TryGetSupergroup(chat, out Supergroup supergroup))
            {
                return supergroup.IsForum;
            }

            return false;
        }

        public bool IsChatAccessible(Chat chat)
        {
            // This method is definitely misleading, and it should probably cover more cases
            if (_chatAccessibleUntil.TryGetValue(chat.Id, out DateTime until))
            {
                return until > DateTime.Now;
            }

            return false;
        }

        public bool CanPostMessages(Chat chat)
        {
            if (TryGetSupergroup(chat, out Supergroup supergroup))
            {
                return supergroup.CanPostMessages();
            }
            else if (TryGetBasicGroup(chat, out BasicGroup basicGroup))
            {
                return basicGroup.CanPostMessages();
            }

            // TODO: secret chats maybe?

            return true;
        }

        public BaseObject GetMessageSender(MessageSender sender)
        {
            if (sender is MessageSenderUser user)
            {
                return GetUser(user.UserId);
            }
            else if (sender is MessageSenderChat chat)
            {
                return GetChat(chat.ChatId);
            }

            return null;
        }

        public bool TryGetChat(long chatId, out Chat chat)
        {
            chat = GetChat(chatId);
            return chat != null;
        }

        public bool TryGetChat(MessageSender sender, out Chat value)
        {
            if (sender is MessageSenderChat senderChat)
            {
                return TryGetChat(senderChat.ChatId, out value);
            }

            value = null;
            return false;
        }

        public bool TryGetChatFromUser(long userId, out long value)
        {
            return _usersToChats.TryGetValue(userId, out value);
        }

        public IList<Chat> GetChats(IList<long> ids)
        {
#if MOCKUP
            return _chats.Values.ToList();
#endif

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

        public IList<User> GetUsers(IEnumerable<long> ids)
        {
            var result = new List<User>();

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

        public SecretChat GetSecretChat(int id)
        {
            if (_secretChats.TryGetValue(id, out SecretChat value))
            {
                return value;
            }

            return null;
        }

        public SecretChat GetSecretChat(Chat chat)
        {
            if (chat?.Type is ChatTypeSecret secret)
            {
                return GetSecretChat(secret.SecretChatId);
            }

            return null;
        }

        public SecretChat GetSecretChatForUser(long id)
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

        public User GetUser(long id)
        {
            if (_users.TryGetValue(id, out User value))
            {
                return value;
            }

            return null;
        }

        public bool TryGetUser(long id, out User value)
        {
            return _users.TryGetValue(id, out value);
        }

        public bool TryGetUser(MessageSender sender, out User value)
        {
            if (sender is MessageSenderUser senderUser)
            {
                return TryGetUser(senderUser.UserId, out value);
            }

            value = null;
            return false;
        }

        public bool TryGetUser(Chat chat, out User value)
        {
            if (chat?.Type is ChatTypePrivate privata)
            {
                return TryGetUser(privata.UserId, out value);
            }
            else if (chat?.Type is ChatTypeSecret secret)
            {
                return TryGetUser(secret.UserId, out value);
            }

            value = null;
            return false;
        }



        public UserFullInfo GetUserFull(long id)
        {
            if (_usersFull.TryGetValue(id, out UserFullInfo value))
            {
                return value;
            }

            return null;
        }

        public UserFullInfo GetUserFull(Chat chat)
        {
            if (chat?.Type is ChatTypePrivate privata)
            {
                return GetUserFull(privata.UserId);
            }
            else if (chat?.Type is ChatTypeSecret secret)
            {
                return GetUserFull(secret.UserId);
            }

            return null;
        }

        public bool TryGetUserFull(long id, out UserFullInfo value)
        {
            return _usersFull.TryGetValue(id, out value);
        }

        public bool TryGetUserFull(Chat chat, out UserFullInfo value)
        {
            if (chat?.Type is ChatTypePrivate privata)
            {
                return TryGetUserFull(privata.UserId, out value);
            }
            else if (chat?.Type is ChatTypeSecret secret)
            {
                return TryGetUserFull(secret.UserId, out value);
            }

            value = null;
            return false;
        }



        public BasicGroup GetBasicGroup(long id)
        {
            if (_basicGroups.TryGetValue(id, out BasicGroup value))
            {
                return value;
            }

            return null;
        }

        public BasicGroup GetBasicGroup(Chat chat)
        {
            if (chat?.Type is ChatTypeBasicGroup basicGroup)
            {
                return GetBasicGroup(basicGroup.BasicGroupId);
            }

            return null;
        }

        public bool TryGetBasicGroup(long id, out BasicGroup value)
        {
            return _basicGroups.TryGetValue(id, out value);
        }

        public bool TryGetBasicGroup(Chat chat, out BasicGroup value)
        {
            if (chat?.Type is ChatTypeBasicGroup basicGroup)
            {
                return TryGetBasicGroup(basicGroup.BasicGroupId, out value);
            }

            value = null;
            return false;
        }



        public BasicGroupFullInfo GetBasicGroupFull(long id)
        {
            if (_basicGroupsFull.TryGetValue(id, out BasicGroupFullInfo value))
            {
                return value;
            }

            return null;
        }

        public BasicGroupFullInfo GetBasicGroupFull(Chat chat)
        {
            if (chat?.Type is ChatTypeBasicGroup basicGroup)
            {
                return GetBasicGroupFull(basicGroup.BasicGroupId);
            }

            return null;
        }

        public bool TryGetBasicGroupFull(long id, out BasicGroupFullInfo value)
        {
            return _basicGroupsFull.TryGetValue(id, out value);
        }

        public bool TryGetBasicGroupFull(Chat chat, out BasicGroupFullInfo value)
        {
            if (chat?.Type is ChatTypeBasicGroup basicGroup)
            {
                return TryGetBasicGroupFull(basicGroup.BasicGroupId, out value);
            }

            value = null;
            return false;
        }



        public Supergroup GetSupergroup(long id)
        {
            if (_supergroups.TryGetValue(id, out Supergroup value))
            {
                return value;
            }

            return null;
        }

        public Supergroup GetSupergroup(Chat chat)
        {
            if (chat?.Type is ChatTypeSupergroup supergroup)
            {
                return GetSupergroup(supergroup.SupergroupId);
            }

            return null;
        }

        public bool TryGetSupergroup(long id, out Supergroup value)
        {
            return _supergroups.TryGetValue(id, out value);
        }

        public bool TryGetSupergroup(Chat chat, out Supergroup value)
        {
            if (chat?.Type is ChatTypeSupergroup supergroup)
            {
                return TryGetSupergroup(supergroup.SupergroupId, out value);
            }

            value = null;
            return false;
        }



        public SupergroupFullInfo GetSupergroupFull(long id)
        {
            if (_supergroupsFull.TryGetValue(id, out SupergroupFullInfo value))
            {
                return value;
            }

            return null;
        }

        public SupergroupFullInfo GetSupergroupFull(Chat chat)
        {
            if (chat?.Type is ChatTypeSupergroup supergroup)
            {
                return GetSupergroupFull(supergroup.SupergroupId);
            }

            return null;
        }

        public bool TryGetSupergroupFull(long id, out SupergroupFullInfo value)
        {
            return _supergroupsFull.TryGetValue(id, out value);
        }

        public bool TryGetSupergroupFull(Chat chat, out SupergroupFullInfo value)
        {
            if (chat?.Type is ChatTypeSupergroup supergroup)
            {
                return TryGetSupergroupFull(supergroup.SupergroupId, out value);
            }

            value = null;
            return false;
        }



        public ForumTopicInfo GetTopicInfo(long chatId, long messageThreadId)
        {
            if (_topics.TryGetValue(new ChatMessageId(chatId, messageThreadId), out ForumTopicInfo value))
            {
                return value;
            }

            return null;
        }

        public bool TryGetTopicInfo(long chatId, long messageThreadId, out ForumTopicInfo value)
        {
            return _topics.TryGetValue(new ChatMessageId(chatId, messageThreadId), out value);
        }



        public bool IsStickerRecent(int id)
        {
            if (_recentStickers != null)
            {
                return _recentStickers.Contains(id);
            }

            return false;
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

        public bool IsAnimationSaved(int id)
        {
            if (_savedAnimations != null)
            {
                return _savedAnimations.Contains(id);
            }

            return false;
        }

        public ChatTheme GetChatTheme(string themeName)
        {
            if (string.IsNullOrEmpty(themeName))
            {
                return null;
            }

            var themes = GetChatThemes();
            if (themes != null)
            {
                return themes.FirstOrDefault(x => string.Equals(x.Name, themeName));
            }

            return null;
        }

        public IList<ChatTheme> GetChatThemes()
        {
            return _chatThemes?.ChatThemes ?? new ChatTheme[0];
        }

        public bool IsDiceEmoji(string text, out string dice)
        {
            text = text.Trim();

            if (_diceEmojis == null)
            {
                dice = null;
                return false;
            }

            dice = text;
            return _diceEmojis.Contains(text);
        }

        #endregion



        public void OnResult(BaseObject update)
        {
            ProcessFiles(update);

            if (update is UpdateAuthorizationState updateAuthorizationState)
            {
                switch (updateAuthorizationState.AuthorizationState)
                {
                    case AuthorizationStateLoggingOut loggingOut:
                        _settings.Clear();
                        break;
                    case AuthorizationStateClosed closed:
                        CleanUp();
                        break;
                    case AuthorizationStateReady ready:
                        InitializeReady();
                        break;
                }

                _authorizationState = updateAuthorizationState.AuthorizationState;
            }
            else if (update is UpdateAnimationSearchParameters updateAnimationSearchParameters)
            {
                _animationSearchParameters = updateAnimationSearchParameters;
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
            else if (update is UpdateChatAction updateUserChatAction)
            {
                if (updateUserChatAction.MessageThreadId != 0)
                {
                    var threadActions = _topicActions.GetOrAdd(new ChatMessageId(updateUserChatAction.ChatId, updateUserChatAction.MessageThreadId), x => new ConcurrentDictionary<MessageSender, ChatAction>(new MessageSenderEqualityComparer()));
                    if (updateUserChatAction.Action is ChatActionCancel)
                    {
                        threadActions.TryRemove(updateUserChatAction.SenderId, out _);
                    }
                    else
                    {
                        threadActions[updateUserChatAction.SenderId] = updateUserChatAction.Action;
                    }
                }

                var actions = _chatActions.GetOrAdd(updateUserChatAction.ChatId, x => new ConcurrentDictionary<MessageSender, ChatAction>(new MessageSenderEqualityComparer()));
                if (updateUserChatAction.Action is ChatActionCancel)
                {
                    actions.TryRemove(updateUserChatAction.SenderId, out _);
                }
                else
                {
                    actions[updateUserChatAction.SenderId] = updateUserChatAction.Action;
                }
            }
            else if (update is UpdateChatActionBar updateChatActionBar)
            {
                if (_chats.TryGetValue(updateChatActionBar.ChatId, out Chat value))
                {
                    value.ActionBar = updateChatActionBar.ActionBar;
                }
            }
            else if (update is UpdateChatAvailableReactions chatAvailableReactions)
            {
                if (_chats.TryGetValue(chatAvailableReactions.ChatId, out Chat value))
                {
                    value.AvailableReactions = chatAvailableReactions.AvailableReactions;
                }
            }
            else if (update is UpdateChatBackground chatBackground)
            {
                if (_chats.TryGetValue(chatBackground.ChatId, out Chat value))
                {
                    value.Background = chatBackground.Background;
                }
            }
            else if (update is UpdateChatHasProtectedContent updateChatHasProtectedContent)
            {
                if (_chats.TryGetValue(updateChatHasProtectedContent.ChatId, out Chat value))
                {
                    value.HasProtectedContent = updateChatHasProtectedContent.HasProtectedContent;
                }
            }
            else if (update is UpdateChatDefaultDisableNotification updateChatDefaultDisableNotification)
            {
                if (_chats.TryGetValue(updateChatDefaultDisableNotification.ChatId, out Chat value))
                {
                    value.DefaultDisableNotification = updateChatDefaultDisableNotification.DefaultDisableNotification;
                }
            }
            else if (update is UpdateChatMessageSender updateChatMessageSender)
            {
                if (_chats.TryGetValue(updateChatMessageSender.ChatId, out Chat value))
                {
                    value.MessageSenderId = updateChatMessageSender.MessageSenderId;
                }
            }
            else if (update is UpdateChatDraftMessage updateChatDraftMessage)
            {
                if (_chats.TryGetValue(updateChatDraftMessage.ChatId, out Chat value))
                {
                    Monitor.Enter(value);

                    value.DraftMessage = updateChatDraftMessage.DraftMessage;
                    SetChatPositions(value, updateChatDraftMessage.Positions);

                    Monitor.Exit(value);
                }
            }
            else if (update is UpdateChatFolders updateChatFolders)
            {
                _chatFolders = updateChatFolders.ChatFolders.ToList();
                _mainChatListPosition = updateChatFolders.MainChatListPosition;
            }
            else if (update is UpdateChatHasScheduledMessages updateChatHasScheduledMessages)
            {
                if (_chats.TryGetValue(updateChatHasScheduledMessages.ChatId, out Chat value))
                {
                    value.HasScheduledMessages = updateChatHasScheduledMessages.HasScheduledMessages;
                }
            }
            else if (update is UpdateChatIsBlocked updateChatIsBlocked)
            {
                if (_chats.TryGetValue(updateChatIsBlocked.ChatId, out Chat value))
                {
                    value.IsBlocked = updateChatIsBlocked.IsBlocked;
                }
            }
            else if (update is UpdateChatIsMarkedAsUnread updateChatIsMarkedAsUnread)
            {
                if (_chats.TryGetValue(updateChatIsMarkedAsUnread.ChatId, out Chat value))
                {
                    value.IsMarkedAsUnread = updateChatIsMarkedAsUnread.IsMarkedAsUnread;
                }
            }
            else if (update is UpdateChatLastMessage updateChatLastMessage)
            {
                if (_chats.TryGetValue(updateChatLastMessage.ChatId, out Chat value))
                {
                    Monitor.Enter(value);

                    value.LastMessage = updateChatLastMessage.LastMessage;
                    SetChatPositions(value, updateChatLastMessage.Positions);

                    Monitor.Exit(value);
                }
            }
            else if (update is UpdateChatNotificationSettings updateNotificationSettings)
            {
                if (_chats.TryGetValue(updateNotificationSettings.ChatId, out Chat value))
                {
                    value.NotificationSettings = updateNotificationSettings.NotificationSettings;
                }
            }
            else if (update is UpdateChatPendingJoinRequests updateChatPendingJoinRequests)
            {
                if (_chats.TryGetValue(updateChatPendingJoinRequests.ChatId, out Chat value))
                {
                    value.PendingJoinRequests = updateChatPendingJoinRequests.PendingJoinRequests;
                }
            }
            else if (update is UpdateChatPermissions updateChatPermissions)
            {
                if (_chats.TryGetValue(updateChatPermissions.ChatId, out Chat value))
                {
                    value.Permissions = updateChatPermissions.Permissions;
                }
            }
            else if (update is UpdateChatPhoto updateChatPhoto)
            {
                if (_chats.TryGetValue(updateChatPhoto.ChatId, out Chat value))
                {
                    value.Photo = updateChatPhoto.Photo;
                }
            }
            else if (update is UpdateChatPosition updateChatPosition)
            {
                if (_chats.TryGetValue(updateChatPosition.ChatId, out Chat value))
                {
                    Monitor.Enter(value);

                    int i;
                    for (i = 0; i < value.Positions.Count; i++)
                    {
                        if (value.Positions[i].List.ToId() == updateChatPosition.Position.List.ToId())
                        {
                            break;
                        }
                    }

                    var newPositions = new List<ChatPosition>(value.Positions.Count + (updateChatPosition.Position.Order == 0 ? 0 : 1) - (i < value.Positions.Count ? 1 : 0));
                    if (updateChatPosition.Position.Order != 0)
                    {
                        newPositions.Add(updateChatPosition.Position);
                    }

                    for (int j = 0; j < value.Positions.Count; j++)
                    {
                        if (j != i)
                        {
                            newPositions.Add(value.Positions[j]);
                        }
                    }

                    SetChatPositions(value, newPositions);

                    Monitor.Exit(value);
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
            else if (update is UpdateChatTheme updateChatTheme)
            {
                if (_chats.TryGetValue(updateChatTheme.ChatId, out Chat value))
                {
                    value.ThemeName = updateChatTheme.ThemeName;
                }
            }
            else if (update is UpdateChatThemes updateChatThemes)
            {
                _chatThemes = updateChatThemes;
            }
            else if (update is UpdateChatTitle updateChatTitle)
            {
                if (_chats.TryGetValue(updateChatTitle.ChatId, out Chat value))
                {
                    value.Title = updateChatTitle.Title;
                }
            }
            else if (update is UpdateChatMessageAutoDeleteTime updateChatMessageAutoDeleteTime)
            {
                if (_chats.TryGetValue(updateChatMessageAutoDeleteTime.ChatId, out Chat value))
                {
                    value.MessageAutoDeleteTime = updateChatMessageAutoDeleteTime.MessageAutoDeleteTime;
                }
            }
            else if (update is UpdateChatUnreadMentionCount updateChatUnreadMentionCount)
            {
                if (_chats.TryGetValue(updateChatUnreadMentionCount.ChatId, out Chat value))
                {
                    value.UnreadMentionCount = updateChatUnreadMentionCount.UnreadMentionCount;
                }
            }
            else if (update is UpdateChatUnreadReactionCount updateChatUnreadReactionCount)
            {
                if (_chats.TryGetValue(updateChatUnreadReactionCount.ChatId, out Chat value))
                {
                    value.UnreadReactionCount = updateChatUnreadReactionCount.UnreadReactionCount;
                }
            }
            else if (update is UpdateChatVideoChat updateChatVideoChat)
            {
                if (_chats.TryGetValue(updateChatVideoChat.ChatId, out Chat value))
                {
                    value.VideoChat = updateChatVideoChat.VideoChat;
                }
            }
            else if (update is UpdateConnectionState updateConnectionState)
            {
                _connectionState = updateConnectionState.State;
            }
            else if (update is UpdateDefaultReactionType updateDefaultReactionType)
            {
                _defaultReaction = updateDefaultReactionType.ReactionType;
            }
            else if (update is UpdateDeleteMessages updateDeleteMessages)
            {

            }
            else if (update is UpdateDiceEmojis updateDiceEmojis)
            {
                _diceEmojis = updateDiceEmojis.Emojis.ToArray();
            }
            else if (update is UpdateFavoriteStickers updateFavoriteStickers)
            {
                _favoriteStickers = updateFavoriteStickers.StickerIds;
            }
            else if (update is UpdateFile updateFile)
            {
                // TODO: move the message after track when figured out why WeakAction throws a NRE
                EventAggregator.Default.Send(updateFile.File, $"{SessionId}_{updateFile.File.Id}", updateFile.File.Local.IsDownloadingCompleted);
                TrackDownloadedFile(updateFile.File);
                //return;
            }
            else if (update is UpdateFileGenerationStart updateFileGenerationStart)
            {

            }
            else if (update is UpdateFileGenerationStop updateFileGenerationStop)
            {

            }
            else if (update is UpdateForumTopicInfo updateForumTopicInfo)
            {
                _topics[new ChatMessageId(updateForumTopicInfo.ChatId, updateForumTopicInfo.Info.MessageThreadId)] = updateForumTopicInfo.Info;
            }
            else if (update is UpdateInstalledStickerSets updateInstalledStickerSets)
            {
                switch (updateInstalledStickerSets.StickerType)
                {
                    case StickerTypeRegular:
                        _installedStickerSets = updateInstalledStickerSets.StickerSetIds;
                        break;
                    case StickerTypeMask:
                        _installedMaskSets = updateInstalledStickerSets.StickerSetIds;
                        break;
                    case StickerTypeCustomEmoji:
                        _installedEmojiSets = updateInstalledStickerSets.StickerSetIds;
                        break;
                }
            }
            else if (update is UpdateLanguagePackStrings updateLanguagePackStrings)
            {
                _locale.Handle(updateLanguagePackStrings);
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
            else if (update is UpdateMessageInteractionInfo updateMessageInteractionInfo)
            {

            }
            else if (update is UpdateMessageIsPinned updateMessageIsPinned)
            {
                _settings.SetChatPinnedMessage(updateMessageIsPinned.ChatId, 0);
            }
            else if (update is UpdateMessageMentionRead updateMessageMentionRead)
            {
                if (_chats.TryGetValue(updateMessageMentionRead.ChatId, out Chat value))
                {
                    value.UnreadMentionCount = updateMessageMentionRead.UnreadMentionCount;
                }
            }
            else if (update is UpdateMessageUnreadReactions updateMessageUnreadReactions)
            {
                if (_chats.TryGetValue(updateMessageUnreadReactions.ChatId, out Chat value))
                {
                    value.UnreadReactionCount = updateMessageUnreadReactions.UnreadReactionCount;
                }
            }
            else if (update is UpdateNewChat updateNewChat)
            {
                _chats[updateNewChat.Chat.Id] = updateNewChat.Chat;

                Monitor.Enter(updateNewChat.Chat);
                SetChatPositions(updateNewChat.Chat, updateNewChat.Chat.Positions);
                Monitor.Exit(updateNewChat.Chat);

                if (updateNewChat.Chat.Type is ChatTypePrivate privata)
                {
                    _usersToChats[privata.UserId] = updateNewChat.Chat.Id;
                }
            }
            else if (update is UpdateOption updateOption)
            {
                _options.Update(updateOption.Name, updateOption.Value);

                if (updateOption.Name == "my_id" && updateOption.Value is OptionValueInteger myId)
                {
                    _settings.UserId = myId.Value;
                }
                else if (updateOption.Name == "is_premium" || updateOption.Name == "is_premium_available")
                {
                    _aggregator.Publish(new UpdatePremiumState(IsPremium, IsPremiumAvailable));
                }
            }
            else if (update is UpdateActiveEmojiReactions updateReactions)
            {
                _reactions = updateReactions.Emojis;
            }
            else if (update is UpdateRecentStickers updateRecentStickers)
            {
                if (updateRecentStickers.IsAttached)
                {

                }
                else
                {
                    _recentStickers = updateRecentStickers.StickerIds;
                }
            }
            else if (update is UpdateSavedAnimations updateSavedAnimations)
            {
                _savedAnimations = updateSavedAnimations.AnimationIds;
            }
            else if (update is UpdateScopeNotificationSettings updateScopeNotificationSettings)
            {
                _settings.Notifications.Scope[updateScopeNotificationSettings.Scope.GetType()] = updateScopeNotificationSettings.NotificationSettings;
            }
            else if (update is UpdateSecretChat updateSecretChat)
            {
                _secretChats[updateSecretChat.SecretChat.Id] = updateSecretChat.SecretChat;
            }
            else if (update is UpdateSelectedBackground updateSelectedBackground)
            {
                if (updateSelectedBackground.ForDarkTheme)
                {
                    _selectedBackgroundDark = updateSelectedBackground.Background;
                }
                else
                {
                    _selectedBackground = updateSelectedBackground.Background;
                }
            }
            else if (update is UpdateServiceNotification updateServiceNotification)
            {

            }
            else if (update is UpdateStickerSet updateStickerSet)
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
            else if (update is UpdateUnreadChatCount updateUnreadChatCount)
            {
                SetUnreadCount(updateUnreadChatCount.ChatList, chatCount: updateUnreadChatCount);
            }
            else if (update is UpdateUnreadMessageCount updateUnreadMessageCount)
            {
                SetUnreadCount(updateUnreadMessageCount.ChatList, messageCount: updateUnreadMessageCount);
            }
            else if (update is UpdateUser updateUser)
            {
                _users[updateUser.User.Id] = updateUser.User;
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

        private void ProcessTopicUpdate(Update update)
        {
            if (update is UpdateNewMessage newMessage)
            {
                if (newMessage.Message.Content is MessageForumTopicCreated)
                {

                }
                else if (newMessage.Message.Content is MessageForumTopicEdited)
                {

                }
                else if (newMessage.Message.Content is MessageForumTopicIsClosedToggled)
                {

                }
                else if (newMessage.Message.Content is MessageForumTopicIsHiddenToggled)
                {

                }
            }
        }
    }

    public class TopicLoader
    {

    }

    public class ChatListUnreadCount
    {
        public ChatList ChatList { get; set; }

        public UpdateUnreadChatCount UnreadChatCount { get; set; }
        public UpdateUnreadMessageCount UnreadMessageCount { get; set; }
    }

    public class FileContext<T> : ConcurrentDictionary<int, List<T>>
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
            set => base[id] = value;
        }
    }

    static class TdClientExtensions
    {
        public static void Send(this Client client, Function function, Action<BaseObject> handler)
        {
            if (handler == null)
            {
                client.Send(function, null);
            }
            else
            {
                client.Send(function, new TdHandler(handler));
            }
        }

        public static void Send(this Client client, Function function)
        {
            client.Send(function, null);
        }

        public static Task<BaseObject> SendAsync(this Client client, Function function, Action<BaseObject> closure)
        {
            var tsc = new TdCompletionSource(closure);
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

        public static bool MessageEquals(this Error error, ErrorType type)
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
        private readonly Action<BaseObject> _closure;

        public TdCompletionSource(Action<BaseObject> closure)
        {
            _closure = closure;
        }

        public void OnResult(BaseObject result)
        {
            _closure(result);
            SetResult(result);
        }
    }

    class TdHandler : ClientResultHandler
    {
        private readonly Action<BaseObject> _callback;

        public TdHandler(Action<BaseObject> callback)
        {
            _callback = callback;
        }

        public void OnResult(BaseObject result)
        {
            try
            {
                _callback(result);
            }
            catch
            {
                // We need to explicitly catch here because
                // an exception on the handler thread will cause
                // the app to no longer receive any update from TDLib.
            }
        }
    }

    public class MessageSenderEqualityComparer : IEqualityComparer<MessageSender>
    {
        public bool Equals(MessageSender x, MessageSender y)
        {
            return x.AreTheSame(y);
        }

        public int GetHashCode(MessageSender obj)
        {
            if (obj is MessageSenderUser user)
            {
                return user.UserId.GetHashCode();
            }
            else if (obj is MessageSenderChat chat)
            {
                return chat.ChatId.GetHashCode();
            }

            return obj.GetHashCode();
        }
    }
}
