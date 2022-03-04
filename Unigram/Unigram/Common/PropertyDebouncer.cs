using System;
using Windows.UI.Xaml;

namespace Unigram.Common
{
    public class PropertyDebouncer<T>
    {
        private readonly DispatcherTimer _timer;

        private readonly Action<T> _subscription;

        private T _lastValue;
        private T _value;

        public PropertyDebouncer(double milliseconds, Action<T> subscription)
            : this(TimeSpan.FromMilliseconds(milliseconds), subscription)
        {
        }

        public PropertyDebouncer(TimeSpan throttle, Action<T> subscription)
        {
            _timer = new DispatcherTimer();
            _timer.Interval = throttle;
            _timer.Tick += OnTick;

            _subscription = subscription;
        }

        public static implicit operator T(PropertyDebouncer<T> debouncer)
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
            _subscription(_lastValue);
            _lastValue = default;
        }

        public void Set(T value)
        {
            _timer?.Stop();

            _lastValue = value;
            _timer?.Start();
        }
    }

}
