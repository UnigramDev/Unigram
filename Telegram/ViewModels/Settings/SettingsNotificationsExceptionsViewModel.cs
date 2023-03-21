//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.Foundation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Settings
{
    public class SettingsNotificationsExceptionsViewModel : TLMultipleViewModelBase
    {
        public SettingsNotificationsExceptionsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            RemoveCommand = new RelayCommand<Chat>(RemoveExecute);
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is SettingsNotificationsExceptionsScope scope)
            {
                switch (scope)
                {
                    case SettingsNotificationsExceptionsScope.PrivateChats:
                        Scope = new SettingsNotificationsScope(ClientService, typeof(NotificationSettingsScopePrivateChats), Strings.NotificationsPrivateChats, Icons.Person);
                        Items = new ItemsCollection(ClientService, new NotificationSettingsScopePrivateChats());
                        break;
                    case SettingsNotificationsExceptionsScope.GroupChats:
                        Scope = new SettingsNotificationsScope(ClientService, typeof(NotificationSettingsScopeGroupChats), Strings.NotificationsGroups, Icons.People);
                        Items = new ItemsCollection(ClientService, new NotificationSettingsScopeGroupChats());
                        break;
                    case SettingsNotificationsExceptionsScope.ChannelChats:
                        Scope = new SettingsNotificationsScope(ClientService, typeof(NotificationSettingsScopeChannelChats), Strings.NotificationsChannels, Icons.Megaphone);
                        Items = new ItemsCollection(ClientService, new NotificationSettingsScopeChannelChats());
                        break;
                }

                RaisePropertyChanged(nameof(Scope));
                RaisePropertyChanged(nameof(Items));

                Children.Add(Scope);
            }

            return base.OnNavigatedToAsync(parameter, mode, state);
        }

        public SettingsNotificationsScope Scope { get; private set; }

        public ItemsCollection Items { get; private set; }

        public class ItemsCollection : MvxObservableCollection<Chat>, ISupportIncrementalLoading
        {
            private readonly IClientService _clientService;
            private readonly NotificationSettingsScope _scope;

            private bool _hasMoreItems = true;

            public ItemsCollection(IClientService clientService, NotificationSettingsScope scope)
            {
                _clientService = clientService;
                _scope = scope;
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(async token =>
                {
                    var response = await _clientService.SendAsync(new GetChatNotificationSettingsExceptions(_scope, false));
                    if (response is Telegram.Td.Api.Chats chats)
                    {
                        foreach (var id in chats.ChatIds)
                        {
                            var chat = _clientService.GetChat(id);
                            if (chat != null)
                            {
                                Add(chat);
                            }
                        }

                        _hasMoreItems = false;
                        return new LoadMoreItemsResult { Count = (uint)chats.ChatIds.Count };
                    }

                    _hasMoreItems = false;
                    return new LoadMoreItemsResult();
                });
            }

            public bool HasMoreItems => _hasMoreItems;
        }

        public async void ChooseSound()
        {
            var tsc = new TaskCompletionSource<object>();

            var confirm = await NavigationService.ShowAsync(typeof(ChooseSoundPopup), Scope.SoundId, tsc);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var selected = await tsc.Task;
            if (selected is NotificationSound sound)
            {
                Scope.SoundId = sound.Id;
                Scope.SendExecute();
            }
        }

        public RelayCommand<Chat> RemoveCommand { get; }
        private void RemoveExecute(Chat chat)
        {
            Items.Remove(chat);

            ClientService.Send(new SetChatNotificationSettings(chat.Id, new ChatNotificationSettings
            {
                UseDefaultMuteFor = true,
                UseDefaultSound = true,
                UseDefaultShowPreview = true,
                UseDefaultDisableMentionNotifications = true,
                UseDefaultDisablePinnedMessageNotifications = true,
            }));
        }
    }

    public enum SettingsNotificationsExceptionsScope
    {
        PrivateChats,
        GroupChats,
        ChannelChats
    }
}
