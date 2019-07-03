using Microsoft.Graphics.Canvas.Effects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Unigram.ViewModels;
using Unigram.ViewModels.Delegates;
using Windows.Devices.Sensors;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Display;
using Windows.Storage;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

namespace Unigram.Views
{
    public sealed partial class WallpaperPage : Page, IHandle<UpdateFile>, IWallpaperDelegate
    {
        public WallpaperViewModel ViewModel => DataContext as WallpaperViewModel;

        private Visual _motionVisual;
        private SpriteVisual _blurVisual;
        private CompositionEffectBrush _blurBrush;
        private Compositor _compositor;

        private WallpaperParallaxEffect _parallaxEffect;

        public WallpaperPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<WallpaperViewModel, IWallpaperDelegate>(this);

            Message1.Mockup(Strings.Resources.BackgroundPreviewLine1, false, DateTime.Now.AddSeconds(-25));
            Message2.Mockup(Strings.Resources.BackgroundPreviewLine2, true, DateTime.Now);

            //Presenter.Update(ViewModel.SessionId, ViewModel.Settings, ViewModel.Aggregator);

            InitializeMotion();
            InitializeBlur();
        }

        private void InitializeBlur()
        {
            _motionVisual = ElementCompositionPreview.GetElementVisual(Presenter);
            _compositor = _motionVisual.Compositor;

            ElementCompositionPreview.GetElementVisual(this).Clip = _compositor.CreateInsetClip();

            var graphicsEffect = new GaussianBlurEffect
            {
                Name = "Blur",
                BlurAmount = 0,
                BorderMode = EffectBorderMode.Hard,
                Source = new CompositionEffectSourceParameter("backdrop")
            };

            var effectFactory = _compositor.CreateEffectFactory(graphicsEffect, new[] { "Blur.BlurAmount" });
            var effectBrush = effectFactory.CreateBrush();
            var backdrop = _compositor.CreateBackdropBrush();
            effectBrush.SetSourceParameter("backdrop", backdrop);

            _blurBrush = effectBrush;
            _blurVisual = _compositor.CreateSpriteVisual();
            _blurVisual.Brush = _blurBrush;

            // Why does this crashes due to an access violation exception on certain devices?
            ElementCompositionPreview.SetElementChildVisual(BlurPanel, _blurVisual);
        }

        private async void InitializeMotion()
        {
            _parallaxEffect = new WallpaperParallaxEffect();

            Motion.Visibility = (await _parallaxEffect.IsSupportedAsync()) ? Visibility.Visible : Visibility.Collapsed;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.Aggregator.Subscribe(this);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.Aggregator.Unsubscribe(this);
        }

        private void BlurPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _motionVisual.CenterPoint = new Vector3((float)ActualWidth / 2, (float)ActualHeight / 2, 0);
            _blurVisual.Size = e.NewSize.ToVector2();
        }

        private void Blur_Click(object sender, RoutedEventArgs e)
        {
            var animation = _compositor.CreateScalarKeyFrameAnimation();
            animation.Duration = TimeSpan.FromMilliseconds(300);

            if (sender is CheckBox check && check.IsChecked == true)
            {
                animation.InsertKeyFrame(1, 12);
            }
            else
            {
                animation.InsertKeyFrame(1, 0);
            }

            _blurBrush.Properties.StartAnimation("Blur.BlurAmount", animation);
        }

        private async void Motion_Click(object sender, RoutedEventArgs e)
        {
            var animation = _compositor.CreateVector3KeyFrameAnimation();
            animation.Duration = TimeSpan.FromMilliseconds(300);

            if (sender is CheckBox check && check.IsChecked == true)
            {
                animation.InsertKeyFrame(1, new Vector3(_parallaxEffect.getScale(ActualWidth, ActualHeight)));
                await _parallaxEffect.RegisterAsync(OnParallaxChanged);
            }
            else
            {
                animation.InsertKeyFrame(1, new Vector3(1));
                await _parallaxEffect.UnregisterAsync(OnParallaxChanged);
            }

            _motionVisual.StartAnimation("Scale", animation);
        }

        private void OnParallaxChanged(object sender, (int x, int y) e)
        {
            _motionVisual.Offset = new Vector3(e.x, e.y, 0);
        }

        //private void Color_Click(object sender, RoutedEventArgs e)
        //{
        //    var row = Grid.GetRow(ColorPanel);
        //    Grid.SetRow(ColorPanel, row == 3 ? 2 : 3);
        //}

        private async void Image_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            var wallpaper = args.NewValue as Background;
            if (wallpaper == null)
            {
                return;
            }

            if (wallpaper.Id == Constants.WallpaperLocalId)
            {
                var file = await ApplicationData.Current.LocalFolder.GetFileAsync($"{ViewModel.SessionId}\\{Constants.WallpaperLocalFileName}");
                using (var stream = await file.OpenReadAsync())
                {
                    var bitmap = new BitmapImage();
                    await bitmap.SetSourceAsync(stream);

                    var content = sender as Rectangle;
                    content.Fill = new ImageBrush { ImageSource = bitmap, AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center, Stretch = Stretch.UniformToFill };
                }
            }
            else
            {
                var big = wallpaper.Document;
                if (big == null)
                {
                    return;
                }

                var content = sender as Rectangle;
                content.Fill = new ImageBrush { ImageSource = PlaceholderHelper.GetBitmap(ViewModel.ProtoService, big.DocumentValue, 0, 0), AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center, Stretch = Stretch.UniformToFill };
            }
        }

        private void Rectangle_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            var wallpaper = args.NewValue as Background;
            if (wallpaper == null)
            {
                return;
            }

            var solid = wallpaper.Type as BackgroundTypeSolid;
            if (solid == null)
            {
                return;
            }

            var content = sender as Rectangle;
            content.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, (byte)((solid.Color >> 16) & 0xFF), (byte)((solid.Color >> 8) & 0xFF), (byte)(solid.Color & 0xFF)));
        }

        #region Delegates

        public async void UpdateWallpaper(Background wallpaper)
        {
            if (wallpaper == null)
            {
                return;
            }

            if (wallpaper.Id == Constants.WallpaperLocalId || wallpaper.Document != null)
            {
                Blur.Visibility = Visibility.Visible;
                Motion.Visibility = (await _parallaxEffect.IsSupportedAsync()) ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                Blur.Visibility = Visibility.Collapsed;
                ViewModel.IsBlurEnabled = false;

                Motion.Visibility = Visibility.Collapsed;
                ViewModel.IsMotionEnabled = false;
            }
        }

        #endregion

        public void Handle(UpdateFile update)
        {
            this.BeginOnUIThread(() =>
            {
                if (Presenter.Content is Background wallpaper && wallpaper.UpdateFile(update.File))
                {
                    var big = wallpaper.Document;
                    if (big == null)
                    {
                        return;
                    }

                    var content = Presenter.ContentTemplateRoot as Rectangle;
                    content.Fill = new ImageBrush { ImageSource = PlaceholderHelper.GetBitmap(ViewModel.ProtoService, big.DocumentValue, 0, 0), AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center };
                }
            });
        }
    }

    public class WallpaperParallaxEffect
    {
        /** Earth's gravity in SI units (m/s^2) */
        public const float GRAVITY_EARTH = 9.80665f;

        private Accelerometer _accelerometer;
        private DisposableMutex _registerMutex;
        private bool _loaded;

        private float[] _rollBuffer = new float[3];
        private float[] _pitchBuffer = new float[3];
        private int bufferOffset;

        public WallpaperParallaxEffect()
        {
            //_accelerometer = Accelerometer.GetDefault();
            _registerMutex = new DisposableMutex();
        }

        private void Initialize()
        {
            if (_accelerometer != null)
            {
                // Establish the report interval
                uint minReportInterval = _accelerometer.MinimumReportInterval;
                uint reportInterval = minReportInterval > 16 ? minReportInterval : 16;
                _accelerometer.ReportInterval = reportInterval;
            }
        }

        public async Task<bool> IsSupportedAsync()
        {
            using (await _registerMutex.WaitAsync())
            {
                if (_accelerometer == null && !_loaded)
                {
                    _loaded = true;
                    _accelerometer = await Task.Run(() => Accelerometer.GetDefault());
                }

                return _accelerometer != null;
            }
        }

        private void ReadingChanged(Accelerometer sender, AccelerometerReadingChangedEventArgs args)
        {
            //int rotation = wm.getDefaultDisplay().getRotation();
            var rotation = DisplayOrientations.Portrait; //DisplayInformation.GetForCurrentView().CurrentOrientation;
            double x = args.Reading.AccelerationX / GRAVITY_EARTH;
            double y = args.Reading.AccelerationY / GRAVITY_EARTH;
            double z = args.Reading.AccelerationZ / GRAVITY_EARTH;

            float pitch = (float)(Math.Atan2(x, Math.Sqrt(y * y + z * z)) / Math.PI * 2.0);
            float roll = (float)(Math.Atan2(y, Math.Sqrt(x * x + z * z)) / Math.PI * 2.0);

            switch (rotation)
            {
                case DisplayOrientations.Portrait:
                    break;
                case DisplayOrientations.Landscape:
                    {
                        float tmp = pitch;
                        pitch = roll;
                        roll = tmp;
                        break;
                    }
                case DisplayOrientations.LandscapeFlipped:
                    roll = -roll;
                    pitch = -pitch;
                    break;
                case DisplayOrientations.PortraitFlipped:
                    {
                        float tmp = -pitch;
                        pitch = roll;
                        roll = tmp;
                        break;
                    }
            }

            _rollBuffer[bufferOffset] = roll;
            _pitchBuffer[bufferOffset] = pitch;
            bufferOffset = (bufferOffset + 1) % _rollBuffer.Length;
            roll = pitch = 0;
            for (int i = 0; i < _rollBuffer.Length; i++)
            {
                roll += _rollBuffer[i];
                pitch += _pitchBuffer[i];
            }
            roll /= _rollBuffer.Length;
            pitch /= _rollBuffer.Length;
            if (roll > 1f)
            {
                roll = 2f - roll;
            }
            else if (roll < -1f)
            {
                roll = -2f - roll;
            }
            int offsetX = (int)Math.Round(pitch * 16);
            int offsetY = (int)Math.Round(roll * 16);

            //if (callback != null)
            //    callback.onOffsetsChanged(offsetX, offsetY);

            _valueChanged?.Invoke(this, (offsetX, offsetY));
        }

        private event EventHandler<(int, int)> _valueChanged;

        public async Task RegisterAsync(EventHandler<(int, int)> handler)
        {
            using (await _registerMutex.WaitAsync())
            {
                if (_accelerometer == null && !_loaded)
                {
                    _loaded = true;
                    _accelerometer = await Task.Run(() => Accelerometer.GetDefault());
                }

                _valueChanged += handler;
                _accelerometer.ReadingChanged += ReadingChanged;
            }
        }

        public async Task UnregisterAsync(EventHandler<(int, int)> handler)
        {
            using (await _registerMutex.WaitAsync())
            {
                if (_accelerometer != null)
                {
                    _accelerometer.ReadingChanged -= ReadingChanged;
                }

                _valueChanged -= handler;
            }
        }

        public float getScale(double boundsWidth, double boundsHeight)
        {
            int offset = 16;
            return (float)Math.Max((boundsWidth + offset * 2) / boundsWidth, (boundsHeight + offset * 2) / boundsHeight);
        }
    }
}
