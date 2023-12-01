//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Common.Chats;
using Telegram.Controls;
using Telegram.Controls.Chats;
using Telegram.Controls.Messages;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Factories;
using Telegram.Streams;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels.Chats;
using Telegram.ViewModels.Delegates;
using Telegram.ViewModels.Drawers;
using Telegram.Views;
using Telegram.Views.Popups;
using Telegram.Views.Premium.Popups;
using Telegram.Views.Users;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Point = Windows.Foundation.Point;

namespace Telegram.ViewModels
{
    public class ChatNavigationArgs
    {
        public ChatNavigationArgs(long chatId, long threadId)
        {
            ChatId = chatId;
            MessageId = threadId;
        }

        public long ChatId { get; }

        public long MessageId { get; }
    }

    public partial class DialogViewModel : ComposeViewModel, IDelegable<IDialogDelegate>
    {
        private readonly ConcurrentDictionary<long, MessageViewModel> _selectedItems = new();
        public IDictionary<long, MessageViewModel> SelectedItems => _selectedItems;

        public int SelectedCount => SelectedItems.Count;

        protected readonly ConcurrentDictionary<long, MessageViewModel> _groupedMessages = new();

        protected static readonly Dictionary<MessageId, MessageContent> _contentOverrides = new();

        protected readonly DisposableMutex _loadMoreLock = new();

        protected readonly EmojiDrawerViewModel _emoji;
        protected readonly StickerDrawerViewModel _stickers;
        protected readonly AnimationDrawerViewModel _animations;

        protected readonly IMessageDelegate _messageDelegate;

        protected readonly DialogUnreadMessagesViewModel _mentions;
        protected readonly DialogUnreadMessagesViewModel _reactions;

        protected readonly ILocationService _locationService;
        protected readonly INotificationsService _notificationsService;
        protected readonly IPlaybackService _playbackService;
        protected readonly IVoipService _voipService;
        protected readonly IVoipGroupService _voipGroupService;
        protected readonly INetworkService _networkService;
        protected readonly IStorageService _storageService;
        protected readonly ITranslateService _translateService;
        protected readonly IMessageFactory _messageFactory;

        public IPlaybackService PlaybackService => _playbackService;

        public IStorageService StorageService => _storageService;

        public ITranslateService TranslateService => _translateService;

        public DialogUnreadMessagesViewModel Mentions => _mentions;
        public DialogUnreadMessagesViewModel Reactions => _reactions;

        public IDialogDelegate Delegate { get; set; }

        public DialogViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, ILocationService locationService, INotificationsService pushService, IPlaybackService playbackService, IVoipService voipService, IVoipGroupService voipGroupService, INetworkService networkService, IStorageService storageService, ITranslateService translateService, IMessageFactory messageFactory)
            : base(clientService, settingsService, aggregator)
        {
            _locationService = locationService;
            _notificationsService = pushService;
            _playbackService = playbackService;
            _voipService = voipService;
            _voipGroupService = voipGroupService;
            _networkService = networkService;
            _storageService = storageService;
            _translateService = translateService;
            _messageFactory = messageFactory;

            //_stickers = new DialogStickersViewModel(clientService, settingsService, aggregator);
            _emoji = EmojiDrawerViewModel.GetForCurrentView(clientService.SessionId);
            _stickers = StickerDrawerViewModel.GetForCurrentView(clientService.SessionId);
            _animations = AnimationDrawerViewModel.GetForCurrentView(clientService.SessionId);

            _messageDelegate = new DialogMessageDelegate(this);

            _mentions = new DialogUnreadMessagesViewModel(this, new SearchMessagesFilterUnreadMention());
            _reactions = new DialogUnreadMessagesViewModel(this, new SearchMessagesFilterUnreadReaction());

            //Items = new LegacyMessageCollection();
            //Items.CollectionChanged += (s, args) => IsEmpty = Items.Count == 0;

            _count++;
            System.Diagnostics.Debug.WriteLine("Creating DialogViewModel {0}", _count);
        }

        private static volatile int _count;

        ~DialogViewModel()
        {
            System.Diagnostics.Debug.WriteLine("Finalizing DialogViewModel {0}", _count);
            _count--;
        }

        public void Dispose()
        {
            System.Diagnostics.Debug.WriteLine("Disposing DialogViewModel");
            _groupedMessages.Clear();
        }

        public Action<Sticker> Sticker_Click;

        public override IDispatcherContext Dispatcher
        {
            get => base.Dispatcher;
            set
            {
                base.Dispatcher = value;

                _stickers.Dispatcher = value;
                _animations.Dispatcher = value;
            }
        }

        protected Chat _linkedChat;
        public Chat LinkedChat
        {
            get => _linkedChat;
            set => Set(ref _linkedChat, value);
        }

        public override long ThreadId
        {
            get
            {
                if (_topic != null)
                {
                    return _topic.Info.IsGeneral ? 0 : _topic.Info.MessageThreadId;
                }
                else if (_thread != null)
                {
                    return _thread.MessageThreadId;
                }

                return 0;
            }
        }

        protected MessageThreadInfo _thread;
        public MessageThreadInfo Thread
        {
            get => _thread;
            set => Set(ref _thread, value);
        }

        protected ForumTopic _topic;
        public ForumTopic Topic
        {
            get => _topic;
            set => Set(ref _topic, value);
        }

        protected Chat _chat;
        public override Chat Chat
        {
            get => _chat;
            set => Set(ref _chat, value);
        }

        private DialogType _type => Type;
        public virtual DialogType Type => DialogType.History;

        private string _lastSeen;
        public string LastSeen
        {
            get => _type == DialogType.EventLog ? Strings.EventLog : _lastSeen;
            set { Set(ref _lastSeen, value); RaisePropertyChanged(nameof(Subtitle)); }
        }

        private string _onlineCount;
        public string OnlineCount
        {
            get => _onlineCount;
            set { Set(ref _onlineCount, value); RaisePropertyChanged(nameof(Subtitle)); }
        }

        public virtual string Subtitle
        {
            get
            {
                var chat = _chat;
                if (chat == null)
                {
                    return null;
                }

                if (chat.Type is ChatTypePrivate or ChatTypeSecret)
                {
                    return _lastSeen;
                }

                if (Topic == null && !string.IsNullOrEmpty(_onlineCount) && !string.IsNullOrEmpty(_lastSeen))
                {
                    return string.Format("{0}, {1}", _lastSeen, _onlineCount);
                }

                return _lastSeen;
            }
        }

        private ChatSearchViewModel _search;
        public ChatSearchViewModel Search
        {
            get => _search;
            set => Set(ref _search, value);
        }

        public void DisposeSearch()
        {
            var search = _search;
            if (search != null)
            {
                search.Dispose();
                UpdateQuery(string.Empty);
            }

            Search = null;
        }

        private string _accessToken;
        public string AccessToken
        {
            get => _accessToken;
            set
            {
                Set(ref _accessToken, value);
                RaisePropertyChanged(nameof(HasAccessToken));
            }
        }

        public bool HasAccessToken
        {
            get
            {
                return (_accessToken != null || _isEmpty) && !_loadingSlice;
            }
        }

        private DispatcherTimer _informativeTimer;

        private MessageViewModel _informativeMessage;
        public MessageViewModel InformativeMessage
        {
            get => _informativeMessage;
            set
            {
                if (value != null)
                {
                    if (_informativeTimer == null)
                    {
                        _informativeTimer = new DispatcherTimer();
                        _informativeTimer.Interval = TimeSpan.FromSeconds(5);
                        _informativeTimer.Tick += (s, args) =>
                        {
                            _informativeTimer.Stop();
                            InformativeMessage = null;
                        };
                    }

                    _informativeTimer.Stop();
                    _informativeTimer.Start();
                }

                Set(ref _informativeMessage, value);
                Delegate?.UpdateCallbackQueryAnswer(_chat, value);
            }
        }

        private OutputChatActionManager _chatActionManager;
        public OutputChatActionManager ChatActionManager
        {
            get
            {
                return _chatActionManager ??= new OutputChatActionManager(ClientService, _chat, ThreadId);
            }
        }

        private bool _needsUpdateSpeechRecognitionTrial;

        private bool _hasLoadedLastPinnedMessage = false;

        public long LockedPinnedMessageId { get; set; }
        public MessageViewModel LastPinnedMessage { get; private set; }
        public IList<MessageViewModel> PinnedMessages { get; } = new List<MessageViewModel>();

        private SponsoredMessage _sponsoredMessage;
        public SponsoredMessage SponsoredMessage
        {
            get => _sponsoredMessage;
            set => Set(ref _sponsoredMessage, value);
        }

        public int UnreadCount
        {
            get
            {
                if (_type != DialogType.History)
                {
                    return 0;
                }

                return _chat?.UnreadCount ?? 0;
            }
        }

        private bool _isSelectionEnabled;
        public bool IsSelectionEnabled
        {
            get => _isSelectionEnabled;
            set
            {
                Set(ref _isSelectionEnabled, value);

                if (value)
                {
                    DisposeSearch();
                }
                else
                {
                    SelectedItems.Clear();
                }
            }
        }

        public ChatTextBox TextField { get; set; }
        public ChatHistoryView HistoryField { get; set; }

        public void SetSelection(int start)
        {
            var field = TextField;
            if (field == null)
            {
                return;
            }

            field.Document.GetText(TextGetOptions.None, out string text);
            field.Document.Selection.SetRange(start, text.Length);
        }

        public void SetText(FormattedText text, bool focus = false)
        {
            if (text == null)
            {
                SetText(null, null, focus);
            }
            else
            {
                SetText(text.Text, text.Entities, focus);
            }
        }

        public void SetText(string text, IList<TextEntity> entities = null, bool focus = false)
        {
            var field = TextField;
            if (field == null)
            {
                return;
            }

            var chat = Chat;
            if (chat != null && chat.Type is ChatTypeSupergroup super && super.IsChannel && !string.IsNullOrEmpty(text))
            {
                var supergroup = ClientService.GetSupergroup(super.SupergroupId);
                if (supergroup != null && !supergroup.CanPostMessages())
                {
                    return;
                }
            }

            if (string.IsNullOrEmpty(text))
            {
                field.SetText(null);
            }
            else
            {
                field.SetText(text, entities);
            }

            if (focus)
            {
                field.Focus(FocusState.Keyboard);
            }
        }

        public void SetScrollMode(ItemsUpdatingScrollMode mode, bool force)
        {
            var field = HistoryField;
            if (field == null)
            {
                return;
            }

            field.SetScrollingMode(mode, force);
        }

        public string GetText(TextGetOptions options = TextGetOptions.NoHidden)
        {
            var field = TextField;
            if (field == null)
            {
                return null;
            }

            field.Document.GetText(options, out string text);
            return text;
        }

        public override FormattedText GetFormattedText(bool clear = false)
        {
            var field = TextField;
            if (field == null)
            {
                return new FormattedText(string.Empty, Array.Empty<TextEntity>());
            }

            return field.GetFormattedText(clear);
        }

        public bool IsEndReached()
        {
            var chat = _chat;
            if (chat?.LastMessage == null)
            {
                return false;
            }

            var last = Items.LastOrDefault();
            if (last?.Content is MessageAlbum album)
            {
                last = album.Messages.LastOrDefault();
            }

            if (last == null || last.Id == 0)
            {
                return true;
            }

            return chat.LastMessage?.Id == last.Id;
        }

        public bool? IsFirstSliceLoaded { get; set; }
        public bool? IsLastSliceLoaded { get; set; }

        private bool _isEmpty = true;
        public bool IsEmpty
        {
            get => _isEmpty && !_loadingSlice;
            set
            {
                Set(ref _isEmpty, value);
                RaisePropertyChanged(nameof(HasAccessToken));
            }
        }

        public override bool IsLoading
        {
            get => _loadingSlice;
            set
            {
                base.IsLoading = value;
                RaisePropertyChanged(nameof(IsEmpty));
                RaisePropertyChanged(nameof(HasAccessToken));
            }
        }

        protected bool _loadingSlice;

        protected Stack<long> _repliesStack = new Stack<long>();

        public Stack<long> RepliesStack => _repliesStack;

        // Scrolling to top
        public virtual Task LoadNextSliceAsync()
        {
            return LoadNextSliceAsync(PanelScrollingDirection.Backward);
        }

        // Scrolling to bottom
        public Task LoadPreviousSliceAsync()
        {
            return LoadNextSliceAsync(PanelScrollingDirection.Forward);
        }

        private async Task LoadNextSliceAsync(PanelScrollingDirection direction)
        {
            // Backward => Going to top, to the past
            // Forward => Going to bottom, to the present

            if (_type is not DialogType.History and not DialogType.Thread and not DialogType.Pinned)
            {
                return;
            }

            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            using (await _loadMoreLock.WaitAsync())
            {
                if (_loadingSlice || _chat?.Id != chat.Id || Items.Count < 1)
                {
                    return;
                }

                if (direction == PanelScrollingDirection.Backward && IsLastSliceLoaded == true)
                {
                    return;
                }

                _loadingSlice = true;
                IsLoading = true;

                System.Diagnostics.Debug.WriteLine("DialogViewModel: LoadNextSliceAsync");

                long fromMessageId;
                int offset;

                if (direction == PanelScrollingDirection.Backward)
                {
                    fromMessageId = Items.FirstId;
                    offset = 0;
                }
                else
                {
                    fromMessageId = Items.LastId;
                    offset = -49;
                }

                if (fromMessageId == long.MaxValue || fromMessageId == long.MinValue)
                {
                    _loadingSlice = false;
                    IsLoading = false;

                    return;
                }

                Function func;
                if (_topic != null)
                {
                    func = new GetMessageThreadHistory(chat.Id, _topic.Info.MessageThreadId, fromMessageId, offset, 50);
                }
                else if (ThreadId != 0)
                {
                    func = new GetMessageThreadHistory(chat.Id, ThreadId, fromMessageId, offset, 50);
                }
                else if (_type == DialogType.Pinned)
                {
                    func = new SearchChatMessages(chat.Id, string.Empty, null, fromMessageId, offset, 50, new SearchMessagesFilterPinned(), 0);
                }
                else
                {
                    func = new GetChatHistory(chat.Id, fromMessageId, offset, 50, false);
                }

                var tsc = new TaskCompletionSource<MessageCollection>();
                void handler(BaseObject result)
                {
                    if (result is FoundChatMessages foundChatMessages)
                    {
                        tsc.SetResult(new MessageCollection(Items.Ids, foundChatMessages.Messages, CreateMessage));
                    }
                    else if (result is Messages messages)
                    {
                        tsc.SetResult(new MessageCollection(Items.Ids, messages.MessagesValue, CreateMessage));
                    }
                    else
                    {
                        tsc.SetResult(null);
                    }
                }

                ClientService.Send(func, handler);

                var response = await tsc.Task;
                if (response is MessageCollection replied)
                {
                    if (replied.Count > 0)
                    {
                        ProcessMessages(chat, replied);

                        if (direction == PanelScrollingDirection.Backward)
                        {
                            SetScrollMode(ItemsUpdatingScrollMode.KeepLastItemInView, true);
                            Items.RawInsertRange(0, replied, true, out bool empty);
                        }
                        else
                        {
                            SetScrollMode(ItemsUpdatingScrollMode.KeepItemsInView, true);
                            Items.RawAddRange(replied, true, out bool empty);
                        }
                    }
                    else if (direction == PanelScrollingDirection.Backward)
                    {
                        await AddHeaderAsync();
                    }
                    else
                    {
                        SetScrollMode(ItemsUpdatingScrollMode.KeepLastItemInView, true);
                    }

                    if (direction == PanelScrollingDirection.Backward)
                    {
                        IsLastSliceLoaded = replied.Count == 0;
                        UpdateDetectedLanguage();
                    }
                    else
                    {
                        IsFirstSliceLoaded = replied.Count == 0 || IsEndReached();
                    }
                }

                _loadingSlice = false;
                IsLoading = false;

                LoadPinnedMessagesSliceAsync(fromMessageId, direction);
            }
        }

        protected async Task AddHeaderAsync()
        {
            var previous = Items.FirstOrDefault();
            if (previous != null && (previous.Content is MessageHeaderDate || (previous.Content is MessageText && previous.Id == 0)))
            {
                return;
            }

            var chat = _chat;
            if (chat == null || _type != DialogType.History)
            {
                goto AddDate;
            }

            var user = ClientService.GetUser(chat);
            if (user == null || user.Type is not UserTypeBot)
            {
                goto AddDate;
            }

            var fullInfo = ClientService.GetUserFull(user.Id);
            fullInfo ??= await ClientService.SendAsync(new GetUserFullInfo(user.Id)) as UserFullInfo;

            if (fullInfo != null && fullInfo.BotInfo?.Description.Length > 0)
            {
                var result = Client.Execute(new GetTextEntities(fullInfo.BotInfo.Description)) as TextEntities;
                var entities = result?.Entities ?? new List<TextEntity>();

                foreach (var entity in entities)
                {
                    entity.Offset += Strings.BotInfoTitle.Length + Environment.NewLine.Length;
                }

                entities.Add(new TextEntity(0, Strings.BotInfoTitle.Length, new TextEntityTypeBold()));

                var message = $"{Strings.BotInfoTitle}{Environment.NewLine}{fullInfo.BotInfo.Description}";
                var text = new FormattedText(message, entities);

                MessageContent content;
                if (fullInfo.BotInfo.Animation != null)
                {
                    content = new MessageAnimation(fullInfo.BotInfo.Animation, text, false, false);
                }
                else if (fullInfo.BotInfo.Photo != null)
                {
                    content = new MessagePhoto(fullInfo.BotInfo.Photo, text, false, false);
                }
                else
                {
                    content = new MessageText(text, null, null);
                }

                Items.Insert(0, CreateMessage(new Message(0, new MessageSenderUser(user.Id), chat.Id, null, null, false, false, false, false, false, false, true, false, false, false, false, false, false, false, false, false, false, false, 0, 0, null, null, null, null, null, 0, null, 0, 0, 0, string.Empty, 0, string.Empty, content, null)));
                return;
            }

        AddDate:
            if (_topic == null && _thread != null)
            {
                var replied = _thread.Messages.OrderBy(x => x.Id)
                    .Select(x => CreateMessage(x))
                    .ToList();

                ProcessMessages(chat, replied);

                previous = replied[0];

                if (Items.Count > 0)
                {
                    Items.Insert(0, CreateMessage(new Message(0, previous.SenderId, previous.ChatId, null, null, previous.IsOutgoing, false, false, false, false, false, true, false, false, false, false, false, false, false, false, previous.IsChannelPost, previous.IsTopicMessage, false, previous.Date, 0, null, null, null, null, null, 0, null, 0, 0, 0, string.Empty, 0, string.Empty, new MessageCustomServiceAction(Strings.DiscussionStarted), null)));
                }
                else
                {
                    Items.Insert(0, CreateMessage(new Message(0, previous.SenderId, previous.ChatId, null, null, previous.IsOutgoing, false, false, false, false, false, true, false, false, false, false, false, false, false, false, previous.IsChannelPost, previous.IsTopicMessage, false, previous.Date, 0, null, null, null, null, null, 0, null, 0, 0, 0, string.Empty, 0, string.Empty, new MessageCustomServiceAction(Strings.NoComments), null)));
                }

                Items.Insert(0, previous);
                Items.Insert(0, CreateMessage(new Message(0, previous.SenderId, previous.ChatId, null, null, previous.IsOutgoing, false, false, false, false, false, true, false, false, false, false, false, false, false, false, previous.IsChannelPost, previous.IsTopicMessage, false, previous.Date, 0, null, null, null, null, null, 0, null, 0, 0, 0, string.Empty, 0, string.Empty, new MessageHeaderDate(), null)));
            }
            else if (previous != null)
            {
                Items.Insert(0, CreateMessage(new Message(0, previous.SenderId, previous.ChatId, null, null, previous.IsOutgoing, false, false, false, false, false, true, false, false, false, false, false, false, false, false, previous.IsChannelPost, previous.IsTopicMessage, false, previous.Date, 0, null, null, null, null, null, 0, null, 0, 0, 0, string.Empty, 0, string.Empty, new MessageHeaderDate(), null)));
            }
        }

        public async void PreviousSlice()
        {
            if (_type is DialogType.ScheduledMessages or DialogType.EventLog)
            {
                ScrollToBottom();
            }
            else if (_repliesStack.Count > 0)
            {
                await LoadMessageSliceAsync(null, _repliesStack.Pop());
            }
            else
            {
                await LoadLastSliceAsync();
            }

            TextField?.Focus(FocusState.Programmatic);
        }

        public Task LoadLastSliceAsync()
        {
            var chat = _chat;
            if (chat == null)
            {
                return Task.CompletedTask;
            }

            long lastReadMessageId;
            long lastMessageId;

            if (_topic is ForumTopic topic)
            {
                lastReadMessageId = topic.LastReadInboxMessageId;
                lastMessageId = topic.LastMessage?.Id ?? long.MaxValue;
            }
            else if (_thread is MessageThreadInfo thread)
            {
                lastReadMessageId = thread.ReplyInfo?.LastReadInboxMessageId ?? long.MaxValue;
                lastMessageId = thread.ReplyInfo?.LastMessageId ?? long.MaxValue;
            }
            else
            {
                lastReadMessageId = chat.LastReadInboxMessageId;
                lastMessageId = chat.LastMessage?.Id ?? long.MaxValue;
            }

            if (TryGetLastVisibleMessageId(out long id, out _) && id < lastReadMessageId)
            {
                return LoadMessageSliceAsync(null, lastReadMessageId, VerticalAlignment.Top, disableAnimation: false);
            }
            else
            {
                return LoadMessageSliceAsync(null, lastMessageId, VerticalAlignment.Top, disableAnimation: false);
            }
        }

        private bool TryGetLastVisibleMessageId(out long id, out int index)
        {
            var field = HistoryField;
            if (field != null)
            {
                var panel = field.ItemsPanelRoot as ItemsStackPanel;
                if (panel != null && panel.LastVisibleIndex >= 0 && panel.LastVisibleIndex < Items.Count - 1 && Items.Count > 0)
                {
                    id = Items[panel.LastVisibleIndex].Id;
                    index = panel.LastVisibleIndex;
                    return true;
                }
            }

            id = -1;
            index = -1;
            return false;
        }

        private bool TryGetFirstVisibleMessageId(out long id)
        {
            var field = HistoryField;
            if (field != null)
            {
                var panel = field.ItemsPanelRoot as ItemsStackPanel;
                if (panel != null && panel.FirstVisibleIndex >= 0 && panel.FirstVisibleIndex < Items.Count && Items.Count > 0)
                {
                    id = Items[panel.FirstVisibleIndex].Id;
                    return true;
                }
            }

            id = -1;
            return false;
        }

        private async void LoadPinnedMessagesSliceAsync(long maxId, PanelScrollingDirection direction = PanelScrollingDirection.None)
        {
            await Task.Yield();

            var chat = _chat;
            if (chat == null || _type != DialogType.History)
            {
                return;
            }

            var hidden = Settings.GetChatPinnedMessage(chat.Id);
            if (hidden != 0)
            {
                Delegate?.UpdatePinnedMessage(chat, false);
                return;
            }

            if (direction == PanelScrollingDirection.Backward)
            {
                var first = PinnedMessages.FirstOrDefault();
                if (first?.Id < maxId)
                {
                    return;
                }
            }
            else if (direction == PanelScrollingDirection.Forward)
            {
                var last = PinnedMessages.LastOrDefault();
                if (last?.Id > maxId)
                {
                    return;
                }
            }

            var filter = new SearchMessagesFilterPinned();

            if (!_hasLoadedLastPinnedMessage)
            {
                _hasLoadedLastPinnedMessage = true;
                //Delegate?.UpdatePinnedMessage(chat, null, chat.PinnedMessageId != 0);
                //Delegate?.UpdatePinnedMessage(chat, true);

                var count = await ClientService.SendAsync(new GetChatMessageCount(chat.Id, filter, true)) as Count;
                if (count != null)
                {
                    Delegate?.UpdatePinnedMessage(chat, count.CountValue > 0);
                }
                else
                {
                    Delegate?.UpdatePinnedMessage(chat, false);
                }

                var pinned = await ClientService.SendAsync(new GetChatPinnedMessage(chat.Id)) as Message;
                //if (pinned == null)
                //{
                //    Delegate?.UpdatePinnedMessage(chat, false);
                //    return;
                //}

                LastPinnedMessage = CreateMessage(pinned);
                //Delegate?.UpdatePinnedMessage(chat, LastPinnedMessage);
            }

            var offset = direction == PanelScrollingDirection.Backward ? 0 : direction == PanelScrollingDirection.Forward ? -49 : -25;
            var limit = 50;

            if (direction == PanelScrollingDirection.None && (maxId == LastPinnedMessage?.Id || maxId == 0))
            {
                offset = -1;
                limit = 100;
            }

            var response = await ClientService.SendAsync(new SearchChatMessages(chat.Id, string.Empty, null, maxId, offset, limit, new SearchMessagesFilterPinned(), 0));
            if (response is FoundChatMessages messages)
            {
                if (direction == PanelScrollingDirection.None)
                {
                    PinnedMessages.Clear();
                }

                var replied = messages.Messages.OrderByDescending(x => x.Id).Select(x => CreateMessage(x)).ToList();

                foreach (var message in replied)
                {
                    if (direction == PanelScrollingDirection.Forward)
                    {
                        InsertMessageInOrder(PinnedMessages, message);
                    }
                    else
                    {
                        PinnedMessages.Insert(0, message);
                    }
                }

                Delegate?.ViewVisibleMessages();
            }
            else
            {
                Delegate?.UpdatePinnedMessage(chat, false);
            }
        }

        // This is to notify the view to update bindings
        public event EventHandler MessageSliceLoaded;

        protected void NotifyMessageSliceLoaded()
        {
            MessageSliceLoaded?.Invoke(this, EventArgs.Empty);
            MessageSliceLoaded = null;
        }

        public async Task LoadMessageSliceAsync(long? previousId, long maxId, VerticalAlignment alignment = VerticalAlignment.Center, double? pixel = null, ScrollIntoViewAlignment? direction = null, bool? disableAnimation = null, TextQuote highlight = null, bool second = false)
        {
            if (_type is not DialogType.History and not DialogType.Thread and not DialogType.Pinned)
            {
                NotifyMessageSliceLoaded();
                return;
            }

            var chat = _chat;
            if (chat == null)
            {
                NotifyMessageSliceLoaded();
                return;
            }

            if (direction == null && TryGetFirstVisibleMessageId(out long firstVisibleId))
            {
                direction = firstVisibleId < maxId ? ScrollIntoViewAlignment.Default : ScrollIntoViewAlignment.Leading;
            }

            if (alignment == VerticalAlignment.Bottom && pixel == null)
            {
                pixel = int.MaxValue;
            }

            if (Items.TryGetValue(maxId, out MessageViewModel already))
            {
                if (alignment == VerticalAlignment.Top && !second)
                {
                    long lastMessageId;
                    if (_topic is ForumTopic topic)
                    {
                        lastMessageId = topic.LastMessage?.Id ?? long.MaxValue;
                    }
                    else if (_thread is MessageThreadInfo thread)
                    {
                        lastMessageId = thread.ReplyInfo?.LastMessageId ?? long.MaxValue;
                    }
                    else
                    {
                        lastMessageId = chat.LastMessage?.Id ?? long.MaxValue;
                    }

                    // If we're loading the last message and it has been read already
                    // then we want to align it at bottom, as it might be taller than the window height
                    if (maxId == lastMessageId)
                    {
                        alignment = VerticalAlignment.Bottom;
                        pixel = null;
                    }
                }

                var field = HistoryField;
                if (field != null)
                {
                    await field.ScrollToItem(already, alignment, alignment == VerticalAlignment.Center ? new MessageBubbleHighlightOptions(highlight) : null, pixel, direction ?? ScrollIntoViewAlignment.Leading, disableAnimation);
                }

                if (previousId.HasValue && !_repliesStack.Contains(previousId.Value))
                {
                    _repliesStack.Push(previousId.Value);
                }

                NotifyMessageSliceLoaded();
                return;
            }
            else if (second)
            {
                if (maxId == 0)
                {
                    if (_thread != null)
                    {
                        ScrollToTop();
                    }
                    else
                    {
                        ScrollToBottom();
                    }
                }

                //var max = _dialog?.UnreadCount > 0 ? _dialog.ReadInboxMaxId : int.MaxValue;
                //var offset = _dialog?.UnreadCount > 0 && max > 0 ? -16 : 0;
                //await LoadFirstSliceAsync(max, offset);
                NotifyMessageSliceLoaded();
                return;
            }

            using (await _loadMoreLock.WaitAsync())
            {
                if (_loadingSlice || _chat?.Id != chat.Id)
                {
                    NotifyMessageSliceLoaded();
                    return;
                }

                _loadingSlice = true;
                IsLastSliceLoaded = null;
                IsFirstSliceLoaded = null;
                IsLoading = true;

                System.Diagnostics.Debug.WriteLine("DialogViewModel: LoadMessageSliceAsync");

                var response = await Task.Run(() => LoadMessageSliceImpl(chat, maxId, alignment, direction, pixel));
                if (response is LoadSliceResult slice)
                {
                    _groupedMessages.Clear();

                    maxId = slice.FromMessageId;
                    pixel = slice.Pixel;
                    alignment = slice.Alignment;

                    SetScrollMode(slice.ScrollMode, true);

                    var replied = slice.Items;

                    ProcessMessages(chat, replied);
                    Items.RawReplaceWith(replied);

                    NotifyMessageSliceLoaded();

                    IsLastSliceLoaded = null;
                    IsFirstSliceLoaded = IsEndReached();

                    UpdateDetectedLanguage();

                    if (_topic == null && _thread != null && _thread.Messages.Any(x => x.Id == maxId))
                    {
                        await AddHeaderAsync();
                    }
                    else if (replied.Empty())
                    {
                        await AddHeaderAsync();
                    }

                    await AddSponsoredMessagesAsync();
                }

                _loadingSlice = false;
                IsLoading = false;

                LoadPinnedMessagesSliceAsync(maxId);
            }

            await LoadMessageSliceAsync(previousId, maxId, alignment, pixel, direction, disableAnimation, highlight, true);
        }

        private async Task<LoadSliceResult> LoadMessageSliceImpl(Chat chat, long maxId, VerticalAlignment alignment, ScrollIntoViewAlignment? direction, double? pixel)
        {
            Task<BaseObject> func;
            if (_topic != null)
            {
                // TODO: Workaround, should be removed some day
                await ClientService.SendAsync(new GetMessage(chat.Id, _topic.Info.MessageThreadId));

                func = ClientService.SendAsync(new GetMessageThreadHistory(chat.Id, _topic.Info.MessageThreadId, maxId, -25, 50));
            }
            else if (ThreadId != 0)
            {
                // MaxId == 0 means that the thread was never opened
                if (maxId == 0 || Thread.Messages.Any(x => x.Id == maxId))
                {
                    func = ClientService.SendAsync(new GetMessageThreadHistory(chat.Id, ThreadId, 1, -25, 50));
                }
                else
                {
                    func = ClientService.SendAsync(new GetMessageThreadHistory(chat.Id, ThreadId, maxId, -25, 50));
                }
            }
            else if (_type == DialogType.Pinned)
            {
                func = ClientService.SendAsync(new SearchChatMessages(chat.Id, string.Empty, null, maxId, -25, 50, new SearchMessagesFilterPinned(), 0));
            }
            else
            {
                async Task<BaseObject> GetChatHistoryAsync(long chatId, long fromMessageId, int offset, int limit, bool onlyLocal)
                {
                    var response = await ClientService.SendAsync(new GetChatHistory(chatId, fromMessageId, offset, limit, onlyLocal));
                    if (response is Messages messages && onlyLocal)
                    {
                        var count = messages.MessagesValue.Count;
                        var outOfRange = fromMessageId != 0 && count > 0 && messages.MessagesValue[^1].Id > fromMessageId;

                        if (outOfRange || count < 5)
                        {
                            var force = await ClientService.SendAsync(new GetChatHistory(chatId, fromMessageId, offset, limit, count > 0 && !outOfRange));
                            if (force is Messages forceMessages)
                            {
                                if (forceMessages.MessagesValue.Count == 0 && count > 0 && !outOfRange)
                                {
                                    force = await ClientService.SendAsync(new GetChatHistory(chatId, fromMessageId, offset, limit, false));

                                    if (force is Messages forceMessages2)
                                    {
                                        return forceMessages2;
                                    }
                                }
                                else
                                {
                                    return forceMessages;
                                }
                            }
                        }
                    }

                    return response;
                }

                func = GetChatHistoryAsync(chat.Id, maxId, -25, 50, alignment == VerticalAlignment.Top);
            }

            if (alignment != VerticalAlignment.Center)
            {
                var wait = await Task.WhenAny(func, Task.Delay(200));
                if (wait != func)
                {
                    Dispatcher.Dispatch(NotifyMessageSliceLoaded);
                    //Items.Clear();
                    //NotifyMessageSliceLoaded();
                }
            }

            var response = await func;
            if (response is FoundChatMessages foundChatMessages)
            {
                response = new Messages(foundChatMessages.TotalCount, foundChatMessages.Messages);
            }

            if (response is Messages messages)
            {
                _groupedMessages.Clear();

                var firstVisibleIndex = -1;
                var firstVisibleItem = default(Message);

                var unread = false;

                if (alignment != VerticalAlignment.Center)
                {
                    long lastReadMessageId;
                    long lastMessageId;

                    if (_topic is ForumTopic topic)
                    {
                        lastReadMessageId = topic.LastReadInboxMessageId;
                        lastMessageId = topic.LastMessage?.Id ?? long.MaxValue;
                    }
                    else if (_thread is MessageThreadInfo thread)
                    {
                        lastReadMessageId = thread.ReplyInfo?.LastReadInboxMessageId ?? long.MaxValue;
                        lastMessageId = thread.ReplyInfo?.LastMessageId ?? long.MaxValue;
                    }
                    else
                    {
                        lastReadMessageId = chat.LastReadInboxMessageId;
                        lastMessageId = chat.LastMessage?.Id ?? long.MaxValue;
                    }

                    bool Included(long id)
                    {
                        return true;
                        return messages.MessagesValue.Count > 0 && messages.MessagesValue[0].Id >= id && messages.MessagesValue[^1].Id <= id;
                    }

                    // If we're loading from the last read message
                    // then we want to skip it to align first unread message at top
                    if (lastReadMessageId != 0 && lastReadMessageId != lastMessageId && Included(maxId) && Included(lastReadMessageId) /*maxId >= lastReadMessageId*/)
                    {
                        var target = default(Message);
                        var index = -1;

                        for (int i = messages.MessagesValue.Count - 1; i >= 0; i--)
                        {
                            var current = messages.MessagesValue[i];
                            if (current.Id > lastReadMessageId)
                            {
                                if (index == -1)
                                {
                                    firstVisibleIndex = i;
                                    firstVisibleItem = current;
                                }

                                if ((target == null || target.IsOutgoing) && !current.IsOutgoing)
                                {
                                    target = current;
                                    index = i;
                                }
                                else if (current.IsOutgoing)
                                {
                                    target = current;
                                    index = -1;
                                }
                            }
                            else if (firstVisibleIndex == -1 && i == 0)
                            {
                                firstVisibleIndex = i;
                                firstVisibleItem = current;
                            }
                        }

                        if (target != null)
                        {
                            if (index >= 0 && index < messages.MessagesValue.Count - 1)
                            {
                                messages.MessagesValue.Insert(index + 1, new Message(0, target.SenderId, target.ChatId, null, null, target.IsOutgoing, false, false, false, false, false, true, false, false, false, false, false, false, false, false, target.IsChannelPost, target.IsTopicMessage, false, target.Date, 0, null, null, null, null, null, 0, null, 0, 0, 0, string.Empty, 0, string.Empty, new MessageHeaderUnread(), null));
                                unread = true;
                            }
                            else if (maxId == lastReadMessageId)
                            {
                                Logger.Debug("Looking for first unread message, can't find it");
                            }

                            if (maxId == lastReadMessageId && pixel == null)
                            {
                                maxId = target.Id;
                                pixel = 28 + 48;
                            }
                        }
                    }

                    if (firstVisibleItem != null && pixel == null)
                    {
                        maxId = firstVisibleItem.Id;
                    }

                    // If we're loading the last message and it has been read already
                    // then we want to align it at bottom, as it might be taller than the window height
                    if (maxId == lastMessageId)
                    {
                        alignment = VerticalAlignment.Bottom;
                        pixel = null;
                    }
                }

                if (firstVisibleIndex == -1)
                {
                    for (int i = 0; i < messages.MessagesValue.Count; i++)
                    {
                        if (messages.MessagesValue[i].Id == maxId)
                        {
                            firstVisibleIndex = i;
                            unread = false;
                            break;
                        }
                    }
                }

                if (firstVisibleIndex == -1)
                {
                    System.Diagnostics.Debugger.Break();
                }

                IEnumerable<Message> values;
                ItemsUpdatingScrollMode scrollMode;
                if (alignment == VerticalAlignment.Bottom)
                {
                    scrollMode = ItemsUpdatingScrollMode.KeepLastItemInView;
                    values = messages.MessagesValue;
                }
                else if (alignment == VerticalAlignment.Top)
                {
                    if (unread)
                    {
                        firstVisibleIndex++;
                    }

                    scrollMode = ItemsUpdatingScrollMode.KeepItemsInView;
                    values = firstVisibleIndex == -1
                        ? messages.MessagesValue
                        : messages.MessagesValue.Take(firstVisibleIndex + 1);
                }
                else
                {
                    scrollMode = direction == ScrollIntoViewAlignment.Default ? ItemsUpdatingScrollMode.KeepLastItemInView : ItemsUpdatingScrollMode.KeepItemsInView;
                    values = messages.MessagesValue;
                }

                var replied = new MessageCollection(null, values, CreateMessage);
                return new LoadSliceResult(replied, maxId, scrollMode, alignment, pixel);
            }

            return null;
        }

        private class LoadSliceResult
        {
            public LoadSliceResult(MessageCollection items, long fromMessageId, ItemsUpdatingScrollMode scrollMode, VerticalAlignment alignment, double? pixel)
            {
                Items = items;
                FromMessageId = fromMessageId;
                ScrollMode = scrollMode;
                Alignment = alignment;
                Pixel = pixel;
            }

            public MessageCollection Items { get; }

            public long FromMessageId { get; }

            public ItemsUpdatingScrollMode ScrollMode { get; }

            public VerticalAlignment Alignment { get; }

            public double? Pixel { get; }
        }

        private async Task AddSponsoredMessagesAsync()
        {
            var chat = _chat;
            if (chat == null || chat.Type is not ChatTypeSupergroup supergroup || !supergroup.IsChannel)
            {
                return;
            }

            var response = await ClientService.SendAsync(new GetChatSponsoredMessages(chat.Id));
            if (response is SponsoredMessages sponsored && sponsored.Messages.Count > 0)
            {
                SponsoredMessage = sponsored.Messages[0];
                //Items.Add(CreateMessage(new Message(0, new MessageSenderChat(sponsored.SponsorChatId), sponsored.SponsorChatId, null, null, false, false, false, false, false, false, false, false, false, false, false, false, true, false, 0, 0, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, sponsored.Content, null)));
            }
        }


        public async Task LoadScheduledSliceAsync()
        {
            using (await _loadMoreLock.WaitAsync())
            {
                var chat = _chat;
                if (chat == null)
                {
                    return;
                }

                if (_loadingSlice)
                {
                    return;
                }

                _loadingSlice = true;
                IsLastSliceLoaded = null;
                IsFirstSliceLoaded = null;
                IsLoading = true;

                Logger.Info();

                var response = await ClientService.SendAsync(new GetChatScheduledMessages(chat.Id));
                if (response is Messages messages)
                {
                    _groupedMessages.Clear();

                    if (messages.MessagesValue.Count > 0)
                    {
                        SetScrollMode(ItemsUpdatingScrollMode.KeepLastItemInView, true);
                        Logger.Debug("Setting scroll mode to KeepLastItemInView");
                    }

                    var replied = messages.MessagesValue.OrderBy(x => x.Id).Select(x => CreateMessage(x)).ToList();
                    ProcessMessages(chat, replied);

                    var target = replied.FirstOrDefault();
                    if (target != null)
                    {
                        replied.Insert(0, CreateMessage(new Message(0, target.SenderId, target.ChatId, null, target.SchedulingState, target.IsOutgoing, false, false, false, false, false, true, false, false, false, false, false, false, false, false, target.IsChannelPost, target.IsTopicMessage, false, target.Date, 0, null, null, null, null, null, 0, null, 0, 0, 0, string.Empty, 0, string.Empty, new MessageHeaderDate(), null)));
                    }

                    Items.ReplaceWith(replied);

                    IsLastSliceLoaded = true;
                    IsFirstSliceLoaded = true;
                }

                _loadingSlice = false;
                IsLoading = false;
            }
        }

        public virtual Task LoadEventLogSliceAsync(string query = "")
        {
            return Task.CompletedTask;
        }

        public async Task LoadDateSliceAsync(int dateOffset)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var response = await ClientService.SendAsync(new GetChatMessageByDate(chat.Id, dateOffset));
            if (response is Message message)
            {
                await LoadMessageSliceAsync(null, message.Id);
            }
            else
            {
                response = await ClientService.SendAsync(new GetChatHistory(chat.Id, 1, -1, 1, false));
                if (response is Messages messages && messages.MessagesValue.Count > 0)
                {
                    await LoadMessageSliceAsync(null, messages.MessagesValue[0].Id);
                }
            }
        }

        public void ScrollToBottom()
        {
            //if (IsFirstSliceLoaded)
            {
                var field = HistoryField;
                if (field == null)
                {
                    return;
                }

                field.ScrollToBottom();
                field.SetScrollingMode(ItemsUpdatingScrollMode.KeepLastItemInView, true);
            }
        }

        public void ScrollToTop()
        {
            //if (IsFirstSliceLoaded)
            {
                var field = HistoryField;
                if (field == null)
                {
                    return;
                }

                field.ScrollToTop();
                field.SetScrollingMode(ItemsUpdatingScrollMode.KeepItemsInView, true);
            }
        }

        public MessageCollection Items { get; } = new MessageCollection();

        public MessageViewModel CreateMessage(Message message, bool forLanguageStatistics = false)
        {
            if (message == null)
            {
                return null;
            }

            var model = _messageFactory.Create(_messageDelegate, _chat, message, true);

            if (forLanguageStatistics)
            {
                UpdateLanguageStatistics(model);
            }

            return model;
        }

        protected void ProcessMessages(Chat chat, IList<MessageViewModel> messages)
        {
            ProcessAlbums(chat, messages);

            for (int i = 0; i < messages.Count; i++)
            {
                var message = messages[i];
                if (message.Content is MessageForumTopicCreated or MessageChatUpgradeFrom && Type == DialogType.Thread)
                {
                    messages.RemoveAt(i);
                    i--;

                    continue;
                }

                if (_contentOverrides.TryGetValue(message.CombinedId, out MessageContent content))
                {
                    message.Content = content;
                }

                if (message.Content is MessageDice dice)
                {
                    if (message.Id > chat.LastReadInboxMessageId)
                    {
                        message.GeneratedContentUnread = true;
                    }
                    else if (!message.GeneratedContentUnread)
                    {
                        message.GeneratedContentUnread = dice.IsInitialState();
                    }
                }
                else if (message.Id > chat.LastReadInboxMessageId)
                {
                    message.GeneratedContentUnread = true;
                }

                if (message.Content is MessageStory story)
                {
                    message.Content = new MessageAsyncStory
                    {
                        ViaMention = story.ViaMention,
                        StoryId = story.StoryId,
                        StorySenderChatId = story.StorySenderChatId
                    };
                }

                ProcessEmoji(message);
                ProcessReplies(chat, message);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessEmoji(MessageViewModel message)
        {
            if (ClientService.Options.DisableAnimatedEmoji)
            {
                return;
            }

            if (message.Content is MessageText text && text.WebPage == null && text.Text.Entities.Count == 0)
            {
                if (Emoji.TryCountEmojis(text.Text.Text, out int count, 3))
                {
                    message.GeneratedContent = new MessageBigEmoji(text.Text, count);
                }
                else
                {
                    message.GeneratedContent = null;
                }
            }
            else if (message.Content is MessageAnimatedEmoji animatedEmoji && animatedEmoji.AnimatedEmoji.Sticker != null)
            {
                message.GeneratedContent = new MessageSticker(animatedEmoji.AnimatedEmoji.Sticker, false);
            }
            else
            {
                message.GeneratedContent = null;
            }
        }

        private void ProcessAlbums(Chat chat, IList<MessageViewModel> slice)
        {
            Dictionary<long, Tuple<MessageViewModel, long>> groups = null;
            Dictionary<long, long> newGroups = null;

            for (int i = 0; i < slice.Count; i++)
            {
                var message = slice[i];
                if (message.MediaAlbumId == 0)
                {
                    continue;
                }

                var groupedId = message.MediaAlbumId;

                _groupedMessages.TryGetValue(groupedId, out MessageViewModel group);

                if (group == null)
                {
                    var media = new MessageAlbum(message.Content is MessagePhoto or MessageVideo);

                    var groupBase = new Message();
                    groupBase.Content = media;
                    groupBase.Date = message.Date;

                    group = CreateMessage(groupBase);

                    slice[i] = group;
                    newGroups ??= new();
                    newGroups[groupedId] = groupedId;
                    _groupedMessages[groupedId] = group;
                }
                else
                {
                    slice.RemoveAt(i);
                    i--;
                }

                if (group.Content is MessageAlbum album)
                {
                    groups ??= new();
                    groups[groupedId] = Tuple.Create(group, group.Id);

                    album.Messages.Add(message);

                    var first = album.Messages.FirstOrDefault();
                    if (first != null)
                    {
                        group.UpdateWith(first);
                    }
                }
            }

            if (groups != null)
            {
                foreach (var group in groups.Values)
                {
                    if (group.Item1.Content is MessageAlbum album)
                    {
                        album.Invalidate();
                    }

                    if (newGroups != null && newGroups.ContainsKey(group.Item1.MediaAlbumId))
                    {
                        continue;
                    }

                    Handle(new UpdateMessageContent(chat.Id, group.Item2, group.Item1.Content));
                    Handle(new UpdateMessageEdited(chat.Id, group.Item2, group.Item1.EditDate, group.Item1.ReplyMarkup));
                    Handle(new UpdateMessageInteractionInfo(chat.Id, group.Item2, group.Item1.InteractionInfo));
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessReplies(Chat chat, MessageViewModel message)
        {
            if (message.ReplyTo is MessageReplyToMessage replyToMessage)
            {
                if (_thread?.ChatId == replyToMessage.ChatId &&
                    _thread?.Messages[^1].Id == replyToMessage.MessageId)
                {
                    message.ReplyToState = MessageReplyToState.Hidden;
                    return;
                }
                else if (replyToMessage.Origin != null)
                {
                    message.ReplyToState = MessageReplyToState.None;
                    message.ReplyToItem = replyToMessage;
                    return;
                }
            }

            if (message.ReplyTo is not null ||
                message.Content is MessagePinMessage ||
                message.Content is MessageGameScore ||
                message.Content is MessagePaymentSuccessful)
            {
                message.ReplyToState = MessageReplyToState.Loading;

                ClientService.GetReplyTo(message, response =>
                {
                    if (response is Message result)
                    {
                        message.ReplyToItem = CreateMessage(result);
                        message.ReplyToState = MessageReplyToState.None;
                    }
                    else if (response is Story story)
                    {
                        message.ReplyToItem = story;
                        message.ReplyToState = MessageReplyToState.None;
                    }
                    else
                    {
                        message.ReplyToState = MessageReplyToState.Deleted;
                    }

                    BeginOnUIThread(() => Handle(message,
                        bubble => bubble.UpdateMessageReply(message),
                        service => service.UpdateMessage(message)));
                });
            }
            else if (message.Content is MessageAsyncStory asyncStory)
            {
                asyncStory.State = MessageStoryState.Loading;

                ClientService.GetStory(asyncStory.StorySenderChatId, asyncStory.StoryId, response =>
                {
                    if (response is Story story)
                    {
                        asyncStory.Story = story;
                        asyncStory.State = MessageStoryState.None;

                        if (story.Content is StoryContentPhoto photo)
                        {
                            message.GeneratedContent = new MessagePhoto(photo.Photo, null, false, false);
                        }
                        else if (story.Content is StoryContentVideo video)
                        {
                            message.GeneratedContent = new MessageVideo(new Video((int)video.Video.Duration, video.Video.Width, video.Video.Height, "video.mp4", "video/mp4", video.Video.HasStickers, true, video.Video.Minithumbnail, video.Video.Thumbnail, video.Video.Video), null, false, false);
                        }
                    }
                    else
                    {
                        asyncStory.State = MessageStoryState.Expired;
                    }

                    BeginOnUIThread(() => Handle(message,
                        bubble => { bubble.UpdateMessageHeader(message); bubble.UpdateMessageContent(message); },
                        service => service.UpdateMessage(message)));
                });
            }
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is ChatNavigationArgs args)
            {
                Topic = await ClientService.SendAsync(new GetForumTopic(args.ChatId, args.MessageId)) as ForumTopic;
                Thread = await ClientService.SendAsync(new GetMessageThread(args.ChatId, args.MessageId)) as MessageThreadInfo;

                if (Topic != null)
                {
                    parameter = args.ChatId;
                }
                else if (Thread != null)
                {
                    parameter = Thread.ChatId;
                }
                else
                {
                    return;
                }
            }

            var chat = ClientService.GetChat((long)parameter);
            chat ??= await ClientService.SendAsync(new GetChat((long)parameter)) as Chat;

            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSecret || chat.HasProtectedContent)
            {
                WindowContext.Current.DisableScreenCapture(GetHashCode());
            }

            Chat = chat;
            SetScrollMode(ItemsUpdatingScrollMode.KeepLastItemInView, true);
            SetTranslating();

            if (state.TryGet("access_token", out string accessToken))
            {
                state.Remove("access_token");
                AccessToken = accessToken;
            }

            if (state.TryGet("search", out string search))
            {
                state.Remove("search");
                SearchExecute(search);
            }

#pragma warning disable CS4014
            if (_type == DialogType.ScheduledMessages)
            {
                Logger.Debug(string.Format("{0} - Loadings scheduled messages", chat.Id));

                NotifyMessageSliceLoaded();
                LoadScheduledSliceAsync();
            }
            else if (_type == DialogType.EventLog)
            {
                Logger.Debug(string.Format("{0} - Loadings event log", chat.Id));

                NotifyMessageSliceLoaded();
                LoadEventLogSliceAsync();
            }
            else if (state.TryGet("message_id", out long navigation))
            {
                Logger.Debug(string.Format("{0} - Loading messages from specific id", chat.Id));

                state.Remove("message_id");
                LoadMessageSliceAsync(null, navigation);
            }
            else
            {
                bool TryRemove(long chatId, out long v1, out long v2)
                {
                    var a = Settings.Chats.TryRemove(chat.Id, ThreadId, ChatSetting.ReadInboxMaxId, out v1);
                    var b = Settings.Chats.TryRemove(chat.Id, ThreadId, ChatSetting.Index, out v2);
                    return a && b;
                }

                long lastReadMessageId;
                long lastMessageId;

                if (_topic is ForumTopic topic)
                {
                    lastReadMessageId = topic.LastReadInboxMessageId;
                    lastMessageId = topic.LastMessage?.Id ?? long.MaxValue;
                }
                else if (_thread is MessageThreadInfo thread)
                {
                    lastReadMessageId = thread.ReplyInfo?.LastReadInboxMessageId ?? long.MaxValue;
                    lastMessageId = thread.ReplyInfo?.LastMessageId ?? long.MaxValue;
                }
                else
                {
                    lastReadMessageId = chat.LastReadInboxMessageId;
                    lastMessageId = chat.LastMessage?.Id ?? long.MaxValue;
                }

                // TODO: verify this is valid in all cases
                if (lastReadMessageId == 0 && _thread == null)
                {
                    lastReadMessageId = lastMessageId;
                }

                if (TryRemove(chat.Id, out long readInboxMaxId, out long start) &&
                    readInboxMaxId == lastReadMessageId &&
                    start <= lastReadMessageId)
                {
                    if (Settings.Chats.TryRemove(chat.Id, ThreadId, ChatSetting.Pixel, out double pixel))
                    {
                        Logger.Debug(string.Format("{0} - Loading messages from specific pixel", chat.Id));
                        LoadMessageSliceAsync(null, start, VerticalAlignment.Bottom, pixel);
                    }
                    else
                    {
                        Logger.Debug(string.Format("{0} - Loading messages from specific id, pixel missing", chat.Id));
                        LoadMessageSliceAsync(null, start, VerticalAlignment.Bottom);
                    }
                }
                else /*if (chat.UnreadCount > 0)*/
                {
                    Logger.Debug(string.Format("{0} - Loading messages from LastReadInboxMessageId: {1}", chat.Id, chat.LastReadInboxMessageId));
                    LoadMessageSliceAsync(null, lastReadMessageId, VerticalAlignment.Top);
                }
            }
#pragma warning restore CS4014

#if MOCKUP
            int TodayDate(int hour, int minute)
            {
                var dateTime = DateTime.Now.Date.AddHours(hour).AddMinutes(minute);

                var dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                DateTime.SpecifyKind(dtDateTime, DateTimeKind.Utc);

                return (int)(dateTime.ToUniversalTime() - dtDateTime).TotalSeconds;
            }

            if (chat.Id == 10)
            {
                Items.Add(CreateMessage(new Message(0, new MessageSenderUser(0), chat.Id, null, null, true,  false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(14, 58), 0, null, null, null, 0, 0,  0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("Hey Eileen", Array.Empty<TextEntity>()), null), null)));
                Items.Add(CreateMessage(new Message(1, new MessageSenderUser(0), chat.Id, null, null, true,  false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(14, 59), 0, null, null, null, 0, 0,  0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("So, why is Telegram cool?", Array.Empty<TextEntity>()), null), null)));
                Items.Add(CreateMessage(new Message(2, new MessageSenderUser(7), chat.Id, null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(14, 59), 0, null, null, null, 0, 0,  0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("Well, look. Telegram is superfast and you can use it on all your devices at the same time - phones, tablets, even desktops.", Array.Empty<TextEntity>()), null), null)));
                Items.Add(CreateMessage(new Message(3, new MessageSenderUser(0), chat.Id, null, null, true,  false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(14, 59), 0, null, null, null, 0, 0,  0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageBigEmoji(new FormattedText("😴", Array.Empty<TextEntity>()), 1), null)));
                Items.Add(CreateMessage(new Message(4, new MessageSenderUser(7), chat.Id, null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(15, 00), 0, null, null, null, 0, 0,  0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("And it has secret chats, like this one, with end-to-end encryption!", Array.Empty<TextEntity>()), null), null)));
                Items.Add(CreateMessage(new Message(5, new MessageSenderUser(0), chat.Id, null, null, true,  false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(15, 00), 0, null, null, null, 0, 0,  0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("End encryption to what end??", Array.Empty<TextEntity>()), null), null)));
                Items.Add(CreateMessage(new Message(6, new MessageSenderUser(7), chat.Id, null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(15, 01), 0, null, null, null, 0, 0,  0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("Arrgh. Forget it. You can set a timer and send photos that will disappear when the time rush out. Yay!", Array.Empty<TextEntity>()), null), null)));
                Items.Add(CreateMessage(new Message(7, new MessageSenderUser(7), chat.Id, null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(15, 01), 0, null, null, null, 0, 0,  0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageChatSetTtl(15), null)));
                Items.Add(CreateMessage(new Message(8, new MessageSenderUser(0), chat.Id, null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(15, 05), 0, null, null, null, 0, 0, 0, 15, 0, 0, string.Empty, 0, string.Empty, new MessagePhoto(new Photo(false, null, new[] { new PhotoSize("t", new File(0, 0, 0, new LocalFile(System.IO.Path.Combine(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Assets\\Mockup\\hot.png"), true, true, false, true, 0, 0, 0), new RemoteFile()), 800, 500, null), new PhotoSize("i", new File(0, 0, 0, new LocalFile(System.IO.Path.Combine(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Assets\\Mockup\\hot.png"), true, true, false, true, 0, 0, 0), new RemoteFile()), 800, 500, null) }), new FormattedText(string.Empty, Array.Empty<TextEntity>()), true), null)));

                SetText("😱🙈👍");
            }
            else
            {
                Items.Add(CreateMessage(new Message(4, new MessageSenderUser(11), chat.Id, null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(15, 25), 0, null, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageSticker(new Sticker(0, 512, 512, "", new StickerTypeStatic(), null, null, null, new File(0, 0, 0, new LocalFile(System.IO.Path.Combine(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Assets\\Mockup\\sticker0.webp"), true, true, false, true, 0, 0, 0), null)), false), null)));
                Items.Add(CreateMessage(new Message(1, new MessageSenderUser(9),  chat.Id, null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(15, 26), 0, null, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("Are you sure it's safe here?", Array.Empty<TextEntity>()), null), null)));
                Items.Add(CreateMessage(new Message(2, new MessageSenderUser(7),  chat.Id, null, null, true,  false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(15, 27), 0, null, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("Yes, sure, don't worry.", Array.Empty<TextEntity>()), null), null)));
                Items.Add(CreateMessage(new Message(3, new MessageSenderUser(13), chat.Id, null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(15, 27), 0, null, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("Hallo alle zusammen! Is the NSA reading this? 😀", Array.Empty<TextEntity>()), null), null)));
                Items.Add(CreateMessage(new Message(4, new MessageSenderUser(9),  chat.Id, null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(15, 29), 0, null, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageSticker(new Sticker(0, 512, 512, "", new StickerTypeStatic(), null, null, null, new File(0, 0, 0, new LocalFile(System.IO.Path.Combine(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Assets\\Mockup\\sticker1.webp"), true, true, false, true, 0, 0, 0), null)), false), null)));
                Items.Add(CreateMessage(new Message(5, new MessageSenderUser(10), chat.Id, null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(15, 29), 0, null, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("Sorry, I'll have to publish this conversation on the web.", Array.Empty<TextEntity>()), null), null)));
                Items.Add(CreateMessage(new Message(6, new MessageSenderUser(10), chat.Id, null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(15, 01), 0, null, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageChatDeleteMember(10), null)));
                Items.Add(CreateMessage(new Message(7, new MessageSenderUser(8),  chat.Id, null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(15, 30), 0, null, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("Wait, we could have made so much money on this!", Array.Empty<TextEntity>()), null), null)));
            }
#endif

            if (chat.IsMarkedAsUnread)
            {
                ClientService.Send(new ToggleChatIsMarkedAsUnread(chat.Id, false));
            }

            ClientService.Send(new OpenChat(chat.Id));

            Delegate?.UpdateChat(chat);
            Delegate?.UpdateChatActions(chat, ClientService.GetChatActions(chat.Id));

            if (chat.Type is ChatTypePrivate privata)
            {
                var item = ClientService.GetUser(privata.UserId);
                var cache = ClientService.GetUserFull(privata.UserId);

                _stickers?.UpdateSupergroupFullInfo(chat, null, null);
                Delegate?.UpdateUser(chat, item, false);

                if (cache != null)
                {
                    Delegate?.UpdateUserFullInfo(chat, item, cache, false, _accessToken != null);
                }

                ClientService.Send(new GetUserFullInfo(privata.UserId));
            }
            else if (chat.Type is ChatTypeSecret secretType)
            {
                var secret = ClientService.GetSecretChat(secretType.SecretChatId);
                var item = ClientService.GetUser(secretType.UserId);
                var cache = ClientService.GetUserFull(secretType.UserId);

                _stickers?.UpdateSupergroupFullInfo(chat, null, null);
                Delegate?.UpdateSecretChat(chat, secret);
                Delegate?.UpdateUser(chat, item, true);

                if (cache != null)
                {
                    Delegate?.UpdateUserFullInfo(chat, item, cache, true, false);
                }

                ClientService.Send(new GetUserFullInfo(secret.UserId));
            }
            else if (chat.Type is ChatTypeBasicGroup basic)
            {
                var item = ClientService.GetBasicGroup(basic.BasicGroupId);
                var cache = ClientService.GetBasicGroupFull(basic.BasicGroupId);

                _stickers?.UpdateSupergroupFullInfo(chat, null, null);
                Delegate?.UpdateBasicGroup(chat, item);

                if (cache != null)
                {
                    Delegate?.UpdateBasicGroupFullInfo(chat, item, cache);
                }

                ClientService.Send(new GetBasicGroupFullInfo(basic.BasicGroupId));
                _messageDelegate.UpdateAdministrators(chat.Id);
            }
            else if (chat.Type is ChatTypeSupergroup super)
            {
                var item = ClientService.GetSupergroup(super.SupergroupId);
                var cache = ClientService.GetSupergroupFull(super.SupergroupId);

                Delegate?.UpdateSupergroup(chat, item);

                if (cache != null)
                {
                    _stickers?.UpdateSupergroupFullInfo(chat, item, cache);
                    Delegate?.UpdateSupergroupFullInfo(chat, item, cache);
                }

                ClientService.Send(new GetSupergroupFullInfo(super.SupergroupId));
                _messageDelegate.UpdateAdministrators(chat.Id);
            }

            UpdateGroupCall(chat, chat.VideoChat.GroupCallId);

            ShowReplyMarkup(chat);
            ShowDraftMessage(chat);
            ShowSwitchInline(state);
            ShowReplyTo(state);

            if (_type == DialogType.History && App.DataPackages.TryRemove(chat.Id, out DataPackageView package))
            {
                await HandlePackageAsync(package);
            }
            else if (_type == DialogType.History && state.TryGet("package", out DataPackageView packageView))
            {
                state.Remove("package");
                await HandlePackageAsync(packageView);
            }
        }

        protected override void OnNavigatedFrom(NavigationState suspensionState, bool suspending)
        {
            // Explicit unsubscribe because NavigatedFrom
            // is not invoked when switching between chats.
            Aggregator.Unsubscribe(this);
            WindowContext.Current.EnableScreenCapture(GetHashCode());

            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            _groupedMessages.Clear();
            _hasLoadedLastPinnedMessage = false;
            _chatActionManager = null;

            SelectedItems.Clear();

            PinnedMessages.Clear();
            LastPinnedMessage = null;
            LockedPinnedMessageId = 0;

            DisposeSearch();
            IsSelectionEnabled = false;

            IsLastSliceLoaded = null;
            IsFirstSliceLoaded = null;

            ClientService.Send(new CloseChat(chat.Id));

            void Remove(string reason)
            {
                Settings.Chats.TryRemove(chat.Id, ThreadId, ChatSetting.ReadInboxMaxId, out long _);
                Settings.Chats.TryRemove(chat.Id, ThreadId, ChatSetting.Index, out long _);
                Settings.Chats.TryRemove(chat.Id, ThreadId, ChatSetting.Pixel, out double _);

                Logger.Debug(string.Format("{0} - Removing scrolling position, {1}", chat.Id, reason));
            }

            try
            {
                var field = HistoryField;
                if (field != null && TryGetLastVisibleMessageId(out long lastVisibleId, out int lastVisibleIndex))
                {
                    var firstNonVisibleId = lastVisibleIndex < Items.Count - 2
                        ? Items[lastVisibleIndex + 1].Id
                        : lastVisibleId;

                    if (lastVisibleId != 0 && lastVisibleId != chat.LastMessage?.Id)
                    {
                        long lastReadMessageId;

                        if (_topic is ForumTopic topic)
                        {
                            lastReadMessageId = topic.LastReadInboxMessageId;
                        }
                        else if (_thread is MessageThreadInfo thread)
                        {
                            lastReadMessageId = thread.ReplyInfo.LastReadInboxMessageId;
                        }
                        else
                        {
                            lastReadMessageId = chat.LastReadInboxMessageId;
                        }

                        if (firstNonVisibleId < lastReadMessageId)
                        {
                            Settings.Chats[chat.Id, ThreadId, ChatSetting.ReadInboxMaxId] = lastReadMessageId;
                            Settings.Chats[chat.Id, ThreadId, ChatSetting.Index] = lastVisibleId;

                            var container = field.ContainerFromIndex(lastVisibleIndex) as ListViewItem;
                            if (container != null)
                            {
                                var transform = container.TransformToVisual(field);
                                var position = transform.TransformPoint(new Point());

                                Settings.Chats[chat.Id, ThreadId, ChatSetting.Pixel] = field.ActualHeight - (position.Y + container.ActualHeight);
                                Logger.Debug(string.Format("{0} - Saving scrolling position, message: {1}, pixel: {2}", chat.Id, lastVisibleId, field.ActualHeight - (position.Y + container.ActualHeight)));
                            }
                            else
                            {
                                Settings.Chats.TryRemove(chat.Id, ThreadId, ChatSetting.Pixel, out double pixel);
                                Logger.Debug(string.Format("{0} - Saving scrolling position, message: {1}, pixel: none", chat.Id, lastVisibleId));
                            }
                        }
                        else
                        {
                            Remove("as first non visible item is unread");
                        }
                    }
                    else
                    {
                        Remove("as last item is chat.LastMessage");
                    }
                }
                else
                {
                    Remove("generic reason");
                }
            }
            catch
            {
                Remove("exception");
            }

            SaveDraft();
        }

        private void ShowSwitchInline(IDictionary<string, object> state)
        {
            if (_type == DialogType.History && state.TryGet("switch_query", out string query) && state.TryGet("switch_bot", out long userId))
            {
                state.Remove("switch_query");
                state.Remove("switch_bot");

                var bot = ClientService.GetUser(userId);
                if (bot == null || !bot.HasActiveUsername(out string username))
                {
                    return;
                }

                SetText(string.Format("@{0} {1}", username, query), focus: true);
                ResolveInlineBot(username, query);
            }
        }

        private void ShowReplyTo(IDictionary<string, object> state)
        {
            if (_type == DialogType.History && state.TryGet("reply_to", out Message message))
            {
                state.TryGet("reply_to_quote", out InputTextQuote quote);

                state.Remove("reply_to");
                state.Remove("reply_to_quote");

                ComposerHeader = new MessageComposerHeader(ClientService)
                {
                    ReplyToMessage = CreateMessage(message),
                    ReplyToQuote = quote
                };

                TextField?.Focus(FocusState.Keyboard);
            }
        }

        private async void ShowReplyMarkup(Chat chat)
        {
            if (chat.ReplyMarkupMessageId == 0 || _type != DialogType.History)
            {
                Delegate?.UpdateChatReplyMarkup(chat, null);
            }
            else
            {
                var response = await ClientService.SendAsync(new GetMessage(chat.Id, chat.ReplyMarkupMessageId));
                if (response is Message message)
                {
                    Delegate?.UpdateChatReplyMarkup(chat, CreateMessage(message));
                }
                else
                {
                    Delegate?.UpdateChatReplyMarkup(chat, null);
                }
            }
        }

        public void ShowDraft()
        {
            var chat = _chat;
            if (chat != null)
            {
                ShowDraftMessage(chat);
            }
        }

        private DraftMessage _draft;

        private async void ShowDraftMessage(Chat chat, bool force = true)
        {
            DraftMessage draft;

            var thread = _thread;
            if (thread != null)
            {
                draft = thread.DraftMessage;
            }
            else
            {
                draft = chat.DraftMessage;
            }

            if (!force)
            {
                var current = GetFormattedText();

                var prev = _draft?.InputMessageText as InputMessageText;
                var next = draft?.InputMessageText as InputMessageText;

                if (prev != null && !prev.Text.AreTheSame(current))
                {
                    return;
                }
                else if (next != null && prev != null && next.Text.AreTheSame(prev.Text))
                {
                    return;
                }
                else if (next != null && next.Text.AreTheSame(current))
                {
                    return;
                }
            }

            var input = draft?.InputMessageText as InputMessageText;
            if (input == null || (_type != DialogType.History && _type != DialogType.Thread))
            {
                _draft = null;

                SetText(null as string);
                ComposerHeader = null;
            }
            else
            {
                _draft = draft;

                if (draft.ReplyTo is InputMessageReplyToMessage replyToMessage)
                {
                    var response = await ClientService.SendAsync(new GetMessage(chat.Id, replyToMessage.MessageId));
                    if (response is Message message)
                    {
                        ComposerHeader = new MessageComposerHeader(ClientService)
                        {
                            ReplyToMessage = CreateMessage(message),
                            ReplyToQuote = replyToMessage.Quote
                        };
                    }
                    else
                    {
                        ComposerHeader = null;
                    }
                }
                else
                {
                    ComposerHeader = null;
                }

                if (draft.InputMessageText is InputMessageText text)
                {
                    SetText(text.Text);
                }
            }
        }

        private IAutocompleteCollection _autocomplete;
        public IAutocompleteCollection Autocomplete
        {
            get => _autocomplete;
            set
            {
                Set(ref _autocomplete, value);
                Delegate?.UpdateAutocomplete(_chat, value);
            }
        }

        private bool _hasBotCommands;
        public bool HasBotCommands
        {
            get => _hasBotCommands;
            set => Set(ref _hasBotCommands, value);
        }

        private List<UserCommand> _botCommands;
        public List<UserCommand> BotCommands
        {
            get => _botCommands;
            set => Set(ref _botCommands, value);
        }

        public void SaveDraft()
        {
            SaveDraft(false);
        }

        public void SaveDraft(bool clear = false)
        {
            if (_type != DialogType.History && _type != DialogType.Thread)
            {
                return;
            }

            if (_isSelectionEnabled && !clear)
            {
                return;
            }

            var embedded = _composerHeader;
            if (embedded != null && embedded.EditingMessage != null)
            {
                return;
            }

            var chat = Chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup super && super.IsChannel)
            {
                var supergroup = ClientService.GetSupergroup(super.SupergroupId);
                if (supergroup != null && !supergroup.CanPostMessages())
                {
                    return;
                }
            }

            var formattedText = GetFormattedText(clear);
            if (formattedText == null)
            {
                return;
            }

            var replyToMessageId = 0L;
            var replyToChatId = 0L;
            var quote = default(InputTextQuote);

            if (embedded != null && embedded.ReplyToMessage != null)
            {
                replyToMessageId = embedded.ReplyToMessage.Id;
                replyToChatId = embedded.ReplyToMessage.ChatId;
                quote = embedded.ReplyToQuote;

                if (replyToChatId == chat.Id)
                {
                    replyToChatId = 0;
                }
            }

            DraftMessage draft = null;
            if (!string.IsNullOrWhiteSpace(formattedText.Text) || replyToMessageId != 0)
            {
                if (formattedText.Text.Length > ClientService.Options.MessageTextLengthMax * 4)
                {
                    formattedText = formattedText.Substring(0, ClientService.Options.MessageTextLengthMax * 4);
                }

                var inputReply = replyToMessageId != 0
                    ? new InputMessageReplyToMessage(replyToChatId, replyToMessageId, quote)
                    : null;

                draft = new DraftMessage(inputReply, 0, new InputMessageText(formattedText, null, false));
            }

            ClientService.Send(new SetChatDraftMessage(_chat.Id, ThreadId, draft));
        }

        #region Reply 

        private MessageComposerHeader _composerHeader;
        public MessageComposerHeader ComposerHeader
        {
            get => _composerHeader;
            set
            {
                Set(ref _composerHeader, value);
                Delegate?.UpdateComposerHeader(_chat, value);
            }
        }

        public bool DisableWebPagePreview
        {
            get
            {
                // Force disable if needed

                var chat = _chat;
                if (chat?.Type is ChatTypeSecret && !Settings.IsSecretPreviewsEnabled)
                {
                    return true;
                }

                return false;
            }
        }

        public void ClearReply()
        {
            var container = _composerHeader;
            if (container == null)
            {
                return;
            }

            if (container.WebPagePreview != null)
            {
                ComposerHeader = new MessageComposerHeader(ClientService)
                {
                    EditingMessage = container.EditingMessage,
                    ReplyToMessage = container.ReplyToMessage,
                    ReplyToQuote = container.ReplyToQuote,
                    WebPageUrl = container.WebPageUrl,
                    WebPagePreview = null,
                    WebPageDisabled = true
                };
            }
            else
            {
                if (container.EditingMessage != null)
                {
                    var chat = _chat;
                    if (chat != null)
                    {
                        ShowDraftMessage(chat);
                    }
                    else
                    {
                        ComposerHeader = null;
                        SetText(null, false);
                    }
                }
                else
                {
                    ComposerHeader = null;
                }
            }

            TextField?.Focus(FocusState.Programmatic);
        }

        protected override InputMessageReplyTo GetReply(bool clean, bool notify = true)
        {
            var embedded = _composerHeader;
            if (embedded == null || embedded.ReplyToMessage == null)
            {
                return null;
            }

            if (clean)
            {
                if (notify)
                {
                    ComposerHeader = null;
                }
                else
                {
                    _composerHeader = null;
                }
            }

            var chatId = embedded.ReplyToMessage.ChatId;
            if (chatId == _chat?.Id)
            {
                chatId = 0;
            }

            return new InputMessageReplyToMessage(chatId, embedded.ReplyToMessage.Id, embedded.ReplyToQuote);
        }

        #endregion

        public async void SendMessage(string args)
        {
            await SendMessageAsync(args);
        }

        protected override LinkPreviewOptions DisableWebPreview()
        {
            var header = _composerHeader;
            return header?.LinkPreviewOptions;
        }

        protected override async Task<bool> BeforeSendMessageAsync(FormattedText formattedText)
        {
            if (Chat is not Chat chat)
            {
                return false;
            }

            var header = _composerHeader;
            var disablePreview = DisableWebPreview();

            if (header?.EditingMessage == null)
            {
                return false;
            }

            var editing = header.EditingMessage;

            var factory = header.EditingMessageMedia;
            if (factory != null)
            {
                var input = factory.Delegate(factory.InputFile, header.EditingMessageCaption);

                var response = await ClientService.SendAsync(new SendMessageAlbum(editing.ChatId, editing.MessageThreadId, null, Constants.PreviewOnly, new[] { input }));
                if (response is Messages messages && messages.MessagesValue.Count == 1)
                {
                    _contentOverrides[editing.CombinedId] = messages.MessagesValue[0].Content;
                    Aggregator.Publish(new UpdateMessageContent(editing.ChatId, editing.Id, messages.MessagesValue[0].Content));

                    ComposerHeader = null;
                    ClientService.Send(new EditMessageMedia(editing.ChatId, editing.Id, null, input));
                }
            }
            else
            {
                Function function;
                if (editing.Content is MessageText or MessageAnimatedEmoji or MessageBigEmoji)
                {
                    function = new EditMessageText(chat.Id, editing.Id, null, new InputMessageText(formattedText, disablePreview, true));
                }
                else
                {
                    function = new EditMessageCaption(chat.Id, editing.Id, null, formattedText);
                }

                var response = await ClientService.SendAsync(function);
                if (response is Message message)
                {
                    ShowDraftMessage(chat);
                    Aggregator.Publish(new UpdateMessageSendSucceeded(message, editing.Id));
                }
                else if (response is Error error)
                {
                    if (error.MessageEquals(ErrorType.MESSAGE_NOT_MODIFIED))
                    {
                        ShowDraftMessage(chat);
                    }
                    else
                    {
                        // TODO: ...
                    }
                }
            }

            return true;
        }

        protected override Task AfterSendMessageAsync()
        {
            return LoadLastSliceAsync();
        }

        #region Set default message sender

        public async void SetSender(ChatMessageSender messageSender)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (messageSender.NeedsPremium && !IsPremium)
            {
                await ShowPopupAsync(Strings.SelectSendAsPeerPremiumHint, Strings.AppName, Strings.OK);
                return;
            }

            ClientService.Send(new SetChatMessageSender(chat.Id, messageSender.Sender));
        }

        #endregion

        #region Gift premium

        public async void GiftPremium()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (ClientService.TryGetUser(chat, out User user)
                && ClientService.TryGetUserFull(chat, out UserFullInfo userFull))
            {
                await ShowPopupAsync(new GiftPopup(ClientService, NavigationService, user, userFull.PremiumGiftOptions));
            }
        }

        #endregion

        #region Join channel

        public async void JoinChannel()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var response = await ClientService.SendAsync(new JoinChat(chat.Id));
            if (response is Error error)
            {
                if (error.MessageEquals(ErrorType.INVITE_REQUEST_SENT))
                {
                    await MessagePopup.ShowAsync(chat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel ? Strings.RequestToJoinChannelSentDescription : Strings.RequestToJoinGroupSentDescription, Strings.RequestToJoinSent, Strings.OK);
                    return;

                    var message = Strings.RequestToJoinSent + Environment.NewLine + (chat.Type is ChatTypeSupergroup supergroup2 && supergroup2.IsChannel ? Strings.RequestToJoinChannelSentDescription : Strings.RequestToJoinGroupSentDescription);
                    var entity = new TextEntity(0, Strings.RequestToJoinSent.Length, new TextEntityTypeBold());

                    var text = new FormattedText(message, new[] { entity });

                    Window.Current.ShowToast(text, new LocalFileSource("ms-appx:///Assets/Toasts/JoinRequested.tgs"));
                }
            }
        }

        #endregion

        #region Toggle mute

        public void Mute()
        {
            ToggleMute(false);
        }

        public void Unmute()
        {
            ToggleMute(true);
        }

        public void ToggleMute()
        {
            ToggleMute(ClientService.Notifications.GetMutedFor(_chat) > 0);
        }

        private void ToggleMute(bool unmute)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            _notificationsService.SetMuteFor(chat, unmute ? 0 : 632053052);
        }

        #endregion

        #region Toggle silent

        public void ToggleSilent()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            ClientService.Send(new ToggleChatDefaultDisableNotification(chat.Id, !chat.DefaultDisableNotification));
        }

        #endregion

        #region Report Spam

        public void RemoveActionBar()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            ClientService.Send(new RemoveChatActionBar(chat.Id));
        }

        public async void ReportSpam(ReportReason reason)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var title = Strings.AppName;
            var message = Strings.ReportSpamAlert;

            if (reason is ReportReasonUnrelatedLocation)
            {
                title = Strings.ReportUnrelatedGroup;

                var fullInfo = ClientService.GetSupergroupFull(chat);
                if (fullInfo != null && fullInfo.Location.Address.Length > 0)
                {
                    message = string.Format(Strings.ReportUnrelatedGroupText, fullInfo.Location.Address);
                }
                else
                {
                    message = Strings.ReportUnrelatedGroupTextNoAddress;
                }
            }
            else if (reason is ReportReasonSpam)
            {
                if (chat.Type is ChatTypeSupergroup supergroup)
                {
                    message = supergroup.IsChannel ? Strings.ReportSpamAlertChannel : Strings.ReportSpamAlertGroup;
                }
                else if (chat.Type is ChatTypeBasicGroup)
                {
                    message = Strings.ReportSpamAlertGroup;
                }
            }

            var confirm = await ShowPopupAsync(message, title, Strings.OK, Strings.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {

                return;
            }

            ClientService.Send(new ReportChat(chat.Id, Array.Empty<long>(), reason, string.Empty));

            if (chat.Type is ChatTypeBasicGroup or ChatTypeSupergroup)
            {
                ClientService.Send(new LeaveChat(chat.Id));
            }
            else if (chat.Type is ChatTypePrivate privata)
            {
                ClientService.Send(new SetMessageSenderBlockList(new MessageSenderUser(privata.UserId), new BlockListMain()));
            }
            else if (chat.Type is ChatTypeSecret secret)
            {
                ClientService.Send(new SetMessageSenderBlockList(new MessageSenderUser(secret.UserId), new BlockListMain()));
            }

            ClientService.Send(new DeleteChatHistory(chat.Id, true, false));
        }

        #endregion

        #region Delete and Exit

        public async void DeleteChat()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var updated = await ClientService.SendAsync(new GetChat(chat.Id)) as Chat ?? chat;
            var dialog = new DeleteChatPopup(ClientService, updated, null, false);

            var confirm = await ShowPopupAsync(dialog);
            if (confirm == ContentDialogResult.Primary)
            {
                var check = dialog.IsChecked == true;

                if (updated.Type is ChatTypeSecret secret)
                {
                    await ClientService.SendAsync(new CloseSecretChat(secret.SecretChatId));
                }
                else if (updated.Type is ChatTypeBasicGroup or ChatTypeSupergroup)
                {
                    await ClientService.SendAsync(new LeaveChat(updated.Id));
                }

                var user = ClientService.GetUser(updated);
                if (user != null && user.Type is UserTypeRegular)
                {
                    ClientService.Send(new DeleteChatHistory(updated.Id, true, check));
                }
                else
                {
                    if (updated.Type is ChatTypePrivate privata && check)
                    {
                        await ClientService.SendAsync(new SetMessageSenderBlockList(new MessageSenderUser(privata.UserId), new BlockListMain()));
                    }

                    ClientService.Send(new DeleteChatHistory(updated.Id, true, false));
                }
            }
        }

        #endregion

        #region Clear history

        public async void ClearHistory()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var updated = await ClientService.SendAsync(new GetChat(chat.Id)) as Chat ?? chat;
            var dialog = new DeleteChatPopup(ClientService, updated, null, true);

            var confirm = await ShowPopupAsync(dialog);
            if (confirm == ContentDialogResult.Primary)
            {
                ClientService.Send(new DeleteChatHistory(updated.Id, false, dialog.IsChecked));
            }
        }

        #endregion

        #region Call

        public void VoiceCall()
        {
            Call(false);
        }

        public void VideoCall()
        {
            Call(true);
        }

        public async void Call(bool video)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypePrivate or ChatTypeSecret)
            {
                _voipService.Start(chat.Id, video);
            }
            else
            {
                await _voipGroupService.JoinAsync(chat.Id);
            }
        }

        #endregion

        #region Unpin message

        public async void HidePinnedMessage()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var message = PinnedMessages.LastOrDefault();
            if (message == null || PinnedMessages.Count > 1)
            {
                return;
            }

            var supergroupType = chat.Type as ChatTypeSupergroup;
            var basicGroupType = chat.Type as ChatTypeBasicGroup;

            var supergroup = supergroupType != null ? ClientService.GetSupergroup(supergroupType.SupergroupId) : null;
            var basicGroup = basicGroupType != null ? ClientService.GetBasicGroup(basicGroupType.BasicGroupId) : null;

            if (supergroup != null && supergroup.CanPinMessages() ||
                basicGroup != null && basicGroup.CanPinMessages() ||
                chat.Type is ChatTypePrivate privata)
            {
                var confirm = await ShowPopupAsync(Strings.UnpinMessageAlert, Strings.AppName, Strings.OK, Strings.Cancel);
                if (confirm == ContentDialogResult.Primary)
                {
                    ClientService.Send(new UnpinChatMessage(chat.Id, message.Id));
                    Delegate?.UpdatePinnedMessage(chat, false);
                }
            }
            else
            {
                Settings.SetChatPinnedMessage(chat.Id, message.Id);
                Delegate?.UpdatePinnedMessage(chat, false);
            }
        }

        public void ShowPinnedMessage()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            Settings.SetChatPinnedMessage(chat.Id, 0);
            LoadPinnedMessagesSliceAsync(0);
        }

        public void OpenPinnedMessages()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(ChatPinnedPage), chat.Id);
        }

        #endregion

        #region Unblock

        public async void Unblock()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var privata = chat.Type as ChatTypePrivate;
            if (privata == null)
            {
                return;
            }

            var user = ClientService.GetUser(privata.UserId);
            if (user.Type is UserTypeBot)
            {
                await ClientService.SendAsync(new SetMessageSenderBlockList(new MessageSenderUser(user.Id), null));
                Start();
            }
            else
            {
                var confirm = await ShowPopupAsync(Strings.AreYouSureUnblockContact, Strings.AppName, Strings.OK, Strings.Cancel);
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }

                ClientService.Send(new SetMessageSenderBlockList(new MessageSenderUser(user.Id), null));
            }
        }

        #endregion

        #region Switch

        public async void ActivateInlineBot()
        {
            if (InlineBotResults?.Button == null || _currentInlineBot == null)
            {
                return;
            }

            if (InlineBotResults.Button.Type is InlineQueryResultsButtonTypeStartBot startBot)
            {
                var response = await ClientService.SendAsync(new CreatePrivateChat(_currentInlineBot.Id, false));
                if (response is Chat chat)
                {
                    SetText(null, false);

                    ClientService.Send(new SendBotStartMessage(_currentInlineBot.Id, chat.Id, startBot.Parameter));
                    NavigationService.NavigateToChat(chat);
                }
            }
            else if (InlineBotResults.Button.Type is InlineQueryResultsButtonTypeWebApp webApp)
            {
                var response = await ClientService.SendAsync(new GetWebAppUrl(_currentInlineBot.Id, webApp.Url, Theme.Current.Parameters, Strings.AppName));
                if (response is HttpUrl httpUrl)
                {
                    await ShowPopupAsync(new WebBotPopup(ClientService, NavigationService, _currentInlineBot, httpUrl.Url, null, Chat));
                }
            }
        }

        #endregion

        #region Share my contact

        public void ShareMyContact()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var user = ClientService.GetUser(chat);
            if (user == null)
            {
                return;
            }

            ClientService.Send(new SharePhoneNumber(user.Id));
        }

        #endregion

        #region Unarchive

        public void Unarchive()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            ClientService.Send(new AddChatToList(chat.Id, new ChatListMain()));
            ClientService.Send(new SetChatNotificationSettings(chat.Id, new ChatNotificationSettings
            {
                UseDefaultDisableMentionNotifications = true,
                UseDefaultDisablePinnedMessageNotifications = true,
                UseDefaultMuteFor = true,
                UseDefaultShowPreview = true,
                UseDefaultSound = true
            }));
        }

        #endregion

        #region Invite

        public async void Invite()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup or ChatTypeBasicGroup)
            {
                var header = chat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel
                    ? Strings.AddSubscriber
                    : Strings.AddMember;

                var selected = await ChooseChatsPopup.PickUsersAsync(ClientService, header);
                if (selected == null || selected.Count == 0)
                {
                    return;
                }

                string title = Locale.Declension(Strings.R.AddManyMembersAlertTitle, selected.Count);
                string message;

                if (selected.Count <= 5)
                {
                    var names = string.Join(", ", selected.Select(x => x.FullName()));
                    message = string.Format(Strings.AddMembersAlertNamesText, names, chat.Title);
                }
                else
                {
                    message = Locale.Declension(Strings.R.AddManyMembersAlertNamesText, selected.Count, chat.Title);
                }

                var confirm = await ShowPopupAsync(message, title, Strings.Add, Strings.Cancel);
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }

                var response = await ClientService.SendAsync(new AddChatMembers(chat.Id, selected.Select(x => x.Id).ToArray()));
                if (response is Error error)
                {

                }
            }
        }

        #endregion

        #region Add contact

        public void AddToContacts()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var user = ClientService.GetUser(chat);
            if (user == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(UserEditPage), user.Id);
        }

        #endregion

        #region Start

        public void Start()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var bot = GetStartingBot();
            if (bot == null)
            {
                return;
            }

            var token = _accessToken;

            AccessToken = null;
            ClientService.Send(new SendBotStartMessage(bot.Id, chat.Id, token ?? string.Empty));
        }

        private User GetStartingBot()
        {
            var chat = _chat;
            if (chat == null)
            {
                return null;
            }

            if (chat.Type is ChatTypePrivate privata)
            {
                return ClientService.GetUser(privata.UserId);
            }

            //var user = _with as TLUser;
            //if (user != null && user.IsBot)
            //{
            //    return user;
            //}

            //var chat = _with as TLChatBase;
            //if (chat != null)
            //{
            //    // TODO
            //    //return this._bot;
            //}

            return null;
        }

        #endregion

        #region Search

        public void SearchExecute(string query)
        {
            if (Search == null)
            {
                Search = new ChatSearchViewModel(ClientService, Settings, Aggregator, this, query);
            }
            else
            {
                Search = null;
            }
        }

        #endregion

        #region Jump to date

        public async void JumpDate()
        {
            var dialog = new CalendarPopup();
            dialog.MaxDate = DateTimeOffset.Now.Date;
            //dialog.SelectedDates.Add(BindConvert.Current.DateTime(message.Date));

            var confirm = await ShowPopupAsync(dialog);
            if (confirm == ContentDialogResult.Primary && dialog.SelectedDates.Count > 0)
            {
                var first = dialog.SelectedDates.FirstOrDefault();
                var offset = first.Date.ToTimestamp();

                await LoadDateSliceAsync(offset);
            }
        }

        #endregion

        #region Group stickers

        private void GroupStickersExecute()
        {
            //var channel = With as TLChannel;
            //if (channel == null)
            //{
            //    return;
            //}

            //var channelFull = Full as TLChannelFull;
            //if (channelFull == null)
            //{
            //    return;
            //}

            //if ((channel.IsCreator || (channel.HasAdminRights && channel.AdminRights.IsChangeInfo)) && channelFull.IsCanSetStickers)
            //{
            //    NavigationService.Navigate(typeof(SupergroupEditStickerSetPage), channel.ToPeer());
            //}
            //else
            //{
            //    Stickers.HideGroup(channelFull);
            //}
        }

        #endregion

        #region Read mentions

        public void ReadMentions()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (ThreadId != 0)
            {
                ClientService.Send(new ReadAllMessageThreadMentions(chat.Id, ThreadId));
            }
            else
            {
                ClientService.Send(new ReadAllChatMentions(chat.Id));
            }
        }

        #endregion

        #region Read reactions

        public void ReadReactions()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (ThreadId != 0)
            {
                ClientService.Send(new ReadAllMessageThreadReactions(chat.Id, ThreadId));
            }
            else
            {
                ClientService.Send(new ReadAllChatReactions(chat.Id));
            }
        }

        #endregion

        #region Read messages

        public void ReadMessages()
        {
            RepliesStack.Clear();
            PreviousSlice();
        }

        #endregion

        #region Mute for

        public async void MuteFor(int? value)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (value is int update)
            {
                _notificationsService.SetMuteFor(chat, update);
            }
            else
            {
                var mutedFor = Settings.Notifications.GetMutedFor(chat);
                var popup = new ChatMutePopup(mutedFor);

                var confirm = await ShowPopupAsync(popup);
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }

                if (mutedFor != popup.Value)
                {
                    _notificationsService.SetMuteFor(chat, popup.Value);
                }
            }
        }

        #endregion

        #region Report Chat

        public async void Report()
        {
            await ReportAsync(Array.Empty<long>());
        }

        private async Task ReportAsync(IList<long> messages)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var form = await GetReportFormAsync(NavigationService);
            if (form.Reason != null)
            {
                ClientService.Send(new ReportChat(chat.Id, messages, form.Reason, form.Text));
            }
        }

        public static async Task<(ReportReason Reason, string Text)> GetReportFormAsync(INavigationService navigation)
        {
            var items = new[]
            {
                new ChooseOptionItem(new ReportReasonSpam(), Strings.ReportChatSpam, true),
                new ChooseOptionItem(new ReportReasonViolence(), Strings.ReportChatViolence, false),
                new ChooseOptionItem(new ReportReasonChildAbuse(), Strings.ReportChatChild, false),
                new ChooseOptionItem(new ReportReasonIllegalDrugs(), Strings.ReportChatIllegalDrugs, false),
                new ChooseOptionItem(new ReportReasonPersonalDetails(), Strings.ReportChatPersonalDetails, false),
                new ChooseOptionItem(new ReportReasonPornography(), Strings.ReportChatPornography, false),
                new ChooseOptionItem(new ReportReasonCustom(), Strings.ReportChatOther, false)
            };

            var popup = new ChooseOptionPopup(items);
            popup.Title = Strings.ReportChat;
            popup.PrimaryButtonText = Strings.OK;
            popup.SecondaryButtonText = Strings.Cancel;

            var confirm = await popup.ShowQueuedAsync();
            if (confirm != ContentDialogResult.Primary)
            {
                return (null, string.Empty);
            }

            var reason = popup.SelectedIndex as ReportReason;
            if (reason is not ReportReasonCustom)
            {
                return (reason, string.Empty);
            }

            var input = new InputPopup();
            input.Title = Strings.ReportChat;
            input.PlaceholderText = Strings.ReportChatDescription;
            input.IsPrimaryButtonEnabled = true;
            input.IsSecondaryButtonEnabled = true;
            input.PrimaryButtonText = Strings.OK;
            input.SecondaryButtonText = Strings.Cancel;

            var inputResult = await input.ShowQueuedAsync();
            if (inputResult == ContentDialogResult.Primary)
            {
                return (reason, input.Text);
            }

            return (null, string.Empty);
        }

        #endregion

        #region Set timer

        public async void SetTimer()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var dialog = new ChatTtlPopup(chat.Type is ChatTypeSecret ? ChatTtlType.Secret : ChatTtlType.Normal);
            dialog.Value = chat.MessageAutoDeleteTime;

            var confirm = await ShowPopupAsync(dialog);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            ClientService.Send(new SetChatMessageAutoDeleteTime(chat.Id, dialog.Value));
        }

        #endregion

        #region Set theme

        public void ChangeTheme()
        {
            Delegate?.ChangeTheme();
        }

        #endregion

        #region Scheduled messages

        public void ShowScheduled()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.NavigateToChat(chat, scheduled: true);
        }

        #endregion

        #region Action

        protected virtual void FilterExecute()
        {

        }

        public async void Action()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (_type == DialogType.EventLog)
            {
                FilterExecute();
            }
            else if (_type == DialogType.Pinned)
            {
                var supergroupType = chat.Type as ChatTypeSupergroup;
                var basicGroupType = chat.Type as ChatTypeBasicGroup;

                var supergroup = supergroupType != null ? ClientService.GetSupergroup(supergroupType.SupergroupId) : null;
                var basicGroup = basicGroupType != null ? ClientService.GetBasicGroup(basicGroupType.BasicGroupId) : null;

                if (supergroup != null && supergroup.CanPinMessages() ||
                    basicGroup != null && basicGroup.CanPinMessages() ||
                    chat.Type is ChatTypePrivate privata)
                {
                    var confirm = await ShowPopupAsync(Strings.UnpinMessageAlert, Strings.AppName, Strings.OK, Strings.Cancel);
                    if (confirm == ContentDialogResult.Primary)
                    {
                        ClientService.Send(new UnpinAllChatMessages(chat.Id));
                        Delegate?.UpdatePinnedMessage(chat, false);
                    }
                }
                else
                {
                    Settings.SetChatPinnedMessage(chat.Id, int.MaxValue);
                    Delegate?.UpdatePinnedMessage(chat, false);
                }
            }
            else if (chat.Type is ChatTypePrivate privata)
            {
                var user = ClientService.GetUser(privata.UserId);
                if (user == null)
                {
                    return;
                }

                if (user.Type is UserTypeDeleted)
                {
                    DeleteChat();
                }
                else if (ClientService.IsRepliesChat(chat))
                {
                    ToggleMute();
                }
                else if (chat.BlockList is BlockListMain)
                {
                    Unblock();
                }
                else if (user.Type is UserTypeBot)
                {
                    Start();
                }
            }
            else if (chat.Type is ChatTypeBasicGroup basic)
            {
                var group = ClientService.GetBasicGroup(basic.BasicGroupId);
                if (group == null)
                {
                    return;
                }

                if (group.UpgradedToSupergroupId != 0)
                {
                    var response = await ClientService.SendAsync(new CreateSupergroupChat(group.UpgradedToSupergroupId, false));
                    if (response is Chat migratedChat)
                    {
                        NavigationService.NavigateToChat(migratedChat);
                    }
                }
                else if (group.Status is ChatMemberStatusLeft or ChatMemberStatusBanned)
                {
                    // Delete and exit
                    DeleteChat();
                }
                else if (group.Status is ChatMemberStatusCreator creator && !creator.IsMember)
                {
                    JoinChannel();
                }
            }
            else if (chat.Type is ChatTypeSupergroup super)
            {
                var group = ClientService.GetSupergroup(super.SupergroupId);
                if (group == null)
                {
                    return;
                }

                if (group.IsChannel)
                {
                    if (group.Status is ChatMemberStatusLeft || (group.Status is ChatMemberStatusCreator creator && !creator.IsMember))
                    {
                        JoinChannel();
                    }
                    else
                    {
                        ToggleMute();
                    }
                }
                else
                {
                    if (group.Status is ChatMemberStatusLeft || (group.Status is ChatMemberStatusRestricted restricted && !restricted.IsMember) || (group.Status is ChatMemberStatusCreator creator && !creator.IsMember))
                    {
                        JoinChannel();
                    }
                    else if (group.Status is ChatMemberStatusBanned)
                    {
                        DeleteChat();
                    }
                }
            }
        }

        #endregion

        #region Join requests

        public async void ShowJoinRequests()
        {
            var popup = new ChatJoinRequestsPopup(ClientService, Settings, Aggregator, _chat, string.Empty);
            var confirm = await ShowPopupAsync(popup);
        }

        #endregion

        #region Group calls

        public async void JoinGroupCall()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            await _voipGroupService.JoinAsync(chat.Id);
        }

        #endregion
    }

    public class UserCommand
    {
        public UserCommand(long userId, BotCommand command)
        {
            UserId = userId;
            Item = command;
        }

        public long UserId { get; set; }
        public BotCommand Item { get; set; }
    }

    public class MessageCollection : MvxObservableCollection<MessageViewModel>
    {
        private readonly Dictionary<long, MessageViewModel> _messages = new();

        private long _first = long.MaxValue;
        private long _last = long.MinValue;

        private bool _suppressOperations = false;
        private bool _suppressPrev = false;
        private bool _suppressNext = false;

        public ICollection<long> Ids => _messages.Keys;

        public long FirstId => _first;
        public long LastId => _last;

        public Action<IEnumerable<MessageViewModel>> AttachChanged;

        public MessageCollection()
        {
            _messages = new();
        }

        public MessageCollection(ICollection<long> exclude, IEnumerable<Message> source, Func<Message, bool, MessageViewModel> create)
        {
            foreach (var item in source)
            {
                if (exclude != null && exclude.Contains(item.Id))
                {
                    continue;
                }

                Insert(0, create(item, true /* forLanguageStatistics */));
            }
        }

        //~MessageCollection()
        //{
        //    Debug.WriteLine("Finalizing MessageCollection");
        //    GC.Collect();
        //}

        protected override void ClearItems()
        {
            _messages.Clear();
            base.ClearItems();
        }

        public bool ContainsKey(long id)
        {
            return _messages.ContainsKey(id);
        }

        public bool TryGetValue(long id, out MessageViewModel value)
        {
            return _messages.TryGetValue(id, out value);
        }

        public void UpdateMessageSendSucceeded(long oldMessageId, MessageViewModel message)
        {
            _messages.Remove(oldMessageId);
            _messages[message.Id] = message;
        }

        public void RawAddRange(IList<MessageViewModel> source, bool filter, out bool empty)
        {
            empty = true;

            for (int i = 0; i < source.Count; i++)
            {
                if (filter)
                {
                    if (source[i].Id != 0 && _messages.ContainsKey(source[i].Id))
                    {
                        continue;
                    }
                    else if (source[i].Id < _last)
                    {
                        continue;
                    }
                }

                _suppressOperations = i > 0;
                _suppressNext = !_suppressOperations;

                Add(source[i]);
                empty = false;

                if (i == source.Count - 1)
                {
                    _last = source[i].Id;
                }
            }

            _suppressOperations = false;
        }

        public void RawInsertRange(int index, IList<MessageViewModel> source, bool filter, out bool empty)
        {
            empty = true;

            for (int i = source.Count - 1; i >= 0; i--)
            {
                if (filter)
                {
                    if (source[i].Id != 0 && _messages.ContainsKey(source[i].Id))
                    {
                        continue;
                    }
                    else if (source[i].Id > _first)
                    {
                        continue;
                    }
                }

                _suppressOperations = i < source.Count - 1;
                _suppressPrev = !_suppressOperations;

                Insert(0, source[i]);
                empty = false;

                if (i == 0)
                {
                    _first = source[i].Id;
                }
            }

            _suppressOperations = false;
        }

        public void RawReplaceWith(IEnumerable<MessageViewModel> source)
        {
            _messages.Clear();
            _suppressOperations = true;

            _first = long.MaxValue;
            _last = long.MinValue;

            ReplaceWith(source);

            _suppressOperations = false;
        }

        protected override void InsertItem(int index, MessageViewModel item)
        {
            if (item.Content is MessageAlbum album)
            {
                foreach (var child in album.Messages)
                {
                    _messages[child.Id] = item;
                }
            }

            _messages[item.Id] = item;

            if (item.Id != 0)
            {
                _first = Math.Min(item.Id, _first);
                _last = Math.Max(item.Id, _last);
            }

            if (_suppressOperations)
            {
                base.InsertItem(index, item);
            }
            else if (_suppressNext)
            {
                var prev = index > 0 ? this[index - 1] : null;
                var prevSeparator = UpdateSeparatorOnInsert(prev, item);
                var prevHash = AttachHash(prev);

                if (prevSeparator != null)
                {
                    UpdateAttach(null, prev);
                    UpdateAttach(prevSeparator, item);
                }
                else
                {
                    UpdateAttach(item, prev);
                }

                if (prevSeparator != null)
                {
                    base.InsertItem(index++, prevSeparator);
                }

                base.InsertItem(index, item);

                var prevUpdate = AttachHash(prev);
                if (prevUpdate != prevHash)
                {
                    AttachChanged?.Invoke(new[] { prev });
                }
            }
            else if (_suppressPrev)
            {
                var next = index < Count ? this[index] : null;
                var nextSeparator = UpdateSeparatorOnInsert(item, next);
                var nextHash = AttachHash(next);

                if (nextSeparator != null)
                {
                    UpdateAttach(next, null);
                    UpdateAttach(item, nextSeparator);
                }
                else
                {
                    UpdateAttach(next, item);
                }

                base.InsertItem(index, item);

                if (nextSeparator != null)
                {
                    base.InsertItem(++index, nextSeparator);
                }

                var nextUpdate = AttachHash(next);
                if (nextUpdate != nextHash)
                {
                    AttachChanged?.Invoke(new[] { next });
                }
            }
            else
            {
                var prev = index > 0 ? this[index - 1] : null;
                var next = index < Count ? this[index] : null;

                // Order must be:
                // Separator between previous and item
                // Item
                // Separator between item and next
                // UpdateSeparatorOnInsert must return the new messages
                // This way only two AttachChanged will be needed at most

                var prevSeparator = UpdateSeparatorOnInsert(prev, item);
                var nextSeparator = UpdateSeparatorOnInsert(item, next);

                var nextHash = AttachHash(next);
                var prevHash = AttachHash(prev);

                if (prevSeparator != null)
                {
                    UpdateAttach(null, prev);
                    UpdateAttach(prevSeparator, item);
                }
                else
                {
                    UpdateAttach(item, prev);
                }

                if (nextSeparator != null)
                {
                    UpdateAttach(next, null);
                    UpdateAttach(item, nextSeparator);
                }
                else
                {
                    UpdateAttach(next, item);
                }

                if (prevSeparator != null)
                {
                    base.InsertItem(index++, prevSeparator);
                }

                base.InsertItem(index, item);

                if (nextSeparator != null)
                {
                    base.InsertItem(++index, nextSeparator);
                }

                var nextUpdate = AttachHash(next);
                var prevUpdate = AttachHash(prev);

                if (prevHash != prevUpdate || nextHash != nextUpdate)
                {
                    AttachChanged?.Invoke(new[]
                    {
                        prevHash != prevUpdate ? prev : null,
                        nextHash != nextUpdate ? next : null
                    });
                }
            }
        }

        protected override void RemoveItem(int index)
        {
            _messages?.Remove(this[index].Id);

            var next = index > 0 ? this[index - 1] : null;
            var previous = index < Count - 1 ? this[index + 1] : null;

            var hash2 = AttachHash(next);
            var hash3 = AttachHash(previous);

            UpdateAttach(previous, next);

            var update2 = AttachHash(next);
            var update3 = AttachHash(previous);

            if (hash3 != update3 || hash2 != update2)
            {
                AttachChanged?.Invoke(new[]
                {
                    hash3 != update3 ? previous : null,
                    hash2 != update2 ? next : null
                });
            }

            base.RemoveItem(index);

            UpdateSeparatorOnRemove(next, previous, index);
        }

        // TODO: Support MoveItem to optimize UpdateMessageSendSucceeded

        private static MessageViewModel UpdateSeparatorOnInsert(MessageViewModel item, MessageViewModel next)
        {
            if (item != null && next != null && item.Content is not MessageHeaderDate && next.Content is not MessageHeaderDate)
            {
                var itemDate = Formatter.ToLocalTime(GetMessageDate(item));
                var previousDate = Formatter.ToLocalTime(GetMessageDate(next));
                if (previousDate.Date != itemDate.Date)
                {
                    return new MessageViewModel(next.ClientService, next.PlaybackService, next.Delegate, next.Chat, new Message(0, next.SenderId, next.ChatId, null, next.SchedulingState, next.IsOutgoing, false, false, false, false, false, true, false, false, false, false, false, false, false, false, next.IsChannelPost, next.IsTopicMessage, false, next.Date, 0, null, null, null, null, null, 0, null, 0, 0, 0, string.Empty, 0, string.Empty, new MessageHeaderDate(), null));
                }
            }

            return null;
        }

        private void UpdateSeparatorOnRemove(MessageViewModel next, MessageViewModel previous, int index)
        {
            if (next != null && next.Content is MessageHeaderDate && previous != null)
            {
                var itemDate = Formatter.ToLocalTime(GetMessageDate(next));
                var previousDate = Formatter.ToLocalTime(GetMessageDate(previous));
                if (previousDate.Date != itemDate.Date)
                {
                    base.RemoveItem(index - 1);
                }
            }
            else if (next != null && next.Content is MessageHeaderDate && previous == null)
            {
                base.RemoveItem(index - 1);
            }
        }

        private static int AttachHash(MessageViewModel item)
        {
            var hash = 0;
            if (item != null && item.IsFirst)
            {
                hash |= 1 << 0;
            }
            if (item != null && item.IsLast)
            {
                hash |= 2 << 0;
            }

            return hash;
        }

        private static int GetMessageDate(MessageViewModel item)
        {
            if (item.SchedulingState is MessageSchedulingStateSendAtDate sendAtDate)
            {
                return sendAtDate.SendDate;
            }
            else if (item.SchedulingState is MessageSchedulingStateSendWhenOnline)
            {
                return int.MinValue;
            }

            return item.Date;
        }

        private static void UpdateAttach(MessageViewModel item, MessageViewModel previous)
        {
            if (item == null)
            {
                if (previous != null)
                {
                    previous.IsLast = true;
                }

                return;
            }

            if (item.IsChannelPost)
            {
                item.IsFirst = true;
                item.IsLast = true;
                return;
            }

            var attach = false;
            if (previous != null)
            {
                var previousPost = previous.IsChannelPost;

                attach = !previousPost &&
                         //!(previous.IsService()) &&
                         AreTogether(item, previous) &&
                         GetMessageDate(item) - GetMessageDate(previous) < 900;
            }

            item.IsFirst = !attach;

            if (previous != null)
            {
                previous.IsLast = item.IsFirst /*|| item.IsService()*/;
            }
        }

        private static bool AreTogether(MessageViewModel message1, MessageViewModel message2)
        {
            if (message1.IsService || message2.IsService)
            {
                return false;
            }

            var saved1 = message1.IsSaved;
            var saved2 = message2.IsSaved;

            if (saved1 && saved2)
            {
                if (message1.ForwardInfo?.Origin is MessageOriginUser fromUser1 && message2.ForwardInfo?.Origin is MessageOriginUser fromUser2)
                {
                    return fromUser1.SenderUserId == fromUser2.SenderUserId && message1.ForwardInfo.FromChatId == message2.ForwardInfo.FromChatId;
                }
                else if (message1.ForwardInfo?.Origin is MessageOriginChat fromChat1 && message2.ForwardInfo?.Origin is MessageOriginChat fromChat2)
                {
                    return fromChat1.SenderChatId == fromChat2.SenderChatId && message1.ForwardInfo.FromChatId == message2.ForwardInfo.FromChatId;
                }
                else if (message1.ForwardInfo?.Origin is MessageOriginChannel fromChannel1 && message2.ForwardInfo?.Origin is MessageOriginChannel fromChannel2)
                {
                    return fromChannel1.ChatId == fromChannel2.ChatId && message1.ForwardInfo.FromChatId == message2.ForwardInfo.FromChatId;
                }
                else if (message1.ForwardInfo?.Origin is MessageOriginHiddenUser hiddenUser1 && message2.ForwardInfo?.Origin is MessageOriginHiddenUser hiddenUser2)
                {
                    return hiddenUser1.SenderName == hiddenUser2.SenderName;
                }
                else if (message1.ImportInfo != null && message2.ImportInfo != null)
                {
                    return message1.ImportInfo.SenderName == message2.ImportInfo.SenderName;
                }

                return false;
            }
            else if (saved1 || saved2)
            {
                return false;
            }

            if (message1.SenderId is MessageSenderChat chat1 && message2.SenderId is MessageSenderChat chat2)
            {
                return chat1.ChatId == chat2.ChatId
                    && message1.AuthorSignature == message2.AuthorSignature;
            }
            else if (message1.SenderId is MessageSenderUser user1 && message2.SenderId is MessageSenderUser user2)
            {
                return user1.UserId == user2.UserId;
            }

            return false;
        }
    }

    public class DialogUnreadMessagesViewModel : BindableBase
    {
        private readonly DialogViewModel _viewModel;
        private readonly SearchMessagesFilter _filter;

        private readonly bool _oldToNew;

        private List<long> _messages = new();
        private long _lastMessage;

        public DialogUnreadMessagesViewModel(DialogViewModel viewModel, SearchMessagesFilter filter)
        {
            _viewModel = viewModel;
            _filter = filter;

            _oldToNew = filter is SearchMessagesFilterUnreadMention;
        }

        public void SetLastViewedMessage(long messageId)
        {
            _lastMessage = messageId;
        }

        public void RemoveMessage(long messageId)
        {
            _messages?.Remove(messageId);
        }

        public async void NextMessage()
        {
            var chat = _viewModel.Chat;
            if (chat == null)
            {
                return;
            }

            if (_messages != null && _messages.Count > 0)
            {
                await _viewModel.LoadMessageSliceAsync(null, _messages.RemoveLast());
            }
            else
            {
                long fromMessageId;
                if (_lastMessage != 0)
                {
                    fromMessageId = _lastMessage;
                }
                else
                {
                    var first = _viewModel.Items.FirstOrDefault();
                    if (first != null)
                    {
                        fromMessageId = first.Id;
                    }
                    else
                    {
                        return;
                    }
                }

                var response = await _viewModel.ClientService.SendAsync(new SearchChatMessages(chat.Id, string.Empty, null, fromMessageId, -9, 10, _filter, _viewModel.ThreadId));
                if (response is FoundChatMessages messages)
                {
                    List<long> stack = null;

                    if (_oldToNew)
                    {
                        foreach (var message in messages.Messages.Reverse())
                        {
                            stack ??= new List<long>();
                            stack.Add(message.Id);
                        }
                    }
                    else
                    {
                        foreach (var message in messages.Messages)
                        {
                            stack ??= new List<long>();
                            stack.Add(message.Id);
                        }
                    }

                    if (stack != null)
                    {
                        _messages = stack;
                        NextMessage();
                    }
                }
            }
        }
    }

    public class MessageComposerHeader
    {
        public IClientService ClientService { get; }

        public MessageComposerHeader(IClientService clientService)
        {
            ClientService = clientService;
        }

        public MessageViewModel ReplyToMessage { get; set; }
        public InputTextQuote ReplyToQuote { get; set; }

        public MessageViewModel EditingMessage { get; set; }

        public InputMessageFactory EditingMessageMedia { get; set; }
        public FormattedText EditingMessageCaption { get; set; }

        public WebPage WebPagePreview { get; set; }
        public string WebPageUrl { get; set; }

        public bool WebPageDisabled
        {
            get => LinkPreviewOptions?.IsDisabled ?? false;
            set
            {
                if (LinkPreviewOptions == null && !value)
                {
                    return;
                }

                LinkPreviewOptions ??= new();
                LinkPreviewOptions.IsDisabled = value;
            }
        }

        public LinkPreviewOptions LinkPreviewOptions { get; set; } = new();

        public bool IsEmpty
        {
            get
            {
                return ReplyToMessage == null && EditingMessage == null;
            }
        }

        public bool Matches(long messageId)
        {
            if (ReplyToMessage != null && ReplyToMessage.Id == messageId)
            {
                return true;
            }
            else if (EditingMessage != null && EditingMessage.Id == messageId)
            {
                return true;
            }

            return false;
        }
    }

    [Flags]
    public enum DialogType
    {
        History,
        Thread,
        Pinned,
        ScheduledMessages,
        EventLog
    }
}
