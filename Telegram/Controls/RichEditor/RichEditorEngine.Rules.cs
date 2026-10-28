//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Globalization;

namespace Telegram.Controls.RichEditor
{
    /// <summary>
    /// Markdown typed at the start of a line, turned into the block it stands for as the space
    /// after it is typed. A rule is one undo step of its
    /// own, after the typing, so Undo - or Backspace straight away - gives the characters back.
    /// </summary>
    public sealed partial class RichEditorEngine
    {
        // Set by a rule, cleared by anything else: whether Backspace right now means "take it back".
        private bool _ruleApplied;

        // While positive, edits join the step already open instead of each taking a snapshot:
        // a rule is several operations that undo as one.
        private int _suppressUndo;

        private const int MaxRulePrefix = 64;

        /// <summary>Called after a space is typed; true when the line became something else.</summary>
        private bool TryApplySpaceRule(RichTextLeaf leaf, int caret)
        {
            // A message's text keeps what was typed: "> " and "- " are how people write in
            // chats, and markdown in a message is parsed when it is sent, as it always was.
            if (Schema != null)
            {
                return false;
            }

            if (leaf.Owner is not RichParagraphBlock { Kind: RichBlockKind.Paragraph } paragraph || leaf.Role != RichLeafRole.Text || leaf.IsPlain)
            {
                return false;
            }

            // Only in a line of its own: inside a list the same characters are content.
            if (paragraph.Parent is RichListBlock)
            {
                return false;
            }

            // Every rule is a few characters, a code fence's language the longest of them: a
            // longer line cannot be one, and copying it to find out would copy a whole paragraph
            // on every space typed in it.
            if (caret < 2 || caret - 1 > MaxRulePrefix || leaf[caret - 1] != ' ')
            {
                return false;
            }

            var prefix = leaf.Substring(0, caret - 1);
            if (prefix.IndexOf(RichTextLeaf.ObjectChar) >= 0)
            {
                return false;
            }

            if (prefix.Length <= 6 && prefix.Trim('#').Length == 0)
            {
                ApplyRule(leaf, caret, () => SetTextKind(RichBlockKind.Heading, prefix.Length));
                return true;
            }

            switch (prefix)
            {
                case "-":
                case "*":
                case "+":
                case "•":
                    ApplyRule(leaf, caret, () => ToggleList(RichListStyle.Bullet));
                    return true;
                case "[]":
                case "[ ]":
                case "[x]":
                case "[X]":
                    {
                        var check = prefix.Length == 3 && prefix[1] != ' ';
                        ApplyRule(leaf, caret, () =>
                        {
                            ToggleList(RichListStyle.Checklist);

                            if (check && GetItemOf(_focus.Leaf.Owner, out _) is RichListItem item)
                            {
                                item.IsChecked = true;
                            }
                        });
                        return true;
                    }
                case ">" when FindAncestor<RichBlockQuote>(paragraph) == null:
                    ApplyRule(leaf, caret, ToggleBlockQuote);
                    return true;
            }

            // "1." or "1)": a numbered list starting there.
            if (prefix.Length >= 2 && (prefix[prefix.Length - 1] == '.' || prefix[prefix.Length - 1] == ')')
                && int.TryParse(prefix.Substring(0, prefix.Length - 1), NumberStyles.None, CultureInfo.InvariantCulture, out int start)
                && start > 0 && start < 100000)
            {
                ApplyRule(leaf, caret, () =>
                {
                    ToggleList(RichListStyle.Ordered);

                    if (FindAncestor<RichListBlock>(_focus.Leaf.Owner) is RichListBlock list)
                    {
                        list.Start = start;
                    }
                });
                return true;
            }

            // "```js " - the rest of the line becomes the code.
            if (prefix.StartsWith("```", StringComparison.Ordinal) && prefix.IndexOf(' ') < 0)
            {
                var language = prefix.Substring(3);
                ApplyRule(leaf, caret, () =>
                {
                    SetTextKind(RichBlockKind.Preformatted);
                    paragraph.Language = language;
                });
                return true;
            }

            return false;
        }

        /// <summary>
        /// On Enter, the two rules that are a whole line: "---" is a divider, and "```lang" a code
        /// block waiting for its first line.
        /// </summary>
        private bool TryApplyEnterRule()
        {
            if (!IsSelectionEmpty || Schema != null)
            {
                return false;
            }

            var leaf = _focus.Leaf;
            if (leaf.Owner is not RichParagraphBlock { Kind: RichBlockKind.Paragraph } paragraph || leaf.Role != RichLeafRole.Text || _focus.Offset != leaf.Length || paragraph.Parent is RichListBlock)
            {
                return false;
            }

            // A rule's line is short, as for the space rules: a longer one is not made a string
            // to find out.
            if (leaf.Length > MaxRulePrefix + 1)
            {
                return false;
            }

            var text = leaf.Text;

            if (text == "---" || text == "***" || text == "___")
            {
                ApplyRule(leaf, leaf.Length, () =>
                {
                    var divider = new RichDividerBlock();
                    Replace(paragraph, divider);
                    InsertAfter(divider, new RichParagraphBlock(RichBlockKind.Paragraph));
                    _document.Reindex();

                    var next = PositionAfter(divider);
                    if (next is RichPosition position)
                    {
                        SetCaretInternal(position);
                    }
                });

                return true;
            }

            if (text.StartsWith("```", StringComparison.Ordinal) && text.IndexOf(' ') < 0)
            {
                var language = text.Substring(3);
                ApplyRule(leaf, leaf.Length, () =>
                {
                    SetTextKind(RichBlockKind.Preformatted);
                    paragraph.Language = language;
                });

                return true;
            }

            return false;
        }

        private void ApplyRule(RichTextLeaf leaf, int length, Action apply)
        {
            BeginEdit(RichEditKind.Other);
            BeginBatch();
            _suppressUndo++;

            try
            {
                leaf.Delete(0, length);
                SetCaretInternal(new RichPosition(leaf, 0));
                Raise(RichEditorChanges.Text | RichEditorChanges.Selection, leaf);

                apply();
                _document.Reindex();
                Raise(RichEditorChanges.Structure | RichEditorChanges.Selection, null);
            }
            finally
            {
                _suppressUndo--;
                EndBatch();
            }

            EndEdit(RichEditKind.Other);
            _ruleApplied = true;
        }
    }
}
