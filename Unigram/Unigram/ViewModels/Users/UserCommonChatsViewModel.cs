using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Services;
using Windows.Foundation;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Users
{
    public class UserCommonChatsViewModel : TLViewModelBase
    {
        public UserCommonChatsViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (parameter is long chatId)
            {
                var chat = CacheService.GetChat(chatId);
                if (chat == null)
                {
                    return Task.CompletedTask;
                }

                var user = CacheService.GetUser(chat);
                if (user == null)
                {
                    return Task.CompletedTask;
                }

                Items = new ItemsCollection(ProtoService, user.Id);
                RaisePropertyChanged(() => Items);
            }
            else if (parameter is int userId)
            {
                Items = new ItemsCollection(ProtoService, userId);
                RaisePropertyChanged(() => Items);
            }

            return Task.CompletedTask;
        }

        public ItemsCollection Items { get; private set; }

        public class ItemsCollection : MvxObservableCollection<Chat>, ISupportIncrementalLoading
        {
            private readonly IProtoService _protoService;
            private readonly int _userId;

            public ItemsCollection(IProtoService protoService, int userId)
            {
                _protoService = protoService;
                _userId = userId;
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(async token =>
                {
                    var offset = 0L;

                    var last = this.LastOrDefault();
                    if (last != null)
                    {
                        offset = last.Id;
                    }

                    var response = await _protoService.SendAsync(new GetGroupsInCommon(_userId, offset, 20));
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

                        return new LoadMoreItemsResult { Count = (uint)chats.ChatIds.Count };
                    }

                    return new LoadMoreItemsResult();
                });
            }

            public bool HasMoreItems => true;
        }
    }
}
