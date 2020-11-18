using System;
using Windows.UI.Xaml;

namespace Unigram.Common
{
    public class EventThrottler<TEventArgs>
    {
        public delegate void Something(object sender, TEventArgs args);

        private readonly DispatcherTimer _timer;
        private readonly Action<Action<object, TEventArgs>> _unsubscription;

        private object _lastSender;
        private TEventArgs _lastArgs;

        public EventThrottler(double milliseconds, Action<Action<object, TEventArgs>> subscription, Action<Action<object, TEventArgs>> unsubscription = null)
            : this(TimeSpan.FromMilliseconds(milliseconds), subscription, unsubscription)
        {
        }

        public EventThrottler(TimeSpan throttle, Action<Action<object, TEventArgs>> subscription, Action<Action<object, TEventArgs>> unsubscription = null)
        {
            _timer = new DispatcherTimer();
            _timer.Interval = throttle;
            _timer.Tick += OnTick;

            _unsubscription = unsubscription;

            subscription(OnInvoked);
        }

        public void Unsubscribe()
        {
            _unsubscription(OnInvoked);
        }

        public event EventHandler<TEventArgs> Invoked;

        private void OnTick(object sender, object e)
        {
            _timer.Stop();

            Invoked?.Invoke(_lastSender, _lastArgs);

            _lastSender = null;
            _lastArgs = default;
        }

        private void OnInvoked(object sender, TEventArgs args)
        {
            _timer.Stop();

            _lastSender = sender;
            _lastArgs = args;
            _timer.Start();
        }
    }
}
