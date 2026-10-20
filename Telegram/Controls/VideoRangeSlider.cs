//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    public partial class VideoRangeSlider : RangeBase
    {
        private bool _pressed;
        private bool _holdable;
        private double _delta;
        private double _distance;
        private double _lastX;
        private FrameworkElement _target;

        // The part of the video the track shows: all of it, or while zoomed, the window
        // around the handle that was held still.
        private double _viewStart = 0;
        private double _viewLength = 1;

        private bool _zoomed;
        private double _zoomStart = 0;
        private double _zoomLength = 1;

        private DispatcherTimer _zoomTimer;

        private bool _animating;
        private TimeSpan? _animationStart;
        private double _fromStart;
        private double _fromLength;

        private double _tempMin = 0;
        private double _tempMax = 1;

        private double _minimum = 0;
        private double _maximum = 1;
        private double _value = 0;

        private double _minLength = 0;
        private double _maxLength = 1;

        private const double THICKNESS = 12;

        private const double ZOOM_DELAY = 500;
        private const double ZOOM_DURATION = 300;

        private ToolTip _toolTip;

        private FrameworkElement BackgroundMinimum;
        private FrameworkElement BackgroundMaximum;
        private FrameworkElement MiddleThumb1;
        private FrameworkElement MinimumThumb;
        private FrameworkElement MaximumThumb;
        private FrameworkElement MiddleThumb2;

        public VideoRangeSlider()
        {
            DefaultStyleKey = typeof(VideoRangeSlider);

            SizeChanged += OnSizeChanged;
            Unloaded += OnUnloaded;
        }

        public event EventHandler<double> MinimumChanged;
        public event EventHandler<double> MaximumChanged;

        // Raised on every frame of a zoom, so that what is drawn under the track can follow it.
        public event EventHandler ViewChanged;

        // How many times shorter the window a held handle zooms into is; 1 never zooms.
        public double ZoomFactor { get; set; } = 1;

        public double ViewStart => _viewStart;
        public double ViewLength => _viewLength;

        // The window the track is zoomed into, or zooming into; the whole video otherwise.
        public double ZoomStart => _zoomed ? _zoomStart : 0;
        public double ZoomLength => _zoomed ? _zoomLength : 1;

        // Where the track draws a value while it shows the window at start and length.
        public double GetPosition(double value, double start, double length)
        {
            return THICKNESS + (value - start) / length * (ActualWidth - THICKNESS * 2);
        }

        private TimeSpan _originalDuration;
        public TimeSpan OriginalDuration
        {
            get => _originalDuration;
            set => SetOriginalDuration(value, TimeSpan.FromSeconds(10));
        }

        public void SetOriginalDuration(TimeSpan duration, TimeSpan maxLength)
        {
            _originalDuration = duration;

            _maxLength = Math.Min(maxLength.TotalSeconds / duration.TotalSeconds, 1);
            _minLength = Math.Min(3 / duration.TotalSeconds, 1);

            _minimum = 0;
            _maximum = _maxLength;

            _value = Math.Clamp(_value, _minimum, _maximum);

            Arrange();
        }

        protected override void OnApplyTemplate()
        {
            BackgroundMinimum = GetTemplateChild(nameof(BackgroundMinimum)) as FrameworkElement;
            BackgroundMaximum = GetTemplateChild(nameof(BackgroundMaximum)) as FrameworkElement;
            MiddleThumb1 = GetTemplateChild(nameof(MiddleThumb1)) as FrameworkElement;
            MinimumThumb = GetTemplateChild(nameof(MinimumThumb)) as FrameworkElement;
            MaximumThumb = GetTemplateChild(nameof(MaximumThumb)) as FrameworkElement;
            MiddleThumb2 = GetTemplateChild(nameof(MiddleThumb2)) as FrameworkElement;

            ToolTipService.SetToolTip(MiddleThumb2, _toolTip = new());
        }

        protected override void OnMaximumChanged(double oldMaximum, double newMaximum)
        {
            _maximum = newMaximum;
            Arrange();
        }

        protected override void OnMinimumChanged(double oldMinimum, double newMinimum)
        {
            _minimum = newMinimum;
            Arrange();
        }

        protected override void OnValueChanged(double oldValue, double newValue)
        {
            _value = Math.Clamp(newValue, _minimum, _maximum);
            Arrange();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            Arrange();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _toolTip.IsOpen = false;

            _zoomTimer?.Stop();
            StopAnimation();
        }

        public bool IsChanging => _pressed;

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            var pointer = e.GetCurrentPoint(this);
            var point = pointer.Position;

            if (e.OriginalSource == MinimumThumb)
            {
                _tempMin = Math.Max(0, _maximum - _maxLength);
                _tempMax = Math.Max(0, _maximum - _minLength);
                _target = MinimumThumb;
            }
            else if (e.OriginalSource == MiddleThumb1)
            {
                _tempMin = 0;
                _tempMax = 1 - (_maximum - _minimum);
                _target = MiddleThumb1;
            }
            else if (e.OriginalSource == MiddleThumb2)
            {
                _tempMin = _minimum;
                _tempMax = _maximum;
                _target = MiddleThumb2;
            }
            else if (e.OriginalSource == MaximumThumb)
            {
                _tempMin = Math.Min(1, _minimum + _minLength);
                _tempMax = Math.Min(1, _minimum + _maxLength);
                _target = MaximumThumb;
            }
            else
            {
                // The dimmed ends: no drag, or the next move would drag whatever was dragged last.
                return;
            }

            _pressed = true;
            CapturePointer(e.Pointer);

            // A zoom out still running would carry the handles away from under the pointer.
            if (_animating)
            {
                StopAnimation();
                SetView(_zoomed ? _zoomStart : 0, _zoomed ? _zoomLength : 1);
            }

            _delta = _target.Margin.Left - point.X;
            _distance = _maximum - _minimum;
            _lastX = point.X;

            if (_target != MiddleThumb1)
            {
                _toolTip.IsOpen = true;
            }

            // Not the playhead: scrubbing is pausing to look at frames, and each pause would zoom.
            _holdable = _target == MinimumThumb || _target == MaximumThumb;

            if (_holdable)
            {
                ScheduleZoom();
            }
        }

        protected override void OnPointerMoved(PointerRoutedEventArgs e)
        {
            if (!_pressed)
            {
                return;
            }

            var x = e.GetCurrentPoint(this).Position.X;
            Calculate(x, false);
            Arrange();

            // The hold that zooms in starts over on every move.
            if (_holdable && x != _lastX)
            {
                ScheduleZoom();
            }

            _lastX = x;
        }

        protected override void OnPointerReleased(PointerRoutedEventArgs e)
        {
            if (!_pressed)
            {
                return;
            }

            EndDrag(e.GetCurrentPoint(this).Position.X);
            ReleasePointerCapture(e.Pointer);
        }

        protected override void OnPointerCaptureLost(PointerRoutedEventArgs e)
        {
            // Capture goes without a release when the window loses focus mid drag. Ending the
            // drag where the pointer was last seen also raises MaximumChanged, which resumes
            // the playback MinimumChanged paused.
            if (_pressed)
            {
                EndDrag(_lastX);
            }
        }

        private void EndDrag(double x)
        {
            _pressed = false;

            Calculate(x, true);
            Arrange();

            _toolTip.IsOpen = false;

            ZoomOut();
        }

        private void ScheduleZoom()
        {
            if (_zoomed || ZoomFactor <= 1 || _originalDuration.TotalSeconds <= 1)
            {
                return;
            }

            if (_zoomTimer == null)
            {
                _zoomTimer = new DispatcherTimer();
                _zoomTimer.Interval = TimeSpan.FromMilliseconds(ZOOM_DELAY);
                _zoomTimer.Tick += OnZoomTick;
            }

            _zoomTimer.Stop();
            _zoomTimer.Start();
        }

        private void OnZoomTick(object sender, object e)
        {
            _zoomTimer.Stop();

            if (!_pressed || _zoomed)
            {
                return;
            }

            var pivot = _target == MinimumThumb
                ? _minimum
                : _target == MaximumThumb
                ? _maximum
                : _value;

            // The view is the whole video here, so this keeps the held handle where it is
            // and the window inside the video.
            _zoomed = true;
            _zoomLength = 1 / ZoomFactor;
            _zoomStart = pivot * (1 - _zoomLength);

            StartAnimation();
        }

        private void ZoomOut()
        {
            _zoomTimer?.Stop();

            if (_zoomed)
            {
                _zoomed = false;
                StartAnimation();
            }
        }

        private void StartAnimation()
        {
            _fromStart = _viewStart;
            _fromLength = _viewLength;
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

            var t = Math.Min(1, (time - _animationStart.Value).TotalMilliseconds / ZOOM_DURATION);
            var progress = Math.Cos((t + 1) * Math.PI) / 2 + 0.5;

            var toStart = _zoomed ? _zoomStart : 0;
            var toLength = _zoomed ? _zoomLength : 1;

            // Lerping start and length together keeps the point both views share in place,
            // which is the held handle. The last frame lands on the target exactly, as
            // listeners compare against it.
            if (t >= 1)
            {
                StopAnimation();
                SetView(toStart, toLength);
            }
            else
            {
                SetView(_fromStart + (toStart - _fromStart) * progress, _fromLength + (toLength - _fromLength) * progress);
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

        private void SetView(double start, double length)
        {
            _viewStart = start;
            _viewLength = length;

            Arrange();
            ViewChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Calculate(double x, bool set)
        {
            // _delta + x recovers the margin Arrange gave the thumb, so this has to invert
            // Arrange exactly: the same track and view, and the same origin within it for
            // this thumb. Anything else and a press that never moves resolves to a different
            // value than the one the thumb was drawn at, which shifts the range under the
            // pointer.
            var track = ActualWidth - THICKNESS * 2;
            var origin = _target == MiddleThumb2
                ? THICKNESS / 2
                : _target == MaximumThumb
                ? THICKNESS
                : 0;

            var value = _viewStart + (_delta + x - origin) / track * _viewLength;
            value = Math.Max(_tempMin, Math.Min(_tempMax, value));

            if (_target == MinimumThumb)
            {
                _minimum = value;
                _value = _minimum;
            }
            else if (_target == MiddleThumb1)
            {
                _minimum = value;
                _maximum = value + _distance;
                _value = _minimum;
            }
            else if (_target == MiddleThumb2)
            {
                _value = value;
            }
            else if (_target == MaximumThumb)
            {
                _maximum = value;
                _value = _maximum;
            }

            if (set)
            {
                if (_maximum < Minimum)
                {
                    Minimum = _minimum;
                    Maximum = _maximum;
                }
                else
                {
                    Maximum = _maximum;
                    Minimum = _minimum;
                }

                Value = _value;
                MaximumChanged?.Invoke(this, _target == MaximumThumb ? _maximum : _target == MiddleThumb2 ? _value : _minimum);
            }
            else
            {
                MinimumChanged?.Invoke(this, _target == MaximumThumb ? _maximum : _target == MiddleThumb2 ? _value : _minimum);
            }
        }

        private void Arrange()
        {
            if (BackgroundMinimum == null)
            {
                return;
            }

            // Both handles stay inside the control, so the distance either travels is the
            // width less one of each. Calculate inverts this.
            var track = ActualWidth - THICKNESS * 2;
            var minimum = Position(_minimum, track);
            var maximum = Position(_maximum, track);
            var value = Position(_value, track);

            BackgroundMinimum.Margin = new Thickness(0, 0, ((1 - minimum) * track) + THICKNESS, 0);
            BackgroundMaximum.Margin = new Thickness((maximum * track) + THICKNESS, 0, 0, 0);
            MiddleThumb1.Margin = new Thickness(minimum * track, 0, (1 - maximum) * track, 0);
            MinimumThumb.Margin = new Thickness(minimum * track, 0, 0, 0);
            MiddleThumb2.Margin = new Thickness(THICKNESS / 2 + value * track, 0, 0, 0);
            MaximumThumb.Margin = new Thickness(THICKNESS + maximum * track, 0, 0, 0);

            _toolTip.Content = (_originalDuration * _value).ToDuration();
            _toolTip.HorizontalOffset = MiddleThumb2.Margin.Left;
        }

        private double Position(double value, double track)
        {
            // While zoomed, a handle outside the window is parked just past the edge, where the
            // template's rounded clip hides it, instead of thousands of pixels away. Not
            // Math.Clamp: before layout the track is negative, and it throws on min > max.
            var edge = THICKNESS / track;
            return Math.Max(-edge, Math.Min(1 + edge, (value - _viewStart) / _viewLength));
        }
    }
}
