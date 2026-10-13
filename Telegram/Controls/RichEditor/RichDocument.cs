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
    public enum RichBlockKind
    {
        Paragraph,
        Heading,
        Preformatted,
        Footer,
        BlockQuote,
        PullQuote,
        List,
        Details,
        Table,
        Divider,
        Anchor,
        Math,
        Media,
        MediaGroup,
        Map,
        ButtonRow,
    }

    /// <summary>
    /// A node of the document. Blocks hold their children in plain lists, and the document
    /// stamps <see cref="Parent"/> and the leaf index on every structural change, so a block
    /// never has to keep either up to date by itself.
    /// </summary>
    public abstract class RichBlock
    {
        public abstract RichBlockKind Kind { get; }

        public RichBlock Parent { get; internal set; }

        /// <summary>
        /// The stops this block spans, itself and its descendants. A closed details block's body
        /// has none, and reports First &gt; Last.
        /// </summary>
        public int FirstStop { get; internal set; }

        public int LastStop { get; internal set; }

        /// <summary>Blocks that cannot hold a caret: they are selected whole.</summary>
        public virtual bool IsAtomic => false;

        /// <summary>The stops of this block in reading order, children included: the block itself if it is atomic, then its leaves.</summary>
        public abstract void CollectStops(List<RichStop> stops);

        /// <summary>The block lists this block owns, for walking the tree.</summary>
        public virtual void CollectChildLists(List<List<RichBlock>> lists)
        {
        }

        public abstract RichBlock Clone();

        protected static List<RichBlock> CloneAll(List<RichBlock> blocks)
        {
            var clone = new List<RichBlock>(blocks.Count);
            foreach (var block in blocks)
            {
                clone.Add(block.Clone());
            }

            return clone;
        }

        protected static void CollectAll(List<RichBlock> blocks, List<RichStop> stops)
        {
            foreach (var block in blocks)
            {
                block.CollectStops(stops);
            }
        }
    }

    /// <summary>Paragraph, heading, code block and footer: one leaf and a kind.</summary>
    public sealed class RichParagraphBlock : RichBlock
    {
        private RichBlockKind _kind;

        public RichParagraphBlock(RichBlockKind kind, RichTextLeaf text = null)
        {
            _kind = kind;
            Text = text ?? new RichTextLeaf(RichLeafRole.Text, kind == RichBlockKind.Preformatted);
            HeadingSize = 2;
            Language = string.Empty;
        }

        public override RichBlockKind Kind => _kind;

        public RichTextLeaf Text { get; }

        /// <summary>1-6, 1 the largest; read only for headings.</summary>
        public int HeadingSize { get; set; }

        /// <summary>The code block's language, empty for none; read only for code blocks.</summary>
        public string Language { get; set; }

        /// <summary>
        /// Turns the block into another text kind in place. Into a code block the text loses
        /// its styles, which is the one lossy conversion: code is plain by definition.
        /// </summary>
        public void SetKind(RichBlockKind kind)
        {
            _kind = kind;
            Text.SetPlain(kind == RichBlockKind.Preformatted);
        }

        public override void CollectStops(List<RichStop> stops) => stops.Add(new RichStop(Text));

        public override RichBlock Clone() => new RichParagraphBlock(_kind, Text.Clone())
        {
            HeadingSize = HeadingSize,
            Language = Language
        };
    }

    public sealed class RichBlockQuote : RichBlock
    {
        public RichBlockQuote(List<RichBlock> children = null, RichTextLeaf credit = null)
        {
            Children = children ?? new List<RichBlock>();
            Credit = credit ?? new RichTextLeaf(RichLeafRole.Credit);
        }

        public override RichBlockKind Kind => RichBlockKind.BlockQuote;

        public List<RichBlock> Children { get; }

        public RichTextLeaf Credit { get; }

        /// <summary>
        /// Shown to its first lines and sent as an expandable quote. Only a quote of one paragraph
        /// is: an expandable quote holds one text, and the paragraphs are joined when it collapses.
        /// </summary>
        public bool IsCollapsed { get; set; }

        /// <summary>Text and nothing else, which is all an expandable quote can hold.</summary>
        public bool IsCollapsible => Children.Count > 0 && Children.TrueForAll(IsParagraph);

        private static bool IsParagraph(RichBlock block) => block is RichParagraphBlock { Kind: RichBlockKind.Paragraph };

        // Collapsed, nothing in it can be edited before it is opened, so it is selected whole;
        // the credit stays a line of its own, as a photo's does.
        public override bool IsAtomic => IsCollapsed;

        public override void CollectStops(List<RichStop> stops)
        {
            if (IsCollapsed)
            {
                stops.Add(new RichStop(this));
            }
            else
            {
                CollectAll(Children, stops);
            }

            stops.Add(new RichStop(Credit));
        }

        public override void CollectChildLists(List<List<RichBlock>> lists) => lists.Add(Children);

        public override RichBlock Clone() => new RichBlockQuote(CloneAll(Children), Credit.Clone()) { IsCollapsed = IsCollapsed };
    }

    /// <summary>A pull quote: one text and a credit.</summary>
    public sealed class RichTextQuote : RichBlock
    {
        public RichTextQuote(RichTextLeaf text = null, RichTextLeaf credit = null)
        {
            Text = text ?? new RichTextLeaf();
            Credit = credit ?? new RichTextLeaf(RichLeafRole.Credit);
        }

        public override RichBlockKind Kind => RichBlockKind.PullQuote;

        public RichTextLeaf Text { get; }

        public RichTextLeaf Credit { get; }

        public override void CollectStops(List<RichStop> stops)
        {
            stops.Add(new RichStop(Text));
            stops.Add(new RichStop(Credit));
        }

        public override RichBlock Clone() => new RichTextQuote(Text.Clone(), Credit.Clone());
    }

    public enum RichListStyle
    {
        Bullet,
        Ordered,
        Checklist,
    }

    public sealed class RichListItem
    {
        public RichListItem(List<RichBlock> children = null)
        {
            Children = children ?? new List<RichBlock>();
        }

        public List<RichBlock> Children { get; }

        public bool IsChecked { get; set; }

        public RichListItem Clone()
        {
            var children = new List<RichBlock>(Children.Count);
            foreach (var child in Children)
            {
                children.Add(child.Clone());
            }

            return new RichListItem(children) { IsChecked = IsChecked };
        }
    }

    /// <summary>
    /// A list. Every item starts with a text block - the editor keeps it that way, as the web
    /// editor's schema did (<c>list_item: paragraph block*</c>) - so an item always has a line
    /// to type on and nested lists hang off a line.
    /// </summary>
    public sealed class RichListBlock : RichBlock
    {
        public RichListBlock(RichListStyle style, List<RichListItem> items = null)
        {
            Style = style;
            Items = items ?? new List<RichListItem>();
            OrderedType = "1";
            Start = 1;
        }

        public override RichBlockKind Kind => RichBlockKind.List;

        public RichListStyle Style { get; set; }

        /// <summary>"1", "a", "A", "i" or "I"; ordered lists only.</summary>
        public string OrderedType { get; set; }

        public int Start { get; set; }

        public List<RichListItem> Items { get; }

        public override void CollectStops(List<RichStop> stops)
        {
            foreach (var item in Items)
            {
                CollectAll(item.Children, stops);
            }
        }

        public override void CollectChildLists(List<List<RichBlock>> lists)
        {
            foreach (var item in Items)
            {
                lists.Add(item.Children);
            }
        }

        public override RichBlock Clone()
        {
            var items = new List<RichListItem>(Items.Count);
            foreach (var item in Items)
            {
                items.Add(item.Clone());
            }

            return new RichListBlock(Style, items)
            {
                OrderedType = OrderedType,
                Start = Start
            };
        }
    }

    public sealed class RichDetailsBlock : RichBlock
    {
        public RichDetailsBlock(RichTextLeaf header = null, List<RichBlock> children = null)
        {
            Header = header ?? new RichTextLeaf(RichLeafRole.Header);
            Children = children ?? new List<RichBlock>();
            IsOpen = true;
        }

        public override RichBlockKind Kind => RichBlockKind.Details;

        public RichTextLeaf Header { get; }

        public List<RichBlock> Children { get; }

        public bool IsOpen { get; set; }

        public override void CollectStops(List<RichStop> stops)
        {
            stops.Add(new RichStop(Header));

            // A closed block's body is hidden in the editor too, and a caret cannot go where
            // nothing is drawn.
            if (IsOpen)
            {
                CollectAll(Children, stops);
            }
        }

        public override void CollectChildLists(List<List<RichBlock>> lists) => lists.Add(Children);

        public override RichBlock Clone() => new RichDetailsBlock(Header.Clone(), CloneAll(Children)) { IsOpen = IsOpen };
    }

    public sealed class RichTableCell
    {
        public RichTableCell(RichTextLeaf text = null)
        {
            Text = text ?? new RichTextLeaf(RichLeafRole.Cell);
            Colspan = 1;
            Rowspan = 1;
            Align = new PageBlockHorizontalAlignmentLeft();
            Valign = new PageBlockVerticalAlignmentTop();
        }

        public RichTextLeaf Text { get; }

        public bool IsHeader { get; set; }

        public int Colspan { get; set; }

        public int Rowspan { get; set; }

        public PageBlockHorizontalAlignment Align { get; set; }

        public PageBlockVerticalAlignment Valign { get; set; }

        public RichTableCell Clone() => new(Text.Clone())
        {
            IsHeader = IsHeader,
            Colspan = Colspan,
            Rowspan = Rowspan,
            Align = Align,
            Valign = Valign
        };
    }

    public sealed class RichTableBlock : RichBlock
    {
        public RichTableBlock(List<List<RichTableCell>> rows = null, RichTextLeaf caption = null)
        {
            Rows = rows ?? new List<List<RichTableCell>>();
            Caption = caption ?? new RichTextLeaf(RichLeafRole.Caption);
            IsBordered = true;
        }

        public override RichBlockKind Kind => RichBlockKind.Table;

        /// <summary>
        /// Rows of cells as TDLib sends them: a cell spanning columns or rows appears once, in
        /// the row it starts in, and the rows below it are shorter by its width.
        /// </summary>
        public List<List<RichTableCell>> Rows { get; }

        public RichTextLeaf Caption { get; }

        public bool IsBordered { get; set; }

        public bool IsStriped { get; set; }

        public bool IsCompact { get; set; }

        // The caption first: it is drawn above the table, as the sent message draws it.
        public override void CollectStops(List<RichStop> stops)
        {
            stops.Add(new RichStop(Caption));

            foreach (var row in Rows)
            {
                foreach (var cell in row)
                {
                    stops.Add(new RichStop(cell.Text));
                }
            }
        }

        public override RichBlock Clone()
        {
            var rows = new List<List<RichTableCell>>(Rows.Count);
            foreach (var row in Rows)
            {
                var clone = new List<RichTableCell>(row.Count);
                foreach (var cell in row)
                {
                    clone.Add(cell.Clone());
                }

                rows.Add(clone);
            }

            return new RichTableBlock(rows, Caption.Clone())
            {
                IsBordered = IsBordered,
                IsStriped = IsStriped,
                IsCompact = IsCompact
            };
        }
    }

    public sealed class RichDividerBlock : RichBlock
    {
        public override RichBlockKind Kind => RichBlockKind.Divider;

        public override bool IsAtomic => true;

        public override void CollectStops(List<RichStop> stops)
        {
            stops.Add(new RichStop(this));
        }

        public override RichBlock Clone() => new RichDividerBlock();
    }

    public sealed class RichAnchorBlock : RichBlock
    {
        public RichAnchorBlock(string name)
        {
            Name = name ?? string.Empty;
        }

        public override RichBlockKind Kind => RichBlockKind.Anchor;

        public override bool IsAtomic => true;

        public string Name { get; set; }

        public override void CollectStops(List<RichStop> stops)
        {
            stops.Add(new RichStop(this));
        }

        public override RichBlock Clone() => new RichAnchorBlock(Name);
    }

    public sealed class RichMathBlock : RichBlock
    {
        public RichMathBlock(string expression)
        {
            Expression = expression ?? string.Empty;
        }

        public override RichBlockKind Kind => RichBlockKind.Math;

        public override bool IsAtomic => true;

        public string Expression { get; set; }

        public override void CollectStops(List<RichStop> stops)
        {
            stops.Add(new RichStop(this));
        }

        public override RichBlock Clone() => new RichMathBlock(Expression);
    }

    public enum RichMediaKind
    {
        Photo,
        Video,
        Animation,
        Audio,
        VoiceNote,
        Document,
    }

    /// <summary>
    /// What a media block shows and sends, immutable and shared between snapshots. Exactly one
    /// of <see cref="Remote"/> and <see cref="Input"/> is set: a block read from a sent message
    /// keeps the display block and is re-sent by file id; a block made in the editor holds the
    /// prepared <c>input*</c> object, and TDLib uploads it with the message.
    /// </summary>
    public sealed class RichMediaSource
    {
        private RichMediaSource(RichMediaKind kind, PageBlock remote, Object input, string localPath, int width, int height, int duration)
        {
            Kind = kind;
            Remote = remote;
            Input = input;
            LocalPath = localPath;
            Width = width;
            Height = height;
            Duration = duration;
        }

        public RichMediaKind Kind { get; }

        /// <summary>The <c>pageBlockPhoto/Video/...</c> the block was read from.</summary>
        public PageBlock Remote { get; }

        /// <summary><c>InputPhoto</c>, <c>InputVideo</c>, <c>InputAnimation</c>, <c>InputAudio</c>, <c>InputVoiceNote</c> or <c>InputDocument</c>.</summary>
        public Object Input { get; }

        /// <summary>The local file a new block was made from, for its preview.</summary>
        public string LocalPath { get; }

        public int Width { get; }

        public int Height { get; }

        public int Duration { get; }

        public static RichMediaSource FromRemote(RichMediaKind kind, PageBlock block, int width, int height, int duration)
            => new(kind, block, null, null, width, height, duration);

        public static RichMediaSource FromInput(RichMediaKind kind, Object input, string localPath, int width, int height, int duration)
            => new(kind, null, input, localPath, width, height, duration);

        public static RichMediaSource FromLocal(RichMediaKind kind, Object input, object local, object preview, int width, int height, int duration)
            => new(kind, null, input, null, width, height, duration) { Local = local, Preview = preview };

        /// <summary>
        /// The <c>StorageMedia</c> a new block was made from, kept so the media editor can open
        /// it again; typed loosely because the model does not depend on the app's entities.
        /// </summary>
        public object Local { get; private init; }

        /// <summary>The still shown for a new block, an <c>ImageSource</c> made once when it was added.</summary>
        public object Preview { get; private init; }
    }

    public sealed class RichMediaBlock : RichBlock
    {
        public RichMediaBlock(RichMediaSource source, RichTextLeaf caption = null, RichTextLeaf credit = null)
        {
            Source = source;
            Caption = caption ?? new RichTextLeaf(RichLeafRole.Caption);
            Credit = credit ?? new RichTextLeaf(RichLeafRole.Credit);
        }

        public override RichBlockKind Kind => RichBlockKind.Media;

        public override bool IsAtomic => true;

        public RichMediaSource Source { get; set; }

        public RichTextLeaf Caption { get; }

        public RichTextLeaf Credit { get; }

        public bool HasSpoiler { get; set; }

        /// <summary>Inside a collage or slideshow the group owns the caption, and items show none.</summary>
        public bool IsGroupItem => Parent is RichMediaGroupBlock;

        public override void CollectStops(List<RichStop> stops)
        {
            stops.Add(new RichStop(this));
            if (!IsGroupItem)
            {
                stops.Add(new RichStop(Caption));
                stops.Add(new RichStop(Credit));
            }
        }

        public override RichBlock Clone() => new RichMediaBlock(Source, Caption.Clone(), Credit.Clone()) { HasSpoiler = HasSpoiler };
    }

    /// <summary>A collage or a slideshow of media blocks.</summary>
    public sealed class RichMediaGroupBlock : RichBlock
    {
        public RichMediaGroupBlock(bool isSlideshow, List<RichBlock> items = null, RichTextLeaf caption = null, RichTextLeaf credit = null)
        {
            IsSlideshow = isSlideshow;
            Items = items ?? new List<RichBlock>();
            Caption = caption ?? new RichTextLeaf(RichLeafRole.Caption);
            Credit = credit ?? new RichTextLeaf(RichLeafRole.Credit);
        }

        public override RichBlockKind Kind => RichBlockKind.MediaGroup;

        public override bool IsAtomic => true;

        public bool IsSlideshow { get; set; }

        /// <summary><see cref="RichMediaBlock"/>s only.</summary>
        public List<RichBlock> Items { get; }

        public RichTextLeaf Caption { get; }

        public RichTextLeaf Credit { get; }

        public override void CollectStops(List<RichStop> stops)
        {
            stops.Add(new RichStop(this));
            stops.Add(new RichStop(Caption));
            stops.Add(new RichStop(Credit));
        }

        public override void CollectChildLists(List<List<RichBlock>> lists) => lists.Add(Items);

        public override RichBlock Clone() => new RichMediaGroupBlock(IsSlideshow, CloneAll(Items), Caption.Clone(), Credit.Clone());
    }

    public sealed class RichMapBlock : RichBlock
    {
        public RichMapBlock(Location location, int zoom, int width, int height, RichTextLeaf caption = null, RichTextLeaf credit = null)
        {
            Location = location;
            Zoom = zoom;
            Width = width;
            Height = height;
            Caption = caption ?? new RichTextLeaf(RichLeafRole.Caption);
            Credit = credit ?? new RichTextLeaf(RichLeafRole.Credit);
        }

        public override RichBlockKind Kind => RichBlockKind.Map;

        public override bool IsAtomic => true;

        public Location Location { get; set; }

        public int Zoom { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public RichTextLeaf Caption { get; }

        public RichTextLeaf Credit { get; }

        public override void CollectStops(List<RichStop> stops)
        {
            stops.Add(new RichStop(this));
            stops.Add(new RichStop(Caption));
            stops.Add(new RichStop(Credit));
        }

        public override RichBlock Clone() => new RichMapBlock(Location, Zoom, Width, Height, Caption.Clone(), Credit.Clone());
    }

    public sealed class RichButtonRowBlock : RichBlock
    {
        public RichButtonRowBlock(List<InlineButton> buttons = null)
        {
            Buttons = buttons ?? new List<InlineButton>();
        }

        public override RichBlockKind Kind => RichBlockKind.ButtonRow;

        public override bool IsAtomic => true;

        /// <summary>Immutable <see cref="InlineButton"/>s: editing one replaces it.</summary>
        public List<InlineButton> Buttons { get; }

        /// <summary>Null for buttons that share the full width.</summary>
        public PageBlockHorizontalAlignment Align { get; set; }

        public override void CollectStops(List<RichStop> stops)
        {
            stops.Add(new RichStop(this));
        }

        public override RichBlock Clone() => new RichButtonRowBlock(new List<InlineButton>(Buttons)) { Align = Align };
    }

    /// <summary>
    /// One place the selection can rest, in reading order: a leaf, or an atomic block selected
    /// whole. Arrow keys walk these, so a divider between two paragraphs is one step, not none.
    /// </summary>
    public readonly struct RichStop
    {
        public RichStop(RichTextLeaf leaf)
        {
            Leaf = leaf;
            Block = null;
        }

        public RichStop(RichBlock block)
        {
            Leaf = null;
            Block = block;
        }

        public RichTextLeaf Leaf { get; }

        public RichBlock Block { get; }

        public bool IsLeaf => Leaf != null;
    }

    /// <summary>
    /// The document: top-level blocks, and the index everything else is addressed through.
    /// </summary>
    public sealed class RichDocument
    {
        private readonly List<RichStop> _stops = new();
        private readonly List<RichTextLeaf> _leaves = new();
        private readonly List<RichBlock> _blocks = new();
        private readonly Dictionary<RichBlock, int> _stopOfBlock = new();

        public RichDocument(List<RichBlock> blocks = null)
        {
            Blocks = blocks ?? new List<RichBlock>();
            IsRtl = false;
            Reindex();
        }

        public List<RichBlock> Blocks { get; }

        public bool IsRtl { get; set; }

        /// <summary>Bumped on every structural change.</summary>
        public int Version { get; private set; }

        public IReadOnlyList<RichTextLeaf> Leaves => _leaves;

        public IReadOnlyList<RichStop> Stops => _stops;

        /// <summary>Every block, containers before their children; how a snapshot names a selected block.</summary>
        public IReadOnlyList<RichBlock> AllBlocks => _blocks;

        public bool IsEmpty
        {
            get
            {
                if (Blocks.Count == 0)
                {
                    return true;
                }

                if (Blocks.Count > 1 || Blocks[0] is not RichParagraphBlock text)
                {
                    return false;
                }

                return text.Kind == RichBlockKind.Paragraph && text.Text.IsEmpty;
            }
        }

        public RichDocument Clone()
        {
            var blocks = new List<RichBlock>(Blocks.Count);
            foreach (var block in Blocks)
            {
                blocks.Add(block.Clone());
            }

            return new RichDocument(blocks) { IsRtl = IsRtl };
        }

        /// <summary>The stop an atomic block occupies, or -1.</summary>
        public int GetStopIndex(RichBlock block)
        {
            return block != null && _stopOfBlock.TryGetValue(block, out int index) ? index : -1;
        }

        /// <summary>
        /// Restamps parents, owners and the indices. Every structural mutation ends here; text
        /// edits inside a leaf do not need it.
        /// </summary>
        public void Reindex()
        {
            Normalize(Blocks);

            // The document is never without a line to type on.
            if (Blocks.Count == 0)
            {
                Blocks.Add(new RichParagraphBlock(RichBlockKind.Paragraph));
            }

            _blocks.Clear();
            Stamp(Blocks, null);

            _stops.Clear();
            _leaves.Clear();
            _stopOfBlock.Clear();

            foreach (var block in Blocks)
            {
                block.CollectStops(_stops);
            }

            foreach (var block in _blocks)
            {
                block.FirstStop = int.MaxValue;
                block.LastStop = -1;
            }

            for (int i = 0; i < _stops.Count; i++)
            {
                var stop = _stops[i];
                RichBlock owner;

                if (stop.Leaf != null)
                {
                    stop.Leaf.Index = _leaves.Count;
                    stop.Leaf.StopIndex = i;
                    _leaves.Add(stop.Leaf);
                    owner = stop.Leaf.Owner;
                }
                else
                {
                    _stopOfBlock[stop.Block] = i;
                    owner = stop.Block;
                }

                for (var block = owner; block != null; block = block.Parent)
                {
                    if (i < block.FirstStop)
                    {
                        block.FirstStop = i;
                    }

                    if (i > block.LastStop)
                    {
                        block.LastStop = i;
                    }
                }
            }

            Version++;
        }

        /// <summary>
        /// The shape rules every edit relies on, restored after it: containers are never empty,
        /// a list item and a quote start with a line of text, and a collage holds only media, and
        /// more than one.
        /// </summary>
        private static void Normalize(List<RichBlock> blocks)
        {
            for (int i = blocks.Count - 1; i >= 0; i--)
            {
                switch (blocks[i])
                {
                    case RichBlockQuote quote:
                        Normalize(quote.Children);
                        EnsureLeadingText(quote.Children);

                        // Whatever undid its single paragraph opens it.
                        if (quote.IsCollapsed && (quote.Children.Count != 1 || !quote.IsCollapsible))
                        {
                            quote.IsCollapsed = false;
                        }
                        break;
                    case RichDetailsBlock details:
                        Normalize(details.Children);
                        EnsureLeadingText(details.Children);
                        break;
                    case RichListBlock list:
                        for (int j = list.Items.Count - 1; j >= 0; j--)
                        {
                            var item = list.Items[j];
                            Normalize(item.Children);

                            if (item.Children.Count == 0)
                            {
                                list.Items.RemoveAt(j);
                            }
                            else
                            {
                                EnsureLeadingText(item.Children);
                            }
                        }

                        if (list.Items.Count == 0)
                        {
                            blocks.RemoveAt(i);
                        }
                        break;
                    case RichMediaGroupBlock group:
                        group.Items.RemoveAll(IsNotMedia);

                        if (group.Items.Count == 0)
                        {
                            blocks.RemoveAt(i);
                        }
                        else if (group.Items.Count == 1 && group.Items[0] is RichMediaBlock media)
                        {
                            // One item is not a collage: it stands on its own, with the
                            // collage's caption, as Ungroup leaves it.
                            media.Caption.SetContent(group.Caption);
                            media.Credit.SetContent(group.Credit);
                            blocks[i] = media;
                        }
                        break;
                    case RichTableBlock table:
                        // A row with no cells of its own is not empty: a cell above may span into
                        // it. Only a table with no cells at all goes.
                        if (!table.Rows.Exists(HasCells))
                        {
                            blocks.RemoveAt(i);
                        }
                        break;
                }
            }
        }

        private static bool IsNotMedia(RichBlock block) => block is not RichMediaBlock;

        private static bool HasCells(List<RichTableCell> row) => row.Count > 0;

        private static void EnsureLeadingText(List<RichBlock> children)
        {
            if (children.Count == 0 || children[0] is not RichParagraphBlock)
            {
                children.Insert(0, new RichParagraphBlock(RichBlockKind.Paragraph));
            }
        }

        private void Stamp(List<RichBlock> blocks, RichBlock parent)
        {
            foreach (var block in blocks)
            {
                block.Parent = parent;
                _blocks.Add(block);
                StampLeaves(block);

                var lists = new List<List<RichBlock>>();
                block.CollectChildLists(lists);

                foreach (var children in lists)
                {
                    Stamp(children, block);
                }
            }
        }

        private static void StampLeaves(RichBlock block)
        {
            // Only the block's own leaves, not its children's: a child stamps its own, and
            // stamping them here first would leave the parent as their owner.
            switch (block)
            {
                case RichParagraphBlock text:
                    text.Text.Owner = block;
                    break;
                case RichBlockQuote quote:
                    quote.Credit.Owner = block;
                    break;
                case RichTextQuote quote:
                    quote.Text.Owner = block;
                    quote.Credit.Owner = block;
                    break;
                case RichDetailsBlock details:
                    details.Header.Owner = block;
                    break;
                case RichTableBlock table:
                    foreach (var row in table.Rows)
                    {
                        foreach (var cell in row)
                        {
                            cell.Text.Owner = block;
                        }
                    }

                    table.Caption.Owner = block;
                    break;
                case RichMediaBlock media:
                    media.Caption.Owner = block;
                    media.Credit.Owner = block;
                    break;
                case RichMediaGroupBlock group:
                    group.Caption.Owner = block;
                    group.Credit.Owner = block;
                    break;
                case RichMapBlock map:
                    map.Caption.Owner = block;
                    map.Credit.Owner = block;
                    break;
            }
        }

        /// <summary>The list <paramref name="block"/> lives in, and its index there.</summary>
        public List<RichBlock> GetContainer(RichBlock block, out int index)
        {
            var list = GetContainer(block);
            index = list?.IndexOf(block) ?? -1;
            return list;
        }

        public List<RichBlock> GetContainer(RichBlock block)
        {
            switch (block.Parent)
            {
                case null:
                    return Blocks;
                case RichBlockQuote quote:
                    return quote.Children;
                case RichDetailsBlock details:
                    return details.Children;
                case RichMediaGroupBlock group:
                    return group.Items;
                case RichListBlock list:
                    foreach (var item in list.Items)
                    {
                        if (item.Children.Contains(block))
                        {
                            return item.Children;
                        }
                    }
                    return null;
                default:
                    return null;
            }
        }

        /// <summary>The list item <paramref name="block"/> is a direct child of, if any.</summary>
        public RichListItem GetListItem(RichBlock block, out RichListBlock list)
        {
            list = block.Parent as RichListBlock;
            if (list == null)
            {
                return null;
            }

            foreach (var item in list.Items)
            {
                if (item.Children.Contains(block))
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>How many containers deep <paramref name="block"/> sits; top level is 1.</summary>
        public static int GetDepth(RichBlock block)
        {
            var depth = 0;
            for (var current = block; current != null; current = current.Parent)
            {
                depth++;
            }

            return depth;
        }
    }
}
