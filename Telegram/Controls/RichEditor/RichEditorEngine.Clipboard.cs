//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using System.Text;

namespace Telegram.Controls.RichEditor
{
    public sealed partial class RichEditorEngine
    {
        /// <summary>
        /// The selection as a document of its own: what copy puts on the clipboard. A block
        /// only partly selected is kept with the rest of it emptied - a quote stays a quote
        /// around the lines taken from it, a table keeps its shape - and a selected block is
        /// copied whole.
        /// </summary>
        public RichDocument GetSelectionFragment()
        {
            if (_node != null)
            {
                return new RichDocument(new List<RichBlock> { _node.Clone() });
            }

            if (IsSelectionEmpty)
            {
                return null;
            }

            var start = Start;
            var end = End;

            // The clone is indexed in the same order, so the selection's stops carry over.
            return GetFragment(_document.Clone(), start.Leaf.StopIndex, start.Offset, end.Leaf.StopIndex, end.Offset);
        }

        /// <summary>
        /// <paramref name="document"/> cut down in place to what lies between two stops, as
        /// <see cref="GetSelectionFragment"/> cuts a selection. The offsets are into the stops'
        /// leaves; an atomic block at either end is kept whole.
        /// </summary>
        public static RichDocument GetFragment(RichDocument document, int startStop, int startOffset, int endStop, int endOffset)
        {
            foreach (var leaf in document.Leaves)
            {
                if (leaf.StopIndex < startStop || leaf.StopIndex > endStop)
                {
                    leaf.Delete(0, leaf.Length);
                }
            }

            // The end first: in the same leaf, cutting the start would move it.
            if (document.Stops[endStop].Leaf is RichTextLeaf endLeaf)
            {
                endLeaf.Delete(endOffset, endLeaf.Length);
            }

            if (document.Stops[startStop].Leaf is RichTextLeaf startLeaf)
            {
                startLeaf.Delete(0, startOffset);
            }

            Prune(document.Blocks, startStop, endStop);
            document.Reindex();

            return document;
        }

        /// <summary>Blocks wholly outside [first, last] go; containers that reach into it stay, pruned in turn.</summary>
        private static void Prune(List<RichBlock> blocks, int first, int last)
        {
            for (int i = blocks.Count - 1; i >= 0; i--)
            {
                var block = blocks[i];

                if (block.LastStop < first || block.FirstStop > last)
                {
                    blocks.RemoveAt(i);
                    continue;
                }

                if (block is RichListBlock list)
                {
                    for (int j = list.Items.Count - 1; j >= 0; j--)
                    {
                        var children = list.Items[j].Children;
                        Prune(children, first, last);

                        if (children.Count == 0)
                        {
                            list.Items.RemoveAt(j);
                        }
                    }
                }
                else if (block is not RichMediaGroupBlock and not RichTableBlock)
                {
                    var lists = new List<List<RichBlock>>();
                    block.CollectChildLists(lists);

                    foreach (var children in lists)
                    {
                        Prune(children, first, last);
                    }
                }
            }
        }

        /// <summary>
        /// Paste of a document. One plain paragraph goes into the line at the caret, styles and
        /// objects included; more than that splits the line, and the first and last pasted
        /// paragraphs join its two halves, so pasting into the middle of a sentence reads on.
        /// Where only text can go - a cell, a caption, a code block - the text goes.
        /// </summary>
        public void InsertFragment(RichDocument fragment)
        {
            if (fragment == null)
            {
                return;
            }

            // Copied, so pasting the same fragment twice does not put the same blocks in twice.
            var blocks = fragment.Clone().Blocks;
            Schema?.Conform(blocks);

            if (blocks.Count == 0)
            {
                return;
            }

            BeginEdit(RichEditKind.Other);
            BeginBatch();

            var position = PrepareInsertion();
            var leaf = position.Leaf;
            var offset = position.Offset;

            if (blocks.Count == 1 && blocks[0] is RichParagraphBlock { Kind: RichBlockKind.Paragraph } single)
            {
                leaf.Insert(offset, single.Text);
                SetCaretInternal(new RichPosition(leaf, offset + (leaf.IsPlain ? single.Text.ToPlainText().Length : single.Text.Length)));
                Raise(RichEditorChanges.Text | RichEditorChanges.Selection, leaf);
            }
            else if (leaf.Owner is not RichParagraphBlock text || leaf.Role != RichLeafRole.Text || text.Kind == RichBlockKind.Preformatted)
            {
                var plain = GetPlainText(blocks);
                leaf.Insert(offset, plain, RichMarks.None, null);
                SetCaretInternal(new RichPosition(leaf, offset + plain.Length));
                Raise(RichEditorChanges.Text | RichEditorChanges.Selection, leaf);
            }
            else
            {
                InsertBlocks(text, offset, blocks);
            }

            EndBatch();
            EndEdit(RichEditKind.Other);
        }

        private void InsertBlocks(RichParagraphBlock text, int offset, List<RichBlock> blocks)
        {
            var leaf = text.Text;
            var container = _document.GetContainer(text, out int index);

            var tail = leaf.Split(offset, RichLeafRole.Text);

            if (blocks[0] is RichParagraphBlock { Kind: RichBlockKind.Paragraph } head)
            {
                leaf.Append(head.Text);
                blocks.RemoveAt(0);
            }

            RichTextLeaf caretLeaf = leaf;
            var caretOffset = leaf.Length;

            if (blocks.Count > 0 && blocks[blocks.Count - 1] is RichParagraphBlock { Kind: RichBlockKind.Paragraph } last)
            {
                caretLeaf = last.Text;
                caretOffset = last.Text.Length;
                last.Text.Append(tail);
            }
            else if (blocks.Count > 0 || !tail.IsEmpty)
            {
                var after = new RichParagraphBlock(text.Kind == RichBlockKind.Footer ? RichBlockKind.Footer : RichBlockKind.Paragraph, tail);
                blocks.Add(after);

                if (blocks.Count == 1)
                {
                    caretLeaf = leaf;
                    caretOffset = leaf.Length;
                }
                else
                {
                    caretLeaf = tail;
                    caretOffset = 0;
                }
            }

            container.InsertRange(index + 1, blocks);

            // The line the paste started on, left empty by it, goes rather than leaving a gap.
            if (leaf.IsEmpty && text.Kind == RichBlockKind.Paragraph && blocks.Count > 0 && !ReferenceEquals(caretLeaf, leaf))
            {
                container.Remove(text);
            }

            _document.Reindex();
            Raise(RichEditorChanges.Structure, null);

            SetCaretInternal(Clamp(new RichPosition(caretLeaf, caretOffset)));
            Raise(RichEditorChanges.Selection, null);
        }

        private static string GetPlainText(List<RichBlock> blocks)
        {
            var stops = new List<RichStop>();
            foreach (var block in blocks)
            {
                block.CollectStops(stops);
            }

            var builder = new StringBuilder();
            foreach (var stop in stops)
            {
                if (stop.Leaf == null || stop.Leaf.IsEmpty)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(stop.Leaf.ToPlainText());
            }

            return builder.ToString();
        }
    }
}
