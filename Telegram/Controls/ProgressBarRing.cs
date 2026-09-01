//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Numerics;
using Telegram.Native.Controls;
using Telegram.Navigation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    public partial class ProgressBarRing : ControlEx
    {
        // Every FileButton template carries one of these, so nothing is created until there
        // is an arc to draw: a ring that never sees a download costs no composition objects.
        private ShapeVisual _visual;
        private CompositionSpriteShape _shape;
        private CompositionEllipseGeometry _ellipse;
        private CompositionColorBrush _stroke;

        // The trims as they were last written directly, NaN while an animation owns them.
        private float _trimStartValue = float.NaN;
        private float _trimEndValue = float.NaN;

        private bool _spinning;
        private ScalarKeyFrameAnimation _foreverAnimation;

        // Composition animations are templates - StartAnimation generates an instance from
        // one - so a single object can drive every update, and inserting at a progress that
        // already holds a key frame replaces it. Key frames can never be removed though, so
        // the pair that restarts from zero (it needs a frame at 0, and eases by default)
        // cannot be the same pair as the one that continues from wherever the trim is.
        private LinearEasingFunction _linearEasing;
        private ScalarKeyFrameAnimation _trimStartAnimation;
        private ScalarKeyFrameAnimation _trimEndAnimation;
        private ScalarKeyFrameAnimation _trimStartFromZero;
        private ScalarKeyFrameAnimation _trimEndFromZero;

        public ProgressBarRing()
        {
            DefaultStyleKey = typeof(ProgressBarRing);
            RegisterPropertyChangedCallback(ForegroundProperty, OnForegroundChanged);
        }

        public double Radius { get; set; } = 21;
        public double Center { get; set; } = 24;

        public double Thickness { get; set; } = 2;

        public bool Spin { get; set; } = true;

        public bool ShrinkOut { get; set; } = true;

        public bool Mirror { get; set; } = false;

        private void EnsureVisual()
        {
            if (_visual != null)
            {
                return;
            }

            var compositor = BootStrapper.Current.Compositor;

            var ellipse = compositor.CreateEllipseGeometry();
            ellipse.Radius = new Vector2((float)Radius);
            ellipse.Center = new Vector2((float)Center);
            ellipse.TrimEnd = 0f;

            var shape = compositor.CreateSpriteShape(ellipse);
            shape.CenterPoint = new Vector2((float)Center);
            shape.StrokeThickness = (float)Thickness;
            shape.StrokeStartCap = CompositionStrokeCap.Round;
            shape.StrokeEndCap = CompositionStrokeCap.Round;

            if (Foreground is SolidColorBrush brush)
            {
                _stroke = compositor.CreateColorBrush(brush.Color);
                shape.StrokeBrush = _stroke;
            }

            var visual = compositor.CreateShapeVisual();
            visual.Shapes.Add(shape);
            visual.Size = new Vector2((float)Center * 2);
            visual.CenterPoint = new Vector3((float)Center);

            _visual = visual;
            _shape = shape;
            _ellipse = ellipse;

            _trimStartValue = 0;
            _trimEndValue = 0;

            ElementCompositionPreview.SetElementChildVisual(this, visual);
        }

        private void EnsureAnimations()
        {
            if (_trimStartAnimation != null)
            {
                return;
            }

            var compositor = BootStrapper.Current.Compositor;

            _linearEasing ??= compositor.CreateLinearEasingFunction();

            _trimStartAnimation = compositor.CreateScalarKeyFrameAnimation();
            _trimEndAnimation = compositor.CreateScalarKeyFrameAnimation();

            // The frame at 0 is what snaps the arc back to empty before it grows again.
            _trimStartFromZero = compositor.CreateScalarKeyFrameAnimation();
            _trimStartFromZero.InsertKeyFrame(0, 0);

            _trimEndFromZero = compositor.CreateScalarKeyFrameAnimation();
            _trimEndFromZero.InsertKeyFrame(0, 0);
        }

        protected override void OnApplyTemplate()
        {
            if (_visual != null)
            {
                _ellipse.Radius = new Vector2((float)Radius);
                _ellipse.Center = new Vector2((float)Center);

                _shape.CenterPoint = new Vector2((float)Center);
                _shape.StrokeThickness = (float)Thickness;

                _visual.Size = new Vector2((float)Center * 2);
                _visual.CenterPoint = new Vector3((float)Center);
            }

            base.OnApplyTemplate();
        }

        private void OnForegroundChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (Foreground is not SolidColorBrush brush)
            {
                return;
            }

            if (_stroke != null)
            {
                _stroke.Color = brush.Color;
            }
            else if (_shape != null)
            {
                _stroke = BootStrapper.Current.Compositor.CreateColorBrush(brush.Color);
                _shape.StrokeBrush = _stroke;
            }

            // With no visual yet there is nothing to paint: EnsureVisual reads Foreground.
        }

        protected override void OnLoaded()
        {
            // Value can't be within the spinning range without a visual behind it.
            if (_spinning is false && Value is > 0 and < 1 && Spin)
            {
                StartSpinning();
            }
        }

        protected override void OnUnloaded()
        {
            if (_spinning)
            {
                StopSpinning();
            }
        }

        private void StartSpinning()
        {
            _spinning = true;
            _visual.StartAnimation("RotationAngleInDegrees", _foreverAnimation ??= CreateSpinAnimation());
        }

        private void StopSpinning()
        {
            _spinning = false;
            _visual.StopAnimation("RotationAngleInDegrees");
        }

        private ScalarKeyFrameAnimation CreateSpinAnimation()
        {
            var compositor = BootStrapper.Current.Compositor;
            var easing = _linearEasing ??= compositor.CreateLinearEasingFunction();

            var forever = compositor.CreateScalarKeyFrameAnimation();
            forever.InsertKeyFrame(0, 220, easing);
            forever.InsertKeyFrame(1, 580, easing);
            forever.IterationBehavior = AnimationIterationBehavior.Forever;
            forever.Duration = TimeSpan.FromSeconds(3);

            return forever;
        }

        private double _value;
        public double Value
        {
            get => _value;
            set
            {
                // The same progress arrives repeatedly - once per file update - and 0 while
                // idle. Without this every one of them would run the completion animation.
                if (_value != value)
                {
                    OnValueChanged(_value, _value = value);
                }
            }
        }

        private void OnValueChanged(double oldValue, double newValue)
        {
            if (double.IsNaN(newValue))
            {
                newValue = 0;
            }

            if (newValue > 0)
            {
                newValue = Math.Clamp(newValue, 0.05, 1);
            }
            else
            {
                newValue = Math.Clamp(newValue, 0, 1);
            }

            OnValueChanged((float)oldValue, (float)newValue);
        }

        private void OnValueChanged(float oldValue, float newValue)
        {
            // A wipe that starts and ends on an empty arc, on a ring that has never drawn
            // anything: the case of an already-downloaded file, which reports 1 on every
            // update it takes part in. Nothing to create, nothing to animate. Mirroring
            // swaps 0 and 1, so the bounds hold either way round.
            if (_visual == null && ShrinkOut && newValue is <= 0 or >= 1)
            {
                Completed?.Invoke(this, EventArgs.Empty);
                return;
            }

            EnsureVisual();

            if (_spinning is false && newValue is > 0 and < 1 && Spin)
            {
                StartSpinning();
            }

            if (Mirror)
            {
                oldValue = 1 - oldValue;
                newValue = 1 - newValue;
            }

            var diff = Math.Abs(oldValue - newValue);
            if (diff < 0.10 && newValue < 1 && oldValue != 0 && newValue > 0.10)
            {
                // A small step forward: newValue is necessarily inside (0, 1) here, so the
                // arc stays partial and the new end can simply be written.
                ApplyTrim(0, newValue);
            }
            else if (newValue > 0 && newValue < 1)
            {
                Animate(0, newValue, oldValue == 0, false);
            }
            else
            {
                // Done: the start catches up with the end and wipes the arc out.
                Animate(ShrinkOut ? 1 : 0, 1, oldValue == 0, true);
            }
        }

        /// <summary>
        /// Writes the two ends of the arc, skipping whichever of them hasn't moved - with no
        /// mirroring the start is pinned to 0 and would otherwise be rewritten every update,
        /// re-tessellating the geometry for nothing.
        /// </summary>
        private void ApplyTrim(float start, float end)
        {
            // Writing a property doesn't override an animation running on it: the value is
            // masked and then lost when the animation lands on its own target. NaN is what
            // says one may still be in flight; stopping leaves the trim where it got to.
            if (float.IsNaN(_trimStartValue))
            {
                _ellipse.StopAnimation("TrimStart");
                _ellipse.StopAnimation("TrimEnd");
            }

            var trimStart = Mirror ? end : start;
            var trimEnd = Mirror ? start : end;

            if (_trimStartValue != trimStart)
            {
                _ellipse.TrimStart = _trimStartValue = trimStart;
            }

            if (_trimEndValue != trimEnd)
            {
                _ellipse.TrimEnd = _trimEndValue = trimEnd;
            }
        }

        private void Animate(float start, float end, bool fromZero, bool completing)
        {
            // Off the tree there is nobody to watch the transition, and a recycled row keeps
            // receiving file updates - land on the target and skip the compositor work.
            if (!IsConnected)
            {
                ApplyTrim(start, end);

                if (completing)
                {
                    OnCompleted();
                }

                return;
            }

            EnsureAnimations();

            var startAnimation = fromZero ? _trimStartFromZero : _trimStartAnimation;
            var endAnimation = fromZero ? _trimEndFromZero : _trimEndAnimation;
            var easing = fromZero ? null : _linearEasing;

            startAnimation.InsertKeyFrame(1, start, easing);
            endAnimation.InsertKeyFrame(1, end, easing);

            CompositionScopedBatch batch = null;

            if (completing)
            {
                batch = BootStrapper.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
                batch.Completed += OnBatchCompleted;
            }

            // The trims belong to the compositor for as long as the animation runs, so what
            // they hold can no longer be predicted from here.
            _trimStartValue = float.NaN;
            _trimEndValue = float.NaN;

            _ellipse.StartAnimation(Mirror ? "TrimEnd" : "TrimStart", startAnimation);
            _ellipse.StartAnimation(Mirror ? "TrimStart" : "TrimEnd", endAnimation);

            batch?.End();
        }

        private void OnBatchCompleted(object sender, CompositionBatchCompletedEventArgs args)
        {
            if (sender is CompositionScopedBatch batch)
            {
                batch.Completed -= OnBatchCompleted;
            }

            OnCompleted();
        }

        private void OnCompleted()
        {
            if (_spinning)
            {
                StopSpinning();
            }

            Completed?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler Completed;
    }
}
