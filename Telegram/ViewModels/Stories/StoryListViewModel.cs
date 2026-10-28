//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Stories;
using Telegram.Entities;
using Telegram.Native;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Factories;
using Telegram.Td.Api;
using Telegram.Views;
using Telegram.Views.Popups;
using Telegram.Views.Premium.Popups;
using Telegram.Views.Stories.Popups;
using Windows.Foundation;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace Telegram.ViewModels.Stories
{
    public partial class StoryListViewModel : ViewModelBase
    {
        private readonly IStorageService _storageService;
        private readonly ITranslateService _translateService;
        private readonly IStoryService _storyService;

        public StoryListViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, StoryList storyList)
            : base(clientService, settingsService, aggregator)
        {
            _storageService = Session.Resolve<IStorageService>();
            _translateService = Session.Resolve<ITranslateService>();
            _storyService = Session.Resolve<IStoryService>();

            Items = new ItemsCollection(clientService, aggregator, this, storyList);
        }

        public StoryListViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, params ActiveStoriesViewModel[] items)
            : base(clientService, settingsService, aggregator)
        {
            _storageService = Session.Resolve<IStorageService>();
            _translateService = Session.Resolve<ITranslateService>();
            _storyService = Session.Resolve<IStoryService>();

            Items = new ObservableCollection<ActiveStoriesViewModel>(items);
        }

        public static StoryListViewModel Create(INavigationService navigationService, ActiveStoriesViewModel activeStories)
        {
            return new StoryListViewModel(activeStories.ClientService, activeStories.Settings, activeStories.Aggregator, activeStories)
            {
                NavigationService = navigationService
            };
        }

        public void UpdateSelectedItem()
        {
            foreach (var activeStories in Items)
            {
                activeStories.SelectStartItem();
            }
        }

        public ITranslateService TranslateService => _translateService;

        public override INavigationService NavigationService
        {
            get => base.NavigationService;
            set
            {
                base.NavigationService = value;
                this.Items.ForEach(x => x.NavigationService = value);
            }
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

            if (chat.Type is ChatTypePrivate)
            {
                NavigationService.Navigate(typeof(ProfilePage), chat.Id);
            }
            else
            {
                NavigationService.NavigateToChat(chat);
            }
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
                ShowToast(string.Format(settings.MuteStories ? Strings.NotificationsStoryMutedHint : Strings.NotificationsStoryUnmutedHint, user.FirstName));
            }
            else
            {
                ShowToast(string.Format(settings.MuteStories ? Strings.NotificationsStoryMutedHint : Strings.NotificationsStoryUnmutedHint, activeStories.Chat.Title));
            }
        }

        public void HideProfile(ActiveStoriesViewModel activeStories)
        {
            if (activeStories.CanBeArchived)
            {
                ClientService.Send(new SetChatActiveStoriesList(activeStories.ChatId, new StoryListArchive()));
            }
            else
            {
                ClientService.Send(new RemoveTopChat(new TopChatCategoryUsers(), activeStories.ChatId));
            }

            if (ClientService.TryGetUser(activeStories.Chat, out User user))
            {
                ShowToast(string.Format(Strings.StoriesMovedToContacts, user.FirstName));
            }
            else
            {
                ShowToast(string.Format(Strings.StoriesMovedToContacts, activeStories.Chat.Title));
            }
        }

        public void ShowProfile(ActiveStoriesViewModel activeStories)
        {
            ClientService.Send(new SetChatActiveStoriesList(activeStories.ChatId, new StoryListMain()));

            if (ClientService.TryGetUser(activeStories.Chat, out User user))
            {
                ShowToast(string.Format(Strings.StoriesMovedToDialogs, user.FirstName));
            }
            else
            {
                ShowToast(string.Format(Strings.StoriesMovedToDialogs, activeStories.Chat.Title));
            }
        }

        public Task ShareStoryAsync(StoryViewModel story)
        {
            return ShowPopupAsync(new ChooseChatsPopup(), new ChooseChatsConfigurationShareStory(story.PosterChatId, story.Id), requestedTheme: ElementTheme.Dark);
        }

        public Task TranslateStoryAsync(StoryViewModel story)
        {
            var language = LanguageIdentification.IdentifyLanguage(story.Caption.Text);
            var popup = new TranslatePopup(_translateService, story.Caption, language, Settings.Translate.To, !story.CanBeForwarded)
            {
                RequestedTheme = ElementTheme.Dark
            };

            return ShowPopupAsync(popup);
        }

        public async Task RetryStoryAsync(StoryViewModel story)
        {
            // A post that failed on a limit would only fail again. Any other failure shows on the
            // story itself, with the new error.
            if (await CanPostStoryAsync(story.PosterChatId, ElementTheme.Dark))
            {
                await _storyService.RetryAsync(story);
            }
        }

        // Says why not, where a story cannot be posted to the chat now.
        private async Task<bool> CanPostStoryAsync(long chatId, ElementTheme requestedTheme = ElementTheme.Default)
        {
            var response = await ClientService.SendAsync(new CanPostStory(chatId));
            switch (response)
            {
                case CanPostStoryResultOk:
                    return true;
                case CanPostStoryResultPremiumNeeded:
                    NavigationService.ShowPromo(new PremiumFeatureUpgradedStories());
                    break;
                case CanPostStoryResultActiveStoryLimitExceeded:
                    await ShowPopupAsync(new LimitReachedPopup(NavigationService, ClientService, new PremiumLimitTypeActiveStoryCount()), requestedTheme: requestedTheme);
                    break;
                case CanPostStoryResultWeeklyLimitExceeded:
                    await ShowPopupAsync(new LimitReachedPopup(NavigationService, ClientService, new PremiumLimitTypeWeeklyPostedStoryCount()), requestedTheme: requestedTheme);
                    break;
                case CanPostStoryResultMonthlyLimitExceeded:
                    await ShowPopupAsync(new LimitReachedPopup(NavigationService, ClientService, new PremiumLimitTypeMonthlyPostedStoryCount()), requestedTheme: requestedTheme);
                    break;
                case CanPostStoryResultLiveStoryIsActive:
                    await ShowPopupAsync(Strings.LiveStoryAlreadyStreaming, Strings.LiveStoryAlreadyStreamingTitle, Strings.OK, requestedTheme: requestedTheme);
                    break;
                case Error error:
                    ToastPopup.ShowError(XamlRoot, error);
                    break;

                    // CanPostStoryResultBoostNeeded is for channels, which cannot post stories yet.
            }

            return false;
        }

        public async Task DiscardStoryAsync(StoryViewModel story)
        {
            var response = await _storyService.DiscardAsync(story);
            if (response is Error error)
            {
                // TDLib can only cancel a post while its file is still uploading.
                if (error.Message == "Story upload has been already completed")
                {
                    ShowToast(Strings.StoryCancelTooLate);
                }
                else
                {
                    ShowToast(error);
                }
            }
        }

        public async Task DeleteStoryAsync(StoryViewModel story)
        {
            var message = Strings.DeleteStorySubtitle;
            var title = Strings.DeleteStoryTitle;

            var confirm = await ShowPopupAsync(message, title, Strings.Delete, Strings.Cancel, destructive: true);
            if (confirm == ContentDialogResult.Primary)
            {
                ClientService.Send(new DeleteStory(story.PosterChatId, story.Id));
            }
        }

        public void ArchiveStory(StoryViewModel story)
        {
            ClientService.Send(new ToggleStoryIsPostedToChatPage(story.PosterChatId, story.Id, !story.IsPostedToChatPage));

            ShowToast(story.IsPostedToChatPage ? Strings.StoryRemovedFromProfile : Strings.StorySavedToProfile);
        }

        public async Task ReportStoryAsync(StoryViewModel story)
        {
            await ShowPopupAsync(new ReportStoryPopup(ClientService, NavigationService, story.PosterChatId, story.Id, null, string.Empty)
            {
                RequestedTheme = ElementTheme.Dark
            });
        }

        public async void AddStory()
        {
            try
            {
                // Before the picker, so that nobody edits a story they then cannot post.
                if (!await CanPostStoryAsync(ClientService.Options.MyId))
                {
                    return;
                }

                var picker = new FileOpenPicker();
                picker.ViewMode = PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                picker.FileTypeFilter.AddRange(Constants.MediaTypes);

                var media = await picker.PickSingleMediaAsync(XamlRoot);
                if (media is StoragePhoto or StorageVideo)
                {
                    var editor = new EditStoryPopup(NavigationService, ClientService, media);
                    if (await editor.ShowAsync() != ContentDialogResult.Primary)
                    {
                        return;
                    }

                    var options = editor.PostOptions;
                    var content = await StoryFactory.CreateContentAsync(media);

                    // Posted as the current user through the Saved Messages chat, whose id is the user's.
                    var request = new PostStory(ClientService.Options.MyId, content, StoryFactory.CreateAreas(media), options.Caption, options.PrivacySettings, Vector<int>.Empty, options.ActivePeriod, null, options.IsPostedToChatPage, options.ProtectContent);

                    var response = await ClientService.SendAsync(request);
                    if (response is Story story)
                    {
                        _storyService.TrackPost(request, story);
                    }
                    else if (response is Error error)
                    {
                        ToastPopup.ShowError(XamlRoot, error);
                    }
                }
                else if (media is StorageInvalid)
                {
                    await ShowPopupAsync(Strings.OpenImageUnsupported, Strings.AppName, Strings.OK);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        public async void SaveStory(StoryViewModel story)
        {
            var file = story.GetFile();
            if (file != null)
            {
                await _storageService.SaveFileAsAsync(XamlRoot, file);
            }
        }

        protected ActiveStoriesViewModel Create(ChatActiveStories activeStories, Chat chat)
        {
            return new ActiveStoriesViewModel(ClientService, Settings, Aggregator, activeStories, chat)
            {
                NavigationService = NavigationService
            };
        }

        public void OpenStory(ActiveStoriesViewModel activeStories, Rect origin, Func<ActiveStoriesViewModel, Rect> closing)
        {
            // Your own cell can be in the strip with no stories, to add one from.
            var viewModel = new StoryListViewModel(ClientService, Settings, Aggregator, Items.Where(x => x.Items.Count > 0).ToArray());
            viewModel.NavigationService = NavigationService;
            viewModel.UpdateSelectedItem();

            var window = new StoriesWindow(NavigationService.XamlRoot);
            window.Update(viewModel, activeStories, StoryOpenOrigin.ProfilePhoto, origin, closing);
            _ = window.ShowAsync();
        }

        public void SetList(StoryList storyList)
        {
            if (Items is ItemsCollection collection && !collection.StoryList.AreTheSame(storyList))
            {
                _ = collection.ReloadAsync(storyList);
            }
        }

        public partial class ItemsCollection : ObservableCollection<ActiveStoriesViewModel>, ISupportIncrementalLoading
        {
            private readonly IClientService _clientService;
            private readonly IEventAggregator _aggregator;

            private CancellationTokenSource _token = new();
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

                _ = LoadMoreItemsAsync(0);
            }

            public Task ReloadAsync(StoryList storyList)
            {
                _token?.Cancel();
                _token = new CancellationTokenSource();

                _aggregator.Unsubscribe(this);
                _hasMoreItems = false;

                _lastChatId = 0;
                _lastOrder = 0;

                _storyList = storyList;

                _chats.Clear();
                Clear();

                return LoadMoreItemsAsync();
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return IncrementalLoading.Run(token => LoadMoreItemsAsync());
            }

            private async Task<LoadMoreItemsResult> LoadMoreItemsAsync()
            {
                var token = _token;
                var totalCount = 0u;

                // Your own cell needs the Saved Messages chat, which nothing may have loaded yet.
                if (_storyList is StoryListMain && !_clientService.TryGetChat(_clientService.Options.MyId, out Chat _))
                {
                    await _clientService.SendAsync(new CreatePrivateChat(_clientService.Options.MyId, true));
                }

                var response = await _clientService.GetStoryListAsync(_storyList, Count, 20);
                if (response is Telegram.Td.Api.Chats chats && !token.IsCancellationRequested)
                {
                    foreach (var activeStories in _clientService.GetActiveStorieses(chats.ChatIds))
                    {
                        var order = activeStories.Order;
                        if (order != 0)
                        {
                            // TODO: is this redundant?
                            var next = NextIndexOf(activeStories.ChatId, order);
                            if (next >= 0 && _clientService.TryGetChat(activeStories.ChatId, out Chat chat))
                            {
                                if (_chats.TryGetValue(activeStories.ChatId, out var prev))
                                {
                                    Remove(prev);
                                }

                                var item = _viewModel.Create(activeStories, chat);

                                _chats[activeStories.ChatId] = item;
                                Insert(Math.Min(Count, next), item);

                                totalCount++;
                            }

                            _lastChatId = activeStories.ChatId;
                            _lastOrder = order;
                        }
                    }

                    UpdateMyStory();

                    IsEmpty = Count == 0;

                    _hasMoreItems = chats.TotalCount >= 0;
                    Subscribe();

                    if (_hasMoreItems == false)
                    {
                        OnPropertyChanged(new PropertyChangedEventArgs("HasMoreItems"));
                    }
                }

                return new LoadMoreItemsResult
                {
                    Count = totalCount
                };
            }

            private void Subscribe()
            {
                // Stories themselves are kept current by IStoryService, and TDLib follows a
                // deletion with the chat's new active stories.
                _aggregator.Subscribe<UpdateChatActiveStories>(this, Handle)
                    .Subscribe<UpdateFailedStories>(Handle);
            }

            public bool HasMoreItems => _hasMoreItems;

            #region Handle

            public void Handle(UpdateChatActiveStories update)
            {
                _viewModel.BeginOnUIThread(() => UpdateChatOrder(update.ActiveStories));
            }

            public void Handle(UpdateFailedStories update)
            {
                if (_clientService.TryGetActiveStories(update.ChatId, out ChatActiveStories activeStories))
                {
                    _viewModel.BeginOnUIThread(() => UpdateChatOrder(activeStories));
                }
            }

            private void UpdateChatOrder(ChatActiveStories activeStories)
            {
                var mine = activeStories.ChatId == _clientService.Options.MyId;
                var inList = (activeStories.List is StoryListMain && _storyList is StoryListMain) || (activeStories.List is StoryListArchive && _storyList is StoryListArchive);

                if (_chats.TryGetValue(activeStories.ChatId, out var item))
                {
                    item.Update(activeStories);
                    UpdateChatOrder(item, inList ? GetOrder(item) : 0, true);
                }
                else if (inList && (activeStories.Order != 0 || mine) && _clientService.TryGetChat(activeStories.ChatId, out Chat chat))
                {
                    item = _viewModel.Create(activeStories, chat);
                    UpdateChatOrder(item, GetOrder(item), true);
                }

                if (!mine)
                {
                    UpdateMyStory();
                }
            }

            // The item's order, not TDLib's. Your own cell leads the main list while it has stories
            // of its own, including any still posting or failed, which TDLib gives no order, or
            // while anyone else's are in the list, to add a story from.
            private long GetOrder(ActiveStoriesViewModel item)
            {
                if (item.IsMyStory)
                {
                    var others = _chats.ContainsKey(item.ChatId) ? Count - 1 : Count;
                    return item.Items.Count > 0 || others > 0 ? long.MaxValue : 0;
                }

                return item.Order;
            }

            // Adds your own cell when the first of anyone else's stories arrives, and takes it away
            // again with the last, if it has none of its own.
            private void UpdateMyStory()
            {
                if (_storyList is not StoryListMain)
                {
                    return;
                }

                var myId = _clientService.Options.MyId;

                if (_chats.TryGetValue(myId, out var item))
                {
                    if (GetOrder(item) == 0)
                    {
                        UpdateChatOrder(item, 0, true);
                    }
                }
                else if (Count > 0 && _clientService.TryGetChat(myId, out Chat chat))
                {
                    if (!_clientService.TryGetActiveStories(myId, out ChatActiveStories activeStories))
                    {
                        activeStories = new ChatActiveStories(myId, new StoryListMain(), 0, false, 0, Vector<StoryInfo>.Empty);
                    }

                    item = _viewModel.Create(activeStories, chat);
                    UpdateChatOrder(item, GetOrder(item), true);
                }
            }

            private void UpdateChatOrder(ActiveStoriesViewModel activeStories, long order, bool lastMessage)
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
