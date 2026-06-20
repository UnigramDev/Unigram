//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using System.Text;
using Markdig;
using Markdig.Extensions.Mathematics;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Telegram.Td.Api;

namespace Telegram.Common
{
    /// <summary>
    /// Mostly written by Claude, it's terrible, needs refactoring
    /// Parses Markdown (CommonMark + GFM + math) into a list of Instant View PageBlock
    /// objects, using Markdig as the underlying CommonMark parser.
    /// </summary>
    public static class MarkdownToInstantView
    {
        private static readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
        //.UsePipeTables()
        //.UseGridTables()
        //.UseTaskLists()
        //.UseEmphasisExtras() // strikethrough, sub/sup, marked
        //.UseAutoLinks()
        //.UseMathematics()
        //.UseFootnotes()
        //.UseDiagrams()
        //.Build();

        public static List<PageBlock> Parse(string markdown)
        {
            var result = new List<PageBlock>();
            if (string.IsNullOrEmpty(markdown)) return result;

            var doc = Markdown.Parse(markdown, _pipeline);
            foreach (var block in doc)
            {
                ConvertBlock(block, result);
            }
            return result;
        }

        // ----------------------------------------------------------------
        // Bare URL post-pass
        // Markdig's AutoLinkExtension misses some bare URLs in odd contexts
        // (e.g. after "( + word + space"). We do a cheap second pass over
        // RichTextPlain runs to catch those, with the same word-boundary
        // and trailing-punctuation rules GFM uses.
        // ----------------------------------------------------------------

        private static readonly string[] _bareUrlSchemes = { "https://", "http://", "ftp://", "www." };

        private static RichText AutolinkBareUrls(RichText rt)
        {
            switch (rt)
            {
                case RichTextPlain plain:
                    return AutolinkPlain(plain.Text);
                case RichTexts container:
                    {
                        var newParts = new List<RichText>(container.Texts.Count);
                        bool changed = false;
                        foreach (var part in container.Texts)
                        {
                            var converted = AutolinkBareUrls(part);
                            if (!ReferenceEquals(converted, part)) changed = true;
                            FlattenInto(converted, newParts);
                        }
                        if (!changed) return rt;
                        return Compact(newParts);
                    }
                // Wrappers carry inner RichText; recurse into it but don't autolink inside
                // already-link wrappers (avoid nested links) or code (intentional literal).
                case RichTextUrl _:
                case RichTextEmailAddress _:
                case RichTextFixed _:
                case RichTextMathematicalExpression _:
                    return rt;
                case RichTextBold b:
                    {
                        var inner = AutolinkBareUrls(b.Text);
                        if (ReferenceEquals(inner, b.Text)) return rt;
                        return new RichTextBold { Text = inner };
                    }
                case RichTextItalic i:
                    {
                        var inner = AutolinkBareUrls(i.Text);
                        if (ReferenceEquals(inner, i.Text)) return rt;
                        return new RichTextItalic { Text = inner };
                    }
                case RichTextStrikethrough s:
                    {
                        var inner = AutolinkBareUrls(s.Text);
                        if (ReferenceEquals(inner, s.Text)) return rt;
                        return new RichTextStrikethrough { Text = inner };
                    }
            }
            return rt;
        }

        private static void FlattenInto(RichText rt, List<RichText> sink)
        {
            if (rt is RichTexts rs)
            {
                foreach (var t in rs.Texts) FlattenInto(t, sink);
            }
            else
            {
                sink.Add(rt);
            }
        }

        private static RichText AutolinkPlain(string text)
        {
            if (string.IsNullOrEmpty(text)) return new RichTextPlain { Text = text };

            List<RichText> parts = null;
            int writeFrom = 0;
            int i = 0;
            while (i < text.Length)
            {
                int matchLen;
                string scheme = TryBareUrlAt(text, i, out matchLen);
                if (scheme == null)
                {
                    i++;
                    continue;
                }

                // Word boundary on the left: previous char must be at-start, whitespace,
                // or one of the GFM-permitted opening punctuation chars.
                if (i > 0)
                {
                    char prev = text[i - 1];
                    if (!IsBoundaryBefore(prev))
                    {
                        i++;
                        continue;
                    }
                }

                // Extend the URL as far as it can go.
                int end = i + matchLen;
                while (end < text.Length && IsUrlChar(text[end])) end++;

                // Trim trailing punctuation per GFM rules.
                end = TrimTrailingPunct(text, i, end);

                int urlBodyLen = end - i;
                if (urlBodyLen <= matchLen)
                {
                    // Only matched the scheme itself, no host - skip.
                    i++;
                    continue;
                }

                if (parts == null) parts = new List<RichText>();
                if (i > writeFrom)
                {
                    parts.Add(new RichTextPlain { Text = text.Substring(writeFrom, i - writeFrom) });
                }
                var displayUrl = text.Substring(i, urlBodyLen);
                var actualUrl = scheme == "www." ? "http://" + displayUrl : displayUrl;
                parts.Add(new RichTextUrl
                {
                    Text = new RichTextPlain { Text = displayUrl },
                    Url = actualUrl,
                    IsCached = false
                });
                writeFrom = end;
                i = end;
            }

            if (parts == null) return new RichTextPlain { Text = text };
            if (writeFrom < text.Length)
            {
                parts.Add(new RichTextPlain { Text = text.Substring(writeFrom) });
            }
            return parts.Count == 1 ? parts[0] : new RichTexts { Texts = parts };
        }

        private static string TryBareUrlAt(string s, int pos, out int len)
        {
            foreach (var scheme in _bareUrlSchemes)
            {
                if (pos + scheme.Length <= s.Length
                    && string.CompareOrdinal(s, pos, scheme, 0, scheme.Length) == 0)
                {
                    len = scheme.Length;
                    return scheme;
                }
            }
            len = 0;
            return null;
        }

        private static bool IsBoundaryBefore(char c)
        {
            if (char.IsWhiteSpace(c)) return true;
            // GFM allows opening with these
            return c == '(' || c == '[' || c == '{' || c == '<' || c == '"' || c == '\'';
        }

        private static bool IsUrlChar(char c)
        {
            if (c <= ' ') return false;
            if (c == '<' || c == '>' || c == '"') return false;
            return true;
        }

        private static int TrimTrailingPunct(string s, int start, int end)
        {
            while (end > start)
            {
                char last = s[end - 1];
                if (last == '.' || last == ',' || last == ';' || last == ':'
                    || last == '!' || last == '?' || last == '\'' || last == '"')
                {
                    end--;
                    continue;
                }
                if (last == ')' || last == ']' || last == '}' || last == '>')
                {
                    int open, close;
                    char openChar = last == ')' ? '(' : last == ']' ? '[' : last == '}' ? '{' : '<';
                    open = 0; close = 0;
                    for (int k = start; k < end; k++)
                    {
                        if (s[k] == openChar) open++;
                        else if (s[k] == last) close++;
                    }
                    if (close > open) { end--; continue; }
                }
                break;
            }
            return end;
        }

        // ----------------------------------------------------------------
        // Block conversion
        // ----------------------------------------------------------------

        private static void ConvertBlock(Block block, List<PageBlock> output)
        {
            switch (block)
            {
                case HeadingBlock h:
                    var inline = ConvertInlinesTopLevel(h.Inline);
                    output.Add(new PageBlockAnchor(Slugify(inline.ToPlainText())));
                    output.Add(MakeHeading(h.Level, inline));
                    break;

                case ParagraphBlock p:
                    output.Add(new PageBlockParagraph { Text = ConvertInlinesTopLevel(p.Inline) });
                    break;

                case QuoteBlock q:
                    ConvertQuote(q, output);
                    break;

                case ListBlock l:
                    ConvertList(l, output);
                    break;

                case MathBlock math:
                    output.Add(new PageBlockMathematicalExpression { Expression = JoinCodeLines(math).Trim() });
                    break;

                case FencedCodeBlock fenced:
                    output.Add(new PageBlockPreformatted
                    {
                        Text = new RichTextPlain { Text = JoinCodeLines(fenced) },
                        Language = fenced.Info ?? ""
                    });
                    break;

                case CodeBlock code: // indented code (FencedCodeBlock handled above)
                    output.Add(new PageBlockPreformatted
                    {
                        Text = new RichTextPlain { Text = JoinCodeLines(code) },
                        Language = ""
                    });
                    break;

                case ThematicBreakBlock _:
                    output.Add(new PageBlockDivider());
                    break;

                case Table table:
                    ConvertTable(table, output);
                    break;

                case HtmlBlock _:
                    // HTML is unsupported per design; emit as preformatted text so it's not lost.
                    output.Add(new PageBlockPreformatted
                    {
                        Text = new RichTextPlain { Text = JoinCodeLines((LeafBlock)block) },
                        Language = "html"
                    });
                    break;

                case ContainerBlock container:
                    // Fallback: recursively convert children.
                    foreach (var child in container)
                    {
                        ConvertBlock(child, output);
                    }
                    break;

                    // LinkReferenceDefinitionGroup, etc. — silently ignored, already resolved by Markdig.
            }
        }

        private static PageBlock MakeHeading(int level, RichText text)
        {
            return new PageBlockSectionHeading
            {
                Size = level,
                Text = text
            };
        }

        private static string JoinCodeLines(LeafBlock block)
        {
            // CodeBlock / FencedCodeBlock / MathBlock / HtmlBlock all expose their content as Lines.
            var sb = new StringBuilder();
            var lines = block.Lines.Lines;
            int count = block.Lines.Count;
            for (int i = 0; i < count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(lines[i].ToString());
            }
            return sb.ToString();
        }

        // --- Blockquote ---

        private static void ConvertQuote(QuoteBlock quote, List<PageBlock> output)
        {
            var trailing = new List<PageBlock>();

            foreach (var child in quote)
            {
                ConvertBlock(child, trailing);
            }

            output.Add(new PageBlockBlockQuote(trailing, null));
        }

        // --- Lists ---

        private static void ConvertList(ListBlock list, List<PageBlock> output)
        {
            bool ordered = list.IsOrdered;
            bool loose = list.IsLoose;

            if (ordered)
            {
                var items = new List<PageBlockListItem>();
                int autoNum = ParseStart(list.OrderedStart);
                foreach (var item in list)
                {
                    if (!(item is ListItemBlock li)) continue;
                    ExtractTaskMarker(li, out bool checkbox, out bool isChecked);
                    items.Add(BuildListItem(li, autoNum.ToString(), checkbox, isChecked));
                    autoNum++;
                }
                output.Add(new PageBlockList { Items = items });
            }
            else
            {
                var items = new List<PageBlockListItem>();
                foreach (var item in list)
                {
                    if (!(item is ListItemBlock li)) continue;
                    ExtractTaskMarker(li, out bool checkbox, out bool isChecked);
                    items.Add(BuildListItem(li, "•", checkbox, isChecked));
                }
                output.Add(new PageBlockList { Items = items });
            }
        }

        private static int ParseStart(string s)
        {
            if (string.IsNullOrEmpty(s)) return 1;
            return int.TryParse(s, out var n) ? n : 1;
        }

        private static void ExtractTaskMarker(ListItemBlock item, out bool checkbox, out bool isChecked)
        {
            // Markdig's TaskList extension inserts a TaskList inline at the start of the first paragraph.
            // The marker is followed by a space in source; after removing the marker, strip the leading
            // space from the next inline so the rendered text doesn't start with " ".
            checkbox = false;
            isChecked = false;
            if (item.Count > 0 && item[0] is ParagraphBlock p && p.Inline != null)
            {
                var first = p.Inline.FirstChild;
                if (first is TaskList task)
                {
                    checkbox = true;
                    isChecked = task.Checked;
                    var next = task.NextSibling;
                    task.Remove();
                    if (next is LiteralInline lit)
                    {
                        var c = lit.Content;
                        if (c.Length > 0 && c.Start < c.End && c.Text[c.Start] == ' ')
                        {
                            c.Start++;
                            lit.Content = c;
                        }
                    }
                }
            }
        }

        private static PageBlockListItem BuildListItem(ListItemBlock item, string num, bool checkbox, bool isChecked)
        {
            //// Tight list with single paragraph -> Text variant. Otherwise Blocks variant.
            //if (!loose && item.Count == 1 && item[0] is ParagraphBlock p)
            //{
            //    return new PageBlockListItem
            //    {
            //        Checkbox = checkbox,
            //        Checked = isChecked,
            //        Text = ConvertInlinesTopLevel(p.Inline)
            //    };
            //}
            //var blocks = new List<PageBlock>();
            //foreach (var child in item)
            //{
            //    ConvertBlock(child, blocks);
            //}
            //return new PageBlockListItemBlocks
            //{
            //    Checkbox = checkbox,
            //    Checked = isChecked,
            //    PageBlocks = blocks
            //};

            var trailing = new List<PageBlock>();

            foreach (var child in item)
            {
                ConvertBlock(child, trailing);
            }

            return new PageBlockListItem
            {
                Blocks = trailing,
                Label = num,
                HasCheckbox = checkbox,
                IsChecked = isChecked
            };


            return null;
        }

        // --- Tables ---

        private static void ConvertTable(Table table, List<PageBlock> output)
        {
            var rows = new List<IList<PageBlockTableCell>>();
            var aligns = table.ColumnDefinitions;

            foreach (var child in table)
            {
                if (!(child is TableRow row)) continue;
                var cells = new List<PageBlockTableCell>();
                int colIdx = 0;
                foreach (var cellChild in row)
                {
                    if (!(cellChild is TableCell cell)) continue;
                    var align = colIdx < aligns.Count ? aligns[colIdx].Alignment : null;
                    cells.Add(new PageBlockTableCell
                    {
                        Text = ConvertTableCellContent(cell),
                        IsHeader = row.IsHeader,
                        Colspan = cell.ColumnSpan > 0 ? cell.ColumnSpan : 1,
                        Rowspan = cell.RowSpan > 0 ? cell.RowSpan : 1,
                        Align = ConvertAlign(align),
                        Valign = new PageBlockVerticalAlignmentMiddle()
                    });
                    colIdx += cell.ColumnSpan > 0 ? cell.ColumnSpan : 1;
                }
                rows.Add(cells);
            }

            output.Add(new PageBlockTable
            {
                Caption = new RichTextPlain { Text = "" },
                Cells = rows,
                IsBordered = false,
                IsStriped = false
            });
        }

        private static RichText ConvertTableCellContent(TableCell cell)
        {
            // A TableCell holds blocks; for IV cells we want a single RichText.
            // GFM tables put a single ParagraphBlock per cell; concatenate if there are several.
            var parts = new List<RichText>();
            foreach (var child in cell)
            {
                if (child is ParagraphBlock p)
                {
                    if (parts.Count > 0) parts.Add(new RichTextPlain { Text = "\n" });
                    parts.Add(ConvertInlinesTopLevel(p.Inline));
                }
            }
            if (parts.Count == 0) return new RichTextPlain { Text = "" };
            if (parts.Count == 1) return parts[0];
            return new RichTexts { Texts = parts };
        }

        private static PageBlockHorizontalAlignment ConvertAlign(TableColumnAlign? a)
        {
            switch (a)
            {
                case TableColumnAlign.Center: return new PageBlockHorizontalAlignmentCenter();
                case TableColumnAlign.Right: return new PageBlockHorizontalAlignmentRight();
                default: return new PageBlockHorizontalAlignmentLeft();
            }
        }

        // ----------------------------------------------------------------
        // Inline conversion
        // ----------------------------------------------------------------

        // For block-level inline content: apply the bare-URL post-pass.
        private static RichText ConvertInlinesTopLevel(ContainerInline container)
        {
            return AutolinkBareUrls(ConvertInlines(container));
        }

        // For nested inline contexts (e.g. inside a link, emphasis): no post-pass,
        // since we don't want to re-autolink the contents of an existing link or
        // produce nested URLs.
        private static RichText ConvertInlines(ContainerInline container)
        {
            if (container == null) return new RichTextPlain { Text = "" };
            var parts = new List<RichText>();
            foreach (var inline in container)
            {
                var rt = ConvertInline(inline);
                if (rt != null) parts.Add(rt);
            }
            return Compact(parts);
        }

        private static RichText ConvertInline(Inline inline)
        {
            switch (inline)
            {
                case LiteralInline lit:
                    return new RichTextPlain { Text = lit.Content.ToString() };

                case LineBreakInline _:
                    // Soft and hard line breaks both flatten to a space (RichText has no break primitive).
                    return new RichTextPlain { Text = " " };

                case CodeInline code:
                    return new RichTextFixed { Text = new RichTextPlain { Text = code.Content } };

                case EmphasisInline em:
                    return ConvertEmphasis(em);

                case LinkInline link:
                    return ConvertLink(link);

                case AutolinkInline auto:
                    return ConvertAutolink(auto);

                case Markdig.Extensions.Mathematics.MathInline math:
                    return new RichTextMathematicalExpression { Expression = math.Content.ToString() };

                case TaskList tl:
                    // Leftover task marker (shouldn't happen — we strip them in ExtractTaskMarker).
                    return new RichTextPlain { Text = tl.Checked ? "[x] " : "[ ] " };

                case HtmlInline _:
                case HtmlEntityInline _:
                    // HTML is unsupported; preserve as literal text via the source-side reproduction
                    // Markdig already gives us in HtmlInline.Tag (or HtmlEntityInline.Transcoded).
                    if (inline is HtmlInline h) return new RichTextPlain { Text = h.Tag };
                    if (inline is HtmlEntityInline he) return new RichTextPlain { Text = he.Transcoded.ToString() };
                    return null;

                case ContainerInline ci:
                    // Unknown container -> flatten children.
                    return ConvertInlines(ci);
            }

            return null;
        }

        private static RichText ConvertEmphasis(EmphasisInline em)
        {
            // DelimiterChar + DelimiterCount tells us what marker was used.
            // GFM/Markdig: ** or __ with count=2 -> strong; * or _ with count=1 -> italic;
            // ~~ -> strikethrough; ^...^ -> superscript; ~...~ -> subscript; ==...== -> marked.
            // We only honor: bold, italic, strikethrough. (Per design: no underline/marked/sub/super in MD.)
            var inner = ConvertInlines(em);
            char d = em.DelimiterChar;
            int n = em.DelimiterCount;

            if (d == '~' && n == 2)
            {
                return new RichTextStrikethrough { Text = inner };
            }
            if (n >= 2)
            {
                return new RichTextBold { Text = inner };
            }
            return new RichTextItalic { Text = inner };
        }

        private static RichText ConvertLink(LinkInline link)
        {
            // Images: per design, emit a RichTextUrl with the alt text and the URL.
            // Regular links: same shape.
            var url = link.GetDynamicUrl?.Invoke() ?? link.Url ?? "";
            var innerText = ConvertInlines(link);

            // If inner is empty (e.g. image with no alt), fall back to the URL as display text.
            if (IsEmpty(innerText))
            {
                innerText = new RichTextPlain { Text = url };
            }

            if (url.StartsWith('#'))
            {
                return new RichTextAnchorLink
                {
                    Text = innerText,
                    Url = url,
                    AnchorName = url.TrimStart('#')
                };
            }

            return new RichTextUrl
            {
                Text = innerText,
                Url = url,
                IsCached = false
            };
        }

        private static RichText ConvertAutolink(AutolinkInline auto)
        {
            var url = auto.Url ?? "";
            if (auto.IsEmail)
            {
                return new RichTextEmailAddress
                {
                    Text = new RichTextPlain { Text = url },
                    EmailAddress = url
                };
            }
            return new RichTextUrl
            {
                Text = new RichTextPlain { Text = url },
                Url = url,
                IsCached = false
            };
        }

        // ----------------------------------------------------------------
        // RichText compaction
        // ----------------------------------------------------------------

        /// <summary>
        /// Merges adjacent RichTextPlain runs into a single one, drops empties,
        /// and unwraps singletons. Produces a clean shape: one RichText if there's
        /// only one part, a RichTexts wrapper otherwise.
        /// </summary>
        private static RichText Compact(List<RichText> parts)
        {
            // Merge consecutive plain runs.
            var merged = new List<RichText>(parts.Count);
            StringBuilder pending = null;
            foreach (var part in parts)
            {
                if (part is RichTextPlain plain)
                {
                    if (string.IsNullOrEmpty(plain.Text)) continue;
                    if (pending == null) pending = new StringBuilder();
                    pending.Append(plain.Text);
                }
                else
                {
                    if (pending != null && pending.Length > 0)
                    {
                        merged.Add(new RichTextPlain { Text = pending.ToString() });
                        pending = null;
                    }
                    merged.Add(part);
                }
            }
            if (pending != null && pending.Length > 0)
            {
                merged.Add(new RichTextPlain { Text = pending.ToString() });
            }

            if (merged.Count == 0) return new RichTextPlain { Text = "" };
            if (merged.Count == 1) return merged[0];
            return new RichTexts { Texts = merged };
        }

        private static bool IsEmpty(RichText rt)
        {
            return rt is RichTextPlain p && string.IsNullOrEmpty(p.Text);
        }

        private static string Slugify(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            var sb = new StringBuilder(text.Length);
            foreach (var ch in text)
            {
                if (char.IsLetterOrDigit(ch) || ch == '_' || ch == '-')
                {
                    sb.Append(char.ToLowerInvariant(ch));
                }
                else if (char.IsWhiteSpace(ch))
                {
                    sb.Append('-');
                }
            }
            return sb.ToString();
        }
    }
}
