using Microsoft.Graphics.Canvas.Effects;
using System;
using System.Numerics;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls.Brushes;
using Unigram.Services;
using Unigram.Views;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;

namespace Unigram.Controls.Chats
{
    public class ChatBackgroundPresenter : Grid, IHandle<UpdateSelectedBackground>
    {
        private int _session;
        private IProtoService _protoService;
        private IEventAggregator _aggregator;

        private Background _oldBackground = new Background();
        private bool? _oldDark;

        private Rectangle _imageBackground;
        private Rectangle _colorBackground;

        private SpriteVisual _blurVisual;
        private CompositionEffectBrush _blurBrush;

        private Visual _motionVisual;
        private Compositor _compositor;

        private BackgroundParallaxEffect _parallaxEffect;

        public ChatBackgroundPresenter()
        {
            _parallaxEffect = new BackgroundParallaxEffect();
            _imageBackground = new Rectangle();
            _colorBackground = new Rectangle();
            _colorBackground.SizeChanged += OnSizeChanged;

            Children.Add(_colorBackground);
            Children.Add(_imageBackground);

            RegisterPropertyChangedCallback(OpacityProperty, OnOpacityChanged);



            _motionVisual = ElementCompositionPreview.GetElementVisual(_colorBackground);
            _compositor = _motionVisual.Compositor;

            ElementCompositionPreview.GetElementVisual(this).Clip = _compositor.CreateInsetClip();
        }

        private async void OnOpacityChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (Opacity > 0 && (_protoService?.SelectedBackground?.IsMoving() ?? false) && await _parallaxEffect.IsSupportedAsync())
            {
                await _parallaxEffect.RegisterAsync(OnParallaxChanged);
            }
            else
            {
                await _parallaxEffect.UnregisterAsync(OnParallaxChanged);
            }
        }

        private void OnParallaxChanged(object sender, (int x, int y) e)
        {
            _motionVisual.Offset = new Vector3(e.x, e.y, 0);
        }

        private async void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            _motionVisual.Size = e.NewSize.ToVector2();

            if ((_protoService?.SelectedBackground?.IsMoving() ?? false) && await _parallaxEffect.IsSupportedAsync())
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

        public void Handle(UpdateSelectedBackground update)
        {
            this.BeginOnUIThread(() =>
            {
                if (update.ForDarkTheme == SettingsService.Current.Appearance.IsDarkTheme())
                {
                    Update(_session, _protoService.SelectedBackground, update.ForDarkTheme);
                }
            });
        }

        public void Update(int session, IProtoService protoService, IEventAggregator aggregator)
        {
            _session = session;
            _protoService = protoService;
            _aggregator = aggregator;

            aggregator.Subscribe(this);
            //Update(session, settings.Wallpaper);
            Update(session, protoService.SelectedBackground, SettingsService.Current.Appearance.IsDarkTheme());
        }

        private void Update(int session, Background background, bool dark)
        {
            if (BackgroundEquals(_oldBackground, background) && _oldDark == dark)
            {
                return;
            }

            _oldBackground = background;
            _oldDark = dark;

            if (background == null)
            {
                UpdateMotion(false);
                UpdateBlurred(false);

                Background = null;
                _colorBackground.Opacity = 1;

                if (SettingsService.Current.Appearance.IsLightTheme())
                {
                    _colorBackground.Fill = new TiledBrush { Source = new Uri("ms-appx:///Assets/Images/DefaultBackground.theme-light.png") };
                }
                else
                {
                    _colorBackground.Fill = new TiledBrush { Source = new Uri("ms-appx:///Assets/Images/DefaultBackground.theme-dark.png") };
                }
            }
            else if (background.Type is BackgroundTypeFill typeFill)
            {
                UpdateMotion(false);
                UpdateBlurred(false);

                Background = typeFill.ToBrush();
                _colorBackground.Opacity = 1;
                _colorBackground.Fill = null;
            }
            else if (background.Type is BackgroundTypePattern typePattern)
            {
                UpdateMotion(typePattern.IsMoving);
                UpdateBlurred(false);

                Background = typePattern.ToBrush();
                _colorBackground.Opacity = typePattern.Intensity / 100d;

                var document = background.Document.DocumentValue;
                if (document.Local.IsDownloadingCompleted)
                {
                    if (string.Equals(background.Document.MimeType, "application/x-tgwallpattern", StringComparison.OrdinalIgnoreCase))
                    {
                        _colorBackground.Fill = new TiledBrush { SvgSource = PlaceholderHelper.GetVectorSurface(null, document, typePattern.GetForeground()) };
                    }
                    else
                    {
                        _colorBackground.Fill = new ImageBrush { ImageSource = new BitmapImage(new Uri("file:///" + document.Local.Path)), AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center, Stretch = Stretch.UniformToFill };
                    }
                }
            }
            else if (background.Type is BackgroundTypeWallpaper typeWallpaper)
            {
                UpdateMotion(typeWallpaper.IsMoving);
                UpdateBlurred(typeWallpaper.IsBlurred);

                Background = null;
                _colorBackground.Opacity = 1;

                var document = background.Document.DocumentValue;
                if (document.Local.IsDownloadingCompleted)
                {
                    _colorBackground.Fill = new ImageBrush { ImageSource = new BitmapImage(new Uri("file:///" + document.Local.Path)), AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center, Stretch = Stretch.UniformToFill };
                }
            }
        }

        private bool BackgroundEquals(Background prev, Background next)
        {
            return Equals(prev, next);

            if (prev == null)
            {
                return false;
            }

            if (prev.Type is BackgroundTypeFill prevFill && next.Type is BackgroundTypeFill nextFill)
            {

            }
            else if (prev.Type is BackgroundTypePattern prevPattern && next.Type is BackgroundTypePattern nextPattern)
            {

            }
            else if (prev.Type is BackgroundTypeWallpaper prevWallpaper && next.Type is BackgroundTypeWallpaper nextWallpaper)
            {

            }
        }

        private async void UpdateMotion(bool enabled)
        {
            if (enabled && await _parallaxEffect.IsSupportedAsync())
            {
                _motionVisual.CenterPoint = new Vector3((float)ActualWidth / 2, (float)ActualHeight / 2, 0);
                _motionVisual.Scale = new Vector3(_parallaxEffect.getScale(ActualWidth, ActualHeight));

                await _parallaxEffect.RegisterAsync(OnParallaxChanged);
            }
            else
            {
                _motionVisual.CenterPoint = new Vector3(0);
                _motionVisual.Scale = new Vector3(1);

                await _parallaxEffect.UnregisterAsync(OnParallaxChanged);
            }
        }

        private void UpdateBlurred(bool enabled)
        {
            if (enabled)
            {
                var graphicsEffect = new GaussianBlurEffect
                {
                    Name = "Blur",
                    BlurAmount = 12,
                    BorderMode = EffectBorderMode.Hard,
                    Source = new CompositionEffectSourceParameter("backdrop")
                };

                var effectFactory = _compositor.CreateEffectFactory(graphicsEffect, new[] { "Blur.BlurAmount" });
                var effectBrush = effectFactory.CreateBrush();
                var backdrop = _compositor.CreateBackdropBrush();
                effectBrush.SetSourceParameter("backdrop", backdrop);

                _blurBrush = effectBrush;
                _blurVisual = _compositor.CreateSpriteVisual();
                _blurVisual.RelativeSizeAdjustment = Vector2.One;
                _blurVisual.Brush = _blurBrush;

                ElementCompositionPreview.SetElementChildVisual(_imageBackground, _blurVisual);
            }
            else
            {
                ElementCompositionPreview.SetElementChildVisual(_imageBackground, null);

                _blurBrush?.Dispose();
                _blurBrush = null;

                _blurVisual?.Dispose();
                _blurVisual = null;
            }
        }
    }
}
