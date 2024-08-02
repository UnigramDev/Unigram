//
// Copyright Fela Ameghino 2015-2024
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
using Telegram.Native;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Storage;
using TimeZone = Telegram.Td.Api.TimeZone;

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

        void GetReplyTo(MessageViewModel message, Action<BaseObject> handler);
        void GetStory(long storySenderChatId, int storyId, Action<BaseObject> handler);

        Task<BaseObject> CheckChatInviteLinkAsync(string inviteLink);

        Task<StorageFile> GetFileAsync(File file, bool completed = true);
        Task<StorageFile> GetPermanentFileAsync(File file);

        void DownloadFile(int fileId, int priority, int offset = 0, int limit = 0, bool synchronous = false);
        Task<File> DownloadFileAsync(File file, int priority, int offset = 0, int limit = 0);

        void AddFileToDownloads(File file, long chatId, long messageId, int priority = 30);
        void CancelDownloadFile(File file, bool onlyIfPending = false);
        bool IsDownloadFileCanceled(int fileId);

        Task<bool> HasPrivacySettingsRuleAsync<T>(UserPrivacySetting setting) where T : UserPrivacySettingRule;

        Task<Chats> GetChatListAsync(ChatList chatList, int offset, int limit);
        Task<Chats> GetStoryListAsync(StoryList storyList, int offset, int limit);

        Task<IList<SavedMessagesTopic>> GetSavedMessagesChatsAsync(int offset, int limit);

        Task<BaseObject> GetStarTransactionsAsync(MessageSender ownerId, StarTransactionDirection direction, string offset, int limit);

        Sticker NextGreetingSticker();

        int SessionId { get; }
    }

    public interface ICacheService
    {
        bool IsPremium { get; }
        bool IsPremiumAvailable { get; }

        long OwnedStarCount { get; }

        UnconfirmedSession UnconfirmedSession { get; }

        MessageSender MyId { get; }
        IOptionsService Options { get; }
        JsonValueObject Config { get; }

        IList<NameColor> GetAvailableAccentColors();
        IList<ProfileColor> GetAvailableProfileColors();

        NameColor GetAccentColor(int id);
        ProfileColor GetProfileColor(int id);
        bool TryGetProfileColor(int id, out ProfileColor color);

        ReactionType DefaultReaction { get; }

        IList<ChatFolderInfo> ChatFolders { get; }
        int MainChatListPosition { get; }
        bool AreTagsEnabled { get; }

        IList<AttachmentMenuBot> AttachmentMenuBots { get; }

        IList<AttachmentMenuBot> GetBotsForChat(long chatId);
        IList<AttachmentMenuBot> GetBotsForMenu(out long hash);

        UpdateAvailableMessageEffects AvailableMessageEffects { get; }

        IList<string> ActiveReactions { get; }

        IList<string> AnimationSearchEmojis { get; }
        string AnimationSearchProvider { get; }

        UpdateSpeechRecognitionTrial SpeechRecognitionTrial { get; }

        IList<CloseBirthdayUser> CloseBirthdayUsers { get; }

        Background GetDefaultBackground(bool darkTheme);
        Background DefaultBackground { get; }

        Task<AuthorizationState> GetAuthorizationStateAsync();
        AuthorizationState AuthorizationState { get; }
        ConnectionState ConnectionState { get; }

        string GetTitle(Chat chat, bool tiny = false);
        string GetTitle(long chatId, bool tiny = false);
        string GetTitle(SavedMessagesTopic topic);
        string GetTitle(MessageOrigin origin, MessageImportInfo import);
        string GetTitle(MessageSender sender);

        IList<ChatFolderInfo> GetChatFolders(Chat chat);

        bool TryGetCachedReaction(string emoji, out EmojiReaction value);
        Task<IDictionary<string, EmojiReaction>> GetAllReactionsAsync();
        Task<IDictionary<string, EmojiReaction>> GetReactionsAsync(IEnumerable<string> reactions);

        Task<IDictionary<MessageId, MessageProperties>> GetMessagePropertiesAsync(IEnumerable<MessageId> messageIds);

        Chat GetChat(long id);
        IEnumerable<Chat> GetChats(IEnumerable<long> ids);

        ChatActiveStories GetActiveStories(long id);
        IEnumerable<ChatActiveStories> GetActiveStorieses(IEnumerable<long> ids);

        IDictionary<MessageSender, ChatAction> GetChatActions(long id, long threadId = 0);

        QuickReplyShortcut GetQuickReplyShortcut(int id);
        QuickReplyShortcut GetQuickReplyShortcut(string name);
        IList<QuickReplyMessage> GetQuickReplyMessages(int id);
        IList<QuickReplyShortcut> GetQuickReplyShortcuts();
        bool CheckQuickReplyShortcutName(string name);

        Task<IList<MessageEffect>> GetMessageEffectsAsync(IEnumerable<long> effectIds);
        MessageEffect LoadMessageEffect(long effectId, bool preload);

        bool IsSavedMessages(MessageSender sender);
        bool IsSavedMessages(User user);
        bool IsSavedMessages(Chat chat);

        bool IsRepliesChat(Chat chat);
        bool IsForum(Chat chat);

        bool IsChatAccessible(Chat chat);

        bool IsBotAddedToAttachmentMenu(long userId);

        bool CanPostMessages(Chat chat);
        bool CanInviteUsers(Chat chat);

        BaseObject GetMessageSender(MessageSender sender);

        bool TryGetSavedMessagesTopic(long savedMessagesTopicId, out SavedMessagesTopic topic);

        bool TryGetChat(long chatId, out Chat chat);
        bool TryGetChat(MessageSender sender, out Chat value);

        bool TryGetChatFromUser(long userId, out long value);
        bool TryGetChatFromUser(long userId, out Chat value);
        bool TryGetActiveStoriesFromUser(long userId, out ChatActiveStories activeStories);

        bool TryGetActiveStories(long chatId, out ChatActiveStories activeStories);

        bool TryGetTimeZone(string timeZoneId, out TimeZone timeZone);

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

        MessageTag GetSavedMessagesTag(ReactionType reaction);
        bool TryGetSavedMessagesTag(ReactionType reaction, out MessageTag value);

        int GetMembersCount(long chatId);
        int GetMembersCount(Chat chat);

        bool IsAnimationSaved(int id);
        bool IsStickerRecent(int id);
        bool IsStickerFavorite(int id);
        bool IsStickerSetInstalled(long id);

        ChatListUnreadCount GetUnreadCount(ChatList chatList);

        UpdateStoryStealthMode StealthMode { get; }

        ChatTheme GetChatTheme(string themeName);
        IList<ChatTheme> ChatThemes { get; }

        bool IsDiceEmoji(string text, out string dice);

        bool HasSuggestedAction(SuggestedAction action);

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

        private readonly ConcurrentDictionary<long, MessageEffect> _effects = new();

        private readonly Action<BaseObject> _processFilesDelegate;

        private readonly Dictionary<long, ChatActiveStories> _activeStories = new();

        private readonly Dictionary<long, Chat> _chats = new();
        private readonly ConcurrentDictionary<long, ConcurrentDictionary<MessageSender, ChatAction>> _chatActions = new();
        private readonly ConcurrentDictionary<ChatMessageId, ConcurrentDictionary<MessageSender, ChatAction>> _topicActions = new();

        private readonly ConcurrentDictionary<long, SavedMessagesTopic> _savedMessagesTopics = new();

        private readonly Dictionary<int, SecretChat> _secretChats = new();

        private readonly Dictionary<long, long> _usersToChats = new();

        private readonly Dictionary<long, User> _users = new();
        private readonly ConcurrentDictionary<long, UserFullInfo> _usersFull = new();

        private readonly Dictionary<long, BasicGroup> _basicGroups = new();
        private readonly ConcurrentDictionary<long, BasicGroupFullInfo> _basicGroupsFull = new();

        private readonly Dictionary<long, Supergroup> _supergroups = new();
        private readonly ConcurrentDictionary<long, SupergroupFullInfo> _supergroupsFull = new();

        private readonly Dictionary<ChatMessageId, ForumTopicInfo> _topics = new();

        private readonly ConcurrentDictionary<int, ChatListUnreadCount> _unreadCounts = new();

        private readonly Dictionary<int, File> _files = new();

        private UnconfirmedSession _unconfirmedSession;

        private IList<string> _diceEmojis;

        private IList<int> _savedAnimations;
        private IList<int> _recentStickers;
        private IList<int> _favoriteStickers;
        private IList<long> _installedStickerSets;
        private IList<long> _installedMaskSets;
        private IList<long> _installedEmojiSets;

        private ReactionType _defaultReaction;

        private IList<ChatFolderInfo> _chatFolders = Array.Empty<ChatFolderInfo>();
        private Dictionary<int, ChatFolderInfo> _chatFolders2 = new();
        private readonly object _chatFoldersLock = new();
        private int _mainChatListPosition = 0;
        private bool _areTagsEnabled;

        private UpdateAvailableMessageEffects _availableMessageEffects;

        private IList<string> _activeReactions = Array.Empty<string>();
        private Dictionary<string, EmojiReaction> _cachedReactions = new();

        private IList<AttachmentMenuBot> _attachmentMenuBots = Array.Empty<AttachmentMenuBot>();

        private UpdateSpeechRecognitionTrial _speechRecognitionTrial;

        private UpdateAnimationSearchParameters _animationSearchParameters;

        private UpdateChatThemes _chatThemes;

        private UpdateStoryStealthMode _storyStealthMode = new();

        private UpdateContactCloseBirthdays _contactCloseBirthdays;

        private readonly Dictionary<ReactionType, MessageTag> _savedMessagesTags = new(new ReactionTypeEqualityComparer());

        private class ReactionTypeEqualityComparer : IEqualityComparer<ReactionType>
        {
            public bool Equals(ReactionType x, ReactionType y)
            {
                return x.AreTheSame(y);
            }

            public int GetHashCode(ReactionType obj)
            {
                if (obj is ReactionTypeEmoji emoji)
                {
                    return emoji.Emoji.GetHashCode();
                }
                else if (obj is ReactionTypeCustomEmoji customEmoji)
                {
                    return customEmoji.CustomEmojiId.GetHashCode();
                }

                return obj.GetHashCode();
            }
        }

        private TaskCompletionSource<bool> _authorizationStateTask = new();
        private AuthorizationState _authorizationState;
        private ConnectionState _connectionState;

        private JsonValueObject _config;

        private Background _selectedBackground;
        private Background _selectedBackgroundDark;

        private bool _initializeAfterClose;

        private static volatile Task _longRunningTask;
        private static readonly object _longRunningLock = new();

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

            _secretChats[1] = new SecretChat(1, 3, new SecretChatStateReady(), false, Array.Empty<byte>(), 75);

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

            var lastMessage0  = new Message(long.MaxValue, new MessageSenderUser(0),  0,  null, null, true, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(21, 41),  0, null, null, null, 0, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("Bob says hi.", Array.Empty<TextEntity>()), null), null);
            var lastMessage1  = new Message(long.MaxValue, new MessageSenderUser(1),  1,  null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(21, 41),  0, null, null, null, 0, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("Say hello to Alice.", Array.Empty<TextEntity>()), null), null);
            var lastMessage2  = new Message(long.MaxValue, new MessageSenderUser(9),  2,  null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(21, 41),  0, null, null, null, 0, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("Sometimes possession is an abstract concept. They took my purse, but the...", Array.Empty<TextEntity>()), null), null);
            var lastMessage3  = new Message(long.MaxValue, new MessageSenderUser(3),  3,  null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(21, 22),  0, null, null, null, 0, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageVideo(new Sticker(0, 0, 0, "😍", null, null, null, null, null), new FormattedText("Moar ct videos in this channel?", Array.Empty<TextEntity>()), false, false), null);
            var lastMessage4  = new Message(long.MaxValue, new MessageSenderUser(4),  4,  null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(21, 12),  0, null, null, null, 0, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("Don't tell mom yet, but I got the job! I'm going to ROME!", Array.Empty<TextEntity>()), null), null);
            var lastMessage5  = new Message(long.MaxValue, new MessageSenderUser(5),  5,  null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(20, 28),  0, null, null, null, 0, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageDocument(new FormattedText("I looove new Surfaces! If fact, they invited me to a focus group.", Array.Empty<TextEntity>()), null), null);
            var lastMessage6  = new Message(long.MaxValue, new MessageSenderUser(6),  6,  null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(19, 36),  0, null, null, null, 0, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageVideoNote(null, false, false), null);
            var lastMessage7  = new Message(long.MaxValue, new MessageSenderUser(7),  7,  null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TuesdayDate(),      0, null, null, null, 0, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessagePhoto(new Document("LaserBlastSafetyGuide.pdf", string.Empty, null, null, null), new FormattedText(string.Empty, Array.Empty<TextEntity>())), null);
            var lastMessage8  = new Message(long.MaxValue, new MessageSenderUser(8),  8,  null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TuesdayDate(),      0, null, null, null, 0, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("It's impossible.", Array.Empty<TextEntity>()), null), null);
            var lastMessage9  = new Message(long.MaxValue, new MessageSenderUser(9),  9,  null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TuesdayDate(),      0, null, null, null, 0, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("Hola!", Array.Empty<TextEntity>()), null), null);
            var lastMessage10 = new Message(long.MaxValue, new MessageSenderUser(17), 12, null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TuesdayDate(),      0, null, null, null, 0, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("Let's design more robust memes", Array.Empty<TextEntity>()), null), null);
            var lastMessage11 = new Message(long.MaxValue, new MessageSenderUser(18), 13, null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TuesdayDate(),      0, null, null, null, 0, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("What?! 😱", Array.Empty<TextEntity>()), null), null);
            var lastMessage12 = new Message(long.MaxValue, new MessageSenderUser(8),  9,  null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(15, 30),  0, null, null, null, 0, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("Wait, we could have made so much money on this!", Array.Empty<TextEntity>()), null), null);

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
                    UseTestDc = _settings.UseTestDC
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
            Send(new SearchEmojis("cucumber", new[] { NativeUtils.GetKeyboardCulture() }));

            UpdateGreetingStickers();
            UpdateTimeZones();
        }

        private void InitializeFlush()
        {
            // Flush animated stickers cache files that have not been accessed in three days
            Task.Factory.StartNew(() =>
            {
                static IEnumerable<string> GetFiles(string path)
                {
                    try
                    {
                        if (System.IO.Directory.Exists(path))
                        {
                            return System.IO.Directory.GetFiles(path, "*.cache");
                        }
                    }
                    catch
                    {

                    }

                    return Enumerable.Empty<string>();
                }

                var now = DateTime.Now;
                var path = System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, $"{_session}", "stickers");

                foreach (var file in GetFiles(path))
                {
                    var date = System.IO.File.GetLastAccessTime(file);

                    var diff = now - date;
                    if (diff.TotalDays >= 3)
                    {
                        try
                        {
                            System.IO.File.Delete(file);
                        }
                        catch
                        {
                            // File might be in use
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

        private void UpdateTimeZones()
        {
            Send(new GetTimeZones(), result =>
            {
                if (result is TimeZones timeZones)
                {
                    lock (_timezones)
                    {
                        _timezones.Clear();

                        foreach (var zone in timeZones.TimeZonesValue)
                        {
                            _timezones[zone.Id] = zone;
                        }
                    }
                }
            });
        }

        public bool TryGetTimeZone(string timeZoneId, out TimeZone timeZone)
        {
            return _timezones.TryGetValue(timeZoneId, out timeZone);
        }

        private void UpdateGreetingStickers()
        {
            _waitGreetingSticker = true;

            Send(new GetGreetingStickers(), result =>
            {
                if (result is Stickers stickers && stickers.StickersValue.Count > 0)
                {
                    _greetingStickers = stickers.StickersValue;
                    LoadNextGreetingSticker();
                }
                else
                {
                    _waitGreetingSticker = false;
                }
            });
        }

        private Sticker LoadNextGreetingSticker()
        {
            if (_greetingStickers == null)
            {
                return null;
            }

            var next = _greetingStickers.Random();
            var prev = _nextGreetingSticker ?? next;

            _nextGreetingSticker = next;

            if (_waitGreetingSticker)
            {
                _aggregator.Publish(new UpdateGreetingSticker(prev));
                _waitGreetingSticker = false;
            }

            DownloadFile(next.StickerValue.Id, 16);

            return prev;
        }

        public Sticker NextGreetingSticker()
        {
            if (_waitGreetingSticker)
            {
                return null;
            }

            return LoadNextGreetingSticker();
        }

        private IList<Sticker> _greetingStickers;
        private Sticker _nextGreetingSticker;
        private bool _waitGreetingSticker;

        private readonly Dictionary<string, TimeZone> _timezones = new();

        public UpdateAvailableMessageEffects AvailableMessageEffects => _availableMessageEffects;

        public IList<string> ActiveReactions => _activeReactions;

        public IDictionary<int, NameColor> AccentColors { get; private set; }
        public IList<int> AvailableAccentColors { get; private set; }

        public IDictionary<int, ProfileColor> ProfileColors { get; private set; }
        public IList<int> AvailableProfileColors { get; private set; }

        public IList<NameColor> GetAvailableAccentColors()
        {
            if (AccentColors == null || AvailableAccentColors == null)
            {
                return Array.Empty<NameColor>();
            }

            IList<NameColor> colors = null;

            foreach (var id in AvailableAccentColors)
            {
                if (AccentColors.TryGetValue(id, out NameColor value))
                {
                    colors ??= new List<NameColor>();
                    colors.Add(value);
                }
            }

            return colors ?? Array.Empty<NameColor>();
        }

        public IList<ProfileColor> GetAvailableProfileColors()
        {
            if (ProfileColors == null || AvailableProfileColors == null)
            {
                return Array.Empty<ProfileColor>();
            }

            IList<ProfileColor> colors = null;

            foreach (var id in AvailableProfileColors)
            {
                if (ProfileColors.TryGetValue(id, out ProfileColor value))
                {
                    colors ??= new List<ProfileColor>();
                    colors.Add(value);
                }
            }

            return colors ?? Array.Empty<ProfileColor>();
        }

        public NameColor GetAccentColor(int id)
        {
            if (AccentColors != null && AccentColors.TryGetValue(id, out var accentColor))
            {
                return accentColor;
            }
            else if (id == -1)
            {
                return null;
            }

            return new NameColor(id);
        }

        public ProfileColor GetProfileColor(int id)
        {
            if (ProfileColors != null && ProfileColors.TryGetValue(id, out var accentColor))
            {
                return accentColor;
            }

            return null;
        }

        public bool TryGetProfileColor(int id, out ProfileColor color)
        {
            if (ProfileColors != null && ProfileColors.TryGetValue(id, out color))
            {
                return true;
            }

            color = null;
            return false;
        }

        public void CleanUp()
        {
            _options.Clear();

            _files.Clear();

            _activeReactions = Array.Empty<string>();

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

            _suggestedActions.Clear();

            _savedAnimations = null;
            _favoriteStickers = null;
            _installedStickerSets = null;
            _installedMaskSets = null;
            _installedEmojiSets = null;

            _chatFolders = Array.Empty<ChatFolderInfo>();
            _chatFolders2.Clear();

            _timezones.Clear();

            _animationSearchParameters = null;

            _authorizationStateTask = new();
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
            return _client.SendAsync(function, _processFilesDelegate);
        }



        public void GetReplyTo(MessageViewModel message, Action<BaseObject> handler)
        {
            if (message.ReplyTo is MessageReplyToMessage replyToMessage ||
                message.Content is MessagePinMessage ||
                message.Content is MessageGameScore ||
                message.Content is MessagePaymentSuccessful)
            {
                Send(new GetRepliedMessage(message.ChatId, message.Id), handler);
            }
            else if (message.ReplyTo is MessageReplyToStory replyToStory)
            {
                GetStory(replyToStory.StorySenderChatId, replyToStory.StoryId, handler);
            }
        }

        public void GetStory(long storySenderChatId, int storyId, Action<BaseObject> handler)
        {
            Send(new GetStory(storySenderChatId, storyId, true), result =>
            {
                if (result is Error)
                {
                    Send(new GetStory(storySenderChatId, storyId, false), handler);
                }
                else
                {
                    handler(result);
                }
            });
        }



        private readonly Dictionary<long, DateTime> _chatAccessibleUntil = new();

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


        public async Task<BaseObject> GetStarTransactionsAsync(MessageSender ownerId, StarTransactionDirection direction, string offset, int limit)
        {
            var response = await SendAsync(new GetStarTransactions(ownerId, direction, offset, limit));
            if (response is StarTransactions transactions)
            {
                if (ownerId == null || ownerId.IsUser(Options.MyId))
                {
                    OwnedStarCount = transactions.StarCount;
                    _aggregator.Publish(new UpdateOwnedStarCount(transactions.StarCount));
                }
            }

            return response;
        }


        public async Task<bool> HasPrivacySettingsRuleAsync<T>(UserPrivacySetting setting) where T : UserPrivacySettingRule
        {
            var response = await SendAsync(new GetUserPrivacySettingRules(setting));
            if (response is UserPrivacySettingRules rules)
            {
                foreach (var rule in rules.Rules)
                {
                    if (typeof(T) == rule.GetType())
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public async Task<IList<MessageEffect>> GetMessageEffectsAsync(IEnumerable<long> effectIds)
        {
            IList<MessageEffect> result = null;

            foreach (var id in effectIds)
            {
                if (_effects.TryGetValue(id, out MessageEffect effect))
                {
                    result ??= new List<MessageEffect>();
                    result.Add(effect);
                }
                else
                {
                    var response = await SendAsync(new GetMessageEffect(id));
                    if (response is MessageEffect item)
                    {
                        _effects[id] = item;

                        result ??= new List<MessageEffect>();
                        result.Add(item);
                    }
                }
            }

            return result ?? Array.Empty<MessageEffect>();
        }

        public MessageEffect LoadMessageEffect(long effectId, bool preload)
        {
            if (_effects.TryGetValue(effectId, out var effect))
            {
                return effect;
            }

            Send(new GetMessageEffect(effectId), result =>
            {
                if (result is MessageEffect effect)
                {
                    if (preload)
                    {
                        if (effect.Type is MessageEffectTypeEmojiReaction emojiReaction)
                        {
                            DownloadFile(emojiReaction.EffectAnimation.StickerValue.Id, 16);
                        }
                        else if (effect.Type is MessageEffectTypePremiumSticker premiumSticker && premiumSticker.Sticker.FullType is StickerFullTypeRegular regular)
                        {
                            DownloadFile(regular.PremiumAnimation.Id, 16);
                        }
                    }

                    _effects[effectId] = effect;
                    _aggregator.Publish(new UpdateMessageEffect(effect));
                }
            });
            return null;
        }


        public int SessionId => _session;

        public Client Client => _client;

        #region Cache

        public UpdateStoryStealthMode StealthMode => _storyStealthMode;

        public ChatListUnreadCount GetUnreadCount(ChatList chatList)
        {
            var id = chatList switch
            {
                ChatListArchive => 1,
                ChatListFolder folder => folder.ChatFolderId,
                _ => 0
            };

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
            var id = chatList switch
            {
                ChatListArchive => 1,
                ChatListFolder folder => folder.ChatFolderId,
                _ => 0
            };

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



        public async Task<AuthorizationState> GetAuthorizationStateAsync()
        {
            if (_authorizationState is not null)
            {
                return _authorizationState;
            }

            await _authorizationStateTask.Task;
            return _authorizationState;
        }

        public UnconfirmedSession UnconfirmedSession => _unconfirmedSession;

        public AuthorizationState AuthorizationState => _authorizationState;

        public ConnectionState ConnectionState => _connectionState;

        public Settings.NotificationsSettings Notifications => _settings.Notifications;

        public bool IsPremium => _options.IsPremium;

        public bool IsPremiumAvailable => _options.IsPremium || _options.IsPremiumAvailable;

        public long OwnedStarCount { get; private set; }

        public MessageSender MyId => new MessageSenderUser(_options.MyId);

        public IOptionsService Options => _options;

        public JsonValueObject Config => _config;

        public ReactionType DefaultReaction => _defaultReaction;

        public IList<ChatFolderInfo> ChatFolders => _chatFolders;

        public int MainChatListPosition => _mainChatListPosition;

        public bool AreTagsEnabled => _areTagsEnabled;

        public IList<AttachmentMenuBot> AttachmentMenuBots => _attachmentMenuBots;

        public IList<AttachmentMenuBot> GetBotsForChat(long chatId)
        {
            List<AttachmentMenuBot> bots = null;

            if (_chats.TryGetValue(chatId, out Chat chat))
            {
                foreach (var bot in _attachmentMenuBots)
                {
                    if (!bot.ShowInAttachmentMenu)
                    {
                        continue;
                    }

                    if (bot.SupportsGroupChats)
                    {
                        if (chat.Type is ChatTypeBasicGroup || (chat.Type is ChatTypeSupergroup supergroup && !supergroup.IsChannel))
                        {
                            bots ??= new();
                            bots.Add(bot);
                        }
                    }

                    if (bot.SupportsChannelChats)
                    {
                        if (chat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel)
                        {
                            bots ??= new();
                            bots.Add(bot);
                        }
                    }

                    if (bot.SupportsUserChats || bot.SupportsBotChats || bot.SupportsSelfChat)
                    {
                        if (TryGetUser(chat, out User user))
                        {
                            var supportsSelf = bot.SupportsSelfChat && user.Id == Options.MyId;
                            var supportsBot = bot.SupportsBotChats && user.Type is UserTypeBot;
                            var supportsUser = !supportsSelf && !supportsBot && user.Type is UserTypeRegular;

                            if (supportsSelf || supportsBot || supportsUser)
                            {
                                bots ??= new();
                                bots.Add(bot);
                            }
                        }
                    }
                }
            }

            return (IList<AttachmentMenuBot>)bots ?? Array.Empty<AttachmentMenuBot>();
        }

        public IList<AttachmentMenuBot> GetBotsForMenu(out long hash)
        {
            List<AttachmentMenuBot> bots = null;
            hash = Options.MyId;

            foreach (var bot in _attachmentMenuBots)
            {
                if (bot.ShowInSideMenu)
                {
                    hash = ((hash * 20261) + 0x80000000L + bot.BotUserId) % 0x80000000L;

                    if (bot.ShowDisclaimerInSideMenu)
                    {
                        hash = ((hash * 20261) + 0x80000001L) % 0x80000000L;
                    }

                    bots ??= new();
                    bots.Add(bot);
                }
            }

            return (IList<AttachmentMenuBot>)bots ?? Array.Empty<AttachmentMenuBot>();
        }

        public UpdateSpeechRecognitionTrial SpeechRecognitionTrial => _speechRecognitionTrial ??= new();

        public IList<CloseBirthdayUser> CloseBirthdayUsers => _contactCloseBirthdays?.CloseBirthdayUsers ?? Array.Empty<CloseBirthdayUser>();

        public IList<string> AnimationSearchEmojis => _animationSearchParameters?.Emojis ?? Array.Empty<string>();

        public string AnimationSearchProvider => _animationSearchParameters?.Provider;

        public Background DefaultBackground => GetDefaultBackground(_settings.Appearance.IsDarkTheme());

        public Background GetDefaultBackground(bool darkTheme)
        {
            if (darkTheme)
            {
                return _selectedBackgroundDark;
            }

            return _selectedBackground;
        }

        public string GetTitle(long chatId, bool tiny = false)
        {
            if (_chats.TryGetValue(chatId, out var chat))
            {
                return GetTitle(chat, tiny);
            }

            return string.Empty;
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

        public string GetTitle(SavedMessagesTopic topic)
        {
            if (topic?.Type is SavedMessagesTopicTypeMyNotes)
            {
                return Strings.MyNotes;
            }
            else if (topic?.Type is SavedMessagesTopicTypeAuthorHidden)
            {
                return Strings.AnonymousForward;
            }
            else if (topic?.Type is SavedMessagesTopicTypeSavedFromChat savedFromChat && TryGetChat(savedFromChat.ChatId, out Chat chat))
            {
                return GetTitle(chat);
            }

            return Strings.AnonymousForward;
        }

        public string GetTitle(MessageOrigin origin, MessageImportInfo import)
        {
            if (origin is MessageOriginUser fromUser)
            {
                return GetUser(fromUser.SenderUserId)?.FullName();
            }
            else if (origin is MessageOriginChat fromChat)
            {
                return GetTitle(fromChat.SenderChatId);
            }
            else if (origin is MessageOriginChannel fromChannel)
            {
                return GetTitle(fromChannel.ChatId);
            }
            else if (origin is MessageOriginHiddenUser fromHiddenUser)
            {
                return fromHiddenUser.SenderName;
            }
            else if (import != null)
            {
                return import.SenderName;
            }

            return null;
        }

        public string GetTitle(MessageSender sender)
        {
            if (TryGetUser(sender, out User user))
            {
                return user.FullName();
            }
            else if (TryGetChat(sender, out Chat chat))
            {
                return chat.Title;
            }

            return string.Empty;
        }

        public IList<ChatFolderInfo> GetChatFolders(Chat chat)
        {
            // TODO: can this be improved?
            List<ChatFolderInfo> result = null;

            lock (_chatFoldersLock)
            {
                lock (chat)
                {
                    foreach (var chatList in chat.ChatLists)
                    {
                        if (chatList is not ChatListFolder folder)
                        {
                            continue;
                        }

                        if (_chatFolders2.TryGetValue(folder.ChatFolderId, out ChatFolderInfo info) && info.ColorId >= 0 && info.ColorId <= 6)
                        {
                            result ??= new List<ChatFolderInfo>();
                            result.Add(info);
                        }
                    }
                }

                if (result != null)
                {
                    result.Sort((x, y) => _chatFolders.IndexOf(x) - _chatFolders.IndexOf(y));
                    return result;
                }
            }

            return Array.Empty<ChatFolderInfo>();
        }

        public bool TryGetCachedReaction(string emoji, out EmojiReaction value)
        {
            return _cachedReactions.TryGetValue(emoji, out value);
        }

        public async Task<IDictionary<string, EmojiReaction>> GetAllReactionsAsync()
        {
            var result = new Dictionary<string, EmojiReaction>();

            foreach (var emoji in _activeReactions)
            {
                if (_cachedReactions.TryGetValue(emoji, out EmojiReaction cached))
                {
                    result[emoji] = cached;
                }
                else
                {
                    var response = await SendAsync(new GetEmojiReaction(emoji));
                    if (response is EmojiReaction reaction)
                    {
                        _cachedReactions[emoji] = reaction;
                        result[emoji] = reaction;
                    }
                }
            }

            return result;
        }

        public async Task<IDictionary<string, EmojiReaction>> GetReactionsAsync(IEnumerable<string> reactions)
        {
            var result = new Dictionary<string, EmojiReaction>();

            foreach (var emoji in reactions)
            {
                if (_cachedReactions.TryGetValue(emoji, out EmojiReaction cached))
                {
                    result[emoji] = cached;
                }
                else
                {
                    var response = await SendAsync(new GetEmojiReaction(emoji));
                    if (response is EmojiReaction reaction)
                    {
                        _cachedReactions[emoji] = reaction;
                        result[emoji] = reaction;
                    }
                }
            }

            return result;
        }

        public async Task<IDictionary<MessageId, MessageProperties>> GetMessagePropertiesAsync(IEnumerable<MessageId> messageIds)
        {
            var map = new Dictionary<MessageId, MessageProperties>();

            foreach (var messageId in messageIds)
            {
                var properties = await SendAsync(new GetMessageProperties(messageId.ChatId, messageId.Id)) as MessageProperties;
                if (properties != null)
                {
                    map[messageId] = properties;
                }
            }

            return map;
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

        public QuickReplyShortcut GetQuickReplyShortcut(int id)
        {
            _quickReplyShortcuts.TryGetValue(id, out var value);
            return value?.Shortcut;
        }

        public QuickReplyShortcut GetQuickReplyShortcut(string name)
        {
            return _quickReplyShortcuts.Values
                .Select(x => x.Shortcut)
                .FirstOrDefault(x => x.Name == name);
        }

        public IList<QuickReplyMessage> GetQuickReplyMessages(int id)
        {
            if (_quickReplyShortcuts.TryGetValue(id, out var value))
            {
                return value.Messages;
            }

            return Array.Empty<QuickReplyMessage>();
        }

        public IList<QuickReplyShortcut> GetQuickReplyShortcuts()
        {
            if (_quickReplyShortcutIds != null)
            {
                var result = new List<QuickReplyShortcut>();

                foreach (var id in _quickReplyShortcutIds)
                {
                    if (_quickReplyShortcuts.TryGetValue(id, out var value))
                    {
                        result.Add(value.Shortcut);
                    }
                }

                return result;
            }

            return Array.Empty<QuickReplyShortcut>();
        }

        public bool CheckQuickReplyShortcutName(string name)
        {
            if (_quickReplyShortcuts.Values.Any(x => string.Equals(x.Shortcut.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            return ClientEx.CheckQuickReplyShortcutName(name);
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

        public bool IsBotAddedToAttachmentMenu(long userId)
        {
            foreach (var menuBot in _attachmentMenuBots)
            {
                if (menuBot.BotUserId == userId)
                {
                    return true;
                }
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

        public bool CanInviteUsers(Chat chat)
        {
            if (TryGetSupergroup(chat, out Supergroup supergroup))
            {
                return supergroup.CanInviteUsers();
            }
            else if (TryGetBasicGroup(chat, out BasicGroup basicGroup))
            {
                return basicGroup.CanInviteUsers();
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

        public bool TryGetSavedMessagesTopic(long savedMessagesTopicId, out SavedMessagesTopic topic)
        {
            return _savedMessagesTopics.TryGetValue(savedMessagesTopicId, out topic);
        }

        public bool TryGetChat(long chatId, out Chat chat)
        {
            return _chats.TryGetValue(chatId, out chat);
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

        public bool TryGetChatFromUser(long userId, out Chat chat)
        {
            if (_usersToChats.TryGetValue(userId, out long chatId))
            {
                return TryGetChat(chatId, out chat);
            }

            chat = null;
            return false;
        }

        public bool TryGetActiveStoriesFromUser(long userId, out ChatActiveStories activeStories)
        {
            if (_usersToChats.TryGetValue(userId, out long chatId))
            {
                return TryGetActiveStories(chatId, out activeStories);
            }

            activeStories = null;
            return false;
        }

        public IEnumerable<Chat> GetChats(IEnumerable<long> ids)
        {
#if MOCKUP
            return _chats.Values.ToList();
#endif

            foreach (var id in ids)
            {
                var chat = GetChat(id);
                if (chat != null)
                {
                    yield return chat;
                }
            }
        }

        public ChatActiveStories GetActiveStories(long id)
        {
            if (_activeStories.TryGetValue(id, out ChatActiveStories value))
            {
                return value;
            }

            return null;
        }

        public bool TryGetActiveStories(long id, out ChatActiveStories value)
        {
            return _activeStories.TryGetValue(id, out value);
        }

        public IEnumerable<ChatActiveStories> GetActiveStorieses(IEnumerable<long> ids)
        {
#if MOCKUP
            return _chats.Values.ToList();
#endif

            foreach (var id in ids)
            {
                var activeStories = GetActiveStories(id);
                if (activeStories != null)
                {
                    yield return activeStories;
                }
            }
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



        public MessageTag GetSavedMessagesTag(ReactionType reaction)
        {
            lock (_savedMessagesTags)
            {
                if (_savedMessagesTags.TryGetValue(reaction, out MessageTag value))
                {
                    return value;
                }
            }

            return null;
        }

        public bool TryGetSavedMessagesTag(ReactionType reaction, out MessageTag value)
        {
            lock (_savedMessagesTags)
            {
                return _savedMessagesTags.TryGetValue(reaction, out value);
            }
        }

        public int GetMembersCount(long chatId)
        {
            if (TryGetChat(chatId, out Chat chat))
            {
                return GetMembersCount(chat);
            }

            return 0;
        }

        public int GetMembersCount(Chat chat)
        {
            if (TryGetSupergroupFull(chat, out SupergroupFullInfo supergroupFullInfo))
            {
                return supergroupFullInfo.MemberCount;
            }
            else if (TryGetBasicGroupFull(chat, out BasicGroupFullInfo basicGroupFullInfo))
            {
                return basicGroupFullInfo.Members.Count;
            }
            else if (TryGetSupergroup(chat, out Supergroup supergroup))
            {
                return supergroup.MemberCount;
            }
            else if (TryGetBasicGroup(chat, out BasicGroup basicGroup))
            {
                return basicGroup.MemberCount;
            }

            return 0;
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

            return ChatThemes.FirstOrDefault(x => string.Equals(x.Name, themeName));
        }

        public IList<ChatTheme> ChatThemes => _chatThemes?.ChatThemes ?? Array.Empty<ChatTheme>();

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

        private readonly HashSet<SuggestedAction> _suggestedActions = new(new SuggestedActionComparer());

        private class SuggestedActionComparer : IEqualityComparer<SuggestedAction>
        {
            public bool Equals(SuggestedAction x, SuggestedAction y)
            {
                return x switch
                {
                    SuggestedActionCheckPassword => y is SuggestedActionCheckPassword,
                    SuggestedActionCheckPhoneNumber => y is SuggestedActionCheckPhoneNumber,
                    SuggestedActionEnableArchiveAndMuteNewChats => y is SuggestedActionEnableArchiveAndMuteNewChats,
                    SuggestedActionGiftPremiumForChristmas => y is SuggestedActionGiftPremiumForChristmas,
                    SuggestedActionRestorePremium => y is SuggestedActionRestorePremium,
                    SuggestedActionSetBirthdate => y is SuggestedActionSetBirthdate,
                    SuggestedActionSubscribeToAnnualPremium => y is SuggestedActionSubscribeToAnnualPremium,
                    SuggestedActionUpgradePremium => y is SuggestedActionUpgradePremium,
                    SuggestedActionViewChecksHint => y is SuggestedActionViewChecksHint,
                    SuggestedActionConvertToBroadcastGroup convertToBroadcastGroup => y is SuggestedActionConvertToBroadcastGroup yc && yc.SupergroupId == convertToBroadcastGroup.SupergroupId,
                    SuggestedActionSetPassword setPassword => y is SuggestedActionSetPassword ys && ys.AuthorizationDelay == setPassword.AuthorizationDelay,
                    _ => false
                };
            }

            public int GetHashCode(SuggestedAction obj)
            {
                return obj switch
                {
                    SuggestedActionCheckPassword => 0,
                    SuggestedActionCheckPhoneNumber => 1,
                    SuggestedActionEnableArchiveAndMuteNewChats => 2,
                    SuggestedActionGiftPremiumForChristmas => 3,
                    SuggestedActionRestorePremium => 4,
                    SuggestedActionSetBirthdate => 5,
                    SuggestedActionSubscribeToAnnualPremium => 6,
                    SuggestedActionUpgradePremium => 7,
                    SuggestedActionViewChecksHint => 8,
                    SuggestedActionConvertToBroadcastGroup convertToBroadcastGroup => HashCode.Combine(9, convertToBroadcastGroup.SupergroupId),
                    SuggestedActionSetPassword setPassword => HashCode.Combine(10, setPassword.AuthorizationDelay),
                    _ => -1
                };
            }
        }

        public bool HasSuggestedAction(SuggestedAction action)
        {
            lock (_suggestedActions)
            {
                return _suggestedActions.Contains(action);
            }
        }

        #endregion



        public void OnResult(BaseObject update)
        {
            ProcessFiles(update);

            if (update is UpdateFile updateFile)
            {
                // TODO: move the message after track when figured out why WeakAction throws a NRE
                var token = (SessionId << 16) | updateFile.File.Id;
                if (updateFile.File.Local.IsDownloadingCompleted)
                {
                    EventAggregator.Current.Publish(updateFile.File, token | 0x01000000, true);
                }

                EventAggregator.Current.Publish(updateFile.File, token, false);
                TrackDownloadedFile(updateFile.File);
                return;
            }
            else if (update is UpdateChatPosition updateChatPosition)
            {
                if (_chats.TryGetValue(updateChatPosition.ChatId, out Chat value))
                {
                    Monitor.Enter(value);

                    int i;
                    for (i = 0; i < value.Positions.Count; i++)
                    {
                        if (ChatListEqualityComparer.Instance.Equals(value.Positions[i].List, updateChatPosition.Position.List))
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
            else if (update is UpdateUser updateUser)
            {
                _users.TryGetValue(updateUser.User.Id, out User value);
                _users[updateUser.User.Id] = updateUser.User;

                if (value != null && value.IsContact != updateUser.User.IsContact)
                {
                    _aggregator.Publish(new UpdateUserIsContact(updateUser.User.Id));
                }
            }
            else if (update is UpdateUnreadMessageCount updateUnreadMessageCount)
            {
                SetUnreadCount(updateUnreadMessageCount.ChatList, messageCount: updateUnreadMessageCount);
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
            else if (update is UpdateSavedMessagesTopic updateSavedMessagesTopic)
            {
                if (_savedMessagesTopics.TryGetValue(updateSavedMessagesTopic.Topic.Id, out SavedMessagesTopic topic))
                {
                    Monitor.Enter(topic);
                    SetSavedMessagesTopicOrder(topic, updateSavedMessagesTopic.Topic.Order);
                    Monitor.Exit(topic);

                    topic.DraftMessage = updateSavedMessagesTopic.Topic.DraftMessage;
                    topic.LastMessage = updateSavedMessagesTopic.Topic.LastMessage;
                    topic.IsPinned = updateSavedMessagesTopic.Topic.IsPinned;
                    topic.Order = updateSavedMessagesTopic.Topic.Order;

                    updateSavedMessagesTopic.Topic = topic;
                }
                else
                {
                    Monitor.Enter(updateSavedMessagesTopic.Topic);
                    SetSavedMessagesTopicOrder(updateSavedMessagesTopic.Topic, updateSavedMessagesTopic.Topic.Order);
                    Monitor.Exit(updateSavedMessagesTopic.Topic);

                    _savedMessagesTopics[updateSavedMessagesTopic.Topic.Id] = updateSavedMessagesTopic.Topic;
                }
            }
            else if (update is UpdateChatAddedToList updateChatAddedToList)
            {
                if (_chats.TryGetValue(updateChatAddedToList.ChatId, out Chat value))
                {
                    value.ChatLists.Add(updateChatAddedToList.ChatList);
                }
            }
            else if (update is UpdateChatRemovedFromList updateChatRemovedFromList)
            {
                if (_chats.TryGetValue(updateChatRemovedFromList.ChatId, out Chat value))
                {
                    foreach (var chatList in value.ChatLists)
                    {
                        if (chatList.AreTheSame(updateChatRemovedFromList.ChatList))
                        {
                            value.ChatLists.Remove(chatList);
                            break;
                        }
                    }
                }
            }
            else if (update is UpdateAuthorizationState updateAuthorizationState)
            {
                switch (updateAuthorizationState.AuthorizationState)
                {
                    case AuthorizationStateLoggingOut:
                        _settings.Clear();
                        break;
                    case AuthorizationStateClosed:
                        CleanUp();
                        break;
                    case AuthorizationStateReady:
                        InitializeReady();
                        break;
                }

                if (updateAuthorizationState.AuthorizationState is not AuthorizationStateWaitTdlibParameters)
                {
                    _authorizationStateTask.TrySetResult(true);
                    _authorizationState = updateAuthorizationState.AuthorizationState;
                }
            }
            else if (update is UpdateChatActiveStories updateActiveStories)
            {
                _activeStories.TryGetValue(updateActiveStories.ActiveStories.ChatId, out ChatActiveStories value);
                _activeStories[updateActiveStories.ActiveStories.ChatId] = updateActiveStories.ActiveStories;

                Monitor.Enter(updateActiveStories.ActiveStories);
                SetActiveStoriesPositions(updateActiveStories.ActiveStories, value);
                Monitor.Exit(updateActiveStories.ActiveStories);
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
            else if (update is UpdateChatEmojiStatus updateChatEmojiStatus)
            {
                if (_chats.TryGetValue(updateChatEmojiStatus.ChatId, out Chat value))
                {
                    value.EmojiStatus = updateChatEmojiStatus.EmojiStatus;
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
                lock (_chatFoldersLock)
                {
                    _chatFolders = updateChatFolders.ChatFolders.ToList();
                    _chatFolders2 = updateChatFolders.ChatFolders.ToDictionary(x => x.Id);
                }

                _mainChatListPosition = updateChatFolders.MainChatListPosition;
                _areTagsEnabled = updateChatFolders.AreTagsEnabled;
            }
            else if (update is UpdateChatHasScheduledMessages updateChatHasScheduledMessages)
            {
                if (_chats.TryGetValue(updateChatHasScheduledMessages.ChatId, out Chat value))
                {
                    value.HasScheduledMessages = updateChatHasScheduledMessages.HasScheduledMessages;
                }
            }
            else if (update is UpdateChatAccentColors updateChatAccentColors)
            {
                if (_chats.TryGetValue(updateChatAccentColors.ChatId, out Chat value))
                {
                    value.AccentColorId = updateChatAccentColors.AccentColorId;
                    value.BackgroundCustomEmojiId = updateChatAccentColors.BackgroundCustomEmojiId;
                    value.ProfileAccentColorId = updateChatAccentColors.ProfileAccentColorId;
                    value.ProfileBackgroundCustomEmojiId = updateChatAccentColors.ProfileBackgroundCustomEmojiId;
                }
            }
            else if (update is UpdateChatBlockList updateChatBlockList)
            {
                if (_chats.TryGetValue(updateChatBlockList.ChatId, out Chat value))
                {
                    value.BlockList = updateChatBlockList.BlockList;
                }
            }
            else if (update is UpdateChatIsMarkedAsUnread updateChatIsMarkedAsUnread)
            {
                if (_chats.TryGetValue(updateChatIsMarkedAsUnread.ChatId, out Chat value))
                {
                    value.IsMarkedAsUnread = updateChatIsMarkedAsUnread.IsMarkedAsUnread;
                }
            }
            else if (update is UpdateChatIsTranslatable updateChatIsTranslatable)
            {
                if (_chats.TryGetValue(updateChatIsTranslatable.ChatId, out Chat value))
                {
                    value.IsTranslatable = updateChatIsTranslatable.IsTranslatable;
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
            else if (update is UpdateChatViewAsTopics updateChatViewAsTopics)
            {
                if (_chats.TryGetValue(updateChatViewAsTopics.ChatId, out Chat value))
                {
                    value.ViewAsTopics = updateChatViewAsTopics.ViewAsTopics;
                }
            }
            else if (update is UpdateChatBusinessBotManageBar updateChatBusinessBotManageBar)
            {
                if (_chats.TryGetValue(updateChatBusinessBotManageBar.ChatId, out Chat value))
                {
                    value.BusinessBotManageBar = updateChatBusinessBotManageBar.BusinessBotManageBar;
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
            else if (update is UpdateDiceEmojis updateDiceEmojis)
            {
                _diceEmojis = updateDiceEmojis.Emojis.ToArray();
            }
            else if (update is UpdateFavoriteStickers updateFavoriteStickers)
            {
                _favoriteStickers = updateFavoriteStickers.StickerIds;
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
                _activeReactions = updateReactions.Emojis;
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
            else if (update is UpdateDefaultBackground updateDefaultBackground)
            {
                if (updateDefaultBackground.ForDarkTheme)
                {
                    _selectedBackgroundDark = updateDefaultBackground.Background;
                }
                else
                {
                    _selectedBackground = updateDefaultBackground.Background;
                }
            }
            else if (update is UpdateSpeechRecognitionTrial updateSpeechRecognitionTrial)
            {
                _speechRecognitionTrial = updateSpeechRecognitionTrial;
            }
            else if (update is UpdateStoryStealthMode updateStoryStealthMode)
            {
                _storyStealthMode = updateStoryStealthMode;
            }
            else if (update is UpdateSupergroup updateSupergroup)
            {
                _supergroups[updateSupergroup.Supergroup.Id] = updateSupergroup.Supergroup;
            }
            else if (update is UpdateSupergroupFullInfo updateSupergroupFullInfo)
            {
                _supergroupsFull[updateSupergroupFullInfo.SupergroupId] = updateSupergroupFullInfo.SupergroupFullInfo;
            }
            else if (update is UpdateUnreadChatCount updateUnreadChatCount)
            {
                SetUnreadCount(updateUnreadChatCount.ChatList, chatCount: updateUnreadChatCount);
            }
            else if (update is UpdateUserFullInfo updateUserFullInfo)
            {
                _usersFull[updateUserFullInfo.UserId] = updateUserFullInfo.UserFullInfo;
            }
            else if (update is UpdateUserStatus updateUserStatus)
            {
                if (_users.TryGetValue(updateUserStatus.UserId, out User value))
                {
                    value.Status = updateUserStatus.Status;
                }
            }
            else if (update is UpdateUnconfirmedSession updateUnconfirmedSession)
            {
                _unconfirmedSession = updateUnconfirmedSession.Session;
            }
            else if (update is UpdateAttachmentMenuBots updateAttachmentMenuBots)
            {
                _attachmentMenuBots = updateAttachmentMenuBots.Bots;
            }
            else if (update is UpdateAccentColors updateAccentColors)
            {
                var colors = new Dictionary<int, NameColor>();

                for (int i = 0; i < 7; i++)
                {
                    colors[i] = new NameColor(i);
                }

                foreach (var color in updateAccentColors.Colors)
                {
                    colors[color.Id] = new NameColor(color);
                }

                AvailableAccentColors = updateAccentColors.AvailableAccentColorIds.ToList();
                AccentColors = colors;
            }
            else if (update is UpdateProfileAccentColors updateProfileAccentColors)
            {
                var colors = new Dictionary<int, ProfileColor>();

                foreach (var color in updateProfileAccentColors.Colors)
                {
                    colors[color.Id] = new ProfileColor(color);
                }

                AvailableProfileColors = updateProfileAccentColors.AvailableAccentColorIds.ToList();
                ProfileColors = colors;
            }
            else if (update is UpdateSavedMessagesTags updateSavedMessagesTags)
            {
                lock (_savedMessagesTags)
                {
                    if (updateSavedMessagesTags.SavedMessagesTopicId == 0)
                    {
                        var temp = new List<MessageTag>(updateSavedMessagesTags.Tags.Tags.Count);

                        foreach (var tag in updateSavedMessagesTags.Tags.Tags)
                        {
                            if (_savedMessagesTags.TryGetValue(tag.Tag, out MessageTag cache))
                            {
                                cache.Count = tag.Count;
                                cache.Label = tag.Label;
                                temp.Add(cache);
                            }
                            else
                            {
                                temp.Add(new MessageTag(tag));
                            }
                        }

                        _savedMessagesTags.Clear();

                        foreach (var tag in temp)
                        {
                            _savedMessagesTags[tag.Tag] = tag;
                        }
                    }
                }
            }
            else if (update is UpdateSuggestedActions updateSuggestedActions)
            {
                lock (_suggestedActions)
                {
                    foreach (var action in updateSuggestedActions.RemovedActions)
                    {
                        _suggestedActions.Remove(action);
                    }

                    foreach (var action in updateSuggestedActions.AddedActions)
                    {
                        _suggestedActions.Add(action);
                    }
                }
            }
            else if (update is UpdateQuickReplyShortcut updateQuickReplyShortcut)
            {
                if (_quickReplyShortcuts.TryGetValue(updateQuickReplyShortcut.Shortcut.Id, out var value))
                {
                    value.Shortcut = updateQuickReplyShortcut.Shortcut;
                }
                else
                {
                    _quickReplyShortcuts[updateQuickReplyShortcut.Shortcut.Id] = new QuickReplyShortcutInfo
                    {
                        Shortcut = updateQuickReplyShortcut.Shortcut
                    };
                }
            }
            else if (update is UpdateQuickReplyShortcutDeleted updateQuickReplyShortcutDeleted)
            {
                _quickReplyShortcuts.Remove(updateQuickReplyShortcutDeleted.ShortcutId);
            }
            else if (update is UpdateQuickReplyShortcutMessages updateQuickReplyShortcutMessages)
            {
                if (_quickReplyShortcuts.TryGetValue(updateQuickReplyShortcutMessages.ShortcutId, out var value))
                {
                    value.Messages = updateQuickReplyShortcutMessages.Messages;
                }
                else
                {
                    _quickReplyShortcuts[updateQuickReplyShortcutMessages.ShortcutId] = new QuickReplyShortcutInfo
                    {
                        Messages = updateQuickReplyShortcutMessages.Messages
                    };
                }
            }
            else if (update is UpdateQuickReplyShortcuts updateQuickReplyShortcuts)
            {
                _quickReplyShortcutIds = updateQuickReplyShortcuts.ShortcutIds.ToList();
            }
            else if (update is UpdateContactCloseBirthdays updateContactCloseBirthdays)
            {
                _contactCloseBirthdays = updateContactCloseBirthdays;
            }
            else if (update is UpdateAvailableMessageEffects updateAvailableMessageEffects)
            {
                _availableMessageEffects = updateAvailableMessageEffects;
            }
            else if (update is UpdateOwnedStarCount updateOwnedStarCount)
            {
                OwnedStarCount = updateOwnedStarCount.StarCount;
            }

            _aggregator.Publish(update);
        }

        private readonly Dictionary<int, QuickReplyShortcutInfo> _quickReplyShortcuts = new();
        private IList<int> _quickReplyShortcutIds;
    }

    public class QuickReplyShortcutInfo
    {
        public QuickReplyShortcut Shortcut { get; set; }

        public IList<QuickReplyMessage> Messages { get; set; }
    }

    public class ChatListUnreadCount
    {
        public ChatList ChatList { get; set; }

        public UpdateUnreadChatCount UnreadChatCount { get; set; }
        public UpdateUnreadMessageCount UnreadMessageCount { get; set; }
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
