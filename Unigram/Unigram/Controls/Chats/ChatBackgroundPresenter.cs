using Microsoft.Graphics.Canvas.Effects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Controls.Brushes;
using Unigram.Services;
using Unigram.Services.Settings;
using Unigram.Services.Updates;
using Unigram.Views;
using Windows.Storage;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;

namespace Unigram.Controls.Chats
{
    public class ChatBackgroundPresenter : Grid, IHandle<UpdateWallpaper>
    {
        private int _session;
        private IEventAggregator _aggregator;
        private ISettingsService _settings;

        private ContentControl _container;

        private Rectangle _imageBackground;
        private Rectangle _colorBackground;

        private Visual _motionVisual;
        private Compositor _compositor;

        private WallpaperParallaxEffect _parallaxEffect;

        public ChatBackgroundPresenter()
        {
            _parallaxEffect = new WallpaperParallaxEffect();
            _container = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, IsTabStop = false };
            _container.SizeChanged += OnSizeChanged;

            Children.Add(_container);

            RegisterPropertyChangedCallback(OpacityProperty, OnOpacityChanged);



            _motionVisual = ElementCompositionPreview.GetElementVisual(_container);
            _compositor = _motionVisual.Compositor;

            ElementCompositionPreview.GetElementVisual(this).Clip = _compositor.CreateInsetClip();
        }

        private void OnOpacityChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (Opacity > 0 && _settings != null && _settings.Wallpaper.IsMotionEnabled && _parallaxEffect.IsSupported)
            {
                _parallaxEffect.ValueChanged += OnParallaxChanged;
            }
            else
            {
                _parallaxEffect.ValueChanged -= OnParallaxChanged;
            }
        }

        private void OnParallaxChanged(object sender, (int x, int y) e)
        {
            _motionVisual.Offset = new Vector3(e.x, e.y, 0);
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            _motionVisual.Size = e.NewSize.ToVector2();

            if (_settings != null && _settings.Wallpaper.IsMotionEnabled && _parallaxEffect.IsSupported)
            {
                _motionVisual.CenterPoint = new Vector3((float)e.NewSize.Width / 2, (float)e.NewSize.Height / 2, 0);
                _motionVisual.Scale = new Vector3(_parallaxEffect.getScale(e.NewSize.Width, e.NewSize.Height));
            }
            else
            {
                _motionVisual.CenterPoint = new Vector3(0);
                _motionVisual.Scale = new Vector3(1);
            }
        }

        public void Handle(UpdateWallpaper update)
        {
            this.BeginOnUIThread(() => Update(_session, _settings.Wallpaper));
        }

        public void Update(int session, ISettingsService settings, IEventAggregator aggregator)
        {
            _session = session;
            _aggregator = aggregator;
            _settings = settings;

            aggregator.Subscribe(this);
            Update(session, settings.Wallpaper);
        }

        private async void Update(int session, WallpaperSettings settings)
        {
            try
            {
                if (settings.SelectedColor == 0)
                {
                    if (settings.SelectedBackground != 1000001)
                    {
                        var item = await ApplicationData.Current.LocalFolder.TryGetItemAsync($"{session}\\{Constants.WallpaperFileName}");
                        if (item is StorageFile file)
                        {
                            if (_imageBackground == null)
                                _imageBackground = new Rectangle();

                            using (var stream = await file.OpenReadAsync())
                            {
                                var bitmap = new BitmapImage();
                                await bitmap.SetSourceAsync(stream);
                                _imageBackground.Fill = new ImageBrush { ImageSource = bitmap, AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center, Stretch = Stretch.UniformToFill };
                            }

                            _container.Content = _imageBackground;
                        }
                    }
                    else if (SettingsService.Current.Appearance.RequestedTheme.HasFlag(TelegramTheme.Light))
                    {
                        if (_colorBackground == null)
                            _colorBackground = new Rectangle();

                        _colorBackground.Fill = new TiledBrush { Source = new Uri("ms-appx:///Assets/Images/DefaultBackground.theme-light.png") };
                        _container.Content = _colorBackground;
                    }
                    else
                    {
                        if (_colorBackground == null)
                            _colorBackground = new Rectangle();

                        _colorBackground.Fill = new TiledBrush { Source = new Uri("ms-appx:///Assets/Images/DefaultBackground.theme-dark.png") };
                        _container.Content = _colorBackground;

                        //_container.Content = null;
                    }
                }
                else
                {
                    if (_colorBackground == null)
                        _colorBackground = new Rectangle();

                    _colorBackground.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF,
                        (byte)((settings.SelectedColor >> 16) & 0xFF),
                        (byte)((settings.SelectedColor >> 8) & 0xFF),
                        (byte)((settings.SelectedColor & 0xFF))));

                    _container.Content = _colorBackground;
                }

                InitializeMotion(settings);
            }
            catch
            {
                if (SettingsService.Current.Appearance.RequestedTheme.HasFlag(TelegramTheme.Light))
                {
                    if (_colorBackground == null)
                        _colorBackground = new Rectangle();

                    _colorBackground.Fill = new TiledBrush { Source = new Uri("ms-appx:///Assets/Images/DefaultBackground.theme-light.png") };
                    _container.Content = _colorBackground;
                }
                else
                {
                    if (_colorBackground == null)
                        _colorBackground = new Rectangle();

                    _colorBackground.Fill = new TiledBrush { Source = new Uri("ms-appx:///Assets/Images/DefaultBackground.theme-dark.png") };
                    _container.Content = _colorBackground;

                    //_container.Content = null;
                }
            }
        }

        private void InitializeMotion(WallpaperSettings settings)
        {
            if (settings.IsMotionEnabled && _parallaxEffect.IsSupported)
            {
                _motionVisual.CenterPoint = new Vector3((float)ActualWidth / 2, (float)ActualHeight / 2, 0);
                _motionVisual.Scale = new Vector3(_parallaxEffect.getScale(ActualWidth, ActualHeight));

                _parallaxEffect.ValueChanged += OnParallaxChanged;
            }
            else
            {
                _motionVisual.CenterPoint = new Vector3(0);
                _motionVisual.Scale = new Vector3(1);

                _parallaxEffect.ValueChanged -= OnParallaxChanged;
            }
        }
    }
}
