using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
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

            var user = value as TLUser;
            if (user != null)
            {
                word1 = user.FirstName ?? string.Empty;
                word2 = user.LastName ?? string.Empty;
            }

            var chat = value as TLChatBase;
            if (chat != null)
            {
                var words = chat.DisplayName.Split(new char[] { ' ' });
                if (words.Length > 0)
                {
                    word1 = words[0];
                    word2 = string.Empty;

                    //if (words.Length == 1)
                    //{
                    //    var si = StringInfo.GetTextElementEnumerator(chat.FullName);

                    //    word1 = si.MoveNext() ? si.GetTextElement() : string.Empty;
                    //    word2 = si.MoveNext() ? si.GetTextElement() : string.Empty;
                    //}
                    //else
                    //{
                    //    word1 = words[0];
                    //    word2 = words[words.Length - 1];
                    //}
                }
            }

            if (chat == null && user == null)
            {
                var str = value as string;
                if (str != null)
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
