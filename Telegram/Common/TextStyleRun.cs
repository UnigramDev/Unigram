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

        // Not Array.Empty: TextStylePart is a WinRT struct and the native side takes an
        // IVector<TextStylePart>, so an array has to be boxed as IReferenceArray, which NativeAOT
        // cannot synthesise - it throws NotSupportedException the moment the CCW is built. A List
        // marshals through the generated path instead. Shared, so the empty case stays allocation
        // free; nothing on either side of the ABI writes to it.
        public static readonly IList<TextStylePart> NoParts = new List<TextStylePart>();

        public static IList<TextStylePart> GetParts(Vector<TextEntity> entities)
        {
            if (entities == null || entities.Count == 0)
            {
                return NoParts;
            }

            // Built on the first style, not up front: most messages carry no styling at all,
            // and the ones that do are usually a link or a custom emoji, which are not styles
            // either. This runs once per StyledText and once per paragraph in it.
            List<TextStylePart> items = null;

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

                items ??= new List<TextStylePart>(entities.Count);
                items.Add(new TextStylePart
                {
                    Offset = entity.Offset,
                    Length = entity.Length,
                    Type = type
                });
            }

            return items ?? NoParts;
        }

        public static IList<TextStyleRun> GetRuns(FormattedText formatted)
        {
            return GetRuns(formatted.Text, formatted.Entities);
        }

        public static IList<TextStyleRun> GetRuns(string text, Vector<TextEntity> entities)
        {
            if (entities == null || entities.Count == 0)
            {
                return Array.Empty<TextStyleRun>();
            }

            List<TextStyleRun> runs = null;
            var sorted = true;

            for (int i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (entity.Length <= 0 || entity.Offset < 0 || entity.Offset >= text.Length)
                {
                    continue;
                }

                var run = new TextStyleRun
                {
                    Start = entity.Offset,
                    // Clamped here rather than on the entity: entities belong to the message and
                    // are rendered again against other text - a paragraph of it, a search preview.
                    End = Math.Min(entity.Offset + entity.Length, text.Length)
                };

                (run.Flags, run.Type) = entity.Type switch
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
                    TextEntityTypeButton => (TextStyle.Button, entity.Type),
                    // A marker laid over a link, never standing alone, so it leaves Type null
                    // and the link it covers keeps its own.
                    TextEntityTypeCached => (TextStyle.Cached, null),
                    _ => (TextStyle.Url, entity.Type)
                };

                runs ??= new List<TextStyleRun>(entities.Count);
                sorted = sorted && (runs.Count == 0 || runs[runs.Count - 1].Start <= run.Start);
                runs.Add(run);
            }

            if (runs == null)
            {
                return Array.Empty<TextStyleRun>();
            }

            if (!sorted)
            {
                // Shorter first where two start together, so Split sees a wrapper's content
                // before the wrapper. List.Sort is unstable, and a RichText flattens to entities
                // in post-order - inner before outer, frequently at the same offset - so without
                // the second key which of the two owns the run would come down to the sort.
                runs.Sort((x, y) => x.Start != y.Start ? x.Start.CompareTo(y.Start) : x.End.CompareTo(y.End));
            }

            // Nothing overlaps in the overwhelming majority of messages, and then the entities
            // already are the runs - no splitting, and nothing allocated beyond this list.
            for (int i = 1; i < runs.Count; i++)
            {
                if (runs[i].Start < runs[i - 1].End)
                {
                    return Split(runs);
                }
            }

            return runs;
        }

        /// <summary>
        /// Cuts <paramref name="sources"/> at every entity edge and gives each resulting segment
        /// the union of the entities covering it, so the result is an ordered partition: no gaps
        /// invented, no segment empty or inverted, and no two overlapping.
        /// </summary>
        private static List<TextStyleRun> Split(List<TextStyleRun> sources)
        {
            var bounds = new int[sources.Count * 2];

            for (int i = 0; i < sources.Count; i++)
            {
                bounds[i * 2] = sources[i].Start;
                bounds[i * 2 + 1] = sources[i].End;
            }

            Array.Sort(bounds);

            var runs = new List<TextStyleRun>(bounds.Length);

            for (int i = 0; i < bounds.Length - 1; i++)
            {
                var start = bounds[i];
                var end = bounds[i + 1];

                if (start == end)
                {
                    continue;
                }

                TextStyleRun merged = null;
                TextStyleRun owner = null;

                for (int j = 0; j < sources.Count; j++)
                {
                    var source = sources[j];

                    // The bounds are consecutive edges, so a source spans the whole segment or
                    // none of it - there is no partial case to split further.
                    if (source.Start > start || source.End < end)
                    {
                        continue;
                    }

                    merged ??= new TextStyleRun
                    {
                        Start = start,
                        End = end
                    };

                    merged.Flags |= source.Flags;

                    if (source.Type == null)
                    {
                        continue;
                    }

                    // A wrapper is longer than what it wraps, so the shortest entity over the
                    // segment is the content - and the renderer branches on the flags and then
                    // reads Type expecting the content's. A custom emoji wins outright: it is
                    // drawn inside its spoiler, not instead of it. Two entities over exactly the
                    // same range cannot be told apart this way and the first wins; the flags of
                    // both survive either way, and they are what the renderer dispatches on.
                    if (owner == null
                        || source.Type is TextEntityTypeCustomEmoji
                        || (owner.Type is not TextEntityTypeCustomEmoji && source.Length < owner.Length))
                    {
                        owner = source;
                    }
                }

                if (merged != null)
                {
                    merged.Type = owner?.Type;
                    runs.Add(merged);
                }
            }

            return runs;
        }

        private static readonly char[] _lineBreakChars = new[] { '\n', '\r', '\v' };

        private static bool ContainsLineBreaks(string text, int offset, int length)
        {
            var starts = offset == 0 || _lineBreakChars.Contains(text[offset - 1]);
            var ends = offset + length == text.Length || _lineBreakChars.Contains(text[offset + length]);

            return (starts && ends) || text.IndexOfAny(_lineBreakChars, offset, length) >= 0;
        }

        public static Vector<TextEntity> GetEntities(string text, IList<TextStyleRun> runs)
        {
            if (runs == null)
            {
                return Array.Empty<TextEntity>();
            }

            var results = new MutableVector<TextEntity>();

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

        public static StyledText GetText(string text, Vector<TextEntity> entities)
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
            var entities = new MutableVector<TextEntity>();
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

        private static IList<StyledParagraph> GetParagraphs(string text, Vector<TextEntity> entities)
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

        private static StyledParagraph Split(string text, Vector<TextEntity> entities, int startIndex, int length, TextDirectionality? direction, int padding)
        {
            if (length <= 0)
            {
                return new StyledParagraph(string.Empty, startIndex, length, Array.Empty<TextEntity>());
            }

            var message = text.Substring(startIndex, Math.Min(text.Length - startIndex, length));
            MutableVector<TextEntity> sub = null;

            foreach (var entity in entities)
            {
                if (GetRelativeRange(entity.Offset, entity.Length, startIndex, length, out int newOffset, out int newLength))
                {
                    sub ??= new MutableVector<TextEntity>();
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
        public StyledText(string text, Vector<TextEntity> entities, IList<StyledParagraph> paragraphs)
        {
            Text = text;
            Parts = TextStyleRun.GetParts(entities);
            Paragraphs = paragraphs;
            IsComplex = GetIsComplex(paragraphs);
        }

        private static bool GetIsComplex(IList<StyledParagraph> paragraphs)
        {
            for (int i = 0; i < paragraphs.Count; i++)
            {
                if (paragraphs[i].Type != null)
                {
                    return true;
                }
            }

            return false;
        }

        public string Text { get; }

        public IList<TextStylePart> Parts { get; }

        public IList<StyledParagraph> Paragraphs { get; }

        public bool IsPlain { get; }

        public bool IsComplex { get; }

        /// <summary>
        /// Extracts the [start, start+length) character range of this styled text as a
        /// standalone <see cref="FormattedText"/> — the substring plus the entities that
        /// intersect the range, clipped and re-based to the slice. Operates purely on the
        /// styled text (no original FormattedText needed), so it behaves the same whether
        /// this came from a FormattedText or a RichText. Used to copy a selection.
        /// </summary>
        public FormattedText Substring(int start, int length)
        {
            if (start < 0)
            {
                length += start;
                start = 0;
            }

            var end = Math.Min(Text.Length, start + Math.Max(0, length));
            if (end <= start)
            {
                return new FormattedText(string.Empty, new MutableVector<TextEntity>());
            }

            // Paragraph entities are paragraph-relative; lift each back to an absolute
            // offset before clipping to the requested range. (StyledText no longer keeps a
            // flattened entity list — the paragraphs are the source of truth.)
            var entities = new MutableVector<TextEntity>();
            foreach (var paragraph in Paragraphs)
            {
                if (paragraph.Entities == null)
                {
                    continue;
                }

                foreach (var entity in paragraph.Entities)
                {
                    var absolute = paragraph.Offset + entity.Offset;
                    var from = Math.Max(absolute, start);
                    var to = Math.Min(absolute + entity.Length, end);
                    if (to > from)
                    {
                        entities.Add(new TextEntity(from - start, to - from, entity.Type));
                    }
                }
            }

            return new FormattedText(Text.Substring(start, end - start), entities);
        }

        public static StyledText Empty = new(string.Empty, Array.Empty<TextEntity>(), Array.Empty<StyledParagraph>());
    }

    public partial class StyledParagraph
    {
        private readonly bool _hasDates;
        private readonly bool _hasRelativeDates;

        public StyledParagraph(string text, Vector<TextEntity> entities)
            : this(text, 0, text.Length, entities)
        {

        }

        public StyledParagraph(string text, int offset, int length, Vector<TextEntity> entities, TextDirectionality? direction = null, int padding = 0)
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
            else
            {
                IsPlain = true;
            }
        }

        public string Text { get; }

        public int Offset { get; }

        public int Length { get; }

        public Vector<TextEntity> Entities { get; }

        public IList<TextStylePart> Parts { get; }

        public IList<TextStyleRun> Runs { get; }

        public TextDirectionality Direction { get; }

        public int Padding { get; }

        public TextParagraphType Type { get; }

        public bool IsPlain { get; }

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

    public partial class TextEntityTypeButton : TextEntityType
    {
        public InlineButton Button { get; set; }

        public TextEntityTypeButton() { }

        public TextEntityTypeButton(InlineButton button)
        {
            Button = button;
        }
    }

    /// <summary>
    /// Marks a link whose target already has an instant view (richTextUrl.is_cached,
    /// and the in-page reference/anchor links). Following one keeps the reader in the
    /// app, so it's highlighted rather than drawn as an ordinary link.
    ///
    /// Client-only, and deliberately a SEPARATE entity laid over the link rather than a
    /// flavour of textEntityTypeTextUrl: everything that dispatches on the link type
    /// keeps working untouched, and GetRuns merges the two into one run.
    /// </summary>
    public partial class TextEntityTypeCached : TextEntityType
    {
    }
}
