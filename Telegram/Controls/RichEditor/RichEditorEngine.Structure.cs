//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using Telegram.Td.Api;

namespace Telegram.Controls.RichEditor
{
    public sealed partial class RichEditorEngine
    {
        // =====================================================================================
        // Edits made outside the engine
        // =====================================================================================

        /// <summary>
        /// Opens an undo step for a change the caller makes to a block directly - a button
        /// edited in its popup, a spoiler toggled on a photo. Pair with <see cref="EndChange"/>.
        /// Engine edits made in between join the step, so a paste of several lines undoes as one.
        /// </summary>
        public void BeginChange()
        {
            BeginEdit(RichEditKind.Other);
            BeginBatch();
            _suppressUndo++;
        }

        public void EndChange()
        {
            _suppressUndo--;
            Reindex();
            EndBatch();
            EndEdit(RichEditKind.Other);
        }

        // =====================================================================================
        // Tree helpers
        // =====================================================================================

        private void InsertAfter(RichBlock anchor, RichBlock block)
        {
            // Blocks never go inside a collage; after one of its items means after the collage.
            while (anchor.Parent is RichMediaGroupBlock group)
            {
                anchor = group;
            }

            var container = _document.GetContainer(anchor, out int index);
            container.Insert(index + 1, block);
        }

        private void InsertBefore(RichBlock anchor, RichBlock block)
        {
            while (anchor.Parent is RichMediaGroupBlock group)
            {
                anchor = group;
            }

            var container = _document.GetContainer(anchor, out int index);
            container.Insert(index, block);
        }

        private void Remove(RichBlock block)
        {
            var container = _document.GetContainer(block, out int index);
            if (index >= 0)
            {
                container.RemoveAt(index);
            }
        }

        private void Replace(RichBlock block, RichBlock replacement)
        {
            var container = _document.GetContainer(block, out int index);
            if (index >= 0)
            {
                container[index] = replacement;

                // Stamped now, not at the next reindex: an insertion relative to the replacement
                // may follow before then, and it finds its container through the parent.
                replacement.Parent = block.Parent;
            }
        }

        private bool IsOnlyChild(RichBlock block)
        {
            var container = _document.GetContainer(block);
            return container == null || container.Count == 1;
        }

        private static RichTextLeaf FirstLeafOf(List<RichBlock> blocks)
        {
            var stops = new List<RichStop>();
            foreach (var block in blocks)
            {
                block.CollectStops(stops);

                foreach (var stop in stops)
                {
                    if (stop.Leaf != null)
                    {
                        return stop.Leaf;
                    }
                }

                stops.Clear();
            }

            return null;
        }

        private static RichListItem FindItem(RichListBlock list, List<RichBlock> children)
        {
            foreach (var item in list.Items)
            {
                if (ReferenceEquals(item.Children, children))
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>Moves <paramref name="block"/> out of an item into a new item right after it.</summary>
        private static void InsertListItemSibling(RichListBlock list, RichListItem item, RichBlock block)
        {
            var next = new RichListItem();
            next.Children.Add(block);
            list.Items.Insert(list.Items.IndexOf(item) + 1, next);
        }

        /// <summary>
        /// Shift+Tab, and Backspace or Enter on an empty item: one level out. A nested item
        /// becomes an item of the list around it, taking the items after it along as its own
        /// sublist so the reading order stays; a top-level item stops being a list item and
        /// splits the list in two.
        /// </summary>
        private void LiftListItem(RichListBlock list, RichListItem item)
        {
            var container = _document.GetContainer(list, out int listIndex);
            var k = list.Items.IndexOf(item);

            var after = list.Items.GetRange(k + 1, list.Items.Count - k - 1);
            list.Items.RemoveRange(k, list.Items.Count - k);

            if (list.Parent is RichListBlock parentList && FindItem(parentList, container) is RichListItem parentItem)
            {
                if (after.Count > 0)
                {
                    item.Children.Add(CopyList(list, after));
                }

                parentList.Items.Insert(parentList.Items.IndexOf(parentItem) + 1, item);
            }
            else
            {
                container.InsertRange(listIndex + 1, item.Children);

                if (after.Count > 0)
                {
                    container.Insert(listIndex + 1 + item.Children.Count, CopyList(list, after));
                }
            }
        }

        private static RichListBlock CopyList(RichListBlock list, List<RichListItem> items)
        {
            return new RichListBlock(list.Style, items)
            {
                OrderedType = list.OrderedType,
                Start = list.Start
            };
        }

        /// <summary>The first line of a quote or a collapsible block, moved out above it.</summary>
        private void LiftOut(RichBlock block)
        {
            var parent = block.Parent;
            var container = _document.GetContainer(block, out int index);
            container.RemoveAt(index);

            if (container.Count == 0)
            {
                Replace(parent, block);
            }
            else
            {
                InsertBefore(parent, block);
            }
        }

        private void DeleteNode(RichBlock block)
        {
            if (block.Parent is RichMediaGroupBlock group)
            {
                // Where the caret goes if the collage goes too, worked out while it is still there.
                var fallback = PositionBefore(group) ?? new RichPosition(_document.Leaves[0], 0);

                var index = group.Items.IndexOf(block);
                group.Items.RemoveAt(index);
                _node = null;
                Reindex();

                // The collage goes with its last item, in the reindex, and with its second to
                // last the item left takes its place.
                if (group.Items.Count > 0 && _document.GetStopIndex(group) >= 0)
                {
                    SelectNode(group.Items[System.Math.Min(index, group.Items.Count - 1)]);
                }
                else if (group.Items.Count == 1 && _document.GetStopIndex(group.Items[0]) >= 0)
                {
                    SelectNode(group.Items[0]);
                }
                else
                {
                    SetCaretInternal(Clamp(fallback));
                    Raise(RichEditorChanges.Selection, null);
                }

                return;
            }

            var before = PositionBefore(block);
            var after = PositionAfter(block);

            // The block's own caption is after it and goes with it, so "after" is past that.
            if (after is RichPosition next && IsInside(next.Leaf.Owner, block))
            {
                after = null;
                var stop = block.LastStop + 1;

                for (int i = stop; i < _document.Stops.Count; i++)
                {
                    if (_document.Stops[i].Leaf is RichTextLeaf leaf)
                    {
                        after = new RichPosition(leaf, 0);
                        break;
                    }
                }
            }

            Remove(block);
            _node = null;
            Reindex();

            var caret = after ?? before ?? new RichPosition(_document.Leaves[0], 0);
            SetCaretInternal(Clamp(caret));
            Raise(RichEditorChanges.Selection, null);
        }

        private static bool IsInside(RichBlock block, RichBlock ancestor)
        {
            for (var current = block; current != null; current = current.Parent)
            {
                if (ReferenceEquals(current, ancestor))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The run of sibling blocks the selection touches, at the shallowest level where its two
        /// ends share a container: what wrapping in a quote or a list applies to.
        /// </summary>
        private bool GetSelectedSiblings(out List<RichBlock> container, out int first, out int last)
        {
            RichBlock a = _node ?? Start.Leaf.Owner;
            RichBlock b = _node ?? End.Leaf.Owner;

            for (var x = a; x != null; x = x.Parent)
            {
                if (x.Parent is RichMediaGroupBlock)
                {
                    continue;
                }

                var candidate = _document.GetContainer(x);
                if (candidate == null)
                {
                    continue;
                }

                for (var y = b; y != null; y = y.Parent)
                {
                    if (ReferenceEquals(_document.GetContainer(y), candidate))
                    {
                        container = candidate;
                        first = candidate.IndexOf(x);
                        last = candidate.IndexOf(y);

                        if (first > last)
                        {
                            (first, last) = (last, first);
                        }

                        return true;
                    }
                }
            }

            container = null;
            first = last = -1;
            return false;
        }

        private T FindAncestor<T>(RichBlock block) where T : RichBlock
        {
            for (var current = block; current != null; current = current.Parent)
            {
                if (current is T match)
                {
                    return match;
                }
            }

            return null;
        }

        // =====================================================================================
        // Block kinds
        // =====================================================================================

        /// <summary>Paragraph, heading (with <paramref name="headingSize"/>), code or footer, for every line selected.</summary>
        public void SetTextKind(RichBlockKind kind, int headingSize = 2)
        {
            if (_node != null)
            {
                return;
            }

            BeginChange();

            var start = Start;
            var end = End;
            var leaves = _document.Leaves;

            for (int i = start.Leaf.Index; i <= end.Leaf.Index; i++)
            {
                var leaf = leaves[i];
                if (leaf.Role != RichLeafRole.Text)
                {
                    continue;
                }

                if (leaf.Owner is RichParagraphBlock text)
                {
                    text.SetKind(kind);

                    if (kind == RichBlockKind.Heading)
                    {
                        text.HeadingSize = headingSize;
                    }
                }
                else if (leaf.Owner is RichTextQuote quote)
                {
                    var block = new RichParagraphBlock(kind, leaf) { HeadingSize = headingSize };
                    block.SetKind(kind);
                    Replace(quote, block);

                    if (!quote.Credit.IsEmpty)
                    {
                        InsertAfter(block, new RichParagraphBlock(RichBlockKind.Paragraph, quote.Credit));
                    }
                }
            }

            EndChange();
        }

        public void SetLanguage(RichParagraphBlock block, string language)
        {
            BeginChange();
            block.Language = language ?? string.Empty;
            EndChange();
        }

        /// <summary>The selected line as a pull quote, or a pull quote back to a paragraph.</summary>
        public void TogglePullQuote()
        {
            if (_node != null)
            {
                return;
            }

            var leaf = _focus.Leaf;

            if (leaf.Owner is RichTextQuote quote && !quote.IsExpandable)
            {
                SetTextKind(RichBlockKind.Paragraph);
                return;
            }

            if (leaf.Owner is RichParagraphBlock text && leaf.Role == RichLeafRole.Text)
            {
                BeginChange();

                text.SetKind(RichBlockKind.Paragraph);
                Replace(text, new RichTextQuote(false, leaf));

                EndChange();
            }
        }

        /// <summary>
        /// Wraps the selected blocks in a quote, or - with the selection inside a quote -
        /// unwraps it. A credit that would be lost by unwrapping stays, as a paragraph after.
        /// </summary>
        public void ToggleBlockQuote()
        {
            var quote = FindAncestor<RichBlockQuote>(_node ?? Start.Leaf.Owner);
            if (quote != null && IsInside(_node ?? End.Leaf.Owner, quote))
            {
                BeginChange();

                var container = _document.GetContainer(quote, out int index);
                container.RemoveAt(index);
                container.InsertRange(index, quote.Children);

                if (!quote.Credit.IsEmpty)
                {
                    container.Insert(index + quote.Children.Count, new RichParagraphBlock(RichBlockKind.Paragraph, quote.Credit));
                }

                EndChange();
                return;
            }

            if (!GetSelectedSiblings(out List<RichBlock> siblings, out int first, out int last))
            {
                return;
            }

            BeginChange();

            var children = siblings.GetRange(first, last - first + 1);
            siblings.RemoveRange(first, last - first + 1);
            siblings.Insert(first, new RichBlockQuote(children));

            EndChange();
        }

        /// <summary>
        /// Wraps the selected blocks in a collapsible block with an empty title, and puts the
        /// caret in the title.
        /// </summary>
        public void InsertDetails()
        {
            if (!GetSelectedSiblings(out List<RichBlock> siblings, out int first, out int last))
            {
                return;
            }

            BeginChange();

            var children = siblings.GetRange(first, last - first + 1);
            siblings.RemoveRange(first, last - first + 1);

            var details = new RichDetailsBlock(null, children);
            siblings.Insert(first, details);

            EndChange();

            SetCaretInternal(new RichPosition(details.Header, 0));
            Raise(RichEditorChanges.Selection, null);
        }

        /// <summary>The collapsible block the caret is in, out of it; or the selection into a new one.</summary>
        public void ToggleDetails()
        {
            if (GetFocusedDetails() is RichDetailsBlock details)
            {
                RemoveDetails(details);
            }
            else
            {
                InsertDetails();
            }
        }

        /// <summary>The innermost collapsible block around the caret, its title included.</summary>
        internal RichDetailsBlock GetFocusedDetails()
        {
            for (var block = _node ?? _focus.Leaf?.Owner; block != null; block = block.Parent)
            {
                if (block is RichDetailsBlock details)
                {
                    return details;
                }
            }

            return null;
        }

        /// <summary>
        /// A collapsible block back to what is in it, its title - when it has one - as the line
        /// before. The caret stays where it was, in the title's line if it was in the title.
        /// </summary>
        public void RemoveDetails(RichDetailsBlock details)
        {
            BeginChange();
            var caret = UnwrapDetails(details);
            _node = null;
            EndChange();

            SetCaretInternal(Clamp(caret));
            Raise(RichEditorChanges.Selection, null);
        }

        /// <summary>The structural half of <see cref="RemoveDetails"/>, for an edit already open; returns where the caret goes.</summary>
        private RichPosition UnwrapDetails(RichDetailsBlock details)
        {
            var container = _document.GetContainer(details, out int index);
            container.RemoveAt(index);

            RichParagraphBlock title = null;
            if (!details.Header.IsEmpty)
            {
                title = new RichParagraphBlock(RichBlockKind.Paragraph);
                title.Text.SetContent(details.Header);
                container.Insert(index++, title);
            }

            container.InsertRange(index, details.Children);

            if (!ReferenceEquals(_focus.Leaf, details.Header))
            {
                return _focus;
            }

            return title != null
                ? new RichPosition(title.Text, _focus.Offset)
                : new RichPosition(FirstLeafOf(details.Children), 0);
        }

        /// <summary>
        /// Down from the document's last block when that is not a line of text - a quote, a list,
        /// a collapsible block, a table, media: a new line after it, which nothing else reaches.
        /// False when there is a line to go to already, or the caret is not in that block.
        /// </summary>
        public bool AppendLineAfterLast()
        {
            var blocks = _document.Blocks;
            if (blocks.Count == 0 || blocks[blocks.Count - 1] is RichParagraphBlock or RichTextQuote)
            {
                return false;
            }

            var root = _node ?? _focus.Leaf?.Owner;
            while (root?.Parent != null)
            {
                root = root.Parent;
            }

            if (!ReferenceEquals(root, blocks[blocks.Count - 1]))
            {
                return false;
            }

            var paragraph = new RichParagraphBlock(RichBlockKind.Paragraph);

            BeginChange();
            blocks.Add(paragraph);
            _node = null;
            EndChange();

            SetCaretInternal(new RichPosition(paragraph.Text, 0));
            Raise(RichEditorChanges.Selection, null);
            return true;
        }

        public void SetDetailsOpen(RichDetailsBlock details, bool open)
        {
            if (details.IsOpen == open)
            {
                return;
            }

            BeginChange();
            details.IsOpen = open;

            // Closing it with the caret inside would leave the caret on a leaf no longer drawn.
            if (!open && _focus.Leaf.Owner != null && IsInside(_focus.Leaf.Owner, details) && !ReferenceEquals(_focus.Leaf, details.Header))
            {
                SetCaretInternal(new RichPosition(details.Header, details.Header.Length));
            }

            if (!open && _anchor.Leaf.Owner != null && IsInside(_anchor.Leaf.Owner, details) && !ReferenceEquals(_anchor.Leaf, details.Header))
            {
                _anchor = _focus;
            }

            EndChange();
        }

        // =====================================================================================
        // Lists
        // =====================================================================================

        /// <summary>
        /// The toolbar's list buttons. Inside a list of the same style the selected items leave
        /// the list; inside one of another style the list changes style; outside a list the
        /// selected blocks become one item each.
        /// </summary>
        public void ToggleList(RichListStyle style)
        {
            var anchor = _node ?? Start.Leaf.Owner;
            var list = FindAncestor<RichListBlock>(anchor);

            if (list != null && list.Style != style)
            {
                BeginChange();
                list.Style = style;
                EndChange();
                return;
            }

            if (list != null)
            {
                RemoveList();
                return;
            }

            if (!GetSelectedSiblings(out List<RichBlock> siblings, out int first, out int last))
            {
                return;
            }

            BeginChange();

            var created = new RichListBlock(style);

            for (int i = first; i <= last; i++)
            {
                if (siblings[i] is RichListBlock existing)
                {
                    created.Items.AddRange(existing.Items);
                }
                else
                {
                    var item = new RichListItem();
                    item.Children.Add(siblings[i]);
                    created.Items.Add(item);
                }
            }

            siblings.RemoveRange(first, last - first + 1);
            siblings.Insert(first, created);

            EndChange();
        }

        /// <summary>The selected items out of their list, back to plain blocks.</summary>
        public void RemoveList()
        {
            var startItem = GetItemOf(Start.Leaf.Owner, out RichListBlock list);
            if (startItem == null)
            {
                return;
            }

            var endItem = GetItemOf(End.Leaf.Owner, out RichListBlock endList);
            if (!ReferenceEquals(list, endList))
            {
                endItem = list.Items[list.Items.Count - 1];
            }

            BeginChange();

            var first = list.Items.IndexOf(startItem);
            var last = list.Items.IndexOf(endItem);

            for (int i = last; i >= first; i--)
            {
                // A nested item comes out one level per press; a top-level one becomes text.
                LiftListItem(list, list.Items[i]);
            }

            EndChange();
        }

        /// <summary>Tab inside a list item: under the item before it.</summary>
        public bool Indent(int maxDepth)
        {
            var item = GetItemOf(_focus.Leaf.Owner, out RichListBlock list);
            if (item == null)
            {
                return false;
            }

            var index = list.Items.IndexOf(item);
            if (index == 0)
            {
                return false;
            }

            // Each level costs a list and an item block: refusing early here is clearer than a
            // send that fails on depth.
            if (RichDocument.GetDepth(list) + 2 > maxDepth)
            {
                return false;
            }

            BeginChange();

            var previous = list.Items[index - 1];
            list.Items.RemoveAt(index);

            if (previous.Children.Count > 0 && previous.Children[previous.Children.Count - 1] is RichListBlock nested && nested.Style == list.Style)
            {
                nested.Items.Add(item);
            }
            else
            {
                var created = CopyList(list, new List<RichListItem>());
                created.Items.Add(item);
                previous.Children.Add(created);
            }

            EndChange();
            return true;
        }

        /// <summary>Shift+Tab inside a list item.</summary>
        public bool Outdent()
        {
            var item = GetItemOf(_focus.Leaf.Owner, out RichListBlock list);
            if (item == null)
            {
                return false;
            }

            BeginChange();
            LiftListItem(list, item);
            EndChange();

            return true;
        }

        public void SetChecked(RichListItem item, bool value)
        {
            if (item.IsChecked == value)
            {
                return;
            }

            BeginChange();
            item.IsChecked = value;
            EndChange();
        }

        /// <summary>The list item <paramref name="block"/> sits in, through any blocks nested in the item.</summary>
        public RichListItem GetItemOf(RichBlock block, out RichListBlock list)
        {
            for (var current = block; current != null; current = current.Parent)
            {
                if (current.Parent is RichListBlock parent)
                {
                    list = parent;
                    return _document.GetListItem(current, out _);
                }
            }

            list = null;
            return null;
        }

        // =====================================================================================
        // Inserting blocks
        // =====================================================================================

        /// <summary>
        /// Puts a block at the caret: in place of an empty paragraph, before or after the line
        /// at its edges, or between the two halves of a line split at the caret. A block that
        /// cannot hold a caret gets a paragraph after it when nothing editable follows, so there
        /// is always somewhere to keep typing.
        /// </summary>
        public void InsertBlock(RichBlock block)
        {
            BeginChange();

            if (_node != null)
            {
                InsertAfter(_node, block);
                _node = null;
            }
            else
            {
                if (!IsSelectionEmpty)
                {
                    SetCaretInternal(DeleteRange(Start, End));
                }

                var leaf = _focus.Leaf;
                var offset = _focus.Offset;

                if (leaf.Owner is RichParagraphBlock text && leaf.Role == RichLeafRole.Text && text.Kind != RichBlockKind.Preformatted)
                {
                    var container = _document.GetContainer(text, out int index);

                    if (leaf.IsEmpty && text.Kind == RichBlockKind.Paragraph)
                    {
                        container[index] = block;
                    }
                    else if (offset == 0)
                    {
                        container.Insert(index, block);
                    }
                    else if (offset == leaf.Length)
                    {
                        container.Insert(index + 1, block);
                    }
                    else
                    {
                        var tail = leaf.Split(offset, RichLeafRole.Text);
                        container.Insert(index + 1, block);
                        container.Insert(index + 2, new RichParagraphBlock(text.Kind, tail) { HeadingSize = text.HeadingSize });
                    }
                }
                else
                {
                    InsertAfter(TopOf(leaf.Owner), block);
                }
            }

            // Indexed first: the check finds the block's container through its parent.
            _document.Reindex();
            EnsureParagraphAfter(block);
            _document.Reindex();

            if (block.IsAtomic)
            {
                _node = block;

                var near = PositionAfter(block) ?? PositionBefore(block);
                if (near is RichPosition position)
                {
                    _anchor = _focus = position;
                }
            }
            else
            {
                var first = FirstLeafOf(new List<RichBlock> { block });
                if (first != null)
                {
                    SetCaretInternal(new RichPosition(first, 0));
                }
            }

            EndChange();
            Raise(RichEditorChanges.Selection, null);
        }

        /// <summary>A block whose leaf is a cell or a caption is inserted after; this is the block it belongs to.</summary>
        private static RichBlock TopOf(RichBlock block)
        {
            while (block.Parent is RichMediaGroupBlock group)
            {
                block = group;
            }

            return block;
        }

        private void EnsureParagraphAfter(RichBlock block)
        {
            var container = _document.GetContainer(block, out int index);
            if (container == null)
            {
                return;
            }

            if (index == container.Count - 1 || container[index + 1] is not RichParagraphBlock)
            {
                if (block.IsAtomic || block is RichTableBlock)
                {
                    container.Insert(index + 1, new RichParagraphBlock(RichBlockKind.Paragraph));
                }
            }
        }

        public void InsertDivider() => InsertBlock(new RichDividerBlock());

        public void InsertMathBlock(string expression) => InsertBlock(new RichMathBlock(expression));

        public void InsertAnchor(string name) => InsertBlock(new RichAnchorBlock(name));

        public void InsertButtonRow(IList<InlineButton> buttons, PageBlockHorizontalAlignment align = null)
        {
            InsertBlock(new RichButtonRowBlock(new List<InlineButton>(buttons)) { Align = align });
        }

        /// <summary>
        /// Several media at once go in as one collage, as an album does in a chat; one goes in as
        /// a single block.
        /// </summary>
        /// <summary>
        /// Media at the caret, in the order given: photos and videos picked together are one
        /// collage, and anything else - a file, audio - is a block of its own, as it is sent.
        /// </summary>
        public void InsertMedia(IList<RichMediaSource> sources)
        {
            if (sources == null || sources.Count == 0)
            {
                return;
            }

            // One step, however many blocks: each insert after the last, which it selects.
            BeginChange();

            for (int i = 0; i < sources.Count;)
            {
                var end = i;
                while (end < sources.Count && CanGroup(sources[end]))
                {
                    end++;
                }

                if (end - i > 1)
                {
                    var group = new RichMediaGroupBlock(false);
                    for (int j = i; j < end; j++)
                    {
                        group.Items.Add(new RichMediaBlock(sources[j]));
                    }

                    InsertBlock(group);
                    i = end;
                }
                else
                {
                    InsertBlock(new RichMediaBlock(sources[i]));
                    i++;
                }
            }

            EndChange();
        }

        public void InsertMap(Location location, string caption = null, int zoom = 15, int width = 600, int height = 400)
        {
            var map = new RichMapBlock(location, zoom, width, height);

            if (!string.IsNullOrEmpty(caption))
            {
                map.Caption.Insert(0, caption, RichMarks.None, null);
            }

            InsertBlock(map);
        }

        public void InsertTable(int rows, int columns)
        {
            var table = new RichTableBlock();

            for (int i = 0; i < rows; i++)
            {
                var row = new List<RichTableCell>(columns);
                for (int j = 0; j < columns; j++)
                {
                    row.Add(new RichTableCell());
                }

                table.Rows.Add(row);
            }

            InsertBlock(table);

            // Into the first cell rather than the caption above it: a table is written from there.
            if (rows > 0 && columns > 0)
            {
                SetCaret(new RichPosition(table.Rows[0][0].Text, 0));
            }
        }

        /// <summary>Moves the selected block, or the block the caret is in, one place up or down among its siblings.</summary>
        public void MoveBlock(int direction)
        {
            if (!CanMoveBlock(direction))
            {
                return;
            }

            var block = _node ?? TopOf(_focus.Leaf.Owner);
            var container = _document.GetContainer(block, out int index);
            var target = index + direction;

            BeginChange();

            container.RemoveAt(index);
            container.Insert(target, block);

            EndChange();
        }

        /// <summary>
        /// Whether the selected block, or the one the caret is in, has somewhere to go that way
        /// among its siblings. The empty line kept after a block at the end is not somewhere:
        /// moving past it puts another one after the block, and nothing has moved.
        /// </summary>
        public bool CanMoveBlock(int direction)
        {
            var block = _node ?? TopOf(_focus.Leaf.Owner);
            var container = _document.GetContainer(block, out int index);

            var target = index + direction;
            if (container == null || target < 0 || target >= container.Count)
            {
                return false;
            }

            return !(direction > 0
                && block.IsAtomic
                && target == container.Count - 1
                && container[target] is RichParagraphBlock { Kind: RichBlockKind.Paragraph } line
                && line.Text.IsEmpty);
        }

        /// <summary>Deletes the selected block, or the one given.</summary>
        public void Delete(RichBlock block)
        {
            BeginChange();
            DeleteNode(block);
            EndChange();
        }
    }
}
