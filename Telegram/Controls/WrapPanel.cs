//
// Copyright Fela Ameghino 2015-2024
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
            var parentMeasure = new UvMeasure(Orientation, finalSize.Width, finalSize.Height);
            var position = new UvMeasure();

            double currentV = 0;
            foreach (var child in Children)
            {
                var desiredMeasure = new UvMeasure(Orientation, child.DesiredSize.Width, child.DesiredSize.Height);
                if ((desiredMeasure.U + position.U) > parentMeasure.U)
                {
                    // next row!
                    position.U = 0;
                    position.V += currentV;
                    currentV = 0;
                }

                // Place the item
                if (Orientation == Orientation.Horizontal)
                {
                    child.Arrange(new Rect(position.U, position.V, child.DesiredSize.Width, child.DesiredSize.Height));
                }
                else
                {
                    child.Arrange(new Rect(position.V, position.U, child.DesiredSize.Width, child.DesiredSize.Height));
                }

                // adjust the location for the next items
                position.U += desiredMeasure.U;
                currentV = Math.Max(desiredMeasure.V, currentV);
            }

            return finalSize;
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
