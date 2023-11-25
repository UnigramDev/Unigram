//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Chats;
using Telegram.Controls.Gallery;
using Telegram.Controls.Messages;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Chats;
using Telegram.ViewModels.Gallery;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Point = Windows.Foundation.Point;

namespace Telegram.Views
{
    public partial class ChatView
    {
        private readonly DispatcherTimer _debouncer;

        private void OnViewSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (Messages.ScrollingHost.ScrollableHeight > 0)
            {
                return;
            }

            Arrows.IsVisible = false;
            ViewVisibleMessages(false);
        }

        private void OnViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (Messages.ScrollingHost.ScrollableHeight - Messages.ScrollingHost.VerticalOffset < 40 && ViewModel.IsFirstSliceLoaded != false)
            {
                Arrows.IsVisible = false;
            }
            else if (ViewModel.Type is DialogType.History or DialogType.Thread)
            {
                Arrows.IsVisible = true;
            }

            ViewVisibleMessages(false);
        }

        private void UnloadVisibleMessages()
        {
            _prev.Clear();
        }

        public void ViewVisibleMessages()
        {
            _debouncer.Stop();
            _debouncer.Start();
        }

        public void ViewVisibleMessages(bool intermediate)
        {
            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            var panel = Messages.ItemsPanelRoot as ItemsStackPanel;
            if (panel == null || panel.FirstVisibleIndex < 0 || panel.LastVisibleIndex >= _messages.Count)
            {
                return;
            }

            var firstVisibleId = 0L;
            var lastVisibleId = 0L;

            var minItem = true;
            var minDate = true;
            var minDateIndex = panel.FirstVisibleIndex;
            var minDateValue = 0L;

            var messages = new List<long>(panel.LastVisibleIndex - panel.FirstVisibleIndex);
            var animations = new List<(SelectorItem, MessageViewModel)>(panel.LastVisibleIndex - panel.FirstVisibleIndex);

            for (int i = panel.FirstVisibleIndex; i <= panel.LastVisibleIndex; i++)
            {
                // TODO: this would be preferable, but it can't be done because
                // date service messages aren't mapped in the array
                //var message = _messages[i];
                //_messageIdToSelector.TryGetValue(message.Id, out SelectorItem container);

                //if (container == null)
                //{
                //    continue;
                //}

                var container = Messages.ContainerFromIndex(i) as SelectorItem;
                if (container == null)
                {
                    continue;
                }

                var message = Messages.ItemFromContainer(container) as MessageViewModel;
                if (message == null)
                {
                    continue;
                }

                if (firstVisibleId == 0)
                {
                    firstVisibleId = message.Id;
                }
                if (message.Id != 0)
                {
                    lastVisibleId = message.Id;
                }

                if (minItem && i >= panel.FirstVisibleIndex)
                {
                    var transform = container.TransformToVisual(DateHeaderRelative);
                    var point = transform.TransformPoint(new Point());

                    if (point.Y + container.ActualHeight >= 0)
                    {
                        minItem = false;
                        minDateValue = Math.Max(message.Id, message.Date);

                        if (message.SchedulingState is MessageSchedulingStateSendAtDate sendAtDate)
                        {
                            UpdateDateHeader(sendAtDate.SendDate, true);
                        }
                        else if (message.SchedulingState is MessageSchedulingStateSendWhenOnline)
                        {
                            UpdateDateHeader(0, true);
                        }
                        else if (message.Date > 0)
                        {
                            UpdateDateHeader(message.Date, false);
                        }
                    }
                }

                if (message.Content is MessageHeaderDate && minDate && i >= panel.FirstVisibleIndex)
                {
                    var transform = container.TransformToVisual(DateHeaderRelative);
                    var point = transform.TransformPoint(new Point());
                    var height = DateHeader.ActualSize.Y;
                    var offset = (float)point.Y + height;

                    minDate = false;

                    if (/*offset >= 0 &&*/ offset < height)
                    {
                        container.Opacity = 0;
                        minDateIndex = int.MaxValue; // Force show
                    }
                    else
                    {
                        container.Opacity = 1;
                        minDateIndex = i;
                    }

                    if (offset >= height && offset < height * 2)
                    {
                        _dateHeader.Offset = new Vector3(0, -height * 2 + offset, 0);
                    }
                    else
                    {
                        _dateHeader.Offset = Vector3.Zero;
                    }
                }
                else
                {
                    container.Opacity = 1;
                }

                // Read and play messages logic:
                if (message.Id == 0)
                {
                    continue;
                }

                if (message.ContainsUnreadMention)
                {
                    ViewModel.Mentions.SetLastViewedMessage(message.Id);
                }

                if (message.UnreadReactions?.Count > 0)
                {
                    ViewModel.Reactions.SetLastViewedMessage(message.Id);

                    var root = container.ContentTemplateRoot as FrameworkElement;
                    if (root is MessageSelector selector && selector.Content is MessageBubble bubble)
                    {
                        bubble.UpdateMessageReactions(message, true);
                    }
                }

                if (message.Content is MessageAlbum album)
                {
                    messages.AddRange(album.Messages.Keys);
                }
                else
                {
                    messages.Add(message.Id);
                    animations.Add((container, message));
                }

                while (ViewModel.RepliesStack.TryPeek(out long reply) && reply == message.Id)
                {
                    ViewModel.RepliesStack.Pop();
                }
            }

            if (minDate)
            {
                _dateHeader.Offset = Vector3.Zero;
            }

            _dateHeaderTimer.Stop();
            _dateHeaderTimer.Start();
            ShowHideDateHeader(minDateValue > 0 && minDateIndex > 0, minDateValue > 0 && minDateIndex is > 0 and < int.MaxValue);

            // Read and play messages logic:
            if (messages.Count > 0 && WindowContext.Current.ActivationMode == CoreWindowActivationMode.ActivatedInForeground)
            {
                MessageSource source = ViewModel.Type switch
                {
                    DialogType.EventLog => new MessageSourceChatEventLog(),
                    DialogType.Thread => ViewModel.Topic != null
                        ? new MessageSourceForumTopicHistory()
                        : new MessageSourceMessageThreadHistory(),
                    _ => new MessageSourceChatHistory()
                };

                ViewModel.ClientService.Send(new ViewMessages(chat.Id, messages, source, false));
            }

            if (animations.Count > 0 && !intermediate)
            {
                Play(animations);
            }

            // Pinned banner
            if (firstVisibleId == 0 || lastVisibleId == 0)
            {
                return;
            }

            if (ViewModel.LockedPinnedMessageId < firstVisibleId)
            {
                ViewModel.LockedPinnedMessageId = 0;
            }

            var thread = ViewModel.Thread;
            if (thread != null)
            {
                var message = thread.Messages.LastOrDefault();
                if (message == null || (firstVisibleId <= message.Id && lastVisibleId >= message.Id) || Messages.ScrollingHost.ScrollableHeight == 0)
                {
                    PinnedMessage.UpdateMessage(ViewModel.Chat, null, false, 0, 1, false);
                }
                else
                {
                    PinnedMessage.UpdateMessage(ViewModel.Chat, ViewModel.CreateMessage(message), false, 0, 1, false);
                }
            }
            else if (ViewModel.PinnedMessages.Count > 0)
            {
                var currentPinned = ViewModel.LockedPinnedMessageId != 0
                    ? ViewModel.PinnedMessages.LastOrDefault(x => x.Id < firstVisibleId) ?? ViewModel.PinnedMessages.LastOrDefault()
                    : ViewModel.PinnedMessages.LastOrDefault(x => x.Id <= lastVisibleId) ?? ViewModel.PinnedMessages.FirstOrDefault();
                if (currentPinned != null)
                {
                    //PinnedMessage.UpdateIndex(ViewModel.PinnedMessages.IndexOf(currentPinned), ViewModel.PinnedMessages.Count, intermediate);
                    PinnedMessage.UpdateMessage(ViewModel.Chat, currentPinned, false,
                        ViewModel.PinnedMessages.IndexOf(currentPinned), ViewModel.PinnedMessages.Count, intermediate);
                }
                else
                {
                    PinnedMessage.UpdateMessage(ViewModel.Chat, null, false, 0, 1, false);
                }
            }
        }

        private bool _dateHeaderCollapsed = true;

        private void ShowHideDateHeader(bool show, bool animate)
        {
            if (_dateHeaderCollapsed != show)
            {
                return;
            }

            _dateHeaderCollapsed = !show;
            DateHeaderPanel.Visibility = show || animate ? Visibility.Visible : Visibility.Collapsed;

            if (!animate)
            {
                _dateHeaderPanel.Opacity = show ? 1 : 0;
                return;
            }

            var batch = _dateHeaderPanel.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                if (show)
                {
                    _dateHeaderCollapsed = false;
                }
                else
                {
                    DateHeaderPanel.Visibility = Visibility.Collapsed;
                }
            };

            var opacity = _dateHeaderPanel.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, show ? 0 : 1);
            opacity.InsertKeyFrame(1, show ? 1 : 0);

            _dateHeaderPanel.StartAnimation("Opacity", opacity);

            batch.End();
        }

        private int _dateHeaderDate;
        private bool _dateHeaderScheduled;

        private void UpdateDateHeader(int date, bool scheduled)
        {
            // TODO: this makes little sense since date will be always
            // different, until time is removed from it.
            if (_dateHeaderDate == date && _dateHeaderScheduled == scheduled)
            {
                return;
            }

            _dateHeaderDate = date;
            _dateHeaderScheduled = scheduled;

            if (scheduled)
            {
                if (date != 0)
                {
                    DateHeader.Tag = null;
                    DateHeaderLabel.Text = string.Format(Strings.MessageScheduledOn, Formatter.DayGrouping(Formatter.ToLocalTime(date)));
                }
                else
                {
                    DateHeader.Tag = null;
                    DateHeaderLabel.Text = Strings.MessageScheduledUntilOnline;
                }
            }
            else
            {
                DateHeader.Tag = date;
                DateHeaderLabel.Text = Formatter.DayGrouping(Formatter.ToLocalTime(date));
            }
        }

        private readonly Dictionary<long, WeakReference> _prev = new Dictionary<long, WeakReference>();

        public async void PlayMessage(MessageViewModel message, FrameworkElement target)
        {
            var text = message.Content as MessageText;

            if (PowerSavingPolicy.AutoPlayAnimations && (message.Content is MessageAnimation || (text?.WebPage != null && text.WebPage.Animation != null) || (message.Content is MessageGame game && game.Game.Animation != null)))
            {
                if (_prev.TryGetValue(message.AnimationHash(), out WeakReference reference) && reference.Target is IPlayerView item)
                {
                    GalleryViewModelBase viewModel;
                    if (message.Content is MessageAnimation)
                    {
                        viewModel = new ChatGalleryViewModel(ViewModel.ClientService, ViewModel.StorageService, ViewModel.Aggregator, message.ChatId, ViewModel.ThreadId, message.Get());
                    }
                    else
                    {
                        viewModel = new SingleGalleryViewModel(ViewModel.ClientService, ViewModel.StorageService, ViewModel.Aggregator, new GalleryMessage(ViewModel.ClientService, message.Get()));
                    }

                    viewModel.NavigationService = ViewModel.NavigationService;
                    await GalleryWindow.ShowAsync(viewModel, () => target);
                }
                else
                {
                    ViewVisibleMessages();
                }
            }
            else
            {
                if (_prev.ContainsKey(message.AnimationHash()))
                {
                    Play(new (SelectorItem, MessageViewModel)[0]);
                }
                else
                {
                    if (_messageIdToSelector.TryGetValue(message.Id, out SelectorItem container))
                    {
                        Play(new (SelectorItem, MessageViewModel)[] { (container, message) });
                    }
                }
            }
        }

        public void Play(IEnumerable<(SelectorItem Container, MessageViewModel Message)> items)
        {
            Dictionary<long, IPlayerView> next = null;
            HashSet<long> prev = null;

            foreach (var pair in items)
            {
                var message = pair.Message;
                var container = pair.Container;

                if (message.Content is MessageDice dice)
                {
                    if (message.GeneratedContentUnread)
                    {
                        message.GeneratedContentUnread = dice.IsInitialState();
                    }
                    else
                    {
                        // We don't want to start already played dices
                        // but we don't even want to stop them if they're already playing.
                        prev ??= new HashSet<long>();
                        prev.Add(message.AnimationHash());
                        continue;
                    }
                }

                if (message.IsAnimatedContentDownloadCompleted())
                {
                    var root = container.ContentTemplateRoot as FrameworkElement;
                    if (root is not MessageSelector selector || selector.Content is not MessageBubble bubble)
                    {
                        continue;
                    }

                    var player = bubble.GetPlaybackElement();
                    if (player != null)
                    {
                        next ??= new Dictionary<long, IPlayerView>();
                        next[message.AnimationHash()] = player;
                    }
                }
            }

            var skip = next != null && prev != null
                ? next.Keys.Union(prev)
                : next != null ? next.Keys
                : prev;

            if (skip != null)
            {
                foreach (var item in _prev.Keys.Except(skip).ToList())
                {
                    var presenter = _prev[item].Target as IPlayerView;
                    if (presenter != null && presenter.LoopCount == 0)
                    {
                        presenter.Pause();
                    }

                    _prev.Remove(item);
                }
            }

            if (next != null)
            {
                foreach (var item in next)
                {
                    _prev[item.Key] = new WeakReference(item.Value);
                    item.Value.Play();
                }
            }
        }











        private readonly Dictionary<long, SelectorItem> _albumIdToSelector = new();
        private readonly Dictionary<long, SelectorItem> _messageIdToSelector = new();
        private readonly MultiValueDictionary<long, long> _messageIdToMessageIds = new();

        private readonly Dictionary<string, DataTemplate> _typeToTemplateMapping = new Dictionary<string, DataTemplate>();
        private readonly Dictionary<string, HashSet<SelectorItem>> _typeToItemHashSetMapping = new Dictionary<string, HashSet<SelectorItem>>();

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            var typeName = SelectTemplateCore(args.Item);
            var relevantHashSet = _typeToItemHashSetMapping[typeName];

            // args.ItemContainer is used to indicate whether the ListView is proposing an
            // ItemContainer (ListViewItem) to use. If args.Itemcontainer != null, then there was a
            // recycled ItemContainer available to be reused.
            if (args.ItemContainer is ChatHistoryViewItem selector)
            {
                if (selector.TypeName.Equals(typeName))
                {
                    // Suggestion matches what we want, so remove it from the recycle queue
                    relevantHashSet.Remove(args.ItemContainer);
                }
                else
                {
                    // The ItemContainer's datatemplate does not match the needed
                    // datatemplate.
                    // Don't remove it from the recycle queue, since XAML will resuggest it later
                    args.ItemContainer = null;
                }
            }

            // If there was no suggested container or XAML's suggestion was a miss, pick one up from the recycle queue
            // or create a new one
            if (args.ItemContainer == null)
            {
                // See if we can fetch from the correct list.
                if (relevantHashSet.Count > 0)
                {
                    // Unfortunately have to resort to LINQ here. There's no efficient way of getting an arbitrary
                    // item from a hashset without knowing the item. Queue isn't usable for this scenario
                    // because you can't remove a specific element (which is needed in the block above).
                    args.ItemContainer = relevantHashSet.First();
                    relevantHashSet.Remove(args.ItemContainer);
                }
                else
                {
                    // There aren't any (recycled) ItemContainers available. So a new one
                    // needs to be created.
                    var item = CreateSelectorItem(typeName);
                    item.Style = Messages.ItemContainerStyleSelector.SelectStyle(args.Item, item);
                    args.ItemContainer = item;
                }
            }

            // Indicate to XAML that we picked a container for it
            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.Item is not MessageViewModel message || args.ItemContainer is not ChatHistoryViewItem container)
            {
                return;
            }

            UpdateCache(message, args.ItemContainer, args.InRecycleQueue);

            if (args.InRecycleQueue)
            {
                // XAML has indicated that the item is no longer being shown, so add it to the recycle queue
                _typeToItemHashSetMapping[container.TypeName].Add(args.ItemContainer);

                if (args.ItemContainer.ContentTemplateRoot is MessageSelector selector)
                {
                    selector.Recycle();
                }

                if (_sizeChangedHandler != null)
                {
                    args.ItemContainer.SizeChanged -= _sizeChangedHandler;
                }

                return;
            }
            else
            {
                var content = args.ItemContainer.ContentTemplateRoot as FrameworkElement;
                if (content == null)
                {
                    return;
                }

                _updateThemeTask?.TrySetResult(true);

                // TODO: are there chances that at this point TextArea is not up to date yet?
                container.PrepareForItemOverride(message,
                    _viewModel.Type is DialogType.History or DialogType.Thread or DialogType.ScheduledMessages
                    && TextArea.Visibility == Visibility.Visible);

                if (content is MessageSelector checkbox)
                {
                    checkbox.UpdateMessage(message, Messages);
                    checkbox.UpdateSelectionEnabled(ViewModel.IsSelectionEnabled, false);

                    content = checkbox.Content as FrameworkElement;
                }

                if (content is MessageBubble bubble)
                {
                    bubble.UpdateQuery(ViewModel.Search?.Query);
                    bubble.UpdateMessage(args.Item as MessageViewModel);

                    args.RegisterUpdateCallback(2, RegisterEvents);
                    args.Handled = true;
                }
                else if (content is MessageService service)
                {
                    service.UpdateMessage(args.Item as MessageViewModel);
                    args.Handled = true;
                }
            }
        }

        private void RegisterEvents(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            args.ItemContainer.SizeChanged += _sizeChangedHandler ??= new SizeChangedEventHandler(Item_SizeChanged);

            if (args.ItemContainer.ContentTemplateRoot is MessageSelector selector && selector.Content is MessageBubble bubble)
            {
                bubble.RegisterEvents();
            }
        }

        private TypedEventHandler<UIElement, ContextRequestedEventArgs> _contextRequestedHandler;
        private SizeChangedEventHandler _sizeChangedHandler;

        private SelectorItem CreateSelectorItem(string typeName)
        {
            SelectorItem item = new ChatHistoryViewItem(Messages, typeName);
            item.ContentTemplate = _typeToTemplateMapping[typeName];
            item.AddHandler(ContextRequestedEvent, _contextRequestedHandler ??= new TypedEventHandler<UIElement, ContextRequestedEventArgs>(Message_ContextRequested), true);

            return item;
        }

        private void Item_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var next = e.NewSize.ToVector2();
            var prev = e.PreviousSize.ToVector2();

            var diff = next.Y - prev.Y;

            var panel = Messages.ItemsPanelRoot as ItemsStackPanel;
            if (panel == null || prev.Y == next.Y || Math.Abs(diff) <= 2)
            {
                return;
            }

            var index = Messages.IndexFromContainer(sender as SelectorItem);
            if (index < panel.LastVisibleIndex && e.PreviousSize.Width < 1 && e.PreviousSize.Height < 1)
            {
                return;
            }

            var message = Messages.ItemFromContainer(sender as SelectorItem) as MessageViewModel;
            if (message == null || message.IsInitial)
            {
                if (message != null && e.PreviousSize.Width > 0 && e.PreviousSize.Height > 0)
                {
                    message.IsInitial = false;
                }
                else
                {
                    return;
                }
            }

            if (index >= panel.FirstVisibleIndex && index <= panel.LastVisibleIndex && sender is SelectorItem selector)
            {
                var direction = panel.ItemsUpdatingScrollMode == ItemsUpdatingScrollMode.KeepItemsInView ? -1 : 1;
                var edge = (index == panel.LastVisibleIndex && direction == 1) || index == panel.FirstVisibleIndex && direction == -1;

                if (edge && !Messages.VisualContains(selector))
                {
                    direction *= -1;
                }

                var first = direction == 1 ? panel.FirstCacheIndex : index + 1;
                var last = direction == 1 ? index : panel.LastCacheIndex;

                var batch = Window.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
                var anim = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
                anim.InsertKeyFrame(0, diff * direction);
                anim.InsertKeyFrame(1, 0);
                //anim.Duration = TimeSpan.FromSeconds(5);

                for (int i = first; i <= last; i++)
                {
                    var container = Messages.ContainerFromIndex(i) as SelectorItem;
                    if (container == null)
                    {
                        continue;
                    }

                    var child = VisualTreeHelper.GetChild(container, 0) as UIElement;
                    if (child != null)
                    {
                        var visual = ElementCompositionPreview.GetElementVisual(child);
                        visual.StartAnimation("Offset.Y", anim);
                    }
                }

                batch.End();
            }
        }

        private string SelectTemplateCore(object item)
        {
            var message = item as MessageViewModel;
            if (message == null)
            {
                return "EmptyMessageTemplate";
            }

            if (message.IsService)
            {
                if (message.Content is MessagePremiumGiftCode)
                {
                    return "ServiceMessageGiftTemplate";
                }
                else if (message.Content is MessageChatChangePhoto or MessageSuggestProfilePhoto or MessageAsyncStory)
                {
                    return "ServiceMessagePhotoTemplate";
                }
                else if (message.Content is MessageChatSetBackground setBackground && setBackground.OldBackgroundMessageId == 0)
                {
                    return "ServiceMessageBackgroundTemplate";
                }
                else if (message.Content is MessageHeaderUnread)
                {
                    return "ServiceMessageUnreadTemplate";
                }

                return "ServiceMessageTemplate";
            }

            if (message.IsChannelPost || message.IsSaved)
            {
                return "FriendMessageTemplate";
            }
            else if (message.IsOutgoing)
            {
                return "UserMessageTemplate";
            }

            return "FriendMessageTemplate";
        }

        public bool HasContainerForItem(long id)
        {
            return _messageIdToSelector.ContainsKey(id);
        }

        public SelectorItem ContainerFromItem(long id)
        {
            if (_messageIdToSelector.TryGetValue(id, out var container))
            {
                return container;
            }

            return null;
        }

        public void UpdateContainerWithMessageId(long id, Action<SelectorItem> action)
        {
            if (_messageIdToSelector.TryGetValue(id, out var container))
            {
                action(container);
            }
        }

        public void UpdateBubbleWithMessageId(long id, Action<MessageBubble> action)
        {
            if (_messageIdToSelector.TryGetValue(id, out var container))
            {
                if (container.ContentTemplateRoot is MessageSelector selector && selector.Content is MessageBubble bubble)
                {
                    action(bubble);
                }
            }
        }

        public void UpdateBubbleWithMediaAlbumId(long id, Action<MessageBubble> action)
        {
            if (_albumIdToSelector.TryGetValue(id, out var container))
            {
                if (container.ContentTemplateRoot is MessageSelector selector && selector.Content is MessageBubble bubble)
                {
                    action(bubble);
                }
            }
        }

        public void UpdateBubbleWithReplyToMessageId(long id, Action<MessageBubble, MessageViewModel> action)
        {
            if (_messageIdToMessageIds.TryGetValue(id, out var ids))
            {
                foreach (var messageId in ids)
                {
                    if (_viewModel.Items.TryGetValue(messageId, out MessageViewModel message))
                    {
                        if (message.ReplyToItem is MessageViewModel && _messageIdToSelector.TryGetValue(messageId, out var container))
                        {
                            if (container.ContentTemplateRoot is MessageSelector selector && selector.Content is MessageBubble bubble)
                            {
                                action(bubble, message);
                            }
                        }
                    }
                }
            }
        }

        public void ForEach(Action<MessageBubble, MessageViewModel> action)
        {
            foreach (var item in _messageIdToSelector)
            {
                if (_viewModel.Items.TryGetValue(item.Key, out MessageViewModel message))
                {
                    if (item.Value.ContentTemplateRoot is MessageSelector selector && selector.Content is MessageBubble bubble)
                    {
                        action(bubble, message);
                    }
                }
            }
        }

        public void ForEach(Action<MessageBubble> action)
        {
            foreach (var item in _messageIdToSelector)
            {
                if (item.Value.ContentTemplateRoot is MessageSelector selector && selector.Content is MessageBubble bubble)
                {
                    action(bubble);
                }
            }
        }

        public void UpdateMessageSendSucceeded(long oldMessageId, MessageViewModel message)
        {
            if (_messageIdToSelector.TryGetValue(oldMessageId, out SelectorItem container))
            {
                _messageIdToSelector[message.Id] = container;
                _messageIdToSelector.Remove(oldMessageId);
            }

            if (message.ReplyTo is MessageReplyToMessage replyToMessage && _messageIdToMessageIds.TryGetValue(replyToMessage.MessageId, out var ids))
            {
                ids.Add(message.Id);
                ids.Remove(oldMessageId);
            }
        }

        private void UpdateCache(MessageViewModel message, SelectorItem container, bool recycle)
        {
            if (recycle)
            {
                if (message.MediaAlbumId != 0)
                    _albumIdToSelector.Remove(message.MediaAlbumId);

                if (message.Id != 0)
                    _messageIdToSelector.Remove(message.Id);

                if (message.ReplyTo is MessageReplyToMessage replyToMessage)
                    _messageIdToMessageIds.Remove(replyToMessage.MessageId, message.Id);
            }
            else
            {
                if (message.MediaAlbumId != 0)
                    _albumIdToSelector[message.MediaAlbumId] = container;

                if (message.Id != 0)
                    _messageIdToSelector[message.Id] = container;

                if (message.ReplyTo is MessageReplyToMessage replyToMessage)
                    _messageIdToMessageIds.Add(replyToMessage.MessageId, message.Id);
            }
        }
    }
}
