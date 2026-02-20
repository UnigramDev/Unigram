//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

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
                text.SetRange(end - hidden - 11, end - hidden);
                text.GetText(TextGetOptions.NoHidden, out string shorter);

                //var shorter = text;
                if (shorter.Length > 11)
                {
                    shorter = shorter.Substring(shorter.Length - 11);
                }

                var emoji = Emoji.EnumerateByComposedCharacterSequenceReverse(shorter);
                var last = emoji.FirstOrDefault();

                if (last != null && Emoji.ContainsSingleEmoji(last))
                {
                    result = last;
                    index = end - hidden - last.Length;
                    entity = AutocompleteEntity.Sticker;
                }
            }

            return entity != AutocompleteEntity.None;
        }

        public static bool IsValidSymbol(char symbol)
        {
            return char.IsLetter(symbol) || char.IsDigit(symbol) || symbol == '_';
        }
    }
}
