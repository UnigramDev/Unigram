using System;
using System.Collections.Generic;
using System.Text;

namespace Unigram.Core.Rtf.Write
{
    /// <summary>
    /// Summary description for RtfTable
    /// </summary>
    public class RtfTable : RtfBlock
    {
        private Align _alignment;
        private Margins _margins;
        private int _rowCount;
        private int _colCount;
        private RtfTableCell[][] _cells;
        private float _defaultCellWidth;
        private List<RtfTableCell> _representativeList;
        private bool _startNewPage;
        private float[] _rowHeight;
        private bool[] _rowKeepInSamePage;
        private int _titleRowCount;
        private readonly float _fontSize;
        private RtfCharFormat _defaultCharFormat;
        private Margins[] _cellPadding;

        public RtfTable(int rowCount, int colCount, float horizontalWidth, float fontSize)
        {
            _fontSize = fontSize;
            _alignment = Align.None;
            _margins = new Margins();
            _rowCount = rowCount;
            _colCount = colCount;
            _representativeList = new List<RtfTableCell>();
            _startNewPage = false;
            _titleRowCount = 0;
            _cellPadding = new Margins[_rowCount];
            if (_rowCount < 1 || _colCount < 1) {
                throw new Exception("The number of rows or columns is less than 1.");
            }

            HeaderBackgroundColour = null;
            RowBackgroundColour = null;
            RowAltBackgroundColour = null;

            // Set cell default width according to paper width
            _defaultCellWidth = horizontalWidth / (float)colCount;
            _cells = new RtfTableCell[_rowCount][];
            _rowHeight = new float[_rowCount];
            _rowKeepInSamePage = new bool[_rowCount];
            for (int i = 0; i < _rowCount; i++) {
                _cells[i] = new RtfTableCell[_colCount];
                _rowHeight[i] = 0F;
                _rowKeepInSamePage[i] = false;
                _cellPadding[i] = new Margins();
                for (int j = 0; j < _colCount; j++) {
                    _cells[i][j] = new RtfTableCell(_defaultCellWidth, i, j, this);
                }
            }
        }

        public ColorDescriptor HeaderBackgroundColour { get; set; }
        public ColorDescriptor RowBackgroundColour { get; set; }
        public ColorDescriptor RowAltBackgroundColour { get; set; }

        public override Align Alignment
        {
            get
            {
                return _alignment;
            }
            set
            {
                _alignment = value;
            }
        }

        public override Margins Margins
        {
            get
            {
                return _margins;
            }
        }

        public override RtfCharFormat DefaultCharFormat
        {
            get
            {
                if (_defaultCharFormat == null) {
                    _defaultCharFormat = new RtfCharFormat(-1, -1, 1);
                }
                return _defaultCharFormat;
            }
        }

        public override bool StartNewPage
        {
            get
            {
                return _startNewPage;
            }
            set
            {
                _startNewPage = value;
            }
        }
        
        public int RowCount
        {
            get
            {
                return _rowCount;
            }
        }
        
        public int ColCount
        {
            get
            {
                return _colCount;
            }
        }
        
        /// <summary>
        /// Title row will be displayed in every page on which the table appears.
        /// </summary>
        public int TitleRowCount
        {
            get
            {
                return _titleRowCount;
            }
            set
            {
                _titleRowCount = value;
            }
        }
        
        public Margins[] CellPadding
        {
            get
            {
                return _cellPadding;
            }
        }

        internal override string BlockHead
        {
            set
            {
                throw new Exception("BlockHead is not supported for tables.");
            }
        }

        internal override string BlockTail
        {
            set
            {
                throw new Exception("BlockHead is not supported for tables.");
            }
        }
        
        public RtfTableCell cell(int row, int col)
        {
            if (_cells[row][col].IsMerged) {
                return _cells[row][col].MergeInfo.Representative;
            }
            return _cells[row][col];
        }

        public void setColWidth(int col, float width)
        {
            if (col < 0 || col >= _colCount) {
                throw new Exception("Column index out of range");
            }
            for (int i = 0; i < _rowCount; i++) {
                if (_cells[i][col].IsMerged) {
                    throw new Exception("Column width cannot be set "
                                        + "because some cell in this column has been merged.");
                }
            }
            for (int i = 0; i < _rowCount; i++) {
                _cells[i][col].Width = width;
            }
        }
        
        public void setRowHeight(int row, float height)
        {
            if (row < 0 || row >= _rowCount) {
                throw new Exception("Row index out of range");
            }
            for (int i = 0; i < _colCount; i++) {
                if (_cells[row][i].IsMerged
                    && _cells[row][i].MergeInfo.Representative.MergeInfo.RowSpan > 1) {
                    throw new Exception("Row height cannot be set "
                                        + "because some cell in this row has been merged.");
                }
            }
            _rowHeight[row] = height;
        }
        
        public void setRowKeepInSamePage(int row, bool allow)
        {
            if (row < 0 || row >= _rowCount) {
                throw new Exception("Row index out of range");
            }
            _rowKeepInSamePage[row] = allow;
        }

        public RtfTableCell merge(int topRow, int leftCol, int rowSpan, int colSpan)
        {
            if (topRow < 0 || topRow >= _rowCount) {
                throw new Exception("Row index out of range");
            }
            if (leftCol < 0 || leftCol >= _colCount) {
                throw new Exception("Column index out of range");
            }
            if (rowSpan < 1 || topRow + rowSpan - 1 >= _rowCount) {
                throw new Exception("Row span out of range.");
            }
            if (colSpan < 1 || leftCol + colSpan - 1 >= _colCount) {
                throw new Exception("Column span out of range.");
            }
            if (colSpan == 1 && rowSpan == 1) {
                return cell(topRow, leftCol);
            }
            // Check if the cell has been merged before.
            for (int i = 0; i < rowSpan; i++) {
                for (int j = 0; j < colSpan; j++) {
                    if (_cells[topRow + i][leftCol + j].IsMerged) {
                        throw new Exception("Cannot merge cells because some of the cells has been merged.");
                    }
                }
            }

            float width = 0;
            for (int i = 0; i < rowSpan; i++) {
                for (int j = 0; j < colSpan; j++) {
                    // Sum up the column widths in the first row.
                    if (i == 0) {
                        width += _cells[topRow][leftCol + j].Width;
                    }
                    // Set merge info for each cell.
                    // Note: The representatives of all cells are set to the (topRow, leftCol) cell.
                    _cells[topRow + i][leftCol + j].MergeInfo
                        = new CellMergeInfo(_cells[topRow][leftCol], rowSpan, colSpan, i, j);
                    if (i != 0 || j != 0) {
                        // Transfer the blocks (contents) of each cell to their representative cell.
                        _cells[topRow + i][leftCol + j].TransferBlocksTo(
                            _cells[topRow + i][leftCol + j].MergeInfo.Representative);
                    }
                }
            }
            // Set cell width in the representative cell.
            _cells[topRow][leftCol].Width = width;
            _representativeList.Add(_cells[topRow][leftCol]);
            return _cells[topRow][leftCol];
        }

        private void validateAllMergedCellBorders()
        {
            for (int i = 0; i < _representativeList.Count; i++) {
                validateMergedCellBorders(_representativeList[i]);
            }
        }

        private void validateMergedCellBorders(RtfTableCell representative)
        {
            if (!representative.IsMerged) {
                throw new Exception("Invalid representative (cell is not merged).");
            }
            validateMergedCellBorder(representative, Direction.Top);
            validateMergedCellBorder(representative, Direction.Right);
            validateMergedCellBorder(representative, Direction.Bottom);
            validateMergedCellBorder(representative, Direction.Left);
        }

        private void validateMergedCellBorder(RtfTableCell representative, Direction dir)
        {
            if (!representative.IsMerged) {
                throw new Exception("Invalid representative (cell is not merged).");
            }
            Dictionary<Border, int> stat = new Dictionary<Border, int>();
            Border majorityBorder;
            int majorityCount;
            int limit = (dir == Direction.Top || dir == Direction.Bottom) ?
                representative.MergeInfo.ColSpan : representative.MergeInfo.RowSpan;
            
            for (int i = 0; i < limit; i++) {
                int r, c;
                Border bdr;
                if (dir == Direction.Top || dir == Direction.Bottom) {
                    if (dir == Direction.Top) {
                        r = 0;
                    } else { // dir == bottom
                        r = representative.MergeInfo.RowSpan - 1;
                    }
                    c = i;
                } else { // dir == right || left
                    if (dir == Direction.Right) {
                        c = representative.MergeInfo.ColSpan - 1;
                    } else { // dir == left
                        c = 0;
                    }
                    r = i;
                }
                bdr = _cells[representative.RowIndex + r][representative.ColIndex + c].Borders[dir];
                if (stat.ContainsKey(bdr)) {
                    stat[bdr] = (int)stat[bdr] + 1;
                } else {
                    stat[bdr] = 1;
                }
            }
            majorityCount = -1;
            majorityBorder = representative.Borders[dir];
            foreach(KeyValuePair<Border, int> de in stat) {
                if(de.Value > majorityCount) {
                    majorityCount = de.Value;
                    majorityBorder.Style = de.Key.Style;
                    majorityBorder.Width = de.Key.Width;
                    majorityBorder.Color = de.Key.Color;
                }
            }
        }

        /// <summary>
        /// Set ALL inner borders (colour will be set to default)
        /// </summary>
        /// <param name="style"></param>
        /// <param name="width"></param>
        public void setInnerBorder(BorderStyle style, float width)
        {
            setInnerBorder(style, width, new ColorDescriptor(0));
        }

        /// <summary>
        /// Sets ALL inner borders as specified
        /// </summary>
        /// <param name="style"></param>
        /// <param name="width"></param>
        /// <param name="color"></param>
        public void setInnerBorder(BorderStyle style, float width, ColorDescriptor color)
        {
            for (int i = 0; i < _rowCount; i++) {
                for (int j = 0; j < _colCount; j++) {
                    if (i == 0) {
                        // The first row
                        _cells[i][j].Borders[Direction.Bottom].Style = style;
                        _cells[i][j].Borders[Direction.Bottom].Width = width;
                        _cells[i][j].Borders[Direction.Bottom].Color = color;
                    } else if (i == _rowCount - 1) {
                        // The last row
                        _cells[i][j].Borders[Direction.Top].Style = style;
                        _cells[i][j].Borders[Direction.Top].Width = width;
                        _cells[i][j].Borders[Direction.Top].Color = color;
                    } else {
                        _cells[i][j].Borders[Direction.Top].Style = style;
                        _cells[i][j].Borders[Direction.Top].Width = width;
                        _cells[i][j].Borders[Direction.Top].Color = color;
                        _cells[i][j].Borders[Direction.Bottom].Style = style;
                        _cells[i][j].Borders[Direction.Bottom].Color = color;
                        _cells[i][j].Borders[Direction.Bottom].Width = width;
                    }
                    if (j == 0) {
                        // The first column
                        _cells[i][j].Borders[Direction.Right].Style = style;
                        _cells[i][j].Borders[Direction.Right].Width = width;
                        _cells[i][j].Borders[Direction.Right].Color = color;
                    } else if (j == _colCount - 1) {
                        // The last column
                        _cells[i][j].Borders[Direction.Left].Style = style;
                        _cells[i][j].Borders[Direction.Left].Width = width;
                        _cells[i][j].Borders[Direction.Left].Color = color;
                    } else {
                        _cells[i][j].Borders[Direction.Right].Style = style;
                        _cells[i][j].Borders[Direction.Right].Width = width;
                        _cells[i][j].Borders[Direction.Right].Color = color;
                        _cells[i][j].Borders[Direction.Left].Style = style;
                        _cells[i][j].Borders[Direction.Left].Width = width;
                        _cells[i][j].Borders[Direction.Left].Color = color;
                    }
                }
            }
        }
        
        /// <summary>
        /// Set ALL outer borders (colour will be set to default)
        /// </summary>
        /// <param name="style"></param>
        /// <param name="width"></param>
        public void setOuterBorder(BorderStyle style, float width)
        {
            setOuterBorder(style, width, new ColorDescriptor(0));
        }
        
        /// <summary>
        /// Sets ALL outer borders as specified
        /// </summary>
        /// <param name="style"></param>
        /// <param name="width"></param>
        /// <param name="color"></param>
        public void setOuterBorder(BorderStyle style, float width, ColorDescriptor color)
        {
            for (int i = 0; i < _colCount; i++) {
                _cells[0][i].Borders[Direction.Top].Style = style;
                _cells[0][i].Borders[Direction.Top].Width = width;
                _cells[0][i].Borders[Direction.Top].Color = color;
                _cells[_rowCount - 1][i].Borders[Direction.Bottom].Style = style;
                _cells[_rowCount - 1][i].Borders[Direction.Bottom].Width = width;
                _cells[_rowCount - 1][i].Borders[Direction.Bottom].Color = color;
            }
            for (int i = 0; i < _rowCount; i++) {
                _cells[i][0].Borders[Direction.Left].Style = style;
                _cells[i][0].Borders[Direction.Left].Width = width;
                _cells[i][0].Borders[Direction.Left].Color = color;
                _cells[i][_colCount - 1].Borders[Direction.Right].Style = style;
                _cells[i][_colCount - 1].Borders[Direction.Right].Width = width;
                _cells[i][_colCount - 1].Borders[Direction.Right].Color = color;
            }
        }

        public void setHeaderBorderColors(ColorDescriptor colorOuter, ColorDescriptor colorInner)
        {
            for (int j = 0; j < _colCount; j++)
            {
                _cells[0][j].Borders[Direction.Top].Color = colorOuter;
                _cells[0][j].Borders[Direction.Bottom].Color = colorInner;
                if (j == 0)
                {
                    // The first column
                    _cells[0][j].Borders[Direction.Right].Color = colorInner;
                    _cells[0][j].Borders[Direction.Left].Color = colorOuter;

                }
                else if (j == _colCount - 1)
                {
                    // The last column
                    _cells[0][j].Borders[Direction.Right].Color = colorOuter;
                    _cells[0][j].Borders[Direction.Left].Color = colorInner;

                }
                else
                {
                    _cells[0][j].Borders[Direction.Right].Color = colorInner;
                    _cells[0][j].Borders[Direction.Left].Color = colorInner;
                }
            }
        }

        public override string Render()
        {
            StringBuilder result = new StringBuilder();

            // validate borders for each cell.
            // (borders may be changed because of cell merging)
            validateAllMergedCellBorders();
            // set default char format for each cell.
            if (_defaultCharFormat != null) {
                for (int i = 0; i < _rowCount; i++) {
                    for (int j = 0; j < _colCount; j++) {
                        if (_cells[i][j].IsMerged
                            && _cells[i][j].MergeInfo.Representative != _cells[i][j]) {
                            continue;
                        }
                        if (_cells[i][j].DefaultCharFormat != null) {
                            _cells[i][j].DefaultCharFormat.copyFrom(_defaultCharFormat);
                        }
                    }
                }
            }

            float topMargin = _margins[Direction.Top] - _fontSize;

            if(_startNewPage || topMargin > 0) {
                result.Append(@"{\pard");
                if (_startNewPage) {
                    result.Append(@"\pagebb");
                }
                if (_margins[Direction.Top] >= 0) {
                    result.Append(@"\sl-" + RtfUtility.pt2Twip(topMargin));
                } else {
                    result.Append(@"\sl-1");
                }
                result.AppendLine(@"\slmult0\par}");
            }

            int colAcc;

            for (int i = 0; i < _rowCount; i++)
            {
                colAcc = 0;
                result.Append(@"{\trowd\trgaph" +
                              string.Format(@"\trpaddl{0}\trpaddt{1}\trpaddr{2}\trpaddb{3}",
                                            RtfUtility.pt2Twip(CellPadding[i][Direction.Left]),
                                            RtfUtility.pt2Twip(CellPadding[i][Direction.Top]),
                                            RtfUtility.pt2Twip(CellPadding[i][Direction.Right]),
                                            RtfUtility.pt2Twip(CellPadding[i][Direction.Bottom])));
                switch (_alignment) {
                    case Align.Left:
                        result.Append(@"\trql");
                        break;
                    case Align.Right:
                        result.Append(@"\trqr");
                        break;
                    case Align.Center:
                        result.Append(@"\trqc");
                        break;
                    case Align.FullyJustify:
                        result.Append(@"\trqj");
                        break;
                }
                result.AppendLine();
                if (_margins[Direction.Left] >= 0) {
                    result.AppendLine(@"\trleft" + RtfUtility.pt2Twip(_margins[Direction.Left]));
                    colAcc = RtfUtility.pt2Twip(_margins[Direction.Left]);
                }
                if (_rowHeight[i] > 0) {
                    result.Append(@"\trrh" + RtfUtility.pt2Twip(_rowHeight[i]));
                }
                if (_rowKeepInSamePage[i]) {
                    result.Append(@"\trkeep");
                }
                if (i < _titleRowCount) {
                    result.Append(@"\trhdr");
                }
                result.AppendLine();

                for (int j = 0; j < _colCount; j++)
                {
                    if (_cells[i][j].IsMerged && !_cells[i][j].IsBeginOfColSpan) {
                        continue;
                    }
                    float nextCellLeftBorderClearance = j < _colCount - 1 ? cell(i, j + 1).OuterLeftBorderClearance : 0;
                    colAcc += RtfUtility.pt2Twip(cell(i, j).Width);
                    int colRightPos = colAcc;
                    if(nextCellLeftBorderClearance < 0)
                    {
                        colRightPos += RtfUtility.pt2Twip(nextCellLeftBorderClearance);
                        colRightPos = colRightPos == 0 ? 1 : colRightPos;
                    }

                    // Borders
                    for (Direction d = Direction.Top; d <= Direction.Left; d++) {
                        Border bdr = cell(i, j).Borders[d];
                        if (bdr.Style != BorderStyle.None) {
                            result.Append(@"\clbrdr");
                            switch (d) {
                                case Direction.Top:
                                    result.Append("t");
                                    break;
                                case Direction.Right:
                                    result.Append("r");
                                    break;
                                case Direction.Bottom:
                                    result.Append("b");
                                    break;
                                case Direction.Left:
                                    result.Append("l");
                                    break;
                            }
                            result.Append(@"\brdrw" + RtfUtility.pt2Twip(bdr.Width));
                            result.Append(@"\brdr");
                            switch (bdr.Style) {
                                case BorderStyle.Single:
                                    result.Append("s");
                                    break;
                                case BorderStyle.Dotted:
                                    result.Append("dot");
                                    break;
                                case BorderStyle.Dashed:
                                    result.Append("dash");
                                    break;
                                case BorderStyle.Double:
                                    result.Append("db");
                                    break;
                                default:
                                    throw new Exception("Unkown border style");
                            }
                            result.Append(@"\brdrcf" + bdr.Color.Value);
                        }
                    }

                    // Cell background colour
                    if (cell(i, j).BackgroundColour != null) result.Append(string.Format(@"\clcbpat{0}", cell(i, j).BackgroundColour.Value)); // cell.BackGroundColor overrides others
                    else if (i == 0 && HeaderBackgroundColour != null) result.Append(string.Format(@"\clcbpat{0}", HeaderBackgroundColour.Value)); // header
                    else if (RowBackgroundColour != null && (RowAltBackgroundColour == null || i % 2 == 0)) result.Append(string.Format(@"\clcbpat{0}", RowBackgroundColour.Value)); // row colour
                    else if (RowBackgroundColour != null && RowAltBackgroundColour != null && i % 2 != 0) result.Append(string.Format(@"\clcbpat{0}", RowAltBackgroundColour.Value)); // alt row colour

                    if (_cells[i][j].IsMerged && _cells[i][j].MergeInfo.RowSpan > 1) {
                        if (_cells[i][j].IsBeginOfRowSpan) {
                            result.Append(@"\clvmgf");
                        } else {
                            result.Append(@"\clvmrg");
                        }
                    }
                    switch (_cells[i][j].AlignmentVertical)
                    {
                        case AlignVertical.Top:
                            result.Append(@"\clvertalt");
                            break;
                        case AlignVertical.Middle:
                            result.Append(@"\clvertalc");
                            break;
                        case AlignVertical.Bottom:
                            result.Append(@"\clvertalb");
                            break;
                    }
                    result.AppendLine(@"\cellx" + colRightPos);
                }

                for (int j = 0; j < _colCount; j++)
                {
                    if (!_cells[i][j].IsMerged || _cells[i][j].IsBeginOfColSpan) {
                        result.Append(_cells[i][j].Render());
                    }
                }

                result.AppendLine(@"\row}");
            }

            if (_margins[Direction.Bottom] >= 0) {
                result.Append(@"\sl-" + RtfUtility.pt2Twip(_margins[Direction.Bottom]) + @"\slmult");
            }

            return result.ToString();
        }
    }
}
