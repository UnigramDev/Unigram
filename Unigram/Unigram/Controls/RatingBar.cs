using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    public class RatingBar : Control
    {
        private readonly Dictionary<int, FontIcon> _unselected = new Dictionary<int, FontIcon>();

        public RatingBar()
        {
            DefaultStyleKey = typeof(RatingBar);
        }

        protected override void OnApplyTemplate()
        {
            for (int i = 0; i < 5; i++)
            {
                _unselected[i] = GetTemplateChild($"Star{i}") as FontIcon;
            }

            if (Windows.ApplicationModel.DesignMode.DesignModeEnabled)
            {
                return;
            }

            PointerMoved += OnPointerMoved;
            PointerExited += OnPointerExited;
            PointerCaptureLost += OnPointerCaptureLost;
            PointerPressed += OnPointerPressed;
            PointerReleased += OnPointerReleased;

            UpdateVisual();
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            UpdateVisual(e);
        }

        private void OnPointerExited(object sender, PointerRoutedEventArgs e)
        {
            UpdateVisual();
        }

        private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            UpdateVisual();
        }

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            CapturePointer(e.Pointer);
            UpdateVisual(e);
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            var position = e.GetCurrentPoint(this);
            var index = (int)Math.Truncate(position.Position.X / (FontSize + Padding.Left + Padding.Right));

            Value = index;
            ReleasePointerCapture(e.Pointer);
            UpdateVisual(e);
        }

        private void UpdateVisual(PointerRoutedEventArgs e)
        {
            var position = e.GetCurrentPoint(this);
            var index = (int)Math.Truncate(position.Position.X / (FontSize + Padding.Left + Padding.Right));
            UpdateVisual(index);
        }

        private void UpdateVisual(int index = -1)
        {
            for (int i = 0; i < _unselected.Count; i++)
            {
                var brush = i <= Value || (i <= index) ? "TextControlBorderBrushFocused" : "TextControlBorderBrush";
                var glyph = i <= Value && (index == -1 ? true : i <= index) ? "\uE1CF" : "\uE1CE";

                _unselected[i].Foreground = App.Current.Resources[brush] as SolidColorBrush;
                _unselected[i].Glyph = glyph;
            }
        }

        public event RatingBarValueChangedEventHandler ValueChanged;

        #region Value

        public int Value
        {
            get { return (int)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(int), typeof(RatingBar), new PropertyMetadata(-1, OnValueChanged));

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((RatingBar)d).OnValueChanged((int)e.NewValue, (int)e.OldValue);
        }

        private void OnValueChanged(int newValue, int oldValue)
        {
            if (Windows.ApplicationModel.DesignMode.DesignModeEnabled)
            {
                return;
            }

            UpdateVisual();
            ValueChanged?.Invoke(this, new RatingBarValueChangedEventArgs(newValue, oldValue));
        }

        #endregion
    }

    public delegate void RatingBarValueChangedEventHandler(object sender, RatingBarValueChangedEventArgs e);

    public class RatingBarValueChangedEventArgs : EventArgs
    {
        public RatingBarValueChangedEventArgs(int newValue, int oldValue)
        {
            NewValue = newValue;
            OldValue = oldValue;
        }

        public int NewValue { get; }

        public int OldValue { get; }
    }
}
