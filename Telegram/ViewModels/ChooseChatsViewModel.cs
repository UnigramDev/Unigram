//
// Copyright (c) Fela Ameghino 2015-2026
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
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Telegram.Views.Supergroups.Popups;
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

            Folders = new ObservableCollection<ChatFolderViewModel>();

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

            switch (parameter)
            {
                case ChooseChatsConfigurationGroupCall:
                    SelectionMode = ListViewSelectionMode.Multiple;
                    Options = ChooseChatsOptions.PostMessages;
                    PrimaryButtonText = Strings.Send;
                    IsCommentEnabled = true;
                    IsChatSelection = false;
                    break;
                case ChooseChatsConfigurationShareOperation:
                    SelectionMode = ListViewSelectionMode.Multiple;
                    Options = ChooseChatsOptions.PostMessages;
                    IsCommentEnabled = true;
                    IsChatSelection = false;
                    break;
                case ChooseChatsConfigurationSwitchInline configurationSwitchInline:
                    {
                        SelectionMode = ListViewSelectionMode.None;
                        Options = ChooseChatsOptions.PostMessages;
                        IsCommentEnabled = false;
                        IsChatSelection = false;

                        if (configurationSwitchInline.TargetChat is TargetChatChosen chosen)
                        {
                            Options.AllowBotChats = chosen.Types.AllowBotChats;
                            Options.AllowUserChats = chosen.Types.AllowUserChats;
                            Options.AllowGroupChats = chosen.Types.AllowGroupChats;
                            Options.AllowChannelChats = chosen.Types.AllowChannelChats;
                        }

                        break;
                    }

                case ChooseChatsConfigurationPostText configurationPostText:
                    SelectionMode = ListViewSelectionMode.None;
                    Options = ChooseChatsOptions.PostMessages;
                    PrimaryButtonText = Strings.Send;
                    IsCommentEnabled = true;
                    IsChatSelection = false;

                    SendMessage = configurationPostText.Text;
                    break;
                case ChooseChatsConfigurationReplyToMessage:
                    SelectionMode = ListViewSelectionMode.None;
                    Options = ChooseChatsOptions.PostMessages;
                    IsCommentEnabled = false;
                    IsChatSelection = false;
                    break;
                case ChooseChatsConfigurationShareGame:
                    SelectionMode = ListViewSelectionMode.Multiple;
                    Options = ChooseChatsOptions.PostMessages;
                    PrimaryButtonText = Strings.Send;
                    IsCommentEnabled = true;
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
                    break;
                case ChooseChatsConfigurationInviteToChat configurationInviteToChat:
                    {
                        SelectionMode = ListViewSelectionMode.Multiple;
                        Options = ChooseChatsOptions.InviteUsers;
                        PrimaryButtonText = Strings.Done;
                        IsCommentEnabled = false;
                        IsChatSelection = false;

                        if (ClientService.TryGetChat(configurationInviteToChat.ChatId, out Chat chat))
                        {
                            Title = chat.Type is ChatTypeSupergroup { IsChannel: true }
                                ? Strings.AddSubscriber
                                : Strings.AddMember;
                        }

                        break;
                    }

                case ChooseChatsConfigurationShareStory:
                    SelectionMode = ListViewSelectionMode.Multiple;
                    Options = ChooseChatsOptions.PostMessages;
                    PrimaryButtonText = Strings.Send;
                    IsCommentEnabled = true;
                    IsChatSelection = false;
                    break;
                case ChooseChatsConfigurationShareMessages:
                    SelectionMode = ListViewSelectionMode.Multiple;
                    Options = ChooseChatsOptions.PostMessages;
                    PrimaryButtonText = Strings.Send;
                    IsCommentEnabled = true;
                    IsChatSelection = false;
                    break;
                case ChooseChatsConfigurationPostLink configurationPostLink:
                    SelectionMode = ListViewSelectionMode.Multiple;
                    Options = ChooseChatsOptions.PostMessages;
                    PrimaryButtonText = Strings.Send;
                    IsCommentEnabled = true;
                    IsChatSelection = false;

                    ShareLink = configurationPostLink.Url;
                    break;
                case ChooseChatsConfigurationPostMessage:
                    SelectionMode = ListViewSelectionMode.Multiple;
                    Options = ChooseChatsOptions.PostMessages;
                    PrimaryButtonText = Strings.Send;
                    IsCommentEnabled = true;
                    IsChatSelection = false;
                    break;
                case ChooseChatsConfigurationPostLogs:
                    SelectionMode = ListViewSelectionMode.Multiple;
                    Options = ChooseChatsOptions.PostMessages;
                    PrimaryButtonText = Strings.Send;
                    IsCommentEnabled = true;
                    IsChatSelection = false;
                    break;
                case ChooseChatsConfigurationStartBot:
                    SelectionMode = ListViewSelectionMode.None;
                    Options = ChooseChatsOptions.GroupsAndChannels;
                    IsCommentEnabled = false;
                    IsChatSelection = false;

                    Title = Strings.AddToGroupOrChannel;
                    break;
                case ChooseChatsConfigurationSetTheme:
                    SelectionMode = ListViewSelectionMode.None;
                    Options = new ChooseChatsOptions()
                    {
                        AllowChannelChats = false,
                        AllowGroupChats = false,
                        AllowBotChats = false,
                        AllowUserChats = true,
                        AllowSecretChats = false,
                        AllowSelf = false,
                        CanPostMessages = false,
                        CanInviteUsers = false,
                        CanShareContact = false,
                        Mode = ChooseChatsMode.Chats,
                        ShowMessages = false
                    };
                    IsCommentEnabled = false;
                    IsChatSelection = false;

                    Title = Strings.SelectChat;
                    break;
                case ChooseChatsConfigurationRequestUsers configurationRequestUsers:
                    SelectionMode = configurationRequestUsers.MaxQuantity != 1
                            ? ListViewSelectionMode.Multiple
                            : ListViewSelectionMode.None;
                    Options = new ChooseChatsOptionsRequestUsers(configurationRequestUsers);
                    IsCommentEnabled = false;
                    IsChatSelection = false;

                    if (configurationRequestUsers.RestrictUserIsBot && configurationRequestUsers.UserIsBot)
                    {
                        Title = Strings.ChooseBot;
                    }
                    else
                    {
                        Title = configurationRequestUsers.MaxQuantity != 1
                            ? Strings.ChooseUsers
                            : Strings.ChooseUser;
                    }
                    break;
                case ChooseChatsConfigurationRequestChat configurationRequestChat:
                    SelectionMode = ListViewSelectionMode.None;
                    Options = new ChooseChatsOptionsRequestChat(configurationRequestChat);
                    IsCommentEnabled = false;
                    IsChatSelection = false;

                    Title = configurationRequestChat.ChatIsChannel
                        ? Strings.ChooseChannel
                        : Strings.ChooseGroup;
                    break;
                case ChooseChatsConfigurationVerifyChat:
                    SelectionMode = ListViewSelectionMode.None;
                    Options = ChooseChatsOptions.All;
                    ShouldCloseOnCommit = false;
                    IsCommentEnabled = false;
                    IsChatSelection = false;

                    Title = Strings.BotChooseChatToVerify;
                    break;
                case ChooseChatsConfigurationTransferGift transferGift:
                    {
                        SelectionMode = ListViewSelectionMode.None;
                        Options = ChooseChatsOptions.UsersAndChannels;
                        ShouldCloseOnCommit = false;
                        IsCommentEnabled = false;
                        IsChatSelection = false;

                        if (transferGift.Gift.Gift is SentGiftUpgraded upgraded)
                        {
                            Title = string.Format(Strings.Gift2Transfer, upgraded.Gift.ToName());
                        }

                        break;
                    }

                case ChooseChatsConfigurationCreateGroupCall:
                    SelectionMode = ListViewSelectionMode.Multiple;
                    Options = ChooseChatsOptions.Contacts;
                    IsCommentEnabled = false;
                    IsChatSelection = false;

                    Title = Strings.NewCall;
                    break;
                case ChooseChatsConfigurationBotAddToChannel:
                    SelectionMode = ListViewSelectionMode.None;
                    Options = ChooseChatsOptions.ChannelsCanPromoteMembers;
                    IsCommentEnabled = false;
                    IsChatSelection = false;
                    break;
            }

            #endregion

            if (IsCommentEnabled || parameter is ChooseChatsConfigurationReplyToMessage)
            {
                LoadFolders();
            }

            LoadChats();
            return Task.CompletedTask;
        }

        private void LoadFolders()
        {
            ChatList chatList = ClientService.MainChatListPosition > 0 && ClientService.ChatFolders.Count > 0
                ? new ChatListFolder(ClientService.ChatFolders[0].Id)
                : new ChatListMain();

            if (ClientService.ChatFolders.Count > 0)
            {
                var folders = ClientService.ChatFolders.ToList();
                var index = Math.Min(ClientService.MainChatListPosition, folders.Count);

                folders.Insert(index, new ChatFolderInfo
                {
                    Id = Constants.ChatListMain,
                    Name = new ChatFolderName(Strings.FilterAllChats.AsFormattedText(), false),
                    Icon = new ChatFolderIcon("All")
                });

                Folders = new ObservableCollection<ChatFolderViewModel>(folders.Select(x => new ChatFolderViewModel(ClientService, x)));

                foreach (var folder in Folders)
                {
                    var unreadCount = ClientService.GetUnreadCount(folder.ChatList);
                    if (unreadCount == null)
                    {
                        continue;
                    }

                    folder.UpdateCount(unreadCount.UnreadChatCount, Settings.Notifications.IncludeMutedChatsInFolderCounters);
                }

                // Important not to raise SelectedFolder setter
                Set(ref _selectedFolder, Folders.FirstOrDefault());
            }
            else
            {
                Folders = new ObservableCollection<ChatFolderViewModel>();
            }
        }

        private async void LoadChats()
        {
            if (Options == null)
            {
                return;
            }

            if (SelectedItems.Count > 0)
            {
                PreSelectedItems = new List<long>(SelectedItems.Select(x => x.Id));
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

        private MvxObservableCollection<Chat> _selectedItems = new();
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

        public bool SendDisableNotifications { get; set; }
        public MessageSchedulingState SendSchedulingState { get; set; }

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

        public FormattedText SendMessage { get; set; }

        public bool IsChatSelection { get; set; }
        public IList<long> PreSelectedItems { get; set; }

        public MvxObservableCollection<Chat> Items { get; private set; }

        public ObservableCollection<ChatFolderViewModel> Folders { get; private set; }

        public Dictionary<long, MessageTopic> SelectedTopics = new();

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

        public async Task<bool> ConfirmPaidMessagesAsync()
        {
            if (_configuration != null)
            {
                var confirm = await _configuration.ConfirmSelectionAsync(this, SelectedItems);
                return confirm == ContentDialogResult.Primary;
            }

            return true;
        }

        private Task<ContentDialogResult> ShowPaidMessageConfirmationAsync(IList<Chat> chats, int messageCount)
        {
            int chatCount = 0;
            long starCount = 0;

            foreach (var chat in chats)
            {
                var paidMessageStarCount = 0L;

                if (ClientService.TryGetUserFull(chat, out UserFullInfo userFullInfo))
                {
                    paidMessageStarCount = userFullInfo.OutgoingPaidMessageStarCount;
                }
                else if (ClientService.TryGetSupergroup(chat, out Supergroup supergroup))
                {
                    paidMessageStarCount = supergroup.PaidMessageStarCount;
                }

                if (paidMessageStarCount > 0)
                {
                    chatCount++;
                    starCount += paidMessageStarCount;
                }
            }

            if (starCount != 0)
            {
                if (!string.IsNullOrEmpty(SendMessage?.Text) || !string.IsNullOrEmpty(Caption?.Text))
                {
                    messageCount++;
                }

                var message1 = Locale.Declension(Strings.R.MessageLockedStarsConfirmMessageMulti1, chatCount);
                var message3 = Locale.Declension(Strings.R.MessageLockedStarsConfirmMessageMulti2Messages, chatCount * messageCount);
                var message2 = Locale.Declension(Strings.R.MessageLockedStarsConfirmMessageMulti2, starCount * messageCount, message3);

                var title = Strings.MessageLockedStarsConfirmTitle;
                var message = string.Format("{0} {1}", message1, message2);
                var primaryButtonText = Icons.Premium16 + Icons.Spacing + (starCount * messageCount).ToString("N0"); //Locale.Declension(Strings.R.MessageLockedStarsConfirmMessagePay, messageCount),
                var secondaryButtonText = Strings.Cancel;

                return ShowPopupAsync(message, title, primaryButtonText, secondaryButtonText);
            }

            return Task.FromResult(ContentDialogResult.Primary);
        }

        public void SendWithChat(Chat chat, Action<MessageSendOptions, MessageTopic> action)
        {
            _ = ClientService.PaidMessageStarCount(chat);
            var options = new MessageSendOptions(null, SendDisableNotifications, false, 0, false, SendSchedulingState, 0, 0, false);

            SelectedTopics.TryGetValue(chat.Id, out MessageTopic topic);
            action(options, topic);
        }

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
                    SendWithChat(chat, (options, topic) =>
                    {
                        ClientService.Send(new SendMessage(chat.Id, topic, null, options, new InputMessageText(_caption, null, false)));
                    });
                }
            }

            if (_configuration is ChooseChatsConfigurationReplyToMessage replyToMessage)
            {
                SelectedTopics.TryGetValue(chats[0].Id, out MessageTopic topic);
                NavigationService.NavigateToChat(chats[0], topic: topic, state: new NavigationState
                {
                    { "reply_to", replyToMessage.Message },
                    { "reply_to_quote", replyToMessage.Quote },
                    { "reply_to_task_id", replyToMessage.ChecklistTaskId },
                    { "reply_to_option_id", replyToMessage.PollOptionId },
                });
            }
            else if (_configuration is ChooseChatsConfigurationShareGame shareGame)
            {
                ShowForwardMessagesToast(chats, 1);

                foreach (var chat in chats)
                {
                    SendWithChat(chat, (options, topic) =>
                    {
                        ClientService.Send(new SendMessage(chat.Id, topic, null, options, new InputMessageForwarded(shareGame.Messages[0].ChatId, shareGame.Messages[0].Id, shareGame.WithMyScore, false, 0, new MessageCopyOptions(_sendAsCopy || _removeCaptions, _removeCaptions, null, false))));
                    });
                }
            }
            else if (_configuration is ChooseChatsConfigurationShareMessages shareMessages)
            {
                ShowForwardMessagesToast(chats, shareMessages.Messages.Count);

                foreach (var chat in chats)
                {
                    foreach (var messages in shareMessages.Messages.GroupBy(x => x.ChatId))
                    {
                        SendWithChat(chat, (options, topic) =>
                        {
                            ClientService.Send(new ForwardMessages(chat.Id, topic, messages.Key, messages.Select(x => x.Id).ToList(), options, _sendAsCopy || _removeCaptions, _removeCaptions));
                        });
                    }
                }
            }
            else if (_configuration is ChooseChatsConfigurationShareStory shareStory)
            {
                ShowForwardStoryToast(chats);

                foreach (var chat in chats)
                {
                    SendWithChat(chat, (options, topic) =>
                    {
                        ClientService.Send(new SendMessage(chat.Id, topic, null, options, new InputMessageStory(shareStory.ChatId, shareStory.StoryId)));
                    });
                }
            }
            else if (_configuration is ChooseChatsConfigurationPostMessage postMessage)
            {
                foreach (var chat in chats)
                {
                    SendWithChat(chat, (options, topic) =>
                    {
                        ClientService.Send(new SendMessage(chat.Id, topic, null, options, postMessage.Content));
                    });
                }

                //NavigationService.GoBack();
            }
            else if (_configuration is ChooseChatsConfigurationPostLogs postLogs)
            {
                var content = new InputMessageDocument(new InputDocument(new InputFileLocal(postLogs.Path), null, true), null);
                var verbosityLevel = SettingsService.Current.VerbosityLevel;

                Client.Execute(new SetLogVerbosityLevel(0));

                foreach (var chat in chats)
                {
                    SendWithChat(chat, (options, topic) =>
                    {
                        ClientService.Send(new SendMessage(chat.Id, topic, null, options, content), result =>
                        {
                            if (result is Message { Content: MessageDocument document })
                            {
                                ClientService.PrepareLogs(document.Document.DocumentValue.Id, verbosityLevel);
                            }
                        });
                    });
                }
            }
            else if (_configuration is ChooseChatsConfigurationPostLink postLink && postLink.InternalLink != null)
            {
                var response = await ClientService.SendAsync(new GetInternalLink(postLink.InternalLink, true));
                if (response is HttpUrl httpUrl)
                {
                    var formatted = httpUrl.Url.AsFormattedText();

                    foreach (var chat in chats)
                    {
                        SendWithChat(chat, (options, topic) =>
                        {
                            ClientService.Send(new SendMessage(chat.Id, topic, null, options, new InputMessageText(formatted, null, false)));
                        });
                    }
                }
            }
            else if (ShareLink != null)
            {
                var formatted = ShareLink.Url.AsFormattedText();

                foreach (var chat in chats)
                {
                    SendWithChat(chat, (options, topic) =>
                    {
                        ClientService.Send(new SendMessage(chat.Id, topic, null, options, new InputMessageText(formatted, null, false)));
                    });
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
                if (switchInline.Result != null)
                {
                    ShowForwardMessagesToast(chats, 1);

                    foreach (var chat in chats)
                    {
                        SendWithChat(chat, (options, topic) =>
                        {
                            ClientService.Send(new SendInlineQueryResultMessage(chat.Id, topic, null, options, switchInline.InlineQueryId, switchInline.Result.GetId(), false));
                        });
                    }
                }
                else
                {
                    NavigationService.NavigateToChat(chats[0], state: NavigationState.GetSwitchQuery(switchInline.Query, switchInline.Bot.Id));
                }
            }
            else if (_configuration is ChooseChatsConfigurationGroupCall groupCall)
            {
                var response = await ClientService.SendAsync(new GetVideoChatInviteLink(groupCall.GroupCallId, false));
                if (response is HttpUrl httpUrl)
                {
                    FormattedText formatted;
                    if (groupCall.IsRtmpStream)
                    {
                        formatted = httpUrl.Url.AsFormattedText();
                    }
                    else
                    {
                        formatted = string.Format(Strings.VoipGroupInviteText, httpUrl.Url).AsFormattedText();
                    }

                    foreach (var chat in chats)
                    {
                        SendWithChat(chat, (options, topic) =>
                        {
                            ClientService.Send(new SendMessage(chat.Id, topic, null, options, new InputMessageText(formatted, null, false)));
                        });
                    }
                }
            }
            else if (_configuration is ChooseChatsConfigurationRequestUsers requestUsers)
            {
                var userIds = chats
                    .Select(x => x.Type is ChatTypePrivate privata ? privata.UserId : 0)
                    .Where(x => x != 0)
                    .ToList();
                ClientService.Send(new ShareUsersWithBot(requestUsers.Source, requestUsers.Id, userIds, false));
            }
            else if (_configuration is ChooseChatsConfigurationVerifyChat verifyChat && ClientService.TryGetUserFull(verifyChat.BotUserId, out UserFullInfo verifyChatFullInfo))
            {
                var chat = chats[0];
                var verifiedId = chats[0].ToMessageSender();

                var verification = await ClientService.GetBotVerificationAsync(chat);
                if (verification?.BotUserId == verifyChat.BotUserId)
                {
                    var confirm = await VerifyChatPopup.ShowAsync(XamlRoot, ClientService, chat, true, false);
                    if (confirm.Result == ContentDialogResult.Primary)
                    {
                        NavigationService.HidePopup(typeof(ChooseChatsPopup));

                        var response = await ClientService.SendAsync(new RemoveMessageSenderBotVerification(verifyChat.BotUserId, verifiedId));
                        if (response is Ok)
                        {
                            ShowToast(string.Format(Strings.BotSentRevokeVerifyRequest, chat.Title));
                        }
                        else if (response is Error error)
                        {
                            ToastPopup.ShowError(XamlRoot, error);
                        }
                    }
                }
                else
                {
                    var confirm = await VerifyChatPopup.ShowAsync(XamlRoot, ClientService, chat, false, verifyChatFullInfo.BotInfo.VerificationParameters?.CanSetCustomDescription ?? false);
                    if (confirm.Result == ContentDialogResult.Primary)
                    {
                        NavigationService.HidePopup(typeof(ChooseChatsPopup));

                        var response = await ClientService.SendAsync(new SetMessageSenderBotVerification(verifyChat.BotUserId, verifiedId, confirm.Text));
                        if (response is Ok)
                        {
                            ShowToast(string.Format(Strings.BotSentVerifyRequest, chat.Title));
                        }
                        else if (response is Error error)
                        {
                            ToastPopup.ShowError(XamlRoot, error);
                        }
                    }
                }
            }
            else if (_configuration is ChooseChatsConfigurationTransferGift transferGift)
            {
                var confirm = await TransferGiftPopup.ShowAsync(XamlRoot, ClientService, transferGift.Gift, chats[0], false);
                if (confirm == ContentDialogResult.Primary)
                {
                    NavigationService.HidePopup(typeof(ChooseChatsPopup));

                    var response = await ClientService.SendAsync(new TransferGift(transferGift.Gift.ReceivedGiftId, chats[0].ToMessageSender(), transferGift.Gift.TransferStarCount));
                    if (response is Ok && transferGift.Gift.Gift is SentGiftUpgraded upgraded)
                    {
                        Aggregator.Publish(new UpdateGiftIsSold(transferGift.Gift.ReceivedGiftId));

                        ShowToast(string.Format(Strings.Gift2TransferredText, upgraded.Gift.ToName(), chats[0].Title));
                    }
                    else if (response is Error error)
                    {
                        ToastPopup.ShowError(XamlRoot, error);
                    }
                }
            }
            else if (_configuration is ChooseChatsConfigurationCreateGroupCall)
            {
                Session.Resolve<IVoipService>().CreateGroupCall(NavigationService, Array.Empty<long>());
            }
            else if (_configuration is ChooseChatsConfigurationBotAddToChannel botAddToChannel)
            {
                var response = await ClientService.SendAsync(new GetChatMember(chats[0].Id, new MessageSenderUser(botAddToChannel.BotUserId)));
                if (response is ChatMember member && member.Status is not ChatMemberStatusAdministrator { CanBeEdited: false })
                {
                    NavigationService.ShowPopup(new SupergroupEditAdministratorPopup(), new SupergroupEditMemberArgs(chats[0].Id, member.MemberId, botAddToChannel.AdministratorRights));
                }
            }
            else if (_configuration is ChooseChatsConfigurationSetTheme setTheme)
            {
                ClientService.Send(new SetChatTheme(chats[0].Id, new InputChatThemeGift(setTheme.Gift.Name)));
                NavigationService.NavigateToChat(chats[0]);
            }
            else if (_configuration is ChooseChatsConfigurationInviteToChat inviteToChat && ClientService.TryGetChat(inviteToChat.ChatId, out Chat chat))
            {
                async Task<Object> AddChatMembers(Chat chat, IEnumerable<long> users)
                {
                    if (chat.Type is ChatTypeSupergroup)
                    {
                        return await ClientService.SendAsync(new AddChatMembers(chat.Id, users.ToArray()));
                    }

                    IList<FailedToAddMember> members = null;

                    foreach (var userId in users)
                    {
                        var response = await ClientService.SendAsync(new AddChatMember(chat.Id, userId, 100));
                        if (response is FailedToAddMembers failed)
                        {
                            members ??= new List<FailedToAddMember>();
                            members.AddRange(failed.FailedToAddMembersValue);
                        }
                        else if (response is Error)
                        {
                            // TODO: this is not ideal as the app will not try to add subsequent users
                            return response;
                        }
                    }

                    return new FailedToAddMembers(members ?? Array.Empty<FailedToAddMember>());
                }

                var selected = chats.Select(x => ClientService.GetUser(x)).Where(x => x != null).ToList();

                var selectedBotUser = selected.FirstOrDefault(x => x.Type is UserTypeBot);
                if (selectedBotUser != null && chat.Type is ChatTypeSupergroup { IsChannel: true })
                {
                    HidePopup(typeof(ChooseChatsPopup));
                    ShowPopup(new SupergroupEditAdministratorPopup(), new SupergroupEditMemberArgs(chat.Id, new MessageSenderUser(selectedBotUser.Id)));

                    return;
                }

                var response = await AddChatMembers(chat, selected.Select(x => x.Id));
                if (response is FailedToAddMembers failed)
                {
                    if (failed.FailedToAddMembersValue.Count > 0)
                    {
                        ShowPopup(new ChatInviteFallbackPopup(ClientService, chat.Id, failed.FailedToAddMembersValue));
                    }

                    var failedUserIds = failed.FailedToAddMembersValue
                        .Select(x => x.UserId)
                        .ToHashSet();

                    foreach (var user in selected)
                    {
                        if (failedUserIds.Contains(user.Id))
                        {
                            continue;
                        }

                        Aggregator.Publish(new UpdateChatMember(chat.Id, 0, 0, null, false, false, null, new ChatMember(new MessageSenderUser(user.Id), string.Empty, ClientService.Options.MyId, DateTime.Now.ToTimestamp(), new ChatMemberStatusMember())));
                    }
                }
                else if (response is Error error)
                {
                    ShowPopup(error.Message, Strings.AppName);
                }
            }
        }

        public bool ShouldCloseOnCommit { get; private set; } = true;

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
