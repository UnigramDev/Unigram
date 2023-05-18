using Rg.DiffUtils;
using System;
using System.Collections.Generic;
using System.Linq;
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
    public class SearchChatsViewModel : ViewModelBase, IIncrementalCollectionOwner
    {
        private readonly KeyedCollection<SearchResult> _recent = new(Strings.Recent, new SearchResultDiffHandler());
        private readonly KeyedCollection<SearchResult> _chatsAndContacts = new(Strings.ChatsAndContacts, new SearchResultDiffHandler());
        private readonly KeyedCollection<SearchResult> _globalSearch = new(Strings.GlobalSearch, new SearchResultDiffHandler());
        private readonly KeyedCollection<Message> _messages = new(Strings.SearchMessages, null);

        private readonly ChooseChatsTracker _tracker;

        private CancellationTokenSource _cancellation = new();

        private string _nextOffset;

        public SearchChatsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            _tracker = new ChooseChatsTracker(clientService, true);
            _tracker.Options = ChooseChatsOptions.All;

            _query = new(Constants.TypingTimeout, UpdateQuery, CanUpdateQuery);
            _query.Value = string.Empty;

            TopChats = new DiffObservableCollection<Chat>(new ChatDiffHandler(), Constants.DiffOptions);
            Items = new FlatteningCollection(this, _recent, _chatsAndContacts, _globalSearch, _messages);
        }

        public ChooseChatsOptions Options
        {
            get => _tracker.Options;
            set => _tracker.Options = value;
        }

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
            UpdateQueryOffline(value);
            return value.Length > 3;
        }

        private async void UpdateQueryOffline(string value)
        {
            _globalSearch.Clear();
            _messages.Clear();

            _nextOffset = null;

            _tracker.Clear();

            var query = value ?? string.Empty;
            var token = _cancellation.Token;

            if (string.IsNullOrEmpty(query))
            {
                IsTopChatsVisible = true;
                await LoadTopChatsAsync(token);
            }
            else
            {
                IsTopChatsVisible = false;
            }

            await LoadRecentAsync(query, token);
            await LoadChatsAndContactsPart1Async(query, token);
        }

        private async Task LoadTopChatsAsync(CancellationToken cancellationToken)
        {
            var temp = new List<Chat>();

            var response = await ClientService.SendAsync(new GetTopChats(new TopChatCategoryUsers(), 30));
            if (response is Td.Api.Chats chats && !cancellationToken.IsCancellationRequested)
            {
                foreach (var chat in ClientService.GetChats(chats.ChatIds))
                {
                    temp.Add(chat);
                }
            }

            TopChats.ReplaceDiff(temp);
        }

        private async Task LoadRecentAsync(string query, CancellationToken cancellationToken)
        {
            var temp = new List<SearchResult>();

            // TODO: replace with new TDLib method
            var response = await ClientService.SendAsync(new SearchChats(string.Empty, 50));
            if (response is Td.Api.Chats chats && !cancellationToken.IsCancellationRequested)
            {
                foreach (var chat in ClientService.GetChats(chats.ChatIds))
                {
                    // TODO: replace with new TDLib method
                    if (!chat.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (_tracker.Filter(chat))
                    {
                        temp.Add(new SearchResult(chat, query, SearchResultType.Recent));
                    }
                }
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            _recent.ReplaceDiff(temp);
        }

        private async Task<Chat> LoadSavedMessagesAsync(string query, CancellationToken cancellationToken)
        {
            if (query.Length > 0 && Strings.SavedMessages.StartsWith(_query, StringComparison.OrdinalIgnoreCase))
            {
                var savedMessages = await ClientService.SendAsync(new CreatePrivateChat(ClientService.Options.MyId, false));
                if (savedMessages is Chat chat && !cancellationToken.IsCancellationRequested)
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
                _chatsAndContacts.Clear();
                return;
            }

            var task1 = LoadSavedMessagesAsync(query, cancellationToken);
            var task2 = ClientService.SendAsync(new SearchChats(query, 100));
            var task3 = ClientService.SendAsync(new SearchContacts(query, 100));

            await Task.WhenAny(task1, task2, task3);

            var temp = new List<SearchResult>();

            var response1 = await task1;
            if (response1 is Chat savedMessages && !cancellationToken.IsCancellationRequested)
            {
                if (_tracker.Filter(savedMessages))
                {
                    temp.Add(new SearchResult(savedMessages, query, SearchResultType.Chats));
                }
            }

            var response2 = await task2;
            if (response2 is Td.Api.Chats chats && !cancellationToken.IsCancellationRequested)
            {
                foreach (var chat in ClientService.GetChats(chats.ChatIds))
                {
                    if (_tracker.Filter(chat))
                    {
                        temp.Add(new SearchResult(chat, query, SearchResultType.Chats));
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
                        temp.Add(new SearchResult(user, query, SearchResultType.Contacts));
                    }
                }
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            _chatsAndContacts.ReplaceDiff(temp);
        }

        private async Task LoadChatsAndContactsPart2Async(string query, CancellationToken cancellationToken)
        {
            var response = await ClientService.SendAsync(new SearchChatsOnServer(query, 100));
            if (response is Td.Api.Chats chats && !cancellationToken.IsCancellationRequested)
            {
                foreach (var chat in ClientService.GetChats(chats.ChatIds))
                {
                    if (_tracker.Filter(chat))
                    {
                        _chatsAndContacts.Add(new SearchResult(chat, query, SearchResultType.ChatsOnServer));
                    }
                }
            }
        }

        private async Task LoadGlobalSearchAsync(string query, CancellationToken cancellationToken)
        {
            var response = await ClientService.SendAsync(new SearchPublicChats(query));
            if (response is Td.Api.Chats chats && !cancellationToken.IsCancellationRequested)
            {
                foreach (var chat in ClientService.GetChats(chats.ChatIds))
                {
                    if (_tracker.Filter(chat))
                    {
                        _globalSearch.Add(new SearchResult(chat, query, SearchResultType.PublicChats));
                    }
                }
            }
        }

        private async Task LoadMessagesAsync(string query, CancellationToken cancellationToken)
        {
            var response = await ClientService.SendAsync(new SearchMessages(null, query, _nextOffset ?? string.Empty, 50, null, 0, 0));
            if (response is FoundMessages messages && !cancellationToken.IsCancellationRequested)
            {
                _nextOffset = string.IsNullOrEmpty(messages.NextOffset) ? null : messages.NextOffset;

                foreach (var message in messages.Messages)
                {
                    _messages.Add(message);
                }
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
            var confirm = await ShowPopupAsync(Strings.ClearSearchAlert, Strings.ClearSearchAlertTitle, Strings.ClearButton, Strings.Cancel, dangerous: true);
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

            var confirm = await ShowPopupAsync(message, Strings.ClearSearchSingleAlertTitle, Strings.ClearSearchRemove, Strings.Cancel, dangerous: true);
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

            var confirm = await ShowPopupAsync(string.Format(Strings.ChatHintsDeleteAlert, ClientService.GetTitle(chat)), Strings.ChatHintsDeleteAlertTitle, Strings.Remove, Strings.Cancel, dangerous: true);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            TopChats.Remove(chat);
            ClientService.Send(new RemoveTopChat(new TopChatCategoryUsers(), chat.Id));
        }

        #endregion
    }

    public class KeyedCollection<T> : DiffObservableCollection<T>, IKeyedCollection
    {
        public string Key { get; }

        public int Index { get; set; }

        public int Displacement => Index + (Key != null && Count > 0 ? 1 : 0);

        public int TotalCount => Count + (Key != null && Count > 0 ? 1 : 0);

        public KeyedCollection(string key, IDiffHandler<T> handler)
            : base(handler, Constants.DiffOptions)
        {
            Key = key;
        }

        public KeyedCollection(string key, IEnumerable<T> source, IDiffHandler<T> handler)
            : base(source, handler, Constants.DiffOptions)
        {
            Key = key;
        }

        public KeyedCollection(IGrouping<string, T> source, IDiffHandler<T> handler)
            : base(source, handler, Constants.DiffOptions)
        {
            Key = source.Key;
        }

        public override string ToString()
        {
            return Key ?? base.ToString();
        }
    }
}
