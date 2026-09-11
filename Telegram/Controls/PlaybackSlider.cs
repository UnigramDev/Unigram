//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Navigation;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;

namespace Telegram.Controls
{
    public record PlaybackSliderPositionChanged(TimeSpan NewPosition);

    public partial class PlaybackSlider : Control
    {
        private UIElement ProgressBarIndicator;
        private UIElement ProgressBarThumb;
        private Popup ThumbToolTipPopup;
        private ToolTip ThumbToolTip;

        private DispatcherTimer _staleTimer;

        public PlaybackSlider()
        {
            DefaultStyleKey = typeof(PlaybackSlider);

            Unloaded += OnUnloaded;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _staleTimer?.Stop();

            // The animation runs to the end of the track, so a recycled control would otherwise
            // keep one alive for as long as the track lasts.
            Freeze();
        }

        protected override void OnApplyTemplate()
        {
            ProgressBarIndicator = GetTemplateChild(nameof(ProgressBarIndicator)) as UIElement;
            ProgressBarThumb = GetTemplateChild(nameof(ProgressBarThumb)) as UIElement;
            ThumbToolTipPopup = GetTemplateChild(nameof(ThumbToolTipPopup)) as Popup;
            ThumbToolTip = GetTemplateChild(nameof(ThumbToolTip)) as ToolTip;

            // The expressions are bound to the visuals of the template that was replaced.
            _expressionsStarted = false;
            _toolTipStarted = false;

            UpdateValue(_position, _duration, _playing, _rate);

            base.OnApplyTemplate();
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new PlaybackSliderAutomationPeer(this);
        }

        private bool _pressed;
        private bool _entered;

        public bool IsScrubbing => _pressed;

        public TimeSpan Position => _position;

        public TimeSpan Duration => _duration;

        public event TypedEventHandler<PlaybackSlider, PlaybackSliderPositionChanged> PositionStarted;
        public event TypedEventHandler<PlaybackSlider, PlaybackSliderPositionChanged> PositionChanging;
        public event TypedEventHandler<PlaybackSlider, PlaybackSliderPositionChanged> PositionChanged;
        public event TypedEventHandler<PlaybackSlider, object> PositionCanceled;

        private TimeSpan _position;
        private TimeSpan _duration;
        private bool _playing;
        private double _rate = 1;

        // Where the running animation was started from, and when. With the rate they say where
        // the bar is drawn at any instant, which is what an update landing slightly behind is
        // measured against.
        private TimeSpan _origin;
        private ulong _originTicks;

        private CompositionPropertySet _props;
        private LinearEasingFunction _easing;

        private bool _expressionsStarted;
        private bool _toolTipStarted;

        // A position update describes a moment that has already passed by the time it lands, so
        // a report a little behind the drawn value is the reporting delay rather than a move:
        // carrying on from where the bar is keeps it from stuttering backwards several times a
        // second. Anything further off than this is a seek, and still snaps.
        private static readonly TimeSpan MaxLead = TimeSpan.FromMilliseconds(250);

        // Composition rejects a zero-length animation, and a track this close to its end has
        // nothing left to animate anyway.
        private static readonly TimeSpan MinDuration = TimeSpan.FromMilliseconds(1);

        public void UpdateValue(double position, double duration, bool playing, double rate = 1)
        {
            UpdateValue(TimeSpan.FromSeconds(position), TimeSpan.FromSeconds(duration), playing, rate);
        }

        public void UpdateValue(TimeSpan position, TimeSpan duration, bool playing, double rate = 1)
        {
            var drawn = DrawnPosition();
            var wasPlaying = _playing;

            _position = position;
            _duration = duration;
            _playing = playing;
            _rate = rate > 0 ? rate : 1;

            if (ProgressBarIndicator == null)
            {
                return;
            }

            var compositor = BootStrapper.Current.Compositor;
            var visual = ElementComposition.GetElementVisual(ProgressBarIndicator);

            if (_props == null)
            {
                _props = compositor.CreatePropertySet();
                _props.InsertScalar("Progress", 0);
            }

            EnsureExpressions(compositor, visual);

            _staleTimer?.Stop();

            // Scrubbing is the one case where the bar has to land exactly where it is told.
            var origin = position;
            if (wasPlaying && !_pressed && drawn > position && drawn - position <= MaxLead)
            {
                origin = drawn;
            }

            _origin = origin;
            _originTicks = Logger.TickCount;

            // The bar is a straight line from where the player says it is to the end of the
            // track, re-based by every update. Media time and wall time are only the same thing
            // at 1x, so the remainder has to be divided by the rate the player is running at.
            var remaining = TimeSpan.FromTicks((long)((duration - origin).Ticks / _rate));

            if (playing && remaining >= MinDuration)
            {
                _easing ??= compositor.CreateLinearEasingFunction();

                var animation = compositor.CreateScalarKeyFrameAnimation();
                animation.Duration = remaining;
                animation.InsertKeyFrame(0, ToStep(origin), _easing);
                animation.InsertKeyFrame(1, 1, _easing);

                _props.StartAnimation("Progress", animation);

                if (_staleTimer == null)
                {
                    _staleTimer = new DispatcherTimer();
                    _staleTimer.Interval = TimeSpan.FromSeconds(1);
                    _staleTimer.Tick += OnStaleTimerTick;
                }

                _staleTimer.Start();
            }
            else
            {
                _playing = false;

                _props.StopAnimation("Progress");
                _props.InsertScalar("Progress", ToStep(origin));
            }
        }

        /// <summary>
        /// Starts the expressions that map Progress onto the template, once per template: every
        /// update writes the property set they read, so they never have to be started again.
        /// </summary>
        private void EnsureExpressions(Compositor compositor, Visual visual)
        {
            if (_expressionsStarted)
            {
                EnsureThumbToolTip(compositor, visual);
                return;
            }

            _expressionsStarted = true;

            var clip = (visual.Clip ??= compositor.CreateInsetClip()) as InsetClip;

            var progressAnimation = compositor.CreateExpressionAnimation("visual.Size.X - (_.Progress * visual.Size.X)");
            progressAnimation.SetReferenceParameter("_", _props);
            progressAnimation.SetReferenceParameter("visual", visual);

            clip.StartAnimation("RightInset", progressAnimation);

            if (ProgressBarThumb != null)
            {
                var thumbAnimation = compositor.CreateExpressionAnimation("_.Progress * visual.Size.X");
                thumbAnimation.SetReferenceParameter("_", _props);
                thumbAnimation.SetReferenceParameter("visual", visual);

                var thumb = ElementComposition.GetElementVisual(ProgressBarThumb);
                thumb.StartAnimation("Offset.X", thumbAnimation);
            }

            EnsureThumbToolTip(compositor, visual);
        }

        // Unlike the rest of the template this one is switched on and off while the control is
        // loaded, so it can't be bound once and forgotten.
        private void EnsureThumbToolTip(Compositor compositor, Visual visual)
        {
            if (_toolTipStarted || !ComputedIsThumbToolTipEnabled)
            {
                return;
            }

            _toolTipStarted = true;

            var toolTipAnimation = compositor.CreateExpressionAnimation("Vector3(_.Progress * visual.Size.X - this.Target.Size.X / 2, -this.Target.Size.Y - 8, 0)");
            toolTipAnimation.SetReferenceParameter("_", _props);
            toolTipAnimation.SetReferenceParameter("visual", visual);

            var toolTip = ElementComposition.GetElementVisual(ThumbToolTip);
            toolTip.StartAnimation("Offset", toolTipAnimation);

            ThumbToolTip.Shadow = new Windows.UI.Xaml.Media.ThemeShadow();
            ThumbToolTip.Translation = new System.Numerics.Vector3(0, 0, 32);
        }

        /// <summary>
        /// Where the bar is drawn at this instant: the point the running animation started from
        /// plus the media time that has passed since, which is wall time scaled by the rate.
        /// </summary>
        private TimeSpan DrawnPosition()
        {
            if (!_playing)
            {
                return _origin;
            }

            var drawn = _origin + TimeSpan.FromMilliseconds((Logger.TickCount - _originTicks) * _rate);
            return drawn > _duration ? _duration : drawn;
        }

        private float ToStep(TimeSpan position)
        {
            var step = (float)(position.TotalSeconds / _duration.TotalSeconds);
            if (float.IsNaN(step) || float.IsInfinity(step))
            {
                return 0;
            }

            return Math.Clamp(step, 0f, 1f);
        }

        private void OnStaleTimerTick(object sender, object e)
        {
            _staleTimer.Stop();

            // Updates have stopped arriving, so the player has stalled or gone away. Holding the
            // bar where it is drawn is what keeps it from snapping back to a report that is a
            // whole interval old by now.
            Freeze();
        }

        private void Freeze()
        {
            if (_props == null || !_playing)
            {
                return;
            }

            var drawn = DrawnPosition();

            _origin = drawn;
            _playing = false;

            _props.StopAnimation("Progress");
            _props.InsertScalar("Progress", ToStep(drawn));
        }

        protected override void OnPointerEntered(PointerRoutedEventArgs e)
        {
            _entered = true;
            VisualStateManager.GoToState(this, "PointerOver", true);
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(this);
            if (point.Properties.IsLeftButtonPressed)
            {
                _pressed = true;
                VisualStateManager.GoToState(this, "PointerOver", true);
                CapturePointer(e.Pointer);

                PositionStarted?.Invoke(this, null);

                var position = CalculatePosition(point);
                UpdateValue(position, _duration, false);
                PositionChanging?.Invoke(this, new PlaybackSliderPositionChanged(position));

                if (ComputedIsThumbToolTipEnabled)
                {
                    ThumbToolTipPopup.IsOpen = true;
                }
            }
        }

        protected override void OnPointerMoved(PointerRoutedEventArgs e)
        {
            if (_pressed)
            {
                var position = CalculatePosition(e.GetCurrentPoint(this));
                UpdateValue(position, _duration, false);
                PositionChanging?.Invoke(this, new PlaybackSliderPositionChanged(position));
            }

            VisualStateManager.GoToState(this, "PointerOver", true);
        }

        protected override void OnPointerExited(PointerRoutedEventArgs e)
        {
            if (_pressed)
            {
                _entered = true;
                VisualStateManager.GoToState(this, "PointerOver", true);
            }
            else
            {
                _entered = false;
                VisualStateManager.GoToState(this, "Normal", true);
            }
        }

        protected override void OnPointerCanceled(PointerRoutedEventArgs e)
        {
            if (_pressed)
            {
                PositionCanceled?.Invoke(this, null);
            }

            _pressed = false;
            UpdateVisualState(e);

            if (ComputedIsThumbToolTipEnabled)
            {
                ThumbToolTipPopup.IsOpen = false;
            }
        }

        protected override void OnPointerCaptureLost(PointerRoutedEventArgs e)
        {
            if (_pressed)
            {
                PositionCanceled?.Invoke(this, null);
            }

            _pressed = false;
            UpdateVisualState(e);

            if (ComputedIsThumbToolTipEnabled)
            {
                ThumbToolTipPopup.IsOpen = false;
            }
        }

        protected override void OnPointerReleased(PointerRoutedEventArgs e)
        {
            if (_pressed)
            {
                SetValue(CalculatePosition(e.GetCurrentPoint(this)));
            }

            _pressed = false;
            ReleasePointerCapture(e.Pointer);
            UpdateVisualState(e);

            if (ComputedIsThumbToolTipEnabled)
            {
                ThumbToolTipPopup.IsOpen = false;
            }
        }

        private void UpdateVisualState(PointerRoutedEventArgs e)
        {
            var pointer = e.GetCurrentPoint(this);
            if (pointer.Position.X >= 0 && pointer.Position.Y >= 0 && pointer.Position.X <= ActualWidth && pointer.Position.Y <= ActualHeight)
            {
                _entered = true;
                VisualStateManager.GoToState(this, "PointerOver", true);
            }
            else
            {
                _entered = false;
                VisualStateManager.GoToState(this, "Normal", true);
            }
        }

        private TimeSpan CalculatePosition(PointerPoint point)
        {
            return TimeSpan.FromSeconds(Math.Clamp(point.Position.X, 0, ActualWidth) / ActualWidth * _duration.TotalSeconds);
        }

        private void SetValue(TimeSpan position)
        {
            PositionChanged?.Invoke(this, new PlaybackSliderPositionChanged(position));
        }

        public void SetValue(double position, double duration, bool playing)
        {
            if (duration > 0)
            {
                position = Math.Clamp(position, 0, duration);
            }

            UpdateValue(TimeSpan.FromSeconds(position), TimeSpan.FromSeconds(duration), playing);
            PositionChanged?.Invoke(this, new PlaybackSliderPositionChanged(TimeSpan.FromSeconds(position)));
        }

        public bool ComputedIsThumbToolTipEnabled => IsThumbToolTipEnabled && ThumbToolTip != null && ThumbToolTipPopup != null;

        #region IsThumbToolTipEnabled

        public bool IsThumbToolTipEnabled
        {
            get { return (bool)GetValue(IsThumbToolTipEnabledProperty); }
            set { SetValue(IsThumbToolTipEnabledProperty, value); }
        }

        public static readonly DependencyProperty IsThumbToolTipEnabledProperty =
            DependencyProperty.Register("IsThumbToolTipEnabled", typeof(bool), typeof(PlaybackSlider), new PropertyMetadata(false));

        #endregion

        #region ThumbToolTipContent

        public object ThumbToolTipContent
        {
            get { return (object)GetValue(ThumbToolTipContentProperty); }
            set { SetValue(ThumbToolTipContentProperty, value); }
        }

        public static readonly DependencyProperty ThumbToolTipContentProperty =
            DependencyProperty.Register("ThumbToolTipContent", typeof(object), typeof(PlaybackSlider), new PropertyMetadata(null));

        #endregion
    }

    public partial class PlaybackSliderAutomationPeer : FrameworkElementAutomationPeer, IRangeValueProvider, IValueProvider
    {
        private readonly PlaybackSlider _owner;

        public PlaybackSliderAutomationPeer(PlaybackSlider owner)
            : base(owner)
        {
            _owner = owner;
        }

        protected override string GetClassNameCore()
        {
            return "Slider";
        }

        protected override string GetNameCore()
        {
            return "Seek";
        }

        protected override object GetPatternCore(PatternInterface patternInterface)
        {
            if (patternInterface is PatternInterface.RangeValue or PatternInterface.Value)
            {
                return this;
            }

            return base.GetPatternCore(patternInterface);
        }

        public bool IsReadOnly => false;

        public double LargeChange => 1;

        public double SmallChange => 1;

        public double Minimum => 0;

        public double Maximum => _owner.Duration.TotalSeconds;

        public double Value => _owner.Position.TotalSeconds;

        string IValueProvider.Value => _owner.Position.ToDuration();

        public void SetValue(double value)
        {
            throw new NotImplementedException();
        }

        public void SetValue(string value)
        {
            throw new NotImplementedException();
        }
    }
}
