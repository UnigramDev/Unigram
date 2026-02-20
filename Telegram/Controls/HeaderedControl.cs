//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    public partial class HeaderedControl : ItemsControl
    {
        private Grid ContentRoot;

        public HeaderedControl()
        {
            DefaultStyleKey = typeof(HeaderedControl);

            //ItemContainerTransitions = new TransitionCollection
            //{
            //    new RepositionThemeTransition()
            //};
        }

        protected override void OnApplyTemplate()
        {
            ContentRoot = GetTemplateChild(nameof(ContentRoot)) as Grid;

            VisualStateManager.GoToState(this, IsFooterAtBottom ? "FooterBottomLeft" : "FooterTopRight", false);

            base.OnApplyTemplate();
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

        #region IsFooterAtBottom

        public bool IsFooterAtBottom
        {
            get { return (bool)GetValue(IsFooterAtBottomProperty); }
            set { SetValue(IsFooterAtBottomProperty, value); }
        }

        public static readonly DependencyProperty IsFooterAtBottomProperty =
            DependencyProperty.Register("IsFooterAtBottom", typeof(bool), typeof(HeaderedControl), new PropertyMetadata(true, OnIsFooterAtBottomChanged));

        private static void OnIsFooterAtBottomChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((HeaderedControl)d).OnIsFooterAtBottomChanged((bool)e.NewValue, (bool)e.OldValue);
        }

        private void OnIsFooterAtBottomChanged(bool newValue, bool oldValue)
        {
            VisualStateManager.GoToState(this, newValue ? "FooterBottomLeft" : "FooterTopRight", false);
        }

        #endregion

        #region IsFooterLink

        public bool IsFooterLink
        {
            get { return (bool)GetValue(IsFooterLinkProperty); }
            set { SetValue(IsFooterLinkProperty, value); }
        }

        public static readonly DependencyProperty IsFooterLinkProperty =
            DependencyProperty.Register("IsFooterLink", typeof(bool), typeof(HeaderedControl), new PropertyMetadata(false));

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

        public event EventHandler<TextUrlClickEventArgs> Click;

        // Used by TextBlockHelper
        public bool OnClick(string url)
        {
            if (Click != null)
            {
                Click.Invoke(this, new TextUrlClickEventArgs(url));
                return true;
            }

            return false;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (ContentRoot == null || ItemsSource != null)
            {
                return base.MeasureOverride(availableSize);
            }

            ContentRoot.Measure(availableSize);

            if (ItemsPanelRoot?.DesiredSize.Height > 0)
            {
                return ContentRoot.DesiredSize;
            }

            return new Size(0, 0);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (ContentRoot == null || ItemsSource != null)
            {
                return base.ArrangeOverride(finalSize);
            }

            ContentRoot.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));

            return finalSize;
        }
    }

    public partial class TextUrlClickEventArgs
    {
        public TextUrlClickEventArgs(string url)
        {
            Url = url;
        }

        public string Url { get; }
    }

    public partial class HeaderedControlPanel : StackPanel
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            var last = true;
            var first = default(UIElement);

            for (int i = Children.Count - 1; i >= 0; i--)
            {
                var child = Children[i];
                if (child.Visibility == Visibility.Visible)
                {
                    switch (child)
                    {
                        //case ContentPresenter presenter:
                        //    presenter.BorderThickness = new Thickness(0, 0, 0, last ? 0 : 1);
                        //    presenter.CornerRadius = new CornerRadius(0, 0, last ? 4 : 0, last ? 4 : 0);
                        //    break;
                        //case Control control:
                        //    control.BorderThickness = new Thickness(0, 0, 0, last ? 0 : 1);
                        //    control.CornerRadius = new CornerRadius(0, 0, last ? 4 : 0, last ? 4 : 0);
                        //    break;
                        //case Grid grid:
                        //    grid.BorderThickness = new Thickness(0, 0, 0, last ? 0 : 1);
                        //    grid.CornerRadius = new CornerRadius(0, 0, last ? 4 : 0, last ? 4 : 0);
                        //    break;
                        //case Border border:
                        //    border.BorderThickness = new Thickness(0, 0, 0, last ? 0 : 1);
                        //    border.CornerRadius = new CornerRadius(0, 0, last ? 4 : 0, last ? 4 : 0);
                        //    break;

                        case ContentPresenter presenter:
                            presenter.BorderThickness = new Thickness(1);
                            presenter.CornerRadius = new CornerRadius(4);
                            presenter.Margin = new Thickness(0, 0, 0, last ? 0 : 3);
                            break;
                        case Control control:
                            control.BorderThickness = new Thickness(1);
                            control.CornerRadius = new CornerRadius(4);
                            control.Margin = new Thickness(0, 0, 0, last ? 0 : 3);
                            break;
                        case Grid grid:
                            grid.BorderThickness = new Thickness(1);
                            grid.CornerRadius = new CornerRadius(4);
                            grid.Margin = new Thickness(0, 0, 0, last ? 0 : 3);
                            break;
                        case Border border:
                            border.BorderThickness = new Thickness(1);
                            border.CornerRadius = new CornerRadius(4);
                            border.Margin = new Thickness(0, 0, 0, last ? 0 : 3);
                            break;
                    }

                    last = false;
                    first = child;
                }
            }

            //if (first != null)
            //{
            //    switch (first)
            //    {
            //        case ContentPresenter presenter:
            //            presenter.CornerRadius = new CornerRadius(4, 4, presenter.CornerRadius.BottomRight, presenter.CornerRadius.BottomLeft);
            //            break;
            //        case Control control:
            //            control.CornerRadius = new CornerRadius(4, 4, control.CornerRadius.BottomRight, control.CornerRadius.BottomLeft);
            //            break;
            //        case Grid grid:
            //            grid.CornerRadius = new CornerRadius(4, 4, grid.CornerRadius.BottomRight, grid.CornerRadius.BottomLeft);
            //            break;
            //        case Border border:
            //            border.CornerRadius = new CornerRadius(4, 4, border.CornerRadius.BottomRight, border.CornerRadius.BottomLeft);
            //            break;
            //    }
            //}

            return base.MeasureOverride(availableSize);
        }
    }
}
