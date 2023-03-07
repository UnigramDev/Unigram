//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Unigram.Controls
{
    public class HeaderedControl : ItemsControl
    {
        public HeaderedControl()
        {
            DefaultStyleKey = typeof(HeaderedControl);
        }

        #region Header

        public string Header
        {
            get => (string)GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register("Header", typeof(string), typeof(HeaderedControl), new PropertyMetadata(null));

        #endregion

        #region Footer

        public string Footer
        {
            get => (string)GetValue(FooterProperty);
            set => SetValue(FooterProperty, value);
        }

        public static readonly DependencyProperty FooterProperty =
            DependencyProperty.Register("Footer", typeof(string), typeof(HeaderedControl), new PropertyMetadata(null));

        #endregion

        #region ItemPresenterStyle

        public Style ItemPresenterStyle
        {
            get { return (Style)GetValue(ItemPresenterStyleProperty); }
            set { SetValue(ItemPresenterStyleProperty, value); }
        }

        public static readonly DependencyProperty ItemPresenterStyleProperty =
            DependencyProperty.Register("ItemPresenterStyle", typeof(Style), typeof(HeaderedControl), new PropertyMetadata(null));

        #endregion

        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            if (element is ContentPresenter presenter)
            {
                presenter.Style = ItemPresenterStyle;
            }

            base.PrepareContainerForItemOverride(element, item);
        }
    }

    public class HeaderedControlPanel : StackPanel
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            var last = true;
            var first = default(UIElement);

            for (int i = Children.Count - 1; i >= 0; i--)
            {
                if (Children[i].Visibility == Visibility.Visible)
                {
                    switch (Children[i])
                    {
                        case ContentPresenter presenter:
                            presenter.BorderThickness = new Thickness(0, 0, 0, last ? 0 : 1);
                            presenter.CornerRadius = new CornerRadius(0, 0, last ? 4 : 0, last ? 4 : 0);
                            break;
                        case Control control:
                            control.BorderThickness = new Thickness(0, 0, 0, last ? 0 : 1);
                            control.CornerRadius = new CornerRadius(0, 0, last ? 4 : 0, last ? 4 : 0);
                            break;
                        case Grid grid:
                            grid.BorderThickness = new Thickness(0, 0, 0, last ? 0 : 1);
                            grid.CornerRadius = new CornerRadius(0, 0, last ? 4 : 0, last ? 4 : 0);
                            break;
                        case Border border:
                            border.BorderThickness = new Thickness(0, 0, 0, last ? 0 : 1);
                            border.CornerRadius = new CornerRadius(0, 0, last ? 4 : 0, last ? 4 : 0);
                            break;
                    }

                    last = false;
                    first = Children[i];
                }
            }

            if (first != null)
            {
                switch (first)
                {
                    case ContentPresenter presenter:
                        presenter.CornerRadius = new CornerRadius(4, 4, presenter.CornerRadius.BottomRight, presenter.CornerRadius.BottomLeft);
                        break;
                    case Control control:
                        control.CornerRadius = new CornerRadius(4, 4, control.CornerRadius.BottomRight, control.CornerRadius.BottomLeft);
                        break;
                    case Grid grid:
                        grid.CornerRadius = new CornerRadius(4, 4, grid.CornerRadius.BottomRight, grid.CornerRadius.BottomLeft);
                        break;
                    case Border border:
                        border.CornerRadius = new CornerRadius(4, 4, border.CornerRadius.BottomRight, border.CornerRadius.BottomLeft);
                        break;
                }
            }

            return base.MeasureOverride(availableSize);
        }
    }
}
