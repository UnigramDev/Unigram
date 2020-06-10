using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Unigram.Views.Settings;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsNotificationsViewModel : TLMultipleViewModelBase, IHandle<UpdateScopeNotificationSettings>
    {
        public SettingsNotificationsViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Scopes = new MvxObservableCollection<SettingsNotificationsScope>
            {
                new SettingsNotificationsScope(protoService, typeof(NotificationSettingsScopePrivateChats), Strings.Resources.NotificationsForPrivateChats),
                new SettingsNotificationsScope(protoService, typeof(NotificationSettingsScopeGroupChats), Strings.Resources.NotificationsForGroups),
                new SettingsNotificationsScope(protoService, typeof(NotificationSettingsScopeChannelChats), Strings.Resources.NotificationsForChannels),
            };

            foreach (var scope in Scopes)
            {
                Children.Add(scope);
            }

            ResetCommand = new RelayCommand(ResetExecute);

            Aggregator.Subscribe(this);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            foreach (var scope in Scopes)
            {
                scope.Update();
            }

            return Task.CompletedTask;
        }

        public MvxObservableCollection<SettingsNotificationsScope> Scopes { get; private set; }

        #region InApp

        public bool InAppFlash
        {
            get
            {
                return Settings.Notifications.InAppFlash;
            }
            set
            {
                Settings.Notifications.InAppFlash = value;
                RaisePropertyChanged();
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

        public bool IsAllAccountsNotifications
        {
            get
            {
                return SettingsService.Current.IsAllAccountsNotifications;
            }
            set
            {
                SettingsService.Current.IsAllAccountsNotifications = value;
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

                var unreadCount = CacheService.GetUnreadCount(new ChatListMain());
                Aggregator.Publish(unreadCount.UnreadChatCount);
                Aggregator.Publish(unreadCount.UnreadMessageCount);
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

                var unreadCount = CacheService.GetUnreadCount(new ChatListMain());
                Aggregator.Publish(unreadCount.UnreadChatCount);
                Aggregator.Publish(unreadCount.UnreadMessageCount);
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
            var confirm = await MessagePopup.ShowAsync(Strings.Resources.ResetNotificationsAlert, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                foreach (var scope in Scopes)
                {
                    scope.Reset();
                }

                InAppFlash = true;
                InAppSounds = true;
                InAppPreview = true;

                ProtoService.Send(new ResetAllNotificationSettings());
            }
        }
    }

    public class SettingsNotificationsScope : TLViewModelBase
    {
        private readonly Type _type;
        private readonly string _title;

        public SettingsNotificationsScope(IProtoService protoService, Type type, string title)
            : base(protoService, null, null, null)
        {
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

        private string _exceptionsCount;
        public string ExceptionsCount
        {
            get { return _exceptionsCount; }
            set { Set(ref _exceptionsCount, value); }
        }

        private bool _disableMention;
        private bool _disablePinnedMessage;

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
                    _disablePinnedMessage = false;
                    _disableMention = false;
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
                        _disablePinnedMessage = settings.DisablePinnedMessageNotifications;
                        _disableMention = settings.DisableMentionNotifications;
                    });
                }
            });

            ProtoService.Send(new GetChatNotificationSettingsExceptions(GetScope(), false), result =>
            {
                if (result is Telegram.Td.Api.Chats chats)
                {
                    BeginOnUIThread(() =>
                    {
                        ExceptionsCount = Locale.Declension("Chats", chats.ChatIds.Count);
                    });
                }
            });
        }

        public RelayCommand SendCommand { get; }
        public async void SendExecute()
        {
            await ProtoService.SendAsync(new SetScopeNotificationSettings(GetScope(), new ScopeNotificationSettings(_alert ? 0 : int.MaxValue, string.Empty, _preview, _disablePinnedMessage, _disableMention)));
        }

        public RelayCommand ExceptionsCommand { get; }
        public void ExceptionsExecute()
        {
            switch (GetScope())
            {
                case NotificationSettingsScopePrivateChats privateChats:
                    NavigationService.Navigate(typeof(SettingsNotificationsExceptionsPage), SettingsNotificationsExceptionsScope.PrivateChats);
                    break;
                case NotificationSettingsScopeGroupChats groupChats:
                    NavigationService.Navigate(typeof(SettingsNotificationsExceptionsPage), SettingsNotificationsExceptionsScope.GroupChats);
                    break;
                case NotificationSettingsScopeChannelChats channelChats:
                    NavigationService.Navigate(typeof(SettingsNotificationsExceptionsPage), SettingsNotificationsExceptionsScope.ChannelChats);
                    break;
            }
        }

        private NotificationSettingsScope GetScope()
        {
            return Activator.CreateInstance(_type) as NotificationSettingsScope;
        }
    }
}
