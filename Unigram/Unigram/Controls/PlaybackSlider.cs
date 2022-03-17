using System;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;

namespace Unigram.Controls
{
    public class PlaybackSlider : RangeBase
    {
        private Grid LayoutRoot;

        public PlaybackSlider()
        {
            DefaultStyleKey = typeof(PlaybackSlider);
        }

        protected override void OnApplyTemplate()
        {
            LayoutRoot = GetTemplateChild("LayoutRoot") as Grid;

            LayoutRoot.ColumnDefinitions[0].Width = new GridLength(Value, GridUnitType.Star);
            LayoutRoot.ColumnDefinitions[1].Width = new GridLength(Maximum - Value, GridUnitType.Star);

            base.OnApplyTemplate();
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new PlaybackSliderAutomationPeer(this);
        }

        protected override void OnValueChanged(double oldValue, double newValue)
        {
            base.OnValueChanged(oldValue, newValue);

            if (LayoutRoot != null)
            {
                LayoutRoot.ColumnDefinitions[0].Width = new GridLength(newValue, GridUnitType.Star);
                LayoutRoot.ColumnDefinitions[1].Width = new GridLength(Maximum - newValue, GridUnitType.Star);
            }
        }

        protected override void OnMaximumChanged(double oldMaximum, double newMaximum)
        {
            base.OnMaximumChanged(oldMaximum, newMaximum);

            if (LayoutRoot != null)
            {
                LayoutRoot.ColumnDefinitions[0].Width = new GridLength(Value, GridUnitType.Star);
                LayoutRoot.ColumnDefinitions[1].Width = new GridLength(newMaximum - Value, GridUnitType.Star);
            }
        }

        private bool _pressed;
        private bool _entered;

        protected override void OnPointerEntered(PointerRoutedEventArgs e)
        {
            _entered = true;
            VisualStateManager.GoToState(this, "PointerOver", true);

            base.OnPointerEntered(e);
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            _pressed = true;
            VisualStateManager.GoToState(this, "PointerOver", true);
            CapturePointer(e.Pointer);

            var point = e.GetCurrentPoint(this);
            Value = point.Position.X / ActualWidth * Maximum;

            base.OnPointerPressed(e);
        }

        protected override void OnPointerMoved(PointerRoutedEventArgs e)
        {
            if (_pressed)
            {
                var point = e.GetCurrentPoint(this);
                Value = point.Position.X / ActualWidth * Maximum;
            }

            VisualStateManager.GoToState(this, "PointerOver", true);

            base.OnPointerMoved(e);
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

            base.OnPointerExited(e);
        }

        protected override void OnPointerCanceled(PointerRoutedEventArgs e)
        {
            _pressed = false;
            UpdateVisualState(e);

            base.OnPointerCanceled(e);
        }

        protected override void OnPointerCaptureLost(PointerRoutedEventArgs e)
        {
            _pressed = false;
            UpdateVisualState(e);

            base.OnPointerCaptureLost(e);
        }

        protected override void OnPointerReleased(PointerRoutedEventArgs e)
        {
            _pressed = false;
            ReleasePointerCapture(e.Pointer);
            UpdateVisualState(e);

            base.OnPointerReleased(e);
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
    }

    public class PlaybackSliderAutomationPeer : RangeBaseAutomationPeer, IValueProvider
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
            if (patternInterface == PatternInterface.Value)
            {
                return this;
            }

            return base.GetPatternCore(patternInterface);
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.Slider;
        }

        public string Value
        {
            get
            {
                return ReadableTime((int)_owner.Value);
            }
        }

        public void SetValue(string value)
        {
        }

        public bool IsReadOnly
        {
            get
            {
                return true;
            }
        }

        private static string ReadableTime(double seconds)
        {
            var duration = TimeSpan.FromSeconds(seconds);
            if (duration.TotalHours >= 1)
            {
                return duration.ToString("h\\:mm\\:ss");
            }
            else
            {
                return duration.ToString("mm\\:ss");
            }
        }




    }
}
