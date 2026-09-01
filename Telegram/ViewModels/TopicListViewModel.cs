//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.Views.Supergroups.Popups;
using Windows.System;
using Windows.UI.Xaml.Controls;

namespace Telegram.ViewModels
{
    public partial class TopicListViewModel : ViewModelBase, IDelegable<ITopicListDelegate>
    {
        private readonly INotificationsService _notificationsService;

        private readonly bool _chatList;
        private readonly bool _forum;

        private readonly Dictionary<long, bool> _deletedChats = new();

        public ITopicListDelegate Delegate { get; set; }

        public bool IsForum => _forum;

        public TopicListViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, INotificationsService notificationsService, bool chatList, bool forum)
            : base(clientService, settingsService, aggregator)
        {
            _notificationsService = notificationsService ?? Session.Resolve<INotificationsService>();

            _chatList = chatList;
            _forum = forum;

            if (forum)
            {
                Items = new ForumTopicsCollection(clientService, aggregator, this, null);
            }
            else
            {
                Items = new DirectMessagesChatTopicsCollection(clientService, aggregator, this, null);
            }

            ChatsMarkCommand = new RelayCommand(ChatsMarkExecute);
            ChatsNotifyCommand = new RelayCommand(ChatsNotifyExecute);
            ChatsDeleteCommand = new RelayCommand(ChatsDeleteExecute);
            ChatsClearCommand = new RelayCommand(ChatsClearExecute);

            SelectedItems = new RangeObservableCollection<object>();
        }

        #region Selection

        public MessageTopic LastSelectedItem { get; private set; }

        private MessageTopic _selectedItem;
        public MessageTopic SelectedItem
        {
            get => _selectedItem;
            set
            {
                Set(ref _selectedItem, value);

                if (value != null)
                {
                    LastSelectedItem = value;
                }
            }
        }

        private RangeObservableCollection<object> _selectedItems;
        public RangeObservableCollection<object> SelectedItems
        {
            get => _selectedItems;
            set => Set(ref _selectedItems, value);
        }

        private ListViewSelectionMode _selectionMode = ListViewSelectionMode.None;
        public ListViewSelectionMode SelectionMode
        {
            get => _selectionMode;
            set => Set(ref _selectionMode, value);
        }

        #endregion

        public ITopicListCollection Items { get; private set; }

        public bool IsLastSliceLoaded { get; set; }

        #region Open

        public void OpenTopic(ForumTopic topic)
        {
            NavigationService.NavigateToChat(topic.Info.ChatId, topic: topic.ToId(), createNewWindow: true);
        }

        #endregion

        #region Pin

        public void HideTopic(ForumTopic topic)
        {
            if (Chat is Chat chat)
            {
                ClientService.Send(new ToggleGeneralForumTopicIsHidden(chat.Id, !topic.Info.IsHidden));
            }
        }

        #endregion

        #region Pin

        public async void PinTopic(ForumTopic topic)
        {
            //var position = chat.GetPosition(Items.ChatList);
            //if (position == null)
            //{
            //    return;
            //}
            var response = await ClientService.SendAsync(new ToggleForumTopicIsPinned(topic.Info.ChatId, topic.Info.ForumTopicId, !topic.IsPinned));
            if (response is Error error && error.Code == 400)
            {
                ShowPopup(string.Format(Strings.LimitReachedPinnedTopics, ClientService.Options.PinnedForumTopicCountMax), Strings.LimitReached, Strings.OK);
            }
        }

        #endregion

        #region Mark

        public void MarkTopicAsRead(ForumTopic topic)
        {
            if (topic.UnreadCount > 0)
            {
                if (topic.LastMessage != null)
                {
                    ClientService.ViewMessages(topic.Info.ChatId, topic.ToId(), new[] { topic.LastMessage.Id }, new MessageSourceForumTopicHistory(), true);
                }

                if (topic.UnreadMentionCount > 0)
                {
                    ClientService.Send(new ReadAllForumTopicMentions(topic.Info.ChatId, topic.Info.ForumTopicId));
                }

                if (topic.UnreadReactionCount > 0)
                {
                    ClientService.Send(new ReadAllForumTopicReactions(topic.Info.ChatId, topic.Info.ForumTopicId));
                }
            }
        }

        #endregion

        #region Multiple Mark

        public RelayCommand ChatsMarkCommand { get; }
        private void ChatsMarkExecute()
        {
            //var chats = SelectedItems.ToList();
            //var unread = chats.Any(x => x.IsUnread());
            //foreach (var chat in chats)
            //{
            //    if (unread)
            //    {
            //        if (chat.UnreadCount > 0 && chat.LastMessage != null)
            //        {
            //            ClientService.Send(new ViewMessages(chat.Id, 0, new[] { chat.LastMessage.Id }, true));
            //        }
            //        else if (chat.IsMarkedAsUnread)
            //        {
            //            ClientService.Send(new ToggleChatIsMarkedAsUnread(chat.Id, false));
            //        }

            //        if (chat.UnreadMentionCount > 0)
            //        {
            //            ClientService.Send(new ReadAllChatMentions(chat.Id));
            //        }
            //    }
            //    else if (chat.UnreadCount == 0 && !chat.IsMarkedAsUnread)
            //    {
            //        ClientService.Send(new ToggleChatIsMarkedAsUnread(chat.Id, true));
            //    }
            //}

            //Delegate?.SetSelectionMode(false);
            //SelectedItems.Clear();
        }

        #endregion

        #region Notify

        public void NotifyTopic(ForumTopic topic)
        {
            if (Chat is Chat chat)
            {
                _notificationsService.SetMuteFor(topic, ClientService.Notifications.GetMuteFor(chat, topic) > 0 ? 0 : 632053052, NavigationService.XamlRoot);
            }
        }

        #endregion

        #region Notify

        public void CloseTopic(ForumTopic topic)
        {
            if (Chat is Chat chat)
            {
                ClientService.Send(new ToggleForumTopicIsClosed(chat.Id, topic.Info.ForumTopicId, !topic.Info.IsClosed));
            }
        }

        #endregion

        #region Multiple Notify

        public RelayCommand ChatsNotifyCommand { get; }
        private void ChatsNotifyExecute()
        {
            //var chats = SelectedItems.ToList();
            //var muted = chats.Any(x => ClientService.Notifications.GetMutedFor(x) > 0);

            //foreach (var chat in chats)
            //{
            //    if (chat.Type is ChatTypePrivate privata && privata.UserId == ClientService.Options.MyId)
            //    {
            //        continue;
            //    }

            //    _notificationsService.SetMuteFor(chat, muted ? 0 : 632053052);
            //}

            //Delegate?.SetSelectionMode(false);
            //SelectedItems.Clear();
        }

        #endregion

        #region Delete

        public async void DeleteTopic(ForumTopic topic)
        {
            var message = string.Format(Strings.DeleteSelectedTopic, topic.Info.Name);
            var title = Locale.Declension(Strings.R.DeleteTopics, 1);

            var confirm = await ShowPopupAsync(message, title, Strings.Delete, Strings.Cancel, destructive: true);
            if (confirm == ContentDialogResult.Primary)
            {
                // TODO: Handle the case where topics can't be deleted because user isn't admin
                ClientService.Send(new DeleteForumTopic(Chat.Id, topic.Info.ForumTopicId));
            }
        }

        #endregion

        #region Multiple Delete

        public RelayCommand ChatsDeleteCommand { get; }
        private void ChatsDeleteExecute()
        {
            //var chats = SelectedItems.ToList();

            //var confirm = await ShowPopupAsync(Strings.AreYouSureDeleteFewChats, Locale.Declension("ChatsSelected", chats.Count), Strings.Delete, Strings.Cancel);
            //if (confirm == ContentDialogResult.Primary)
            //{
            //    foreach (var chat in chats)
            //    {
            //        _deletedChats[chat.Id] = true;
            //        Items.Handle(chat.Id, 0);
            //    }

            //    Delegate?.ShowChatsUndo(chats, UndoType.Delete, items =>
            //    {
            //        foreach (var undo in items)
            //        {
            //            _deletedChats.Remove(undo.Id);
            //            Items.Handle(undo.Id, undo.Positions);
            //        }
            //    }, async items =>
            //    {
            //        foreach (var delete in items)
            //        {
            //            if (delete.Type is ChatTypeSecret secret)
            //            {
            //                await ClientService.SendAsync(new CloseSecretChat(secret.SecretChatId));
            //            }
            //            else if (delete.Type is ChatTypeBasicGroup or ChatTypeSupergroup)
            //            {
            //                await ClientService.SendAsync(new LeaveChat(delete.Id));
            //            }

            //            ClientService.Send(new DeleteChatHistory(delete.Id, true, false));
            //        }
            //    });
            //}

            //Delegate?.SetSelectionMode(false);
            //SelectedItems.Clear();
        }

        #endregion

        #region Clear

        public void ClearTopic(ForumTopic chat)
        {
            //var updated = await ClientService.SendAsync(new GetChat(chat.Id)) as Chat ?? chat;
            //var dialog = new DeleteChatPopup(ClientService, updated, Items.ChatList, true);

            //var confirm = await ShowPopupAsync(dialog);
            //if (confirm == ContentDialogResult.Primary)
            //{
            //    Delegate?.ShowChatsUndo(new[] { chat }, UndoType.Clear, items =>
            //    {
            //        var undo = items.FirstOrDefault();
            //        if (undo == null)
            //        {
            //            return;
            //        }

            //        _deletedChats.Remove(undo.Id);
            //        Items.Handle(undo.Id, undo.Positions);
            //    }, items =>
            //    {
            //        foreach (var delete in items)
            //        {
            //            ClientService.Send(new DeleteChatHistory(delete.Id, false, dialog.IsChecked));
            //        }
            //    });
            //}
        }

        public async void ClearTopic(DirectMessagesChatTopic topic)
        {
            var message = string.Format(Strings.AreYouSureClearHistoryWithUser, ClientService.GetTitle(topic.SenderId));
            var title = Strings.ClearHistory;

            var confirm = await ShowPopupAsync(message, title, Strings.Delete, Strings.Cancel, destructive: true);
            if (confirm == ContentDialogResult.Primary)
            {
                ClientService.Send(new DeleteDirectMessagesChatTopicHistory(ChatId, topic.Id));
            }
        }

        #endregion

        #region Multiple Clear

        public RelayCommand ChatsClearCommand { get; }
        private void ChatsClearExecute()
        {
            //var chats = SelectedItems.ToList();

            //var confirm = await ShowPopupAsync(Strings.AreYouSureClearHistoryFewChats, Locale.Declension("ChatsSelected", chats.Count), Strings.ClearHistory, Strings.Cancel);
            //if (confirm == ContentDialogResult.Primary)
            //{
            //    Delegate?.ShowChatsUndo(chats, UndoType.Clear, items =>
            //    {
            //        foreach (var undo in items)
            //        {
            //            _deletedChats.Remove(undo.Id);
            //            Items.Handle(undo.Id, undo.Positions);
            //        }
            //    }, items =>
            //    {
            //        var clear = items.FirstOrDefault();
            //        if (clear == null)
            //        {
            //            return;
            //        }

            //        ClientService.Send(new DeleteChatHistory(clear.Id, false, false));
            //    });
            //}

            //Delegate?.SetSelectionMode(false);
            //SelectedItems.Clear();
        }

        #endregion

        #region Select

        public void SelectTopic(ForumTopic chat)
        {
            //SelectedItems.ReplaceWith(new[] { chat });
            //SelectionMode = ListViewSelectionMode.Multiple;

            //Delegate?.SetSelectedItems(_selectedItems);
        }

        #endregion

        public Chat Chat => Items.Chat;

        public long ChatId => Items.Chat?.Id ?? 0;

        public void SetChat(Chat chat)
        {
            if (chat?.Id != Items.Chat?.Id)
            {
                Items.Restart(chat);

                LastSelectedItem = null;

                SelectedItem = null;
                SelectedItems.Clear();

                if (_forum)
                {
                    Aggregator.Subscribe<UpdateForumTopicInfo>(this, Handle)
                        .Subscribe<UpdateForumTopicReadInbox>(Handle)
                        .Subscribe<UpdateForumTopicReadOutbox>(Handle)
                        .Subscribe<UpdateForumTopicUnreadMentionCount>(Handle)
                        .Subscribe<UpdateForumTopicUnreadReactionCount>(Handle)
                        .Subscribe<UpdateForumTopicNotificationSettings>(Handle)
                        .Subscribe<UpdateChatAction>(Handle);
                }
                else
                {
                    Aggregator.Subscribe<UpdateDirectMessagesChatTopicReadInbox>(this, Handle)
                        .Subscribe<UpdateDirectMessagesChatTopicReadOutbox>(Handle)
                        .Subscribe<UpdateDirectMessagesChatTopicUnreadMentionCount>(Handle)
                        .Subscribe<UpdateDirectMessagesChatTopicUnreadReactionCount>(Handle);
                }
            }
            else if (chat == null)
            {
                LastSelectedItem = null;

                SelectedItem = null;
                SelectedItems.Clear();

                Aggregator.Unsubscribe(this);
            }
        }

        #region ForumTopic

        private void Handle(UpdateChatAction update)
        {
            if (update.ChatId == Chat?.Id && update.TopicId is MessageTopicForum topicForum)
            {
                BeginOnUIThread(() => Delegate?.HandleForumTopic(topicForum.ForumTopicId, (cell, topic) => cell.UpdateForumTopicActions(topic, ClientService.GetChatActions(update.ChatId, update.TopicId))));
            }
        }

        private void Handle(UpdateForumTopicInfo update)
        {
            if (update.Info.ChatId == Chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.HandleForumTopic(update.Info.ForumTopicId, (cell, topic) => cell.UpdateForumTopicInfo(topic)));
            }
        }

        private void Handle(UpdateForumTopicReadInbox update)
        {
            if (update.ChatId == Chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.HandleForumTopic(update.ForumTopicId, (cell, topic) => cell.UpdateForumTopicReadInbox(topic)));
            }
        }

        private void Handle(UpdateForumTopicReadOutbox update)
        {
            if (update.ChatId == Chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.HandleForumTopic(update.ForumTopicId, (cell, topic) => cell.UpdateForumTopicReadOutbox(topic)));
            }
        }

        private void Handle(UpdateForumTopicUnreadMentionCount update)
        {
            if (update.ChatId == Chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.HandleForumTopic(update.ForumTopicId, (cell, topic) => cell.UpdateForumTopicUnreadMentionCount(topic)));
            }
        }

        private void Handle(UpdateForumTopicUnreadReactionCount update)
        {
            if (update.ChatId == Chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.HandleForumTopic(update.ForumTopicId, (cell, topic) => cell.UpdateForumTopicUnreadMentionCount(topic)));
            }
        }

        public void Handle(UpdateForumTopicNotificationSettings update)
        {
            if (update.ChatId == Chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.HandleForumTopic(update.ForumTopicId, (cell, topic) => cell.UpdateForumTopicNotificationSettings(topic)));
            }
        }

        #endregion

        #region ForumTopic

        private void Handle(UpdateDirectMessagesChatTopicReadInbox update)
        {
            if (update.ChatId == Chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.HandleDirectMessagesChatTopic(update.TopicId, (cell, topic) => cell.UpdateDirectMessagesChatTopicReadInbox(topic)));
            }
        }

        private void Handle(UpdateDirectMessagesChatTopicReadOutbox update)
        {
            if (update.ChatId == Chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.HandleDirectMessagesChatTopic(update.TopicId, (cell, topic) => cell.UpdateDirectMessagesChatTopicReadOutbox(topic)));
            }
        }

        private void Handle(UpdateDirectMessagesChatTopicUnreadMentionCount update)
        {
            if (update.ChatId == Chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.HandleDirectMessagesChatTopic(update.TopicId, (cell, topic) => cell.UpdateDirectMessagesChatTopicUnreadMentionCount(topic)));
            }
        }

        private void Handle(UpdateDirectMessagesChatTopicUnreadReactionCount update)
        {
            if (update.ChatId == Chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.HandleDirectMessagesChatTopic(update.TopicId, (cell, topic) => cell.UpdateDirectMessagesChatTopicUnreadMentionCount(topic)));
            }
        }

        #endregion

        public async void ViewAsMessages()
        {
            if (Chat is not Chat chat)
            {
                return;
            }

            await ClientService.SendAsync(new ToggleChatViewAsTopics(chat.Id, false));
            NavigationService.NavigateToChat(chat, force: false, clearBackStack: true);
        }

        public async void CreateTopic()
        {
            if (Chat is not Chat chat)
            {
                return;
            }

            var popup = new SupergroupTopicPopup(ClientService, null);

            var confirm = await ShowPopupAsync(popup);
            if (confirm == ContentDialogResult.Primary)
            {
                var response = await ClientService.SendAsync(new CreateForumTopic(chat.Id, popup.SelectedName, false, popup.SelectedIcon));
                if (response is ForumTopicInfo info)
                {
                    NavigationService.NavigateToChat(chat, topic: new MessageTopicForum(info.ForumTopicId), force: false, clearBackStack: true);
                }
            }
        }

        private static Action<DispatcherQueueHandler> Post(TopicListViewModel viewModel)
        {
            if (viewModel != null)
            {
                return viewModel.BeginOnUIThread;
            }

            // The chat picker builds a collection with no view model behind it: post to the
            // thread that created it, which is the view the collection is bound to.
            var dispatcher = DispatcherContext.Current;
            if (dispatcher != null)
            {
                return handler => dispatcher.Dispatch(handler);
            }

            return handler => handler();
        }

        public interface ITopicListCollection : IList, ICollectionWithTotalCount
        {
            Chat Chat { get; }

            void Restart(Chat chat);

            object GetItem(MessageTopic topic);
        }

        public partial class ForumTopicsCollection : WindowedCollection<ForumTopic, int, long, OrderChangedEventArgs<ForumTopic>>, ITopicListCollection
        {
            private readonly IClientService _clientService;
            private readonly IEventAggregator _aggregator;

            private CancellationTokenSource _token = new();

            private readonly TopicListViewModel _viewModel;

            private Chat _chat;

            // The list this is a window over. Ordering comes from it rather than from the raw
            // updates, so the order a topic is placed with is the one the model decided under
            // its lock, not one read back from the topic afterwards.
            private ForumTopicService _service;

            public Chat Chat => _chat;

            public ForumTopicsCollection(IClientService clientService, IEventAggregator aggregator, TopicListViewModel viewModel, Chat chat)
                : base(Post(viewModel))
            {
                _clientService = clientService;
                _aggregator = aggregator;

                _viewModel = viewModel;
                _chat = chat;

                Attach();
            }

            private void Attach()
            {
                if (_chat != null)
                {
                    _service = _clientService.GetForumTopicList(_chat.Id);
                }
            }

            private void Detach()
            {
                if (_service != null)
                {
                    _service.Changed -= OnChanged;
                    _service = null;
                }
            }

            public override void Dispose()
            {
                base.Dispose();

                Detach();
                _aggregator.Unsubscribe(this);
            }

            public void Restart(Chat chat)
            {
                if (_chat != null)
                {
                    _clientService.Send(new CloseChat(_chat.Id));
                }

                _token?.Cancel();
                _token = new CancellationTokenSource();

                _aggregator.Unsubscribe(this);
                Detach();

                _chat = chat;
                Attach();

                // Re-arms, and abandons the load in flight - and whatever the drain collected
                // for the chat being replaced - so the one below starts from nothing.
                Restart();

                if (_chat != null)
                {
                    _clientService.Send(new OpenChat(chat.Id));
                    _ = LoadMoreItemsAsync(0);
                }
            }

            protected override async Task<IncrementalLoadResult> OnLoadMoreItemsAsync(uint count)
            {
                Logger.Info(Count);

                var token = _token;
                var totalCount = 0u;

                await Task.Yield();

                if (_service == null)
                {
                    return default;
                }

                var response = await _service.GetForumTopicsAsync(Count, 20);
                if (response is ForumTopics2 topics && !token.IsCancellationRequested)
                {
                    if (_viewModel != null && !_viewModel._chatList && Count == 0)
                    {
                        topics.TopicIds = new List<int>(topics.TopicIds);
                        topics.TopicIds.Insert(0, int.MaxValue);
                    }

                    foreach (var topic in _clientService.GetForumTopics(_chat.Id, topics.TopicIds))
                    {
                        var order = topic.Order;
                        if (order == 0)
                        {
                            continue;
                        }

                        // An update can have inserted it already while the page was in
                        // flight, and can have moved it since: place it where it belongs.
                        var next = NextIndexOf(topic, topic.Info.ForumTopicId, order, out int prev);
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

                        SetOrder(topic.Info.ForumTopicId, order);
                        Insert(Math.Min(Count, next), topic);

                        if ((_viewModel?.SelectedItem == null && topic.Info.ForumTopicId == 0) || _viewModel?.SelectedItem?.IsForum(topic.Info.ForumTopicId) is true)
                        {
                            _viewModel?.Delegate?.SetSelectedItem(topic);
                        }
                    }

                    Logger.Info(string.Format("Received {0} items, added {1}", topics.TopicIds.Count, totalCount));

                    IsEmpty = Count == 0;

                    // Before Subscribe, so the first update drains against a settled window.
                    UpdateWindow(topics.TotalCount >= 0);

                    Subscribe();

                    _viewModel?.Delegate?.SetSelectedItems(_viewModel.SelectedItems);

                    // The cache wrapper reuses ForumTopics2 and answers -1 in TotalCount once
                    // it holds the whole list, so this is the has-more test, not a count.
                    return new IncrementalLoadResult(totalCount, topics.TotalCount >= 0);
                }

                // Cancelled, or an error: a reload is already on its way, and the version it
                // bumped discards whatever this returns.
                return default;
            }

            // Called by every page, and the first one is the one that takes. Nothing is
            // watched until then: with no page in, an update for a topic sorting far down the
            // list would become the whole window - which is the offset the next page asks for.
            private void Subscribe()
            {
                _service.Changed -= OnChanged;
                _service.Changed += OnChanged;

                _aggregator.Subscribe<UpdateAuthorizationState>(this, Handle);
            }

            #region Handle

            public void Handle(UpdateAuthorizationState update)
            {
                if (update.AuthorizationState is AuthorizationStateReady)
                {
                    _viewModel?.BeginOnUIThread(() => Restart(_chat));
                }
            }

            private void OnChanged(OrderedSourceService<ForumTopic> sender, OrderChangedEventArgs<ForumTopic> args)
            {
                Enqueue(args);
            }

            /// <summary>
            /// Places a topic at an order the list already knows, for a caller that has just
            /// changed it locally rather than through an update.
            /// </summary>
            public void ApplyOrder(int forumTopicId, long order, bool lastMessage = false)
            {
                var topic = GetTopic(forumTopicId);
                if (topic != null)
                {
                    Enqueue(new OrderChangedEventArgs<ForumTopic>(topic, order, lastMessage));
                }
            }

            protected override int GetKey(ForumTopic item)
            {
                return item.Info.ForumTopicId;
            }

            protected override ForumTopic GetItem(OrderChangedEventArgs<ForumTopic> args)
            {
                return args.Item;
            }

            protected override long GetOrder(OrderChangedEventArgs<ForumTopic> args)
            {
                return args.Order;
            }

            protected override bool IsPlaced(long order)
            {
                return order != 0;
            }

            protected override int Compare(long order, int forumTopicId, long otherOrder, int otherForumTopicId)
            {
                if (order != otherOrder)
                {
                    return order > otherOrder ? 1 : -1;
                }

                return forumTopicId.CompareTo(otherForumTopicId);
            }

            // The newer order wins, but a last message anywhere in the batch still has to
            // redraw the row: the update that placed it last may have carried none.
            protected override OrderChangedEventArgs<ForumTopic> Merge(OrderChangedEventArgs<ForumTopic> previous, OrderChangedEventArgs<ForumTopic> next)
            {
                return next.LastMessage || !previous.LastMessage
                    ? next
                    : new OrderChangedEventArgs<ForumTopic>(next.Item, next.Order, true);
            }

            protected override void OnPlaced(OrderChangedEventArgs<ForumTopic> args, int previousIndex, int index)
            {
                if (_viewModel?.SelectedItem?.IsForum(args.Item.Info.ForumTopicId) is true)
                {
                    _viewModel.Delegate?.SetSelectedItem(args.Item);
                }

                if (_viewModel?.SelectedItems.Contains(args.Item) is true)
                {
                    _viewModel.Delegate?.SetSelectedItems(_viewModel.SelectedItems);
                }

                IsEmpty = Count == 0;
            }

            protected override void OnRemoved(OrderChangedEventArgs<ForumTopic> args, int index)
            {
                if (_viewModel?.SelectedItems.Contains(args.Item) is true)
                {
                    _viewModel.SelectedItems.Remove(args.Item);
                    _viewModel.Delegate?.SetSelectedItems(_viewModel.SelectedItems);
                }

                IsEmpty = Count == 0;
            }

            protected override void OnUnchanged(OrderChangedEventArgs<ForumTopic> args, int index)
            {
                if (args.LastMessage)
                {
                    _viewModel?.Delegate?.UpdateForumTopicLastMessage(args.Item);
                }
            }

            public ForumTopic GetTopic(int forumTopicId)
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

                if (forumTopicId == 0 && _viewModel != null && !_viewModel._chatList && Items.Count > 0)
                {
                    return Items[0];
                }

                return _clientService.GetForumTopic(_chat.Id, forumTopicId);
            }

            public object GetItem(MessageTopic topic)
            {
                if (topic == null && _viewModel != null && !_viewModel._chatList && Items.Count > 0)
                {
                    return Items[0];
                }

                if (topic is MessageTopicForum forum && ContainsKey(forum.ForumTopicId))
                {
                    return _clientService.GetForumTopic(_chat.Id, forum.ForumTopicId);
                }

                return null;
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
                        _viewModel?.Dispatcher?.Dispatch(NotifyChanged, Windows.System.DispatcherQueuePriority.Low);
                    }
                }
            }

            private void NotifyChanged()
            {
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsEmpty)));
            }
        }

        public partial class DirectMessagesChatTopicsCollection : WindowedCollection<DirectMessagesChatTopic, long, long, OrderChangedEventArgs<DirectMessagesChatTopic>>, ITopicListCollection
        {
            private readonly IClientService _clientService;
            private readonly IEventAggregator _aggregator;

            private CancellationTokenSource _token = new();

            private readonly TopicListViewModel _viewModel;

            private Chat _chat;

            // The list this is a window over. Ordering comes from it rather than from the raw
            // updates, so the order a topic is placed with is the one the model decided under
            // its lock, not one read back from the topic afterwards.
            private DirectMessagesChatTopicService _service;

            public Chat Chat => _chat;

            public DirectMessagesChatTopicsCollection(IClientService clientService, IEventAggregator aggregator, TopicListViewModel viewModel, Chat chat)
                : base(Post(viewModel))
            {
                _clientService = clientService;
                _aggregator = aggregator;

                _viewModel = viewModel;
                _chat = chat;

                Attach();

                _ = LoadMoreItemsAsync(0);
            }

            private void Attach()
            {
                if (_chat != null)
                {
                    _service = _clientService.GetDirectMessagesChatTopicList(_chat.Id);
                }
            }

            private void Detach()
            {
                if (_service != null)
                {
                    _service.Changed -= OnChanged;
                    _service = null;
                }
            }

            public override void Dispose()
            {
                base.Dispose();

                Detach();
                _aggregator.Unsubscribe(this);
            }

            public void Restart(Chat chat)
            {
                if (_chat != null)
                {
                    _clientService.Send(new CloseChat(_chat.Id));
                }

                _token?.Cancel();
                _token = new CancellationTokenSource();

                _aggregator.Unsubscribe(this);
                Detach();

                _chat = chat;
                Attach();

                // Re-arms, and abandons the load in flight - and whatever the drain collected
                // for the chat being replaced - so the one below starts from nothing.
                Restart();

                if (_chat != null)
                {
                    _clientService.Send(new OpenChat(chat.Id));
                    _ = LoadMoreItemsAsync(0);
                }
            }

            protected override async Task<IncrementalLoadResult> OnLoadMoreItemsAsync(uint count)
            {
                Logger.Info(Count);

                var token = _token;
                var totalCount = 0u;

                await Task.Yield();

                if (_service == null)
                {
                    return default;
                }

                var response = await _service.GetDirectMessagesChatTopicsAsync(Count, 20);
                if (response is Topics topics && !token.IsCancellationRequested)
                {
                    if (_viewModel != null && !_viewModel._chatList && Count == 0)
                    {
                        topics.TopicIds = new List<long>(topics.TopicIds);
                        topics.TopicIds.Insert(0, long.MaxValue);
                    }

                    foreach (var topic in _clientService.GetDirectMessagesChatTopics(_chat.Id, topics.TopicIds))
                    {
                        var order = topic.Order;
                        if (order == 0)
                        {
                            continue;
                        }

                        // An update can have inserted it already while the page was in
                        // flight, and can have moved it since: place it where it belongs.
                        var next = NextIndexOf(topic, topic.Id, order, out int prev);
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

                        SetOrder(topic.Id, order);
                        Insert(Math.Min(Count, next), topic);

                        if ((_viewModel?.SelectedItem == null && topic.Id == 0) || _viewModel?.SelectedItem?.IsDirectMessagesChat(topic.Id) is true)
                        {
                            _viewModel?.Delegate?.SetSelectedItem(topic);
                        }
                    }

                    Logger.Info(string.Format("Received {0} items, added {1}", topics.TopicIds.Count, totalCount));

                    IsEmpty = Count == 0;

                    // Before Subscribe, so the first update drains against a settled window.
                    UpdateWindow(topics.TotalCount >= 0);

                    Subscribe();

                    _viewModel?.Delegate?.SetSelectedItems(_viewModel.SelectedItems);

                    // The cache wrapper reuses Topics and answers -1 in TotalCount once it
                    // holds the whole list, so this is the has-more test, not a count.
                    return new IncrementalLoadResult(totalCount, topics.TotalCount >= 0);
                }

                // Cancelled, or an error: a reload is already on its way, and the version it
                // bumped discards whatever this returns.
                return default;
            }

            // Called by every page, and the first one is the one that takes. Nothing is
            // watched until then: with no page in, an update for a topic sorting far down the
            // list would become the whole window - which is the offset the next page asks for.
            private void Subscribe()
            {
                _service.Changed -= OnChanged;
                _service.Changed += OnChanged;

                _aggregator.Subscribe<UpdateAuthorizationState>(this, Handle);
            }

            #region Handle

            public void Handle(UpdateAuthorizationState update)
            {
                if (update.AuthorizationState is AuthorizationStateReady)
                {
                    _viewModel?.BeginOnUIThread(() => Restart(_chat));
                }
            }

            private void OnChanged(OrderedSourceService<DirectMessagesChatTopic> sender, OrderChangedEventArgs<DirectMessagesChatTopic> args)
            {
                Enqueue(args);
            }

            protected override long GetKey(DirectMessagesChatTopic item)
            {
                return item.Id;
            }

            protected override DirectMessagesChatTopic GetItem(OrderChangedEventArgs<DirectMessagesChatTopic> args)
            {
                return args.Item;
            }

            protected override long GetOrder(OrderChangedEventArgs<DirectMessagesChatTopic> args)
            {
                return args.Order;
            }

            protected override bool IsPlaced(long order)
            {
                return order != 0;
            }

            protected override int Compare(long order, long topicId, long otherOrder, long otherTopicId)
            {
                if (order != otherOrder)
                {
                    return order > otherOrder ? 1 : -1;
                }

                return topicId.CompareTo(otherTopicId);
            }

            // The newer order wins, but a last message anywhere in the batch still has to
            // redraw the row: the update that placed it last may have carried none.
            protected override OrderChangedEventArgs<DirectMessagesChatTopic> Merge(OrderChangedEventArgs<DirectMessagesChatTopic> previous, OrderChangedEventArgs<DirectMessagesChatTopic> next)
            {
                return next.LastMessage || !previous.LastMessage
                    ? next
                    : new OrderChangedEventArgs<DirectMessagesChatTopic>(next.Item, next.Order, true);
            }

            protected override void OnPlaced(OrderChangedEventArgs<DirectMessagesChatTopic> args, int previousIndex, int index)
            {
                if (_viewModel?.SelectedItem?.IsDirectMessagesChat(args.Item.Id) is true)
                {
                    _viewModel.Delegate?.SetSelectedItem(args.Item);
                }

                if (_viewModel?.SelectedItems.Contains(args.Item) is true)
                {
                    _viewModel.Delegate?.SetSelectedItems(_viewModel.SelectedItems);
                }

                IsEmpty = Count == 0;
            }

            protected override void OnRemoved(OrderChangedEventArgs<DirectMessagesChatTopic> args, int index)
            {
                if (_viewModel?.SelectedItems.Contains(args.Item) is true)
                {
                    _viewModel.SelectedItems.Remove(args.Item);
                    _viewModel.Delegate?.SetSelectedItems(_viewModel.SelectedItems);
                }

                IsEmpty = Count == 0;
            }

            protected override void OnUnchanged(OrderChangedEventArgs<DirectMessagesChatTopic> args, int index)
            {
                if (args.LastMessage)
                {
                    _viewModel?.Delegate?.UpdateDirectMessagesChatTopicLastMessage(args.Item);
                }
            }

            public DirectMessagesChatTopic GetTopic(long messageThreadId)
            {
                if (messageThreadId == 0 && _viewModel != null && !_viewModel._chatList && Items.Count > 0)
                {
                    return Items[0];
                }

                return _clientService.GetDirectMessagesChatTopic(_chat.Id, messageThreadId);
            }

            public object GetItem(MessageTopic topic)
            {
                if (topic == null && _viewModel != null && !_viewModel._chatList && Items.Count > 0)
                {
                    return Items[0];
                }

                if (topic is MessageTopicDirectMessages directMessagesChat && ContainsKey(directMessagesChat.DirectMessagesChatTopicId))
                {
                    return _clientService.GetDirectMessagesChatTopic(_chat.Id, directMessagesChat.DirectMessagesChatTopicId);
                }

                return null;
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
                        _viewModel?.Dispatcher?.Dispatch(NotifyChanged, Windows.System.DispatcherQueuePriority.Low);
                    }
                }
            }

            private void NotifyChanged()
            {
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsEmpty)));
            }
        }
    }
}
