//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Telegram.Charts
{
    public partial class AnimatorLoopThread
    {
        public static object DrawLock = new object();

        protected readonly List<Animator> _animators = new List<Animator>();
        protected readonly object _animatorsLock = new object();

        private readonly Timer _timer;
        private bool _looping = true;

        public AnimatorLoopThread()
        {
            _timer = new Timer(OnTick, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(1000 / 60));
        }

        private static AnimatorLoopThread _current;
        public static AnimatorLoopThread Current => _current ??= new AnimatorLoopThread();

        private void OnTick(object state)
        {
            lock (DrawLock)
            {
                lock (_animatorsLock)
                {
                    foreach (var animator in _animators.ToArray())
                    {
                        animator.Tick();
                    }
                }
            }
        }

        public void Change(Animator animator, bool enable)
        {
            lock (_animatorsLock)
            {
                if (enable)
                {
                    _animators.Add(animator);

                    if (_animators.Count > 0 && !_looping)
                    {
                        _looping = true;
                        _timer.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(1000 / 60));
                    }
                }
                else
                {
                    _animators.Remove(animator);

                    if (_animators.Count < 1 && _looping)
                    {
                        _looping = false;
                        _timer.Change(Timeout.Infinite, Timeout.Infinite);
                    }
                }
            }
        }
    }

    public abstract class Animator
    {
        protected readonly List<AnimatorUpdateListener> _listeners = new List<AnimatorUpdateListener>();
        protected readonly List<AnimatorUpdateListener> _updateListeners = new List<AnimatorUpdateListener>();

        protected readonly object _listenersLock = new object();

        protected readonly Action<Animator, bool> _listener;

        public Animator()
        {
            _listener = AnimatorLoopThread.Current.Change;
        }

        internal abstract void Cancel();

        internal abstract void Start();

        internal void AddUpdateListener(AnimatorUpdateListener l)
        {
            lock (_listenersLock)
            {
                _updateListeners.Add(l);
            }
        }

        internal void AddListener(AnimatorUpdateListener l)
        {
            lock (_listenersLock)
            {
                _listeners.Add(l);
            }
        }

        internal void RemoveAllListeners()
        {
            lock (_listenersLock)
            {
                _updateListeners.Clear();
                _listeners.Clear();
            }
        }

        internal virtual object GetAnimatedValue()
        {
            return null;
        }

        internal abstract bool Tick();
    }

    public partial class AnimatorSet : Animator
    {
        private readonly List<Animator> _animators = new List<Animator>();

        public AnimatorSet()
            : base()
        {
        }

        internal void PlayTogether(params Animator[] valueAnimator)
        {
            foreach (var animator in valueAnimator)
            {
                _animators.Add(animator);
            }
        }

        internal override void Start()
        {
            foreach (var animator in _animators)
            {
                animator.Start();
            }
        }

        internal override void Cancel()
        {
            foreach (var animator in _animators)
            {
                animator.Cancel();
            }
        }

        internal override bool Tick()
        {
            var completed = true;
            foreach (var animator in _animators)
            {
                if (!animator.Tick())
                {
                    completed = false;
                }
            }

            return completed;
        }
    }

    public partial class ValueAnimator : Animator
    {
        private readonly float _f1;
        private readonly float _f2;

        private int _begin;
        private int _duration = 300;

        private float _result;

        private FastOutSlowInInterpolator _interpolator;

        public ValueAnimator(float f1, float f2)
            : base()
        {
            _f1 = _result = f1;
            _f2 = f2;
        }

        internal override void Start()
        {
            if (_f1 == _f2)
            {
                return;
            }

            _begin = Environment.TickCount;
            _listener(this, true);
        }

        internal override void Cancel()
        {
            _begin = Timeout.Infinite;
            _listener(this, false);

            lock (_listenersLock)
            {
                foreach (var l in _listeners)
                {
                    l.Action(this);
                }
            }
        }

        internal override bool Tick()
        {
            var tick = Environment.TickCount;
            if (tick >= _begin + _duration)
            {
                _result = _f2;

                Complete();
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
                var value = _f1 + maximum * perc;

                _result = Math.Min(_f2, value);
            }
            else
            {
                var maximum = _f1 - _f2;
                var value = _f2 + maximum * (1 - perc);

                _result = Math.Max(_f2, value);
            }

            if ((_f2 > _f1 && _result >= _f2) || (_f2 < _f1 && _result <= _f2))
            {
                Complete();
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

        private void Complete()
        {
            _begin = Timeout.Infinite;
            _listener(this, false);

            lock (_listenersLock)
            {
                foreach (var l in _listeners.Union(_updateListeners))
                {
                    l.Action(this);
                }
            }
        }

        internal static ValueAnimator OfFloat(float f1, float f2)
        {
            return new ValueAnimator(f1, f2);
        }

        internal ValueAnimator SetDuration(int duration)
        {
            _duration = duration;
            return this;
        }

        internal Animator setInterpolator(FastOutSlowInInterpolator interpolator)
        {
            _interpolator = interpolator;
            return this;
        }

        internal bool IsRunning()
        {
            return _begin != Timeout.Infinite;
        }

        internal override object GetAnimatedValue()
        {
            return _result;
        }
    }

    public partial class AnimatorUpdateListener
    {
        private readonly Action<Animator> _update;
        private readonly Action<Animator> _end;

        public AnimatorUpdateListener(Action<Animator> update = null, Action<Animator> end = null)
        {
            _update = update;
            _end = end;
        }

        public Action<Animator> Action => _update ?? _end;
    }
}
