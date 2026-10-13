//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

namespace Telegram.Controls.RichEditor
{
    /// <summary>
    /// What a toolbar shows: the formatting at the selection and the block it is in. Computed
    /// on demand from the engine, once per change, rather than kept in step with it.
    /// </summary>
    public sealed class RichEditorStatus
    {
        public RichMarks Marks { get; init; }

        public bool HasLink { get; init; }

        public bool HasDate { get; init; }

        /// <summary>
        /// The block the toolbar speaks of, by precedence: a selected block, then the list the
        /// caret is in, then the quote, then the block of the line itself.
        /// </summary>
        public RichBlockKind BlockKind { get; init; }

        /// <summary>The kind of the line itself, under any list or quote around it.</summary>
        public RichBlockKind TextKind { get; init; }

        public int HeadingSize { get; init; }

        /// <summary>Null outside a list.</summary>
        public RichListStyle? ListStyle { get; init; }

        public bool InBlockQuote { get; init; }

        /// <summary>The caret is in a collapsible block, its title or anything in it.</summary>
        public bool InDetails { get; init; }

        public RichTableStatus Table { get; init; }

        public RichParagraphBlock CodeBlock { get; init; }

        public bool CanUndo { get; init; }

        public bool CanRedo { get; init; }

        public bool IsSelectionEmpty { get; init; }

        /// <summary>A text range is selected, not a caret and not a block: what the formatting bar is for.</summary>
        public bool HasTextSelection { get; init; }

        public bool IsDocumentEmpty { get; init; }

        /// <summary>Whether marks apply at the caret: not in a code block, not on a selected block.</summary>
        public bool CanFormat { get; init; }
    }

    public sealed partial class RichEditorEngine
    {
        public RichEditorStatus GetStatus()
        {
            var leaf = _focus.Leaf;
            var owner = _node ?? leaf.Owner;

            var list = _node == null ? FindAncestor<RichListBlock>(owner) : null;
            var quote = _node == null ? FindAncestor<RichBlockQuote>(owner) : null;

            var text = owner as RichParagraphBlock;
            var textKind = text?.Kind ?? owner?.Kind ?? RichBlockKind.Paragraph;

            RichBlockKind kind;
            if (_node != null)
            {
                kind = _node.Kind;
            }
            else if (list != null)
            {
                kind = RichBlockKind.List;
            }
            else if (quote != null)
            {
                kind = RichBlockKind.BlockQuote;
            }
            else
            {
                kind = textKind;
            }

            var link = GetSelectionLink();
            var hasText = _node == null && !IsSelectionEmpty;

            return new RichEditorStatus
            {
                Marks = GetSelectionMarks(),
                HasLink = link != null && link.Kind != RichLinkKind.DateTime,
                HasDate = link?.Kind == RichLinkKind.DateTime,
                BlockKind = kind,
                TextKind = textKind,
                HeadingSize = text?.HeadingSize ?? 0,
                ListStyle = list?.Style,
                InBlockQuote = quote != null,
                InDetails = GetFocusedDetails() != null,
                Table = _node == null ? GetTableStatus() : null,
                CodeBlock = text?.Kind == RichBlockKind.Preformatted ? text : null,
                CanUndo = CanUndo,
                CanRedo = CanRedo,
                IsSelectionEmpty = IsSelectionEmpty,
                HasTextSelection = hasText,
                IsDocumentEmpty = _document.IsEmpty,
                CanFormat = _node == null && !leaf.IsPlain
            };
        }
    }
}
