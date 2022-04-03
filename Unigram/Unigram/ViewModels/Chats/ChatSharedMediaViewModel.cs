using Rg.DiffUtils;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Unigram.Views;
using Unigram.Views.Chats;
using Unigram.Views.Popups;
using Unigram.Views.Users;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Chats
{
    public class ProfileItem
    {
        public string Text { get; set; }

        public Type Type { get; set; }

        public ProfileItem(string text, Type type)
        {
            Text = text;
            Type = type;
        }
    }

    public class ChatSharedMediaViewModel : TLMultipleViewModelBase, IMessageDelegate, IHandle<UpdateDeleteMessages>
    {
        private readonly IPlaybackService _playbackService;
        private readonly IStorageService _storageService;

        public ChatSharedMediaViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IStorageService storageService, IEventAggregator aggregator, IPlaybackService playbackService)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _playbackService = playbackService;
            _storageService = storageService;

            Items = new ObservableCollection<ProfileItem>();

            Media = new SearchCollection<MessageWithOwner, MediaCollection>(SetSearch, new SearchMessagesFilterPhotoAndVideo(), new MessageDiffHandler());
            Files = new SearchCollection<MessageWithOwner, MediaCollection>(SetSearch, new SearchMessagesFilterDocument(), new MessageDiffHandler());
            Links = new SearchCollection<MessageWithOwner, MediaCollection>(SetSearch, new SearchMessagesFilterUrl(), new MessageDiffHandler());
            Music = new SearchCollection<MessageWithOwner, MediaCollection>(SetSearch, new SearchMessagesFilterAudio(), new MessageDiffHandler());
            Voice = new SearchCollection<MessageWithOwner, MediaCollection>(SetSearch, new SearchMessagesFilterVoiceNote(), new MessageDiffHandler());
            Animations = new SearchCollection<MessageWithOwner, MediaCollection>(SetSearch, new SearchMessagesFilterAnimation(), new MessageDiffHandler());

            MessagesForwardCommand = new RelayCommand(MessagesForwardExecute, MessagesForwardCanExecute);
            MessagesDeleteCommand = new RelayCommand(MessagesDeleteExecute, MessagesDeleteCanExecute);
            MessagesUnselectCommand = new RelayCommand(MessagesUnselectExecute);
            MessageViewCommand = new RelayCommand<Message>(MessageViewExecute);
            MessageSaveCommand = new RelayCommand<Message>(MessageSaveExecute);
            MessageDeleteCommand = new RelayCommand<Message>(MessageDeleteExecute);
            MessageForwardCommand = new RelayCommand<Message>(MessageForwardExecute);
            MessageSelectCommand = new RelayCommand<Message>(MessageSelectExecute);
        }

        public ObservableCollection<ProfileItem> Items { get; }

        public IPlaybackService PlaybackService => _playbackService;

        public IStorageService StorageService => _storageService;

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var chatId = (long)parameter;

            if (state.TryGet("selectedIndex", out int selectedIndex))
            {
                SelectedIndex = selectedIndex;
            }

            Chat = ProtoService.GetChat(chatId);

            Media.SetQuery(string.Empty);
            Files.SetQuery(string.Empty);
            Links.SetQuery(string.Empty);
            Music.SetQuery(string.Empty);
            Voice.SetQuery(string.Empty);
            Animations.SetQuery(string.Empty);

            Aggregator.Subscribe(this);

            Items.Clear();

            if (Chat.Type is ChatTypeBasicGroup || Chat.Type is ChatTypeSupergroup supergroup && !supergroup.IsChannel)
            {
                Items.Add(new ProfileItem(Strings.Resources.ChannelMembers, typeof(ChatSharedMembersPage)));
                HasSharedMembers = true;
                SelectedItem = Items.FirstOrDefault();
            }

            await base.OnNavigatedToAsync(parameter, mode, state);
            await UpdateSharedCountAsync(Chat);
        }

        private int[] _sharedCount = new int[] { 0, 0, 0, 0, 0, 0 };
        public int[] SharedCount
        {
            get => _sharedCount;
            set => Set(ref _sharedCount, value);
        }

        private ProfileItem _selectedItem;
        public ProfileItem SelectedItem
        {
            get => _selectedItem;
            set => Set(ref _selectedItem, value);
        }

        public double VerticalOffset { get; set; }

        public bool HasSharedGroups { get; private set; }

        public bool HasSharedMembers { get; private set; }

        private async Task UpdateSharedCountAsync(Chat chat)
        {
            var filters = new SearchMessagesFilter[]
            {
                new SearchMessagesFilterPhotoAndVideo(),
                new SearchMessagesFilterDocument(),
                new SearchMessagesFilterUrl(),
                new SearchMessagesFilterAudio(),
                new SearchMessagesFilterVoiceNote(),
                new SearchMessagesFilterAnimation(),
            };

            for (int i = 0; i < filters.Length; i++)
            {
                var response = await ProtoService.SendAsync(new GetChatMessageCount(chat.Id, filters[i], false));
                if (response is Count count)
                {
                    SharedCount[i] = count.CountValue;
                }
            }

            SharedCount[SharedCount.Length - 1] = 0;

            if (SharedCount[0] > 0)
            {
                Items.Add(new ProfileItem(Strings.Resources.SharedMediaTab2, typeof(ChatSharedMediaPage)));
            }
            if (SharedCount[1] > 0)
            {
                Items.Add(new ProfileItem(Strings.Resources.SharedFilesTab2, typeof(ChatSharedFilesPage)));
            }
            if (SharedCount[2] > 0)
            {
                Items.Add(new ProfileItem(Strings.Resources.SharedLinksTab2, typeof(ChatSharedLinksPage)));
            }
            if (SharedCount[3] > 0)
            {
                Items.Add(new ProfileItem(Strings.Resources.SharedMusicTab2, typeof(ChatSharedMusicPage)));
            }
            if (SharedCount[4] > 0)
            {
                Items.Add(new ProfileItem(Strings.Resources.SharedVoiceTab2, typeof(ChatSharedVoicePage)));
            }

            if (chat.Type is ChatTypePrivate or ChatTypeSecret)
            {
                var user = ProtoService.GetUser(chat);

                var cached = ProtoService.GetUserFull(chat);
                if (cached == null)
                {
                    // This should really rarely happen
                    cached = await ProtoService.SendAsync(new GetUserFullInfo(user.Id)) as UserFullInfo;
                }

                if (cached.GroupInCommonCount > 0)
                {
                    Items.Add(new ProfileItem(Strings.Resources.SharedGroupsTab2, typeof(UserCommonChatsPage)));
                    HasSharedGroups = true;
                }
            }

            SelectedItem ??= Items.FirstOrDefault();
            RaisePropertyChanged(nameof(SharedCount));
        }

        public override Task OnNavigatedFromAsync(NavigationState suspensionState, bool suspending)
        {
            Aggregator.Unsubscribe(this);
            return Task.CompletedTask;
        }

        public void Handle(UpdateDeleteMessages update)
        {
            if (update.ChatId == _chat?.Id && !update.FromCache)
            {
                var table = update.MessageIds.ToImmutableHashSet();

                BeginOnUIThread(() =>
                {
                    UpdateDeleteMessages(Media, table);
                    UpdateDeleteMessages(Files, table);
                    UpdateDeleteMessages(Links, table);
                    UpdateDeleteMessages(Music, table);
                    UpdateDeleteMessages(Voice, table);
                    UpdateDeleteMessages(Animations, table);
                });
            }
        }

        private void UpdateDeleteMessages(IList<MessageWithOwner> target, ImmutableHashSet<long> table)
        {
            for (int i = 0; i < target.Count; i++)
            {
                var message = target[i];
                if (table.Contains(message.Id))
                {
                    target.RemoveAt(i);
                    i--;
                }
            }
        }

        protected Chat _chat;
        public Chat Chat
        {
            get => _chat;
            set => Set(ref _chat, value);
        }

        private int _selectedIndex;
        public int SelectedIndex
        {
            get => _selectedIndex;
            set => Set(ref _selectedIndex, value);
        }

        public SearchCollection<MessageWithOwner, MediaCollection> Media { get; private set; }
        public SearchCollection<MessageWithOwner, MediaCollection> Files { get; private set; }
        public SearchCollection<MessageWithOwner, MediaCollection> Links { get; private set; }
        public SearchCollection<MessageWithOwner, MediaCollection> Music { get; private set; }
        public SearchCollection<MessageWithOwner, MediaCollection> Voice { get; private set; }
        public SearchCollection<MessageWithOwner, MediaCollection> Animations { get; private set; }

        public MediaCollection SetSearch(object sender, string query)
        {
            if (sender is SearchMessagesFilter filter)
            {
                return new MediaCollection(ProtoService, Chat.Id, filter, query);
            }

            return null;
        }

        public class MessageDiffHandler : IDiffHandler<MessageWithOwner>
        {
            public bool CompareItems(MessageWithOwner oldItem, MessageWithOwner newItem)
            {
                return oldItem?.Id == newItem?.Id && oldItem?.ChatId == newItem?.ChatId;
            }

            public void UpdateItem(MessageWithOwner oldItem, MessageWithOwner newItem)
            {
            }
        }

        private ListViewSelectionMode _selectionMode = ListViewSelectionMode.None;
        public ListViewSelectionMode SelectionMode
        {
            get => _selectionMode;
            set => Set(ref _selectionMode, value);
        }

        private List<Message> _selectedItems = new List<Message>();
        public List<Message> SelectedItems
        {
            get => _selectedItems;
            set
            {
                Set(ref _selectedItems, value);
                MessagesForwardCommand.RaiseCanExecuteChanged();
                MessagesDeleteCommand.RaiseCanExecuteChanged();
            }
        }

        #region View

        public RelayCommand<Message> MessageViewCommand { get; }
        private void MessageViewExecute(Message message)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.NavigateToChat(chat, message: message.Id);
        }

        #endregion

        #region Save

        public RelayCommand<Message> MessageSaveCommand { get; }
        private async void MessageSaveExecute(Message message)
        {
            var file = message.GetFile();
            if (file != null)
            {
                await _storageService.SaveAsAsync(file);
            }
        }

        #endregion

        #region Delete

        public RelayCommand<Message> MessageDeleteCommand { get; }
        private void MessageDeleteExecute(Message message)
        {
            if (message == null)
            {
                return;
            }

            var chat = ProtoService.GetChat(message.ChatId);
            if (chat == null)
            {
                return;
            }

            //if (message != null && message.Media is TLMessageMediaGroup groupMedia)
            //{
            //    ExpandSelection(new[] { message });
            //    MessagesDeleteExecute();
            //    return;
            //}

            MessagesDelete(chat, new[] { message });
        }

        private async void MessagesDelete(Chat chat, IList<Message> messages)
        {
            var first = messages.FirstOrDefault();
            if (first == null)
            {
                return;
            }

            var response = await ProtoService.SendAsync(new GetMessages(chat.Id, messages.Select(x => x.Id).ToArray()));
            if (response is Messages updated)
            {
                for (int i = 0; i < updated.MessagesValue.Count; i++)
                {
                    if (updated.MessagesValue[i] != null)
                    {
                        messages[i] = updated.MessagesValue[i];
                    }
                    else
                    {
                        messages.RemoveAt(i);
                        updated.MessagesValue.RemoveAt(i);

                        i--;
                    }
                }
            }

            var sameUser = messages.All(x => x.SenderId.IsEqual(first.SenderId));
            var dialog = new DeleteMessagesPopup(CacheService, messages.Where(x => x != null).ToArray());

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            SelectionMode = ListViewSelectionMode.None;

            if (dialog.DeleteAll && sameUser)
            {
                ProtoService.Send(new DeleteChatMessagesBySender(chat.Id, first.SenderId));
            }
            else
            {
                ProtoService.Send(new DeleteMessages(chat.Id, messages.Select(x => x.Id).ToList(), dialog.Revoke));
            }

            if (dialog.BanUser && sameUser)
            {
                ProtoService.Send(new SetChatMemberStatus(chat.Id, first.SenderId, new ChatMemberStatusBanned()));
            }

            if (dialog.ReportSpam && sameUser && chat.Type is ChatTypeSupergroup supertype)
            {
                ProtoService.Send(new ReportSupergroupSpam(supertype.SupergroupId, messages.Select(x => x.Id).ToList()));
            }
        }

        #endregion

        #region Forward

        public RelayCommand<Message> MessageForwardCommand { get; }
        private async void MessageForwardExecute(Message message)
        {
            SelectionMode = ListViewSelectionMode.None;
            await SharePopup.GetForCurrentView().ShowAsync(message);
        }

        #endregion

        #region Multiple Delete

        public RelayCommand MessagesDeleteCommand { get; }
        private void MessagesDeleteExecute()
        {
            var messages = new List<Message>(SelectedItems);

            var first = messages.FirstOrDefault();
            if (first == null)
            {
                return;
            }

            var chat = ProtoService.GetChat(first.ChatId);
            if (chat == null)
            {
                return;
            }

            MessagesDelete(chat, messages);
        }

        private bool MessagesDeleteCanExecute()
        {
            return SelectedItems.Count > 0 && SelectedItems.All(x => x.CanBeDeletedForAllUsers || x.CanBeDeletedOnlyForSelf);
        }

        #endregion

        #region Multiple Forward

        public RelayCommand MessagesForwardCommand { get; }
        private async void MessagesForwardExecute()
        {
            var messages = SelectedItems.Where(x => x.CanBeForwarded).OrderBy(x => x.Id).ToList();
            if (messages.Count > 0)
            {
                SelectionMode = ListViewSelectionMode.None;
                await SharePopup.GetForCurrentView().ShowAsync(messages);
            }
        }

        private bool MessagesForwardCanExecute()
        {
            return SelectedItems.Count > 0 && SelectedItems.All(x => x.CanBeForwarded);
        }

        #endregion

        #region Select

        public RelayCommand<Message> MessageSelectCommand { get; }
        private void MessageSelectExecute(Message message)
        {
            SelectionMode = ListViewSelectionMode.Multiple;

            SelectedItems = new List<Message> { message };
            RaisePropertyChanged("SelectedItems");
        }

        #endregion

        #region Unselect

        public RelayCommand MessagesUnselectCommand { get; }
        private void MessagesUnselectExecute()
        {
            SelectionMode = ListViewSelectionMode.None;
        }

        #endregion

        #region Delegate

        public bool CanBeDownloaded(object content, File file)
        {
            return true;
        }

        public void DownloadFile(MessageViewModel message, File file)
        {
        }

        public void ReplyToMessage(MessageViewModel message)
        {
        }

        public void ViewVisibleMessages(bool intermediate)
        {

        }

        public void OpenReply(MessageViewModel message)
        {
        }

        public async void OpenFile(File file)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                try
                {
                    var temp = await ProtoService.GetFileAsync(file);
                    var result = await Windows.System.Launcher.LaunchFileAsync(temp);
                    //var folder = await temp.GetParentAsync();
                    //var options = new Windows.System.FolderLauncherOptions();
                    //options.ItemsToSelect.Add(temp);

                    //var result = await Windows.System.Launcher.LaunchFolderAsync(folder, options);
                }
                catch { }
            }
        }

        public void OpenWebPage(WebPage webPage)
        {
        }

        public void OpenSticker(Sticker sticker)
        {
        }

        public void OpenLocation(Location location, string title)
        {
        }

        public void OpenLiveLocation(MessageViewModel message)
        {

        }

        public void OpenInlineButton(MessageViewModel message, InlineKeyboardButton button)
        {
        }

        public void OpenMedia(MessageViewModel message, FrameworkElement target, int timestamp = 0)
        {
        }

        public void PlayMessage(MessageViewModel message)
        {
        }

        public async void OpenUsername(string username)
        {
            var response = await ProtoService.SendAsync(new SearchPublicChat(username));
            if (response is Chat chat)
            {
                if (chat.Type is ChatTypePrivate privata)
                {
                    var user = ProtoService.GetUser(privata.UserId);
                    if (user?.Type is UserTypeBot)
                    {
                        NavigationService.NavigateToChat(chat);
                    }
                    else
                    {
                        NavigationService.Navigate(typeof(ProfilePage), chat.Id);
                    }
                }
                else
                {
                    NavigationService.NavigateToChat(chat);
                }
            }
        }

        public void OpenHashtag(string hashtag)
        {
        }

        public void OpenBankCardNumber(string number)
        {
        }

        public async void OpenUser(long userId)
        {
            var response = await ProtoService.SendAsync(new CreatePrivateChat(userId, false));
            if (response is Chat chat)
            {
                var user = ProtoService.GetUser(userId);
                if (user?.Type is UserTypeBot)
                {
                    NavigationService.NavigateToChat(chat);
                }
                else
                {
                    NavigationService.Navigate(typeof(ProfilePage), chat.Id);
                }
            }
        }

        public void OpenChat(long chatId, bool profile = false)
        {
        }

        public void OpenChat(long chatId, long messageId)
        {
        }

        public void OpenViaBot(long viaBotUserId)
        {
        }

        public async void OpenUrl(string url, bool untrust)
        {
            if (MessageHelper.TryCreateUri(url, out Uri uri))
            {
                if (MessageHelper.IsTelegramUrl(uri))
                {
                    MessageHelper.OpenTelegramUrl(ProtoService, NavigationService, uri);
                }
                else
                {
                    if (untrust)
                    {
                        var confirm = await MessagePopup.ShowAsync(string.Format(Strings.Resources.OpenUrlAlert, url), Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
                        if (confirm != ContentDialogResult.Primary)
                        {
                            return;
                        }
                    }

                    try
                    {
                        await Windows.System.Launcher.LaunchUriAsync(uri);
                    }
                    catch { }
                }
            }
        }

        public void SendBotCommand(string command)
        {
        }

        public void Call(MessageViewModel message, bool video)
        {
            throw new NotImplementedException();
        }

        public void VotePoll(MessageViewModel message, IList<PollOption> options)
        {
            throw new NotImplementedException();
        }

        public string GetAdminTitle(MessageViewModel message)
        {
            throw new NotImplementedException();
        }

        public void OpenThread(MessageViewModel message)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
