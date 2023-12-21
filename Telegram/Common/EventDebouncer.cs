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
    public class EventDebouncer<TEventArgs>
    {
        private readonly DispatcherTimer _timer;
        private readonly Timer _backgroundTimer;

        private readonly TimeSpan _interval;

        private readonly Action<Action<object, TEventArgs>> _subscription;
        private readonly Action<Action<object, TEventArgs>> _unsubscription;

        private object _lastSender;
        private TEventArgs _lastArgs;

        public EventDebouncer(double milliseconds, Action<Action<object, TEventArgs>> subscription, Action<Action<object, TEventArgs>> unsubscription = null, bool useBackgroundThread = false)
            : this(TimeSpan.FromMilliseconds(milliseconds), subscription, unsubscription, useBackgroundThread)
        {
        }

        public EventDebouncer(TimeSpan throttle, Action<Action<object, TEventArgs>> subscription, Action<Action<object, TEventArgs>> unsubscription = null, bool useBackgroundThread = false)
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

            _subscription = subscription;
            _unsubscription = unsubscription;
        }

        public void Cancel()
        {
            _timer?.Stop();
            _backgroundTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public void Unsubscribe()
        {
            _unsubscription(OnInvoked);
        }

        private event EventHandler<TEventArgs> _invoked;
        public event EventHandler<TEventArgs> Invoked
        {
            add
            {
                if (_invoked == null)
                {
                    _subscription(OnInvoked);
                }

                _invoked += value;
            }
            remove
            {
                _invoked -= value;

                if (_invoked == null)
                {
                    _unsubscription?.Invoke(OnInvoked);
                    Cancel();
                }
            }
        }

        private void OnTick(object sender, object e)
        {
            _timer.Stop();

            _invoked?.Invoke(_lastSender, _lastArgs);

            _lastSender = null;
            _lastArgs = default;
        }

        private void OnTick(object sender)
        {
            _backgroundTimer.Change(Timeout.Infinite, Timeout.Infinite);

            _invoked?.Invoke(_lastSender, _lastArgs);

            _lastSender = null;
            _lastArgs = default;
        }

        private void OnInvoked(object sender, TEventArgs args)
        {
            _timer?.Stop();
            _backgroundTimer?.Change(Timeout.Infinite, Timeout.Infinite);

            _lastSender = sender;
            _lastArgs = args;
            _timer?.Start();
            _backgroundTimer?.Change(_interval, TimeSpan.Zero);
        }

        public void Invoke()
        {
            _timer?.Stop();
            _backgroundTimer?.Change(Timeout.Infinite, Timeout.Infinite);

            _lastSender = null;
            _lastArgs = default;
            _timer?.Start();
            _backgroundTimer?.Change(_interval, TimeSpan.Zero);
        }
    }
}
