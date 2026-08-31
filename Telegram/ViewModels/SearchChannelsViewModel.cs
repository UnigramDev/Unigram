//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

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

namespace Telegram.ViewModels
{
    public partial class SearchChannelsViewModel : ViewModelBase, IIncrementalCollectionOwner
    {
        private readonly KeyedCollection<SearchResult> _recent = new(Strings.SearchMyChannels, new SearchResultDiffHandler());
        private readonly KeyedCollection<SearchResult> _similar = new(Strings.SearchRecommendedChannels, new SearchResultDiffHandler());
        private readonly KeyedCollection<SearchResult> _chatsAndContacts1 = new(Strings.FilterChannels, new SearchResultDiffHandler());
        private readonly KeyedCollection<SearchResult> _chatsAndContacts2 = new(null as string, new SearchResultDiffHandler());
        private readonly KeyedCollection<SearchResult> _globalSearch = new(Strings.GlobalSearch, new SearchResultDiffHandler());
        private readonly KeyedCollection<Message> _messages = new(Strings.SearchMessages, new MessageDiffHandler());

        private const int RecentLimit = 50;
        private const int SearchLimit = 30;
        private const int MessagesLimit = 20;

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

            _offline = new(Constants.LocalTypingTimeout, UpdateQueryOffline);
            _query = new(Constants.TypingTimeout, UpdateQuery, CanUpdateQuery);
            _query.Value = string.Empty;

            Items = new FlatteningCollection(this, _recent, _similar, _chatsAndContacts1, _chatsAndContacts2, _globalSearch, _messages);

            // The cascade fetches the first page of messages; there is nothing to continue
            // from until it has.
            Items.HasMoreItems = false;
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

        private readonly DebouncedPropertyWithToken<string> _offline;

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

            _offline.Set(_prevQuery = value, token);
            return value.Length > 0;
        }

        private async void UpdateQueryOffline(string value, CancellationToken token)
        {
            if (string.IsNullOrEmpty(value))
            {
                _chatsAndContacts2.ClearIfNotEmpty();
                _globalSearch.ClearIfNotEmpty();
            }

            _nextOffset = null;
            Items.HasMoreItems = false;

            // Left alone for a query that will run: LoadMessagesAsync replaces the group once the
            // response lands, instead of emptying it now and refilling it a debounce later.
            if (string.IsNullOrEmpty(value) || !Options.ShowMessages)
            {
                _messages.ClearIfNotEmpty();
            }

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
                var response = await ClientService.SendAsync(new SearchRecentlyFoundChats(query, new SearchChatTypeFilterChannel(), RecentLimit));
                if (response is Td.Api.Chats chats && !cancellationToken.IsCancellationRequested)
                {
                    foreach (var chat in ClientService.GetChats(chats.ChatIds))
                    {
                        if (_tracker.Filter(chat))
                        {
                            temp.Add(_recent.Reuse(temp.Count, chat, null, query, SearchResultType.Recent)
                                ?? new SearchResult(ClientService, chat, query, SearchResultType.Recent, CanSendMessageToUser));
                        }
                    }
                }
            }

            _recent.Replace(temp);
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
                            temp.Add(_similar.Reuse(temp.Count, chat, null, query, SearchResultType.Recent)
                                ?? new SearchResult(ClientService, chat, query, SearchResultType.Recent, CanSendMessageToUser));
                        }
                    }
                }
            }

            _similar.Replace(temp);
        }

        private async Task LoadChatsAndContactsPart1Async(string query, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(query))
            {
                _chatsAndContacts1.Clear();
                return;
            }

            var task2 = ClientService.SendAsync(new SearchChats(query, new SearchChatTypeFilterChannel(), SearchLimit));
            var task3 = ClientService.SendAsync(new SearchContacts(query, SearchLimit));

            await Task.WhenAny(task2, task3);

            var temp = new List<SearchResult>();

            var response2 = await task2;
            if (response2 is Td.Api.Chats chats && !cancellationToken.IsCancellationRequested)
            {
                foreach (var chat in ClientService.GetChats(chats.ChatIds))
                {
                    if (_tracker.Filter(chat))
                    {
                        temp.Add(_chatsAndContacts1.Reuse(temp.Count, chat, null, query, SearchResultType.Chats)
                            ?? new SearchResult(ClientService, chat, query, SearchResultType.Chats, CanSendMessageToUser));
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
                        temp.Add(_chatsAndContacts1.Reuse(temp.Count, null, user, query, SearchResultType.Contacts)
                            ?? new SearchResult(ClientService, user, query, SearchResultType.Contacts, CanSendMessageToUser));
                    }
                }
            }

            _chatsAndContacts1.Replace(temp);
        }

        private async Task LoadChatsAndContactsPart2Async(string query, CancellationToken cancellationToken)
        {
            var response = await ClientService.SendAsync(new SearchChatsOnServer(query, new SearchChatTypeFilterChannel(), SearchLimit));
            if (response is Td.Api.Chats chats && !cancellationToken.IsCancellationRequested)
            {
                var temp = new List<SearchResult>();

                foreach (var chat in ClientService.GetChats(chats.ChatIds))
                {
                    if (_tracker.Filter(chat))
                    {
                        temp.Add(_chatsAndContacts2.Reuse(temp.Count, chat, null, query, SearchResultType.ChatsOnServer)
                            ?? new SearchResult(ClientService, chat, query, SearchResultType.ChatsOnServer, CanSendMessageToUser));
                    }
                }

                _chatsAndContacts2.Replace(temp);
            }
        }

        private async Task LoadGlobalSearchAsync(string query, CancellationToken cancellationToken)
        {
            var response = await ClientService.SendAsync(new SearchPublicChats(query, new SearchChatTypeFilterChannel()));
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

                _globalSearch.Replace(temp);
            }
        }

        private async Task<uint> LoadMessagesAsync(string query, CancellationToken cancellationToken)
        {
            var firstPage = string.IsNullOrEmpty(_nextOffset);

            var response = await ClientService.SendAsync(new SearchMessages(null, query, _nextOffset ?? string.Empty, MessagesLimit, null, null, 0, 0));
            if (response is FoundMessages messages && !cancellationToken.IsCancellationRequested)
            {
                _nextOffset = string.IsNullOrEmpty(messages.NextOffset) ? null : messages.NextOffset;
                Items.HasMoreItems = _nextOffset != null && Options.ShowMessages;

                // The first page replaces what the previous query left; later pages append.
                if (firstPage)
                {
                    _messages.Replace(messages.Messages);
                }
                else
                {
                    _messages.AddRange(messages.Messages);
                }

                return (uint)messages.Messages.Count;
            }

            return 0;
        }

        #region ISupportIncrementalLoading

        public async Task<IncrementalLoadResult> LoadMoreItemsAsync(uint count)
        {
            Logger.Info();

            var totalCount = await LoadMessagesAsync(_query.Value, _cancellation.Token);
            return new IncrementalLoadResult(totalCount, _nextOffset != null && Options.ShowMessages);
        }

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
