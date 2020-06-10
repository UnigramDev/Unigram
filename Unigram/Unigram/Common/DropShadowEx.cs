using System.Numerics;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Shapes;

namespace Unigram.Common
{
    public static class DropShadowEx
    {
        public static Visual Attach(UIElement element, float radius, float opacity, CompositionClip clip = null)
        {
            var elementVisual = ElementCompositionPreview.GetElementVisual(element);

            var shadow = elementVisual.Compositor.CreateDropShadow();
            shadow.BlurRadius = radius;
            shadow.Opacity = opacity;
            shadow.Color = Colors.Black;

            var visual = elementVisual.Compositor.CreateSpriteVisual();
            visual.Shadow = shadow;
            visual.Size = new Vector2(0, 0);
            visual.Offset = new Vector3(0, 0, 0);
            visual.Clip = clip;

            switch (element)
            {
                case Image image:
                    shadow.Mask = image.GetAlphaMask();
                    break;
                case Shape shape:
                    shadow.Mask = shape.GetAlphaMask();
                    break;
                case TextBlock textBlock:
                    shadow.Mask = textBlock.GetAlphaMask();
                    break;
            }

            ElementCompositionPreview.SetElementChildVisual(element, visual);
            return visual;
        }
    }
}
