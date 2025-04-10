//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System;
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

namespace Telegram.ViewModels
{
    public partial class TopicListViewModel : ViewModelBase, IDelegable<IChatListDelegate>
    {
        private readonly INotificationsService _notificationsService;

        private readonly Dictionary<long, bool> _deletedChats = new Dictionary<long, bool>();

        public IChatListDelegate Delegate { get; set; }

        public TopicListViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, INotificationsService notificationsService, long chatId)
            : base(clientService, settingsService, aggregator)
        {
            _notificationsService = notificationsService;

            Items = new ItemsCollection(clientService, aggregator, this, null);

            SearchFilters = new MvxObservableCollection<ISearchChatsFilter>();

            ChatsMarkCommand = new RelayCommand(ChatsMarkExecute);
            ChatsNotifyCommand = new RelayCommand(ChatsNotifyExecute);
            ChatsDeleteCommand = new RelayCommand(ChatsDeleteExecute);
            ChatsClearCommand = new RelayCommand(ChatsClearExecute);

#if MOCKUP
            Items.AddRange(clientService.GetChats(null));
#endif

            SelectedItems = new MvxObservableCollection<ForumTopic>();
        }

        #region Selection

        private long? _selectedItem;
        public long? SelectedItem
        {
            get => _selectedItem;
            set => Set(ref _selectedItem, value);
        }

        private MvxObservableCollection<ForumTopic> _selectedItems;
        public MvxObservableCollection<ForumTopic> SelectedItems
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

        public ItemsCollection Items { get; private set; }

        public bool IsLastSliceLoaded { get; set; }

        public MvxObservableCollection<ISearchChatsFilter> SearchFilters { get; private set; }

        #region Open

        public void OpenTopic(ForuminoTopicino chat)
        {
            //NavigationService.NavigateToChat(chat, createNewWindow: true);
        }

        #endregion

        #region Pin

        public void HideTopic(ForuminoTopicino topic)
        {
            if (Chat is Chat chat)
            {
                ClientService.Send(new ToggleGeneralForumTopicIsHidden(chat.Id, !topic.Info.IsHidden));
            }
        }

        #endregion

        #region Pin

        public async void PinTopic(ForuminoTopicino chat)
        {
            //var position = chat.GetPosition(Items.ChatList);
            //if (position == null)
            //{
            //    return;
            //}

            //var response = await ClientService.SendAsync(new ToggleChatIsPinned(Items.ChatList, chat.Id, !position.IsPinned));
            //if (response is Error error && error.Code == 400)
            //{
            //    // This is not the right way
            //    NavigationService.ShowLimitReached(new PremiumLimitTypePinnedChatCount());
            //}
        }

        #endregion

        #region Mark

        public void MarkTopicAsRead(ForuminoTopicino topic)
        {
            if (Chat is Chat chat && topic.UnreadCount > 0)
            {
                if (topic.LastMessage != null)
                {
                    ClientService.Send(new ViewMessages(chat.Id, new[] { topic.LastMessage.Id }, new MessageSourceChatList(), true));
                }

                if (topic.UnreadMentionCount > 0)
                {
                    ClientService.Send(new ReadAllMessageThreadMentions(chat.Id, topic.Info.MessageThreadId));
                }

                if (topic.UnreadReactionCount > 0)
                {
                    ClientService.Send(new ReadAllMessageThreadReactions(chat.Id, topic.Info.MessageThreadId));
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

        public void NotifyTopic(ForuminoTopicino topic)
        {
            if (Chat is Chat chat)
            {
                _notificationsService.SetMuteFor(chat, ClientService.Notifications.GetMuteFor(chat, topic) > 0 ? 0 : 632053052, NavigationService.XamlRoot);
            }
        }

        #endregion

        #region Notify

        public void CloseTopic(ForuminoTopicino topic)
        {
            if (Chat is Chat chat)
            {
                ClientService.Send(new ToggleForumTopicIsClosed(chat.Id, topic.Info.MessageThreadId, !topic.Info.IsClosed));
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

        public async void DeleteTopic(ForuminoTopicino topic)
        {
            var message = string.Format(Strings.DeleteSelectedTopic, topic.Info.Name);
            var title = Locale.Declension(Strings.R.DeleteTopics, 1);

            var confirm = await ShowPopupAsync(message, title, Strings.Delete, Strings.Cancel, destructive: true);
            if (confirm == ContentDialogResult.Primary)
            {
                // TODO: Handle the case where topics can't be deleted because user isn't admin
                ClientService.Send(new DeleteForumTopic(Chat.Id, topic.Id));
            }
        }

        #endregion

        #region Multiple Delete

        public RelayCommand ChatsDeleteCommand { get; }
        private async void ChatsDeleteExecute()
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

        private async void ClearTopic(ForumTopic chat)
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

        #endregion

        #region Multiple Clear

        public RelayCommand ChatsClearCommand { get; }
        private async void ChatsClearExecute()
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

        public void SelectTopic(ForuminoTopicino chat)
        {
            //SelectedItems.ReplaceWith(new[] { chat });
            //SelectionMode = ListViewSelectionMode.Multiple;

            //Delegate?.SetSelectedItems(_selectedItems);
        }

        #endregion

        public Chat Chat => Items.Chat;

        public void SetChat(Chat chat)
        {
            if (chat?.Id != Items.Chat?.Id)
            {
                _ = Items.ReloadAsync(chat);
            }
        }

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
                var response = await ClientService.SendAsync(new CreateForumTopic(chat.Id, popup.SelectedName, popup.SelectedIcon));
                if (response is ForumTopicInfo info)
                {
                    NavigationService.NavigateToChat(chat, thread: info.MessageThreadId, force: false, clearBackStack: true);
                }
            }
        }

        public partial class ItemsCollection : ObservableCollection<ForuminoTopicino>, ISupportIncrementalLoading
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

            public ItemsCollection(IClientService clientService, IEventAggregator aggregator, TopicListViewModel viewModel, Chat chat)
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
                    return new LoadMoreItemsResult
                    {
                        Count = totalCount
                    };
                }

                var response = await _clientService.GetForumTopicsAsync(_chat.Id, Count, 20);
                if (response is ForuminoTopicinos topics && !token.IsCancellationRequested)
                {
                    foreach (var topic in _clientService.GetTopics(_chat.Id, topics.TopicIds))
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

                                if (topic.Id == _viewModel?._selectedItem)
                                {
                                    //_viewModel.Delegate?.SetSelectedItem(topic);
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

                    //_viewModel.Delegate?.SetSelectedItems(_viewModel.SelectedItems);
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
                    Handle(update.MessageThreadId, update.Order);
                }
            }

            public void Handle(UpdateForumTopicLastMessage update)
            {
                if (update.ChatId == _chat.Id)
                {
                    Handle(update.MessageThreadId, update.Order, true);
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

            private void Handle(ForuminoTopicino topic, long order, bool lastMessage)
            {
                //var chat = GetChat(chatId);
                if (topic != null /*&& _chatList.ListEquals(chat.ChatList)*/)
                {
                    _viewModel?.BeginOnUIThread(() => UpdateForumTopicOrder(topic, order, lastMessage));
                }
            }

            private void UpdateForumTopicOrder(ForuminoTopicino topic, long order, bool lastMessage)
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

                        //if (topic.Id == _viewModel._selectedItem)
                        //{
                        //    _viewModel.Delegate?.SetSelectedItem(topic);
                        //}
                        //if (_viewModel.SelectedItems.Contains(topic))
                        //{
                        //    _viewModel.Delegate?.SetSelectedItems(_viewModel.SelectedItems);
                        //}

                        IsEmpty = Count == 0;
                    }
                    else if (lastMessage)
                    {
                        _viewModel.Delegate?.UpdateForumTopicLastMessage(topic);
                    }
                }
                else if (_topics.Contains(topic.Id))
                {
                    _topics.Remove(topic.Id);
                    Remove(topic);

                    //if (topic.Id == _viewModel._selectedItem)
                    //{
                    //    _viewModel.Delegate?.SetSelectedItem(topic);
                    //}
                    //if (_viewModel.SelectedItems.Contains(topic))
                    //{
                    //    _viewModel.SelectedItems.Remove(topic);
                    //    _viewModel.Delegate?.SetSelectedItems(_viewModel.SelectedItems);
                    //}

                    IsEmpty = Count == 0;

                    //if (!_hasMoreItems)
                    //{
                    //    await LoadMoreItemsAsync(0);
                    //}
                }
            }

            private int NextIndexOf(ForuminoTopicino topic, long order)
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

            private ForuminoTopicino GetTopic(long messageThreadId)
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

                return _clientService.GetTopic(_chat.Id, messageThreadId);
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
                        _viewModel.Dispatcher?.Dispatch(NotifyChanged, Microsoft.UI.Dispatching.DispatcherQueuePriority.Low);
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
