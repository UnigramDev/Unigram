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
using Telegram.Api.Services.Cache.EventArgs;
using Telegram.Api.Aggregator;
using Unigram.Common;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls.Views
{
    public sealed partial class GallerySecretView : ContentDialogBase, IHandle<MessageExpiredEventArgs>
    {
        public GallerySecretViewModel ViewModel => DataContext as GallerySecretViewModel;

        public BindConvert Convert => BindConvert.Current;

        private Func<FrameworkElement> _closing;

        private TimeSpan _lastPosition;
        private bool _playedOnce;
        private bool _destructed;

        private DisplayRequest _request;

        private Visual _layerVisual;
        //private Visual _topBarVisual;
        //private Visual _botBarVisual;

        private GallerySecretView()
        {
            InitializeComponent();

            Layer.Visibility = Visibility.Collapsed;
            TopBar.Visibility = Visibility.Collapsed;

            //_mediaPlayer = new MediaPlayerElement();
            _mediaPlayer.AutoPlay = true;
            _mediaPlayer.SetMediaPlayer(new MediaPlayer());
            _mediaPlayer.MediaPlayer.PlaybackSession.PlaybackStateChanged += OnPlaybackStateChanged;
            _mediaPlayer.MediaPlayer.PlaybackSession.PositionChanged += OnPositionChanged;
            _mediaPlayer.MediaPlayer.IsLoopingEnabled = true;

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

                var topHideAnimation = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
                topHideAnimation.InsertKeyFrame(0.0f, new Vector3(), easing);
                topHideAnimation.InsertKeyFrame(1.0f, new Vector3(0, -48, 0), easing);
                topHideAnimation.Target = nameof(Visual.Offset);
                topHideAnimation.Duration = duration;

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

                ElementCompositionPreview.SetImplicitShowAnimation(TopBar, topShowAnimation);
                ElementCompositionPreview.SetImplicitHideAnimation(TopBar, topHideAnimation);
                ElementCompositionPreview.SetImplicitShowAnimation(Layer, layerShowAnimation);
                ElementCompositionPreview.SetImplicitHideAnimation(Layer, layerHideAnimation);
            }

            TelegramEventAggregator.Instance.Subscribe(this);
        }

        private void OnPositionChanged(MediaPlaybackSession sender, object args)
        {
            if (!_playedOnce)
            {
                _playedOnce = sender.Position < _lastPosition;
                _lastPosition = sender.Position;
            }

            this.BeginOnUIThread(() =>
            {
                if (_destructed)
                {
                    OnBackRequestedOverride(this, new HandledEventArgs());
                }
            });
        }

        public void Handle(MessageExpiredEventArgs args)
        {
            _destructed = true;

            if (!_playedOnce)
            {
                return;
            }

            this.BeginOnUIThread(() =>
            {
                if (ViewModel?.SelectedItem is GalleryMessageItem messageItem && messageItem.Message == args.Message)
                {
                    OnBackRequestedOverride(this, new HandledEventArgs());
                }
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

        private static GallerySecretView _current;
        public static GallerySecretView Current
        {
            get
            {
                //return new GalleryView();

                if (_current == null)
                    _current = new GallerySecretView();

                return _current;
            }
        }

        public IAsyncOperation<ContentDialogBaseResult> ShowAsync(GallerySecretViewModel parameter, Func<FrameworkElement> closing)
        {
            _destructed = false;
            _playedOnce = false;
            _lastPosition = TimeSpan.Zero;

            _closing = closing;

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
            Dispose();

            Layer.Visibility = Visibility.Collapsed;
            TopBar.Visibility = Visibility.Collapsed;

            var animation = ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("FullScreenPicture", Surface);
            if (animation != null && _closing != null)
            {
                animation.TryStart(_destructed ? Window.Current.Content : _closing());
            }

            DataContext = null;
            Bindings.StopTracking();

            Hide();

            e.Handled = true;
        }

        private void ImageView_ImageOpened(object sender, RoutedEventArgs e)
        {
            var animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("FullScreenPicture");
            if (animation != null)
            {
                Layer.Visibility = Visibility.Visible;
                TopBar.Visibility = Visibility.Visible;

                //Flip.Opacity = 1;
                if (animation.TryStart(Surface))
                {
                    animation.Completed += (s, args) =>
                    {
                        //Flip.Opacity = 1;
                        //Surface.Visibility = Visibility.Collapsed;

                        Download_Click(Surface, null);
                    };
                }
                else
                {
                    //Flip.Opacity = 1;
                    //Surface.Visibility = Visibility.Collapsed;

                    Download_Click(Surface, null);
                }
            }
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
            else
            {
                _playedOnce = true;
            }
        }

        private void Play(GalleryItem item)
        {
            try
            {
                _mediaPlayer.Source = MediaSource.CreateFromUri(item.GetVideoSource());
                _mediaPlayer.MediaPlayer.Play();
            }
            catch { }
        }

        private void Flip_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Dispose();
        }

        private void Dispose()
        {
            if (_mediaPlayer?.Source != null)
            {
                _mediaPlayer.MediaPlayer.Pause();
                _mediaPlayer.Source = null;
            }
        }

        private void ImageView_Tapped(object sender, TappedRoutedEventArgs e)
        {
            TopBar.Visibility = TopBar.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
#if !DEBUG
            ApplicationView.GetForCurrentView().IsScreenCaptureEnabled = false;
#endif
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
#if !DEBUG
            ApplicationView.GetForCurrentView().IsScreenCaptureEnabled = true;
#endif

            DataContext = null;
            Bindings.StopTracking();
        }

        private void Download_Click(object sender, object e)
        {

        }

        private void Surface_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _mediaPlayer.MediaPlayer.SetSurfaceSize(e.NewSize);
        }






        private string ConvertTitle(TLMessage message)
        {
            return message.IsPhoto() ? Strings.Android.DisappearingPhoto : Strings.Android.DisappearingVideo;
        }

        private string ConvertUnread(TLMessage message)
        {
            var user = message.Parent as TLUser;
            if (user == null)
            {
                return string.Empty;
            }

            return message.IsPhoto() ? $"{user.FirstName} hasn't opened this photo yet" : $"{user.FirstName} hasn't opened this video yet";
        }
    }
}
