//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Effects;
using System;
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
            catch
            {
                OnDisconnected();
                CreateResources();
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

                var visual = BootStrapper.Current.Compositor.CreateSpriteVisual();
                visual.Size = logical;
                visual.Brush = surfaceBrush;

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

                symbolSurface.SourceSize = new Vector2(maxWidth, maxWidth);
                Symbol.Width = maxWidth;
                Symbol.Height = maxWidth;
                Symbol.FrameSize = new Windows.Foundation.Size(maxWidth, maxWidth);

                var visualSurfaceBrush = compositor.CreateSurfaceBrush();
                var visualSurface = compositor.CreateVisualSurface();

                visualSurface.SourceVisual = visual;
                visualSurface.SourceOffset = new Vector2(0, 0);
                visualSurface.SourceSize = logical;
                visualSurfaceBrush.HorizontalAlignmentRatio = 0;
                visualSurfaceBrush.VerticalAlignmentRatio = 0;
                visualSurfaceBrush.Surface = visualSurface;
                visualSurfaceBrush.Stretch = CompositionStretch.None;
                visualSurfaceBrush.BitmapInterpolationMode = CompositionBitmapInterpolationMode.NearestNeighbor;
                visualSurfaceBrush.SnapToPixels = true;

                modelBrush = CreateModelBrush();
                return visualSurfaceBrush;
            }

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

                _negativeFactory = BootStrapper.Current.Compositor.CreateEffectFactory(alphaMaskEffect, ["Intensity.Opacity"]);
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

                _positiveFactory = BootStrapper.Current.Compositor.CreateEffectFactory(blendEffect, ["Intensity.Opacity"]);
            }

            return _positiveFactory.CreateBrush();
        }

        private CompositionBrush CreateBackdropBrush()
        {
            if (IsNegative && Pattern == null)
            {
                _freeform?.Stop();
                _freeform = null;

                return BootStrapper.Current.Compositor.CreateColorBrush(Colors.Black);
            }

            if (Fill is BackgroundFillFreeformGradient freeform)
            {
                if (_freeform != null)
                {
                    _freeform.Colors = freeform.GetColors();
                }
                else
                {
                    _freeform?.Stop();
                    _freeform = PlaceholderHelper.Foreground.CreateFreeformGradient(freeform.GetColors());
                }

                return _freeform.Brush;
            }
            else if (Fill is BackgroundFillGradient gradient)
            {
                _freeform?.Stop();
                _freeform = null;

                return TdBackground.GetGradient(BootStrapper.Current.Compositor, gradient.TopColor, gradient.BottomColor, gradient.RotationAngle);
            }
            else if (Fill is BackgroundFillSolid solid)
            {
                _freeform?.Stop();
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
                catch
                {
                    _recreate = true;
                    _effect = null;
                }
            }
        }

        public void OnDisconnected()
        {
            _connected = false;

            _effect?.Dispose();
            _effect = null;

            _brush?.Dispose();
            _brush = null;

            if (Pattern != null)
            {
                //ImageSource.Dispose();
                //ImageSource = null;
            }
        }

        private bool _connected;
        private bool _negative;
        private bool _pattern;
        private bool _recreate;

        public void Update()
        {
            if (_connected && (_recreate || _effect != null || _brush != null) && (Pattern != null || Fill != null))
            {
                if (_recreate || _negative != IsNegative || (_pattern != (Pattern != null)))
                {
                    _recreate = true;
                    OnConnected();
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
                    else if (_brush != null)
                    {
                        _brush = CreateBackdropBrush();
                        _visual.Brush = _brush;
                    }
                }
                catch
                {
                    _recreate = true;
                    OnConnected();
                }
            }
        }

        public void Next()
        {
            _freeform?.Next();
        }

        public void CrossFade(bool show)
        {
            if (_effect is CompositionEffectBrush effectBrush)
            {
                var animation = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
                animation.InsertKeyFrame(0, show ? 0 : Intensity);
                animation.InsertKeyFrame(1, show ? Intensity : 0);

                effectBrush.StartAnimation("Intensity.Opacity", animation);
            }
        }
    }
}
