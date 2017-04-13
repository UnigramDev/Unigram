using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Native;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Media.Playback;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Content Dialog item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Controls.Views
{
    public sealed partial class RoundVideoView : ContentDialogBase
    {
        public RoundVideoView()
        {
            this.InitializeComponent();


            var visual = ElementCompositionPreview.GetElementVisual(this);
            visual.Clip = visual.Compositor.CreateInsetClip(0, 0, 0, 48);

            var capture = ElementCompositionPreview.GetElementVisual(Capture);
            _compositor = capture.Compositor;
            _capture = _compositor.CreateSpriteVisual();
            _capture.Size = new Vector2(180, 180);

            ImageLoader.Initialize(_compositor);
            ElementCompositionPreview.SetElementChildVisual(Capture, _capture);

            Loaded += OnLoaded;
        }

        private MediaPlayer _player;
        private MediaPlayerSurface _surface;
        private MediaCapture _media;
        private MediaCapturePreviewSource _preview;
        private Compositor _compositor;
        private SpriteVisual _capture;

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Vga);
            profile.Audio = null;
            profile.Container = null;

            var settings = new MediaCaptureInitializationSettings();
            settings.MediaCategory = MediaCategory.Media;
            settings.MemoryPreference = MediaCaptureMemoryPreference.Auto;
            settings.SharingMode = MediaCaptureSharingMode.SharedReadOnly;
            settings.StreamingCaptureMode = StreamingCaptureMode.AudioAndVideo;

            _preview = MediaCapturePreviewSource.CreateFromVideoEncodingProperties(profile.Video);
            _media = new MediaCapture();
            await _media.InitializeAsync(settings);
            await _media.StartPreviewToCustomSinkAsync(profile, _preview.MediaSink);

            _player = new MediaPlayer();
            _player.RealTimePlayback = true;
            _player.AutoPlay = true;
            _player.Source = _preview.MediaSource as IMediaPlaybackSource;

            //PlayerElement.SetMediaPlayer(_player);

            _surface = _player.GetSurface(_compositor);

            var brush = _compositor.CreateSurfaceBrush(_surface.CompositionSurface);
            brush.Stretch = CompositionStretch.UniformToFill;

            _capture.Brush = brush;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }
    }
}
