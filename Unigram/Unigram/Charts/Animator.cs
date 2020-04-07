using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Xaml;

namespace Unigram.Charts
{
    public abstract class Animator
    {
        protected readonly List<AnimatorUpdateListener> _listeners = new List<AnimatorUpdateListener>();
        protected readonly List<AnimatorUpdateListener> _updateListeners = new List<AnimatorUpdateListener>();

        protected readonly object _listenersLock = new object();

        protected readonly ChartPickerDelegate.Listener _listener;

        public Animator(ChartPickerDelegate.Listener listener)
        {
            _listener = listener;
        }

        internal abstract void cancel();

        internal abstract void start();

        internal void addUpdateListener(AnimatorUpdateListener l)
        {
            lock (_listenersLock)
            {
                _updateListeners.Add(l);
            }
        }

        internal void addListener(AnimatorUpdateListener l)
        {
            lock (_listenersLock)
            {
                _listeners.Add(l);
            }
        }

        internal void removeAllListeners()
        {
            lock (_listenersLock)
            {
                _updateListeners.Clear();
                _listeners.Clear();
            }
        }

        internal virtual object getAnimatedValue()
        {
            return null;
        }

        internal abstract bool tick();
    }

    public class AnimatorSet : Animator
    {
        private readonly List<Animator> _animators = new List<Animator>();

        public AnimatorSet(ChartPickerDelegate.Listener listener)
            : base(listener)
        {
        }

        internal void playTogether(params Animator[] valueAnimator)
        {
            foreach (var animator in valueAnimator)
            {
                _animators.Add(animator);
            }
        }

        internal override void start()
        {
            foreach (var animator in _animators)
            {
                animator.start();
            }
        }

        internal override void cancel()
        {
            foreach (var animator in _animators)
            {
                animator.cancel();
            }
        }

        internal override bool tick()
        {
            var completed = true;
            foreach (var animator in _animators)
            {
                if (!animator.tick())
                {
                    completed = false;
                }
            }

            return completed;
        }
    }

    public class ValueAnimator : Animator
    {
        private readonly float _f1;
        private readonly float _f2;

        //private readonly Timer _timer;

        private int _begin;
        private int _duration = 300;

        private float _result;

        private FastOutSlowInInterpolator _interpolator;

        public ValueAnimator(ChartPickerDelegate.Listener listener, float f1, float f2)
            : base(listener)
        {
            _f1 = _result = f1;
            _f2 = f2;

            //_timer = new Timer(OnTick, null, Timeout.Infinite, Timeout.Infinite);
            //_timer.Interval = TimeSpan.FromMilliseconds(1000d / 30d);
            //_timer.Tick += OnTick;
        }

        internal override void start()
        {
            if (_f1 == _f2)
            {
                return;
            }

            _begin = Environment.TickCount;
            _listener.change(this, true);
            //_timer.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(1000d / 30d));
        }

        internal override void cancel()
        {
            _begin = Timeout.Infinite;
            _listener.change(this, false);
            //_timer.Change(Timeout.Infinite, Timeout.Infinite);

            lock (_listenersLock)
            {
                foreach (var l in _listeners)
                {
                    l.Action(this);
                }
            }
        }

        internal override bool tick()
        {
            var tick = Environment.TickCount;
            if (tick >= _begin + _duration)
            {
                //System.Diagnostics.Debug.WriteLine($"_f1: {_f1}; _f2: {_f2}; _result: {_result} -> timeout");

                _result = _f2;

                complete();
                return true;
            }

            var diff = tick - _begin;
            var perc = (float)diff / _duration;

            if (_interpolator != null)
            {
                perc = _interpolator.getInterpolation(perc);
            }

            if (_f2 > _f1)
            {
                var maximum = _f2 - _f1;
                var value = _f1 + (maximum * perc);

                _result = Math.Min(_f2, value);
            }
            else
            {
                var maximum = _f1 - _f2;
                var value = _f2 + (maximum * (1 - perc));

                _result = Math.Max(_f2, value);
            }

            //System.Diagnostics.Debug.WriteLine($"_f1: {_f1}; _f2: {_f2}; _result: {_result}");

            if ((_f2 > _f1 && _result >= _f2) || (_f2 < _f1 && _result <= _f2))
            {
                complete();
                return true;
            }
            else
            {
                lock (_listenersLock)
                {
                    foreach (var l in _updateListeners)
                    {
                        l.Action(this);
                    }
                }
            }

            return false;
        }

        private void complete()
        {
            _begin = Timeout.Infinite;
            _listener.change(this, false);
            //_timer.Change(Timeout.Infinite, Timeout.Infinite);

            lock (_listenersLock)
            {
                //foreach (var l in _updateListeners.Union(_listeners))
                foreach (var l in _listeners.Union(_updateListeners))
                {
                    l.Action(this);
                }
            }
        }

        internal static ValueAnimator ofFloat(ChartPickerDelegate.Listener listener, float f1, float f2)
        {
            return new ValueAnimator(listener, f1, f2);
        }

        internal ValueAnimator setDuration(int duration)
        {
            _duration = duration;
            return this;
        }

        internal Animator setInterpolator(FastOutSlowInInterpolator interpolator)
        {
            _interpolator = interpolator;
            return this;
        }

        internal bool isRunning()
        {
            return _begin != Timeout.Infinite;
        }

        internal override object getAnimatedValue()
        {
            return _result;
            return _f2;
        }
    }

    public class AnimatorUpdateListener
    {
        private Action<Animator> _update;
        private Action<Animator> _end;

        public AnimatorUpdateListener(Action<Animator> update = null, Action<Animator> end = null)
        {
            _update = update;
            _end = end;
        }

        public Action<Animator> Action => _update ?? _end;
    }
}
