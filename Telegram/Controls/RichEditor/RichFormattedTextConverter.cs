//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Text;
using Telegram.Td.Api;

namespace Telegram.Controls.RichEditor
{
    /// <summary>
    /// Between a message's <see cref="FormattedText"/> and a document in a
    /// <see cref="RichSchema"/>: a paragraph per line, block quotes and code blocks over the lines
    /// their entities cover, marks, links and custom emoji inside the lines.
    ///
    /// Unlike <see cref="RichDocumentConverter.FromFormattedText"/>, which reads a message for
    /// display, nothing is lost: empty lines stay, and so do the expandable flag and the code
    /// language. Text comes back as it went in, offsets in UTF-16 as TDLib counts them.
    /// </summary>
    public static class RichFormattedTextConverter
    {
        // =====================================================================================
        // In
        // =====================================================================================

        /// <summary>
        /// A quote or code entity that starts or ends inside a line covers the whole line here:
        /// a paragraph is quoted or not, and splitting the line at the edge would change the
        /// text. What the user sees is then what is sent.
        /// </summary>
        public static RichDocument FromFormattedText(FormattedText text, RichSchema schema)
        {
            schema ??= RichSchema.Message;

            var s = text?.Text ?? string.Empty;
            var entities = text?.Entities;

            var lineStarts = new List<int> { 0 };
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\n')
                {
                    lineStarts.Add(i + 1);
                }
            }

            var lines = lineStarts.Count;
            var quoteOf = new int[lines];
            var codeOf = new int[lines];

            for (int i = 0; i < lines; i++)
            {
                quoteOf[i] = codeOf[i] = -1;
            }

            if (entities != null)
            {
                for (int k = 0; k < entities.Count; k++)
                {
                    var entity = entities[k];
                    if (entity.Length <= 0 || entity.Offset < 0 || entity.Offset >= s.Length)
                    {
                        continue;
                    }

                    var target = entity.Type switch
                    {
                        TextEntityTypeBlockQuote or TextEntityTypeExpandableBlockQuote when schema.AllowsQuotes => quoteOf,
                        TextEntityTypePre or TextEntityTypePreCode when schema.AllowsCode => codeOf,
                        _ => null
                    };

                    if (target == null)
                    {
                        continue;
                    }

                    var first = LineOf(lineStarts, entity.Offset);
                    var last = LineOf(lineStarts, Math.Min(entity.Offset + entity.Length, s.Length) - 1);

                    for (int line = first; line <= last; line++)
                    {
                        // The first entity to claim a line keeps it: a message has neither a quote
                        // in a quote nor code in code.
                        if (target[line] < 0)
                        {
                            target[line] = k;
                        }
                    }
                }
            }

            var blocks = new List<RichBlock>();

            for (int line = 0; line < lines;)
            {
                var quote = quoteOf[line];
                var next = line + 1;

                while (next < lines && quoteOf[next] == quote)
                {
                    next++;
                }

                if (quote >= 0)
                {
                    var block = new RichBlockQuote
                    {
                        HasCredit = false,
                        IsExpandable = entities[quote].Type is TextEntityTypeExpandableBlockQuote
                    };

                    AddLines(block.Children, s, entities, lineStarts, codeOf, line, next, schema);
                    blocks.Add(block);
                }
                else
                {
                    AddLines(blocks, s, entities, lineStarts, codeOf, line, next, schema);
                }

                line = next;
            }

            var document = new RichDocument(blocks);
            document.Reindex();
            return document;
        }

        private static int LineOf(List<int> lineStarts, int offset)
        {
            var index = lineStarts.BinarySearch(offset);
            return index >= 0 ? index : ~index - 1;
        }

        private static int LineEnd(List<int> lineStarts, int line, int length)
        {
            return line + 1 < lineStarts.Count ? lineStarts[line + 1] - 1 : length;
        }

        private static void AddLines(List<RichBlock> target, string s, Vector<TextEntity> entities, List<int> lineStarts, int[] codeOf, int from, int to, RichSchema schema)
        {
            for (int line = from; line < to;)
            {
                var code = codeOf[line];
                if (code < 0)
                {
                    var leaf = new RichTextLeaf();
                    AppendText(leaf, s, entities, lineStarts[line], LineEnd(lineStarts, line, s.Length), schema);

                    target.Add(new RichParagraphBlock(RichBlockKind.Paragraph, leaf));
                    line++;
                    continue;
                }

                var next = line + 1;
                while (next < to && codeOf[next] == code)
                {
                    next++;
                }

                // One block for the lines of one entity, its newlines kept inside the leaf, as
                // Enter in a code block keeps them.
                var block = new RichParagraphBlock(RichBlockKind.Preformatted)
                {
                    Language = entities[code].Type is TextEntityTypePreCode preCode ? preCode.Language ?? string.Empty : string.Empty
                };

                var start = lineStarts[line];
                var end = LineEnd(lineStarts, next - 1, s.Length);

                block.Text.AppendRun(s.Substring(start, end - start), RichMarks.None, null);
                target.Add(block);

                line = next;
            }
        }

        /// <summary>
        /// Appends s[start, end) with the inline entities over it. The text is cut wherever an
        /// entity starts or ends, and each piece gets the union of the marks over it and the
        /// innermost link; a custom emoji's text becomes its object.
        /// </summary>
        private static void AppendText(RichTextLeaf leaf, string s, Vector<TextEntity> entities, int start, int end, RichSchema schema)
        {
            if (end <= start)
            {
                return;
            }

            if (entities == null || entities.Count == 0)
            {
                leaf.AppendRun(s.Substring(start, end - start), RichMarks.None, null);
                return;
            }

            var cuts = new List<int> { start, end };

            foreach (var entity in entities)
            {
                if (IsInline(entity.Type, schema))
                {
                    AddCut(cuts, entity.Offset, start, end);
                    AddCut(cuts, entity.Offset + entity.Length, start, end);
                }
            }

            cuts.Sort();

            for (int i = 0; i < cuts.Count - 1; i++)
            {
                var from = cuts[i];
                var to = cuts[i + 1];

                if (to <= from)
                {
                    continue;
                }

                var marks = RichMarks.None;
                var link = default(RichLink);
                var emoji = default(TextEntity);

                foreach (var entity in entities)
                {
                    if (entity.Offset > from || entity.Offset + entity.Length < to || !IsInline(entity.Type, schema))
                    {
                        continue;
                    }

                    switch (entity.Type)
                    {
                        case TextEntityTypeCustomEmoji:
                            emoji = entity;
                            break;
                        case TextEntityTypeTextUrl textUrl:
                            link = RichLink.FromUrl(textUrl.Url);
                            break;
                        case TextEntityTypeMentionName mentionName:
                            link = RichLink.FromMentionName(mentionName.UserId);
                            break;
                        case TextEntityTypeDateTime dateTime:
                            link = RichLink.FromDateTime(dateTime.UnixTime, dateTime.FormattingType);
                            break;
                        default:
                            marks |= ToMark(entity.Type);
                            break;
                    }
                }

                if (emoji != null)
                {
                    // The object stands for the entity's whole text, so only the first piece of it
                    // adds it. An entity cut short by the line keeps the part inside it.
                    if (from == Math.Max(emoji.Offset, start))
                    {
                        var alternative = s.Substring(from, Math.Min(emoji.Offset + emoji.Length, end) - from);
                        leaf.AppendObject(RichInlineObject.FromCustomEmoji(((TextEntityTypeCustomEmoji)emoji.Type).CustomEmojiId, alternative), marks);
                    }

                    continue;
                }

                leaf.AppendRun(s.Substring(from, to - from), marks, link);
            }
        }

        private static void AddCut(List<int> cuts, int offset, int start, int end)
        {
            if (offset > start && offset < end && !cuts.Contains(offset))
            {
                cuts.Add(offset);
            }
        }

        private static bool IsInline(TextEntityType type, RichSchema schema)
        {
            return type switch
            {
                TextEntityTypeCustomEmoji => schema.AllowsCustomEmoji,
                TextEntityTypeTextUrl => schema.AllowsUrls,
                TextEntityTypeMentionName => schema.AllowsMentions,
                TextEntityTypeDateTime => schema.AllowsDates,
                _ => schema.AllowsMark(ToMark(type))
            };
        }

        // Detected entities - mentions, hashtags, URLs and the like - map to nothing: the server
        // finds them again in what is sent.
        private static RichMarks ToMark(TextEntityType type)
        {
            return type switch
            {
                TextEntityTypeBold => RichMarks.Bold,
                TextEntityTypeItalic => RichMarks.Italic,
                TextEntityTypeUnderline => RichMarks.Underline,
                TextEntityTypeStrikethrough => RichMarks.Strikethrough,
                TextEntityTypeSpoiler => RichMarks.Spoiler,
                TextEntityTypeCode => RichMarks.Monospace,
                _ => RichMarks.None
            };
        }

        // =====================================================================================
        // Out
        // =====================================================================================

        /// <summary>
        /// The document as one message: its lines joined by <c>\n</c>, a quote entity over each
        /// quote's lines and a pre entity over each code block. Expects a document in a
        /// <see cref="RichSchema"/>; anything else is read as its leaves, a line each.
        /// </summary>
        public static FormattedText ToFormattedText(RichDocument document)
        {
            var text = new StringBuilder();
            var entities = new MutableVector<TextEntity>();

            var first = true;

            foreach (var block in document.Blocks)
            {
                AppendBlock(block, text, entities, ref first);
            }

            return new FormattedText(text.ToString(), entities);
        }

        private static void AppendBlock(RichBlock block, StringBuilder text, MutableVector<TextEntity> entities, ref bool first)
        {
            switch (block)
            {
                case RichParagraphBlock paragraph:
                    {
                        Separate(text, ref first);

                        var start = text.Length;
                        RichClipboard.AppendLeaf(paragraph.Text, text, entities);

                        if (paragraph.Kind == RichBlockKind.Preformatted && text.Length > start)
                        {
                            entities.Add(new TextEntity(start, text.Length - start, string.IsNullOrEmpty(paragraph.Language)
                                ? new TextEntityTypePre()
                                : new TextEntityTypePreCode(paragraph.Language)));
                        }
                    }
                    break;

                case RichBlockQuote quote:
                    {
                        // The quote starts after the newline that separates it, not on it.
                        Separate(text, ref first);

                        var start = text.Length;
                        var inner = true;

                        foreach (var child in quote.Children)
                        {
                            AppendBlock(child, text, entities, ref inner);
                        }

                        if (text.Length > start)
                        {
                            entities.Add(new TextEntity(start, text.Length - start, quote.IsExpandable || quote.IsCollapsed
                                ? new TextEntityTypeExpandableBlockQuote()
                                : new TextEntityTypeBlockQuote()));
                        }

                        if (quote.HasCredit && !quote.Credit.IsEmpty)
                        {
                            Separate(text, ref first);
                            RichClipboard.AppendLeaf(quote.Credit, text, entities);
                        }
                    }
                    break;

                default:
                    {
                        var stops = new List<RichStop>();
                        block.CollectStops(stops);

                        foreach (var stop in stops)
                        {
                            if (stop.Leaf != null)
                            {
                                Separate(text, ref first);
                                RichClipboard.AppendLeaf(stop.Leaf, text, entities);
                            }
                        }
                    }
                    break;
            }
        }

        private static void Separate(StringBuilder text, ref bool first)
        {
            if (first)
            {
                first = false;
            }
            else
            {
                text.Append('\n');
            }
        }

        /// <summary>
        /// The length of <see cref="ToFormattedText"/>'s text, without building it: what a
        /// length limit counts on every keystroke.
        /// </summary>
        public static int GetLength(RichDocument document)
        {
            var leaves = document.Leaves;
            var length = Math.Max(leaves.Count - 1, 0);

            for (int i = 0; i < leaves.Count; i++)
            {
                var leaf = leaves[i];
                length += leaf.Length;

                if (leaf.Text.IndexOf(RichTextLeaf.ObjectChar) < 0)
                {
                    continue;
                }

                foreach (var run in leaf.Runs)
                {
                    if (run.Object != null)
                    {
                        length += run.Object.PlainText.Length - 1;
                    }
                }
            }

            return length;
        }
    }
}
