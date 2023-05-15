//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace Telegram.Collections
{
    public class SearchUsersCollection : ObservableCollection<KeyedList<string, object>>, ISupportIncrementalLoading
    {
        private readonly IClientService _clientService;
        private readonly string _query;

        private readonly List<long> _users = new List<long>();

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

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint phase)
        {
            return AsyncInfo.Run(async token =>
            {
                if (phase == 0)
                {
                    var response = await _clientService.SendAsync(new SearchContacts(_query, 100));
                    if (response is Users users)
                    {
                        foreach (var id in users.UserIds)
                        {
                            var user = _clientService.GetUser(id);
                            if (user != null)
                            {
                                _users.Add(id);
                                _local.Add(new SearchResult(user, _query, SearchResultType.Contacts));
                            }
                        }
                    }
                }
                else if (phase == 1)
                {
                    var response = await _clientService.SendAsync(new SearchChatsOnServer(_query, 100));
                    if (response is Chats chats && _local != null)
                    {
                        foreach (var id in chats.ChatIds)
                        {
                            var chat = _clientService.GetChat(id);
                            if (chat != null && chat.Type is ChatTypePrivate privata)
                            {
                                if (_users.Contains(privata.UserId))
                                {
                                    continue;
                                }

                                _users.Add(privata.UserId);
                                _local.Add(new SearchResult(chat, _query, SearchResultType.ChatsOnServer));
                            }
                        }
                    }
                }
                else if (phase == 2)
                {
                    var response = await _clientService.SendAsync(new SearchPublicChats(_query));
                    if (response is Chats chats)
                    {
                        foreach (var id in chats.ChatIds)
                        {
                            var chat = _clientService.GetChat(id);
                            if (chat != null && chat.Type is ChatTypePrivate privata)
                            {
                                if (_users.Contains(privata.UserId))
                                {
                                    continue;
                                }

                                _remote.Add(new SearchResult(chat, _query, SearchResultType.PublicChats));
                            }
                        }
                    }
                }

                return new LoadMoreItemsResult();
            });
        }

        public bool HasMoreItems => false;
    }
}
