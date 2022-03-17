using System;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.Views;
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
                new SettingsNotificationsScope(protoService, typeof(NotificationSettingsScopePrivateChats), Strings.Resources.NotificationsPrivateChats, Icons.Person),
                new SettingsNotificationsScope(protoService, typeof(NotificationSettingsScopeGroupChats), Strings.Resources.NotificationsGroups, Icons.People),
                new SettingsNotificationsScope(protoService, typeof(NotificationSettingsScopeChannelChats), Strings.Resources.NotificationsChannels, Icons.Megaphone),
            };

            foreach (var scope in Scopes)
            {
                Children.Add(scope);
            }

            ResetCommand = new RelayCommand(ResetExecute);

            Aggregator.Subscribe(this);
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            await base.OnNavigatedToAsync(parameter, mode, state);
            RaisePropertyChanged(nameof(IsPinnedEnabled));
        }

        public MvxObservableCollection<SettingsNotificationsScope> Scopes { get; private set; }

        #region InApp

        public bool InAppFlash
        {
            get => Settings.Notifications.InAppFlash;
            set
            {
                Settings.Notifications.InAppFlash = value;
                RaisePropertyChanged();
            }
        }

        public bool InAppSounds
        {
            get => Settings.Notifications.InAppSounds;
            set
            {
                Settings.Notifications.InAppSounds = value;
                RaisePropertyChanged();
            }
        }

        public bool InAppPreview
        {
            get => Settings.Notifications.InAppPreview;
            set
            {
                Settings.Notifications.InAppPreview = value;
                RaisePropertyChanged();
            }
        }

        #endregion

        public bool IsContactEnabled
        {
            get => !CacheService.Options.DisableContactRegisteredNotifications;
            set
            {
                CacheService.Options.DisableContactRegisteredNotifications = !value;
                RaisePropertyChanged();
            }
        }

        public bool IsPinnedEnabled
        {
            get => Scopes.All(x => !x.DisablePinnedMessage);
            set => SetPinnedEnabled(value);
        }
        
        private async void SetPinnedEnabled(bool value)
        {
            foreach (var scope in Scopes)
            {
                await scope.ToggleDisablePinnedMessage(!value);
            }

            RaisePropertyChanged();
        }

        public bool IsAllAccountsAvailable => TLContainer.Current.GetSessions().Count() > 1;

        public bool IsAllAccountsNotifications
        {
            get => SettingsService.Current.IsAllAccountsNotifications;
            set
            {
                SettingsService.Current.IsAllAccountsNotifications = value;
                RaisePropertyChanged();
            }
        }

        public bool IncludeMutedChats
        {
            get => Settings.Notifications.IncludeMutedChats;
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
            get => Settings.Notifications.CountUnreadMessages;
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
        private readonly string _glyph;

        public SettingsNotificationsScope(IProtoService protoService, Type type, string title, string glyph)
            : base(protoService, null, null, null)
        {
            _type = type;
            _title = title;
            _glyph = glyph;
        }

        public string Title => _title;

        public string Glyph => _glyph;

        private bool _alert;
        public bool Alert
        {
            get => _alert;
            set => Set(ref _alert, value);
        }

        private bool _preview;
        public bool Preview
        {
            get => _preview;
            set => Set(ref _preview, value);
        }

        private string _sound;
        public string Sound
        {
            get => _sound;
            set => Set(ref _sound, value);
        }

        private string _exceptionsCount;
        public string ExceptionsCount
        {
            get => _exceptionsCount;
            set => Set(ref _exceptionsCount, value);
        }

        private bool _disableMention;
        public bool DisableMention => _disableMention;

        private bool _disablePinnedMessage;
        public bool DisablePinnedMessage => _disablePinnedMessage;

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

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var settings = await ProtoService.SendAsync(new GetScopeNotificationSettings(GetScope())) as ScopeNotificationSettings;
            if (settings != null)
            {
                Alert = settings.MuteFor == 0;
                Preview = settings.ShowPreview;
                Sound = string.Empty;

                _disablePinnedMessage = settings.DisablePinnedMessageNotifications;
                _disableMention = settings.DisableMentionNotifications;
            }

            var chats = await ProtoService.SendAsync(new GetChatNotificationSettingsExceptions(GetScope(), false)) as Telegram.Td.Api.Chats;
            if (chats != null)
            {
                ExceptionsCount = string.Format("{0}, {1}", Alert ? Strings.Resources.NotificationsOn : Strings.Resources.NotificationsOff, Locale.Declension("Exception", chats.ChatIds.Count));
            }
        }

        public RelayCommand SendCommand { get; }
        public void SendExecute()
        {
            ProtoService.Send(new SetScopeNotificationSettings(GetScope(), new ScopeNotificationSettings(_alert ? 0 : int.MaxValue, string.Empty, _preview, _disablePinnedMessage, _disableMention)));
        }

        public async Task ToggleDisablePinnedMessage(bool disable)
        {
            var settings = await ProtoService.SendAsync(new GetScopeNotificationSettings(GetScope())) as ScopeNotificationSettings;
            if (settings != null)
            {
                await ProtoService.SendAsync(new SetScopeNotificationSettings(GetScope(), new ScopeNotificationSettings(settings.MuteFor, settings.Sound, settings.ShowPreview, disable, settings.DisableMentionNotifications)));
            }
        }

        public RelayCommand ExceptionsCommand { get; }
        public void ExceptionsExecute()
        {
            switch (GetScope())
            {
                case NotificationSettingsScopePrivateChats:
                    NavigationService.Navigate(typeof(SettingsNotificationsExceptionsPage), SettingsNotificationsExceptionsScope.PrivateChats);
                    break;
                case NotificationSettingsScopeGroupChats:
                    NavigationService.Navigate(typeof(SettingsNotificationsExceptionsPage), SettingsNotificationsExceptionsScope.GroupChats);
                    break;
                case NotificationSettingsScopeChannelChats:
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
