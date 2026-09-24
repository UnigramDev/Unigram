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
using Telegram.Native;
using Telegram.Navigation;
using Telegram.Td.Api;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Controls.Media
{
    public partial class ChatBackgroundBrush
    {
        public ChatBackgroundPattern Pattern { get; set; }

        public BackgroundFill Fill { get; set; }

        public AnimatedImage Symbol { get; set; }

        public ChatBackgroundSymbol Model { get; set; }

        public bool IsNegative { get; set; }

        public float Intensity { get; set; } = 1;

        public float Zoom { get; set; } = 1;

        private FreeformGradientSurface _freeform;
        private CompositionEffectBrush _effect;
        private CompositionBrush _brush;
        private SpriteVisual _visual;

        public SpriteVisual Visual
        {
            get
            {
                if (_visual == null)
                {
                    _visual = BootStrapper.Current.Compositor.CreateSpriteVisual();
                    _visual.RelativeSizeAdjustment = Vector2.One;
                }

                OnConnected();

                return _visual;
            }
        }

        public void OnConnected()
        {
            try
            {
                CreateResources();
            }
            catch (Exception ex)
            {
                // Logged, not just swallowed: a device loss and an outright bug in the brush graph
                // are indistinguishable from here, and the second kind leaves the chat looking fine
                // bar a missing layer.
                Logger.Error(ex.ToString());

                OnDisconnected();
                CreateResources();
            }
        }

        private CompositionSurfaceBrush _tileBrush;
        private ContainerVisual _tileVisual;
        private SpriteVisual _tileContent;
        private CompositionVisualSurface _tileSurface;

        // Many patterns only tile horizontally, so a tile is never shorter than the area: it grows
        // to cover the height, and the one row repeats sideways.
        public void UpdateZoom()
        {
            if (Pattern == null)
            {
                return;
            }

            var logical = Pattern.RenderSize;
            var physical = Pattern.RenderPhysicalSize;

            if (physical.X <= 0 || physical.Y <= 0)
            {
                return;
            }

            if (_tileContent != null)
            {
                var size = logical * Zoom;

                _tileVisual.Size = size;
                _tileContent.Scale = new Vector3(Zoom, Zoom, 1);
                _tileSurface.SourceSize = size;
            }
            else if (_tileBrush != null)
            {
                _tileBrush.Scale = logical * Zoom / physical;
            }
        }

        private CompositionSurfaceBrush CreateSurfaceBrush(out CompositionSurfaceBrush modelBrush)
        {
            var surface = Pattern.Surface;
            var logical = Pattern.RenderSize;
            var physical = Pattern.RenderPhysicalSize;

            var surfaceBrush = BootStrapper.Current.Compositor.CreateSurfaceBrush(surface);
            surfaceBrush.Stretch = CompositionStretch.None;
            surfaceBrush.SnapToPixels = true;
            surfaceBrush.Scale = logical / physical;
            surfaceBrush.HorizontalAlignmentRatio = 0;
            surfaceBrush.VerticalAlignmentRatio = 0;

            if (Pattern.Symbols.Count > 0)
            {
                var compositor = BootStrapper.Current.Compositor;
                var factor = logical / physical;

                // UpdateZoom scales the content, never the root the visual surface captures, so the
                // capture does not depend on whether a source visual's own transform is applied.
                var tile = compositor.CreateContainerVisual();

                var visual = BootStrapper.Current.Compositor.CreateSpriteVisual();
                visual.Size = logical;
                visual.Brush = surfaceBrush;

                tile.Children.InsertAtTop(visual);

                var symbolSurfaceBrush = compositor.CreateSurfaceBrush();
                var symbolSurface = compositor.CreateVisualSurface();

                var symbolVisual = ElementComposition.GetElementVisual(Symbol);

                symbolSurface.SourceVisual = symbolVisual;
                symbolSurface.SourceOffset = new Vector2(0, 0);
                symbolSurfaceBrush.HorizontalAlignmentRatio = 0.5f;
                symbolSurfaceBrush.VerticalAlignmentRatio = 0.5f;
                symbolSurfaceBrush.Surface = symbolSurface;
                symbolSurfaceBrush.Stretch = CompositionStretch.Fill;
                symbolSurfaceBrush.BitmapInterpolationMode = CompositionBitmapInterpolationMode.NearestNeighbor;
                symbolSurfaceBrush.SnapToPixels = true;

                var maxWidth = 0f;

                for (int i = 0; i < Pattern.Symbols.Count; i++)
                {
                    var pattern = Pattern.Symbols[i];
                    var sprite = visual.Compositor.CreateSpriteVisual();
                    sprite.Size = pattern.Size * factor;
                    sprite.Offset = new Vector3(pattern.Offset * factor, 0);
                    sprite.RotationAngle = pattern.RotationAngle;
                    sprite.Brush = symbolSurfaceBrush;

                    visual.Children.InsertAtTop(sprite);

                    maxWidth = Math.Max(maxWidth, sprite.Size.X);
                }

                // Drawn at the tile's zoom, so the symbols stay as sharp as the pattern around them.
                var symbolSize = maxWidth * Zoom;

                symbolSurface.SourceSize = new Vector2(symbolSize, symbolSize);
                Symbol.Width = symbolSize;
                Symbol.Height = symbolSize;
                Symbol.FrameSize = new Windows.Foundation.Size(symbolSize, symbolSize);

                var visualSurfaceBrush = compositor.CreateSurfaceBrush();
                var visualSurface = compositor.CreateVisualSurface();

                visualSurface.SourceVisual = tile;
                visualSurface.SourceOffset = new Vector2(0, 0);
                visualSurfaceBrush.HorizontalAlignmentRatio = 0;
                visualSurfaceBrush.VerticalAlignmentRatio = 0;
                visualSurfaceBrush.Surface = visualSurface;
                visualSurfaceBrush.Stretch = CompositionStretch.None;
                visualSurfaceBrush.BitmapInterpolationMode = CompositionBitmapInterpolationMode.NearestNeighbor;
                visualSurfaceBrush.SnapToPixels = true;

                _tileBrush = null;
                _tileVisual = tile;
                _tileContent = visual;
                _tileSurface = visualSurface;
                UpdateZoom();

                modelBrush = CreateModelBrush();
                return visualSurfaceBrush;
            }

            _tileBrush = surfaceBrush;
            _tileVisual = null;
            _tileContent = null;
            _tileSurface = null;
            UpdateZoom();

            modelBrush = null;
            return surfaceBrush;
        }

        private CompositionSurfaceBrush CreateModelBrush()
        {
            var cos = MathF.Abs(MathF.Cos(Model.RotationAngle));
            var sin = MathF.Abs(MathF.Sin(Model.RotationAngle));

            var boundingWidth = Model.Size.X * cos + Model.Size.Y * sin;
            var boundingHeight = Model.Size.X * sin + Model.Size.Y * cos;

            var visual = BootStrapper.Current.Compositor.CreateContainerVisual();
            visual.Size = new Vector2(Model.Offset.X + boundingWidth, Model.Offset.Y + boundingHeight);

            var sprite = BootStrapper.Current.Compositor.CreateSpriteVisual();
            sprite.Brush = BootStrapper.Current.Compositor.CreateColorBrush(Colors.Black);
            sprite.Size = Model.Size;

            visual.Children.InsertAtTop(sprite);

            var visualSurfaceBrush = BootStrapper.Current.Compositor.CreateSurfaceBrush();
            var visualSurface = BootStrapper.Current.Compositor.CreateVisualSurface();

            visualSurface.SourceVisual = sprite;
            visualSurface.SourceOffset = new Vector2(0, 0);
            visualSurface.SourceSize = sprite.Size;
            visualSurfaceBrush.Offset = Model.Offset;
            visualSurfaceBrush.RotationAngle = Model.RotationAngle;
            visualSurfaceBrush.HorizontalAlignmentRatio = 0;
            visualSurfaceBrush.VerticalAlignmentRatio = 0;
            visualSurfaceBrush.Surface = visualSurface;
            visualSurfaceBrush.Stretch = CompositionStretch.None;
            visualSurfaceBrush.BitmapInterpolationMode = CompositionBitmapInterpolationMode.NearestNeighbor;
            visualSurfaceBrush.SnapToPixels = true;

            return visualSurfaceBrush;
        }

        private CompositionEffectFactory _negativeFactory;
        private CompositionEffectFactory _positiveFactory;

        private CompositionEffectBrush CreateNegativeEffectBrush()
        {
            if (_negativeFactory == null)
            {
                var borderEffect = new BorderEffect()
                {
                    Source = new CompositionEffectSourceParameter("Source"),
                    ExtendX = Microsoft.Graphics.Canvas.CanvasEdgeBehavior.Wrap,
                    ExtendY = Microsoft.Graphics.Canvas.CanvasEdgeBehavior.Wrap
                };

                var opacityEffect = new OpacityEffect
                {
                    Name = "Intensity",
                    Source = borderEffect,
                    Opacity = Intensity
                };

                var alphaMaskEffect = new AlphaMaskEffect
                {
                    AlphaMask = opacityEffect,
                    Source = new CompositionEffectSourceParameter("Backdrop"),
                };

                _negativeFactory = BootStrapper.Current.Compositor.CreateEffectFactory(alphaMaskEffect, new[] { "Intensity.Opacity" });
            }

            return _negativeFactory.CreateBrush();
        }

        private CompositionEffectBrush CreatePositiveEffectBrush()
        {
            if (_positiveFactory == null)
            {
                var borderEffect = new BorderEffect()
                {
                    Source = new CompositionEffectSourceParameter("Source"),
                    ExtendX = Microsoft.Graphics.Canvas.CanvasEdgeBehavior.Wrap,
                    ExtendY = Microsoft.Graphics.Canvas.CanvasEdgeBehavior.Wrap
                };

                var opacityEffect = new OpacityEffect
                {
                    Name = "Intensity",
                    Source = borderEffect,
                    Opacity = Intensity
                };

                var blendEffect = new BlendEffect
                {
                    Background = opacityEffect,
                    Foreground = new CompositionEffectSourceParameter("Backdrop"),
                    Mode = BlendEffectMode.SoftLight
                };

                _positiveFactory = BootStrapper.Current.Compositor.CreateEffectFactory(blendEffect, new[] { "Intensity.Opacity" });
            }

            return _positiveFactory.CreateBrush();
        }

        private CompositionBrush CreateBackdropBrush()
        {
            if (IsNegative && Pattern == null)
            {
                _freeform?.Dispose();
                _freeform = null;

                return BootStrapper.Current.Compositor.CreateColorBrush(Colors.Black);
            }

            if (Fill is BackgroundFillFreeformGradient freeform)
            {
                // TDLib's own int[], handed straight across: Int32 is one of the few element types
                // an array can cross the ABI as, and it saves widening each entry to a WinRT Color
                // that the native side would only unpack again.
                var colors = freeform.Colors;

                if (_freeform != null)
                {
                    _freeform.Colors = colors;
                }
                else
                {
                    _freeform?.Dispose();
                    _freeform = Direct2D.Current.CreateFreeformGradient(colors);
                }

                return _freeform.Brush;
            }
            else if (Fill is BackgroundFillGradient gradient)
            {
                _freeform?.Dispose();
                _freeform = null;

                return TdBackground.GetGradient(BootStrapper.Current.Compositor, gradient.TopColor, gradient.BottomColor, gradient.RotationAngle);
            }
            else if (Fill is BackgroundFillSolid solid)
            {
                _freeform?.Dispose();
                _freeform = null;

                return BootStrapper.Current.Compositor.CreateColorBrush(solid.Color.ToColor());
            }

            return null;
        }

        private void CreateResources()
        {
            _connected = true;
            _negative = IsNegative;
            _pattern = Pattern != null;
            _fill = Fill;

            if (_recreate || (_effect == null && (Pattern != null || Fill != null)))
            {
                _recreate = false;

                try
                {
                    if (Pattern != null)
                    {
                        var surfaceBrush = CreateSurfaceBrush(out CompositionSurfaceBrush modelBrush);
                        var backdropBrush = CreateBackdropBrush();

                        var effect = IsNegative
                            ? CreateNegativeEffectBrush()
                            : CreatePositiveEffectBrush();

                        effect.SetSourceParameter("Source", surfaceBrush);
                        effect.SetSourceParameter("Backdrop", backdropBrush);

                        _brush = backdropBrush;
                        _effect = effect;
                        _visual.Brush = effect;
                    }
                    else
                    {
                        var brush = CreateBackdropBrush();

                        _effect = null;
                        _brush = brush;
                        _visual.Brush = brush;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.ToString());

                    _recreate = true;
                    _effect = null;
                }
            }
        }

        public void OnDisconnected()
        {
            _connected = false;

            while (_fading.Count > 0)
            {
                ReleaseFade(_fading.Dequeue());
            }

            _effect?.Dispose();
            _effect = null;

            _brush?.Dispose();
            _brush = null;

            _freeform?.Dispose();
            _freeform = null;

            if (Pattern != null)
            {
                //ImageSource.Dispose();
                //ImageSource = null;
            }
        }

        private bool _connected;
        private bool _negative;
        private bool _pattern;
        private BackgroundFill _fill;
        private bool _recreate;

        public void Update()
        {
            if (_connected && (_recreate || _effect != null || _brush != null) && (Pattern != null || Fill != null))
            {
                if (_negative != IsNegative || (_pattern && Pattern == null) || !(_fill == Fill || _fill.AreTheSame(Fill)))
                {
                    FadeOutCurrent();
                    _recreate = true;
                }

                if (_recreate || _negative != IsNegative || (_pattern != (Pattern != null)))
                {
                    var added = !_pattern && Pattern != null;

                    _recreate = true;
                    OnConnected();

                    if (added)
                    {
                        FadeInPattern();
                    }

                    return;
                }

                try
                {
                    if (_effect is CompositionEffectBrush effectBrush)
                    {
                        effectBrush.SetSourceParameter("Source", CreateSurfaceBrush(out CompositionSurfaceBrush modelBrush));
                        effectBrush.SetSourceParameter("Backdrop", CreateBackdropBrush());
                        effectBrush.Properties.InsertScalar("Intensity.Opacity", Intensity);

                        // TODO: support gifts
                        //if (modelBrush != null)
                        //{
                        //    effectBrush.SetSourceParameter("Model", modelBrush);
                        //}
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.ToString());

                    _recreate = true;
                    OnConnected();
                }
            }
        }

        private readonly Queue<FadingLayer> _fading = new();

        private readonly struct FadingLayer
        {
            public readonly SpriteVisual Visual;
            public readonly CompositionScopedBatch Batch;
            public readonly CompositionBrush Effect;
            public readonly CompositionBrush Brush;
            public readonly FreeformGradientSurface Freeform;

            public FadingLayer(SpriteVisual visual, CompositionScopedBatch batch, CompositionBrush effect, CompositionBrush brush, FreeformGradientSurface freeform)
            {
                Visual = visual;
                Batch = batch;
                Effect = effect;
                Brush = brush;
                Freeform = freeform;
            }
        }

        // Hands what is on screen to a sprite above the brush that fades out on its own, so the
        // caller can rebuild the brush at once. It takes the freeform surface with it, because the
        // rebuilt brush would otherwise recolour that same surface under the fade.
        private void FadeOutCurrent()
        {
            if (_visual?.Brush is not CompositionBrush current)
            {
                return;
            }

            var compositor = BootStrapper.Current.Compositor;

            var overlay = compositor.CreateSpriteVisual();
            overlay.RelativeSizeAdjustment = Vector2.One;
            overlay.Brush = current;

            // At the bottom, so a fade still running from an earlier change keeps covering this one.
            _visual.Children.InsertAtBottom(overlay);
            _visual.Brush = null;

            var animation = compositor.CreateScalarKeyFrameAnimation();
            animation.InsertKeyFrame(0, 1);
            animation.InsertKeyFrame(1, 0);

            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            overlay.StartAnimation("Opacity", animation);
            batch.End();
            batch.Completed += OnFadeCompleted;

            _fading.Enqueue(new FadingLayer(overlay, batch, _effect, _brush, _freeform));

            _effect = null;
            _brush = null;
            _freeform = null;
        }

        private void OnFadeCompleted(object sender, CompositionBatchCompletedEventArgs args)
        {
            // Every fade runs for the same duration, so they complete in the order they started.
            if (_fading.Count > 0)
            {
                ReleaseFade(_fading.Dequeue());
            }
        }

        private void ReleaseFade(FadingLayer layer)
        {
            layer.Batch.Completed -= OnFadeCompleted;

            _visual?.Children.Remove(layer.Visual);
            layer.Visual.Dispose();

            layer.Effect?.Dispose();
            layer.Brush?.Dispose();
            layer.Freeform?.Dispose();
        }

        public void Next()
        {
            _freeform?.Next();
        }

        private void FadeInPattern()
        {
            if (_effect is CompositionEffectBrush effectBrush)
            {
                var animation = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
                animation.InsertKeyFrame(0, 0);
                animation.InsertKeyFrame(1, Intensity);

                effectBrush.StartAnimation("Intensity.Opacity", animation);
            }
        }
    }
}
