//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Telegram.Assets.Icons;
using Telegram.Common;
using Telegram.Navigation;
using Windows.UI;

namespace Telegram.Controls
{
    public partial class DownloadsIndicator : Control
    {
        private readonly IAnimatedVisualSource2 _visualSource;
        private readonly IAnimatedVisual _visual;

        private readonly CompositionPropertySet _properties;
        private readonly ScalarKeyFrameAnimation _animation;

        enum State
        {
            Normal,
            Indeterminate,
            IndeterminateToCompleted,
            Completed
        }

        private State _state;

        public DownloadsIndicator()
        {
            DefaultStyleKey = typeof(DownloadsIndicator);

            var compositor = BootStrapper.Current.Compositor;
            var source = new Downloading();

            var visual = source.TryCreateAnimatedVisual(compositor, out _);
            if (visual == null)
            {
                return;
            }

            _visual = visual;
            _visual.RootVisual.Scale = new System.Numerics.Vector3(0.1f, 0.1f, 1);
            _visualSource = source;

            ThemeChanged();

            var linearEasing = compositor.CreateLinearEasingFunction();

            _animation = compositor.CreateScalarKeyFrameAnimation();
            _animation.Duration = visual.Duration;
            _animation.InsertKeyFrame(1, 60f / 90f, linearEasing);
            //animation.IterationBehavior = AnimationIterationBehavior.Forever;

            _properties = compositor.CreatePropertySet();
            _properties.InsertScalar("Progress", 30f / 90f);

            var progressAnimation = compositor.CreateExpressionAnimation("_.Progress");
            progressAnimation.SetReferenceParameter("_", _properties);
            visual.RootVisual.Properties.InsertScalar("Progress", 0.0F);
            visual.RootVisual.Properties.StartAnimation("Progress", progressAnimation);

            ActualThemeChanged += OnActualThemeChanged;
        }

        protected override void OnApplyTemplate()
        {
            var target = GetTemplateChild("Target") as FrameworkElement;
            if (target != null)
            {
                ElementCompositionPreview.SetElementChildVisual(target, _visual.RootVisual);
            }

            base.OnApplyTemplate();
        }

        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            ThemeChanged();
        }

        private void ThemeChanged()
        {
            if (_visualSource != null)
            {
                var foreground = ActualTheme == ElementTheme.Light ? Colors.Black : Colors.White;
                var background = ActualTheme == ElementTheme.Light ? Colors.White : Colors.Black;
                var stroke = ActualTheme == ElementTheme.Light ? Color.FromArgb(0xFF, 0xE6, 0xE6, 0xE6) : Color.FromArgb(0xFF, 0x1F, 0x1F, 0x1F);
                var accent = Theme.Accent;

                _visualSource.SetColorProperty("Foreground", foreground);
                _visualSource.SetColorProperty("Background", background);
                //_visualSource.SetColorProperty("Stroke", ActualTheme == ElementTheme.Light ? Color.FromArgb(0xFF, 0xF2, 0xF2, 0xF2) : Color.FromArgb(0xFF, 0x2B, 0x2B, 0x2B));
                _visualSource.SetColorProperty("Stroke", _state == State.Normal ? foreground : stroke);
                _visualSource.SetColorProperty("Accent", _state == State.Normal ? foreground : accent);
            }
        }

        private CompositionScopedBatch PrepareBatch()
        {
            var batch = _visual.RootVisual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, e) =>
            {
                if (_state is not State.Indeterminate and not State.IndeterminateToCompleted)
                {
                    return;
                }

                var batch = PrepareBatch();

                var compositor = BootStrapper.Current.Compositor;
                var linearEasing = compositor.CreateLinearEasingFunction();

                _animation.Duration = _visual.Duration / (_state == State.IndeterminateToCompleted ? 3 : 2);
                _animation.InsertKeyFrame(0, _state == State.IndeterminateToCompleted ? 60f / 90f : 0, linearEasing);
                _animation.InsertKeyFrame(1, _state == State.IndeterminateToCompleted ? 1 : 60f / 90f, linearEasing);
                _properties.StartAnimation("Progress", _animation);

                _state = _state == State.IndeterminateToCompleted ? State.Completed : State.Indeterminate;
                batch.End();
            };

            return batch;
        }

        #region Progress

        public double Progress
        {
            get { return (double)GetValue(ProgressProperty); }
            set { SetValue(ProgressProperty, value); }
        }

        public static readonly DependencyProperty ProgressProperty =
            DependencyProperty.Register("Progress", typeof(double), typeof(DownloadsIndicator), new PropertyMetadata(0d, OnProgressChanged));

        private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((DownloadsIndicator)d).OnProgressChanged((double)e.NewValue, (double)e.OldValue);
        }

        private void OnProgressChanged(double newValue, double oldValue)
        {
            if (newValue == 0 && _state != State.Normal)
            {
                _state = State.Normal;
                _properties.InsertScalar("Progress", 30f / 90f);

                ThemeChanged();
            }
            else if (newValue is > 0 and < 1 && _state != State.Indeterminate)
            {
                _state = State.Indeterminate;

                var batch = PrepareBatch();
                _animation.Duration = _visual.Duration / 3;
                _animation.InsertKeyFrame(0, 30f / 90f);
                _animation.InsertKeyFrame(1, 60f / 90f);
                _properties.StartAnimation("Progress", _animation);

                batch.End();

                ThemeChanged();
            }
            else if (newValue == 1 && _state is not State.IndeterminateToCompleted and not State.Completed)
            {
                _state = State.IndeterminateToCompleted;
            }
        }

        #endregion
    }
}
