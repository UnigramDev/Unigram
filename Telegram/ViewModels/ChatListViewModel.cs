//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.Views.Folders;
using Telegram.Views.Popups;
using Windows.UI.Xaml.Controls;

namespace Telegram.ViewModels
{
    public partial class ChatListViewModel : ViewModelBase, IDelegable<IChatListDelegate>
    {
        private readonly INotificationsService _notificationsService;

        public IChatListDelegate Delegate { get; set; }

        public ChatListViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, INotificationsService notificationsService, ChatList chatList)
            : base(clientService, settingsService, aggregator)
        {
            _notificationsService = notificationsService;

            Items = new ItemsCollection(clientService, aggregator, this, chatList);

            SelectedItems = new RangeObservableCollection<Chat>();
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

        public RangeObservableCollection<Chat> SelectedItems { get; }

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
            if (chat.UnreadCount > 0 || chat.UnreadMentionCount > 0 || chat.UnreadReactionCount > 0)
            {
                if (chat.UnreadCount > 0 && chat.LastMessage != null)
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

        public void SetChatSound(Tuple<Chat, bool> value)
        {
            var chat = value.Item1;
            if (chat == null)
            {
                return;
            }

            _notificationsService.SetSound(chat, value.Item2, XamlRoot);
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

                string title;
                if (chat.Type is ChatTypeSupergroup super)
                {
                    title = super.IsChannel ? Strings.ChannelDeletedUndo : Strings.GroupDeletedUndo;
                }
                else
                {
                    title = chat.Type is ChatTypeBasicGroup ? Strings.GroupDeletedUndo : Strings.ChatDeletedUndo;
                }

                DeleteChatService.AddPending(XamlRoot, ClientService, new[] { chat.Id }, title, true, check, check);
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
                DeleteChatService.AddPending(XamlRoot, ClientService, chats.Select(x => x.Id).ToArray(), Strings.ChatDeletedUndo, true, false, false);
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
                DeleteChatService.AddPending(XamlRoot, ClientService, new[] { chat.Id }, Strings.HistoryClearedUndo, false, dialog.IsChecked, false);
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
                DeleteChatService.AddPending(XamlRoot, ClientService, chats.Select(x => x.Id).ToArray(), Strings.HistoryClearedUndo, false, false, false);
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

            folder.ExcludedChatIds = folder.ExcludedChatIds.Without(data.Chat.Id);
            folder.IncludedChatIds = folder.IncludedChatIds.With(data.Chat.Id);

            ClientService.Send(new EditChatFolder(data.ChatFolderId, folder));

            // TODO: use FormattedTextBlock in Toasts
            ShowToast(string.Format(data.Chat.Type is ChatTypePrivate or ChatTypeSecret
                ? Strings.FilterUserAddedToExisting
                : Strings.FilterChatAddedToExisting, data.Chat.Title, folder.Name.Text.Text), ToastPopupIcon.FolderIn);
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
                folder.IncludedChatIds = folder.IncludedChatIds.Without(data.Chat.Id);
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

                folder.IncludedChatIds = folder.IncludedChatIds.Without(data.Chat.Id);
                folder.ExcludedChatIds = folder.ExcludedChatIds.With(data.Chat.Id);
            }

            if (folder.Empty())
            {
                // TODO: Warn user about chat being already in the folder?
                return;
            }

            ClientService.Send(new EditChatFolder(data.ChatFolderId, folder));

            // TODO: use FormattedTextBlock in Toasts
            ShowToast(string.Format(data.Chat.Type is ChatTypePrivate or ChatTypeSecret
                ? Strings.FilterUserRemovedFrom
                : Strings.FilterChatRemovedFrom, data.Chat.Title, folder.Name.Text.Text), ToastPopupIcon.FolderOut);
        }

        #endregion

        #region Folder create

        public void CreateFolder(Chat chat)
        {
            NavigationService.Navigate(typeof(FolderPage), new FolderPageCreateArgs(chat.Id));
        }

        #endregion

        public void SetChatList(ChatList chatList)
        {
            Items.Restart(chatList);
        }

        public partial class ItemsCollection : WindowedCollection<Chat, long, long, OrderChangedEventArgs<Chat>>
        {
            private readonly IClientService _clientService;
            private readonly IEventAggregator _aggregator;

            private CancellationTokenSource _token = new();

            private readonly ChatListViewModel _viewModel;

            private ChatList _chatList;

            // The list this is a window over. Ordering comes from it rather than from the raw
            // updates, so the order a chat is placed with is the one the model decided under
            // its lock, not one read back from the chat afterwards.
            private ChatListService _service;

            public ChatList ChatList => _chatList;

            public ItemsCollection(IClientService clientService, IEventAggregator aggregator, ChatListViewModel viewModel, ChatList chatList)
                : base(viewModel.BeginOnUIThread)
            {
                _clientService = clientService;
                _aggregator = aggregator;

                _viewModel = viewModel;

                Attach(chatList);

                _ = LoadMoreItemsAsync(0);
            }

            public void Restart(ChatList chatList)
            {
                _token?.Cancel();
                _token = new CancellationTokenSource();

                Detach();
                Attach(chatList);


                // Re-arms, and abandons the load in flight so the one below is not coalesced
                // into a load that was paging the list being replaced.
                Restart();

                _ = LoadMoreItemsAsync(0);
            }

            protected override async Task<IncrementalLoadResult> OnLoadMoreItemsAsync(uint count)
            {
                var token = _token;
                var totalCount = 0u;

                // The constructor starts the first load, so that the chat list is on its way
                // before anything else at startup; yielding keeps its body out of there.
                await Task.Yield();

                var response = await _service.GetChatsAsync(Count, 20);
                if (response is Telegram.Td.Api.Chats chats && !token.IsCancellationRequested)
                {
                    foreach (var chat in _clientService.GetChats(chats.ChatIds))
                    {
                        // The order the list holds, not the one on the chat: an update can
                        // have moved it since the page was decided.
                        var order = _service.GetOrder(chat.Id);
                        if (order == 0)
                        {
                            continue;
                        }

                        // An update can have inserted it already while the page was in
                        // flight, and can have moved it since: place it where it belongs.
                        var next = NextIndexOf(chat, chat.Id, order, out int prev);
                        if (next == prev)
                        {
                            continue;
                        }

                        if (prev >= 0)
                        {
                            RemoveAt(prev);
                        }
                        else
                        {
                            totalCount++;
                        }

                        SetOrder(chat.Id, order);
                        Insert(Math.Min(Count, next), chat);

                        if (chat.Id == _viewModel.SelectedItem)
                        {
                            _viewModel.Delegate?.SetSelectedItem(chat);
                        }
                    }

                    IsEmpty = Count == 0;

                    // Before Subscribe, so the first update drains against a settled window.
                    UpdateWindow(chats.TotalCount >= 0);

                    Subscribe();

                    _viewModel.Delegate?.SetSelectedItems(_viewModel.SelectedItems);

                    // The cache wrapper reuses Chats and answers -1 in TotalCount once it holds
                    // the whole list, so this is the has-more test rather than a count.
                    return new IncrementalLoadResult(totalCount, chats.TotalCount >= 0);
                }

                // Cancelled, or an error: a reload is already on its way, and the version it
                // bumped discards whatever this returns.
                return default;
            }

            private void Attach(ChatList chatList)
            {
                _chatList = chatList;
                _service = _clientService.GetChatList(chatList);
            }

            private void Subscribe()
            {
                // Idempotent: every load calls it, and the first one is the one that takes.
                _service.Changed -= OnChanged;
                _service.Changed += OnChanged;

                _aggregator.Subscribe<UpdateAuthorizationState>(this, Handle);
            }

            private void Detach()
            {
                _service.Changed -= OnChanged;

                _aggregator.Unsubscribe(this);
            }

            private void OnChanged(OrderedSourceService<Chat> sender, OrderChangedEventArgs<Chat> args)
            {
                Enqueue(args);
            }

            protected override long GetKey(Chat item)
            {
                return item.Id;
            }

            protected override Chat GetItem(OrderChangedEventArgs<Chat> args)
            {
                return args.Item;
            }

            protected override long GetOrder(OrderChangedEventArgs<Chat> args)
            {
                return args.Order;
            }

            protected override bool IsPlaced(long order)
            {
                return order != 0;
            }

            protected override int Compare(long order, long chatId, long otherOrder, long otherChatId)
            {
                if (order != otherOrder)
                {
                    return order > otherOrder ? 1 : -1;
                }

                return chatId.CompareTo(otherChatId);
            }

            public override void Dispose()
            {
                base.Dispose();
                Detach();
            }

            #region Handle

            public void Handle(UpdateAuthorizationState update)
            {
                if (update.AuthorizationState is AuthorizationStateReady)
                {
                    _viewModel.BeginOnUIThread(() => Restart(_chatList));
                }
            }

            /// <summary>
            /// Places a chat the caller already knows the order of, from the dispatcher.
            /// </summary>
            public void ApplyOrder(long chatId, long order)
            {
                var chat = _clientService.GetChat(chatId);
                if (chat != null)
                {
                    Place(new OrderChangedEventArgs<Chat>(chat, order, false), chat, order, HasMoreItems);
                }
            }

            protected override void OnPlaced(OrderChangedEventArgs<Chat> args, int previousIndex, int index)
            {
                RaiseMoved(previousIndex, index);

                if (args.Item.Id == _viewModel.SelectedItem)
                {
                    _viewModel.Delegate?.SetSelectedItem(args.Item);
                }

                if (_viewModel.SelectedItems.Contains(args.Item))
                {
                    _viewModel.Delegate?.SetSelectedItems(_viewModel.SelectedItems);
                }

                IsEmpty = Count == 0;
            }

            protected override void OnRemoved(OrderChangedEventArgs<Chat> args, int index)
            {
                RaiseMoved(index, -1);

                if (_viewModel.SelectedItems.Contains(args.Item))
                {
                    _viewModel.SelectedItems.Remove(args.Item);
                    _viewModel.Delegate?.SetSelectedItems(_viewModel.SelectedItems);
                }

                IsEmpty = Count == 0;
            }

            // The newer order wins, but a last message anywhere in the batch still has to
            // redraw the row: the update that placed it last may have carried none.
            protected override OrderChangedEventArgs<Chat> Merge(OrderChangedEventArgs<Chat> previous, OrderChangedEventArgs<Chat> next)
            {
                return next.LastMessage || !previous.LastMessage
                    ? next
                    : new OrderChangedEventArgs<Chat>(next.Item, next.Order, true);
            }

            protected override void OnUnchanged(OrderChangedEventArgs<Chat> args, int index)
            {
                if (args.LastMessage)
                {
                    _viewModel.Delegate?.UpdateChatLastMessage(args.Item);
                }
            }

            // Reused rather than allocated per move: this fires on every reorder, so a handler
            // must read it and not retain it.
            private ChatListMovedEventArgs _moved;
            public event EventHandler<ChatListMovedEventArgs> Moved;

            private void RaiseMoved(int oldIndex, int newIndex)
            {
                _moved ??= new();
                _moved.OldIndex = oldIndex;
                _moved.NewIndex = newIndex;

                Moved?.Invoke(this, _moved);
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

    public class ChatListMovedEventArgs
    {
        public int OldIndex { get; set; }

        public int NewIndex { get; set; }
    }

    public enum SearchResultType
    {
        Recent,
        Chats,
        ChatsOnServer,
        Contacts,
        PublicChats,

        Ads,
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
            _canSendMessageToUser = canSendMessageToUser;

            Chat = chat;
            Query = query;
            Type = type;
        }

        public SearchResult(IClientService clientService, Chat chat, bool canSendMessageToUser)
        {
            _clientService = clientService;
            _canSendMessageToUser = canSendMessageToUser;

            Chat = chat;
            Query = string.Empty;
            Type = SearchResultType.None;
        }

        public SearchResult(IClientService clientService, User user, string query, SearchResultType type, bool canSendMessageToUser)
        {
            _clientService = clientService;
            _canSendMessageToUser = canSendMessageToUser;

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

        public async void CanSendMessageToUser()
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

            var response = await _clientService.SendAsync(new CanSendMessageToUser(userId.Value, false));
            if (response is CanSendMessageToUserResultUserRestrictsNewChats)
            {
                RestrictsNewChats = true;
            }
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
