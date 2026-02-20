//
// Copyright (c) Fela Ameghino 2015-2026
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
using Telegram.Common;
using Telegram.Common.Chats;
using Telegram.Controls;
using Telegram.Controls.Chats;
using Telegram.Controls.Messages;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels.Chats;
using Telegram.ViewModels.Delegates;
using Telegram.ViewModels.Supergroups;
using Telegram.Views;
using Telegram.Views.Chats.Popups;
using Telegram.Views.Popups;
using Telegram.Views.Premium.Popups;
using Telegram.Views.Supergroups.Popups;
using Telegram.Views.Users;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels
{
    public class MessagesLoadedEventArgs : EventArgs
    {
        public PanelScrollingDirection Direction { get; init; }

        public MessagesLoadedEventArgs(PanelScrollingDirection direction)
        {
            Direction = direction;
        }
    }

    public partial class ChatMessageTopic : IEquatable<ChatMessageTopic>
    {
        public ChatMessageTopic(long chatId, MessageTopic messageTopic)
        {
            ChatId = chatId;
            MessageTopic = messageTopic;
        }

        public long ChatId { get; }

        public MessageTopic MessageTopic { get; }

        public bool Equals(ChatMessageTopic other)
        {
            if (other == null) return false;
            return ChatId == other.ChatId && MessageTopic.AreTheSame(other.MessageTopic);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ChatMessageTopic);
        }

        public override int GetHashCode()
        {
            long messageTopicId = MessageTopic switch
            {
                MessageTopicDirectMessages directMessages => directMessages.DirectMessagesChatTopicId,
                MessageTopicForum forum => forum.ForumTopicId,
                MessageTopicSavedMessages savedMessages => savedMessages.SavedMessagesTopicId,
                MessageTopicThread thread => thread.MessageThreadId,
                _ => 0
            };

            return HashCode.Combine(ChatId, messageTopicId);
        }

        public override string ToString()
        {
            return $"ChatId: {ChatId}, MessageTopic: {MessageTopic}";
        }
    }

    public partial class ChatBusinessRepliesIdNavigationArgs
    {
        public ChatBusinessRepliesIdNavigationArgs(string quickReplyShortcut)
        {
            QuickReplyShortcut = quickReplyShortcut;
        }

        public string QuickReplyShortcut { get; }
    }

    public partial class DialogViewModel : ComposeViewModel, IDelegable<IDialogDelegate>
    {
        private readonly ConcurrentDictionary<long, MessageViewModel> _selectedItems = new();
        public IDictionary<long, MessageViewModel> SelectedItems => _selectedItems;

        public int SelectedCount => SelectedItems.Count;

        protected readonly ConcurrentDictionary<long, MessageViewModel> _groupedMessages = new();
        protected readonly ConcurrentDictionary<long, HashSet<long>> _messageEffects = new();

        protected static readonly Dictionary<MessageId, MessageContent> _contentOverrides = new();

        protected Dictionary<long, DialogPendingTextMessage> _pendingTextMessages = new();

        protected readonly IMessageDelegate _messageDelegate;
        protected readonly WeakReference _messageDelegateWeak;

        protected readonly ILocationService _locationService;
        protected readonly INotificationsService _notificationsService;
        protected readonly IVoipService _voipService;
        protected readonly INetworkService _networkService;
        protected readonly IStorageService _storageService;
        protected readonly ITranslateService _translateService;

        public IStorageService StorageService => _storageService;

        public ITranslateService TranslateService => _translateService;

        public IVoipService VoipService => _voipService;

        public DialogUnreadMessagesViewModel Mentions { get; }
        public DialogUnreadMessagesViewModel Reactions { get; }

        public DialogPinnedMessagesViewModel PinnedMessages { get; }

        public IDialogDelegate Delegate { get; set; }

        public DialogViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, ILocationService locationService, INotificationsService pushService, IVoipService voipService, INetworkService networkService, IStorageService storageService, ITranslateService translateService)
            : base(clientService, settingsService, aggregator)
        {
            _locationService = locationService;
            _notificationsService = pushService;
            _voipService = voipService;
            _networkService = networkService;
            _storageService = storageService;
            _translateService = translateService;

            _messageDelegate = new DialogMessageDelegate(this);
            _messageDelegateWeak = new WeakReference(_messageDelegate);

            Mentions = new DialogUnreadMessagesViewModel(this, new SearchMessagesFilterUnreadMention());
            Reactions = new DialogUnreadMessagesViewModel(this, new SearchMessagesFilterUnreadReaction());

            PinnedMessages = new DialogPinnedMessagesViewModel(this);

            Items = new MessageCollection(this);

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

        public bool IsSavedMessagesTab { get; set; }

        protected Chat _linkedChat;
        public Chat LinkedChat
        {
            get => _linkedChat;
            set => Set(ref _linkedChat, value);
        }

        public override MessageTopic TopicId { get; set; }

        public override MessageTopic OutgoingTopicId
        {
            get
            {
                if (DirectMessagesChatTopic != null)
                {
                    return new MessageTopicDirectMessages(DirectMessagesChatTopic.Id);
                }
                else if (ForumTopic != null)
                {
                    return new MessageTopicForum(ForumTopic.Info.ForumTopicId);
                }
                else if (Thread != null)
                {
                    return new MessageTopicThread(Thread.MessageThreadId);
                }
                else if (IsForum && _chat.LastMessage != null && _chat.LastMessage.TopicId is MessageTopicForum topicForum)
                {
                    //if (topicForum.ForumTopicId != ForumTopicService.GeneralId)
                    //{
                    //    return _chat.LastMessage.MessageThreadId;
                    //}

                    return topicForum;
                }

                return null;
            }
        }

        public override long ThreadId
        {
            get
            {
                if (_directMessagesChatTopic != null)
                {
                    return _directMessagesChatTopic.Id;
                }
                else if (_forumTopic != null)
                {
                    return _forumTopic.Info.ForumTopicId << 20;
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

        protected ForumTopic _forumTopic;
        public ForumTopic ForumTopic
        {
            get => _forumTopic;
            set => Set(ref _forumTopic, value);
        }

        protected SavedMessagesTopic _savedMessagesTopic;
        public SavedMessagesTopic SavedMessagesTopic
        {
            get => _savedMessagesTopic;
            set => Set(ref _savedMessagesTopic, value);
        }

        protected DirectMessagesChatTopic _directMessagesChatTopic;
        public DirectMessagesChatTopic DirectMessagesChatTopic
        {
            get => _directMessagesChatTopic;
            set => Set(ref _directMessagesChatTopic, value);
        }

        protected QuickReplyShortcut _quickReplyShortcut;
        public QuickReplyShortcut QuickReplyShortcut
        {
            get => _quickReplyShortcut;
            set => Set(ref _quickReplyShortcut, value);
        }

        public long SavedMessagesTopicId => SavedMessagesTopic?.Id ?? 0;

        public long ChatId => Chat?.Id ?? 0;

        public bool IsForum { get; private set; }

        public bool IsDirectMessagesGroup { get; private set; }

        public bool HasProtectedContent => Chat?.Type is ChatTypeSecret || (Chat?.HasProtectedContent ?? false);

        protected Chat _chat;
        public override Chat Chat
        {
            get => _chat;
            set => Set(ref _chat, value);
        }

        private DialogType _type => Type;
        public virtual DialogType Type { get; private set; } = DialogType.History;

        private DispatcherTimer _lastSeenTimer;

        private string _lastSeen;
        public string LastSeen
        {
            get
            {
                if (Type == DialogType.History)
                {
                    return IsDirectMessagesGroup ? Strings.ChatMessageSuggestions : _lastSeen;
                }
                else if (Type == DialogType.Thread)
                {
                    if (SavedMessagesTopic != null)
                    {
                        return Strings.SavedMessagesTab;
                    }
                }
                else if (Type == DialogType.EventLog)
                {
                    return Strings.EventLog;
                }
                else if (Type == DialogType.Pinned)
                {
                    if (MessagesCount > 0)
                    {
                        return Locale.Declension(Strings.R.PinnedMessagesCount, MessagesCount);
                    }

                    return Strings.PinnedMessages;
                }

                return _lastSeen;
            }
            set
            {
                Set(ref _lastSeen, value);
                RaisePropertyChanged(nameof(Subtitle));
            }
        }

        public void UpdateLastSeen(string value)
        {
            _lastSeenTimer?.Stop();
            LastSeen = value;
        }

        public void UpdateLastSeen(User user)
        {
            var interval = LastSeenConverter.OnlinePhraseChange(user.Status, DateTime.Now);
            if (interval > 0 && _lastSeenTimer == null)
            {
                _lastSeenTimer ??= new DispatcherTimer();
                _lastSeenTimer.Tick += LastSeenTimer_Tick;
            }

            _lastSeenTimer?.Stop();

            if (interval > 0)
            {
                _lastSeenTimer.Interval = TimeSpan.FromSeconds(interval);
                _lastSeenTimer.Start();
            }

            LastSeen = LastSeenConverter.GetLabel(user, true, true);
        }

        private void LastSeenTimer_Tick(object sender, object e)
        {
            if (ClientService.TryGetUser(Chat, out User user))
            {
                UpdateLastSeen(user);
            }
        }

        private string _onlineCount;
        public string OnlineCount
        {
            get => _onlineCount;
            set
            {
                Set(ref _onlineCount, value);
                RaisePropertyChanged(nameof(Subtitle));
            }
        }

        private int _messagesCount;
        public int MessagesCount
        {
            get => _messagesCount;
            set => Set(ref _messagesCount, value);
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
                    return LastSeen;
                }

                if (ForumTopic == null && !string.IsNullOrEmpty(OnlineCount) && !string.IsNullOrEmpty(LastSeen))
                {
                    return string.Format("{0}, {1}", LastSeen, OnlineCount);
                }

                return LastSeen;
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

        private bool _restrictsNewChats;
        public bool RestrictsNewChats
        {
            get => _restrictsNewChats;
            set => Set(ref _restrictsNewChats, value);
        }

        private DispatcherTimer _informativeTimer;

        private OutputChatActionManager _chatActionManager;
        public OutputChatActionManager ChatActionManager
        {
            get
            {
                return _chatActionManager ??= new OutputChatActionManager(ClientService, _chat, OutgoingTopicId);
            }
        }

        private bool _needsUpdateSpeechRecognitionTrial;

        private Td.Api.Chats _groupsInCommon;
        public Td.Api.Chats GroupsInCommon
        {
            get => _groupsInCommon;
            set => Set(ref _groupsInCommon, value);
        }

        private SponsoredMessage _sponsoredMessage;
        public SponsoredMessage SponsoredMessage
        {
            get => _sponsoredMessage;
            set => Set(ref _sponsoredMessage, value);
        }

        private SponsoredMessage _pendingSponsoredMessage;
        public SponsoredMessage PendingSponsoredMessage
        {
            get => _pendingSponsoredMessage;
            set => Set(ref _pendingSponsoredMessage, value);
        }

        private SavedMessagesTags _savedMessagesTags;
        public SavedMessagesTags SavedMessagesTags
        {
            get => _savedMessagesTags;
            set => Set(ref _savedMessagesTags, value);
        }

        public void UpdateSavedMessagesTag(ReactionType tag, bool filterByTag, ReactionType lastTag)
        {
            bool TryGetLastVisibleMessageIdAndPixel(out long lastVisibleId, out double? lastVisiblePixel)
            {
                lastVisibleId = 0;
                lastVisiblePixel = null;

                var field = HistoryField;
                if (field != null && !field.IsSuspended && TryGetLastVisibleMessageId(out lastVisibleId, out int lastVisibleIndex))
                {
                    if (lastVisibleId != 0)
                    {
                        var message = Items[lastVisibleIndex];
                        if (message.InteractionInfo?.Reactions != null && message.InteractionInfo.Reactions.IsChosen(lastTag))
                        {
                            var container = field.ContainerFromIndex(lastVisibleIndex) as SelectorItem;
                            if (container != null)
                            {
                                var transform = container.TransformToVisual(field);
                                var position = transform.TransformPoint(new Point());

                                lastVisiblePixel = field.ActualHeight - (position.Y + container.ActualHeight);
                            }

                            return true;
                        }
                    }
                }

                return false;
            }

            if (lastTag != null && TryGetLastVisibleMessageIdAndPixel(out long lastVisibleId, out double? lastVisiblePixel))
            {
                _ = LoadMessageSliceAsync(null, lastVisibleId, VerticalAlignment.Bottom, lastVisiblePixel, onlyRemote: true);
            }
            else
            {
                _ = LoadMessageSliceAsync(null, long.MaxValue, VerticalAlignment.Bottom, onlyRemote: true);
            }

            if (_chat is Chat chat && chat.Type is ChatTypePrivate privata)
            {
                var item = ClientService.GetUser(privata.UserId);
                var cache = ClientService.GetUserFull(privata.UserId);

                if (cache != null)
                {
                    Delegate?.UpdateUser(chat, item, cache, false, false);
                }
                else
                {
                    ClientService.Send(new GetUserFullInfo(privata.UserId));
                }
            }

            if (filterByTag is false && tag != null)
            {
                Search?.Search(Search.Query, null, tag);
            }
        }

        public int UnreadCount
        {
            get
            {
                if (Type != DialogType.History || IsDirectMessagesGroup)
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
            set => ShowHideSelection(value);
        }

        public void ShowHideSelection(bool value, ReportChatSelection report = null)
        {
            if (_isSelectionEnabled != value)
            {
                Set(ref _isReportingMessages, report, nameof(IsReportingMessages));
                Set(ref _isSelectionEnabled, value, nameof(IsSelectionEnabled));

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

        public override FormattedText GetFormattedText(bool clear = false, bool parseMarkdown = true)
        {
            try
            {
                var field = TextField;
                if (field == null)
                {
                    return string.Empty.AsFormattedText();
                }

                return field.GetFormattedText(clear, parseMarkdown);
            }
            catch
            {
                return string.Empty.AsFormattedText();
            }
        }

        public bool IsEndReached()
        {
            return IsEndReached(Items);
        }

        public bool IsEndReached(IList<MessageViewModel> messages)
        {
            var lastMessage = _savedMessagesTopic?.LastMessage ?? _forumTopic?.LastMessage ?? _chat?.LastMessage;
            if (lastMessage == null)
            {
                return messages.Empty();
            }

            var last = messages.LastOrDefault();
            if (last?.Content is MessageAlbum album)
            {
                last = album.Messages.LastOrDefault();
            }

            if (last == null || last.Id == 0)
            {
                return true;
            }

            return lastMessage.Id == last.Id;
        }

        private bool _isChatEmpty;

        private Sticker _greetingSticker;
        public Sticker GreetingSticker
        {
            get => _greetingSticker;
            set => Set(ref _greetingSticker, value);
        }

        public bool HasMoreItemsAtTop => IsSavedMessagesTab ? IsNewestSliceLoaded != true : IsOldestSliceLoaded != true;

        private bool? _isNewestSliceLoaded;
        public bool? IsNewestSliceLoaded
        {
            get => _isNewestSliceLoaded;
            set => Set(ref _isNewestSliceLoaded, value);
        }

        public bool HasMoreItemsAtBottom => IsSavedMessagesTab ? IsOldestSliceLoaded != true : IsNewestSliceLoaded != true;

        private bool? _isOldestSliceLoaded;
        public bool? IsOldestSliceLoaded
        {
            get => _isOldestSliceLoaded;
            set => Set(ref _isOldestSliceLoaded, value);
        }

        public bool HasUnreadMessages { get; private set; }

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

        protected Stack<long> _repliesStack = new();

        public Stack<long> RepliesStack => _repliesStack;

        public virtual async Task LoadNextSliceAsync(PanelScrollingDirection direction)
        {
            // Backward => Going to top, to the past
            // Forward => Going to bottom, to the present

            if (IsSavedMessagesTab)
            {
                direction = direction == PanelScrollingDirection.Forward
                    ? PanelScrollingDirection.Backward
                    : PanelScrollingDirection.Forward;
            }

            if (Type is not DialogType.History and not DialogType.Thread and not DialogType.Pinned)
            {
                return;
            }

            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (_loadingSlice || _chat?.Id != chat.Id || Items.Count < 1)
            {
                return;
            }

            if (direction == PanelScrollingDirection.Backward && IsOldestSliceLoaded == true)
            {
                return;
            }

            _loadingSlice = true;
            IsLoading = true;

            System.Diagnostics.Debug.WriteLine("DialogViewModel: LoadNextSliceAsync");

            MessageViewModel fromMessage;
            long fromMessageId;
            int offset;

            if (direction == PanelScrollingDirection.Backward)
            {
                fromMessage = Items.Count > 0 ? Items[0] : null;
                fromMessageId = Items.FirstId;
                offset = 0;
            }
            else
            {
                fromMessage = null;
                fromMessageId = Items.LastId;
                offset = -Constants.HistoryLimit;
            }

            if (fromMessageId == long.MaxValue || fromMessageId == long.MinValue)
            {
                _loadingSlice = false;
                IsLoading = false;

                return;
            }

            Function func;
            if (Type == DialogType.Pinned)
            {
                func = new SearchChatMessages(chat.Id, TopicId, string.Empty, null, fromMessageId, offset, Constants.HistoryLimit, new SearchMessagesFilterPinned());
            }
            else if (Search?.SavedMessagesTag != null)
            {
                func = new SearchSavedMessages(SavedMessagesTopicId, Search.SavedMessagesTag, string.Empty, fromMessageId, offset, Constants.HistoryLimit);
            }
            else if (SavedMessagesTopic != null)
            {
                func = new GetSavedMessagesTopicHistory(SavedMessagesTopic.Id, fromMessageId, offset, Constants.HistoryLimit);
            }
            else if (DirectMessagesChatTopic != null)
            {
                func = new GetDirectMessagesChatTopicHistory(chat.Id, DirectMessagesChatTopic.Id, fromMessageId, offset, Constants.HistoryLimit);
            }
            else if (ForumTopic != null)
            {
                func = new GetForumTopicHistory(chat.Id, _forumTopic.Info.ForumTopicId, fromMessageId, offset, Constants.HistoryLimit);
            }
            else if (Thread != null)
            {
                func = new GetMessageThreadHistory(chat.Id, _thread.MessageThreadId, fromMessageId, offset, Constants.HistoryLimit);
            }
            else
            {
                func = new GetChatHistory(chat.Id, fromMessageId, offset, Constants.HistoryLimit, false);
            }

            var tsc = new TaskCompletionSource<MessageCollection>();
            async void handler(Object result)
            {
                if (result is FoundChatMessages foundChatMessages)
                {
                    result = await PreloadAlbumsAsync(chat.Id, foundChatMessages);
                }

                if (result is Messages messages)
                {
                    if (messages.MessagesValue.Count > 0 && messages.MessagesValue[^1].Content is MessageForumTopicCreated)
                    {
                        messages.MessagesValue.RemoveAt(messages.MessagesValue.Count - 1);
                    }

                    var endReached = messages.MessagesValue.Empty();
                    if (endReached && direction == PanelScrollingDirection.Backward)
                    {
                        await AddHeaderAsync(messages.MessagesValue, fromMessage?.Get());
                    }

                    tsc.SetResult(new MessageCollection(this, Items.Ids, messages.MessagesValue, endReached, Type));
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

                    if (Items.Count > 200)
                    {
                        if (direction == PanelScrollingDirection.Backward)
                        {
                            IsNewestSliceLoaded = false;
                        }
                        else
                        {
                            IsOldestSliceLoaded = false;
                        }

                        while (Items.Count > 200)
                        {
                            Items.RemoveAt(direction == PanelScrollingDirection.Backward ? Items.Count - 1 : 0);
                        }
                    }
                }
                else if (direction != PanelScrollingDirection.Backward)
                {
                    SetScrollMode(ItemsUpdatingScrollMode.KeepLastItemInView, true);
                }

                if (direction == PanelScrollingDirection.Backward)
                {
                    IsOldestSliceLoaded = replied.IsEndReached;
                    UpdateDetectedLanguage();
                }
                else
                {
                    IsNewestSliceLoaded = replied.IsEndReached || IsEndReached();
                }
            }

            _loadingSlice = false;
            IsLoading = false;
            MessagesLoaded?.Invoke(this, new MessagesLoadedEventArgs(direction));

            PinnedMessages.LoadSlice(fromMessageId, direction);
        }

        protected async Task AddHeaderAsync(IList<Message> messages, Message previous)
        {
            if (previous != null && (previous.Content is MessageHeaderDate || (previous.Content is MessageText && previous.Id == 0)))
            {
                return;
            }

            var chat = _chat;
            if (chat == null || Type != DialogType.History)
            {
                goto AddDate;
            }

            var user = ClientService.GetUser(chat);
            if (user?.Type is not UserTypeBot)
            {
                if (user != null && chat.ActionBar is ChatActionBarReportAddBlock reportAddBlock && reportAddBlock.AccountInfo != null)
                {
                    var fullInfo = ClientService.GetUserFull(user.Id);
                    fullInfo ??= await ClientService.SendAsync(new GetUserFullInfo(user.Id)) as UserFullInfo;

                    messages.Add(new Message(0, new MessageSenderUser(user.Id), chat.Id, null, null, false, false, false, false, false, false, false, false, false, 0, 0, null, null, null, null, null, null, null, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, 0, null, string.Empty, new MessageHeaderAccountInfo(), null));
                    return;
                }

                goto AddDate;
            }

            if (chat.Id == ClientService.Options.VerificationCodesBotChatId)
            {
                var entities = ClientEx.GetTextEntities(Strings.VerifyChatInfo);
                var text = new FormattedText(Strings.VerifyChatInfo, entities);

                var content = new MessageText(text, null, null);

                messages.Add(new Message(0, new MessageSenderUser(user.Id), chat.Id, null, null, false, false, false, false, false, false, false, false, false, 0, 0, null, null, null, null, null, null, null, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, 0, null, string.Empty, content, null));
                return;
            }
            else
            {
                MessageContent content = null;
                if (chat.Id == ClientService.Options.RepliesBotChatId)
                {
                    content = new MessageText(Strings.RepliesChatInfo.AsFormattedText(), null, null);
                }
                else if (chat.Id == ClientService.Options.VerificationCodesBotChatId)
                {
                    content = new MessageText(Strings.VerifyChatInfo.AsFormattedText(), null, null);
                }
                else
                {
                    var fullInfo = ClientService.GetUserFull(user.Id);
                    fullInfo ??= await ClientService.SendAsync(new GetUserFullInfo(user.Id)) as UserFullInfo;

                    if (fullInfo?.BotInfo?.Description.Length > 0)
                    {
                        var entities = ClientEx.GetTextEntities(fullInfo.BotInfo.Description);

                        foreach (var entity in entities)
                        {
                            entity.Offset += Strings.BotInfoTitle.Length + 1;
                        }

                        entities.Add(new TextEntity(0, Strings.BotInfoTitle.Length, new TextEntityTypeBold()));

                        var message = $"{Strings.BotInfoTitle}\n{fullInfo.BotInfo.Description}";
                        var text = new FormattedText(message, entities);

                        if (fullInfo.BotInfo.Animation != null)
                        {
                            content = new MessageAnimation(fullInfo.BotInfo.Animation, text, false, false, false);
                        }
                        else if (fullInfo.BotInfo.Photo != null)
                        {
                            content = new MessagePhoto(fullInfo.BotInfo.Photo, text, false, false, false);
                        }
                        else
                        {
                            content = new MessageText(text, null, null);
                        }
                    }
                }

                if (content != null)
                {
                    messages.Add(new Message(0, new MessageSenderUser(user.Id), chat.Id, null, null, false, false, false, false, false, false, false, false, false, 0, 0, null, null, null, null, null, null, null, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, 0, null, string.Empty, content, null));
                    return;
                }
            }

        AddDate:
            if (_forumTopic == null && _thread != null)
            {
                var replied = _thread.Messages.OrderBy(x => x.Id).ToList();
                var empty = previous == null;

                previous = replied[0];

                if (empty)
                {
                    messages.Add(new Message(0, previous.SenderId, previous.ChatId, null, null, previous.IsOutgoing, false, false, false, false, previous.IsChannelPost, false, false, false, previous.Date, 0, null, null, null, null, null, null, null, previous.TopicId, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, 0, null, string.Empty, new MessageCustomServiceAction(Strings.NoComments), null));
                }
                else
                {
                    messages.Add(new Message(0, previous.SenderId, previous.ChatId, null, null, previous.IsOutgoing, false, false, false, false, previous.IsChannelPost, false, false, false, previous.Date, 0, null, null, null, null, null, null, null, previous.TopicId, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, 0, null, string.Empty, new MessageCustomServiceAction(Strings.DiscussionStarted), null));
                }

                for (int i = replied.Count - 1; i >= 0; i--)
                {
                    messages.Add(replied[i]);
                }
            }

            if (previous != null && !IsSavedMessagesTab)
            {
                messages.Add(new Message(0, previous.SenderId, previous.ChatId, null, null, previous.IsOutgoing, false, false, false, false, previous.IsChannelPost, false, false, false, previous.Date, 0, null, null, null, null, null, null, null, previous.TopicId, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, 0, null, string.Empty, new MessageHeaderDate(previous.Date), null));
            }
        }

        public async void PreviousSlice()
        {
            if (Type is DialogType.ScheduledMessages or DialogType.EventLog)
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

            PinnedMessages.SetLocked(0);

            var details = GetCurrentDetails();

            if (TryGetLastVisibleMessageId(out long lastVisibleId, out int lastVisibleIndex))
            {
                // Find first valid non-visible message
                var firstNonVisibleId = lastVisibleId;

                for (int i = lastVisibleIndex + 1; i < Items.Count; i++)
                {
                    var message = Items[i].Id;
                    if (message != 0)
                    {
                        firstNonVisibleId = message;
                        break;
                    }
                }

                if (firstNonVisibleId != 0 && firstNonVisibleId < details.LastReadInboxMessageId)
                {
                    return LoadMessageSliceAsync(null, details.LastReadInboxMessageId, VerticalAlignment.Top, disableAnimation: false);
                }
            }

            return LoadMessageSliceAsync(null, details.LastMessageId, VerticalAlignment.Bottom, disableAnimation: false);
        }

        private bool TryGetLastVisibleMessageId(out long id, out int index)
        {
            var field = HistoryField;
            if (field != null)
            {
                var panel = field.ItemsPanelRoot as ItemsStackPanel;
                if (panel != null && panel.LastVisibleIndex >= 0 && panel.LastVisibleIndex < Items.Count && Items.Count > 0)
                {
                    for (int i = panel.LastVisibleIndex; i >= panel.FirstVisibleIndex; i--)
                    {
                        var item = Items[i];
                        if (item.Id == 0)
                        {
                            continue;
                        }

                        if (item.Content is MessageAlbum album)
                        {
                            item = album.Messages.LastOrDefault();
                        }

                        id = item.Id;
                        index = i;
                        return true;
                    }
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
                    for (int i = panel.FirstVisibleIndex; i <= panel.LastVisibleIndex; i++)
                    {
                        var item = Items[i];
                        if (item.Id == 0)
                        {
                            continue;
                        }

                        if (item.Content is MessageAlbum album)
                        {
                            item = album.Messages.FirstOrDefault();
                        }

                        id = item.Id;
                        return true;
                    }
                }
            }

            id = -1;
            return false;
        }

        // This is to notify the view to update bindings
        public event EventHandler Initialized;

        public event EventHandler<MessagesLoadedEventArgs> MessagesLoaded;

        protected void NotifyInitialized()
        {
            Initialized?.Invoke(this, EventArgs.Empty);
            Initialized = null;
        }

        public async Task LoadMessageSliceAsync(long? previousId, long fromMessageId, VerticalAlignment alignment = VerticalAlignment.Center, double? pixel = null, ScrollIntoViewAlignment? direction = null, bool? disableAnimation = null, TextQuote highlight = null, int checklistTaskId = 0, int fromDateOffset = 0, bool onlyRemote = false)
        {
            if (Type is not DialogType.History and not DialogType.Thread and not DialogType.Pinned)
            {
                NotifyInitialized();
                return;
            }
            else if (Type is DialogType.Thread)
            {
                NotifyInitialized();
            }

            var chat = _chat;
            if (chat == null)
            {
                NotifyInitialized();
                return;
            }

            if (direction == null && TryGetFirstVisibleMessageId(out long firstVisibleId))
            {
                direction = firstVisibleId < fromMessageId ? ScrollIntoViewAlignment.Default : ScrollIntoViewAlignment.Leading;
            }

            if (alignment == VerticalAlignment.Bottom && pixel == null)
            {
                pixel = int.MaxValue;
            }

            if (onlyRemote is false && Items.TryGetValue(fromMessageId, out MessageViewModel already))
            {
                if (alignment == VerticalAlignment.Center)
                {
                    var index = Items.IndexOf(already);
                    var needNextSlice = index < 25 || Items.Count - index < 25;

                    if (needNextSlice)
                    {
                        if (direction == ScrollIntoViewAlignment.Leading)
                        {
                            await LoadNextSliceAsync(PanelScrollingDirection.Forward);
                        }
                        else
                        {
                            await LoadNextSliceAsync(PanelScrollingDirection.Backward);
                        }
                    }
                }
                else if (alignment == VerticalAlignment.Top && !onlyRemote)
                {
                    var details = GetCurrentDetails();

                    // If we're loading the last message and it has been read already
                    // then we want to align it at bottom, as it might be taller than the window height
                    if (fromMessageId == details.LastMessageId)
                    {
                        alignment = VerticalAlignment.Bottom;
                        pixel = null;
                    }
                }

                HistoryField?.ScrollToItem(already, alignment, alignment == VerticalAlignment.Center ? new MessageBubbleHighlightOptions(fromMessageId, highlight, checklistTaskId) : null, pixel, direction ?? ScrollIntoViewAlignment.Leading, disableAnimation);

                if (previousId.HasValue && !_repliesStack.Contains(previousId.Value))
                {
                    _repliesStack.Push(previousId.Value);
                }

                NotifyInitialized();
                return;
            }

            var loadMore = PanelScrollingDirection.None;

            if (_loadingSlice || _chat?.Id != chat.Id)
            {
                NotifyInitialized();
                return;
            }

            _loadingSlice = true;
            IsOldestSliceLoaded = null;
            IsNewestSliceLoaded = null;
            IsLoading = true;

            System.Diagnostics.Debug.WriteLine("DialogViewModel: LoadMessageSliceAsync");

            var func = Task.Run(() => LoadMessageSliceImpl(chat, fromMessageId, fromDateOffset, alignment, direction, pixel));

            if (alignment != VerticalAlignment.Center)
            {
                var wait = await Task.WhenAny(func, Task.Delay(200));
                if (wait != func)
                {
                    NotifyInitialized();
                }
            }

            var response = await func;
            if (response is LoadSliceResult slice)
            {
                _groupedMessages.Clear();

                fromMessageId = slice.FromMessageId;
                pixel = slice.Pixel;
                alignment = slice.Alignment;

                SetScrollMode(slice.ScrollMode, true);

                var messages = slice.Items;
                var endReached = IsEndReached(messages);

                ProcessMessages(chat, messages);

                if (endReached && messages.Count > 0)
                {
                    var previous = messages[^1];

                    if (IsSavedMessagesTab)
                    {
                        messages.Add(CreateMessage(new Message(0, previous.SenderId, previous.ChatId, null, null, previous.IsOutgoing, false, false, false, false, previous.IsChannelPost, false, false, false, previous.Date, 0, null, null, null, null, null, null, null, previous.TopicId, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, 0, null, string.Empty, new MessageHeaderDate(previous.Date), null)));
                        messages.Add(CreateMessage(new Message(0, previous.SenderId, previous.ChatId, null, null, previous.IsOutgoing, false, false, false, false, previous.IsChannelPost, false, false, false, previous.Date, 0, null, null, null, null, null, null, null, previous.TopicId, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, 0, null, string.Empty, new MessageCustomServiceAction(Strings.SavedMessagesProfileHint), null)));
                    }
                    else if (IsForum && ForumTopic == null && chat.Type is ChatTypePrivate privata && ClientService.TryGetUser(chat, out User user) && user.Type is UserTypeBot { AllowsUsersToCreateTopics: true })
                    {
                        messages.Add(CreateMessage(new Message(long.MaxValue, new MessageSenderUser(privata.UserId), previous.ChatId, null, null, false, false, false, false, false, false, false, false, false, int.MaxValue, 0, null, null, null, null, null, null, null, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, 0, null, string.Empty, new MessageHeaderNewThread(), null)));
                        fromMessageId = long.MaxValue;
                    }
                }

                Items.RawReplaceWith(messages);

                MessagesCount = slice.TotalCount;
                HasUnreadMessages = slice.IsUnread;

                if (Type == DialogType.Pinned)
                {
                    RaisePropertyChanged(nameof(LastSeen));
                }

                NotifyInitialized();

                IsOldestSliceLoaded = null;
                IsNewestSliceLoaded = endReached;

                if (Items.TryGetValue(fromMessageId, out already))
                {
                    HistoryField?.ScrollToItem(already, alignment, alignment == VerticalAlignment.Center ? new MessageBubbleHighlightOptions(fromMessageId, highlight, checklistTaskId) : null, pixel, direction ?? ScrollIntoViewAlignment.Leading, disableAnimation);

                    if (previousId.HasValue && !_repliesStack.Contains(previousId.Value))
                    {
                        _repliesStack.Push(previousId.Value);
                    }
                }
                else
                {
                    //if (maxId == 0)
                    //{
                    if (_thread != null && alignment == VerticalAlignment.Top)
                    {
                        ScrollToTop();
                    }
                    else
                    {
                        ScrollToBottom();
                    }
                    //}

                    HistoryField?.Resume();
                }

                // If the response contains a single item, immediately load more in the past
                if (slice.Items.Count == 1)
                {
                    loadMore = PanelScrollingDirection.Backward;
                }

                UpdateDetectedLanguage();
            }
            else
            {
                NotifyInitialized();

                IsOldestSliceLoaded = true;
                IsNewestSliceLoaded = true;

                HistoryField?.Resume();
            }

            _loadingSlice = false;
            IsLoading = false;
            MessagesLoaded?.Invoke(this, new MessagesLoadedEventArgs(PanelScrollingDirection.None));

            PinnedMessages.LoadSlice(fromMessageId);

            if (loadMore != PanelScrollingDirection.None)
            {
                await LoadNextSliceAsync(loadMore);
            }

            await AddSponsoredMessagesAsync();
        }

        private async Task<Messages> PreloadAlbumsAsync(long chatId, FoundChatMessages foundChatMessages)
        {
            for (int i = foundChatMessages.Messages.Count - 1; i >= 0; i--)
            {
                var message = foundChatMessages.Messages[i];
                if (message.MediaAlbumId == 0)
                {
                    continue;
                }

                Function func;
                if (DirectMessagesChatTopic != null)
                {
                    func = new GetDirectMessagesChatTopicHistory(chatId, DirectMessagesChatTopic.Id, message.Id, -10, 10);
                }
                else if (ForumTopic != null)
                {
                    func = new GetForumTopicHistory(chatId, _forumTopic.Info.ForumTopicId, message.Id, -10, 10);
                }
                else
                {
                    func = new GetChatHistory(chatId, message.Id, -10, 10, false);
                }

                var response = await ClientService.SendAsync(func);
                if (response is Messages album
                    && album.MessagesValue.Count > 1
                    && album.MessagesValue[^1].Id == message.Id)
                {
                    for (int j = album.MessagesValue.Count - 2; j >= 0; j--)
                    {
                        var part = album.MessagesValue[j];
                        if (part.MediaAlbumId == message.MediaAlbumId)
                        {
                            foundChatMessages.Messages.Insert(i, album.MessagesValue[j]);
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }

            return new Messages(foundChatMessages.TotalCount, foundChatMessages.Messages);
        }

        private async Task<LoadSliceResult> LoadMessageSliceImpl(Chat chat, long fromMessageId, int fromDateOffset, VerticalAlignment alignment, ScrollIntoViewAlignment? direction, double? pixel)
        {
            fromMessageId = fromMessageId == long.MaxValue ? 0 : fromMessageId;

            Task<Object> func;
            if (Type == DialogType.Pinned)
            {
                func = ClientService.SendAsync(new SearchChatMessages(chat.Id, TopicId, string.Empty, null, fromMessageId, Constants.HistoryOffset, Constants.HistoryLimit, new SearchMessagesFilterPinned()));
            }
            else if (Search?.SavedMessagesTag != null && Search.FilterByTag)
            {
                func = ClientService.SendAsync(new SearchSavedMessages(SavedMessagesTopicId, Search.SavedMessagesTag, string.Empty, fromMessageId, Constants.HistoryOffset, Constants.HistoryLimit));
            }
            else if (SavedMessagesTopic != null)
            {
                func = ClientService.SendAsync(new GetSavedMessagesTopicHistory(SavedMessagesTopic.Id, fromMessageId, Constants.HistoryOffset, Constants.HistoryLimit));
            }
            else if (DirectMessagesChatTopic != null)
            {
                func = ClientService.SendAsync(new GetDirectMessagesChatTopicHistory(chat.Id, DirectMessagesChatTopic.Id, fromMessageId, Constants.HistoryOffset, Constants.HistoryLimit));
            }
            else if (ForumTopic != null)
            {
                func = ClientService.SendAsync(new GetForumTopicHistory(chat.Id, _forumTopic.Info.ForumTopicId, fromMessageId, Constants.HistoryOffset, Constants.HistoryLimit));
            }
            else if (Thread != null)
            {
                // MaxId == 0 means that the thread was never opened
                if (fromMessageId == 0 || Thread.Messages.Any(x => x.Id == fromMessageId))
                {
                    func = ClientService.SendAsync(new GetMessageThreadHistory(chat.Id, _thread.MessageThreadId, 1, Constants.HistoryOffset, Constants.HistoryLimit));
                }
                else
                {
                    func = ClientService.SendAsync(new GetMessageThreadHistory(chat.Id, _thread.MessageThreadId, fromMessageId, Constants.HistoryOffset, Constants.HistoryLimit));
                }
            }
            else
            {
                async Task<Object> GetChatHistoryAsync(long chatId, long fromMessageId, int offset, int limit, bool onlyLocal)
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

                func = GetChatHistoryAsync(chat.Id, fromMessageId, Constants.HistoryOffset, Constants.HistoryLimit, alignment == VerticalAlignment.Top);
            }

            var response = await func;
            if (response is FoundChatMessages foundChatMessages)
            {
                response = await PreloadAlbumsAsync(chat.Id, foundChatMessages);
            }

            if (response is Messages messages)
            {
                var firstVisibleIndex = -1;
                var firstVisibleItem = default(Message);

                var unread = false;

                if (alignment != VerticalAlignment.Center)
                {
                    var details = GetCurrentDetails();

                    bool Included(long id)
                    {
                        return true;
                        return messages.MessagesValue.Count > 0 && messages.MessagesValue[0].Id >= id && messages.MessagesValue[^1].Id <= id;
                    }

                    // If we're loading from the last read message
                    // then we want to skip it to align first unread message at top
                    if (details.LastReadInboxMessageId != 0 && details.LastReadInboxMessageId != details.LastMessageId && Included(fromMessageId) && Included(details.LastReadInboxMessageId) /*maxId >= lastReadMessageId*/)
                    {
                        var target = default(Message);
                        var index = -1;

                        for (int i = messages.MessagesValue.Count - 1; i >= 0; i--)
                        {
                            var current = messages.MessagesValue[i];
                            if (current.Id > details.LastReadInboxMessageId && current.Id < long.MaxValue)
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
                                messages.MessagesValue.Insert(index + 1, new Message(0, target.SenderId, target.ChatId, null, null, target.IsOutgoing, false, false, false, false, target.IsChannelPost, false, false, false, target.Date, 0, null, null, null, null, null, null, null, target.TopicId, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, 0, null, string.Empty, new MessageHeaderUnread(), null));
                                unread = true;
                            }
                            else if (fromMessageId == details.LastReadInboxMessageId)
                            {
                                Logger.Debug("Looking for first unread message, can't find it");
                            }

                            if (fromMessageId == details.LastReadInboxMessageId && pixel == null)
                            {
                                fromMessageId = target.Id;
                                pixel = 28 + 48;
                            }
                        }
                    }

                    if (firstVisibleItem != null && pixel == null)
                    {
                        fromMessageId = firstVisibleItem.Id;
                    }

                    // If we're loading the last message and it has been read already
                    // then we want to align it at bottom, as it might be taller than the window height
                    if (fromMessageId == details.LastMessageId && !unread)
                    {
                        alignment = VerticalAlignment.Bottom;
                        pixel = null;
                    }
                }

                if (firstVisibleIndex == -1)
                {
                    firstVisibleIndex = fromDateOffset != 0
                        ? messages.MessagesValue.FindLastIndex(m => m.Date >= fromDateOffset)
                        : messages.MessagesValue.FindIndex(m => m.Id == fromMessageId);

                    if (firstVisibleIndex != -1)
                    {
                        if (fromDateOffset != 0)
                            fromMessageId = messages.MessagesValue[firstVisibleIndex].Id;
                        else
                            unread = false;
                    }
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
                    values = firstVisibleIndex == -1 || messages.MessagesValue.Count == firstVisibleIndex + 1
                        ? messages.MessagesValue
                        : messages.MessagesValue.Take(firstVisibleIndex + 1);
                }
                else
                {
                    scrollMode = direction == ScrollIntoViewAlignment.Default ? ItemsUpdatingScrollMode.KeepLastItemInView : ItemsUpdatingScrollMode.KeepItemsInView;
                    values = messages.MessagesValue;
                }

                if (values is IList<Message> temp)
                {
                    if (_forumTopic == null && _thread != null && (fromMessageId == 0 || _thread.Messages.Any(x => x.Id == fromMessageId)))
                    {
                        await AddHeaderAsync(temp, temp.Count > 0 ? temp[^1] : null);
                    }
                    else if (temp.Empty())
                    {
                        await AddHeaderAsync(temp, null);
                    }
                }

                var replied = new MessageCollection(this, null, values, false, Type);
                return new LoadSliceResult(replied, fromMessageId, scrollMode, alignment, pixel, unread, messages.TotalCount);
            }

            return null;
        }

        private class LoadSliceResult
        {
            public LoadSliceResult(MessageCollection items, long fromMessageId, ItemsUpdatingScrollMode scrollMode, VerticalAlignment alignment, double? pixel, bool unread, int totalCount)
            {
                Items = items;
                FromMessageId = fromMessageId;
                ScrollMode = scrollMode;
                Alignment = alignment;
                Pixel = pixel;
                IsUnread = unread;
                TotalCount = totalCount;
            }

            public MessageCollection Items { get; }

            public long FromMessageId { get; }

            public ItemsUpdatingScrollMode ScrollMode { get; }

            public VerticalAlignment Alignment { get; }

            public double? Pixel { get; }

            public bool IsUnread { get; }

            public int TotalCount { get; }
        }

        private async Task AddSponsoredMessagesAsync()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup { IsChannel: true })
            {
                var response = await ClientService.SendAsync(new GetChatSponsoredMessages(chat.Id));
                if (response is SponsoredMessages sponsored && sponsored.Messages.Count > 0)
                {
                    PendingSponsoredMessage = sponsored.Messages[0];

                    //for (int i = 1; i < sponsored.Messages.Count; i++)
                    //{
                    //    var message = sponsored.Messages[i];
                    //    var test = CreateMessage(new Message(message.MessageId, null, chat.Id, null, null, false, false, false, false, false, true, false, false, false, 0, 0, null, null, null, null, null, null, null, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, 0, null, new MessageSponsored(message), null));
                    //    InsertMessageInOrder(test);
                    //}
                }
            }
            else if (chat.Type is ChatTypePrivate && ClientService.TryGetUser(chat, out User user) && user.Type is UserTypeBot)
            {
                var response = await ClientService.SendAsync(new GetChatSponsoredMessages(chat.Id));
                if (response is SponsoredMessages sponsored && sponsored.Messages.Count > 0)
                {
                    SponsoredMessage = sponsored.Messages[0];
                    //Items.Add(CreateMessage(new Message(0, new MessageSenderChat(sponsored.SponsorChatId), sponsored.SponsorChatId, null, null, false, false, false, false, false, false, false, false, false, false, false, false, true, false, 0, 0, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, sponsored.Content, null)));
                }
            }
        }


        public async Task LoadScheduledSliceAsync()
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
            IsOldestSliceLoaded = null;
            IsNewestSliceLoaded = null;
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
                    replied.Insert(0, CreateMessage(new Message(0, target.SenderId, target.ChatId, null, target.SchedulingState, target.IsOutgoing, false, false, false, false, target.IsChannelPost, false, false, false, target.Date, 0, null, null, null, null, null, null, null, target.TopicId, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, 0, null, string.Empty, new MessageHeaderDate(target.Date), null)));
                }

                Items.ReplaceWith(replied);

                IsOldestSliceLoaded = true;
                IsNewestSliceLoaded = true;
            }

            _loadingSlice = false;
            IsLoading = false;
        }

        public virtual Task LoadEventLogSliceAsync(string query = "")
        {
            return Task.CompletedTask;
        }

        public virtual Task LoadQuickReplyShortcutSliceAsync()
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
                await LoadMessageSliceAsync(null, message.Id, fromDateOffset: dateOffset, onlyRemote: true);
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

        public MessageCollection Items { get; }

        public MessageViewModel CreateMessage(Message message, bool forLanguageStatistics = false)
        {
            if (message == null)
            {
                return null;
            }

            var model = new MessageViewModel(ClientService, _messageDelegateWeak, _chat, _forumTopic, _directMessagesChatTopic, message, true);

            if (forLanguageStatistics)
            {
                UpdateLanguageStatistics(model);
            }

            return model;
        }

        public PinnedMessageViewModel CreatePinnedMessage(Message message, int index)
        {
            if (message == null)
            {
                return null;
            }

            return new PinnedMessageViewModel(ClientService, _messageDelegateWeak, _chat, message, index);
        }

        protected void ProcessMessages(Chat chat, IList<MessageViewModel> messages, bool returnAlbumRoot = false)
        {
            ProcessAlbums(chat, messages, returnAlbumRoot);

            for (int i = 0; i < messages.Count; i++)
            {
                var message = messages[i];
                if (message.Content is MessageForumTopicCreated or MessageChatUpgradeFrom && Type == DialogType.Thread)
                {
                    messages.RemoveAt(i);
                    i--;

                    continue;
                }
                else if (message.Content is MessageChatUpgradeFrom upgradeFrom)
                {
                    if (ClientService.TryGetBasicGroup(upgradeFrom.BasicGroupId, out BasicGroup basicGroup))
                    {
                        // TODO: check if group is accessible
                    }
                    else
                    {
                        messages.RemoveAt(i);
                        i--;

                        continue;
                    }
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
                else if (message.Content is MessageStakeDice stakeDice)
                {
                    if (message.Id > chat.LastReadInboxMessageId)
                    {
                        message.GeneratedContentUnread = true;
                    }
                    else if (!message.GeneratedContentUnread)
                    {
                        message.GeneratedContentUnread = stakeDice.IsInitialState();
                    }
                }
                else if (message.Id > chat.LastReadInboxMessageId)
                {
                    message.GeneratedContentUnread = true;
                }

                if (message.Content is MessagePaidMedia paidMedia)
                {
                    message.Content = new MessagePaidAlbum(paidMedia);
                }
                else if (message.Content is MessageStory story)
                {
                    message.Content = new MessageAsyncStory
                    {
                        ViaMention = story.ViaMention,
                        StoryId = story.StoryId,
                        StoryPosterChatId = story.StoryPosterChatId
                    };
                }
                else if (message.Content is MessageContact contact && contact.Contact.PhoneNumber == "999888777666")
                {
                    message.SenderId = new MessageSenderUser(contact.Contact.UserId);
                    message.Content = new MessageChatAddMembers(new[] { contact.Contact.UserId });
                }

                if (message.EffectId != 0)
                {
                    if (message.Id > chat.LastReadInboxMessageId)
                    {
                        message.GeneratedContentUnread = true;
                    }

                    message.Effect = ClientService.LoadMessageEffect(message.EffectId, message.GeneratedContentUnread);

                    if (message.Effect == null)
                    {
                        if (_messageEffects.TryGetValue(message.EffectId, out var hashSet))
                        {
                            hashSet.Add(message.Id);
                        }
                        else
                        {
                            _messageEffects[message.EffectId] = new HashSet<long>
                            {
                                message.Id
                            };
                        }
                    }
                }

                if (message.SuggestedPostInfo != null)
                {
                    message.ReplyMarkup = message.SuggestedPostInfo.ToReplyMarkup(message.IsOutgoing);
                }

                if (IsTranslating)
                {
                    _translateService.Translate(message, Settings.Translate.To);
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

            if (message.Content is MessageText text && text.LinkPreview == null)
            {
                if (text.Text.Entities.Count == 0 && Emoji.TryCountEmojis(text.Text.Text, out int count, 3))
                {
                    message.GeneratedContent = new MessageBigEmoji(text.Text, count);
                }
                else if (text.Text.Entities.Count > 0 && Emoji.TryCountCustomEmojis(text.Text, out count))
                {
                    message.GeneratedContent = new MessageBigEmoji(text.Text, count);
                }
                else
                {
                    message.GeneratedContent = null;
                }
            }
            else
            {
                message.GeneratedContent = null;
            }
        }

        private void ProcessAlbums(Chat chat, IList<MessageViewModel> slice, bool returnAlbumRoot)
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
                    groupBase.SenderId = message.SenderId;
                    groupBase.ForwardInfo = message.ForwardInfo;

                    group = CreateMessage(groupBase);

                    slice[i] = group;
                    newGroups ??= new();
                    newGroups[groupedId] = groupedId;
                    _groupedMessages[groupedId] = group;
                }
                else if (returnAlbumRoot)
                {
                    slice[i] = group;
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
                }
            }

            if (groups != null)
            {
                foreach (var group in groups.Values)
                {
                    if (group.Item1.Content is MessageAlbum album)
                    {
                        album.Invalidate();

                        var first = album.Messages.FirstOrDefault();
                        if (first != null)
                        {
                            group.Item1.IsFirst = first.IsFirst;
                            group.Item1.IsLast = album.Messages[^1].IsLast;

                            group.Item1.UpdateAlbum(first);
                        }
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
                    _thread?.Messages[^1].Id == replyToMessage.MessageId &&
                    replyToMessage.Quote == null)
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
                message.Content is MessagePaymentSuccessful ||
                message.Content is MessageChecklistTasksAdded ||
                message.Content is MessageChecklistTasksDone ||
                message.Content is MessageSuggestedPostPaid ||
                message.Content is MessageSuggestedPostRefunded)
            {
                message.ReplyToState = message.Content is MessageGiveawayWinners
                    ? MessageReplyToState.Hidden
                    : MessageReplyToState.Loading;

                ClientService.GetReplyTo(message, response =>
                {
                    if (response is Message result)
                    {
                        message.ReplyToItem = CreateMessage(result);
                        message.ReplyToState = message.Content is MessageGiveawayWinners
                            ? MessageReplyToState.Hidden
                            : MessageReplyToState.None;

                        if (IsTranslating)
                        {
                            _translateService.Translate(message.ReplyToItem as MessageViewModel, Settings.Translate.To);
                        }
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
                        bubble =>
                        {
                            bubble.UpdateMessageReply(message);

                            if (message.SuggestedPostInfo != null)
                            {
                                bubble.UpdateMessageSuggestedPostInfo(message);
                            }
                        },
                        service => service.UpdateMessage(message)));
                });
            }
            else if (message.Content is MessageAsyncStory asyncStory)
            {
                asyncStory.State = MessageStoryState.Loading;

                ClientService.GetStory(asyncStory.StoryPosterChatId, asyncStory.StoryId, response =>
                {
                    if (response is Story story)
                    {
                        asyncStory.Story = story;
                        asyncStory.State = MessageStoryState.None;

                        if (story.Content is StoryContentPhoto photo)
                        {
                            message.GeneratedContent = new MessagePhoto(photo.Photo, null, false, false, false);
                        }
                        else if (story.Content is StoryContentVideo video)
                        {
                            message.GeneratedContent = new MessageVideo(new Video((int)video.Video.Duration, video.Video.Width, video.Video.Height, "video.mp4", "video/mp4", video.Video.HasStickers, true, video.Video.Minithumbnail, video.Video.Thumbnail, video.Video.Video), Array.Empty<AlternativeVideo>(), Array.Empty<VideoStoryboard>(), null, 0, null, false, false, false);
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
            if (parameter is ChatMessageTopic chatMessageTopic)
            {
                parameter = chatMessageTopic.ChatId;

                if (Type == DialogType.History)
                {
                    Type = DialogType.Thread;
                }

                if (chatMessageTopic.MessageTopic is MessageTopicSavedMessages savedMessages)
                {
                    if (ClientService.TryGetSavedMessagesTopic(savedMessages.SavedMessagesTopicId, out var topic))
                    {
                        SavedMessagesTopic = topic;
                        TopicId = new MessageTopicSavedMessages(topic.Id);
                    }
                    else
                    {
                        return;
                    }

                    if (ClientService.TryGetChat(SavedMessagesTopic.Type, out Chat savedMessagesChat))
                    {
                        ClientService.LoadFullInfo(savedMessagesChat);
                    }
                }
                else if (chatMessageTopic.MessageTopic is MessageTopicForum forum)
                {
                    if (ClientService.TryGetForumTopic(chatMessageTopic.ChatId, forum.ForumTopicId, out ForumTopic forumTopic))
                    {
                        ForumTopic = forumTopic;
                    }
                    else
                    {
                        ForumTopic = await ClientService.SendAsync(new GetForumTopic(chatMessageTopic.ChatId, forum.ForumTopicId)) as ForumTopic;
                    }

                    if (ForumTopic != null)
                    {
                        parameter = chatMessageTopic.ChatId;
                        TopicId = new MessageTopicForum(_forumTopic.Info.ForumTopicId);
                    }
                    else
                    {
                        return;
                    }
                }
                else if (chatMessageTopic.MessageTopic is MessageTopicDirectMessages directMessagesChat)
                {
                    if (ClientService.TryGetDirectMessagesChatTopic(chatMessageTopic.ChatId, directMessagesChat.DirectMessagesChatTopicId, out DirectMessagesChatTopic feedbeckChatTopic))
                    {
                        DirectMessagesChatTopic = feedbeckChatTopic;
                        TopicId = new MessageTopicDirectMessages(DirectMessagesChatTopic.Id);
                    }
                    else
                    {
                        DirectMessagesChatTopic = await ClientService.SendAsync(new GetDirectMessagesChatTopic(chatMessageTopic.ChatId, directMessagesChat.DirectMessagesChatTopicId)) as DirectMessagesChatTopic;
                        TopicId = new MessageTopicDirectMessages(DirectMessagesChatTopic.Id);
                    }

                    if (ClientService.TryGetChat(DirectMessagesChatTopic.SenderId, out Chat directMessagesChatChat))
                    {
                        ClientService.LoadFullInfo(directMessagesChatChat);
                    }
                    else if (ClientService.TryGetUser(DirectMessagesChatTopic.SenderId, out User directMessagesChatUser))
                    {
                        ClientService.Send(new GetUserFullInfo(directMessagesChatUser.Id));
                    }
                }
                else if (chatMessageTopic.MessageTopic is MessageTopicThread thread)
                {
                    Thread = await ClientService.SendAsync(new GetMessageThread(chatMessageTopic.ChatId, thread.MessageThreadId)) as MessageThreadInfo;

                    if (Thread != null)
                    {
                        parameter = Thread.ChatId;
                        TopicId = new MessageTopicThread(Thread.MessageThreadId);
                    }
                    else
                    {
                        return;
                    }
                }
            }
            else if (parameter is ChatBusinessRepliesIdNavigationArgs businessRepliesIdArgs)
            {
                QuickReplyShortcut = ClientService.GetQuickReplyShortcut(businessRepliesIdArgs.QuickReplyShortcut);
                QuickReplyShortcut ??= new QuickReplyShortcut(-1, businessRepliesIdArgs.QuickReplyShortcut, null, 0);
                parameter = ClientService.Options.MyId;
            }

            var chat = ClientService.GetChat((long)parameter);
            chat ??= await ClientService.SendAsync(new GetChat((long)parameter)) as Chat;

            if (chat == null)
            {
                return;
            }

            if (HasProtectedContent)
            {
                Delegate?.DisableScreenCapture();
            }

            Chat = chat;
            IsForum = Type == DialogType.History && ClientService.IsForum(chat);
            IsDirectMessagesGroup = Type == DialogType.History && ClientService.IsDirectMessagesGroup(chat);

            SetScrollMode(ItemsUpdatingScrollMode.KeepLastItemInView, true);
            SetTranslating();

            if (state.TryRemove("access_token", out string accessToken))
            {
                AccessToken = accessToken;
            }

            if (state.TryRemove("search", out string search))
            {
                SearchExecute(search);
            }

#pragma warning disable CS4014
            if (Type == DialogType.ScheduledMessages)
            {
                Logger.Debug(string.Format("{0} - Loading scheduled messages", chat.Id));

                NotifyInitialized();
                LoadScheduledSliceAsync();
            }
            else if (Type == DialogType.EventLog)
            {
                Logger.Debug(string.Format("{0} - Loading event log", chat.Id));

                NotifyInitialized();
                LoadEventLogSliceAsync();
            }
            else if (Type == DialogType.BusinessReplies)
            {
                Logger.Debug(string.Format("{0} - Loading business replies", chat.Id));

                NotifyInitialized();
                LoadQuickReplyShortcutSliceAsync();
            }
            else if (IsSavedMessagesTab)
            {
                Logger.Debug(string.Format("{0} - Loading messages from last", chat.Id));
                LoadMessageSliceAsync(null, 0, VerticalAlignment.Bottom);
            }
            else if (state.TryRemove("message_id", out long navigation))
            {
                var details = GetCurrentDetails();

                Settings.Chats.Clear(chat.Id, details.TopicId);
                Logger.Debug(string.Format("{0} - Loading messages from specific id", chat.Id));

                state.TryRemove("highlight", out TextQuote quote);
                state.TryRemove("checklist_task_id", out int checklistTaskId);
                LoadMessageSliceAsync(null, navigation, highlight: quote, checklistTaskId: checklistTaskId);
            }
            else if (state.TryRemove("prepared_message", out Function preparedMessage))
            {
                // TODO: this is quite terrible
                IsOldestSliceLoaded = true;
                IsNewestSliceLoaded = true;
                NotifyInitialized();
                _ = SendMessageAsync(preparedMessage);
            }
            else
            {
                var status = ClientService.GetChatMemberStatus(chat, out _);
                if (status is ChatMemberStatusLeft)
                {
                    Logger.Debug(string.Format("{0} - Loading messages from zero", chat.Id));
                    LoadMessageSliceAsync(null, long.MaxValue, VerticalAlignment.Top);
                }
                else
                {
                    var details = GetCurrentDetails();

                    bool TryRemove(long chatId, out long v1, out long v2)
                    {
                        var a = Settings.Chats.TryRemove(chat.Id, details.TopicId, ChatSetting.ReadInboxMaxId, out v1);
                        var b = Settings.Chats.TryRemove(chat.Id, details.TopicId, ChatSetting.Index, out v2);
                        return a && b;
                    }

                    if (TryRemove(chat.Id, out long readInboxMaxId, out long start) &&
                        readInboxMaxId == details.LastReadInboxMessageId &&
                        start <= details.LastReadInboxMessageId)
                    {
                        if (Settings.Chats.TryRemove(chat.Id, details.TopicId, ChatSetting.Pixel, out double pixel))
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
                        LoadMessageSliceAsync(null, details.LastReadInboxMessageId, VerticalAlignment.Top);
                    }
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
            ClientService.AddRecentlyOpenedChat(chat.Id);

            Delegate?.UpdateChat(chat);
            Delegate?.UpdateChatActions(chat, ClientService.GetChatActions(chat.Id));

            UpdateChatActionBar(chat);

            if (chat.Type is ChatTypePrivate privata)
            {
                var item = ClientService.GetUser(privata.UserId);
                var cache = ClientService.GetUserFull(privata.UserId);

                Delegate?.UpdateUser(chat, item, cache, false, _accessToken != null);

                ClientService.Send(new GetUserFullInfo(privata.UserId));

                if (cache != null)
                {
                    UpdateEmptyState(item, cache, false);
                }

                if (privata.UserId == ClientService.Options.MyId)
                {
                    ClientService.Send(new GetSavedMessagesTags(SavedMessagesTopicId), result =>
                    {
                        if (result is SavedMessagesTags tags)
                        {
                            BeginOnUIThread(() => SavedMessagesTags = tags);
                        }
                    });
                }
            }
            else if (chat.Type is ChatTypeSecret secretType)
            {
                var secret = ClientService.GetSecretChat(secretType.SecretChatId);
                var item = ClientService.GetUser(secretType.UserId);
                var cache = ClientService.GetUserFull(secretType.UserId);

                Delegate?.UpdateSecretChat(chat, secret);
                Delegate?.UpdateUser(chat, item, cache, true, false);
                Delegate?.UpdateUserEmptyState(chat, null, null, null);

                ClientService.Send(new GetUserFullInfo(secret.UserId));
            }
            else if (chat.Type is ChatTypeBasicGroup basic)
            {
                var item = ClientService.GetBasicGroup(basic.BasicGroupId);
                var cache = ClientService.GetBasicGroupFull(basic.BasicGroupId);

                Delegate?.UpdateBasicGroup(chat, item, cache);
                Delegate?.UpdateUserEmptyState(chat, null, null, null);

                ClientService.Send(new GetBasicGroupFullInfo(basic.BasicGroupId));
                _messageDelegate.UpdateAdministrators(chat.Id);
            }
            else if (chat.Type is ChatTypeSupergroup super)
            {
                var item = ClientService.GetSupergroup(super.SupergroupId);
                var cache = ClientService.GetSupergroupFull(super.SupergroupId);

                Delegate?.UpdateSupergroup(chat, item, cache);
                Delegate?.UpdateSupergroupEmptyState(chat, item);
                Delegate?.UpdateUserEmptyState(chat, null, null, null);

                ClientService.Send(new GetSupergroupFullInfo(super.SupergroupId));
                _messageDelegate.UpdateAdministrators(chat.Id);
            }

            UpdateGroupCall(chat, chat.VideoChat.GroupCallId);

            ShowReplyMarkup(chat);
            ShowDraftMessage(chat);
            ShowSwitchInline(state);
            ShowReplyTo(state);

            if (Type is DialogType.History or DialogType.Thread && state.TryRemove("package", out DataPackageView package))
            {
                await HandlePackageAsync(package);
            }
            else if (Type is DialogType.History && state.TryRemove("videoChat", out string videoChat))
            {
                _voipService.JoinGroupCall(NavigationService, chat.Id, videoChat);
            }
        }

        readonly struct ChatMessageTopicDetails
        {
            public readonly long ChatId;
            public readonly MessageTopic TopicId;
            public readonly long LastMessageId;
            public readonly long LastReadInboxMessageId;

            public ChatMessageTopicDetails(long chatId, MessageTopic topicId, long lastMessageId, long lastReadInboxMessageId)
            {
                ChatId = chatId;
                TopicId = topicId;
                LastMessageId = lastMessageId;
                LastReadInboxMessageId = lastReadInboxMessageId;
            }
        }

        private ChatMessageTopicDetails GetCurrentDetails()
        {
            MessageTopic topicId;
            long lastMessageId;
            long lastReadInboxMessageId;

            if (SavedMessagesTopic != null)
            {
                topicId = new MessageTopicSavedMessages(SavedMessagesTopic.Id);
                lastMessageId = SavedMessagesTopic.LastMessage?.Id ?? long.MaxValue;
                lastReadInboxMessageId = SavedMessagesTopic.LastMessage?.Id ?? long.MaxValue;
            }
            else if (DirectMessagesChatTopic != null)
            {
                topicId = new MessageTopicDirectMessages(DirectMessagesChatTopic.Id);
                lastMessageId = DirectMessagesChatTopic.LastMessage?.Id ?? long.MaxValue;
                lastReadInboxMessageId = DirectMessagesChatTopic.LastReadInboxMessageId;
            }
            else if (ForumTopic != null)
            {
                topicId = new MessageTopicForum(ForumTopic.Info.ForumTopicId);
                lastMessageId = ForumTopic.LastMessage?.Id ?? long.MaxValue;
                lastReadInboxMessageId = ForumTopic.LastReadInboxMessageId;
            }
            else if (Thread != null)
            {
                topicId = new MessageTopicThread(Thread.MessageThreadId);
                lastMessageId = Thread.ReplyInfo?.LastMessageId ?? long.MaxValue;
                lastReadInboxMessageId = Thread.ReplyInfo?.LastReadInboxMessageId ?? long.MaxValue;
            }
            else
            {
                topicId = null;
                lastMessageId = Chat.LastMessage?.Id ?? long.MaxValue;
                lastReadInboxMessageId = Chat.LastReadInboxMessageId;
            }

            // TODO: verify this is valid in all cases
            if (lastReadInboxMessageId == 0 && Thread == null)
            {
                lastReadInboxMessageId = lastMessageId;
            }

            return new ChatMessageTopicDetails(Chat.Id, topicId, lastMessageId, lastReadInboxMessageId);
        }

        protected override void OnNavigatedFrom(NavigationState suspensionState, bool suspending)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            foreach (var pending in _pendingTextMessages.Values)
            {
                pending.Stop();
                pending.Updated -= PendingTextMessage_Updated;
                pending.Completed -= PendingTextMessage_Completed;
            }

            _pendingTextMessages.Clear();

            _lastSeenTimer?.Stop();
            _groupedMessages.Clear();
            _chatActionManager = null;

            SelectedItems.Clear();

            IsSelectionEnabled = false;

            ClientService.Send(new CloseChat(chat.Id));

            if (Type is not DialogType.History and not DialogType.Thread || IsSavedMessagesTab)
            {
                return;
            }

            var details = GetCurrentDetails();

            void Remove(string reason)
            {
                Settings.Chats.Clear(chat.Id, details.TopicId);
                Logger.Debug(string.Format("{0} - Removing scrolling position, {1}", chat.Id, reason));
            }

            try
            {
                var field = HistoryField;
                if (field != null && !field.IsSuspended && TryGetLastVisibleMessageId(out long lastVisibleId, out int lastVisibleIndex))
                {
                    var firstNonVisibleId = lastVisibleIndex < Items.Count - 1
                        ? Items[lastVisibleIndex + 1].Id
                        : lastVisibleId;

                    if (lastVisibleId != 0 && lastVisibleId != chat.LastMessage?.Id)
                    {
                        if (firstNonVisibleId < details.LastReadInboxMessageId)
                        {
                            Settings.Chats[chat.Id, details.TopicId, ChatSetting.ReadInboxMaxId] = details.LastReadInboxMessageId;
                            Settings.Chats[chat.Id, details.TopicId, ChatSetting.Index] = lastVisibleId;

                            var container = field.ContainerFromIndex(lastVisibleIndex) as ListViewItem;
                            if (container != null)
                            {
                                var transform = container.TransformToVisual(field);
                                var position = transform.TransformPoint(new Point());

                                Settings.Chats[chat.Id, details.TopicId, ChatSetting.Pixel] = field.ActualHeight - (position.Y + container.ActualHeight);
                                Logger.Debug(string.Format("{0} - Saving scrolling position, message: {1}, pixel: {2}", chat.Id, lastVisibleId, field.ActualHeight - (position.Y + container.ActualHeight)));
                            }
                            else
                            {
                                Settings.Chats.TryRemove(chat.Id, details.TopicId, ChatSetting.Pixel, out double pixel);
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
            }
            catch
            {
                Remove("exception");
            }

            SaveDraft();
        }

        private void ShowSwitchInline(IDictionary<string, object> state)
        {
            if (Type == DialogType.History && state.TryGet("switch_query", out string query) && state.TryGet("switch_bot", out long userId))
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
            else if (Type == DialogType.History && state.TryGet("draft", out FormattedText draft))
            {
                state.Remove("draft");

                SetText(draft, focus: true);
            }
        }

        private void ShowReplyTo(IDictionary<string, object> state)
        {
            if (Type is DialogType.History or DialogType.Thread && state.TryGet("reply_to", out MessageViewModel message))
            {
                state.TryGet("reply_to_quote", out InputTextQuote quote);
                state.TryGet("reply_to_task_id", out int taskId);

                state.Remove("reply_to");
                state.Remove("reply_to_quote");
                state.Remove("reply_to_task_id");

                // We arrive here from "Reply in another chat", so we assume the message can be replied in another chat
                ComposerHeader = new MessageComposerHeader(ClientService)
                {
                    ReplyTo = new MessageComposerReplyTo(message, quote, taskId, true)
                };

                TextField?.Focus(FocusState.Keyboard);
            }
        }

        private async void ShowReplyMarkup(Chat chat)
        {
            if (chat.ReplyMarkupMessageId == 0 || Type != DialogType.History)
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
            if (ForumTopic != null)
            {
                draft = ForumTopic.DraftMessage;
            }
            else if (DirectMessagesChatTopic != null)
            {
                draft = DirectMessagesChatTopic.DraftMessage;
            }
            else if (Thread != null)
            {
                draft = Thread.DraftMessage;
            }
            else
            {
                draft = chat.DraftMessage;
            }

            if (!force)
            {
                var current = GetFormattedText(false, false);

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
            if (input == null || Type is not DialogType.History and not DialogType.Thread)
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
                        var properties = await ClientService.SendAsync(new GetMessageProperties(message.ChatId, message.Id)) as MessageProperties;

                        ComposerHeader = new MessageComposerHeader(ClientService)
                        {
                            ReplyTo = new MessageComposerReplyTo(CreateMessage(message), replyToMessage.Quote, replyToMessage.ChecklistTaskId, properties?.CanBeRepliedInAnotherChat ?? false)
                        };

                        goto UpdateText;
                    }
                }
                else if (draft.ReplyTo is InputMessageReplyToExternalMessage replyToExternalMessage)
                {
                    var response = await ClientService.SendAsync(new GetMessage(replyToExternalMessage.ChatId, replyToExternalMessage.MessageId));
                    if (response is Message message)
                    {
                        var properties = await ClientService.SendAsync(new GetMessageProperties(message.ChatId, message.Id)) as MessageProperties;

                        ComposerHeader = new MessageComposerHeader(ClientService)
                        {
                            ReplyTo = new MessageComposerReplyTo(CreateMessage(message), replyToExternalMessage.Quote, replyToExternalMessage.ChecklistTaskId, properties?.CanBeRepliedInAnotherChat ?? false)
                        };

                        goto UpdateText;
                    }
                }

                ComposerHeader = null;

            UpdateText:
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
            if (Type is not DialogType.History and not DialogType.Thread)
            {
                return;
            }

            if (_isSelectionEnabled && !clear)
            {
                return;
            }

            var embedded = _composerHeader;
            if (embedded != null && embedded.Editing != null)
            {
                return;
            }

            var chat = Chat;
            if (chat == null || RestrictsNewChats)
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

            var formattedText = GetFormattedText(clear, false);
            if (formattedText == null)
            {
                return;
            }

            var replyToMessageId = 0L;
            var replyToChatId = 0L;
            var replyToTaskId = 0;
            var quote = default(InputTextQuote);

            if (embedded != null && embedded.ReplyTo != null)
            {
                replyToMessageId = embedded.ReplyTo.Message.Id;
                replyToChatId = embedded.ReplyTo.Message.ChatId;
                replyToTaskId = embedded.ReplyTo.ChecklistTaskId;
                quote = embedded.ReplyTo.Quote;

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

                InputMessageReplyTo inputReply = replyToMessageId != 0
                    ? replyToChatId == chat.Id || replyToChatId == 0
                    ? new InputMessageReplyToMessage(replyToMessageId, quote, replyToTaskId)
                    : new InputMessageReplyToExternalMessage(replyToChatId, replyToMessageId, quote, replyToTaskId)
                    : null;

                draft = new DraftMessage(inputReply, 0, new InputMessageText(formattedText, null, false), 0, embedded?.SuggestedPostInfo);
            }

            _draft = draft;
            ClientService.Send(new SetChatDraftMessage(_chat.Id, OutgoingTopicId, draft));
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
            var header = _composerHeader;
            if (header == null)
            {
                return;
            }

            if (header.SuggestedPostInfo != null)
            {
                ComposerHeader = new MessageComposerHeader(ClientService)
                {
                    Editing = header.Editing,
                    ReplyTo = header.ReplyTo,
                    LinkPreviewUrl = header.LinkPreviewUrl,
                    LinkPreview = header.LinkPreview,
                    LinkPreviewDisabled = header.LinkPreviewDisabled
                };
            }
            else if (header.LinkPreview != null)
            {
                ComposerHeader = new MessageComposerHeader(ClientService)
                {
                    Editing = header.Editing,
                    ReplyTo = header.ReplyTo,
                    SuggestedPostInfo = header.SuggestedPostInfo,
                    LinkPreviewUrl = header.LinkPreviewUrl,
                    LinkPreview = null,
                    LinkPreviewDisabled = true
                };
            }
            else
            {
                if (header.Editing != null)
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
                    if (header.ReplyTo?.Message.ReplyMarkup is ReplyMarkupForceReply)
                    {
                        ClientService.Send(new DeleteChatReplyMarkup(header.ReplyTo.Message.ChatId, header.ReplyTo.Message.Id));
                    }

                    ComposerHeader = null;
                }
            }

            TextField?.Focus(FocusState.Programmatic);
        }

        protected override InputMessageReplyTo GetReply(bool clean, bool notify = true)
        {
            var embedded = _composerHeader;
            if (embedded == null)
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

                if (embedded.ReplyTo?.Message.ReplyMarkup is ReplyMarkupForceReply)
                {
                    ClientService.Send(new DeleteChatReplyMarkup(embedded.ReplyTo.Message.ChatId, embedded.ReplyTo.Message.Id));
                }
            }

            return embedded.ReplyTo?.ToInput(this);
        }

        #endregion

        public async void SendMessage(string args)
        {
            await SendMessageAsync(args);
        }

        public override LinkPreviewOptions GetLinkPreviewOptions()
        {
            return _composerHeader?.LinkPreviewOptions;
        }

        protected override Function CreateSendMessage(long chatId, MessageTopic topicId, InputMessageReplyTo replyTo, MessageSendOptions messageSendOptions, InputMessageContent inputMessageContent)
        {
            if (QuickReplyShortcut != null)
            {
                if (replyTo is InputMessageReplyToMessage replyToMessage)
                {
                    return new AddQuickReplyShortcutMessage(QuickReplyShortcut.Name, replyToMessage.MessageId, inputMessageContent);
                }

                return new AddQuickReplyShortcutMessage(QuickReplyShortcut.Name, 0, inputMessageContent);
            }

            if (IsForum && ForumTopic == null && Chat.Type is ChatTypePrivate)
            {
                var response = ClientService.SendAsync(new CreateForumTopic(chatId, Strings.BotForumNewTopic, true, new ForumTopicIcon(0x6FB9F0, 0))).Result;
                if (response is ForumTopicInfo info)
                {
                    topicId = new MessageTopicForum(info.ForumTopicId);

                    var function = base.CreateSendMessage(chatId, topicId, replyTo, messageSendOptions, inputMessageContent);
                    var state = new NavigationState
                    {
                        { "prepared_message", function }
                    };

                    NavigationService.NavigateToChat(chatId, topic: topicId, force: false, state: state);
                    return null;
                }
            }

            return base.CreateSendMessage(chatId, topicId, replyTo, messageSendOptions, inputMessageContent);
        }

        protected override Function CreateSendMessageAlbum(long chatId, MessageTopic topicId, InputMessageReplyTo replyTo, MessageSendOptions messageSendOptions, IList<InputMessageContent> inputMessageContent)
        {
            if (QuickReplyShortcut != null)
            {
                if (replyTo is InputMessageReplyToMessage replyToMessage)
                {
                    return new AddQuickReplyShortcutMessageAlbum(QuickReplyShortcut.Name, replyToMessage.MessageId, inputMessageContent);
                }

                return new AddQuickReplyShortcutMessageAlbum(QuickReplyShortcut.Name, 0, inputMessageContent);
            }

            if (IsForum && ForumTopic == null && ClientService.TryGetUser(Chat, out User user) && user.Type is UserTypeBot { AllowsUsersToCreateTopics: true })
            {
                var response = ClientService.SendAsync(new CreateForumTopic(chatId, Strings.BotForumNewTopic, true, new ForumTopicIcon(0x6FB9F0, 0))).Result;
                if (response is ForumTopicInfo info)
                {
                    topicId = new MessageTopicForum(info.ForumTopicId);

                    var function = base.CreateSendMessageAlbum(chatId, topicId, replyTo, messageSendOptions, inputMessageContent);
                    var state = new NavigationState
                    {
                        { "prepared_message", function }
                    };

                    NavigationService.NavigateToChat(chatId, topic: topicId, force: false, state: state);
                    return null;
                }
            }

            return base.CreateSendMessageAlbum(chatId, topicId, replyTo, messageSendOptions, inputMessageContent);
        }

        protected override async Task<bool> BeforeSendMessageAsync(FormattedText formattedText, LinkPreviewOptions linkPreview)
        {
            if (Chat is not Chat chat)
            {
                return false;
            }

            var header = _composerHeader;
            if (header?.Editing == null)
            {
                return false;
            }

            var editing = header.Editing.Message;

            var factory = header.Editing.Media;
            if (factory is InputMessageContent input)
            {
                var topicId = editing.TopicId;
                if (topicId is MessageTopicSavedMessages)
                {
                    topicId = null;
                }

                var options = new MessageSendOptions(header.SuggestedPostInfo, false, false, 0, false, null, 0, 0, true);

                var response = await ClientService.SendAsync(new SendMessage(editing.ChatId, topicId, null, options, input));
                if (response is Message preview)
                {
                    _contentOverrides[editing.CombinedId] = preview.Content;
                    Aggregator.Publish(new UpdateMessageContent(editing.ChatId, editing.Id, preview.Content));

                    ComposerHeader = null;
                    ClientService.Send(new EditMessageMedia(editing.ChatId, editing.Id, input));
                }
            }
            else
            {
                _contentOverrides.Remove(editing.CombinedId);

                var textContent = editing.Content is MessageText or MessageAnimatedEmoji or MessageBigEmoji;
                if (textContent && string.IsNullOrEmpty(formattedText.Text))
                {
                    ShowDraftMessage(chat);
                    DeleteMessage(editing);
                }
                else
                {
                    Function function;
                    if (QuickReplyShortcut != null)
                    {
                        if (textContent)
                        {
                            function = new EditQuickReplyMessage(QuickReplyShortcut.Id, editing.Id, new InputMessageText(formattedText, linkPreview, true));
                        }
                        else
                        {
                            // TODO
                            function = new EditQuickReplyMessage(QuickReplyShortcut.Id, editing.Id, new InputMessageText(formattedText, linkPreview, true));
                        }
                    }
                    else if (textContent)
                    {
                        function = new EditMessageText(chat.Id, editing.Id, new InputMessageText(formattedText, linkPreview, true));
                    }
                    else
                    {
                        function = new EditMessageCaption(chat.Id, editing.Id, formattedText, editing.ShowCaptionAboveMedia());
                    }

                    var response = await ClientService.SendAsync(function);
                    if (response is Message message)
                    {
                        ShowDraftMessage(chat);
                        Aggregator.Publish(new UpdateMessageSendSucceeded(message, editing.Id));
                    }
                    else if (response is Ok)
                    {
                        // TODO: quick reply
                        ComposerHeader = null;
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

        public void GiftPremium()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (ClientService.TryGetUser(chat, out User user) &&
                ClientService.TryGetUserFull(chat, out UserFullInfo fullInfo))
            {
                ShowPopup(new GiftPopup(ClientService, NavigationService, user, fullInfo));
            }
            else
            {
                ShowPopup(new GiftPopup(ClientService, NavigationService, chat));
            }
        }

        #endregion

        #region Suggest post

        public async void SuggestPost()
        {
            var popup = new SuggestPostPopup(ClientService, ComposerHeader?.SuggestedPostInfo);

            var confirm = await ShowPopupAsync(popup);
            if (confirm == ContentDialogResult.Primary)
            {
                ComposerHeader = new MessageComposerHeader(ClientService)
                {
                    SuggestedPostInfo = popup.SuggestedPostInfo,
                    ReplyTo = ComposerHeader?.ReplyTo,
                };
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
                    await ShowPopupAsync(chat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel ? Strings.RequestToJoinChannelSentDescription : Strings.RequestToJoinGroupSentDescription, Strings.RequestToJoinSent, Strings.OK);
                    return;

                    var message = Strings.RequestToJoinSent + Environment.NewLine + (chat.Type is ChatTypeSupergroup supergroup2 && supergroup2.IsChannel ? Strings.RequestToJoinChannelSentDescription : Strings.RequestToJoinGroupSentDescription);
                    var entity = new TextEntity(0, Strings.RequestToJoinSent.Length, new TextEntityTypeBold());

                    var text = new FormattedText(message, new[] { entity });

                    ToastPopup.Show(XamlRoot, text, ToastPopupIcon.JoinRequested);
                }
                else if (error.MessageEquals(ErrorType.CHANNELS_TOO_MUCH))
                {
                    NavigationService.ShowLimitReached(new PremiumLimitTypeSupergroupCount());
                }
                else
                {
                    ShowToast(error);
                }
            }
            else if (Constants.DEBUG)
            {
                ClientService.Send(new AddLocalMessage(chat.Id, new MessageSenderChat(chat.Id), null, true, new InputMessageContact(new Contact("999888777666", "SIMILAR", "CHANNELS", string.Empty, ClientService.Options.MyId))));
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
            ToggleMute(ClientService.Notifications.IsMuted(_chat));
        }

        private void ToggleMute(bool unmute)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            _notificationsService.SetMuteFor(chat, unmute ? 0 : 632053052, XamlRoot);
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

        public async void ReportUser()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var popup = new ContentPopup();
            var content = new StackPanel();
            var reportSpam = new CheckBox { Content = Strings.DeleteReportSpam, IsChecked = true, Margin = new Thickness(0, 16, 0, 0) };
            var deleteChat = new CheckBox { Content = Strings.DeleteThisChat, IsChecked = true, Margin = new Thickness(0, 0, 0, -4) };

            var text = new TextBlock
            {
                Style = BootStrapper.Current.Resources["BodyTextBlockStyle"] as Style
            };

            TextBlockHelper.SetMarkdown(text, string.Format(Strings.BlockUserAlert, chat.Title));

            content.Children.Add(text);
            content.Children.Add(reportSpam);
            content.Children.Add(deleteChat);

            popup.Title = string.Format(Strings.BlockUserTitle, chat.Title);
            popup.Content = content;
            popup.PrimaryButtonText = Strings.ClearButton;
            popup.SecondaryButtonText = Strings.Cancel;

            var confirm = await ShowPopupAsync(popup);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            if (reportSpam.IsChecked is true)
            {
                Report();
            }

            if (chat.Type is ChatTypePrivate privata)
            {
                ClientService.Send(new SetMessageSenderBlockList(new MessageSenderUser(privata.UserId), new BlockListMain()));
            }
            else if (chat.Type is ChatTypeSecret secret)
            {
                ClientService.Send(new SetMessageSenderBlockList(new MessageSenderUser(secret.UserId), new BlockListMain()));
            }

            if (deleteChat.IsChecked is true)
            {
                ClientService.Send(new DeleteChatHistory(chat.Id, true, false));
            }
        }

        public async void ReportSpam()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }
            else if (chat.Type is ChatTypePrivate or ChatTypeSecret)
            {
                ReportUser();
                return;
            }

            var title = Strings.AppName;
            var message = Strings.ReportSpamAlert;

            if (chat.Type is ChatTypeSupergroup supergroup)
            {
                message = supergroup.IsChannel ? Strings.ReportSpamAlertChannel : Strings.ReportSpamAlertGroup;
            }
            else if (chat.Type is ChatTypeBasicGroup)
            {
                message = Strings.ReportSpamAlertGroup;
            }

            var confirm = await ShowPopupAsync(message, title, Strings.OK, Strings.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {

                return;
            }

            Report();

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

        public void Boost()
        {
            Boost(0);
        }

        public async void Boost(int requiredLevel)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var response1 = await ClientService.SendAsync(new GetChatBoostFeatures(chat.Type is ChatTypeSupergroup { IsChannel: true }));
            var response2 = await ClientService.SendAsync(new GetAvailableChatBoostSlots());
            var response3 = await ClientService.SendAsync(new GetChatBoostStatus(chat.Id));

            if (response1 is ChatBoostFeatures features && response2 is ChatBoostSlots slots && response3 is ChatBoostStatus status)
            {
                ShowPopup(new ChatBoostFeaturesPopup(ClientService, NavigationService, chat, status, slots, features, ChatBoostFeature.None, requiredLevel));
            }
        }

        #region Delete and Exit

        public async void DeleteChat()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            Logger.Info(chat.Type);

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

        public async void ViewAsChats()
        {
            if (Chat is not Chat chat)
            {
                return;
            }

            await ClientService.SendAsync(new ToggleChatViewAsTopics(chat.Id, true));

            var target = typeof(ProfilePage);
            var parameter = new ChatMessageTopic(chat.Id, null);

            NavigationService.GoBackAt(0, false);

            NavigationService.Frame.BackStack.Add(new PageStackEntry(target, parameter, null));
            NavigationService.GoBack(infoOverride: new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromLeft });
            NavigationService.Frame.ForwardStack.Clear();
        }

        public async void ViewAsTopics()
        {
            if (Chat is not Chat chat)
            {
                return;
            }

            await ClientService.SendAsync(new ToggleChatViewAsTopics(chat.Id, true));
            NavigationService.GoBackAt(0);
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
                var response = await ClientService.SendAsync(new CreateForumTopic(chat.Id, popup.SelectedName, false, popup.SelectedIcon));
                if (response is ForumTopicInfo info)
                {
                    NavigationService.NavigateToChat(chat, topic: new MessageTopicForum(info.ForumTopicId), force: false, clearBackStack: true);
                }
            }
        }

        public void OpenProfile(INavigationService navigationService)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Id == ClientService.Options.RepliesBotChatId
                || chat.Id == ClientService.Options.VerificationCodesBotChatId
                || Type == DialogType.EventLog
                || Type == DialogType.Pinned
                || Type == DialogType.BusinessReplies
                || Type == DialogType.ScheduledMessages)
            {
                return;
            }

            if (SavedMessagesTopic != null || ForumTopic != null)
            {
                navigationService.Navigate(typeof(ProfilePage), new ChatMessageTopic(chat.Id, TopicId), infoOverride: new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });
            }
            else if (DirectMessagesChatTopic != null)
            {
                navigationService.NavigateToSender(DirectMessagesChatTopic.SenderId, infoOverride: new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });
            }
            else if (IsDirectMessagesGroup && ClientService.TryGetSupergroupFull(chat, out SupergroupFullInfo fullInfo))
            {
                navigationService.Navigate(typeof(ProfilePage), fullInfo.DirectMessagesChatId, infoOverride: new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });
            }
            else if (ChatId == ClientService.Options.MyId)
            {
                navigationService.Navigate(typeof(ProfilePage), new ChatMessageTopic(chat.Id, null), infoOverride: new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });
            }
            else
            {
                navigationService.Navigate(typeof(ProfilePage), chat.Id, infoOverride: new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });
            }
        }

        public void DeleteTopic()
        {
            if (SavedMessagesTopic != null)
            {
                DeleteSavedMessagesTopic(SavedMessagesTopic);
            }
            else if (DirectMessagesChatTopic != null)
            {
                DeleteDirectMessagesChatTopic(DirectMessagesChatTopic);
            }
        }

        private async void DeleteSavedMessagesTopic(SavedMessagesTopic topic)
        {
            string message;
            string title;
            string primary;

            if (topic.Type is SavedMessagesTopicTypeMyNotes)
            {
                message = Strings.ClearHistoryMyNotesMessage;
                title = Strings.ClearHistoryMyNotesTitle;
                primary = Strings.Delete;
            }
            else
            {
                var chatTitle = ClientService.GetTitle(topic);

                message = string.Format(Strings.ClearHistoryMessageSingle, chatTitle);
                title = string.Format(Strings.ClearHistoryTitleSingle, chatTitle);
                primary = Strings.Remove;
            }

            var confirm = await ShowPopupAsync(message, title, primary, Strings.Cancel, destructive: true);
            if (confirm == ContentDialogResult.Primary)
            {
                NavigationService.GoBack();
                ClientService.Send(new DeleteSavedMessagesTopicHistory(topic.Id));
            }
        }

        private async void DeleteDirectMessagesChatTopic(DirectMessagesChatTopic topic)
        {
            var message = string.Format(Strings.AreYouSureClearHistoryWithUser, ClientService.GetTitle(topic.SenderId));
            var title = Strings.ClearHistory;

            var confirm = await ShowPopupAsync(message, title, Strings.Delete, Strings.Cancel, destructive: true);
            if (confirm == ContentDialogResult.Primary)
            {
                NavigationService.GoBack();
                ClientService.Send(new DeleteDirectMessagesChatTopicHistory(ChatId, topic.Id));
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

            Logger.Info(chat.Type);

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

            if (ClientService.TryGetUserFull(chat, out UserFullInfo userFull))
            {
                if (userFull.CanBeCalled)
                {
                    _voipService.StartPrivateCall(NavigationService, chat, video);
                }
                else
                {
                    var confirm = await ShowPopupAsync(string.Format(Strings.CallForbiddenInviteLinkText, chat.Title), Strings.CallForbiddenInviteLinkTitle, Strings.CallForbiddenInviteLinkButton, Strings.Cancel);
                    if (confirm == ContentDialogResult.Primary)
                    {
                        var response = await ClientService.SendAsync(new CreateGroupCall(null));
                        if (response is GroupCallInfo info && ClientService.TryGetGroupCall(info.GroupCallId, out GroupCall groupCall))
                        {
                            var options = await PickMessageSendOptionsAsync();
                            if (options == null)
                            {
                                return;
                            }

                            await SendMessageAsync(null, new InputMessageText(groupCall.InviteLink.AsFormattedText(), null, false), options);
                        }
                    }
                }
            }
            else
            {
                _voipService.JoinGroupCall(NavigationService, chat.Id);
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

            if (chat.CanPinMessages(ClientService))
            {
                var confirm = await ShowPopupAsync(Strings.UnpinMessageAlert, Strings.AppName, Strings.OK, Strings.Cancel);
                if (confirm == ContentDialogResult.Primary)
                {
                    if (DirectMessagesChatTopic != null)
                    {
                        ClientService.Send(new UnpinAllDirectMessagesChatTopicMessages(chat.Id, DirectMessagesChatTopic.Id));
                    }
                    else if (ForumTopic != null)
                    {
                        ClientService.Send(new UnpinAllForumTopicMessages(chat.Id, ForumTopic.Info.ForumTopicId));
                    }
                    else
                    {
                        ClientService.Send(new UnpinAllChatMessages(chat.Id));
                    }

                    Delegate?.UpdatePinnedMessage(chat, false);
                }
            }
            else
            {
                Settings.SetChatPinnedMessage(chat.Id, int.MaxValue);
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

            if (TryGetFirstVisibleMessageId(out long firstVisibleId))
            {
                PinnedMessages.LoadSlice(firstVisibleId);
            }
        }

        public void OpenPinnedMessages()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (TopicId != null)
            {
                NavigationService.Navigate(typeof(ChatPinnedPage), new ChatMessageTopic(chat.Id, TopicId));
            }
            else
            {
                NavigationService.Navigate(typeof(ChatPinnedPage), chat.Id);
            }
        }

        #endregion

        public void HideSponsoredMessage(MessageViewModel message = null)
        {
            if (IsPremium)
            {
                if (message != null)
                {
                    Items.Remove(message);
                }
                else
                {
                    for (int i = Items.Count - 1; i >= 0; i--)
                    {
                        if (Items[i].Content is MessageSponsored)
                        {
                            Items.RemoveAt(i);

                            // TODO: multiple ads
                            break;
                        }
                    }
                }

                SponsoredMessage = null;
                ClientService.Send(new ToggleHasSponsoredMessagesEnabled(false));

                ToastPopup.Show(XamlRoot, Strings.AdHidden, ToastPopupIcon.AntiSpam);
            }
            else if (IsPremiumAvailable)
            {
                NavigationService.ShowPromo(new PremiumSourceFeature(new PremiumFeatureDisabledAds()));
            }
        }

        public void ViewSponsoredMessage()
        {
            var chat = _chat;
            var message = _sponsoredMessage;

            if (chat == null || message == null)
            {
                return;
            }

            ClientService.Send(new ViewMessages(chat.Id, new[] { message.MessageId }, new MessageSourceChatHistory(), true));
        }

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
            else if (InlineBotResults.Button.Type is InlineQueryResultsButtonTypeWebApp webApp && _currentInlineBot is User botUser)
            {
                var response = await ClientService.SendAsync(new GetWebAppUrl(botUser.Id, webApp.Url, new WebAppOpenParameters(Theme.Current.Parameters, Constants.WebAppHostName, new WebAppOpenModeFullSize())));
                if (response is HttpUrl httpUrl)
                {
                    NavigationService.NavigateToWebApp(botUser, httpUrl.Url, sourceChat: Chat);
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

        public void Invite()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup or ChatTypeBasicGroup)
            {
                ShowPopup(new ChooseChatsPopup(), new ChooseChatsConfigurationInviteToChat(chat.Id));
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

        #region Show read date

        public async void ShowReadDate()
        {
            if (ClientService.TryGetUser(_chat, out User user))
            {
                var popup = new ChangePrivacyPopup(user, ChangePrivacyType.ReadDate, IsPremium, IsPremiumAvailable);

                var confirm = await ShowPopupAsync(popup);
                if (confirm == ContentDialogResult.Primary)
                {
                    ClientService.Send(new SetReadDatePrivacySettings(new ReadDatePrivacySettings(true)));
                    ShowToast(Strings.PremiumReadSet, ToastPopupIcon.Info);
                }
                else if (confirm == ContentDialogResult.Secondary && IsPremiumAvailable && !IsPremium)
                {
                    NavigationService.ShowPromo(new PremiumSourceFeature(new PremiumFeatureAdvancedChatManagement()));
                }
            }
        }

        #endregion

        #region Search

        public void SearchExecute(string query, MessageSender from = null)
        {
            if (Search == null || !string.IsNullOrEmpty(query) || from != null)
            {
                Search = new ChatSearchViewModel(ClientService, NavigationService, Settings, Aggregator, this, query, from);
            }
            else
            {
                // TODO: focus and select search bar
                Search = null;
            }
        }

        #endregion

        #region Jump to date

        public async void JumpDate()
        {
            var dialog = new CalendarPopup(ClientService, ChatId, TopicId);
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

            if (ForumTopic != null)
            {
                ClientService.Send(new ReadAllForumTopicMentions(chat.Id, ForumTopic.Info.ForumTopicId));
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

            if (DirectMessagesChatTopic != null)
            {
                ClientService.Send(new ReadAllDirectMessagesChatTopicReactions(chat.Id, DirectMessagesChatTopic.Id));
            }
            else if (ForumTopic != null)
            {
                ClientService.Send(new ReadAllForumTopicReactions(chat.Id, ForumTopic.Info.ForumTopicId));
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
                _notificationsService.SetMuteFor(chat, update, XamlRoot);
            }
            else
            {
                var muteFor = Settings.Notifications.GetMuteFor(chat);
                var popup = new ChatMutePopup(muteFor);

                var confirm = await ShowPopupAsync(popup);
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }

                if (muteFor != popup.Value)
                {
                    _notificationsService.SetMuteFor(chat, popup.Value, XamlRoot);
                }
            }
        }

        public void SetSound(bool silent)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            _notificationsService.SetSound(chat, silent, XamlRoot);
        }

        #endregion

        #region Report Chat

        private ReportChatSelection _isReportingMessages;
        public ReportChatSelection IsReportingMessages
        {
            get => _isReportingMessages;
            set => Set(ref _isReportingMessages, value);
        }

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

            ReportChatPopup popup;
            if (IsReportingMessages is ReportChatSelection selection)
            {
                popup = new ReportChatPopup(ClientService, NavigationService, chat.Id, IsReportingMessages?.Option, messages, selection.Text);
            }
            else
            {
                popup = new ReportChatPopup(ClientService, NavigationService, chat.Id, null, messages, string.Empty);
            }

            var report = await popup.ReportAsync();
            if (report?.Result is ReportChatResultMessagesRequired)
            {
                ShowHideSelection(true, report);
            }
            else if (IsReportingMessages != null)
            {
                ShowHideSelection(false);
            }
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
            if (confirm != ContentDialogResult.Primary || chat.MessageAutoDeleteTime == dialog.Value)
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

        #region Unpin Messages

        public async void UnpinMessages()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.CanPinMessages(ClientService))
            {
                var confirm = await ShowPopupAsync(Strings.UnpinMessageAlert, Strings.AppName, Strings.OK, Strings.Cancel);
                if (confirm == ContentDialogResult.Primary)
                {
                    if (DirectMessagesChatTopic != null)
                    {
                        ClientService.Send(new UnpinAllDirectMessagesChatTopicMessages(chat.Id, DirectMessagesChatTopic.Id));
                    }
                    else if (ForumTopic != null)
                    {
                        ClientService.Send(new UnpinAllForumTopicMessages(chat.Id, ForumTopic.Info.ForumTopicId));
                    }
                    else
                    {
                        ClientService.Send(new UnpinAllChatMessages(chat.Id));
                    }

                    Delegate?.UpdatePinnedMessage(chat, false);
                }
            }
            else
            {
                Settings.SetChatPinnedMessage(chat.Id, int.MaxValue);
                Delegate?.UpdatePinnedMessage(chat, false);
            }
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

            if (ClientService.FreezeState.IsFrozen)
            {
                ShowPopup(new FrozenPopup(ClientService.FreezeState));
            }
            else if (Search?.SavedMessagesTag != null)
            {
                Search.FilterByTag = !Search.FilterByTag;
            }
            else if (Type == DialogType.EventLog)
            {
                FilterExecute();
            }
            else if (SavedMessagesTopic != null)
            {
                if (SavedMessagesTopic.Type is SavedMessagesTopicTypeSavedFromChat savedFromChat && ClientService.TryGetChat(savedFromChat.ChatId, out Chat savedChat))
                {
                    NavigationService.NavigateToChat(savedChat);
                }
            }
            else if (Type == DialogType.Pinned)
            {
                UnpinMessages();
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
                else if (chat.Id == ClientService.Options.RepliesBotChatId || chat.Id == ClientService.Options.VerificationCodesBotChatId)
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
                    if (Constants.DEBUG)
                    {
                        ClientService.Send(new AddLocalMessage(chat.Id, new MessageSenderChat(chat.Id), null, true, new InputMessageContact(new Contact("999888777666", "SIMILAR", "CHANNELS", string.Empty, ClientService.Options.MyId))));
                        return;
                    }

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
                    else if (ClientService.TryGetSupergroupFull(group.Id, out SupergroupFullInfo fullInfo))
                    {
                        if (fullInfo.MyBoostCount < fullInfo.UnrestrictBoostCount)
                        {
                            Boost(fullInfo.UnrestrictBoostCount);
                        }
                    }
                }
            }
        }

        #endregion

        #region Join requests

        public async void ShowJoinRequests()
        {
            var popup = new ChatJoinRequestsPopup(ClientService, NavigationService, Settings, Aggregator, _chat, string.Empty);
            await ShowPopupAsync(popup);
        }

        #endregion

        #region Group calls

        public void JoinGroupCall()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            _voipService.JoinGroupCall(NavigationService, chat.Id);
        }

        #endregion
    }

    public partial class UserCommand
    {
        public UserCommand(long userId, BotCommand command)
        {
            UserId = userId;
            Item = command;
        }

        public long UserId { get; set; }
        public BotCommand Item { get; set; }
    }

    [Flags]
    public enum DialogType
    {
        History,
        Thread,
        Pinned,
        ScheduledMessages,
        BusinessReplies,
        EventLog
    }
}
