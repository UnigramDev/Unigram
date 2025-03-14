//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Native;
using Telegram.Td.Api;

namespace Telegram.Common
{
    public partial class TextStyleRun
    {
        public TextStyle Flags { get; set; }

        public int Start { get; set; }
        public int Offset => Start;

        public int End { get; set; }
        public int Length => End - Start;

        public TextEntityType Type { get; set; }

        public TextStyleRun()
        {

        }

        private TextStyleRun(TextStyleRun run)
        {
            Flags = run.Flags;
            Start = run.Start;
            End = run.End;
            Type = run.Type;
        }

        public bool HasFlag(TextStyle flag)
        {
            return (Flags & flag) != 0;
        }

        private void Merge(TextStyleRun run)
        {
            Flags |= run.Flags;
            Type ??= run.Type;

            // TODO: probably makes sense to add all entity types that provide some additional value.
            if (run.Type is TextEntityTypeCustomEmoji)
            {
                Type = run.Type;
            }
        }

        public static IList<TextStyleRun> GetRuns(FormattedText formatted)
        {
            return GetRuns(formatted.Text, formatted.Entities);
        }

        public static IList<TextStyleRun> GetRuns(string text, IList<TextEntity> entities)
        {
            if (entities == null || entities.Count == 0)
            {
                return Array.Empty<TextStyleRun>();
            }

            var runs = new List<TextStyleRun>();
            var entitiesCopy = new List<TextEntity>(entities);

            entitiesCopy.Sort((x, y) =>
            {
                if (x.Offset > y.Offset)
                {
                    return 1;
                }
                else if (y.Offset > x.Offset)
                {
                    return -1;
                }

                return 0;
            });

            for (int a = 0, N = entitiesCopy.Count; a < N; a++)
            {
                var entity = entitiesCopy[a];
                if (entity.Length <= 0 || entity.Offset < 0 || entity.Offset >= text.Length)
                {
                    continue;
                }
                else if (entity.Offset + entity.Length > text.Length)
                {
                    entity.Length = text.Length - entity.Offset;
                }

                var newRun = new TextStyleRun();
                newRun.Start = entity.Offset;
                newRun.End = newRun.Start + entity.Length;

                if (entity.Type is TextEntityTypeStrikethrough)
                {
                    newRun.Flags = TextStyle.Strikethrough;
                }
                else if (entity.Type is TextEntityTypeUnderline)
                {
                    newRun.Flags = TextStyle.Underline;
                }
                else if (entity.Type is TextEntityTypeSpoiler)
                {
                    newRun.Flags = TextStyle.Spoiler;
                    newRun.Type = entity.Type;
                }
                else if (entity.Type is TextEntityTypeBold)
                {
                    newRun.Flags = TextStyle.Bold;
                }
                else if (entity.Type is TextEntityTypeItalic)
                {
                    newRun.Flags = TextStyle.Italic;
                }
                else if (entity.Type is TextEntityTypeBlockQuote or TextEntityTypeExpandableBlockQuote)
                {
                    newRun.Flags = TextStyle.Quote;
                }
                else if (entity.Type is TextEntityTypeCode or TextEntityTypePre or TextEntityTypePreCode)
                {
                    newRun.Flags = TextStyle.Monospace;
                    newRun.Type = entity.Type;
                }
                else if (entity.Type is TextEntityTypeMentionName)
                {
                    newRun.Flags = TextStyle.Mention;
                    newRun.Type = entity.Type;
                }
                else if (entity.Type is TextEntityTypeCustomEmoji)
                {
                    newRun.Flags = TextStyle.Emoji;
                    newRun.Type = entity.Type;
                }
                else
                {
                    newRun.Flags = TextStyle.Url;
                    newRun.Type = entity.Type;
                }

                for (int b = 0, N2 = runs.Count; b < N2; b++)
                {
                    TextStyleRun run = runs[b];

                    if (newRun.Start > run.Start)
                    {
                        if (newRun.Start >= run.End)
                        {
                            continue;
                        }

                        if (newRun.End < run.End)
                        {
                            TextStyleRun r = new TextStyleRun(newRun);
                            r.Merge(run);
                            b++;
                            N2++;
                            runs.Insert(b, r);

                            r = new TextStyleRun(run);
                            r.Start = newRun.End;
                            b++;
                            N2++;
                            runs.Insert(b, r);
                        }
                        else if (newRun.End >= run.End)
                        {
                            TextStyleRun r = new TextStyleRun(newRun);
                            r.Merge(run);
                            r.End = run.End;
                            b++;
                            N2++;
                            runs.Insert(b, r);
                        }

                        int temp = newRun.Start;
                        newRun.Start = run.End;
                        run.End = temp;
                    }
                    else
                    {
                        if (run.Start >= newRun.End)
                        {
                            continue;
                        }
                        int temp = run.Start;
                        if (newRun.End == run.End)
                        {
                            run.Merge(newRun);
                        }
                        else if (newRun.End < run.End)
                        {
                            TextStyleRun r = new TextStyleRun(run);
                            r.Merge(newRun);
                            r.End = newRun.End;
                            b++;
                            N2++;
                            runs.Insert(b, r);

                            run.Start = newRun.End;
                        }
                        else
                        {
                            TextStyleRun r = new TextStyleRun(newRun);
                            r.Start = run.End;
                            b++;
                            N2++;
                            runs.Insert(b, r);

                            run.Merge(newRun);
                        }
                        newRun.End = temp;
                    }
                }
                if (newRun.Start < newRun.End)
                {
                    runs.Add(newRun);
                }
            }

            runs.Sort((x, y) =>
            {
                if (x.Offset > y.Offset)
                {
                    return 1;
                }
                else if (y.Offset > x.Offset)
                {
                    return -1;
                }

                return 0;
            });

            return runs;
        }

        public static IList<TextEntity> GetEntities(string text, IList<TextStyleRun> runs)
        {
            if (runs == null)
            {
                return Array.Empty<TextEntity>();
            }

            var results = new List<TextEntity>();

            foreach (var run in runs)
            {
                if (run.HasFlag(TextStyle.Emoji))
                {
                    Create(run.Offset, run.Length, results, run.Type);
                }
                else if (run.HasFlag(TextStyle.Monospace))
                {
                    var part = text.Substring(run.Offset, run.Length);
                    if (part.Contains('\v') || part.Contains('\r'))
                    {
                        CreateOrMerge(run.Offset, run.Length, results, new TextEntityTypePre());
                    }
                    else
                    {
                        CreateOrMerge(run.Offset, run.Length, results, new TextEntityTypeCode());
                    }
                }
                else
                {
                    if (run.HasFlag(TextStyle.Bold))
                    {
                        CreateOrMerge(run.Offset, run.Length, results, new TextEntityTypeBold());
                    }
                    if (run.HasFlag(TextStyle.Italic))
                    {
                        CreateOrMerge(run.Offset, run.Length, results, new TextEntityTypeItalic());
                    }
                    if (run.HasFlag(TextStyle.Strikethrough))
                    {
                        CreateOrMerge(run.Offset, run.Length, results, new TextEntityTypeStrikethrough());
                    }
                    if (run.HasFlag(TextStyle.Underline))
                    {
                        CreateOrMerge(run.Offset, run.Length, results, new TextEntityTypeUnderline());
                    }
                    if (run.HasFlag(TextStyle.Spoiler))
                    {
                        CreateOrMerge(run.Offset, run.Length, results, new TextEntityTypeSpoiler());
                    }
                    if (run.HasFlag(TextStyle.Quote))
                    {
                        CreateOrMerge(run.Offset, run.Length, results, new TextEntityTypeBlockQuote());
                    }

                    if (run.Type != null)
                    {
                        CreateOrMerge(run.Offset, run.Length, results, run.Type);
                    }
                }
            }

            return results;
        }

        private static void Create(int offset, int length, IList<TextEntity> entities, TextEntityType type)
        {
            entities.Add(new TextEntity(offset, length, type));
        }

        private static void CreateOrMerge(int offset, int length, IList<TextEntity> entities, TextEntityType type)
        {
            var last = entities.LastOrDefault(x => x.Length + x.Offset == offset && AreTheSame(x.Type, type));
            if (last != null)
            {
                last.Length += length;
            }
            else
            {
                entities.Add(new TextEntity(offset, length, type));
            }
        }

        private static bool AreTheSame(TextEntityType x, TextEntityType y)
        {
            if (x is TextEntityTypeTextUrl xTextUrl && y is TextEntityTypeTextUrl yTextUrl)
            {
                return string.Equals(xTextUrl.Url, yTextUrl.Url, StringComparison.OrdinalIgnoreCase);
            }
            else if (x is TextEntityTypeMentionName xMentionName && y is TextEntityTypeMentionName yMentionName)
            {
                return Equals(xMentionName.UserId, yMentionName.UserId);
            }

            return x.GetType() == y.GetType();
        }

        #region Paragraphs

        public static StyledText GetText(FormattedText text)
        {
            if (string.IsNullOrEmpty(text?.Text))
            {
                return null;
            }

            return new StyledText(text.Text, GetParagraphs(text.Text, text.Entities));
        }

        public static StyledText GetText(string text, IList<TextEntity> entities)
        {
            return new StyledText(text, GetParagraphs(text, entities ?? Array.Empty<TextEntity>()));
        }

        struct Break
        {
            public int Offset;

            public int Length;

            public Break(int offset, int length)
            {
                Offset = offset;
                Length = length;
            }

            public override string ToString()
            {
                return Offset.ToString();
            }
        }

        private static IList<StyledParagraph> GetParagraphs(string text, IList<TextEntity> entities)
        {
            List<Break> indexes = null;
            var previous = 0;

            int Break(int previous, int limit)
            {
                if (limit - previous < 0)
                {
                    return previous;
                }

                var index = text.IndexOf('\n', previous, limit - previous);

                while (index != -1)
                {
                    indexes ??= new();
                    indexes.Add(new Break(index, 1));

                    previous = index + 1;
                    index = text.IndexOf('\n', index + 1, limit - index);
                }

                return previous;
            }

            for (int i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (entity.Type is TextEntityTypePre or TextEntityTypePreCode or TextEntityTypeBlockQuote or TextEntityTypeExpandableBlockQuote)
                {
                    if (entity.Offset > 0 && text[entity.Offset - 1] != '\n')
                    {
                        indexes ??= new();
                        indexes.Add(new Break(entity.Offset, 0));
                    }

                    Break(previous, entity.Offset);

                    if (text.Length > entity.Offset + entity.Length && text[entity.Offset + entity.Length] != '\n' && text[entity.Offset + entity.Length - 1] != '\n')
                    {
                        indexes ??= new();
                        indexes.Add(new Break(entity.Offset + entity.Length, 0));
                    }
                    else if (text.Length > entity.Offset + entity.Length && text[entity.Offset + entity.Length - 1] == '\n')
                    {
                        indexes ??= new();
                        indexes.Add(new Break(entity.Offset + entity.Length - 1, 1));
                    }

                    previous = entity.Offset + entity.Length;
                }
            }

            if (text.Length > previous)
            {
                Break(previous, text.Length - 1);
            }

            if (indexes != null)
            {
                var prev = 0;
                var list = new List<StyledParagraph>();

                // The code may generate duplicate indexes (example: https://t.me/c/1896357006/2)
                // District is used to avoid that, but it would be better to fix the algorithm.
                foreach (var index in indexes.DistinctBy(x => x.Offset).OrderBy(x => x.Offset))
                {
                    list.Add(Split(text, entities, prev, index.Offset - prev, null, index.Length));
                    prev = index.Offset + index.Length;
                }

                if (text.Length > prev)
                {
                    list.Add(Split(text, entities, prev, text.Length - prev, null, 0));
                }

                return list;
            }

            return new[]
            {
                new StyledParagraph(text, 0, text.Length, entities)
            };
        }

        private static StyledParagraph Split(string text, IList<TextEntity> entities, int startIndex, int length, TextDirectionality? direction, int padding)
        {
            if (length <= 0)
            {
                return new StyledParagraph(string.Empty, startIndex, length, null);
            }

            var message = text.Substring(startIndex, Math.Min(text.Length - startIndex, length));
            IList<TextEntity> sub = null;

            foreach (var entity in entities)
            {
                if (GetRelativeRange(entity.Offset, entity.Length, startIndex, length, out int newOffset, out int newLength))
                {
                    sub ??= new List<TextEntity>();
                    sub.Add(new TextEntity
                    {
                        Offset = newOffset,
                        Length = newLength,
                        Type = entity.Type
                    });
                }
            }

            return new StyledParagraph(message, startIndex, message.Length, sub, direction, padding);
        }

        public static bool GetRelativeRange(int offset, int length, int relativeOffset, int relativeLength, out int newOffset, out int newLength)
        {
            // Included, Included
            if (offset > relativeOffset && offset + length <= relativeOffset + relativeLength)
            {
                newOffset = offset - relativeOffset;
                newLength = length;
            }
            // Before, Included
            else if (offset <= relativeOffset && offset + length > relativeOffset && offset + length < relativeOffset + relativeLength)
            {
                newOffset = 0;
                newLength = length - (relativeOffset - offset);
            }
            // Included, After
            else if (offset > relativeOffset && offset < relativeOffset + relativeLength && offset + length > relativeOffset + relativeLength)
            {
                newOffset = offset - relativeOffset;
                newLength = (relativeOffset + relativeLength) - offset;
            }
            // Before, After
            else if (offset <= relativeOffset && offset + length >= relativeOffset + relativeLength)
            {
                newOffset = 0;
                newLength = relativeLength;
            }
            else
            {
                newOffset = -1;
                newLength = length;
                return false;
            }

            return true;
        }

        #endregion
    }

    [Flags]
    public enum TextStyle
    {
        Bold = 1,
        Italic = 2,
        Monospace = 4,
        Strikethrough = 8,
        Underline = 16,
        Spoiler = 32,
        Mention = 64,
        Url = 128,
        Emoji = 256,
        Quote = 512,
    }

    public partial class StyledText
    {
        public StyledText(string text, IList<StyledParagraph> paragraphs)
        {
            Text = text;
            Paragraphs = paragraphs;

            if (paragraphs.Count == 1)
            {
                var paragraph = paragraphs[0];
                var plain = text.Length > 0
                    && paragraph.Entities.Count == 0;

                IsPlain = plain;
            }
        }

        public string Text { get; }

        public IList<StyledParagraph> Paragraphs { get; }

        public bool IsPlain { get; }
    }

    public partial class StyledParagraph
    {
        public StyledParagraph(string text, IList<TextEntity> entities)
            : this(text, 0, text.Length, entities)
        {

        }

        public StyledParagraph(string text, int offset, int length, IList<TextEntity> entities, TextDirectionality? direction = null, int padding = 0)
        {
            Offset = offset;
            Length = length;
            Entities = entities ?? Array.Empty<TextEntity>();
            Runs = TextStyleRun.GetRuns(text, entities);
            Direction = direction ?? NativeUtils.GetDirectionality(text);
            Padding = length > 0 ? padding : 1;

            if (entities?.Count > 0)
            {
                Type = entities[0].Type switch
                {
                    TextEntityTypePreCode preCode => new TextParagraphTypeMonospace(preCode.Language),
                    TextEntityTypePre => new TextParagraphTypeMonospace(),
                    TextEntityTypeBlockQuote => new TextParagraphTypeQuote(),
                    TextEntityTypeExpandableBlockQuote => new TextParagraphTypeQuote(),
                    _ => null
                };
            }
        }

        public int Offset { get; }

        public int Length { get; }

        public IList<TextEntity> Entities { get; }

        public IList<TextStyleRun> Runs { get; }

        public TextDirectionality Direction { get; }

        public int Padding { get; }

        public TextParagraphType Type { get; }
    }

    public interface TextParagraphType
    {

    }

    public partial class TextParagraphTypeQuote : TextParagraphType
    {

    }

    public partial class TextParagraphTypeMonospace : TextParagraphType
    {
        public TextParagraphTypeMonospace(string language)
        {
            Language = language;
        }

        public TextParagraphTypeMonospace()
        {
            Language = string.Empty;
        }

        public string Language { get; }
    }
}
