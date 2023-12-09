//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.Graphics.Canvas.Effects;
using System;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Controls.Chats
{
    public class ChatBackgroundPresenter : ControlEx
    {
        private readonly ChatBackgroundFreeform _freeform = new();
        private readonly DispatcherTimer _freeformTimer;

        private LoadedImageSurface _pattern;
        private string _patternPath;
        private string _wallpaperPath;

        private Background _background;
        private bool _vector = false;
        private bool _negative = false;
        private byte _intensity = 255;

        private int _backgroundId;
        private BackgroundFill _backgroundFill;

        private bool _thumbnail;
        private long _fileToken;

        private double _rasterizationScale;

        public ChatBackgroundPresenter()
        {
            DefaultStyleKey = typeof(ChatBackgroundPresenter);
            Connected += OnLoaded;
            Disconnected += OnUnloaded;

            _freeformTimer = new DispatcherTimer();
            _freeformTimer.Interval = TimeSpan.FromMilliseconds(500 / 30d);
            _freeformTimer.Tick += OnTick;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            XamlRoot.Changed += OnRasterizationScaleChanged;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            XamlRoot.Changed -= OnRasterizationScaleChanged;
        }

        private void OnRasterizationScaleChanged(XamlRoot sender, XamlRootChangedEventArgs args)
        {
            var value = sender.RasterizationScale;
            if (value != _rasterizationScale && _background?.Type is BackgroundTypePattern && _background?.Document?.DocumentValue != null)
            {
                UpdatePattern(_background.Document.DocumentValue, value);
            }
        }

        private void OnTick(object sender, object e)
        {
            if (_backgroundFill is BackgroundFillFreeformGradient freeformGradient
                && Background is ImageBrush imageBrush
                && imageBrush.ImageSource is WriteableBitmap bitmap)
            {
                _freeform.Next(freeformGradient, bitmap, _easing[_index % _easing.Length]);
                _index++;

                if (_index == _easing.Length)
                {
                    _index = 0;
                    _freeformTimer.Stop();
                }
            }
            else
            {
                _freeformTimer.Stop();
            }
        }

        public void Next()
        {
            _freeformTimer.Stop();
            _easing = _freeform.Next();
            _index = 0;
            _freeformTimer.Start();
        }

        private Vector2[][] _easing;
        private int _index;

        public void UpdateSource(IClientService clientService, Background background, bool thumbnail)
        {
            UpdateManager.Unsubscribe(this, ref _fileToken, true);

            _background = background;

            if (background.Type is BackgroundTypeFill typeFill)
            {
                _negative = false;
                _intensity = 255;
                _backgroundFill = typeFill.Fill;

                _pattern = null;
                _patternPath = null;
                _wallpaperPath = null;

                _backgroundId = 0;
                _thumbnail = false;
                _vector = false;

                Background = typeFill.ToBrush(_freeform.Phase);
                Foreground = null;
                BorderBrush = null;

                UpdateBlurred(false);
            }
            else if (background.Type is BackgroundTypePattern typePattern)
            {
                _negative = typePattern.IsInverted;
                _intensity = (byte)(255 * (typePattern.Intensity / 100d));
                _backgroundFill = typePattern.Fill;

                _wallpaperPath = null;

                Background = typePattern.ToBrush(_freeform.Phase);
                Foreground = null;
                BorderBrush = null;

                UpdateBlurred(false);

                var file = background.Document.DocumentValue;
                if (thumbnail && background.Document.Thumbnail != null)
                {
                    file = background.Document.Thumbnail.File;
                }
                else
                {
                    thumbnail = false;
                }

                _backgroundId = file.Id;
                _thumbnail = thumbnail;
                _vector = thumbnail is false && background.Document.MimeType == "application/x-tgwallpattern";

                if (file.Local.IsDownloadingCompleted)
                {
                    UpdatePattern(file, WindowContext.Current.RasterizationScale);
                }
                else if (clientService != null)
                {
                    if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                    {
                        clientService.DownloadFile(file.Id, 16);
                    }

                    UpdateManager.Subscribe(background, clientService, file, ref _fileToken, UpdateFile, true);
                }
            }
            else if (background.Type is BackgroundTypeWallpaper typeWallpaper)
            {
                _negative = false;
                _intensity = 255;
                _backgroundFill = null;

                _pattern = null;
                _patternPath = null;

                Foreground = null;
                BorderBrush = null;

                UpdateBlurred(typeWallpaper.IsBlurred);

                var file = background.Document.DocumentValue;
                if (thumbnail && background.Document.Thumbnail != null)
                {
                    file = background.Document.Thumbnail.File;
                }
                else
                {
                    thumbnail = false;
                }

                _backgroundId = file.Id;
                _thumbnail = thumbnail;
                _vector = false;

                if (file.Local.IsDownloadingCompleted)
                {
                    UpdateWallpaper(file);
                }
                else if (clientService != null)
                {
                    if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                    {
                        clientService.DownloadFile(file.Id, 16);
                    }

                    UpdateManager.Subscribe(this, clientService, file, ref _fileToken, UpdateFile, true);
                }
            }
        }

        private void UpdateWallpaper(File file)
        {
            Foreground = null;
            BorderBrush = null;

            if (_wallpaperPath != file.Local.Path)
            {
                _wallpaperPath = file.Local.Path;

                if (Background is ImageBrush imageBrush)
                {
                    imageBrush.ImageSource = UriEx.ToBitmap(file.Local.Path, 0, 0);
                }
                else
                {
                    Background = new ImageBrush
                    {
                        ImageSource = UriEx.ToBitmap(file.Local.Path, 0, 0),
                        Stretch = Stretch.UniformToFill,
                        AlignmentX = AlignmentX.Center,
                        AlignmentY = AlignmentY.Center
                    };
                }
            }
        }

        private async void UpdatePattern(File file, double scale)
        {
            if (_pattern == null || _patternPath != file.Local.Path || _rasterizationScale != scale)
            {
                if (_negative)
                {
                    BorderBrush = new SolidColorBrush(Colors.Black);
                }

                _patternPath = file.Local.Path;
                _rasterizationScale = scale;

                if (_vector)
                {
                    _pattern = await PlaceholderHelper.LoadPatternBitmapAsync(file, scale);
                }
                else
                {
                    _pattern = await PlaceholderHelper.LoadBitmapAsync(file);
                }

                void handler(LoadedImageSurface s, LoadedImageSourceLoadCompletedEventArgs args)
                {
                    s.LoadCompleted -= handler;

                    if (_backgroundId == file.Id && !IsDisconnected)
                    {
                        BorderBrush = null;
                        UpdatePattern();
                    }
                    else
                    {
                        s.Dispose();
                    }
                }

                if (_pattern != null)
                {
                    _pattern.LoadCompleted += handler;
                }
                else if (_backgroundId == file.Id)
                {
                    BorderBrush = null;
                }
            }
            else
            {
                UpdatePattern();
            }
        }

        private void UpdatePattern()
        {
            if (Foreground is TiledBrush tiledBrush)
            {
                tiledBrush.ImageSource = _pattern;
                tiledBrush.Intensity = _intensity;
                tiledBrush.IsNegative = _negative;

                tiledBrush.Update();
            }
            else
            {
                Foreground = new TiledBrush
                {
                    ImageSource = _pattern,
                    Intensity = _intensity,
                    IsNegative = _negative,
                };
            }
        }

        private SpriteVisual _blurVisual;
        private CompositionEffectBrush _blurBrush;

        private void UpdateBlurred(bool enabled, float amount = 12)
        {
            if (_blurVisual == null && enabled)
            {
                var graphicsEffect = new GaussianBlurEffect
                {
                    Name = "Blur",
                    BlurAmount = amount,
                    BorderMode = EffectBorderMode.Hard,
                    Source = new CompositionEffectSourceParameter("Backdrop")
                };

                var compositor = Window.Current.Compositor;
                var effectFactory = compositor.CreateEffectFactory(graphicsEffect, new[] { "Blur.BlurAmount" });
                var effectBrush = effectFactory.CreateBrush();
                var backdrop = compositor.CreateBackdropBrush();
                effectBrush.SetSourceParameter("Backdrop", backdrop);

                _blurBrush = effectBrush;
                _blurVisual = compositor.CreateSpriteVisual();
                _blurVisual.RelativeSizeAdjustment = Vector2.One;
                _blurVisual.Brush = _blurBrush;

                ElementCompositionPreview.SetElementChildVisual(this, _blurVisual);
            }
            else if (_blurVisual != null && !enabled)
            {
                ElementCompositionPreview.SetElementChildVisual(this, null);

                _blurBrush?.Dispose();
                _blurBrush = null;

                _blurVisual?.Dispose();
                _blurVisual = null;
            }
        }

        private void UpdateFile(object target, File file)
        {
            if (file.Id == _backgroundId)
            {
                this.BeginOnUIThread(() => UpdateSource(null, _background, _thumbnail));
            }
        }
    }
}
