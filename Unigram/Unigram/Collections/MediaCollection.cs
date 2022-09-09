using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Services;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace Unigram.Collections
{
    public class MediaCollection : ObservableCollection<MessageWithOwner>, ISupportIncrementalLoading
    {
        private readonly IClientService _clientService;
        private readonly SearchMessagesFilter _filter;
        private readonly long _chatId;
        private readonly string _query;

        private long _lastMaxId;
        private bool _hasMore = true;

        public MediaCollection(IClientService clientService, long chatId, SearchMessagesFilter filter, string query = null)
        {
            _clientService = clientService;
            _chatId = chatId;
            _filter = filter;
            _query = query ?? string.Empty;
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return AsyncInfo.Run(async token =>
            {
                var count = 0u;

                var response = await _clientService.SendAsync(new SearchChatMessages(_chatId, _query, null, _lastMaxId, 0, 50, _filter, 0));
                if (response is Messages messages)
                {
                    if (messages.MessagesValue.Count > 0)
                    {
                        _lastMaxId = messages.MessagesValue.Min(x => x.Id);
                        _hasMore = true;
                    }
                    else
                    {
                        _hasMore = false;
                    }

                    foreach (var message in messages.MessagesValue)
                    {
                        Add(new MessageWithOwner(_clientService, message));
                        count++;
                    }
                }

                return new LoadMoreItemsResult { Count = count };
            });
        }

        public bool HasMoreItems => _hasMore;
    }
}
