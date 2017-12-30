using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class PhoneTextBox : UpdateTextBox
    {
        private string previousText = string.Empty;
        private int selectionStart;

        private int characterAction = -1;
        private int actionPosition;

        private bool ignoreOnPhoneChange;

        public PhoneTextBox()
        {
            TextChanging += OnTextChanging;
            TextChanged += OnTextChanged;
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
            String phoneChars = "0123456789";
            String str = Text.ToString();
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
            ignoreOnPhoneChange = true;
            String hint = PlaceholderText;
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
                            if (start == a && characterAction != 2 && characterAction != 3)
                            {
                                start++;
                            }
                        }
                    }
                    else
                    {
                        builder.Insert(a, ' ');
                        if (start == a + 1 && characterAction != 2 && characterAction != 3)
                        {
                            start++;
                        }
                        break;
                    }
                }
            }
            Text = builder.ToString();
            if (start >= 0)
            {
                selectionStart = start <= Text.Length ? start : Text.Length;
                SelectionStart = selectionStart;
            }
            ignoreOnPhoneChange = false;

            previousText = Text;
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            SelectionStart = selectionStart;
        }
    }
}
