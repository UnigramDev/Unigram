using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Navigation;
using Unigram.Services;
using Unigram.Services.Factories;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class ShareViewModel : TLViewModelBase
    {
        private readonly IMessageFactory _messageFactory;

        public ShareViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, IMessageFactory messageFactory)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _messageFactory = messageFactory;

            Items = new MvxObservableCollection<Chat>();
            SelectedItems = new MvxObservableCollection<Chat>();

            SendCommand = new RelayCommand(SendExecute, () => SelectedItems?.Count > 0);
        }

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

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            //if (mode == NavigationMode.New)
            //{
            //    _dialogs = null;
            //}

            //var response = await ProtoService.SendAsync(new GetChats(new ChatListMain(), long.MaxValue, 0, int.MaxValue));
            var response = await ProtoService.GetChatListAsync(new ChatListMain(), 0, 200);
            if (response is Telegram.Td.Api.Chats chats)
            {
                var list = ProtoService.GetChats(chats.ChatIds);
                Items.Clear();

                if (_searchType == SearchChatsType.Post || _searchType == SearchChatsType.All)
                {
                    var myId = CacheService.Options.MyId;
                    var self = list.FirstOrDefault(x => x.Type is ChatTypePrivate privata && privata.UserId == myId);
                    if (self == null)
                    {
                        self = await ProtoService.SendAsync(new CreatePrivateChat(myId, false)) as Chat;
                    }

                    if (self != null)
                    {
                        list.Remove(self);
                        list.Insert(0, self);
                    }
                }

                foreach (var chat in list)
                {
                    if (_searchType == SearchChatsType.BasicAndSupergroups)
                    {
                        if (chat.Type is ChatTypeBasicGroup basic)
                        {
                            var basicGroup = ProtoService.GetBasicGroup(basic.BasicGroupId);
                            if (basicGroup == null)
                            {
                                continue;
                            }

                            if (basicGroup.CanInviteUsers())
                            {
                                Items.Add(chat);
                            }
                        }
                        else if (chat.Type is ChatTypeSupergroup super)
                        {
                            var supergroup = ProtoService.GetSupergroup(super.SupergroupId);
                            if (supergroup == null)
                            {
                                continue;
                            }

                            if (supergroup.CanInviteUsers())
                            {
                                Items.Add(chat);
                            }
                        }
                    }
                    else if (_searchType == SearchChatsType.PrivateAndGroups)
                    {
                        if (chat.Type is ChatTypePrivate || chat.Type is ChatTypeBasicGroup || chat.Type is ChatTypeSupergroup supergroup && !supergroup.IsChannel)
                        {
                            Items.Add(chat);
                        }
                    }
                    else if (_searchType == SearchChatsType.Private)
                    {
                        if (chat.Type is ChatTypePrivate)
                        {
                            Items.Add(chat);
                        }
                    }
                    else if (_searchType == SearchChatsType.Post)
                    {
                        if (CacheService.CanPostMessages(chat))
                        {
                            Items.Add(chat);
                        }
                    }
                    else
                    {
                        Items.Add(chat);
                    }
                }

                var pre = PreSelectedItems;
                if (pre == null)
                {
                    return;
                }

                var items = Items;
                var selectedItems = SelectedItems;

                foreach (var id in pre)
                {
                    var chat = CacheService.GetChat(id);
                    if (chat == null)
                    {
                        chat = await ProtoService.SendAsync(new GetChat(id)) as Chat;
                    }

                    if (chat == null)
                    {
                        continue;
                    }

                    selectedItems.Add(chat);

                    var index = items.IndexOf(chat);
                    if (index > -1)
                    {
                        if (index > 0)
                        {
                            items.Remove(chat);
                            items.Insert(1, chat);
                        }
                    }
                    else if (items.Count > 0)
                    {
                        items.Insert(1, chat);
                    }
                }

                if (PreSelectedItems.Count > 0 && SelectionMode == ListViewSelectionMode.Multiple)
                {
                    RaisePropertyChanged(nameof(PreSelectedItems));
                }
            }
        }

        private MvxObservableCollection<Chat> _selectedItems = new MvxObservableCollection<Chat>();
        public MvxObservableCollection<Chat> SelectedItems
        {
            get
            {
                return _selectedItems;
            }
            set
            {
                Set(ref _selectedItems, value);
                SendCommand?.RaiseCanExecuteChanged();
            }
        }

        private bool _allowEmptySelection = false;
        public bool AllowEmptySelection
        {
            get => _allowEmptySelection;
            set => Set(ref _allowEmptySelection, value);
        }

        private string _title = Strings.Resources.ShareSendTo;
        public string Title
        {
            get => _title;
            set => Set(ref _title, value);
        }

        private string _comment;
        public string Comment
        {
            get
            {
                return _comment;
            }
            set
            {
                Set(ref _comment, value);
            }
        }

        private IList<Message> _messages;
        public IList<Message> Messages
        {
            get
            {
                return _messages;
            }
            set
            {
                Set(ref _messages, value);
            }
        }

        private User _inviteBot;
        public User InviteBot
        {
            get
            {
                return _inviteBot;
            }
            set
            {
                Set(ref _inviteBot, value);
            }
        }

        private string _inviteToken;
        public string InviteToken
        {
            get
            {
                return _inviteToken;
            }
            set
            {
                Set(ref _inviteToken, value);
            }
        }

        private InputMessageContent _inputMedia;
        public InputMessageContent InputMedia
        {
            get
            {
                return _inputMedia;
            }
            set
            {
                Set(ref _inputMedia, value);
            }
        }

        public bool IsWithMyScore { get; set; }

        public bool IsCopyLinkEnabled
        {
            get
            {
                return _shareLink != null && DataTransferManager.IsSupported();
            }
        }

        private bool _sendAsCopy;
        public bool SendAsCopy
        {
            get { return _sendAsCopy; }
            set { Set(ref _sendAsCopy, value); }
        }

        private bool _removeCaptions;
        public bool RemoveCaptions
        {
            get { return _removeCaptions; }
            set { Set(ref _removeCaptions, value); }
        }

        private Uri _shareLink;
        public Uri ShareLink
        {
            get
            {
                return _shareLink;
            }
            set
            {
                Set(ref _shareLink, value);
                RaisePropertyChanged(() => IsCopyLinkEnabled);
            }
        }

        private string _shareTitle;
        public string ShareTitle
        {
            get
            {
                return _shareTitle;
            }
            set
            {
                Set(ref _shareTitle, value);
            }
        }

        private bool _isCommentEnabled;
        public bool IsCommentEnabled
        {
            get
            {
                return _isCommentEnabled;
            }
            set
            {
                Set(ref _isCommentEnabled, value);
            }
        }

        private bool _isSendCopyEnabled;
        public bool IsSendAsCopyEnabled
        {
            get { return _isSendCopyEnabled; }
            set { Set(ref _isSendCopyEnabled, value); }
        }

        private SearchChatsType _searchType;
        public SearchChatsType SearchType
        {
            get
            {
                return _searchType;
            }
            set
            {
                Set(ref _searchType, value);
            }
        }

        private InlineKeyboardButtonTypeSwitchInline _switchInline;
        public InlineKeyboardButtonTypeSwitchInline SwitchInline
        {
            get { return _switchInline; }
            set { _switchInline = value; }
        }

        private User _switchInlineBot;
        public User SwitchInlineBot
        {
            get { return _switchInlineBot; }
            set { _switchInlineBot = value; }
        }

        private DataPackageView _package;
        public DataPackageView Package
        {
            get { return _package; }
            set { _package = value; }
        }

        public string SendMessage { get; set; }
        public bool SendMessageUrl { get; set; }

        public bool IsChatSelection { get; set; }
        public IList<long> PreSelectedItems { get; set; }

        public void Clear()
        {
            Package = null;
            SwitchInline = null;
            SwitchInlineBot = null;
            SendMessage = null;
            SendMessageUrl = false;
            Comment = null;
            ShareLink = null;
            ShareTitle = null;
            Messages = null;
            InviteBot = null;
            InputMedia = null;
            IsWithMyScore = false;
            PreSelectedItems = null;
        }



        public MvxObservableCollection<Chat> Items { get; private set; }



        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var chats = SelectedItems.ToList();
            if (chats.Count == 0 || IsChatSelection)
            {
                return;
            }

            if (!string.IsNullOrEmpty(_comment))
            {
                var formatted = GetFormattedText(_comment);

                foreach (var chat in chats)
                {
                    var response = await ProtoService.SendAsync(new SendMessage(chat.Id, 0, 0, new MessageSendOptions(false, false, null), null, new InputMessageText(formatted, false, false)));
                }
            }

            if (_messages != null)
            {
                foreach (var chat in chats)
                {
                    if (IsWithMyScore)
                    {
                        var response = await ProtoService.SendAsync(new SendMessage(chat.Id, 0, 0, new MessageSendOptions(false, false, null), null, new InputMessageForwarded(_messages[0].ChatId, _messages[0].Id, true, new MessageCopyOptions(false, false, null))));
                    }
                    else
                    {
                        var album = false;

                        var first = _messages.FirstOrDefault();
                        if (first != null)
                        {
                            album = first.MediaAlbumId != 0 && _messages.All(x => x.MediaAlbumId == first.MediaAlbumId);
                        }

                        var response = await ProtoService.SendAsync(new ForwardMessages(chat.Id, _messages[0].ChatId, _messages.Select(x => x.Id).ToList(), new MessageSendOptions(false, false, null), _sendAsCopy, _removeCaptions));
                    }
                }

                //NavigationService.GoBack();
            }
            else if (_inputMedia != null)
            {
                foreach (var chat in chats)
                {
                    var response = await ProtoService.SendAsync(new SendMessage(chat.Id, 0, 0, new MessageSendOptions(false, false, null), null, _inputMedia));
                }

                //NavigationService.GoBack();
            }
            else if (_shareLink != null)
            {
                var formatted = new FormattedText(_shareLink.AbsoluteUri, new TextEntity[0]);

                foreach (var chat in chats)
                {
                    var response = await ProtoService.SendAsync(new SendMessage(chat.Id, 0, 0, new MessageSendOptions(false, false, null), null, new InputMessageText(formatted, false, false)));
                }

                //NavigationService.GoBack();
            }
            else if (_inviteBot != null)
            {
                var chat = chats.FirstOrDefault();
                if (chat == null)
                {
                    return;
                }

                var response = await ProtoService.SendAsync(new SetChatMemberStatus(chat.Id, _inviteBot.Id, new ChatMemberStatusMember()));
                if (response is Ok)
                {
                    if (_inviteToken != null)
                    {
                        response = await ProtoService.SendAsync(new SendBotStartMessage(_inviteBot.Id, chat.Id, _inviteToken));

                        var service = WindowContext.GetForCurrentView().NavigationServices.GetByFrameId("Main" + ProtoService.SessionId);
                        if (service != null)
                        {
                            service.NavigateToChat(chat, accessToken: _inviteToken);
                        }
                    }
                    //NavigationService.GoBack();
                }
            }
            else if (_switchInline != null && _switchInlineBot != null)
            {
                var chat = chats.FirstOrDefault();
                if (chat == null)
                {
                    return;
                }

                var state = new Dictionary<string, object>();
                state["switch_query"] = _switchInline.Query;
                state["switch_bot"] = _switchInlineBot.Id;

                //NavigationService.GoBack();

                var service = WindowContext.GetForCurrentView().NavigationServices.GetByFrameId("Main" + ProtoService.SessionId);
                if (service != null)
                {
                    service.NavigateToChat(chat, state: state);
                }
            }
            else if (_package != null)
            {
                var chat = chats.FirstOrDefault();
                if (chat == null)
                {
                    return;
                }

                App.DataPackages[chat.Id] = _package;

                var service = WindowContext.GetForCurrentView().NavigationServices.GetByFrameId("Main" + ProtoService.SessionId);
                if (service != null)
                {
                    service.NavigateToChat(chat);
                }
            }

            //App.InMemoryState.ForwardMessages = new List<TLMessage>(messages);
            //NavigationService.GoBackAt(0);
        }

        private FormattedText GetFormattedText(string text)
        {
            if (text == null)
            {
                return new FormattedText();
            }

            text = text.Format();
            return Client.Execute(new ParseMarkdown(new FormattedText(text, new TextEntity[0]))) as FormattedText;
        }

        #region Search

        private ListViewSelectionMode _selectionMode = ListViewSelectionMode.Multiple;
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

        #endregion
    }
}
