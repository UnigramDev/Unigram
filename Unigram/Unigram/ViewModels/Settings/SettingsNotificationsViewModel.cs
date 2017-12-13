using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Core.Services;
using Unigram.Strings;
using Windows.Foundation.Metadata;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsNotificationsViewModel : UnigramViewModelBase, IHandle<TLUpdateNotifySettings>, IHandle
    {
        private readonly IVibrationService _vibrationService;

        private bool _suppressUpdating;

        public SettingsNotificationsViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, IVibrationService vibrationService) 
            : base(protoService, cacheService, aggregator)
        {
            _vibrationService = vibrationService;

            ResetCommand = new RelayCommand(ResetExecute);

            Aggregator.Subscribe(this);
            PropertyChanged += OnPropertyChanged;
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            IsVibrationAvailable = await _vibrationService.GetAvailabilityAsync();
            await UpdateAsync();
        }

        public Task UpdatePrivateAsync()
        {
            return ProtoService.UpdateNotifySettingsAsync(new TLInputNotifyUsers(), new TLInputPeerNotifySettings
            {
                MuteUntil = _privateAlert ? 0 : int.MaxValue,
                IsShowPreviews = _privatePreview,
                //Sound = _privateSound
            });
        }

        public Task UpdateGroupAsync()
        {
            return ProtoService.UpdateNotifySettingsAsync(new TLInputNotifyChats(), new TLInputPeerNotifySettings
            {
                MuteUntil = _groupAlert ? 0 : int.MaxValue,
                IsShowPreviews = _groupPreview,
                //Sound = _privateSound
            });
        }

        private async void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_suppressUpdating)
            {
                return;
            }

            if (e.PropertyName.Equals(nameof(InAppVibrate)) && InAppVibrate && IsVibrationAvailable)
            {
                await _vibrationService.VibrateAsync();
            }
        }

        #region Private

        private bool _privateAlert;
        public bool PrivateAlert
        {
            get
            {
                return _privateAlert;
            }
            set
            {
                Set(ref _privateAlert, value);
            }
        }

        private bool _privatePreview;
        public bool PrivatePreview
        {
            get
            {
                return _privatePreview;
            }
            set
            {
                Set(ref _privatePreview, value);
            }
        }

        private string _privateSound;
        public string PrivateSound
        {
            get
            {
                return _privateSound;
            }
            set
            {
                Set(ref _privateSound, value);
            }
        }

        #endregion

        #region Group

        private bool _groupAlert;
        public bool GroupAlert
        {
            get
            {
                return _groupAlert;
            }
            set
            {
                Set(ref _groupAlert, value);
            }
        }

        private bool _groupPreview;
        public bool GroupPreview
        {
            get
            {
                return _groupPreview;
            }
            set
            {
                Set(ref _groupPreview, value);
            }
        }

        private string _groupSound;
        public string GroupSound
        {
            get
            {
                return _groupSound;
            }
            set
            {
                Set(ref _groupSound, value);
            }
        }

        #endregion

        #region InApp

        private bool _isVibrationAvailable;
        public bool IsVibrationAvailable
        {
            get
            {
                return _isVibrationAvailable;
            }
            set
            {
                Set(ref _isVibrationAvailable, value);
            }
        }

        public bool InAppSounds
        {
            get
            {
                return ApplicationSettings.Current.InAppSounds;
            }
            set
            {
                ApplicationSettings.Current.InAppSounds = value;
                RaisePropertyChanged();
            }
        }

        public bool InAppVibrate
        {
            get
            {
                return ApplicationSettings.Current.InAppVibrate;
            }
            set
            {
                ApplicationSettings.Current.InAppVibrate = value;
                RaisePropertyChanged();
            }
        }

        public bool InAppPreview
        {
            get
            {
                return ApplicationSettings.Current.InAppPreview;
            }
            set
            {
                ApplicationSettings.Current.InAppPreview = value;
                RaisePropertyChanged();
            }
        }

        #endregion

        private async Task UpdateAsync()
        {
            ProtoService.GetNotifySettingsAsync(new TLInputNotifyUsers(), result =>
            {
                if (result is TLPeerNotifySettings settings)
                {
                    BeginOnUIThread(() =>
                    {
                        PrivateAlert = settings.MuteUntil == 0;
                        PrivatePreview = settings.IsShowPreviews;
                    });
                    //sound = Enumerable.FirstOrDefault<string>(this.StateService.Sounds, (string x) => string.Equals(x, settings.Sound.Value, 5));
                    //this.Settings.ContactSound = (sound ?? this.StateService.Sounds.get_Item(0));
                }

                //this.SaveSettings();
            });

            ProtoService.GetNotifySettingsAsync(new TLInputNotifyChats(), result =>
            {
                if (result is TLPeerNotifySettings settings)
                {
                    BeginOnUIThread(() =>
                    {
                        GroupAlert = settings.MuteUntil == 0;
                        GroupPreview = settings.IsShowPreviews;
                    });
                    //sound = Enumerable.FirstOrDefault<string>(this.StateService.Sounds, (string x) => string.Equals(x, settings.Sound.Value, 5));
                    //this.Settings.GroupSound = (sound ?? this.StateService.Sounds.get_Item(0));
                }

                //this.SaveSettings();
            });
        }

        public void Handle(TLUpdateNotifySettings update)
        {
            var settings = update.NotifySettings as TLPeerNotifySettings;

            if (update.Peer is TLNotifyUsers notifyUsers && settings != null)
            {
                BeginOnUIThread(() =>
                {
                    PrivateAlert = settings.MuteUntil == 0;
                    PrivatePreview = settings.IsShowPreviews;
                });
            }
            else if (update.Peer is TLNotifyChats notifyChats && settings != null)
            {
                BeginOnUIThread(() =>
                {
                    GroupAlert = settings.MuteUntil == 0;
                    GroupPreview = settings.IsShowPreviews;
                });
            }
        }

        public RelayCommand ResetCommand { get; }
        private async void ResetExecute()
        {
            //var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.ResetNotificationsDialogBody, Strings.Resources.ResetNotificationsDialogTitle, Strings.Resources.OK, Strings.Resources.Cancel);
            //if (confirm == ContentDialogResult.Primary)
            //{
                _suppressUpdating = true;
                PrivateAlert = true;
                PrivatePreview = true;
                PrivateSound = string.Empty;
                GroupAlert = true;
                GroupPreview = true;
                GroupSound = string.Empty;
                InAppSounds = true;
                InAppPreview = true;
                InAppVibrate = true;
                _suppressUpdating = false;

                var response = await ProtoService.ResetNotifySettingsAsync();
            //}
        }
    }
}
