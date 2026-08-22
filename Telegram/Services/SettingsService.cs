//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using RLottie;
using System;
using System.Linq;
using System.Numerics;
using Telegram.Common;
using Telegram.Native.Calls;
using Telegram.Services.Settings;
using Telegram.Td.Api;
using Windows.System.Profile;
using AutoDownloadSettings = Telegram.Services.Settings.AutoDownloadSettings;

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
        VideoSettings Video { get; }
        VoIPSettings VoIP { get; }
        ToolTipSettings ToolTip { get; }
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
        bool IsContactsSortedByEpoch { get; set; }
        bool IsSecretPreviewsEnabled { get; set; }
        bool AutoPlayAnimations { get; set; }
        bool AutoPlayVideos { get; set; }
        bool IsAccountsSelectorExpanded { get; set; }
        bool IsAllAccountsNotifications { get; set; }
        bool AreSmoothTransitionsEnabled { get; set; }
        bool AreMaterialsEnabled { get; set; }

        bool UseSystemProxy { get; set; }
        int LastProxyId { get; set; }
        int EnabledProxyId { get; set; }
        bool MigratedProxy { get; set; }

        int[] AccountsSelectorOrder { get; set; }

        bool UseLeftTabsForChats { get; set; }
        bool UseLeftTabsForForums { get; set; }

        Vector2 Pencil { get; set; }

        DistanceUnits DistanceUnits { get; set; }

        bool SwipeToShare { get; set; }
        bool SwipeToReply { get; set; }
        bool SwipeToGoBack { get; set; }
        bool FullScreenGallery { get; set; }
        bool UseSystemSpellChecker { get; set; }

        bool SendLargePhotos { get; set; }

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

        void Initialize();
    }

    public enum DistanceUnits
    {
        Automatic,
        Kilometers,
        Miles
    }

    public partial class SettingsServiceBase
    {
        protected readonly ISettingsStore _container;

        public SettingsServiceBase(string key)
            : this(ApplicationDataSettingsStore.Local.GetContainer(key))
        {

        }

        public SettingsServiceBase(ISettingsStore container = null)
        {
            _container = container ?? ApplicationDataSettingsStore.Local;
        }

        public void AddOrUpdateValue(string key, object value)
        {
            AddOrUpdateValue(_container, key, value);
        }

        public void AddOrUpdateValue<T>(ref T storage, string key, T value)
        {
            storage = value;
            AddOrUpdateValue(_container, key, value);
        }

        protected void AddOrUpdateValue<T>(ref T storage, ISettingsStore container, string key, T value)
        {
            storage = value;
            AddOrUpdateValue(container, key, value);
        }

        protected void AddOrUpdateValue(ISettingsStore container, string key, object value)
        {
            container.SetValue(key, value);
        }

        public valueType GetValueOrDefault<valueType>(string key, valueType defaultValue)
        {
            return GetValueOrDefault(_container, key, defaultValue);
        }

        protected valueType GetValueOrDefault<valueType>(ISettingsStore container, string key, valueType defaultValue)
        {
            return TryGetValue(container, key, out valueType value) ? value : defaultValue;
        }

        public bool TryGetValue<T>(string key, out T value)
        {
            return TryGetValue(_container, key, out value);
        }

        // A value stored as the wrong type used to throw on the cast. Falling back to the default
        // is the safer answer for something read on the way up.
        protected static bool TryGetValue<T>(ISettingsStore container, string key, out T value)
        {
            if (container.TryGetValue(key, out object stored) && stored is T result)
            {
                value = result;
                return true;
            }

            value = default;
            return false;
        }

        public virtual void Clear()
        {
            _container.Clear();
        }
    }

    public partial class SettingsService : SettingsServiceBase, ISettingsService
    {
        private static SettingsService _current;
        public static SettingsService Current => _current ??= new SettingsService();

        private readonly int _session;
        private readonly ISettingsStore _local;
        private readonly ISettingsStore _own;

        private SettingsService()
        {
            _local = ApplicationDataSettingsStore.Local;
        }

        public SettingsService(int session)
            : base(ApplicationDataSettingsStore.Local.GetContainer($"{session}"))
        {
            _session = session;
            _local = ApplicationDataSettingsStore.Local;
            _own = _container;
        }

        // LifetimeService discovers sessions from the folders on disk, and creates them, before
        // any SettingsService exists for them: ClientService reads UseTestDC while it is being
        // constructed, so the value has to be in the container first.
        public static bool IsAuthorized(int session)
        {
            return ApplicationDataSettingsStore.Local.TryGetContainer($"{session}", out var container)
                && container.ContainsKey("UserId");
        }

        public static void SetUseTestDC(int session, bool value)
        {
            ApplicationDataSettingsStore.Local.GetContainer($"{session}").SetValue("UseTestDC", value);
        }

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

        private ISettingsStore _autoDownloadStore;
        private ISettingsStore AutoDownloadStore => _autoDownloadStore ??= _own.GetContainer("AutoDownload");

        private AutoDownloadSettings _autoDownload;
        public AutoDownloadSettings AutoDownload
        {
            get => _autoDownload ??= new AutoDownloadSettings(AutoDownloadStore);
            set
            {
                _autoDownload = value ?? AutoDownloadSettings.Default;
                _autoDownload.Save(AutoDownloadStore);
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

        private VideoSettings _video;
        public VideoSettings Video => _video ??= new VideoSettings(_own);

        private static VoIPSettings _voip;
        public VoIPSettings VoIP => _voip ??= new VoIPSettings();

        private static ToolTipSettings _toolTip;
        public ToolTipSettings ToolTip => _toolTip ??= new ToolTipSettings();

        private static int? _verbosityLevel;
        public int VerbosityLevel
        {
            get => _verbosityLevel ??= GetValueOrDefault(_local, "VerbosityLevel", ApiInfo.IsPackagedRelease ? 4 : 2);
            set => AddOrUpdateValue(ref _verbosityLevel, _local, "VerbosityLevel", value);
        }

        private bool? _useTestDC;
        public bool UseTestDC
        {
            get => _useTestDC ??= GetValueOrDefault(_own, "UseTestDC", false);
            set => AddOrUpdateValue(ref _useTestDC, _own, "UseTestDC", value);
        }

        private long? _userId;

        // The setter also maintains the root User{id} -> session index that LifetimeService reads.
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
            get => (DistanceUnits)(_distanceUnits ??= GetValueOrDefault(_local, "DistanceUnits", 0));
            set => AddOrUpdateValue(ref _distanceUnits, _local, "DistanceUnits", (int)value);
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

        private bool? _useSystemProxy;
        public bool UseSystemProxy
        {
            get => _useSystemProxy ??= GetValueOrDefault(_own, "UseSystemProxy", true);
            set => AddOrUpdateValue(ref _useSystemProxy, _own, "UseSystemProxy", value);
        }

        private int? _lastProxyId;
        public int LastProxyId
        {
            get => _lastProxyId ??= GetValueOrDefault(_own, "LastProxyId", -1);
            set => AddOrUpdateValue(ref _lastProxyId, _own, "LastProxyId", value);
        }

        private static bool? _useLeftTabsForChats;
        public bool UseLeftTabsForChats
        {
            get => _useLeftTabsForChats ??= GetValueOrDefault(_local, "IsLeftTabsEnabled", true);
            set => AddOrUpdateValue(ref _useLeftTabsForChats, _local, "IsLeftTabsEnabled", value);
        }

        private static bool? _useLeftTabsForForums;
        public bool UseLeftTabsForForums
        {
            get => _useLeftTabsForForums ??= GetValueOrDefault(_local, "UseLeftTabsForForums", false);
            set => AddOrUpdateValue(ref _useLeftTabsForForums, _local, "UseLeftTabsForForums", value);
        }

        private static bool? _swipeToShare;
        public bool SwipeToShare
        {
            get => _swipeToShare ??= GetValueOrDefault(_local, "SwipeToShare", false);
            set => AddOrUpdateValue(ref _swipeToShare, _local, "SwipeToShare", value);
        }

        private static bool? _swipeToReply;
        public bool SwipeToReply
        {
            get => _swipeToReply ??= GetValueOrDefault(_local, "SwipeToReply", true);
            set => AddOrUpdateValue(ref _swipeToReply, _local, "SwipeToReply", value);
        }

        private static bool? _swipeToGoBack;
        public bool SwipeToGoBack
        {
            get => _swipeToGoBack ??= GetValueOrDefault(_local, "SwipeToGoBack", true);
            set => AddOrUpdateValue(ref _swipeToGoBack, _local, "SwipeToGoBack", value);
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

        private static bool? _isReplaceEmojiEnabled;
        public bool IsReplaceEmojiEnabled
        {
            get => _isReplaceEmojiEnabled ??= GetValueOrDefault(_local, "IsReplaceEmojiEnabled", true);
            set => AddOrUpdateValue(ref _isReplaceEmojiEnabled, _local, "IsReplaceEmojiEnabled", value);
        }

        private static bool? _isContactsSortedByEpoch;
        public bool IsContactsSortedByEpoch
        {
            get => _isContactsSortedByEpoch ??= GetValueOrDefault(_local, "IsContactsSortedByEpoch", true);
            set => AddOrUpdateValue(ref _isContactsSortedByEpoch, _local, "IsContactsSortedByEpoch", value);
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
            get => _isAutoPlayAnimationsEnabled ??= GetValueOrDefault(_local, "IsAutoPlayEnabled", true);
            set => AddOrUpdateValue(ref _isAutoPlayAnimationsEnabled, _local, "IsAutoPlayEnabled", value);
        }

        private static bool? _isAutoPlayVideosEnabled;
        public bool AutoPlayVideos
        {
            get => _isAutoPlayVideosEnabled ??= GetValueOrDefault(_local, "IsAutoPlayVideosEnabled", true);
            set => AddOrUpdateValue(ref _isAutoPlayVideosEnabled, _local, "IsAutoPlayVideosEnabled", value);
        }

        private static bool? _autoPlayStickers;
        public bool AutoPlayStickers
        {
            get => _autoPlayStickers ??= GetValueOrDefault(_local, "AutoPlayStickers", true);
            set => AddOrUpdateValue(ref _autoPlayStickers, _local, "AutoPlayStickers", value);
        }

        private static bool? _autoPlayStickersInChats;
        public bool AutoPlayStickersInChats
        {
            get => _autoPlayStickersInChats ??= GetValueOrDefault(_local, "AutoPlayStickersInChats", true);
            set => AddOrUpdateValue(ref _autoPlayStickersInChats, _local, "AutoPlayStickersInChats", value);
        }

        private static bool? _autoPlayEmoji;
        public bool AutoPlayEmoji
        {
            get => _autoPlayEmoji ??= GetValueOrDefault(_local, "AutoPlayEmoji", true);
            set => AddOrUpdateValue(ref _autoPlayEmoji, _local, "AutoPlayEmoji", value);
        }

        private static bool? _autoPlayEmojiInChats;
        public bool AutoPlayEmojiInChats
        {
            get => _autoPlayEmojiInChats ??= GetValueOrDefault(_local, "AutoPlayEmojiInChats", true);
            set => AddOrUpdateValue(ref _autoPlayEmojiInChats, _local, "AutoPlayEmojiInChats", value);
        }

        private static bool? _isPowerSavingEnabled;
        public bool IsPowerSavingEnabled
        {
            get => _isPowerSavingEnabled ??= GetValueOrDefault(_local, "IsPowerSavingEnabled", true);
            set => AddOrUpdateValue(ref _isPowerSavingEnabled, _local, "IsPowerSavingEnabled", value);
        }

        private bool? _sendLargePhotos;
        public bool SendLargePhotos
        {
            get => _sendLargePhotos ??= Diagnostics.GetValueOrDefault("SendLargePhotos", false);
            set => Diagnostics.AddOrUpdateValue(ref _sendLargePhotos, "SendLargePhotos", value);
        }

        private bool? _isStreamingEnabled;
        public bool IsStreamingEnabled
        {
            get => _isStreamingEnabled ??= GetValueOrDefault(_local, "IsStreamingEnabled", true);
            set => AddOrUpdateValue(ref _isStreamingEnabled, _local, "IsStreamingEnabled", value);
        }

        private bool? _isDownloadFolderEnabled;
        public bool IsDownloadFolderEnabled
        {
            get => _isDownloadFolderEnabled ??= GetValueOrDefault(_local, "IsDownloadFolderEnabled", true);
            set => AddOrUpdateValue(ref _isDownloadFolderEnabled, _local, "IsDownloadFolderEnabled", value);
        }

        private static double? _volumeLevel;
        public double VolumeLevel
        {
            get => _volumeLevel ??= GetValueOrDefault(_local, "VolumeLevel", 1d);
            set => AddOrUpdateValue(ref _volumeLevel, _local, "VolumeLevel", value);
        }

        private static bool? _volumeMuted;
        public bool VolumeMuted
        {
            get => _volumeMuted ??= GetValueOrDefault(_local, "VolumeMuted", false);
            set => AddOrUpdateValue(ref _volumeMuted, _local, "VolumeMuted", value);
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
            get => _languagePackId ??= GetValueOrDefault(_local, "LanguagePackId", LocaleService.SystemLanguageId());
            set => AddOrUpdateValue(ref _languagePackId, _local, "LanguagePackId", value);
        }

        private string _languagePluralId;
        public string LanguagePluralId
        {
            get => _languagePluralId ??= GetValueOrDefault(_local, "LanguagePluralId", LocaleService.SystemLanguageId());
            set => AddOrUpdateValue(ref _languagePluralId, _local, "LanguagePluralId", value);
        }

        private string _languageBaseId;
        public string LanguageBaseId
        {
            get => _languageBaseId ??= GetValueOrDefault(_local, "LanguageBaseId", LocaleService.SystemLanguageId());
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
            get => _installBetaUpdates ??= GetValueOrDefault(_local, "InstallBetaUpdates", true);
            set => AddOrUpdateValue(ref _installBetaUpdates, _local, "InstallBetaUpdates", value);
        }

        private static int? _enabledProxyId;
        public int EnabledProxyId
        {
            get => _enabledProxyId ??= GetValueOrDefault(_local, "EnabledProxyId", 0);
            set => AddOrUpdateValue(ref _enabledProxyId, _local, "EnabledProxyId", value);
        }

        private static bool? _migratedProxy;
        public bool MigratedProxy
        {
            get => _migratedProxy ??= GetValueOrDefault(_local, "MigratedProxy", false);
            set => AddOrUpdateValue(ref _migratedProxy, _local, "MigratedProxy", value);
        }

        private static int? _useLessData;
        public VoipDataSaving UseLessData
        {
            get => (VoipDataSaving)(_useLessData ??= GetValueOrDefault(_local, "UseLessData", 0));
            set => AddOrUpdateValue(ref _useLessData, _local, "UseLessData", (int)value);
        }

        private static int? _reportsCount;
        public int ReportsCount
        {
            get => _reportsCount ??= GetValueOrDefault(_local, "ReportsCount", 100);
            set => AddOrUpdateValue(ref _reportsCount, _local, "ReportsCount", value);
        }

        private static long? _reportsDate;
        public DateTime ReportsDate
        {
            get => DateTime.FromFileTimeUtc(_reportsDate ??= GetValueOrDefault(_local, "ReportsDate", DateTime.Now.ToFileTimeUtc()));
            set => AddOrUpdateValue(ref _reportsDate, _local, "ReportsDate", value.ToFileTimeUtc());
        }

        private static string _anonymousUserId;
        public string AnonymousUserId
        {
            get
            {
                if (_anonymousUserId != null)
                {
                    return _anonymousUserId;
                }

                var value = GetValueOrDefault<string>(_local, "AnonymousUserId", null);
                if (value == null)
                {
                    value = Guid.NewGuid().ToString();
                    AddOrUpdateValue(_local, "AnonymousUserId", value);
                }

                return _anonymousUserId = value;
            }
            set => AddOrUpdateValue(ref _anonymousUserId, _local, "AnonymousUserId", value);
        }

        private ISettingsStore _pinnedMessages;
        private ISettingsStore PinnedMessages => _pinnedMessages ??= _own.GetContainer("PinnedMessages");

        public void SetChatPinnedMessage(long chatId, long messageId)
        {
            AddOrUpdateValue(PinnedMessages, $"{chatId}", messageId);
        }

        public long GetChatPinnedMessage(long chatId)
        {
            return GetValueOrDefault(PinnedMessages, $"{chatId}", 0L);
        }

        public void CleanUp()
        {
            // Here should be cleaned up all the settings that are shared with background tasks.
            //_useLessData = null;
        }

        public new void Clear()
        {
            var useTestDC = UseTestDC;

            _local.Remove($"User{UserId}");

            // Values.Clear() leaves the sub-containers behind, so auto-download, video positions
            // and pinned messages would outlive the account they belong to.
            _own.DeleteContainer("AutoDownload");
            _own.DeleteContainer("Video");
            _own.DeleteContainer("PinnedMessages");
            _own.Clear();

            ResetCache();

            UseTestDC = useTestDC;
            _own.Flush();
        }

        // Every value is cached on first read, and a log out is followed by a log in on the same
        // session id, so emptying the container is only half of it.
        private void ResetCache()
        {
            _chats = null;
            _notifications = null;
            _autoDownload = null;
            _video = null;

            // Both point at containers Clear has just deleted.
            _autoDownloadStore = null;
            _pinnedMessages = null;

            _version = null;
            _systemVersion = null;
            _useTestDC = null;
            _userId = null;
            _useSystemProxy = null;
            _lastProxyId = null;
            _isSecretPreviewsEnabled = null;
            _lastMessageTtl = null;
        }

        public void Initialize()
        {
            if (Diagnostics.LastUpdateVersion < Constants.BuildNumber)
            {
                var updateCount = Diagnostics.UpdateCount;

                Diagnostics.LastUpdateVersion = Constants.BuildNumber;
                Diagnostics.UpdateCount++;

                if (updateCount > 0)
                {
                    if (!_local.ContainsKey("IsLeftTabsEnabled"))
                    {
                        _local.SetValue("IsLeftTabsEnabled", false);
                        _useLeftTabsForChats = false;
                    }
                }
            }

            MigrateContainers();

            LottieAnimation.UseTLottie = Diagnostics.UseTLottieRenderer;
        }

        // Session 0 predates multi-account: its settings were addressed as the root container and
        // stayed there when per-account containers arrived, so the root holds the first account's
        // settings mixed in with the app-wide ones. These are the keys on the wrong side.
        private static readonly string[] _accountScopedKeys = new[]
        {
            "InAppPreview",
            "InAppVibrate",
            "InAppFlash",
            "InAppSounds",
            "ShowName",
            "ShowText",
            "ShowReply",
            "IncludeMutedChats",
            "IncludeMutedChatsInFolderCounters",
            "CountUnreadMessages",
            "IsSecretPreviewsEnabled",
            "LastMessageTtl"
        };

        // HasRemovedCollections is deliberately absent: it sits on NotificationsSettings but is an
        // app-level one-shot flag, and NotificationsService reads it through Current.
        private static readonly string[] _appScopedKeys = new[]
        {
            "IsReplaceEmojiEnabled",
            "IsContactsSortedByEpoch",
            "UseLeftTabsForForums",
            "UseLessData",
            "DistanceUnits",
            "VolumeMuted"
        };

        // Every move is a copy followed by a delete, so a second pass finds nothing to do and no
        // version marker is needed. A downgrade that writes the root again is migrated afresh,
        // which is right: the older build's value is then the newer one.
        private void MigrateContainers()
        {
            ISettingsStore zero = null;

            foreach (var key in _accountScopedKeys)
            {
                if (_local.TryGetValue(key, out object value))
                {
                    zero ??= _local.GetContainer("0");
                    zero.SetValue(key, value);
                    _local.Remove(key);
                }
            }

            // Snapshot: the loop writes into the containers it is walking.
            var names = _local.ContainerNames.ToArray();
            var active = ActiveSession;

            foreach (var name in names)
            {
                // Session 0 wrote to the root, so its copy of an app-wide key is already in place.
                if (!int.TryParse(name, out int id) || id == 0 || !_local.TryGetContainer(name, out var container))
                {
                    continue;
                }

                foreach (var key in _appScopedKeys)
                {
                    if (container.TryGetValue(key, out object value))
                    {
                        // Only one value can survive, and it is the one the user is looking at.
                        if (id == active)
                        {
                            _local.SetValue(key, value);
                        }

                        container.Remove(key);
                    }
                }
            }

            _local.Flush();
        }
    }

    public partial class ChatSettingsBase : SettingsServiceBase
    {
        public ChatSettingsBase(ISettingsStore container = null)
            : base(container)
        {
        }

        public object this[long chatId, MessageTopic topicId, ChatSetting key]
        {
            //get => GetValueOrDefault<object>(chatId + key, null);
            set => AddOrUpdateValue(ConvertToKey(chatId, topicId, key), value);
        }

        public bool TryRemove<T>(long chatId, MessageTopic topicId, ChatSetting key, out T value)
        {
            var setting = ConvertToKey(chatId, topicId, key);
            if (TryGetValue(_container, setting, out value))
            {
                _container.Remove(setting);
                return true;
            }

            value = default;
            return false;
        }

        public bool TryGet<T>(long chatId, MessageTopic topicId, ChatSetting key, out T value)
        {
            var setting = ConvertToKey(chatId, topicId, key);
            return TryGetValue(_container, setting, out value);
        }

        public T GetValueOrDefault<T>(long chatId, MessageTopic topicId, ChatSetting key, T defaultValue)
        {
            var setting = ConvertToKey(chatId, topicId, key);
            if (TryGetValue(_container, setting, out T value))
            {
                return value;
            }

            return defaultValue;
        }

        public void Clear(long chatId, MessageTopic topicId)
        {
            var setting1 = ConvertToKey(chatId, topicId, ChatSetting.ReadInboxMaxId);
            var setting2 = ConvertToKey(chatId, topicId, ChatSetting.Index);
            var setting3 = ConvertToKey(chatId, topicId, ChatSetting.Pixel);

            _container.Remove(setting1);
            _container.Remove(setting2);
            _container.Remove(setting3);
        }

        private string ConvertToKey(long chatId, MessageTopic topicId, ChatSetting setting)
        {
            return topicId switch
            {
                MessageTopicDirectMessages directMesages => $"{chatId}{directMesages.DirectMessagesChatTopicId}{setting}",
                MessageTopicForum forum => $"{chatId}{forum.ForumTopicId << 20}{setting}",
                MessageTopicSavedMessages savedMessages => $"{chatId}{savedMessages.SavedMessagesTopicId}{setting}",
                MessageTopicThread thread => $"{chatId}{thread.MessageThreadId}{setting}",
                _ => $"{chatId}{setting}"
            };
        }
    }

    public enum ChatSetting
    {
        Index,
        Pixel,
        ReadInboxMaxId,
        IsTranslating,
        PaidMessageStarCount
    }
}
