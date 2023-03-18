//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    public class SettingsPanel : Panel
    {
        public bool IsHeader { get; set; }

        protected override Size MeasureOverride(Size availableSize)
        {
            var accumulated = 0d;

            foreach (UIElement child in Children)
            {
                child.Measure(availableSize);

                if (child.DesiredSize.Height > 0)
                {
                    accumulated += child.DesiredSize.Height;
                    accumulated += 16;
                }
            }

            if (IsHeader)
            {
                return new Size(availableSize.Width, accumulated - 16);
            }
            else
            {
                return new Size(availableSize.Width, accumulated + 16);
            }
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var accumulated = 0d;

            foreach (var child in Children)
            {
                child.Arrange(new Rect(0, accumulated, finalSize.Width, child.DesiredSize.Height));

                if (child.DesiredSize.Height > 0)
                {
                    accumulated += child.DesiredSize.Height;
                    accumulated += 16;
                }
            }

            return finalSize;
        }
    }
}
