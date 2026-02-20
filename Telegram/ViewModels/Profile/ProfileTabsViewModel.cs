//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Chats;
using Telegram.Views.Profile;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Profile
{
    public class ProfileTabArchivedPosts : ProfileTab
    {
        public override string ToString()
        {
            return nameof(ProfileTabArchivedPosts);
        }
    }

    public class ProfileTabSavedChats : ProfileTab
    {
        public override string ToString()
        {
            return nameof(ProfileTabSavedChats);
        }
    }

    public class ProfileTabTopics : ProfileTab
    {
        public override string ToString()
        {
            return nameof(ProfileTabTopics);
        }
    }

    public class ProfileTabPreviews : ProfileTab
    {
        public override string ToString()
        {
            return nameof(ProfileTabPreviews);
        }
    }

    public class ProfileTabGroups : ProfileTab
    {
        public override string ToString()
        {
            return nameof(ProfileTabGroups);
        }
    }

    public class ProfileTabSimilarBots : ProfileTab
    {
        public override string ToString()
        {
            return nameof(ProfileTabSimilarBots);
        }
    }

    public class ProfileTabSimilarChannels : ProfileTab
    {
        public override string ToString()
        {
            return nameof(ProfileTabSimilarChannels);
        }
    }

    public class ProfileTabMembers : ProfileTab
    {
        public override string ToString()
        {
            return nameof(ProfileTabMembers);
        }
    }

    public class ProfileTabSavedMessages : ProfileTab
    {
        public override string ToString()
        {
            return nameof(ProfileTabSavedMessages);
        }
    }

    public partial class ProfileTabItem : BindableBase
    {
        private readonly ICollectionWithTotalCount _items;
        private readonly int _totalCount;
        private readonly string _locale;

        public ProfileTabItem(ProfileTab type, object parameter = null)
        {
            (Text, PageType) = GetText(type);

            Type = type;
            Parameter = parameter;
        }

        public ProfileTabItem(ProfileTab type, object parameter, int totalCount, string locale)
        {
            (Text, PageType) = GetText(type);

            Type = type;
            Parameter = parameter;

            _totalCount = totalCount;
            _locale = locale;
        }

        public ProfileTabItem(ProfileTab type, object parameter, ICollectionWithTotalCount items, string locale)
        {
            (Text, PageType) = GetText(type);

            Type = type;
            Parameter = parameter;

            _items = items;
            _items.PropertyChanged += OnPropertyChanged;

            _locale = locale;
        }

        private (string, Type) GetText(ProfileTab type)
        {
            return type switch
            {
                ProfileTabPosts => (Strings.ProfileStories, typeof(ProfileStoriesTabPage)),
                ProfileTabGifts => (Strings.ProfileGifts, typeof(ProfileGiftsTabPage)),
                ProfileTabArchivedPosts => (Strings.ArchivedStories, typeof(ProfileStoriesTabPage)),
                ProfileTabSavedChats => (Strings.SavedDialogsTab, typeof(ProfileSavedChatsTabPage)),
                ProfileTabTopics => (Strings.Topics, typeof(ProfileTopicsTabPage)),
                ProfileTabPreviews => (Strings.ProfileBotPreviewTab, typeof(ProfileStoriesTabPage)),
                ProfileTabGroups => (Strings.SharedGroupsTab2, typeof(ProfileGroupsTabPage)),
                ProfileTabSimilarBots => (Strings.SimilarBotsTab, typeof(ProfileBotsTabPage)),
                ProfileTabSimilarChannels => (Strings.SimilarChannelsTab, typeof(ProfileChannelsTabPage)),
                ProfileTabMembers => (Strings.ChannelMembers, typeof(ProfileMembersTabPage)),
                ProfileTabMedia => (Strings.SharedMediaTab2, typeof(ProfileMediaTabPage)),
                ProfileTabSavedMessages => (Strings.SavedMessagesTab2, typeof(ProfileSavedMessagesTabPage)),
                ProfileTabFiles => (Strings.SharedFilesTab2, typeof(ProfileFilesTabPage)),
                ProfileTabLinks => (Strings.SharedLinksTab2, typeof(ProfileLinksTabPage)),
                ProfileTabMusic => (Strings.SharedMusicTab2, typeof(ProfileMusicTabPage)),
                ProfileTabVoice => (Strings.SharedVoiceTab2, typeof(ProfileVoiceTabPage)),
                ProfileTabGifs => (Strings.SharedGIFsTab2, typeof(ProfileAnimationsTabPage)),
                _ => (string.Empty, null)
            };
        }

        public ProfileTab Type { get; set; }

        public string Text { get; set; }

        public Type PageType { get; set; }

        public object Parameter { get; set; }

        public string Subtitle => Locale.Declension(_locale, _items?.TotalCount ?? _totalCount);

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_items.TotalCount))
            {
                RaisePropertyChanged(nameof(Subtitle));
            }
        }

        public bool CanSetAsMain => Type is ProfileTabPosts or ProfileTabGifts or ProfileTabMedia or ProfileTabFiles or ProfileTabLinks or ProfileTabMusic or ProfileTabGifs;

        public override string ToString()
        {
            return Text;
        }
    }

    public abstract partial class ProfileTabsViewModel : MediaTabsViewModelBase, IHandle
    {
        protected readonly ProfileSavedChatsTabViewModel _savedChatsTabViewModel;
        protected readonly ProfileTopicsTabViewModel _topicsTabViewModel;
        protected readonly ProfileStoriesTabViewModel _pinnedStoriesTabViewModel;
        protected readonly ProfileStoriesTabViewModel _archivedStoriesTabViewModel;
        protected readonly ProfileGroupsTabViewModel _groupsTabViewModel;
        protected readonly ProfileChannelsTabViewModel _channelsTabViewModel;
        protected readonly ProfileBotsTabViewModel _botsTabViewModel;
        protected readonly ProfileGiftsTabViewModel _giftsTabViewModel;
        protected readonly ProfileMembersTabViewModel _membersTabVieModel;

        public ProfileTabsViewModel(IClientService clientService, ISettingsService settingsService, IStorageService storageService, IEventAggregator aggregator)
            : base(clientService, settingsService, storageService, aggregator)
        {
            _savedChatsTabViewModel = Session.Resolve<ProfileSavedChatsTabViewModel>();
            _topicsTabViewModel = Session.Resolve<ProfileTopicsTabViewModel>();
            _pinnedStoriesTabViewModel = Session.Resolve<ProfileStoriesTabViewModel>();
            _archivedStoriesTabViewModel = Session.Resolve<ProfileStoriesTabViewModel>();
            _groupsTabViewModel = Session.Resolve<ProfileGroupsTabViewModel>();
            _channelsTabViewModel = Session.Resolve<ProfileChannelsTabViewModel>();
            _botsTabViewModel = Session.Resolve<ProfileBotsTabViewModel>();
            _giftsTabViewModel = Session.Resolve<ProfileGiftsTabViewModel>();
            _membersTabVieModel = Session.Resolve<ProfileMembersTabViewModel>();
            _membersTabVieModel.IsEmbedded = true;

            _pinnedStoriesTabViewModel.SetType(ChatStoriesType.Pinned);
            _archivedStoriesTabViewModel.SetType(ChatStoriesType.Archive);

            Children.Add(_savedChatsTabViewModel);
            Children.Add(_topicsTabViewModel);
            Children.Add(_pinnedStoriesTabViewModel);
            Children.Add(_archivedStoriesTabViewModel);
            Children.Add(_groupsTabViewModel);
            Children.Add(_channelsTabViewModel);
            Children.Add(_botsTabViewModel);
            Children.Add(_giftsTabViewModel);
            Children.Add(_membersTabVieModel);

            Items = new MvxObservableCollection<ProfileTabItem>();
        }

        public MvxObservableCollection<ProfileTabItem> Items { get; }

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

        public bool MyProfile { get; private set; }

        public bool IsSavedMessages { get; private set; }

        public override Task NavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is long chatId)
            {
                MyProfile = chatId == ClientService.Options.MyId;
            }
            else if (parameter is ChatMessageTopic chatMessageTopic)
            {
                IsSavedMessages = chatMessageTopic.ChatId == ClientService.Options.MyId;
            }

            return base.NavigatedToAsync(parameter, mode, state);
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is ChatMessageTopic chatMessageTopic)
            {
                parameter = chatMessageTopic.ChatId;
            }

            var chatId = (long)parameter;
            Chat = ClientService.GetChat(chatId);

            Media.UpdateQuery(string.Empty);
            Files.UpdateQuery(string.Empty);
            Links.UpdateQuery(string.Empty);
            Music.UpdateQuery(string.Empty);
            Voice.UpdateQuery(string.Empty);
            Animations.UpdateQuery(string.Empty);

            if (Items.Empty())
            {
                await UpdateTabsAsync(Chat);
            }
        }

        private ProfileTabItem _selectedItem;
        public ProfileTabItem SelectedItem
        {
            get => _selectedItem;
            set => Set(ref _selectedItem, value);
        }

        protected abstract Task UpdateTabsAsync(Chat chat);

        protected async Task UpdateSharedCountAsync(Chat chat, IList<ProfileTabItem> tabs)
        {
            var filters = new SearchMessagesFilter[]
            {
                new SearchMessagesFilterPhotoAndVideo(),
                new SearchMessagesFilterEmpty(),
                new SearchMessagesFilterDocument(),
                new SearchMessagesFilterUrl(),
                new SearchMessagesFilterAudio(),
                new SearchMessagesFilterVoiceAndVideoNote(),
                new SearchMessagesFilterAnimation(),
            };

            var sparseMessagesAvailable = Topic is MessageTopicSavedMessages or null;

            var savedMessagesTopicId = 0L;
            if (Topic is MessageTopicSavedMessages savedMessagesTopic)
            {
                savedMessagesTopicId = savedMessagesTopic.SavedMessagesTopicId;
            }

            async Task<Count> GetCountAsync(SearchMessagesFilter filter)
            {
                if (filter is SearchMessagesFilterEmpty)
                {
                    if (IsSavedMessages || MyProfile)
                    {
                        return new Count(0);
                    }

                    var response = await ClientService.SendAsync(new GetSavedMessagesTopicHistory(chat.Id, 0, 0, 1));
                    if (response is Messages messages)
                    {
                        return new Count(messages.TotalCount);
                    }

                    return new Count(0);
                }

                if (sparseMessagesAvailable && filter is SearchMessagesFilterPhotoAndVideo or SearchMessagesFilterDocument or SearchMessagesFilterAudio or SearchMessagesFilterVoiceAndVideoNote or SearchMessagesFilterAnimation)
                {
                    var source = await ClientService.SendAsync(new GetChatMessageCount(chat.Id, Topic, filter, false)) as Count;
                    if (source?.CountValue > 50)
                    {
                        switch (filter)
                        {
                            case SearchMessagesFilterPhotoAndVideo:
                            case SearchMessagesFilterPhoto:
                            case SearchMessagesFilterVideo:
                                Media.DataSource = new MediaDataSource(ClientService, chat.Id, savedMessagesTopicId, filter);
                                break;
                            case SearchMessagesFilterDocument:
                                Files.DataSource = new MediaDataSource(ClientService, chat.Id, savedMessagesTopicId, filter);
                                break;
                            case SearchMessagesFilterAudio:
                                Music.DataSource = new MediaDataSource(ClientService, chat.Id, savedMessagesTopicId, filter);
                                break;
                            case SearchMessagesFilterVoiceAndVideoNote:
                                Voice.DataSource = new MediaDataSource(ClientService, chat.Id, savedMessagesTopicId, filter);
                                break;
                            case SearchMessagesFilterAnimation:
                                Animations.DataSource = new MediaDataSource(ClientService, chat.Id, savedMessagesTopicId, filter);
                                break;
                        }
                    }

                    return source;
                }

                return await ClientService.SendAsync(new GetChatMessageCount(chat.Id, Topic, filter, false)) as Count;
            }

            for (int i = 0; i < filters.Length; i++)
            {
                var response = await GetCountAsync(filters[i]);
                if (response is Count count)
                {
                    if (count.CountValue > 0)
                    {
                        var item = filters[i] switch
                        {
                            SearchMessagesFilterPhotoAndVideo => new ProfileTabItem(new ProfileTabMedia(), null, count.CountValue, Strings.R.Media),
                            SearchMessagesFilterEmpty => new ProfileTabItem(new ProfileTabSavedMessages(), new ChatMessageTopic(ClientService.Options.MyId, new MessageTopicSavedMessages(chat.Id)), count.CountValue, Strings.R.SavedMessagesCount),
                            SearchMessagesFilterDocument => new ProfileTabItem(new ProfileTabFiles(), null, count.CountValue, Strings.R.Files),
                            SearchMessagesFilterUrl => new ProfileTabItem(new ProfileTabLinks(), null, count.CountValue, Strings.R.Links),
                            SearchMessagesFilterAudio => new ProfileTabItem(new ProfileTabMusic(), null, count.CountValue, Strings.R.MusicFiles),
                            SearchMessagesFilterVoiceAndVideoNote => new ProfileTabItem(new ProfileTabVoice(), null, count.CountValue, Strings.R.Voice),
                            SearchMessagesFilterAnimation => new ProfileTabItem(new ProfileTabGifs(), null, count.CountValue, Strings.R.GIFs),
                            _ => null
                        };

                        tabs.Add(item);
                    }
                }
            }
        }

        protected override bool ShouldHandleDeleteMessages(UpdateDeleteMessages update)
        {
            return update.ChatId == _chat?.Id;
        }

        protected Chat _chat;
        public Chat Chat
        {
            get => _chat;
            set => Set(ref _chat, value);
        }

        public long ChatId => Chat?.Id ?? 0;

        public override MediaCollection SetSearch(object sender, string query)
        {
            var target = sender switch
            {
                SearchMessagesFilterPhotoAndVideo => Media,
                SearchMessagesFilterPhoto => Media,
                SearchMessagesFilterVideo => Media,
                SearchMessagesFilterDocument => Files,
                SearchMessagesFilterAudio => Music,
                SearchMessagesFilterVoiceAndVideoNote => Voice,
                SearchMessagesFilterAnimation => Animations,
                _ => null
            };

            if (sender is SearchMessagesFilter filter && (target?.DataSource == null || query.Length > 0))
            {
                target?.UseDataSource = false;

                return new MediaCollection(ClientService, Chat.Id, Topic, filter, query);
            }

            target?.UseDataSource = true;

            return null;
        }
    }
}
