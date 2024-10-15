//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
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
    public partial class ChooseChatsViewModel : ViewModelBase
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

            ChatList chatList = ClientService.MainChatListPosition > 0 && ClientService.ChatFolders.Count > 0
                ? new ChatListFolder(ClientService.ChatFolders[0].Id)
                : new ChatListMain();

            if (ClientService.ChatFolders.Count > 0)
            {
                var folders = ClientService.ChatFolders.ToList();
                var index = Math.Min(ClientService.MainChatListPosition, folders.Count);

                folders.Insert(index, new ChatFolderInfo { Id = Constants.ChatListMain, Title = Strings.FilterAllChats, Icon = new ChatFolderIcon("All") });

                Folders = new ObservableCollection<ChatFolderViewModel>(folders.Select(x => new ChatFolderViewModel(x)));

                foreach (var folder in Folders)
                {
                    if (folder.ChatList is ChatListMain)
                    {
                        continue;
                    }

                    var unreadCount = ClientService.GetUnreadCount(folder.ChatList);
                    if (unreadCount == null)
                    {
                        continue;
                    }

                    folder.UpdateCount(unreadCount.UnreadChatCount);
                }

                // Important not to raise SelectedFolder setter
                Set(ref _selectedFolder, Folders.FirstOrDefault());
            }
            else
            {
                Folders = new ObservableCollection<ChatFolderViewModel>();
            }
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

        private ChooseChatsConfiguration _configuration;
        public ChooseChatsConfiguration Configuration => _configuration;

        public override INavigationService NavigationService
        {
            get => base.NavigationService;
            set
            {
                SearchChats.NavigationService = value;
                base.NavigationService = value;
            }
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            // The following is absolutely awful

            #region Configuration

            _configuration = parameter as ChooseChatsConfiguration;

            if (parameter is ChooseChatsConfigurationGroupCall configurationGroupCall)
            {
                SelectionMode = ListViewSelectionMode.Multiple;
                Options = ChooseChatsOptions.PostMessages;
                PrimaryButtonText = Strings.Send;
                IsCommentEnabled = true;
                IsChatSelection = false;
            }
            else if (parameter is ChooseChatsConfigurationDataPackage configurationDataPackage)
            {
                SelectionMode = ListViewSelectionMode.Single;
                Options = ChooseChatsOptions.PostMessages;
                PrimaryButtonText = Strings.Send;
                IsCommentEnabled = false;
                IsChatSelection = false;
            }
            else if (parameter is ChooseChatsConfigurationSwitchInline configurationSwitchInline)
            {
                SelectionMode = ListViewSelectionMode.Single;
                Options = ChooseChatsOptions.PostMessages;
                IsCommentEnabled = false;
                IsChatSelection = false;

                if (configurationSwitchInline.TargetChat is TargetChatChosen chosen)
                {
                    Options.AllowBotChats = chosen.AllowBotChats;
                    Options.AllowUserChats = chosen.AllowUserChats;
                    Options.AllowGroupChats = chosen.AllowGroupChats;
                    Options.AllowChannelChats = chosen.AllowChannelChats;
                }
            }
            else if (parameter is ChooseChatsConfigurationPostText configurationPostText)
            {
                SelectionMode = ListViewSelectionMode.Single;
                Options = ChooseChatsOptions.PostMessages;
                PrimaryButtonText = Strings.Send;
                IsCommentEnabled = true;
                IsChatSelection = false;

                SendMessage = configurationPostText.Text;
            }
            else if (parameter is ChooseChatsConfigurationReplyToMessage)
            {
                SelectionMode = ListViewSelectionMode.None;
                Options = ChooseChatsOptions.PostMessages;
                IsCommentEnabled = false;
                IsSendAsCopyEnabled = false;
                IsChatSelection = false;
            }
            else if (parameter is ChooseChatsConfigurationShareMessage configurationShareMessage)
            {
                SelectionMode = ListViewSelectionMode.Multiple;
                Options = ChooseChatsOptions.PostMessages;
                PrimaryButtonText = Strings.Send;
                IsCommentEnabled = true;
                IsSendAsCopyEnabled = true;
                IsChatSelection = false;

                // TODO: sharing links isn't currently supported anyway
                //Messages = new[] { configurationShareMessage.Message };
                //IsWithMyScore = configurationShareMessage.WithMyScore;

                //var message = configurationShareMessage.Message;
                //var chat = ClientService.GetChat(configurationShareMessage.ChatId);

                //if (ClientService.TryGetSupergroup(chat, out Supergroup supergroup)
                //    && supergroup.HasActiveUsername(out string username))
                //{
                //    var link = $"{username}/{message.Id}";

                //    if (message.Content is MessageVideoNote)
                //    {
                //        link = $"https://telesco.pe/{link}";
                //    }
                //    else
                //    {
                //        link = MeUrlPrefixConverter.Convert(ClientService, link);
                //    }

                //    var title = message.GetCaption()?.Text;
                //    if (message.Content is MessageText text)
                //    {
                //        title = text.Text.Text;
                //    }

                //    ShareLink = new HttpUrl(link);
                //}
                //else if (message.Content is MessageGame game)
                //{
                //    var viaBot = ClientService.GetUser(message.ViaBotUserId);
                //    if (viaBot != null && viaBot.HasActiveUsername(out username))
                //    {
                //        ShareLink = new HttpUrl(MeUrlPrefixConverter.Convert(ClientService, $"{username}?game={game.Game.ShortName}"));
                //    }
                //}
            }
            else if (parameter is ChooseChatsConfigurationShareStory)
            {
                SelectionMode = ListViewSelectionMode.Multiple;
                Options = ChooseChatsOptions.PostMessages;
                PrimaryButtonText = Strings.Send;
                IsCommentEnabled = true;
                IsSendAsCopyEnabled = true;
                IsChatSelection = false;
            }
            else if (parameter is ChooseChatsConfigurationShareMessages configurationShareMessages)
            {
                SelectionMode = ListViewSelectionMode.Multiple;
                Options = ChooseChatsOptions.PostMessages;
                PrimaryButtonText = Strings.Send;
                IsCommentEnabled = true;
                IsSendAsCopyEnabled = true;
                IsChatSelection = false;
            }
            else if (parameter is ChooseChatsConfigurationPostLink configurationPostLink)
            {
                SelectionMode = ListViewSelectionMode.Multiple;
                Options = ChooseChatsOptions.PostMessages;
                PrimaryButtonText = Strings.Send;
                IsCommentEnabled = true;
                IsChatSelection = false;

                ShareLink = configurationPostLink.Url;
            }
            else if (parameter is ChooseChatsConfigurationPostMessage configurationPostMessage)
            {
                SelectionMode = ListViewSelectionMode.Multiple;
                Options = ChooseChatsOptions.PostMessages;
                PrimaryButtonText = Strings.Send;
                IsCommentEnabled = true;
                IsChatSelection = false;
            }
            else if (parameter is ChooseChatsConfigurationStartBot configurationStartBot)
            {
                SelectionMode = ListViewSelectionMode.Single;
                Options = ChooseChatsOptions.GroupsAndChannels;
                IsCommentEnabled = false;
                IsChatSelection = false;

                Title = Strings.AddToGroupOrChannel;
            }
            else if (parameter is ChooseChatsConfigurationRequestUsers configurationRequestUsers)
            {
                SelectionMode = configurationRequestUsers.MaxQuantity != 1
                    ? ListViewSelectionMode.Multiple
                    : ListViewSelectionMode.Single;
                Options = new ChooseChatsOptionsRequestUsers(configurationRequestUsers);
                IsCommentEnabled = false;
                IsChatSelection = false;

                Title = configurationRequestUsers.MaxQuantity != 1
                    ? Strings.ChooseUsers
                    : Strings.ChooseUsers;
            }
            else if (parameter is ChooseChatsConfigurationRequestChat configurationRequestChat)
            {
                SelectionMode = ListViewSelectionMode.Single;
                Options = new ChooseChatsOptionsRequestChat(configurationRequestChat);
                IsCommentEnabled = false;
                IsChatSelection = false;

                Title = configurationRequestChat.ChatIsChannel
                    ? Strings.ChooseChannel
                    : Strings.ChooseGroup;
            }

            #endregion

            LoadChats();
            return Task.CompletedTask;
        }

        private async void LoadChats()
        {
            if (Options == null)
            {
                return;
            }

            var chatList = SelectedFolder?.ChatList ?? new ChatListMain();

            var response = await ClientService.GetChatListAsync(chatList, 0, 200);
            if (response is Telegram.Td.Api.Chats chats)
            {
                var list = ClientService.GetChats(chats.ChatIds).ToList();
                var preIndex = 0;

                Items.Clear();

                if (chatList is ChatListMain && Options.AllowSelf && (Options.AllowAll || Options.CanPostMessages))
                {
                    var myId = ClientService.Options.MyId;
                    var self = list.FirstOrDefault(x => x.Type is ChatTypePrivate privata && privata.UserId == myId);
                    self ??= await ClientService.SendAsync(new CreatePrivateChat(myId, false)) as Chat;

                    if (self != null)
                    {
                        preIndex = 1;

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
                            items.Insert(preIndex, chat);
                        }
                    }
                    else if (items.Count > 0)
                    {
                        items.Insert(preIndex, chat);
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

        private string _primaryButtonText = Strings.Done;
        public string PrimaryButtonText
        {
            get => _primaryButtonText;
            set => Set(ref _primaryButtonText, value);
        }

        private FormattedText _caption;
        public FormattedText Caption
        {
            get => _caption;
            set => Set(ref _caption, value);
        }

        public bool IsCopyLinkEnabled
        {
            get
            {
                return ShareLink != null && DataTransferManager.IsSupported();
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

        private HttpUrl _shareLink;
        public HttpUrl ShareLink
        {
            get => _shareLink;
            set
            {
                Set(ref _shareLink, value);
                RaisePropertyChanged(nameof(IsCopyLinkEnabled));
            }
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

        public FormattedText SendMessage { get; set; }

        public bool IsChatSelection { get; set; }
        public IList<long> PreSelectedItems { get; set; }

        public MvxObservableCollection<Chat> Items { get; private set; }

        public ObservableCollection<ChatFolderViewModel> Folders { get; }

        public Dictionary<long, long> SelectedTopics = new();

        private ChatFolderViewModel _selectedFolder;
        public ChatFolderViewModel SelectedFolder
        {
            get => _selectedFolder;
            set
            {
                if (Set(ref _selectedFolder, value))
                {
                    LoadChats();
                }
            }
        }

        //private async Task<IList<Chat>> GetChatsFromSelectionAsync()
        //{
        //    List<Chat> results = null;

        //    foreach (var item in SelectedItems)
        //    {
        //        if (item.Chat != null)
        //        {
        //            results.Add(item.Chat);
        //        }
        //        else if (item.User != null)
        //        {
        //            if (ClientService.TryGetChatFromUser(item.User.Id, out Chat cached))
        //            {
        //                results.Add(cached);
        //            }
        //            else
        //            {
        //                var response = await ClientService.SendAsync(new CreatePrivateChat(item.User.Id, false));
        //                if (response is Chat chat)
        //                {
        //                    results.Add(chat);
        //                }
        //            }
        //        }
        //    }

        //    return results;
        //}

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
                IsCommentEnabled = true;
                Caption = SendMessage;
            }

            if (IsCommentEnabled && !string.IsNullOrEmpty(Caption?.Text))
            {
                foreach (var chat in chats)
                {
                    SelectedTopics.TryGetValue(chat.Id, out long messageThreadId);
                    var response = await ClientService.SendAsync(new SendMessage(chat.Id, messageThreadId, null, null, null, new InputMessageText(_caption, null, false)));
                }
            }

            if (_configuration is ChooseChatsConfigurationReplyToMessage replyToMessage)
            {
                NavigationService.NavigateToChat(chats[0], state: new NavigationState
                {
                    { "reply_to", replyToMessage.Message },
                    { "reply_to_quote", replyToMessage.Quote }
                });
            }
            else if (_configuration is ChooseChatsConfigurationShareMessages shareMessages)
            {
                ShowForwardMessagesToast(chats, shareMessages.MessageIds.Count);

                foreach (var chat in chats)
                {
                    foreach (var messages in shareMessages.MessageIds.GroupBy(x => x.ChatId))
                    {
                        SelectedTopics.TryGetValue(chat.Id, out long messageThreadId);
                        ClientService.Send(new ForwardMessages(chat.Id, messageThreadId, messages.Key, messages.Select(x => x.Id).ToList(), null, _sendAsCopy || _removeCaptions, _removeCaptions));
                    }
                }
            }
            else if (_configuration is ChooseChatsConfigurationShareMessage shareMessage)
            {
                ShowForwardMessagesToast(chats, 1);

                foreach (var chat in chats)
                {
                    SelectedTopics.TryGetValue(chat.Id, out long messageThreadId);
                    ClientService.Send(new SendMessage(chat.Id, messageThreadId, null, null, null, new InputMessageForwarded(shareMessage.ChatId, shareMessage.MessageId, shareMessage.WithMyScore, new MessageCopyOptions(_sendAsCopy || _removeCaptions, _removeCaptions, null, false))));
                }
            }
            else if (_configuration is ChooseChatsConfigurationShareStory shareStory)
            {
                ShowForwardStoryToast(chats);

                foreach (var chat in chats)
                {
                    SelectedTopics.TryGetValue(chat.Id, out long messageThreadId);
                    ClientService.Send(new SendMessage(chat.Id, messageThreadId, null, null, null, new InputMessageStory(shareStory.ChatId, shareStory.StoryId)));
                }
            }
            else if (_configuration is ChooseChatsConfigurationPostMessage postMessage)
            {
                foreach (var chat in chats)
                {
                    SelectedTopics.TryGetValue(chat.Id, out long messageThreadId);
                    ClientService.Send(new SendMessage(chat.Id, messageThreadId, null, null, null, postMessage.Content));
                }

                //NavigationService.GoBack();
            }
            else if (_configuration is ChooseChatsConfigurationPostLink postLink && postLink.InternalLink != null)
            {
                var response = await ClientService.SendAsync(new GetInternalLink(postLink.InternalLink, true));
                if (response is HttpUrl httpUrl)
                {
                    var formatted = new FormattedText(httpUrl.Url, Array.Empty<TextEntity>());

                    foreach (var chat in chats)
                    {
                        SelectedTopics.TryGetValue(chat.Id, out long messageThreadId);
                        ClientService.Send(new SendMessage(chat.Id, messageThreadId, null, null, null, new InputMessageText(formatted, null, false)));
                    }
                }
            }
            else if (ShareLink != null)
            {
                var formatted = new FormattedText(ShareLink.Url, Array.Empty<TextEntity>());

                foreach (var chat in chats)
                {
                    SelectedTopics.TryGetValue(chat.Id, out long messageThreadId);
                    ClientService.Send(new SendMessage(chat.Id, messageThreadId, null, null, null, new InputMessageText(formatted, null, false)));
                }

                //NavigationService.GoBack();
            }
            else if (_configuration is ChooseChatsConfigurationStartBot startBot)
            {
                var chat = chats.FirstOrDefault();
                if (chat == null)
                {
                    return;
                }

                var response = await ClientService.SendAsync(new GetChatMember(chat.Id, new MessageSenderUser(startBot.Bot.Id)));
                if (response is ChatMember member && member.Status is ChatMemberStatusLeft)
                {
                    await ClientService.SendAsync(new SetChatMemberStatus(chat.Id, new MessageSenderUser(startBot.Bot.Id), new ChatMemberStatusMember()));
                }

                if (startBot.Token != null)
                {
                    response = await ClientService.SendAsync(new SendBotStartMessage(startBot.Bot.Id, chat.Id, startBot.Token));
                    NavigationService.NavigateToChat(chat, accessToken: startBot.Token);
                }
            }
            else if (_configuration is ChooseChatsConfigurationSwitchInline switchInline)
            {
                NavigationService.NavigateToChat(chats[0], state: NavigationState.GetSwitchQuery(switchInline.Query, switchInline.Bot.Id));
            }
            else if (_configuration is ChooseChatsConfigurationDataPackage configurationDataPackage)
            {
                NavigationService.NavigateToChat(chats[0], state: new NavigationState
                {
                    { "package", configurationDataPackage.Package }
                });
            }
            else if (_configuration is ChooseChatsConfigurationGroupCall groupCall)
            {
                var response = await ClientService.SendAsync(new GetGroupCallInviteLink(groupCall.GroupCallId, false));
                if (response is HttpUrl httpUrl)
                {
                    var formatted = new FormattedText(string.Format(Strings.VoipGroupInviteText, httpUrl.Url), Array.Empty<TextEntity>());

                    foreach (var chat in chats)
                    {
                        await ClientService.SendAsync(new SendMessage(chat.Id, 0, null, null, null, new InputMessageText(formatted, null, false)));
                    }
                }
            }
            else if (_configuration is ChooseChatsConfigurationRequestUsers requestUsers)
            {
                var userIds = chats
                    .Select(x => x.Type is ChatTypePrivate privata ? privata.UserId : 0)
                    .Where(x => x != 0)
                    .ToList();
                ClientService.Send(new ShareUsersWithBot(requestUsers.ChatId, requestUsers.MessageId, requestUsers.Id, userIds, false));
            }

            //App.InMemoryState.ForwardMessages = new List<TLMessage>(messages);
            //NavigationService.GoBackAt(0);
        }

        private ListViewSelectionMode _selectionMode = ListViewSelectionMode.Multiple;
        public ListViewSelectionMode SelectionMode
        {
            get => _selectionMode;
            set => Set(ref _selectionMode, value);
        }

        private void ShowForwardMessagesToast(IList<Chat> chats, int messagesCount)
        {
            if (chats.Count == 1)
            {
                var chat = chats[0];
                if (chat.IsUser(out long userId))
                {
                    if (userId == ClientService.Options.MyId)
                    {
                        ShowToast(messagesCount > 1
                            ? Strings.FwdMessagesToSavedMessages
                            : Strings.FwdMessageToSavedMessages, ToastPopupIcon.SavedMessages);
                    }
                    else
                    {
                        ShowToast(messagesCount > 1
                            ? string.Format(Strings.FwdMessagesToUser, chat.Title)
                            : string.Format(Strings.FwdMessageToUser, chat.Title), ToastPopupIcon.Forward);
                    }
                }
                else if (chat.IsBasicGroup(out _))
                {
                    ShowToast(messagesCount > 1
                        ? string.Format(Strings.FwdMessagesToGroup, chat.Title)
                        : string.Format(Strings.FwdMessageToGroup, chat.Title), ToastPopupIcon.Forward);

                }
                else if (chat.IsSupergroup(out _, out bool isChannel))
                {
                    if (isChannel)
                    {
                        ShowToast(messagesCount > 1
                            ? string.Format(Strings.FwdMessagesToChats, chat.Title)
                            : string.Format(Strings.FwdMessageToChats, chat.Title), ToastPopupIcon.Forward);
                    }
                    else
                    {
                        ShowToast(messagesCount > 1
                            ? string.Format(Strings.FwdMessagesToGroup, chat.Title)
                            : string.Format(Strings.FwdMessageToGroup, chat.Title), ToastPopupIcon.Forward);
                    }
                }
            }
            else
            {
                ShowToast(messagesCount > 1
                    ? Locale.Declension(Strings.R.FwdMessagesToManyChats, chats.Count)
                    : Locale.Declension(Strings.R.FwdMessageToManyChats, chats.Count), ToastPopupIcon.Forward);
            }
        }

        private void ShowForwardStoryToast(IList<Chat> chats)
        {
            if (chats.Count == 1)
            {
                if (ClientService.IsSavedMessages(chats[0]))
                {
                    ShowToast(Strings.StorySharedToSavedMessages, ToastPopupIcon.SavedMessages);
                }
                else
                {
                    ShowToast(string.Format(Strings.StorySharedTo, chats[0].Title), ToastPopupIcon.Forward);
                }
            }
            else
            {
                ShowToast(Locale.Declension(Strings.R.StorySharedToManyChats, chats.Count), ToastPopupIcon.Forward);
            }
        }

    }

    public partial class ChooseChatsTracker
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

            if (Options.Allow(_clientService, chat))
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

        public bool Filter(User user)
        {
            if (_knownUsers != null && _knownUsers.Contains(user.Id))
            {
                return false;
            }

            if (Options.Allow(_clientService, user))
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
    }
}
