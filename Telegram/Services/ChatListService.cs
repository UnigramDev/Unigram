//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Threading.Tasks;
using Telegram.Td.Api;

namespace Telegram.Services
{
    /// <summary>
    /// Every chat TDLib has told us about for one chat list, in order.
    /// </summary>
    /// <remarks>
    /// The chats themselves belong to <see cref="ClientService"/> and are shared with every other
    /// list; what this owns is the order.
    /// <para/>
    /// Its <see cref="OrderedSourceService{TItem}.Changed"/> is raised with the chat's own lock
    /// held, since <see cref="ClientService.SetChatPositions"/> routes positions from inside it: a
    /// handler must marshal to its thread, and must not take that lock.
    /// </remarks>
    public partial class ChatListService : OrderedSourceService<Chat>
    {
        private readonly IClientService _clientService;
        private readonly ChatList _chatList;

        public ChatListService(IClientService clientService, ChatList chatList)
        {
            _clientService = clientService;
            _chatList = chatList;
        }

        public ChatList ChatList => _chatList;

        /// <summary>
        /// Places the chat at <paramref name="order"/>, or takes it out of the list when that is
        /// zero, and reports it.
        /// </summary>
        public void SetOrder(Chat chat, long order, bool lastMessage)
        {
            SetOrder(chat, chat.Id, order, lastMessage);
        }

        /// <summary>
        /// A page of the list, loading from the server whatever it does not hold yet.
        /// </summary>
        /// <remarks>
        /// Answers <see cref="Chats"/> with a TotalCount of -1 once the whole list is held, which
        /// is how a caller tells that there is nothing left to page.
        /// </remarks>
        public async Task<Chats> GetChatsAsync(int offset, int limit)
        {
            var page = await GetItemsAsync(offset, limit);
            return new Chats(page.HaveFullList ? -1 : 0, page.Ids);
        }

        protected override Task<Object> LoadMoreItemsAsync(int count)
        {
            return _clientService.SendAsync(new LoadChats(_chatList, count));
        }
    }
}
