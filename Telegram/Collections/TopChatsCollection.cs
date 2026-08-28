//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Threading.Tasks;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.Collections
{
    public partial class TopChatsCollection : IncrementalCollection<Chat>
    {
        private readonly IClientService _clientService;
        private readonly TopChatCategory _category;
        private readonly int _limit;

        public TopChatsCollection(IClientService clientService, TopChatCategory category, int limit)
        {
            _clientService = clientService;
            _category = category;
            _limit = limit;
        }

        // One response covers the category, so there is never a second page.
        protected override async Task<IncrementalLoadResult> OnLoadMoreItemsAsync(uint count)
        {
            var totalCount = 0u;

            var response = await _clientService.SendAsync(new GetTopChats(_category, _limit));
            if (response is Chats chats)
            {
                foreach (var chat in _clientService.GetChats(chats.ChatIds))
                {
                    Add(chat);
                    totalCount++;
                }
            }

            return new IncrementalLoadResult(totalCount, false);
        }
    }
}
