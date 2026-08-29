//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Native;
using Telegram.Native.Controls;
using Telegram.Native.Highlight;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Core.Direct;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Controls
{
    public partial class TextEntityClickEventArgs : HandledEventArgs
    {
        public TextEntityClickEventArgs(TextEntityType type, string text = null)
        {
            Type = type;
            Text = text;
        }

        public TextEntityType Type { get; }

        public string Text { get; }
    }

    // A Hyperlink with its XamlDirect projection. It is the one element the block builds through
    // the projection rather than through XamlDirect, because Click has no XamlEventIndex - and
    // GetXamlDirectObject plus the Inlines lookup measured ~18us together, more than everything
    // else the link path does. Both answers are stable for the life of the element, so they are
    // taken once and travel with it through the pool.
    public sealed class ProjectedHyperlink
    {
        public readonly Hyperlink Element;
        public readonly IXamlDirectObject Native;
        public readonly IXamlDirectObject Inlines;

        public ProjectedHyperlink(XamlDirect direct, Hyperlink element)
        {
            Element = element;
            Native = direct.GetXamlDirectObject(element);
            Inlines = direct.GetXamlDirectObjectProperty(Native, XamlPropertyIndex.Span_Inlines);
        }
    }

    public class FormattedTextBlockRecyclePool
    {
        // Bounded per kind, for the reason MessageContentRecyclePool is: an element is only of use
        // to a block that is about to render, so what the realized blocks need at once is the
        // ceiling worth keeping. Uncapped, one pathological message - a code block, or a text dense
        // with entities - parks its whole element tree here until the window closes.
        //
        // Runs get the larger share because they are the only kind produced in bulk: one per style
        // change, and one per syntax token inside a code block. A paragraph, a span or a hyperlink
        // is a handful per message at most.
        private const int RunCapacity = 256;
        private const int Capacity = 64;

        // Private so the caps cannot be walked around: everything goes back in through Put.
        private readonly Queue<IXamlDirectObject> _paragraphs = new();
        private readonly Queue<ProjectedHyperlink> _hyperlinks = new();
        private readonly Queue<IXamlDirectObject> _spans = new();
        private readonly Queue<IXamlDirectObject> _runs = new();
        //private readonly Queue<InlineUIContainer> _emoji = new();

        public bool TryTakeParagraph(out IXamlDirectObject paragraph)
        {
            return _paragraphs.TryDequeue(out paragraph);
        }

        public bool TryTakeHyperlink(out ProjectedHyperlink hyperlink)
        {
            return _hyperlinks.TryDequeue(out hyperlink);
        }

        public bool TryTakeSpan(out IXamlDirectObject span)
        {
            return _spans.TryDequeue(out span);
        }

        public bool TryTakeRun(out IXamlDirectObject run)
        {
            return _runs.TryDequeue(out run);
        }

        public void PutParagraph(IXamlDirectObject paragraph)
        {
            if (_paragraphs.Count < Capacity)
            {
                _paragraphs.Enqueue(paragraph);
            }
#if NET9_0_OR_GREATER
            else
            {
                Utils.ReleaseHandle(paragraph);
            }
#endif
        }

        public void PutHyperlink(ProjectedHyperlink hyperlink)
        {
            if (_hyperlinks.Count < Capacity)
            {
                _hyperlinks.Enqueue(hyperlink);
            }
#if NET9_0_OR_GREATER
            else
            {
                Utils.ReleaseHandle(hyperlink.Native);
                Utils.ReleaseHandle(hyperlink.Inlines);
            }
#endif
        }

        public void PutSpan(IXamlDirectObject span)
        {
            if (_spans.Count < Capacity)
            {
                _spans.Enqueue(span);
            }
#if NET9_0_OR_GREATER
            else
            {
                Utils.ReleaseHandle(span);
            }
#endif
        }

        public void PutRun(IXamlDirectObject run)
        {
            if (_runs.Count < RunCapacity)
            {
                _runs.Enqueue(run);
            }
#if NET9_0_OR_GREATER
            else
            {
                Utils.ReleaseHandle(run);
            }
#endif
        }

        public void Clear()
        {
            _paragraphs.Clear();
            _hyperlinks.Clear();
            _spans.Clear();
            _runs.Clear();
            //_emoji.Clear();
        }

#if NET9_0_OR_GREATER
        public void ReleaseNative()
        {
            foreach (var paragraph in _paragraphs)
            {
                Utils.ReleaseHandle(paragraph);
            }

            foreach (var span in _spans)
            {
                Utils.ReleaseHandle(span);
            }

            foreach (var run in _runs)
            {
                Utils.ReleaseHandle(run);
            }

            foreach (var hyperlink in _hyperlinks)
            {
                Utils.ReleaseHandle(hyperlink.Native);
                Utils.ReleaseHandle(hyperlink.Inlines);
            }

            Clear();
        }
#endif
    }

    [ContentProperty(Name = "Blocks")]
    public partial class FormattedTextBlock : FormattedTextBlockBase
    {
        private IClientService _clientService;
        private StyledText _text;
        private bool _plain = true;
        private TextDirectionality _direction;
        private double _fontSize;

        private IXamlDirectObject _fastRun;

        private string _query;

        // Paragraph range of the shared StyledText this control currently renders
        // (inclusive). Full text => [0, Paragraphs.Count - 1]. Used to keep the build loop
        // and the post-build geometry passes in sync when MessageTextBlock renders a slice.
        private int _first;
        private int _last;

        private bool _ignoreSpoilers = false;

        private AnimatedImage _spoilerPresenter;
        private CanvasGeometry _spoilerGeometry;
        private bool _spoilerAdded;

        private Span _spanForInlines;

        // Null until the text actually carries one of these, which for most blocks is never.
        private List<Hyperlink> _links;
        private List<IXamlDirectObject> _dates;
        private List<TextStyleSpoiler> _spoilers;

        // Offset and Length are in the paragraph's SOURCE text, so they never go stale. The
        // displayed position - which moves whenever a relative date in front of the spoiler is
        // rewritten - is derived from the paragraph's runs at the point it is needed
        // (UpdateSpoilers). It used to be stored here and patched on every date tick, which
        // could not be right for more than one date.
        readonly struct TextStyleSpoiler
        {
            public readonly int Offset;
            public readonly int Length;
            public readonly int ParagraphIndex;

            public TextStyleSpoiler(int offset, int length, int paragraphIndex)
            {
                Offset = offset;
                Length = length;
                ParagraphIndex = paragraphIndex;
            }
        }

        private TextHighlighter _cached;
        private TextHighlighter _marked;
        private TextHighlighter _spoiler;

        private bool _highlightersPending;

        // Map between the rendered/highlighter index space (the `offset` SetText builds,
        // and the space TextHighlighter.Ranges use) and StyledText.Text offsets. Built
        // by SetText as it emits runs; used by the selection layer (FormattedTextBlock.
        // Selectable.cs) to convert a selection back to a StyledText slice for copy.
        // Each segment is rendered/styled-contiguous; lengths differ for custom emoji,
        // dates and ZWNJ inserts (and paragraph breaks advance styled but not rendered).
        private readonly struct IndexSegment
        {
            public readonly int Rendered;
            public readonly int Styled;
            public readonly int RenderedLength;
            public readonly int StyledLength;

            public IndexSegment(int rendered, int styled, int renderedLength, int styledLength)
            {
                Rendered = rendered;
                Styled = styled;
                RenderedLength = renderedLength;
                StyledLength = styledLength;
            }
        }

        private List<IndexSegment> _indexMap;

        // The StyledText offset that rendered index 0 maps to. The map holds absolute offsets,
        // so this is only read on the no-map fallback — where the block can still be rendering
        // a paragraph that doesn't start at 0, since MessageTextBlock hands out slices of one
        // shared StyledText. Zero for a block that renders from the first paragraph.
        private int _origin;

        private Canvas Below;
        private RichTextBlock TextBlock;

        private bool _templateApplied;
        private bool _textApplied;

        // Identifies the current content for work that outlives SetText (ProcessCodeBlock's
        // tokenization). Never reset — _textApplied is cleared on unload, so a flag or a
        // restarting counter would match again on the next render and let a stale
        // tokenization write into inlines already recycled into another block.
        private int _generation;

        public FormattedTextBlock()
        {
            DefaultStyleKey = typeof(FormattedTextBlock);
        }

        public StyledText Text => _text;

        public bool AdjustLineEnding { get; set; }

        private bool _hasLineEnding;
        public bool HasLineEnding
        {
            get => _hasLineEnding;
            set
            {
                if (_hasLineEnding != value)
                {
                    _hasLineEnding = value;
                    //InvalidateMeasure();
                }
            }
        }

        private bool _hasCodeBlocks;
        public bool HasCodeBlocks
        {
            get => _hasCodeBlocks;
            set
            {
                if (_hasCodeBlocks != value)
                {
                    _hasCodeBlocks = value;

                    if (value)
                    {
                        ActualThemeChanged += OnActualThemeChanged;
                    }
                    else
                    {
                        ActualThemeChanged -= OnActualThemeChanged;
                    }
                }
            }
        }

        private IList<Block> _blocks;
        public IList<Block> Blocks
        {
            get => TextBlock?.Blocks ?? (_blocks ??= new List<Block>());
        }

        public event EventHandler<TextEntityClickEventArgs> TextEntityClick;

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            Below = GetTemplateChild(nameof(Below)) as Canvas;
            TextBlock = GetTemplateChild(nameof(TextBlock)) as RichTextBlock;

            if (TextBlock != null)
            {
                // Driven in code (was a TemplateBinding): only the native Enabled mode
                // turns on the inner control's own selection; Disabled/Extended leave it
                // off (Extended is handled by TextSelectionManager).
                TextBlock.IsTextSelectionEnabled = TextSelection == TextSelectionMode.Enabled;
            }

            // The XAML-declared Blocks, handed over once. Dropped afterwards because a second
            // OnApplyTemplate - a style or theme change - would otherwise re-parent Paragraphs
            // that already belong to a RichTextBlock. Blocks reads through to TextBlock.Blocks
            // from here on.
            if (TextBlock != null && _blocks != null)
            {
                for (int i = 0; i < _blocks.Count; i++)
                {
                    if (_blocks[i] is not Paragraph block)
                    {
                        continue;
                    }

                    TextBlock.Blocks.Add(block);

                    if (i == _blocks.Count - 1 && block.Inlines.Count > 0 && block.Inlines[^1] is Span spanForInlines)
                    {
                        _spanForInlines = spanForInlines;
                    }
                }

                _blocks = null;
            }

            _templateApplied = true;

            if (/*_clientService != null &&*/ _text != null)
            {
                // SetText applies the highlighters itself (ApplyHighlighters).
                SetText(_clientService, _text, _first, _last, _fontSize);
            }
        }

        public double LastAvailableWidth { get; private set; }

        public bool IsTextTrimmable { get; private set; }
        public event EventHandler IsTextTrimmableChanged;

        // Inputs of the last MaxLines call, so a re-measure at the same width doesn't repeat it.
        // The text is compared by reference: GetParts hands back the same instance until a
        // relative date rewrites the paragraph, which is exactly when the answer can change.
        private string _trimmableText;
        private double _trimmableWidth;
        private double _trimmableSize;

        protected override Size MeasureOverride(Size availableSize)
        {
            if (_text != null && TextBlock != null && _first == _last && _text.Paragraphs[_first].Type is TextParagraphTypeQuote { IsExpandable: true })
            {
                var styled = _text.Paragraphs[_first];
                var entities = styled.GetParts(out var partial) ?? TextStyleRun.NoParts;
                var quoteSize = (AutoFontSize ? AppSettings.Appearance.CaptionFontSize : TextBlock.FontSize) * BootStrapper.Current.TextScaleFactor;

                if (!ReferenceEquals(partial, _trimmableText) || availableSize.Width != _trimmableWidth || quoteSize != _trimmableSize)
                {
                    _trimmableText = partial;
                    _trimmableWidth = availableSize.Width;
                    _trimmableSize = quoteSize;

                    var metrics = Direct2D.Current.MaxLines(partial, 0, partial.Length, entities, quoteSize, availableSize.Width, false, 3);
                    var trimmable = metrics.TruncatedHeight < metrics.Height;

                    if (IsTextTrimmable != trimmable)
                    {
                        IsTextTrimmable = trimmable;
                        IsTextTrimmableChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
            }

            LastAvailableWidth = availableSize.Width;
            return base.MeasureOverride(availableSize);
        }

        private PointerCursorType _textSelectionCursor = PointerCursorType.Arrow;

        // Whether the Hand above is a spoiler's, which also takes the text out of hit-testing.
        // Tracked apart from the cursor itself, as an inline button shows the Hand too.
        private bool _spoilerCursor;

        // The answer to "is the pointer over a link", and the point it was resolved for. The
        // hit test behind it costs ~13us at pointer sample rate, and a move of a pixel or two
        // cannot change the answer except within that distance of a link's edge - where being a
        // frame late in swapping the cursor is not something anyone can see.
        private Point _cursorPoint;
        private bool _cursorOverLink;
        private bool _cursorResolved;

        protected override void OnPointerMoved(PointerRoutedEventArgs e)
        {
            try
            {
                base.OnPointerMoved(e);
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }

            if (_spanForInlines == null && _spoilerGeometry != null && _spoilerPresenter != null)
            {
                var point = e.GetCurrentPoint(_spoilerPresenter);
                var position = point.Position.ToVector2();

                if (IsPointerWithinSpoiler(position))
                {
                    if (!_spoilerCursor)
                    {
                        _spoilerCursor = true;
                        _textSelectionCursor = PointerCursorType.Hand;
                        TextBlock.IsHitTestVisible = false;
                        WindowContext.SetPointerCursor(PointerCursorType.Hand);
                    }

                    e.Handled = true;
                    return;
                }
            }

            if (_spanForInlines == null && _spoilerCursor)
            {
                _spoilerCursor = false;
                _textSelectionCursor = PointerCursorType.Arrow;
                TextBlock.IsHitTestVisible = true;
                WindowContext.SetPointerCursor(PointerCursorType.Arrow);
            }

            // Extended: native selection is off, so RichTextBlock won't show the I-beam.
            // Drive it ourselves over text; leave the cursor over a hyperlink (its own Hand)
            // and over a spoiler (handled above, which returns early).
            if (_spanForInlines == null && TextBlock != null && _textSelection == TextSelectionMode.Extended)
            {
                // Without a link in the message the pointer cannot be over one wherever it is,
                // and the whole question - GetCurrentPoint included, the expensive half - is skipped.
                if (_activeHyperlinks.Count > 0)
                {
                    var point = e.GetCurrentPoint(TextBlock).Position;
                    if (!_cursorResolved
                        || Math.Abs(point.X - _cursorPoint.X) >= 2
                        || Math.Abs(point.Y - _cursorPoint.Y) >= 2)
                    {
                        _cursorOverLink = IsLinkAt(TextBlock.GetPositionFromPoint(point).Offset);
                        _cursorPoint = point;
                        _cursorResolved = true;
                    }
                }

                if (!_cursorOverLink)
                {
                    // There's no text to select over an inline button: it reads as a button
                    // (Hand), or as nothing at all when it's disabled.
                    var button = GetInlineButtonFromSource(e.OriginalSource);
                    var cursor = button == null
                        ? PointerCursorType.IBeam
                        : button.IsEnabled ? PointerCursorType.Hand : PointerCursorType.Arrow;

                    // Only on the way in: this runs at pointer sample rate, and setting
                    // PointerCursor is a marshalled call on top of the allocation.
                    if (_textSelectionCursor != cursor)
                    {
                        _textSelectionCursor = cursor;
                        WindowContext.SetPointerCursor(cursor);
                    }
                }
            }
        }

        // The inline button (TextEntityTypeButton) under the pointer, or null. The button is a
        // real control inside an InlineUIContainer, so it — not the RichTextBlock — is what
        // the pointer lands on; a disabled one takes no input at all, which is what the
        // transparent wrapper in CreateInlineButton is for. Text hits report the RichTextBlock
        // itself, where the walk ends immediately.
        private ReplyMarkupInlineButton GetInlineButtonFromSource(object source)
        {
            var node = source as DependencyObject;

            while (node != null && node != TextBlock && node != this)
            {
                if (node is ReplyMarkupInlineButton button)
                {
                    return button;
                }
                else if (node is Border wrapper && wrapper.Child is ReplyMarkupInlineButton disabled)
                {
                    return disabled;
                }

                node = VisualTreeHelper.GetParent(node);
            }

            return null;
        }

        protected override void OnPointerExited(PointerRoutedEventArgs e)
        {
            if (_spanForInlines == null && _textSelectionCursor != PointerCursorType.Arrow)
            {
                _spoilerCursor = false;
                _textSelectionCursor = PointerCursorType.Arrow;
                TextBlock.IsHitTestVisible = true;
                WindowContext.SetPointerCursor(PointerCursorType.Arrow);
            }

            try
            {
                base.OnPointerExited(e);
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }

        protected override void OnTapped(TappedRoutedEventArgs e)
        {
            if (_spanForInlines == null && _spoilerGeometry != null && _spoilerPresenter != null)
            {
                var point = e.GetPosition(_spoilerPresenter);
                var position = point.ToVector2();

                if (IsPointerWithinSpoiler(position))
                {
                    IgnoreSpoilers = true;
                    e.Handled = true;
                }
            }

            base.OnTapped(e);
        }

        private bool IsPointerWithinSpoiler(Vector2 position)
        {
            if (position.X >= 0 && position.Y >= 0 && position.X <= _spoilerPresenter.ActualSize.X && position.Y <= _spoilerPresenter.ActualSize.Y)
            {
                return _spoilerGeometry.FillContainsPoint(position);
            }

            return false;
        }

        // Resets what a recycled container would otherwise inherit (MessageService.Recycle and
        // the service contents that override it). All four highlighter sources go, and
        // ApplyHighlighters takes them off the RichTextBlock - dropping the fields alone left
        // the previous message's spoiler and highlights painted on the next one.
        public void Clear()
        {
            //_clientService = null;
            //_text = null;

            _query = null;
            _spoiler = null;
            _cached = null;
            _marked = null;
            _selection = null;
            _ignoreSpoilers = false;

            ClearEntities();
            ApplyHighlighters();
        }

        private void ClearEntities()
        {
            if (_links != null)
            {
                foreach (var link in _links)
                {
                    ToolTipService.SetToolTip(link, null);
                }

                _links.Clear();
            }

            if (_dates != null)
            {
                foreach (var date in _dates)
                {
                    RelativeDateService.Unsubscribe(date, XamlRoot);
                }

                _dates.Clear();
            }

            _spoilers?.Clear();

            if (_effectiveViewportChanged != null)
            {
                _effectiveViewportChanged = null;
                UnregisterViewportChanged();
            }
        }

        public bool IgnoreSpoilers
        {
            get => _ignoreSpoilers;
            set
            {
                if (value == _ignoreSpoilers)
                {
                    return;
                }

                _ignoreSpoilers = value;

                if (value)
                {
                    // SetText reapplies the highlighters; SetQuery(string.Empty) used to follow
                    // it for that, and only had the side effect of dropping the search term.
                    SetText(_clientService, _text, _first, _last, _fontSize);

                    if (Below == null || _spoilerPresenter == null)
                    {
                        return;
                    }

                    Below.Children.Remove(_spoilerPresenter);
                    _spoilerPresenter = null;
                    _spoilerGeometry = null;
                    _spoilerAdded = false;
                }
            }
        }

        public void SetFontSize(double fontSize)
        {
            _fontSize = fontSize;

            if (TextBlock?.Blocks.Count > 0 && TextBlock.Blocks[0] is Paragraph existing)
            {
                existing.FontSize = fontSize;
            }
        }

        // Sets the search query highlighter only. The spoiler/cached/marked highlighters
        // are owned by SetText and applied there, so callers that don't search never have
        // to call this. Both setters funnel through ApplyHighlighters.
        public void SetQuery(string query, bool force = false)
        {
            if (!force && (_query ?? string.Empty) == (query ?? string.Empty))
            {
                return;
            }

            _query = query;
            ApplyHighlighters();
        }

        // Rebuilds the inner RichTextBlock's TextHighlighters from the current state:
        // the query highlighter (from _query, recomputed against the live text) plus the
        // marked/cached/spoiler highlighters SetText produced. Both SetText and SetQuery
        // call this, so neither has to be invoked after the other. Z-order (bottom to
        // top): query, marked, cached, spoiler, then the active selection.
        private void ApplyHighlighters()
        {
            if (TextBlock == null || !TextBlock.IsLoaded)
            {
                // Everything SetText just computed - spoiler included - would be lost, and
                // nothing else would put it back, so remember to run again once we're loaded.
                _highlightersPending = true;
                return;
            }

            _highlightersPending = false;
            TextBlock.TextHighlighters.Clear();

            if (_text != null && _query?.Length > 0 && _last >= _first)
            {
                // Search within THIS block's paragraph range only (shared StyledText), then
                // map the absolute match offset to this block's rendered index via _indexMap.
                // Clamp to the text length (paragraph offsets can occasionally over-run it).
                var blockStart = Math.Min(_text.Paragraphs[_first].Offset, _text.Text.Length);
                var blockEnd = Math.Min(_text.Paragraphs[_last].Offset + _text.Paragraphs[_last].Length, _text.Text.Length);

                var find = blockEnd > blockStart
                    ? _text.Text.IndexOf(_query, blockStart, blockEnd - blockStart, StringComparison.OrdinalIgnoreCase)
                    : -1;
                if (find != -1)
                {
                    // Inline mode renders into a Span the host owns, behind whatever it put in
                    // front (a sender name), and highlighter indices count from the start of
                    // the whole RichTextBlock. Same correction the spoiler ranges apply.
                    var start = StyledToRendered(find);
                    if (_spanForInlines != null)
                    {
                        start += _spanForInlines.ContentStart.OffsetToIndex(TextBlock);
                    }

                    var highligher = new TextHighlighter();
                    highligher.Foreground = new SolidColorBrush(Colors.White);
                    highligher.Background = new SolidColorBrush(Colors.Orange);
                    highligher.Ranges.Add(new TextRange { StartIndex = start, Length = _query.Length });

                    TextBlock.TextHighlighters.Add(highligher);
                }
            }

            if (_marked != null)
            {
                TextBlock.TextHighlighters.Add(_marked);
            }

            if (_cached != null)
            {
                TextBlock.TextHighlighters.Add(_cached);
            }

            if (_spoiler != null)
            {
                TextBlock.TextHighlighters.Add(_spoiler);
            }
            else if (Below != null && _spoilerPresenter != null)
            {
                Below.Children.Remove(_spoilerPresenter);
                _spoilerPresenter = null;
                _spoilerGeometry = null;
                _spoilerAdded = false;
            }

            // The cross-block selection (Extended mode) sits on top of the base ones.
            if (_selection != null)
            {
                TextBlock.TextHighlighters.Add(_selection);
            }
        }

        public void SetText(IClientService clientService, FormattedText text, double fontSize = 0)
        {
            SetText(clientService, TextStyleRun.GetText(text), fontSize);
        }

        public void SetText(IClientService clientService, string text, Vector<TextEntity> entities, double fontSize = 0)
        {
            SetText(clientService, TextStyleRun.GetText(text, entities), fontSize);
        }

        public void SetText(IClientService clientService, RichText text)
        {
            SetText(clientService, TextStyleRun.GetText(text));
        }

        private readonly List<IXamlDirectObject> _activeParagraphs = new();
        private readonly List<ProjectedHyperlink> _activeHyperlinks = new();
        private readonly List<IXamlDirectObject> _activeSpans = new();
        private readonly List<IXamlDirectObject> _activeRuns = new();
        //private readonly HashSet<InlineUIContainer> _activeEmojis = new();

        private IXamlDirectObject GetOrCreateParagraph(XamlDirect direct)
        {
            if (_pools != null && _pools.TryTakeParagraph(out var paragraph))
            {
                direct.ClearProperty(paragraph, XamlPropertyIndex.Block_TextAlignment);
                direct.ClearProperty(paragraph, XamlPropertyIndex.TextElement_FontSize);
                direct.ClearProperty(paragraph, XamlPropertyIndex.TextElement_FontFamily);

                _activeParagraphs.Add(paragraph);
                return paragraph;
            }

            paragraph = direct.CreateInstance(XamlTypeIndex.Paragraph);
            _activeParagraphs.Add(paragraph);
            return paragraph;
        }

        // Static so it captures nothing: the Hyperlink outlives the block in the pool, and an
        // instance handler would root it there - which unsubscribing on Unloaded only fixes when
        // Unloaded is raised, and microsoft-ui-xaml#1900 says it isn't always.
        private static readonly TypedEventHandler<Hyperlink, HyperlinkClickEventArgs> _entityHandler = OnEntityClick;

        private static void OnEntityClick(Hyperlink sender, HyperlinkClickEventArgs e)
        {
            var args = MessageHelper.GetHyperlinkInfo(sender);
            var owner = sender.GetParent<FormattedTextBlock>();

            if (args != null && owner != null)
            {
                owner.Entity_Click(args);
            }
        }

        private ProjectedHyperlink GetOrCreateHyperlink(XamlDirect direct)
        {
            if (_pools != null && _pools.TryTakeHyperlink(out var hyperlink))
            {
                _activeHyperlinks.Add(hyperlink);
                return hyperlink;
            }

            hyperlink = new ProjectedHyperlink(direct, new Hyperlink());
            hyperlink.Element.Click += _entityHandler;

            _activeHyperlinks.Add(hyperlink);
            return hyperlink;
        }

        // A link's appearance through XamlDirect rather than the projection: a managed DependencyProperty
        // set measured ~7x the cost of the equivalent XamlDirect one, and every link carries three of
        // them. Returns the projected object, which the caller needs for the Inlines anyway.
        //
        // The weight is set unconditionally, pooled links included: a Hyperlink coming back from the
        // pool still carries whatever the last message set on it.
        private void ApplyHyperlinkProperties(XamlDirect direct, ProjectedHyperlink hyperlink, Brush foreground, UnderlineStyle underline, FontWeight weight)
        {
            direct.SetObjectProperty(hyperlink.Native, XamlPropertyIndex.TextElement_Foreground, foreground);
            direct.SetObjectProperty(hyperlink.Native, XamlPropertyIndex.TextElement_FontWeight, weight);
            direct.SetEnumProperty(hyperlink.Native, XamlPropertyIndex.Hyperlink_UnderlineStyle, (uint)underline);
        }

        // A pooled Span is reset here rather than at the sites that build one, on the rule
        // ApplyRunProperties spells out for Runs: the spoiler path and the code-block path set
        // different subsets of the same four properties, so leaving each to clear only what it sets
        // is how a bold code token came back as a bold spoiler.
        private IXamlDirectObject GetOrCreateSpan(XamlDirect direct)
        {
            if (_pools != null && _pools.TryTakeSpan(out var span))
            {
                direct.ClearProperty(span, XamlPropertyIndex.TextElement_Foreground);
                direct.ClearProperty(span, XamlPropertyIndex.TextElement_FontFamily);
                direct.ClearProperty(span, XamlPropertyIndex.TextElement_FontWeight);
                direct.ClearProperty(span, XamlPropertyIndex.TextElement_FontStyle);

                _activeSpans.Add(span);
                return span;
            }

            span = direct.CreateInstance(XamlTypeIndex.Span);

            _activeSpans.Add(span);
            return span;
        }

        // Resets a pooled Run to the requested style. Every property the build loop can set has
        // to be cleared when it is not wanted, or it would carry over from whatever the Run said
        // last time. NativeUtils.AddRunToCollection does the same for a Run it creates.
        private static void ApplyRunProperties(XamlDirect direct, IXamlDirectObject run, FlowDirection direction, TextStyle style, FontFamily fontFamily, double fontSize)
        {
            direct.SetEnumProperty(run, XamlPropertyIndex.Run_FlowDirection, (uint)direction);

            if ((style & TextStyle.Bold) != TextStyle.None)
            {
                direct.SetObjectProperty(run, XamlPropertyIndex.TextElement_FontWeight, FontWeights.SemiBold);
            }
            else
            {
                direct.ClearProperty(run, XamlPropertyIndex.TextElement_FontWeight);
            }

            if ((style & TextStyle.Italic) != TextStyle.None)
            {
                direct.SetEnumProperty(run, XamlPropertyIndex.TextElement_FontStyle, (uint)FontStyle.Italic);
            }
            else
            {
                direct.ClearProperty(run, XamlPropertyIndex.TextElement_FontStyle);
            }

            var decorations = TextDecorations.None;
            if ((style & TextStyle.Underline) != TextStyle.None)
            {
                decorations |= TextDecorations.Underline;
            }
            if ((style & TextStyle.Strikethrough) != TextStyle.None)
            {
                decorations |= TextDecorations.Strikethrough;
            }

            if (decorations != TextDecorations.None)
            {
                direct.SetEnumProperty(run, XamlPropertyIndex.TextElement_TextDecorations, (uint)decorations);
            }
            else
            {
                direct.ClearProperty(run, XamlPropertyIndex.TextElement_TextDecorations);
            }

            if (fontFamily != null)
            {
                direct.SetObjectProperty(run, XamlPropertyIndex.TextElement_FontFamily, fontFamily);
            }
            else
            {
                direct.ClearProperty(run, XamlPropertyIndex.TextElement_FontFamily);
            }

            if (fontSize > 0)
            {
                direct.SetDoubleProperty(run, XamlPropertyIndex.TextElement_FontSize, fontSize);
            }
            else
            {
                direct.ClearProperty(run, XamlPropertyIndex.TextElement_FontSize);
            }

            // Cleared rather than left alone: ProcessCodeBlock colours runs directly, and a
            // pooled one would carry that colour into the next message's plain text.
            direct.ClearProperty(run, XamlPropertyIndex.TextElement_Foreground);
        }

        // `prefix` is the zero-width character an inline object left behind, carried on the front
        // of the next plain run instead of in a Run of its own - see the emoji branch in SetText.
        private IXamlDirectObject GetOrCreateRun(XamlDirect direct, IXamlDirectObject inlines, string text, int offset, int length, FlowDirection direction, TextStyle style, FontFamily fontFamily, double fontSize, string prefix = null)
        {
            if (_pools != null && _pools.TryTakeRun(out var run))
            {
                direct.SetStringProperty(run, XamlPropertyIndex.Run_Text, prefix == null
                    ? text.Substring(offset, length)
                    : prefix + text.Substring(offset, length));
                ApplyRunProperties(direct, run, direction, style, fontFamily, fontSize);
                direct.AddToCollection(inlines, run);

                _activeRuns.Add(run);
                return run;
            }

            run = prefix == null
                ? NativeUtils.AddRunToCollection(direct, inlines, text, offset, length, direction, style, fontFamily, fontSize)
                : NativeUtils.AddRunToCollection(direct, inlines, prefix + text.Substring(offset, length), direction, style, fontFamily, fontSize);

            _activeRuns.Add(run);
            return run;
        }

        private IXamlDirectObject GetOrCreateRun(XamlDirect direct, IXamlDirectObject inlines, string text, FlowDirection direction, TextStyle style, FontFamily fontFamily, double fontSize)
        {
            if (_pools != null && _pools.TryTakeRun(out var run))
            {
                direct.SetStringProperty(run, XamlPropertyIndex.Run_Text, text);
                ApplyRunProperties(direct, run, direction, style, fontFamily, fontSize);
                direct.AddToCollection(inlines, run);

                _activeRuns.Add(run);
                return run;
            }

            run = NativeUtils.AddRunToCollection(direct, inlines, text, direction, style, fontFamily, fontSize);

            _activeRuns.Add(run);
            return run;
        }

        private CustomEmojiIcon GetOrCreateEmoji(out InlineUIContainer inline)
        {
            //if (_pools != null && _pools.Emoji.TryDequeue(out inline))
            //{
            //    _activeEmojis.Add(inline);
            //    return inline.Child as CustomEmojiIcon;
            //}

            var player = new CustomEmojiIcon();
            inline = new InlineUIContainer
            {
                Child = player
            };

            //_activeEmojis.Add(inline);
            return player;
        }

        private void Recycle(XamlDirect xd)
        {
            if (_pools != null)
            {
                IXamlDirectObject inlines;
                foreach (var paragraph in _activeParagraphs)
                {
                    inlines = xd.GetXamlDirectObjectProperty(paragraph, XamlPropertyIndex.Paragraph_Inlines);
                    xd.ClearCollection(inlines);
                    _pools.PutParagraph(paragraph);
                }
                foreach (var hyperlink in _activeHyperlinks)
                {
                    xd.ClearCollection(hyperlink.Inlines);
                    _pools.PutHyperlink(hyperlink);
                }
                foreach (var span in _activeSpans)
                {
                    inlines = xd.GetXamlDirectObjectProperty(span, XamlPropertyIndex.Span_Inlines);
                    xd.ClearCollection(inlines);
                    _pools.PutSpan(span);
                }
                foreach (var run in _activeRuns)
                {
                    _pools.PutRun(run);
                }
                // Let's disable emoji recycle for now and just recycle bare TextElement types
                //foreach (var emoji in _activeEmojis)
                //{
                //    if (_pools.Emoji.Count < 500)
                //    {
                //        _pools.Emoji.Enqueue(emoji);
                //    }
                //}
            }

            _fastRun = null;

            //_activeEmojis.Clear();
            _activeRuns.Clear();
            _activeSpans.Clear();
            _activeHyperlinks.Clear();
            _activeParagraphs.Clear();
        }

        protected override void OnLoaded()
        {
            // OnApplyTemplate runs before we're loaded, so the highlighters it computed may
            // have been dropped. This is the only chance to put them back for a block that
            // returns below.
            if (_highlightersPending)
            {
                ApplyHighlighters();
            }

            // Don't reapply the text if it was just applied by OnApplyTemplate
            if (_textApplied || _pools == null)
            {
                return;
            }

            if (/*_clientService != null &&*/ _text != null)
            {
                // SetText applies the highlighters itself (ApplyHighlighters).
                SetText(_clientService, _text, _first, _last, _fontSize);
            }
        }

        protected override void OnUnloaded()
        {
#if NET9_0_OR_GREATER
            // The handles are gone. ClearEntities would hand a disposed one to
            // RelativeDateService.Unsubscribe, and the tail of this method drives XamlDirect.
            if (_released)
            {
                return;
            }
#endif

            _textApplied = false;
            ClearEntities();

            if (!_templateApplied || _pools == null || (_fastRun != null && _plain))
            {
                return;
            }

            var direct = XamlDirect.GetDefault();

            if (_spanForInlines == null)
            {
                var directBlock = direct.GetXamlDirectObject(TextBlock);
                var blocks = direct.GetXamlDirectObjectProperty(directBlock, XamlPropertyIndex.RichTextBlock_Blocks);

                direct.ClearCollection(blocks);
            }
            else
            {
                _spanForInlines.Inlines.Clear();
            }

            Recycle(direct);
        }

        public void SetText(IClientService clientService, StyledText styled, double fontSize = 0)
        {
            SetText(clientService, styled, 0, (styled?.Paragraphs.Count ?? 0) - 1, fontSize);
        }

        // Renders only paragraphs [first, last] of a SHARED StyledText (MessageTextBlock hands
        // each child a block's range). Offsets stay absolute (Map/copy index the shared text);
        // the rendered/highlighter space is per-block. Full range == the single-arg overload.
        public void SetText(IClientService clientService, StyledText styled, int rangeStart, int rangeEnd, double fontSize = 0)
        {
#if NET9_0_OR_GREATER
            // Building now would create handles after the window's release pass has run, and
            // nothing would be left to dispose them.
            if (_released)
            {
                return;
            }
#endif

            var prevPlain = _plain;
            var prevDirection = _direction;
            var prevFontSize = _fontSize;

            var autoFontSize = fontSize;

            _clientService = clientService;
            _text = styled;
            _plain = styled != null && rangeStart == rangeEnd && styled.Paragraphs[rangeStart].IsPlain;
            _direction = styled != null && rangeStart == rangeEnd ? styled.Paragraphs[rangeStart].Direction : TextDirectionality.Neutral;
            _fontSize = fontSize;
            _first = rangeStart;
            _last = rangeEnd;
            InvalidateContentLength();
            _cursorResolved = false;
            _origin = styled != null && rangeStart <= rangeEnd && rangeStart < styled.Paragraphs.Count
                ? styled.Paragraphs[rangeStart].Offset
                : 0;

            if (!_templateApplied)
            {
                return;
            }

            _textApplied = true;

            var generation = ++_generation;

            var xamlFontSize = TextBlock.FontSize;
            if (AutoFontSize && fontSize == 0)
            {
                fontSize = AppSettings.Appearance.MessageFontSize;
            }

            var direct = XamlDirect.GetDefault();

#if NET9_0_OR_GREATER
            RegisterNative();
#endif
            var locale = LocaleService.Current.FlowDirection;

            // PERF: fast path if both model and view have one paragraph with one run
            if (_plain && _plain == prevPlain && !HasCodeBlocks)
            {
                var direction = _spanForInlines != null ? locale : _direction switch
                {
                    TextDirectionality.LeftToRight => FlowDirection.LeftToRight,
                    TextDirectionality.RightToLeft => FlowDirection.RightToLeft,
                    _ => locale
                };

                if (_fastRun == null)
                {
                    IXamlDirectObject paragraph;
                    IXamlDirectObject inlines;
                    if (_spanForInlines != null)
                    {
                        paragraph = null;
                        inlines = direct.GetXamlDirectObjectProperty(direct.GetXamlDirectObject(_spanForInlines), XamlPropertyIndex.Span_Inlines);
                    }
                    else
                    {
                        paragraph = GetOrCreateParagraph(direct);
                        inlines = direct.GetXamlDirectObjectProperty(paragraph, XamlPropertyIndex.Paragraph_Inlines);
                    }

                    _fastRun = GetOrCreateRun(direct, inlines, styled.Paragraphs[rangeStart].Text, direction, TextStyle.None, null, fontSize);

                    if (paragraph != null)
                    {
                        var directBlock2 = direct.GetXamlDirectObject(TextBlock);
                        var blocks2 = direct.GetXamlDirectObjectProperty(directBlock2, XamlPropertyIndex.RichTextBlock_Blocks);

                        direct.AddToCollection(blocks2, paragraph);
                    }
                }
                else
                {
                    if (_direction != prevDirection)
                    {
                        direct.SetEnumProperty(_fastRun, XamlPropertyIndex.Run_FlowDirection, (uint)direction);
                    }

                    // TODO: the if check is not correct here, as it should compare between computed sizes
                    if (_fontSize != prevFontSize)
                    {
                        // fontSize, not _fontSize: the latter is the raw value, which AutoFontSize
                        // resolves to the theme size above. XAML rejects the raw 0.
                        direct.SetDoubleProperty(_fastRun, XamlPropertyIndex.TextElement_FontSize, fontSize);
                    }

                    direct.SetStringProperty(_fastRun, XamlPropertyIndex.Run_Text, styled.Paragraphs[rangeStart].Text);
                }

                // Plain single run: rendered index == styled offset shifted by _origin, which
                // the converters' no-map fallback applies — no map needed.
                _indexMap = null;

                // Plain text has no spoiler/cached/marked; only the query may apply.
                ApplyHighlighters();

                HasLineEnding = AdjustLineEnding && direction != locale;

                if (!_skeletonCollapsed)
                {
                    RegisterLayoutChanged();
                }

                return;
            }

            var directBlock = direct.GetXamlDirectObject(TextBlock);
            var blocks = direct.GetXamlDirectObjectProperty(directBlock, XamlPropertyIndex.RichTextBlock_Blocks);

            ClearEntities();

            var textOffset = -1;

            if (_spanForInlines == null)
            {
                direct.ClearCollection(blocks);
            }
            else
            {
                _spanForInlines.Inlines.Clear();
            }

            Recycle(direct);

            if (string.IsNullOrEmpty(styled?.Text))
            {
                _spoiler = null;
                _cached = null;
                _marked = null;
                _indexMap = null;

                // Clear any highlighters left over from previous content.
                ApplyHighlighters();
                return;
            }

            TextHighlighter spoiler = null;
            TextHighlighter cached = null;
            TextHighlighter marked = null;

            var preformatted = false;
            TextParagraphType lastType = null;
            TextParagraphType firstType = null;

            var alignment = TextAlignment;
            var offset = 0;

            _indexMap = new List<IndexSegment>();

            // Records one rendered<->styled segment for the index map; call BEFORE the
            // matching `offset += renderedLength`. `styledStart` is the StyledText.Text
            // offset (part.Offset + the paragraph-local position).
            void Map(int styledStart, int renderedLength, int styledLength)
            {
                _indexMap.Add(new IndexSegment(offset, styledStart, renderedLength, styledLength));
            }

            // Records the segment of an inline object (custom emoji, math image, button), which
            // renders as a container taking no index plus the ZWNJ standing in for it.
            //
            // Rendered indices sit BETWEEN characters, so the one that addresses the object's
            // leading edge belongs to the zero-width character in FRONT of it - the mark emitted
            // just above, or the ZWNJ trailing the object right before - and that is where the
            // object's source text has to map. Its own ZWNJ then only marks the edge past it.
            // With no such character in front (an object in the middle of text) the ZWNJ carries
            // it, and the object can only be selected along with the character before it.
            void MapObject(int styledStart, int styledLength)
            {
                var last = _indexMap.Count - 1;
                if (last >= 0 && _indexMap[last].StyledLength == 0 && _indexMap[last].Rendered == offset - 1)
                {
                    _indexMap[last] = new IndexSegment(offset - 1, styledStart, 1, styledLength);
                    Map(styledStart + styledLength, 1, 0);
                }
                else
                {
                    Map(styledStart, 1, styledLength);
                }
            }

            // No return between here and the matching Tally, or the scope would be closed by
            // whatever runs next and charge its time to this label.

            for (int i = _first; i <= _last; i++)
            {
                var part = styled.Paragraphs[i];
                var text = part.Text;
                var type = part.Type;
                var runs = part.Runs;
                var partFontSize = fontSize;

                var previous = 0;

                // The ZWNJ an inline object leaves behind, not yet emitted: if plain text follows
                // it rides on the front of that run instead of taking a Run of its own. Every
                // iteration of the entity loop below either merges it or emits it, so it can never
                // reach a branch that does not know about it - and it never crosses a paragraph,
                // where it would move a content unit into the next block.
                string pending = null;

                IXamlDirectObject paragraph;
                IXamlDirectObject inlines;
                if (_spanForInlines != null)
                {
                    paragraph = null;
                    inlines = direct.GetXamlDirectObjectProperty(direct.GetXamlDirectObject(_spanForInlines), XamlPropertyIndex.Span_Inlines);
                }
                else
                {
                    paragraph = GetOrCreateParagraph(direct);

                    inlines = direct.GetXamlDirectObjectProperty(paragraph, XamlPropertyIndex.Paragraph_Inlines);
                }

                // TODO: we use DetectFromContent, but this could be used too:
                //direct.SetEnumProperty(paragraph, XamlPropertyIndex.Block_TextAlignment, part.Direction switch
                //{
                //    TextDirectionality.LeftToRight => (uint)TextAlignment.Left,
                //    TextDirectionality.RightToLeft => (uint)TextAlignment.Right,
                //    _ => (uint)TextAlignment.DetectFromContent
                //});

                if (alignment == TextAlignment.Center && paragraph != null)
                {
                    direct.SetEnumProperty(paragraph, XamlPropertyIndex.Block_TextAlignment, (uint)alignment);
                }

                var direction = paragraph == null ? locale : part.Direction switch
                {
                    TextDirectionality.LeftToRight => FlowDirection.LeftToRight,
                    TextDirectionality.RightToLeft => FlowDirection.RightToLeft,
                    _ => locale
                };

                if (part.Type is TextParagraphTypeQuote quote && paragraph != null)
                {
                    // TODO: quotes in RichMessage use normal font size, quotes in formatted text small
                    // decide what of the two we want to keep.
                    direct.SetDoubleProperty(paragraph, XamlPropertyIndex.TextElement_FontSize, AppSettings.Appearance.CaptionFontSize);
                    partFontSize = AppSettings.Appearance.CaptionFontSize;
                }

                for (int j = 0; j < runs.Count; j++)
                {
                    var entity = runs[j];
                    if (entity.Offset > previous)
                    {
                        GetOrCreateRun(direct, inlines, text, previous, entity.Offset - previous, direction, Native.TextStyle.None, null, fontSize: partFontSize, prefix: pending);
                        Map(part.Offset + previous, entity.Offset - previous, entity.Offset - previous);
                        offset += entity.Offset - previous;
                        pending = null;
                    }
                    else if (pending != null)
                    {
                        // What follows is an object or a styled run, neither of which may carry it.
                        GetOrCreateRun(direct, inlines, pending, direction, Native.TextStyle.None, null, partFontSize);
                        pending = null;
                    }

                    if (entity.Length + entity.Offset > text.Length)
                    {
                        previous = entity.Offset + entity.Length;
                        continue;
                    }

                    if (entity.HasFlag(Native.TextStyle.Monospace))
                    {
                        var data = text.Substring(entity.Offset, entity.Length);
                        if (paragraph != null)
                        {
                            if (entity.Type is not TextEntityTypePre and not TextEntityTypePreCode)
                            {
                                var hyperlink = GetOrCreateHyperlink(direct);
                                ApplyHyperlinkProperties(direct, hyperlink, CodeForeground, UnderlineStyle.None, FontWeights.Normal);

                                MessageHelper.SetHyperlinkInfo(hyperlink.Element, new TextEntityClickEventArgs(entity.Type, data));

                                GetOrCreateRun(direct, hyperlink.Inlines, data, direction, Native.TextStyle.None, Theme.MonospaceFontFamily, partFontSize);
                                Map(part.Offset + entity.Offset, data.Length, data.Length);
                                offset += data.Length;

                                direct.AddToCollection(inlines, hyperlink.Native);
                            }
                            else
                            {
                                direct.SetObjectProperty(paragraph, XamlPropertyIndex.TextElement_FontFamily, Theme.MonospaceFontFamily);

                                var placeholder = GetOrCreateRun(direct, inlines, data, direction, Native.TextStyle.None, null, 0);
                                Map(part.Offset + entity.Offset, data.Length, data.Length);
                                offset += data.Length;

                                preformatted = true;

                                if (entity.Type is TextEntityTypePreCode preCode && preCode.Language.Length > 0)
                                {
                                    ProcessCodeBlock(direct, inlines, placeholder, data, preCode.Language, generation);
                                }
                            }
                        }
                        else
                        {
                            GetOrCreateRun(direct, inlines, data, direction, Native.TextStyle.None, Theme.MonospaceFontFamily, 0);
                            Map(part.Offset + entity.Offset, data.Length, data.Length);
                            offset += data.Length;
                        }
                    }
                    else
                    {
                        IXamlDirectObject parent = null;
                        IXamlDirectObject parentInlines = inlines;

                        // A spoiler's rendered length is whatever its content turns out to
                        // occupy - a date inside it renders longer than the source it came
                        // from - so the range is written now and measured at the end.
                        var spoilerRange = -1;
                        var spoilerStart = offset;

                        if (paragraph != null)
                        {
                            if (_ignoreSpoilers is false && entity.HasFlag(Native.TextStyle.Spoiler))
                            {
                                var span = GetOrCreateSpan(direct);
                                direct.SetObjectProperty(span, XamlPropertyIndex.TextElement_Foreground, null);
                                direct.SetObjectProperty(span, XamlPropertyIndex.TextElement_FontFamily, BootStrapper.Current.Resources["SpoilerFontFamily"] as FontFamily);

                                (_spoilers ??= new List<TextStyleSpoiler>()).Add(new TextStyleSpoiler(entity.Offset, entity.Length, i - _first));

                                spoiler ??= new TextHighlighter();
                                spoilerRange = spoiler.Ranges.Count;
                                spoiler.Ranges.Add(new TextRange { StartIndex = offset, Length = entity.Length });

                                parent = span;
                                parentInlines = direct.GetXamlDirectObjectProperty(parent, XamlPropertyIndex.Span_Inlines);
                            }
                            else if ((entity.HasFlag(Native.TextStyle.Mention) || entity.HasFlag(Native.TextStyle.Url)))
                            {
                                if (entity.Type is TextEntityTypeMentionName or TextEntityTypeTextUrl)
                                {
                                    var hyperlink = GetOrCreateHyperlink(direct);
                                    if (entity.Type is TextEntityTypeTextUrl textUrl)
                                    {
                                        MessageHelper.SetHyperlinkInfo(hyperlink.Element, new TextEntityClickEventArgs(entity.Type, textUrl.Url));

                                        if (textUrl.Url.StartsWith("http"))
                                        {
                                            (_links ??= new List<Hyperlink>()).Add(hyperlink.Element);
                                            ToolTipService.SetToolTip(hyperlink.Element, textUrl.Url);
                                        }
                                    }
                                    else
                                    {
                                        MessageHelper.SetHyperlinkInfo(hyperlink.Element, new TextEntityClickEventArgs(entity.Type));
                                    }

                                    ApplyHyperlinkProperties(direct, hyperlink, HyperlinkForeground, UnderlineStyle.None, HyperlinkFontWeight);

                                    parent = hyperlink.Native;
                                    parentInlines = hyperlink.Inlines;
                                }
                                else
                                {
                                    var hyperlink = GetOrCreateHyperlink(direct);

                                    var data = text.Substring(entity.Offset, entity.Length);

                                    ApplyHyperlinkProperties(direct, hyperlink, HyperlinkForeground,
                                        entity.Type is TextEntityTypeUrl ? UnderlineStyle.Single : UnderlineStyle.None,
                                        HyperlinkFontWeight);

                                    if (entity.Type is TextEntityTypeDateTime dateTime)
                                    {
                                        (_links ??= new List<Hyperlink>()).Add(hyperlink.Element);
                                        ToolTipService.SetToolTip(hyperlink.Element, Formatter.LongDateAt(dateTime.UnixTime));
                                    }

                                    MessageHelper.SetHyperlinkInfo(hyperlink.Element, new TextEntityClickEventArgs(entity.Type, data));

                                    parent = hyperlink.Native;
                                    parentInlines = hyperlink.Inlines;
                                }
                            }
                        }
                        else if (_ignoreSpoilers is false && entity.HasFlag(Native.TextStyle.Spoiler))
                        {
                            var span = GetOrCreateSpan(direct);
                            direct.SetObjectProperty(span, XamlPropertyIndex.TextElement_Foreground, null);
                            direct.SetObjectProperty(span, XamlPropertyIndex.TextElement_FontFamily, BootStrapper.Current.Resources["SpoilerFontFamily"] as FontFamily);

                            (_spoilers ??= new List<TextStyleSpoiler>()).Add(new TextStyleSpoiler(entity.Offset, entity.Length, i - _first));

                            if (textOffset == -1)
                            {
                                textOffset = _spanForInlines.ContentStart.OffsetToIndex(TextBlock);
                            }

                            spoiler ??= new TextHighlighter();
                            spoilerRange = spoiler.Ranges.Count;
                            spoiler.Ranges.Add(new TextRange { StartIndex = textOffset + offset, Length = entity.Length });

                            parent = span;
                            parentInlines = direct.GetXamlDirectObjectProperty(span, XamlPropertyIndex.Span_Inlines);
                        }

                        if (_spanForInlines == null && entity.HasFlag(TextStyle.Marked))
                        {
                            marked ??= new TextHighlighter();
                            marked.Ranges.Add(new TextRange { StartIndex = offset, Length = entity.Length });
                        }

                        if (_spanForInlines == null && entity.HasFlag(TextStyle.Cached))
                        {
                            cached ??= new TextHighlighter();
                            cached.Ranges.Add(new TextRange { StartIndex = offset, Length = entity.Length });
                        }

                        // Consumes local inlines instead of paragraph's
                        // TODO: still use a InlineUIContainer for emojis in spoilers to avoid text resizes
                        // !!!
                        if (entity.Type is TextEntityTypeCustomEmoji customEmoji /*&& ((_ignoreSpoilers && entity.HasFlag(Native.TextStyle.Spoiler)) || !entity.HasFlag(Native.TextStyle.Spoiler))*/)
                        {
                            var data = text.Substring(entity.Offset, entity.Length);

                            InlineUIContainer inline;
                            if (customEmoji.CustomEmojiId == -1)
                            {
                                var block = new TextBlock
                                {
                                    Text = data,
                                    FontSize = 16,
                                    FontFamily = BootStrapper.Current.Resources["SymbolThemeFontFamily"] as FontFamily,
                                    Margin = new Thickness(0, 0, 0, -4)
                                };

                                inline = new InlineUIContainer
                                {
                                    Child = new Border
                                    {
                                        Child = block
                                    }
                                };

                                // TODO: this isn't going to be updated on theme changes
                                block.Foreground = IconForeground;
                            }
                            else
                            {
                                var player = GetOrCreateEmoji(out inline);

                                player.LoopCount = 0;
                                player.HorizontalAlignment = HorizontalAlignment.Left;
                                player.FlowDirection = FlowDirection.LeftToRight;
                                player.ReplacementColor = IconForeground;
                                player.IsHitTestVisible = false;
                                player.IsEnabled = false;
                                player.IsViewportAware = false;
                                player.Emoji = data;

                                if ((_ignoreSpoilers && entity.HasFlag(Native.TextStyle.Spoiler)) || !entity.HasFlag(Native.TextStyle.Spoiler))
                                {
                                    player.Source = new CustomEmojiFileSource(clientService, customEmoji.CustomEmojiId);
                                }
                                else
                                {
                                    player.Source = null;
                                }

                                if (_effectiveViewportChanged == null)
                                {
                                    _effectiveViewportChanged = new();
                                    RegisterViewportChanged();
                                }

                                _effectiveViewportChanged.Add(player);

                                if (autoFontSize != 0)
                                {
                                    player.Width = autoFontSize * (20d / 14d);
                                    player.Height = autoFontSize * (20d / 14d);
                                    player.Margin = new Thickness(0, -2 * (20d / 14d), 0, -6 * (20d / 14d));
                                    player.FrameSize = new Size(autoFontSize * (20d / 14d), autoFontSize * (20d / 14d));
                                }
                                else if (xamlFontSize == 14)
                                {
                                    player.Width = 20;
                                    player.Height = 20;
                                    player.Margin = new Thickness(0, -2, 0, -6);
                                    player.FrameSize = new Size(20, 20);
                                }
                                else if (xamlFontSize == 12)
                                {
                                    player.Margin = new Thickness(0, 0, 0, -4);
                                    player.Width = 16;
                                    player.Height = 16;
                                    player.FrameSize = new Size(16, 16);
                                }
                            }

                            // We are working around multiple issues here:
                            // ZWNJ is always added right after a custom emoji to make sure that the line height always matches Segoe UI.
                            // RTL/LTR mark is added in case the custom emoji is the first element in the Paragraph.
                            // This is needed because we can't use TextReadingOrder = DetectFromContent due to a bug
                            // that causes text selection and hit tests to follow the flow direction rather than the reading order.
                            // Because of this, we're forced to use TextReadingOrder = UseFlowDirection, and to set each
                            // Run.FlowDirection to the one calculated by calling GetStringTypeEx on the text of each paragraph.
                            // Since InlineUIContainer doesn't have a FlowDirection property (and the child flow direction seems to be ignored)
                            // the first custom emoji in a paragraph with reading order different from the one of the app, would appear on the
                            // wrong side of the block, thus we add a RTL/LTR mark right before, and the RichTextBlock seems to respect this.
                            // Additionally, we need to prepend a ZWNJ character if:
                            // - the paragraph begins by an emoji, to prevent early text trimming in inline mode
                            // - the emoji is preceded by a spoiler, to prevent text highlight to run over the emoji

                            if (entity.Offset == 0 || (entity.Offset == previous && runs[j - 1].HasFlag(Native.TextStyle.Spoiler)))
                            {
                                var character = direction != locale
                                    ? direction == FlowDirection.RightToLeft ? Icons.RTL : Icons.LTR
                                    : Icons.ZWNJ;

                                GetOrCreateRun(direct, inlines, character, direction, Native.TextStyle.None, null, fontSize: partFontSize);
                                Map(part.Offset + entity.Offset, 1, 0); // leading mark, carries the object below
                                offset++;
                            }

                            direct.AddToCollection(inlines, direct.GetXamlDirectObject(inline));
                            pending = Icons.ZWNJ;
                            MapObject(part.Offset + entity.Offset, entity.Length); // alt-text
                            offset++;
                        }
                        else if (entity.Type is TextEntityTypeDateTime date && date.FormattingType != null)
                        {
                            entity.Update(part);

                            var run = GetOrCreateRun(direct, parentInlines, entity.FormattedText, direction, entity.Flags, null, partFontSize);
                            Map(part.Offset + entity.Offset, entity.FormattedText.Length, entity.Length); // displayed date <-> original
                            offset += entity.FormattedText.Length;

                            if (date.FormattingType is DateTimeFormattingTypeRelative)
                            {
                                (_dates ??= new List<IXamlDirectObject>()).Add(run);

                                // Map was called for this date immediately above, so its segment
                                // is the last one - that is what a tick has to shift from.
                                RelativeDateService.Subscribe(run, this, part, entity, date, _indexMap.Count - 1);
                            }
                        }
                        else if (_spanForInlines == null && entity.Type is TextEntityTypeMathematicalExpression mathematicalExpression)
                        {
                            var tex = new RichMathImage
                            {
                                Source = mathematicalExpression.Expression
                            };

                            if (tex.IsValid)
                            {
                                TextBlock.MinHeight = Math.Max(TextBlock.MinHeight, tex.PixelHeight);
                                tex.Margin = new Thickness(0, 0, 0, tex.Baseline * tex.PixelHeight - tex.PixelHeight);

                                var inline = new InlineUIContainer
                                {
                                    Child = tex
                                };

                                if (entity.Offset == 0 || (entity.Offset == previous && runs[j - 1].HasFlag(Native.TextStyle.Spoiler)))
                                {
                                    var character = direction != locale
                                        ? direction == FlowDirection.RightToLeft ? Icons.RTL : Icons.LTR
                                        : Icons.ZWNJ;

                                    GetOrCreateRun(direct, inlines, character, direction, Native.TextStyle.None, null, fontSize: partFontSize);
                                    Map(part.Offset + entity.Offset, 1, 0); // leading mark, carries the object below
                                    offset++;
                                }

                                direct.AddToCollection(inlines, direct.GetXamlDirectObject(inline));
                                GetOrCreateRun(direct, inlines, Icons.ZWNJ, direction, Native.TextStyle.None, null, partFontSize);
                                MapObject(part.Offset + entity.Offset, entity.Length); // expression
                                offset++;
                            }
                            else
                            {
                                GetOrCreateRun(direct, parentInlines, mathematicalExpression.Expression, entity.Offset, entity.Length, direction, entity.Flags, null, partFontSize);
                                Map(part.Offset + entity.Offset, entity.Length, entity.Length);
                                offset += entity.Length;
                            }

                        }
                        else if (_spanForInlines == null && entity.Type is TextEntityTypeButton button)
                        {
                            var inline = new InlineUIContainer
                            {
                                Child = CreateInlineButton(clientService, button.Button)
                            };

                            if (entity.Offset == 0 || (entity.Offset == previous && runs[j - 1].HasFlag(Native.TextStyle.Spoiler)))
                            {
                                var character = direction != locale
                                    ? direction == FlowDirection.RightToLeft ? Icons.RTL : Icons.LTR
                                    : Icons.ZWNJ;

                                GetOrCreateRun(direct, inlines, character, direction, Native.TextStyle.None, null, fontSize: partFontSize);
                                Map(part.Offset + entity.Offset, 1, 0); // leading mark, carries the object below
                                offset++;
                            }

                            direct.AddToCollection(inlines, direct.GetXamlDirectObject(inline));
                            GetOrCreateRun(direct, inlines, Icons.ZWNJ, direction, Native.TextStyle.None, null, partFontSize);
                            MapObject(part.Offset + entity.Offset, entity.Length); // button text
                            offset++;
                        }
                        else if (_spanForInlines == null && entity.Type is TextEntityTypeIcon icon)
                        {
                            // TODO
                        }
                        else
                        {
                            GetOrCreateRun(direct, parentInlines, text, entity.Offset, entity.Length, direction, entity.Flags, null, partFontSize);
                            Map(part.Offset + entity.Offset, entity.Length, entity.Length);
                            offset += entity.Length;
                        }

                        if (spoilerRange >= 0 && offset > spoilerStart)
                        {
                            var range = spoiler.Ranges[spoilerRange];
                            spoiler.Ranges[spoilerRange] = new TextRange { StartIndex = range.StartIndex, Length = offset - spoilerStart };
                        }

                        if (parent != null)
                        {
                            direct.AddToCollection(inlines, parent);
                        }
                    }

                    previous = entity.Offset + entity.Length;
                }

                if (text.Length > previous)
                {
                    _fastRun = GetOrCreateRun(direct, inlines, text, previous, text.Length - previous, direction, Native.TextStyle.None, null, partFontSize, prefix: pending);
                    Map(part.Offset + previous, text.Length - previous, text.Length - previous);
                    offset += text.Length - previous;
                    pending = null;
                }
                else if (pending != null)
                {
                    GetOrCreateRun(direct, inlines, pending, direction, Native.TextStyle.None, null, partFontSize);
                    pending = null;
                }

                if (paragraph != null)
                {
                    direct.AddToCollection(blocks, paragraph);
                }
                else if (i < _last)
                {
                    GetOrCreateRun(direct, inlines, " ", direction, Native.TextStyle.None, null, 0);
                    offset++;
                }

                if (i == _first)
                {
                    firstType = type;
                }

                lastType = type;
            }

            //Padding = new Thickness(0, firstFormatted ? 4 : 0, 0, 0);

            //ContentPanel.MaxWidth = preformatted ? double.PositiveInfinity : 432;

            //_isFormatted = runs.Count > 0 || fontSize != 0;
            HasCodeBlocks = preformatted;

            var spoilerChanged = (_spoiler != null) || (spoiler != null);
            if (spoiler?.Ranges.Count > 0)
            {
                spoiler.Foreground = new SolidColorBrush(Colors.Transparent);
                spoiler.Background = new SolidColorBrush(Colors.Transparent);

                _spoiler = spoiler;
            }
            else
            {
                _spoiler = null;
            }

            if (cached?.Ranges.Count > 0)
            {
                var accent = ActualTheme == ElementTheme.Light
                    ? Theme.AccentLight.Default
                    : Theme.AccentDark.Default;

                cached.Background = new SolidColorBrush(accent.WithAlpha(22));
                cached.Foreground = new SolidColorBrush(accent);

                _cached = cached;
            }
            else
            {
                _cached = null;
            }

            if (marked?.Ranges.Count > 0)
            {
                marked.Background = new SolidColorBrush(Colors.PaleGoldenrod);

                _marked = marked;
            }
            else
            {
                _marked = null;
            }

            // TODO: get rid of _spoiler

            // Apply the spoiler/cached/marked highlighters just produced (plus the
            // current query). Callers no longer have to follow SetText with SetQuery.
            ApplyHighlighters();

            var bottomPadding = false;

            if (_spanForInlines == null)
            {
                if (AdjustLineEnding && _last >= _first)
                {
                    var direction = styled.Paragraphs[_last].Direction switch
                    {
                        TextDirectionality.LeftToRight => FlowDirection.LeftToRight,
                        TextDirectionality.RightToLeft => FlowDirection.RightToLeft,
                        _ => locale
                    };

                    if (direction != locale || lastType is not null)
                    {
                        bottomPadding = true;
                    }
                }
            }

            HasLineEnding = bottomPadding;

            if (spoilerChanged || !_skeletonCollapsed)
            {
                RegisterLayoutChanged();
            }
        }

        private UIElement CreateInlineButton(IClientService clientService, InlineButton button)
        {
            var element = new ReplyMarkupInlineButton
            {
                Tag = button,
                Padding = new Thickness(4, 0, 4, 0),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(10),
                Height = 20,
                MinWidth = 20,
            };

            element.SetButton(clientService, null, 0, button.Style, button.Type);
            element.Click += InlineButton_Click;

            if (button.Text is RichTextPlain plain)
            {
                element.Content = plain.Text;
            }
            else
            {
                var block = new FormattedTextBlock
                {
                    AutoFontSize = true,
                    IgnoreSpoilers = false,
                    HorizontalTextAlignment = TextAlignment.DetectFromContent,
                    TextReadingOrder = TextReadingOrder.UseFlowDirection,
                    TextSelection = TextSelectionMode.Disabled,
                    AdjustLineEnding = false,
                };

                block.IconForeground = element.Foreground;
                block.SetText(clientService, button.Text);

                element.Content = block;
            }

            // The margin belongs out here rather than on the button: it lets the button hang
            // below its line box either way, but on the button it would shrink the wrapper to
            // less than what it renders, leaving that strip outside the background below.
            var wrapper = new Border
            {
                Child = element,
                Margin = new Thickness(0, 0, 0, -4)
            };

            // A disabled control takes no pointer input, so the pointer falls through to the
            // text behind the button and the cursor reads as an I-beam over it. A background
            // makes the wrapper the target instead — see GetInlineButtonFromSource.
            if (!element.IsEnabled)
            {
                wrapper.Background = new SolidColorBrush(Colors.Transparent);
            }

            return wrapper;
        }

        private void InlineButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ReplyMarkupInlineButton { Tag: InlineButton button })
            {
                TextEntityClick?.Invoke(this, new TextEntityClickEventArgs(new TextEntityTypeButton(button)));
            }
        }

        private HashSet<CustomEmojiIcon> _effectiveViewportChanged;

        protected override void OnViewportChanged(double left, double top, double right, double bottom)
        {
            if (_effectiveViewportChanged == null)
            {
                UnregisterViewportChanged();
                return;
            }

            foreach (var child in _effectiveViewportChanged)
            {
                bool intersects =
                    child.ActualOffset.X + child.ActualSize.X > left &&
                    child.ActualOffset.X < right &&
                    child.ActualOffset.Y + child.ActualSize.Y > top &&
                    child.ActualOffset.Y < bottom;

                child.ViewportChanged(intersects);
            }
        }

        protected override void OnLayoutUpdated()
        {
            UpdateSpoilers();

            if (!_skeletonCollapsed && _text != null)
            {
                InvalidateSkeleton();
            }
        }

        // A relative date rewrote itself, so the rendered space grew or shrank by `delta` at the
        // end of `segment`. Everything downstream is expressed in that space and has to move
        // with it: the index map the selection layer reads, and the highlighter ranges. Without
        // this, one tick's worth of characters separates what is copied from what is shown, and
        // the spoiler cover slides off its text.
        private void ShiftRenderedSpace(int segment, int delta)
        {
            var map = _indexMap;
            if (map == null || segment < 0 || segment >= map.Count)
            {
                return;
            }

            var date = map[segment];
            var from = date.Rendered + date.RenderedLength;

            map[segment] = new IndexSegment(date.Rendered, date.Styled, date.RenderedLength + delta, date.StyledLength);

            for (int i = segment + 1; i < map.Count; i++)
            {
                var next = map[i];
                map[i] = new IndexSegment(next.Rendered + delta, next.Styled, next.RenderedLength, next.StyledLength);
            }

            // The date's Run changed length, so every TextPointer offset after it moved.
            InvalidateContentLength();

            ShiftRanges(_spoiler, from, delta);
            ShiftRanges(_marked, from, delta);
            ShiftRanges(_cached, from, delta);

            // A TextHighlighter does not repaint when its ranges change under it, and the query
            // range is recomputed from the map this just fixed, so rebuild the lot.
            ApplyHighlighters();
        }

        private static void ShiftRanges(TextHighlighter highlighter, int from, int delta)
        {
            var ranges = highlighter?.Ranges;

            for (int i = 0; i < ranges?.Count; i++)
            {
                var range = ranges[i];

                if (range.StartIndex >= from)
                {
                    ranges[i] = new TextRange { StartIndex = range.StartIndex + delta, Length = range.Length };
                }
                else if (range.StartIndex + range.Length >= from)
                {
                    // The date is inside this range - a spoiler wrapping it - so it stretches
                    // rather than moves.
                    ranges[i] = new TextRange { StartIndex = range.StartIndex, Length = range.Length + delta };
                }
            }
        }

        // A spoiler's range in the paragraph's DISPLAYED text: its source range, plus the growth
        // of every relative date that has been rewritten. Dates before it push it along; a date
        // inside it stretches it. Derived rather than stored, so any number of dates updating in
        // any order lands in the same place.
        private static void DisplayedRange(StyledParagraph styled, TextStyleSpoiler spoiler, out int offset, out int length)
        {
            var before = 0;
            var inside = 0;

            var runs = styled.Runs;
            for (int i = 0; i < runs.Count; i++)
            {
                var run = runs[i];

                // Mirrors what SetText renders as a date. A date with no FormattingType is drawn
                // from its source text and never gets a FormattedText, so it displaces nothing -
                // and FormattedText starts as an empty string rather than null, which would read
                // as shrinking the paragraph to nothing.
                if (run.Type is not TextEntityTypeDateTime { FormattingType: not null } || string.IsNullOrEmpty(run.FormattedText))
                {
                    continue;
                }

                var growth = run.FormattedText.Length - run.Length;
                if (run.Offset + run.Length <= spoiler.Offset)
                {
                    before += growth;
                }
                else if (run.Offset >= spoiler.Offset && run.Offset + run.Length <= spoiler.Offset + spoiler.Length)
                {
                    inside += growth;
                }
            }

            offset = spoiler.Offset + before;
            length = spoiler.Length + inside;
        }

        private void UpdateSpoilers()
        {
            if (_ignoreSpoilers || _spoilers == null || _spoilers.Count == 0)
            {
                if (_spoilerPresenter != null)
                {
                    Below.Children.Remove(_spoilerPresenter);
                    _spoilerPresenter = null;
                    _spoilerGeometry = null;
                    _spoilerAdded = false;
                }

                return;
            }

            var fontSize = (AutoFontSize ? AppSettings.Appearance.MessageFontSize : TextBlock.FontSize) * BootStrapper.Current.TextScaleFactor;
            var quoteSize = (AutoFontSize ? AppSettings.Appearance.CaptionFontSize : TextBlock.FontSize) * BootStrapper.Current.TextScaleFactor;

            var width = LastAvailableWidth;

            var position = new Windows.Foundation.Point(0, 0);

            var shapes = new List<List<Rect>>();
            var current = new List<Rect>();
            var last = default(Rect);

            var minX = double.MaxValue;
            var minY = double.MaxValue;
            var maxX = double.MinValue;
            var maxY = double.MinValue;

            if (_spanForInlines == null)
            {
                // Would be cool to optimize this for contiguous paragraphs
                foreach (var spoiler in _spoilers)
                {
                    StyledParagraph styled = _text.Paragraphs[_first + spoiler.ParagraphIndex];
                    Paragraph paragraph = TextBlock.Blocks[spoiler.ParagraphIndex] as Paragraph;

                    if (paragraph == null)
                    {
                        // TODO: figure out why this happens
                        continue;
                    }

                    // GetParts hands back the date-expanded text, so the range has to be in that
                    // space too.
                    DisplayedRange(styled, spoiler, out int xoffset, out int xlength);

                    var entities = styled.GetParts(out var partial) ?? TextStyleRun.NoParts;

                    var size = styled.Type is TextParagraphTypeQuote
                        ? quoteSize
                        : fontSize;

                    var rectangles = Direct2D.Current.RangeMetrics(partial, xoffset, xlength, entities, size, width - paragraph.Margin.Left - paragraph.Margin.Right, styled.Direction == TextDirectionality.RightToLeft, true);
                    var relative = paragraph.ContentStart.GetCharacterRect(paragraph.ContentStart.LogicalDirection);

                    var point = new Windows.Foundation.Point(paragraph.Margin.Left + position.X, relative.Y + position.Y);

                    for (int i = 0; i < rectangles?.Count; i++)
                    {
                        var rect = rectangles[i];
                        rect = new Rect(rect.X, rect.Y, rect.Width, rect.Height);
                        rect.X += point.X;
                        rect.Y += point.Y;

                        if (current.Count > 0 && !rect.IntersectsOrTouches(last))
                        {
                            shapes.Add(current);
                            current = new List<Rect>();
                        }

                        current.Add(rect);
                        last = rect;

                        minX = Math.Min(minX, rect.Left);
                        minY = Math.Min(minY, rect.Top);
                        maxX = Math.Max(maxX, rect.Right);
                        maxY = Math.Max(maxY, rect.Bottom);
                    }
                }
            }
            else
            {
                var paragraph = TextBlock.Blocks[^1] as Paragraph;

                Rect relative;
                if (paragraph.Inlines.Count > 1)
                {
                    relative = paragraph.Inlines[^2].ContentEnd.GetCharacterRect(LogicalDirection.Forward);
                }
                else
                {
                    relative = paragraph.Inlines[^1].ContentStart.GetCharacterRect(LogicalDirection.Forward);
                }

                // Would be cool to optimize this for contiguous paragraphs
                foreach (var spoiler in _spoilers)
                {
                    StyledParagraph styled = _text.Paragraphs[_first + spoiler.ParagraphIndex];

                    // Measured against the raw text here, not the date-expanded one, so these
                    // stay source offsets - the mismatch the stored displayed offset used to
                    // introduce in this branch.
                    int xoffset = styled.Offset + spoiler.Offset;
                    int xlength = spoiler.Length;

                    var partial = _text.Text.Replace('\n', ' ');
                    var entities = _text.Parts;

                    var size = fontSize;

                    var rectangles = Direct2D.Current.RangeMetrics(partial, xoffset, xlength, entities, size, width - relative.X, styled.Direction == TextDirectionality.RightToLeft, false);
                    var point = new Windows.Foundation.Point(relative.X + position.X, relative.Y + position.Y);

                    for (int i = 0; i < rectangles?.Count; i++)
                    {
                        var rect = rectangles[i];
                        rect = new Rect(rect.X, rect.Y, rect.Width, rect.Height);
                        rect.X += point.X;
                        rect.Y += point.Y;

                        if (current.Count > 0 && !rect.IntersectsOrTouches(last))
                        {
                            shapes.Add(current);
                            current = new List<Rect>();
                        }

                        current.Add(rect);
                        last = rect;

                        minX = Math.Min(minX, rect.Left);
                        minY = Math.Min(minY, rect.Top);
                        maxX = Math.Max(maxX, rect.Right);
                        maxY = Math.Max(maxY, rect.Bottom);
                    }
                }
            }

            if (current.Count > 0)
            {
                shapes.Add(current);
            }

            if (maxX - minX <= 0 || maxY - minY <= 0)
            {
                if (_spoilerPresenter != null)
                {
                    Below.Children.Remove(_spoilerPresenter);
                    _spoilerPresenter = null;
                    _spoilerGeometry = null;
                    _spoilerAdded = false;
                }

                return;
            }

            using (var builder = new CanvasPathBuilder(null))
            {
                for (int j = 0; j < shapes.Count; j++)
                {
                    var rectangles = shapes[j];

                    for (int i = 0; i < rectangles.Count; i++)
                    {
                        var rectangle = rectangles[i];
                        rectangle.X -= minX;
                        rectangle.Y -= minY;

                        builder.AddGeometry(CanvasGeometry.CreateRectangle(null, rectangle));
                    }
                }

                _spoilerGeometry = CanvasGeometry.CreatePath(builder);
            }

            Color foreground = Colors.Black;
            if (Foreground is SolidColorBrush brush)
            {
                foreground = brush.Color;
            }

            if (_spoilerPresenter == null)
            {
                _spoilerPresenter = new AnimatedImage
                {
                    IsViewportAware = true,
                    FrameSize = new Size(0, 0),
                    ResizeMode = AnimatedImageResizeMode.Fill,
                    DecodeFrameType = DecodePixelType.Logical,
                    Stretch = Stretch.UniformToFill,
                    Source = new ParticlesImageSource(foreground),
                    Width = maxX - minX,
                    Height = maxY - minY
                };
            }
            else
            {
                _spoilerPresenter.Width = maxX - minX;
                _spoilerPresenter.Height = maxY - minY;
            }

            Canvas.SetLeft(_spoilerPresenter, minX);
            Canvas.SetTop(_spoilerPresenter, minY);

            if (!_spoilerAdded)
            {
                _spoilerAdded = true;
                Below.Children.Add(_spoilerPresenter);
            }

            var visual = ElementComposition.GetElementVisual(_spoilerPresenter);
            var geometry = visual.Compositor.CreatePathGeometry(new CompositionPath(_spoilerGeometry));
            visual.Clip = visual.Compositor.CreateGeometricClip(geometry);
        }

        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            // Subscribed for any pre block, but only a tokenized one ever built brushes.
            if (_brushes == null)
            {
                return;
            }

            var resources = sender.ActualTheme == ElementTheme.Light ? _light : _dark;

            foreach (var item in _brushes)
            {
                item.Value.Color = resources[item.Key];
            }
        }

        #region PreCode

        private async void ProcessCodeBlock(XamlDirect direct, IXamlDirectObject inlines, IXamlDirectObject placeholder, string text, string language, int generation)
        {
            try
            {
                var tokens = await SyntaxToken.TokenizeAsync(language.ToLowerInvariant(), text);

                // Only apply if we're still rendering the content this was started for:
                // `inlines` belongs to a Paragraph that Recycle may have handed to another
                // block by now, and ClearCollection would wipe whatever it holds instead.
                if (_generation == generation)
                {
                    // We need to manually recycle the Run or we'll lose track of it
                    if (_pools != null && _activeRuns.Contains(placeholder))
                    {
                        _pools.PutRun(placeholder);
                        _activeRuns.Remove(placeholder);
                    }

                    direct.ClearCollection(inlines);
                    ProcessCodeBlock(direct, inlines, tokens.Children);

                    // The inline tree changed (placeholder -> syntax spans); the cached
                    // selection length must be recomputed on next access.
                    InvalidateContentLength();
                }
            }
            catch
            {
                // Tokenization may fail
            }
        }

        private void ProcessCodeBlock(XamlDirect direct, IXamlDirectObject inlines, IList<Token> tokens)
        {
            // Recursive: a new FontFamily here was one per node of the token tree.
            var fontFamily = Theme.MonospaceFontFamily;

            foreach (var token in tokens)
            {
                if (token is SyntaxToken syntax)
                {
                    var color = GetColor(syntax.Type);
                    if (color == null && syntax.Alias.Length > 0)
                    {
                        color = GetColor(syntax.Alias);
                    }

                    var span = GetOrCreateSpan(direct);
                    var collection = direct.GetXamlDirectObjectProperty(span, XamlPropertyIndex.Span_Inlines);

                    direct.SetObjectProperty(span, XamlPropertyIndex.TextElement_FontFamily, fontFamily);

                    if (color != null)
                    {
                        direct.SetObjectProperty(span, XamlPropertyIndex.TextElement_Foreground, color);
                    }

                    if (syntax.Type == "bold")
                    {
                        direct.SetObjectProperty(span, XamlPropertyIndex.TextElement_FontWeight, FontWeights.SemiBold);
                    }
                    else if (syntax.Type == "italic")
                    {
                        direct.SetEnumProperty(span, XamlPropertyIndex.TextElement_FontStyle, (uint)FontStyle.Italic);
                    }

                    ProcessCodeBlock(direct, collection, syntax.Children);
                    direct.AddToCollection(inlines, span);
                }
                else if (token is TextToken text)
                {
                    GetOrCreateRun(direct, inlines, text.Value, FlowDirection.LeftToRight, Native.TextStyle.None, fontFamily, 0);
                }
            }
        }

        SolidColorBrush GetColor(string type)
        {
            _brushes ??= new Dictionary<string, SolidColorBrush>();

            if (_brushes.TryGetValue(type, out var brush))
            {
                return brush;
            }

            var target = ActualTheme == ElementTheme.Light ? _light : _dark;
            if (target.TryGetValue(type, out var color))
            {
                brush = new SolidColorBrush(color);
                _brushes[type] = brush;
                return brush;
            }

            return null;
        }

        // Static: the tables are constant and Color is a value type, so they cost nothing per
        // block — as instance fields they were two ~28-entry dictionaries on every text block
        // in the app, code or not. The brushes built from them can't be shared the same way
        // (a DependencyObject belongs to the thread that created it, and the app runs several).
        private static readonly Dictionary<string, Color> _light = new()
        {
            { "comment", Colors.SlateGray },
            { "block-comment", Colors.SlateGray },
            { "prolog", Colors.SlateGray },
            { "doctype", Colors.SlateGray },
            { "cdata", Colors.SlateGray },
            { "punctuation", Color.FromArgb(0xFF, 0x99, 0x99, 0x99) },
            { "property", Color.FromArgb(0xFF, 0x99, 0x00, 0x55) },
            { "tag", Color.FromArgb(0xFF, 0x99, 0x00, 0x55) },
            { "boolean", Color.FromArgb(0xFF, 0x99, 0x00, 0x55) },
            { "number", Color.FromArgb(0xFF, 0x99, 0x00, 0x55) },
            { "constant", Color.FromArgb(0xFF, 0x99, 0x00, 0x55) },
            { "symbol", Color.FromArgb(0xFF, 0x99, 0x00, 0x55) },
            { "deleted", Color.FromArgb(0xFF, 0x99, 0x00, 0x55) },
            { "selector", Color.FromArgb(0xFF, 0x66, 0x99, 0x00) },
            { "attr-name", Color.FromArgb(0xFF, 0x66, 0x99, 0x00) },
            { "string", Color.FromArgb(0xFF, 0x66, 0x99, 0x00) },
            { "char", Color.FromArgb(0xFF, 0x66, 0x99, 0x00) },
            { "builtin", Color.FromArgb(0xFF, 0x66, 0x99, 0x00) },
            { "inserted", Color.FromArgb(0xFF, 0x66, 0x99, 0x00) },
            { "operator", Color.FromArgb(0xFF, 0x9a, 0x6e, 0x3a) },
            { "entity", Color.FromArgb(0xFF, 0x9a, 0x6e, 0x3a) },
            { "url", Color.FromArgb(0xFF, 0x9a, 0x6e, 0x3a) },
            { "atrule", Color.FromArgb(0xFF, 0x00, 0x77, 0xAA) },
            { "attr-value", Color.FromArgb(0xFF, 0x00, 0x77, 0xAA) },
            { "keyword", Color.FromArgb(0xFF, 0x00, 0x77, 0xAA) },
            { "function", Color.FromArgb(0xFF, 0x00, 0x77, 0xAA) },
            { "class-name", Color.FromArgb(0xFF, 0xDD, 0x4A, 0x68) },
        };

        private static readonly Dictionary<string, Color> _dark = new()
        {
            { "comment", Color.FromArgb(0xFF, 0x99, 0x99, 0x99) },
            { "block-comment", Color.FromArgb(0xFF, 0x99, 0x99, 0x99) },
            { "prolog", Color.FromArgb(0xFF, 0x99, 0x99, 0x99) },
            { "doctype", Color.FromArgb(0xFF, 0x99, 0x99, 0x99) },
            { "cdata", Color.FromArgb(0xFF, 0x99, 0x99, 0x99) },
            { "punctuation", Color.FromArgb(0xFF, 0xCC, 0xCC, 0xCC) },
            { "property", Color.FromArgb(0xFF, 0xf8, 0xc5, 0x55) },
            { "tag", Color.FromArgb(0xFF, 0xe2, 0x77, 0x7a) },
            { "boolean", Color.FromArgb(0xFF, 0xf0, 0x8d, 0x49) },
            { "number", Color.FromArgb(0xFF, 0xf0, 0x8d, 0x49) },
            { "constant", Color.FromArgb(0xFF, 0xf8, 0xc5, 0x55) },
            { "symbol", Color.FromArgb(0xFF, 0xf8, 0xc5, 0x55) },
            { "deleted", Color.FromArgb(0xFF, 0xe2, 0x77, 0x7a) },
            { "selector", Color.FromArgb(0xFF, 0xcc, 0x99, 0xcd) },
            { "attr-name", Color.FromArgb(0xFF, 0xe2, 0x77, 0x7a) },
            { "string", Color.FromArgb(0xFF, 0x7e, 0xc6, 0x99) },
            { "char", Color.FromArgb(0xFF, 0x7e, 0xc6, 0x99) },
            { "builtin", Color.FromArgb(0xFF, 0xcc, 0x99, 0xcd) },
            { "inserted", Color.FromArgb(0xFF, 0x66, 0x99, 0x00) },
            { "operator", Color.FromArgb(0xFF, 0x67, 0xcd, 0xcc) },
            { "entity", Color.FromArgb(0xFF, 0x67, 0xcd, 0xcc) },
            { "url", Color.FromArgb(0xFF, 0x67, 0xcd, 0xcc) },
            { "atrule", Color.FromArgb(0xFF, 0xcc, 0x99, 0xcd) },
            { "attr-value", Color.FromArgb(0xFF, 0x7e, 0xc6, 0x99) },
            { "keyword", Color.FromArgb(0xFF, 0xcc, 0x99, 0xcd) },
            { "function", Color.FromArgb(0xFF, 0xf0, 0x8d, 0x49) },
            { "class-name", Color.FromArgb(0xFF, 0xf8, 0xc5, 0x55) },
            // namespace 0xe2, 0x77, 0x7a
            // function-name 6196cc
        };

        // Only code blocks ever reach GetColor, so this stays null for everything else.
        private Dictionary<string, SolidColorBrush> _brushes;

        #endregion

        private void Entity_Click(TextEntityClickEventArgs args)
        {
            args.Handled = false;
            TextEntityClick?.Invoke(this, args);

            if (args.Handled)
            {
                return;
            }

            if (args.Type is TextEntityTypeCode or TextEntityTypePre or TextEntityTypePreCode && args.Text is string code)
            {
                MessageHelper.CopyText(XamlRoot, code);
            }
            else if (args.Type is TextEntityTypeSpoiler)
            {
                IgnoreSpoilers = true;
            }

            // TODO: handle more cases internally
        }

        #region TextAlignment

        public TextAlignment TextAlignment
        {
            get { return (TextAlignment)GetValue(TextAlignmentProperty); }
            set { SetValue(TextAlignmentProperty, value); }
        }

        public static readonly DependencyProperty TextAlignmentProperty =
            DependencyProperty.Register("TextAlignment", typeof(TextAlignment), typeof(FormattedTextBlock), new PropertyMetadata(TextAlignment.Left));

        #endregion

        // IsTextSelectionEnabled / TextSelection (the TextSelectionMode enum + DP) live
        // in FormattedTextBlock.Selectable.cs.

        #region OverflowContentTarget

        public RichTextBlockOverflow OverflowContentTarget
        {
            get { return (RichTextBlockOverflow)GetValue(OverflowContentTargetProperty); }
            set { SetValue(OverflowContentTargetProperty, value); }
        }

        public static readonly DependencyProperty OverflowContentTargetProperty =
            DependencyProperty.Register("OverflowContentTarget", typeof(RichTextBlockOverflow), typeof(FormattedTextBlock), new PropertyMetadata(null));

        #endregion

        #region TextTrimming

        public TextTrimming TextTrimming
        {
            get { return (TextTrimming)GetValue(TextTrimmingProperty); }
            set { SetValue(TextTrimmingProperty, value); }
        }

        public static readonly DependencyProperty TextTrimmingProperty =
            DependencyProperty.Register("TextTrimming", typeof(TextTrimming), typeof(FormattedTextBlock), new PropertyMetadata(TextTrimming.None));

        #endregion

        #region TextWrapping

        public TextWrapping TextWrapping
        {
            get { return (TextWrapping)GetValue(TextWrappingProperty); }
            set { SetValue(TextWrappingProperty, value); }
        }

        public static readonly DependencyProperty TextWrappingProperty =
            DependencyProperty.Register("TextWrapping", typeof(TextWrapping), typeof(FormattedTextBlock), new PropertyMetadata(TextWrapping.Wrap));

        #endregion

        #region HorizontalTextAlignment

        public TextAlignment HorizontalTextAlignment
        {
            get { return (TextAlignment)GetValue(HorizontalTextAlignmentProperty); }
            set { SetValue(HorizontalTextAlignmentProperty, value); }
        }

        public static readonly DependencyProperty HorizontalTextAlignmentProperty =
            DependencyProperty.Register("HorizontalTextAlignment", typeof(TextAlignment), typeof(FormattedTextBlock), new PropertyMetadata(TextAlignment.Left));

        #endregion

        #region TextReadingOrder

        public TextReadingOrder TextReadingOrder
        {
            get { return (TextReadingOrder)GetValue(TextReadingOrderProperty); }
            set { SetValue(TextReadingOrderProperty, value); }
        }

        public static readonly DependencyProperty TextReadingOrderProperty =
            DependencyProperty.Register("TextReadingOrder", typeof(TextReadingOrder), typeof(FormattedTextBlock), new PropertyMetadata(TextReadingOrder.UseFlowDirection));

        #endregion

        public TextDecorations TextDecorations
        {
            get { return (TextDecorations)GetValue(TextDecorationsProperty); }
            set { SetValue(TextDecorationsProperty, value); }
        }

        public static readonly DependencyProperty TextDecorationsProperty =
            DependencyProperty.Register("TextDecorations", typeof(TextDecorations), typeof(FormattedTextBlock), new PropertyMetadata(TextDecorations.None));

        #region TextDecorations

        #endregion

        #region MaxLines

        public int MaxLines
        {
            get { return (int)GetValue(MaxLinesProperty); }
            set { SetValue(MaxLinesProperty, value); }
        }

        public static readonly DependencyProperty MaxLinesProperty =
            DependencyProperty.Register("MaxLines", typeof(int), typeof(FormattedTextBlock), new PropertyMetadata(0));

        #endregion

        #region Hyperlink

        public bool AutoFontSize { get; set; } = true;

        public UnderlineStyle HyperlinkStyle { get; set; } = UnderlineStyle.Single;

        public FontWeight HyperlinkFontWeight { get; set; } = FontWeights.Normal;

        #endregion

        #region HyperlinkForeground

        public Brush HyperlinkForeground
        {
            get { return (Brush)GetValue(HyperlinkForegroundProperty); }
            set { SetValue(HyperlinkForegroundProperty, value); }
        }

        public static readonly DependencyProperty HyperlinkForegroundProperty =
            DependencyProperty.Register("HyperlinkForeground", typeof(Brush), typeof(FormattedTextBlock), new PropertyMetadata(null, OnHyperlinkForegroundChanged));

        private static void OnHyperlinkForegroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((FormattedTextBlock)d).RecolorHyperlinks((Brush)e.NewValue, (Brush)e.OldValue);
        }

        // Both foregrounds land on Hyperlinks and the code ones are not tracked separately, so
        // this recolours by identity: only the links still carrying the outgoing brush. If the
        // two properties were ever set to the same Brush instance, one change would move both.
        private void RecolorHyperlinks(Brush newValue, Brush oldValue)
        {
            foreach (var child in _activeHyperlinks)
            {
                if (child.Element.Foreground == oldValue)
                {
                    child.Element.Foreground = newValue;
                }
            }
        }

        #endregion

        #region CodeForeground

        public Brush CodeForeground
        {
            get { return (Brush)GetValue(CodeForegroundProperty); }
            set { SetValue(CodeForegroundProperty, value); }
        }

        public static readonly DependencyProperty CodeForegroundProperty =
            DependencyProperty.Register("CodeForeground", typeof(Brush), typeof(FormattedTextBlock), new PropertyMetadata(null, OnCodeForegroundChanged));

        private static void OnCodeForegroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((FormattedTextBlock)d).RecolorHyperlinks((Brush)e.NewValue, (Brush)e.OldValue);
        }

        #endregion

        #region IconForeground

        public Brush IconForeground
        {
            get { return (Brush)GetValue(IconForegroundProperty); }
            set { SetValue(IconForegroundProperty, value); }
        }

        public static readonly DependencyProperty IconForegroundProperty =
            DependencyProperty.Register("IconForeground", typeof(Brush), typeof(FormattedTextBlock), new PropertyMetadata(null, OnIconForegroundChanged));

        private static void OnIconForegroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((FormattedTextBlock)d).OnIconForegroundChanged((Brush)e.NewValue);
        }

        private void OnIconForegroundChanged(Brush newValue)
        {
            if (_effectiveViewportChanged != null)
            {
                foreach (var child in _effectiveViewportChanged)
                {
                    child.ReplacementColor = newValue;
                }
            }
        }

        #endregion

#if NET9_0_OR_GREATER
        #region Native teardown

        // A XamlDirect object is a bare CDependencyObject with no framework peer, so
        // DXamlCore::ShutdownAllPeers - which severs every entry in the peer table before
        // CCoreServices is released - never sees one. Its RCW, finalized after the view's core
        // is gone, marshals the Release back into the uninitializing apartment through
        // IContextCallback and faults unparenting a core-less object. microsoft/CsWinRT#2532.
        //
        // The handles therefore have to be released deterministically rather than left to
        // collection: XAML holds the peers until ShutdownAllPeers, so they only become garbage
        // after the core is already gone, and no GC pass scheduled at any hook can beat that.
        // Disposing on the owning thread costs nothing extra - ObjectReferenceWithContext
        // marshals only when the calling context differs from the one it captured.
        //
        // Weak on both sides deliberately: Unloaded is not always raised, so there is no
        // reliable point to unregister from, and a strong list would pin every block a window
        // ever rendered for as long as the window lives.
        private static readonly ConditionalWeakTable<XamlRoot, ConditionalWeakTable<FormattedTextBlock, object>> _live = new();

        private static readonly object _present = new();

        private bool _registered;
        private bool _released;

        // Called where the handles are emitted, so a block is registered exactly when it starts
        // owning something to release. SetText builds nothing before OnApplyTemplate, and a
        // template is only applied to a parented element, so there is always a XamlRoot here.
        private void RegisterNative()
        {
            if (_registered)
            {
                return;
            }

            var xamlRoot = XamlRoot;
            Debug.Assert(xamlRoot != null);

            if (xamlRoot != null)
            {
                _registered = true;
                _live.GetOrCreateValue(xamlRoot).AddOrUpdate(this, _present);
            }
        }

        /// <summary>
        /// Releases every XamlDirect handle held by the blocks of one window. Must run on that
        /// window's thread, while its XAML core is still alive.
        /// </summary>
        internal static void ReleaseNative(XamlRoot xamlRoot)
        {
            if (xamlRoot == null || !_live.TryGetValue(xamlRoot, out var blocks))
            {
                return;
            }

            _live.Remove(xamlRoot);

            var count = 0;

            foreach (var block in blocks)
            {
                block.Key.ReleaseNative();
                count++;
            }

            RelativeDateService.Release(xamlRoot);

            Logger.Info($"released {count} block(s)");
        }

        internal void ReleaseNative()
        {
            if (_released)
            {
                return;
            }

            _released = true;

            Utils.ReleaseHandle(_fastRun);
            _fastRun = null;

            ReleaseHandles(_activeParagraphs);
            ReleaseHandles(_activeSpans);
            ReleaseHandles(_activeRuns);

            for (int i = 0; i < _activeHyperlinks.Count; i++)
            {
                Utils.ReleaseHandle(_activeHyperlinks[i].Native);
                Utils.ReleaseHandle(_activeHyperlinks[i].Inlines);
            }

            _activeHyperlinks.Clear();

            // Every entry is a run, so _activeRuns has already disposed it. Clearing is all
            // that is left, and it has to happen: ClearEntities would otherwise hand a disposed
            // handle to RelativeDateService.Unsubscribe.
            _dates?.Clear();

            // Shared by the blocks of one chat, so this runs once per block - the queues are
            // emptied by the first pass and the rest see nothing to do.
            _pools?.ReleaseNative();
            _pools = null;
        }

        private static void ReleaseHandles(List<IXamlDirectObject> handles)
        {
            if (handles == null)
            {
                return;
            }

            for (int i = 0; i < handles.Count; i++)
            {
                Utils.ReleaseHandle(handles[i]);
            }

            handles.Clear();
        }

        #endregion
#endif

        #region RecyclePool

        private FormattedTextBlockRecyclePool _pools;
        public FormattedTextBlockRecyclePool RecyclePool
        {
            get => _pools;
            set => _pools = value;
        }

        #endregion

        public bool HasOverflowContent => TextBlock?.HasOverflowContent ?? false;

        private bool _skeletonCollapsed = true;
        private ContainerVisual _skeleton;
        private SpriteVisual _foreground;

        public void ShowHideSkeleton(bool show)
        {
            if (_skeletonCollapsed != show)
            {
                return;
            }

            _skeletonCollapsed = !show;

            if (show)
            {
                var ease = BootStrapper.Current.Compositor.CreateLinearEasingFunction();
                var animation = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
                animation.InsertKeyFrame(0, new Vector3(-1, 0, 0), ease);
                animation.InsertKeyFrame(1, new Vector3(0, 0, 0), ease);
                animation.IterationBehavior = AnimationIterationBehavior.Forever;
                animation.Duration = TimeSpan.FromSeconds(1);

                var transparent = Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF);
                var foregroundColor = Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF);
                var backgroundColor = Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF);

                // TODO: Improve colors
                var lookup = ThemeService.GetLookup(ActualTheme);
                lookup.TryGetColor("SystemControlDisabledChromeDisabledLowBrush", out backgroundColor);
                lookup.TryGetColor("ApplicationPageBackgroundThemeBrush", out foregroundColor);

                var gradient = BootStrapper.Current.Compositor.CreateLinearGradientBrush();
                gradient.ColorStops.Add(BootStrapper.Current.Compositor.CreateColorGradientStop(0, Color.FromArgb(0x00, backgroundColor.R, backgroundColor.G, backgroundColor.B)));
                gradient.ColorStops.Add(BootStrapper.Current.Compositor.CreateColorGradientStop(0.67f, Color.FromArgb(0x67, backgroundColor.R, backgroundColor.G, backgroundColor.B)));
                gradient.ColorStops.Add(BootStrapper.Current.Compositor.CreateColorGradientStop(1, Color.FromArgb(0x00, backgroundColor.R, backgroundColor.G, backgroundColor.B)));
                gradient.StartPoint = new Vector2(0, 0);
                gradient.EndPoint = new Vector2(0.5f, 0);
                gradient.ExtendMode = CompositionGradientExtendMode.Wrap;

                var background = BootStrapper.Current.Compositor.CreateSpriteVisual();
                background.RelativeSizeAdjustment = Vector2.One;
                background.Brush = BootStrapper.Current.Compositor.CreateColorBrush(foregroundColor);

                _foreground = BootStrapper.Current.Compositor.CreateSpriteVisual();
                _foreground.RelativeSizeAdjustment = new Vector2(2, 1);
                _foreground.Brush = gradient;
                _foreground.StartAnimation("RelativeOffsetAdjustment", animation);

                //Placeholder = GetTemplateChild(nameof(Placeholder)) as TextBlock;
                //Presenter = GetTemplateChild(nameof(Presenter)) as TextBlock;

                _skeleton = BootStrapper.Current.Compositor.CreateContainerVisual();
                //_skeleton.Children.InsertAtTop(background);
                _skeleton.Children.InsertAtTop(_foreground);
                //_skeleton.Opacity = 0.67f;
                //_skeleton.RelativeSizeAdjustment = Vector2.One;

                //_skeleton.AnchorPoint = new Vector2(IsPlaceholderRightToLeft ? 1 : 0, 0);
                //_skeleton.RelativeOffsetAdjustment = new Vector3(IsPlaceholderRightToLeft ? 1 : 0, 0, 0);

                ElementCompositionPreview.SetElementChildVisual(this, _skeleton);

                //InvalidateSkeleton();
            }
            else
            {
                _skeleton?.Opacity = 0;
                _skeleton = null;
                ElementCompositionPreview.SetElementChildVisual(this, null);
            }
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (_skeleton == null || _text == null)
            {
                return base.ArrangeOverride(finalSize);
            }

            finalSize = base.ArrangeOverride(finalSize);

            InvalidateSkeleton();

            return finalSize;
        }

        private void InvalidateSkeleton()
        {
            var width = LastAvailableWidth;

            var fontSize = (AutoFontSize ? AppSettings.Appearance.MessageFontSize : TextBlock.FontSize) * BootStrapper.Current.TextScaleFactor;
            var quoteSize = (AutoFontSize ? AppSettings.Appearance.CaptionFontSize : TextBlock.FontSize) * BootStrapper.Current.TextScaleFactor;

            var shapes = new List<IList<Rect>>();
            var current = new List<Rect>();
            var last = default(Rect);

            for (int block = 0; block <= _last - _first; block++)
            {
                StyledParagraph styled = _text.Paragraphs[_first + block];
                Paragraph paragraph = TextBlock.Blocks[block] as Paragraph;

                if (paragraph == null)
                {
                    // TODO: figure out why this happens
                    continue;
                }

                var partial = _text.Text.Substring(styled.Offset, styled.Length);
                var entities = styled.Parts ?? TextStyleRun.NoParts;

                var size = styled.Type is TextParagraphTypeQuote
                    ? quoteSize
                    : fontSize;

                var rectangles = Direct2D.Current.LineMetrics(partial, entities, size, width - paragraph.Margin.Left - paragraph.Margin.Right, styled.Direction == TextDirectionality.RightToLeft);
                var relative = paragraph.ContentStart.GetCharacterRect(paragraph.ContentStart.LogicalDirection);

                var point = new Windows.Foundation.Point(paragraph.Margin.Left /*+ position.X*/, relative.Y /*+ position.Y*/);

                for (int i = 0; i < rectangles.Count; i++)
                {
                    var rect = rectangles[i];
                    if (rect.Width < 1 || rect.Height < 1)
                    {
                        continue;
                    }

                    rect = new Rect(rect.X - 2, rect.Y, rect.Width + 4, rect.Height);
                    rect.X += point.X;
                    rect.Y += point.Y;

                    if (current.Count > 0 && !rect.IntersectsOrTouches(last))
                    {
                        shapes.Add(current);
                        current = new List<Rect>();
                    }

                    current.Add(rect);
                    last = rect;
                }
            }

            if (current.Count > 0)
            {
                shapes.Add(current);
            }

            _skeleton.Clip = BootStrapper.Current.Compositor.CreateGeometricClip(BootStrapper.Current.Compositor.CreatePathGeometry(Direct2D.Current.GetRoundedPolygon(shapes)));
            //_skeleton.Size = Placeholder.DesiredSize.ToVector2();
            _skeleton.Size = new Vector2(TextBlock.ActualSize.X + 8, TextBlock.ActualSize.Y + 4);
            _skeleton.Offset = new Vector3(-0, -0, 0);
            //_skeleton.Size = new Vector2(TextBlock.ActualSize.X + 8, TextBlock.ActualSize.Y + 4);
            //_skeleton.Offset = new Vector3(-4, -2, 0);
        }

        public class RelativeDateService
        {
            // A dictionary value keyed by Element, so it never needs value equality - and .NET
            // Native doesn't do records anyway.
            class TextDate
            {
                public TextDate(IXamlDirectObject element, FormattedTextBlock textBlock, StyledParagraph paragraph, TextStyleRun entity, TextEntityTypeDateTime entityType, int segment)
                {
                    Element = element;
                    TextBlock = textBlock;
                    Paragraph = paragraph;
                    Entity = entity;
                    Date = Formatter.ToLocalTime(entityType.UnixTime);
                    Segment = segment;
                }

                public IXamlDirectObject Element { get; }

                public FormattedTextBlock TextBlock { get; }

                public StyledParagraph Paragraph { get; }

                public TextStyleRun Entity { get; }

                public DateTime Date { get; }

                // Where this date sits in the block's index map, captured when the block built
                // it. Only valid until the next SetText, which resubscribes.
                public int Segment { get; }

                public ulong NextUpdateAt { get; set; }

                public string Update()
                {
                    // How much the displayed date grew or shrank THIS tick. Measuring against
                    // Entity.Length - the source length - is what made the old patching wrong:
                    // it is the total growth since the first render, so applying it again on
                    // every tick, and once per date, compounded.
                    var before = string.IsNullOrEmpty(Entity.FormattedText) ? Entity.Length : Entity.FormattedText.Length;
                    var text = Entity.Update(Paragraph);
                    var delta = (string.IsNullOrEmpty(text) ? Entity.Length : text.Length) - before;

                    // Spoiler geometry needs nothing here: UpdateSpoilers derives it from the
                    // paragraph's runs, which Update has just rewritten.
                    if (delta != 0)
                    {
                        TextBlock.ShiftRenderedSpace(Segment, delta);
                    }

                    TextBlock.RegisterLayoutChanged();

                    return text;
                }
            }

            private readonly DispatcherTimer _timer = new();
            private readonly Dictionary<IXamlDirectObject, TextDate> _dates = new();

            private static readonly ConditionalWeakTable<XamlRoot, RelativeDateService> _instances = new();

#if NET9_0_OR_GREATER
            // The keys are XamlDirect handles owned by the blocks, and they are disposed by the
            // time this runs - so the dictionary only has to stop naming them.
            public static void Release(XamlRoot xamlRoot)
            {
                if (_instances.TryGetValue(xamlRoot, out RelativeDateService instance))
                {
                    _instances.Remove(xamlRoot);

                    instance._timer.Stop();
                    instance._timer.Tick -= instance.OnTick;
                    instance._dates.Clear();
                }
            }
#endif

            private RelativeDateService()
            {
                _timer.Tick += OnTick;
            }

            private void OnTick(object sender, object e)
            {
                _timer.Stop();

                _timer.Interval = GetNextUpdateInterval(_dates.Values, true);
                _timer.Start();
            }

            public static void Subscribe(IXamlDirectObject element, FormattedTextBlock textBlock, StyledParagraph paragraph, TextStyleRun run, TextEntityTypeDateTime entity, int segment)
            {
                Debug.Assert(textBlock.XamlRoot != null);

                _instances.TryGetValue(textBlock.XamlRoot, out RelativeDateService instance);

                if (instance == null)
                {
                    _instances.Add(textBlock.XamlRoot, instance = new());
                }

                instance.SubscribeImpl(element, textBlock, paragraph, run, entity, segment);
            }

            private void SubscribeImpl(IXamlDirectObject element, FormattedTextBlock textBlock, StyledParagraph paragraph, TextStyleRun run, TextEntityTypeDateTime entity, int segment)
            {
                // Replaces rather than skips. The key is a Run from the shared pool, so the same
                // object comes back around attached to a different block, and a registration
                // that outlived its block would otherwise make that Run unsubscribable - its new
                // date silently never updating - for the rest of the session.
                _dates[element] = new TextDate(element, textBlock, paragraph, run, entity, segment);
                _timer.Stop();

                _timer.Interval = GetNextUpdateInterval(_dates.Values, false);
                _timer.Start();
            }

            public static void Unsubscribe(IXamlDirectObject element, XamlRoot xamlRoot)
            {
                if (_instances.TryGetValue(xamlRoot, out var instance))
                {
                    instance.UnsubscribeImpl(element);
                }
            }

            private void UnsubscribeImpl(IXamlDirectObject element)
            {
                if (_dates.ContainsKey(element))
                {
                    _dates.Remove(element);
                    _timer.Stop();

                    if (_dates.Count > 0)
                    {
                        _timer.Interval = GetNextUpdateInterval(_dates.Values, false);
                        _timer.Start();
                    }
                }
            }

            private static TimeSpan GetNextUpdateInterval(IEnumerable<TextDate> dates, bool invalidate)
            {
                var minSeconds = int.MaxValue;

                var tickCount = Logger.TickCount;
                var currentTime = DateTime.Now;

                XamlDirect direct = null;

                foreach (var item in dates)
                {
                    var shouldReschedule = !invalidate;

                    if (invalidate || item.NextUpdateAt == 0)
                    {
                        if (item.NextUpdateAt <= tickCount)
                        {
                            shouldReschedule = true;

                            direct ??= XamlDirect.GetDefault();
                            direct.SetStringProperty(item.Element, XamlPropertyIndex.Run_Text, item.Update());
                        }
                    }

                    if (shouldReschedule)
                    {
                        var nextForThisItem = GetNextUpdateIntervalSeconds(currentTime, item.Date);

                        // Each item gets its own update time
                        item.NextUpdateAt = tickCount + (ulong)(nextForThisItem * 1000);

                        // Track the global minimum for timer interval
                        if (nextForThisItem < minSeconds)
                        {
                            minSeconds = nextForThisItem;
                        }
                    }
                    else
                    {
                        // Item doesn't need rescheduling, but still consider its existing schedule.
                        // Round up, never down to zero: an item due in under a second used to be
                        // dropped from the minimum entirely, and if every item was in that state
                        // - which is the norm for the one-second bucket, where the timer can fire
                        // a hair early - nothing set the minimum and the next tick was scheduled
                        // int.MaxValue seconds out.
                        var remainingSeconds = ((long)(item.NextUpdateAt - tickCount) + 999) / 1000;
                        if (remainingSeconds > 0 && remainingSeconds < minSeconds)
                        {
                            minSeconds = (int)remainingSeconds;
                        }
                    }
                }

                // An empty set leaves the minimum untouched; a second is the shortest the
                // buckets below ever ask for anyway.
                return TimeSpan.FromSeconds(minSeconds == int.MaxValue ? 1 : minSeconds);
            }

            private static int GetNextUpdateIntervalSeconds(DateTime currentTime, DateTime relativeTime)
            {
                TimeSpan difference = currentTime - relativeTime;
                bool isPast = difference.TotalSeconds > 0;
                double absDifference = Math.Abs(difference.TotalSeconds);

                if (absDifference < 60)
                {
                    return 1;
                }
                else if (absDifference < 3600)
                {
                    double secondsPastMinute = absDifference % 60;

                    if (isPast)
                    {
                        return (int)Math.Ceiling(60 - secondsPastMinute);
                    }
                    else
                    {
                        return (int)Math.Ceiling(secondsPastMinute);
                    }
                }
                else if (absDifference < 86400)
                {
                    double secondsPastHour = absDifference % 3600;

                    if (isPast)
                    {
                        return (int)Math.Ceiling(3600 - secondsPastHour);
                    }
                    else
                    {
                        return (int)Math.Ceiling(secondsPastHour);
                    }
                }
                else
                {
                    double secondsPastDay = absDifference % 86400;

                    if (isPast)
                    {
                        return (int)Math.Ceiling(86400 - secondsPastDay);
                    }
                    else
                    {
                        return (int)Math.Ceiling(secondsPastDay);
                    }
                }
            }
        }
    }
}
