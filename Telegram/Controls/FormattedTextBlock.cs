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
using System.Numerics;
using System.Threading;
using Telegram.Common;
using Telegram.Controls.Media;
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
using Windows.UI.Xaml.Data;
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

    public class FormattedTextBlockRecyclePool
    {
        public readonly Queue<IXamlDirectObject> Paragraphs = new();
        public readonly Queue<Hyperlink> Hyperlinks = new();
        public readonly Queue<IXamlDirectObject> Spans = new();
        public readonly Queue<IXamlDirectObject> Runs = new();
        public readonly Queue<InlineUIContainer> Emoji = new();
    }

    [ContentProperty(Name = "Blocks")]
    public partial class FormattedTextBlock : FormattedTextBlockBase
    {
        private IClientService _clientService;
        private StyledText _text;
        private double _fontSize;

        private IXamlDirectObject _fastRun;
        private double _fastFontSize;

        private string _query;

        private bool _isHighlighted;
        private bool _ignoreSpoilers = false;

        private AnimatedImage _spoilerPresenter;
        private CanvasGeometry _spoilerGeometry;

        private Span _spanForInlines;

        private readonly List<int> _codeBlocks = new();
        private readonly List<Hyperlink> _links = new();
        private readonly List<TextStyleSpoiler> _spoilers = new();

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

        private TextHighlighter _spoiler;
        private bool _invalidateSpoilers;

        private Canvas Below;
        private RichTextBlock TextBlock;

        private bool _templateApplied;
        private int _templateExecuted;

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

            for (int i = 0; i < _blocks?.Count; i++)
            {
                var block = _blocks[i] as Paragraph;
                TextBlock.Blocks.Add(block);

                if (i == _blocks.Count - 1 && block.Inlines.Count > 0 && block.Inlines[^1] is Span spanForInlines)
                {
                    _spanForInlines = spanForInlines;
                }
            }

            _templateApplied = true;

            if (_clientService != null && _text != null)
            {
                SetText(_clientService, _text, _fontSize);

                if (_query != null || _spoiler != null)
                {
                    SetQuery(_query, true);
                }
            }
        }

        public double LastAvailableWidth { get; private set; }

        protected override Size MeasureOverride(Size availableSize)
        {
            LastAvailableWidth = availableSize.Width;
            return base.MeasureOverride(availableSize);
        }

        private bool _textSelectionDisabled;

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
                    if (!_textSelectionDisabled)
                    {
                        _textSelectionDisabled = true;
                        TextBlock.IsHitTestVisible = false;
                        Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Hand, 0);
                    }

                    e.Handled = true;
                    return;
                }
            }

            if (_spanForInlines == null && _textSelectionDisabled)
            {
                _textSelectionDisabled = false;
                TextBlock.IsHitTestVisible = true;
                Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Arrow, 0);
            }
        }

        protected override void OnPointerExited(PointerRoutedEventArgs e)
        {
            if (_spanForInlines == null && _textSelectionDisabled)
            {
                _textSelectionDisabled = false;
                TextBlock.IsHitTestVisible = true;
                Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Arrow, 0);
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

        public void Clear()
        {
            _clientService = null;
            //_text = null;

            _query = null;
            _spoiler = null;
            _ignoreSpoilers = false;

            ClearEntities();
        }

        private void ClearEntities()
        {
            foreach (var link in _links)
            {
                ToolTipService.SetToolTip(link, null);
            }

            _links.Clear();
            _spoilers.Clear();
            _codeBlocks.Clear();

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
                    SetText(_clientService, _text, _fontSize);
                    SetQuery(string.Empty);

                    if (Below == null || _spoilerPresenter == null)
                    {
                        return;
                    }

                    Below.Children.Remove(_spoilerPresenter);
                    _spoilerPresenter = null;
                    _spoilerGeometry = null;
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

        public void SetQuery(string query, bool force = false)
        {
            if ((_query ?? string.Empty) == (query ?? string.Empty) && _isHighlighted == (_spoiler != null) && !force && !_invalidateSpoilers)
            {
                return;
            }

            _query = query;
            _invalidateSpoilers = false;

            if (TextBlock == null || !TextBlock.IsLoaded)
            {
                return;
            }

            if (_text != null)
            {
                if (_isHighlighted)
                {
                    _isHighlighted = false;
                    TextBlock.TextHighlighters.Clear();
                }

                if (query?.Length > 0)
                {
                    var find = _text.Text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
                    if (find != -1)
                    {
                        var shift = 0;

                        foreach (var para in _text.Paragraphs)
                        {
                            if (para.Offset + para.Length < find)
                            {
                                shift++;
                            }
                        }

                        var highligher = new TextHighlighter();
                        highligher.Foreground = new SolidColorBrush(Colors.White);
                        highligher.Background = new SolidColorBrush(Colors.Orange);
                        highligher.Ranges.Add(new TextRange { StartIndex = find - shift, Length = query.Length });

                        _isHighlighted = true;
                        TextBlock.TextHighlighters.Add(highligher);
                    }
                }

                if (_spoiler != null)
                {
                    _isHighlighted = true;
                    TextBlock.TextHighlighters.Add(_spoiler);
                }
                else
                {
                    if (Below == null || _spoilerPresenter == null)
                    {
                        return;
                    }

                    Below.Children.Remove(_spoilerPresenter);
                    _spoilerPresenter = null;
                    _spoilerGeometry = null;
                }
            }
            else if (_isHighlighted)
            {
                _isHighlighted = false;
                TextBlock.TextHighlighters.Clear();

                if (Below == null || _spoilerPresenter == null)
                {
                    return;
                }

                Below.Children.Remove(_spoilerPresenter);
                _spoilerPresenter = null;
                _spoilerGeometry = null;
                _spoiler = null;
            }
        }

        public void SetText(IClientService clientService, FormattedText text, double fontSize = 0)
        {
            SetText(clientService, TextStyleRun.GetText(text), fontSize);
        }

        public void SetText(IClientService clientService, string text, IList<TextEntity> entities, double fontSize = 0)
        {
            SetText(clientService, TextStyleRun.GetText(text, entities), fontSize);
        }

        private HashSet<IXamlDirectObject> _activeParagraphs;
        private HashSet<Hyperlink> _activeHyperlinks;
        private HashSet<IXamlDirectObject> _activeSpans;
        private HashSet<IXamlDirectObject> _activeRuns;
        private HashSet<InlineUIContainer> _activeEmojis;

        private IXamlDirectObject GetOrCreateParagraph(XamlDirect direct)
        {
            if (_pools != null && _pools.Paragraphs.TryDequeue(out var paragraph))
            {
                direct.ClearProperty(paragraph, XamlPropertyIndex.Block_Margin);
                direct.ClearProperty(paragraph, XamlPropertyIndex.Block_TextAlignment);
                direct.ClearProperty(paragraph, XamlPropertyIndex.TextElement_FontSize);
                direct.ClearProperty(paragraph, XamlPropertyIndex.TextElement_FontFamily);

                _activeParagraphs.Add(paragraph);
                return paragraph;
            }

            paragraph = direct.CreateInstance(XamlTypeIndex.Paragraph);
            _activeParagraphs?.Add(paragraph);
            return paragraph;
        }

        private Hyperlink GetOrCreateHyperlink()
        {
            if (_pools != null && _pools.Hyperlinks.TryDequeue(out var hyperlink))
            {
                _activeHyperlinks.Add(hyperlink);
                return hyperlink;
            }

            hyperlink = new Hyperlink();
            _activeHyperlinks?.Add(hyperlink);
            return hyperlink;
        }

        private IXamlDirectObject GetOrCreateSpan(XamlDirect direct)
        {
            if (_pools != null && _pools.Spans.TryDequeue(out var span))
            {
                _activeSpans.Add(span);
                return span;
            }

            span = direct.CreateInstance(XamlTypeIndex.Span);
            _activeSpans?.Add(span);
            return span;
        }

        private IXamlDirectObject GetOrCreateRun(XamlDirect direct, IXamlDirectObject inlines, string text, int offset, int length, FlowDirection direction, TextStyle style, FontFamily fontFamily, double fontSize, bool transparent)
        {
            if (_pools != null && _pools.Runs.TryDequeue(out var run))
            {
                direct.SetStringProperty(run, XamlPropertyIndex.Run_Text, text.Substring(offset, length));
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

                // TODO: removed once fixed by Microsoft
                if (transparent)
                {
                    direct.SetObjectProperty(run, XamlPropertyIndex.TextElement_Foreground, null);
                }
                else
                {
                    direct.ClearProperty(run, XamlPropertyIndex.TextElement_Foreground);
                }

                direct.AddToCollection(inlines, run);

                _activeRuns.Add(run);
                return run;
            }

            run = NativeUtils.AddRunToCollection(direct, inlines, text, offset, length, direction, style, fontFamily, fontSize, transparent);
            _activeRuns?.Add(run);
            return run;
        }

        private IXamlDirectObject GetOrCreateRun(XamlDirect direct, IXamlDirectObject inlines, string text, FlowDirection direction, TextStyle style, FontFamily fontFamily, double fontSize, bool transparent)
        {
            if (_pools != null && _pools.Runs.TryDequeue(out var run))
            {
                direct.SetStringProperty(run, XamlPropertyIndex.Run_Text, text);
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

                // TODO: removed once fixed by Microsoft
                if (transparent)
                {
                    direct.SetObjectProperty(run, XamlPropertyIndex.TextElement_Foreground, null);
                }
                else
                {
                    direct.ClearProperty(run, XamlPropertyIndex.TextElement_Foreground);
                }

                direct.AddToCollection(inlines, run);

                _activeRuns.Add(run);
                return run;
            }

            run = NativeUtils.AddRunToCollection(direct, inlines, text, direction, style, fontFamily, fontSize, transparent);
            _activeRuns?.Add(run);
            return run;
        }

        private CustomEmojiIcon GetOrCreateEmoji(out InlineUIContainer inline)
        {
            if (_pools != null && _pools.Emoji.TryDequeue(out inline))
            {
                _activeEmojis.Add(inline);
                return inline.Child as CustomEmojiIcon;
            }

            var player = new CustomEmojiIcon();
            inline = new InlineUIContainer
            {
                Child = player
            };

            _activeEmojis?.Add(inline);
            return player;
        }

        private void Recycle(XamlDirect xd)
        {
            if (_pools == null)
            {
                return;
            }

            IXamlDirectObject inlines;
            foreach (var paragraph in _activeParagraphs)
            {
                inlines = xd.GetXamlDirectObjectProperty(paragraph, XamlPropertyIndex.Paragraph_Inlines);
                xd.ClearCollection(inlines);
                _pools.Paragraphs.Enqueue(paragraph);
            }
            foreach (var hyperlink in _activeHyperlinks)
            {
                hyperlink.Inlines.Clear();
                hyperlink.Click -= Entity_Click;
                _pools.Hyperlinks.Enqueue(hyperlink);
            }
            foreach (var span in _activeSpans)
            {
                inlines = xd.GetXamlDirectObjectProperty(span, XamlPropertyIndex.Span_Inlines);
                xd.ClearCollection(inlines);
                _pools.Spans.Enqueue(span);
            }
            foreach (var run in _activeRuns)
            {
                _pools.Runs.Enqueue(run);
            }
            foreach (var emoji in _activeEmojis)
            {
                if (_pools.Emoji.Count < 500)
                {
                    _pools.Emoji.Enqueue(emoji);
                }
            }

            _activeEmojis.Clear();
            _activeRuns.Clear();
            _activeSpans.Clear();
            _activeHyperlinks.Clear();
            _activeParagraphs.Clear();
        }

        protected override void OnLoaded()
        {
            // Don't reapply the text if it was just applied by OnApplyTemplate
            if (_templateExecuted > 0 || _pools == null)
            {
                return;
            }

            if (_clientService != null && _text != null)
            {
                SetText(_clientService, _text, _fontSize);

                if (_query != null || _spoiler != null)
                {
                    SetQuery(_query, true);
                }
            }
        }

        protected override void OnUnloaded()
        {
            _templateExecuted = 0;

            if (_pools == null || (_fastRun != null && _text?.IsPlain is true))
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

            _fastRun = null;
            Recycle(direct);
            ClearEntities();
        }

        public void SetText(IClientService clientService, StyledText styled, double fontSize = 0)
        {
            var prevPlain = _text?.IsPlain ?? false;
            var prevDirection = prevPlain ? _text?.Paragraphs[0].Direction : TextDirectionality.Neutral;

            _clientService = clientService;
            _text = styled;
            _fontSize = fontSize;

            if (!_templateApplied)
            {
                return;
            }

            var execution = ++_templateExecuted;

            var autoFontSize = fontSize;
            var xamlFontSize = TextBlock.FontSize;

            if (AutoFontSize && fontSize == 0)
            {
                fontSize = Theme.Current.MessageFontSize;
            }

            var direct = XamlDirect.GetDefault();

            // PERF: fast path if both model and view have one paragraph with one run
            if (_fastRun != null && styled != null && prevPlain && styled.IsPlain && prevDirection == styled.Paragraphs[0].Direction && !HasCodeBlocks)
            {
                if (_fastFontSize != fontSize)
                {
                    _fastFontSize = fontSize;
                    direct.SetDoubleProperty(_fastRun, XamlPropertyIndex.TextElement_FontSize, fontSize);
                }

                direct.SetStringProperty(_fastRun, XamlPropertyIndex.Run_Text, styled.Text);

                if (!_skeletonCollapsed)
                {
                    RegisterLayoutChanged();
                }

                return;
            }

            var locale = LocaleService.Current.FlowDirection;

            var directBlock = direct.GetXamlDirectObject(TextBlock);
            var blocks = direct.GetXamlDirectObjectProperty(directBlock, XamlPropertyIndex.RichTextBlock_Blocks);

            _fastFontSize = fontSize;
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
                _invalidateSpoilers = _spoiler != null;
                return;
            }

            TextHighlighter spoiler = null;

            var preformatted = false;
            TextParagraphType lastType = null;
            TextParagraphType firstType = null;

            FontFamily monospaceFontFamily = null;
            FontFamily GetMonospaceFontFamily()
            {
                return monospaceFontFamily ?? new FontFamily("Consolas, " + Theme.Current.XamlAutoFontFamily);
            }

            var alignment = TextAlignment;

            var text = styled.Text;
            var offset = 0;

            for (int i = 0; i < styled.Paragraphs.Count; i++)
            {
                StyledParagraph part = styled.Paragraphs[i];

                // This should not happen, but it does.
                text = styled.Text.Substring(part.Offset, Math.Min(part.Length, styled.Text.Length - part.Offset));

                var type = part.Type;
                var runs = part.Runs;
                var partFontSize = fontSize;

                var previous = 0;

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
                    var last = part == styled.Paragraphs[^1];
                    var temp = direct.GetObject(paragraph) as Paragraph;
                    direct.SetThicknessProperty(paragraph, XamlPropertyIndex.Block_Margin, new Thickness(11, 6, 24, last ? 0 : 8));
                    direct.SetDoubleProperty(paragraph, XamlPropertyIndex.TextElement_FontSize, Theme.Current.CaptionFontSize);
                    partFontSize = Theme.Current.CaptionFontSize;

                    _codeBlocks.Add(i);
                }

                for (int j = 0; j < runs.Count; j++)
                {
                    var entity = runs[j];
                    if (entity.Offset > previous)
                    {
                        GetOrCreateRun(direct, inlines, text, previous, entity.Offset - previous, direction, Native.TextStyle.None, null, fontSize: partFontSize, false);
                        offset += entity.Offset - previous;
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
                            if (entity.Type is TextEntityTypeCode)
                            {
                                var hyperlink = GetOrCreateHyperlink();
                                hyperlink.Click += Entity_Click;
                                hyperlink.UnderlineStyle = UnderlineStyle.None;

                                MessageHelper.SetHyperlinkInfo(hyperlink, new TextEntityClickEventArgs(entity.Type, data));
                                BindingOperations.SetBinding(hyperlink, Hyperlink.ForegroundProperty, _foregroundBinding ??= new Binding
                                {
                                    Path = new PropertyPath("Foreground"),
                                    Source = this
                                });

                                var native = direct.GetXamlDirectObject(hyperlink);
                                var collection = direct.GetXamlDirectObjectProperty(native, XamlPropertyIndex.Span_Inlines);

                                GetOrCreateRun(direct, collection, data, direction, Native.TextStyle.None, GetMonospaceFontFamily(), partFontSize, false);
                                offset += data.Length;

                                direct.AddToCollection(inlines, native);
                            }
                            else
                            {
                                direct.SetObjectProperty(paragraph, XamlPropertyIndex.TextElement_FontFamily, GetMonospaceFontFamily());

                                var placeholder = GetOrCreateRun(direct, inlines, data, direction, Native.TextStyle.None, null, 0, false);
                                offset += data.Length;

                                preformatted = true;

                                var has = entity.Type is TextEntityTypePreCode { Language.Length: > 0 };

                                var last = part == styled.Paragraphs[^1];
                                var temp = direct.GetObject(paragraph) as Paragraph;

                                direct.SetThicknessProperty(paragraph, XamlPropertyIndex.Block_Margin, new Thickness(11, (has ? 22 : 0) + 6, has ? 8 : 24, last ? 0 : 8));

                                if (entity.Type is TextEntityTypePreCode preCode && preCode.Language.Length > 0)
                                {
                                    ProcessCodeBlock(direct, inlines, placeholder, data, preCode.Language, execution);
                                }

                                _codeBlocks.Add(i);
                            }
                        }
                        else
                        {
                            GetOrCreateRun(direct, inlines, data, direction, Native.TextStyle.None, GetMonospaceFontFamily(), 0, false);
                            offset += data.Length;
                        }
                    }
                    else
                    {
                        IXamlDirectObject parent = null;
                        IXamlDirectObject parentInlines = inlines;

                        if (paragraph != null)
                        {
                            if (_ignoreSpoilers is false && entity.HasFlag(Native.TextStyle.Spoiler))
                            {
                                var hyperlink = GetOrCreateSpan(direct);
                                direct.SetObjectProperty(hyperlink, XamlPropertyIndex.TextElement_Foreground, null);
                                direct.SetObjectProperty(hyperlink, XamlPropertyIndex.TextElement_FontFamily, BootStrapper.Current.Resources["SpoilerFontFamily"] as FontFamily);

                                _spoilers.Add(new TextStyleSpoiler(entity.Offset, entity.Length, i));

                                spoiler ??= new TextHighlighter();
                                spoiler.Ranges.Add(new TextRange { StartIndex = offset, Length = entity.Length });

                                parent = hyperlink;
                                parentInlines = direct.GetXamlDirectObjectProperty(parent, XamlPropertyIndex.Span_Inlines);
                            }
                            else if ((entity.HasFlag(Native.TextStyle.Mention) || entity.HasFlag(Native.TextStyle.Url)))
                            {
                                if (entity.Type is TextEntityTypeMentionName or TextEntityTypeTextUrl)
                                {
                                    var hyperlink = GetOrCreateHyperlink();
                                    if (entity.Type is TextEntityTypeTextUrl textUrl)
                                    {
                                        MessageHelper.SetHyperlinkInfo(hyperlink, new TextEntityClickEventArgs(entity.Type, textUrl.Url));

                                        _links.Add(hyperlink);

                                        if (textUrl.Url.StartsWith("http"))
                                        {
                                            ToolTipService.SetToolTip(hyperlink, textUrl.Url);
                                        }
                                    }
                                    else
                                    {
                                        MessageHelper.SetHyperlinkInfo(hyperlink, new TextEntityClickEventArgs(entity.Type));
                                    }

                                    hyperlink.Click += Entity_Click;
                                    hyperlink.UnderlineStyle = HyperlinkStyle;
                                    hyperlink.FontWeight = HyperlinkFontWeight;
                                    hyperlink.UnderlineStyle = UnderlineStyle.None;

                                    BindingOperations.SetBinding(hyperlink, Hyperlink.ForegroundProperty, _hyperlinkBinding ??= new Binding
                                    {
                                        Path = new PropertyPath("HyperlinkForeground"),
                                        Source = this
                                    });

                                    parent = direct.GetXamlDirectObject(hyperlink);
                                    parentInlines = direct.GetXamlDirectObjectProperty(parent, XamlPropertyIndex.Span_Inlines);
                                }
                                else
                                {
                                    var hyperlink = GetOrCreateHyperlink();
                                    var data = text.Substring(entity.Offset, entity.Length);

                                    hyperlink.Click += Entity_Click;
                                    hyperlink.UnderlineStyle = HyperlinkStyle;
                                    hyperlink.FontWeight = HyperlinkFontWeight;
                                    hyperlink.UnderlineStyle = entity.Type is TextEntityTypeUrl
                                        ? UnderlineStyle.Single
                                        : UnderlineStyle.None;

                                    BindingOperations.SetBinding(hyperlink, Hyperlink.ForegroundProperty, _hyperlinkBinding ??= new Binding
                                    {
                                        Path = new PropertyPath("HyperlinkForeground"),
                                        Source = this
                                    });

                                    MessageHelper.SetHyperlinkInfo(hyperlink, new TextEntityClickEventArgs(entity.Type, data));

                                    parent = direct.GetXamlDirectObject(hyperlink);
                                    parentInlines = direct.GetXamlDirectObjectProperty(parent, XamlPropertyIndex.Span_Inlines);
                                }
                            }
                        }
                        else if (_ignoreSpoilers is false && entity.HasFlag(Native.TextStyle.Spoiler))
                        {
                            var hyperlink = GetOrCreateSpan(direct);
                            direct.SetObjectProperty(hyperlink, XamlPropertyIndex.TextElement_Foreground, null);
                            direct.SetObjectProperty(hyperlink, XamlPropertyIndex.TextElement_FontFamily, BootStrapper.Current.Resources["SpoilerFontFamily"] as FontFamily);

                            _spoilers.Add(new TextStyleSpoiler(entity.Offset, entity.Length, i));

                            if (textOffset == -1)
                            {
                                textOffset = _spanForInlines.ContentStart.OffsetToIndex(TextBlock);
                            }

                            spoiler ??= new TextHighlighter();
                            spoiler.Ranges.Add(new TextRange { StartIndex = textOffset + offset, Length = entity.Length });

                            parent = hyperlink;
                            parentInlines = direct.GetXamlDirectObjectProperty(hyperlink, XamlPropertyIndex.Span_Inlines);
                        }

                        // Consumes local inlines instead of paragraph's
                        // TODO: still use a InlineUIContainer for emojis in spoilers to avoid text resizes
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

                                BindingOperations.SetBinding(block, global::Windows.UI.Xaml.Controls.TextBlock.ForegroundProperty, _emojiBinding ??= new Binding
                                {
                                    Path = new PropertyPath("IconForeground"),
                                    Source = this
                                });
                            }
                            else
                            {
                                var player = GetOrCreateEmoji(out inline);
                                player.LoopCount = 0;
                                player.HorizontalAlignment = HorizontalAlignment.Left;
                                player.FlowDirection = FlowDirection.LeftToRight;
                                player.Style = EmojiStyle;
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

                                BindingOperations.SetBinding(player, AnimatedImage.ReplacementColorProperty, _emojiBinding ??= new Binding
                                {
                                    Path = new PropertyPath("IconForeground"),
                                    Source = this
                                });

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

                                GetOrCreateRun(direct, inlines, character, direction, Native.TextStyle.None, null, fontSize: partFontSize, transparent: true);
                                offset++;
                            }

                            direct.AddToCollection(inlines, direct.GetXamlDirectObject(inline));
                            GetOrCreateRun(direct, inlines, Icons.ZWNJ, direction, Native.TextStyle.None, null, partFontSize, true);
                            offset++;
                        }
                        else
                        {
                            GetOrCreateRun(direct, parentInlines, text, entity.Offset, entity.Length, direction, entity.Flags, null, partFontSize, false);
                            offset += entity.Length;
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
                    _fastRun = GetOrCreateRun(direct, inlines, text, previous, text.Length - previous, direction, Native.TextStyle.None, null, partFontSize, false);
                    offset += text.Length - previous;
                }

                if (paragraph != null)
                {
                    direct.AddToCollection(blocks, paragraph);
                }
                else if (i < styled.Paragraphs.Count - 1)
                {
                    GetOrCreateRun(direct, inlines, " ", direction, Native.TextStyle.None, null, 0, false);
                    offset++;
                }

                if (part.Offset == 0)
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

                _invalidateSpoilers = _spoiler != null;
                _spoiler = spoiler;
            }
            else
            {
                _invalidateSpoilers = _spoiler != null;
                _spoiler = null;
            }

            var topPadding = 0d;
            var bottomPadding = false;

            if (_spanForInlines == null)
            {
                if (firstType is TextParagraphTypeMonospace { Language.Length: > 0 })
                {
                    topPadding = 22 + 6;
                }
                else if (firstType is not null)
                {
                    topPadding = 6;
                }

                if (AdjustLineEnding && styled.Paragraphs.Count > 0)
                {
                    var direction = styled.Paragraphs[^1].Direction switch
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

            Below.Margin = new Thickness(0, topPadding, 0, 0);
            TextBlock.Margin = new Thickness(0, topPadding, 0, 0);

            if (spoilerChanged || !_skeletonCollapsed)
            {
                RegisterLayoutChanged();
            }
        }

        private Binding _foregroundBinding;
        private Binding _hyperlinkBinding;
        private Binding _emojiBinding;

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
            UpdateBelow();
            UpdateSpoilers();

            if (!_skeletonCollapsed)
            {
                InvalidateSkeleton();
            }
        }

        private void UpdateBelow()
        {
            Below.Children.ClearIfNotEmpty();

            var fontSize = (AutoFontSize ? Theme.Current.MessageFontSize : TextBlock.FontSize) * BootStrapper.Current.TextScaleFactor;
            var quoteSize = (AutoFontSize ? Theme.Current.CaptionFontSize : TextBlock.FontSize) * BootStrapper.Current.TextScaleFactor;

            var width = LastAvailableWidth;

            foreach (var block in _codeBlocks)
            {
                StyledParagraph styled = _text.Paragraphs[block];
                Paragraph paragraph = TextBlock.Blocks[block] as Paragraph;

                if (paragraph == null)
                {
                    // TODO: figure out why this happens
                    continue;
                }

                var partial = _text.Text.Substring(styled.Offset, styled.Length);
                var entities = styled.Parts ?? Array.Empty<TextStylePart>();

                var size = styled.Type is TextParagraphTypeQuote
                    ? quoteSize
                    : fontSize;

                var rectangles = PlaceholderHelper.Foreground.LayoutMetrics(partial, 0, partial.Length, entities, size, width - paragraph.Margin.Left - paragraph.Margin.Right, styled.Direction == TextDirectionality.RightToLeft);
                var relative = paragraph.ContentStart.GetCharacterRect(paragraph.ContentStart.LogicalDirection);
                var end = paragraph.ContentEnd.GetCharacterRect(paragraph.ContentEnd.LogicalDirection);

                var startY = Math.Round(relative.Y);
                var endBottom = Math.Round(end.Bottom);

                if (styled.Type is TextParagraphTypeMonospace monospace && monospace.Language.Length > 0)
                {
                    var rect = new BlockCode();
                    rect.Width = rectangles.Width + paragraph.Margin.Left + paragraph.Margin.Right;
                    rect.Height = Math.Max(endBottom - startY + 6 + 22, 0);
                    rect.LanguageName = monospace.Language;
                    Canvas.SetLeft(rect, styled.Direction == TextDirectionality.RightToLeft ? ActualWidth - rect.Width : 0);
                    Canvas.SetTop(rect, startY - 2 - 22);

                    Below.Children.Add(rect);
                }
                else
                {
                    var rect = new BlockQuote();
                    rect.Width = rectangles.Width + paragraph.Margin.Left + paragraph.Margin.Right;
                    rect.Height = Math.Max(endBottom - startY + 6, 0);
                    rect.Glyph = paragraph.FontSize == Theme.Current.MessageFontSize ? Icons.CodeFilled16 : Icons.QuoteBlockFilled16;
                    Canvas.SetLeft(rect, styled.Direction == TextDirectionality.RightToLeft ? ActualWidth - rect.Width : 0);
                    Canvas.SetTop(rect, startY - 2);

                    Below.Children.Add(rect);
                }
            }
        }

        private void UpdateSpoilers()
        {
            if (_ignoreSpoilers || _spoilers.Empty())
            {
                if (_spoilerPresenter != null)
                {
                    Below.Children.Remove(_spoilerPresenter);
                    _spoilerPresenter = null;
                    _spoilerGeometry = null;
                }

                return;
            }

            var fontSize = (AutoFontSize ? Theme.Current.MessageFontSize : TextBlock.FontSize) * BootStrapper.Current.TextScaleFactor;
            var quoteSize = (AutoFontSize ? Theme.Current.CaptionFontSize : TextBlock.FontSize) * BootStrapper.Current.TextScaleFactor;

            var width = LastAvailableWidth;
            var inset = 0;

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
                foreach (var hyperlink in _spoilers)
                {
                    StyledParagraph styled = _text.Paragraphs[hyperlink.ParagraphIndex];
                    Paragraph paragraph = TextBlock.Blocks[hyperlink.ParagraphIndex] as Paragraph;

                    if (paragraph == null)
                    {
                        // TODO: figure out why this happens
                        continue;
                    }

                    if (hyperlink.ParagraphIndex == 0)
                    {
                        inset = styled.Type switch
                        {
                            TextParagraphTypeMonospace { Language.Length: > 0 } => 22 + 6,
                            not null => 6,
                            _ => 0
                        };
                    }

                    int xoffset = hyperlink.Offset;
                    int xlength = hyperlink.Length;

                    var partial = _text.Text.Substring(styled.Offset, styled.Length);
                    var entities = styled.Parts ?? Array.Empty<TextStylePart>();

                    var size = styled.Type is TextParagraphTypeQuote
                        ? quoteSize
                        : fontSize;

                    var rectangles = PlaceholderHelper.Foreground.RangeMetrics(partial, xoffset, xlength, entities, size, width - paragraph.Margin.Left - paragraph.Margin.Right, styled.Direction == TextDirectionality.RightToLeft, true);
                    var relative = paragraph.ContentStart.GetCharacterRect(paragraph.ContentStart.LogicalDirection);

                    var point = new Windows.Foundation.Point(paragraph.Margin.Left + position.X, relative.Y + position.Y + inset);

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
                foreach (var hyperlink in _spoilers)
                {
                    StyledParagraph styled = _text.Paragraphs[hyperlink.ParagraphIndex];

                    int xoffset = styled.Offset + hyperlink.Offset;
                    int xlength = hyperlink.Length;

                    var partial = _text.Text.Replace('\n', ' ');
                    var entities = _text.Parts;

                    var size = fontSize;

                    var rectangles = PlaceholderHelper.Foreground.RangeMetrics(partial, xoffset, xlength, entities, size, width - relative.X, styled.Direction == TextDirectionality.RightToLeft, false);
                    var point = new Windows.Foundation.Point(relative.X + position.X, relative.Y + position.Y + inset);

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

            Below.Children.Add(_spoilerPresenter);

            var visual = ElementComposition.GetElementVisual(_spoilerPresenter);
            var geometry = visual.Compositor.CreatePathGeometry(new CompositionPath(_spoilerGeometry));
            visual.Clip = visual.Compositor.CreateGeometricClip(geometry);
        }

        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            var resources = sender.ActualTheme == ElementTheme.Light ? _light : _dark;

            foreach (var item in _brushes)
            {
                item.Value.Color = resources[item.Key];
            }
        }

        #region PreCode

        private async void ProcessCodeBlock(XamlDirect direct, IXamlDirectObject inlines, IXamlDirectObject placeholder, string text, string language, int execution)
        {
            try
            {
                var tokens = await SyntaxToken.TokenizeAsync(language.ToLowerInvariant(), text);

                // Only apply if text block is still loaded
                if (_templateExecuted == execution)
                {
                    // We need to manually recycle the Run or we'll lose track of it
                    if (_pools != null && !_activeRuns.Contains(placeholder))
                    {
                        _pools.Runs.Enqueue(placeholder);
                        _activeRuns.Remove(placeholder);
                    }

                    direct.ClearCollection(inlines);
                    ProcessCodeBlock(direct, inlines, tokens.Children);
                }
            }
            catch
            {
                // Tokenization may fail
            }
        }

        private void ProcessCodeBlock(XamlDirect direct, IXamlDirectObject inlines, IList<Token> tokens)
        {
            var fontFamily = new FontFamily("Consolas, " + Theme.Current.XamlAutoFontFamily);

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
                    else
                    {
                        direct.ClearProperty(span, XamlPropertyIndex.TextElement_Foreground);
                    }

                    if (syntax.Type == "bold")
                    {
                        direct.SetObjectProperty(span, XamlPropertyIndex.TextElement_FontWeight, FontWeights.SemiBold);
                    }
                    else if (syntax.Type == "italic")
                    {
                        direct.SetEnumProperty(span, XamlPropertyIndex.TextElement_FontStyle, (uint)FontStyle.Italic);
                    }
                    else
                    {
                        direct.ClearProperty(span, XamlPropertyIndex.TextElement_FontWeight);
                        direct.ClearProperty(span, XamlPropertyIndex.TextElement_FontStyle);
                    }

                    ProcessCodeBlock(direct, collection, syntax.Children);
                    direct.AddToCollection(inlines, span);
                }
                else if (token is TextToken text)
                {
                    GetOrCreateRun(direct, inlines, text.Value, FlowDirection.LeftToRight, Native.TextStyle.None, fontFamily, 0, false);
                }
            }
        }

        SolidColorBrush GetColor(string type)
        {
            if (_brushes.TryGetValue(type, out var brush))
            {
                return brush;
            }

            var target = ActualTheme == ElementTheme.Light ? _light : _dark;
            if (target.TryGetValue(type, out var color))
            {
                _brushes[type] = new SolidColorBrush(color);
                return _brushes[type];
            }

            return null;
        }

        private readonly Dictionary<string, Color> _light = new()
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

        private readonly Dictionary<string, Color> _dark = new()
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

        private readonly Dictionary<string, SolidColorBrush> _brushes = new();

        private CancellationTokenSource _token;

        #endregion

        private void Entity_Click(Hyperlink hyperlink, HyperlinkClickEventArgs e)
        {
            var args = MessageHelper.GetHyperlinkInfo(hyperlink);
            if (args == null)
            {
                return;
            }

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

        #region EmojiStyle

        public Style EmojiStyle
        {
            get { return (Style)GetValue(EmojiStyleProperty); }
            set { SetValue(EmojiStyleProperty, value); }
        }

        public static readonly DependencyProperty EmojiStyleProperty =
            DependencyProperty.Register("EmojiStyle", typeof(Style), typeof(FormattedTextBlock), new PropertyMetadata(null));

        #endregion

        #region IsTextSelectionEnabled

        public bool IsTextSelectionEnabled
        {
            get { return (bool)GetValue(IsTextSelectionEnabledProperty); }
            set { SetValue(IsTextSelectionEnabledProperty, value); }
        }

        public static readonly DependencyProperty IsTextSelectionEnabledProperty =
            DependencyProperty.Register("IsTextSelectionEnabled", typeof(bool), typeof(FormattedTextBlock), new PropertyMetadata(true));

        #endregion

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
            DependencyProperty.Register("HyperlinkForeground", typeof(Brush), typeof(FormattedTextBlock), new PropertyMetadata(null));

        #endregion

        #region IconForeground

        public Brush IconForeground
        {
            get { return (Brush)GetValue(IconForegroundProperty); }
            set { SetValue(IconForegroundProperty, value); }
        }

        public static readonly DependencyProperty IconForegroundProperty =
            DependencyProperty.Register("IconForeground", typeof(Brush), typeof(FormattedTextBlock), new PropertyMetadata(null));

        #endregion

        #region RecyclePool

        private FormattedTextBlockRecyclePool _pools;
        public FormattedTextBlockRecyclePool RecyclePool
        {
            get => _pools;
            set
            {
                // Currently recycle pool is only used by message text blocks
                // This means that we only set the recycle pool once and that's it.
                // In case this changes, the current logic becomes invalid.
                if (_pools == null && value != null)
                {
                    _activeParagraphs = new();
                    _activeHyperlinks = new();
                    _activeSpans = new();
                    _activeRuns = new();
                    _activeEmojis = new();
                }

                _pools = value;
            }
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
                lookup.TryGet("SystemControlDisabledChromeDisabledLowBrush", out backgroundColor);
                lookup.TryGet("ApplicationPageBackgroundThemeBrush", out foregroundColor);

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
            if (_skeleton == null)
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
            var inset = 0;

            var fontSize = (AutoFontSize ? Theme.Current.MessageFontSize : TextBlock.FontSize) * BootStrapper.Current.TextScaleFactor;
            var quoteSize = (AutoFontSize ? Theme.Current.CaptionFontSize : TextBlock.FontSize) * BootStrapper.Current.TextScaleFactor;

            var shapes = new List<IList<Rect>>();
            var current = new List<Rect>();
            var last = default(Rect);

            for (int block = 0; block < _text.Paragraphs.Count; block++)
            {
                StyledParagraph styled = _text.Paragraphs[block];
                Paragraph paragraph = TextBlock.Blocks[block] as Paragraph;

                if (paragraph == null)
                {
                    // TODO: figure out why this happens
                    continue;
                }

                if (block == 0)
                {
                    inset = styled.Type switch
                    {
                        TextParagraphTypeMonospace { Language.Length: > 0 } => 22 + 6,
                        not null => 6,
                        _ => 0
                    };
                }

                var partial = _text.Text.Substring(styled.Offset, styled.Length);
                var entities = styled.Parts ?? Array.Empty<TextStylePart>();

                var size = styled.Type is TextParagraphTypeQuote
                    ? quoteSize
                    : fontSize;

                var rectangles = PlaceholderHelper.Foreground.LineMetrics(partial, entities, size, width - paragraph.Margin.Left - paragraph.Margin.Right, styled.Direction == TextDirectionality.RightToLeft);
                var relative = paragraph.ContentStart.GetCharacterRect(paragraph.ContentStart.LogicalDirection);

                var point = new Windows.Foundation.Point(paragraph.Margin.Left /*+ position.X*/, relative.Y /*+ position.Y*/ + inset);

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

            _skeleton.Clip = BootStrapper.Current.Compositor.CreateGeometricClip(BootStrapper.Current.Compositor.CreatePathGeometry(PlaceholderHelper.Foreground.GetRoundedPolygon(shapes)));
            //_skeleton.Size = Placeholder.DesiredSize.ToVector2();
            _skeleton.Size = new Vector2(TextBlock.ActualSize.X + 8, TextBlock.ActualSize.Y + 4);
            _skeleton.Offset = new Vector3(-0, -0, 0);
            //_skeleton.Size = new Vector2(TextBlock.ActualSize.X + 8, TextBlock.ActualSize.Y + 4);
            //_skeleton.Offset = new Vector3(-4, -2, 0);
        }
    }
}
