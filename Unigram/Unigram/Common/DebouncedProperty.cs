using System;
using Windows.UI.Xaml;

namespace Unigram.Common
{
    public class DebouncedProperty<T>
    {
        private readonly DispatcherTimer _timer;

        private readonly Action<T> _update;
        private readonly Func<T, bool> _canUpdate;

        private T _lastValue;
        private T _value;

        public DebouncedProperty(double milliseconds, Action<T> update, Func<T, bool> canUpdate = null)
            : this(TimeSpan.FromMilliseconds(milliseconds), update, canUpdate)
        {
        }

        public DebouncedProperty(TimeSpan throttle, Action<T> update, Func<T, bool> canUpdate = null)
        {
            _timer = new DispatcherTimer();
            _timer.Interval = throttle;
            _timer.Tick += OnTick;

            _update = update;
            _canUpdate = canUpdate ?? DefaultCanUpdate;
        }

        private static bool DefaultCanUpdate(T value)
        {
            return true;
        }

        public static implicit operator T(DebouncedProperty<T> debouncer)
        {
            return debouncer._value;
        }

        public void Cancel()
        {
            _timer?.Stop();
        }

        private void OnTick(object sender, object e)
        {
            _timer.Stop();

            _value = _lastValue;
            _update(_lastValue);
            _lastValue = default;
        }

        public T Value
        {
            get => _value;
            set => _value = value;
        }

        public void Set(T value)
        {
            _timer?.Stop();

            if (_canUpdate(value))
            {
                _lastValue = value;
                _timer?.Start();
            }
            else
            {
                _value = value;
                _lastValue = default;
            }
        }
    }

}
