using System;
using System.Threading;
using Windows.System.Threading;
using Windows.UI.Xaml.Media;

namespace Unigram.Common
{
    public class LoopThread
    {
        private TimeSpan _interval;
        private TimeSpan _elapsed;

        private ThreadPoolTimer _timer;

        private readonly SemaphoreSlim _tickSemaphore;
        private bool _dropInvalidate;

        public LoopThread(TimeSpan interval)
        {
            _interval = interval;
            _tickSemaphore = new SemaphoreSlim(1, 1);
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

        private void OnTick(ThreadPoolTimer timer)
        {
            if (_tickSemaphore.Wait(0))
            {
                _tick?.Invoke(this, EventArgs.Empty);
                _tickSemaphore.Release();
            }
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
                _timer ??= ThreadPoolTimer.CreatePeriodicTimer(OnTick, _interval);
                _tick += value;
            }
            remove
            {
                _tick -= value;

                if (_tick == null && _timer != null)
                {
                    _timer.Cancel();
                    _timer = null;
                }
            }
        }

        private event EventHandler _invalidate;
        public event EventHandler Invalidate
        {
            add
            {
                if (_invalidate == null)
                {
                    CompositionTarget.Rendering += OnInvalidate;
                }

                _invalidate += value;
            }
            remove
            {
                _invalidate -= value;

                if (_invalidate == null)
                {
                    CompositionTarget.Rendering -= OnInvalidate;
                }
            }
        }
    }
}
