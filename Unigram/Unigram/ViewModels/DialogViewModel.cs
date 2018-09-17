using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Template10.Common;
using Template10.Services.NavigationService;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Views;
using Unigram.Converters;
using Unigram.Core.Common;
using Unigram.Core.Services;
using Unigram.Native.Tasks;
using Unigram.Entities;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Dialogs;
using Unigram.Views;
using Unigram.Views.Supergroups;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Calls;
using Windows.Foundation;
using Windows.UI.Notifications;
using Windows.UI.StartScreen;
using Windows.UI.Text;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
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

            //for (int i = 0; i < messages.Count; i++)
            //{
            //    if (messages[i] is TLMessage message && message.Media is TLMessageMediaGroup groupMedia)
            //    {
            //        messages.RemoveAt(i);

            //        for (int j = 0; j < groupMedia.Layout.Messages.Count; j++)
            //        {
            //            messages.Insert(i, groupMedia.Layout.Messages[j]);
            //            i++;
            //        }

            //        i--;
            //    }
            //}

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

        private static readonly Dictionary<long, long> _scrollingIndex = new Dictionary<long, long>();
        private static readonly Dictionary<long, double> _scrollingPixel = new Dictionary<long, double>();

        private readonly DisposableMutex _loadMoreLock = new DisposableMutex();
        private readonly DisposableMutex _insertLock = new DisposableMutex();

        private readonly DialogStickersViewModel _stickers;
        private readonly ILocationService _locationService;
        private readonly ILiveLocationService _liveLocationService;
        private readonly INotificationsService _pushService;
        private readonly IPlaybackService _playbackService;
        private readonly IVoIPService _voipService;

        //private UserActivitySession _timelineSession;

        public IDialogDelegate Delegate { get; set; }

        public DialogViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, ILocationService locationService, ILiveLocationService liveLocationService, INotificationsService pushService, IPlaybackService playbackService, IVoIPService voipService)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _locationService = locationService;
            _liveLocationService = liveLocationService;
            _pushService = pushService;
            _playbackService = playbackService;
            _voipService = voipService;

            _stickers = new DialogStickersViewModel(protoService, cacheService, settingsService, aggregator);

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
            PinnedCommand = new RelayCommand(PinnedExecute);
            JoinChannelCommand = new RelayCommand(JoinChannelExecute);
            ToggleMuteCommand = new RelayCommand<bool>(ToggleMuteExecute);
            MuteCommand = new RelayCommand(() => ToggleMuteExecute(false));
            UnmuteCommand = new RelayCommand(() => ToggleMuteExecute(true));
            ToggleSilentCommand = new RelayCommand(ToggleSilentExecute);
            HideReportSpamCommand = new RelayCommand(HideReportSpamExecute);
            ReportSpamCommand = new RelayCommand(ReportSpamExecute);
            ReportCommand = new RelayCommand(ReportExecute);
            OpenStickersCommand = new RelayCommand(OpenStickersExecute);
            DialogDeleteCommand = new RelayCommand(DialogDeleteExecute);
            DialogClearCommand = new RelayCommand(DialogClearExecute);
            CallCommand = new RelayCommand(CallExecute);
            UnpinMessageCommand = new RelayCommand(UnpinMessageExecute);
            UnblockCommand = new RelayCommand(UnblockExecute);
            ShareContactCommand = new RelayCommand(ShareContactExecute);
            AddContactCommand = new RelayCommand(AddContactExecute);
            PinChatCommand = new RelayCommand(PinChatExecute, CanExecutePinChatCommand);
            UnpinChatCommand = new RelayCommand(UnpinChatExecute, CanUnpinChatExecute);
            StartCommand = new RelayCommand(StartExecute);
            SearchCommand = new RelayCommand(SearchExecute);
            JumpDateCommand = new RelayCommand(JumpDateExecute);
            GroupStickersCommand = new RelayCommand(GroupStickersExecute);
            ReadMentionsCommand = new RelayCommand(ReadMentionsExecute);
            SendCommand = new RelayCommand<string>(SendMessage);
            SwitchCommand = new RelayCommand<string>(SwitchExecute);
            SetTimerCommand = new RelayCommand(SetTimerExecute);
            ActionCommand = new RelayCommand(ActionExecute);

            MessagesForwardCommand = new RelayCommand(MessagesForwardExecute, MessagesForwardCanExecute);
            MessagesDeleteCommand = new RelayCommand(MessagesDeleteExecute, MessagesDeleteCanExecute);
            MessagesCopyCommand = new RelayCommand(MessagesCopyExecute, MessagesCopyCanExecute);
            MessagesReportCommand = new RelayCommand(MessagesReportExecute, MessagesReportCanExecute);

            MessageReplyLastCommand = new RelayCommand(MessageReplyLastExecute);
            MessageReplyCommand = new RelayCommand<MessageViewModel>(MessageReplyExecute);
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
            MessageStickerPackInfoCommand = new RelayCommand<MessageViewModel>(MessageStickerPackInfoExecute);
            MessageFaveStickerCommand = new RelayCommand<MessageViewModel>(MessageFaveStickerExecute);
            MessageUnfaveStickerCommand = new RelayCommand<MessageViewModel>(MessageUnfaveStickerExecute);
            MessageSaveMediaCommand = new RelayCommand<MessageViewModel>(MessageSaveMediaExecute);
            MessageSaveDownloadCommand = new RelayCommand<MessageViewModel>(MessageSaveDownloadExecute);
            MessageSaveAnimationCommand = new RelayCommand<MessageViewModel>(MessageSaveAnimationExecute);
            MessageAddContactCommand = new RelayCommand<MessageViewModel>(MessageAddContactExecute);
            MessageServiceCommand = new RelayCommand<MessageViewModel>(MessageServiceExecute);

            SendStickerCommand = new RelayCommand<Sticker>(SendStickerExecute);
            SendAnimationCommand = new RelayCommand<Animation>(SendAnimationExecute);
            SendFileCommand = new RelayCommand(SendFileExecute);
            SendMediaCommand = new RelayCommand(SendMediaExecute);
            SendContactCommand = new RelayCommand(SendContactExecute);
            SendLocationCommand = new RelayCommand(SendLocationExecute);

            //Items = new LegacyMessageCollection();
            //Items.CollectionChanged += (s, args) => IsEmpty = Items.Count == 0;

            Aggregator.Subscribe(this);
        }

        //~DialogViewModel()
        //{
        //    Debug.WriteLine("Finalizing DialogViewModel");
        //    Aggregator.Unsubscribe(this);

        //    GC.Collect();
        //}

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

        public bool IsActive
        {
            get
            {
                return NavigationService?.IsPeerActive(_chatId) ?? false;
            }
        }

        public DialogStickersViewModel Stickers => _stickers;

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

        private object _full;
        public object Full
        {
            get
            {
                return _full;
            }
            set
            {
                Set(ref _full, value);
                RaisePropertyChanged(() => IsSilentVisible);
            }
        }

        private string _lastSeen;
        public string LastSeen
        {
            get
            {
                return _lastSeen;
            }
            set
            {
                Set(ref _lastSeen, value);
            }
        }

        private DialogSearchViewModel _search;
        public DialogSearchViewModel Search
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

        private Message _pinnedMessage;
        public Message PinnedMessage
        {
            get
            {
                return _pinnedMessage;
            }
            set
            {
                Set(ref _pinnedMessage, value);
            }
        }

        private DispatcherTimer _informativeTimer;

        private Message _informativeMessage;
        public Message InformativeMessage
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
            }
        }

        public int UnreadCount
        {
            get
            {
                return _chat?.UnreadCount ?? 0;
            }
        }

        private bool _isReportSpam;
        public bool IsReportSpam
        {
            get
            {
                return _isReportSpam;
            }
            set
            {
                Set(ref _isReportSpam, value);
            }
        }

        private bool _canPinChat;
        public bool CanPinChat
        {
            get
            {
                return _canPinChat;
            }
            set
            {
                Set(ref _canPinChat, value);
            }
        }

        private bool _canUnpinChat;
        public bool CanUnpinChat
        {
            get
            {
                return _canUnpinChat;
            }
            set
            {
                Set(ref _canUnpinChat, value);
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
            }
        }

        public BubbleTextBox TextField { get; set; }
        public BubbleListView ListField { get; set; }

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

        public string GetText()
        {
            var field = TextField;
            if (field == null)
            {
                return null;
            }

            field.Document.GetText(TextGetOptions.NoHidden, out string text);
            return text;
        }

        public bool IsEndReached()
        {
            var chat = _chat;
            if (chat == null)
            {
                return false;
            }

            var last = Items.LastOrDefault();
            if (last == null)
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

        public async Task LoadNextSliceAsync(bool force = false)
        {
            using (await _loadMoreLock.WaitAsync())
            {
                if (_isLoadingNextSlice || _isLoadingPreviousSlice || _chat == null || Items.Count < 1)
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
                var maxId = Items.FirstOrDefault().Id;
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

                var response = await ProtoService.SendAsync(new GetChatHistory(_chat.Id, maxId, 0, limit, false));
                if (response is Telegram.Td.Api.Messages messages)
                {
                    if (messages.MessagesValue.Count > 0)
                    {
                        SetScrollMode(ItemsUpdatingScrollMode.KeepLastItemInView, force);
                    }

                    var replied = messages.MessagesValue.OrderByDescending(x => x.Id).Select(x => GetMessage(x)).ToList();
                    ProcessFiles(_chat, replied);
                    ProcessReplies(replied);

                    //Items.InsertRange(0, replied);

                    foreach (var message in replied)
                    {
                        Items.Insert(0, message);

                        //var index = InsertMessageInOrder(Messages, message);
                    }
                }

                _isLoadingNextSlice = false;
                IsLoading = false;
            }
        }

        public async Task LoadPreviousSliceAsync(bool force = false, bool reserved = false)
        {
            using (await _loadMoreLock.WaitAsync())
            {
                if (_isLoadingNextSlice || _isLoadingPreviousSlice || _chat == null || Items.Count < 1)
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

                var maxId = Items.LastOrDefault().Id;
                var limit = 50;

                //for (int i = 0; i < Messages.Count; i++)
                //{
                //    if (Messages[i].Id != 0 && Messages[i].Id < maxId)
                //    {
                //        maxId = Messages[i].Id;
                //    }
                //}

                var response = await ProtoService.SendAsync(new GetChatHistory(_chat.Id, maxId, -49, limit, false));
                if (response is Telegram.Td.Api.Messages messages)
                {
                    if (messages.MessagesValue.Any(x => !Items.ContainsKey(x.Id)))
                    {
                        SetScrollMode(ItemsUpdatingScrollMode.KeepItemsInView, true);
                    }
                    else
                    {
                        SetScrollMode(ItemsUpdatingScrollMode.KeepLastItemInView, true);
                    }

                    var added = false;

                    var replied = messages.MessagesValue.OrderBy(x => x.Id).Select(x => GetMessage(x)).ToList();
                    ProcessFiles(_chat, replied);
                    ProcessReplies(replied);

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

                await LoadMessageSliceAsync(null, chat.LastMessage?.Id ?? long.MaxValue, SnapPointsAlignment.Far, 8);
            }

            TextField?.FocusMaybe(FocusState.Keyboard);
        }

        public async Task LoadMessageSliceAsync(long? previousId, long maxId, SnapPointsAlignment alignment = SnapPointsAlignment.Center, double? pixel = null, bool second = false)
        {
            var already = Items.FirstOrDefault(x => x.Id == maxId);
            if (already != null)
            {
                var field = ListField;
                if (field != null)
                {
                    await field.ScrollToItem(already, alignment, alignment == SnapPointsAlignment.Center, pixel);
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
                if (_isLoadingNextSlice || _isLoadingPreviousSlice || _chat == null)
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

                var response = await ProtoService.SendAsync(new GetChatHistory(_chat.Id, maxId, offset, limit, false));
                if (response is Telegram.Td.Api.Messages messages)
                {
                    if (messages.MessagesValue.Count > 0)
                    {
                        SetScrollMode(ItemsUpdatingScrollMode.KeepLastItemInView, true);
                    }

                    var lastRead = false;

                    var replied = messages.MessagesValue.OrderBy(x => x.Id).Select(x => GetMessage(x)).ToList();
                    ProcessFiles(_chat, replied);
                    ProcessReplies(replied);

                    Items.ReplaceWith(replied);

                    IsLastSliceLoaded = null;
                    IsFirstSliceLoaded = IsEndReached();
                }

                _isLoadingNextSlice = false;
                _isLoadingPreviousSlice = false;
                IsLoading = false;
            }

            //await Task.Delay(200);
            //await LoadNextSliceAsync(true);
            await LoadMessageSliceAsync(null, maxId, alignment, pixel, true);
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

        private void ProcessView(Chat chat, IList<MessageViewModel> messages)
        {

        }

        private void ProcessFiles(Chat chat, IList<MessageViewModel> messages)
        {
            foreach (var message in messages)
            {
                var content = message.Content as object;
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
                    if (textMessage?.WebPage?.Animation != null)
                    {
                        content = textMessage?.WebPage?.Animation;
                    }
                    else if (textMessage?.WebPage?.Document != null)
                    {
                        content = textMessage?.WebPage?.Document;
                    }
                    else if (textMessage?.WebPage?.Sticker != null)
                    {
                        content = textMessage?.WebPage?.Sticker;
                    }
                    else if (textMessage?.WebPage?.Video != null)
                    {
                        content = textMessage?.WebPage?.Video;
                    }
                    else if (textMessage?.WebPage?.VideoNote != null)
                    {
                        content = textMessage?.WebPage?.VideoNote;
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
                        _filesMap[animation.Thumbnail.Photo.Id].Add(message);
                    }

                    _filesMap[animation.AnimationValue.Id].Add(message);
                }
                else if (content is Audio audio)
                {
                    if (audio.AlbumCoverThumbnail != null)
                    {
                        _filesMap[audio.AlbumCoverThumbnail.Photo.Id].Add(message);
                    }

                    _filesMap[audio.AudioValue.Id].Add(message);
                }
                else if (content is Document document)
                {
                    if (document.Thumbnail != null)
                    {
                        _filesMap[document.Thumbnail.Photo.Id].Add(message);
                    }

                    _filesMap[document.DocumentValue.Id].Add(message);
                }
                else if (content is Photo photo)
                {
                    foreach (var size in photo.Sizes)
                    {
                        _filesMap[size.Photo.Id].Add(message);
                    }
                }
                else if (content is Sticker sticker)
                {
                    if (sticker.Thumbnail != null)
                    {
                        _filesMap[sticker.Thumbnail.Photo.Id].Add(message);
                    }

                    _filesMap[sticker.StickerValue.Id].Add(message);
                }
                else if (content is Video video)
                {
                    if (video.Thumbnail != null)
                    {
                        _filesMap[video.Thumbnail.Photo.Id].Add(message);
                    }

                    _filesMap[video.VideoValue.Id].Add(message);
                }
                else if (content is VideoNote videoNote)
                {
                    if (videoNote.Thumbnail != null)
                    {
                        _filesMap[videoNote.Thumbnail.Photo.Id].Add(message);
                    }

                    _filesMap[videoNote.Video.Id].Add(message);
                }
                else if (content is VoiceNote voiceNote)
                {
                    _filesMap[voiceNote.Voice.Id].Add(message);
                }

                if (message.IsSaved() || ((chat.Type is ChatTypeBasicGroup || chat.Type is ChatTypeSupergroup) && !message.IsOutgoing && !message.IsChannelPost))
                {
                    if (message.IsSaved())
                    {
                        if (message.ForwardInfo is MessageForwardedFromUser fromUser)
                        {
                            var user = message.ProtoService.GetUser(fromUser.SenderUserId);
                            if (user != null && user.ProfilePhoto != null)
                            {
                                _photosMap[user.ProfilePhoto.Small.Id].Add(message);
                            }
                        }
                        else if (message.ForwardInfo is MessageForwardedPost post)
                        {
                            var fromChat = message.ProtoService.GetChat(post.ForwardedFromChatId);
                            if (fromChat != null && fromChat.Photo != null)
                            {
                                _photosMap[fromChat.Photo.Small.Id].Add(message);
                            }
                        }
                    }
                    else
                    {
                        var user = message.GetSenderUser();
                        if (user != null && user.ProfilePhoto != null)
                        {
                            _photosMap[user.ProfilePhoto.Small.Id].Add(message);
                        }
                    }
                }
            }
        }

        private async void ProcessReplies(IList<MessageViewModel> replied)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            foreach (var message in replied)
            {
                message.ReplyToMessageState = ReplyToMessageState.Loading;
            }

            var replies = new List<long>();

            foreach (var message in replied)
            {
                if (message.Content is MessagePinMessage pinMessage && !replies.Contains(pinMessage.MessageId))
                {
                    replies.Add(pinMessage.MessageId);
                }
                else if (message.Content is MessageGameScore gameScore && !replies.Contains(gameScore.GameMessageId))
                {
                    replies.Add(gameScore.GameMessageId);
                }
                else if (message.Content is MessagePaymentSuccessful paymentSuccessful && !replies.Contains(paymentSuccessful.InvoiceMessageId))
                {
                    replies.Add(paymentSuccessful.InvoiceMessageId);
                }
                else if (message.ReplyToMessageId != 0 && !replies.Contains(message.ReplyToMessageId))
                {
                    replies.Add(message.ReplyToMessageId);
                }
            }

            var response = await ProtoService.SendAsync(new GetMessages(chat.Id, replies));
            if (response is Telegram.Td.Api.Messages messages)
            {
                foreach (var message in replied)
                {
                    foreach (var result in messages.MessagesValue)
                    {
                        if (result == null)
                        {
                            continue;
                        }

                        if (message.Content is MessagePinMessage pinMessage && pinMessage.MessageId == result.Id)
                        {
                            message.ReplyToMessage = GetMessage(result);
                            break;
                        }
                        else if (message.Content is MessageGameScore gameScore && gameScore.GameMessageId == result.Id)
                        {
                            message.ReplyToMessage = GetMessage(result);
                            break;
                        }
                        else if (message.Content is MessagePaymentSuccessful paymentSuccessful && paymentSuccessful.InvoiceMessageId == result.Id)
                        {
                            message.ReplyToMessage = GetMessage(result);
                            break;
                        }
                        else if (message.ReplyToMessageId == result.Id)
                        {
                            message.ReplyToMessage = GetMessage(result);
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

                    var text = message.Content is MessageText tex2 ? tex2.Text : null;
                    Handle(message, bubble => bubble.UpdateMessageReply(message), service => service.UpdateMessage(message));

                    //var container = ListField.ContainerFromItem(message) as ListViewItem;
                    //if (container == null)
                    //{
                    //    return;
                    //}

                    //var content = container.ContentTemplateRoot as FrameworkElement;
                    //if (content is Grid grid)
                    //{
                    //    content = grid.FindName("Bubble") as FrameworkElement;
                    //}

                    //if (content is MessageBubble bubble)
                    //{
                    //    bubble.UpdateMessageReply(message);
                    //}
                }
            }
        }

        private long _chatId;

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            _chatId = (long)parameter;

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
            if (state.TryGet("message_id", out long navigation))
            {
                state.Remove("message_id");
                LoadMessageSliceAsync(null, navigation);
            }
            else
            {
                if (_scrollingIndex.TryGetValue(chat.Id, out long start))
                {
                    if (_scrollingPixel.TryGetValue(chat.Id, out double pixel))
                    {
                        LoadMessageSliceAsync(null, start, SnapPointsAlignment.Far, pixel);
                    }
                    else
                    {
                        LoadMessageSliceAsync(null, start, SnapPointsAlignment.Far);
                    }
                }
                else
                {
                    LoadMessageSliceAsync(null, chat.LastReadInboxMessageId, SnapPointsAlignment.Near);
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
                Items.Add(GetMessage(new Message(0, 0, chat.Id, null, true,  false, false, false, false, false, false, TodayDate(14, 58), 0, null, 0, 0,  0, 0, string.Empty, 0, 0, new MessageText(new FormattedText("Hey Eileen", new TextEntity[0]), null), null)));
                Items.Add(GetMessage(new Message(1, 0, chat.Id, null, true,  false, false, false, false, false, false, TodayDate(14, 59), 0, null, 0, 0,  0, 0, string.Empty, 0, 0, new MessageText(new FormattedText("So, why is Telegram cool?", new TextEntity[0]), null), null)));
                Items.Add(GetMessage(new Message(2, 7, chat.Id, null, false, false, false, false, false, false, false, TodayDate(14, 59), 0, null, 0, 0,  0, 0, string.Empty, 0, 0, new MessageText(new FormattedText("Well, look. Telegram is superfast and you can use it on all your devices at the same time - phones, tablets, even desktops.", new TextEntity[0]), null), null)));
                Items.Add(GetMessage(new Message(3, 0, chat.Id, null, true,  false, false, false, false, false, false, TodayDate(14, 59), 0, null, 0, 0,  0, 0, string.Empty, 0, 0, new MessageText(new FormattedText("😴", new TextEntity[0]), null), null)));
                Items.Add(GetMessage(new Message(4, 7, chat.Id, null, false, false, false, false, false, false, false, TodayDate(15, 00), 0, null, 0, 0,  0, 0, string.Empty, 0, 0, new MessageText(new FormattedText("And it has secret chats, like this one, with end-to-end encryption!", new TextEntity[0]), null), null)));
                Items.Add(GetMessage(new Message(5, 0, chat.Id, null, true,  false, false, false, false, false, false, TodayDate(15, 00), 0, null, 0, 0,  0, 0, string.Empty, 0, 0, new MessageText(new FormattedText("End encryption to what end??", new TextEntity[0]), null), null)));
                Items.Add(GetMessage(new Message(6, 7, chat.Id, null, false, false, false, false, false, false, false, TodayDate(15, 01), 0, null, 0, 0,  0, 0, string.Empty, 0, 0, new MessageText(new FormattedText("Arrgh. Forget it. You can set a timer and send photos that will disappear when the time rush out. Yay!", new TextEntity[0]), null), null)));
                Items.Add(GetMessage(new Message(7, 7, chat.Id, null, false, false, false, false, false, false, false, TodayDate(15, 01), 0, null, 0, 0,  0, 0, string.Empty, 0, 0, new MessageChatSetTtl(15), null)));
                Items.Add(GetMessage(new Message(8, 0, chat.Id, null, false, false, false, false, false, false, false, TodayDate(15, 05), 0, null, 0, 15, 0, 0, string.Empty, 0, 0, new MessagePhoto(new Photo(0, false, new[] { new PhotoSize("t", new File(0, 0, 0, new LocalFile(System.IO.Path.Combine(Package.Current.InstalledLocation.Path, "Assets\\Mockup\\hot.png"), true, true, false, true, 0, 0), new RemoteFile()), 580, 596) }), new FormattedText(string.Empty, new TextEntity[0]), true), null)));

                SetText("😱🙈👍");
            }
            else
            {
                //Items.Add(GetMessage(new Message(0, 11, chat.Id, null, false, false, false, false, false, false, false, TodayDate(15, 25), 0, null, 0, 0, 0, 0, string.Empty, 0, 0, new MessageText(new FormattedText("Yeah, that was my iPhone X", new TextEntity[0]), null), null)));
                Items.Add(GetMessage(new Message(4, 11, chat.Id, null, false, false, false, false, false, false, false, TodayDate(15, 25), 0, null, 0, 0, 0, 0, string.Empty, 0, 0, new MessageSticker(new Sticker(0, 512, 512, "", false, null, null, new File(0, 0, 0, new LocalFile(System.IO.Path.Combine(Package.Current.InstalledLocation.Path, "Assets\\Mockup\\sticker0.webp"), true, true, false, true, 0, 0), null))), null)));
                Items.Add(GetMessage(new Message(1, 9,  chat.Id, null, false, false, false, false, false, false, false, TodayDate(15, 26), 0, null, 0, 0, 0, 0, string.Empty, 0, 0, new MessageText(new FormattedText("Are you sure it's safe here?", new TextEntity[0]), null), null)));
                Items.Add(GetMessage(new Message(2, 7,  chat.Id, null, true,  false, false, false, false, false, false, TodayDate(15, 27), 0, null, 0, 0, 0, 0, string.Empty, 0, 0, new MessageText(new FormattedText("Yes, sure, don't worry.", new TextEntity[0]), null), null)));
                Items.Add(GetMessage(new Message(3, 13, chat.Id, null, false, false, false, false, false, false, false, TodayDate(15, 27), 0, null, 0, 0, 0, 0, string.Empty, 0, 0, new MessageText(new FormattedText("Hallo alle zusammen! Is the NSA reading this? 😀", new TextEntity[0]), null), null)));
                Items.Add(GetMessage(new Message(4, 9,  chat.Id, null, false, false, false, false, false, false, false, TodayDate(15, 29), 0, null, 0, 0, 0, 0, string.Empty, 0, 0, new MessageSticker(new Sticker(0, 512, 512, "", false, null, null, new File(0, 0, 0, new LocalFile(System.IO.Path.Combine(Package.Current.InstalledLocation.Path, "Assets\\Mockup\\sticker1.webp"), true, true, false, true, 0, 0), null))), null)));
                Items.Add(GetMessage(new Message(5, 10, chat.Id, null, false, false, false, false, false, false, false, TodayDate(15, 29), 0, null, 0, 0, 0, 0, string.Empty, 0, 0, new MessageText(new FormattedText("Sorry, I'll have to publish this conversation on the web.", new TextEntity[0]), null), null)));
                Items.Add(GetMessage(new Message(6, 10, chat.Id, null, false, false, false, false, false, false, false, TodayDate(15, 01), 0, null, 0, 0, 0, 0, string.Empty, 0, 0, new MessageChatDeleteMember(10), null)));
                Items.Add(GetMessage(new Message(7, 8,  chat.Id, null, false, false, false, false, false, false, false, TodayDate(15, 30), 0, null, 0, 0, 0, 0, string.Empty, 0, 0, new MessageText(new FormattedText("Wait, we could have made so much money on this!", new TextEntity[0]), null), null)));
            }
#endif

            if (chat.IsMarkedAsUnread)
            {
                ProtoService.Send(new ToggleChatIsMarkedAsUnread(chat.Id, false));
            }

            ProtoService.Send(new OpenChat(chat.Id));

            Delegate?.UpdateChat(chat);

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
                }
            }

            ShowReplyMarkup(chat);
            ShowDraftMessage(chat);
            ShowSwitchInline(state);

            ProtoService.Send(new GetChatReportSpamState(chat.Id), result =>
            {
                if (result is ChatReportSpamState spamState)
                {
                    BeginOnUIThread(() => IsReportSpam = spamState.CanReportSpam);
                }
            });



            RemoveNotifications();
        }

        public override Task OnNavigatingFromAsync(NavigatingEventArgs args)
        {
            Aggregator.Unsubscribe(this);

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
                    return Task.CompletedTask;
                }

                var panel = field.ItemsPanelRoot as ItemsStackPanel;
                if (panel != null && panel.LastVisibleIndex >= 0 && panel.LastVisibleIndex < Items.Count - 1 && Items.Count > 0)
                {
                    _scrollingIndex[chat.Id] = Items[panel.LastVisibleIndex].Id;

                    var container = field.ContainerFromIndex(panel.LastVisibleIndex) as ListViewItem;
                    if (container != null)
                    {
                        var transform = container.TransformToVisual(field);
                        var position = transform.TransformPoint(new Point());

                        _scrollingPixel[chat.Id] = field.ActualHeight - (position.Y + container.ActualHeight);
                    }
                }
                else
                {
                    _scrollingIndex.Remove(chat.Id);
                    _scrollingPixel.Remove(chat.Id);
                }
            }
            catch
            {
                _scrollingIndex.Remove(chat.Id);
                _scrollingPixel.Remove(chat.Id);
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
                    Delegate?.UpdateChatReplyMarkup(chat, GetMessage(message));
                }
                else
                {
                    Delegate?.UpdateChatReplyMarkup(chat, null);
                }
            }
        }

        public async void ShowPinnedMessage(Chat chat, SupergroupFullInfo fullInfo)
        {
            if (fullInfo == null || fullInfo.PinnedMessageId == 0)
            {
                Delegate?.UpdatePinnedMessage(chat, null, false);
            }
            else
            {
                var pinned = Settings.GetChatPinnedMessage(chat.Id);
                if (pinned == fullInfo.PinnedMessageId)
                {
                    Delegate?.UpdatePinnedMessage(chat, null, false);
                    return;
                }

                Delegate?.UpdatePinnedMessage(chat, null, true);

                var response = await ProtoService.SendAsync(new GetMessage(chat.Id, fullInfo.PinnedMessageId));
                if (response is Message message)
                {
                    Delegate?.UpdatePinnedMessage(chat, GetMessage(message), false);
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
                        EmbedData = new MessageEmbedData { ReplyToMessage = GetMessage(message) };
                    }
                }
            }
        }

        private IList<Sticker> _stickerPack;
        public IList<Sticker> StickerPack
        {
            get
            {
                return _stickerPack;
            }
            set
            {
                Set(ref _stickerPack, value);
            }
        }

        private ICollection _autocomplete;
        public ICollection Autocomplete
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

        private void RemoveNotifications()
        {
            return;

            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var group = string.Empty;
            if (chat.Type is ChatTypePrivate privata)
            {
                group = "u" + privata.UserId;
            }
            else if (chat.Type is ChatTypeSecret secret)
            {
                group = "s" + secret.SecretChatId;
            }
            else if (chat.Type is ChatTypeSupergroup supergroup)
            {
                group = "c" + supergroup.SupergroupId;
            }
            else if (chat.Type is ChatTypeBasicGroup basicGroup)
            {
                group = "c" + basicGroup.BasicGroupId;
            }

            Execute.BeginOnThreadPool(() =>
            {
                ToastNotificationManager.History.RemoveGroup(group, "App");
            });
        }

        public void SaveDraft()
        {
            if (_currentInlineBot != null)
            {
                return;
            }

            var embedded = _embedData;
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

            var messageText = GetText().Format();
            var reply = 0L;

            if (embedded != null && embedded.ReplyToMessage != null)
            {
                reply = embedded.ReplyToMessage.Id;
            }

            DraftMessage draft = null;
            if (!string.IsNullOrWhiteSpace(messageText))
            {
                draft = new DraftMessage(reply, new InputMessageText(GetFormattedText(messageText), false, false));
            }

            ProtoService.Send(new SetChatDraftMessage(_chat.Id, draft));
        }

        //public async void ProcessDraftReply(TLDraftMessage draft)
        //{
        //    var shouldFetch = false;

        //    var replyId = draft.ReplyToMsgId;
        //    if (replyId != null && replyId.Value != 0)
        //    {
        //        var channelId = new int?();
        //        //var channel = message.ToId as TLPeerChat;
        //        //if (channel != null)
        //        //{
        //        //    channelId = channel.Id;
        //        //}
        //        // TODO: verify
        //        if (Peer is TLInputPeerChannel)
        //        {
        //            channelId = Peer.ToPeer().Id;
        //        }

        //        var reply = CacheService.GetMessage(replyId.Value, channelId);
        //        if (reply != null)
        //        {
        //            Reply = reply;
        //        }
        //        else
        //        {
        //            shouldFetch = true;
        //        }
        //    }

        //    if (shouldFetch)
        //    {
        //        Task<MTProtoResponse<TLMessagesMessagesBase>> task = null;

        //        if (Peer is TLInputPeerChannel)
        //        {
        //            // TODO: verify
        //            //var first = replyToMsgs.FirstOrDefault();
        //            //if (first.ToId is TLPeerChat)
        //            //{
        //            //    task = ProtoService.GetMessagesAsync(new TLVector<int> { draft.ReplyToMsgId.Value });
        //            //}
        //            //else
        //            {
        //                var peer = Peer as TLInputPeerChannel;
        //                task = LegacyService.GetMessagesAsync(new TLInputChannel { ChannelId = peer.ChannelId, AccessHash = peer.AccessHash }, new TLVector<int> { draft.ReplyToMsgId.Value });
        //            }
        //        }
        //        else
        //        {
        //            task = LegacyService.GetMessagesAsync(new TLVector<int> { draft.ReplyToMsgId.Value });
        //        }

        //        var response = await task;
        //        if (response.IsSucceeded && response.Result is ITLMessages result)
        //        {
        //            //CacheService.AddChats(result.Result.Chats, (results) => { });
        //            //CacheService.AddUsers(result.Result.Users, (results) => { });
        //            CacheService.SyncUsersAndChats(result.Users, result.Chats, tuple => { });

        //            for (int j = 0; j < result.Messages.Count; j++)
        //            {
        //                if (draft.ReplyToMsgId.Value == result.Messages[j].Id)
        //                {
        //                    Reply = result.Messages[j];
        //                }
        //            }
        //        }
        //        else
        //        {
        //            Execute.ShowDebugMessage("messages.getMessages error " + response.Error);
        //        }
        //    }
        //}

        private bool _isSilent;
        public bool IsSilent
        {
            get
            {
                //return _isSilent && Chat != null && Chat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel;
                return false;
            }
            set
            {
                Set(ref _isSilent, value);
            }
        }

        public bool IsSilentVisible
        {
            get
            {
                //return Chat != null && Chat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel;
                return false;
            }
        }

        #region Reply 

        private MessageEmbedData _embedData;
        public MessageEmbedData EmbedData
        {
            get
            {
                return _embedData;
            }
            set
            {
                if (_embedData != value)
                {
                    _embedData = value;
                    RaisePropertyChanged();
                }
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
            var container = _embedData;
            if (container == null)
            {
                return;
            }

            if (container.WebPagePreview != null)
            {
                EmbedData = new MessageEmbedData { EditingMessage = container.EditingMessage, ReplyToMessage = container.ReplyToMessage, WebPagePreview = null };
                DisableWebPagePreview = true;
            }
            else
            {
                if (container.EditingMessage != null)
                {
                    SetText(null, false);
                    //Aggregator.Publish(new EditMessageEventArgs(container.PreviousMessage, container.PreviousMessage.Message));
                }

                EmbedData = null;
            }
        }

        private long GetReply(bool clean)
        {
            var embedded = _embedData;
            if (embedded == null || embedded.ReplyToMessage == null)
            {
                return 0;
            }

            if (clean)
            {
                EmbedData = null;
            }

            return embedded.ReplyToMessage.Id;
        }

        #endregion

        public RelayCommand PinnedCommand { get; }
        private async void PinnedExecute()
        {
            if (PinnedMessage != null)
            {
                await LoadMessageSliceAsync(null, PinnedMessage.Id);
            }
        }

        public RelayCommand<string> SendCommand { get; }
        private async void SendMessage(string args)
        {
            await SendMessageAsync(args);
        }

        public async Task SendMessageAsync(string text)
        {
            text = text.Format();

            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var disablePreview = DisableWebPagePreview;
            DisableWebPagePreview = false;

            var embedded = EmbedData;
            if (embedded != null && embedded.EditingMessage != null)
            {
                var editing = embedded.EditingMessage;

                Function function;
                if (editing.Content is MessageText)
                {
                    function = new EditMessageText(chat.Id, editing.Id, null, new InputMessageText(GetFormattedText(text), disablePreview, true));
                }
                else
                {
                    function = new EditMessageCaption(chat.Id, editing.Id, null, GetFormattedText(text));
                }

                var response = await ProtoService.SendAsync(function);
                if (response is Message message)
                {
                    EmbedData = null;
                    Aggregator.Publish(new UpdateMessageSendSucceeded(message, editing.Id));
                }
                else if (response is Error error)
                {
                    if (error.TypeEquals(ErrorType.MESSAGE_NOT_MODIFIED))
                    {
                        EmbedData = null;
                    }
                    else
                    {
                        // TODO: ...
                    }
                }
            }
            else
            {
                var reply = GetReply(true);

                if (text.Length > 0)
                {
                    int count = (int)Math.Ceiling(text.Length / 4096.0);
                    for (int a = 0; a < count; a++)
                    {
                        var message = text.Substr(a * 4096, Math.Min((a + 1) * 4096, text.Length));
                        var input = new InputMessageText(GetFormattedText(message), disablePreview, true);

                        var boh = await SendMessageAsync(reply, input);
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
        private void JoinChannelExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            ProtoService.Send(new JoinChat(chat.Id));
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

            ProtoService.Send(new SetChatNotificationSettings(chat.Id, new ChatNotificationSettings(false, unmute ? 0 : 632053052, false, chat.NotificationSettings.Sound, false, chat.NotificationSettings.ShowPreview)));
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

        public RelayCommand HideReportSpamCommand { get; }
        private void HideReportSpamExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            IsReportSpam = false;
            ProtoService.Send(new ChangeChatReportSpamState(chat.Id, false));
        }

        public RelayCommand ReportSpamCommand { get; }
        private async void ReportSpamExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (IsReportSpam)
            {
                var message = Strings.Resources.ReportSpamAlert;
                if (chat.Type is ChatTypeSupergroup supergroup)
                {
                    message = supergroup.IsChannel ? Strings.Resources.ReportSpamAlertChannel : Strings.Resources.ReportSpamAlertGroup;
                }
                else if (chat.Type is ChatTypeBasicGroup)
                {
                    message = Strings.Resources.ReportSpamAlertGroup;
                }

                var confirm = await TLMessageDialog.ShowAsync(message, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }

                IsReportSpam = false;
                ProtoService.Send(new ChangeChatReportSpamState(chat.Id, true));

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

                ProtoService.Send(new DeleteChatHistory(chat.Id, true));
            }
        }

        #endregion

        #region Stickers

        public RelayCommand OpenStickersCommand { get; }
        private void OpenStickersExecute()
        {
            _stickers.SyncStickers();
            _stickers.SyncGifs();
        }

        #endregion

        #region Delete and Exit

        public RelayCommand DialogDeleteCommand { get; }
        private async void DialogDeleteExecute()
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

                ProtoService.Send(new DeleteChatHistory(chat.Id, true));
            }
        }

        #endregion

        #region Clear history

        public RelayCommand DialogClearCommand { get; }
        private async void DialogClearExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.AreYouSureClearHistory, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                ProtoService.Send(new DeleteChatHistory(chat.Id, false));
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

            var response = await ProtoService.SendAsync(new CreateCall(user.Id, new CallProtocol(true, true, 65, 74)));
            if (response is Error error)
            {
                if (error.Code == 400 && error.Message.Equals("PARTICIPANT_VERSION_OUTDATED"))
                {
                    await TLMessageDialog.ShowAsync(string.Format(Strings.Resources.VoipPeerOutdated, user.GetFullName()), Strings.Resources.AppName, Strings.Resources.OK);
                }
                else if(error.Code == 400 && error.Message.Equals("USER_PRIVACY_RESTRICTED"))
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
            if (supergroupType == null)
            {
                return;
            }

            var supergroup = ProtoService.GetSupergroup(supergroupType.SupergroupId);
            if (supergroup == null)
            {
                return;
            }

            if (supergroup.Status is ChatMemberStatusCreator || (supergroup.Status is ChatMemberStatusAdministrator administrator && administrator.CanPinMessages))
            {
                var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.UnpinMessageAlert, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
                if (confirm == ContentDialogResult.Primary)
                {
                    PinnedMessage = null;
                    ProtoService.Send(new UnpinSupergroupMessage(supergroup.Id));
                }
            }
            else
            {
                var fullInfo = CacheService.GetSupergroupFull(supergroup.Id);
                if (fullInfo == null)
                {
                    return;
                }

                Settings.SetChatPinnedMessage(chat.Id, fullInfo.PinnedMessageId);
                Delegate?.UpdatePinnedMessage(chat, null, false);

                //var appData = ApplicationData.Current.LocalSettings.CreateContainer("Channels", ApplicationDataCreateDisposition.Always);
                //appData.Values["Pinned" + supergroup.Id] = _pinnedMessage.Id;
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
        private async void ShareContactExecute()
        {
            var response = await ProtoService.SendAsync(new GetMe());
            if (response is User user)
            {
                await SendContactAsync(new Contact(user.PhoneNumber, user.FirstName, user.LastName, string.Empty, user.Id));
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

            var user = ProtoService.GetUser(chat);
            if (user == null)
            {
                return;
            }

            var dialog = new EditUserNameView(user.FirstName, user.LastName);

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                ProtoService.Send(new ImportContacts(new[] { new Contact(user.PhoneNumber, dialog.FirstName, dialog.LastName, string.Empty, user.Id) }));
            }
        }

        #endregion

        #region Pin chat

        public RelayCommand PinChatCommand { get; }
        private async void PinChatExecute()
        {
            //var group = _pushService.GetGroup(this.With);
            //if (string.IsNullOrWhiteSpace(group))
            //{
            //    return;
            //}

            //var displayName = _pushService.GetTitle(this.With);
            //var arguments = _pushService.GetLaunch(this.With);
            //var picture = _pushService.GetPicture(this.With, group);

            //var secondaryTile = new SecondaryTile(group, displayName, arguments, new Uri(picture), TileSize.Default);
            //secondaryTile.VisualElements.Wide310x150Logo = new Uri(picture);
            //secondaryTile.VisualElements.Square310x310Logo = new Uri(picture);

            //var tileCreated = await secondaryTile.RequestCreateAsync();
            //if (tileCreated)
            //{
            //    UpdatePinChatCommands();
            //    ResetTile();
            //}
        }

        private void ResetTile()
        {
            //var group = _pushService.GetGroup(this.With);
            //if (string.IsNullOrWhiteSpace(group))
            //{
            //    return;
            //}

            //var displayName = _pushService.GetTitle(this.With);
            //var picture = _pushService.GetPicture(this.With, group);

            //var existsSecondaryTile = SecondaryTile.Exists(group);
            //if (existsSecondaryTile)
            //{
            //    NotificationTask.ResetSecondaryTile(displayName, picture, group);
            //}
        }

        private bool CanExecutePinChatCommand()
        {
            //var group = _pushService.GetGroup(this.With);
            //if (string.IsNullOrWhiteSpace(group))
            //{
            //    return false;
            //}

            //return !SecondaryTile.Exists(group);

            return false;
        }

        private void UpdatePinChatCommands()
        {
            CanPinChat = CanExecutePinChatCommand();
            CanUnpinChat = !CanPinChat;
        }

        #endregion

        #region Unpin chat

        public RelayCommand UnpinChatCommand { get; }
        private async void UnpinChatExecute()
        {
            //var group = _pushService.GetGroup(this.With);
            //if (string.IsNullOrWhiteSpace(group))
            //{
            //    return;
            //}

            //var secondaryTile = new SecondaryTile(group);
            //if (secondaryTile == null)
            //{
            //    return;
            //}

            //var tileDeleted = await secondaryTile.RequestDeleteAsync();
            //if (tileDeleted)
            //{
            //    UpdatePinChatCommands();
            //}
        }

        private bool CanUnpinChatExecute()
        {
            //var group = _pushService.GetGroup(this.With);
            //if (string.IsNullOrWhiteSpace(group))
            //{
            //    return false;
            //}

            //return SecondaryTile.Exists(group);
            return false;
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
            var command = chat.Type is ChatTypePrivate ? "/start" : "/start@" + bot.Username;

            if (bot == null)
            {
                return;
            }

            if (_accessToken == null)
            {
                await SendMessageAsync(command);
            }
            else
            {
                ProtoService.Send(new SendBotStartMessage(bot.Id, chat.Id, _accessToken));
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
            Search = new DialogSearchViewModel(ProtoService, CacheService, Settings, Aggregator, this);
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

            var opt1 = new RadioButton { Content = Strings.Resources.ReportChatSpam, HorizontalAlignment = HorizontalAlignment.Stretch };
            var opt2 = new RadioButton { Content = Strings.Resources.ReportChatViolence, HorizontalAlignment = HorizontalAlignment.Stretch };
            var opt3 = new RadioButton { Content = Strings.Resources.ReportChatPornography, HorizontalAlignment = HorizontalAlignment.Stretch };
            var opt4 = new RadioButton { Content = Strings.Resources.ReportChatOther, HorizontalAlignment = HorizontalAlignment.Stretch, IsChecked = true };
            var stack = new StackPanel();
            stack.Children.Add(opt1);
            stack.Children.Add(opt2);
            stack.Children.Add(opt3);
            stack.Children.Add(opt4);
            stack.Margin = new Thickness(12, 16, 12, 0);

            var dialog = new ContentDialog { Style = BootStrapper.Current.Resources["ModernContentDialogStyle"] as Style };
            dialog.Content = stack;
            dialog.Title = Strings.Resources.ReportChat;
            dialog.IsPrimaryButtonEnabled = true;
            dialog.IsSecondaryButtonEnabled = true;
            dialog.PrimaryButtonText = Strings.Resources.OK;
            dialog.SecondaryButtonText = Strings.Resources.Cancel;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var reason = opt1.IsChecked == true
                ? new ChatReportReasonSpam()
                : (opt2.IsChecked == true
                    ? new ChatReportReasonViolence()
                    : (opt3.IsChecked == true
                        ? new ChatReportReasonPornography()
                        : (ChatReportReason)new ChatReportReasonCustom()));

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
                        ToggleMuteExecute(chat.NotificationSettings.MuteFor != 0);
                    }
                }
                else
                {
                    if (group.Status is ChatMemberStatusLeft || (group.Status is ChatMemberStatusCreator creator && !creator.IsMember))
                    {
                        JoinChannelExecute();
                    }
                    else if (group.Status is ChatMemberStatusBanned)
                    {
                        DialogDeleteExecute();
                    }
                }
            }
        }

        #endregion

        private MessageViewModel GetMessage(Message message)
        {
            return new MessageViewModel(ProtoService, this, message);
        }
    }

    //public class TLMessageMediaGroup : TLMessageMediaBase
    //{
    //    public TLMessageMediaGroup()
    //    {
    //        Layout = new GroupedMessages();
    //    }

    //    public String Caption { get; set; }
    //    public GroupedMessages Layout { get; private set; }
    //}

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
        private readonly Dictionary<long, MessageViewModel> _messages = new Dictionary<long, MessageViewModel>();
        private readonly HashSet<DateTime> _dates = new HashSet<DateTime>();

        public Action<IEnumerable<MessageViewModel>> AttachChanged;

        public bool ContainsKey(long id)
        {
            return _messages.ContainsKey(id);
        }

        protected override void InsertItem(int index, MessageViewModel item)
        {
            _messages[item.Id] = item;

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
                var itemDate = Utils.UnixTimestampToDateTime(item.Date);
                var previousDate = Utils.UnixTimestampToDateTime(previous.Date);
                if (previousDate.Date != itemDate.Date)
                {
                    var timestamp = previousDate.ToTimestamp();
                    var service = new MessageViewModel(previous.ProtoService, previous.Delegate, new Message(0, previous.SenderUserId, previous.ChatId, null, previous.IsOutgoing, false, false, true, false, previous.IsChannelPost, false, previous.Date, 0, null, 0, 0, 0, 0, string.Empty, 0, 0, new MessageHeaderDate(), null));

                    //base.InsertItem(index + 1, service);
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
                var itemDate = Utils.UnixTimestampToDateTime(next.Date);
                var previousDate = Utils.UnixTimestampToDateTime(previous.Date);
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

            var oldFirst = item.IsFirst;

            var itemPost = item.IsChannelPost;

            if (!itemPost)
            {
                var attach = false;
                if (previous != null)
                {
                    var previousPost = previous.IsChannelPost;

                    attach = !previousPost &&
                             //!(previous is TLMessageService && !(((TLMessageService)previous).Action is TLMessageActionPhoneCall)) &&
                             !(previous.IsService()) &&
                             AreTogether(item, previous) &&
                             item.Date - previous.Date < 900;
                }

                item.IsFirst = !attach;

                if (previous != null)
                {
                    previous.IsLast = item.IsFirst || item.IsService();
                }
            }
            else
            {
                item.IsFirst = true;

                if (previous != null)
                {
                    previous.IsLast = false;
                }
            }
        }

        private static bool AreTogether(MessageViewModel message1, MessageViewModel message2)
        {
            var saved1 = message1.IsSaved();
            var saved2 = message2.IsSaved();
            if (saved1 && saved2)
            {
                if (message1.ForwardInfo is MessageForwardedFromUser fromUser1 && message2.ForwardInfo is MessageForwardedFromUser fromUser2)
                {
                    return fromUser1.SenderUserId == fromUser2.SenderUserId && fromUser1.ForwardedFromChatId == fromUser2.ForwardedFromChatId;
                }
                else if (message1.ForwardInfo is MessageForwardedPost post1 && message2.ForwardInfo is MessageForwardedPost post2)
                {
                    return post1.ChatId == post2.ChatId && post1.ForwardedFromChatId == post2.ForwardedFromChatId;
                }
            }
            else if (saved1 || saved2)
            {
                return false;
            }

            return message1.SenderUserId == message2.SenderUserId;
        }
    }

    public class MessageEmbedData
    {
        public MessageViewModel ReplyToMessage { get; set; }
        public MessageViewModel EditingMessage { get; set; }

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
