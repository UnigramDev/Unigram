using System.Numerics;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Composition
{
    public static class CompositionExtensions
    {
        public static CompositionBrush CreateRedirectBrush(this Compositor compositor, UIElement source, Vector2 sourceOffset, Vector2 sourceSize, bool freeze = false)
        {
            // Create a VisualSurface positioned at the same location as this control and feed that
            // through the color effect.
            var surfaceBrush = compositor.CreateSurfaceBrush();
            var surface = compositor.CreateVisualSurface();

            // Select the source visual and the offset/size of this control in that element's space.
            surface.SourceVisual = ElementCompositionPreview.GetElementVisual(source);
            surface.SourceOffset = sourceOffset;
            surface.SourceSize = sourceSize;
            surfaceBrush.Surface = surface;
            surfaceBrush.Stretch = CompositionStretch.Fill;

            if (freeze && surface is object obj && obj is ICompositionVisualSurfacePartner partner)
            {
                partner.Stretch = CompositionStretch.Fill;
                partner.RealizationSize = sourceSize * (float)source.XamlRoot.RasterizationScale;
                partner.Freeze();
            }

            return surfaceBrush;
        }

        public static SpriteVisual CreateRedirectVisual(this Compositor compositor, UIElement source, Vector2 sourceOffset, Vector2 sourceSize, bool freeze = false)
        {
            var redirect = compositor.CreateSpriteVisual();
            redirect.Brush = compositor.CreateRedirectBrush(source, sourceOffset, sourceSize, freeze);
            redirect.Size = sourceSize;

            return redirect;
        }
    }
}
