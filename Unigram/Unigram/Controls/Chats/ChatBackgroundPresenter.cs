using Microsoft.Graphics.Canvas.Effects;
using System;
using System.Numerics;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls.Brushes;
using Unigram.Services;
using Windows.Foundation.Metadata;
using Windows.UI;
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

        private readonly Rectangle _imageBackground;
        private readonly Rectangle _colorBackground;

        private readonly ChatBackgroundDefault _defaultBackground;

        private readonly Compositor _compositor;

        private SpriteVisual _blurVisual;
        private CompositionEffectBrush _blurBrush;

        public ChatBackgroundPresenter()
        {
            _imageBackground = new Rectangle();
            _colorBackground = new Rectangle();

            _defaultBackground = new ChatBackgroundDefault();

            Children.Add(_colorBackground);
            Children.Add(_imageBackground);

            _compositor = Window.Current.Compositor;
            ElementCompositionPreview.GetElementVisual(this).Clip = _compositor.CreateInsetClip();

            SizeChanged += OnSizeChanged;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            _defaultBackground.UpdateLayout(e.NewSize.ToVector2());
        }

        public void Handle(UpdateSelectedBackground update)
        {
            this.BeginOnUIThread(() =>
            {
                if (update.ForDarkTheme == (ActualTheme == ElementTheme.Dark)) //SettingsService.Current.Appearance.IsDarkTheme())
                {
                    Update(_session, update.Background /*_protoService.SelectedBackground*/, update.ForDarkTheme);
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

        private async void Update(int session, Background background, bool dark)
        {
            if (BackgroundEquals(_oldBackground, background) && _oldDark == dark)
            {
                if (background == null && ApiInfo.CanUseActualFloats)
                {
                    _defaultBackground.UpdateLayout(ActualSize, true);
                }

                return;
            }

            _oldBackground = background;
            _oldDark = dark;

            if (background == null)
            {
                UpdateBlurred(ActualTheme == ElementTheme.Light, 20);

                Background = null;
                _colorBackground.Opacity = 1;

                if (ActualTheme == ElementTheme.Light)
                {
                    _colorBackground.Fill = null;
                    ElementCompositionPreview.SetElementChildVisual(_colorBackground, _defaultBackground.Visual);
                }
                else
                {
                    _colorBackground.Fill = new TiledBrush { Source = new Uri("ms-appx:///Assets/Images/DefaultBackground.theme-dark.png") };
                    ElementCompositionPreview.SetElementChildVisual(_colorBackground, null);
                }
            }
            else if (background.Type is BackgroundTypeFill typeFill)
            {
                UpdateBlurred(false);

                Background = typeFill.ToBrush();
                _colorBackground.Opacity = 1;
                _colorBackground.Fill = null;

                ElementCompositionPreview.SetElementChildVisual(_colorBackground, null);
            }
            else if (background.Type is BackgroundTypePattern typePattern)
            {
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
                        _colorBackground.Fill = new ImageBrush { ImageSource = new BitmapImage(UriEx.ToLocal(document.Local.Path)), AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center, Stretch = Stretch.UniformToFill };
                    }
                }

                ElementCompositionPreview.SetElementChildVisual(_colorBackground, null);
            }
            else if (background.Type is BackgroundTypeWallpaper typeWallpaper)
            {
                UpdateBlurred(typeWallpaper.IsBlurred);

                Background = null;
                _colorBackground.Opacity = 1;

                var document = background.Document.DocumentValue;
                if (document.Local.IsDownloadingCompleted)
                {
                    try
                    {
                        BitmapImage image;
                        _colorBackground.Fill = new ImageBrush { ImageSource = image = new BitmapImage(), AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center, Stretch = Stretch.UniformToFill };

                        var test = await _protoService.GetFileAsync(document);
                        using (var stream = await test.OpenReadAsync())
                        {
                            await image.SetSourceAsync(stream);
                        }
                    }
                    catch { }

                    //_colorBackground.Fill = new ImageBrush { ImageSource = new BitmapImage(UriEx.ToLocal(document.Local.Path)), AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center, Stretch = Stretch.UniformToFill };
                }

                ElementCompositionPreview.SetElementChildVisual(_colorBackground, null);
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

        private void UpdateBlurred(bool enabled, float amount = 12)
        {
            if (enabled)
            {
                var graphicsEffect = new GaussianBlurEffect
                {
                    Name = "Blur",
                    BlurAmount = amount,
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

    public class ChatBackgroundDefaultPreview : Panel
    {
        private readonly ChatBackgroundDefault _background;

        public ChatBackgroundDefaultPreview()
        {
            _background = new ChatBackgroundDefault();
            ElementCompositionPreview.SetElementChildVisual(this, _background.Visual);

            SizeChanged += OnSizeChanged;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            _background.UpdateLayout(e.NewSize.ToVector2());
        }
    }

    public class ChatBackgroundDefault
    {
        private readonly SpriteVisual[] _pivotVisuals = new SpriteVisual[4];
        private int _phase;

        public ChatBackgroundDefault()
        {
            var container = Window.Current.Compositor.CreateContainerVisual();
            container.RelativeSizeAdjustment = Vector2.One;
            container.Clip = Window.Current.Compositor.CreateInsetClip();

            if (ApiInformation.IsMethodPresent("Windows.UI.Composition.Compositor", "CreateRadialGradientBrush"))
            {
                var colors = new Color[]
                {
                    Color.FromArgb(0xFF, 0x7F, 0xA3, 0x81),
                    Color.FromArgb(0xFF, 0xFF, 0xF5, 0xC5),
                    Color.FromArgb(0xFF, 0x33, 0x6F, 0x55),
                    Color.FromArgb(0xFF, 0xFB, 0xE3, 0x7D)
                };

                for (int i = 0; i < colors.Length; i++)
                {
                    var brush = Window.Current.Compositor.CreateRadialGradientBrush();
                    brush.ColorStops.Add(Window.Current.Compositor.CreateColorGradientStop(0.0f, colors[i]));
                    brush.ColorStops.Add(Window.Current.Compositor.CreateColorGradientStop(0.1f, colors[i]));
                    brush.ColorStops.Add(Window.Current.Compositor.CreateColorGradientStop(1.0f, colors[i].WithAlpha(0)));
                    brush.CenterPoint = new Vector2(150, 150);

                    var visual = Window.Current.Compositor.CreateSpriteVisual();
                    visual.CenterPoint = new Vector3(150, 150, 0);
                    visual.Size = new Vector2(300, 300);
                    visual.Brush = brush;

                    _pivotVisuals[i] = visual;
                    container.Children.InsertAtTop(visual);
                }
            }

            Visual = container;
        }

        public ContainerVisual Visual { get; }

        public void UpdateLayout(Vector2 actualSize, bool animate = false)
        {
            if (animate)
            {
                _phase++;
            }

            var positions = new Vector2[]
            {
                new Vector2(0.80f, 0.10f),
                new Vector2(0.60f, 0.20f),
                new Vector2(0.35f, 0.25f),
                new Vector2(0.25f, 0.60f),
                new Vector2(0.20f, 0.90f),
                new Vector2(0.40f, 0.80f),
                new Vector2(0.65f, 0.75f),
                new Vector2(0.75f, 0.40f),
            };

            positions.Shift(_phase % 8);

            for (int i = 0; i < _pivotVisuals.Length; i++)
            {
                if (_pivotVisuals[i] == null)
                {
                    break;
                }

                var minSide = Math.Min(actualSize.X, actualSize.Y);

                var x = minSide;
                var y = minSide;

                if (actualSize.X > actualSize.Y * 2 || actualSize.Y > actualSize.X * 2)
                {
                    x = actualSize.X;
                    y = actualSize.Y;
                }

                var pointCenter = new Vector2(actualSize.X * positions[i * 2].X, actualSize.Y * positions[i * 2].Y);
                var pointSize = new Vector2(x * 2, y * 2);

                if (animate)
                {
                    var size = Window.Current.Compositor.CreateVector2KeyFrameAnimation();
                    size.InsertKeyFrame(1, pointSize);

                    var scale = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
                    scale.InsertKeyFrame(1, new Vector3(pointSize.X / 300, pointSize.Y / 300, 0));

                    var offset = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
                    offset.InsertKeyFrame(1, new Vector3(pointCenter.X - 150, pointCenter.Y - 150, 0));
                    offset.Duration = TimeSpan.FromMilliseconds(500);

                    _pivotVisuals[i].StartAnimation("Scale", scale);
                    _pivotVisuals[i].StartAnimation("Offset", offset);
                }
                else
                {
                    _pivotVisuals[i].Scale = new Vector3(pointSize.X / 300, pointSize.Y / 300, 0);
                    _pivotVisuals[i].Offset = new Vector3(pointCenter.X - 150, pointCenter.Y - 150, 0);
                }
            }
        }
    }
}
