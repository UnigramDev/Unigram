using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls.Gallery
{
    public class GalleryBottomPanel : Panel
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            var element1 = Children[0];
            var element2 = Children[1];
            var element3 = Children[2] as FrameworkElement;

            element1.Measure(availableSize);
            element2.Measure(availableSize);

            var width = Math.Max(element1.DesiredSize.Width, element2.DesiredSize.Width);
            var height = Math.Max(element1.DesiredSize.Height, element2.DesiredSize.Height);

            if (element3.MinWidth > availableSize.Width - width * 2)
            {
                element3.Measure(new Size(availableSize.Width, availableSize.Height - height));
            }
            else
            {
                element3.Measure(new Size(availableSize.Width - width * 2, availableSize.Height - height));
            }

            return base.MeasureOverride(availableSize);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var element1 = Children[0];
            var element2 = Children[1];
            var element3 = Children[2] as FrameworkElement;

            element1.Arrange(new Rect(
                0,
                finalSize.Height - element1.DesiredSize.Height,
                element1.DesiredSize.Width,
                element1.DesiredSize.Height));
            element2.Arrange(new Rect(
                finalSize.Width - element2.DesiredSize.Width,
                finalSize.Height - element2.DesiredSize.Height,
                element2.DesiredSize.Width,
                element2.DesiredSize.Height));

            var width = Math.Max(element1.DesiredSize.Width, element2.DesiredSize.Width);
            var height = Math.Max(element1.DesiredSize.Height, element2.DesiredSize.Height);

            if (element3.MinWidth > finalSize.Width - width * 2)
            {
                element3.Arrange(new Rect(
                    0,
                    finalSize.Height - element3.DesiredSize.Height - height,
                    finalSize.Width,
                    element3.DesiredSize.Height));
            }
            else
            {
                element3.Arrange(new Rect(
                    width,
                    finalSize.Height - element3.DesiredSize.Height,
                    finalSize.Width - width * 2,
                    element3.DesiredSize.Height));
            }

            return base.ArrangeOverride(finalSize);
        }
    }
}
