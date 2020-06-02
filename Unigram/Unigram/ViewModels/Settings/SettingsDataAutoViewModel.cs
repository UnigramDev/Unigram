using System.Collections.Generic;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Services;
using Unigram.Services.Settings;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsDataAutoViewModel : TLViewModelBase
    {
        private AutoDownloadType _type;

        public SettingsDataAutoViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode navigationMode, IDictionary<string, object> state)
        {
            if (parameter is AutoDownloadType type)
            {
                _type = type;

                Title = type == AutoDownloadType.Photos
                    ? Strings.Resources.AutoDownloadPhotos
                    : type == AutoDownloadType.Videos
                    ? Strings.Resources.AutoDownloadVideos
                    : Strings.Resources.AutoDownloadFiles;
                Header = type == AutoDownloadType.Photos
                    ? Strings.Resources.AutoDownloadPhotosTitle
                    : type == AutoDownloadType.Videos
                    ? Strings.Resources.AutoDownloadVideosTitle
                    : Strings.Resources.AutoDownloadFilesTitle;

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

        private int _limit;
        public int Limit
        {
            get => _limit;
            set => Set(ref _limit, value);
        }

        public bool IsLimitSupported => _type != AutoDownloadType.Photos;

        public RelayCommand SendCommand { get; }
        private void SendExecute()
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
            NavigationService.GoBack();
        }
    }
}
