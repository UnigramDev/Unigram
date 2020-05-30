using System;
using System.Threading;
using Windows.UI.Xaml;

namespace Unigram.Common
{
    public class LoopThread
    {
        private TimeSpan _interval;

        private readonly Timer _timerTick;
        private readonly DispatcherTimer _timerInvalidate;

        private bool _dropTick;
        private bool _dropInvalidate;

        public LoopThread(TimeSpan interval)
        {
            _interval = interval;

            _timerTick = new Timer(OnTick, null, Timeout.Infinite, Timeout.Infinite);

            _timerInvalidate = new DispatcherTimer();
            _timerInvalidate.Interval = _interval;
            _timerInvalidate.Tick += OnInvalidate;
        }

        private static LoopThread _animations;
        public static LoopThread Animations => _animations = _animations ?? new LoopThread(TimeSpan.FromMilliseconds(1000 / 25));

        private static LoopThread _stickers;
        public static LoopThread Stickers => _stickers = _stickers ?? new LoopThread(TimeSpan.FromMilliseconds(1000 / 30));

        private void OnTick(object state)
        {
            if (_dropTick)
            {
                return;
            }

            _dropTick = true;
            _tick?.Invoke(state, EventArgs.Empty);
            _dropTick = false;
        }

        private void OnInvalidate(object sender, object e)
        {
            if (_dropInvalidate)
            {
                return;
            }

            _dropInvalidate = true;
            _invalidate?.Invoke(sender, EventArgs.Empty);
            _dropInvalidate = false;
        }

        private event EventHandler _tick;
        public event EventHandler Tick
        {
            add
            {
                if (_tick == null) _timerTick.Change(TimeSpan.Zero, _interval);
                _tick += value;
            }
            remove
            {
                _tick -= value;
                if (_tick == null) _timerTick.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }

        private event EventHandler _invalidate;
        public event EventHandler Invalidate
        {
            add
            {
                if (_invalidate == null) _timerInvalidate.Start();
                _invalidate += value;
            }
            remove
            {
                _invalidate -= value;
                if (_invalidate == null) _timerInvalidate.Stop();
            }
        }
    }
}
