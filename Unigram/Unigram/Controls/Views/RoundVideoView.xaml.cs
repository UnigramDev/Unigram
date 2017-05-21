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
using Windows.Media.Effects;
using Windows.Media.MediaProperties;
using Windows.Media.Playback;
using Windows.Storage;
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


            //var visual = ElementCompositionPreview.GetElementVisual(this);
            //visual.Clip = visual.Compositor.CreateInsetClip(0, 0, 0, 48);

            //var capture = ElementCompositionPreview.GetElementVisual(Capture);
            //_compositor = capture.Compositor;
            //_capture = _compositor.CreateSpriteVisual();
            //_capture.Size = new Vector2(180, 180);

            //ImageLoader.Initialize(_compositor);
            //ElementCompositionPreview.SetElementChildVisual(Capture, _capture);

            Loaded += OnLoaded;
            Unloaded += RoundVideoView_Unloaded;
        }

        private async void RoundVideoView_Unloaded(object sender, RoutedEventArgs e)
        {
            await _lowLag.StopWithResultAsync();
            await _media.StopPreviewAsync();
            _media.Dispose();
        }

        private MediaPlayer _player;
        private MediaPlayerSurface _surface;
        private MediaCapture _media;
        private MediaCapturePreviewSource _preview;
        private Compositor _compositor;
        private SpriteVisual _capture;

        private LowLagMediaRecording _lowLag;

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

            //_preview = MediaCapturePreviewSource.CreateFromVideoEncodingProperties(profile.Video);
            _media = new MediaCapture();
            await _media.InitializeAsync(settings);
            Capture.Source = _media;
            await _media.StartPreviewAsync();

            //_media.SetRecordRotation(VideoRotation.Clockwise90Degrees);

            var effect = new VideoTransformEffectDefinition();
            effect.CropRectangle = new Rect(40, 0, 240, 240);

            await _media.AddVideoEffectAsync(effect, MediaStreamType.VideoRecord);

            var record = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Vga);
            record.Video.Width = 240;
            record.Video.Height = 240;
            //record.Video.Bitrate = 300000;
            //record.Audio.ChannelCount = 1;
            //record.Audio.Bitrate = 62000;

            var file = await ApplicationData.Current.LocalFolder.CreateFileAsync("RoundVideo.mp4", CreationCollisionOption.ReplaceExisting);
            _lowLag = await _media.PrepareLowLagRecordToStorageFileAsync(record, file);

            await _lowLag.StartAsync();

            //await _media.StartPreviewToCustomSinkAsync(profile, _preview.MediaSink);

            //_player = new MediaPlayer();
            //_player.RealTimePlayback = true;
            //_player.AutoPlay = true;
            //_player.Source = _preview.MediaSource as IMediaPlaybackSource;

            //_surface = _player.GetSurface(_compositor);

            //var brush = _compositor.CreateSurfaceBrush(_surface.CompositionSurface);
            //brush.Stretch = CompositionStretch.UniformToFill;

            //_capture.Brush = brush;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            OuterClip.Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height);
            InnerClip.Center = new Point(e.NewSize.Width / 2, e.NewSize.Height / 2);
        }
    }
}
