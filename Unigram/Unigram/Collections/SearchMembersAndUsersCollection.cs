using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Services;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace Unigram.Collections
{
    public class SearchMembersAndUsersCollection : ObservableCollection<KeyedList<string, object>>, ISupportIncrementalLoading
    {
        private readonly IProtoService _protoService;
        private readonly long _chatId;
        private readonly ChatMembersFilter _filter;
        private readonly string _query;

        private readonly List<int> _users = new List<int>();

        private KeyedList<string, object> _chat;
        private KeyedList<string, object> _local;
        private KeyedList<string, object> _remote;

        public SearchMembersAndUsersCollection(IProtoService protoService, long chatId, ChatMembersFilter filter, string query)
        {
            _protoService = protoService;
            _chatId = chatId;
            _filter = filter;
            _query = query;

            _chat = new KeyedList<string, object>(null as string);
            _local = new KeyedList<string, object>(Strings.Resources.Contacts.ToUpper());
            _remote = new KeyedList<string, object>(Strings.Resources.GlobalSearch);

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
                    var response = await _protoService.SendAsync(new SearchChatMembers(_chatId, _query, 100, _filter));
                    if (response is ChatMembers members)
                    {
                        foreach (var member in members.Members)
                        {
                            var user = _protoService.GetUser(member.UserId);
                            if (user != null)
                            {
                                _users.Add(member.UserId);
                                _chat.Add(new SearchResult(user, _query, false));
                            }
                        }
                    }
                }
                else if (phase == 1)
                {
                    var response = await _protoService.SendAsync(new SearchContacts(_query, 100));
                    if (response is Users users)
                    {
                        foreach (var id in users.UserIds)
                        {
                            if (_users.Contains(id))
                            {
                                continue;
                            }

                            var user = _protoService.GetUser(id);
                            if (user != null)
                            {
                                _users.Add(id);
                                _local.Add(new SearchResult(user, _query, false));
                            }
                        }
                    }
                }
                else if (phase == 2)
                {
                    var response = await _protoService.SendAsync(new SearchChatsOnServer(_query, 100));
                    if (response is Chats chats)
                    {
                        foreach (var id in chats.ChatIds)
                        {
                            var chat = _protoService.GetChat(id);
                            if (chat != null && chat.Type is ChatTypePrivate privata)
                            {
                                if (_users.Contains(privata.UserId))
                                {
                                    continue;
                                }

                                _users.Add(privata.UserId);
                                _local.Add(new SearchResult(chat, _query, false));
                            }
                        }
                    }
                }
                else if (phase == 3)
                {
                    var response = await _protoService.SendAsync(new SearchPublicChats(_query));
                    if (response is Chats chats)
                    {
                        foreach (var id in chats.ChatIds)
                        {
                            var chat = _protoService.GetChat(id);
                            if (chat != null && chat.Type is ChatTypePrivate privata)
                            {
                                if (_users.Contains(privata.UserId))
                                {
                                    continue;
                                }

                                _remote.Add(new SearchResult(chat, _query, true));
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
