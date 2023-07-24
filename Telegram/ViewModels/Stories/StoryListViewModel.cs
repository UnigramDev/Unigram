using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls.Stories;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views;
using Telegram.Views.Popups;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace Telegram.ViewModels.Stories
{
    public class StoryListViewModel : ViewModelBase
    {
        public StoryListViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, StoryList storyList)
            : base(clientService, settingsService, aggregator)
        {
            Items = new ItemsCollection(clientService, aggregator, this, storyList);
        }

        public StoryListViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, params ActiveStoriesViewModel[] items)
            : base(clientService, settingsService, aggregator)
        {
            Items = new ObservableCollection<ActiveStoriesViewModel>(items);
        }

        public ObservableCollection<ActiveStoriesViewModel> Items { get; }

        public void SendMessage(ActiveStoriesViewModel activeStories)
        {
            var chat = activeStories.Chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.NavigateToChat(chat);
        }

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

            var settings = chat.NotificationSettings.Clone();
            settings.UseDefaultMuteStories = false;
            settings.MuteStories = !settings.MuteStories;

            ClientService.Send(new SetChatNotificationSettings(chat.Id, settings));

            if (ClientService.TryGetUser(activeStories.Chat, out User user))
            {
                Window.Current.ShowTeachingTip(string.Format(settings.MuteStories ? Strings.NotificationsStoryMutedHint : Strings.NotificationsStoryUnmutedHint, user.FirstName));
            }
        }

        public void HideProfile(ActiveStoriesViewModel activeStories)
        {
            ClientService.Send(new SetChatActiveStoriesList(activeStories.ChatId, new StoryListArchive()));

            if (ClientService.TryGetUser(activeStories.Chat, out User user))
            {
                Window.Current.ShowTeachingTip(string.Format(Strings.StoriesMovedToContacts, user.FirstName));
            }
        }

        public void ShowProfile(ActiveStoriesViewModel activeStories)
        {
            ClientService.Send(new SetChatActiveStoriesList(activeStories.ChatId, new StoryListMain()));

            if (ClientService.TryGetUser(activeStories.Chat, out User user))
            {
                Window.Current.ShowTeachingTip(string.Format(Strings.StoriesMovedToDialogs, user.FirstName));
            }
        }

        public void ShareStory(StoryViewModel story)
        {
            ShowPopup(typeof(ChooseChatsPopup), new ChooseChatsConfigurationShareStory(story.ChatId, story.StoryId), requestedTheme: ElementTheme.Dark);
        }

        public async void DeleteStory(StoryViewModel story)
        {
            var message = Strings.DeleteStorySubtitle;
            var title = Strings.DeleteStoryTitle;

            var confirm = await ShowPopupAsync(message, title, Strings.Delete, Strings.Cancel, dangerous: true);
            if (confirm == ContentDialogResult.Primary)
            {
                ClientService.Send(new DeleteStory(story.StoryId));
            }
        }

        public void ToggleStory(StoryViewModel story)
        {
            ClientService.Send(new ToggleStoryIsPinned(story.StoryId, !story.IsPinned));

            Window.Current.ShowTeachingTip(story.IsPinned ? Strings.StoryRemovedFromProfile : Strings.StorySavedToProfile);
        }

        public async void ReportStory(StoryViewModel story)
        {
            var form = await DialogViewModel.GetReportFormAsync(NavigationService);
            if (form.Reason != null)
            {
                ClientService.Send(new ReportStory(story.ChatId, story.StoryId, form.Reason, form.Text));
            }
        }

        protected ActiveStoriesViewModel Create(ChatActiveStories activeStories)
        {
            return new ActiveStoriesViewModel(ClientService, Settings, Aggregator, activeStories);
        }

        public void OpenStory(ActiveStoriesViewModel activeStories, Rect origin, Func<ActiveStoriesViewModel, Rect> closing)
        {
            var items = Items.ToArray();

            var viewModel = new StoryListViewModel(ClientService, Settings, Aggregator, items);
            viewModel.NavigationService = NavigationService;

            var window = new StoriesWindow();
            window.Update(viewModel, activeStories, StoryOrigin.ProfilePhoto, origin, closing);
            _ = window.ShowAsync();
        }

        public class ItemsCollection : ObservableCollection<ActiveStoriesViewModel>, ISupportIncrementalLoading
        {
            private readonly IClientService _clientService;
            private readonly IEventAggregator _aggregator;

            private readonly DisposableMutex _loadMoreLock = new();
            private readonly Dictionary<long, ActiveStoriesViewModel> _chats = new();

            private readonly StoryListViewModel _viewModel;

            private StoryList _storyList;

            private bool _hasMoreItems = true;

            private long _lastChatId;
            private long _lastOrder;

            public StoryList StoryList => _storyList;

            public ItemsCollection(IClientService clientService, IEventAggregator aggregator, StoryListViewModel viewModel, StoryList storyList)
            {
                _clientService = clientService;
                _aggregator = aggregator;

                _viewModel = viewModel;

                _storyList = storyList;

#if MOCKUP
                _hasMoreItems = false;
#endif

                _ = LoadMoreItemsAsync(0);
            }

            public async Task ReloadAsync(StoryList storyList)
            {
                using (await _loadMoreLock.WaitAsync())
                {
                    _aggregator.Unsubscribe(this);

                    _lastChatId = 0;
                    _lastOrder = 0;

                    _storyList = storyList;

                    _chats.Clear();
                    Clear();
                }

                await LoadMoreItemsAsync();
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(token => LoadMoreItemsAsync());
            }

            private async Task<LoadMoreItemsResult> LoadMoreItemsAsync()
            {
                using (await _loadMoreLock.WaitAsync())
                {
                    var response = await _clientService.GetStoryListAsync(_storyList, Count, 20);
                    if (response is Telegram.Td.Api.Chats chats)
                    {
                        var totalCount = 0u;

                        foreach (var activeStories in _clientService.GetActiveStorieses(chats.ChatIds))
                        {
                            var order = activeStories.Order;
                            if (order != 0)
                            {
                                // TODO: is this redundant?
                                var next = NextIndexOf(activeStories.ChatId, order);
                                if (next >= 0)
                                {
                                    if (_chats.TryGetValue(activeStories.ChatId, out var prev))
                                    {
                                        Remove(prev);
                                    }

                                    var item = _viewModel.Create(activeStories);

                                    _chats[activeStories.ChatId] = item;
                                    Insert(Math.Min(Count, next), item);

                                    totalCount++;
                                }

                                _lastChatId = activeStories.ChatId;
                                _lastOrder = order;
                            }
                        }

                        IsEmpty = Count == 0;

                        _hasMoreItems = chats.ChatIds.Count > 0;
                        Subscribe();

                        if (_hasMoreItems == false)
                        {
                            OnPropertyChanged(new PropertyChangedEventArgs("HasMoreItems"));
                        }

                        return new LoadMoreItemsResult { Count = totalCount };
                    }

                    return new LoadMoreItemsResult { Count = 0 };
                }
            }

            private void Subscribe()
            {
                _aggregator.Subscribe<UpdateChatActiveStories>(this, Handle);
            }

            public bool HasMoreItems => _hasMoreItems;

            #region Handle

            public void Handle(UpdateChatActiveStories update)
            {
                if ((update.ActiveStories.List is StoryListMain && _storyList is StoryListMain) || (update.ActiveStories.List is StoryListArchive && _storyList is StoryListArchive))
                {
                    Handle(update.ActiveStories, update.ActiveStories.Order, true);
                }
                else
                {
                    Handle(update.ActiveStories, 0, true);
                }
            }

            private void Handle(ChatActiveStories activeStories, long order, bool lastMessage)
            {
                if (_chats.TryGetValue(activeStories.ChatId, out var item))
                {
                    item.Update(activeStories);

                    _viewModel.BeginOnUIThread(() => UpdateChatOrder(item, order, lastMessage));
                }
                else
                {
                    item = _viewModel.Create(activeStories);

                    _chats.Add(activeStories.ChatId, item);
                    _viewModel.BeginOnUIThread(() => UpdateChatOrder(item, order, lastMessage));
                }
            }

            private async void UpdateChatOrder(ActiveStoriesViewModel activeStories, long order, bool lastMessage)
            {
                using (await _loadMoreLock.WaitAsync())
                {
                    if (order > 0 && (order > _lastOrder || !_hasMoreItems || (order == _lastOrder && activeStories.ChatId >= _lastChatId)))
                    {
                        var next = NextIndexOf(activeStories.ChatId, order);
                        if (next >= 0)
                        {
                            if (_chats.ContainsKey(activeStories.ChatId))
                            {
                                Remove(activeStories);
                            }

                            _chats[activeStories.ChatId] = activeStories;
                            Insert(Math.Min(Count, next), activeStories);

                            if (next == Count - 1)
                            {
                                _lastChatId = activeStories.ChatId;
                                _lastOrder = order;
                            }

                            IsEmpty = Count == 0;
                        }
                        else
                        {
                            activeStories.RaisePropertyChanged(nameof(activeStories.Item));
                        }
                    }
                    else if (_chats.ContainsKey(activeStories.ChatId))
                    {
                        _chats.Remove(activeStories.ChatId);
                        Remove(activeStories);

                        IsEmpty = Count == 0;

                        //if (!_hasMoreItems)
                        //{
                        //    await LoadMoreItemsAsync(0);
                        //}
                    }
                }
            }

            private int NextIndexOf(long chatId, long order)
            {
                var prev = -1;
                var next = 0;

                for (int i = 0; i < Count; i++)
                {
                    var item = this[i];
                    if (item.ChatId == chatId)
                    {
                        prev = i;
                        continue;
                    }

                    if (order > item.Order || order == item.Order && chatId >= item.ChatId)
                    {
                        return next == prev ? -1 : next;
                    }

                    next++;
                }

                return Count;
            }

            #endregion

            private bool _isEmpty;
            public bool IsEmpty
            {
                get
                {
                    return _isEmpty;
                }
                set
                {
                    if (_isEmpty != value)
                    {
                        _isEmpty = value;
                        OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsEmpty)));
                    }
                }
            }
        }
    }
}
