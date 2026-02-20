//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Effects;
using System;
using System.Collections.Generic;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Native;
using Telegram.Native.Controls;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Chats
{
    public partial class ChatBackgroundPresenter : ControlEx
    {
        private IClientService _clientService;

        private ChatBackgroundPattern _pattern;
        private string _patternPath;
        private Sticker _symbol;
        private Sticker _model;
        private string _wallpaperPath;

        private Background _background;
        private bool _vector = false;
        private bool _negative = false;
        private float _intensity = 1;

        private int _backgroundId;
        private BackgroundFill _backgroundFill;

        private ChatTheme _theme;

        private bool _thumbnail;
        private long _fileToken;

        private double _rasterizationScale;

        private AnimatedImage Symbol;
        private AnimatedImage Model;

        public ChatBackgroundPresenter()
        {
            DefaultStyleKey = typeof(ChatBackgroundPresenter);
        }

        protected override void OnApplyTemplate()
        {
            Symbol = GetTemplateChild(nameof(Symbol)) as AnimatedImage;
            Model = GetTemplateChild(nameof(Model)) as AnimatedImage;

            Symbol.Source = DelayedFileSource.FromSticker(_clientService, _symbol);
            Model.Source = DelayedFileSource.FromSticker(_clientService, _model);
        }

        protected override void OnLoaded()
        {
            XamlRoot.Changed += OnRasterizationScaleChanged;
        }

        protected override void OnUnloaded()
        {
            XamlRoot.Changed -= OnRasterizationScaleChanged;

            UpdateManager.Unsubscribe(this, ref _fileToken);
        }

        private void OnRasterizationScaleChanged(XamlRoot sender, XamlRootChangedEventArgs args)
        {
            var value = sender.RasterizationScale;
            if (value != _rasterizationScale && _vector && _background?.Type is BackgroundTypePattern pattern && _background?.Document?.DocumentValue != null)
            {
                UpdatePattern(pattern, _background.Document.DocumentValue, value, _symbol, _model);
            }
            else
            {
                _rasterizationScale = value;
            }
        }

        private ChatBackgroundBrush _tiledBrush;

        public void Next()
        {
            if (_tiledBrush is ChatBackgroundBrush tiledBrush)
            {
                tiledBrush.Next();
            }
        }

        public void UpdateSource(IClientService clientService, Background background, bool thumbnail, ChatTheme theme = null)
        {
            UpdateManager.Unsubscribe(this, ref _fileToken);

            var clear = _background == null;

            _clientService = clientService;
            _background = background;
            _theme = theme;

            if (background.Type is BackgroundTypeFill typeFill)
            {
                _negative = false;
                _intensity = 1;
                _backgroundFill = typeFill.Fill;

                _pattern = null;
                _patternPath = null;
                _wallpaperPath = null;

                _model = null;
                _symbol = null;
                UpdateModel();

                _backgroundId = 0;
                _thumbnail = false;
                _vector = false;

                Background = null; //typeFill.ToBrush(0);

                UpdateBlurred(false);
                UpdateTiledBrush(true);
            }
            else if (background.Type is BackgroundTypePattern typePattern)
            {
                _negative = typePattern.IsInverted;
                _intensity = typePattern.Intensity / 100f;
                _backgroundFill = typePattern.Fill;

                _wallpaperPath = null;

                //if (clear)
                {
                    Background = _negative ? new SolidColorBrush(Colors.Black) : null; // typePattern.ToBrush(0);
                }

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

                if (theme is ChatThemeGift gift)
                {
                    UpdatePattern(typePattern, file, WindowContext.Current.RasterizationScale, gift.GiftTheme.Gift.Symbol.Sticker, gift.GiftTheme.Gift.Model.Sticker);
                }
                else
                {
                    UpdatePattern(typePattern, file, WindowContext.Current.RasterizationScale, null, null);
                }

                if (clientService != null && !file.Local.IsDownloadingCompleted)
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
                _intensity = 1;
                _backgroundFill = null;

                _pattern = null;
                _patternPath = null;

                _model = null;
                _symbol = null;
                UpdateModel();

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
            else if (background.Type is BackgroundTypeChatTheme typeChatTheme)
            {
                if (clientService.TryGetEmojiChatTheme(typeChatTheme.ThemeName, out EmojiChatTheme emoji))
                {
                    // TODO: support light/dark changed
                    background = ActualTheme == ElementTheme.Light
                        ? emoji.LightSettings.Background
                        : emoji.DarkSettings.Background;

                    UpdateSource(clientService, background, thumbnail, null);
                    return;
                }
            }
        }

        private void UpdateWallpaper(File file)
        {
            if (_tiledBrush != null)
            {
                _tiledBrush.OnDisconnected();
                _tiledBrush = null;

                ElementCompositionPreview.SetElementChildVisual(this, null);
            }

            if (_wallpaperPath != file.Local.Path || Background == null)
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

        private async void UpdatePattern(BackgroundTypePattern pattern, File file, double scale, Sticker symbol, Sticker model)
        {
            if (_tiledBrush == null)
            {
                CreateTiledBrush();
            }

            if (_pattern != null && _patternPath == file.Local.Path && _rasterizationScale == scale && _symbol?.Id == symbol?.Id && _model?.Id == model?.Id)
            {
                UpdateTiledBrush(true);
                return;
            }

            UpdateTiledBrush(false);

            if (file.Local.IsDownloadingCompleted)
            {
                _patternPath = file.Local.Path;
                _rasterizationScale = scale;

                if (_vector)
                {
                    _pattern = await PlaceholderHelper.LoadPatternBitmapAsync(file, _intensity, _negative, scale);
                    _symbol = symbol;
                    _model = model;
                }
                else
                {
                    _pattern = await PlaceholderHelper.LoadBitmapAsync(file);
                    _symbol = null;
                    _model = null;
                }

                void handler(LoadedImageSurface s, LoadedImageSourceLoadCompletedEventArgs args)
                {
                    s.LoadCompleted -= handler;

                    if (_backgroundId == file.Id && !IsDisconnected)
                    {
                        UpdateTiledBrush(true);
                    }
                    // TODO: Dispose here shouldn't be needed
                    //else
                    //{
                    //    s.Dispose();
                    //}
                }

                if (_backgroundId != file.Id || IsDisconnected)
                {
                    return;
                }

                if (_pattern != null)
                {
                    if (_pattern.Surface is LoadedImageSurface surface)
                    {
                        surface.LoadCompleted += handler;
                    }
                    else
                    {
                        UpdateTiledBrush(true);
                    }
                }
            }
        }

        private bool _collapsed = true;

        private void UpdateTiledBrush(bool show)
        {
            if (Symbol != null)
            {
                Symbol.Source = DelayedFileSource.FromSticker(_clientService, _symbol);
                Model.Source = DelayedFileSource.FromSticker(_clientService, _model);
            }

            if (show)
            {
                if (_tiledBrush is ChatBackgroundBrush tiledBrush)
                {
                    tiledBrush.Pattern = _pattern;
                    tiledBrush.Fill = _backgroundFill;
                    tiledBrush.Symbol = Symbol;
                    tiledBrush.Model = UpdateModel();
                    tiledBrush.Intensity = _intensity;
                    tiledBrush.IsNegative = _negative;

                    tiledBrush.Update();
                }
                else
                {
                    CreateTiledBrush();
                }
            }

            if (_collapsed != show || _tiledBrush == null)
            {
                return;
            }

            _collapsed = !show;
            _tiledBrush.CrossFade(show);
        }

        private void CreateTiledBrush()
        {
            _tiledBrush = new ChatBackgroundBrush
            {
                Pattern = _pattern,
                Fill = _backgroundFill,
                Symbol = Symbol,
                Model = UpdateModel(),
                Intensity = _intensity,
                IsNegative = _negative,
            };

            ElementCompositionPreview.SetElementChildVisual(this, _tiledBrush.Visual);
        }

        private ContainerVisual _modelVisual;

        private ChatBackgroundSymbol UpdateModel()
        {
            if (_pattern == null || _model == null)
            {
                _modelVisual?.Children.RemoveAll();
                return default;
            }

            if (_modelVisual == null)
            {
                _modelVisual = BootStrapper.Current.Compositor.CreateContainerVisual();
                _modelVisual.RelativeSizeAdjustment = Vector2.One;

                ElementCompositionPreview.SetElementChildVisual(this, _modelVisual);
            }
            else
            {
                _modelVisual.Children.RemoveAll();
            }

            _modelVisual.Opacity = _intensity;

            var width = (int)Math.Ceiling(ActualWidth / _pattern.RenderSize.X);
            var height = (int)Math.Ceiling(ActualHeight / _pattern.RenderSize.Y);

            var logical = _pattern.RenderSize;
            var physical = _pattern.RenderPhysicalSize;
            var factor = logical / physical;

            var topBound = 48 * 3;
            var bottomBound = ActualSize.Y - 48 * 2;
            var rightBound = ActualSize.X;

            var available = new List<ChatBackgroundSymbol>(_pattern.Symbols.Count * (height * width + width));

            for (int y = 0; y < height; y++)
            {
                var offsetY = logical.Y * y;

                for (int x = 0; x < width; x++)
                {
                    var offsetX = logical.X * x;

                    for (int i = 0; i < _pattern.Symbols.Count; i++)
                    {
                        var temp = _pattern.Symbols[i];

                        var size = temp.Size * factor;
                        var offset = new Vector2(offsetX + temp.Offset.X * factor.X, offsetY + temp.Offset.Y * factor.Y);

                        if (offset.Y < topBound || offset.Y + size.Y > bottomBound || offset.X + size.X > rightBound)
                        {
                            continue;
                        }

                        available.Add(new ChatBackgroundSymbol
                        {
                            Size = size,
                            Offset = offset,
                            RotationAngle = temp.RotationAngle
                        });
                    }
                }
            }

            var index = new Random().Next(0, available.Count);
            var pattern = available[index];

            var compositor = BootStrapper.Current.Compositor;
            var visual = ElementComposition.GetElementVisual(Model);

            var sprite = compositor.CreateRedirectVisual(visual);
            sprite.Size = pattern.Size;
            sprite.Offset = new Vector3(pattern.Offset, 0);
            sprite.RotationAngle = pattern.RotationAngle;

            Model.Width = sprite.Size.X;
            Model.Height = sprite.Size.Y;
            Model.FrameSize = sprite.Size.ToSize();
            Model.LoopCount = 1;
            Model.Play();

            _modelVisual.Children.InsertAtTop(sprite);

            return pattern;
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

                var compositor = BootStrapper.Current.Compositor;
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

                _blurBrush = null;
                _blurVisual = null;
            }
        }

        private void UpdateFile(object target, File file)
        {
            if (file.Id == _backgroundId)
            {
                this.BeginOnUIThread(() =>
                {
                    if (IsConnected)
                    {
                        UpdateSource(null, _background, _thumbnail, _theme);
                    }
                });
            }
        }
    }
}
