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
using Telegram.Services;
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

            Arrow.Visibility = Visibility.Collapsed;
            //VisualUtilities.SetIsVisible(Arrow, false);

            ViewVisibleMessages(true);

            _debouncer.Stop();
            _debouncer.Start();
        }

        private void OnViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (Messages.ScrollingHost.ScrollableHeight - Messages.ScrollingHost.VerticalOffset < 120 && ViewModel.IsFirstSliceLoaded != false)
            {
                Arrow.Visibility = Visibility.Collapsed;
                //VisualUtilities.SetIsVisible(Arrow, false);
            }
            else if (ViewModel.Type is DialogType.History or DialogType.Thread)
            {
                Arrow.Visibility = Visibility.Visible;
                //VisualUtilities.SetIsVisible(Arrow, true);
            }

            ViewVisibleMessages(true);

            _debouncer.Stop();
            _debouncer.Start();
        }

        private void UnloadVisibleMessages()
        {
            foreach (var item in _prev.Values)
            {
                if (item.Target is IPlayerView view)
                {
                    view.Unload();
                }
            }

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
            if (panel == null || panel.FirstVisibleIndex < 0)
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
                            DateHeader.CommandParameter = null;
                            DateHeaderLabel.Text = string.Format(Strings.MessageScheduledOn, Formatter.DayGrouping(Formatter.ToLocalTime(sendAtDate.SendDate)));
                        }
                        else if (message.SchedulingState is MessageSchedulingStateSendWhenOnline)
                        {
                            DateHeader.CommandParameter = null;
                            DateHeaderLabel.Text = Strings.MessageScheduledUntilOnline;
                        }
                        else if (message.Date > 0)
                        {
                            DateHeader.CommandParameter = message.Date;
                            DateHeaderLabel.Text = Formatter.DayGrouping(Formatter.ToLocalTime(message.Date));
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
            if (messages.Count > 0 && !Messages.IsProgrammaticScrolling && _windowContext.ActivationMode == CoreWindowActivationMode.ActivatedInForeground)
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

                    await GalleryView.ShowAsync(viewModel, () => target);
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
                    var container = Messages.ContainerFromItem(message) as ListViewItem;
                    if (container == null)
                    {
                        return;
                    }

                    Play(new (SelectorItem, MessageViewModel)[] { (container, message) });
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
                        player.Tag = message;

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
                    if (presenter != null && presenter.IsLoopingEnabled)
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













        private readonly Dictionary<string, DataTemplate> _typeToTemplateMapping = new Dictionary<string, DataTemplate>();
        private readonly Dictionary<string, HashSet<SelectorItem>> _typeToItemHashSetMapping = new Dictionary<string, HashSet<SelectorItem>>();

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            var typeName = SelectTemplateCore(args.Item);
            var relevantHashSet = _typeToItemHashSetMapping[typeName];

            // args.ItemContainer is used to indicate whether the ListView is proposing an
            // ItemContainer (ListViewItem) to use. If args.Itemcontainer != null, then there was a
            // recycled ItemContainer available to be reused.
            if (args.ItemContainer != null)
            {
                if (args.ItemContainer.Tag.Equals(typeName))
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
            if (args.InRecycleQueue)
            {
                // XAML has indicated that the item is no longer being shown, so add it to the recycle queue
                var tag = args.ItemContainer.Tag as string;
                var added = _typeToItemHashSetMapping[tag].Add(args.ItemContainer);

                if (args.ItemContainer.ContentTemplateRoot is MessageSelector selettore)
                {
                    if (_effectiveViewportHandler != null)
                    {
                        selettore.EffectiveViewportChanged -= _effectiveViewportHandler;
                    }

                    selettore.Unload();
                }

                if (_sizeChangedHandler != null)
                {
                    args.ItemContainer.SizeChanged -= _sizeChangedHandler;
                }

                return;
            }

            var message = args.Item as MessageViewModel;

            var content = args.ItemContainer.ContentTemplateRoot as FrameworkElement;
            if (content == null)
            {
                return;
            }

            _updateThemeTask?.TrySetResult(true);

            if (args.ItemContainer is ChatListViewItem selector)
            {
                // TODO: are there chances that at this point TextArea is not up to date yet?
                selector.PrepareForItemOverride(message, TextArea.Visibility == Visibility.Visible);
            }

            if (content is MessageSelector checkbox)
            {
                checkbox.UpdateMessage(message, args.ItemContainer as LazoListViewItem);
                checkbox.UpdateSelectionEnabled(ViewModel.IsSelectionEnabled, false);

                content = checkbox.Content as FrameworkElement;
            }
            else if (content is MessageService service)
            {
                if (message.Content is MessageChatChangePhoto chatChangePhoto)
                {
                    var photo = service.FindName("Photo") as ProfilePicture;
                    photo?.SetChatPhoto(message.ClientService, chatChangePhoto.Photo, 120);

                    var view = service.FindName("View") as TextBlock;
                    if (view != null)
                    {
                        view.Text = chatChangePhoto.Photo.Animation != null
                            ? Strings.ViewVideoAction
                            : Strings.ViewPhotoAction;
                    }
                }
                else if (message.Content is MessageSuggestProfilePhoto suggestProfilePhoto)
                {
                    var photo = service.FindName("Photo") as ProfilePicture;
                    photo?.SetChatPhoto(message.ClientService, suggestProfilePhoto.Photo, 120);

                    var view = service.FindName("View") as TextBlock;
                    if (view != null)
                    {
                        view.Text = suggestProfilePhoto.Photo.Animation != null
                            ? Strings.ViewVideoAction
                            : Strings.ViewPhotoAction;
                    }
                }
                else if (message.Content is MessageChatSetBackground chatSetBackground)
                {
                    var photo = service.FindName("Photo") as ChatBackgroundRenderer;
                    photo?.UpdateSource(_viewModel.ClientService, chatSetBackground.Background.Background, true);

                    var view = service.FindName("View") as Border;
                    if (view != null)
                    {
                        view.Visibility = message.IsOutgoing
                            ? Visibility.Collapsed
                            : Visibility.Visible;
                    }
                }
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

        private void RegisterEvents(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            args.ItemContainer.SizeChanged += _sizeChangedHandler ??= new SizeChangedEventHandler(Item_SizeChanged);

            if (args.ItemContainer.ContentTemplateRoot is MessageSelector selector
                && selector.Content is MessageBubble bubble)
            {
                if (SettingsService.Current.Diagnostics.StickyPhotos)
                {
                    selector.EffectiveViewportChanged += _effectiveViewportHandler ??= new TypedEventHandler<FrameworkElement, EffectiveViewportChangedEventArgs>(Item_EffectiveViewportChanged);
                }

                bubble.RegisterEvents();
            }
        }

        private void Item_EffectiveViewportChanged(FrameworkElement sender, EffectiveViewportChangedEventArgs args)
        {
            if (StickyPhoto == null)
            {
                FindName(nameof(StickyPhoto));
            }

            if (sender is MessageSelector selector && selector.Content is MessageBubble bubble)
            {
                var message = selector.Message;

                if (args.EffectiveViewport.Bottom <= sender.ActualHeight && args.EffectiveViewport.Bottom >= 0)
                {
                    if (message.HasSenderPhoto)
                    {
                        if (args.EffectiveViewport.Bottom < 34 && message.IsFirst)
                        {
                            bubble.UpdatePhoto(message, true);
                            bubble.ShowHidePhoto(true, VerticalAlignment.Top);

                            StickyPhoto.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            bubble.ShowHidePhoto(false);

                            var source = bubble.PhotoSource;
                            if (source?.Source == null)
                            {
                                StickyPhoto.SetMessage(message);
                            }
                            else
                            {
                                StickyPhoto.CloneSource(source);
                            }

                            StickyPhoto.Visibility = Visibility.Visible;
                        }
                    }
                    else
                    {
                        StickyPhoto.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    bubble.ShowHidePhoto(message.IsLast && args.EffectiveViewport.Bottom > 0);
                }
            }
        }

        private TypedEventHandler<UIElement, ContextRequestedEventArgs> _contextRequestedHandler;
        private TypedEventHandler<FrameworkElement, EffectiveViewportChangedEventArgs> _effectiveViewportHandler;
        private SizeChangedEventHandler _sizeChangedHandler;

        private SelectorItem CreateSelectorItem(string typeName)
        {
            SelectorItem item = new ChatListViewItem(Messages);
            item.ContentTemplate = _typeToTemplateMapping[typeName];
            item.Tag = typeName;

            item.AddHandler(ContextRequestedEvent, _contextRequestedHandler ??= new TypedEventHandler<UIElement, ContextRequestedEventArgs>(Message_ContextRequested), true);

            return item;
        }

        private void Item_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var panel = Messages.ItemsStack;
            if (panel == null || e.PreviousSize.Height == e.NewSize.Height)
            {
                return;
            }

            var selector = sender as SelectorItem;

            var index = Messages.IndexFromContainer(selector);
            if (index < panel.LastVisibleIndex && e.PreviousSize.Width < 1 && e.PreviousSize.Height < 1)
            {
                return;
            }

            var message = Messages.ItemFromContainer(selector) as MessageViewModel;
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

            if (index >= panel.FirstVisibleIndex && index <= panel.LastVisibleIndex)
            {
                var diff = (float)e.NewSize.Height - (float)e.PreviousSize.Height;
                if (Math.Abs(diff) < 2)
                {
                    return;
                }

                var direction = Messages.ScrollingDirection == PanelScrollingDirection.Backward ? -1 : 1;
                var batch = Window.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

                var anim = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
                anim.InsertKeyFrame(0, (float)(diff * direction));
                anim.InsertKeyFrame(1, 0);
                //anim.Duration = TimeSpan.FromSeconds(5);

                System.Diagnostics.Debug.WriteLine(diff);

                var first = direction == 1 ? panel.FirstCacheIndex : index;
                var last = direction == 1 ? index : panel.LastCacheIndex;

                for (int i = first; i <= last; i++)
                {
                    var container = Messages.ContainerFromIndex(i) as SelectorItem;
                    if (container == null)
                    {
                        continue;
                    }

                    var child = VisualTreeHelper.GetChild(container, 0) as UIElement;

                    var visual = ElementCompositionPreview.GetElementVisual(child);
                    visual.StartAnimation("Offset.Y", anim);
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

            if (message.IsService())
            {
                if (message.Content is MessageChatChangePhoto or MessageSuggestProfilePhoto)
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
    }
}
