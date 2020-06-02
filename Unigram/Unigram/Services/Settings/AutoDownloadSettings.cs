using System;
using Telegram.Td.Api;
using Unigram.Common;
using Windows.Storage;

namespace Unigram.Services.Settings
{
    [Flags]
    public enum AutoDownloadMode
    {
        None = 0,

        CellularContacts = 1 << 0,
        WifiContacts = 1 << 1,

        CellularPrivateChats = 1 << 2,
        WifiPrivateChats = 1 << 3,

        CellularGroups = 1 << 4,
        WifiGroups = 1 << 5,

        CellularChannels = 1 << 6,
        WifiChannels = 1 << 7,

        AutosavePhotosAll = CellularContacts | CellularPrivateChats | CellularGroups | CellularChannels,

        AllPrivateChats = CellularContacts | WifiContacts | CellularPrivateChats | WifiPrivateChats,
        AllGroups = CellularGroups | WifiGroups | CellularChannels | WifiChannels,
        All = CellularContacts | WifiContacts | CellularPrivateChats | WifiPrivateChats | CellularGroups | WifiGroups | CellularChannels | WifiChannels
    }

    public enum AutoDownloadType
    {
        Photos,
        Videos,
        Documents,
    }

    public enum AutoDownloadChat
    {
        Contact,
        OtherPrivateChat,
        Group,
        Channel
    }

    public class AutoDownloadSettings
    {
        private AutoDownloadSettings()
        {

        }

        public AutoDownloadSettings(ApplicationDataContainer container)
        {
            _disabled = container.GetBoolean("disabled", false);
            _photos = (AutoDownloadMode)container.GetInt32("photos", (int)AutoDownloadMode.All);
            _videos = (AutoDownloadMode)container.GetInt32("videos", (int)AutoDownloadMode.All);
            _maximumVideoSize = container.GetInt32("maxVideoSize", 10 * 1024 * 1024);
            _documents = (AutoDownloadMode)container.GetInt32("documents", (int)AutoDownloadMode.All);
            _maximumDocumentSize = container.GetInt32("maxDocumentSize", 3 * 1024 * 1024);
        }

        public void Save(ApplicationDataContainer container)
        {
            container.Values["disabled"] = _disabled;
            container.Values["photos"] = (int)_photos;
            container.Values["videos"] = (int)_videos;
            container.Values["maxVideoSize"] = _maximumVideoSize;
            container.Values["documents"] = (int)_documents;
            container.Values["maxDocumentSize"] = _maximumDocumentSize;
        }

        public static AutoDownloadSettings Default
        {
            get
            {
                var preferences = new AutoDownloadSettings();
                preferences._photos = AutoDownloadMode.All;
                preferences._videos = AutoDownloadMode.All;
                preferences._maximumVideoSize = 10 * 1024 * 1024;
                preferences._documents = AutoDownloadMode.All;
                preferences._maximumDocumentSize = 3 * 1024 * 1024;
                return preferences;
            }
        }

        public static AutoDownloadSettings FromPreset(Telegram.Td.Api.AutoDownloadSettings preset)
        {
            var preferences = new AutoDownloadSettings();
            preferences._disabled = !preset.IsAutoDownloadEnabled;
            preferences._photos = AutoDownloadMode.All;
            preferences._videos = AutoDownloadMode.All;
            preferences._maximumVideoSize = preset.MaxVideoFileSize;
            preferences._documents = AutoDownloadMode.All;
            preferences._maximumDocumentSize = preset.MaxOtherFileSize;
            return preferences;
        }

        public bool IsDefault
        {
            get
            {
                return _photos == AutoDownloadMode.All &&
                    _videos == AutoDownloadMode.All &&
                    _maximumVideoSize == 10 * 1024 * 1024 &&
                    _documents == AutoDownloadMode.All &&
                    _maximumDocumentSize == 3 * 1024 * 1024;
            }
        }

        private bool _disabled;
        public bool Disabled => _disabled;

        private AutoDownloadMode _photos;
        public AutoDownloadMode Photos => _photos;

        private AutoDownloadMode _videos;
        public AutoDownloadMode Videos => _videos;

        private int _maximumVideoSize;
        public int MaximumVideoSize => _maximumVideoSize;

        private AutoDownloadMode _documents;
        public AutoDownloadMode Documents => _documents;

        private int _maximumDocumentSize;
        public int MaximumDocumentSize => _maximumDocumentSize;

        public AutoDownloadSettings UpdateDisabled(bool disabled)
        {
            var preferences = new AutoDownloadSettings();
            preferences._disabled = disabled;
            preferences._photos = _photos;
            preferences._videos = _videos;
            preferences._maximumVideoSize = _maximumVideoSize;
            preferences._documents = _documents;
            preferences._maximumDocumentSize = _maximumDocumentSize;
            return preferences;
        }

        public AutoDownloadSettings UpdatePhotosMode(AutoDownloadMode mode)
        {
            var preferences = new AutoDownloadSettings();
            preferences._photos = mode;
            preferences._videos = _videos;
            preferences._maximumVideoSize = _maximumVideoSize;
            preferences._documents = _documents;
            preferences._maximumDocumentSize = _maximumDocumentSize;
            return preferences;
        }

        public AutoDownloadSettings UpdateVideosMode(AutoDownloadMode mode, int maximumSize)
        {
            var preferences = new AutoDownloadSettings();
            preferences._photos = _photos;
            preferences._videos = mode;
            preferences._maximumVideoSize = maximumSize;
            preferences._documents = _documents;
            preferences._maximumDocumentSize = _maximumDocumentSize;
            return preferences;
        }

        public AutoDownloadSettings UpdateDocumentsMode(AutoDownloadMode mode, int maximumSize)
        {
            var preferences = new AutoDownloadSettings();
            preferences._photos = _photos;
            preferences._videos = _videos;
            preferences._maximumVideoSize = _maximumVideoSize;
            preferences._documents = mode;
            preferences._maximumDocumentSize = maximumSize;
            return preferences;
        }



        public bool ShouldDownload(AutoDownloadMode mode, AutoDownloadChat chat, NetworkType networkType = null)
        {
            bool isWiFi = networkType is NetworkTypeWiFi || networkType == null;
            bool isCellular = !isWiFi && !(networkType is NetworkTypeNone);

            bool shouldDownload = false;
            switch (chat)
            {
                case AutoDownloadChat.Contact:
                    if (isCellular)
                        shouldDownload = (mode & AutoDownloadMode.CellularContacts) != 0;
                    else if (isWiFi)
                        shouldDownload = (mode & AutoDownloadMode.WifiContacts) != 0;
                    break;

                case AutoDownloadChat.OtherPrivateChat:
                    if (isCellular)
                        shouldDownload = (mode & AutoDownloadMode.CellularPrivateChats) != 0;
                    else if (isWiFi)
                        shouldDownload = (mode & AutoDownloadMode.WifiPrivateChats) != 0;
                    break;

                case AutoDownloadChat.Group:
                    if (isCellular)
                        shouldDownload = (mode & AutoDownloadMode.CellularGroups) != 0;
                    else if (isWiFi)
                        shouldDownload = (mode & AutoDownloadMode.WifiGroups) != 0;
                    break;

                case AutoDownloadChat.Channel:
                    if (isCellular)
                        shouldDownload = (mode & AutoDownloadMode.CellularChannels) != 0;
                    else if (isWiFi)
                        shouldDownload = (mode & AutoDownloadMode.WifiChannels) != 0;
                    break;

                default:
                    break;
            }

            return shouldDownload;
        }

        public bool ShouldDownloadPhoto(AutoDownloadChat chat, NetworkType networkType = null)
        {
            if (_disabled)
            {
                return false;
            }

            return ShouldDownload(_photos, chat, networkType);
        }

        public bool ShouldDownloadVideo(AutoDownloadChat chat, int size, NetworkType networkType = null)
        {
            if (_disabled)
            {
                return false;
            }

            if (size > _maximumVideoSize)
            {
                return false;
            }

            return ShouldDownload(_videos, chat, networkType);
        }

        public bool ShouldDownloadDocument(AutoDownloadChat chat, int size, NetworkType networkType = null)
        {
            if (_disabled)
            {
                return false;
            }

            if (size > _maximumDocumentSize)
            {
                return false;
            }

            return ShouldDownload(_documents, chat, networkType);
        }
    }
}
