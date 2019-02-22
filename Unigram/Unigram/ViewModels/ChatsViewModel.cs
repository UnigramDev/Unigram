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
using Unigram.Controls.Views;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Windows.Foundation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace Unigram.ViewModels
{
    public class ChatsViewModel : TLViewModelBase, IDelegable<IChatsDelegate>, IHandle<UpdateChatDraftMessage>, IHandle<UpdateChatIsPinned>, IHandle<UpdateChatIsSponsored>, IHandle<UpdateChatLastMessage>, IHandle<UpdateChatOrder>
    {
        private readonly Dictionary<long, ChatViewModel> _viewModels = new Dictionary<long, ChatViewModel>();

        private readonly Dictionary<long, bool> _deletedChats = new Dictionary<long, bool>();

        public IChatsDelegate Delegate { get; set; }

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

#if MOCKUP
            Items.AddRange(protoService.GetChats(20));
#endif
        }

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
            var dialog = new DeleteChatView(ProtoService, chat, false);

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                _deletedChats[chat.Id] = true;
                Handle(chat.Id, 0);

                Delegate?.DeleteChat(chat, false, async delete =>
                {
                    if (delete.Type is ChatTypeSecret secret)
                    {
                        await ProtoService.SendAsync(new CloseSecretChat(secret.SecretChatId));
                    }
                    else if (delete.Type is ChatTypeBasicGroup || delete.Type is ChatTypeSupergroup)
                    {
                        await ProtoService.SendAsync(new LeaveChat(delete.Id));
                    }

                    ProtoService.Send(new DeleteChatHistory(delete.Id, true));
                }, undo =>
                {
                    _deletedChats.Remove(undo.Id);
                    Handle(undo.Id, undo.Order);
                });
            }
        }

        public RelayCommand<Chat> ChatClearCommand { get; }
        private async void ChatClearExecute(Chat chat)
        {
            var dialog = new DeleteChatView(ProtoService, chat, true);

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                Delegate?.DeleteChat(chat, true, delete =>
                {
                    ProtoService.Send(new DeleteChatHistory(delete.Id, false));
                }, undo =>
                {
                    _deletedChats.Remove(undo.Id);
                    Handle(undo.Id, undo.Order);
                });
            }
        }

        public RelayCommand<Chat> ChatDeleteAndStopCommand { get; }
        private async void ChatDeleteAndStopExecute(Chat chat)
        {
            var dialog = new DeleteChatView(ProtoService, chat, false);

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                _deletedChats[chat.Id] = true;
                Handle(chat.Id, 0);

                Delegate?.DeleteChat(chat, false, delete =>
                {
                    if (delete.Type is ChatTypePrivate privata)
                    {
                        ProtoService.Send(new BlockUser(privata.UserId));
                    }

                    ProtoService.Send(new DeleteChatHistory(delete.Id, true));
                }, undo =>
                {
                    _deletedChats.Remove(undo.Id);
                    Handle(undo.Id, undo.Order);
                });
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
            if (chat != null)
            {
                if (items.Filter != null && !items.Filter.Intersects(chat))
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
                        }
                    }
                    else
                    {
                        items.Remove(chat);

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

        public void SetFilter(IChatFilter filter)
        {
            Items = new ItemsCollection(ProtoService, Aggregator, this, filter);
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

            private readonly IChatFilter _filter;

            private bool _hasMoreItems = true;

            private long _lastChatId;
            private long _lastOrder;

            private long _internalChatId = 0;
            private long _internalOrder = long.MaxValue;

            public long LastChatId => _lastChatId;
            public long LastOrder => _lastOrder;

            public IChatFilter Filter => _filter;

            public ItemsCollection(IProtoService protoService, IEventAggregator aggregator, ChatsViewModel viewModel, IChatFilter filter = null)
                : base(new ChatComparer(), true)
            {
                _protoService = protoService;
                _aggregator = aggregator;

                _viewModel = viewModel;

                _filter = filter;
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(async token =>
                {
                    var response = await _protoService.SendAsync(new GetChats(_internalOrder, _internalChatId, 20));
                    if (response is Telegram.Td.Api.Chats chats)
                    {
                        foreach (var id in chats.ChatIds)
                        {
                            var chat = _protoService.GetChat(id);
                            if (chat != null && chat.Order != 0)
                            {
                                _internalChatId = chat.Id;
                                _internalOrder = chat.Order;

                                if (_filter != null && !_filter.Intersects(chat))
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

                        return new LoadMoreItemsResult { Count = (uint)chats.ChatIds.Count };
                    }

                    return new LoadMoreItemsResult { Count = 0 };
                });
            }

            public bool HasMoreItems => _hasMoreItems;
        }
    }

    public interface IChatFilter
    {
        bool Intersects(Chat chat); 
    }

    public class ChatTypeFilter : IChatFilter
    {
        private readonly ICacheService _cacheService;
        private readonly ChatTypeFilterMode _filter;

        public ChatTypeFilter(ICacheService cacheService, ChatTypeFilterMode filter)
        {
            _cacheService = cacheService;
            _filter = filter;
        }

        public bool Intersects(Chat chat)
        {
            switch (_filter)
            {
                case ChatTypeFilterMode.Unmuted:
                    return chat.NotificationSettings.MuteFor > 0 ? false : true;
                case ChatTypeFilterMode.Unread:
                    return chat.UnreadCount > 0 || chat.UnreadMentionCount > 0 || chat.IsMarkedAsUnread;
                case ChatTypeFilterMode.Users:
                    var user = _cacheService.GetUser(chat);
                    if (user != null)
                    {
                        return user.Type is UserTypeRegular;
                    }

                    return false;
                case ChatTypeFilterMode.Bots:
                    var bot = _cacheService.GetUser(chat);
                    if (bot != null)
                    {
                        return bot.Type is UserTypeBot;
                    }

                    return false;
                case ChatTypeFilterMode.Groups:
                    return chat.Type is ChatTypeBasicGroup || chat.Type is ChatTypeSupergroup super && !super.IsChannel;
                case ChatTypeFilterMode.Channels:
                    return chat.Type is ChatTypeSupergroup channel && channel.IsChannel;
                default:
                case ChatTypeFilterMode.None:
                    return true;
            }
        }
    }

    public enum ChatTypeFilterMode
    {
        None,

        Users,
        Bots,
        Groups,
        Channels,

        Unread,
        Unmuted,
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
