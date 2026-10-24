//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Globalization;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.RichEditor
{
    /// <summary>
    /// What draws one block. Presenters are kept per block object and reused across every
    /// structural change, so pressing Enter re-lays out the two lines it touched rather than the
    /// document; only an undo, which swaps the document for a copy, builds them all again.
    /// </summary>
    public abstract partial class RichBlockPresenter : Control
    {
        protected RichEditorView Owner { get; private set; }

        public RichBlock Block { get; private set; }

        /// <summary>Where a root block is in the document, which is where the editor's panel places it; stale in a nested one.</summary>
        internal int RootIndex { get; set; } = -1;

        private bool _applied;

        protected bool IsApplied => _applied;

        internal void Bind(RichEditorView owner, RichBlock block)
        {
            Owner = owner;
            Block = block;
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            OnTemplate();
            _applied = true;

            if (Block != null && !Owner.IsRealizing)
            {
                Update();
            }
        }

        internal void Update()
        {
            if (_applied && Block != null)
            {
                OnUpdate();
            }
        }

        /// <summary>Takes the template's parts.</summary>
        protected abstract void OnTemplate();

        /// <summary>Shows <see cref="Block"/> as it is now, and registers its leaves with the editor.</summary>
        protected abstract void OnUpdate();

        /// <summary>The element a selected block outlines; a captioned block's media, not its caption.</summary>
        internal virtual FrameworkElement NodeElement => this;

        /// <summary>Makes <paramref name="panel"/> hold the presenters of <paramref name="blocks"/>, in order, and realizes them.</summary>
        protected void Sync(Panel panel, IList<RichBlock> blocks)
        {
            if (panel == null)
            {
                return;
            }

            Owner.Reconcile(panel, blocks);

            for (int i = 0; i < blocks.Count; i++)
            {
                Owner.Realize(blocks, i, false);
            }
        }
    }

    public sealed partial class RichTextBlockPresenter : RichBlockPresenter
    {
        private RichLeafView Leaf;
        private HyperlinkButton LanguageButton;

        public RichTextBlockPresenter()
        {
            DefaultStyleKey = typeof(RichTextBlockPresenter);
        }

        protected override void OnTemplate()
        {
            if (LanguageButton != null)
            {
                LanguageButton.Click -= OnLanguageClick;
            }

            Leaf = GetTemplateChild(nameof(Leaf)) as RichLeafView;
            LanguageButton = GetTemplateChild(nameof(LanguageButton)) as HyperlinkButton;

            if (LanguageButton != null)
            {
                LanguageButton.Click += OnLanguageClick;
            }
        }

        protected override void OnUpdate()
        {
            var block = (RichParagraphBlock)Block;
            var code = block.Kind == RichBlockKind.Preformatted;

            VisualStateManager.GoToState(this, code ? "Code" : "Text", false);

            if (LanguageButton != null && code)
            {
                LanguageButton.Content = string.IsNullOrEmpty(block.Language)
                    ? Strings.ArticleHintLanguage
                    : Native.Highlight.SyntaxToken.GetLanguageName(block.Language);
            }

            var appearance = block.Kind switch
            {
                RichBlockKind.Heading => RichLeafAppearance.Heading1 + Math.Clamp(block.HeadingSize, 1, 6) - 1,
                RichBlockKind.Preformatted => RichLeafAppearance.Preformatted,
                RichBlockKind.Footer => RichLeafAppearance.Footer,
                _ => RichLeafAppearance.Paragraph
            };

            if (Leaf != null)
            {
                Leaf.MaxLines = block.Parent is RichBlockQuote { IsCollapsed: true } ? RichBlockQuotePresenter.CollapsedLines : 0;
            }

            Owner.BindLeaf(Leaf, block.Text, appearance, Owner.GetPlaceholder(block), code ? block.Language : null);
        }

        private void OnLanguageClick(object sender, RoutedEventArgs e)
        {
            if (Block is RichParagraphBlock block)
            {
                Owner.RequestLanguage(block, LanguageButton);
            }
        }
    }

    public sealed partial class RichBlockQuotePresenter : RichBlockPresenter
    {
        // The lines a quote collapses to, as the sent message has it.
        internal const int CollapsedLines = 3;

        private StackPanel Children;
        private RichLeafView Credit;
        private Button Toggle;

        public RichBlockQuotePresenter()
        {
            DefaultStyleKey = typeof(RichBlockQuotePresenter);
        }

        // Collapsed, the text is what is selected whole; the credit under it is still a line.
        internal override FrameworkElement NodeElement => Children ?? (FrameworkElement)this;

        protected override void OnTemplate()
        {
            if (Children != null)
            {
                Children.SizeChanged -= OnChildrenSizeChanged;
            }

            if (Toggle != null)
            {
                Toggle.Click -= OnToggleClick;
            }

            Children = GetTemplateChild(nameof(Children)) as StackPanel;
            Credit = GetTemplateChild(nameof(Credit)) as RichLeafView;
            Toggle = GetTemplateChild(nameof(Toggle)) as Button;

            if (Children != null)
            {
                Children.SizeChanged += OnChildrenSizeChanged;
            }

            if (Toggle != null)
            {
                Toggle.Click += OnToggleClick;
            }
        }

        protected override void OnUpdate()
        {
            var quote = (RichBlockQuote)Block;

            VisualStateManager.GoToState(this, quote.IsCollapsed ? "Collapsed" : "Expanded", false);

            if (Toggle != null)
            {
                AutomationProperties.SetName(Toggle, quote.IsCollapsed ? Strings.AccDescrExpandPanel : Strings.AccDescrCollapsePanel);
            }

            Sync(Children, quote.Children);
            Owner.BindLeaf(Credit, quote.HasCredit ? quote.Credit : null, RichLeafAppearance.QuoteCredit, Owner.GetCreditPlaceholder(quote.Credit));

            if (quote.IsCollapsed)
            {
                Owner.RegisterNode(this);
            }

            UpdateToggle();
        }

        // How many lines the text takes is known once it is laid out, and a change in it is a
        // change in height. The chevron is drawn over the padding and is shorter than the lines
        // it waits for, so showing it changes no size and asks for no further pass.
        private void OnChildrenSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateToggle();
        }

        internal void UpdateToggle()
        {
            if (Toggle == null || Block is not RichBlockQuote quote)
            {
                return;
            }

            var visibility = quote.IsCollapsed || (quote.IsCollapsible && IsLongerThanCollapsed(quote))
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (Toggle.Visibility != visibility)
            {
                Toggle.Visibility = visibility;
            }
        }

        // Empty lines at the end are not counted: Enter on the third line would otherwise offer
        // to collapse three lines, and collapsing drops them anyway.
        private bool IsLongerThanCollapsed(RichBlockQuote quote)
        {
            var last = quote.Children.Count - 1;
            while (last > 0 && quote.Children[last] is RichParagraphBlock { Text.IsEmpty: true })
            {
                last--;
            }

            var lines = 0;

            for (int i = 0; i <= last; i++)
            {
                if (quote.Children[i] is RichParagraphBlock text && Owner.TryGetLeafView(text.Text, out RichLeafView view) && view.TextBlock != null)
                {
                    lines += view.TextBlock.LineCount;

                    // A line break at the very end is drawn as an empty line after it.
                    if (i == last && text.Text.EndsWith('\n'))
                    {
                        lines--;
                    }

                    if (lines > CollapsedLines)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void OnToggleClick(object sender, RoutedEventArgs e)
        {
            if (Block is RichBlockQuote quote)
            {
                Owner.Engine.SetQuoteCollapsed(quote, !quote.IsCollapsed);
            }
        }
    }

    public sealed partial class RichTextQuotePresenter : RichBlockPresenter
    {
        private RichLeafView Text;
        private RichLeafView Credit;

        public RichTextQuotePresenter()
        {
            DefaultStyleKey = typeof(RichTextQuotePresenter);
        }

        protected override void OnTemplate()
        {
            Text = GetTemplateChild(nameof(Text)) as RichLeafView;
            Credit = GetTemplateChild(nameof(Credit)) as RichLeafView;
        }

        protected override void OnUpdate()
        {
            var quote = (RichTextQuote)Block;

            Owner.BindLeaf(Text, quote.Text, RichLeafAppearance.PullQuote, Strings.ArticleHintQuote);
            Owner.BindLeaf(Credit, quote.Credit, RichLeafAppearance.PullQuoteCredit, Owner.GetCreditPlaceholder(quote.Credit));
        }
    }

    public sealed partial class RichListPresenter : RichBlockPresenter
    {
        private StackPanel Items;

        public RichListPresenter()
        {
            DefaultStyleKey = typeof(RichListPresenter);
        }

        protected override void OnTemplate()
        {
            Items = GetTemplateChild(nameof(Items)) as StackPanel;
        }

        protected override void OnUpdate()
        {
            if (Items == null)
            {
                return;
            }

            var list = (RichListBlock)Block;
            Owner.Reconcile(Items, list);

            for (int i = 0; i < list.Items.Count; i++)
            {
                var presenter = Owner.GetItemPresenter(list.Items[i], list);
                presenter.Bind(Owner, list, list.Items[i], i);
                presenter.ApplyTemplate();
                presenter.Update();
            }
        }
    }

    public sealed partial class RichListItemPresenter : Control
    {
        private RichEditorView _owner;
        private RichListBlock _list;
        private RichListItem _item;
        private int _index;

        private TextBlock Marker;
        private CheckBox CheckBox;
        private StackPanel Children;

        public RichListItemPresenter()
        {
            DefaultStyleKey = typeof(RichListItemPresenter);
        }

        internal void Bind(RichEditorView owner, RichListBlock list, RichListItem item, int index)
        {
            _owner = owner;
            _list = list;
            _item = item;
            _index = index;
        }

        protected override void OnApplyTemplate()
        {
            if (CheckBox != null)
            {
                CheckBox.Click -= OnCheckBoxClick;
            }

            Marker = GetTemplateChild(nameof(Marker)) as TextBlock;
            CheckBox = GetTemplateChild(nameof(CheckBox)) as CheckBox;
            Children = GetTemplateChild(nameof(Children)) as StackPanel;

            if (CheckBox != null)
            {
                CheckBox.Click += OnCheckBoxClick;
            }

            if (_owner != null && !_owner.IsRealizing)
            {
                Update();
            }
        }

        internal void Update()
        {
            if (_item == null || Children == null)
            {
                return;
            }

            switch (_list.Style)
            {
                case RichListStyle.Checklist:
                    VisualStateManager.GoToState(this, _item.IsChecked ? "Checked" : "Unchecked", false);

                    if (CheckBox != null)
                    {
                        CheckBox.IsChecked = _item.IsChecked;

                        if (_item.Children.Count > 0 && _item.Children[0] is RichParagraphBlock first)
                        {
                            AutomationProperties.SetName(CheckBox, first.Text.ToPlainText());
                        }
                    }
                    break;
                case RichListStyle.Ordered:
                    VisualStateManager.GoToState(this, "Ordered", false);
                    Marker.Text = FormatNumber(_list.OrderedType, _list.Start + _index) + ".";
                    break;
                default:
                    VisualStateManager.GoToState(this, "Bullet", false);
                    Marker.Text = "•";
                    break;
            }

            _owner.Reconcile(Children, _item.Children);

            for (int i = 0; i < _item.Children.Count; i++)
            {
                _owner.Realize(_item.Children, i, false);
            }
        }

        private void OnCheckBoxClick(object sender, RoutedEventArgs e)
        {
            if (_item != null)
            {
                _owner.Engine.SetChecked(_item, !_item.IsChecked);
            }
        }

        /// <summary>The marker an ordered list draws for <paramref name="value"/>: 1, a, A, i or I.</summary>
        public static string FormatNumber(string type, int value)
        {
            switch (type)
            {
                case "a":
                    return ToLetters(value, 'a');
                case "A":
                    return ToLetters(value, 'A');
                case "i":
                    return ToRoman(value).ToLowerInvariant();
                case "I":
                    return ToRoman(value);
                default:
                    return value.ToString(CultureInfo.CurrentCulture);
            }
        }

        private static string ToLetters(int value, char first)
        {
            if (value <= 0)
            {
                return value.ToString(CultureInfo.CurrentCulture);
            }

            var result = string.Empty;
            while (value > 0)
            {
                value--;
                result = (char)(first + value % 26) + result;
                value /= 26;
            }

            return result;
        }

        private static readonly int[] _romanValues = { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
        private static readonly string[] _romanSymbols = { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };

        private static string ToRoman(int value)
        {
            if (value <= 0 || value >= 4000)
            {
                return value.ToString(CultureInfo.CurrentCulture);
            }

            var result = new System.Text.StringBuilder();
            for (int i = 0; i < _romanValues.Length; i++)
            {
                while (value >= _romanValues[i])
                {
                    result.Append(_romanSymbols[i]);
                    value -= _romanValues[i];
                }
            }

            return result.ToString();
        }
    }

    public sealed partial class RichDetailsPresenter : RichBlockPresenter
    {
        private RichLeafView Header;
        private StackPanel Children;
        private DetailsBlockHeader Toggle;

        public RichDetailsPresenter()
        {
            DefaultStyleKey = typeof(RichDetailsPresenter);
        }

        protected override void OnTemplate()
        {
            if (Toggle != null)
            {
                Toggle.Click -= OnToggleClick;
            }

            Header = GetTemplateChild(nameof(Header)) as RichLeafView;
            Children = GetTemplateChild(nameof(Children)) as StackPanel;
            Toggle = GetTemplateChild(nameof(Toggle)) as DetailsBlockHeader;

            if (Toggle != null)
            {
                Toggle.Click += OnToggleClick;
            }
        }

        protected override void OnUpdate()
        {
            var details = (RichDetailsBlock)Block;

            VisualStateManager.GoToState(this, details.IsOpen ? "Open" : "Closed", false);

            if (Toggle != null)
            {
                Toggle.IsOpen = details.IsOpen;
                AutomationProperties.SetName(Toggle, details.IsOpen ? Strings.AccDescrCollapsePanel : Strings.AccDescrExpandPanel);
            }

            Owner.BindLeaf(Header, details.Header, RichLeafAppearance.DetailsHeader, Strings.ArticleHintDetailsTitle);

            // The body is only realized while open: a closed block's leaves are not in the
            // document's index, and a caret has nowhere to be inside it.
            Sync(Children, details.IsOpen ? details.Children : Array.Empty<RichBlock>());
        }

        private void OnToggleClick(object sender, RoutedEventArgs e)
        {
            if (Block is RichDetailsBlock details)
            {
                Owner.Engine.SetDetailsOpen(details, !details.IsOpen);
            }
        }
    }

    public sealed partial class RichTablePresenter : RichBlockPresenter
    {
        // An empty cell still has to be hit, so no column is narrower than this.
        private const double MinCellWidth = 96;

        private Grid Cells;
        private ScrollViewer Scroller;
        private RichLeafView Caption;

        private readonly TableColumns _resolver = new TableColumns(0, MinCellWidth);

        // The cell presenters as Layout placed them, so a measure walks a list rather than the
        // grid's children.
        private readonly List<RichTableCellPresenter> _cellViews = new List<RichTableCellPresenter>();

        // What the columns were last measured from: the cells' texts, each by its leaf and
        // version, and the scale and border. A measure with all of it unchanged - a scroll, a
        // keystroke outside the table - resolves without asking any text for its widths.
        private int _measuredKey;
        private bool _measured;

        // The shape the grid was built for; a table whose shape has not changed keeps its
        // definitions and only rebinds its cells.
        private int _rows = -1;
        private int _columns = -1;

        // Which slots a cell already covers, row by row; kept, as every pass lays the table out.
        private bool[] _occupied = Array.Empty<bool>();

        public RichTablePresenter()
        {
            DefaultStyleKey = typeof(RichTablePresenter);
        }

        protected override void OnTemplate()
        {
            Cells = GetTemplateChild(nameof(Cells)) as Grid;
            Scroller = GetTemplateChild(nameof(Scroller)) as ScrollViewer;
            Caption = GetTemplateChild(nameof(Caption)) as RichLeafView;
            _rows = _columns = -1;
            _measured = false;
        }

        // The grid is under a ScrollViewer, which measures it with an unbounded width: left to
        // it, every column is as wide as its longest line and nothing wraps. The columns are
        // resolved here instead, in the width the table has, as the sent table resolves them.
        protected override Size MeasureOverride(Size availableSize)
        {
            if (Cells != null && Scroller != null && _columns > 0)
            {
                if (!_measured || _measuredKey != GetMeasureKey())
                {
                    MeasureCells();
                }

                _resolver.Resolve(availableSize.Width);
                _resolver.Apply(Cells, Scroller);
            }

            return base.MeasureOverride(availableSize);
        }

        private int GetMeasureKey()
        {
            var key = new HashCode();
            key.Add(_columns);
            key.Add(XamlRoot?.RasterizationScale ?? 1);
            key.Add(((RichTableBlock)Block).IsBordered);

            foreach (var presenter in _cellViews)
            {
                key.Add(presenter.Cell?.Text);
                key.Add(presenter.Cell?.Text.Version ?? 0);
                key.Add(presenter.Column);
                key.Add(presenter.ColumnSpan);
            }

            return key.ToHashCode();
        }

        private void MeasureCells()
        {
            var padding = _cellViews.Count > 0 ? _cellViews[0].TextMargin : default;
            var thickness = ((RichTableBlock)Block).IsBordered ? 1 : 0;

            _resolver.Begin(_columns, XamlRoot?.RasterizationScale ?? 1, padding, thickness);

            // A cell whose text has no view yet is measured again next time, when it will.
            var complete = true;

            foreach (var presenter in _cellViews)
            {
                if (presenter.GetContentWidths(out double min, out double max))
                {
                    _resolver.Add(presenter.Column, presenter.ColumnSpan, min, max);
                }
                else
                {
                    complete = false;
                }
            }

            _resolver.End();

            _measured = complete;
            _measuredKey = GetMeasureKey();
        }

        protected override void OnUpdate()
        {
            var table = (RichTableBlock)Block;

            if (Cells != null)
            {
                Layout(table);
            }

            Owner.BindLeaf(Caption, table.Caption, RichLeafAppearance.Caption, Owner.GetCaptionPlaceholder(table.Caption, Strings.ArticleTableTitleHint));
        }

        /// <summary>
        /// Places each cell where TDLib's rows put it: a cell occupies the first free column of
        /// its row, and a cell spanning rows takes that column from the rows below it too.
        /// </summary>
        private void Layout(RichTableBlock table)
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

            var slots = table.Rows.Count * columns;
            if (_occupied.Length < slots)
            {
                _occupied = new bool[slots];
            }
            else
            {
                Array.Clear(_occupied, 0, slots);
            }

            var occupied = _occupied;

            if (_rows != table.Rows.Count || _columns != columns)
            {
                _rows = table.Rows.Count;
                _columns = columns;

                Cells.RowDefinitions.Clear();
                Cells.ColumnDefinitions.Clear();

                for (int i = 0; i < _rows; i++)
                {
                    Cells.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                }

                for (int i = 0; i < _columns; i++)
                {
                    Cells.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                }
            }

            var children = Cells.Children;
            var index = 0;

            _cellViews.Clear();

            for (int r = 0; r < table.Rows.Count; r++)
            {
                var column = 0;

                foreach (var cell in table.Rows[r])
                {
                    while (column < columns && occupied[r * columns + column])
                    {
                        column++;
                    }

                    var colspan = Math.Max(1, cell.Colspan);
                    var rowspan = Math.Max(1, cell.Rowspan);

                    for (int y = r; y < Math.Min(r + rowspan, table.Rows.Count); y++)
                    {
                        for (int x = column; x < Math.Min(column + colspan, columns); x++)
                        {
                            occupied[y * columns + x] = true;
                        }
                    }

                    var presenter = Owner.GetCellPresenter(cell);

                    if (index >= children.Count || !ReferenceEquals(children[index], presenter))
                    {
                        Owner.Attach(Cells, presenter, index);
                    }

                    Grid.SetRow(presenter, r);
                    Grid.SetColumn(presenter, column);
                    Grid.SetRowSpan(presenter, rowspan);
                    Grid.SetColumnSpan(presenter, colspan);

                    presenter.Bind(Owner, cell);
                    presenter.Place(table, r, column, Math.Min(r + rowspan, table.Rows.Count) - 1, Math.Min(column + colspan, columns) - 1, table.Rows.Count, columns);
                    presenter.ApplyTemplate();
                    presenter.Update();

                    _cellViews.Add(presenter);
                    index++;
                    column += colspan;
                }
            }

            while (children.Count > index)
            {
                children.RemoveAt(children.Count - 1);
            }
        }
    }

    public sealed partial class RichTableCellPresenter : Control
    {
        private RichEditorView _owner;
        private RichTableCell _cell;
        private RichLeafView Leaf;
        private Border Chrome;

        private RichTableBlock _table;
        private int _row;
        private int _column;
        private int _lastRow;
        private int _lastColumn;
        private int _rows;
        private int _columns;

        private bool _selected;

        /// <summary>Drawn as selected whole: a selection that runs from one cell into another selects cells, not text.</summary>
        internal void SetSelected(bool value)
        {
            if (_selected != value)
            {
                _selected = value;
                VisualStateManager.GoToState(this, value ? "Selected" : "Unselected", false);
            }
        }

        public RichTableCellPresenter()
        {
            DefaultStyleKey = typeof(RichTableCellPresenter);
        }

        internal void Bind(RichEditorView owner, RichTableCell cell)
        {
            _owner = owner;
            _cell = cell;
        }

        internal RichTableCell Cell => _cell;

        internal int Column => _column;

        internal int ColumnSpan => _lastColumn - _column + 1;

        /// <summary>The room between the cell's edges and its text.</summary>
        internal Thickness TextMargin => Leaf?.Margin ?? default;

        // The widths last read from the text, by the leaf and its version: typing into one cell
        // asks that cell again and none of the others.
        private RichTextLeaf _widthsLeaf;
        private int _widthsVersion;
        private double _minWidth;
        private double _maxWidth;

        /// <summary>
        /// The narrowest the cell's text can be laid out without breaking a word, and its widest
        /// line; false while the text has no view to ask.
        /// </summary>
        internal bool GetContentWidths(out double min, out double max)
        {
            var leaf = _cell?.Text;

            if (leaf != null && ReferenceEquals(leaf, _widthsLeaf) && leaf.Version == _widthsVersion)
            {
                min = _minWidth;
                max = _maxWidth;
                return true;
            }

            if (leaf == null || Leaf?.TextBlock is not DirectTextBlock text)
            {
                min = max = 0;
                return false;
            }

            var widths = text.ContentWidths();

            _widthsLeaf = leaf;
            _widthsVersion = leaf.Version;
            _minWidth = min = widths.X;
            _maxWidth = max = widths.Y;

            return true;
        }

        /// <summary>Where the cell is in its table, which decides its edges and corners.</summary>
        internal void Place(RichTableBlock table, int row, int column, int lastRow, int lastColumn, int rows, int columns)
        {
            _table = table;
            _row = row;
            _column = column;
            _lastRow = lastRow;
            _lastColumn = lastColumn;
            _rows = rows;
            _columns = columns;
        }

        protected override void OnApplyTemplate()
        {
            Leaf = GetTemplateChild(nameof(Leaf)) as RichLeafView;
            Chrome = GetTemplateChild(nameof(Chrome)) as Border;

            if (_owner != null && !_owner.IsRealizing)
            {
                Update();
            }
        }

        internal void Update()
        {
            if (_cell == null || Leaf == null)
            {
                return;
            }

            // As PageBlockRenderer draws a cell: borders collapsed onto the right and bottom
            // edges, the outer corners rounded, and a striped table's even rows tinted as a
            // header is.
            VisualStateManager.GoToState(this, _cell.IsHeader || (_table != null && _table.IsStriped && _row % 2 == 0) ? "Header" : "Normal", false);

            if (Chrome != null && _table != null)
            {
                var t = _table.IsBordered ? 1 : 0;
                var last = _lastColumn == _columns - 1;
                var bottom = _lastRow == _rows - 1;

                Chrome.BorderThickness = new Thickness(_column == 0 ? t : 0, _row == 0 ? t : 0, t, t);
                Chrome.CornerRadius = new CornerRadius(
                    _column == 0 && _row == 0 ? 4 : 0,
                    last && _row == 0 ? 4 : 0,
                    last && bottom ? 4 : 0,
                    _column == 0 && bottom ? 4 : 0);
                Leaf.Margin = _table.IsCompact ? new Thickness(4, 2, 4, 2) : new Thickness(8, 4, 8, 4);
            }

            if (Leaf != null)
            {
                Leaf.HitTarget = this;
            }

            Leaf.HorizontalAlignment = _cell.Align switch
            {
                PageBlockHorizontalAlignmentCenter => HorizontalAlignment.Center,
                PageBlockHorizontalAlignmentRight => HorizontalAlignment.Right,
                _ => HorizontalAlignment.Left
            };

            Leaf.VerticalAlignment = _cell.Valign switch
            {
                PageBlockVerticalAlignmentMiddle => VerticalAlignment.Center,
                PageBlockVerticalAlignmentBottom => VerticalAlignment.Bottom,
                _ => VerticalAlignment.Top
            };

            _owner.BindLeaf(Leaf, _cell.Text, _cell.IsHeader ? RichLeafAppearance.HeaderCell : RichLeafAppearance.Paragraph, null);
        }
    }

    /// <summary>
    /// A block selected whole, to automation: an element with its kind and caption as its name,
    /// reached as a child of the editor's text, between the lines it sits between.
    /// </summary>
    public sealed partial class RichBlockAutomationPeer : FrameworkElementAutomationPeer
    {
        private readonly RichBlockPresenter _owner;

        public RichBlockAutomationPeer(RichBlockPresenter owner)
            : base(owner)
        {
            _owner = owner;
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return _owner.Block switch
            {
                RichDividerBlock => AutomationControlType.Separator,
                RichMediaBlock { Source.Kind: RichMediaKind.Photo or RichMediaKind.Video or RichMediaKind.Animation } => AutomationControlType.Image,
                RichMapBlock => AutomationControlType.Image,
                _ => AutomationControlType.Group
            };
        }

        protected override string GetClassNameCore() => _owner.GetType().Name;

        protected override string GetNameCore()
        {
            var name = base.GetNameCore();
            return string.IsNullOrEmpty(name) && _owner.Block != null ? RichEditorView.Describe(_owner.Block) : name;
        }
    }

    /// <summary>Divider, anchor, block formula and button row: blocks with no text to edit.</summary>
    public sealed partial class RichAtomPresenter : RichBlockPresenter
    {
        protected override AutomationPeer OnCreateAutomationPeer() => new RichBlockAutomationPeer(this);

        private TextBlock AnchorName;
        private RichMathImage MathImage;
        private Grid Buttons;

        public RichAtomPresenter()
        {
            DefaultStyleKey = typeof(RichAtomPresenter);
        }

        protected override void OnTemplate()
        {
            AnchorName = GetTemplateChild(nameof(AnchorName)) as TextBlock;
            MathImage = GetTemplateChild(nameof(MathImage)) as RichMathImage;
            Buttons = GetTemplateChild(nameof(Buttons)) as Grid;
        }

        protected override void OnUpdate()
        {
            switch (Block)
            {
                case RichDividerBlock:
                    VisualStateManager.GoToState(this, "Divider", false);
                    break;
                case RichAnchorBlock anchor:
                    VisualStateManager.GoToState(this, "Anchor", false);
                    AnchorName.Text = anchor.Name;
                    break;
                case RichMathBlock math:
                    VisualStateManager.GoToState(this, "Math", false);

                    if (MathImage != null && MathImage.Source != math.Expression)
                    {
                        MathImage.Source = math.Expression;
                    }
                    break;
                case RichButtonRowBlock row:
                    VisualStateManager.GoToState(this, "ButtonRow", false);
                    UpdateButtons(row);
                    break;
            }

            Owner.RegisterNode(this);
        }

        // The buttons last laid out, and how: a row rebuilds its columns only when these change.
        private readonly List<InlineButton> _shownButtons = new();
        private PageBlockHorizontalAlignment _shownAlign;
        private bool _hasShownButtons;

        private void UpdateButtons(RichButtonRowBlock row)
        {
            if (Buttons == null)
            {
                return;
            }

            if (_hasShownButtons && ReferenceEquals(_shownAlign, row.Align) && SameButtons(row.Buttons))
            {
                return;
            }

            _hasShownButtons = true;
            _shownAlign = row.Align;
            _shownButtons.Clear();
            _shownButtons.AddRange(row.Buttons);

            var children = Buttons.Children;

            Buttons.HorizontalAlignment = row.Align switch
            {
                PageBlockHorizontalAlignmentCenter => HorizontalAlignment.Center,
                PageBlockHorizontalAlignmentRight => HorizontalAlignment.Right,
                PageBlockHorizontalAlignmentLeft => HorizontalAlignment.Left,
                _ => HorizontalAlignment.Stretch
            };

            // Equal columns whatever the alignment, as PageBlockRenderer lays the row out.
            var columns = Buttons.ColumnDefinitions;
            while (columns.Count > row.Buttons.Count)
            {
                columns.RemoveAt(columns.Count - 1);
            }

            while (columns.Count < row.Buttons.Count)
            {
                columns.Add(new ColumnDefinition());
            }

            for (int i = 0; i < row.Buttons.Count; i++)
            {
                ReplyMarkupInlineButton element;

                if (i < children.Count)
                {
                    element = (ReplyMarkupInlineButton)children[i];
                }
                else
                {
                    element = new ReplyMarkupInlineButton
                    {
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch,
                        CornerRadius = new CornerRadius(16),
                        IsTabStop = false,
                        AllowFocusOnInteraction = false
                    };

                    element.Click += OnButtonClick;
                    children.Add(element);
                }

                var button = row.Buttons[i];
                element.Tag = i;
                element.SetButton(Owner.ClientService, null, 0, button.Style, button.Type);
                element.Content = RichDocumentConverter.GetPlainText(button.Text);

                Grid.SetColumn(element, i);
            }

            while (children.Count > row.Buttons.Count)
            {
                if (children[children.Count - 1] is ReplyMarkupInlineButton element)
                {
                    element.Click -= OnButtonClick;
                }

                children.RemoveAt(children.Count - 1);
            }
        }

        private bool SameButtons(List<InlineButton> buttons)
        {
            if (buttons.Count != _shownButtons.Count)
            {
                return false;
            }

            for (int i = 0; i < buttons.Count; i++)
            {
                // Buttons are replaced when edited, never changed in place.
                if (!ReferenceEquals(buttons[i], _shownButtons[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private void OnButtonClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: int index } && Block is RichButtonRowBlock row)
            {
                Owner.RequestEditButton(row, index);
            }
        }
    }

    /// <summary>A photo, video, animation, audio, voice note or file; a collage or slideshow; a map.</summary>
    /// <summary>The parts of a media block's template, handed to the renderer together.</summary>
    internal sealed class RichMediaParts
    {
        public RichMediaFrame Media;
        public Border Backdrop;
        public RichCollagePanel Tiles;
        public ImageView Thumbnail;
        public Border Badge;
        public TextBlock BadgeText;
        public Grid FileRow;
        public TextBlock FileIcon;
        public TextBlock FileTitle;
        public TextBlock FileSubtitle;
        public Button PreviousButton;
        public Button NextButton;

        // How far an upload has come: over a picture or a collage, or on a file's icon.
        public Border UploadOverlay;
        public ProgressBarRing UploadProgress;
        public ProgressBarRing FileUploadProgress;

        // The controls on a picture: its menu, more media, and collage or slideshow. A collage
        // has a menu button per tile instead, in TileControls.
        public Button MoreButton;
        public Button AddButton;
        public Button LayoutButton;
        public RichCollagePanel TileControls;

        // What the parts show now. A structural change anywhere in the document updates every
        // block, and a block that did not change must not set its image again - that is a
        // decode, and for a map a request.
        public object Shown;
        public bool IsFile;
        public int ShownPage = -1;
        public bool ShownSlideshow;
        public readonly List<RichMediaSource> ShownItems = new();
    }

    /// <summary>A round button over a picture, as TDesktop's editor puts them there.</summary>
    public sealed partial class RichMediaControlButton : Button
    {
        public RichMediaControlButton()
        {
            DefaultStyleKey = typeof(RichMediaControlButton);
        }
    }

    public sealed partial class RichMediaPresenter : RichBlockPresenter
    {
        protected override AutomationPeer OnCreateAutomationPeer() => new RichBlockAutomationPeer(this);

        private readonly RichMediaParts _parts = new();
        private RichLeafView Caption;
        private RichLeafView Credit;

        // The slide a slideshow shows; kept across updates, clamped to what the group holds.
        private int _page;

        public RichMediaPresenter()
        {
            DefaultStyleKey = typeof(RichMediaPresenter);
        }

        internal override FrameworkElement NodeElement => _parts.Media ?? (FrameworkElement)this;

        protected override void OnTemplate()
        {
            if (_parts.PreviousButton != null)
            {
                _parts.PreviousButton.Click -= OnPreviousClick;
                _parts.NextButton.Click -= OnNextClick;
            }

            if (_parts.MoreButton != null)
            {
                _parts.MoreButton.Click -= OnMoreClick;
                _parts.AddButton.Click -= OnAddClick;
                _parts.LayoutButton.Click -= OnLayoutClick;
            }

            _parts.Media = GetTemplateChild(nameof(_parts.Media)) as RichMediaFrame;
            _parts.Backdrop = GetTemplateChild(nameof(_parts.Backdrop)) as Border;
            _parts.Thumbnail = GetTemplateChild(nameof(_parts.Thumbnail)) as ImageView;
            _parts.Tiles = GetTemplateChild(nameof(_parts.Tiles)) as RichCollagePanel;
            _parts.Badge = GetTemplateChild(nameof(_parts.Badge)) as Border;
            _parts.BadgeText = GetTemplateChild(nameof(_parts.BadgeText)) as TextBlock;
            _parts.FileRow = GetTemplateChild(nameof(_parts.FileRow)) as Grid;
            _parts.FileIcon = GetTemplateChild(nameof(_parts.FileIcon)) as TextBlock;
            _parts.FileTitle = GetTemplateChild(nameof(_parts.FileTitle)) as TextBlock;
            _parts.FileSubtitle = GetTemplateChild(nameof(_parts.FileSubtitle)) as TextBlock;
            _parts.PreviousButton = GetTemplateChild(nameof(_parts.PreviousButton)) as Button;
            _parts.NextButton = GetTemplateChild(nameof(_parts.NextButton)) as Button;
            _parts.UploadOverlay = GetTemplateChild(nameof(_parts.UploadOverlay)) as Border;
            _parts.UploadProgress = GetTemplateChild(nameof(_parts.UploadProgress)) as ProgressBarRing;
            _parts.FileUploadProgress = GetTemplateChild(nameof(_parts.FileUploadProgress)) as ProgressBarRing;

            if (_parts.UploadProgress == null || _parts.FileUploadProgress == null)
            {
                _parts.UploadOverlay = null;
            }
            _parts.MoreButton = GetTemplateChild(nameof(_parts.MoreButton)) as Button;
            _parts.AddButton = GetTemplateChild(nameof(_parts.AddButton)) as Button;
            _parts.LayoutButton = GetTemplateChild(nameof(_parts.LayoutButton)) as Button;
            _parts.TileControls = GetTemplateChild(nameof(_parts.TileControls)) as RichCollagePanel;
            Caption = GetTemplateChild(nameof(Caption)) as RichLeafView;
            Credit = GetTemplateChild(nameof(Credit)) as RichLeafView;

            // New parts show nothing yet, whatever the old ones showed.
            _parts.Shown = null;
            _parts.ShownItems.Clear();

            if (_parts.PreviousButton != null && _parts.NextButton != null)
            {
                _parts.PreviousButton.Click += OnPreviousClick;
                _parts.NextButton.Click += OnNextClick;
            }

            if (_parts.MoreButton != null && _parts.AddButton != null && _parts.LayoutButton != null)
            {
                _parts.MoreButton.Click += OnMoreClick;
                _parts.AddButton.Click += OnAddClick;
                _parts.LayoutButton.Click += OnLayoutClick;
            }
            else
            {
                _parts.MoreButton = _parts.AddButton = _parts.LayoutButton = null;
            }
        }

        // The menu of what the button is on: a slideshow's current slide, or the block.
        private void OnMoreClick(object sender, RoutedEventArgs e)
        {
            var node = Block is RichMediaGroupBlock group && group.Items.Count > 0
                ? group.Items[System.Math.Clamp(_page, 0, group.Items.Count - 1)]
                : Block;

            Owner.RequestMediaMenu(node, _parts.MoreButton);
        }

        private void OnAddClick(object sender, RoutedEventArgs e)
        {
            Owner.RequestMediaAdd(Block);
        }

        private void OnLayoutClick(object sender, RoutedEventArgs e)
        {
            if (Block is RichMediaGroupBlock group)
            {
                Owner.Engine.SetSlideshow(group, !group.IsSlideshow);
            }
        }

        /// <summary>Whether <paramref name="source"/> is what this block shows, or one of its tiles.</summary>
        internal bool Shows(RichMediaSource source)
        {
            return ReferenceEquals(_parts.Shown, source) || _parts.ShownItems.Contains(source);
        }

        internal void UpdateUpload()
        {
            Owner.Media.ShowUpload(_parts, Block);
        }

        private void OnPreviousClick(object sender, RoutedEventArgs e) => Turn(-1);

        private void OnNextClick(object sender, RoutedEventArgs e) => Turn(1);

        private void Turn(int direction)
        {
            if (Block is RichMediaGroupBlock group && group.Items.Count > 0)
            {
                _page = (_page + direction + group.Items.Count) % group.Items.Count;
                Update();
            }
        }

        protected override void OnUpdate()
        {
            RichTextLeaf caption = null;
            RichTextLeaf credit = null;

            switch (Block)
            {
                case RichMediaBlock media:
                    caption = media.Caption;
                    credit = media.Credit;
                    VisualStateManager.GoToState(this, media.HasSpoiler ? "Spoiler" : "NoSpoiler", false);
                    Owner.Media.Show(_parts, media);
                    break;
                case RichMediaGroupBlock group:
                    caption = group.Caption;
                    credit = group.Credit;
                    VisualStateManager.GoToState(this, "NoSpoiler", false);
                    _page = group.Items.Count > 0 ? System.Math.Min(_page, group.Items.Count - 1) : 0;
                    Owner.Media.Show(_parts, group, _page);
                    break;
                case RichMapBlock map:
                    caption = map.Caption;
                    credit = map.Credit;
                    VisualStateManager.GoToState(this, "NoSpoiler", false);
                    Owner.Media.Show(_parts, map);
                    break;
            }

            Owner.Media.ShowUpload(_parts, Block);

            // Under a picture the caption is inset, as the sent message has it; under a file or
            // a map it lines up with the block.
            var inset = _parts.IsFile || Block is RichMapBlock ? 0 : 12;
            if (Caption != null && Caption.Margin.Left != inset)
            {
                Caption.Margin = new Thickness(inset, 8, 0, 0);
                Credit.Margin = new Thickness(inset, 0, 0, 0);
            }

            Owner.BindLeaf(Caption, caption, RichLeafAppearance.Caption, Owner.GetCaptionPlaceholder(caption, Strings.AddCaption));
            Owner.BindLeaf(Credit, credit, RichLeafAppearance.Caption, Owner.GetCreditPlaceholder(credit));
            Owner.RegisterNode(this);
        }
    }
}
