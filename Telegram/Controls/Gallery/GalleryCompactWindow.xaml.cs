using LibVLCSharp.Platforms.Windows;
using LibVLCSharp.Shared;
using System;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Services.ViewService;
using Telegram.Streams;
using Telegram.ViewModels.Gallery;
using Windows.ApplicationModel.Core;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;

namespace Telegram.Controls.Gallery
{
    public sealed partial class GalleryCompactWindow : UserControl
    {
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly LifoActionWorker _playbackQueue;

        private GalleryViewModelBase _viewModel;
        private RemoteFileStream _fileStream;
        private long _initialTime;

        private LibVLC _library;
        private MediaPlayer _mediaPlayer;

        public static ViewLifetimeControl Current { get; private set; }

        public GalleryCompactWindow(ViewLifetimeControl control, GalleryViewModelBase viewModel, RemoteFileStream stream, long time)
        {
            InitializeComponent();

            Current = control;

            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            _playbackQueue = new LifoActionWorker();

            _viewModel = viewModel;
            _fileStream = stream;
            _initialTime = time;

            Controls.IsCompact = true;

            var view = ApplicationView.GetForCurrentView();
            view.TitleBar.ButtonForegroundColor = Colors.White;

            var visual = ElementCompositionPreview.GetElementVisual(ControlsRoot);
            visual.Opacity = 0;
        }

        private void OnInitialized(object sender, InitializedEventArgs e)
        {
            _library = new LibVLC(e.SwapChainOptions);

            _mediaPlayer = new MediaPlayer(_library);
            //_mediaPlayer.EndReached += OnEndReached;
            //_mediaPlayer.Stopped += OnStopped;
            //_mediaPlayer.VolumeChanged += OnVolumeChanged;
            //_mediaPlayer.SourceChanged += OnSourceChanged;
            //_mediaPlayer.MediaOpened += OnMediaOpened;
            //_mediaPlayer.PlaybackSession.PlaybackStateChanged += OnPlaybackStateChanged;

            Controls.Attach(_mediaPlayer);

            _playbackQueue.Enqueue(() =>
            {
                _mediaPlayer.Play(new LibVLCSharp.Shared.Media(_library, _fileStream));
                _mediaPlayer.Time = _initialTime;
            });
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Current = null;

            Task.Run(() =>
            {
                _mediaPlayer?.Stop();
                _mediaPlayer?.Dispose();
                _mediaPlayer = null;

                _library?.Dispose();
                _library = null;

                _fileStream?.Close();
                _fileStream = null;
            });
        }

        protected override void OnPointerEntered(PointerRoutedEventArgs e)
        {
            ShowHideTransport(true);
            base.OnPointerEntered(e);
        }

        protected override void OnPointerExited(PointerRoutedEventArgs e)
        {
            ShowHideTransport(false);
            base.OnPointerExited(e);
        }

        private bool _transportCollapsed = true;

        private void ShowHideTransport(bool show)
        {
            if (show != _transportCollapsed)
            {
                return;
            }

            _transportCollapsed = !show;

            var visual = ElementCompositionPreview.GetElementVisual(ControlsRoot);
            var opacity = visual.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, show ? 0 : 1);
            opacity.InsertKeyFrame(1, show ? 1 : 0);

            visual.StartAnimation("Opacity", opacity);
        }

        private async void Controls_CompactClick(object sender, RoutedEventArgs e)
        {
            var mainDispatcher = CoreApplication.MainView.Dispatcher;
            if (mainDispatcher == null || _mediaPlayer == null)
            {
                return;
            }

            var position = _mediaPlayer.Time;
            var prevId = ApplicationView.GetForCurrentView().Id;

            _playbackQueue.Enqueue(_mediaPlayer.Stop);

            await mainDispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                var nextId = ApplicationView.GetForCurrentView().Id;
                _ = ApplicationViewSwitcher.SwitchAsync(nextId, prevId, ApplicationViewSwitchingOptions.ConsolidateViews);
                _ = GalleryWindow.ShowAsync(_viewModel, null, position);
            });
        }

        public void Play(GalleryViewModelBase viewModel, RemoteFileStream stream, long time)
        {
            _viewModel = viewModel;

            if (_mediaPlayer != null)
            {
                _playbackQueue.Enqueue(() =>
                {
                    _mediaPlayer.Play(new LibVLCSharp.Shared.Media(_library, stream));
                    _mediaPlayer.Time = time;
                });
            }
        }

        public void Pause()
        {
            _mediaPlayer?.SetPause(true);
        }
    }
}
