using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Common;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Users
{
    public class UserCommonChatsViewModel : UnigramViewModelBase
    {
        public UserCommonChatsViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator)
        {
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (parameter is TLInputUserBase userId)
            {
                if (Items != null)
                {
                    Items.HasMoreItems = false;
                    Items.Clear();
                }

                Items = new ItemsCollection(ProtoService, userId);
                RaisePropertyChanged(() => Items);
            }

            return Task.CompletedTask;
        }

        public ItemsCollection Items { get; private set; }

        public class ItemsCollection : IncrementalCollection<TLChatBase>
        {
            private readonly IMTProtoService _protoService;
            private readonly TLInputUserBase _userId;

            public ItemsCollection(IMTProtoService protoService, TLInputUserBase userId)
            {
                _protoService = protoService;
                _userId = userId;
            }

            public override async Task<IList<TLChatBase>> LoadDataAsync()
            {
                var offset = Count == 0 ? 0 : this[Count - 1].Id;
                var limit = Count == 0 ? 50 : 100;

                var response = await _protoService.GetCommonChatsAsync(_userId, offset, limit);
                if (response.IsSucceeded)
                {
                    return response.Result.Chats;
                }

                return new TLChatBase[0];
            }
        }
    }
}
