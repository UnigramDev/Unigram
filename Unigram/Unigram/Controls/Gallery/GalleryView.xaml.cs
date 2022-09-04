using System;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Navigation;
using Unigram.Services;
using Unigram.Services.ViewService;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Gallery;
using Unigram.Views;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.System.Display;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Controls.Gallery
{
    public sealed partial class GalleryView : OverlayPage, IGalleryDelegate
    //, IHandle<UpdateDeleteMessages>
    //, IHandle<UpdateMessageContent>
    {
        public GalleryViewModelBase ViewModel => DataContext as GalleryViewModelBase;

        public IProtoService ProtoService => ViewModel.ProtoService;

        private Func<FrameworkElement> _closing;

        private readonly DispatcherTimer _inactivityTimer;

        private DisplayRequest _request;
        private readonly MediaPlayerElement _mediaPlayerElement;
        private MediaPlayer _mediaPlayer;
        private RemoteFileStream _fileStream;
        private Border _surface;

        private readonly Visual _layout;

        private readonly Visual _layer;
        private readonly Visual _bottom;

        private bool _wasFullScreen;
        private bool _unloaded;

        private int? _initialPosition;

        public int InitialPosition
        {
            get => _initialPosition ?? 0;
            set => _initialPosition = value > 0 ? value : null;
        }

        private GalleryView()
        {
            InitializeComponent();

            _layout = ElementCompositionPreview.GetElementVisual(LayoutRoot);

            _layer = ElementCompositionPreview.GetElementVisual(Layer);
            _bottom = ElementCompositionPreview.GetElementVisual(BottomPanel);

            _layer.Opacity = 0;
            _bottom.Opacity = 0;

            _mediaPlayerElement = new MediaPlayerElement { Style = Resources["TransportLessMediaPlayerStyle"] as Style };
            _mediaPlayerElement.AreTransportControlsEnabled = true;
            _mediaPlayerElement.TransportControls = Transport;
            _mediaPlayerElement.SetMediaPlayer(_mediaPlayer);

            _inactivityTimer = new DispatcherTimer();
            _inactivityTimer.Tick += OnTick;
            _inactivityTimer.Interval = TimeSpan.FromSeconds(2);
            _inactivityTimer.Start();

            Transport.Visibility = Visibility.Collapsed;

            ScrollingHost.AddHandler(PointerPressedEvent, new PointerEventHandler(OnPointerPressed), true);
            ScrollingHost.AddHandler(PointerWheelChangedEvent, new PointerEventHandler(OnPointerWheelChanged), true);
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

            var point = e.GetCurrentPoint(Transport);
            if (point.Position.X < 0
                || point.Position.Y < 0
                || point.Position.X > Transport.ActualWidth
                || point.Position.Y > Transport.ActualHeight)
            {
                _inactivityTimer.Start();
            }

            base.OnPointerMoved(e);
        }

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint((UIElement)_surface ?? this);
            if (point.Properties.IsLeftButtonPressed is false || !_areInteractionsEnabled || e.Handled)
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
                if (_surface != null
                    && point.Position.X >= 0
                    && point.Position.Y >= 0
                    && point.Position.X <= _surface.ActualWidth
                    && point.Position.Y <= _surface.ActualHeight)
                {
                    TogglePlaybackState();
                    return;
                }

                OnBackRequested(new BackRequestedRoutedEventArgs());
            }
        }

        private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs args)
        {
            var a = args.GetCurrentPoint(ScrollingHost).Properties.MouseWheelDelta;
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

            batch.End();
        }

        public void Handle(UpdateDeleteMessages update)
        {
            this.BeginOnUIThread(() =>
            {
                var viewModel = ViewModel;
                if (viewModel != null && viewModel.SelectedItem is GalleryMessage message && message.ChatId == update.ChatId && update.MessageIds.Any(x => x == message.Id))
                {
                    Hide();
                }
            });
        }

        public void Handle(UpdateMessageContent update)
        {
            this.BeginOnUIThread(() =>
            {
                var viewModel = ViewModel;
                if (viewModel != null && viewModel.SelectedItem is GalleryMessage message && message.Id == update.MessageId && message.ChatId == update.ChatId && (update.NewContent is MessageExpiredPhoto || update.NewContent is MessageExpiredVideo))
                {
                    Hide();
                }
            });
        }

        public void OpenFile(GalleryContent item, File file)
        {
            Play(LayoutRoot.CurrentElement, item);
        }

        public void OpenItem(GalleryContent item)
        {
            OnBackRequested(new BackRequestedRoutedEventArgs());
        }

        private void OnSourceChanged(MediaPlayer sender, object args)
        {
            OnSourceChanged();
        }

        private async void OnSourceChanged()
        {
            var source = _mediaPlayer == null || _mediaPlayer.Source == null;
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Transport.Visibility = source ? Visibility.Collapsed : Visibility.Visible;
                //Details.Visibility = _mediaPlayer == null || _mediaPlayer.Source == null ? Visibility.Visible : Visibility.Collapsed;
                //Caption.Visibility = _mediaPlayer == null || _mediaPlayer.Source == null ? Visibility.Visible : Visibility.Collapsed;
                Element0.IsHitTestVisible = source;
                Element1.IsHitTestVisible = source;
                Element2.IsHitTestVisible = source;

                if (_request != null && source)
                {
                    _request.RequestRelease();
                    _request = null;
                }
            });
        }

        private async void OnPlaybackStateChanged(MediaPlaybackSession sender, object args)
        {
            var state = sender.PlaybackState;
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                switch (state)
                {
                    case MediaPlaybackState.Opening:
                    case MediaPlaybackState.Buffering:
                    case MediaPlaybackState.Playing:
                        if (_request == null)
                        {
                            _request = new DisplayRequest();
                            _request.RequestActive();
                        }
                        break;
                    default:
                        if (_request != null)
                        {
                            _request.RequestRelease();
                            _request = null;
                        }
                        break;
                }
            });
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

        public static GalleryView GetForCurrentView()
        {
            return new GalleryView();
        }

        public Task<ContentDialogResult> ShowAsync(GalleryViewModelBase parameter, Func<FrameworkElement> closing = null)
        {
            _closing = closing;

            if (_closing != null && IsConstrainedToRootBounds)
            {
                ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("FullScreenPicture", _closing());
            }

            parameter.Items.CollectionChanged -= OnCollectionChanged;
            parameter.Items.CollectionChanged += OnCollectionChanged;

            Load(parameter);

            PrepareNext(0, true);

            RoutedEventHandler handler = null;
            handler = new RoutedEventHandler(async (s, args) =>
            {
                if (_unloaded)
                {
                    return;
                }

                _wasFullScreen = ApplicationView.GetForCurrentView().IsFullScreenMode;

                Transport.Focus(FocusState.Programmatic);

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

        private void OnCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            PrepareNext(0, true);
        }

        protected override void OnBackRequestedOverride(object sender, HandledEventArgs e)
        {
            e.Handled = true;

            if (!_wasFullScreen)
            {
                ApplicationView.GetForCurrentView().ExitFullScreenMode();
            }

            WindowContext.Current.EnableScreenCapture(ViewModel.GetHashCode());

            if (ViewModel != null)
            {
                ViewModel.Aggregator.Unsubscribe(this);
                ViewModel.Items.CollectionChanged -= OnCollectionChanged;

                Bindings.StopTracking();
            }

            var translate = true;

            if (ViewModel != null && ViewModel.SelectedItem == ViewModel.FirstItem && _closing != null)
            {
                var root = LayoutRoot.CurrentElement;
                if (root.IsLoaded && IsConstrainedToRootBounds)
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

            var image = sender as GalleryContentView;
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

                        Transport.Show();
                        InitializeVideo(container, item);
                    }

                    animation.Completed += handler;
                    return;
                }
            }

            Transport.Show();
            InitializeVideo(container, item);
        }

        private void InitializeVideo(Grid container, GalleryContent item)
        {
            if (item.IsVideo)
            {
                Play(container, item);

                if (GalleryCompactView.Current is ViewLifetimeControl compact)
                {
                    compact.Dispatcher.Dispatch(() =>
                    {
                        if (compact.Window.Content is GalleryCompactView player)
                        {
                            player.MediaPlayer?.Pause();
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
                return user.GetFullName();
            }
            else if (with is Chat chat)
            {
                return chat.Title;
            }

            return null;
        }

        private string ConvertDate(int value)
        {
            var date = Converter.DateTime(value);
            return string.Format(Strings.Resources.formatDateAtTime, Converter.ShortDate.Format(date), Converter.ShortTime.Format(date));
        }

        private string ConvertOf(int index, int count)
        {
            return string.Format(Strings.Resources.Of, index, count);
        }

        private Visibility ConvertCompactVisibility(GalleryContent item)
        {
            if (item != null && item.IsVideo && !item.IsLoop)
            {
                if (item is GalleryMessage message && message.IsProtected)
                {
                    return Visibility.Collapsed;
                }

                return ApplicationView.GetForCurrentView().IsViewModeSupported(ApplicationViewMode.CompactOverlay) ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }

        #endregion

        private void Play(FrameworkElement container, GalleryContent item)
        {
            if (_unloaded || container is not GalleryContentView content)
            {
                return;
            }

            try
            {
                var file = item.GetFile();
                if (!file.Local.IsDownloadingCompleted && !SettingsService.Current.IsStreamingEnabled)
                {
                    return;
                }

                if (_surface != content.Presenter && _surface != null && _mediaPlayerElement != null)
                {
                    _surface.Child = null;
                    _surface = null;
                }

                if (_mediaPlayer == null)
                {
                    _mediaPlayer = Task.Run(() => new MediaPlayer()).Result;
                    _mediaPlayer.VolumeChanged += OnVolumeChanged;
                    _mediaPlayer.SourceChanged += OnSourceChanged;
                    _mediaPlayer.MediaOpened += OnMediaOpened;
                    _mediaPlayer.PlaybackSession.PlaybackStateChanged += OnPlaybackStateChanged;
                    _mediaPlayerElement.SetMediaPlayer(_mediaPlayer);
                }

                var dpi = WindowContext.Current.RasterizationScale;
                _mediaPlayer.SetSurfaceSize(new Size(
                    content.Presenter.ActualWidth * dpi,
                    content.Presenter.ActualHeight * dpi));

                if (_surface != content.Presenter)
                {
                    _surface = content.Presenter;
                    _surface.Child = _mediaPlayerElement;
                }

                //Transport.DownloadMaximum = file.Size;
                //Transport.DownloadValue = file.Local.DownloadOffset + file.Local.DownloadedPrefixSize;

                var streamable = SettingsService.Current.IsStreamingEnabled && item.IsStreamable /*&& !file.Local.IsDownloadingCompleted*/;
                if (streamable)
                {
                    if (_fileStream == null || _fileStream.FileId != file.Id)
                    {
                        _fileStream = new RemoteFileStream(item.ProtoService, file, item.Duration);
                        _mediaPlayer.Source = MediaSource.CreateFromStream(_fileStream, item.MimeType);
                    }

                    //Transport.DownloadMaximum = file.Size;
                    //Transport.DownloadValue = file.Local.DownloadOffset + file.Local.DownloadedPrefixSize;
                }
                else
                {
                    _mediaPlayer.Source = MediaSource.CreateFromUri(UriEx.ToLocal(file.Local.Path));
                }

                if (_initialPosition is int initialPosition)
                {
                    _initialPosition = null;
                    _mediaPlayer.PlaybackSession.Position = TimeSpan.FromSeconds(initialPosition);
                }

                _mediaPlayer.IsLoopingEnabled = item.IsLoop;
                _mediaPlayer.Play();
            }
            catch { }
        }

        private void Dispose()
        {
            ScrollingHost.Zoom(1, true);

            if (_surface != null)
            {
                _surface.Child = null;
                _surface = null;
            }

            if (_fileStream != null)
            {
                if (GalleryCompactView.Current == null)
                {
                    _fileStream.Dispose();
                }

                _fileStream = null;
            }

            if (_mediaPlayer != null)
            {
                _mediaPlayer.VolumeChanged -= OnVolumeChanged;
                _mediaPlayer.SourceChanged -= OnSourceChanged;
                _mediaPlayer.MediaOpened -= OnMediaOpened;
                _mediaPlayer.PlaybackSession.PlaybackStateChanged -= OnPlaybackStateChanged;

                _mediaPlayerElement.SetMediaPlayer(null);
                //_mediaPlayerElement.AreTransportControlsEnabled = false;
                //_mediaPlayerElement.TransportControls = null;
                //_mediaPlayerElement = null;

                // TODO: it probably leaks
                if (GalleryCompactView.Current == null)
                {
                    _mediaPlayer.Dispose();
                }

                _mediaPlayer = null;
                OnSourceChanged();
            }

            if (_request != null)
            {
                _request.RequestRelease();
                _request = null;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            WindowContext.Current.AcceleratorKeyActivated += OnAcceleratorKeyActivated;
        }

        private void Load(object parameter)
        {
            DataContext = parameter;
            Bindings.Update();

            if (ViewModel != null)
            {
                ViewModel.Aggregator.Subscribe<UpdateDeleteMessages>(this, Handle)
                    .Subscribe<UpdateDeleteMessages>(Handle);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Unload();
            WindowContext.Current.AcceleratorKeyActivated -= OnAcceleratorKeyActivated;
        }

        private void Unload()
        {
            _unloaded = true;

            if (ViewModel != null)
            {
                ViewModel.Aggregator.Unsubscribe(this);
            }

            DataContext = null;
            Bindings.StopTracking();
        }

        private bool TogglePlaybackState()
        {
            if (_mediaPlayer != null)
            {
                if (_mediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Paused)
                {
                    _mediaPlayer.Play();
                }
                else
                {
                    _mediaPlayer.Pause();
                }

                return true;
            }

            return false;
        }

        private void OnAcceleratorKeyActivated(CoreDispatcher sender, AcceleratorKeyEventArgs args)
        {
            if (args.EventType is not CoreAcceleratorKeyEventType.KeyDown and not CoreAcceleratorKeyEventType.SystemKeyDown)
            {
                return;
            }

            var alt = Window.Current.CoreWindow.GetKeyState(Windows.System.VirtualKey.Menu).HasFlag(CoreVirtualKeyStates.Down);
            var ctrl = Window.Current.CoreWindow.GetKeyState(Windows.System.VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
            var shift = Window.Current.CoreWindow.GetKeyState(Windows.System.VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);

            var keyCode = (int)args.VirtualKey;

            if (args.VirtualKey is Windows.System.VirtualKey.Left or Windows.System.VirtualKey.GamepadLeftShoulder)
            {
                ChangeView(CarouselDirection.Previous, false);
                args.Handled = true;
            }
            else if (args.VirtualKey is Windows.System.VirtualKey.Right or Windows.System.VirtualKey.GamepadRightShoulder)
            {
                ChangeView(CarouselDirection.Next, false);
                args.Handled = true;
            }
            else if (args.VirtualKey is Windows.System.VirtualKey.Space && !ctrl && !alt && !shift)
            {
                args.Handled = TogglePlaybackState();
            }
            else if (args.VirtualKey is Windows.System.VirtualKey.C && ctrl && !alt && !shift)
            {
                ViewModel?.CopyCommand.Execute();
                args.Handled = true;
            }
            else if (args.VirtualKey is Windows.System.VirtualKey.S && ctrl && !alt && !shift)
            {
                ViewModel?.SaveCommand.Execute();
                args.Handled = true;
            }
            else if (keyCode is 187 or 189 || args.VirtualKey is Windows.System.VirtualKey.Add or Windows.System.VirtualKey.Subtract)
            {
                ScrollingHost.Zoom(keyCode is 187 || args.VirtualKey is Windows.System.VirtualKey.Add);
                args.Handled = true;
            }
        }

        private void LayoutRoot_ViewChanging(object sender, CarouselViewChangingEventArgs e)
        {
            ChangeView(e.Direction);
        }

        private void LayoutRoot_ViewChanged(object sender, CarouselViewChangedEventArgs e)
        {
            if (ViewModel?.SelectedItem is GalleryContent item && item.IsVideo && (item.IsLoop || SettingsService.Current.IsStreamingEnabled))
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

            Element0.MaxWidth = Element1.MaxWidth = Element2.MaxWidth = finalSize.Width;
            Element0.MaxHeight = Element1.MaxHeight = Element2.MaxHeight = finalSize.Height - Padding.Top;

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
            if (viewModel == null)
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
                out GalleryContentView previous,
                out GalleryContentView target,
                out GalleryContentView next);

            var index = viewModel.SelectedIndex;
            if (index < 0 || viewModel.Items.IsEmpty())
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
            if (item.IsVideo && initialize)
            {
                Play(target, item);
            }
        }

        private bool TrySet(GalleryContentView element, GalleryContent content)
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
                flyout.ShowAt(element, new FlyoutShowOptions { Placement = FlyoutPlacementMode.BottomEdgeAlignedRight });
            }
        }

        private void PopulateContextRequested(MenuFlyout flyout, GalleryViewModelBase viewModel, GalleryContent item)
        {
            if (item.IsVideo && !item.IsLoop && _mediaPlayer != null)
            {
                var rates = new double[] { 0.25, 0.5, 1, 1.5, 2 };
                var labels = new string[] { Strings.Resources.SpeedVerySlow, Strings.Resources.SpeedSlow, Strings.Resources.SpeedNormal, Strings.Resources.SpeedFast, Strings.Resources.SpeedVeryFast };

                var command = new RelayCommand<double>(rate =>
                {
                    _mediaPlayer.PlaybackSession.PlaybackRate = rate;
                });

                var speed = new MenuFlyoutSubItem();
                speed.Text = Strings.Resources.Speed;
                speed.Icon = new FontIcon { Glyph = Icons.TopSpeed, FontFamily = BootStrapper.Current.Resources["TelegramThemeFontFamily"] as FontFamily };

                for (int i = 0; i < rates.Length; i++)
                {
                    var rate = rates[i];
                    var toggle = new ToggleMenuFlyoutItem
                    {
                        Text = labels[i],
                        IsChecked = _mediaPlayer.PlaybackSession.PlaybackRate == rate,
                        CommandParameter = rate,
                        Command = command
                    };

                    speed.Items.Add(toggle);
                }

                flyout.Items.Add(speed);
                flyout.CreateFlyoutSeparator();
            }

            flyout.CreateFlyoutItem(x => item.CanView, viewModel.ViewCommand, item, Strings.Resources.ShowInChat, new FontIcon { Glyph = Icons.Comment });
            flyout.CreateFlyoutItem(x => item.CanShare, viewModel.ForwardCommand, item, Strings.Resources.Forward, new FontIcon { Glyph = Icons.Share });
            flyout.CreateFlyoutItem(x => item.CanCopy, viewModel.CopyCommand, item, Strings.Resources.Copy, new FontIcon { Glyph = Icons.DocumentCopy }, Windows.System.VirtualKey.C);
            flyout.CreateFlyoutItem(x => item.CanSave, viewModel.SaveCommand, item, Strings.Resources.lng_mediaview_save_as, new FontIcon { Glyph = Icons.SaveAs }, Windows.System.VirtualKey.S);
            flyout.CreateFlyoutItem(x => viewModel.CanOpenWith, viewModel.OpenWithCommand, item, Strings.Resources.OpenInExternalApp, new FontIcon { Glyph = Icons.OpenIn });
            flyout.CreateFlyoutItem(x => viewModel.CanDelete, viewModel.DeleteCommand, item, Strings.Resources.Delete, new FontIcon { Glyph = Icons.Delete });
        }

        #endregion

        #region Compact overlay

        private async void Compact_Click(object sender, RoutedEventArgs e)
        {
            var item = ViewModel?.SelectedItem;
            if (item == null)
            {
                return;
            }

            if (_mediaPlayer == null || _mediaPlayer.Source == null)
            {
                Play(LayoutRoot.CurrentElement, item);
            }

            _mediaPlayerElement.SetMediaPlayer(null);

            var width = 360d;
            var height = 225d;

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

            if (width > 360 || height > 360)
            {
                var ratioX = 360d / width;
                var ratioY = 360d / height;
                var ratio = Math.Min(ratioX, ratioY);

                width *= ratio;
                height *= ratio;
            }

            var aggregator = TLContainer.Current.Resolve<IEventAggregator>();
            var viewService = TLContainer.Current.Resolve<IViewService>();

            var mediaPlayer = _mediaPlayer;
            var fileStream = _fileStream;

            if (GalleryCompactView.Current is ViewLifetimeControl control)
            {
                control.Dispatcher.Dispatch(() =>
                {
                    if (control.Window.Content is GalleryCompactView player)
                    {
                        player.Update(mediaPlayer, fileStream);
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
                    Content = control => new GalleryCompactView(control, mediaPlayer, fileStream)
                };

                await viewService.OpenAsync(parameters);
            }

            OnBackRequestedOverride(this, new HandledEventArgs());
        }

        #endregion

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            OnBackRequestedOverride(this, new HandledEventArgs());
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
        private GalleryContentView GetElement(CarouselDirection direction)
        {
            return LayoutRoot.GetElement(direction) as GalleryContentView;
        }
    }
}
