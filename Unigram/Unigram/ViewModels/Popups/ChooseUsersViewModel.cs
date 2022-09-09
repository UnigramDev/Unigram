using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Services;
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace Unigram.ViewModels.Popups
{
    public class ChooseUsersViewModel : TLViewModelBase
    {
        public ChooseUsersViewModel(IClientService clientService, ICacheService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, clientService, settingsService, aggregator)
        {
        }

        public SearchCollection<User, UserCollection> Items { get; private set; }
    }

    public class UserCollection : ObservableCollection<User>, ISupportIncrementalLoading
    {
        private readonly IClientService _clientService;
        private readonly string _query;

        private readonly HashSet<long> _users = new();

        private int _phase;

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return AsyncInfo.Run(async token =>
            {
                if (_phase == 0)
                {
                    var response = await _clientService.SendAsync(new SearchContacts(_query, 100));
                    if (response is Telegram.Td.Api.Users users)
                    {
                        foreach (var id in users.UserIds)
                        {
                            var user = _clientService.GetUser(id);
                            if (user != null)
                            {
                                _users.Add(id);
                                Add(user);
                            }
                        }
                    }
                }
                else if (_phase == 1)
                {
                    var response = await _clientService.SendAsync(new SearchChatsOnServer(_query, 100));
                    if (response is Telegram.Td.Api.Chats chats)
                    {
                        foreach (var id in chats.ChatIds)
                        {
                            var chat = _clientService.GetChat(id);
                            if (chat != null && _clientService.TryGetUser(chat, out User user))
                            {
                                if (_users.Contains(user.Id))
                                {
                                    continue;
                                }

                                _users.Add(user.Id);
                                Add(user);
                            }
                        }
                    }
                }
                else if (_phase == 2)
                {
                    var response = await _clientService.SendAsync(new SearchPublicChats(_query));
                    if (response is Telegram.Td.Api.Chats chats)
                    {
                        foreach (var id in chats.ChatIds)
                        {
                            var chat = _clientService.GetChat(id);
                            if (chat != null && _clientService.TryGetUser(chat, out User user))
                            {
                                if (_users.Contains(user.Id))
                                {
                                    continue;
                                }

                                Add(user);
                            }
                        }
                    }
                }

                _phase++;
                return new LoadMoreItemsResult();
            });
        }

        public bool HasMoreItems => _phase != 2;
    }
}
