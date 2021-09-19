using Microsoft.Graphics.Canvas.Effects;
using System;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls.Media;
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
    public class ChatBackgroundPresenter : Grid, IHandle<UpdateSelectedBackground>, IHandle<UpdateFile>
    {
        private int _session;
        private IProtoService _protoService;
        private IEventAggregator _aggregator;

        private Background _oldBackground = new Background();
        private bool? _oldDark;

        private readonly Rectangle _imageBackground;
        private readonly Rectangle _colorBackground;

        private readonly ChatBackgroundFreeform _defaultBackground;

        private readonly Compositor _compositor;

        private SpriteVisual _blurVisual;
        private CompositionEffectBrush _blurBrush;

        public ChatBackgroundPresenter()
        {
            _imageBackground = new Rectangle();
            _colorBackground = new Rectangle();

            _defaultBackground = new ChatBackgroundFreeform();

            Children.Add(_colorBackground);
            Children.Add(_imageBackground);

            _compositor = Window.Current.Compositor;
            ElementCompositionPreview.GetElementVisual(this).Clip = _compositor.CreateInsetClip();

            SizeChanged += OnSizeChanged;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_oldBackground?.Type is BackgroundTypeFill typeFill && typeFill.Fill is BackgroundFillFreeformGradient freeform1)
            {
                _defaultBackground.UpdateLayout(_colorBackground, freeform1);
            }
            else if (_oldBackground?.Type is BackgroundTypePattern typePattern && typePattern.Fill is BackgroundFillFreeformGradient freeform2)
            {
                _defaultBackground.UpdateLayout(_colorBackground, freeform2);
            }
        }

        public void Handle(UpdateFile update)
        {
            if (update.File.Id == _oldBackground?.Document?.DocumentValue.Id)
            {
                var background = _oldBackground;
                _oldBackground.UpdateFile(update.File);
                _oldBackground = null;

                this.BeginOnUIThread(() =>
                {
                    Update(background, ActualTheme == ElementTheme.Dark);
                });
            }
        }

        public void Handle(UpdateSelectedBackground update)
        {
            this.BeginOnUIThread(() =>
            {
                if (update.ForDarkTheme == (ActualTheme == ElementTheme.Dark))
                {
                    var background = update.Background;

                    // I'm not a big fan of this, but this is the easiest way to keep background in sync
                    var chat = update.ForDarkTheme ? Theme.Current.ChatTheme?.DarkSettings.Background : Theme.Current.ChatTheme?.LightSettings.Background;
                    if (chat != null)
                    {
                        background = chat;
                    }

                    Update(background, update.ForDarkTheme);
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
            Update(protoService.SelectedBackground, ActualTheme == ElementTheme.Dark);
        }

        private async void Update(Background background, bool dark)
        {
            if (background == null)
            {
                var freeform = dark ? new[] { 0x1B2836, 0x121A22, 0x1B2836, 0x121A22 } : new[] { 0xDBDDBB, 0x6BA587, 0xD5D88D, 0x88B884 };
                background = new Background(0, true, dark, string.Empty, null,
                    new BackgroundTypeFill(new BackgroundFillFreeformGradient(freeform)));
            }

            if (_oldDark == dark && BackgroundEquals(_oldBackground, background))
            {
                if (background?.Type is BackgroundTypeFill updateFill && updateFill.Fill is BackgroundFillFreeformGradient freeform1)
                {
                    _defaultBackground.UpdateLayout(_colorBackground, freeform1, true);
                }
                else if (background?.Type is BackgroundTypePattern updatePattern && updatePattern.Fill is BackgroundFillFreeformGradient freeform2)
                {
                    _defaultBackground.UpdateLayout(_colorBackground, freeform2, true);
                }

                return;
            }

            _oldBackground = background;
            _oldDark = dark;

            if (background.Type is BackgroundTypeFill typeFill)
            {
                UpdateBlurred(false);

                _imageBackground.Fill = null;
                _imageBackground.Opacity = 1;

                if (typeFill.Fill is BackgroundFillFreeformGradient freeform)
                {
                    _colorBackground.Fill = new ImageBrush { AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center, Stretch = Stretch.UniformToFill };
                    _defaultBackground.UpdateLayout(_colorBackground, freeform);
                }
                else
                {
                    _colorBackground.Fill = typeFill.ToBrush();
                }

                Background = null;
            }
            else if (background.Type is BackgroundTypePattern typePattern)
            {
                UpdateBlurred(false);

                if (typePattern.Fill is BackgroundFillFreeformGradient freeform)
                {
                    _colorBackground.Fill = new ImageBrush { AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center, Stretch = Stretch.UniformToFill };
                    _defaultBackground.UpdateLayout(_colorBackground, freeform);
                }
                else
                {
                    _colorBackground.Fill = typePattern.Fill.ToBrush();
                }

                if (typePattern.IsInverted)
                {
                    _imageBackground.Opacity = 1;
                    _colorBackground.Opacity = typePattern.Intensity / 100d;

                    Background = new SolidColorBrush(Colors.Black);
                }
                else
                {
                    _imageBackground.Opacity = typePattern.Intensity / 100d;
                    _colorBackground.Opacity = 1;

                    Background = null;
                }

                var document = background.Document.DocumentValue;
                if (document.Local.IsDownloadingCompleted)
                {
                    if (string.Equals(background.Document.MimeType, "application/x-tgwallpattern", StringComparison.OrdinalIgnoreCase))
                    {
                        await SetPatternAsync(typePattern, document);
                    }
                    else
                    {
                        _imageBackground.Fill = new ImageBrush { ImageSource = new BitmapImage(UriEx.ToLocal(document.Local.Path)), AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center, Stretch = Stretch.UniformToFill };
                    }
                }
                else if (document.Local.CanBeDownloaded && !document.Local.IsDownloadingActive)
                {
                    _protoService.DownloadFile(document.Id, 16);
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
                else if (document.Local.CanBeDownloaded && !document.Local.IsDownloadingActive)
                {
                    _protoService.DownloadFile(document.Id, 16);
                }

                Background = null;
            }
        }

        private async Task SetPatternAsync(BackgroundTypePattern typePattern, File file)
        {
            if (_imageBackground.Fill is TiledBrush brush)
            {
                brush.Surface = await PlaceholderHelper.GetPatternSurfaceAsync(null, file);
                brush.FallbackColor = typePattern.GetForeground();
                brush.IsInverted = typePattern.IsInverted;
                brush.Update();
            }
            else
            {
                _imageBackground.Fill = new TiledBrush
                {
                    Surface = await PlaceholderHelper.GetPatternSurfaceAsync(null, file),
                    FallbackColor = typePattern.GetForeground(),
                    IsInverted = typePattern.IsInverted,
                };
            }
        }

        private bool BackgroundEquals(Background prev, Background next)
        {
            if (prev == null)
            {
                return next == null;
            }

            if (prev.Type is BackgroundTypeFill prevFill && next.Type is BackgroundTypeFill nextFill)
            {
                return FillEquals(prevFill.Fill, nextFill.Fill);
            }
            else if (prev.Type is BackgroundTypePattern prevPattern && next.Type is BackgroundTypePattern nextPattern)
            {

            }
            else if (prev.Type is BackgroundTypeWallpaper prevWallpaper && next.Type is BackgroundTypeWallpaper nextWallpaper)
            {

            }

            return Equals(prev, next);
        }

        private bool FillEquals(BackgroundFill prev, BackgroundFill next)
        {
            if (prev is BackgroundFillSolid prevSolid && next is BackgroundFillSolid nextSolid)
            {
                return prevSolid.Color == nextSolid.Color;
            }
            else if (prev is BackgroundFillGradient prevGradient && next is BackgroundFillGradient nextGradient)
            {
                return prevGradient.TopColor == nextGradient.TopColor
                    && prevGradient.BottomColor == nextGradient.BottomColor
                    && prevGradient.RotationAngle == nextGradient.RotationAngle;
            }
            else if (prev is BackgroundFillFreeformGradient prevFreeform && next is BackgroundFillFreeformGradient nextFreeform)
            {
                return prevFreeform.Colors.SequenceEqual(nextFreeform.Colors);
            }

            return false;
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

    public class ChatBackgroundPreview : Grid
    {
        private readonly ChatBackgroundFreeform _background = new(false);

        public ChatBackgroundPreview()
        {
            SizeChanged += OnSizeChanged;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_fill is BackgroundFillFreeformGradient freeform)
            {
                _background.UpdateLayout(Background as ImageBrush, e.NewSize.Width, e.NewSize.Height, freeform);
            }
        }

        public void Play()
        {
            if (_fill is BackgroundFillFreeformGradient freeform)
            {
                _background.UpdateLayout(Background as ImageBrush, ActualWidth, ActualHeight, freeform, true);
            }
        }

        private BackgroundFill _fill;
        public BackgroundFill Fill
        {
            get => _fill;
            set
            {
                _fill = value;

                if (value is BackgroundFillFreeformGradient freeform)
                {
                    Background = new ImageBrush { AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center, Stretch = Stretch.UniformToFill };
                    _background.UpdateLayout(Background as ImageBrush, ActualWidth, ActualHeight, freeform);
                }
                else
                {
                    Background = value?.ToBrush();
                }
            }
        }
    }

    public class ChatBackgroundFreeform
    {
        private static readonly Color[] _colors = new Color[]
        {
            Color.FromArgb(0xFF, 0xDB, 0xDD, 0xBB),
            Color.FromArgb(0xFF, 0x6B, 0xA5, 0x87),
            Color.FromArgb(0xFF, 0xD5, 0xD8, 0x8D),
            Color.FromArgb(0xFF, 0x88, 0xB8, 0x84)
        };

        private static readonly Vector2[] _positions = new Vector2[]
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

        private static readonly float[] _curve = new float[]
        {
            0f, 0.25f, 0.50f, 0.75f, 1f, 1.5f, 2f, 2.5f, 3f, 3.5f, 4f, 5f, 6f, 7f, 8f, 9f, 10f, 11f, 12f,
            13f, 14f, 15f, 16f, 17f, 18f, 18.3f, 18.6f, 18.9f, 19.2f, 19.5f, 19.8f, 20.1f, 20.4f, 20.7f,
            21.0f, 21.3f, 21.6f, 21.9f, 22.2f, 22.5f, 22.8f, 23.1f, 23.4f, 23.7f, 24.0f, 24.3f, 24.6f,
            24.9f, 25.2f, 25.5f, 25.8f, 26.1f, 26.3f, 26.4f, 26.5f, 26.6f, 26.7f, 26.8f, 26.9f, 27f,
        };

        private int _phase;

        public ChatBackgroundFreeform(bool random = true)
        {
            if (random)
            {
                _phase = new Random().Next(0, 7);
            }
        }

        public void UpdateLayout(Rectangle target, BackgroundFillFreeformGradient freeform = null, bool animate = false)
        {
            if (target.Fill is ImageBrush brush)
            {
                UpdateLayout(brush, target.ActualWidth, target.ActualHeight, freeform, animate);
            }
        }

        public void UpdateLayout(ImageBrush target, double actualWidth, double actualHeight, BackgroundFillFreeformGradient freeform = null, bool animate = false)
        {
            if (target == null || actualWidth == 0 || actualHeight == 0)
            {
                return;
            }

            if (animate)
            {
                _phase++;
            }

            var colors = freeform?.GetColors() ?? _colors;

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
                        Value = GenerateGradient(width, height, colors, current),
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
                target.ImageSource = GenerateGradient(width, height, colors, next);
            }
        }

        private static Vector2[] Gather(Vector2[] list, int offset)
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

        private static unsafe WriteableBitmap GenerateGradient(int width, int height, Color[] colors, Vector2[] positions)
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
