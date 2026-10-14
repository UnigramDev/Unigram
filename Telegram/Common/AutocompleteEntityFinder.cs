//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI.Text;

namespace Telegram.Common
{
    public enum AutocompleteEntity
    {
        None,
        Emoji,
        Hashtag,
        Username,
        Command,
        Sticker
    }

    public partial class AutocompleteEntityFinder
    {
        private static readonly HashSet<char> _symbols = new() { ':', '#', '@', '/' };

        // Longest sequence the emoji list can produce is a two-tone kiss, at 15 chars.
        // Cutting the lookbehind shorter breaks the trailing emoji apart and no sticker
        // is ever suggested for it.
        private const int LongestEmoji = 16;

        public static AutocompleteEntity Search(ITextRange text, out string result, out int index)
        {
            TrySearch(text, out AutocompleteEntity entity, out result, out index);
            return entity;
        }

        public static bool TrySearch(ITextRange text, out AutocompleteEntity entity, out string result, out int index)
        {
            entity = AutocompleteEntity.None;
            result = string.Empty;
            index = -1;

            var found = true;
            var end = text.EndPosition;

            var hidden = 0;

            void Move()
            {
                text.SetRange(text.StartPosition - 1, text.EndPosition - 1);
            }

            text.SetRange(text.EndPosition - 1, text.EndPosition);

            while (text.StartPosition >= 0 && text.EndPosition > text.StartPosition)
            {
                if (text.CharacterFormat.Hidden == FormatEffect.On)
                {
                    hidden++;

                    Move();
                    continue;
                }
                else if (_symbols.Contains(text.Character))
                {
                    var i = text.StartPosition;
                    var character = text.Character;
                    Move();

                    if (text.StartPosition == 0 || text.Character == ' ' || text.Character == '\n' || text.Character == '\r' || text.Character == '\v')
                    {
                        index = i;
                        break;
                    }
                    // If preceding character is a surrogate pair we assume it's an emoji
                    else if (character == ':' && (text.Character == '\uEA4F' || (text.Text.Length >= 2 && char.IsSurrogatePair(text.Text, text.Text.Length - 2))))
                    {
                        index = i;
                        break;
                    }

                    found = false;
                    break;
                }
                else if (text.StartPosition > 0 && IsValidSymbol(text.Character))
                {
                    Move();
                }
                else
                {
                    found = false;
                    break;
                }
            }

            if (found && index >= 0)
            {
                text.SetRange(index, end);
                text.GetText(TextGetOptions.NoHidden, out result);

                entity = text.Character switch
                {
                    ':' => AutocompleteEntity.Emoji,
                    '#' => AutocompleteEntity.Hashtag,
                    '@' => AutocompleteEntity.Username,
                    '/' => AutocompleteEntity.Command,
                    _ => AutocompleteEntity.None
                };

                if (entity != AutocompleteEntity.None && result.Length > 0)
                {
                    result = result.Substring(1);
                }

                // Special case for emoji
                if (entity == AutocompleteEntity.Emoji && result.Length == 1 && result[0] == char.ToUpper(result[0]))
                {
                    entity = AutocompleteEntity.None;
                    result = string.Empty;
                }
                else if (entity == AutocompleteEntity.Emoji && result.Length == 0)
                {
                    entity = AutocompleteEntity.None;
                    result = string.Empty;
                }
            }

            if (entity == AutocompleteEntity.None)
            {
                text.SetRange(end - hidden - LongestEmoji, end - hidden);
                text.GetText(TextGetOptions.NoHidden, out string shorter);

                //var shorter = text;
                if (shorter.Length > LongestEmoji)
                {
                    shorter = shorter.Substring(shorter.Length - LongestEmoji);
                }

                var emoji = Emoji.EnumerateByComposedCharacterSequenceReverse(shorter);
                var last = emoji.FirstOrDefault();

                if (last != null && Emoji.ContainsSingleEmoji(last))
                {
                    result = last;
                    index = end - hidden - (shorter.Length - StartOf(shorter, shorter.Length, last));
                    entity = AutocompleteEntity.Sticker;
                }
            }

            return entity != AutocompleteEntity.None;
        }

        /// <summary>
        /// The same search over plain text, for an editor that is not RichEdit: what is typed in
        /// <paramref name="text"/> before <paramref name="caret"/>. <paramref name="index"/> is
        /// where the entity starts, its symbol included. <paramref name="objectChar"/> is what
        /// stands for a custom emoji, after which a ':' still starts an emoji query.
        /// </summary>
        public static bool TrySearch(string text, int caret, char objectChar, out AutocompleteEntity entity, out string result, out int index)
        {
            entity = AutocompleteEntity.None;
            result = string.Empty;
            index = -1;

            for (int i = caret - 1; i >= 0; i--)
            {
                var character = text[i];

                if (_symbols.Contains(character))
                {
                    if (i == 0 || text[i - 1] is ' ' or '\n' or '\r' or '\v')
                    {
                        index = i;
                    }
                    // If preceding character is a surrogate pair we assume it's an emoji
                    else if (character == ':' && (text[i - 1] == objectChar || char.IsLowSurrogate(text[i - 1])))
                    {
                        index = i;
                    }

                    break;
                }
                else if (i == 0 || !IsValidSymbol(character))
                {
                    break;
                }
            }

            if (index >= 0)
            {
                entity = text[index] switch
                {
                    ':' => AutocompleteEntity.Emoji,
                    '#' => AutocompleteEntity.Hashtag,
                    '@' => AutocompleteEntity.Username,
                    '/' => AutocompleteEntity.Command,
                    _ => AutocompleteEntity.None
                };

                result = text.Substring(index + 1, caret - index - 1);

                // Special case for emoji
                if (entity == AutocompleteEntity.Emoji && (result.Length == 0 || (result.Length == 1 && result[0] == char.ToUpper(result[0]))))
                {
                    entity = AutocompleteEntity.None;
                    result = string.Empty;
                }
            }

            if (entity == AutocompleteEntity.None && caret > 0 && MayEndEmoji(text[caret - 1]))
            {
                var start = Math.Max(0, caret - LongestEmoji);
                var shorter = text.Substring(start, caret - start);

                var emoji = Emoji.EnumerateByComposedCharacterSequenceReverse(shorter);
                var last = emoji.FirstOrDefault();

                if (last != null && Emoji.ContainsSingleEmoji(last))
                {
                    result = last;
                    index = StartOf(text, caret, last);
                    entity = AutocompleteEntity.Sticker;
                }
            }

            if (entity == AutocompleteEntity.None)
            {
                index = -1;
            }

            return entity != AutocompleteEntity.None;
        }

        // How an emoji can end: a surrogate pair, a variation selector, or a symbol from the
        // BMP's emoji blocks (U+20E3, the keycap, among them). Letters in any script do not, so
        // typing them costs nothing more than this.
        private static bool MayEndEmoji(char c)
        {
            return char.IsLowSurrogate(c) || c == '️' || (c >= ' ' && c <= '㋿') || c == '©' || c == '®';
        }

        // The emoji comes back without its skin tones, which is what stickers are found by, so
        // its length is not how far back it starts: walk back over it, stepping over the tones.
        private static int StartOf(string text, int caret, string emoji)
        {
            var index = caret;
            var remaining = emoji.Length;

            while (remaining > 0 && index > 0)
            {
                if (index >= 2 && Emoji.IsSkinModifierCharacter(text, index - 2))
                {
                    index -= 2;
                }
                else if (text[index - 1] == emoji[remaining - 1])
                {
                    index--;
                    remaining--;
                }
                else
                {
                    break;
                }
            }

            return index;
        }

        public static bool IsValidSymbol(char symbol)
        {
            return char.IsLetter(symbol) || char.IsDigit(symbol) || symbol == '_';
        }
    }
}
