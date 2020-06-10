using System;
using Windows.Foundation;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class TablePanel : Panel
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            var panelDesiredSize = new Size();

            foreach (var element in Children)
            {
                element.Measure(new Size(Math.Min(500, availableSize.Width), availableSize.Height));
                panelDesiredSize.Height += element.DesiredSize.Height;
                panelDesiredSize.Width = Math.Max(panelDesiredSize.Width, element.DesiredSize.Width);
            }

            return panelDesiredSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            foreach (var element in Children)
            {
                element.Arrange(new Rect(0, 0, Math.Min(500, finalSize.Width), finalSize.Height));
            }

            return finalSize;
        }
    }
}
