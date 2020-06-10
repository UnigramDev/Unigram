using System;
using System.Threading;
using Windows.UI.Xaml.Media;

namespace Unigram.Common
{
    public class LoopThread
    {
        private TimeSpan _interval;
        private TimeSpan _elapsed;

        private readonly Timer _timerTick;

        private bool _dropTick;
        private bool _dropInvalidate;

        public LoopThread(TimeSpan interval)
        {
            _interval = interval;
            _timerTick = new Timer(OnTick, null, Timeout.Infinite, Timeout.Infinite);
        }

        [ThreadStatic]
        private static LoopThread _animations;
        public static LoopThread Animations => _animations ??= new LoopThread(TimeSpan.FromMilliseconds(1000 / 30));

        [ThreadStatic]
        private static LoopThread _stickers;
        public static LoopThread Stickers => _stickers ??= new LoopThread(TimeSpan.FromMilliseconds(1000 / 30));

        [ThreadStatic]
        private static LoopThread _chats;
        public static LoopThread Chats => _chats ??= new LoopThread(TimeSpan.FromMilliseconds(1000 / 60));

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
            var args = e as RenderingEventArgs;
            var diff = args.RenderingTime - _elapsed;

            if (_dropInvalidate || diff < _interval)
            {
                return;
            }

            _elapsed = args.RenderingTime;

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
                if (_invalidate == null) CompositionTarget.Rendering += OnInvalidate;
                _invalidate += value;
            }
            remove
            {
                _invalidate -= value;
                if (_invalidate == null) CompositionTarget.Rendering -= OnInvalidate;
            }
        }
    }
}
