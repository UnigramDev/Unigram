using System;
using System.IO.IsolatedStorage;
using System.Diagnostics;
using System.ComponentModel;
using System.Threading;
using Windows.Storage;
using Telegram.Api.TL;
using Unigram.Core.Services;
using Telegram.Api.Services;
using Telegram.Api.TL.Account;
using Windows.UI.Xaml;

namespace Unigram.Common
{
    public class ApplicationSettings
    {
        private readonly ApplicationDataContainer isolatedStore;

        public ApplicationSettings(ApplicationDataContainer container = null)
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

        #region InApp

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

        #endregion

        #region App version

        public const int CurrentVersion = 086910;
        public const string CurrentChangelog = "- Share your location with friends in real time with the new Live Locations. \n- Control whether new members in supergroups can see earlier message history.\n- Easily recognize messages from group admins by the new �admin� badge.\n- Select and send multiple photos and videos from the attach flyout.\n- Send videos choosing compression level.\n- Launch Unigram when system starts.";

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

        private int? _contactsSavedCount;
        public int ContactsSavedCount
        {
            get
            {
                if (_contactsSavedCount == null)
                    _contactsSavedCount = GetValueOrDefault("ContactsSavedCount", 0);

                return _contactsSavedCount ?? 0;
            }
            set
            {
                _contactsSavedCount = value;
                AddOrUpdateValue("ContactsSavedCount", value);
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

        //private bool? _isPeerToPeer;
        //public bool IsPeerToPeer
        //{
        //    get
        //    {
        //        if (_isPeerToPeer == null)
        //            _isPeerToPeer = GetValueOrDefault("IsPeerToPeer", true);

        //        return _isPeerToPeer ?? true;
        //    }
        //    set
        //    {
        //        _isPeerToPeer = value;
        //        AddOrUpdateValue("IsPeerToPeer", value);
        //    }
        //}

        // This setting should not be cached or changes will be not be reflected during the session
        public bool IsPeerToPeer
        {
            get
            {
                return GetValueOrDefault("IsPeerToPeer", true);
            }
            set
            {
                AddOrUpdateValue("IsPeerToPeer", value);
            }
        }

        private TLAccountTmpPassword _tmpPassword;
        public TLAccountTmpPassword TmpPassword
        {
            get
            {
                if (_tmpPassword == null)
                {
                    var payload = GetValueOrDefault<string>("TmpPassword", null);
                    var data = TLSerializationService.Current.Deserialize<TLAccountTmpPassword>(payload);

                    _tmpPassword = data;
                }

                return _tmpPassword;
            }
            set
            {
                var payload = value != null ? TLSerializationService.Current.Serialize(value) : null;
                var data = AddOrUpdateValue("TmpPassword", payload);

                _tmpPassword = value;
            }
        }

        public ApplicationSettingsDownload AutoDownload => new ApplicationSettingsDownload();
    }

    public class ApplicationSettingsDownload
    {
        private int[] _defaults = new int[3];

        public ApplicationSettingsDownload()
        {
            _defaults[(int)NetworkType.Mobile] = (int)(AutoDownloadType.Photo | AutoDownloadType.Audio | AutoDownloadType.Music | AutoDownloadType.GIF | AutoDownloadType.Round);
            _defaults[(int)NetworkType.WiFi] = (int)(AutoDownloadType.Photo | AutoDownloadType.Audio | AutoDownloadType.Music | AutoDownloadType.GIF | AutoDownloadType.Round);
            _defaults[(int)NetworkType.Roaming] = 0;
        }

        private AutoDownloadType?[] _autoDownload = new AutoDownloadType?[3];
        public AutoDownloadType this[NetworkType index]
        {
            get
            {
                var i = (int)index;
                if (_autoDownload[i] == null)
                    _autoDownload[i] = (AutoDownloadType)ApplicationSettings.Current.GetValueOrDefault("auto_download_" + i, _defaults[(int)index]);

                return _autoDownload[i].Value;
            }
            set
            {
                var i = (int)index;
                _autoDownload[i] = value;
                ApplicationSettings.Current.AddOrUpdateValue("auto_download_" + i, (int)value);
            }
        }
    }

    [Flags]
    public enum AutoDownloadType
    {
        Photo = 1,
        Audio = 2,
        Video = 4,
        Document = 8,
        Music = 16,
        GIF = 32,
        Round = 64,
    }
}
