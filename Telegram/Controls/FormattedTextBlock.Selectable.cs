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
using Windows.UI.Xaml.Core.Direct;
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

            // The offset table is built with the inlines, and only for an Extended block, so one
            // that becomes Extended after its text was applied has to render again to get one.
            // Every caller sets the mode before the text; this is here so that stays a choice.
            if (sender._textSelection == TextSelectionMode.Extended && sender._textApplied)
            {
                sender.SetText(sender._clientService, sender._text, sender._first, sender._last, sender._fontSize);
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

        // The last entry ends where the content does, so this is a read rather than the tree
        // walk it used to be — which mattered, because Select() asks for it on every pointer
        // move of a drag.
        public int ContentLength
        {
            get
            {
                var last = _offsets != null ? _offsets.Count - 1 : -1;
                if (last < 0)
                {
                    return 0;
                }

                var entry = _offsets[last];
                return entry.Rendered + entry.Length;
            }
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

        // One entry per leaf inline, in document order. Both lists are reused across builds: a
        // recycled bubble runs SetText on every message it shows.
        private List<OffsetEntry> _offsets;

        // Where each Block begins and ends in TextPointer space. A position outside every one of
        // them sits between blocks - an empty paragraph, which holds no inline to expand to - and
        // that used to be read off pointer.Parent, at the cost of projecting one more object per
        // pointer move. _contentEnd is the same story: TextBlock.ContentEnd allocates a
        // TextPointer to answer, and the answer only changes when the text does.
        private List<(int Start, int End)> _blockRanges;
        private int _contentEnd;

        #region Building

        // The table is built as the inlines are, and never by reading the tree back. An inline
        // built through XamlDirect is a bare core object with no framework peer, so asking the
        // projection for one - Blocks[i], Inlines[i], ElementStart - inflates a peer and an RCW
        // for every element the build deliberately left deflated. SetText fills the table,
        // UpdateDate shifts it, ProcessCodeBlock splices it.
        //
        // TextPointer space is a position count, and a TextElement's four offsets are fixed
        // distances into the span of it the element reserves (CTextElement::GetOffsetForEdge):
        //
        //     ElementStart = 0   ContentStart = 1   ContentEnd = count - 1   ElementEnd = count
        //
        // and `count` is what each type reserves:
        //
        //     Run                text length + 2   CRun::GetPositionCount
        //     Span, Hyperlink    children + 2      CSpan -> CInlineCollection::GetPositionCount,
        //                                          "plus one each for the start and end"
        //     Paragraph          children + 2      CParagraph::GetPositionCount
        //     InlineUIContainer  2                 no override, so CTextElement's two edges:
        //                                          "InlineUIContainer only has 2 positions -
        //                                          Open/Close" (CInlineUIContainer::GetRun).
        //                                          The embedded object reserves none of its own.
        //     LineBreak          2                 no override either. Nothing emits one today.
        //     RichTextBlock      sum of blocks     CBlockCollection::GetPositionCount adds no
        //                                          wrapper of its own, and CRichTextBlock
        //                                          "always starts at 0", so block 0 opens at 0.
        //
        // Read out of the XAML text core, microsoft-ui-xaml/src/dxaml/xcp/core/text - the same
        // tree the system XAML names in its own frames (onecoreuap/windows/dxaml/xcp/core/...).
        // These are the values, not an inference from them: do not re-derive by experiment.
        private const int ObjectLength = 2;

        private int _offsetPosition;    // running TextPointer offset
        private int _offsetRendered;    // running TextHighlighter.Ranges index
        private int _offsetBlock;       // index of the block being filled
        private bool _offsetLink;       // inside a Hyperlink
        private bool _offsetsEnabled;

        // Where the entries go: _offsets while SetText builds, a scratch list while
        // ProcessCodeBlock rebuilds one paragraph in the middle of it.
        private List<OffsetEntry> _offsetTarget;

        private void BeginOffsets()
        {
            // Only an Extended block reads the table - IsLinkAt and the whole ISelectableControl
            // surface are gated on it - so nothing else pays to build one. A block rendering
            // into a Span owned by another control is gated out for the same reason: it has no
            // Blocks of its own, and OnPointerMoved already skips it.
            _offsetsEnabled = _textSelection == TextSelectionMode.Extended
                && _spanForInlines == null
                && TextBlock != null;

            ClearOffsets();

            if (!_offsetsEnabled)
            {
                return;
            }

            _offsetTarget = _offsets ??= new List<OffsetEntry>();
            _blockRanges ??= new List<(int, int)>();

            _offsetPosition = 0;
            _offsetRendered = 0;
            _offsetBlock = 0;
            _offsetLink = false;
        }

        // The tree the table describes is gone, and only SetText builds another.
        private void ClearOffsets()
        {
            _offsets?.Clear();
            _blockRanges?.Clear();
            _contentEnd = 0;
        }

        private void OpenBlock()
        {
            if (_offsetsEnabled)
            {
                _blockRanges.Add((_offsetPosition, 0));
                _offsetPosition++;
            }
        }

        private void CloseBlock()
        {
            if (_offsetsEnabled)
            {
                // ElementEnd sits one past ContentEnd, and the next block opens there.
                var last = _blockRanges.Count - 1;
                _blockRanges[last] = (_blockRanges[last].Start, ++_offsetPosition);
                _offsetBlock++;
            }
        }

        // A Span, Hyperlink or any other element that holds inlines of its own. `link` is passed
        // at both ends rather than saved: nothing here nests a hyperlink inside another.
        private void OpenInline(bool link)
        {
            if (_offsetsEnabled)
            {
                _offsetPosition++;
                _offsetLink |= link;
            }
        }

        private void CloseInline(bool link)
        {
            if (_offsetsEnabled)
            {
                _offsetPosition++;
                _offsetLink &= !link;
            }
        }

        private void AddRunOffset(int length, bool mark)
        {
            if (_offsetsEnabled)
            {
                var start = _offsetPosition;

                _offsetTarget.Add(new OffsetEntry(start, start + 1, start + 1 + length, start + length + 2,
                    _offsetRendered, length, _offsetBlock, false, mark, _offsetLink));

                _offsetPosition = start + length + 2;
                _offsetRendered += length;
            }
        }

        private void AddObjectOffset()
        {
            if (_offsetsEnabled)
            {
                var start = _offsetPosition;

                _offsetTarget.Add(new OffsetEntry(start, 0, 0, start + ObjectLength,
                    _offsetRendered, 0, _offsetBlock, true, false, _offsetLink));

                _offsetPosition = start + ObjectLength;
            }
        }

        private void EndOffsets()
        {
            if (_offsetsEnabled)
            {
                _contentEnd = _offsetPosition;
            }
        }

        // A relative date rewrote its Run, so that entry grew by `delta` and every offset after
        // it moved with it. The rendered space is shifted by ShiftRenderedSpace, which owns the
        // index map and the highlighter ranges; this is the same shift in TextPointer space.
        private void ShiftOffsets(object element, int delta)
        {
            if (!_offsetsEnabled || _dates == null || _dateOffsets == null)
            {
                return;
            }

            var date = _dates.IndexOf(element as IXamlDirectObject);
            if (date < 0 || date >= _dateOffsets.Count)
            {
                return;
            }

            var found = _dateOffsets[date];
            if (found < 0 || found >= _offsets.Count)
            {
                return;
            }

            var entry = _offsets[found];
            _offsets[found] = new OffsetEntry(entry.Start, entry.ContentStart, entry.ContentEnd + delta, entry.End + delta,
                entry.Rendered, entry.Length + delta, entry.Block, entry.Object, entry.Mark, entry.Link);

            ShiftOffsetsFrom(found + 1, entry.Block, delta, delta);
        }

        // Moves everything from `first` on by `delta` in TextPointer space and `rendered` in
        // highlighter space, and the blocks it spans with it. `block` is the one the change
        // happened in: its start does not move, only its end.
        private void ShiftOffsetsFrom(int first, int block, int delta, int rendered)
        {
            for (int i = first; i < _offsets.Count; i++)
            {
                var next = _offsets[i];

                // ContentStart/ContentEnd are 0 on an inline object rather than offsets, and
                // GetHighlightIndex reads that zero as "no content of its own".
                _offsets[i] = new OffsetEntry(next.Start + delta,
                    next.Object ? 0 : next.ContentStart + delta,
                    next.Object ? 0 : next.ContentEnd + delta,
                    next.End + delta, next.Rendered + rendered, next.Length, next.Block,
                    next.Object, next.Mark, next.Link);
            }

            for (int i = block; i < _blockRanges.Count; i++)
            {
                var range = _blockRanges[i];
                _blockRanges[i] = (i == block ? range.Start : range.Start + delta, range.End + delta);
            }

            _contentEnd += delta;
        }

        // Tokenization replaced one paragraph's inlines wholesale, so its entries are rebuilt
        // between these two: the old ones come out, whatever is emitted in between goes in
        // their place, and everything after moves by what the paragraph gained.
        private List<OffsetEntry> _offsetSplice;
        private int _spliceFirst = -1;
        private int _spliceCount;
        private int _spliceBlock;
        private int _spliceEnd;
        private int _spliceRendered;

        private void BeginSplice(int block)
        {
            _spliceFirst = -1;

            if (!_offsetsEnabled)
            {
                return;
            }

            // Everything the rebuild emits lands here first. That has to be set up before the
            // search below can give up: a paragraph with no entries left to replace - the block
            // was unloaded while the tokenizer ran - would otherwise have its new inlines
            // appended to the end of the table.
            _offsetTarget = _offsetSplice ??= new List<OffsetEntry>();
            _offsetTarget.Clear();

            var first = -1;
            var last = -1;

            for (int i = 0; i < _offsets.Count; i++)
            {
                if (_offsets[i].Block != block)
                {
                    continue;
                }

                if (first < 0)
                {
                    first = i;
                }

                last = i;
            }

            if (first < 0)
            {
                return;
            }

            var head = _offsets[first];
            var tail = _offsets[last];

            _spliceFirst = first;
            _spliceCount = last - first + 1;
            _spliceBlock = block;
            _spliceEnd = tail.End;
            _spliceRendered = tail.Rendered + tail.Length;

            _offsets.RemoveRange(first, last - first + 1);

            _offsetPosition = head.Start;
            _offsetRendered = head.Rendered;
            _offsetBlock = block;
            _offsetLink = false;
        }

        // _dateOffsets holds indices into _offsets, so a splice that changes how many entries
        // the paragraph takes moves every date after it. One inside the spliced paragraph is
        // gone with the inlines that were replaced - a code block cannot hold a date today, but
        // dropping the index is what keeps a shift from landing on someone else's entry.
        private void ReindexDates(int moved)
        {
            if (_dateOffsets == null)
            {
                return;
            }

            for (int i = 0; i < _dateOffsets.Count; i++)
            {
                var index = _dateOffsets[i];

                if (index < _spliceFirst)
                {
                    continue;
                }

                _dateOffsets[i] = index < _spliceFirst + _spliceCount ? -1 : index + moved;
            }
        }

        private void EndSplice()
        {
            if (!_offsetsEnabled)
            {
                return;
            }

            var scratch = _offsetSplice;
            _offsetTarget = _offsets;

            if (_spliceFirst < 0)
            {
                scratch.Clear();
                return;
            }

            _offsets.InsertRange(_spliceFirst, scratch);

            // A date in a LATER paragraph is still in the table, but its entry has moved.
            ReindexDates(scratch.Count - _spliceCount);

            ShiftOffsetsFrom(_spliceFirst + scratch.Count, _spliceBlock,
                _offsetPosition - _spliceEnd, _offsetRendered - _spliceRendered);

            scratch.Clear();
            _spliceFirst = -1;
        }

        #endregion


        // The last element beginning at or before the position; everything after it is
        // irrelevant. -1 when the position precedes every element.
        private int FindOffset(int target)
        {
            var offsets = _offsets;
            var found = -1;
            var lo = 0;
            var hi = offsets != null ? offsets.Count - 1 : -1;

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

            var entry = _offsets[found];
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

            var offsets = _offsets;
            if (offsets == null || offsets.Count == 0)
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
                mark = count > 0 && entry.Mark && found + 1 < offsets.Count && offsets[found + 1].Object;
                return entry.Rendered + count;
            }

            // Past this element: the position belongs to whatever comes next, which adds nothing
            // to the index of its own. Past the last one it resolves in no block at all.
            if (found + 1 < offsets.Count)
            {
                block = offsets[found + 1].Block;
            }

            return entry.Rendered + entry.Length;
        }

        private bool IsBetweenBlocks(int target)
        {
            var blocks = _blockRanges;
            if (blocks == null)
            {
                return true;
            }

            for (int i = 0; i < blocks.Count; i++)
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

        // The same question over a range of a larger string, which is how the build asks it -
        // the Run's text is a substring the native path never materializes.
        private static bool IsZeroWidth(string text, int offset, int length)
        {
            return length == 1
                && (text[offset] == Icons.ZWNJ[0] || text[offset] == Icons.LTR[0] || text[offset] == Icons.RTL[0]);
        }
    }
}
