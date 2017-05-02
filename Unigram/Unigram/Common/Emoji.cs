using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unigram.Common
{
    public static class Emoji
    {
        public static bool ContainsSingleEmoji(string text)
        {
            var result = false;
            var processed = false;

            foreach (var last in EnumerateByComposedCharacterSequence(text))
            {
                if (processed)
                {
                    result = false;
                    break;
                }
                else if (IsEmoji(last))
                {
                    result = true;
                    processed = true;
                }
                else
                {
                    result = false;
                    break;
                }
            }

            return result;
        }

        public static bool IsEmoji(string text)
        {
            var high = text[0];

            // Surrogate pair (U+1D000-1F77F)
            if (0xd800 <= high && high <= 0xdbff && text.Length >= 2)
            {
                var low = text[1];
                var codepoint = ((high - 0xd800) * 0x400) + (low - 0xdc00) + 0x10000;

                return (0x1d000 <= codepoint && codepoint <= 0x1f77f);

            }
            else
            {
                // Not surrogate pair (U+2100-27BF)
                return (0x2100 <= high && high <= 0x27bf);
            }
        }

        public static IEnumerable<string> EnumerateByComposedCharacterSequence(string text)
        {
            var last = string.Empty;
            var joiner = true;

            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsSurrogatePair(text, i) || IsKeyCapCharacter(text, i) || IsModifierCharacter(text, i))
                {
                    // skin modifier for emoji diversity acts as a joiner
                    if (!joiner && !IsSkinModifierCharacter(text, i))
                    {
                        yield return last;
                        last = string.Empty;
                        joiner = true;
                    }

                    last += text[i + 0];
                    last += text[i + 1];
                    joiner = IsRegionalIndicator(text, i);
                    i++;
                }
                else if (text[i] == 0x200D) // zero width joiner
                //else if (char.IsControl(text, i))
                {
                    last += text[i];
                    joiner = true;
                }
                else
                {
                    if (last.Length > 0)
                    {
                        yield return last;
                    }

                    if (i + 2 < text.Length && IsSkinModifierCharacter(text, i + 1))
                    {
                        last += text[i];
                    }
                    else
                    {
                        yield return text[i].ToString();
                        last = string.Empty;
                    }

                    joiner = true;
                }
            }

            if (last.Length > 0)
            {
                yield return last;
            }
        }

        public static bool IsSkinModifierCharacter(string s, int index)
        {
            if (index + 2 <= s.Length)
            {
                char c1 = s[index + 0];
                char c2 = s[index + 1];
                return c1 == '\uD83C' && c2 >= '\uDFFB' && c2 <= '\uDFFF';
            }

            return false;
        }

        public static bool IsKeyCapCharacter(string s, int index)
        {
            return index + 1 < s.Length && s[index + 1] == '\u20E3';
        }

        public static bool IsModifierCharacter(string s, int index)
        {
            return index + 1 < s.Length && s[index + 1] == '\uFE0F';
        }

        public static bool IsRegionalIndicator(string s, int index)
        {
            if (index + 4 > s.Length)
            {
                return false;
            }

            if (IsRegionalIndicator(s[index], s[index + 1]) && IsRegionalIndicator(s[index + 2], s[index + 3]))
            {
                return true;
            }

            return false;
        }

        public static bool IsRegionalIndicator(char highSurrogate, char lowSurrogate)
        {
            if (char.IsHighSurrogate(highSurrogate) && char.IsLowSurrogate(lowSurrogate))
            {
                var utf32 = char.ConvertToUtf32(highSurrogate, lowSurrogate);
                return utf32 >= 127462u && utf32 <= 127487u;
            }

            return false;
        }

        public static string BuildUri(string string2)
        {
            var result = string.Empty;
            var i = 0;

            do
            {
                if (char.IsSurrogatePair(string2, i))
                {
                    result += char.ConvertToUtf32(string2, i).ToString("x2");
                    i += 2;
                }
                else
                {
                    result += ((short)string2[i]).ToString("x4");
                    i++;
                }

                if (i < string2.Length)
                    result += "-";

            } while (i < string2.Length);

            return $"ms-appx:///Assets/Emojis/{result}.png";
        }
    }
}
