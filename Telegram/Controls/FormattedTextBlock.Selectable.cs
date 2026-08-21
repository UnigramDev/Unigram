//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Windows.UI;
using Windows.UI.Xaml;
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
                    var offsets = Offsets;
                    var last = offsets.Length - 1;

                    _contentLength = last < 0 ? 0 : offsets[last].Rendered + offsets[last].Length;
                }

                return _contentLength < 0 ? 0 : _contentLength;
            }
        }

        internal void InvalidateContentLength()
        {
            _contentLength = -1;
            _offsets = null;
            _blockRanges = null;
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

            var position = GetHighlightIndex(pointer.Offset, out var block, out var mark);

            // The pointer tells the two sides of a custom emoji apart, but the index can't: the
            // object takes none of its own, so the position in front of it and the one past it
            // both count the same characters. `mark` says the pointer stopped in front of an
            // object that has one of the zero-width characters SetText emits ahead of it, and
            // that character's index is the one addressing its leading edge. Without this a
            // leading custom emoji can only ever be selected together with what follows it.
            if (mark)
            {
                position--;
            }

            // An empty paragraph holds no inline, so a point on that line resolves BETWEEN
            // blocks rather than inside one. There's nothing on the line to expand to, and the
            // index it flattens to is the one the NEXT paragraph starts at — which is why a
            // double tap on a blank line selected the following line's first word. The end of
            // the text lands between blocks too, but it does sit in a paragraph, so keep the
            // position-derived answer there.
            if (IsBetweenBlocks(pointer.Offset))
            {
                if (pointer.Offset < _contentEnd)
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
        // One entry per leaf inline (Run, inline object, LineBreak) in document order: where it
        // begins in TextPointer space, and the highlighter index standing before it. Spans are
        // not recorded - they carry no content unit, so a position at a span's start resolves to
        // the same index as one at its first child's.
        private readonly struct OffsetEntry
        {
            public readonly int Start;         // ElementStart.Offset
            public readonly int ContentStart;  // runs only
            public readonly int ContentEnd;    // runs only
            public readonly int End;           // ElementEnd.Offset
            public readonly int Rendered;      // highlighter index before this element
            public readonly int Length;        // content units this element contributes
            public readonly int Block;         // index of the containing Block
            public readonly bool Object;       // an InlineUIContainer, which counts nothing
            public readonly bool Mark;         // a Run holding one of the zero-width characters
            public readonly bool Link;         // sits inside a Hyperlink

            public OffsetEntry(int start, int contentStart, int contentEnd, int end, int rendered, int length, int block, bool inlineObject, bool mark, bool link)
            {
                Start = start;
                ContentStart = contentStart;
                ContentEnd = contentEnd;
                End = end;
                Rendered = rendered;
                Length = length;
                Block = block;
                Object = inlineObject;
                Mark = mark;
                Link = link;
            }
        }

        private OffsetEntry[] _offsets;

        // Where each Block begins and ends in TextPointer space. A position outside every one of
        // them sits between blocks - an empty paragraph, which holds no inline to expand to - and
        // that used to be read off pointer.Parent, at the cost of projecting one more object per
        // pointer move. _contentEnd is the same story: TextBlock.ContentEnd allocates a
        // TextPointer to answer, and the answer only changes when the text does.
        private (int Start, int End)[] _blockRanges;
        private int _contentEnd;

        // Built on the first hit test after SetText rather than during it: rendering must not pay
        // for a table only a pointer needs, and reading an offset means projecting a TextPointer
        // per property, which is exactly the cost being moved out of the per-move path. Once
        // built, resolving a point is a binary search with no calls into XAML at all.
        private OffsetEntry[] Offsets
        {
            get
            {
                if (_offsets == null)
                {
                    var entries = new List<OffsetEntry>();
                    var blocks = new List<(int, int)>();

                    if (TextBlock != null)
                    {
                        var index = 0;
                        var block = 0;

                        foreach (var current in TextBlock.Blocks)
                        {
                            blocks.Add((current.ElementStart.Offset, current.ElementEnd.Offset));

                            if (current is Paragraph paragraph)
                            {
                                CollectOffsets(paragraph.Inlines, entries, ref index, block, false);
                            }

                            block++;
                        }

                        _contentEnd = TextBlock.ContentEnd.Offset;
                    }

                    _blockRanges = blocks.ToArray();
                    _offsets = entries.ToArray();
                }

                return _offsets;
            }
        }

        private static void CollectOffsets(InlineCollection inlines, List<OffsetEntry> entries, ref int index, int block, bool link)
        {
            var total = inlines.Count;

            for (int i = 0; i < total; i++)
            {
                var inline = inlines[i];

                switch (inline)
                {
                    case Run run:
                        var text = run.Text;
                        var length = text != null ? text.Length : 0;

                        entries.Add(new OffsetEntry(run.ElementStart.Offset, run.ContentStart.Offset, run.ContentEnd.Offset,
                            run.ElementEnd.Offset, index, length, block, false, IsZeroWidth(text), link));

                        index += length;
                        break;
                    case Span span: // Bold/Italic/Underline/Hyperlink/... derive from Span
                        CollectOffsets(span.Inlines, entries, ref index, block, link || span is Hyperlink);
                        break;
                    case InlineUIContainer:
                        entries.Add(new OffsetEntry(inline.ElementStart.Offset, 0, 0, inline.ElementEnd.Offset,
                            index, 0, block, true, false, link));
                        break;
                    case LineBreak:
                        entries.Add(new OffsetEntry(inline.ElementStart.Offset, 0, 0, inline.ElementEnd.Offset,
                            index, 1, block, false, false, link));
                        index += 1; // one object-replacement / break unit
                        break;
                }
            }
        }

        // The last element beginning at or before the position; everything after it is
        // irrelevant. -1 when the position precedes every element.
        private int FindOffset(int target)
        {
            var offsets = Offsets;
            var found = -1;
            var lo = 0;
            var hi = offsets.Length - 1;

            while (lo <= hi)
            {
                var mid = (lo + hi) / 2;
                if (offsets[mid].Start <= target)
                {
                    found = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            return found;
        }

        // Whether a position lands inside a Hyperlink. Answered from the table rather than by
        // walking up from pointer.Parent, which projects a TextElement per level - and this is
        // asked on every pointer move, only to choose a cursor.
        internal bool IsLinkAt(int target)
        {
            var found = FindOffset(target);
            if (found < 0)
            {
                return false;
            }

            var entry = Offsets[found];
            return entry.Link && target <= entry.End;
        }

        // A TextPointer offset -> the TextHighlighter.Ranges index, over the table above.
        //
        // `block` is the index of the block the position resolved in (-1 when it resolved past
        // everything), which the returned index can't express — blocks are laid out one per
        // rendered paragraph, so it maps to StyledText as _first + block.
        //
        // `mark` says the position stopped in front of an inline object that has a zero-width
        // character ahead of it (an object opening a paragraph, one after a spoiler, and the ZWNJ
        // trailing the object right before all qualify): the object counts nothing, so the
        // position lands PAST it, and the character in front is what addresses its leading edge.
        private int GetHighlightIndex(int target, out int block, out bool mark)
        {
            block = -1;
            mark = false;

            var offsets = Offsets;
            if (offsets.Length == 0)
            {
                return 0;
            }

            var found = FindOffset(target);

            if (found < 0)
            {
                // Before the first element: nothing precedes the position.
                block = offsets[0].Block;
                return 0;
            }

            var entry = offsets[found];

            // In the order the tree walk tried them: an inline object claims a position
            // anywhere up to its end - including the one it begins at, which is what the pointer
            // reports for a hit on its leading half - and only then does a position at an
            // element's start resolve in front of that element.
            if (entry.Object && target < entry.End)
            {
                block = entry.Block;
                mark = found > 0 && offsets[found - 1].Mark;
                return entry.Rendered;
            }
            else if (target <= entry.Start)
            {
                block = entry.Block;
                return entry.Rendered;
            }
            else if (entry.ContentEnd > 0 && target <= entry.ContentEnd)
            {
                var count = target - entry.ContentStart;

                block = entry.Block;
                mark = count > 0 && entry.Mark && found + 1 < offsets.Length && offsets[found + 1].Object;
                return entry.Rendered + count;
            }

            // Past this element: the position belongs to whatever comes next, which adds nothing
            // to the index of its own. Past the last one it resolves in no block at all.
            if (found + 1 < offsets.Length)
            {
                block = offsets[found + 1].Block;
            }

            return entry.Rendered + entry.Length;
        }

        private bool IsBetweenBlocks(int target)
        {
            // Harvested with the offsets, so touching it first is what builds both.
            _ = Offsets;

            var blocks = _blockRanges;

            for (int i = 0; i < blocks.Length; i++)
            {
                if (target > blocks[i].Start && target < blocks[i].End)
                {
                    return false;
                }
            }

            return true;
        }

        // One of the zero-width characters SetText emits around an inline object: the ZWNJ that
        // stands in for it, or the RTL/LTR mark opening a paragraph.
        private static bool IsZeroWidth(string text)
        {
            return text is Icons.ZWNJ or Icons.LTR or Icons.RTL;
        }
    }
}
