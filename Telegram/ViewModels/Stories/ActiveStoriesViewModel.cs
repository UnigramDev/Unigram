//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
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

        private readonly IStoryService _storyService;
        private ChatActiveStories _activeStories;
        private StoryViewModel _startItem;

        private readonly ChatMessageDelegate _messageDelegate;

        private readonly TaskCompletionSource<bool> _task;

        public ChatActiveStories Item => _activeStories;

        /// <summary>
        /// A chat's active stories, kept current by whoever owns this through <see cref="Update"/>.
        /// </summary>
        public ActiveStoriesViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, ChatActiveStories activeStories, Chat chat)
            : this(clientService, settingsService, aggregator, chat, new ObservableCollection<StoryViewModel>())
        {
            _storyService = clientService.Session.Resolve<IStoryService>();
            _activeStories = activeStories;
            _task = new TaskCompletionSource<bool>();

            Update(activeStories);
        }

        /// <summary>
        /// A viewer over another list's stories, such as a profile grid.
        /// </summary>
        public ActiveStoriesViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, StoryViewModel selectedItem, ObservableCollection<StoryViewModel> stories)
            : this(clientService, settingsService, aggregator, clientService.GetChat(selectedItem.PosterChatId), stories)
        {
            SelectedItem = selectedItem;
        }

        /// <summary>
        /// A single story.
        /// </summary>
        public ActiveStoriesViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, Story story)
            : this(clientService, settingsService, aggregator, clientService.GetChat(story.PosterChatId), new ObservableCollection<StoryViewModel>())
        {
            var selectedItem = clientService.Session.Resolve<IStoryService>().GetOrCreate(story);

            Items.Add(selectedItem);
            SelectedItem = selectedItem;
        }

        private ActiveStoriesViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, Chat chat, ObservableCollection<StoryViewModel> items)
            : base(clientService, settingsService, aggregator)
        {
            _chatId = chat.Id;

            Chat = chat;
            IsMyStory = chat.Id == clientService.Options.MyId;

            _messageDelegate = new ChatMessageDelegate(this, chat);

            Items = items;
        }

        public Task Wait => _task?.Task ?? Task.CompletedTask;

        public long ChatId => _chatId;

        public override Chat Chat { get; set; }

        public long Order => _activeStories?.Order ?? 0;

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

        public void Update(ChatActiveStories activeStories)
        {
            _activeStories = activeStories;

            var start = default(StoryViewModel);
            var index = 0;

            foreach (var story in activeStories.Stories)
            {
                var item = _storyService.GetOrCreate(activeStories.ChatId, story);

                if (story.IsLive)
                {
                    start = item;
                }
                else if (story.StoryId > activeStories.MaxReadStoryId)
                {
                    start ??= item;
                }

                // Moved into place rather than cleared and refilled, so that a viewer showing one
                // of them still finds it in Items.
                var position = Items.IndexOf(item);
                if (position == -1)
                {
                    Items.Insert(index, item);
                }
                else if (position != index)
                {
                    Items.Move(position, index);
                }

                index++;
            }

            while (Items.Count > index)
            {
                Items.RemoveAt(Items.Count - 1);
            }

            _startItem = start ?? Items.FirstOrDefault();
            _ = LoadStartItemAsync(_startItem);
        }

        private async Task LoadStartItemAsync(StoryViewModel startItem)
        {
            if (startItem != null)
            {
                await startItem.LoadAsync();
            }

            _task?.TrySetResult(true);
        }

        /// <summary>
        /// Selects the story a viewer opens on: the live one, otherwise the first unread.
        /// </summary>
        public void SelectStartItem()
        {
            SelectedItem = _startItem ?? Items.FirstOrDefault();
        }

        public override MessageTopic TopicId { get; set; }

        public override MessageTopic OutgoingTopicId { get; }

        protected override bool CanSchedule => false;

        public override void ViewSticker(Sticker sticker)
        {
            //throw new NotImplementedException();
        }

        protected override void HideStickers()
        {
            //throw new NotImplementedException();
        }

        protected override ComposerSnapshot PeekComposer()
        {
            // A story reply always answers whatever the carousel is showing; there is no composer
            // header behind it, and so nothing to consume when the send goes through.
            if (SelectedItem is StoryViewModel story)
            {
                return new ComposerSnapshot
                {
                    ReplyTo = new InputMessageReplyToStory(ChatId, story.Id)
                };
            }

            return ComposerSnapshot.None;
        }

        public override FormattedText GetFormattedText(bool clear, bool parseMarkdown)
        {
            return new FormattedText(string.Empty, Array.Empty<TextEntity>());
        }

        protected override void SetFormattedText(FormattedText text)
        {
            //throw new NotImplementedException();
        }

        protected override Task<SendPlan> PrepareSendAsync(ComposerSnapshot composer, int messageCount, SchedulingState schedule, bool? silent, bool reorder, long effectId)
        {
            // Nothing to ask the user: story replies are neither paid nor scheduled.
            return Task.FromResult(new SendPlan
            {
                ReplyTo = composer?.ReplyTo,
                DisableNotification = silent ?? false,
                UpdateOrderOfInstalledStickerSets = AppSettings.Stickers.DynamicPackOrder && reorder,
                EffectId = effectId
            });
        }
    }
}
