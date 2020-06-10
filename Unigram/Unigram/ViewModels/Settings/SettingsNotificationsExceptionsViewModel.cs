using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Services;
using Windows.Foundation;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsNotificationsExceptionsViewModel : TLViewModelBase
    {
        public SettingsNotificationsExceptionsViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (parameter is SettingsNotificationsExceptionsScope scope)
            {
                switch (scope)
                {
                    case SettingsNotificationsExceptionsScope.PrivateChats:
                        Items = new ItemsCollection(ProtoService, new NotificationSettingsScopePrivateChats());
                        break;
                    case SettingsNotificationsExceptionsScope.GroupChats:
                        Items = new ItemsCollection(ProtoService, new NotificationSettingsScopeGroupChats());
                        break;
                    case SettingsNotificationsExceptionsScope.ChannelChats:
                        Items = new ItemsCollection(ProtoService, new NotificationSettingsScopeChannelChats());
                        break;
                }

                RaisePropertyChanged(() => Items);
            }

            return Task.CompletedTask;
        }

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
    }

    public enum SettingsNotificationsExceptionsScope
    {
        PrivateChats,
        GroupChats,
        ChannelChats
    }
}
