//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using Telegram.Composition;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Common
{
    /// <summary>
    /// The drop shadow XAML draws for a <see cref="Windows.UI.Xaml.Media.ThemeShadow"/>, as an
    /// ordinary child visual: a hollow nine grid over a pre-rendered ambient plus directional
    /// shadow.
    /// </summary>
    /// <remarks>
    /// Ported from https://github.com/microsoft/microsoft-ui-xaml/blob/main/src/dxaml/xcp/components/comptree/HWCompNodeWinRT.cpp
    /// <para>
    /// This is for Windows 10, where <c>UIElement.Shadow</c> is still the projected shadow. On
    /// Windows 11 a package targeting SDK 22000 or later already gets this exact drop shadow from
    /// the framework, so <c>UIElement.Shadow</c> is the cheaper way to ask for it there - it needs
    /// no managed object per caster.
    /// </para>
    /// <para>
    /// Brushes are cached per window and per recipe, the way the framework caches them on its
    /// WUCBrushManager: every bubble in a chat shares one. A caster costs a sprite visual and the
    /// two subscriptions it needs to keep up with size and theme, which <see cref="Detach"/>
    /// releases.
    /// </para>
    /// <para>
    /// The element's child visual slot belongs to this object for as long as it is attached.
    /// </para>
    /// </remarks>
    public partial class ThemeShadowEx
    {
        private readonly FrameworkElement _element;
        private readonly float _translationZ;

        private readonly SpriteVisual _visual;

        private DropShadowRecipe _recipe;
        private RecipeKey _key;

        private bool _detached;

        public ThemeShadowEx(FrameworkElement element, float translationZ)
        {
            _element = element;
            _translationZ = translationZ;

            _visual = ElementComposition.GetElementVisual(element).Compositor.CreateSpriteVisual();
            _visual.IsVisible = false;

            _element.SizeChanged += OnSizeChanged;
            _element.ActualThemeChanged += OnActualThemeChanged;

            ElementComposition.SetElementChildVisual(element, _visual);

            Update();
        }

        /// <summary>
        /// Rebuilds the shadow if anything it is derived from has changed. Cheap and safe to call
        /// on any update: an unchanged recipe reuses the brush and only re-reads the size.
        /// </summary>
        /// <remarks>
        /// Corner radius is read rather than observed - registering for a property changed callback
        /// on every caster costs more than the callers, who know when they changed it.
        /// </remarks>
        public void Invalidate()
        {
            Update();
        }

        private bool _isVisible = true;
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (_isVisible != value)
                {
                    _isVisible = value;
                    Update();
                }
            }
        }

        /// <summary>
        /// Detaches from the element and releases the sprite. The cached brush is shared and stays.
        /// </summary>
        public void Detach()
        {
            if (_detached)
            {
                return;
            }

            _detached = true;

            _element.SizeChanged -= OnSizeChanged;
            _element.ActualThemeChanged -= OnActualThemeChanged;

            ElementComposition.SetElementChildVisual(_element, null);

            _visual.Brush = null;
            _visual.Dispose();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            Update();
        }

        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            Update();
        }

        private void Update()
        {
            if (_detached)
            {
                return;
            }

            // Casters are built before the window has content, and again from a recycled element
            // that is not in the tree yet. Both come back through SizeChanged once laid out.
            var xamlRoot = _element.XamlRoot;
            if (xamlRoot == null)
            {
                _visual.IsVisible = false;
                return;
            }

            var size = _element.ActualSize;
            if (!_isVisible || size.X < 1 || size.Y < 1)
            {
                _visual.IsVisible = false;
                return;
            }

            GetCornerRadius(_element, out var radiusX, out var radiusY);

            var key = new RecipeKey(_translationZ, _element.ActualTheme, radiusX, radiusY);
            if (!key.Equals(_key))
            {
                _key = key;
                _recipe = GetDropShadowRecipe(key);
                _visual.Brush = GetBrush(xamlRoot, _visual.Compositor, key, _recipe);
            }

            _visual.Offset = new Vector3(-_recipe.LeftInset, -_recipe.TopInset, 0);
            _visual.Size = new Vector2(
                size.X + _recipe.LeftInset + _recipe.RightInset,
                size.Y + _recipe.TopInset + _recipe.BottomInset);
            _visual.IsVisible = true;
        }

        private static void GetCornerRadius(FrameworkElement element, out float radiusX, out float radiusY)
        {
            if (element is Rectangle rect)
            {
                radiusX = (float)rect.RadiusX;
                radiusY = (float)rect.RadiusY;
                return;
            }

            // Control does not override FrameworkElement::GetCornerRadius, so the value has to come
            // from the type that declares it. Add others as needed: the UWP FrameworkElement does
            // not expose CornerRadius.
            var cornerRadius = element switch
            {
                Control control => control.CornerRadius,
                Border border => border.CornerRadius,
                _ => default
            };

            // The shadow is drawn from a rounded rectangle, which has one radius per axis, so a
            // CornerRadius collapses to its largest corner.
            radiusX = radiusY = (float)Math.Max(
                Math.Max(cornerRadius.TopLeft, cornerRadius.TopRight),
                Math.Max(cornerRadius.BottomLeft, cornerRadius.BottomRight));
        }

        #region Brush cache

        // Keyed on the window because a composition object belongs to one view's compositor, and
        // not on the thread: a XAML island host puts more than one window on a thread.
        private static readonly ConditionalWeakTable<XamlRoot, Dictionary<RecipeKey, CompositionBrush>> _brushes = new();
        private static readonly ConditionalWeakTable<XamlRoot, Dictionary<RecipeKey, CompositionBrush>>.CreateValueCallback _create = _ => new Dictionary<RecipeKey, CompositionBrush>();

        private static CompositionBrush GetBrush(XamlRoot xamlRoot, Compositor compositor, RecipeKey key, DropShadowRecipe recipe)
        {
            var cache = _brushes.GetValue(xamlRoot, _create);
            if (cache.TryGetValue(key, out var brush))
            {
                return brush;
            }

            brush = CreateBrush(compositor, recipe);
            cache[key] = brush;

            return brush;
        }

        /// <summary>
        /// The same shadow, for a caster that paints it as its own fill rather than hosting it as a
        /// child visual. Uncached: the caller is a brush that is itself declared once per theme.
        /// </summary>
        internal static CompositionBrush CreateBrush(Compositor compositor, float translationZ, ElementTheme theme, float radiusX, float radiusY)
        {
            var key = new RecipeKey(translationZ, theme, radiusX, radiusY);
            return CreateBrush(compositor, GetDropShadowRecipe(key));
        }

        /// <summary>
        /// How far the shadow falls outside the caster, which is how much the caster has to grow by
        /// to leave room for it. A function of elevation alone - theme moves colour and opacity, and
        /// the corner radius belongs to the nine grid rather than to the bounds.
        /// </summary>
        internal static Thickness GetInsets(float translationZ)
        {
            var recipe = GetDropShadowRecipe(new RecipeKey(translationZ, ElementTheme.Default, 0, 0));
            return new Thickness(recipe.LeftInset, recipe.TopInset, recipe.RightInset, recipe.BottomInset);
        }

        /// <summary>
        /// Everything the brush is derived from. Two casters that agree on all of it share one.
        /// </summary>
        private readonly struct RecipeKey : IEquatable<RecipeKey>
        {
            public readonly float TranslationZ;
            public readonly ElementTheme Theme;
            public readonly float RadiusX;
            public readonly float RadiusY;

            public RecipeKey(float translationZ, ElementTheme theme, float radiusX, float radiusY)
            {
                TranslationZ = translationZ;
                Theme = theme;
                RadiusX = radiusX;
                RadiusY = radiusY;
            }

            public bool Equals(RecipeKey other)
            {
                return TranslationZ == other.TranslationZ
                    && Theme == other.Theme
                    && RadiusX == other.RadiusX
                    && RadiusY == other.RadiusY;
            }

            public override bool Equals(object obj)
            {
                return obj is RecipeKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(TranslationZ, Theme, RadiusX, RadiusY);
            }
        }

        #endregion

        private static CompositionBrush CreateBrush(Compositor compositor, DropShadowRecipe recipe)
        {
            var shadowVisual = CreateDropShadowVisual(compositor, recipe);

            // The shadow visual has to go through a surface to be the source of the nine grid.
            var surface = compositor.CreateVisualSurface();
            surface.SourceVisual = shadowVisual;
            surface.SourceSize = shadowVisual.Size;

            // Logical size, and the insets below stay logical with it - the framework does the same
            // and passes 1.0 for the nine grid's realization scale, so every dialog and flyout on
            // Windows 11 is drawn this way at every scale. If a shadow ever comes out soft at 200%,
            // the fix is the pair WUCBrushManager::GetNineGridBrush spells out: realize at
            // size * scale, insets * scale, inset scales 1 / scale. Not one without the other.
            if (surface.TryGetPartner(out var partner))
            {
                // Required to avoid dwm picking an arbitrary size that will cause bluriness.
                partner.SetRealizationSize(shadowVisual.Size);
            }

            // Deliberately not frozen, unlike the other visual surfaces in the app: freezing drops
            // the source, and a bubble shadow has to survive a device loss that a burst effect does
            // not live long enough to see.

            var surfaceBrush = compositor.CreateSurfaceBrush(surface);
            surfaceBrush.Stretch = CompositionStretch.Fill; // Required for use as a nine grid source

            // The shape casting the shadow is rounded, so the insets have to extend over its corner
            // radius: otherwise the nine grid does not reach into the rounded corners of the content
            // and there is no shadow behind them. One pixel more so it overlaps the real visual.
            var maxCR = Math.Max(recipe.RadiusX, recipe.RadiusY);

            var nineGridBrush = compositor.CreateNineGridBrush();
            nineGridBrush.SetInsets(
                recipe.LeftInset + maxCR + 1,
                recipe.TopInset + maxCR + 1,
                recipe.RightInset + maxCR + 1,
                recipe.BottomInset + maxCR + 1);
            nineGridBrush.IsCenterHollow = true;
            nineGridBrush.Source = surfaceBrush;

            return nineGridBrush;
        }

        // Creates this visual tree:
        // <ContainerVisual>         <!-- Sized to the largest shadow's visible bounds -->
        //   <ContainerVisual.Clip>
        //     <GeometricClip/>      <!-- Removes the pixels the shape visuals drew, keeps the shadows -->
        //   </ContainerVisual.Clip>
        //   <LayerVisual>           <!-- Ambient shadow, only above elevation 16 -->
        //   <LayerVisual>           <!-- Directional shadow -->
        // </ContainerVisual>
        private static Visual CreateDropShadowVisual(Compositor compositor, DropShadowRecipe recipe)
        {
            var roundedRectSize = new Vector2(
                // Square, for simplicity. Large enough for a rounded corner on both sides, and
                // never so small that a large blur radius washes the shadow out.
                Math.Max(MathF.Round(2 * Math.Max(recipe.RadiusX, recipe.RadiusY)), 64.0f));

            var roundedRectSizeSmaller = new Vector2(roundedRectSize.X - 2, roundedRectSize.Y - 2);

            // (0, 0) is the top-left of the shadow region, so the content renders at an offset. The
            // extra pixel keeps the smaller dummy shape centered in the area the real visual covers.
            var roundedRectOffset = new Vector3(recipe.LeftInset + 1, recipe.TopInset + 1, 0);

            // The bounding box of the shadow the rounded rect casts, and so the size of the image
            // the hollow nine grid draws. The insets are the space allotted to the shadow and it
            // does not fill all of it - the empty pixels along the edges are harmless, the mapping
            // from surface to visual is 1:1.
            var containerSize = new Vector2(
                roundedRectSize.X + recipe.LeftInset + recipe.RightInset,
                roundedRectSize.Y + recipe.TopInset + recipe.BottomInset);

            var roundedRectangleGeometry = compositor.CreateRoundedRectangleGeometry();
            roundedRectangleGeometry.Size = roundedRectSizeSmaller;
            roundedRectangleGeometry.CornerRadius = new Vector2(recipe.RadiusX, recipe.RadiusY);

            // Opaque: the clip removes the colour, but the drop shadow inherits the shape's opacity.
            var fillBrush = compositor.CreateColorBrush(Color.FromArgb(255, 0, 0, 0));

            // One shape per shape visual, over the shared geometry. The framework puts the same
            // shape in both, which only ever runs above elevation 16 - not worth inheriting a
            // question about who owns a shape for an object that is created once per recipe.
            CompositionSpriteShape CreateShape()
            {
                var shape = compositor.CreateSpriteShape();
                shape.Geometry = roundedRectangleGeometry;
                shape.FillBrush = fillBrush;

                return shape;
            }

            var shadowVisual = compositor.CreateContainerVisual();

            // At low elevations there is no ambient shadow, which the recipe marks with a zero
            // opacity. The blur radius is bumped by a pixel to compensate for pushing the shadow
            // into the real visual. InheritFromVisualContent keeps the layer visual itself from
            // casting - only the shape inside it draws pixels.
            if (recipe.AmbientOpacity > 0)
            {
                var ambientShadow = MakeDropShadow(compositor, recipe.AmbientBlurRadius + 1, recipe.AmbientColor, recipe.AmbientYOffset);
                var ambientShapeVisual = MakeShapeVisual(compositor, CreateShape(), roundedRectSizeSmaller, roundedRectOffset);

                shadowVisual.Children.InsertAtTop(MakeLayerVisual(compositor, ambientShadow, ambientShapeVisual, containerSize));
            }

            var directionalShadow = MakeDropShadow(compositor, recipe.DirectionalBlurRadius + 1, recipe.DirectionalColor, recipe.DirectionalYOffset);
            var directionalShapeVisual = MakeShapeVisual(compositor, CreateShape(), roundedRectSizeSmaller, roundedRectOffset);

            shadowVisual.Children.InsertAtTop(MakeLayerVisual(compositor, directionalShadow, directionalShapeVisual, containerSize));
            shadowVisual.Size = containerSize;

            // Clip the rounded rectangle back out of the result. D2D because it combines geometries.
            // Rooted with using: Win2D's UWP projection has no GC.KeepAlive, so a geometry whose only
            // reference is a temporary can be finalized while CombineWith is still running.
            using var outerRect = CanvasGeometry.CreateRectangle(null, 0, 0, containerSize.X, containerSize.Y);
            using var innerRect = CanvasGeometry.CreateRoundedRectangle(null, roundedRectOffset.X, roundedRectOffset.Y, roundedRectSizeSmaller.X, roundedRectSizeSmaller.Y, recipe.RadiusX, recipe.RadiusY);
            using var combined = outerRect.CombineWith(innerRect, Matrix3x2.Identity, CanvasGeometryCombine.Exclude);

            shadowVisual.Clip = compositor.CreateGeometricClip(compositor.CreatePathGeometry(new CompositionPath(combined)));

            return shadowVisual;
        }

        private static CompositionShadow MakeDropShadow(Compositor compositor, float blurRadius, Color color, float offsetY)
        {
            var dropShadow = compositor.CreateDropShadow();
            dropShadow.BlurRadius = blurRadius;
            dropShadow.Color = color;
            dropShadow.Offset = new Vector3(0, offsetY, 0);
            dropShadow.SourcePolicy = CompositionDropShadowSourcePolicy.InheritFromVisualContent;

            return dropShadow;
        }

        private static Visual MakeShapeVisual(Compositor compositor, CompositionShape compositionShape, Vector2 size, Vector3 offset)
        {
            var shapeVisual = compositor.CreateShapeVisual();
            shapeVisual.Shapes.Add(compositionShape);
            shapeVisual.Size = size;
            shapeVisual.Offset = offset;

            return shapeVisual;
        }

        private static Visual MakeLayerVisual(Compositor compositor, CompositionShadow shadow, Visual child, Vector2 size)
        {
            var layerVisual = compositor.CreateLayerVisual();
            layerVisual.Shadow = shadow;
            layerVisual.Children.InsertAtTop(child);
            layerVisual.Size = size;

            return layerVisual;
        }

        // The values necessary to achieve the theme shadow look, intended to be applied to a
        // composition drop shadow.
        private struct DropShadowRecipe
        {
            // The corner radiuses of the content rounded rect that casts the shadow.
            public float RadiusX;
            public float RadiusY;

            // There are two sets of blur radius and colour because the shadow is two visuals - an
            // ambient and a directional one, each with a drop shadow.

            // Provided by design. How blurred the shadow is, and so how far out it extends.
            public float AmbientBlurRadius;
            public float DirectionalBlurRadius;

            // Provided by design. A Y offset gives the illusion of light hitting the caster at an
            // angle.
            public float AmbientYOffset;
            public float DirectionalYOffset;

            // Provided by design.
            public float AmbientOpacity;
            public float DirectionalOpacity;

            // The colour cast, opacity already multiplied in.
            public Color AmbientColor;
            public Color DirectionalColor;

            // How far the shadow pokes out from underneath the caster.
            public float LeftInset;
            public float TopInset;
            public float RightInset;
            public float BottomInset;
        }

        // The "theme" drop shadow is two drop shadows, simulating the two shell-wide light sources:
        // one ambient, one directional. The formulas are the shell team's ThemeNineGridShadowBrush.
        // Below elevation 16 there is no ambient shadow, above it there is.
        private static DropShadowRecipe GetDropShadowRecipe(RecipeKey key)
        {
            DropShadowRecipe recipe = new();
            recipe.RadiusX = key.RadiusX;
            recipe.RadiusY = key.RadiusY;

            // Clamped to a max of 64, which is Translation.Z = 128
            var elevation = Math.Min(64.0f, key.TranslationZ / 2);

            if (elevation < 2)
            {
                recipe.AmbientBlurRadius = 2.0f;

                recipe.AmbientOpacity = 0;
                recipe.DirectionalOpacity = 0;
            }
            else if (elevation >= 2 && elevation <= 16)
            {
                recipe.AmbientBlurRadius = 2.0f;

                // Ambient shadow won't show.
                recipe.AmbientOpacity = 0;
                if (key.Theme == ElementTheme.Dark)
                {
                    recipe.DirectionalOpacity = 0.26f;
                }
                else
                {
                    // Maxes out at elevation = 8
                    recipe.DirectionalOpacity = Math.Min((elevation / 100.0f) + 0.06f, 0.14f);
                }
            }
            else
            {
                // Translation.Z / 6, but it was already divided by 2
                recipe.AmbientBlurRadius = elevation / 3;
                recipe.AmbientYOffset = 2;

                if (key.Theme == ElementTheme.Dark)
                {
                    recipe.AmbientOpacity = 0.37f;
                    recipe.DirectionalOpacity = 0.37f;
                }
                else
                {
                    recipe.AmbientOpacity = 0.15f;
                    recipe.DirectionalOpacity = 0.19f;
                }
            }

            recipe.AmbientColor = Color.FromArgb((byte)(recipe.AmbientOpacity * 255), 0, 0, 0);
            recipe.DirectionalBlurRadius = elevation;
            recipe.DirectionalYOffset = elevation * 0.5f; // Positive means shifted down
            recipe.DirectionalColor = Color.FromArgb((byte)(recipe.DirectionalOpacity * 255), 0, 0, 0);

            var maxBlurRadius = MathF.Ceiling(Math.Max(recipe.AmbientBlurRadius, recipe.DirectionalBlurRadius));
            recipe.LeftInset = maxBlurRadius;
            recipe.RightInset = maxBlurRadius;

            // A vertically shifted shadow needs different room above and below: the space depends on
            // both the blur radius and the shift, and a positive offset means down.
            recipe.TopInset = MathF.Ceiling(Math.Max(recipe.AmbientBlurRadius - recipe.AmbientYOffset, recipe.DirectionalBlurRadius - recipe.DirectionalYOffset));
            recipe.BottomInset = MathF.Ceiling(Math.Max(recipe.AmbientBlurRadius + recipe.AmbientYOffset, recipe.DirectionalBlurRadius + recipe.DirectionalYOffset));

            return recipe;
        }
    }
}
