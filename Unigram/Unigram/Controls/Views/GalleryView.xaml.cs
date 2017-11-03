using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
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

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls.Views
{
    public sealed partial class GalleryView : ContentDialogBase
    {
        public GalleryViewModelBase ViewModel => DataContext as GalleryViewModelBase;

        public BindConvert Convert => BindConvert.Current;

        private Func<FrameworkElement> _closing;

        private DisplayRequest _request;
        private MediaPlayerElement _mediaPlayerElement;
        private MediaPlayer _mediaPlayer;
        private Grid _surface;

        private Visual _layerVisual;
        //private Visual _topBarVisual;
        //private Visual _botBarVisual;

        private GalleryView()
        {
            InitializeComponent();

            Layer.Visibility = Visibility.Collapsed;
            //TopBar.Visibility = Visibility.Collapsed;
            //BotBar.Visibility = Visibility.Collapsed;

            _mediaPlayerElement = new MediaPlayerElement { Style = Resources["yolo"] as Style };
            _mediaPlayerElement.AreTransportControlsEnabled = true;
            _mediaPlayerElement.TransportControls = Transport;
            _mediaPlayerElement.Tapped += MediaPlayer_Tapped;

            _layerVisual = ElementCompositionPreview.GetElementVisual(Layer);
            //_topBarVisual = ElementCompositionPreview.GetElementVisual(TopBar);
            //_botBarVisual = ElementCompositionPreview.GetElementVisual(BotBar);

            //_layerVisual.Opacity = 0;
            //_topBarVisual.Offset = new Vector3(0, -48, 0);
            //_botBarVisual.Offset = new Vector3(0, 48, 0);

            if (ApiInformation.IsMethodPresent("Windows.UI.Xaml.Hosting.ElementCompositionPreview", "SetImplicitShowAnimation"))
            {
                var easing = ConnectedAnimationService.GetForCurrentView().DefaultEasingFunction;
                var duration = ConnectedAnimationService.GetForCurrentView().DefaultDuration;

                var topShowAnimation = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
                topShowAnimation.InsertKeyFrame(0.0f, new Vector3(0, -48, 0), easing);
                topShowAnimation.InsertKeyFrame(1.0f, new Vector3(), easing);
                topShowAnimation.Target = nameof(Visual.Offset);
                topShowAnimation.Duration = duration;

                var botHideAnimation = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
                botHideAnimation.InsertKeyFrame(0.0f, new Vector3(), easing);
                botHideAnimation.InsertKeyFrame(1.0f, new Vector3(0, 48, 0), easing);
                botHideAnimation.Target = nameof(Visual.Offset);
                botHideAnimation.Duration = duration;

                var topHideAnimation = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
                topHideAnimation.InsertKeyFrame(0.0f, new Vector3(), easing);
                topHideAnimation.InsertKeyFrame(1.0f, new Vector3(0, -48, 0), easing);
                topHideAnimation.Target = nameof(Visual.Offset);
                topHideAnimation.Duration = duration;

                var botShowAnimation = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
                botShowAnimation.InsertKeyFrame(0.0f, new Vector3(0, 48, 0), easing);
                botShowAnimation.InsertKeyFrame(1.0f, new Vector3(), easing);
                botShowAnimation.Target = nameof(Visual.Offset);
                botShowAnimation.Duration = duration;

                var layerShowAnimation = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
                layerShowAnimation.InsertKeyFrame(0.0f, 0.0f, easing);
                layerShowAnimation.InsertKeyFrame(1.0f, 1.0f, easing);
                layerShowAnimation.Target = nameof(Visual.Opacity);
                layerShowAnimation.Duration = duration;

                var layerHideAnimation = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
                layerHideAnimation.InsertKeyFrame(0.0f, 1.0f, easing);
                layerHideAnimation.InsertKeyFrame(1.0f, 0.0f, easing);
                layerHideAnimation.Target = nameof(Visual.Opacity);
                layerHideAnimation.Duration = duration;

                //ElementCompositionPreview.SetImplicitShowAnimation(TopBar, topShowAnimation);
                //ElementCompositionPreview.SetImplicitHideAnimation(TopBar, topHideAnimation);
                //ElementCompositionPreview.SetImplicitShowAnimation(BotBar, botShowAnimation);
                //ElementCompositionPreview.SetImplicitHideAnimation(BotBar, botHideAnimation);
                ElementCompositionPreview.SetImplicitShowAnimation(Layer, layerShowAnimation);
                ElementCompositionPreview.SetImplicitHideAnimation(Layer, layerHideAnimation);
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
            });
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

        private static GalleryView _current;
        public static GalleryView Current
        {
            get
            {
                //return new GalleryView();

                if (_current == null)
                    _current = new GalleryView();

                return _current;
            }
        }

        public IAsyncOperation<ContentDialogBaseResult> ShowAsync(GalleryViewModelBase parameter, Func<FrameworkElement> closing)
        {
            _closing = closing;

            //EventHandler handler = null;
            //handler = new EventHandler((s, args) =>
            //{
            //    DataContext = null;
            //    Bindings.StopTracking();

            //    Closing -= handler;
            //    closing?.Invoke(this, args);
            //});

            //Closing += handler;
            return ShowAsync(parameter);
        }

        public IAsyncOperation<ContentDialogBaseResult> ShowAsync(GalleryViewModelBase parameter)
        {
            ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("FullScreenPicture", _closing());

            DataContext = parameter;
            Bindings.Update();

            RoutedEventHandler handler = null;
            handler = new RoutedEventHandler(async (s, args) =>
            {
                Loaded -= handler;
                await ViewModel.OnNavigatedToAsync(parameter, NavigationMode.New, null);
            });

            Loaded += handler;
            return ShowAsync();
        }

        protected override void MaskTitleAndStatusBar()
        {
            var titlebar = ApplicationView.GetForCurrentView().TitleBar;
            titlebar.BackgroundColor = Colors.Black;
            titlebar.ForegroundColor = Colors.White;
            titlebar.ButtonBackgroundColor = Colors.Black;
            titlebar.ButtonForegroundColor = Colors.White;

            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                var statusBar = StatusBar.GetForCurrentView();
                statusBar.BackgroundColor = Colors.Black;
                statusBar.ForegroundColor = Colors.White;
            }
        }

        protected override void OnBackRequestedOverride(object sender, HandledEventArgs e)
        {
            if (ViewModel.SelectedItem == ViewModel.FirstItem)
            {
                Surface.Visibility = Visibility.Visible;

                var animation = ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("FullScreenPicture", Surface);
                if (animation != null && _closing != null)
                {
                    var element = _closing();
                    if (element.ActualWidth > 0)
                    {
                        animation.TryStart(element);
                    }
                }
            }
            
            Layer.Visibility = Visibility.Collapsed;
            //TopBar.Visibility = Visibility.Collapsed;
            //BotBar.Visibility = Visibility.Collapsed;

            if (Transport.IsVisible)
            {
                Transport.Hide();
            }

            DataContext = null;
            Bindings.StopTracking();

            Dispose();
            Hide();

            e.Handled = true;
        }

        private void ImageView_ImageOpened(object sender, RoutedEventArgs e)
        {
            var animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("FullScreenPicture");
            if (animation != null)
            {
                Layer.Visibility = Visibility.Visible;
                //TopBar.Visibility = Visibility.Visible;
                //BotBar.Visibility = Visibility.Visible;

                Transport.Show();

                //Flip.Opacity = 1;
                if (animation.TryStart(Surface))
                {
                    animation.Completed += (s, args) =>
                    {
                        //Flip.Opacity = 1;
                        //Surface.Visibility = Visibility.Collapsed;
                    };
                }
                else
                {
                    //Flip.Opacity = 1;
                    //Surface.Visibility = Visibility.Collapsed;
                }

                var border = sender as FrameworkElement;
                var item = border.DataContext as GalleryItem;
                if (item == null)
                {
                    return;
                }

                if (item.IsVideo)
                {
                    Play(item);
                }
            }
        }

        private string ConvertFrom(ITLDialogWith with)
        {
            return with is TLUser user && user.IsSelf ? user.FullName : with?.DisplayName;
        }

        private string ConvertDate(int value)
        {
            var date = Convert.DateTime(value);
            return string.Format("{0} at {1}", date.Date == DateTime.Now.Date ? "Today" : Convert.ShortDate.Format(date), Convert.ShortTime.Format(date));
        }

        private void Download_Click(object sender, TransferCompletedEventArgs e)
        {
            var border = sender as FrameworkElement;
            var item = border.DataContext as GalleryItem;
            if (item == null)
            {
                return;
            }

            if (item.IsVideo)
            {
                Play(item);
            }
        }

        private void Play(GalleryItem item)
        {
            try
            {
                if (_surface != null)
                {
                    _surface.Children.Remove(_mediaPlayerElement);
                }

                var parent = Surface;

                //var container = Flip.ContainerFromItem(item) as ContentControl;
                //if (container != null && container.ContentTemplateRoot is Grid parent)
                {
                    //_surface = parent.FindName("Surface") as ImageView;

                    if (_mediaPlayer == null)
                    {
                        _mediaPlayer = new MediaPlayer();
                        _mediaPlayer.SourceChanged += OnSourceChanged;
                        _mediaPlayer.PlaybackSession.PlaybackStateChanged += OnPlaybackStateChanged;
                    }

                    _mediaPlayerElement.Tag = item.Source;
                    _mediaPlayerElement.SetMediaPlayer(_mediaPlayer);

                    _mediaPlayer.Source = MediaSource.CreateFromUri(item.GetVideoSource());
                    _mediaPlayer.IsLoopingEnabled = item.IsLoop;
                    _mediaPlayer.Play();

                    _surface = parent;
                    _surface.Children.Add(_mediaPlayerElement);
                }
            }
            catch { }
        }

        private void Flip_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Dispose();
        }

        private void Dispose()
        {
            if (_surface != null)
            {
                _surface.Children.Remove(_mediaPlayerElement);
                _surface = null;
            }

            if (_mediaPlayer != null)
            {
                _mediaPlayer.SourceChanged -= OnSourceChanged;
                _mediaPlayer.PlaybackSession.PlaybackStateChanged -= OnPlaybackStateChanged;

                _mediaPlayerElement.SetMediaPlayer(null);

                _mediaPlayer.Dispose();
                _mediaPlayer = null;
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

        private void MediaPlayer_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (Transport.IsVisible)
            {
                Transport.Hide();
            }
            else
            {
                Transport.Show();
            }

            e.Handled = true;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            DataContext = null;
            Bindings.StopTracking();
        }
    }
}
