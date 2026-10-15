//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;

namespace Telegram.Controls.RichEditor
{
    /// <summary>
    /// What a document may hold, when it is less than a rich message: the text of an ordinary
    /// message, a caption, a poll option. The engine checks every way content arrives - typing
    /// rules, commands, paste, loading - against it, so nothing it refuses can be made at all.
    /// </summary>
    public sealed class RichSchema
    {
        public const RichMarks MessageMarks = RichMarks.Bold | RichMarks.Italic | RichMarks.Underline | RichMarks.Strikethrough | RichMarks.Spoiler | RichMarks.Monospace;

        /// <summary>Everything a message's text can carry, as <c>textEntityType*</c> has it.</summary>
        public static readonly RichSchema Message = new(MessageMarks, true, true, true, true, true, true);

        public RichSchema(RichMarks marks, bool urls, bool mentions, bool dates, bool customEmoji, bool quotes, bool code)
        {
            Marks = marks & MessageMarks;
            AllowsUrls = urls;
            AllowsMentions = mentions;
            AllowsDates = dates;
            AllowsCustomEmoji = customEmoji;
            AllowsQuotes = quotes;
            AllowsCode = code;
        }

        public RichMarks Marks { get; }

        /// <summary>Text URLs, which e-mail addresses and phone numbers are sent as.</summary>
        public bool AllowsUrls { get; }

        public bool AllowsMentions { get; }

        public bool AllowsDates { get; }

        public bool AllowsCustomEmoji { get; }

        public bool AllowsQuotes { get; }

        public bool AllowsCode { get; }

        public bool AllowsMark(RichMarks mark) => mark != RichMarks.None && (Marks & mark) == mark;

        public bool AllowsLink(RichLink link)
        {
            return link == null || link.Kind switch
            {
                RichLinkKind.MentionName => AllowsMentions,
                RichLinkKind.DateTime => AllowsDates,
                _ => AllowsUrls
            };
        }

        public bool AllowsObject(RichInlineObject inline) => inline.Kind == RichInlineKind.CustomEmoji && AllowsCustomEmoji;

        /// <summary>
        /// A block this schema keeps as it is. Quotes hold paragraphs and code only - a message
        /// has no quote within a quote - and code holds no objects by definition.
        /// </summary>
        public bool AllowsBlock(RichBlock block, bool inQuote = false)
        {
            return block switch
            {
                RichParagraphBlock { Kind: RichBlockKind.Paragraph } => true,
                RichParagraphBlock { Kind: RichBlockKind.Preformatted } => AllowsCode,
                RichBlockQuote => AllowsQuotes && !inQuote,
                _ => false
            };
        }

        public bool AllowsTextKind(RichBlockKind kind) => kind == RichBlockKind.Paragraph || (kind == RichBlockKind.Preformatted && AllowsCode);

        // =====================================================================================
        // Conforming
        // =====================================================================================

        /// <summary>
        /// Rewrites <paramref name="blocks"/> into what this schema allows, keeping every word of
        /// text: a heading becomes a paragraph, a list its items with their bullets or numbers in
        /// front, a table a line per row, a photo its caption. What has no text - a divider, a
        /// photo without a caption, a button row - goes.
        /// </summary>
        public void Conform(List<RichBlock> blocks)
        {
            var result = new List<RichBlock>(blocks.Count);
            Conform(blocks, result, false);

            blocks.Clear();
            blocks.AddRange(result);
        }

        private void Conform(List<RichBlock> blocks, List<RichBlock> result, bool inQuote)
        {
            foreach (var block in blocks)
            {
                Conform(block, result, inQuote);
            }
        }

        private void Conform(RichBlock block, List<RichBlock> result, bool inQuote)
        {
            switch (block)
            {
                case RichParagraphBlock paragraph:
                    if (!AllowsTextKind(paragraph.Kind))
                    {
                        paragraph.SetKind(RichBlockKind.Paragraph);
                    }

                    Conform(paragraph.Text);
                    result.Add(paragraph);
                    break;

                case RichBlockQuote quote when AllowsQuotes && !inQuote:
                    {
                        var children = new List<RichBlock>(quote.Children.Count);
                        Conform(quote.Children, children, true);

                        quote.Children.Clear();
                        quote.Children.AddRange(children);
                        quote.HasCredit = false;

                        result.Add(quote);
                        AddLine(quote.Credit, result);
                    }
                    break;

                case RichBlockQuote quote:
                    Conform(quote.Children, result, inQuote);
                    AddLine(quote.Credit, result);
                    break;

                case RichTextQuote pull:
                    AddLine(pull.Text, result, keepEmpty: true);
                    AddLine(pull.Credit, result);
                    break;

                case RichListBlock list:
                    for (int i = 0; i < list.Items.Count; i++)
                    {
                        var item = list.Items[i];
                        var first = result.Count;

                        Conform(item.Children, result, inQuote);

                        // The marker goes on the item's first line of text, as a plain-text copy
                        // of the list would read.
                        if (first < result.Count && result[first] is RichParagraphBlock { Kind: RichBlockKind.Paragraph } line)
                        {
                            line.Text.Insert(0, Marker(list, item, i), RichMarks.None, null);
                        }
                    }
                    break;

                case RichDetailsBlock details:
                    AddLine(details.Header, result, keepEmpty: true);
                    Conform(details.Children, result, inQuote);
                    break;

                case RichTableBlock table:
                    AddLine(table.Caption, result);

                    foreach (var row in table.Rows)
                    {
                        var line = new RichTextLeaf();

                        for (int i = 0; i < row.Count; i++)
                        {
                            if (i > 0)
                            {
                                line.AppendRun(" | ", RichMarks.None, null);
                            }

                            line.Insert(line.Length, row[i].Text);
                        }

                        AddLine(line, result);
                    }
                    break;

                case RichMathBlock math when !string.IsNullOrEmpty(math.Expression):
                    {
                        var line = new RichTextLeaf();
                        line.AppendRun(math.Expression, RichMarks.None, null);
                        result.Add(new RichParagraphBlock(RichBlockKind.Paragraph, line));
                    }
                    break;

                case RichMediaBlock media:
                    AddLine(media.Caption, result);
                    AddLine(media.Credit, result);
                    break;

                case RichMediaGroupBlock group:
                    AddLine(group.Caption, result);
                    AddLine(group.Credit, result);
                    break;

                case RichMapBlock map:
                    AddLine(map.Caption, result);
                    AddLine(map.Credit, result);
                    break;
            }
        }

        private static string Marker(RichListBlock list, RichListItem item, int index)
        {
            return list.Style switch
            {
                RichListStyle.Ordered => (list.Start + index).ToString() + ". ",
                RichListStyle.Checklist => item.IsChecked ? "☑ " : "☐ ",
                _ => "• "
            };
        }

        /// <summary>The leaf's text as a paragraph of its own, whatever role it had.</summary>
        private void AddLine(RichTextLeaf leaf, List<RichBlock> result, bool keepEmpty = false)
        {
            if (leaf.IsEmpty && !keepEmpty)
            {
                return;
            }

            var line = new RichTextLeaf();
            line.Insert(0, leaf);
            Conform(line);

            result.Add(new RichParagraphBlock(RichBlockKind.Paragraph, line));
        }

        /// <summary>Takes off the marks, links and objects this schema has no place for. Objects read as their text.</summary>
        public void Conform(RichTextLeaf leaf)
        {
            if (leaf.IsPlain || IsConforming(leaf))
            {
                return;
            }

            var conformed = new RichTextLeaf(leaf.Role);
            var source = leaf.Text;
            var position = 0;

            foreach (var run in leaf.Runs)
            {
                var marks = run.Marks & Marks;

                if (run.Object is RichInlineObject inline)
                {
                    if (AllowsObject(inline))
                    {
                        conformed.AppendObject(inline, marks);
                    }
                    else
                    {
                        conformed.AppendRun(inline.PlainText, marks, null);
                    }
                }
                else
                {
                    conformed.AppendRun(source.Substring(position, run.Length), marks, AllowsLink(run.Link) ? run.Link : null);
                }

                position += run.Length;
            }

            leaf.SetContent(conformed);
        }

        private bool IsConforming(RichTextLeaf leaf)
        {
            foreach (var run in leaf.Runs)
            {
                if ((run.Marks & ~Marks) != 0 || !AllowsLink(run.Link) || (run.Object != null && !AllowsObject(run.Object)))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
