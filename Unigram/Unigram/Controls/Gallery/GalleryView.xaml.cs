using System;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
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
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.System.Display;
using Windows.UI;
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
    public sealed partial class GalleryView : OverlayPage
        , INavigatingPage
        , IGalleryDelegate
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
        private Grid _surface;

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
        }

        private void OnTick(object sender, object e)
        {
            _inactivityTimer.Stop();
            ShowHideTransport(false);
        }

        protected override void OnPointerMoved(PointerRoutedEventArgs e)
        {
            ShowHideTransport(true);

            _inactivityTimer.Stop();
            _inactivityTimer.Start();
            base.OnPointerMoved(e);
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            base.OnPointerPressed(e);
        }

        private bool _transportCollapsed = false;

        private void ShowHideTransport(bool show)
        {
            if ((show && BottomPanel.Visibility == Visibility.Visible) || (!show && (BottomPanel.Visibility == Visibility.Collapsed || _transportCollapsed)))
            {
                return;
            }

            if (show)
            {
                _transportCollapsed = false;
            }
            else
            {
                _transportCollapsed = true;
            }

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
                if (viewModel != null && viewModel.FirstItem is GalleryMessage message && message.ChatId == update.ChatId && update.MessageIds.Any(x => x == message.Id))
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
                if (viewModel != null && viewModel.FirstItem is GalleryMessage message && message.Id == update.MessageId && message.ChatId == update.ChatId && (update.NewContent is MessageExpiredPhoto || update.NewContent is MessageExpiredVideo))
                {
                    Hide();
                }
            });
        }

        public void OpenFile(GalleryContent item, File file)
        {
            Play(item, file);
        }

        public void OpenItem(GalleryContent item)
        {
            OnBackRequested(new HandledEventArgs());
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

        public IAsyncOperation<ContentDialogResult> ShowAsync(GalleryViewModelBase parameter, Func<FrameworkElement> closing = null)
        {
            return AsyncInfo.Run(async token =>
            {
                _closing = closing;

                if (_closing != null)
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
                return await ShowAsync();
            });
        }

        private void OnCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            PrepareNext(0, true);
        }

        protected override void MaskTitleAndStatusBar()
        {
            var titlebar = ApplicationView.GetForCurrentView().TitleBar;
            //titlebar.BackgroundColor = Colors.Black;
            titlebar.ForegroundColor = Colors.White;
            //titlebar.ButtonBackgroundColor = Colors.Black;
            titlebar.ButtonForegroundColor = Colors.White;
        }

        public void OnBackRequesting(HandledEventArgs e)
        {
            if (!_wasFullScreen)
            {
                ApplicationView.GetForCurrentView().ExitFullScreenMode();
            }

            WindowContext.Current.EnableScreenCapture(ViewModel.GetHashCode());

            Unload();
            Dispose();

            e.Handled = true;
        }

        protected override void OnBackRequestedOverride(object sender, HandledEventArgs e)
        {
            if (!_wasFullScreen)
            {
                ApplicationView.GetForCurrentView().ExitFullScreenMode();
            }

            if (ViewModel != null)
            {
                ViewModel.Aggregator.Unsubscribe(this);
                ViewModel.Items.CollectionChanged -= OnCollectionChanged;

                Bindings.StopTracking();
            }

            var batch = _layer.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                Hide();
            };

            //var container = GetContainer(0);
            //var root = container.Presenter;
            if (ViewModel != null && ViewModel.SelectedItem == ViewModel.FirstItem && _closing != null)
            {
                ScrollingHost.Opacity = 0;
                Preview.Opacity = 1;

                var root = Preview.Presenter;
                if (root.IsLoaded)
                {
                    var animation = ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("FullScreenPicture", root);
                    if (animation != null)
                    {
                        animation.Configuration = new BasicConnectedAnimationConfiguration();

                        var element = _closing();
                        if (element.ActualWidth > 0)
                        {
                            animation.TryStart(element);
                        }
                        else
                        {
                            Hide();
                        }
                    }
                    else
                    {
                        Hide();
                    }
                }
                else
                {
                    Hide();
                }
            }
            else
            {
                _layout.StartAnimation("Offset.Y", CreateScalarAnimation(_layout.Offset.Y, (float)ActualHeight));
            }

            _layer.StartAnimation("Opacity", CreateScalarAnimation(1, 0));
            _bottom.StartAnimation("Opacity", CreateScalarAnimation(1, 0));
            batch.End();

            Unload();
            Dispose();

            e.Handled = true;
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

            ScrollingHost.Opacity = 0;
            Preview.Opacity = 1;

            var container = GetContainer(0);

            var animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("FullScreenPicture");
            if (animation != null)
            {
                animation.Configuration = new BasicConnectedAnimationConfiguration();

                _layer.StartAnimation("Opacity", CreateScalarAnimation(0, 1));
                _bottom.StartAnimation("Opacity", CreateScalarAnimation(0, 1));

                if (animation.TryStart(image.Presenter))
                {
                    void handler(ConnectedAnimation s, object args)
                    {
                        animation.Completed -= handler;

                        Transport.Show();
                        ScrollingHost.Opacity = 1;
                        Preview.Opacity = 0;

                        if (item.IsVideo && container != null)
                        {
                            Play(container.Presenter, item, item.GetFile());

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

                    animation.Completed += handler;

                    return;
                }
            }

            _layer.Opacity = 1;
            _bottom.Opacity = 1;

            Transport.Show();
            ScrollingHost.Opacity = 1;
            Preview.Opacity = 0;

            if (item.IsVideo && container != null)
            {
                Play(container.Presenter, item, item.GetFile());
            }
        }

        private CompositionAnimation CreateScalarAnimation(float from, float to)
        {
            var scalar = _layer.Compositor.CreateScalarKeyFrameAnimation();
            scalar.InsertKeyFrame(0, from);
            scalar.InsertKeyFrame(1, to, ConnectedAnimationService.GetForCurrentView().DefaultEasingFunction);
            scalar.Duration = ConnectedAnimationService.GetForCurrentView().DefaultDuration - TimeSpan.FromMilliseconds(50);

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

        private void Play(GalleryContent item, File file)
        {
            try
            {
                if (_surface != null && _mediaPlayerElement != null)
                {
                    _surface.Children.Remove(_mediaPlayerElement);
                }

                var container = GetContainer(0);
                //if (container != null && container.ContentTemplateRoot is Grid parent)
                //{
                //    Play(parent, item);
                //}

                Play(container.Presenter, item, file);
            }
            catch { }
        }

        private void Play(Grid parent, GalleryContent item, File file)
        {
            if (_unloaded)
            {
                return;
            }

            try
            {
                if (!file.Local.IsDownloadingCompleted && !SettingsService.Current.IsStreamingEnabled)
                {
                    return;
                }

                if (_surface != parent && _surface != null && _mediaPlayerElement != null)
                {
                    _surface.Children.Remove(_mediaPlayerElement);
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
                _mediaPlayer.SetSurfaceSize(new Size(parent.ActualWidth * dpi, parent.ActualHeight * dpi));

                if (_surface != parent)
                {
                _surface = parent;
                _surface.Children.Add(_mediaPlayerElement);
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
            Element2.Reset();
            Element0.Reset();
            Element1.Reset();

            if (_surface != null)
            {
                _surface.Children.Remove(_mediaPlayerElement);
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
                ChangeView(0, false);
                args.Handled = true;
            }
            else if (args.VirtualKey is Windows.System.VirtualKey.Right or Windows.System.VirtualKey.GamepadRightShoulder)
            {
                ChangeView(2, false);
                args.Handled = true;
            }
            else if (args.VirtualKey is Windows.System.VirtualKey.Space && !ctrl && !alt && !shift)
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

                    args.Handled = true;
                }
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
                var container = GetContainer(0);
                container.Zoom(keyCode is 187 || args.VirtualKey is Windows.System.VirtualKey.Add);
                args.Handled = true;
            }
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            ChangeView(0, false);
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            ChangeView(2, false);
        }

        #region Flippitiflip

        protected override Size MeasureOverride(Size availableSize)
        {
            var size = base.MeasureOverride(availableSize);

            LayoutRoot.SnapPointWidth = (float)availableSize.Width;
            LayoutRoot.Height = availableSize.Height - Padding.Top;

            Element0.Width = availableSize.Width;
            Element1.Width = availableSize.Width;
            Element2.Width = availableSize.Width;

            if (_layout != null)
            {
                _layout.Offset = new Vector3(0, 0, 0);
            }

            return size;
        }

        private void LayoutRoot_HorizontalSnapPointsChanged(object sender, object e)
        {
            ScrollingHost.HorizontalSnapPointsType = SnapPointsType.None;
            ScrollingHost.ChangeView(LayoutRoot.SnapPointWidth, null, null, true);
            ScrollingHost.HorizontalSnapPointsType = SnapPointsType.MandatorySingle;
        }

        private bool _needFinal;

        private void ScrollingHost_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            // We want to update containers when the view has been scrolled to the start/end or when the change has ended
            if (ScrollingHost.HorizontalOffset == 0 || ScrollingHost.HorizontalOffset == ScrollingHost.ScrollableWidth || (_needFinal && !e.IsIntermediate))
            {
                _needFinal = false;

                var viewModel = ViewModel;
                if (viewModel == null)
                {
                    // Page is most likely being closed, just reset the view
                    LayoutRoot_HorizontalSnapPointsChanged(LayoutRoot, null);
                    return;
                }

                var selected = viewModel.SelectedIndex;
                var previous = selected > 0;
                var next = selected < viewModel.Items.Count - 1;

#if GALLERY_EXPERIMENTAL
                var difference = previous ? 0 : LayoutRoot.SnapPointWidth;
#else
                var difference = 0;
#endif

                var index = Math.Round((difference + ScrollingHost.HorizontalOffset) / LayoutRoot.SnapPointWidth);
                if (index == 0 && previous)
                {
                    viewModel.SelectedItem = viewModel.Items[selected - 1];
                    PrepareNext(-1, dispose: true);
                }
                else if (index == 2 && next)
                {
                    viewModel.SelectedItem = viewModel.Items[selected + 1];
                    PrepareNext(+1, dispose: true);
                }

                viewModel.LoadMore();

#if GALLERY_EXPERIMENTAL
                if (ViewModel.SelectedIndex == 0 || !previous)
                {
                    //ScrollingHost.ChangeView(index == 2 ? LayoutRoot.SnapPointWidth : 0, null, null, true);
                    ScrollingHost.ChangeView(0, null, null, true);
                }
                else
#endif
                {
                    ScrollingHost.HorizontalSnapPointsType = SnapPointsType.None;
                    ScrollingHost.ChangeView(LayoutRoot.SnapPointWidth, null, null, true);
                    ScrollingHost.HorizontalSnapPointsType = SnapPointsType.MandatorySingle;
                }
            }
            else
            {
                _needFinal = true;
            }
        }

        private bool ChangeView(int index, bool disableAnimation)
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return false;
            }

            var selected = viewModel.SelectedIndex;
            var previous = selected > 0;
            var next = selected < viewModel.Items.Count - 1;

            if (previous && index == 0)
            {
                viewModel.SelectedItem = viewModel.Items[selected - 1];
                PrepareNext(-1, dispose: true);

                viewModel.LoadMore();

                return true;
            }
            else if (next && index == 2)
            {
                viewModel.SelectedItem = viewModel.Items[selected + 1];
                PrepareNext(+1, dispose: true);

                viewModel.LoadMore();

                return true;
            }

            return false;
            //ScrollingHost.ChangeView(ActualWidth * index, null, null, disableAnimation);
        }

        private void PrepareNext(int direction, bool initialize = false, bool dispose = false)
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return;
            }

            GalleryContentView previous = null;
            GalleryContentView target = null;
            GalleryContentView next = null;
            if (Grid.GetColumn(Element1) == direction + 1)
            {
                previous = Element0;
                target = Element1;
                next = Element2;
            }
            else if (Grid.GetColumn(Element0) == direction + 1)
            {
                previous = Element2;
                target = Element0;
                next = Element1;
            }
            else if (Grid.GetColumn(Element2) == direction + 1)
            {
                previous = Element1;
                target = Element2;
                next = Element0;
            }

            Grid.SetColumn(target, 1);
            Grid.SetColumn(previous, 0);
            Grid.SetColumn(next, 2);

            var index = viewModel.SelectedIndex;
            if (index < 0 || viewModel.Items.IsEmpty())
            {
                return;
            }

            var set = TrySet(target, viewModel.Items[index]);
            TrySet(previous, index > 0 ? viewModel.Items[index - 1] : null);
            TrySet(next, index < viewModel.Items.Count - 1 ? viewModel.Items[index + 1] : null);

            if (initialize)
            {
                TrySet(Preview, viewModel.Items[index]);
            }

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

#if GALLERY_EXPERIMENTAL
            LayoutRoot.ColumnDefinitions[0].Width = new GridLength(0, index > 0 ? GridUnitType.Auto : GridUnitType.Pixel);
            LayoutRoot.ColumnDefinitions[2].Width = new GridLength(0, index < viewModel.Items.Count - 1 ? GridUnitType.Auto : GridUnitType.Pixel);
#endif

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
            if (item.IsVideo && (item.IsLoop || initialize))
            {
                Play(target.Presenter, item, item.GetFile());
            }
        }

        private GalleryContentView GetContainer(int direction)
        {
            if (Grid.GetColumn(Element1) == direction + 1)
            {
                return Element1;
            }
            else if (Grid.GetColumn(Element0) == direction + 1)
            {
                return Element0;
            }
            else if (Grid.GetColumn(Element2) == direction + 1)
            {
                return Element2;
            }

            return null;
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
            var viewModel = ViewModel;
            if (viewModel == null)
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

            flyout.ShowAt(sender as FrameworkElement, new FlyoutShowOptions { Placement = FlyoutPlacementMode.BottomEdgeAlignedRight });
        }

        private void ImageView_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return;
            }

            var element = sender as FrameworkElement;
            if (element == null)
            {
                return;
            }

            var item = element.Tag as GalleryContent;
            if (item == null)
            {
                return;
            }

            var flyout = new MenuFlyout();

            PopulateContextRequested(flyout, viewModel, item);

            args.ShowAt(flyout, element);
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
                Play(item, item.GetFile());
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
            var container = GetContainer(0);
            container.Zoom(true);
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            var container = GetContainer(0);
            container.Zoom(false);
        }

        private void ImageView_InteractionsEnabledChanged(object sender, EventArgs e)
        {
            //var container = GetContainer(0);
            //ZoomIn.IsEnabled = container.CanZoomIn;
            //ZoomOut.IsEnabled = container.CanZoomOut;

            IsLightDismissEnabled = Element2.AreInteractionsEnabled
                && Element0.AreInteractionsEnabled
                && Element1.AreInteractionsEnabled;
        }
    }
}
