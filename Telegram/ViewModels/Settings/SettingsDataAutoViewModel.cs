//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Navigation;
using Telegram.Services;
using Telegram.Services.Settings;

namespace Telegram.ViewModels.Settings
{
    public partial class SettingsDataAutoViewModel : ViewModelBase
    {
        private AutoDownloadType _type;

        public SettingsDataAutoViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, AutoDownloadType type)
            : base(clientService, settingsService, aggregator)
        {
            _type = type;

            Title = type == AutoDownloadType.Photos
                ? Strings.AutoDownloadPhotos
                : type == AutoDownloadType.Videos
                ? Strings.AutoDownloadVideos
                : Strings.AutoDownloadFiles;
            Header = type == AutoDownloadType.Photos
                ? Strings.AutoDownloadPhotosTitle
                : type == AutoDownloadType.Videos
                ? Strings.AutoDownloadVideosTitle
                : Strings.AutoDownloadFilesTitle;

            var preferences = Settings.AutoDownload;
            var mode = type == AutoDownloadType.Photos
                ? preferences.Photos
                : type == AutoDownloadType.Videos
                ? preferences.Videos
                : preferences.Documents;
            var limit = type == AutoDownloadType.Photos
                ? 0
                : type == AutoDownloadType.Videos
                ? preferences.MaximumVideoSize
                : preferences.MaximumDocumentSize;

            _contacts = mode.HasFlag(AutoDownloadMode.WifiContacts);
            _privateChats = mode.HasFlag(AutoDownloadMode.WifiPrivateChats);
            _groups = mode.HasFlag(AutoDownloadMode.WifiGroups);
            _channels = mode.HasFlag(AutoDownloadMode.WifiChannels);
            _limit = limit;
        }

        private string _title;
        public string Title
        {
            get => _title;
            set => Set(ref _title, value);
        }

        private string _header;
        public string Header
        {
            get => _header;
            set => Set(ref _header, value);
        }

        private bool _contacts;
        public bool Contacts
        {
            get => _contacts;
            set
            {
                if (Set(ref _contacts, value))
                {
                    Save();
                }
            }
        }

        private bool _privateChats;
        public bool PrivateChats
        {
            get => _privateChats;
            set
            {
                if (Set(ref _privateChats, value))
                {
                    Save();
                }
            }
        }

        private bool _groups;
        public bool Groups
        {
            get => _groups;
            set
            {
                if (Set(ref _groups, value))
                {
                    Save();
                }
            }
        }

        private bool _channels;
        public bool Channels
        {
            get => _channels;
            set
            {
                if (Set(ref _channels, value))
                {
                    Save();
                }
            }
        }

        private long _limit;
        public long Limit
        {
            get => _limit;
            set
            {
                if (Set(ref _limit, value))
                {
                    Save();
                }
            }
        }

        private bool _preload;
        public bool Preload
        {
            get => _preload;
            set => Set(ref _preload, value);
        }

        public bool IsLimitSupported => _type != AutoDownloadType.Photos;

        public bool IsPreloadSupported => _type == AutoDownloadType.Videos && SettingsService.Current.Diagnostics.VideoPreloadDebug;

        public void Save()
        {
            var preferences = Settings.AutoDownload;
            var mode = (AutoDownloadMode)0;

            if (_contacts)
            {
                mode |= AutoDownloadMode.WifiContacts;
            }
            if (_privateChats)
            {
                mode |= AutoDownloadMode.WifiPrivateChats;
            }
            if (_groups)
            {
                mode |= AutoDownloadMode.WifiGroups;
            }
            if (_channels)
            {
                mode |= AutoDownloadMode.WifiChannels;
            }

            if (_type == AutoDownloadType.Photos)
            {
                preferences = preferences.UpdatePhotosMode(mode);
            }
            else if (_type == AutoDownloadType.Videos)
            {
                preferences = preferences.UpdateVideosMode(mode, _limit, _preload);
            }
            else if (_type == AutoDownloadType.Documents)
            {
                preferences = preferences.UpdateDocumentsMode(mode, _limit);
            }

            Settings.AutoDownload = preferences;
        }
    }
}
