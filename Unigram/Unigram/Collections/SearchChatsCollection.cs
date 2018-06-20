using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace Unigram.Collections
{
    public class SearchChatsCollection : ObservableCollection<KeyedList<string, object>>, ISupportIncrementalLoading
    {
        private readonly IProtoService _protoService;
        private readonly string _query;

        private readonly List<long> _chats = new List<long>();
        private readonly List<int> _users = new List<int>();

        private KeyedList<string, object> _local;
        private KeyedList<string, object> _remote;
        private KeyedList<string, object> _messages;

        public SearchChatsCollection(IProtoService protoService, string query)
        {
            _protoService = protoService;
            _query = query;

            _local = new KeyedList<string, object>(null as string);
            _remote = new KeyedList<string, object>(Strings.Resources.GlobalSearch);
            _messages = new KeyedList<string, object>(Strings.Resources.SearchMessages);

            Add(_local);
            Add(_remote);
            Add(_messages);
        }

        public string Query => _query;

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint phase)
        {
            return AsyncInfo.Run(async token =>
            {
                // If the query string is empty we want to load recent chats only
                var empty = string.IsNullOrEmpty(_query);
                if (empty && phase > 0)
                {
                    return new LoadMoreItemsResult();
                }

                if (phase == 0)
                {
                    var response = await _protoService.SendAsync(new SearchChats(_query, 100));
                    if (response is Chats chats)
                    {
                        foreach (var id in chats.ChatIds)
                        {
                            var chat = _protoService.GetChat(id);
                            if (chat != null)
                            {
                                if (chat.Type is ChatTypePrivate privata)
                                {
                                    _users.Add(privata.UserId);
                                }

                                _chats.Add(id);
                                _local.Add(new SearchResult(chat, _query, false));
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
                    if (response is Chats chats && _local != null)
                    {
                        foreach (var id in chats.ChatIds)
                        {
                            if (_chats.Contains(id))
                            {
                                continue;
                            }

                            var chat = _protoService.GetChat(id);
                            if (chat != null)
                            {
                                if (chat.Type is ChatTypePrivate privata && _users.Contains(privata.UserId))
                                {
                                    continue;
                                }

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
                            if (chat != null)
                            {
                                _remote.Add(new SearchResult(chat, _query, true));
                            }
                        }
                    }
                }
                else if (phase == 4)
                {
                    var response = await _protoService.SendAsync(new SearchMessages(_query, int.MaxValue, 0, 0, 100));
                    if (response is Messages messages)
                    {
                        foreach (var message in messages.MessagesValue)
                        {
                            _messages.Add(message);
                        }
                    }
                }

                return new LoadMoreItemsResult();
            });
        }

        public bool HasMoreItems => false;
    }
}
