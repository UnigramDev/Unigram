//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views;
using Telegram.Views.Settings;

namespace Telegram.ViewModels.Settings
{
    public partial class SettingsNotificationsViewModel : MultiViewModelBase, IHandle
    {
        public SettingsNotificationsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Scopes = new MvxObservableCollection<SettingsNotificationsScope>
            {
                new SettingsNotificationsScope(clientService, new NotificationSettingsScopePrivateChats(), Strings.NotificationsPrivateChats, Icons.Person),
                new SettingsNotificationsScope(clientService, new NotificationSettingsScopeGroupChats(), Strings.NotificationsGroups, Icons.People),
                new SettingsNotificationsScope(clientService, new NotificationSettingsScopeChannelChats(), Strings.NotificationsChannels, Icons.Megaphone),
            };

            foreach (var scope in Scopes)
            {
                Children.Add(scope);
            }
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            await base.OnNavigatedToAsync(parameter, mode, state);
            RaisePropertyChanged(nameof(IsPinnedEnabled));
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateScopeNotificationSettings>(this, Handle);
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
            get => !ClientService.Options.DisableContactRegisteredNotifications;
            set
            {
                ClientService.Options.DisableContactRegisteredNotifications = !value;
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

        public bool IsAllAccountsAvailable => TypeResolver.Current.GetSessions().Count() > 1;

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

                var unreadCount = ClientService.GetUnreadCount(new ChatListMain());
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

                var unreadCount = ClientService.GetUnreadCount(new ChatListMain());
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

        public async void Reset()
        {
            var confirm = await ShowPopupAsync(Strings.ResetNotificationsAlert, Strings.AppName, Strings.OK, Strings.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                foreach (var scope in Scopes)
                {
                    scope.Reset();
                }

                InAppFlash = true;
                InAppSounds = true;
                InAppPreview = true;

                ClientService.Send(new ResetAllNotificationSettings());
            }
        }
    }

    public partial class SettingsNotificationsScope : ViewModelBase
    {
        private readonly NotificationSettingsScope _scope;
        private readonly string _title;
        private readonly string _glyph;

        public SettingsNotificationsScope(IClientService clientService, NotificationSettingsScope scope, string title, string glyph)
            : base(clientService, null, null)
        {
            _scope = scope;
            _title = title;
            _glyph = glyph;
        }

        public string Title => _title;

        public string Glyph => _glyph;

        private int _muteFor;
        public int MuteFor
        {
            get => _muteFor;
            set
            {
                if (Set(ref _muteFor, value))
                {
                    RaisePropertyChanged(nameof(Alert));
                }
            }
        }

        public bool Alert
        {
            get => _muteFor == 0;
            set
            {
                if (Set(ref _muteFor, value ? 0 : int.MaxValue))
                {
                    RaisePropertyChanged(nameof(MuteFor));
                }
            }
        }

        private bool _preview;
        public bool Preview
        {
            get => _preview;
            set => Set(ref _preview, value);
        }

        private long _soundId;
        public long SoundId
        {
            get => _soundId;
            set => Set(ref _soundId, value);
        }

        private string _soundTitle;
        public string SoundTitle
        {
            get => _soundTitle;
            set => Set(ref _soundTitle, value);
        }

        private string _exceptionsCount = string.Empty;
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
            SoundId = 0;
        }

        public void Update(UpdateScopeNotificationSettings update)
        {
            if (update.Scope.GetType() == _scope.GetType())
            {
                BeginOnUIThread(() =>
                {
                    MuteFor = update.NotificationSettings.MuteFor;
                    Preview = update.NotificationSettings.ShowPreview;
                    SoundId = update.NotificationSettings.SoundId;

                    _disablePinnedMessage = false;
                    _disableMention = false;
                });
            }
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var settings = await ClientService.SendAsync(new GetScopeNotificationSettings(_scope)) as ScopeNotificationSettings;
            if (settings != null)
            {
                MuteFor = settings.MuteFor;
                Preview = settings.ShowPreview;
                SoundId = settings.SoundId;

                _disablePinnedMessage = settings.DisablePinnedMessageNotifications;
                _disableMention = settings.DisableMentionNotifications;

                ReloadSound();
            }

            ExceptionsCount = Alert ? Strings.NotificationsOn : Strings.NotificationsOff;

            var chats = await ClientService.SendAsync(new GetChatNotificationSettingsExceptions(_scope, false)) as Telegram.Td.Api.Chats;
            if (chats?.ChatIds.Count > 0)
            {
                ExceptionsCount = string.Format("{0}, {1}", Alert ? Strings.NotificationsOn : Strings.NotificationsOff, Locale.Declension(Strings.R.Exception, chats.ChatIds.Count));
            }
        }

        public async void Save()
        {
            var settings = await ClientService.SendAsync(new GetScopeNotificationSettings(_scope)) as ScopeNotificationSettings;
            if (settings != null)
            {
                await ClientService.SendAsync(new SetScopeNotificationSettings(_scope, new ScopeNotificationSettings(_muteFor, _soundId, _preview, settings.UseDefaultMuteStories, settings.MuteStories, settings.StorySoundId, settings.ShowStorySender, _disablePinnedMessage, _disableMention)));

                ReloadSound();
            }
        }

        public async Task ToggleDisablePinnedMessage(bool disable)
        {
            var settings = await ClientService.SendAsync(new GetScopeNotificationSettings(_scope)) as ScopeNotificationSettings;
            if (settings != null)
            {
                await ClientService.SendAsync(new SetScopeNotificationSettings(_scope, new ScopeNotificationSettings(settings.MuteFor, settings.SoundId, settings.ShowPreview, settings.UseDefaultMuteStories, settings.MuteStories, settings.StorySoundId, settings.ShowStorySender, disable, settings.DisableMentionNotifications)));
            }
        }

        public void Exceptions()
        {
            switch (_scope)
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

        private async void ReloadSound()
        {
            if (_soundId != 0)
            {
                var response = await ClientService.SendAsync(new GetSavedNotificationSound(_soundId));
                if (response is NotificationSound sound)
                {
                    SoundTitle = sound.Title;
                }
                else
                {
                    SoundTitle = Strings.SoundDefault;
                }
            }
            else
            {
                SoundTitle = Strings.NoSound;
            }
        }
    }
}
