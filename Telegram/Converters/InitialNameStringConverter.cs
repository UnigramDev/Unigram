//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Globalization;

namespace Telegram.Converters
{
    public partial class InitialNameStringConverter
    {
        public static string Convert(string title, bool split = false)
        {
            title ??= string.Empty;

            var word1 = string.Empty;
            var word2 = string.Empty;

            var words = title.Split(new char[] { ' ' });
            if (words.Length > 1 && split)
            {
                word1 = words[0];
                word2 = words[words.Length - 1];
            }
            else
            {
                word1 = words[0];
                word2 = string.Empty;
            }

            return Convert(word1, word2);
        }

        public static string Convert(string word1, string word2)
        {
            word1 ??= string.Empty;
            word2 ??= string.Empty;

            var si1 = StringInfo.GetTextElementEnumerator(word1);
            var si2 = StringInfo.GetTextElementEnumerator(word2);

            word1 = si1.MoveNext() ? si1.GetTextElement() : string.Empty;
            word2 = si2.MoveNext() ? si2.GetTextElement() : string.Empty;

            return string.Format("{0}{1}", word1, word2).Trim().ToUpperInvariant();
        }
    }
}
