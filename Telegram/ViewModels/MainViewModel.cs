//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Updates;
using Telegram.Streams;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels.Folders;
using Telegram.ViewModels.Stories;
using Telegram.Views;
using Telegram.Views.Folders;
using Telegram.Views.Popups;
using Telegram.Views.Settings.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels
{
    public class MainViewModel : MultiViewModelBase, IDisposable
    {
        private readonly IContactsService _contactsService;
        private readonly IPasscodeService _passcodeService;
        private readonly ILifetimeService _lifetimeService;
        private readonly ISessionService _sessionService;
        private readonly IVoipService _voipService;
        private readonly IVoipGroupService _voipGroupService;
        private readonly ICloudUpdateService _cloudUpdateService;
        private readonly IPlaybackService _playbackService;
        private readonly IShortcutsService _shortcutService;

        public bool Refresh { get; set; }

        public MainViewModel(IClientService clientService, ISettingsService settingsService, IStorageService storageService, IEventAggregator aggregator, INotificationsService pushService, IContactsService contactsService, IPasscodeService passcodeService, ILifetimeService lifecycle, ISessionService session, IVoipService voipService, IVoipGroupService voipGroupService, ISettingsSearchService settingsSearchService, ICloudUpdateService cloudUpdateService, IPlaybackService playbackService, IShortcutsService shortcutService)
            : base(clientService, settingsService, aggregator)
        {
            _contactsService = contactsService;
            _passcodeService = passcodeService;
            _lifetimeService = lifecycle;
            _sessionService = session;
            _voipService = voipService;
            _voipGroupService = voipGroupService;
            _cloudUpdateService = cloudUpdateService;
            _playbackService = playbackService;
            _shortcutService = shortcutService;

            Folders = new ChatFolderCollection();
            NavigationItems = new List<IEnumerable<ChatFolderViewModel>>
            {
                Folders,
                new ChatFolderViewModel[]
                {
                    new ChatFolderViewModel(int.MaxValue - 1, Strings.Contacts, "\uE95E", "\uE95D"),
                    new ChatFolderViewModel(int.MaxValue - 2, Strings.Calls, "\uE991", "\uE990"),
                    new ChatFolderViewModel(int.MaxValue - 3, Strings.Settings, "\uE98F", "\uE98E"),
                }
            };

            ChatList chatList = ClientService.MainChatListPosition > 0 && ClientService.ChatFolders.Count > 0
                ? new ChatListFolder(ClientService.ChatFolders[0].Id)
                : new ChatListMain();

            Chats = new ChatListViewModel(clientService, settingsService, aggregator, pushService, chatList);
            SearchChats = new SearchChatsViewModel(clientService, settingsService, aggregator);
            Stories = new StoryListViewModel(clientService, settingsService, aggregator, new StoryListMain());
            Topics = new TopicListViewModel(clientService, settingsService, aggregator, pushService, 0);
            Contacts = new ContactsViewModel(clientService, settingsService, aggregator);
            Calls = new CallsViewModel(clientService, settingsService, aggregator);
            Settings = new SettingsViewModel(clientService, settingsService, storageService, aggregator, settingsSearchService);

            // This must represent pivot tabs
            Children.Add(Chats);
            Children.Add(Contacts);
            Children.Add(Calls);
            Children.Add(Settings);

            // Any additional child
            Children.Add(_voipService as ViewModelBase);
            Children.Add(SearchChats);
            Children.Add(Topics);
            Children.Add(Stories);

            Subscribe();
        }

        public void Dispose()
        {
            Aggregator.Unsubscribe(Chats.Items);
            Aggregator.Unsubscribe(this);

            if (Dispatcher != null && Dispatcher.HasThreadAccess)
            {
                Chats.Items.Clear();
            }

            Children.Clear();
        }

        public ILifetimeService Lifetime => _lifetimeService;
        public ISessionService Session => _sessionService;

        public IPasscodeService Passcode => _passcodeService;

        public IPlaybackService PlaybackService => _playbackService;

        public IShortcutsService ShortcutService => _shortcutService;

        public IVoipService VoipService => _voipService;
        public IVoipGroupService VoipGroupService => _voipGroupService;

        public void ToggleArchive()
        {
            base.Settings.HideArchivedChats = !base.Settings.HideArchivedChats;
        }

        private int _unreadCount;
        public int UnreadCount
        {
            get => _unreadCount;
            set => Set(ref _unreadCount, value);
        }

        private int _unreadMutedCount;
        public int UnreadMutedCount
        {
            get => _unreadMutedCount;
            set => Set(ref _unreadMutedCount, value);
        }

        private int _unreadUnmutedCount;
        public int UnreadUnmutedCount
        {
            get => _unreadUnmutedCount;
            set => Set(ref _unreadUnmutedCount, value);
        }

        private bool _isUpdateAvailable;
        public bool IsUpdateAvailable
        {
            get => _isUpdateAvailable;
            set => Set(ref _isUpdateAvailable, value);
        }

        public void ReturnToCall()
        {
            _voipService.Show();
        }

        public void Handle(UpdateAppVersion update)
        {
            BeginOnUIThread(() => UpdateAppVersion(update.Update));
        }

        public void Handle(UpdateWindowActivated update)
        {
            if (update.IsActive)
            {
                _ = _cloudUpdateService.UpdateAsync(false);
            }
        }

        private void UpdateAppVersion(CloudUpdate update)
        {
            IsUpdateAvailable = update?.File != null;
        }

        public void Handle(UpdateServiceNotification update)
        {

        }

        public void Handle(UpdateUnreadMessageCount update)
        {
            if (update.ChatList is ChatListMain)
            {
                BeginOnUIThread(() =>
                {
                    UnreadCount = update.UnreadCount;
                    UnreadUnmutedCount = update.UnreadUnmutedCount;
                    UnreadMutedCount = update.UnreadCount - update.UnreadUnmutedCount;
                });
            }
        }

        public void Handle(UpdateUnreadChatCount update)
        {
            foreach (var folder in _folders)
            {
                if (folder.ChatList is ChatListFolder && folder.ChatList.ListEquals(update.ChatList))
                {
                    BeginOnUIThread(() => folder.UpdateCount(update));
                }
            }
        }

        public void Handle(UpdateDeleteMessages update)
        {
            if (update.FromCache)
            {
                return;
            }

            var message = _playbackService.CurrentItem;
            if (message == null || message.ClientService != ClientService)
            {
                return;
            }

            if (message.ChatId == update.ChatId && update.MessageIds.Contains(message.Id))
            {
                _playbackService.Clear();
            }
        }

        public void Handle(UpdateAddChatMembersPrivacyForbidden update)
        {
            BeginOnUIThread(() => UpdateAddChatMembersPrivacyForbidden(update.ChatId, update.UserIds));
        }

        private async void UpdateAddChatMembersPrivacyForbidden(long chatId, IList<long> userIds)
        {
            var popup = new ChatInviteFallbackPopup(ClientService, chatId, userIds);
            await ShowPopupAsync(popup);
        }

        public void Handle(UpdateChatFolders update)
        {
            BeginOnUIThread(() => UpdateChatFolders(update.ChatFolders, update.MainChatListPosition));
        }

        private void UpdateChatFolders(IList<ChatFolderInfo> chatFolders, int mainChatListPosition)
        {
            if (chatFolders.Count > 0)
            {
                var selected = SelectedFolder?.ChatFolderId ?? Constants.ChatListMain;

                var folders = chatFolders.ToList();
                var index = Math.Min(mainChatListPosition, folders.Count);

                folders.Insert(index, new ChatFolderInfo { Id = Constants.ChatListMain, Title = Strings.FilterAllChats, Icon = new ChatFolderIcon("All") });

                Merge(Folders, folders);

                if (Chats.Items.ChatList is ChatListFolder already && already.ChatFolderId != selected)
                {
                    SelectedFolder = Folders[0];
                }
                else
                {
                    RaisePropertyChanged(nameof(SelectedFolder));
                }

                foreach (var folder in _folders)
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
            }
            else
            {
                Folders.Clear();
                SelectedFolder = ChatFolderViewModel.Main;
            }
        }

        private void Merge(IList<ChatFolderViewModel> destination, IList<ChatFolderInfo> origin)
        {
            if (destination.Count > 0)
            {
                for (int i = 0; i < destination.Count; i++)
                {
                    var user = destination[i];
                    var index = -1;

                    for (int j = 0; j < origin.Count; j++)
                    {
                        if (origin[j].Id == user.ChatFolderId)
                        {
                            index = j;
                            break;
                        }
                    }

                    if (index == -1)
                    {
                        destination.Remove(user);
                        i--;
                    }
                }

                for (int i = 0; i < origin.Count; i++)
                {
                    var folder = origin[i];
                    var index = -1;

                    for (int j = 0; j < destination.Count; j++)
                    {
                        if (destination[j].ChatFolderId == folder.Id)
                        {
                            destination[j].Update(folder);

                            index = j;
                            break;
                        }
                    }

                    if (index > -1 && index != i)
                    {
                        destination.RemoveAt(index);
                        destination.Insert(Math.Min(i, destination.Count), new ChatFolderViewModel(folder));
                    }
                    else if (index == -1)
                    {
                        destination.Insert(Math.Min(i, destination.Count), new ChatFolderViewModel(folder));
                    }
                }
            }
            else
            {
                destination.Clear();
                destination.AddRange(origin.Select(x => new ChatFolderViewModel(x)));
            }
        }

        private ChatFolderCollection _folders;
        public ChatFolderCollection Folders
        {
            get => _folders;
            set => Set(ref _folders, value);
        }

        public List<IEnumerable<ChatFolderViewModel>> NavigationItems { get; private set; }

        public ChatFolderViewModel SelectedFolder
        {
            get
            {
                if (Chats.Items.ChatList is ChatListFolder folder && _folders != null)
                {
                    return _folders.FirstOrDefault(x => x.ChatFolderId == folder.ChatFolderId);
                }

                return _folders?.FirstOrDefault(x => x.ChatList is ChatListMain);
            }
            set
            {
                if (Chats.Items.ChatList.ListEquals(value.ChatList))
                {
                    return;
                }

                Chats.SetFolder(value.ChatList);
                RaisePropertyChanged();
            }
        }

        private static bool _shown;

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            //BeginOnUIThread(() => Calls.OnNavigatedToAsync(parameter, mode, state));
            //BeginOnUIThread(() => Settings.OnNavigatedToAsync(parameter, mode, state));
            //Dispatch(() => Dialogs.LoadFirstSlice());
            //Dispatch(() => Contacts.getTLContacts());
            //Dispatch(() => Contacts.GetSelfAsync());

            UpdateAppVersion(_cloudUpdateService.NextUpdate);
            UpdateChatFolders(ClientService.ChatFolders, ClientService.MainChatListPosition);

            var unreadCount = ClientService.GetUnreadCount(new ChatListMain());
            UnreadCount = unreadCount.UnreadMessageCount.UnreadCount;
            UnreadMutedCount = unreadCount.UnreadMessageCount.UnreadCount - unreadCount.UnreadMessageCount.UnreadUnmutedCount;

            if (_voipService.Call != null)
            {
                Aggregator.Publish(new UpdateCallDialog(_voipService.Call));
            }
            else if (_voipGroupService.Call != null)
            {
                Aggregator.Publish(new UpdateCallDialog(_voipGroupService.Call));
            }

            if (mode == NavigationMode.New)
            {
                Task.Run(() => _contactsService.JumpListAsync());
                Task.Run(() => _cloudUpdateService.UpdateAsync(false));
            }

            if (ApiInfo.IsPackagedRelease && WatchDog.HasCrashedInLastSession && !_shown)
            {
                _shown = true;

                var confirm = await ShowPopupAsync("It seems that the app terminated unexpectedly. Do you want to report this problem?", "Something went wrong", "OK", "Cancel");
                if (confirm == ContentDialogResult.Primary)
                {
                    MessageHelper.NavigateToUsername(ClientService, NavigationService, "unigraminsiders", null, null);
                }
            }

            //return base.OnNavigatedToAsync(parameter, mode, state);
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateServiceNotification>(this, Handle)
                .Subscribe<UpdateUnreadMessageCount>(Handle)
                .Subscribe<UpdateUnreadChatCount>(Handle)
                .Subscribe<UpdateDeleteMessages>(Handle)
                .Subscribe<UpdateChatFolders>(Handle)
                .Subscribe<UpdateAddChatMembersPrivacyForbidden>(Handle)
                .Subscribe<UpdateAppVersion>(Handle)
                .Subscribe<UpdateWindowActivated>(Handle);
        }

        public ChatListViewModel Chats { get; }
        public SearchChatsViewModel SearchChats { get; }
        public StoryListViewModel Stories { get; }
        public TopicListViewModel Topics { get; }
        public ContactsViewModel Contacts { get; }
        public CallsViewModel Calls { get; }
        public SettingsViewModel Settings { get; }


        
        public void ConfirmSession()
        {
            var session = ClientService.UnconfirmedSession;
            if (session == null)
            {
                Aggregator.Publish(new UpdateUnconfirmedSession(null));
                return;
            }

            ClientService.Send(new ConfirmSession(session.Id));
            //Aggregator.Publish(new UpdateUnconfirmedSession(null));

            var message = Strings.UnconfirmedAuthConfirmed + Environment.NewLine + Strings.UnconfirmedAuthConfirmedMessage;
            var entity = new TextEntity(0, Strings.RequestToJoinSent.Length, new TextEntityTypeBold());

            var markdown = new FormattedText(message, new[] { entity });
            var text = Client.Execute(new ParseMarkdown(markdown)) as FormattedText;

            Window.Current.ShowTeachingTip(text, new LocalFileSource("ms-appx:///Assets/Toasts/Success.tgs"));
        }

        public async void DenySession()
        {
            var session = ClientService.UnconfirmedSession;
            if (session == null)
            {
                Aggregator.Publish(new UpdateUnconfirmedSession(null));
                return;
            }

            ClientService.Send(new TerminateSession(session.Id));
            //Aggregator.Publish(new UpdateUnconfirmedSession(null));

            await ShowPopupAsync(new UnconfirmedSessionPopup(session));
        }

        public async void UpdateApp()
        {
            await CloudUpdateService.LaunchAsync(Dispatcher);
        }

        public async void CreateSecretChat()
        {
            var user = await ChooseChatsPopup.PickUserAsync(ClientService, Strings.NewSecretChat, true);
            if (user == null)
            {
                return;
            }

            var response = await ClientService.SendAsync(new CreateNewSecretChat(user.Id));
            if (response is Chat chat)
            {
                NavigationService.NavigateToChat(chat);
            }
        }

        public void EditFolder(ChatFolderViewModel folder)
        {
            if (folder.ChatFolderId == Constants.ChatListMain)
            {
                NavigationService.Navigate(typeof(FoldersPage));
            }
            else
            {
                NavigationService.Navigate(typeof(FolderPage), folder.ChatFolderId);
            }
        }

        public async void AddToFolder(ChatFolderViewModel folder)
        {
            var viewModel = TLContainer.Current.Resolve<FolderViewModel>();
            await viewModel.NavigatedToAsync(folder.ChatFolderId, NavigationMode.New, null);
            await viewModel.AddIncludeAsync();
            await viewModel.SendAsync();
        }

        public async void MarkFolderAsRead(ChatFolderViewModel folder)
        {
            var confirm = await ShowPopupAsync(Strings.AreYouSure, Strings.AppName, Strings.MarkAllAsRead, Strings.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var chats = await ClientService.GetChatListAsync(folder.ChatList, 0, int.MaxValue);
            if (chats.TotalCount > chats.ChatIds.Count)
            {
                // ???
            }

            foreach (var id in chats.ChatIds)
            {
                var chat = ClientService.GetChat(id);
                if (chat == null || chat.LastMessage == null || !chat.IsUnread())
                {
                    continue;
                }

                ClientService.Send(new ViewMessages(chat.Id, new[] { chat.LastMessage.Id }, new MessageSourceChatList(), true));
            }
        }

        public void DeleteFolder(ChatFolderViewModel folder)
        {
            FoldersViewModel.Delete(ClientService, NavigationService, folder.Info);
        }

        public void ArchiveSettings()
        {
            ShowPopup(new SettingsArchivePopup(ClientService));
        }
    }

    public class ChatFolderViewModel : BindableBase
    {
        public static ChatFolderViewModel Main => new ChatFolderViewModel(new ChatListMain())
        {
            ChatFolderId = Constants.ChatListMain,
            Title = Strings.FilterAllChats
        };

        public static ChatFolderViewModel Archive => new ChatFolderViewModel(new ChatListArchive())
        {
            ChatFolderId = Constants.ChatListArchive,
            Title = Strings.ArchivedChats
        };

        public bool IsNavigationItem { get; }

        public ChatFolderViewModel(ChatFolderInfo info)
        {
            if (info.Id == Constants.ChatListMain)
            {
                ChatList = new ChatListMain();
            }
            else if (info.Id == Constants.ChatListArchive)
            {
                ChatList = new ChatListArchive();
            }
            else
            {
                ChatList = new ChatListFolder(info.Id);
            }

            Info = info;
            ChatFolderId = info.Id;

            _title = info.Title;
            _icon = Icons.ParseFolder(info.Icon);

            var glyph = Icons.FolderToGlyph(_icon);
            _iconGlyph = glyph.Item1;
            _filledIconGlyph = glyph.Item2;
        }

        public ChatFolderViewModel(int id, string title, string glyph, string filledGlyph)
        {
            ChatFolderId = id;
            IsNavigationItem = true;

            Title = title;
            IconGlyph = glyph;
            FilledIconGlyph = filledGlyph;
        }

        private ChatFolderViewModel(ChatList list)
        {
            ChatList = list;
        }

        public void Update(ChatFolderInfo info)
        {
            Title = info.Title;
            Icon = Icons.ParseFolder(info.Icon);

            var glyph = Icons.FolderToGlyph(_icon);
            IconGlyph = glyph.Item1;
            FilledIconGlyph = glyph.Item2;
        }

        public ChatList ChatList { get; }

        public int ChatFolderId { get; set; }

        public ChatFolderInfo Info { get; }

        private string _title;
        public string Title
        {
            get => _title;
            set => Set(ref _title, value);
        }

        private ChatFolderIcon2 _icon;
        public ChatFolderIcon2 Icon
        {
            get => _icon;
            set => Set(ref _icon, value);
        }

        private string _iconGlyph;
        public string IconGlyph
        {
            get => _iconGlyph;
            set => Set(ref _iconGlyph, value);
        }

        private string _filledIconGlyph;
        public string FilledIconGlyph
        {
            get => _filledIconGlyph;
            set => Set(ref _filledIconGlyph, value);
        }

        private int _unreadCount;
        public int UnreadCount
        {
            get => _unreadCount;
            set => Set(ref _unreadCount, value);
        }

        private bool _isUnmuted;
        public bool IsUnmuted
        {
            get => _isUnmuted;
            set => Set(ref _isUnmuted, value);
        }

        public bool ShowCount => UnreadCount > 0;

        public void UpdateCount(UpdateUnreadChatCount update)
        {
            var unreadCount = update.UnreadCount;
            var unreadUnmutedCount = update.UnreadUnmutedCount;
            var unreadMutedCount = update.UnreadCount - update.UnreadUnmutedCount;

            if (unreadMutedCount > 0 && unreadUnmutedCount == 0)
            {
                UnreadCount = unreadMutedCount;
                IsUnmuted = false;
            }
            else
            {
                UnreadCount = unreadCount;
                IsUnmuted = true;
            }

            RaisePropertyChanged(nameof(ShowCount));
        }
    }

    public enum ChatFolderIcon2
    {
        Custom,
        All,
        Unread,
        Unmuted,
        Bots,
        Channels,
        Groups,
        Private,
        Cat,
        Crown,
        Favorite,
        Flower,
        Game,
        Home,
        Love,
        Mask,
        Party,
        Sport,
        Study,
        Trade,
        Travel,
        Work,
        Book,
        Money,
        Light,
        Like,
        Note,
        Palette,
        Airplane,
        Setup
    }

    public class ChatFolderCollection : ObservableCollection<ChatFolderViewModel>, IKeyIndexMapping
    {
        public ChatFolderCollection()
        {

        }

        public ChatFolderCollection(IEnumerable<ChatFolderViewModel> source)
            : base(source)
        {

        }

        public string KeyFromIndex(int index)
        {
            return this[index].ChatFolderId.ToString();
        }

        public int IndexFromKey(string key)
        {
            return IndexOf(this.FirstOrDefault(x => key == x.ChatFolderId.ToString()));
        }
    }
}
