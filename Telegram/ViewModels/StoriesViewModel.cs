using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views;
using Windows.UI.Xaml.Data;

namespace Telegram.ViewModels
{
    public class StoriesViewModel : ViewModelBase, IIncrementalCollectionOwner
    {
        public StoriesViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new IncrementalCollection<ActiveStoriesViewModel>(this);
        }

        public IncrementalCollection<ActiveStoriesViewModel> Items { get; }

        public bool HiddenStories { get; set; }

        public async Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            var totalCount = 0u;

            var response = await ClientService.SendAsync(new GetContacts());
            if (response is Td.Api.Users users)
            {
                if (ClientService.TryGetUser(ClientService.Options.MyId, out User self) && self.HasActiveStories)
                {
                    totalCount++;
                    Items.Add(new ActiveStoriesViewModel(ClientService, ClientService.Options.MyId));
                }

                foreach (var userId in users.UserIds)
                {
                    if (ClientService.TryGetUser(userId, out User user) && user.HasActiveStories)
                    {
                        totalCount++;
                        Items.Add(new ActiveStoriesViewModel(ClientService, userId));
                    }
                }
            }

            HasMoreItems = false;

            return new LoadMoreItemsResult
            {
                Count = totalCount
            };
        }

        public bool HasMoreItems { get; private set; } = true;

        public void OpenProfile(ActiveStoriesViewModel activeStories)
        {
            var chat = activeStories.Chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(ProfilePage), chat.Id);
        }

        public void MuteProfile(ActiveStoriesViewModel activeStories)
        {
            var chat = activeStories.Chat;
            if (chat == null)
            {
                return;
            }

            var settings = chat.NotificationSettings;

            ClientService.Send(new SetChatNotificationSettings(chat.Id,
                new ChatNotificationSettings(
                    settings.UseDefaultMuteFor, settings.MuteFor,
                    settings.UseDefaultSound, settings.SoundId,
                    settings.UseDefaultShowPreview, settings.ShowPreview,
                    false, !settings.MuteStories,
                    settings.UseDefaultDisablePinnedMessageNotifications, settings.DisablePinnedMessageNotifications,
                    settings.UseDefaultDisableMentionNotifications, settings.DisableMentionNotifications)));
        }

        public void HideProfile(ActiveStoriesViewModel activeStories)
        {
            Items.Remove(activeStories);
            ClientService.Send(new ToggleUserStoriesAreHidden(activeStories.UserId, true));
        }

        public void ShowProfile(ActiveStoriesViewModel activeStories)
        {
            Items.Remove(activeStories);
            ClientService.Send(new ToggleUserStoriesAreHidden(activeStories.UserId, false));
        }
    }

    public class StoryViewModel : BindableBase
    {
        public IClientService ClientService { get; }

        public StoryViewModel(IClientService clientService, long userId, StoryInfo storyInfo)
        {
            ClientService = clientService;

            UserId = userId;

            Date = storyInfo.Date;
            StoryId = storyInfo.StoryId;
        }

        public int Date { get; set; }

        public long UserId { get; set; }

        public int StoryId { get; set; }

        public static async Task<StoryViewModel> LoadAsyc(IClientService clientService, long userId, StoryInfo storyInfo)
        {
            var story = new StoryViewModel(clientService, userId, storyInfo);
            await story.InitializeAsync();
            return story;
        }

        private async Task InitializeAsync()
        {
            var response = await ClientService.SendAsync(new GetStory(UserId, StoryId));
            if (response is Story story)
            {
                Caption = story.Caption;
                Content = story.Content;
                PrivacyRules = story.PrivacyRules;
                InteractionInfo = story.InteractionInfo;
                CanGetViewers = story.CanGetViewers;
                IsPinned = story.IsPinned;
            }
        }

        public FormattedText Caption { get; private set; }

        public StoryContent Content { get; private set; }

        /// <summary>
        /// Privacy rules affecting story visibility; may be null if the story isn't owned.
        /// </summary>
        public UserPrivacySettingRules PrivacyRules { get; private set; }

        /// <summary>
        /// Information about interactions with the story; may be null if the story isn't
        /// owned or there were no interactions.
        /// </summary>
        public StoryInteractionInfo InteractionInfo { get; private set; }

        /// <summary>
        /// True, users viewed the story can be received through getStoryViewers.
        /// </summary>
        public bool CanGetViewers { get; private set; }

        /// <summary>
        /// True, if the story is saved in the sender's profile and will be available there
        /// after expiration.
        /// </summary>
        public bool IsPinned { get; private set; }

    }

    public class ActiveStoriesViewModel : BindableBase
    {
        private readonly IClientService _clientService;
        private readonly long _userId;

        private Chat _chat;

        public ActiveStoriesViewModel(IClientService clientService, long userId)
        {
            _clientService = clientService;
            _userId = userId;

            Items = new ObservableCollection<StoryViewModel>();
            Initialize(false);
        }

        public IClientService ClientService => _clientService;

        public User User => _clientService.GetUser(_userId);

        public long UserId => _userId;

        public Chat Chat => _chat;

        public ObservableCollection<StoryViewModel> Items { get; }

        private StoryViewModel _selectedItem;
        public StoryViewModel SelectedItem
        {
            get => _selectedItem;
            set => Set(ref _selectedItem, value);
        }

        private ActiveStoriesState _state;
        public ActiveStoriesState State
        {
            get => _state;
            set => Set(ref _state, value);
        }

        private int _unreadCount;
        public int UnreadCount
        {
            get => _unreadCount;
            set => Set(ref _unreadCount, value);
        }

        public string Subtitle => UnreadCount > 0
            ? Locale.Declension(Strings.R.NewStories, UnreadCount)
            : Locale.Declension(Strings.R.Stories, Items.Count);

        private async void Initialize(bool lazy)
        {
            if (lazy is false)
            {
                var response2 = await _clientService.SendAsync(new CreatePrivateChat(_userId, true));
                if (response2 is Chat chat)
                {
                    _chat = chat;
                }
            }

            var response = await _clientService.SendAsync(new GetUserActiveStories(_userId));
            if (response is ActiveStories activeStories)
            {
                foreach (var story in activeStories.Stories)
                {
                    var item = lazy
                        ? new StoryViewModel(_clientService, _userId, story)
                        : await StoryViewModel.LoadAsyc(_clientService, _userId, story);

                    Items.Add(item);

                    if (story.StoryId > activeStories.MaxReadStoryId)
                    {
                        SelectedItem ??= item;
                        UnreadCount++;
                    }
                }

                if (SelectedItem == null)
                {
                    State = ActiveStoriesState.Read;
                    SelectedItem = Items.LastOrDefault();
                }
                else if (SelectedItem.PrivacyRules.AllowCloseFriends())
                {
                    State = ActiveStoriesState.CloseFriends;
                }
                else
                {
                    State = ActiveStoriesState.Unread;
                }
            }

            RaisePropertyChanged(nameof(Subtitle));
        }
    }

    public enum ActiveStoriesState
    {
        Unread,
        CloseFriends,
        Read
    }
}
