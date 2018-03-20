using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Core.Services;
using Unigram.Services;
using Unigram.Strings;
using Windows.Foundation.Metadata;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsNotificationsViewModel : UnigramViewModelBase, IHandle<UpdateNotificationSettings>
    {
        private readonly IVibrationService _vibrationService;

        private bool _suppressUpdating;

        public SettingsNotificationsViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, IVibrationService vibrationService) 
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _vibrationService = vibrationService;

            ResetCommand = new RelayCommand(ResetExecute);

            Aggregator.Subscribe(this);
            PropertyChanged += OnPropertyChanged;
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            ProtoService.Send(new GetNotificationSettings(new NotificationSettingsScopePrivateChats()), result =>
            {
                if (result is NotificationSettings settings)
                {
                    BeginOnUIThread(() =>
                    {
                        PrivateAlert = settings.MuteFor == 0;
                        PrivatePreview = settings.ShowPreview;
                    });
                }
            });

            ProtoService.Send(new GetNotificationSettings(new NotificationSettingsScopeBasicGroupChats()), result =>
            {
                if (result is NotificationSettings settings)
                {
                    BeginOnUIThread(() =>
                    {
                        GroupAlert = settings.MuteFor == 0;
                        GroupPreview = settings.ShowPreview;
                    });
                }
            });

            IsVibrationAvailable = await _vibrationService.GetAvailabilityAsync();
        }

        public Task UpdatePrivateAsync()
        {
            return ProtoService.SendAsync(new SetNotificationSettings(new NotificationSettingsScopePrivateChats(), new NotificationSettings(_privateAlert ? 0 : int.MaxValue, string.Empty, _privatePreview)));
        }

        public Task UpdateGroupAsync()
        {
            return ProtoService.SendAsync(new SetNotificationSettings(new NotificationSettingsScopeBasicGroupChats(), new NotificationSettings(_groupAlert ? 0 : int.MaxValue, string.Empty, _groupPreview)));
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
                return Settings.Notifications.InAppSounds;
            }
            set
            {
                Settings.Notifications.InAppSounds = value;
                RaisePropertyChanged();
            }
        }

        public bool InAppVibrate
        {
            get
            {
                return Settings.Notifications.InAppVibrate;
            }
            set
            {
                Settings.Notifications.InAppVibrate = value;
                RaisePropertyChanged();
            }
        }

        public bool InAppPreview
        {
            get
            {
                return Settings.Notifications.InAppPreview;
            }
            set
            {
                Settings.Notifications.InAppPreview = value;
                RaisePropertyChanged();
            }
        }

        #endregion

        public bool IncludeMutedChats
        {
            get
            {
                return Settings.Notifications.IncludeMutedChats;
            }
            set
            {
                Settings.Notifications.IncludeMutedChats = value;
                RaisePropertyChanged();
            }
        }

        public void Handle(UpdateNotificationSettings update)
        {
            if (update.Scope is NotificationSettingsScopePrivateChats)
            {
                BeginOnUIThread(() =>
                {
                    PrivateAlert = update.NotificationSettings.MuteFor == 0;
                    PrivatePreview = update.NotificationSettings.ShowPreview;
                });
            }
            else if (update.Scope is NotificationSettingsScopeBasicGroupChats)
            {
                BeginOnUIThread(() =>
                {
                    GroupAlert = update.NotificationSettings.MuteFor == 0;
                    GroupPreview = update.NotificationSettings.ShowPreview;
                });
            }
        }

        public RelayCommand ResetCommand { get; }
        private void ResetExecute()
        {
            //var confirm = await TLMessageDialog.ShowAsync(Strings.Additional.ResetNotificationsDialogBody, Strings.Additional.ResetNotificationsDialogTitle, Strings.Additional.OK, Strings.Additional.Cancel);
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

            ProtoService.Send(new ResetAllNotificationSettings());
            //}
        }
    }
}
