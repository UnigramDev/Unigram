//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Threading.Tasks;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;

namespace Telegram.Collections
{
    public partial class MediaCollection : IncrementalCollection<MessageWithOwner>
    {
        private readonly IClientService _clientService;
        private readonly SearchMessagesFilter _filter;
        private readonly long _chatId;
        private readonly MessageTopic _topic;
        private readonly string _query;

        private string _nextOffset;
        private long _nextFromMessageId;

        public SearchMessagesFilter Filter => _filter;

        public MediaCollection(IClientService clientService, long chatId, MessageTopic topic, SearchMessagesFilter filter, string query = null)
        {
            _clientService = clientService;
            _chatId = chatId;
            _topic = topic;
            _filter = filter;
            _query = query ?? string.Empty;
        }

        public MediaCollection(IClientService clientService, SearchMessagesFilter filter, string query = null)
        {
            _clientService = clientService;
            _filter = filter;
            _query = query ?? string.Empty;
        }

        protected override async Task<IncrementalLoadResult> OnLoadMoreItemsAsync(uint count)
        {
            var totalCount = 0u;
            var hasMoreItems = false;

            Function func;
            if (_chatId != 0)
            {
                func = new SearchChatMessages(_chatId, _topic, _query, null, _nextFromMessageId, 0, 50, _filter);
            }
            else
            {
                func = new SearchMessages(new ChatListMain(), _query, _nextOffset ?? string.Empty, 50, _filter, null, 0, 0);
            }

            var response = await _clientService.SendAsync(func);
            if (response is FoundChatMessages foundChatMessages)
            {
                if (foundChatMessages.NextFromMessageId != 0)
                {
                    _nextFromMessageId = foundChatMessages.NextFromMessageId;
                    hasMoreItems = foundChatMessages.NextFromMessageId != 0;
                }

                foreach (var message in foundChatMessages.Messages)
                {
                    Add(new MessageWithOwner(_clientService, message));
                    totalCount++;
                }
            }
            else if (response is FoundMessages foundMessages)
            {
                if (foundMessages.NextOffset.Length > 0)
                {
                    _nextOffset = foundMessages.NextOffset;
                    hasMoreItems = foundMessages.NextOffset.Length > 0;
                }

                foreach (var message in foundMessages.Messages)
                {
                    Add(new MessageWithOwner(_clientService, message));
                    totalCount++;
                }
            }

            return new IncrementalLoadResult(totalCount, hasMoreItems);
        }
    }
}
