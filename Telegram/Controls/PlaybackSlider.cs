//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
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
        }

        protected override void OnApplyTemplate()
        {
            ProgressBarIndicator = GetTemplateChild(nameof(ProgressBarIndicator)) as UIElement;
            ProgressBarThumb = GetTemplateChild(nameof(ProgressBarThumb)) as UIElement;
            ThumbToolTipPopup = GetTemplateChild(nameof(ThumbToolTipPopup)) as Popup;
            ThumbToolTip = GetTemplateChild(nameof(ThumbToolTip)) as ToolTip;

            UpdateValue(_position, _duration, _playing);

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

        private CompositionPropertySet _props;

        public void UpdateValue(double position, double duration, bool playing)
        {
            UpdateValue(TimeSpan.FromSeconds(position), TimeSpan.FromSeconds(duration), playing);
        }

        public void UpdateValue(TimeSpan position, TimeSpan duration, bool playing)
        {
            _position = position;
            _duration = duration;
            _playing = playing;

            if (ProgressBarIndicator == null)
            {
                return;
            }

            var compositor = Window.Current.Compositor;

            var visual = ElementComposition.GetElementVisual(ProgressBarIndicator);
            var clip = (visual.Clip ??= compositor.CreateInsetClip()) as InsetClip;

            var step = (float)(position.TotalSeconds / duration.TotalSeconds);
            if (double.IsNaN(step))
            {
                step = 0;
            }

            _staleTimer?.Stop();

            if (_props == null)
            {
                _props = compositor.CreatePropertySet();
                _props.InsertScalar("Progress", 0);
            }

            if (playing && duration - position > TimeSpan.Zero)
            {
                var linearEasing = compositor.CreateLinearEasingFunction();
                var animation = compositor.CreateScalarKeyFrameAnimation();
                animation.Duration = duration - position;
                animation.InsertKeyFrame(0, step, linearEasing);
                animation.InsertKeyFrame(1, 1, linearEasing);

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
                _props.StopAnimation("Progress");
                _props.InsertScalar("Progress", step);
            }

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

            if (ComputedIsThumbToolTipEnabled)
            {
                var toolTipAnimation = compositor.CreateExpressionAnimation("Vector3(_.Progress * visual.Size.X - this.Target.Size.X / 2, -this.Target.Size.Y - 8, 0)");
                toolTipAnimation.SetReferenceParameter("_", _props);
                toolTipAnimation.SetReferenceParameter("visual", visual);

                var toolTip = ElementComposition.GetElementVisual(ThumbToolTip);
                toolTip.StartAnimation("Offset", toolTipAnimation);

                ThumbToolTip.Shadow = new Windows.UI.Xaml.Media.ThemeShadow();
                ThumbToolTip.Translation = new System.Numerics.Vector3(0, 0, 32);
            }
        }

        private void OnStaleTimerTick(object sender, object e)
        {
            _staleTimer.Stop();

            if (_props == null)
            {
                return;
            }

            var step = (float)(_position.TotalSeconds / _duration.TotalSeconds);
            if (double.IsNaN(step))
            {
                step = 0;
            }

            _props.StopAnimation("Progress");
            _props.InsertScalar("Progress", step);
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
