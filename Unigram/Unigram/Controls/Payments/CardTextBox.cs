using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls.Payments
{
    public class CardTextBox : TextBox
    {
        public String[] PREFIXES_15 = new[] { "34", "37" };
        public String[] PREFIXES_14 = new[] { "300", "301", "302", "303", "304", "305", "309", "36", "38", "39" };
        public String[] PREFIXES_16 = new[]
        {
            "2221", "2222", "2223", "2224", "2225", "2226", "2227", "2228", "2229",
            "223", "224", "225", "226", "227", "228", "229",
            "23", "24", "25", "26",
            "270", "271", "2720",
            "50", "51", "52", "53", "54", "55",

            "4",

            "60", "62", "64", "65",

            "35"
        };

        public static int MAX_LENGTH_STANDARD = 16;
        public static int MAX_LENGTH_AMERICAN_EXPRESS = 15;
        public static int MAX_LENGTH_DINERS_CLUB = 14;

        private string previousText = string.Empty;
        private int selectionStart;

        private int characterAction = -1;
        private int actionPosition;

        private bool ignoreOnCardChange;

        public CardTextBox()
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

        private void OnTextChanging(TextBox sender, TextBoxTextChangingEventArgs e)
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
            String hint = null;
            int maxLength = 100;
            if (builder.Length > 0)
            {
                String currentString = builder.ToString();
                for (int a = 0; a < 3; a++)
                {
                    String[] checkArr;
                    String resultHint;
                    int resultMaxLength;
                    switch (a)
                    {
                        case 0:
                            checkArr = PREFIXES_16;
                            resultMaxLength = 16;
                            resultHint = "xxxx xxxx xxxx xxxx";
                            break;
                        case 1:
                            checkArr = PREFIXES_15;
                            resultMaxLength = 15;
                            resultHint = "xxxx xxxx xxxx xxx";
                            break;
                        case 2:
                        default:
                            checkArr = PREFIXES_14;
                            resultMaxLength = 14;
                            resultHint = "xxxx xxxx xxxx xx";
                            break;
                    }
                    for (int b = 0; b < checkArr.Length; b++)
                    {
                        String prefix = checkArr[b];
                        if (currentString.Length <= prefix.Length)
                        {
                            if (prefix.StartsWith(currentString))
                            {
                                hint = resultHint;
                                maxLength = resultMaxLength;
                                break;
                            }
                        }
                        else
                        {
                            if (currentString.StartsWith(prefix))
                            {
                                hint = resultHint;
                                maxLength = resultMaxLength;
                                break;
                            }
                        }
                    }
                    if (hint != null)
                    {
                        break;
                    }
                }
                if (maxLength != 0)
                {
                    if (builder.Length > maxLength)
                    {
                        builder.Length = maxLength;
                    }
                }
            }
            if (hint != null)
            {
                if (maxLength != 0)
                {
                    if (builder.Length == maxLength)
                    {
                        //inputFields[FIELD_EXPIRE_DATE].requestFocus();
                    }
                }
                //phoneField.setTextColor(Theme.getColor(Theme.key_windowBackgroundWhiteBlackText));
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
            else
            {
                //phoneField.setTextColor(builder.length() > 0 ? Theme.getColor(Theme.key_windowBackgroundWhiteRedText4) : Theme.getColor(Theme.key_windowBackgroundWhiteBlackText));
            }
            Text = builder.ToString();
            if (start >= 0)
            {
                //phoneField.setSelection(start <= phoneField.length() ? start : phoneField.length());
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
