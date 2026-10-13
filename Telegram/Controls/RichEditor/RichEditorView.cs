//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Numerics;
using Telegram.Navigation;
using Telegram.Services;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls.RichEditor
{
    /// <summary>
    /// The native rich message editor. A <see cref="RichEditorEngine"/> holds the document and
    /// the selection; this draws them and turns input into engine calls. Text is entered
    /// through a <see cref="Windows.UI.Text.Core.CoreTextEditContext"/> over the leaf the caret
    /// is in, which is what gives the IME, the touch keyboard, dictation and handwriting
    /// without a text control underneath.
    /// </summary>
    public sealed partial class RichEditorView : Control
    {
        private RichEditorEngine _engine;
        private IClientService _clientService;

        private ScrollViewer ScrollViewer;

        // Where pointer input is caught - the whole content, padding included - and the surface
        // every coordinate in this control is measured from: the one the blocks and the layers
        // drawn over them share.
        private RichEditorColumn Root;
        private Grid Surface;
        private StackPanel Blocks;
        private Canvas Decorations;
        private Rectangle SpoilerSwatch;
        private Rectangle MarkedSwatch;

        private readonly List<Rectangle> _decorationShapes = new();
        private readonly List<(int Start, int End)> _markRanges = new();
        private Border NodeOutline;
        private Rectangle Caret;
        private RichSelectionGripper StartGripper;
        private RichSelectionGripper EndGripper;

        // Presenters by the object they draw. Kept across structural changes, so a block that
        // only moved keeps its laid out text; pruned after each pass of whatever was not used.
        private readonly Dictionary<RichBlock, RichBlockPresenter> _presenters = new();
        private readonly Dictionary<RichListItem, RichListItemPresenter> _itemPresenters = new();
        private readonly Dictionary<RichTableCell, RichTableCellPresenter> _cellPresenters = new();

        // What the last realize pass touched; whatever is left out of it is gone from the document.
        private readonly HashSet<object> _realized = new();

        private readonly Dictionary<RichTextLeaf, RichLeafView> _leafViews = new();
        private readonly Dictionary<RichBlock, RichBlockPresenter> _nodeViews = new();

        // The tiles of collages, by the item they show: selected and outlined on their own.
        private readonly Dictionary<RichBlock, FrameworkElement> _itemViews = new();

        // Leaves drawing a selection right now, so the next selection clears exactly those.
        private readonly List<RichLeafView> _selectedViews = new();

        // The same for cells selected whole.
        private readonly List<RichTableCellPresenter> _selectedCellViews = new();
        private readonly List<RichTableCell> _selectedCells = new();

        private RichTextLeaf _placeholderLeaf;

        private ScalarKeyFrameAnimation _blink;
        private Windows.UI.ViewManagement.UISettings _settings;

        public RichEditorView()
        {
            DefaultStyleKey = typeof(RichEditorView);

            Media = new RichMediaRenderer(this);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public RichEditorEngine Engine => _engine;

        public IClientService ClientService => _clientService;

        internal RichMediaRenderer Media { get; }

        #region MaxContentWidth

        public double MaxContentWidth
        {
            get => (double)GetValue(MaxContentWidthProperty);
            set => SetValue(MaxContentWidthProperty, value);
        }

        public static readonly DependencyProperty MaxContentWidthProperty =
            DependencyProperty.Register("MaxContentWidth", typeof(double), typeof(RichEditorView), new PropertyMetadata(double.PositiveInfinity));

        #endregion

        /// <summary>Raised after the document or the selection changed and the view caught up: what a toolbar listens to.</summary>
        public event EventHandler StateChanged;

        /// <summary>Raised when the user asks for the code block's language to be picked.</summary>
        public event EventHandler<RichLanguageRequestedEventArgs> LanguageRequested;

        /// <summary>Raised when the user opens a button of a button row for editing.</summary>
        public event EventHandler<RichButtonRequestedEventArgs> ButtonRequested;

        /// <summary>Raised when the user opens an inline object - a formula, a button - for editing.</summary>
        public event EventHandler<RichObjectRequestedEventArgs> ObjectRequested;

        public void Initialize(IClientService clientService, RichEditorEngine engine)
        {
            if (_engine != null)
            {
                _engine.Changed -= OnEngineChanged;
            }

            _clientService = clientService;
            _engine = engine;
            _engine.Changed += OnEngineChanged;

            Refresh(RichEditorChanges.Structure | RichEditorChanges.Selection, null);
        }

        protected override void OnApplyTemplate()
        {
            if (Root != null)
            {
                UnhookPointer(Root);
            }

            if (StartGripper != null)
            {
                UnhookGripper(StartGripper);
                UnhookGripper(EndGripper);
            }

            if (ScrollViewer != null)
            {
                ScrollViewer.ViewChanged -= OnViewChanged;
            }

            ScrollViewer = GetTemplateChild(nameof(ScrollViewer)) as ScrollViewer;
            Root = GetTemplateChild(nameof(Root)) as RichEditorColumn;
            Surface = GetTemplateChild(nameof(Surface)) as Grid;
            Blocks = GetTemplateChild(nameof(Blocks)) as StackPanel;
            Decorations = GetTemplateChild(nameof(Decorations)) as Canvas;
            SpoilerSwatch = GetTemplateChild(nameof(SpoilerSwatch)) as Rectangle;
            MarkedSwatch = GetTemplateChild(nameof(MarkedSwatch)) as Rectangle;
            _decorationShapes.Clear();
            NodeOutline = GetTemplateChild(nameof(NodeOutline)) as Border;
            Caret = GetTemplateChild(nameof(Caret)) as Rectangle;
            StartGripper = GetTemplateChild(nameof(StartGripper)) as RichSelectionGripper;
            EndGripper = GetTemplateChild(nameof(EndGripper)) as RichSelectionGripper;

            if (Root != null)
            {
                HookPointer(Root);
            }

            if (StartGripper != null && EndGripper != null)
            {
                HookGripper(StartGripper);
                HookGripper(EndGripper);
            }

            if (ScrollViewer != null)
            {
                ScrollViewer.ViewChanged += OnViewChanged;
            }

            _presenters.Clear();
            _itemPresenters.Clear();
            _cellPresenters.Clear();

            if (_engine != null)
            {
                Refresh(RichEditorChanges.Structure | RichEditorChanges.Selection, null);
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            CreateEditContext();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            DestroyEditContext();

            if (_scrollTimer != null)
            {
                _scrollTimer.Stop();
                _scrollTimer.Tick -= OnScrollTimerTick;
                _scrollTimer = null;
            }
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new RichEditorAutomationPeer(this);
        }

        // =====================================================================================
        // Keeping up with the engine
        // =====================================================================================

        private void OnEngineChanged(object sender, RichEditorChangedEventArgs e)
        {
            Refresh(e.Changes, e.Leaf);
            SyncEditContext(e.Changes);
            (Peer as RichEditorAutomationPeer)?.RaiseChanged(e.Changes);
            Announce();

            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        private RichEditorAutomationPeer Peer => FrameworkElementAutomationPeer.FromElement(this) as RichEditorAutomationPeer;

        private void Refresh(RichEditorChanges changes, RichTextLeaf leaf)
        {
            if (Blocks == null || _engine == null)
            {
                return;
            }

            if ((changes & RichEditorChanges.Structure) != 0)
            {
                Realize();
                _caretStale = true;
            }
            else if ((changes & RichEditorChanges.Text) != 0 && leaf != null && _leafViews.TryGetValue(leaf, out RichLeafView view))
            {
                view.Render();
                RefreshCredits(leaf);
            }

            UpdatePlaceholders();
            UpdateSelection();
        }

        /// <summary>The whole tree again, reusing every presenter whose object survived.</summary>
        private void Realize()
        {
            _bounds.Clear();
            _realized.Clear();
            _leafViews.Clear();
            _nodeViews.Clear();
            _itemViews.Clear();

            var blocks = _engine.Document.Blocks;
            var children = Blocks.Children;
            var same = children.Count == blocks.Count;

            for (int i = 0; i < blocks.Count && same; i++)
            {
                same = ReferenceEquals(children[i], GetPresenter(blocks[i]));
            }

            if (!same)
            {
                children.Clear();

                foreach (var block in blocks)
                {
                    Attach(Blocks, GetPresenter(block));
                }
            }

            for (int i = 0; i < blocks.Count; i++)
            {
                Realize(blocks, i, true);
            }

            Prune(_presenters);
            Prune(_itemPresenters);
            Prune(_cellPresenters);
        }

        private void Prune<TKey, TValue>(Dictionary<TKey, TValue> presenters) where TValue : FrameworkElement
        {
            List<TKey> stale = null;

            foreach (var pair in presenters)
            {
                if (!_realized.Contains(pair.Key))
                {
                    (stale ??= new List<TKey>()).Add(pair.Key);
                }
            }

            if (stale != null)
            {
                foreach (var key in stale)
                {
                    Detach(presenters[key]);
                    presenters.Remove(key);
                }
            }
        }

        /// <summary>
        /// While true, a presenter whose template is applied leaves its update to the pass that
        /// is running, which calls it right after; outside a pass - a template applied late by
        /// layout - the presenter updates itself.
        /// </summary>
        internal bool IsRealizing { get; private set; }

        /// <summary>
        /// Realizes the block at <paramref name="index"/> of a list, spaced from its neighbours
        /// as <c>PageBlockRenderer.UpdateSpacing</c> spaces the sent message, so a block
        /// sits where it will be read. <paramref name="root"/> is the document's own list.
        /// </summary>
        internal void Realize(IList<RichBlock> blocks, int index, bool root)
        {
            var block = blocks[index];
            var presenter = GetPresenter(block);

            var margin = Spacing(blocks, index, root);
            if (presenter.Margin != margin)
            {
                presenter.Margin = margin;
            }

            Realize(presenter, block);
        }

        private static Thickness Spacing(IList<RichBlock> blocks, int index, bool root)
        {
            var block = blocks[index];
            var kind = block.Kind;

            RichBlock previous = null;
            for (int i = index - 1; i >= 0 && previous == null; i--)
            {
                if (blocks[i].Kind != RichBlockKind.Anchor)
                {
                    previous = blocks[i];
                }
            }

            double top;
            if (index == 0)
            {
                top = root && IsAudio(block) ? 4 : 0;
            }
            else if (kind == RichBlockKind.Anchor || (kind == RichBlockKind.Paragraph && previous?.Kind == RichBlockKind.Paragraph))
            {
                top = 0;
            }
            else if (kind == RichBlockKind.ButtonRow && previous?.Kind == RichBlockKind.ButtonRow)
            {
                top = 4;
            }
            else if (kind == RichBlockKind.Divider)
            {
                top = 12;
            }
            else
            {
                top = 8;
            }

            var bottom = kind == RichBlockKind.Divider ? 4 : root && index == blocks.Count - 1 && !IsFullMedia(block) ? 6 : 0;
            var padding = root && !IsEdgeToEdge(block) ? 10 : 0;

            return new Thickness(padding, top, padding, bottom);
        }

        private static bool IsAudio(RichBlock block)
        {
            return block is RichMediaBlock { Source.Kind: RichMediaKind.Audio };
        }

        /// <summary>Media that runs to the edges of the message, as photos, videos, collages and maps do; an animation does not.</summary>
        private static bool IsEdgeToEdge(RichBlock block)
        {
            return block switch
            {
                RichMediaBlock media => media.Source.Kind is RichMediaKind.Photo or RichMediaKind.Video,
                RichMediaGroupBlock or RichMapBlock => true,
                _ => false
            };
        }

        private static bool IsFullMedia(RichBlock block)
        {
            return block switch
            {
                RichMediaBlock media => media.Source.Kind is RichMediaKind.Photo or RichMediaKind.Video or RichMediaKind.Animation && media.Caption.IsEmpty,
                RichMediaGroupBlock group => group.Caption.IsEmpty,
                RichMapBlock map => map.Caption.IsEmpty,
                _ => false
            };
        }

        internal void Realize(RichBlockPresenter presenter, RichBlock block)
        {
            _realized.Add(block);

            var realizing = IsRealizing;
            IsRealizing = true;

            presenter.Bind(this, block);

            // Explicitly, rather than waiting for the measure pass: the leaves register while
            // their presenter updates, and the selection is drawn into them right after this.
            presenter.ApplyTemplate();
            presenter.Update();

            IsRealizing = realizing;
        }

        internal RichBlockPresenter GetPresenter(RichBlock block)
        {
            if (_presenters.TryGetValue(block, out RichBlockPresenter presenter))
            {
                return presenter;
            }

            presenter = block switch
            {
                RichParagraphBlock => new RichTextBlockPresenter(),
                RichBlockQuote => new RichBlockQuotePresenter(),
                RichTextQuote => new RichTextQuotePresenter(),
                RichListBlock => new RichListPresenter(),
                RichDetailsBlock => new RichDetailsPresenter(),
                RichTableBlock => new RichTablePresenter(),
                RichMediaBlock or RichMediaGroupBlock or RichMapBlock => new RichMediaPresenter(),
                _ => new RichAtomPresenter()
            };

            _presenters[block] = presenter;
            return presenter;
        }

        internal RichListItemPresenter GetItemPresenter(RichListItem item)
        {
            _realized.Add(item);

            if (!_itemPresenters.TryGetValue(item, out RichListItemPresenter presenter))
            {
                _itemPresenters[item] = presenter = new RichListItemPresenter();
            }

            return presenter;
        }

        internal RichTableCellPresenter GetCellPresenter(RichTableCell cell)
        {
            _realized.Add(cell);

            if (!_cellPresenters.TryGetValue(cell, out RichTableCellPresenter presenter))
            {
                _cellPresenters[cell] = presenter = new RichTableCellPresenter();
            }

            return presenter;
        }

        /// <summary>Takes a presenter out of the panel <see cref="Attach"/> put it in.</summary>
        internal void Detach(FrameworkElement element)
        {
            if (_hosts.TryGetValue(element, out Panel panel))
            {
                _hosts.Remove(element);

                var index = panel.Children.IndexOf(element);
                if (index >= 0)
                {
                    panel.Children.RemoveAt(index);
                }
            }
        }

        /// <summary>
        /// Puts a presenter in a panel at <paramref name="index"/>, or last, taking it out of the
        /// one it was in first.
        /// </summary>
        internal void Attach(Panel panel, FrameworkElement element, int index = -1)
        {
            Detach(element);

            var children = panel.Children;
            if (index >= 0 && index < children.Count)
            {
                children.Insert(index, element);
            }
            else
            {
                children.Add(element);
            }

            _hosts[element] = panel;
        }

        // Which panel each presenter was put in. XAML cannot say: an element in a panel that has
        // left the live tree answers null to both Parent and VisualTreeHelper.GetParent, and is
        // still that panel's child as far as Children.Add is concerned.
        private readonly Dictionary<FrameworkElement, Panel> _hosts = new();

        internal void BindLeaf(RichLeafView view, RichTextLeaf leaf, RichLeafAppearance appearance, string placeholder, string language = null)
        {
            if (view == null)
            {
                return;
            }

            if (leaf == null)
            {
                view.Visibility = Visibility.Collapsed;
                return;
            }

            view.Visibility = _engine.IsLeafHidden(leaf) ? Visibility.Collapsed : Visibility.Visible;

            UnhookLeaf(view);
            view.Bind(_clientService, leaf, appearance, placeholder, language);
            HookLeaf(view);

            _leafViews[leaf] = view;
        }

        private void HookLeaf(RichLeafView view)
        {
            view.ObjectClick += OnLeafObjectClick;
        }

        private void UnhookLeaf(RichLeafView view)
        {
            view.ObjectClick -= OnLeafObjectClick;
        }

        private void OnLeafObjectClick(object sender, RichInlineObjectClickEventArgs e)
        {
            ObjectRequested?.Invoke(this, new RichObjectRequestedEventArgs(e.Leaf, -1, RichInlineObject.FromButton(e.Button)));
        }

        internal void RegisterNode(RichBlockPresenter presenter)
        {
            _nodeViews[presenter.Block] = presenter;
        }

        internal void RegisterItem(RichBlock item, FrameworkElement element)
        {
            _itemViews[item] = element;
        }

        internal void UnregisterItems(RichMediaGroupBlock group)
        {
            foreach (var item in group.Items)
            {
                _itemViews.Remove(item);
            }
        }

        internal bool TryGetLeafView(RichTextLeaf leaf, out RichLeafView view) => _leafViews.TryGetValue(leaf, out view);

        internal bool TryGetNodeView(RichBlock block, out FrameworkElement element)
        {
            if (_nodeViews.TryGetValue(block, out RichBlockPresenter presenter))
            {
                element = presenter;
                return true;
            }

            element = null;
            return false;
        }

        /// <summary>What a block selected whole is called, to a screen reader: its kind, and its caption if it has one.</summary>
        internal static string Describe(RichBlock block)
        {
            string kind = block switch
            {
                RichDividerBlock => Strings.ArticleCommandDivider,
                RichAnchorBlock anchor => "#" + anchor.Name,
                RichMathBlock math => Strings.AccDescrIVFormula + ", " + math.Expression,
                RichButtonRowBlock row => Strings.AccDescrIVButtons + ", " + string.Join(", ", ButtonLabels(row)),
                RichMediaGroupBlock group => (group.IsSlideshow ? Strings.AccDescrIVSlideshow : Strings.AccDescrCollage) + ", " + group.Items.Count.ToString(System.Globalization.CultureInfo.CurrentCulture),
                RichMapBlock => Strings.AttachLocation,
                RichMediaBlock media => media.Source.Kind switch
                {
                    RichMediaKind.Photo => Strings.AttachPhoto,
                    RichMediaKind.Video => Strings.AttachVideo,
                    RichMediaKind.Animation => Strings.AttachGif,
                    RichMediaKind.Audio => Strings.AccDescrIVAudio,
                    RichMediaKind.VoiceNote => Strings.AttachAudio,
                    _ => Strings.AttachDocument
                },
                RichTableBlock => Strings.AccDescrIVTable,
                _ => null
            };

            var caption = block switch
            {
                RichMediaBlock media => media.Caption,
                RichMediaGroupBlock group => group.Caption,
                RichMapBlock map => map.Caption,
                _ => null
            };

            var spoiler = block is RichMediaBlock { HasSpoiler: true } ? ", " + Strings.Spoiler : string.Empty;

            return caption == null || caption.IsEmpty
                ? kind + spoiler
                : kind + spoiler + ", " + caption.ToPlainText();
        }

        private static IEnumerable<string> ButtonLabels(RichButtonRowBlock row)
        {
            foreach (var button in row.Buttons)
            {
                yield return RichDocumentConverter.GetPlainText(button.Text);
            }
        }

        /// <summary>What kind of line the caret is on, said when a command changes it.</summary>
        internal static string DescribeLine(RichTextLeaf leaf)
        {
            for (var block = leaf.Owner; block != null; block = block.Parent)
            {
                if (block is RichListBlock list)
                {
                    return list.Style switch
                    {
                        RichListStyle.Ordered => Strings.ArticleListNumbered,
                        RichListStyle.Checklist => Strings.ArticleListTodo,
                        _ => Strings.ArticleListBulleted
                    };
                }

                if (block is RichBlockQuote)
                {
                    return Strings.AccDescrIVBlockquote;
                }
            }

            return leaf.Owner switch
            {
                RichParagraphBlock { Kind: RichBlockKind.Heading } heading => Strings.AccDescrIVHeading + " " + heading.HeadingSize.ToString(System.Globalization.CultureInfo.CurrentCulture),
                RichParagraphBlock { Kind: RichBlockKind.Preformatted } => Strings.AccDescrIVCode,
                RichParagraphBlock { Kind: RichBlockKind.Footer } => Strings.AccDescrIVFooter,
                RichTextQuote { IsExpandable: false } => Strings.AccDescrIVPullquote,
                RichTextQuote => Strings.AccDescrIVBlockquote,
                RichDetailsBlock details => Strings.AccDescrIVDetails + ", " + (details.IsOpen ? Strings.AccDescrIVExpanded : Strings.AccDescrIVCollapsed),
                _ => Strings.ArticleText
            };
        }

        private RichBlock _announcedNode;
        private RichTextLeaf _announcedLeaf;
        private string _announcedLine;

        /// <summary>
        /// The two things a reader following the text pattern would miss: arriving on a block
        /// selected whole, which has no text, and a command changing the kind of the line the
        /// caret is on - Ctrl+Alt+2 makes a heading without the caret moving at all.
        /// </summary>
        private void Announce()
        {
            var peer = Peer;
            if (peer == null || _engine == null)
            {
                return;
            }

            var node = _engine.SelectedNode;
            if (node != null)
            {
                if (!ReferenceEquals(node, _announcedNode))
                {
                    _announcedNode = node;
                    peer.Announce(Describe(node));
                }

                _announcedLeaf = null;
                return;
            }

            _announcedNode = null;

            var leaf = _engine.Focus.Leaf;
            var line = DescribeLine(leaf);

            if (ReferenceEquals(leaf, _announcedLeaf) && line != _announcedLine)
            {
                peer.Announce(line);
            }

            _announcedLeaf = leaf;
            _announcedLine = line;
        }

        internal IEnumerable<RichLeafView> LeafViews => _leafViews.Values;

        // =====================================================================================
        // Placeholders
        // =====================================================================================

        internal string GetPlaceholder(RichParagraphBlock block)
        {
            var leaf = block.Text;
            if (!leaf.IsEmpty)
            {
                return null;
            }

            var document = _engine.Document;
            var focused = ReferenceEquals(leaf, _engine.Focus.Leaf) && FocusState != FocusState.Unfocused;

            if (block.Kind == RichBlockKind.Paragraph && !focused && !(document.IsEmpty && ReferenceEquals(document.Blocks[0], block)))
            {
                return null;
            }

            return block.Kind switch
            {
                RichBlockKind.Heading => Strings.ArticleHintTitle,
                RichBlockKind.Preformatted => Strings.ArticleHintCode,
                _ => Strings.ArticleHintText
            };
        }

        internal string GetCreditPlaceholder(RichTextLeaf leaf)
        {
            return leaf != null && leaf.IsEmpty ? Strings.ArticleHintAuthor : null;
        }

        internal string GetCaptionPlaceholder(RichTextLeaf leaf, string hint)
        {
            return leaf != null && leaf.IsEmpty ? hint : null;
        }

        /// <summary>
        /// The hint on an empty paragraph follows the caret, and so does an empty credit in a quote
        /// with no text: both are settled again whenever the caret changes leaf.
        /// </summary>
        private void UpdatePlaceholders()
        {
            var leaf = _engine.Focus.Leaf;
            if (ReferenceEquals(leaf, _placeholderLeaf))
            {
                return;
            }

            var previous = _placeholderLeaf;
            _placeholderLeaf = leaf;

            RefreshLeafChrome(previous);
            RefreshLeafChrome(leaf);
        }

        // A quote's credit shows once the quote has text, so text typed in a quote, at any depth,
        // can show or hide the credit of each quote around it.
        private void RefreshCredits(RichTextLeaf leaf)
        {
            for (var block = leaf.Owner; block != null; block = block.Parent)
            {
                if (block is RichTextQuote text && !ReferenceEquals(text.Credit, leaf))
                {
                    RefreshLeafChrome(text.Credit);
                }
                else if (block is RichBlockQuote quote && !ReferenceEquals(quote.Credit, leaf))
                {
                    RefreshLeafChrome(quote.Credit);
                }
            }
        }

        private void RefreshLeafChrome(RichTextLeaf leaf)
        {
            if (leaf == null || !_leafViews.TryGetValue(leaf, out RichLeafView view))
            {
                return;
            }

            view.Visibility = _engine.IsLeafHidden(leaf) ? Visibility.Collapsed : Visibility.Visible;

            if (leaf.Owner is RichParagraphBlock block && leaf.Role == RichLeafRole.Text)
            {
                view.SetPlaceholder(GetPlaceholder(block));
            }
        }

        // =====================================================================================
        // Selection, caret and the outline of a selected block
        // =====================================================================================

        private void UpdateSelection()
        {
            // Everything below reads the layout: what just changed has to be measured first.
            Surface?.UpdateLayout();

            UpdateDecorations();

            foreach (var view in _selectedViews)
            {
                view.Select(0, 0);
            }

            _selectedViews.Clear();

            foreach (var cell in _selectedCellViews)
            {
                cell.SetSelected(false);
            }

            _selectedCellViews.Clear();

            var node = _engine.SelectedNode;
            UpdateNodeOutline(node);

            // Across cells, the cells are what is selected: drawn whole, and their text not at all.
            _engine.GetSelectedCells(_selectedCells);

            foreach (var cell in _selectedCells)
            {
                if (_cellPresenters.TryGetValue(cell, out RichTableCellPresenter presenter))
                {
                    presenter.SetSelected(true);
                    _selectedCellViews.Add(presenter);
                }
            }

            if (node == null && !_engine.IsSelectionEmpty && _selectedCells.Count == 0)
            {
                var start = _engine.Start;
                var end = _engine.End;
                var leaves = _engine.Document.Leaves;

                for (int i = start.Leaf.Index; i <= end.Leaf.Index; i++)
                {
                    if (_leafViews.TryGetValue(leaves[i], out RichLeafView view))
                    {
                        var from = i == start.Leaf.Index ? start.Offset : 0;
                        var to = i == end.Leaf.Index ? end.Offset : leaves[i].Length;

                        view.Select(from, to);
                        _selectedViews.Add(view);
                    }
                }
            }

            UpdateCaret();
            UpdateGrippers();
        }

        /// <summary>
        /// The spoiler tint and the highlight, as rectangles under the lines they cover. Drawn
        /// again on every change, which costs nothing for the leaves without either - the runs
        /// are checked before the layout is asked anything.
        /// </summary>
        private void UpdateDecorations()
        {
            if (Decorations == null || SpoilerSwatch == null)
            {
                return;
            }

            var used = 0;

            foreach (var view in _leafViews.Values)
            {
                var leaf = view.Leaf;
                if (leaf == null || leaf.IsPlain || view.Visibility != Visibility.Visible || !HasDecoratedRuns(leaf))
                {
                    continue;
                }

                var transform = view.TransformToVisual(Surface);

                used = Decorate(view, transform, RichMarks.Marked, MarkedSwatch.Fill, used);
                used = Decorate(view, transform, RichMarks.Spoiler, SpoilerSwatch.Fill, used);
            }

            for (int i = used; i < _decorationShapes.Count; i++)
            {
                _decorationShapes[i].Visibility = Visibility.Collapsed;
            }
        }

        private static bool HasDecoratedRuns(RichTextLeaf leaf)
        {
            foreach (var run in leaf.Runs)
            {
                if ((run.Marks & (RichMarks.Spoiler | RichMarks.Marked)) != 0)
                {
                    return true;
                }
            }

            return false;
        }

        private int Decorate(RichLeafView view, GeneralTransform transform, RichMarks mark, Brush fill, int used)
        {
            view.GetMarkRanges(mark, _markRanges);

            foreach (var range in _markRanges)
            {
                foreach (var rect in view.GetRangeRects(range.Start, range.End))
                {
                    Rectangle shape;
                    if (used < _decorationShapes.Count)
                    {
                        shape = _decorationShapes[used];
                    }
                    else
                    {
                        shape = new Rectangle
                        {
                            RadiusX = 3,
                            RadiusY = 3
                        };

                        _decorationShapes.Add(shape);
                        Decorations.Children.Add(shape);
                    }

                    var origin = transform.TransformPoint(new Point(rect.X, rect.Y));

                    shape.Fill = fill;
                    shape.Width = rect.Width;
                    shape.Height = rect.Height;
                    Canvas.SetLeft(shape, origin.X);
                    Canvas.SetTop(shape, origin.Y);
                    shape.Visibility = Visibility.Visible;

                    used++;
                }
            }

            return used;
        }

        private void UpdateNodeOutline(RichBlock node)
        {
            if (NodeOutline == null)
            {
                return;
            }

            FrameworkElement element = null;

            if (node != null && _itemViews.TryGetValue(node, out FrameworkElement tile))
            {
                element = tile;
            }
            else if (node != null && _nodeViews.TryGetValue(node, out RichBlockPresenter presenter))
            {
                element = presenter.NodeElement;
            }

            if (element == null || !IsFocusedOrMenuOpen)
            {
                NodeOutline.Visibility = Visibility.Collapsed;
                return;
            }

            // The presenter may not have been measured yet; the layout pass after this one moves
            // the outline to it.
            if (element.ActualHeight <= 0)
            {
                _invalidOverlay = true;
            }

            var bounds = element.TransformToVisual(Surface).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));

            Canvas.SetLeft(NodeOutline, bounds.X - 4);
            Canvas.SetTop(NodeOutline, bounds.Y - 4);
            NodeOutline.Width = Math.Max(0, bounds.Width + 8);
            NodeOutline.Height = Math.Max(0, bounds.Height + 8);
            NodeOutline.Visibility = Visibility.Visible;
        }

        private bool IsFocusedOrMenuOpen => FocusState != FocusState.Unfocused || _menuOpen;

        private bool _menuOpen;
        private bool _invalidOverlay;

        /// <summary>The caret rectangle in root coordinates, or empty when there is no caret.</summary>
        internal Rect GetCaretRect()
        {
            var focus = _engine.Focus;
            if (_engine.SelectedNode != null || !_leafViews.TryGetValue(focus.Leaf, out RichLeafView view) || view.Visibility != Visibility.Visible)
            {
                return Rect.Empty;
            }

            var rect = view.GetCaretRect(focus.Offset);
            if (rect.IsEmpty)
            {
                return Rect.Empty;
            }

            var origin = view.TransformToVisual(Surface).TransformPoint(new Point(rect.X, rect.Y));
            return new Rect(origin.X, origin.Y, 0, rect.Height);
        }

        private void UpdateCaret()
        {
            if (Caret == null)
            {
                return;
            }

            var visible = FocusState != FocusState.Unfocused && _engine.SelectedNode == null && _engine.IsSelectionEmpty;
            if (!visible)
            {
                Caret.Visibility = Visibility.Collapsed;
                return;
            }

            // The text may have been laid out again by this very change; measuring it now is
            // what keeps the caret from trailing a keystroke behind.
            Surface.UpdateLayout();

            var rect = GetCaretRect();
            if (rect.IsEmpty)
            {
                Caret.Visibility = Visibility.Collapsed;
                return;
            }

            Canvas.SetLeft(Caret, Math.Round(rect.X));
            Canvas.SetTop(Caret, rect.Y);
            Caret.Height = rect.Height;
            Caret.Visibility = Visibility.Visible;

            RestartBlink();
            BringIntoView(rect);
        }

        /// <summary>
        /// Blinks the caret the way the system does, and starts the cycle again on every move:
        /// a caret that vanishes the instant it moved is the one thing that reads as lag.
        /// </summary>
        private void RestartBlink()
        {
            var visual = ElementCompositionPreview.GetElementVisual(Caret);

            _settings ??= new Windows.UI.ViewManagement.UISettings();

            var interval = _settings.CaretBlinkRate;
            if (interval <= 0 || interval == uint.MaxValue)
            {
                visual.StopAnimation("Opacity");
                visual.Opacity = 1;
                return;
            }

            if (_blink == null)
            {
                var compositor = visual.Compositor;
                var hold = compositor.CreateStepEasingFunction();
                hold.IsFinalStepSingleFrame = true;

                _blink = compositor.CreateScalarKeyFrameAnimation();
                _blink.InsertKeyFrame(0, 1, hold);
                _blink.InsertKeyFrame(0.5f, 0, hold);
                _blink.InsertKeyFrame(1, 1, hold);
                _blink.IterationBehavior = AnimationIterationBehavior.Forever;
                _blink.Duration = TimeSpan.FromMilliseconds(interval * 2);
            }

            visual.StopAnimation("Opacity");
            visual.Opacity = 1;
            visual.StartAnimation("Opacity", _blink);
        }

        private void BringIntoView(Rect rect)
        {
            if (ScrollViewer == null || _isDragging)
            {
                return;
            }

            // The surface sits in the scrolled content below the top padding.
            var top = rect.Y + Padding.Top - 8;
            var bottom = rect.Y + Padding.Top + rect.Height + 8;
            var offset = ScrollViewer.VerticalOffset;
            var viewport = ScrollViewer.ViewportHeight;

            if (top < offset)
            {
                ScrollViewer.ChangeView(null, top, null, true);
            }
            else if (bottom > offset + viewport && viewport > 0)
            {
                ScrollViewer.ChangeView(null, bottom - viewport, null, true);
            }
        }

        private void OnViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            // The text input service keeps its candidate window beside the caret.
            NotifyLayoutChanged();
        }

        private double _arrangedWidth;

        protected override Size ArrangeOverride(Size finalSize)
        {
            var size = base.ArrangeOverride(finalSize);

            // A new width wraps the text again, and everything drawn over it moves.
            if (finalSize.Width != _arrangedWidth)
            {
                _arrangedWidth = finalSize.Width;
                _invalidOverlay = true;
            }

            if (_invalidOverlay)
            {
                _invalidOverlay = false;
                _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Low, UpdateSelection);
            }

            return size;
        }

        // =====================================================================================
        // Requests the host handles: pickers and popups belong to the window, not to the editor.
        // =====================================================================================

        internal void RequestLanguage(RichParagraphBlock block, FrameworkElement anchor)
        {
            LanguageRequested?.Invoke(this, new RichLanguageRequestedEventArgs(block, anchor));
        }

        internal void RequestEditButton(RichButtonRowBlock row, int index)
        {
            _engine.SelectNode(row);
            ButtonRequested?.Invoke(this, new RichButtonRequestedEventArgs(row, index));
        }

        /// <summary>Puts the caret in <paramref name="leaf"/>, making a hidden credit visible: "Add credit".</summary>
        public void FocusLeaf(RichTextLeaf leaf)
        {
            _engine.SetCaret(new RichPosition(leaf, leaf.Length));
            Focus(FocusState.Programmatic);
        }
    }

    public sealed class RichLanguageRequestedEventArgs : EventArgs
    {
        public RichLanguageRequestedEventArgs(RichParagraphBlock block, FrameworkElement anchor)
        {
            Block = block;
            Anchor = anchor;
        }

        public RichParagraphBlock Block { get; }

        public FrameworkElement Anchor { get; }
    }

    public sealed class RichButtonRequestedEventArgs : EventArgs
    {
        public RichButtonRequestedEventArgs(RichButtonRowBlock row, int index)
        {
            Row = row;
            Index = index;
        }

        public RichButtonRowBlock Row { get; }

        /// <summary>The button opened, or -1 for a new one.</summary>
        public int Index { get; }
    }

    public sealed class RichObjectRequestedEventArgs : EventArgs
    {
        public RichObjectRequestedEventArgs(RichTextLeaf leaf, int offset, RichInlineObject inline)
        {
            Leaf = leaf;
            Offset = offset;
            Object = inline;
        }

        public RichTextLeaf Leaf { get; }

        /// <summary>Where the object is in the leaf; -1 when only the object is known.</summary>
        public int Offset { get; }

        public RichInlineObject Object { get; }
    }

    /// <summary>A touch selection handle.</summary>
    /// <summary>
    /// The editor's column: as wide as it is allowed to be, up to its MaxWidth, rather than as
    /// wide as its text. Asked for in measure, so nothing is set during layout: a stretched Grid
    /// with a MaxWidth was arranged at its content's width once the view was wider than that,
    /// and short text sat in a narrow column in the middle until it wrapped.
    /// </summary>
    public sealed partial class RichEditorColumn : Grid
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            // The available width is already clamped to MaxWidth.
            var size = base.MeasureOverride(availableSize);
            return double.IsInfinity(availableSize.Width) ? size : new Size(availableSize.Width, size.Height);
        }
    }

    public sealed partial class RichSelectionGripper : Control
    {
        public RichSelectionGripper()
        {
            DefaultStyleKey = typeof(RichSelectionGripper);
        }
    }
}
