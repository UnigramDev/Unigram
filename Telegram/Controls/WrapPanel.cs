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

/// <summary>
/// From https://github.com/Microsoft/UWPCommunityToolkit/tree/dev/Microsoft.Toolkit.Uwp.UI.Controls/WrapPanel
/// Since they don't have it in nuget package yet
/// </summary>
namespace Telegram.Controls
{
    /// <summary>
    /// WrapPanel is a panel that position child control vertically or horizontally based on the orientation and when max width/ max height is received a new row(in case of horizontal) or column (in case of vertical) is created to fit new controls.
    /// </summary>
    public partial class WrapPanel : Panel
    {
        /// <summary>
        /// Gets or sets the orientation of the WrapPanel, Horizontal or vertical means that child controls will be added horizontally until the width of the panel can't fit more control then a new row is added to fit new horizontal added child controls, vertical means that child will be added vertically until the height of the panel is received then a new column is added
        /// </summary>
        public Orientation Orientation
        {
            get => (Orientation)GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        /// <summary>
        /// Identifies the <see cref="Orientation"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty OrientationProperty =
            DependencyProperty.Register(
                "Orientation",
                typeof(Orientation),
                typeof(WrapPanel),
                new PropertyMetadata(Orientation.Horizontal, OrientationPropertyChanged));

        private static void OrientationPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var wrapPanel = d as WrapPanel;
            wrapPanel?.InvalidateMeasure();
            wrapPanel?.InvalidateArrange();
        }

        /// <summary>
        /// Where each row sits across the panel when it is narrower than the panel. Rows only:
        /// a vertical panel's columns always start at its top.
        /// </summary>
        public HorizontalAlignment HorizontalContentAlignment
        {
            get => (HorizontalAlignment)GetValue(HorizontalContentAlignmentProperty);
            set => SetValue(HorizontalContentAlignmentProperty, value);
        }

        public static readonly DependencyProperty HorizontalContentAlignmentProperty =
            DependencyProperty.Register(
                "HorizontalContentAlignment",
                typeof(HorizontalAlignment),
                typeof(WrapPanel),
                new PropertyMetadata(HorizontalAlignment.Left, HorizontalContentAlignmentPropertyChanged));

        private static void HorizontalContentAlignmentPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as WrapPanel)?.InvalidateArrange();
        }

        /// <inheritdoc />
        protected override Size MeasureOverride(Size availableSize)
        {
            var totalMeasure = new UvMeasure();
            var parentMeasure = new UvMeasure(Orientation, availableSize.Width, availableSize.Height);
            var lineMeasure = new UvMeasure();
            foreach (var child in Children)
            {
                child.Measure(availableSize);

                var currentMeasure = new UvMeasure(Orientation, child.DesiredSize.Width, child.DesiredSize.Height);

                if (parentMeasure.U > currentMeasure.U + lineMeasure.U)
                {
                    lineMeasure.U += currentMeasure.U;
                    lineMeasure.V = Math.Max(lineMeasure.V, currentMeasure.V);
                }
                else
                {
                    // new line should be added
                    // to get the max U to provide it correctly to ui width ex: ---| or -----|
                    totalMeasure.U = Math.Max(lineMeasure.U, totalMeasure.U);
                    totalMeasure.V += lineMeasure.V;

                    // if the next new row still can handle more controls
                    if (parentMeasure.U > currentMeasure.U)
                    {
                        // set lineMeasure initial values to the currentMeasure to be calculated later on the new loop
                        lineMeasure = currentMeasure;
                    }

                    // the control will take one row alone
                    else
                    {
                        // validate the new control measures
                        totalMeasure.U = Math.Max(currentMeasure.U, totalMeasure.U);
                        totalMeasure.V += currentMeasure.V;

                        // add new empty line
                        lineMeasure = new UvMeasure();
                    }
                }
            }

            // update value with the last line
            // if the the last loop is(parentMeasure.U > currentMeasure.U + lineMeasure.U) the total isn't calculated then calculate it
            // if the last loop is (parentMeasure.U > currentMeasure.U) the currentMeasure isn't added to the total so add it here
            // for the last condition it is zeros so adding it will make no difference
            // this way is faster than an if condition in every loop for checking the last item
            totalMeasure.U = Math.Max(lineMeasure.U, totalMeasure.U);
            totalMeasure.V += lineMeasure.V;

            return Orientation == Orientation.Horizontal ? new Size(totalMeasure.U, totalMeasure.V) : new Size(totalMeasure.V, totalMeasure.U);
        }

        /// <inheritdoc />
        protected override Size ArrangeOverride(Size finalSize)
        {
            var horizontal = Orientation == Orientation.Horizontal;
            var available = horizontal ? finalSize.Width : finalSize.Height;
            var children = Children;
            var count = children.Count;

            double v = 0;
            var start = 0;

            // A row at a time: first what fits in it, at least one child, then where it starts.
            while (start < count)
            {
                double rowU = 0;
                double rowV = 0;
                var end = start;

                while (end < count)
                {
                    var size = children[end].DesiredSize;
                    var u = horizontal ? size.Width : size.Height;

                    if (end > start && rowU + u > available)
                    {
                        break;
                    }

                    rowU += u;
                    rowV = Math.Max(rowV, horizontal ? size.Height : size.Width);
                    end++;
                }

                var position = horizontal ? GetRowOffset(available - rowU) : 0;

                for (int i = start; i < end; i++)
                {
                    var child = children[i];
                    var size = child.DesiredSize;

                    if (horizontal)
                    {
                        child.Arrange(new Rect(position, v, size.Width, size.Height));
                        position += size.Width;
                    }
                    else
                    {
                        child.Arrange(new Rect(v, position, size.Width, size.Height));
                        position += size.Height;
                    }
                }

                v += rowV;
                start = end;
            }

            return finalSize;
        }

        private double GetRowOffset(double slack)
        {
            return HorizontalContentAlignment switch
            {
                HorizontalAlignment.Center => Math.Max(0, slack) / 2,
                HorizontalAlignment.Right => Math.Max(0, slack),
                _ => 0
            };
        }
    }

    /// <summary>
    /// WrapPanel is a panel that position child control vertically or horizontally based on the orientation and when max width/ max height is received a new row(in case of horizontal) or column (in case of vertical) is created to fit new controls.
    /// </summary>
    public partial class WrapPanel
    {
        internal class UvMeasure
        {
            internal double U { get; set; }

            internal double V { get; set; }

            public UvMeasure()
            {
                U = 0.0;
                V = 0.0;
            }

            public UvMeasure(Orientation orientation, double width, double height)
            {
                if (orientation == Orientation.Horizontal)
                {
                    U = width;
                    V = height;
                }
                else
                {
                    U = height;
                    V = width;
                }
            }
        }
    }
}
