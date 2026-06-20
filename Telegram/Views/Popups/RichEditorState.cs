//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Windows.Data.Json;

namespace Telegram.Views.Popups
{
    /// <summary>
    /// The block type reported by the editor (<c>block.type</c>). Headline category
    /// at the selection, by precedence: selected media node &gt; list &gt; blockquote
    /// &gt; innermost text block.
    /// </summary>
    public enum RichEditorBlockType
    {
        Paragraph,
        Heading,
        Preformatted,
        Blockquote,
        Pullquote,
        List,
        Table,
        Photo,
        Video,
        Audio,
        Animation,
        Voice,
        Map,
        Math,
        Anchor,
    }

    /// <summary>List flavour (<c>block.listType</c>). <see cref="None"/> when not in a list.</summary>
    public enum RichEditorListType
    {
        None,
        Bullet,
        Ordered,
        Checkbox,
    }

    /// <summary>Horizontal alignment of the selected table cell(s) (<c>table.align</c>).</summary>
    public enum RichEditorCellAlignment
    {
        Left,
        Center,
        Right,
    }

    /// <summary>Vertical alignment of the selected table cell(s) (<c>table.valign</c>).</summary>
    public enum RichEditorCellVerticalAlignment
    {
        Top,
        Middle,
        Bottom,
    }

    /// <summary>
    /// Mirrors the <c>{ type:"state", ... }</c> message the Instant View editor pushes
    /// to the host on every selection/document change. Construct once and feed each
    /// incoming state object to <see cref="Update"/>; all properties are read-only to
    /// consumers and reflect the latest push (sensible defaults until the first one).
    /// </summary>
    public class RichEditorState
    {
        // --- marks (active formatting at the selection) -------------------------
        public bool Bold { get; private set; }
        public bool Italic { get; private set; }
        public bool Underline { get; private set; }
        public bool Strikethrough { get; private set; }
        public bool Code { get; private set; }
        public bool Spoiler { get; private set; }
        public bool Marked { get; private set; }
        public bool Subscript { get; private set; }
        public bool Superscript { get; private set; }
        public bool Link { get; private set; }
        public bool DateTime { get; private set; }

        // --- block --------------------------------------------------------------
        public RichEditorBlockType BlockType { get; private set; } = RichEditorBlockType.Paragraph;

        /// <summary>Heading size 1..6; only meaningful when <see cref="BlockType"/> is <see cref="RichEditorBlockType.Heading"/> (0 otherwise).</summary>
        public int HeadingSize { get; private set; }

        /// <summary>List flavour; only meaningful when <see cref="BlockType"/> is <see cref="RichEditorBlockType.List"/>.</summary>
        public RichEditorListType ListType { get; private set; } = RichEditorListType.None;

        /// <summary>Code language; only meaningful when <see cref="BlockType"/> is <see cref="RichEditorBlockType.Preformatted"/> (empty = none).</summary>
        public string Language { get; private set; } = string.Empty;

        /// <summary>Anchor name; only meaningful when <see cref="BlockType"/> is <see cref="RichEditorBlockType.Anchor"/>.</summary>
        public string AnchorName { get; private set; } = string.Empty;

        // --- history ------------------------------------------------------------
        public bool CanUndo { get; private set; }
        public bool CanRedo { get; private set; }

        // --- table (only meaningful while InTable is true) ----------------------
        /// <summary>True when the caret is inside a table; the remaining table properties are only meaningful then.</summary>
        public bool InTable { get; private set; }

        /// <summary>Number of cells covered by the selection.</summary>
        public int CellCount { get; private set; }

        /// <summary>Shared horizontal alignment of the selected cells, or <c>null</c> when they disagree (mixed).</summary>
        public RichEditorCellAlignment? CellAlignment { get; private set; }

        /// <summary>Shared vertical alignment of the selected cells, or <c>null</c> when they disagree (mixed).</summary>
        public RichEditorCellVerticalAlignment? CellVerticalAlignment { get; private set; }

        /// <summary>Shared header state of the selected cells, or <c>null</c> when they disagree (mixed).</summary>
        public bool? CellIsHeader { get; private set; }

        public bool CanMergeCells { get; private set; }
        public bool CanUnmergeCells { get; private set; }
        public bool CanAddRow { get; private set; }
        public bool CanAddColumn { get; private set; }
        public bool CanDeleteRow { get; private set; }
        public bool CanDeleteColumn { get; private set; }

        // --- selection ----------------------------------------------------------
        /// <summary>True when the selection is a caret with nothing selected.</summary>
        public bool SelectionIsEmpty { get; private set; } = true;

        /// <summary>True when a non-empty text range is selected.</summary>
        public bool SelectionHasText { get; private set; }

        /// <summary>True when an atom/figure node is selected (NodeSelection).</summary>
        public bool SelectionIsNode { get; private set; }

        public int SelectionFrom { get; private set; }
        public int SelectionTo { get; private set; }

        /// <summary>
        /// Applies an editor <c>state</c> message. Missing sections fall back to defaults,
        /// so a partial object still leaves the instance in a consistent state.
        /// </summary>
        public void Update(JsonObject state)
        {
            if (state == null)
            {
                return;
            }

            var marks = state.GetNamedObject("marks", null) ?? new JsonObject();
            Bold = marks.GetNamedBoolean("bold", false);
            Italic = marks.GetNamedBoolean("italic", false);
            Underline = marks.GetNamedBoolean("underline", false);
            Strikethrough = marks.GetNamedBoolean("strike", false);
            Code = marks.GetNamedBoolean("code", false);
            Spoiler = marks.GetNamedBoolean("spoiler", false);
            Marked = marks.GetNamedBoolean("marked", false);
            Subscript = marks.GetNamedBoolean("subscript", false);
            Superscript = marks.GetNamedBoolean("superscript", false);
            Link = marks.GetNamedBoolean("link", false);
            DateTime = marks.GetNamedBoolean("dateTime", false);

            var block = state.GetNamedObject("block", null) ?? new JsonObject();
            BlockType = ParseBlockType(block.GetNamedString("type", "paragraph"));
            HeadingSize = (int)GetNullableNumber(block, "size");
            ListType = ParseListType(GetNullableString(block, "listType"));
            Language = GetNullableString(block, "language");
            AnchorName = GetNullableString(block, "name");

            var table = GetNullableObject(state, "table");
            if (table != null)
            {
                InTable = true;
                CellCount = (int)table.GetNamedNumber("cellCount", 0);
                CellAlignment = ParseCellAlignment(GetNullableString(table, "align"));
                CellVerticalAlignment = ParseCellVerticalAlignment(GetNullableString(table, "valign"));
                CellIsHeader = GetNullableBoolean(table, "isHeader");
                CanMergeCells = table.GetNamedBoolean("canMerge", false);
                CanUnmergeCells = table.GetNamedBoolean("canUnmerge", false);
                CanAddRow = table.GetNamedBoolean("canAddRow", false);
                CanAddColumn = table.GetNamedBoolean("canAddColumn", false);
                CanDeleteRow = table.GetNamedBoolean("canDeleteRow", false);
                CanDeleteColumn = table.GetNamedBoolean("canDeleteColumn", false);
            }
            else
            {
                InTable = false;
                CellCount = 0;
                CellAlignment = null;
                CellVerticalAlignment = null;
                CellIsHeader = null;
                CanMergeCells = false;
                CanUnmergeCells = false;
                CanAddRow = false;
                CanAddColumn = false;
                CanDeleteRow = false;
                CanDeleteColumn = false;
            }

            var can = state.GetNamedObject("can", null) ?? new JsonObject();
            CanUndo = can.GetNamedBoolean("undo", false);
            CanRedo = can.GetNamedBoolean("redo", false);

            var selection = state.GetNamedObject("selection", null) ?? new JsonObject();
            SelectionIsEmpty = selection.GetNamedBoolean("empty", true);
            SelectionHasText = selection.GetNamedBoolean("hasText", false);
            SelectionIsNode = selection.GetNamedBoolean("isNode", false);
            SelectionFrom = (int)selection.GetNamedNumber("from", 0);
            SelectionTo = (int)selection.GetNamedNumber("to", 0);
        }

        private static bool? GetNullableBoolean(JsonObject obj, string key)
        {
            if (obj.TryGetValue(key, out var value) && value.ValueType == JsonValueType.Boolean)
            {
                return value.GetBoolean();
            }

            return null;
        }

        private static string GetNullableString(JsonObject obj, string key)
        {
            if (obj.TryGetValue(key, out var value) && value.ValueType == JsonValueType.String)
            {
                return value.GetString();
            }

            return null;
        }

        private static double GetNullableNumber(JsonObject obj, string key)
        {
            if (obj.TryGetValue(key, out var value) && value.ValueType == JsonValueType.Number)
            {
                return value.GetNumber();
            }

            return 0;
        }

        private static JsonObject GetNullableObject(JsonObject obj, string key)
        {
            if (obj.TryGetValue(key, out var value) && value.ValueType == JsonValueType.Object)
            {
                return value.GetObject();
            }

            return null;
        }

        private static RichEditorBlockType ParseBlockType(string value)
        {
            return value switch
            {
                "heading" => RichEditorBlockType.Heading,
                "preformatted" => RichEditorBlockType.Preformatted,
                "blockquote" => RichEditorBlockType.Blockquote,
                "pullquote" => RichEditorBlockType.Pullquote,
                "list" => RichEditorBlockType.List,
                "table" => RichEditorBlockType.Table,
                "photo" => RichEditorBlockType.Photo,
                "video" => RichEditorBlockType.Video,
                "audio" => RichEditorBlockType.Audio,
                "animation" => RichEditorBlockType.Animation,
                "voice" => RichEditorBlockType.Voice,
                "map" => RichEditorBlockType.Map,
                "math" => RichEditorBlockType.Math,
                "anchor" => RichEditorBlockType.Anchor,
                _ => RichEditorBlockType.Paragraph,
            };
        }

        private static RichEditorListType ParseListType(string value)
        {
            return value switch
            {
                "bullet" => RichEditorListType.Bullet,
                "ordered" => RichEditorListType.Ordered,
                "checkbox" => RichEditorListType.Checkbox,
                _ => RichEditorListType.None,
            };
        }

        private static RichEditorCellAlignment? ParseCellAlignment(string value)
        {
            return value switch
            {
                "left" => RichEditorCellAlignment.Left,
                "center" => RichEditorCellAlignment.Center,
                "right" => RichEditorCellAlignment.Right,
                _ => null,
            };
        }

        private static RichEditorCellVerticalAlignment? ParseCellVerticalAlignment(string value)
        {
            return value switch
            {
                "top" => RichEditorCellVerticalAlignment.Top,
                "middle" => RichEditorCellVerticalAlignment.Middle,
                "bottom" => RichEditorCellVerticalAlignment.Bottom,
                _ => null,
            };
        }
    }
}
