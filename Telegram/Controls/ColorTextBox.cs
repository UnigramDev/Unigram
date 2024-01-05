//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Globalization;
using System.Text;
using Telegram.Common;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    public class ColorTextBox : TextBox
    {
        private string _previousText = string.Empty;
        private int _selectionStart;

        private int _characterAction = -1;
        private int _actionPosition;

        private bool _ignoreOnPhoneChange;

        private int _hexDigits = 6;

        public ColorTextBox()
        {
            DefaultStyleKey = typeof(ColorTextBox);

            TextChanging += OnTextChanging;
            TextChanged += OnTextChanged;

            MaxLength = 7;
        }

        private void Started()
        {
            var start = SelectionStart;
            var after = Math.Max(0, Text.Length - _previousText.Length);
            var count = Math.Max(0, _previousText.Length - Text.Length);

            if (count == 0 && after == 1)
            {
                _characterAction = 1;
            }
            else if (count == 1 && after == 0)
            {
                if (_previousText[start] == ' ' && start > 0)
                {
                    _characterAction = 3;
                    _actionPosition = start - 1;
                }
                else
                {
                    _characterAction = 2;
                }
            }
            else
            {
                _characterAction = -1;
            }
        }

        private void OnTextChanging(TextBox sender, TextBoxTextChangingEventArgs w)
        {
            Started();

            if (_ignoreOnPhoneChange)
            {
                return;
            }

            int start = SelectionStart;
            string phoneChars = "0123456789ABCDEF";
            string str = Text.ToUpper();
            if (_characterAction == 3)
            {
                str = str.Substring(0, _actionPosition) + str.Substring(_actionPosition + 1);
                start--;
            }
            StringBuilder builder = new StringBuilder(str.Length);
            for (int a = 0; a < str.Length; a++)
            {
                string ch = str.Substring(a, 1);
                if (phoneChars.Contains(ch))
                {
                    builder.Append(ch);
                }
            }
            if (builder.Length == 0 || builder[0] != '#')
            {
                builder.Insert(0, '#');
            }
            _ignoreOnPhoneChange = true;
            Text = builder.ToString();
            if (start >= 0)
            {
                _selectionStart = start <= Text.Length ? start : Text.Length;
                SelectionStart = Math.Max(1, _selectionStart);
            }
            _ignoreOnPhoneChange = false;

            _previousText = Text;
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            SelectionStart = Math.Max(1, _selectionStart);
            ParseColor(Text);
        }

        private void ParseColor(string text)
        {
            text = text.TrimStart('#');

            if (text.Length == _hexDigits && int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int color))
            {
                Color = color.ToColor(IsTransparencyEnabled);
            }
        }

        public event TypedEventHandler<ColorTextBox, ColorChangedEventArgs> ColorChanged;

        #region Color

        public Color Color
        {
            get => (Color)GetValue(ColorProperty);
            set => SetValue(ColorProperty, value);
        }

        public static readonly DependencyProperty ColorProperty =
            DependencyProperty.Register("Color", typeof(Color), typeof(ColorTextBox), new PropertyMetadata(default(Color), OnColorChanged));

        private static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ColorTextBox)d).OnColorChanged((Color)e.NewValue, (Color)e.OldValue);
        }

        protected virtual void OnColorChanged(Color newValue, Color oldValue)
        {
            if (newValue == oldValue)
            {
                return;
            }

            if (newValue == default)
            {
                return;
            }

            var value = newValue.ToValue(IsTransparencyEnabled);
            var hex = IsTransparencyEnabled ? string.Format("#{0:X8}", value) : string.Format("#{0:X6}", value);

            Text = hex;
            ColorChanged?.Invoke(this, new ColorChangedEventArgs(newValue, oldValue));
        }

        #endregion

        #region IsTransparencyEnabled

        public bool IsTransparencyEnabled
        {
            get { return (bool)GetValue(IsTransparencyEnabledProperty); }
            set { SetValue(IsTransparencyEnabledProperty, value); }
        }

        public static readonly DependencyProperty IsTransparencyEnabledProperty =
            DependencyProperty.Register("IsTransparencyEnabled", typeof(bool), typeof(ColorTextBox), new PropertyMetadata(false, OnTransparencyEnabledChanged));

        private static void OnTransparencyEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ColorTextBox)d).OnTransparencyEnabledChanged((bool)e.NewValue, (bool)e.OldValue);
        }

        protected virtual void OnTransparencyEnabledChanged(bool newValue, bool oldValue)
        {
            if (newValue == oldValue)
            {
                return;
            }

            _hexDigits = newValue ? 8 : 6;
            MaxLength = newValue ? 9 : 7;

            var value = Color.ToValue(newValue);
            var hex = newValue ? string.Format("#{0:X8}", value) : string.Format("#{0:X6}", value);

            Text = hex;
        }

        #endregion
    }
}
