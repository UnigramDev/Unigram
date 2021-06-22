using System.Linq;
using System.Numerics;
using Unigram.Native.Calls;
using Unigram.Services.Settings;
using Windows.Globalization;
using Windows.Storage;
using Windows.System.Profile;

namespace Unigram.Services
{
    public interface ISettingsService
    {
        int Session { get; }
        ulong Version { get; }
        ulong SystemVersion { get; }

        void UpdateVersion();

        ChatSettingsBase Chats { get; }
        NotificationsSettings Notifications { get; }
        StickersSettings Stickers { get; }
        EmojiSettings Emoji { get; }
        AutoDownloadSettings AutoDownload { get; set; }
        AppearanceSettings Appearance { get; }
        FiltersSettings Filters { get; }
        PasscodeLockSettings PasscodeLock { get; }
        PlaybackSettings Playback { get; }
        VoIPSettings VoIP { get; }

        DiagnosticsSettings Diagnostics { get; }

        int UserId { get; set; }
        long PushReceiverId { get; set; }

        string FilesDirectory { get; set; }

        int VerbosityLevel { get; set; }
        bool UseTestDC { get; set; }

        bool CollapseArchivedChats { get; set; }
        bool IsAdaptiveWideEnabled { get; set; }
        bool IsTrayVisible { get; set; }
        bool IsLaunchMinimized { get; set; }
        bool IsSendByEnterEnabled { get; set; }
        bool IsTextFormattingVisible { get; set; }
        bool IsReplaceEmojiEnabled { get; set; }
        bool IsLargeEmojiEnabled { get; set; }
        bool IsContactsSyncEnabled { get; set; }
        bool IsContactsSyncRequested { get; set; }
        bool IsContactsSortedByEpoch { get; set; }
        bool IsSecretPreviewsEnabled { get; set; }
        bool IsAutoPlayAnimationsEnabled { get; set; }
        bool IsAutoPlayVideosEnabled { get; set; }
        bool IsSendGrouped { get; set; }
        bool IsAccountsSelectorExpanded { get; set; }
        bool IsAllAccountsNotifications { get; set; }

        bool UseSystemProxy { get; set; }

        int[] AccountsSelectorOrder { get; set; }

        bool IsLeftTabsEnabled { get; set; }

        Vector2 Pencil { get; set; }

        DistanceUnits DistanceUnits { get; set; }

        bool FullScreenGallery { get; set; }
        bool DisableHighlightWords { get; set; }

        bool IsStreamingEnabled { get; set; }
        double VolumeLevel { get; set; }

        int LastMessageTtl { get; set; }

        string LanguagePackId { get; set; }
        string LanguagePluralId { get; set; }
        string LanguageShownId { get; set; }

        string PushToken { get; set; }

        VoipDataSaving UseLessData { get; set; }

        void SetChatPinnedMessage(long chatId, long messageId);
        long GetChatPinnedMessage(long chatId);

        void Clear();
    }

    public enum DistanceUnits
    {
        Automatic,
        Kilometers,
        Miles
    }

    public class SettingsServiceBase
    {
        protected readonly ApplicationDataContainer _container;

        public SettingsServiceBase(string key)
            : this(ApplicationData.Current.LocalSettings.CreateContainer(key, ApplicationDataCreateDisposition.Always))
        {

        }

        public SettingsServiceBase(ApplicationDataContainer container = null)
        {
            _container = container ?? ApplicationData.Current.LocalSettings;
        }



        public bool AddOrUpdateValue(string key, object value)
        {
            return AddOrUpdateValue(_container, key, value);
        }

        protected bool AddOrUpdateValue<T>(ref T storage, string key, T value)
        {
            storage = value;
            return AddOrUpdateValue(_container, key, value);
        }

        protected bool AddOrUpdateValue<T>(ref T storage, ApplicationDataContainer container, string key, T value)
        {
            storage = value;
            return AddOrUpdateValue(container, key, value);
        }

        protected bool AddOrUpdateValue(ApplicationDataContainer container, string key, object value)
        {
            bool valueChanged = false;

            if (container.Values.ContainsKey(key))
            {
                if (container.Values[key] != value)
                {
                    container.Values[key] = value;
                    valueChanged = true;
                }
            }
            else
            {
                container.Values.Add(key, value);
                valueChanged = true;
            }

            return valueChanged;
        }

        public valueType GetValueOrDefault<valueType>(string key, valueType defaultValue)
        {
            return GetValueOrDefault(_container, key, defaultValue);
        }

        protected valueType GetValueOrDefault<valueType>(ApplicationDataContainer container, string key, valueType defaultValue)
        {
            valueType value;

            if (container.Values.ContainsKey(key))
            {
                value = (valueType)container.Values[key];
            }
            else
            {
                value = defaultValue;
            }

            return value;
        }

        public virtual void Clear()
        {
            _container.Values.Clear();
        }
    }

    public class SettingsService : SettingsServiceBase, ISettingsService
    {
        private static SettingsService _current;
        public static SettingsService Current
        {
            get
            {
                if (_current == null)
                {
                    _current = new SettingsService();
                }

                return _current;
            }
        }

        private readonly int _session;
        private readonly ApplicationDataContainer _local;
        private readonly ApplicationDataContainer _own;

        private SettingsService()
        {
            _local = ApplicationData.Current.LocalSettings;
        }

        public SettingsService(int session)
            : base(session > 0 ? ApplicationData.Current.LocalSettings.CreateContainer($"{session}", ApplicationDataCreateDisposition.Always) : null)
        {
            _session = session;
            _local = ApplicationData.Current.LocalSettings;
            _own = ApplicationData.Current.LocalSettings.CreateContainer($"{session}", ApplicationDataCreateDisposition.Always);
        }

        public ApplicationDataContainer Container => _container;

        #region App version

        public const ulong CurrentVersion = (7UL << 48) | (7UL << 32) | (0UL << 16);
        public const string CurrentChangelog = @"Payments 2.0, Scheduled Voice Chats

**Payments 2.0**
• Offer real goods and services for sale in any group, channel or bot – Telegram doesn't charge a commission.
• Pay for goods securely using one of the 8 integrated payment providers – Telegram doesn't collect your payment info.
• See how this works in our @TestStore.

**Scheduled Voice Chats**
• Schedule voice chats to let participants know about them in advance.
• View a countdown to the voice chat and get notified when it starts.";

        public int Session => _session;

        private ulong? _version;
        public ulong Version
        {
            get => _version ??= GetValueOrDefault("LongVersion", 0UL);
            set => AddOrUpdateValue(ref _version, "LongVersion", value);
        }

        private ulong? _systemVersion;
        public ulong SystemVersion
        {
            get => _systemVersion ??= GetValueOrDefault("SystemVersion", 0UL);
            set => AddOrUpdateValue(ref _systemVersion, "SystemVersion", value);
        }

        public void UpdateVersion()
        {
            string deviceFamilyVersion = AnalyticsInfo.VersionInfo.DeviceFamilyVersion;
            ulong version = ulong.Parse(deviceFamilyVersion);
            ulong build = (version & 0x00000000FFFF0000L) >> 16;

            Version = CurrentVersion;
            SystemVersion = build;
        }

        #endregion

        private ChatSettingsBase _chats;
        public ChatSettingsBase Chats => _chats ??= new ChatSettingsBase(_own);

        private NotificationsSettings _notifications;
        public NotificationsSettings Notifications => _notifications ??= new NotificationsSettings(_container);

        private static StickersSettings _stickers;
        public StickersSettings Stickers => _stickers ??= new StickersSettings(_local);

        private static EmojiSettings _emoji;
        public EmojiSettings Emoji => _emoji ??= new EmojiSettings();

        private AutoDownloadSettings _autoDownload;
        public AutoDownloadSettings AutoDownload
        {
            get => _autoDownload ??= new AutoDownloadSettings(_own.CreateContainer("AutoDownload", ApplicationDataCreateDisposition.Always));
            set
            {
                _autoDownload = value ?? AutoDownloadSettings.Default;
                _autoDownload.Save(_own.CreateContainer("AutoDownload", ApplicationDataCreateDisposition.Always));
            }
        }

        private static AppearanceSettings _appearance;
        public AppearanceSettings Appearance => _appearance ??= new AppearanceSettings();

        private static DiagnosticsSettings _diagnostics;
        public DiagnosticsSettings Diagnostics => _diagnostics ??= new DiagnosticsSettings();

        private FiltersSettings _filters;
        public FiltersSettings Filters => _filters ??= new FiltersSettings(_own);

        private static PasscodeLockSettings _passcodeLock;
        public PasscodeLockSettings PasscodeLock => _passcodeLock ??= new PasscodeLockSettings();

        private static PlaybackSettings _playback;
        public PlaybackSettings Playback => _playback ??= new PlaybackSettings(_local);

        private static VoIPSettings _voip;
        public VoIPSettings VoIP => _voip ??= new VoIPSettings();

        private string _filesDirectory;
        public string FilesDirectory
        {
            get => _filesDirectory ??= GetValueOrDefault("FilesDirectory", null as string);
            set => AddOrUpdateValue(ref _filesDirectory, "FilesDirectory", value);
        }

        private int? _verbosityLevel;
        public int VerbosityLevel
        {
#if DEBUG
            get => _verbosityLevel ??= GetValueOrDefault(_local, "VerbosityLevel", 5);
#else
            get => _verbosityLevel ??= GetValueOrDefault(_local, "VerbosityLevel", 1);
#endif
            set => AddOrUpdateValue(ref _verbosityLevel, _local, "VerbosityLevel", value);
        }

        private bool? _useTestDC;
        public bool UseTestDC
        {
            get => _useTestDC ??= GetValueOrDefault(_own, "UseTestDC", false);
            set => AddOrUpdateValue(ref _useTestDC, _own, "UseTestDC", value);
        }

        private int? _userId;
        public int UserId
        {
            get => _userId ??= GetValueOrDefault(_own, "UserId", 0);
            set
            {
                _userId = value;
                AddOrUpdateValue(_local, $"User{value}", Session);
                AddOrUpdateValue(_own, "UserId", value);
            }
        }

        private long? _pushReceiverId;
        public long PushReceiverId
        {
            get
            {
                if (_pushReceiverId == null)
                {
                    _pushReceiverId = GetValueOrDefault(_own, "PushReceiverId", 0L);
                }

                return _pushReceiverId ?? 0L;
            }
            set
            {
                _pushReceiverId = value;
                AddOrUpdateValue(_local, $"PushReceiverId{value}", Session);
                AddOrUpdateValue(_own, "PushReceiverId", value);
            }
        }

        private static int? _distanceUnits;
        public DistanceUnits DistanceUnits
        {
            get => (DistanceUnits)(_distanceUnits ??= GetValueOrDefault("DistanceUnits", 0));
            set => AddOrUpdateValue(ref _distanceUnits, "DistanceUnits", (int)value);
        }

        private static double? _dialogsWidthRatio;
        public double DialogsWidthRatio
        {
            get => _dialogsWidthRatio ??= GetValueOrDefault(_local, "DialogsWidthRatio", 5d / 14d);
            set => AddOrUpdateValue(ref _dialogsWidthRatio, _local, "DialogsWidthRatio", value);
        }

        private bool? _isSidebarOpen;
        public bool IsSidebarOpen
        {
            get => _isSidebarOpen ??= GetValueOrDefault(_local, "IsSidebarOpen", true);
            set => AddOrUpdateValue(ref _isSidebarOpen, _local, "IsSidebarOpen", value);
        }

        private static bool? _isAdaptiveWideEnabled;
        public bool IsAdaptiveWideEnabled
        {
            get => _isAdaptiveWideEnabled ??= GetValueOrDefault(_local, "IsAdaptiveWideEnabled", false);
            set => AddOrUpdateValue(ref _isAdaptiveWideEnabled, _local, "IsAdaptiveWideEnabled", value);
        }

        private static bool? _isTrayVisible;
        public bool IsTrayVisible
        {
            get => _isTrayVisible ??= GetValueOrDefault(_local, "IsTrayVisible", true);
            set => AddOrUpdateValue(ref _isTrayVisible, _local, "IsTrayVisible", value);
        }

        private static bool? _isLaunchMinimized;
        public bool IsLaunchMinimized
        {
            get => _isLaunchMinimized ??= GetValueOrDefault(_local, "IsLaunchMinimized", false);
            set => AddOrUpdateValue(ref _isLaunchMinimized, _local, "IsLaunchMinimized", value);
        }

        private static bool? _collapseArchivedChats;
        public bool CollapseArchivedChats
        {
            get => _collapseArchivedChats ??= GetValueOrDefault(_local, "CollapseArchivedChats", false);
            set => AddOrUpdateValue(ref _collapseArchivedChats, _local, "CollapseArchivedChats", value);
        }

        private static bool? _isAccountsSelectorExpanded;
        public bool IsAccountsSelectorExpanded
        {
            get => _isAccountsSelectorExpanded ??= GetValueOrDefault(_local, "IsAccountsSelectorExpanded", false);
            set => AddOrUpdateValue(ref _isAccountsSelectorExpanded, _local, "IsAccountsSelectorExpanded", value);
        }

        private int[] _accountsSelectorOrder;
        public int[] AccountsSelectorOrder
        {
            get
            {
                if (_accountsSelectorOrder == null)
                {
                    var value = GetValueOrDefault<string>(_local, "AccountsSelectorOrder", null);
                    if (value == null)
                    {
                        _accountsSelectorOrder = new int[0];
                    }
                    else
                    {
                        _accountsSelectorOrder = value.Split(',').Select(x => int.Parse(x)).ToArray();
                    }
                }

                return _accountsSelectorOrder;
            }
            set
            {
                _accountsSelectorOrder = value;
                AddOrUpdateValue(_local, "AccountsSelectorOrder", value != null ? string.Join(",", value) : null);
            }
        }

        private static bool? _isAllAccountsNotifications;
        public bool IsAllAccountsNotifications
        {
            get => _isAllAccountsNotifications ??= GetValueOrDefault(_local, "IsAllAccountsNotifications", true);
            set => AddOrUpdateValue(ref _isAllAccountsNotifications, _local, "IsAllAccountsNotifications", value);
        }

        private static bool? _useSystemProxy;
        public bool UseSystemProxy
        {
            get => _useSystemProxy ??= GetValueOrDefault(_own, "UseSystemProxy", false);
            set => AddOrUpdateValue(ref _useSystemProxy, _own, "UseSystemProxy", value);
        }

        private static bool? _isLeftTabsEnabled;
        public bool IsLeftTabsEnabled
        {
            get => _isLeftTabsEnabled ??= GetValueOrDefault(_local, "IsLeftTabsEnabled", false);
            set => AddOrUpdateValue(ref _isLeftTabsEnabled, _local, "IsLeftTabsEnabled", value);
        }

        private static bool? _fullScreenGallery;
        public bool FullScreenGallery
        {
            get => _fullScreenGallery ??= GetValueOrDefault(_local, "FullScreenGallery", false);
            set => AddOrUpdateValue(ref _fullScreenGallery, _local, "FullScreenGallery", value);
        }

        private static bool? _disableHighlightWords;
        public bool DisableHighlightWords
        {
            get => _disableHighlightWords ??= GetValueOrDefault(_local, "DisableHighlightWords", false);
            set => AddOrUpdateValue(ref _disableHighlightWords, _local, "DisableHighlightWords", value);
        }

        private static bool? _isSendByEnterEnabled;
        public bool IsSendByEnterEnabled
        {
            get => _isSendByEnterEnabled ??= GetValueOrDefault(_local, "IsSendByEnterEnabled", true);
            set => AddOrUpdateValue(ref _isSendByEnterEnabled, _local, "IsSendByEnterEnabled", value);
        }

        private bool? _isTextFormattingVisible;
        public bool IsTextFormattingVisible
        {
            get => _isTextFormattingVisible ??= GetValueOrDefault("IsTextFormattingVisible", false);
            set => AddOrUpdateValue(ref _isTextFormattingVisible, "IsTextFormattingVisible", value);
        }

        private bool? _isReplaceEmojiEnabled;
        public bool IsReplaceEmojiEnabled
        {
            get => _isReplaceEmojiEnabled ??= GetValueOrDefault("IsReplaceEmojiEnabled", true);
            set => AddOrUpdateValue(ref _isReplaceEmojiEnabled, "IsReplaceEmojiEnabled", value);
        }

        private static bool? _isLargeEmojiEnabled;
        public bool IsLargeEmojiEnabled
        {
            get => _isLargeEmojiEnabled ??= GetValueOrDefault(_local, "IsLargeEmojiEnabled", true);
            set => AddOrUpdateValue(ref _isLargeEmojiEnabled, _local, "IsLargeEmojiEnabled", value);
        }

        private bool? _isContactsSyncEnabled;
        public bool IsContactsSyncEnabled
        {
            get => _isContactsSyncEnabled ??= GetValueOrDefault("IsContactsSyncEnabled", true);
            set => AddOrUpdateValue(ref _isContactsSyncEnabled, "IsContactsSyncEnabled", value);
        }

        private bool? _isContactsSyncRequested;
        public bool IsContactsSyncRequested
        {
            get => _isContactsSyncRequested ??= GetValueOrDefault("IsContactsSyncRequested", false);
            set => AddOrUpdateValue(ref _isContactsSyncRequested, "IsContactsSyncRequested", value);
        }

        private bool? _isContactsSortedByEpoch;
        public bool IsContactsSortedByEpoch
        {
            get => _isContactsSortedByEpoch ??= GetValueOrDefault("IsContactsSortedByEpoch", true);
            set => AddOrUpdateValue(ref _isContactsSortedByEpoch, "IsContactsSortedByEpoch", value);
        }

        private bool? _isSecretPreviewsEnabled;
        public bool IsSecretPreviewsEnabled
        {
            get => _isSecretPreviewsEnabled ??= GetValueOrDefault("IsSecretPreviewsEnabled", false);
            set => AddOrUpdateValue(ref _isSecretPreviewsEnabled, "IsSecretPreviewsEnabled", value);
        }

        private bool? _isAutoPlayAnimationsEnabled;
        public bool IsAutoPlayAnimationsEnabled
        {
            get => _isAutoPlayAnimationsEnabled ??= GetValueOrDefault("IsAutoPlayEnabled", true);
            set => AddOrUpdateValue(ref _isAutoPlayAnimationsEnabled, "IsAutoPlayEnabled", value);
        }

        private bool? _isAutoPlayVideosEnabled;
        public bool IsAutoPlayVideosEnabled
        {
            get => _isAutoPlayVideosEnabled ??= GetValueOrDefault("IsAutoPlayVideosEnabled", true);
            set => AddOrUpdateValue(ref _isAutoPlayVideosEnabled, "IsAutoPlayVideosEnabled", value);
        }

        private bool? _isSendGrouped;
        public bool IsSendGrouped
        {
            get => _isSendGrouped ??= GetValueOrDefault("IsSendGrouped", true);
            set => AddOrUpdateValue(ref _isSendGrouped, "IsSendGrouped", value);
        }

        private bool? _isStreamingEnabled;
        public bool IsStreamingEnabled
        {
            get => _isStreamingEnabled ??= GetValueOrDefault("IsStreamingEnabled", true);
            set => AddOrUpdateValue(ref _isStreamingEnabled, "IsStreamingEnabled", value);
        }

        private static double? _volumeLevel;
        public double VolumeLevel
        {
            get => _volumeLevel ??= GetValueOrDefault("VolumeLevel", 1d);
            set => AddOrUpdateValue(ref _volumeLevel, "VolumeLevel", value);
        }

        private static Vector2? _pencil;
        public Vector2 Pencil
        {
            get
            {
                if (_pencil == null)
                {
                    var offset = GetValueOrDefault(_local, "PencilOffset", 1f);
                    var thickness = GetValueOrDefault(_local, "PencilThickness", 0.22f);

                    _pencil = new Vector2(offset, thickness);
                }

                return _pencil ?? new Vector2(1f, 0.22f);
            }
            set
            {
                _pencil = value;
                AddOrUpdateValue(_local, "PencilOffset", value.X);
                AddOrUpdateValue(_local, "PencilThickness", value.Y);
            }
        }

        private int? _lastMessageTtl;
        public int LastMessageTtl
        {
            get => _lastMessageTtl ??= GetValueOrDefault("LastMessageTtl", 7);
            set => AddOrUpdateValue(ref _lastMessageTtl, "LastMessageTtl", value);
        }

        private int? _previousSession;
        public int PreviousSession
        {
            get => _previousSession ??= GetValueOrDefault(_local, "PreviousSession", 0);
            set => AddOrUpdateValue(ref _previousSession, _local, "PreviousSession", value);
        }

        private int? _activeSession;
        public int ActiveSession
        {
            get => _activeSession ??= GetValueOrDefault(_local, "SelectedAccount", 0);
            set => AddOrUpdateValue(ref _activeSession, _local, "SelectedAccount", value);
        }

        private string _languagePackId;
        public string LanguagePackId
        {
            get => _languagePackId ??= GetValueOrDefault(_local, "LanguagePackId", ApplicationLanguages.Languages[0].Split('-').First());
            set => AddOrUpdateValue(ref _languagePackId, _local, "LanguagePackId", value);
        }

        private string _languagePluralId;
        public string LanguagePluralId
        {
            get => _languagePluralId ??= GetValueOrDefault(_local, "LanguagePluralId", ApplicationLanguages.Languages[0].Split('-').First());
            set => AddOrUpdateValue(ref _languagePluralId, _local, "LanguagePluralId", value);
        }

        private string _languageShownId;
        public string LanguageShownId
        {
            get => _languageShownId ??= GetValueOrDefault<string>(_local, "LanguageShownId", null);
            set => AddOrUpdateValue(ref _languageShownId, _local, "LanguageShownId", value);
        }

        private string _pushToken;
        public string PushToken
        {
            get => _pushToken ??= GetValueOrDefault<string>("ChannelUri", null);
            set => AddOrUpdateValue(ref _pushToken, "ChannelUri", value);
        }

        private int? _useLessData;
        public VoipDataSaving UseLessData
        {
            get => (VoipDataSaving)(_useLessData ??= GetValueOrDefault("UseLessData", 0));
            set => AddOrUpdateValue(ref _useLessData, "UseLessData", (int)value);
        }

        public void SetChatPinnedMessage(long chatId, long messageId)
        {
            var container = _own.CreateContainer("PinnedMessages", ApplicationDataCreateDisposition.Always);
            AddOrUpdateValue(container, $"{chatId}", messageId);
        }

        public long GetChatPinnedMessage(long chatId)
        {
            var container = _own.CreateContainer("PinnedMessages", ApplicationDataCreateDisposition.Always);
            return GetValueOrDefault(container, $"{chatId}", 0L);
        }

        public void CleanUp()
        {
            // Here should be cleaned up all the settings that are shared with background tasks.
            //_useLessData = null;
        }

        public new void Clear()
        {
            _container.Values.Clear();

            if (_own != null)
            {
                _own.Values.Clear();
            }

            if (_local != null)
            {
                _local.Values.Remove($"User{UserId}");
            }
        }
    }

    public class ChatSettingsBase : SettingsServiceBase
    {
        public ChatSettingsBase(ApplicationDataContainer container = null)
            : base(container)
        {
        }

        public object this[long chatId, long threadId, ChatSetting key]
        {
            //get => GetValueOrDefault<object>(chatId + key, null);
            set => AddOrUpdateValue(ConvertToKey(chatId, threadId, key), value);
        }

        public bool TryRemove<T>(long chatId, long threadId, ChatSetting key, out T value)
        {
            if (_container.Values.ContainsKey(ConvertToKey(chatId, threadId, key)))
            {
                value = (T)_container.Values[ConvertToKey(chatId, threadId, key)];
                _container.Values.Remove(ConvertToKey(chatId, threadId, key));
                return true;
            }

            value = default;
            return false;
        }

        private string ConvertToKey(long chatId, long threadId, ChatSetting setting)
        {
            if (threadId != 0)
            {
                return $"{chatId}{threadId}{setting}";
            }

            return $"{chatId}{setting}";
        }
    }

    public enum ChatSetting
    {
        Index,
        Pixel,
        ReadInboxMaxId
    }
}
