//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Telegram.Charts
{
    public partial class AnimatorCoordinator
    {
        private readonly Action _invalidate;
        private readonly List<Animator> _animators = new();

        public AnimatorCoordinator(Action invalidate)
        {
            _invalidate = invalidate;
            //_timer = new Timer(OnTick, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(1000 / 60));
        }

        public bool IsRunning => _animators.Count > 0;

        //private static AnimatorLoopThread _current;
        //public static AnimatorLoopThread Current => _current ??= new AnimatorLoopThread();

        //private void OnTick(object state)
        //{
        //    lock (DrawLock)
        //    {
        //        lock (_animatorsLock)
        //        {
        //            foreach (var animator in _animators.ToArray())
        //            {
        //                animator.Tick();
        //            }
        //        }
        //    }
        //}

        // The frame's timestamp comes from the caller: the view needs the same instant for its own
        // min/max step, and two reads would put them on different sides of a clock edge.
        public void Tick(long timestamp)
        {
            foreach (var animator in _animators.ToArray())
            {
                animator.Tick(timestamp);
            }
        }

        public void Change(Animator animator, bool enable)
        {
            if (enable)
            {
                _animators.Add(animator);

                // An animator can be started from anywhere, so asking for a frame here is what
                // guarantees it gets ticked. The list draining is what stops the frames again.
                _invalidate();
            }
            else
            {
                _animators.Remove(animator);
            }
        }
    }

    public abstract class Animator
    {
        protected readonly List<AnimatorUpdateListener> _listeners = new();
        protected readonly List<AnimatorUpdateListener> _updateListeners = new();

        protected readonly Action<Animator, bool> _listener;

        public Animator(AnimatorCoordinator thread)
        {
            _listener = thread.Change;
        }

        internal abstract void Cancel();

        internal abstract void Start();

        internal void AddUpdateListener(AnimatorUpdateListener l)
        {
            _updateListeners.Add(l);
        }

        internal void AddListener(AnimatorUpdateListener l)
        {
            _listeners.Add(l);
        }

        internal void RemoveAllListeners()
        {
            _updateListeners.Clear();
            _listeners.Clear();
        }

        internal virtual object GetAnimatedValue()
        {
            return null;
        }

        internal abstract bool Tick(long timestamp);
    }

    public partial class AnimatorSet : Animator
    {
        private readonly List<Animator> _animators = new();

        public AnimatorSet(AnimatorCoordinator thread)
            : base(thread)
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

        internal override bool Tick(long timestamp)
        {
            var completed = true;
            foreach (var animator in _animators)
            {
                if (!animator.Tick(timestamp))
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

        private long _begin;
        private long _duration = DurationToTicks(300);

        private float _result;

        private FastOutSlowInInterpolator _interpolator;

        public ValueAnimator(AnimatorCoordinator thread, float f1, float f2)
            : base(thread)
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

            _begin = Stopwatch.GetTimestamp();
            _listener(this, true);
        }

        internal override void Cancel()
        {
            _begin = 0;
            _listener(this, false);

            foreach (var l in _listeners)
            {
                l.Action(this);
            }
        }

        internal override bool Tick(long timestamp)
        {
            var diff = timestamp - _begin;
            if (diff >= _duration)
            {
                _result = _f2;

                Complete();
                return true;
            }

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
                foreach (var l in _updateListeners)
                {
                    l.Action(this);
                }
            }

            return false;
        }

        private void Complete()
        {
            _begin = 0;
            _listener(this, false);

            foreach (var l in _listeners.Union(_updateListeners))
            {
                l.Action(this);
            }
        }

        internal static ValueAnimator OfFloat(AnimatorCoordinator thread, float f1, float f2)
        {
            return new ValueAnimator(thread, f1, f2);
        }

        internal ValueAnimator SetDuration(uint duration)
        {
            _duration = DurationToTicks(duration);
            return this;
        }

        // Durations are authored in milliseconds but compared against Stopwatch ticks,
        // so convert on assignment rather than dividing by Frequency every frame.
        private static long DurationToTicks(uint duration)
        {
            return duration * Stopwatch.Frequency / 1000;
        }

        internal Animator setInterpolator(FastOutSlowInInterpolator interpolator)
        {
            _interpolator = interpolator;
            return this;
        }

        internal bool IsRunning()
        {
            return _begin != 0;
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
