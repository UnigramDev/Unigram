using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Collections;
using Unigram.Common;
using Windows.UI.Xaml.Navigation;
using Template10.Common;
using Telegram.Api.Helpers;
using Unigram.Core.Services;
using Telegram.Api.Services.Updates;
using Telegram.Api.Services.Connection;
using System.Threading;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Collections;
using System.ComponentModel;
using Windows.UI.Xaml;
using Unigram.Converters;
using Windows.UI.Xaml.Media;
using System.Diagnostics;
using Telegram.Api.Services.FileManager;
using Windows.Storage.Pickers;
using System.IO;
using Windows.Storage;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Helpers;
using Unigram.Controls.Views;
using Unigram.Core.Models;
using Unigram.Controls;
using Unigram.Core.Helpers;
using Org.BouncyCastle.Security;
using Unigram.Core;
using Unigram.Services;
using Windows.Storage.FileProperties;
using Windows.System;
using Windows.UI.Xaml.Controls;
using Windows.UI.Popups;
using Telegram.Api.TL.Messages.Methods;
using Telegram.Api;
using Unigram.Views;
using Telegram.Api.TL.Phone.Methods;
using Windows.ApplicationModel.Calls;
using Unigram.Native.Tasks;
using Windows.Media.Effects;
using Windows.Media.Transcoding;
using Windows.Media.MediaProperties;
using Telegram.Api.Services.Cache.EventArgs;
using Windows.Foundation.Metadata;
using Windows.UI.Text;
using Telegram.Api.TL.Messages;
using Windows.UI.Notifications;
using Unigram.Native;
using Unigram.Views.Channels;
using Telegram.Api.TL.Channels;
using Windows.UI.StartScreen;
using Unigram.Models;
using Telegram.Api.Native;
using Newtonsoft.Json;
using Unigram.Core.Common;
using Unigram.ViewModels.Dialogs;
using Windows.UI.Xaml.Controls.Primitives;
using System.Collections.Concurrent;
using Windows.ApplicationModel.UserActivities;
using Windows.Foundation;
using Template10.Services.NavigationService;

namespace Unigram.ViewModels
{
    public partial class DialogViewModel : UnigramViewModelBase
    {
        public MessageCollection Items { get; private set; }

        private List<TLMessageCommonBase> _selectedItems = new List<TLMessageCommonBase>();
        public List<TLMessageCommonBase> SelectedItems
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
            }
        }

        public void ExpandSelection(IEnumerable<TLMessageCommonBase> vector)
        {
            var messages = vector.ToList();

            for (int i = 0; i < messages.Count; i++)
            {
                if (messages[i] is TLMessage message && message.Media is TLMessageMediaGroup groupMedia)
                {
                    messages.RemoveAt(i);

                    for (int j = 0; j < groupMedia.Layout.Messages.Count; j++)
                    {
                        messages.Insert(i, groupMedia.Layout.Messages[j]);
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
                return App.Current.Resources["MediaLibrary"] as MediaLibraryCollection;
            }
        }

        // Kludge
        public static Dictionary<long, IList<TLChannelParticipantBase>> Admins { get; } = new Dictionary<long, IList<TLChannelParticipantBase>>();

        private readonly ConcurrentDictionary<long, TLMessage> _groupedMessages = new ConcurrentDictionary<long, TLMessage>();

        private static readonly Dictionary<TLPeerBase, int> _scrollingIndex = new Dictionary<TLPeerBase, int>();
        private static readonly Dictionary<TLPeerBase, double> _scrollingPixel = new Dictionary<TLPeerBase, double>();

        private readonly DisposableMutex _loadMoreLock = new DisposableMutex();
        private readonly DisposableMutex _insertLock = new DisposableMutex();

        private readonly DialogStickersViewModel _stickers;
        private readonly IStickersService _stickersService;
        private readonly ILocationService _locationService;
        private readonly ILiveLocationService _liveLocationService;
        private readonly IUploadFileManager _uploadFileManager;
        private readonly IUploadAudioManager _uploadAudioManager;
        private readonly IUploadDocumentManager _uploadDocumentManager;
        private readonly IUploadVideoManager _uploadVideoManager;
        private readonly IPushService _pushService;

        //private UserActivitySession _timelineSession;

        public DialogViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, IUploadFileManager uploadFileManager, IUploadAudioManager uploadAudioManager, IUploadDocumentManager uploadDocumentManager, IUploadVideoManager uploadVideoManager, IStickersService stickersService, ILocationService locationService, ILiveLocationService liveLocationService, IPushService pushService)
            : base(protoService, cacheService, aggregator)
        {
            _uploadFileManager = uploadFileManager;
            _uploadAudioManager = uploadAudioManager;
            _uploadDocumentManager = uploadDocumentManager;
            _uploadVideoManager = uploadVideoManager;
            _stickersService = stickersService;
            _locationService = locationService;
            _liveLocationService = liveLocationService;
            _pushService = pushService;

            _stickers = new DialogStickersViewModel(protoService, cacheService, aggregator, stickersService);

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
            ToggleMuteCommand = new RelayCommand(ToggleMuteExecute);
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
            SwitchCommand = new RelayCommand<TLInlineBotSwitchPM>(SwitchExecute);

            MessagesForwardCommand = new RelayCommand(MessagesForwardExecute, MessagesForwardCanExecute);
            MessagesDeleteCommand = new RelayCommand(MessagesDeleteExecute, MessagesDeleteCanExecute);
            MessagesCopyCommand = new RelayCommand(MessagesCopyExecute, MessagesCopyCanExecute);

            MessageReplyCommand = new RelayCommand<TLMessageBase>(MessageReplyExecute);
            MessageDeleteCommand = new RelayCommand<TLMessageBase>(MessageDeleteExecute);
            MessageForwardCommand = new RelayCommand<TLMessageBase>(MessageForwardExecute);
            MessageShareCommand = new RelayCommand<TLMessage>(MessageShareExecute);
            MessageSelectCommand = new RelayCommand<TLMessageBase>(MessageSelectExecute);
            MessageCopyCommand = new RelayCommand<TLMessage>(MessageCopyExecute);
            MessageCopyMediaCommand = new RelayCommand<TLMessage>(MessageCopyMediaExecute);
            MessageCopyLinkCommand = new RelayCommand<TLMessageCommonBase>(MessageCopyLinkExecute);
            MessageEditLastCommand = new RelayCommand(MessageEditLastExecute);
            MessageEditCommand = new RelayCommand<TLMessage>(MessageEditExecute);
            MessagePinCommand = new RelayCommand<TLMessageBase>(MessagePinExecute);
            //KeyboardButtonCommand = new RelayCommand<TLKeyboardButtonBase>(KeyboardButtonExecute);
            MessageOpenReplyCommand = new RelayCommand<TLMessageCommonBase>(MessageOpenReplyExecute);
            MessageStickerPackInfoCommand = new RelayCommand<TLMessage>(MessageStickerPackInfoExecute);
            MessageFaveStickerCommand = new RelayCommand<TLMessage>(MessageFaveStickerExecute);
            MessageUnfaveStickerCommand = new RelayCommand<TLMessage>(MessageUnfaveStickerExecute);
            MessageSaveStickerCommand = new RelayCommand<TLMessage>(MessageSaveStickerExecute);
            MessageSaveMediaCommand = new RelayCommand<TLMessage>(MessageSaveMediaExecute);
            MessageSaveDownloadCommand = new RelayCommand<TLMessage>(MessageSaveDownloadExecute);
            MessageSaveGIFCommand = new RelayCommand<TLMessage>(MessageSaveGIFExecute);
            MessageAddContactCommand = new RelayCommand<TLMessage>(MessageAddContactExecute);
            MessageServiceCommand = new RelayCommand<TLMessageService>(MessageServiceExecute);

            SendStickerCommand = new RelayCommand<TLDocument>(SendStickerExecute);
            SendGifCommand = new RelayCommand<TLDocument>(SendGifExecute);
            SendFileCommand = new RelayCommand(SendFileExecute);
            SendMediaCommand = new RelayCommand(SendMediaExecute);
            SendContactCommand = new RelayCommand(SendContactExecute);
            SendLocationCommand = new RelayCommand(SendLocationExecute);

            Items = new MessageCollection();
            Items.CollectionChanged += (s, args) => IsEmpty = Items.Count == 0;

            Aggregator.Subscribe(this);
        }

        ~DialogViewModel()
        {
            Debug.WriteLine("Finalizing DialogViewModel");
            Aggregator.Unsubscribe(this);

            GC.Collect();

            //if (Messages != null)
            //{
            //    Messages.Clear();
            //    Messages = null;
            //}
            //if (BotCommands != null)
            //{
            //    BotCommands.Clear();
            //    BotCommands = null;
            //}
            //if (UnfilteredBotCommands != null)
            //{
            //    UnfilteredBotCommands.Clear();
            //    UnfilteredBotCommands = null;
            //}
            //if (UsernameHints != null)
            //{
            //    UsernameHints.Clear();
            //    UsernameHints = null;
            //}
            //if (StickerPack != null)
            //{
            //    StickerPack.Clear();
            //    StickerPack = null;
            //}
            //if (SelectedMessages != null)
            //{
            //    SelectedMessages.Clear();
            //    SelectedMessages = null;
            //}
        }

        public bool IsActive
        {
            get
            {
                return NavigationService?.IsPeerActive(Peer) ?? false;
            }
        }

        public DialogStickersViewModel Stickers => _stickers;

        private TLDialog _dialog;
        public TLDialog Dialog
        {
            get
            {
                return _dialog;
            }
            set
            {
                Set(ref _dialog, value);
            }
        }

        private ITLDialogWith _with;
        public ITLDialogWith With
        {
            get
            {
                return _with;
            }
            set
            {
                Set(ref _with, value);
                RaisePropertyChanged(() => IsSilentVisible);

                if (value is TLUser)
                    RaisePropertyChanged(() => WithUser);
                else if (value is TLChat)
                    RaisePropertyChanged(() => WithChat);
                else if (value is TLChannel)
                    RaisePropertyChanged(() => WithChannel);
            }
        }

        public TLUser WithUser => _with as TLUser;
        public TLChat WithChat => _with as TLChat;
        public TLChannel WithChannel => _with as TLChannel;

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
                RaisePropertyChanged(() => With);
                RaisePropertyChanged(() => IsSilentVisible);

                if (value is TLUserFull)
                    RaisePropertyChanged(() => FullUser);
                else if (value is TLChatFull)
                    RaisePropertyChanged(() => FullChat);
                else if (value is TLChannelFull)
                    RaisePropertyChanged(() => FullChannel);
            }
        }

        public TLUserFull FullUser => _full as TLUserFull;
        public TLChatFull FullChat => _full as TLChatFull;
        public TLChannelFull FullChannel => _full as TLChannelFull;

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

        private TLInputPeerBase _peer;
        public TLInputPeerBase Peer
        {
            get
            {
                return _peer;
            }
            set
            {
                Set(ref _peer, value);
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

        private TLMessageBase _pinnedMessage;
        public TLMessageBase PinnedMessage
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

        private TLMessageBase _informativeMessage;
        public TLMessageBase InformativeMessage
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

        private bool _isPhoneCallsAvailable;
        public bool IsPhoneCallsAvailable
        {
            get
            {
                return _isPhoneCallsAvailable;
            }
            set
            {
                Set(ref _isPhoneCallsAvailable, value);
            }
        }

        private bool _isShareContactAvailable;
        public bool IsShareContactAvailable
        {
            get
            {
                return _isShareContactAvailable;
            }
            set
            {
                Set(ref _isShareContactAvailable, value);
            }
        }

        private bool _isAddContactAvailable;
        public bool IsAddContactAvailable
        {
            get
            {
                return _isAddContactAvailable;
            }
            set
            {
                Set(ref _isAddContactAvailable, value);
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
            if (TextField == null)
            {
                return;
            }

            TextField.Document.GetText(TextGetOptions.None, out string text);
            TextField.Document.Selection.SetRange(start, text.Length);
        }

        public void SetText(string text, TLVector<TLMessageEntityBase> entities = null, bool focus = false)
        {
            if (TextField == null)
            {
                return;
            }

            if (With is TLChannel channel)
            {
                if (channel.IsBroadcast && !(channel.IsCreator || channel.HasAdminRights))
                {
                    return;
                }
            }

            if (string.IsNullOrEmpty(text))
            {
                TextField.Document.SetText(TextSetOptions.FormatRtf, @"{\rtf1\fbidis\ansi\ansicpg1252\deff0\nouicompat\deflang1040{\fonttbl{\f0\fnil Segoe UI;}}{\*\generator Riched20 10.0.14393}\viewkind4\uc1\pard\ltrpar\tx720\cf1\f0\fs23\lang1033}");
            }
            else
            {
                TextField.SetText(text, entities);
            }

            if (focus)
            {
                TextField.Focus(FocusState.Keyboard);
            }
        }

        public string GetText()
        {
            if (TextField == null)
            {
                return null;
            }

            TextField.Document.GetText(TextGetOptions.NoHidden, out string text);
            return text;
        }

        public bool IsFirstSliceLoaded { get; set; }

        private bool _isLastSliceLoaded;
        public bool IsLastSliceLoaded
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

        private Stack<int> _goBackStack = new Stack<int>();

        public async Task LoadNextSliceAsync(bool force = false)
        {
            using (await _loadMoreLock.WaitAsync())
            {
                if (_isLoadingNextSlice || _isLoadingPreviousSlice || _peer == null)
                {
                    return;
                }

                _isLoadingNextSlice = true;
                IsLoading = true;

                Debug.WriteLine("DialogViewModel: LoadNextSliceAsync");

                var first = Items.FirstOrDefault(x => x.Id != 0);
                if (first is TLMessage firstMessage && firstMessage.Media is TLMessageMediaGroup groupMedia)
                {
                    first = groupMedia.Layout.Messages.FirstOrDefault();
                }

                var maxId = first?.Id ?? int.MaxValue;
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

                var response = await ProtoService.GetHistoryAsync(Peer, Peer.ToPeer(), true, 0, 0, maxId, limit, 0);
                if (response.IsSucceeded && response.Result is ITLMessages result)
                {
                    if (result.Messages.Count > 0)
                    {
                        ListField.SetScrollMode(ItemsUpdatingScrollMode.KeepLastItemInView, force);
                    }

                    ProcessReplies(result.Messages);
                    MessageCollection.ProcessReplies(result.Messages);

                    Debug.WriteLine("DialogViewModel: LoadNextSliceAsync: Replies processed");

                    //foreach (var item in result.Result.Messages.OrderByDescending(x => x.Date))
                    for (int i = 0; i < result.Messages.Count; i++)
                    {
                        var item = result.Messages[i];
                        if (item is TLMessageService serviceMessage && serviceMessage.Action is TLMessageActionHistoryClear)
                        {
                            continue;
                        }

                        Items.Insert(0, item);
                        //InsertMessage(item as TLMessageCommonBase);
                    }

                    Debug.WriteLine("DialogViewModel: LoadNextSliceAsync: Items added");

                    foreach (var item in result.Messages.OrderByDescending(x => x.Date))
                    {
                        var message = item as TLMessage;
                        if (message != null && !message.IsOut && message.HasFromId && message.HasReplyMarkup && message.ReplyMarkup != null)
                        {
                            var user = CacheService.GetUser(message.FromId) as TLUser;
                            if (user != null && user.IsBot)
                            {
                                SetReplyMarkup(message);
                            }
                        }
                    }

                    if (result.Messages.Count < limit)
                    {
                        IsLastSliceLoaded = true;
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
                if (_isLoadingNextSlice || _isLoadingPreviousSlice || _peer == null)
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

                var last = Items.LastOrDefault();
                if (last is TLMessage lastMessage && lastMessage.Media is TLMessageMediaGroup groupMedia)
                {
                    last = groupMedia.Layout.Messages.LastOrDefault();
                }

                var maxId = last?.Id ?? 1;
                var limit = 50;

                //for (int i = 0; i < Messages.Count; i++)
                //{
                //    if (Messages[i].Id != 0 && Messages[i].Id < maxId)
                //    {
                //        maxId = Messages[i].Id;
                //    }
                //}

                var response = await ProtoService.GetHistoryAsync(Peer, Peer.ToPeer(), true, -limit - 1, 0, maxId, limit, 0);
                if (response.IsSucceeded && response.Result is ITLMessages result)
                {
                    if (result.Messages.Count > 0)
                    {
                        //ListField.SetScrollMode(response.Result.Messages.Count < limit ? ItemsUpdatingScrollMode.KeepLastItemInView : ItemsUpdatingScrollMode.KeepItemsInView, force);
                        ListField.SetScrollMode(ItemsUpdatingScrollMode.KeepItemsInView, true);
                    }

                    ProcessReplies(result.Messages);
                    MessageCollection.ProcessReplies(result.Messages);

                    //foreach (var item in result.Result.Messages.OrderBy(x => x.Date))
                    for (int i = result.Messages.Count - 1; i >= 0; i--)
                    {
                        var item = result.Messages[i];
                        if (item is TLMessageService serviceMessage && serviceMessage.Action is TLMessageActionHistoryClear)
                        {
                            continue;
                        }

                        if (item.Id > maxId)
                        {
                            Items.Add(item);
                        }
                        //InsertMessage(item as TLMessageCommonBase);
                    }

                    foreach (var item in result.Messages.OrderByDescending(x => x.Date))
                    {
                        var message = item as TLMessage;
                        if (message != null && !message.IsOut && message.HasFromId && message.HasReplyMarkup && message.ReplyMarkup != null)
                        {
                            var user = CacheService.GetUser(message.FromId) as TLUser;
                            if (user != null && user.IsBot)
                            {
                                SetReplyMarkup(message);
                            }
                        }
                    }

                    if (result.Messages.Count < limit)
                    {
                        IsFirstSliceLoaded = true;
                    }
                }

                _isLoadingPreviousSlice = false;
                IsLoading = false;
            }
        }

        public RelayCommand NextMentionCommand { get; }
        private async void NextMentionExecute()
        {
            var dialog = _dialog;
            if (dialog == null)
            {
                return;
            }

            var response = await ProtoService.GetUnreadMentionsAsync(_peer, 0, dialog.UnreadMentionsCount - 1, 1, 0, 0);
            if (response.IsSucceeded && response.Result is ITLMessages result)
            {
                var count = 0;
                if (response.Result is TLMessagesChannelMessages channelMessages)
                {
                    count = channelMessages.Count;
                }

                dialog.UnreadMentionsCount = count;
                dialog.RaisePropertyChanged(() => dialog.UnreadMentionsCount);

                //if (response.Result.Messages.IsEmpty())
                //{
                //    dialog.UnreadMentionsCount = 0;
                //    dialog.RaisePropertyChanged(() => dialog.UnreadMentionsCount);
                //    return;
                //}

                var commonMessage = result.Messages.FirstOrDefault() as TLMessageCommonBase;
                if (commonMessage == null)
                {
                    return;
                }

                commonMessage.IsMediaUnread = false;
                commonMessage.RaisePropertyChanged(() => commonMessage.IsMediaUnread);

                // DO NOT AWAIT
                LoadMessageSliceAsync(null, commonMessage.Id);

                if (With is TLChannel channel)
                {
                    await ProtoService.ReadMessageContentsAsync(channel.ToInputChannel(), new TLVector<int> { commonMessage.Id });
                }
                else
                {
                    await ProtoService.ReadMessageContentsAsync(new TLVector<int> { commonMessage.Id });
                }
            }
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
                Items.Clear();

                var maxId = _dialog?.UnreadCount > 0 ? _dialog.ReadInboxMaxId : int.MaxValue;
                var offset = _dialog?.UnreadCount > 0 && maxId > 0 ? -16 : 0;
                await LoadFirstSliceAsync(maxId, offset);
            }
        }

        public async Task LoadMessageSliceAsync(int? previousId, int maxId, bool highlight = true, double? pixel = null, bool second = false)
        {
            var already = Items.FirstOrDefault(x => x.Id == maxId);
            if (already != null)
            {
                //ListField.ScrollIntoView(already);
                await ListField.ScrollToItem(already, highlight ? SnapPointsAlignment.Center : SnapPointsAlignment.Far, highlight, pixel);
                return;
            }
            else if (second)
            {
                var max = _dialog?.UnreadCount > 0 ? _dialog.ReadInboxMaxId : int.MaxValue;
                var offset = _dialog?.UnreadCount > 0 && max > 0 ? -16 : 0;
                await LoadFirstSliceAsync(max, offset);
                return;
            }

            using (await _loadMoreLock.WaitAsync())
            {
                if (_isLoadingNextSlice || _isLoadingPreviousSlice || _peer == null)
                {
                    return;
                }

                _isLoadingNextSlice = true;
                _isLoadingPreviousSlice = true;
                IsLoading = true;
                IsLastSliceLoaded = false;
                IsFirstSliceLoaded = false;

                Debug.WriteLine("DialogViewModel: LoadMessageSliceAsync");

                if (previousId.HasValue)
                {
                    _goBackStack.Push(previousId.Value);
                }

                var offset = -25;
                var limit = 50;

                var response = await ProtoService.GetHistoryAsync(Peer, Peer.ToPeer(), true, offset, 0, maxId, limit, 0);
                if (response.IsSucceeded && response.Result is ITLMessages result)
                {
                    if (result.Messages.Count > 0)
                    {
                        ListField.SetScrollMode(ItemsUpdatingScrollMode.KeepItemsInView, true);
                    }

                    Items.Clear();

                    ProcessReplies(result.Messages);
                    MessageCollection.ProcessReplies(result.Messages);

                    //foreach (var item in result.Result.Messages.OrderByDescending(x => x.Date))
                    for (int i = result.Messages.Count - 1; i >= 0; i--)
                    {
                        var item = result.Messages[i];
                        if (item is TLMessageService serviceMessage && serviceMessage.Action is TLMessageActionHistoryClear)
                        {
                            continue;
                        }

                        Items.Add(item);
                    }

                    foreach (var item in result.Messages.OrderByDescending(x => x.Date))
                    {
                        var message = item as TLMessage;
                        if (message != null && !message.IsOut && message.HasFromId && message.HasReplyMarkup && message.ReplyMarkup != null)
                        {
                            var user = CacheService.GetUser(message.FromId) as TLUser;
                            if (user != null && user.IsBot)
                            {
                                SetReplyMarkup(message);
                            }
                        }
                    }

                    //if (response.Result.Messages.Count < limit)
                    //{
                    //    IsFirstSliceLoaded = true;
                    //}

                    IsLastSliceLoaded = false;
                    IsFirstSliceLoaded = false;
                }

                _isLoadingNextSlice = false;
                _isLoadingPreviousSlice = false;
                IsLoading = false;
            }

            //await Task.Delay(200);
            //await LoadNextSliceAsync(true);
            await LoadMessageSliceAsync(null, maxId, highlight, pixel, true);
        }

        public async Task LoadDateSliceAsync(int dateOffset)
        {
            var offset = -1;
            var limit = 1;

            var obj = new TLMessagesGetHistory { Peer = Peer, OffsetId = 0, OffsetDate = dateOffset - 1, AddOffset = offset, Limit = limit, MaxId = 0, MinId = 0 };
            ProtoService.SendRequestAsync<TLMessagesMessagesBase>("messages.getHistory", obj, result =>
            {
                if (result is ITLMessages messages && messages.Messages.Count > 0)
                {
                    BeginOnUIThread(async () =>
                    {
                        await LoadMessageSliceAsync(null, messages.Messages[0].Id);
                    });
                }
            });

            //var result = await ProtoService.GetHistoryAsync(Peer, Peer.ToPeer(), true, offset, dateOffset, 0, limit);
            //if (result.IsSucceeded)
            //{
            //    await LoadMessageSliceAsync(null, result.Result.Messages[0].Id);
            //}
        }


        public async Task LoadFirstSliceAsync(int maxId, int offset)
        {
            using (await _loadMoreLock.WaitAsync())
            {
                if (_isLoadingNextSlice || _isLoadingPreviousSlice || _peer == null)
                {
                    return;
                }

                _isLoadingNextSlice = true;
                _isLoadingPreviousSlice = true;
                IsLoading = true;

                Debug.WriteLine("DialogViewModel: LoadFirstSliceAsync");

                var lastRead = true;

                //var maxId = _currentDialog?.UnreadCount > 0 ? _currentDialog.ReadInboxMaxId : int.MaxValue;
                var readMaxId = With as ITLReadMaxId;

                //var maxId = readMaxId.ReadInboxMaxId > 0 ? readMaxId.ReadInboxMaxId : int.MaxValue;
                //var offset = _currentDialog?.UnreadCount > 0 && maxId > 0 ? -51 : 0;

                //var maxId = _currentDialog?.UnreadCount > 0 ? _currentDialog.ReadInboxMaxId : int.MaxValue;
                //var offset = _currentDialog?.UnreadCount > 0 && maxId > 0 ? -16 : 0;
                var limit = 15;

                Retry:
                var response = await ProtoService.GetHistoryAsync(Peer, Peer.ToPeer(), true, offset, 0, maxId, limit, 0);
                if (response.IsSucceeded && response.Result is ITLMessages result)
                {
                    if (result.Messages.Count == 0 && offset < 0)
                    {
                        maxId = int.MaxValue;
                        offset = 0;
                        goto Retry;
                    }

                    if (result.Messages.Count > 0)
                    {
                        ListField.SetScrollMode(maxId == int.MaxValue ? ItemsUpdatingScrollMode.KeepLastItemInView : ItemsUpdatingScrollMode.KeepItemsInView, true);
                    }

                    Items.Clear();

                    ProcessReplies(result.Messages);
                    MessageCollection.ProcessReplies(result.Messages);

                    //foreach (var item in result.Result.Messages.OrderBy(x => x.Date))
                    for (int i = result.Messages.Count - 1; i >= 0; i--)
                    {
                        var item = result.Messages[i];
                        if (item is TLMessageService serviceMessage && serviceMessage.Action is TLMessageActionHistoryClear)
                        {
                            continue;
                        }

                        var message = item as TLMessageCommonBase;

                        if (item.Id > maxId && lastRead && message != null && !message.IsOut)
                        {
                            var unreadMessage = new TLMessageService
                            {
                                FromId = SettingsHelper.UserId,
                                ToId = Peer.ToPeer(),
                                State = TLMessageState.Sending,
                                IsOut = true,
                                IsUnread = true,
                                Date = item.Date,
                                Action = new TLMessageActionUnreadMessages(),
                                RandomId = TLLong.Random()
                            };

                            Items.Add(unreadMessage);
                            lastRead = false;
                        }

                        Items.Add(item);
                    }

                    foreach (var item in result.Messages.OrderByDescending(x => x.Date))
                    {
                        var message = item as TLMessage;
                        if (message != null && !message.IsOut && message.HasFromId && message.HasReplyMarkup && message.ReplyMarkup != null)
                        {
                            var user = CacheService.GetUser(message.FromId) as TLUser;
                            if (user != null && user.IsBot)
                            {
                                SetReplyMarkup(message);
                            }
                        }
                    }

                    if (result.Messages.Count < limit)
                    {
                        IsFirstSliceLoaded = maxId < int.MaxValue;
                        IsLastSliceLoaded = maxId == int.MaxValue;
                    }
                    else
                    {
                        IsFirstSliceLoaded = false;
                        IsLastSliceLoaded = false;
                    }
                }

                _isLoadingNextSlice = false;
                _isLoadingPreviousSlice = false;
                IsLoading = false;
            }

            //if (maxId < int.MaxValue)
            //{
            //    await LoadMessageSliceAsync(null, maxId);
            //}
        }

        public void ScrollToBottom(object item)
        {
            //if (IsFirstSliceLoaded)
            {
                if (item == null)
                {
                    ListField.ScrollToBottom();
                }
                else
                {
                    ListField.ScrollIntoView(item, ScrollIntoViewAlignment.Leading);
                }

                ListField.SetScrollMode(ItemsUpdatingScrollMode.KeepLastItemInView, true);
            }
        }

        public async void ProcessReplies(IList<TLMessageBase> messages)
        {
            var replyIds = new TLVector<int>();
            var replyToMsgs = new List<TLMessageCommonBase>();

            var groups = new Dictionary<long, Tuple<TLMessage, GroupedMessages>>();

            for (int i = 0; i < messages.Count; i++)
            {
                var commonMessage = messages[i] as TLMessageCommonBase;
                if (commonMessage != null)
                {
                    if (commonMessage is TLMessage message && message.HasGroupedId && message.GroupedId is long groupedId)
                    {
                        _groupedMessages.TryGetValue(groupedId, out TLMessage group);

                        var next = false;

                        if (group == null)
                        {
                            var media = new TLMessageMediaGroup();

                            group = new TLMessage();
                            group.Media = media;
                            group.Date = message.Date;

                            messages[i] = group;
                            commonMessage = group;

                            groups[groupedId] = Tuple.Create(group, media.Layout);
                            _groupedMessages[groupedId] = group;
                        }
                        else
                        {
                            next = true;
                            messages.RemoveAt(i);
                            i--;
                        }

                        if (group.Media is TLMessageMediaGroup groupMedia)
                        {
                            //var array = group.Messages.ToArray();
                            //var insert = Array.BinarySearch(array, message, _comparer);
                            //if (insert < 0) insert = ~insert;

                            groupMedia.Layout.GroupedId = groupedId;
                            groupMedia.Layout.Messages.Add(message);
                            groupMedia.Layout.Calculate();

                            var first = groupMedia.Layout.Messages.FirstOrDefault();
                            if (first != null)
                            {
                                group.Id = first.Id;
                                group.Flags = first.Flags;
                                group.HasEditDate = false;
                                group.FromId = first.FromId;
                                group.ToId = first.ToId;
                                group.FwdFrom = first.FwdFrom;
                                group.ViaBotId = first.ViaBotId;
                                group.ReplyToMsgId = first.ReplyToMsgId;
                                group.Date = first.Date;
                                group.Message = first.Message;
                                group.ReplyMarkup = first.ReplyMarkup;
                                group.Entities = first.Entities;
                                group.Views = first.Views;
                                group.PostAuthor = first.PostAuthor;
                                group.GroupedId = first.GroupedId;
                            }

                            if (first.Media is ITLMessageMediaCaption caption)
                            {
                                group.HasEditDate = first.HasEditDate;
                                group.EditDate = first.EditDate;

                                groupMedia.Caption = caption.Caption;

                                group.RaisePropertyChanged(() => group.EditDate);
                                group.RaisePropertyChanged(() => group.Self);
                            }
                        }

                        if (next)
                        {
                            continue;
                        }
                    }

                    var replyId = commonMessage.ReplyToMsgId;
                    if (replyId != null && replyId.Value != 0)
                    {
                        var channelId = new int?();
                        var channel = commonMessage.ToId as TLPeerChannel;
                        if (channel != null)
                        {
                            channelId = channel.Id;
                        }

                        var reply = CacheService.GetMessage(replyId.Value, channelId);
                        if (reply != null)
                        {
                            messages[i].Reply = reply;
                            messages[i].RaisePropertyChanged(() => messages[i].Reply);
                            messages[i].RaisePropertyChanged(() => messages[i].ReplyInfo);

                            if (messages[i] is TLMessageService)
                            {
                                messages[i].RaisePropertyChanged(() => ((TLMessageService)messages[i]).Self);
                            }
                        }
                        else
                        {
                            replyIds.Add(replyId.Value);
                            replyToMsgs.Add(commonMessage);
                        }
                    }

                    //if (message.NotListened && message.Media != null)
                    //{
                    //    message.Media.NotListened = true;
                    //}
                }
            }

            foreach (var group in groups.Values)
            {
                group.Item2.Calculate();
                group.Item1.RaisePropertyChanged(() => group.Item1.Media);
            }

            if (replyIds.Count > 0)
            {
                Task<MTProtoResponse<TLMessagesMessagesBase>> task = null;

                if (Peer is TLInputPeerChannel)
                {
                    var first = replyToMsgs.FirstOrDefault();
                    if (first.ToId is TLPeerChat)
                    {
                        task = ProtoService.GetMessagesAsync(replyIds);
                    }
                    else
                    {
                        var peer = Peer as TLInputPeerChannel;
                        task = ProtoService.GetMessagesAsync(new TLInputChannel { ChannelId = peer.ChannelId, AccessHash = peer.AccessHash }, replyIds);
                    }
                }
                else
                {
                    task = ProtoService.GetMessagesAsync(replyIds);
                }

                var response = await task;
                if (response.IsSucceeded && response.Result is ITLMessages result)
                {
                    //CacheService.AddChats(result.Result.Chats, (results) => { });
                    //CacheService.AddUsers(result.Result.Users, (results) => { });
                    CacheService.SyncUsersAndChats(result.Users, result.Chats, tuple => { });

                    for (int j = 0; j < result.Messages.Count; j++)
                    {
                        for (int k = 0; k < replyToMsgs.Count; k++)
                        {
                            var message = replyToMsgs[k];
                            if (message != null && message.ReplyToMsgId.Value == result.Messages[j].Id)
                            {
                                replyToMsgs[k].Reply = result.Messages[j];
                                replyToMsgs[k].RaisePropertyChanged(() => replyToMsgs[k].Reply);
                                replyToMsgs[k].RaisePropertyChanged(() => replyToMsgs[k].ReplyInfo);

                                if (replyToMsgs[k] is TLMessageService)
                                {
                                    replyToMsgs[k].RaisePropertyChanged(() => ((TLMessageService)replyToMsgs[k]).Self);
                                }
                            }
                        }
                    }
                }
                else
                {
                    Execute.ShowDebugMessage("messages.getMessages error " + response.Error);
                }
            }
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            //if (mode != NavigationMode.New)
            //{
            //    return;
            //}

            //Messages.Clear();
            //With = null;
            //Full = null;

            var messageId = new int?();
            if (App.InMemoryState.NavigateToMessage.HasValue)
            {
                messageId = App.InMemoryState.NavigateToMessage;
                App.InMemoryState.NavigateToMessage = null;
            }

            AccessToken = App.InMemoryState.NavigateToAccessToken;
            App.InMemoryState.NavigateToAccessToken = null;

            var participant = GetParticipant(parameter as TLPeerBase);
            if (participant == null)
            {
                return;
            }

            Peer = participant.ToInputPeer();
            With = participant;
            Dialog = CacheService.GetDialog(Peer.ToPeer());

            var highlight = true;
            var pixel = new double?();

            if (_scrollingIndex.TryGetValue(participant.ToPeer(), out int visible))
            {
                messageId = visible;
                highlight = false;
            }

            if (_scrollingPixel.TryGetValue(participant.ToPeer(), out double kpixel))
            {
                pixel = kpixel;
            }

            //Aggregator.Subscribe(this);

            //var storage = ApplicationSettings.Current.GetValueOrDefault(TLSerializationService.Current.Serialize(parameter), -1);
            //if (storage != -1 && messageId == null && _currentDialog?.UnreadCount == 0)
            //{
            //    messageId = storage;
            //}
            var dialog = _dialog;
            //if (dialog == null)
            //{
            //    var response = await ProtoService.GetPeerDialogsAsync(new TLVector<TLInputPeerBase> { _peer });
            //    if (response.IsSucceeded)
            //    {
            //        Dialog = response.Result.Dialogs.FirstOrDefault();
            //        dialog = response.Result.Dialogs.FirstOrDefault();
            //    }
            //}

            //for (int i = 0; i < 200;)
            //{
            //    var request = new TLMessagesSendMessage { Peer = _peer, Message = "Message " + i, RandomId = TLLong.Random() };
            //    var response = await ProtoService.SendRequestAsync<TLUpdatesBase>("messages.sendMessage", request);
            //    if (response.IsSucceeded)
            //    {
            //        i++;
            //    }
            //    else
            //    {
            //        await Task.Delay(500);
            //    }
            //}

            if (participant is TLChannel before && before.IsMegaGroup)
            {
                Execute.BeginOnThreadPool(async () =>
                {
                    Admins.TryGetValue(before.Id, out IList<TLChannelParticipantBase> participants);

                    if (participants == null)
                    {
                        participants = new TLChannelParticipantBase[0];
                    }

                    var acc = 0L;
                    foreach (var item in participants.OrderBy(x => x.UserId))
                    {
                        acc = ((acc * 20261) + 0x80000000L + item.UserId) % 0x80000000L;
                    }

                    var response = await ProtoService.GetParticipantsAsync(before.ToInputChannel(), new TLChannelParticipantsAdmins(), 0, 200, (int)acc);
                    if (response.IsSucceeded && response.Result is TLChannelsChannelParticipants result)
                    {
                        participants = result.Participants;
                    }

                    Admins[before.Id] = participants;
                });
            }

            if (messageId is int slice)
            {
                LoadMessageSliceAsync(null, slice, highlight, pixel);
            }
            else
            {
                var maxId = _dialog?.UnreadCount > 0 ? _dialog.ReadInboxMaxId : int.MaxValue;
                var offset = _dialog?.UnreadCount > 0 && maxId > 0 ? -16 : 0;

                if (maxId == int.MaxValue)
                {
                    var history = CacheService.GetHistory(_peer.ToPeer());
                    if (history.IsEmpty())
                    {
                        LoadFirstSliceAsync(maxId, offset);
                    }
                    else
                    {
                        using (await _loadMoreLock.WaitAsync())
                        {
                            var verify = history.Where(x => x.RandomId == null).ToList();

                            ProcessReplies(history);
                            MessageCollection.ProcessReplies(history);

                            IsLastSliceLoaded = false;
                            IsFirstSliceLoaded = false;

                            ListField.SetScrollMode(ItemsUpdatingScrollMode.KeepLastItemInView, true);
                            Items.AddRange(history.Reverse());

                            foreach (var item in history.OrderByDescending(x => x.Date))
                            {
                                var message = item as TLMessage;
                                if (message != null && !message.IsOut && message.HasFromId && message.HasReplyMarkup && message.ReplyMarkup != null)
                                {
                                    var bot = CacheService.GetUser(message.FromId) as TLUser;
                                    if (bot != null && bot.IsBot)
                                    {
                                        SetReplyMarkup(message);
                                    }
                                }
                            }

                            var hash = 0L;
                            var hashable = verify.SelectMany(x =>
                            {
                                if (x is TLMessage msg)
                                {
                                    return new[] { x.Id, msg.EditDate ?? x.Date };
                                }

                                return new[] { x.Id, x.Date };
                            });

                            foreach (var item in hashable)
                            {
                                hash = ((hash * 20261) + 0x80000000L + item) % 0x80000000L;
                            }

                            //var response = await ProtoService.GetHistoryAsync(Peer, Peer.ToPeer(), true, -1, 0, history[0].Id, history.Count, (int)hash);
                            var response = await ProtoService.GetHistoryAsync(Peer, Peer.ToPeer(), true, 0, 0, int.MaxValue, verify.Count, (int)hash);
                            if (response.IsSucceeded && response.Result is ITLMessages result)
                            {
                                Items.Clear();

                                if (result.Messages.Count > 0)
                                {
                                    ListField.SetScrollMode(maxId == int.MaxValue ? ItemsUpdatingScrollMode.KeepLastItemInView : ItemsUpdatingScrollMode.KeepItemsInView, true);
                                }

                                ProcessReplies(result.Messages);
                                MessageCollection.ProcessReplies(result.Messages);

                                for (int i = result.Messages.Count - 1; i >= 0; i--)
                                {
                                    var item = result.Messages[i];
                                    if (item is TLMessageService serviceMessage && serviceMessage.Action is TLMessageActionHistoryClear)
                                    {
                                        continue;
                                    }

                                    Items.Add(item);
                                }

                                foreach (var item in result.Messages.OrderByDescending(x => x.Date))
                                {
                                    var message = item as TLMessage;
                                    if (message != null && !message.IsOut && message.HasFromId && message.HasReplyMarkup && message.ReplyMarkup != null)
                                    {
                                        var bot = CacheService.GetUser(message.FromId) as TLUser;
                                        if (bot != null && bot.IsBot)
                                        {
                                            SetReplyMarkup(message);
                                        }
                                    }
                                }

                                if (result.Messages.Count < history.Count)
                                {
                                    IsFirstSliceLoaded = maxId < int.MaxValue;
                                    IsLastSliceLoaded = maxId == int.MaxValue;
                                }
                                else
                                {
                                    IsFirstSliceLoaded = false;
                                    IsLastSliceLoaded = false;
                                }
                            }
                        };
                    }
                }
                else
                {
                    LoadFirstSliceAsync(maxId, offset);
                }
            }

            if (App.InMemoryState.ForwardMessages != null)
            {
                Reply = new TLMessagesContainter { FwdMessages = new TLVector<TLMessage>(App.InMemoryState.ForwardMessages) };
            }

            if (App.InMemoryState.SwitchInline != null)
            {
                var switchInlineButton = App.InMemoryState.SwitchInline;
                var bot = App.InMemoryState.SwitchInlineBot;

                SetText(string.Format("@{0} {1}", bot.Username, switchInlineButton.Query), focus: true);
                ResolveInlineBot(bot.Username, switchInlineButton.Query);

                App.InMemoryState.SwitchInline = null;
                App.InMemoryState.SwitchInlineBot = null;
            }
            else if (App.InMemoryState.SendMessage != null)
            {
                var message = App.InMemoryState.SendMessage;
                var hasUrl = App.InMemoryState.SendMessageUrl;

                SetText(message);

                if (hasUrl)
                {
                    SetSelection(message.IndexOf('\n') + 1);
                }

                App.InMemoryState.SendMessage = null;
                App.InMemoryState.SendMessageUrl = false;
            }
            else
            {
                if (dialog != null && dialog.HasDraft)
                {
                    if (dialog.Draft is TLDraftMessage draft)
                    {
                        SetText(draft.Message, draft.Entities);
                        ProcessDraftReply(draft);
                    }
                }
            }

            if (participant is TLUser user)
            {
                IsPhoneCallsAvailable = false;
                IsShareContactAvailable = user.HasAccessHash && !user.HasPhone && !user.IsSelf && !user.IsContact && !user.IsMutualContact;
                IsAddContactAvailable = user.HasAccessHash && user.HasPhone && !user.IsSelf && !user.IsContact && !user.IsMutualContact;

                var full = CacheService.GetFullUser(user.Id);
                if (full == null)
                {
                    var response = await ProtoService.GetFullUserAsync(new TLInputUser { UserId = user.Id, AccessHash = user.AccessHash ?? 0 });
                    if (response.IsSucceeded)
                    {
                        full = response.Result;
                    }
                }

                if (full != null)
                {
                    Full = full;
                    IsPhoneCallsAvailable = full.IsPhoneCallsAvailable && !user.IsSelf && ApiInformation.IsApiContractPresent("Windows.ApplicationModel.Calls.CallsVoipContract", 1);

                    if (user.IsBot && full.HasBotInfo)
                    {
                        BotCommands = full.BotInfo.Commands.Select(x => new TLUserCommand { User = user, Item = x }).ToList();
                        HasBotCommands = BotCommands.Count > 0;
                    }
                    else
                    {
                        BotCommands = null;
                        HasBotCommands = false;
                    }
                }
            }
            else if (participant is TLChannel channel)
            {
                IsPhoneCallsAvailable = false;
                IsShareContactAvailable = false;
                IsAddContactAvailable = false;

                var full = CacheService.GetFullChat(channel.Id) as TLChannelFull;
                if (full == null)
                {
                    var response = await ProtoService.GetFullChannelAsync(channel.ToInputChannel());
                    if (response.IsSucceeded)
                    {
                        full = response.Result.FullChat as TLChannelFull;
                    }
                }

                if (full != null)
                {
                    Full = full;

                    if (channel.IsMegaGroup)
                    {
                        var commands = new List<TLUserCommand>();

                        foreach (var info in full.BotInfo)
                        {
                            var bot = CacheService.GetUser(info.UserId) as TLUser;
                            if (bot != null)
                            {
                                commands.AddRange(info.Commands.Select(x => new TLUserCommand { User = bot, Item = x }));
                            }
                        }

                        BotCommands = commands;
                        HasBotCommands = BotCommands.Count > 0;

                        Stickers.SyncGroup(full);
                    }
                    else
                    {
                        BotCommands = null;
                        HasBotCommands = false;
                    }

                    if (full.HasPinnedMsgId)
                    {
                        var update = true;

                        var appData = ApplicationData.Current.LocalSettings.CreateContainer("Channels", ApplicationDataCreateDisposition.Always);
                        if (appData.Values.TryGetValue("Pinned" + channel.Id, out object pinnedObj))
                        {
                            var pinnedId = (int)pinnedObj;
                            if (pinnedId == full.PinnedMsgId)
                            {
                                update = false;
                            }
                        }

                        var pinned = CacheService.GetMessage(full.PinnedMsgId, channel.Id);
                        if (pinned == null && update)
                        {
                            var y = await ProtoService.GetMessagesAsync(channel.ToInputChannel(), new TLVector<int>() { full.PinnedMsgId ?? 0 });
                            if (y.IsSucceeded && y.Result is ITLMessages result)
                            {
                                CacheService.SyncUsersAndChats(result.Users, result.Chats, tuple => { });

                                pinned = result.Messages.FirstOrDefault();
                            }
                        }

                        if (pinned != null && update)
                        {
                            PinnedMessage = pinned;
                        }
                        else
                        {
                            PinnedMessage = null;
                        }
                    }
                }

            }
            else if (participant is TLChat chat)
            {
                IsPhoneCallsAvailable = false;
                IsShareContactAvailable = false;
                IsAddContactAvailable = false;

                var full = CacheService.GetFullChat(chat.Id) as TLChatFull;
                if (full == null)
                {
                    var response = await ProtoService.GetFullChatAsync(chat.Id);
                    if (response.IsSucceeded)
                    {
                        full = response.Result.FullChat as TLChatFull;
                    }
                }

                if (full != null)
                {
                    Full = full;

                    var commands = new List<TLUserCommand>();

                    foreach (var info in full.BotInfo)
                    {
                        var bot = CacheService.GetUser(info.UserId) as TLUser;
                        if (bot != null)
                        {
                            commands.AddRange(info.Commands.Select(x => new TLUserCommand { User = bot, Item = x }));
                        }
                    }

                    BotCommands = commands;
                    HasBotCommands = BotCommands.Count > 0;
                }
            }
            else if (participant is TLChatForbidden forbiddenChat)
            {
                With = forbiddenChat;
                Peer = new TLInputPeerChat { ChatId = forbiddenChat.Id };
            }

            if (dialog != null)
            {
                var unread = dialog.UnreadCount;
                if (Peer is TLInputPeerChannel && participant is TLChannel channel)
                {
                    await ProtoService.ReadHistoryAsync(channel, dialog.TopMessage);
                }
                else
                {
                    await ProtoService.ReadHistoryAsync(Peer, dialog.TopMessage, 0);
                }

                var readPeer = With as ITLReadMaxId;
                if (readPeer != null)
                {
                    readPeer.ReadInboxMaxId = dialog.TopMessage;
                }

                dialog.ReadInboxMaxId = dialog.TopMessage;
                dialog.UnreadCount = dialog.UnreadCount - unread;
                dialog.RaisePropertyChanged(() => dialog.UnreadCount);

                RemoveNotifications();
            }

            LastSeen = await GetSubtitle();

            var settings = await ProtoService.GetPeerSettingsAsync(Peer);
            if (settings.IsSucceeded)
            {
                IsReportSpam = settings.Result.IsReportSpam;
            }



            // Moved to the end: calls to notifications APIs are slow some times
            UpdatePinChatCommands();

            if (CanUnpinChat)
            {
                ResetTile();
            }

            //_timelineSession = await UserActivityHelper.GenerateActivityAsync(participant.ToPeer());
        }

        private async void ShowPinnedMessage(TLChannel channel)
        {
            if (channel == null) return;
            if (channel.PinnedMsgId == null) return;
            if (PinnedMessage != null && PinnedMessage.Id == channel.PinnedMsgId.Value) return;
            if (channel.HiddenPinnedMsgId != null && channel.HiddenPinnedMsgId.Value == channel.PinnedMsgId.Value) return;
            if (channel.PinnedMsgId.Value <= 0)
            {
                if (PinnedMessage != null)
                {
                    PinnedMessage = null;
                    return;
                }
            }
            else
            {
                var messageBase = CacheService.GetMessage(channel.PinnedMsgId, channel.Id);
                if (messageBase != null)
                {
                    PinnedMessage = messageBase;
                    return;
                }

                var response = await ProtoService.GetMessagesAsync(channel.ToInputChannel(), new TLVector<int> { channel.PinnedMsgId.Value });
                if (response.IsSucceeded && response.Result is ITLMessages result)
                {
                    CacheService.SyncUsersAndChats(result.Users, result.Chats, tuple => { });

                    PinnedMessage = result.Messages.FirstOrDefault(x => x.Id == channel.PinnedMsgId.Value);
                }
                else
                {
                    Telegram.Api.Helpers.Execute.ShowDebugMessage("channels.getMessages error " + response.Error);
                }
            }
        }

        private List<TLDocument> _stickerPack;
        public List<TLDocument> StickerPack
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

        private List<TLUserCommand> _botCommands;
        public List<TLUserCommand> BotCommands
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

        public override Task OnNavigatingFromAsync(NavigatingEventArgs args)
        {
            var peer = Peer?.ToPeer();
            if (peer == null)
            {
                return Task.CompletedTask;
            }

            try
            {
                var panel = ListField.ItemsPanelRoot as ItemsStackPanel;
                if (panel.LastVisibleIndex < Items.Count - 1 && Items.Count > 0)
                {
                    _scrollingIndex[peer] = Items[panel.LastVisibleIndex].Id;

                    var container = ListField.ContainerFromIndex(panel.LastVisibleIndex) as ListViewItem;
                    if (container != null)
                    {
                        var transform = container.TransformToVisual(ListField);
                        var position = transform.TransformPoint(new Point());

                        _scrollingPixel[peer] = ListField.ActualHeight - (position.Y + container.ActualHeight);
                    }
                }
                else
                {
                    _scrollingIndex.Remove(peer);
                    _scrollingPixel.Remove(peer);
                }
            }
            catch
            {
                _scrollingIndex.Remove(peer);
                _scrollingPixel.Remove(peer);
            }

            return Task.CompletedTask;
        }

        public override Task OnNavigatedFromAsync(IDictionary<string, object> pageState, bool suspending)
        {
            if (Dispatcher != null)
            {
                Dispatcher.Dispatch(SaveDraft);
            }

            //if (_timelineSession != null)
            //{
            //    _timelineSession.Dispose();
            //    _timelineSession = null;
            //}

            return Task.CompletedTask;
        }

        private ITLDialogWith GetParticipant(TLPeerBase peer)
        {
            if (peer is TLPeerUser user)
            {
                return CacheService.GetUser(user.UserId);
            }

            if (peer is TLPeerChat chat)
            {
                return CacheService.GetChat(chat.ChatId);
            }

            if (peer is TLPeerChannel channel)
            {
                return CacheService.GetChat(channel.ChannelId);
            }

            return null;
        }

        private void RemoveNotifications()
        {
            var group = string.Empty;
            if (_peer is TLInputPeerUser peerUser)
            {
                group = "u" + peerUser.UserId;
            }
            else if (_peer is TLInputPeerChannel peerChannel)
            {
                group = "c" + peerChannel.ChannelId;
            }
            else if (_peer is TLInputPeerChat peerChat)
            {
                group = "c" + peerChat.ChatId;
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

            if (EditedMessage != null)
            {
                return;
            }

            if (With == null)
            {
                return;
            }

            if (With is TLChannel channel)
            {
                if (channel.IsBroadcast && (!channel.IsCreator || !channel.HasAdminRights))
                {
                    return;
                }
            }

            var messageText = GetText().Format();
            var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);
            var reply = new int?();

            if (Reply is TLMessagesContainter container)
            {
            }
            else if (Reply != null)
            {
                reply = Reply.Id;
            }

            TLDraftMessageBase draft = null;
            if (reply.HasValue || !string.IsNullOrWhiteSpace(messageText))
            {
                draft = new TLDraftMessage { Message = messageText, Date = date, ReplyToMsgId = reply };
            }
            else if (_dialog != null && _dialog.HasDraft && _dialog.Draft is TLDraftMessage)
            {
                draft = new TLDraftMessageEmpty();
            }

            if (draft != null)
            {
                ProtoService.SaveDraftAsync(Peer, draft, result =>
                {
                    if (_dialog != null)
                    {
                        _dialog.Draft = draft;
                        _dialog.HasDraft = draft != null;
                    }

                    Aggregator.Publish(new TLUpdateDraftMessage { Peer = Peer.ToPeer(), Draft = draft });
                });
            }
        }

        public async void ProcessDraftReply(TLDraftMessage draft)
        {
            var shouldFetch = false;

            var replyId = draft.ReplyToMsgId;
            if (replyId != null && replyId.Value != 0)
            {
                var channelId = new int?();
                //var channel = message.ToId as TLPeerChat;
                //if (channel != null)
                //{
                //    channelId = channel.Id;
                //}
                // TODO: verify
                if (Peer is TLInputPeerChannel)
                {
                    channelId = Peer.ToPeer().Id;
                }

                var reply = CacheService.GetMessage(replyId.Value, channelId);
                if (reply != null)
                {
                    Reply = reply;
                }
                else
                {
                    shouldFetch = true;
                }
            }

            if (shouldFetch)
            {
                Task<MTProtoResponse<TLMessagesMessagesBase>> task = null;

                if (Peer is TLInputPeerChannel)
                {
                    // TODO: verify
                    //var first = replyToMsgs.FirstOrDefault();
                    //if (first.ToId is TLPeerChat)
                    //{
                    //    task = ProtoService.GetMessagesAsync(new TLVector<int> { draft.ReplyToMsgId.Value });
                    //}
                    //else
                    {
                        var peer = Peer as TLInputPeerChannel;
                        task = ProtoService.GetMessagesAsync(new TLInputChannel { ChannelId = peer.ChannelId, AccessHash = peer.AccessHash }, new TLVector<int> { draft.ReplyToMsgId.Value });
                    }
                }
                else
                {
                    task = ProtoService.GetMessagesAsync(new TLVector<int> { draft.ReplyToMsgId.Value });
                }

                var response = await task;
                if (response.IsSucceeded && response.Result is ITLMessages result)
                {
                    //CacheService.AddChats(result.Result.Chats, (results) => { });
                    //CacheService.AddUsers(result.Result.Users, (results) => { });
                    CacheService.SyncUsersAndChats(result.Users, result.Chats, tuple => { });

                    for (int j = 0; j < result.Messages.Count; j++)
                    {
                        if (draft.ReplyToMsgId.Value == result.Messages[j].Id)
                        {
                            Reply = result.Messages[j];
                        }
                    }
                }
                else
                {
                    Execute.ShowDebugMessage("messages.getMessages error " + response.Error);
                }
            }
        }

        private bool _isSilent;
        public bool IsSilent
        {
            get
            {
                return _isSilent && With is TLChannel && ((TLChannel)With).IsBroadcast;
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
                return With is TLChannel && ((TLChannel)With).IsBroadcast;
            }
        }

        #region Reply 

        private TLMessageBase _reply;
        public TLMessageBase Reply
        {
            get
            {
                return _reply;
            }
            set
            {
                if (_reply != value)
                {
                    if (_reply is TLMessagesContainter container && container.FwdMessages != null && value == null)
                    {
                        App.InMemoryState.ForwardMessages = null;
                    }

                    _reply = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(() => ReplyInfo);
                }
            }
        }

        public ReplyInfo ReplyInfo
        {
            get
            {
                if (_reply != null)
                {
                    return new ReplyInfo
                    {
                        Reply = _reply,
                        ReplyToMsgId = _reply.Id
                    };
                }

                return null;
            }
        }

        public RelayCommand ClearReplyCommand { get; }
        private void ClearReplyExecute()
        {
            if (Reply is TLMessagesContainter container && container.EditMessage != null)
            {
                SetText(null);
                //Aggregator.Publish(new EditMessageEventArgs(container.PreviousMessage, container.PreviousMessage.Message));
            }

            Reply = null;
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
            await SendMessageAsync(args, null, false);
        }

        public async Task SendMessageAsync(string text, List<TLMessageEntityBase> entities = null, bool useReplyMarkup = false)
        {
            if (Peer == null)
            {
                await new TLMessageDialog("Something went wrong. Close the chat and open it again.").ShowQueuedAsync();
                return;
            }

            if (With is TLChannel check && check.IsBroadcast && !(check.IsCreator || (check.HasAdminRights && check.AdminRights.IsPostMessages)))
            {
                return;
            }

            var messageText = text.Format();
            if (messageText.Equals("/tg_logs", StringComparison.OrdinalIgnoreCase))
            {
                var item = await FileUtils.TryGetItemAsync("Logs");
                if (item is StorageFolder folder)
                {
                    var files = await folder.GetFilesAsync();
                    foreach (var file in files)
                    {
                        await SendFileAsync(file);
                    }
                }

                return;
            }
            else if (messageText.Equals("/tg_dialog", StringComparison.OrdinalIgnoreCase))
            {
                if (_dialog != null)
                {
                    var data = new
                    {
                        Flags = _dialog.Flags,
                        Peer = _dialog.Peer,
                        TopMessage = _dialog.TopMessage,
                        ReadInboxMaxId = _dialog.ReadInboxMaxId,
                        ReadOutboxMaxId = _dialog.ReadOutboxMaxId,
                        UnreadCount = _dialog.UnreadCount,
                        UnreadMentionsCount = _dialog.UnreadMentionsCount,
                        NotifySettings = _dialog.NotifySettings,
                        Pts = _dialog.Pts,
                        Draft = _dialog.Draft,
                    };

                    var json = JsonConvert.SerializeObject(data, Formatting.Indented, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
                    json = json.Format();

                    await SendMessageAsync(json, new List<TLMessageEntityBase> { new TLMessageEntityPre { Offset = 0, Length = json.Length } });
                }

                return;
            }

            var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);
            var message = TLUtils.GetMessage(SettingsHelper.UserId, Peer.ToPeer(), TLMessageState.Sending, true, true, date, messageText, new TLMessageMediaEmpty(), TLLong.Random(), null);

            message.Entities = entities != null ? new TLVector<TLMessageEntityBase>(entities) : null;
            message.HasEntities = entities != null;

            if (message.Entities == null)
            {
                MessageHelper.PreprocessEntities(ref message);
            }

            var channel = With as TLChannel;
            if (channel != null && channel.IsBroadcast)
            {
                message.IsSilent = IsSilent;
            }

            IEnumerable<TLMessage> forwardMessages = null;

            if (Reply != null)
            {
                if (Reply is TLMessagesContainter container)
                {
                    if (container.EditMessage != null)
                    {
                        var edit = await ProtoService.EditMessageAsync(Peer, container.EditMessage.Id, message.Message, message.Entities, null, null, false, false);
                        if (edit.IsSucceeded)
                        {
                            CacheService.SyncEditedMessage(container.EditMessage, true, true, cachedMessage =>
                            {
                                if (container.EditMessage.HasGroupedId && container.EditMessage.GroupedId is long groupedId && _groupedMessages.TryGetValue(groupedId, out TLMessage group) && group.Media is TLMessageMediaGroup groupMedia)
                                {
                                    groupMedia.Caption = message.Message;
                                    groupMedia.RaisePropertyChanged(() => groupMedia.Caption);
                                    group.RaisePropertyChanged(() => group.Self);
                                }
                            });
                        }
                        else
                        {

                        }

                        Reply = null;
                        return;
                    }
                    else if (container.FwdMessages != null)
                    {
                        forwardMessages = container.FwdMessages;
                        Reply = null;
                    }
                }
                else if (Reply != null)
                {
                    message.HasReplyToMsgId = true;
                    message.ReplyToMsgId = Reply.Id;
                    message.Reply = Reply;
                    Reply = null;
                }
            }

            if (string.IsNullOrWhiteSpace(messageText) == false)
            {
                var previousMessage = InsertSendingMessage(message, useReplyMarkup);
                CacheService.SyncSendingMessage(message, previousMessage, async (m) =>
                {
                    var response = await ProtoService.SendMessageAsync(message, null);
                    if (response.IsSucceeded)
                    {
                        message.RaisePropertyChanged(() => message.Media);
                    }
                    else
                    {
                        if (response.Error.TypeEquals(TLErrorType.PEER_FLOOD))
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

                        return;
                    }

                    if (forwardMessages != null)
                    {
                        App.InMemoryState.ForwardMessages = null;
                        ForwardMessages(forwardMessages);
                    }
                });
            }
            else
            {
                if (forwardMessages != null)
                {
                    App.InMemoryState.ForwardMessages = null;
                    ForwardMessages(forwardMessages);
                }
            }
        }

        public void ForwardMessages(IEnumerable<TLMessage> forwardMessages)
        {
            var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);

            TLInputPeerBase fromPeer = null;
            var msgs = new TLVector<TLMessage>();
            var msgIds = new TLVector<int>();

            foreach (var fwdMessage in forwardMessages)
            {
                var clone = fwdMessage.Clone();
                clone.Id = 0;
                clone.HasEditDate = false;
                clone.EditDate = null;
                clone.HasReplyToMsgId = false;
                clone.ReplyToMsgId = null;
                clone.HasReplyMarkup = false;
                clone.ReplyMarkup = null;
                clone.Date = date;
                clone.ToId = Peer.ToPeer();
                clone.RandomId = TLLong.Random();
                clone.IsOut = true;
                clone.IsPost = false;
                clone.FromId = SettingsHelper.UserId;
                clone.IsMediaUnread = Peer is TLInputPeerChannel ? true : false;
                clone.IsUnread = true;
                clone.State = TLMessageState.Sending;

                if (clone.Media == null)
                {
                    clone.HasMedia = true;
                    clone.Media = new TLMessageMediaEmpty();
                }

                if (With is TLChannel channel)
                {
                    if (channel.IsBroadcast)
                    {
                        if (!channel.IsSignatures)
                        {
                            clone.HasFromId = false;
                            clone.FromId = null;
                        }

                        if (IsSilent)
                        {
                            clone.IsSilent = true;
                        }

                        clone.HasViews = true;
                        clone.Views = 1;
                    }
                }

                if (clone.Media is TLMessageMediaGame gameMedia)
                {
                    clone.HasEntities = false;
                    clone.Entities = null;
                    clone.Message = null;
                }
                else if (clone.Media is TLMessageMediaGeoLive geoLiveMedia)
                {
                    clone.Media = new TLMessageMediaGeo { Geo = geoLiveMedia.Geo };
                }

                if (fromPeer == null)
                {
                    fromPeer = fwdMessage.Parent.ToInputPeer();
                }

                if (clone.FwdFrom == null && !clone.IsGame())
                {
                    if (fwdMessage.ToId is TLPeerChannel)
                    {
                        var fwdChannel = CacheService.GetChat(fwdMessage.ToId.Id) as TLChannel;
                        if (fwdChannel != null && fwdChannel.IsMegaGroup)
                        {
                            clone.HasFwdFrom = true;
                            clone.FwdFrom = new TLMessageFwdHeader
                            {
                                HasFromId = true,
                                FromId = fwdMessage.FromId,
                                Date = fwdMessage.Date
                            };
                        }
                        else
                        {
                            clone.HasFwdFrom = true;
                            clone.FwdFrom = new TLMessageFwdHeader
                            {
                                HasFromId = fwdMessage.HasFromId,
                                FromId = fwdMessage.FromId,
                                Date = fwdMessage.Date
                            };

                            if (fwdChannel.IsBroadcast)
                            {
                                clone.FwdFrom.HasChannelId = clone.FwdFrom.HasChannelPost = true;
                                clone.FwdFrom.ChannelId = fwdChannel.Id;
                                clone.FwdFrom.ChannelPost = fwdMessage.Id;
                                clone.FwdFrom.HasPostAuthor = fwdMessage.HasPostAuthor;
                                clone.FwdFrom.PostAuthor = fwdMessage.PostAuthor;
                            }
                        }
                    }
                    else if (fwdMessage.FromId == SettingsHelper.UserId && fwdMessage.ToId is TLPeerUser peerUser && peerUser.UserId == SettingsHelper.UserId)
                    {

                    }
                    else
                    {
                        clone.HasFwdFrom = true;
                        clone.FwdFrom = new TLMessageFwdHeader
                        {
                            HasFromId = true,
                            FromId = fwdMessage.FromId,
                            Date = fwdMessage.Date
                        };
                    }

                    if (clone.FwdFrom != null && ((_peer is TLInputPeerUser user && user.UserId == SettingsHelper.UserId) || _peer is TLInputPeerSelf))
                    {
                        clone.FwdFrom.SavedFromMsgId = fwdMessage.Id;
                        clone.FwdFrom.SavedFromPeer = fwdMessage.Parent?.ToPeer();
                        clone.FwdFrom.HasSavedFromMsgId = true;
                        clone.FwdFrom.HasSavedFromPeer = clone.FwdFrom.SavedFromPeer != null;
                    }
                }

                msgs.Add(clone);
                msgIds.Add(fwdMessage.Id);

                Items.Add(clone);
            }

            CacheService.SyncSendingMessages(msgs, null, async (_) =>
            {
                await ProtoService.ForwardMessagesAsync(Peer, fromPeer, msgIds, msgs, false, false);
            });
        }

        private TLMessageBase InsertSendingMessage(TLMessage message, bool useReplyMarkup = false)
        {
            CheckChannelMessage(message);

            if (_dialog != null && _peer != null && _dialog.Draft is TLDraftMessage)
            {
                var draft = new TLDraftMessageEmpty();
                var peer = _peer.ToPeer();

                _dialog.Draft = draft;
                _dialog.HasDraft = draft != null;

                Aggregator.Publish(new TLUpdateDraftMessage { Peer = peer, Draft = draft });
            }

            TLMessageBase result;
            if (IsFirstSliceLoaded)
            {
                if (useReplyMarkup && _replyMarkupMessage != null)
                {
                    var chat = With as TLChatBase;
                    if (chat != null)
                    {
                        message.HasReplyToMsgId = true;
                        message.ReplyToMsgId = _replyMarkupMessage.Id;
                        message.Reply = _replyMarkupMessage;
                    }

                    BeginOnUIThread(() =>
                    {
                        if (Reply != null)
                        {
                            Reply = null;
                            SetReplyMarkup(null);
                        }
                    });
                }

                var messagesContainer = Reply as TLMessagesContainter;
                if (Reply != null)
                {
                    if (Reply.Id != 0)
                    {
                        message.HasReplyToMsgId = true;
                        message.ReplyToMsgId = Reply.Id;
                        message.Reply = Reply;
                    }
                    else if (messagesContainer != null && !string.IsNullOrEmpty(message.Message.ToString()))
                    {
                        message.Reply = Reply;
                    }

                    var replyMessage = Reply as TLMessage;
                    if (replyMessage != null)
                    {
                        //var replyMarkup = replyMessage.ReplyMarkup;
                        //if (replyMarkup != null)
                        //{
                        //    replyMarkup.HasResponse = true;
                        //}
                    }

                    BeginOnUIThread(delegate
                    {
                        Reply = null;
                    });
                }

                result = Items.LastOrDefault();
                Items.Add(message);

                //if (messagesContainer != null && !string.IsNullOrEmpty(message.Message))
                //{
                //    foreach (var fwdMessage in messagesContainer.FwdMessages)
                //    {
                //        Messages.Insert(0, fwdMessage);
                //    }
                //}

                for (int i = 1; i < Items.Count; i++)
                {
                    var serviceMessage = Items[i] as TLMessageService;
                    if (serviceMessage != null)
                    {
                        var unreadAction = serviceMessage.Action as TLMessageActionUnreadMessages;
                        if (unreadAction != null)
                        {
                            Items.RemoveAt(i);
                            break;
                        }
                    }
                }

                ScrollToBottom(message);
            }
            else
            {
                var container = Reply as TLMessagesContainter;
                if (Reply != null)
                {
                    if (Reply.Id != 0)
                    {
                        message.HasReplyToMsgId = true;
                        message.ReplyToMsgId = Reply.Id;
                        message.Reply = Reply;
                    }
                    else if (container != null && !string.IsNullOrEmpty(message.Message))
                    {
                        message.Reply = Reply;
                    }

                    Reply = null;
                }

                Items.Clear();
                Items.Insert(0, message);

                var history = CacheService.GetHistory(Peer.ToPeer(), 15);
                result = history.FirstOrDefault();

                for (int j = 0; j < history.Count; j++)
                {
                    Items.Insert(0, history[j]);
                }

                //if (container != null && !string.IsNullOrEmpty(message.Message.ToString()))
                //{
                //    foreach (var fwdMessage in container.FwdMessages)
                //    {
                //        Messages.Insert(0, fwdMessage);
                //    }
                //}

                for (int k = 1; k < Items.Count; k++)
                {
                    if (Items[k] is TLMessageService serviceMessage)
                    {
                        if (serviceMessage.Action is TLMessageActionUnreadMessages unreadAction)
                        {
                            Items.RemoveAt(k);
                            break;
                        }
                    }
                }

                //ScrollToBottom();

                //var messagesContainer = Reply as TLMessagesContainter;
                //if (Reply != null)
                //{
                //    if (Reply.Id != 0)
                //    {
                //        message.HasReplyToMsgId = true;
                //        message.ReplyToMsgId = Reply.Id;
                //        message.Reply = Reply;
                //    }
                //    else if (messagesContainer != null && !string.IsNullOrEmpty(message.Message.ToString()))
                //    {
                //        message.Reply = Reply;
                //    }
                //    Reply = null;
                //}

                //Messages.Clear();
                //Messages.Add(message);

                //var history = CacheService.GetHistory(Peer.ToPeer(), 15);
                //result = history.FirstOrDefault();

                ////for (int j = 0; j < history.Count; j++)
                ////{
                ////    Messages.Add(history[j]);
                ////}





                ////if (messagesContainer != null && !string.IsNullOrEmpty(message.Message.ToString()))
                ////{
                ////    foreach (var fwdMessage in messagesContainer.FwdMessages)
                ////    {
                ////        Messages.Insert(0, fwdMessage);
                ////    }
                ////}

                ////for (int k = 1; k < Messages.Count; k++)
                ////{
                ////    var serviceMessage = Messages[k] as TLMessageService;
                ////    if (serviceMessage != null)
                ////    {
                ////        var unreadAction = serviceMessage.Action as TLMessageActionUnreadMessages;
                ////        if (unreadAction != null)
                ////        {
                ////            Messages.RemoveAt(k);
                ////            break;
                ////        }
                ////    }
                ////}
            }

            return result;
        }

        private void CheckChannelMessage(TLMessage message)
        {
            if (With is TLChannel channel && channel.IsBroadcast)
            {
                //if (channel.IsSilent)
                //{
                //    message.IsSilent = true;
                //}

                var self = CacheService.GetUser(SettingsHelper.UserId) as TLUser;

                if (channel.IsSignatures && self != null)
                {
                    message.PostAuthor = self.FullName;
                    message.HasPostAuthor = true;
                }

                message.FromId = null;
                message.HasFromId = false;

                message.Views = 1;
                message.HasViews = true;

                message.IsOut = false;
                message.IsPost = true;
            }
        }

        private async Task<string> GetSubtitle()
        {
            if (With is TLUser user)
            {
                return LastSeenConverter.GetLabel(user, true);
            }
            else if (With is TLChannel channel && channel.HasAccessHash && channel.AccessHash.HasValue)
            {
                var full = Full as TLChannelFull;
                if (full == null)
                {
                    full = CacheService.GetFullChat(channel.Id) as TLChannelFull;
                }

                if (full == null)
                {
                    var response = await ProtoService.GetFullChannelAsync(new TLInputChannel { ChannelId = channel.Id, AccessHash = channel.AccessHash.Value });
                    if (response.IsSucceeded)
                    {
                        full = response.Result.FullChat as TLChannelFull;
                    }
                }

                if (full == null)
                {
                    return string.Empty;
                }

                if (channel.IsBroadcast && full.HasParticipantsCount)
                {
                    return LocaleHelper.Declension("Subscribers", full.ParticipantsCount ?? 0);
                }
                else if (full.HasParticipantsCount)
                {
                    var config = CacheService.GetConfig();
                    if (config == null)
                    {
                        return LocaleHelper.Declension("Members", full.ParticipantsCount ?? 0);
                    }

                    var participants = await ProtoService.GetParticipantsAsync(channel.ToInputChannel(), new TLChannelParticipantsRecent(), 0, config.ChatSizeMax, 0);
                    if (participants.IsSucceeded && participants.Result is TLChannelsChannelParticipants channelParticipants)
                    {
                        full.Participants = participants.Result;

                        if (full.ParticipantsCount <= config.ChatSizeMax)
                        {
                            var count = 0;
                            foreach (var item in channelParticipants.Users.OfType<TLUser>())
                            {
                                if (item.HasStatus && item.Status is TLUserStatusOnline)
                                {
                                    count++;
                                }
                            }

                            if (count > 1)
                            {
                                return string.Format("{0}, {1}", LocaleHelper.Declension("Members", full.ParticipantsCount ?? 0), LocaleHelper.Declension("OnlineCount", count));
                            }
                        }
                    }

                    return LocaleHelper.Declension("Members", full.ParticipantsCount ?? 0);
                }
            }
            else if (With is TLChat chat)
            {
                var full = Full as TLChatFull;
                if (full == null)
                {
                    full = CacheService.GetFullChat(chat.Id) as TLChatFull;
                }

                if (full == null)
                {
                    var response = await ProtoService.GetFullChatAsync(chat.Id);
                    if (response.IsSucceeded)
                    {
                        full = response.Result.FullChat as TLChatFull;
                    }
                }

                if (full == null)
                {
                    return string.Empty;
                }

                var participants = full.Participants as TLChatParticipants;
                if (participants != null)
                {
                    var count = 0;
                    foreach (var item in participants.Participants)
                    {
                        if (item.User != null && item.User.HasStatus && item.User.Status is TLUserStatusOnline)
                        {
                            count++;
                        }
                    }

                    if (count > 1)
                    {
                        return string.Format("{0}, {1}", LocaleHelper.Declension("Members", participants.Participants.Count), LocaleHelper.Declension("OnlineCount", count));
                    }

                    return LocaleHelper.Declension("Members", participants.Participants.Count);
                }
            }

            return string.Empty;
        }

        #region Join channel

        public RelayCommand JoinChannelCommand { get; }
        private async void JoinChannelExecute()
        {
            var channel = With as TLChannel;
            if (channel == null)
            {
                return;
            }

            var response = await ProtoService.JoinChannelAsync(channel);
            if (response.IsSucceeded)
            {
                RaisePropertyChanged(() => With);

                if (response.Result is TLUpdates update)
                {
                    var newChannelMessage = update.Updates.FirstOrDefault(x => x is TLUpdateNewChannelMessage) as TLUpdateNewChannelMessage;
                    if (newChannelMessage != null)
                    {
                        Items.Add(newChannelMessage.Message);

                        if (_dialog == null)
                        {
                            _dialog = CacheService.GetDialog(channel.ToPeer());
                        }

                        Aggregator.Publish(new TopMessageUpdatedEventArgs(_dialog, newChannelMessage.Message));
                    }
                    else
                    {
                        var message = new TLMessageService
                        {
                            RandomId = TLLong.Random(),
                            FromId = SettingsHelper.UserId,
                            ToId = new TLPeerChannel
                            {
                                Id = channel.Id
                            },
                            State = TLMessageState.Confirmed,
                            IsOut = true,
                            IsUnread = false,
                            Date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, System.DateTime.Now),
                            Action = new TLMessageActionChatAddUser
                            {
                                Users = new TLVector<int> { SettingsHelper.UserId }
                            }
                        };

                        CacheService.SyncMessage(message, true, true, cachedMessage =>
                        {
                            Items.Add(cachedMessage);

                            if (_dialog == null)
                            {
                                _dialog = CacheService.GetDialog(channel.ToPeer());
                            }

                            Aggregator.Publish(new TopMessageUpdatedEventArgs(_dialog, newChannelMessage.Message));
                        });
                    }
                }
            }
        }

        #endregion

        #region Toggle mute

        public RelayCommand ToggleMuteCommand { get; }
        private async void ToggleMuteExecute()
        {
            TLPeerNotifySettings notifySettings = null;

            var chatFull = Full as TLChatFullBase;
            var userFull = Full as TLUserFull;

            if (chatFull != null)
            {
                notifySettings = chatFull.NotifySettings as TLPeerNotifySettings;
            }
            else if (userFull != null)
            {
                notifySettings = userFull.NotifySettings as TLPeerNotifySettings;
            }

            if (notifySettings == null)
            {
                return;
            }

            var peer = _peer;
            if (peer == null)
            {
                return;
            }

            var muteUntil = notifySettings.MuteUntil == int.MaxValue ? 0 : int.MaxValue;
            var settings = new TLInputPeerNotifySettings
            {
                MuteUntil = muteUntil,
                IsShowPreviews = notifySettings.IsShowPreviews,
                IsSilent = notifySettings.IsSilent,
                Sound = notifySettings.Sound
            };

            var response = await ProtoService.UpdateNotifySettingsAsync(new TLInputNotifyPeer { Peer = peer }, settings);
            if (response.IsSucceeded)
            {
                notifySettings.MuteUntil = muteUntil;

                var dialog = CacheService.GetDialog(peer.ToPeer());
                if (dialog != null)
                {
                    dialog.NotifySettings = notifySettings;
                    dialog.RaisePropertyChanged(() => dialog.NotifySettings);
                    dialog.RaisePropertyChanged(() => dialog.IsMuted);
                    dialog.RaisePropertyChanged(() => dialog.Self);
                }

                if (chatFull != null)
                {
                    chatFull.NotifySettings = notifySettings;
                    chatFull.RaisePropertyChanged(() => chatFull.NotifySettings);
                }
                else if (userFull != null)
                {
                    userFull.NotifySettings = notifySettings;
                    userFull.RaisePropertyChanged(() => userFull.NotifySettings);
                }

                CacheService.Commit();
                RaisePropertyChanged(() => With);
            }
        }

        #endregion

        #region Toggle silent

        public RelayCommand ToggleSilentCommand { get; }
        private async void ToggleSilentExecute()
        {
            var channel = With as TLChannel;
            if (channel != null && _dialog != null)
            {
                var notifySettings = _dialog.NotifySettings as TLPeerNotifySettings;
                if (notifySettings != null)
                {
                    var silent = !notifySettings.IsSilent;
                    var settings = new TLInputPeerNotifySettings
                    {
                        MuteUntil = notifySettings.MuteUntil,
                        IsShowPreviews = notifySettings.IsShowPreviews,
                        IsSilent = silent,
                        Sound = notifySettings.Sound
                    };

                    var response = await ProtoService.UpdateNotifySettingsAsync(new TLInputNotifyPeer { Peer = Peer }, settings);
                    if (response.IsSucceeded)
                    {
                        notifySettings.IsSilent = silent;
                        channel.RaisePropertyChanged(() => _dialog.NotifySettings);

                        var dialog = CacheService.GetDialog(Peer.ToPeer());
                        if (dialog != null)
                        {
                            dialog.NotifySettings = _dialog.NotifySettings;
                            dialog.RaisePropertyChanged(() => dialog.NotifySettings);
                            dialog.RaisePropertyChanged(() => dialog.IsMuted);
                            dialog.RaisePropertyChanged(() => dialog.Self);

                            var chatFull = CacheService.GetFullChat(channel.Id);
                            if (chatFull != null)
                            {
                                chatFull.NotifySettings = _dialog.NotifySettings;
                                chatFull.RaisePropertyChanged(() => chatFull.NotifySettings);
                            }

                            // TODO: 06/05/2017
                            //var dialogChannel = dialog.With as TLChannel;
                            //if (dialogChannel != null)
                            //{
                            //    dialogChannel.NotifySettings = channel.NotifySettings;
                            //}
                        }

                        CacheService.Commit();
                        RaisePropertyChanged(() => With);
                    }
                }
            }
        }

        #endregion

        #region Report Spam

        public RelayCommand HideReportSpamCommand { get; }
        private async void HideReportSpamExecute()
        {
            var response = await ProtoService.HideReportSpamAsync(Peer);
            if (response.IsSucceeded)
            {
                IsReportSpam = false;
            }
        }

        public RelayCommand ReportSpamCommand { get; }
        private async void ReportSpamExecute()
        {
            if (IsReportSpam)
            {
                var message = Strings.Android.ReportSpamAlert;
                if (With is TLChannel channel)
                {
                    message = channel.IsMegaGroup ? Strings.Android.ReportSpamAlertGroup : Strings.Android.ReportSpamAlertChannel;
                }
                else if (With is TLChat chat)
                {
                    message = Strings.Android.ReportSpamAlertGroup;
                }

                var confirm = await TLMessageDialog.ShowAsync(message, Strings.Android.AppName, Strings.Android.OK, Strings.Android.Cancel);
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }

                var response = await ProtoService.ReportSpamAsync(Peer);
                if (response.IsSucceeded)
                {
                    IsReportSpam = false;
                    DialogDeleteExecute();
                }
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
        private void DialogDeleteExecute()
        {
            if (_dialog != null)
            {
                UnigramContainer.Current.ResolveType<MainViewModel>().Dialogs.DialogDeleteCommand.Execute(_dialog);
            }
        }

        #endregion

        #region Clear history

        public RelayCommand DialogClearCommand { get; }
        private void DialogClearExecute()
        {
            if (_dialog != null)
            {
                UnigramContainer.Current.ResolveType<MainViewModel>().Dialogs.DialogClearCommand.Execute(_dialog);
            }
        }

        #endregion

        #region Call

        public RelayCommand CallCommand { get; }
        private async void CallExecute()
        {
            var user = With as TLUser;
            if (user == null)
            {
                return;
            }

            try
            {
                var coordinator = VoipCallCoordinator.GetDefault();
                var result = await coordinator.ReserveCallResourcesAsync("Unigram.Tasks.VoIPCallTask");
                if (result == VoipPhoneCallResourceReservationStatus.Success)
                {
                    await VoIPConnection.Current.SendRequestAsync("voip.startCall", user);
                }
            }
            catch
            {
                await TLMessageDialog.ShowAsync("Something went wrong. Please, try to close and relaunch the app.", "Unigram", "OK");
            }
        }

        #endregion

        #region Unpin message

        public RelayCommand UnpinMessageCommand { get; }
        private async void UnpinMessageExecute()
        {
            if (_pinnedMessage == null)
            {
                return;
            }

            var channel = With as TLChannel;
            if (channel == null)
            {
                return;
            }

            if (channel.IsCreator || (channel.HasAdminRights && channel.AdminRights.IsPinMessages))
            {
                var confirm = await TLMessageDialog.ShowAsync(Strings.Android.UnpinMessageAlert, Strings.Android.AppName, Strings.Android.OK, Strings.Android.Cancel);
                if (confirm == ContentDialogResult.Primary)
                {
                    var response = await ProtoService.UpdatePinnedMessageAsync(false, channel.ToInputChannel(), 0);
                    if (response.IsSucceeded)
                    {
                        PinnedMessage = null;
                    }
                    else
                    {
                        // TODO
                    }
                }
            }
            else
            {
                var appData = ApplicationData.Current.LocalSettings.CreateContainer("Channels", ApplicationDataCreateDisposition.Always);
                appData.Values["Pinned" + channel.Id] = _pinnedMessage.Id;

                PinnedMessage = null;
            }
        }

        #endregion

        #region Unblock

        public RelayCommand UnblockCommand { get; }
        private async void UnblockExecute()
        {
            var user = _with as TLUser;
            if (user == null)
            {
                return;
            }

            var confirm = await TLMessageDialog.ShowAsync(Strings.Android.AreYouSureUnblockContact, Strings.Android.AppName, Strings.Android.OK, Strings.Android.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var result = await ProtoService.UnblockAsync(user.ToInputUser());
            if (result.IsSucceeded)
            {
                if (Full is TLUserFull full)
                {
                    full.IsBlocked = false;
                    full.RaisePropertyChanged(() => full.IsBlocked);
                }

                RaisePropertyChanged(() => With);
                RaisePropertyChanged(() => Full);

                CacheService.Commit();
                Aggregator.Publish(new TLUpdateUserBlocked { UserId = user.Id, Blocked = false });
            }
        }

        #endregion

        #region Switch

        public RelayCommand<TLInlineBotSwitchPM> SwitchCommand { get; }
        private void SwitchExecute(TLInlineBotSwitchPM switchPM)
        {
            if (_currentInlineBot == null)
            {
                return;
            }

            // TODO: edit to send it automatically
            NavigationService.NavigateToDialog(_currentInlineBot, accessToken: switchPM.StartParam);
        }

        #endregion

        #region Share my contact

        public RelayCommand ShareContactCommand { get; }
        private async void ShareContactExecute()
        {
            var user = InMemoryCacheService.Current.GetUser(SettingsHelper.UserId) as TLUser;
            if (user == null)
            {
                return;
            }

            await SendContactAsync(user);
        }

        #endregion

        #region Add contact

        public RelayCommand AddContactCommand { get; }
        private async void AddContactExecute()
        {
            var user = With as TLUser;
            if (user == null)
            {
                return;
            }

            var confirm = await EditUserNameView.Current.ShowAsync(user.FirstName, user.LastName);
            if (confirm == ContentDialogResult.Primary)
            {
                var contact = new TLInputPhoneContact
                {
                    ClientId = user.Id,
                    FirstName = EditUserNameView.Current.FirstName,
                    LastName = EditUserNameView.Current.LastName,
                    Phone = user.Phone
                };

                var response = await ProtoService.ImportContactsAsync(new TLVector<TLInputContactBase> { contact });
                if (response.IsSucceeded)
                {
                    if (response.Result.Users.Count > 0)
                    {
                        Aggregator.Publish(new TLUpdateContactLink
                        {
                            UserId = response.Result.Users[0].Id,
                            MyLink = new TLContactLinkContact(),
                            ForeignLink = new TLContactLinkUnknown()
                        });
                    }

                    user.RaisePropertyChanged(() => user.HasFirstName);
                    user.RaisePropertyChanged(() => user.HasLastName);
                    user.RaisePropertyChanged(() => user.FirstName);
                    user.RaisePropertyChanged(() => user.LastName);
                    user.RaisePropertyChanged(() => user.FullName);
                    user.RaisePropertyChanged(() => user.DisplayName);

                    user.RaisePropertyChanged(() => user.HasPhone);
                    user.RaisePropertyChanged(() => user.Phone);

                    var dialog = CacheService.GetDialog(user.ToPeer());
                    if (dialog != null)
                    {
                        dialog.RaisePropertyChanged(() => dialog.With);
                    }
                }
            }
        }

        #endregion

        #region Pin chat

        public RelayCommand PinChatCommand { get; }
        private async void PinChatExecute()
        {
            var group = _pushService.GetGroup(this.With);
            if (string.IsNullOrWhiteSpace(group))
            {
                return;
            }

            var displayName = _pushService.GetTitle(this.With);
            var arguments = _pushService.GetLaunch(this.With);
            var picture = _pushService.GetPicture(this.With, group);

            var secondaryTile = new SecondaryTile(group, displayName, arguments, new Uri(picture), TileSize.Default);
            secondaryTile.VisualElements.Wide310x150Logo = new Uri(picture);
            secondaryTile.VisualElements.Square310x310Logo = new Uri(picture);

            var tileCreated = await secondaryTile.RequestCreateAsync();
            if (tileCreated)
            {
                UpdatePinChatCommands();
                ResetTile();
            }
        }

        private void ResetTile()
        {
            var group = _pushService.GetGroup(this.With);
            if (string.IsNullOrWhiteSpace(group))
            {
                return;
            }

            var displayName = _pushService.GetTitle(this.With);
            var picture = _pushService.GetPicture(this.With, group);

            var existsSecondaryTile = SecondaryTile.Exists(group);
            if (existsSecondaryTile)
            {
                NotificationTask.ResetSecondaryTile(displayName, picture, group);
            }
        }

        private bool CanExecutePinChatCommand()
        {
            var group = _pushService.GetGroup(this.With);
            if (string.IsNullOrWhiteSpace(group))
            {
                return false;
            }

            return !SecondaryTile.Exists(group);
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
            var group = _pushService.GetGroup(this.With);
            if (string.IsNullOrWhiteSpace(group))
            {
                return;
            }

            var secondaryTile = new SecondaryTile(group);
            if (secondaryTile == null)
            {
                return;
            }

            var tileDeleted = await secondaryTile.RequestDeleteAsync();
            if (tileDeleted)
            {
                UpdatePinChatCommands();
            }
        }

        private bool CanUnpinChatExecute()
        {
            var group = _pushService.GetGroup(this.With);
            if (string.IsNullOrWhiteSpace(group))
            {
                return false;
            }

            return SecondaryTile.Exists(group);
        }

        #endregion

        #region Start

        public RelayCommand StartCommand { get; }
        private async void StartExecute()
        {
            var bot = GetStartingBot();
            var command = _with is TLUser ? "/start" : "/start@" + bot.Username;

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
                var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);
                var message = TLUtils.GetMessage(SettingsHelper.UserId, Peer.ToPeer(), TLMessageState.Sending, true, true, date, command, new TLMessageMediaEmpty(), TLLong.Random(), null);
                var previousMessage = InsertSendingMessage(message, false);

                CacheService.SyncSendingMessage(message, previousMessage, async m =>
                {
                    var response = await ProtoService.StartBotAsync(bot.ToInputUser(), _accessToken, message);
                    if (response.IsSucceeded)
                    {
                        AccessToken = null;
                    }
                    else
                    {
                        if (response.Error.TypeEquals(TLErrorType.PEER_FLOOD))
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

                        return;
                    }
                });
            }
        }

        private TLUser GetStartingBot()
        {
            var user = _with as TLUser;
            if (user != null && user.IsBot)
            {
                return user;
            }

            var chat = _with as TLChatBase;
            if (chat != null)
            {
                // TODO
                //return this._bot;
            }

            return null;
        }

        #endregion

        #region Search

        public RelayCommand SearchCommand { get; }
        private void SearchExecute()
        {
            Search = new DialogSearchViewModel(ProtoService, CacheService, Aggregator, this);
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
                var offset = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, dialog.SelectedDates.FirstOrDefault().Date);
                await LoadDateSliceAsync(offset);
            }
        }

        #endregion

        #region Group stickers

        public RelayCommand GroupStickersCommand { get; }
        private void GroupStickersExecute()
        {
            var channel = With as TLChannel;
            if (channel == null)
            {
                return;
            }

            var channelFull = Full as TLChannelFull;
            if (channelFull == null)
            {
                return;
            }

            if ((channel.IsCreator || (channel.HasAdminRights && channel.AdminRights.IsChangeInfo)) && channelFull.IsCanSetStickers)
            {
                NavigationService.Navigate(typeof(ChannelEditStickerSetPage), channel.ToPeer());
            }
            else
            {
                Stickers.HideGroup(channelFull);
            }
        }

        #endregion

        #region Read mentions

        public RelayCommand ReadMentionsCommand { get; }
        private async void ReadMentionsExecute()
        {
            var peer = _peer;
            if (peer == null)
            {
                return;
            }

            var dialog = _dialog;
            if (dialog == null)
            {
                return;
            }

            var confirm = await TLMessageDialog.ShowAsync("Are you sure you want to clear your mentions?", "Telegram", "OK", "Cancel");
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var response = await ProtoService.ReadMentionsAsync(peer);
            if (response.IsSucceeded)
            {
                dialog.UnreadMentionsCount = 0;
                dialog.RaisePropertyChanged(() => dialog.UnreadMentionsCount);
            }
        }

        #endregion

        #region Report Chat

        public RelayCommand ReportCommand { get; }
        private async void ReportExecute()
        {
            var peer = _peer;
            if (peer == null)
            {
                return;
            }

            var opt1 = new RadioButton { Content = Strings.Android.ReportChatSpam, HorizontalAlignment = HorizontalAlignment.Stretch };
            var opt2 = new RadioButton { Content = Strings.Android.ReportChatViolence, HorizontalAlignment = HorizontalAlignment.Stretch };
            var opt3 = new RadioButton { Content = Strings.Android.ReportChatPornography, HorizontalAlignment = HorizontalAlignment.Stretch };
            var opt4 = new RadioButton { Content = Strings.Android.ReportChatOther, HorizontalAlignment = HorizontalAlignment.Stretch, IsChecked = true };
            var stack = new StackPanel();
            stack.Children.Add(opt1);
            stack.Children.Add(opt2);
            stack.Children.Add(opt3);
            stack.Children.Add(opt4);
            stack.Margin = new Thickness(12, 16, 12, 0);

            var dialog = new ContentDialog { Style = BootStrapper.Current.Resources["ModernContentDialogStyle"] as Style };
            dialog.Content = stack;
            dialog.Title = Strings.Android.ReportChat;
            dialog.IsPrimaryButtonEnabled = true;
            dialog.IsSecondaryButtonEnabled = true;
            dialog.PrimaryButtonText = Strings.Android.OK;
            dialog.SecondaryButtonText = Strings.Android.Cancel;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var reason = opt1.IsChecked == true
                ? new TLInputReportReasonSpam()
                : (opt2.IsChecked == true
                    ? new TLInputReportReasonViolence()
                    : (opt3.IsChecked == true
                        ? new TLInputReportReasonPornography()
                        : (TLReportReasonBase)new TLInputReportReasonOther()));

            if (reason is TLInputReportReasonOther other)
            {
                var input = new InputDialog();
                input.Title = Strings.Android.ReportChat;
                input.PlaceholderText = Strings.Android.ReportChatDescription;
                input.IsPrimaryButtonEnabled = true;
                input.IsSecondaryButtonEnabled = true;
                input.PrimaryButtonText = Strings.Android.OK;
                input.SecondaryButtonText = Strings.Android.Cancel;

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

            var result = await ProtoService.ReportPeerAsync(peer, reason);
            if (result.IsSucceeded && result.Result)
            {
                //await new TLMessageDialog("Resources.ReportSpamNotification", "Unigram").ShowQueuedAsync();
            }
        }

        #endregion
    }

    public class MessageCollection : ObservableCollection<TLMessageBase>
    {
        public static void ProcessReplies(IList<TLMessageBase> items)
        {
            for (int index = 0; index < items.Count; index++)
            {
                var item = items[index];

                var next = index > 0 ? items[index - 1] : null;
                var previous = index < items.Count - 1 ? items[index + 1] : null;

                //UpdateSeparatorOnInsert(items, item, next, index - 1);
                //UpdateSeparatorOnInsert(items, previous, item, index);

                UpdateAttach(next, item, index + 1);
                UpdateAttach(item, previous, index);
            }
        }

        protected override void InsertItem(int index, TLMessageBase item)
        {
            base.InsertItem(index, item);

            var previous = index > 0 ? this[index - 1] : null;
            var next = index < Count - 1 ? this[index + 1] : null;

            UpdateSeparatorOnInsert(item, next, index);
            UpdateSeparatorOnInsert(previous, item, index - 1);

            UpdateAttach(next, item, index + 1);
            UpdateAttach(item, previous, index);
        }

        protected override void RemoveItem(int index)
        {
            var next = index > 0 ? this[index - 1] : null;
            var previous = index < Count - 1 ? this[index + 1] : null;

            UpdateAttach(previous, next, index + 1);

            base.RemoveItem(index);

            UpdateSeparatorOnRemove(next, previous, index);
        }

        private static TLMessageService ShouldUpdateSeparatorOnInsert(TLMessageBase item, TLMessageBase previous, int index)
        {
            if (item != null && previous != null)
            {
                var itemDate = Utils.UnixTimestampToDateTime(item.Date);
                var previousDate = Utils.UnixTimestampToDateTime(previous.Date);
                if (previousDate.Date != itemDate.Date)
                {
                    var timestamp = (int)Utils.DateTimeToUnixTimestamp(previousDate.Date);
                    var service = new TLMessageService
                    {
                        Date = timestamp,
                        FromId = SettingsHelper.UserId,
                        HasFromId = true,
                        Action = new TLMessageActionDate
                        {
                            Date = timestamp
                        }
                    };

                    //base.InsertItem(index + 1, service);
                    return service;
                }
            }

            return null;
        }

        private void UpdateSeparatorOnInsert(TLMessageBase item, TLMessageBase previous, int index)
        {
            var service = ShouldUpdateSeparatorOnInsert(item, previous, index);
            if (service != null)
            {
                base.InsertItem(index + 1, service);
            }
        }

        private static void UpdateSeparatorOnInsert(IList<TLMessageBase> items, TLMessageBase item, TLMessageBase previous, int index)
        {
            var service = ShouldUpdateSeparatorOnInsert(item, previous, index);
            if (service != null)
            {
                items.Insert(index + 1, service);
            }
        }

        private static bool ShouldUpdateSeparatorOnRemove(TLMessageBase next, TLMessageBase previous, int index)
        {
            if (next is TLMessageService && previous != null)
            {
                var action = ((TLMessageService)next).Action as TLMessageActionDate;
                if (action != null)
                {
                    var itemDate = Utils.UnixTimestampToDateTime(action.Date);
                    var previousDate = Utils.UnixTimestampToDateTime(previous.Date);
                    if (previousDate.Date != itemDate.Date)
                    {
                        //base.RemoveItem(index - 1);
                        return true;
                    }
                }
            }
            else if (next is TLMessageService && previous == null)
            {
                var action = ((TLMessageService)next).Action as TLMessageActionDate;
                if (action != null)
                {
                    //base.RemoveItem(index - 1);
                    return true;
                }
            }

            return false;
        }

        private void UpdateSeparatorOnRemove(TLMessageBase next, TLMessageBase previous, int index)
        {
            if (ShouldUpdateSeparatorOnRemove(next, previous, index))
            {
                base.RemoveItem(index - 1);
            }
        }

        private static void UpdateSeparatorOnRemove(IList<TLMessageBase> items, TLMessageBase next, TLMessageBase previous, int index)
        {
            if (ShouldUpdateSeparatorOnRemove(next, previous, index))
            {
                items.RemoveAt(index - 1);
            }
        }

        private static void UpdateAttach(TLMessageBase item, TLMessageBase previous, int index)
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

            var itemPost = item is TLMessage && ((TLMessage)item).IsPost;

            if (!itemPost)
            {
                var attach = false;
                if (previous != null)
                {
                    var previousPost = previous is TLMessage && ((TLMessage)previous).IsPost;

                    attach = !previousPost &&
                             !(previous is TLMessageService && !(((TLMessageService)previous).Action is TLMessageActionPhoneCall)) &&
                             !(previous.IsService()) &&
                             !(previous is TLMessageEmpty) &&
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

        private static bool AreTogether(TLMessageBase item1, TLMessageBase item2)
        {
            if (item1 is TLMessage message1 && item2 is TLMessage message2)
            {
                var saved1 = message1.IsSaved();
                var saved2 = message2.IsSaved();
                if (saved1 && saved2)
                {
                    return message1.FwdFrom.FromId == message2.FwdFrom.FromId &&
                           message1.FwdFrom.SavedFromPeer.Equals(message2.FwdFrom.SavedFromPeer);
                }
                else if (saved1 || saved2)
                {
                    return false;
                }
            }

            return item1.FromId == item2.FromId;
        }

        public void RaiseCollectionChanged(NotifyCollectionChangedEventArgs args)
        {
            if (args.Action == NotifyCollectionChangedAction.Move)
            {
                var index = args.NewStartingIndex;
                var item = args.NewItems[0] as TLMessageBase;

                var previous = index > 0 ? this[index - 1] : null;
                var next = index < Count - 1 ? this[index + 1] : null;

                if (args.NewStartingIndex != args.OldStartingIndex)
                {
                    UpdateSeparatorOnInsert(item, next, index);
                    UpdateSeparatorOnInsert(previous, item, index - 1);
                }

                UpdateAttach(next, item, index + 1);
                UpdateAttach(item, previous, index);
            }

            OnCollectionChanged(args);
        }
    }

    public class TLMessageMediaGroup : TLMessageMediaBase, ITLMessageMediaCaption
    {
        public TLMessageMediaGroup()
        {
            Layout = new GroupedMessages();
        }

        public String Caption { get; set; }
        public GroupedMessages Layout { get; private set; }
    }

    public class TLUserCommand
    {
        public TLUser User { get; set; }
        public TLBotCommand Item { get; set; }
    }
}
