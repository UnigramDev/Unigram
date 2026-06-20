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

namespace Telegram.Common
{
    /// <summary>
    /// Media-block categories used by <see cref="PageBlockHelper.FindFirstMedia"/>.
    /// Combinable bit flags; pass <see cref="Media"/>, <see cref="Visual"/>, or
    /// <see cref="Any"/> when you don't care which specific kind matches first.
    /// </summary>
    [Flags]
    public enum PageBlockMediaKind : uint
    {
        None = 0,
        Photo = 1u,
        Video = 2u,
        Animation = 4u,
        Audio = 8u,
        VoiceNote = 0x10u,
        Map = 0x20u,
        Embedded = 0x40u,        // pageBlockEmbedded (iframe-style)
        EmbeddedPost = 0x80u,    // pageBlockEmbeddedPost (Telegram post embed)

        // Conventional groupings
        Media = Photo | Video,
        Visual = Photo | Video | Animation,
        Audible = Audio | VoiceNote,
        Any = Photo | Video | Animation | Audio | VoiceNote | Map | Embedded | EmbeddedPost
    }

    /// <summary>
    /// Origin of a link extracted by <see cref="PageBlockHelper.GetLinks"/>.
    /// </summary>
    public enum PageBlockLinkKind
    {
        Url,             // richTextUrl
        Email,           // richTextEmailAddress / richTextAutoEmailAddress
        Phone,           // richTextPhoneNumber / richTextAutoPhoneNumber
        Reference,       // richTextReference (in-page anchor)
        ReferenceLink,   // richTextReferenceLink (in-page anchor)
        AnchorLink,      // richTextAnchorLink (in-page anchor)
        Mention,         // richTextMention (@username)
        MentionName,     // richTextMentionName (by user_id)
        ChatLink,        // pageBlockChatLink (chat username)
        RelatedArticle,  // pageBlockRelatedArticles → article.url
        Embedded,        // pageBlockEmbedded.url
        EmbeddedPost,    // pageBlockEmbeddedPost.url
        PhotoUrl         // pageBlockPhoto.url (clickable photo)
    }

    public class PageBlockLink
    {
        public string Url { get; }
        public string Text { get; }
        public PageBlockLinkKind Kind { get; }

        public PageBlockLink(string url, string text, PageBlockLinkKind kind)
        {
            Url = url ?? string.Empty;
            Text = text ?? string.Empty;
            Kind = kind;
        }

        public override string ToString() => $"[{Kind}] {Url}" + (Text.Length > 0 && Text != Url ? $" ({Text})" : "");
    }

    public static class PageBlockHelper
    {
        // Placeholder strings inserted in place of non-text blocks when projecting
        // a page to a flat RichText. Hardcoded; swap for localized strings later.
        private const string PlaceholderPhoto = "\U0001F5BC";
        private const string PlaceholderVideo = "\U0001F4F9";
        private const string PlaceholderAnimation = "\U0001F47E";
        private const string PlaceholderAudio = "\U0001F3B5";
        private const string PlaceholderVoiceNote = "\U0001F3A4";
        private const string PlaceholderMap = "\U0001F4CD";
        private const string PlaceholderEmbedded = "[embed]";
        private const string PlaceholderEmbeddedPost = "[post]";
        private const string PlaceholderChatLink = "[chat]";
        private const string PlaceholderDivider = "---";

        // =====================================================================
        // FindFirstMedia
        // =====================================================================

        /// <summary>
        /// Walks the block list (descending into container blocks: cover, list,
        /// details, collage, slideshow, blockquote, embedded post) and returns the
        /// first block whose kind is included in <paramref name="kind"/>, or null
        /// if none matches.
        /// </summary>
        public static PageBlock FindFirstMedia(IList<PageBlock> blocks, PageBlockMediaKind kind)
        {
            if (blocks == null || kind == PageBlockMediaKind.None)
            {
                return null;
            }

            foreach (var block in blocks)
            {
                var match = FindFirstMediaCore(block, kind);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static PageBlock FindFirstMediaCore(PageBlock block, PageBlockMediaKind kind)
        {
            switch (block)
            {
                case null:
                    return null;

                // Direct matches
                case PageBlockPhoto when (kind & PageBlockMediaKind.Photo) != 0:
                case PageBlockVideo when (kind & PageBlockMediaKind.Video) != 0:
                case PageBlockAnimation when (kind & PageBlockMediaKind.Animation) != 0:
                case PageBlockAudio when (kind & PageBlockMediaKind.Audio) != 0:
                case PageBlockVoiceNote when (kind & PageBlockMediaKind.VoiceNote) != 0:
                case PageBlockMap when (kind & PageBlockMediaKind.Map) != 0:
                case PageBlockEmbedded when (kind & PageBlockMediaKind.Embedded) != 0:
                case PageBlockEmbeddedPost when (kind & PageBlockMediaKind.EmbeddedPost) != 0:
                    return block;

                // Containers — descend in document order, but only when the block
                // itself didn't match. EmbeddedPost is special: it can be either a
                // match (the post itself is what you want) or a container to descend
                // into; the case above handles the match, this case handles descent.
                case PageBlockCover cover:
                    return FindFirstMediaCore(cover.Cover, kind);
                case PageBlockList list:
                    if (list.Items != null)
                    {
                        foreach (var item in list.Items)
                        {
                            var m = FindFirstMediaInList(item.Blocks, kind);
                            if (m != null) return m;
                        }
                    }
                    return null;
                case PageBlockDetails details:
                    return FindFirstMediaInList(details.Blocks, kind);
                case PageBlockCollage collage:
                    return FindFirstMediaInList(collage.Blocks, kind);
                case PageBlockSlideshow slideshow:
                    return FindFirstMediaInList(slideshow.Blocks, kind);
                case PageBlockBlockQuote blockquote:
                    return FindFirstMediaInList(blockquote.Blocks, kind);
                case PageBlockEmbeddedPost ep:
                    return FindFirstMediaInList(ep.Blocks, kind);

                default:
                    return null;
            }
        }

        private static PageBlock FindFirstMediaInList(IList<PageBlock> blocks, PageBlockMediaKind kind)
        {
            if (blocks == null) return null;
            foreach (var b in blocks)
            {
                var m = FindFirstMediaCore(b, kind);
                if (m != null) return m;
            }
            return null;
        }

        // =====================================================================
        // GetLinks
        // =====================================================================

        /// <summary>
        /// Collects every link reachable from the block list, in document order.
        /// Walks both the block tree and any nested rich-text content (paragraphs,
        /// captions, table cells, etc.). The returned list is owned by the caller.
        /// </summary>
        public static IList<PageBlockLink> GetLinks(IList<PageBlock> blocks)
        {
            var result = new List<PageBlockLink>();
            if (blocks == null)
            {
                return result;
            }

            foreach (var block in blocks)
            {
                CollectLinksFromBlock(block, result);
            }

            return result;
        }

        private static void CollectLinksFromBlocks(IList<PageBlock> blocks, List<PageBlockLink> result)
        {
            if (blocks == null) return;
            foreach (var b in blocks)
            {
                CollectLinksFromBlock(b, result);
            }
        }

        private static void CollectLinksFromBlock(PageBlock block, List<PageBlockLink> result)
        {
            switch (block)
            {
                case null:
                    return;

                // Text-bearing blocks
                case PageBlockTitle t:
                    CollectLinksFromRichText(t.Title, result);
                    return;
                case PageBlockSubtitle st:
                    CollectLinksFromRichText(st.Subtitle, result);
                    return;
                case PageBlockKicker k:
                    CollectLinksFromRichText(k.Kicker, result);
                    return;
                case PageBlockAuthorDate ad:
                    CollectLinksFromRichText(ad.Author, result);
                    return;
                case PageBlockHeader h:
                    CollectLinksFromRichText(h.Header, result);
                    return;
                case PageBlockSubheader sh:
                    CollectLinksFromRichText(sh.Subheader, result);
                    return;
                case PageBlockSectionHeading sh2:
                    CollectLinksFromRichText(sh2.Text, result);
                    return;
                case PageBlockThinking th:
                    CollectLinksFromRichText(th.Text, result);
                    return;
                case PageBlockFooter f:
                    CollectLinksFromRichText(f.Footer, result);
                    return;
                case PageBlockParagraph p:
                    CollectLinksFromRichText(p.Text, result);
                    return;
                case PageBlockPreformatted pre:
                    CollectLinksFromRichText(pre.Text, result);
                    return;
                case PageBlockPullQuote pq:
                    CollectLinksFromRichText(pq.Text, result);
                    CollectLinksFromRichText(pq.Credit, result);
                    return;

                // Container blocks
                case PageBlockBlockQuote bq:
                    CollectLinksFromBlocks(bq.Blocks, result);
                    CollectLinksFromRichText(bq.Credit, result);
                    return;
                case PageBlockList list:
                    if (list.Items != null)
                    {
                        foreach (var item in list.Items)
                        {
                            CollectLinksFromBlocks(item.Blocks, result);
                        }
                    }
                    return;
                case PageBlockDetails details:
                    CollectLinksFromRichText(details.Header, result);
                    CollectLinksFromBlocks(details.Blocks, result);
                    return;
                case PageBlockCover cv:
                    CollectLinksFromBlock(cv.Cover, result);
                    return;
                case PageBlockCollage c:
                    CollectLinksFromBlocks(c.Blocks, result);
                    CollectLinksFromCaption(c.Caption, result);
                    return;
                case PageBlockSlideshow s:
                    CollectLinksFromBlocks(s.Blocks, result);
                    CollectLinksFromCaption(s.Caption, result);
                    return;

                // Table cells contain RichText, not blocks
                case PageBlockTable table:
                    CollectLinksFromRichText(table.Caption, result);
                    if (table.Cells != null)
                    {
                        foreach (var row in table.Cells)
                        {
                            if (row == null) continue;
                            foreach (var cell in row)
                            {
                                if (cell != null) CollectLinksFromRichText(cell.Text, result);
                            }
                        }
                    }
                    return;

                // Related articles
                case PageBlockRelatedArticles ra:
                    CollectLinksFromRichText(ra.Header, result);
                    if (ra.Articles != null)
                    {
                        foreach (var article in ra.Articles)
                        {
                            if (article != null && !string.IsNullOrEmpty(article.Url))
                            {
                                result.Add(new PageBlockLink(article.Url, article.Title ?? string.Empty, PageBlockLinkKind.RelatedArticle));
                            }
                        }
                    }
                    return;

                // Media blocks: caption text + optional URL
                case PageBlockPhoto ph:
                    if (!string.IsNullOrEmpty(ph.Url))
                    {
                        result.Add(new PageBlockLink(ph.Url, string.Empty, PageBlockLinkKind.PhotoUrl));
                    }
                    CollectLinksFromCaption(ph.Caption, result);
                    return;
                case PageBlockVideo v:
                    CollectLinksFromCaption(v.Caption, result);
                    return;
                case PageBlockAnimation a:
                    CollectLinksFromCaption(a.Caption, result);
                    return;
                case PageBlockAudio au:
                    CollectLinksFromCaption(au.Caption, result);
                    return;
                case PageBlockVoiceNote vn:
                    CollectLinksFromCaption(vn.Caption, result);
                    return;
                case PageBlockMap m:
                    CollectLinksFromCaption(m.Caption, result);
                    return;
                case PageBlockEmbedded em:
                    if (!string.IsNullOrEmpty(em.Url))
                    {
                        result.Add(new PageBlockLink(em.Url, string.Empty, PageBlockLinkKind.Embedded));
                    }
                    CollectLinksFromCaption(em.Caption, result);
                    return;
                case PageBlockEmbeddedPost ep:
                    if (!string.IsNullOrEmpty(ep.Url))
                    {
                        result.Add(new PageBlockLink(ep.Url, ep.Author ?? string.Empty, PageBlockLinkKind.EmbeddedPost));
                    }
                    CollectLinksFromBlocks(ep.Blocks, result);
                    CollectLinksFromCaption(ep.Caption, result);
                    return;
                case PageBlockChatLink cl:
                    if (!string.IsNullOrEmpty(cl.Username))
                    {
                        result.Add(new PageBlockLink(cl.Username, cl.Title ?? string.Empty, PageBlockLinkKind.ChatLink));
                    }
                    return;

                // No links carried
                case PageBlockAnchor _:
                case PageBlockDivider _:
                case PageBlockMathematicalExpression _:
                    return;
            }
        }

        private static void CollectLinksFromCaption(PageBlockCaption caption, List<PageBlockLink> result)
        {
            if (caption == null) return;
            CollectLinksFromRichText(caption.Text, result);
            CollectLinksFromRichText(caption.Credit, result);
        }

        private static void CollectLinksFromRichText(RichText rt, List<PageBlockLink> result)
        {
            switch (rt)
            {
                case null:
                    return;

                // No-link leaves
                case RichTextPlain _:
                case RichTextCustomEmoji _:
                case RichTextIcon _:
                case RichTextAnchor _:
                case RichTextDateTime _:
                case RichTextMathematicalExpression _:
                case RichTextHashtag _:
                case RichTextCashtag _:
                case RichTextBotCommand _:
                case RichTextBankCardNumber _:
                    return;

                case RichTexts rs:
                    if (rs.Texts != null)
                    {
                        foreach (var t in rs.Texts) CollectLinksFromRichText(t, result);
                    }
                    return;

                // Style wrappers: recurse to find links nested under styling.
                case RichTextBold b: CollectLinksFromRichText(b.Text, result); return;
                case RichTextItalic b: CollectLinksFromRichText(b.Text, result); return;
                case RichTextUnderline b: CollectLinksFromRichText(b.Text, result); return;
                case RichTextStrikethrough b: CollectLinksFromRichText(b.Text, result); return;
                case RichTextSpoiler b: CollectLinksFromRichText(b.Text, result); return;
                case RichTextFixed b: CollectLinksFromRichText(b.Text, result); return;
                case RichTextSubscript b: CollectLinksFromRichText(b.Text, result); return;
                case RichTextSuperscript b: CollectLinksFromRichText(b.Text, result); return;
                case RichTextMarked b: CollectLinksFromRichText(b.Text, result); return;

                // Link-producing wrappers: emit a link, don't recurse into the
                // display text (a link's children describe its label, not nested
                // links — Telegram doesn't generate nested <a>).
                case RichTextUrl u:
                    result.Add(new PageBlockLink(u.Url, GetPlainText(u.Text), PageBlockLinkKind.Url));
                    return;
                case RichTextEmailAddress e:
                    {
                        var text = GetPlainText(e.Text);
                        result.Add(new PageBlockLink(e.EmailAddress ?? text, text, PageBlockLinkKind.Email));
                        return;
                    }
                case RichTextPhoneNumber pn:
                    {
                        var text = GetPlainText(pn.Text);
                        result.Add(new PageBlockLink(pn.PhoneNumber ?? text, text, PageBlockLinkKind.Phone));
                        return;
                    }
                case RichTextMention m:
                    {
                        var text = GetPlainText(m.Text);
                        result.Add(new PageBlockLink(text, text, PageBlockLinkKind.Mention));
                        return;
                    }
                case RichTextMentionName mn:
                    {
                        var text = GetPlainText(mn.Text);
                        result.Add(new PageBlockLink("tg://user?id=" + mn.UserId, text, PageBlockLinkKind.MentionName));
                        return;
                    }
                case RichTextReferenceLink rl:
                    result.Add(new PageBlockLink(rl.Url, GetPlainText(rl.Text), PageBlockLinkKind.ReferenceLink));
                    return;
                case RichTextAnchorLink al:
                    result.Add(new PageBlockLink(al.Url, GetPlainText(al.Text), PageBlockLinkKind.AnchorLink));
                    return;
            }
        }

        // Lightweight plain-text projection of a RichText subtree — used to
        // produce the visible label for an extracted link. Skips icons and
        // anchors (which contribute no text), but includes alt text from
        // custom emoji and the literal expression from math nodes.
        private static string GetPlainText(RichText rt)
        {
            if (rt == null) return string.Empty;
            var sb = new StringBuilder();
            AppendPlainText(rt, sb);
            return sb.ToString();
        }

        // =====================================================================
        // GetRichText — flatten a block list into a single RichTexts (lines joined by '\n')
        // =====================================================================

        public static string GetPlainText(IList<PageBlock> blocks)
        {
            return GetRichText(blocks).ToPlainText();
        }

        /// <summary>
        /// Projects the block tree onto a single <see cref="RichTexts"/> whose
        /// children are the page's text fragments — paragraphs, headings, list
        /// items, table cells, captions, credits, etc. — separated by newline
        /// fragments. Non-text blocks (photos, videos, dividers, &amp;c.) are
        /// rendered as hardcoded placeholder strings; replace the
        /// <c>Placeholder*</c> constants at the top of this class to localize.
        /// </summary>
        public static RichTexts GetRichText(IList<PageBlock> blocks)
        {
            var pieces = new List<RichText>();
            if (blocks != null)
            {
                foreach (var block in blocks)
                {
                    CollectRichTextFromBlock(block, pieces);
                }
            }

            return JoinWithNewlines(pieces);
        }

        private static RichTexts JoinWithNewlines(List<RichText> pieces)
        {
            if (pieces.Count == 0)
            {
                return new RichTexts(Array.Empty<RichText>());
            }

            // Drop empty pieces (null/empty plain text / empty concatenations) so we
            // don't produce double newlines around them.
            pieces.RemoveAll(IsEmpty);

            if (pieces.Count == 0)
            {
                return new RichTexts(Array.Empty<RichText>());
            }

            var joined = new List<RichText>(pieces.Count * 2 - 1);
            for (int i = 0; i < pieces.Count; i++)
            {
                if (i > 0) joined.Add(new RichTextPlain("\n"));
                joined.Add(pieces[i]);
            }
            return new RichTexts(joined);
        }

        private static void CollectRichTextFromBlocks(IList<PageBlock> blocks, List<RichText> pieces)
        {
            if (blocks == null) return;
            foreach (var b in blocks) CollectRichTextFromBlock(b, pieces);
        }

        private static void CollectRichTextFromBlock(PageBlock block, List<RichText> pieces)
        {
            switch (block)
            {
                case null:
                    return;

                // Text-bearing blocks — append the RichText directly. The block-level
                // type (heading vs paragraph vs caption) is dropped here; callers that
                // want headings highlighted can wrap them upstream.
                case PageBlockTitle t: pieces.Add(t.Title); return;
                case PageBlockSubtitle st: pieces.Add(st.Subtitle); return;
                case PageBlockKicker k: pieces.Add(k.Kicker); return;
                case PageBlockAuthorDate ad: pieces.Add(ad.Author); return;
                case PageBlockHeader h: pieces.Add(h.Header); return;
                case PageBlockSubheader sh: pieces.Add(sh.Subheader); return;
                case PageBlockSectionHeading sh2: pieces.Add(sh2.Text); return;
                case PageBlockThinking th: pieces.Add(th.Text); return;
                case PageBlockFooter f: pieces.Add(f.Footer); return;
                case PageBlockParagraph p: pieces.Add(p.Text); return;
                case PageBlockPreformatted pre: pieces.Add(pre.Text); return;
                case PageBlockPullQuote pq:
                    pieces.Add(pq.Text);
                    if (!IsEmpty(pq.Credit)) pieces.Add(pq.Credit);
                    return;

                // Container blocks
                case PageBlockBlockQuote bq:
                    CollectRichTextFromBlocks(bq.Blocks, pieces);
                    if (!IsEmpty(bq.Credit)) pieces.Add(bq.Credit);
                    return;
                case PageBlockList list:
                    if (list.Items != null)
                    {
                        foreach (var item in list.Items)
                        {
                            CollectRichTextFromBlocks(item.Blocks, pieces);
                        }
                    }
                    return;
                case PageBlockDetails details:
                    if (!IsEmpty(details.Header)) pieces.Add(details.Header);
                    CollectRichTextFromBlocks(details.Blocks, pieces);
                    return;
                case PageBlockCover cover:
                    CollectRichTextFromBlock(cover.Cover, pieces);
                    return;
                case PageBlockCollage c:
                    CollectRichTextFromBlocks(c.Blocks, pieces);
                    CollectRichTextFromCaption(c.Caption, pieces);
                    return;
                case PageBlockSlideshow s:
                    CollectRichTextFromBlocks(s.Blocks, pieces);
                    CollectRichTextFromCaption(s.Caption, pieces);
                    return;

                // Table — each cell becomes its own piece; row structure is lost.
                // If you need it back, change this arm to join row cells with " | ".
                case PageBlockTable table:
                    if (!IsEmpty(table.Caption)) pieces.Add(table.Caption);
                    if (table.Cells != null)
                    {
                        foreach (var row in table.Cells)
                        {
                            if (row == null) continue;
                            foreach (var cell in row)
                            {
                                if (cell?.Text != null) pieces.Add(cell.Text);
                            }
                        }
                    }
                    return;

                // Related articles — header, then each article title.
                case PageBlockRelatedArticles ra:
                    if (!IsEmpty(ra.Header)) pieces.Add(ra.Header);
                    if (ra.Articles != null)
                    {
                        foreach (var article in ra.Articles)
                        {
                            if (!string.IsNullOrEmpty(article?.Title))
                            {
                                pieces.Add(new RichTextPlain(article.Title));
                            }
                        }
                    }
                    return;

                // Media blocks — placeholder + caption (when present).
                case PageBlockPhoto ph:
                    pieces.Add(new RichTextPlain(PlaceholderPhoto));
                    CollectRichTextFromCaption(ph.Caption, pieces);
                    return;
                case PageBlockVideo v:
                    pieces.Add(new RichTextPlain(PlaceholderVideo));
                    CollectRichTextFromCaption(v.Caption, pieces);
                    return;
                case PageBlockAnimation a:
                    pieces.Add(new RichTextPlain(PlaceholderAnimation));
                    CollectRichTextFromCaption(a.Caption, pieces);
                    return;
                case PageBlockAudio au:
                    pieces.Add(new RichTextPlain(PlaceholderAudio));
                    CollectRichTextFromCaption(au.Caption, pieces);
                    return;
                case PageBlockVoiceNote vn:
                    pieces.Add(new RichTextPlain(PlaceholderVoiceNote));
                    CollectRichTextFromCaption(vn.Caption, pieces);
                    return;
                case PageBlockMap m:
                    pieces.Add(new RichTextPlain(PlaceholderMap));
                    CollectRichTextFromCaption(m.Caption, pieces);
                    return;
                case PageBlockEmbedded em:
                    pieces.Add(new RichTextPlain(PlaceholderEmbedded));
                    CollectRichTextFromCaption(em.Caption, pieces);
                    return;
                case PageBlockEmbeddedPost ep:
                    pieces.Add(new RichTextPlain(PlaceholderEmbeddedPost));
                    CollectRichTextFromBlocks(ep.Blocks, pieces);
                    CollectRichTextFromCaption(ep.Caption, pieces);
                    return;
                case PageBlockChatLink cl:
                    pieces.Add(new RichTextPlain(PlaceholderChatLink));
                    return;
                case PageBlockDivider _:
                    pieces.Add(new RichTextPlain(PlaceholderDivider));
                    return;

                // Inline math at block level — emit the raw expression.
                case PageBlockMathematicalExpression me:
                    if (!string.IsNullOrEmpty(me.Expression))
                    {
                        pieces.Add(new RichTextPlain(me.Expression));
                    }
                    return;

                // Anchors have no textual content — skip.
                case PageBlockAnchor _:
                    return;
            }
        }

        private static void CollectRichTextFromCaption(PageBlockCaption caption, List<RichText> pieces)
        {
            if (caption == null) return;
            if (!IsEmpty(caption.Text)) pieces.Add(caption.Text);
            if (!IsEmpty(caption.Credit)) pieces.Add(caption.Credit);
        }

        public static bool IsEmpty(RichText rt)
        {
            switch (rt)
            {
                case null:
                    return true;
                case RichTextPlain p:
                    return string.IsNullOrEmpty(p.Text);
                case RichTexts rs:
                    if (rs.Texts == null || rs.Texts.Count == 0) return true;
                    foreach (var t in rs.Texts)
                    {
                        if (!IsEmpty(t)) return false;
                    }
                    return true;
                default:
                    return false;
            }
        }
        private static void AppendPlainText(RichText rt, StringBuilder sb)
        {
            switch (rt)
            {
                case null:
                case RichTextAnchor _:
                case RichTextIcon _:
                    return;
                case RichTextPlain p:
                    if (p.Text != null) sb.Append(p.Text);
                    return;
                case RichTexts rs:
                    if (rs.Texts != null)
                    {
                        foreach (var t in rs.Texts) AppendPlainText(t, sb);
                    }
                    return;
                case RichTextCustomEmoji ce:
                    if (ce.AlternativeText != null) sb.Append(ce.AlternativeText);
                    return;
                case RichTextMathematicalExpression me:
                    if (me.Expression != null) sb.Append(me.Expression);
                    return;
                case RichTextBold b: AppendPlainText(b.Text, sb); return;
                case RichTextItalic b: AppendPlainText(b.Text, sb); return;
                case RichTextUnderline b: AppendPlainText(b.Text, sb); return;
                case RichTextStrikethrough b: AppendPlainText(b.Text, sb); return;
                case RichTextSpoiler b: AppendPlainText(b.Text, sb); return;
                case RichTextFixed b: AppendPlainText(b.Text, sb); return;
                case RichTextSubscript b: AppendPlainText(b.Text, sb); return;
                case RichTextSuperscript b: AppendPlainText(b.Text, sb); return;
                case RichTextMarked b: AppendPlainText(b.Text, sb); return;
                case RichTextUrl b: AppendPlainText(b.Text, sb); return;
                case RichTextEmailAddress b: AppendPlainText(b.Text, sb); return;
                case RichTextPhoneNumber b: AppendPlainText(b.Text, sb); return;
                case RichTextMention b: AppendPlainText(b.Text, sb); return;
                case RichTextMentionName b: AppendPlainText(b.Text, sb); return;
                case RichTextHashtag b: AppendPlainText(b.Text, sb); return;
                case RichTextCashtag b: AppendPlainText(b.Text, sb); return;
                case RichTextBotCommand b: AppendPlainText(b.Text, sb); return;
                case RichTextBankCardNumber b: AppendPlainText(b.Text, sb); return;
                case RichTextReference b: AppendPlainText(b.Text, sb); return;
                case RichTextReferenceLink b: AppendPlainText(b.Text, sb); return;
                case RichTextAnchorLink b: AppendPlainText(b.Text, sb); return;
                case RichTextDateTime b: AppendPlainText(b.Text, sb); return;
            }
        }

        // Placeholder used in flat text for inline image icons (richTextIcon).
        // The richTextAnchor variant is intentionally skipped — it has no text
        // representation and isn't yet plumbed through StyledText. If a renderer
        // needs scroll-to-anchor, add an Anchors side-channel on StyledText.
        private const char ObjectReplacementChar = '\uFFFC';

        public static void Flatten(RichText richText, StringBuilder text, IList<TextEntity> entities)
        {
            switch (richText)
            {
                case null:
                    return;

                case RichTextPlain p:
                    if (!string.IsNullOrEmpty(p.Text))
                    {
                        text.Append(p.Text);
                    }
                    return;

                case RichTexts rs:
                    if (rs.Texts != null)
                    {
                        foreach (var child in rs.Texts)
                        {
                            Flatten(child, text, entities);
                        }
                    }
                    return;

                // Style wrappers
                case RichTextBold b:
                    EmitSpan(b.Text, text, entities, new TextEntityTypeBold());
                    return;
                case RichTextItalic b:
                    EmitSpan(b.Text, text, entities, new TextEntityTypeItalic());
                    return;
                case RichTextUnderline b:
                    EmitSpan(b.Text, text, entities, new TextEntityTypeUnderline());
                    return;
                case RichTextStrikethrough b:
                    EmitSpan(b.Text, text, entities, new TextEntityTypeStrikethrough());
                    return;
                case RichTextSpoiler b:
                    EmitSpan(b.Text, text, entities, new TextEntityTypeSpoiler());
                    return;
                case RichTextFixed b:
                    // richTextFixed maps to inline code. Block-level <pre> comes from
                    // pageBlockPreformatted, which is handled at the block layer above.
                    EmitSpan(b.Text, text, entities, new TextEntityTypeCode());
                    return;
                case RichTextSubscript b:
                    EmitSpan(b.Text, text, entities, new TextEntityTypeSubscript());
                    return;
                case RichTextSuperscript b:
                    EmitSpan(b.Text, text, entities, new TextEntityTypeSuperscript());
                    return;
                case RichTextMarked b:
                    EmitSpan(b.Text, text, entities, new TextEntityTypeMarked());
                    return;

                // Url-shaped wrappers (carry a payload)
                case RichTextUrl b:
                    EmitSpan(b.Text, text, entities, new TextEntityTypeTextUrl(b.Url));
                    return;
                case RichTextEmailAddress b:
                    EmitSpan(b.Text, text, entities, new TextEntityTypeEmailAddress());
                    return;
                case RichTextPhoneNumber b:
                    EmitSpan(b.Text, text, entities, new TextEntityTypePhoneNumber());
                    return;
                case RichTextMentionName b:
                    EmitSpan(b.Text, text, entities, new TextEntityTypeMentionName(b.UserId));
                    return;
                case RichTextReference b:
                    // Reference and AnchorLink both navigate to an in-page anchor. The
                    // server-provided URL is the standard target, so they fold into
                    // TextEntityTypeTextUrl. If the renderer needs the anchor_name
                    // separately, swap this for a dedicated entity type.
                    //EmitSpan(b.Text, text, entities, new TextEntityTypeTextUrl(b.Name));
                    Flatten(b.Text, text, entities);
                    return;
                case RichTextReferenceLink b:
                    // Reference and AnchorLink both navigate to an in-page anchor. The
                    // server-provided URL is the standard target, so they fold into
                    // TextEntityTypeTextUrl. If the renderer needs the anchor_name
                    // separately, swap this for a dedicated entity type.
                    EmitSpan(b.Text, text, entities, new TextEntityTypeTextUrl(b.Url));
                    return;
                case RichTextAnchorLink b:
                    EmitSpan(b.Text, text, entities, new TextEntityTypeTextUrl(b.Url));
                    return;

                // Auto-detected entities (text is the value)
                case RichTextMention b:
                    EmitSpan(b.Text, text, entities, new TextEntityTypeMention());
                    return;
                case RichTextHashtag b:
                    EmitSpan(b.Text, text, entities, new TextEntityTypeHashtag());
                    return;
                case RichTextCashtag b:
                    EmitSpan(b.Text, text, entities, new TextEntityTypeCashtag());
                    return;
                case RichTextBotCommand b:
                    EmitSpan(b.Text, text, entities, new TextEntityTypeBotCommand());
                    return;
                case RichTextBankCardNumber b:
                    EmitSpan(b.Text, text, entities, new TextEntityTypeBankCardNumber());
                    return;
                case RichTextDateTime b:
                    EmitSpan(b.Text, text, entities, new TextEntityTypeDateTime(b.UnixTime, b.FormattingType));
                    return;

                // Leaves carrying their own text content
                case RichTextCustomEmoji ce:
                    if (!string.IsNullOrEmpty(ce.AlternativeText))
                    {
                        int start = text.Length;
                        text.Append(ce.AlternativeText);
                        entities.Add(new TextEntity(start, ce.AlternativeText.Length,
                            new TextEntityTypeCustomEmoji(ce.CustomEmojiId)));
                    }
                    return;
                case RichTextMathematicalExpression me:
                    if (!string.IsNullOrEmpty(me.Expression))
                    {
                        int start = text.Length;
                        text.Append(me.Expression);
                        entities.Add(new TextEntity(start, me.Expression.Length,
                            new TextEntityTypeMathematicalExpression(me.Expression)));
                    }
                    return;

                // Leaf with no text — use a single placeholder character so the entity
                // has a non-zero length and survives GetRuns. The renderer dispatches
                // on TextEntityTypeIcon and substitutes an InlineUIContainer.
                case RichTextIcon icon:
                    {
                        int start = text.Length;
                        text.Append(ObjectReplacementChar);
                        entities.Add(new TextEntity(start, 1,
                            new TextEntityTypeIcon(icon.Document, icon.Width, icon.Height)));
                        return;
                    }

                case RichTextAnchor _:
                    // Skipped — see ObjectReplacementChar comment above.
                    return;
            }
        }

        private static void EmitSpan(RichText inner, StringBuilder text, IList<TextEntity> entities, TextEntityType type)
        {
            int start = text.Length;
            Flatten(inner, text, entities);
            int length = text.Length - start;
            if (length > 0)
            {
                entities.Add(new TextEntity(start, length, type));
            }
        }

        // =====================================================================
        // ToPageBlocks — FormattedText (message text + entities) -> page blocks
        // =====================================================================

        /// <summary>
        /// Converts a <see cref="FormattedText"/> (a message's text + entities) into a
        /// list of <see cref="PageBlock"/>, preserving every entity — the inverse of
        /// <see cref="Flatten"/>. Block-level entities become their own blocks: block
        /// quotes (incl. expandable) → <see cref="PageBlockBlockQuote"/>, pre / pre-code
        /// → <see cref="PageBlockPreformatted"/>. Everything else becomes a
        /// <see cref="PageBlockParagraph"/>, one per line (the text is split on every
        /// '\n', inside block quotes too; empty lines are dropped). Inline entities are
        /// mapped to their <c>richText*</c> equivalents, with nested/overlapping ranges
        /// resolved into a nested <see cref="RichText"/> tree.
        /// </summary>
        public static IList<PageBlock> ToPageBlocks(FormattedText text)
        {
            var blocks = new List<PageBlock>();
            var s = text?.Text ?? string.Empty;
            var entities = text?.Entities ?? new List<TextEntity>();

            // Split entities: block-level ones partition the text; the rest are inline.
            var blockEntities = new List<TextEntity>();
            var inline = new List<TextEntity>();
            foreach (var e in entities)
            {
                (IsBlockEntity(e.Type) ? blockEntities : inline).Add(e);
            }
            blockEntities.Sort((a, b) => a.Offset.CompareTo(b.Offset));

            int pos = 0;
            foreach (var be in blockEntities)
            {
                if (be.Offset < pos)
                {
                    continue; // nested/overlapping block entity — folded into the outer one
                }

                int end = Math.Min(be.Offset + be.Length, s.Length);
                if (be.Offset > pos)
                {
                    AppendParagraphs(blocks, s, pos, be.Offset, inline);
                }

                switch (be.Type)
                {
                    case TextEntityTypePre _:
                        blocks.Add(new PageBlockPreformatted(BuildRichText(s, be.Offset, end, inline), string.Empty));
                        break;
                    case TextEntityTypePreCode pc:
                        blocks.Add(new PageBlockPreformatted(BuildRichText(s, be.Offset, end, inline), pc.Language ?? string.Empty));
                        break;
                    default: // BlockQuote / ExpandableBlockQuote
                        var inner = new List<PageBlock>();
                        AppendParagraphs(inner, s, be.Offset, end, inline);
                        if (inner.Count == 0)
                        {
                            inner.Add(new PageBlockParagraph(new RichTextPlain(string.Empty)));
                        }
                        blocks.Add(new PageBlockBlockQuote(inner, new RichTextPlain(string.Empty)));
                        break;
                }

                pos = end;
            }

            if (pos < s.Length)
            {
                AppendParagraphs(blocks, s, pos, s.Length, inline);
            }

            return blocks;
        }

        private static bool IsBlockEntity(TextEntityType type)
        {
            return type is TextEntityTypeBlockQuote
                || type is TextEntityTypeExpandableBlockQuote
                || type is TextEntityTypePre
                || type is TextEntityTypePreCode;
        }

        // Split [start,end) on '\n' and add one PageBlockParagraph per non-empty line.
        private static void AppendParagraphs(IList<PageBlock> blocks, string s, int start, int end, List<TextEntity> inline)
        {
            int lineStart = start;
            for (int i = start; i <= end; i++)
            {
                if (i == end || s[i] == '\n')
                {
                    if (i > lineStart) // skip empty lines (consecutive newlines)
                    {
                        blocks.Add(new PageBlockParagraph(BuildRichText(s, lineStart, i, inline)));
                    }
                    lineStart = i + 1;
                }
            }
        }

        // Build a nested RichText for s[start,end), applying the inline entities that
        // intersect it. At each step the earliest-starting (then longest) entity wins
        // and becomes the outer wrapper; the entities inside it recurse to form its
        // child. Crossing ranges are clipped to the chosen span.
        private static RichText BuildRichText(string s, int start, int end, List<TextEntity> inline)
        {
            if (end <= start)
            {
                return new RichTextPlain(string.Empty);
            }

            var active = new List<TextEntity>();
            foreach (var e in inline)
            {
                if (Math.Max(e.Offset, start) < Math.Min(e.Offset + e.Length, end))
                {
                    active.Add(e);
                }
            }

            var parts = new List<RichText>();
            int pos = start;
            while (pos < end)
            {
                TextEntity next = null;
                int ns = end, ne = end;
                foreach (var e in active)
                {
                    int es = Math.Max(e.Offset, pos);
                    int ee = Math.Min(e.Offset + e.Length, end);
                    if (ee <= pos || es >= end)
                    {
                        continue;
                    }
                    if (es < ns || (es == ns && ee > ne))
                    {
                        next = e; ns = es; ne = ee;
                    }
                }

                if (next == null)
                {
                    parts.Add(new RichTextPlain(s.Substring(pos, end - pos)));
                    break;
                }

                if (ns > pos)
                {
                    parts.Add(new RichTextPlain(s.Substring(pos, ns - pos)));
                }

                var covered = s.Substring(ns, ne - ns);
                if (IsLeafEntity(next.Type))
                {
                    parts.Add(MakeLeaf(next.Type, covered));
                }
                else
                {
                    var innerList = new List<TextEntity>();
                    foreach (var e in active)
                    {
                        if (ReferenceEquals(e, next))
                        {
                            continue;
                        }
                        if (Math.Max(e.Offset, ns) < Math.Min(e.Offset + e.Length, ne))
                        {
                            innerList.Add(e);
                        }
                    }
                    parts.Add(Wrap(next.Type, BuildRichText(s, ns, ne, innerList), covered));
                }

                pos = ne;
            }

            if (parts.Count == 0) return new RichTextPlain(string.Empty);
            return parts.Count == 1 ? parts[0] : new RichTexts(parts);
        }

        // Entities with no RichText child of their own (their span IS their content).
        private static bool IsLeafEntity(TextEntityType type)
            => type is TextEntityTypeCustomEmoji || type is TextEntityTypeMathematicalExpression;

        private static RichText MakeLeaf(TextEntityType type, string covered)
        {
            switch (type)
            {
                case TextEntityTypeCustomEmoji ce:
                    return new RichTextCustomEmoji(ce.CustomEmojiId, covered);
                case TextEntityTypeMathematicalExpression me:
                    return new RichTextMathematicalExpression(string.IsNullOrEmpty(me.Expression) ? covered : me.Expression);
                default:
                    return new RichTextPlain(covered);
            }
        }

        // Inline entity -> RichText wrapper around its (already-built) child. `covered`
        // is the entity's plain text, used to fill payload fields that the entity type
        // doesn't carry (mention/hashtag/auto-url/...).
        private static RichText Wrap(TextEntityType type, RichText child, string covered)
        {
            switch (type)
            {
                case TextEntityTypeBold _: return new RichTextBold(child);
                case TextEntityTypeItalic _: return new RichTextItalic(child);
                case TextEntityTypeUnderline _: return new RichTextUnderline(child);
                case TextEntityTypeStrikethrough _: return new RichTextStrikethrough(child);
                case TextEntityTypeSpoiler _: return new RichTextSpoiler(child);
                case TextEntityTypeCode _: return new RichTextFixed(child);
                case TextEntityTypeSubscript _: return new RichTextSubscript(child);
                case TextEntityTypeSuperscript _: return new RichTextSuperscript(child);
                case TextEntityTypeMarked _: return new RichTextMarked(child);
                case TextEntityTypeTextUrl u: return new RichTextUrl(child, u.Url, false);
                case TextEntityTypeUrl _: return new RichTextUrl(child, covered, false);
                case TextEntityTypeMentionName mn: return new RichTextMentionName(child, mn.UserId);
                case TextEntityTypeMention _: return new RichTextMention(child, Strip(covered, '@'));
                case TextEntityTypeHashtag _: return new RichTextHashtag(child, Strip(covered, '#'));
                case TextEntityTypeCashtag _: return new RichTextCashtag(child, Strip(covered, '$'));
                case TextEntityTypeBotCommand _: return new RichTextBotCommand(child, Strip(covered, '/'));
                case TextEntityTypeBankCardNumber _: return new RichTextBankCardNumber(child, covered);
                case TextEntityTypeEmailAddress _: return new RichTextEmailAddress(child, covered);
                case TextEntityTypePhoneNumber _: return new RichTextPhoneNumber(child, covered);
                case TextEntityTypeDateTime dt: return new RichTextDateTime(child, dt.UnixTime, dt.FormattingType);
                default: return child; // MediaTimestamp, Icon, etc. — keep the text, drop the wrapper
            }
        }

        private static string Strip(string s, char prefix)
            => s.Length > 0 && s[0] == prefix ? s.Substring(1) : s;

        // =====================================================================
        // Compare — structural equality for incremental (streamed) updates
        // =====================================================================

        /// <summary>
        /// Deep structural equality of two page blocks, used as the item-equality
        /// test when diffing a streaming message so unchanged blocks aren't
        /// re-rendered. Returns true only when the two blocks would render
        /// identically — every rendered property is compared. (Media is compared by
        /// its scalar attributes + caption; the underlying file objects, which are
        /// stable across a stream, are not deep-compared.)
        /// </summary>
        public static bool Compare(PageBlock x, PageBlock y)
        {
            if (x == null || y == null)
            {
                return x == y;
            }

            return (x, y) switch
            {
                // Text-only blocks
                (PageBlockTitle a, PageBlockTitle b) => Compare(a.Title, b.Title),
                (PageBlockSubtitle a, PageBlockSubtitle b) => Compare(a.Subtitle, b.Subtitle),
                (PageBlockAuthorDate a, PageBlockAuthorDate b) => a.PublishDate == b.PublishDate && Compare(a.Author, b.Author),
                (PageBlockHeader a, PageBlockHeader b) => Compare(a.Header, b.Header),
                (PageBlockSubheader a, PageBlockSubheader b) => Compare(a.Subheader, b.Subheader),
                (PageBlockKicker a, PageBlockKicker b) => Compare(a.Kicker, b.Kicker),
                (PageBlockSectionHeading a, PageBlockSectionHeading b) => a.Size == b.Size && Compare(a.Text, b.Text),
                (PageBlockParagraph a, PageBlockParagraph b) => Compare(a.Text, b.Text),
                (PageBlockPreformatted a, PageBlockPreformatted b) => string.Equals(a.Language, b.Language) && Compare(a.Text, b.Text),
                (PageBlockFooter a, PageBlockFooter b) => Compare(a.Footer, b.Footer),
                (PageBlockThinking a, PageBlockThinking b) => Compare(a.Text, b.Text),
                (PageBlockPullQuote a, PageBlockPullQuote b) => Compare(a.Text, b.Text) && Compare(a.Credit, b.Credit),
                (PageBlockMathematicalExpression a, PageBlockMathematicalExpression b) => string.Equals(a.Expression, b.Expression),

                // Atomic
                (PageBlockAnchor a, PageBlockAnchor b) => a.Name == b.Name,
                (PageBlockDivider, PageBlockDivider) => true,
                (PageBlockChatLink a, PageBlockChatLink b) => a.Title == b.Title && a.Username == b.Username && a.AccentColorId == b.AccentColorId,

                // Media + caption (scalar attributes + caption)
                (PageBlockAnimation a, PageBlockAnimation b) => a.NeedAutoplay == b.NeedAutoplay && a.HasSpoiler == b.HasSpoiler && Compare(a.Caption, b.Caption),
                (PageBlockAudio a, PageBlockAudio b) => Compare(a.Caption, b.Caption),
                (PageBlockPhoto a, PageBlockPhoto b) => string.Equals(a.Url, b.Url) && a.HasSpoiler == b.HasSpoiler && Compare(a.Caption, b.Caption),
                (PageBlockVideo a, PageBlockVideo b) => a.NeedAutoplay == b.NeedAutoplay && a.IsLooped == b.IsLooped && a.HasSpoiler == b.HasSpoiler && Compare(a.Caption, b.Caption),
                (PageBlockVoiceNote a, PageBlockVoiceNote b) => Compare(a.Caption, b.Caption),
                (PageBlockMap a, PageBlockMap b) => a.Zoom == b.Zoom && a.Width == b.Width && a.Height == b.Height && SameLocation(a.Location, b.Location) && Compare(a.Caption, b.Caption),
                (PageBlockEmbedded a, PageBlockEmbedded b) => string.Equals(a.Url, b.Url) && string.Equals(a.Html, b.Html) && a.Width == b.Width && a.Height == b.Height && a.IsFullWidth == b.IsFullWidth && a.AllowScrolling == b.AllowScrolling && Compare(a.Caption, b.Caption),

                // Containers
                (PageBlockCover a, PageBlockCover b) => Compare(a.Cover, b.Cover),
                (PageBlockList a, PageBlockList b) => Compare(a.Items, b.Items),
                (PageBlockBlockQuote a, PageBlockBlockQuote b) => Compare(a.Blocks, b.Blocks) && Compare(a.Credit, b.Credit),
                (PageBlockCollage a, PageBlockCollage b) => Compare(a.Blocks, b.Blocks) && Compare(a.Caption, b.Caption),
                (PageBlockSlideshow a, PageBlockSlideshow b) => Compare(a.Blocks, b.Blocks) && Compare(a.Caption, b.Caption),
                (PageBlockEmbeddedPost a, PageBlockEmbeddedPost b) => string.Equals(a.Url, b.Url) && string.Equals(a.Author, b.Author) && a.Date == b.Date && Compare(a.Blocks, b.Blocks) && Compare(a.Caption, b.Caption),
                (PageBlockDetails a, PageBlockDetails b) => a.IsOpen == b.IsOpen && Compare(a.Header, b.Header) && Compare(a.Blocks, b.Blocks),
                (PageBlockTable a, PageBlockTable b) => a.IsBordered == b.IsBordered && a.IsStriped == b.IsStriped && Compare(a.Caption, b.Caption) && Compare(a.Cells, b.Cells),

                // PageBlockRelatedArticles is intentionally not diffed (always re-rendered).
                _ => false,
            };
        }

        private static bool Compare(RichText x, RichText y)
        {
            switch (x, y)
            {
                case (null, null): return true;
                case (RichTextPlain a, RichTextPlain b): return string.Equals(a.Text, b.Text);

                case (RichTexts a, RichTexts b):
                    if (a.Texts.Count != b.Texts.Count)
                    {
                        return false;
                    }
                    for (int i = 0; i < a.Texts.Count; i++)
                    {
                        if (!Compare(a.Texts[i], b.Texts[i]))
                        {
                            return false;
                        }
                    }
                    return true;

                // Leaves
                case (RichTextAnchor a, RichTextAnchor b): return a.Name == b.Name;
                case (RichTextIcon a, RichTextIcon b): return a.Document.DocumentValue.Id == b.Document.DocumentValue.Id && a.Width == b.Width && a.Height == b.Height;
                case (RichTextCustomEmoji a, RichTextCustomEmoji b): return a.CustomEmojiId == b.CustomEmojiId && string.Equals(a.AlternativeText, b.AlternativeText);
                case (RichTextMathematicalExpression a, RichTextMathematicalExpression b): return string.Equals(a.Expression, b.Expression);

                // Plain style wrappers (no payload beyond the inner text)
                case (RichTextBold a, RichTextBold b): return Compare(a.Text, b.Text);
                case (RichTextItalic a, RichTextItalic b): return Compare(a.Text, b.Text);
                case (RichTextUnderline a, RichTextUnderline b): return Compare(a.Text, b.Text);
                case (RichTextStrikethrough a, RichTextStrikethrough b): return Compare(a.Text, b.Text);
                case (RichTextSpoiler a, RichTextSpoiler b): return Compare(a.Text, b.Text);
                case (RichTextFixed a, RichTextFixed b): return Compare(a.Text, b.Text);
                case (RichTextSubscript a, RichTextSubscript b): return Compare(a.Text, b.Text);
                case (RichTextSuperscript a, RichTextSuperscript b): return Compare(a.Text, b.Text);
                case (RichTextMarked a, RichTextMarked b): return Compare(a.Text, b.Text);

                // Wrappers carrying a payload — compare the payload too
                case (RichTextUrl a, RichTextUrl b): return string.Equals(a.Url, b.Url) && a.IsCached == b.IsCached && Compare(a.Text, b.Text);
                case (RichTextEmailAddress a, RichTextEmailAddress b): return string.Equals(a.EmailAddress, b.EmailAddress) && Compare(a.Text, b.Text);
                case (RichTextPhoneNumber a, RichTextPhoneNumber b): return string.Equals(a.PhoneNumber, b.PhoneNumber) && Compare(a.Text, b.Text);
                case (RichTextMention a, RichTextMention b): return string.Equals(a.Username, b.Username) && Compare(a.Text, b.Text);
                case (RichTextHashtag a, RichTextHashtag b): return string.Equals(a.Hashtag, b.Hashtag) && Compare(a.Text, b.Text);
                case (RichTextCashtag a, RichTextCashtag b): return string.Equals(a.Cashtag, b.Cashtag) && Compare(a.Text, b.Text);
                case (RichTextBotCommand a, RichTextBotCommand b): return string.Equals(a.BotCommand, b.BotCommand) && Compare(a.Text, b.Text);
                case (RichTextMentionName a, RichTextMentionName b): return a.UserId == b.UserId && Compare(a.Text, b.Text);
                case (RichTextBankCardNumber a, RichTextBankCardNumber b): return string.Equals(a.BankCardNumber, b.BankCardNumber) && Compare(a.Text, b.Text);
                case (RichTextDateTime a, RichTextDateTime b): return a.UnixTime == b.UnixTime && SameType(a.FormattingType, b.FormattingType) && Compare(a.Text, b.Text);
                case (RichTextReference a, RichTextReference b): return string.Equals(a.Name, b.Name) && Compare(a.Text, b.Text);
                case (RichTextReferenceLink a, RichTextReferenceLink b): return string.Equals(a.ReferenceName, b.ReferenceName) && string.Equals(a.Url, b.Url) && Compare(a.Text, b.Text);
                case (RichTextAnchorLink a, RichTextAnchorLink b): return string.Equals(a.AnchorName, b.AnchorName) && string.Equals(a.Url, b.Url) && Compare(a.Text, b.Text);

                default: return false;
            }
        }

        private static bool Compare(PageBlockCaption a, PageBlockCaption b)
        {
            if (a == null || b == null)
            {
                return a == b;
            }

            return Compare(a.Credit, b.Credit) && Compare(a.Text, b.Text);
        }

        private static bool Compare(IList<PageBlock> a, IList<PageBlock> b)
        {
            if (a == null || b == null)
            {
                return a == b;
            }
            if (a.Count != b.Count)
            {
                return false;
            }
            for (int i = 0; i < a.Count; i++)
            {
                if (!Compare(a[i], b[i]))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool Compare(PageBlockListItem a, PageBlockListItem b)
        {
            return a.HasCheckbox == b.HasCheckbox
                && a.IsChecked == b.IsChecked
                && a.Label == b.Label
                && a.Type == b.Type
                && a.Value == b.Value
                && Compare(a.Blocks, b.Blocks);
        }

        private static bool Compare(IList<PageBlockListItem> a, IList<PageBlockListItem> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }
            for (int i = 0; i < a.Count; i++)
            {
                if (!Compare(a[i], b[i]))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool Compare(PageBlockTableCell a, PageBlockTableCell b)
        {
            return a.IsHeader == b.IsHeader
                && a.Colspan == b.Colspan
                && a.Rowspan == b.Rowspan
                && SameType(a.Align, b.Align)
                && SameType(a.Valign, b.Valign)
                && Compare(a.Text, b.Text);
        }

        private static bool Compare(IList<PageBlockTableCell> a, IList<PageBlockTableCell> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }
            for (int i = 0; i < a.Count; i++)
            {
                if (!Compare(a[i], b[i]))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool Compare(IList<IList<PageBlockTableCell>> a, IList<IList<PageBlockTableCell>> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }
            for (int i = 0; i < a.Count; i++)
            {
                if (!Compare(a[i], b[i]))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool SameType(object a, object b) => a?.GetType() == b?.GetType();

        private static bool SameLocation(Location a, Location b)
        {
            if (a == null || b == null)
            {
                return a == b;
            }

            return a.Latitude == b.Latitude && a.Longitude == b.Longitude && a.HorizontalAccuracy == b.HorizontalAccuracy;
        }
    }
}
