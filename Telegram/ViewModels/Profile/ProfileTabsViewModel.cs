//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Rg.DiffUtils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.Views;
using Telegram.Views.Popups;
using Telegram.Views.Profile;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Profile
{
    public class ProfileTabItem
    {
        public string Text { get; set; }

        public Type Type { get; set; }

        public ProfileTabItem(string text, Type type)
        {
            Text = text;
            Type = type;
        }
    }

    public class ProfileTabsViewModel : MultiViewModelBase, IHandle
    {
        private readonly IPlaybackService _playbackService;
        private readonly IStorageService _storageService;

        private readonly IMessageDelegate _messageDelegate;

        protected readonly ProfileSavedChatsTabViewModel _savedChatsViewModel;
        protected readonly ProfileStoriesTabViewModel _storiesTabViewModel;
        protected readonly ProfileGroupsTabViewModel _groupsTabViewModel;
        protected readonly ProfileChannelsTabViewModel _channelsTabViewModel;
        protected readonly ProfileMembersTabViewModel _membersTabVieModel;

        public ProfileTabsViewModel(IClientService clientService, ISettingsService settingsService, IStorageService storageService, IEventAggregator aggregator, IPlaybackService playbackService)
            : base(clientService, settingsService, aggregator)
        {
            _playbackService = playbackService;
            _storageService = storageService;

            _messageDelegate = new MessageDelegate(this);

            _savedChatsViewModel = TypeResolver.Current.Resolve<ProfileSavedChatsTabViewModel>(clientService.SessionId);
            _storiesTabViewModel = TypeResolver.Current.Resolve<ProfileStoriesTabViewModel>(clientService.SessionId);
            _groupsTabViewModel = TypeResolver.Current.Resolve<ProfileGroupsTabViewModel>(clientService.SessionId);
            _channelsTabViewModel = TypeResolver.Current.Resolve<ProfileChannelsTabViewModel>(clientService.SessionId);
            _membersTabVieModel = TypeResolver.Current.Resolve<ProfileMembersTabViewModel>(clientService.SessionId);
            _membersTabVieModel.IsEmbedded = true;

            Children.Add(_savedChatsViewModel);
            Children.Add(_storiesTabViewModel);
            Children.Add(_groupsTabViewModel);
            Children.Add(_channelsTabViewModel);
            Children.Add(_membersTabVieModel);

            Items = new ObservableCollection<ProfileTabItem>();

            SelectedItems = new MvxObservableCollection<MessageWithOwner>();
            SelectedItems.CollectionChanged += OnConnectionChanged;

            Media = new SearchCollection<MessageWithOwner, MediaCollection>(SetSearch, new SearchMessagesFilterPhotoAndVideo(), new MessageDiffHandler());
            Files = new SearchCollection<MessageWithOwner, MediaCollection>(SetSearch, new SearchMessagesFilterDocument(), new MessageDiffHandler());
            Links = new SearchCollection<MessageWithOwner, MediaCollection>(SetSearch, new SearchMessagesFilterUrl(), new MessageDiffHandler());
            Music = new SearchCollection<MessageWithOwner, MediaCollection>(SetSearch, new SearchMessagesFilterAudio(), new MessageDiffHandler());
            Voice = new SearchCollection<MessageWithOwner, MediaCollection>(SetSearch, new SearchMessagesFilterVoiceNote(), new MessageDiffHandler());
            Animations = new SearchCollection<MessageWithOwner, MediaCollection>(SetSearch, new SearchMessagesFilterAnimation(), new MessageDiffHandler());
        }

        private void OnConnectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            RaisePropertyChanged(nameof(CanForwardSelectedMessages));
            RaisePropertyChanged(nameof(CanDeleteSelectedMessages));
        }

        public ObservableCollection<ProfileTabItem> Items { get; }

        public IPlaybackService PlaybackService => _playbackService;

        public IStorageService StorageService => _storageService;

        protected MessageThreadInfo _thread;
        public MessageThreadInfo Thread
        {
            get => _thread;
            set => Set(ref _thread, value);
        }

        public long ThreadId => Thread?.MessageThreadId ?? 0;

        protected ForumTopicInfo _topic;
        public ForumTopicInfo Topic
        {
            get => _topic;
            set => Set(ref _topic, value);
        }

        public long SavedMessagesTopicId => SavedMessagesTopic?.Id ?? 0;

        protected SavedMessagesTopic _savedMessagesTopic;
        public SavedMessagesTopic SavedMessagesTopic
        {
            get => _savedMessagesTopic;
            set => Set(ref _savedMessagesTopic, value);
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is ChatSavedMessagesTopicIdNavigationArgs savedMessagesTopicIdArgs)
            {
                parameter = savedMessagesTopicIdArgs.ChatId;
            }
            else if (parameter is ChatMessageIdNavigationArgs args)
            {
                parameter = args.ChatId;
            }

            var chatId = (long)parameter;

            if (state.TryGet("selectedIndex", out int selectedIndex))
            {
                SelectedIndex = selectedIndex;
            }

            Chat = ClientService.GetChat(chatId);

            Media.UpdateQuery(string.Empty);
            Files.UpdateQuery(string.Empty);
            Links.UpdateQuery(string.Empty);
            Music.UpdateQuery(string.Empty);
            Voice.UpdateQuery(string.Empty);
            Animations.UpdateQuery(string.Empty);

            Aggregator.Subscribe<UpdateDeleteMessages>(this, Handle);

            await UpdateTabsAsync(Chat);
        }

        private int[] _sharedCount = new int[] { 0, 0, 0, 0, 0, 0 };
        public int[] SharedCount
        {
            get => _sharedCount;
            set => Set(ref _sharedCount, value);
        }

        private ProfileTabItem _selectedItem;
        public ProfileTabItem SelectedItem
        {
            get => _selectedItem;
            set => Set(ref _selectedItem, value);
        }

        private async Task UpdateTabsAsync(Chat chat)
        {
            if (_savedMessagesTopic != null)
            {
                await UpdateSharedCountAsync(chat);
            }
            else if (chat.Type is ChatTypePrivate or ChatTypeSecret)
            {
                var user = ClientService.GetUser(chat);
                var cached = ClientService.GetUserFull(chat);

                // This should really rarely happen
                cached ??= await ClientService.SendAsync(new GetUserFullInfo(user.Id)) as UserFullInfo;

                if (user.Id == ClientService.Options.MyId)
                {
                    AddTab(new ProfileTabItem(Strings.SavedDialogsTab, typeof(ProfileSavedChatsTabPage)));
                }
                else if (cached != null && cached.HasPinnedStories)
                {
                    AddTab(new ProfileTabItem(Strings.ProfileStories, typeof(ProfileStoriesTabPage)));
                }

                await UpdateSharedCountAsync(chat);

                if (cached != null && cached.GroupInCommonCount > 0)
                {
                    AddTab(new ProfileTabItem(Strings.SharedGroupsTab2, typeof(ProfileGroupsTabPage)));
                }
            }
            else if (chat.Type is ChatTypeSupergroup typeSupergroup && typeSupergroup.IsChannel)
            {
                var supergroup = ClientService.GetSupergroup(chat);
                var cached = ClientService.GetSupergroupFull(chat);

                // This should really rarely happen
                cached ??= await ClientService.SendAsync(new GetSupergroupFullInfo(supergroup.Id)) as SupergroupFullInfo;

                if (cached != null && cached.HasPinnedStories)
                {
                    AddTab(new ProfileTabItem(Strings.ProfileStories, typeof(ProfileStoriesTabPage)));
                }

                await UpdateSharedCountAsync(chat);
                await _channelsTabViewModel.LoadMoreItemsAsync(0);

                if (_channelsTabViewModel.Items.Count > 0)
                {
                    AddTab(new ProfileTabItem(Strings.SimilarChannelsTab, typeof(ProfileChannelsTabPage)));
                }
            }
            else if (chat.Type is ChatTypeBasicGroup || Chat.Type is ChatTypeSupergroup supergroup && !supergroup.IsChannel)
            {
                AddTab(new ProfileTabItem(Strings.ChannelMembers, typeof(ProfileMembersTabPage)));

                await UpdateSharedCountAsync(chat);
            }
        }

        private async Task UpdateSharedCountAsync(Chat chat)
        {
            var filters = new SearchMessagesFilter[]
            {
                new SearchMessagesFilterPhotoAndVideo(),
                new SearchMessagesFilterDocument(),
                new SearchMessagesFilterUrl(),
                new SearchMessagesFilterAudio(),
                new SearchMessagesFilterVoiceNote(),
                //new SearchMessagesFilterAnimation(),
            };

            for (int i = 0; i < filters.Length; i++)
            {
                var response = await ClientService.SendAsync(new GetChatMessageCount(chat.Id, filters[i], SavedMessagesTopicId, false));
                if (response is Count count)
                {
                    SharedCount[i] = count.CountValue;

                    if (count.CountValue > 0)
                    {
                        var item = filters[i] switch
                        {
                            SearchMessagesFilterPhotoAndVideo => new ProfileTabItem(Strings.SharedMediaTab2, typeof(ProfileMediaTabPage)),
                            SearchMessagesFilterDocument => new ProfileTabItem(Strings.SharedFilesTab2, typeof(ProfileFilesTabPage)),
                            SearchMessagesFilterUrl => new ProfileTabItem(Strings.SharedLinksTab2, typeof(ProfileLinksTabPage)),
                            SearchMessagesFilterAudio => new ProfileTabItem(Strings.SharedMusicTab2, typeof(ProfileMusicTabPage)),
                            SearchMessagesFilterVoiceNote => new ProfileTabItem(Strings.SharedVoiceTab2, typeof(ProfileVoiceTabPage)),
                            _ => null
                        };

                        AddTab(item);
                    }
                }
            }
        }

        private void AddTab(ProfileTabItem item)
        {
            Items.Add(item);

            if (Items.Count == 1)
            {
                SelectedItem ??= Items.FirstOrDefault();
                RaisePropertyChanged(nameof(SharedCount));
            }
        }

        public void Handle(UpdateDeleteMessages update)
        {
            if (update.ChatId == _chat?.Id && !update.FromCache)
            {
                var table = update.MessageIds.ToHashSet();

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

        private void UpdateDeleteMessages(IList<MessageWithOwner> target, HashSet<long> table)
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
                return new MediaCollection(ClientService, Chat.Id, ThreadId, SavedMessagesTopicId, filter, query);
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

        public ObservableCollection<MessageWithOwner> SelectedItems { get; }

        #region View

        public void ViewMessage(MessageWithOwner message)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.NavigateToChat(chat, message.Id, ThreadId, SavedMessagesTopicId);
        }

        #endregion

        #region Save file as

        public async void SaveMessageMedia(MessageWithOwner message)
        {
            var file = message.GetFile();
            if (file != null)
            {
                await _storageService.SaveFileAsAsync(file);
            }
        }

        #endregion

        #region Open with

        public async void OpenMessageWith(MessageWithOwner message)
        {
            var file = message.GetFile();
            if (file != null)
            {
                await _storageService.OpenFileWithAsync(file);
            }
        }

        #endregion

        #region Show in folder

        public async void OpenMessageFolder(MessageWithOwner message)
        {
            var file = message.GetFile();
            if (file != null)
            {
                await _storageService.OpenFolderAsync(file);
            }
        }

        #endregion

        #region Delete

        public void DeleteMessage(MessageWithOwner message)
        {
            if (message == null)
            {
                return;
            }

            var chat = ClientService.GetChat(message.ChatId);
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

            DeleteMessages(chat, new[] { message });
        }

        private async void DeleteMessages(Chat chat, IList<MessageWithOwner> messages)
        {
            var first = messages.FirstOrDefault();
            if (first == null)
            {
                return;
            }

            var items = messages.Select(x => x.Get()).ToList();

            var response = await ClientService.SendAsync(new GetMessages(chat.Id, items.Select(x => x.Id).ToArray()));
            if (response is Messages updated)
            {
                for (int i = 0; i < updated.MessagesValue.Count; i++)
                {
                    if (updated.MessagesValue[i] != null)
                    {
                        items[i] = updated.MessagesValue[i];
                    }
                    else
                    {
                        items.RemoveAt(i);
                        updated.MessagesValue.RemoveAt(i);

                        i--;
                    }
                }
            }

            if (items.Count == 0)
            {
                return;
            }

            var sameUser = messages.All(x => x.SenderId.AreTheSame(first.SenderId));
            var dialog = new DeleteMessagesPopup(ClientService, SavedMessagesTopicId, items.Where(x => x != null).ToArray());

            var confirm = await ShowPopupAsync(dialog);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            UnselectMessages();

            if (dialog.DeleteAll && sameUser)
            {
                ClientService.Send(new DeleteChatMessagesBySender(chat.Id, first.SenderId));
            }
            else
            {
                ClientService.Send(new DeleteMessages(chat.Id, messages.Select(x => x.Id).ToList(), dialog.Revoke));
            }

            if (dialog.BanUser && sameUser)
            {
                ClientService.Send(new SetChatMemberStatus(chat.Id, first.SenderId, new ChatMemberStatusBanned()));
            }

            if (dialog.ReportSpam && sameUser && chat.Type is ChatTypeSupergroup supertype)
            {
                ClientService.Send(new ReportSupergroupSpam(supertype.SupergroupId, messages.Select(x => x.Id).ToList()));
            }
        }

        #endregion

        #region Forward

        public async void ForwardMessage(MessageWithOwner message)
        {
            UnselectMessages();
            await ShowPopupAsync(typeof(ChooseChatsPopup), new ChooseChatsConfigurationShareMessage(message.ChatId, message.Id));
        }

        #endregion

        #region Multiple Delete

        public void DeleteSelectedMessages()
        {
            var messages = new List<MessageWithOwner>(SelectedItems);

            var first = messages.FirstOrDefault();
            if (first == null)
            {
                return;
            }

            var chat = ClientService.GetChat(first.ChatId);
            if (chat == null)
            {
                return;
            }

            DeleteMessages(chat, messages);
        }

        public bool CanDeleteSelectedMessages => SelectedItems.Count > 0 && SelectedItems.All(x => x.CanBeDeletedForAllUsers || x.CanBeDeletedOnlyForSelf);

        #endregion

        #region Multiple Forward

        public async void ForwardSelectedMessages()
        {
            var messages = SelectedItems.Where(x => x.CanBeForwarded).OrderBy(x => x.Id).ToList();
            if (messages.Count > 0)
            {
                UnselectMessages();
                await ShowPopupAsync(typeof(ChooseChatsPopup), new ChooseChatsConfigurationShareMessages(messages[0].ChatId, messages.Select(x => x.Id)));
            }
        }

        public bool CanForwardSelectedMessages => SelectedItems.Count > 0 && SelectedItems.All(x => x.CanBeForwarded);

        #endregion

        #region Select

        public void SelectMessage(MessageWithOwner message)
        {
            SelectedItems.Add(message);
        }

        #endregion

        #region Unselect

        public void UnselectMessages()
        {
            SelectedItems.Clear();
        }

        #endregion

        #region Delegate

        public IMessageDelegate MessageDelegate => _messageDelegate;

        public void OpenUsername(string username)
        {
            _messageDelegate.OpenUsername(username);
        }

        public void OpenUser(long userId)
        {
            _messageDelegate.OpenUser(userId);
        }

        public void OpenUrl(string url, bool untrust)
        {
            _messageDelegate.OpenUrl(url, untrust);
        }

        #endregion
    }
}
