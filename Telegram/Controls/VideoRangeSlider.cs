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

namespace Telegram.Controls
{
    public partial class VideoRangeSlider : RangeBase
    {
        private bool _pressed;
        private double _delta;
        private double _distance;
        private FrameworkElement _target;

        private double _tempMin = 0;
        private double _tempMax = 1;

        private double _minimum = 0;
        private double _maximum = 1;
        private double _value = 0;

        private double _minLength = 0;
        private double _maxLength = 1;

        private const double THICKNESS = 12;

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
        }

        public bool IsChanging => _pressed;

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

            if (_target != MiddleThumb1)
            {
                _toolTip.IsOpen = true;
            }
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

            _toolTip.IsOpen = false;
        }

        private void Calculate(double x, bool set)
        {
            var width = ActualWidth - THICKNESS;
            var delta = _delta + x;
            delta = Math.Max(_tempMin * width, Math.Min(_tempMax * width, delta));

            if (_target == MinimumThumb)
            {
                _minimum = delta / width;
                _value = _minimum;
            }
            else if (_target == MiddleThumb1)
            {
                _minimum = delta / width;
                _maximum = (delta / width) + _distance;
                _value = _minimum;
            }
            else if (_target == MiddleThumb2)
            {
                _value = delta / width;
            }
            else if (_target == MaximumThumb)
            {
                _maximum = delta / width;
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

            var width = ActualWidth - THICKNESS;
            BackgroundMinimum.Margin = new Thickness(0, 0, ((1 - _minimum) * (width - THICKNESS)) + THICKNESS, 0);
            BackgroundMaximum.Margin = new Thickness((_maximum * (width - THICKNESS)) + THICKNESS, 0, 0, 0);
            MiddleThumb1.Margin = new Thickness(_minimum * (width - THICKNESS), 0, (1 - _maximum) * (width - THICKNESS), 0);
            MinimumThumb.Margin = new Thickness(_minimum * (width - THICKNESS), 0, 0, 0);
            MiddleThumb2.Margin = new Thickness(THICKNESS / 2 + _value * (width - THICKNESS), 0, 0, 0);
            MaximumThumb.Margin = new Thickness(THICKNESS + _maximum * (width - THICKNESS), 0, 0, 0);

            _toolTip.Content = (_originalDuration * _value).ToDuration();
            _toolTip.HorizontalOffset = MiddleThumb2.Margin.Left;
        }
    }
}
