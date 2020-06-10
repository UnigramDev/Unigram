using System;
using System.Globalization;
using System.Text;
using Unigram.Common;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class ColorTextBox : TextBox
    {
        private string previousText = string.Empty;
        private int selectionStart;

        private int characterAction = -1;
        private int actionPosition;

        private bool ignoreOnPhoneChange;

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
            var after = Math.Max(0, Text.Length - previousText.Length);
            var count = Math.Max(0, previousText.Length - Text.Length);

            if (count == 0 && after == 1)
            {
                characterAction = 1;
            }
            else if (count == 1 && after == 0)
            {
                if (previousText[start] == ' ' && start > 0)
                {
                    characterAction = 3;
                    actionPosition = start - 1;
                }
                else
                {
                    characterAction = 2;
                }
            }
            else
            {
                characterAction = -1;
            }
        }

        private void OnTextChanging(TextBox sender, TextBoxTextChangingEventArgs w)
        {
            Started();

            if (ignoreOnPhoneChange)
            {
                return;
            }

            int start = SelectionStart;
            String phoneChars = "0123456789ABCDEF";
            String str = Text.ToUpper();
            if (characterAction == 3)
            {
                str = str.Substring(0, actionPosition) + str.Substring(actionPosition + 1);
                start--;
            }
            StringBuilder builder = new StringBuilder(str.Length);
            for (int a = 0; a < str.Length; a++)
            {
                String ch = str.Substring(a, 1);
                if (phoneChars.Contains(ch))
                {
                    builder.Append(ch);
                }
            }
            if (builder.Length == 0 || builder[0] != '#')
            {
                builder.Insert(0, '#');
            }
            ignoreOnPhoneChange = true;
            Text = builder.ToString();
            if (start >= 0)
            {
                selectionStart = start <= Text.Length ? start : Text.Length;
                SelectionStart = Math.Max(1, selectionStart);
            }
            ignoreOnPhoneChange = false;

            previousText = Text;
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            SelectionStart = Math.Max(1, selectionStart);
            ParseColor(Text);
        }

        private void ParseColor(string text)
        {
            text = text.TrimStart('#');

            if (text.Length == 6 && int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int color))
            {
                Color = color.ToColor();
            }
        }

        public event TypedEventHandler<ColorTextBox, ColorChangedEventArgs> ColorChanged;

        #region Color

        public Color Color
        {
            get { return (Color)GetValue(ColorProperty); }
            set { SetValue(ColorProperty, value); }
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

            var value = newValue.ToValue();
            var hex = string.Format("#{0:X6}", value);

            Text = hex;
            ColorChanged?.Invoke(this, new ColorChangedEventArgs(newValue, oldValue));
        }

        #endregion
    }
}
