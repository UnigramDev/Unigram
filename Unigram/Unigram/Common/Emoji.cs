using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;

namespace Unigram.Common
{
    public class EmojiSet
    {
        public string Id { get; set; }
        public string Title { get; set; }

        public File Thumbnail { get; set; }
        public File Document { get; set; }

        public bool IsDefault { get; set; }

        public bool UpdateFile(File file)
        {
            if (Thumbnail.Id == file.Id)
            {
                Thumbnail = file;
                return true;
            }
            else if (Document.Id == file.Id)
            {
                Document = file;
                return true;
            }

            return false;
        }
    }

    public enum EmojiSkinTone
    {
        Default,
        Fitz12,
        Fitz3,
        Fitz4,
        Fitz5,
        Fitz6
    }

    public class EmojiData
    {
        protected EmojiData()
        {

        }

        public EmojiData(string value)
        {
            Value = value;
        }

        public string Value { get; protected set; }
    }

    public class EmojiSkinData : EmojiData, INotifyPropertyChanged
    {
        private string _value;

        public EmojiSkinData(string value, EmojiSkinTone tone)
        {
            _value = value;
            SetValue(tone);
        }

        public void SetValue(EmojiSkinTone tone)
        {
            if (tone == EmojiSkinTone.Default)
            {
                Value = _value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Value"));

                return;
            }

            var emoji = _value;
            var result = string.Empty;

            if (char.IsSurrogatePair(emoji, 0))
            {
                result = emoji.Substring(0, 2);
                emoji = emoji.Substring(2);
            }
            else if (emoji.Length <= 2)
            {
                result = emoji;
                emoji = string.Empty;
            }
            else if (emoji.Length == 5 && (emoji[3] == '\u2642' || emoji[3] == '\u2640'))
            {
                result = emoji.Substring(0, 1);
                emoji = emoji.Substring(1);
            }

            switch (tone)
            {
                case EmojiSkinTone.Fitz12:
                    result += "\uD83C\uDFFB";
                    break;
                case EmojiSkinTone.Fitz3:
                    result += "\uD83C\uDFFC";
                    break;
                case EmojiSkinTone.Fitz4:
                    result += "\uD83C\uDFFD";
                    break;
                case EmojiSkinTone.Fitz5:
                    result += "\uD83C\uDFFE";
                    break;
                case EmojiSkinTone.Fitz6:
                    result += "\uD83C\uDFFF";
                    break;
            }

            Value = result + emoji;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Value"));
        }



        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }

    public partial class EmojiGroupInternal
    {
        public string Title { get; set; }
        public string Glyph { get; set; }

        public string[] Items { get; set; }

        public EmojiGroup ToGroup(EmojiSkinTone tone)
        {
            return new EmojiGroup
            {
                Title = Title,
                Glyph = Glyph,
                Items = Items.Select(x =>
                {
                    if (_skinEmojis.Contains(x))
                    {
                        return new EmojiSkinData(x, tone);
                    }

                    return new EmojiData(x);

                }).ToArray()
            };
        }
    }

    public class EmojiGroup
    {
        public string Title { get; set; }
        public string Glyph { get; set; }

        public EmojiData[] Items { get; set; }
    }

    public static partial class Emoji
    {
        public static List<EmojiGroup> Get(EmojiSkinTone skin, bool flags)
        {
            var results = new List<EmojiGroup>();
            if (flags)
            {
                results.AddRange(Items.Select(x => x.ToGroup(skin)));
            }
            else
            {
                results.AddRange(Items.Take(Items.Count - 1).Select(x => x.ToGroup(skin)));
            }

            return results;
        }

        public static bool ContainsSingleEmoji(string text)
        {
            text = text.Trim();

            if (text.Length < 1 || text.Contains(" "))
            {
                return false;
            }

            return _rawEmojis.Contains(text);



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

        public static bool TryCountEmojis(string text, out int count, int max = int.MaxValue)
        {
            count = 0;
            text = text.Trim();

            if (text.Contains(" "))
            {
                return false;
            }

            var result = false;

            foreach (var last in EnumerateByComposedCharacterSequence(text))
            {
                if (_rawEmojis.Contains(last))
                {
                    count++;
                    result = count <= max;

                    if (count > max)
                    {
                        break;
                    }
                }
                else
                {
                    count = 0;
                    result = false;
                    break;
                }
            }

            return result;
        }

        public static void Assert()
        {
            var stringify = String.Join(string.Empty, _rawEmojis);
            var success = TryCountEmojis(stringify, out int count);

            Debug.Assert(success);
            Debug.Assert(count == _rawEmojis.Length);
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
                    var skin = IsSkinModifierCharacter(text, i);
                    if (!joiner && !skin)
                    {
                        yield return last;
                        last = string.Empty;
                        joiner = true;
                    }

                    if (!skin)
                    {
                        last += text[i + 0];
                        last += text[i + 1];
                    }

                    joiner = IsRegionalIndicator(text, i);
                    i++;
                }
                else if (text[i] == 0x200D) // zero width joiner
                {
                    last += text[i];
                    joiner = true;
                }
                else if (text[i] == 0xFE0F) // variation selector
                {
                    last += text[i];

                    if (i + 1 < text.Length && text[i + 1] == 0x200D)
                    {
                        joiner = true;
                    }
                }
                else if (text[i] == 0x20E3)
                {
                    last += text[i];
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
