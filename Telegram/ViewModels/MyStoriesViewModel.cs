using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls.Stories;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Stories;
using Telegram.Views;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels
{
    public enum MyStoriesType
    {
        Pinned,
        Archive
    }

    public class MyStoriesViewModel : ViewModelBase, IIncrementalCollectionOwner
    {
        private MyStoriesType _type;
        private int _fromStoryId;

        public MyStoriesViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new IncrementalCollection<StoryViewModel>(this);
            SelectedItems = new MvxObservableCollection<StoryViewModel>();
        }

        public string Title => _type switch
        {
            MyStoriesType.Archive => Strings.ProfileStoriesArchive,
            MyStoriesType.Pinned or _ => Strings.ProfileMyStories
        };

        public bool IsPinned => _type == MyStoriesType.Pinned;

        public ObservableCollection<StoryViewModel> Items { get; }
        public ObservableCollection<StoryViewModel> SelectedItems { get; }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is MyStoriesType type)
            {
                _type = type;
            }
            else
            {
                _type = MyStoriesType.Pinned;
            }

            return Task.CompletedTask;
        }

        public void OpenArchive()
        {
            NavigationService.Navigate(typeof(MyStoriesPage), MyStoriesType.Archive);
        }

        public void ToggleStory(StoryViewModel story)
        {
            ClientService.Send(new ToggleStoryIsPinned(story.StoryId, !IsPinned));

            if (IsPinned)
            {
                Items.Remove(story);
            }

            Window.Current.ShowTeachingTip(IsPinned ? Strings.StoryRemovedFromProfile : Strings.StorySavedToProfile);
        }

        public void ToggleSelectedStories()
        {
            var selection = SelectedItems.ToArray();

            foreach (var story in selection)
            {
                ClientService.Send(new ToggleStoryIsPinned(story.StoryId, !IsPinned));

                if (IsPinned)
                {
                    Items.Remove(story);
                }
            }

            Window.Current.ShowTeachingTip(Locale.Declension(IsPinned ? Strings.R.StoriesRemovedFromProfile : Strings.R.StoriesSavedToProfile, selection.Length));
            UnselectStories();
        }

        public async void DeleteStory(StoryViewModel story)
        {
            var message = Strings.DeleteStorySubtitle;
            var title = Strings.DeleteStoryTitle;

            var confirm = await ShowPopupAsync(message, title, Strings.Delete, Strings.Cancel, dangerous: true);
            if (confirm == ContentDialogResult.Primary)
            {
                ClientService.Send(new DeleteStory(story.StoryId));
                Items.Remove(story);
            }
        }

        public async void DeleteSelectedStories()
        {
            var message = Locale.Declension(Strings.R.DeleteStoriesSubtitle, SelectedItems.Count);
            var title = SelectedItems.Count == 1 ? Strings.DeleteStoryTitle : Strings.DeleteStoriesTitle;

            var confirm = await ShowPopupAsync(message, title, Strings.Delete, Strings.Cancel, dangerous: true);
            if (confirm == ContentDialogResult.Primary)
            {
                var selection = SelectedItems.ToArray();

                foreach (var story in selection)
                {
                    ClientService.Send(new DeleteStory(story.StoryId));
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
            window.Update(viewModel, activeStories, StoryOrigin.Card, origin, closing);
            _ = window.ShowAsync();
        }

        public async Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            var totalCount = 0u;

            Function function = _type switch
            {
                MyStoriesType.Archive => new GetArchivedStories(_fromStoryId, 50),
                MyStoriesType.Pinned or _ => new GetChatPinnedStories(ClientService.Options.MyId, _fromStoryId, 50),
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
            set => Set(ref _isEmpty, value);
        }
    }
}
