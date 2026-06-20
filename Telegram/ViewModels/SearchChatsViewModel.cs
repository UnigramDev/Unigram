//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Rg.DiffUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Collections.Handlers;
using Telegram.Common;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels.Profile;
using Telegram.Views;
using Telegram.Views.Popups;
using Telegram.Views.Profile;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace Telegram.ViewModels
{
    public partial class SearchChatsTabItem
    {
        public SearchChatsTabItem(string text, Type type, SearchCollection<MessageWithOwner, MediaCollection> items = null, bool isNew = false)
        {
            Text = text;
            Type = type;
            Items = items;
            IsNew = isNew;
        }

        public string Text { get; }

        public Type Type { get; }

        public SearchCollection<MessageWithOwner, MediaCollection> Items { get; }

        public bool IsNew { get; }
    }

    public partial class SearchChatsViewModel : MediaTabsViewModelBase, IIncrementalCollectionOwner
    {
        private readonly KeyedCollection<SearchResult> _recent = new(Strings.Recent, new SearchResultDiffHandler());
        private readonly KeyedCollection<SearchResult> _chatsAndContacts1 = new(Strings.ChatsAndContacts, new SearchResultDiffHandler());
        private readonly KeyedCollection<SearchResult> _chatsAndContacts2 = new(null as string, new SearchResultDiffHandler());
        private readonly KeyedCollection<SearchResult> _globalSearch = new(Strings.GlobalSearch, new SearchResultDiffHandler());
        private readonly KeyedCollection<Message> _messages = new(Strings.SearchMessages, null);

        private readonly SearchChannelsViewModel _channels;
        private readonly SearchWebAppsViewModel _webApps;
        private readonly SearchPostsViewModel _posts;

        private readonly ChooseChatsTracker _tracker;

        private CancellationTokenSource _cancellation = new();
        private CancellationToken _messagesToken;

        private string _prevQuery;
        private string _nextOffset;

        public SearchChatsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, clientService.Session.Resolve<IStorageService>(), aggregator)
        {
            _channels = new SearchChannelsViewModel(clientService, settingsService, aggregator);
            _webApps = new SearchWebAppsViewModel(clientService, settingsService, aggregator);
            _posts = new SearchPostsViewModel(clientService, settingsService, aggregator);

            _tracker = new ChooseChatsTracker(clientService, true);
            _tracker.Options = ChooseChatsOptions.All;

            _query = new(Constants.TypingTimeout, UpdateQuery, CanUpdateQuery);
            _query.Value = string.Empty;

            TopChats = new DiffObservableCollection<Chat>(new ChatDiffHandler(), Constants.DiffOptions);
            Items = new FlatteningCollection(this, _recent, _chatsAndContacts1, _chatsAndContacts2, _globalSearch, _messages);

            Tabs = new List<SearchChatsTabItem>
            {
                new(Strings.FilterChats, typeof(BlankPage)),
                new(Strings.FilterChannels, typeof(BlankPage)),
                new(Strings.AppsTab, typeof(BlankPage)),
                new(Strings.SearchPosts, typeof(SearchPostsTabPage), isNew: true),
                new(Strings.SharedMediaTab2, typeof(ProfileMediaTabPage), Media.Items),
                new(Strings.SharedFilesTab2, typeof(ProfileFilesTabPage), Files.Items),
                new(Strings.SharedLinksTab2, typeof(ProfileLinksTabPage), Links.Items),
                new(Strings.SharedMusicTab2, typeof(ProfileMusicTabPage), Music.Items),
                new(Strings.SharedVoiceTab2, typeof(ProfileVoiceTabPage), Voice.Items)
            };
        }

        public SearchPostsViewModel Posts => _posts;

        public List<SearchChatsTabItem> Tabs { get; }

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

        public FlatteningCollection ItemsView => SelectedTab switch
        {
            0 => Items,
            1 => _channels.Items,
            2 => _webApps.Items,
            _ => null,
        };

        public override INavigationService NavigationService
        {
            get => base.NavigationService;
            set
            {
                base.NavigationService = value;

                _channels.NavigationService = value;
                _webApps.NavigationService = value;
                _posts.NavigationService = value;
            }
        }

        private readonly DebouncedPropertyWithToken<string> _query;
        public string Query
        {
            get => _query;
            set
            {
                var query = value.TrimEnd();
                if (string.Equals(query, _prevQuery) && !string.IsNullOrEmpty(query))
                {
                    return;
                }

                _cancellation.Cancel();
                _cancellation = new();

                IsEmpty = false;

                if (SelectedTab >= Tabs.Count)
                {
                    return;
                }

                var tab = Tabs[SelectedTab];
                if (tab.Items != null)
                {
                    tab.Items.UpdateQuery(query);
                    _channels.SynchronizeQuery(query);
                    _webApps.SynchronizeQuery(query);
                    _posts.SynchronizeQuery(query);
                    _query.Value = query;
                }
                else if (SelectedTab == 1)
                {
                    _channels.Query = query;
                    _webApps.SynchronizeQuery(query);
                    _posts.SynchronizeQuery(query);
                    _query.Value = query;
                }
                else if (SelectedTab == 2)
                {
                    _webApps.Query = query;
                    _channels.SynchronizeQuery(query);
                    _posts.SynchronizeQuery(query);
                    _query.Value = query;
                }
                else if (SelectedTab == 3)
                {
                    _posts.Query = query;
                    _channels.SynchronizeQuery(query);
                    _webApps.SynchronizeQuery(query);
                    _query.Value = query;
                }
                else
                {
                    _query.Set(query, _cancellation.Token);
                    _channels.SynchronizeQuery(query);
                    _webApps.SynchronizeQuery(query);
                    _posts.SynchronizeQuery(query);
                }
            }
        }

        private bool _isTopChatsVisible;
        public bool IsTopChatsVisible
        {
            get => _isTopChatsVisible && Options.AllowUserChats && SelectedTab == 0;
            set => Set(ref _isTopChatsVisible, value);
        }

        private int _selectedTab;
        public int SelectedTab
        {
            get => _selectedTab;
            set
            {
                if (Set(ref _selectedTab, value))
                {
                    if (value >= Tabs.Count)
                    {
                        return;
                    }

                    _cancellation.Cancel();
                    _cancellation = new CancellationTokenSource();

                    var tab = Tabs[value];
                    if (tab.Items != null)
                    {
                        tab.Items.UpdateQuery(Query);
                        return;
                    }

                    RaisePropertyChanged(nameof(ItemsView));
                    RaisePropertyChanged(nameof(IsTopChatsVisible));

                    if (value == 1)
                    {
                        _channels.Query = Query;
                    }
                    else if (value == 2)
                    {
                        _webApps.Query = Query;
                    }
                    else if (value == 3)
                    {
                        _posts.Query = Query;
                    }
                    else
                    {
                        _query.Set(_query.Value, _cancellation.Token);
                    }
                }
            }
        }

        public void Activate()
        {
            IsDeactivated = false;
        }

        public void Deactivate()
        {
            IsDeactivated = true;
            SelectedTab = 0;

            Media.UpdateQuery(string.Empty);
            Files.UpdateQuery(string.Empty);
            Links.UpdateQuery(string.Empty);
            Music.UpdateQuery(string.Empty);
            Voice.UpdateQuery(string.Empty);
            Animations.UpdateQuery(string.Empty);
        }

        private bool _isEmpty;
        public bool IsEmpty
        {
            get => _isEmpty;
            set => Set(ref _isEmpty, value);
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
            _nextOffset = null;
            _messagesToken = default;

            _messages.ClearIfNotEmpty();
            _messages.Key = Strings.SearchMessages;

            _tracker.Clear();

            var query = value ?? string.Empty;

            if (string.IsNullOrEmpty(value))
            {
                _chatsAndContacts1.ClearIfNotEmpty();
                _chatsAndContacts2.ClearIfNotEmpty();
                _globalSearch.ClearIfNotEmpty();

                IsTopChatsVisible = true;

                await LoadTopChatsAsync(token);
                await LoadRecentAsync(query, token);
            }
            else
            {
                IsTopChatsVisible = false;

                await LoadRecentAsync(query, token);
                await LoadChatsAndContactsPart1Async(query, token);
            }
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

            IsEmpty = Items.Empty();
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

            ReplaceDiff(TopChats, temp);
        }

        private async Task LoadRecentAsync(string query, CancellationToken cancellationToken)
        {
            var temp = new List<SearchResult>();

            var response = await ClientService.SendAsync(new SearchRecentlyFoundChats(query, null, 50));
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

            ReplaceDiff(_recent, temp);
        }

        private Chat LoadSavedMessages(string query, CancellationToken cancellationToken)
        {
            if (ClientEx.SearchByPrefix(Strings.SavedMessages, query))
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
                _chatsAndContacts1.ClearIfNotEmpty();
                return;
            }

            var task2 = ClientService.SendAsync(new SearchChats(query, null, 100));
            var task3 = ClientService.SendAsync(new SearchContacts(query, 100));

            var temp = new List<SearchResult>();

            var response1 = LoadSavedMessages(query, cancellationToken);
            if (response1 is Chat savedMessages && !cancellationToken.IsCancellationRequested)
            {
                if (_tracker.Filter(savedMessages))
                {
                    temp.Add(new SearchResult(ClientService, savedMessages, query, SearchResultType.Chats, CanSendMessageToUser));
                }
            }

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
            var response = await ClientService.SendAsync(new SearchChatsOnServer(query, null, 100));
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
            var response = await ClientService.SendAsync(new SearchPublicChats(query, null));
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
            _messagesToken = cancellationToken;

            var response = await ClientService.SendAsync(new SearchMessages(null, query, _nextOffset ?? string.Empty, 50, null, null, 0, 0));
            if (response is FoundMessages messages && !cancellationToken.IsCancellationRequested)
            {
                if (string.IsNullOrEmpty(_nextOffset))
                {
                    _messages.Key = Locale.Declension(Strings.R.messages, messages.TotalCount);
                }

                _nextOffset = string.IsNullOrEmpty(messages.NextOffset) ? null : messages.NextOffset;

                foreach (var message in messages.Messages)
                {
                    _messages.Add(message);
                }
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
            await LoadMessagesAsync(_query.Value, _messagesToken);
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

    public partial class KeyedCollection<T> : DiffObservableCollection<T>, IKeyedCollection
    {
        public string Key { get; set; }

        public int Index { get; set; }

        public int TotalIndex => Index + (Key != null && Count > 0 ? 1 : 0);

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
