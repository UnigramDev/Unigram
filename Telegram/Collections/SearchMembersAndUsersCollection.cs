//
// Copyright Fela Ameghino 2015-2024
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
    public class SearchMembersAndUsersCollection : ObservableCollection<KeyedList<string, object>>, ISupportIncrementalLoading
    {
        private readonly IClientService _clientService;
        private readonly long _chatId;
        private readonly ChatMembersFilter _filter;
        private readonly string _query;
        private readonly bool _canSendMessageToUser;

        private readonly List<long> _users = new();

        private readonly KeyedList<string, object> _chat;
        private readonly KeyedList<string, object> _local;
        private readonly KeyedList<string, object> _remote;

        public SearchMembersAndUsersCollection(IClientService clientService, long chatId, string query, bool canSendMessageToUser)
        {
            _clientService = clientService;
            _chatId = chatId;
            _filter = new ChatMembersFilterMembers();
            _query = query;
            _canSendMessageToUser = canSendMessageToUser;

            _chat = new KeyedList<string, object>(null as string);
            _local = new KeyedList<string, object>(Strings.Contacts);
            _remote = new KeyedList<string, object>(Strings.GlobalSearch);

            Add(_chat);
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
                    var response = await _clientService.SendAsync(new SearchChatMembers(_chatId, _query, 100, _filter));
                    if (response is ChatMembers members)
                    {
                        foreach (var member in members.Members)
                        {
                            if (_clientService.TryGetUser(member.MemberId, out User user))
                            {
                                _users.Add(user.Id);
                                _chat.Add(new SearchResult(_clientService, user, _query, SearchResultType.ChatMembers, false));
                            }
                        }
                    }
                }
                else if (phase == 1)
                {
                    var response = await _clientService.SendAsync(new SearchContacts(_query, 100));
                    if (response is Users users)
                    {
                        foreach (var id in users.UserIds)
                        {
                            if (_users.Contains(id))
                            {
                                continue;
                            }

                            var user = _clientService.GetUser(id);
                            if (user != null)
                            {
                                _users.Add(id);
                                _local.Add(new SearchResult(_clientService, user, _query, SearchResultType.Contacts, _canSendMessageToUser));
                            }
                        }
                    }
                }
                else if (phase == 2)
                {
                    var response = await _clientService.SendAsync(new SearchChatsOnServer(_query, 100));
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

                                _users.Add(privata.UserId);
                                _local.Add(new SearchResult(_clientService, chat, _query, SearchResultType.ChatsOnServer, _canSendMessageToUser));
                            }
                        }
                    }
                }
                else if (phase == 3)
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

                                _remote.Add(new SearchResult(_clientService, chat, _query, SearchResultType.PublicChats, _canSendMessageToUser));
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
