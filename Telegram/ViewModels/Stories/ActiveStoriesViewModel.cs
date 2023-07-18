using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Data;

namespace Telegram.ViewModels.Stories
{
    public enum ActiveStoriesState
    {
        Unread,
        CloseFriends,
        Read
    }

    public class ActiveStoriesViewModel : ComposeViewModel
    {
        private readonly IClientService _clientService;
        private readonly long _chatId;

        private readonly ChatActiveStories _activeStories;
        private readonly HashSet<int> _knownStories = new();

        private readonly TaskCompletionSource<bool> _task;

        public ChatActiveStories Item => _activeStories;

        public ActiveStoriesViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, ChatActiveStories activeStories)
            : base(clientService, settingsService, aggregator)
        {
            _clientService = clientService;
            _chatId = activeStories.ChatId;

            _activeStories = activeStories;
            _task = new TaskCompletionSource<bool>();

            Chat = clientService.GetChat(activeStories.ChatId);
            IsMyStory = Chat.Type is ChatTypePrivate privata && privata.UserId == clientService.Options.MyId;

            Items = new ObservableCollection<StoryViewModel>();
            Initialize(activeStories);
        }

        public ActiveStoriesViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, StoryViewModel selectedItem, ObservableCollection<StoryViewModel> stories)
            : base(clientService, settingsService, aggregator)
        {
            _clientService = clientService;
            _chatId = selectedItem.ChatId;

            Chat = clientService.GetChat(selectedItem.ChatId);
            IsMyStory = Chat.Type is ChatTypePrivate privata && privata.UserId == clientService.Options.MyId;

            Items = stories;
            SelectedItem = selectedItem;
        }

        public ActiveStoriesViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, StoryViewModel selectedItem)
            : base(clientService, settingsService, aggregator)
        {
            _clientService = clientService;
            _chatId = selectedItem.ChatId;

            Chat = clientService.GetChat(selectedItem.ChatId);
            IsMyStory = Chat.Type is ChatTypePrivate privata && privata.UserId == clientService.Options.MyId;

            Items = new ObservableCollection<StoryViewModel> { selectedItem };
            SelectedItem = selectedItem;
        }

        public Task Wait => _task?.Task ?? Task.CompletedTask;

        public long ChatId => _chatId;

        public override Chat Chat { get; set; }

        public long Order => _activeStories?.Order ?? 0;

        public StoryList List => _activeStories?.List;

        public bool IsMyStory { get; }

        public ObservableCollection<StoryViewModel> Items { get; }

        private StoryViewModel _selectedItem;
        public StoryViewModel SelectedItem
        {
            get => _selectedItem;
            set => SetSelectedItem(value);
        }

        private async void SetSelectedItem(StoryViewModel story)
        {
            Set(ref _selectedItem, story, nameof(SelectedItem));

            if (Items.Count > 0 && story == Items[^1] && Items is ISupportIncrementalLoading incremental && incremental.HasMoreItems)
            {
                await incremental.LoadMoreItemsAsync(50);
            }
        }

        public void Update(ChatActiveStories activeStories)
        {
            var prev = Items.ToDictionary(x => x.StoryId);
            var next = new List<StoryViewModel>();

            foreach (var story in activeStories.Stories)
            {
                if (prev.TryGetValue(story.StoryId, out var item))
                {
                    next.Add(item);
                }
                else
                {
                    next.Add(new StoryViewModel(_clientService, activeStories.ChatId, story));
                }
            }

            _activeStories.List = activeStories.List;
            _activeStories.ChatId = activeStories.ChatId;
            _activeStories.Order = activeStories.Order;
            _activeStories.MaxReadStoryId = activeStories.MaxReadStoryId;
            _activeStories.Stories = activeStories.Stories;
        }

        private async void Initialize(ChatActiveStories activeStories)
        {
            foreach (var story in activeStories.Stories)
            {
                var item = new StoryViewModel(_clientService, _chatId, story);

                if (story.StoryId > activeStories.MaxReadStoryId)
                {
                    SelectedItem ??= item;
                }

                Items.Add(item);
            }

            SelectedItem ??= Items.LastOrDefault();

            if (SelectedItem != null)
            {
                await SelectedItem.LoadAsync();
            }

            _task?.TrySetResult(true);
        }

        public override long ThreadId => 0;

        protected override bool CanSchedule => false;

        public override void ViewSticker(Sticker sticker)
        {
            //throw new NotImplementedException();
        }

        protected override void HideStickers()
        {
            //throw new NotImplementedException();
        }

        protected override MessageReplyTo GetReply(bool clear, bool notify = true)
        {
            return new MessageReplyToStory(ChatId, SelectedItem.StoryId);
        }

        public override FormattedText GetFormattedText(bool clear)
        {
            return new FormattedText(string.Empty, new TextEntity[0]);
        }

        protected override void SetFormattedText(FormattedText text)
        {
            //throw new NotImplementedException();
        }

        public override Task<MessageSendOptions> PickMessageSendOptionsAsync(bool? schedule = null, bool? silent = null, bool reorder = false)
        {
            return Task.FromResult(new MessageSendOptions(silent ?? false, false, false, reorder, null, 0));
        }
    }
}
