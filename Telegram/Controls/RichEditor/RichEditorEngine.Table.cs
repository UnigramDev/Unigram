//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using Telegram.Td.Api;

namespace Telegram.Controls.RichEditor
{
    /// <summary>
    /// A table as a grid of slots, each pointing at the cell that covers it: a cell spanning two
    /// columns is in two slots. Every table edit is made on the slots, where inserting a row or
    /// merging a range is plain array work, and the rows TDLib wants - each cell once, in the row
    /// it starts in - are rebuilt from them afterwards.
    /// </summary>
    internal sealed class RichTableGrid
    {
        private readonly List<List<RichTableCell>> _slots;

        private RichTableGrid(List<List<RichTableCell>> slots)
        {
            _slots = slots;
        }

        public int Rows => _slots.Count;

        public int Columns => _slots.Count > 0 ? _slots[0].Count : 0;

        public RichTableCell this[int row, int column] => _slots[row][column];

        public static RichTableGrid From(RichTableBlock table)
        {
            var columns = 0;
            foreach (var row in table.Rows)
            {
                var width = 0;
                foreach (var cell in row)
                {
                    width += Math.Max(1, cell.Colspan);
                }

                columns = Math.Max(columns, width);
            }

            var slots = new List<List<RichTableCell>>(table.Rows.Count);
            for (int r = 0; r < table.Rows.Count; r++)
            {
                slots.Add(new List<RichTableCell>(new RichTableCell[columns]));
            }

            for (int r = 0; r < table.Rows.Count; r++)
            {
                var column = 0;

                foreach (var cell in table.Rows[r])
                {
                    while (column < columns && slots[r][column] != null)
                    {
                        column++;
                    }

                    var rowspan = Math.Max(1, cell.Rowspan);
                    var colspan = Math.Max(1, cell.Colspan);

                    for (int y = r; y < Math.Min(r + rowspan, slots.Count); y++)
                    {
                        for (int x = column; x < Math.Min(column + colspan, columns); x++)
                        {
                            slots[y][x] = cell;
                        }
                    }

                    column += colspan;
                }
            }

            // A row short of the width - TDLib allows ragged tables - is filled out, so every
            // operation can count on a full rectangle.
            foreach (var row in slots)
            {
                for (int x = 0; x < row.Count; x++)
                {
                    row[x] ??= new RichTableCell();
                }
            }

            return new RichTableGrid(slots);
        }

        /// <summary>Writes the slots back as rows, with every span measured from them.</summary>
        public void ApplyTo(RichTableBlock table)
        {
            table.Rows.Clear();

            var seen = new HashSet<RichTableCell>();

            for (int r = 0; r < _slots.Count; r++)
            {
                var row = new List<RichTableCell>();

                for (int x = 0; x < _slots[r].Count; x++)
                {
                    var cell = _slots[r][x];
                    if (!seen.Add(cell))
                    {
                        continue;
                    }

                    GetBounds(cell, out int top, out int left, out int bottom, out int right);
                    cell.Rowspan = bottom - top + 1;
                    cell.Colspan = right - left + 1;

                    row.Add(cell);
                }

                table.Rows.Add(row);
            }
        }

        public bool Find(RichTableCell cell, out int row, out int column)
        {
            for (int r = 0; r < _slots.Count; r++)
            {
                for (int x = 0; x < _slots[r].Count; x++)
                {
                    if (ReferenceEquals(_slots[r][x], cell))
                    {
                        row = r;
                        column = x;
                        return true;
                    }
                }
            }

            row = column = -1;
            return false;
        }

        public void GetBounds(RichTableCell cell, out int top, out int left, out int bottom, out int right)
        {
            top = left = int.MaxValue;
            bottom = right = -1;

            for (int r = 0; r < _slots.Count; r++)
            {
                for (int x = 0; x < _slots[r].Count; x++)
                {
                    if (ReferenceEquals(_slots[r][x], cell))
                    {
                        top = Math.Min(top, r);
                        left = Math.Min(left, x);
                        bottom = Math.Max(bottom, r);
                        right = Math.Max(right, x);
                    }
                }
            }
        }

        /// <summary>
        /// Grows a range until no cell sticks out of it: selecting across a merged cell takes the
        /// whole of it, as a merge or an alignment can only apply to cells whole.
        /// </summary>
        public void Expand(ref int top, ref int left, ref int bottom, ref int right)
        {
            bool grown;

            do
            {
                grown = false;

                for (int r = top; r <= bottom; r++)
                {
                    for (int x = left; x <= right; x++)
                    {
                        GetBounds(_slots[r][x], out int t, out int l, out int b, out int ri);

                        if (t < top || l < left || b > bottom || ri > right)
                        {
                            top = Math.Min(top, t);
                            left = Math.Min(left, l);
                            bottom = Math.Max(bottom, b);
                            right = Math.Max(right, ri);
                            grown = true;
                        }
                    }
                }
            }
            while (grown);
        }

        /// <summary>A new row at <paramref name="index"/>; a cell spanning across that line grows instead.</summary>
        public void InsertRow(int index)
        {
            var columns = Columns;
            var row = new List<RichTableCell>(columns);

            for (int x = 0; x < columns; x++)
            {
                var above = index > 0 ? _slots[index - 1][x] : null;
                var below = index < _slots.Count ? _slots[index][x] : null;

                row.Add(above != null && ReferenceEquals(above, below) ? above : new RichTableCell());
            }

            _slots.Insert(index, row);
        }

        public void RemoveRow(int index)
        {
            _slots.RemoveAt(index);
        }

        public void InsertColumn(int index)
        {
            foreach (var row in _slots)
            {
                var left = index > 0 ? row[index - 1] : null;
                var right = index < row.Count ? row[index] : null;

                row.Insert(index, left != null && ReferenceEquals(left, right) ? left : new RichTableCell());
            }
        }

        public void RemoveColumn(int index)
        {
            foreach (var row in _slots)
            {
                row.RemoveAt(index);
            }
        }

        /// <summary>One cell over the range: the first one, holding the others' text a line each.</summary>
        public RichTableCell Merge(int top, int left, int bottom, int right)
        {
            var target = _slots[top][left];
            var merged = new HashSet<RichTableCell> { target };

            for (int r = top; r <= bottom; r++)
            {
                for (int x = left; x <= right; x++)
                {
                    var cell = _slots[r][x];

                    if (merged.Add(cell) && !cell.Text.IsEmpty)
                    {
                        if (!target.Text.IsEmpty)
                        {
                            target.Text.Insert(target.Text.Length, "\n", RichMarks.None, null);
                        }

                        target.Text.Append(cell.Text);
                    }

                    _slots[r][x] = target;
                }
            }

            return target;
        }

        /// <summary>A merged cell back into one per slot; the text stays in the first.</summary>
        public void Split(RichTableCell cell)
        {
            GetBounds(cell, out int top, out int left, out int bottom, out int right);

            for (int r = top; r <= bottom; r++)
            {
                for (int x = left; x <= right; x++)
                {
                    if (r != top || x != left)
                    {
                        _slots[r][x] = new RichTableCell
                        {
                            IsHeader = cell.IsHeader,
                            Align = cell.Align,
                            Valign = cell.Valign
                        };
                    }
                }
            }
        }

        public IEnumerable<RichTableCell> CellsIn(int top, int left, int bottom, int right)
        {
            var seen = new HashSet<RichTableCell>();

            for (int r = top; r <= bottom; r++)
            {
                for (int x = left; x <= right; x++)
                {
                    if (seen.Add(_slots[r][x]))
                    {
                        yield return _slots[r][x];
                    }
                }
            }
        }
    }

    public sealed partial class RichEditorEngine
    {
        /// <summary>The table the selection is in, with the range of slots it covers.</summary>
        private bool GetTableSelection(out RichTableBlock table, out RichTableGrid grid, out int top, out int left, out int bottom, out int right)
        {
            table = null;
            grid = null;
            top = left = bottom = right = -1;

            var start = Start.Leaf;
            var end = End.Leaf;

            if (_node != null || start.Owner is not RichTableBlock owner || start.Role != RichLeafRole.Cell)
            {
                return false;
            }

            table = owner;
            grid = RichTableGrid.From(table);

            var first = FindCell(table, start);
            if (first == null || !grid.Find(first, out _, out _))
            {
                return false;
            }

            grid.GetBounds(first, out top, out left, out bottom, out right);

            if (ReferenceEquals(end.Owner, table) && end.Role == RichLeafRole.Cell && FindCell(table, end) is RichTableCell last)
            {
                grid.GetBounds(last, out int t, out int l, out int b, out int r);

                top = Math.Min(top, t);
                left = Math.Min(left, l);
                bottom = Math.Max(bottom, b);
                right = Math.Max(right, r);
            }

            grid.Expand(ref top, ref left, ref bottom, ref right);
            return true;
        }

        private static RichTableCell FindCell(RichTableBlock table, RichTextLeaf leaf)
        {
            foreach (var row in table.Rows)
            {
                foreach (var cell in row)
                {
                    if (ReferenceEquals(cell.Text, leaf))
                    {
                        return cell;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// The cells a selection covers when it runs from one cell into another of the same
        /// table: every cell in the rectangle between them, merged cells whole. Empty for a
        /// selection inside one cell, which is text.
        /// </summary>
        public void GetSelectedCells(List<RichTableCell> cells)
        {
            cells.Clear();

            if (_node != null || ReferenceEquals(Start.Leaf, End.Leaf) || !ReferenceEquals(Start.Leaf.Owner, End.Leaf.Owner))
            {
                return;
            }

            if (Start.Leaf.Role != RichLeafRole.Cell || End.Leaf.Role != RichLeafRole.Cell)
            {
                return;
            }

            if (GetTableSelection(out _, out RichTableGrid grid, out int top, out int left, out int bottom, out int right))
            {
                cells.AddRange(grid.CellsIn(top, left, bottom, right));
            }
        }

        /// <summary>
        /// The tables a selection running past them takes whole: drawn as selected cells, not as
        /// text. A selection inside one table is that table's own, unless it is all of it.
        /// </summary>
        public void GetCoveredTables(List<RichTableBlock> tables)
        {
            tables.Clear();

            if (_node != null || IsSelectionEmpty)
            {
                return;
            }

            var start = Start;
            var end = End;

            foreach (var block in _document.AllBlocks)
            {
                if (block is RichTableBlock table && table.FirstStop <= table.LastStop && Covers(table, start, end))
                {
                    tables.Add(table);
                }
            }
        }

        // From its first character to its last: the caption's start, the last cell's end.
        private static bool Covers(RichTableBlock table, RichPosition start, RichPosition end)
        {
            var startStop = start.Leaf.StopIndex;
            var endStop = end.Leaf.StopIndex;

            var fromStart = table.FirstStop > startStop || (table.FirstStop == startStop && start.Offset == 0);
            var toEnd = table.LastStop < endStop || (table.LastStop == endStop && end.Offset == end.Leaf.Length);

            return fromStart && toEnd;
        }

        private static RichTableBlock TableOf(RichPosition position) => position.Leaf?.Owner as RichTableBlock;

        private RichPosition TableStart(RichTableBlock table) => new(_document.Stops[table.FirstStop].Leaf, 0);

        private RichPosition TableEnd(RichTableBlock table)
        {
            var leaf = _document.Stops[table.LastStop].Leaf;
            return new RichPosition(leaf, leaf.Length);
        }

        /// <summary>
        /// A selection with one end in a table and the other outside it takes the table whole,
        /// as word processors do: cells join nothing around them, so half a table is nothing a
        /// delete or a copy can act on. The end inside goes to the table's far edge.
        /// </summary>
        private void CoverTables(ref RichPosition anchor, ref RichPosition focus)
        {
            if (TableOf(focus) is RichTableBlock focusTable && !ReferenceEquals(focusTable, TableOf(anchor)))
            {
                focus = RichPosition.Compare(anchor, focus) < 0 ? TableEnd(focusTable) : TableStart(focusTable);
            }

            if (TableOf(anchor) is RichTableBlock anchorTable && !ReferenceEquals(anchorTable, TableOf(focus)))
            {
                anchor = RichPosition.Compare(anchor, focus) < 0 ? TableStart(anchorTable) : TableEnd(anchorTable);
            }
        }

        /// <summary>
        /// Extending back into a table the selection had taken: the table is let go of, or a key
        /// that narrows the selection would be put back at the table's far edge every time.
        /// </summary>
        private RichPosition ReleaseTable(RichPosition focus)
        {
            if (TableOf(focus) is not RichTableBlock table || ReferenceEquals(table, TableOf(_anchor)))
            {
                return focus;
            }

            var forward = RichPosition.Compare(_anchor, focus) < 0;
            var narrowing = forward ? RichPosition.Compare(focus, _focus) < 0 : RichPosition.Compare(focus, _focus) > 0;

            if (!narrowing)
            {
                return focus;
            }

            return (forward ? PositionBefore(table) : PositionAfter(table)) ?? _anchor;
        }

        public RichTableStatus GetTableStatus()
        {
            if (!GetTableSelection(out RichTableBlock table, out RichTableGrid grid, out int top, out int left, out int bottom, out int right))
            {
                return null;
            }

            var cell = grid[top, left];
            var single = true;

            foreach (var other in grid.CellsIn(top, left, bottom, right))
            {
                if (!ReferenceEquals(other, cell))
                {
                    single = false;
                    break;
                }
            }

            return new RichTableStatus
            {
                Table = table,
                Align = cell.Align,
                Valign = cell.Valign,
                IsHeader = cell.IsHeader,
                CanMerge = !single,
                CanSplit = single && (cell.Colspan > 1 || cell.Rowspan > 1),
                CanDeleteRow = grid.Rows > bottom - top + 1,
                CanDeleteColumn = grid.Columns > right - left + 1,
                Rows = grid.Rows,
                Columns = grid.Columns
            };
        }

        private void EditTable(Action<RichTableBlock, RichTableGrid, int, int, int, int> edit, Func<RichTableBlock, RichTableGrid, RichTableCell> caret = null)
        {
            if (!GetTableSelection(out RichTableBlock table, out RichTableGrid grid, out int top, out int left, out int bottom, out int right))
            {
                return;
            }

            BeginChange();

            edit(table, grid, top, left, bottom, right);
            var target = caret?.Invoke(table, grid);
            grid.ApplyTo(table);

            _document.Reindex();

            if (target != null)
            {
                SetCaretInternal(new RichPosition(target.Text, target.Text.Length));
            }
            else
            {
                SetCaretInternal(Clamp(_focus));
            }

            EndChange();
            Raise(RichEditorChanges.Selection, null);
        }

        public void TableInsertRow(bool after)
        {
            var row = -1;
            var column = 0;

            EditTable(
                (table, grid, top, left, bottom, right) =>
                {
                    row = after ? bottom + 1 : top;
                    column = left;
                    grid.InsertRow(row);
                },
                (table, grid) => grid[row, column]);
        }

        public void TableInsertColumn(bool after)
        {
            var row = 0;
            var column = -1;

            EditTable(
                (table, grid, top, left, bottom, right) =>
                {
                    row = top;
                    column = after ? right + 1 : left;
                    grid.InsertColumn(column);
                },
                (table, grid) => grid[row, column]);
        }

        public void TableDeleteRows()
        {
            var row = 0;
            var column = 0;

            EditTable(
                (table, grid, top, left, bottom, right) =>
                {
                    if (grid.Rows <= bottom - top + 1)
                    {
                        return;
                    }

                    for (int r = bottom; r >= top; r--)
                    {
                        grid.RemoveRow(r);
                    }

                    row = Math.Min(top, grid.Rows - 1);
                    column = left;
                },
                (table, grid) => grid[row, column]);
        }

        public void TableDeleteColumns()
        {
            var row = 0;
            var column = 0;

            EditTable(
                (table, grid, top, left, bottom, right) =>
                {
                    if (grid.Columns <= right - left + 1)
                    {
                        return;
                    }

                    for (int x = right; x >= left; x--)
                    {
                        grid.RemoveColumn(x);
                    }

                    row = top;
                    column = Math.Min(left, grid.Columns - 1);
                },
                (table, grid) => grid[row, column]);
        }

        public void TableMergeCells()
        {
            RichTableCell merged = null;

            EditTable(
                (table, grid, top, left, bottom, right) => merged = grid.Merge(top, left, bottom, right),
                (table, grid) => merged);
        }

        public void TableSplitCell()
        {
            RichTableCell split = null;

            EditTable(
                (table, grid, top, left, bottom, right) =>
                {
                    split = grid[top, left];
                    grid.Split(split);
                },
                (table, grid) => split);
        }

        public void TableToggleHeader()
        {
            EditTable((table, grid, top, left, bottom, right) =>
            {
                var value = !grid[top, left].IsHeader;

                foreach (var cell in grid.CellsIn(top, left, bottom, right))
                {
                    cell.IsHeader = value;
                }
            });
        }

        public void TableSetAlignment(PageBlockHorizontalAlignment align)
        {
            EditTable((table, grid, top, left, bottom, right) =>
            {
                foreach (var cell in grid.CellsIn(top, left, bottom, right))
                {
                    cell.Align = align;
                }
            });
        }

        public void TableSetVerticalAlignment(PageBlockVerticalAlignment valign)
        {
            EditTable((table, grid, top, left, bottom, right) =>
            {
                foreach (var cell in grid.CellsIn(top, left, bottom, right))
                {
                    cell.Valign = valign;
                }
            });
        }

        public void TableSetStyle(RichTableBlock table, bool? bordered, bool? striped, bool? compact)
        {
            BeginChange();

            table.IsBordered = bordered ?? table.IsBordered;
            table.IsStriped = striped ?? table.IsStriped;
            table.IsCompact = compact ?? table.IsCompact;

            EndChange();
        }
    }

    public sealed class RichTableStatus
    {
        public RichTableBlock Table { get; init; }

        public PageBlockHorizontalAlignment Align { get; init; }

        public PageBlockVerticalAlignment Valign { get; init; }

        public bool IsHeader { get; init; }

        public bool CanMerge { get; init; }

        public bool CanSplit { get; init; }

        public bool CanDeleteRow { get; init; }

        public bool CanDeleteColumn { get; init; }

        public int Rows { get; init; }

        public int Columns { get; init; }
    }
}
