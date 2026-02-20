//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading;
using Windows.UI.Xaml;

namespace Telegram.Common
{
    public partial class DebouncedPropertyWithToken<T>
    {
        private readonly DispatcherTimer _timer;
        private readonly Timer _backgroundTimer;

        private readonly TimeSpan _interval;

        private readonly Action<T, CancellationToken> _update;
        private readonly Func<T, CancellationToken, bool> _canUpdate;

        private CancellationToken _cancellationToken;

        private T _lastValue;
        private T _value;

        public DebouncedPropertyWithToken(double milliseconds, Action<T, CancellationToken> update, Func<T, CancellationToken, bool> canUpdate = null, bool useBackgroundThread = false)
            : this(TimeSpan.FromMilliseconds(milliseconds), update, canUpdate, useBackgroundThread)
        {
        }

        public DebouncedPropertyWithToken(TimeSpan throttle, Action<T, CancellationToken> update, Func<T, CancellationToken, bool> canUpdate = null, bool useBackgroundThread = false)
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

        private static bool DefaultCanUpdate(T value, CancellationToken token)
        {
            return !token.IsCancellationRequested;
        }

        public static implicit operator T(DebouncedPropertyWithToken<T> debouncer)
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
                _update(_lastValue, _cancellationToken);
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
                _update(_lastValue, _cancellationToken);
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

            if (_canUpdate(value, cancellationToken))
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
