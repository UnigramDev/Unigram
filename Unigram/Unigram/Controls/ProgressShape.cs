//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls
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
