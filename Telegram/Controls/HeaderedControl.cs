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
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    public partial class HeaderedControl : ItemsControl
    {
        private static readonly Thickness _cardBorder = new(1);
        private static readonly CornerRadius _cardCorner = new(4);

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

        // Every row is a rounded card of its own. It is the same card whichever row it is, so there
        // is nothing for a layout pass to decide - a container is prepared once and keeps it.
        //
        // A ContentPresenter when the row was generated from an item, and the element itself when it
        // was written into the control's content: IsItemItsOwnContainerOverride takes any UIElement,
        // and this runs for both - with element and item the same object in the second case.
        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            ApplyCardLayout(element);

            base.PrepareContainerForItemOverride(element, item);
        }

        internal static void ApplyCardLayout(DependencyObject element)
        {
            switch (element)
            {
                case Control control:
                    control.BorderThickness = _cardBorder;
                    control.CornerRadius = _cardCorner;
                    break;
                case Grid grid:
                    grid.BorderThickness = _cardBorder;
                    grid.CornerRadius = _cardCorner;
                    break;
                case Border border:
                    border.BorderThickness = _cardBorder;
                    border.CornerRadius = _cardCorner;
                    break;
            }
        }

        protected override DependencyObject GetContainerForItemOverride()
        {
            return new HeaderedControlPresenter();
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

    // The rows of a HeaderedControl, stacked, with a gap between the visible ones.
    //
    // Not a StackPanel. The gap used to be a bottom margin written onto every child but the last
    // from inside MeasureOverride, which meant a row could not carry a margin of its own and that
    // finding the last visible one was a backwards scan on every pass.
    public partial class HeaderedControlPanel : Panel
    {
        private const double Spacing = 3;

        protected override Size MeasureOverride(Size availableSize)
        {
            var constraint = new Size(availableSize.Width, double.PositiveInfinity);

            var width = 0d;
            var height = 0d;
            var any = false;

            for (int i = 0; i < Children.Count; i++)
            {
                var child = Children[i];

                if (child.Visibility == Visibility.Collapsed)
                {
                    continue;
                }

                child.Measure(constraint);

                var desired = child.DesiredSize;

                if (any)
                {
                    height += Spacing;
                }

                width = Math.Max(width, desired.Width);
                height += desired.Height;

                any = true;
            }

            return new Size(width, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var y = 0d;
            var any = false;

            for (int i = 0; i < Children.Count; i++)
            {
                var child = Children[i];

                if (child.Visibility == Visibility.Collapsed)
                {
                    continue;
                }

                if (any)
                {
                    y += Spacing;
                }

                // DesiredSize carries the child's own margin and Arrange takes it back out, so a
                // row that wants one composes with the spacing rather than fighting it.
                var height = child.DesiredSize.Height;

                child.Arrange(new Rect(0, y, finalSize.Width, height));

                y += height;
                any = true;
            }

            return finalSize;
        }
    }

    public partial class HeaderedControlPresenter : ContentPresenter
    {
        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            var child = VisualTreeHelper.GetChild(this, 0);
            if (child != null)
            {
                HeaderedControl.ApplyCardLayout(child);
            }
        }
    }
}
