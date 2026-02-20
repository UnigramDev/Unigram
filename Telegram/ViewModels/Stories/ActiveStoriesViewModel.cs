//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

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

    public partial class ActiveStoriesViewModel : ComposeViewModel
    {
        private readonly long _chatId;

        private readonly ChatActiveStories _activeStories;
        private readonly Dictionary<int, StoryViewModel> _stories = new();

        private readonly ChatMessageDelegate _messageDelegate;

        private readonly TaskCompletionSource<bool> _task;

        public ChatActiveStories Item => _activeStories;

        public ActiveStoriesViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, ChatActiveStories activeStories, Chat chat)
            : base(clientService, settingsService, aggregator)
        {
            _chatId = activeStories.ChatId;

            _activeStories = activeStories;
            _task = new TaskCompletionSource<bool>();

            Chat = chat;
            IsMyStory = Chat.Type is ChatTypePrivate privata && privata.UserId == clientService.Options.MyId;

            _messageDelegate = new ChatMessageDelegate(this, Chat);

            Items = new ObservableCollection<StoryViewModel>();
            Update(activeStories);
        }

        public ActiveStoriesViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, StoryViewModel selectedItem, ObservableCollection<StoryViewModel> stories)
            : base(clientService, settingsService, aggregator)
        {
            _chatId = selectedItem.PosterChatId;

            Chat = clientService.GetChat(selectedItem.PosterChatId);
            IsMyStory = Chat.Type is ChatTypePrivate privata && privata.UserId == clientService.Options.MyId;

            _messageDelegate = new ChatMessageDelegate(this, Chat);

            Items = stories;
            SelectedItem = selectedItem;
        }

        public ActiveStoriesViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, Story story)
            : base(clientService, settingsService, aggregator)
        {
            _chatId = story.PosterChatId;

            Chat = clientService.GetChat(story.PosterChatId);
            IsMyStory = Chat.Type is ChatTypePrivate privata && privata.UserId == clientService.Options.MyId;

            _messageDelegate = new ChatMessageDelegate(this, Chat);

            var selectedItem = new StoryViewModel(clientService, story);

            Items = new ObservableCollection<StoryViewModel> { selectedItem };
            SelectedItem = selectedItem;
        }

        public Task Wait => _task?.Task ?? Task.CompletedTask;

        public long ChatId => _chatId;

        public override Chat Chat { get; set; }

        public long Order => _activeStories?.Order ?? 0;

        public long MaxReadStoryId => _activeStories?.MaxReadStoryId ?? 0;

        /// <summary>
        /// True, if the stories are shown in the main story list and can be archived; otherwise, the stories can be hidden from the main story list -only by calling removeTopChat with topChatCategoryUsers and the chat_id. Stories of the current user can't be archived nor hidden using removeTopChat
        /// </summary>
        public bool CanBeArchived => _activeStories?.CanBeArchived ?? true;

        public StoryList List => _activeStories?.List;

        public bool IsMyStory { get; }

        public ObservableCollection<StoryViewModel> Items { get; }

        public MessageDelegate Delegate => _messageDelegate;

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

        public async void Update(ChatActiveStories activeStories)
        {
            var next = new List<StoryViewModel>();
            var selected = default(StoryViewModel);

            foreach (var story in activeStories.Stories)
            {
                _stories.TryGetValue(story.StoryId, out var item);
                item ??= new StoryViewModel(ClientService, activeStories.ChatId, story);

                if (story.IsLive)
                {
                    selected = item;
                }
                else if (story.StoryId > activeStories.MaxReadStoryId)
                {
                    selected ??= item;
                }

                next.Add(item);
            }

            _activeStories.List = activeStories.List;
            _activeStories.ChatId = activeStories.ChatId;
            _activeStories.Order = activeStories.Order;
            _activeStories.MaxReadStoryId = activeStories.MaxReadStoryId;
            _activeStories.Stories = activeStories.Stories;

            _stories.Clear();
            Items.Clear();

            foreach (var item in next)
            {
                _stories[item.Id] = item;
                Items.Add(item);
            }

            var selectedItem = selected ?? Items.FirstOrDefault();
            if (selectedItem != null)
            {
                await selectedItem.LoadAsync();
            }

            _task?.TrySetResult(true);
        }

        public override MessageTopic TopicId { get; set; }

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

        protected override InputMessageReplyTo GetReply(bool clear, bool notify = true)
        {
            return new InputMessageReplyToStory(ChatId, SelectedItem.Id);
        }

        public override FormattedText GetFormattedText(bool clear, bool parseMarkdown)
        {
            return new FormattedText(string.Empty, Array.Empty<TextEntity>());
        }

        protected override void SetFormattedText(FormattedText text)
        {
            //throw new NotImplementedException();
        }

        public override Task<MessageSendOptions> PickMessageSendOptionsAsync(int messageCount = 1, SchedulingState schedule = SchedulingState.Auto, bool? silent = null, bool reorder = false)
        {
            return Task.FromResult(new MessageSendOptions(null, silent ?? false, false, 0, Settings.Stickers.DynamicPackOrder && reorder, null, 0, 0, false));
        }

        public void Handle(UpdateStory update)
        {
            if (_stories.TryGetValue(update.Story.Id, out var story))
            {
                story.Update(update.Story);
            }
        }

        public void Handle(UpdateStoryDeleted update)
        {
            if (_stories.Remove(update.StoryId, out var story))
            {
                Items.Remove(story);
            }
        }
    }
}
