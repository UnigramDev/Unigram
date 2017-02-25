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
using Windows.Foundation.Metadata;
using Windows.Phone.Devices.Notification;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsNotificationsViewModel : UnigramViewModelBase, IHandle<TLUpdateNotifySettings>, IHandle
    {
        private bool _suppressUpdating;

        public SettingsNotificationsViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator)
        {
            Aggregator.Subscribe(this);
            PropertyChanged += OnPropertyChanged;
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
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
                VibrationDevice.GetDefault().Vibrate(TimeSpan.FromMilliseconds(300));
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

        public bool IsVibrationAvailable
        {
            get
            {
                return ApiInformation.IsTypePresent("Windows.Phone.Devices.Notification.VibrationDevice") && VibrationDevice.GetDefault() != null;
            }
        }

        private bool _inAppSounds;
        public bool InAppSounds
        {
            get
            {
                return _inAppSounds;
            }
            set
            {
                Set(ref _inAppSounds, value);
            }
        }

        private bool _inAppVibrate;
        public bool InAppVibrate
        {
            get
            {
                return _inAppVibrate;
            }
            set
            {
                Set(ref _inAppVibrate, value);
            }
        }

        private bool _inAppPreview;
        public bool InAppPreview
        {
            get
            {
                return _inAppPreview;
            }
            set
            {
                Set(ref _inAppPreview, value);
            }
        }

        #endregion

        private async Task UpdateAsync()
        {
            ProtoService.GetNotifySettingsCallback(new TLInputNotifyUsers(), result =>
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

            ProtoService.GetNotifySettingsCallback(new TLInputNotifyChats(), result =>
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
    }
}
