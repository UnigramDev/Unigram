//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Numerics;
using Telegram.Navigation;
using Telegram.Services;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Common
{
    public partial class VisualUtilities
    {
        /// <summary>
        /// Attaches a shimmering placeholder to <paramref name="element"/>, clipped to
        /// <paramref name="shapes"/>: a band of the theme's hover colour sweeping left
        /// to right over a flat fill of the same colour.
        /// </summary>
        /// <param name="size">
        /// How far the band travels. The visual fills the element, but the sweep runs
        /// from -size.X to size.X, so this is the extent of the animation rather than
        /// of the placeholder.
        /// </param>
        /// <param name="shapes">
        /// The outline to clip to. Ownership passes here: they are grouped into the
        /// clip and must not be reused. Callers holding a list can pass its array
        /// directly, which params forwards without copying.
        /// </param>
        public static void SetSkeleton(FrameworkElement element, Vector2 size, params CanvasGeometry[] shapes)
        {
            // An empty group clips everything away, so the skeleton would be attached
            // and invisible. Callers that build shapes from a row count reach this
            // whenever the count is zero, which includes being called before layout has
            // given the host a height.
            if (shapes == null || shapes.Length == 0)
            {
                return;
            }

            var compositor = BootStrapper.Current.Compositor;

            var path = compositor.CreatePathGeometry(new CompositionPath(
                CanvasGeometry.CreateGroup(null, shapes, CanvasFilledRegionDetermination.Winding)));

            // The same colour fills the background and peaks in the middle of the
            // gradient, so the band reads as a highlight passing over the fill rather
            // than a change of colour. Six percent white stands in when the theme has
            // no entry for it.
            var transparent = Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF);
            var shimmer = Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF);

            var lookup = ThemeService.GetLookup(element.ActualTheme);
            if (lookup.TryGetColor("MenuFlyoutItemBackgroundPointerOver", out Color color))
            {
                shimmer = color;
            }

            var gradient = compositor.CreateLinearGradientBrush();
            gradient.StartPoint = new Vector2(0, 0);
            gradient.EndPoint = new Vector2(1, 0);
            gradient.ColorStops.Add(compositor.CreateColorGradientStop(0.0f, transparent));
            gradient.ColorStops.Add(compositor.CreateColorGradientStop(0.5f, shimmer));
            gradient.ColorStops.Add(compositor.CreateColorGradientStop(1.0f, transparent));

            var background = compositor.CreateRectangleGeometry();
            background.Size = size;

            var backgroundShape = compositor.CreateSpriteShape(background);
            backgroundShape.FillBrush = compositor.CreateColorBrush(shimmer);

            var foreground = compositor.CreateRectangleGeometry();
            foreground.Size = size;

            var foregroundShape = compositor.CreateSpriteShape(foreground);
            foregroundShape.FillBrush = gradient;

            var visual = compositor.CreateShapeVisual();
            visual.Clip = compositor.CreateGeometricClip(path);
            visual.Shapes.Add(backgroundShape);
            visual.Shapes.Add(foregroundShape);
            visual.RelativeSizeAdjustment = Vector2.One;

            var animation = compositor.CreateVector2KeyFrameAnimation();
            animation.InsertKeyFrame(0, new Vector2(-size.X, 0));
            animation.InsertKeyFrame(1, new Vector2(size.X, 0));
            animation.IterationBehavior = AnimationIterationBehavior.Forever;
            animation.Duration = TimeSpan.FromSeconds(1);

            foregroundShape.StartAnimation("Offset", animation);

            ElementCompositionPreview.SetElementChildVisual(element, visual);
        }

        // The geometries above are handed to CreateGroup, which keeps them, and the
        // group reaches the compositor through a CompositionPath. Whether Composition
        // copies the path or holds the D2D geometry for re-rasterization decides
        // whether any of this can be disposed, and disposing on the wrong answer either
        // leaks or corrupts the clip. Left to the finalizer until that is settled — but
        // now in one place rather than seven.
    }
}
