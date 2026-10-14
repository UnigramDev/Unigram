//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Globalization;

namespace Telegram.Controls.RichEditor
{
    public readonly struct RichPosition : IEquatable<RichPosition>
    {
        public RichPosition(RichTextLeaf leaf, int offset)
        {
            Leaf = leaf;
            Offset = offset;
        }

        public RichTextLeaf Leaf { get; }

        public int Offset { get; }

        public bool IsValid => Leaf != null;

        public static int Compare(RichPosition x, RichPosition y)
        {
            var leaf = x.Leaf.Index.CompareTo(y.Leaf.Index);
            return leaf != 0 ? leaf : x.Offset.CompareTo(y.Offset);
        }

        public bool Equals(RichPosition other) => ReferenceEquals(Leaf, other.Leaf) && Offset == other.Offset;

        public override bool Equals(object obj) => obj is RichPosition other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Leaf, Offset);
    }

    [Flags]
    public enum RichEditorChanges
    {
        None = 0,
        Selection = 1,

        /// <summary>Text inside <see cref="RichEditorChangedEventArgs.Leaf"/> changed, and nothing else did.</summary>
        Text = 2,

        /// <summary>Blocks were added, removed, moved or changed kind: everything may be stale.</summary>
        Structure = 4,
    }

    public sealed class RichEditorChangedEventArgs : EventArgs
    {
        public RichEditorChanges Changes { get; internal set; }

        public RichTextLeaf Leaf { get; internal set; }
    }

    public enum RichEditKind
    {
        None,
        Typing,
        Deleting,
        Other,
    }

    /// <summary>
    /// The editor without a view: the document, the selection, and every change to either.
    /// The view turns pointer, keyboard and IME input into calls here and redraws from
    /// <see cref="Changed"/>; nothing in this class knows about XAML, so it can be exercised on
    /// its own.
    ///
    /// Undo is a snapshot per transaction, like tdesktop's and Android's editors: every
    /// operation that is not plain typing clones the document first. Documents are bounded by
    /// the server limits (a few hundred blocks), and a clone shares every immutable payload.
    /// </summary>
    public sealed partial class RichEditorEngine
    {
        private const int UndoLimit = 200;
        private const int TypingCoalesceMilliseconds = 1000;

        private RichDocument _document;

        private RichPosition _anchor;
        private RichPosition _focus;
        private RichBlock _node;

        // Marks toggled with nothing selected: they apply to what is typed next, until the
        // caret moves.
        private RichMarks? _storedMarks;

        private readonly List<Snapshot> _undo = new();
        private readonly List<Snapshot> _redo = new();

        private RichEditKind _lastEdit;
        private RichTextLeaf _lastEditLeaf;
        private int _lastEditTime;

        private readonly RichEditorChangedEventArgs _args = new();
        private RichEditorChanges _pending;
        private RichTextLeaf _pendingLeaf;
        private int _batch;

        public RichEditorEngine(RichDocument document = null)
        {
            Load(document ?? new RichDocument());
        }

        public RichDocument Document => _document;

        public event EventHandler<RichEditorChangedEventArgs> Changed;

        public RichPosition Anchor => _anchor;

        public RichPosition Focus => _focus;

        /// <summary>The atomic block selected whole, if the selection is a block rather than text.</summary>
        public RichBlock SelectedNode => _node;

        public RichPosition Start => _node == null && RichPosition.Compare(_anchor, _focus) > 0 ? _focus : _anchor;

        public RichPosition End => _node == null && RichPosition.Compare(_anchor, _focus) > 0 ? _anchor : _focus;

        public bool IsSelectionEmpty => _node == null && _anchor.Equals(_focus);

        public bool CanUndo => _undo.Count > 0;

        public bool CanRedo => _redo.Count > 0;

        public RichMarks? StoredMarks => _storedMarks;

        /// <summary>
        /// What the document may hold, or null for a rich message, which may hold anything. Set
        /// it before <see cref="Load"/>, which conforms the document to it.
        /// </summary>
        public RichSchema Schema { get; set; }

        public void Load(RichDocument document)
        {
            _document = document;
            Schema?.Conform(_document.Blocks);
            _document.Reindex();

            _undo.Clear();
            _redo.Clear();
            _lastEdit = RichEditKind.None;

            var first = _document.Leaves[0];
            _anchor = _focus = new RichPosition(first, 0);
            _node = null;
            _storedMarks = null;

            Raise(RichEditorChanges.Structure | RichEditorChanges.Selection, null);
        }

        // =====================================================================================
        // Change notification
        // =====================================================================================

        /// <summary>
        /// Groups several operations into one notification: the view redraws once, after the
        /// outermost <see cref="EndBatch"/>.
        /// </summary>
        public void BeginBatch() => _batch++;

        public void EndBatch()
        {
            if (--_batch == 0 && _pending != RichEditorChanges.None)
            {
                var changes = _pending;
                var leaf = _pendingLeaf;

                _pending = RichEditorChanges.None;
                _pendingLeaf = null;

                Notify(changes, leaf);
            }
        }

        private void Raise(RichEditorChanges changes, RichTextLeaf leaf)
        {
            if (_batch > 0)
            {
                // Text changes in two different leaves are no longer "one leaf's text".
                if ((changes & RichEditorChanges.Text) != 0 && (_pending & RichEditorChanges.Text) != 0 && !ReferenceEquals(leaf, _pendingLeaf))
                {
                    changes |= RichEditorChanges.Structure;
                }

                _pending |= changes;
                _pendingLeaf ??= leaf;
                return;
            }

            Notify(changes, leaf);
        }

        private void Notify(RichEditorChanges changes, RichTextLeaf leaf)
        {
            if ((changes & RichEditorChanges.Structure) != 0)
            {
                leaf = null;
            }

            _args.Changes = changes;
            _args.Leaf = leaf;
            Changed?.Invoke(this, _args);
        }

        // =====================================================================================
        // Selection
        // =====================================================================================

        public void SetCaret(RichPosition position) => Select(position, position);

        public void Select(RichPosition anchor, RichPosition focus)
        {
            anchor = Clamp(anchor);
            focus = Clamp(focus);
            CoverTables(ref anchor, ref focus);

            if (_node == null && anchor.Equals(_anchor) && focus.Equals(_focus))
            {
                return;
            }

            _anchor = anchor;
            _focus = focus;
            _node = null;
            _storedMarks = null;
            _lastEdit = RichEditKind.None;
            _ruleApplied = false;

            Raise(RichEditorChanges.Selection, null);
        }

        /// <summary>Moves only the focus end: shift-click, shift-arrows, dragging.</summary>
        public void ExtendTo(RichPosition focus)
        {
            if (_node != null)
            {
                // Extending from a selected block starts from the nearest text around it.
                var anchor = PositionBefore(_node) ?? PositionAfter(_node) ?? focus;
                Select(anchor, focus);
                return;
            }

            Select(_anchor, ReleaseTable(focus));
        }

        public void SelectNode(RichBlock block)
        {
            if (block == null || !block.IsAtomic)
            {
                return;
            }

            if (ReferenceEquals(block, _node))
            {
                return;
            }

            _node = block;
            _storedMarks = null;
            _lastEdit = RichEditKind.None;

            // The text ends keep pointing near the block, so leaving the node selection with an
            // arrow has somewhere to start from.
            var near = PositionBefore(block) ?? PositionAfter(block);
            if (near is RichPosition position)
            {
                _anchor = _focus = position;
            }

            Raise(RichEditorChanges.Selection, null);
        }

        public void SelectAll()
        {
            var last = LastShownLeaf();
            Select(new RichPosition(_document.Leaves[0], 0), new RichPosition(last, last.Length));
        }

        /// <summary>The whole of one leaf: triple click.</summary>
        public void SelectLeaf(RichTextLeaf leaf)
        {
            Select(new RichPosition(leaf, 0), new RichPosition(leaf, leaf.Length));
        }

        public void SelectWord(RichPosition position)
        {
            var leaf = position.Leaf;
            GetWordRange(leaf.Text, position.Offset, out int start, out int end);
            Select(new RichPosition(leaf, start), new RichPosition(leaf, end));
        }

        private RichPosition Clamp(RichPosition position)
        {
            var leaf = position.Leaf;
            if (leaf == null || leaf.Index >= _document.Leaves.Count || !ReferenceEquals(_document.Leaves[leaf.Index], leaf))
            {
                leaf = _document.Leaves[0];
                return new RichPosition(leaf, 0);
            }

            return new RichPosition(leaf, Math.Clamp(position.Offset, 0, leaf.Length));
        }

        /// <summary>
        /// The stop a block is at, or spans from or to. An item of a collage has none of its own -
        /// the collage is selected and walked past as one - so it answers with its collage's.
        /// </summary>
        private int StopOf(RichBlock block, bool last)
        {
            for (var current = block; current != null; current = current.Parent)
            {
                var index = _document.GetStopIndex(current);
                if (index >= 0)
                {
                    return index;
                }

                if (current.FirstStop <= current.LastStop)
                {
                    return last ? current.LastStop : current.FirstStop;
                }
            }

            return -1;
        }

        /// <summary>The end of the leaf right before <paramref name="block"/> in reading order.</summary>
        private RichPosition? PositionBefore(RichBlock block)
        {
            var stop = StopOf(block, false);
            if (stop < 0)
            {
                return null;
            }

            for (int i = stop - 1; i >= 0; i--)
            {
                var leaf = _document.Stops[i].Leaf;
                if (leaf != null)
                {
                    return new RichPosition(leaf, leaf.Length);
                }
            }

            return null;
        }

        private RichPosition? PositionAfter(RichBlock block)
        {
            var stop = StopOf(block, true);
            if (stop < 0)
            {
                return null;
            }

            for (int i = stop + 1; i < _document.Stops.Count; i++)
            {
                var leaf = _document.Stops[i].Leaf;
                if (leaf != null)
                {
                    return new RichPosition(leaf, 0);
                }
            }

            return null;
        }

        // =====================================================================================
        // Logical movement. Lines, Home/End and Up/Down need the layout and live in the view;
        // these are the moves that only need the text.
        // =====================================================================================

        /// <summary>
        /// One caret stop forward or back. At a leaf's edge the next stop is the next leaf or an
        /// atomic block, which is selected whole.
        /// </summary>
        public void MoveCharacter(int direction, bool extend)
        {
            if (_node != null && !extend)
            {
                if (_node.Parent is RichMediaGroupBlock group)
                {
                    var index = group.Items.IndexOf(_node) + direction;
                    if (index >= 0 && index < group.Items.Count)
                    {
                        SelectNode(group.Items[index]);
                        return;
                    }

                    MoveToStop(_document.GetStopIndex(group) + direction, direction);
                    return;
                }

                var stop = _document.GetStopIndex(_node);
                MoveToStop(stop + direction, direction);
                return;
            }

            var focus = _focus;

            if (!extend && !IsSelectionEmpty)
            {
                // Collapsing a range goes to its edge, not one past it.
                SetCaret(direction < 0 ? Start : End);
                return;
            }

            var leaf = focus.Leaf;
            var offset = direction < 0 ? PreviousCaretStop(leaf.Text, focus.Offset) : NextCaretStop(leaf.Text, focus.Offset);

            if (offset != focus.Offset)
            {
                Move(new RichPosition(leaf, offset), extend);
                return;
            }

            if (extend)
            {
                // A text selection does not take in a block; it crosses it to the next leaf.
                var next = NextLeaf(leaf, direction);
                if (next != null)
                {
                    Move(new RichPosition(next, direction < 0 ? next.Length : 0), true);
                }

                return;
            }

            MoveToStop(leaf.StopIndex + direction, direction);
        }

        public void MoveWord(int direction, bool extend)
        {
            if (_node != null)
            {
                MoveCharacter(direction, extend);
                return;
            }

            var focus = _focus;
            var leaf = focus.Leaf;
            var offset = direction < 0 ? PreviousWordStart(leaf.Text, focus.Offset) : NextWordEnd(leaf.Text, focus.Offset);

            if (offset == focus.Offset)
            {
                MoveCharacter(direction, extend);
                return;
            }

            Move(new RichPosition(leaf, offset), extend);
        }

        public void MoveToLeafEdge(int direction, bool extend)
        {
            var leaf = _focus.Leaf;
            Move(new RichPosition(leaf, direction < 0 ? 0 : leaf.Length), extend);
        }

        public void MoveToDocumentEdge(int direction, bool extend)
        {
            var leaf = direction < 0 ? _document.Leaves[0] : LastShownLeaf();
            Move(new RichPosition(leaf, direction < 0 ? 0 : leaf.Length), extend);
        }

        // The last leaf, unless that is a credit not drawn, which ending there would show.
        private RichTextLeaf LastShownLeaf()
        {
            var leaves = _document.Leaves;
            var index = leaves.Count - 1;

            while (index > 0 && IsLeafHidden(leaves[index]))
            {
                index--;
            }

            return leaves[index];
        }

        public void Move(RichPosition position, bool extend)
        {
            if (extend)
            {
                ExtendTo(position);
            }
            else
            {
                SetCaret(position);
            }
        }

        /// <summary>
        /// An empty credit is not drawn until the caret goes into it - a quote or a photo with
        /// no author would otherwise carry an empty line under it - so moving does not stop in
        /// one either. "Add credit" is how one is reached.
        /// </summary>
        public bool IsLeafHidden(RichTextLeaf leaf)
        {
            // With a block selected the caret is in no leaf: its ends are parked next to the
            // block, which is the end of the credit of a block just before it.
            return leaf.Role == RichLeafRole.Credit
                && leaf.IsEmpty
                && (_node != null || (!ReferenceEquals(leaf, _focus.Leaf) && !ReferenceEquals(leaf, _anchor.Leaf)))
                && !HasQuotedText(leaf);
        }

        // An empty credit is shown once its quote has text: there is no other way to reach it.
        private bool HasQuotedText(RichTextLeaf credit)
        {
            if (credit.Owner is RichTextQuote text)
            {
                return !text.Text.IsEmpty;
            }

            if (credit.Owner is not RichBlockQuote quote)
            {
                return false;
            }

            // Its text is not among the leaves, and a quote is only collapsed for being long.
            if (quote.IsCollapsed)
            {
                return true;
            }

            // A quote's leaves come right before its credit.
            var leaves = _document.Leaves;
            var index = credit.Index;

            if (index >= leaves.Count || !ReferenceEquals(leaves[index], credit))
            {
                return false;
            }

            for (int i = index - 1; i >= 0 && IsWithin(leaves[i].Owner, quote); i--)
            {
                if (!leaves[i].IsEmpty)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsWithin(RichBlock block, RichBlock ancestor)
        {
            for (; block != null; block = block.Parent)
            {
                if (ReferenceEquals(block, ancestor))
                {
                    return true;
                }
            }

            return false;
        }

        private void MoveToStop(int index, int direction)
        {
            var stops = _document.Stops;

            while (index >= 0 && index < stops.Count && stops[index].Leaf is RichTextLeaf skipped && IsLeafHidden(skipped))
            {
                index += direction;
            }

            // Off either end: nothing further to go to, so the selection stays.
            if (index < 0 || index >= stops.Count)
            {
                return;
            }

            var stop = stops[index];
            if (stop.Leaf != null)
            {
                SetCaret(new RichPosition(stop.Leaf, direction < 0 ? stop.Leaf.Length : 0));
            }
            else
            {
                SelectNode(stop.Block);
            }
        }

        private RichTextLeaf NextLeaf(RichTextLeaf leaf, int direction)
        {
            var index = leaf.Index + direction;
            var leaves = _document.Leaves;

            while (index >= 0 && index < leaves.Count && IsLeafHidden(leaves[index]))
            {
                index += direction;
            }

            return index >= 0 && index < leaves.Count ? leaves[index] : null;
        }

        // =====================================================================================
        // Text boundaries
        // =====================================================================================

        /// <summary>The next caret position: whole surrogate pairs and combining sequences, never half of one.</summary>
        public static int NextCaretStop(string text, int offset)
        {
            if (offset >= text.Length)
            {
                return text.Length;
            }

            var element = StringInfo.GetNextTextElement(text, offset);
            var next = offset + Math.Max(1, element.Length);

            // Emoji sequences: StringInfo on .NET Native predates extended grapheme clusters, so
            // a ZWJ sequence or a modifier would be stepped into. Absorb joiners, variation
            // selectors, skin tones and tag characters.
            while (next < text.Length)
            {
                var c = text[next];
                if (c == '‍')
                {
                    next++;
                    if (next < text.Length)
                    {
                        next += char.IsHighSurrogate(text[next]) && next + 1 < text.Length && char.IsLowSurrogate(text[next + 1]) ? 2 : 1;
                    }
                }
                else if (c == '️' || c == '︎' || c == '⃣')
                {
                    next++;
                }
                else if (char.IsHighSurrogate(c) && next + 1 < text.Length && char.IsLowSurrogate(text[next + 1]) && IsEmojiModifierOrTag(char.ConvertToUtf32(c, text[next + 1])))
                {
                    next += 2;
                }
                else
                {
                    break;
                }
            }

            return Math.Min(next, text.Length);
        }

        public static int PreviousCaretStop(string text, int offset)
        {
            if (offset <= 0)
            {
                return 0;
            }

            // Walk forward from a safe point; caret stops are only well defined going forward.
            var start = offset - 1;
            while (start > 0 && offset - start < 32 && IsContinuation(text, start))
            {
                start--;
            }

            var position = start;
            var previous = start;

            while (position < offset)
            {
                previous = position;
                position = NextCaretStop(text, position);
            }

            return previous;
        }

        private static bool IsContinuation(string text, int index)
        {
            var c = text[index];
            if (char.IsLowSurrogate(c) || c == '‍' || c == '️' || c == '︎' || c == '⃣')
            {
                return true;
            }

            if (index > 0 && text[index - 1] == '‍')
            {
                return true;
            }

            if (char.IsHighSurrogate(c) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
            {
                return IsEmojiModifierOrTag(char.ConvertToUtf32(c, text[index + 1]));
            }

            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            return category == UnicodeCategory.NonSpacingMark || category == UnicodeCategory.EnclosingMark;
        }

        private static bool IsEmojiModifierOrTag(int codepoint)
        {
            return (codepoint >= 0x1F3FB && codepoint <= 0x1F3FF) || (codepoint >= 0xE0020 && codepoint <= 0xE007F);
        }

        private enum CharClass
        {
            Space,
            Word,
            Punctuation,
            Object,
        }

        private static CharClass Classify(char c)
        {
            if (c == RichTextLeaf.ObjectChar)
            {
                return CharClass.Object;
            }

            if (char.IsWhiteSpace(c))
            {
                return CharClass.Space;
            }

            if (char.IsLetterOrDigit(c) || c == '_' || char.IsSurrogate(c) || char.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                return CharClass.Word;
            }

            return CharClass.Punctuation;
        }

        /// <summary>Ctrl+Right: past the end of the next word, as Windows text boxes do.</summary>
        public static int NextWordEnd(string text, int offset)
        {
            var i = offset;
            while (i < text.Length && Classify(text[i]) == CharClass.Space)
            {
                i++;
            }

            if (i < text.Length)
            {
                var kind = Classify(text[i]);
                if (kind == CharClass.Object)
                {
                    i++;
                }
                else
                {
                    while (i < text.Length && Classify(text[i]) == kind)
                    {
                        i++;
                    }
                }
            }

            while (i < text.Length && Classify(text[i]) == CharClass.Space && text[i] != '\n')
            {
                i++;
            }

            return i;
        }

        /// <summary>Ctrl+Left: to the start of the word before the caret.</summary>
        public static int PreviousWordStart(string text, int offset)
        {
            var i = offset;
            while (i > 0 && Classify(text[i - 1]) == CharClass.Space)
            {
                i--;
            }

            if (i > 0)
            {
                var kind = Classify(text[i - 1]);
                if (kind == CharClass.Object)
                {
                    i--;
                }
                else
                {
                    while (i > 0 && Classify(text[i - 1]) == kind)
                    {
                        i--;
                    }
                }
            }

            return i;
        }

        /// <summary>The word, run of spaces or run of punctuation around <paramref name="offset"/>: double click.</summary>
        public static void GetWordRange(string text, int offset, out int start, out int end)
        {
            if (text.Length == 0)
            {
                start = end = 0;
                return;
            }

            var probe = Math.Min(offset, text.Length - 1);

            // Double clicking just past a word selects the word, not the space after it.
            if (probe > 0 && offset == text.Length)
            {
                probe = text.Length - 1;
            }

            var kind = Classify(text[probe]);

            start = probe;
            end = probe + 1;

            if (kind == CharClass.Object)
            {
                return;
            }

            while (start > 0 && Classify(text[start - 1]) == kind && text[start - 1] != '\n')
            {
                start--;
            }

            while (end < text.Length && Classify(text[end]) == kind && text[end] != '\n')
            {
                end++;
            }
        }

        // =====================================================================================
        // Undo
        // =====================================================================================

        private sealed class Snapshot
        {
            public RichDocument Document;
            public int AnchorLeaf;
            public int AnchorOffset;
            public int FocusLeaf;
            public int FocusOffset;
            public int Node;
        }

        private Snapshot Capture()
        {
            return new Snapshot
            {
                Document = _document.Clone(),
                AnchorLeaf = _anchor.Leaf.Index,
                AnchorOffset = _anchor.Offset,
                FocusLeaf = _focus.Leaf.Index,
                FocusOffset = _focus.Offset,
                Node = _node != null ? IndexOf(_document.AllBlocks, _node) : -1
            };
        }

        private static int IndexOf(IReadOnlyList<RichBlock> blocks, RichBlock block)
        {
            for (int i = 0; i < blocks.Count; i++)
            {
                if (ReferenceEquals(blocks[i], block))
                {
                    return i;
                }
            }

            return -1;
        }

        private void Restore(Snapshot snapshot)
        {
            _document = snapshot.Document;
            _document.Reindex();

            var leaves = _document.Leaves;
            var anchor = leaves[Math.Min(snapshot.AnchorLeaf, leaves.Count - 1)];
            var focus = leaves[Math.Min(snapshot.FocusLeaf, leaves.Count - 1)];

            _anchor = new RichPosition(anchor, Math.Min(snapshot.AnchorOffset, anchor.Length));
            _focus = new RichPosition(focus, Math.Min(snapshot.FocusOffset, focus.Length));
            _node = snapshot.Node >= 0 && snapshot.Node < _document.AllBlocks.Count && _document.AllBlocks[snapshot.Node].IsAtomic
                ? _document.AllBlocks[snapshot.Node]
                : null;

            _storedMarks = null;
            _lastEdit = RichEditKind.None;

            Raise(RichEditorChanges.Structure | RichEditorChanges.Selection, null);
        }

        public void Undo()
        {
            if (_undo.Count == 0)
            {
                return;
            }

            _redo.Add(Capture());

            var snapshot = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);

            Restore(snapshot);
        }

        public void Redo()
        {
            if (_redo.Count == 0)
            {
                return;
            }

            _undo.Add(Capture());

            var snapshot = _redo[_redo.Count - 1];
            _redo.RemoveAt(_redo.Count - 1);

            Restore(snapshot);
        }

        /// <summary>
        /// Called before every change. Typing and deleting in the same leaf, at the caret and
        /// in quick succession, extend the transaction already open; a word typed after a
        /// pause starts a new one, so undo takes back words rather than whole paragraphs.
        /// </summary>
        private void BeginEdit(RichEditKind kind)
        {
            _ruleApplied = false;
            _redo.Clear();

            // Inside a rule: the rule's own step is already open.
            if (_suppressUndo > 0)
            {
                return;
            }

            var now = Environment.TickCount;
            var coalesce = kind != RichEditKind.Other
                && kind == _lastEdit
                && IsSelectionEmpty
                && ReferenceEquals(_focus.Leaf, _lastEditLeaf)
                && unchecked(now - _lastEditTime) < TypingCoalesceMilliseconds;

            if (!coalesce)
            {
                _undo.Add(Capture());

                if (_undo.Count > UndoLimit)
                {
                    _undo.RemoveAt(0);
                }
            }

            _redo.Clear();

            _lastEdit = kind;
            _lastEditTime = now;
        }

        /// <summary>After a change: what the next change may coalesce with.</summary>
        private void EndEdit(RichEditKind kind)
        {
            _lastEdit = kind;
            _lastEditLeaf = _focus.Leaf;
            _lastEditTime = Environment.TickCount;
        }

        /// <summary>Closes the open typing transaction: the next keystroke starts a new undo step.</summary>
        public void BreakUndoCoalescing()
        {
            _lastEdit = RichEditKind.None;
        }
    }
}
