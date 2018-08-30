using System;
using Unigram.Common;
using Unigram.Services;
using Windows.Storage;
using Windows.UI.Xaml;

namespace Unigram.Services
{
    public interface ISettingsService
    {
        int Session { get; }
        int Version { get; }

        void UpdateVersion();

        NotificationsSettings Notifications { get; }
        StickersSettings Stickers { get; }
        AppearanceSettings Appearance { get; }

        bool IsWorkModeVisible { get; set; }
        bool IsWorkModeEnabled { get; set; }

        string FilesDirectory { get; set; }
        int FilesTtl { get; set; }

        int VerbosityLevel { get; }

        bool IsSendByEnterEnabled { get; set; }
        bool IsReplaceEmojiEnabled { get; set; }
        bool IsContactsSyncEnabled { get; set; }
        bool IsSecretPreviewsEnabled { get; set; }
        bool IsAutoPlayEnabled { get; set; }
        bool IsSendGrouped { get; set; }

        string NotificationsToken { get; set; }

        int SelectedBackground { get; set; }
        int SelectedColor { get; set; }

        int PeerToPeerMode { get; set; }
        libtgvoip.DataSavingMode UseLessData { get; set; }
    }
}

namespace Unigram.Common
{
    public class ApplicationSettingsBase
    {
        protected readonly ApplicationDataContainer _container;

        public ApplicationSettingsBase(ApplicationDataContainer container = null)
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

        public void Clear()
        {
            _container.Values.Clear();
        }
    }

    public class ApplicationSettings : ApplicationSettingsBase, ISettingsService
    {
        private static ApplicationSettings _current;
        public static ApplicationSettings Current
        {
            get
            {
                if (_current == null)
                    _current = new ApplicationSettings();

                return _current;
            }
        }

        private readonly int _session;

        private ApplicationSettings()
        {

        }

        public ApplicationSettings(int session)
            : base(session > 0 ? ApplicationData.Current.LocalSettings.CreateContainer(session.ToString(), ApplicationDataCreateDisposition.Always) : null)
        {
            _session = session;
        }

        #region App version

        public const int CurrentVersion = 1816050;
        public const string CurrentChangelog = "New in version 1.8.1605:\r\n- You can now resize chats list width by dragging it with your mouse.\r\n- It is now possible to setup HTTP proxies.";

        public int Session => _session;

        private int? _version;
        public int Version
        {
            get
            {
                if (_version == null)
                    _version = GetValueOrDefault("AppVersion", 0);

                return _version ?? 0;
            }
            private set
            {
                _version = value;
                AddOrUpdateValue("AppVersion", value);
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

        private StickersSettings _stickers;
        public StickersSettings Stickers
        {
            get
            {
                return _stickers = _stickers ?? new StickersSettings(_container);
            }
        }

        private AppearanceSettings _appearance;
        public AppearanceSettings Appearance
        {
            get
            {
                return _appearance = _appearance ?? new AppearanceSettings();
            }
        }

        private bool? _isWorkModeVisible;
        public bool IsWorkModeVisible
        {
            get
            {
                if (_isWorkModeVisible == null)
                    _isWorkModeVisible = GetValueOrDefault("IsWorkModeVisible", false);

                return _isWorkModeVisible ?? false;
            }
            set
            {
                _isWorkModeVisible = value;
                AddOrUpdateValue("IsWorkModeVisible", value);
            }
        }

        private bool? _isWorkModeEnabled;
        public bool IsWorkModeEnabled
        {
            get
            {
                if (_isWorkModeEnabled == null)
                    _isWorkModeEnabled = GetValueOrDefault("IsWorkModeEnabled", false);

                return _isWorkModeEnabled ?? false;
            }
            set
            {
                _isWorkModeEnabled = value;
                AddOrUpdateValue("IsWorkModeEnabled", value);
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

        private int? _filesTtl;
        public int FilesTtl
        {
            get
            {
                if (_filesTtl == null)
                    _filesTtl = GetValueOrDefault("FilesTtl", 0);

                return _filesTtl ?? 0;
            }
            set
            {
                _filesTtl = value;
                AddOrUpdateValue("FilesTtl", value);
            }
        }

        private int? _verbosityLevel;
        public int VerbosityLevel
        {
            get
            {
                if (_verbosityLevel == null)
#if DEBUG
                    _verbosityLevel = GetValueOrDefault("VerbosityLevel", 5);

                return _verbosityLevel ?? 5;
#else
                    _verbosityLevel = GetValueOrDefault("VerbosityLevel", 0);

                return _verbosityLevel ?? 0;
#endif
            }
            set
            {
                _verbosityLevel = value;
                AddOrUpdateValue("VerbosityLevel", value);
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

        private bool? _isSecretPreviewsEnabled;
        public bool IsSecretPreviewsEnabled
        {
            get
            {
                if (_isSecretPreviewsEnabled == null)
                    _isSecretPreviewsEnabled = GetValueOrDefault("IsSecretPreviewsEnabled", true);

                return _isSecretPreviewsEnabled ?? true;
            }
            set
            {
                _isSecretPreviewsEnabled = value;
                AddOrUpdateValue("IsSecretPreviewsEnabled", value);
            }
        }

        private bool? _isAutoPlayEnabled;
        public bool IsAutoPlayEnabled
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

        private int? _selectedAccount;
        public int SelectedAccount
        {
            get
            {
                if (_selectedAccount == null)
                    _selectedAccount = GetValueOrDefault("SelectedAccount", 0);

                return _selectedAccount ?? 0;
            }
            set
            {
                _selectedAccount = value;
                AddOrUpdateValue("SelectedAccount", value);
            }
        }

        private string _notificationsToken;
        public string NotificationsToken
        {
            get
            {
                if (_notificationsToken == null)
                    _notificationsToken = GetValueOrDefault<string>("ChannelUri", null);

                return _notificationsToken;
            }
            set
            {
                _notificationsToken = value;
                AddOrUpdateValue("ChannelUri", value);
            }
        }

        private int? _selectedBackground;
        public int SelectedBackground
        {
            get
            {
                if (_selectedBackground == null)
                    _selectedBackground = GetValueOrDefault("SelectedBackground", 1000001);

                return _selectedBackground ?? 1000001;
            }
            set
            {
                _selectedBackground = value;
                AddOrUpdateValue("SelectedBackground", value);
            }
        }

        private int? _selectedColor;
        public int SelectedColor
        {
            get
            {
                if (_selectedColor == null)
                    _selectedColor = GetValueOrDefault("SelectedColor", 0);

                return _selectedColor ?? 0;
            }
            set
            {
                _selectedColor = value;
                AddOrUpdateValue("SelectedColor", value);
            }
        }

        private int? _peerToPeerMode;
        public int PeerToPeerMode
        {
            get
            {
                if (_peerToPeerMode == null)
                    _peerToPeerMode = GetValueOrDefault("PeerToPeerMode", 1);

                return _peerToPeerMode ?? 1;
            }
            set
            {
                _peerToPeerMode = value;
                AddOrUpdateValue("PeerToPeerMode", value);
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

        public void CleanUp()
        {
            // Here should be cleaned up all the settings that are shared with background tasks.
            _peerToPeerMode = null;
            _useLessData = null;
        }
    }

    public class NotificationsSettings : ApplicationSettingsBase
    {
        public NotificationsSettings(ApplicationDataContainer container)
            : base(container)
        {

        }

        private bool? _inAppPreview;
        public bool InAppPreview
        {
            get
            {
                if (_inAppPreview == null)
                    _inAppPreview = GetValueOrDefault("InAppPreview", true);

                return _inAppPreview ?? true;
            }
            set
            {
                _inAppPreview = value;
                AddOrUpdateValue("InAppPreview", value);
            }
        }

        private bool? _inAppVibrate;
        public bool InAppVibrate
        {
            get
            {
                if (_inAppVibrate == null)
                    _inAppVibrate = GetValueOrDefault("InAppVibrate", true);

                return _inAppVibrate ?? true;
            }
            set
            {
                _inAppVibrate = value;
                AddOrUpdateValue("InAppVibrate", value);
            }
        }

        private bool? _inAppSounds;
        public bool InAppSounds
        {
            get
            {
                if (_inAppSounds == null)
                    _inAppSounds = GetValueOrDefault("InAppSounds", true);

                return _inAppSounds ?? true;
            }
            set
            {
                _inAppSounds = value;
                AddOrUpdateValue("InAppSounds", value);
            }
        }

        private bool? _includeMutedChats;
        public bool IncludeMutedChats
        {
            get
            {
                if (_includeMutedChats == null)
                    _includeMutedChats = GetValueOrDefault("IncludeMutedChats", false);

                return _includeMutedChats ?? false;
            }
            set
            {
                _includeMutedChats = value;
                AddOrUpdateValue("IncludeMutedChats", value);
            }
        }
    }

    public class AppearanceSettings : ApplicationSettingsBase
    {
        public AppearanceSettings()
            : base(ApplicationData.Current.LocalSettings.CreateContainer("Theme", ApplicationDataCreateDisposition.Always))
        {

        }

        private TelegramTheme? _currentTheme;
        public TelegramTheme CurrentTheme
        {
            get
            {
                if (_currentTheme == null)
                    _currentTheme = RequestedTheme;

                return _currentTheme ?? (TelegramTheme.Default | TelegramTheme.Brand);
            }
        }

        private TelegramTheme? _requestedTheme;
        public TelegramTheme RequestedTheme
        {
            get
            {
                if (_requestedTheme == null)
                {
                    _requestedTheme = (TelegramTheme)GetValueOrDefault(_container, "Theme", (int)(TelegramTheme.Default | TelegramTheme.Brand));
                    _currentTheme = _requestedTheme;
                }

                return _requestedTheme ?? (TelegramTheme.Default | TelegramTheme.Brand);
            }
            set
            {
                _requestedTheme = value;
                AddOrUpdateValue(_container, "Theme", (int)value);
            }
        }

    }

    public class StickersSettings : ApplicationSettingsBase
    {
        public StickersSettings(ApplicationDataContainer container)
            : base(container)
        {

        }

        private int? _suggestionMode;
        public StickersSuggestionMode SuggestionMode
        {
            get
            {
                if (_suggestionMode == null)
                    _suggestionMode = GetValueOrDefault("SuggestionMode", 0);

                return (StickersSuggestionMode)(_suggestionMode ?? 0);
            }
            set
            {
                _suggestionMode = (int)value;
                AddOrUpdateValue("SuggestionMode", (int)value);
            }
        }
    }

    public enum StickersSuggestionMode
    {
        All,
        Installed,
        None
    }

    [Flags]
    public enum TelegramTheme
    {
        Default = 1 << 0,
        Light = 1 << 1,
        Dark = 1 << 2,

        Brand = 1 << 3,
        Custom = 1 << 4,
    }
}
