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
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls
{
    /// <summary>
    /// The brush size, as a wedge in the toolbar: a thin line until the pointer reaches it, then a
    /// track that widens towards the right. Dragging is relative, so grabbing it anywhere does not
    /// make the size jump.
    /// </summary>
    public partial class PencilWeightSlider : Control
    {
        private const double BigRadius = 14;
        private const double SmallRadius = 3;
        private const double IdleRadius = 9;

        // Room at each end, so that the thumb's largest size fits inside the control.
        private const double EndRoom = BigRadius;
        private const double Duration = 200;

        private Canvas LayoutRoot;
        private Rectangle HitArea;
        private XamlPath Track;
        private Ellipse Thumb;

        private double _minimum = 0.05;
        private double _maximum = 1;
        private double _value = 0.25;

        private bool _expanded;
        private bool _hovered;

        // From the idle line (0) to the full wedge (1). Track, thumb and position all follow it, so
        // the line grows into the wedge rather than one fading into the other.
        private double _progress;
        private double _progressFrom;
        private TimeSpan? _animationStart;
        private bool _animating;

        private uint? _pointerId;
        private double _startValue;
        private double _startX;

        private double _left;
        private double _width;
        private double _middle;

        public PencilWeightSlider()
        {
            DefaultStyleKey = typeof(PencilWeightSlider);
            Unloaded += OnUnloaded;
        }

        public event EventHandler ValueChanged;
        public event EventHandler DragStarted;
        public event EventHandler DragCompleted;

        public double Minimum => _minimum;
        public double Maximum => _maximum;

        public double Value
        {
            get => _value;
            set
            {
                _value = Math.Clamp(value, _minimum, _maximum);
                UpdateVisuals();
            }
        }

        public void SetRange(double minimum, double maximum, double value)
        {
            _minimum = minimum;
            _maximum = maximum;
            Value = value;
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            LayoutRoot = GetTemplateChild(nameof(LayoutRoot)) as Canvas;
            HitArea = GetTemplateChild(nameof(HitArea)) as Rectangle;
            Track = GetTemplateChild(nameof(Track)) as XamlPath;
            Thumb = GetTemplateChild(nameof(Thumb)) as Ellipse;

            LayoutRoot.SizeChanged += OnSizeChanged;
            ArrangeParts(LayoutRoot.ActualWidth, LayoutRoot.ActualHeight);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            StopAnimation();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            ArrangeParts(e.NewSize.Width, e.NewSize.Height);
        }

        private void ArrangeParts(double width, double height)
        {
            if (LayoutRoot == null)
            {
                return;
            }

            _left = EndRoom;
            _width = Math.Max(0, width - EndRoom * 2);
            _middle = Math.Round(height / 2);

            HitArea.Width = width;
            HitArea.Height = height;

            Canvas.SetLeft(Track, _left);
            Canvas.SetTop(Track, _middle);

            UpdateVisuals();
        }

        // Centred on y = 0, with round ends of the given radii at the left and the right.
        private static Geometry CreateTrack(double width, double left, double right)
        {
            var figure = new PathFigure
            {
                StartPoint = new Point(left, -left),
                IsClosed = true
            };

            figure.Segments.Add(new LineSegment { Point = new Point(width - right, -right) });

            figure.Segments.Add(new ArcSegment
            {
                Point = new Point(width - right, right),
                Size = new Size(right, right),
                SweepDirection = SweepDirection.Clockwise
            });

            figure.Segments.Add(new LineSegment { Point = new Point(left, left) });

            figure.Segments.Add(new ArcSegment
            {
                Point = new Point(left, -left),
                Size = new Size(left, left),
                SweepDirection = SweepDirection.Clockwise
            });

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            return geometry;
        }

        private void UpdateVisuals()
        {
            if (Thumb == null || _width == 0)
            {
                return;
            }

            var p = _progress;
            var value = (_value - _minimum) / (_maximum - _minimum);

            var right = Lerp(SmallRadius, BigRadius, p);
            Track.Data = CreateTrack(_width, SmallRadius, right);

            // Between the centres of the track's round ends, where its half-thickness grows
            // linearly from one radius to the other: expanded, the thumb is exactly as wide as
            // the wedge wherever it is, never past either end.
            var x = _left + SmallRadius + (_width - SmallRadius - right) * value;
            var radius = Lerp(IdleRadius, Lerp(SmallRadius, right, value), p);

            Thumb.Width = radius * 2;
            Thumb.Height = radius * 2;

            Canvas.SetLeft(Thumb, x - radius);
            Canvas.SetTop(Thumb, _middle - radius);
        }

        private static double Lerp(double from, double to, double progress)
        {
            return from + (to - from) * progress;
        }

        private void SetExpanded(bool expanded)
        {
            if (_expanded == expanded)
            {
                return;
            }

            _expanded = expanded;

            _progressFrom = _progress;
            _animationStart = null;

            if (!_animating)
            {
                _animating = true;
                CompositionTarget.Rendering += OnRendering;
            }
        }

        private void OnRendering(object sender, object e)
        {
            var time = ((RenderingEventArgs)e).RenderingTime;
            _animationStart ??= time;

            var target = _expanded ? 1 : 0;
            var t = Math.Min(1, (time - _animationStart.Value).TotalMilliseconds / Duration);

            _progress = Lerp(_progressFrom, target, Math.Cos((t + 1) * Math.PI) / 2 + 0.5);
            UpdateVisuals();

            if (t >= 1)
            {
                StopAnimation();
            }
        }

        private void StopAnimation()
        {
            if (_animating)
            {
                _animating = false;
                CompositionTarget.Rendering -= OnRendering;
            }
        }

        #region Input

        protected override void OnPointerEntered(PointerRoutedEventArgs e)
        {
            base.OnPointerEntered(e);

            _hovered = true;
            SetExpanded(true);
        }

        protected override void OnPointerExited(PointerRoutedEventArgs e)
        {
            base.OnPointerExited(e);

            _hovered = false;
            SetExpanded(_pointerId != null);
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (_pointerId == null && CapturePointer(e.Pointer))
            {
                _pointerId = e.Pointer.PointerId;
                _startValue = _value;
                _startX = e.GetCurrentPoint(this).Position.X;

                SetExpanded(true);
                DragStarted?.Invoke(this, EventArgs.Empty);
            }

            e.Handled = true;
        }

        protected override void OnPointerMoved(PointerRoutedEventArgs e)
        {
            base.OnPointerMoved(e);

            if (_pointerId != e.Pointer.PointerId || _width == 0)
            {
                return;
            }

            var x = e.GetCurrentPoint(this).Position.X;
            ChangeValue(_startValue + (x - _startX) / _width * (_maximum - _minimum));
        }

        protected override void OnPointerReleased(PointerRoutedEventArgs e)
        {
            base.OnPointerReleased(e);
            Complete(e);
        }

        protected override void OnPointerCanceled(PointerRoutedEventArgs e)
        {
            base.OnPointerCanceled(e);
            Complete(e);
        }

        protected override void OnPointerCaptureLost(PointerRoutedEventArgs e)
        {
            base.OnPointerCaptureLost(e);
            Complete(e);
        }

        private void Complete(PointerRoutedEventArgs e)
        {
            if (_pointerId == e.Pointer.PointerId)
            {
                _pointerId = null;
                ReleasePointerCapture(e.Pointer);

                SetExpanded(_hovered);
                DragCompleted?.Invoke(this, EventArgs.Empty);
            }
        }

        protected override void OnPointerWheelChanged(PointerRoutedEventArgs e)
        {
            base.OnPointerWheelChanged(e);

            var delta = e.GetCurrentPoint(this).Properties.MouseWheelDelta;
            ChangeValue(_value + delta / 120d * (_maximum - _minimum) / 20);

            e.Handled = true;
        }

        private void ChangeValue(double value)
        {
            value = Math.Clamp(value, _minimum, _maximum);

            if (value != _value)
            {
                _value = value;
                UpdateVisuals();

                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        #endregion
    }
}
