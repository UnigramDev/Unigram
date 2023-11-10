//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Services.Keyboard;
using Telegram.Services.ViewService;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Chats;
using Telegram.ViewModels.Delegates;
using Telegram.ViewModels.Gallery;
using Telegram.ViewModels.Users;
using Telegram.Views;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.Media.Playback;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using VirtualKey = Windows.System.VirtualKey;

namespace Telegram.Controls.Gallery
{
    public sealed partial class GalleryWindow : OverlayWindow, IGalleryDelegate
    //, IHandle<UpdateDeleteMessages>
    //, IHandle<UpdateMessageContent>
    {
        public GalleryViewModelBase ViewModel => DataContext as GalleryViewModelBase;

        public IClientService ClientService => ViewModel.ClientService;

        private Func<FrameworkElement> _closing;

        private readonly DispatcherTimer _inactivityTimer;

        private readonly Visual _layout;

        private readonly Visual _layer;
        private readonly Visual _layerFullScreen;
        private readonly Visual _bottom;

        private bool _wasFullScreen;
        private bool _lastFullScreen;

        private bool _unloaded;

        private static readonly ConcurrentDictionary<int, long> _knownPositions = new();
        private long? _initialPosition;

        public long InitialPosition
        {
            get => _initialPosition ?? 0;
            set => _initialPosition = value > 0 ? value : null;
        }

        private GalleryWindow()
        {
            InitializeComponent();

            _layout = ElementCompositionPreview.GetElementVisual(LayoutRoot);

            _layer = ElementCompositionPreview.GetElementVisual(Layer);
            _layerFullScreen = ElementCompositionPreview.GetElementVisual(LayerFullScreen);
            _bottom = ElementCompositionPreview.GetElementVisual(BottomPanel);

            _layer.Opacity = 0;
            _layerFullScreen.Opacity = 0;
            _bottom.Opacity = 0;

            _inactivityTimer = new DispatcherTimer();
            _inactivityTimer.Tick += OnTick;
            _inactivityTimer.Interval = TimeSpan.FromSeconds(2);
            _inactivityTimer.Start();

            ScrollingHost.AddHandler(PointerReleasedEvent, new PointerEventHandler(OnPointerReleased), true);
        }

        private void OnTick(object sender, object e)
        {
            _inactivityTimer.Stop();
            ShowHideTransport(false);
        }

        protected override void OnPointerMoved(PointerRoutedEventArgs e)
        {
            _inactivityTimer.Stop();
            ShowHideTransport(true);

            var point = e.GetCurrentPoint(Controls);
            if (point.Position.X < 0
                || point.Position.Y < 0
                || point.Position.X > Controls.ActualWidth
                || point.Position.Y > Controls.ActualHeight)
            {
                _inactivityTimer.Start();
            }

            base.OnPointerMoved(e);
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint((UIElement)_current ?? this);
            if (point.Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonReleased || !_areInteractionsEnabled)
            {
                return;
            }

            if (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse)
            {
                _inactivityTimer.Stop();
                ShowHideTransport(true);
            }
            else
            {
                if (_current != null
                    && point.Position.X >= 0
                    && point.Position.Y >= 0
                    && point.Position.X <= _current.ActualWidth
                    && point.Position.Y <= _current.ActualHeight)
                {
                    Controls.TogglePlaybackState();
                    return;
                }

                OnBackRequested(new BackRequestedRoutedEventArgs());
            }
        }

        private bool _transportCollapsed = false;

        private void ShowHideTransport(bool show)
        {
            if (show != _transportCollapsed)
            {
                return;
            }

            _transportCollapsed = !show;
            BottomPanel.Visibility = Visibility.Visible;

            var parent = ElementCompositionPreview.GetElementVisual(BottomPanel);
            var next = ElementCompositionPreview.GetElementVisual(NextButton);
            var prev = ElementCompositionPreview.GetElementVisual(PrevButton);
            var back = ElementCompositionPreview.GetElementVisual(BackButton);

            parent.Opacity = next.Opacity = prev.Opacity = back.Opacity = 1;

            var batch = parent.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                if (show)
                {
                    _transportCollapsed = false;
                }
                else
                {
                    BottomPanel.Visibility = Visibility.Collapsed;
                }
            };

            var opacity = parent.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, show ? 0 : 1);
            opacity.InsertKeyFrame(1, show ? 1 : 0);
            parent.StartAnimation("Opacity", opacity);
            next.StartAnimation("Opacity", opacity);
            prev.StartAnimation("Opacity", opacity);
            back.StartAnimation("Opacity", opacity);

            batch.End();
        }

        public void Handle(UpdateDeleteMessages update)
        {
            if (update.FromCache || !update.IsPermanent)
            {
                return;
            }

            this.BeginOnUIThread(() =>
            {
                var viewModel = ViewModel;
                if (viewModel != null && viewModel.SelectedItem is GalleryMessage message && message.ChatId == update.ChatId && update.MessageIds.Any(x => x == message.Id))
                {
                    OnBackRequestedOverride(this, new BackRequestedRoutedEventArgs());
                }
            });
        }

        public void Handle(UpdateMessageContent update)
        {
            this.BeginOnUIThread(() =>
            {
                var viewModel = ViewModel;
                if (viewModel != null && viewModel.SelectedItem is GalleryMessage message && message.ChatId == update.ChatId && message.Id == update.MessageId && message.SelfDestructType is MessageSelfDestructTypeTimer)
                {
                    OnBackRequestedOverride(this, new BackRequestedRoutedEventArgs());
                }
            });
        }

        public void OpenFile(GalleryMedia item, File file)
        {
            Play(LayoutRoot.CurrentElement, item);
        }

        public void OpenItem(GalleryMedia item)
        {
            OnBackRequested(new BackRequestedRoutedEventArgs());
        }

        private async void OnSourceChanged()
        {
            //var source = _mediaPlayer == null || _mediaPlayer.Source == null;
            //await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            //{
            //    Element0.IsHitTestVisible = source;
            //    Element1.IsHitTestVisible = source;
            //    Element2.IsHitTestVisible = source;
            //});
        }

        private async void OnVolumeChanged(MediaPlayer sender, object args)
        {
            SettingsService.Current.VolumeLevel = sender.Volume;

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                //Transport.Volume = sender.Volume;
            });
        }

        private void OnMediaOpened(MediaPlayer sender, object args)
        {
            sender.Volume = SettingsService.Current.VolumeLevel;
        }

        public static Task<ContentDialogResult> ShowAsync(ViewModelBase viewModelBase, IStorageService storageService, Chat chat, Func<FrameworkElement> closing = null)
        {
            var clientService = viewModelBase.ClientService;
            var aggregator = viewModelBase.Aggregator;
            var navigationService = viewModelBase.NavigationService;

            if (chat.Type is ChatTypePrivate or ChatTypeSecret)
            {
                var user = clientService.GetUser(chat);
                if (user == null)
                {
                    return Task.FromResult(ContentDialogResult.None);
                }

                var userFull = clientService.GetUserFull(user.Id);
                if (userFull?.Photo == null && userFull?.PublicPhoto == null && userFull?.PersonalPhoto == null)
                {
                    return Task.FromResult(ContentDialogResult.None);
                }

                var viewModel = new UserPhotosViewModel(clientService, storageService, aggregator, user, userFull);
                viewModel.NavigationService = navigationService;
                return ShowAsync(viewModel, closing);
            }
            else if (chat.Type is ChatTypeBasicGroup)
            {
                var basicGroupFull = clientService.GetBasicGroupFull(chat);
                if (basicGroupFull?.Photo == null)
                {
                    return Task.FromResult(ContentDialogResult.None);
                }

                var viewModel = new ChatPhotosViewModel(clientService, storageService, aggregator, chat, basicGroupFull.Photo);
                viewModel.NavigationService = navigationService;
                return ShowAsync(viewModel, closing);
            }
            else if (chat.Type is ChatTypeSupergroup)
            {
                var supergroupFull = clientService.GetSupergroupFull(chat);
                if (supergroupFull?.Photo == null)
                {
                    return Task.FromResult(ContentDialogResult.None);
                }

                var viewModel = new ChatPhotosViewModel(clientService, storageService, aggregator, chat, supergroupFull.Photo);
                viewModel.NavigationService = navigationService;
                return ShowAsync(viewModel, closing);
            }

            return Task.FromResult(ContentDialogResult.None);
        }

        public static Task<ContentDialogResult> ShowAsync(GalleryViewModelBase parameter, Func<FrameworkElement> closing = null, long timestamp = 0)
        {
            var popup = new GalleryWindow
            {
                InitialPosition = timestamp
            };

            return popup.ShowAsyncInternal(parameter, closing);
        }

        private Task<ContentDialogResult> ShowAsyncInternal(GalleryViewModelBase parameter, Func<FrameworkElement> closing = null)
        {
            _closing = closing;

            if (_closing != null && IsConstrainedToRootBounds)
            {
                ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("FullScreenPicture", _closing());
            }

            Load(parameter);

            RoutedEventHandler handler = null;
            handler = new RoutedEventHandler(async (s, args) =>
            {
                if (_unloaded)
                {
                    return;
                }

                parameter.Items.CollectionChanged -= OnCollectionChanged;
                parameter.Items.CollectionChanged += OnCollectionChanged;

                PrepareNext(0, true);

                var applicationView = ApplicationView.GetForCurrentView();

                _wasFullScreen = applicationView.IsFullScreenMode;

                if (CanUnconstrainFromRootBounds && !_wasFullScreen)
                {
                    applicationView.TryEnterFullScreenMode();
                }

                applicationView.VisibleBoundsChanged += OnVisibleBoundsChanged;
                OnVisibleBoundsChanged(applicationView, null);

                InitializeBackButton();
                Controls.Focus(FocusState.Programmatic);

                Loaded -= handler;

                var viewModel = ViewModel;
                if (viewModel != null)
                {
                    await viewModel.NavigatedToAsync(parameter, NavigationMode.New, null);
                }
            });

            Loaded += handler;
            return ShowAsync();
        }

        protected override void MaskTitleAndStatusBar()
        {
            base.MaskTitleAndStatusBar();
            Window.Current.SetTitleBar(TitleBar);
        }

        private void InitializeBackButton()
        {
            if (IsConstrainedToRootBounds)
            {
                BackButton.Glyph = "\uE72B";
                BackButton.Margin = new Thickness(0, -40, 0, 0);
                BackButton.HorizontalAlignment = HorizontalAlignment.Left;
            }
            else
            {
                BackButton.Glyph = "\uE711";
                BackButton.Margin = new Thickness();
                BackButton.HorizontalAlignment = HorizontalAlignment.Right;
            }
        }

        private void OnVisibleBoundsChanged(ApplicationView sender, object args)
        {
            if (_lastFullScreen != sender.IsFullScreenMode)
            {
                //Window.Current.Content.Visibility = sender.IsFullScreenMode
                //    ? Visibility.Collapsed
                //    : Visibility.Visible;

                Controls.IsFullScreen = sender.IsFullScreenMode;
                Padding = new Thickness(0, sender.IsFullScreenMode ? 0 : 40, 0, 0);

                if (LayoutRoot.CurrentElement is GalleryContent container)
                {
                    container.Stretch = sender.IsFullScreenMode
                        ? Stretch.UniformToFill
                        : Stretch.Uniform;
                }

                var anim = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
                anim.InsertKeyFrame(0, sender.IsFullScreenMode ? 0 : 1);
                anim.InsertKeyFrame(1, sender.IsFullScreenMode ? 1 : 0);

                _layerFullScreen.StartAnimation("Opacity", anim);
            }

            _lastFullScreen = sender.IsFullScreenMode;
        }

        private void OnCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            PrepareNext(0, true);
        }

        protected override void OnBackRequestedOverride(object sender, BackRequestedRoutedEventArgs e)
        {
            e.Handled = true;

            var applicationView = ApplicationView.GetForCurrentView();
            if (applicationView.IsFullScreenMode && !_wasFullScreen)
            {
                applicationView.ExitFullScreenMode();

                if (e.Key == VirtualKey.Escape)
                {
                    Cancel();
                    return;
                }
            }

            applicationView.VisibleBoundsChanged -= OnVisibleBoundsChanged;

            var translate = true;

            if (ViewModel != null)
            {
                WindowContext.Current.EnableScreenCapture(ViewModel.GetHashCode());

                ViewModel.Aggregator.Unsubscribe(this);
                ViewModel.Items.CollectionChanged -= OnCollectionChanged;

                Bindings.StopTracking();

                if (ViewModel.SelectedItem == ViewModel.FirstItem && _closing != null)
                {
                    var root = LayoutRoot.CurrentElement;
                    if (root != null && root.IsLoaded && IsConstrainedToRootBounds && !_lastFullScreen)
                    {
                        var animation = ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("FullScreenPicture", root);
                        if (animation != null)
                        {
                            var element = _closing();
                            if (element.IsLoaded)
                            {
                                void handler(ConnectedAnimation s, object e)
                                {
                                    animation.Completed -= handler;
                                    Hide();
                                }

                                animation.Completed += handler;
                                animation.Configuration = new DirectConnectedAnimationConfiguration();
                                translate = animation.TryStart(element);
                            }
                        }
                    }
                }
            }

            var batch = _layer.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

            _layer.StartAnimation("Opacity", CreateScalarAnimation(1, 0, false));
            _bottom.StartAnimation("Opacity", CreateScalarAnimation(1, 0, false));

            if (translate)
            {
                _layout.StartAnimation("Offset.Y", CreateScalarAnimation(_layout.Offset.Y, ActualSize.Y, false));
                batch.Completed += (s, args) =>
                {
                    Hide();
                };
            }

            batch.End();

            Unload();
            Dispose();
        }

        private void Preview_ImageOpened(object sender, RoutedEventArgs e)
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return;
            }

            var image = sender as GalleryContent;
            if (image.Item != viewModel.FirstItem)
            {
                return;
            }

            var item = image.Item;
            if (item == null)
            {
                return;
            }

            _layer.StartAnimation("Opacity", CreateScalarAnimation(0, 1, true));
            _bottom.StartAnimation("Opacity", CreateScalarAnimation(0, 1, true));

            var container = LayoutRoot.CurrentElement as Grid;

            var animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("FullScreenPicture");
            if (animation != null)
            {
                if (animation.TryStart(image))
                {
                    void handler(ConnectedAnimation s, object args)
                    {
                        animation.Completed -= handler;

                        //Transport.Show();
                        InitializeVideo(container, item);
                    }

                    animation.Completed += handler;
                    return;
                }
            }

            //Transport.Show();
            InitializeVideo(container, item);
        }

        private void InitializeVideo(Grid container, GalleryMedia item)
        {
            if (item.IsVideo)
            {
                Play(container, item);

                if (GalleryCompactWindow.Current is ViewLifetimeControl compact)
                {
                    compact.Dispatcher.Dispatch(() =>
                    {
                        if (compact.Window.Content is GalleryCompactWindow player)
                        {
                            player.Pause();
                        }
                    });
                }
            }
        }

        private CompositionAnimation CreateScalarAnimation(float from, float to, bool showing)
        {
            var scalar = _layer.Compositor.CreateScalarKeyFrameAnimation();
            scalar.InsertKeyFrame(0, from);
            scalar.InsertKeyFrame(1, to, ConnectedAnimationService.GetForCurrentView().DefaultEasingFunction);
            scalar.Duration = TimeSpan.FromMilliseconds(showing ? 250 : 150);

            return scalar;
        }

        #region Binding

        private string ConvertFrom(object with)
        {
            if (with is User user)
            {
                return user.FullName();
            }
            else if (with is Chat chat)
            {
                return chat.Title;
            }

            return null;
        }

        private string ConvertDate(int value)
        {
            var date = Formatter.ToLocalTime(value);
            return string.Format(Strings.formatDateAtTime, Formatter.ShortDate.Format(date), Formatter.ShortTime.Format(date));
        }

        private string ConvertOf(GalleryMedia item, int index, int count)
        {
            if (item.IsPersonal)
            {
                return Strings.CustomAvatarTooltip;
            }
            else if (item.IsPublic)
            {
                return Strings.FallbackTooltip;
            }

            return string.Format(Strings.Of, index, count);
        }

        private Visibility ConvertCaption(FormattedText text)
        {
            if (string.IsNullOrEmpty(text?.Text))
            {
                return Visibility.Collapsed;
            }

            Caption.SetText(ViewModel.ClientService, text);
            return Visibility.Visible;
        }

        #endregion

        private GalleryContent _current;

        private void Play(FrameworkElement container, GalleryMedia item)
        {
            if (_unloaded || container is not GalleryContent content)
            {
                return;
            }

            var position = 0L;
            var file = item.GetFile();

            if (_initialPosition is long initialPosition)
            {
                _initialPosition = null;
                position = initialPosition;
            }
            else if (_knownPositions.TryRemove(file.Id, out long knownPosition))
            {
                position = knownPosition;
            }

            //_mediaPlayer.IsLoopingEnabled = item.IsLoop;

            _current = content;
            _current.Play(item, position, Controls);
        }

        private void Dispose()
        {
            ScrollingHost.Zoom(1, true);
            Controls.Stop();

            if (_current != null)
            {
                _current.Stop(out int fileId, out long position);
                _current = null;

                if (fileId != 0)
                {
                    _knownPositions[fileId] = position;
                }
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            WindowContext.Current.InputListener.KeyDown += OnAcceleratorKeyActivated;
        }

        private void Load(object parameter)
        {
            DataContext = parameter;
            Bindings.Update();

            ViewModel?.Aggregator.Subscribe<UpdateDeleteMessages>(this, Handle)
                    .Subscribe<UpdateMessageContent>(Handle);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Unload();
            WindowContext.Current.InputListener.KeyDown -= OnAcceleratorKeyActivated;
        }

        private void Unload()
        {
            if (_unloaded)
            {
                return;
            }

            _unloaded = true;

            ViewModel?.Aggregator.Unsubscribe(this);

            DataContext = null;
            Bindings.StopTracking();

            Controls.Unload();

            Element0.Unload();
            Element1.Unload();
            Element2.Unload();
        }

        private void OnAcceleratorKeyActivated(Window sender, InputKeyDownEventArgs args)
        {
            var keyCode = (int)args.VirtualKey;

            if (args.VirtualKey is VirtualKey.Left or VirtualKey.GamepadLeftShoulder && args.OnlyKey)
            {
                ChangeView(CarouselDirection.Previous, false);
                args.Handled = true;
            }
            else if (args.VirtualKey is VirtualKey.Right or VirtualKey.GamepadRightShoulder && args.OnlyKey)
            {
                ChangeView(CarouselDirection.Next, false);
                args.Handled = true;
            }
            else if (args.VirtualKey is VirtualKey.C && args.OnlyControl)
            {
                ViewModel?.Copy();
                args.Handled = true;
            }
            else if (args.VirtualKey is VirtualKey.S && args.OnlyControl)
            {
                ViewModel?.Save();
                args.Handled = true;
            }
            else if (args.VirtualKey is VirtualKey.F11 || (args.VirtualKey is VirtualKey.F && args.OnlyControl))
            {
                FullScreen_Click(null, null);
                args.Handled = true;
            }
            else if (keyCode is 187 or 189 || args.VirtualKey is VirtualKey.Add or VirtualKey.Subtract)
            {
                ScrollingHost.Zoom(keyCode is 187 || args.VirtualKey is VirtualKey.Add);
                args.Handled = true;
            }
            else
            {
                Controls.OnAcceleratorKeyActivated(args);
            }
        }

        private void LayoutRoot_ViewChanging(object sender, CarouselViewChangingEventArgs e)
        {
            ChangeView(e.Direction);
        }

        private void LayoutRoot_ViewChanged(object sender, CarouselViewChangedEventArgs e)
        {
            if (ViewModel?.SelectedItem is GalleryMedia item && item.IsVideo && (item.IsLoop || SettingsService.Current.IsStreamingEnabled))
            {
                Play(LayoutRoot.CurrentElement, item);
            }
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            ChangeView(CarouselDirection.Previous, false);
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            ChangeView(CarouselDirection.Next, false);
        }

        #region Flippitiflip

        protected override Size ArrangeOverride(Size finalSize)
        {
            LayoutRoot.Width = finalSize.Width;
            LayoutRoot.Height = finalSize.Height - Padding.Top;

            static void Apply(AspectView element, Size size)
            {
                if (element.Constraint is not Size)
                {
                    element.MaxWidth = size.Width;
                    element.MaxHeight = size.Height;
                }
            }

            Apply(Element2, finalSize);
            Apply(Element0, finalSize);
            Apply(Element1, finalSize);

            if (_layout != null)
            {
                _layout.Offset = new Vector3(0, 0, 0);
            }

            return base.ArrangeOverride(finalSize);
        }

        private bool ChangeView(CarouselDirection direction, bool disableAnimation)
        {
            if (ChangeView(direction))
            {
                if (disableAnimation)
                {
                    return true;
                }

                LayoutRoot.ChangeView(direction);
                return true;
            }

            return false;
        }

        private bool ChangeView(CarouselDirection direction)
        {
            var viewModel = ViewModel;
            if (viewModel == null || LayoutRoot.IsScrolling)
            {
                return false;
            }

            var selected = viewModel.SelectedIndex;
            var previous = selected > 0;
            var next = selected < viewModel.Items.Count - 1;

            if (previous && direction == CarouselDirection.Previous)
            {
                viewModel.SelectedItem = viewModel.Items[selected - 1];
                PrepareNext(direction, dispose: true);

                viewModel.LoadMore();
                return true;
            }
            else if (next && direction == CarouselDirection.Next)
            {
                viewModel.SelectedItem = viewModel.Items[selected + 1];
                PrepareNext(direction, dispose: true);

                viewModel.LoadMore();
                return true;
            }

            return false;
        }

        private void PrepareNext(CarouselDirection direction, bool initialize = false, bool dispose = false)
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return;
            }

            LayoutRoot.PrepareElements(direction,
                out GalleryContent previous,
                out GalleryContent target,
                out GalleryContent next);

            previous.IsEnabled = false;
            target.IsEnabled = true;
            next.IsEnabled = false;

            var index = viewModel.SelectedIndex;
            if (index < 0 || viewModel.Items.Empty())
            {
                return;
            }

            var set = TrySet(target, viewModel.Items[index]);
            TrySet(previous, index > 0 ? viewModel.Items[index - 1] : null);
            TrySet(next, index < viewModel.Items.Count - 1 ? viewModel.Items[index + 1] : null);

            LayoutRoot.HasPrevious = index > 0;
            LayoutRoot.HasNext = index < viewModel.Items.Count - 1;

            if (UIViewSettings.GetForCurrentView().UserInteractionMode == UserInteractionMode.Mouse)
            {
                PrevButton.Visibility = index > 0 ? Visibility.Visible : Visibility.Collapsed;
                NextButton.Visibility = index < viewModel.Items.Count - 1 ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                PrevButton.Visibility = Visibility.Collapsed;
                NextButton.Visibility = Visibility.Collapsed;
            }

            if (set)
            {
                Dispose();
                viewModel.OpenMessage(viewModel.Items[index]);
            }
            else if (dispose)
            {
                Dispose();
            }

            var item = viewModel.Items[index];
            if (item.IsVideo /*&& initialize*/)
            {
                Play(target, item);
            }
        }

        private bool TrySet(GalleryContent element, GalleryMedia content)
        {
            if (Equals(element.Item, content))
            {
                return false;
            }

            element.UpdateItem(this, content);
            return true;
        }

        #endregion

        #region Context menu

        private void Menu_ContextRequested(object sender, RoutedEventArgs e)
        {
            Menu_ContextRequested(sender as UIElement, null);
        }

        private void Menu_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var viewModel = ViewModel;
            if (viewModel == null || sender is not FrameworkElement element)
            {
                return;
            }

            var item = viewModel.SelectedItem;
            if (item == null)
            {
                return;
            }

            var flyout = new MenuFlyout();

            PopulateContextRequested(flyout, viewModel, item);

            if (args != null)
            {
                args.ShowAt(flyout, element);
            }
            else
            {
                flyout.ShowAt(element, FlyoutPlacementMode.TopEdgeAlignedRight);
            }
        }

        private void PopulateContextRequested(MenuFlyout flyout, GalleryViewModelBase viewModel, GalleryMedia item)
        {
            flyout.CreateFlyoutItem(() => item.CanView, viewModel.View, Strings.ShowInChat, Icons.ChatEmpty);
            flyout.CreateFlyoutItem(() => item.CanShare, viewModel.Forward, Strings.Forward, Icons.Share);
            flyout.CreateFlyoutItem(() => item.CanCopy, viewModel.Copy, Strings.Copy, Icons.DocumentCopy, VirtualKey.C);
            flyout.CreateFlyoutItem(() => item.CanSave, viewModel.Save, Strings.SaveAs, Icons.SaveAs, VirtualKey.S);

            if (viewModel is UserPhotosViewModel userPhotos && userPhotos.CanDelete && userPhotos.SelectedIndex > 0)
            {
                flyout.CreateFlyoutItem(() => viewModel.CanDelete, userPhotos.SetAsMain, Strings.SetAsMain, Icons.PersonCircle);
            }
            else if (viewModel is ChatPhotosViewModel chatPhotos && chatPhotos.CanDelete && chatPhotos.SelectedIndex > 0)
            {
                flyout.CreateFlyoutItem(() => viewModel.CanDelete, chatPhotos.SetAsMain, Strings.SetAsMain, Icons.PersonCircle);
            }

            flyout.CreateFlyoutItem(() => viewModel.CanOpenWith, viewModel.OpenWith, Strings.OpenInExternalApp, Icons.OpenIn);
            flyout.CreateFlyoutItem(() => viewModel.CanDelete, viewModel.Delete, Strings.Delete, Icons.Delete, destructive: true);
        }

        #endregion

        #region Compact overlay

        private void Compact_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = ViewModel;
            var item = viewModel?.SelectedItem;

            if (item == null)
            {
                return;
            }

            //if (_mediaPlayer == null || _mediaPlayer.Source == null)
            {
                Play(LayoutRoot.CurrentElement, item);
            }

            var width = 320d;
            var height = 200d;

            var constraint = item.Constraint;
            if (constraint is MessageAnimation messageAnimation)
            {
                constraint = messageAnimation.Animation;
            }
            else if (constraint is MessageVideo messageVideo)
            {
                constraint = messageVideo.Video;
            }

            if (constraint is Animation animation)
            {
                width = animation.Width;
                height = animation.Height;
            }
            else if (constraint is Video video)
            {
                width = video.Width;
                height = video.Height;
            }

            if (width > 320 || height > 320)
            {
                var ratioX = 320d / width;
                var ratioY = 320d / height;
                var ratio = Math.Min(ratioX, ratioY);

                width *= ratio;
                height *= ratio;
            }

            var aggregator = TLContainer.Current.Resolve<IEventAggregator>();
            var viewService = TLContainer.Current.Resolve<IViewService>();

            //var mediaPlayer = _mediaPlayer;
            //var fileStream = _fileStream;

            _current.Stop(out int fileId, out long time);

            var fileStream = new RemoteFileStream(ViewModel.ClientService, item.GetFile());

            if (GalleryCompactWindow.Current is ViewLifetimeControl control)
            {
                control.Dispatcher.Dispatch(() =>
                {
                    if (control.Window.Content is GalleryCompactWindow player)
                    {
                        player.Play(viewModel, fileStream, time);
                    }

                    ApplicationView.GetForCurrentView().TryResizeView(new Size(width, height));
                });
            }
            else
            {
                var parameters = new ViewServiceParams
                {
                    ViewMode = ApplicationViewMode.CompactOverlay,
                    Width = width,
                    Height = height,
                    PersistentId = "PIP",
                    Content = control => new GalleryCompactWindow(control, viewModel, fileStream, time)
                };

                _ = viewService.OpenAsync(parameters);
            }

            OnBackRequestedOverride(this, new BackRequestedRoutedEventArgs());
        }

        #endregion

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            OnBackRequestedOverride(this, new BackRequestedRoutedEventArgs());
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            ScrollingHost.Zoom(true);
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            ScrollingHost.Zoom(false);
        }

        private bool _areInteractionsEnabled = true;

        private void ScrollingHost_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            var neutral = ScrollingHost.ZoomFactor.AlmostEquals(1);
            if (neutral == _areInteractionsEnabled)
            {
                Element2.Opacity = 1;
                Element0.Opacity = 1;
                Element1.Opacity = 1;
                return;
            }

            _areInteractionsEnabled = neutral;
            LayoutRoot.IsManipulationEnabled = neutral;

            ZoomIn.IsEnabled = ScrollingHost.CanZoomIn;
            ZoomOut.IsEnabled = ScrollingHost.CanZoomOut;

            var container = GetElement(CarouselDirection.None);
            container.IsEnabled = neutral;
            container.ManipulationMode = neutral ? ManipulationModes.TranslateY | ManipulationModes.TranslateRailsY | ManipulationModes.System : ManipulationModes.System;

            var container2 = GetElement(CarouselDirection.Previous);
            container2.Opacity = neutral ? 1 : 0;

            var container1 = GetElement(CarouselDirection.Next);
            container1.Opacity = neutral ? 1 : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private GalleryContent GetElement(CarouselDirection direction)
        {
            return LayoutRoot.GetElement(direction) as GalleryContent;
        }

        private void FullScreen_Click(object sender, RoutedEventArgs e)
        {
            var applicationView = ApplicationView.GetForCurrentView();
            if (applicationView.IsFullScreenMode)
            {
                applicationView.ExitFullScreenMode();
            }
            else
            {
                applicationView.TryEnterFullScreenMode();
            }
        }
    }
}
