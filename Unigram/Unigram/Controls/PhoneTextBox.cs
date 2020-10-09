using System;
using System.Text;
using Unigram.Common;
using Unigram.Entities;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class PhoneTextBox : TextBox
    {
        private string _previousText = string.Empty;
        private int _selectionStart;

        private int _characterAction = -1;
        private int _actionPosition;

        private bool _ignoreOnPhoneChange;

        private Country _country;

        public PhoneTextBox()
        {
            DefaultStyleKey = typeof(PhoneTextBox);

            Text = "+";

            TextChanging += OnTextChanging;
            TextChanged += OnTextChanged;
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

        private string GetHint(string text)
        {
            var groups = PhoneNumber.Parse(text);
            if (groups.Length < 1)
            {
                _country = null;
                Country = null;

                return null;
            }

            var builder = new StringBuilder();

            for (int i = 0; i < groups.Length; i++)
            {
                for (int j = 0; j < groups[i]; j++)
                {
                    builder.Append('-');
                }

                if (i + 1 < groups.Length)
                {
                    builder.Append(' ');
                }
            }

            if (Country.KeyedCountries.TryGetValue(text.Substring(0, groups[0]), out Country value))
            {
                _country = value;
                Country = value;
            }
            else
            {
                _country = null;
                Country = null;
            }

            return builder.ToString();
        }

        private void OnTextChanging(TextBox sender, TextBoxTextChangingEventArgs w)
        {
            Started();

            if (_ignoreOnPhoneChange)
            {
                return;
            }

            int start = SelectionStart;
            String phoneChars = "0123456789";
            String str = Text;
            if (_characterAction == 3)
            {
                str = str.Substring(0, _actionPosition) + str.Substring(_actionPosition + 1);
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
            _ignoreOnPhoneChange = true;
            String hint = GetHint(builder.ToString());
            if (hint != null)
            {
                for (int a = 0; a < builder.Length; a++)
                {
                    if (a < hint.Length)
                    {
                        if (hint[a] == ' ')
                        {
                            builder.Insert(a, ' ');
                            a++;
                            if (start == a && _characterAction != 2 && _characterAction != 3)
                            {
                                start++;
                            }
                        }
                    }
                    else
                    {
                        builder.Insert(a, ' ');
                        if (start == a + 1 && _characterAction != 2 && _characterAction != 3)
                        {
                            start++;
                        }
                        break;
                    }
                }
            }
            Text = $"+{builder}";
            if (start + 1 >= 0)
            {
                _selectionStart = start + 1 <= Text.Length ? start + 1 : Text.Length;
                SelectionStart = _selectionStart;
            }
            _ignoreOnPhoneChange = false;

            _previousText = Text;
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            SelectionStart = _selectionStart;
        }

        #region Country

        public Country Country
        {
            get { return (Country)GetValue(CountryProperty); }
            set { SetValue(CountryProperty, value); }
        }

        public static readonly DependencyProperty CountryProperty =
            DependencyProperty.Register("Country", typeof(Country), typeof(PhoneTextBox), new PropertyMetadata(null, OnCountryChanged));

        private static void OnCountryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((PhoneTextBox)d).OnCountryChange((Country)e.NewValue, (Country)e.OldValue);
        }

        private void OnCountryChange(Country newValue, Country oldValue)
        {
            if (newValue?.PhoneCode == oldValue?.PhoneCode)
            {
                return;
            }

            if (newValue?.PhoneCode == _country?.PhoneCode)
            {
                return;
            }

            _ignoreOnPhoneChange = true;
            _selectionStart = $"+{newValue?.PhoneCode}".Length;

            Text = $"+{newValue?.PhoneCode}";

            _country = newValue;

            _ignoreOnPhoneChange = false;
            _previousText = string.Empty;

            SelectionStart = Text.Length;
            Started();
        }

        #endregion
    }
}
