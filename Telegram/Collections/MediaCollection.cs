//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.ObjectModel;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace Telegram.Collections
{
    public class MediaCollection : ObservableCollection<MessageWithOwner>, ISupportIncrementalLoading
    {
        private readonly IClientService _clientService;
        private readonly SearchMessagesFilter _filter;
        private readonly long _chatId;
        private readonly long _threadId;
        private readonly string _query;

        private long _lastMaxId;
        private bool _hasMore = true;

        public MediaCollection(IClientService clientService, long chatId, long threadId, SearchMessagesFilter filter, string query = null)
        {
            _clientService = clientService;
            _chatId = chatId;
            _threadId = threadId;
            _filter = filter;
            _query = query ?? string.Empty;
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return AsyncInfo.Run(async token =>
            {
                var count = 0u;

                var response = await _clientService.SendAsync(new SearchChatMessages(_chatId, _query, null, _lastMaxId, 0, 50, _filter, _threadId));
                if (response is FoundChatMessages messages)
                {
                    if (messages.NextFromMessageId != 0)
                    {
                        _lastMaxId = messages.NextFromMessageId;
                        _hasMore = true;
                    }
                    else
                    {
                        _hasMore = false;
                    }

                    foreach (var message in messages.Messages)
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
