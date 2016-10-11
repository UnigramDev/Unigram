using System.Linq;
using System.Text;

namespace Unigram.Core.Unidecode
{
    /// <summary>
    /// ASCII transliterations of Unicode text
    /// </summary>
    public static partial class Unidecoder
    {
        /// <summary>
        /// Transliterate Unicode string to ASCII string.
        /// </summary>
        /// <param name="input">String you want to transliterate into ASCII</param>
        /// <param name="tempStringBuilderCapacity">
        ///     If you know the length of the result,
        ///     pass the value for StringBuilder capacity.
        ///     InputString.Length*2 is used by default.
        /// </param>
        /// <returns>
        ///     ASCII string. There are [?] (3 characters) in places of some unknown(?) unicode characters.
        ///     It is this way in Python code as well.
        /// </returns>
        public static string Unidecode(this string input, int? tempStringBuilderCapacity = null)
        {
            if (string.IsNullOrEmpty(input))
            {
                return "";
            }

            if (input.All(x => x < 0x80))
            {
                return input;
            }
            

            // Unidecode result often can be at least two times longer than input string.
            var sb = new StringBuilder(tempStringBuilderCapacity ?? input.Length*2);
            foreach (char c in input)
            {
                // Copypaste is bad, but sb.Append(c.Unidecode()); would be a bit slower.
                if (c < 0x80)
                {
                    sb.Append(c);
                }
                else
                {
                    int high = c >> 8;
                    int low = c & 0xff;
                    string[] transliterations;
                    if (characters.TryGetValue(high, out transliterations))
                    {
                        sb.Append(transliterations[low]);
                    }
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Transliterate Unicode character to ASCII string.
        /// </summary>
        /// <param name="c">Character you want to transliterate into ASCII</param>
        /// <returns>
        ///     ASCII string. Unknown(?) unicode characters will return [?] (3 characters).
        ///     It is this way in Python code as well.
        /// </returns>
        public static string Unidecode(this char c)
        {
            string result;
            if (c < 0x80)
            {
                result = new string(c, 1);
            }
            else
            {
                int high = c >> 8;
                int low = c & 0xff;
                string[] transliterations;
                if (characters.TryGetValue(high, out transliterations))
                {
                    result = transliterations[low];
                }
                else
                {
                    result = "";
                }
            }

            return result;
        }
    }
}
