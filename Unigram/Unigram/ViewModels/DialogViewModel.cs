using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Telegram.Td;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Common.Chats;
using Unigram.Controls;
using Unigram.Controls.Chats;
using Unigram.Navigation;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.Services.Factories;
using Unigram.Services.Updates;
using Unigram.ViewModels.Chats;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Drawers;
using Unigram.Views;
using Unigram.Views.Popups;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Point = Windows.Foundation.Point;

namespace Unigram.ViewModels
{
    public partial class DialogViewModel : TLViewModelBase, IDelegable<IDialogDelegate>
    {
        private readonly ConcurrentDictionary<long, MessageViewModel> _selectedItems = new();
        public IDictionary<long, MessageViewModel> SelectedItems => _selectedItems;

        public int SelectedCount => SelectedItems.Count;

        protected readonly ConcurrentDictionary<long, MessageViewModel> _groupedMessages = new();

        protected static readonly Dictionary<long, IList<ChatAdministrator>> _admins = new();
        protected static readonly Dictionary<string, MessageContent> _contentOverrides = new();

        protected readonly DisposableMutex _loadMoreLock = new DisposableMutex();

        protected readonly EmojiDrawerViewModel _emoji;
        protected readonly StickerDrawerViewModel _stickers;
        protected readonly AnimationDrawerViewModel _animations;

        protected readonly DialogUnreadMessagesViewModel<SearchMessagesFilterUnreadMention> _mentions;
        protected readonly DialogUnreadMessagesViewModel<SearchMessagesFilterUnreadReaction> _reactions;

        protected readonly ILocationService _locationService;
        protected readonly INotificationsService _pushService;
        protected readonly IPlaybackService _playbackService;
        protected readonly IVoipService _voipService;
        protected readonly IGroupCallService _groupCallService;
        protected readonly INetworkService _networkService;
        protected readonly IStorageService _storageService;
        protected readonly ITranslateService _translateService;
        protected readonly IMessageFactory _messageFactory;

        public IPlaybackService PlaybackService => _playbackService;

        public IStorageService StorageService => _storageService;

        public ITranslateService TranslateService => _translateService;

        public DialogUnreadMessagesViewModel<SearchMessagesFilterUnreadMention> Mentions => _mentions;

        public DialogUnreadMessagesViewModel<SearchMessagesFilterUnreadReaction> Reactions => _reactions;

        public IDialogDelegate Delegate { get; set; }

        public DialogViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, ILocationService locationService, INotificationsService pushService, IPlaybackService playbackService, IVoipService voipService, IGroupCallService groupCallService, INetworkService networkService, IStorageService storageService, ITranslateService translateService, IMessageFactory messageFactory)
            : base(clientService, settingsService, aggregator)
        {
            _locationService = locationService;
            _pushService = pushService;
            _playbackService = playbackService;
            _voipService = voipService;
            _groupCallService = groupCallService;
            _networkService = networkService;
            _storageService = storageService;
            _translateService = translateService;
            _messageFactory = messageFactory;

            //_stickers = new DialogStickersViewModel(clientService, settingsService, aggregator);
            _emoji = EmojiDrawerViewModel.GetForCurrentView(clientService.SessionId);
            _stickers = StickerDrawerViewModel.GetForCurrentView(clientService.SessionId);
            _animations = AnimationDrawerViewModel.GetForCurrentView(clientService.SessionId);

            _mentions = new DialogUnreadMessagesViewModel<SearchMessagesFilterUnreadMention>(this, true);
            _reactions = new DialogUnreadMessagesViewModel<SearchMessagesFilterUnreadReaction>(this, false);

            ToggleMuteCommand = new RelayCommand<bool>(ToggleMuteExecute);
            MuteCommand = new RelayCommand(() => ToggleMuteExecute(false));
            UnmuteCommand = new RelayCommand(() => ToggleMuteExecute(true));
            ReportSpamCommand = new RelayCommand<ChatReportReason>(ReportSpamExecute);
            ReportCommand = new RelayCommand(ReportExecute);
            ChatDeleteCommand = new RelayCommand(ChatDeleteExecute);
            ChatClearCommand = new RelayCommand(ChatClearExecute);
            CallCommand = new RelayCommand<bool>(CallExecute);
            PinnedHideCommand = new RelayCommand(PinnedHideExecute);
            PinnedShowCommand = new RelayCommand(PinnedShowExecute);
            PinnedListCommand = new RelayCommand(PinnedListExecute);
            UnarchiveCommand = new RelayCommand(UnarchiveExecute);
            InviteCommand = new RelayCommand(InviteExecute);
            ShareContactCommand = new RelayCommand(ShareContactExecute);
            AddContactCommand = new RelayCommand(AddContactExecute);
            SearchCommand = new RelayCommand(SearchExecute);
            GroupStickersCommand = new RelayCommand(GroupStickersExecute);
            SwitchCommand = new RelayCommand<string>(SwitchExecute);
            MuteForCommand = new RelayCommand<int?>(MuteForExecute);
            SetTimerCommand = new RelayCommand<int?>(SetTimerExecute);
            SetThemeCommand = new RelayCommand(SetThemeExecute);
            OpenUserCommand = new RelayCommand<long>(OpenUser);
            SetSenderCommand = new RelayCommand<ChatMessageSender>(SetSenderExecute);

            MessagesForwardCommand = new RelayCommand(MessagesForwardExecute, MessagesForwardCanExecute);
            MessagesDeleteCommand = new RelayCommand(MessagesDeleteExecute, MessagesDeleteCanExecute);
            MessagesCopyCommand = new RelayCommand(MessagesCopyExecute, MessagesCopyCanExecute);
            MessagesReportCommand = new RelayCommand(MessagesReportExecute, MessagesReportCanExecute);
            MessagesUnselectCommand = new RelayCommand(MessagesUnselectExecute);

            MessageReplyCommand = new RelayCommand<MessageViewModel>(MessageReplyExecute);
            MessageRetryCommand = new RelayCommand<MessageViewModel>(MessageRetryExecute);
            MessageDeleteCommand = new RelayCommand<MessageViewModel>(MessageDeleteExecute);
            MessageForwardCommand = new RelayCommand<MessageViewModel>(MessageForwardExecute);
            MessageSelectCommand = new RelayCommand<MessageViewModel>(MessageSelectExecute);
            MessageStatisticsCommand = new RelayCommand<MessageViewModel>(MessageStatisticsExecute);
            MessageCopyCommand = new RelayCommand<MessageViewModel>(MessageCopyExecute);
            MessageCopyMediaCommand = new RelayCommand<MessageViewModel>(MessageCopyMediaExecute);
            MessageCopyLinkCommand = new RelayCommand<MessageViewModel>(MessageCopyLinkExecute);
            MessageEditCommand = new RelayCommand<MessageViewModel>(MessageEditExecute);
            MessageThreadCommand = new RelayCommand<MessageViewModel>(MessageThreadExecute);
            MessagePinCommand = new RelayCommand<MessageViewModel>(MessagePinExecute);
            MessageReportCommand = new RelayCommand<MessageViewModel>(MessageReportExecute);
            MessageReportFalsePositiveCommand = new RelayCommand<MessageViewModel>(MessageReportFalsePositiveExecute);
            MessageAddStickerCommand = new RelayCommand<MessageViewModel>(MessageAddStickerExecute);
            MessageFaveStickerCommand = new RelayCommand<MessageViewModel>(MessageFaveStickerExecute);
            MessageUnfaveStickerCommand = new RelayCommand<MessageViewModel>(MessageUnfaveStickerExecute);
            MessageSaveMediaCommand = new RelayCommand<MessageViewModel>(MessageSaveMediaExecute);
            MessageSaveAnimationCommand = new RelayCommand<MessageViewModel>(MessageSaveAnimationExecute);
            MessageSaveSoundCommand = new RelayCommand<MessageViewModel>(MessageSaveSoundExecute);
            MessageOpenWithCommand = new RelayCommand<MessageViewModel>(MessageOpenWithExecute);
            MessageOpenFolderCommand = new RelayCommand<MessageViewModel>(MessageOpenFolderExecute);
            MessageAddContactCommand = new RelayCommand<MessageViewModel>(MessageAddContactExecute);
            MessageServiceCommand = new RelayCommand<MessageViewModel>(MessageServiceExecute);
            MessageUnvotePollCommand = new RelayCommand<MessageViewModel>(MessageUnvotePollExecute);
            MessageStopPollCommand = new RelayCommand<MessageViewModel>(MessageStopPollExecute);
            MessageSendNowCommand = new RelayCommand<MessageViewModel>(MessageSendNowExecute);
            MessageRescheduleCommand = new RelayCommand<MessageViewModel>(MessageRescheduleExecute);
            MessageTranslateCommand = new RelayCommand<MessageViewModel>(MessageTranslateExecute);
            MessageShowEmojiCommand = new RelayCommand<MessageViewModel>(MessageShowEmojiExecute);

            SendDocumentCommand = new RelayCommand(SendDocumentExecute);
            SendCameraCommand = new RelayCommand(SendCameraExecute);
            SendMediaCommand = new RelayCommand(SendMediaExecute);
            SendContactCommand = new RelayCommand(SendContactExecute);
            SendLocationCommand = new RelayCommand(SendLocationExecute);
            SendPollCommand = new RelayCommand(SendPollExecute);

            StickerSendCommand = new RelayCommand<Sticker>(StickerSendExecute);
            StickerViewCommand = new RelayCommand<Sticker>(StickerViewExecute);
            StickerFaveCommand = new RelayCommand<Sticker>(StickerFaveExecute);
            StickerUnfaveCommand = new RelayCommand<Sticker>(StickerUnfaveExecute);
            StickerDeleteCommand = new RelayCommand<Sticker>(StickerDeleteExecute);

            AnimationSendCommand = new RelayCommand<Animation>(AnimationSendExecute);
            AnimationDeleteCommand = new RelayCommand<Animation>(AnimationDeleteExecute);
            AnimationSaveCommand = new RelayCommand<Animation>(AnimationSaveExecute);

            EditDocumentCommand = new RelayCommand(EditDocumentExecute);
            EditMediaCommand = new RelayCommand(EditMediaExecute);
            EditCurrentCommand = new RelayCommand(EditCurrentExecute);

            GroupCallJoinCommand = new RelayCommand(GroupCallJoinExecute);

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

        public void Handle(UpdateWindowActivated update)
        {
            if (!update.IsActive)
            {
                BeginOnUIThread(SaveDraft);
            }
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

        protected Chat _migratedChat;
        public Chat MigratedChat
        {
            get => _migratedChat;
            set => Set(ref _migratedChat, value);
        }

        protected Chat _linkedChat;
        public Chat LinkedChat
        {
            get => _linkedChat;
            set => Set(ref _linkedChat, value);
        }

        protected long _threadId => _thread?.MessageThreadId ?? 0;
        public long ThreadId => _thread?.MessageThreadId ?? 0;

        protected MessageThreadInfo _thread;
        public MessageThreadInfo Thread
        {
            get => _thread;
            set => Set(ref _thread, value);
        }

        protected ForumTopicInfo _topic;
        public ForumTopicInfo Topic
        {
            get => _topic;
            set => Set(ref _topic, value);
        }

        protected Chat _chat;
        public Chat Chat
        {
            get => _chat;
            set => Set(ref _chat, value);
        }

        private DialogType _type => Type;
        public virtual DialogType Type => DialogType.History;

        private string _lastSeen;
        public string LastSeen
        {
            get => _type == DialogType.EventLog ? Strings.Resources.EventLog : _lastSeen;
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
                return (_accessToken != null || _isEmpty) && !_isLoadingNextSlice && !_isLoadingPreviousSlice;
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
                return _chatActionManager ??= new OutputChatActionManager(ClientService, _chat, _threadId);
            }
        }

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
        public ChatListView ListField { get; set; }

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
            var field = ListField;
            if (field == null)
            {
                return;
            }

            field.SetScrollMode(mode, force);
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

        public FormattedText GetFormattedText(bool clear = false)
        {
            var field = TextField;
            if (field == null)
            {
                return new FormattedText(string.Empty, new TextEntity[0]);
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
            get => _isEmpty && !_isLoadingNextSlice && !_isLoadingPreviousSlice;
            set
            {
                Set(ref _isEmpty, value);
                RaisePropertyChanged(nameof(HasAccessToken));
            }
        }

        public override bool IsLoading
        {
            get => _isLoadingNextSlice || _isLoadingPreviousSlice;
            set
            {
                base.IsLoading = value;
                RaisePropertyChanged(nameof(IsEmpty));
                RaisePropertyChanged(nameof(HasAccessToken));
            }
        }

        protected bool _isLoadingNextSlice;
        protected bool _isLoadingPreviousSlice;

        protected Stack<long> _repliesStack = new Stack<long>();

        public Stack<long> RepliesStack => _repliesStack;

        // Scrolling to top
        public virtual async Task LoadNextSliceAsync(bool force = false)
        {
            if (_type is not DialogType.History and not DialogType.Thread and not DialogType.Pinned)
            {
                return;
            }

            var chat = _migratedChat ?? _chat;
            if (chat == null)
            {
                return;
            }

            using (await _loadMoreLock.WaitAsync())
            {
                if (_isLoadingNextSlice || _isLoadingPreviousSlice || (_chat?.Id != chat.Id && _migratedChat?.Id != chat.Id) || Items.Count < 1 || IsLastSliceLoaded == true)
                {
                    return;
                }

                _isLoadingNextSlice = true;
                IsLoading = true;

                System.Diagnostics.Debug.WriteLine("DialogViewModel: LoadNextSliceAsync");

                //var first = Items.FirstOrDefault(x => x.Id != 0);
                //if (first is TLMessage firstMessage && firstMessage.Media is TLMessageMediaGroup groupMedia)
                //{
                //    first = groupMedia.Layout.Messages.FirstOrDefault();
                //}

                //var maxId = first?.Id ?? int.MaxValue;
                var maxId = Items.FirstOrDefault(x => x != null && x.Id != 0)?.Id;
                if (maxId == null)
                {
                    _isLoadingNextSlice = false;
                    IsLoading = false;

                    return;
                }

                System.Diagnostics.Debug.WriteLine("DialogViewModel: LoadNextSliceAsync: Begin request");

                Function func;
                if (_threadId != 0)
                {
                    func = new GetMessageThreadHistory(chat.Id, _threadId, maxId.Value, 0, 50);
                }
                else if (_type == DialogType.Pinned)
                {
                    func = new SearchChatMessages(chat.Id, string.Empty, null, maxId.Value, 0, 50, new SearchMessagesFilterPinned(), 0);
                }
                else
                {
                    func = new GetChatHistory(chat.Id, maxId.Value, 0, 50, false);
                }

                var response = await ClientService.SendAsync(func);
                if (response is Messages messages)
                {
                    if (_chat?.Id != chat.Id && _migratedChat?.Id != chat.Id)
                    {
                        _isLoadingNextSlice = false;
                        IsLoading = false;

                        return;
                    }

                    var replied = new MessageCollection(Items.Ids, messages.MessagesValue.Select(x => _messageFactory.Create(this, x)));
                    if (replied.Count > 0)
                    {
                        await ProcessMessagesAsync(chat, replied);

                        if (replied.Count > 0 && replied[0].Content is MessageChatUpgradeFrom chatUpgradeFrom)
                        {
                            var chatUpgradeTo = await PrepareMigratedAsync(chatUpgradeFrom.BasicGroupId);
                            if (chatUpgradeTo != null)
                            {
                                replied[0] = chatUpgradeTo;
                            }
                        }

                        SetScrollMode(ItemsUpdatingScrollMode.KeepLastItemInView, true);
                        Items.RawInsertRange(0, replied, true, out bool empty);
                    }
                    else
                    {
                        await AddHeaderAsync();
                    }

                    IsLastSliceLoaded = replied.Count == 0;
                }

                _isLoadingNextSlice = false;
                IsLoading = false;

                LoadPinnedMessagesSliceAsync(maxId.Value, VerticalAlignment.Top);
            }
        }

        // Scrolling to bottom
        public async Task LoadPreviousSliceAsync(bool force = false)
        {
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
                if (_isLoadingNextSlice || _isLoadingPreviousSlice || _chat?.Id != chat.Id || Items.Count < 1)
                {
                    return;
                }

                _isLoadingPreviousSlice = true;
                IsLoading = true;

                System.Diagnostics.Debug.WriteLine("DialogViewModel: LoadPreviousSliceAsync");

                var maxId = Items.LastOrDefault(x => x != null && x.Id != 0)?.Id;
                if (maxId == null)
                {
                    _isLoadingPreviousSlice = false;
                    IsLoading = false;

                    return;
                }

                Function func;
                if (_threadId != 0)
                {
                    func = new GetMessageThreadHistory(chat.Id, _threadId, maxId.Value, -49, 50);
                }
                else if (_type == DialogType.Pinned)
                {
                    func = new SearchChatMessages(chat.Id, string.Empty, null, maxId.Value, -49, 50, new SearchMessagesFilterPinned(), 0);
                }
                else
                {
                    func = new GetChatHistory(chat.Id, maxId.Value, -49, 50, false);
                }

                var response = await ClientService.SendAsync(func);
                if (response is Messages messages)
                {
                    if (_chat?.Id != chat.Id)
                    {
                        _isLoadingPreviousSlice = false;
                        IsLoading = false;

                        return;
                    }

                    var replied = new MessageCollection(Items.Ids, messages.MessagesValue.Select(x => _messageFactory.Create(this, x)));
                    if (replied.Count > 0)
                    {
                        await ProcessMessagesAsync(chat, replied);

                        SetScrollMode(ItemsUpdatingScrollMode.KeepItemsInView, true);
                        Items.RawAddRange(replied, true, out bool empty);
                    }
                    else
                    {
                        SetScrollMode(ItemsUpdatingScrollMode.KeepLastItemInView, true);
                    }

                    IsFirstSliceLoaded = replied.Count == 0 || IsEndReached();
                }

                _isLoadingPreviousSlice = false;
                IsLoading = false;

                LoadPinnedMessagesSliceAsync(maxId.Value, VerticalAlignment.Bottom);
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
            if (fullInfo == null)
            {
                fullInfo = await ClientService.SendAsync(new GetUserFullInfo(user.Id)) as UserFullInfo;
            }

            if (fullInfo != null && fullInfo.BotInfo?.Description.Length > 0)
            {
                var result = Client.Execute(new GetTextEntities(fullInfo.BotInfo.Description)) as TextEntities;
                var entities = result?.Entities ?? new List<TextEntity>();

                foreach (var entity in entities)
                {
                    entity.Offset += Strings.Resources.BotInfoTitle.Length + Environment.NewLine.Length;
                }

                entities.Add(new TextEntity(0, Strings.Resources.BotInfoTitle.Length, new TextEntityTypeBold()));

                var message = $"{Strings.Resources.BotInfoTitle}{Environment.NewLine}{fullInfo.BotInfo.Description}";
                var text = new FormattedText(message, entities);

                MessageContent content;
                if (fullInfo.BotInfo.Animation != null)
                {
                    content = new MessageAnimation(fullInfo.BotInfo.Animation, text, false);
                }
                else if (fullInfo.BotInfo.Photo != null)
                {
                    content = new MessagePhoto(fullInfo.BotInfo.Photo, text, false, false);
                }
                else
                {
                    content = new MessageText(text, null);
                }

                Items.Insert(0, _messageFactory.Create(this, new Message(0, new MessageSenderUser(user.Id), chat.Id, null, null, false, false, false, false, false, true, false, false, false, false, false, false, false, false, false, false, false, 0, 0, null, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, content, null)));
                return;
            }

        AddDate:
            var thread = _thread;
            if (thread != null && !thread.Messages.Any(x => x.IsTopicMessage))
            {
                var replied = thread.Messages.OrderBy(x => x.Id)
                    .Select(x => _messageFactory.Create(this, x))
                    .ToList();

                await ProcessMessagesAsync(chat, replied);

                previous = replied[0];

                if (Items.Count > 0)
                {
                    Items.Insert(0, _messageFactory.Create(this, new Message(0, previous.SenderId, previous.ChatId, null, null, previous.IsOutgoing, false, false, false, false, true, false, false, false, false, false, false, false, false, previous.IsChannelPost, previous.IsTopicMessage, false, previous.Date, 0, null, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageCustomServiceAction(Strings.Resources.DiscussionStarted), null)));
                }
                else
                {
                    Items.Insert(0, _messageFactory.Create(this, new Message(0, previous.SenderId, previous.ChatId, null, null, previous.IsOutgoing, false, false, false, false, true, false, false, false, false, false, false, false, false, previous.IsChannelPost, previous.IsTopicMessage, false, previous.Date, 0, null, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageCustomServiceAction(Strings.Resources.NoComments), null)));
                }

                Items.Insert(0, previous);
                Items.Insert(0, _messageFactory.Create(this, new Message(0, previous.SenderId, previous.ChatId, null, null, previous.IsOutgoing, false, false, false, false, true, false, false, false, false, false, false, false, false, previous.IsChannelPost, previous.IsTopicMessage, false, previous.Date, 0, null, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageHeaderDate(), null)));
            }
            else if (previous != null)
            {
                Items.Insert(0, _messageFactory.Create(this, new Message(0, previous.SenderId, previous.ChatId, null, null, previous.IsOutgoing, false, false, false, false, true, false, false, false, false, false, false, false, false, previous.IsChannelPost, previous.IsTopicMessage, false, previous.Date, 0, null, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageHeaderDate(), null)));
            }
        }

        private async Task<MessageViewModel> PrepareMigratedAsync(long basicGroupId)
        {
            //if (_migratedChat != null)
            //{
            //    return null;
            //}

            var chat = _chat;
            if (chat == null)
            {
                return null;
            }

            var supergroup = ClientService.GetSupergroup(chat);
            if (supergroup == null)
            {
                return null;
            }

            var response = await ClientService.SendAsync(new CreateBasicGroupChat(basicGroupId, false));
            if (response is Chat migrated)
            {
                var messages = await ClientService.SendAsync(new GetChatHistory(migrated.Id, 0, 0, 1, false)) as Messages;
                if (messages != null && messages.MessagesValue.Count > 0)
                {
                    MigratedChat = migrated;
                    IsLastSliceLoaded = false;

                    return _messageFactory.Create(this, messages.MessagesValue[0]);
                }
            }

            return null;
        }

        public async void PreviousSlice()
        {
            if (_type is DialogType.ScheduledMessages or DialogType.EventLog)
            {
                var already = Items.LastOrDefault();
                if (already != null)
                {
                    var field = ListField;
                    if (field != null)
                    {
                        await field.ScrollToItem(already, VerticalAlignment.Bottom, false, int.MaxValue, ScrollIntoViewAlignment.Leading, false);
                    }
                }
            }
            else if (_repliesStack.Count > 0)
            {
                await LoadMessageSliceAsync(null, _repliesStack.Pop());
            }
            else
            {
                var chat = _chat;
                if (chat == null)
                {
                    return;
                }

                var thread = _thread;
                if (thread != null)
                {
                    await LoadMessageSliceAsync(null, thread.ReplyInfo?.LastMessageId ?? long.MaxValue, VerticalAlignment.Bottom, disableAnimation: false);
                }
                else
                {
                    await LoadMessageSliceAsync(null, chat.LastMessage?.Id ?? long.MaxValue, VerticalAlignment.Bottom, disableAnimation: false);
                }
            }

            TextField?.Focus(FocusState.Programmatic);
        }

        private async void LoadPinnedMessagesSliceAsync(long maxId, VerticalAlignment direction = VerticalAlignment.Center)
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

            if (direction == VerticalAlignment.Top)
            {
                var first = PinnedMessages.FirstOrDefault();
                if (first?.Id < maxId)
                {
                    return;
                }
            }
            else if (direction == VerticalAlignment.Bottom)
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

                LastPinnedMessage = _messageFactory.Create(this, pinned);
                //Delegate?.UpdatePinnedMessage(chat, LastPinnedMessage);
            }

            var offset = direction == VerticalAlignment.Top ? 0 : direction == VerticalAlignment.Bottom ? -49 : -25;
            var limit = 50;

            if (direction == VerticalAlignment.Center && (maxId == LastPinnedMessage?.Id || maxId == 0))
            {
                offset = -1;
                limit = 100;
            }

            var response = await ClientService.SendAsync(new SearchChatMessages(chat.Id, string.Empty, null, maxId, offset, limit, new SearchMessagesFilterPinned(), 0));
            if (response is Messages messages)
            {
                if (direction == VerticalAlignment.Center)
                {
                    PinnedMessages.Clear();
                }

                var replied = messages.MessagesValue.OrderByDescending(x => x.Id).Select(x => _messageFactory.Create(this, x)).ToList();

                foreach (var message in replied)
                {
                    if (direction == VerticalAlignment.Bottom)
                    {
                        InsertMessageInOrder(PinnedMessages, message);
                    }
                    else
                    {
                        PinnedMessages.Insert(0, message);
                    }
                }

                Delegate?.UpdatePinnedMessage();
            }
            else
            {
                Delegate?.UpdatePinnedMessage(chat, false);
            }
        }

        // This is to notify the view to update bindings
        public event EventHandler MessageSliceLoaded;

        private void NotifyMessageSliceLoaded()
        {
            MessageSliceLoaded?.Invoke(this, EventArgs.Empty);
            MessageSliceLoaded = null;
        }

        public async Task LoadMessageSliceAsync(long? previousId, long maxId, VerticalAlignment alignment = VerticalAlignment.Center, double? pixel = null, ScrollIntoViewAlignment? direction = null, bool? disableAnimation = null, bool second = false)
        {
            if (_type is not DialogType.History and not DialogType.Thread and not DialogType.Pinned)
            {
                NotifyMessageSliceLoaded();
                return;
            }

            if (direction == null)
            {
                var field = ListField;
                if (field != null)
                {
                    var panel = field.ItemsPanelRoot as ItemsStackPanel;
                    if (panel != null && panel.FirstVisibleIndex >= 0 && panel.FirstVisibleIndex < Items.Count && Items.Count > 0)
                    {
                        direction = Items[panel.FirstVisibleIndex].Id < maxId ? ScrollIntoViewAlignment.Default : ScrollIntoViewAlignment.Leading;
                    }
                }
            }

            if (alignment == VerticalAlignment.Bottom && pixel == null)
            {
                pixel = int.MaxValue;
            }

            var already = Items.FirstOrDefault(x => x.Id == maxId || x.Content is MessageAlbum album && album.Messages.ContainsKey(maxId));
            if (already != null)
            {
                var field = ListField;
                if (field != null)
                {
                    await field.ScrollToItem(already, alignment, alignment == VerticalAlignment.Center, pixel, direction ?? ScrollIntoViewAlignment.Leading, disableAnimation);
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
                    ScrollToBottom();
                }

                //var max = _dialog?.UnreadCount > 0 ? _dialog.ReadInboxMaxId : int.MaxValue;
                //var offset = _dialog?.UnreadCount > 0 && max > 0 ? -16 : 0;
                //await LoadFirstSliceAsync(max, offset);
                NotifyMessageSliceLoaded();
                return;
            }

            var chat = _chat;
            if (chat == null)
            {
                NotifyMessageSliceLoaded();
                return;
            }

            using (await _loadMoreLock.WaitAsync())
            {
                if (_isLoadingNextSlice || _isLoadingPreviousSlice || _chat?.Id != chat.Id)
                {
                    NotifyMessageSliceLoaded();
                    return;
                }

                _isLoadingNextSlice = true;
                _isLoadingPreviousSlice = true;
                IsLastSliceLoaded = null;
                IsFirstSliceLoaded = null;
                IsLoading = true;

                System.Diagnostics.Debug.WriteLine("DialogViewModel: LoadMessageSliceAsync");

                Function func;
                if (_threadId != 0)
                {
                    if (_thread.Messages.Any(x => x.Id == maxId))
                    {
                        func = new GetMessageThreadHistory(chat.Id, _threadId, 1, -25, 50);
                    }
                    else
                    {
                        func = new GetMessageThreadHistory(chat.Id, _threadId, maxId, -25, 50);
                    }
                }
                else if (_type == DialogType.Pinned)
                {
                    func = new SearchChatMessages(chat.Id, string.Empty, null, maxId, -25, 50, new SearchMessagesFilterPinned(), 0);
                }
                else
                {
                    func = new GetChatHistory(chat.Id, maxId, -25, 50, false);
                }

                var send = ClientService.SendAsync(func);

                if (alignment != VerticalAlignment.Center)
                {
                    var wait = await Task.WhenAny(send, Task.Delay(200));
                    if (wait != send)
                    {
                        Items.Clear();
                        NotifyMessageSliceLoaded();
                    }
                }

                var response = await send;
                if (response is Messages messages)
                {
                    if (_chat?.Id != chat.Id)
                    {
                        _isLoadingNextSlice = false;
                        _isLoadingPreviousSlice = false;
                        IsLoading = false;

                        NotifyMessageSliceLoaded();
                        return;
                    }

                    _groupedMessages.Clear();

                    var replied = new MessageCollection(null, messages.MessagesValue.Select(x => _messageFactory.Create(this, x)));
                    await ProcessMessagesAsync(chat, replied);

                    var firstVisibleIndex = -1;
                    var firstVisibleItem = default(MessageViewModel);

                    var unread = false;

                    if (alignment != VerticalAlignment.Center)
                    {
                        long lastReadMessageId;
                        long lastMessageId;

                        var thread = _thread;
                        if (thread != null)
                        {
                            lastReadMessageId = thread.ReplyInfo?.LastReadInboxMessageId ?? long.MaxValue;
                            lastMessageId = thread.ReplyInfo?.LastMessageId ?? long.MaxValue;
                        }
                        else
                        {
                            lastReadMessageId = chat.LastReadInboxMessageId;
                            lastMessageId = chat.LastMessage?.Id ?? long.MaxValue;
                        }

                        // If we're loading from the last read message
                        // then we want to skip it to align first unread message at top
                        if (lastReadMessageId != lastMessageId && maxId >= lastReadMessageId)
                        {
                            var target = default(MessageViewModel);
                            var index = -1;

                            for (int i = 0; i < replied.Count; i++)
                            {
                                var current = replied[i];
                                if (current.Id > lastReadMessageId)
                                {
                                    if (index == -1)
                                    {
                                        firstVisibleIndex = i;
                                        firstVisibleItem = current;
                                    }

                                    if (target == null && !current.IsOutgoing)
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
                                else if (firstVisibleIndex == -1 && i == replied.Count - 1)
                                {
                                    firstVisibleIndex = i;
                                    firstVisibleItem = current;
                                }
                            }

                            if (target != null)
                            {
                                if (index > 0)
                                {
                                    replied.Insert(index, _messageFactory.Create(this, new Message(0, target.SenderId, target.ChatId, null, null, target.IsOutgoing, false, false, false, false, true, false, false, false, false, false, false, false, false, target.IsChannelPost, target.IsTopicMessage, false, target.Date, 0, null, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageHeaderUnread(), null)));
                                    unread = true;
                                }
                                else if (maxId == lastReadMessageId)
                                {
                                    Logs.Logger.Debug(Logs.LogTarget.Chat, "Looking for first unread message, can't find it");
                                }

                                if (maxId == lastReadMessageId)
                                {
                                    maxId = target.Id;
                                    pixel = 28 + 48;
                                }
                            }
                        }

                        if (firstVisibleItem != null)
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

                    if (replied.Count > 0 && replied[0].Content is MessageChatUpgradeFrom chatUpgradeFrom)
                    {
                        var chatUpgradeTo = await PrepareMigratedAsync(chatUpgradeFrom.BasicGroupId);
                        if (chatUpgradeTo != null)
                        {
                            replied[0] = chatUpgradeTo;
                        }
                    }

                    if (firstVisibleIndex == -1)
                    {
                        firstVisibleItem = replied.FirstOrDefault(x => x.Id == maxId);

                        if (firstVisibleItem != null)
                        {
                            firstVisibleIndex = replied.IndexOf(firstVisibleItem);
                            unread = false;
                        }
                    }

                    if (firstVisibleIndex == -1)
                    {
                        System.Diagnostics.Debugger.Break();
                    }

                    if (alignment == VerticalAlignment.Bottom)
                    {
                        if (unread)
                        {
                            firstVisibleIndex++;
                        }

                        SetScrollMode(ItemsUpdatingScrollMode.KeepLastItemInView, true);
                        Items.RawReplaceWith(firstVisibleIndex == -1 ? replied : replied.Take(firstVisibleIndex + 1));
                    }
                    else if (alignment == VerticalAlignment.Top)
                    {
                        SetScrollMode(ItemsUpdatingScrollMode.KeepItemsInView, true);
                        Items.RawReplaceWith(firstVisibleIndex == -1 ? replied : replied.Skip(firstVisibleIndex));
                    }
                    else
                    {
                        SetScrollMode(direction == ScrollIntoViewAlignment.Default ? ItemsUpdatingScrollMode.KeepLastItemInView : ItemsUpdatingScrollMode.KeepItemsInView, true);
                        Items.RawReplaceWith(replied);
                    }

                    NotifyMessageSliceLoaded();

                    IsLastSliceLoaded = null;
                    IsFirstSliceLoaded = IsEndReached();

                    if (_thread != null && _thread.Messages.Any(x => x.Id == maxId))
                    {
                        await AddHeaderAsync();
                    }
                    else if (replied.IsEmpty())
                    {
                        await AddHeaderAsync();
                    }

                    await AddSponsoredMessagesAsync();
                }

                _isLoadingNextSlice = false;
                _isLoadingPreviousSlice = false;
                IsLoading = false;

                LoadPinnedMessagesSliceAsync(maxId, VerticalAlignment.Center);
            }

            await LoadMessageSliceAsync(previousId, maxId, alignment, pixel, direction, disableAnimation, true);
        }

        private async Task AddSponsoredMessagesAsync()
        {
            var chat = _chat;
            if (chat == null || chat.Type is not ChatTypeSupergroup supergroup || !supergroup.IsChannel)
            {
                return;
            }

            var response = await ClientService.SendAsync(new GetChatSponsoredMessage(chat.Id));
            if (response is SponsoredMessage sponsored)
            {
                SponsoredMessage = sponsored;
                //Items.Add(_messageFactory.Create(this, new Message(0, new MessageSenderChat(sponsored.SponsorChatId), sponsored.SponsorChatId, null, null, false, false, false, false, false, false, false, false, false, false, false, false, true, false, 0, 0, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, sponsored.Content, null)));
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

                if (_isLoadingNextSlice || _isLoadingPreviousSlice)
                {
                    return;
                }

                _isLoadingNextSlice = true;
                _isLoadingPreviousSlice = true;
                IsLastSliceLoaded = null;
                IsFirstSliceLoaded = null;
                IsLoading = true;

                System.Diagnostics.Debug.WriteLine("DialogViewModel: LoadScheduledSliceAsync");

                var response = await ClientService.SendAsync(new GetChatScheduledMessages(chat.Id));
                if (response is Messages messages)
                {
                    _groupedMessages.Clear();

                    if (messages.MessagesValue.Count > 0)
                    {
                        SetScrollMode(ItemsUpdatingScrollMode.KeepLastItemInView, true);
                        Logs.Logger.Debug(Logs.LogTarget.Chat, "Setting scroll mode to KeepLastItemInView");
                    }

                    var replied = messages.MessagesValue.OrderBy(x => x.Id).Select(x => _messageFactory.Create(this, x)).ToList();
                    await ProcessMessagesAsync(chat, replied);

                    var target = replied.FirstOrDefault();
                    if (target != null)
                    {
                        replied.Insert(0, _messageFactory.Create(this, new Message(0, target.SenderId, target.ChatId, null, target.SchedulingState, target.IsOutgoing, false, false, false, false, true, false, false, false, false, false, false, false, false, target.IsChannelPost, target.IsTopicMessage, false, target.Date, 0, null, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageHeaderDate(), null)));
                    }

                    Items.ReplaceWith(replied);

                    IsLastSliceLoaded = true;
                    IsFirstSliceLoaded = true;
                }

                _isLoadingNextSlice = false;
                _isLoadingPreviousSlice = false;
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
                var field = ListField;
                if (field == null)
                {
                    return;
                }

                field.ScrollToBottom();
                field.SetScrollMode(ItemsUpdatingScrollMode.KeepLastItemInView, true);
            }
        }

        public MessageCollection Items { get; } = new MessageCollection();

        public MessageViewModel CreateMessage(Message message)
        {
            if (message == null)
            {
                return null;
            }

            return _messageFactory.Create(this, message);
        }

        protected Task ProcessMessagesAsync(Chat chat, IList<MessageViewModel> messages)
        {
            ProcessAlbums(chat, messages);

            for (int i = 0; i < messages.Count; i++)
            {
                var message = messages[i];
                if (message.Content is MessageForumTopicCreated && Type == DialogType.Thread)
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

                ProcessEmoji(message);
                ProcessReplies(chat, message);
            }

            return Task.CompletedTask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessEmoji(MessageViewModel message)
        {
            if (ClientService.Options.DisableAnimatedEmoji)
            {
                return;
            }

            if (message.Content is MessageText text && text.WebPage == null && !text.Text.Entities.Any(/*x => x.Type is not TextEntityTypeCustomEmoji*/))
            {
                if (Emoji.TryCountEmojis(text.Text.Text, out int count, 3))
                {
                    message.GeneratedContent = new MessageBigEmoji(text.Text, count);
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
            var groups = new Dictionary<long, Tuple<MessageViewModel, MessageAlbum>>();
            var newGroups = new Dictionary<long, long>();

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

                    group = _messageFactory.Create(this, groupBase);

                    slice[i] = group;
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
                    groups[groupedId] = Tuple.Create(group, album);

                    album.Messages.Add(message);

                    var first = album.Messages.FirstOrDefault();
                    if (first != null)
                    {
                        group.UpdateWith(first);
                    }
                }
            }

            foreach (var group in groups.Values)
            {
                group.Item2.Invalidate();

                if (newGroups.ContainsKey(group.Item1.MediaAlbumId))
                {
                    continue;
                }

                Handle(new UpdateMessageContent(chat.Id, group.Item1.Id, group.Item1.Content));
                Handle(new UpdateMessageEdited(chat.Id, group.Item1.Id, group.Item1.EditDate, group.Item1.ReplyMarkup));
                Handle(new UpdateMessageInteractionInfo(chat.Id, group.Item1.Id, group.Item1.InteractionInfo));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessReplies(Chat chat, MessageViewModel message)
        {
            var thread = _thread;
            if (thread?.ChatId == message.ReplyInChatId &&
                thread?.Messages[thread.Messages.Count - 1].Id == message.ReplyToMessageId)
            {
                message.ReplyToMessageState = ReplyToMessageState.Hidden;
                return;
            }

            if (message.ReplyToMessageId != 0 ||
                message.Content is MessagePinMessage ||
                message.Content is MessageGameScore ||
                message.Content is MessagePaymentSuccessful)
            {
                message.ReplyToMessageState = ReplyToMessageState.Loading;

                ClientService.Send(new GetRepliedMessage(message.ChatId, message.Id), response =>
                {
                    if (response is Message result)
                    {
                        message.ReplyToMessage = _messageFactory.Create(this, result);
                        message.ReplyToMessageState = ReplyToMessageState.None;
                    }
                    else
                    {
                        message.ReplyToMessageState = ReplyToMessageState.Deleted;
                    }

                    BeginOnUIThread(() => Handle(message,
                        bubble => bubble.UpdateMessageReply(message),
                        service => service.UpdateMessage(message)));
                });
            }
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is string pair)
            {
                var split = pair.Split(';');
                if (split.Length != 2)
                {
                    return;
                }

                var failed1 = !long.TryParse(split[0], out long result1);
                var failed2 = !long.TryParse(split[1], out long result2);

                if (failed1 || failed2)
                {
                    return;
                }

                Thread = await ClientService.SendAsync(new GetMessageThread(result1, result2)) as MessageThreadInfo;
                parameter = Thread?.ChatId;

                if (parameter == null)
                {
                    return;
                }

                // TODO: replace with real code
                var message = Thread.Messages.FirstOrDefault();
                if (message != null && message.Content is MessageForumTopicCreated forumTopicCreated)
                {
                    Topic = new ForumTopicInfo(message.MessageThreadId, forumTopicCreated.Name, forumTopicCreated.Icon, message.Date, message.SenderId, message.IsOutgoing, false);
                }
            }

            var chat = ClientService.GetChat((long)parameter);
            if (chat == null)
            {
                chat = await ClientService.SendAsync(new GetChat((long)parameter)) as Chat;
            }

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

            if (state.TryGet("access_token", out string accessToken))
            {
                state.Remove("access_token");
                AccessToken = accessToken;
            }

            if (state.TryGet("search", out bool search))
            {
                state.Remove("search");
                SearchExecute();
            }

#pragma warning disable CS4014
            if (_type == DialogType.ScheduledMessages)
            {
                Logs.Logger.Debug(Logs.LogTarget.Chat, string.Format("{0} - Loadings scheduled messages", chat.Id));

                NotifyMessageSliceLoaded();
                LoadScheduledSliceAsync();
            }
            else if (_type == DialogType.EventLog)
            {
                Logs.Logger.Debug(Logs.LogTarget.Chat, string.Format("{0} - Loadings event log", chat.Id));

                NotifyMessageSliceLoaded();
                LoadEventLogSliceAsync();
            }
            else if (state.TryGet("message_id", out long navigation))
            {
                Logs.Logger.Debug(Logs.LogTarget.Chat, string.Format("{0} - Loading messages from specific id", chat.Id));

                state.Remove("message_id");
                LoadMessageSliceAsync(null, navigation);
            }
            else
            {
                bool TryRemove(long chatId, out long v1, out long v2)
                {
                    var a = Settings.Chats.TryRemove(chat.Id, _threadId, ChatSetting.ReadInboxMaxId, out v1);
                    var b = Settings.Chats.TryRemove(chat.Id, _threadId, ChatSetting.Index, out v2);
                    return a && b;
                }

                long lastReadMessageId;
                long lastMessageId;

                var thread = _thread;
                if (thread != null)
                {
                    lastReadMessageId = thread.ReplyInfo?.LastReadInboxMessageId ?? long.MaxValue;
                    lastMessageId = thread.ReplyInfo?.LastMessageId ?? long.MaxValue;
                }
                else
                {
                    lastReadMessageId = chat.LastReadInboxMessageId;
                    lastMessageId = chat.LastMessage?.Id ?? long.MaxValue;
                }

                if (TryRemove(chat.Id, out long readInboxMaxId, out long start) &&
                    readInboxMaxId == lastReadMessageId &&
                    start < lastReadMessageId)
                {
                    if (Settings.Chats.TryRemove(chat.Id, _threadId, ChatSetting.Pixel, out double pixel))
                    {
                        Logs.Logger.Debug(Logs.LogTarget.Chat, string.Format("{0} - Loading messages from specific pixel", chat.Id));

                        LoadMessageSliceAsync(null, start, VerticalAlignment.Bottom, pixel);
                    }
                    else
                    {
                        Logs.Logger.Debug(Logs.LogTarget.Chat, string.Format("{0} - Loading messages from specific id, pixel missing", chat.Id));

                        LoadMessageSliceAsync(null, start, VerticalAlignment.Bottom);
                    }
                }
                else /*if (chat.UnreadCount > 0)*/
                {
                    Logs.Logger.Debug(Logs.LogTarget.Chat, string.Format("{0} - Loading messages from LastReadInboxMessageId: {1}", chat.Id, chat.LastReadInboxMessageId));

                    LoadMessageSliceAsync(null, lastReadMessageId, VerticalAlignment.Top);
                }
                //else
                //{
                //    Logs.Logger.Debug(Logs.Target.Chat, string.Format("{0} - Loading messages from LastMessageId: {1}", chat.Id, chat.LastMessage?.Id));

                //    LoadMessageSliceAsync(null, chat.LastMessage?.Id ?? long.MaxValue, VerticalAlignment.Bottom);
                //}
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
                Items.Add(_messageFactory.Create(this, new Message(0, new MessageSenderUser(0), chat.Id, null, null, true,  false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(14, 58), 0, null, null, null, 0, 0,  0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("Hey Eileen", new TextEntity[0]), null), null)));
                Items.Add(_messageFactory.Create(this, new Message(1, new MessageSenderUser(0), chat.Id, null, null, true,  false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(14, 59), 0, null, null, null, 0, 0,  0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("So, why is Telegram cool?", new TextEntity[0]), null), null)));
                Items.Add(_messageFactory.Create(this, new Message(2, new MessageSenderUser(7), chat.Id, null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(14, 59), 0, null, null, null, 0, 0,  0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("Well, look. Telegram is superfast and you can use it on all your devices at the same time - phones, tablets, even desktops.", new TextEntity[0]), null), null)));
                Items.Add(_messageFactory.Create(this, new Message(3, new MessageSenderUser(0), chat.Id, null, null, true,  false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(14, 59), 0, null, null, null, 0, 0,  0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageBigEmoji(new FormattedText("😴", new TextEntity[0]), 1), null)));
                Items.Add(_messageFactory.Create(this, new Message(4, new MessageSenderUser(7), chat.Id, null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(15, 00), 0, null, null, null, 0, 0,  0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("And it has secret chats, like this one, with end-to-end encryption!", new TextEntity[0]), null), null)));
                Items.Add(_messageFactory.Create(this, new Message(5, new MessageSenderUser(0), chat.Id, null, null, true,  false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(15, 00), 0, null, null, null, 0, 0,  0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("End encryption to what end??", new TextEntity[0]), null), null)));
                Items.Add(_messageFactory.Create(this, new Message(6, new MessageSenderUser(7), chat.Id, null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(15, 01), 0, null, null, null, 0, 0,  0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("Arrgh. Forget it. You can set a timer and send photos that will disappear when the time rush out. Yay!", new TextEntity[0]), null), null)));
                Items.Add(_messageFactory.Create(this, new Message(7, new MessageSenderUser(7), chat.Id, null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(15, 01), 0, null, null, null, 0, 0,  0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageChatSetTtl(15), null)));
                Items.Add(_messageFactory.Create(this, new Message(8, new MessageSenderUser(0), chat.Id, null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(15, 05), 0, null, null, null, 0, 0, 0, 15, 0, 0, string.Empty, 0, string.Empty, new MessagePhoto(new Photo(false, null, new[] { new PhotoSize("t", new File(0, 0, 0, new LocalFile(System.IO.Path.Combine(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Assets\\Mockup\\hot.png"), true, true, false, true, 0, 0, 0), new RemoteFile()), 800, 500, null), new PhotoSize("i", new File(0, 0, 0, new LocalFile(System.IO.Path.Combine(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Assets\\Mockup\\hot.png"), true, true, false, true, 0, 0, 0), new RemoteFile()), 800, 500, null) }), new FormattedText(string.Empty, new TextEntity[0]), true), null)));

                SetText("😱🙈👍");
            }
            else
            {
                Items.Add(_messageFactory.Create(this, new Message(4, new MessageSenderUser(11), chat.Id, null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(15, 25), 0, null, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageSticker(new Sticker(0, 512, 512, "", new StickerTypeStatic(), null, null, null, new File(0, 0, 0, new LocalFile(System.IO.Path.Combine(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Assets\\Mockup\\sticker0.webp"), true, true, false, true, 0, 0, 0), null)), false), null)));
                Items.Add(_messageFactory.Create(this, new Message(1, new MessageSenderUser(9),  chat.Id, null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(15, 26), 0, null, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("Are you sure it's safe here?", new TextEntity[0]), null), null)));
                Items.Add(_messageFactory.Create(this, new Message(2, new MessageSenderUser(7),  chat.Id, null, null, true,  false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(15, 27), 0, null, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("Yes, sure, don't worry.", new TextEntity[0]), null), null)));
                Items.Add(_messageFactory.Create(this, new Message(3, new MessageSenderUser(13), chat.Id, null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(15, 27), 0, null, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("Hallo alle zusammen! Is the NSA reading this? 😀", new TextEntity[0]), null), null)));
                Items.Add(_messageFactory.Create(this, new Message(4, new MessageSenderUser(9),  chat.Id, null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(15, 29), 0, null, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageSticker(new Sticker(0, 512, 512, "", new StickerTypeStatic(), null, null, null, new File(0, 0, 0, new LocalFile(System.IO.Path.Combine(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Assets\\Mockup\\sticker1.webp"), true, true, false, true, 0, 0, 0), null)), false), null)));
                Items.Add(_messageFactory.Create(this, new Message(5, new MessageSenderUser(10), chat.Id, null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(15, 29), 0, null, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("Sorry, I'll have to publish this conversation on the web.", new TextEntity[0]), null), null)));
                Items.Add(_messageFactory.Create(this, new Message(6, new MessageSenderUser(10), chat.Id, null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(15, 01), 0, null, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageChatDeleteMember(10), null)));
                Items.Add(_messageFactory.Create(this, new Message(7, new MessageSenderUser(8),  chat.Id, null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, TodayDate(15, 30), 0, null, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageText(new FormattedText("Wait, we could have made so much money on this!", new TextEntity[0]), null), null)));
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

                ClientService.Send(new GetChatAdministrators(chat.Id), result =>
                {
                    if (result is ChatAdministrators users)
                    {
                        _admins[chat.Id] = users.Administrators;
                    }
                });
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

                ClientService.Send(new GetChatAdministrators(chat.Id), result =>
                {
                    if (result is ChatAdministrators users)
                    {
                        _admins[chat.Id] = users.Administrators;
                    }
                });
            }

            UpdateGroupCall(chat, chat.VideoChat.GroupCallId);

            ShowReplyMarkup(chat);
            ShowDraftMessage(chat);
            ShowSwitchInline(state);

            if (_type == DialogType.History && App.DataPackages.TryRemove(chat.Id, out DataPackageView package))
            {
                await HandlePackageAsync(package);
            }
        }

        public override void NavigatingFrom(NavigatingEventArgs args)
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

            OnlineCount = null;

            PinnedMessages.Clear();
            LastPinnedMessage = null;
            LockedPinnedMessageId = 0;

            DisposeSearch();
            IsSelectionEnabled = false;

            IsLastSliceLoaded = null;
            IsFirstSliceLoaded = null;

            ClientService.Send(new CloseChat(chat.Id));

            try
            {
                var field = ListField;
                if (field == null)
                {
                    Settings.Chats.TryRemove(chat.Id, _threadId, ChatSetting.Index, out long index);
                    Settings.Chats.TryRemove(chat.Id, _threadId, ChatSetting.Pixel, out double pixel);

                    Logs.Logger.Debug(Logs.LogTarget.Chat, string.Format("{0} - Removing scrolling position, generic reason", chat.Id));

                    return;
                }

                var panel = field.ItemsPanelRoot as ItemsStackPanel;
                if (panel != null && panel.LastVisibleIndex >= 0 && panel.LastVisibleIndex < Items.Count - 1 && Items.Count > 0)
                {
                    var start = Items[panel.LastVisibleIndex].Id;
                    if (start != chat.LastMessage?.Id)
                    {
                        long lastReadMessageId;

                        var thread = _thread;
                        if (thread != null)
                        {
                            lastReadMessageId = thread.ReplyInfo?.LastReadInboxMessageId ?? long.MaxValue;
                        }
                        else
                        {
                            lastReadMessageId = chat.LastReadInboxMessageId;
                        }

                        var container = field.ContainerFromIndex(panel.LastVisibleIndex) as ListViewItem;
                        if (container != null)
                        {
                            var transform = container.TransformToVisual(field);
                            var position = transform.TransformPoint(new Point());

                            Settings.Chats[chat.Id, _threadId, ChatSetting.ReadInboxMaxId] = lastReadMessageId;
                            Settings.Chats[chat.Id, _threadId, ChatSetting.Index] = start;
                            Settings.Chats[chat.Id, _threadId, ChatSetting.Pixel] = field.ActualHeight - (position.Y + container.ActualHeight);

                            Logs.Logger.Debug(Logs.LogTarget.Chat, string.Format("{0} - Saving scrolling position, message: {1}, pixel: {2}", chat.Id, Items[panel.LastVisibleIndex].Id, field.ActualHeight - (position.Y + container.ActualHeight)));
                        }
                        else
                        {
                            Settings.Chats[chat.Id, _threadId, ChatSetting.ReadInboxMaxId] = lastReadMessageId;
                            Settings.Chats[chat.Id, _threadId, ChatSetting.Index] = start;
                            Settings.Chats.TryRemove(chat.Id, _threadId, ChatSetting.Pixel, out double pixel);

                            Logs.Logger.Debug(Logs.LogTarget.Chat, string.Format("{0} - Saving scrolling position, message: {1}, pixel: none", chat.Id, Items[panel.LastVisibleIndex].Id));
                        }

                    }
                    else
                    {
                        Settings.Chats.TryRemove(chat.Id, _threadId, ChatSetting.ReadInboxMaxId, out long _);
                        Settings.Chats.TryRemove(chat.Id, _threadId, ChatSetting.Index, out long _);
                        Settings.Chats.TryRemove(chat.Id, _threadId, ChatSetting.Pixel, out double _);

                        Logs.Logger.Debug(Logs.LogTarget.Chat, string.Format("{0} - Removing scrolling position, as last item is chat.LastMessage", chat.Id));
                    }
                }
                else
                {
                    Settings.Chats.TryRemove(chat.Id, _threadId, ChatSetting.ReadInboxMaxId, out long _);
                    Settings.Chats.TryRemove(chat.Id, _threadId, ChatSetting.Index, out long _);
                    Settings.Chats.TryRemove(chat.Id, _threadId, ChatSetting.Pixel, out double _);

                    Logs.Logger.Debug(Logs.LogTarget.Chat, string.Format("{0} - Removing scrolling position, generic reason", chat.Id));
                }
            }
            catch
            {
                Settings.Chats.TryRemove(chat.Id, _threadId, ChatSetting.ReadInboxMaxId, out long _);
                Settings.Chats.TryRemove(chat.Id, _threadId, ChatSetting.Index, out long _);
                Settings.Chats.TryRemove(chat.Id, _threadId, ChatSetting.Pixel, out double _);

                Logs.Logger.Debug(Logs.LogTarget.Chat, string.Format("{0} - Removing scrolling position, exception", chat.Id));
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
                    Delegate?.UpdateChatReplyMarkup(chat, _messageFactory.Create(this, message));
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
                var previous = _draft?.InputMessageText as InputMessageText;

                if (previous != null && !string.Equals(previous.Text.Text, current.Text))
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

                if (draft.InputMessageText is InputMessageText text)
                {
                    SetText(text.Text);
                }

                if (draft.ReplyToMessageId != 0)
                {
                    var response = await ClientService.SendAsync(new GetMessage(chat.Id, draft.ReplyToMessageId));
                    if (response is Message message)
                    {
                        ComposerHeader = new MessageComposerHeader { ReplyToMessage = _messageFactory.Create(this, message) };
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

            var reply = 0L;

            if (embedded != null && embedded.ReplyToMessage != null)
            {
                reply = embedded.ReplyToMessage.Id;
            }

            DraftMessage draft = null;
            if (!string.IsNullOrWhiteSpace(formattedText.Text) || reply != 0)
            {
                if (formattedText.Text.Length > ClientService.Options.MessageTextLengthMax * 4)
                {
                    formattedText = formattedText.Substring(0, ClientService.Options.MessageTextLengthMax * 4);
                }

                draft = new DraftMessage(reply, 0, new InputMessageText(formattedText, false, false));
            }

            ClientService.Send(new SetChatDraftMessage(_chat.Id, _threadId, draft));
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
                ComposerHeader = new MessageComposerHeader
                {
                    EditingMessage = container.EditingMessage,
                    ReplyToMessage = container.ReplyToMessage,
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

        private long GetReply(bool clean, bool notify = true)
        {
            var embedded = _composerHeader;
            if (embedded == null || embedded.ReplyToMessage == null)
            {
                return 0;
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

            return embedded.ReplyToMessage.Id;
        }

        #endregion

        public async void SendMessage(string args)
        {
            await SendMessageAsync(args);
        }

        public Task SendMessageAsync(FormattedText formattedText, MessageSendOptions options = null)
        {
            return SendMessageAsync(formattedText.Text, formattedText.Entities, options);
        }

        public async Task SendMessageAsync(string text, IList<TextEntity> entities = null, MessageSendOptions options = null)
        {
            text = text.Replace('\v', '\n').Replace('\r', '\n');

            if (text.StartsWith("/k8"))
            {
                var data = text.Split(' ');
                var emoji = data[1];

                var sticker = await ClientService.SendAsync(new GetAnimatedEmoji(emoji)) as AnimatedEmoji;
                if (sticker != null)
                {
                    if (sticker.Sticker.StickerValue.Local.IsDownloadingCompleted)
                    {
                        var arguments = new GenerationService.ChatPhotoConversion
                        {
                            StickerFileId = sticker.Sticker.StickerValue.Id,
                            Scale = 1
                        };

                        var background = ClientService.GetChatTheme(_chat.ThemeName)?.LightSettings.Background;
                        if (background == null)
                        {
                            background = ClientService.GetSelectedBackground(false);
                        }

                        if (background != null)
                        {
                            var url = await ClientService.SendAsync(new GetBackgroundUrl(background.Name, background.Type)) as HttpUrl;
                            if (url != null)
                            {
                                arguments.BackgroundUrl = url.Url;
                            }
                        }

                        ClientService.Send(new SendMessage(_chat.Id, 0, 0, null, null, new InputMessageAnimation(new InputFileGenerated("animation.mp4", "token" + "#" + ConversionType.ChatPhoto + "#" + Newtonsoft.Json.JsonConvert.SerializeObject(arguments) + "#" + DateTime.Now.ToString("s"), 0), null, null, 0, 640, 640, null)));
                        return;
                    }
                    else
                    {
                        ClientService.DownloadFile(sticker.Sticker.StickerValue.Id, 32);
                    }
                }
            }

            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            FormattedText formattedText;
            if (entities == null)
            {
                formattedText = GetFormattedText(text);
            }
            else
            {
                formattedText = new FormattedText(text, entities);
            }

            if (options == null)
            {
                options = await PickMessageSendOptionsAsync();
            }

            if (options == null)
            {
                return;
            }

            var header = _composerHeader;
            var disablePreview = header?.WebPageDisabled ?? false;

            if (header?.EditingMessage != null)
            {
                var editing = header.EditingMessage;

                var factory = header.EditingMessageMedia;
                if (factory != null)
                {
                    var input = factory.Delegate(factory.InputFile, header.EditingMessageCaption);

                    var response = await ClientService.SendAsync(new SendMessageAlbum(editing.ChatId, editing.MessageThreadId, editing.ReplyToMessageId, null, new[] { input }, true));
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
                        if (error.TypeEquals(ErrorType.MESSAGE_NOT_MODIFIED))
                        {
                            ShowDraftMessage(chat);
                        }
                        else
                        {
                            // TODO: ...
                        }
                    }
                }
            }
            else
            {
                var reply = GetReply(true, options?.SchedulingState != null);

                //if (string.Equals(text.Trim(), "\uD83C\uDFB2"))
                if (ClientService.IsDiceEmoji(text, out string dice))
                {
                    var input = new InputMessageDice(dice, true);
                    await SendMessageAsync(chat, reply, input, options);
                }
                else
                {
                    if (text.Length > ClientService.Options.MessageTextLengthMax)
                    {
                        foreach (var split in formattedText.Split(ClientService.Options.MessageTextLengthMax))
                        {
                            var input = new InputMessageText(split, disablePreview, true);
                            await SendMessageAsync(chat, reply, input, options);
                        }
                    }
                    else if (text.Length > 0)
                    {
                        var input = new InputMessageText(formattedText, disablePreview, true);
                        await SendMessageAsync(chat, reply, input, options);
                    }
                    else
                    {
                        await LoadMessageSliceAsync(null, chat.LastMessage?.Id ?? long.MaxValue, VerticalAlignment.Bottom);
                    }
                }
            }


            /*                        if (response.Error.TypeEquals(TLErrorType.PEER_FLOOD))
                        {
                            var dialog = new MessagePopup();
                            dialog.Title = "Telegram";
                            dialog.Message = "Sorry, you can only send messages to mutual contacts at the moment.";
                            dialog.PrimaryButtonText = "More info";
                            dialog.SecondaryButtonText = "OK";

                            var confirm = await dialog.ShowQueuedAsync();
                            if (confirm == ContentDialogResult.Primary)
                            {
                                MessageHelper.HandleTelegramUrl("t.me/SpamBot");
                            }
                        }
*/
            //ClientService.Send(new SendMessage())
        }

        public RelayCommand<long> OpenUserCommand { get; }

        #region Set default message sender

        public RelayCommand<ChatMessageSender> SetSenderCommand;
        public async void SetSenderExecute(ChatMessageSender messageSender)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (messageSender.NeedsPremium && !IsPremium)
            {
                await MessagePopup.ShowAsync(Strings.Resources.SelectSendAsPeerPremiumHint, Strings.Resources.AppName, Strings.Resources.OK);
                return;
            }

            ClientService.Send(new SetChatMessageSender(chat.Id, messageSender.Sender));
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

            }
        }

        #endregion

        #region Toggle mute

        public RelayCommand MuteCommand { get; }
        public RelayCommand UnmuteCommand { get; }

        public RelayCommand<bool> ToggleMuteCommand { get; }
        private void ToggleMuteExecute(bool unmute)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            _pushService.SetMuteFor(chat, unmute ? 0 : 632053052);
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

        public RelayCommand<ChatReportReason> ReportSpamCommand { get; }
        private async void ReportSpamExecute(ChatReportReason reason)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var title = Strings.Resources.AppName;
            var message = Strings.Resources.ReportSpamAlert;

            if (reason is ChatReportReasonUnrelatedLocation)
            {
                title = Strings.Resources.ReportUnrelatedGroup;

                var fullInfo = ClientService.GetSupergroupFull(chat);
                if (fullInfo != null && fullInfo.Location.Address.Length > 0)
                {
                    message = string.Format(Strings.Resources.ReportUnrelatedGroupText, fullInfo.Location.Address);
                }
                else
                {
                    message = Strings.Resources.ReportUnrelatedGroupTextNoAddress;
                }
            }
            else if (reason is ChatReportReasonSpam)
            {
                if (chat.Type is ChatTypeSupergroup supergroup)
                {
                    message = supergroup.IsChannel ? Strings.Resources.ReportSpamAlertChannel : Strings.Resources.ReportSpamAlertGroup;
                }
                else if (chat.Type is ChatTypeBasicGroup)
                {
                    message = Strings.Resources.ReportSpamAlertGroup;
                }
            }

            var confirm = await MessagePopup.ShowAsync(message, title, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            ClientService.Send(new ReportChat(chat.Id, new long[0], reason, string.Empty));

            if (chat.Type is ChatTypeBasicGroup or ChatTypeSupergroup)
            {
                ClientService.Send(new LeaveChat(chat.Id));
            }
            else if (chat.Type is ChatTypePrivate privata)
            {
                ClientService.Send(new ToggleMessageSenderIsBlocked(new MessageSenderUser(privata.UserId), true));
            }
            else if (chat.Type is ChatTypeSecret secret)
            {
                ClientService.Send(new ToggleMessageSenderIsBlocked(new MessageSenderUser(secret.UserId), true));
            }

            ClientService.Send(new DeleteChatHistory(chat.Id, true, false));
        }

        #endregion

        #region Delete and Exit

        public RelayCommand ChatDeleteCommand { get; }
        private async void ChatDeleteExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var updated = await ClientService.SendAsync(new GetChat(chat.Id)) as Chat ?? chat;
            var dialog = new DeleteChatPopup(ClientService, updated, null, false);

            var confirm = await dialog.ShowQueuedAsync();
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
                        await ClientService.SendAsync(new ToggleMessageSenderIsBlocked(new MessageSenderUser(privata.UserId), true));
                    }

                    ClientService.Send(new DeleteChatHistory(updated.Id, true, false));
                }
            }
        }

        #endregion

        #region Clear history

        public RelayCommand ChatClearCommand { get; }
        private async void ChatClearExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var updated = await ClientService.SendAsync(new GetChat(chat.Id)) as Chat ?? chat;
            var dialog = new DeleteChatPopup(ClientService, updated, null, true);

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                ClientService.Send(new DeleteChatHistory(updated.Id, false, dialog.IsChecked));
            }
        }

        #endregion

        #region Call

        public RelayCommand<bool> CallCommand { get; }
        private async void CallExecute(bool video)
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
                await _groupCallService.JoinAsync(chat.Id);
            }
        }

        #endregion

        #region Unpin message

        public RelayCommand PinnedHideCommand { get; }
        private async void PinnedHideExecute()
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
                var confirm = await MessagePopup.ShowAsync(Strings.Resources.UnpinMessageAlert, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
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

        public RelayCommand PinnedShowCommand { get; }
        private void PinnedShowExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            Settings.SetChatPinnedMessage(chat.Id, 0);
            LoadPinnedMessagesSliceAsync(0, VerticalAlignment.Center);
        }

        public RelayCommand PinnedListCommand { get; }
        private void PinnedListExecute()
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
                await ClientService.SendAsync(new ToggleMessageSenderIsBlocked(new MessageSenderUser(user.Id), false));
                Start();
            }
            else
            {
                var confirm = await MessagePopup.ShowAsync(Strings.Resources.AreYouSureUnblockContact, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }

                ClientService.Send(new ToggleMessageSenderIsBlocked(new MessageSenderUser(user.Id), false));
            }
        }

        #endregion

        #region Switch

        public RelayCommand<string> SwitchCommand { get; }
        private async void SwitchExecute(string start)
        {
            if (_currentInlineBot == null)
            {
                return;
            }

            var response = await ClientService.SendAsync(new CreatePrivateChat(_currentInlineBot.Id, false));
            if (response is Chat chat)
            {
                SetText(null, false);

                ClientService.Send(new SendBotStartMessage(_currentInlineBot.Id, chat.Id, start ?? string.Empty));
                NavigationService.NavigateToChat(chat);
            }
        }

        #endregion

        #region Share my contact

        public RelayCommand ShareContactCommand { get; }
        private void ShareContactExecute()
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

        public RelayCommand UnarchiveCommand { get; }
        private void UnarchiveExecute()
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

        public RelayCommand InviteCommand { get; }
        private async void InviteExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup or ChatTypeBasicGroup)
            {
                var selected = await SharePopup.PickUsersAsync(ClientService, Strings.Resources.SelectContact);
                if (selected == null || selected.Count == 0)
                {
                    return;
                }

                var userIds = selected.Select(x => x.Id).ToArray();
                var count = Locale.Declension("Users", selected.Count);

                var title = string.Format(Strings.Resources.AddMembersAlertTitle, count);
                var message = string.Format(Strings.Resources.AddMembersAlertCountText, count, chat.Title);

                var confirm = await MessagePopup.ShowAsync(message, title, Strings.Resources.Add, Strings.Resources.Cancel);
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }

                var response = await ClientService.SendAsync(new AddChatMembers(chat.Id, userIds));
                if (response is Error error)
                {

                }
            }
        }

        #endregion

        #region Add contact

        public RelayCommand AddContactCommand { get; }
        private async void AddContactExecute()
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

            var fullInfo = ClientService.GetUserFull(chat);
            if (fullInfo == null)
            {
                return;
            }

            var dialog = new EditUserNamePopup(user.FirstName, user.LastName, fullInfo.NeedPhoneNumberPrivacyException);

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                ClientService.Send(new AddContact(new Contact(user.PhoneNumber, dialog.FirstName, dialog.LastName, string.Empty, user.Id),
                    fullInfo.NeedPhoneNumberPrivacyException ? dialog.SharePhoneNumber : true));
            }
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

        public RelayCommand SearchCommand { get; }
        private void SearchExecute()
        {
            if (Search == null)
            {
                Search = new ChatSearchViewModel(ClientService, Settings, Aggregator, this);
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

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary && dialog.SelectedDates.Count > 0)
            {
                var first = dialog.SelectedDates.FirstOrDefault();
                var offset = first.Date.ToTimestamp();

                await LoadDateSliceAsync(offset);
            }
        }

        #endregion

        #region Group stickers

        public RelayCommand GroupStickersCommand { get; }
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

            if (_threadId != 0)
            {
                ClientService.Send(new ReadAllMessageThreadMentions(chat.Id, _threadId));
            }
            else
            {
                ClientService.Send(new ReadAllChatMentions(chat.Id));
            }
        }

        #endregion

        #region Mute for

        public RelayCommand<int?> MuteForCommand { get; }
        private async void MuteForExecute(int? value)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (value is int update)
            {
                _pushService.SetMuteFor(chat, update);
            }
            else
            {
                var mutedFor = Settings.Notifications.GetMutedFor(chat);
                var popup = new ChatMutePopup(mutedFor);

                var confirm = await popup.ShowQueuedAsync();
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }

                if (mutedFor != popup.Value)
                {
                    _pushService.SetMuteFor(chat, popup.Value);
                }
            }
        }

        #endregion

        #region Report Chat

        public RelayCommand ReportCommand { get; }
        private async void ReportExecute()
        {
            await ReportAsync(new long[0]);
        }

        private async Task ReportAsync(IList<long> messages)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var items = new[]
            {
                new ChooseOptionItem(new ChatReportReasonSpam(), Strings.Resources.ReportChatSpam, true),
                new ChooseOptionItem(new ChatReportReasonViolence(), Strings.Resources.ReportChatViolence, false),
                new ChooseOptionItem(new ChatReportReasonChildAbuse(), Strings.Resources.ReportChatChild, false),
                //new ChooseOptionItem(new ChatReportReasonIllegalDrugs(), Strings.Resources.ReportChatIllegalDrugs, false),
                //new ChooseOptionItem(new ChatReportReasonPersonalDetails(), Strings.Resources.ReportChatPersonalDetails, false),
                new ChooseOptionItem(new ChatReportReasonPornography(), Strings.Resources.ReportChatPornography, false),
                new ChooseOptionItem(new ChatReportReasonCustom(), Strings.Resources.ReportChatOther, false)
            };

            var dialog = new ChooseOptionPopup(items);
            dialog.Title = Strings.Resources.ReportChat;
            dialog.PrimaryButtonText = Strings.Resources.OK;
            dialog.SecondaryButtonText = Strings.Resources.Cancel;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var reason = dialog.SelectedIndex as ChatReportReason;
            if (reason == null)
            {
                return;
            }

            var input = new InputPopup();
            input.Title = Strings.Resources.ReportChat;
            input.PlaceholderText = Strings.Resources.ReportChatDescription;
            input.IsPrimaryButtonEnabled = true;
            input.IsSecondaryButtonEnabled = true;
            input.PrimaryButtonText = Strings.Resources.OK;
            input.SecondaryButtonText = Strings.Resources.Cancel;

            var inputResult = await input.ShowQueuedAsync();

            string text;
            if (inputResult == ContentDialogResult.Primary)
            {
                text = input.Text;
            }
            else
            {
                return;
            }

            ClientService.Send(new ReportChat(chat.Id, messages, reason, text));
        }

        #endregion

        #region Set timer

        public RelayCommand<int?> SetTimerCommand { get; }
        private async void SetTimerExecute(int? ttl)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (ttl is int value)
            {
                ClientService.Send(new SetChatMessageTtl(chat.Id, value));
            }
            else
            {
                var dialog = new ChatTtlPopup(chat.Type is ChatTypeSecret ? ChatTtlType.Secret : ChatTtlType.Normal);
                dialog.Value = chat.MessageTtl;

                var confirm = await dialog.ShowQueuedAsync();
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }

                ClientService.Send(new SetChatMessageTtl(chat.Id, dialog.Value));
            }
        }

        #endregion

        #region Set theme

        public RelayCommand SetThemeCommand { get; }
        private async void SetThemeExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var dialog = new ChatThemePopup(ClientService, chat.ThemeName);

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                ClientService.Send(new SetChatTheme(chat.Id, dialog.ThemeName));
            }
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
                    var confirm = await MessagePopup.ShowAsync(Strings.Resources.UnpinMessageAlert, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
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

                if (ClientService.IsRepliesChat(chat))
                {
                    ToggleMuteExecute(ClientService.Notifications.GetMutedFor(chat) > 0);
                }
                else if (chat.IsBlocked)
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

                if (group.Status is ChatMemberStatusLeft or ChatMemberStatusBanned)
                {
                    // Delete and exit
                    ChatDeleteExecute();
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
                        ToggleMuteExecute(ClientService.Notifications.GetMutedFor(chat) > 0);
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
                        ChatDeleteExecute();
                    }
                }
            }
        }

        #endregion

        #region Join requests

        public async void ShowJoinRequests()
        {
            var popup = new ChatJoinRequestsPopup(ClientService, Settings, Aggregator, _chat, string.Empty);
            var confirm = await popup.ShowQueuedAsync();
        }

        #endregion

        #region Group calls

        public RelayCommand GroupCallJoinCommand { get; }
        private async void GroupCallJoinExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            await _groupCallService.JoinAsync(chat.Id);
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
        private readonly HashSet<long> _messages;

        private long _first = long.MaxValue;
        private long _last = long.MaxValue;

        private bool _suppressOperations = false;
        private bool _suppressPrev = false;
        private bool _suppressNext = false;

        public ISet<long> Ids => _messages;

        public Action<IEnumerable<MessageViewModel>> AttachChanged;

        public MessageCollection()
        {
            _messages = new();
        }

        public MessageCollection(ISet<long> exclude, IEnumerable<MessageViewModel> source)
        {
            foreach (var item in source)
            {
                if (exclude != null && exclude.Contains(item.Id))
                {
                    continue;
                }

                Insert(0, item);
            }
        }

        //~MessageCollection()
        //{
        //    Debug.WriteLine("Finalizing MessageCollection");
        //    GC.Collect();
        //}

        public bool ContainsKey(long id)
        {
            return _messages.Contains(id);
        }

        public void RawAddRange(IList<MessageViewModel> source, bool filter, out bool empty)
        {
            empty = true;

            for (int i = 0; i < source.Count; i++)
            {
                if (filter)
                {
                    if (source[i].Id != 0 && _messages.Contains(source[i].Id))
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
                    if (source[i].Id != 0 && _messages.Contains(source[i].Id))
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
            _messages?.Add(item.Id);

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

                AttachChanged?.Invoke(new[]
                {
                    prevHash != prevUpdate ? prev : null,
                });
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

                AttachChanged?.Invoke(new[]
                {
                    nextHash != nextUpdate ? next : null
                });
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

                AttachChanged?.Invoke(new[]
                {
                    prevHash != prevUpdate ? prev : null,
                    nextHash != nextUpdate ? next : null
                });
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

            AttachChanged?.Invoke(new[]
            {
                hash3 != update3 ? previous : null,
                hash2 != update2 ? next : null
            });

            base.RemoveItem(index);

            UpdateSeparatorOnRemove(next, previous, index);
        }

        private static MessageViewModel UpdateSeparatorOnInsert(MessageViewModel item, MessageViewModel next)
        {
            if (item != null && next != null)
            {
                var itemDate = Utils.UnixTimestampToDateTime(GetMessageDate(item));
                var previousDate = Utils.UnixTimestampToDateTime(GetMessageDate(next));
                if (previousDate.Date != itemDate.Date)
                {
                    return new MessageViewModel(next.ClientService, next.PlaybackService, next.Delegate, new Message(0, next.SenderId, next.ChatId, null, next.SchedulingState, next.IsOutgoing, false, false, false, false, true, false, false, false, false, false, false, false, false, next.IsChannelPost, next.IsTopicMessage, false, next.Date, 0, null, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageHeaderDate(), null));
                }
            }

            return null;
        }

        private void UpdateSeparatorOnRemove(MessageViewModel next, MessageViewModel previous, int index)
        {
            if (next != null && next.Content is MessageHeaderDate && previous != null)
            {
                var itemDate = Utils.UnixTimestampToDateTime(GetMessageDate(next));
                var previousDate = Utils.UnixTimestampToDateTime(GetMessageDate(previous));
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
            if (message1.IsService())
            {
                return message2.IsService();
            }
            else if (message2.IsService())
            {
                return false;
            }

            var saved1 = message1.IsSaved;
            var saved2 = message2.IsSaved;

            if (saved1 && saved2)
            {
                if (message1.ForwardInfo?.Origin is MessageForwardOriginUser fromUser1 && message2.ForwardInfo?.Origin is MessageForwardOriginUser fromUser2)
                {
                    return fromUser1.SenderUserId == fromUser2.SenderUserId && message1.ForwardInfo.FromChatId == message2.ForwardInfo.FromChatId;
                }
                else if (message1.ForwardInfo?.Origin is MessageForwardOriginChat fromChat1 && message2.ForwardInfo?.Origin is MessageForwardOriginChat fromChat2)
                {
                    return fromChat1.SenderChatId == fromChat2.SenderChatId && message1.ForwardInfo.FromChatId == message2.ForwardInfo.FromChatId;
                }
                else if (message1.ForwardInfo?.Origin is MessageForwardOriginChannel fromChannel1 && message2.ForwardInfo?.Origin is MessageForwardOriginChannel fromChannel2)
                {
                    return fromChannel1.ChatId == fromChannel2.ChatId && message1.ForwardInfo.FromChatId == message2.ForwardInfo.FromChatId;
                }
                else if (message1.ForwardInfo?.Origin is MessageForwardOriginMessageImport fromImport1 && message2.ForwardInfo?.Origin is MessageForwardOriginMessageImport fromImport2)
                {
                    return fromImport1.SenderName == fromImport2.SenderName;
                }
                else if (message1.ForwardInfo?.Origin is MessageForwardOriginHiddenUser hiddenUser1 && message2.ForwardInfo?.Origin is MessageForwardOriginHiddenUser hiddenUser2)
                {
                    return hiddenUser1.SenderName == hiddenUser2.SenderName;
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

    public class DialogUnreadMessagesViewModel<T> : BindableBase where T : SearchMessagesFilter
    {
        private readonly DialogViewModel _viewModel;
        private readonly SearchMessagesFilter _filter;

        private readonly bool _oldToNew;

        private List<long> _messages = new List<long>();
        private long _lastMessage;

        public DialogUnreadMessagesViewModel(DialogViewModel viewModel, bool oldToNew)
        {
            _viewModel = viewModel;
            _filter = Activator.CreateInstance<T>();

            _oldToNew = oldToNew;
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
                if (response is Messages messages)
                {
                    var stack = new List<long>();

                    if (_oldToNew)
                    {
                        foreach (var message in messages.MessagesValue.Reverse())
                        {
                            stack.Add(message.Id);
                        }
                    }
                    else
                    {
                        foreach (var message in messages.MessagesValue)
                        {
                            stack.Add(message.Id);
                        }
                    }

                    if (stack.Count > 0)
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
        public MessageViewModel ReplyToMessage { get; set; }
        public MessageViewModel EditingMessage { get; set; }

        public InputMessageFactory EditingMessageMedia { get; set; }
        public FormattedText EditingMessageCaption { get; set; }

        public WebPage WebPagePreview { get; set; }
        public string WebPageUrl { get; set; }

        public bool WebPageDisabled { get; set; }

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
