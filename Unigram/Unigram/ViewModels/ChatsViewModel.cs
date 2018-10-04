using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Template10.Mvvm;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Windows.Foundation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace Unigram.ViewModels
{
    public class ChatsViewModel : TLViewModelBase, IHandle<UpdateChatDraftMessage>, IHandle<UpdateChatIsPinned>, IHandle<UpdateChatIsSponsored>, IHandle<UpdateChatLastMessage>, IHandle<UpdateChatOrder>
    {
        private readonly Dictionary<long, ChatViewModel> _viewModels = new Dictionary<long, ChatViewModel>();

        public ChatsViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Items = new ItemsCollection(protoService, aggregator, this);

            ChatPinCommand = new RelayCommand<Chat>(ChatPinExecute);
            ChatMarkCommand = new RelayCommand<Chat>(ChatMarkExecute);
            ChatNotifyCommand = new RelayCommand<Chat>(ChatNotifyExecute);
            ChatDeleteCommand = new RelayCommand<Chat>(ChatDeleteExecute);
            ChatClearCommand = new RelayCommand<Chat>(ChatClearExecute);
            ChatDeleteAndStopCommand = new RelayCommand<Chat>(ChatDeleteAndStopExecute);

            ClearRecentChatsCommand = new RelayCommand(ClearRecentChatsExecute);

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

        #region Commands

        public RelayCommand<Chat> ChatPinCommand { get; }
        private void ChatPinExecute(Chat chat)
        {
            ProtoService.Send(new ToggleChatIsPinned(chat.Id, !chat.IsPinned));
        }

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

        public RelayCommand<Chat> ChatNotifyCommand { get; }
        private void ChatNotifyExecute(Chat chat)
        {
            ProtoService.Send(new SetChatNotificationSettings(chat.Id, new ChatNotificationSettings(false, chat.NotificationSettings.MuteFor > 0 ? 0 : 632053052, false, chat.NotificationSettings.Sound, false, chat.NotificationSettings.ShowPreview)));
        }

        public RelayCommand<Chat> ChatDeleteCommand { get; }
        private async void ChatDeleteExecute(Chat chat)
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
                    await ProtoService.SendAsync(new LeaveChat(chat.Id));
                }

                ProtoService.Send(new DeleteChatHistory(chat.Id, true));
            }
        }

        public RelayCommand<Chat> ChatClearCommand { get; }
        private async void ChatClearExecute(Chat chat)
        {
            var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.AreYouSureClearHistory, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                ProtoService.Send(new DeleteChatHistory(chat.Id, false));
            }
        }

        public RelayCommand<Chat> ChatDeleteAndStopCommand { get; }
        private async void ChatDeleteAndStopExecute(Chat chat)
        {
            var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.AreYouSureDeleteThisChat, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm == ContentDialogResult.Primary && chat.Type is ChatTypePrivate privata)
            {
                ProtoService.Send(new BlockUser(privata.UserId));
                ProtoService.Send(new DeleteChatHistory(chat.Id, true));
            }
        }



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

        #endregion

        protected override void BeginOnUIThread(Action action)
        {
            // This is somehow needed because this viewmodel requires a Dispatcher
            // in some situations where base one might be null.
            Execute.BeginOnUIThread(action);
        }

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
            var chat = GetChat(chatId);
            if (chat != null)
            {
                BeginOnUIThread(() =>
                {
                    if (order == 0)
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
                    //if (response is Telegram.Td.Api.Chats chats)
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
