//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Controls.Messages;
using Telegram.Controls.Stories.Widgets;
using Telegram.Converters;
using Telegram.Native;
using Telegram.Native.Calls;
using Telegram.Native.Composition;
using Telegram.Native.Media;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Services.Calls;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Stories;
using Telegram.Views.Popups;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls.Stories
{
    public partial class StoryEventArgs : EventArgs
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

    // TODO: Live story no stream state
    public sealed partial class StoryContent : UserControlEx
    {
        private volatile bool _unloaded;

        private readonly LayerVisual _messagesVisual;

        public StoryContent()
        {
            InitializeComponent();

            _texture = Texture1;

            _timer = new StoryContentPhotoTimer();
            _timer.Tick += OnTick;

            _messagesVisual = CompositionDevice.GetElementLayerVisual(MessagesHost);

            SizeChanged += OnSizeChanged;
            Disconnected += OnUnloaded;
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

            if (_player != null)
            {
                _player.VideoOut -= OnVout;
                _player.Buffering -= OnBuffering;
                _player.EndReached -= OnEndReached;
                _player.Close();
            }

            DiscardGroupCall();
        }

        private void DiscardGroupCall()
        {
            if (_unifiedVideo != null)
            {
                _unifiedVideo.FrameReceived -= OnFrameReceived;
                _unifiedVideo.Stop();
                _unifiedVideo = null;
            }

            if (_call != null)
            {
                _call.NetworkStateChanged -= OnNetworkStateChanged;
                _call.MessagesChanged -= OnMessagesChanged;
                _call.PinnedMessagesChanged -= OnPinnedMessagesChanged;
                _call.ReactionsChanged -= OnReactionsChanged;
                _call.TopDonorsChanged -= OnTopDonorsChanged;
                _call.StreamerChanged -= OnStreamerChanged;
                _call.PropertyChanged -= OnPropertyChanged;
                _call.Discard();
                _call = null;
            }
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
            var root = ElementComposition.GetElementVisual(Content);
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
            Logger.Info("Start");

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

                    PhotoMini.Source = ProfilePictureSource.User(activeStories.ClientService, user);
                    Photo.Source = ProfilePictureSource.User(activeStories.ClientService, user);

                    if (activeStories.SelectedItem?.Content is StoryContentLive && user.ProfilePhoto != null)
                    {
                        BackgroundPhoto.SetSource(activeStories.ClientService, user.ProfilePhoto.Big, user.ProfilePhoto.Minithumbnail, blurRadius: 15);
                    }
                    else
                    {
                        BackgroundPhoto.Clear();
                    }
                }
                else
                {
                    TitleMini.Text = chat.Title;
                    Title.Text = chat.Title;

                    Identity.SetStatus(activeStories.ClientService, chat);

                    PhotoMini.Source = ProfilePictureSource.Chat(activeStories.ClientService, chat);
                    Photo.Source = ProfilePictureSource.Chat(activeStories.ClientService, chat);

                    if (activeStories.SelectedItem?.Content is StoryContentLive && chat.Photo != null)
                    {
                        BackgroundPhoto.SetSource(activeStories.ClientService, chat.Photo.Big, chat.Photo.Minithumbnail, blurRadius: 15);
                    }
                    else
                    {
                        BackgroundPhoto.Clear();
                    }
                }

                if (activeStories.Item != null)
                {
                    SegmentsInactive.UpdateActiveStories(activeStories.Item, 48, true);
                }

                _player?.Context.Clear();
            }

            var story = activeStories.SelectedItem;
            if (story != null && story.Id != _storyId)
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

            var root = ElementComposition.GetElementVisual(Content);
            var mini = ElementComposition.GetElementVisual(MiniInside);

            var inactive = ElementComposition.GetElementVisual(InactiveRoot);
            var active = ElementComposition.GetElementVisual(ActiveRoot);

            var visual = ElementComposition.GetElementVisual(MiniInside);
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

            if (story == null || _unloaded)
            {
                return;
            }

            if (open && (story.Id != _storyId || !_open))
            {
                if (story.PosterChatId != _openedChatId || story.Id != _openedStoryId)
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
            _storyId = story.Id;
        }

        public void UpdateQuality()
        {
            var story = ViewModel?.SelectedItem;
            if (story != null)
            {
                Activate(story);
            }
        }

        private void Update(StoryViewModel story)
        {
            if (story.Content is StoryContentLive && story.GroupCall != null)
            {
                MessagesRoot.Visibility = Visibility.Visible;
                LiveBadge.Visibility = Visibility.Visible;
                Subtitle.Text = Locale.Declension(Strings.R.LiveStoryWatching, story.GroupCall.ParticipantCount);

            }
            else
            {
                MessagesRoot.Visibility = Visibility.Collapsed;
                LiveBadge.Visibility = Visibility.Collapsed;
                Subtitle.Text = story.Date != 0
                    ? Locale.FormatRelativeShort(story.Date)
                    : string.Format("{0}/{1}", 1 + ViewModel.Items.IndexOf(story), ViewModel.Items.Count);
            }

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
                Privacy.Visibility = story.PrivacySettings is StoryPrivacySettingsEveryone or null
                    ? Visibility.Collapsed
                    : Visibility.Visible;

            if (story.Content is StoryContentPhoto photoContent)
            {
                var file = photoContent.Photo.GetBig();
                if (file != null)
                {
                    UpdatePhoto(story, file.Photo, true);
                }

                var thumbnail = photoContent.Photo.GetSmall();
                UpdateThumbnail(story, thumbnail?.Photo, photoContent.Photo.Minithumbnail, true);

                Mute.Visibility = Visibility.Collapsed;
                MutePlaceholder.Visibility = Visibility.Collapsed;
            }
            else if (story.Content is StoryContentVideo videoContent)
            {
                var video = SelectVideoFile(videoContent);

                var thumbnail = video.Thumbnail;
                UpdateThumbnail(story, thumbnail?.File, video.Minithumbnail, true);
                UpdateVideo(story, /*video.AlternativeVideo?.Video ??*/ video.Video, true);

                Mute.Visibility = Visibility.Visible;
                MutePlaceholder.Visibility = video.IsAnimation
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                Mute.IsEnabled = !video.IsAnimation;
                Mute.IsChecked = !video.IsAnimation && !_viewModel.Settings.VolumeMuted;
            }
            else if (story.Content is StoryContentLive)
            {
                Mute.Visibility = Visibility.Collapsed;
                MutePlaceholder.Visibility = Visibility.Collapsed;
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
                bool autoSize = true;

                if (area.Type is StoryAreaTypeSuggestedReaction suggestedReaction)
                {
                    autoSize = false;

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

                    VisualUtilities.DropShadow(path, target: shadow);

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
                else if (area.Type is StoryAreaTypeWeather weather)
                {
                    element = new StoryWeatherWidget(weather, new CornerRadius(area.Position.CornerRadiusPercentage / 100 * ActualWidth));
                }
                else
                {
                    var button = new HyperlinkButton();
                    button.Click += Area_Click;

                    element = button;
                }

                if (autoSize)
                {
                    element.Width = area.Position.WidthPercentage / 100 * ActualWidth;
                    element.Height = area.Position.HeightPercentage / 100 * ActualHeight;
                    element.RenderTransformOrigin = new Point(0.5, 0.5);
                    element.RenderTransform = new RotateTransform
                    {
                        Angle = area.Position.RotationAngle
                    };

                    if (element is Control control)
                    {
                        control.CornerRadius = new CornerRadius(area.Position.CornerRadiusPercentage / 100 * ActualWidth);
                    }
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

        private async void Area_Click(object sender, RoutedEventArgs e)
        {
            if (sender is HyperlinkButton element && element.Tag is StoryArea area)
            {
                var target = element.Content as FrameworkElement ?? element;

                if (area.Type is StoryAreaTypeLocation or StoryAreaTypeVenue)
                {
                    var text = new TextBlock();
                    text.Inlines.Add(new Run
                    {
                        Text = Strings.StoryViewLocation,
                        Foreground = new SolidColorBrush(Colors.White)
                    });

                    var window = element.GetParent<StoriesWindow>();
                    var result = await window?.ShowActionAsync(target, text, TeachingTipPlacementMode.Top);

                    if (result == ContentDialogResult.Primary)
                    {
                        try
                        {
                            Location location = area.Type switch
                            {
                                StoryAreaTypeLocation typeLocation => typeLocation.Location,
                                StoryAreaTypeVenue typeVenue => typeVenue.Venue.Location,
                                _ => null
                            };

                            await Windows.System.Launcher.LaunchUriAsync(new Uri(string.Format(CultureInfo.InvariantCulture, "https://www.google.com/maps/search/?api=1&query={0},{1}", location.Latitude, location.Longitude)));
                        }
                        catch
                        {
                            // All the remote procedure calls must be wrapped in a try-catch block
                        }
                    }
                }
                else if (area.Type is StoryAreaTypeMessage typeMessage)
                {
                    var text = new TextBlock();
                    text.Inlines.Add(new Run
                    {
                        Text = Strings.StoryViewMessage,
                        Foreground = new SolidColorBrush(Colors.White)
                    });

                    var window = element.GetParent<StoriesWindow>();
                    var result = await window?.ShowActionAsync(target, text, TeachingTipPlacementMode.Top);

                    if (result == ContentDialogResult.Primary)
                    {
                        ViewModel.NavigationService.NavigateToChat(typeMessage.ChatId, message: typeMessage.MessageId);
                    }
                }
                else if (area.Type is StoryAreaTypeLink typeLink)
                {
                    var text = new TextBlock();
                    text.Inlines.Add(new Run
                    {
                        Text = Strings.StoryOpenLink,
                        Foreground = new SolidColorBrush(Colors.White)
                    });
                    text.Inlines.Add(new LineBreak());
                    text.Inlines.Add(new Run
                    {
                        Text = typeLink.Url,
                        FontSize = 12,
                        FontWeight = FontWeights.Normal,
                        Foreground = BootStrapper.Current.Resources["SystemControlDisabledChromeDisabledLowBrush"] as Brush
                    });

                    var window = element.GetParent<StoriesWindow>();
                    var result = await window?.ShowActionAsync(target, text, TeachingTipPlacementMode.Top);

                    if (result == ContentDialogResult.Primary)
                    {
                        MessageHelper.OpenUrl(ViewModel.ClientService, ViewModel.NavigationService, typeLink.Url);
                    }
                }
                else if (area.Type is StoryAreaTypeUpgradedGift typeUpgradedGift)
                {
                    var text = new TextBlock();
                    text.Inlines.Add(new Run
                    {
                        Text = Strings.StoryViewGift,
                        Foreground = new SolidColorBrush(Colors.White)
                    });

                    var window = element.GetParent<StoriesWindow>();
                    var result = await window?.ShowActionAsync(target, text, TeachingTipPlacementMode.Top);

                    if (result == ContentDialogResult.Primary)
                    {
                        MessageHelper.OpenTelegramUrl(ViewModel.ClientService, ViewModel.NavigationService, new InternalLinkTypeUpgradedGift(typeUpgradedGift.GiftName));
                    }
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

            var root = ElementComposition.GetElementVisual(Content);
            var mini = ElementComposition.GetElementVisual(MiniInside);

            var inactive = ElementComposition.GetElementVisual(InactiveRoot);
            var active = ElementComposition.GetElementVisual(ActiveRoot);

            var visual = ElementComposition.GetElementVisual(MiniInside);
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

            var rect1 = CanvasGeometry.CreateRoundedRectangle(null, 0, 0, ActualSize.X, ActualSize.Y, 8, 8);
            var rect2 = CanvasGeometry.CreateRoundedRectangle(null, 0, 0, ActualSize.X, ActualSize.Y, 8 * 2.5f, 8 * 2.5f);

            var geometry1 = compositor.CreatePathGeometry(new CompositionPath(rect1));
            var clip1 = compositor.CreateGeometricClip(geometry1);

            var pathAnimation = compositor.CreatePathKeyFrameAnimation();
            pathAnimation.InsertKeyFrame(to == 3 ? 1 : 0, new CompositionPath(rect1));
            pathAnimation.InsertKeyFrame(to == 3 ? 0 : 1, new CompositionPath(rect2));

            geometry1.StartAnimation("Path", pathAnimation);
            root.Clip = clip1;
        }

        private void Play(StoryVideo stream)
        {
            if (_player == null || Video == null)
            {
                InitializeVideo();
            }

            if (_player != null && !_unloaded && _viewModel != null && stream != null)
            {
                _player.Play(new RemoteFileSource(_viewModel.ClientService, stream.Video));
            }
        }

        private VoipGroupCall _call;
        private ObservableCollection<GroupCallMessage> _messages;
        private SortedObservableCollection<GroupCallMessage> _pinnedMessages;
        private Dictionary<MessageSender, int> _topDonors;

        private void Activate(StoryViewModel story)
        {
            CollapseCaption();
            DiscardGroupCall();

            if (story.Content is StoryContentVideo videoContent && !_unloaded)
            {
                var video = SelectVideoFile(videoContent);

                Progress.Update(_viewModel.Items.IndexOf(_viewModel.SelectedItem), _viewModel.Items.Count, video.Duration);
                Play(video);
            }
            else if (story.Content is StoryContentLive live && story.ClientService.TryGetGroupCall(live.GroupCallId, out GroupCall groupCall) && !_unloaded)
            {
                _timer.Stop();
                Progress.Update(-1, 1, 0);

                Suspend(StoryPauseSource.Live);

                _call = new VoipGroupCall(ViewModel.ClientService, ViewModel.Settings, ViewModel.Aggregator, XamlRoot, ViewModel.Chat, groupCall, null, null, true);
                _call.NetworkStateChanged += OnNetworkStateChanged;
                _call.MessagesChanged += OnMessagesChanged;
                _call.PinnedMessagesChanged += OnPinnedMessagesChanged;
                _call.ReactionsChanged += OnReactionsChanged;
                _call.TopDonorsChanged += OnTopDonorsChanged;
                _call.StreamerChanged += OnStreamerChanged;
                _call.PropertyChanged += OnPropertyChanged;

                if (_call.IsRtmpStream)
                {
                    _call.AddIncomingVideoOutput("unified", _unifiedVideo = VoipVideoOutput.CreateSink(LayoutRoot, uniformToFill: true));
                    _unifiedVideo.FrameReceived += OnFrameReceived;
                }
                else
                {
                    UpdateStreamer(_call.Streamer);
                }

                story.GroupCall = _call;

                _topDonors = new Dictionary<MessageSender, int>(new MessageSenderEqualityComparer());

                _messages = new ObservableCollection<GroupCallMessage>(_call.Messages);
                MessagesHost.ItemsSource = _messages;

                _pinnedMessages = new SortedObservableCollection<GroupCallMessage>(_call.PinnedMessages, new GroupCallMessageComparer());
                PinnedMessagesHost.ItemsSource = _pinnedMessages;

                ShowSkeleton();
            }
            else if (!_loading && !_unloaded)
            {
                _timer.Stop();
                _timer.Start();
                Progress.Update(_viewModel.Items.IndexOf(_viewModel.SelectedItem), _viewModel.Items.Count, 5);
            }
        }

        private void OnFrameReceived(VoipVideoOutputSink sender, FrameReceivedEventArgs args)
        {
            _unifiedVideo?.FrameReceived -= OnFrameReceived;

            this.BeginOnUIThread(HideSkeleton);
        }

        private VoipVideoOutputSink _unifiedVideo;

        private void OnNetworkStateChanged(VoipGroupCall sender, VoipGroupCallNetworkStateChangedEventArgs args)
        {
            // TODO
        }

        private void OnMessagesChanged(VoipGroupCall sender, VoipGroupCallMessagesChangedEventArgs args)
        {
            this.BeginOnUIThread(() => UpdateMessages(args.Message, args.Deleted));
        }

        private void OnPinnedMessagesChanged(VoipGroupCall sender, VoipGroupCallMessagesChangedEventArgs args)
        {
            this.BeginOnUIThread(() => UpdatePinnedMessages(args.Message, args.Deleted));
        }

        private void OnReactionsChanged(VoipGroupCall sender, VoipGroupCallReactionsChangedEventArgs args)
        {
            this.BeginOnUIThread(() => ReactionsHost.Add(sender.ClientService, args.SenderId, args.StarCount));
        }

        private void OnTopDonorsChanged(VoipGroupCall sender, VoipGroupCallTopDonorsChangedEventArgs args)
        {
            this.BeginOnUIThread(() => UpdateTopDonors(args.Donors));
        }

        private void OnStreamerChanged(VoipGroupCall sender, VoipGroupCallStreamerChangedEventArgs args)
        {
            this.BeginOnUIThread(() => UpdateStreamer(args.Streamer));
        }

        private void UpdateStreamer(GroupCallParticipant participant)
        {
            if (_unifiedVideo != null)
            {
                _unifiedVideo.FrameReceived -= OnFrameReceived;
                _unifiedVideo.Stop();
                _unifiedVideo = null;
            }

            if (participant?.VideoInfo != null)
            {
                var channelInfo = new VoipVideoChannelInfo(participant.AudioSourceId, participant.ParticipantId.ToId(), participant.VideoInfo.EndpointId, participant.VideoInfo.SourceGroups.ToCalls(), VoipVideoChannelQuality.Full, VoipVideoChannelQuality.Full);

                _call.SetRequestedVideoChannels([channelInfo]);
                _call.AddIncomingVideoOutput(channelInfo.EndpointId, _unifiedVideo = VoipVideoOutput.CreateSink(LayoutRoot, uniformToFill: true));

                _unifiedVideo.FrameReceived += OnFrameReceived;
            }
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is VoipGroupCall groupCall && e.PropertyName == nameof(Call))
            {
                this.BeginOnUIThread(() =>
                {
                    Subtitle.Text = Locale.Declension(Strings.R.LiveStoryWatching, groupCall.ParticipantCount);
                });
            }
        }

        private void UpdateMessages(GroupCallMessage message, bool expired)
        {
            if (expired)
            {
                _messages.Remove(message);
            }
            else
            {
                _messages.Add(message);

                //if (message.PaidMessageStarCount > 0)
                //{
                //    ReactionsHost.Add(ViewModel.ClientService, message.SenderId, message.PaidMessageStarCount);
                //}
            }
        }

        private void UpdatePinnedMessages(GroupCallMessage message, bool expired)
        {
            if (expired)
            {
                _pinnedMessages.Remove(message);
            }
            else
            {
                _pinnedMessages.Add(message);
            }
        }

        private void UpdateTopDonors(IList<PaidReactor> donors)
        {
            var diff = new Dictionary<MessageSender, int>(_topDonors.Count + donors.Count, new MessageSenderEqualityComparer());

            foreach (var donor in _topDonors)
            {
                diff[donor.Key] = 0;
            }

            for (int i = 0; i < donors.Count; i++)
            {
                var donor = donors[i].SenderId;
                var newPosition = i + 1;

                if (_topDonors.TryGetValue(donor, out int oldPosition))
                {
                    if (oldPosition == newPosition)
                    {
                        diff.Remove(donor);
                    }
                    else
                    {
                        diff[donor] = newPosition;
                    }
                }
                else
                {
                    diff[donor] = newPosition;
                }
            }

            _topDonors.Clear();

            for (int i = 0; i < donors.Count; i++)
            {
                _topDonors[donors[i].SenderId] = i + 1;
            }

            MessagesHost.ForEach<LiveStoryMessageCell, GroupCallMessage>((content, message) =>
            {
                if (diff.TryGetValue(message.SenderId, out int position))
                {
                    content.Update(ViewModel.ClientService, message, position);
                }
            });

            PinnedMessagesHost.ForEach<LiveStoryPinnedMessageCell, GroupCallMessage>((content, message) =>
            {
                if (diff.TryGetValue(message.SenderId, out int position))
                {
                    content.Update(ViewModel.ClientService, message, position);
                }
            });
        }

        private void Deactivate(StoryViewModel story)
        {
            _player?.Stop();

            //UnloadVideo();
            DiscardGroupCall();
            CollapseCaption();

            if (_openedChatId == story.PosterChatId && _openedStoryId == story.Id)
            {
                _viewModel.ClientService.Send(new CloseStory(story.PosterChatId, story.Id));
                _openedChatId = 0;
                _openedStoryId = 0;
            }
        }

        private void Photo_Click(object sender, RoutedEventArgs e)
        {
            var parent = this.GetParent<StoriesWindow>();
            if (parent != null)
            {
                parent.TryHide(ContentDialogResult.Primary);
                parent.ViewModel.NavigationService.NavigateToChat(ViewModel.Chat);
            }
        }



        private ThumbnailController _thumbnailController;

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

        private StoryVideo SelectVideoFile(StoryContentVideo video)
        {
            //if (video.AlternativeVideo == null || (SettingsService.Current.Playback.HighQuality && _viewModel.IsPremium))
            {
                return video.Video;
            }

            return video.AlternativeVideo;
        }

        private void UpdateThumbnail(StoryViewModel story, File file, Minithumbnail minithumbnail, bool download)
        {
            if (file?.Id == _thumbnailId && download)
            {
                return;
            }

            _thumbnailId = file?.Id ?? 0;
            _thumbnailController ??= new ThumbnailController(ThumbnailTexture);

            if (file != null)
            {
                if (file.Local.IsDownloadingCompleted)
                {
                    _thumbnailController.Blur(file.Local.Path, 3, 0);
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
                        _thumbnailController.Blur(minithumbnail.Data, 3, 0);
                    }
                    else
                    {
                        _thumbnailController.Recycle();
                    }
                }
            }
            else if (minithumbnail != null)
            {
                _thumbnailController.Blur(minithumbnail.Data, 3, 0);
            }
            else
            {
                _thumbnailController.Recycle();
            }
        }

        private void UpdatePhoto(StoryViewModel story, File file, bool download)
        {
            if (file.Id == _fileId && download)
            {
                return;
            }

            UpdateManager.Unsubscribe(this, ref _fileToken);

            _fileId = file.Id;
            Logger.Info();

            _player?.Stop();

            if (_type == StoryType.Photo)
            {
                _player?.Context.Clear();
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

        private StoryType _type;

        private int _fileId;
        private int _thumbnailId;

        private void UpdateVideo(StoryViewModel story, File file, bool download)
        {
            if (file.Id == _fileId && download)
            {
                return;
            }

            UpdateManager.Unsubscribe(this, ref _fileToken);

            _fileId = file.Id;
            //Player.Source = null;

            Logger.Info();

            //_player?.Stop();

            //if (_mediaStream != null)
            //{
            //    _mediaStream.Dispose();
            //    _mediaStream = null;
            //}

            var videoContent = story.Content as StoryContentVideo;
            var video = SelectVideoFile(videoContent);

            //_loading = true;
            //ShowSkeleton();

            var prev = _texture == Texture1 ? Texture2 : Texture1;
            var next = _texture;

            Canvas.SetZIndex(VideoPanel, 1);
            Canvas.SetZIndex(next, 0);
            Canvas.SetZIndex(prev, -1);

            // Preloaded?
            if (file.Local.DownloadedPrefixSize >= video.PreloadPrefixSize)
            {
                if (_type == StoryType.Photo && Video != null)
                {
                    _player?.Context.Clear();
                }
            }
            else
            {
                _player?.Context.Clear();
            }

            story.ClientService.DownloadFile(file.Id, 32, 0, video.PreloadPrefixSize);

            _type = StoryType.Video;
        }

        private bool _hidden = false;

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(this);
            if (point.Properties.IsLeftButtonPressed)
            {
                _hidden = true;

                var active = ElementComposition.GetElementVisual(ActiveRoot);
                var opacity = active.Compositor.CreateScalarKeyFrameAnimation();
                opacity.InsertKeyFrame(0, 1);
                opacity.InsertKeyFrame(1, 0);

                active.StartAnimation("Opacity", opacity);

                Suspend(StoryPauseSource.Interaction);
            }
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_hidden)
            {
                _hidden = false;
                var active = ElementComposition.GetElementVisual(ActiveRoot);
                var opacity = active.Compositor.CreateScalarKeyFrameAnimation();
                opacity.InsertKeyFrame(0, 0);
                opacity.InsertKeyFrame(1, 1);

                active.StartAnimation("Opacity", opacity);

                Resume(StoryPauseSource.Interaction);
            }
        }

        private void MoreButton_Click(object sender, RoutedEventArgs e)
        {
            MoreClick?.Invoke(sender, new StoryEventArgs(ViewModel));
        }

        private void ShowSkeleton()
        {
            _loading = true;

            if (ActualSize.X == 0 || ActualSize.Y == 0)
            {
                return;
            }

            Logger.Debug("ShowSkeleton " + _viewModel.ChatId);

            var compositor = BootStrapper.Current.Compositor;
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

        private void HideSkeleton()
        {
            _loading = false;
            ElementCompositionPreview.SetElementChildVisual(ActiveRoot, BootStrapper.Current.Compositor.CreateSpriteVisual());
        }

        private bool _loading;

        private void Texture_ImageOpened(object sender, RoutedEventArgs e)
        {
            Logger.Debug("ImageOpened " + _viewModel.ChatId);

            HideSkeleton();

            _player?.Context.Clear();

            if (_open)
            {
                _timer.Stop();
                _timer.Start();

                if (_viewModel?.SelectedItem != null && _viewModel.SelectedItem.PosterChatId != _openedChatId && _viewModel.SelectedItem.Id != _openedStoryId)
                {
                    _openedChatId = _viewModel.SelectedItem.PosterChatId;
                    _openedStoryId = _viewModel.SelectedItem.Id;
                    _viewModel.ClientService.Send(new OpenStory(_openedChatId, _openedStoryId));
                }
            }
        }

        public void TryStart(StoryOpenOrigin ciccio, Windows.Foundation.Rect origin, bool show = true)
        {
            var transform = TransformToVisual(null);
            var point = transform.TransformVector2();

            if (origin.IsEmpty && XamlRoot.Content is FrameworkElement root)
            {
                origin = new Windows.Foundation.Rect(root.ActualWidth / 2, root.ActualHeight, 48, 48);
            }

            var reoffset = new Vector2((float)origin.X, (float)origin.Y);
            var resize = new Vector2((float)origin.Width, (float)origin.Height);
            var relativeX = reoffset.X - (point.X + 8);
            var relativeY = reoffset.Y - (point.Y + 18);

            var photo = ElementComposition.GetElementVisual(Photo);
            var layout = ElementComposition.GetElementVisual(LayoutRoot);
            var caption = ElementComposition.GetElementVisual(CaptionPanel);
            var visual = ElementComposition.GetElementVisual(Content);
            ElementCompositionPreview.SetIsTranslationEnabled(CaptionPanel, true);
            ElementCompositionPreview.SetIsTranslationEnabled(Content, true);

            if (ciccio == StoryOpenOrigin.ProfilePhoto)
            {
                visual.Properties.InsertVector3("Translation", new Vector3(relativeX, relativeY, 0));
                //layout.CenterPoint = new Vector3(8 + 16, 18 + 16, 0);
                layout.CenterPoint = new Vector3(8, 18, 0);
                layout.Scale = new Vector3(resize.X / (ActualSize.X - 8));

                photo.Scale = new Vector3(resize.X / 32f, resize.Y / 32f, 0);

                var compositor = BootStrapper.Current.Compositor;

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

                var compositor = BootStrapper.Current.Compositor;

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

        private void InitializeVideo()
        {
            if (_unloaded)
            {
                return;
            }

            Logger.Info();

            FindName(nameof(Video));

            var options = new AsyncMediaPlayerOptions
            {
                CreateSwapChain = true,
                Mute = _viewModel.Settings.VolumeMuted,
                Volume = 1,
                Debug = SettingsService.Current.VerbosityLevel >= 4,
            };

            _player = new AsyncMediaPlayer(options);
            _player.VideoOut += OnVout;
            _player.Buffering += OnBuffering;
            _player.EndReached += OnEndReached;

            _player.Context.Attach(Video, true);
        }

        private void OnVout(AsyncMediaPlayer sender, object e)
        {
            HideSkeleton();

            Texture1.Source = null;
            Texture2.Source = null;
        }

        private void OnEndReached(AsyncMediaPlayer sender, object e)
        {
            Completed?.Invoke(this, EventArgs.Empty);
        }

        private void OnBuffering(AsyncMediaPlayer sender, AsyncMediaPlayerBufferingEventArgs e)
        {
            //Logger.Debug(e.Cache);

            if (e.Cache == 100 && _loading)
            {
                HideSkeleton();

                if (_viewModel?.SelectedItem != null && _viewModel.SelectedItem.PosterChatId != _openedChatId && _viewModel.SelectedItem.Id != _openedStoryId)
                {
                    _openedChatId = _viewModel.SelectedItem.PosterChatId;
                    _openedStoryId = _viewModel.SelectedItem.Id;
                    _viewModel.ClientService.Send(new OpenStory(_openedChatId, _openedStoryId));
                }
            }
            else if (e.Cache < 100 && !_loading)
            {
                ShowSkeleton();
            }
        }

        private Image _texture;

        private AsyncMediaPlayer _player;

        private StoryContentPhotoTimer _timer;
        private bool _paused;

        private StoryPauseSource _state;

        private void MutePlaceholder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                var window = element.GetParent<StoriesWindow>();
                window?.ShowToast(element, Strings.StoryNoSound, TeachingTipPlacementMode.BottomLeft);
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

                var window = element.GetParent<StoriesWindow>();
                window?.ShowToast(element, message, TeachingTipPlacementMode.BottomLeft);
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
                    _player?.Pause(true);
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
                    _player?.Pause(false);
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

            _player?.Mute = _viewModel.Settings.VolumeMuted;
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

            var visual = ElementComposition.GetElementVisual(ShowMore);
            ElementCompositionPreview.SetIsTranslationEnabled(ShowMore, true);

            var width = CaptionPanel.ActualSize.X - 24;
            var offset = width - ShowMore.ActualSize.X;

            var diff = offset - Overflow.ActualSize.X;

            visual.Properties.InsertVector3("Translation", new Vector3(-diff, 0, 0));
        }

        private void Overflow_LayoutUpdated(object sender, object e)
        {
            Overflow.LayoutUpdated -= Overflow_LayoutUpdated;

            var visual = ElementComposition.GetElementVisual(ShowMore);
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

            var overlay = ElementComposition.GetElementVisual(CaptionOverlay);
            var visual = ElementComposition.GetElementVisual(CaptionPanel);

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
            var visual = ElementComposition.GetElementVisual(MiniInside);
            visual.CenterPoint = new Vector3(MiniInside.ActualSize / 2, 0);
        }

        public void CopyLink(StoryViewModel story)
        {
            if (story.ClientService.TryGetUser(story.Chat, out User user) && user.HasActiveUsername(out string username))
            {
                MessageHelper.CopyLink(story.ClientService, XamlRoot, new InternalLinkTypeStory(username, story.Id));
            }
        }

        private void Caption_TextEntityClick(object sender, TextEntityClickEventArgs e)
        {
            if (e.Type is TextEntityTypeBotCommand && e.Text is string command)
            {
                ViewModel.Delegate.SendBotCommand(command);
            }
            else if (e.Type is TextEntityTypeEmailAddress)
            {
                ViewModel.Delegate.OpenUrl("mailto:" + e.Text, false);
            }
            else if (e.Type is TextEntityTypePhoneNumber)
            {
                ViewModel.Delegate.OpenUrl("tel:" + e.Text, false);
            }
            else if (e.Type is TextEntityTypeHashtag or TextEntityTypeCashtag && e.Text is string hashtag)
            {
                ViewModel.Delegate.OpenHashtag(hashtag);
            }
            else if (e.Type is TextEntityTypeMention && e.Text is string username)
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
            else if (e.Type is TextEntityTypeUrl && e.Text is string url)
            {
                ViewModel.Delegate.OpenUrl(url, false);
            }
            else if (e.Type is TextEntityTypeBankCardNumber && e.Text is string cardNumber)
            {
                ViewModel.Delegate.OpenBankCardNumber(cardNumber);
            }
            else if (e.Type is TextEntityTypeMediaTimestamp mediaTimestamp)
            {
                // Never happens here
            }
        }

        private void MessagesHost_ChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new ListViewItem
                {
                    Style = sender.ItemContainerStyle,
                    ContentTemplate = sender.ItemTemplate
                };

                args.ItemContainer.ContextRequested += MessagesHost_ContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void MessagesHost_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();
            var message = MessagesHost.ItemFromContainer(sender) as GroupCallMessage;

            var textBlock = new TextBlock();
            textBlock.Text = Formatter.SentDate(message.Date);
            textBlock.FontSize = 12;

            var placeholder = new MenuFlyoutContent();
            placeholder.Content = textBlock;
            placeholder.FontSize = 12;
            placeholder.Padding = new Thickness(12, 4, 12, 4);
            placeholder.HorizontalAlignment = HorizontalAlignment.Left;

            flyout.Items.Add(placeholder);
            flyout.CreateFlyoutSeparator();
            flyout.CreateFlyoutItem(OpenMessage, message, Strings.OpenProfile, Icons.PersonCircle);
            flyout.CreateFlyoutItem(CopyMessage, message, Strings.Copy, Icons.Copy);

            var translate = ViewModel.Session.Resolve<ITranslateService>();
            if (translate.CanTranslateText(message.Text.Text))
            {
                flyout.CreateFlyoutItem(TranslateMessage, message, Strings.TranslateMessage, Icons.Translate);
            }

            if (message.CanBeDeleted)
            {
                flyout.CreateFlyoutItem(DeleteMessage, message, Strings.Delete, Icons.Delete, destructive: true);
            }

            flyout.ShowAt(sender, args);
        }

        private void OpenMessage(GroupCallMessage message)
        {
            ViewModel.NavigationService.NavigateToSender(message.SenderId);
        }

        private void CopyMessage(GroupCallMessage message)
        {
            MessageHelper.CopyText(XamlRoot, message.Text);
        }

        private void TranslateMessage(GroupCallMessage message)
        {
            var language = LanguageIdentification.IdentifyLanguage(message.Text.Text);
            var translate = ViewModel.Session.Resolve<ITranslateService>();

            var popup = new TranslatePopup(translate, message.Text, language, ViewModel.Settings.Translate.To, false)
            {
                RequestedTheme = ElementTheme.Dark
            };

            ViewModel.ShowPopup(popup);
        }

        public async void DeleteMessage(GroupCallMessage message)
        {
            if (ViewModel.SelectedItem?.Content is not StoryContentLive live || !ViewModel.ClientService.TryGetGroupCall(live.GroupCallId, out GroupCall groupCall))
            {
                return;
            }

            var popup = new DeleteMessagesPopup(ViewModel.ClientService, groupCall, message);
            popup.RequestedTheme = ElementTheme.Dark;

            var confirm = await ViewModel.ShowPopupAsync(popup);
            if (confirm == ContentDialogResult.Primary)
            {
                if (popup.DeleteAll.Any())
                {
                    ViewModel.ClientService.Send(new DeleteGroupCallMessagesBySender(_call.Id, message.SenderId, popup.ReportSpam.Any()));
                }
                else
                {
                    ViewModel.ClientService.Send(new DeleteGroupCallMessages(_call.Id, [message.MessageId], popup.ReportSpam.Any()));
                }

                if (popup.BanUser.Any())
                {
                    if (ViewModel.Chat.Type is ChatTypePrivate)
                    {
                        ViewModel.ClientService.Send(new SetMessageSenderBlockList(message.SenderId, new BlockListMain()));
                    }
                    else
                    {
                        ViewModel.ClientService.Send(new SetChatMemberStatus(ViewModel.ChatId, message.SenderId, new ChatMemberStatusBanned()));
                    }
                }
            }
        }

        private void MessagesHost_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is LiveStoryMessageCell content && args.Item is GroupCallMessage message)
            {
                _topDonors.TryGetValue(message.SenderId, out int position);
                content.Update(_call.ClientService, message, position);
            }
        }

        private void MessagesHost_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var layerVisual = _messagesVisual;
            if (layerVisual == null)
            {
                return;
            }

            var compositor = layerVisual.Compositor;

            var alphaMask = new AlphaMaskEffect
            {
                Name = "AlphaMask",
                Source = new CompositionEffectSourceParameter("source"),
                AlphaMask = new CompositionEffectSourceParameter("mask")
            };

            var next = e.NewSize.ToVector2();
            var top = 24 / next.Y;
            var bottom = (next.Y - 8) / next.Y;

            var gradientBrush = compositor.CreateLinearGradientBrush();
            gradientBrush.StartPoint = new(0, 0);
            gradientBrush.EndPoint = new(0, 1);
            gradientBrush.MappingMode = CompositionMappingMode.Relative;
            //gradientBrush.ExtendMode = CompositionGradientExtendMode.Clamp;

            gradientBrush.ColorStops.Add(compositor.CreateColorGradientStop(0, Colors.Transparent));
            gradientBrush.ColorStops.Add(compositor.CreateColorGradientStop(top, Colors.White));
            gradientBrush.ColorStops.Add(compositor.CreateColorGradientStop(bottom, Colors.White));
            gradientBrush.ColorStops.Add(compositor.CreateColorGradientStop(1, Colors.Transparent));

            var effectFactory = compositor.CreateEffectFactory(alphaMask);
            var effectBrush = effectFactory.CreateBrush();
            effectBrush.SetSourceParameter("mask", gradientBrush);
            layerVisual.Effect = effectBrush;
        }

        private void PinnedMessagesHost_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is LiveStoryPinnedMessageCell content && args.Item is GroupCallMessage message)
            {
                _topDonors.TryGetValue(message.SenderId, out int position);
                content.Update(_call.ClientService, message, position);
            }
        }

        private void ReactionsHost_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var visual = ElementComposition.GetElementVisual(MessagesHost);
            ElementCompositionPreview.SetIsTranslationEnabled(MessagesHost, true);

            var prev = e.PreviousSize.ToVector2();
            var next = e.NewSize.ToVector2();

            var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
            animation.InsertKeyFrame(0, next.Y - prev.Y);
            animation.InsertKeyFrame(1, 0);

            visual.StartAnimation("Translation.Y", animation);
        }

        private async void PinnedMessage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not LiveStoryPinnedMessageCell pinnedContent || pinnedContent.Message == null)
            {
                return;
            }

            await MessagesHost.ScrollToItem2(pinnedContent.Message, VerticalAlignment.Center);

            var container = MessagesHost.ContainerFromItem(pinnedContent.Message) as SelectorItem;
            if (container?.ContentTemplateRoot is LiveStoryMessageCell content)
            {
                content.Highlight();
            }
        }

        private bool _messagesCollapsed;

        public void ShowHideMessages(bool show)
        {
            if (_messagesCollapsed != show)
            {
                return;
            }

            _messagesCollapsed = !show;
            MessagesHost.Visibility = Visibility.Visible;
            MessagesShadow.Visibility = Visibility.Visible;

            var visual = ElementComposition.GetElementVisual(MessagesHost);
            var shadow = ElementComposition.GetElementVisual(MessagesShadow);
            ElementCompositionPreview.SetIsTranslationEnabled(MessagesHost, true);

            var compositor = visual.Compositor;

            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                if (_messagesCollapsed)
                {
                    MessagesHost.Visibility = Visibility.Collapsed;
                    MessagesShadow.Visibility = Visibility.Collapsed;
                }
            };

            var translation = compositor.CreateScalarKeyFrameAnimation();
            translation.InsertKeyFrame(0, show ? MessagesShadow.ActualSize.Y : 0);
            translation.InsertKeyFrame(1, show ? 0 : MessagesShadow.ActualSize.Y);

            var opacity = compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, show ? 0 : 1);
            opacity.InsertKeyFrame(1, show ? 1 : 0);

            visual.StartAnimation("Translation.Y", translation);
            visual.StartAnimation("Opacity", opacity);
            shadow.StartAnimation("Opacity", opacity);

            batch.End();
        }
    }

    public partial class StoryContentPhotoTimer
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

    public partial class StoryProgress : Grid
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
                    var compositor = BootStrapper.Current.Compositor;

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
