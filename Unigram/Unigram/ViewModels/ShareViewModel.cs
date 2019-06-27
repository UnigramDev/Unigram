using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Template10.Common;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
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

            var response = await ProtoService.SendAsync(new GetChats(long.MaxValue, 0, int.MaxValue));
            if (response is Telegram.Td.Api.Chats chats)
            {
                var list = ProtoService.GetChats(chats.ChatIds);
                Items.Clear();

                if (_searchType != SearchChatsType.BasicAndSupergroups)
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
                    else
                    {
                        if (CacheService.CanPostMessages(chat))
                        {
                            Items.Add(chat);
                        }
                    }
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
        }



        public MvxObservableCollection<Chat> Items { get; private set; }



        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var chats = SelectedItems.ToList();
            if (chats.Count == 0)
            {
                return;
            }

            if (!string.IsNullOrEmpty(_comment))
            {
                var formatted = GetFormattedText(_comment);

                foreach (var chat in chats)
                {
                    var response = await ProtoService.SendAsync(new SendMessage(chat.Id, 0, false, false, null, new InputMessageText(formatted, false, false)));
                }
            }

            if (_messages != null)
            {
                if (_sendAsCopy)
                {
                    foreach (var message in _messages)
                    {
                        if (!_messageFactory.CanBeCopied(message))
                        {
                            var confirm = await TLMessageDialog.ShowAsync("Polls, voice and video notes can't be forwarded as copy. Do you want to proceed anyway?", Strings.Resources.AppName, Strings.Resources.Forward, Strings.Resources.Cancel);
                            if (confirm != ContentDialogResult.Primary)
                            {
                                return;
                            }
                        }
                    }
                }

                foreach (var chat in chats)
                {
                    if (IsWithMyScore)
                    {
                        var response = await ProtoService.SendAsync(new SendMessage(chat.Id, 0, false, false, null, new InputMessageForwarded(_messages[0].ChatId, _messages[0].Id, true)));
                    }
                    else
                    {
                        //var response = await ProtoService.SendAsync(new ForwardMessages(chat.Id, _messages[0].ChatId, _messages.Select(x => x.Id).ToList(), false, false, true));
                        await _messageFactory.ForwardMessagesAsync(chat.Id, _messages[0].ChatId, _messages, _sendAsCopy, !_removeCaptions);
                    }
                }

                //NavigationService.GoBack();
            }
            else if (_inputMedia != null)
            {
                foreach (var chat in chats)
                {
                    var response = await ProtoService.SendAsync(new SendMessage(chat.Id, 0, false, false, null, _inputMedia));
                }

                //NavigationService.GoBack();
            }
            else if (_shareLink != null)
            {
                var formatted = new FormattedText(_shareLink.AbsoluteUri, new TextEntity[0]);

                foreach (var chat in chats)
                {
                    var response = await ProtoService.SendAsync(new SendMessage(chat.Id, 0, false, false, null, new InputMessageText(formatted, false, false)));
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

            var entities = Markdown.Parse(ref text);
            if (entities == null)
            {
                entities = new List<TextEntity>();
            }

            return new FormattedText(text, entities);

            //return ProtoService.Execute(new ParseTextEntities(text.Format(), new TextParseModeMarkdown())) as FormattedText;
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

        public async void Find(string text)
        {
            var results = await SearchLocalAsync(text);
            if (results != null)
            {
                SelectionMode = ListViewSelectionMode.None;
                //Items.ReplaceWith(results.Cast<TLDialog>().Select(x => x.With));
            }
            else
            {
                //var dialogs = GetDialogs();
                //if (dialogs != null)
                //{
                //    foreach (var item in _selectedItems)
                //    {
                //        //dialogs.Remove(item);
                //        //dialogs.Insert(0, item);

                //        //if (dialogs.Contains(item)) { }
                //        //else
                //        //{
                //        //    dialogs.Insert(0, item);
                //        //}
                //    }

                //    SelectionMode = ListViewSelectionMode.None;
                //    //Items.ReplaceWith(dialogs);
                //}
            }
        }

        private async Task<KeyedList<string, System.Object>> SearchLocalAsync(string query1)
        {
            //if (string.IsNullOrWhiteSpace(query1))
            //{
            //    return null;
            //}

            //var dialogs = await Task.Run(() => CacheService.GetDialogs());
            //var contacts = await Task.Run(() => CacheService.GetContacts());

            //if (dialogs != null && contacts != null)
            //{
            //    var query = LocaleHelper.GetQuery(query1);

            //    var simple = new List<TLDialog>();
            //    var parent = dialogs.Where(dialog =>
            //    {
            //        if (dialog.With is TLUser user)
            //        {
            //            return user.IsLike(query, StringComparison.OrdinalIgnoreCase);
            //        }
            //        else if (dialog.With is TLChannel channel)
            //        {
            //            return channel.IsLike(query, StringComparison.OrdinalIgnoreCase);
            //        }
            //        else if (dialog.With is TLChat chat)
            //        {
            //            return !chat.HasMigratedTo && chat.IsLike(query, StringComparison.OrdinalIgnoreCase);
            //        }
            //        else
            //        {
            //            return false;
            //        }
            //    }).ToList();

            //    var contactsResults = contacts.OfType<TLUser>().Where(x => x.IsLike(query, StringComparison.OrdinalIgnoreCase));

            //    foreach (var result in contactsResults)
            //    {
            //        var dialog = parent.FirstOrDefault(x => x.Peer.TypeId == TLType.PeerUser && x.Id == result.Id);
            //        if (dialog == null)
            //        {
            //            simple.Add(new TLDialog
            //            {
            //                With = result,
            //                Peer = new TLPeerUser { UserId = result.Id }
            //            });
            //        }
            //    }

            //    if (parent.Count > 0 || simple.Count > 0)
            //    {
            //        return new KeyedList<string, TLObject>(null, parent.Union(simple.OrderBy(x => x.With.DisplayName)));
            //    }
            //}

            return null;
        }

        #endregion
    }
}
