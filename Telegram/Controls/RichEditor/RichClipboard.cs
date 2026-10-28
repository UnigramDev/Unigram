//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Td.Api;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;
using Windows.UI.Xaml;

namespace Telegram.Controls.RichEditor
{
    /// <summary>
    /// The editor's clipboard formats.
    ///
    /// A copy goes out three ways: plain text; the chat's own text-with-entities format, so it
    /// pastes into the composer with its styles; and HTML, in the markup <see cref="RichHtml"/>
    /// reads, for everything else. The document
    /// itself, media included, stays in this process: the
    /// clipboard carries a token naming it, and only a paste that finds the same token gets it
    /// back whole. Files cannot round-trip through the clipboard, so a document can't either.
    /// </summary>
    public static class RichClipboard
    {
        public const string FormatId = "application/x-tl-rich-message";

        private static RichDocument _fragment;
        private static string _token;

        // A stream, not the token as a string: once flushed, the clipboard hands a custom format
        // back as a stream whatever it was set as.
        private static async Task<IRandomAccessStream> StoreAsync(RichDocument fragment)
        {
            _fragment = fragment;
            _token = Guid.NewGuid().ToString("N");

            using (var stream = new InMemoryRandomAccessStream())
            {
                using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
                {
                    writer.WriteInt32((int)writer.MeasureString(_token));
                    writer.WriteString(_token);

                    await writer.FlushAsync();
                    await writer.StoreAsync();
                }

                stream.Seek(0);
                return stream.CloneStream();
            }
        }

        /// <summary>
        /// The fragment the last copy in this process put on the clipboard, while the clipboard
        /// still holds it; null for anything else.
        /// </summary>
        public static async Task<RichDocument> TakeAsync(DataPackageView content)
        {
            if (_token == null || !content.Contains(FormatId) || await content.GetDataAsync(FormatId) is not IRandomAccessStream stream)
            {
                return null;
            }

            using (var reader = new DataReader(stream.GetInputStreamAt(0)))
            {
                await reader.LoadAsync((uint)stream.Size);

                // The size can come back rounded up, hence the length ahead of the token.
                if (reader.UnconsumedBufferLength < 4)
                {
                    return null;
                }

                var length = reader.ReadInt32();
                if (length < 0 || length > reader.UnconsumedBufferLength)
                {
                    return null;
                }

                return reader.ReadString((uint)length) == _token ? _fragment : null;
            }
        }

        /// <summary>
        /// <see cref="CopyAsync"/>, with the toast <see cref="MessageHelper.CopyText(XamlRoot, FormattedText)"/>
        /// shows for a copy out of a chat.
        /// </summary>
        public static async void Copy(XamlRoot xamlRoot, RichDocument fragment)
        {
            if (await CopyAsync(fragment) && xamlRoot != null)
            {
                ToastPopup.Show(xamlRoot, Strings.TextCopied, ToastPopupIcon.Copied);
            }
        }

        /// <summary>
        /// The fragment on the clipboard in every form another place can take: plain text, the
        /// chat's text with entities, HTML, and a token naming the fragment itself for a paste
        /// into an editor.
        /// </summary>
        public static async Task<bool> CopyAsync(RichDocument fragment)
        {
            var package = new DataPackage();

            try
            {
                await MessageHelper.SetTextAsync(package, ToFormattedText(fragment));
                package.SetHtmlFormat(HtmlFormatHelper.CreateHtmlFormat(ToHtml(fragment)));
                package.SetData(FormatId, await StoreAsync(fragment));
            }
            catch
            {
                return false;
            }

            ClipboardEx.TrySetContent(package);
            return true;
        }

        // =====================================================================================
        // Out: text with entities
        // =====================================================================================

        /// <summary>
        /// The document flattened into one message's text: a line per leaf, with the styles a
        /// message can carry. Code blocks become pre entities and quotes blockquote entities;
        /// what a message has no entity for - headings, sub- and superscript, marking - stays
        /// as its text.
        /// </summary>
        public static FormattedText ToFormattedText(RichDocument document)
        {
            var text = new StringBuilder();
            var entities = new MutableVector<TextEntity>();

            var first = true;

            foreach (var leaf in document.Leaves)
            {
                if (leaf.IsEmpty && leaf.Role != RichLeafRole.Text)
                {
                    continue;
                }

                if (!first)
                {
                    text.Append('\n');
                }

                first = false;

                var start = text.Length;
                AppendLeaf(leaf, text, entities);

                if (text.Length > start)
                {
                    if (leaf.Owner is RichParagraphBlock { Kind: RichBlockKind.Preformatted } code)
                    {
                        entities.Add(new TextEntity(start, text.Length - start, string.IsNullOrEmpty(code.Language)
                            ? new TextEntityTypePre()
                            : new TextEntityTypePreCode(code.Language)));
                    }
                    else if (IsInQuote(leaf.Owner))
                    {
                        AddOrExtend(entities, start, text.Length - start, new TextEntityTypeBlockQuote());
                    }
                }
            }

            return new FormattedText(text.ToString(), entities);
        }

        private static bool IsInQuote(RichBlock block)
        {
            for (var current = block; current != null; current = current.Parent)
            {
                if (current is RichBlockQuote or RichTextQuote)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Consecutive quoted lines are one quote, joined across the newline between them.</summary>
        private static void AddOrExtend(MutableVector<TextEntity> entities, int offset, int length, TextEntityType type)
        {
            for (int i = entities.Count - 1; i >= 0; i--)
            {
                var entity = entities[i];
                if (entity.Type.GetType() == type.GetType() && entity.Offset + entity.Length + 1 == offset)
                {
                    entity.Length = offset + length - entity.Offset;
                    return;
                }
            }

            entities.Add(new TextEntity(offset, length, type));
        }

        private static readonly RichMarks[] _entityMarks =
        {
            RichMarks.Bold,
            RichMarks.Italic,
            RichMarks.Underline,
            RichMarks.Strikethrough,
            RichMarks.Spoiler,
            RichMarks.Monospace,
        };

        internal static void AppendLeaf(RichTextLeaf leaf, StringBuilder text, MutableVector<TextEntity> entities)
        {
            var source = leaf.Text;
            var position = 0;

            // Marks open across runs, so a bold word split by a link is one bold entity.
            var open = new int[_entityMarks.Length];
            for (int i = 0; i < open.Length; i++)
            {
                open[i] = -1;
            }

            foreach (var run in leaf.Runs)
            {
                var runStart = text.Length;

                if (run.Object is RichInlineObject inline)
                {
                    var plain = inline.PlainText;
                    text.Append(plain);

                    if (inline.Kind == RichInlineKind.CustomEmoji && plain.Length > 0)
                    {
                        entities.Add(new TextEntity(runStart, plain.Length, new TextEntityTypeCustomEmoji(inline.CustomEmojiId)));
                    }
                }
                else
                {
                    text.Append(source, position, run.Length);

                    if (run.Link is RichLink link && run.Length > 0)
                    {
                        TextEntityType type = link.Kind switch
                        {
                            RichLinkKind.Url => new TextEntityTypeTextUrl(link.Url),
                            RichLinkKind.EmailAddress => new TextEntityTypeTextUrl("mailto:" + link.Url),
                            RichLinkKind.PhoneNumber => new TextEntityTypeTextUrl("tel:" + link.Url),
                            RichLinkKind.MentionName => new TextEntityTypeMentionName(link.UserId),
                            RichLinkKind.DateTime => new TextEntityTypeDateTime(link.UnixTime, link.FormattingType),
                            _ => null
                        };

                        if (type != null)
                        {
                            entities.Add(new TextEntity(runStart, run.Length, type));
                        }
                    }
                }

                var runEnd = text.Length;

                for (int i = 0; i < _entityMarks.Length; i++)
                {
                    var has = (run.Marks & _entityMarks[i]) != 0;

                    if (has && open[i] < 0)
                    {
                        open[i] = runStart;
                    }
                    else if (!has && open[i] >= 0)
                    {
                        AddMark(entities, _entityMarks[i], open[i], runStart);
                        open[i] = -1;
                    }
                }

                position += run.Length;

                // The last run closes whatever is open.
                if (position == source.Length)
                {
                    for (int i = 0; i < _entityMarks.Length; i++)
                    {
                        if (open[i] >= 0)
                        {
                            AddMark(entities, _entityMarks[i], open[i], runEnd);
                            open[i] = -1;
                        }
                    }
                }
            }
        }

        private static void AddMark(MutableVector<TextEntity> entities, RichMarks mark, int start, int end)
        {
            if (end <= start)
            {
                return;
            }

            TextEntityType type = mark switch
            {
                RichMarks.Bold => new TextEntityTypeBold(),
                RichMarks.Italic => new TextEntityTypeItalic(),
                RichMarks.Underline => new TextEntityTypeUnderline(),
                RichMarks.Strikethrough => new TextEntityTypeStrikethrough(),
                RichMarks.Spoiler => new TextEntityTypeSpoiler(),
                _ => new TextEntityTypeCode()
            };

            entities.Add(new TextEntity(start, end - start, type));
        }

        // =====================================================================================
        // HTML
        // =====================================================================================

        /// <summary>
        /// The document as HTML: what other applications paste, and - through
        /// <see cref="RichHtml"/>, which reads exactly this markup - what the chat composer
        /// opens in the rich editor. Media writes nothing; its file cannot travel.
        /// </summary>
        public static string ToHtml(RichDocument document)
        {
            var html = new StringBuilder();
            WriteBlocks(document.Blocks, html);

            return html.ToString();
        }

        /// <summary>HTML pasted in, read by <see cref="RichHtml"/>, the composer's reader, so both paste the same.</summary>
        public static RichDocument FromHtml(string html)
        {
            return RichDocumentConverter.FromPageBlocks(RichHtml.Parse(html));
        }

        private static void WriteBlocks(List<RichBlock> blocks, StringBuilder html)
        {
            foreach (var block in blocks)
            {
                WriteBlock(block, html);
            }
        }

        private static void WriteBlock(RichBlock block, StringBuilder html)
        {
            switch (block)
            {
                case RichParagraphBlock { Kind: RichBlockKind.Heading } x:
                    var level = Math.Clamp(x.HeadingSize, 1, 6).ToString(CultureInfo.InvariantCulture);
                    html.Append("<h").Append(level).Append('>');
                    WriteLeaf(x.Text, html);
                    html.Append("</h").Append(level).Append('>');
                    break;
                case RichParagraphBlock { Kind: RichBlockKind.Preformatted } x:
                    html.Append("<pre");
                    if (!string.IsNullOrEmpty(x.Language))
                    {
                        html.Append(" data-language=\"").Append(Encode(x.Language)).Append('"');
                    }
                    html.Append('>').Append(Encode(x.Text.Text)).Append("</pre>");
                    break;
                case RichParagraphBlock { Kind: RichBlockKind.Footer } x:
                    html.Append("<footer>");
                    WriteLeaf(x.Text, html);
                    html.Append("</footer>");
                    break;
                case RichParagraphBlock x:
                    html.Append("<p>");
                    WriteLeaf(x.Text, html);
                    html.Append("</p>");
                    break;
                case RichBlockQuote { IsCollapsed: true, Children: [RichParagraphBlock text] } x:
                    html.Append("<blockquote class=\"expandable\">");
                    WriteLeaf(text.Text, html);
                    WriteCredit(x.Credit, html);
                    html.Append("</blockquote>");
                    break;
                case RichBlockQuote x:
                    html.Append("<blockquote>");
                    WriteBlocks(x.Children, html);
                    WriteCredit(x.Credit, html);
                    html.Append("</blockquote>");
                    break;
                case RichTextQuote x:
                    html.Append("<blockquote class=\"pull\">");
                    WriteLeaf(x.Text, html);
                    WriteCredit(x.Credit, html);
                    html.Append("</blockquote>");
                    break;
                case RichListBlock x:
                    var ordered = x.Style == RichListStyle.Ordered;
                    html.Append(ordered ? "<ol" : "<ul");
                    if (ordered && x.Start != 1)
                    {
                        html.Append(" start=\"").Append(x.Start.ToString(CultureInfo.InvariantCulture)).Append('"');
                    }
                    html.Append('>');
                    foreach (var item in x.Items)
                    {
                        if (x.Style == RichListStyle.Checklist)
                        {
                            html.Append(item.IsChecked ? "<li data-checkbox data-checked>" : "<li data-checkbox>");
                        }
                        else
                        {
                            html.Append("<li>");
                        }

                        WriteBlocks(item.Children, html);
                        html.Append("</li>");
                    }
                    html.Append(ordered ? "</ol>" : "</ul>");
                    break;
                case RichDetailsBlock x:
                    html.Append(x.IsOpen ? "<details open><summary>" : "<details><summary>");
                    WriteLeaf(x.Header, html);
                    html.Append("</summary>");
                    WriteBlocks(x.Children, html);
                    html.Append("</details>");
                    break;
                case RichTableBlock x:
                    html.Append(x.IsBordered ? "<table border=\"1\"" : "<table");
                    html.Append(x.IsStriped ? " class=\"striped\">" : ">");
                    if (!x.Caption.IsEmpty)
                    {
                        html.Append("<caption>");
                        WriteLeaf(x.Caption, html);
                        html.Append("</caption>");
                    }
                    foreach (var row in x.Rows)
                    {
                        html.Append("<tr>");
                        foreach (var cell in row)
                        {
                            var tag = cell.IsHeader ? "th" : "td";
                            html.Append('<').Append(tag);
                            if (cell.Colspan > 1)
                            {
                                html.Append(" colspan=\"").Append(cell.Colspan.ToString(CultureInfo.InvariantCulture)).Append('"');
                            }
                            if (cell.Rowspan > 1)
                            {
                                html.Append(" rowspan=\"").Append(cell.Rowspan.ToString(CultureInfo.InvariantCulture)).Append('"');
                            }
                            switch (cell.Align)
                            {
                                case PageBlockHorizontalAlignmentCenter:
                                    html.Append(" align=\"center\"");
                                    break;
                                case PageBlockHorizontalAlignmentRight:
                                    html.Append(" align=\"right\"");
                                    break;
                            }
                            switch (cell.Valign)
                            {
                                case PageBlockVerticalAlignmentMiddle:
                                    html.Append(" valign=\"middle\"");
                                    break;
                                case PageBlockVerticalAlignmentBottom:
                                    html.Append(" valign=\"bottom\"");
                                    break;
                            }
                            html.Append('>');
                            WriteLeaf(cell.Text, html);
                            html.Append("</").Append(tag).Append('>');
                        }
                        html.Append("</tr>");
                    }
                    html.Append("</table>");
                    break;
                case RichDividerBlock:
                    html.Append("<hr>");
                    break;
                case RichAnchorBlock x:
                    html.Append("<a data-anchor=\"").Append(Encode(x.Name)).Append("\"></a>");
                    break;
                case RichMathBlock x:
                    html.Append("<div class=\"pm-math-block\" data-latex=\"").Append(Encode(x.Expression)).Append("\">")
                        .Append(Encode(x.Expression)).Append("</div>");
                    break;
                case RichButtonRowBlock x:
                    // The buttons themselves only travel as TDLib JSON, which nothing here
                    // writes; a link button still pastes as its link.
                    html.Append("<p>");
                    foreach (var button in x.Buttons)
                    {
                        var label = Encode(RichDocumentConverter.GetPlainText(button.Text));
                        if (button.Type is InlineKeyboardButtonTypeUrl url)
                        {
                            html.Append("<a href=\"").Append(Encode(url.Url)).Append("\">").Append(label).Append("</a> ");
                        }
                        else
                        {
                            html.Append(label).Append(' ');
                        }
                    }
                    html.Append("</p>");
                    break;
                case RichMediaBlock x:
                    WriteCaption(x.Caption, x.Credit, html);
                    break;
                case RichMediaGroupBlock x:
                    WriteCaption(x.Caption, x.Credit, html);
                    break;
                case RichMapBlock x:
                    WriteCaption(x.Caption, x.Credit, html);
                    break;
            }
        }

        private static string Encode(string text) => WebUtility.HtmlEncode(text ?? string.Empty);

        private static void WriteCredit(RichTextLeaf credit, StringBuilder html)
        {
            if (!credit.IsEmpty)
            {
                html.Append("<cite>");
                WriteLeaf(credit, html);
                html.Append("</cite>");
            }
        }

        private static void WriteCaption(RichTextLeaf caption, RichTextLeaf credit, StringBuilder html)
        {
            if (caption.IsEmpty && credit.IsEmpty)
            {
                return;
            }

            html.Append("<figure><figcaption>");
            WriteLeaf(caption, html);
            WriteCredit(credit, html);
            html.Append("</figcaption></figure>");
        }

        private static void WriteLeaf(RichTextLeaf leaf, StringBuilder html)
        {
            var source = leaf.Text;
            var position = 0;

            foreach (var run in leaf.Runs)
            {
                var open = new StringBuilder();
                var close = new StringBuilder();

                if (run.Link is RichLink link)
                {
                    switch (link.Kind)
                    {
                        case RichLinkKind.MentionName:
                            open.Append("<a class=\"pm-mention\" data-user-id=\"").Append(link.UserId.ToString(CultureInfo.InvariantCulture)).Append("\" href=\"tg://user?id=").Append(link.UserId.ToString(CultureInfo.InvariantCulture)).Append("\">");
                            break;
                        case RichLinkKind.DateTime:
                            open.Append("<a class=\"pm-datetime\" data-unix-time=\"").Append(link.UnixTime.ToString(CultureInfo.InvariantCulture)).Append("\">");
                            break;
                        case RichLinkKind.EmailAddress:
                            open.Append("<a href=\"mailto:").Append(Encode(link.Url)).Append("\">");
                            break;
                        case RichLinkKind.PhoneNumber:
                            open.Append("<a href=\"tel:").Append(Encode(link.Url)).Append("\">");
                            break;
                        default:
                            open.Append("<a href=\"").Append(Encode(link.Url)).Append("\">");
                            break;
                    }

                    close.Insert(0, "</a>");
                }

                Wrap(run.Marks, RichMarks.Bold, "b", open, close);
                Wrap(run.Marks, RichMarks.Italic, "i", open, close);
                Wrap(run.Marks, RichMarks.Underline, "u", open, close);
                Wrap(run.Marks, RichMarks.Strikethrough, "s", open, close);
                Wrap(run.Marks, RichMarks.Spoiler, "spoiler", open, close);
                Wrap(run.Marks, RichMarks.Monospace, "code", open, close);
                Wrap(run.Marks, RichMarks.Marked, "mark", open, close);
                Wrap(run.Marks, RichMarks.Subscript, "sub", open, close);
                Wrap(run.Marks, RichMarks.Superscript, "sup", open, close);

                html.Append(open);

                if (run.Object is RichInlineObject inline)
                {
                    switch (inline.Kind)
                    {
                        case RichInlineKind.CustomEmoji:
                            html.Append("<animated-emoji data-document-id=\"").Append(inline.CustomEmojiId.ToString(CultureInfo.InvariantCulture)).Append("\">")
                                .Append(Encode(inline.Text)).Append("</animated-emoji>");
                            break;
                        case RichInlineKind.Math:
                            html.Append("<span class=\"pm-math-inline\" data-latex=\"").Append(Encode(inline.Text)).Append("\">")
                                .Append(Encode(inline.Text)).Append("</span>");
                            break;
                        default:
                            html.Append(Encode(inline.PlainText));
                            break;
                    }
                }
                else
                {
                    html.Append(Encode(source.Substring(position, run.Length)).Replace("\n", "<br>"));
                }

                html.Append(close);
                position += run.Length;
            }
        }

        private static void Wrap(RichMarks marks, RichMarks mark, string tag, StringBuilder open, StringBuilder close)
        {
            if ((marks & mark) != 0)
            {
                open.Append('<').Append(tag).Append('>');
                close.Insert(0, "</" + tag + ">");
            }
        }
    }
}
