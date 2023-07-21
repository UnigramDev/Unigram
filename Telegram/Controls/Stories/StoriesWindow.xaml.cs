using LinqToVisualTree;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Services.Keyboard;
using Telegram.Td.Api;
using Telegram.ViewModels.Stories;
using Telegram.Views.Popups;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using VirtualKey = Windows.System.VirtualKey;

namespace Telegram.Controls.Stories
{
    public enum StoryOrigin
    {
        ProfilePhoto,
        Card
    }

    public sealed partial class StoriesWindow : OverlayPage
    {
        public StoriesWindow()
        {
            InitializeComponent();

            _nextTimer = new DispatcherTimer();
            _nextTimer.Interval = TimeSpan.FromSeconds(5);
            _nextTimer.Tick += _nextTimer_Tick;

            Loaded += StoriesWindow_Loaded;

            Unloaded += (s, args) =>
            {
                _nextTimer.Stop();
            };

            InitializeStickers();
        }

        protected override void MaskTitleAndStatusBar()
        {
            base.MaskTitleAndStatusBar();
            Window.Current.SetTitleBar(TitleBar);
        }

        protected override void OnPointerWheelChanged(PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(this);
            var direction = point.Properties.MouseWheelDelta > 0
                ? Direction.Backward
                : Direction.Forward;

            Move(direction);

            e.Handled = true;
            base.OnPointerWheelChanged(e);
        }

        private void StoriesWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var layer = ElementCompositionPreview.GetElementVisual(Layer);
            var backButton = ElementCompositionPreview.GetElementVisual(BackButton);
            var viewport = ElementCompositionPreview.GetElementVisual(Viewport);
            var composer = ElementCompositionPreview.GetElementVisual(Composer);
            ElementCompositionPreview.SetIsTranslationEnabled(Composer, true);

            var opacity = composer.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, 0);
            opacity.InsertKeyFrame(1, 1);
            opacity.Duration = Constants.FastAnimation;

            for (int i = 0; i < _indexes.Length; i++)
            {
                var child = LayoutRoot.Children[i] as StoryContent;
                var index = _indexes[i];
                var real = _index + index - 3;

                if (index == 3)
                {
                    child.TryStart(_ciccio, _origin);
                }
                else
                {
                    var visual = ElementCompositionPreview.GetElementVisual(child);
                    visual.StartAnimation("Opacity", opacity);
                }
            }

            var translation = composer.Compositor.CreateScalarKeyFrameAnimation();
            translation.InsertKeyFrame(0, 48 + 8);
            translation.InsertKeyFrame(1, 0);
            translation.Duration = Constants.FastAnimation;

            layer.StartAnimation("Opacity", opacity);
            backButton.StartAnimation("Opacity", opacity);
            viewport.StartAnimation("Opacity", opacity);
            composer.StartAnimation("Translation.Y", translation);
        }

        private bool _done;

        protected override void OnBackRequestedOverride(object sender, BackRequestedRoutedEventArgs e)
        {
            if (_done)
            {
                return;
            }

            _done = true;
            e.Handled = true;

            ActiveCard?.Suspend(StoryPauseSource.Window);

            var layer = ElementCompositionPreview.GetElementVisual(Layer);
            var backButton = ElementCompositionPreview.GetElementVisual(BackButton);
            var viewport = ElementCompositionPreview.GetElementVisual(Viewport);
            var composer = ElementCompositionPreview.GetElementVisual(Composer);
            ElementCompositionPreview.SetIsTranslationEnabled(Composer, true);

            var batch = layer.Compositor.CreateScopedBatch(Windows.UI.Composition.CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                Hide();
            };

            var opacity = composer.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(1, 0);
            opacity.InsertKeyFrame(0, 1);
            opacity.Duration = Constants.FastAnimation;

            for (int i = 0; i < _indexes.Length; i++)
            {
                var child = LayoutRoot.Children[i] as StoryContent;
                var index = _indexes[i];
                var real = _index + index - 3;

                if (index == 3 && _closing != null)
                {
                    var viewModel = _viewModel.Items[real];

                    var origin = _closing(viewModel);
                    if (origin.IsEmpty)
                    {
                        var visual = ElementCompositionPreview.GetElementVisual(child);
                        visual.StartAnimation("Opacity", opacity);
                    }
                    else
                    {
                        child.TryStart(_ciccio, origin, false);
                    }
                }
                else
                {
                    var visual = ElementCompositionPreview.GetElementVisual(child);
                    visual.StartAnimation("Opacity", opacity);
                }
            }

            var translation = composer.Compositor.CreateScalarKeyFrameAnimation();
            translation.InsertKeyFrame(1, 48 + 8);
            translation.InsertKeyFrame(0, 0);
            translation.Duration = Constants.FastAnimation;

            layer.StartAnimation("Opacity", opacity);
            backButton.StartAnimation("Opacity", opacity);
            viewport.StartAnimation("Opacity", opacity);
            composer.StartAnimation("Translation.Y", translation);

            batch.End();
        }

        #region Stickers

        private void InitializeStickers()
        {
            StickersPanel.EmojiClick = Emojis_ItemClick;

            StickersPanel.StickerClick = Stickers_ItemClick;
            //StickersPanel.StickerContextRequested += Sticker_ContextRequested;
            //StickersPanel.SettingsClick += StickersPanel_SettingsClick;

            StickersPanel.AnimationClick = Animations_ItemClick;
            //StickersPanel.AnimationContextRequested += Animation_ContextRequested;
        }

        private void Emojis_ItemClick(object emoji)
        {
            if (emoji is string text)
            {
                TextField.InsertText(text);
            }
            else if (emoji is Sticker sticker)
            {
                TextField.InsertEmoji(sticker);
            }

            //_focusState.Set(FocusState.Programmatic);
        }

        public void Stickers_ItemClick(Sticker sticker)
        {
            Stickers_ItemClick(sticker, false);
        }

        public void Stickers_ItemClick(Sticker sticker, bool fromStickerSet)
        {
            var index = _indexes[_synchronizedIndex];
            var real = _index + index - 3;

            var viewModel = _viewModel.Items[real];

            viewModel.SendSticker(sticker, null, null, null, fromStickerSet);
            ButtonStickers.Collapse();

            //_focusState.Set(FocusState.Programmatic);
        }

        public void Animations_ItemClick(Animation animation)
        {
            var index = _indexes[_synchronizedIndex];
            var real = _index + index - 3;

            var viewModel = _viewModel.Items[real];

            viewModel.SendAnimation(animation);
            ButtonStickers.Collapse();

            //_focusState.Set(FocusState.Programmatic);
        }

        #endregion

        private void _nextTimer_Tick(object sender, object e)
        {
            Move(Direction.Forward);
        }

        private StoryListViewModel _viewModel;
        public StoryListViewModel ViewModel => _viewModel ??= DataContext as StoryListViewModel;

        private DispatcherTimer _nextTimer;

        private Windows.Foundation.Rect _origin;
        private StoryOrigin _ciccio;
        private Func<ActiveStoriesViewModel, Rect> _closing;

        public async void Update(StoryListViewModel viewModel, ActiveStoriesViewModel activeStories, StoryOrigin origin, Rect point, Func<ActiveStoriesViewModel, Rect> closing)
        {
            _ciccio = origin;
            _origin = point;
            _closing = closing;

            if (_viewModel != viewModel)
            {
                _viewModel = viewModel;
                DataContext = viewModel;

                _index = viewModel.Items.IndexOf(activeStories);
                _total = viewModel.Items.Count;
            }

            for (int i = 0; i < _indexes.Length; i++)
            {
                var child = LayoutRoot.Children[i] as StoryContent;
                var index = _indexes[i];
                var real = _index + index - 3;

                if (real >= 0 && real < _total)
                {
                    child.Visibility = Visibility.Visible;
                    child.Update(viewModel.Items[real], real == _index, index);

                    child.Completed -= OnCompleted;

                    if (index == 3)
                    {
                        _synchronizedIndex = i;

                        child.Completed += OnCompleted;
                        Composer.DataContext = viewModel.Items[real];

                        if (viewModel.Items[real].IsMyStory)
                        {
                            Interactions.Visibility = Visibility.Visible;
                            TextArea.Visibility = Visibility.Collapsed;

                            Interactions.Update(viewModel.Items[real].SelectedItem);
                        }
                        else if (viewModel.Items[real].SelectedItem.CanBeReplied)
                        {
                            Interactions.Visibility = Visibility.Collapsed;
                            TextArea.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            Interactions.Visibility = Visibility.Collapsed;
                            TextArea.Visibility = Visibility.Collapsed;
                        }
                    }
                }
                else
                {
                    child.Visibility = Visibility.Collapsed;
                }
            }

            _nextTimer.Stop();
            //_nextTimer.Start();

            if (_index == _total - 1 && _viewModel.Items is ISupportIncrementalLoading incremental && incremental.HasMoreItems)
            {
                await incremental.LoadMoreItemsAsync(20);
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            float height = MathF.Min((float)e.NewSize.Height, 720);
            float width = MathF.Min((float)e.NewSize.Width, 720);

            float ratioX = height / 9;
            float ratioY = height / 16;
            float ratio = Math.Min(ratioX, ratioY);

            float aatioX = width / 9;
            float aatioY = width / 16;
            float aatio = Math.Max(aatioX, aatioY);

            ratio = Math.Min(ratio, aatio);

            float y = 16 * ratio;
            float x = 9 * ratio;

            float small = (x - 0) * 0.40f;
            float margin = small * 0.2f;

            for (int i = 0; i < _indexes.Length; i++)
            {
                var child = LayoutRoot.Children[i] as StoryContent;
                child.Width = x;
                child.Height = y;
                child.Margin = new Thickness();

                var index = _indexes[i];
                var real = _index + index - 3;

                var visual = ElementCompositionPreview.GetElementVisual(LayoutRoot.Children[i]);
                visual.CenterPoint = new Vector3(x / 2, y / 2, 0);
                visual.Scale = new Vector3(index == 3 ? 1 : 0.4f);

                ElementCompositionPreview.SetIsTranslationEnabled(LayoutRoot.Children[i], true);

                if (index < 3 || index > 3)
                {
                    var zero = x / 2 + small / 2 - small;
                    zero += (small + margin) * (index < 3 ? (3 - index) : (index - 3));
                    zero *= index < 3 ? -1 : 1;

                    visual.Properties.InsertVector3("Translation", new Vector3(zero, 0, 0));
                }
                else
                {
                    visual.Properties.InsertVector3("Translation", new Vector3(0, 0, 0));
                }
            }

            Viewport.Width = x + 48;
            Composer.Width = x + 24;

            StickersPanel.Width = x + 24;
        }

        private float ItemOffset(int index)
        {
            float height = MathF.Min((float)LayoutRoot.ActualHeight, 1280);

            float ratioX = height / 9;
            float ratioY = height / 16;
            float ratio = Math.Min(ratioX, ratioY);

            float y = 16 * ratio;
            float x = 9 * ratio;

            float small = (x - 0) * 0.40f;
            float margin = small * 0.2f;

            if (index < 3 || index > 3)
            {
                var zero = x / 2 + small / 2 - small;
                zero += (small + margin) * (index < 3 ? (3 - index) : (index - 3));
                zero *= index < 3 ? -1 : 1;

                return zero;
            }

            return 0;
        }

        private readonly int[] _indexes = new int[]
        {
            0, 1, 2, 3, 4, 5, 6
        };

        private void Prev_Click(object sender, RoutedEventArgs e)
        {
            Move(Direction.Backward);
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            Move(Direction.Forward);
        }

        private int _index = 0;
        private int _total = 15;

        private int _synchronizedIndex = 3;

        private bool Move(Direction direction, int increment = 1, bool force = false)
        {
            if (!force)
            {
                var user = _viewModel.Items[_index];

                var item = user.Items.IndexOf(user.SelectedItem);
                if ((item > 0 && direction == Direction.Backward) || (item < user.Items.Count - 1 && direction == Direction.Forward))
                {
                    user.SelectedItem = user.Items[direction == Direction.Forward ? item + 1 : item - 1];

                    Update(_viewModel, user, _ciccio, _origin, _closing);
                    return true;
                }
            }

            if (_index < _total - increment && direction == Direction.Forward)
            {
                _index += increment;
            }
            else if (_index >= increment && direction == Direction.Backward)
            {
                _index -= increment;
            }
            else
            {
                return false;
            }

            for (int i = 0; i < _indexes.Length; i++)
            {
                var previous = _indexes[i];

                for (int j = 0; j < increment; j++)
                {
                    if (direction == Direction.Forward)
                    {
                        if (_indexes[i] == 0)
                        {
                            _indexes[i] = 6;
                            previous = 7;
                        }
                        else
                        {
                            _indexes[i]--;
                        }
                    }
                    else
                    {
                        if (_indexes[i] == 6)
                        {
                            _indexes[i] = 0;
                            previous = -1;
                        }
                        else
                        {
                            _indexes[i]++;
                        }
                    }
                }

                var index = _indexes[i];
                var real = _index + index - 3;

                if (index == 3)
                {
                    _synchronizedIndex = i;
                }

                if (LayoutRoot.Children[i] is FrameworkElement elle)
                {
                    elle.Visibility = real >= 0 && real < _total ? Visibility.Visible : Visibility.Collapsed;
                }

                ElementCompositionPreview.SetIsTranslationEnabled(LayoutRoot.Children[i], true);

                var visual = ElementCompositionPreview.GetElementVisual(LayoutRoot.Children[i]);

                //var from = ItemOffset(previous);
                //var to = ItemOffset(index);

                var translate = visual.Compositor.CreateScalarKeyFrameAnimation();
                translate.InsertKeyFrame(0, ItemOffset(previous));
                translate.InsertKeyFrame(1, ItemOffset(index));
                //translate.Duration = TimeSpan.FromSeconds(5);

                visual.StartAnimation("Translation.X", translate);

                if (previous == 3 || index == 3)
                {
                    var scale = visual.Compositor.CreateVector3KeyFrameAnimation();
                    //scale.InsertKeyFrame(index == 3 ? 1 : 0, new Vector3(1.00f));
                    //scale.InsertKeyFrame(index == 3 ? 0 : 1, new Vector3(0.40f));
                    scale.InsertKeyFrame(1, new Vector3(index == 3 ? 1.00f : 0.40f));
                    //scale.Duration = TimeSpan.FromSeconds(5);

                    visual.StartAnimation("Scale", scale);
                }
                else
                {
                    visual.Scale = new Vector3(0.40f);
                }

                if (LayoutRoot.Children[i] is StoryContent content)
                {
                    content.Animate(previous, index);
                }
            }

            Update(_viewModel, _viewModel.Items[_index], _ciccio, _origin, _closing);
            return true;
        }

        private void OnCompleted(object sender, EventArgs e)
        {
            if (Move(Direction.Forward))
            {
                return;
            }

            TryHide(ContentDialogResult.None);
        }

        private void TextArea_SizeChanged(object sender, SizeChangedEventArgs e)
        {

        }

        private void TextField_TextChanged(object sender, RoutedEventArgs e)
        {

        }

        private void TextField_Tapped(object sender, TappedRoutedEventArgs e)
        {

        }

        private void TextField_Sending(object sender, EventArgs e)
        {

        }

        private void Attach_Click(object sender, RoutedEventArgs e)
        {
            var index = _indexes[_synchronizedIndex];
            var real = _index + index - 3;

            var viewModel = _viewModel.Items[real];

            var chat = viewModel.Chat;
            if (chat == null)
            {
                return;
            }

            var flyout = new MenuFlyout();

            var photoRights = !viewModel.VerifyRights(chat, x => x.CanSendPhotos);
            var videoRights = !viewModel.VerifyRights(chat, x => x.CanSendVideos);
            var documentRights = !viewModel.VerifyRights(chat, x => x.CanSendDocuments);

            var messageRights = !viewModel.VerifyRights(chat, x => x.CanSendBasicMessages);
            var pollRights = !viewModel.VerifyRights(chat, x => x.CanSendPolls, Strings.GlobalAttachMediaRestricted, Strings.AttachMediaRestrictedForever, Strings.AttachMediaRestricted, out string pollsLabel);

            var pollsAllowed = chat.Type is ChatTypeSupergroup or ChatTypeBasicGroup;
            if (!pollsAllowed && ViewModel.ClientService.TryGetUser(chat, out User user))
            {
                pollsAllowed = user.Type is UserTypeBot;
            }

            if (photoRights || videoRights)
            {
                flyout.CreateFlyoutItem(viewModel.SendMedia, Strings.PhotoOrVideo, Icons.Image);
                flyout.CreateFlyoutItem(viewModel.SendCamera, Strings.ChatCamera, Icons.Camera);
            }

            if (documentRights)
            {
                flyout.CreateFlyoutItem(viewModel.SendDocument, Strings.ChatDocument, Icons.Document);
            }

            if (messageRights)
            {
                flyout.CreateFlyoutItem(viewModel.SendLocation, Strings.ChatLocation, Icons.Location);
            }

            if (pollRights && pollsAllowed)
            {
                flyout.CreateFlyoutItem(viewModel.SendPoll, Strings.Poll, Icons.Poll);
            }

            if (messageRights)
            {
                flyout.CreateFlyoutItem(viewModel.SendContact, Strings.AttachContact, Icons.Person);
            }

            if (flyout.Items.Count > 0)
            {
                flyout.ShowAt(ButtonAttach, FlyoutPlacementMode.TopEdgeAlignedLeft);
            }
        }

        private void btnSendMessage_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Send_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {

        }

        private async void Interactions_ViewersClick(object sender, RoutedEventArgs e)
        {
            var user = ViewModel.Items[_index];
            var story = user.SelectedItem;

            if (story.CanGetViewers && !story.HasExpiredViewers)
            {
                ActiveCard.Suspend(StoryPauseSource.Popup);
                await ViewModel.ShowPopupAsync(typeof(InteractionsPopup), new MessageReplyToStory(story.ChatId, story.StoryId), requestedTheme: ElementTheme.Dark);
                ActiveCard.Resume(StoryPauseSource.Popup);
            }
            else
            {
                ShowTeachingTip(sender as FrameworkElement, Strings.ExpiredViewsStub);
            }
        }

        private void Story_Click(object sender, RoutedEventArgs e)
        {
            if (sender is UIElement element)
            {
                sender = element.Ancestors<StoryContent>().FirstOrDefault();
            }

            if (sender is StoryContent story)
            {
                var index = LayoutRoot.Children.IndexOf(story);
                var adjus = _indexes[index];

                var distance = adjus - _indexes[_synchronizedIndex];

                Move(distance > 0 ? Direction.Forward : Direction.Backward, Math.Abs(distance), true);
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            WindowContext.Current.Activated += OnActivated;
            WindowContext.Current.InputListener.KeyDown += OnAcceleratorKeyActivated;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            WindowContext.Current.Activated -= OnActivated;
            WindowContext.Current.InputListener.KeyDown -= OnAcceleratorKeyActivated;
        }

        private void OnActivated(object sender, Windows.UI.Core.WindowActivatedEventArgs e)
        {
            if (e.WindowActivationState != Windows.UI.Core.CoreWindowActivationState.Deactivated)
            {
                ActiveCard.Resume(StoryPauseSource.Window);
            }
            else
            {
                ActiveCard.Suspend(StoryPauseSource.Window);
            }
        }

        private void OnAcceleratorKeyActivated(Window sender, InputKeyDownEventArgs args)
        {
            var keyCode = (int)args.VirtualKey;

            if (args.VirtualKey is VirtualKey.Left or VirtualKey.GamepadLeftShoulder or VirtualKey.PageUp)
            {
                Move(Direction.Backward, force: args.VirtualKey is VirtualKey.PageUp);
                args.Handled = true;
            }
            else if (args.VirtualKey is VirtualKey.Right or VirtualKey.GamepadRightShoulder or VirtualKey.PageDown)
            {
                Move(Direction.Forward, force: args.VirtualKey is VirtualKey.PageDown);
                args.Handled = true;
            }
            else if (args.VirtualKey is VirtualKey.Space && args.OnlyKey)
            {
                ActiveCard.Toggle();
                args.Handled = true;
            }
            //else if (args.VirtualKey is VirtualKey.C && args.OnlyControl)
            //{
            //    ViewModel?.Copy();
            //    args.Handled = true;
            //}
            //else if (args.VirtualKey is VirtualKey.S && args.OnlyControl)
            //{
            //    ViewModel?.Save();
            //    args.Handled = true;
            //}
            //else if (args.VirtualKey is VirtualKey.F11 || (args.VirtualKey is VirtualKey.F && args.OnlyControl))
            //{
            //    FullScreen_Click(null, null);
            //    args.Handled = true;
            //}
            //else if (keyCode is 187 or 189 || args.VirtualKey is VirtualKey.Add or VirtualKey.Subtract)
            //{
            //    ScrollingHost.Zoom(keyCode is 187 || args.VirtualKey is VirtualKey.Add);
            //    args.Handled = true;
            //}
            //else if (args.VirtualKey is VirtualKey.Up && _mediaPlayer?.Source != null)
            //{
            //    _mediaPlayer.Volume = Math.Clamp(_mediaPlayer.Volume + 0.1, 0, 1);
            //    args.Handled = true;
            //}
            //else if (args.VirtualKey is VirtualKey.Down && _mediaPlayer?.Source != null)
            //{
            //    _mediaPlayer.Volume = Math.Clamp(_mediaPlayer.Volume - 0.1, 0, 1);
            //    args.Handled = true;
            //}
        }

        private void Story_ContextRequested(object sender, StoryEventArgs e)
        {
            var element = sender as FrameworkElement;

            var story = e.ActiveStories?.SelectedItem;
            if (story == null)
            {
                return;
            }

            var flyout = new MenuFlyout();
            flyout.Closing += Flyout_Closing;
            ActiveCard.Suspend(StoryPauseSource.Flyout);

            if (e.ActiveStories.IsMyStory)
            {
                flyout.CreateFlyoutItem(ViewModel.ToggleStory, story, story.IsPinned ? Strings.ArchiveStory : Strings.SaveToProfile, story.IsPinned ? Icons.Archive : Icons.Add);

                if (story.Content is StoryContentPhoto)
                {
                    flyout.CreateFlyoutItem(ViewModel.ToggleStory, story, Strings.SavePhoto, Icons.SaveAs);
                }
                else if (story.Content is StoryContentVideo)
                {
                    flyout.CreateFlyoutItem(ViewModel.ToggleStory, story, Strings.SaveVideo, Icons.SaveAs);
                }

                if (story.CanBeForwarded)
                {
                    flyout.CreateFlyoutItem(ViewModel.ShareStory, story, Strings.CopyLink, Icons.Link);
                    flyout.CreateFlyoutItem(ViewModel.ShareStory, story, Strings.StickersShare, Icons.Share);
                }

                flyout.CreateFlyoutItem(ViewModel.DeleteStory, story, Strings.Delete, Icons.Delete, dangerous: true);
            }
            else
            {
                if (story.CanBeForwarded)
                {
                    flyout.CreateFlyoutItem(ViewModel.ShareStory, story, Strings.CopyLink, Icons.Link);
                    flyout.CreateFlyoutItem(ViewModel.ShareStory, story, Strings.StickersShare, Icons.Share);
                }

                var muted = ViewModel.Settings.Notifications.GetMuteStories(e.ActiveStories.Chat);
                var archived = e.ActiveStories.List is StoryListArchive;

                flyout.CreateFlyoutItem(ViewModel.MuteProfile, e.ActiveStories, muted ? Strings.NotificationsStoryUnmute : Strings.NotificationsStoryMute, muted ? Icons.Alert : Icons.AlertOff);

                if (archived)
                {
                    flyout.CreateFlyoutItem(ViewModel.ShowProfile, e.ActiveStories, Strings.Unarchive, Icons.Unarchive);
                }
                else
                {
                    flyout.CreateFlyoutItem(ViewModel.HideProfile, e.ActiveStories, Strings.Archive, Icons.Archive);
                }

                flyout.CreateFlyoutItem(ViewModel.ReportStory, story, Strings.ReportChat, Icons.ErrorCircle);
            }

            flyout.ShowAt(sender as FrameworkElement, FlyoutPlacementMode.BottomEdgeAlignedRight);
        }

        private void Flyout_Closing(FlyoutBase sender, FlyoutBaseClosingEventArgs args)
        {
            sender.Closing -= Flyout_Closing;
            ActiveCard.Resume(StoryPauseSource.Flyout);
        }

        public void ShowTeachingTip(FrameworkElement target, string text, TeachingTipPlacementMode placement = TeachingTipPlacementMode.TopRight)
        {
            var tip = Window.Current.ShowTeachingTip(target, text, placement, ElementTheme.Dark);
            tip.Closing += TeachingTip_Closing;
            ActiveCard.Suspend(StoryPauseSource.TeachingTip);
        }

        private void TeachingTip_Closing(TeachingTip sender, TeachingTipClosingEventArgs args)
        {
            sender.Closing -= TeachingTip_Closing;
            ActiveCard.Resume(StoryPauseSource.TeachingTip);
        }

        private void ButtonStickers_Opening(object sender, EventArgs e)
        {
            ActiveCard.Suspend(StoryPauseSource.Stickers);
        }

        private void ButtonStickers_Closing(object sender, EventArgs e)
        {
            ActiveCard.Resume(StoryPauseSource.Stickers);
        }

        private void ButtonRecord_Started(object sender, EventArgs e)
        {
            ActiveCard.Suspend(StoryPauseSource.Record);
        }

        private void ButtonRecord_Stopped(object sender, EventArgs e)
        {
            ActiveCard.Resume(StoryPauseSource.Record);
        }

        private void TextField_GotFocus(object sender, RoutedEventArgs e)
        {
            ActiveCard.Suspend(StoryPauseSource.Text);
        }

        private void TextField_LostFocus(object sender, RoutedEventArgs e)
        {
            ActiveCard.Resume(StoryPauseSource.Text);
        }

        private StoryContent ActiveCard => LayoutRoot.Children[_synchronizedIndex] as StoryContent;

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            TryHide(ContentDialogResult.Primary);
        }

        private bool _backGesture;

        private void Layer_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (_backGesture)
            {
                _backGesture = false;
                return;
            }

            TryHide(ContentDialogResult.Primary);
        }

        private void Layer_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (InputListener.IsPointerGoBackGesture(e.GetCurrentPoint(this)))
            {
                _backGesture = true;
            }
        }
    }

    [Flags]
    public enum StoryPauseSource
    {
        None = 0,
        Stickers = 1,
        Record = 2,
        Text = 4,
        TeachingTip = 8,
        Flyout = 16,
        Popup = 32,
        Interaction = 64,
        Caption = 128,
        Window = 256
    }

    enum Direction
    {
        Forward,
        Backward
    }
}
