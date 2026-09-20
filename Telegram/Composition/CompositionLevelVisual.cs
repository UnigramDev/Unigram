//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Composition;

namespace Telegram.Composition
{
    /// <summary>
    /// The audio level plumbing shared by the blob and curve visuals.
    /// </summary>
    /// <remarks>
    /// Levels arrive off the UI thread - tgcalls raises them from a 100ms timer, the recorder from
    /// the capture graph - so <see cref="UpdateLevel(float, bool)"/> may only store floats, and the
    /// hop onto the compositor is what these visuals used to buy with a 30Hz frame callback. The
    /// filter that callback ran is unchanged: 0.9^(30*dt) samples the same exponential at whatever
    /// rate levels actually arrive, and the key frame animations it feeds interpolate the rest on
    /// the compositor, so nothing is subscribed to the frame loop and no work happens per frame.
    /// </remarks>
    public abstract partial class CompositionLevelVisual
    {
        private const float Decay = 0.9f;
        private const float DecayRate = 30f;

        private readonly DispatcherQueue _dispatcherQueue;

        // Cached so that an apply costs no closure: levels arrive faster than anything else in this
        // file and the handler never varies.
        private readonly DispatcherQueueHandler _applyLevel;

        private readonly float _maxLevel;

        private float _audioLevel;
        private float _presentationAudioLevel;

        // The last level the shapes were given, updated whether or not the dead band let it
        // through, so a level creeping by less than the band never restarts an animation and the
        // one already running settles on its own. Each shape used to keep this, with the same
        // value in all of them.
        private float _lastLevel;

        private long _timestamp;
        private int _pending;

        private bool _animating;

        protected CompositionLevelVisual(float maxLevel)
        {
            _maxLevel = maxLevel;
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            _applyLevel = ApplyLevel;
        }

        public void UpdateLevel(float level)
        {
            UpdateLevel(level, immediately: false);
        }

        public void UpdateLevel(float level, bool immediately = false)
        {
            var normalizedLevel = MathF.Min(1, MathF.Max(level / _maxLevel, 0));

            UpdateSpeedLevel(normalizedLevel);

            _audioLevel = normalizedLevel;

            if (immediately)
            {
                _presentationAudioLevel = normalizedLevel;
            }

            if (_animating && Interlocked.CompareExchange(ref _pending, 1, 0) == 0
                && !_dispatcherQueue.TryEnqueue(_applyLevel))
            {
                _pending = 0;
            }
        }

        private void ApplyLevel()
        {
            _pending = 0;

            if (!_animating)
            {
                return;
            }

            var timestamp = Stopwatch.GetTimestamp();
            var elapsed = (float)((timestamp - _timestamp) / (double)Stopwatch.Frequency);

            _timestamp = timestamp;

            var level = _audioLevel + (_presentationAudioLevel - _audioLevel) * MathF.Pow(Decay, DecayRate * elapsed);
            _presentationAudioLevel = level;

            if (MathF.Abs(level - _lastLevel) > 0.01f)
            {
                OnLevelChanged(level);
            }

            _lastLevel = level;
        }

        public void StartAnimating()
        {
            StartAnimating(false);
        }

        public void StartAnimating(bool immediately = false)
        {
            if (_animating)
            {
                return;
            }

            _animating = true;
            _timestamp = Stopwatch.GetTimestamp();

            OnStartAnimating(immediately);
            UpdateShapesState(true);
        }

        public void StopAnimating()
        {
            StopAnimating(duration: 0.15);
        }

        public void StopAnimating(double duration)
        {
            if (!_animating)
            {
                return;
            }

            _animating = false;

            OnStopAnimating(duration);
            UpdateShapesState(false);
        }

        /// <summary>
        /// Raised on the thread the level came from, so it may only touch fields.
        /// </summary>
        protected abstract void UpdateSpeedLevel(float level);

        /// <summary>
        /// Raised on the UI thread, with the smoothed level.
        /// </summary>
        protected abstract void OnLevelChanged(float level);

        protected abstract void UpdateShapesState(bool animating);

        protected virtual void OnStartAnimating(bool immediately)
        {
        }

        protected virtual void OnStopAnimating(double duration)
        {
        }

        public abstract void Clear();
    }

    /// <summary>
    /// The self restarting path morph shared by the blob and curve shapes.
    /// </summary>
    public abstract partial class CompositionMorphShape
    {
        protected readonly CompositionSpriteShape _shape;
        protected readonly CompositionPathGeometry _shapeLayer;

        protected readonly Random _random = new();

        // The generated points, held rather than returned: a shape rebuilds its path a few times a
        // second and the array never outlives the CreateSmoothCurve call that copies it.
        protected readonly Vector2[] _points;

        // One template, instantiated by every StartAnimation. Re-inserting a key frame at a
        // progress that already holds one replaces it, and leaves the running instance alone.
        private readonly PathKeyFrameAnimation _pathAnimation;
        private readonly TypedEventHandler<object, CompositionBatchCompletedEventArgs> _batchCompleted;

        private CompositionScopedBatch _batch;

        private readonly float _minSpeed;
        private readonly float _maxSpeed;

        protected float _speedLevel;

        private bool _animating;

        protected CompositionMorphShape(CompositionSpriteShape shape, Vector2 size, int pointsCount, float minSpeed, float maxSpeed)
        {
            _shape = shape;
            _shapeLayer = shape.Geometry as CompositionPathGeometry;
            _size = size;

            _minSpeed = minSpeed;
            _maxSpeed = maxSpeed;

            _points = new Vector2[pointsCount];

            // A shape over an ellipse geometry never morphs, and has no path animation at all.
            if (_shapeLayer != null)
            {
                _pathAnimation = shape.Compositor.CreatePathKeyFrameAnimation();
                _batchCompleted = OnBatchCompleted;
            }
        }

        private Vector2 _size;
        public Vector2 Size
        {
            get => _size;
            set => _size = value;
        }

        public void UpdateSpeedLevel(float newSpeedLevel)
        {
            _speedLevel = MathF.Max(_speedLevel, newSpeedLevel);
        }

        protected float NextRandom()
        {
            var accuracy = 1000;
            var random = _random.Next(accuracy);
            return (float)random / (float)accuracy;
        }

        public virtual void StartAnimating()
        {
            _animating = true;
            AnimateToNewShape();
        }

        public void StopAnimating()
        {
            _animating = false;
            _shapeLayer?.StopAnimation("Path");
        }

        public void Clear()
        {
            _shape.Scale = Vector2.Zero;
        }

        private void AnimateToNewShape()
        {
            if (_shapeLayer == null || !_animating)
            {
                return;
            }

            if (ShouldResetPath)
            {
                _shapeLayer.Path = CreateNextPath();
            }

            _pathAnimation.InsertKeyFrame(0, _shapeLayer.Path);
            _pathAnimation.InsertKeyFrame(1, CreateNextPath());
            _pathAnimation.Duration = TimeSpan.FromSeconds(1 / (_minSpeed + (_maxSpeed - _minSpeed) * _speedLevel));

            // Composition reports completion per batch and not per animation, so the morph chains
            // itself from one. The handler is a field rather than a lambda both because it has to
            // come off again and because a closure here would be one per morph.
            _batch = _shapeLayer.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            _batch.Completed += _batchCompleted;

            _shapeLayer.StartAnimation("Path", _pathAnimation);
            _batch.End();

            OnPathAnimated();

            _speedLevel = 0;
        }

        private void OnBatchCompleted(object sender, CompositionBatchCompletedEventArgs args)
        {
            // Held in a field rather than taken from the sender: a scoped batch is single use, and
            // this is the only place that can release the one that just finished.
            if (_batch != null)
            {
                _batch.Completed -= _batchCompleted;
                _batch.Dispose();
                _batch = null;
            }

            AnimateToNewShape();
        }

        protected virtual bool ShouldResetPath => _shapeLayer.Path == null;

        protected virtual void OnPathAnimated()
        {
        }

        protected abstract CompositionPath CreateNextPath();
    }
}
