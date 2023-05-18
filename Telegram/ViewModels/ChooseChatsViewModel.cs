//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels
{
    public class ChooseChatsViewModel : ViewModelBase
    {
        private readonly ChooseChatsTracker _tracker;

        public ChooseChatsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            _tracker = new ChooseChatsTracker(clientService, false);
            SearchChats = new SearchChatsViewModel(clientService, settingsService, aggregator);
            SearchChats.Options = _tracker.Options;

            Items = new MvxObservableCollection<Chat>();
            SelectedItems = new MvxObservableCollection<Chat>();

            SendCommand = new RelayCommand(SendExecute, () => SelectedItems?.Count > 0);
        }

        public SearchChatsViewModel SearchChats { get; }

        public ChooseChatsOptions Options
        {
            get => _tracker.Options;
            set
            {
                _tracker.Options = value;
                SearchChats.Options = value;
            }
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var response = await ClientService.GetChatListAsync(new ChatListMain(), 0, 200);
            if (response is Telegram.Td.Api.Chats chats)
            {
                var list = ClientService.GetChats(chats.ChatIds).ToList();
                Items.Clear();

                if (Options.AllowAll || Options.CanPostMessages)
                {
                    var myId = ClientService.Options.MyId;
                    var self = list.FirstOrDefault(x => x.Type is ChatTypePrivate privata && privata.UserId == myId);
                    self ??= await ClientService.SendAsync(new CreatePrivateChat(myId, false)) as Chat;

                    if (self != null)
                    {
                        list.Remove(self);
                        list.Insert(0, self);
                    }
                }

                foreach (var chat in list)
                {
                    if (_tracker.Filter(chat))
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
                    var chat = ClientService.GetChat(id);
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
            get => _selectedItems;
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

        private string _title = Strings.ShareSendTo;
        public string Title
        {
            get => _title;
            set => Set(ref _title, value);
        }

        private FormattedText _caption;
        public FormattedText Caption
        {
            get => _caption;
            set => Set(ref _caption, value);
        }

        private IList<Message> _messages;
        public IList<Message> Messages
        {
            get => _messages;
            set => Set(ref _messages, value);
        }

        private GroupCall _groupCall;
        public GroupCall GroupCall
        {
            get => _groupCall;
            set
            {
                Set(ref _groupCall, value);
                RaisePropertyChanged(nameof(IsSpeakerLinkEnabled));
            }
        }

        public bool IsSpeakerLinkEnabled
        {
            get => _groupCall != null && _groupCall.CanBeManaged && _groupCall.MuteNewParticipants;
        }

        private bool _isSpeakerLink;
        public bool IsSpeakerLink
        {
            get => _isSpeakerLink;
            set => Set(ref _isSpeakerLink, value);
        }

        private User _inviteBot;
        public User InviteBot
        {
            get => _inviteBot;
            set => Set(ref _inviteBot, value);
        }

        private string _inviteToken;
        public string InviteToken
        {
            get => _inviteToken;
            set => Set(ref _inviteToken, value);
        }

        private InputMessageContent _inputMedia;
        public InputMessageContent InputMedia
        {
            get => _inputMedia;
            set => Set(ref _inputMedia, value);
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
            get => _sendAsCopy;
            set => Set(ref _sendAsCopy, value);
        }

        private bool _removeCaptions;
        public bool RemoveCaptions
        {
            get => _removeCaptions;
            set => Set(ref _removeCaptions, value);
        }

        private Uri _shareLink;
        public Uri ShareLink
        {
            get => _shareLink;
            set
            {
                Set(ref _shareLink, value);
                RaisePropertyChanged(nameof(IsCopyLinkEnabled));
            }
        }

        private string _shareTitle;
        public string ShareTitle
        {
            get => _shareTitle;
            set => Set(ref _shareTitle, value);
        }

        private bool _isCommentEnabled;
        public bool IsCommentEnabled
        {
            get => _isCommentEnabled;
            set => Set(ref _isCommentEnabled, value);
        }

        private bool _isSendCopyEnabled;
        public bool IsSendAsCopyEnabled
        {
            get => _isSendCopyEnabled;
            set => Set(ref _isSendCopyEnabled, value);
        }

        private InlineKeyboardButtonTypeSwitchInline _switchInline;
        public InlineKeyboardButtonTypeSwitchInline SwitchInline
        {
            get => _switchInline;
            set => _switchInline = value;
        }

        private User _switchInlineBot;
        public User SwitchInlineBot
        {
            get => _switchInlineBot;
            set => _switchInlineBot = value;
        }

        private DataPackageView _package;
        public DataPackageView Package
        {
            get => _package;
            set => _package = value;
        }

        public FormattedText SendMessage { get; set; }

        public bool IsChatSelection { get; set; }
        public IList<long> PreSelectedItems { get; set; }

        public MvxObservableCollection<Chat> Items { get; private set; }



        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var chats = SelectedItems.ToList();
            if (chats.Count == 0 || IsChatSelection)
            {
                return;
            }

            if (!string.IsNullOrEmpty(SendMessage?.Text))
            {
                _isCommentEnabled = true;
                _caption = SendMessage;
            }

            if (_isCommentEnabled && !string.IsNullOrEmpty(_caption?.Text))
            {
                foreach (var chat in chats)
                {
                    var response = await ClientService.SendAsync(new SendMessage(chat.Id, 0, 0, null, null, new InputMessageText(_caption, false, false)));
                }
            }

            if (_messages != null)
            {
                foreach (var chat in chats)
                {
                    if (IsWithMyScore)
                    {
                        var response = await ClientService.SendAsync(new SendMessage(chat.Id, 0, 0, null, null, new InputMessageForwarded(_messages[0].ChatId, _messages[0].Id, true, new MessageCopyOptions(false, false, null))));
                    }
                    else
                    {
                        var album = false;

                        var first = _messages.FirstOrDefault();
                        if (first != null)
                        {
                            album = first.MediaAlbumId != 0 && _messages.All(x => x.MediaAlbumId == first.MediaAlbumId);
                        }

                        var response = await ClientService.SendAsync(new ForwardMessages(chat.Id, 0, _messages[0].ChatId, _messages.Select(x => x.Id).ToList(), null, _sendAsCopy || _removeCaptions, _removeCaptions, false));
                    }
                }

                //NavigationService.GoBack();
            }
            else if (_inputMedia != null)
            {
                foreach (var chat in chats)
                {
                    var response = await ClientService.SendAsync(new SendMessage(chat.Id, 0, 0, null, null, _inputMedia));
                }

                //NavigationService.GoBack();
            }
            else if (_shareLink != null)
            {
                var formatted = new FormattedText(_shareLink.AbsoluteUri, new TextEntity[0]);

                foreach (var chat in chats)
                {
                    var response = await ClientService.SendAsync(new SendMessage(chat.Id, 0, 0, null, null, new InputMessageText(formatted, false, false)));
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

                var response = await ClientService.SendAsync(new GetChatMember(chat.Id, new MessageSenderUser(_inviteBot.Id)));
                if (response is ChatMember member && member.Status is ChatMemberStatusLeft)
                {
                    await ClientService.SendAsync(new SetChatMemberStatus(chat.Id, new MessageSenderUser(_inviteBot.Id), new ChatMemberStatusMember()));
                }

                if (_inviteToken != null)
                {
                    response = await ClientService.SendAsync(new SendBotStartMessage(_inviteBot.Id, chat.Id, _inviteToken));

                    var service = WindowContext.Current.NavigationServices.GetByFrameId("Main" + ClientService.SessionId);
                    service?.NavigateToChat(chat, accessToken: _inviteToken);
                }
            }
            else if (_switchInline != null && _switchInlineBot != null)
            {
                var chat = chats.FirstOrDefault();
                if (chat == null)
                {
                    return;
                }

                var service = WindowContext.Current.NavigationServices.GetByFrameId("Main" + ClientService.SessionId);
                service?.NavigateToChat(chat, state: NavigationState.GetSwitchQuery(_switchInline.Query, _switchInlineBot.Id));
            }
            else if (_package != null)
            {
                var chat = chats.FirstOrDefault();
                if (chat == null)
                {
                    return;
                }

                App.DataPackages[chat.Id] = _package;

                var service = WindowContext.Current.NavigationServices.GetByFrameId("Main" + ClientService.SessionId);
                service?.NavigateToChat(chat);
            }
            else if (_groupCall != null)
            {
                var response = await ClientService.SendAsync(new GetGroupCallInviteLink(_groupCall.Id, _isSpeakerLink));
                if (response is HttpUrl httpUrl)
                {
                    var formatted = new FormattedText(string.Format(Strings.VoipGroupInviteText, httpUrl.Url), new TextEntity[0]);

                    foreach (var chat in chats)
                    {
                        await ClientService.SendAsync(new SendMessage(chat.Id, 0, 0, null, null, new InputMessageText(formatted, false, false)));
                    }
                }
            }

            //App.InMemoryState.ForwardMessages = new List<TLMessage>(messages);
            //NavigationService.GoBackAt(0);
        }

        #region Search

        private ListViewSelectionMode _selectionMode = ListViewSelectionMode.Multiple;
        public ListViewSelectionMode SelectionMode
        {
            get => _selectionMode;
            set => Set(ref _selectionMode, value);
        }

        #endregion
    }

    public class ChooseChatsTracker
    {
        private readonly IClientService _clientService;

        private readonly HashSet<long> _knownChats;
        private readonly HashSet<long> _knownUsers;

        public ChooseChatsTracker(IClientService clientService, bool track)
        {
            _clientService = clientService;

            if (track)
            {
                _knownChats = new();
                _knownUsers = new();
            }
        }

        public ChooseChatsOptions Options { get; set; }

        public void Clear()
        {
            _knownChats?.Clear();
            _knownUsers?.Clear();
        }

        public bool Filter(Chat chat)
        {
            if (_knownChats != null && _knownChats.Contains(chat.Id))
            {
                return false;
            }
            else if (_knownUsers != null && chat.Type is ChatTypePrivate privata && _knownUsers.Contains(privata.UserId))
            {
                return false;
            }

            if (Options.AllowAll || Allow(chat))
            {
                Track(chat);
                return true;
            }

            return false;
        }

        private void Track(Chat chat)
        {
            _knownChats?.Add(chat.Id);

            if (chat.Type is ChatTypePrivate privata)
            {
                _knownUsers?.Add(privata.UserId);
            }
        }

        private bool Allow(Chat chat)
        {
            switch (chat.Type)
            {
                case ChatTypeBasicGroup:
                    if (Options.AllowGroupChats)
                    {
                        if (Options.CanPostMessages)
                        {
                            return _clientService.CanPostMessages(chat);
                        }
                        else if (Options.CanInviteUsers)
                        {
                            return _clientService.CanInviteUsers(chat);
                        }

                        return true;
                    }
                    return false;
                case ChatTypePrivate privata:
                    if (_clientService.TryGetUser(privata.UserId, out User user))
                    {
                        if (user.Type is UserTypeBot)
                        {
                            return Options.AllowBotChats;
                        }
                    }
                    return Options.AllowUserChats;
                case ChatTypeSecret:
                    return Options.AllowSecretChats;
                case ChatTypeSupergroup supergroup:
                    if (supergroup.IsChannel ? Options.AllowChannelChats : Options.AllowGroupChats)
                    {
                        if (Options.CanPostMessages)
                        {
                            return _clientService.CanPostMessages(chat);
                        }
                        else if (Options.CanInviteUsers)
                        {
                            return _clientService.CanInviteUsers(chat);
                        }

                        return true;
                    }
                    return false;
                default:
                    return false;
            }
        }

        public bool Filter(User user)
        {
            if (_knownUsers != null && _knownUsers.Contains(user.Id))
            {
                return false;
            }

            if (Options.AllowAll || Allow(user))
            {
                Track(user);
                return true;
            }

            return false;
        }

        private void Track(User user)
        {
            _knownUsers?.Add(user.Id);
        }

        private bool Allow(User user)
        {
            if (user.Type is UserTypeBot)
            {
                return Options.AllowBotChats;
            }

            return Options.AllowUserChats;
        }
    }
}
