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
    /// The brush size, as a wedge along the left edge of the canvas: a thin line until the pointer
    /// reaches it, then a track that widens towards the top. Dragging is relative, so grabbing it
    /// anywhere does not make the size jump.
    /// </summary>
    public partial class PencilWeightSlider : Control
    {
        private const double BigRadius = 16;
        private const double SmallRadius = 3;
        private const double IdleRadius = 12;

        // How far the track moves into the canvas once expanded.
        private const double Inset = 32;
        private const double Duration = 200;

        private Canvas LayoutRoot;
        private Rectangle HitArea;
        private Path Track;
        private Ellipse Thumb;
        private TranslateTransform PanelTransform;

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
        private double _startY;

        private double _top;
        private double _height;

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
            Track = GetTemplateChild(nameof(Track)) as Path;
            Thumb = GetTemplateChild(nameof(Thumb)) as Ellipse;
            PanelTransform = GetTemplateChild(nameof(PanelTransform)) as TranslateTransform;

            LayoutRoot.SizeChanged += OnSizeChanged;
            ArrangeParts(LayoutRoot.ActualHeight);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            StopAnimation();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            ArrangeParts(e.NewSize.Height);
        }

        private void ArrangeParts(double height)
        {
            if (LayoutRoot == null)
            {
                return;
            }

            _height = Math.Round(height * 0.3);
            _top = Math.Round((height - _height) / 2);

            Canvas.SetTop(HitArea, _top);
            HitArea.Height = _height;

            Canvas.SetTop(Track, _top);

            UpdateVisuals();
        }

        // Centred on x = 0, with round ends of the given radii at the top and the bottom.
        private static Geometry CreateTrack(double height, double top, double bottom)
        {
            var figure = new PathFigure
            {
                StartPoint = new Point(-top, top),
                IsClosed = true
            };

            figure.Segments.Add(new ArcSegment
            {
                Point = new Point(top, top),
                Size = new Size(top, top),
                SweepDirection = SweepDirection.Clockwise
            });

            figure.Segments.Add(new LineSegment { Point = new Point(bottom, height - bottom) });

            figure.Segments.Add(new ArcSegment
            {
                Point = new Point(-bottom, height - bottom),
                Size = new Size(bottom, bottom),
                SweepDirection = SweepDirection.Clockwise
            });

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            return geometry;
        }

        private void UpdateVisuals()
        {
            if (Thumb == null || _height == 0)
            {
                return;
            }

            var p = _progress;
            var value = (_value - _minimum) / (_maximum - _minimum);

            Track.Data = CreateTrack(_height, Lerp(SmallRadius, BigRadius, p), SmallRadius);
            PanelTransform.X = Inset * p;

            var radius = Lerp(IdleRadius, Lerp(SmallRadius * 1.5, BigRadius, value), p);
            var y = Math.Clamp(_top + _height * (1 - value), _top + BigRadius, _top + _height - SmallRadius * 1.5);

            Thumb.Width = radius * 2;
            Thumb.Height = radius * 2;

            Canvas.SetLeft(Thumb, -radius);
            Canvas.SetTop(Thumb, y - radius);
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
                _startY = e.GetCurrentPoint(this).Position.Y;

                SetExpanded(true);
                DragStarted?.Invoke(this, EventArgs.Empty);
            }

            e.Handled = true;
        }

        protected override void OnPointerMoved(PointerRoutedEventArgs e)
        {
            base.OnPointerMoved(e);

            if (_pointerId != e.Pointer.PointerId || _height == 0)
            {
                return;
            }

            var y = e.GetCurrentPoint(this).Position.Y;
            ChangeValue(_startValue + (_startY - y) / _height * (_maximum - _minimum));
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
