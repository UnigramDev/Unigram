using Microsoft.Graphics.Canvas.Effects;
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls.Brushes;
using Unigram.Services;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
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
            _defaultBackground.UpdateLayout(_colorBackground);
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

        private void Update(int session, Background background, bool dark)
        {
            if (BackgroundEquals(_oldBackground, background) && _oldDark == dark)
            {
                if (background == null && ActualTheme == ElementTheme.Light)
                {
                    _defaultBackground.UpdateLayout(_colorBackground, true);
                }

                return;
            }

            _oldBackground = background;
            _oldDark = dark;

            if (background == null)
            {
                UpdateBlurred(ActualTheme == ElementTheme.Light, 20);

                _imageBackground.Opacity = 1;

                if (ActualTheme == ElementTheme.Light)
                {
                    _colorBackground.Fill = new ImageBrush { AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center, Stretch = Stretch.UniformToFill };
                    _imageBackground.Fill = null;
                    _defaultBackground.UpdateLayout(_colorBackground);
                }
                else
                {
                    _colorBackground.Fill = null;
                    _imageBackground.Fill = new TiledBrush { Source = new Uri("ms-appx:///Assets/Images/DefaultBackground.theme-dark.png") };
                }
            }
            else if (background.Type is BackgroundTypeFill typeFill)
            {
                UpdateBlurred(false);

                _colorBackground.Fill = typeFill.ToBrush();
                _imageBackground.Opacity = 1;
                _imageBackground.Fill = null;
            }
            else if (background.Type is BackgroundTypePattern typePattern)
            {
                UpdateBlurred(false);

                _colorBackground.Fill = typePattern.ToBrush();
                _imageBackground.Opacity = typePattern.Intensity / 100d;

                var document = background.Document.DocumentValue;
                if (document.Local.IsDownloadingCompleted)
                {
                    if (string.Equals(background.Document.MimeType, "application/x-tgwallpattern", StringComparison.OrdinalIgnoreCase))
                    {
                        _imageBackground.Fill = new TiledBrush { SvgSource = PlaceholderHelper.GetVectorSurface(null, document, typePattern.GetForeground()) };
                    }
                    else
                    {
                        _imageBackground.Fill = new ImageBrush { ImageSource = new BitmapImage(UriEx.ToLocal(document.Local.Path)), AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center, Stretch = Stretch.UniformToFill };
                    }
                }
            }
            else if (background.Type is BackgroundTypeWallpaper typeWallpaper)
            {
                UpdateBlurred(typeWallpaper.IsBlurred);

                _colorBackground.Fill = null;
                _imageBackground.Opacity = 1;

                var document = background.Document.DocumentValue;
                if (document.Local.IsDownloadingCompleted)
                {
                    _imageBackground.Fill = new ImageBrush { ImageSource = new BitmapImage(UriEx.ToLocal(document.Local.Path)), AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center, Stretch = Stretch.UniformToFill };
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
            Background = new ImageBrush { AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center, Stretch = Stretch.UniformToFill };

            SizeChanged += OnSizeChanged;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            _background.UpdateLayout(Background as ImageBrush, e.NewSize.Width, e.NewSize.Height);
        }
    }

    public class ChatBackgroundDefault
    {
        private readonly Color[] _colors = new Color[]
        {
            Color.FromArgb(0xFF, 0x7F, 0xA3, 0x81),
            Color.FromArgb(0xFF, 0xFF, 0xF5, 0xC5),
            Color.FromArgb(0xFF, 0x33, 0x6F, 0x55),
            Color.FromArgb(0xFF, 0xFB, 0xE3, 0x7D)
        };

        private readonly Vector2[] _positions = new Vector2[]
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

        private readonly float[] _curve = new float[]
        {
            0f, 0.25f, 0.50f, 0.75f, 1f, 1.5f, 2f, 2.5f, 3f, 3.5f, 4f, 5f, 6f, 7f, 8f, 9f, 10f, 11f, 12f,
            13f, 14f, 15f, 16f, 17f, 18f, 18.3f, 18.6f, 18.9f, 19.2f, 19.5f, 19.8f, 20.1f, 20.4f, 20.7f,
            21.0f, 21.3f, 21.6f, 21.9f, 22.2f, 22.5f, 22.8f, 23.1f, 23.4f, 23.7f, 24.0f, 24.3f, 24.6f,
            24.9f, 25.2f, 25.5f, 25.8f, 26.1f, 26.3f, 26.4f, 26.5f, 26.6f, 26.7f, 26.8f, 26.9f, 27f,
        };

        private int _phase;

        public ChatBackgroundDefault()
        {
            _phase = new Random().Next(0, 7);
        }

        public void UpdateLayout(Rectangle target, bool animate = false)
        {
            if (target.Fill is ImageBrush brush)
            {
                UpdateLayout(brush, target.ActualWidth, target.ActualHeight, animate);
            }
        }

        public void UpdateLayout(ImageBrush target, double actualWidth, double actualHeight, bool animate = false)
        {
            if (target == null || actualWidth == 0 || actualHeight == 0)
            {
                return;
            }

            if (animate)
            {
                _phase++;
            }

            double ratioX = 50 / actualWidth;
            double ratioY = 50 / actualHeight;
            double ratio = Math.Max(ratioX, ratioY);

            var width = (int)(actualWidth * ratio);
            var height = (int)(actualHeight * ratio);

            var next = Gather(_positions, _phase % 8);
            if (next.Length > 0 && animate)
            {
                var prev = Gather(_positions, (_phase - 1) % 8);

                var animation = new ObjectAnimationUsingKeyFrames();

                const float h = 27f;
                var d1x = (next[0].X - prev[0].X) / h;
                var d1y = (next[0].Y - prev[0].Y) / h;
                var d2x = (next[1].X - prev[1].X) / h;
                var d2y = (next[1].Y - prev[1].Y) / h;
                var d3x = (next[2].X - prev[2].X) / h;
                var d3y = (next[2].Y - prev[2].Y) / h;
                var d4x = (next[3].X - prev[3].X) / h;
                var d4y = (next[3].Y - prev[3].Y) / h;

                for (int i = 0; i < 30; i++)
                {
                    var current = new Vector2[]
                    {
                        new Vector2(prev[0].X + d1x * _curve[i * 2]!, prev[0].Y + d1y * _curve[i * 2]!),
                        new Vector2(prev[1].X + d2x * _curve[i * 2]!, prev[1].Y + d2y * _curve[i * 2]!),
                        new Vector2(prev[2].X + d3x * _curve[i * 2]!, prev[2].Y + d3y * _curve[i * 2]!),
                        new Vector2(prev[3].X + d4x * _curve[i * 2]!, prev[3].Y + d4y * _curve[i * 2]!)
                    };

                    animation.KeyFrames.Add(new DiscreteObjectKeyFrame
                    {
                        Value = GenerateGradient(width, height, _colors, current),
                        KeyTime = TimeSpan.FromMilliseconds(500 / 30f * i),
                    });
                }

                Storyboard.SetTarget(animation, target);
                Storyboard.SetTargetProperty(animation, "ImageSource");

                var storyboard = new Storyboard();
                storyboard.Children.Add(animation);
                storyboard.Begin();
            }
            else
            {
                target.ImageSource = GenerateGradient(width, height, _colors, next);
            }
        }

        private Vector2[] Gather(Vector2[] list, int offset)
        {
            if (offset > 0)
            {
                list = list.Shift(offset);
            }

            var result = new Vector2[list.Length / 2];
            for (int i = 0; i < list.Length / 2; i++)
            {
                result[i] = list[i * 2];
            }

            return result;
        }

        [ComImport]
        [Guid("905A0FEF-BC53-11DF-8C49-001E4FC686DA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IBufferByteAccess
        {
            unsafe void Buffer(out byte* value);
        }

        private unsafe WriteableBitmap GenerateGradient(int width, int height, Color[] colors, Vector2[] positions)
        {
            var context = new WriteableBitmap(width, height);
            var buffer = (IBufferByteAccess)context.PixelBuffer;
            buffer.Buffer(out byte* imageBytes);

            for (int y = 0; y < height; y++)
            {
                var directPixelY = y / (float)height;
                var centerDistanceY = directPixelY - 0.5f;
                var centerDistanceY2 = centerDistanceY * centerDistanceY;

                var lineBytes = imageBytes + width * 4 * y;
                for (int x = 0; x < width; x++)
                {
                    var directPixelX = x / (float)width;

                    var centerDistanceX = directPixelX - 0.5f;
                    var centerDistance = MathF.Sqrt(centerDistanceX * centerDistanceX + centerDistanceY2);

                    var swirlFactor = 0.35f * centerDistance;
                    var theta = swirlFactor * swirlFactor * 0.8f * 8.0f;
                    var sinTheta = MathF.Sin(theta);
                    var cosTheta = MathF.Cos(theta);

                    var pixelX = MathF.Max(0.0f, MathF.Min(1.0f, 0.5f + centerDistanceX * cosTheta - centerDistanceY * sinTheta));
                    var pixelY = MathF.Max(0.0f, MathF.Min(1.0f, 0.5f + centerDistanceX * sinTheta + centerDistanceY * cosTheta));

                    var distanceSum = 0.0f;

                    var r = 0.0f;
                    var g = 0.0f;
                    var b = 0.0f;

                    for (int i = 0; i < colors.Length; i++)
                    {
                        var colorX = positions[i].X;
                        var colorY = positions[i].Y;

                        var distanceX = pixelX - colorX;
                        var distanceY = pixelY - colorY;

                        var distance = MathF.Max(0.0f, 0.9f - MathF.Sqrt(distanceX * distanceX + distanceY * distanceY));
                        distance = distance * distance * distance * distance;
                        distanceSum += distance;

                        r += distance * colors[i].R / 255f;
                        g += distance * colors[i].G / 255f;
                        b += distance * colors[i].B / 255f;
                    }

                    var pixelBytes = lineBytes + x * 4;
                    pixelBytes[0] = (byte)(b / distanceSum * 255.0f);
                    pixelBytes[1] = (byte)(g / distanceSum * 255.0f);
                    pixelBytes[2] = (byte)(r / distanceSum * 255.0f);
                    pixelBytes[3] = 0xff;
                }
            }

            return context;
        }
    }
}
