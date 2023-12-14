using LibVLCSharp.Shared;
using LinqToVisualTree;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Controls.Messages;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Stories;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;
using Point = Windows.Foundation.Point;

namespace Telegram.Controls.Stories
{
    public class StoryEventArgs : EventArgs
    {
        public ActiveStoriesViewModel ActiveStories { get; }

        public StoryEventArgs(ActiveStoriesViewModel activeStories)
        {
            ActiveStories = activeStories;
        }
    }

    public enum StoryType
    {
        Photo,
        Video,
    }

    public sealed partial class StoryContent : UserControl
    {
        private readonly Windows.System.DispatcherQueue _dispatcherQueue;
        private readonly LifoActionWorker _playbackQueue;

        private volatile bool _unloaded;

        public StoryContent()
        {
            InitializeComponent();

            _dispatcherQueue = Windows.System.DispatcherQueue.GetForCurrentThread();
            _playbackQueue = new LifoActionWorker();

            _texture = Texture1;

            _timer = new StoryContentPhotoTimer();
            _timer.Tick += OnTick;

            SizeChanged += OnSizeChanged;
            Unloaded += OnUnloaded;
        }

        private void OnTick(object sender, EventArgs e)
        {
            Completed?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler<StoryEventArgs> MoreClick;

        public event EventHandler Completed;

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _unloaded = true;
            Logger.Info();

            Task.Run(() =>
            {
                if (_player != null)
                {
                    _player.Stop();
                    _player.Vout -= OnVout;
                    _player.Buffering -= OnBuffering;
                    _player.EndReached -= OnEndReached;
                    //_player.Stopped -= OnStopped;

                    _player.Dispose();
                    _player = null;
                }

                _library?.Dispose();
                _library = null;

                _mediaStream?.Dispose();
                _mediaStream = null;
            });
        }

        private void CollapseCaption()
        {
            Overflow.MaxLines = 1;
            ShowMore.Visibility = Overflow.MaxLines == 1 && Caption.HasOverflowContent
                ? Visibility.Visible
                : Visibility.Collapsed;

            Grid.SetRow(CaptionExpand, 1);

            CaptionOverlay.Visibility = Visibility.Collapsed;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            var root = ElementCompositionPreview.GetElementVisual(Content);
            var compositor = root.Compositor;

            var rect1 = CanvasGeometry.CreateRoundedRectangle(null, 0, 0, ActualSize.X, ActualSize.Y, _open ? 8 : 8 * 2.5f, _open ? 8 : 8 * 2.5f);

            var geometry1 = compositor.CreatePathGeometry(new CompositionPath(rect1));
            var clip1 = compositor.CreateGeometricClip(geometry1);
            root.Clip = clip1;

            if (_loading)
            {
                ShowSkeleton();
            }

            UpdateAreas();
        }

        public event RoutedEventHandler Click
        {
            add => InactiveRoot.Click += value;
            remove => InactiveRoot.Click -= value;
        }

        private ActiveStoriesViewModel _viewModel;
        public ActiveStoriesViewModel ViewModel => _viewModel;

        private long _chatId;
        private int _storyId;

        private long _openedChatId;
        private int _openedStoryId;

        private bool _open;
        private int _index;

        public void Update(ActiveStoriesViewModel activeStories, bool open, int index)
        {
            _viewModel = activeStories;
            _index = index;

            if (open)
            {
                _unloaded = false;
            }

            var chat = activeStories.Chat;
            if (chat != null && chat.Id != _chatId)
            {
                _chatId = chat.Id;
                _storyId = 0;

                _state = StoryPauseSource.None;

                if (activeStories.ClientService.TryGetUser(chat, out User user))
                {
                    if (activeStories.IsMyStory)
                    {
                        TitleMini.Text = Strings.SelfStoryTitle;
                        Title.Text = Strings.SelfStoryTitle;

                        Identity.ClearStatus();
                    }
                    else
                    {
                        TitleMini.Text = user.FirstName;
                        Title.Text = user.FirstName;

                        Identity.SetStatus(activeStories.ClientService, user);
                    }

                    PhotoMini.SetUser(activeStories.ClientService, user, 48);
                    Photo.SetUser(activeStories.ClientService, user, 32);
                }
                else
                {
                    TitleMini.Text = chat.Title;
                    Title.Text = chat.Title;

                    Identity.SetStatus(activeStories.ClientService, chat);

                    PhotoMini.SetChat(activeStories.ClientService, chat, 48);
                    Photo.SetChat(activeStories.ClientService, chat, 32);
                }

                if (activeStories.Item != null)
                {
                    SegmentsInactive.UpdateActiveStories(activeStories.Item, 48, true);
                }

                Video?.Clear();
            }

            var story = activeStories.SelectedItem;
            if (story != null && story.StoryId != _storyId)
            {
                _timer.Stop();
                _state = StoryPauseSource.None;

                var position = activeStories.Items.IndexOf(story);
                var limit = Math.Min(position + 10, activeStories.Items.Count);

                // We go reverse, as download queue is LIFO
                for (int i = activeStories.Items.Count - 1; i >= 0; i--)
                {
                    if (i > position && i < limit)
                    {
                        activeStories.Items[i].Prepare();
                    }
                    else
                    {
                        activeStories.Items[i].Load();
                    }
                }

                Update(story);
            }

            Canvas.SetZIndex(ActiveRoot, open ? 1 : 0);

            var root = ElementCompositionPreview.GetElementVisual(Content);
            var mini = ElementCompositionPreview.GetElementVisual(MiniInside);

            var inactive = ElementCompositionPreview.GetElementVisual(InactiveRoot);
            var active = ElementCompositionPreview.GetElementVisual(ActiveRoot);

            var visual = ElementCompositionPreview.GetElementVisual(MiniInside);
            visual.CenterPoint = new Vector3(MiniInside.ActualSize / 2, 0);

            mini.Opacity = 1;

            ElementCompositionPreview.SetIsTranslationEnabled(MiniInside, true);

            var compositor = visual.Compositor;

            inactive.Opacity = open ? 0 : 1;
            active.Opacity = open ? 1 : 0;
            visual.Scale = new Vector3(open ? 1 : 2.5f);

            var rect1 = CanvasGeometry.CreateRoundedRectangle(null, 0, 0, ActualSize.X, ActualSize.Y, open ? 8 : 8 * 2.5f, open ? 8 : 8 * 2.5f);

            var geometry1 = compositor.CreatePathGeometry(new CompositionPath(rect1));
            var clip1 = compositor.CreateGeometricClip(geometry1);
            root.Clip = clip1;

            if (story == null)
            {
                return;
            }

            if (open && (story.StoryId != _storyId || !_open))
            {
                if (story.ChatId != _openedChatId || story.StoryId != _openedStoryId)
                {
                    _viewModel.ClientService.Send(new CloseStory(_openedChatId, _openedStoryId));
                    _openedChatId = 0;
                    _openedStoryId = 0;
                }

                Activate(story);
            }
            else if (_open && !open)
            {
                Deactivate(story);
            }

            _open = open;
            _storyId = story.StoryId;
        }

        private void Update(StoryViewModel story)
        {
            Subtitle.Text = Locale.FormatRelativeShort(story.Date);

            switch (story.PrivacySettings)
            {
                case StoryPrivacySettingsCloseFriends:
                    Privacy.Background = App.Current.Resources["StoryPrivacyCloseFriendsBrush"] as Brush;
                    PrivacyIcon.Text = Icons.StarFilled16;
                    break;
                case StoryPrivacySettingsSelectedUsers:
                    Privacy.Background = App.Current.Resources["StoryPrivacySelectedContactsBrush"] as Brush;
                    PrivacyIcon.Text = Icons.PeopleFilled16;
                    break;
                case StoryPrivacySettingsContacts:
                    Privacy.Background = App.Current.Resources["StoryPrivacyContactsBrush"] as Brush;
                    PrivacyIcon.Text = Icons.PersonCircleFilled16;
                    break;
            }

            PrivacyButton.Visibility =
                Privacy.Visibility = story.PrivacySettings is StoryPrivacySettingsEveryone
                    ? Visibility.Collapsed
                    : Visibility.Visible;

            if (story.Content is StoryContentPhoto photo)
            {
                var file = photo.Photo.GetBig();
                if (file != null)
                {
                    UpdatePhoto(story, file.Photo, true);
                }

                var thumbnail = photo.Photo.GetSmall();
                if (thumbnail != null /*&& (file == null || !file.Photo.Local.IsDownloadingCompleted)*/)
                {
                    UpdateThumbnail(story, thumbnail.Photo, photo.Photo.Minithumbnail, true);
                }

                Mute.Visibility = Visibility.Collapsed;
                MutePlaceholder.Visibility = Visibility.Collapsed;
            }
            else if (story.Content is StoryContentVideo video)
            {
                var thumbnail = video.Video.Thumbnail;
                if (thumbnail != null /*&& (file == null || !file.Photo.Local.IsDownloadingCompleted)*/)
                {
                    UpdateThumbnail(story, thumbnail.File, video.Video.Minithumbnail, true);
                }

                UpdateVideo(story, /*video.AlternativeVideo?.Video ??*/ video.Video.Video, true);

                Mute.Visibility = Visibility.Visible;
                MutePlaceholder.Visibility = video.Video.IsAnimation
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                Mute.IsEnabled = !video.Video.IsAnimation;
                Mute.IsChecked = !video.Video.IsAnimation && !_viewModel.Settings.VolumeMuted;
            }

            if (string.IsNullOrEmpty(story.Caption?.Text))
            {
                CaptionRoot.Visibility = Visibility.Collapsed;
                Caption.Clear();
            }
            else
            {
                CaptionRoot.Visibility = Visibility.Visible;
                Caption.SetText(story.ClientService, story.Caption);
                Overflow.LayoutUpdated += Overflow_LayoutUpdated;
            }

            UpdateAreas();
        }

        private void UpdateAreas()
        {
            AreasPanel.Children.Clear();

            var story = _viewModel?.SelectedItem;
            if (story?.Areas == null)
            {
                return;
            }

            foreach (var area in story.Areas)
            {
                FrameworkElement element;
                if (area.Type is StoryAreaTypeSuggestedReaction suggestedReaction)
                {
                    var desiredWidth = area.Position.WidthPercentage / 100 * ActualWidth;
                    var desiredHeight = area.Position.HeightPercentage / 100 * ActualHeight;

                    var flipped = suggestedReaction.IsFlipped;
                    var test = new Grid
                    {
                        //Background = new SolidColorBrush(Color.FromArgb(0x7F, 0xFF, 0, 0)),
                        Width = 115,
                        Height = 115,
                        RenderTransformOrigin = new Point(0.5, 0.5),
                        RenderTransform = new CompositeTransform
                        {
                            Rotation = area.Position.RotationAngle,
                            ScaleX = desiredWidth / 115,
                            ScaleY = desiredHeight / 115
                        }
                    };

                    var data = new GeometryGroup
                    {
                        FillRule = FillRule.Nonzero
                    };
                    data.Children.Add(new EllipseGeometry
                    {
                        Center = new Point(flipped ? 52 : 50, 50),
                        RadiusX = 50,
                        RadiusY = 50
                    });
                    data.Children.Add(new EllipseGeometry
                    {
                        Center = new Point(flipped ? 21 : 83, 83),
                        RadiusX = 12.5,
                        RadiusY = 12.5
                    });
                    data.Children.Add(new EllipseGeometry
                    {
                        Center = new Point(flipped ? 5 : 98, 98),
                        RadiusX = 5,
                        RadiusY = 5
                    });

                    var path = new Path
                    {
                        Data = data,
                        Fill = new SolidColorBrush(suggestedReaction.IsDark ? Colors.Black : Colors.White),
                        Opacity = suggestedReaction.IsDark ? 0.5 : 1,
                        StrokeThickness = 0,
                        Stretch = Stretch.Uniform,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch,
                        Width = 103,
                        Height = 103
                    };

                    var shadow = new Border();

                    DropShadowEx.Attach(path, target: shadow);

                    var test2 = new Grid();
                    test2.Padding = new Thickness(6);

                    var side = 70; //Math.Sqrt((100 * 100) / 2);
                    var margin = (100 - side) / 2;

                    //var lottie = new AnimatedImage
                    //{
                    //    Width = side,
                    //    Height = side,
                    //    DecodeFrameType = Windows.UI.Xaml.Media.Imaging.DecodePixelType.Logical,
                    //    FrameSize = new Windows.Foundation.Size(side, side),
                    //    Source = new ReactionFileSource(ViewModel.ClientService, suggestedReaction.ReactionType),
                    //    HorizontalAlignment = HorizontalAlignment.Left,
                    //    VerticalAlignment = VerticalAlignment.Top,
                    //    Margin = new Thickness(flipped ? margin + 3 : margin, margin, 0, 0),
                    //    AutoPlay = true 
                    //};

                    var lottie = new StoryReactionButton
                    {
                        Width = side,
                        Height = side,
                        Foreground = new SolidColorBrush(suggestedReaction.IsDark ? Colors.White : Colors.Black),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(flipped ? margin + 3 : margin, margin, 0, 0),
                        Style = Resources["SuggestedReactionButtonStyle"] as Style
                    };

                    LoadButton(story, lottie, suggestedReaction);

                    test2.Children.Add(shadow);
                    test2.Children.Add(path);
                    test2.Children.Add(lottie);

                    //test.Children.Add(new Viewbox
                    //{
                    //    Child = test2
                    //});

                    test.Children.Add(test2);

                    element = test;
                }
                else
                {
                    var button = new HyperlinkButton
                    {
                        Content = new Border
                        {
                            Width = 24,
                            Height = 24,
                            HorizontalAlignment = HorizontalAlignment.Center
                        },
                        Width = area.Position.WidthPercentage / 100 * ActualWidth,
                        Height = area.Position.HeightPercentage / 100 * ActualHeight,
                        RenderTransformOrigin = new Point(0.5, 0.5),
                        RenderTransform = new RotateTransform
                        {
                            Angle = area.Position.RotationAngle
                        }
                    };

                    button.Click += Area_Click;
                    element = button;
                }

                Canvas.SetLeft(element, area.Position.XPercentage / 100 * ActualWidth - element.Width / 2);
                Canvas.SetTop(element, area.Position.YPercentage / 100 * ActualHeight - element.Height / 2);

                AreasPanel.Children.Add(element);

                element.Tag = area;
            }
        }

        private async void LoadButton(StoryViewModel story, StoryReactionButton button, StoryAreaTypeSuggestedReaction suggestedReaction)
        {
            var reactionType = suggestedReaction.ReactionType;
            if (reactionType is ReactionTypeEmoji emoji)
            {
                if (story.ClientService.TryGetCachedReaction(emoji.Emoji, out EmojiReaction reaction))
                {
                    button.SetReaction(story, reactionType, reaction, null);
                }
                else
                {
                    var response = await story.ClientService.SendAsync(new GetEmojiReaction(emoji.Emoji));
                    if (response is EmojiReaction reaction2)
                    {
                        button.SetReaction(story, reactionType, reaction2, null);
                    }
                }
            }
            else if (reactionType is ReactionTypeCustomEmoji customEmoji)
            {
                if (EmojiCache.TryGet(customEmoji.CustomEmojiId, out Sticker sticker))
                {
                    button.SetReaction(story, reactionType, sticker, null);
                }
                else
                {
                    var response = await EmojiCache.GetAsync(story.ClientService, customEmoji.CustomEmojiId);
                    if (response is Sticker sticker2)
                    {
                        button.SetReaction(story, reactionType, sticker2, null);
                    }
                }
            }
        }

        private void Area_Click(object sender, RoutedEventArgs e)
        {
            if (sender is HyperlinkButton element && element.Tag is StoryArea area)
            {
                if (area.Type is StoryAreaTypeLocation or StoryAreaTypeVenue)
                {
                    var window = element.Ancestors<StoriesWindow>().FirstOrDefault();
                    window?.ShowTeachingTip(element.Content as Border, Strings.StoryViewLocation, TeachingTipPlacementMode.Top);
                }
            }
        }

        public void Animate(int from, int to)
        {
            if (from != 3 && to != 3)
            {
                return;
            }

            Canvas.SetZIndex(ActiveRoot, to == 3 ? 1 : 0);

            var root = ElementCompositionPreview.GetElementVisual(Content);
            var mini = ElementCompositionPreview.GetElementVisual(MiniInside);

            var inactive = ElementCompositionPreview.GetElementVisual(InactiveRoot);
            var active = ElementCompositionPreview.GetElementVisual(ActiveRoot);

            var visual = ElementCompositionPreview.GetElementVisual(MiniInside);
            visual.CenterPoint = new Vector3(MiniInside.ActualSize / 2, 0);

            mini.Opacity = 1;

            ElementCompositionPreview.SetIsTranslationEnabled(MiniInside, true);

            var compositor = visual.Compositor;

            var opacityOut = compositor.CreateScalarKeyFrameAnimation();
            //opacityOut.InsertKeyFrame(to == 3 ? 0 : 1, 1);
            //opacityOut.InsertKeyFrame(to == 3 ? 1 : 0, 0);

            opacityOut.InsertKeyFrame(1, to == 3 ? 0 : 1);

            var opacityIn = compositor.CreateScalarKeyFrameAnimation();
            //opacityIn.InsertKeyFrame(to == 3 ? 0 : 1, 0);
            //opacityIn.InsertKeyFrame(to == 3 ? 1 : 0, 1);

            opacityIn.InsertKeyFrame(1, to == 3 ? 1 : 0);

            inactive.StartAnimation("Opacity", opacityOut);
            active.StartAnimation("Opacity", opacityIn);

            var scale = visual.Compositor.CreateVector3KeyFrameAnimation();
            //scale.InsertKeyFrame(to == 3 ? 1 : 0, new Vector3(36f / 48f));
            //scale.InsertKeyFrame(to == 3 ? 0 : 1, new Vector3(2.5f));

            scale.InsertKeyFrame(1, new Vector3(to == 3 ? 36f / 48f : 2.5f));

            //scale.Duration = TimeSpan.FromSeconds(1);

            visual.StartAnimation("Scale", scale);

            var device = CanvasDevice.GetSharedDevice();
            var rect1 = CanvasGeometry.CreateRoundedRectangle(device, 0, 0, ActualSize.X, ActualSize.Y, 8, 8);
            var rect2 = CanvasGeometry.CreateRoundedRectangle(device, 0, 0, ActualSize.X, ActualSize.Y, 8 * 2.5f, 8 * 2.5f);

            var geometry1 = compositor.CreatePathGeometry(new CompositionPath(rect1));
            var clip1 = compositor.CreateGeometricClip(geometry1);

            var pathAnimation = compositor.CreatePathKeyFrameAnimation();
            pathAnimation.InsertKeyFrame(to == 3 ? 1 : 0, new CompositionPath(rect1));
            pathAnimation.InsertKeyFrame(to == 3 ? 0 : 1, new CompositionPath(rect2));

            geometry1.StartAnimation("Path", pathAnimation);
            root.Clip = clip1;
        }

        private void Play(RemoteFileStream stream)
        {
            if (_player != null && !_unloaded && stream != null)
            {
                _player.Play(new LibVLCSharp.Shared.Media(_library, stream));
            }
        }

        private void Activate(StoryViewModel story)
        {
            CollapseCaption();

            if (story.Content is StoryContentVideo video)
            {
                Progress.Update(_viewModel.Items.IndexOf(_viewModel.SelectedItem), _viewModel.Items.Count, video.Video.Duration);

                var file = video.Video.Video;
                var stream = new RemoteFileStream(story.ClientService, file);

                if (_player != null)
                {
                    _mediaStream = stream;
                    _playbackQueue.Enqueue(() => Play(_mediaStream));
                }
                else if (Video != null)
                {
                    if (Video.IsLoaded)
                    {
                        _mediaStream = stream;
                        Video_Initialized(Video, new LibVLCSharp.Platforms.Windows.InitializedEventArgs(Video.SwapChainOptions));
                    }
                    else
                    {
                        // TODO: dispose
                    }
                }
                else
                {
                    _mediaStream = stream;
                    FindName(nameof(Video));
                }
            }
            else if (!_loading)
            {
                _timer.Stop();
                _timer.Start();
                Progress.Update(_viewModel.Items.IndexOf(_viewModel.SelectedItem), _viewModel.Items.Count, 5);
            }
        }

        private void Deactivate(StoryViewModel story)
        {
            if (_player != null)
            {
                _playbackQueue.Enqueue(_player.Stop);
            }

            //UnloadVideo();
            CollapseCaption();

            if (_openedChatId == story.ChatId && _openedStoryId == story.StoryId)
            {
                _viewModel.ClientService.Send(new CloseStory(story.ChatId, story.StoryId));
                _openedChatId = 0;
                _openedStoryId = 0;
            }
        }

        private void Photo_Click(object sender, RoutedEventArgs e)
        {

        }





        private long _fileToken;
        private long _thumbnailToken;

        private void UpdateThumbnail(object target, File file)
        {
            UpdateThumbnail(_viewModel.SelectedItem, file, null, false);
        }

        private void UpdatePhoto(object target, File file)
        {
            UpdatePhoto(_viewModel.SelectedItem, file, false);
        }

        private void UpdateVideo(object target, File file)
        {
            UpdateVideo(_viewModel.SelectedItem, file, false);
        }

        private void UpdateThumbnail(StoryViewModel story, File file, Minithumbnail minithumbnail, bool download)
        {
            if (file.Id == _thumbnailId && download)
            {
                return;
            }

            _thumbnailId = file.Id;

            BitmapImage source = null;
            ImageBrush brush;

            if (LayoutRoot.Background is ImageBrush existing)
            {
                brush = existing;
            }
            else
            {
                brush = new ImageBrush
                {
                    Stretch = Stretch.UniformToFill,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center
                };

                LayoutRoot.Background = brush;
            }

            if (file != null)
            {
                if (file.Local.IsDownloadingCompleted)
                {
                    source = new BitmapImage();
                    PlaceholderHelper.GetBlurred(source, file.Local.Path, 3);
                }
                else
                {
                    if (download)
                    {
                        if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                        {
                            story.ClientService.DownloadFile(file.Id, 1);
                        }

                        UpdateManager.Subscribe(this, story.ClientService, file, ref _thumbnailToken, UpdateThumbnail, true);
                    }

                    if (minithumbnail != null)
                    {
                        source = new BitmapImage();
                        PlaceholderHelper.GetBlurred(source, minithumbnail.Data, 3);
                    }
                }
            }
            else if (minithumbnail != null)
            {
                source = new BitmapImage();
                PlaceholderHelper.GetBlurred(source, minithumbnail.Data, 3);
            }

            brush.ImageSource = source;
        }

        private void UpdatePhoto(StoryViewModel story, File file, bool download)
        {
            if (file.Id == _fileId && download)
            {
                return;
            }

            UpdateManager.Unsubscribe(this, ref _fileToken, true);

            _fileId = file.Id;
            Logger.Info();

            if (_player != null)
            {
                _playbackQueue.Enqueue(_player.Stop);
            }

            if (_type == StoryType.Photo)
            {
                Video?.Clear();
            }

            if (file.Local.IsDownloadingCompleted)
            {
                var next = _texture == Texture1 ? Texture2 : Texture1;
                var prev = _texture;

                Canvas.SetZIndex(next, 1);
                Canvas.SetZIndex(prev, _type == StoryType.Photo ? 0 : -1);
                Canvas.SetZIndex(VideoPanel, _type == StoryType.Video ? 0 : -1);

                next.Source = UriEx.ToBitmap(file.Local.Path, 0, 0);
            }
            else if (download)
            {
                _loading = true;
                ShowSkeleton();

                //VideoPanel.Opacity = 0;
                //Texture1.Opacity = 0;
                //Texture2.Opacity = 0;

                if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    story.ClientService.DownloadFile(file.Id, 1);
                }

                UpdateManager.Subscribe(this, _viewModel.ClientService, file, ref _fileToken, UpdatePhoto, true);
            }

            _type = StoryType.Photo;
        }

        private RemoteFileStream _mediaStream;

        private StoryType _type;

        private int _fileId;
        private int _thumbnailId;

        private void UpdateVideo(StoryViewModel story, File file, bool download)
        {
            if (file.Id == _fileId && download)
            {
                return;
            }

            UpdateManager.Unsubscribe(this, ref _fileToken, true);

            _fileId = file.Id;
            //Player.Source = null;

            Logger.Info();

            //_player?.Stop();

            //if (_mediaStream != null)
            //{
            //    _mediaStream.Dispose();
            //    _mediaStream = null;
            //}

            var video = story.Content as StoryContentVideo;

            //_loading = true;
            //ShowSkeleton();

            var prev = _texture == Texture1 ? Texture2 : Texture1;
            var next = _texture;

            Canvas.SetZIndex(VideoPanel, 1);
            Canvas.SetZIndex(next, 0);
            Canvas.SetZIndex(prev, -1);

            // Preloaded?
            if (file.Local.DownloadedPrefixSize >= video.Video.PreloadPrefixSize)
            {
                if (_type == StoryType.Photo && Video != null)
                {
                    Video.Clear();
                }
            }
            else
            {
                Video?.Clear();
            }

            story.ClientService.DownloadFile(file.Id, 32, 0, video.Video.PreloadPrefixSize);

            _type = StoryType.Video;
        }

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var active = ElementCompositionPreview.GetElementVisual(ActiveRoot);
            var opacity = active.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, 1);
            opacity.InsertKeyFrame(1, 0);

            active.StartAnimation("Opacity", opacity);

            Suspend(StoryPauseSource.Interaction);
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            var active = ElementCompositionPreview.GetElementVisual(ActiveRoot);
            var opacity = active.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, 0);
            opacity.InsertKeyFrame(1, 1);

            active.StartAnimation("Opacity", opacity);

            Resume(StoryPauseSource.Interaction);
        }

        private void MoreButton_Click(object sender, RoutedEventArgs e)
        {
            MoreClick?.Invoke(sender, new StoryEventArgs(ViewModel));
        }

        private void ShowSkeleton()
        {
            if (ActualSize.X == 0 || ActualSize.Y == 0)
            {
                return;
            }

            Logger.Debug("ShowSkeleton " + _viewModel.ChatId);

            var compositor = Window.Current.Compositor;
            var rectangle = compositor.CreateRoundedRectangleGeometry();
            rectangle.Size = new Vector2(ActualSize.X - 2, ActualSize.Y - 2);
            rectangle.Offset = new Vector2(1, 1);
            rectangle.CornerRadius = new Vector2(8);

            var stroke = compositor.CreateLinearGradientBrush();
            stroke.ColorStops.Add(compositor.CreateColorGradientStop(0.0f, Color.FromArgb(0x00, 0xff, 0xff, 0xff)));
            stroke.ColorStops.Add(compositor.CreateColorGradientStop(0.5f, Color.FromArgb(0x55, 0xff, 0xff, 0xff)));
            stroke.ColorStops.Add(compositor.CreateColorGradientStop(1.0f, Color.FromArgb(0x00, 0xff, 0xff, 0xff)));

            var fill = compositor.CreateLinearGradientBrush();
            fill.ColorStops.Add(compositor.CreateColorGradientStop(0.0f, Color.FromArgb(0x00, 0xff, 0xff, 0xff)));
            fill.ColorStops.Add(compositor.CreateColorGradientStop(0.5f, Color.FromArgb(0x22, 0xff, 0xff, 0xff)));
            fill.ColorStops.Add(compositor.CreateColorGradientStop(1.0f, Color.FromArgb(0x00, 0xff, 0xff, 0xff)));

            var shape = compositor.CreateSpriteShape();
            shape.Geometry = rectangle;
            shape.FillBrush = fill;
            shape.StrokeBrush = stroke;
            shape.StrokeThickness = 2;

            var visual = compositor.CreateShapeVisual();
            visual.Size = new Vector2(ActualSize.X, ActualSize.Y);
            visual.Shapes.Add(shape);

            var endless = compositor.CreateScalarKeyFrameAnimation();
            endless.InsertKeyFrame(0, -ActualSize.X);
            endless.InsertKeyFrame(1, +ActualSize.X);
            endless.IterationBehavior = AnimationIterationBehavior.Forever;
            endless.Duration = TimeSpan.FromMilliseconds(2000);

            stroke.StartAnimation("Offset.X", endless);
            fill.StartAnimation("Offset.X", endless);

            ElementCompositionPreview.SetElementChildVisual(ActiveRoot, visual);
        }

        private bool _loading;

        private void Texture_ImageOpened(object sender, RoutedEventArgs e)
        {
            Logger.Debug("ImageOpened " + _viewModel.ChatId);

            _loading = false;
            ElementCompositionPreview.SetElementChildVisual(ActiveRoot, Window.Current.Compositor.CreateSpriteVisual());

            Video?.Clear();

            if (_open)
            {
                _timer.Stop();
                _timer.Start();

                if (_viewModel?.SelectedItem != null && _viewModel.SelectedItem.ChatId != _openedChatId && _viewModel.SelectedItem.StoryId != _openedStoryId)
                {
                    _openedChatId = _viewModel.SelectedItem.ChatId;
                    _openedStoryId = _viewModel.SelectedItem.StoryId;
                    _viewModel.ClientService.Send(new OpenStory(_openedChatId, _openedStoryId));
                }
            }
        }

        internal void TryStart(StoryOpenOrigin ciccio, Windows.Foundation.Rect origin, bool show = true)
        {
            var transform = TransformToVisual(Window.Current.Content);
            var point = transform.TransformPoint(new Windows.Foundation.Point()).ToVector2();

            if (origin.IsEmpty && Window.Current.Content is FrameworkElement root)
            {
                origin = new Windows.Foundation.Rect(root.ActualWidth / 2, root.ActualHeight, 48, 48);
            }

            var reoffset = new Vector2((float)origin.X, (float)origin.Y);
            var resize = new Vector2((float)origin.Width, (float)origin.Height);
            var relativeX = reoffset.X - (point.X + 8);
            var relativeY = reoffset.Y - (point.Y + 18);

            var photo = ElementCompositionPreview.GetElementVisual(Photo);
            var layout = ElementCompositionPreview.GetElementVisual(LayoutRoot);
            var caption = ElementCompositionPreview.GetElementVisual(Caption.Parent as UIElement);
            var visual = ElementCompositionPreview.GetElementVisual(Content);
            ElementCompositionPreview.SetIsTranslationEnabled(Caption.Parent as UIElement, true);
            ElementCompositionPreview.SetIsTranslationEnabled(Content, true);

            if (ciccio == StoryOpenOrigin.ProfilePhoto)
            {
                visual.Properties.InsertVector3("Translation", new Vector3(relativeX, relativeY, 0));
                //layout.CenterPoint = new Vector3(8 + 16, 18 + 16, 0);
                layout.CenterPoint = new Vector3(8, 18, 0);
                layout.Scale = new Vector3(resize.X / (ActualSize.X - 8));

                photo.Scale = new Vector3(resize.X / 32f, resize.Y / 32f, 0);

                var compositor = Window.Current.Compositor;

                var rect = compositor.CreateRoundedRectangleGeometry();
                rect.Size = new Vector2(resize.X, resize.Y);
                rect.Offset = new Vector2(8, 18);
                rect.CornerRadius = resize / 2;

                visual.Clip = compositor.CreateGeometricClip(rect);

                var size = compositor.CreateVector2KeyFrameAnimation();
                size.InsertKeyFrame(show ? 0 : 1, new Vector2(resize.X, resize.Y));
                size.InsertKeyFrame(show ? 1 : 0, new Vector2(ActualSize.X, ActualSize.Y));
                //size.Duration = TimeSpan.FromSeconds(5);

                var offset = compositor.CreateVector2KeyFrameAnimation();
                offset.InsertKeyFrame(show ? 0 : 1, new Vector2(8, 18));
                offset.InsertKeyFrame(show ? 1 : 0, new Vector2(0, 0));
                //offset.Duration = TimeSpan.FromSeconds(5);

                var cornerRadius = compositor.CreateVector2KeyFrameAnimation();
                cornerRadius.InsertKeyFrame(show ? 0 : 1, new Vector2(resize.X / 2));
                cornerRadius.InsertKeyFrame(show ? 1 : 0, new Vector2(8, 8));
                //cornerRadius.Duration = TimeSpan.FromSeconds(5);

                var translation = compositor.CreateVector3KeyFrameAnimation();
                translation.InsertKeyFrame(show ? 0 : 1, new Vector3(relativeX, relativeY, 0));
                translation.InsertKeyFrame(show ? 1 : 0, new Vector3());
                //translation.Duration = TimeSpan.FromSeconds(5);

                var entranceY = compositor.CreateScalarKeyFrameAnimation();
                entranceY.InsertKeyFrame(show ? 0 : 1, -Caption.ActualSize.Y * 5);
                entranceY.InsertKeyFrame(show ? 1 : 0, 0);
                //entranceY.Duration = TimeSpan.FromSeconds(5);

                var scale = compositor.CreateVector3KeyFrameAnimation();
                scale.InsertKeyFrame(show ? 0 : 1, new Vector3(resize.X / 32f));
                scale.InsertKeyFrame(show ? 1 : 0, new Vector3(1));
                //scale.Duration = TimeSpan.FromSeconds(5);

                var scale2 = compositor.CreateVector3KeyFrameAnimation();
                scale2.InsertKeyFrame(show ? 0 : 1, new Vector3(resize.X / (ActualSize.X - 8)));
                scale2.InsertKeyFrame(show ? 1 : 0, new Vector3(1));
                //scale2.Duration = TimeSpan.FromSeconds(5);

                rect.StartAnimation("Size", size);
                rect.StartAnimation("Offset", offset);
                rect.StartAnimation("CornerRadius", cornerRadius);
                visual.StartAnimation("Translation", translation);
                caption.StartAnimation("Translation.Y", entranceY);
                layout.StartAnimation("Scale", scale2);
                photo.StartAnimation("Scale", scale);
            }
            else
            {
                visual.Properties.InsertVector3("Translation", new Vector3(relativeX + 8 + resize.X / 2 - ActualSize.X / 2, relativeY + 18 + resize.Y / 2 - ActualSize.Y / 2, 0));

                layout.CenterPoint = new Vector3(ActualSize / 2, 0);
                layout.Scale = new Vector3(resize.X / ActualSize.X, resize.X / ActualSize.X, 1);

                var compositor = Window.Current.Compositor;

                var rect = compositor.CreateRoundedRectangleGeometry();
                rect.Size = new Vector2(resize.X, resize.Y);
                rect.Offset = (ActualSize - rect.Size) / 2;
                rect.CornerRadius = ciccio == StoryOpenOrigin.Mention ? resize / 2 : new Vector2(4, 4);

                visual.Clip = compositor.CreateGeometricClip(rect);

                var size = compositor.CreateVector2KeyFrameAnimation();
                size.InsertKeyFrame(show ? 0 : 1, new Vector2(resize.X, resize.Y));
                size.InsertKeyFrame(show ? 1 : 0, new Vector2(ActualSize.X, ActualSize.Y));
                //size.Duration = TimeSpan.FromSeconds(5);

                var offset = compositor.CreateVector2KeyFrameAnimation();
                offset.InsertKeyFrame(show ? 0 : 1, (ActualSize - rect.Size) / 2);
                offset.InsertKeyFrame(show ? 1 : 0, new Vector2(0, 0));
                //offset.Duration = TimeSpan.FromSeconds(5);

                var cornerRadius = compositor.CreateVector2KeyFrameAnimation();
                cornerRadius.InsertKeyFrame(show ? 0 : 1, ciccio == StoryOpenOrigin.Mention ? resize / 2 : new Vector2(4, 4));
                cornerRadius.InsertKeyFrame(show ? 1 : 0, new Vector2(8, 8));
                //cornerRadius.Duration = TimeSpan.FromSeconds(5);

                var translation = compositor.CreateVector3KeyFrameAnimation();
                translation.InsertKeyFrame(show ? 0 : 1, new Vector3(relativeX + 8 + resize.X / 2 - ActualSize.X / 2, relativeY + 18 + resize.Y / 2 - ActualSize.Y / 2, 0));
                translation.InsertKeyFrame(show ? 1 : 0, new Vector3());
                //translation.Duration = TimeSpan.FromSeconds(5);

                var entranceY = compositor.CreateScalarKeyFrameAnimation();
                entranceY.InsertKeyFrame(show ? 0 : 1, -Caption.ActualSize.Y * 5);
                entranceY.InsertKeyFrame(show ? 1 : 0, 0);
                //entranceY.Duration = TimeSpan.FromSeconds(5);

                var scale2 = compositor.CreateVector3KeyFrameAnimation();
                scale2.InsertKeyFrame(show ? 0 : 1, new Vector3(resize.X / ActualSize.X, resize.X / ActualSize.X, 1));
                scale2.InsertKeyFrame(show ? 1 : 0, new Vector3(1));
                //scale2.Duration = TimeSpan.FromSeconds(5);

                rect.StartAnimation("Size", size);
                rect.StartAnimation("Offset", offset);
                rect.StartAnimation("CornerRadius", cornerRadius);
                //visual.StartAnimation("Translation.X", translationX);
                //visual.StartAnimation("Translation.Y", translationY);
                visual.StartAnimation("Translation", translation);
                caption.StartAnimation("Translation.Y", entranceY);
                layout.StartAnimation("Scale", scale2);
            }
        }

        private void Video_Initialized(object sender, LibVLCSharp.Platforms.Windows.InitializedEventArgs e)
        {
            // Generating plugins cache requires a breakpoint in bank.c#662
            _library = new LibVLC(e.SwapChainOptions); //"--quiet", "--reset-plugins-cache");
            //_library.Log += _library_Log;

            _player = new MediaPlayer(_library);
            _player.EnableHardwareDecoding = true;
            _player.ESSelected += OnESSelected;
            _player.Vout += OnVout;
            _player.Buffering += OnBuffering;
            _player.EndReached += OnEndReached;
            //_player.Stopped += OnStopped;

            //_player.FileCaching = 1;

            if (_mediaStream != null)
            {
                _playbackQueue.Enqueue(() => Play(_mediaStream));
            }
        }

        private void OnStopped(object sender, EventArgs e)
        {
            _dispatcherQueue.TryEnqueue(Video.Clear);
        }

        private void OnVout(object sender, MediaPlayerVoutEventArgs e)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                _loading = false;
                ElementCompositionPreview.SetElementChildVisual(ActiveRoot, Window.Current.Compositor.CreateSpriteVisual());

                Texture1.Source = null;
                Texture2.Source = null;
            });
        }

        private void OnESSelected(object sender, MediaPlayerESSelectedEventArgs e)
        {
            if (e.Type == TrackType.Video && e.Id != -1)
            {
                //_dispatcherQueue.TryEnqueue(UpdateStretch);
            }
            else if (e.Type == TrackType.Audio && e.Id != -1)
            {
                _dispatcherQueue.TryEnqueue(() => _player.Mute = _viewModel.Settings.VolumeMuted);
            }
        }

        private void UpdateStretch()
        {
            var videoTrack = GetVideoTrack(_player);
            if (videoTrack is not VideoTrack track)
            {
                return;
            }

            var trackWidth = track.Width;
            var trackHeight = track.Height;

            if (trackWidth == 0 || trackHeight == 0)
            {
                _player.Scale = 0;
            }
            else
            {
                if (track.SarNum != track.SarDen)
                {
                    trackWidth = trackWidth * track.SarNum / track.SarDen;
                }

                var width = (Video.ActualSize.X * XamlRoot.RasterizationScale) / trackWidth;
                var height = (Video.ActualSize.Y * XamlRoot.RasterizationScale) / trackHeight;

                _player.Scale = (float)Math.Max(width, height);
            }
        }

        private VideoTrack? GetVideoTrack(MediaPlayer mediaPlayer)
        {
            if (mediaPlayer == null)
            {
                return null;
            }
            var selectedVideoTrack = mediaPlayer.VideoTrack;
            if (selectedVideoTrack == -1)
            {
                return null;
            }

            try
            {
                var media = mediaPlayer.Media;
                MediaTrack? videoTrack = null;
                if (media != null)
                {
                    videoTrack = media.Tracks?.FirstOrDefault(t => t.Id == selectedVideoTrack);
                    media.Dispose();
                }
                return videoTrack == null ? (VideoTrack?)null : ((MediaTrack)videoTrack).Data.Video;
            }
            catch (Exception)
            {
                return null;
            }
        }


        private static readonly Regex _videoLooking = new("using (.*?) module \"(.*?)\" from (.*?)$", RegexOptions.Compiled);
        private static readonly object _syncObject = new();

        private void _library_Log(object sender, LogEventArgs e)
        {
            Debug.WriteLine(e.FormattedLog);

            lock (_syncObject)
            {
                var match = _videoLooking.Match(e.FormattedLog);
                if (match.Success)
                {
                    System.IO.File.AppendAllText(ApplicationData.Current.LocalFolder.Path + "\\vlc.txt", string.Format("{2}\n", match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value));
                }
            }
        }

        private void OnEndReached(object sender, EventArgs e)
        {
            _dispatcherQueue.TryEnqueue(() => Completed?.Invoke(this, EventArgs.Empty));
        }

        private void OnBuffering(object sender, MediaPlayerBufferingEventArgs e)
        {
            //Logger.Debug(e.Cache);

            _dispatcherQueue.TryEnqueue(() =>
            {
                if (e.Cache == 100 && _loading)
                {
                    _loading = false;
                    ElementCompositionPreview.SetElementChildVisual(ActiveRoot, Window.Current.Compositor.CreateSpriteVisual());

                    if (_viewModel?.SelectedItem != null && _viewModel.SelectedItem.ChatId != _openedChatId && _viewModel.SelectedItem.StoryId != _openedStoryId)
                    {
                        _openedChatId = _viewModel.SelectedItem.ChatId;
                        _openedStoryId = _viewModel.SelectedItem.StoryId;
                        _viewModel.ClientService.Send(new OpenStory(_openedChatId, _openedStoryId));
                    }
                }
                else if (e.Cache < 100 && !_loading)
                {
                    _loading = true;
                    ShowSkeleton();
                }
            });
        }

        private Image _texture;

        private LibVLC _library;
        private MediaPlayer _player;

        private StoryContentPhotoTimer _timer;
        private bool _paused;

        private StoryPauseSource _state;

        private void MutePlaceholder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                var window = element.Ancestors<StoriesWindow>().FirstOrDefault();
                window?.ShowTeachingTip(element, Strings.StoryNoSound, TeachingTipPlacementMode.BottomLeft);
            }
        }

        private void Privacy_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                string message;
                if (_viewModel.IsMyStory)
                {
                    message = _viewModel.SelectedItem?.PrivacySettings switch
                    {
                        StoryPrivacySettingsCloseFriends => Strings.CloseFriendsHintSelf,
                        StoryPrivacySettingsSelectedUsers => Strings.StorySelectedContactsHintSelf,
                        StoryPrivacySettingsContacts => Strings.StoryContactsHintSelf,
                        _ => string.Empty
                    };
                }
                else if (_viewModel.ClientService.TryGetUser(_viewModel.Chat, out User user))
                {
                    message = _viewModel.SelectedItem?.PrivacySettings switch
                    {
                        StoryPrivacySettingsCloseFriends => Strings.CloseFriendsHint,
                        StoryPrivacySettingsSelectedUsers => Strings.StorySelectedContactsHint,
                        StoryPrivacySettingsContacts => Strings.StoryContactsHint,
                        _ => string.Empty
                    };

                    message = string.Format(message, user.FirstName);
                }
                else
                {
                    return;
                }

                var window = element.Ancestors<StoriesWindow>().FirstOrDefault();
                window?.ShowTeachingTip(element, message, TeachingTipPlacementMode.BottomLeft);
            }
        }

        public void Suspend(StoryPauseSource source)
        {
            var none = _state == StoryPauseSource.None;

            _state |= source;

            if (none)
            {
                if (_type == StoryType.Video)
                {
                    _playbackQueue.Enqueue(() => _player?.SetPause(true));
                }
                else
                {
                    _timer.Pause();
                }

                Progress.Suspend();
            }
        }

        public void Resume(StoryPauseSource source)
        {
            _state &= ~source;

            if (_state == StoryPauseSource.None)
            {
                if (_type == StoryType.Video)
                {
                    _playbackQueue.Enqueue(() => _player?.SetPause(false));
                }
                else
                {
                    _timer.Start();
                }

                Progress.Resume();
            }
        }

        public void Toggle()
        {
            if (_state != StoryPauseSource.None)
            {
                Resume(StoryPauseSource.Interaction);
            }
            else
            {
                Suspend(StoryPauseSource.Interaction);
            }
        }

        private void Mute_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.Settings.VolumeMuted = Mute.IsChecked is false;

            if (_player != null)
            {
                _playbackQueue.Enqueue(() => _player.Mute = _viewModel.Settings.VolumeMuted);
            }
        }

        private void Caption_Click(object sender, RoutedEventArgs e)
        {
            Overflow.MaxLines = Overflow.MaxLines == 0 ? 1 : 0;
            ShowMore.Visibility = Overflow.MaxLines == 1 && Caption.HasOverflowContent
                ? Visibility.Visible
                : Visibility.Collapsed;

            Grid.SetRow(CaptionExpand, Overflow.MaxLines == 0 ? 0 : 1);

            CaptionOverlay.Visibility = Visibility.Visible;

            CaptionPanel.SizeChanged -= CaptionPanel_SizeChanged_1;
            CaptionPanel.SizeChanged += CaptionPanel_SizeChanged_1;

            if (Overflow.MaxLines == 0)
            {
                Suspend(StoryPauseSource.Caption);
            }
            else
            {
                Resume(StoryPauseSource.Caption);
            }
        }

        private void SizeMore_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            //CaptionPanel.ColumnDefinitions[0].MaxWidth = ActualWidth - ShowMore.ActualWidth;
            //CaptionPanel.ColumnDefinitions[1].MinWidth = ShowMore.ActualWidth;

            var visual = ElementCompositionPreview.GetElementVisual(ShowMore);
            ElementCompositionPreview.SetIsTranslationEnabled(ShowMore, true);

            var width = CaptionPanel.ActualSize.X - 24;
            var offset = width - ShowMore.ActualSize.X;

            var diff = offset - Overflow.ActualSize.X;

            visual.Properties.InsertVector3("Translation", new Vector3(-diff, 0, 0));
        }

        private void Overflow_LayoutUpdated(object sender, object e)
        {
            Overflow.LayoutUpdated -= Overflow_LayoutUpdated;

            var visual = ElementCompositionPreview.GetElementVisual(ShowMore);
            ElementCompositionPreview.SetIsTranslationEnabled(ShowMore, true);

            var width = CaptionPanel.ActualSize.X - 24;
            var offset = width - ShowMore.ActualSize.X;

            var diff = offset - Overflow.ActualSize.X;

            visual.Properties.InsertVector3("Translation", new Vector3(-diff, 0, 0));

            ShowMore.Visibility = Overflow.MaxLines == 1 && Caption.HasOverflowContent
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void CaptionPanel_SizeChanged_1(object sender, SizeChangedEventArgs e)
        {
            CaptionPanel.SizeChanged -= CaptionPanel_SizeChanged_1;
            ElementCompositionPreview.SetIsTranslationEnabled(CaptionPanel, true);

            var prev = e.PreviousSize.ToVector2();
            var next = e.NewSize.ToVector2();

            var overlay = ElementCompositionPreview.GetElementVisual(CaptionOverlay);
            var visual = ElementCompositionPreview.GetElementVisual(CaptionPanel);

            var opacity = visual.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, Overflow.MaxLines == 0 ? 0 : 1);
            opacity.InsertKeyFrame(0, Overflow.MaxLines == 0 ? 1 : 0);

            var translation = visual.Compositor.CreateScalarKeyFrameAnimation();
            translation.InsertKeyFrame(0, next.Y - prev.Y);
            translation.InsertKeyFrame(1, 0);

            overlay.StartAnimation("Opacity", opacity);
            visual.StartAnimation("Translation.Y", translation);
        }

        private void InactivePanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var visual = ElementCompositionPreview.GetElementVisual(MiniInside);
            visual.CenterPoint = new Vector3(MiniInside.ActualSize / 2, 0);
        }

        public void CopyLink(StoryViewModel story)
        {
            if (story.ClientService.TryGetUser(story.Chat, out User user) && user.HasActiveUsername(out string username))
            {
                MessageHelper.CopyLink(story.ClientService, new InternalLinkTypeStory(username, story.StoryId));
            }
        }

        private void Caption_TextEntityClick(object sender, TextEntityClickEventArgs e)
        {
            if (e.Type is TextEntityTypeBotCommand && e.Data is string command)
            {
                ViewModel.Delegate.SendBotCommand(command);
            }
            else if (e.Type is TextEntityTypeEmailAddress)
            {
                ViewModel.Delegate.OpenUrl("mailto:" + e.Data, false);
            }
            else if (e.Type is TextEntityTypePhoneNumber)
            {
                ViewModel.Delegate.OpenUrl("tel:" + e.Data, false);
            }
            else if (e.Type is TextEntityTypeHashtag or TextEntityTypeCashtag && e.Data is string hashtag)
            {
                ViewModel.Delegate.OpenHashtag(hashtag);
            }
            else if (e.Type is TextEntityTypeMention && e.Data is string username)
            {
                ViewModel.Delegate.OpenUsername(username);
            }
            else if (e.Type is TextEntityTypeMentionName mentionName)
            {
                ViewModel.Delegate.OpenUser(mentionName.UserId);
            }
            else if (e.Type is TextEntityTypeTextUrl textUrl)
            {
                ViewModel.Delegate.OpenUrl(textUrl.Url, true);
            }
            else if (e.Type is TextEntityTypeUrl && e.Data is string url)
            {
                ViewModel.Delegate.OpenUrl(url, false);
            }
            else if (e.Type is TextEntityTypeBankCardNumber && e.Data is string cardNumber)
            {
                ViewModel.Delegate.OpenBankCardNumber(cardNumber);
            }
            else if (e.Type is TextEntityTypeMediaTimestamp mediaTimestamp)
            {
                // Never happens here
            }
            else if (e.Type is TextEntityTypeCode or TextEntityTypePre or TextEntityTypePreCode && e.Data is string code)
            {
                MessageHelper.CopyText(code);
            }
            else if (e.Type is TextEntityTypeSpoiler)
            {
                Caption.IgnoreSpoilers = true;
            }
        }
    }

    public class StoryContentPhotoTimer
    {
        private readonly Stopwatch _watch;
        private readonly DispatcherTimer _timer;

        private readonly TimeSpan _interval;

        public StoryContentPhotoTimer()
        {
            _interval = TimeSpan.FromSeconds(5);

            _watch = new Stopwatch();

            _timer = new DispatcherTimer();
            _timer.Interval = _interval;
            _timer.Tick += OnTick;
        }

        public void Start()
        {
            _watch.Start();
            _timer.Start();
        }

        public void Pause()
        {
            _watch.Stop();
            _timer.Stop();

            _timer.Interval = _interval - _watch.Elapsed;
        }

        public void Stop()
        {
            _watch.Reset();

            _timer.Stop();
            _timer.Interval = TimeSpan.FromSeconds(5);
        }

        public event EventHandler Tick;

        private void OnTick(object sender, object e)
        {
            Stop();
            Tick?.Invoke(this, EventArgs.Empty);
        }
    }

    public class StoryProgress : Grid
    {
        private CompositionPropertySet _progressPropertySet;
        private AnimationController _progressController;

        public void Suspend()
        {
            _progressController?.Pause();
        }

        public void Resume()
        {
            _progressController?.Resume();
        }

        public void Update(int index, int count, double duration)
        {
            Children.Clear();
            ColumnDefinitions.Clear();

            for (int i = 0; i < count; i++)
            {
                var rectangle = new Rectangle
                {
                    Margin = new Thickness(0, 2, 2, 2),
                    Height = 2,
                    RadiusX = 1,
                    RadiusY = 1,
                    Fill = new SolidColorBrush(Windows.UI.Colors.White)
                    {
                        Opacity = i < index ? 1 : 0.3
                    }
                };

                if (i == index)
                {
                    var compositor = Window.Current.Compositor;

                    _progressPropertySet = compositor.CreatePropertySet();
                    _progressPropertySet.InsertScalar("Progress", 0);

                    var ellipse = compositor.CreateRoundedRectangleGeometry();
                    ellipse.CornerRadius = new Vector2(1);
                    ellipse.Size = new Vector2(20, 2);

                    var shape2 = compositor.CreateSpriteShape();
                    shape2.Geometry = ellipse;
                    shape2.FillBrush = compositor.CreateColorBrush(Windows.UI.Colors.White);

                    var visual = compositor.CreateShapeVisual();
                    visual.Shapes.Add(shape2);
                    visual.Size = new Vector2(20, 2);

                    rectangle.SizeChanged += (s, args) =>
                    {
                        visual.Size = args.NewSize.ToVector2();
                    };

                    ElementCompositionPreview.SetElementChildVisual(rectangle, visual);

                    ExpressionAnimation expression = compositor.CreateExpressionAnimation();
                    expression.SetReferenceParameter("_", _progressPropertySet);
                    expression.SetReferenceParameter("V", visual);
                    expression.Expression = "Vector2(_.Progress * V.Size.X, 2)";

                    // Apply the expression to the point visual's Offset property
                    ellipse.StartAnimation("Size", expression);

                    // Start the animation by incrementing the progress value
                    var easing = compositor.CreateLinearEasingFunction();
                    var compositorAnimation = compositor.CreateScalarKeyFrameAnimation();
                    compositorAnimation.InsertKeyFrame(1.0f, 1.0f, easing);
                    compositorAnimation.Duration = TimeSpan.FromSeconds(duration); // Adjust duration as needed

                    _progressPropertySet.StartAnimation("Progress", compositorAnimation);
                    _progressController = _progressPropertySet.TryGetAnimationController("Progress");
                }

                ColumnDefinitions.Add(new ColumnDefinition());
                Grid.SetColumn(rectangle, i);

                Children.Add(rectangle);
            }


        }
    }
}
