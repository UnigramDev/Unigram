//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using System;
using System.Numerics;
using System.Threading;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Windows.Foundation;
using Windows.UI;

namespace Unigram.Controls.Chats
{
    public class ChatBackgroundRenderer : IndividualAnimatedControl<object>
    {
        private readonly SemaphoreSlim _additionalResourcesLock = new(1, 1);

        private CanvasBitmap _pattern;
        private string _patternPath;

        private File _background;
        private int _backgroundId;
        private bool _vector = false;
        private bool _blur = false;
        private bool _dark = false;
        private byte _intensity = 255;

        private BackgroundFill _backgroundFill;

        private string _fileToken;

        public ChatBackgroundRenderer()
            : base(false, false)
        {
            DefaultStyleKey = typeof(ChatBackgroundRenderer);
            Loaded += (s, args) =>
            {
                _animation = new object();

                UpdateLayout(false);
                OnSourceChanged();
            };
        }

        protected override CanvasBitmap CreateBitmap(CanvasDevice device)
        {
            double ratioX = 50 / _currentSize.X;
            double ratioY = 50 / _currentSize.Y;
            double ratio = Math.Max(ratioX, ratioY);

            var width = (int)(_currentSize.X * ratio);
            var height = (int)(_currentSize.Y * ratio);

            CreateAdditionalResources(device);

            bool needsCreate = _bitmap == null;
            needsCreate |= _bitmap?.Size.Width != width || _bitmap?.Size.Height != height;
            needsCreate |= _bitmap?.Device != device;
            needsCreate &= _currentSize.X > 0 && _currentSize.Y > 0;

            if (needsCreate)
            {
                var bitmap = CreateTarget(device, width, height);

                if (_backgroundFill is BackgroundFillGradient or BackgroundFillSolid)
                {
                    using (var args = bitmap.CreateDrawingSession())
                    using (var brush = _backgroundFill.ToCanvasBrush(bitmap, bitmap.SizeInPixels.Width, bitmap.SizeInPixels.Height))
                    {
                        args.FillRectangle(new Rect(0, 0, bitmap.Size.Width, bitmap.Size.Height), brush);
                    }
                }
                else if (_backgroundFill is BackgroundFillFreeformGradient && _easing != null)
                {
                    var index = Math.Max(0, _index - 1);
                    bitmap.SetPixelBytes(GenerateGradient(width, height, _colors, _easing[index]));
                }

                return bitmap;
            }

            return null;
        }

        private async void CreateAdditionalResources(CanvasDevice device)
        {
            await _additionalResourcesLock.WaitAsync();

            bool needsCreate = _pattern == null;
            needsCreate |= _pattern?.Device != device;
            needsCreate |= _background == null || _background.Local.Path != _patternPath;

            if (needsCreate)
            {
                if (_pattern != null)
                {
                    _pattern.Dispose();
                    _pattern = null;
                    _patternPath = null;
                }

                if (_background != null)
                {
                    if (_vector)
                    {
                        _patternPath = _background.Local.Path;
                        _pattern = await PlaceholderHelper.GetPatternBitmapAsync(device, _background);
                    }
                    else
                    {
                        _patternPath = _background.Local.Path;
                        _pattern = await CanvasBitmap.LoadAsync(device, _background.Local.Path);
                    }
                }
            }

            _additionalResourcesLock.Release();

            if (needsCreate)
            {
                // Must happen asynchronously because the method
                // might be called from within a drawing session
                // causing an exception to be thrown by DirectX
                _dispatcher.TryEnqueue(Invalidate);
            }
        }

        protected override void OnSurfaceContentsLost()
        {
            if (_pattern != null)
            {
                _pattern.Dispose();
                _pattern = null;
                _patternPath = null;
            }
        }

        protected override void Dispose()
        {
            _additionalResourcesLock.Wait();

            lock (_drawFrameLock)
            {
                if (_pattern != null)
                {
                    _pattern.Dispose();
                    _pattern = null;
                    _patternPath = null;
                }
            }

            _additionalResourcesLock.Release();
        }

        protected override void DrawFrame(CanvasImageSource sender, CanvasDrawingSession args)
        {
            if (_pattern != null && _bitmap != null && _additionalResourcesLock.Wait(0))
            {
                if (_backgroundFill != null)
                {
                    if (_dark)
                    {
                        using var scale2 = new ScaleEffect { Source = _pattern, BorderMode = EffectBorderMode.Hard, Scale = new Vector2(0.5f, 0.5f) };
                        using var colorize = new TintEffect { Source = scale2, Color = Color.FromArgb(_intensity, 00, 00, 00) };
                        using var border = new BorderEffect { Source = colorize, ExtendX = CanvasEdgeBehavior.Wrap, ExtendY = CanvasEdgeBehavior.Wrap };
                        using var effect = new ColorMatrixEffect
                        {
                            Source = border,
                            ColorMatrix = new Matrix5x4
                            {
#pragma warning disable format
                                M11 = 1, M12 = 0, M13 = 0, M14 = 0,
                                M21 = 0, M22 = 1, M23 = 0, M24 = 0,
                                M31 = 0, M32 = 0, M33 = 1, M34 = 0,
                                M41 = 0, M42 = 0, M43 = 0, M44 =-1,
                                M51 = 0, M52 = 0, M53 = 0, M54 = 1
#pragma warning restore format
                            }
                        };

                        args.DrawImage(_bitmap, new Rect(0, 0, sender.Size.Width, sender.Size.Height), new Rect(0, 0, sender.Size.Width, sender.Size.Height));
                        args.DrawImage(effect, new Rect(0, 0, sender.Size.Width, sender.Size.Height), new Rect(0, 0, sender.Size.Width, sender.Size.Height));
                    }
                    else
                    {
                        var parentSize = new Vector2(sender.SizeInPixels.Width, sender.SizeInPixels.Height);
                        var bitmapSize = _bitmap.Size.ToVector2();

                        using var scale = new ScaleEffect { Source = _bitmap, BorderMode = EffectBorderMode.Hard, Scale = new Vector2(parentSize.X / bitmapSize.X, parentSize.Y / bitmapSize.Y) };
                        using var scale2 = new ScaleEffect { Source = _pattern, BorderMode = EffectBorderMode.Hard, Scale = new Vector2(0.5f, 0.5f) };
                        using var tint = new TintEffect { Source = scale2, Color = Color.FromArgb(_intensity, 00, 00, 00) };
                        using var border = new BorderEffect { Source = tint, ExtendX = CanvasEdgeBehavior.Wrap, ExtendY = CanvasEdgeBehavior.Wrap };
                        using var effect = new BlendEffect { Foreground = border, Background = scale, Mode = BlendEffectMode.SoftLight };

                        args.DrawImage(effect, new Rect(0, 0, sender.Size.Width, sender.Size.Height), new Rect(0, 0, sender.Size.Width, sender.Size.Height));
                    }
                }
                else
                {
                    var width = _pattern.Size.Width;
                    var height = _pattern.Size.Height;

                    double ratioX = sender.Size.Width / width;
                    double ratioY = sender.Size.Height / height;

                    if (ratioX > ratioY)
                    {
                        width = sender.Size.Width;
                        height *= ratioX;
                    }
                    else
                    {
                        width *= ratioY;
                        height = sender.Size.Height;
                    }

                    var y = (sender.Size.Height - height) / 2;
                    var x = (sender.Size.Width - width) / 2;

                    if (_blur)
                    {
                        using (var effect = new GaussianBlurEffect { Source = _pattern, BlurAmount = 12, BorderMode = EffectBorderMode.Hard })
                        {
                            args.DrawImage(effect, new Rect(x, y, width, height), new Rect(0, 0, _pattern.Size.Width, _pattern.Size.Height));
                        }
                    }
                    else
                    {
                        args.DrawImage(_pattern, new Rect(x, y, width, height));
                    }
                }

                _additionalResourcesLock.Release();
            }
            else if (_pattern != null && _dark)
            {
                args.Clear(Colors.Black);
            }
            else
            {
                args.DrawImage(_bitmap, new Rect(0, 0, sender.Size.Width, sender.Size.Height));
            }
        }

        protected override void NextFrame()
        {
            if (_bitmap is not CanvasRenderTarget target)
            {
                return;
            }

            var width = (int)_bitmap.SizeInPixels.Width;
            var height = (int)_bitmap.SizeInPixels.Height;

            if (_backgroundFill is BackgroundFillGradient or BackgroundFillSolid)
            {
                using (var args = target.CreateDrawingSession())
                using (var brush = _backgroundFill.ToCanvasBrush(target, target.SizeInPixels.Width, target.SizeInPixels.Height))
                {
                    args.FillRectangle(new Rect(0, 0, target.Size.Width, target.Size.Height), brush);
                }

                Subscribe(false);
            }
            else if (_backgroundFill is BackgroundFillFreeformGradient)
            {
                if (_easing == null)
                {
                    UpdateLayout(false);
                }

                if (_index >= _easing?.Length)
                {
                    _index = _easing.Length - 1;
                }

                _bitmap.SetPixelBytes(GenerateGradient(width, height, _colors, _easing[_index++]));

                if (_index >= _easing.Length)
                {
                    Subscribe(false);
                }
            }
        }

        public void Next()
        {
            UpdateLayout(true);
            Play();
        }

        protected override void SourceChanged()
        {
            //throw new NotImplementedException();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            _layoutRoot?.Measure(new Size(availableSize.Width, availableSize.Height));
            _canvas?.Measure(new Size(availableSize.Width, availableSize.Height));

            return new Size(0, 0);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _layoutRoot?.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
            _canvas?.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));

            if (_canvas != null)
            {
                _canvas.MaxWidth = finalSize.Width;
                _canvas.MaxHeight = finalSize.Height;
            }

            return finalSize;
        }

        private Vector2[][] _easing;
        private int _index;

        private int _phase;

        public void UpdateSource(IClientService clientService, Background background, bool thumbnail)
        {
            if (_fileToken is string fileToken)
            {
                _fileToken = null;
                UpdateManager.Unsubscribe(this);
            }

            if (background.Type is BackgroundTypeFill typeFill)
            {
                if (typeFill.Fill is BackgroundFillFreeformGradient freeform)
                {
                    for (int i = 0; i < freeform.Colors.Count; i++)
                    {
                        _colors[i] = freeform.Colors[i].ToColor();
                    }
                }

                _backgroundFill = typeFill.Fill;
                _background = null;
                _backgroundId = 0;
            }
            else if (background.Type is BackgroundTypePattern typePattern)
            {
                if (typePattern.Fill is BackgroundFillFreeformGradient freeform)
                {
                    for (int i = 0; i < freeform.Colors.Count; i++)
                    {
                        _colors[i] = freeform.Colors[i].ToColor();
                    }
                }

                _dark = typePattern.IsInverted;
                _intensity = (byte)(255 * (typePattern.Intensity / 100d));

                _backgroundFill = typePattern.Fill;

                var file = background.Document.DocumentValue;
                if (thumbnail && background.Document.Thumbnail != null)
                {
                    file = background.Document.Thumbnail.File;
                }
                else
                {
                    thumbnail = false;
                }

                if (file.Local.IsDownloadingCompleted)
                {
                    _background = file;
                    _backgroundId = 0;
                    _vector = thumbnail is false && background.Document.MimeType == "application/x-tgwallpattern";
                }
                else
                {
                    _background = null;
                    _backgroundId = file.Id;
                    _vector = thumbnail is false && background.Document.MimeType == "application/x-tgwallpattern";

                    if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                    {
                        clientService.DownloadFile(file.Id, 16);
                    }

                    UpdateManager.Subscribe(background, clientService, file, ref _fileToken, UpdateFile, true);
                }
            }
            else if (background.Type is BackgroundTypeWallpaper typeWallpaper)
            {
                _backgroundFill = null;

                _blur = typeWallpaper.IsBlurred;

                var file = background.Document.DocumentValue;
                if (thumbnail && background.Document.Thumbnail != null)
                {
                    file = background.Document.Thumbnail.File;
                }
                else
                {
                    thumbnail = false;
                }

                if (file.Local.IsDownloadingCompleted)
                {
                    _background = file;
                    _backgroundId = 0;
                    _vector = thumbnail is false && background.Document.MimeType == "application/x-tgwallpattern";
                }
                else
                {
                    _background = null;
                    _backgroundId = file.Id;
                    _vector = thumbnail is false && background.Document.MimeType == "application/x-tgwallpattern";

                    if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                    {
                        clientService.DownloadFile(file.Id, 16);
                    }

                    UpdateManager.Subscribe(background, clientService, file, ref _fileToken, UpdateFile, true);
                }
            }

            if (_surface != null)
            {
                CreateAdditionalResources(_surface.Device);
            }

            this.BeginOnUIThread(OnSourceChanged);
        }

        private void UpdateFile(object target, File file)
        {
            if (file.Id == _backgroundId && target is Background background)
            {
                UpdateSource(null, background, !_vector);
            }
        }

        private readonly Color[] _colors = new Color[]
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

        public void UpdateLayout(bool animate = false)
        {
            if (animate)
            {
                _phase++;
            }

            var next = Gather(_positions, _phase % 8);
            if (next.Length > 0 && animate)
            {
                var prev = Gather(_positions, (_phase - 1) % 8);

                var animation = new Vector2[30][];

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

                    animation[i] = current;
                }

                _easing = animation;
                _index = 0;
            }
            else
            {
                _easing = new Vector2[][] { next };
                _index = 0;
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

        public static unsafe byte[] GenerateGradient(int width, int height, Color[] colors, Vector2[] positions)
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
