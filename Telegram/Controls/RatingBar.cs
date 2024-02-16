//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
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

            Value = index + 1;
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
            var value = Value - 1;

            for (int i = 0; i < _unselected.Count; i++)
            {
                var brush = i <= value || (i <= index) ? "TextControlBorderBrushFocused" : "TextControlBorderBrush";
                var glyph = i <= value && (index == -1 || i <= index) ? Icons.StarFilled : Icons.Star;

                _unselected[i].Foreground = BootStrapper.Current.Resources[brush] as SolidColorBrush;
                _unselected[i].Glyph = glyph;
            }
        }

        public event RatingBarValueChangedEventHandler ValueChanged;

        #region Value

        public int Value
        {
            get => (int)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(int), typeof(RatingBar), new PropertyMetadata(0, OnValueChanged));

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
