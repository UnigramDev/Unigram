//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Controls
{
    public class LoopingPicker : Button
    {
        private Button NextButton;
        private Button PrevButton;

        private TextBlock ValueText;

        private readonly int _digits = 2;

        public LoopingPicker()
        {
            DefaultStyleKey = typeof(LoopingPicker);
            Click += OnClick;
        }

        private void OnClick(object sender, RoutedEventArgs e)
        {
            Focus(FocusState.Keyboard);
        }

        protected override void OnApplyTemplate()
        {
            NextButton = GetTemplateChild(nameof(NextButton)) as Button;
            PrevButton = GetTemplateChild(nameof(PrevButton)) as Button;

            ValueText = GetTemplateChild(nameof(ValueText)) as TextBlock;
            ValueText.Text = Value.ToString($"D{_digits}");

            NextButton.Click += NextButton_Click;
            PrevButton.Click += PrevButton_Click;

            base.OnApplyTemplate();
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            Focus(FocusState.Keyboard);
            Increase();
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            Focus(FocusState.Keyboard);
            Decrease();
        }

        protected void OnValueChanged(int oldValue, int newValue)
        {
            if (ValueText != null)
            {
                ValueText.Text = newValue.ToString($"D{_digits}");
            }

            ValueChanged?.Invoke(this, new LoopingPickerValueChangedEventArgs(oldValue, newValue));
        }

        protected void OnMaximumChanged(int oldMaximum, int newMaximum)
        {
            //_digits = (int)Math.Floor(Math.Log10(newMaximum) + 1);
        }

        protected override void OnKeyDown(KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Up)
            {
                Increase();
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Down)
            {
                Decrease();
                e.Handled = true;
            }
            else if (e.Key >= VirtualKey.Number0
                && e.Key <= VirtualKey.Number9)
            {
                TypeDigit(e.Key - VirtualKey.Number0);
            }
            else if (e.Key >= VirtualKey.NumberPad0
                && e.Key <= VirtualKey.NumberPad9)
            {
                TypeDigit(e.Key - VirtualKey.NumberPad0);
            }

            base.OnKeyDown(e);
        }

        protected override void OnPointerWheelChanged(PointerRoutedEventArgs e)
        {
            if (FocusState == FocusState.Keyboard)
            {
                var pointer = e.GetCurrentPoint(this);
                if (pointer.Properties.MouseWheelDelta > 0)
                {
                    Increase();
                    e.Handled = true;
                }
                else if (pointer.Properties.MouseWheelDelta < 0)
                {
                    Decrease();
                    e.Handled = true;
                }
            }

            base.OnPointerWheelChanged(e);
        }

        public void Increase()
        {
            if (Value < Maximum)
            {
                Value++;
            }
            else
            {
                Value = 0;
            }
        }

        public void Decrease()
        {
            if (Value > 0)
            {
                Value--;
            }
            else
            {
                Value = Maximum;
            }
        }

        private void TypeDigit(int value)
        {
            var concat = 10 * Value + value;

            var digits = (int)Math.Floor(Math.Log10(concat) + 1);
            if (digits > _digits && concat > Maximum)
            {
                concat %= (int)Math.Pow(10, Math.Ceiling(Math.Log10(concat)) - 1);
            }

            if (concat <= Maximum)
            {
                Value = concat;
            }
            else
            {
                Value = value;
            }
        }

        public event TypedEventHandler<LoopingPicker, LoopingPickerValueChangedEventArgs> ValueChanged;

        #region Value

        public int Value
        {
            get { return (int)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(int), typeof(LoopingPicker), new PropertyMetadata(0, OnValueChanged));

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LoopingPicker)d).OnValueChanged((int)e.OldValue, (int)e.NewValue);
        }

        #endregion

        #region Maximum

        public int Maximum
        {
            get { return (int)GetValue(MaximumProperty); }
            set { SetValue(MaximumProperty, value); }
        }

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register("Maximum", typeof(int), typeof(LoopingPicker), new PropertyMetadata(0, OnMaximumChanged));

        private static void OnMaximumChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LoopingPicker)d).OnMaximumChanged((int)e.OldValue, (int)e.NewValue);
        }

        #endregion

        #region Header

        public string Header
        {
            get { return (string)GetValue(HeaderProperty); }
            set { SetValue(HeaderProperty, value); }
        }

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register("Header", typeof(string), typeof(LoopingPicker), new PropertyMetadata(null));

        #endregion
    }

    public class LoopingPickerValueChangedEventArgs
    {
        public LoopingPickerValueChangedEventArgs(int oldValue, int newValue)
        {
            OldValue = oldValue;
            NewValue = newValue;
        }

        public int OldValue { get; }

        public int NewValue { get; }
    }
}
