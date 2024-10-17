//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.Views.Folders;
using Telegram.Views.Popups;
using Windows.Foundation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace Telegram.ViewModels
{
    public partial class ChatListViewModel : ViewModelBase, IDelegable<IChatListDelegate>
    {
        private readonly INotificationsService _notificationsService;

        private readonly Dictionary<long, bool> _deletedChats = new Dictionary<long, bool>();

        public IChatListDelegate Delegate { get; set; }

        public ChatListViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, INotificationsService notificationsService, ChatList chatList)
            : base(clientService, settingsService, aggregator)
        {
            _notificationsService = notificationsService;

            Items = new ItemsCollection(clientService, aggregator, this, chatList);

#if MOCKUP
            Items.AddRange(clientService.GetChats(null));
#endif

            SelectedItems = new MvxObservableCollection<Chat>();
        }

        #region Selection

        public long LastSelectedItem { get; private set; }

        private long? _selectedItem;
        public long? SelectedItem
        {
            get => _selectedItem;
            set
            {
                Set(ref _selectedItem, value);

                if (value.HasValue)
                {
                    LastSelectedItem = value.Value;
                }
            }
        }

        public MvxObservableCollection<Chat> SelectedItems { get; }

        private ListViewSelectionMode _selectionMode = ListViewSelectionMode.None;
        public ListViewSelectionMode SelectionMode
        {
            get => _selectionMode;
            set => Set(ref _selectionMode, value);
        }

        #endregion

        public ItemsCollection Items { get; private set; }

        public bool IsLastSliceLoaded { get; set; }

        #region Open

        public void OpenChat(Chat chat)
        {
            NavigationService.NavigateToChat(chat, createNewWindow: true);
        }

        #endregion

        #region Pin

        public async void PinChat(Chat chat)
        {
            var position = chat.GetPosition(Items.ChatList);
            if (position == null)
            {
                return;
            }

            var response = await ClientService.SendAsync(new ToggleChatIsPinned(Items.ChatList, chat.Id, !position.IsPinned));
            if (response is Error error && error.Code == 400)
            {
                // This is not the right way
                NavigationService.ShowLimitReached(new PremiumLimitTypePinnedChatCount());
            }
        }

        #endregion

        #region Archive

        public async void ArchiveChat(Chat chat)
        {
            var archived = chat.Positions.Any(x => x.List is ChatListArchive);
            if (archived)
            {
                ClientService.Send(new AddChatToList(chat.Id, new ChatListMain()));
                return;
            }
            else
            {
                ClientService.Send(new AddChatToList(chat.Id, new ChatListArchive()));
            }

            var confirm = await ToastPopup.ShowActionAsync(XamlRoot, Strings.ChatArchived, Strings.Undo, ToastPopupIcon.Archived);
            if (confirm == ContentDialogResult.Primary)
            {
                ClientService.Send(new AddChatToList(chat.Id, new ChatListMain()));
            }
        }

        #endregion

        #region Multiple Archive

        public async void ArchiveSelectedChats()
        {
            var chats = SelectedItems.ToList();

            foreach (var chat in chats)
            {
                ClientService.Send(new AddChatToList(chat.Id, new ChatListArchive()));
            }

            Delegate?.SetSelectionMode(false);
            SelectedItems.Clear();

            var confirm = await ToastPopup.ShowActionAsync(XamlRoot, Strings.ChatsArchived, Strings.Undo, ToastPopupIcon.Archived);
            if (confirm == ContentDialogResult.Primary)
            {
                foreach (var undo in chats)
                {
                    ClientService.Send(new AddChatToList(undo.Id, new ChatListMain()));
                }
            }
        }

        #endregion

        #region Mark

        public void MarkChatAsRead(Chat chat)
        {
            if (chat.UnreadCount > 0)
            {
                if (chat.LastMessage != null)
                {
                    ClientService.Send(new ViewMessages(chat.Id, new[] { chat.LastMessage.Id }, new MessageSourceChatList(), true));
                }

                if (chat.UnreadMentionCount > 0)
                {
                    ClientService.Send(new ReadAllChatMentions(chat.Id));
                }

                if (chat.UnreadReactionCount > 0)
                {
                    ClientService.Send(new ReadAllChatReactions(chat.Id));
                }
            }
            else
            {
                ClientService.Send(new ToggleChatIsMarkedAsUnread(chat.Id, !chat.IsMarkedAsUnread));
            }
        }

        #endregion

        #region Multiple Mark

        public void MarkSelectedChatsAsRead()
        {
            var chats = SelectedItems.ToList();
            var unread = chats.Any(x => x.IsUnread());
            foreach (var chat in chats)
            {
                if (unread)
                {
                    if (chat.UnreadCount > 0 && chat.LastMessage != null)
                    {
                        ClientService.Send(new ViewMessages(chat.Id, new[] { chat.LastMessage.Id }, new MessageSourceChatList(), true));
                    }
                    else if (chat.IsMarkedAsUnread)
                    {
                        ClientService.Send(new ToggleChatIsMarkedAsUnread(chat.Id, false));
                    }

                    if (chat.UnreadMentionCount > 0)
                    {
                        ClientService.Send(new ReadAllChatMentions(chat.Id));
                    }

                    if (chat.UnreadReactionCount > 0)
                    {
                        ClientService.Send(new ReadAllChatReactions(chat.Id));
                    }
                }
                else if (chat.UnreadCount == 0 && !chat.IsMarkedAsUnread)
                {
                    ClientService.Send(new ToggleChatIsMarkedAsUnread(chat.Id, true));
                }
            }

            Delegate?.SetSelectionMode(false);
            SelectedItems.Clear();
        }

        #endregion

        #region Notify

        public void NotifyChat(Chat chat)
        {
            _notificationsService.SetMuteFor(chat, ClientService.Notifications.IsMuted(chat) ? 0 : 632053052, XamlRoot);
        }

        #endregion

        #region Mute for

        public async void MuteChatFor(Tuple<Chat, int?> value)
        {
            var chat = value.Item1;
            if (chat == null)
            {
                return;
            }

            if (value.Item2 is int update)
            {
                _notificationsService.SetMuteFor(chat, update, XamlRoot);
            }
            else
            {
                var muteFor = Settings.Notifications.GetMuteFor(chat);
                var popup = new ChatMutePopup(muteFor);

                var confirm = await ShowPopupAsync(popup);
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }

                if (muteFor != popup.Value)
                {
                    _notificationsService.SetMuteFor(chat, popup.Value, XamlRoot);
                }
            }
        }

        #endregion


        #region Multiple Notify

        public void NotifySelectedChats()
        {
            var chats = SelectedItems.ToList();
            var muted = chats.Any(x => ClientService.Notifications.IsMuted(x));

            foreach (var chat in chats)
            {
                if (chat.Type is ChatTypePrivate privata && privata.UserId == ClientService.Options.MyId)
                {
                    continue;
                }

                _notificationsService.SetMuteFor(chat, muted ? 0 : 632053052, XamlRoot);
            }

            Delegate?.SetSelectionMode(false);
            SelectedItems.Clear();
        }

        #endregion

        #region Delete

        public async void DeleteChat(Chat chat)
        {
            Logger.Info(chat.Type);

            var updated = await ClientService.SendAsync(new GetChat(chat.Id)) as Chat ?? chat;
            var popup = new DeleteChatPopup(ClientService, updated, Items.ChatList, false);

            var confirm = await ShowPopupAsync(popup);
            if (confirm == ContentDialogResult.Primary)
            {
                var check = popup.IsChecked == true;

                _deletedChats[chat.Id] = true;
                Items.Handle(chat.Id, 0);

                string title;
                if (chat.Type is ChatTypeSupergroup super)
                {
                    title = super.IsChannel ? Strings.ChannelDeletedUndo : Strings.GroupDeletedUndo;
                }
                else
                {
                    title = chat.Type is ChatTypeBasicGroup ? Strings.GroupDeletedUndo : Strings.ChatDeletedUndo;
                }

                var undo = await ToastPopup.ShowCountdownAsync(XamlRoot, title, Strings.Undo, TimeSpan.FromSeconds(5));
                if (undo == ContentDialogResult.Primary)
                {
                    _deletedChats.Remove(chat.Id);
                    Items.Handle(chat.Id, chat.Positions);
                }
                else
                {
                    if (chat.Type is ChatTypeBasicGroup or ChatTypeSupergroup)
                    {
                        await ClientService.SendAsync(new LeaveChat(chat.Id));
                        await ClientService.SendAsync(new DeleteChatHistory(chat.Id, true, false));
                    }
                    else if (chat.Type is ChatTypeSecret)
                    {
                        await ClientService.SendAsync(new DeleteChat(chat.Id));
                    }
                    else
                    {
                        var user = ClientService.GetUser(chat);
                        if (user?.Type is UserTypeRegular)
                        {
                            await ClientService.SendAsync(new DeleteChatHistory(chat.Id, true, check));
                        }
                        else
                        {
                            if (user?.Type is UserTypeBot && check)
                            {
                                await ClientService.SendAsync(new SetMessageSenderBlockList(new MessageSenderUser(user.Id), new BlockListMain()));
                            }

                            await ClientService.SendAsync(new DeleteChatHistory(chat.Id, true, false));
                        }
                    }
                };
            }
        }

        #endregion

        #region Multiple Delete

        public async void DeleteSelectedChats()
        {
            var chats = SelectedItems.ToList();

            var confirm = await ShowPopupAsync(Strings.AreYouSureDeleteFewChats, Locale.Declension(Strings.R.ChatsSelected, chats.Count), Strings.Delete, Strings.Cancel, destructive: true);
            if (confirm == ContentDialogResult.Primary)
            {
                foreach (var chat in chats)
                {
                    _deletedChats[chat.Id] = true;
                    Items.Handle(chat.Id, 0);
                }

                var undo = await ToastPopup.ShowCountdownAsync(XamlRoot, Strings.ChatDeletedUndo, Strings.Undo, TimeSpan.FromSeconds(5));
                if (undo == ContentDialogResult.Primary)
                {
                    foreach (var chat in chats)
                    {
                        _deletedChats.Remove(chat.Id);
                        Items.Handle(chat.Id, chat.Positions);
                    }
                }
                else
                {
                    foreach (var chat in chats)
                    {
                        if (chat.Type is ChatTypeBasicGroup or ChatTypeSupergroup)
                        {
                            await ClientService.SendAsync(new LeaveChat(chat.Id));
                            await ClientService.SendAsync(new DeleteChatHistory(chat.Id, true, false));
                        }
                        else if (chat.Type is ChatTypeSecret secret)
                        {
                            await ClientService.SendAsync(new DeleteChat(chat.Id));
                            await ClientService.SendAsync(new CloseSecretChat(secret.SecretChatId));
                        }
                        else
                        {
                            await ClientService.SendAsync(new DeleteChatHistory(chat.Id, true, false));
                        }
                    }
                };
            }

            Delegate?.SetSelectionMode(false);
            SelectedItems.Clear();
        }

        #endregion

        #region Clear

        public async void ClearChat(Chat chat)
        {
            Logger.Info(chat.Type);

            var updated = await ClientService.SendAsync(new GetChat(chat.Id)) as Chat ?? chat;
            var dialog = new DeleteChatPopup(ClientService, updated, Items.ChatList, true);

            var confirm = await ShowPopupAsync(dialog);
            if (confirm == ContentDialogResult.Primary)
            {
                var undo = await ToastPopup.ShowCountdownAsync(XamlRoot, Strings.HistoryClearedUndo, Strings.Undo, TimeSpan.FromSeconds(5));
                if (undo == ContentDialogResult.Primary)
                {
                    _deletedChats.Remove(chat.Id);
                    Items.Handle(chat.Id, chat.Positions);
                }
                else
                {
                    ClientService.Send(new DeleteChatHistory(chat.Id, false, dialog.IsChecked));
                }
            }
        }

        #endregion

        #region Multiple Clear

        public async void ClearSelectedChats()
        {
            var chats = SelectedItems.ToList();

            var confirm = await ShowPopupAsync(Strings.AreYouSureClearHistoryFewChats, Locale.Declension(Strings.R.ChatsSelected, chats.Count), Strings.ClearHistory, Strings.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                var undo = await ToastPopup.ShowCountdownAsync(XamlRoot, Strings.HistoryClearedUndo, Strings.Undo, TimeSpan.FromSeconds(5));
                if (undo == ContentDialogResult.Primary)
                {
                    foreach (var chat in chats)
                    {
                        _deletedChats.Remove(chat.Id);
                        Items.Handle(chat.Id, chat.Positions);
                    }
                }
                else
                {
                    foreach (var chat in chats)
                    {
                        ClientService.Send(new DeleteChatHistory(chat.Id, false, false));
                    }
                }
            }

            Delegate?.SetSelectionMode(false);
            SelectedItems.Clear();
        }

        #endregion

        #region Select

        public void SelectChat(Chat chat)
        {
            SelectedItems.ReplaceWith(new[] { chat });
            SelectionMode = ListViewSelectionMode.Multiple;

            Delegate?.SetSelectedItems(SelectedItems);
        }

        #endregion

        #region Folder add

        public async void AddToFolder((int ChatFolderId, Chat Chat) data)
        {
            var folder = await ClientService.SendAsync(new GetChatFolder(data.ChatFolderId)) as ChatFolder;
            if (folder == null)
            {
                return;
            }

            var total = folder.IncludedChatIds.Count + folder.PinnedChatIds.Count + 1;
            if (total > 99)
            {
                await ShowPopupAsync(Strings.FilterAddToAlertFullText, Strings.FilterAddToAlertFullTitle, Strings.OK);
                return;
            }

            if (folder.IncludedChatIds.Contains(data.Chat.Id))
            {
                // Warn user about chat being already in the folder?
                return;
            }

            folder.ExcludedChatIds.Remove(data.Chat.Id);
            folder.IncludedChatIds.Add(data.Chat.Id);

            ClientService.Send(new EditChatFolder(data.ChatFolderId, folder));
        }

        #endregion

        #region Folder remove

        public async void RemoveFromFolder((int ChatFolderId, Chat Chat) data)
        {
            var folder = await ClientService.SendAsync(new GetChatFolder(data.ChatFolderId)) as ChatFolder;
            if (folder == null)
            {
                return;
            }

            if (folder.IsShareable)
            {
                folder.IncludedChatIds.Remove(data.Chat.Id);
            }
            else
            {
                var total = folder.ExcludedChatIds.Count + 1;
                if (total > 99)
                {
                    await ShowPopupAsync(Strings.FilterRemoveFromAlertFullText, Strings.AppName, Strings.OK);
                    return;
                }

                if (folder.ExcludedChatIds.Contains(data.Chat.Id))
                {
                    // TODO: Warn user about chat being already in the folder?
                    return;
                }

                folder.IncludedChatIds.Remove(data.Chat.Id);
                folder.ExcludedChatIds.Add(data.Chat.Id);
            }

            if (folder.Empty())
            {
                // TODO: Warn user about chat being already in the folder?
                return;
            }

            ClientService.Send(new EditChatFolder(data.ChatFolderId, folder));
        }

        #endregion

        #region Folder create

        public void CreateFolder(Chat chat)
        {
            NavigationService.Navigate(typeof(FolderPage), new FolderPageCreateArgs(chat.Id));
        }

        #endregion

        public async void SetFolder(ChatList chatList)
        {
            await Items.ReloadAsync(chatList);
            //Aggregator.Unsubscribe(Items);
            //Items = new ItemsCollection(ClientService, Aggregator, this, chatList);
            //RaisePropertyChanged(nameof(Items));
        }

        public partial class ItemsCollection : ObservableCollection<Chat>, ISupportIncrementalLoading
        {
            private readonly IClientService _clientService;
            private readonly IEventAggregator _aggregator;

            private readonly DisposableMutex _loadMoreLock = new();
            private readonly HashSet<long> _chats = new();

            private readonly ChatListViewModel _viewModel;

            private ChatList _chatList;

            private bool _hasMoreItems = true;

            private long _lastChatId;
            private long _lastOrder;

            public ChatList ChatList => _chatList;

            public ItemsCollection(IClientService clientService, IEventAggregator aggregator, ChatListViewModel viewModel, ChatList chatList)
            {
                _clientService = clientService;
                _aggregator = aggregator;

                _viewModel = viewModel;
                _viewModel.IsLoading = true;

                _chatList = chatList;

#if MOCKUP
                _hasMoreItems = false;
#endif

                _ = LoadMoreItemsAsync(0);
            }

            public async Task ReloadAsync(ChatList chatList)
            {
                _viewModel.IsLoading = true;

                using (await _loadMoreLock.WaitAsync())
                {
                    _aggregator.Unsubscribe(this);

                    _lastChatId = 0;
                    _lastOrder = 0;

                    _chatList = chatList;

                    _chats.Clear();
                    Clear();
                }

                await LoadMoreItemsAsync();
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(token => LoadMoreItemsAsync());
            }

            private async Task<LoadMoreItemsResult> LoadMoreItemsAsync()
            {
                using var guard = await _loadMoreLock.WaitAsync();
                var totalCount = 0u;

                var response = await _clientService.GetChatListAsync(_chatList, Count, 20);
                if (response is Telegram.Td.Api.Chats chats)
                {
                    if (_viewModel.Delegate != null)
                    {
                        // Exp: does this affect layout cycles?
                        await _viewModel.Delegate.UpdateLayoutAsync();
                    }

                    foreach (var chat in _clientService.GetChats(chats.ChatIds))
                    {
                        var order = chat.GetOrder(_chatList);
                        if (order != 0)
                        {
                            // TODO: is this redundant?
                            var next = NextIndexOf(chat, order);
                            if (next >= 0)
                            {
                                if (_chats.Contains(chat.Id))
                                {
                                    Remove(chat);
                                }

                                _chats.Add(chat.Id);
                                Insert(Math.Min(Count, next), chat);

                                if (chat.Id == _viewModel._selectedItem)
                                {
                                    _viewModel.Delegate?.SetSelectedItem(chat);
                                }

                                totalCount++;
                            }

                            _lastChatId = chat.Id;
                            _lastOrder = order;
                        }
                    }

                    IsEmpty = Count == 0;

                    _hasMoreItems = chats.ChatIds.Count > 0;
                    Subscribe();

                    _viewModel.IsLoading = false;
                    _viewModel.Delegate?.SetSelectedItems(_viewModel.SelectedItems);
                }

                return new LoadMoreItemsResult
                {
                    Count = totalCount
                };
            }

            private void Subscribe()
            {
                _aggregator.Subscribe<UpdateAuthorizationState>(this, Handle)
                    .Subscribe<UpdateChatDraftMessage>(Handle)
                    .Subscribe<UpdateChatLastMessage>(Handle)
                    .Subscribe<UpdateChatPosition>(Handle);
            }

            public bool HasMoreItems => _hasMoreItems;

            #region Handle

            public void Handle(UpdateAuthorizationState update)
            {
                if (update.AuthorizationState is AuthorizationStateReady)
                {
                    _viewModel.BeginOnUIThread(async () => await ReloadAsync(_chatList));
                }
            }

            public void Handle(UpdateChatPosition update)
            {
                if (update.Position.List.AreTheSame(_chatList))
                {
                    Handle(update.ChatId, update.Position.Order);
                }

                // Can't be else otherwise cell won't update while archive is open
                if (update.Position.List is ChatListArchive)
                {
                    _viewModel.Delegate?.UpdateChatListArchive();
                }
            }

            public void Handle(UpdateChatLastMessage update)
            {
                Handle(update.ChatId, update.Positions, true);
            }

            public void Handle(UpdateChatDraftMessage update)
            {
                Handle(update.ChatId, update.Positions, true);
            }

            public void Handle(long chatId, IList<ChatPosition> positions, bool lastMessage = false)
            {
                var chat = GetChat(chatId);
                var order = 0L;

                for (int i = 0; i < positions.Count; i++)
                {
                    var position = positions[i];
                    if (position.List.AreTheSame(_chatList))
                    {
                        order = position.Order;
                    }

                    // Can't be else otherwise cell won't update while archive is open
                    if (position.List is ChatListArchive)
                    {
                        _viewModel.Delegate?.UpdateChatListArchive();
                    }
                }

                Handle(chat, order, lastMessage);
            }

            public void Handle(long chatId, long order)
            {
                var chat = GetChat(chatId);
                if (chat != null)
                {
                    Handle(chat, order, false);
                }
            }

            private void Handle(Chat chat, long order, bool lastMessage)
            {
                if (_viewModel._deletedChats.ContainsKey(chat.Id))
                {
                    if (order == 0)
                    {
                        _viewModel._deletedChats.Remove(chat.Id);
                    }
                    else
                    {
                        return;
                    }
                }

                //var chat = GetChat(chatId);
                if (chat != null /*&& _chatList.ListEquals(chat.ChatList)*/)
                {
                    _viewModel.BeginOnUIThread(() => UpdateChatOrder(chat, order, lastMessage));
                }
            }

            private void UpdateChatOrder(Chat chat, long order, bool lastMessage)
            {
                if (order > 0 && (order > _lastOrder || (order == _lastOrder && chat.Id >= _lastChatId)))
                {
                    var next = NextIndexOf(chat, order);
                    if (next >= 0)
                    {
                        if (_chats.Contains(chat.Id))
                        {
                            Remove(chat);
                        }
                        else
                        {
                            _chats.Add(chat.Id);
                        }

                        Insert(Math.Min(Count, next), chat);

                        if (next == Count - 1)
                        {
                            _lastChatId = chat.Id;
                            _lastOrder = order;
                        }

                        if (chat.Id == _viewModel._selectedItem)
                        {
                            _viewModel.Delegate?.SetSelectedItem(chat);
                        }
                        if (_viewModel.SelectedItems.Contains(chat))
                        {
                            _viewModel.Delegate?.SetSelectedItems(_viewModel.SelectedItems);
                        }

                        IsEmpty = Count == 0;
                    }
                    else if (lastMessage)
                    {
                        _viewModel.Delegate?.UpdateChatLastMessage(chat);
                    }
                }
                else if (_chats.Contains(chat.Id))
                {
                    _chats.Remove(chat.Id);
                    Remove(chat);

                    if (chat.Id == _viewModel._selectedItem)
                    {
                        _viewModel.Delegate?.SetSelectedItem(chat);
                    }
                    if (_viewModel.SelectedItems.Contains(chat))
                    {
                        _viewModel.SelectedItems.Remove(chat);
                        _viewModel.Delegate?.SetSelectedItems(_viewModel.SelectedItems);
                    }

                    IsEmpty = Count == 0;

                    //if (!_hasMoreItems)
                    //{
                    //    await LoadMoreItemsAsync(0);
                    //}
                }
            }

            private int NextIndexOf(Chat chat, long order)
            {
                var prev = -1;
                var next = 0;

                for (int i = 0; i < Count; i++)
                {
                    var item = this[i];
                    if (item.Id == chat.Id)
                    {
                        prev = i;
                        continue;
                    }

                    var itemOrder = item.GetOrder(_chatList);

                    if (order > itemOrder || order == itemOrder && chat.Id >= item.Id)
                    {
                        return next == prev ? -1 : next;
                    }

                    next++;
                }

                return Count;
            }

            private Chat GetChat(long chatId)
            {
                //if (_viewModels.ContainsKey(chatId))
                //{
                //    return _viewModels[chatId];
                //}
                //else
                //{
                //    var chat = ClientService.GetChat(chatId);
                //    var item = _viewModels[chatId] = new ChatViewModel(ClientService, chat);

                //    return item;
                //}

                return _clientService.GetChat(chatId);
            }

            #endregion

            private bool _isEmpty;
            public bool IsEmpty
            {
                get
                {
                    return _isEmpty;
                }
                set
                {
                    if (_isEmpty != value)
                    {
                        _isEmpty = value;
                        _viewModel.Dispatcher?.Dispatch(NotifyChanged, Windows.System.DispatcherQueuePriority.Low);
                    }
                }
            }

            private void NotifyChanged()
            {
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsEmpty)));
            }
        }
    }

    public enum SearchResultType
    {
        Recent,
        Chats,
        ChatsOnServer,
        Contacts,
        PublicChats,

        RecentWebApps,
        WebApps,
        ChatMembers,
        None
    }

    // TODO: always load User when creating by Chat
    public partial class SearchResult : BindableBase
    {
        private readonly IClientService _clientService;
        private readonly bool _canSendMessageToUser;

        public Chat Chat { get; set; }
        public User User { get; set; }
        public ForumTopic Topic { get; set; }

        public string Query { get; set; }

        public SearchResultType Type { get; }

        public bool IsPublic => Type == SearchResultType.PublicChats;

        public SearchResult(IClientService clientService, Chat chat, string query, SearchResultType type, bool canSendMessageToUser)
        {
            _clientService = clientService;

            Chat = chat;
            Query = query;
            Type = type;
        }

        public SearchResult(IClientService clientService, Chat chat, bool canSendMessageToUser)
        {
            _clientService = clientService;

            Chat = chat;
            Query = string.Empty;
            Type = SearchResultType.None;
        }

        public SearchResult(IClientService clientService, User user, string query, SearchResultType type, bool canSendMessageToUser)
        {
            _clientService = clientService;

            User = user;
            Query = query;
            Type = type;
        }

        public SearchResult(ForumTopic topic, string query, SearchResultType type)
        {
            Topic = topic;
            Query = query;
            Type = type;
        }

        private bool? _restrictsNewChats;
        public bool? RestrictsNewChats
        {
            get => _restrictsNewChats;
            set => Set(ref _restrictsNewChats, value);
        }

        public void CanSendMessageToUser()
        {
            long? userId;
            if (Chat?.Type is ChatTypePrivate privata)
            {
                userId = privata.UserId;
            }
            else if (Chat?.Type is ChatTypeSecret secret)
            {
                userId = secret.UserId;
            }
            else
            {
                userId = User?.Id;
            }

            if (userId == null || !_canSendMessageToUser || _restrictsNewChats.HasValue)
            {
                return;
            }

            _restrictsNewChats = false;

            var dispatcher = WindowContext.Current.Dispatcher;
            if (dispatcher == null)
            {
                return;
            }

            _clientService.Send(new CanSendMessageToUser(userId.Value, false), result =>
            {
                if (result is CanSendMessageToUserResultUserRestrictsNewChats)
                {
                    dispatcher.Dispatch(() => RestrictsNewChats = true);
                }
            });
        }
    }
}

namespace Telegram.Td.Api
{
    [Flags]
    public enum ChatListFolderFlags
    {
        IncludeContacts,
        IncludeNonContacts,
        IncludeGroups,
        IncludeChannels,
        IncludeBots,
        ExcludeMuted,
        ExcludeRead,
        ExcludeArchived,

        // Used by business recipients
        NewChats,
        ExistingChats,
    }
}
