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
using Telegram.Converters;
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

        #region DateTime

        public string FormattedText { get; set; } = string.Empty;

        public string Update(StyledParagraph paragraph)
        {
            if (string.IsNullOrEmpty(FormattedText) || Type is TextEntityTypeDateTime { FormattingType: DateTimeFormattingTypeRelative })
            {
                FormattedText = Formatter.Relative(Type as TextEntityTypeDateTime);
                paragraph.IsDirty = true;
            }

            return FormattedText;
        }

        #endregion

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

        public static IList<TextStylePart> GetParts(IList<TextEntity> entities)
        {
            if (entities == null)
            {
                return Array.Empty<TextStylePart>();
            }

            var items = new List<TextStylePart>(entities.Count);

            foreach (var entity in entities)
            {
                var type = entity.Type switch
                {
                    TextEntityTypeBold => TextStyle.Bold,
                    TextEntityTypeItalic => TextStyle.Italic,
                    TextEntityTypeUnderline => TextStyle.Underline,
                    TextEntityTypeStrikethrough => TextStyle.Strikethrough,
                    TextEntityTypeCode or TextEntityTypePre or TextEntityTypePreCode => TextStyle.Monospace,
                    TextEntityTypeSubscript => TextStyle.Subscript,
                    TextEntityTypeSuperscript => TextStyle.Superscript,
                    TextEntityTypeMarked => TextStyle.Marked,
                    _ => TextStyle.None
                };

                if (type == TextStyle.None)
                {
                    continue;
                }

                items.Add(new TextStylePart
                {
                    Offset = entity.Offset,
                    Length = entity.Length,
                    Type = type
                });
            }

            return items;
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

            entitiesCopy.Sort((x, y) => x.Offset.CompareTo(y.Offset));

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

                var newRun = new TextStyleRun
                {
                    Start = entity.Offset,
                    End = entity.Offset + entity.Length
                };

                (newRun.Flags, newRun.Type) = entity.Type switch
                {
                    TextEntityTypeStrikethrough => (TextStyle.Strikethrough, null),
                    TextEntityTypeUnderline => (TextStyle.Underline, null),
                    TextEntityTypeSpoiler => (TextStyle.Spoiler, entity.Type),
                    TextEntityTypeBold => (TextStyle.Bold, null),
                    TextEntityTypeItalic => (TextStyle.Italic, null),
                    TextEntityTypeBlockQuote or TextEntityTypeExpandableBlockQuote => (TextStyle.Quote, null),
                    TextEntityTypeCode or TextEntityTypePre or TextEntityTypePreCode => (TextStyle.Monospace, entity.Type),
                    TextEntityTypeMentionName => (TextStyle.Mention, entity.Type),
                    TextEntityTypeCustomEmoji => (TextStyle.Emoji, entity.Type),
                    // RichText-only additions:
                    TextEntityTypeSubscript => (TextStyle.Subscript, null),
                    TextEntityTypeSuperscript => (TextStyle.Superscript, null),
                    TextEntityTypeMarked => (TextStyle.Marked, null),
                    TextEntityTypeIcon => (TextStyle.Icon, entity.Type),
                    TextEntityTypeMathematicalExpression => (TextStyle.Math, entity.Type),
                    _ => (TextStyle.Url, entity.Type)
                };

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
                            TextStyleRun r = new(newRun);
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
                            TextStyleRun r = new(newRun);
                            r.Merge(run);
                            r.End = run.End;
                            b++;
                            N2++;
                            runs.Insert(b, r);
                        }

                        (newRun.Start, run.End) = (run.End, newRun.Start);
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
                            TextStyleRun r = new(run);
                            r.Merge(newRun);
                            r.End = newRun.End;
                            b++;
                            N2++;
                            runs.Insert(b, r);

                            run.Start = newRun.End;
                        }
                        else
                        {
                            TextStyleRun r = new(newRun);
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

            runs.Sort((x, y) => x.Offset.CompareTo(y.Offset));
            return runs;
        }

        private static readonly char[] _lineBreakChars = new[] { '\n', '\r', '\v' };

        private static bool ContainsLineBreaks(string text, int offset, int length)
        {
            var starts = offset == 0 || _lineBreakChars.Contains(text[offset - 1]);
            var ends = offset + length == text.Length || _lineBreakChars.Contains(text[offset + length]);

            return (starts && ends) || text.IndexOfAny(_lineBreakChars, offset, length) >= 0;
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
                if (run.End > text.Length)
                {
                    if (run.Start < text.Length)
                    {
                        run.End = text.Length;
                    }
                    else
                    {
                        continue;
                    }
                }

                if (run.HasFlag(TextStyle.Monospace))
                {
                    CreateOrMerge(text, run.Offset, run.Length, results, new TextEntityTypeCode());

                    if (run.HasFlag(TextStyle.Quote))
                    {
                        CreateOrMerge(text, run.Offset, run.Length, results, new TextEntityTypeBlockQuote());
                    }
                }
                else
                {
                    if (run.HasFlag(TextStyle.Bold))
                    {
                        CreateOrMerge(text, run.Offset, run.Length, results, new TextEntityTypeBold());
                    }
                    if (run.HasFlag(TextStyle.Italic))
                    {
                        CreateOrMerge(text, run.Offset, run.Length, results, new TextEntityTypeItalic());
                    }
                    if (run.HasFlag(TextStyle.Strikethrough))
                    {
                        CreateOrMerge(text, run.Offset, run.Length, results, new TextEntityTypeStrikethrough());
                    }
                    if (run.HasFlag(TextStyle.Underline))
                    {
                        CreateOrMerge(text, run.Offset, run.Length, results, new TextEntityTypeUnderline());
                    }
                    if (run.HasFlag(TextStyle.Spoiler))
                    {
                        CreateOrMerge(text, run.Offset, run.Length, results, new TextEntityTypeSpoiler());
                    }
                    if (run.HasFlag(TextStyle.Quote))
                    {
                        CreateOrMerge(text, run.Offset, run.Length, results, new TextEntityTypeBlockQuote());
                    }

                    if (run.Type != null)
                    {
                        CreateOrMerge(text, run.Offset, run.Length, results, run.Type);
                    }
                }
            }

            return results;
        }

        private static void Create(int offset, int length, IList<TextEntity> entities, TextEntityType type)
        {
            entities.Add(new TextEntity(offset, length, type));
        }

        private static void CreateOrMerge(string text, int offset, int length, IList<TextEntity> entities, TextEntityType type)
        {
            var last = entities.LastOrDefault(x => x.Length + x.Offset == offset && AreTheSame(x.Type, type));
            if (last != null)
            {
                if (type is TextEntityTypeCode && ContainsLineBreaks(text, last.Offset, last.Length + length))
                {
                    last.Type = new TextEntityTypePre();
                }

                last.Length += length;
            }
            else
            {
                if (type is TextEntityTypeCode && ContainsLineBreaks(text, offset, length))
                {
                    type = new TextEntityTypePre();
                }

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
            else if (x is TextEntityTypePre or TextEntityTypeCode && y is TextEntityTypeCode or TextEntityTypePre)
            {
                return true;
            }
            else if (x is TextEntityTypeCustomEmoji && y is TextEntityTypeCustomEmoji)
            {
                return false;
            }

            return x.GetType() == y.GetType();
        }

        #region Paragraphs

        public static StyledText GetText(FormattedText text)
        {
            if (string.IsNullOrEmpty(text?.Text))
            {
                return StyledText.Empty;
            }

            return new StyledText(text.Text, text.Entities, GetParagraphs(text.Text, text.Entities));
        }

        public static StyledText GetText(string text, IList<TextEntity> entities)
        {
            if (string.IsNullOrEmpty(text))
            {
                return StyledText.Empty;
            }

            return new StyledText(text, entities, GetParagraphs(text, entities ?? Array.Empty<TextEntity>()));
        }

        #region RichText

        public static StyledText GetText(RichMessage message)
        {
            return GetText(PageBlockHelper.GetRichText(message.Blocks));
        }

        /// <summary>
        /// Flattens a <see cref="RichText"/> tree into a string + entities pair and
        /// wraps the result in a <see cref="StyledText"/>, reusing the same paragraph
        /// splitting and run-merging machinery used for <see cref="FormattedText"/>.
        /// </summary>
        public static StyledText GetText(RichText richText)
        {
            if (richText == null)
            {
                return StyledText.Empty;
            }

            var builder = new StringBuilder();
            var entities = new List<TextEntity>();
            PageBlockHelper.Flatten(richText, builder, entities);

            if (builder.Length == 0)
            {
                return StyledText.Empty;
            }

            var text = builder.ToString();
            return new StyledText(text, entities, GetParagraphs(text, entities));
        }

        #endregion

        private readonly struct Break
        {
            public readonly int Offset;

            public readonly int Length;

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
                return new StyledParagraph(string.Empty, startIndex, length, Array.Empty<TextEntity>());
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

    public partial class StyledText
    {
        public StyledText(string text, IList<TextEntity> entities, IList<StyledParagraph> paragraphs)
        {
            Text = text;
            Entities = entities;
            Parts = TextStyleRun.GetParts(entities);
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

        public IList<TextEntity> Entities { get; }

        public IList<TextStylePart> Parts { get; }

        public IList<StyledParagraph> Paragraphs { get; }

        public bool IsPlain { get; }

        public static StyledText Empty = new(string.Empty, Array.Empty<TextEntity>(), Array.Empty<StyledParagraph>());
    }

    public partial class StyledParagraph
    {
        private readonly bool _hasDates;
        private readonly bool _hasRelativeDates;

        public StyledParagraph(string text, IList<TextEntity> entities)
            : this(text, 0, text.Length, entities)
        {

        }

        public StyledParagraph(string text, int offset, int length, IList<TextEntity> entities, TextDirectionality? direction = null, int padding = 0)
        {
            Text = text;
            Offset = offset;
            Length = length;
            Entities = entities ?? Array.Empty<TextEntity>();
            Parts = TextStyleRun.GetParts(entities);
            Runs = TextStyleRun.GetRuns(text, entities);
            Direction = direction ?? NativeUtils.GetDirectionality(text);
            Padding = length > 0 ? padding : 1;

            if (entities?.Count > 0)
            {
                _hasRelativeDates = entities.Any(x => x.Type is TextEntityTypeDateTime { FormattingType: DateTimeFormattingTypeRelative });
                _hasDates = _hasRelativeDates || entities.Any(x => x.Type is TextEntityTypeDateTime { FormattingType: DateTimeFormattingTypeAbsolute });

                Type = entities[0].Type switch
                {
                    TextEntityTypePreCode preCode => new TextParagraphTypeMonospace(preCode.Language),
                    TextEntityTypePre => new TextParagraphTypeMonospace(),
                    TextEntityTypeBlockQuote => new TextParagraphTypeQuote(false),
                    TextEntityTypeExpandableBlockQuote => new TextParagraphTypeQuote(true),
                    _ => null
                };
            }
        }

        public string Text { get; }

        public int Offset { get; }

        public int Length { get; }

        public IList<TextEntity> Entities { get; }

        public IList<TextStylePart> Parts { get; }

        public IList<TextStyleRun> Runs { get; }

        public TextDirectionality Direction { get; }

        public int Padding { get; }

        public TextParagraphType Type { get; }

        public bool IsDirty { get; set; } = true;

        private string _dynamicText;
        private IList<TextStylePart> _dynamicParts;

        public IList<TextStylePart> GetParts(out string text)
        {
            text = _dynamicText ?? Text;

            if (_hasDates && IsDirty)
            {
                text = Text;

                var parts = new List<TextStylePart>();
                var offset = 0;

                foreach (var entity in Runs)
                {
                    if (entity.Type is TextEntityTypeDateTime && entity.FormattedText != null)
                    {
                        text = text.Remove(entity.Offset + offset, entity.Length);
                        text = text.Insert(entity.Offset + offset, entity.FormattedText);

                        offset += entity.FormattedText.Length - entity.Length;
                    }
                    else if (entity.Flags != TextStyle.None)
                    {
                        parts.Add(new TextStylePart
                        {
                            Offset = entity.Offset + offset,
                            Length = entity.Length,
                            Type = entity.Flags
                        });
                    }
                }

                _dynamicText = text;
                _dynamicParts = parts;
                IsDirty = false;
            }

            return _dynamicParts ?? Parts;
        }
    }

    public interface TextParagraphType
    {

    }

    public partial class TextParagraphTypeQuote : TextParagraphType
    {
        public TextParagraphTypeQuote(bool isExpandable)
        {
            IsExpandable = isExpandable;
        }

        public bool IsExpandable { get; }
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

// =====================================================================
// Synthetic TextEntityType subclasses for RichText-only constructs.
// These don't exist in TDLib's schema; they only flow through the
// in-memory rendering pipeline (RichText -> StyledText -> FormattedTextBlock).
// Move to your TDLib API folder, or to a dedicated synthetic-entities
// file, if you'd prefer to keep this file focused on TextStyleRun.
// =====================================================================
namespace Telegram.Td.Api
{
    public partial class TextEntityTypeSubscript : TextEntityType { }
    public partial class TextEntityTypeSuperscript : TextEntityType { }
    public partial class TextEntityTypeMarked : TextEntityType { }

    public partial class TextEntityTypeIcon : TextEntityType
    {
        public Document Document { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public TextEntityTypeIcon() { }

        public TextEntityTypeIcon(Document document, int width, int height)
        {
            Document = document;
            Width = width;
            Height = height;
        }
    }

    public partial class TextEntityTypeMathematicalExpression : TextEntityType
    {
        public string Expression { get; set; }

        public TextEntityTypeMathematicalExpression() { }

        public TextEntityTypeMathematicalExpression(string expression)
        {
            Expression = expression;
        }
    }
}
