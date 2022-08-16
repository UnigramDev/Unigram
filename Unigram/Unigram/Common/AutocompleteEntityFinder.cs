using System.Collections.Generic;
using System.Linq;

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

        public static bool IsValidSymbol(char symbol)
        {
            return char.IsLetter(symbol) || char.IsDigit(symbol) || symbol == '_';
        }
    }
}
