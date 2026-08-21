//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    /// <summary>
    /// How a <see cref="FormattedTextBlock"/> participates in text selection.
    /// </summary>
    public enum TextSelectionMode
    {
        /// <summary>Native RichTextBlock selection (own I-beam); excluded from TextSelectionManager.</summary>
        Enabled,
        /// <summary>No selection at all; excluded from TextSelectionManager.</summary>
        Disabled,
        /// <summary>Native selection off; selected by TextSelectionManager (cross-block), I-beam driven manually.</summary>
        Extended
    }

    // ISelectableControl implementation for the cross-block read-view selection
    // (TextSelectionManager). Selection is rendered as a TextHighlighter rather than
    // the native (focus-bound) selection, so it composes with the other blocks.
    //
    // Indices are in TextHighlighter.Ranges space: GetPositionFromPoint resolves a
    // point to a TextPointer and GetHighlightIndex maps it there. That space counts
    // only content units — characters in Runs, plus 1 per line break — and does NOT
    // count paragraph breaks (the shift correction in SetText exists for exactly that
    // reason). An inline object (custom emoji, image, math) counts 0: the ZWNJ that
    // SetText always emits next to one is the unit that stands in for it. Since indices sit
    // between characters, the object's leading edge is the index of the zero-width character
    // in FRONT of it, and that is the index its source text is mapped to (see MapObject) —
    // only an object with no such character has to be addressed by its own ZWNJ. The ZWNJ
    // workaround characters are real Run chars, so they're counted and highlighting them is
    // harmless (zero width). Copy, which needs FormattedText/StyledText offsets, is a
    // separate layer.
    public partial class FormattedTextBlock : ISelectableControl
    {
        private TextHighlighter _selection;

        #region TextSelection

        private TextSelectionMode _textSelection = TextSelectionMode.Enabled;
        public TextSelectionMode TextSelection
        {
            get => (TextSelectionMode)GetValue(TextSelectionProperty);
            set => SetValue(TextSelectionProperty, value);
        }

        public static readonly DependencyProperty TextSelectionProperty =
            DependencyProperty.Register("TextSelection", typeof(TextSelectionMode), typeof(FormattedTextBlock), new PropertyMetadata(TextSelectionMode.Enabled, OnTextSelectionChanged));

        private static void OnTextSelectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = (FormattedTextBlock)d;

            sender._textSelection = (TextSelectionMode)e.NewValue;

            if (sender.TextBlock != null)
            {
                // Only native (Enabled) mode lets the inner control select on its own;
                // Disabled/Extended keep it off (Extended is driven by TextSelectionManager).
                sender.TextBlock.IsTextSelectionEnabled = sender._textSelection == TextSelectionMode.Enabled;
            }
        }

        // Back-compat shim over TextSelection: true == Enabled (native), false == Disabled.
        // Existing callers/XAML keep working; use TextSelection directly for Extended.
        public bool IsTextSelectionEnabled
        {
            get => TextSelection == TextSelectionMode.Enabled;
            set => TextSelection = value ? TextSelectionMode.Enabled : TextSelectionMode.Disabled;
        }

        #endregion

        // Only Extended blocks are collected by TextSelectionManager.
        public bool IsSelectionEnabled => _textSelection == TextSelectionMode.Extended;

        // Cached: GetHighlightIndex(ContentEnd) walks the whole inline tree, and ContentLength
        // is hit on every Select() during a drag — for a syntax-highlighted code block (a deep
        // span tree) that re-walk per pointer move is the selection lag. Invalidated (-1) by
        // SetText and when ProcessCodeBlock rebuilds the inlines.
        private int _contentLength = -1;
        public int ContentLength
        {
            get
            {
                if (_contentLength < 0 && TextBlock != null)
                {
                    _contentLength = GetHighlightIndex(TextBlock.ContentEnd, out _, out _);
                }

                return _contentLength < 0 ? 0 : _contentLength;
            }
        }

        internal void InvalidateContentLength()
        {
            _contentLength = -1;
        }

        // The hit token handed back to GetSelectionBoundary is the StyledText paragraph the
        // point landed in, SelectionHit.None when it can't be told, or:
        private const int HitNothing = -2; // an empty line: nothing to expand to

        public int GetPositionFromPoint(Point point, out int hit)
        {
            hit = SelectionHit.None;

            if (TextBlock == null)
            {
                return 0;
            }

            var pointer = TextBlock.GetPositionFromPoint(point);
            if (pointer == null)
            {
                return 0;
            }

            var position = GetHighlightIndex(pointer, out var block, out var mark);

            // The pointer tells the two sides of a custom emoji apart, but the index can't: the
            // object takes none of its own, so the position in front of it and the one past it
            // both count the same characters. `mark` is the zero-width character SetText emits
            // ahead of the object, whose index is the one that addresses its leading edge - so
            // a pointer that stopped in front of the object belongs there. Without this a
            // leading custom emoji can only ever be selected together with what follows it.
            if (mark != null)
            {
                position--;
            }

            // An empty paragraph holds no inline, so a point on that line resolves BETWEEN
            // blocks and parents to the RichTextBlock rather than to a run. There's nothing on
            // the line to expand to, and the index it flattens to is the one the NEXT paragraph
            // starts at — which is why a double tap on a blank line selected the following
            // line's first word. The end of the text parents to the RichTextBlock too, but it
            // does sit in a paragraph, so keep the position-derived answer there.
            if (pointer.Parent is RichTextBlock)
            {
                if (pointer.Offset < TextBlock.ContentEnd.Offset)
                {
                    hit = HitNothing;
                }
            }
            else if (block >= 0)
            {
                hit = _first + block;
            }

            return position;
        }

        public void Select(int start, int end)
        {
            if (TextBlock == null || end <= start)
            {
                ClearSelection();
                return;
            }

            // RichTextBlock does NOT repaint when an already-added highlighter's Ranges
            // change, so remove (if present) and re-add the same instance to force it.
            if (_selection != null)
            {
                TextBlock.TextHighlighters.Remove(_selection);
                _selection.Ranges.Clear();
            }
            else
            {
                _selection = new TextHighlighter
                {
                    Background = TextBlock.SelectionHighlightColor,
                    Foreground = new SolidColorBrush(Colors.White)
                };
            }

            // A "to end" selection (full block, or the first block of a multi-block
            // range) must cover the trailing ZWNJ workaround chars, which sit past
            // ContentLength. Overshoot and let the control clamp to the real end.
            var length = end >= ContentLength ? int.MaxValue - start : end - start;
            _selection.Ranges.Add(new TextRange { StartIndex = start, Length = length });
            TextBlock.TextHighlighters.Add(_selection);
        }

        public void ClearSelection()
        {
            if (_selection == null)
            {
                return;
            }

            // Remove unconditionally (cheaper than Contains); drop the reference so a
            // later Select rebuilds it.
            TextBlock?.TextHighlighters.Remove(_selection);
            _selection = null;
        }

        // The selection over [start, end) (rendered indices) as a standalone slice of
        // the StyledText. Converts the rendered range to StyledText.Text offsets via the
        // map SetText built (_indexMap), then slices — so copy reflects exactly what's
        // shown (and doesn't need the original FormattedText).
        public FormattedText GetSelectedText(int start, int end)
        {
            if (_text == null || end <= start)
            {
                return null;
            }

            var from = RenderedToStyled(start);
            var to = RenderedToStyled(end);
            return to > from ? _text.Substring(from, to - from) : null;
        }

        // A rendered position -> its absolute offset in the SOURCE text (StyledText.Text,
        // which for a message equals the original FormattedText.Text — no virtual breaks).
        public int GetSourceOffset(int position)
        {
            return _text != null ? RenderedToStyled(position) : 0;
        }

        // The SOURCE text over absolute [from, to). Slices the shared StyledText, so any block
        // of a message can return the whole message's range (all blocks share one StyledText).
        public FormattedText GetSourceText(int from, int to)
        {
            return _text != null && to > from ? _text.Substring(from, to - from) : null;
        }

        // Expand a rendered position to its word/paragraph, returned in rendered indices.
        // Works in StyledText space (the real characters, no injected ZWNJ/marks) so the
        // ProcessCodeBlock span tree and emoji workarounds don't perturb boundaries, then
        // maps back. A word never crosses a line, so it's clamped to the containing
        // StyledParagraph — which is also the Paragraph-granularity answer.
        public void GetSelectionBoundary(int position, int hit, TextSelectionGranularity granularity, out int start, out int end)
        {
            start = end = position;

            if (granularity == TextSelectionGranularity.Character || _text == null || TextBlock == null || hit == HitNothing)
            {
                return;
            }

            var offset = RenderedToStyled(position);

            int lo, hi;
            if (hit >= 0 && hit < _text.Paragraphs.Count)
            {
                // The paragraph the point landed in, which the position alone can't give: a
                // paragraph break takes no rendered unit, so the end of a line and the start of
                // the next are the same index, and deriving it resolves to the latter — that is
                // how a double tap past a line's last word selected the next line's first.
                var paragraph = _text.Paragraphs[hit];
                lo = paragraph.Offset;
                hi = paragraph.Offset + paragraph.Length;
            }
            else if (!ParagraphRange(offset, out lo, out hi))
            {
                return;
            }

            if (hi <= lo)
            {
                return;
            }

            if (granularity == TextSelectionGranularity.Paragraph)
            {
                start = StyledToRendered(lo);
                end = StyledToRendered(hi);
                return;
            }

            // Word: expand over the run of the same class around the hit char. Mirrors the
            // native SelectionWordBreaker — a contiguous run of punctuation/symbols is itself
            // a "word", whitespace is its own run, everything else is word characters.
            var text = _text.Text;
            var i = Math.Min(Math.Max(offset, lo), hi - 1);
            var cls = Classify(text[i]);

            var s = i;
            var e = i + 1;
            while (s > lo && Classify(text[s - 1]) == cls) s--;
            while (e < hi && Classify(text[e]) == cls) e++;

            start = StyledToRendered(s);
            end = StyledToRendered(e);
        }

        // [lo, hi) absolute StyledText offsets of the rendered paragraph containing (or
        // nearest at/before) the given offset, within this block's [_first, _last] range.
        private bool ParagraphRange(int offset, out int lo, out int hi)
        {
            lo = hi = 0;

            var paragraphs = _text.Paragraphs;
            var found = false;

            for (int i = _first; i <= _last && i < paragraphs.Count; i++)
            {
                var paragraph = paragraphs[i];
                if (!found || paragraph.Offset <= offset)
                {
                    lo = paragraph.Offset;
                    hi = paragraph.Offset + paragraph.Length;
                    found = true;
                }

                if (offset >= paragraph.Offset && offset < paragraph.Offset + paragraph.Length)
                {
                    break;
                }
            }

            return found;
        }

        private enum CharClass { Word, Punctuation, Space }

        private static CharClass Classify(char c)
        {
            if (char.IsWhiteSpace(c)) return CharClass.Space;
            if (char.IsPunctuation(c) || char.IsSymbol(c)) return CharClass.Punctuation;
            return CharClass.Word;
        }

        // Rendered/highlighter index -> StyledText.Text offset, via _indexMap (built by
        // SetText). Segments are rendered-contiguous; text segments are linear, while
        // emoji/date segments differ in length and snap to their start/end. A null map
        // (plain single run / fast path) is 1:1 from _origin — which is NOT 0 when the block
        // renders a middle paragraph of a shared StyledText.
        private int RenderedToStyled(int rendered)
        {
            var map = _indexMap;
            if (map == null || map.Count == 0)
            {
                return rendered + _origin;
            }

            var styledEnd = 0;
            foreach (var seg in map)
            {
                if (rendered <= seg.Rendered)
                {
                    return seg.Styled; // at/before this segment's start (covers paragraph jumps)
                }

                if (rendered < seg.Rendered + seg.RenderedLength)
                {
                    // within the segment
                    return seg.RenderedLength == seg.StyledLength
                        ? seg.Styled + (rendered - seg.Rendered)        // text: linear
                        : seg.Styled;                                   // emoji/date: snap to start
                }

                styledEnd = seg.Styled + seg.StyledLength;
            }

            return styledEnd; // past the last segment
        }

        // Inverse of RenderedToStyled: an absolute StyledText.Text offset -> this block's
        // rendered/highlighter index, via _indexMap. A null map is the fast path (plain,
        // single paragraph), where the block's own paragraph starts at _origin. Used to place
        // the search-query highlight.
        private int StyledToRendered(int styled)
        {
            var map = _indexMap;
            if (map == null || map.Count == 0)
            {
                return Math.Max(0, styled - _origin);
            }

            var renderedEnd = 0;
            foreach (var seg in map)
            {
                if (styled <= seg.Styled)
                {
                    return seg.Rendered; // at/before this segment's start
                }

                if (styled < seg.Styled + seg.StyledLength)
                {
                    return seg.RenderedLength == seg.StyledLength
                        ? seg.Rendered + (styled - seg.Styled)   // text: linear
                        : seg.Rendered;                          // emoji/date: snap to start
                }

                renderedEnd = seg.Rendered + seg.RenderedLength;
            }

            return renderedEnd; // past the last segment
        }

        // Maps a TextPointer to the TextHighlighter.Ranges index by walking the inline
        // tree and counting content units up to the pointer. Paragraph breaks are NOT
        // counted (that's what the SetText 'shift' compensates for); inline objects and
        // line breaks count as 1, Run characters as their length.
        //
        // `block` is the index of the block the pointer resolved in (-1 when it resolved in
        // none), which the returned index can't express — blocks are laid out one per
        // rendered paragraph, so it maps to StyledText as _first + block.
        //
        // `mark` is the zero-width run in front of the inline object the pointer stopped in
        // front of, null in every other case: its index is the one that addresses the object's
        // leading edge.
        private int GetHighlightIndex(TextPointer pointer, out int block, out Run mark)
        {
            block = -1;
            mark = null;

            if (TextBlock == null || pointer == null)
            {
                return 0;
            }

            var target = pointer.Offset;
            var index = 0;
            var i = 0;

            foreach (var current in TextBlock.Blocks)
            {
                if (current is Paragraph paragraph && WalkInlines(paragraph.Inlines, target, ref index, ref mark))
                {
                    block = i;
                    return index;
                }

                i++;
            }

            return index;
        }

        // Walks inlines accumulating the content-unit count in `index`. Returns true
        // once the pointer (`target` offset) is reached, leaving `index` at it.
        //
        // A pointer stopping in front of an inline object also reports the zero-width run
        // SetText emitted ahead of that object in `mark` (an object opening a paragraph, one
        // after a spoiler, and the ZWNJ trailing the object right before all qualify) — the
        // index it lands on is the one PAST the object, since the object counts nothing, and
        // the mark's is the one in front of it. Null with no such run: the object then has no
        // index of its own and can only be addressed along with the character before it.
        private static bool WalkInlines(InlineCollection inlines, int target, ref int index, ref Run mark)
        {
            Inline previous = null;
            var total = inlines.Count;

            for (int i = 0; i < total; i++)
            {
                var inline = inlines[i];

                if (inline is InlineUIContainer)
                {
                    // The object counts nothing, so `index` is already the position past it -
                    // anything short of its very end resolved in front of it.
                    if (target < inline.ElementEnd.Offset)
                    {
                        mark = ZeroWidth(previous);
                        return true;
                    }

                    previous = inline;
                    continue;
                }

                // Pointer is before this element: we're done, `index` is already correct.
                if (target <= inline.ElementStart.Offset)
                {
                    return true;
                }

                switch (inline)
                {
                    case Run run:
                        if (target <= run.ContentEnd.Offset)
                        {
                            // Within the run: add the characters before the pointer.
                            var count = target - run.ContentStart.Offset;
                            index += count;

                            // Past its start with an object right after it: the pointer stopped
                            // in front of that object, not inside this run's text.
                            if (count > 0 && i + 1 < total && inlines[i + 1] is InlineUIContainer)
                            {
                                mark = ZeroWidth(run);
                            }

                            return true;
                        }
                        index += run.Text != null ? run.Text.Length : 0;
                        break;
                    case Span span: // Bold/Italic/Underline/Hyperlink/... derive from Span
                        if (WalkInlines(span.Inlines, target, ref index, ref mark))
                        {
                            return true;
                        }
                        break;
                    case LineBreak:
                        index += 1; // one object-replacement / break unit
                        break;
                }

                previous = inline;
            }

            return false;
        }

        // The run as one of the zero-width characters SetText emits around an inline object
        // (the ZWNJ that stands in for it, or the RTL/LTR mark opening a paragraph), else null.
        private static Run ZeroWidth(Inline inline)
        {
            return inline is Run run && run.Text is Icons.ZWNJ or Icons.LTR or Icons.RTL ? run : null;
        }
    }
}
