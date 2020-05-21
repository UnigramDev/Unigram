using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Views;
using Unigram.Navigation;
using Unigram.Services;
using Unigram.Views.Folders;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class MainViewModel : TLMultipleViewModelBase, IHandle<UpdateServiceNotification>, IHandle<UpdateUnreadMessageCount>, IHandle<UpdateChatFilters>
    {
        private readonly INotificationsService _pushService;
        private readonly IContactsService _contactsService;
        private readonly IVibrationService _vibrationService;
        private readonly IPasscodeService _passcodeService;
        private readonly ILifetimeService _lifetimeService;
        private readonly ISessionService _sessionService;
        private readonly IVoIPService _voipService;
        private readonly IEmojiSetService _emojiSetService;
        private readonly IPlaybackService _playbackService;
        private readonly IShortcutsService _shortcutService;

        public bool Refresh { get; set; }

        public MainViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, INotificationsService pushService, IContactsService contactsService, IVibrationService vibrationService, IPasscodeService passcodeService, ILifetimeService lifecycle, ISessionService session, IVoIPService voipService, ISettingsSearchService settingsSearchService, IEmojiSetService emojiSetService, IPlaybackService playbackService, IShortcutsService shortcutService)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _pushService = pushService;
            _contactsService = contactsService;
            _vibrationService = vibrationService;
            _passcodeService = passcodeService;
            _lifetimeService = lifecycle;
            _sessionService = session;
            _voipService = voipService;
            _emojiSetService = emojiSetService;
            _playbackService = playbackService;
            _shortcutService = shortcutService;

            Filters = new ChatFilterCollection();

            Chats = new ChatsViewModel(protoService, cacheService, settingsService, aggregator, pushService, new ChatListMain());
            ArchivedChats = new ChatsViewModel(protoService, cacheService, settingsService, aggregator, pushService, new ChatListArchive());
            Contacts = new ContactsViewModel(protoService, cacheService, settingsService, aggregator, contactsService);
            Calls = new CallsViewModel(protoService, cacheService, settingsService, aggregator);
            Settings = new SettingsViewModel(protoService, cacheService, settingsService, aggregator, pushService, contactsService, settingsSearchService);

            // This must represent pivot tabs
            Children.Add(Chats);
            Children.Add(Contacts);
            Children.Add(Calls);
            Children.Add(Settings);

            // Any additional child
            Children.Add(ArchivedChats);
            Children.Add(_voipService as TLViewModelBase);

            aggregator.Subscribe(this);

            ReturnToCallCommand = new RelayCommand(ReturnToCallExecute);

            ToggleArchiveCommand = new RelayCommand(ToggleArchiveExecute);

            CreateSecretChatCommand = new RelayCommand(CreateSecretChatExecute);

            SetupFiltersCommand = new RelayCommand(SetupFiltersExecute);

            FilterEditCommand = new RelayCommand<ChatFilterViewModel>(FilterEditExecute);
            FilterAddCommand = new RelayCommand<ChatFilterViewModel>(FilterAddExecute);
            FilterDeleteCommand = new RelayCommand<ChatFilterViewModel>(FilterDeleteExecute);
        }

        public ILifetimeService Lifetime => _lifetimeService;
        public ISessionService Session => _sessionService;

        public IPasscodeService Passcode => _passcodeService;

        public IPlaybackService PlaybackService => _playbackService;

        public IShortcutsService ShortcutService => _shortcutService;

        public RelayCommand ToggleArchiveCommand { get; }
        private void ToggleArchiveExecute()
        {
            CollapseArchivedChats = !CollapseArchivedChats;
        }

        public RelayCommand SetupFiltersCommand { get; }
        private void SetupFiltersExecute()
        {
            NavigationService.Navigate(typeof(FoldersPage));
        }

        public bool CollapseArchivedChats
        {
            get
            {
                return base.Settings.CollapseArchivedChats;
            }
            set
            {
                base.Settings.CollapseArchivedChats = value;
                RaisePropertyChanged();
            }
        }

        private int _unreadCount;
        public int UnreadCount
        {
            get { return _unreadCount; }
            set { Set(ref _unreadCount, value); }
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

        public RelayCommand ReturnToCallCommand { get; }
        private void ReturnToCallExecute()
        {
            _voipService.Show();
        }

        public void Handle(UpdateServiceNotification update)
        {

        }

        public void Handle(UpdateUnreadMessageCount update)
        {
            if (update.ChatList is ChatListArchive)
            {
                return;
            }

            BeginOnUIThread(() =>
            {
                UnreadCount = update.UnreadCount;
                UnreadUnmutedCount = update.UnreadUnmutedCount;
                UnreadMutedCount = update.UnreadCount - update.UnreadUnmutedCount;
            });
        }

        public void Handle(UpdateChatFilters update)
        {
            BeginOnUIThread(() => UpdateChatFilters(update.ChatFilters));
        }

        private void UpdateChatFilters(IList<ChatFilterInfo> chatFilters)
        {
            if (chatFilters.Count > 0)
            {
                var selected = SelectedFilter?.ChatFilterId ?? Constants.ChatListMain;
                //var origin = chatFilters.Select(x => Filters.FirstOrDefault(y => y.ChatFilterId == x.ChatFilterId));

                Merge(Filters, new[] { new ChatFilterInfo { ChatFilterId = Constants.ChatListMain, Title = Strings.Resources.FilterAllChats, Emoji = "\U0001F4BC" } }.Union(chatFilters).ToArray());

                if (Chats.Items.ChatList is ChatListFilter already && already.ChatFilterId != selected)
                {
                    SelectedFilter = Filters[0];
                }
                else
                {
                    RaisePropertyChanged(() => SelectedFilter);
                }

                foreach (var filter in _filters)
                {
                    var unreadCount = CacheService.GetUnreadCount(filter.ChatList);
                    if (unreadCount == null)
                    {
                        continue;
                    }

                    filter.UnreadCount = unreadCount.UnreadChatCount.UnreadUnmutedCount;
                }
            }
            else
            {
                Filters.Clear();
                SelectedFilter = ChatFilterViewModel.Main;
            }
        }

        private void Merge(IList<ChatFilterViewModel> destination, IList<ChatFilterInfo> origin)
        {
            if (destination.Count > 0)
            {
                for (int i = 0; i < destination.Count; i++)
                {
                    var user = destination[i];
                    var index = -1;

                    for (int j = 0; j < origin.Count; j++)
                    {
                        if (origin[j].ChatFilterId == user.ChatFilterId)
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
                    var filter = origin[i];
                    var index = -1;

                    for (int j = 0; j < destination.Count; j++)
                    {
                        if (destination[j].ChatFilterId == filter.ChatFilterId)
                        {
                            destination[j].Update(filter);

                            index = j;
                            break;
                        }
                    }

                    if (index > -1 && index != i)
                    {
                        destination.RemoveAt(index);
                        destination.Insert(Math.Min(i, destination.Count), new ChatFilterViewModel(filter));
                    }
                    else if (index == -1)
                    {
                        destination.Insert(Math.Min(i, destination.Count), new ChatFilterViewModel(filter));
                    }
                }
            }
            else
            {
                destination.Clear();
                destination.AddRange(origin.Select(x => new ChatFilterViewModel(x)));
            }
        }

        private ChatFilterCollection _filters;
        public ChatFilterCollection Filters
        {
            get => _filters;
            set => Set(ref _filters, value);
        }

        public ChatFilterViewModel SelectedFilter
        {
            get
            {
                if (Chats.Items.ChatList is ChatListFilter filter)
                {
                    return _filters.FirstOrDefault(x => x.ChatFilterId == filter.ChatFilterId);
                }
                else if (Chats.Items.ChatList is ChatListArchive)
                {
                    return _filters[1];
                }

                return _filters.FirstOrDefault();
            }
            set
            {
                if (Chats.Items.ChatList.ListEquals(value.ChatList))
                {
                    return;
                }

                Chats.SetFilter(value.ChatList);
                RaisePropertyChanged();
            }
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            //BeginOnUIThread(() => Calls.OnNavigatedToAsync(parameter, mode, state));
            //BeginOnUIThread(() => Settings.OnNavigatedToAsync(parameter, mode, state));
            //Dispatch(() => Dialogs.LoadFirstSlice());
            //Dispatch(() => Contacts.getTLContacts());
            //Dispatch(() => Contacts.GetSelfAsync());

            UpdateChatFilters(CacheService.ChatFilters);

            var unreadCount = CacheService.GetUnreadCount(new ChatListMain());
            UnreadCount = unreadCount.UnreadMessageCount.UnreadCount;
            UnreadMutedCount = unreadCount.UnreadMessageCount.UnreadCount - unreadCount.UnreadMessageCount.UnreadUnmutedCount;

            if (mode == NavigationMode.New)
            {
                Task.Run(() => _pushService.RegisterAsync());
                Task.Run(() => _contactsService.JumpListAsync());
                Task.Run(() => _emojiSetService.UpdateAsync());
            }

            return base.OnNavigatedToAsync(parameter, mode, state);
        }

        public ChatsViewModel Chats { get; private set; }
        public ChatsViewModel ArchivedChats { get; private set; }
        public ContactsViewModel Contacts { get; private set; }
        public CallsViewModel Calls { get; private set; }
        public SettingsViewModel Settings { get; private set; }

        public ChatsViewModel Folder { get; private set; }

        public void SetFolder(ChatList chatList)
        {
            if (chatList is ChatListMain || chatList == null)
            {
                return;
            }

            Folder = ArchivedChats;
            RaisePropertyChanged(() => Folder);
            return;

            Folder = new ChatsViewModel(ProtoService, CacheService, base.Settings, Aggregator, _pushService, chatList);
            Folder.Dispatcher = Dispatcher;
            Folder.NavigationService = NavigationService;
            RaisePropertyChanged(() => Folder);
        }



        public RelayCommand CreateSecretChatCommand { get; }
        private async void CreateSecretChatExecute()
        {
            var selected = await ShareView.PickChatAsync(Strings.Resources.NewSecretChat);
            var user = CacheService.GetUser(selected);

            if (user == null)
            {
                return;
            }

            var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.AreYouSureSecretChat, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var response = await ProtoService.SendAsync(new CreateNewSecretChat(user.Id));
            if (response is Chat chat)
            {
                NavigationService.NavigateToChat(chat);
            }
        }

        public RelayCommand<ChatFilterViewModel> FilterAddCommand { get; }
        private void FilterEditExecute(ChatFilterViewModel filter)
        {
            if (filter.ChatFilterId == Constants.ChatListMain)
            {
                NavigationService.Navigate(typeof(FoldersPage));
            }
            else
            {
                NavigationService.Navigate(typeof(FolderPage), filter.ChatFilterId);
            }
        }

        public RelayCommand<ChatFilterViewModel> FilterEditCommand { get; }
        private async void FilterAddExecute(ChatFilterViewModel filter)
        {
            // Meh I'm lazy
        }

        public RelayCommand<ChatFilterViewModel> FilterDeleteCommand { get; }
        private async void FilterDeleteExecute(ChatFilterViewModel filter)
        {
            var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.FilterDeleteAlert, Strings.Resources.FilterDelete, Strings.Resources.Delete, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            ProtoService.Send(new DeleteChatFilter(filter.ChatFilterId));
        }
    }

    public class ChatFilterViewModel : BindableBase
    {
        public static ChatFilterViewModel Main => new ChatFilterViewModel
        {
            ChatFilterId = Constants.ChatListMain,
            Title = Strings.Resources.FilterAllChats
        };

        public ChatFilterViewModel(ChatFilterInfo info)
        {
            if (info.ChatFilterId == Constants.ChatListMain)
            {
                ChatList = new ChatListMain();
            }
            else
            {
                ChatList = new ChatListFilter(info.ChatFilterId);
                ChatList = new ChatListArchive();
            }

            ChatFilterId = info.ChatFilterId;

            _title = info.Title;
            _emoji = info.Emoji;
            _glyph = ChatFilterIcon.FromEmoji(info.Emoji);
        }

        private ChatFilterViewModel()
        {
            ChatList = new ChatListMain();
        }

        public void Update(ChatFilterInfo info)
        {
            Title = info.Title;
            Emoji = info.Emoji;
            Glyph = ChatFilterIcon.FromEmoji(info.Emoji);
        }

        public ChatList ChatList { get; }

        public int ChatFilterId { get; set; }

        private string _title;
        public string Title
        {
            get => _title;
            set => Set(ref _title, value);
        }

        private string _emoji;
        public string Emoji
        {
            get => _emoji;
            set => Set(ref _emoji, value);
        }

        private string _glyph;
        public string Glyph
        {
            get => _glyph;
            set => Set(ref _glyph, value);
        }

        private int _unreadCount;
        public int UnreadCount
        {
            get => _unreadCount;
            set => Set(ref _unreadCount, value);
        }
    }

    public class ChatFilterIcon
    {
        public string Emoji { get; set; }
        public string Glyph { get; set; }

        private static readonly Dictionary<string, ChatFilterIcon> _map;
        public static IList<ChatFilterIcon> Items { get; }

        public static string Default { get; } = "\U0001F4C1";

        public static string FromEmoji(string emoji)
        {
            if (_map.TryGetValue(emoji, out ChatFilterIcon icon))
            {
                return icon.Glyph;
            }

            return Items[0].Glyph;
        }

        static ChatFilterIcon()
        {
            Items = new ChatFilterIcon[]
            {
                new ChatFilterIcon("\U0001F431", "\uF1AD"),
                new ChatFilterIcon("\U0001F451", ""),
                new ChatFilterIcon("\u2B50",     "\uE734"),
                new ChatFilterIcon("\U0001F339", ""),
                new ChatFilterIcon("\U0001F3AE", "\uE7FC"),
                new ChatFilterIcon("\U0001F3E0", "\uE80F"),
                new ChatFilterIcon("\u2764",     "\uEB51"),
                new ChatFilterIcon("\U0001F3AD", ""),
                new ChatFilterIcon("\U0001F378", ""),
                new ChatFilterIcon("\u26BD", ""),
                new ChatFilterIcon("\U0001F393", "\uE7BE"),
                new ChatFilterIcon("\U0001F4C8", ""),
                new ChatFilterIcon("\u2708",     "\uE709"),
                new ChatFilterIcon("\U0001F4BC", "\uE821"),
                new ChatFilterIcon("\U0001F4AC", "\uE8F2"),
                new ChatFilterIcon("\u2705", ""),
                new ChatFilterIcon("\U0001F514", ""),

                new ChatFilterIcon("\U0001F916", "\uE99A"),
                new ChatFilterIcon("\U0001F4E2", "\uE789"),
                new ChatFilterIcon("\U0001F465", "\uE902"),
                new ChatFilterIcon("\U0001F464", "\uE77B"),
                new ChatFilterIcon("\U0001F4C1", "\uF12B"),
                new ChatFilterIcon("\U0001F4CB", "\uEA37"),
            };
             
            _map = Items.ToDictionary(x => x.Emoji, y => y);
        }

        private ChatFilterIcon(string emoji, string glyph)
        {
            Emoji = emoji;
            Glyph = string.IsNullOrEmpty(glyph) ? emoji : glyph;
        }
    }

    public class ChatFilterCollection : ObservableCollection<ChatFilterViewModel>, IKeyIndexMapping
    {
        public ChatFilterCollection()
        {

        }

        public ChatFilterCollection(IEnumerable<ChatFilterViewModel> source)
            : base(source)
        {

        }

        public string KeyFromIndex(int index)
        {
            return this[index].ChatFilterId.ToString();
        }

        public int IndexFromKey(string key)
        {
            return IndexOf(this.FirstOrDefault(x => key == x.ChatFilterId.ToString()));
        }
    }
}
