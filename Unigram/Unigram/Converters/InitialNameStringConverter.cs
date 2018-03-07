using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class InitialNameStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return Convert(value);
        }

        public static string Convert(object value)
        {
            if (value == null)
            {
                return null;
            }

            var word1 = string.Empty;
            var word2 = string.Empty;

            if (value is User user)
            {
                word1 = user.FirstName ?? string.Empty;
                word2 = user.LastName ?? string.Empty;
            }
            else if (value is Chat chat)
            {
                var words = chat.Title.Split(new char[] { ' ' });
                if (words.Length > 1 && chat.Type is ChatTypePrivate || chat.Type is ChatTypeSecret)
                {
                    word1 = words[0];
                    word2 = words[words.Length - 1];
                }
                else if (words.Length > 0)
                {
                    word1 = words[0];
                    word2 = string.Empty;
                }
            }
            else if (value is ChatInviteLinkInfo info)
            {
                var words = info.Title.Split(new char[] { ' ' });
                if (words.Length > 0)
                {
                    word1 = words[0];
                    word2 = string.Empty;
                }
            }
            else if (value is string str)
            {
                var words = str.Split(new char[] { ' ' });
                if (words.Length > 1)
                {
                    word1 = words[0];
                    word2 = words[words.Length - 1];
                }
                else
                {
                    word1 = words[0];
                    word2 = string.Empty;
                }
            }

            var si1 = StringInfo.GetTextElementEnumerator(word1);
            var si2 = StringInfo.GetTextElementEnumerator(word2);

            word1 = si1.MoveNext() ? si1.GetTextElement() : string.Empty;
            word2 = si2.MoveNext() ? si2.GetTextElement() : string.Empty;

            return string.Format("{0}{1}", word1, word2).Trim().ToUpperInvariant();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotSupportedException();
        }
    }
}
