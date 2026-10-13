//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Text;
using Telegram.Common;
using Telegram.Td.Api;

namespace Telegram.Controls.RichEditor
{
    /// <summary>
    /// Between the editor's document and TDLib: a sent message (<see cref="RichMessage"/>) or the
    /// composer's <see cref="FormattedText"/> in, <see cref="InputRichMessage"/> out.
    /// </summary>
    public static class RichDocumentConverter
    {
        // =====================================================================================
        // In
        // =====================================================================================

        public static RichDocument FromRichMessage(RichMessage message)
        {
            var document = FromPageBlocks(message?.Blocks);
            document.IsRtl = message?.IsRtl ?? false;
            return document;
        }

        public static RichDocument FromFormattedText(FormattedText text)
        {
            if (text == null || string.IsNullOrEmpty(text.Text))
            {
                return new RichDocument();
            }

            return FromPageBlocks(PageBlockHelper.ToPageBlocks(text));
        }

        public static RichDocument FromPageBlocks(IReadOnlyList<PageBlock> blocks)
        {
            var result = new List<RichBlock>();
            if (blocks != null)
            {
                foreach (var block in blocks)
                {
                    AddBlock(block, result);
                }
            }

            var document = new RichDocument(result);
            document.Reindex();
            return document;
        }

        private static void AddBlocks(IReadOnlyList<PageBlock> blocks, List<RichBlock> result)
        {
            if (blocks == null)
            {
                return;
            }

            foreach (var block in blocks)
            {
                AddBlock(block, result);
            }
        }

        private static void AddBlock(PageBlock block, List<RichBlock> result)
        {
            switch (block)
            {
                case PageBlockParagraph x:
                    result.Add(Text(RichBlockKind.Paragraph, x.Text));
                    break;
                case PageBlockSectionHeading x:
                    result.Add(Heading(x.Text, Math.Clamp(x.Size, 1, 6)));
                    break;
                case PageBlockTitle x:
                    result.Add(Heading(x.Title, 1));
                    break;
                case PageBlockSubtitle x:
                    result.Add(Heading(x.Subtitle, 2));
                    break;
                case PageBlockHeader x:
                    result.Add(Heading(x.Header, 2));
                    break;
                case PageBlockSubheader x:
                    result.Add(Heading(x.Subheader, 3));
                    break;
                case PageBlockKicker x:
                    result.Add(Text(RichBlockKind.Paragraph, x.Kicker));
                    break;
                case PageBlockThinking x:
                    result.Add(Text(RichBlockKind.Paragraph, x.Text));
                    break;
                case PageBlockPreformatted x:
                    result.Add(new RichParagraphBlock(RichBlockKind.Preformatted, ToLeaf(x.Text, RichLeafRole.Text, true))
                    {
                        Language = x.Language ?? string.Empty
                    });
                    break;
                case PageBlockFooter x:
                    result.Add(Text(RichBlockKind.Footer, x.Footer));
                    break;
                case PageBlockDivider:
                    result.Add(new RichDividerBlock());
                    break;
                case PageBlockAnchor x:
                    result.Add(new RichAnchorBlock(x.Name));
                    break;
                case PageBlockMathematicalExpression x:
                    result.Add(new RichMathBlock(x.Expression));
                    break;
                case PageBlockList x:
                    result.Add(List(x));
                    break;
                case PageBlockBlockQuote x:
                    {
                        var children = new List<RichBlock>();
                        AddBlocks(x.Blocks, children);
                        EnsureText(children);
                        result.Add(new RichBlockQuote(children, ToLeaf(x.Credit, RichLeafRole.Credit)));
                        break;
                    }
                case PageBlockPullQuote x:
                    result.Add(new RichTextQuote(false, ToLeaf(x.Text, RichLeafRole.Text), ToLeaf(x.Credit, RichLeafRole.Credit)));
                    break;
                case PageBlockExpandableBlockQuote x:
                    result.Add(new RichTextQuote(true, ToLeaf(x.Text, RichLeafRole.Text), ToLeaf(x.Credit, RichLeafRole.Credit)));
                    break;
                case PageBlockDetails x:
                    {
                        var children = new List<RichBlock>();
                        AddBlocks(x.Blocks, children);
                        EnsureText(children);
                        result.Add(new RichDetailsBlock(ToLeaf(x.Header, RichLeafRole.Header), children) { IsOpen = x.IsOpen });
                        break;
                    }
                case PageBlockTable x:
                    result.Add(Table(x));
                    break;
                case PageBlockCollage x:
                    result.Add(Group(false, x.Blocks, x.Caption));
                    break;
                case PageBlockSlideshow x:
                    result.Add(Group(true, x.Blocks, x.Caption));
                    break;
                case PageBlockMap x:
                    result.Add(new RichMapBlock(x.Location, x.Zoom, x.Width, x.Height, ToLeaf(x.Caption?.Text, RichLeafRole.Caption), ToLeaf(x.Caption?.Credit, RichLeafRole.Credit)));
                    break;
                case PageBlockButtonRow x:
                    result.Add(new RichButtonRowBlock(new List<InlineButton>(x.Buttons)) { Align = x.Align });
                    break;
                case PageBlockCover x:
                    AddBlock(x.Cover, result);
                    break;
                default:
                    if (Media(block) is RichMediaBlock media)
                    {
                        result.Add(media);
                    }

                    // Everything else (embeds, related articles, chat links, author lines) has no
                    // input counterpart, so it cannot be sent back and is dropped, as
                    // PageBlockHelper.ToInputBlocks drops it.
                    break;
            }
        }

        private static RichParagraphBlock Text(RichBlockKind kind, RichText text)
        {
            return new RichParagraphBlock(kind, ToLeaf(text, RichLeafRole.Text));
        }

        private static RichParagraphBlock Heading(RichText text, int size)
        {
            return new RichParagraphBlock(RichBlockKind.Heading, ToLeaf(text, RichLeafRole.Text)) { HeadingSize = size };
        }

        /// <summary>A container needs a first text block for its caret to have somewhere to go.</summary>
        private static void EnsureText(List<RichBlock> children)
        {
            if (children.Count == 0 || children[0] is not RichParagraphBlock)
            {
                children.Insert(0, new RichParagraphBlock(RichBlockKind.Paragraph));
            }
        }

        private static RichListBlock List(PageBlockList list)
        {
            var style = RichListStyle.Bullet;
            var orderedType = "1";
            var start = 1;

            if (list.Items.Count > 0)
            {
                var first = list.Items[0];
                if (first.HasCheckbox)
                {
                    style = RichListStyle.Checklist;
                }
                else if (!string.IsNullOrEmpty(first.Type))
                {
                    style = RichListStyle.Ordered;
                    orderedType = first.Type;
                    start = first.Value;
                }
            }

            var items = new List<RichListItem>(list.Items.Count);
            foreach (var item in list.Items)
            {
                var children = new List<RichBlock>();
                AddBlocks(item.Blocks, children);
                EnsureText(children);

                items.Add(new RichListItem(children) { IsChecked = item.IsChecked });
            }

            return new RichListBlock(style, items)
            {
                OrderedType = orderedType,
                Start = start
            };
        }

        private static RichTableBlock Table(PageBlockTable table)
        {
            var rows = new List<List<RichTableCell>>(table.Cells.Count);
            foreach (var row in table.Cells)
            {
                var cells = new List<RichTableCell>(row.Count);
                foreach (var cell in row)
                {
                    cells.Add(new RichTableCell(ToLeaf(cell.Text, RichLeafRole.Cell))
                    {
                        IsHeader = cell.IsHeader,
                        Colspan = Math.Max(1, cell.Colspan),
                        Rowspan = Math.Max(1, cell.Rowspan),
                        Align = cell.Align ?? new PageBlockHorizontalAlignmentLeft(),
                        Valign = cell.Valign ?? new PageBlockVerticalAlignmentTop()
                    });
                }

                rows.Add(cells);
            }

            return new RichTableBlock(rows, ToLeaf(table.Caption, RichLeafRole.Caption))
            {
                IsBordered = table.IsBordered,
                IsStriped = table.IsStriped,
                IsCompact = table.IsCompact
            };
        }

        private static RichMediaGroupBlock Group(bool isSlideshow, IReadOnlyList<PageBlock> blocks, PageBlockCaption caption)
        {
            var items = new List<RichBlock>();
            if (blocks != null)
            {
                foreach (var block in blocks)
                {
                    if (Media(block) is RichMediaBlock media)
                    {
                        items.Add(media);
                    }
                }
            }

            return new RichMediaGroupBlock(isSlideshow, items, ToLeaf(caption?.Text, RichLeafRole.Caption), ToLeaf(caption?.Credit, RichLeafRole.Credit));
        }

        private static RichMediaBlock Media(PageBlock block)
        {
            RichMediaSource source;
            PageBlockCaption caption;
            var spoiler = false;

            switch (block)
            {
                case PageBlockPhoto x when x.Photo != null:
                    var size = x.Photo.Sizes.Count > 0 ? x.Photo.Sizes[x.Photo.Sizes.Count - 1] : null;
                    source = RichMediaSource.FromRemote(RichMediaKind.Photo, block, size?.Width ?? 0, size?.Height ?? 0, 0);
                    caption = x.Caption;
                    spoiler = x.HasSpoiler;
                    break;
                case PageBlockVideo x when x.Video != null:
                    source = RichMediaSource.FromRemote(RichMediaKind.Video, block, x.Video.Width, x.Video.Height, x.Video.Duration);
                    caption = x.Caption;
                    spoiler = x.HasSpoiler;
                    break;
                case PageBlockAnimation x when x.Animation != null:
                    source = RichMediaSource.FromRemote(RichMediaKind.Animation, block, x.Animation.Width, x.Animation.Height, x.Animation.Duration);
                    caption = x.Caption;
                    spoiler = x.HasSpoiler;
                    break;
                case PageBlockAudio x when x.Audio != null:
                    source = RichMediaSource.FromRemote(RichMediaKind.Audio, block, 0, 0, x.Audio.Duration);
                    caption = x.Caption;
                    break;
                case PageBlockVoiceNote x when x.VoiceNote != null:
                    source = RichMediaSource.FromRemote(RichMediaKind.VoiceNote, block, 0, 0, x.VoiceNote.Duration);
                    caption = x.Caption;
                    break;
                case PageBlockDocument x when x.Document != null:
                    source = RichMediaSource.FromRemote(RichMediaKind.Document, block, 0, 0, 0);
                    caption = x.Caption;
                    break;
                default:
                    return null;
            }

            return new RichMediaBlock(source, ToLeaf(caption?.Text, RichLeafRole.Caption), ToLeaf(caption?.Credit, RichLeafRole.Credit))
            {
                HasSpoiler = spoiler
            };
        }

        // -------------------------------------------------------------------------------------
        // RichText -> leaf
        // -------------------------------------------------------------------------------------

        public static RichTextLeaf ToLeaf(RichText text, RichLeafRole role, bool isPlain = false)
        {
            var leaf = new RichTextLeaf(role, isPlain);

            if (isPlain)
            {
                leaf.AppendRun(GetPlainText(text), RichMarks.None, null);
            }
            else
            {
                AppendRichText(text, leaf, RichMarks.None, null);
            }

            return leaf;
        }

        private static void AppendRichText(RichText text, RichTextLeaf leaf, RichMarks marks, RichLink link)
        {
            switch (text)
            {
                case null:
                    return;
                case RichTextPlain x:
                    leaf.AppendRun(x.Text, marks, link);
                    return;
                case RichTexts x:
                    foreach (var child in x.Texts)
                    {
                        AppendRichText(child, leaf, marks, link);
                    }
                    return;

                case RichTextBold x: AppendRichText(x.Text, leaf, marks | RichMarks.Bold, link); return;
                case RichTextItalic x: AppendRichText(x.Text, leaf, marks | RichMarks.Italic, link); return;
                case RichTextUnderline x: AppendRichText(x.Text, leaf, marks | RichMarks.Underline, link); return;
                case RichTextStrikethrough x: AppendRichText(x.Text, leaf, marks | RichMarks.Strikethrough, link); return;
                case RichTextSpoiler x: AppendRichText(x.Text, leaf, marks | RichMarks.Spoiler, link); return;
                case RichTextFixed x: AppendRichText(x.Text, leaf, marks | RichMarks.Monospace, link); return;
                case RichTextMarked x: AppendRichText(x.Text, leaf, marks | RichMarks.Marked, link); return;
                case RichTextSubscript x: AppendRichText(x.Text, leaf, (marks & ~RichMarks.Superscript) | RichMarks.Subscript, link); return;
                case RichTextSuperscript x: AppendRichText(x.Text, leaf, (marks & ~RichMarks.Subscript) | RichMarks.Superscript, link); return;

                // Links do not nest; the innermost wins, which is what a reader sees.
                case RichTextUrl x: AppendRichText(x.Text, leaf, marks, RichLink.FromUrl(x.Url)); return;
                case RichTextEmailAddress x: AppendRichText(x.Text, leaf, marks, RichLink.FromEmailAddress(x.EmailAddress)); return;
                case RichTextPhoneNumber x: AppendRichText(x.Text, leaf, marks, RichLink.FromPhoneNumber(x.PhoneNumber)); return;
                case RichTextMentionName x: AppendRichText(x.Text, leaf, marks, RichLink.FromMentionName(x.UserId)); return;
                case RichTextDateTime x: AppendRichText(x.Text, leaf, marks, RichLink.FromDateTime(x.UnixTime, x.FormattingType)); return;
                case RichTextAnchorLink x: AppendRichText(x.Text, leaf, marks, string.IsNullOrEmpty(x.Url) ? link : RichLink.FromUrl(x.Url)); return;
                case RichTextReferenceLink x: AppendRichText(x.Text, leaf, marks, string.IsNullOrEmpty(x.Url) ? link : RichLink.FromUrl(x.Url)); return;

                // Detected entities are detected again on send (detect_automatic_blocks), so
                // they come in as the text they cover.
                case RichTextMention x: AppendRichText(x.Text, leaf, marks, link); return;
                case RichTextHashtag x: AppendRichText(x.Text, leaf, marks, link); return;
                case RichTextCashtag x: AppendRichText(x.Text, leaf, marks, link); return;
                case RichTextBotCommand x: AppendRichText(x.Text, leaf, marks, link); return;
                case RichTextBankCardNumber x: AppendRichText(x.Text, leaf, marks, link); return;
                case RichTextTonAddress x: AppendRichText(x.Text, leaf, marks, link); return;
                case RichTextReference x: AppendRichText(x.Text, leaf, marks, link); return;
                case RichTextDiff x: AppendRichText(x.Text, leaf, marks, link); return;

                case RichTextCustomEmoji x: leaf.AppendObject(RichInlineObject.FromCustomEmoji(x.CustomEmojiId, x.AlternativeText), marks); return;
                case RichTextMathematicalExpression x: leaf.AppendObject(RichInlineObject.FromMath(x.Expression), marks); return;
                case RichTextButton x: leaf.AppendObject(RichInlineObject.FromButton(x.Button), marks); return;
                case RichTextIcon x: leaf.AppendObject(RichInlineObject.FromIcon(x), marks); return;
                case RichTextAnchor x: leaf.AppendObject(RichInlineObject.FromAnchor(x.Name), marks); return;
            }
        }

        public static string GetPlainText(RichText text)
        {
            var builder = new StringBuilder();
            AppendPlainText(text, builder);
            return builder.ToString();
        }

        private static void AppendPlainText(RichText text, StringBuilder builder)
        {
            switch (text)
            {
                case null:
                    return;
                case RichTextPlain x:
                    builder.Append(x.Text);
                    return;
                case RichTexts x:
                    foreach (var child in x.Texts)
                    {
                        AppendPlainText(child, builder);
                    }
                    return;
                case RichTextCustomEmoji x:
                    builder.Append(x.AlternativeText);
                    return;
                case RichTextMathematicalExpression x:
                    builder.Append(x.Expression);
                    return;
                case RichTextButton x:
                    AppendPlainText(x.Button?.Text, builder);
                    return;
                case RichTextIcon:
                case RichTextAnchor:
                    return;
                default:
                    AppendPlainText(GetChild(text), builder);
                    return;
            }
        }

        private static RichText GetChild(RichText text) => text switch
        {
            RichTextBold x => x.Text,
            RichTextItalic x => x.Text,
            RichTextUnderline x => x.Text,
            RichTextStrikethrough x => x.Text,
            RichTextSpoiler x => x.Text,
            RichTextFixed x => x.Text,
            RichTextMarked x => x.Text,
            RichTextSubscript x => x.Text,
            RichTextSuperscript x => x.Text,
            RichTextUrl x => x.Text,
            RichTextEmailAddress x => x.Text,
            RichTextPhoneNumber x => x.Text,
            RichTextMentionName x => x.Text,
            RichTextDateTime x => x.Text,
            RichTextAnchorLink x => x.Text,
            RichTextReferenceLink x => x.Text,
            RichTextMention x => x.Text,
            RichTextHashtag x => x.Text,
            RichTextCashtag x => x.Text,
            RichTextBotCommand x => x.Text,
            RichTextBankCardNumber x => x.Text,
            RichTextTonAddress x => x.Text,
            RichTextReference x => x.Text,
            RichTextDiff x => x.Text,
            _ => null
        };

        // =====================================================================================
        // Out
        // =====================================================================================

        /// <summary>
        /// The document to send, or null when nothing in it is. The server refuses a message with
        /// an empty block anywhere in it, with nothing to tell the user which, so whatever has no
        /// content goes - by the rules TDesktop applies on send. Null when nothing is left.
        /// </summary>
        public static InputRichMessage ToInputRichMessage(RichDocument document)
        {
            var blocks = ToInputBlocks(document.Blocks);
            if (blocks.Count == 0)
            {
                return null;
            }

            return new InputRichMessage(new RichMessageSourceBlocks(blocks), document.IsRtl, true);
        }

        private static bool HasText(RichTextLeaf leaf)
        {
            return !string.IsNullOrWhiteSpace(leaf.Text);
        }

        private static bool IsEmptyParagraph(RichBlock block)
        {
            return block is RichParagraphBlock { Kind: RichBlockKind.Paragraph } text && !HasText(text.Text);
        }

        /// <summary>
        /// The blocks with content. Empty paragraphs are blank lines, which are kept between two
        /// blocks and dropped at either end of the list - the line a caret was left on is not
        /// content - including the ends left by a block that was dropped.
        /// </summary>
        public static MutableVector<InputPageBlock> ToInputBlocks(IList<RichBlock> blocks)
        {
            var result = new MutableVector<InputPageBlock>(blocks.Count);

            // The blank lines since the last block kept, sent only once another one follows.
            var blank = -1;

            for (int i = 0; i < blocks.Count; i++)
            {
                if (IsEmptyParagraph(blocks[i]))
                {
                    if (blank < 0 && result.Count > 0)
                    {
                        blank = i;
                    }

                    continue;
                }

                if (ToInputBlock(blocks[i]) is not InputPageBlock input)
                {
                    continue;
                }

                if (blank >= 0)
                {
                    for (int j = blank; j < i; j++)
                    {
                        if (IsEmptyParagraph(blocks[j]))
                        {
                            result.Add(new InputPageBlockParagraph(new RichTextPlain(string.Empty)));
                        }
                    }

                    blank = -1;
                }

                result.Add(input);
            }

            return result;
        }

        private static InputPageBlock ToInputBlock(RichBlock block)
        {
            switch (block)
            {
                case RichParagraphBlock x when !HasText(x.Text):
                    return null;
                case RichParagraphBlock x:
                    return x.Kind switch
                    {
                        RichBlockKind.Heading => new InputPageBlockSectionHeading(ToRichText(x.Text), x.HeadingSize),
                        RichBlockKind.Preformatted => new InputPageBlockPreformatted(new RichTextPlain(x.Text.Text), x.Language ?? string.Empty),
                        RichBlockKind.Footer => new InputPageBlockFooter(ToRichText(x.Text)),
                        _ => new InputPageBlockParagraph(ToRichText(x.Text))
                    };
                // A credit is not content on its own, and a details header is not either.
                case RichBlockQuote x:
                    {
                        var children = ToInputBlocks(x.Children);
                        return children.Count == 0 ? null : new InputPageBlockBlockQuote(children, ToRichTextOrNull(x.Credit));
                    }
                case RichTextQuote x when !HasText(x.Text):
                    return null;
                case RichTextQuote x when x.IsExpandable:
                    return new InputPageBlockExpandableBlockQuote(ToRichText(x.Text), ToRichTextOrNull(x.Credit));
                case RichTextQuote x:
                    return new InputPageBlockPullQuote(ToRichText(x.Text), ToRichTextOrNull(x.Credit));
                case RichListBlock x:
                    {
                        var items = ToInputListItems(x);
                        return items.Count == 0 ? null : new InputPageBlockList(items);
                    }
                case RichDetailsBlock x:
                    {
                        var children = ToInputBlocks(x.Children);
                        return children.Count == 0 ? null : new InputPageBlockDetails(ToRichText(x.Header), children, x.IsOpen);
                    }
                case RichTableBlock x:
                    return HasCellText(x) ? ToInputTable(x) : null;
                case RichMathBlock x when string.IsNullOrWhiteSpace(x.Expression):
                    return null;
                case RichButtonRowBlock x when x.Buttons.Count == 0:
                    return null;
                case RichDividerBlock:
                    return new InputPageBlockDivider();
                case RichAnchorBlock x:
                    return new InputPageBlockAnchor(x.Name);
                case RichMathBlock x:
                    return new InputPageBlockMathematicalExpression(x.Expression);
                case RichMediaBlock x:
                    return ToInputMedia(x, ToCaption(x.Caption, x.Credit));
                case RichMediaGroupBlock x:
                    {
                        var items = new MutableVector<InputPageBlock>(x.Items.Count);
                        foreach (var item in x.Items)
                        {
                            if (item is RichMediaBlock media && ToInputMedia(media, null) is InputPageBlock input)
                            {
                                items.Add(input);
                            }
                        }

                        if (items.Count == 0)
                        {
                            return null;
                        }

                        var caption = ToCaption(x.Caption, x.Credit);
                        return x.IsSlideshow
                            ? new InputPageBlockSlideshow(items, caption)
                            : new InputPageBlockCollage(items, caption);
                    }
                case RichMapBlock x:
                    return new InputPageBlockMap(x.Location, x.Zoom, x.Width, x.Height, ToCaption(x.Caption, x.Credit));
                case RichButtonRowBlock x:
                    return new InputPageBlockButtonRow(new Vector<InlineButton>(x.Buttons), x.Align);
                default:
                    return null;
            }
        }

        private static MutableVector<InputPageBlockListItem> ToInputListItems(RichListBlock list)
        {
            var items = new MutableVector<InputPageBlockListItem>(list.Items.Count);
            var ordered = list.Style == RichListStyle.Ordered;
            var checklist = list.Style == RichListStyle.Checklist;

            foreach (var item in list.Items)
            {
                // An empty item goes and the ones after it are numbered on, as they are shown
                // once it has.
                var children = ToInputBlocks(item.Children);
                if (children.Count == 0)
                {
                    continue;
                }

                items.Add(new InputPageBlockListItem(
                    children,
                    checklist,
                    checklist && item.IsChecked,
                    ordered ? list.Start + items.Count : 0,
                    ordered ? list.OrderedType ?? "1" : string.Empty));
            }

            return items;
        }

        // Empty cells are kept, as a table is a grid: only a table with nothing in any goes.
        private static bool HasCellText(RichTableBlock table)
        {
            foreach (var row in table.Rows)
            {
                foreach (var cell in row)
                {
                    if (HasText(cell.Text))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static InputPageBlockTable ToInputTable(RichTableBlock table)
        {
            var rows = new MutableVector<Vector<PageBlockTableCell>>(table.Rows.Count);
            foreach (var row in table.Rows)
            {
                var cells = new MutableVector<PageBlockTableCell>(row.Count);
                foreach (var cell in row)
                {
                    cells.Add(new PageBlockTableCell(ToRichText(cell.Text), cell.IsHeader, cell.Colspan, cell.Rowspan, cell.Align, cell.Valign));
                }

                rows.Add(cells);
            }

            return new InputPageBlockTable(ToRichText(table.Caption), rows, table.IsBordered, table.IsStriped, table.IsCompact);
        }

        private static PageBlockCaption ToCaption(RichTextLeaf caption, RichTextLeaf credit)
        {
            if (!HasText(caption) && !HasText(credit))
            {
                return null;
            }

            return new PageBlockCaption(ToRichText(caption), ToRichText(credit));
        }

        private static InputPageBlock ToInputMedia(RichMediaBlock media, PageBlockCaption caption)
        {
            var source = media.Source;
            if (source == null)
            {
                return null;
            }

            if (source.Input != null)
            {
                return source.Input switch
                {
                    InputPhoto x => new InputPageBlockPhoto(x, caption, media.HasSpoiler),
                    InputVideo x => new InputPageBlockVideo(x, caption, media.HasSpoiler),
                    InputAnimation x => new InputPageBlockAnimation(x, caption, media.HasSpoiler),
                    InputAudio x => new InputPageBlockAudio(x, caption),
                    InputVoiceNote x => new InputPageBlockVoiceNote(x, caption),
                    InputDocument x => new InputPageBlockDocument(x, caption),
                    _ => null
                };
            }

            // A block read from a message goes back by file id. PageBlockHelper already knows
            // how to turn each display block into its input twin, so the display block is
            // rebuilt with what the editor changed and handed to it.
            PageBlock display = source.Remote switch
            {
                PageBlockPhoto x => new PageBlockPhoto(x.Photo, caption, x.Url, media.HasSpoiler),
                PageBlockVideo x => new PageBlockVideo(x.Video, caption, x.NeedAutoplay, x.IsLooped, media.HasSpoiler),
                PageBlockAnimation x => new PageBlockAnimation(x.Animation, caption, x.NeedAutoplay, media.HasSpoiler),
                PageBlockAudio x => new PageBlockAudio(x.Audio, caption),
                PageBlockVoiceNote x => new PageBlockVoiceNote(x.VoiceNote, caption),
                PageBlockDocument x => new PageBlockDocument(x.Document, caption),
                _ => null
            };

            if (display == null)
            {
                return null;
            }

            var converted = PageBlockHelper.ToInputBlocks(new MutableVector<PageBlock> { display });
            return converted.Count > 0 ? converted[0] : null;
        }

        // -------------------------------------------------------------------------------------
        // leaf -> RichText
        // -------------------------------------------------------------------------------------

        // Outermost first. A link wraps its styles rather than the other way round, so a link
        // with a bold word in it stays one link.
        private static readonly RichMarks[] _markOrder =
        {
            RichMarks.Bold,
            RichMarks.Italic,
            RichMarks.Underline,
            RichMarks.Strikethrough,
            RichMarks.Spoiler,
            RichMarks.Marked,
            RichMarks.Subscript,
            RichMarks.Superscript,
            RichMarks.Monospace,
        };

        private static RichText ToRichTextOrNull(RichTextLeaf leaf)
        {
            return HasText(leaf) ? ToRichText(leaf) : null;
        }

        public static RichText ToRichText(RichTextLeaf leaf)
        {
            var runs = leaf.Runs;
            if (runs.Count == 0)
            {
                return new RichTextPlain(string.Empty);
            }

            var starts = new int[runs.Count];
            var position = 0;

            for (int i = 0; i < runs.Count; i++)
            {
                starts[i] = position;
                position += runs[i].Length;
            }

            return Build(leaf.Text, runs, starts, 0, runs.Count, -1);
        }

        /// <summary>
        /// Runs [first, last) at grouping level <paramref name="level"/>: -1 groups by link, then
        /// each mark in <see cref="_markOrder"/>. Neighbouring runs that share the level's
        /// attribute go under one wrapper, so a word made bold in the middle of an italic
        /// sentence is one italic with a bold inside it, not three italics.
        /// </summary>
        private static RichText Build(string text, IReadOnlyList<RichTextRun> runs, int[] starts, int first, int last, int level)
        {
            if (level >= _markOrder.Length)
            {
                if (last - first == 1)
                {
                    return Leaf(text, runs[first], starts[first]);
                }

                var leaves = new MutableVector<RichText>(last - first);
                for (int i = first; i < last; i++)
                {
                    leaves.Add(Leaf(text, runs[i], starts[i]));
                }

                return new RichTexts(leaves);
            }

            var parts = new MutableVector<RichText>();
            var groupStart = first;

            while (groupStart < last)
            {
                var groupEnd = groupStart + 1;
                while (groupEnd < last && SameAtLevel(runs[groupStart], runs[groupEnd], level))
                {
                    groupEnd++;
                }

                var child = Build(text, runs, starts, groupStart, groupEnd, level + 1);
                parts.Add(Wrap(runs[groupStart], child, level));

                groupStart = groupEnd;
            }

            return parts.Count == 1 ? parts[0] : new RichTexts(parts);
        }

        private static bool SameAtLevel(in RichTextRun x, in RichTextRun y, int level)
        {
            if (level < 0)
            {
                return RichLink.AreEqual(x.Link, y.Link);
            }

            var mark = _markOrder[level];
            return (x.Marks & mark) == (y.Marks & mark);
        }

        private static RichText Wrap(in RichTextRun run, RichText child, int level)
        {
            if (level < 0)
            {
                var link = run.Link;
                return link?.Kind switch
                {
                    RichLinkKind.Url => new RichTextUrl(child, link.Url, false),
                    RichLinkKind.EmailAddress => new RichTextEmailAddress(child, link.Url),
                    RichLinkKind.PhoneNumber => new RichTextPhoneNumber(child, link.Url),
                    RichLinkKind.MentionName => new RichTextMentionName(child, link.UserId),
                    RichLinkKind.DateTime => new RichTextDateTime(child, link.UnixTime, link.FormattingType),
                    _ => child
                };
            }

            var mark = _markOrder[level];
            if ((run.Marks & mark) == 0)
            {
                return child;
            }

            return mark switch
            {
                RichMarks.Bold => new RichTextBold(child),
                RichMarks.Italic => new RichTextItalic(child),
                RichMarks.Underline => new RichTextUnderline(child),
                RichMarks.Strikethrough => new RichTextStrikethrough(child),
                RichMarks.Spoiler => new RichTextSpoiler(child),
                RichMarks.Marked => new RichTextMarked(child),
                RichMarks.Subscript => new RichTextSubscript(child),
                RichMarks.Superscript => new RichTextSuperscript(child),
                RichMarks.Monospace => new RichTextFixed(child),
                _ => child
            };
        }

        private static RichText Leaf(string text, in RichTextRun run, int start)
        {
            var inline = run.Object;
            if (inline == null)
            {
                return new RichTextPlain(text.Substring(start, run.Length));
            }

            return inline.Kind switch
            {
                RichInlineKind.CustomEmoji => new RichTextCustomEmoji(inline.CustomEmojiId, inline.Text),
                RichInlineKind.Math => new RichTextMathematicalExpression(inline.Text),
                RichInlineKind.Button => new RichTextButton(inline.Button),
                RichInlineKind.Anchor => new RichTextAnchor(inline.Text),
                _ => inline.Original ?? new RichTextPlain(string.Empty)
            };
        }
    }
}
