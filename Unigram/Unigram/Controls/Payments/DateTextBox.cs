using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls.Payments
{
    public class DateTextBox : TextBox
    {
        private string previousText = string.Empty;
        private int selectionStart;

        private int characterAction = -1;
        private bool isYear;
        private int actionPosition;

        private bool ignoreOnCardChange;

        public DateTextBox()
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
                isYear = Text.IndexOf('/') != -1;
                characterAction = 1;
            }
            else if (count == 1 && after == 0)
            {
                if (previousText[start] == '/' && start > 0)
                {
                    isYear = false;
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

            if (ignoreOnCardChange)
            {
                return;
            }

            int start = SelectionStart;
            String str = Text;
            if (characterAction == 3)
            {
                str = str.Substring(0, actionPosition) + str.Substring(actionPosition + 1);
                start--;
            }
            StringBuilder builder = new StringBuilder(str.Length);
            for (int a = 0; a < str.Length; a++)
            {
                char ch = str[a];
                if (char.IsDigit(ch))
                {
                    builder.Append(ch);
                }
            }
            ignoreOnCardChange = true;
            //inputFields[FIELD_EXPIRE_DATE].setTextColor(Theme.getColor(Theme.key_windowBackgroundWhiteBlackText));
            if (builder.Length > 4)
            {
                builder.Length = 4;
            }
            if (builder.Length < 2)
            {
                isYear = false;
            }
            bool isError = false;
            if (isYear)
            {
                String[] args = new String[builder.Length > 2 ? 2 : 1];
                args[0] = builder.ToString().Substring(0, 2);
                if (args.Length == 2)
                {
                    args[1] = builder.ToString().Substring(2);
                }
                if (builder.Length == 4 && args.Length == 2)
                {
                    int month = int.Parse(args[0]);
                    int year = int.Parse(args[1]) + 2000;
                    DateTime rightNow = DateTime.Now;
                    int currentYear = rightNow.Year;
                    int currentMonth = rightNow.Month + 1;
                    if (year < currentYear || year == currentYear && month < currentMonth)
                    {
                        //inputFields[FIELD_EXPIRE_DATE].setTextColor(Theme.getColor(Theme.key_windowBackgroundWhiteRedText4));
                        isError = true;
                    }
                }
                else
                {
                    int value = int.Parse(args[0]);
                    if (value > 12 || value == 0)
                    {
                        //inputFields[FIELD_EXPIRE_DATE].setTextColor(Theme.getColor(Theme.key_windowBackgroundWhiteRedText4));
                        isError = true;
                    }
                }
            }
            else
            {
                if (builder.Length == 1)
                {
                    int value = int.Parse(builder.ToString());
                    if (value != 1 && value != 0)
                    {
                        builder.Insert(0, "0");
                        start++;
                    }
                }
                else if (builder.Length == 2)
                {
                    int value = int.Parse(builder.ToString());
                    if (value > 12 || value == 0)
                    {
                        //inputFields[FIELD_EXPIRE_DATE].setTextColor(Theme.getColor(Theme.key_windowBackgroundWhiteRedText4));
                        isError = true;
                    }
                    start++;
                }
            }
            if (!isError && builder.Length == 4)
            {
                //inputFields[need_card_name ? FIELD_CARDNAME : FIELD_CVV].requestFocus();
            }
            if (builder.Length == 2)
            {
                builder.Append('/');
                start++;
            }
            else if (builder.Length > 2 && builder[2] != '/')
            {
                builder.Insert(2, '/');
                start++;
            }

            Text = builder.ToString();
            if (start >= 0)
            {
                selectionStart = start <= Text.Length ? start : Text.Length;
                SelectionStart = selectionStart;
            }
            ignoreOnCardChange = false;

            previousText = Text;
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            SelectionStart = selectionStart;
        }
    }
}
