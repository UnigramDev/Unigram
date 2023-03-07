//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Shapes;
using System.Numerics;
using Unigram.Navigation;

namespace Unigram.Common
{
    public static class DropShadowEx
    {
        public static SpriteVisual Attach(UIElement element, float radius = 20, float opacity = 0.25f)
        {
            var shadow = BootStrapper.Current.Compositor.CreateDropShadow();
            shadow.BlurRadius = radius;
            shadow.Opacity = opacity;
            shadow.Color = Colors.Black;

            var visual = BootStrapper.Current.Compositor.CreateSpriteVisual();
            visual.Shadow = shadow;
            visual.Size = new Vector2(0, 0);
            visual.Offset = new Vector3(0, 0, 0);
            visual.RelativeSizeAdjustment = Vector2.One;

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
