//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.Views.Supergroups.Popups;
using Windows.Foundation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

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

#if MOCKUP
            Items.AddRange(clientService.GetChats(null));
#endif

            SelectedItems = new MvxObservableCollection<object>();
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

        private MvxObservableCollection<object> _selectedItems;
        public MvxObservableCollection<object> SelectedItems
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
                _ = Items.ReloadAsync(chat);

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

        public interface ITopicListCollection : IList, ICollectionWithTotalCount
        {
            Chat Chat { get; }

            Task ReloadAsync(Chat chat);

            object GetItem(MessageTopic topic);
        }

        public partial class ForumTopicsCollection : ObservableCollection<ForumTopic>, ISupportIncrementalLoading, ITopicListCollection
        {
            private readonly IClientService _clientService;
            private readonly IEventAggregator _aggregator;

            private CancellationTokenSource _token = new();
            private readonly HashSet<int> _topics = new();

            private readonly TopicListViewModel _viewModel;

            private Chat _chat;

            private bool _hasMoreItems = true;

            private int _lastTopicId;
            private long _lastOrder;

            public Chat Chat => _chat;

            public ForumTopicsCollection(IClientService clientService, IEventAggregator aggregator, TopicListViewModel viewModel, Chat chat)
            {
                _clientService = clientService;
                _aggregator = aggregator;

                _viewModel = viewModel;
                _chat = chat;

#if MOCKUP
                _hasMoreItems = false;
#endif

                //_ = LoadMoreItemsAsync(0);
            }

            public Task ReloadAsync(Chat chat)
            {
                if (_chat != null)
                {
                    _clientService.Send(new CloseChat(_chat.Id));
                }

                _token?.Cancel();
                _token = new CancellationTokenSource();

                _aggregator.Unsubscribe(this);
                _hasMoreItems = false;

                _lastTopicId = 0;
                _lastOrder = 0;

                _chat = chat;

                _topics.Clear();
                Clear();

                if (_chat != null)
                {
                    _clientService.Send(new OpenChat(chat.Id));
                    return LoadMoreItemsAsync();
                }

                return Task.CompletedTask;
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(token => LoadMoreItemsAsync());
            }

            private async Task<LoadMoreItemsResult> LoadMoreItemsAsync()
            {
                Logger.Info(Count);

                var token = _token;
                var totalCount = 0u;

                await Task.Yield();

                if (_chat == null)
                {
                    _hasMoreItems = false;

                    return new LoadMoreItemsResult
                    {
                        Count = totalCount
                    };
                }

                var response = await _clientService.GetForumTopicsAsync(_chat.Id, Count, 20);
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
                        if (order != 0)
                        {
                            // TODO: is this redundant?
                            var next = NextIndexOf(topic, order);
                            if (next >= 0)
                            {
                                if (_topics.Contains(topic.Info.ForumTopicId))
                                {
                                    Remove(topic);
                                }

                                _topics.Add(topic.Info.ForumTopicId);
                                Insert(Math.Min(Count, next), topic);

                                if ((_viewModel?.SelectedItem == null && topic.Info.ForumTopicId == 0) || _viewModel?.SelectedItem?.IsForum(topic.Info.ForumTopicId) is true)
                                {
                                    _viewModel?.Delegate?.SetSelectedItem(topic);
                                }

                                totalCount++;
                            }

                            _lastTopicId = topic.Info.ForumTopicId;
                            _lastOrder = order;
                        }
                    }

                    Logger.Info(string.Format("Received {0} items, added {1}", topics.TopicIds.Count, totalCount));

                    IsEmpty = Count == 0;

                    _hasMoreItems = topics.TotalCount >= 0;
                    Subscribe();

                    _viewModel?.Delegate?.SetSelectedItems(_viewModel.SelectedItems);
                }

                return new LoadMoreItemsResult
                {
                    Count = totalCount
                };
            }

            private void Subscribe()
            {
                _aggregator.Subscribe<UpdateAuthorizationState>(this, Handle)
                    //.Subscribe<UpdateChatDraftMessage>(Handle)
                    .Subscribe<UpdateForumTopicLastMessage>(Handle)
                    .Subscribe<UpdateForumTopicPosition>(Handle);
            }

            public bool HasMoreItems => _hasMoreItems;

            #region Handle

            public void Handle(UpdateAuthorizationState update)
            {
                if (update.AuthorizationState is AuthorizationStateReady)
                {
                    _viewModel.BeginOnUIThread(() => _ = ReloadAsync(_chat));
                }
            }

            public void Handle(UpdateForumTopicPosition update)
            {
                if (update.ChatId == _chat.Id)
                {
                    Handle(update.ForumTopicId, update.Order);
                }
            }

            public void Handle(UpdateForumTopicLastMessage update)
            {
                if (update.ChatId == _chat.Id)
                {
                    Handle(update.ForumTopicId, update.Order, true);
                }
            }

            //public void Handle(UpdateChatDraftMessage update)
            //{
            //    Handle(update.ChatId, update.Positions, true);
            //}

            public void Handle(int forumTopicId, long order, bool lastMessage = false)
            {
                var topic = GetTopic(forumTopicId);

                Handle(topic, order, lastMessage);
            }

            public void Handle(int forumTopicId, long order)
            {
                var chat = GetTopic(forumTopicId);
                if (chat != null)
                {
                    Handle(chat, order, false);
                }
            }

            private void Handle(ForumTopic topic, long order, bool lastMessage)
            {
                //var chat = GetChat(chatId);
                if (topic != null /*&& _chatList.ListEquals(chat.ChatList)*/)
                {
                    _viewModel?.BeginOnUIThread(() => UpdateForumTopicOrder(topic, order, lastMessage));
                }
            }

            private void UpdateForumTopicOrder(ForumTopic topic, long order, bool lastMessage)
            {
                if (order > 0 && (order > _lastOrder || (order == _lastOrder && topic.Info.ForumTopicId >= _lastTopicId)))
                {
                    var next = NextIndexOf(topic, order);
                    if (next >= 0)
                    {
                        if (_topics.Contains(topic.Info.ForumTopicId))
                        {
                            Remove(topic);
                        }
                        else
                        {
                            _topics.Add(topic.Info.ForumTopicId);
                        }

                        Insert(Math.Min(Count, next), topic);

                        if (next == Count - 1)
                        {
                            _lastTopicId = topic.Info.ForumTopicId;
                            _lastOrder = order;
                        }

                        if (_viewModel.SelectedItem.IsForum(topic.Info.ForumTopicId))
                        {
                            _viewModel.Delegate?.SetSelectedItem(topic);
                        }
                        if (_viewModel.SelectedItems.Contains(topic))
                        {
                            _viewModel.Delegate?.SetSelectedItems(_viewModel.SelectedItems);
                        }

                        IsEmpty = Count == 0;
                    }
                    else if (lastMessage)
                    {
                        _viewModel.Delegate?.UpdateForumTopicLastMessage(topic);
                    }
                }
                else if (_topics.Contains(topic.Info.ForumTopicId))
                {
                    _topics.Remove(topic.Info.ForumTopicId);
                    Remove(topic);

                    if (_viewModel.SelectedItems.Contains(topic))
                    {
                        _viewModel.SelectedItems.Remove(topic);
                        _viewModel.Delegate?.SetSelectedItems(_viewModel.SelectedItems);
                    }

                    IsEmpty = Count == 0;

                    //if (!_hasMoreItems)
                    //{
                    //    await LoadMoreItemsAsync(0);
                    //}
                }
            }

            private int NextIndexOf(ForumTopic topic, long order)
            {
                var prev = -1;
                var next = 0;

                for (int i = 0; i < Count; i++)
                {
                    var item = this[i];
                    if (item.Info.ForumTopicId == topic.Info.ForumTopicId)
                    {
                        prev = i;
                        continue;
                    }

                    if (order > item.Order || order == item.Order && topic.Info.ForumTopicId >= item.Info.ForumTopicId)
                    {
                        return next == prev ? -1 : next;
                    }

                    next++;
                }

                return Count;
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

                if (topic is MessageTopicForum forum && _topics.Contains(forum.ForumTopicId))
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
                        _viewModel.Dispatcher?.Dispatch(NotifyChanged, Windows.System.DispatcherQueuePriority.Low);
                    }
                }
            }

            private int _totalCount;
            public int TotalCount
            {
                get => _totalCount;
                set
                {
                    if (_totalCount != value)
                    {
                        _totalCount = value;
                        OnPropertyChanged(new PropertyChangedEventArgs(nameof(TotalCount)));
                    }
                }
            }

            private void NotifyChanged()
            {
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsEmpty)));
            }
        }

        public partial class DirectMessagesChatTopicsCollection : ObservableCollection<DirectMessagesChatTopic>, ISupportIncrementalLoading, ITopicListCollection
        {
            private readonly IClientService _clientService;
            private readonly IEventAggregator _aggregator;

            private CancellationTokenSource _token = new();
            private readonly HashSet<long> _topics = new();

            private readonly TopicListViewModel _viewModel;

            private Chat _chat;

            private bool _hasMoreItems = true;

            private long _lastTopicId;
            private long _lastOrder;

            public Chat Chat => _chat;

            public DirectMessagesChatTopicsCollection(IClientService clientService, IEventAggregator aggregator, TopicListViewModel viewModel, Chat chat)
            {
                _clientService = clientService;
                _aggregator = aggregator;

                _viewModel = viewModel;
                _chat = chat;

#if MOCKUP
                _hasMoreItems = false;
#endif

                _ = LoadMoreItemsAsync(0);
            }

            public Task ReloadAsync(Chat chat)
            {
                if (_chat != null)
                {
                    _clientService.Send(new CloseChat(_chat.Id));
                }

                _token?.Cancel();
                _token = new CancellationTokenSource();

                _aggregator.Unsubscribe(this);
                _hasMoreItems = false;

                _lastTopicId = 0;
                _lastOrder = 0;

                _chat = chat;

                _topics.Clear();
                Clear();

                if (_chat != null)
                {
                    _clientService.Send(new OpenChat(chat.Id));
                    return LoadMoreItemsAsync();
                }

                return Task.CompletedTask;
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(token => LoadMoreItemsAsync());
            }

            private async Task<LoadMoreItemsResult> LoadMoreItemsAsync()
            {
                Logger.Info(Count);

                var token = _token;
                var totalCount = 0u;

                await Task.Yield();

                if (_chat == null)
                {
                    _hasMoreItems = false;

                    return new LoadMoreItemsResult
                    {
                        Count = totalCount
                    };
                }

                var response = await _clientService.GetDirectMessagesChatTopicsAsync(_chat.Id, Count, 20);
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
                        if (order != 0)
                        {
                            // TODO: is this redundant?
                            var next = NextIndexOf(topic, order);
                            if (next >= 0)
                            {
                                if (_topics.Contains(topic.Id))
                                {
                                    Remove(topic);
                                }

                                _topics.Add(topic.Id);
                                Insert(Math.Min(Count, next), topic);

                                if ((_viewModel?.SelectedItem == null && topic.Id == 0) || _viewModel?.SelectedItem?.IsDirectMessagesChat(topic.Id) is true)
                                {
                                    _viewModel?.Delegate?.SetSelectedItem(topic);
                                }

                                totalCount++;
                            }

                            _lastTopicId = topic.Id;
                            _lastOrder = order;
                        }
                    }

                    Logger.Info(string.Format("Received {0} items, added {1}", topics.TopicIds.Count, totalCount));

                    IsEmpty = Count == 0;

                    _hasMoreItems = topics.TotalCount >= 0;
                    Subscribe();

                    _viewModel?.Delegate?.SetSelectedItems(_viewModel.SelectedItems);
                }

                return new LoadMoreItemsResult
                {
                    Count = totalCount
                };
            }

            private void Subscribe()
            {
                _aggregator.Subscribe<UpdateAuthorizationState>(this, Handle)
                    //.Subscribe<UpdateChatDraftMessage>(Handle)
                    .Subscribe<UpdateDirectMessagesChatTopicLastMessage>(Handle)
                    .Subscribe<UpdateDirectMessagesChatTopicPosition>(Handle);
            }

            public bool HasMoreItems => _hasMoreItems;

            #region Handle

            public void Handle(UpdateAuthorizationState update)
            {
                if (update.AuthorizationState is AuthorizationStateReady)
                {
                    _viewModel.BeginOnUIThread(() => _ = ReloadAsync(_chat));
                }
            }

            public void Handle(UpdateDirectMessagesChatTopicPosition update)
            {
                if (update.ChatId == _chat.Id)
                {
                    Handle(update.TopicId, update.Order);
                }
            }

            public void Handle(UpdateDirectMessagesChatTopicLastMessage update)
            {
                if (update.ChatId == _chat.Id)
                {
                    Handle(update.TopicId, update.Order, true);
                }
            }

            //public void Handle(UpdateChatDraftMessage update)
            //{
            //    Handle(update.ChatId, update.Positions, true);
            //}

            public void Handle(long chatId, long order, bool lastMessage = false)
            {
                var topic = GetTopic(chatId);

                Handle(topic, order, lastMessage);
            }

            public void Handle(long chatId, long order)
            {
                var chat = GetTopic(chatId);
                if (chat != null)
                {
                    Handle(chat, order, false);
                }
            }

            private void Handle(DirectMessagesChatTopic topic, long order, bool lastMessage)
            {
                //var chat = GetChat(chatId);
                if (topic != null /*&& _chatList.ListEquals(chat.ChatList)*/)
                {
                    _viewModel?.BeginOnUIThread(() => UpdateForumTopicOrder(topic, order, lastMessage));
                }
            }

            private void UpdateForumTopicOrder(DirectMessagesChatTopic topic, long order, bool lastMessage)
            {
                if (order > 0 && (order > _lastOrder || (order == _lastOrder && topic.Id >= _lastTopicId)))
                {
                    var next = NextIndexOf(topic, order);
                    if (next >= 0)
                    {
                        if (_topics.Contains(topic.Id))
                        {
                            Remove(topic);
                        }
                        else
                        {
                            _topics.Add(topic.Id);
                        }

                        Insert(Math.Min(Count, next), topic);

                        if (next == Count - 1)
                        {
                            _lastTopicId = topic.Id;
                            _lastOrder = order;
                        }

                        if (_viewModel.SelectedItem.IsDirectMessagesChat(topic.Id))
                        {
                            _viewModel.Delegate?.SetSelectedItem(topic);
                        }
                        if (_viewModel.SelectedItems.Contains(topic))
                        {
                            _viewModel.Delegate?.SetSelectedItems(_viewModel.SelectedItems);
                        }

                        IsEmpty = Count == 0;
                    }
                    else if (lastMessage)
                    {
                        _viewModel.Delegate?.UpdateDirectMessagesChatTopicLastMessage(topic);
                    }
                }
                else if (_topics.Contains(topic.Id))
                {
                    _topics.Remove(topic.Id);
                    Remove(topic);

                    if (_viewModel.SelectedItems.Contains(topic))
                    {
                        _viewModel.SelectedItems.Remove(topic);
                        _viewModel.Delegate?.SetSelectedItems(_viewModel.SelectedItems);
                    }

                    IsEmpty = Count == 0;

                    //if (!_hasMoreItems)
                    //{
                    //    await LoadMoreItemsAsync(0);
                    //}
                }
            }

            private int NextIndexOf(DirectMessagesChatTopic topic, long order)
            {
                var prev = -1;
                var next = 0;

                for (int i = 0; i < Count; i++)
                {
                    var item = this[i];
                    if (item.Id == topic.Id)
                    {
                        prev = i;
                        continue;
                    }

                    if (order > item.Order || order == item.Order && topic.Id >= item.Id)
                    {
                        return next == prev ? -1 : next;
                    }

                    next++;
                }

                return Count;
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

                if (topic is MessageTopicDirectMessages directMessagesChat && _topics.Contains(directMessagesChat.DirectMessagesChatTopicId))
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
                        _viewModel.Dispatcher?.Dispatch(NotifyChanged, Windows.System.DispatcherQueuePriority.Low);
                    }
                }
            }

            private int _totalCount;
            public int TotalCount
            {
                get => _totalCount;
                set
                {
                    if (_totalCount != value)
                    {
                        _totalCount = value;
                        OnPropertyChanged(new PropertyChangedEventArgs(nameof(TotalCount)));
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
