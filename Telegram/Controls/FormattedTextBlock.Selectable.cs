//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
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
    // only content units — characters in Runs, plus 1 per inline object (custom emoji,
    // image, math) and per line break — and does NOT count paragraph breaks (the
    // shift correction in SetText exists for exactly that reason). The ZWNJ workaround
    // characters FormattedTextBlock injects are real Run chars, so they're counted and
    // highlighting them is harmless (zero width). Copy, which needs FormattedText/
    // StyledText offsets, is a separate layer.
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
                    _contentLength = GetHighlightIndex(TextBlock.ContentEnd);
                }

                return _contentLength < 0 ? 0 : _contentLength;
            }
        }

        internal void InvalidateContentLength()
        {
            _contentLength = -1;
        }

        public int GetPositionFromPoint(Point point)
        {
            if (TextBlock == null)
            {
                return 0;
            }

            var pointer = TextBlock.GetPositionFromPoint(point);
            return pointer != null ? GetHighlightIndex(pointer) : 0;
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
        public void GetSelectionBoundary(int position, TextSelectionGranularity granularity, out int start, out int end)
        {
            start = end = position;

            if (granularity == TextSelectionGranularity.Character || _text == null || TextBlock == null)
            {
                return;
            }

            var offset = RenderedToStyled(position);
            if (!ParagraphRange(offset, out var lo, out var hi) || hi <= lo)
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
        // (plain single run / fast path) is an exact 1:1 mapping.
        private int RenderedToStyled(int rendered)
        {
            var map = _indexMap;
            if (map == null || map.Count == 0)
            {
                return rendered;
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
        // full range) where rendered == styled. Used to place the search-query highlight.
        private int StyledToRendered(int styled)
        {
            var map = _indexMap;
            if (map == null || map.Count == 0)
            {
                return styled;
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
        private int GetHighlightIndex(TextPointer pointer)
        {
            if (TextBlock == null || pointer == null)
            {
                return 0;
            }

            var target = pointer.Offset;
            var index = 0;

            foreach (var block in TextBlock.Blocks)
            {
                if (block is Paragraph paragraph && WalkInlines(paragraph.Inlines, target, ref index))
                {
                    return index;
                }
            }

            return index;
        }

        // Walks inlines accumulating the content-unit count in `index`. Returns true
        // once the pointer (`target` offset) is reached, leaving `index` at it.
        private static bool WalkInlines(InlineCollection inlines, int target, ref int index)
        {
            foreach (var inline in inlines)
            {
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
                            index += target - run.ContentStart.Offset;
                            return true;
                        }
                        index += run.Text != null ? run.Text.Length : 0;
                        break;
                    case Span span: // Bold/Italic/Underline/Hyperlink/... derive from Span
                        if (WalkInlines(span.Inlines, target, ref index))
                        {
                            return true;
                        }
                        break;
                    case InlineUIContainer:
                        break;
                    case LineBreak:
                        index += 1; // one object-replacement / break unit
                        break;
                }
            }

            return false;
        }
    }
}
