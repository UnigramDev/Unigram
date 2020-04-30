﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Template10.Common;
using Unigram.Converters;
using Unigram.ViewModels;
using Unigram.Views;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;
using LinqToVisualTree;
using Windows.Foundation.Metadata;
using Windows.UI;
using Microsoft.Graphics.Canvas.Effects;
using Windows.UI.ViewManagement;
using Windows.System.Display;
using Unigram.Common;
using Windows.Graphics.Display;
using Telegram.Td.Api;
using System.Windows.Input;
using Windows.Storage.Streams;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Template10.Services.ViewService;
using Unigram.ViewModels.Gallery;
using Unigram.Controls.Gallery;
using Unigram.Native.Streaming;
using System.ComponentModel;

namespace Unigram.Controls.Gallery
{
    public sealed partial class GalleryView : OverlayPage, INavigatingPage, IGalleryDelegate, IFileDelegate, IHandle<UpdateFile>, IHandle<UpdateDeleteMessages>, IHandle<UpdateMessageContent>
    {
        public GalleryViewModelBase ViewModel => DataContext as GalleryViewModelBase;

        public BindConvert Convert => BindConvert.Current;

        private Func<FrameworkElement> _closing;

        private DisplayRequest _request;
        private MediaPlayerElement _mediaPlayerElement;
        private MediaPlayer _mediaPlayer;
        private FFmpegInteropMSS _streamingInterop;
        private Grid _surface;

        private Visual _layer;

        private bool _wasFullScreen;

        private GalleryView()
        {
            InitializeComponent();

            //CreateKeyboardAccelerator(Windows.System.VirtualKey.C);
            //CreateKeyboardAccelerator(Windows.System.VirtualKey.S);
            //CreateKeyboardAccelerator(Windows.System.VirtualKey.Left, Windows.System.VirtualKeyModifiers.None);
            //CreateKeyboardAccelerator(Windows.System.VirtualKey.GamepadLeftShoulder, Windows.System.VirtualKeyModifiers.None);
            //CreateKeyboardAccelerator(Windows.System.VirtualKey.Right, Windows.System.VirtualKeyModifiers.None);
            //CreateKeyboardAccelerator(Windows.System.VirtualKey.GamepadRightShoulder, Windows.System.VirtualKeyModifiers.None);

            _layer = ElementCompositionPreview.GetElementVisual(Layer);
            _layer.Opacity = 0;

            _mediaPlayerElement = new MediaPlayerElement { Style = Resources["TransportLessMediaPlayerStyle"] as Style };
            _mediaPlayerElement.AreTransportControlsEnabled = true;
            _mediaPlayerElement.TransportControls = Transport;
            _mediaPlayerElement.SetMediaPlayer(_mediaPlayer);

            Initialize();
        }

        private void CreateKeyboardAccelerator(Windows.System.VirtualKey key, Windows.System.VirtualKeyModifiers modifiers = Windows.System.VirtualKeyModifiers.Control)
        {
            if (ApiInformation.IsPropertyPresent("Windows.UI.Xaml.UIElement", "KeyboardAccelerators"))
            {
                var accelerator = new KeyboardAccelerator { Modifiers = modifiers, Key = key, ScopeOwner = this };
                accelerator.Invoked += FlyoutAccelerator_Invoked;

                Transport.KeyboardAccelerators.Add(accelerator);
            }
        }

        private void FlyoutAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (sender.Key == Windows.System.VirtualKey.C && sender.Modifiers == Windows.System.VirtualKeyModifiers.Control)
            {
                ViewModel.CopyCommand.Execute();
                args.Handled = true;
            }
            else if (sender.Key == Windows.System.VirtualKey.S && sender.Modifiers == Windows.System.VirtualKeyModifiers.Control)
            {
                ViewModel.SaveCommand.Execute();
                args.Handled = true;
            }
            else if (sender.Key == Windows.System.VirtualKey.Left || sender.Key == Windows.System.VirtualKey.GamepadLeftShoulder)
            {
                ChangeView(0, false);
                args.Handled = true;
            }
            else if (sender.Key == Windows.System.VirtualKey.Right || sender.Key == Windows.System.VirtualKey.GamepadRightShoulder)
            {
                ChangeView(2, false);
                args.Handled = true;
            }
        }

        public void Handle(UpdateFile update)
        {
            _streamingInterop?.UpdateFile(update.File);
            this.BeginOnUIThread(() => UpdateFile(update.File));
        }

        public void Handle(UpdateDeleteMessages update)
        {

        }

        public void Handle(UpdateMessageContent update)
        {
            this.BeginOnUIThread(() =>
            {
                var viewModel = ViewModel;
                if (viewModel != null && viewModel.FirstItem is GalleryMessage message && message.Id == update.MessageId && (update.NewContent is MessageExpiredPhoto || update.NewContent is MessageExpiredVideo))
                {
                    OnBackRequestedOverride(this, new HandledEventArgs());
                }
            });
        }

        public void UpdateFile(File file)
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return;
            }

            foreach (var item in viewModel.Items)
            {
                if (item.UpdateFile(file))
                {
                    if (Element0.Item == item)
                    {
                        Element0.UpdateFile(item, file);
                    }

                    if (Element1.Item == item)
                    {
                        Element1.UpdateFile(item, file);
                    }

                    if (Element2.Item == item)
                    {
                        Element2.UpdateFile(item, file);
                    }

                    if (_streamingInterop?.FileId == file.Id)
                    {
                        Transport.DownloadMaximum = file.Size;
                        Transport.DownloadValue = file.Local.DownloadOffset + file.Local.DownloadedPrefixSize;
                    }
                }
            }
        }

        public void OpenFile(GalleryContent item, File file)
        {
            Play(item, file);
        }

        public void OpenItem(GalleryContent item)
        {
            if (Transport.IsVisible)
            {
                Transport.Hide();
            }
            else
            {
                Transport.Show();
            }
        }

        private void OnSourceChanged(MediaPlayer sender, object args)
        {
            OnSourceChanged();
        }

        private async void OnSourceChanged()
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Transport.TransportVisibility = _mediaPlayer == null || _mediaPlayer.Source == null ? Visibility.Collapsed : Visibility.Visible;
                Details.Visibility = _mediaPlayer == null || _mediaPlayer.Source == null ? Visibility.Visible : Visibility.Collapsed;
                Caption.Visibility = _mediaPlayer == null || _mediaPlayer.Source == null ? Visibility.Visible : Visibility.Collapsed;
                Element0.IsHitTestVisible = _mediaPlayer == null || _mediaPlayer.Source == null;
                Element1.IsHitTestVisible = _mediaPlayer == null || _mediaPlayer.Source == null;
                Element2.IsHitTestVisible = _mediaPlayer == null || _mediaPlayer.Source == null;
            });

            if (_request != null && (_mediaPlayer == null || _mediaPlayer.Source == null))
            {
                _request.RequestRelease();
                _request = null;
            }
        }

        private async void OnPlaybackStateChanged(MediaPlaybackSession sender, object args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                switch (sender.PlaybackState)
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
                Transport.Volume = sender.Volume;
            });
        }

        private void OnMediaOpened(MediaPlayer sender, object args)
        {
            sender.Volume = SettingsService.Current.VolumeLevel;
        }

        private static Dictionary<int, WeakReference<GalleryView>> _windowContext = new Dictionary<int, WeakReference<GalleryView>>();
        public static GalleryView GetForCurrentView()
        {
            return new GalleryView();

            var id = ApplicationView.GetApplicationViewIdForWindow(Window.Current.CoreWindow);
            if (_windowContext.TryGetValue(id, out WeakReference<GalleryView> reference) && reference.TryGetTarget(out GalleryView value))
            {
                return value;
            }

            var context = new GalleryView();
            _windowContext[id] = new WeakReference<GalleryView>(context);

            return context;
        }

        public IAsyncOperation<ContentDialogResult> ShowAsync(GalleryViewModelBase parameter, Func<FrameworkElement> closing = null)
        {
            return AsyncInfo.Run(async (token) =>
            {
                _closing = closing;

                if (SettingsService.Current.AreAnimationsEnabled)
                {
                    ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("FullScreenPicture", _closing());
                }

                if (_compactLifetime != null)
                {
                    var compact = _compactLifetime;
                    await compact.CoreDispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        compact.StopViewInUse();
                        compact.WindowWrapper.Close();
                    });

                    _compactLifetime = null;
                    Dispose();
                }

                parameter.Delegate = this;
                parameter.Items.CollectionChanged -= OnCollectionChanged;
                parameter.Items.CollectionChanged += OnCollectionChanged;

                Load(parameter);

                PrepareNext(0, true);

                RoutedEventHandler handler = null;
                handler = new RoutedEventHandler(async (s, args) =>
                {
                    _wasFullScreen = ApplicationView.GetForCurrentView().IsFullScreenMode;

                    Transport.Focus(FocusState.Programmatic);

                    Loaded -= handler;
                    await ViewModel?.OnNavigatedToAsync(parameter, NavigationMode.New, null);
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

            //var container = GetContainer(0);
            //var root = container.Presenter;
            if (ViewModel != null && ViewModel.SelectedItem == ViewModel.FirstItem && _closing != null)
            {
                ScrollingHost.Opacity = 0;
                Preview.Opacity = 1;

                var root = Preview.Presenter;

                if (SettingsService.Current.AreAnimationsEnabled)
                {
                    var animation = ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("FullScreenPicture", root);
                    if (animation != null)
                    {
                        if (ApiInformation.IsPropertyPresent("Windows.UI.Xaml.Media.Animation.ConnectedAnimation", "Configuration"))
                        {
                            animation.Configuration = new BasicConnectedAnimationConfiguration();
                        }

                        var element = _closing();
                        if (element.ActualWidth > 0 && animation.TryStart(element))
                        {
                            animation.Completed += (s, args) =>
                            {
                                Hide();
                            };
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
                var batch = _layout.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

                _layout.StartAnimation("Offset.Y", CreateScalarAnimation(_layout.Offset.Y, (float)ActualHeight));

                batch.End();
                batch.Completed += (s, args) =>
                {
                    ScrollingHost.Opacity = 0;
                    Preview.Opacity = 1;

                    Hide();
                };
            }

            _layer.StartAnimation("Opacity", CreateScalarAnimation(1, 0));

            if (Transport.IsVisible)
            {
                Transport.Hide();
            }

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

            var item = image.Item as GalleryContent;
            if (item == null)
            {
                return;
            }

            ScrollingHost.Opacity = 0;
            Preview.Opacity = 1;

            var container = GetContainer(0);

            if (SettingsService.Current.AreAnimationsEnabled)
            {
                var animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("FullScreenPicture");
                if (animation != null)
                {
                    if (ApiInformation.IsPropertyPresent("Windows.UI.Xaml.Media.Animation.ConnectedAnimation", "Configuration"))
                    {
                        animation.Configuration = new BasicConnectedAnimationConfiguration();
                    }

                    _layer.StartAnimation("Opacity", CreateScalarAnimation(0, 1));

                    if (animation.TryStart(image.Presenter))
                    {
                        animation.Completed += (s, args) =>
                        {
                            Transport.Show();
                            ScrollingHost.Opacity = 1;
                            Preview.Opacity = 0;

                            if (item.IsVideo && container != null)
                            {
                                Play(container.Presenter, item, item.GetFile());
                            }
                        };

                        return;
                    }
                }
            }

            _layer.Opacity = 1;

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
            scalar.Duration = ConnectedAnimationService.GetForCurrentView().DefaultDuration;

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
            var date = Convert.DateTime(value);
            return string.Format(Strings.Resources.FormatDateAtTime, Convert.ShortDate.Format(date), Convert.ShortTime.Format(date));
        }

        private string ConvertOf(int index, int count)
        {
            return string.Format(Strings.Resources.Of, index, count);
        }

        private Visibility ConvertCompactVisibility(GalleryContent item)
        {
            if (item != null && item.IsVideo && !item.IsLoop)
            {
                if (item is GalleryMessage message && message.IsHot)
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

        private async void Play(Grid parent, GalleryContent item, File file)
        {
            try
            {
                if (!file.Local.IsDownloadingCompleted && !SettingsService.Current.IsStreamingEnabled)
                {
                    return;
                }

                if (_surface != null && _mediaPlayerElement != null)
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

                var dpi = DisplayInformation.GetForCurrentView().LogicalDpi / 96.0f;
                _mediaPlayer.SetSurfaceSize(new Size(parent.ActualWidth * dpi, parent.ActualHeight * dpi));

                _surface = parent;
                _surface.Children.Add(_mediaPlayerElement);

                if (ApiInformation.IsPropertyPresent("Windows.UI.Xaml.Controls.MediaTransportControls", "ShowAndHideAutomatically"))
                {
                    Transport.ShowAndHideAutomatically = true;
                }

                Transport.DownloadMaximum = file.Size;
                Transport.DownloadValue = file.Local.DownloadOffset + file.Local.DownloadedPrefixSize;

                var streamable = SettingsService.Current.IsStreamingEnabled && item.IsStreamable && !file.Local.IsDownloadingCompleted;
                if (streamable)
                {
                    _streamingInterop = new FFmpegInteropMSS(new FFmpegInteropConfig());
                    var interop = await _streamingInterop.CreateFromFileAsync(ViewModel.ProtoService.Client, file);

                    _mediaPlayer.Source = interop.CreateMediaPlaybackItem();

                    Transport.DownloadMaximum = file.Size;
                    Transport.DownloadValue = file.Local.DownloadOffset + file.Local.DownloadedPrefixSize;
                }
                else
                {
                    _mediaPlayer.Source = MediaSource.CreateFromUri(new Uri("file:///" + file.Local.Path));
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

            if (_streamingInterop != null)
            {
                var interop = _streamingInterop;
                _streamingInterop = null;

                Task.Run(() => interop?.Dispose());
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

                if (_compactLifetime == null)
                {
                    _mediaPlayer.Dispose();
                    _mediaPlayer = null;
                }

                OnSourceChanged();
            }

            if (_request != null)
            {
                _request.RequestRelease();
                _request = null;
            }

            if (ApiInformation.IsPropertyPresent("Windows.UI.Xaml.Controls.MediaTransportControls", "ShowAndHideAutomatically"))
            {
                Transport.ShowAndHideAutomatically = false;
            }
        }

        private void ImageView_Click(object sender, RoutedEventArgs e)
        {
            if (Transport.IsVisible)
            {
                Transport.Hide();
            }
            else
            {
                Transport.Show();
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            WindowContext.GetForCurrentView().AcceleratorKeyActivated += OnAcceleratorKeyActivated;
        }

        private void Load(object parameter)
        {
            DataContext = parameter;
            Bindings.Update();

            if (ViewModel != null)
            {
                ViewModel.Aggregator.Subscribe(this);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Unload();
            WindowContext.GetForCurrentView().AcceleratorKeyActivated -= OnAcceleratorKeyActivated;
        }

        private void Unload()
        {
            if (ViewModel != null)
            {
                ViewModel.Aggregator.Unsubscribe(this);
            }

            DataContext = null;
            Bindings.StopTracking();
        }

        private void OnAcceleratorKeyActivated(CoreDispatcher sender, AcceleratorKeyEventArgs args)
        {
            if (args.EventType != CoreAcceleratorKeyEventType.KeyDown && args.EventType != CoreAcceleratorKeyEventType.SystemKeyDown)
            {
                return;
            }

            if (args.VirtualKey == Windows.System.VirtualKey.Left || args.VirtualKey == Windows.System.VirtualKey.GamepadLeftShoulder)
            {
                ChangeView(0, false);
                args.Handled = true;
            }
            else if (args.VirtualKey == Windows.System.VirtualKey.Right || args.VirtualKey == Windows.System.VirtualKey.GamepadRightShoulder)
            {
                ChangeView(2, false);
                args.Handled = true;
            }
        }

        private void Transport_Switch(GalleryTransportControls sender, int args)
        {
            ChangeView(args, false);
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
                    // Page is most likey being closed, just reset the view
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
                    PrepareNext(-1);
                    Dispose();
                }
                else if (index == 2 && next)
                {
                    viewModel.SelectedItem = viewModel.Items[selected + 1];
                    PrepareNext(+1);
                    Dispose();
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

        private void ChangeView(int index, bool disableAnimation)
        {
            ScrollingHost.ChangeView(ActualWidth * index, null, null, disableAnimation);
        }

        private void PrepareNext(int direction, bool initialize = false)
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
                Transport.PreviousVisibility = index > 0 ? Visibility.Visible : Visibility.Collapsed;
                Transport.NextVisibility = index < viewModel.Items.Count - 1 ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                Transport.PreviousVisibility = Visibility.Collapsed;
                Transport.NextVisibility = Visibility.Collapsed;
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
            if (object.Equals(element.Item, content))
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

            var item = viewModel.SelectedItem as GalleryContent;
            if (item == null)
            {
                return;
            }

            var flyout = new MenuFlyout();

            flyout.CreateFlyoutItem(x => item.CanView, viewModel.ViewCommand, item, Strings.Resources.ShowInChat, new FontIcon { Glyph = Icons.Message });
            flyout.CreateFlyoutItem(x => item.CanCopy, viewModel.CopyCommand, item, Strings.Resources.Copy, new FontIcon { Glyph = Icons.Copy }, Windows.System.VirtualKey.C);
            flyout.CreateFlyoutItem(x => item.CanSave, viewModel.SaveCommand, item, Strings.Additional.SaveAs, new FontIcon { Glyph = Icons.SaveAs }, Windows.System.VirtualKey.S);
            flyout.CreateFlyoutItem(x => viewModel.CanOpenWith, viewModel.OpenWithCommand, item, Strings.Resources.OpenInExternalApp, new FontIcon { Glyph = Icons.OpenIn });
            flyout.CreateFlyoutItem(x => viewModel.CanDelete, viewModel.DeleteCommand, item, Strings.Resources.Delete, new FontIcon { Glyph = Icons.Delete });

            if (ApiInformation.IsEnumNamedValuePresent("Windows.UI.Xaml.Controls.Primitives.FlyoutPlacementMode", "BottomEdgeAlignedRight"))
            {
                flyout.Placement = FlyoutPlacementMode.BottomEdgeAlignedRight;
            }

            flyout.ShowAt(sender as FrameworkElement);
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

            flyout.CreateFlyoutItem(x => item.CanView, viewModel.ViewCommand, item, Strings.Resources.ShowInChat, new FontIcon { Glyph = Icons.Message });
            flyout.CreateFlyoutItem(x => item.CanCopy, viewModel.CopyCommand, item, Strings.Resources.Copy, new FontIcon { Glyph = Icons.Copy }, Windows.System.VirtualKey.C);
            flyout.CreateFlyoutItem(x => item.CanSave, viewModel.SaveCommand, item, Strings.Additional.SaveAs, new FontIcon { Glyph = Icons.SaveAs }, Windows.System.VirtualKey.S);
            flyout.CreateFlyoutItem(x => viewModel.CanOpenWith, viewModel.OpenWithCommand, item, Strings.Resources.OpenInExternalApp, new FontIcon { Glyph = Icons.OpenIn });
            flyout.CreateFlyoutItem(x => viewModel.CanDelete, viewModel.DeleteCommand, item, Strings.Resources.Delete, new FontIcon { Glyph = Icons.Delete });

            args.ShowAt(flyout, element);
        }

        #endregion

        #region Compact overlay

        private static ViewLifetimeControl _compactLifetime;
        private IViewService _viewService;

        private async void Compact_Click(object sender, RoutedEventArgs e)
        {
            var item = ViewModel.SelectedItem;
            if (item == null)
            {
                return;
            }

            _viewService = TLContainer.Current.Resolve<IViewService>();

            if (_mediaPlayer == null || _mediaPlayer.Source == null)
            {
                Play(item, item.GetFile());
            }

            _mediaPlayerElement.SetMediaPlayer(null);

            var width = 340d;
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

            if (width > 500 || height > 500)
            {
                var ratioX = 500d / width;
                var ratioY = 500d / height;
                var ratio = Math.Min(ratioX, ratioY);

                width *= ratio;
                height *= ratio;
            }

            _compactLifetime = await _viewService.OpenAsync(() =>
            {
                var element = new MediaPlayerElement();
                element.RequestedTheme = ElementTheme.Dark;
                element.SetMediaPlayer(_mediaPlayer);
                element.TransportControls = new MediaTransportControls
                {
                    IsCompact = true,
                    IsCompactOverlayButtonVisible = false,
                    IsFastForwardButtonVisible = false,
                    IsFastRewindButtonVisible = false,
                    IsFullWindowButtonVisible = false,
                    IsNextTrackButtonVisible = false,
                    IsPlaybackRateButtonVisible = false,
                    IsPreviousTrackButtonVisible = false,
                    IsRepeatButtonVisible = false,
                    IsSkipBackwardButtonVisible = false,
                    IsSkipForwardButtonVisible = false,
                    IsVolumeButtonVisible = false,
                    IsStopButtonVisible = false,
                    IsZoomButtonVisible = false,
                };
                element.AreTransportControlsEnabled = true;
                return element;

            }, "PIP", width, height);
            _compactLifetime.WindowWrapper.ApplicationView().Consolidated += (s, args) =>
            {
                if (_compactLifetime != null)
                {
                    _compactLifetime.StopViewInUse();
                    _compactLifetime.WindowWrapper.Window.Close();
                    _compactLifetime = null;
                }

                this.BeginOnUIThread(() =>
                {
                    Dispose();
                });
            };

            OnBackRequestedOverride(this, new HandledEventArgs());
        }

        #endregion

        #region Swipe to close

        private Visual _layout;

        private void Initialize()
        {
            _layout = ElementCompositionPreview.GetElementVisual(LayoutRoot);

#if !GALLERY_EXPERIMENTAL
            void Register(Grid presenter)
            {
                presenter.ManipulationMode =
                    //ManipulationModes.TranslateX |
                    ManipulationModes.TranslateY |
                    //ManipulationModes.TranslateRailsX |
                    ManipulationModes.TranslateRailsY |
                    ManipulationModes.TranslateInertia |
                    ManipulationModes.System;
                presenter.ManipulationDelta += LayoutRoot_ManipulationDelta;
                presenter.ManipulationCompleted += LayoutRoot_ManipulationCompleted;
            }

            Register(Element2.Presenter);
            Register(Element0.Presenter);
            Register(Element1.Presenter);
#else
            LayoutRoot.ManipulationMode =
                //ManipulationModes.TranslateX |
                ManipulationModes.TranslateY |
                //ManipulationModes.TranslateRailsX |
                ManipulationModes.TranslateRailsY |
                ManipulationModes.TranslateInertia |
                ManipulationModes.System;
            LayoutRoot.ManipulationDelta += LayoutRoot_ManipulationDelta;
            LayoutRoot.ManipulationCompleted += LayoutRoot_ManipulationCompleted;
#endif

            if (ApiInformation.IsPropertyPresent("Windows.UI.Xaml.Controls.MediaTransportControls", "ShowAndHideAutomatically"))
            {
                Transport.ShowAndHideAutomatically = false;
            }
        }

        private void LayoutRoot_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            if (e.IsInertial)
            {
                e.Complete();
                return;
            }

            var height = (float)ActualHeight;
            var offset = _layout.Offset;

            if (Math.Abs(e.Cumulative.Translation.Y) > Math.Abs(e.Cumulative.Translation.X))
            {
                offset.Y = Math.Max(-height, Math.Min(height, offset.Y + (float)e.Delta.Translation.Y));

                var opacity = Math.Abs((offset.Y - -height) / height);
                if (opacity > 1)
                {
                    opacity = 2 - opacity;
                }

                _layer.Opacity = opacity;
            }

            _layout.Offset = offset;
            e.Handled = true;
        }

        private void LayoutRoot_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            var height = (float)ActualHeight;
            var offset = _layout.Offset;

            if (Math.Abs(e.Cumulative.Translation.Y) > Math.Abs(e.Cumulative.Translation.X))
            {
                var current = 0;
                var delta = -(offset.Y - current) / height;

                var maximum = current - height;
                var minimum = current + height;

                var direction = 0;

                var animation = _layout.Compositor.CreateScalarKeyFrameAnimation();
                animation.InsertKeyFrame(0, offset.Y);

                if (delta < 0 && e.Velocities.Linear.Y > 1.5)
                {
                    // previous
                    direction--;
                    animation.InsertKeyFrame(1, minimum);
                }
                else if (delta > 0 && e.Velocities.Linear.Y < -1.5)
                {
                    // next
                    direction++;
                    animation.InsertKeyFrame(1, maximum);
                }
                else
                {
                    // back
                    animation.InsertKeyFrame(1, current);
                }

                if (direction != 0)
                {
                    _layer.StartAnimation("Opacity", CreateScalarAnimation(1, 0));

                    if (Transport.IsVisible)
                    {
                        Transport.Hide();
                    }

                    Unload();

                    Dispose();
                    Hide();
                }
                else
                {
                    _layer.StartAnimation("Opacity", CreateScalarAnimation(_layer.Opacity, 1));
                }

                _layout.StartAnimation("Offset.Y", animation);
            }

            e.Handled = true;
        }

        #endregion

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            OnBackRequestedOverride(this, new HandledEventArgs());
        }
    }
}
