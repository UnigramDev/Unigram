using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;

namespace Unigram.Controls
{
    public class VideoRangeSlider : RangeBase
    {
        private bool _pressed;
        private double _delta;
        private double _distance;
        private FrameworkElement _target;

        private double _tempMin = 0;
        private double _tempMax = 1;

        private double _minimum = 0;
        private double _maximum = 1;
        private double _value = 0.5;

        private double _minLength = 0;
        private double _maxLength = 1;

        private const double THICKNESS = 12;

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
        }

        public event EventHandler<double> MinimumChanged;
        public event EventHandler<double> MaximumChanged;

        public TimeSpan OriginalDuration { get; private set; }

        public void SetOriginalDuration(TimeSpan duration)
        {
            OriginalDuration = duration;

            _maxLength = Math.Min(9 / duration.TotalSeconds, 1);
            _minLength = Math.Min(3 / duration.TotalSeconds, 1);

            _minimum = 0;
            _maximum = _maxLength;

            Arrange();
        }

        protected override void OnApplyTemplate()
        {
            BackgroundMinimum = GetTemplateChild("BackgroundMinimum") as FrameworkElement;
            BackgroundMaximum = GetTemplateChild("BackgroundMaximum") as FrameworkElement;
            MiddleThumb1 = GetTemplateChild("MiddleThumb1") as FrameworkElement;
            MinimumThumb = GetTemplateChild("MinimumThumb") as FrameworkElement;
            MaximumThumb = GetTemplateChild("MaximumThumb") as FrameworkElement;
            MiddleThumb2 = GetTemplateChild("MiddleThumb2") as FrameworkElement;
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
            _value = newValue;
            Arrange();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            Arrange();
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            _pressed = true;
            CapturePointer(e.Pointer);

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
                return;
            }

            _delta = _target.Margin.Left - point.X;
            _distance = _maximum - _minimum;
        }

        protected override void OnPointerMoved(PointerRoutedEventArgs e)
        {
            if (!_pressed)
            {
                return;
            }

            var pointer = e.GetCurrentPoint(this);
            Calculate(pointer.Position.X, false);
            Arrange();
        }

        protected override void OnPointerReleased(PointerRoutedEventArgs e)
        {
            _pressed = false;
            ReleasePointerCapture(e.Pointer);

            var pointer = e.GetCurrentPoint(this);
            Calculate(pointer.Position.X, true);
            Arrange();
        }

        private void Calculate(double x, bool set)
        {
            var width = ActualWidth - THICKNESS;
            var delta = _delta + x;
            delta = Math.Max(_tempMin * width, Math.Min(_tempMax * width, delta));

            if (_target == MinimumThumb)
            {
                _minimum = delta / width;
                _value = Math.Max(_minimum, _value);
            }
            else if (_target == MiddleThumb1)
            {
                _minimum = delta / width;
                _maximum = (delta / width) + _distance;
                _value = Math.Max(_minimum, Math.Min(_maximum, _value));
            }
            else if (_target == MiddleThumb2)
            {
                _value = delta / width;
            }
            else if (_target == MaximumThumb)
            {
                _maximum = delta / width;
                _value = Math.Min(_maximum, _value);
            }

            if (set)
            {
                Maximum = _maximum;
                Minimum = _minimum;
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

            var width = ActualWidth - THICKNESS;
            BackgroundMinimum.Margin = new Thickness(0, 0, ((1 - _minimum) * (width - THICKNESS)) + THICKNESS, 0);
            BackgroundMaximum.Margin = new Thickness((_maximum * (width - THICKNESS)) + THICKNESS, 0, 0, 0);
            MiddleThumb1.Margin = new Thickness(_minimum * (width - THICKNESS), 0, (1 - _maximum) * (width - THICKNESS), 0);
            MinimumThumb.Margin = new Thickness(_minimum * (width - THICKNESS), 0, 0, 0);
            MiddleThumb2.Margin = new Thickness(THICKNESS / 2 + _value * (width - THICKNESS), 0, 0, 0);
            MaximumThumb.Margin = new Thickness(THICKNESS + _maximum * (width - THICKNESS), 0, 0, 0);
        }
    }
}
