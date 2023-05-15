//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Threading;
using Windows.UI.Xaml;

namespace Telegram.Common
{
    public class DebouncedProperty<T>
    {
        private readonly DispatcherTimer _timer;
        private readonly Timer _backgroundTimer;

        private readonly TimeSpan _interval;

        private readonly Action<T> _update;
        private readonly Func<T, bool> _canUpdate;

        private CancellationToken _cancellationToken;

        private T _lastValue;
        private T _value;

        public DebouncedProperty(double milliseconds, Action<T> update, Func<T, bool> canUpdate = null, bool useBackgroundThread = false)
            : this(TimeSpan.FromMilliseconds(milliseconds), update, canUpdate, useBackgroundThread)
        {
        }

        public DebouncedProperty(TimeSpan throttle, Action<T> update, Func<T, bool> canUpdate = null, bool useBackgroundThread = false)
        {
            if (useBackgroundThread)
            {
                _backgroundTimer = new Timer(OnTick);
            }
            else
            {
                _timer = new DispatcherTimer();
                _timer.Interval = throttle;
                _timer.Tick += OnTick;
            }

            _interval = throttle;

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
            _backgroundTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private void OnTick(object sender)
        {
            _backgroundTimer.Change(Timeout.Infinite, Timeout.Infinite);

            if (_cancellationToken.IsCancellationRequested)
            {
                _lastValue = default;
            }
            else
            {
                _value = _lastValue;
                _update(_lastValue);
                _lastValue = default;
            }
        }

        private void OnTick(object sender, object e)
        {
            _timer.Stop();

            if (_cancellationToken.IsCancellationRequested)
            {
                _lastValue = default;
            }
            else
            {
                _value = _lastValue;
                _update(_lastValue);
                _lastValue = default;
            }
        }

        public T Value
        {
            get => _value;
            set => _value = value;
        }

        public void Set(T value, CancellationToken cancellationToken = default)
        {
            _timer?.Stop();
            _backgroundTimer?.Change(Timeout.Infinite, Timeout.Infinite);

            if (_canUpdate(value))
            {
                _cancellationToken = cancellationToken;

                _lastValue = value;
                _timer?.Start();
                _backgroundTimer?.Change(_interval, TimeSpan.Zero);
            }
            else
            {
                _cancellationToken = default;

                _value = value;
                _lastValue = default;
            }
        }
    }

}
