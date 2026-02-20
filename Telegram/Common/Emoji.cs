//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Controls.Media;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.Td.Api;
using Telegram.ViewModels.Drawers;
using WinRT;

namespace Telegram.Common
{
    public partial class EmojiSet
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public int Version { get; set; }

        public File Thumbnail { get; set; }
        public File Document { get; set; }

        public bool IsDefault { get; set; }
        public bool IsOfficial { get; set; }

        public bool UpdateFile(File file)
        {
            if (Thumbnail?.Id == file.Id)
            {
                Thumbnail = file;
                return true;
            }
            else if (Document?.Id == file.Id)
            {
                Document = file;
                return true;
            }

            return false;
        }

        public InstalledEmojiSet ToInstalled()
        {
            return new InstalledEmojiSet
            {
                Id = Id,
                Title = Title,
                Version = Version
            };
        }
    }

    public enum EmojiSkinTone
    {
        Default = 0,
        Fitz12 = 1,
        Fitz3 = 2,
        Fitz4 = 3,
        Fitz5 = 4,
        Fitz6 = 5
    }

    public partial class EmojiData
    {
        public EmojiData(string value)
        {
            Value = value;
            Emoji = value;
        }

        public string Value { get; protected set; }

        public string Emoji { get; protected set; }
    }

    public partial class EmojiSkinData : EmojiData, INotifyPropertyChanged
    {
        public EmojiSkinData(string value, EmojiSkinTone tone)
            : base(value)
        {
            SetValue(tone);
        }

        public EmojiSkinData(string value, EmojiSkinTone tone1, EmojiSkinTone tone2)
            : base(value)
        {
            SetValue(tone1, tone2);
        }

        public EmojiSkinTone Tone1 { get; private set; }
        public EmojiSkinTone Tone2 { get; private set; }

        public void SetValue(EmojiSkinTone tone)
        {
            if (tone == Tone1)
            {
                return;
            }
            else if (tone == EmojiSkinTone.Default)
            {
                Tone1 = EmojiSkinTone.Default;
                Tone2 = EmojiSkinTone.Default;

                Value = Emoji;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Value"));

                return;
            }

            var emoji = Emoji;
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

            Tone1 = tone;
            Tone2 = EmojiSkinTone.Default;

            Value = result + GetTone(tone);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Value"));
        }

        public void SetValue(EmojiSkinTone tone1, EmojiSkinTone tone2)
        {
            if (tone1 == Tone1 && tone2 == Tone2 && Value != null)
            {
                return;
            }
            else if (tone1 == EmojiSkinTone.Default || tone2 == EmojiSkinTone.Default)
            {
                Tone1 = EmojiSkinTone.Default;
                Tone2 = EmojiSkinTone.Default;

                Value = Emoji;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Value"));

                return;
            }

            var result = Emoji switch
            {
                "\U0001F91D" => tone1 != tone2 ? "\U0001FAF1{0}\u200D\U0001FAF2{1}" : Emoji + "{0}",
                "\U0001F46B" => tone1 != tone2 ? "\U0001F469{0}\u200D\U0001F91D\u200D\U0001F468{1}" : Emoji + "{0}",
                "\U0001F46D" => tone1 != tone2 ? "\U0001F469{0}\u200D\U0001F91D\u200D\U0001F469{1}" : Emoji + "{0}",
                "\U0001F46C" => tone1 != tone2 ? "\U0001F468{0}\u200D\U0001F91D\u200D\U0001F468{1}" : Emoji + "{0}",
                //"\U0001F469\u200D\u2764\uFE0F\u200D\U0001F468" => "\U0001F469{0}\u200D\u2764\uFE0F\u200D\U0001F468{1}",
                //"\U0001F469\u200D\u2764\uFE0F\u200D\U0001F469" => "\U0001F469{0}\u200D\u2764\uFE0F\u200D\U0001F469{1}",
                "\U0001F491" => tone1 != tone2 ? "\U0001F9D1{0}\u200D\u2764\uFE0F\u200D\U0001F9D1{1}" : Emoji + "{0}",
                //"\U0001F468\u200D\u2764\uFE0F\u200D\U0001F468" => "\U0001F468{0}\u200D\u2764\uFE0F\u200D\U0001F468{1}",
                //"\U0001F469\u200D\u2764\uFE0F\u200D\U0001F48B\u200D\U0001F468" => "\U0001F469{0}\u200D\u2764\uFE0F\u200D\U0001F48B\u200D\U0001F468{1}",
                //"\U0001F469\u200D\u2764\uFE0F\u200D\U0001F48B\u200D\U0001F469" => "\U0001F469{0}\u200D\u2764\uFE0F\u200D\U0001F48B\u200D\U0001F469{1}",
                "\U0001F48F" => tone1 != tone2 ? "\U0001F9D1{0}\u200D\u2764\uFE0F\u200D\U0001F48B\u200D\U0001F9D1{1}" : Emoji + "{0}",
                //"\U0001F468\u200D\u2764\uFE0F\u200D\U0001F48B\u200D\U0001F468" => "\U0001F468{0}\u200D\u2764\uFE0F\u200D\U0001F48B\u200D\U0001F468{1}",
                _ => Emoji.Insert(2, "{0}") + "{1}"
            };

            Tone1 = tone1;
            Tone2 = tone2;

            Value = string.Format(result, GetTone(tone1), GetTone(tone2));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Value"));
        }

        private string GetTone(EmojiSkinTone tone)
        {
            switch (tone)
            {
                case EmojiSkinTone.Fitz12:
                    return "\uD83C\uDFFB";
                case EmojiSkinTone.Fitz3:
                    return "\uD83C\uDFFC";
                case EmojiSkinTone.Fitz4:
                    return "\uD83C\uDFFD";
                case EmojiSkinTone.Fitz5:
                    return "\uD83C\uDFFE";
                case EmojiSkinTone.Fitz6:
                    return "\uD83C\uDFFF";
                default:
                    return string.Empty;
            }
        }


        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }

    [GeneratedBindableCustomProperty]
    public partial class EmojiGroup
    {
        public string Title { get; set; }
        public string Glyph { get; set; }

        public EmojiData[] Stickers { get; set; }

        public bool IsInstalled { get; } = true;

        public override string ToString()
        {
            return Title;
        }
    }

    public static partial class Emoji
    {
        public partial class EmojiGroupInternal
        {
            public string Title { get; set; }
            public string Glyph { get; set; }

            public string[] Items { get; set; }

            public EmojiGroup ToGroup()
            {
                return new EmojiGroup
                {
                    Title = Title,
                    Glyph = Glyph,
                    Stickers = Items.Select(x =>
                    {
                        if (_skinEmojis.Contains(x))
                        {
                            return SettingsService.Current.Emoji.GetEmojiSkinTone(x);
                        }

                        return new EmojiData(x);

                    }).ToArray()
                };
            }
        }

        public static IList<EmojiData> GetRecents()
        {
            return SettingsService.Current.Emoji.RecentEmoji.Select(x =>
            {
                if (EmojiGroupInternal._skinEmojis.Contains(x))
                {
                    return SettingsService.Current.Emoji.GetEmojiSkinTone(x);
                }

                return new EmojiData(x);

            }).ToArray();
        }

        public static List<EmojiGroup> Get()
        {
            var results = new List<EmojiGroup>();
            var recent = new EmojiGroup
            {
                Title = Strings.RecentStickers,
                Glyph = Icons.EmojiRecents,
                Stickers = SettingsService.Current.Emoji.RecentEmoji.Select(x =>
                {
                    if (EmojiGroupInternal._skinEmojis.Contains(x))
                    {
                        return SettingsService.Current.Emoji.GetEmojiSkinTone(x);
                    }

                    return new EmojiData(x);

                }).ToArray()
            };

            results.AddRange(Items.Select(x => x.ToGroup()));
            return results;
        }

        public static async Task<IList<StickerViewModel>> SearchAsync(IClientService clientService, IEnumerable<string> emojis)
        {
            var result = new List<StickerViewModel>();
            var query = string.Join(" ", emojis);

            var resp = await clientService.SendAsync(new SearchStickers(new StickerTypeCustomEmoji(), query, string.Empty, Array.Empty<string>(), 0, 100));
            if (resp is Stickers stickers)
            {
                foreach (var item in stickers.StickersValue)
                {
                    result.Add(new StickerViewModel(clientService, item));
                }
            }

            return result;
        }

        public static bool ContainsSingleEmoji(string text)
        {
            text = text.Trim();

            if (text.Length < 1 || text.Contains(" "))
            {
                return false;
            }

            var last = RemoveModifiers(text);
            return _rawEmojis.Contains(last);
        }

        public static bool TryCountCustomEmojis(FormattedText text, out int count)
        {
            var previous = 0;
            var lineCount = 0;
            var lines = 0;
            count = 0;

            for (int i = 0; i < text.Entities.Count; i++)
            {
                var entity = text.Entities[i];
                if (entity.Offset - previous > 1)
                {
                    return false;
                }
                else if (entity.Offset - previous == 1)
                {
                    if (text.Text[entity.Offset - 1] is not '\n' and not ' ')
                    {
                        return false;
                    }

                    count = Math.Max(count, lineCount);
                    lineCount = 0;
                    lines++;
                }

                if (entity.Type is not TextEntityTypeCustomEmoji)
                {
                    return false;
                }

                lineCount++;
                previous = entity.Offset + entity.Length;
            }

            if (previous < text.Text.Length)
            {
                return false;
            }

            count = Math.Max(lines, lineCount);
            return previous > 0;
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
                var clean = RemoveModifiers(last);

                if (_rawEmojis.Contains(clean))
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
            var stringify = string.Join(string.Empty, _rawEmojis);
            var success = TryCountEmojis(stringify, out int count);

            Debug.Assert(success);
            Debug.Assert(count == _rawEmojis.Count);
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
                    }

                    if (!skin)
                    {
                        last += text[i + 0];
                        last += text[i + 1];
                    }

                    joiner = last.Length == 2 && IsRegionalIndicator(text, i);
                    joiner = joiner || IsTagIndicator(text, i + 2) || IsTagIndicator(text, i);
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
                else if (i > 0 && text[i - 1] == 0x200D)
                {
                    last += text[i];
                    joiner = false;
                }
                else
                {
                    if (last.Length > 0)
                    {
                        yield return last;
                        last = string.Empty;
                    }

                    if (i + 2 < text.Length && IsSkinModifierCharacter(text, i + 1))
                    {
                        last += text[i];
                    }
                    else if (i + 1 < text.Length && text[i + 1] == 0x200D)
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

        public static IEnumerable<string> EnumerateByComposedCharacterSequenceReverse(string text)
        {
            var last = string.Empty;
            var joiner = true;

            for (int i = text.Length - 1; i >= 0; i--)
            {
                if (i > 0 && (char.IsSurrogatePair(text, i - 1) || IsKeyCapCharacter(text, i - 1) || IsModifierCharacter(text, i - 1)))
                {
                    // skin modifier for emoji diversity acts as a joiner
                    var skin = IsSkinModifierCharacter(text, i - i);
                    if (!joiner && !skin)
                    {
                        yield return last;
                        last = string.Empty;
                    }

                    if (!skin)
                    {
                        last = text[i] + last;
                        last = text[i - 1] + last;
                    }

                    joiner = last.Length == 2 && IsRegionalIndicator(text, i - 3);
                    joiner = joiner || IsTagIndicator(text, i - 3) || IsTagIndicator(text, i - 1);
                    i--;
                }
                else if (text[i] == 0x200D) // zero width joiner
                {
                    last = text[i] + last;
                    joiner = true;
                }
                else if (text[i] == 0xFE0F) // variation selector
                {
                    last = text[i] + last;

                    if (i + 1 < text.Length && text[i + 1] == 0x200D)
                    {
                        joiner = true;
                    }
                }
                else if (text[i] == 0x20E3)
                {
                    last = text[i] + last;
                }
                else if (i < text.Length - 1 && text[i + 1] == 0x200D)
                {
                    last = text[i] + last;
                    joiner = false;
                }
                else
                {
                    if (last.Length > 0)
                    {
                        yield return last;
                        last = string.Empty;
                    }

                    if (i - 3 > 0 && IsSkinModifierCharacter(text, i - 3))
                    {
                        last = text[i] + last;
                    }
                    else if (i > 0 && text[i - 1] == 0x200D)
                    {
                        last = text[i] + last;
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
            if (index < 0 || index + 1 >= s.Length)
            {
                return false;
            }

            char c1 = s[index + 0];
            char c2 = s[index + 1];
            return c1 == '\uD83C' && c2 >= '\uDFFB' && c2 <= '\uDFFF';
        }

        public static bool IsKeyCapCharacter(string s, int index)
        {
            return index + 1 < s.Length && s[index + 1] == '\u20E3';
        }

        public static bool IsModifierCharacter(string s, int index)
        {
            return index + 1 < s.Length && s[index + 1] == '\uFE0F';
        }

        public static bool IsTagIndicator(string s, int index)
        {
            if (index < 0 || index + 1 >= s.Length)
            {
                return false;
            }

            if (IsTagIndicator(s[index], s[index + 1]))
            {
                return true;
            }

            return false;
        }

        public static bool IsTagIndicator(char highSurrogate, char lowSurrogate)
        {
            if (char.IsHighSurrogate(highSurrogate) && char.IsLowSurrogate(lowSurrogate))
            {
                var utf32 = char.ConvertToUtf32(highSurrogate, lowSurrogate);
                return utf32 is >= 0xE0061 and <= 0xE007A;
            }

            return false;
        }

        public static bool IsRegionalIndicator(string s, int index)
        {
            if (index < 0 || index + 3 >= s.Length)
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
                return utf32 is >= 0x1F1E6 and <= 0x1F1FF;
            }

            return false;
        }
    }
}
