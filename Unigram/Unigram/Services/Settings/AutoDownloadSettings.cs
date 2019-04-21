using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        VoiceNotes,
        VideoNotes,
        Audios,
        Animations
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
            _videos = (AutoDownloadMode)container.GetInt32("videos", (int)AutoDownloadMode.None);
            _maximumVideoSize = container.GetInt32("maxVideoSize", 10);
            _documents = (AutoDownloadMode)container.GetInt32("documents", (int)AutoDownloadMode.None);
            _maximumDocumentSize = container.GetInt32("maxDocumentSize", 10);
            _voiceNotes = (AutoDownloadMode)container.GetInt32("voiceNotes", (int)AutoDownloadMode.All);
            _videoNotes = (AutoDownloadMode)container.GetInt32("videoNotes", (int)AutoDownloadMode.All);
            _audios = (AutoDownloadMode)container.GetInt32("audios", (int)AutoDownloadMode.None);
            _animations = (AutoDownloadMode)container.GetInt32("animations", (int)AutoDownloadMode.All);
        }

        public void Save(ApplicationDataContainer container)
        {
            container.Values["disabled"] = _disabled;
            container.Values["photos"] = (int)_photos;
            container.Values["videos"] = (int)_videos;
            container.Values["maxVideoSize"] = _maximumVideoSize;
            container.Values["documents"] = (int)_documents;
            container.Values["maxDocumentSize"] = _maximumDocumentSize;
            container.Values["voiceNotes"] = (int)_voiceNotes;
            container.Values["videoNotes"] = (int)_videoNotes;
            container.Values["audios"] = (int)_audios;
            container.Values["animations"] = (int)_animations;
        }

        public static AutoDownloadSettings Default
        {
            get
            {
                var preferences = new AutoDownloadSettings();
                preferences._photos = AutoDownloadMode.All;
                preferences._videos = AutoDownloadMode.None;
                preferences._maximumVideoSize = 10;
                preferences._documents = AutoDownloadMode.None;
                preferences._maximumDocumentSize = 10;
                preferences._voiceNotes = AutoDownloadMode.All;
                preferences._videoNotes = AutoDownloadMode.All;
                preferences._audios = AutoDownloadMode.None;
                preferences._animations = AutoDownloadMode.All;
                return preferences;

            }
        }

        private bool _disabled;

        private AutoDownloadMode _photos;
        private AutoDownloadMode _videos;
        private int _maximumVideoSize;
        private AutoDownloadMode _documents;
        private int _maximumDocumentSize;
        private AutoDownloadMode _voiceNotes;
        private AutoDownloadMode _videoNotes;
        private AutoDownloadMode _audios;
        private AutoDownloadMode _animations;



        public bool Disabled => _disabled;

        public AutoDownloadMode Photos => _photos;
        public AutoDownloadMode Videos => _videos;
        public int MaximumVideoSize => _maximumVideoSize;
        public AutoDownloadMode Documents => _documents;
        public int MaximumDocumentSize => _maximumDocumentSize;
        public AutoDownloadMode VoiceNotes => _voiceNotes;
        public AutoDownloadMode VideoNotes => _videoNotes;
        public AutoDownloadMode Audios => _audios;
        public AutoDownloadMode Animations => _animations;

        public AutoDownloadSettings UpdateDisabled(bool disabled)
        {
            var preferences = new AutoDownloadSettings();
            preferences._disabled = disabled;
            preferences._photos = _photos;
            preferences._videos = _videos;
            preferences._maximumVideoSize = _maximumVideoSize;
            preferences._documents = _documents;
            preferences._maximumDocumentSize = _maximumDocumentSize;
            preferences._voiceNotes = _voiceNotes;
            preferences._videoNotes = _videoNotes;
            preferences._audios = _audios;
            preferences._animations = _animations;
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
            preferences._voiceNotes = _voiceNotes;
            preferences._videoNotes = _videoNotes;
            preferences._audios = _audios;
            preferences._animations = _animations;
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
            preferences._voiceNotes = _voiceNotes;
            preferences._videoNotes = _videoNotes;
            preferences._audios = _audios;
            preferences._animations = _animations;
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
            preferences._voiceNotes = _voiceNotes;
            preferences._videoNotes = _videoNotes;
            preferences._audios = _audios;
            preferences._animations = _animations;
            return preferences;
        }

        public AutoDownloadSettings UpdateVoiceNotesMode(AutoDownloadMode mode)
        {
            var preferences = new AutoDownloadSettings();
            preferences._photos = _photos;
            preferences._videos = _videos;
            preferences._maximumVideoSize = _maximumVideoSize;
            preferences._documents = _documents;
            preferences._maximumDocumentSize = _maximumDocumentSize;
            preferences._voiceNotes = mode;
            preferences._videoNotes = _videoNotes;
            preferences._audios = _audios;
            preferences._animations = _animations;
            return preferences;
        }

        public AutoDownloadSettings UpdateVideoNotesMode(AutoDownloadMode mode)
        {
            var preferences = new AutoDownloadSettings();
            preferences._photos = _photos;
            preferences._videos = _videos;
            preferences._maximumVideoSize = _maximumVideoSize;
            preferences._documents = _documents;
            preferences._maximumDocumentSize = _maximumDocumentSize;
            preferences._voiceNotes = _voiceNotes;
            preferences._videoNotes = mode;
            preferences._audios = _audios;
            preferences._animations = _animations;
            return preferences;
        }

        public AutoDownloadSettings UpdateAnimationsMode(AutoDownloadMode mode)
        {
            var preferences = new AutoDownloadSettings();
            preferences._photos = _photos;
            preferences._videos = _videos;
            preferences._maximumVideoSize = _maximumVideoSize;
            preferences._documents = _documents;
            preferences._maximumDocumentSize = _maximumDocumentSize;
            preferences._voiceNotes = _voiceNotes;
            preferences._videoNotes = _videoNotes;
            preferences._animations = mode;
            preferences._audios = _audios;
            return preferences;
        }

        public AutoDownloadSettings UpdateAudiosMode(AutoDownloadMode mode)
        {
            var preferences = new AutoDownloadSettings();
            preferences._photos = _photos;
            preferences._videos = _videos;
            preferences._maximumVideoSize = _maximumVideoSize;
            preferences._documents = _documents;
            preferences._maximumDocumentSize = _maximumDocumentSize;
            preferences._voiceNotes = _voiceNotes;
            preferences._videoNotes = _videoNotes;
            preferences._animations = _animations;
            preferences._audios = mode;
            return preferences;
        }



        public bool ShouldDownload(AutoDownloadMode mode, AutoDownloadChat chat, NetworkType networkType)
        {
            bool isWiFi = networkType is NetworkTypeWiFi;
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

        public bool ShouldDownloadPhoto(AutoDownloadChat chat, NetworkType networkType)
        {
            if (_disabled)
            {
                return false;
            }

            return ShouldDownload(_photos, chat, networkType);
        }

        public bool ShouldDownloadVideo(AutoDownloadChat chat, NetworkType networkType, int size)
        {
            if (_disabled)
            {
                return false;
            }

            if ((size / 1024f) / 1024f > _maximumVideoSize)
            {
                return false;
            }

            return ShouldDownload(_videos, chat, networkType);
        }

        public bool ShouldDownloadDocument(AutoDownloadChat chat, NetworkType networkType, int size)
        {
            if (_disabled)
            {
                return false;
            }

            if ((size / 1024f) / 1024f > _maximumDocumentSize)
            {
                return false;
            }

            return ShouldDownload(_documents, chat, networkType);
        }

        public bool ShouldDownloadVoiceNote(AutoDownloadChat chat, NetworkType networkType)
        {
            if (_disabled)
            {
                return false;
            }

            return ShouldDownload(_voiceNotes, chat, networkType);
        }

        public bool ShouldDownloadVideoNote(AutoDownloadChat chat, NetworkType networkType)
        {
            if (_disabled)
            {
                return false;
            }

            return ShouldDownload(_videoNotes, chat, networkType);
        }

        public bool ShouldDownloadAnimation(AutoDownloadChat chat, NetworkType networkType)
        {
            if (_disabled)
            {
                return false;
            }

            return ShouldDownload(_animations, chat, networkType);
        }

        public bool ShouldDownloadAudio(AutoDownloadChat chat, NetworkType networkType)
        {
            if (_disabled)
            {
                return false;
            }

            return ShouldDownload(_audios, chat, networkType);
        }
    }
}
