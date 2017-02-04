using System;
using System.ComponentModel;
using System.Windows;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Unigram.Controls
{
    /// <summary>
    /// A Path that represents a ring slice with a given
    /// (outer) Radius,
    /// InnerRadius,
    /// StartAngle,
    /// EndAngle and
    /// Center.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ProgressBarRingSlice : Path
    {
        private bool _isUpdating;

        #region StartAngle
        /// <summary>
        /// The start angle property.
        /// </summary>
        public static readonly DependencyProperty StartAngleProperty =
            DependencyProperty.Register(
                "StartAngle",
                typeof(double),
                typeof(ProgressBarRingSlice),
                new PropertyMetadata(
                    0d,
                    OnStartAngleChanged));

        /// <summary>
        /// Gets or sets the start angle.
        /// </summary>
        /// <value>
        /// The start angle.
        /// </value>
        public double StartAngle
        {
            get { return (double)GetValue(StartAngleProperty); }
            set { SetValue(StartAngleProperty, value); }
        }

        private static void OnStartAngleChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var target = (ProgressBarRingSlice)sender;
            var oldStartAngle = (double)e.OldValue;
            var newStartAngle = (double)e.NewValue;
            target.OnStartAngleChanged(oldStartAngle, newStartAngle);
        }

        private void OnStartAngleChanged(double oldStartAngle, double newStartAngle)
        {
            UpdatePath();
        }
        #endregion

        #region EndAngle
        /// <summary>
        /// The end angle property.
        /// </summary>
        public static readonly DependencyProperty EndAngleProperty =
            DependencyProperty.Register(
                "EndAngle",
                typeof(double),
                typeof(ProgressBarRingSlice),
                new PropertyMetadata(
                    0d,
                    OnEndAngleChanged));

        /// <summary>
        /// Gets or sets the end angle.
        /// </summary>
        /// <value>
        /// The end angle.
        /// </value>
        public double EndAngle
        {
            get { return (double)GetValue(EndAngleProperty); }
            set { SetValue(EndAngleProperty, value); }
        }

        private static void OnEndAngleChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var target = (ProgressBarRingSlice)sender;
            var oldEndAngle = (double)e.OldValue;
            var newEndAngle = (double)e.NewValue;
            target.OnEndAngleChanged(oldEndAngle, newEndAngle);
        }

        private void OnEndAngleChanged(double oldEndAngle, double newEndAngle)
        {
            UpdatePath();
        }
        #endregion

        #region Radius
        /// <summary>
        /// The radius property
        /// </summary>
        public static readonly DependencyProperty RadiusProperty =
            DependencyProperty.Register(
                "Radius",
                typeof(double),
                typeof(ProgressBarRingSlice),
                new PropertyMetadata(
                    0d,
                    OnRadiusChanged));

        /// <summary>
        /// Gets or sets the outer radius.
        /// </summary>
        /// <value>
        /// The outer radius.
        /// </value>
        public double Radius
        {
            get { return (double)GetValue(RadiusProperty); }
            set { SetValue(RadiusProperty, value); }
        }

        private static void OnRadiusChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var target = (ProgressBarRingSlice)sender;
            var oldRadius = (double)e.OldValue;
            var newRadius = (double)e.NewValue;
            target.OnRadiusChanged(oldRadius, newRadius);
        }

        private void OnRadiusChanged(double oldRadius, double newRadius)
        {
            Width = Height = 2 * Radius;
            UpdatePath();
        }
        #endregion

        #region InnerRadius
        /// <summary>
        /// The inner radius property
        /// </summary>
        public static readonly DependencyProperty InnerRadiusProperty =
            DependencyProperty.Register(
                "InnerRadius",
                typeof(double),
                typeof(ProgressBarRingSlice),
                new PropertyMetadata(
                    0d,
                    OnInnerRadiusChanged));

        /// <summary>
        /// Gets or sets the inner radius.
        /// </summary>
        /// <value>
        /// The inner radius.
        /// </value>
        public double InnerRadius
        {
            get { return (double)GetValue(InnerRadiusProperty); }
            set { SetValue(InnerRadiusProperty, value); }
        }

        private static void OnInnerRadiusChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var target = (ProgressBarRingSlice)sender;
            var oldInnerRadius = (double)e.OldValue;
            var newInnerRadius = (double)e.NewValue;
            target.OnInnerRadiusChanged(oldInnerRadius, newInnerRadius);
        }

        private void OnInnerRadiusChanged(double oldInnerRadius, double newInnerRadius)
        {
            if (newInnerRadius < 0)
            {
                throw new ArgumentException("InnerRadius can't be a negative value.", "InnerRadius");
            }

            UpdatePath();
        }
        #endregion

        #region Center
        /// <summary>
        /// Center Dependency Property
        /// </summary>
        public static readonly DependencyProperty CenterProperty =
            DependencyProperty.Register(
                "Center",
                typeof(Point?),
                typeof(ProgressBarRingSlice),
                new PropertyMetadata(null, OnCenterChanged));

        /// <summary>
        /// Gets or sets the Center property. This dependency property 
        /// indicates the center point.
        /// Center point is calculated based on Radius and StrokeThickness if not specified.    
        /// </summary>
        public Point? Center
        {
            get { return (Point?)GetValue(CenterProperty); }
            set { SetValue(CenterProperty, value); }
        }

        /// <summary>
        /// Handles changes to the Center property.
        /// </summary>
        /// <param name="d">
        /// The <see cref="DependencyObject"/> on which
        /// the property has changed value.
        /// </param>
        /// <param name="e">
        /// Event data that is issued by any event that
        /// tracks changes to the effective value of this property.
        /// </param>
        private static void OnCenterChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var target = (ProgressBarRingSlice)d;
            Point? oldCenter = (Point?)e.OldValue;
            Point? newCenter = target.Center;
            target.OnCenterChanged(oldCenter, newCenter);
        }

        /// <summary>
        /// Provides derived classes an opportunity to handle changes
        /// to the Center property.
        /// </summary>
        /// <param name="oldCenter">The old Center value</param>
        /// <param name="newCenter">The new Center value</param>
        private void OnCenterChanged(
            Point? oldCenter, Point? newCenter)
        {
            UpdatePath();
        }
        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="ProgressBarRingSlice" /> class.
        /// </summary>
        public ProgressBarRingSlice()
        {
            SizeChanged += OnSizeChanged;
            //new PropertyChangeEventSource<double>(
            //    this, "StrokeThickness", BindingMode.OneWay).ValueChanged +=
            //    OnStrokeThicknessChanged;
        }

        private void OnStrokeThicknessChanged(object sender, double e)
        {
            UpdatePath();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs sizeChangedEventArgs)
        {
            UpdatePath();
        }

        /// <summary>
        /// Suspends path updates until EndUpdate is called;
        /// </summary>
        public void BeginUpdate()
        {
            _isUpdating = true;
        }

        /// <summary>
        /// Resumes immediate path updates every time a component property value changes. Updates the path.
        /// </summary>
        public void EndUpdate()
        {
            _isUpdating = false;
            UpdatePath();
        }

        private void UpdatePath()
        {
            var innerRadius = InnerRadius + StrokeThickness / 2;
            var outerRadius = Radius - StrokeThickness / 2;

            if (_isUpdating ||
                ActualWidth == 0 ||
                innerRadius <= 0 ||
                outerRadius < innerRadius)
            {
                return;
            }

            var center =
                Center ??
                new Point(
                    outerRadius + StrokeThickness / 2,
                    outerRadius + StrokeThickness / 2);

            if (EndAngle > 0 && EndAngle < 360)
            {
                var pathGeometry = new PathGeometry();
                var pathFigure = new PathFigure();
                pathFigure.IsClosed = false;

                // Starting Point
                pathFigure.StartPoint =
                    new Point(
                        center.X + Math.Sin(StartAngle * Math.PI / 180) * innerRadius,
                        center.Y - Math.Cos(StartAngle * Math.PI / 180) * innerRadius);

                // Inner Arc
                var innerArcSegment = new ArcSegment();
                innerArcSegment.IsLargeArc = (EndAngle - StartAngle) >= 180.0;
                innerArcSegment.Point =
                    new Point(
                        center.X + Math.Sin(EndAngle * Math.PI / 180) * innerRadius,
                        center.Y - Math.Cos(EndAngle * Math.PI / 180) * innerRadius);
                innerArcSegment.Size = new Size(innerRadius, innerRadius);
                innerArcSegment.SweepDirection = SweepDirection.Clockwise;

                var lineSegment =
                    new LineSegment
                    {
                        Point = new Point(
                            center.X + Math.Sin(EndAngle * Math.PI / 180) * outerRadius,
                            center.Y - Math.Cos(EndAngle * Math.PI / 180) * outerRadius)
                    };

                // Outer Arc
                var outerArcSegment = new ArcSegment();
                outerArcSegment.IsLargeArc = (EndAngle - StartAngle) >= 180.0;
                outerArcSegment.Point =
                    new Point(
                            center.X + Math.Sin(StartAngle * Math.PI / 180) * outerRadius,
                            center.Y - Math.Cos(StartAngle * Math.PI / 180) * outerRadius);
                outerArcSegment.Size = new Size(outerRadius, outerRadius);
                outerArcSegment.SweepDirection = SweepDirection.Counterclockwise;

                pathFigure.Segments.Add(innerArcSegment);
                //pathFigure.Segments.Add(lineSegment);
                //pathFigure.Segments.Add(outerArcSegment);
                pathGeometry.Figures.Add(pathFigure);

                InvalidateArrange();
                Data = pathGeometry;
            }
            else if (EndAngle == 360)
            {
                var geometry = new GeometryGroup();
                geometry.FillRule = FillRule.EvenOdd;

                var innerSegment = new EllipseGeometry();
                innerSegment.Center = center;
                innerSegment.RadiusX = innerRadius;
                innerSegment.RadiusY = innerRadius;

                var outerSegment = new EllipseGeometry();
                outerSegment.Center = center;
                outerSegment.RadiusX = outerRadius;
                outerSegment.RadiusY = outerRadius;

                geometry.Children.Add(innerSegment);
                geometry.Children.Add(outerSegment);

                InvalidateArrange();
                Data = geometry;

                return;
            }
        }
    }
}