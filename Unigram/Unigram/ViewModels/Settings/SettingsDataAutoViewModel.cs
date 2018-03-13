using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Services;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsDataAutoViewModel : UnigramViewModelBase
    {
        private AutoDownloadType _type;

        public SettingsDataAutoViewModel(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode navigationMode, IDictionary<string, object> state)
        {
            if (parameter is AutoDownloadType type)
            {
                _type = type;

                Title = type == AutoDownloadType.Photos
                    ? Strings.Resources.LocalPhotoCache
                    : type == AutoDownloadType.Videos
                    ? Strings.Resources.LocalVideoCache
                    : type == AutoDownloadType.Documents
                    ? Strings.Resources.FilesDataUsage
                    : type == AutoDownloadType.VideoNotes
                    ? Strings.Resources.VideoMessagesAutodownload
                    : type == AutoDownloadType.VoiceNotes
                    ? Strings.Resources.AudioAutodownload
                    : type == AutoDownloadType.Audios
                    ? Strings.Resources.LocalMusicCache
                    : Strings.Resources.LocalGifCache;

                var preferences = ProtoService.Preferences;
                var mode = type == AutoDownloadType.Photos
                    ? preferences.Photos
                    : type == AutoDownloadType.Videos
                    ? preferences.Videos
                    : type == AutoDownloadType.Documents
                    ? preferences.Documents
                    : type == AutoDownloadType.VideoNotes
                    ? preferences.VideoNotes
                    : type == AutoDownloadType.VoiceNotes
                    ? preferences.VoiceNotes
                    : type == AutoDownloadType.Audios
                    ? preferences.Audios
                    : preferences.Animations;

                Contacts = mode.HasFlag(AutoDownloadMode.WifiContacts);
                PrivateChats = mode.HasFlag(AutoDownloadMode.WifiPrivateChats);
                Groups = mode.HasFlag(AutoDownloadMode.WifiGroups);
                Channels = mode.HasFlag(AutoDownloadMode.WifiChannels);
            }

            return Task.CompletedTask;
        }

        private string _title;
        public string Title
        {
            get
            {
                return _title;
            }
            set
            {
                Set(ref _title, value);
            }
        }

        private bool _contacts;
        public bool Contacts
        {
            get
            {
                return _contacts;
            }
            set
            {
                Set(ref _contacts, value);
            }
        }

        private bool _privateChats;
        public bool PrivateChats
        {
            get
            {
                return _privateChats;
            }
            set
            {
                Set(ref _privateChats, value);
            }
        }

        private bool _groups;
        public bool Groups
        {
            get
            {
                return _groups;
            }
            set
            {
                Set(ref _groups, value);
            }
        }

        private bool _channels;
        public bool Channels
        {
            get
            {
                return _channels;
            }
            set
            {
                Set(ref _channels, value);
            }
        }

        public RelayCommand SendCommand { get; }
        private void SendExecute()
        {
            var preferences = ProtoService.Preferences;
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
                preferences = preferences.UpdateVideosMode(mode, 10);
            }
            else if (_type == AutoDownloadType.Documents)
            {
                preferences = preferences.UpdateDocumentsMode(mode, 10);
            }
            else if (_type == AutoDownloadType.VoiceNotes)
            {
                preferences = preferences.UpdateVoiceNotesMode(mode);
            }
            else if (_type == AutoDownloadType.VideoNotes)
            {
                preferences = preferences.UpdateVideoNotesMode(mode);
            }
            else if (_type == AutoDownloadType.Audios)
            {
                preferences = preferences.UpdateAudiosMode(mode);
            }
            else if (_type == AutoDownloadType.Animations)
            {
                preferences = preferences.UpdateAnimationsMode(mode);
            }

            ProtoService.SetPreferences(preferences);
            NavigationService.GoBack();
        }
    }
}
