//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.RichEditor
{
    /// <summary>
    /// The middle of the editor's toolbar, the commands in groups as Word's ribbon has them,
    /// each expanded to its buttons or folded into one, expanding in their
    /// <see cref="RichToolbarGroup.Priority"/> order while they keep fitting. Its child is a
    /// scroll viewer holding the panel of groups and separators, which scrolls when even every
    /// group folded does not fit.
    /// </summary>
    /// <remarks>
    /// Only this host knows the width there is: the scroll viewer measures the groups unbounded.
    /// A part not shown is collapsed, not clipped, or it would stay in the tab order and the
    /// automation tree, so a group only has a width in the state it is in. The other one is
    /// remembered from when it was last shown, and may be unknown or stale: a choice is applied
    /// and measured again in the same pass, until the widths it was made on are the real ones.
    /// Groups expand strictly in order, so a wider toolbar never folds one back.
    /// </remarks>
    public sealed partial class RichToolbarHost : Grid
    {
        // Per child of the panel, its whole width expanded and folded, margins and padding
        // included; NaN until it has been measured so. A separator's is the same in both.
        private readonly List<double> _expanded = new();
        private readonly List<double> _folded = new();

        // Group indices, the most wanted first.
        private readonly List<int> _order = new();

        protected override Size MeasureOverride(Size availableSize)
        {
            var size = base.MeasureOverride(availableSize);

            if (double.IsInfinity(availableSize.Width) || Children.Count == 0 || Children[0] is not ScrollViewer { Content: StackPanel groups } scroll)
            {
                return size;
            }

            var limit = availableSize.Width
                - Horizontal(Padding) - Horizontal(BorderThickness)
                - Horizontal(scroll.Margin) - Horizontal(scroll.Padding) - Horizontal(scroll.BorderThickness);

            // Each pass learns the width of every group it flipped, so this settles in one or
            // two; the bound is only against a group whose width depends on its neighbours.
            for (int pass = 0; pass < 4 && Choose(groups, limit); pass++)
            {
                size = base.MeasureOverride(availableSize);
            }

            return size;
        }

        private static double Horizontal(Thickness thickness) => thickness.Left + thickness.Right;

        private static double Known(double width) => double.IsNaN(width) ? 0 : width;

        private bool Choose(StackPanel groups, double limit)
        {
            var items = groups.Children;

            if (_expanded.Count != items.Count)
            {
                _expanded.Clear();
                _folded.Clear();

                for (int i = 0; i < items.Count; i++)
                {
                    _expanded.Add(double.NaN);
                    _folded.Add(double.NaN);
                }
            }

            // The panel's own padding and spacing: whatever its children do not account for.
            var total = groups.DesiredSize.Width;
            _order.Clear();

            for (int i = 0; i < items.Count; i++)
            {
                var width = items[i].DesiredSize.Width;
                total -= width;

                if (items[i] is RichToolbarGroup group)
                {
                    if (group.IsExpanded)
                    {
                        _expanded[i] = width;
                    }
                    else
                    {
                        _folded[i] = width;
                    }

                    Insert(i, group.Priority, items);
                }
                else
                {
                    _expanded[i] = _folded[i] = width;
                }

                // An unknown width is taken as nothing: the pass that follows measures it.
                total += Known(_folded[i]);
            }

            var fits = true;
            var changed = false;

            foreach (var i in _order)
            {
                var delta = Known(_expanded[i]) - Known(_folded[i]);

                fits = fits && total + delta <= limit;

                if (fits)
                {
                    total += delta;
                }

                changed |= ((RichToolbarGroup)items[i]).SetExpanded(fits);
            }

            return changed;
        }

        private void Insert(int index, int priority, UIElementCollection items)
        {
            var at = _order.Count;

            while (at > 0 && ((RichToolbarGroup)items[_order[at - 1]]).Priority > priority)
            {
                at--;
            }

            _order.Insert(at, index);
        }
    }

    /// <summary>
    /// A group of the grouped toolbar: its first child is the group expanded, its buttons over
    /// its label, and the second is the group folded into one button that opens its menu.
    /// </summary>
    public sealed partial class RichToolbarGroup : ContentControl
    {
        private Button Button;
        private ContentPresenter ContentPresenter;

        private bool _expanded;

        public RichToolbarGroup()
        {
            DefaultStyleKey = typeof(RichToolbarGroup);
        }

        protected override void OnApplyTemplate()
        {
            Button = GetTemplateChild(nameof(Button)) as Button;
            Button.Click += OnClick;

            ContentPresenter = GetTemplateChild(nameof(ContentPresenter)) as ContentPresenter;

            SetExpanded(_expanded);

            base.OnApplyTemplate();
        }

        private void OnClick(object sender, RoutedEventArgs e)
        {
            Click?.Invoke(sender, e);
        }

        public event RoutedEventHandler Click;

        /// <summary>Which group expands first when there is room for some of them: the lowest.</summary>
        public int Priority
        {
            get => (int)GetValue(PriorityProperty);
            set => SetValue(PriorityProperty, value);
        }

        public static readonly DependencyProperty PriorityProperty =
            DependencyProperty.Register("Priority", typeof(int), typeof(RichToolbarGroup), new PropertyMetadata(0));

        internal bool IsExpanded => _expanded;

        /// <summary>Returns whether the group changed.</summary>
        internal bool SetExpanded(bool expanded)
        {
            var changed = _expanded != expanded;
            _expanded = expanded;

            if (ContentPresenter is UIElement expandedPart && Button is UIElement foldedPart)
            {
                var show = expanded ? Visibility.Visible : Visibility.Collapsed;
                var hide = expanded ? Visibility.Collapsed : Visibility.Visible;

                if (expandedPart.Visibility != show)
                {
                    expandedPart.Visibility = show;
                }

                if (foldedPart.Visibility != hide)
                {
                    foldedPart.Visibility = hide;
                }
            }

            return changed;
        }

        #region Glyph

        public string Glyph
        {
            get { return (string)GetValue(GlyphProperty); }
            set { SetValue(GlyphProperty, value); }
        }

        public static readonly DependencyProperty GlyphProperty =
            DependencyProperty.Register(nameof(Glyph), typeof(string), typeof(RichToolbarGroup), new PropertyMetadata(string.Empty));

        #endregion

        #region Text

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(RichToolbarGroup), new PropertyMetadata(string.Empty));

        #endregion
    }
}
