using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TdWindows;
using Telegram.Api.Helpers;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Core.Common;
using Unigram.Services;
using Windows.Foundation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace Unigram.ViewModels
{
    public class ChatsViewModel : UnigramViewModelBase, IHandle<UpdateChatDraftMessage>, IHandle<UpdateChatIsPinned>, IHandle<UpdateChatLastMessage>, IHandle<UpdateChatOrder>
    {
        public ChatsViewModel(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            Items = new ItemsCollection(protoService, aggregator, this);

            DialogPinCommand = new RelayCommand<Chat>(DialogPinExecute);
            DialogNotifyCommand = new RelayCommand<Chat>(DialogNotifyExecute);
            DialogDeleteCommand = new RelayCommand<Chat>(DialogDeleteExecute);
            DialogClearCommand = new RelayCommand<Chat>(DialogClearExecute);
            DialogDeleteAndStopCommand = new RelayCommand<Chat>(DialogDeleteAndStopExecute);

            aggregator.Subscribe(this);
            protoService.Send(new GetChats(long.MaxValue, 0, 20));

#if MOCKUP
            Items.AddRange(protoService.GetChats(20));
#endif
        }

        private bool _isFirstPinned;
        public bool IsFirstPinned
        {
            get
            {
                return _isFirstPinned;
            }
            set
            {
                Set(ref _isFirstPinned, value);
            }
        }

        public SortedObservableCollection<Chat> Items { get; private set; }

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

        #region Commands

        public RelayCommand<Chat> DialogPinCommand { get; }
        private void DialogPinExecute(Chat chat)
        {
            ProtoService.Send(new ToggleChatIsPinned(chat.Id, !chat.IsPinned));
        }

        public RelayCommand<Chat> DialogNotifyCommand { get; }
        private void DialogNotifyExecute(Chat chat)
        {
            ProtoService.Send(new SetNotificationSettings(new NotificationSettingsScopeChat(chat.Id), new NotificationSettings(chat.NotificationSettings.MuteFor > 0 ? 0 : 632053052, chat.NotificationSettings.Sound, chat.NotificationSettings.ShowPreview)));
        }

        public RelayCommand<Chat> DialogDeleteCommand { get; }
        private async void DialogDeleteExecute(Chat chat)
        {
            var message = Strings.Resources.AreYouSureDeleteAndExit;
            if (chat.Type is ChatTypePrivate || chat.Type is ChatTypeSecret)
            {
                message = Strings.Resources.AreYouSureDeleteThisChat;
            }
            else if (chat.Type is ChatTypeSupergroup super)
            {
                message = super.IsChannel ? Strings.Resources.ChannelLeaveAlert : Strings.Resources.MegaLeaveAlert;
            }

            var confirm = await TLMessageDialog.ShowAsync(message, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                if (chat.Type is ChatTypeSecret secret)
                {
                    await ProtoService.SendAsync(new CloseSecretChat(secret.SecretChatId));
                }
                else if (chat.Type is ChatTypeBasicGroup || chat.Type is ChatTypeSupergroup)
                {
                    await ProtoService.SendAsync(new SetChatMemberStatus(chat.Id, ProtoService.GetMyId(), new ChatMemberStatusLeft()));
                }

                ProtoService.Send(new DeleteChatHistory(chat.Id, true));
            }
        }

        public RelayCommand<Chat> DialogClearCommand { get; }
        private async void DialogClearExecute(Chat chat)
        {
            var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.AreYouSureClearHistory, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                ProtoService.Send(new DeleteChatHistory(chat.Id, false));
            }
        }

        public RelayCommand<Chat> DialogDeleteAndStopCommand { get; }
        private async void DialogDeleteAndStopExecute(Chat chat)
        {
            var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.AreYouSureDeleteThisChat, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm == ContentDialogResult.Primary && chat.Type is ChatTypePrivate privata)
            {
                ProtoService.Send(new BlockUser(privata.UserId));
                ProtoService.Send(new DeleteChatHistory(chat.Id, true));
            }
        }

        #endregion

        protected override void BeginOnUIThread(Action action)
        {
            // This is somehow needed because this viewmodel requires a Dispatcher
            // in some situations where base one might be null.
            Execute.BeginOnUIThread(action);
        }

        public void Handle(UpdateChatOrder update)
        {
            var chat = ProtoService.GetChat(update.ChatId);
            if (chat != null)
            {
                BeginOnUIThread(() =>
                {
                    if (update.Order == 0)
                    {
                        Items.Remove(chat);
                    }
                    else
                    {
                        var index = Items.IndexOf(chat);
                        var next = Items.NextIndexOf(chat);

                        if (next >= 0 && index != next)
                        {
                            Items.Remove(chat);
                            Items.Add(chat);
                        }
                    }
                });
            }
        }

        public void Handle(UpdateChatLastMessage update)
        {
            var chat = ProtoService.GetChat(update.ChatId);
            if (chat != null)
            {
                BeginOnUIThread(() =>
                {
                    if (update.Order == 0)
                    {
                        Items.Remove(chat);
                    }
                    else
                    {
                        var index = Items.IndexOf(chat);
                        var next = Items.NextIndexOf(chat);

                        if (next >= 0 && index != next)
                        {
                            Items.Remove(chat);
                            Items.Add(chat);
                        }
                    }
                });
            }
        }

        public void Handle(UpdateChatIsPinned update)
        {
            var chat = ProtoService.GetChat(update.ChatId);
            if (chat != null)
            {
                BeginOnUIThread(() =>
                {
                    if (update.Order == 0)
                    {
                        Items.Remove(chat);
                    }
                    else
                    {
                        var index = Items.IndexOf(chat);
                        var next = Items.NextIndexOf(chat);

                        if (next >= 0 && index != next)
                        {
                            Items.Remove(chat);
                            Items.Add(chat);
                        }
                    }
                });
            }
        }

        public void Handle(UpdateChatDraftMessage update)
        {
            var chat = ProtoService.GetChat(update.ChatId);
            if (chat != null)
            {
                BeginOnUIThread(() =>
                {
                    if (update.Order == 0)
                    {
                        Items.Remove(chat);
                    }
                    else
                    {
                        var index = Items.IndexOf(chat);
                        var next = Items.NextIndexOf(chat);

                        if (next >= 0 && index != next)
                        {
                            Items.Remove(chat);
                            Items.Add(chat);
                        }
                    }
                });
            }
        }

        class ItemsCollection : SortedObservableCollection<Chat>, IGroupSupportIncrementalLoading
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

            public ItemsCollection(IProtoService protoService, IEventAggregator aggregator, ChatsViewModel viewModel)
                : base(new ChatComparer())
            {
                _protoService = protoService;
                _aggregator = aggregator;

                _viewModel = viewModel;
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(async token =>
                {
                    var order = long.MaxValue;
                    var offset = 0L;

                    var last = this.LastOrDefault();
                    if (last != null)
                    {
                        order = last.Order;
                        offset = last.Id;

                        if (order == 0)
                        {
                            return new LoadMoreItemsResult();
                        }
                    }

                    _aggregator.Subscribe(_viewModel);
                    _protoService.Send(new GetChats(order, offset, 20));

                    //var response = await _protoService.SendAsync(new GetChats(order, offset, 20));
                    //if (response is TdWindows.Chats chats)
                    //{
                    //    foreach (var id in chats.ChatIds)
                    //    {
                    //        var chat = _protoService.GetChat(id);
                    //        if (chat != null && chat.Order != 0)
                    //        {
                    //            Add(chat);
                    //        }
                    //    }

                    //    _aggregator.Subscribe(_viewModel);
                    //    return new LoadMoreItemsResult { Count = (uint)chats.ChatIds.Count };
                    //}

                    return new LoadMoreItemsResult { Count = 20 };
                });
            }

            public bool HasMoreItems => true;
        }

        public class SearchChatsCollection : ObservableCollection<KeyedList<string, object>>, ISupportIncrementalLoading
        {
            private readonly IProtoService _protoService;
            private readonly string _query;

            private readonly List<long> _chats = new List<long>();
            private readonly List<int> _users = new List<int>();

            private KeyedList<string, object> _local;
            private KeyedList<string, object> _remote;
            private KeyedList<string, object> _messages;

            public SearchChatsCollection(IProtoService protoService, string query)
            {
                _protoService = protoService;
                _query = query;

                _local = new KeyedList<string, object>(null as string);
                _remote = new KeyedList<string, object>(Strings.Resources.GlobalSearch);
                _messages = new KeyedList<string, object>(Strings.Resources.SearchMessages);

                Add(_local);
                Add(_remote);
                Add(_messages);
            }

            public string Query => _query;

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint phase)
            {
                return AsyncInfo.Run(async token =>
                {
                    if (phase == 0)
                    {
                        var response = await _protoService.SendAsync(new SearchChats(_query, 100));
                        if (response is TdWindows.Chats chats)
                        {
                            foreach (var id in chats.ChatIds)
                            {
                                var chat = _protoService.GetChat(id);
                                if (chat != null)
                                {
                                    if (chat.Type is ChatTypePrivate privata)
                                    {
                                        _users.Add(privata.UserId);
                                    }

                                    _chats.Add(id);
                                    _local.Add(new SearchResult(chat, _query, false));
                                }
                            }
                        }
                    }
                    else if (phase == 1)
                    {
                        var response = await _protoService.SendAsync(new SearchContacts(_query, 100));
                        if (response is TdWindows.Users users)
                        {
                            foreach (var id in users.UserIds)
                            {
                                if (_users.Contains(id))
                                {
                                    continue;
                                }

                                var user = _protoService.GetUser(id);
                                if (user != null)
                                {
                                    _users.Add(id);
                                    _local.Add(new SearchResult(user, _query, false));
                                }
                            }
                        }
                    }
                    else if (phase == 2)
                    {
                        var response = await _protoService.SendAsync(new SearchChatsOnServer(_query, 100));
                        if (response is TdWindows.Chats chats && _local != null)
                        {
                            foreach (var id in chats.ChatIds)
                            {
                                if (_chats.Contains(id))
                                {
                                    continue;
                                }

                                var chat = _protoService.GetChat(id);
                                if (chat != null)
                                {
                                    if (chat.Type is ChatTypePrivate privata && _users.Contains(privata.UserId))
                                    {
                                        continue;
                                    }

                                    _local.Add(new SearchResult(chat, _query, false));
                                }
                            }
                        }
                    }
                    else if (phase == 3)
                    {
                        var response = await _protoService.SendAsync(new SearchPublicChats(_query));
                        if (response is TdWindows.Chats chats)
                        {
                            foreach (var id in chats.ChatIds)
                            {
                                var chat = _protoService.GetChat(id);
                                if (chat != null)
                                {
                                    _remote.Add(new SearchResult(chat, _query, true));
                                }
                            }
                        }
                    }
                    else if (phase == 4)
                    {
                        var response = await _protoService.SendAsync(new SearchMessages(_query, int.MaxValue, 0, 0, 100));
                        if (response is TdWindows.Messages messages)
                        {
                            foreach (var message in messages.MessagesData)
                            {
                                _messages.Add(message);
                            }
                        }
                    }

                    return new LoadMoreItemsResult();
                });
            }

            public bool HasMoreItems => false;
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
