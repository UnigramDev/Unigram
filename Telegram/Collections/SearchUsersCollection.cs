//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;

namespace Telegram.Collections
{
    // Not incremental: the three searches are separate requests the caller runs in order, and each
    // one dedupes against the users the ones before it found.
    // TODO: XAML implementation needs refactoring
    public partial class SearchUsersCollection : ObservableCollection<KeyedList<string, object>>
    {
        private readonly IClientService _clientService;
        private readonly string _query;

        private readonly List<long> _users = new();

        private readonly KeyedList<string, object> _local;
        private readonly KeyedList<string, object> _remote;

        public SearchUsersCollection(IClientService clientService, string query)
        {
            _clientService = clientService;
            _query = query;

            _local = new KeyedList<string, object>(null as string);
            _remote = new KeyedList<string, object>(Strings.GlobalSearch);

            Add(_local);
            Add(_remote);
        }

        public string Query => _query;

        public async Task SearchContactsAsync()
        {
            var response = await _clientService.SendAsync(new SearchContacts(_query, 100));
            if (response is Users users)
            {
                foreach (var user in _clientService.GetUsers(users.UserIds))
                {
                    _users.Add(user.Id);
                    _local.Add(new SearchResult(_clientService, user, _query, SearchResultType.Contacts, false));
                }
            }
        }

        public async Task SearchChatsOnServerAsync()
        {
            var response = await _clientService.SendAsync(new SearchChatsOnServer(_query, null, 100));
            if (response is Chats chats)
            {
                foreach (var chat in _clientService.GetChats(chats.ChatIds))
                {
                    if (chat.Type is ChatTypePrivate privata)
                    {
                        if (_users.Contains(privata.UserId))
                        {
                            continue;
                        }

                        _users.Add(privata.UserId);
                        _local.Add(new SearchResult(_clientService, chat, _query, SearchResultType.ChatsOnServer, false));
                    }
                }
            }
        }

        public async Task SearchPublicChatsAsync()
        {
            var response = await _clientService.SendAsync(new SearchPublicChats(_query, null));
            if (response is Chats chats)
            {
                foreach (var chat in _clientService.GetChats(chats.ChatIds))
                {
                    if (chat.Type is ChatTypePrivate privata)
                    {
                        if (_users.Contains(privata.UserId))
                        {
                            continue;
                        }

                        _remote.Add(new SearchResult(_clientService, chat, _query, SearchResultType.PublicChats, false));
                    }
                }
            }
        }
    }
}
