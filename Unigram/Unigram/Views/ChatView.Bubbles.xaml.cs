using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reactive.Linq;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Chats;
using Unigram.Controls.Gallery;
using Unigram.Controls.Messages;
using Unigram.Converters;
using Unigram.Services;
using Unigram.ViewModels;
using Unigram.ViewModels.Chats;
using Unigram.ViewModels.Gallery;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Views
{
    public partial class ChatView
    {
        private void OnViewSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (Messages.ScrollingHost.ScrollableHeight > 0)
            {
                return;
            }

            Arrow.Visibility = Visibility.Collapsed;
            //VisualUtilities.SetIsVisible(Arrow, false);

            ViewVisibleMessages(false);
            UpdateHeaderDate(false);
        }

        private void OnViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (Messages.ScrollingHost.ScrollableHeight - Messages.ScrollingHost.VerticalOffset < 120 && ViewModel.IsFirstSliceLoaded != false)
            {
                Arrow.Visibility = Visibility.Collapsed;
                //VisualUtilities.SetIsVisible(Arrow, false);
            }
            else if (ViewModel.Type == DialogType.History || ViewModel.Type == DialogType.Thread)
            {
                Arrow.Visibility = Visibility.Visible;
                //VisualUtilities.SetIsVisible(Arrow, true);
            }

            ViewVisibleMessages(e.IsIntermediate);
            UpdateHeaderDate(e.IsIntermediate);
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

            var messages = new List<long>(panel.LastVisibleIndex - panel.FirstVisibleIndex);
            var animations = new List<MessageViewModel>(panel.LastVisibleIndex - panel.FirstVisibleIndex);

            for (int i = panel.FirstVisibleIndex; i <= panel.LastVisibleIndex; i++)
            {
                var container = Messages.ContainerFromIndex(i) as SelectorItem;
                if (container == null)
                {
                    continue;
                }

                var message = Messages.ItemFromContainer(container) as MessageViewModel;
                if (message == null || message.Id == 0)
                {
                    continue;
                }

                if (message.ContainsUnreadMention)
                {
                    ViewModel.SetLastViewedMention(message.Id);
                }

                if (message.Content is MessageAlbum album)
                {
                    messages.AddRange(album.Messages.Keys);
                }
                else
                {
                    messages.Add(message.Id);
                    animations.Add(message);
                }

                while (ViewModel.RepliesStack.TryPeek(out long reply) && reply == message.Id)
                {
                    ViewModel.RepliesStack.Pop();
                }
            }

            if (messages.Count > 0 && _windowContext.ActivationMode == CoreWindowActivationMode.ActivatedInForeground)
            {
                ViewModel.ProtoService.Send(new ViewMessages(chat.Id, ViewModel.ThreadId, messages, false));
            }

            if (animations.Count > 0 && !intermediate)
            {
                Play(animations, ViewModel.Settings.IsAutoPlayAnimationsEnabled, false);
            }
        }

        private void UnloadVisibleMessages()
        {
            foreach (var item in _old.Values)
            {
                var presenter = item.Presenter;
                if (presenter != null /*&& presenter.MediaPlayer != null*/)
                {
                    //try
                    //{
                    //    presenter.MediaPlayer.Dispose();
                    //    presenter.MediaPlayer = null;
                    //}
                    //catch { }

                    try
                    {
                        item.Container.Children.Remove(presenter);
                    }
                    catch { }
                }
            }

            foreach (var item in _oldStickers.Values)
            {
                var presenter = item.Player;
                if (presenter != null)
                {
                    try
                    {
                        presenter.Pause();
                    }
                    catch { }
                }
            }

            _old.Clear();
            _oldStickers.Clear();
        }

        public void UpdatePinnedMessage()
        {
            UpdateHeaderDate(false);
        }

        private void UpdateHeaderDate(bool intermediate)
        {
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
                else if (message.Id != 0)
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

                        if (message.SchedulingState is MessageSchedulingStateSendAtDate sendAtDate)
                        {
                            DateHeader.CommandParameter = null;
                            DateHeaderLabel.Text = string.Format(Strings.Resources.MessageScheduledOn, BindConvert.DayGrouping(Utils.UnixTimestampToDateTime(sendAtDate.SendDate)));
                        }
                        else if (message.SchedulingState is MessageSchedulingStateSendWhenOnline)
                        {
                            DateHeader.CommandParameter = null;
                            DateHeaderLabel.Text = Strings.Resources.MessageScheduledUntilOnline;
                        }
                        else
                        {
                            DateHeader.CommandParameter = message.Date;
                            DateHeaderLabel.Text = BindConvert.DayGrouping(Utils.UnixTimestampToDateTime(message.Date));
                        }
                    }
                }

                if (message.Content is MessageHeaderDate && minDate && i >= panel.FirstVisibleIndex)
                {
                    var transform = container.TransformToVisual(DateHeaderRelative);
                    var point = transform.TransformPoint(new Point());
                    var height = (float)DateHeader.ActualHeight;
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
            }

            _dateHeaderTimer.Stop();
            _dateHeaderTimer.Start();
            ShowHideDateHeader(minDateIndex > 0, minDateIndex > 0 && minDateIndex < int.MaxValue);

            var thread = ViewModel.Thread;
            if (thread != null)
            {
                var message = thread.Messages.LastOrDefault();
                if (message == null || (firstVisibleId <= message.Id && lastVisibleId >= message.Id))
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
                var currentPinned = ViewModel.PinnedMessages.LastOrDefault(x => x.Id <= lastVisibleId) ?? ViewModel.PinnedMessages.FirstOrDefault();
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
            if ((show && DateHeaderPanel.Visibility == Visibility.Visible) || (!show && (DateHeaderPanel.Visibility == Visibility.Collapsed || _dateHeaderCollapsed)))
            {
                return;
            }

            if (show)
            {
                _dateHeaderCollapsed = false;
            }
            else
            {
                _dateHeaderCollapsed = true;
            }

            if (!animate)
            {
                DateHeaderPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                return;
            }

            DateHeaderPanel.Visibility = Visibility.Visible;

            var batch = _dateHeaderPanel.Compositor.CreateScopedBatch(Windows.UI.Composition.CompositionBatchTypes.Animation);
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

        class MediaPlayerItem
        {
            public File File { get; set; }
            public Grid Container { get; set; }
            public AnimationView Presenter { get; set; }
            public bool Watermark { get; set; }
            public bool Clip { get; set; }
        }

        class LottieViewItem
        {
            public IPlayerView Player { get; set; }
        }

        private Dictionary<long, MediaPlayerItem> _old = new Dictionary<long, MediaPlayerItem>();
        private Dictionary<long, LottieViewItem> _oldStickers = new Dictionary<long, LottieViewItem>();

        public async void PlayMessage(MessageViewModel message, FrameworkElement target)
        {
            var text = message.Content as MessageText;

            // If autoplay is enabled and the message contains a video note, then we want a different behavior
            if (ViewModel.Settings.IsAutoPlayAnimationsEnabled && (message.Content is MessageVideoNote || text?.WebPage != null && text.WebPage.Video != null))
            {
                ViewModel.PlaybackService.Enqueue(message.Get());
                //if (_old.TryGetValue(message.Id, out MediaPlayerItem item))
                //{
                //    if (item.Presenter == null || item.Presenter.MediaPlayer == null)
                //    {
                //        return;
                //    }

                //    // If the video player is muted, then let's play the video again with audio turned on
                //    if (item.Presenter.MediaPlayer.IsMuted)
                //    {
                //        TypedEventHandler<MediaPlayer, object> handler = null;
                //        handler = (player, args) =>
                //        {
                //            player.MediaEnded -= handler;
                //            player.IsMuted = true;
                //            player.IsLoopingEnabled = true;
                //            player.Play();
                //        };

                //        item.Presenter.MediaPlayer.MediaEnded += handler;
                //        item.Presenter.MediaPlayer.IsMuted = false;
                //        item.Presenter.MediaPlayer.IsLoopingEnabled = false;
                //        item.Presenter.MediaPlayer.PlaybackSession.Position = TimeSpan.Zero;

                //        // Mark it as viewed if needed
                //        if (message.Content is MessageVideoNote videoNote && !message.IsOutgoing && !videoNote.IsViewed)
                //        {
                //            ViewModel.ProtoService.Send(new OpenMessageContent(message.ChatId, message.Id));
                //        }
                //    }
                //    // If the video player is paused, then resume playback
                //    else if (item.Presenter.MediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Paused)
                //    {
                //        item.Presenter.MediaPlayer.Play();
                //    }
                //    // And last, if the video player can be pause, then pause it
                //    else if (item.Presenter.MediaPlayer.PlaybackSession.CanPause)
                //    {
                //        item.Presenter.MediaPlayer.Pause();
                //    }
                //}
            }
            else if (ViewModel.Settings.IsAutoPlayAnimationsEnabled && (message.Content is MessageAnimation || (text?.WebPage != null && text.WebPage.Animation != null) || (message.Content is MessageGame game && game.Game.Animation != null)))
            {
                if (_old.TryGetValue(message.AnimationHash(), out MediaPlayerItem item))
                {
                    GalleryViewModelBase viewModel;
                    if (message.Content is MessageAnimation)
                    {
                        viewModel = new ChatGalleryViewModel(ViewModel.ProtoService, ViewModel.Aggregator, message.ChatId, ViewModel.ThreadId, message.Get());
                    }
                    else
                    {
                        viewModel = new SingleGalleryViewModel(ViewModel.ProtoService, ViewModel.Aggregator, new GalleryMessage(ViewModel.ProtoService, message.Get()));
                    }

                    await GalleryView.GetForCurrentView().ShowAsync(viewModel, () => target);
                }
                else
                {
                    ViewVisibleMessages(false);
                }
            }
            else
            {
                if (_old.ContainsKey(message.AnimationHash()))
                {
                    Play(new MessageViewModel[0], false, false);
                }
                else
                {
                    Play(new[] { message }, true, true);
                }
            }
        }

        public void Play(IEnumerable<MessageViewModel> items, bool auto, bool audio)
        {
            PlayStickers(items);

            var next = new Dictionary<long, MediaPlayerItem>();

            foreach (var message in items)
            {
                var animation = message.GetAnimation();
                if (animation == null || !animation.Local.IsDownloadingCompleted)
                {
                    continue;
                }

                var container = Messages.ContainerFromItem(message) as ListViewItem;
                if (container == null)
                {
                    continue;
                }

                var root = container.ContentTemplateRoot as FrameworkElement;
                if (root == null)
                {
                    continue;
                }

                if (root is MessageBubble == false)
                {
                    root = root.FindName("Bubble") as FrameworkElement;
                }

                if (root == null)
                {
                    continue;
                }

                var target = message.Content as object;
                var media = root.FindName("Media") as Border;
                var panel = media.Child as Panel;

                if (target is MessageText messageText && messageText.WebPage != null)
                {
                    if (messageText.WebPage.Animation != null)
                    {
                        target = messageText.WebPage.Animation;
                    }
                    else if (messageText.WebPage.Audio != null)
                    {
                        target = messageText.WebPage.Audio;
                    }
                    else if (messageText.WebPage.Document != null)
                    {
                        target = messageText.WebPage.Document;
                    }
                    else if (messageText.WebPage.Sticker != null)
                    {
                        target = messageText.WebPage.Sticker;
                    }
                    else if (messageText.WebPage.Video != null)
                    {
                        target = messageText.WebPage.Video;
                    }
                    else if (messageText.WebPage.VideoNote != null)
                    {
                        target = messageText.WebPage.VideoNote;
                    }
                    else if (messageText.WebPage.VoiceNote != null)
                    {
                        target = messageText.WebPage.VoiceNote;
                    }
                    else if (messageText.WebPage.Photo != null)
                    {
                        // Photo at last: web page preview might have both a file and a thumbnail
                        target = messageText.WebPage.Photo;
                    }

                    media = panel?.FindName("Media") as Border;
                    panel = media?.Child as Panel;
                }
                else if (target is MessageGame)
                {
                    media = panel?.FindName("Media") as Border;
                    panel = media?.Child as Panel;
                }
                else if (target is MessageVideoNote messageVideoNote)
                {
                    target = messageVideoNote.VideoNote;
                }

                if (target is VideoNote)
                {
                    panel = panel?.FindName("Presenter") as Panel;
                }

                if (panel is Grid final)
                {
                    final.Tag = message;
                    next[message.AnimationHash()] = new MediaPlayerItem { File = animation, Container = final, Watermark = message.Content is MessageGame, Clip = target is VideoNote };
                }
            }

            foreach (var item in _old.Keys.Except(next.Keys).ToList())
            {
                var presenter = _old[item].Presenter;
                //if (presenter != null && presenter.MediaPlayer != null)
                //{
                //    presenter.MediaPlayer.Dispose();
                //    presenter.MediaPlayer = null;
                //}

                var container = _old[item].Container;
                if (container != null && presenter != null)
                {
                    container.Children.Remove(presenter);
                }

                _old.Remove(item);
            }

            if (!auto)
            {
                return;
            }

            foreach (var item in next.Keys.Except(_old.Keys).ToList())
            {
                if (_old.ContainsKey(item))
                {
                    continue;
                }

                if (next.TryGetValue(item, out MediaPlayerItem data) && data.Container != null && data.Container.Children.Count < 5)
                {
                    var presenter = new AnimationView();
                    presenter.AutoPlay = true;
                    presenter.IsLoopingEnabled = true;
                    presenter.IsHitTestVisible = false;
                    presenter.Source = UriEx.GetLocal(data.File.Local.Path);

                    //if (data.Clip && ApiInformation.IsTypePresent("Windows.UI.Composition.CompositionGeometricClip"))
                    //{
                    //    var ellipse = Window.Current.Compositor.CreateEllipseGeometry();
                    //    ellipse.Center = new Vector2(100, 100);
                    //    ellipse.Radius = new Vector2(100, 100);

                    //    var clip = ellipse.Compositor.CreateGeometricClip();
                    //    clip.ViewBox = ellipse.Compositor.CreateViewBox();
                    //    clip.ViewBox.Size = new Vector2(200, 200);
                    //    clip.ViewBox.Stretch = CompositionStretch.UniformToFill;
                    //    clip.Geometry = ellipse;

                    //    var visual = ElementCompositionPreview.GetElementVisual(presenter);
                    //    visual.Clip = clip;
                    //}

                    data.Presenter = presenter;
                    //container.Children.Insert(news[item].Watermark ? 2 : 2, presenter);
                    //container.Children.Add(presenter);

                    data.Container.Children.Add(presenter);
                }

                _old[item] = next[item];
            }
        }

        public void PlayStickers(IEnumerable<MessageViewModel> items)
        {
            var next = new Dictionary<long, LottieViewItem>();
            var prev = new HashSet<long>();

            foreach (var message in items)
            {
                var animation = message.IsAnimatedStickerDownloadCompleted();
                if (animation == false)
                {
                    continue;
                }

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
                        prev.Add(message.AnimationHash());
                        continue;
                    }
                }

                var container = Messages.ContainerFromItem(message) as ListViewItem;
                if (container == null)
                {
                    continue;
                }

                var root = container.ContentTemplateRoot as FrameworkElement;
                if (root == null)
                {
                    continue;
                }

                if (root is MessageBubble == false)
                {
                    root = root.FindName("Bubble") as FrameworkElement;
                }

                if (root == null)
                {
                    continue;
                }

                var target = message.Content as object;
                var media = root.FindName("Media") as Border;
                var panel = media.Child as FrameworkElement;

                if (target is MessageText messageText && messageText.WebPage != null)
                {
                    media = panel?.FindName("Media") as Border;
                    panel = media?.Child as FrameworkElement;
                }

                var lottie = panel?.FindName("Player") as IPlayerView;
                if (lottie != null)
                {
                    lottie.Tag = message;
                    next[message.AnimationHash()] = new LottieViewItem { Player = lottie };
                    System.Diagnostics.Debug.WriteLine("Hash: " + message.AnimationHash());
                }
            }

            foreach (var item in _oldStickers.Keys.Except(next.Keys.Union(prev)).ToList())
            {
                var presenter = _oldStickers[item].Player;
                if (presenter != null && presenter.IsLoopingEnabled)
                {
                    presenter.Pause();
                }

                _oldStickers.Remove(item);
            }

            foreach (var item in next.Keys.Except(_oldStickers.Keys).ToList())
            {
                if (_oldStickers.ContainsKey(item))
                {
                    continue;
                }

                if (next.TryGetValue(item, out LottieViewItem data) && data.Player != null)
                {
                    data.Player.Play();
                }

                _oldStickers[item] = next[item];
            }
        }













        private Dictionary<string, DataTemplate> _typeToTemplateMapping = new Dictionary<string, DataTemplate>();
        private Dictionary<string, HashSet<SelectorItem>> _typeToItemHashSetMapping = new Dictionary<string, HashSet<SelectorItem>>();

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
            if (args.InRecycleQueue == true)
            {
                var test = args.ItemContainer.ContentTemplateRoot as FrameworkElement;
                if (test is Grid grudd)
                {
                    test = grudd.FindName("Bubble") as FrameworkElement;
                }

                if (test is MessageBubble bubbu)
                {
                    bubbu.UpdateMessage(null);
                }

                // XAML has indicated that the item is no longer being shown, so add it to the recycle queue
                var tag = args.ItemContainer.Tag as string;
                var added = _typeToItemHashSetMapping[tag].Add(args.ItemContainer);

                return;
            }

            var message = args.Item as MessageViewModel;

            var content = args.ItemContainer.ContentTemplateRoot as FrameworkElement;
            if (content == null)
            {
                return;
            }

            content.Tag = message;

            if (args.ItemContainer is ChatListViewItem selector)
            {
                selector.PrepareForItemOverride(message);
            }

            if (content is Grid grid)
            {
                var photo = grid.FindName("Photo") as ProfilePicture;
                if (photo != null)
                {
                    photo.Visibility = message.IsLast ? Visibility.Visible : Visibility.Collapsed;
                    photo.Tag = message;

                    if (message.IsSaved())
                    {
                        if (message.ForwardInfo?.Origin is MessageForwardOriginUser fromUser)
                        {
                            var user = message.ProtoService.GetUser(fromUser.SenderUserId);
                            if (user != null)
                            {
                                photo.Source = PlaceholderHelper.GetUser(ViewModel.ProtoService, user, 30);
                            }
                        }
                        else if (message.ForwardInfo?.Origin is MessageForwardOriginChat fromChat)
                        {
                            var chat = message.ProtoService.GetChat(fromChat.SenderChatId);
                            if (chat != null)
                            {
                                photo.Source = PlaceholderHelper.GetChat(ViewModel.ProtoService, chat, 30);
                            }
                        }
                        else if (message.ForwardInfo?.Origin is MessageForwardOriginChannel fromChannel)
                        {
                            var chat = message.ProtoService.GetChat(fromChannel.ChatId);
                            if (chat != null)
                            {
                                photo.Source = PlaceholderHelper.GetChat(ViewModel.ProtoService, chat, 30);
                            }
                        }
                        else if (message.ForwardInfo?.Origin is MessageForwardOriginHiddenUser fromHiddenUser)
                        {
                            photo.Source = PlaceholderHelper.GetNameForUser(fromHiddenUser.SenderName, 30);
                        }
                    }
                    else if (message.ProtoService.TryGetUser(message.Sender, out User senderUser))
                    {
                        photo.Source = PlaceholderHelper.GetUser(ViewModel.ProtoService, senderUser, 30);
                    }
                    else if (message.ProtoService.TryGetChat(message.Sender, out Chat senderChat))
                    {
                        photo.Source = PlaceholderHelper.GetChat(ViewModel.ProtoService, senderChat, 30);
                    }
                }

                var action = grid.FindName("Action") as Border;
                if (action != null)
                {
                    var button = action.Child as GlyphButton;
                    button.Tag = message;

                    if (message.IsSaved())
                    {
                        if (message.ForwardInfo?.Origin is MessageForwardOriginHiddenUser)
                        {
                            action.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            button.Glyph = "\uE72A";
                            action.Visibility = Visibility.Visible;

                            Automation.SetToolTip(button, Strings.Resources.AccDescrOpenChat);
                        }
                    }
                    else if (message.IsShareable())
                    {
                        button.Glyph = "\uE72D";
                        action.Visibility = Visibility.Visible;

                        Automation.SetToolTip(button, Strings.Resources.ShareFile);
                    }
                    else
                    {
                        action.Visibility = Visibility.Collapsed;
                    }
                }

                content = grid.FindName("Bubble") as FrameworkElement;
            }
            else if (content is StackPanel panel && !(content is MessageBubble))
            {
                content = panel.FindName("Service") as FrameworkElement;

                if (message.Content is MessageChatChangePhoto chatChangePhoto)
                {
                    var photo = panel.FindName("Photo") as Image;
                    if (photo != null)
                    {
                        var file = chatChangePhoto.Photo.GetSmall();
                        if (file != null)
                        {
                            if (file.Photo.Local.IsDownloadingCompleted)
                            {
                                photo.Source = new BitmapImage(new Uri("file:///" + file.Photo.Local.Path)) { DecodePixelWidth = 120, DecodePixelHeight = 120, DecodePixelType = DecodePixelType.Logical };
                            }
                            else if (file.Photo.Local.CanBeDownloaded && !file.Photo.Local.IsDownloadingActive)
                            {
                                photo.Source = null;
                                ViewModel.ProtoService.DownloadFile(file.Photo.Id, 1);
                            }
                        }
                    }
                }
            }

            if (content is MessageBubble bubble)
            {
                bubble.UpdateMessage(args.Item as MessageViewModel);
                args.Handled = true;
            }
            else if (content is MessageService service)
            {
                if (args.Item is MessageViewModel viewModel && (viewModel.Content is MessageChatUpgradeFrom || viewModel.Content is MessageChatUpgradeTo))
                {
                    service.Visibility = Visibility.Collapsed;
                }
                else
                {
                    service.Visibility = Visibility.Visible;
                }

                service.UpdateMessage(args.Item as MessageViewModel);
                args.Handled = true;
            }
        }

        private TypedEventHandler<UIElement, ContextRequestedEventArgs> _contextRequestedHandler;

        private SelectorItem CreateSelectorItem(string typeName)
        {
            SelectorItem item = new ChatListViewItem(Messages);
            item.ContentTemplate = _typeToTemplateMapping[typeName];
            item.Tag = typeName;

            // For some reason the event is available since Anniversary Update,
            // but the property has been added in April Update.
            if (ApiInfo.CanAddContextRequestedEvent)
            {
                item.AddHandler(ContextRequestedEvent, _contextRequestedHandler ??= new TypedEventHandler<UIElement, ContextRequestedEventArgs>(Message_ContextRequested), true);
            }
            else
            {
                item.ContextRequested += _contextRequestedHandler ??= new TypedEventHandler<UIElement, ContextRequestedEventArgs>(Message_ContextRequested);
            }

            return item;
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
                if (message.Content is MessageChatChangePhoto)
                {
                    return "ServiceMessagePhotoTemplate";
                }
                else if (message.Content is MessageHeaderUnread)
                {
                    return "ServiceMessageUnreadTemplate";
                }

                return "ServiceMessageTemplate";
            }

            if (message.IsChannelPost)
            {
                return "FriendMessageTemplate";
            }
            else if (message.IsSaved())
            {
                return "ChatFriendMessageTemplate";
            }
            else if (message.IsOutgoing)
            {
                return "UserMessageTemplate";
            }

            var chat = message.GetChat();
            if (chat != null && chat.Type is ChatTypeSupergroup || chat.Type is ChatTypeBasicGroup)
            {
                return "ChatFriendMessageTemplate";
            }

            return "FriendMessageTemplate";
        }
    }

    public interface IGifPlayback
    {
        void Play(MessageViewModel message);
        void Play(IEnumerable<MessageViewModel> items, bool auto);
    }

    public interface IPlayerView
    {
        void Play();
        void Pause();

        bool IsLoopingEnabled { get; }

        object Tag { get; set; }
    }
}
