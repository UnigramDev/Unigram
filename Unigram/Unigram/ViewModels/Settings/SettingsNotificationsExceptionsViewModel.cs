using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.Views.Popups;
using Windows.Foundation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsNotificationsExceptionsViewModel : TLMultipleViewModelBase
    {
        public SettingsNotificationsExceptionsViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            ChooseSoundCommand = new RelayCommand(ChooseSound);
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is SettingsNotificationsExceptionsScope scope)
            {
                switch (scope)
                {
                    case SettingsNotificationsExceptionsScope.PrivateChats:
                        Scope = new SettingsNotificationsScope(ProtoService, typeof(NotificationSettingsScopePrivateChats), Strings.Resources.NotificationsPrivateChats, Icons.Person);
                        Items = new ItemsCollection(ProtoService, new NotificationSettingsScopePrivateChats());
                        break;
                    case SettingsNotificationsExceptionsScope.GroupChats:
                        Scope = new SettingsNotificationsScope(ProtoService, typeof(NotificationSettingsScopeGroupChats), Strings.Resources.NotificationsGroups, Icons.People);
                        Items = new ItemsCollection(ProtoService, new NotificationSettingsScopeGroupChats());
                        break;
                    case SettingsNotificationsExceptionsScope.ChannelChats:
                        Scope = new SettingsNotificationsScope(ProtoService, typeof(NotificationSettingsScopeChannelChats), Strings.Resources.NotificationsChannels, Icons.Megaphone);
                        Items = new ItemsCollection(ProtoService, new NotificationSettingsScopeChannelChats());
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
            private readonly IProtoService _protoService;
            private readonly NotificationSettingsScope _scope;

            private bool _hasMoreItems = true;

            public ItemsCollection(IProtoService protoService, NotificationSettingsScope scope)
            {
                _protoService = protoService;
                _scope = scope;
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(async token =>
                {
                    var response = await _protoService.SendAsync(new GetChatNotificationSettingsExceptions(_scope, false));
                    if (response is Telegram.Td.Api.Chats chats)
                    {
                        foreach (var id in chats.ChatIds)
                        {
                            var chat = _protoService.GetChat(id);
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

        public RelayCommand ChooseSoundCommand { get; }
        private async void ChooseSound()
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
    }

    public enum SettingsNotificationsExceptionsScope
    {
        PrivateChats,
        GroupChats,
        ChannelChats
    }
}
