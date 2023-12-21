using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Stories;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Stories;
using Telegram.Views.Chats;
using Windows.Foundation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Chats
{
    public class ChatStoriesViewModel : ViewModelBase, IIncrementalCollectionOwner
    {
        private ChatStoriesType _type;
        private long _chatId;
        private int _fromStoryId;

        public ChatStoriesViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new IncrementalCollection<StoryViewModel>(this);
            SelectedItems = new MvxObservableCollection<StoryViewModel>();
            SelectedItems.CollectionChanged += OnCollectionChanged;
        }

        private void OnCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            RaisePropertyChanged(nameof(CanSelectedToggleIsPinned));
            RaisePropertyChanged(nameof(CanSelectedBeDeleted));
        }

        public string Title => _type switch
        {
            ChatStoriesType.Archive => Strings.ProfileStoriesArchive,
            ChatStoriesType.Pinned or _ => Strings.ProfileMyStories
        };

        public bool IsPinned => _type == ChatStoriesType.Pinned;

        public ObservableCollection<StoryViewModel> Items { get; }
        public ObservableCollection<StoryViewModel> SelectedItems { get; }

        public bool CanSelectedToggleIsPinned => SelectedItems.All(x => x.CanToggleIsPinned);
        public bool CanSelectedBeDeleted => SelectedItems.All(x => x.CanBeDeleted);

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is ChatStoriesArgs args)
            {
                _chatId = args.ChatId;
                _type = args.Type;
            }
            else if (parameter is long chatId)
            {
                _chatId = chatId;
                _type = ChatStoriesType.Pinned;
            }
            else
            {
                _chatId = ClientService.Options.MyId;
                _type = ChatStoriesType.Pinned;
            }

            return Task.CompletedTask;
        }

        public void OpenArchive()
        {
            NavigationService.Navigate(typeof(ChatStoriesPage), new ChatStoriesArgs(_chatId, ChatStoriesType.Archive));
        }

        public void ToggleStory(StoryViewModel story)
        {
            ClientService.Send(new ToggleStoryIsPinned(story.ChatId, story.StoryId, !IsPinned));

            if (IsPinned)
            {
                Items.Remove(story);
            }

            ToastPopup.Show(IsPinned ? Strings.StoryRemovedFromProfile : Strings.StorySavedToProfile);
        }

        public void ToggleSelectedStories()
        {
            var selection = SelectedItems.ToArray();

            foreach (var story in selection)
            {
                ClientService.Send(new ToggleStoryIsPinned(story.ChatId, story.StoryId, !IsPinned));

                if (IsPinned)
                {
                    Items.Remove(story);
                }
            }

            ToastPopup.Show(Locale.Declension(IsPinned ? Strings.R.StoriesRemovedFromProfile : Strings.R.StoriesSavedToProfile, selection.Length));
            UnselectStories();
        }

        public async void DeleteStory(StoryViewModel story)
        {
            var message = Strings.DeleteStorySubtitle;
            var title = Strings.DeleteStoryTitle;

            var confirm = await ShowPopupAsync(message, title, Strings.Delete, Strings.Cancel, destructive: true);
            if (confirm == ContentDialogResult.Primary)
            {
                ClientService.Send(new DeleteStory(story.ChatId, story.StoryId));
                Items.Remove(story);
            }
        }

        public async void DeleteSelectedStories()
        {
            var message = Locale.Declension(Strings.R.DeleteStoriesSubtitle, SelectedItems.Count);
            var title = SelectedItems.Count == 1 ? Strings.DeleteStoryTitle : Strings.DeleteStoriesTitle;

            var confirm = await ShowPopupAsync(message, title, Strings.Delete, Strings.Cancel, destructive: true);
            if (confirm == ContentDialogResult.Primary)
            {
                var selection = SelectedItems.ToArray();

                foreach (var story in selection)
                {
                    ClientService.Send(new DeleteStory(story.ChatId, story.StoryId));
                    Items.Remove(story);
                }

                UnselectStories();
            }
        }

        public void SelectStory(StoryViewModel story)
        {
            SelectedItems.Add(story);
        }

        public void UnselectStories()
        {
            SelectedItems.Clear();
        }

        public void OpenStory(StoryViewModel story, Rect origin, Func<ActiveStoriesViewModel, Rect> closing)
        {
            var activeStories = new ActiveStoriesViewModel(ClientService, Settings, Aggregator, story, Items);
            var viewModel = new StoryListViewModel(ClientService, Settings, Aggregator, activeStories);
            viewModel.NavigationService = NavigationService;

            var window = new StoriesWindow();
            window.Update(viewModel, activeStories, StoryOpenOrigin.Card, origin, closing);
            _ = window.ShowAsync();
        }

        public async Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            var totalCount = 0u;

            Function function = _type switch
            {
                ChatStoriesType.Archive => new GetChatArchivedStories(_chatId, _fromStoryId, 50),
                ChatStoriesType.Pinned or _ => new GetChatPinnedStories(_chatId, _fromStoryId, 50),
            };

            var response = await ClientService.SendAsync(function);
            if (response is Td.Api.Stories stories)
            {
                HasMoreItems = stories.StoriesValue.Count > 0;

                foreach (var story in stories.StoriesValue)
                {
                    _fromStoryId = story.Id;
                    Items.Add(new StoryViewModel(ClientService, story));
                }
            }

            IsEmpty = Items.Count == 0;
            return new LoadMoreItemsResult
            {
                Count = totalCount
            };
        }

        public bool HasMoreItems { get; private set; } = true;

        private bool _isEmpty = false;
        public bool IsEmpty
        {
            get => _isEmpty;
            set
            {
                if (Set(ref _isEmpty, value))
                {
                    RaisePropertyChanged(nameof(ShowHint));
                }
            }
        }

        public bool ShowHint => !IsEmpty && _type == ChatStoriesType.Archive && _chatId != ClientService.Options.MyId;
    }
}
