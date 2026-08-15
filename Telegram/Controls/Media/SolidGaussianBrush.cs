//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using System.Numerics;
using Telegram.Common;
using Telegram.Navigation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;

namespace Telegram.Controls.Media
{
    public partial class SolidGaussianBrush : PowerSavingBrushBase
    {
        protected override CompositionBrush OnUpdateBrush()
        {
            return CreateBrush(BootStrapper.Current.Compositor, TintColor);
        }

        /// <summary>
        /// The same effect graph this brush paints with, for callers drawing into Composition
        /// directly rather than through XAML.
        /// </summary>
        public static CompositionBrush CreateBrush(Compositor compositor, Color tintColor)
        {
            var gaussianBlur = new GaussianBlurEffect
            {
                Name = "Blur",
                BlurAmount = 30,
                Optimization = EffectOptimization.Speed,
                BorderMode = EffectBorderMode.Hard,
                Source = new CompositionEffectSourceParameter("Backdrop"),
            };

            var saturationEffect = new SaturationEffect
            {
                Name = "Saturation",
                Saturation = 1.7f,
                Source = gaussianBlur
            };

            var tintColorEffect = new ColorSourceEffect
            {
                Name = "TintColor",
                Color = tintColor
            };

            var compositeEffect = new CompositeEffect();
            compositeEffect.Mode = CanvasComposite.SourceOver;
            compositeEffect.Sources.Add(saturationEffect);
            compositeEffect.Sources.Add(tintColorEffect);

            var effectFactory = compositor.CreateEffectFactory(compositeEffect);
            var backdrop = compositor.CreateBackdropBrush();

            var brush = effectFactory.CreateBrush();
            brush.SetSourceParameter("Backdrop", backdrop);

            return brush;
        }

        /// <summary>
        /// A circular brush for a SpriteVisual of the same diameter, frosted where materials are
        /// allowed and flat where they are not.
        /// </summary>
        /// <remarks>
        /// A circle drawn as a CompositionSpriteShape cannot carry this: shape fills take colour
        /// and gradient brushes only. So the circle becomes a mask instead - drawn once into a
        /// VisualSurface, which keeps the antialiasing a geometric clip would have thrown away -
        /// and the effect is what it masks.
        ///
        /// The tint is baked into the effect graph, so a theme change needs a new brush rather
        /// than a property set on this one.
        /// </remarks>
        public static CompositionBrush CreateCircleBrush(Compositor compositor, float radius, Color tintColor)
        {
            var ellipse = compositor.CreateEllipseGeometry();
            ellipse.Radius = new Vector2(radius);

            var ellipseShape = compositor.CreateSpriteShape(ellipse);
            ellipseShape.FillBrush = compositor.CreateColorBrush(Colors.White);
            ellipseShape.Offset = new Vector2(radius);

            var shape = compositor.CreateShapeVisual();
            shape.Shapes.Add(ellipseShape);
            shape.Size = new Vector2(radius * 2);

            var surface = compositor.CreateVisualSurface();
            surface.SourceVisual = shape;
            surface.SourceSize = new Vector2(radius * 2);

            // Masked in both cases, so the fallback is still a circle and not a square.
            var brush = compositor.CreateMaskBrush();
            brush.Mask = compositor.CreateSurfaceBrush(surface);
            brush.Source = PowerSavingPolicy.AreMaterialsEnabled
                ? CreateBrush(compositor, tintColor)
                : compositor.CreateColorBrush(tintColor);

            return brush;
        }

        #region TintColor

        public Color TintColor
        {
            get { return (Color)GetValue(TintColorProperty); }
            set { SetValue(TintColorProperty, value); }
        }

        public static readonly DependencyProperty TintColorProperty =
            DependencyProperty.Register("TintColor", typeof(Color), typeof(SolidGaussianBrush), new PropertyMetadata(default(Color)));

        #endregion
    }
}
