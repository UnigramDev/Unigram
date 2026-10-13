//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;

namespace Telegram.Controls.RichEditor
{
    public sealed partial class RichEditorEngine
    {
        // =====================================================================================
        // Typing
        // =====================================================================================

        /// <summary>Inserts text at the selection, replacing it. What every keystroke and the IME end in.</summary>
        public void InsertText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            BeginEdit(RichEditKind.Typing);
            BeginBatch();

            var position = PrepareInsertion();
            var leaf = position.Leaf;

            RichMarks marks;
            RichLink link;

            if (_storedMarks is RichMarks stored)
            {
                leaf.GetInsertionStyle(position.Offset, out _, out link);
                marks = stored;
            }
            else
            {
                leaf.GetInsertionStyle(position.Offset, out marks, out link);
            }

            // A caption, a cell or a header is one line in TDLib's model. Pasted text with line
            // breaks keeps them as newlines inside the leaf, which is how those render them.
            leaf.Insert(position.Offset, text, marks, link);

            var caret = new RichPosition(leaf, position.Offset + text.Length - CountObjectChars(text));
            SetCaretInternal(caret);

            Raise(RichEditorChanges.Text | RichEditorChanges.Selection, leaf);
            EndBatch();

            EndEdit(RichEditKind.Typing);

            if (text == " ")
            {
                TryApplySpaceRule(leaf, caret.Offset);
            }
        }

        /// <summary>
        /// Replaces [start, end) of <paramref name="leaf"/>: what the input service asks for
        /// while composing, and for corrections it offers on text already typed. Coalesces into
        /// one undo step with the typing around it.
        /// </summary>
        public void ReplaceText(RichTextLeaf leaf, int start, int end, string text)
        {
            start = Math.Clamp(start, 0, leaf.Length);
            end = Math.Clamp(end, start, leaf.Length);

            // Collapsed first, so the step coalesces like typing does; the composition's own
            // range is the selection here, not a user's.
            _anchor = _focus = new RichPosition(leaf, start);
            _node = null;

            var kind = string.IsNullOrEmpty(text) ? RichEditKind.Deleting : RichEditKind.Typing;
            BeginEdit(kind);

            RichMarks marks;
            RichLink link;

            if (_storedMarks is RichMarks stored)
            {
                leaf.GetInsertionStyle(start, out _, out link);
                marks = stored;
            }
            else
            {
                leaf.GetInsertionStyle(start, out marks, out link);
            }

            leaf.Delete(start, end);
            leaf.Insert(start, text, marks, link);

            var length = string.IsNullOrEmpty(text) ? 0 : text.Length - CountObjectChars(text);
            _anchor = _focus = new RichPosition(leaf, start + length);
            _storedMarks = null;

            Raise(RichEditorChanges.Text | RichEditorChanges.Selection, leaf);
            EndEdit(kind);

            if (text == " ")
            {
                TryApplySpaceRule(leaf, start + 1);
            }
        }

        private static int CountObjectChars(string text)
        {
            var count = 0;
            foreach (var c in text)
            {
                if (c == RichTextLeaf.ObjectChar)
                {
                    count++;
                }
            }

            return count;
        }

        public void InsertObject(RichInlineObject inline)
        {
            BeginEdit(RichEditKind.Other);
            BeginBatch();

            var position = PrepareInsertion();
            var leaf = position.Leaf;

            if (leaf.IsPlain)
            {
                // Code holds no objects; what the object reads as goes in instead.
                leaf.Insert(position.Offset, inline.PlainText, RichMarks.None, null);
                SetCaretInternal(new RichPosition(leaf, position.Offset + inline.PlainText.Length));
            }
            else
            {
                leaf.GetInsertionStyle(position.Offset, out RichMarks marks, out _);
                leaf.InsertObject(position.Offset, inline, _storedMarks ?? marks);
                SetCaretInternal(new RichPosition(leaf, position.Offset + 1));
            }

            Raise(RichEditorChanges.Text | RichEditorChanges.Selection, leaf);
            EndBatch();

            EndEdit(RichEditKind.Other);
        }

        /// <summary>Replaces the object at <paramref name="position"/>: a formula or a button edited in place.</summary>
        public void ReplaceObject(RichPosition position, RichInlineObject inline)
        {
            var leaf = position.Leaf;
            if (leaf.GetObjectAt(position.Offset) == null)
            {
                return;
            }

            BeginEdit(RichEditKind.Other);

            leaf.GetInsertionStyle(position.Offset + 1, out RichMarks marks, out _);
            leaf.Delete(position.Offset, position.Offset + 1);
            leaf.InsertObject(position.Offset, inline, marks);

            Raise(RichEditorChanges.Text, leaf);
            EndEdit(RichEditKind.Other);
        }

        /// <summary>
        /// Clears what is selected and returns where the insertion goes. A selected block makes
        /// room for text with a new paragraph after it, as a block cannot be typed into.
        /// </summary>
        private RichPosition PrepareInsertion()
        {
            if (_node != null)
            {
                var paragraph = new RichParagraphBlock(RichBlockKind.Paragraph);
                InsertAfter(_node, paragraph);
                _node = null;
                Reindex();

                return new RichPosition(paragraph.Text, 0);
            }

            if (!IsSelectionEmpty)
            {
                return DeleteRange(Start, End);
            }

            return _focus;
        }

        private void SetCaretInternal(RichPosition position)
        {
            _anchor = _focus = position;
            _node = null;
            _storedMarks = null;
        }

        private void Reindex()
        {
            _document.Reindex();
            Raise(RichEditorChanges.Structure, null);
        }

        // =====================================================================================
        // Deleting
        // =====================================================================================

        public void DeleteSelection()
        {
            if (IsSelectionEmpty)
            {
                return;
            }

            BeginEdit(RichEditKind.Other);
            BeginBatch();

            if (_node != null)
            {
                DeleteNode(_node);
            }
            else
            {
                var start = Start;
                var structural = !ReferenceEquals(start.Leaf, End.Leaf);

                SetCaretInternal(DeleteRange(start, End));
                Raise((structural ? RichEditorChanges.Structure : RichEditorChanges.Text) | RichEditorChanges.Selection, start.Leaf);
            }

            EndBatch();
            EndEdit(RichEditKind.Other);
        }

        /// <summary>Backspace. With <paramref name="word"/>, Ctrl+Backspace.</summary>
        public void DeleteBackward(bool word = false)
        {
            if (_ruleApplied && IsSelectionEmpty)
            {
                _ruleApplied = false;
                Undo();
                return;
            }

            if (!IsSelectionEmpty)
            {
                DeleteSelection();
                return;
            }

            var leaf = _focus.Leaf;
            var offset = _focus.Offset;

            if (offset > 0)
            {
                BeginEdit(RichEditKind.Deleting);

                var start = word ? PreviousWordStart(leaf.Text, offset) : PreviousDeletionStop(leaf.Text, offset);
                leaf.Delete(start, offset);
                SetCaretInternal(new RichPosition(leaf, start));

                Raise(RichEditorChanges.Text | RichEditorChanges.Selection, leaf);
                EndEdit(RichEditKind.Deleting);
                return;
            }

            BeginEdit(RichEditKind.Other);
            BeginBatch();
            JoinBackward(leaf);
            EndBatch();
            EndEdit(RichEditKind.Other);
        }

        /// <summary>Delete. With <paramref name="word"/>, Ctrl+Delete.</summary>
        public void DeleteForward(bool word = false)
        {
            if (!IsSelectionEmpty)
            {
                DeleteSelection();
                return;
            }

            var leaf = _focus.Leaf;
            var offset = _focus.Offset;

            if (offset < leaf.Length)
            {
                BeginEdit(RichEditKind.Deleting);

                var end = word ? NextWordEnd(leaf.Text, offset) : NextCaretStop(leaf.Text, offset);
                leaf.Delete(offset, end);

                Raise(RichEditorChanges.Text, leaf);
                EndEdit(RichEditKind.Deleting);
                return;
            }

            BeginEdit(RichEditKind.Other);
            BeginBatch();
            JoinForward(leaf);
            EndBatch();
            EndEdit(RichEditKind.Other);
        }

        /// <summary>
        /// Backspace deletes code points, not grapheme clusters, the way Windows text boxes do -
        /// so an accent typed on its own can be taken back - except across a surrogate pair or
        /// a joined emoji sequence, which would leave garbage.
        /// </summary>
        private static int PreviousDeletionStop(string text, int offset)
        {
            var previous = PreviousCaretStop(text, offset);

            var c = text[offset - 1];
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);

            if (category == System.Globalization.UnicodeCategory.NonSpacingMark && offset - previous > 1)
            {
                return offset - 1;
            }

            return previous;
        }

        /// <summary>
        /// Backspace at the start of a leaf. What it means depends on where the leaf is: a
        /// heading becomes a paragraph, a list item leaves the list, the first line of a quote
        /// leaves the quote, and otherwise the line joins the one before it. A block before it
        /// is selected first, so a second Backspace is what deletes it.
        /// </summary>
        private void JoinBackward(RichTextLeaf leaf)
        {
            var owner = leaf.Owner;

            if (owner is RichParagraphBlock text && leaf.Role == RichLeafRole.Text)
            {
                if (text.Kind != RichBlockKind.Paragraph)
                {
                    text.SetKind(RichBlockKind.Paragraph);
                    Raise(RichEditorChanges.Structure, null);
                    return;
                }

                var container = _document.GetContainer(text, out int index);

                if (text.Parent is RichListBlock list && index == 0)
                {
                    var item = _document.GetListItem(text, out _);
                    LiftListItem(list, item);
                    Reindex();
                    SetCaretInternal(new RichPosition(leaf, 0));
                    Raise(RichEditorChanges.Selection, null);
                    return;
                }

                // Lifting a collapsible block's only line out would leave it empty, which
                // Normalize fills with a new line again: the block goes instead.
                if (text.Parent is RichDetailsBlock only && index == 0 && container.Count == 1)
                {
                    var caret = UnwrapDetails(only);
                    Reindex();
                    SetCaretInternal(Clamp(caret));
                    Raise(RichEditorChanges.Selection, null);
                    return;
                }

                if (text.Parent is RichBlockQuote or RichDetailsBlock && index == 0)
                {
                    LiftOut(text);
                    Reindex();
                    SetCaretInternal(new RichPosition(leaf, 0));
                    Raise(RichEditorChanges.Selection, null);
                    return;
                }
            }
            else if (owner is RichDetailsBlock details && leaf.Role == RichLeafRole.Header)
            {
                // The start of the title is the start of the block: Backspace there takes the
                // block away and leaves what was in it, as it does for a quote's first line.
                var caret = UnwrapDetails(details);
                Reindex();
                SetCaretInternal(Clamp(caret));
                Raise(RichEditorChanges.Selection, null);
                return;
            }
            else if (owner is RichTextQuote quote && leaf.Role == RichLeafRole.Text)
            {
                // A pull quote with no credit is a paragraph with a different look; going back
                // to the paragraph is the same step as a heading's.
                if (quote.Credit.IsEmpty)
                {
                    var paragraph = new RichParagraphBlock(RichBlockKind.Paragraph, leaf);
                    Replace(quote, paragraph);
                    Reindex();
                    SetCaretInternal(new RichPosition(leaf, 0));
                    Raise(RichEditorChanges.Selection, null);
                }

                return;
            }

            var stopIndex = PreviousShownStop(leaf.StopIndex);
            if (stopIndex < 0)
            {
                return;
            }

            var previous = _document.Stops[stopIndex];

            if (previous.Block != null)
            {
                // The block's own caption comes after the block: Backspace from it selects the
                // block, it never deletes the caption's line.
                if (leaf.IsEmpty && owner is RichParagraphBlock empty && empty.Kind == RichBlockKind.Paragraph && !IsOnlyChild(empty))
                {
                    Remove(empty);
                    Reindex();
                }

                SelectNode(previous.Block);
                return;
            }

            var target = previous.Leaf;

            if (CanMerge(target, leaf))
            {
                var caret = new RichPosition(target, target.Length);
                Merge(target, leaf);
                Reindex();

                SetCaretInternal(caret);
                Raise(RichEditorChanges.Selection, null);
            }
            else
            {
                if (leaf.IsEmpty && owner is RichParagraphBlock empty && empty.Kind == RichBlockKind.Paragraph && !IsOnlyChild(empty))
                {
                    Remove(empty);
                    Reindex();
                }

                SetCaretInternal(new RichPosition(target, target.Length));
                Raise(RichEditorChanges.Selection, null);
            }
        }

        // The stops either side of one, past the credits that are not drawn: joining into one
        // would put the caret on a line that appears out of nowhere.
        private int PreviousShownStop(int index)
        {
            var stops = _document.Stops;

            for (index--; index >= 0; index--)
            {
                if (stops[index].Leaf is not RichTextLeaf skipped || !IsLeafHidden(skipped))
                {
                    return index;
                }
            }

            return -1;
        }

        private int NextShownStop(int index)
        {
            var stops = _document.Stops;

            for (index++; index < stops.Count; index++)
            {
                if (stops[index].Leaf is not RichTextLeaf skipped || !IsLeafHidden(skipped))
                {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>Delete at the end of a leaf: the mirror of <see cref="JoinBackward"/>.</summary>
        private void JoinForward(RichTextLeaf leaf)
        {
            var stopIndex = NextShownStop(leaf.StopIndex);
            if (stopIndex < 0)
            {
                return;
            }

            var next = _document.Stops[stopIndex];

            if (next.Block != null)
            {
                // An empty line before a block is removed in favour of the block, as a word
                // processor does; otherwise the block is selected and the next Delete takes it.
                if (leaf.IsEmpty && leaf.Owner is RichParagraphBlock empty && empty.Kind == RichBlockKind.Paragraph && !IsOnlyChild(empty))
                {
                    Remove(empty);
                    Reindex();
                }

                SelectNode(next.Block);
                return;
            }

            if (CanMerge(leaf, next.Leaf))
            {
                Merge(leaf, next.Leaf);
                Reindex();

                SetCaretInternal(new RichPosition(leaf, _focus.Offset));
                Raise(RichEditorChanges.Selection, null);
            }
        }

        /// <summary>
        /// Whether <paramref name="second"/>'s line can be appended to <paramref name="first"/>:
        /// only a block's own text line can move, and it can move anywhere text goes except a
        /// table cell, which would pull text into a table from outside it.
        /// </summary>
        private static bool CanMerge(RichTextLeaf first, RichTextLeaf second)
        {
            if (second.Owner is not RichParagraphBlock || second.Role != RichLeafRole.Text)
            {
                return false;
            }

            return first.Role != RichLeafRole.Cell && first.Owner is not RichTableBlock;
        }

        /// <summary>Appends <paramref name="second"/>'s text to <paramref name="first"/> and removes its block.</summary>
        private void Merge(RichTextLeaf first, RichTextLeaf second)
        {
            first.Append(second);

            var block = (RichParagraphBlock)second.Owner;
            var container = _document.GetContainer(block, out int index);

            // What followed the line inside its list item stays with it: those blocks move to
            // where the line went, rather than being stranded in an item that lost its first line.
            if (block.Parent is RichListBlock && index == 0 && container.Count > 1 && first.Owner is RichParagraphBlock target)
            {
                var item = _document.GetListItem(block, out RichListBlock list);
                var rest = container.GetRange(1, container.Count - 1);

                list.Items.Remove(item);

                var destination = _document.GetContainer(target, out int at);
                destination.InsertRange(at + 1, rest);
                return;
            }

            container.RemoveAt(index);

            // An item emptied by the merge goes with its line.
            if (block.Parent is RichListBlock owner && container.Count == 0)
            {
                foreach (var item in owner.Items)
                {
                    if (ReferenceEquals(item.Children, container))
                    {
                        owner.Items.Remove(item);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Deletes [start, end) across any number of blocks and returns where the caret goes.
        /// Blocks wholly inside go; the two end leaves are trimmed and joined when the second can
        /// move into the first; leaves partly inside a block that stays (cells of a table only
        /// partly selected, a quote's credit) are emptied.
        /// </summary>
        private RichPosition DeleteRange(RichPosition start, RichPosition end)
        {
            var first = start.Leaf;
            var last = end.Leaf;

            if (ReferenceEquals(first, last))
            {
                first.Delete(start.Offset, end.Offset);
                return start;
            }

            var startStop = first.StopIndex;
            var endStop = last.StopIndex;

            var removals = new List<RichBlock>();
            CollectCovered(_document.Blocks, startStop, endStop, removals);

            // A table is cells, which join nothing around them: one the range takes from its
            // first character to its last goes whole, even with an end of the range in it, or
            // selecting everything and deleting leaves an empty table behind.
            AddWholeTable(first.Owner, start, end, removals);
            AddWholeTable(last.Owner, start, end, removals);

            var keepFirst = !IsInside(first.Owner, removals);
            var keepLast = !IsInside(last.Owner, removals);

            // Leaves between the ends that survive, because their block only partly falls in.
            var stops = _document.Stops;
            for (int i = startStop + 1; i < endStop; i++)
            {
                var leaf = stops[i].Leaf;
                if (leaf != null && !IsInside(leaf.Owner, removals))
                {
                    leaf.Delete(0, leaf.Length);
                }
            }

            if (keepFirst)
            {
                first.Delete(start.Offset, first.Length);
            }

            if (keepLast)
            {
                last.Delete(0, end.Offset);
            }

            foreach (var block in removals)
            {
                Remove(block);
            }

            if (keepFirst && keepLast && CanMerge(first, last))
            {
                Merge(first, last);
            }

            _document.Reindex();
            Raise(RichEditorChanges.Structure, null);

            return keepFirst
                ? new RichPosition(first, start.Offset)
                : keepLast ? new RichPosition(last, 0) : Clamp(new RichPosition(first, 0));
        }

        private void AddWholeTable(RichBlock owner, RichPosition start, RichPosition end, List<RichBlock> removals)
        {
            if (owner is not RichTableBlock table || removals.Contains(table) || IsInside(table, removals))
            {
                return;
            }

            var startStop = start.Leaf.StopIndex;
            var endStop = end.Leaf.StopIndex;

            var fromStart = table.FirstStop > startStop || (table.FirstStop == startStop && start.Offset == 0);
            var toEnd = table.LastStop < endStop || (table.LastStop == endStop && end.Offset == end.Leaf.Length);

            if (fromStart && toEnd)
            {
                removals.Add(table);
            }
        }

        private void CollectCovered(List<RichBlock> blocks, int startStop, int endStop, List<RichBlock> removals)
        {
            foreach (var block in blocks)
            {
                if (block.LastStop < block.FirstStop)
                {
                    continue;
                }

                if (block.FirstStop > startStop && block.LastStop < endStop)
                {
                    removals.Add(block);
                }
                else if (block.FirstStop < endStop && block.LastStop > startStop)
                {
                    if (block is RichListBlock list)
                    {
                        // A list item is not a block, but it goes whole the same way.
                        foreach (var item in list.Items)
                        {
                            if (item.Children.Count > 0
                                && item.Children[0].FirstStop > startStop
                                && item.Children[item.Children.Count - 1].LastStop < endStop)
                            {
                                removals.AddRange(item.Children);
                            }
                            else
                            {
                                CollectCovered(item.Children, startStop, endStop, removals);
                            }
                        }
                    }
                    else if (block is not RichMediaGroupBlock)
                    {
                        var lists = new List<List<RichBlock>>();
                        block.CollectChildLists(lists);

                        foreach (var children in lists)
                        {
                            CollectCovered(children, startStop, endStop, removals);
                        }
                    }
                }
            }
        }

        private static bool IsInside(RichBlock block, List<RichBlock> blocks)
        {
            for (var current = block; current != null; current = current.Parent)
            {
                if (blocks.Contains(current))
                {
                    return true;
                }
            }

            return false;
        }

        // =====================================================================================
        // Enter
        // =====================================================================================

        /// <summary>
        /// Enter. Splits the line in two, with the rules people expect from a document editor:
        /// an empty list item leaves the list, an empty last line leaves a quote, code and cells
        /// take a newline, a caption's Enter goes to a new paragraph after its block.
        /// </summary>
        public void InsertParagraph()
        {
            if (TryApplyEnterRule())
            {
                return;
            }

            BeginEdit(RichEditKind.Other);
            BeginBatch();

            var position = PrepareInsertion();
            var leaf = position.Leaf;
            var offset = position.Offset;
            var owner = leaf.Owner;

            switch (owner)
            {
                case RichParagraphBlock text when leaf.Role == RichLeafRole.Text:
                    SplitTextBlock(text, offset);
                    break;

                case RichTextQuote quote when leaf.Role == RichLeafRole.Text:
                    EnterInMultilineLeaf(quote, leaf, offset);
                    break;

                case RichDetailsBlock details when leaf.Role == RichLeafRole.Header:
                    {
                        details.IsOpen = true;
                        Reindex();

                        var first = FirstLeafOf(details.Children);
                        SetCaretInternal(new RichPosition(first, 0));
                        break;
                    }

                case RichTableBlock when leaf.Role == RichLeafRole.Cell:
                    leaf.Insert(offset, "\n", RichMarks.None, null);
                    SetCaretInternal(new RichPosition(leaf, offset + 1));
                    Raise(RichEditorChanges.Text, leaf);
                    break;

                default:
                    {
                        // Captions, credits: a new paragraph right after the block they belong to.
                        var paragraph = new RichParagraphBlock(RichBlockKind.Paragraph);
                        InsertAfter(owner, paragraph);
                        Reindex();
                        SetCaretInternal(new RichPosition(paragraph.Text, 0));
                        break;
                    }
            }

            Raise(RichEditorChanges.Selection, null);
            EndBatch();
            EndEdit(RichEditKind.Other);
        }

        /// <summary>Shift+Enter: a line break inside the leaf.</summary>
        public void InsertLineBreak()
        {
            InsertText("\n");
        }

        private void SplitTextBlock(RichParagraphBlock text, int offset)
        {
            var leaf = text.Text;

            if (text.Kind == RichBlockKind.Preformatted)
            {
                EnterInMultilineLeaf(text, leaf, offset);
                return;
            }

            var container = _document.GetContainer(text, out int index);

            if (text.Parent is RichListBlock list && index == 0)
            {
                var item = _document.GetListItem(text, out _);

                if (leaf.IsEmpty && item.Children.Count == 1)
                {
                    LiftListItem(list, item);
                    Reindex();
                    SetCaretInternal(new RichPosition(leaf, 0));
                    return;
                }

                // A new item, carrying the rest of the line and whatever followed it in this item.
                var tail = leaf.Split(offset, RichLeafRole.Text);
                var paragraph = new RichParagraphBlock(RichBlockKind.Paragraph, tail);

                var next = new RichListItem();
                next.Children.Add(paragraph);
                next.Children.AddRange(item.Children.GetRange(1, item.Children.Count - 1));
                item.Children.RemoveRange(1, item.Children.Count - 1);

                list.Items.Insert(list.Items.IndexOf(item) + 1, next);

                Reindex();
                SetCaretInternal(new RichPosition(tail, 0));
                return;
            }

            // An empty last line inside a quote or a collapsible block is how one gets out of it.
            if (leaf.IsEmpty && text.Kind == RichBlockKind.Paragraph && index == container.Count - 1 && container.Count > 1
                && text.Parent is RichBlockQuote or RichDetailsBlock or RichListBlock)
            {
                container.RemoveAt(index);

                if (text.Parent is RichListBlock parentList)
                {
                    var item = FindItem(parentList, container);
                    InsertListItemSibling(parentList, item, text);
                }
                else
                {
                    InsertAfter(text.Parent, text);
                }

                Reindex();
                SetCaretInternal(new RichPosition(leaf, 0));
                return;
            }

            // At the start of a non-empty line, the new line goes above and the caret stays: the
            // heading keeps its kind instead of a paragraph appearing with the heading's text.
            // A footer is a heading's kind of block here: what Enter starts is a paragraph.
            if (offset == 0 && !leaf.IsEmpty)
            {
                var above = new RichParagraphBlock(RichBlockKind.Paragraph);
                container.Insert(index, above);
                Reindex();
                SetCaretInternal(new RichPosition(leaf, 0));
                return;
            }

            {
                var tail = leaf.Split(offset, RichLeafRole.Text);
                var paragraph = new RichParagraphBlock(RichBlockKind.Paragraph, tail);
                container.Insert(index + 1, paragraph);

                Reindex();
                SetCaretInternal(new RichPosition(tail, 0));
            }
        }

        /// <summary>
        /// Code blocks and pull quotes take Enter as a newline; Enter on an empty last line
        /// removes that line and leaves the block for a new paragraph below it.
        /// </summary>
        private void EnterInMultilineLeaf(RichBlock block, RichTextLeaf leaf, int offset)
        {
            if (offset == leaf.Length && leaf.Length > 0 && leaf.Text[leaf.Length - 1] == '\n')
            {
                leaf.Delete(leaf.Length - 1, leaf.Length);

                var paragraph = new RichParagraphBlock(RichBlockKind.Paragraph);
                InsertAfter(block, paragraph);
                Reindex();
                SetCaretInternal(new RichPosition(paragraph.Text, 0));
                return;
            }

            leaf.Insert(offset, "\n", RichMarks.None, null);
            SetCaretInternal(new RichPosition(leaf, offset + 1));
            Raise(RichEditorChanges.Text, leaf);
        }

        // =====================================================================================
        // Marks and links
        // =====================================================================================

        /// <summary>
        /// Ctrl+B and friends. On a range: on everywhere unless it is on everywhere already,
        /// then off. On a caret: toggles what the next typed text gets.
        /// </summary>
        public void ToggleMark(RichMarks mark)
        {
            if (_node != null)
            {
                return;
            }

            if (IsSelectionEmpty)
            {
                var current = _storedMarks ?? GetInsertionMarks();
                _storedMarks = current ^ mark;

                if ((_storedMarks.Value & mark) != 0)
                {
                    if (mark == RichMarks.Subscript)
                    {
                        _storedMarks &= ~RichMarks.Superscript;
                    }
                    else if (mark == RichMarks.Superscript)
                    {
                        _storedMarks &= ~RichMarks.Subscript;
                    }
                }

                Raise(RichEditorChanges.Selection, null);
                return;
            }

            var value = (GetSelectionMarks() & mark) == 0;

            BeginEdit(RichEditKind.Other);
            BeginBatch();

            ForEachSelectedRange(out bool multiple, (leaf, start, end) => leaf.SetMark(start, end, mark, value));
            Raise(multiple ? RichEditorChanges.Structure : RichEditorChanges.Text, _focus.Leaf);

            EndBatch();
            EndEdit(RichEditKind.Other);
        }

        public void ClearFormatting()
        {
            if (_node != null || IsSelectionEmpty)
            {
                _storedMarks = RichMarks.None;
                return;
            }

            BeginEdit(RichEditKind.Other);
            BeginBatch();

            ForEachSelectedRange(out bool multiple, (leaf, start, end) =>
            {
                leaf.ClearMarks(start, end);
                leaf.SetLink(start, end, null);
            });

            Raise(multiple ? RichEditorChanges.Structure : RichEditorChanges.Text, _focus.Leaf);

            EndBatch();
            EndEdit(RichEditKind.Other);
        }

        /// <summary>
        /// Sets the link over the selection, or removes it with null. With a caret inside a link
        /// the whole link is changed; with a caret anywhere else, <paramref name="text"/> is
        /// inserted as the link's text.
        /// </summary>
        public void SetLink(RichLink link, string text = null)
        {
            if (_node != null)
            {
                return;
            }

            BeginEdit(RichEditKind.Other);
            BeginBatch();

            if (IsSelectionEmpty)
            {
                var leaf = _focus.Leaf;

                if (leaf.TryGetLinkRange(_focus.Offset, out int start, out int end) && string.IsNullOrEmpty(text))
                {
                    leaf.SetLink(start, end, link);
                    Raise(RichEditorChanges.Text, leaf);
                }
                else if (!string.IsNullOrEmpty(text) && link != null && !leaf.IsPlain)
                {
                    leaf.GetInsertionStyle(_focus.Offset, out RichMarks marks, out _);
                    leaf.Insert(_focus.Offset, text, marks, link);
                    SetCaretInternal(new RichPosition(leaf, _focus.Offset + text.Length));
                    Raise(RichEditorChanges.Text | RichEditorChanges.Selection, leaf);
                }
            }
            else
            {
                ForEachSelectedRange(out bool multiple, (leaf, start, end) => leaf.SetLink(start, end, link));
                Raise(multiple ? RichEditorChanges.Structure : RichEditorChanges.Text, _focus.Leaf);
            }

            EndBatch();
            EndEdit(RichEditKind.Other);
        }

        private RichMarks GetInsertionMarks()
        {
            _focus.Leaf.GetInsertionStyle(_focus.Offset, out RichMarks marks, out _);
            return marks;
        }

        /// <summary>The marks on all of the selection, or the ones typing would use at a caret.</summary>
        public RichMarks GetSelectionMarks()
        {
            if (_node != null)
            {
                return RichMarks.None;
            }

            if (IsSelectionEmpty)
            {
                return _storedMarks ?? GetInsertionMarks();
            }

            var start = Start;
            var end = End;
            var common = (RichMarks)ushort.MaxValue;
            var any = false;

            var leaves = _document.Leaves;
            for (int i = start.Leaf.Index; i <= end.Leaf.Index; i++)
            {
                var leaf = leaves[i];
                var from = i == start.Leaf.Index ? start.Offset : 0;
                var to = i == end.Leaf.Index ? end.Offset : leaf.Length;

                if (to > from && !leaf.IsPlain)
                {
                    common &= leaf.GetCommonMarks(from, to);
                    any = true;
                }
            }

            return any ? common : RichMarks.None;
        }

        /// <summary>The link the selection is wholly inside, or the caret is in.</summary>
        public RichLink GetSelectionLink()
        {
            if (_node != null)
            {
                return null;
            }

            var start = Start;
            var end = End;

            if (!ReferenceEquals(start.Leaf, end.Leaf))
            {
                return null;
            }

            return start.Leaf.GetCommonLink(start.Offset, end.Offset);
        }

        /// <summary>The plain text of the selection, leaves joined by newlines.</summary>
        public string GetSelectedText()
        {
            if (_node != null || IsSelectionEmpty)
            {
                return string.Empty;
            }

            var start = Start;
            var end = End;
            var builder = new System.Text.StringBuilder();

            var leaves = _document.Leaves;
            for (int i = start.Leaf.Index; i <= end.Leaf.Index; i++)
            {
                var leaf = leaves[i];
                var from = i == start.Leaf.Index ? start.Offset : 0;
                var to = i == end.Leaf.Index ? end.Offset : leaf.Length;

                if (i > start.Leaf.Index)
                {
                    builder.Append('\n');
                }

                builder.Append(leaf.ToPlainText(from, to));
            }

            return builder.ToString();
        }

        private void ForEachSelectedRange(out bool multiple, Action<RichTextLeaf, int, int> action)
        {
            var start = Start;
            var end = End;

            multiple = !ReferenceEquals(start.Leaf, end.Leaf);

            var leaves = _document.Leaves;
            for (int i = start.Leaf.Index; i <= end.Leaf.Index; i++)
            {
                var leaf = leaves[i];
                var from = i == start.Leaf.Index ? start.Offset : 0;
                var to = i == end.Leaf.Index ? end.Offset : leaf.Length;

                if (to > from)
                {
                    action(leaf, from, to);
                }
            }
        }
    }
}
