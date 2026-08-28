//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Telegram.Td.Api;

namespace Telegram.Common
{
    /// <summary>
    /// Parses the clipboard HTML that Telegram Android writes
    /// (<c>org/telegram/ui/iv/RichHtml.java</c>) and that this app's own rich editor writes
    /// (<c>Libraries/unigram-iv-editor/src/clipboard.js</c>) into page blocks — the inverse of
    /// <see cref="PageBlockHelper.Flatten"/>'s world, and the way rich content pasted from
    /// another client keeps its structure instead of arriving as loose text.
    /// </summary>
    /// <remarks>
    /// <para>Unlike Android — whose editor holds a flat list of rows — this builds the nested
    /// <c>pageBlock*</c> tree directly, so a list, a quote of blocks and a collapsible section
    /// come out the shape TDLib expects.</para>
    /// <para>Media is dropped. The HTML carries only a file id, which means nothing outside the
    /// process that copied it, so a photo can't be reconstructed here — Android does the same
    /// with an id it can't resolve. A caption left without its media becomes a paragraph, so
    /// the words at least survive.</para>
    /// <para>Anything unrecognized is descended into rather than dropped, which is what makes
    /// this survive the wrapper soup real browsers put on the clipboard.</para>
    /// </remarks>
    public static class RichHtml
    {
        /// <summary>
        /// The attribute the rich editor stamps on the first element of everything it
        /// copies (<c>ORIGIN_ATTRIBUTE</c> in <c>clipboard.js</c>). Keep the two in sync.
        /// </summary>
        public const string OriginAttribute = "data-telegram-rich";

        /// <summary>
        /// Whether this fragment was copied out of this app. Foreign HTML — a web page,
        /// a document — still becomes message text when it can be said with entities, but
        /// it never reopens the rich editor: that is a separate window and a paid feature,
        /// and nobody expects one because they copied a heading.
        /// </summary>
        public static bool IsOwnFragment(string html)
        {
            return html != null && html.IndexOf(OriginAttribute, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Parses an HTML fragment into page blocks. Returns an empty list when there is
        /// nothing to paste — the caller decides what an empty paste means.
        /// </summary>
        public static Vector<PageBlock> Parse(string html)
        {
            var blocks = new MutableVector<PageBlock>();
            if (string.IsNullOrEmpty(html))
            {
                return blocks;
            }

            ParseBlocks(Tokenize(html), blocks);
            return blocks;
        }

        #region Blocks

        private static void ParseBlocks(List<Node> nodes, MutableVector<PageBlock> output)
        {
            // Inline content between block tags is collected here and flushed as a
            // paragraph the moment a block interrupts it.
            MutableVector<RichText> pending = null;

            foreach (var node in nodes)
            {
                if (node.IsText)
                {
                    var text = Collapse(Decode(node.Text));
                    if (IsBlank(text))
                    {
                        continue;
                    }

                    (pending ??= new MutableVector<RichText>()).Add(new RichTextPlain { Text = text });
                    continue;
                }

                // A block anchor is written as an empty <a data-anchor>, which would
                // otherwise be swallowed by the inline path below.
                if (node.Tag == "a" && node.Has("data-anchor"))
                {
                    pending = Flush(output, pending);
                    output.Add(new PageBlockAnchor { Name = node.Attribute("data-anchor") ?? string.Empty });
                    continue;
                }

                if (IsInline(node.Tag))
                {
                    var inline = InlineNode(node, false);
                    if (inline != null)
                    {
                        (pending ??= new MutableVector<RichText>()).Add(inline);
                    }
                    continue;
                }

                pending = Flush(output, pending);
                ParseBlock(node, output);
            }

            Flush(output, pending);
        }

        private static void ParseBlock(Node node, MutableVector<PageBlock> output)
        {
            switch (node.Tag)
            {
                case "p":
                    output.Add(new PageBlockParagraph { Text = InlineOf(node) });
                    return;

                case "h1":
                case "h2":
                case "h3":
                case "h4":
                case "h5":
                case "h6":
                    output.Add(new PageBlockSectionHeading
                    {
                        Size = node.Tag[1] - '0',
                        Text = InlineOf(node)
                    });
                    return;

                case "footer":
                    output.Add(new PageBlockFooter { Footer = InlineOf(node) });
                    return;

                case "pre":
                    output.Add(new PageBlockPreformatted
                    {
                        // Whitespace is content here, so the inline walk keeps it verbatim.
                        Text = InlineChildren(node, true) ?? EmptyText(),
                        Language = node.Attribute("language") ?? node.Attribute("lang") ?? node.Attribute("data-language") ?? string.Empty
                    });
                    return;

                case "hr":
                    output.Add(new PageBlockDivider());
                    return;

                case "ul":
                case "ol":
                    ParseList(node, output, node.Tag == "ol");
                    return;

                case "blockquote":
                    ParseBlockQuote(node, output);
                    return;

                case "details":
                    ParseDetails(node, output);
                    return;

                case "table":
                    ParseTable(node, output);
                    return;

                case "figure":
                    // The media is gone; keep whatever the caption said.
                    AppendCaptionAsParagraph(node, output);
                    return;

                case "div":
                    ParseDiv(node, output);
                    return;

                // Media, and the elements that only ever carry it.
                case "img":
                case "video":
                case "audio":
                case "location":
                case "source":
                case "picture":
                    return;

                // Nothing a document can use.
                case "script":
                case "style":
                case "head":
                case "meta":
                case "link":
                case "title":
                case "col":
                case "colgroup":
                    return;

                case "summary":
                    // Only reachable outside a <details>, where it is just a line of text.
                    output.Add(new PageBlockParagraph { Text = InlineOf(node) });
                    return;

                default:
                    // Unknown wrappers (section, article, main, body, and the div soup
                    // browsers put on the clipboard) contribute their children.
                    ParseBlocks(node.Children, output);
                    return;
            }
        }

        private static void ParseDiv(Node node, MutableVector<PageBlock> output)
        {
            if (node.HasClass("pm-math-block"))
            {
                output.Add(new PageBlockMathematicalExpression
                {
                    Expression = node.Attribute("data-latex") ?? PlainTextOf(node)
                });
                return;
            }

            if (node.HasClass("pm-button-row"))
            {
                // The whole block travels as JSON: its buttons carry a style and a type
                // there is no authoring UI for, and nothing here could rebuild them.
                if (FromJson(node.Attribute("data-block")) is PageBlockButtonRow row)
                {
                    output.Add(row);
                }
                return;
            }

            if (node.HasClass("collage") || node.HasClass("slideshow"))
            {
                // A gallery whose media can't be resolved is no gallery at all.
                AppendCaptionAsParagraph(node, output);
                return;
            }

            // A <div> wrapping blocks is a wrapper; one wrapping text is a paragraph.
            // A white-space:pre wrapper — an editor writing code one line per div — could
            // become preformatted instead, which is the shape a code paste wants; pre-wrap
            // is ordinary prose all over the web, so telling the two apart needs a real
            // source to calibrate against first.
            if (HasBlockChild(node))
            {
                ParseBlocks(node.Children, output);
            }
            else
            {
                var text = InlineChildren(node, false);
                if (text != null)
                {
                    output.Add(new PageBlockParagraph { Text = Trim(text) });
                }
            }
        }

        private static void ParseList(Node node, MutableVector<PageBlock> output, bool ordered)
        {
            var items = new MutableVector<PageBlockListItem>();
            var value = 1;

            // The blocks of the item last added, kept here rather than read back off it: Blocks is
            // declared as an immutable vector, and a nested list has to append to it.
            MutableVector<PageBlock> lastBlocks = null;

            foreach (var child in node.Children)
            {
                if (child.IsText)
                {
                    continue;
                }

                // Android writes a nested list as a SIBLING of the <li> it belongs to
                // rather than inside it. Give it back to the item above.
                if (child.Tag == "ul" || child.Tag == "ol")
                {
                    if (lastBlocks != null)
                    {
                        ParseList(child, lastBlocks, child.Tag == "ol");
                    }

                    continue;
                }

                if (child.Tag != "li")
                {
                    continue;
                }

                var blocks = new MutableVector<PageBlock>();
                ParseBlocks(child.Children, blocks);

                if (blocks.Count == 0)
                {
                    blocks.Add(new PageBlockParagraph { Text = EmptyText() });
                }

                lastBlocks = blocks;

                items.Add(new PageBlockListItem
                {
                    Blocks = blocks,
                    // Both are presence tests on Android, so an unchecked item never
                    // carries data-checked at all.
                    HasCheckbox = child.Has("data-checkbox") || child.HasClass("checkbox"),
                    IsChecked = child.Has("data-checked"),
                    Label = ordered ? value.ToString(CultureInfo.InvariantCulture) : string.Empty,
                    Value = ordered ? value : 0,
                    // Empty means unordered — this is what tells the two apart on load.
                    Type = ordered ? "1" : string.Empty
                });

                if (ordered)
                {
                    value++;
                }
            }

            if (items.Count > 0)
            {
                output.Add(new PageBlockList { Items = items });
            }
        }

        private static void ParseBlockQuote(Node node, MutableVector<PageBlock> output)
        {
            Node cite = null;
            var hasBlocks = false;

            foreach (var child in node.Children)
            {
                if (child.IsText || child.Tag == null)
                {
                    continue;
                }
                if (child.Tag == "cite")
                {
                    cite ??= child;
                }
                else if (!IsInline(child.Tag))
                {
                    hasBlocks = true;
                }
            }

            var credit = cite != null ? InlineOf(cite) : EmptyText();

            if (node.HasClass("pull"))
            {
                output.Add(new PageBlockPullQuote { Text = InlineExcept(node, "cite"), Credit = credit });
                return;
            }

            if (node.HasClass("expandable"))
            {
                output.Add(new PageBlockExpandableBlockQuote { Text = InlineExcept(node, "cite"), Credit = credit });
                return;
            }

            var blocks = new MutableVector<PageBlock>();
            if (hasBlocks)
            {
                var body = new List<Node>(node.Children.Count);
                foreach (var child in node.Children)
                {
                    if (child.IsText || child.Tag != "cite")
                    {
                        body.Add(child);
                    }
                }
                ParseBlocks(body, blocks);
            }
            else
            {
                blocks.Add(new PageBlockParagraph { Text = InlineExcept(node, "cite") });
            }

            if (blocks.Count == 0)
            {
                blocks.Add(new PageBlockParagraph { Text = EmptyText() });
            }

            output.Add(new PageBlockBlockQuote { Blocks = blocks, Credit = credit });
        }

        private static void ParseDetails(Node node, MutableVector<PageBlock> output)
        {
            RichText header = null;
            var body = new List<Node>(node.Children.Count);

            foreach (var child in node.Children)
            {
                if (!child.IsText && child.Tag == "summary")
                {
                    header ??= InlineOf(child);
                }
                else
                {
                    body.Add(child);
                }
            }

            var blocks = new MutableVector<PageBlock>();
            ParseBlocks(body, blocks);

            if (blocks.Count == 0)
            {
                blocks.Add(new PageBlockParagraph { Text = EmptyText() });
            }

            output.Add(new PageBlockDetails
            {
                Header = header ?? EmptyText(),
                Blocks = blocks,
                IsOpen = node.Has("open")
            });
        }

        private static void ParseTable(Node node, MutableVector<PageBlock> output)
        {
            var caption = EmptyText();
            var rows = new MutableVector<Vector<PageBlockTableCell>>();
            CollectRows(node, rows, ref caption);

            if (rows.Count == 0)
            {
                return;
            }

            output.Add(new PageBlockTable
            {
                Caption = caption,
                Cells = rows,
                IsBordered = node.Has("border"),
                IsStriped = node.HasClass("striped")
            });
        }

        private static void CollectRows(Node node, MutableVector<Vector<PageBlockTableCell>> rows, ref RichText caption)
        {
            foreach (var child in node.Children)
            {
                if (child.IsText)
                {
                    continue;
                }

                switch (child.Tag)
                {
                    case "caption":
                        caption = InlineOf(child);
                        break;
                    case "thead":
                    case "tbody":
                    case "tfoot":
                        CollectRows(child, rows, ref caption);
                        break;
                    case "tr":
                        rows.Add(ParseRow(child));
                        break;
                }
            }
        }

        private static Vector<PageBlockTableCell> ParseRow(Node row)
        {
            var cells = new MutableVector<PageBlockTableCell>();

            foreach (var child in row.Children)
            {
                if (child.IsText || (child.Tag != "td" && child.Tag != "th"))
                {
                    continue;
                }

                var align = child.Attribute("align") ?? AlignFromStyle(child.Attribute("style"));
                var valign = child.Attribute("valign") ?? child.Attribute("data-valign");

                cells.Add(new PageBlockTableCell
                {
                    Text = InlineOf(child),
                    IsHeader = child.Tag == "th" || child.Has("header"),
                    Colspan = ParseInt(child.Attribute("colspan"), 1),
                    Rowspan = ParseInt(child.Attribute("rowspan"), 1),
                    Align = string.Equals(align, "center", StringComparison.OrdinalIgnoreCase)
                        ? new PageBlockHorizontalAlignmentCenter()
                        : string.Equals(align, "right", StringComparison.OrdinalIgnoreCase)
                        ? new PageBlockHorizontalAlignmentRight()
                        : new PageBlockHorizontalAlignmentLeft(),
                    Valign = string.Equals(valign, "middle", StringComparison.OrdinalIgnoreCase)
                        ? new PageBlockVerticalAlignmentMiddle()
                        : string.Equals(valign, "bottom", StringComparison.OrdinalIgnoreCase)
                        ? new PageBlockVerticalAlignmentBottom()
                        : new PageBlockVerticalAlignmentTop()
                });
            }

            return cells;
        }

        private static void AppendCaptionAsParagraph(Node node, MutableVector<PageBlock> output)
        {
            foreach (var child in node.Children)
            {
                if (!child.IsText && child.Tag == "figcaption")
                {
                    var text = InlineChildren(child, false);
                    if (text != null)
                    {
                        output.Add(new PageBlockParagraph { Text = Trim(text) });
                    }
                    return;
                }
            }
        }

        private static MutableVector<RichText> Flush(MutableVector<PageBlock> output, MutableVector<RichText> pending)
        {
            if (pending != null && pending.Count > 0)
            {
                output.Add(new PageBlockParagraph { Text = Trim(Combine(pending)) });
            }

            return null;
        }

        private static bool HasBlockChild(Node node)
        {
            foreach (var child in node.Children)
            {
                if (!child.IsText && !IsInline(child.Tag))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Inline

        private static bool IsInline(string tag)
        {
            switch (tag)
            {
                case "a":
                case "animated-emoji":
                case "b":
                case "br":
                case "code":
                case "del":
                case "em":
                case "font":
                case "i":
                case "mark":
                case "s":
                case "span":
                case "spoiler":
                case "strike":
                case "strong":
                case "sub":
                case "sup":
                case "tt":
                case "u":
                    return true;
                default:
                    return false;
            }
        }

        private static RichText InlineOf(Node node)
        {
            return Trim(InlineChildren(node, false) ?? EmptyText());
        }

        private static RichText InlineExcept(Node node, string skip)
        {
            MutableVector<RichText> parts = null;

            foreach (var child in node.Children)
            {
                var text = child.IsText ? PlainOf(child.Text, false) : child.Tag == skip ? null : InlineNode(child, false);
                if (text != null)
                {
                    (parts ??= new MutableVector<RichText>()).Add(text);
                }
            }

            return Trim(Combine(parts) ?? EmptyText());
        }

        private static RichText InlineChildren(Node node, bool preserveWhitespace)
        {
            MutableVector<RichText> parts = null;

            foreach (var child in node.Children)
            {
                var text = child.IsText
                    ? PlainOf(child.Text, preserveWhitespace)
                    : InlineNode(child, preserveWhitespace);

                if (text != null)
                {
                    (parts ??= new MutableVector<RichText>()).Add(text);
                }
            }

            return Combine(parts);
        }

        private static RichText InlineNode(Node node, bool preserveWhitespace)
        {
            switch (node.Tag)
            {
                case "br":
                    return new RichTextPlain { Text = "\n" };

                case "b":
                case "strong":
                    return Wrap(node, preserveWhitespace, inner => new RichTextBold { Text = inner });
                case "i":
                case "em":
                    return Wrap(node, preserveWhitespace, inner => new RichTextItalic { Text = inner });
                case "u":
                    return Wrap(node, preserveWhitespace, inner => new RichTextUnderline { Text = inner });
                case "s":
                case "strike":
                case "del":
                    return Wrap(node, preserveWhitespace, inner => new RichTextStrikethrough { Text = inner });
                case "code":
                case "tt":
                    return Wrap(node, preserveWhitespace, inner => new RichTextFixed { Text = inner });
                case "spoiler":
                    return Wrap(node, preserveWhitespace, inner => new RichTextSpoiler { Text = inner });
                case "mark":
                    return Wrap(node, preserveWhitespace, inner => new RichTextMarked { Text = inner });
                case "sub":
                    return Wrap(node, preserveWhitespace, inner => new RichTextSubscript { Text = inner });
                case "sup":
                    return Wrap(node, preserveWhitespace, inner => new RichTextSuperscript { Text = inner });

                case "a":
                    return InlineAnchor(node, preserveWhitespace);

                case "animated-emoji":
                    return new RichTextCustomEmoji
                    {
                        CustomEmojiId = ParseLong(node.Attribute("data-document-id")),
                        AlternativeText = PlainTextOf(node)
                    };

                case "span":
                    return InlineSpan(node, preserveWhitespace);

                // Media never survives the clipboard, inline or not.
                case "img":
                case "video":
                case "audio":
                case "location":
                    return null;

                default:
                    return InlineChildren(node, preserveWhitespace);
            }
        }

        private static RichText InlineAnchor(Node node, bool preserveWhitespace)
        {
            // An anchor is a block, handled before this is ever reached; inline it has
            // nothing to contribute.
            if (node.Has("data-anchor"))
            {
                return null;
            }

            var inner = InlineChildren(node, preserveWhitespace);
            if (inner == null)
            {
                return null;
            }

            if (node.HasClass("pm-datetime"))
            {
                return new RichTextDateTime
                {
                    Text = inner,
                    UnixTime = ParseInt(node.Attribute("data-unix-time"), 0),
                    FormattingType = null
                };
            }

            if (node.HasClass("pm-mention"))
            {
                return new RichTextMentionName
                {
                    Text = inner,
                    UserId = ParseLong(node.Attribute("data-user-id"))
                };
            }

            var url = node.Attribute("href");
            if (string.IsNullOrEmpty(url))
            {
                return inner;
            }

            return new RichTextUrl
            {
                Text = inner,
                Url = url,
                IsCached = node.Has("data-cached")
            };
        }

        private static RichText InlineSpan(Node node, bool preserveWhitespace)
        {
            if (node.HasClass("pm-math-inline"))
            {
                return new RichTextMathematicalExpression
                {
                    Expression = node.Attribute("data-latex") ?? PlainTextOf(node)
                };
            }

            if (node.HasClass("pm-button"))
            {
                // Same as the button row: the button itself only survives as JSON. A
                // label with nothing behind it stays text.
                if (FromJson(node.Attribute("data-button")) is InlineButton button)
                {
                    return new RichTextButton { Button = button };
                }
            }

            return InlineChildren(node, preserveWhitespace);
        }

        private static RichText Wrap(Node node, bool preserveWhitespace, Func<RichText, RichText> wrap)
        {
            var inner = InlineChildren(node, preserveWhitespace);
            return inner == null ? null : wrap(inner);
        }

        private static RichText PlainOf(string raw, bool preserveWhitespace)
        {
            var text = Decode(raw);
            if (!preserveWhitespace)
            {
                text = Collapse(text);
            }

            return text.Length == 0 ? null : new RichTextPlain { Text = text };
        }

        private static RichText Combine(Vector<RichText> parts)
        {
            if (parts == null || parts.Count == 0)
            {
                return null;
            }

            return parts.Count == 1 ? parts[0] : new RichTexts { Texts = parts };
        }

        private static RichText EmptyText()
        {
            return new RichTextPlain { Text = string.Empty };
        }

        /// <summary>
        /// Trims the whitespace an HTML author left around a block's content, by trimming
        /// the first and last plain leaf of the tree rather than rebuilding it.
        /// </summary>
        private static RichText Trim(RichText text)
        {
            TrimEdge(text, true);
            TrimEdge(text, false);
            return text;
        }

        private static bool TrimEdge(RichText text, bool start)
        {
            switch (text)
            {
                case RichTextPlain plain:
                    if (string.IsNullOrEmpty(plain.Text))
                    {
                        return false;
                    }
                    plain.Text = start ? TrimStart(plain.Text) : TrimEnd(plain.Text);
                    // An all-whitespace leaf leaves the trimming to the next one along.
                    return plain.Text.Length > 0;

                case RichTexts texts:
                    if (texts.Texts != null)
                    {
                        for (int i = 0; i < texts.Texts.Count; i++)
                        {
                            var child = texts.Texts[start ? i : texts.Texts.Count - 1 - i];
                            if (TrimEdge(child, start))
                            {
                                return true;
                            }
                        }
                    }
                    return false;

                // Wrappers delegate to what they wrap; leaves with no text of their own
                // (custom emoji, math, buttons) stop the walk — there is nothing to trim
                // and anything past them is not an edge anymore.
                case RichTextBold bold: return TrimEdge(bold.Text, start);
                case RichTextItalic italic: return TrimEdge(italic.Text, start);
                case RichTextUnderline underline: return TrimEdge(underline.Text, start);
                case RichTextStrikethrough strikethrough: return TrimEdge(strikethrough.Text, start);
                case RichTextSpoiler spoiler: return TrimEdge(spoiler.Text, start);
                case RichTextFixed fixedText: return TrimEdge(fixedText.Text, start);
                case RichTextMarked marked: return TrimEdge(marked.Text, start);
                case RichTextSubscript subscript: return TrimEdge(subscript.Text, start);
                case RichTextSuperscript superscript: return TrimEdge(superscript.Text, start);
                case RichTextUrl url: return TrimEdge(url.Text, start);
                case RichTextDateTime dateTime: return TrimEdge(dateTime.Text, start);
                case RichTextMentionName mention: return TrimEdge(mention.Text, start);
                default: return true;
            }
        }

        private static string PlainTextOf(Node node)
        {
            var builder = new StringBuilder();
            AppendPlainText(node, builder);
            return builder.ToString();
        }

        private static void AppendPlainText(Node node, StringBuilder builder)
        {
            foreach (var child in node.Children)
            {
                if (child.IsText)
                {
                    builder.Append(Decode(child.Text));
                }
                else
                {
                    AppendPlainText(child, builder);
                }
            }
        }

        #endregion

        #region Text helpers

        private static bool IsBlank(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (!IsSpace(text[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsSpace(char c)
        {
            return c is ' ' or '\t' or '\n' or '\r' or '\f';
        }

        // TrimStart/TrimEnd would take U+00A0 with them, and a non-breaking space is
        // content: it is what an editor writes to keep an indent alive through collapsing.
        private static string TrimStart(string text)
        {
            var i = 0;
            while (i < text.Length && IsSpace(text[i]))
            {
                i++;
            }

            return i == 0 ? text : text.Substring(i);
        }

        private static string TrimEnd(string text)
        {
            var i = text.Length;
            while (i > 0 && IsSpace(text[i - 1]))
            {
                i--;
            }

            return i == text.Length ? text : text.Substring(0, i);
        }

        /// <summary>
        /// Collapses the runs of whitespace HTML uses for indentation into a single space,
        /// the way any renderer would. Our own clipboard HTML has none of it; the HTML real
        /// browsers write is full of it, and Android — which keeps it verbatim — pastes the
        /// indentation along with the text.
        /// </summary>
        private static string Collapse(string text)
        {
            var space = false;
            var i = 0;

            for (; i < text.Length; i++)
            {
                if (IsSpace(text[i]))
                {
                    if (space || text[i] != ' ')
                    {
                        break;
                    }
                    space = true;
                }
                else
                {
                    space = false;
                }
            }

            // Nothing to collapse — the common case, and the only one that allocates nothing.
            if (i == text.Length)
            {
                return text;
            }

            var builder = new StringBuilder(text.Length);
            builder.Append(text, 0, i);
            space = i > 0 && IsSpace(text[i - 1]);

            for (; i < text.Length; i++)
            {
                if (IsSpace(text[i]))
                {
                    if (!space)
                    {
                        builder.Append(' ');
                        space = true;
                    }
                }
                else
                {
                    builder.Append(text[i]);
                    space = false;
                }
            }

            return builder.ToString();
        }

        private static string Decode(string text)
        {
            if (text == null)
            {
                return string.Empty;
            }

            var amp = text.IndexOf('&');
            if (amp < 0)
            {
                return text;
            }

            var builder = new StringBuilder(text.Length);
            builder.Append(text, 0, amp);

            for (int i = amp; i < text.Length; i++)
            {
                if (text[i] != '&')
                {
                    builder.Append(text[i]);
                    continue;
                }

                var semi = text.IndexOf(';', i + 1);
                // Longer than any entity we know: an ampersand that just happens to sit
                // near a semicolon.
                if (semi < 0 || semi - i > 12)
                {
                    builder.Append(text[i]);
                    continue;
                }

                var entity = Entity(text.Substring(i + 1, semi - i - 1));
                if (entity == null)
                {
                    builder.Append(text[i]);
                }
                else
                {
                    builder.Append(entity);
                    i = semi;
                }
            }

            return builder.ToString();
        }

        private static string Entity(string name)
        {
            switch (name)
            {
                case "lt": return "<";
                case "gt": return ">";
                case "amp": return "&";
                case "quot": return "\"";
                case "apos": return "'";
                // The character, not a space: HTML only collapses the space it writes as one.
                case "nbsp": return "\u00A0";
            }

            if (name.Length > 1 && name[0] == '#')
            {
                var hex = name[1] is 'x' or 'X';
                var digits = name.Substring(hex ? 2 : 1);

                // Surrogate halves are excluded on purpose: ConvertFromUtf32 throws on them.
                if (int.TryParse(digits, hex ? NumberStyles.HexNumber : NumberStyles.Integer, CultureInfo.InvariantCulture, out int code)
                    && code > 0 && code <= 0x10FFFF && (code < 0xD800 || code > 0xDFFF))
                {
                    return char.ConvertFromUtf32(code);
                }
            }

            return null;
        }

        private static string AlignFromStyle(string style)
        {
            if (style == null)
            {
                return null;
            }

            var index = style.IndexOf("text-align", StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return null;
            }

            if (style.IndexOf("center", index, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "center";
            }

            if (style.IndexOf("right", index, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "right";
            }

            return null;
        }

        /// <summary>
        /// Reads a whole TDLib object back out of a data-* attribute. What can't be rebuilt
        /// from HTML — a button's style and type, and the block that holds them — travels as
        /// the JSON TDLib itself speaks.
        /// </summary>
        private static Td.Api.Object FromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            try
            {
                // Returns an Error rather than throwing on anything it doesn't recognize,
                // so the pattern match at the call site is the real guard.
                return ClientJson.FromJson(json);
            }
            catch
            {
                return null;
            }
        }

        private static int ParseInt(string value, int fallback)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) ? result : fallback;
        }

        private static long ParseLong(string value)
        {
            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result) ? result : 0;
        }

        #endregion

        #region Tokenizer

        private class Node
        {
            public string Tag;
            public string Text;
            public Dictionary<string, string> Attributes;
            public readonly List<Node> Children = new();

            public bool IsText => Tag == null;

            public string Attribute(string name)
            {
                return Attributes != null && Attributes.TryGetValue(name, out string value) ? value : null;
            }

            public bool Has(string name)
            {
                return Attributes != null && Attributes.ContainsKey(name);
            }

            // Substring, not a word match: this is what Android tests, and the classes it
            // looks for ("pull", "checkbox") are written exactly that way.
            public bool HasClass(string name)
            {
                var value = Attribute("class");
                return value != null && value.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        /// <summary>
        /// A tolerant tag scanner rather than a conforming HTML parser: unclosed tags,
        /// unquoted attributes and stray markup all have to survive, because this runs on
        /// whatever the clipboard happens to hold.
        /// </summary>
        private static List<Node> Tokenize(string html)
        {
            var roots = new List<Node>();
            var stack = new List<Node>();
            var position = 0;

            while (position < html.Length)
            {
                if (html[position] != '<')
                {
                    var next = html.IndexOf('<', position);
                    if (next < 0)
                    {
                        next = html.Length;
                    }

                    AddChild(stack, roots, new Node { Text = html.Substring(position, next - position) });
                    position = next;
                    continue;
                }

                if (string.CompareOrdinal(html, position, "<!--", 0, 4) == 0)
                {
                    var end = html.IndexOf("-->", Math.Min(position + 4, html.Length), StringComparison.Ordinal);
                    position = end < 0 ? html.Length : end + 3;
                    continue;
                }

                if (position + 1 < html.Length && html[position + 1] == '!')
                {
                    var end = html.IndexOf('>', position);
                    position = end < 0 ? html.Length : end + 1;
                    continue;
                }

                if (position + 1 < html.Length && html[position + 1] == '/')
                {
                    var end = html.IndexOf('>', position);
                    var nameEnd = end < 0 ? html.Length : end;
                    // "</" at the very end of the input has no name to close.
                    if (nameEnd > position + 2)
                    {
                        CloseTag(stack, html.Substring(position + 2, nameEnd - position - 2).Trim().ToLowerInvariant());
                    }
                    position = end < 0 ? html.Length : end + 1;
                    continue;
                }

                var tagEnd = FindTagEnd(html, position);
                if (tagEnd < 0)
                {
                    // Malformed to the end of the input: whatever is left is text.
                    AddChild(stack, roots, new Node { Text = html.Substring(position) });
                    break;
                }

                var inner = html.Substring(position + 1, tagEnd - position - 1);
                position = tagEnd + 1;

                var selfClosing = inner.EndsWith("/", StringComparison.Ordinal);
                if (selfClosing)
                {
                    inner = inner.Substring(0, inner.Length - 1);
                }

                var element = ParseTag(inner);
                if (element == null)
                {
                    continue;
                }

                AutoClose(stack, element.Tag);
                AddChild(stack, roots, element);

                if (!selfClosing && !IsVoid(element.Tag))
                {
                    stack.Add(element);
                }
            }

            return roots;
        }

        private static int FindTagEnd(string html, int from)
        {
            var quote = '\0';

            for (int i = from + 1; i < html.Length; i++)
            {
                var c = html[i];
                if (quote != '\0')
                {
                    if (c == quote)
                    {
                        quote = '\0';
                    }
                }
                else if (c is '"' or '\'')
                {
                    quote = c;
                }
                else if (c == '>')
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Closes the tags HTML closes for you: a paragraph ends at the next block, a list
        /// item at the next item, a cell at the next cell. Without this an unclosed
        /// &lt;p&gt; swallows the rest of the document as one paragraph — and unclosed tags
        /// are everywhere in the HTML real apps put on the clipboard.
        /// </summary>
        private static void AutoClose(List<Node> stack, string tag)
        {
            while (stack.Count > 0)
            {
                var closes = stack[stack.Count - 1].Tag switch
                {
                    "p" => !IsInline(tag),
                    "li" => tag == "li",
                    "td" or "th" => tag is "td" or "th" or "tr",
                    "tr" => tag == "tr",
                    _ => false
                };

                if (!closes)
                {
                    return;
                }

                stack.RemoveAt(stack.Count - 1);
            }
        }

        private static void CloseTag(List<Node> stack, string name)
        {
            for (int i = stack.Count - 1; i >= 0; i--)
            {
                if (stack[i].Tag == name)
                {
                    stack.RemoveRange(i, stack.Count - i);
                    return;
                }
            }
        }

        private static void AddChild(List<Node> stack, List<Node> roots, Node node)
        {
            if (node.IsText && node.Text.Length == 0)
            {
                return;
            }

            if (stack.Count == 0)
            {
                roots.Add(node);
            }
            else
            {
                stack[stack.Count - 1].Children.Add(node);
            }
        }

        private static Node ParseTag(string inner)
        {
            inner = inner.Trim();
            if (inner.Length == 0)
            {
                return null;
            }

            var i = 0;
            while (i < inner.Length && !IsSpace(inner[i]))
            {
                i++;
            }

            var node = new Node { Tag = inner.Substring(0, i).ToLowerInvariant() };
            if (node.Tag.Length == 0)
            {
                return null;
            }

            while (i < inner.Length)
            {
                while (i < inner.Length && IsSpace(inner[i]))
                {
                    i++;
                }

                if (i >= inner.Length)
                {
                    break;
                }

                var nameStart = i;
                while (i < inner.Length && inner[i] != '=' && !IsSpace(inner[i]))
                {
                    i++;
                }

                var name = inner.Substring(nameStart, i - nameStart).ToLowerInvariant();
                var value = string.Empty;

                while (i < inner.Length && IsSpace(inner[i]))
                {
                    i++;
                }

                if (i < inner.Length && inner[i] == '=')
                {
                    i++;
                    while (i < inner.Length && IsSpace(inner[i]))
                    {
                        i++;
                    }

                    if (i < inner.Length && (inner[i] is '"' or '\''))
                    {
                        var quote = inner[i];
                        var valueStart = ++i;
                        while (i < inner.Length && inner[i] != quote)
                        {
                            i++;
                        }

                        value = inner.Substring(valueStart, Math.Min(i, inner.Length) - valueStart);
                        if (i < inner.Length)
                        {
                            i++;
                        }
                    }
                    else
                    {
                        var valueStart = i;
                        while (i < inner.Length && !IsSpace(inner[i]))
                        {
                            i++;
                        }

                        value = inner.Substring(valueStart, i - valueStart);
                    }
                }

                if (name.Length > 0)
                {
                    node.Attributes ??= new Dictionary<string, string>();
                    node.Attributes[name] = Decode(value);
                }
            }

            return node;
        }

        private static bool IsVoid(string tag)
        {
            switch (tag)
            {
                case "br":
                case "hr":
                case "img":
                case "input":
                case "link":
                case "location":
                case "meta":
                case "source":
                case "wbr":
                    return true;
                default:
                    return false;
            }
        }

        #endregion
    }
}
