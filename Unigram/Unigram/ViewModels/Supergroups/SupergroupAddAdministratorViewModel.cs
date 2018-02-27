using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using TdWindows;
using Unigram.Common;
using Unigram.Services;
using Windows.Foundation;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Supergroups
{
    public class SupergroupAddAdministratorViewModel : SupergroupMembersViewModelBase
    {
        public SupergroupAddAdministratorViewModel(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator)
            : base(protoService, cacheService, aggregator, new SupergroupMembersFilterRecent(), query => new SupergroupMembersFilterSearch(query))
        {
        }

        private SearchUsersCollection _search;
        public new SearchUsersCollection Search
        {
            get
            {
                return _search;
            }
            set
            {
                Set(ref _search, value);
            }
        }
    }

    public class SearchUsersCollection : ObservableCollection<KeyedList<string, object>>, ISupportIncrementalLoading
    {
        private readonly IProtoService _protoService;
        private readonly string _query;

        private readonly List<int> _users = new List<int>();

        private KeyedList<string, object> _local;
        private KeyedList<string, object> _remote;

        public SearchUsersCollection(IProtoService protoService, string query)
        {
            _protoService = protoService;
            _query = query;

            _local = new KeyedList<string, object>(null as string);
            _remote = new KeyedList<string, object>(Strings.Android.GlobalSearch);

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
                    var response = await _protoService.SendAsync(new SearchContacts(_query, 100));
                    if (response is TdWindows.Users users)
                    {
                        foreach (var id in users.UserIds)
                        {
                            var user = _protoService.GetUser(id);
                            if (user != null)
                            {
                                _users.Add(id);
                                _local.Add(new SearchResult(user, _query, false));
                            }
                        }
                    }
                }
                else if (phase == 1)
                {
                    var response = await _protoService.SendAsync(new SearchChatsOnServer(_query, 100));
                    if (response is TdWindows.Chats chats && _local != null)
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

                                _local.Add(new SearchResult(chat, _query, false));
                            }
                        }
                    }
                }
                else if (phase == 2)
                {
                    var response = await _protoService.SendAsync(new SearchPublicChats(_query));
                    if (response is TdWindows.Chats chats)
                    {
                        foreach (var id in chats.ChatIds)
                        {
                            var chat = _protoService.GetChat(id);
                            if (chat != null && chat.Type is ChatTypePrivate)
                            {
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
