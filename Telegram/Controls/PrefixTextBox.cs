//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    public class PrefixTextBox : TextBox
    {
        private string _value;

        public PrefixTextBox()
        {
            DefaultStyleKey = typeof(PrefixTextBox);

            BeforeTextChanging += OnBeforeTextChanging;
            TextChanged += OnTextChanged;
        }

        private void OnBeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(args.NewText))
            {
                Text = Prefix;
                SelectionStart = Prefix.Length;
            }
            else
            {
                args.Cancel = !args.NewText.StartsWith(Prefix);
            }
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (Text.Length > Prefix.Length)
            {
                _value = Text.Substring(Prefix.Length);
                Value = _value;
            }
            else
            {
                _value = string.Empty;
                Value = _value;
            }
        }

        #region Prefix

        public string Prefix
        {
            get => (string)GetValue(PrefixProperty);
            set => SetValue(PrefixProperty, value);
        }

        public static readonly DependencyProperty PrefixProperty =
            DependencyProperty.Register("Prefix", typeof(string), typeof(PrefixTextBox), new PropertyMetadata(string.Empty, OnPrefixChanged));

        private static void OnPrefixChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((PrefixTextBox)d).OnPrefixChanged((string)e.NewValue, (string)e.OldValue);
        }

        private void OnPrefixChanged(string newValue, string oldValue)
        {
            Text = (newValue ?? string.Empty) + Value;
        }

        #endregion

        #region Value

        public string Value
        {
            get => (string)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(string), typeof(PrefixTextBox), new PropertyMetadata(string.Empty, OnValueChanged));

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((PrefixTextBox)d).OnValueChanged((string)e.NewValue, (string)e.OldValue);
        }

        private void OnValueChanged(string newValue, string oldValue)
        {
            if (newValue == oldValue)
            {
                return;
            }

            if (newValue == _value)
            {
                return;
            }

            _value = Prefix + (newValue ?? string.Empty);
            Text = _value;
        }

        #endregion
    }
}
