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
        Document = 0x100u,       // pageBlockDocument (a file, not a viewable medium)

        // Conventional groupings. Document is deliberately outside Visual and
        // Media: it has no preview to show, so a gallery asking for Visual must
        // not be handed one.
        Media = Photo | Video,
        Visual = Photo | Video | Animation,
        Audible = Audio | VoiceNote,
        Any = Photo | Video | Animation | Audio | VoiceNote | Map | Embedded | EmbeddedPost | Document
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
        private const string PlaceholderDocument = "\U0001F4C4";
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
        public static PageBlock FindFirstMedia(Vector<PageBlock> blocks, PageBlockMediaKind kind)
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
                case PageBlockDocument when (kind & PageBlockMediaKind.Document) != 0:
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

        private static PageBlock FindFirstMediaInList(Vector<PageBlock> blocks, PageBlockMediaKind kind)
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
        // FindAllMedia
        // =====================================================================

        /// <summary>
        /// Walks the block list (descending into the same container blocks as
        /// <see cref="FindFirstMedia"/>) and returns every block whose kind is
        /// included in <paramref name="kind"/>, in document order.
        /// </summary>
        public static List<PageBlock> FindAllMedia(Vector<PageBlock> blocks, PageBlockMediaKind kind)
        {
            var result = new List<PageBlock>();
            FindAllMedia(blocks, kind, result);
            return result;
        }

        /// <summary>
        /// Appends every matching media block to <paramref name="result"/> (which the
        /// caller owns — reuse it across calls to avoid the list allocation). The
        /// traversal is recursion over the call stack with no intermediate
        /// allocations (no LINQ/iterators/closures).
        /// </summary>
        public static void FindAllMedia(Vector<PageBlock> blocks, PageBlockMediaKind kind, List<PageBlock> result)
        {
            if (blocks == null || kind == PageBlockMediaKind.None)
            {
                return;
            }

            foreach (var block in blocks)
            {
                CollectMediaCore(block, kind, result);
            }
        }

        private static void CollectMediaCore(PageBlock block, PageBlockMediaKind kind, List<PageBlock> result)
        {
            // A block can be both a match and a container (PageBlockEmbeddedPost), so
            // unlike FindFirst — which stops at the first hit — we add the match AND
            // still descend, to collect everything.
            if (MatchesKind(block, kind))
            {
                result.Add(block);
            }

            switch (block)
            {
                case PageBlockCover cover:
                    CollectMediaCore(cover.Cover, kind, result);
                    return;
                case PageBlockList list:
                    if (list.Items != null)
                    {
                        foreach (var item in list.Items)
                        {
                            CollectMediaInList(item.Blocks, kind, result);
                        }
                    }
                    return;
                case PageBlockDetails details:
                    CollectMediaInList(details.Blocks, kind, result);
                    return;
                case PageBlockCollage collage:
                    CollectMediaInList(collage.Blocks, kind, result);
                    return;
                case PageBlockSlideshow slideshow:
                    CollectMediaInList(slideshow.Blocks, kind, result);
                    return;
                case PageBlockBlockQuote blockquote:
                    CollectMediaInList(blockquote.Blocks, kind, result);
                    return;
                case PageBlockEmbeddedPost ep:
                    CollectMediaInList(ep.Blocks, kind, result);
                    return;
            }
        }

        private static void CollectMediaInList(Vector<PageBlock> blocks, PageBlockMediaKind kind, List<PageBlock> result)
        {
            if (blocks == null) return;
            foreach (var b in blocks)
            {
                CollectMediaCore(b, kind, result);
            }
        }

        private static bool MatchesKind(PageBlock block, PageBlockMediaKind kind)
        {
            return block switch
            {
                PageBlockPhoto => (kind & PageBlockMediaKind.Photo) != 0,
                PageBlockVideo => (kind & PageBlockMediaKind.Video) != 0,
                PageBlockAnimation => (kind & PageBlockMediaKind.Animation) != 0,
                PageBlockAudio => (kind & PageBlockMediaKind.Audio) != 0,
                PageBlockVoiceNote => (kind & PageBlockMediaKind.VoiceNote) != 0,
                PageBlockDocument => (kind & PageBlockMediaKind.Document) != 0,
                PageBlockMap => (kind & PageBlockMediaKind.Map) != 0,
                PageBlockEmbedded => (kind & PageBlockMediaKind.Embedded) != 0,
                PageBlockEmbeddedPost => (kind & PageBlockMediaKind.EmbeddedPost) != 0,
                _ => false,
            };
        }

        // =====================================================================
        // GetLinks
        // =====================================================================

        /// <summary>
        /// Collects every link reachable from the block list, in document order.
        /// Walks both the block tree and any nested rich-text content (paragraphs,
        /// captions, table cells, etc.). The returned list is owned by the caller.
        /// </summary>
        public static IList<PageBlockLink> GetLinks(Vector<PageBlock> blocks)
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

        private static void CollectLinksFromBlocks(Vector<PageBlock> blocks, List<PageBlockLink> result)
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
                // Text and credit like a pull quote, not a list of blocks like a
                // block quote — the expandable one holds rich text directly.
                case PageBlockExpandableBlockQuote eq:
                    CollectLinksFromRichText(eq.Text, result);
                    CollectLinksFromRichText(eq.Credit, result);
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
                case PageBlockDocument doc:
                    CollectLinksFromCaption(doc.Caption, result);
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

                // No links carried. A button row is a row of actions, not of
                // links: what a button does is the client's business, and it is
                // not part of the page's link graph.
                case PageBlockButtonRow _:
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

                // No-link leaves. RichTextButton is one of them on purpose: its target
                // is an action to invoke, not a link to follow — the same reason an
                // inline keyboard never appears in GetLinks.
                case RichTextButton _:
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

        public static string GetPlainText(Vector<PageBlock> blocks)
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
        public static RichTexts GetRichText(Vector<PageBlock> blocks)
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

            var joined = new MutableVector<RichText>(pieces.Count * 2 - 1);
            for (int i = 0; i < pieces.Count; i++)
            {
                if (i > 0) joined.Add(new RichTextPlain("\n"));
                joined.Add(pieces[i]);
            }
            return new RichTexts(joined);
        }

        private static void CollectRichTextFromBlocks(Vector<PageBlock> blocks, List<RichText> pieces)
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
                // Holds rich text directly (like a pull quote), not a list of
                // blocks like PageBlockBlockQuote — the "expandable" part is a
                // rendering affordance and has no text projection.
                case PageBlockExpandableBlockQuote eq:
                    pieces.Add(eq.Text);
                    if (!IsEmpty(eq.Credit)) pieces.Add(eq.Credit);
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
                case PageBlockDocument doc:
                    pieces.Add(new RichTextPlain(PlaceholderDocument));
                    CollectRichTextFromCaption(doc.Caption, pieces);
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

                // A row stays one piece (pieces are joined by '\n', and these sit
                // side by side). The buttons are carried through as RichTextButton
                // rather than flattened to their placeholder here, so a renderer
                // downstream of GetRichText can still show real buttons.
                case PageBlockButtonRow br:
                    if (br.Buttons != null && br.Buttons.Count > 0)
                    {
                        if (br.Buttons.Count == 1)
                        {
                            pieces.Add(new RichTextButton(br.Buttons[0]));
                        }
                        else
                        {
                            var row = new MutableVector<RichText>(br.Buttons.Count * 2 - 1);
                            for (int i = 0; i < br.Buttons.Count; i++)
                            {
                                if (i > 0) row.Add(new RichTextPlain(" "));
                                row.Add(new RichTextButton(br.Buttons[i]));
                            }
                            pieces.Add(new RichTexts(row));
                        }
                    }
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
                case RichTextButton b: AppendPlainText(b.Button.Text, sb); return;
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
                    {
                        // is_cached would otherwise be dropped here: it says the target
                        // already has an instant view, which is why the link is
                        // highlighted instead of underlined. Emitted as a marker over the
                        // same range so the link entity keeps its exact meaning.
                        int start = text.Length;
                        EmitSpan(b.Text, text, entities, new TextEntityTypeTextUrl(b.Url));
                        EmitCached(b.IsCached, start, text, entities);
                        return;
                    }
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
                    {
                        // Reference and AnchorLink both navigate to an in-page anchor. The
                        // server-provided URL is the standard target, so they fold into
                        // TextEntityTypeTextUrl. If the renderer needs the anchor_name
                        // separately, swap this for a dedicated entity type.
                        int start = text.Length;
                        EmitSpan(b.Text, text, entities, new TextEntityTypeTextUrl(b.Url));
                        // Always cached: the target is this very page.
                        EmitCached(true, start, text, entities);
                        return;
                    }
                case RichTextAnchorLink b:
                    {
                        int start = text.Length;
                        EmitSpan(b.Text, text, entities, new TextEntityTypeTextUrl(b.Url));
                        EmitCached(true, start, text, entities);
                        return;
                    }

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

                // The label carries the text, the entity carries the whole button, so a
                // renderer can substitute the real thing and anything that doesn't still
                // reads.
                //
                // The label goes in as PLAIN text: the button renders it itself, so an entity
                // of its own inside the button's range would only compete with the button's —
                // a run carrying both reports the inner type (GetRuns merges custom emoji over
                // everything) and the button is never substituted, and an entity narrower than
                // the label splits the range into several runs, i.e. several buttons.
                case RichTextButton button:
                    {
                        int start = text.Length;
                        AppendPlainText(button.Button.Text, text);

                        int length = text.Length - start;
                        if (length > 0)
                        {
                            entities.Add(new TextEntity(start, length, new TextEntityTypeButton(button.Button)));
                        }

                        return;
                    }

                case RichTextAnchor _:
                    // Skipped — see ObjectReplacementChar comment above.
                    return;
            }
        }

        // Lays the cached marker over the span an EmitSpan just wrote. Purely a style:
        // the link entity underneath keeps its type, so click handling is unaffected and
        // only the highlighter looks for this.
        private static void EmitCached(bool isCached, int start, StringBuilder text, IList<TextEntity> entities)
        {
            int length = text.Length - start;
            if (isCached && length > 0)
            {
                entities.Add(new TextEntity(start, length, new TextEntityTypeCached()));
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
        // TryGetFormattedText — RichMessage / InputRichMessage -> FormattedText,
        // only when the flatten is LOSSLESS (fail on any unrepresentable content)
        // =====================================================================

        /// <summary>
        /// Attempts to flatten a display <see cref="RichMessage"/> into a single
        /// <see cref="FormattedText"/> (text + inline entities), succeeding <b>only when nothing
        /// is lost</b>. Representable: paragraphs (joined by '\n'), preformatted (→ pre / pre-code),
        /// block quotes with an empty credit whose body is paragraphs (→ block-quote entity), and
        /// every inline that maps 1:1 to a message entity. Any other block (heading, footer, list,
        /// table, divider, anchor, details, collage, slideshow, cover, media, …), a block quote
        /// with a credit or non-paragraph body, or an inline with no faithful entity (icon, anchor,
        /// reference / reference-link / anchor-link) makes this return <c>false</c>.
        /// </summary>
        public static bool TryGetFormattedText(RichMessage message, out FormattedText result)
        {
            var text = new StringBuilder();
            var entities = new MutableVector<TextEntity>();
            if (TryAppendBlocks(message?.Blocks, text, entities))
            {
                result = new FormattedText(text.ToString(), entities);
                return true;
            }

            result = null;
            return false;
        }

        /// <summary>
        /// Attempts to flatten an <see cref="InputRichMessage"/> into a single
        /// <see cref="FormattedText"/> under the same lossless rules as the
        /// <see cref="TryGetFormattedText(RichMessage, out FormattedText)"/> overload. Only a
        /// <see cref="RichMessageSourceBlocks"/> source is considered — markdown / HTML sources
        /// (which would need parsing to flatten) always return <c>false</c>. <c>is_rtl</c> /
        /// <c>detect_automatic_blocks</c> are rendering/authoring hints and are ignored.
        /// </summary>
        public static bool TryGetFormattedText(InputRichMessage message, out FormattedText result)
        {
            if (message?.Source is RichMessageSourceBlocks source)
            {
                var text = new StringBuilder();
                var entities = new MutableVector<TextEntity>();
                if (TryAppendInputBlocks(source.Blocks, text, entities))
                {
                    result = new FormattedText(text.ToString(), entities);
                    return true;
                }
            }

            result = null;
            return false;
        }

        /// <summary>
        /// LOSSY flatten of a <see cref="RichMessage"/> to a plain message <see cref="FormattedText"/>,
        /// for "send without formatting": block structure is flattened to lines (paragraphs joined by
        /// '\n'; blockquotes/pre/headings become plain text), media becomes its placeholder, and inline
        /// entities that a message can't carry (subscript/superscript/marked/math/icon/button) are dropped —
        /// keeping the covered text as plain. Supported inline formatting (bold/italic/link/spoiler/…)
        /// survives. Unlike <see cref="TryGetFormattedText(RichMessage, out FormattedText)"/> it never
        /// fails. Reuses <see cref="GetRichText"/> (block tree -&gt; RichText) + <see cref="Flatten"/>.
        /// </summary>
        public static FormattedText GetFormattedText(RichMessage message)
        {
            var text = new StringBuilder();
            var entities = new MutableVector<TextEntity>();
            Flatten(GetRichText(message?.Blocks), text, entities);

            // Drop entities with no message equivalent (keep their text as plain).
            for (int i = entities.Count - 1; i >= 0; i--)
            {
                if (!IsMessageEntity(entities[i].Type))
                {
                    entities.RemoveAt(i);
                }
            }

            return new FormattedText(text.ToString(), entities);
        }

        // Entity types a message FormattedText can carry. Excludes the IV-only entities that Flatten can
        // emit (subscript/superscript/marked/mathematical-expression/icon/button) — see the supported set
        // in td_api (textEntityType*). Button is client-only on top of that: it exists nowhere in the
        // schema, so it can never leave the client.
        private static bool IsMessageEntity(TextEntityType type)
        {
            switch (type)
            {
                case TextEntityTypeSubscript:
                case TextEntityTypeSuperscript:
                case TextEntityTypeMarked:
                case TextEntityTypeMathematicalExpression:
                case TextEntityTypeIcon:
                case TextEntityTypeButton:
                    return false;
                default:
                    return true;
            }
        }

        // Top-level blocks are joined by a single '\n' (the inverse of ToPageBlocks, which splits
        // paragraphs on '\n'). Returns false the moment a block isn't losslessly representable.
        private static bool TryAppendBlocks(Vector<PageBlock> blocks, StringBuilder text, MutableVector<TextEntity> entities)
        {
            if (blocks == null)
            {
                return true;
            }

            for (int i = 0; i < blocks.Count; i++)
            {
                if (i > 0)
                {
                    text.Append('\n');
                }

                if (!TryAppendBlock(blocks[i], text, entities))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryAppendBlock(PageBlock block, StringBuilder text, MutableVector<TextEntity> entities)
        {
            switch (block)
            {
                case PageBlockParagraph p:
                    return TryAppendRichText(p.Text, text, entities);

                case PageBlockPreformatted pre:
                    {
                        int start = text.Length;
                        if (!TryAppendRichText(pre.Text, text, entities))
                        {
                            return false;
                        }

                        int length = text.Length - start;
                        if (length > 0)
                        {
                            TextEntityType type = string.IsNullOrEmpty(pre.Language)
                                ? new TextEntityTypePre()
                                : new TextEntityTypePreCode(pre.Language);
                            entities.Add(new TextEntity(start, length, type));
                        }

                        return true;
                    }

                case PageBlockBlockQuote quote:
                    {
                        // A credit can't live in a block-quote entity; the body must be paragraphs.
                        if (!IsEmpty(quote.Credit))
                        {
                            return false;
                        }

                        int start = text.Length;
                        var body = quote.Blocks;
                        if (body != null)
                        {
                            for (int i = 0; i < body.Count; i++)
                            {
                                if (body[i] is not PageBlockParagraph para)
                                {
                                    return false;
                                }

                                if (i > 0)
                                {
                                    text.Append('\n');
                                }

                                if (!TryAppendRichText(para.Text, text, entities))
                                {
                                    return false;
                                }
                            }
                        }

                        int length = text.Length - start;
                        if (length > 0)
                        {
                            entities.Add(new TextEntity(start, length, new TextEntityTypeBlockQuote()));
                        }

                        return true;
                    }

                case PageBlockExpandableBlockQuote quote:
                    {
                        // Unlike the plain block quote this one holds RichText directly, so it maps
                        // 1:1 onto the expandable-blockquote entity — no paragraph-shape requirement.
                        // A credit still can't be carried.
                        if (!IsEmpty(quote.Credit))
                        {
                            return false;
                        }

                        int start = text.Length;
                        if (!TryAppendRichText(quote.Text, text, entities))
                        {
                            return false;
                        }

                        int length = text.Length - start;
                        if (length > 0)
                        {
                            entities.Add(new TextEntity(start, length, new TextEntityTypeExpandableBlockQuote()));
                        }

                        return true;
                    }

                default:
                    // Any other block type (heading/footer/list/table/media/document/button-row/
                    // divider/anchor/…) = loss.
                    return false;
            }
        }

        private static bool TryAppendInputBlocks(Vector<InputPageBlock> blocks, StringBuilder text, MutableVector<TextEntity> entities)
        {
            if (blocks == null)
            {
                return true;
            }

            for (int i = 0; i < blocks.Count; i++)
            {
                if (i > 0)
                {
                    text.Append('\n');
                }

                if (!TryAppendInputBlock(blocks[i], text, entities))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryAppendInputBlock(InputPageBlock block, StringBuilder text, MutableVector<TextEntity> entities)
        {
            switch (block)
            {
                case InputPageBlockParagraph p:
                    return TryAppendRichText(p.Text, text, entities);

                case InputPageBlockPreformatted pre:
                    {
                        int start = text.Length;
                        if (!TryAppendRichText(pre.Text, text, entities))
                        {
                            return false;
                        }

                        int length = text.Length - start;
                        if (length > 0)
                        {
                            TextEntityType type = string.IsNullOrEmpty(pre.Language)
                                ? new TextEntityTypePre()
                                : new TextEntityTypePreCode(pre.Language);
                            entities.Add(new TextEntity(start, length, type));
                        }

                        return true;
                    }

                case InputPageBlockBlockQuote quote:
                    {
                        if (!IsEmpty(quote.Credit))
                        {
                            return false;
                        }

                        int start = text.Length;
                        var body = quote.Blocks;
                        if (body != null)
                        {
                            for (int i = 0; i < body.Count; i++)
                            {
                                if (body[i] is not InputPageBlockParagraph para)
                                {
                                    return false;
                                }

                                if (i > 0)
                                {
                                    text.Append('\n');
                                }

                                if (!TryAppendRichText(para.Text, text, entities))
                                {
                                    return false;
                                }
                            }
                        }

                        int length = text.Length - start;
                        if (length > 0)
                        {
                            entities.Add(new TextEntity(start, length, new TextEntityTypeBlockQuote()));
                        }

                        return true;
                    }

                case InputPageBlockExpandableBlockQuote quote:
                    {
                        // Holds RichText directly — see the display-side twin above.
                        if (!IsEmpty(quote.Credit))
                        {
                            return false;
                        }

                        int start = text.Length;
                        if (!TryAppendRichText(quote.Text, text, entities))
                        {
                            return false;
                        }

                        int length = text.Length - start;
                        if (length > 0)
                        {
                            entities.Add(new TextEntity(start, length, new TextEntityTypeExpandableBlockQuote()));
                        }

                        return true;
                    }

                default:
                    return false;
            }
        }

        // Strict inline flatten: like Flatten, but returns false on any RichText that can't be
        // faithfully carried by a message entity (icon, anchor, reference / reference-link /
        // anchor-link, or any unknown type — all caught by the default arm).
        private static bool TryAppendRichText(RichText richText, StringBuilder text, IList<TextEntity> entities)
        {
            switch (richText)
            {
                case null:
                    return true;

                case RichTextPlain p:
                    if (!string.IsNullOrEmpty(p.Text))
                    {
                        text.Append(p.Text);
                    }
                    return true;

                case RichTexts rs:
                    if (rs.Texts != null)
                    {
                        foreach (var child in rs.Texts)
                        {
                            if (!TryAppendRichText(child, text, entities))
                            {
                                return false;
                            }
                        }
                    }
                    return true;

                // Style wrappers
                case RichTextBold b: return TryEmitSpan(b.Text, text, entities, new TextEntityTypeBold());
                case RichTextItalic b: return TryEmitSpan(b.Text, text, entities, new TextEntityTypeItalic());
                case RichTextUnderline b: return TryEmitSpan(b.Text, text, entities, new TextEntityTypeUnderline());
                case RichTextStrikethrough b: return TryEmitSpan(b.Text, text, entities, new TextEntityTypeStrikethrough());
                case RichTextSpoiler b: return TryEmitSpan(b.Text, text, entities, new TextEntityTypeSpoiler());
                case RichTextFixed b: return TryEmitSpan(b.Text, text, entities, new TextEntityTypeCode());
                // NOTE: subscript / superscript / marked have NO message TextEntityType (they exist for IV
                // RichText only), so they're not representable — they fall through to the default (false).

                // Payload-carrying wrappers
                case RichTextUrl b: return TryEmitSpan(b.Text, text, entities, new TextEntityTypeTextUrl(b.Url));
                case RichTextEmailAddress b: return TryEmitSpan(b.Text, text, entities, new TextEntityTypeEmailAddress());
                case RichTextPhoneNumber b: return TryEmitSpan(b.Text, text, entities, new TextEntityTypePhoneNumber());
                case RichTextMentionName b: return TryEmitSpan(b.Text, text, entities, new TextEntityTypeMentionName(b.UserId));
                case RichTextDateTime b: return TryEmitSpan(b.Text, text, entities, new TextEntityTypeDateTime(b.UnixTime, b.FormattingType));

                // Auto-detected (the text is the value)
                case RichTextMention b: return TryEmitSpan(b.Text, text, entities, new TextEntityTypeMention());
                case RichTextHashtag b: return TryEmitSpan(b.Text, text, entities, new TextEntityTypeHashtag());
                case RichTextCashtag b: return TryEmitSpan(b.Text, text, entities, new TextEntityTypeCashtag());
                case RichTextBotCommand b: return TryEmitSpan(b.Text, text, entities, new TextEntityTypeBotCommand());
                case RichTextBankCardNumber b: return TryEmitSpan(b.Text, text, entities, new TextEntityTypeBankCardNumber());

                // Leaves carrying their own text content
                case RichTextCustomEmoji ce:
                    if (!string.IsNullOrEmpty(ce.AlternativeText))
                    {
                        int start = text.Length;
                        text.Append(ce.AlternativeText);
                        entities.Add(new TextEntity(start, ce.AlternativeText.Length, new TextEntityTypeCustomEmoji(ce.CustomEmojiId)));
                    }
                    return true;

                // NOTE: mathematical expressions have no message TextEntityType either (IV-only), so
                // inline math is not representable — it falls through to the default (false).

                default:
                    // richTextIcon / richTextAnchor / richTextReference / richTextReferenceLink /
                    // richTextAnchorLink / richTextButton (and any unknown) can't be represented
                    // without loss. A button especially so: a message carries no way to act on it.
                    return false;
            }
        }

        private static bool TryEmitSpan(RichText inner, StringBuilder text, IList<TextEntity> entities, TextEntityType type)
        {
            int start = text.Length;
            if (!TryAppendRichText(inner, text, entities))
            {
                return false;
            }

            int length = text.Length - start;
            if (length > 0)
            {
                entities.Add(new TextEntity(start, length, type));
            }

            return true;
        }

        // =====================================================================
        // ToPageBlocks — FormattedText (message text + entities) -> page blocks
        // =====================================================================

        /// <summary>
        /// Converts a <see cref="FormattedText"/> (a message's text + entities) into a
        /// list of <see cref="PageBlock"/>, preserving every entity — the inverse of
        /// <see cref="Flatten"/>. Block-level entities become their own blocks: block
        /// quotes → <see cref="PageBlockBlockQuote"/>, expandable ones →
        /// <see cref="PageBlockExpandableBlockQuote"/>, pre / pre-code
        /// → <see cref="PageBlockPreformatted"/>. Everything else becomes a
        /// <see cref="PageBlockParagraph"/>, one per line (the text is split on every
        /// '\n', inside block quotes too; empty lines are dropped). Inline entities are
        /// mapped to their <c>richText*</c> equivalents, with nested/overlapping ranges
        /// resolved into a nested <see cref="RichText"/> tree.
        /// </summary>
        public static Vector<PageBlock> ToPageBlocks(FormattedText text)
        {
            var blocks = new MutableVector<PageBlock>();
            var s = text?.Text ?? string.Empty;
            var entities = text?.Entities ?? new MutableVector<TextEntity>();

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
                    case TextEntityTypeExpandableBlockQuote _:
                        // pageBlockExpandableBlockQuote holds RichText directly, so the span
                        // stays one node (newlines and all) instead of being split into
                        // paragraphs — which makes this the exact inverse of TryAppendBlock.
                        blocks.Add(new PageBlockExpandableBlockQuote(
                            BuildRichText(s, be.Offset, end, inline), new RichTextPlain(string.Empty)));
                        break;
                    default: // BlockQuote
                        var inner = new MutableVector<PageBlock>();
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

            var parts = new MutableVector<RichText>();
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
        /// identically — every rendered property is compared. (Media is compared by its
        /// scalar attributes + caption + the media file id / Url, so a block whose media
        /// is swapped to a different file is treated as changed.)
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
                (PageBlockExpandableBlockQuote a, PageBlockExpandableBlockQuote b) => Compare(a.Text, b.Text) && Compare(a.Credit, b.Credit),
                (PageBlockMathematicalExpression a, PageBlockMathematicalExpression b) => string.Equals(a.Expression, b.Expression),

                // Atomic
                (PageBlockAnchor a, PageBlockAnchor b) => a.Name == b.Name,
                (PageBlockDivider, PageBlockDivider) => true,
                (PageBlockUnsupported, PageBlockUnsupported) => true,
                (PageBlockChatLink a, PageBlockChatLink b) => a.Title == b.Title && a.Username == b.Username && a.AccentColorId == b.AccentColorId,

                // Media + caption (scalar attributes + caption)
                (PageBlockAnimation a, PageBlockAnimation b) => a.NeedAutoplay == b.NeedAutoplay && a.HasSpoiler == b.HasSpoiler && SameFile(a.Animation?.AnimationValue, b.Animation?.AnimationValue) && Compare(a.Caption, b.Caption),
                (PageBlockAudio a, PageBlockAudio b) => SameFile(a.Audio.AudioValue, b.Audio.AudioValue) && Compare(a.Caption, b.Caption),
                (PageBlockPhoto a, PageBlockPhoto b) => string.Equals(a.Url, b.Url) && a.HasSpoiler == b.HasSpoiler && SamePhoto(a.Photo, b.Photo) && Compare(a.Caption, b.Caption),
                (PageBlockVideo a, PageBlockVideo b) => a.NeedAutoplay == b.NeedAutoplay && a.IsLooped == b.IsLooped && a.HasSpoiler == b.HasSpoiler && SameFile(a.Video?.VideoValue, b.Video?.VideoValue) && Compare(a.Caption, b.Caption),
                (PageBlockVoiceNote a, PageBlockVoiceNote b) => SameFile(a.VoiceNote.Voice, b.VoiceNote.Voice) && Compare(a.Caption, b.Caption),
                (PageBlockDocument a, PageBlockDocument b) => string.Equals(a.Document.FileName, b.Document.FileName) && SameFile(a.Document.DocumentValue, b.Document.DocumentValue) && Compare(a.Caption, b.Caption),
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
                (PageBlockButtonRow a, PageBlockButtonRow b) => SameType(a.Align, b.Align) && Compare(a.Buttons, b.Buttons),
                (PageBlockTable a, PageBlockTable b) => a.IsBordered == b.IsBordered && a.IsStriped == b.IsStriped && a.IsCompact == b.IsCompact && Compare(a.Caption, b.Caption) && Compare(a.Cells, b.Cells),

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
                case (RichTextButton a, RichTextButton b): return Compare(a.Button, b.Button);
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

        private static bool Compare(InlineButton a, InlineButton b)
        {
            if (a == null || b == null)
            {
                return a == b;
            }

            return SameType(a.Style, b.Style) && SameButtonType(a.Type, b.Type) && Compare(a.Text, b.Text);
        }

        private static bool Compare(Vector<InlineButton> a, Vector<InlineButton> b)
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

        // Two buttons that look identical but act differently must still compare unequal,
        // so the payload matters, not just the variant. Parameterless variants
        // (callbackGame/buy/disabled) are settled by SameType in the default arm.
        private static bool SameButtonType(InlineKeyboardButtonType x, InlineKeyboardButtonType y)
        {
            switch (x, y)
            {
                case (InlineKeyboardButtonTypeUrl a, InlineKeyboardButtonTypeUrl b): return string.Equals(a.Url, b.Url);
                case (InlineKeyboardButtonTypeWebApp a, InlineKeyboardButtonTypeWebApp b): return string.Equals(a.Url, b.Url);
                case (InlineKeyboardButtonTypeLoginUrl a, InlineKeyboardButtonTypeLoginUrl b): return a.Id == b.Id && string.Equals(a.Url, b.Url) && string.Equals(a.ForwardText, b.ForwardText);
                case (InlineKeyboardButtonTypeCallback a, InlineKeyboardButtonTypeCallback b): return SameBytes(a.Data, b.Data);
                case (InlineKeyboardButtonTypeCallbackWithPassword a, InlineKeyboardButtonTypeCallbackWithPassword b): return SameBytes(a.Data, b.Data);
                case (InlineKeyboardButtonTypeSwitchInline a, InlineKeyboardButtonTypeSwitchInline b): return string.Equals(a.Query, b.Query) && SameType(a.TargetChat, b.TargetChat);
                case (InlineKeyboardButtonTypeUser a, InlineKeyboardButtonTypeUser b): return a.UserId == b.UserId;
                case (InlineKeyboardButtonTypeCopyText a, InlineKeyboardButtonTypeCopyText b): return string.Equals(a.Text, b.Text);
                default: return SameType(x, y);
            }
        }

        private static bool SameBytes(Vector<byte> a, Vector<byte> b)
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
                if (a[i] != b[i])
                {
                    return false;
                }
            }
            return true;
        }

        private static bool Compare(PageBlockCaption a, PageBlockCaption b)
        {
            if (a == null || b == null)
            {
                return a == b;
            }

            return Compare(a.Credit, b.Credit) && Compare(a.Text, b.Text);
        }

        private static bool Compare(Vector<PageBlock> a, Vector<PageBlock> b)
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

        private static bool Compare(Vector<PageBlockListItem> a, Vector<PageBlockListItem> b)
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

        private static bool Compare(Vector<PageBlockTableCell> a, Vector<PageBlockTableCell> b)
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

        private static bool Compare(Vector<Vector<PageBlockTableCell>> a, Vector<Vector<PageBlockTableCell>> b)
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

        private static bool SameFile(File a, File b)
        {
            if (a == null || b == null)
            {
                return a == b;
            }

            return a.Id == b.Id;
        }

        private static bool SamePhoto(Photo a, Photo b)
        {
            if (a == null || b == null)
            {
                return a == b;
            }

            if (a.Sizes.Count != b.Sizes.Count)
            {
                return false;
            }

            for (int i = 0; i < a.Sizes.Count; i++)
            {
                if (!SameFile(a.Sizes[i].Photo, b.Sizes[i].Photo))
                {
                    return false;
                }
            }

            return true;
        }

        // =====================================================================
        // ToInputRichMessage — RichMessage (display) -> InputRichMessage (sendable)
        // =====================================================================

        /// <summary>
        /// Converts a display <see cref="RichMessage"/> (blocks with resolved media objects,
        /// e.g. as returned by getMessageRichMessage) into a sendable <see cref="InputRichMessage"/>:
        /// each <see cref="PageBlock"/> becomes its <c>inputPageBlock*</c> equivalent (media is
        /// rebuilt into <c>input*</c> media, preferring remote file ids), wrapped in a
        /// <see cref="RichMessageSourceBlocks"/>. Blocks with no input equivalent
        /// (title/subtitle/kicker/authorDate/header/subheader/embedded/embeddedPost/chatLink/
        /// relatedArticles/…) are dropped; a cover unwraps to the block it covers.
        /// </summary>
        public static InputRichMessage ToInputRichMessage(RichMessage message, bool detectAutomaticBlocks = false)
        {
            var blocks = ToInputBlocks(message?.Blocks);
            return new InputRichMessage(new RichMessageSourceBlocks(blocks), message?.IsRtl ?? false, detectAutomaticBlocks);
        }

        /// <summary>Converts a list of display <see cref="PageBlock"/> into their <c>inputPageBlock*</c> equivalents (unsupported blocks dropped). The returned list is owned by the caller.</summary>
        public static Vector<InputPageBlock> ToInputBlocks(Vector<PageBlock> blocks)
        {
            var result = new MutableVector<InputPageBlock>();
            if (blocks != null)
            {
                foreach (var block in blocks)
                {
                    var input = BlockToInput(block);
                    if (input != null)
                    {
                        result.Add(input);
                    }
                }
            }

            return result;
        }

        private static InputPageBlock BlockToInput(PageBlock block)
        {
            switch (block)
            {
                case PageBlockSectionHeading x:
                    return new InputPageBlockSectionHeading(x.Text, x.Size);
                case PageBlockParagraph x:
                    return new InputPageBlockParagraph(x.Text);
                case PageBlockPreformatted x:
                    return new InputPageBlockPreformatted(x.Text, x.Language ?? string.Empty);
                case PageBlockFooter x:
                    return new InputPageBlockFooter(x.Footer);
                //case PageBlockThinking x:
                //    return new InputPageBlockThinking(x.Text);
                case PageBlockDivider:
                    return new InputPageBlockDivider();
                case PageBlockMathematicalExpression x:
                    return new InputPageBlockMathematicalExpression(x.Expression);
                case PageBlockAnchor x:
                    return new InputPageBlockAnchor(x.Name);
                case PageBlockList x:
                    return new InputPageBlockList(ToInputListItems(x.Items));
                case PageBlockBlockQuote x:
                    return new InputPageBlockBlockQuote(ToInputBlocks(x.Blocks), x.Credit);
                case PageBlockPullQuote x:
                    return new InputPageBlockPullQuote(x.Text, x.Credit);
                case PageBlockExpandableBlockQuote x:
                    return new InputPageBlockExpandableBlockQuote(x.Text, x.Credit);
                case PageBlockDocument x:
                    return new InputPageBlockDocument(ToInputDocument(x.Document), x.Caption);
                case PageBlockButtonRow x:
                    // inputPageBlockButtonRow takes inlineButton, not an input twin — the
                    // buttons carry across unchanged.
                    return new InputPageBlockButtonRow(x.Buttons, x.Align);
                case PageBlockAnimation x:
                    return new InputPageBlockAnimation(ToInputAnimation(x.Animation), x.Caption, x.HasSpoiler);
                case PageBlockAudio x:
                    return new InputPageBlockAudio(ToInputAudio(x.Audio), x.Caption);
                case PageBlockPhoto x:
                    return new InputPageBlockPhoto(ToInputPhoto(x.Photo), x.Caption, x.HasSpoiler);
                case PageBlockVideo x:
                    return new InputPageBlockVideo(ToInputVideo(x.Video), x.Caption, x.HasSpoiler);
                case PageBlockVoiceNote x:
                    return new InputPageBlockVoiceNote(ToInputVoiceNote(x.VoiceNote), x.Caption);
                case PageBlockCollage x:
                    return new InputPageBlockCollage(ToInputBlocks(x.Blocks), x.Caption);
                case PageBlockSlideshow x:
                    return new InputPageBlockSlideshow(ToInputBlocks(x.Blocks), x.Caption);
                case PageBlockTable x:
                    return new InputPageBlockTable(x.Caption, x.Cells, x.IsBordered, x.IsStriped, x.IsCompact);
                case PageBlockDetails x:
                    return new InputPageBlockDetails(x.Header, ToInputBlocks(x.Blocks), x.IsOpen);
                case PageBlockMap x:
                    return new InputPageBlockMap(x.Location, x.Zoom, x.Width, x.Height, x.Caption);
                case PageBlockCover x:
                    // No inputPageBlockCover — unwrap to the covered block.
                    return BlockToInput(x.Cover);
                default:
                    // No input equivalent (title/subtitle/kicker/authorDate/header/subheader/
                    // embedded/embeddedPost/chatLink/relatedArticles/subscribe/anchor-only/...).
                    return null;
            }
        }

        private static Vector<InputPageBlockListItem> ToInputListItems(Vector<PageBlockListItem> items)
        {
            var result = new MutableVector<InputPageBlockListItem>();
            if (items != null)
            {
                foreach (var item in items)
                {
                    result.Add(new InputPageBlockListItem(ToInputBlocks(item.Blocks), item.HasCheckbox, item.IsChecked, item.Value, item.Type ?? string.Empty));
                }
            }

            return result;
        }

        // --- display media -> input media ---------------------------------------
        // Prefer the persistent remote id (survives beyond the session), else the local id.
        private static InputFile ToInputFile(File file)
        {
            if (file == null)
            {
                return null;
            }

            if (file.Remote != null && !string.IsNullOrEmpty(file.Remote.Id))
            {
                return new InputFileRemote(file.Remote.Id);
            }

            return new InputFileId(file.Id);
        }

        private static InputThumbnail ToInputThumbnail(Thumbnail thumbnail)
        {
            if (thumbnail?.File == null)
            {
                return null;
            }

            return new InputThumbnail(ToInputFile(thumbnail.File), thumbnail.Width, thumbnail.Height);
        }

        private static InputPhoto ToInputPhoto(Photo photo)
        {
            if (photo?.Sizes == null || photo.Sizes.Count == 0)
            {
                return null;
            }

            // First size = thumbnail, last = main (largest); no separate thumbnail when there's only one.
            var main = photo.Sizes[photo.Sizes.Count - 1];
            var thumb = photo.Sizes[0];
            var thumbnail = thumb != main
                ? new InputThumbnail(ToInputFile(thumb.Photo), thumb.Width, thumb.Height)
                : null;

            return new InputPhoto(ToInputFile(main.Photo), thumbnail, null, Array.Empty<int>(), main.Width, main.Height);
        }

        private static InputVideo ToInputVideo(Video video)
        {
            if (video == null)
            {
                return null;
            }

            return new InputVideo(ToInputFile(video.VideoValue), ToInputThumbnail(video.Thumbnail), null, 0,
                Array.Empty<int>(), video.Duration, video.Width, video.Height, video.SupportsStreaming);
        }

        private static InputAnimation ToInputAnimation(Animation animation)
        {
            if (animation == null)
            {
                return null;
            }

            return new InputAnimation(ToInputFile(animation.AnimationValue), ToInputThumbnail(animation.Thumbnail),
                Array.Empty<int>(), animation.Duration, animation.Width, animation.Height);
        }

        private static InputAudio ToInputAudio(Audio audio)
        {
            return new InputAudio(ToInputFile(audio.AudioValue), ToInputThumbnail(audio.AlbumCoverThumbnail),
                audio.Duration, audio.Title, audio.Performer);
        }

        private static InputDocument ToInputDocument(Document document)
        {
            // The server already typed this file, so there's nothing to re-detect.
            return new InputDocument(ToInputFile(document.DocumentValue), ToInputThumbnail(document.Thumbnail), false);
        }

        private static InputVoiceNote ToInputVoiceNote(VoiceNote voice)
        {
            return new InputVoiceNote(ToInputFile(voice.Voice), voice.Duration, voice.Waveform);
        }
    }
}
