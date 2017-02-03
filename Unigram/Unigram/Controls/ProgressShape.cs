using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Shapes;

namespace Unigram.Controls
{
    public class ProgressShape : ProgressBar
    {
        private Shape HorizontalDecreaseRect;

        protected override void OnApplyTemplate()
        {
            HorizontalDecreaseRect = (Shape)GetTemplateChild("HorizontalDecreaseRect");
        }

        protected override void OnValueChanged(double oldValue, double newValue)
        {
            UpdateView();
        }

        protected override void OnMinimumChanged(double oldMinimum, double newMinimum)
        {
            UpdateView();
        }

        protected override void OnMaximumChanged(double oldMaximum, double newMaximum)
        {
            UpdateView();
        }

        private void UpdateView()
        {
            var minimum = Minimum;
            var maximum = Maximum;
            var value = Value;

            if (minimum < 0)
            {
                var difference = 0 - minimum;
                minimum = 0;
                maximum += difference;
                value += difference;
            }

            // value : maximum = X : actualWidth;
            HorizontalDecreaseRect.Width = 0;
        }
    }
}
