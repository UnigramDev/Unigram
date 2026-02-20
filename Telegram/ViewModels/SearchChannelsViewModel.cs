//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Rg.DiffUtils;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Collections.Handlers;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace Telegram.ViewModels
{
    public partial class SearchChannelsViewModel : ViewModelBase, IIncrementalCollectionOwner
    {
        private readonly KeyedCollection<SearchResult> _recent = new(Strings.SearchMyChannels, new SearchResultDiffHandler());
        private readonly KeyedCollection<SearchResult> _similar = new(Strings.SearchRecommendedChannels, new SearchResultDiffHandler());
        private readonly KeyedCollection<SearchResult> _chatsAndContacts1 = new(Strings.FilterChannels, new SearchResultDiffHandler());
        private readonly KeyedCollection<SearchResult> _chatsAndContacts2 = new(null as string, new SearchResultDiffHandler());
        private readonly KeyedCollection<SearchResult> _globalSearch = new(Strings.GlobalSearch, new SearchResultDiffHandler());
        private readonly KeyedCollection<Message> _messages = new(Strings.SearchMessages, null);

        private readonly ChooseChatsTracker _tracker;

        private CancellationTokenSource _cancellation = new();

        private string _prevQuery;
        private string _nextOffset;

        private bool _activated;

        public SearchChannelsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            _tracker = new ChooseChatsTracker(clientService, true);
            _tracker.Options = new ChooseChatsOptions
            {
                AllowChannelChats = true,
                AllowGroupChats = false,
                AllowBotChats = false,
                AllowUserChats = false,
                AllowSecretChats = false,
                AllowSelf = false,
                CanPostMessages = false,
                CanInviteUsers = false,
                CanShareContact = false,
                Mode = ChooseChatsMode.Chats
            };

            _query = new(Constants.TypingTimeout, UpdateQuery, CanUpdateQuery);
            _query.Value = string.Empty;

            Items = new FlatteningCollection(this, _recent, _similar, _chatsAndContacts1, _chatsAndContacts2, _globalSearch, _messages);
        }

        public void Activate()
        {
            if (_activated)
            {
                return;
            }

            _activated = true;
            CanUpdateQuery(string.Empty, default);
        }

        public ChooseChatsOptions Options
        {
            get => _tracker.Options;
            set => _tracker.Options = value;
        }

        public bool CanSendMessageToUser =>
            Options == ChooseChatsOptions.PostMessages
            || Options == ChooseChatsOptions.Contacts
            || Options == ChooseChatsOptions.InviteUsers;

        public FlatteningCollection Items { get; }

        private readonly DebouncedPropertyWithToken<string> _query;
        public string Query
        {
            get => _query;
            set
            {
                _cancellation.Cancel();
                _cancellation = new();

                _query.Set(value, _cancellation.Token);
            }
        }

        public void SynchronizeQuery(string query)
        {
            _cancellation.Cancel();
            _cancellation = new();
        }

        public async void UpdateQuery(string value, CancellationToken token)
        {
            var query = value ?? string.Empty;

            _query.Value = query;

            await LoadChatsAndContactsPart2Async(query, token);
            await LoadGlobalSearchAsync(query, token);

            if (Options.ShowMessages)
            {
                await LoadMessagesAsync(query, token);
            }
        }

        private bool CanUpdateQuery(string value, CancellationToken token)
        {
            if (string.Equals(value, _prevQuery))
            {
                return false;
            }

            UpdateQueryOffline(_prevQuery = value, token);
            return value.Length > 0;
        }

        private async void UpdateQueryOffline(string value, CancellationToken token)
        {
            if (string.IsNullOrEmpty(value))
            {
                _chatsAndContacts2.Clear();
                _globalSearch.Clear();
            }

            _nextOffset = null;

            _messages.Clear();
            _tracker.Clear();

            var query = value ?? string.Empty;

            await LoadRecentAsync(query, token);
            await LoadSimilarAsync(query, token);
            await LoadChatsAndContactsPart1Async(query, token);
        }

        private async Task LoadRecentAsync(string query, CancellationToken cancellationToken)
        {
            var temp = new List<SearchResult>();

            if (string.IsNullOrEmpty(query))
            {
                var response = await ClientService.SendAsync(new SearchRecentlyFoundChats(query, 50));
                if (response is Td.Api.Chats chats && !cancellationToken.IsCancellationRequested)
                {
                    foreach (var chat in ClientService.GetChats(chats.ChatIds))
                    {
                        if (_tracker.Filter(chat))
                        {
                            temp.Add(new SearchResult(ClientService, chat, query, SearchResultType.Recent, CanSendMessageToUser));
                        }
                    }
                }
            }

            ReplaceDiff(_recent, temp);
        }

        private async Task LoadSimilarAsync(string query, CancellationToken cancellationToken)
        {
            var temp = new List<SearchResult>();

            if (string.IsNullOrEmpty(query))
            {
                var response = await ClientService.SendAsync(new GetRecommendedChats());
                if (response is Td.Api.Chats chats && !cancellationToken.IsCancellationRequested)
                {
                    foreach (var chat in ClientService.GetChats(chats.ChatIds))
                    {
                        if (_tracker.Filter(chat))
                        {
                            temp.Add(new SearchResult(ClientService, chat, query, SearchResultType.Recent, CanSendMessageToUser));
                        }
                    }
                }
            }

            ReplaceDiff(_similar, temp);
        }

        private async Task LoadChatsAndContactsPart1Async(string query, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(query))
            {
                _chatsAndContacts1.Clear();
                return;
            }

            var task2 = ClientService.SendAsync(new SearchChats(query, 100));
            var task3 = ClientService.SendAsync(new SearchContacts(query, 100));

            await Task.WhenAny(task2, task3);

            var temp = new List<SearchResult>();

            var response2 = await task2;
            if (response2 is Td.Api.Chats chats && !cancellationToken.IsCancellationRequested)
            {
                foreach (var chat in ClientService.GetChats(chats.ChatIds))
                {
                    if (_tracker.Filter(chat))
                    {
                        temp.Add(new SearchResult(ClientService, chat, query, SearchResultType.Chats, CanSendMessageToUser));
                    }
                }
            }

            var response3 = await task3;
            if (response3 is Td.Api.Users users && !cancellationToken.IsCancellationRequested)
            {
                foreach (var user in ClientService.GetUsers(users.UserIds))
                {
                    if (_tracker.Filter(user))
                    {
                        temp.Add(new SearchResult(ClientService, user, query, SearchResultType.Contacts, CanSendMessageToUser));
                    }
                }
            }

            ReplaceDiff(_chatsAndContacts1, temp);
        }

        private async Task LoadChatsAndContactsPart2Async(string query, CancellationToken cancellationToken)
        {
            var response = await ClientService.SendAsync(new SearchChatsOnServer(query, 100));
            if (response is Td.Api.Chats chats && !cancellationToken.IsCancellationRequested)
            {
                var temp = new List<SearchResult>();

                foreach (var chat in ClientService.GetChats(chats.ChatIds))
                {
                    if (_tracker.Filter(chat))
                    {
                        temp.Add(new SearchResult(ClientService, chat, query, SearchResultType.ChatsOnServer, CanSendMessageToUser));
                    }
                }

                ReplaceDiff(_chatsAndContacts2, temp);
            }
        }

        private async Task LoadGlobalSearchAsync(string query, CancellationToken cancellationToken)
        {
            var response = await ClientService.SendAsync(new SearchPublicChats(query));
            if (response is Td.Api.Chats chats && !cancellationToken.IsCancellationRequested)
            {
                var temp = new List<SearchResult>();

                foreach (var chat in ClientService.GetChats(chats.ChatIds))
                {
                    if (_tracker.Filter(chat))
                    {
                        temp.Add(new SearchResult(ClientService, chat, query, SearchResultType.PublicChats, CanSendMessageToUser));
                    }
                }

                ReplaceDiff(_globalSearch, temp);
            }
        }

        private async Task LoadMessagesAsync(string query, CancellationToken cancellationToken)
        {
            var response = await ClientService.SendAsync(new SearchMessages(null, query, _nextOffset ?? string.Empty, 50, null, null, 0, 0));
            if (response is FoundMessages messages && !cancellationToken.IsCancellationRequested)
            {
                _nextOffset = string.IsNullOrEmpty(messages.NextOffset) ? null : messages.NextOffset;

                foreach (var message in messages.Messages)
                {
                    _messages.Add(message);
                }

                //ReplaceDiff(_messages, messages.Messages);
            }
        }

        private void ReplaceDiff<T>(DiffObservableCollection<T> destination, IList<T> source)
        {
            if (destination.Empty())
            {
                destination.AddRange(source);
                return;
            }
            else if (source.Empty())
            {
                destination.ClearIfNotEmpty();
                return;
            }

            var recycledItems = Math.Min(destination.Count, source.Count);
            var changedItems = Math.Max(destination.Count, source.Count);

            if (destination.Count > source.Count)
            {
                for (int i = recycledItems; i < changedItems; i++)
                {
                    destination.RemoveAt(recycledItems);
                }
            }
            else if (source.Count > destination.Count)
            {
                for (int i = recycledItems; i < changedItems; i++)
                {
                    destination.Insert(i, source[i]);
                }
            }

            for (int i = 0; i < recycledItems; i++)
            {
                var oldItem = destination[i];
                var newItem = source[i];

                if (destination.DefaultDiffHandler == null || !destination.DefaultDiffHandler.CompareItems(oldItem, newItem))
                {
                    destination[i] = newItem;
                }
            }
        }

        #region ISupportIncrementalLoading

        public async Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            await LoadMessagesAsync(_query.Value, _cancellation.Token);
            return new LoadMoreItemsResult { Count = 50 };
        }

        public bool HasMoreItems => _nextOffset != null && Options.ShowMessages;

        #endregion

        #region Commands

        public async void ClearRecentChats()
        {
            var confirm = await ShowPopupAsync(Strings.ClearSearchAlert, Strings.ClearSearchAlertTitle, Strings.ClearButton, Strings.Cancel, destructive: true);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            _recent.Clear();
            ClientService.Send(new ClearRecentlyFoundChats());
        }

        public async void RemoveRecentChat(SearchResult result)
        {
            string message;
            if (ClientService.TryGetUser(result.Chat, out User user))
            {
                message = string.Format(Strings.ClearSearchSingleUserAlertText, user.FullName());
            }
            else
            {
                message = string.Format(Strings.ClearSearchSingleChatAlertText, ClientService.GetTitle(result.Chat));
            }

            var confirm = await ShowPopupAsync(message, Strings.ClearSearchSingleAlertTitle, Strings.ClearSearchRemove, Strings.Cancel, destructive: true);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            _recent.Remove(result);
            ClientService.Send(new RemoveRecentlyFoundChat(result.Chat.Id));
        }

        #endregion
    }
}
