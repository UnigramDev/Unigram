//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Windows.Foundation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace Telegram.ViewModels
{
    public class TopicListViewModel : ViewModelBase, IDelegable<IChatListDelegate>
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

        public void OpenTopic(ForumTopic chat)
        {
            //NavigationService.NavigateToChat(chat, createNewWindow: true);
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

        public async void PinTopic(ForumTopic chat)
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

        public void MarkTopicAsRead(ForumTopic topic)
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

        public void NotifyTopic(ForumTopic topic)
        {
            if (Chat is Chat chat)
            {
                _notificationsService.SetMuteFor(chat, ClientService.Notifications.GetMutedFor(chat, topic) > 0 ? 0 : 632053052);
            }
        }

        #endregion

        #region Notify

        public void CloseTopic(ForumTopic topic)
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

        public async void DeleteTopic(ForumTopic chat)
        {
            //var updated = await ClientService.SendAsync(new GetChat(chat.Id)) as Chat ?? chat;
            //var dialog = new DeleteChatPopup(ClientService, updated, Items.ChatList, false);

            //var confirm = await ShowPopupAsync(dialog);
            //if (confirm == ContentDialogResult.Primary)
            //{
            //    var check = dialog.IsChecked == true;

            //    _deletedChats[chat.Id] = true;
            //    Items.Handle(chat.Id, 0);

            //    Delegate?.ShowChatsUndo(new[] { chat }, UndoType.Delete, items =>
            //    {
            //        var undo = items.FirstOrDefault();
            //        if (undo == null)
            //        {
            //            return;
            //        }

            //        _deletedChats.Remove(undo.Id);
            //        Items.Handle(undo.Id, undo.Positions);
            //    }, async items =>
            //    {
            //        var delete = items.FirstOrDefault();
            //        if (delete == null)
            //        {
            //            return;
            //        }

            //        if (delete.Type is ChatTypeSecret secret)
            //        {
            //            await ClientService.SendAsync(new CloseSecretChat(secret.SecretChatId));
            //        }
            //        else if (delete.Type is ChatTypeBasicGroup or ChatTypeSupergroup)
            //        {
            //            await ClientService.SendAsync(new LeaveChat(delete.Id));
            //        }

            //        var user = ClientService.GetUser(delete);
            //        if (user != null && user.Type is UserTypeRegular)
            //        {
            //            ClientService.Send(new DeleteChatHistory(delete.Id, true, check));
            //        }
            //        else
            //        {
            //            if (delete.Type is ChatTypePrivate privata && check)
            //            {
            //                await ClientService.SendAsync(new ToggleMessageSenderIsBlocked(new MessageSenderUser(privata.UserId), true));
            //            }

            //            ClientService.Send(new DeleteChatHistory(delete.Id, true, false));
            //        }
            //    });
            //}
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

        public RelayCommand<ForumTopic> ChatClearCommand { get; }
        private async void ChatClearExecute(ForumTopic chat)
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

        public void SelectTopic(ForumTopic chat)
        {
            //SelectedItems.ReplaceWith(new[] { chat });
            //SelectionMode = ListViewSelectionMode.Multiple;

            //Delegate?.SetSelectedItems(_selectedItems);
        }

        #endregion

        public Chat Chat => Items.Chat;

        public async void SetFilter(Chat chat)
        {
            await Items.ReloadAsync(chat);
            //Aggregator.Unsubscribe(Items);
            //Items = new ItemsCollection(ClientService, Aggregator, this, chatList);
            //RaisePropertyChanged(nameof(Items));
        }

        public class ItemsCollection : ObservableCollection<ForumTopic>
            , ISupportIncrementalLoading
        //, IHandle<UpdateAuthorizationState>
        //, IHandle<UpdateChatDraftMessage>
        //, IHandle<UpdateChatLastMessage>
        //, IHandle<UpdateChatPosition>
        {
            private readonly IClientService _clientService;
            private readonly IEventAggregator _aggregator;

            private readonly DisposableMutex _loadMoreLock = new DisposableMutex();

            private readonly TopicListViewModel _viewModel;

            private Chat _chat;

            private bool _hasMoreItems = true;

            private long _nextOffsetMessageThreadId;
            private long _nextOffsetMessageId;
            private int _nextOffsetDate;

            private readonly Dictionary<long, ForumTopic> _topics = new();

            public Chat Chat => _chat;

            public ItemsCollection(IClientService clientService, IEventAggregator aggregator, TopicListViewModel viewModel, Chat chat)
            {
                _clientService = clientService;
                _aggregator = aggregator;

                _viewModel = viewModel;
                _viewModel.IsLoading = true;

                _chat = chat;
                _hasMoreItems = chat != null;

#if MOCKUP
                _hasMoreItems = false;
#endif

                _ = LoadMoreItemsAsync(0);
            }

            public async Task ReloadAsync(Chat chat)
            {
                _viewModel.IsLoading = true;

                using (await _loadMoreLock.WaitAsync())
                {
                    _aggregator.Unsubscribe(this);

                    _nextOffsetMessageThreadId = 0;
                    _nextOffsetMessageId = 0;
                    _nextOffsetDate = 0;

                    _topics.Clear();
                    _chat = chat;

                    Clear();
                }

                if (chat != null)
                {
                    await LoadMoreItemsAsync();
                }
            }

            public bool TryGetValue(long key, out ForumTopic value)
            {
                return _topics.TryGetValue(key, out value);
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(token => LoadMoreItemsAsync());
            }

            private async Task<LoadMoreItemsResult> LoadMoreItemsAsync()
            {
                using (await _loadMoreLock.WaitAsync())
                {
                    if (_chat == null)
                    {
                        return new LoadMoreItemsResult();
                    }

                    //var response = await _clientService.SendAsync(new GetChats(_chatList, _internalOrder, _internalChatId, 20));
                    var response = await _clientService.SendAsync(new GetForumTopics(_chat.Id, string.Empty, _nextOffsetDate, _nextOffsetMessageId, _nextOffsetMessageThreadId, 20));
                    if (response is ForumTopics topics)
                    {
                        foreach (var topic in topics.Topics)
                        {
                            if (_topics.ContainsKey(topic.Info.MessageThreadId))
                            {
                                continue;
                            }

                            _topics[topic.Info.MessageThreadId] = topic;
                            Add(topic);
                        }

                        IsEmpty = Count == 0;

                        _nextOffsetMessageThreadId = topics.NextOffsetMessageThreadId;
                        _nextOffsetMessageId = topics.NextOffsetMessageId;
                        _nextOffsetDate = topics.NextOffsetDate;

                        _hasMoreItems = topics.Topics.Count > 0;

                        _viewModel.IsLoading = false;
                        //_viewModel.Delegate?.SetSelectedItems(_viewModel._selectedItems);

                        if (_hasMoreItems == false)
                        {
                            OnPropertyChanged(new PropertyChangedEventArgs("HasMoreItems"));
                        }

                        return new LoadMoreItemsResult { Count = (uint)topics.Topics.Count };
                    }

                    return new LoadMoreItemsResult { Count = 0 };
                }
            }

            #region Handle

            public void Handle(UpdateForumTopicInfo update)
            {

            }

            #endregion

            public bool HasMoreItems => _hasMoreItems;

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
                        OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsEmpty)));
                    }
                }
            }
        }
    }
}
