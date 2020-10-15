using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Unigram.Views.Folders;
using Unigram.Views.Popups;
using Windows.Foundation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace Unigram.ViewModels
{
    public class ChatsViewModel : TLViewModelBase, IDelegable<IChatsDelegate>, IChatListDelegate
    {
        private readonly INotificationsService _notificationsService;

        private readonly Dictionary<long, bool> _deletedChats = new Dictionary<long, bool>();

        public IChatsDelegate Delegate { get; set; }

        public ChatsViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, INotificationsService notificationsService, ChatList chatList)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _notificationsService = notificationsService;

            Items = new ItemsCollection(protoService, aggregator, this, chatList);

            SearchFilters = new MvxObservableCollection<ISearchChatsFilter>();

            ChatPinCommand = new RelayCommand<Chat>(ChatPinExecute);
            ChatArchiveCommand = new RelayCommand<Chat>(ChatArchiveExecute);
            ChatMarkCommand = new RelayCommand<Chat>(ChatMarkExecute);
            ChatNotifyCommand = new RelayCommand<Chat>(ChatNotifyExecute);
            ChatDeleteCommand = new RelayCommand<Chat>(ChatDeleteExecute);
            ChatClearCommand = new RelayCommand<Chat>(ChatClearExecute);
            ChatSelectCommand = new RelayCommand<Chat>(ChatSelectExecute);

            ChatsMarkCommand = new RelayCommand(ChatsMarkExecute);
            ChatsNotifyCommand = new RelayCommand(ChatsNotifyExecute);
            ChatsArchiveCommand = new RelayCommand(ChatsArchiveExecute);
            ChatsDeleteCommand = new RelayCommand(ChatsDeleteExecute);
            ChatsClearCommand = new RelayCommand(ChatsClearExecute);

            FolderAddCommand = new RelayCommand<(int, Chat)>(FolderAddExecute);
            FolderRemoveCommand = new RelayCommand<(int, Chat)>(FolderRemoveExecute);
            FolderCreateCommand = new RelayCommand<Chat>(FolderCreateExecute);

            ClearRecentChatsCommand = new RelayCommand(ClearRecentChatsExecute);

            TopChatDeleteCommand = new RelayCommand<Chat>(TopChatDeleteExecute);

#if MOCKUP
            Items.AddRange(protoService.GetChats(null));
#endif

            SelectedItems = new MvxObservableCollection<Chat>();
        }

        #region Selection

        private long? _selectedItem;
        public long? SelectedItem
        {
            get { return _selectedItem; }
            set { Set(ref _selectedItem, value); }
        }

        private MvxObservableCollection<Chat> _selectedItems;
        public MvxObservableCollection<Chat> SelectedItems
        {
            get { return _selectedItems; }
            set { Set(ref _selectedItems, value); }
        }

        private ListViewSelectionMode _selectionMode = ListViewSelectionMode.None;
        public ListViewSelectionMode SelectionMode
        {
            get { return _selectionMode; }
            set { Set(ref _selectionMode, value); }
        }

        public int SelectedCount => _selectedItems.Count;

        public void SetSelectionMode(bool enabled)
        {
            Delegate?.SetSelectionMode(enabled);
        }

        public void SetSelectedItem(Chat chat)
        {
            if (SelectionMode != ListViewSelectionMode.Multiple)
            {
                Delegate?.Navigate(chat);
                //ChatsList.SelectedItem = chat;
            }
        }

        public void SetSelectedItems(IList<Chat> chats)
        {
            //if (ViewModel.Chats.SelectionMode == ListViewSelectionMode.Multiple)
            //{
            //    foreach (var item in chats)
            //    {
            //        if (!ChatsList.SelectedItems.Contains(item))
            //        {
            //            ChatsList.SelectedItems.Add(item);
            //        }
            //    }

            //    foreach (Chat item in ChatsList.SelectedItems)
            //    {
            //        if (!chats.Contains(item))
            //        {
            //            ChatsList.SelectedItems.Remove(item);
            //        }
            //    }
            //}
        }

        public void AddSelectedItem(Chat chat)
        {
            SelectedItems.Add(chat);
        }

        public void RemoveSelectedItem(Chat chat)
        {
            SelectedItems.Remove(chat);
        }

        public bool IsItemSelected(Chat chat)
        {
            return SelectedItems.Contains(chat);
        }

        #endregion

        public ItemsCollection Items { get; private set; }

        public bool IsLastSliceLoaded { get; set; }

        private SearchChatsCollection _search;
        public SearchChatsCollection Search
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

        public MvxObservableCollection<ISearchChatsFilter> SearchFilters { get; private set; }

        private TopChatsCollection _topChats;
        public TopChatsCollection TopChats
        {
            get
            {
                return _topChats;
            }
            set
            {
                Set(ref _topChats, value);
            }
        }

        #region Pin

        public RelayCommand<Chat> ChatPinCommand { get; }
        private void ChatPinExecute(Chat chat)
        {
            var position = chat.GetPosition(Items.ChatList);
            if (position == null)
            {
                return;
            }

            ProtoService.Send(new ToggleChatIsPinned(Items.ChatList, chat.Id, !position.IsPinned));
        }

        #endregion

        #region Archive

        public RelayCommand<Chat> ChatArchiveCommand { get; }
        private void ChatArchiveExecute(Chat chat)
        {
            var archived = chat.Positions.Any(x => x.List is ChatListArchive);
            if (archived)
            {
                ProtoService.Send(new AddChatToList(chat.Id, new ChatListMain()));
                return;
            }
            else
            {
                ProtoService.Send(new AddChatToList(chat.Id, new ChatListArchive()));
            }

            Delegate?.ShowChatsUndo(new[] { chat }, UndoType.Archive, items =>
            {
                var undo = items.FirstOrDefault();
                if (undo == null)
                {
                    return;
                }

                ProtoService.Send(new AddChatToList(chat.Id, new ChatListMain()));
            });
        }

        #endregion

        #region Multiple Archive

        public RelayCommand ChatsArchiveCommand { get; }
        private void ChatsArchiveExecute()
        {
            var chats = SelectedItems.ToList();

            foreach (var chat in chats)
            {
                ProtoService.Send(new AddChatToList(chat.Id, new ChatListArchive()));
            }

            Delegate?.ShowChatsUndo(chats, UndoType.Archive, items =>
            {
                foreach (var undo in items)
                {
                    ProtoService.Send(new AddChatToList(undo.Id, new ChatListMain()));
                }
            });

            Delegate?.SetSelectionMode(false);
            SelectedItems.Clear();
        }

        #endregion

        #region Mark

        public RelayCommand<Chat> ChatMarkCommand { get; }
        private void ChatMarkExecute(Chat chat)
        {
            if (chat.UnreadCount > 0)
            {
                ProtoService.Send(new ViewMessages(chat.Id, 0, new[] { chat.LastMessage.Id }, true));

                if (chat.UnreadMentionCount > 0)
                {
                    ProtoService.Send(new ReadAllChatMentions(chat.Id));
                }
            }
            else
            {
                ProtoService.Send(new ToggleChatIsMarkedAsUnread(chat.Id, !chat.IsMarkedAsUnread));
            }
        }

        #endregion

        #region Multiple Mark

        public RelayCommand ChatsMarkCommand { get; }
        private void ChatsMarkExecute()
        {
            var chats = SelectedItems.ToList();
            var unread = chats.Any(x => x.IsUnread());
            foreach (var chat in chats)
            {
                if (unread)
                {
                    if (chat.UnreadCount > 0)
                    {
                        ProtoService.Send(new ViewMessages(chat.Id, 0, new[] { chat.LastMessage.Id }, true));
                    }
                    else if (chat.IsMarkedAsUnread)
                    {
                        ProtoService.Send(new ToggleChatIsMarkedAsUnread(chat.Id, false));
                    }

                    if (chat.UnreadMentionCount > 0)
                    {
                        ProtoService.Send(new ReadAllChatMentions(chat.Id));
                    }
                }
                else if (chat.UnreadCount == 0 && !chat.IsMarkedAsUnread)
                {
                    ProtoService.Send(new ToggleChatIsMarkedAsUnread(chat.Id, true));
                }
            }

            Delegate?.SetSelectionMode(false);
            SelectedItems.Clear();
        }

        #endregion

        #region Notify

        public RelayCommand<Chat> ChatNotifyCommand { get; }
        private void ChatNotifyExecute(Chat chat)
        {
            _notificationsService.SetMuteFor(chat, CacheService.GetNotificationSettingsMuteFor(chat) > 0 ? 0 : 632053052);
        }

        #endregion

        #region Multiple Notify

        public RelayCommand ChatsNotifyCommand { get; }
        private void ChatsNotifyExecute()
        {
            var chats = SelectedItems.ToList();
            var muted = chats.Any(x => CacheService.GetNotificationSettingsMuteFor(x) > 0);

            foreach (var chat in chats)
            {
                if (chat.Type is ChatTypePrivate privata && privata.UserId == CacheService.Options.MyId)
                {
                    continue;
                }

                _notificationsService.SetMuteFor(chat, muted ? 0 : 632053052);
            }

            Delegate?.SetSelectionMode(false);
            SelectedItems.Clear();
        }

        #endregion

        #region Delete

        public RelayCommand<Chat> ChatDeleteCommand { get; }
        private async void ChatDeleteExecute(Chat chat)
        {
            var updated = await ProtoService.SendAsync(new GetChat(chat.Id)) as Chat ?? chat;
            var dialog = new DeleteChatPopup(ProtoService, updated, Items.ChatList, false);

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                var check = dialog.IsChecked == true;

                _deletedChats[chat.Id] = true;
                Items.Handle(chat.Id, 0);

                Delegate?.ShowChatsUndo(new[] { chat }, UndoType.Delete, items =>
                {
                    var undo = items.FirstOrDefault();
                    if (undo == null)
                    {
                        return;
                    }

                    _deletedChats.Remove(undo.Id);
                    Items.Handle(undo.Id, undo.Positions);
                }, async items =>
                {
                    var delete = items.FirstOrDefault();
                    if (delete == null)
                    {
                        return;
                    }

                    if (delete.Type is ChatTypeSecret secret)
                    {
                        await ProtoService.SendAsync(new CloseSecretChat(secret.SecretChatId));
                    }
                    else if (delete.Type is ChatTypeBasicGroup || delete.Type is ChatTypeSupergroup)
                    {
                        await ProtoService.SendAsync(new LeaveChat(delete.Id));
                    }

                    var user = CacheService.GetUser(delete);
                    if (user != null && user.Type is UserTypeRegular)
                    {
                        ProtoService.Send(new DeleteChatHistory(delete.Id, true, check));
                    }
                    else
                    {
                        if (delete.Type is ChatTypePrivate && check)
                        {
                            await ProtoService.SendAsync(new ToggleChatIsBlocked(delete.Id, true));
                        }

                        ProtoService.Send(new DeleteChatHistory(delete.Id, true, false));
                    }
                });
            }
        }

        #endregion

        #region Multiple Delete

        public RelayCommand ChatsDeleteCommand { get; }
        private async void ChatsDeleteExecute()
        {
            var chats = SelectedItems.ToList();

            var confirm = await MessagePopup.ShowAsync(Strings.Resources.AreYouSureDeleteFewChats, Locale.Declension("ChatsSelected", chats.Count), Strings.Resources.Delete, Strings.Resources.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                foreach (var chat in chats)
                {
                    _deletedChats[chat.Id] = true;
                    Items.Handle(chat.Id, 0);
                }

                Delegate?.ShowChatsUndo(chats, UndoType.Delete, items =>
                {
                    foreach (var undo in items)
                    {
                        _deletedChats.Remove(undo.Id);
                        Items.Handle(undo.Id, undo.Positions);
                    }
                }, async items =>
                {
                    foreach (var delete in items)
                    {
                        if (delete.Type is ChatTypeSecret secret)
                        {
                            await ProtoService.SendAsync(new CloseSecretChat(secret.SecretChatId));
                        }
                        else if (delete.Type is ChatTypeBasicGroup || delete.Type is ChatTypeSupergroup)
                        {
                            await ProtoService.SendAsync(new LeaveChat(delete.Id));
                        }

                        ProtoService.Send(new DeleteChatHistory(delete.Id, true, false));
                    }
                });
            }

            Delegate?.SetSelectionMode(false);
            SelectedItems.Clear();
        }

        #endregion

        #region Clear

        public RelayCommand<Chat> ChatClearCommand { get; }
        private async void ChatClearExecute(Chat chat)
        {
            var updated = await ProtoService.SendAsync(new GetChat(chat.Id)) as Chat ?? chat;
            var dialog = new DeleteChatPopup(ProtoService, updated, Items.ChatList, true);

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                Delegate?.ShowChatsUndo(new[] { chat }, UndoType.Clear, items =>
                {
                    var undo = items.FirstOrDefault();
                    if (undo == null)
                    {
                        return;
                    }

                    _deletedChats.Remove(undo.Id);
                    Items.Handle(undo.Id, undo.Positions);
                }, items =>
                {
                    foreach (var delete in items)
                    {
                        ProtoService.Send(new DeleteChatHistory(delete.Id, false, dialog.IsChecked));
                    }
                });
            }
        }

        #endregion

        #region Multiple Clear

        public RelayCommand ChatsClearCommand { get; }
        private async void ChatsClearExecute()
        {
            var chats = SelectedItems.ToList();

            var confirm = await MessagePopup.ShowAsync(Strings.Resources.AreYouSureClearHistoryFewChats, Locale.Declension("ChatsSelected", chats.Count), Strings.Resources.ClearHistory, Strings.Resources.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                Delegate?.ShowChatsUndo(chats, UndoType.Clear, items =>
                {
                    foreach (var undo in items)
                    {
                        _deletedChats.Remove(undo.Id);
                        Items.Handle(undo.Id, undo.Positions);
                    }
                }, items =>
                {
                    var clear = items.FirstOrDefault();
                    if (clear == null)
                    {
                        return;
                    }

                    ProtoService.Send(new DeleteChatHistory(clear.Id, false, false));
                });
            }

            Delegate?.SetSelectionMode(false);
            SelectedItems.Clear();
        }

        #endregion

        #region Select

        public RelayCommand<Chat> ChatSelectCommand { get; }
        private void ChatSelectExecute(Chat chat)
        {
            SelectedItems.ReplaceWith(new[] { chat });
            SelectionMode = ListViewSelectionMode.Multiple;

            //Delegate?.SetSelectedItems(_selectedItems);
        }

        #endregion

        #region Commands

        public RelayCommand ClearRecentChatsCommand { get; }
        private async void ClearRecentChatsExecute()
        {
            var confirm = await MessagePopup.ShowAsync(Strings.Resources.ClearSearch, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            ProtoService.Send(new ClearRecentlyFoundChats());

            var items = Search;
            if (items != null && string.IsNullOrEmpty(items.Query))
            {
                items.Clear();
            }
        }

        public RelayCommand<Chat> TopChatDeleteCommand { get; }
        private async void TopChatDeleteExecute(Chat chat)
        {
            if (chat == null)
            {
                return;
            }

            var confirm = await MessagePopup.ShowAsync(string.Format(Strings.Resources.ChatHintsDelete, CacheService.GetTitle(chat)), Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            ProtoService.Send(new RemoveTopChat(new TopChatCategoryUsers(), chat.Id));
            TopChats?.Remove(chat);
        }

        #endregion

        #region Folder add

        public RelayCommand<(int, Chat)> FolderAddCommand { get; }
        private async void FolderAddExecute((int ChatFilterId, Chat Chat) data)
        {
            var filter = await ProtoService.SendAsync(new GetChatFilter(data.ChatFilterId)) as ChatFilter;
            if (filter == null)
            {
                return;
            }

            var total = filter.IncludedChatIds.Count + filter.PinnedChatIds.Count + 1;
            if (total > 99)
            {
                await MessagePopup.ShowAsync(Strings.Resources.FilterAddToAlertFullText, Strings.Resources.FilterAddToAlertFullTitle, Strings.Resources.OK);
                return;
            }

            if (filter.IncludedChatIds.Contains(data.Chat.Id))
            {
                // Warn user about chat being already in the folder?
                return;
            }

            filter.ExcludedChatIds.Remove(data.Chat.Id);
            filter.IncludedChatIds.Add(data.Chat.Id);

            ProtoService.Send(new EditChatFilter(data.ChatFilterId, filter));
        }

        #endregion

        #region Folder remove

        public RelayCommand<(int, Chat)> FolderRemoveCommand { get; }
        private async void FolderRemoveExecute((int ChatFilterId, Chat Chat) data)
        {
            var filter = await ProtoService.SendAsync(new GetChatFilter(data.ChatFilterId)) as ChatFilter;
            if (filter == null)
            {
                return;
            }

            var total = filter.ExcludedChatIds.Count + 1;
            if (total > 99)
            {
                await MessagePopup.ShowAsync(Strings.Resources.FilterRemoveFromAlertFullText, Strings.Resources.AppName, Strings.Resources.OK);
                return;
            }

            if (filter.ExcludedChatIds.Contains(data.Chat.Id))
            {
                // Warn user about chat being already in the folder?
                return;
            }

            filter.IncludedChatIds.Remove(data.Chat.Id);
            filter.ExcludedChatIds.Add(data.Chat.Id);

            ProtoService.Send(new EditChatFilter(data.ChatFilterId, filter));
        }

        #endregion

        #region Folder create

        public RelayCommand<Chat> FolderCreateCommand { get; }
        private void FolderCreateExecute(Chat chat)
        {
            NavigationService.Navigate(typeof(FolderPage), state: new NavigationState { { "included_chat_id", chat.Id } });
        }

        #endregion

        public void SetFilter(ChatList chatList)
        {
            Aggregator.Unsubscribe(Items);
            Items = new ItemsCollection(ProtoService, Aggregator, this, chatList);
            RaisePropertyChanged(() => Items);
        }

        public class ItemsCollection : ObservableCollection<Chat>, ISupportIncrementalLoading,
            IHandle<UpdateChatDraftMessage>,
            IHandle<UpdateChatLastMessage>,
            IHandle<UpdateChatPosition>
        {
            private readonly IProtoService _protoService;
            private readonly IEventAggregator _aggregator;

            private readonly DisposableMutex _loadMoreLock = new DisposableMutex();

            private readonly ChatsViewModel _viewModel;

            private readonly ChatList _chatList;

            private bool _hasMoreItems = true;

            private long _internalChatId = 0;
            private long _internalOrder = long.MaxValue;

            private long _lastChatId;
            private long _lastOrder;

            public ChatList ChatList => _chatList;

            public ItemsCollection(IProtoService protoService, IEventAggregator aggregator, ChatsViewModel viewModel, ChatList chatList)
            {
                _protoService = protoService;
                _aggregator = aggregator;

                _viewModel = viewModel;

                _chatList = chatList;

#if MOCKUP
                _hasMoreItems = false;
#endif

                _ = LoadMoreItemsAsync(0);
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(token => LoadMoreItemsAsync());
            }

            private async Task<LoadMoreItemsResult> LoadMoreItemsAsync()
            {
                using (await _loadMoreLock.WaitAsync())
                {
                    //var response = await _protoService.SendAsync(new GetChats(_chatList, _internalOrder, _internalChatId, 20));
                    var response = await _protoService.GetChatListAsync(_chatList, Count, 20);
                    if (response is Telegram.Td.Api.Chats chats)
                    {
                        foreach (var id in chats.ChatIds)
                        {
                            var chat = _protoService.GetChat(id);
                            var order = chat.GetOrder(_chatList);

                            if (chat != null && order != 0)
                            {
                                _internalChatId = chat.Id;
                                _internalOrder = order;

                                var next = NextIndexOf(chat, order);
                                if (next >= 0)
                                {
                                    Insert(next, chat);
                                }

                                _lastChatId = chat.Id;
                                _lastOrder = order;
                            }
                        }

                        _hasMoreItems = chats.ChatIds.Count > 0;
                        _aggregator.Subscribe(this);

                        _viewModel.Delegate?.SetSelectedItems(_viewModel.SelectedItems);

                        return new LoadMoreItemsResult { Count = (uint)chats.ChatIds.Count };
                    }

                    return new LoadMoreItemsResult { Count = 0 };
                }
            }

            public bool HasMoreItems => _hasMoreItems;

            #region Handle

            public void Handle(UpdateChatPosition update)
            {
                if (update.Position.List.ListEquals(_chatList))
                {
                    Handle(update.ChatId, update.Position.Order);
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
                var order = chat.GetOrder(_chatList);

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

            private async void UpdateChatOrder(Chat chat, long order, bool lastMessage)
            {
                if (order > _lastOrder || (order == _lastOrder && chat.Id >= _lastChatId))
                {
                    using (await _loadMoreLock.WaitAsync())
                    {
                        var next = NextIndexOf(chat, order);
                        if (next >= 0)
                        {
                            Remove(chat);
                            Insert(Math.Min(Count, next), chat);

                            if (chat.Id == _viewModel._selectedItem)
                            {
                                _viewModel.Delegate?.SetSelectedItem(chat);
                            }
                            if (_viewModel.SelectedItems.Contains(chat))
                            {
                                _viewModel.Delegate?.SetSelectedItems(_viewModel._selectedItems);
                            }
                        }
                        else if (lastMessage)
                        {
                            _viewModel.Delegate?.UpdateChatLastMessage(chat);
                        }
                    }
                }
                else
                {
                    using (await _loadMoreLock.WaitAsync())
                    {
                        Remove(chat);
                    }

                    if (chat.Id == _viewModel._selectedItem)
                    {
                        _viewModel.Delegate?.SetSelectedItem(chat);
                    }
                    if (_viewModel.SelectedItems.Contains(chat))
                    {
                        _viewModel.SelectedItems.Remove(chat);
                        _viewModel.Delegate?.SetSelectedItems(_viewModel._selectedItems);
                    }

                    if (!_hasMoreItems)
                    {
                        await LoadMoreItemsAsync(0);
                    }
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
                //    var chat = ProtoService.GetChat(chatId);
                //    var item = _viewModels[chatId] = new ChatViewModel(ProtoService, chat);

                //    return item;
                //}

                return _protoService.GetChat(chatId);
            }

            #endregion
        }
    }

    public class SearchResult
    {
        public Chat Chat { get; set; }
        public User User { get; set; }

        public string Query { get; set; }

        public bool IsPublic { get; set; }

        public SearchResult(Chat chat, string query, bool pub)
        {
            Chat = chat;
            Query = query;
            IsPublic = pub;
        }

        public SearchResult(User user, string query, bool pub)
        {
            User = user;
            Query = query;
            IsPublic = pub;
        }
    }
}

namespace Telegram.Td.Api
{
    [Flags]
    public enum ChatListFilterFlags
    {
        IncludeContacts,
        IncludeNonContacts,
        IncludeGroups,
        IncludeChannels,
        IncludeBots,
        ExcludeMuted,
        ExcludeRead,
        ExcludeArchived
    }
}
