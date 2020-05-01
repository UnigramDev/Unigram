using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Views;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Windows.Foundation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace Unigram.ViewModels
{
    public class ChatsViewModel : TLViewModelBase, IDelegable<IChatsDelegate>, IChatListDelegate, IHandle<UpdateChatDraftMessage>, IHandle<UpdateChatIsPinned>, IHandle<UpdateChatIsSponsored>, IHandle<UpdateChatLastMessage>, IHandle<UpdateChatOrder>
    {
        private readonly INotificationsService _notificationsService;

        private readonly Dictionary<long, ChatViewModel> _viewModels = new Dictionary<long, ChatViewModel>();
        private readonly Dictionary<long, bool> _deletedChats = new Dictionary<long, bool>();

        private ChatList _chatList;
        public ChatList ChatList => _chatList;

        public IChatsDelegate Delegate { get; set; }

        public ChatsViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, INotificationsService notificationsService, ChatList chatList, ChatListFilter filter = null)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _notificationsService = notificationsService;

            _chatList = chatList;

            Items = new ItemsCollection(protoService, aggregator, this, chatList, filter);

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

            ClearRecentChatsCommand = new RelayCommand(ClearRecentChatsExecute);

            TopChatDeleteCommand = new RelayCommand<Chat>(TopChatDeleteExecute);

#if MOCKUP
            Items.AddRange(protoService.GetChats(20));
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
            ProtoService.Send(new ToggleChatIsPinned(chat.Id, !chat.IsPinned));
        }

        #endregion

        #region Archive

        public RelayCommand<Chat> ChatArchiveCommand { get; }
        private void ChatArchiveExecute(Chat chat)
        {
            var chatList = chat.ChatList;
            if (chatList is ChatListArchive)
            {
                ProtoService.Send(new SetChatChatList(chat.Id, new ChatListMain()));
                return;
            }
            else
            {
                ProtoService.Send(new SetChatChatList(chat.Id, new ChatListArchive()));
            }

            Delegate?.ShowChatsUndo(new[] { chat }, UndoType.Archive, items =>
            {
                var undo = items.FirstOrDefault();
                if (undo == null)
                {
                    return;
                }

                ProtoService.Send(new SetChatChatList(chat.Id, chatList));
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
                ProtoService.Send(new SetChatChatList(chat.Id, new ChatListArchive()));
            }

            Delegate?.ShowChatsUndo(chats, UndoType.Archive, items =>
            {
                foreach (var undo in items)
                {
                    ProtoService.Send(new SetChatChatList(undo.Id, new ChatListMain()));
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
                ProtoService.Send(new ViewMessages(chat.Id, new[] { chat.LastMessage.Id }, true));

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
                        ProtoService.Send(new ViewMessages(chat.Id, new[] { chat.LastMessage.Id }, true));
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
            var dialog = new DeleteChatView(ProtoService, chat, false);

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                var check = dialog.IsChecked == true;

                _deletedChats[chat.Id] = true;
                Handle(chat.Id, 0);

                Delegate?.ShowChatsUndo(new[] { chat }, UndoType.Delete, items =>
                {
                    var undo = items.FirstOrDefault();
                    if (undo == null)
                    {
                        return;
                    }

                    _deletedChats.Remove(undo.Id);
                    Handle(undo.Id, undo.Order);
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
                        if (delete.Type is ChatTypePrivate privata && check)
                        {
                            await ProtoService.SendAsync(new BlockUser(privata.UserId));
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

            var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.AreYouSureDeleteFewChats, Locale.Declension("ChatsSelected", chats.Count), Strings.Resources.Delete, Strings.Resources.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                foreach (var chat in chats)
                {
                    _deletedChats[chat.Id] = true;
                    Handle(chat.Id, 0);
                }

                Delegate?.ShowChatsUndo(chats, UndoType.Delete, items =>
                {
                    foreach (var undo in items)
                    {
                        _deletedChats.Remove(undo.Id);
                        Handle(undo.Id, undo.Order);
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
            var dialog = new DeleteChatView(ProtoService, chat, true);

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
                    Handle(undo.Id, undo.Order);
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

            var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.AreYouSureClearHistoryFewChats, Locale.Declension("ChatsSelected", chats.Count), Strings.Resources.ClearHistory, Strings.Resources.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                Delegate?.ShowChatsUndo(chats, UndoType.Clear, items =>
                {
                    foreach (var undo in items)
                    {
                        _deletedChats.Remove(undo.Id);
                        Handle(undo.Id, undo.Order);
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
            var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.ClearSearch, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
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

            var confirm = await TLMessageDialog.ShowAsync(string.Format(Strings.Resources.ChatHintsDelete, CacheService.GetTitle(chat)), Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            ProtoService.Send(new RemoveTopChat(new TopChatCategoryUsers(), chat.Id));
            TopChats.Remove(chat);
        }

        #endregion

        public void Handle(UpdateChatOrder update)
        {
            Handle(update.ChatId, update.Order);
        }

        public void Handle(UpdateChatLastMessage update)
        {
            Handle(update.ChatId, update.Order);
        }

        public void Handle(UpdateChatIsPinned update)
        {
            Handle(update.ChatId, update.Order);
        }

        public void Handle(UpdateChatIsSponsored update)
        {
            Handle(update.ChatId, update.Order);
        }

        public void Handle(UpdateChatDraftMessage update)
        {
            Handle(update.ChatId, update.Order);
        }

        private void Handle(long chatId, long order)
        {
            if (_deletedChats.ContainsKey(chatId))
            {
                if (order == 0)
                {
                    _deletedChats.Remove(chatId);
                }
                else
                {
                    return;
                }
            }

            var items = Items;

            var chat = GetChat(chatId);
            if (chat != null && _chatList.ListEquals(chat.ChatList))
            {
                if (items.Filter != null && !items.CanAdd(chat))
                {
                    BeginOnUIThread(() => items.Remove(chat));
                    return;
                }

                BeginOnUIThread(async () =>
                {
                    if (order > items.LastOrder || (order == items.LastOrder && chatId >= items.LastChatId))
                    {
                        var index = items.IndexOf(chat);
                        var next = items.NextIndexOf(chat);

                        if (next >= 0 && index != next)
                        {
                            items.Remove(chat);
                            items.Insert(next, chat);

                            if (chat.Id == _selectedItem)
                            {
                                Delegate?.SetSelectedItem(chat);
                            }
                            if (SelectedItems.Contains(chat))
                            {
                                Delegate?.SetSelectedItems(_selectedItems);
                            }
                        }
                    }
                    else
                    {
                        items.Remove(chat);

                        if (chat.Id == _selectedItem)
                        {
                            Delegate?.SetSelectedItem(chat);
                        }
                        if (SelectedItems.Contains(chat))
                        {
                            SelectedItems.Remove(chat);
                            Delegate?.SetSelectedItems(_selectedItems);
                        }

                        if (!items.HasMoreItems)
                        {
                            await items.LoadMoreItemsAsync(0);
                        }
                    }
                });
            }
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

            return ProtoService.GetChat(chatId);
        }

        public void SetFilter(ChatListFilter filter)
        {
            Items = new ItemsCollection(ProtoService, Aggregator, this, _chatList, filter);
            RaisePropertyChanged(() => Items);
        }

        public class ItemsCollection : SortedObservableCollection<Chat>, IGroupSupportIncrementalLoading
        {
            class ChatComparer : IComparer<Chat>
            {
                public int Compare(Chat x, Chat y)
                {
                    return y.Order.CompareTo(x.Order);
                }
            }

            private readonly IProtoService _protoService;
            private readonly IEventAggregator _aggregator;

            private readonly ChatsViewModel _viewModel;

            private readonly ChatList _chatList;
            private readonly ChatListFilter _filter;

            private bool _hasMoreItems = true;

            private DisposableMutex _loadMoreLock = new DisposableMutex();

            private long _internalChatId = 0;
            private long _internalOrder = long.MaxValue;

            private long _lastChatId;
            private long _lastOrder;

            public long LastChatId => _lastChatId;
            public long LastOrder => _lastOrder;

            public ChatListFilter Filter => _filter;

            public ItemsCollection(IProtoService protoService, IEventAggregator aggregator, ChatsViewModel viewModel, ChatList chatList, ChatListFilter filter = null)
                : base(new ChatComparer(), true)
            {
                _protoService = protoService;
                _aggregator = aggregator;

                _viewModel = viewModel;

                _chatList = chatList;
                _filter = filter;

#if MOCKUP
                _hasMoreItems = false;
#endif

                _ = LoadMoreItemsAsync(0);
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(async token =>
                {
                    using (await _loadMoreLock.WaitAsync())
                    {
                        var response = await _protoService.SendAsync(new GetChats(_chatList, _internalOrder, _internalChatId, 20));
                        if (response is Telegram.Td.Api.Chats chats)
                        {
                            foreach (var id in chats.ChatIds)
                            {
                                var chat = _protoService.GetChat(id);
                                if (chat != null && chat.Order != 0)
                                {
                                    _internalChatId = chat.Id;
                                    _internalOrder = chat.Order;

                                    if (_filter != null && !CanAdd(chat))
                                    {
                                        continue;
                                    }

                                    Add(chat);

                                    _lastChatId = chat.Id;
                                    _lastOrder = chat.Order;
                                }
                            }

                            _hasMoreItems = chats.ChatIds.Count > 0;
                            _aggregator.Subscribe(_viewModel);

                            _viewModel.Delegate?.SetSelectedItems(_viewModel.SelectedItems);

                            return new LoadMoreItemsResult { Count = (uint)chats.ChatIds.Count };
                        }

                        return new LoadMoreItemsResult { Count = 0 };
                    }
                });
            }

            public bool HasMoreItems => _hasMoreItems;

            public bool CanAdd(Chat chat)
            {
                if (_filter == null)
                {
                    return true;
                }

                if (_filter.IncludeChats != null && _filter.IncludeChats.Contains(chat.Id))
                {
                    return true;
                }
                else if (_filter.ExcludeChats != null && _filter.ExcludeChats.Contains(chat.Id))
                {
                    return false;
                }

                if (_filter.ExcludeMuted && _protoService.GetNotificationSettingsMuteFor(chat) > 0)
                {
                    return false;
                }
                if (_filter.ExcludeRead && !(chat.UnreadCount > 0 || chat.UnreadMentionCount > 0 || chat.IsMarkedAsUnread))
                {
                    return false;
                }

                if (_filter.IncludeAll())
                {
                    return true;
                }

                if (chat.Type is ChatTypePrivate || chat.Type is ChatTypeSecret /* ? */)
                {
                    var user = _protoService.GetUser(chat);
                    if (user.Type is UserTypeBot)
                    {
                        return _filter.IncludeBots;
                    }
                    else if (user.IsContact)
                    {
                        return _filter.IncludeContacts;
                    }
                    
                    return _filter.IncludeNonContacts;
                }
                else if (chat.Type is ChatTypeBasicGroup)
                {
                    return _filter.IncludeGroups;
                }
                else if (chat.Type is ChatTypeSupergroup)
                {
                    var supergroup = _protoService.GetSupergroup(chat);
                    if (supergroup.IsChannel)
                    {
                        return _filter.IncludeChannels;
                    }

                    return _filter.IncludeGroups;
                }

                return false;
            }
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
    public class SetChatFilter : Function
    {
        public ChatListFilter Filter { get; }

        public SetChatFilter(ChatListFilter filter)
        {
            Filter = filter;
        }

        public NativeObject ToUnmanaged()
        {
            throw new NotImplementedException();
        }
    }

    public class GetChatListFilters : Function
    {
        public NativeObject ToUnmanaged()
        {
            throw new NotImplementedException();
        }
    }

    public class ChatListFilters : BaseObject
    {
        public IList<ChatListFilter> Filters { get; set; }

        public NativeObject ToUnmanaged()
        {
            throw new NotImplementedException();
        }
    }

    public class ChatListFilterSuggestion : BaseObject
    {
        public ChatListFilter Filter { get; set; }

        public string Description { get; set; } = string.Empty;

        public NativeObject ToUnmanaged()
        {
            throw new NotImplementedException();
        }
    }

    public class ChatListFilterSuggestions : BaseObject
    {
        public IList<ChatListFilterSuggestion> Suggestions { get; set; }

        public NativeObject ToUnmanaged()
        {
            throw new NotImplementedException();
        }
    }

    public class GetChatListFilterSuggestons : Function
    {
        public NativeObject ToUnmanaged()
        {
            throw new NotImplementedException();
        }
    }

    public class ChatListFilter
    {
        // dialogFilter flags:# id:int title:string pm:flags.0?true secret_chats:flags.1?true private_groups:flags.2?true public_groups:flags.3?true broadcasts:flags.4?true bots:flags.5?true exclude_muted:flags.11?true exclude_read:flags.12?true include_peers:Vector<InputPeer> = DialogFilter;

        public int Id { get; set; }
        public string Title { get; set; }

        public bool IncludeContacts { get; set; } = true;
        public bool IncludeNonContacts { get; set; } = true;
        public bool IncludeGroups { get; set; } = true;
        public bool IncludeChannels { get; set; } = true;
        public bool IncludeBots { get; set; } = true;

        public IList<long> IncludeChats { get; set; } = new long[0];

        public bool ExcludeMuted { get; set; }
        public bool ExcludeRead { get; set; }
        public bool ExcludeArchived { get; set; }

        public IList<long> ExcludeChats { get; set; } = new long[0];

        //public ChatFilter(int id, string title, bool pm, bool secret, bool privateGroups, bool publicGroups, bool channel, bool bot, bool excludeMuted, bool excludeRead)
        //{

        //}

        public ChatListFilterFlags ToFlags()
        {
            var filter = this;
            var flags = default(ChatListFilterFlags);

            if (filter.IncludeContacts)
            {
                flags |= ChatListFilterFlags.IncludeContacts;
            }
            if (filter.IncludeNonContacts)
            {
                flags |= ChatListFilterFlags.IncludeNonContacts;
            }
            if (filter.IncludeGroups)
            {
                flags |= ChatListFilterFlags.IncludeGroups;
            }
            if (filter.IncludeChannels)
            {
                flags |= ChatListFilterFlags.IncludeChannels;
            }
            if (filter.IncludeBots)
            {
                flags |= ChatListFilterFlags.IncludeBots;
            }
            if (filter.ExcludeMuted)
            {
                flags |= ChatListFilterFlags.ExcludeMuted;
            }
            if (filter.ExcludeRead)
            {
                flags |= ChatListFilterFlags.ExcludeRead;
            }
            //if (filter.IncludeChats != null && filter.IncludeChats.Count > 0)
            //{
            //    flags |= ChatListFilterFlags.IncludeChats;
            //}

            return flags;
        }

        public bool IncludeAll()
        {
            return IncludeBots && IncludeChannels && IncludeContacts && IncludeGroups && IncludeNonContacts;
        }
    }

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
