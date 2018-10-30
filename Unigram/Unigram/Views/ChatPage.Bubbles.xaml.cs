using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Unigram.Converters;
using Unigram.Views;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Globalization.DateTimeFormatting;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using Windows.Storage.Search;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;
using Unigram.Controls;
using Windows.Media.Core;
using Windows.Media.Playback;
using Unigram.Common;
using Unigram.Controls.Messages;
using LinqToVisualTree;
using Telegram.Td.Api;
using Windows.Foundation.Metadata;
using Unigram.Controls.Chats;
using Unigram.ViewModels.Gallery;
using Unigram.Controls.Gallery;

namespace Unigram.Views
{
    public partial class ChatPage : Page
    {
        private void OnViewSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (Messages.ScrollingHost.ScrollableHeight > 0)
            {
                return;
            }

            Arrow.Visibility = Visibility.Collapsed;

            ViewVisibleMessages(false);
        }

        private void OnViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (Messages.ScrollingHost.ScrollableHeight - Messages.ScrollingHost.VerticalOffset < 120 && ViewModel.IsFirstSliceLoaded != false)
            {
                Arrow.Visibility = Visibility.Collapsed;
            }
            else
            {
                Arrow.Visibility = Visibility.Visible;
            }

            ViewVisibleMessages(e.IsIntermediate);
            //UpdateHeaderDate(e.IsIntermediate);
        }

        private void ViewVisibleMessages(bool intermediate)
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

                messages.Add(message.Id);
                animations.Add(message);
            }

            if (messages.Count > 0 && _windowContext.ActivationMode == CoreWindowActivationMode.ActivatedInForeground)
            {
                ViewModel.ProtoService.Send(new ViewMessages(chat.Id, messages, false));
            }

            if (animations.Count > 0 && !intermediate)
            {
                Play(animations, ViewModel.Settings.IsAutoPlayEnabled, false);
            }
        }

        private void UnloadVisibleMessages()
        {
            foreach (var item in _old.Values)
            {
                var presenter = item.Presenter;
                if (presenter != null && presenter.MediaPlayer != null)
                {
                    presenter.MediaPlayer.Source = null;
                    presenter.MediaPlayer.Dispose();
                    presenter.MediaPlayer = null;
                }
            }

            _old.Clear();
        }

        private void UpdateHeaderDate(bool intermediate)
        {
            var panel = Messages.ItemsPanelRoot as ItemsStackPanel;
            if (panel == null || panel.FirstVisibleIndex < 0)
            {
                return;
            }

            var minItem = true;
            var minDate = true;

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

                if (minItem)
                {
                    var transform = container.TransformToVisual(DateHeaderRelative);
                    var point = transform.TransformPoint(new Point());
                    var height = (float)DateHeader.ActualHeight;
                    var offset = (float)point.Y + height;

                    if (point.Y + container.ActualHeight >= 0)
                    {
                        minItem = false;
                        DateHeaderLabel.Text = DateTimeToFormatConverter.ConvertDayGrouping(Utils.UnixTimestampToDateTime(message.Date));
                    }
                }

                if (message.Content is MessageHeaderDate && minDate)
                {
                    var transform = container.TransformToVisual(DateHeaderRelative);
                    var point = transform.TransformPoint(new Point());
                    var height = (float)DateHeader.ActualHeight;
                    var offset = (float)point.Y + height;

                    minDate = false;

                    if (offset >= 0 && offset < height)
                    {
                        container.Opacity = 0;
                    }
                    else
                    {
                        container.Opacity = 1;
                    }

                    if (offset >= height && offset < height * 2)
                    {
                        _dateHeader.Offset = new Vector3(0, -height * 2 + offset, 0);
                    }
                    else
                    {
                        _dateHeader.Offset = new Vector3();
                    }
                }
                else
                {
                    container.Opacity = 1;
                }
            }
        }

        class MediaPlayerItem
        {
            public File File { get; set; }
            public Grid Container { get; set; }
            public MediaPlayerView Presenter { get; set; }
            public bool Watermark { get; set; }
            public bool Clip { get; set; }
        }

        private Dictionary<long, MediaPlayerItem> _old = new Dictionary<long, MediaPlayerItem>();

        public async void PlayMessage(MessageViewModel message, FrameworkElement target)
        {
            var text = message.Content as MessageText;

            // If autoplay is enabled and the message contains a video note, then we want a different behavior
            if (ViewModel.Settings.IsAutoPlayEnabled && (message.Content is MessageVideoNote || text?.WebPage != null && text.WebPage.Video != null))
            {
                if (_old.TryGetValue(message.Id, out MediaPlayerItem item))
                {
                    if (item.Presenter == null || item.Presenter.MediaPlayer == null)
                    {
                        return;
                    }

                    // If the video player is muted, then let's play the video again with audio turned on
                    if (item.Presenter.MediaPlayer.IsMuted)
                    {
                        TypedEventHandler<MediaPlayer, object> handler = null;
                        handler = (player, args) =>
                        {
                            player.MediaEnded -= handler;
                            player.IsMuted = true;
                            player.IsLoopingEnabled = true;
                            player.Play();
                        };

                        item.Presenter.MediaPlayer.MediaEnded += handler;
                        item.Presenter.MediaPlayer.IsMuted = false;
                        item.Presenter.MediaPlayer.IsLoopingEnabled = false;
                        item.Presenter.MediaPlayer.PlaybackSession.Position = TimeSpan.Zero;
                    }
                    // If the video player is paused, then resume playback
                    else if (item.Presenter.MediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Paused)
                    {
                        item.Presenter.MediaPlayer.Play();
                    }
                    // And last, if the video player can be pause, then pause it
                    else if (item.Presenter.MediaPlayer.PlaybackSession.CanPause)
                    {
                        item.Presenter.MediaPlayer.Pause();
                    }
                }
            }
            else if (ViewModel.Settings.IsAutoPlayEnabled && (message.Content is MessageAnimation || (text?.WebPage != null && text.WebPage.Animation != null) || (message.Content is MessageGame game && game.Game.Animation != null)))
            {
                if (_old.TryGetValue(message.Id, out MediaPlayerItem item))
                {
                    var viewModel = new SingleGalleryViewModel(ViewModel.ProtoService, ViewModel.Aggregator, new GalleryMessage(ViewModel.ProtoService, message.Get()));
                    await GalleryView.GetForCurrentView().ShowAsync(viewModel, () => target);
                }
                else
                {
                    ViewVisibleMessages(false);
                }
            }
            else
            {
                if (_old.ContainsKey(message.Id))
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
            var news = new Dictionary<long, MediaPlayerItem>();

            foreach (var message in items)
            {
                var container = Messages.ContainerFromItem(message) as ListViewItem;
                if (container == null)
                {
                    continue;
                }

                var animation = message.GetAnimation();
                if (animation == null)
                {
                    continue;
                }

                if (animation.Local.IsDownloadingCompleted)
                {
                    var root = container.ContentTemplateRoot as FrameworkElement;
                    if (root is Grid grid)
                    {
                        root = grid.FindName("Bubble") as FrameworkElement;
                    }

                    var target = message.Content as object;
                    var media = root.FindName("Media") as ContentControl;
                    var panel = media.ContentTemplateRoot as Panel;

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

                        media = panel?.FindName("Media") as ContentControl;
                        panel = media?.ContentTemplateRoot as Panel;
                    }
                    else if (target is MessageGame)
                    {
                        media = panel?.FindName("Media") as ContentControl;
                        panel = media?.ContentTemplateRoot as Panel;
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
                        news[message.Id] = new MediaPlayerItem { File = animation, Container = final, Watermark = message.Content is MessageGame, Clip = target is VideoNote };
                    }
                }
            }

            foreach (var item in _old.Keys.Except(news.Keys).ToList())
            {
                var presenter = _old[item].Presenter;
                if (presenter != null && presenter.MediaPlayer != null)
                {
                    presenter.MediaPlayer.Source = null;
                    presenter.MediaPlayer.Dispose();
                    presenter.MediaPlayer = null;
                }

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

            foreach (var item in news.Keys.Except(_old.Keys).ToList())
            {
                if (_old.ContainsKey(item))
                {
                    continue;
                }

                if (news.TryGetValue(item, out MediaPlayerItem data) && data.Container != null && data.Container.Children.Count < 5)
                {
                    var player = new MediaPlayer();
                    player.AutoPlay = true;
                    player.IsMuted = !audio;
                    player.IsLoopingEnabled = true;
                    player.CommandManager.IsEnabled = false;
                    player.Source = MediaSource.CreateFromUri(new Uri("file:///" + data.File.Local.Path));

                    var presenter = new MediaPlayerView();
                    presenter.MediaPlayer = player;
                    presenter.IsHitTestVisible = false;
                    presenter.Constraint = data.Container.Tag;

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

                _old[item] = news[item];
            }
        }














        private Dictionary<string, DataTemplate> _typeToTemplateMapping = new Dictionary<string, DataTemplate>();
        private Dictionary<string, HashSet<SelectorItem>> _typeToItemHashSetMapping = new Dictionary<string, HashSet<SelectorItem>>();

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            var typeName = SelectTemplateCore(args.Item);

            Debug.Assert(_typeToItemHashSetMapping.ContainsKey(typeName), "The type of the item used with DataTemplateSelectorBehavior must have a DataTemplate mapping");
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
#if ENABLE_DEBUG_SPEW
                    Debug.WriteLine($"Removing (suggested) {args.ItemContainer.GetHashCode()} from {typeName}");
#endif // ENABLE_DEBUG_SPEW
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
#if ENABLE_DEBUG_SPEW
                    Debug.WriteLine($"Removing (reused) {args.ItemContainer.GetHashCode()} from {typeName}");
#endif // ENABLE_DEBUG_SPEW
                }
                else
                {
                    // There aren't any (recycled) ItemContainers available. So a new one
                    // needs to be created.
                    var item = CreateSelectorItem(typeName);
                    item.Style = Messages.ItemContainerStyleSelector.SelectStyle(args.Item, item);
                    args.ItemContainer = item;
#if ENABLE_DEBUG_SPEW
                    Debug.WriteLine($"Creating {args.ItemContainer.GetHashCode()} for {typeName}");
#endif // ENABLE_DEBUG_SPEW
                }
            }

            // Indicate to XAML that we picked a container for it
            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue == true)
            {
                // XAML has indicated that the item is no longer being shown, so add it to the recycle queue
                var tag = args.ItemContainer.Tag as string;

#if ENABLE_DEBUG_SPEW
                Debug.WriteLine($"Adding {args.ItemContainer.GetHashCode()} to {tag}");
#endif // ENABLE_DEBUG_SPEW

                var added = _typeToItemHashSetMapping[tag].Add(args.ItemContainer);

#if ENABLE_DEBUG_SPEW
                Debug.Assert(added == true, "Recycle queue should never have dupes. If so, we may be incorrectly reusing a container that is already in use!");
#endif // ENABLE_DEBUG_SPEW

                return;
            }

            var message = args.Item as MessageViewModel;

            var content = args.ItemContainer.ContentTemplateRoot as FrameworkElement;
            content.Tag = message;

            if (content is Grid grid)
            {
                var photo = grid.FindName("Photo") as ProfilePicture;
                if (photo != null)
                {
                    photo.Visibility = message.IsLast ? Visibility.Visible : Visibility.Collapsed;
                    photo.Tag = message;

                    if (message.IsSaved())
                    {
                        if (message.ForwardInfo is MessageForwardedFromUser fromUser)
                        {
                            var user = message.ProtoService.GetUser(fromUser.SenderUserId);
                            if (user != null)
                            {
                                photo.Source = PlaceholderHelper.GetUser(ViewModel.ProtoService, user, 30);
                            }
                        }
                        else if (message.ForwardInfo is MessageForwardedPost post)
                        {
                            var chat = message.ProtoService.GetChat(post.ChatId);
                            if (chat != null)
                            {
                                photo.Source = PlaceholderHelper.GetChat(ViewModel.ProtoService, chat, 30);
                            }
                        }
                    }
                    else
                    {
                        var user = message.GetSenderUser();
                        if (user != null)
                        {
                            photo.Source = PlaceholderHelper.GetUser(ViewModel.ProtoService, user, 30);
                        }
                    }
                }

                var action = grid.FindName("Action") as Border;
                if (action != null)
                {
                    var button = action.Child as GlyphButton;
                    button.Tag = message;

                    if (message.IsSaved())
                    {
                        button.Glyph = "\uE72A";
                        action.Visibility = Visibility.Visible;
                    }
                    else if (message.IsShareable())
                    {
                        button.Glyph = "\uEE35";
                        action.Visibility = Visibility.Visible;
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
            }

            if (content is MessageBubble bubble)
            {
                bubble.UpdateMessage(args.Item as MessageViewModel);
                args.Handled = true;
            }
            else if (content is MessageService service)
            {
                service.UpdateMessage(args.Item as MessageViewModel);
                args.Handled = true;
            }
        }

        private SelectorItem CreateSelectorItem(string typeName)
        {
            SelectorItem item = new ChatListViewItem(Messages);
            //item.ContextRequested += Message_ContextRequested;
            //item.ContentTemplate = _typeToTemplateMapping[typeName];
            item.ContentTemplate = Resources[typeName] as DataTemplate;
            item.Tag = typeName;
            return item;
        }

        private string SelectTemplateCore(object item)
        {
            //if (item is MessageViewModel message)
            //{

            //}
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
}
