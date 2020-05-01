﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Common.Chats;
using Unigram.Controls;
using Unigram.Controls.Chats;
using Unigram.Controls.Views;
using Unigram.Navigation;
using Unigram.Services;
using Unigram.Services.Factories;
using Unigram.Services.Navigation;
using Unigram.ViewModels.Chats;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Dialogs;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public partial class DialogViewModel : TLViewModelBase, IDelegable<IDialogDelegate>
    {
        private List<MessageViewModel> _selectedItems = new List<MessageViewModel>();
        public List<MessageViewModel> SelectedItems
        {
            get
            {
                return _selectedItems;
            }
            set
            {
                Set(ref _selectedItems, value);
                MessagesForwardCommand.RaiseCanExecuteChanged();
                MessagesDeleteCommand.RaiseCanExecuteChanged();
                MessagesCopyCommand.RaiseCanExecuteChanged();
                MessagesReportCommand.RaiseCanExecuteChanged();
            }
        }

        public void ExpandSelection(IEnumerable<MessageViewModel> vector)
        {
            var messages = vector.ToList();

            for (int i = 0; i < messages.Count; i++)
            {
                if (messages[i].Content is MessageAlbum album)
                {
                    messages.RemoveAt(i);

                    for (int j = 0; j < album.Layout.Messages.Count; j++)
                    {
                        messages.Insert(i, album.Layout.Messages[j]);
                        i++;
                    }

                    i--;
                }
            }

            SelectedItems = messages;
        }

        public MediaLibraryCollection MediaLibrary
        {
            get
            {
                return MediaLibraryCollection.GetForCurrentView();
            }
        }

        private readonly ConcurrentDictionary<long, MessageViewModel> _groupedMessages = new ConcurrentDictionary<long, MessageViewModel>();

        private static readonly ConcurrentDictionary<long, IList<ChatAdministrator>> _admins = new ConcurrentDictionary<long, IList<ChatAdministrator>>();

        private readonly DisposableMutex _loadMoreLock = new DisposableMutex();
        private readonly DisposableMutex _insertLock = new DisposableMutex();

        private readonly DialogStickersViewModel _stickers;
        private readonly ILocationService _locationService;
        private readonly INotificationsService _pushService;
        private readonly IPlaybackService _playbackService;
        private readonly IVoIPService _voipService;
        private readonly INetworkService _networkService;
        private readonly IMessageFactory _messageFactory;

        public IPlaybackService PlaybackService => _playbackService;

        //private UserActivitySession _timelineSession;

        public IDialogDelegate Delegate { get; set; }

        public DialogViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, ILocationService locationService, INotificationsService pushService, IPlaybackService playbackService, IVoIPService voipService, INetworkService networkService, IMessageFactory messageFactory)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _locationService = locationService;
            _pushService = pushService;
            _playbackService = playbackService;
            _voipService = voipService;
            _networkService = networkService;
            _messageFactory = messageFactory;

            //_stickers = new DialogStickersViewModel(protoService, cacheService, settingsService, aggregator);
            _stickers = DialogStickersViewModel.GetForCurrentView(protoService, cacheService, settingsService, aggregator);

            _informativeTimer = new DispatcherTimer();
            _informativeTimer.Interval = TimeSpan.FromSeconds(5);
            _informativeTimer.Tick += (s, args) =>
            {
                _informativeTimer.Stop();
                InformativeMessage = null;
            };

            NextMentionCommand = new RelayCommand(NextMentionExecute);
            PreviousSliceCommand = new RelayCommand(PreviousSliceExecute);
            ClearReplyCommand = new RelayCommand(ClearReplyExecute);
            JoinChannelCommand = new RelayCommand(JoinChannelExecute);
            ToggleMuteCommand = new RelayCommand<bool>(ToggleMuteExecute);
            MuteCommand = new RelayCommand(() => ToggleMuteExecute(false));
            UnmuteCommand = new RelayCommand(() => ToggleMuteExecute(true));
            ToggleSilentCommand = new RelayCommand(ToggleSilentExecute);
            RemoveActionBarCommand = new RelayCommand(RemoveActionBarExecute);
            ReportSpamCommand = new RelayCommand<ChatReportReason>(ReportSpamExecute);
            ReportCommand = new RelayCommand(ReportExecute);
            OpenStickersCommand = new RelayCommand(OpenStickersExecute);
            ChatDeleteCommand = new RelayCommand(ChatDeleteExecute);
            ChatClearCommand = new RelayCommand(ChatClearExecute);
            CallCommand = new RelayCommand(CallExecute);
            UnpinMessageCommand = new RelayCommand(UnpinMessageExecute);
            UnblockCommand = new RelayCommand(UnblockExecute);
            ShareContactCommand = new RelayCommand(ShareContactExecute);
            AddContactCommand = new RelayCommand(AddContactExecute);
            StartCommand = new RelayCommand(StartExecute);
            SearchCommand = new RelayCommand(SearchExecute);
            JumpDateCommand = new RelayCommand(JumpDateExecute);
            GroupStickersCommand = new RelayCommand(GroupStickersExecute);
            ReadMentionsCommand = new RelayCommand(ReadMentionsExecute);
            SendCommand = new RelayCommand<string>(SendMessage);
            SwitchCommand = new RelayCommand<string>(SwitchExecute);
            SetTimerCommand = new RelayCommand(SetTimerExecute);
            ActionCommand = new RelayCommand(ActionExecute);
            DiscussCommand = new RelayCommand(DiscussExecute);
            OpenMessageCommand = new RelayCommand<Message>(OpenMessageExecute);
            ScheduledCommand = new RelayCommand(ScheduledExecute);

            MessagesForwardCommand = new RelayCommand(MessagesForwardExecute, MessagesForwardCanExecute);
            MessagesDeleteCommand = new RelayCommand(MessagesDeleteExecute, MessagesDeleteCanExecute);
            MessagesCopyCommand = new RelayCommand(MessagesCopyExecute, MessagesCopyCanExecute);
            MessagesReportCommand = new RelayCommand(MessagesReportExecute, MessagesReportCanExecute);

            MessageReplyPreviousCommand = new RelayCommand(MessageReplyPreviousExecute);
            MessageReplyNextCommand = new RelayCommand(MessageReplyNextExecute);
            MessageReplyCommand = new RelayCommand<MessageViewModel>(MessageReplyExecute);
            MessageRetryCommand = new RelayCommand<MessageViewModel>(MessageRetryExecute);
            MessageDeleteCommand = new RelayCommand<MessageViewModel>(MessageDeleteExecute);
            MessageForwardCommand = new RelayCommand<MessageViewModel>(MessageForwardExecute);
            MessageShareCommand = new RelayCommand<MessageViewModel>(MessageShareExecute);
            MessageSelectCommand = new RelayCommand<MessageViewModel>(MessageSelectExecute);
            MessageCopyCommand = new RelayCommand<MessageViewModel>(MessageCopyExecute);
            MessageCopyMediaCommand = new RelayCommand<MessageViewModel>(MessageCopyMediaExecute);
            MessageCopyLinkCommand = new RelayCommand<MessageViewModel>(MessageCopyLinkExecute);
            MessageEditLastCommand = new RelayCommand(MessageEditLastExecute);
            MessageEditCommand = new RelayCommand<MessageViewModel>(MessageEditExecute);
            MessagePinCommand = new RelayCommand<MessageViewModel>(MessagePinExecute);
            MessageReportCommand = new RelayCommand<MessageViewModel>(MessageReportExecute);
            MessageAddStickerCommand = new RelayCommand<MessageViewModel>(MessageAddStickerExecute);
            MessageFaveStickerCommand = new RelayCommand<MessageViewModel>(MessageFaveStickerExecute);
            MessageUnfaveStickerCommand = new RelayCommand<MessageViewModel>(MessageUnfaveStickerExecute);
            MessageSaveMediaCommand = new RelayCommand<MessageViewModel>(MessageSaveMediaExecute);
            MessageSaveDownloadCommand = new RelayCommand<MessageViewModel>(MessageSaveDownloadExecute);
            MessageSaveAnimationCommand = new RelayCommand<MessageViewModel>(MessageSaveAnimationExecute);
            MessageOpenWithCommand = new RelayCommand<MessageViewModel>(MessageOpenWithExecute);
            MessageOpenFolderCommand = new RelayCommand<MessageViewModel>(MessageOpenFolderExecute);
            MessageAddContactCommand = new RelayCommand<MessageViewModel>(MessageAddContactExecute);
            MessageServiceCommand = new RelayCommand<MessageViewModel>(MessageServiceExecute);
            MessageUnvotePollCommand = new RelayCommand<MessageViewModel>(MessageUnvotePollExecute);
            MessageStopPollCommand = new RelayCommand<MessageViewModel>(MessageStopPollExecute);
            MessageSendNowCommand = new RelayCommand<MessageViewModel>(MessageSendNowExecute);
            MessageRescheduleCommand = new RelayCommand<MessageViewModel>(MessageRescheduleExecute);

            SendDocumentCommand = new RelayCommand(SendDocumentExecute);
            SendMediaCommand = new RelayCommand(SendMediaExecute);
            SendContactCommand = new RelayCommand(SendContactExecute);
            SendLocationCommand = new RelayCommand(SendLocationExecute);
            SendPollCommand = new RelayCommand(SendPollExecute);

            StickerSendCommand = new RelayCommand<Sticker>(StickerSendExecute);
            StickerViewCommand = new RelayCommand<Sticker>(StickerViewExecute);
            StickerFaveCommand = new RelayCommand<Sticker>(StickerFaveExecute);
            StickerUnfaveCommand = new RelayCommand<Sticker>(StickerUnfaveExecute);

            AnimationSendCommand = new RelayCommand<Animation>(AnimationSendExecute);
            AnimationDeleteCommand = new RelayCommand<Animation>(AnimationDeleteExecute);

            EditDocumentCommand = new RelayCommand(EditDocumentExecute);
            EditMediaCommand = new RelayCommand(EditMediaExecute);
            EditCurrentCommand = new RelayCommand(EditCurrentExecute);

            //Items = new LegacyMessageCollection();
            //Items.CollectionChanged += (s, args) => IsEmpty = Items.Count == 0;

            Aggregator.Subscribe(this);
            Window.Current.Activated += Window_Activated;
        }

        //~DialogViewModel()
        //{
        //    Debug.WriteLine("Finalizing DialogViewModel");
        //    GC.Collect();
        //}

        public void Dispose()
        {
            _groupedMessages.Clear();
            _filesMap.Clear();
            _photosMap.Clear();

            Window.Current.Activated -= Window_Activated;
        }

        private void Window_Activated(object sender, Windows.UI.Core.WindowActivatedEventArgs e)
        {
            if (e.WindowActivationState == Windows.UI.Core.CoreWindowActivationState.Deactivated)
            {
                SaveDraft();
            }
        }

        public ItemClickEventHandler Sticker_Click;

        public override IDispatcherWrapper Dispatcher
        {
            get => base.Dispatcher;
            set
            {
                base.Dispatcher = value;
                Stickers.Dispatcher = value;
            }
        }

        public DialogStickersViewModel Stickers => _stickers;

        private Chat _migratedChat;
        public Chat MigratedChat
        {
            get
            {
                return _migratedChat;
            }
            set
            {
                Set(ref _migratedChat, value);
            }
        }

        private Chat _linkedChat;
        public Chat LinkedChat
        {
            get => _linkedChat;
            set => Set(ref _linkedChat, value);
        }

        private Chat _chat;
        public Chat Chat
        {
            get
            {
                return _chat;
            }
            set
            {
                Set(ref _chat, value);
            }
        }

        //private bool _isSchedule;
        //public bool IsSchedule
        //{
        //    get => _isSchedule;
        //    set => Set(ref _isSchedule, value);
        //}
        private bool _isSchedule => IsSchedule;
        public virtual bool IsSchedule => false;

        private string _lastSeen;
        public string LastSeen
        {
            get { return _lastSeen; }
            set { Set(ref _lastSeen, value); RaisePropertyChanged(() => Subtitle); }
        }

        private string _onlineCount;
        public string OnlineCount
        {
            get { return _onlineCount; }
            set { Set(ref _onlineCount, value); RaisePropertyChanged(() => Subtitle); }
        }

        public string Subtitle
        {
            get
            {
                var chat = _chat;
                if (chat == null)
                {
                    return null;
                }

                if (chat.Type is ChatTypePrivate || chat.Type is ChatTypeSecret)
                {
                    return _lastSeen;
                }

                if (!string.IsNullOrEmpty(_onlineCount) && !string.IsNullOrEmpty(_lastSeen))
                {
                    return string.Format("{0}, {1}", _lastSeen, _onlineCount);
                }

                return _lastSeen;
            }
        }

        private ChatSearchViewModel _search;
        public ChatSearchViewModel Search
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

        public void DisposeSearch()
        {
            var search = _search;
            if (search != null)
            {
                search.Dispose();
            }

            Search = null;
        }

        private string _accessToken;
        public string AccessToken
        {
            get
            {
                return _accessToken;
            }
            set
            {
                Set(ref _accessToken, value);
                RaisePropertyChanged(() => HasAccessToken);
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
            get
            {
                return _informativeMessage;
            }
            set
            {
                if (value != null)
                {
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
                return _chatActionManager = _chatActionManager ?? new OutputChatActionManager(ProtoService, _chat);
            }
        }

        public int UnreadCount
        {
            get
            {
                return _chat?.UnreadCount ?? 0;
            }
        }

        private ListViewSelectionMode _selectionMode = ListViewSelectionMode.None;
        public ListViewSelectionMode SelectionMode
        {
            get
            {
                return _selectionMode;
            }
            set
            {
                Set(ref _selectionMode, value);

                if (value != ListViewSelectionMode.None)
                {
                    DisposeSearch();
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
                var supergroup = ProtoService.GetSupergroup(super.SupergroupId);
                if (supergroup != null && !supergroup.CanPostMessages())
                {
                    return;
                }
            }

            if (string.IsNullOrEmpty(text))
            {
                field.Document.SetText(TextSetOptions.FormatRtf, @"{\rtf1\fbidis\ansi\ansicpg1252\deff0\nouicompat\deflang1040{\fonttbl{\f0\fnil Segoe UI;}}{\*\generator Riched20 10.0.14393}\viewkind4\uc1\pard\ltrpar\tx720\cf1\f0\fs23\lang1033}");
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
            if (chat == null)
            {
                return false;
            }

            if (chat.LastMessage == null)
            {
                return true;
            }

            var last = Items.LastOrDefault();
            if (last?.Content is MessageAlbum album)
            {
                last = album.Layout.Messages.LastOrDefault();
            }

            if (last == null || last.Id == 0)
            {
                return true;
            }

            return chat.LastMessage?.Id == last.Id;
        }

        public bool? IsFirstSliceLoaded { get; set; }

        private bool? _isLastSliceLoaded;
        public bool? IsLastSliceLoaded
        {
            get
            {
                return _isLastSliceLoaded;
            }
            set
            {
                Set(ref _isLastSliceLoaded, value);
            }
        }

        private bool _isEmpty = true;
        public bool IsEmpty
        {
            get
            {
                return _isEmpty && !_isLoadingNextSlice && !_isLoadingPreviousSlice;
            }
            set
            {
                Set(ref _isEmpty, value);
                RaisePropertyChanged(() => HasAccessToken);
            }
        }

        public override bool IsLoading
        {
            get
            {
                return _isLoadingNextSlice || _isLoadingPreviousSlice;
            }
            set
            {
                base.IsLoading = value;
                RaisePropertyChanged(() => IsEmpty);
                RaisePropertyChanged(() => HasAccessToken);
            }
        }

        private bool _isLoadingNextSlice;
        private bool _isLoadingPreviousSlice;

        private Stack<long> _goBackStack = new Stack<long>();

        public async Task LoadNextSliceAsync(bool force = false, bool init = false)
        {
            using (await _loadMoreLock.WaitAsync())
            {
                try
                {
                    // We don't want to flood with requests when the chat gets initialized
                    if (init && ListField?.ScrollingHost?.ScrollableHeight >= 200)
                    {
                        return;
                    }
                }
                catch { }

                var chat = _migratedChat ?? _chat;
                if (chat == null)
                {
                    return;
                }

                if (_isSchedule || _isLoadingNextSlice || _isLoadingPreviousSlice || Items.Count < 1 || IsLastSliceLoaded == true)
                {
                    return;
                }

                _isLoadingNextSlice = true;
                IsLoading = true;

                Debug.WriteLine("DialogViewModel: LoadNextSliceAsync");

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

                var limit = 50;

                //for (int i = 0; i < Items.Count; i++)
                //{
                //    if (Items[i].Id != 0 && Items[i].Id < maxId)
                //    {
                //        maxId = Items[i].Id;
                //    }
                //}

                Debug.WriteLine("DialogViewModel: LoadNextSliceAsync: Begin request");

                //return;

                var response = await ProtoService.SendAsync(new GetChatHistory(chat.Id, maxId.Value, 0, limit, false));
                if (response is Messages messages)
                {
                    if (messages.MessagesValue.Count > 0)
                    {
                        SetScrollMode(ItemsUpdatingScrollMode.KeepLastItemInView, force);
                        Logs.Logger.Debug(Logs.Target.Chat, "Setting scroll mode to KeepLastItemInView");
                    }

                    var replied = messages.MessagesValue.OrderByDescending(x => x.Id).Select(x => _messageFactory.Create(this, x)).ToList();
                    await ProcessMessagesAsync(chat, replied);

                    foreach (var message in replied)
                    {
                        if (message.Content is MessageChatUpgradeFrom chatUpgradeFrom)
                        {
                            var chatUpgradeTo = await PrepareMigratedAsync(chatUpgradeFrom.BasicGroupId);
                            if (chatUpgradeTo != null)
                            {
                                Items.Insert(0, chatUpgradeTo);
                            }
                        }
                        else
                        {
                            Items.Insert(0, message);
                        }

                        //var index = InsertMessageInOrder(Messages, message);
                    }

                    IsLastSliceLoaded = replied.IsEmpty();

                    if (replied.IsEmpty())
                    {
                        await AddHeaderAsync();
                    }
                }

                _isLoadingNextSlice = false;
                IsLoading = false;
            }
        }

        public async Task LoadPreviousSliceAsync(bool force = false, bool init = false)
        {
            using (await _loadMoreLock.WaitAsync())
            {
                try
                {
                    // We don't want to flood with requests when the chat gets initialized
                    if (init && ListField?.ScrollingHost?.ScrollableHeight >= 200)
                    {
                        return;
                    }
                }
                catch { }

                var chat = _chat;
                if (chat == null)
                {
                    return;
                }

                if (_isSchedule || _isLoadingNextSlice || _isLoadingPreviousSlice || chat == null || Items.Count < 1)
                {
                    return;
                }


                _isLoadingPreviousSlice = true;
                IsLoading = true;

                //if (last)
                //{
                //    UpdatingScrollMode = force ? UpdatingScrollMode.ForceKeepLastItemInView : UpdatingScrollMode.KeepLastItemInView;
                //}
                //else
                //{
                //    UpdatingScrollMode = force ? UpdatingScrollMode.ForceKeepItemsInView : UpdatingScrollMode.KeepItemsInView;
                //}

                Debug.WriteLine("DialogViewModel: LoadPreviousSliceAsync");

                var maxId = Items.LastOrDefault(x => x != null && x.Id != 0)?.Id;
                if (maxId == null)
                {
                    _isLoadingPreviousSlice = false;
                    IsLoading = false;

                    return;
                }

                var limit = 50;

                //for (int i = 0; i < Messages.Count; i++)
                //{
                //    if (Messages[i].Id != 0 && Messages[i].Id < maxId)
                //    {
                //        maxId = Messages[i].Id;
                //    }
                //}

                var response = await ProtoService.SendAsync(new GetChatHistory(chat.Id, maxId.Value, -49, limit, false));
                if (response is Messages messages)
                {
                    if (messages.MessagesValue.Any(x => !Items.ContainsKey(x.Id)))
                    {
                        SetScrollMode(ItemsUpdatingScrollMode.KeepItemsInView, true);
                        Logs.Logger.Debug(Logs.Target.Chat, "Setting scroll mode to KeepItemsInView");
                    }
                    else
                    {
                        SetScrollMode(ItemsUpdatingScrollMode.KeepLastItemInView, true);
                        Logs.Logger.Debug(Logs.Target.Chat, "Setting scroll mode to KeepLastItemInView");
                    }

                    var added = false;

                    var replied = messages.MessagesValue.OrderBy(x => x.Id).Select(x => _messageFactory.Create(this, x)).ToList();
                    await ProcessMessagesAsync(chat, replied);

                    foreach (var message in replied)
                    {
                        var index = InsertMessageInOrder(Items, message);
                        if (index > -1)
                        {
                            added = true;
                        }

                        //Messages.Add(message);
                    }

                    IsFirstSliceLoaded = !added || IsEndReached();
                }

                _isLoadingPreviousSlice = false;
                IsLoading = false;
            }
        }

        private async Task AddHeaderAsync()
        {
            var previous = Items.FirstOrDefault();
            if (previous != null && (previous.Content is MessageHeaderDate || previous.Id == 0))
            {
                return;
            }

            var chat = _chat;
            if (chat == null)
            {
                goto AddDate;
            }

            var user = CacheService.GetUser(chat);
            if (user == null || !(user.Type is UserTypeBot))
            {
                goto AddDate;
            }

            var fullInfo = CacheService.GetUserFull(user.Id);
            if (fullInfo == null)
            {
                fullInfo = await ProtoService.SendAsync(new GetUserFullInfo(user.Id)) as UserFullInfo;
            }

            if (fullInfo != null && fullInfo.BotInfo.Description.Length > 0)
            {
                var result = ProtoService.Execute(new GetTextEntities(fullInfo.BotInfo.Description)) as TextEntities;
                var entities = result?.Entities ?? new List<TextEntity>();

                foreach (var entity in entities)
                {
                    entity.Offset += Strings.Resources.BotInfoTitle.Length + Environment.NewLine.Length;
                }

                entities.Add(new TextEntity(0, Strings.Resources.BotInfoTitle.Length, new TextEntityTypeBold()));

                var message = $"{Strings.Resources.BotInfoTitle}{Environment.NewLine}{fullInfo.BotInfo.Description}";
                var text = new FormattedText(message, entities);

                Items.Insert(0, _messageFactory.Create(this, new Message(0, user.Id, chat.Id, null, null, false, false, false, true, false, false, false, 0, 0, null, 0, 0, 0, 0, string.Empty, 0, 0, string.Empty, new MessageText(text, null), null)));
                return;
            }

        AddDate:
            if (previous != null)
            {
                Items.Insert(0, _messageFactory.Create(this, new Message(0, previous.SenderUserId, previous.ChatId, null, null, previous.IsOutgoing, false, false, true, false, previous.IsChannelPost, false, previous.Date, 0, null, 0, 0, 0, 0, string.Empty, 0, 0, string.Empty, new MessageHeaderDate(), null)));
            }
        }

        private async Task<MessageViewModel> PrepareMigratedAsync(int basicGroupId)
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

            var supergroup = CacheService.GetSupergroup(chat);
            if (supergroup == null)
            {
                return null;
            }

            var response = await ProtoService.SendAsync(new CreateBasicGroupChat(basicGroupId, false));
            if (response is Chat migrated)
            {
                var messages = await ProtoService.SendAsync(new GetChatHistory(migrated.Id, 0, 0, 1, false)) as Messages;
                if (messages != null && messages.MessagesValue.Count > 0)
                {
                    MigratedChat = migrated;
                    IsLastSliceLoaded = false;

                    return _messageFactory.Create(this, messages.MessagesValue[0]);
                }
            }

            return null;
        }

        private List<long> _mentions = new List<long>();
        private long _lastMention;

        public void SetLastViewedMention(long messageId)
        {
            _lastMention = messageId;
        }

        public RelayCommand NextMentionCommand { get; }
        private async void NextMentionExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (_mentions != null && _mentions.Count > 0)
            {
                await LoadMessageSliceAsync(null, _mentions.RemoveLast());
            }
            else
            {
                long fromMessageId;
                if (_lastMention != 0)
                {
                    fromMessageId = _lastMention;
                }
                else
                {
                    var first = Items.FirstOrDefault();
                    if (first != null)
                    {
                        fromMessageId = first.Id;
                    }
                    else
                    {
                        return;
                    }
                }

                var response = await ProtoService.SendAsync(new SearchChatMessages(chat.Id, string.Empty, 0, fromMessageId, -9, 10, new SearchMessagesFilterUnreadMention()));
                if (response is Messages messages)
                {
                    var stack = new List<long>();
                    foreach (var message in messages.MessagesValue)
                    {
                        stack.Add(message.Id);
                    }

                    if (stack.Count > 0)
                    {
                        _mentions = stack;
                        NextMentionExecute();
                    }
                }
            }

            //var dialog = _dialog;
            //if (dialog == null)
            //{
            //    return;
            //}

            //var responsez = await LegacyService.GetUnreadMentionsAsync(_peer, 0, dialog.UnreadMentionsCount - 1, 1, 0, 0);
            //if (responsez.IsSucceeded && responsez.Result is ITLMessages result)
            //{
            //    var count = 0;
            //    if (responsez.Result is TLMessagesChannelMessages channelMessages)
            //    {
            //        count = channelMessages.Count;
            //    }

            //    dialog.UnreadMentionsCount = count;
            //    dialog.RaisePropertyChanged(() => dialog.UnreadMentionsCount);

            //    //if (response.Result.Messages.IsEmpty())
            //    //{
            //    //    dialog.UnreadMentionsCount = 0;
            //    //    dialog.RaisePropertyChanged(() => dialog.UnreadMentionsCount);
            //    //    return;
            //    //}

            //    var commonMessage = result.Messages.FirstOrDefault() as TLMessageCommonBase;
            //    if (commonMessage == null)
            //    {
            //        return;
            //    }

            //    commonMessage.IsMediaUnread = false;
            //    commonMessage.RaisePropertyChanged(() => commonMessage.IsMediaUnread);

            //    // DO NOT AWAIT
            //    LoadMessageSliceAsync(null, commonMessage.Id);

            //    if (With is TLChannel channel)
            //    {
            //        await LegacyService.ReadMessageContentsAsync(channel.ToInputChannel(), new TLVector<int> { commonMessage.Id });
            //    }
            //    else
            //    {
            //        await LegacyService.ReadMessageContentsAsync(new TLVector<int> { commonMessage.Id });
            //    }
            //}
        }

        public RelayCommand PreviousSliceCommand { get; }
        private async void PreviousSliceExecute()
        {
            if (_goBackStack.Count > 0)
            {
                await LoadMessageSliceAsync(null, _goBackStack.Pop());
            }
            else
            {
                var chat = _chat;
                if (chat == null)
                {
                    return;
                }

                await LoadMessageSliceAsync(null, chat.LastMessage?.Id ?? long.MaxValue, VerticalAlignment.Bottom, 8, disableAnimation: false);
            }

            TextField?.Focus(FocusState.Programmatic);
        }

        public async Task LoadMessageSliceAsync(long? previousId, long maxId, VerticalAlignment alignment = VerticalAlignment.Center, double? pixel = null, ScrollIntoViewAlignment? direction = null, bool? disableAnimation = null, bool second = false)
        {
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

            var already = Items.FirstOrDefault(x => x.Id == maxId || x.Content is MessageAlbum album && album.Layout.Messages.ContainsKey(maxId));
            if (already != null)
            {
                var field = ListField;
                if (field != null)
                {
                    await field.ScrollToItem(already, alignment, alignment == VerticalAlignment.Center, pixel, direction ?? ScrollIntoViewAlignment.Leading, disableAnimation);
                }

                return;
            }
            else if (second)
            {
                if (maxId == 0)
                {
                    ScrollToBottom(null);
                }

                //var max = _dialog?.UnreadCount > 0 ? _dialog.ReadInboxMaxId : int.MaxValue;
                //var offset = _dialog?.UnreadCount > 0 && max > 0 ? -16 : 0;
                //await LoadFirstSliceAsync(max, offset);
                return;
            }

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

                Debug.WriteLine("DialogViewModel: LoadMessageSliceAsync");

                if (previousId.HasValue)
                {
                    _goBackStack.Push(previousId.Value);
                }

                var offset = -25;
                var limit = 50;

                var response = await ProtoService.SendAsync(new GetChatHistory(chat.Id, maxId, offset, limit, false));
                if (response is Messages messages)
                {
                    _groupedMessages.Clear();

                    if (messages.MessagesValue.Count > 0)
                    {
                        SetScrollMode(ItemsUpdatingScrollMode.KeepLastItemInView, true);
                        Logs.Logger.Debug(Logs.Target.Chat, "Setting scroll mode to KeepLastItemInView");
                    }

                    var replied = messages.MessagesValue.OrderBy(x => x.Id).Select(x => _messageFactory.Create(this, x)).ToList();
                    await ProcessMessagesAsync(chat, replied);

                    // If we're loading from the last read message
                    // then we want to skip it to align first unread message at top
                    if (chat.LastReadInboxMessageId != chat.LastMessage?.Id)
                    {
                        var target = default(MessageViewModel);
                        var index = -1;

                        for (int i = 0; i < replied.Count; i++)
                        {
                            var current = replied[i];
                            if (current.Id > chat.LastReadInboxMessageId)
                            {
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
                        }

                        if (target != null)
                        {
                            if (index != -1)
                            {
                                replied.Insert(index, _messageFactory.Create(this, new Message(0, target.SenderUserId, target.ChatId, null, null, target.IsOutgoing, false, false, true, false, target.IsChannelPost, false, target.Date, 0, null, 0, 0, 0, 0, string.Empty, 0, 0, string.Empty, new MessageHeaderUnread(), null)));
                            }
                            else if (maxId == chat.LastReadInboxMessageId)
                            {
                                Logs.Logger.Debug(Logs.Target.Chat, "Looking for first unread message, can't find it");
                            }

                            if (maxId == chat.LastReadInboxMessageId)
                            {
                                maxId = target.Id;
                                pixel = 28 + 48 + 4;
                            }
                        }
                    }

                    // If we're loading the last message and it has been read already
                    // then we want to align it at bottom, as it might be taller than the window height
                    if (maxId == chat.LastReadInboxMessageId && maxId == chat.LastMessage?.Id && alignment != VerticalAlignment.Center)
                    {
                        alignment = VerticalAlignment.Bottom;
                        pixel = 12;
                    }

                    if (replied.Count > 0 && replied[0].Content is MessageChatUpgradeFrom chatUpgradeFrom)
                    {
                        var chatUpgradeTo = await PrepareMigratedAsync(chatUpgradeFrom.BasicGroupId);
                        if (chatUpgradeTo != null)
                        {
                            replied[0] = chatUpgradeTo;
                        }
                    }

                    Items.ReplaceWith(replied);

                    IsLastSliceLoaded = null;
                    IsFirstSliceLoaded = IsEndReached();

                    if (replied.IsEmpty())
                    {
                        await AddHeaderAsync();
                    }
                }

                _isLoadingNextSlice = false;
                _isLoadingPreviousSlice = false;
                IsLoading = false;
            }

            await LoadMessageSliceAsync(null, maxId, alignment, pixel, direction, disableAnimation, true);
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

                Debug.WriteLine("DialogViewModel: LoadScheduledSliceAsync");

                var response = await ProtoService.SendAsync(new GetChatScheduledMessages(chat.Id));
                if (response is Messages messages)
                {
                    _groupedMessages.Clear();

                    if (messages.MessagesValue.Count > 0)
                    {
                        SetScrollMode(ItemsUpdatingScrollMode.KeepLastItemInView, true);
                        Logs.Logger.Debug(Logs.Target.Chat, "Setting scroll mode to KeepLastItemInView");
                    }

                    var replied = messages.MessagesValue.OrderBy(x => x.Id).Select(x => _messageFactory.Create(this, x)).ToList();
                    await ProcessMessagesAsync(chat, replied);

                    var target = replied.FirstOrDefault();
                    if (target != null)
                    {
                        replied.Insert(0, _messageFactory.Create(this, new Message(0, target.SenderUserId, target.ChatId, null, target.SchedulingState, target.IsOutgoing, false, false, true, false, target.IsChannelPost, false, target.Date, 0, null, 0, 0, 0, 0, string.Empty, 0, 0, string.Empty, new MessageHeaderDate(), null)));
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

        public async Task LoadDateSliceAsync(int dateOffset)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var response = await ProtoService.SendAsync(new GetChatMessageByDate(chat.Id, dateOffset));
            if (response is Message message)
            {
                await LoadMessageSliceAsync(null, message.Id);
            }
        }

        public void ScrollToBottom(object item)
        {
            //if (IsFirstSliceLoaded)
            {
                var field = ListField;
                if (field == null)
                {
                    return;
                }

                if (item == null)
                {
                    field.ScrollToBottom();
                }
                else
                {
                    field.ScrollIntoView(item, ScrollIntoViewAlignment.Leading);
                }

                field.SetScrollMode(ItemsUpdatingScrollMode.KeepLastItemInView, true);
            }
        }

        public MessageCollection Items { get; } = new MessageCollection();

        private async Task ProcessMessagesAsync(Chat chat, IList<MessageViewModel> messages)
        {
            if (Settings.IsLargeEmojiEnabled)
            {
                await ProcessEmojiAsync(chat, messages);
            }

            ProcessAlbums(chat, messages);
            ProcessFiles(chat, messages);
            ProcessReplies(chat, messages);
        }

        private async Task ProcessEmojiAsync(Chat chat, IList<MessageViewModel> messages)
        {
            StickerSet set = null;
            foreach (var message in messages)
            {
                if (message.Content is MessageText text)
                {
                    if (Emoji.TryCountEmojis(text.Text.Text, out int count, 1))
                    {
                        if (set == null)
                        {
                            set = await ProtoService.GetAnimatedSetAsync(AnimatedSetType.Emoji);
                        }

                        if (set == null)
                        {
                            break;
                        }

                        var emoji = text.Text.Text.TrimEnd('\uFE0F');
                        if (emoji.StartsWith("\uD83D\uDC4D"))
                        {
                            emoji = "\uD83D\uDC4D";
                        }

                        foreach (var sticker in set.Stickers)
                        {
                            if (string.Equals(sticker.Emoji, emoji, StringComparison.OrdinalIgnoreCase))
                            {
                                message.GeneratedContent = new MessageSticker(sticker);
                                continue;
                            }
                        }
                    }
                }
            }
        }

        private void ProcessFiles(Chat chat, IList<MessageViewModel> messages, MessageViewModel parent = null)
        {
            foreach (var message in messages)
            {
                if (message.Content is MessageDice dice)
                {
                    message.GeneratedContent = new MessageSticker(dice.FinalStateSticker ?? dice.InitialStateSticker);
                }

                var target = parent ?? message;
                var content = message.GeneratedContent ?? message.Content as object;
                if (content is MessageAlbum albumMessage)
                {
                    ProcessFiles(chat, albumMessage.Layout.Messages, message);
                    continue;
                }
                if (content is MessageAnimation animationMessage)
                {
                    content = animationMessage.Animation;
                }
                else if (content is MessageAudio audioMessage)
                {
                    content = audioMessage.Audio;
                }
                else if (content is MessageDocument documentMessage)
                {
                    content = documentMessage.Document;
                }
                else if (content is MessageGame gameMessage)
                {
                    if (gameMessage.Game.Animation != null)
                    {
                        content = gameMessage.Game.Animation;
                    }
                    else if (gameMessage.Game.Photo != null)
                    {
                        content = gameMessage.Game.Photo;
                    }
                }
                else if (content is MessageInvoice invoiceMessage)
                {
                    content = invoiceMessage.Photo;
                }
                else if (content is MessageLocation locationMessage)
                {
                    content = locationMessage.Location;
                }
                else if (content is MessagePhoto photoMessage)
                {
                    content = photoMessage.Photo;
                }
                else if (content is MessageSticker stickerMessage)
                {
                    content = stickerMessage.Sticker;
                }
                else if (content is MessageText textMessage)
                {
                    if (textMessage.WebPage?.Animation != null)
                    {
                        content = textMessage.WebPage.Animation;
                    }
                    else if (textMessage.WebPage?.Audio != null)
                    {
                        content = textMessage.WebPage.Audio;
                    }
                    else if (textMessage.WebPage?.Document != null)
                    {
                        content = textMessage.WebPage.Document;
                    }
                    else if (textMessage.WebPage?.Sticker != null)
                    {
                        content = textMessage.WebPage.Sticker;
                    }
                    else if (textMessage.WebPage?.Video != null)
                    {
                        content = textMessage.WebPage.Video;
                    }
                    else if (textMessage.WebPage?.VideoNote != null)
                    {
                        content = textMessage.WebPage.VideoNote;
                    }
                    else if (textMessage.WebPage?.VoiceNote != null)
                    {
                        content = textMessage.WebPage.VoiceNote;
                    }
                    // PHOTO SHOULD ALWAYS BE AT THE END!
                    else if (textMessage?.WebPage?.Photo != null)
                    {
                        content = textMessage?.WebPage?.Photo;
                    }
                }
                else if (content is MessageVideo videoMessage)
                {
                    content = videoMessage.Video;
                }
                else if (content is MessageVideoNote videoNoteMessage)
                {
                    content = videoNoteMessage.VideoNote;
                }
                else if (content is MessageVoiceNote voiceNoteMessage)
                {
                    content = voiceNoteMessage.VoiceNote;
                }

                if (content is Animation animation)
                {
                    if (animation.Thumbnail != null)
                    {
                        _filesMap[animation.Thumbnail.Photo.Id].Add(target);
                    }

                    _filesMap[animation.AnimationValue.Id].Add(target);
                }
                else if (content is Audio audio)
                {
                    if (audio.AlbumCoverThumbnail != null)
                    {
                        _filesMap[audio.AlbumCoverThumbnail.Photo.Id].Add(target);
                    }

                    _filesMap[audio.AudioValue.Id].Add(target);
                }
                else if (content is Document document)
                {
                    if (document.Thumbnail != null)
                    {
                        _filesMap[document.Thumbnail.Photo.Id].Add(target);
                    }

                    _filesMap[document.DocumentValue.Id].Add(target);
                }
                else if (content is Photo photo)
                {
                    foreach (var size in photo.Sizes)
                    {
                        _filesMap[size.Photo.Id].Add(target);
                    }
                }
                else if (content is Sticker sticker)
                {
                    if (sticker.Thumbnail != null)
                    {
                        _filesMap[sticker.Thumbnail.Photo.Id].Add(target);
                    }

                    _filesMap[sticker.StickerValue.Id].Add(target);
                }
                else if (content is Video video)
                {
                    if (video.Thumbnail != null)
                    {
                        _filesMap[video.Thumbnail.Photo.Id].Add(target);
                    }

                    _filesMap[video.VideoValue.Id].Add(target);
                }
                else if (content is VideoNote videoNote)
                {
                    if (videoNote.Thumbnail != null)
                    {
                        _filesMap[videoNote.Thumbnail.Photo.Id].Add(target);
                    }

                    _filesMap[videoNote.Video.Id].Add(target);
                }
                else if (content is VoiceNote voiceNote)
                {
                    _filesMap[voiceNote.Voice.Id].Add(target);
                }

                if (target.IsSaved() || ((chat.Type is ChatTypeBasicGroup || chat.Type is ChatTypeSupergroup) && !target.IsOutgoing && !target.IsChannelPost))
                {
                    if (target.IsSaved())
                    {
                        if (target.ForwardInfo?.Origin is MessageForwardOriginUser fromUser)
                        {
                            var user = target.ProtoService.GetUser(fromUser.SenderUserId);
                            if (user != null && user.ProfilePhoto != null)
                            {
                                _photosMap[user.ProfilePhoto.Small.Id].Add(target);
                            }
                        }
                        else if (target.ForwardInfo?.Origin is MessageForwardOriginChannel post)
                        {
                            var fromChat = message.ProtoService.GetChat(post.ChatId);
                            if (fromChat != null && fromChat.Photo != null)
                            {
                                _photosMap[fromChat.Photo.Small.Id].Add(target);
                            }
                        }
                    }
                    else
                    {
                        var user = target.GetSenderUser();
                        if (user != null && user.ProfilePhoto != null)
                        {
                            _photosMap[user.ProfilePhoto.Small.Id].Add(target);
                        }
                    }
                }
            }
        }

        private void ProcessAlbums(Chat chat, IList<MessageViewModel> slice)
        {
            var groups = new Dictionary<long, Tuple<MessageViewModel, GroupedMessages>>();
            var newGroups = new Dictionary<long, long>();

            for (int i = 0; i < slice.Count; i++)
            {
                var message = slice[i];
                if (message.MediaAlbumId != 0)
                {
                    var groupedId = message.MediaAlbumId;

                    _groupedMessages.TryGetValue(groupedId, out MessageViewModel group);

                    if (group == null)
                    {
                        var media = new MessageAlbum();

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
                        groups[groupedId] = Tuple.Create(group, album.Layout);

                        album.Layout.GroupedId = groupedId;
                        album.Layout.Messages.Add(message);

                        var first = album.Layout.Messages.FirstOrDefault();
                        if (first != null)
                        {
                            group.UpdateWith(first);
                        }
                    }
                }
            }

            foreach (var group in groups.Values)
            {
                group.Item2.Calculate();

                if (newGroups.ContainsKey(group.Item1.MediaAlbumId))
                {
                    continue;
                }

                Handle(new UpdateMessageContent(chat.Id, group.Item1.Id, group.Item1.Content));
                Handle(new UpdateMessageEdited(chat.Id, group.Item1.Id, group.Item1.EditDate, group.Item1.ReplyMarkup));
            }
        }

        private async void ProcessReplies(Chat chat, IList<MessageViewModel> slice)
        {
            var replies = new List<long>();

            foreach (var message in slice)
            {
                message.ReplyToMessageState = ReplyToMessageState.Loading;

                var replyId = 0L;

                if (message.Content is MessagePinMessage pinMessage)
                {
                    replyId = pinMessage.MessageId;
                }
                else if (message.Content is MessageGameScore gameScore)
                {
                    replyId = gameScore.GameMessageId;
                }
                else if (message.Content is MessagePaymentSuccessful paymentSuccessful)
                {
                    replyId = paymentSuccessful.InvoiceMessageId;
                }
                else if (message.ReplyToMessageId != 0)
                {
                    replyId = message.ReplyToMessageId;
                }

                if (replyId != 0)
                {
                    var enqueue = true;

                    foreach (var reply in slice)
                    {
                        if (reply.Id == replyId)
                        {
                            message.ReplyToMessage = _messageFactory.Create(this, reply.Get());

                            enqueue = false;
                            break;
                        }
                    }

                    if (enqueue && !replies.Contains(replyId))
                    {
                        replies.Add(replyId);
                    }
                }
            }



            var response = await ProtoService.SendAsync(new GetMessages(chat.Id, replies));
            if (response is Telegram.Td.Api.Messages messages)
            {
                foreach (var message in slice)
                {
                    foreach (var result in messages.MessagesValue)
                    {
                        if (result == null)
                        {
                            continue;
                        }

                        if (message.Content is MessagePinMessage pinMessage && pinMessage.MessageId == result.Id)
                        {
                            message.ReplyToMessage = _messageFactory.Create(this, result);
                            break;
                        }
                        else if (message.Content is MessageGameScore gameScore && gameScore.GameMessageId == result.Id)
                        {
                            message.ReplyToMessage = _messageFactory.Create(this, result);
                            break;
                        }
                        else if (message.Content is MessagePaymentSuccessful paymentSuccessful && paymentSuccessful.InvoiceMessageId == result.Id)
                        {
                            message.ReplyToMessage = _messageFactory.Create(this, result);
                            break;
                        }
                        else if (message.ReplyToMessageId == result.Id)
                        {
                            message.ReplyToMessage = _messageFactory.Create(this, result);
                            break;
                        }
                    }

                    if (message.ReplyToMessage == null)
                    {
                        message.ReplyToMessageState = ReplyToMessageState.Deleted;
                    }
                    else
                    {
                        message.ReplyToMessageState = ReplyToMessageState.None;
                    }

                    Handle(message, bubble => bubble.UpdateMessageReply(message), service => service.UpdateMessage(message));
                }
            }
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var chat = ProtoService.GetChat((long)parameter);
            if (chat == null)
            {
                chat = await ProtoService.SendAsync(new GetChat((long)parameter)) as Chat;
            }

            if (chat == null)
            {
                return;
            }

#if !DEBUG
            if (chat.Type is ChatTypeSecret)
            {
                ApplicationView.GetForCurrentView().IsScreenCaptureEnabled = false;
            }
#endif

            Chat = chat;
            SetScrollMode(ItemsUpdatingScrollMode.KeepLastItemInView, true);

            if (state.TryGet("access_token", out string accessToken))
            {
                state.Remove("access_token");
                AccessToken = accessToken;
            }

#pragma warning disable CS4014
            if (_isSchedule)
            {
                Logs.Logger.Debug(Logs.Target.Chat, string.Format("{0} - Loadings scheduled messages", chat.Id));

                LoadScheduledSliceAsync();
            }
            else if (state.TryGet("message_id", out long navigation))
            {
                Logs.Logger.Debug(Logs.Target.Chat, string.Format("{0} - Loading messages from specific id", chat.Id));

                state.Remove("message_id");
                LoadMessageSliceAsync(null, navigation);
            }
            else
            {
                if (Settings.Chats.TryRemove(chat.Id, ChatSetting.Index, out long start) && start < chat.LastReadInboxMessageId)
                {
                    if (Settings.Chats.TryRemove(chat.Id, ChatSetting.Pixel, out double pixel))
                    {
                        Logs.Logger.Debug(Logs.Target.Chat, string.Format("{0} - Loading messages from specific pixel", chat.Id));

                        LoadMessageSliceAsync(null, start, VerticalAlignment.Bottom, pixel);
                    }
                    else
                    {
                        Logs.Logger.Debug(Logs.Target.Chat, string.Format("{0} - Loading messages from specific id, pixel missing", chat.Id));

                        LoadMessageSliceAsync(null, start, VerticalAlignment.Bottom);
                    }
                }
                else if (chat.UnreadCount > 0)
                {
                    Logs.Logger.Debug(Logs.Target.Chat, string.Format("{0} - Loading messages from LastReadInboxMessageId: {1}", chat.Id, chat.LastReadInboxMessageId));

                    LoadMessageSliceAsync(null, chat.LastReadInboxMessageId, VerticalAlignment.Top);
                }
                else
                {
                    Logs.Logger.Debug(Logs.Target.Chat, string.Format("{0} - Loading messages from LastMessageId: {1}", chat.Id, chat.LastMessage?.Id));

                    LoadMessageSliceAsync(null, chat.LastMessage?.Id ?? long.MaxValue, VerticalAlignment.Bottom, 8);
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
                Items.Add(_messageFactory.Create(this, new Message(0, 0, chat.Id, null, true,  false, false, false, false, false, false, TodayDate(14, 58), 0, null, 0, 0,  0, 0, string.Empty, 0, 0, new MessageText(new FormattedText("Hey Eileen", new TextEntity[0]), null), null)));
                Items.Add(_messageFactory.Create(this, new Message(1, 0, chat.Id, null, true,  false, false, false, false, false, false, TodayDate(14, 59), 0, null, 0, 0,  0, 0, string.Empty, 0, 0, new MessageText(new FormattedText("So, why is Telegram cool?", new TextEntity[0]), null), null)));
                Items.Add(_messageFactory.Create(this, new Message(2, 7, chat.Id, null, false, false, false, false, false, false, false, TodayDate(14, 59), 0, null, 0, 0,  0, 0, string.Empty, 0, 0, new MessageText(new FormattedText("Well, look. Telegram is superfast and you can use it on all your devices at the same time - phones, tablets, even desktops.", new TextEntity[0]), null), null)));
                Items.Add(_messageFactory.Create(this, new Message(3, 0, chat.Id, null, true,  false, false, false, false, false, false, TodayDate(14, 59), 0, null, 0, 0,  0, 0, string.Empty, 0, 0, new MessageText(new FormattedText("😴", new TextEntity[0]), null), null)));
                Items.Add(_messageFactory.Create(this, new Message(4, 7, chat.Id, null, false, false, false, false, false, false, false, TodayDate(15, 00), 0, null, 0, 0,  0, 0, string.Empty, 0, 0, new MessageText(new FormattedText("And it has secret chats, like this one, with end-to-end encryption!", new TextEntity[0]), null), null)));
                Items.Add(_messageFactory.Create(this, new Message(5, 0, chat.Id, null, true,  false, false, false, false, false, false, TodayDate(15, 00), 0, null, 0, 0,  0, 0, string.Empty, 0, 0, new MessageText(new FormattedText("End encryption to what end??", new TextEntity[0]), null), null)));
                Items.Add(_messageFactory.Create(this, new Message(6, 7, chat.Id, null, false, false, false, false, false, false, false, TodayDate(15, 01), 0, null, 0, 0,  0, 0, string.Empty, 0, 0, new MessageText(new FormattedText("Arrgh. Forget it. You can set a timer and send photos that will disappear when the time rush out. Yay!", new TextEntity[0]), null), null)));
                Items.Add(_messageFactory.Create(this, new Message(7, 7, chat.Id, null, false, false, false, false, false, false, false, TodayDate(15, 01), 0, null, 0, 0,  0, 0, string.Empty, 0, 0, new MessageChatSetTtl(15), null)));
                Items.Add(_messageFactory.Create(this, new Message(8, 0, chat.Id, null, false, false, false, false, false, false, false, TodayDate(15, 05), 0, null, 0, 15, 0, 0, string.Empty, 0, 0, new MessagePhoto(new Photo(false, new[] { new PhotoSize("t", new File(0, 0, 0, new LocalFile(System.IO.Path.Combine(Package.Current.InstalledLocation.Path, "Assets\\Mockup\\hot.png"), true, true, false, true, 0, 0, 0), new RemoteFile()), 580, 596) }), new FormattedText(string.Empty, new TextEntity[0]), true), null)));

                SetText("😱🙈👍");
            }
            else
            {
                //Items.Add(GetMessage(new Message(0, 11, chat.Id, null, false, false, false, false, false, false, false, TodayDate(15, 25), 0, null, 0, 0, 0, 0, string.Empty, 0, 0, new MessageText(new FormattedText("Yeah, that was my iPhone X", new TextEntity[0]), null), null)));
                Items.Add(_messageFactory.Create(this, new Message(4, 11, chat.Id, null, false, false, false, false, false, false, false, TodayDate(15, 25), 0, null, 0, 0, 0, 0, string.Empty, 0, 0, new MessageSticker(new Sticker(0, 512, 512, "", false, null, null, new File(0, 0, 0, new LocalFile(System.IO.Path.Combine(Package.Current.InstalledLocation.Path, "Assets\\Mockup\\sticker0.webp"), true, true, false, true, 0, 0, 0), null))), null)));
                Items.Add(_messageFactory.Create(this, new Message(1, 9,  chat.Id, null, false, false, false, false, false, false, false, TodayDate(15, 26), 0, null, 0, 0, 0, 0, string.Empty, 0, 0, new MessageText(new FormattedText("Are you sure it's safe here?", new TextEntity[0]), null), null)));
                Items.Add(_messageFactory.Create(this, new Message(2, 7,  chat.Id, null, true,  false, false, false, false, false, false, TodayDate(15, 27), 0, null, 0, 0, 0, 0, string.Empty, 0, 0, new MessageText(new FormattedText("Yes, sure, don't worry.", new TextEntity[0]), null), null)));
                Items.Add(_messageFactory.Create(this, new Message(3, 13, chat.Id, null, false, false, false, false, false, false, false, TodayDate(15, 27), 0, null, 0, 0, 0, 0, string.Empty, 0, 0, new MessageText(new FormattedText("Hallo alle zusammen! Is the NSA reading this? 😀", new TextEntity[0]), null), null)));
                Items.Add(_messageFactory.Create(this, new Message(4, 9,  chat.Id, null, false, false, false, false, false, false, false, TodayDate(15, 29), 0, null, 0, 0, 0, 0, string.Empty, 0, 0, new MessageSticker(new Sticker(0, 512, 512, "", false, null, null, new File(0, 0, 0, new LocalFile(System.IO.Path.Combine(Package.Current.InstalledLocation.Path, "Assets\\Mockup\\sticker1.webp"), true, true, false, true, 0, 0, 0), null))), null)));
                Items.Add(_messageFactory.Create(this, new Message(5, 10, chat.Id, null, false, false, false, false, false, false, false, TodayDate(15, 29), 0, null, 0, 0, 0, 0, string.Empty, 0, 0, new MessageText(new FormattedText("Sorry, I'll have to publish this conversation on the web.", new TextEntity[0]), null), null)));
                Items.Add(_messageFactory.Create(this, new Message(6, 10, chat.Id, null, false, false, false, false, false, false, false, TodayDate(15, 01), 0, null, 0, 0, 0, 0, string.Empty, 0, 0, new MessageChatDeleteMember(10), null)));
                Items.Add(_messageFactory.Create(this, new Message(7, 8,  chat.Id, null, false, false, false, false, false, false, false, TodayDate(15, 30), 0, null, 0, 0, 0, 0, string.Empty, 0, 0, new MessageText(new FormattedText("Wait, we could have made so much money on this!", new TextEntity[0]), null), null)));
            }
#endif

            if (chat.IsMarkedAsUnread)
            {
                ProtoService.Send(new ToggleChatIsMarkedAsUnread(chat.Id, false));
            }

            ProtoService.Send(new OpenChat(chat.Id));

            Delegate?.UpdateChat(chat);
            Delegate?.UpdateChatActions(chat, CacheService.GetChatActions(chat.Id));

            if (chat.Type is ChatTypePrivate privata)
            {
                var item = ProtoService.GetUser(privata.UserId);
                var cache = ProtoService.GetUserFull(privata.UserId);

                Delegate?.UpdateUser(chat, item, false);

                if (cache == null)
                {
                    ProtoService.Send(new GetUserFullInfo(privata.UserId));
                }
                else
                {
                    Delegate?.UpdateUserFullInfo(chat, item, cache, false, _accessToken != null);
                }
            }
            else if (chat.Type is ChatTypeSecret secretType)
            {
                var secret = ProtoService.GetSecretChat(secretType.SecretChatId);
                var item = ProtoService.GetUser(secretType.UserId);
                var cache = ProtoService.GetUserFull(secretType.UserId);

                Delegate?.UpdateSecretChat(chat, secret);
                Delegate?.UpdateUser(chat, item, true);

                if (cache == null)
                {
                    ProtoService.Send(new GetUserFullInfo(secret.UserId));
                }
                else
                {
                    Delegate?.UpdateUserFullInfo(chat, item, cache, true, false);
                }
            }
            else if (chat.Type is ChatTypeBasicGroup basic)
            {
                var item = ProtoService.GetBasicGroup(basic.BasicGroupId);
                var cache = ProtoService.GetBasicGroupFull(basic.BasicGroupId);

                Delegate?.UpdateBasicGroup(chat, item);

                if (cache == null)
                {
                    ProtoService.Send(new GetBasicGroupFullInfo(basic.BasicGroupId));
                }
                else
                {
                    Delegate?.UpdateBasicGroupFullInfo(chat, item, cache);
                }

                ProtoService.Send(new GetChatAdministrators(chat.Id), result =>
                {
                    if (result is Telegram.Td.Api.ChatAdministrators users)
                    {
                        _admins[chat.Id] = users.Administrators;
                    }
                });
            }
            else if (chat.Type is ChatTypeSupergroup super)
            {
                var item = ProtoService.GetSupergroup(super.SupergroupId);
                var cache = ProtoService.GetSupergroupFull(super.SupergroupId);

                Delegate?.UpdateSupergroup(chat, item);

                if (cache == null)
                {
                    ProtoService.Send(new GetSupergroupFullInfo(super.SupergroupId));
                }
                else
                {
                    Delegate?.UpdateSupergroupFullInfo(chat, item, cache);
                    //Stickers?.UpdateSupergroupFullInfo(chat, item, cache);
                }

                ProtoService.Send(new GetChatAdministrators(chat.Id), result =>
                {
                    if (result is Telegram.Td.Api.ChatAdministrators users)
                    {
                        _admins[chat.Id] = users.Administrators;
                    }
                });
            }

            ShowReplyMarkup(chat);
            ShowDraftMessage(chat);
            ShowPinnedMessage(chat);
            ShowSwitchInline(state);

            if (App.DataPackages.TryRemove(chat.Id, out DataPackageView package))
            {
                await HandlePackageAsync(package);
            }
        }

        //public override async Task OnNavigatedFromAsync(IDictionary<string, object> pageState, bool suspending)
        //{
        //    if (suspending)
        //    {
        //        await OnNavigatingFromAsync(new NavigatingEventArgs(null));
        //        Items.Clear();
        //    }
        //}

        public override Task OnNavigatingFromAsync(NavigatingEventArgs args)
        {
            Aggregator.Unsubscribe(this);
            Window.Current.Activated -= Window_Activated;

            var chat = _chat;
            if (chat == null)
            {
                return Task.CompletedTask;
            }

#if !DEBUG
            if (chat.Type is ChatTypeSecret)
            {
                ApplicationView.GetForCurrentView().IsScreenCaptureEnabled = true;
            }
#endif

            ProtoService.Send(new CloseChat(chat.Id));

            try
            {
                var field = ListField;
                if (field == null)
                {
                    Settings.Chats.TryRemove(chat.Id, ChatSetting.Index, out long index);
                    Settings.Chats.TryRemove(chat.Id, ChatSetting.Pixel, out double pixel);

                    Logs.Logger.Debug(Logs.Target.Chat, string.Format("{0} - Removing scrolling position, generic reason", chat.Id));

                    return Task.CompletedTask;
                }

                var panel = field.ItemsPanelRoot as ItemsStackPanel;
                if (panel != null && panel.LastVisibleIndex >= 0 && panel.LastVisibleIndex < Items.Count - 1 && Items.Count > 0)
                {
                    var start = Items[panel.LastVisibleIndex].Id;
                    if (start != chat.LastMessage?.Id)
                    {
                        var container = field.ContainerFromIndex(panel.LastVisibleIndex) as ListViewItem;
                        if (container != null)
                        {
                            var transform = container.TransformToVisual(field);
                            var position = transform.TransformPoint(new Point());

                            Settings.Chats[chat.Id, ChatSetting.Index] = start;
                            Settings.Chats[chat.Id, ChatSetting.Pixel] = field.ActualHeight - (position.Y + container.ActualHeight);

                            Logs.Logger.Debug(Logs.Target.Chat, string.Format("{0} - Saving scrolling position, message: {1}, pixel: {2}", chat.Id, Items[panel.LastVisibleIndex].Id, field.ActualHeight - (position.Y + container.ActualHeight)));
                        }
                        else
                        {
                            Settings.Chats[chat.Id, ChatSetting.Index] = start;
                            Settings.Chats.TryRemove(chat.Id, ChatSetting.Pixel, out double pixel);

                            Logs.Logger.Debug(Logs.Target.Chat, string.Format("{0} - Saving scrolling position, message: {1}, pixel: none", chat.Id, Items[panel.LastVisibleIndex].Id));
                        }

                    }
                    else
                    {
                        Settings.Chats.TryRemove(chat.Id, ChatSetting.Index, out long index);
                        Settings.Chats.TryRemove(chat.Id, ChatSetting.Pixel, out double pixel);

                        Logs.Logger.Debug(Logs.Target.Chat, string.Format("{0} - Removing scrolling position, as last item is chat.LastMessage", chat.Id));
                    }
                }
                else
                {
                    Settings.Chats.TryRemove(chat.Id, ChatSetting.Index, out long index);
                    Settings.Chats.TryRemove(chat.Id, ChatSetting.Pixel, out double pixel);

                    Logs.Logger.Debug(Logs.Target.Chat, string.Format("{0} - Removing scrolling position, generic reason", chat.Id));
                }
            }
            catch
            {
                Settings.Chats.TryRemove(chat.Id, ChatSetting.Index, out long index);
                Settings.Chats.TryRemove(chat.Id, ChatSetting.Pixel, out double pixel);

                Logs.Logger.Debug(Logs.Target.Chat, string.Format("{0} - Removing scrolling position, exception", chat.Id));
            }

            if (Dispatcher != null)
            {
                Dispatcher.Dispatch(SaveDraft);
            }

            return Task.CompletedTask;
        }

        private void ShowSwitchInline(IDictionary<string, object> state)
        {
            if (state.TryGet("switch_query", out string query) && state.TryGet("switch_bot", out int userId))
            {
                state.Remove("switch_query");
                state.Remove("switch_bot");

                var bot = CacheService.GetUser(userId);
                if (bot == null)
                {
                    return;
                }

                SetText(string.Format("@{0} {1}", bot.Username, query), focus: true);
                ResolveInlineBot(bot.Username, query);
            }
        }

        private async void ShowReplyMarkup(Chat chat)
        {
            if (chat.ReplyMarkupMessageId == 0)
            {
                Delegate?.UpdateChatReplyMarkup(chat, null);
            }
            else
            {
                var response = await ProtoService.SendAsync(new GetMessage(chat.Id, chat.ReplyMarkupMessageId));
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

        public async void ShowPinnedMessage(Chat chat)
        {
            if (chat == null || chat.PinnedMessageId == 0)
            {
                Delegate?.UpdatePinnedMessage(chat, null, false);
            }
            else
            {
                var pinned = Settings.GetChatPinnedMessage(chat.Id);
                if (pinned == chat.PinnedMessageId)
                {
                    Delegate?.UpdatePinnedMessage(chat, null, false);
                    return;
                }

                Delegate?.UpdatePinnedMessage(chat, null, true);

                var response = await ProtoService.SendAsync(new GetMessage(chat.Id, chat.PinnedMessageId));
                if (response is Message message)
                {
                    Delegate?.UpdatePinnedMessage(chat, _messageFactory.Create(this, message), false);
                }
                else
                {
                    Delegate?.UpdatePinnedMessage(chat, null, false);
                }
            }
        }

        private async void ShowDraftMessage(Chat chat)
        {
            var draft = chat.DraftMessage;
            if (draft == null)
            {
                SetText(null as string);
                ComposerHeader = null;
            }
            else
            {
                if (draft.InputMessageText is InputMessageText text)
                {
                    SetText(text.Text);
                }

                if (draft.ReplyToMessageId != 0)
                {
                    var response = await ProtoService.SendAsync(new GetMessage(chat.Id, draft.ReplyToMessageId));
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
            get
            {
                return _autocomplete;
            }
            set
            {
                Set(ref _autocomplete, value);
            }
        }

        private bool _hasBotCommands;
        public bool HasBotCommands
        {
            get
            {
                return _hasBotCommands;
            }
            set
            {
                Set(ref _hasBotCommands, value);
            }
        }

        private List<UserCommand> _botCommands;
        public List<UserCommand> BotCommands
        {
            get
            {
                return _botCommands;
            }
            set
            {
                Set(ref _botCommands, value);
            }
        }

        public void SaveDraft()
        {
            if (_currentInlineBot != null)
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
                var supergroup = ProtoService.GetSupergroup(super.SupergroupId);
                if (supergroup != null && !supergroup.CanPostMessages())
                {
                    return;
                }
            }

            var formattedText = GetFormattedText();
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
                if (formattedText.Text.Length > CacheService.Options.MessageTextLengthMax * 4)
                {
                    formattedText = formattedText.Substring(0, CacheService.Options.MessageTextLengthMax * 4);
                }

                draft = new DraftMessage(reply, 0, new InputMessageText(formattedText, false, false));
            }

            ProtoService.Send(new SetChatDraftMessage(_chat.Id, draft));
        }

        #region Reply 

        private MessageComposerHeader _composerHeader;
        public MessageComposerHeader ComposerHeader
        {
            get
            {
                return _composerHeader;
            }
            set
            {
                Set(ref _composerHeader, value);
                Delegate?.UpdateComposerHeader(_chat, value);
            }
        }

        private bool _disableWebPagePreview;
        public bool DisableWebPagePreview
        {
            get
            {
                var chat = _chat;
                if (chat == null)
                {
                    return false;
                }

                // Force disable if needed
                if (chat.Type is ChatTypeSecret && !Settings.IsSecretPreviewsEnabled)
                {
                    return true;
                }

                return _disableWebPagePreview;
            }
            set
            {
                Set(ref _disableWebPagePreview, value);
            }
        }

        public RelayCommand ClearReplyCommand { get; }
        private void ClearReplyExecute()
        {
            var container = _composerHeader;
            if (container == null)
            {
                return;
            }

            if (container.WebPagePreview != null)
            {
                if (container.IsEmpty)
                {
                    ComposerHeader = null;
                }
                else
                {
                    ComposerHeader = new MessageComposerHeader { EditingMessage = container.EditingMessage, ReplyToMessage = container.ReplyToMessage, WebPagePreview = null };
                }

                DisableWebPagePreview = true;
            }
            else
            {
                if (container.EditingMessage != null)
                {
                    if (container.EditingMessageFileId is int fileId)
                    {
                        ProtoService.Send(new CancelUploadFile(fileId));
                    }

                    var chat = _chat;
                    if (chat != null)
                    {
                        ShowDraftMessage(chat);
                    }
                    else
                    {
                        SetText(null, false);
                    }
                }

                ComposerHeader = null;
            }

            TextField?.Focus(FocusState.Programmatic);
        }

        private long GetReply(bool clean)
        {
            var embedded = _composerHeader;
            if (embedded == null || embedded.ReplyToMessage == null)
            {
                return 0;
            }

            if (clean)
            {
                ComposerHeader = null;
            }

            return embedded.ReplyToMessage.Id;
        }

        #endregion

        public RelayCommand<string> SendCommand { get; }
        private async void SendMessage(string args)
        {
            await SendMessageAsync(args);
        }

        public Task SendMessageAsync(FormattedText formattedText, SendMessageOptions options = null)
        {
            return SendMessageAsync(formattedText.Text, formattedText.Entities, options);
        }

        public async Task SendMessageAsync(string text, IList<TextEntity> entities = null, SendMessageOptions options = null)
        {
            text = text.Replace('\v', '\n').Replace('\r', '\n');

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
                options = await PickSendMessageOptionsAsync();
            }

            if (options == null)
            {
                return;
            }

            var disablePreview = DisableWebPagePreview;
            DisableWebPagePreview = false;

            var header = _composerHeader;
            if (header?.EditingMessage != null)
            {
                var editing = header.EditingMessage;
                var factory = header.EditingMessageMedia;
                if (factory != null)
                {
                    var response = await ProtoService.SendAsync(new UploadFile(factory.InputFile, factory.Type, 32));
                    if (response is File file)
                    {
                        if (file.Remote.IsUploadingCompleted)
                        {
                            ComposerHeader = null;
                            ProtoService.Send(new EditMessageMedia(chat.Id, header.EditingMessage.Id, null, header.EditingMessageMedia.Delegate(new InputFileId(file.Id), header.EditingMessageCaption)));
                        }
                        else
                        {
                            ComposerHeader = new MessageComposerHeader
                            {
                                EditingMessage = editing,
                                EditingMessageMedia = factory,
                                EditingMessageCaption = formattedText,
                                EditingMessageFileId = file.Id
                            };
                        }
                    }
                    else
                    {
                        // TODO: ...
                    }
                }
                else
                {
                    Function function;
                    if (editing.Content is MessageText)
                    {
                        function = new EditMessageText(chat.Id, editing.Id, null, new InputMessageText(formattedText, disablePreview, true));
                    }
                    else
                    {
                        function = new EditMessageCaption(chat.Id, editing.Id, null, formattedText);
                    }

                    var response = await ProtoService.SendAsync(function);
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
                var reply = GetReply(true);

                //if (string.Equals(text.Trim(), "\uD83C\uDFB2"))
                if (CacheService.IsDiceEmoji(text, out string dice))
                {
                    var input = new InputMessageDice(dice, true);
                    await SendMessageAsync(reply, input, options);
                }
                else
                {
                    if (text.Length > CacheService.Options.MessageTextLengthMax)
                    {
                        foreach (var split in formattedText.Split(CacheService.Options.MessageTextLengthMax))
                        {
                            var input = new InputMessageText(split, disablePreview, true);
                            await SendMessageAsync(reply, input, options);
                        }
                    }
                    else if (text.Length > 0)
                    {
                        var input = new InputMessageText(formattedText, disablePreview, true);
                        await SendMessageAsync(reply, input, options);
                    }
                    else
                    {
                        await LoadMessageSliceAsync(null, chat.LastMessage?.Id ?? long.MaxValue, VerticalAlignment.Bottom, 8);
                    }
                }
            }


            /*                        if (response.Error.TypeEquals(TLErrorType.PEER_FLOOD))
                        {
                            var dialog = new TLMessageDialog();
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
            //ProtoService.Send(new SendMessage())
        }

        #region Join channel

        public RelayCommand JoinChannelCommand { get; }
        private async void JoinChannelExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var response = await ProtoService.SendAsync(new JoinChat(chat.Id));
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

        public RelayCommand ToggleSilentCommand { get; }
        private void ToggleSilentExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            ProtoService.Send(new ToggleChatDefaultDisableNotification(chat.Id, !chat.DefaultDisableNotification));
        }

        #endregion

        #region Report Spam

        public RelayCommand RemoveActionBarCommand { get; }
        private void RemoveActionBarExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            ProtoService.Send(new RemoveChatActionBar(chat.Id));
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

                var fullInfo = CacheService.GetSupergroupFull(chat);
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

            var confirm = await TLMessageDialog.ShowAsync(message, title, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            ProtoService.Send(new ReportChat(chat.Id, reason, new long[0]));

            if (chat.Type is ChatTypeBasicGroup || chat.Type is ChatTypeSupergroup)
            {
                ProtoService.Send(new LeaveChat(chat.Id));
            }
            else if (chat.Type is ChatTypePrivate || chat.Type is ChatTypeSecret)
            {
                var user = ProtoService.GetUser(chat);
                if (user != null)
                {
                    ProtoService.Send(new BlockUser(user.Id));
                }
            }

            ProtoService.Send(new DeleteChatHistory(chat.Id, true, false));
        }

        #endregion

        #region Stickers

        public RelayCommand OpenStickersCommand { get; }
        private void OpenStickersExecute()
        {
            _stickers.SyncStickers(_chat);
            _stickers.SyncGifs();
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

                ProtoService.Send(new DeleteChatHistory(chat.Id, true, false));
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

            var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.AreYouSureClearHistory, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                ProtoService.Send(new DeleteChatHistory(chat.Id, false, false));
            }
        }

        #endregion

        #region Call

        public RelayCommand CallCommand { get; }
        private async void CallExecute()
        {
            //var user = With as TLUser;
            //if (user == null)
            //{
            //    return;
            //}

            //try
            //{
            //    var coordinator = VoipCallCoordinator.GetDefault();
            //    var result = await coordinator.ReserveCallResourcesAsync("Unigram.Tasks.VoIPCallTask");
            //    if (result == VoipPhoneCallResourceReservationStatus.Success)
            //    {
            //        await VoIPConnection.Current.SendRequestAsync("voip.startCall", user);
            //    }
            //}
            //catch
            //{
            //    await TLMessageDialog.ShowAsync("Something went wrong. Please, try to close and relaunch the app.", "Unigram", "OK");
            //}

            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var user = CacheService.GetUser(chat);
            if (user == null)
            {
                return;
            }

            var call = _voipService.ActiveCall;
            if (call != null)
            {
                var callUser = CacheService.GetUser(call.UserId);
                if (callUser != null && callUser.Id != user.Id)
                {
                    var confirm = await TLMessageDialog.ShowAsync(string.Format(Strings.Resources.VoipOngoingAlert, callUser.GetFullName(), user.GetFullName()), Strings.Resources.VoipOngoingAlertTitle, Strings.Resources.OK, Strings.Resources.Cancel);
                    if (confirm == ContentDialogResult.Primary)
                    {

                    }
                }
                else
                {
                    _voipService.Show();
                }

                return;
            }

            var fullInfo = CacheService.GetUserFull(user.Id);
            if (fullInfo != null && fullInfo.HasPrivateCalls)
            {
                await TLMessageDialog.ShowAsync(string.Format(Strings.Resources.CallNotAvailable, user.GetFullName()), Strings.Resources.VoipFailed, Strings.Resources.OK);
                return;
            }

            var response = await ProtoService.SendAsync(new CreateCall(user.Id, new CallProtocol(true, true, 65, libtgvoip.VoIPControllerWrapper.GetConnectionMaxLayer(), new string[0])));
            if (response is Error error)
            {
                if (error.Code == 400 && error.Message.Equals("PARTICIPANT_VERSION_OUTDATED"))
                {
                    await TLMessageDialog.ShowAsync(string.Format(Strings.Resources.VoipPeerOutdated, user.GetFullName()), Strings.Resources.AppName, Strings.Resources.OK);
                }
                else if (error.Code == 400 && error.Message.Equals("USER_PRIVACY_RESTRICTED"))
                {
                    await TLMessageDialog.ShowAsync(string.Format(Strings.Resources.CallNotAvailable, user.GetFullName()), Strings.Resources.AppName, Strings.Resources.OK);
                }
            }
        }

        #endregion

        #region Unpin message

        public RelayCommand UnpinMessageCommand { get; }
        private async void UnpinMessageExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var supergroupType = chat.Type as ChatTypeSupergroup;
            var basicGroupType = chat.Type as ChatTypeBasicGroup;

            var supergroup = supergroupType != null ? CacheService.GetSupergroup(supergroupType.SupergroupId) : null;
            var basicGroup = basicGroupType != null ? CacheService.GetBasicGroup(basicGroupType.BasicGroupId) : null;

            if (supergroup != null && supergroup.CanPinMessages() ||
                basicGroup != null && basicGroup.CanPinMessages() ||
                chat.Type is ChatTypePrivate privata && privata.UserId == CacheService.Options.MyId)
            {
                var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.UnpinMessageAlert, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
                if (confirm == ContentDialogResult.Primary)
                {
                    Delegate?.UpdatePinnedMessage(chat, null, false);
                    ProtoService.Send(new UnpinChatMessage(chat.Id));
                }
            }
            else
            {
                Settings.SetChatPinnedMessage(chat.Id, chat.PinnedMessageId);
                Delegate?.UpdatePinnedMessage(chat, null, false);
            }
        }

        #endregion

        #region Unblock

        public RelayCommand UnblockCommand { get; }
        private async void UnblockExecute()
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

            var user = CacheService.GetUser(privata.UserId);
            if (user.Type is UserTypeBot)
            {
                await ProtoService.SendAsync(new UnblockUser(user.Id));
                StartExecute();
            }
            else
            {
                var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.AreYouSureUnblockContact, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }

                ProtoService.Send(new UnblockUser(privata.UserId));
            }
        }

        #endregion

        #region Switch

        public RelayCommand<string> SwitchCommand { get; }
        private void SwitchExecute(string start)
        {
            if (_currentInlineBot == null)
            {
                return;
            }
            else
            {

            }

            // TODO: edit to send it automatically
            //NavigationService.NavigateToDialog(_currentInlineBot, accessToken: switchPM.StartParam);
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

            var user = CacheService.GetUser(chat);
            if (user == null)
            {
                return;
            }

            ProtoService.Send(new SharePhoneNumber(user.Id));
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

            var user = CacheService.GetUser(chat);
            if (user == null)
            {
                return;
            }

            var fullInfo = CacheService.GetUserFull(chat);
            if (fullInfo == null)
            {
                return;
            }

            var dialog = new EditUserNameView(user.FirstName, user.LastName, fullInfo.NeedPhoneNumberPrivacyException);

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                ProtoService.Send(new AddContact(new Contact(user.PhoneNumber, dialog.FirstName, dialog.LastName, string.Empty, user.Id),
                    fullInfo.NeedPhoneNumberPrivacyException ? dialog.SharePhoneNumber : true));
            }
        }

        #endregion

        #region Start

        public RelayCommand StartCommand { get; }
        private async void StartExecute()
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

            if (_accessToken == null)
            {
                await SendMessageAsync(chat.Type is ChatTypePrivate ? "/start" : "/start@" + bot.Username);
            }
            else
            {
                var token = _accessToken;

                AccessToken = null;
                ProtoService.Send(new SendBotStartMessage(bot.Id, chat.Id, token));
            }
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
                return CacheService.GetUser(privata.UserId);
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
            Search = new ChatSearchViewModel(ProtoService, CacheService, Settings, Aggregator, this);
        }

        #endregion

        #region Jump to date

        public RelayCommand JumpDateCommand { get; }
        private async void JumpDateExecute()
        {
            var dialog = new Controls.Views.CalendarView();
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

        public RelayCommand ReadMentionsCommand { get; }
        private void ReadMentionsExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            ProtoService.Send(new ReadAllChatMentions(chat.Id));
        }

        #endregion

        #region Report Chat

        public RelayCommand ReportCommand { get; }
        private async void ReportExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var items = new[]
            {
                new SelectRadioItem(new ChatReportReasonSpam(), Strings.Resources.ReportChatSpam, false),
                new SelectRadioItem(new ChatReportReasonViolence(), Strings.Resources.ReportChatViolence, false),
                new SelectRadioItem(new ChatReportReasonPornography(), Strings.Resources.ReportChatPornography, false),
                new SelectRadioItem(new ChatReportReasonChildAbuse(), Strings.Resources.ReportChatChild, false),
                new SelectRadioItem(new ChatReportReasonCustom(), Strings.Resources.ReportChatOther, true)
            };

            var dialog = new SelectRadioView(items);
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

            if (reason is ChatReportReasonCustom other)
            {
                var input = new InputDialog();
                input.Title = Strings.Resources.ReportChat;
                input.PlaceholderText = Strings.Resources.ReportChatDescription;
                input.IsPrimaryButtonEnabled = true;
                input.IsSecondaryButtonEnabled = true;
                input.PrimaryButtonText = Strings.Resources.OK;
                input.SecondaryButtonText = Strings.Resources.Cancel;

                var inputResult = await input.ShowQueuedAsync();
                if (inputResult == ContentDialogResult.Primary)
                {
                    other.Text = input.Text;
                }
                else
                {
                    return;
                }
            }

            var response = await ProtoService.SendAsync(new ReportChat(chat.Id, reason, new long[0]));
        }

        #endregion

        #region Set timer

        public RelayCommand SetTimerCommand { get; }
        private async void SetTimerExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var secretChat = CacheService.GetSecretChat(chat);
            if (secretChat == null)
            {
                return;
            }

            var dialog = new ChatTtlView();
            dialog.Value = secretChat.Ttl;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            ProtoService.Send(new SendChatSetTtlMessage(chat.Id, dialog.Value));
        }

        #endregion

        #region Scheduled messages

        public RelayCommand ScheduledCommand { get; }
        private void ScheduledExecute()
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

        public RelayCommand ActionCommand { get; }
        private void ActionExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypePrivate privata)
            {
                var fullInfo = ProtoService.GetUserFull(privata.UserId);
                if (fullInfo == null)
                {
                    return;
                }

                if (fullInfo.IsBlocked)
                {
                    UnblockExecute();
                }
                else if (fullInfo.BotInfo != null)
                {
                    StartExecute();
                }
            }
            else if (chat.Type is ChatTypeSupergroup super)
            {
                var group = ProtoService.GetSupergroup(super.SupergroupId);
                if (group == null)
                {
                    return;
                }

                if (group.IsChannel)
                {
                    if (group.Status is ChatMemberStatusLeft || (group.Status is ChatMemberStatusCreator creator && !creator.IsMember))
                    {
                        JoinChannelExecute();
                    }
                    else
                    {
                        ToggleMuteExecute(CacheService.GetNotificationSettingsMuteFor(chat) > 0);
                    }
                }
                else
                {
                    if (group.Status is ChatMemberStatusLeft || (group.Status is ChatMemberStatusRestricted restricted && !restricted.IsMember) || (group.Status is ChatMemberStatusCreator creator && !creator.IsMember))
                    {
                        JoinChannelExecute();
                    }
                    else if (group.Status is ChatMemberStatusBanned)
                    {
                        ChatDeleteExecute();
                    }
                }
            }
        }

        #endregion

        #region Linked chat

        public RelayCommand DiscussCommand { get; }
        private void DiscussExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var full = CacheService.GetSupergroupFull(chat);
            if (full == null)
            {
                return;
            }

            if (full.LinkedChatId != 0)
            {
                NavigationService.NavigateToChat(full.LinkedChatId);
            }
        }

        #endregion

        #region Open message

        public RelayCommand<Message> OpenMessageCommand { get; }
        private void OpenMessageExecute(Message message)
        {
            var webPage = message.Content is MessageText text ? text.WebPage : null;

            if (message.Content is MessageVoiceNote || webPage?.VoiceNote != null)
            {
                NavigationService.NavigateToChat(message.ChatId, message: message.Id);
            }
        }

        #endregion
    }

    public class UserCommand
    {
        public UserCommand(int userId, BotCommand command)
        {
            UserId = userId;
            Item = command;
        }

        public int UserId { get; set; }
        public BotCommand Item { get; set; }
    }

    public class MessageCollection : MvxObservableCollection<MessageViewModel>
    {
        private readonly Dictionary<long, long> _messages = new Dictionary<long, long>();
        private readonly HashSet<DateTime> _dates = new HashSet<DateTime>();

        public Action<IEnumerable<MessageViewModel>> AttachChanged;

        //~MessageCollection()
        //{
        //    Debug.WriteLine("Finalizing MessageCollection");
        //    GC.Collect();
        //}

        public bool ContainsKey(long id)
        {
            return _messages.ContainsKey(id);
        }

        protected override void InsertItem(int index, MessageViewModel item)
        {
            _messages[item.Id] = item.Id;

            item.IsFirst = true;
            item.IsLast = true;
            base.InsertItem(index, item);

            var previous = index > 0 ? this[index - 1] : null;
            var next = index < Count - 1 ? this[index + 1] : null;

            UpdateSeparatorOnInsert(item, next, index);
            UpdateSeparatorOnInsert(previous, item, index - 1);

            var hash1 = AttachHash(item);
            var hash2 = AttachHash(next);
            var hash3 = AttachHash(previous);

            UpdateAttach(next, item, index + 1);
            UpdateAttach(item, previous, index);

            var update1 = AttachHash(item);
            var update2 = AttachHash(next);
            var update3 = AttachHash(previous);

            AttachChanged?.Invoke(new[]
            {
                hash3 != update3 ? previous : null,
                hash1 != update1 ? item : null,
                hash2 != update2 ? next : null
            });
        }

        protected override void RemoveItem(int index)
        {
            _messages.Remove(this[index].Id);

            var next = index > 0 ? this[index - 1] : null;
            var previous = index < Count - 1 ? this[index + 1] : null;

            var hash2 = AttachHash(next);
            var hash3 = AttachHash(previous);

            UpdateAttach(previous, next, index + 1);

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

        private static MessageViewModel ShouldUpdateSeparatorOnInsert(MessageViewModel item, MessageViewModel previous, int index)
        {
            if (item != null && previous != null)
            {
                var itemDate = Utils.UnixTimestampToDateTime(GetMessageDate(item));
                var previousDate = Utils.UnixTimestampToDateTime(GetMessageDate(previous));
                if (previousDate.Date != itemDate.Date)
                {
                    var service = new MessageViewModel(previous.ProtoService, previous.PlaybackService, previous.Delegate, new Message(0, previous.SenderUserId, previous.ChatId, null, previous.SchedulingState, previous.IsOutgoing, false, false, true, false, previous.IsChannelPost, false, previous.Date, 0, null, 0, 0, 0, 0, string.Empty, 0, 0, string.Empty, new MessageHeaderDate(), null));
                    return service;
                }
            }

            return null;
        }

        private void UpdateSeparatorOnInsert(MessageViewModel item, MessageViewModel previous, int index)
        {
            var service = ShouldUpdateSeparatorOnInsert(item, previous, index);
            if (service != null)
            {
                base.InsertItem(index + 1, service);
            }
        }

        //private static void UpdateSeparatorOnInsert(IList<TLMessageBase> items, TLMessageBase item, TLMessageBase previous, int index)
        //{
        //    var service = ShouldUpdateSeparatorOnInsert(item, previous, index);
        //    if (service != null)
        //    {
        //        items.Insert(index + 1, service);
        //    }
        //}

        private static bool ShouldUpdateSeparatorOnRemove(MessageViewModel next, MessageViewModel previous, int index)
        {
            if (next != null && next.Content is MessageHeaderDate && previous != null)
            {
                var itemDate = Utils.UnixTimestampToDateTime(GetMessageDate(next));
                var previousDate = Utils.UnixTimestampToDateTime(GetMessageDate(previous));
                if (previousDate.Date != itemDate.Date)
                {
                    //base.RemoveItem(index - 1);
                    return true;
                }
            }
            else if (next != null && next.Content is MessageHeaderDate && previous == null)
            {
                //base.RemoveItem(index - 1);
                return true;
            }

            return false;
        }

        private void UpdateSeparatorOnRemove(MessageViewModel next, MessageViewModel previous, int index)
        {
            if (ShouldUpdateSeparatorOnRemove(next, previous, index))
            {
                base.RemoveItem(index - 1);
            }
        }

        //private static void UpdateSeparatorOnRemove(IList<TLMessageBase> items, TLMessageBase next, TLMessageBase previous, int index)
        //{
        //    if (ShouldUpdateSeparatorOnRemove(next, previous, index))
        //    {
        //        items.RemoveAt(index - 1);
        //    }
        //}

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

        private static void UpdateAttach(MessageViewModel item, MessageViewModel previous, int index)
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
                         //!(previous is TLMessageService && !(((TLMessageService)previous).Action is TLMessageActionPhoneCall)) &&
                         !(previous.IsService()) &&
                         AreTogether(item, previous) &&
                         GetMessageDate(item) - GetMessageDate(previous) < 900;
            }

            item.IsFirst = !attach;

            if (previous != null)
            {
                previous.IsLast = item.IsFirst || item.IsService();
            }
        }

        private static bool AreTogether(MessageViewModel message1, MessageViewModel message2)
        {
            var saved1 = message1.IsSaved();
            var saved2 = message2.IsSaved();
            if (saved1 && saved2)
            {
                if (message1.ForwardInfo?.Origin is MessageForwardOriginUser fromUser1 && message2.ForwardInfo?.Origin is MessageForwardOriginUser fromUser2)
                {
                    return fromUser1.SenderUserId == fromUser2.SenderUserId && message1.ForwardInfo.FromChatId == message2.ForwardInfo.FromChatId;
                }
                else if (message1.ForwardInfo?.Origin is MessageForwardOriginChannel post1 && message2.ForwardInfo?.Origin is MessageForwardOriginChannel post2)
                {
                    return post1.ChatId == post2.ChatId && message1.ForwardInfo.FromChatId == message2.ForwardInfo.FromChatId;
                }
            }
            else if (saved1 || saved2)
            {
                return false;
            }

            return message1.SenderUserId == message2.SenderUserId;
        }
    }

    public class MessageComposerHeader
    {
        public MessageViewModel ReplyToMessage { get; set; }
        public MessageViewModel EditingMessage { get; set; }

        public int? EditingMessageFileId { get; set; }
        public InputMessageFactory EditingMessageMedia { get; set; }
        public FormattedText EditingMessageCaption { get; set; }

        public WebPage WebPagePreview { get; set; }
        public string WebPageUrl { get; set; }

        public bool IsWebPageHidden { get; set; }

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

    public enum ReplyToMessageState
    {
        None,
        Loading,
        Deleted
    }
}
