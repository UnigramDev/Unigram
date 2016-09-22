using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unigram.Common;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Unigram.Controls
{
    public class BubbleTextBox : TextBox
    {
        protected override void OnKeyUp(KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Space)
            {
                var caretPosition = SelectionStart;
                var result = Emoticon.Pattern.Replace(Text, (match) =>
                {
                    var emoticon = match.Groups[1].Value;
                    var emoji = Emoticon.Replace(emoticon);
                    if (match.Index + match.Length < caretPosition)
                    {
                        caretPosition += emoji.Length - emoticon.Length;
                    }
                    if (match.Value.StartsWith(" "))
                    {
                        emoji = $" {emoji}";
                    }

                    return emoji;
                });

                Text = result;
                SelectionStart = caretPosition;
            }

            base.OnKeyUp(e);
        }
    }
}
