using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Template10.Mvvm;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Windows.Foundation.Metadata;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsNotificationsViewModel : TLMultipleViewModelBase, IHandle<UpdateScopeNotificationSettings>
    {
        private readonly IVibrationService _vibrationService;

        private bool _suppressUpdating;

        public SettingsNotificationsViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, IVibrationService vibrationService) 
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _vibrationService = vibrationService;

            Scopes = new MvxObservableCollection<SettingsNotificationsScope>
            {
                new SettingsNotificationsScope(protoService, typeof(NotificationSettingsScopePrivateChats), Strings.Resources.NotificationsForPrivateChats),
                new SettingsNotificationsScope(protoService, typeof(NotificationSettingsScopeGroupChats), Strings.Resources.NotificationsForGroups),
                //new SettingsNotificationsScope(protoService, typeof(NotificationSettingsScopeChannels), Strings.Resources.NotificationsForChannels),
            };

            foreach (var scope in Scopes)
            {
                ChildViewModels.Add(scope);
            }

            ResetCommand = new RelayCommand(ResetExecute);

            Aggregator.Subscribe(this);
            PropertyChanged += OnPropertyChanged;
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            foreach (var scope in Scopes)
            {
                scope.Update();
            }

            IsVibrationAvailable = await _vibrationService.GetAvailabilityAsync();
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

        public MvxObservableCollection<SettingsNotificationsScope> Scopes { get; private set; }

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

        public bool IsContactEnabled
        {
            get
            {
                return !CacheService.Options.DisableContactRegisteredNotifications && Settings.Notifications.IsContactEnabled;
            }
            set
            {
                CacheService.Options.DisableContactRegisteredNotifications = !(Settings.Notifications.IsContactEnabled = value);
                RaisePropertyChanged();
            }
        }

        public bool IsPinnedEnabled
        {
            get
            {
                return !CacheService.Options.DisablePinnedMessageNotifications && Settings.Notifications.IsPinnedEnabled;
            }
            set
            {
                CacheService.Options.DisablePinnedMessageNotifications = !(Settings.Notifications.IsPinnedEnabled = value);
                RaisePropertyChanged();
            }
        }

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
                Aggregator.Publish(CacheService.UnreadChatCount);
                Aggregator.Publish(CacheService.UnreadMessageCount);
            }
        }

        public bool CountUnreadMessages
        {
            get
            {
                return Settings.Notifications.CountUnreadMessages;
            }
            set
            {
                Settings.Notifications.CountUnreadMessages = value;
                RaisePropertyChanged();
                Aggregator.Publish(CacheService.UnreadChatCount);
                Aggregator.Publish(CacheService.UnreadMessageCount);
            }
        }

        public void Handle(UpdateScopeNotificationSettings update)
        {
            foreach (var scope in Scopes)
            {
                scope.Update(update);
            }
        }

        public RelayCommand ResetCommand { get; }
        private async void ResetExecute()
        {
            var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.ResetNotificationsAlert, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                foreach (var scope in Scopes)
                {
                    scope.Reset();
                }

                _suppressUpdating = true;
                InAppSounds = true;
                InAppPreview = true;
                InAppVibrate = true;
                _suppressUpdating = false;

                ProtoService.Send(new ResetAllNotificationSettings());
            }
        }
    }

    public class SettingsNotificationsScope : TLViewModelBase
    {
        private readonly IProtoService _protoService;
        private readonly Type _type;
        private readonly string _title;

        public SettingsNotificationsScope(IProtoService protoService, Type type, string title)
            : base(protoService, null, null, null)
        {
            _protoService = protoService;
            _type = type;
            _title = title;
        }

        public string Title => _title;

        private bool _alert;
        public bool Alert
        {
            get { return _alert; }
            set { Set(ref _alert, value); }
        }

        private bool _preview;
        public bool Preview
        {
            get { return _preview; }
            set { Set(ref _preview, value); }
        }

        private string _sound;
        public string Sound
        {
            get { return _sound; }
            set { Set(ref _sound, value); }
        }

        public void Reset()
        {
            Alert = true;
            Preview = true;
            Sound = string.Empty;
        }

        public void Update(UpdateScopeNotificationSettings update)
        {
            if (update.Scope.GetType() == _type)
            {
                BeginOnUIThread(() =>
                {
                    Alert = update.NotificationSettings.MuteFor == 0;
                    Preview = update.NotificationSettings.ShowPreview;
                    Sound = string.Empty;
                });
            }
        }

        public void Update()
        {
            ProtoService.Send(new GetScopeNotificationSettings(GetScope()), result =>
            {
                if (result is ScopeNotificationSettings settings)
                {
                    BeginOnUIThread(() =>
                    {
                        Alert = settings.MuteFor == 0;
                        Preview = settings.ShowPreview;
                        Sound = string.Empty;
                    });
                }
            });
        }

        public RelayCommand SendCommand { get; }
        public async void SendExecute()
        {
            await _protoService.SendAsync(new SetScopeNotificationSettings(GetScope(), new ScopeNotificationSettings(_alert ? 0 : int.MaxValue, string.Empty, _preview)));
        }

        private NotificationSettingsScope GetScope()
        {
            return Activator.CreateInstance(_type) as NotificationSettingsScope;
        }
    }
}
