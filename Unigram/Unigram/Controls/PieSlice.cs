using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Unigram.Controls
{
    public class PieSlice : Path
    {
        private bool m_HasLoaded = false;
        public PieSlice()
        {
            Loaded += (s, e) =>
            {
                m_HasLoaded = true;
                UpdatePath();
            };
        }

        // StartAngle
        public static readonly DependencyProperty StartAngleProperty
            = DependencyProperty.Register("StartAngle", typeof(double), typeof(PieSlice),
            new PropertyMetadata(DependencyProperty.UnsetValue, (s, e) => { Changed(s as PieSlice); }));
        public double StartAngle
        {
            get { return (double)GetValue(StartAngleProperty); }
            set { SetValue(StartAngleProperty, value); }
        }

        // Angle
        public static readonly DependencyProperty AngleProperty
            = DependencyProperty.Register("Angle", typeof(double), typeof(PieSlice),
            new PropertyMetadata(DependencyProperty.UnsetValue, (s, e) => { Changed(s as PieSlice); }));
        public double Angle
        {
            get { return (double)GetValue(AngleProperty); }
            set { SetValue(AngleProperty, value); }
        }

        // Radius
        public static readonly DependencyProperty RadiusProperty
            = DependencyProperty.Register("Radius", typeof(double), typeof(PieSlice),
            new PropertyMetadata(DependencyProperty.UnsetValue, (s, e) => { Changed(s as PieSlice); }));
        public double Radius
        {
            get { return (double)GetValue(RadiusProperty); }
            set { SetValue(RadiusProperty, value); }
        }

        private static void Changed(PieSlice pieSlice)
        {
            if (pieSlice.m_HasLoaded)
                pieSlice.UpdatePath();
        }

        public void UpdatePath()
        {
            // ensure variables
            if (GetValue(StartAngleProperty) == DependencyProperty.UnsetValue)
                throw new ArgumentNullException("Start Angle is required");
            if (GetValue(RadiusProperty) == DependencyProperty.UnsetValue)
                throw new ArgumentNullException("Radius is required");
            if (GetValue(AngleProperty) == DependencyProperty.UnsetValue)
                throw new ArgumentNullException("Angle is required");

            Width = Height = 2 * (Radius + StrokeThickness);
            var endAngle = StartAngle + Angle;

            var figure = new PathFigure
            {
                StartPoint = new Point(Radius, Radius),
                IsClosed = true,
            };

            var lineX = Radius + Math.Sin(StartAngle * Math.PI / 180) * Radius;
            var lineY = Radius - Math.Cos(StartAngle * Math.PI / 180) * Radius;
            var line = new LineSegment { Point = new Point(lineX, lineY) };
            figure.Segments.Add(line);

            var arcX = Radius + Math.Sin(endAngle * Math.PI / 180) * Radius;
            var arcY = Radius - Math.Cos(endAngle * Math.PI / 180) * Radius;
            var arc = new ArcSegment
            {
                IsLargeArc = Angle >= 180.0,
                Point = new Point(arcX, arcY),
                Size = new Size(Radius, Radius),
                SweepDirection = SweepDirection.Clockwise,
            };
            figure.Segments.Add(arc);

            Data = new PathGeometry { Figures = { figure } };
            InvalidateArrange();
        }
    }
}
