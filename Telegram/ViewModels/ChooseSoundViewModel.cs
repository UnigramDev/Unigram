//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Rg.DiffUtils;
using System;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.Storage.Pickers;
using Microsoft.UI.Xaml.Navigation;

namespace Telegram.ViewModels
{
    public class ChooseSoundViewModel : ViewModelBase, IHandle
    {
        public ChooseSoundViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new DiffObservableCollection<NotificationSoundViewModel>(new NotificationSoundDiffHandler());
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

        public async void Upload()
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
        private long _soundToken;

        public NotificationSoundViewModel(ChooseSoundViewModel parent, NotificationSound notificationSound, bool selected)
        {
            _parent = parent;

            _notificationSound = notificationSound;
            _isSelected = selected;

            Sound = notificationSound.Sound;
            Data = notificationSound.Data;

            if (notificationSound.Title.Length > 0)
            {
                Title = notificationSound.Title;
            }
            else
            {
                Title = string.Format(Strings.SoundNameEmpty, Formatter.Date(notificationSound.Date));
            }

            Date = notificationSound.Date;
            Duration = notificationSound.Duration;
            Id = notificationSound.Id;
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
                    UpdateManager.Subscribe(this, _parent.ClientService, file, ref _soundToken, UpdateFile, true);

                    if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
                    {
                        _parent.ClientService.DownloadFile(file.Id, 16);
                    }
                }
            }
            else
            {
                UpdateManager.Unsubscribe(this, ref _soundToken, true);
            }
        }

        private void UpdateFile(object sender, File file)
        {
            if (sender is NotificationSoundViewModel notificationSound && notificationSound.IsSelected)
            {
                SoundEffects.Play(file);
            }
        }

        /// <summary>
        /// File containing the sound.
        /// </summary>
        public File Sound { get; }

        /// <summary>
        /// Arbitrary data, defined while the sound was uploaded.
        /// </summary>
        public string Data { get; }

        /// <summary>
        /// Title of the notification sound.
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// Point in time (Unix timestamp) when the sound was created.
        /// </summary>
        public int Date { get; }

        /// <summary>
        /// Duration of the sound, in seconds.
        /// </summary>
        public int Duration { get; }

        /// <summary>
        /// Unique identifier of the notification sound.
        /// </summary>
        public long Id { get; }
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
