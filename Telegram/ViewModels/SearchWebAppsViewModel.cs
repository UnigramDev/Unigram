//
// Copyright Fela Ameghino 2015-2024
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
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace Telegram.ViewModels
{
    public class SearchWebAppsViewModel : ViewModelBase, IIncrementalCollectionOwner
    {
        private readonly KeyedCollection<SearchResult> _recent = new(Strings.SearchAppsMine, new SearchResultDiffHandler());
        private readonly KeyedCollection<SearchResult> _similar = new(Strings.SearchAppsPopular, new SearchResultDiffHandler());
        private readonly KeyedCollection<SearchResult> _chatsAndContacts = new(Strings.FilterChannels, new SearchResultDiffHandler());
        private readonly KeyedCollection<SearchResult> _globalSearch = new(Strings.GlobalSearch, new SearchResultDiffHandler());
        private readonly KeyedCollection<Message> _messages = new(Strings.SearchMessages, null);

        private readonly ChooseChatsTracker _tracker;
        private readonly DisposableMutex _diffLock = new();

        private CancellationTokenSource _cancellation = new();

        private string _prevQuery;
        private string _nextOffset;

        private bool _activated;

        public SearchWebAppsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            _tracker = new ChooseChatsTracker(clientService, true);
            _tracker.Options = new ChooseChatsOptions
            {
                AllowChannelChats = false,
                AllowGroupChats = false,
                AllowBotChats = true,
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

            Items = new FlatteningCollection(this, _recent, _similar, _chatsAndContacts, _globalSearch, _messages);
        }

        public void Activate()
        {
            if (_activated)
            {
                return;
            }

            _activated = true;
            CanUpdateQuery(string.Empty);
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

        public DiffObservableCollection<Chat> TopChats { get; }

        public FlatteningCollection Items { get; }

        private readonly DebouncedProperty<string> _query;
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

        private bool _isTopChatsVisible;
        public bool IsTopChatsVisible
        {
            get => _isTopChatsVisible;
            set => Set(ref _isTopChatsVisible, value);
        }

        public async void UpdateQuery(string value)
        {
            var query = value ?? string.Empty;
            var token = _cancellation.Token;

            _query.Value = query;

            await LoadChatsAndContactsPart2Async(query, token);
            await LoadGlobalSearchAsync(query, token);
            await LoadMessagesAsync(query, token);
        }

        private bool CanUpdateQuery(string value)
        {
            if (string.Equals(value, _prevQuery))
            {
                return false;
            }

            var clearOnline = _prevQuery == null || string.IsNullOrWhiteSpace(value) || (!value.StartsWith(_prevQuery) && !_prevQuery.StartsWith(value));

            UpdateQueryOffline(_prevQuery = value, clearOnline);
            return value.Length > 0;
        }

        private async void UpdateQueryOffline(string value, bool clearOnline)
        {
            //if (clearOnline)
            {
                _messages.Clear();
                _globalSearch.Clear();
            }

            _nextOffset = null;

            _tracker.Clear();

            var query = value ?? string.Empty;
            var token = _cancellation.Token;

            await LoadRecentAsync(query, token);
            await LoadSimilarAsync(query, token);
            await LoadChatsAndContactsPart1Async(query, token);
        }

        private async Task LoadRecentAsync(string query, CancellationToken cancellationToken)
        {
            var temp = new List<SearchResult>();

            if (string.IsNullOrEmpty(query))
            {
                var response = await ClientService.SendAsync(new GetTopChats(new TopChatCategoryWebAppBots(), 30));
                if (response is Td.Api.Chats chats && !cancellationToken.IsCancellationRequested)
                {
                    foreach (var chat in ClientService.GetChats(chats.ChatIds))
                    {
                        if (_tracker.Filter(chat))
                        {
                            temp.Add(new SearchResult(ClientService, chat, query, SearchResultType.RecentWebApps, false));
                        }
                    }
                }
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            ReplaceDiff(_recent, temp);
        }

        private async Task LoadSimilarAsync(string query, CancellationToken cancellationToken)
        {
            var temp = new List<SearchResult>();

            if (string.IsNullOrEmpty(query))
            {
                var response = await ClientService.SendAsync(new GetPopularWebAppBots(string.Empty, 50));
                if (response is FoundUsers foundUsers && !cancellationToken.IsCancellationRequested)
                {
                    foreach (var user in ClientService.GetUsers(foundUsers.UserIds))
                    {
                        if (_tracker.Filter(user))
                        {
                            temp.Add(new SearchResult(ClientService, user, query, SearchResultType.WebApps, CanSendMessageToUser));
                        }
                    }
                }
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            ReplaceDiff(_similar, temp);
        }

        private Chat LoadSavedMessages(string query, CancellationToken cancellationToken)
        {
            if (ClientEx.SearchByPrefix(Strings.SavedMessages, _query))
            {
                if (ClientService.TryGetChat(ClientService.Options.MyId, out Chat chat) && !cancellationToken.IsCancellationRequested)
                {
                    return chat;
                }
            }

            return null;
        }

        private async Task LoadChatsAndContactsPart1Async(string query, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(query))
            {
                using (await _diffLock.WaitAsync())
                {
                    _chatsAndContacts.Clear();
                }

                return;
            }

            var task2 = ClientService.SendAsync(new SearchChats(query, 100));
            var task3 = ClientService.SendAsync(new SearchContacts(query, 100));

            await Task.WhenAny(task2, task3);

            var temp = new List<SearchResult>();

            var response1 = LoadSavedMessages(query, cancellationToken);
            if (response1 is Chat savedMessages && !cancellationToken.IsCancellationRequested)
            {
                if (_tracker.Filter(savedMessages))
                {
                    temp.Add(new SearchResult(ClientService, savedMessages, query, SearchResultType.WebApps, CanSendMessageToUser));
                }
            }

            var response2 = await task2;
            if (response2 is Td.Api.Chats chats && !cancellationToken.IsCancellationRequested)
            {
                foreach (var chat in ClientService.GetChats(chats.ChatIds))
                {
                    if (_tracker.Filter(chat))
                    {
                        temp.Add(new SearchResult(ClientService, chat, query, SearchResultType.WebApps, CanSendMessageToUser));
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

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            ReplaceDiff(_chatsAndContacts, temp);
        }

        private async Task LoadChatsAndContactsPart2Async(string query, CancellationToken cancellationToken)
        {
            var response = await ClientService.SendAsync(new SearchChatsOnServer(query, 100));
            if (response is Td.Api.Chats chats && !cancellationToken.IsCancellationRequested)
            {
                using (await _diffLock.WaitAsync())
                {
                    foreach (var chat in ClientService.GetChats(chats.ChatIds))
                    {
                        if (_tracker.Filter(chat))
                        {
                            _chatsAndContacts.Add(new SearchResult(ClientService, chat, query, SearchResultType.WebApps, CanSendMessageToUser));
                        }
                    }
                }
            }
        }

        private async Task LoadGlobalSearchAsync(string query, CancellationToken cancellationToken)
        {
            var response = await ClientService.SendAsync(new SearchPublicChats(query));
            if (response is Td.Api.Chats chats && !cancellationToken.IsCancellationRequested)
            {
                //var temp = new List<SearchResult>();

                foreach (var chat in ClientService.GetChats(chats.ChatIds))
                {
                    if (_tracker.Filter(chat))
                    {
                        //temp.Add(new SearchResult(CanSendMessageToUser ? ClientService : null, chat, query, SearchResultType.PublicChats));
                        _globalSearch.Add(new SearchResult(ClientService, chat, query, SearchResultType.WebApps, CanSendMessageToUser));
                    }
                }

                //ReplaceDiff(_globalSearch, temp);
            }
        }

        private async Task LoadMessagesAsync(string query, CancellationToken cancellationToken)
        {
            var response = await ClientService.SendAsync(new SearchMessages(null, true, query, _nextOffset ?? string.Empty, 50, null, 0, 0));
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

        private async void ReplaceDiff<T>(DiffObservableCollection<T> destination, IEnumerable<T> source)
        {
            using (await _diffLock.WaitAsync())
            {
                var diff = await Task.Run(() => DiffUtil.CalculateDiff(destination, source, destination.DefaultDiffHandler, destination.DefaultOptions));
                destination.ReplaceDiff(diff);
            }
        }

        #region ISupportIncrementalLoading

        public async Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            await LoadMessagesAsync(_query.Value, _cancellation.Token);
            return new LoadMoreItemsResult { Count = 50 };
        }

        public bool HasMoreItems => _nextOffset != null;

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

        public async void RemoveTopChat(Chat chat)
        {
            if (chat == null)
            {
                return;
            }

            var confirm = await ShowPopupAsync(string.Format(Strings.ChatHintsDeleteAlert, ClientService.GetTitle(chat)), Strings.ChatHintsDeleteAlertTitle, Strings.Remove, Strings.Cancel, destructive: true);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            TopChats.Remove(chat);
            ClientService.Send(new RemoveTopChat(new TopChatCategoryUsers(), chat.Id));
        }

        #endregion
    }
}
