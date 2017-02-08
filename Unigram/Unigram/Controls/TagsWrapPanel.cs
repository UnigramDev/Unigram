using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using LinqToVisualTree;

namespace Unigram.Controls
{
    /// <summary>
    /// Positions child elements sequentially from left to right or top to
    /// bottom.  When elements extend beyond the panel edge, elements are
    /// positioned in the next row or column.
    /// </summary>
    /// <QualityBand>Mature</QualityBand>
    public partial class TagsWrapPanel : Panel
    {
        /// <summary>
        /// A value indicating whether a dependency property change handler
        /// should ignore the next change notification.  This is used to reset
        /// the value of properties without performing any of the actions in
        /// their change handlers.
        /// </summary>
        private bool _ignorePropertyChange;

        private TagsTextBox _textBox;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="T:System.Windows.Controls.WrapPanel" /> class.
        /// </summary>
        public TagsWrapPanel()
        {
        }

        /// <summary>
        /// Property changed handler for ItemHeight and ItemWidth.
        /// </summary>
        /// <param name="d">
        /// WrapPanel that changed its ItemHeight or ItemWidth.
        /// </param>
        /// <param name="e">Event arguments.</param>
        [SuppressMessage("Microsoft.Usage", "CA2208:InstantiateArgumentExceptionsCorrectly", Justification = "Almost always set from the CLR property.")]
        private static void OnItemHeightOrWidthPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            TagsWrapPanel source = (TagsWrapPanel)d;
            double value = (double)e.NewValue;

            // Ignore the change if requested
            if (source._ignorePropertyChange)
            {
                source._ignorePropertyChange = false;
                return;
            }

            // Validate the length (which must either be NaN or a positive,
            // finite number)
            if (!value.IsNaN() && ((value <= 0.0) || double.IsPositiveInfinity(value)))
            {
                // Reset the property to its original state before throwing
                source._ignorePropertyChange = true;
                source.SetValue(e.Property, (double)e.OldValue);

                string message = string.Format(
                    CultureInfo.InvariantCulture,
                    "WrapPanel_OnItemHeightOrWidthPropertyChanged_InvalidValue",
                    value);
                throw new ArgumentException(message, "value");
            }

            // The length properties affect measuring.
            source.InvalidateMeasure();
        }

        /// <summary>
        /// Measures the child elements of a
        /// <see cref="T:System.Windows.Controls.WrapPanel" /> in anticipation
        /// of arranging them during the
        /// <see cref="M:System.Windows.Controls.WrapPanel.ArrangeOverride(System.Windows.Size)" />
        /// pass.
        /// </summary>
        /// <param name="constraint">
        /// The size available to child elements of the wrap panel.
        /// </param>
        /// <returns>
        /// The size required by the
        /// <see cref="T:System.Windows.Controls.WrapPanel" /> and its 
        /// elements.
        /// </returns>
        [SuppressMessage("Microsoft.Naming", "CA1725:ParameterNamesShouldMatchBaseDeclaration", MessageId = "0#", Justification = "Compat with WPF.")]
        protected override Size MeasureOverride(Size constraint)
        {
            var inline = Children.OfType<TagsTextBoxFooter>().FirstOrDefault();
            if (inline == null)
            {
                inline = new TagsTextBoxFooter();
                Children.Add(inline);

                if (_textBox == null)
                    _textBox = this.Ancestors<TagsTextBox>().FirstOrDefault() as TagsTextBox;

                if (_textBox != null)
                    _textBox.Initialize(inline);
            }

            // Variables tracking the size of the current line, the total size
            // measured so far, and the maximum size available to fill.  Note
            // that the line might represent a row or a column depending on the
            // orientation.
            Orientation o = Orientation.Horizontal;
            OrientedSize lineSize = new OrientedSize(o);
            OrientedSize totalSize = new OrientedSize(o);
            OrientedSize maximumSize = new OrientedSize(o, constraint.Width, constraint.Height);

            // Determine the constraints for individual items
            double itemWidth = double.NaN;
            double itemHeight = double.NaN;
            bool hasFixedWidth = !itemWidth.IsNaN();
            bool hasFixedHeight = !itemHeight.IsNaN();
            Size itemSize = new Size(
                hasFixedWidth ? itemWidth : constraint.Width,
                hasFixedHeight ? itemHeight : constraint.Height);

            // Measure each of the Children
            foreach (UIElement element in Children)
            {
                if (element == Children.Last())
                {
                    var desiredSize = constraint.Width - lineSize.Width;
                    if (desiredSize < (element as TextBox).MinWidth)
                    {
                        (element as TextBox).Width = constraint.Width;
                    }
                    else
                    {
                        (element as TextBox).Width = desiredSize;
                    }
                }

                // Determine the size of the element
                element.Measure(itemSize);
                OrientedSize elementSize = new OrientedSize(
                    o,
                    hasFixedWidth ? itemWidth : element.DesiredSize.Width,
                    hasFixedHeight ? itemHeight : element.DesiredSize.Height);

                // If this element falls of the edge of the line
                if (NumericExtensions.IsGreaterThan(lineSize.Direct + elementSize.Direct, maximumSize.Direct))
                {
                    // Update the total size with the direct and indirect growth
                    // for the current line
                    totalSize.Direct = Math.Max(lineSize.Direct, totalSize.Direct);
                    totalSize.Indirect += lineSize.Indirect;

                    // Move the element to a new line
                    lineSize = elementSize;

                    // If the current element is larger than the maximum size,
                    // place it on a line by itself
                    if (NumericExtensions.IsGreaterThan(elementSize.Direct, maximumSize.Direct))
                    {
                        // Update the total size for the line occupied by this
                        // single element
                        totalSize.Direct = Math.Max(elementSize.Direct, totalSize.Direct);
                        totalSize.Indirect += elementSize.Indirect;

                        // Move to a new line
                        lineSize = new OrientedSize(o);
                    }
                }
                else
                {
                    // Otherwise just add the element to the end of the line
                    lineSize.Direct += elementSize.Direct;
                    lineSize.Indirect = Math.Max(lineSize.Indirect, elementSize.Indirect);
                }
            }

            // Update the total size with the elements on the last line
            totalSize.Direct = Math.Max(lineSize.Direct, totalSize.Direct);
            totalSize.Indirect += lineSize.Indirect;

            // Return the total size required as an un-oriented quantity
            return new Size(totalSize.Width, totalSize.Height);
        }

        /// <summary>
        /// Arranges and sizes the
        /// <see cref="T:System.Windows.Controls.WrapPanel" /> control and its
        /// child elements.
        /// </summary>
        /// <param name="finalSize">
        /// The area within the parent that the
        /// <see cref="T:System.Windows.Controls.WrapPanel" /> should use 
        /// arrange itself and its children.
        /// </param>
        /// <returns>
        /// The actual size used by the
        /// <see cref="T:System.Windows.Controls.WrapPanel" />.
        /// </returns>
        protected override Size ArrangeOverride(Size finalSize)
        {
            // Variables tracking the size of the current line, and the maximum
            // size available to fill.  Note that the line might represent a row
            // or a column depending on the orientation.
            Orientation o = Orientation.Horizontal;
            OrientedSize lineSize = new OrientedSize(o);
            OrientedSize maximumSize = new OrientedSize(o, finalSize.Width, finalSize.Height);

            // Determine the constraints for individual items
            double itemWidth = double.NaN;
            double itemHeight = double.NaN;
            bool hasFixedWidth = !itemWidth.IsNaN();
            bool hasFixedHeight = !itemHeight.IsNaN();
            double indirectOffset = 0;
            double? directDelta = (o == Orientation.Horizontal) ?
                (hasFixedWidth ? (double?)itemWidth : null) :
                (hasFixedHeight ? (double?)itemHeight : null);

            // Measure each of the Children.  We will process the elements one
            // line at a time, just like during measure, but we will wait until
            // we've completed an entire line of elements before arranging them.
            // The lineStart and lineEnd variables track the size of the
            // currently arranged line.
            UIElementCollection children = Children;
            int count = children.Count;
            int lineStart = 0;
            for (int lineEnd = 0; lineEnd < count; lineEnd++)
            {
                UIElement element = children[lineEnd];

                if (element == Children.Last())
                {
                    var desiredSize = finalSize.Width - lineSize.Width;
                    if (desiredSize < (element as TextBox).MinWidth)
                    {
                        (element as TextBox).Width = finalSize.Width;
                    }
                    else
                    {
                        (element as TextBox).Width = desiredSize;
                    }
                }

                // Get the size of the element
                OrientedSize elementSize = new OrientedSize(
                    o,
                    hasFixedWidth ? itemWidth : element.DesiredSize.Width,
                    hasFixedHeight ? itemHeight : element.DesiredSize.Height);

                // If this element falls of the edge of the line
                if (NumericExtensions.IsGreaterThan(lineSize.Direct + elementSize.Direct, maximumSize.Direct))
                {
                    // Then we just completed a line and we should arrange it
                    ArrangeLine(lineStart, lineEnd, directDelta, indirectOffset, lineSize.Indirect);

                    // Move the current element to a new line
                    indirectOffset += lineSize.Indirect;
                    lineSize = elementSize;

                    // If the current element is larger than the maximum size
                    if (NumericExtensions.IsGreaterThan(elementSize.Direct, maximumSize.Direct))
                    {
                        // Arrange the element as a single line
                        ArrangeLine(lineEnd, ++lineEnd, directDelta, indirectOffset, elementSize.Indirect);

                        // Move to a new line
                        indirectOffset += lineSize.Indirect;
                        lineSize = new OrientedSize(o);
                    }

                    // Advance the start index to a new line after arranging
                    lineStart = lineEnd;
                }
                else
                {
                    // Otherwise just add the element to the end of the line
                    lineSize.Direct += elementSize.Direct;
                    lineSize.Indirect = Math.Max(lineSize.Indirect, elementSize.Indirect);
                }
            }

            // Arrange any elements on the last line
            if (lineStart < count)
            {
                ArrangeLine(lineStart, count, directDelta, indirectOffset, lineSize.Indirect);
            }

            return finalSize;
        }

        /// <summary>
        /// Arrange a sequence of elements in a single line.
        /// </summary>
        /// <param name="lineStart">
        /// Index of the first element in the sequence to arrange.
        /// </param>
        /// <param name="lineEnd">
        /// Index of the last element in the sequence to arrange.
        /// </param>
        /// <param name="directDelta">
        /// Optional fixed growth in the primary direction.
        /// </param>
        /// <param name="indirectOffset">
        /// Offset of the line in the indirect direction.
        /// </param>
        /// <param name="indirectGrowth">
        /// Shared indirect growth of the elements on this line.
        /// </param>
        private void ArrangeLine(int lineStart, int lineEnd, double? directDelta, double indirectOffset, double indirectGrowth)
        {
            double directOffset = 0.0;

            Orientation o = Orientation.Horizontal;

            UIElementCollection children = Children;
            for (int index = lineStart; index < lineEnd; index++)
            {
                // Get the size of the element
                UIElement element = children[index];
                OrientedSize elementSize = new OrientedSize(o, element.DesiredSize.Width, element.DesiredSize.Height);

                // Determine if we should use the element's desired size or the
                // fixed item width or height
                double directGrowth = directDelta != null ?
                    directDelta.Value :
                    elementSize.Direct;

                // Arrange the element
                Rect bounds = new Rect(directOffset, indirectOffset, directGrowth, indirectGrowth);
                element.Arrange(bounds);

                directOffset += directGrowth;
            }
        }
    }

    /// <summary>
    /// The OrientedSize structure is used to abstract the growth direction from
    /// the layout algorithms of WrapPanel.  When the growth direction is
    /// oriented horizontally (ex: the next element is arranged on the side of
    /// the previous element), then the Width grows directly with the placement
    /// of elements and Height grows indirectly with the size of the largest
    /// element in the row.  When the orientation is reversed, so is the
    /// directional growth with respect to Width and Height.
    /// </summary>
    /// <QualityBand>Mature</QualityBand>
    [StructLayout(LayoutKind.Sequential)]
    internal struct OrientedSize
    {
        /// <summary>
        /// The orientation of the structure.
        /// </summary>
        private Orientation _orientation;

        /// <summary>
        /// Gets the orientation of the structure.
        /// </summary>
        public Orientation Orientation
        {
            get { return _orientation; }
        }

        /// <summary>
        /// The size dimension that grows directly with layout placement.
        /// </summary>
        private double _direct;

        /// <summary>
        /// Gets or sets the size dimension that grows directly with layout
        /// placement.
        /// </summary>
        public double Direct
        {
            get { return _direct; }
            set { _direct = value; }
        }

        /// <summary>
        /// The size dimension that grows indirectly with the maximum value of
        /// the layout row or column.
        /// </summary>
        private double _indirect;

        /// <summary>
        /// Gets or sets the size dimension that grows indirectly with the
        /// maximum value of the layout row or column.
        /// </summary>
        public double Indirect
        {
            get { return _indirect; }
            set { _indirect = value; }
        }

        /// <summary>
        /// Gets or sets the width of the size.
        /// </summary>
        public double Width
        {
            get
            {
                return (Orientation == Orientation.Horizontal) ?
                    Direct :
                    Indirect;
            }
            set
            {
                if (Orientation == Orientation.Horizontal)
                {
                    Direct = value;
                }
                else
                {
                    Indirect = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the height of the size.
        /// </summary>
        public double Height
        {
            get
            {
                return (Orientation != Orientation.Horizontal) ?
                    Direct :
                    Indirect;
            }
            set
            {
                if (Orientation != Orientation.Horizontal)
                {
                    Direct = value;
                }
                else
                {
                    Indirect = value;
                }
            }
        }

        /// <summary>
        /// Initializes a new OrientedSize structure.
        /// </summary>
        /// <param name="orientation">Orientation of the structure.</param>
        public OrientedSize(Orientation orientation) :
            this(orientation, 0.0, 0.0)
        {
        }

        /// <summary>
        /// Initializes a new OrientedSize structure.
        /// </summary>
        /// <param name="orientation">Orientation of the structure.</param>
        /// <param name="width">Un-oriented width of the structure.</param>
        /// <param name="height">Un-oriented height of the structure.</param>
        public OrientedSize(Orientation orientation, double width, double height)
        {
            _orientation = orientation;

            // All fields must be initialized before we access the this pointer
            _direct = 0.0;
            _indirect = 0.0;

            Width = width;
            Height = height;
        }
    }

    public static class NumericExtensions
    {
        /// <summary>
        /// NanUnion is a C++ style type union used for efficiently converting
        /// a double into an unsigned long, whose bits can be easily
        /// manipulated.
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        private struct NanUnion
        {
            /// <summary>
            /// Floating point representation of the union.
            /// </summary>
            [SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields", Justification = "It is accessed through the other member of the union")]
            [FieldOffset(0)]
            internal double FloatingValue;

            /// <summary>
            /// Integer representation of the union.
            /// </summary>
            [FieldOffset(0)]
            internal ulong IntegerValue;
        }

        /// <summary>
        /// Check if a number isn't really a number.
        /// </summary>
        /// <param name="value">The number to check.</param>
        /// <returns>
        /// True if the number is not a number, false if it is a number.
        /// </returns>
        public static bool IsNaN(this double value)
        {
            // Get the double as an unsigned long
            NanUnion union = new NanUnion { FloatingValue = value };

            // An IEEE 754 double precision floating point number is NaN if its
            // exponent equals 2047 and it has a non-zero mantissa.
            ulong exponent = union.IntegerValue & 0xfff0000000000000L;
            if ((exponent != 0x7ff0000000000000L) && (exponent != 0xfff0000000000000L))
            {
                return false;
            }
            ulong mantissa = union.IntegerValue & 0x000fffffffffffffL;
            return mantissa != 0L;
        }

        /// <summary>
        /// Determine if one number is greater than another.
        /// </summary>
        /// <param name="left">First number.</param>
        /// <param name="right">Second number.</param>
        /// <returns>
        /// True if the first number is greater than the second, false
        /// otherwise.
        /// </returns>
        public static bool IsGreaterThan(double left, double right)
        {
            return (left > right) && !AreClose(left, right);
        }

        /// <summary>
        /// Determine if two numbers are close in value.
        /// </summary>
        /// <param name="left">First number.</param>
        /// <param name="right">Second number.</param>
        /// <returns>
        /// True if the first number is close in value to the second, false
        /// otherwise.
        /// </returns>
        public static bool AreClose(double left, double right)
        {
            if (left == right)
            {
                return true;
            }

            double a = (Math.Abs(left) + Math.Abs(right) + 10.0) * 2.2204460492503131E-16;
            double b = left - right;
            return (-a < b) && (a > b);
        }
    }
}
