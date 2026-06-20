//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Telegram.Td.Api
{
    public partial class FormattedText : Object, IEquatable<FormattedText>
    {
        public bool Equals(FormattedText other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Text == other.Text && Entities.SequenceEqual(other.Entities);
        }

        public override bool Equals(object? obj) => Equals(obj as FormattedText);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Text);
            foreach (var entity in Entities)
                hash.Add(entity);
            return hash.ToHashCode();
        }

        #region Concat

        /// <summary>
        /// Concatenates multiple FormattedText and string values.
        /// </summary>
        public static FormattedText Concat(params object[] values)
        {
            if (values == null || values.Length == 0)
            {
                return new FormattedText();
            }

            var resultText = new StringBuilder();
            var resultEntities = new List<TextEntity>();
            int currentOffset = 0;

            foreach (var value in values)
            {
                if (value == null)
                    continue;

                switch (value)
                {
                    case FormattedText formatted:
                        AppendFormattedText(formatted, resultText, resultEntities, ref currentOffset);
                        break;
                    case string str:
                        AppendString(str, resultText, ref currentOffset);
                        break;
                    default:
                        AppendString(value.ToString() ?? string.Empty, resultText, ref currentOffset);
                        break;
                }
            }

            return new FormattedText(resultText.ToString(), resultEntities);
        }

        /// <summary>
        /// Concatenates a collection of FormattedText and string values.
        /// </summary>
        public static FormattedText Concat(IEnumerable<object> values)
        {
            if (values == null)
            {
                return new FormattedText();
            }

            return Concat(values.ToArray());
        }

        #endregion

        #region Format

        /// <summary>
        /// Formats a string with mixed FormattedText and string arguments.
        /// </summary>
        public static FormattedText Format(string format, params object[] args)
        {
            if (string.IsNullOrEmpty(format))
            {
                return new FormattedText();
            }

            if (args == null || args.Length == 0)
            {
                return new FormattedText(format, Array.Empty<TextEntity>());
            }

            // Parse the format string to find placeholders
            var segments = ParseFormatString(format, args.Length);
            var resultParts = new List<object>(segments.Count);

            foreach (var segment in segments)
            {
                if (segment.IsPlaceholder)
                {
                    if (segment.ArgumentIndex >= 0 && segment.ArgumentIndex < args.Length)
                    {
                        resultParts.Add(args[segment.ArgumentIndex]);
                    }
                    else
                    {
                        // Preserve invalid placeholder in output
                        resultParts.Add(segment.Text);
                    }
                }
                else
                {
                    resultParts.Add(segment.Text);
                }
            }

            return Concat(resultParts.ToArray());
        }

        /// <summary>
        /// Formats a FormattedText with mixed FormattedText and string arguments.
        /// </summary>
        public static FormattedText Format(FormattedText format, params object[] args)
        {
            if (format == null || string.IsNullOrEmpty(format.Text))
            {
                return new FormattedText();
            }

            if (args == null || args.Length == 0)
            {
                return new FormattedText(format.Text, new List<TextEntity>(format.Entities));
            }

            // Parse the format text to find placeholders
            var segments = ParseFormatString(format.Text, args.Length);
            var resultParts = new List<object>(segments.Count);

            foreach (var segment in segments)
            {
                if (segment.IsPlaceholder)
                {
                    if (segment.ArgumentIndex >= 0 && segment.ArgumentIndex < args.Length)
                    {
                        resultParts.Add(args[segment.ArgumentIndex]);
                    }
                    else
                    {
                        // Preserve invalid placeholder with its original entities
                        resultParts.Add(CreateSubstring(format, segment.OriginalOffset, segment.OriginalLength));
                    }
                }
                else
                {
                    // Extract the text segment with its entities
                    resultParts.Add(CreateSubstring(format, segment.OriginalOffset, segment.OriginalLength));
                }
            }

            return Concat(resultParts.ToArray());
        }

        #endregion

        #region Join

        /// <summary>
        /// Joins multiple FormattedText and string values with a separator.
        /// </summary>
        public static FormattedText Join(string separator, params object[] values)
        {
            if (values == null || values.Length == 0)
            {
                return new FormattedText();
            }

            var resultParts = new List<object>(values.Length * 2 - 1);
            bool first = true;

            foreach (var value in values)
            {
                if (value == null)
                    continue;

                if (!first && separator != null)
                {
                    resultParts.Add(separator);
                }

                resultParts.Add(value);
                first = false;
            }

            return Concat(resultParts.ToArray());
        }

        /// <summary>
        /// Joins multiple FormattedText and string values with a separator.
        /// </summary>
        public static FormattedText Join(FormattedText separator, params object[] values)
        {
            if (values == null || values.Length == 0)
            {
                return new FormattedText();
            }

            var resultParts = new List<object>(values.Length * 2 - 1);
            bool first = true;

            foreach (var value in values)
            {
                if (value == null)
                    continue;

                if (!first && separator != null)
                {
                    resultParts.Add(separator);
                }

                resultParts.Add(value);
                first = false;
            }

            return Concat(resultParts.ToArray());
        }

        /// <summary>
        /// Joins a collection of FormattedText and string values with a separator.
        /// </summary>
        public static FormattedText Join(string separator, IEnumerable<object> values)
        {
            if (values == null)
            {
                return new FormattedText();
            }

            return Join(separator, values.ToArray());
        }

        /// <summary>
        /// Joins a collection of FormattedText and string values with a separator.
        /// </summary>
        public static FormattedText Join(FormattedText separator, IEnumerable<object> values)
        {
            if (values == null)
            {
                return new FormattedText();
            }

            return Join(separator, values.ToArray());
        }

        #endregion

        #region Replace

        /// <summary>
        /// Replaces all occurrences of a string with another string.
        /// </summary>
        public FormattedText Replace(string oldValue, string newValue)
        {
            if (string.IsNullOrEmpty(oldValue))
            {
                throw new ArgumentException("Old value cannot be null or empty.", nameof(oldValue));
            }

            if (string.IsNullOrEmpty(Text))
            {
                return new FormattedText(string.Empty, new List<TextEntity>(0));
            }

            newValue ??= string.Empty;

            // Find all occurrences
            var occurrences = FindAllOccurrences(Text, oldValue);
            if (occurrences.Count == 0)
            {
                return new FormattedText(Text, new List<TextEntity>(Entities));
            }

            return ReplaceInternal(occurrences, oldValue.Length, newValue);
        }

        /// <summary>
        /// Replaces all occurrences of a string with FormattedText.
        /// </summary>
        public FormattedText Replace(string oldValue, FormattedText newValue)
        {
            if (string.IsNullOrEmpty(oldValue))
            {
                throw new ArgumentException("Old value cannot be null or empty.", nameof(oldValue));
            }

            if (string.IsNullOrEmpty(Text))
            {
                return new FormattedText();
            }

            newValue ??= new FormattedText();

            // Find all occurrences
            var occurrences = FindAllOccurrences(Text, oldValue);
            if (occurrences.Count == 0)
            {
                return new FormattedText(Text, new List<TextEntity>(Entities));
            }

            return ReplaceInternal(occurrences, oldValue.Length, newValue);
        }

        /// <summary>
        /// Replaces all occurrences of FormattedText with a string.
        /// </summary>
        public FormattedText Replace(FormattedText oldValue, string newValue)
        {
            if (oldValue == null || string.IsNullOrEmpty(oldValue.Text))
            {
                throw new ArgumentException("Old value cannot be null or empty.", nameof(oldValue));
            }

            return Replace(oldValue.Text, newValue);
        }

        /// <summary>
        /// Replaces all occurrences of FormattedText with another FormattedText.
        /// </summary>
        public FormattedText Replace(FormattedText oldValue, FormattedText newValue)
        {
            if (oldValue == null || string.IsNullOrEmpty(oldValue.Text))
            {
                throw new ArgumentException("Old value cannot be null or empty.", nameof(oldValue));
            }

            return Replace(oldValue.Text, newValue);
        }

        #endregion

        #region Helper Methods

        private static void AppendFormattedText(
            FormattedText formatted,
            StringBuilder resultText,
            List<TextEntity> resultEntities,
            ref int currentOffset)
        {
            if (string.IsNullOrEmpty(formatted.Text))
                return;

            int startOffset = currentOffset;

            resultText.Append(formatted.Text);

            // Clone and offset entities
            if (formatted.Entities != null)
            {
                foreach (var entity in formatted.Entities)
                {
                    resultEntities.Add(new TextEntity(
                        entity.Offset + startOffset,
                        entity.Length,
                        entity.Type
                    ));
                }
            }

            currentOffset += formatted.Text.Length;
        }

        private static void AppendString(string str, StringBuilder resultText, ref int currentOffset)
        {
            if (string.IsNullOrEmpty(str))
                return;

            resultText.Append(str);
            currentOffset += str.Length;
        }

        private static List<FormatSegment> ParseFormatString(string format, int argCount)
        {
            var segments = new List<FormatSegment>();
            int pos = 0;
            int lastPos = 0;

            while (pos < format.Length)
            {
                int openBrace = format.IndexOf('{', pos);
                if (openBrace == -1)
                {
                    // No more placeholders
                    if (lastPos < format.Length)
                    {
                        segments.Add(new FormatSegment
                        {
                            Text = format.Substring(lastPos),
                            IsPlaceholder = false,
                            OriginalOffset = lastPos,
                            OriginalLength = format.Length - lastPos
                        });
                    }
                    break;
                }

                // Check for escaped brace
                if (openBrace + 1 < format.Length && format[openBrace + 1] == '{')
                {
                    // Add text before escaped brace
                    if (lastPos < openBrace + 1)
                    {
                        segments.Add(new FormatSegment
                        {
                            Text = format.Substring(lastPos, openBrace - lastPos + 1),
                            IsPlaceholder = false,
                            OriginalOffset = lastPos,
                            OriginalLength = openBrace - lastPos + 1
                        });
                    }
                    pos = openBrace + 2;
                    lastPos = openBrace + 1;
                    continue;
                }

                // Add text before placeholder
                if (lastPos < openBrace)
                {
                    segments.Add(new FormatSegment
                    {
                        Text = format.Substring(lastPos, openBrace - lastPos),
                        IsPlaceholder = false,
                        OriginalOffset = lastPos,
                        OriginalLength = openBrace - lastPos
                    });
                }

                // Find closing brace
                int closeBrace = format.IndexOf('}', openBrace + 1);
                if (closeBrace == -1)
                {
                    // Malformed placeholder - treat rest as literal text
                    segments.Add(new FormatSegment
                    {
                        Text = format.Substring(openBrace),
                        IsPlaceholder = false,
                        OriginalOffset = openBrace,
                        OriginalLength = format.Length - openBrace
                    });
                    break;
                }

                // Check for escaped closing brace
                if (closeBrace + 1 < format.Length && format[closeBrace + 1] == '}')
                {
                    // This is not a placeholder, continue searching
                    pos = closeBrace + 2;
                    continue;
                }

                // Parse placeholder
                string placeholderContent = format.Substring(openBrace + 1, closeBrace - openBrace - 1);
                if (int.TryParse(placeholderContent, out int argIndex))
                {
                    segments.Add(new FormatSegment
                    {
                        Text = format.Substring(openBrace, closeBrace - openBrace + 1),
                        IsPlaceholder = true,
                        ArgumentIndex = argIndex,
                        OriginalOffset = openBrace,
                        OriginalLength = closeBrace - openBrace + 1
                    });
                }
                else
                {
                    // Invalid placeholder - treat as literal text
                    segments.Add(new FormatSegment
                    {
                        Text = format.Substring(openBrace, closeBrace - openBrace + 1),
                        IsPlaceholder = false,
                        OriginalOffset = openBrace,
                        OriginalLength = closeBrace - openBrace + 1
                    });
                }

                pos = closeBrace + 1;
                lastPos = closeBrace + 1;
            }

            return segments;
        }

        private static FormattedText CreateSubstring(FormattedText source, int offset, int length)
        {
            if (string.IsNullOrEmpty(source.Text) || length == 0)
            {
                return new FormattedText();
            }

            string substringText = source.Text.Substring(offset, length);
            var substringEntities = new List<TextEntity>();

            int endOffset = offset + length;

            foreach (var entity in source.Entities)
            {
                int entityEnd = entity.Offset + entity.Length;

                // Check if entity intersects with the substring range
                if (entity.Offset < endOffset && entityEnd > offset)
                {
                    int newOffset = Math.Max(0, entity.Offset - offset);
                    int newEnd = Math.Min(length, entityEnd - offset);
                    int newLength = newEnd - newOffset;

                    if (newLength > 0)
                    {
                        substringEntities.Add(new TextEntity(
                            newOffset,
                            newLength,
                            entity.Type
                        ));
                    }
                }
            }

            return new FormattedText(substringText, substringEntities);
        }

        private static List<int> FindAllOccurrences(string text, string value)
        {
            var occurrences = new List<int>();
            int index = 0;

            while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) != -1)
            {
                occurrences.Add(index);
                index += value.Length;
            }

            return occurrences;
        }

        private FormattedText ReplaceInternal(List<int> occurrences, int oldLength, object newValue)
        {
            // Build result by processing text between occurrences
            var parts = new List<object>();
            int lastIndex = 0;

            foreach (int occurrence in occurrences)
            {
                // Add text before occurrence
                if (occurrence > lastIndex)
                {
                    parts.Add(CreateSubstring(this, lastIndex, occurrence - lastIndex));
                }

                // Add replacement
                parts.Add(newValue);

                lastIndex = occurrence + oldLength;
            }

            // Add remaining text
            if (lastIndex < Text.Length)
            {
                parts.Add(CreateSubstring(this, lastIndex, Text.Length - lastIndex));
            }

            return Concat(parts.ToArray());
        }

        private class FormatSegment
        {
            public string Text { get; set; }
            public bool IsPlaceholder { get; set; }
            public int ArgumentIndex { get; set; }
            public int OriginalOffset { get; set; }
            public int OriginalLength { get; set; }
        }

        #endregion
    }

    public abstract partial class TextEntityType : Object, IEquatable<TextEntityType>
    {
        public virtual bool Equals(TextEntityType other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return GetType() == other.GetType();
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as TextEntityType);
        }

        public override int GetHashCode()
        {
            return GetType().GetHashCode();
        }

        public static bool operator ==(TextEntityType left, TextEntityType right)
        {
            if (left is null) return right is null;
            return left.Equals(right);
        }

        public static bool operator !=(TextEntityType left, TextEntityType right)
        {
            return !(left == right);
        }
    }

    public partial class TextEntityTypeCustomEmoji : TextEntityType
    {
        public override bool Equals(TextEntityType? other)
        {
            if (!base.Equals(other)) return false;
            return other is TextEntityTypeCustomEmoji emoji &&
                   CustomEmojiId == emoji.CustomEmojiId;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(base.GetHashCode(), CustomEmojiId);
        }
    }

    public partial class TextEntityTypeMediaTimestamp : TextEntityType
    {
        public override bool Equals(TextEntityType other)
        {
            if (!base.Equals(other)) return false;
            return other is TextEntityTypeMediaTimestamp timestamp &&
                   MediaTimestamp == timestamp.MediaTimestamp;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(base.GetHashCode(), MediaTimestamp);
        }
    }

    public partial class TextEntityTypeMentionName : TextEntityType
    {
        public override bool Equals(TextEntityType other)
        {
            if (!base.Equals(other)) return false;
            return other is TextEntityTypeMentionName mention &&
                   UserId == mention.UserId;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(base.GetHashCode(), UserId);
        }
    }

    public partial class TextEntityTypePreCode : TextEntityType
    {
        public override bool Equals(TextEntityType other)
        {
            if (!base.Equals(other)) return false;
            return other is TextEntityTypePreCode preCode &&
                   Language == preCode.Language;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(base.GetHashCode(), Language);
        }
    }

    public partial class TextEntityTypeTextUrl : TextEntityType
    {
        public override bool Equals(TextEntityType other)
        {
            if (!base.Equals(other)) return false;
            return other is TextEntityTypeTextUrl textUrl &&
                   Url == textUrl.Url;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(base.GetHashCode(), Url);
        }
    }

    public partial class TextEntity : Object, IEquatable<TextEntity>
    {
        public bool Equals(TextEntity other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Offset == other.Offset &&
                   Length == other.Length &&
                   Equals(Type, other.Type);
        }

        public override bool Equals(object obj) => Equals(obj as TextEntity);

        public override int GetHashCode() => HashCode.Combine(Offset, Length, Type);
    }
}
