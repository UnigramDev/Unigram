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
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Common
{
    // Ported from https://github.com/microsoft/microsoft-ui-xaml/blob/main/src/dxaml/xcp/components/comptree/HWCompNodeWinRT.cpp
    // TODO: brushes cache, proper invalidation, ...
    // This can be used to have theme shadows on Windows 10, for now we pass
    public partial class ThemeShadowEx
    {
        private readonly FrameworkElement _element;
        private readonly float _translationZ;

        private readonly SpriteVisual _dropShadowSpriteVisual;

        private DropShadowRecipe _recipe;

        public ThemeShadowEx(FrameworkElement element, float translationZ)
        {
            _element = element;
            _element.SizeChanged += OnSizeChanged;
            _element.ActualThemeChanged += OnActualThemeChanged;

            _translationZ = translationZ;
            _dropShadowSpriteVisual = BootStrapper.Current.Compositor.CreateSpriteVisual();

            _recipe = GetDropShadowRecipe(translationZ, _element.ActualTheme);
            UpdateDropShadowRecipeCornerRadius(_element, ref _recipe);
            UpdateDropShadowVisualBrush(BootStrapper.Current.Compositor, _recipe);

            ElementCompositionPreview.SetElementChildVisual(element, _dropShadowSpriteVisual);
        }

        // Let's avoid registering for CornerRadius property changed
        public void Invalidate()
        {
            _recipe = GetDropShadowRecipe(_translationZ, _element.ActualTheme);
            UpdateDropShadowRecipeCornerRadius(_element, ref _recipe);
            UpdateDropShadowVisualBrush(BootStrapper.Current.Compositor, _recipe);
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Update the offset
            Vector3 offset = new();
            offset.X = -_recipe.LeftInset;
            offset.Y = -_recipe.TopInset;
            offset.Z = 0;
            _dropShadowSpriteVisual.Offset = offset;

            // Update the Size
            Vector2 size = new();
            size.X = _element.ActualSize.X + _recipe.LeftInset + _recipe.RightInset;
            size.Y = _element.ActualSize.Y + _recipe.TopInset + _recipe.BottomInset;
            _dropShadowSpriteVisual.Size = size;
        }

        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            _recipe = GetDropShadowRecipe(_translationZ, _element.ActualTheme);
            UpdateDropShadowRecipeCornerRadius(_element, ref _recipe);
            UpdateDropShadowVisualBrush(BootStrapper.Current.Compositor, _recipe);
        }

        void UpdateDropShadowRecipeCornerRadius(FrameworkElement element, ref DropShadowRecipe recipe)
        {
            if (element is Rectangle rect)
            {
                if (rect.RadiusX != 0 || rect.RadiusY != 0)
                {
                    recipe.RadiusX = (float)rect.RadiusX;
                    recipe.RadiusY = (float)rect.RadiusY;
                }
            }
            else
            {
                var cornerRadius = new CornerRadius(0, 0, 0, 0);
                // Since Control doesn't override FrameworkElement::GetCornerRadius, we'll have to detect
                // if the element is a Control, and if so, get the corner radius from Control_CornerRadius instead.
                if (element is Control control)
                {
                    cornerRadius = control.CornerRadius;
                }
                else if (element is Border border)
                {
                    cornerRadius = border.CornerRadius;
                }
                // Add as needed as UWP FrameworkElement wrapper doesn't expose CornerRadius 

                bool hasRoundedCorner =
                cornerRadius.TopLeft != 0
                || cornerRadius.TopRight != 0
                || cornerRadius.BottomLeft != 0
                || cornerRadius.BottomRight != 0;

                if (hasRoundedCorner)
                {
                    // Since we're creating our DropShadow with rectangles, we'll want to
                    // use radiusX and radiusY values. So if the element gives us an XCORNERRADIUS,
                    // fill in the recipe's equivalent radiusX and radiusY values.
                    float cr = (float)Math.Max(Math.Max(cornerRadius.TopLeft, cornerRadius.TopRight), Math.Max(cornerRadius.BottomLeft, cornerRadius.BottomRight));
                    recipe.RadiusX = cr;
                    recipe.RadiusY = cr;
                }
            }
        }

        private void UpdateDropShadowVisualBrush(Compositor compositor, DropShadowRecipe recipe)
        {
            var dropShadowVisual = CreateDropShadowVisual(compositor);

            // We'll need to put the dropShadowVisual inside of a surface to be the source of the NineGrid brush.
            var dropShadowVS = compositor.CreateVisualSurface();
            dropShadowVS.SourceVisual = dropShadowVisual;
            dropShadowVS.SourceSize = dropShadowVisual.Size;

            if (dropShadowVS is object obj && obj is ICompositionVisualSurfacePartner visualSurfacePartner)
            {
                // TODO: Make sure to test in high DPI
                visualSurfacePartner.RealizationSize = dropShadowVisual.Size; // Required to avoid dwm.exe picking an arbitrary size that will cause bluriness.
            }

            var dropShadowBrush = compositor.CreateSurfaceBrush(dropShadowVS);
            dropShadowBrush.Stretch = CompositionStretch.Fill;

            // Since the shape used to generate the shadow is rounded, the insets must extend to include its corner radius.
            // Otherwise the nine grid won't extend into the rounded corners of the content, and there will be no shadow behind
            // the rounded corners.
            // See picture in GetDropShadowRecipe.
            float maxCR = Math.Max(recipe.RadiusX, recipe.RadiusY);
            float leftInset = recipe.LeftInset + maxCR + 1; // We're extending the insets by a pixel to overlap the real visual.
            float topInset = recipe.TopInset + maxCR + 1;  // See diagram and calculations in CreateDropShadowVisual below.
            float rightInset = recipe.RightInset + maxCR + 1;
            float bottomInset = recipe.BottomInset + maxCR + 1;
            var nineGridBrush = compositor.CreateNineGridBrush();
            nineGridBrush.SetInsets(leftInset, topInset, rightInset, bottomInset);
            nineGridBrush.IsCenterHollow = true;
            nineGridBrush.Source = dropShadowBrush;

            // Update the visual with the NineGrid brush and cache it.
            _dropShadowSpriteVisual.Brush = nineGridBrush;
        }

        private Visual CreateDropShadowVisual(Compositor compositor)
        {
            var recipe = _recipe;

            Vector2 roundedRectSize = new(
                // For simplicity we make the content square.
                Math.Max(
                    // The content must be large enough to accommodate rounded corners on both sides.
                    MathF.Round(2 * Math.Max(recipe.RadiusX, recipe.RadiusY)),
                    // A rect that's too small will cast a very light shadow at large shadow radiuses. Clamp to a min value.
                    64.0f
                )
            );

            var roundedRectSizeSmaller = new Vector2(roundedRectSize.X - 2, roundedRectSize.Y - 2);

            // (0, 0) is the top-left corner of the shadow region. The content rounded rect renders at an offset.
            // Note: We add 1 here to both dimensions to keep the smaller dummy shape centered in the area that the real visual
            // is going to cover.
            var roundedRectOffset = new Vector3(recipe.LeftInset + 1, recipe.TopInset + 1, 0);

            //
            // This is the bounding box of the shadow being cast by the content rounded rect. This also defines the size of
            // the drop shadow image that we'll draw using a hollow nine grid brush.
            //
            // Note: recipe.Insets is the amount of space allotted to the shadow, but from empirical measurements the shadow
            // won't actually fill all of this space. There will be a few fully transparent pixels along the edges. This doesn't
            // cause any problems because we're doing a 1:1 mapping from the shadow surface to the visual - the visual that we
            // produce will be large enough to accommodate these empty pixels.
            //
            var containerSize = new Vector2(
                roundedRectSize.X + recipe.LeftInset + recipe.RightInset,
                roundedRectSize.Y + recipe.TopInset + recipe.BottomInset);

            // First we'll create the RoundedRectangleShape that will be put inside
            // both the AmbientShapeVisual and DirectionalShapeVisual.
            var roundedRectangleGeometry = compositor.CreateRoundedRectangleGeometry();
            roundedRectangleGeometry.Size = roundedRectSizeSmaller;
            roundedRectangleGeometry.CornerRadius = new Vector2(recipe.RadiusX, recipe.RadiusY);

            var roundedRectangleShape = compositor.CreateSpriteShape();
            roundedRectangleShape.Geometry = roundedRectangleGeometry;

            // Give the rectangle shape an opaque color - the color will be removed by a clip, but it must be opaque
            // since the DropShadow will inherit from the rectangle's opacity.
            var rectFillColor = compositor.CreateColorBrush(Color.FromArgb(255, 0, 0, 0));
            roundedRectangleShape.FillBrush = rectFillColor;

            // The AmbientLayerVisual has a rounded rectangle shape visual inside as the dummy shape that casts the shadow.
            // The layer visual is sized to include the shadow. We set the shadow's source policy to InheritFromVisualContent
            // so that only the shape inside that draws pixels casts the shadow.
            //
            // At low elevations, there should not be an ambient shadow. This is marked by setting the ambient opacity in the
            // recipe to 0.
            Visual ambientLayerVisual = null;

            if (recipe.AmbientOpacity > 0)
            {
                // Bump up the blur radius to compensate for pushing the shadow to overlap with the real visual.
                var ambientShadow = MakeDropShadow(compositor, recipe.AmbientBlurRadius + 1, recipe.AmbientColor, recipe.AmbientYOffset);

                var ambientShapeVisual = MakeShapeVisual(compositor, roundedRectangleShape, roundedRectSizeSmaller, roundedRectOffset);

                ambientLayerVisual = MakeLayerVisual(compositor, ambientShadow, ambientShapeVisual, containerSize);
            }

            // The DirectionalLayerVisual also has a rounded rectangle shape visual inside as the dummy shape that casts the
            // shadow. The layer visual is sized to include the shadow, and again we set the shadow's source policy to
            // InheritFromVisualContent so that only the dummy shape casts the shadow and not the entire layer visual. This
            // shadow exists at all elevations.
            // Bump up the blur radius to compensate for pushing the shadow to overlap with the real visual.
            var directionalShadow = MakeDropShadow(compositor, recipe.DirectionalBlurRadius + 1, recipe.DirectionalColor, recipe.DirectionalYOffset);

            var directionalShapeVisual = MakeShapeVisual(compositor, roundedRectangleShape, roundedRectSizeSmaller, roundedRectOffset);

            var directionalLayerVisual = MakeLayerVisual(compositor, directionalShadow, directionalShapeVisual, containerSize);

            // Now to combine the Ambient and Directional visuals, we'll put them in a container visual.
            var shadowVisual = compositor.CreateContainerVisual();
            if (ambientLayerVisual != null)
            {
                shadowVisual.Children.InsertAtTop(ambientLayerVisual);
            }
            shadowVisual.Children.InsertAtTop(directionalLayerVisual);
            shadowVisual.Size = containerSize;

            // Now we'll clip out the rounded rectangle from our visual.
            // We'll have to create the clip using D2D since it's able to combine geometries
            var d2OuterRectGeometry = CanvasGeometry.CreateRectangle(null, 0, 0, containerSize.X, containerSize.Y);
            var d2RoundedRectGeometry = CanvasGeometry.CreateRoundedRectangle(null, roundedRectOffset.X, roundedRectOffset.Y, roundedRectSizeSmaller.X, roundedRectSizeSmaller.Y, recipe.RadiusX, recipe.RadiusY);
            var d2PathGeometry = d2OuterRectGeometry.CombineWith(d2RoundedRectGeometry, Matrix3x2.Identity, CanvasGeometryCombine.Exclude);

            var shadowPath = new CompositionPath(d2PathGeometry);
            var compPG = compositor.CreatePathGeometry(shadowPath);
            var geoClip = compositor.CreateGeometricClip(compPG);

            shadowVisual.Clip = geoClip;

            return shadowVisual;
        }

        CompositionShadow MakeDropShadow(Compositor compositor, float blurRadius, Color color, float offsetY)
        {
            var dropShadow = compositor.CreateDropShadow();
            dropShadow.BlurRadius = blurRadius;
            dropShadow.Color = color;
            dropShadow.Offset = new Vector3(0, offsetY, 0);
            dropShadow.SourcePolicy = CompositionDropShadowSourcePolicy.InheritFromVisualContent;

            return dropShadow;
        }

        Visual MakeShapeVisual(Compositor compositor, CompositionShape compositionShape, Vector2 size, Vector3 offset)
        {
            var shapeVisual = compositor.CreateShapeVisual();
            shapeVisual.Shapes.Add(compositionShape);
            shapeVisual.Size = size;
            shapeVisual.Offset = offset;

            return shapeVisual;
        }

        Visual MakeLayerVisual(Compositor compositor, CompositionShadow shadow, Visual child, Vector2 size)
        {
            var layerVisual = compositor.CreateLayerVisual();
            layerVisual.Shadow = shadow;
            layerVisual.Children.InsertAtTop(child);
            layerVisual.Size = size;

            return layerVisual;
        }

        // The values necessary to achieve the theme shadow look, intended to be applied to a composition drop shadow.
        struct DropShadowRecipe
        {
            // Stores the corner radiuses of the content rounded rect that's casting the shadow.
            public float RadiusX;
            public float RadiusY;

            // Drop shadows have the concept of elevation, which effectively simulates elevation that projected shadows
            // have (greater elevation = bigger shadow).
            public float Elevation;

            // There are two sets of BlurRadius/Color because we'll be creating the DropShadowVisual by combining two
            // visuals - an Ambient and a Directional visual with drop shadows.

            // Provided by design. Determines how "blurred" the DropShadow should be and how far out it extends.
            public float AmbientBlurRadius;
            public float DirectionalBlurRadius;

            // Provided by design. Shadows can have a Y offset to give the illusion that light is hitting the caster at a certain angle.
            public float AmbientYOffset;
            public float DirectionalYOffset;

            // Provided by design. The opacity of the shadow.
            public float AmbientOpacity;
            public float DirectionalOpacity;

            // The color of the drop shadow being casted. The AmbientOpacity/DirectionalOpacity is already multiplied in.
            public Color AmbientColor;
            public Color DirectionalColor;

            // How much the drop shadow should poke out from underneath the caster.
            public float LeftInset;
            public float TopInset;
            public float RightInset;
            public float BottomInset;
        }

        // The "theme" drop shadow actually consists of two drop shadows, designed to simulate shadows generated by
        // two shell-wide light sources: one ambient, and one directional.
        // These formulas were taken from the shell team implementation of the ThemeNineGridShadowBrush
        // At low elevations [2 to 16], there should not be an Ambient shadow.
        // At high elevations (> 16), there will be an Ambient shadow.
        private static DropShadowRecipe GetDropShadowRecipe(float translationZ, ElementTheme t)
        {
            DropShadowRecipe recipe = new();
            recipe.Elevation = Math.Min(64.0f, translationZ / 2);   // Clamped to a max of 64 (corresponding to Translation.Z = 128)

            if (recipe.Elevation < 2)
            {
                recipe.AmbientBlurRadius = 2.0f;

                recipe.AmbientOpacity = 0;
                recipe.DirectionalOpacity = 0;
            }
            else if (recipe.Elevation >= 2 && recipe.Elevation <= 16)
            {
                recipe.AmbientBlurRadius = 2.0f;

                // Ambient shadow won't show.
                recipe.AmbientOpacity = 0;
                if (t == ElementTheme.Light)
                {
                    recipe.DirectionalOpacity = Math.Min((recipe.Elevation / 100.0f) + 0.06f, 0.14f);   // maxes out at elevation = 8
                }
                else if (t == ElementTheme.Dark)
                {
                    recipe.DirectionalOpacity = 0.26f;
                }
            }
            else
            {
                recipe.AmbientBlurRadius = recipe.Elevation / 3;    // Translation.Z / 6, but we already divided by 2
                recipe.AmbientYOffset = 2;

                if (t == ElementTheme.Light)
                {
                    recipe.AmbientOpacity = 0.15f;
                    recipe.DirectionalOpacity = 0.19f;
                }
                else if (t == ElementTheme.Dark)
                {
                    recipe.AmbientOpacity = 0.37f;
                    recipe.DirectionalOpacity = 0.37f;
                }
            }

            recipe.AmbientColor = Color.FromArgb((byte)(recipe.AmbientOpacity * 255), 0, 0, 0);
            ;
            recipe.DirectionalBlurRadius = recipe.Elevation;
            recipe.DirectionalYOffset = recipe.Elevation * 0.5f;   // positive means shifted down
            recipe.DirectionalColor = Color.FromArgb((byte)(recipe.DirectionalOpacity * 255), 0, 0, 0);
            ;

            float maxBlurRadius = MathF.Ceiling(Math.Max(recipe.AmbientBlurRadius, recipe.DirectionalBlurRadius));
            recipe.LeftInset = maxBlurRadius;
            recipe.RightInset = maxBlurRadius;
            // If the shadow is shifted vertically, then the amount of space needed for the shadow at the top and bottom
            // depend on both the shadow's blur radius and how much it shifted. A positive offset means to shift down, so
            // subtract from the top and add to the bottom.
            recipe.TopInset = MathF.Ceiling(Math.Max(recipe.AmbientBlurRadius - recipe.AmbientYOffset, recipe.DirectionalBlurRadius - recipe.DirectionalYOffset));
            recipe.BottomInset = MathF.Ceiling(Math.Max(recipe.AmbientBlurRadius + recipe.AmbientYOffset, recipe.DirectionalBlurRadius + recipe.DirectionalYOffset));

            return recipe;
        }
    }
}
