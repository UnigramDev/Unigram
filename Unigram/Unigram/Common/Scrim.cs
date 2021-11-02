using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.Foundation;
using Windows.UI;

namespace Unigram.Common
{
    public static class Scrim
    {
        public static CubicBezierGradient GetGradient(DependencyObject obj)
        {
            return (CubicBezierGradient)obj.GetValue(GradientProperty);
        }

        public static void SetGradient(DependencyObject obj, CubicBezierGradient value)
        {
            obj.SetValue(GradientProperty, value);
        }

        public static readonly DependencyProperty GradientProperty =
            DependencyProperty.RegisterAttached("Gradient", typeof(CubicBezierGradient), typeof(LinearGradientBrush), new PropertyMetadata(null, OnGradientChanged));

        private static void OnGradientChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not LinearGradientBrush linear)
            {
                return;
            }

            if (e.NewValue is CubicBezierGradient gradient)
            {
                gradient.Apply(linear);
            }
            else
            {
                linear.GradientStops.Clear();
            }
        }
    }

    public class CubicBezierGradient : DependencyObject
    {
        #region TopColor

        public SolidColorBrush TopColor
        {
            get => (SolidColorBrush)GetValue(TopColorProperty);
            set => SetValue(TopColorProperty, value);
        }

        public static readonly DependencyProperty TopColorProperty =
            DependencyProperty.Register("TopColor", typeof(Brush), typeof(CubicBezierGradient), new PropertyMetadata(null, OnColorChanged));

        private static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((CubicBezierGradient)d).Update();
        }

        #endregion

        #region BottomColor

        public SolidColorBrush BottomColor
        {
            get => (SolidColorBrush)GetValue(BottomColorProperty);
            set => SetValue(BottomColorProperty, value);
        }

        public static readonly DependencyProperty BottomColorProperty =
            DependencyProperty.Register("BottomColor", typeof(Brush), typeof(CubicBezierGradient), new PropertyMetadata(null, OnColorChanged));

        #endregion

        public double TopOpacity { get; set; } = 1;
        public double BottomOpacity { get; set; } = 1;

        public Point ControlPoint1 { get; set; } = new Point(.30, 0);
        public Point ControlPoint2 { get; set; } = new Point(.58, 1);

        private LinearGradientBrush _target;

        public CubicBezierGradient()
        {

        }

        public CubicBezierGradient(SolidColorBrush top, double topOpacity, SolidColorBrush bottom, double bottomOpacity)
        {
            TopColor = top;
            BottomColor = bottom;

            TopOpacity = topOpacity;
            BottomOpacity = bottomOpacity;
        }

        public void Apply(LinearGradientBrush target)
        {
            _target = target;
            Update();
        }

        private void Update()
        {
            if (_target == null)
            {
                return;
            }

            _target.GradientStops.Clear();

            foreach (var stop in GetGradientStops())
            {
                _target.GradientStops.Add(stop);
            }
        }

        public GradientStop[] GetGradientStops()
        {
            if (TopColor == null || BottomColor == null)
            {
                return new GradientStop[0];
            }

            var top = TopColor.Color;
            var bottom = BottomColor.Color;

            if (top == bottom)
            {
                top.A = (byte)(255 * TopOpacity);
                bottom.A = (byte)(255 * BottomOpacity);
            }

            return GetGradientStops(new[] { top, bottom }, GetCoordinates(ControlPoint1, ControlPoint2));
        }

        private GradientStop[] GetGradientStops(Color[] colors, Point[] coordinates)
        {
            var colorStops = new GradientStop[coordinates.Length];

            for (int i = 0; i < coordinates.Length; i++)
            {
                colorStops[i] = new GradientStop
                {
                    Color = Mix(colors[0], colors[1], coordinates[i].Y),
                    Offset = coordinates[i].X
                };
            }

            return colorStops;
        }

        private Color Mix(Color x, Color y, double amount)
        {
            static double Mix(double x, double y, double f)
            {
                return Math.Sqrt(Math.Pow(x, 2) * (1 - f) + Math.Pow(y, 2) * f);
            }

            return Color.FromArgb(
                (byte)Mix(x.A, y.A, amount),
                (byte)Mix(x.R, y.R, amount),
                (byte)Mix(x.G, y.G, amount),
                (byte)Mix(x.B, y.B, amount));
        }

        private Point[] GetCoordinates(Point p1, Point p2, int polySteps = 15)
        {
            var increment = 1d / polySteps;
            var coordinates = new Point[polySteps + 1];
            var index = 0;

            for (double i = 0; i <= 1; i += increment)
            {
                coordinates[index++] = new Point(GetBezier(i, p1.X, p2.X), GetBezier(i, p1.Y, p2.Y));
            }

            return coordinates;
        }

        private double GetBezier(double t, double n1, double n2)
        {
            return Math.Round(
              (1 - t) * (1 - t) * (1 - t) * 0 +
              3 * ((1 - t) * (1 - t)) * t * n1 +
              3 * (1 - t) * (t * t) * n2 +
              t * t * t * 1, 10);
        }
    }
}
