//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls.Chats
{
    public class ChatBackgroundControl : Grid
    {
        private IClientService _clientService;
        private IEventAggregator _aggregator;

        private Background _oldBackground = new Background();
        private bool? _oldDark;

        private readonly ChatBackgroundPresenter _presenter;

        private readonly Compositor _compositor;

        public ChatBackgroundControl()
        {
            _presenter = new ChatBackgroundPresenter();
            _compositor = Window.Current.Compositor;

            ElementCompositionPreview.GetElementVisual(this).Clip = _compositor.CreateInsetClip();

            Children.Add(_presenter);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _aggregator?.Subscribe<UpdateSelectedBackground>(this, Handle);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _aggregator?.Unsubscribe(this);
        }

        private bool IsDarkTheme => WindowContext.Current.ActualTheme == ElementTheme.Dark;

        public void Handle(UpdateSelectedBackground update)
        {
            this.BeginOnUIThread(() =>
            {
                if (update.ForDarkTheme == IsDarkTheme)
                {
                    var background = update.Background;

                    SyncBackgroundWithChatTheme(ref background, update.ForDarkTheme, out int dimming);
                    UpdateBackground(background, update.ForDarkTheme, dimming);
                }
            });
        }

        public void Update(IClientService clientService, IEventAggregator aggregator)
        {
            _clientService = clientService;
            _aggregator = aggregator;
            _aggregator?.Subscribe<UpdateSelectedBackground>(this, Handle);

            UpdateBackground(clientService.SelectedBackground, IsDarkTheme, 0);
        }

        public void Update(Background background, bool forDarkTheme)
        {
            if (forDarkTheme == IsDarkTheme)
            {
                SyncBackgroundWithChatTheme(ref background, forDarkTheme, out int dimming);
                UpdateBackground(background, forDarkTheme, dimming);
            }
        }

        private void SyncBackgroundWithChatTheme(ref Background background, bool forDarkTheme, out int dimming)
        {
            // I'm not a big fan of this, but this is the easiest way to keep background in sync
            if (Theme.Current.ChatBackground != null)
            {
                background = Theme.Current.ChatBackground.Background;
                dimming = forDarkTheme
                    ? Theme.Current.ChatBackground.DarkThemeDimming
                    : 0;
            }
            else if (Theme.Current.ChatTheme != null)
            {
                dimming = 0;
                background = forDarkTheme
                    ? Theme.Current.ChatTheme?.DarkSettings.Background
                    : Theme.Current.ChatTheme?.LightSettings.Background;
            }
            else
            {
                dimming = 0;
            }
        }

        public void UpdateBackground()
        {
            if (_oldBackground?.Type is BackgroundTypeFill updateFill && updateFill.Fill is BackgroundFillFreeformGradient)
            {
                _presenter.Next();
            }
            else if (_oldBackground?.Type is BackgroundTypePattern updatePattern && updatePattern.Fill is BackgroundFillFreeformGradient)
            {
                _presenter.Next();
            }
        }

        private void UpdateBackground(Background background, bool dark, int dimming)
        {
            if (background == null)
            {
                var freeform = dark ? new[] { 0x6C7FA6, 0x2E344B, 0x7874A7, 0x333258 } : new[] { 0xDBDDBB, 0x6BA587, 0xD5D88D, 0x88B884 };
                background = new Background(0, true, dark, string.Empty,
                    new Document(string.Empty, "application/x-tgwallpattern", null, null, TdExtensions.GetLocalFile("Assets\\Background.tgv", "Background")),
                    new BackgroundTypePattern(new BackgroundFillFreeformGradient(freeform), dark ? 100 : 50, dark, false));
            }

            if (_oldDark == dark && BackgroundEquals(_oldBackground, background))
            {
                return;
            }

            _oldBackground = background;
            _oldDark = dark;

            _presenter.UpdateSource(_clientService, background, false);

            if (dark && dimming != 0)
            {
                _presenter.Opacity = 1 - (dimming / 100d);
                Background = new SolidColorBrush(Colors.Black);
            }
            else
            {
                _presenter.Opacity = 1;
                Background = null;
            }
        }

        public static bool BackgroundEquals(Background prev, Background next, bool fast = false)
        {
            if (prev == null)
            {
                return next == null;
            }

            if (fast && prev.Id != next.Id)
            {
                return false;
            }

            if (prev.Type is BackgroundTypeFill prevFill && next.Type is BackgroundTypeFill nextFill)
            {
                return FillEquals(prevFill.Fill, nextFill.Fill);
            }
            else if (prev.Type is BackgroundTypePattern prevPattern && next.Type is BackgroundTypePattern nextPattern)
            {
                return prevPattern.IsInverted == nextPattern.IsInverted
                    && prevPattern.Intensity == nextPattern.Intensity
                    && prev.Document?.DocumentValue.Id == next.Document?.DocumentValue.Id
                    && FillEquals(prevPattern.Fill, nextPattern.Fill);
            }
            else if (prev.Type is BackgroundTypeWallpaper prevWallpaper && next.Type is BackgroundTypeWallpaper nextWallpaper)
            {
                return prevWallpaper.IsBlurred == nextWallpaper.IsBlurred
                    && prev.Document?.DocumentValue.Id == next.Document?.DocumentValue.Id;
            }

            return Equals(prev, next);
        }

        public static bool FillEquals(BackgroundFill prev, BackgroundFill next)
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
            Logger.Debug();

            if (_fill is BackgroundFillFreeformGradient freeform)
            {
                _background.UpdateLayout(Background as ImageBrush, e.NewSize.Width, e.NewSize.Height, freeform);
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
        public int Phase => _phase;

        public ChatBackgroundFreeform(bool random = true)
        {
            if (random)
            {
                _phase = new Random().Next(0, 7);
            }
        }

        public void UpdateLayout(Rectangle target, BackgroundFillFreeformGradient freeform = null)
        {
            if (target.Fill is ImageBrush brush)
            {
                UpdateLayout(brush, target.ActualWidth, target.ActualHeight, freeform);
            }
        }

        public void UpdateLayout(ImageBrush target, double actualWidth, double actualHeight, BackgroundFillFreeformGradient freeform = null)
        {
            if (target == null || actualWidth == 0 || actualHeight == 0)
            {
                return;
            }

            var colors = freeform?.GetColors() ?? _colors;

            double ratioX = 50 / actualWidth;
            double ratioY = 50 / actualHeight;
            double ratio = Math.Max(ratioX, ratioY);

            var width = (int)(actualWidth * ratio);
            var height = (int)(actualHeight * ratio);

            var next = Gather(_positions, _phase % 8);
            target.ImageSource = GenerateGradient(width, height, colors, next);
        }

        public static ImageSource Create(BackgroundFillFreeformGradient freeform, int offset = 0)
        {
            var colors = freeform.GetColors();

            var next = Gather(_positions, offset % 8);
            var bitmap = GenerateGradient(50, 50, colors, next);

            return bitmap;
        }

        public void Next(BackgroundFillFreeformGradient freeform, WriteableBitmap bitmap, Vector2[] positions)
        {
            var colors = freeform.GetColors();
            GenerateGradient(bitmap, colors, positions);
            bitmap.Invalidate();
        }

        public Vector2[][] Next()
        {
            _phase++;

            var next = Gather(_positions, _phase % 8);
            var prev = Gather(_positions, (_phase - 1) % 8);

            const float h = 27f;
            var d1x = (next[0].X - prev[0].X) / h;
            var d1y = (next[0].Y - prev[0].Y) / h;
            var d2x = (next[1].X - prev[1].X) / h;
            var d2y = (next[1].Y - prev[1].Y) / h;
            var d3x = (next[2].X - prev[2].X) / h;
            var d3y = (next[2].Y - prev[2].Y) / h;
            var d4x = (next[3].X - prev[3].X) / h;
            var d4y = (next[3].Y - prev[3].Y) / h;

            var stops = new Vector2[30][];

            for (int i = 0; i < 30; i++)
            {
                stops[i] = new Vector2[]
                {
                    new Vector2(prev[0].X + d1x * _curve[i * 2]!, prev[0].Y + d1y * _curve[i * 2]!),
                    new Vector2(prev[1].X + d2x * _curve[i * 2]!, prev[1].Y + d2y * _curve[i * 2]!),
                    new Vector2(prev[2].X + d3x * _curve[i * 2]!, prev[2].Y + d3y * _curve[i * 2]!),
                    new Vector2(prev[3].X + d4x * _curve[i * 2]!, prev[3].Y + d4y * _curve[i * 2]!)
                };
            }

            return stops;
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
            GenerateGradient(context, colors, positions);
            return context;
        }

        private static unsafe WriteableBitmap GenerateGradient(WriteableBitmap context, Color[] colors, Vector2[] positions)
        {
            var width = context.PixelWidth;
            var height = context.PixelHeight;
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

        public static unsafe byte[] GenerateGradientData(int width, int height, Color[] colors, Vector2[] positions)
        {
            var imageData = new byte[width * height * 4];

            fixed (byte* imageBytes = imageData)
            {
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
            }

            return imageData;
        }
    }
}
