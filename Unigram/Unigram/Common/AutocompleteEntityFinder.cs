using System.Collections.Generic;
using System.Linq;
using Windows.UI.Text;

namespace Unigram.Common
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

    public class AutocompleteEntityFinder
    {
        private static readonly HashSet<char> _symbols = new HashSet<char> { ':', '#', '@', '/' };

        public static AutocompleteEntity Search(string text, out string result, out int index)
        {
            TrySearch(text, out AutocompleteEntity entity, out result, out index);
            return entity;
        }

        public static bool TrySearch(string text, out AutocompleteEntity entity, out string result, out int index)
        {
            entity = AutocompleteEntity.None;
            result = string.Empty;
            index = -1;

            var found = true;
            var i = text.Length - 1;

            while (i >= 0)
            {
                if (_symbols.Contains(text[i]))
                {
                    if (i == 0 || text[i - 1] == ' ' || text[i - 1] == '\n' || text[i - 1] == '\r' || text[i - 1] == '\v')
                    {
                        index = i;
                        break;
                    }

                    found = false;
                    break;
                }
                else if (IsValidSymbol(text[i]))
                {
                    i--;
                }
                else
                {
                    found = false;
                    break;
                }
            }

            if (found && index >= 0)
            {
                result = text.Substring(index + 1);
                entity = text[index] switch
                {
                    ':' => AutocompleteEntity.Emoji,
                    '#' => AutocompleteEntity.Hashtag,
                    '@' => AutocompleteEntity.Username,
                    '/' => AutocompleteEntity.Command,
                    _ => AutocompleteEntity.None
                };

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
                var shorter = text;
                if (shorter.Length > 11)
                {
                    shorter = shorter.Substring(shorter.Length - 11);
                }

                var emoji = Emoji.EnumerateByComposedCharacterSequence(shorter);
                var last = emoji.LastOrDefault();

                if (last != null && Emoji.ContainsSingleEmoji(last))
                {
                    result = last;
                    index = text.Length - last.Length;
                    entity = AutocompleteEntity.Sticker;
                }
            }

            return entity != AutocompleteEntity.None;
        }

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

            while (text.StartPosition >= 0)
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
                    Move();

                    if (text.StartPosition == 0 || text.Character == ' ' || text.Character == '\n' || text.Character == '\r' || text.Character == '\v')
                    {
                        index = i;
                        break;
                    }

                    found = false;
                    break;
                }
                else if (IsValidSymbol(text.Character))
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

                //result = text.Substring(index + 1);
                entity = text.Character switch
                {
                    ':' => AutocompleteEntity.Emoji,
                    '#' => AutocompleteEntity.Hashtag,
                    '@' => AutocompleteEntity.Username,
                    '/' => AutocompleteEntity.Command,
                    _ => AutocompleteEntity.None
                };

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

                var emoji = Emoji.EnumerateByComposedCharacterSequence(shorter);
                var last = emoji.LastOrDefault();

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
