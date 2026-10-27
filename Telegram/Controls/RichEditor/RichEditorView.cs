//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Telegram.Common;
using Telegram.Native;
using Telegram.Navigation;
using Telegram.Services;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
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
        private RichBlocksPanel Blocks;
        private Canvas Decorations;
        private Rectangle SpoilerSwatch;
        private Rectangle MarkedSwatch;

        private readonly List<XamlPath> _decorationShapes = new();
        private readonly List<(int Start, int End)> _markRanges = new();
        private Border NodeOutline;
        private Rectangle Caret;
        private Polygon CaretFlag;
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
        private readonly List<RichTableBlock> _coveredTables = new();

        private RichTextLeaf _placeholderLeaf;

        private ScalarKeyFrameAnimation _blink;
        private bool _blinking;

        // Whether the editor has focus, as GotFocus and LostFocus say. Not FocusState: a window
        // that is deactivated raises LostFocus but leaves FocusState as it was, and the caret
        // would go on blinking in the background.
        private bool _hasFocus;
        private Windows.UI.ViewManagement.UISettings _settings;

        public RichEditorView()
        {
            DefaultStyleKey = typeof(RichEditorView);

            Media = new RichMediaRenderer(this);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public RichEditorEngine Engine => _engine;

        /// <summary>
        /// Media uploading from the moment it is added, for an article. Null, as for a message's
        /// text, uploads it with the message instead.
        /// </summary>
        public RichMediaUploads Uploads { get; set; }

        // Media whose upload moved on: what shows it shows how far.
        internal void OnUploadChanged(RichMediaSource source)
        {
            foreach (var presenter in _presenters.Values)
            {
                if (presenter is RichMediaPresenter media && media.Shows(source))
                {
                    media.UpdateUpload();
                }
            }
        }

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

        #region PlaceholderText

        /// <summary>
        /// The hint on an empty document, in place of the per-line hints. Set for a message's text,
        /// which has one line to type on rather than an article's kinds of line.
        /// </summary>
        public string PlaceholderText
        {
            get => (string)GetValue(PlaceholderTextProperty);
            set => SetValue(PlaceholderTextProperty, value);
        }

        public static readonly DependencyProperty PlaceholderTextProperty =
            DependencyProperty.Register("PlaceholderText", typeof(string), typeof(RichEditorView), new PropertyMetadata(null, OnPlaceholderTextChanged));

        private static void OnPlaceholderTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = (RichEditorView)d;
            if (sender._engine != null && sender._engine.Document.IsEmpty)
            {
                sender.Refresh(RichEditorChanges.Structure, null);
            }
        }

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

            _spelling.Clear();

            Refresh(RichEditorChanges.Structure | RichEditorChanges.Selection, null);
            UpdateSpelling(RichEditorChanges.Structure, null);

            // Another chat's text, which the input service has to be told of as any other.
            SyncEditContext(RichEditorChanges.Structure);
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
                ScrollViewer.Tapped -= OnScrollViewerTapped;
            }

            if (Blocks != null)
            {
                Blocks.EffectiveViewportChanged -= OnBlocksViewportChanged;
            }

            // On the caret it runs on, which the template is about to replace.
            StopBlink();

            ScrollViewer = GetTemplateChild(nameof(ScrollViewer)) as ScrollViewer;
            Root = GetTemplateChild(nameof(Root)) as RichEditorColumn;
            Surface = GetTemplateChild(nameof(Surface)) as Grid;
            Blocks = GetTemplateChild(nameof(Blocks)) as RichBlocksPanel;
            Decorations = GetTemplateChild(nameof(Decorations)) as Canvas;
            SpoilerSwatch = GetTemplateChild(nameof(SpoilerSwatch)) as Rectangle;
            MarkedSwatch = GetTemplateChild(nameof(MarkedSwatch)) as Rectangle;
            SpellingSwatch = GetTemplateChild(nameof(SpellingSwatch)) as Rectangle;
            _decorationShapes.Clear();
            _compositionLines.Clear();
            _compositionBackgrounds.Clear();
            _spellingLines.Clear();
            _spellingWidths.Clear();
            NodeOutline = GetTemplateChild(nameof(NodeOutline)) as Border;
            Caret = GetTemplateChild(nameof(Caret)) as Rectangle;
            CaretFlag = GetTemplateChild(nameof(CaretFlag)) as Polygon;
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
                ScrollViewer.Tapped += OnScrollViewerTapped;
            }

            if (Blocks != null)
            {
                Blocks.Owner = this;
                Blocks.EffectiveViewportChanged += OnBlocksViewportChanged;
            }

            _viewport = Rect.Empty;
            _pool.Clear();
            _pooled = 0;
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
            HideSuggestions();
            StopBlink();

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
            var started = RichEditorMeter.StartView();

            Refresh(e.Changes, e.Leaf);
            SyncEditContext(e.Changes);
            UpdateSpelling(e.Changes, e.Leaf);
            UpdateSuggestions(e.Changes);

            (Peer as RichEditorAutomationPeer)?.RaiseChanged(e.Changes);
            Announce();

            // What the host does with the state is the host's cost, not the view's.
            RichEditorMeter.StopView(started);

            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>The leaves and the root blocks that have their views, for the meter.</summary>
        internal int RealizedLeafCount => _leafViews.Count;

        internal int RealizedBlockCount => Blocks?.Children.Count ?? 0;

        internal ScrollViewer ScrollerForMeasure => ScrollViewer;

        /// <summary>The text blocks of the leaves that have their views, in document order.</summary>
        internal void GetRealizedTextBlocks(List<DirectTextBlock> blocks)
        {
            foreach (var leaf in _engine.Document.Leaves)
            {
                if (_leafViews.TryGetValue(leaf, out RichLeafView view) && view.TextBlock != null)
                {
                    blocks.Add(view.TextBlock);
                }
            }
        }

        private RichEditorAutomationPeer Peer => FrameworkElementAutomationPeer.FromElement(this) as RichEditorAutomationPeer;

        private void Refresh(RichEditorChanges changes, RichTextLeaf leaf)
        {
            if (Blocks == null || _engine == null)
            {
                return;
            }

            var flipped = UpdateDocumentDirection(changes, leaf);

            if ((changes & RichEditorChanges.Structure) != 0)
            {
                _estimateFontSize = FontSize;
                Blocks.SetRoots(_engine.Document.Blocks);

                Realize();
                _caretStale = true;

                // Media taken out stops uploading; what came in started as it was shown.
                Uploads?.Retain(_engine.Document);
            }
            else if ((changes & RichEditorChanges.Text) != 0 && leaf != null && _leafViews.TryGetValue(leaf, out RichLeafView view))
            {
                view.Render();
                RefreshCredits(leaf);

                // Text typed into an empty last line counts it without the quote growing, so
                // its height changing is not enough to go by; asked once the text is measured.
                if (leaf.Owner?.Parent is RichBlockQuote quote && _presenters.TryGetValue(quote, out RichBlockPresenter presenter))
                {
                    _toggleStale = presenter as RichBlockQuotePresenter;
                }
            }

            // The selection moved somewhere the window does not reach: Ctrl+End, Select All.
            if ((changes & (RichEditorChanges.Structure | RichEditorChanges.Selection)) == RichEditorChanges.Selection && IsVirtualizing && !HoldsPinned())
            {
                RealizeWindow();
            }

            // Every line without a strong character of its own reads the new way.
            if (flipped)
            {
                foreach (var view in _leafViews.Values)
                {
                    view.Render();
                }
            }

            UpdatePlaceholders();
            UpdateDocumentPlaceholder();
            UpdateSelection();
        }

        /// <summary>The roots in the window again, reusing every presenter whose object survived and is still in it.</summary>
        private void Realize()
        {
            var started = Stopwatch.GetTimestamp();

            _bounds.Clear();
            _realized.Clear();
            _leafViews.Clear();
            _nodeViews.Clear();
            _itemViews.Clear();

            var blocks = _engine.Document.Blocks;
            var virtualizing = IsVirtualizing;

            ChooseWindow(blocks, virtualizing);

            // Roots leaving the window go back to the pool before those coming in take from it;
            // what else is gone leaves with the prune. A root nested now is left for its new
            // parent, which takes its presenter as it is.
            var children = Blocks.Children;
            for (int i = children.Count - 1; i >= 0 && virtualizing; i--)
            {
                if (children[i] is RichBlockPresenter root
                    && root.Block is RichBlock block
                    && block.Parent == null
                    && !_windowSet.Contains(block)
                    && _presenters.TryGetValue(block, out RichBlockPresenter cached)
                    && ReferenceEquals(cached, root))
                {
                    UnrealizeTree(block);
                }
            }

            Reconcile(Blocks, _windowBlocks);

            foreach (var index in _window)
            {
                var presenter = GetPresenter(blocks[index]);
                presenter.RootIndex = index;

                // What is realized holds the view still while what is above it is measured for
                // the first time, its estimate replaced.
                presenter.CanBeScrollAnchor = virtualizing;

                Realize(blocks, index, true);
            }

            Prune(_presenters);
            Prune(_itemPresenters);
            Prune(_cellPresenters);

            RealizeCount++;
            WholeRealizeCount++;
            RealizeTicks += Stopwatch.GetTimestamp() - started;
        }

        /// <summary>
        /// The window moved and the document did not: what left it and what came in, where
        /// <see cref="Realize"/> binds every root in it again, some 30 µs each.
        /// </summary>
        private void RealizeWindow()
        {
            if (!IsVirtualizing)
            {
                Realize();
                return;
            }

            var started = Stopwatch.GetTimestamp();
            var blocks = _engine.Document.Blocks;

            // The window before this one is left in the spare set.
            ChooseWindow(blocks, true);

            _bounds.Clear();

            foreach (var block in _windowSpare)
            {
                if (!_windowSet.Contains(block))
                {
                    Unrealize(block);
                }
            }

            foreach (var index in _window)
            {
                var block = blocks[index];
                if (_windowSpare.Contains(block))
                {
                    continue;
                }

                var presenter = GetPresenter(block);
                presenter.RootIndex = index;
                presenter.CanBeScrollAnchor = true;

                Attach(Blocks, presenter, ChildIndexBefore(index));
                Realize(blocks, index, true);
            }

            RealizeCount++;
            RealizeTicks += Stopwatch.GetTimestamp() - started;
        }

        // Where a root goes among the realized ones, which are in document order: before the
        // first that comes after it.
        private int ChildIndexBefore(int index)
        {
            var children = Blocks.Children;
            int low = 0, high = children.Count;

            while (low < high)
            {
                var middle = (low + high) / 2;
                if (((RichBlockPresenter)children[middle]).RootIndex < index)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            return low;
        }

        // For the meter: the passes, how many were whole, what they took, and the presenters of
        // pooled kinds they made or reused.
        internal int RealizeCount { get; private set; }

        internal int WholeRealizeCount { get; private set; }

        internal long RealizeTicks { get; private set; }

        internal int PresentersMade { get; private set; }

        internal int PresentersReused { get; private set; }

        internal int ViewportCount { get; private set; }

        internal long ViewportTicks { get; private set; }

        internal int OverlayCount { get; private set; }

        internal long OverlayTicks { get; private set; }

        internal long PanelMeasureTicks => Blocks?.MeasureTicks ?? 0;

        internal long PanelArrangeTicks => Blocks?.ArrangeTicks ?? 0;

        // =====================================================================================
        // The window: the roots realized in a document long enough to have one
        // =====================================================================================

        // Up to this many roots every one is realized, as in nearly every message and article.
        private const int VirtualizeAbove = 100;

        private bool IsVirtualizing => _engine != null && _engine.Document.Blocks.Count > VirtualizeAbove;

        // The panel's viewport, in its own coordinates, and the stretch of it the window covers.
        private Rect _viewport = Rect.Empty;
        private double _windowTop;
        private double _windowBottom;

        // The roots realized, by index, ascending: a range around the viewport and the pinned
        // roots outside it.
        private readonly List<int> _window = new();
        private readonly List<RichBlock> _windowBlocks = new();
        private HashSet<RichBlock> _windowSet = new();
        private HashSet<RichBlock> _windowSpare = new();

        // Roots realized because something asked about them, until the window next moves.
        private readonly List<RichBlock> _ensured = new();

        private void ChooseWindow(List<RichBlock> blocks, bool virtualizing)
        {
            // What was realized moves to the spare set and stays there after: kept below if still
            // near the viewport, and what RealizeWindow tells what left and what came in by.
            var realized = _windowSet;
            _windowSet = _windowSpare;
            _windowSpare = realized;

            _window.Clear();
            _windowBlocks.Clear();
            _windowSet.Clear();

            if (!virtualizing)
            {
                for (int i = 0; i < blocks.Count; i++)
                {
                    _window.Add(i);
                }

                _windowTop = double.NegativeInfinity;
                _windowBottom = double.PositiveInfinity;
            }
            else
            {
                var focus = RootIndexOf(_engine.Focus.Leaf?.Owner);
                double top, bottom;

                // A screen either side of the viewport; before there is one, of the caret.
                if (_viewport.Height > 0)
                {
                    top = _viewport.Top - _viewport.Height * _windowMargin;
                    bottom = _viewport.Bottom + _viewport.Height * _windowMargin;
                }
                else
                {
                    var screen = ScrollViewer != null && ScrollViewer.ViewportHeight > 0 ? ScrollViewer.ViewportHeight : 800;
                    var at = Blocks.OffsetOf(Math.Max(focus, 0));

                    top = at - screen;
                    bottom = at + screen * 2;
                }

                _windowTop = top;
                _windowBottom = bottom;

                var last = Blocks.IndexAt(bottom);

                var first = Blocks.IndexAt(top);

                for (int i = first; i <= last; i++)
                {
                    _window.Add(i);
                }

                // A narrow margin while the view moves is for what comes in: what is already
                // realized inside the wide one stays, or narrowing and widening again would bind
                // it to another presenter and lay its text out again, twice per move.
                if (_windowMargin < RestingMargin && _viewport.Height > 0)
                {
                    var keepFirst = Blocks.IndexAt(_viewport.Top - _viewport.Height * RestingMargin);
                    var keepLast = Blocks.IndexAt(_viewport.Bottom + _viewport.Height * RestingMargin);

                    for (int i = keepFirst; i <= keepLast; i++)
                    {
                        if ((i < first || i > last) && realized.Contains(blocks[i]))
                        {
                            Pin(i, blocks.Count);
                        }
                    }
                }

                // The caret's root and those either side of it, which a line up or down moves
                // into; both ends of the selection; the block selected whole.
                Pin(focus - 1, blocks.Count);
                Pin(focus, blocks.Count);
                Pin(focus + 1, blocks.Count);
                Pin(RootIndexOf(_engine.Start.Leaf?.Owner), blocks.Count);
                Pin(RootIndexOf(_engine.End.Leaf?.Owner), blocks.Count);
                Pin(RootIndexOf(_engine.SelectedNode), blocks.Count);

                foreach (var block in _ensured)
                {
                    Pin(RootIndexOf(block), blocks.Count);
                }
            }

            foreach (var index in _window)
            {
                _windowBlocks.Add(blocks[index]);
                _windowSet.Add(blocks[index]);
            }
        }

        private void Pin(int index, int count)
        {
            if (index < 0 || index >= count)
            {
                return;
            }

            var at = _window.BinarySearch(index);
            if (at < 0)
            {
                _window.Insert(~at, index);
            }
        }

        private bool HoldsPinned()
        {
            var focus = RootIndexOf(_engine.Focus.Leaf?.Owner);

            return Holds(focus - 1)
                && Holds(focus)
                && Holds(focus + 1)
                && Holds(RootIndexOf(_engine.Start.Leaf?.Owner))
                && Holds(RootIndexOf(_engine.End.Leaf?.Owner))
                && Holds(RootIndexOf(_engine.SelectedNode));
        }

        private bool Holds(int index)
        {
            var blocks = _engine.Document.Blocks;
            return index < 0 || index >= blocks.Count || _windowSet.Contains(blocks[index]);
        }

        /// <summary>The index of the root a block is in, or -1 for one that is not in the document.</summary>
        private int RootIndexOf(RichBlock block)
        {
            if (block == null)
            {
                return -1;
            }

            while (block.Parent != null)
            {
                block = block.Parent;
            }

            return _engine.Document.Blocks.IndexOf(block);
        }

        /// <summary>
        /// Realizes the root a leaf is in, should the window not reach it: for what asks about a
        /// leaf the editor did not put the caret in, as a screen reader scrolling to a range does.
        /// </summary>
        internal void EnsureRealized(RichTextLeaf leaf)
        {
            if (leaf == null || _engine == null || _leafViews.ContainsKey(leaf) || !IsVirtualizing)
            {
                return;
            }

            var index = RootIndexOf(leaf.Owner);
            if (index < 0 || Holds(index))
            {
                return;
            }

            _ensured.Add(_engine.Document.Blocks[index]);

            RealizeWindow();
            UpdateSelection(false);
        }

        /// <summary>Realizes the root at a height of the surface, should the window not reach it: for a move that lands where nothing may be laid out yet.</summary>
        internal void EnsureRealizedAt(double y)
        {
            if (Blocks == null || !IsVirtualizing)
            {
                return;
            }

            var index = Blocks.IndexAt(y);
            if (index < 0 || Holds(index))
            {
                return;
            }

            _ensured.Add(_engine.Document.Blocks[index]);

            RealizeWindow();
            Surface.UpdateLayout();
        }

        /// <summary>
        /// What of the editor is on screen, in its own coordinates, and empty when none of it is;
        /// false until a layout pass has said.
        /// </summary>
        internal bool TryGetVisibleBounds(out Rect bounds)
        {
            bounds = Rect.Empty;

            if (Blocks == null || _viewport.IsEmpty)
            {
                return false;
            }

            var viewport = Blocks.TransformToVisual(this).TransformBounds(_viewport);

            var left = Math.Max(viewport.Left, 0);
            var top = Math.Max(viewport.Top, 0);
            var right = Math.Min(viewport.Right, ActualWidth);
            var bottom = Math.Min(viewport.Bottom, ActualHeight);

            if (right > left && bottom > top)
            {
                bounds = new Rect(left, top, right - left, bottom - top);
            }

            return true;
        }

        // Raised after a layout pass, outside it, which is where the window can move: what it
        // realizes is measured by the pass this raises again.
        private void OnBlocksViewportChanged(FrameworkElement sender, EffectiveViewportChangedEventArgs args)
        {
            var started = Stopwatch.GetTimestamp();
            ViewportCount++;

            FollowViewport(args.EffectiveViewport);

            ViewportTicks += Stopwatch.GetTimestamp() - started;
        }

        private void FollowViewport(Rect viewport)
        {
            _viewport = viewport;

            if (!IsVirtualizing || !(_viewport.Height > 0))
            {
                return;
            }

            // Half of the margin left on either side: scrolling less than that realizes nothing.
            var slack = _viewport.Height * _windowMargin / 2;
            if (_viewport.Top - slack >= _windowTop && _viewport.Bottom + slack <= _windowBottom)
            {
                return;
            }

            // While the view moves, a narrow margin, and none at all where it landed clear of the
            // window: a dragged scrollbar does that every frame, and a margin is realized for a
            // frame that never shows it. Widened once idle.
            var jumped = _viewport.Bottom < _windowTop || _viewport.Top > _windowBottom;
            _windowMargin = jumped ? 0 : MovingMargin;
            _ensured.Clear();

            MoveWindow();

            if (!_widenPending)
            {
                _widenPending = true;
#if WINUI3
                DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, OnWidenWindow);
#else
                _ = Dispatcher.RunIdleAsync(OnWidenWindow);
#endif
            }
        }

#if WINUI3
        // A DispatcherQueue has no idle priority; Low is the nearest.
        private void OnWidenWindow()
#else
        private void OnWidenWindow(IdleDispatchedHandlerArgs e)
#endif
        {
            _widenPending = false;

            if (_windowMargin < RestingMargin && IsVirtualizing)
            {
                _windowMargin = RestingMargin;
                MoveWindow();
            }
        }

        private void MoveWindow()
        {
            RealizeWindow();

            // The selection, the decorations and the squiggles of what came in, once it is laid
            // out; without scrolling to the caret, which may well be what was scrolled away from.
            _staleOverlay = true;
            InvalidateArrange();
        }

        // Screens realized above and below the viewport.
        private const double MovingMargin = 0.5;
        private const double RestingMargin = 1;

        private double _windowMargin = RestingMargin;
        private bool _widenPending;

        private double _estimateFontSize = 15;

        /// <summary>
        /// How tall a root not yet measured at <paramref name="width"/> is likely to be: its
        /// leaves by their length, wrapped at an average character width, and a guess for each
        /// block selected whole; with the spacing it would be given.
        /// </summary>
        internal double EstimateHeight(IList<RichBlock> roots, int index, double width)
        {
            // Every root is realized and measured before it is drawn.
            if (!IsVirtualizing)
            {
                return 0;
            }

            var block = roots[index];
            var margin = Spacing(roots, index, true);
            var inner = Math.Max(1, width - margin.Left - margin.Right);

            var stops = _engine.Document.Stops;
            var height = 0d;

            for (int i = block.FirstStop; i <= block.LastStop && i < stops.Count; i++)
            {
                var stop = stops[i];
                height += stop.IsLeaf ? EstimateText(stop.Leaf, inner) : EstimateAtom(stop.Block, inner);
            }

            return Math.Max(height, _estimateFontSize * LineSpacing) + margin.Top + margin.Bottom;
        }

        // Segoe UI's line height and average character width, as fractions of its size.
        private const double LineSpacing = 1.33;
        private const double CharacterWidth = 0.5;

        private double EstimateText(RichTextLeaf leaf, double width)
        {
            var size = leaf.Owner switch
            {
                RichParagraphBlock { Kind: RichBlockKind.Heading } heading => 26 - 2 * Math.Clamp(heading.HeadingSize, 1, 6),
                RichParagraphBlock { Kind: RichBlockKind.Footer } => 12,
                _ => _estimateFontSize
            };

            var perLine = Math.Max(1, (int)(width / (size * CharacterWidth)));
            var text = leaf.Chars;
            var lines = 0;
            var start = 0;

            for (int i = 0; i <= text.Length; i++)
            {
                if (i == text.Length || text[i] == '\n')
                {
                    lines += Math.Max(1, (i - start + perLine - 1) / perLine);
                    start = i + 1;
                }
            }

            return lines * size * LineSpacing;
        }

        private static double EstimateAtom(RichBlock block, double width)
        {
            return block switch
            {
                RichMediaBlock or RichMediaGroupBlock or RichMapBlock => Math.Min(width * 0.6, 400),
                RichDividerBlock => 1,
                RichAnchorBlock => 0,
                _ => 40
            };
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
                    var presenter = presenters[key];

                    Detach(presenter);
                    presenters.Remove(key);

                    // Items and cells come back to the pool when their root leaves the window;
                    // what is pruned is what an edit took away.
                    if (presenter is RichBlockPresenter block)
                    {
                        Release(block);
                    }
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

        private Thickness Spacing(IList<RichBlock> blocks, int index, bool root)
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

            // A message's text has no bubble around it to keep clear of: its box's padding is all
            // the inset there is.
            var message = _engine?.Schema != null;

            var bottom = kind == RichBlockKind.Divider ? 4 : root && !message && index == blocks.Count - 1 && !IsFullMedia(block) ? 6 : 0;
            var padding = root && !message && !IsEdgeToEdge(block) ? 10 : 0;

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

        // Presenters scrolled out of the window, template, text blocks and surfaces kept, for the
        // ones scrolling in: building them anew is most of what a window move costs. Every kind
        // but media, whose players and uploads are their own, and atoms, which are cheap. Taken
        // down empty: what was nested in one is pooled on its own.
        // By kind and look: a kind is a visual state, and a presenter brought back into the tree
        // for another kind was seen drawing its text in the old one before it took the new.
        private readonly Dictionary<int, Stack<FrameworkElement>> _pool = new();
        private int _pooled;
        private const int MaxPooled = 192;

        private const int ItemPoolKeys = 256;
        private const int CellPoolKeys = 512;

        private static int PoolKey(RichBlock block)
        {
            return block switch
            {
                RichParagraphBlock { Kind: RichBlockKind.Heading } heading => (int)RichBlockKind.Heading * 8 + Math.Clamp(heading.HeadingSize, 1, 6),
                RichBlockQuote { IsCollapsed: true } => (int)RichBlockKind.BlockQuote * 8 + 1,
                RichDetailsBlock { IsOpen: true } => (int)RichBlockKind.Details * 8 + 1,
                _ => (int)block.Kind * 8
            };
        }

        private static bool IsPooled(RichBlock block)
        {
            return block is RichParagraphBlock or RichBlockQuote or RichTextQuote or RichListBlock or RichDetailsBlock or RichTableBlock;
        }

        private void Release(int key, FrameworkElement presenter)
        {
            if (_pooled >= MaxPooled)
            {
                return;
            }

            if (!_pool.TryGetValue(key, out Stack<FrameworkElement> pool))
            {
                _pool[key] = pool = new Stack<FrameworkElement>();
            }

            pool.Push(presenter);
            _pooled++;
        }

        private void Release(RichBlockPresenter presenter)
        {
            if (presenter.Block != null && IsPooled(presenter.Block))
            {
                Release(PoolKey(presenter.Block), presenter);
            }
        }

        private FrameworkElement Take(int key)
        {
            if (_pool.TryGetValue(key, out Stack<FrameworkElement> pool) && pool.Count > 0)
            {
                _pooled--;
                PresentersReused++;
                return pool.Pop();
            }

            PresentersMade++;
            return null;
        }

        internal RichBlockPresenter GetPresenter(RichBlock block)
        {
            if (_presenters.TryGetValue(block, out RichBlockPresenter presenter))
            {
                return presenter;
            }

            if (IsPooled(block))
            {
                presenter = Take(PoolKey(block)) as RichBlockPresenter;
            }

            if (presenter == null)
            {
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
            }

            _presenters[block] = presenter;
            return presenter;
        }

        internal RichListItemPresenter GetItemPresenter(RichListItem item, RichListBlock list)
        {
            _realized.Add(item);

            if (!_itemPresenters.TryGetValue(item, out RichListItemPresenter presenter))
            {
                presenter = Take(ItemPoolKeys + (int)list.Style) as RichListItemPresenter ?? new RichListItemPresenter();
                _itemPresenters[item] = presenter;
            }

            return presenter;
        }

        internal RichTableCellPresenter GetCellPresenter(RichTableCell cell)
        {
            _realized.Add(cell);

            if (!_cellPresenters.TryGetValue(cell, out RichTableCellPresenter presenter))
            {
                presenter = Take(CellPoolKeys + (cell.IsHeader ? 1 : 0)) as RichTableCellPresenter ?? new RichTableCellPresenter();
                _cellPresenters[cell] = presenter;
            }

            return presenter;
        }

        /// <summary>
        /// Takes a root out of the window by itself, where <see cref="Realize"/> would bind every
        /// root in it again: its presenter, everything nested in it, and what they registered.
        /// The document has not changed since it was realized, so what its stops and children
        /// are now is what it was realized with.
        /// </summary>
        private void Unrealize(RichBlock root)
        {
            var stops = _engine.Document.Stops;

            for (int i = root.FirstStop; i <= root.LastStop && i < stops.Count; i++)
            {
                if (stops[i].IsLeaf)
                {
                    _leafViews.Remove(stops[i].Leaf);
                }
            }

            UnrealizeTree(root);
        }

        // Out of the tree before what is in it, which then leaves a panel no longer live.
        private void UnrealizeTree(RichBlock block)
        {
            if (_presenters.TryGetValue(block, out RichBlockPresenter presenter))
            {
                Detach(presenter);
                _presenters.Remove(block);
            }

            switch (block)
            {
                case RichBlockQuote quote:
                    UnrealizeTree(quote.Children);
                    break;
                case RichDetailsBlock details:
                    UnrealizeTree(details.Children);
                    break;
                case RichListBlock list:
                    foreach (var item in list.Items)
                    {
                        if (_itemPresenters.TryGetValue(item, out RichListItemPresenter itemPresenter))
                        {
                            Detach(itemPresenter);
                            _itemPresenters.Remove(item);
                            Release(ItemPoolKeys + (int)list.Style, itemPresenter);
                        }

                        _realized.Remove(item);
                        UnrealizeTree(item.Children);
                    }
                    break;
                case RichTableBlock table:
                    foreach (var row in table.Rows)
                    {
                        foreach (var cell in row)
                        {
                            if (_cellPresenters.TryGetValue(cell, out RichTableCellPresenter cellPresenter))
                            {
                                Detach(cellPresenter);
                                _cellPresenters.Remove(cell);

                                cellPresenter.SetSelected(false);
                                Release(CellPoolKeys + (cell.IsHeader ? 1 : 0), cellPresenter);
                            }

                            _realized.Remove(cell);
                        }
                    }
                    break;
                case RichMediaGroupBlock group:
                    UnregisterItems(group);
                    break;
            }

            _nodeViews.Remove(block);
            _realized.Remove(block);

            if (presenter != null)
            {
                Release(presenter);
            }
        }

        private void UnrealizeTree(List<RichBlock> blocks)
        {
            foreach (var block in blocks)
            {
                UnrealizeTree(block);
            }
        }

        /// <summary>
        /// Makes <paramref name="panel"/> hold the presenters of <paramref name="blocks"/>, in
        /// order, moving only those out of place: a presenter taken out leaves the live tree, is
        /// unloaded and loaded with everything in it and measured again, so clearing the panel to
        /// add them back would do that to the whole document for one Enter.
        /// </summary>
        internal void Reconcile(Panel panel, IList<RichBlock> blocks)
        {
            foreach (var block in blocks)
            {
                _wanted.Add(GetPresenter(block));
            }

            Reconcile(panel);
        }

        internal void Reconcile(Panel panel, RichListBlock list)
        {
            foreach (var item in list.Items)
            {
                _wanted.Add(GetItemPresenter(item, list));
            }

            Reconcile(panel);
        }

        private void Reconcile(Panel panel)
        {
            var children = panel.Children;
            var first = 0;

            while (first < _wanted.Count && first < children.Count && ReferenceEquals(children[first], _wanted[first]))
            {
                first++;
            }

            if (first < _wanted.Count || first < children.Count)
            {
                // What left goes first, so the walk below only meets insertions and moves.
                for (int i = first; i < _wanted.Count; i++)
                {
                    _wantedSet.Add(_wanted[i]);
                }

                for (int i = children.Count - 1; i >= first; i--)
                {
                    if (children[i] is FrameworkElement element && !_wantedSet.Contains(element))
                    {
                        _hosts.Remove(element);
                        children.RemoveAt(i);
                    }
                }

                _wantedSet.Clear();

                for (int i = first; i < _wanted.Count; i++)
                {
                    if (i >= children.Count || !ReferenceEquals(children[i], _wanted[i]))
                    {
                        Attach(panel, _wanted[i], i);
                    }
                }
            }

            _wanted.Clear();
        }

        // Filled and emptied by one Reconcile, which realizes nothing while it runs.
        private readonly List<FrameworkElement> _wanted = new();
        private readonly HashSet<FrameworkElement> _wantedSet = new();

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

            // A collapsed quote's text is drawn and is not among the leaves: nothing may put the
            // caret in it, and hit testing finds the leaves it is told of here.
            var leaves = _engine.Document.Leaves;
            if (leaf.Index < leaves.Count && ReferenceEquals(leaves[leaf.Index], leaf))
            {
                _leafViews[leaf] = view;
            }
        }

        private void HookLeaf(RichLeafView view)
        {
            view.ObjectClick += OnLeafObjectClick;
            view.Resized += OnLeafResized;
        }

        private void UnhookLeaf(RichLeafView view)
        {
            view.ObjectClick -= OnLeafObjectClick;
            view.Resized -= OnLeafResized;
        }

        // Measured again at a new size, in this pass: the caret is put back on its line once the
        // pass is over.
        private void OnLeafResized(object sender, EventArgs e)
        {
            if (_engine != null && ReferenceEquals(((RichLeafView)sender).Leaf, _engine.Focus.Leaf))
            {
                _caretStale = true;
            }
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
                RichBlockQuote { IsCollapsed: true, Children: [RichParagraphBlock text] } => Strings.AccDescrIVBlockquote + ", " + Strings.AccDescrIVCollapsed + ", " + text.Text.ToPlainText(),
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
                RichTextQuote => Strings.AccDescrIVPullquote,
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

            if (PlaceholderText is string placeholder)
            {
                return document.IsEmpty && ReferenceEquals(document.Blocks[0], block) ? placeholder : null;
            }

            var focused = ReferenceEquals(leaf, _engine.Focus.Leaf) && _hasFocus;

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
        // The line whose text decided the article's direction: only an edit there or before it
        // can change it, so typing further down does not ask again.
        private RichTextLeaf _directionLeaf;

        /// <summary>
        /// An article reads the way its first line with a strong character does, as its reader
        /// takes it; the editor mirrors with it, as an instant view does, and that is what is
        /// sent (<see cref="RichDocument.IsRtl"/>). One with no such line keeps what it was
        /// loaded with. A message's text is left to the app's direction, as a text box is.
        /// Returns whether the editor turned around.
        /// </summary>
        private bool UpdateDocumentDirection(RichEditorChanges changes, RichTextLeaf leaf)
        {
            if (_engine.Schema != null || (changes & (RichEditorChanges.Text | RichEditorChanges.Structure)) == 0)
            {
                return false;
            }

            if ((changes & RichEditorChanges.Structure) == 0 && leaf != null && _directionLeaf != null && leaf.Index > _directionLeaf.Index)
            {
                return false;
            }

            var document = _engine.Document;
            var rightToLeft = document.IsRtl;

            _directionLeaf = null;

            foreach (var item in document.Leaves)
            {
                if (item.Length == 0)
                {
                    continue;
                }

                var direction = item.Direction;
                if (direction != TextDirectionality.Neutral)
                {
                    _directionLeaf = item;
                    rightToLeft = direction == TextDirectionality.RightToLeft;
                    break;
                }
            }

            document.IsRtl = rightToLeft;

            var flow = rightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            if (FlowDirection == flow)
            {
                return false;
            }

            FlowDirection = flow;
            return true;
        }

        private bool _documentEmpty = true;

        // The one hint of a message's text depends on the whole document being empty, not on
        // where the caret is, so it is asked again whenever that changes.
        private void UpdateDocumentPlaceholder()
        {
            var empty = _engine.Document.IsEmpty;
            if (PlaceholderText == null || empty == _documentEmpty)
            {
                return;
            }

            _documentEmpty = empty;
            RefreshLeafChrome(_engine.Document.Leaves[0]);
        }

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
            UpdateSelection(true);
        }

        /// <summary>Draws the selection and the caret, and with <paramref name="reveal"/> scrolls the caret into view.</summary>
        private void UpdateSelection(bool reveal)
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
                SelectCell(cell);
            }

            // A table the selection runs past is taken whole, and is drawn as its cells.
            _engine.GetCoveredTables(_coveredTables);

            if (_selectedCells.Count == 0)
            {
                foreach (var table in _coveredTables)
                {
                    foreach (var row in table.Rows)
                    {
                        foreach (var cell in row)
                        {
                            SelectCell(cell);
                        }
                    }
                }
            }

            if (node == null && !_engine.IsSelectionEmpty && _selectedCells.Count == 0)
            {
                var start = _engine.Start;
                var end = _engine.End;
                var leaves = _engine.Document.Leaves;

                for (int i = start.Leaf.Index; i <= end.Leaf.Index; i++)
                {
                    if (leaves[i].Role == RichLeafRole.Cell && leaves[i].Owner is RichTableBlock owner && _coveredTables.Contains(owner))
                    {
                        continue;
                    }

                    if (_leafViews.TryGetValue(leaves[i], out RichLeafView view))
                    {
                        var from = i == start.Leaf.Index ? start.Offset : 0;
                        var to = i == end.Leaf.Index ? end.Offset : leaves[i].Length;

                        view.Select(from, to);
                        _selectedViews.Add(view);
                    }
                }
            }

            UpdateCaret(reveal);
            UpdateGrippers();
        }

        private void SelectCell(RichTableCell cell)
        {
            if (_cellPresenters.TryGetValue(cell, out RichTableCellPresenter presenter))
            {
                presenter.SetSelected(true);
                _selectedCellViews.Add(presenter);
            }
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

            // Laid out again with everything else under the text.
            DrawSpelling();
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
                var lines = view.GetRangeLines(range.Start, range.End);
                if (lines.Length == 0)
                {
                    continue;
                }

                for (int i = 0; i < lines.Length; i++)
                {
                    lines[i] = transform.TransformBounds(lines[i]);
                }

                XamlPath shape;
                if (used < _decorationShapes.Count)
                {
                    shape = _decorationShapes[used];
                }
                else
                {
                    shape = new XamlPath();

                    _decorationShapes.Add(shape);
                    Decorations.Children.Add(shape);
                }

                shape.Fill = fill;
                shape.Data = RoundedOutline(lines, DecorationRadius);
                shape.Visibility = Visibility.Visible;

                used++;
            }

            return used;
        }

        private const double DecorationRadius = 3;

        // The lines of a range as one shape: their outline, stepping in and out where one line
        // is longer than the next, every corner rounded, inner ones too. Lines that do not touch
        // - an empty one between them - make a shape each.
        private static PathGeometry RoundedOutline(Rect[] lines, double radius)
        {
            var geometry = new PathGeometry();
            var first = 0;

            for (int i = 1; i <= lines.Length; i++)
            {
                if (i < lines.Length && Touches(lines[i - 1], lines[i]))
                {
                    continue;
                }

                AddOutline(geometry, lines, first, i, radius);
                first = i;
            }

            return geometry;
        }

        private static bool Touches(Rect above, Rect below)
        {
            return below.Width >= 1 && above.Width >= 1
                && Math.Abs(below.Top - above.Bottom) < 1
                && below.Left < above.Right && below.Right > above.Left;
        }

        private static void AddOutline(PathGeometry geometry, Rect[] lines, int from, int to, double radius)
        {
            if (lines[from].Width < 1 || lines[from].Height < 1)
            {
                return;
            }

            // Clockwise from the top left: down the right edges, back up the left ones, each
            // step between two lines halfway between them. Edges within a pixel of each other
            // are one edge, which a corner of no size would otherwise break.
            var points = new List<Point>((to - from) * 4);

            points.Add(new Point(lines[from].Left, lines[from].Top));
            points.Add(new Point(lines[from].Right, lines[from].Top));

            var right = lines[from].Right;

            for (int i = from; i < to - 1; i++)
            {
                var y = (lines[i].Bottom + lines[i + 1].Top) / 2;
                var next = Math.Abs(lines[i + 1].Right - right) < 1 ? right : lines[i + 1].Right;

                if (next != right)
                {
                    points.Add(new Point(right, y));
                    points.Add(new Point(next, y));
                    right = next;
                }
            }

            points.Add(new Point(right, lines[to - 1].Bottom));

            var left = lines[to - 1].Left;
            points.Add(new Point(left, lines[to - 1].Bottom));

            for (int i = to - 1; i > from; i--)
            {
                var y = (lines[i - 1].Bottom + lines[i].Top) / 2;
                var next = Math.Abs(lines[i - 1].Left - left) < 1 ? left : lines[i - 1].Left;

                if (next != left)
                {
                    points.Add(new Point(left, y));
                    points.Add(new Point(next, y));
                    left = next;
                }
            }

            // Where the outline came back to the start's column, the first point closes it.
            if (left != lines[from].Left)
            {
                points.Add(new Point(left, lines[from].Top));
            }

            // Each corner cut back along both its edges by the radius, or half the shorter one,
            // and joined by a curve through the corner.
            var count = points.Count;
            var figure = new PathFigure { IsClosed = true, IsFilled = true };

            for (int k = 0; k <= count; k++)
            {
                var corner = points[k % count];
                var previous = points[(k + count - 1) % count];
                var next = points[(k + 1) % count];

                var before = Distance(previous, corner);
                var after = Distance(corner, next);
                var r = Math.Min(radius, Math.Min(before, after) / 2);

                var entry = Toward(corner, previous, r, before);
                var exit = Toward(corner, next, r, after);

                if (k == 0)
                {
                    figure.StartPoint = exit;
                    continue;
                }

                figure.Segments.Add(new LineSegment { Point = entry });
                figure.Segments.Add(new QuadraticBezierSegment { Point1 = corner, Point2 = exit });
            }

            geometry.Figures.Add(figure);
        }

        private static double Distance(Point a, Point b)
        {
            var x = b.X - a.X;
            var y = b.Y - a.Y;

            return Math.Sqrt(x * x + y * y);
        }

        // The point r along the edge from a toward b, which is length long.
        private static Point Toward(Point a, Point b, double r, double length)
        {
            return length > 0
                ? new Point(a.X + (b.X - a.X) * r / length, a.Y + (b.Y - a.Y) * r / length)
                : a;
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

        private bool IsFocusedOrMenuOpen => _hasFocus || _menuOpen;

        private bool _menuOpen;
        private bool _invalidOverlay;

        // The same, for what the window realized: drawn without scrolling to the caret.
        private bool _staleOverlay;

        private void RedrawOverlay()
        {
            var started = Stopwatch.GetTimestamp();

            UpdateSelection(false);

            OverlayCount++;
            OverlayTicks += Stopwatch.GetTimestamp() - started;
        }

        /// <summary>The caret rectangle in root coordinates, or empty when there is no caret.</summary>
        internal Rect GetCaretRect()
        {
            return _engine.SelectedNode != null ? Rect.Empty : GetCaretRect(_engine.Focus);
        }

        /// <summary>Where a caret at <paramref name="position"/> would be, in root coordinates, or empty when its text is not realized.</summary>
        private Rect GetCaretRect(RichPosition position)
        {
            if (!position.IsValid || !_leafViews.TryGetValue(position.Leaf, out RichLeafView view) || view.Visibility != Visibility.Visible)
            {
                return Rect.Empty;
            }

            var rect = view.GetCaretRect(position.Offset);
            if (rect.IsEmpty)
            {
                return Rect.Empty;
            }

            var origin = view.TransformToVisual(Surface).TransformPoint(new Point(rect.X, rect.Y));
            return new Rect(origin.X, origin.Y, 0, rect.Height);
        }

        private void UpdateCaret()
        {
            UpdateCaret(true);
        }

        private void UpdateCaret(bool reveal)
        {
            if (Caret == null)
            {
                return;
            }

            // First, as it can lay the caret's line out again in the other direction.
            UpdateKeyboardLine();

            // Over a selection too, at the end that moves, except where grippers or whole cells
            // stand for it.
            var visible = _hasFocus && _engine.SelectedNode == null && _selectedCells.Count == 0
                && (_engine.IsSelectionEmpty || !_touchSelection);
            if (!visible)
            {
                HideCaret();
                return;
            }

            // The text may have been laid out again by this very change; measuring it now is
            // what keeps the caret from trailing a keystroke behind.
            Surface.UpdateLayout();

            var rect = GetCaretRect();
            if (rect.IsEmpty)
            {
                HideCaret();
                return;
            }

            var flag = CaretFlagSize();
            var mirrored = FlowDirection == FlowDirection.RightToLeft;

            // Inside the surface, flag and all: a line starting at the far edge - an empty right
            // to left one starts at it - put the bar just past it, and only the flag showed. At
            // the near edge the bar moves over for the flag, as CTextBoxView::UpdateCaretElement
            // does. The flag is on the left, which a mirrored canvas counts from the other side.
            var left = Math.Round(rect.X);
            var min = mirrored ? 0 : flag;
            var max = Surface.ActualWidth - Caret.Width - (mirrored ? flag : 0);

            if (max >= min)
            {
                left = Math.Clamp(left, min, max);
            }

            Canvas.SetLeft(Caret, left);
            Canvas.SetTop(Caret, rect.Y);
            Caret.Height = rect.Height;
            Caret.Visibility = Visibility.Visible;

            UpdateCaretFlag(left, rect.Y, flag);
            RestartBlink();

            if (reveal)
            {
                BringIntoView(rect);
            }
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
                StopBlink();
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

            // Started together, so the two keep time.
            if (CaretFlag != null)
            {
                var flag = ElementCompositionPreview.GetElementVisual(CaretFlag);
                flag.StopAnimation("Opacity");
                flag.Opacity = 1;
                flag.StartAnimation("Opacity", _blink);
            }

            _blinking = true;
        }

        // Collapsing the caret does not stop its animation, which runs forever on the compositor.
        private void HideCaret()
        {
            Caret.Visibility = Visibility.Collapsed;

            if (CaretFlag != null)
            {
                CaretFlag.Visibility = Visibility.Collapsed;
            }

            StopBlink();
        }

        private void StopBlink()
        {
            if (!_blinking || Caret == null)
            {
                return;
            }

            _blinking = false;

            var visual = ElementCompositionPreview.GetElementVisual(Caret);
            visual.StopAnimation("Opacity");
            visual.Opacity = 1;

            if (CaretFlag != null)
            {
                var flag = ElementCompositionPreview.GetElementVisual(CaretFlag);
                flag.StopAnimation("Opacity");
                flag.Opacity = 1;
            }
        }

        /// <summary>
        /// The caret's flag, shown while the keyboard writes right to left, beside the bar on its
        /// reading side, as CTextBoxView::UpdateCaretElement places it: 2 DIPs, rounded up to whole
        /// pixels (GetRTLIndicatorSize).
        /// </summary>
        private void UpdateCaretFlag(double left, double top, double size)
        {
            if (CaretFlag == null)
            {
                return;
            }

            if (size <= 0)
            {
                CaretFlag.Visibility = Visibility.Collapsed;
                return;
            }

            CaretFlag.Width = size;
            CaretFlag.Height = size;

            // A mirrored editor mirrors the canvas too: the bar's left there is where it ends.
            Canvas.SetLeft(CaretFlag, FlowDirection == FlowDirection.RightToLeft ? left + Caret.Width : left - size);
            Canvas.SetTop(CaretFlag, top);
            CaretFlag.Visibility = Visibility.Visible;
        }

        // How wide the flag is, 0 when there is none.
        private double CaretFlagSize()
        {
            if (CaretFlag == null || !_keyboardRightToLeft)
            {
                return 0;
            }

            var scale = XamlRoot?.RasterizationScale ?? 1;
            return Math.Ceiling(2 * scale) / scale;
        }

        // The view of the caret's line, which reads the keyboard's way while it has no strong
        // character of its own; the one before it goes back to the editor's.
        private RichLeafView _keyboardLine;

        private void UpdateKeyboardLine()
        {
            RichLeafView view = null;

            if (_hasFocus && _engine != null && _engine.SelectedNode == null)
            {
                _leafViews.TryGetValue(_engine.Focus.Leaf, out view);
            }

            if (!ReferenceEquals(view, _keyboardLine))
            {
                if (_keyboardLine != null)
                {
                    _keyboardLine.NeutralRightToLeft = null;
                }

                _keyboardLine = view;
            }

            if (view != null)
            {
                view.NeutralRightToLeft = _keyboardRightToLeft;
            }
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
            var offset = _scrollingTo ?? ScrollViewer.VerticalOffset;
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

        // Where a scroll this control asked for is going, until it gets there: the offset read
        // in between is still the old one, and the caret would be brought into view from it.
        private double? _scrollingTo;

        private void OnViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (!e.IsIntermediate)
            {
                _scrollingTo = null;
            }

            // The text input service keeps its candidate window beside the caret.
            NotifyLayoutChanged();
        }

        // Below the blocks, should the column not reach that far; within it, the surface's
        // own handlers have already answered.
        private void OnScrollViewerTapped(object sender, TappedRoutedEventArgs e)
        {
            if (Surface != null && TryAppendBelow(e.GetPosition(Surface)))
            {
                Focus(FocusState.Pointer);
                e.Handled = true;
            }
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
                _staleOverlay = false;
                this.PostOnUIThread(UpdateSelection, low: true);
            }
            else if (_staleOverlay)
            {
                _staleOverlay = false;
                this.PostOnUIThread(RedrawOverlay, low: true);
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
