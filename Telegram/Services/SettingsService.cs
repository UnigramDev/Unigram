//
// Copyright Fela Ameghino & Contributors 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Linq;
using System.Numerics;
using Telegram.Common;
using Telegram.Native.Calls;
using Telegram.Services.Settings;
using Windows.Globalization;
using Windows.Storage;
using Windows.System.Profile;

#if !ENABLE_CALLS

namespace Telegram.Native.Calls
{
    public enum VoipDataSaving
    {
        Never,
        Mobile,
        Always,
    }
}

#endif

namespace Telegram.Services
{
    public interface ISettingsService
    {
        int Session { get; }
        ulong Version { get; }
        ulong SystemVersion { get; }

        bool UpdateVersion(out string previousVersion);

        ChatSettingsBase Chats { get; }
        NotificationsSettings Notifications { get; }
        StickersSettings Stickers { get; }
        EmojiSettings Emoji { get; }
        AutoDownloadSettings AutoDownload { get; set; }
        AppearanceSettings Appearance { get; }
        PasscodeLockSettings PasscodeLock { get; }
        PlaybackSettings Playback { get; }
        VoIPSettings VoIP { get; }
        TranslateSettings Translate { get; }

        DiagnosticsSettings Diagnostics { get; }

        long UserId { get; set; }

        int VerbosityLevel { get; set; }
        bool UseTestDC { get; set; }

        bool HideArchivedChats { get; set; }
        bool IsAdaptiveWideEnabled { get; set; }
        bool IsTrayVisible { get; set; }
        bool IsLaunchMinimized { get; set; }
        bool IsSendByEnterEnabled { get; set; }
        bool IsReplaceEmojiEnabled { get; set; }
        bool IsContactsSyncEnabled { get; set; }
        bool IsContactsSyncRequested { get; set; }
        bool IsContactsSortedByEpoch { get; set; }
        bool IsSecretPreviewsEnabled { get; set; }
        bool AutoPlayAnimations { get; set; }
        bool AutoPlayVideos { get; set; }
        bool IsSendGrouped { get; set; }
        bool IsAccountsSelectorExpanded { get; set; }
        bool IsAllAccountsNotifications { get; set; }
        bool AreSmoothTransitionsEnabled { get; set; }
        bool AreMaterialsEnabled { get; set; }

        bool UseSystemProxy { get; set; }
        int LastProxyId { get; set; }

        int[] AccountsSelectorOrder { get; set; }

        bool IsLeftTabsEnabled { get; set; }

        Vector2 Pencil { get; set; }

        DistanceUnits DistanceUnits { get; set; }

        bool SwipeToShare { get; set; }
        bool SwipeToReply { get; set; }
        bool FullScreenGallery { get; set; }
        bool UseSystemSpellChecker { get; set; }

        bool IsStreamingEnabled { get; set; }
        double VolumeLevel { get; set; }
        bool VolumeMuted { get; set; }

        int LastMessageTtl { get; set; }

        string LanguagePackId { get; set; }
        string LanguagePluralId { get; set; }
        string LanguageBaseId { get; set; }
        string LanguageShownId { get; set; }

        bool InstallBetaUpdates { get; set; }

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

    public partial class SettingsServiceBase
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

    public partial class SettingsService : SettingsServiceBase, ISettingsService
    {
        private static SettingsService _current;
        public static SettingsService Current => _current ??= new SettingsService();

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

        public const ulong CurrentVersion = (10UL << 48) | (1UL << 32) | (0UL << 16);

        public int Session => _session;

        private ulong? _version;
        public ulong Version
        {
            get => _version ??= GetValueOrDefault("LongVersion", CurrentVersion);
            set => AddOrUpdateValue(ref _version, "LongVersion", value);
        }

        private ulong? _systemVersion;
        public ulong SystemVersion
        {
            get => _systemVersion ??= GetValueOrDefault("SystemVersion", 0UL);
            set => AddOrUpdateValue(ref _systemVersion, "SystemVersion", value);
        }

        public bool UpdateVersion(out string previousVersion)
        {
            string deviceFamilyVersion = AnalyticsInfo.VersionInfo.DeviceFamilyVersion;
            ulong version = ulong.Parse(deviceFamilyVersion);
            ulong build = (version & 0x00000000FFFF0000L) >> 16;

            ulong oldMajor = (Version & 0xFFFF000000000000L) >> 48;
            ulong oldMinor = (Version & 0x0000FFFF00000000L) >> 32;
            ulong oldRevision = (Version & 0x00000000FFFF0000L) >> 16;

            ulong newMajor = (CurrentVersion & 0xFFFF000000000000L) >> 48;
            ulong newMinor = (CurrentVersion & 0x0000FFFF00000000L) >> 32;
            ulong newRevision = (CurrentVersion & 0x00000000FFFF0000L) >> 16;

            Version = CurrentVersion;
            SystemVersion = build;

            previousVersion = $"{oldMajor}.{oldMinor}.{oldRevision}";

            var oldVersion = new Version((int)oldMajor, (int)oldMinor, (int)oldRevision);
            var newVersion = new Version((int)newMajor, (int)newMinor, (int)newRevision);
            return newVersion > oldVersion;
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

        private static TranslateSettings _translate;
        public TranslateSettings Translate => _translate ??= new TranslateSettings(_local);

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

        private static PasscodeLockSettings _passcodeLock;
        public PasscodeLockSettings PasscodeLock => _passcodeLock ??= new PasscodeLockSettings();

        private static PlaybackSettings _playback;
        public PlaybackSettings Playback => _playback ??= new PlaybackSettings(_local);

        private static VoIPSettings _voip;
        public VoIPSettings VoIP => _voip ??= new VoIPSettings();

        private static int? _verbosityLevel;
        public int VerbosityLevel
        {
            get => _verbosityLevel ??= GetValueOrDefault(_local, "VerbosityLevel", Constants.DEBUG ? 4 : 2);
            set => AddOrUpdateValue(ref _verbosityLevel, _local, "VerbosityLevel", value);
        }

        private bool? _useTestDC;
        public bool UseTestDC
        {
            get => _useTestDC ??= GetValueOrDefault(_own, "UseTestDC", false);
            set => AddOrUpdateValue(ref _useTestDC, _own, "UseTestDC", value);
        }

        private long? _userId;
        public long UserId
        {
            get => _userId ??= GetValueOrDefault(_own, "UserId", 0L);
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

        private static bool? _areSmoothTransitionsEnabled;
        public bool AreSmoothTransitionsEnabled
        {
            get => _areSmoothTransitionsEnabled ??= GetValueOrDefault(_local, "AreSmoothTransitionsEnabled", true);
            set => AddOrUpdateValue(ref _areSmoothTransitionsEnabled, _local, "AreSmoothTransitionsEnabled", value);
        }

        private static bool? _areCallsAnimated;
        public bool AreCallsAnimated
        {
            get => _areCallsAnimated ??= GetValueOrDefault(_local, "AreCallsAnimated", true);
            set => AddOrUpdateValue(ref _areCallsAnimated, _local, "AreCallsAnimated", value);
        }

        private static bool? _areMaterialsEnabled;
        public bool AreMaterialsEnabled
        {
            get => _areMaterialsEnabled ??= GetValueOrDefault(_local, "AreMaterialsEnabled", true);
            set => AddOrUpdateValue(ref _areMaterialsEnabled, _local, "AreMaterialsEnabled", value);
        }

        private static bool? _isTrayVisible;
        public bool IsTrayVisible
        {
            get => _isTrayVisible ??= GetValueOrDefault(_local, "IsTrayVisible", Constants.RELEASE);
            set => AddOrUpdateValue(ref _isTrayVisible, _local, "IsTrayVisible", value);
        }

        private static bool? _isLaunchMinimized;
        public bool IsLaunchMinimized
        {
            get => _isLaunchMinimized ??= GetValueOrDefault(_local, "IsLaunchMinimized", false);
            set => AddOrUpdateValue(ref _isLaunchMinimized, _local, "IsLaunchMinimized", value);
        }

        private static bool? _hideArchivedChats;
        public bool HideArchivedChats
        {
            get => _hideArchivedChats ??= GetValueOrDefault(_local, "HideArchivedChats", false);
            set => AddOrUpdateValue(ref _hideArchivedChats, _local, "HideArchivedChats", value);
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
                        _accountsSelectorOrder = Array.Empty<int>();
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

        private static int? _lastProxyId;
        public int LastProxyId
        {
            get => _lastProxyId ??= GetValueOrDefault(_own, "LastProxyId", -1);
            set => AddOrUpdateValue(ref _lastProxyId, _own, "LastProxyId", value);
        }

        private static bool? _isLeftTabsEnabled;
        public bool IsLeftTabsEnabled
        {
            get => _isLeftTabsEnabled ??= GetValueOrDefault(_local, "IsLeftTabsEnabled", false);
            set => AddOrUpdateValue(ref _isLeftTabsEnabled, _local, "IsLeftTabsEnabled", value);
        }

        private static bool? _swipeToShare;
        public bool SwipeToShare
        {
            get => _swipeToShare ??= GetValueOrDefault(_local, "SwipeToShare", true);
            set => AddOrUpdateValue(ref _swipeToShare, _local, "SwipeToShare", value);
        }

        private static bool? _swipeToReply;
        public bool SwipeToReply
        {
            get => _swipeToReply ??= GetValueOrDefault(_local, "SwipeToReply", true);
            set => AddOrUpdateValue(ref _swipeToReply, _local, "SwipeToReply", value);
        }

        private static bool? _fullScreenGallery;
        public bool FullScreenGallery
        {
            get => _fullScreenGallery ??= GetValueOrDefault(_local, "FullScreenGallery", false);
            set => AddOrUpdateValue(ref _fullScreenGallery, _local, "FullScreenGallery", value);
        }

        private static bool? _disableHighlightWords;
        public bool UseSystemSpellChecker
        {
            get => !(_disableHighlightWords ??= GetValueOrDefault(_local, "DisableHighlightWords", false));
            set => AddOrUpdateValue(ref _disableHighlightWords, _local, "DisableHighlightWords", !value);
        }

        private static bool? _isSendByEnterEnabled;
        public bool IsSendByEnterEnabled
        {
            get => _isSendByEnterEnabled ??= GetValueOrDefault(_local, "IsSendByEnterEnabled", true);
            set => AddOrUpdateValue(ref _isSendByEnterEnabled, _local, "IsSendByEnterEnabled", value);
        }

        private bool? _isReplaceEmojiEnabled;
        public bool IsReplaceEmojiEnabled
        {
            get => _isReplaceEmojiEnabled ??= GetValueOrDefault("IsReplaceEmojiEnabled", true);
            set => AddOrUpdateValue(ref _isReplaceEmojiEnabled, "IsReplaceEmojiEnabled", value);
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

        private static bool? _isAutoPlayAnimationsEnabled;
        public bool AutoPlayAnimations
        {
            get => _isAutoPlayAnimationsEnabled ??= GetValueOrDefault("IsAutoPlayEnabled", true);
            set => AddOrUpdateValue(ref _isAutoPlayAnimationsEnabled, "IsAutoPlayEnabled", value);
        }

        private static bool? _isAutoPlayVideosEnabled;
        public bool AutoPlayVideos
        {
            get => _isAutoPlayVideosEnabled ??= GetValueOrDefault("IsAutoPlayVideosEnabled", true);
            set => AddOrUpdateValue(ref _isAutoPlayVideosEnabled, "IsAutoPlayVideosEnabled", value);
        }

        private static bool? _autoPlayStickers;
        public bool AutoPlayStickers
        {
            get => _autoPlayStickers ??= GetValueOrDefault("AutoPlayStickers", true);
            set => AddOrUpdateValue(ref _autoPlayStickers, "AutoPlayStickers", value);
        }

        private static bool? _autoPlayStickersInChats;
        public bool AutoPlayStickersInChats
        {
            get => _autoPlayStickersInChats ??= GetValueOrDefault("AutoPlayStickersInChats", true);
            set => AddOrUpdateValue(ref _autoPlayStickersInChats, "AutoPlayStickersInChats", value);
        }

        private static bool? _autoPlayEmoji;
        public bool AutoPlayEmoji
        {
            get => _autoPlayEmoji ??= GetValueOrDefault("AutoPlayEmoji", true);
            set => AddOrUpdateValue(ref _autoPlayEmoji, "AutoPlayEmoji", value);
        }

        private static bool? _autoPlayEmojiInChats;
        public bool AutoPlayEmojiInChats
        {
            get => _autoPlayEmojiInChats ??= GetValueOrDefault("AutoPlayEmojiInChats", true);
            set => AddOrUpdateValue(ref _autoPlayEmojiInChats, "AutoPlayEmojiInChats", value);
        }

        private static bool? _isPowerSavingEnabled;
        public bool IsPowerSavingEnabled
        {
            get => _isPowerSavingEnabled ??= GetValueOrDefault("IsPowerSavingEnabled", true);
            set => AddOrUpdateValue(ref _isPowerSavingEnabled, "IsPowerSavingEnabled", value);
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

        private bool? _isDownloadFolderEnabled;
        public bool IsDownloadFolderEnabled
        {
            get => _isDownloadFolderEnabled ??= GetValueOrDefault("IsDownloadFolderEnabled", true);
            set => AddOrUpdateValue(ref _isDownloadFolderEnabled, "IsDownloadFolderEnabled", value);
        }

        private static double? _volumeLevel;
        public double VolumeLevel
        {
            get => _volumeLevel ??= GetValueOrDefault("VolumeLevel", 1d);
            set => AddOrUpdateValue(ref _volumeLevel, "VolumeLevel", value);
        }

        private static bool? _volumeMuted;
        public bool VolumeMuted
        {
            get => _volumeMuted ??= GetValueOrDefault("VolumeMuted", false);
            set => AddOrUpdateValue(ref _volumeMuted, "VolumeMuted", value);
        }

        private static Vector2? _pencil;
        public Vector2 Pencil
        {
            get
            {
                if (_pencil == null)
                {
                    var offset = GetValueOrDefault(_local, "PencilOffset", 0f);
                    var thickness = GetValueOrDefault(_local, "PencilThickness", 0.22f);

                    _pencil = new Vector2(offset, thickness);
                }

                return _pencil ?? new Vector2(0f, 0.22f);
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

        private string _languageBaseId;
        public string LanguageBaseId
        {
            get => _languageBaseId ??= GetValueOrDefault(_local, "LanguageBaseId", ApplicationLanguages.Languages[0].Split('-').First());
            set => AddOrUpdateValue(ref _languageBaseId, _local, "LanguageBaseId", value);
        }

        private string _languageShownId;
        public string LanguageShownId
        {
            get => _languageShownId ??= GetValueOrDefault<string>(_local, "LanguageShownId", null);
            set => AddOrUpdateValue(ref _languageShownId, _local, "LanguageShownId", value);
        }

        private static bool? _installBetaUpdates;
        public bool InstallBetaUpdates
        {
            get => _installBetaUpdates ??= GetValueOrDefault("InstallBetaUpdates", true);
            set => AddOrUpdateValue(ref _installBetaUpdates, _local, "InstallBetaUpdates", value);
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
            var useTestDC = UseTestDC;

            _container.Values.Clear();

            _own?.Values.Clear();
            _local?.Values.Remove($"User{UserId}");

            UseTestDC = useTestDC;
        }
    }

    public partial class ChatSettingsBase : SettingsServiceBase
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
            var setting = ConvertToKey(chatId, threadId, key);
            if (_container.Values.TryGet(setting, out value))
            {
                _container.Values.Remove(setting);
                return true;
            }

            value = default;
            return false;
        }

        public bool TryGet<T>(long chatId, long threadId, ChatSetting key, out T value)
        {
            var setting = ConvertToKey(chatId, threadId, key);
            return _container.Values.TryGet(setting, out value);
        }

        public T GetValueOrDefault<T>(long chatId, long threadId, ChatSetting key, T defaultValue)
        {
            var setting = ConvertToKey(chatId, threadId, key);
            if (_container.Values.TryGet(setting, out T value))
            {
                return value;
            }

            return defaultValue;
        }

        public void Clear(long chatId, long threadId)
        {
            var setting1 = ConvertToKey(chatId, threadId, ChatSetting.ReadInboxMaxId);
            var setting2 = ConvertToKey(chatId, threadId, ChatSetting.Index);
            var setting3 = ConvertToKey(chatId, threadId, ChatSetting.Pixel);

            _container.Values.Remove(setting1);
            _container.Values.Remove(setting2);
            _container.Values.Remove(setting3);
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
        ReadInboxMaxId,
        IsTranslating
    }
}
