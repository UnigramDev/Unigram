//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

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
        // Neither local group pages, and a ListView is told about every row one notification at a
        // time, so anything past what gets scrolled to is rows nobody sees. Messages page, so theirs
        // is only the first screenful.
        private const int RecentLimit = 50;
        private const int SearchLimit = 30;
        private const int MessagesLimit = 20;

        private readonly KeyedCollection<SearchResult> _recent = new(Strings.Recent, new SearchResultDiffHandler());
        private readonly KeyedCollection<SearchResult> _chatsAndContacts1 = new(Strings.ChatsAndContacts, new SearchResultDiffHandler());
        private readonly KeyedCollection<SearchResult> _chatsAndContacts2 = new(null as string, new SearchResultDiffHandler());
        private readonly KeyedCollection<SearchResult> _globalSearch = new(Strings.GlobalSearch, new SearchResultDiffHandler());
        private readonly KeyedCollection<Message> _messages = new(Strings.SearchMessages, new MessageDiffHandler());

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

            // The local half gets its own, much shorter interval: it has to read as instant, but on the
            // keystroke itself it cost three local requests and a re-render per character typed.
            _offline = new(Constants.LocalTypingTimeout, UpdateQueryOffline);
            _query = new(Constants.TypingTimeout, UpdateQuery, CanUpdateQuery);
            _query.Value = string.Empty;

            TopChats = new DiffObservableCollection<Chat>(new ChatDiffHandler());
            Items = new FlatteningCollection(this, _recent, _chatsAndContacts1, _chatsAndContacts2, _globalSearch, _messages);

            // The cascade fetches the first page of messages; there is nothing to continue
            // from until it has.
            Items.HasMoreItems = false;

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

        private readonly DebouncedPropertyWithToken<string> _offline;

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
                // SelectedIndex is -1 whenever the selector holds no selection, which it
                // reports while its ItemsSource is being attached — not because a tab was
                // deselected. Set stores the value before returning, so accepting it here
                // would leave the field out of range for every later reader, and the
                // Tabs[value] below would already be reading at -1.
                if (value < 0 || value >= Tabs.Count)
                {
                    // Push the retained index back so the selector re-syncs to it.
                    RaisePropertyChanged();
                    return;
                }

                if (Set(ref _selectedTab, value))
                {
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

            _offline.Set(_prevQuery = value, token);
            return value.Length > 0;
        }

        private async void UpdateQueryOffline(string value, CancellationToken token)
        {
            _nextOffset = null;
            Items.HasMoreItems = false;
            _messagesToken = default;

            var query = value ?? string.Empty;

            // Left alone for a query that will run: LoadMessagesAsync replaces the group once the
            // response lands, instead of emptying it now and refilling it a debounce later. Nothing
            // refreshes it without a query, or with messages hidden, so those two still clear.
            if (query.Length == 0 || !Options.ShowMessages)
            {
                _messages.ClearIfNotEmpty();
            }

            _messages.Key = Strings.SearchMessages;

            _tracker.Clear();

            if (string.IsNullOrEmpty(value))
            {
                _chatsAndContacts1.ClearIfNotEmpty();
                _chatsAndContacts2.ClearIfNotEmpty();
                _globalSearch.ClearIfNotEmpty();

                IsTopChatsVisible = true;

                // Both sent before the first await: awaiting one before sending the other cost a round
                // trip on every keystroke.
                var topChats = ClientService.SendAsync(new GetTopChats(new TopChatCategoryUsers(), 30));
                var recent = ClientService.SendAsync(new SearchRecentlyFoundChats(query, null, RecentLimit));

                await LoadTopChatsAsync(topChats, token);
                await LoadRecentAsync(recent, query, token);
            }
            else
            {
                IsTopChatsVisible = false;

                // All three at once, for the same reason. The order they are *applied* in still
                // matters: _tracker dedupes against what the earlier groups took.
                var recent = ClientService.SendAsync(new SearchRecentlyFoundChats(query, null, RecentLimit));
                var chats = ClientService.SendAsync(new SearchChats(query, null, SearchLimit));
                var contacts = ClientService.SendAsync(new SearchContacts(query, SearchLimit));

                await LoadRecentAsync(recent, query, token);
                await LoadChatsAndContactsPart1Async(chats, contacts, query, token);
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

        private async Task LoadTopChatsAsync(Task<Td.Api.Object> request, CancellationToken cancellationToken)
        {
            var temp = new List<Chat>();

            var response = await request;
            if (response is Td.Api.Chats chats && !cancellationToken.IsCancellationRequested)
            {
                foreach (var chat in ClientService.GetChats(chats.ChatIds))
                {
                    temp.Add(chat);
                }
            }

            TopChats.Replace(temp);
        }

        private async Task LoadRecentAsync(Task<Td.Api.Object> request, string query, CancellationToken cancellationToken)
        {
            var temp = new List<SearchResult>();

            var response = await request;
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

            _recent.Replace(temp);
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

        private async Task LoadChatsAndContactsPart1Async(Task<Td.Api.Object> chatsRequest, Task<Td.Api.Object> contactsRequest, string query, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(query))
            {
                _chatsAndContacts1.ClearIfNotEmpty();
                return;
            }

            var temp = new List<SearchResult>();

            var response1 = LoadSavedMessages(query, cancellationToken);
            if (response1 is Chat savedMessages && !cancellationToken.IsCancellationRequested)
            {
                if (_tracker.Filter(savedMessages))
                {
                    temp.Add(_chatsAndContacts1.Reuse(temp.Count, savedMessages, null, query, SearchResultType.Chats)
                        ?? new SearchResult(ClientService, savedMessages, query, SearchResultType.Chats, CanSendMessageToUser));
                }
            }

            var response2 = await chatsRequest;
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

            var response3 = await contactsRequest;
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
            var response = await ClientService.SendAsync(new SearchChatsOnServer(query, null, SearchLimit));
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

                _globalSearch.Replace(temp);
            }
        }

        private async Task<uint> LoadMessagesAsync(string query, CancellationToken cancellationToken)
        {
            _messagesToken = cancellationToken;

            var firstPage = string.IsNullOrEmpty(_nextOffset);

            var response = await ClientService.SendAsync(new SearchMessages(null, query, _nextOffset ?? string.Empty, MessagesLimit, null, null, 0, 0));
            if (response is FoundMessages messages && !cancellationToken.IsCancellationRequested)
            {
                _nextOffset = string.IsNullOrEmpty(messages.NextOffset) ? null : messages.NextOffset;
                Items.HasMoreItems = _nextOffset != null && Options.ShowMessages;

                // The first page replaces what the previous query left; later pages append.
                if (firstPage)
                {
                    var key = Locale.Declension(Strings.R.messages, messages.TotalCount);
                    var changed = !string.Equals(_messages.Key, key);

                    _messages.Key = key;

                    _messages.Replace(messages.Messages);

                    if (changed)
                    {
                        Items.InvalidateKey(_messages);
                    }
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

            var totalCount = await LoadMessagesAsync(_query.Value, _messagesToken);
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
            : base(handler)
        {
            Key = key;
        }

        public KeyedCollection(string key, IEnumerable<T> source, IDiffHandler<T> handler)
            : base(source, handler)
        {
            Key = key;
        }

        public KeyedCollection(IGrouping<string, T> source, IDiffHandler<T> handler)
            : base(source, handler)
        {
            Key = source.Key;
        }

        public override string ToString()
        {
            return Key ?? base.ToString();
        }
    }
}
