//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Native;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Storage;

namespace Telegram.Services
{
    public partial interface IClientService : ICacheService
    {
        bool TryInitialize();
        void Close(bool restart);
        void Delete(bool restart);

        //void Send(Function function);
        //void Send(Function function, ClientResultHandler handler);
        void Send(Function function, Action<Object> handler = null);
        Task<Object> SendAsync(Function function);
        Task<Object> SendPaymentAsync(long starCount, Function function);

        void GetReplyTo(MessageViewModel message, Action<Object> handler);
        void GetStory(long storyPosterChatId, int storyId, Action<Object> handler);

        Task<Object> CheckChatInviteLinkAsync(string inviteLink);

        Task<File> GetFileAsync(int fileId);
        Task<StorageFile> GetFileAsync(File file, bool completed = true);
        Task<StorageFile> GetPermanentFileAsync(File file);

        void DownloadFile(int fileId, int priority, long offset = 0, long limit = 0, bool synchronous = false);
        Task<File> DownloadFileAsync(File file, int priority, long offset = 0, long limit = 0);

        void AddFileToDownloads(File file, long chatId, long messageId, int priority = 30);
        void CancelDownloadFile(File file, bool onlyIfPending = false, bool onlyIfStreaming = false);
        bool IsDownloadFileCanceled(int fileId);
        bool IsDownloadFileImplicit(int fileId);
        void TrackStreamingFile(int fileId, bool streaming);

        void PrepareLogs(int fileId, int verbosityLevel);

        Task<Object> GetCustomEmojiStickerSets(Vector<long> customEmojiIds);
        Task<bool> HasPrivacySettingsRuleAsync<T>(UserPrivacySetting setting) where T : UserPrivacySettingRule;

        Task<Chats> GetChatListAsync(ChatList chatList, int offset, int limit);

        void LoadFullInfo(Chat chat);

        void ViewMessages(long chatId, MessageTopic topicId, Vector<long> messageIds, MessageSource source, bool forceRead);

        Task<Object> GetStarTransactionsAsync(MessageSender ownerId, string subscriptionId, TransactionDirection direction, string offset, int limit);

        Sticker NextGreetingSticker();

        ISession Session { get; }
        int SessionId { get; }
    }

    public partial interface ICacheService
    {
        bool IsPremium { get; }
        bool IsPremiumAvailable { get; }

        double UnixTime { get; }
        long UnixTimeMilliseconds { get; }

        bool TranslateMessages { get; }
        bool TranslateChats { get; }

        PaidReactionType DefaultPaidReactionType { get; }

        StarAmount OwnedStarCount { get; }

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

        Vector<ChatFolderInfo> ChatFolders { get; }
        int MainChatListPosition { get; }
        bool AreTagsEnabled { get; }

        Vector<AttachmentMenuBot> AttachmentMenuBots { get; }

        IList<AttachmentMenuBot> GetBotsForChat(long chatId);
        IList<AttachmentMenuBot> GetBotsForMenu(out long hash);

        UpdateAvailableMessageEffects AvailableMessageEffects { get; }

        Vector<string> ActiveReactions { get; }

        Vector<TextCompositionStyle> TextCompositionStyles { get; }
        bool IsTextCompositionStyleInstalled(string name);

        Vector<string> AnimationSearchEmojis { get; }
        string AnimationSearchProvider { get; }

        UpdateSpeechRecognitionTrial SpeechRecognitionTrial { get; }

        AgeVerificationParameters AgeVerificationParameters { get; }

        Vector<CloseBirthdayUser> CloseBirthdayUsers { get; }

        Background GetDefaultBackground(bool darkTheme);
        Background DefaultBackground { get; }

        Task<AuthorizationState> GetAuthorizationStateAsync();
        AuthorizationState AuthorizationState { get; }
        ConnectionState ConnectionState { get; }
        StakeDiceState StakeDiceState { get; }
        UpdateFreezeState FreezeState { get; }

        ChatMemberStatus GetChatMemberStatus(Chat chat, out bool channel);

        string GetTitle(Chat chat, bool tiny = false);
        string GetTitle(long chatId, bool tiny = false);
        string GetTitle(MessageOrigin origin, MessageImportInfo import);
        string GetTitle(MessageSender sender, bool firstName = false);

        IList<ChatFolderInfo> GetChatFolders(Chat chat);

        bool TryGetCachedReaction(string emoji, out EmojiReaction value);
        Task<IDictionary<string, EmojiReaction>> GetAllReactionsAsync();
        Task<IDictionary<string, EmojiReaction>> GetReactionsAsync(IEnumerable<string> reactions);

        Task<IDictionary<MessageId, MessageProperties>> GetMessagePropertiesAsync(IEnumerable<MessageId> messageIds);

        Chat GetChat(long id);
        IEnumerable<Chat> GetChats(IEnumerable<long> ids);

        IDictionary<MessageSender, ChatAction> GetChatActions(long id, MessageTopic topicId = null);

        QuickReplyShortcut GetQuickReplyShortcut(int id);
        QuickReplyShortcut GetQuickReplyShortcut(string name);
        Vector<QuickReplyMessage> GetQuickReplyMessages(int id);
        Vector<QuickReplyShortcut> GetQuickReplyShortcuts();
        void LoadQuickReplyShortcuts();
        bool CheckQuickReplyShortcutName(string name);

        Vector<WelcomeMessage> GetWelcomeMessages(long chatId);

        Task<IList<MessageEffect>> GetMessageEffectsAsync(IEnumerable<long> effectIds);
        MessageEffect LoadMessageEffect(long effectId, bool preload);

        bool IsSavedMessages(MessageSender sender);
        bool IsSavedMessages(User user);
        bool IsSavedMessages(Chat chat);

        bool HasActiveUsername(Chat chat, out string username);
        bool HasActiveUsername(MessageSender sender, out string username);

        bool IsForum(Chat chat);
        bool IsDirectMessagesGroup(Chat chat);
        bool IsAdministeredDirectMessagesGroup(Chat chat);
        bool HasTabs(Chat chat);

        bool IsPaid(Chat chat);
        long PaidMessageStarCount(Chat chat);

        bool IsChatAccessible(Chat chat);

        bool IsBotAddedToAttachmentMenu(long userId);

        bool CanPostMessages(Chat chat);
        bool CanInviteUsers(Chat chat);
        bool CanPromoteMembers(Chat chat);
        bool CanEditTag(Chat chat, ChatMember member);

        Object GetMessageSender(MessageSender sender);
        bool TryGetMessageSender(MessageSender sender, out Object value);

        bool TryGetChat(long chatId, out Chat chat);
        bool TryGetChat(MessageSender sender, out Chat value);
        bool TryGetChat(AffiliateType type, out Chat value);
        bool TryGetChat(SavedMessagesTopicType type, out Chat chat);

        bool TryGetChatFromUser(long userId, out long value);
        bool TryGetChatFromUser(long userId, out Chat value);
        bool TryGetActiveStoriesFromUser(long userId, out ChatActiveStories activeStories);

        Task<Chat> GetChatFromMessageSenderAsync(MessageSender messageSender);

        bool TryGetTimeZone(string timeZoneId, out TimeZone timeZone);

        SecretChat GetSecretChat(int id);
        SecretChat GetSecretChat(Chat chat);
        SecretChat GetSecretChatForUser(long id);

        User GetUser(Chat chat);
        User GetUser(long id);
        bool TryGetUser(long id, out User value);
        bool TryGetUser(Chat chat, out User value);
        bool TryGetUser(MessageSender sender, out User value);
        bool TryGetUser(AffiliateType type, out User value);

        UserFullInfo GetUserFull(long id);
        UserFullInfo GetUserFull(Chat chat);
        bool TryGetUserFull(long id, out UserFullInfo value);
        bool TryGetUserFull(Chat chat, out UserFullInfo value);

        IEnumerable<User> GetUsers(IEnumerable<long> ids);

        ChatPermissions GetPermissions(Chat chat, out bool restricted);

        BasicGroup GetBasicGroup(long id);
        BasicGroup GetBasicGroup(Chat chat);
        bool TryGetBasicGroup(long id, out BasicGroup value);
        bool TryGetBasicGroup(Chat chat, out BasicGroup value);
        bool TryGetBasicGroup(MessageSender sender, out BasicGroup value);

        BasicGroupFullInfo GetBasicGroupFull(long id);
        BasicGroupFullInfo GetBasicGroupFull(Chat chat);
        bool TryGetBasicGroupFull(long id, out BasicGroupFullInfo value);
        bool TryGetBasicGroupFull(Chat chat, out BasicGroupFullInfo value);

        Supergroup GetSupergroup(long id);
        Supergroup GetSupergroup(Chat chat);
        bool TryGetSupergroup(long id, out Supergroup value);
        bool TryGetSupergroup(Chat chat, out Supergroup value);
        bool TryGetSupergroup(MessageSender sender, out Supergroup value);

        SupergroupFullInfo GetSupergroupFull(long id);
        SupergroupFullInfo GetSupergroupFull(Chat chat);
        bool TryGetSupergroupFull(long id, out SupergroupFullInfo value);
        bool TryGetSupergroupFull(Chat chat, out SupergroupFullInfo value);

        Community GetCommunity(long id);
        bool TryGetCommunity(long id, out Community value);
        CommunityFullInfo GetCommunityFull(long id);
        bool TryGetCommunityFull(long id, out CommunityFullInfo value);

        GroupCall GetGroupCall(int id);
        bool TryGetGroupCall(int id, out GroupCall value);
        bool TryGetGroupCallMessageLevel(long paidMessageStarCount, out GroupCallMessageLevel value);
        bool TryGetGroupCallMinimumMessageLevel(int length, int customEmojiCount, out GroupCallMessageLevel value);

        MessageTag GetSavedMessagesTag(ReactionType reaction);
        bool TryGetSavedMessagesTag(ReactionType reaction, out MessageTag value);

        int GetMembersCount(long chatId);
        int GetMembersCount(Chat chat);

        Task<BotVerification> GetBotVerificationAsync(Chat chat);

        bool IsAnimationSaved(int id);
        bool IsStickerRecent(int id);
        bool IsStickerFavorite(int id);
        bool IsStickerSetInstalled(long id);

        bool TryGetMediaAlbum(long chatId, long mediaAlbumId, out MessageAlbumLastMessage album);

        ICollection<ChatListUnreadCount> UnreadCounts { get; }
        ChatListUnreadCount GetUnreadCount(ChatList chatList);

        UpdateStoryStealthMode StealthMode { get; }

        bool TryGetEmojiChatTheme(ChatTheme theme, out EmojiChatTheme emoji);
        bool TryGetEmojiChatTheme(string themeName, out EmojiChatTheme emoji);
        Vector<EmojiChatTheme> ChatThemes { get; }

        bool IsDiceEmoji(string text, out string dice);

        bool HasSuggestedAction(SuggestedAction action);

        Settings.NotificationsSettings Notifications { get; }

        void AddRecentlyOpenedChat(long chatId);
        int RecentlyOpenedChatsCount { get; }
        IList<Chat> GetRecentlyOpenedChats();
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

        private readonly ISession _session;

        private readonly IDeviceInfoService _deviceInfoService;
        private readonly ISettingsService _settings;
        private readonly IOptionsService _options;
        private readonly ILocaleService _locale;
        private readonly IEventAggregator _aggregator;

        private readonly ReaderWriterDictionary<long, MessageEffect> _effects = new();

        private readonly ReaderWriterDictionary<long, Chat> _chats = new(1000);

        private readonly ConcurrentDictionary<long, ConcurrentDictionary<MessageSender, ChatAction>> _chatActions = new();
        private readonly ConcurrentDictionary<ChatMessageTopic, ConcurrentDictionary<MessageSender, ChatAction>> _topicActions = new();

        private readonly ReaderWriterDictionary<int, SecretChat> _secretChats = new();

        private readonly ReaderWriterDictionary<long, long> _usersToChats = new(500);

        private readonly ReaderWriterDictionary<long, User> _users = new(500);
        private readonly ReaderWriterDictionary<long, UserFullInfo> _usersFull = new(500);

        private readonly ReaderWriterDictionary<long, BasicGroup> _basicGroups = new(500);
        private readonly ReaderWriterDictionary<long, BasicGroupFullInfo> _basicGroupsFull = new(500);

        private readonly ReaderWriterDictionary<long, Supergroup> _supergroups = new(500);
        private readonly ReaderWriterDictionary<long, SupergroupFullInfo> _supergroupsFull = new(500);

        private readonly ReaderWriterDictionary<long, Community> _communities = new();
        private readonly ReaderWriterDictionary<long, CommunityFullInfo> _communitiesFull = new();

        private readonly ReaderWriterDictionary<int, GroupCall> _groupCalls = new();

        private readonly ReaderWriterDictionary<long, Vector<WelcomeMessage>> _welcomeMessages = new();

        private readonly ConcurrentDictionary<int, ChatListUnreadCount> _unreadCounts = new();

        private readonly ReaderWriterDictionary<long, MessageAlbumLastMessageService> _lastMessageAlbums = new();

        // Files are currently accessed only from TDLib thread
        private readonly Dictionary<int, File> _files = new();

        private readonly List<long> _recentChats = new();
        private readonly object _recentChatsLock = new();

        // A lock rather than a ReaderWriterDictionary, which is what the rest of the caches
        // use, because neither of the two things that make that work applies here: this is a
        // set, not a dictionary, and the field is assigned null — so the operation that
        // matters is compound. UpdateFile removes an id and, only if that emptied the set,
        // restores the verbosity and clears the field; PrepareLogs creates the set and adds
        // to it. A per-call lock inside a collection cannot make either atomic, and without
        // that, a PrepareLogs landing between its own ??= and its Add dereferences null.
        private readonly object _preparedLogsLock = new();
        private HashSet<int> _preparedLogsFileIds;
        private int _preparedLogsVerbosity = -1;

        private UnconfirmedSession _unconfirmedSession;

        private Vector<string> _diceEmojis;

        private Vector<GroupCallMessageLevel> _groupCallMessageLevels;

        private Vector<int> _savedAnimations;
        private Vector<int> _recentStickers;
        private Vector<int> _favoriteStickers;
        private Vector<long> _installedStickerSets;
        private Vector<long> _installedMaskSets;
        private Vector<long> _installedEmojiSets;
        private Vector<TextCompositionStyle> _textCompositionStyles;

        private ReactionType _defaultReaction;

        private Vector<ChatFolderInfo> _chatFolders = Array.Empty<ChatFolderInfo>();
        private Dictionary<int, ChatFolderInfo> _chatFolders2 = new();
        private readonly object _chatFoldersLock = new();
        private int _mainChatListPosition = 0;
        private bool _areTagsEnabled;

        private UpdateAvailableMessageEffects _availableMessageEffects;

        private Vector<string> _activeReactions = Array.Empty<string>();
        private readonly ReaderWriterDictionary<string, EmojiReaction> _cachedReactions = new();

        private Vector<AttachmentMenuBot> _attachmentMenuBots = Array.Empty<AttachmentMenuBot>();

        private UpdateSpeechRecognitionTrial _speechRecognitionTrial;

        private UpdateAnimationSearchParameters _animationSearchParameters;

        private UpdateEmojiChatThemes _chatThemes;

        private UpdateStoryStealthMode _storyStealthMode = new();

        private UpdateContactCloseBirthdays _contactCloseBirthdays;

        private readonly Dictionary<ReactionType, MessageTag> _savedMessagesTags = new(new ReactionTypeEqualityComparer());

        private TaskCompletionSource<bool> _authorizationStateTask = new();
        private AuthorizationState _authorizationState;
        private ConnectionState _connectionState;
        private UpdateFreezeState _freezeState = new();
        private StakeDiceState _stakeDiceState = new();

        private StarAmount _ownedStarCount;
        private long? _ownedGramCount;

        private JsonValueObject _config;

        private Background _selectedBackground;
        private Background _selectedBackgroundDark;

        private bool _cleanAfterClose;
        private bool _initializeAfterClose;

        private static readonly Thread _runThread;

        static ClientService()
        {
            InitializeDiagnostics();

            _runThread = new Thread(Client.Run)
            {
                Name = "TdReceive",
                IsBackground = true
            };
            _runThread.Start();
        }

        public ClientService(ISession session, bool online, IDeviceInfoService deviceInfoService, ISettingsService settings, ILocaleService locale, IEventAggregator aggregator, IDownloadFolderService downloadFolder)
        {
            _session = session;
            _deviceInfoService = deviceInfoService;
            _settings = settings;
            _locale = locale;
            _options = new OptionsService(this);
            _aggregator = aggregator;
            _downloadFolder = downloadFolder;

            Initialize(online);
        }

        public void ViewMessages(long chatId, MessageTopic topicId, Vector<long> messageIds, MessageSource source, bool forceRead)
        {
            Send(new ViewMessages(chatId, messageIds, source, forceRead));

            if (source is MessageSourceForumTopicHistory && topicId is MessageTopicForum forumTopic && _forums.TryGetValue(chatId, out ForumTopicService manager))
            {
                manager.ViewMessages(forumTopic.ForumTopicId, messageIds);
            }
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
            _cleanAfterClose = false;
            _client.Send(new Close());
        }

        public void Delete(bool restart)
        {
            _initializeAfterClose = restart;
            _cleanAfterClose = true;
            _client.Send(new Close());
        }

        private void Initialize(bool online = true)
        {
            _client = new Client(this);

            Task.Factory.StartNew(() =>
            {
                var useMessageDatabase = true;

                if (AppSettings.Diagnostics.DisableDatabase)
                {
                    // ¯\_(ツ)_/¯
                    useMessageDatabase = false;
                }

                var deviceModel = AppSettings.Diagnostics.DeviceName;
                if (deviceModel.Length == 0)
                {
                    deviceModel = _deviceInfoService.DeviceModel;
                }

                InitializeDiagnostics();
                InitializeFlush();

                _client.Send(new SetOption("ignore_background_updates", new OptionValueBoolean(AppSettings.Diagnostics.DisableDatabase)));
                _client.Send(new SetOption("language_pack_database_path", new OptionValueString(System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, "langpack"))));
                _client.Send(new SetOption("localization_target", new OptionValueString(LocaleService.LANGPACK)));
                _client.Send(new SetOption("language_pack_id", new OptionValueString(AppSettings.LanguagePackId)));
                //_client.Send(new SetOption("online", new OptionValueBoolean(online)));
                _client.Send(new SetOption("online", new OptionValueBoolean(false)));
                _client.Send(new SetOption("use_pfs", new OptionValueBoolean(true)));
                _client.Send(new SetOption("notification_group_count_max", new OptionValueInteger(25)));
                _client.Send(new SetOption("storage_max_time_from_last_access", new OptionValueInteger(AppSettings.Diagnostics.StorageMaxTimeFromLastAccess)));
                _client.Send(new SetTdlibParameters(
                    useTestDc: _settings.UseTestDC,
                    databaseDirectory: System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, $"{_session.Id}"),
                    filesDirectory: string.Empty,
                    databaseEncryptionKey: null,
                    useFileDatabase: true,
                    useChatInfoDatabase: true,
                    useMessageDatabase: useMessageDatabase,
                    useSecretChats: true,
                    apiId: Constants.ApiId,
                    apiHash: Constants.ApiHash,
                    systemLanguageCode: _deviceInfoService.SystemLanguageCode,
                    deviceModel: deviceModel,
                    systemVersion: _deviceInfoService.SystemVersion,
                    applicationVersion: _deviceInfoService.ApplicationVersion));
                Send(new GetApplicationConfig(), UpdateConfig);
            });
        }

        private static void InitializeDiagnostics()
        {
            TdThroughput.Enabled = AppSettings.Diagnostics.MeasureDeserialization;

            Client.Execute(new SetLogStream(new LogStreamFile(System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, "tdlib_log.txt"), 100 * 1024 * 1024, false)));
            Client.Execute(new SetLogVerbosityLevel(AppSettings.VerbosityLevel));

            var tags = Client.Execute(new GetLogTags()) as LogTags;
            if (tags == null)
            {
                return;
            }

            foreach (var tag in tags.Tags)
            {
                var level = Client.Execute(new GetLogTagVerbosityLevel(tag)) as LogVerbosityLevel;

                var saved = AppSettings.Diagnostics.GetValueOrDefault(tag, -1);
                if (saved != level.VerbosityLevel && saved > -1)
                {
                    Client.Execute(new SetLogTagVerbosityLevel(tag, saved));
                }
            }
        }

        private void InitializeReady()
        {
            Send(new CreatePrivateChat(Options.MyId, true));
            Send(new LoadChats(new ChatListMain(), 20));
            Send(new SearchEmojis("cucumber", new[] { NativeUtils.GetKeyboardCulture() }));

            UpdateGreetingStickers();
            UpdateTimeZones();
        }

        private void InitializeFlush()
        {
            // Not awaited and not on the startup path: walking a few thousand files and deleting
            // some of them is not worth doing while the app is still opening.
            _ = Task.Run(FlushStickerCache);
        }

        /// <summary>
        /// Drops cache files the app will not read again: the two dead formats outright, and frame
        /// caches that have not been touched for three days.
        /// </summary>
        /// <remarks>
        /// Note this only covers the per-session sticker directory. Caches built by
        /// LottieAnimation.LoadFromData - dice, and anything else identified by a key rather than a
        /// file - sit in the root of LocalState and are not swept, as they were not before either.
        /// There are a handful of them.
        ///
        /// Ageing is by last access, which NTFS stops updating by default on Windows 10 1803 and
        /// later. So a cache in daily use can still look three days idle and be rebuilt. That is
        /// wasteful rather than wrong, and the alternative - touching every file as it is opened -
        /// costs a write per animation load.
        /// </remarks>
        private void FlushStickerCache()
        {
            var path = System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, $"{_session.Id}", "stickers");

            try
            {
                var directory = new System.IO.DirectoryInfo(path);
                if (!directory.Exists)
                {
                    return;
                }

                var expiration = DateTime.Now.AddDays(-3);

                // Enumerated as FileInfo rather than paths: the scan already carries the
                // timestamps, where File.GetLastAccessTime on each name is a separate stat, and
                // there are thousands of them.
                foreach (var file in directory.EnumerateFiles())
                {
                    var expired = file.Extension switch
                    {
                        // The formats the frame cache replaced. Nothing will ever read one again,
                        // so age does not come into it - and the old sweep never matched .tcache
                        // at all, which is why so many are still on disk.
                        ".cache" or ".tcache" => true,
                        ".tgfc" => file.LastAccessTime < expiration,
                        _ => false
                    };

                    if (expired)
                    {
                        try
                        {
                            file.Delete();
                        }
                        catch
                        {
                            // In use, or gone since the scan. Either way it is not ours to insist.
                        }
                    }
                }
            }
            catch
            {
                // The directory can disappear underneath the walk when a session is removed.
            }
        }

        private bool _translateMessages;
        private bool _translateChats;

        private void UpdateConfig(Object value)
        {
            if (value is JsonValueObject obj)
            {
                _config = obj;

                var translationsManual = obj.GetNamedString("translations_manual_enabled", "disabled");
                var translationsAuto = obj.GetNamedString("translations_auto_enabled", "disabled");

                _translateMessages = translationsManual != "disabled";
                _translateChats = translationsAuto != "disabled";
            }
        }

        public bool TranslateMessages => _translateMessages && _settings.Translate.Messages;

        public bool TranslateChats => _translateChats && _settings.Translate.Chats;

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
            lock (_timezones)
            {
                return _timezones.TryGetValue(timeZoneId, out timeZone);
            }
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

        private Vector<Sticker> _greetingStickers;
        private Sticker _nextGreetingSticker;
        private bool _waitGreetingSticker;

        private readonly Dictionary<string, TimeZone> _timezones = new();

        public UpdateAvailableMessageEffects AvailableMessageEffects => _availableMessageEffects;

        public Vector<string> ActiveReactions => _activeReactions;

        public Vector<TextCompositionStyle> TextCompositionStyles => _textCompositionStyles ?? Array.Empty<TextCompositionStyle>();

        public bool IsTextCompositionStyleInstalled(string name)
        {
            return TextCompositionStyles.Any(x => x.Name == name);
        }

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

        private void Clear()
        {
            _localTicksAtSync = 0;
            _serverTimeAtSync = 0;
            _options.Clear();

            _files.Clear();
            _effects.Clear();

            _activeReactions = Array.Empty<string>();
            _cachedReactions.Clear();

            _chats.Clear();

            // Clear() runs on the TDLib thread while the UI may have a list load in flight,
            // so everything below takes the same lock its readers do. One after another and
            // never nested, so no ordering is introduced.
            lock (_chatList)
            {
                _chatList.Clear();
                _haveFullChatList.Clear();
            }

            _chatActions.Clear();
            _topicActions.Clear();

            _secretChats.Clear();

            _usersToChats.Clear();

            _users.Clear();
            _usersFull.Clear();

            _basicGroups.Clear();
            _basicGroupsFull.Clear();

            _supergroups.Clear();
            _supergroupsFull.Clear();

            _communities.Clear();
            _communitiesFull.Clear();

            _groupCalls.Clear();

            _welcomeMessages.Clear();

            _forums.Clear();
            _directMessagesChats.Clear();

            // _activeStories holds the story state itself; clearing only the ordering left
            // the previous account's stories reachable through GetActiveStories.
            _activeStories.Clear();

            lock (_storyList)
            {
                _storyList.Clear();
                _haveFullStoryList.Clear();
            }

            lock (_savedMessages)
            {
                _haveFullSavedMessages = false;
                _savedMessages.Clear();
            }

            _savedMessagesTopics.Clear();

            lock (_savedMessagesTags)
            {
                _savedMessagesTags.Clear();
            }

            _settings.Notifications.Scope.Clear();

            _unreadCounts.Clear();

            _diceEmojis = null;

            _groupCallMessageLevels = null;

            lock (_suggestedActions)
            {
                _suggestedActions.Clear();
            }

            _savedAnimations = null;
            _recentStickers = null;
            _favoriteStickers = null;
            _installedStickerSets = null;
            _installedMaskSets = null;
            _installedEmojiSets = null;
            _textCompositionStyles = null;

            lock (_chatFoldersLock)
            {
                _chatFolders = Array.Empty<ChatFolderInfo>();
                _chatFolders2.Clear();
            }

            _mainChatListPosition = 0;
            _areTagsEnabled = false;

            lock (_timezones)
            {
                _timezones.Clear();
            }

            _animationSearchParameters = null;

            _authorizationStateTask = new();
            _authorizationState = null;
            _connectionState = null;
            _stakeDiceState = new();
            _freezeState = new();

            _config = null;
            _translateMessages = false;
            _translateChats = false;
            _defaultReaction = null;
            _attachmentMenuBots = Array.Empty<AttachmentMenuBot>();
            _availableMessageEffects = null;
            _speechRecognitionTrial = null;
            _chatThemes = null;
            _storyStealthMode = new();
            _contactCloseBirthdays = null;
            _unconfirmedSession = null;
            AccentColors = null;
            AvailableAccentColors = null;
            ProfileColors = null;
            AvailableProfileColors = null;
            _ownedStarCount = null;
            _ownedGramCount = null;
            _requestedStarCount = false;
            _requestedGramCount = false;
            DefaultPaidReactionType = new PaidReactionTypeRegular();
            AgeVerificationParameters = null;
            SavedMessagesTopicCount = 0;
            _quickReplyShortcuts.Clear();
            _quickReplyShortcutIds = null;
            _quickReplyShortcutsRequested = false;
            _selectedBackground = null;
            _selectedBackgroundDark = null;

            _lastMessageAlbums.Clear();

            lock (_recentChatsLock)
            {
                _recentChats.Clear();
            }

            _greetingStickers = null;
            _nextGreetingSticker = null;
            _waitGreetingSticker = false;

            _chatAccessibleUntil.Clear();

            lock (_preparedLogsLock)
            {
                _preparedLogsFileIds = null;
                _preparedLogsVerbosity = -1;
            }

            ClearDownloads();

            if (_cleanAfterClose)
            {
                _cleanAfterClose = false;
                DeleteDatabase();
            }

            if (_initializeAfterClose)
            {
                _initializeAfterClose = false;
                Initialize();
            }
        }

        private void DeleteDatabase()
        {
            var databasePath = System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, $"{_session.Id}", "db.sqlite");
            if (System.IO.File.Exists(databasePath))
            {
                try
                {
                    System.IO.File.Delete(databasePath);
                }
                catch
                {
                    // Shit happens...
                }
            }
            if (System.IO.File.Exists(databasePath + "-shm"))
            {
                try
                {
                    System.IO.File.Delete(databasePath + "-shm");
                }
                catch
                {
                    // Shit happens...
                }
            }
            if (System.IO.File.Exists(databasePath + "-wal"))
            {
                try
                {
                    System.IO.File.Delete(databasePath + "-wal");
                }
                catch
                {
                    // Shit happens...
                }
            }
        }

        public void Send(Function function, Action<Object> handler = null)
        {
            _client.Send(function, handler);
        }

        public Task<Object> SendAsync(Function function)
        {
            var tsc = new TaskCompletionSource<Object>();
            _client.Send(function, tsc.SetResult);

            return tsc.Task;
        }

        public async Task<Object> SendPaymentAsync(long starCount, Function function)
        {
            if (OwnedStarCount.StarCount < starCount)
            {
                var updated = await GetStarTransactionsAsync(MyId, string.Empty, null, string.Empty, 1) as StarTransactions;
                if (updated is null || updated.StarAmount.StarCount < starCount)
                {
                    return new ErrorStarsNeeded();
                }
            }

            return await SendAsync(function);
        }



        public void GetReplyTo(MessageViewModel message, Action<Object> handler)
        {
            if (message.ReplyTo is MessageReplyToMessage replyToMessage ||
                message.Content is MessagePinMessage or
                MessageGameScore or
                MessagePaymentSuccessful or
                MessageChecklistTasksAdded or
                MessageChecklistTasksDone or
                MessagePollOptionAdded or
                MessagePollOptionDeleted or
                MessageSuggestedPostPaid or
                MessageSuggestedPostRefunded)
            {
                Send(new GetRepliedMessage(message.ChatId, message.Id), handler);
            }
            else if (message.ReplyTo is MessageReplyToStory replyToStory)
            {
                GetStory(replyToStory.StoryPosterChatId, replyToStory.StoryId, handler);
            }
        }

        public void GetStory(long storyPosterChatId, int storyId, Action<Object> handler)
        {
            Send(new GetStory(storyPosterChatId, storyId, true), result =>
            {
                if (result is Error)
                {
                    Send(new GetStory(storyPosterChatId, storyId, false), handler);
                }
                else
                {
                    handler(result);
                }
            });
        }



        private readonly ReaderWriterDictionary<long, DateTime> _chatAccessibleUntil = new();

        public async Task<Object> CheckChatInviteLinkAsync(string inviteLink)
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



        public void DownloadFile(int fileId, int priority, long offset = 0, long limit = 0, bool synchronous = false)
        {
            TrackExplicitDownload(fileId);
            Send(new DownloadFile(fileId, priority, offset, limit, synchronous));
        }

        public async Task<File> DownloadFileAsync(File file, int priority, long offset = 0, long limit = 0)
        {
            TrackExplicitDownload(file.Id);

            var response = await SendAsync(new DownloadFile(file.Id, priority, offset, limit, true));
            if (response is File updated)
            {
                return updated;
            }

            return file;
        }


        public async Task<Object> GetStarTransactionsAsync(MessageSender ownerId, string subscriptionId, TransactionDirection direction, string offset, int limit)
        {
            var response = await SendAsync(new GetStarTransactions(ownerId, subscriptionId, direction, offset, limit));
            if (response is StarTransactions transactions)
            {
                if (ownerId == null || ownerId.IsUser(Options.MyId))
                {
                    _ownedStarCount = transactions.StarAmount;
                    _aggregator.Publish(new UpdateOwnedStarCount(transactions.StarAmount));
                }
            }

            return response;
        }

        public async Task<Object> GetCustomEmojiStickerSets(Vector<long> customEmojiIds)
        {
            var stickers = await SendAsync(new GetCustomEmojiStickers(customEmojiIds)) as Stickers;
            if (stickers?.StickersValue.Count > 0)
            {
                var setIds = new HashSet<long>();

                foreach (var sticker in stickers.StickersValue)
                {
                    setIds.Add(sticker.SetId);
                }

                var tasks = new List<Task<Object>>(setIds.Count);

                foreach (var setId in setIds)
                {
                    tasks.Add(SendAsync(new GetStickerSet(setId)));
                }

                // Together rather than in turn: one round trip per distinct set otherwise.
                var responses = await Task.WhenAll(tasks);
                var result = new MutableVector<StickerSetInfo>(responses.Length);

                foreach (var response in responses)
                {
                    if (response is StickerSet stickerSet)
                    {
                        result.Add(stickerSet.ToInfo());
                    }
                }

                return new StickerSets(result.Count, result);
            }

            return new Error();
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
            List<long> ids = null;

            foreach (var id in effectIds)
            {
                ids ??= new List<long>();
                ids.Add(id);
            }

            if (ids == null)
            {
                return Array.Empty<MessageEffect>();
            }

            // Indexed by request position, because the effect drawer and the reaction menu
            // both display these in the order they asked for.
            var effects = new MessageEffect[ids.Count];

            List<int> missing = null;
            List<Task<Object>> tasks = null;

            for (int i = 0; i < ids.Count; i++)
            {
                if (_effects.TryGetValue(ids[i], out MessageEffect effect))
                {
                    effects[i] = effect;
                }
                else
                {
                    missing ??= new List<int>();
                    tasks ??= new List<Task<Object>>();

                    missing.Add(i);
                    tasks.Add(SendAsync(new GetMessageEffect(ids[i])));
                }
            }

            if (tasks != null)
            {
                // Together rather than in turn: one round trip per uncached effect otherwise.
                var responses = await Task.WhenAll(tasks);

                for (int i = 0; i < responses.Length; i++)
                {
                    if (responses[i] is MessageEffect item)
                    {
                        _effects[item.Id] = item;
                        effects[missing[i]] = item;
                    }
                }
            }

            List<MessageEffect> result = null;

            foreach (var effect in effects)
            {
                if (effect != null)
                {
                    result ??= new List<MessageEffect>(effects.Length);
                    result.Add(effect);
                }
            }

            return (IList<MessageEffect>)result ?? Array.Empty<MessageEffect>();
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

        public ISession Session => _session;

        public int SessionId => _session.Id;

        public Client Client => _client;

        #region Cache

        public UpdateStoryStealthMode StealthMode => _storyStealthMode;

        public ICollection<ChatListUnreadCount> UnreadCounts => _unreadCounts.Values;

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



        public void AddRecentlyOpenedChat(long chatId)
        {
            lock (_recentChatsLock)
            {
                if (_recentChats.Contains(chatId))
                {
                    _recentChats.Remove(chatId);
                }

                _recentChats.Insert(0, chatId);

                if (_recentChats.Count > 50)
                {
                    _recentChats.RemoveAt(_recentChats.Count - 1);
                }
            }
        }

        public int RecentlyOpenedChatsCount
        {
            get
            {
                lock (_recentChatsLock)
                {
                    return _recentChats.Count;
                }
            }
        }

        public IList<Chat> GetRecentlyOpenedChats()
        {
            lock (_recentChatsLock)
            {
                return GetChats(_recentChats).ToList();
            }
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

        public UpdateFreezeState FreezeState => _freezeState;

        public StakeDiceState StakeDiceState => _stakeDiceState;

        public Settings.NotificationsSettings Notifications => _settings.Notifications;

        public bool IsPremium => _options.IsPremium;

        public bool IsPremiumAvailable => _options.IsPremium || _options.IsPremiumAvailable;

        private long _localTicksAtSync;
        private long _serverTimeAtSync;

        public double UnixTime
        {
            get
            {
                long currentTicks = Stopwatch.GetTimestamp();
                double elapsedSeconds = (double)(currentTicks - _localTicksAtSync) / Stopwatch.Frequency;
                return _serverTimeAtSync + elapsedSeconds;
            }
        }

        public long UnixTimeMilliseconds => (long)(UnixTime * 1000);

        // These two are read from bindings, which re-evaluate. Without the guards the getter
        // sent a fresh request on every read until the update came back.
        private bool _requestedStarCount;
        private bool _requestedGramCount;

        public StarAmount OwnedStarCount
        {
            get
            {
                if (_ownedStarCount == null)
                {
                    if (!_requestedStarCount)
                    {
                        _requestedStarCount = true;
                        Send(new GetStarTransactions(MyId, string.Empty, null, string.Empty, 1));
                    }

                    return new StarAmount(0, 0);
                }

                return _ownedStarCount;
            }
        }

        public long OwnedGramCount
        {
            get
            {
                if (_ownedGramCount == null)
                {
                    if (!_requestedGramCount)
                    {
                        _requestedGramCount = true;
                        Send(new GetTonTransactions(null, string.Empty, 1));
                    }

                    return 0;
                }

                return _ownedGramCount ?? 0;
            }
        }

        public PaidReactionType DefaultPaidReactionType { get; private set; } = new PaidReactionTypeRegular();

        public MessageSender MyId => new MessageSenderUser(_options.MyId);

        public IOptionsService Options => _options;

        public JsonValueObject Config => _config;

        public ReactionType DefaultReaction => _defaultReaction;

        public Vector<ChatFolderInfo> ChatFolders => _chatFolders;

        public int MainChatListPosition => _mainChatListPosition;

        public bool AreTagsEnabled => _areTagsEnabled;

        public Vector<AttachmentMenuBot> AttachmentMenuBots => _attachmentMenuBots;

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

        public AgeVerificationParameters AgeVerificationParameters { get; private set; }

        public Vector<CloseBirthdayUser> CloseBirthdayUsers => _contactCloseBirthdays?.CloseBirthdayUsers ?? Array.Empty<CloseBirthdayUser>();

        public Vector<string> AnimationSearchEmojis => _animationSearchParameters?.Emojis ?? Array.Empty<string>();

        public string AnimationSearchProvider => _animationSearchParameters?.Provider;

        public Background DefaultBackground => GetDefaultBackground(NightModeService.Current.IsDarkTheme());

        public Background GetDefaultBackground(bool darkTheme)
        {
            if (darkTheme)
            {
                return _selectedBackgroundDark;
            }

            return _selectedBackground;
        }

        public ChatMemberStatus GetChatMemberStatus(Chat chat, out bool channel)
        {
            if (TryGetBasicGroup(chat, out BasicGroup basicGroup))
            {
                channel = false;
                return basicGroup.Status;
            }
            else if (TryGetSupergroup(chat, out Supergroup supergroup))
            {
                channel = supergroup.IsChannel;
                return supergroup.Status;
            }

            channel = false;
            return new ChatMemberStatusMember();
        }

        public void LoadFullInfo(Chat chat)
        {
            if (TryGetUser(chat, out User user))
            {
                Send(new GetUserFullInfo(user.Id));
            }
            else if (TryGetSupergroup(chat, out Supergroup supergroup))
            {
                Send(new GetSupergroupFullInfo(supergroup.Id));
            }
            else if (TryGetBasicGroup(chat, out BasicGroup basicGroup))
            {
                Send(new GetBasicGroupFullInfo(basicGroup.Id));
            }
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
                else if (chat.Id == _options.VerificationCodesBotChatId)
                {
                    return Strings.VerifyCodesNotifications;
                }
                else if (tiny)
                {
                    return user.FirstName;
                }
            }

            return chat.Title;
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

        public string GetTitle(MessageSender sender, bool firstName = false)
        {
            if (TryGetUser(sender, out User user))
            {
                return user.FullName(firstName);
            }
            else if (TryGetChat(sender, out Chat chat))
            {
                return chat.Title;
            }

            return string.Empty;
        }

        // Allocated once. As a lambda inside GetChatFolders this was a fresh closure on every
        // chat cell that showed a folder tag.
        private Comparison<ChatFolderInfo> _chatFolderComparison;

        private int CompareChatFolders(ChatFolderInfo x, ChatFolderInfo y)
        {
            return _chatFolders.IndexOf(x) - _chatFolders.IndexOf(y);
        }

        public IList<ChatFolderInfo> GetChatFolders(Chat chat)
        {
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
                    if (result.Count > 1)
                    {
                        _chatFolderComparison ??= CompareChatFolders;
                        result.Sort(_chatFolderComparison);
                    }

                    return result;
                }
            }

            return Array.Empty<ChatFolderInfo>();
        }

        public bool TryGetCachedReaction(string emoji, out EmojiReaction value)
        {
            return _cachedReactions.TryGetValue(emoji, out value);
        }

        public Task<IDictionary<string, EmojiReaction>> GetAllReactionsAsync()
        {
            return GetReactionsAsync(_activeReactions);
        }

        public async Task<IDictionary<string, EmojiReaction>> GetReactionsAsync(IEnumerable<string> reactions)
        {
            var result = new Dictionary<string, EmojiReaction>();

            List<string> missing = null;
            List<Task<Object>> tasks = null;

            foreach (var emoji in reactions)
            {
                if (_cachedReactions.TryGetValue(emoji, out EmojiReaction cached))
                {
                    result[emoji] = cached;
                }
                else if (missing == null || !missing.Contains(emoji))
                {
                    missing ??= new List<string>();
                    tasks ??= new List<Task<Object>>();

                    missing.Add(emoji);
                    tasks.Add(SendAsync(new GetEmojiReaction(emoji)));
                }
            }

            if (tasks == null)
            {
                return result;
            }

            // Requested together rather than one after another: this is called with the whole
            // active set, so awaiting each in turn cost one round trip per emoji.
            var responses = await Task.WhenAll(tasks);

            for (int i = 0; i < responses.Length; i++)
            {
                if (responses[i] is EmojiReaction reaction)
                {
                    _cachedReactions[missing[i]] = reaction;
                    result[missing[i]] = reaction;
                }
            }

            return result;
        }

        public async Task<IDictionary<MessageId, MessageProperties>> GetMessagePropertiesAsync(IEnumerable<MessageId> messageIds)
        {
            var map = new Dictionary<MessageId, MessageProperties>();

            List<MessageId> pending = null;
            List<Task<Object>> tasks = null;

            foreach (var messageId in messageIds)
            {
                pending ??= new List<MessageId>();
                tasks ??= new List<Task<Object>>();

                pending.Add(messageId);
                tasks.Add(SendAsync(new GetMessageProperties(messageId.ChatId, messageId.Id)));
            }

            if (tasks == null)
            {
                return map;
            }

            // A selection holds up to a hundred messages and nothing can decide which actions
            // to offer until every one of them has answered, so these go out together. Awaiting
            // them in turn made the menu wait for a hundred sequential round trips.
            var responses = await Task.WhenAll(tasks);

            for (int i = 0; i < responses.Length; i++)
            {
                if (responses[i] is MessageProperties properties)
                {
                    map[pending[i]] = properties;
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

        public IDictionary<MessageSender, ChatAction> GetChatActions(long id, MessageTopic topicId = null)
        {
            if (topicId != null)
            {
                if (_topicActions.TryGetValue(new ChatMessageTopic(id, topicId), out ConcurrentDictionary<MessageSender, ChatAction> value))
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

        public Vector<QuickReplyMessage> GetQuickReplyMessages(int id)
        {
            if (_quickReplyShortcuts.TryGetValue(id, out var value))
            {
                return value.Messages;
            }

            return Array.Empty<QuickReplyMessage>();
        }

        public Vector<QuickReplyShortcut> GetQuickReplyShortcuts()
        {
            if (_quickReplyShortcutIds != null)
            {
                var result = new MutableVector<QuickReplyShortcut>();

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

        // Nothing fills the two fields above until this is sent, and the shortcuts then arrive
        // through updateQuickReplyShortcuts. Callers ask whenever they might display them, which
        // for the composer is once per typed character, so the asking is tracked here.
        public void LoadQuickReplyShortcuts()
        {
            if (_quickReplyShortcutsRequested)
            {
                return;
            }

            _quickReplyShortcutsRequested = true;
            Send(new Td.Api.LoadQuickReplyShortcuts());
        }

        public bool CheckQuickReplyShortcutName(string name)
        {
            if (_quickReplyShortcuts.Values.Any(x => string.Equals(x.Shortcut.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            return ClientEx.CheckQuickReplyShortcutName(name);
        }

        public Vector<WelcomeMessage> GetWelcomeMessages(long chatId)
        {
            if (_welcomeMessages.TryGetValue(chatId, out var value))
            {
                return value;
            }

            return Array.Empty<WelcomeMessage>();
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

        public bool HasActiveUsername(Chat chat, out string username)
        {
            if (TryGetUser(chat, out User user))
            {
                return user.HasActiveUsername(out username);
            }
            else if (TryGetSupergroup(chat, out Supergroup supergroup))
            {
                return supergroup.HasActiveUsername(out username);
            }

            username = null;
            return false;
        }

        public bool HasActiveUsername(MessageSender sender, out string username)
        {
            if (TryGetUser(sender, out User user))
            {
                return user.HasActiveUsername(out username);
            }
            else if (TryGetSupergroup(sender, out Supergroup supergroup))
            {
                return supergroup.HasActiveUsername(out username);
            }

            username = null;
            return false;
        }

        public bool IsForum(Chat chat)
        {
            if (TryGetSupergroup(chat, out Supergroup supergroup))
            {
                return supergroup.IsForum;
            }
            else if (TryGetUser(chat, out User user))
            {
                return user.Type is UserTypeBot { HasTopics: true };
            }

            return false;
        }

        public bool IsDirectMessagesGroup(Chat chat)
        {
            if (TryGetSupergroup(chat, out Supergroup supergroup))
            {
                return supergroup.IsDirectMessagesGroup;
            }

            return false;
        }

        public bool IsAdministeredDirectMessagesGroup(Chat chat)
        {
            if (TryGetSupergroup(chat, out Supergroup supergroup))
            {
                return supergroup.IsAdministeredDirectMessagesGroup;
            }

            return false;
        }

        public bool HasTabs(Chat chat)
        {
            if (TryGetSupergroup(chat, out Supergroup supergroup))
            {
                return supergroup.HasForumTabs || supergroup.IsDirectMessagesGroup;
            }

            return false;
        }

        public bool IsPaid(Chat chat)
        {
            if (TryGetUserFull(chat, out UserFullInfo userFullInfo))
            {
                return userFullInfo.OutgoingPaidMessageStarCount > 0;
            }
            else if (TryGetUser(chat, out User user))
            {
                return user.PaidMessageStarCount > 0;
            }
            else if (TryGetSupergroup(chat, out Supergroup supergroup))
            {
                return supergroup.PaidMessageStarCount > 0;
            }

            return false;
        }

        public long PaidMessageStarCount(Chat chat)
        {
            if (TryGetUserFull(chat, out UserFullInfo userFullInfo))
            {
                return userFullInfo.OutgoingPaidMessageStarCount;
            }
            else if (TryGetUser(chat, out User user))
            {
                return user.PaidMessageStarCount;
            }
            else if (TryGetSupergroup(chat, out Supergroup supergroup))
            {
                return supergroup.PaidMessageStarCount;
            }

            return 0;
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
                return supergroup.CanInviteUsers(chat);
            }
            else if (TryGetBasicGroup(chat, out BasicGroup basicGroup))
            {
                return basicGroup.CanInviteUsers(chat);
            }

            // TODO: secret chats maybe?

            return true;
        }

        public bool CanPromoteMembers(Chat chat)
        {
            if (TryGetSupergroup(chat, out Supergroup supergroup))
            {
                return supergroup.CanPromoteMembers();
            }
            else if (TryGetBasicGroup(chat, out BasicGroup basicGroup))
            {
                return basicGroup.CanPromoteMembers();
            }

            // TODO: secret chats maybe?

            return true;
        }

        public bool CanEditTag(Chat chat, ChatMember member)
        {
            if (TryGetSupergroup(chat, out Supergroup supergroup))
            {
                return supergroup.CanEditTag(chat, member, Options.MyId);
            }
            else if (TryGetBasicGroup(chat, out BasicGroup basicGroup))
            {
                return basicGroup.CanEditTag(chat, member, Options.MyId);
            }

            return false;
        }

        public Object GetMessageSender(MessageSender sender)
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

        public bool TryGetMessageSender(MessageSender sender, out Object value)
        {
            if (sender is MessageSenderUser user && TryGetUser(user.UserId, out User resultUser))
            {
                value = resultUser;
                return true;
            }
            else if (sender is MessageSenderChat chat && TryGetChat(chat.ChatId, out Chat resultChat))
            {
                value = resultChat;
                return true;
            }

            value = null;
            return false;
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

        public bool TryGetChat(AffiliateType type, out Chat value)
        {
            if (type is AffiliateTypeChannel typeChannel)
            {
                return TryGetChat(typeChannel.ChatId, out value);
            }

            value = null;
            return false;
        }

        public bool TryGetChat(SavedMessagesTopicType type, out Chat value)
        {
            if (type is SavedMessagesTopicTypeSavedFromChat fromChat)
            {
                return TryGetChat(fromChat.ChatId, out value);
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

        public async Task<Chat> GetChatFromMessageSenderAsync(MessageSender messageSender)
        {
            TryGetChat(messageSender, out Chat chat);

            if (chat == null && messageSender is MessageSenderUser senderUser)
            {
                TryGetChatFromUser(senderUser.UserId, out chat);

                if (chat == null)
                {
                    var response = await SendAsync(new CreatePrivateChat(senderUser.UserId, false));
                    chat = response as Chat;
                }

                return chat;
            }

            return null;
        }

        public IEnumerable<Chat> GetChats(IEnumerable<long> ids)
        {
            foreach (var id in ids)
            {
                var chat = GetChat(id);
                if (chat != null)
                {
                    UpdateMessageTopicNewChat(chat);
                    yield return chat;
                }
            }
        }

        public IEnumerable<User> GetUsers(IEnumerable<long> ids)
        {
            foreach (var id in ids)
            {
                var user = GetUser(id);
                if (user != null)
                {
                    yield return user;
                }
            }
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
            return _secretChats.Find(x => x.UserId == id);
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

        public bool TryGetUser(AffiliateType type, out User value)
        {
            if (type is AffiliateTypeBot typeBot)
            {
                return TryGetUser(typeBot.UserId, out value);
            }
            else if (type is AffiliateTypeCurrentUser)
            {
                return TryGetUser(Options.MyId, out value);
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

        public ChatPermissions GetPermissions(Chat chat, out bool restrict)
        {
            restrict = false;

            if (TryGetSupergroup(chat, out var supergroup))
            {
                if (supergroup.Status is ChatMemberStatusRestricted restricted)
                {
                    restrict = true;
                    return restricted.Permissions;
                }
                else if (supergroup.Status is ChatMemberStatusCreator or ChatMemberStatusAdministrator)
                {
                    return new ChatPermissions(true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true);
                }
            }
            else if (TryGetBasicGroup(chat, out var basicGroup))
            {
                if (basicGroup.Status is ChatMemberStatusRestricted restricted)
                {
                    restrict = true;
                    return restricted.Permissions;
                }
                else if (basicGroup.Status is ChatMemberStatusCreator or ChatMemberStatusAdministrator)
                {
                    return new ChatPermissions(true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true);
                }
            }

            return chat?.Permissions;
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

        public bool TryGetBasicGroup(MessageSender sender, out BasicGroup value)
        {
            if (sender is MessageSenderChat senderChat && TryGetChat(senderChat.ChatId, out Chat chat))
            {
                return TryGetBasicGroup(chat, out value);
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

        public bool TryGetSupergroup(MessageSender sender, out Supergroup value)
        {
            if (sender is MessageSenderChat senderChat && TryGetChat(senderChat.ChatId, out Chat chat))
            {
                return TryGetSupergroup(chat, out value);
            }

            value = null;
            return false;
        }



        public Community GetCommunity(long id)
        {
            if (_communities.TryGetValue(id, out Community value))
            {
                return value;
            }

            return null;
        }

        public bool TryGetCommunity(long id, out Community value)
        {
            return _communities.TryGetValue(id, out value);
        }

        public CommunityFullInfo GetCommunityFull(long id)
        {
            if (_communitiesFull.TryGetValue(id, out CommunityFullInfo value))
            {
                return value;
            }

            return null;
        }

        public bool TryGetCommunityFull(long id, out CommunityFullInfo value)
        {
            return _communitiesFull.TryGetValue(id, out value);
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



        public GroupCall GetGroupCall(int id)
        {
            if (_groupCalls.TryGetValue(id, out GroupCall value))
            {
                return value;
            }

            return null;
        }

        public bool TryGetGroupCall(int id, out GroupCall value)
        {
            return _groupCalls.TryGetValue(id, out value);
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

        public async Task<BotVerification> GetBotVerificationAsync(Chat chat)
        {
            if (chat.Type is ChatTypePrivate privata)
            {
                if (TryGetUserFull(chat, out UserFullInfo fullInfo))
                {
                    return fullInfo.BotVerification;
                }

                var response = await SendAsync(new GetUserFullInfo(privata.UserId)) as UserFullInfo;
                return response?.BotVerification;
            }
            else if (chat.Type is ChatTypeSupergroup supergroup)
            {
                if (TryGetSupergroupFull(supergroup.SupergroupId, out SupergroupFullInfo fullInfo))
                {
                    return fullInfo.BotVerification;
                }

                var response = await SendAsync(new GetSupergroupFullInfo(supergroup.SupergroupId)) as SupergroupFullInfo;
                return response?.BotVerification;
            }

            return null;
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

        public bool TryGetEmojiChatTheme(ChatTheme theme, out EmojiChatTheme value)
        {
            if (theme is ChatThemeEmoji emoji)
            {
                value = ChatThemes.FirstOrDefault(x => string.Equals(x.Name, emoji.Name));
                return value != null;
            }

            value = null;
            return false;
        }

        public bool TryGetEmojiChatTheme(string themeName, out EmojiChatTheme value)
        {
            value = ChatThemes.FirstOrDefault(x => string.Equals(x.Name, themeName));
            return value != null;
        }

        public Vector<EmojiChatTheme> ChatThemes => _chatThemes?.ChatThemes ?? Array.Empty<EmojiChatTheme>();

        public bool TryGetGroupCallMessageLevel(long paidMessageStarCount, out GroupCallMessageLevel value)
        {
            if (_groupCallMessageLevels != null)
            {
                value = _groupCallMessageLevels.FirstOrDefault(x => x.MinStarCount <= paidMessageStarCount);
                return value != null;
            }

            value = null;
            return false;
        }

        public bool TryGetGroupCallMinimumMessageLevel(int length, int customEmojiCount, out GroupCallMessageLevel value)
        {
            if (_groupCallMessageLevels != null)
            {
                for (int i = _groupCallMessageLevels.Count - 1; i >= 0; i--)
                {
                    var level = _groupCallMessageLevels[i];
                    if (level.MaxTextLength >= length && level.MaxCustomEmojiCount >= customEmojiCount)
                    {
                        value = level;
                        return true;
                    }
                }
            }

            value = null;
            return false;
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

        public bool TryGetMediaAlbum(long chatId, long mediaAlbumId, out MessageAlbumLastMessage album)
        {
            if (_lastMessageAlbums.TryGetValue(chatId, out MessageAlbumLastMessageService service))
            {
                if (service.MediaAlbumId == mediaAlbumId && service.LastMessage != null)
                {
                    album = service.Info();
                    return true;
                }
            }

            album = null;
            return false;
        }

        private void UpdateChatLastMessage(Chat chat, Message lastMessage)
        {
            chat.LastMessage = lastMessage;

            if (lastMessage == null || lastMessage.MediaAlbumId == 0 || lastMessage.Content is not MessagePhoto and not MessageVideo || !AppSettings.Diagnostics.AlbumPreloadDebug)
            {
                _lastMessageAlbums.Remove(chat.Id);
                return;
            }

            if (_lastMessageAlbums.TryGetValue(chat.Id, out MessageAlbumLastMessageService service))
            {
                if (service.MediaAlbumId == lastMessage.MediaAlbumId)
                {
                    service.LoadMore(lastMessage.Id);
                    return;
                }
            }

            _lastMessageAlbums[chat.Id] = new MessageAlbumLastMessageService(this, _aggregator, chat, lastMessage);
        }

        private void UpdateChatLastMessage(UpdateDeleteMessages update)
        {
            if (update.FromCache)
            {
                return;
            }

            if (_lastMessageAlbums.TryGetValue(update.ChatId, out MessageAlbumLastMessageService service))
            {
                service.DeleteMessages(update.MessageIds);
            }
        }

        private void UpdateChatLastMessage(UpdateMessageSendSucceeded update)
        {
            if (_lastMessageAlbums.TryGetValue(update.Message.ChatId, out MessageAlbumLastMessageService service))
            {
                service.MessageSendSucceeded(update.OldMessageId, update.Message);
            }
        }

        private void UpdateChatLastMessage(UpdateMessageSendFailed update)
        {
            if (_lastMessageAlbums.TryGetValue(update.Message.ChatId, out MessageAlbumLastMessageService service))
            {
                service.MessageSendFailed(update.OldMessageId, update.Message);
            }
        }

        /// <summary>
        /// The instance a parsed file has to be read into. Files arrive by the hundred on every
        /// history page, nearly always for an id already held, and the whole point of parsing them
        /// here is that the app keeps handing out the same object.
        /// </summary>
        private File GetOrCreateFile(int id, out bool created)
        {
            if (_files.TryGetValue(id, out File obj))
            {
                created = false;
                return obj;
            }

            created = true;

            return new File
            {
                Id = id,
                Local = new(),
                Remote = new()
            };
        }

        private File CommitFile(File obj, bool created, bool updateFile)
        {
            // Everything below runs inside the parse, on the TDLib thread, and none of it is
            // reading JSON - which is why it is timed separately.
            var started = TdThroughput.BeginHandler();

            // Only the first time this id is seen. Every parsed object carries files — a chat
            // history page is hundreds of them, nearly all already cached — so checking on
            // every update spent a syscall each time to re-answer a question already answered.
            //
            // Nothing is really lost: TDLib sends no update when a file disappears behind its
            // back, so the repeat check only ever caught an external delete by luck, when some
            // unrelated update happened to arrive for that same file. The reliable detection
            // is GetFileAsync catching FileNotFoundException at the point of use.
            //
            // Queued rather than answered here — see VerifyFileExists. The syscall is seven
            // times what this whole update costs to parse, and this is the TDLib thread.
            if (created && obj.Local.IsDownloadingCompleted)
            {
                VerifyFileExists(obj.Id, obj.Local.Path);
            }

            _files[obj.Id] = obj;

            if (updateFile)
            {
                UpdateFile(obj);
            }

            TdThroughput.RecordHandler(started);
            return obj;
        }

#if TD_READER_PARSER
        public UpdateFile ParseUpdateFile(ref System.Text.Json.Utf8JsonReader reader)
        {
            ParseFile(ref reader, true);
            return null;
        }

        public File ParseFile(ref System.Text.Json.Utf8JsonReader reader)
        {
            return ParseFile(ref reader, false);
        }

        private File ParseFile(ref System.Text.Json.Utf8JsonReader reader, bool updateFile)
        {
            if (updateFile)
            {
                reader.ReadStartObject();
                reader.Read();
            }

            reader.ReadStartObject();
            reader.Read();

            var obj = GetOrCreateFile(reader.GetInt32(), out bool created);

            //if (!created && !updateFile)
            //{
            //    var depth = reader.CurrentDepth;

            //    do
            //    {
            //        reader.Read();
            //    }
            //    while (depth <= reader.CurrentDepth);
            //    return obj;
            //}

            reader.Read();
            while (reader.TokenType == System.Text.Json.JsonTokenType.PropertyName)
            {
                var hash = ClientJson.ComputeCrc32(reader.ValueSpan);

                reader.Read();
                Handler(ref reader, this, obj, hash);
                reader.Read();
            }

            return CommitFile(obj, created, updateFile);

            static bool Handler(ref System.Text.Json.Utf8JsonReader reader, ClientResultHandler handler, File obj, uint hash)
            {
                switch (hash)
                {
                    case 3208210256:
                        obj.Id = reader.GetInt32();
                        return true;
                    case 4156564586:
                        obj.Size = reader.GetInt64();
                        return true;
                    case 2631592555:
                        obj.ExpectedSize = reader.GetInt64();
                        return true;
                    case 2346092776:
                        obj.Local = FromJson_LocalFile(ref reader, obj, handler);
                        return true;
                    case 1521909682:
                        obj.Remote = FromJson_RemoteFile(ref reader, obj, handler);
                        return true;
                    default: return false;
                }
            }
        }

        public static LocalFile FromJson_LocalFile(ref System.Text.Json.Utf8JsonReader reader, File file, ClientResultHandler handler)
        {
            return ClientJson.ParseObject(ref reader, file.Local, handler, Handler);

            static bool Handler(ref System.Text.Json.Utf8JsonReader reader, ClientResultHandler handler, LocalFile obj, uint hash)
            {
                switch (hash)
                {
                    case 190089999:
                        obj.Path = reader.GetString();
                        return true;
                    case 1241267705:
                        obj.CanBeDownloaded = reader.GetBoolean();
                        return true;
                    case 3790612123:
                        obj.CanBeDeleted = reader.GetBoolean();
                        return true;
                    case 2701185344:
                        obj.IsDownloadingActive = reader.GetBoolean();
                        return true;
                    case 2479055526:
                        obj.IsDownloadingCompleted = reader.GetBoolean();
                        return true;
                    case 2616348667:
                        obj.DownloadOffset = reader.GetInt64();
                        return true;
                    case 1216427891:
                        obj.DownloadedPrefixSize = reader.GetInt64();
                        return true;
                    case 2156605620:
                        obj.DownloadedSize = reader.GetInt64();
                        return true;
                    default: return false;
                }
            }
        }

        public static RemoteFile FromJson_RemoteFile(ref System.Text.Json.Utf8JsonReader reader, File file, ClientResultHandler handler)
        {
            return ClientJson.ParseObject(ref reader, file.Remote, handler, Handler);

            static bool Handler(ref System.Text.Json.Utf8JsonReader reader, ClientResultHandler handler, RemoteFile obj, uint hash)
            {
                switch (hash)
                {
                    case 3208210256:
                        obj.Id = reader.GetString();
                        return true;
                    case 3821437763:
                        obj.UniqueId = reader.GetString();
                        return true;
                    case 4088541240:
                        obj.IsUploadingActive = reader.GetBoolean();
                        return true;
                    case 2871741201:
                        obj.IsUploadingCompleted = reader.GetBoolean();
                        return true;
                    case 3478316327:
                        obj.UploadedSize = reader.GetInt64();
                        return true;
                    default: return false;
                }
            }
        }
#endif

#if TD_POINTER_PARSER
        public UpdateFile ParseUpdateFile(ref TdJsonReader reader)
        {
            ParseFile(ref reader, true);
            return null;
        }

        public File ParseFile(ref TdJsonReader reader)
        {
            return ParseFile(ref reader, false);
        }

        /// <summary>
        /// The pointer reader's half of the same thing. Fields dispatch on name length and an exact
        /// compare, matching what SchemaGenerator emits for every other type on this path — see
        /// Telegram.Benchmarks/README.md for why the hash is not worth its collision risk here.
        /// </summary>
        private File ParseFile(ref TdJsonReader reader, bool updateFile)
        {
            if (updateFile)
            {
                ClientJson.ReadStartObjectPtr(ref reader);
                reader.Read();
            }

            ClientJson.ReadStartObjectPtr(ref reader);
            reader.Read();

            // id is first because TDLib writes fields in scheme order, which is what makes the
            // lookup possible before the rest of the object has been read.
            var obj = GetOrCreateFile(reader.GetInt32(), out bool created);

            reader.Read();
            while (reader.TokenType == System.Text.Json.JsonTokenType.PropertyName)
            {
                var name = reader.ValueSpan;
                reader.Read();

                switch (name.Length)
                {
                    case 2:
                        if (name.SequenceEqual("id"u8)) obj.Id = reader.GetInt32();
                        break;
                    case 4:
                        if (name.SequenceEqual("size"u8)) obj.Size = reader.GetInt64();
                        break;
                    case 5:
                        if (name.SequenceEqual("local"u8)) FromPtr_LocalFile(ref reader, obj);
                        break;
                    case 6:
                        if (name.SequenceEqual("remote"u8)) FromPtr_RemoteFile(ref reader, obj);
                        break;
                    case 13:
                        if (name.SequenceEqual("expected_size"u8)) obj.ExpectedSize = reader.GetInt64();
                        break;
                }

                if (reader.TokenType == System.Text.Json.JsonTokenType.StartObject || reader.TokenType == System.Text.Json.JsonTokenType.StartArray)
                {
                    reader.Skip();
                }

                reader.Read();
            }

            return CommitFile(obj, created, updateFile);
        }

        /// <summary>
        /// Reads into the file's existing LocalFile rather than returning a new one: the whole
        /// point of routing files through here is that the instance the app holds is the instance
        /// that gets updated.
        /// </summary>
        private static void FromPtr_LocalFile(ref TdJsonReader reader, File file)
        {
            var obj = file.Local;

            ClientJson.ReadStartObjectPtr(ref reader);

            while (reader.TokenType == System.Text.Json.JsonTokenType.PropertyName)
            {
                var name = reader.ValueSpan;
                reader.Read();

                switch (name.Length)
                {
                    case 4:
                        if (name.SequenceEqual("path"u8)) obj.Path = reader.GetString();
                        break;
                    case 14:
                        if (name.SequenceEqual("can_be_deleted"u8)) obj.CanBeDeleted = reader.GetBoolean();
                        break;
                    case 15:
                        if (name.SequenceEqual("download_offset"u8)) obj.DownloadOffset = reader.GetInt64();
                        else if (name.SequenceEqual("downloaded_size"u8)) obj.DownloadedSize = reader.GetInt64();
                        break;
                    case 17:
                        if (name.SequenceEqual("can_be_downloaded"u8)) obj.CanBeDownloaded = reader.GetBoolean();
                        break;
                    case 21:
                        if (name.SequenceEqual("is_downloading_active"u8)) obj.IsDownloadingActive = reader.GetBoolean();
                        break;
                    case 22:
                        if (name.SequenceEqual("downloaded_prefix_size"u8)) obj.DownloadedPrefixSize = reader.GetInt64();
                        break;
                    case 24:
                        if (name.SequenceEqual("is_downloading_completed"u8)) obj.IsDownloadingCompleted = reader.GetBoolean();
                        break;
                }

                if (reader.TokenType == System.Text.Json.JsonTokenType.StartObject || reader.TokenType == System.Text.Json.JsonTokenType.StartArray)
                {
                    reader.Skip();
                }

                reader.Read();
            }
        }

        private static void FromPtr_RemoteFile(ref TdJsonReader reader, File file)
        {
            var obj = file.Remote;

            ClientJson.ReadStartObjectPtr(ref reader);

            while (reader.TokenType == System.Text.Json.JsonTokenType.PropertyName)
            {
                var name = reader.ValueSpan;
                reader.Read();

                switch (name.Length)
                {
                    case 2:
                        if (name.SequenceEqual("id"u8)) obj.Id = reader.GetString();
                        break;
                    case 9:
                        if (name.SequenceEqual("unique_id"u8)) obj.UniqueId = reader.GetString();
                        break;
                    case 13:
                        if (name.SequenceEqual("uploaded_size"u8)) obj.UploadedSize = reader.GetInt64();
                        break;
                    case 19:
                        if (name.SequenceEqual("is_uploading_active"u8)) obj.IsUploadingActive = reader.GetBoolean();
                        break;
                    case 22:
                        if (name.SequenceEqual("is_uploading_completed"u8)) obj.IsUploadingCompleted = reader.GetBoolean();
                        break;
                }

                if (reader.TokenType == System.Text.Json.JsonTokenType.StartObject || reader.TokenType == System.Text.Json.JsonTokenType.StartArray)
                {
                    reader.Skip();
                }

                reader.Read();
            }
        }
#endif

        private void UpdateFile(File file)
        {
            var restoreVerbosity = int.MinValue;

            lock (_preparedLogsLock)
            {
                if (_preparedLogsFileIds != null && _preparedLogsFileIds.Contains(file.Id) && file.Remote.UploadedSize > 0)
                {
                    _preparedLogsFileIds.Remove(file.Id);

                    if (_preparedLogsFileIds.Empty())
                    {
                        restoreVerbosity = _preparedLogsVerbosity;

                        _preparedLogsFileIds = null;
                        _preparedLogsVerbosity = -1;
                    }
                }
            }

            if (restoreVerbosity != int.MinValue)
            {
                Client.Execute(new SetLogVerbosityLevel(restoreVerbosity));
            }

            // TODO: move the message after track when figured out why WeakAction throws a NRE
            UpdateManager.Publish(SessionId, file);
            TrackDownloadedFile(file);
        }

        public void OnResult(Object update)
        {
            switch (update)
            {
                case UpdateChatPosition updateChatPosition:
                    {
                        if (_chats.TryGetValue(updateChatPosition.ChatId, out Chat value))
                        {
                            lock (value)
                            {
                                int i;
                                for (i = 0; i < value.Positions.Count; i++)
                                {
                                    if (ChatListEqualityComparer.Instance.Equals(value.Positions[i].List, updateChatPosition.Position.List))
                                    {
                                        break;
                                    }
                                }

                                var newPositions = new MutableVector<ChatPosition>(value.Positions.Count + (updateChatPosition.Position.Order == 0 ? 0 : 1) - (i < value.Positions.Count ? 1 : 0));
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
                            }
                        }

                        break;
                    }

                case UpdateChatLastMessage updateChatLastMessage:
                    {
                        if (_chats.TryGetValue(updateChatLastMessage.ChatId, out Chat value))
                        {
                            lock (value)
                            {
                                UpdateChatLastMessage(value, updateChatLastMessage.LastMessage);
                                SetChatPositions(value, updateChatLastMessage.Positions);
                            }
                        }

                        UpdateForumTopic(updateChatLastMessage.ChatId, false, manager => manager.UpdateChatLastMessage(updateChatLastMessage.LastMessage));
                        break;
                    }

                case UpdateUser updateUser:
                    {
                        _users.TryGetValue(updateUser.User.Id, out User value);
                        _users[updateUser.User.Id] = updateUser.User;

                        if (value != null && value.IsContact != updateUser.User.IsContact)
                        {
                            _aggregator.Publish(new UpdateUserIsContact(updateUser.User.Id));
                        }

                        break;
                    }

                case UpdateUnreadMessageCount updateUnreadMessageCount:
                    SetUnreadCount(updateUnreadMessageCount.ChatList, messageCount: updateUnreadMessageCount);
                    break;
                case UpdateNewChat updateNewChat:
                    {
                        _chats[updateNewChat.Chat.Id] = updateNewChat.Chat;

                        lock (updateNewChat.Chat)
                        {
                            UpdateChatLastMessage(updateNewChat.Chat, updateNewChat.Chat.LastMessage);
                            SetChatPositions(updateNewChat.Chat, updateNewChat.Chat.Positions);
                        }

                        if (updateNewChat.Chat.Type is ChatTypePrivate privata)
                        {
                            _usersToChats[privata.UserId] = updateNewChat.Chat.Id;
                        }

                        break;
                    }

                case UpdateSavedMessagesTopic updateSavedMessagesTopic:
                    {
                        if (_savedMessagesTopics.TryGetValue(updateSavedMessagesTopic.Topic.Id, out SavedMessagesTopic topic))
                        {
                            lock (topic)
                            {
                                SetSavedMessagesTopicOrder(topic, updateSavedMessagesTopic.Topic.Order);
                            }

                            topic.DraftMessage = updateSavedMessagesTopic.Topic.DraftMessage;
                            topic.LastMessage = updateSavedMessagesTopic.Topic.LastMessage;
                            topic.IsPinned = updateSavedMessagesTopic.Topic.IsPinned;
                            topic.Order = updateSavedMessagesTopic.Topic.Order;

                            updateSavedMessagesTopic.Topic = topic;
                        }
                        else
                        {
                            lock (updateSavedMessagesTopic.Topic)
                            {
                                SetSavedMessagesTopicOrder(updateSavedMessagesTopic.Topic, updateSavedMessagesTopic.Topic.Order);
                            }

                            _savedMessagesTopics[updateSavedMessagesTopic.Topic.Id] = updateSavedMessagesTopic.Topic;
                        }

                        break;
                    }

                case UpdateChatAddedToList updateChatAddedToList:
                    {
                        if (_chats.TryGetValue(updateChatAddedToList.ChatId, out Chat value))
                        {
                            lock (value)
                            {
                                // Rebuilt rather than appended to, like UpdateChatPosition above: a
                                // reader outside the lock keeps the list it already has, and the
                                // parser is free to hand out a shared empty for a chat in no list.
                                var lists = new MutableVector<ChatList>(value.ChatLists.Count + 1);

                                for (int i = 0; i < value.ChatLists.Count; i++)
                                {
                                    lists.Add(value.ChatLists[i]);
                                }

                                lists.Add(updateChatAddedToList.ChatList);
                                value.ChatLists = lists;
                            }
                        }

                        break;
                    }

                case UpdateChatRemovedFromList updateChatRemovedFromList:
                    {
                        if (_chats.TryGetValue(updateChatRemovedFromList.ChatId, out Chat value))
                        {
                            lock (value)
                            {
                                int i;
                                for (i = 0; i < value.ChatLists.Count; i++)
                                {
                                    if (value.ChatLists[i].AreTheSame(updateChatRemovedFromList.ChatList))
                                    {
                                        break;
                                    }
                                }

                                // Only the first match, as before. Absent, and the list is left alone
                                // rather than needlessly copied.
                                if (i < value.ChatLists.Count)
                                {
                                    var lists = new MutableVector<ChatList>(value.ChatLists.Count - 1);

                                    for (int j = 0; j < value.ChatLists.Count; j++)
                                    {
                                        if (j != i)
                                        {
                                            lists.Add(value.ChatLists[j]);
                                        }
                                    }

                                    value.ChatLists = lists;
                                }
                            }
                        }

                        break;
                    }

                case UpdateAuthorizationState updateAuthorizationState:
                    switch (updateAuthorizationState.AuthorizationState)
                    {
                        case AuthorizationStateLoggingOut:
                            _settings.Clear();
                            break;
                        case AuthorizationStateClosed:
                            Clear();
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
                    break;
                case UpdateChatActiveStories updateActiveStories:
                    {
                        _activeStories.TryGetValue(updateActiveStories.ActiveStories.ChatId, out ChatActiveStories value);
                        _activeStories[updateActiveStories.ActiveStories.ChatId] = updateActiveStories.ActiveStories;

                        lock (updateActiveStories.ActiveStories)
                        {
                            SetActiveStoriesPositions(updateActiveStories.ActiveStories, value);
                        }

                        break;
                    }

                case UpdateAnimationSearchParameters updateAnimationSearchParameters:
                    _animationSearchParameters = updateAnimationSearchParameters;
                    break;
                case UpdateBasicGroup updateBasicGroup:
                    _basicGroups[updateBasicGroup.BasicGroup.Id] = updateBasicGroup.BasicGroup;
                    break;
                case UpdateBasicGroupFullInfo updateBasicGroupFullInfo:
                    _basicGroupsFull[updateBasicGroupFullInfo.BasicGroupId] = updateBasicGroupFullInfo.BasicGroupFullInfo;
                    break;
                case UpdateChatAction updateUserChatAction:
                    {
                        if (updateUserChatAction.TopicId != null)
                        {
                            var threadActions = _topicActions.GetOrAdd(new ChatMessageTopic(updateUserChatAction.ChatId, updateUserChatAction.TopicId), x => new ConcurrentDictionary<MessageSender, ChatAction>(new MessageSenderEqualityComparer()));
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

                        break;
                    }

                case UpdateChatActionBar updateChatActionBar:
                    {
                        if (_chats.TryGetValue(updateChatActionBar.ChatId, out Chat value))
                        {
                            value.ActionBar = updateChatActionBar.ActionBar;
                        }

                        break;
                    }

                case UpdateChatAvailableReactions chatAvailableReactions:
                    {
                        if (_chats.TryGetValue(chatAvailableReactions.ChatId, out Chat value))
                        {
                            value.AvailableReactions = chatAvailableReactions.AvailableReactions;
                        }

                        break;
                    }

                case UpdateChatBackground chatBackground:
                    {
                        if (_chats.TryGetValue(chatBackground.ChatId, out Chat value))
                        {
                            value.Background = chatBackground.Background;
                        }

                        break;
                    }

                case UpdateChatHasProtectedContent updateChatHasProtectedContent:
                    {
                        if (_chats.TryGetValue(updateChatHasProtectedContent.ChatId, out Chat value))
                        {
                            value.HasProtectedContent = updateChatHasProtectedContent.HasProtectedContent;
                        }

                        break;
                    }

                case UpdateChatDefaultDisableNotification updateChatDefaultDisableNotification:
                    {
                        if (_chats.TryGetValue(updateChatDefaultDisableNotification.ChatId, out Chat value))
                        {
                            value.DefaultDisableNotification = updateChatDefaultDisableNotification.DefaultDisableNotification;
                        }

                        break;
                    }

                case UpdateChatEmojiStatus updateChatEmojiStatus:
                    {
                        if (_chats.TryGetValue(updateChatEmojiStatus.ChatId, out Chat value))
                        {
                            value.EmojiStatus = updateChatEmojiStatus.EmojiStatus;
                        }

                        break;
                    }

                case UpdateChatMessageSender updateChatMessageSender:
                    {
                        if (_chats.TryGetValue(updateChatMessageSender.ChatId, out Chat value))
                        {
                            value.MessageSenderId = updateChatMessageSender.MessageSenderId;
                        }

                        break;
                    }

                case UpdateChatDraftMessage updateChatDraftMessage:
                    {
                        if (_chats.TryGetValue(updateChatDraftMessage.ChatId, out Chat value))
                        {
                            lock (value)
                            {
                                value.DraftMessage = updateChatDraftMessage.DraftMessage;
                                SetChatPositions(value, updateChatDraftMessage.Positions);
                            }
                        }
                        break;
                    }

                case UpdateChatFolders updateChatFolders:
                    {
                        lock (_chatFoldersLock)
                        {
                            _chatFolders = updateChatFolders.ChatFolders.ToVector();
                            _chatFolders2 = updateChatFolders.ChatFolders.ToDictionary(x => x.Id);
                        }

                        _mainChatListPosition = updateChatFolders.MainChatListPosition;
                        _areTagsEnabled = updateChatFolders.AreTagsEnabled;
                        break;
                    }

                case UpdateChatHasScheduledMessages updateChatHasScheduledMessages:
                    {
                        if (_chats.TryGetValue(updateChatHasScheduledMessages.ChatId, out Chat value))
                        {
                            value.HasScheduledMessages = updateChatHasScheduledMessages.HasScheduledMessages;
                        }

                        break;
                    }

                case UpdateChatAccentColors updateChatAccentColors:
                    {
                        if (_chats.TryGetValue(updateChatAccentColors.ChatId, out Chat value))
                        {
                            value.AccentColorId = updateChatAccentColors.AccentColorId;
                            value.BackgroundCustomEmojiId = updateChatAccentColors.BackgroundCustomEmojiId;
                            value.ProfileAccentColorId = updateChatAccentColors.ProfileAccentColorId;
                            value.ProfileBackgroundCustomEmojiId = updateChatAccentColors.ProfileBackgroundCustomEmojiId;
                            value.UpgradedGiftColors = updateChatAccentColors.UpgradedGiftColors;
                        }

                        break;
                    }

                case UpdateChatBlockList updateChatBlockList:
                    {
                        if (_chats.TryGetValue(updateChatBlockList.ChatId, out Chat value))
                        {
                            value.BlockList = updateChatBlockList.BlockList;
                        }

                        break;
                    }

                case UpdateChatIsMarkedAsUnread updateChatIsMarkedAsUnread:
                    {
                        if (_chats.TryGetValue(updateChatIsMarkedAsUnread.ChatId, out Chat value))
                        {
                            value.IsMarkedAsUnread = updateChatIsMarkedAsUnread.IsMarkedAsUnread;
                        }

                        break;
                    }

                case UpdateChatIsTranslatable updateChatIsTranslatable:
                    {
                        if (_chats.TryGetValue(updateChatIsTranslatable.ChatId, out Chat value))
                        {
                            value.IsTranslatable = updateChatIsTranslatable.IsTranslatable;
                        }

                        break;
                    }

                case UpdateChatNotificationSettings updateNotificationSettings:
                    {
                        if (_chats.TryGetValue(updateNotificationSettings.ChatId, out Chat value))
                        {
                            value.NotificationSettings = updateNotificationSettings.NotificationSettings;
                        }

                        break;
                    }

                case UpdateChatPendingJoinRequests updateChatPendingJoinRequests:
                    {
                        if (_chats.TryGetValue(updateChatPendingJoinRequests.ChatId, out Chat value))
                        {
                            value.PendingJoinRequests = updateChatPendingJoinRequests.PendingJoinRequests;
                        }

                        break;
                    }

                case UpdateChatPermissions updateChatPermissions:
                    {
                        if (_chats.TryGetValue(updateChatPermissions.ChatId, out Chat value))
                        {
                            value.Permissions = updateChatPermissions.Permissions;
                        }

                        break;
                    }

                case UpdateChatPhoto updateChatPhoto:
                    {
                        if (_chats.TryGetValue(updateChatPhoto.ChatId, out Chat value))
                        {
                            value.Photo = updateChatPhoto.Photo;
                        }

                        break;
                    }

                case UpdateChatReadInbox updateChatReadInbox:
                    {
                        if (_chats.TryGetValue(updateChatReadInbox.ChatId, out Chat value))
                        {
                            value.UnreadCount = updateChatReadInbox.UnreadCount;
                            value.LastReadInboxMessageId = updateChatReadInbox.LastReadInboxMessageId;
                        }

                        break;
                    }

                case UpdateChatReadOutbox updateChatReadOutbox:
                    {
                        if (_chats.TryGetValue(updateChatReadOutbox.ChatId, out Chat value))
                        {
                            value.LastReadOutboxMessageId = updateChatReadOutbox.LastReadOutboxMessageId;
                        }

                        break;
                    }

                case UpdateChatReplyMarkup updateChatReplyMarkup:
                    {
                        if (_chats.TryGetValue(updateChatReplyMarkup.ChatId, out Chat value))
                        {
                            value.ReplyMarkupMessageId = updateChatReplyMarkup.ReplyMarkupMessage?.Id ?? 0;
                        }

                        break;
                    }

                case UpdateChatTheme updateChatTheme:
                    {
                        if (_chats.TryGetValue(updateChatTheme.ChatId, out Chat value))
                        {
                            value.Theme = updateChatTheme.Theme;
                        }

                        break;
                    }

                case UpdateEmojiChatThemes updateChatThemes:
                    _chatThemes = updateChatThemes;
                    break;
                case UpdateChatTitle updateChatTitle:
                    {
                        if (_chats.TryGetValue(updateChatTitle.ChatId, out Chat value))
                        {
                            value.Title = updateChatTitle.Title;
                        }

                        break;
                    }

                case UpdateChatMessageAutoDeleteTime updateChatMessageAutoDeleteTime:
                    {
                        if (_chats.TryGetValue(updateChatMessageAutoDeleteTime.ChatId, out Chat value))
                        {
                            value.MessageAutoDeleteTime = updateChatMessageAutoDeleteTime.MessageAutoDeleteTime;
                        }

                        break;
                    }

                case UpdateChatUnreadMentionCount updateChatUnreadMentionCount:
                    {
                        if (_chats.TryGetValue(updateChatUnreadMentionCount.ChatId, out Chat value))
                        {
                            value.UnreadMentionCount = updateChatUnreadMentionCount.UnreadMentionCount;
                        }

                        break;
                    }

                case UpdateChatUnreadReactionCount updateChatUnreadReactionCount:
                    {
                        if (_chats.TryGetValue(updateChatUnreadReactionCount.ChatId, out Chat value))
                        {
                            value.UnreadReactionCount = updateChatUnreadReactionCount.UnreadReactionCount;
                        }

                        break;
                    }

                case UpdateChatUnreadPollVoteCount updateChatUnreadPollVoteCount:
                    {
                        if (_chats.TryGetValue(updateChatUnreadPollVoteCount.ChatId, out Chat value))
                        {
                            value.UnreadPollVoteCount = updateChatUnreadPollVoteCount.UnreadPollVoteCount;
                        }

                        break;
                    }

                case UpdateChatVideoChat updateChatVideoChat:
                    {
                        if (_chats.TryGetValue(updateChatVideoChat.ChatId, out Chat value))
                        {
                            value.VideoChat = updateChatVideoChat.VideoChat;
                        }

                        break;
                    }

                case UpdateChatViewAsTopics updateChatViewAsTopics:
                    {
                        if (_chats.TryGetValue(updateChatViewAsTopics.ChatId, out Chat value))
                        {
                            value.ViewAsTopics = updateChatViewAsTopics.ViewAsTopics;
                        }

                        break;
                    }

                case UpdateChatBusinessBotManageBar updateChatBusinessBotManageBar:
                    {
                        if (_chats.TryGetValue(updateChatBusinessBotManageBar.ChatId, out Chat value))
                        {
                            value.BusinessBotManageBar = updateChatBusinessBotManageBar.BusinessBotManageBar;
                        }

                        break;
                    }

                case UpdateChatHasWelcomeMessages updateChatHasWelcomeMessages:
                    {
                        if (_chats.TryGetValue(updateChatHasWelcomeMessages.ChatId, out Chat value))
                        {
                            value.HasWelcomeMessages = updateChatHasWelcomeMessages.HasWelcomeMessages;
                        }

                        break;
                    }

                case UpdateChatWelcomeMessages updateChatWelcomeMessages:
                    {
                        if (_chats.TryGetValue(updateChatWelcomeMessages.ChatId, out Chat value))
                        {
                            _welcomeMessages[updateChatWelcomeMessages.ChatId] = updateChatWelcomeMessages.Messages;
                        }

                        break;
                    }

                case UpdateConnectionState updateConnectionState:
                    _connectionState = updateConnectionState.State;
                    break;
                case UpdateDefaultReactionType updateDefaultReactionType:
                    _defaultReaction = updateDefaultReactionType.ReactionType;
                    break;
                case UpdateDiceEmojis updateDiceEmojis:
                    _diceEmojis = updateDiceEmojis.Emojis.ToArray();
                    break;
                case UpdateFavoriteStickers updateFavoriteStickers:
                    _favoriteStickers = updateFavoriteStickers.StickerIds;
                    break;
                case UpdateForumTopic updateForumTopic:
                    UpdateForumTopic(updateForumTopic.ChatId, true, manager => manager.UpdateForumTopic(updateForumTopic));
                    break;
                case UpdateForumTopicInfo updateForumTopicInfo:
                    UpdateForumTopic(updateForumTopicInfo.Info.ChatId, true, manager => manager.UpdateForumTopicInfo(updateForumTopicInfo.Info));
                    break;
                case UpdateDirectMessagesChatTopic updateDirectMessagesChatTopic:
                    UpdateDirectMessagesChatTopic(updateDirectMessagesChatTopic.Topic.ChatId, manager => manager.UpdateDirectMessagesChatTopic(updateDirectMessagesChatTopic.Topic));
                    break;
                case UpdateCommunity updateCommunity:
                    _communities[updateCommunity.Community.Id] = updateCommunity.Community;
                    break;
                case UpdateCommunityFullInfo updateCommunityFullInfo:
                    _communitiesFull[updateCommunityFullInfo.CommunityId] = updateCommunityFullInfo.CommunityFullInfo;
                    break;
                case UpdateGroupCall updateGroupCall:
                    _groupCalls[updateGroupCall.GroupCall.Id] = updateGroupCall.GroupCall;
                    break;
                case UpdateGroupCallMessageLevels updateGroupCallMessageLevels:
                    _groupCallMessageLevels = updateGroupCallMessageLevels.Levels.ToArray();
                    break;
                case UpdateInstalledStickerSets updateInstalledStickerSets:
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
                    break;
                case UpdateLanguagePackStrings updateLanguagePackStrings:
                    _locale.Handle(updateLanguagePackStrings);
                    break;
                case UpdateMessageIsPinned updateMessageIsPinned:
                    _settings.SetChatPinnedMessage(updateMessageIsPinned.ChatId, 0);
                    UpdateForumTopic(updateMessageIsPinned.ChatId, false, manager => manager.UpdateMessageIsPinned(updateMessageIsPinned.MessageId, updateMessageIsPinned.IsPinned));
                    break;
                case UpdateMessageMentionRead updateMessageMentionRead:
                    {
                        if (_chats.TryGetValue(updateMessageMentionRead.ChatId, out Chat value))
                        {
                            value.UnreadMentionCount = updateMessageMentionRead.UnreadMentionCount;
                        }

                        UpdateForumTopic(updateMessageMentionRead.ChatId, false, manager => manager.UpdateMessageMentionRead(updateMessageMentionRead.MessageId, updateMessageMentionRead.UnreadMentionCount));
                        break;
                    }

                case UpdateMessageUnreadReactions updateMessageUnreadReactions:
                    {
                        if (_chats.TryGetValue(updateMessageUnreadReactions.ChatId, out Chat value))
                        {
                            value.UnreadReactionCount = updateMessageUnreadReactions.UnreadReactionCount;
                        }

                        UpdateForumTopic(updateMessageUnreadReactions.ChatId, false, manager => manager.UpdateMessageUnreadReactions(updateMessageUnreadReactions.MessageId, updateMessageUnreadReactions.UnreadReactions, updateMessageUnreadReactions.UnreadReactionCount));
                        break;
                    }

                case UpdateOption updateOption:
                    {
                        _options.Update(updateOption.Name, updateOption.Value);

                        if (updateOption.Name == OptionsService.R.UnixTime && updateOption.Value is OptionValueInteger unixTime)
                        {
                            _localTicksAtSync = Stopwatch.GetTimestamp();
                            _serverTimeAtSync = unixTime.Value;
                        }
                        else if (updateOption.Name == OptionsService.R.MyId && updateOption.Value is OptionValueInteger myId)
                        {
                            _settings.UserId = myId.Value;
                        }
                        else if (updateOption.Name == OptionsService.R.IsPremium || updateOption.Name == OptionsService.R.IsPremiumAvailable)
                        {
                            _aggregator.Publish(new UpdatePremiumState(IsPremium, IsPremiumAvailable));
                        }

                        break;
                    }

                case UpdateActiveEmojiReactions updateReactions:
                    _activeReactions = updateReactions.Emojis;
                    break;
                case UpdateRecentStickers updateRecentStickers:
                    if (updateRecentStickers.IsAttached)
                    {

                    }
                    else
                    {
                        _recentStickers = updateRecentStickers.StickerIds;
                    }
                    break;
                case UpdateSavedAnimations updateSavedAnimations:
                    _savedAnimations = updateSavedAnimations.AnimationIds;
                    break;
                case UpdateTextCompositionStyles updateTextCompositionStyles:
                    _textCompositionStyles = updateTextCompositionStyles.Styles;
                    break;
                case UpdateScopeNotificationSettings updateScopeNotificationSettings:
                    _settings.Notifications.Scope[updateScopeNotificationSettings.Scope.GetType()] = updateScopeNotificationSettings.NotificationSettings;
                    break;
                case UpdateSecretChat updateSecretChat:
                    _secretChats[updateSecretChat.SecretChat.Id] = updateSecretChat.SecretChat;
                    break;
                case UpdateDefaultBackground updateDefaultBackground:
                    if (updateDefaultBackground.ForDarkTheme)
                    {
                        _selectedBackgroundDark = updateDefaultBackground.Background;
                    }
                    else
                    {
                        _selectedBackground = updateDefaultBackground.Background;
                    }
                    break;
                case UpdateSpeechRecognitionTrial updateSpeechRecognitionTrial:
                    _speechRecognitionTrial = updateSpeechRecognitionTrial;
                    break;
                case UpdateStoryStealthMode updateStoryStealthMode:
                    _storyStealthMode = updateStoryStealthMode;
                    break;
                case UpdateSupergroup updateSupergroup:
                    _supergroups[updateSupergroup.Supergroup.Id] = updateSupergroup.Supergroup;
                    break;
                case UpdateSupergroupFullInfo updateSupergroupFullInfo:
                    _supergroupsFull[updateSupergroupFullInfo.SupergroupId] = updateSupergroupFullInfo.SupergroupFullInfo;
                    break;
                case UpdateUnreadChatCount updateUnreadChatCount:
                    SetUnreadCount(updateUnreadChatCount.ChatList, chatCount: updateUnreadChatCount);
                    break;
                case UpdateUserFullInfo updateUserFullInfo:
                    _usersFull[updateUserFullInfo.UserId] = updateUserFullInfo.UserFullInfo;
                    break;
                case UpdateUserStatus updateUserStatus:
                    {
                        if (_users.TryGetValue(updateUserStatus.UserId, out User value))
                        {
                            value.Status = updateUserStatus.Status;
                        }

                        break;
                    }

                case UpdateUnconfirmedSession updateUnconfirmedSession:
                    _unconfirmedSession = updateUnconfirmedSession.Session;
                    break;
                case UpdateAttachmentMenuBots updateAttachmentMenuBots:
                    _attachmentMenuBots = updateAttachmentMenuBots.Bots;
                    break;
                case UpdateAccentColors updateAccentColors:
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
                        break;
                    }

                case UpdateProfileAccentColors updateProfileAccentColors:
                    {
                        var colors = new Dictionary<int, ProfileColor>();

                        foreach (var color in updateProfileAccentColors.Colors)
                        {
                            colors[color.Id] = new ProfileColor(color);
                        }

                        AvailableProfileColors = updateProfileAccentColors.AvailableAccentColorIds.ToList();
                        ProfileColors = colors;
                        break;
                    }

                case UpdateSavedMessagesTags updateSavedMessagesTags:
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

                        break;
                    }

                case UpdateSuggestedActions updateSuggestedActions:
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

                        break;
                    }

                case UpdateQuickReplyShortcut updateQuickReplyShortcut:
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

                        break;
                    }

                case UpdateQuickReplyShortcutDeleted updateQuickReplyShortcutDeleted:
                    _quickReplyShortcuts.Remove(updateQuickReplyShortcutDeleted.ShortcutId);
                    break;
                case UpdateQuickReplyShortcutMessages updateQuickReplyShortcutMessages:
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

                        break;
                    }

                case UpdateQuickReplyShortcuts updateQuickReplyShortcuts:
                    _quickReplyShortcutIds = updateQuickReplyShortcuts.ShortcutIds.ToList();
                    break;
                case UpdateContactCloseBirthdays updateContactCloseBirthdays:
                    _contactCloseBirthdays = updateContactCloseBirthdays;
                    break;
                case UpdateAvailableMessageEffects updateAvailableMessageEffects:
                    _availableMessageEffects = updateAvailableMessageEffects;
                    break;
                case UpdateOwnedStarCount updateOwnedStarCount:
                    _ownedStarCount = updateOwnedStarCount.StarAmount;
                    break;
                case UpdateOwnedGramCount updateOwnedGramCount:
                    _ownedGramCount = updateOwnedGramCount.GramAmount;
                    break;
                case UpdateDefaultPaidReactionType updateDefaultPaidReactionType:
                    DefaultPaidReactionType = updateDefaultPaidReactionType.Type;
                    break;
                case UpdateFreezeState updateFreezeState:
                    _freezeState = updateFreezeState;
                    break;
                case UpdateStakeDiceState updateStakeDiceState:
                    _stakeDiceState = updateStakeDiceState.State;
                    break;
                case UpdateAgeVerificationParameters updateAgeVerificationParameters:
                    AgeVerificationParameters = updateAgeVerificationParameters.Parameters;
                    break;
                case UpdateSavedMessagesTopicCount updateSavedMessagesTopicCount:
                    SavedMessagesTopicCount = updateSavedMessagesTopicCount.TopicCount;
                    break;
                case UpdateNewMessage updateNewMessage:
                    UpdateForumTopic(updateNewMessage.Message.ChatId, false, manager => manager.UpdateNewMessage(updateNewMessage.Message));
                    break;
                case UpdateDeleteMessages updateDeleteMessages:
                    UpdateChatLastMessage(updateDeleteMessages);
                    UpdateForumTopic(updateDeleteMessages.ChatId, false, manager => manager.UpdateDeleteMessages(updateDeleteMessages.MessageIds, updateDeleteMessages.IsPermanent, updateDeleteMessages.FromCache));
                    break;
                case UpdateMessageSendSucceeded updateMessageSendSucceeded:
                    UpdateChatLastMessage(updateMessageSendSucceeded);
                    UpdateForumTopic(updateMessageSendSucceeded.Message.ChatId, false, manager => manager.UpdateMessageSendSucceeded(updateMessageSendSucceeded.Message, updateMessageSendSucceeded.OldMessageId));
                    break;
                case UpdateMessageSendFailed updateMessageSendFailed:
                    UpdateChatLastMessage(updateMessageSendFailed);
                    UpdateForumTopic(updateMessageSendFailed.Message.ChatId, false, manager => manager.UpdateMessageSendFailed(updateMessageSendFailed.Message, updateMessageSendFailed.OldMessageId, updateMessageSendFailed.Error));
                    break;
                case UpdateMessageContent updateMessageContent:
                    UpdateForumTopic(updateMessageContent.ChatId, false, manager => manager.UpdateMessageContent(updateMessageContent.MessageId, updateMessageContent.NewContent));
                    break;
                case UpdateMessageEdited updateMessageEdited:
                    UpdateForumTopic(updateMessageEdited.ChatId, false, manager => manager.UpdateMessageEdited(updateMessageEdited.MessageId, updateMessageEdited.EditDate, updateMessageEdited.ReplyMarkup));
                    break;
                case UpdateMessageInteractionInfo updateMessageInteractionInfo:
                    UpdateForumTopic(updateMessageInteractionInfo.ChatId, false, manager => manager.UpdateMessageInteractionInfo(updateMessageInteractionInfo.MessageId, updateMessageInteractionInfo.InteractionInfo));
                    break;
                case UpdateMessageContentOpened updateMessageContentOpened:
                    UpdateForumTopic(updateMessageContentOpened.ChatId, false, manager => manager.UpdateMessageContentOpened(updateMessageContentOpened.MessageId));
                    break;
                case UpdateMessageFactCheck updateMessageFactCheck:
                    UpdateForumTopic(updateMessageFactCheck.ChatId, false, manager => manager.UpdateMessageFactCheck(updateMessageFactCheck.MessageId, updateMessageFactCheck.FactCheck));
                    break;
            }

            _aggregator.Publish(update);
        }

        private readonly Dictionary<int, QuickReplyShortcutInfo> _quickReplyShortcuts = new();
        private IList<int> _quickReplyShortcutIds;
        private bool _quickReplyShortcutsRequested;
    }

    public partial class QuickReplyShortcutInfo
    {
        public QuickReplyShortcut Shortcut { get; set; }

        public Vector<QuickReplyMessage> Messages { get; set; }
    }

    public partial class ChatListUnreadCount
    {
        public ChatList ChatList { get; set; }

        public UpdateUnreadChatCount UnreadChatCount { get; set; }
        public UpdateUnreadMessageCount UnreadMessageCount { get; set; }
    }

    public partial class MessageSenderEqualityComparer : IEqualityComparer<MessageSender>
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

namespace Telegram.Td.Api
{
    public sealed partial class Topics
    {
        public Topics(int totalCount, IList<long> topics)
        {
            TotalCount = totalCount;
            TopicIds = topics;
        }

        public int TotalCount { get; set; }

        public IList<long> TopicIds { get; set; }
    }

    public sealed partial class ForumTopics2
    {
        public ForumTopics2(int totalCount, IList<int> topics)
        {
            TotalCount = totalCount;
            TopicIds = topics;
        }

        public int TotalCount { get; set; }

        public IList<int> TopicIds { get; set; }
    }

    public readonly struct OrderedItem : IComparable<OrderedItem>
    {
        public readonly long Id;
        public readonly long Order;

        public OrderedItem(long id, long order)
        {
            Id = id;
            Order = order;
        }

        public int CompareTo(OrderedItem o)
        {
            if (Order != o.Order)
            {
                return o.Order < Order ? -1 : 1;
            }

            if (Id != o.Id)
            {
                return o.Id < Id ? -1 : 1;
            }

            return 0;
        }

        public override bool Equals(object obj)
        {
            OrderedItem o = (OrderedItem)obj;
            return Id == o.Id && Order == o.Order;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, Order);
        }
    }

    public readonly struct OrderedTopic : IComparable<OrderedTopic>
    {
        public readonly int Id;
        public readonly long Order;

        public OrderedTopic(int id, long order)
        {
            Id = id;
            Order = order;
        }

        public int CompareTo(OrderedTopic o)
        {
            if (Order != o.Order)
            {
                return o.Order < Order ? -1 : 1;
            }

            if (Id != o.Id)
            {
                return o.Id < Id ? -1 : 1;
            }

            return 0;
        }

        public override bool Equals(object obj)
        {
            OrderedTopic o = (OrderedTopic)obj;
            return Id == o.Id && Order == o.Order;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, Order);
        }
    }

}