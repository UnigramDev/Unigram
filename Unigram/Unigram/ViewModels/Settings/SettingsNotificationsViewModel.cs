﻿using System;
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

            Aggregator.Subscribe(this);
            PropertyChanged += OnPropertyChanged;
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            IsVibrationAvailable = await _vibrationService.GetAvailabilityAsync();
            await UpdateAsync();
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

            if (e.PropertyName.Equals(nameof(PrivateAlert)) || e.PropertyName.Equals(nameof(PrivatePreview)) || e.PropertyName.Equals(nameof(PrivateSound)))
            {
                if (e.PropertyName.Equals(nameof(PrivateSound)) && !string.IsNullOrEmpty(PrivateSound))
                {
                }

                await ProtoService.UpdateNotifySettingsAsync(new TLInputNotifyUsers(), new TLInputPeerNotifySettings
                {
                    MuteUntil = _privateAlert ? 0 : int.MaxValue,
                    IsShowPreviews = _privatePreview,
                    //Sound = _privateSound
                });
            }
            else if (e.PropertyName.Equals(nameof(GroupAlert)) || e.PropertyName.Equals(nameof(GroupPreview)) || e.PropertyName.Equals(nameof(GroupSound)))
            {
                if (e.PropertyName.Equals(nameof(GroupSound)) && !string.IsNullOrEmpty(GroupSound))
                {
                }

                await ProtoService.UpdateNotifySettingsAsync(new TLInputNotifyChats(), new TLInputPeerNotifySettings
                {
                    MuteUntil = _groupAlert ? 0 : int.MaxValue,
                    IsShowPreviews = _groupPreview,
                    //Sound = _privateSound
                });
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

                var settings = result as TLPeerNotifySettings;
                if (settings != null)
                {
                    Execute.BeginOnUIThread(() =>
                    {
                        _suppressUpdating = true;
                        PrivateAlert = settings.MuteUntil == 0;
                        PrivatePreview = settings.IsShowPreviews;
                        _suppressUpdating = false;
                    });
                    //sound = Enumerable.FirstOrDefault<string>(this.StateService.Sounds, (string x) => string.Equals(x, settings.Sound.Value, 5));
                    //this.Settings.ContactSound = (sound ?? this.StateService.Sounds.get_Item(0));
                }

                //this.SaveSettings();
            });

            ProtoService.GetNotifySettingsAsync(new TLInputNotifyChats(), result =>
            {

                var settings = result as TLPeerNotifySettings;
                if (settings != null)
                {
                    Execute.BeginOnUIThread(() =>
                    {
                        _suppressUpdating = true;
                        GroupAlert = settings.MuteUntil == 0;
                        GroupPreview = settings.IsShowPreviews;
                        _suppressUpdating = false;
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

            var notifyUsers = update.Peer as TLNotifyUsers;
            if (notifyUsers != null && settings != null)
            {
                Execute.BeginOnUIThread(() =>
                {
                    PrivateAlert = settings.MuteUntil == 0;
                    PrivatePreview = settings.IsShowPreviews;
                });
            }

            var notifyChats = update.Peer as TLNotifyChats;
            if (notifyChats != null && settings != null)
            {
                Execute.BeginOnUIThread(() =>
                {
                    GroupAlert = settings.MuteUntil == 0;
                    GroupPreview = settings.IsShowPreviews;
                });
            }
        }

        public RelayCommand ResetCommand => new RelayCommand(ResetExecute);
        private async void ResetExecute()
        {
            var confirm = await TLMessageDialog.ShowAsync("Are you sure you want to reset your notification settings?", "Confirm", "OK", "Cancel");
            if (confirm == ContentDialogResult.Primary)
            {
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
            }
        }
    }
}
