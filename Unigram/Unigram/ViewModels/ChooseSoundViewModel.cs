//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Rg.DiffUtils;
using System;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Navigation;
using Unigram.Navigation.Services;
using Unigram.Services;
using Windows.Storage.Pickers;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class ChooseSoundViewModel : TLViewModelBase
        , IHandle
        //, IHandle<UpdateSavedNotificationSounds>
    {
        public ChooseSoundViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new DiffObservableCollection<NotificationSoundViewModel>(new NotificationSoundDiffHandler());

            UploadCommand = new RelayCommand(Upload);
        }

        public DiffObservableCollection<NotificationSoundViewModel> Items { get; }

        public bool CanUploadMore => Items.Count < ClientService.Options.NotificationSoundCountMax;

        public async void Handle(UpdateSavedNotificationSounds update)
        {
            var response = await ClientService.SendAsync(new GetSavedNotificationSounds());
            if (response is NotificationSounds sounds)
            {
                BeginOnUIThread(() =>
                {
                    Items.ReplaceDiff(sounds.NotificationSoundsValue.Select(x => new NotificationSoundViewModel(this, x, false)));
                    RaisePropertyChanged(nameof(CanUploadMore));
                });
            }
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var response = await ClientService.SendAsync(new GetSavedNotificationSounds());
            if (response is NotificationSounds sounds && parameter is long selected)
            {
                Items.ReplaceDiff(sounds.NotificationSoundsValue.Select(x => new NotificationSoundViewModel(this, x, x.Id == selected)));
                RaisePropertyChanged(nameof(CanUploadMore));
            }
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateSavedNotificationSounds>(this, Handle);
        }

        public void UpdateFile(object sender, File file)
        {
            if (sender is NotificationSoundViewModel notificationSound && notificationSound.IsSelected)
            {
                SoundEffects.Play(file);
            }
        }

        public RelayCommand UploadCommand { get; }
        private async void Upload()
        {
            try
            {
                var picker = new FileOpenPicker();
                picker.ViewMode = PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = PickerLocationId.MusicLibrary;
                picker.FileTypeFilter.Add(".mp3");

                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    var properties = await file.GetBasicPropertiesAsync();
                    if ((long)properties.Size > ClientService.Options.NotificationSoundSizeMax)
                    {
                        // TODO: ...
                        return;
                    }

                    var music = await file.Properties.GetMusicPropertiesAsync();
                    if (music.Duration.TotalSeconds > ClientService.Options.NotificationSoundDurationMax)
                    {
                        // TODO: ...
                        return;
                    }

                    ClientService.Send(new AddSavedNotificationSound(await file.ToGeneratedAsync()));
                }
            }
            catch { }

        }
    }

    public class NotificationSoundViewModel : BindableBase
    {
        private readonly ChooseSoundViewModel _parent;

        private readonly NotificationSound _notificationSound;
        private string _soundToken;

        public NotificationSoundViewModel(ChooseSoundViewModel parent, NotificationSound notificationSound, bool selected)
        {
            _parent = parent;

            _notificationSound = notificationSound;
            _isSelected = selected;
        }

        public NotificationSound Get()
        {
            return _notificationSound;
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetSelected(value);
        }

        private void SetSelected(bool value)
        {
            if (Set(ref _isSelected, value, nameof(IsSelected)) && _isSelected)
            {
                var file = _notificationSound.Sound;
                if (file.Local.IsDownloadingCompleted)
                {
                    SoundEffects.Play(file);
                }
                else
                {
                    UpdateManager.Subscribe(this, _parent.ClientService, file, ref _soundToken, _parent.UpdateFile, true);

                    if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
                    {
                        _parent.ClientService.DownloadFile(file.Id, 16);
                    }
                }
            }
        }

        /// <summary>
        /// File containing the sound.
        /// </summary>
        public File Sound => _notificationSound.Sound;

        /// <summary>
        /// Arbitrary data, defined while the sound was uploaded.
        /// </summary>
        public string Data => _notificationSound.Data;

        /// <summary>
        /// Title of the notification sound.
        /// </summary>
        public string Title => _notificationSound.Title;

        /// <summary>
        /// Point in time (Unix timestamp) when the sound was created.
        /// </summary>
        public int Date => _notificationSound.Date;

        /// <summary>
        /// Duration of the sound, in seconds.
        /// </summary>
        public int Duration => _notificationSound.Duration;

        /// <summary>
        /// Unique identifier of the notification sound.
        /// </summary>
        public long Id => _notificationSound.Id;
    }

    public class NotificationSoundDiffHandler : IDiffHandler<NotificationSoundViewModel>
    {
        public bool CompareItems(NotificationSoundViewModel oldItem, NotificationSoundViewModel newItem)
        {
            return oldItem?.Id == newItem?.Id;
        }

        public void UpdateItem(NotificationSoundViewModel oldItem, NotificationSoundViewModel newItem)
        {
            // ...
        }
    }
}
