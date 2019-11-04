using System;
using System.Linq;
using System.Text;
using Unigram.Common;
using Unigram.Services;
using Unigram.Services.Settings;
using Windows.Globalization;
using Windows.Storage;
using Windows.UI.Xaml;

namespace Unigram.Services
{
    public interface ISettingsService
    {
        int Session { get; }
        ulong Version { get; }

        void UpdateVersion();

        NotificationsSettings Notifications { get; }
        StickersSettings Stickers { get; }
        WalletSettings Wallet { get; }
        AutoDownloadSettings AutoDownload { get; set; }
        AppearanceSettings Appearance { get; }
        WallpaperSettings Wallpaper { get; }
        PasscodeLockSettings PasscodeLock { get; }
        PlaybackSettings Playback { get; }

        int UserId { get; set; }

        string FilesDirectory { get; set; }

        int VerbosityLevel { get; }
        bool UseTestDC { get; set; }

        bool UseThreeLinesLayout { get; set; }
        bool CollapseArchivedChats { get; set; }
        bool IsAdaptiveWideEnabled { get; set; }
        bool IsTrayVisible { get; set; }
        bool IsLaunchMinimized { get; set; }
        bool IsSendByEnterEnabled { get; set; }
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

        DistanceUnits DistanceUnits { get; set; }

        bool AreAnimationsEnabled { get; set; }

        bool IsStreamingEnabled { get; set; }
        double VolumeLevel { get; set; }

        int LastMessageTtl { get; set; }

        string LanguagePackId { get; set; }
        string LanguagePluralId { get; set; }
        string LanguageShownId { get; set; }

        string NotificationsToken { get; set; }
        int[] NotificationsIds { get; set; }

        libtgvoip.DataSavingMode UseLessData { get; set; }

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

        public SettingsServiceBase(ApplicationDataContainer container = null)
        {
            _container = container ?? ApplicationData.Current.LocalSettings;
        }



        public bool AddOrUpdateValue(string key, Object value)
        {
            return AddOrUpdateValue(_container, key, value);
        }

        protected bool AddOrUpdateValue(ApplicationDataContainer container, string key, Object value)
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
            return GetValueOrDefault<valueType>(_container, key, defaultValue);
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
                    _current = new SettingsService();

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
            : base(session > 0 ? ApplicationData.Current.LocalSettings.CreateContainer(session.ToString(), ApplicationDataCreateDisposition.Always) : null)
        {
            _session = session;
            _local = ApplicationData.Current.LocalSettings;
            _own = ApplicationData.Current.LocalSettings.CreateContainer($"{session}", ApplicationDataCreateDisposition.Always);
        }

        #region App version

        public const ulong CurrentVersion = (3UL << 48) | (12UL << 32) | (2605UL << 16);
        public const string CurrentChangelog = "ARCHIVED CHATS\r\n• Right click on any chat to archive it.\r\n• Right click on your archive to hide it from the chat list.\r\n• Pin an unlimited number of chats in your archive.\r\n\r\nADDING TO CONTACTS MADE EASIER\r\n• You can now add any users to your contacts, even if their phone numbers are not visible. \r\n• Quickly add users standing next to you by opening Contacts > Add People Nearby. You will see people who have this section open.\r\n\r\nLOCATION-BASED CHATS \r\n• Host local communities by creating location-based group chats from the People Nearby section.\r\n\r\nALSO IN THIS UPDATE\r\n• Choose who can see your phone number with granular precision in Privacy & Security settings.\r\n\r\nFor group admins and developers:\r\n• Connect a discussion group to your channel to get a 'Discuss' button.\r\n• Seamlessly integrate bots with web services.";
        public const bool CurrentMedia = false;

        public int Session => _session;

        private ulong? _version;
        public ulong Version
        {
            get
            {
                if (_version == null)
                    _version = GetValueOrDefault("LongVersion", 0UL);

                return _version ?? 0;
            }
            private set
            {
                _version = value;
                AddOrUpdateValue("LongVersion", value);
            }
        }

        public void UpdateVersion()
        {
            Version = CurrentVersion;
        }

        #endregion

        private NotificationsSettings _notifications;
        public NotificationsSettings Notifications
        {
            get
            {
                return _notifications = _notifications ?? new NotificationsSettings(_container);
            }
        }

        private static StickersSettings _stickers;
        public StickersSettings Stickers
        {
            get
            {
                return _stickers = _stickers ?? new StickersSettings(_local);
            }
        }

        private WalletSettings _wallet;
        public WalletSettings Wallet
        {
            get
            {
                return _wallet = _wallet ?? new WalletSettings(_own);
            }
        }

        private AutoDownloadSettings _autoDownload;
        public AutoDownloadSettings AutoDownload
        {
            get
            {
                return _autoDownload = _autoDownload ?? new AutoDownloadSettings(_own.CreateContainer("AutoDownload", ApplicationDataCreateDisposition.Always));
            }
            set
            {
                _autoDownload = value ?? AutoDownloadSettings.Default;
                _autoDownload.Save(_own.CreateContainer("AutoDownload", ApplicationDataCreateDisposition.Always));
            }
        }

        private static AppearanceSettings _appearance;
        public AppearanceSettings Appearance
        {
            get
            {
                return _appearance = _appearance ?? new AppearanceSettings();
            }
        }

        private static DiagnosticsSettings _diagnostics;
        public DiagnosticsSettings Diagnostics
        {
            get
            {
                return _diagnostics = _diagnostics ?? new DiagnosticsSettings();
            }
        }

        private WallpaperSettings _wallpaper;
        public WallpaperSettings Wallpaper
        {
            get
            {
                return _wallpaper = _wallpaper ?? new WallpaperSettings(_container);
            }
        }

        private static PasscodeLockSettings _passcodeLock;
        public PasscodeLockSettings PasscodeLock
        {
            get
            {
                return _passcodeLock = _passcodeLock ?? new PasscodeLockSettings();
            }
        }

        private static PlaybackSettings _playback;
        public PlaybackSettings Playback
        {
            get
            {
                return _playback = _playback ?? new PlaybackSettings(_local);
            }
        }

        private static VoIPSettings _voip;
        public VoIPSettings VoIP
        {
            get
            {
                return _voip = _voip ?? new VoIPSettings();
            }
        }

        private string _filesDirectory;
        public string FilesDirectory
        {
            get
            {
                if (_filesDirectory == null)
                    _filesDirectory = GetValueOrDefault("FilesDirectory", null as string);

                return _filesDirectory;
            }
            set
            {
                _filesDirectory = value;
                AddOrUpdateValue("FilesDirectory", value);
            }
        }

        private int? _verbosityLevel;
        public int VerbosityLevel
        {
            get
            {
                if (_verbosityLevel == null)
#if DEBUG
                    _verbosityLevel = GetValueOrDefault(_local, "VerbosityLevel", 5);

                return _verbosityLevel ?? 5;
#else
                    _verbosityLevel = GetValueOrDefault(_local, "VerbosityLevel", 0);

                return _verbosityLevel ?? 0;
#endif
            }
            set
            {
                _verbosityLevel = value;
                AddOrUpdateValue(_local, "VerbosityLevel", value);
            }
        }

        private bool? _useTestDC;
        public bool UseTestDC
        {
            get
            {
                if (_useTestDC == null)
                    _useTestDC = GetValueOrDefault(_local, "UseTestDC", false);

                return _useTestDC ?? false;
            }
            set
            {
                _useTestDC = value;
                AddOrUpdateValue(_local, "UseTestDC", value);
            }
        }

        private int? _userId;
        public int UserId
        {
            get
            {
                if (_userId == null)
                    _userId = GetValueOrDefault(_own, "UserId", 0);

                return _userId ?? 0;
            }
            set
            {
                _userId = value;
                AddOrUpdateValue(_local, $"User{value}", Session);
                AddOrUpdateValue(_own, "UserId", value);
            }
        }

        private static int? _distanceUnits;
        public DistanceUnits DistanceUnits
        {
            get
            {
                if (_distanceUnits == null)
                    _distanceUnits = GetValueOrDefault("DistanceUnits", 0);

                return (DistanceUnits)(_distanceUnits ?? 0);
            }
            set
            {
                _distanceUnits = (int)value;
                AddOrUpdateValue("DistanceUnits", (int)value);
            }
        }

        private static double? _dialogsWidthRatio;
        public double DialogsWidthRatio
        {
            get
            {
                if (_dialogsWidthRatio == null)
                    _dialogsWidthRatio = GetValueOrDefault(_local, "DialogsWidthRatio", 5d / 14d);

                return _dialogsWidthRatio ?? 5d / 14d;
            }
            set
            {
                _dialogsWidthRatio = value;
                AddOrUpdateValue(_local, "DialogsWidthRatio", value);
            }
        }

        private static bool? _isAdaptiveWideEnabled;
        public bool IsAdaptiveWideEnabled
        {
            get
            {
                if (_isAdaptiveWideEnabled == null)
                    _isAdaptiveWideEnabled = GetValueOrDefault(_local, "IsAdaptiveWideEnabled", false);

                return _isAdaptiveWideEnabled ?? false;
            }
            set
            {
                _isAdaptiveWideEnabled = value;
                AddOrUpdateValue(_local, "IsAdaptiveWideEnabled", value);
            }
        }

        private static bool? _isTrayVisible;
        public bool IsTrayVisible
        {
            get
            {
                if (_isTrayVisible == null)
                    _isTrayVisible = GetValueOrDefault(_local, "IsTrayVisible", true);

                return _isTrayVisible ?? true;
            }
            set
            {
                _isTrayVisible = value;
                AddOrUpdateValue(_local, "IsTrayVisible", value);
            }
        }

        private static bool? _isLaunchMinimized;
        public bool IsLaunchMinimized
        {
            get
            {
                if (_isLaunchMinimized == null)
                    _isLaunchMinimized = GetValueOrDefault(_local, "IsLaunchMinimized", false);

                return _isLaunchMinimized ?? false;
            }
            set
            {
                _isLaunchMinimized = value;
                AddOrUpdateValue(_local, "IsTrayVisible", value);
            }
        }

        private static bool? _useThreeLinesLayout;
        public bool UseThreeLinesLayout
        {
            get
            {
                if (_useThreeLinesLayout == null)
                    _useThreeLinesLayout = GetValueOrDefault(_local, "UseThreeLinesLayout", false);

                return _useThreeLinesLayout ?? false;
            }
            set
            {
                _useThreeLinesLayout = value;
                AddOrUpdateValue(_local, "UseThreeLinesLayout", value);
            }
        }

        private static bool? _collapseArchivedChats;
        public bool CollapseArchivedChats
        {
            get
            {
                if (_collapseArchivedChats == null)
                    _collapseArchivedChats = GetValueOrDefault(_local, "CollapseArchivedChats", false);

                return _collapseArchivedChats ?? false;
            }
            set
            {
                _collapseArchivedChats = value;
                AddOrUpdateValue(_local, "CollapseArchivedChats", value);
            }
        }

        private static bool? _isAccountsSelectorExpanded;
        public bool IsAccountsSelectorExpanded
        {
            get
            {
                if (_isAccountsSelectorExpanded == null)
                    _isAccountsSelectorExpanded = GetValueOrDefault(_local, "IsAccountsSelectorExpanded", false);

                return _isAccountsSelectorExpanded ?? false;
            }
            set
            {
                _isAccountsSelectorExpanded = value;
                AddOrUpdateValue(_local, "IsAccountsSelectorExpanded", value);
            }
        }

        private static bool? _isAllAccountsNotifications;
        public bool IsAllAccountsNotifications
        {
            get
            {
                if (_isAllAccountsNotifications == null)
                    _isAllAccountsNotifications = GetValueOrDefault(_local, "IsAllAccountsNotifications", true);

                return _isAllAccountsNotifications ?? true;
            }
            set
            {
                _isAllAccountsNotifications = value;
                AddOrUpdateValue(_local, "IsAllAccountsNotifications", value);
            }
        }

        private static bool? _areAnimationsEnabled;
        public bool AreAnimationsEnabled
        {
            get
            {
                if (_areAnimationsEnabled == null)
                    _areAnimationsEnabled = GetValueOrDefault(_local, "AreAnimationsEnabled", ApiInfo.IsFullExperience);

                return _areAnimationsEnabled ?? ApiInfo.IsFullExperience;
            }
            set
            {
                _areAnimationsEnabled = value;
                AddOrUpdateValue(_local, "AreAnimationsEnabled", value);
            }
        }

        private bool? _isSendByEnterEnabled;
        public bool IsSendByEnterEnabled
        {
            get
            {
                if (_isSendByEnterEnabled == null)
                    _isSendByEnterEnabled = GetValueOrDefault("IsSendByEnterEnabled", true);

                return _isSendByEnterEnabled ?? true;
            }
            set
            {
                _isSendByEnterEnabled = value;
                AddOrUpdateValue("IsSendByEnterEnabled", value);
            }
        }

        private bool? _isReplaceEmojiEnabled;
        public bool IsReplaceEmojiEnabled
        {
            get
            {
                if (_isReplaceEmojiEnabled == null)
                    _isReplaceEmojiEnabled = GetValueOrDefault("IsReplaceEmojiEnabled", true);

                return _isReplaceEmojiEnabled ?? true;
            }
            set
            {
                _isReplaceEmojiEnabled = value;
                AddOrUpdateValue("IsReplaceEmojiEnabled", value);
            }
        }

        private static bool? _isLargeEmojiEnabled;
        public bool IsLargeEmojiEnabled
        {
            get
            {
                if (_isLargeEmojiEnabled == null)
                    _isLargeEmojiEnabled = GetValueOrDefault(_local, "IsLargeEmojiEnabled", true);

                return _isLargeEmojiEnabled ?? true;
            }
            set
            {
                _isLargeEmojiEnabled = value;
                AddOrUpdateValue(_local, "IsLargeEmojiEnabled", value);
            }
        }

        private bool? _isContactsSyncEnabled;
        public bool IsContactsSyncEnabled
        {
            get
            {
                if (_isContactsSyncEnabled == null)
                    _isContactsSyncEnabled = GetValueOrDefault("IsContactsSyncEnabled", true);

                return _isContactsSyncEnabled ?? true;
            }
            set
            {
                _isContactsSyncEnabled = value;
                AddOrUpdateValue("IsContactsSyncEnabled", value);
            }
        }

        private bool? _isContactsSyncRequested;
        public bool IsContactsSyncRequested
        {
            get
            {
                if (_isContactsSyncRequested == null)
                    _isContactsSyncRequested = GetValueOrDefault("IsContactsSyncRequested", false);

                return _isContactsSyncRequested ?? false;
            }
            set
            {
                _isContactsSyncRequested = value;
                AddOrUpdateValue("IsContactsSyncRequested", value);
            }
        }

        private bool? _isContactsSortedByEpoch;
        public bool IsContactsSortedByEpoch
        {
            get
            {
                if (_isContactsSortedByEpoch == null)
                    _isContactsSortedByEpoch = GetValueOrDefault("IsContactsSortedByEpoch", true);

                return _isContactsSortedByEpoch ?? true;
            }
            set
            {
                _isContactsSortedByEpoch = value;
                AddOrUpdateValue("IsContactsSortedByEpoch", value);
            }
        }

        private bool? _isSecretPreviewsEnabled;
        public bool IsSecretPreviewsEnabled
        {
            get
            {
                if (_isSecretPreviewsEnabled == null)
                    _isSecretPreviewsEnabled = GetValueOrDefault("IsSecretPreviewsEnabled", false);

                return _isSecretPreviewsEnabled ?? true;
            }
            set
            {
                _isSecretPreviewsEnabled = value;
                AddOrUpdateValue("IsSecretPreviewsEnabled", value);
            }
        }

        private bool? _isAutoPlayEnabled;
        public bool IsAutoPlayAnimationsEnabled
        {
            get
            {
                if (_isAutoPlayEnabled == null)
                    _isAutoPlayEnabled = GetValueOrDefault("IsAutoPlayEnabled", true);

                return _isAutoPlayEnabled ?? true;
            }
            set
            {
                _isAutoPlayEnabled = value;
                AddOrUpdateValue("IsAutoPlayEnabled", value);
            }
        }

        private bool? _isAutoPlayVideosEnabled;
        public bool IsAutoPlayVideosEnabled
        {
            get
            {
                if (_isAutoPlayVideosEnabled == null)
                    _isAutoPlayVideosEnabled = GetValueOrDefault("IsAutoPlayVideosEnabled", true);

                return _isAutoPlayVideosEnabled ?? true;
            }
            set
            {
                _isAutoPlayVideosEnabled = value;
                AddOrUpdateValue("IsAutoPlayVideosEnabled", value);
            }
        }

        private bool? _isSendGrouped;
        public bool IsSendGrouped
        {
            get
            {
                if (_isSendGrouped == null)
                    _isSendGrouped = GetValueOrDefault("IsSendGrouped", true);

                return _isSendGrouped ?? true;
            }
            set
            {
                _isSendGrouped = value;
                AddOrUpdateValue("IsSendGrouped", value);
            }
        }

        private bool? _isStreamingEnabled;
        public bool IsStreamingEnabled
        {
            get
            {
                if (_isStreamingEnabled == null)
                    _isStreamingEnabled = GetValueOrDefault("IsStreamingEnabled", true);

                return _isStreamingEnabled ?? true;
            }
            set
            {
                _isStreamingEnabled = value;
                AddOrUpdateValue("IsStreamingEnabled", value);
            }
        }

        private static double? _volumeLevel;
        public double VolumeLevel
        {
            get
            {
                if (_volumeLevel == null)
                    _volumeLevel = GetValueOrDefault("VolumeLevel", 1d);

                return _volumeLevel ?? 1d;
            }
            set
            {
                _volumeLevel = value;
                AddOrUpdateValue("VolumeLevel", value);
            }
        }

        private int? _lastMessageTtl;
        public int LastMessageTtl
        {
            get
            {
                if (_lastMessageTtl == null)
                    _lastMessageTtl = GetValueOrDefault("LastMessageTtl", 7);

                return _lastMessageTtl ?? 7;
            }
            set
            {
                _lastMessageTtl = value;
                AddOrUpdateValue("LastMessageTtl", value);
            }
        }

        private int? _previousSession;
        public int PreviousSession
        {
            get
            {
                if (_previousSession == null)
                    _previousSession = GetValueOrDefault(_local, "PreviousSession", 0);

                return _activeSession ?? 0;
            }
            set
            {
                _previousSession = value;
                AddOrUpdateValue(_local, "PreviousSession", value);
            }
        }

        private int? _activeSession;
        public int ActiveSession
        {
            get
            {
                if (_activeSession == null)
                    _activeSession = GetValueOrDefault(_local, "SelectedAccount", 0);

                return _activeSession ?? 0;
            }
            set
            {
                _activeSession = value;
                AddOrUpdateValue(_local, "SelectedAccount", value);
            }
        }

        private string _languagePackId;
        public string LanguagePackId
        {
            get
            {
                if (_languagePackId == null)
                    _languagePackId = GetValueOrDefault(_local, "LanguagePackId", ApplicationLanguages.Languages[0]);

                return _languagePackId;
            }
            set
            {
                _languagePackId = value;
                AddOrUpdateValue(_local, "LanguagePackId", value);
            }
        }

        private string _languagePluralId;
        public string LanguagePluralId
        {
            get
            {
                if (_languagePluralId == null)
                    _languagePluralId = GetValueOrDefault(_local, "LanguagePluralId", ApplicationLanguages.Languages[0]);

                return _languagePluralId;
            }
            set
            {
                _languagePluralId = value;
                AddOrUpdateValue(_local, "LanguagePluralId", value);
            }
        }

        private string _languageShownId;
        public string LanguageShownId
        {
            get
            {
                if (_languageShownId == null)
                    _languageShownId = GetValueOrDefault<string>(_local, "LanguageShownId", null);

                return _languageShownId;
            }
            set
            {
                _languageShownId = value;
                AddOrUpdateValue(_local, "LanguageShownId", value);
            }
        }

        private string _notificationsToken;
        public string NotificationsToken
        {
            get
            {
                if (_notificationsToken == null)
                    _notificationsToken = GetValueOrDefault<string>(_local, "ChannelUri", null);

                return _notificationsToken;
            }
            set
            {
                _notificationsToken = value;
                AddOrUpdateValue(_local, "ChannelUri", value);
            }
        }

        private int[] _notificationsIds;
        public int[] NotificationsIds
        {
            get
            {
                if (_notificationsIds == null)
                {
                    var value = GetValueOrDefault<string>(_local, "NotificationsIds", null);
                    if (value == null)
                    {
                        _notificationsIds = new int[0];
                    }
                    else
                    {
                        _notificationsIds = value.Split(',').Select(x => int.Parse(x)).ToArray();
                    }
                }

                return _notificationsIds;
            }
            set
            {
                _notificationsIds = value;
                AddOrUpdateValue(_local, "NotificationsIds", string.Join(",", value));
            }
        }

        private libtgvoip.DataSavingMode? _useLessData;
        public libtgvoip.DataSavingMode UseLessData
        {
            get
            {
                if (_useLessData == null)
                    _useLessData = (libtgvoip.DataSavingMode)GetValueOrDefault("UseLessData", 0);

                return _useLessData ?? libtgvoip.DataSavingMode.Never;
            }
            set
            {
                _useLessData = value;
                AddOrUpdateValue("UseLessData", (int)value);
            }
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
            _useLessData = null;
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
}
