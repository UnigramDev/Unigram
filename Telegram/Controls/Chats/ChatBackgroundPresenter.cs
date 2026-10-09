//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Effects;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private float _patternHeight;
        private bool _patternLoading;
        private int _patternRequest;

        private float _arrangedHeight;

        // UpdateSource was called before the template was applied, or while unloaded, and the
        // source is applied once both hold.
        private bool _pending;
        private bool _templateApplied;

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

            _templateApplied = true;

            if (_pending && IsDisconnected is false)
            {
                UpdateSource(_clientService, _background, _thumbnail, _theme);
            }
        }

        protected override void OnLoaded()
        {
            if (_pending && _templateApplied)
            {
                UpdateSource(_clientService, _background, _thumbnail, _theme);
            }
        }

        protected override void OnUnloaded()
        {
            UpdateManager.Unsubscribe(this, ref _fileToken);

            // Drops a pattern still drawing, and makes the reload draw it again.
            _patternRequest++;
            _patternLoading = false;
            _pattern = null;
            _patternPath = null;
            _wallpaperPath = null;
            _symbol = null;
            _model = null;

            if (_tiledBrush != null)
            {
                _tiledBrush.OnDisconnected();
                _tiledBrush = null;

                _root.Children.Remove(_tiledVisual);
                _tiledVisual = null;
            }

            _modelVisual?.Children.RemoveAll();
            UpdateBlurred(false);

            if (Symbol != null)
            {
                Symbol.Source = null;
                Model.Source = null;
            }

            Background = null;

            _pending = _background != null;
        }

        // Tall chats need the pattern drawn taller. Heights are rounded up to a step, so a resize
        // redraws it only when crossing one, and composition shrinks it by at most one step.
        private const float PatternHeightStep = 128;

        // The default pattern's height (1440x2960 at a quarter). Others come shorter, some far
        // shorter, and are grown to this so every pattern keeps about the same density.
        private const float PatternMinimumHeight = 740;

        private float GetPatternHeight()
        {
            if (_arrangedHeight <= 0)
            {
                return 0;
            }

            return Math.Max(PatternMinimumHeight, MathF.Ceiling(_arrangedHeight / PatternHeightStep) * PatternHeightStep);
        }

        // Takes the actual height, not the step. Growing past the render always redraws it, but
        // shrinking keeps it for a quarter step more, so a height resting on a step boundary does
        // not flip between the two renders.
        private bool IsSameResolution(float height)
        {
            height = Math.Max(height, PatternMinimumHeight);

            // Up to its own height, or the minimum, a pattern is drawn at that size whatever the
            // chat's height, so no height there needs a redraw.
            var natural = Math.Max(_pattern?.RenderSize.Y ?? 0, PatternMinimumHeight);
            if (height <= natural && _patternHeight <= natural)
            {
                return true;
            }

            return height <= _patternHeight && height > _patternHeight - PatternHeightStep * 1.25f;
        }

        private float GetZoom(ChatBackgroundPattern pattern)
        {
            var height = pattern?.RenderSize.Y ?? 0;
            var minimumHeight = _thumbnail ? 0 : PatternMinimumHeight;
            return height > 0 ? Math.Max(1, Math.Max(_arrangedHeight, minimumHeight) / height) : 1;
        }

        // The one place that notices both a new height and a new scale: a scale change invalidates
        // the whole layout tree, so it arrives here too. ActualHeight is not updated until after
        // this pass, hence _arrangedHeight.
        protected override Windows.Foundation.Size ArrangeOverride(Windows.Foundation.Size finalSize)
        {
            var size = base.ArrangeOverride(finalSize);
            _arrangedHeight = (float)finalSize.Height;

            if (_tiledBrush != null)
            {
                _tiledBrush.Zoom = GetZoom(_tiledBrush.Pattern);
                _tiledBrush.UpdateZoom();
            }

            // Layout runs before Loaded, and OnLoaded draws a pending source itself.
            if (_vector && _pending is false && _background?.Type is BackgroundTypePattern pattern && _background.Document?.DocumentValue is File file && file.Local.IsDownloadingCompleted)
            {
                var scale = XamlRoot.RasterizationScale;
                if (_patternPath != file.Local.Path || _rasterizationScale != scale || !IsSameResolution(_arrangedHeight))
                {
                    UpdatePattern(pattern, file, scale);
                }
            }

            return size;
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

            // UpdateFile passes no client service, and the reload needs one to resume a download.
            _clientService = clientService ?? _clientService;
            _background = background;
            _theme = theme;

            if (!_templateApplied || IsDisconnected)
            {
                _thumbnail = thumbnail;
                _pending = true;
                return;
            }

            _pending = false;

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

                if (ApiInfo.IsPackagedRelease)
                {
                    Debug.Assert(XamlRoot != null);
                }

                UpdatePattern(typePattern, file, WindowContext.Current.RasterizationScale);

                if (clientService != null && !file.Local.IsDownloadingCompleted)
                {
                    if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                    {
                        clientService.DownloadFile(file.Id, 16);
                    }

                    UpdateManager.Subscribe(this, clientService, file, ref _fileToken, UpdateFile, true);
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

        // For a colour picker, which changes the fill many times a second: recolours what is on
        // screen with no transition and no pattern reload. Fails, changing nothing that
        // UpdateSource would not set again, when anything but the fill or intensity differs.
        public bool TryUpdateFill(Background background)
        {
            if (_tiledBrush == null || _background == null || _patternLoading)
            {
                return false;
            }

            BackgroundFill fill;
            float intensity;
            bool negative;

            if (background.Type is BackgroundTypeFill typeFill && _background.Type is BackgroundTypeFill)
            {
                fill = typeFill.Fill;
                intensity = 1;
                negative = false;
            }
            else if (background.Type is BackgroundTypePattern typePattern && _background.Type is BackgroundTypePattern && _pattern != null && background.Document?.DocumentValue.Id == _background.Document?.DocumentValue.Id)
            {
                fill = typePattern.Fill;
                intensity = typePattern.Intensity / 100f;
                negative = typePattern.IsInverted;
            }
            else
            {
                return false;
            }

            if (negative != _negative)
            {
                return false;
            }

            _tiledBrush.Fill = fill;
            _tiledBrush.Intensity = intensity;

            if (_tiledBrush.TryUpdateFill() is false)
            {
                return false;
            }

            _background = background;
            _backgroundFill = fill;
            _intensity = intensity;

            if (_modelVisual != null)
            {
                _modelVisual.Opacity = intensity;
            }

            return true;
        }

        private void UpdateWallpaper(File file)
        {
            if (_tiledBrush != null)
            {
                _tiledBrush.OnDisconnected();
                _tiledBrush = null;

                _root.Children.Remove(_tiledVisual);
                _tiledVisual = null;
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

        private void UpdatePattern(BackgroundTypePattern pattern, File file, double scale)
        {
            if (_theme is ChatThemeGift gift)
            {
                UpdatePattern(pattern, file, scale, gift.GiftTheme.Gift.Symbol.Sticker, gift.GiftTheme.Gift.Model.Sticker);
            }
            else
            {
                UpdatePattern(pattern, file, scale, null, null);
            }
        }

        // _patternPath, _rasterizationScale, _patternHeight, _symbol and _model describe the latest
        // render requested, which may still be drawing while _pattern holds the previous one.
        private async void UpdatePattern(BackgroundTypePattern pattern, File file, double scale, Sticker symbol, Sticker model)
        {
            if (_tiledBrush == null)
            {
                CreateTiledBrush();
            }

            if (_vector is false)
            {
                symbol = null;
                model = null;
            }

            var height = _vector ? GetPatternHeight() : 0;
            var redraw = _patternPath == file.Local.Path && _symbol?.Id == symbol?.Id && _model?.Id == model?.Id;

            if (redraw && _rasterizationScale == scale && IsSameResolution(_vector ? _arrangedHeight : 0))
            {
                if (_patternLoading is false && _pattern != null)
                {
                    UpdateTiledBrush(true);
                    return;
                }
                else if (_patternLoading)
                {
                    return;
                }
            }

            // Only the resolution changed: the pattern stays on screen until the sharper one replaces it.
            if (redraw is false || _pattern == null)
            {
                UpdateTiledBrush(false);
            }

            // Drawn once the control has a height to cover; OnSizeChanged comes back for it.
            if (file.Local.IsDownloadingCompleted && (height > 0 || _vector is false))
            {
                var request = ++_patternRequest;

                _patternPath = file.Local.Path;
                _rasterizationScale = scale;
                _patternHeight = height;
                _patternLoading = true;
                _symbol = symbol;
                _model = model;

                var loaded = _vector
                    ? await Direct2D.LoadPatternBitmapAsync(file, _intensity, _negative, scale, height)
                    : await Direct2D.LoadBitmapAsync(file);

                // A newer request owns _patternLoading, and will show its own result.
                if (request != _patternRequest)
                {
                    return;
                }

                _patternLoading = false;

                if (_backgroundId != file.Id || IsDisconnected)
                {
                    // Otherwise returning to this file would take _pattern, a different one, as its render.
                    _patternPath = null;
                    return;
                }

                _pattern = loaded;

                void handler(LoadedImageSurface s, LoadedImageSourceLoadCompletedEventArgs args)
                {
                    s.LoadCompleted -= handler;

                    if (_backgroundId == file.Id && request == _patternRequest && !IsDisconnected)
                    {
                        UpdateTiledBrush(true);
                    }
                    // TODO: Dispose here shouldn't be needed
                    //else
                    //{
                    //    s.Dispose();
                    //}
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
                    tiledBrush.Zoom = GetZoom(_pattern);

                    tiledBrush.Update();
                }
                else
                {
                    CreateTiledBrush();
                }
            }
            else if (_tiledBrush is ChatBackgroundBrush tiledBrush)
            {
                // The new pattern may still be downloading, but its fill is already known.
                tiledBrush.Pattern = null;
                tiledBrush.Fill = _backgroundFill;
                tiledBrush.IsNegative = _negative;
                tiledBrush.Update();
            }
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
                Zoom = GetZoom(_pattern),
            };

            _tiledVisual = _tiledBrush.Visual;
            Root.Children.InsertAtBottom(_tiledVisual);
        }

        private ContainerVisual _root;
        private Visual _tiledVisual;

        // An element hosts a single child visual, so the brush, the gift model and the blur share
        // this one rather than each setting their own, which detached whichever was there before.
        private ContainerVisual Root
        {
            get
            {
                if (_root == null)
                {
                    _root = BootStrapper.Current.Compositor.CreateContainerVisual();
                    _root.RelativeSizeAdjustment = Vector2.One;

                    ElementCompositionPreview.SetElementChildVisual(this, _root);
                }

                return _root;
            }
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

                Root.Children.InsertAtTop(_modelVisual);
            }
            else
            {
                _modelVisual.Children.RemoveAll();
            }

            _modelVisual.Opacity = _intensity;

            var logical = _pattern.RenderSize * GetZoom(_pattern);
            var physical = _pattern.RenderPhysicalSize;
            var factor = logical / physical;

            if (logical.X <= 0)
            {
                return default;
            }

            // The same single row ChatBackgroundBrush draws, starting at the left edge.
            var columns = (int)MathF.Ceiling(ActualSize.X / logical.X);

            var topBound = 48 * 3;
            var bottomBound = ActualSize.Y - 48 * 2;
            var rightBound = ActualSize.X;

            var available = new List<ChatBackgroundSymbol>(_pattern.Symbols.Count * columns);

            for (int x = 0; x < columns; x++)
            {
                var offsetX = logical.X * x;

                for (int i = 0; i < _pattern.Symbols.Count; i++)
                {
                    var temp = _pattern.Symbols[i];

                    var size = temp.Size * factor;
                    var offset = new Vector2(offsetX + temp.Offset.X * factor.X, temp.Offset.Y * factor.Y);

                    if (offset.Y < topBound || offset.Y + size.Y > bottomBound || offset.X < 0 || offset.X + size.X > rightBound)
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

            if (available.Count == 0)
            {
                return default;
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

                Root.Children.InsertAtTop(_blurVisual);
            }
            else if (_blurVisual != null && !enabled)
            {
                Root.Children.Remove(_blurVisual);

                _blurBrush = null;
                _blurVisual = null;
            }
        }

        private void UpdateFile(File file)
        {
            if (file.Id == _backgroundId && IsConnected)
            {
                UpdateSource(null, _background, _thumbnail, _theme);
            }
        }
    }
}
