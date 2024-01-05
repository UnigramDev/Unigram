//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Threading.Tasks;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Settings;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Settings
{
    public class SettingsDataAutoViewModel : ViewModelBase
    {
        private AutoDownloadType _type;

        public SettingsDataAutoViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode navigationMode, NavigationState state)
        {
            if (parameter is AutoDownloadType type)
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

                Contacts = mode.HasFlag(AutoDownloadMode.WifiContacts);
                PrivateChats = mode.HasFlag(AutoDownloadMode.WifiPrivateChats);
                Groups = mode.HasFlag(AutoDownloadMode.WifiGroups);
                Channels = mode.HasFlag(AutoDownloadMode.WifiChannels);
                Limit = limit;
            }

            return Task.CompletedTask;
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
            set => Set(ref _contacts, value);
        }

        private bool _privateChats;
        public bool PrivateChats
        {
            get => _privateChats;
            set => Set(ref _privateChats, value);
        }

        private bool _groups;
        public bool Groups
        {
            get => _groups;
            set => Set(ref _groups, value);
        }

        private bool _channels;
        public bool Channels
        {
            get => _channels;
            set => Set(ref _channels, value);
        }

        private long _limit;
        public long Limit
        {
            get => _limit;
            set => Set(ref _limit, value);
        }

        public bool IsLimitSupported => _type != AutoDownloadType.Photos;

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
                preferences = preferences.UpdateVideosMode(mode, _limit);
            }
            else if (_type == AutoDownloadType.Documents)
            {
                preferences = preferences.UpdateDocumentsMode(mode, _limit);
            }

            Settings.AutoDownload = preferences;
        }
    }
}
