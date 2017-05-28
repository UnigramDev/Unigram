using System;
using System.IO.IsolatedStorage;
using System.Diagnostics;
using System.ComponentModel;
using System.Threading;
using Windows.Storage;
using Telegram.Api.TL;
using Unigram.Core.Services;
using Telegram.Api.Services;

namespace Unigram.Common
{
    public class ApplicationSettings
    {
        private readonly ApplicationDataContainer isolatedStore;

        public ApplicationSettings()
        {
            try
            {
                isolatedStore = ApplicationData.Current.LocalSettings;
            }
            catch { }
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
