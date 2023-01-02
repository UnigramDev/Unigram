//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Services;
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace Unigram.Collections
{
    public class SearchChatMessagesCollection : MvxObservableCollection<Message>, ISupportIncrementalLoading
    {
        private readonly IClientService _clientService;

        private readonly long _chatId;
        private readonly long _threadId;
        private readonly string _query;
        private readonly MessageSender _sender;
        private readonly long _fromMessageId;

        private readonly SearchMessagesFilter _filter;

        public SearchChatMessagesCollection(IClientService clientService, long chatId, long threadId, string query, MessageSender sender, long fromMessageId, SearchMessagesFilter filter)
        {
            _clientService = clientService;

            _chatId = chatId;
            _threadId = threadId;
            _query = query;
            _sender = sender;
            _fromMessageId = fromMessageId;
            _filter = filter;
        }



        public int TotalCount { get; private set; }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return AsyncInfo.Run(async token =>
            {
                var fromMessageId = _fromMessageId;
                var offset = -49;

                var last = this.LastOrDefault();
                if (last != null)
                {
                    fromMessageId = last.Id;
                    offset = 0;
                }

                var response = await _clientService.SendAsync(new SearchChatMessages(_chatId, _query, _sender, fromMessageId, offset, (int)count, _filter, _threadId));
                if (response is Messages messages)
                {
                    TotalCount = messages.TotalCount;
                    AddRange(messages.MessagesValue);

                    return new LoadMoreItemsResult { Count = (uint)messages.MessagesValue.Count };
                }

                return new LoadMoreItemsResult { Count = 0 };
            });
        }

        public bool HasMoreItems => true;
    }
}
