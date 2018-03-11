using System;
using Windows.Storage;
using Windows.UI.Xaml;

namespace Unigram.Common
{
    public class ApplicationSettingsBase
    {
        protected readonly ApplicationDataContainer isolatedStore;

        public ApplicationSettingsBase(ApplicationDataContainer container = null)
        {
            isolatedStore = container ?? ApplicationData.Current.LocalSettings;
        }

        public bool AddOrUpdateValue(string key, Object value)
        {
            bool valueChanged = false;

            if (isolatedStore.Values.ContainsKey(key))
            {
                if (isolatedStore.Values[key] != value)
                {
                    isolatedStore.Values[key] = value;
                    valueChanged = true;
                }
            }
            else
            {
                isolatedStore.Values.Add(key, value);
                valueChanged = true;
            }

            return valueChanged;
        }

        public valueType GetValueOrDefault<valueType>(string key, valueType defaultValue)
        {
            valueType value;

            if (isolatedStore.Values.ContainsKey(key))
            {
                value = (valueType)isolatedStore.Values[key];
            }
            else
            {
                value = defaultValue;
            }

            return value;
        }

        public void Clear()
        {
            isolatedStore.Values.Clear();
        }
    }

    public class ApplicationSettings : ApplicationSettingsBase
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

        #region App version

        public const int CurrentVersion = 1014530;
        public const string CurrentChangelog = "Ladies and gentlemen, please welcome Unigram X.\r\nSince the beginning of the year we're working closely with TDLib creators to deliver you the best Telegram experience on Windows 10.\r\nWe're proud to announce that the wait is over, and the app has been rewritten from scratch to guarantee you performance, stability and security.\r\n\r\nHere's what we've done so far:\r\n- Every chat you open is now securely cached on your device, so you can browse them even when internet connection isn't available.\r\n- Secret chats are now fully supported: this allows you to communicate with your friends using end-to-end encryption, even on desktop.\r\n- My People integration: you can now pin your contacts to the Windows taskbar and use Unigram to chat with them without opening the full app.\r\n- The app is now faster, stronger, better.\r\n\r\nSome of the features available in the old app might be unavailable at the moment, but they're going to come soon.\r\nIf something important for you is missing in the app don't esitate to contact us and we'll work to bring it to you as soon as possible!";

        private int? _appVersion;
        public int Version
        {
            get
            {
                if (_appVersion == null)
                    _appVersion = GetValueOrDefault("AppVersion", 0);

                return _appVersion ?? 0;
            }
            set
            {
                _appVersion = value;
                AddOrUpdateValue("AppVersion", value);
            }
        }

        #endregion

        private ProxySettings _proxy;
        public ProxySettings Proxy
        {
            get
            {
                return _proxy = _proxy ?? new ProxySettings();
            }
        }

        private NotificationsSettings _notifications;
        public NotificationsSettings Notifications
        {
            get
            {
                return _notifications = _notifications ?? new NotificationsSettings();
            }
        }

        private ElementTheme? _currentTheme;
        public ElementTheme CurrentTheme
        {
            get
            {
                if (_currentTheme == null)
                    _currentTheme = RequestedTheme;

                return _currentTheme ?? ElementTheme.Default;
            }
        }

        private ElementTheme? _requestedTheme;
        public ElementTheme RequestedTheme
        {
            get
            {
                if (_requestedTheme == null)
                {
                    _requestedTheme = (ElementTheme)GetValueOrDefault("RequestedTheme", (int)ElementTheme.Default);
                    _currentTheme = _requestedTheme;
                }

                return _requestedTheme ?? ElementTheme.Default;
            }
            set
            {
                _requestedTheme = value;
                AddOrUpdateValue("RequestedTheme", (int)value);
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

    public class ProxySettings : ApplicationSettingsBase
    {
        public void CleanUp()
        {
            _isEnabled = null;
            _isCallsEnabled = null;
            _server = null;
            _port = null;
            _username = null;
            _password = null;
        }

        private bool? _isEnabled;
        public bool IsEnabled
        {
            get
            {
                if (_isEnabled == null)
                    _isEnabled = GetValueOrDefault("ProxyEnabled", false);

                return _isEnabled ?? false;
            }
            set
            {
                _isEnabled = value;
                AddOrUpdateValue("ProxyEnabled", value);
            }
        }

        private bool? _isCallsEnabled;
        public bool IsCallsEnabled
        {
            get
            {
                if (_isCallsEnabled == null)
                    _isCallsEnabled = GetValueOrDefault("CallsProxyEnabled", false);

                return _isCallsEnabled ?? false;
            }
            set
            {
                _isCallsEnabled = value;
                AddOrUpdateValue("CallsProxyEnabled", value);
            }
        }

        private string _server;
        public string Server
        {
            get
            {
                if (_server == null)
                    _server = GetValueOrDefault<string>("ProxyServer", null);

                return _server;
            }
            set
            {
                _server = value;
                AddOrUpdateValue("ProxyServer", value);
            }
        }

        private int? _port;
        public int Port
        {
            get
            {
                if (_port == null)
                    _port = GetValueOrDefault("ProxyPort", 1080);

                return _port ?? 1080;
            }
            set
            {
                _port = value;
                AddOrUpdateValue("ProxyPort", value);
            }
        }

        private string _username;
        public string Username
        {
            get
            {
                if (_username == null)
                    _username = GetValueOrDefault<string>("ProxyUsername", null);

                return _username;
            }
            set
            {
                _username = value;
                AddOrUpdateValue("ProxyUsername", value);
            }
        }

        private string _password;
        public string Password
        {
            get
            {
                if (_password == null)
                    _password = GetValueOrDefault<string>("ProxyPassword", null);

                return _password;
            }
            set
            {
                _password = value;
                AddOrUpdateValue("ProxyPassword", value);
            }
        }

    }
}
