//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Threading;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Native.Highlight;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI;

namespace Telegram.Controls
{
    public class TextEntityClickEventArgs : EventArgs
    {
        public TextEntityClickEventArgs(int offset, int length, TextEntityType type, object data)
        {
            Offset = offset;
            Length = length;
            Type = type;
            Data = data;
        }

        public int Offset { get; }

        public int Length { get; }

        public TextEntityType Type { get; }

        public object Data { get; }
    }

    public class FormattedParagraph
    {
        public Paragraph Paragraph { get; }

        public TextParagraphType Type { get; }

        public FormattedParagraph(Paragraph paragraph, TextParagraphType type)
        {
            Paragraph = paragraph;
            Type = type;
        }
    }

    public class FormattedTextBlock : Control
    {
        private IClientService _clientService;
        private StyledText _text;
        private double _fontSize;

        private string _query;

        private bool _isHighlighted;
        private bool _ignoreSpoilers = false;

        private ulong _expandSelectionDeadline;

        private readonly List<FormattedParagraph> _codeBlocks = new();
        private readonly List<Hyperlink> _links = new();

        private TextHighlighter _spoiler;
        private bool _invalidateSpoilers;

        private Canvas Below;
        private RichTextBlock TextBlock;

        private bool _templateApplied;

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

        public event EventHandler<TextEntityClickEventArgs> TextEntityClick;

        private ContextMenuOpeningEventHandler _contextMenuOpening;
        public event ContextMenuOpeningEventHandler ContextMenuOpening
        {
            add
            {
                if (TextBlock != null)
                {
                    TextBlock.ContextMenuOpening += value;
                }

                _contextMenuOpening += value;
            }
            remove
            {
                if (TextBlock != null)
                {
                    TextBlock.ContextMenuOpening -= value;
                }

                _contextMenuOpening -= value;
            }
        }

        protected override void OnApplyTemplate()
        {
            Below = GetTemplateChild(nameof(Below)) as Canvas;

            TextBlock = GetTemplateChild(nameof(TextBlock)) as RichTextBlock;
            TextBlock.LostFocus += OnLostFocus;
            TextBlock.SizeChanged += OnSizeChanged;
            TextBlock.ContextMenuOpening += _contextMenuOpening;

            TextBlock.AddHandler(DoubleTappedEvent, new DoubleTappedEventHandler(OnDoubleTapped), true);
            TextBlock.AddHandler(TappedEvent, new TappedEventHandler(OnTapped), true);

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

        private void OnLostFocus(object sender, RoutedEventArgs e)
        {
            TextBlock.Select(TextBlock.ContentStart, TextBlock.ContentStart);
        }

        private void OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (e.PointerDeviceType == PointerDeviceType.Mouse)
            {
                _expandSelectionDeadline = Logger.TickCount + BootStrapper.Current.UISettings.DoubleClickTime;
            }
        }

        private void OnTapped(object sender, TappedRoutedEventArgs e)
        {
            // If a double tap is followed by a single tap, then it's a triple tap (duh)
            if (e.PointerDeviceType == PointerDeviceType.Mouse && Logger.TickCount < _expandSelectionDeadline)
            {
                _expandSelectionDeadline = Logger.TickCount + BootStrapper.Current.UISettings.DoubleClickTime;
                VisualUtilities.QueueCallbackForCompositionRendering(ExpandSelection);
            }
        }

        private void ExpandSelection()
        {
            if (TextBlock.SelectionStart != null && TextBlock.SelectionEnd != null)
            {
                static DependencyObject FindParent(DependencyObject obj)
                {
                    if (obj is RichTextBlock or Paragraph)
                    {
                        return obj;
                    }
                    else if (obj is TextElement element)
                    {
                        return FindParent(element.ElementStart.Parent);
                    }

                    return null;
                }

                var startBlock = FindParent(TextBlock.SelectionStart.Parent);
                var endBlock = FindParent(TextBlock.SelectionEnd.Parent);

                if (startBlock == endBlock)
                {
                    try
                    {
                        if (startBlock is TextElement element)
                        {
                            TextBlock.Select(element.ContentStart, element.ContentEnd);
                        }
                        else if (startBlock is RichTextBlock block)
                        {
                            TextBlock.Select(block.ContentStart, block.ContentEnd);
                        }
                    }
                    catch
                    {
                        // All the remote procedure calls must be wrapped in a try-catch block
                    }
                }
            }
        }

        public void Clear()
        {
            _clientService = null;
            //_text = null;

            _query = null;
            _spoiler = null;

            foreach (var link in _links)
            {
                ToolTipService.SetToolTip(link, null);
            }

            _links.Clear();
            _codeBlocks.Clear();
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

            if (_text != null && TextBlock != null && TextBlock.IsLoaded)
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
            }
        }

        public void SetText(IClientService clientService, FormattedText text, double fontSize = 0)
        {
            if (text != null)
            {
                SetText(clientService, TextStyleRun.GetText(text), fontSize);
            }
            else
            {
                _clientService = clientService;
                _text = null;
                _fontSize = fontSize;

                if (_templateApplied)
                {
                    foreach (var link in _links)
                    {
                        ToolTipService.SetToolTip(link, null);
                    }

                    _links.Clear();
                    _codeBlocks.Clear();
                    TextBlock.Blocks.Clear();
                }
            }
        }

        public void SetText(IClientService clientService, string text, IList<TextEntity> entities, double fontSize = 0)
        {
            if (text != null)
            {
                SetText(clientService, TextStyleRun.GetText(text, entities), fontSize);
            }
            else
            {
                _clientService = clientService;
                _text = null;
                _fontSize = fontSize;

                if (_templateApplied)
                {
                    foreach (var link in _links)
                    {
                        ToolTipService.SetToolTip(link, null);
                    }

                    _links.Clear();
                    _codeBlocks.Clear();
                    TextBlock.Blocks.Clear();
                }
            }
        }

        public void SetText(IClientService clientService, StyledText styled, double fontSize = 0)
        {
            //_clientService = clientService;
            //_text = styled;
            //_fontSize = fontSize;

            //if (!_templateApplied)
            //{
            //    return;
            //}

            //var locale = LocaleService.Current.FlowDirection;

            //// PERF: fast path if both model and view have one paragraph with one run
            //if (styled != null && styled.IsPlain && !HasCodeBlocks)
            //{
            //    var direction = styled.Paragraphs[0].Direction switch
            //    {
            //        TextDirectionality.LeftToRight => FlowDirection.LeftToRight,
            //        TextDirectionality.RightToLeft => FlowDirection.RightToLeft,
            //        _ => locale
            //    };

            //    if (TextBlock.Blocks.Count == 1
            //        && TextBlock.Blocks[0] is Paragraph paragraph
            //        && paragraph.Inlines.Count == 1
            //        && paragraph.Inlines[0] is Run run
            //        && run.FlowDirection == direction)
            //    {
            //        run.Text = styled.Text;
            //        return;
            //    }
            //}

            //var direct = XamlDirect.GetDefault();
            //var directBlock = direct.GetXamlDirectObject(TextBlock);
            //var blocks = direct.GetXamlDirectObjectProperty(directBlock, XamlPropertyIndex.RichTextBlock_Blocks);

            //foreach (var link in _links)
            //{
            //    ToolTipService.SetToolTip(link, null);
            //}

            //_links.Clear();
            //_codeBlocks.Clear();
            //direct.ClearCollection(blocks);

            //if (string.IsNullOrEmpty(styled?.Text))
            //{
            //    return;
            //}

            //TextHighlighter spoiler = null;

            //var preformatted = false;
            //TextParagraphType lastType = null;
            //TextParagraphType firstType = null;

            //var alignment = TextAlignment;

            //var text = styled.Text;
            //var workaround = 0;

            //foreach (var part in styled.Paragraphs)
            //{
            //    // This should not happen, but it does.
            //    text = styled.Text.Substring(part.Offset, Math.Min(part.Length, styled.Text.Length - part.Offset));

            //    var type = part.Type;
            //    var runs = part.Runs;
            //    var previous = 0;

            //    var paragraph = direct.CreateInstance(XamlTypeIndex.Paragraph);
            //    var inlines = direct.GetXamlDirectObjectProperty(paragraph, XamlPropertyIndex.Paragraph_Inlines);

            //    if (AutoFontSize)
            //    {
            //        direct.SetDoubleProperty(paragraph, XamlPropertyIndex.TextElement_FontSize, Theme.Current.MessageFontSize);
            //    }

            //    // TODO: we use DetectFromContent, but this could be used too:
            //    //direct.SetEnumProperty(paragraph, XamlPropertyIndex.Block_TextAlignment, part.Direction switch
            //    //{
            //    //    TextDirectionality.LeftToRight => (uint)TextAlignment.Left,
            //    //    TextDirectionality.RightToLeft => (uint)TextAlignment.Right,
            //    //    _ => (uint)TextAlignment.DetectFromContent
            //    //});

            //    if (alignment == TextAlignment.Center)
            //    {
            //        direct.SetEnumProperty(paragraph, XamlPropertyIndex.Block_TextAlignment, (uint)alignment);
            //    }

            //    var direction = part.Direction switch
            //    {
            //        TextDirectionality.LeftToRight => FlowDirection.LeftToRight,
            //        TextDirectionality.RightToLeft => FlowDirection.RightToLeft,
            //        _ => locale
            //    };

            //    if (part.Type is TextParagraphTypeQuote)
            //    {
            //        var last = part == styled.Paragraphs[^1];
            //        var temp = direct.GetObject(paragraph) as Paragraph;
            //        temp.Margin = new Thickness(11, 6, 24, last ? 0 : 8);
            //        temp.FontSize = Theme.Current.CaptionFontSize;

            //        _codeBlocks.Add(new FormattedParagraph(temp, part.Type));
            //    }

            //    foreach (var entity in runs)
            //    {
            //        if (entity.Offset > previous)
            //        {
            //            direct.AddToCollection(inlines, CreateDirectRun(direct, text.Substring(previous, entity.Offset - previous), direction, fontSize: fontSize));
            //        }

            //        if (entity.Length + entity.Offset > text.Length)
            //        {
            //            previous = entity.Offset + entity.Length;
            //            continue;
            //        }

            //        if (entity.HasFlag(Common.TextStyle.Monospace))
            //        {
            //            var data = text.Substring(entity.Offset, entity.Length);

            //            if (entity.Type is TextEntityTypeCode)
            //            {
            //                var hyperlink = new Hyperlink();
            //                hyperlink.Click += (s, args) => Entity_Click(entity.Offset, entity.Length, entity.Type, data);
            //                hyperlink.Foreground = TextBlock.Foreground;
            //                hyperlink.UnderlineStyle = UnderlineStyle.None;

            //                hyperlink.Inlines.Add(CreateRun(data, direction, fontFamily: new FontFamily("Consolas, " + Theme.Current.XamlAutoFontFamily), fontSize: fontSize));
            //                direct.AddToCollection(inlines, direct.GetXamlDirectObject(hyperlink));
            //            }
            //            else
            //            {
            //                direct.SetObjectProperty(paragraph, XamlPropertyIndex.TextElement_FontFamily, new FontFamily("Consolas, " + Theme.Current.XamlAutoFontFamily));
            //                direct.AddToCollection(inlines, CreateDirectRun(direct, data, direction));

            //                preformatted = true;

            //                var has = entity.Type is TextEntityTypePreCode { Language.Length: > 0 };

            //                var last = part == styled.Paragraphs[^1];
            //                var temp = direct.GetObject(paragraph) as Paragraph;
            //                temp.Margin = new Thickness(11, (has ? 22 : 0) + 6, has ? 8 : 24, last ? 0 : 8);

            //                if (entity.Type is TextEntityTypePreCode preCode && preCode.Language.Length > 0)
            //                {
            //                    _codeBlocks.Add(new FormattedParagraph(temp, part.Type));
            //                    ProcessCodeBlock(temp.Inlines, data, preCode.Language);
            //                }
            //                else
            //                {
            //                    _codeBlocks.Add(new FormattedParagraph(temp, part.Type));
            //                }
            //            }
            //        }
            //        else
            //        {
            //            var local = inlines;

            //            if (_ignoreSpoilers is false && entity.HasFlag(Common.TextStyle.Spoiler))
            //            {
            //                var hyperlink = new Hyperlink();
            //                hyperlink.Click += (s, args) => Entity_Click(entity.Offset, entity.Length, new TextEntityTypeSpoiler(), null);
            //                hyperlink.Foreground = null;
            //                hyperlink.UnderlineStyle = UnderlineStyle.None;
            //                hyperlink.FontFamily = BootStrapper.Current.Resources["SpoilerFontFamily"] as FontFamily;
            //                //hyperlink.Foreground = foreground;

            //                spoiler ??= new TextHighlighter();
            //                spoiler.Ranges.Add(new TextRange { StartIndex = part.Offset + entity.Offset - workaround, Length = entity.Length });

            //                var temp = direct.GetXamlDirectObject(hyperlink);

            //                direct.AddToCollection(inlines, temp);
            //                local = direct.GetXamlDirectObjectProperty(temp, XamlPropertyIndex.Span_Inlines);
            //            }
            //            else if (entity.HasFlag(Common.TextStyle.Mention) || entity.HasFlag(Common.TextStyle.Url))
            //            {
            //                if (entity.Type is TextEntityTypeMentionName or TextEntityTypeTextUrl)
            //                {
            //                    var hyperlink = new Hyperlink();
            //                    object data;
            //                    if (entity.Type is TextEntityTypeTextUrl textUrl)
            //                    {
            //                        data = textUrl.Url;
            //                        MessageHelper.SetEntityData(hyperlink, textUrl.Url);
            //                        MessageHelper.SetEntityType(hyperlink, entity.Type);

            //                        _links.Add(hyperlink);
            //                        ToolTipService.SetToolTip(hyperlink, textUrl.Url);
            //                    }
            //                    else if (entity.Type is TextEntityTypeMentionName mentionName)
            //                    {
            //                        data = mentionName.UserId;
            //                    }

            //                    hyperlink.Click += (s, args) => Entity_Click(entity.Offset, entity.Length, entity.Type, null);
            //                    hyperlink.Foreground = HyperlinkForeground ?? GetBrush("MessageForegroundLinkBrush");
            //                    hyperlink.UnderlineStyle = HyperlinkStyle;
            //                    hyperlink.FontWeight = HyperlinkFontWeight;
            //                    hyperlink.UnderlineStyle = UnderlineStyle.None;

            //                    var temp = direct.GetXamlDirectObject(hyperlink);

            //                    direct.AddToCollection(inlines, temp);
            //                    local = direct.GetXamlDirectObjectProperty(temp, XamlPropertyIndex.Span_Inlines);
            //                }
            //                else
            //                {
            //                    var hyperlink = new Hyperlink();
            //                    //var original = entities.FirstOrDefault(x => x.Offset <= entity.Offset && x.Offset + x.Length >= entity.End);

            //                    var data = text.Substring(entity.Offset, entity.Length);

            //                    //if (original != null)
            //                    //{
            //                    //    data = text.Substring(original.Offset, original.Length);
            //                    //}

            //                    hyperlink.Click += (s, args) => Entity_Click(entity.Offset, entity.Length, entity.Type, data);
            //                    hyperlink.Foreground = HyperlinkForeground ?? GetBrush("MessageForegroundLinkBrush");
            //                    hyperlink.UnderlineStyle = HyperlinkStyle;
            //                    hyperlink.FontWeight = HyperlinkFontWeight;
            //                    hyperlink.UnderlineStyle = entity.Type is TextEntityTypeUrl
            //                        ? UnderlineStyle.Single
            //                        : UnderlineStyle.None;

            //                    //if (entity.Type is TextEntityTypeUrl || entity.Type is TextEntityTypeEmailAddress || entity.Type is TextEntityTypeBankCardNumber)
            //                    {
            //                        MessageHelper.SetEntityData(hyperlink, data);
            //                        MessageHelper.SetEntityType(hyperlink, entity.Type);
            //                    }

            //                    var temp = direct.GetXamlDirectObject(hyperlink);

            //                    direct.AddToCollection(inlines, temp);
            //                    local = direct.GetXamlDirectObjectProperty(temp, XamlPropertyIndex.Span_Inlines);
            //                }
            //            }

            //            if (entity.Type is TextEntityTypeCustomEmoji customEmoji && ((_ignoreSpoilers && entity.HasFlag(Common.TextStyle.Spoiler)) || !entity.HasFlag(Common.TextStyle.Spoiler)))
            //            {
            //                var data = text.Substring(entity.Offset, entity.Length);

            //                var player = new CustomEmojiIcon();
            //                player.LoopCount = 0;
            //                player.Source = new CustomEmojiFileSource(clientService, customEmoji.CustomEmojiId);
            //                player.HorizontalAlignment = HorizontalAlignment.Left;
            //                player.FlowDirection = FlowDirection.LeftToRight;
            //                player.Margin = new Thickness(0, -2, 0, -6);
            //                player.Style = EmojiStyle;
            //                player.IsHitTestVisible = false;
            //                player.IsEnabled = false;
            //                player.Emoji = data;

            //                var inline = new InlineUIContainer();
            //                inline.Child = player;

            //                // We are working around multiple issues here:
            //                // ZWNJ is always added right after a custom emoji to make sure that the line height always matches Segoe UI.
            //                // RTL/LTR mark is added in case the custom emoji is the first element in the Paragraph.
            //                // This is needed because we can't use TextReadingOrder = DetectFromContent due to a bug
            //                // that causes text selection and hit tests to follow the flow direction rather than the reading order.
            //                // Because of this, we're forced to use TextReadingOrder = UseFlowDirection, and to set each
            //                // Run.FlowDirection to the one calculated by calling GetStringTypeEx on the text of each paragraph.
            //                // Since InlineUIContainer doesn't have a FlowDirection property (and the child flow direction seems to be ignored)
            //                // the first custom emoji in a paragraph with reading order different from the one of the app, would appear on the
            //                // wrong side of the block, thus we add a RTL/LTR mark right before, and the RichTextBlock seems to respect this.

            //                if (entity.Offset == 0 && direction != locale)
            //                {
            //                    direct.AddToCollection(inlines, CreateDirectRun(direct, direction == FlowDirection.RightToLeft ? Icons.RTL : Icons.LTR, direction));
            //                }

            //                // TODO: see if there's a better way
            //                direct.AddToCollection(inlines, direct.GetXamlDirectObject(inline));
            //                direct.AddToCollection(inlines, CreateDirectRun(direct, Icons.ZWNJ, direction));

            //                workaround++;
            //            }
            //            else
            //            {
            //                var run = CreateDirectRun(direct, text.Substring(entity.Offset, entity.Length), direction, fontSize: fontSize);
            //                var decorations = TextDecorations.None;

            //                if (entity.HasFlag(Common.TextStyle.Underline))
            //                {
            //                    decorations |= TextDecorations.Underline;
            //                }
            //                if (entity.HasFlag(Common.TextStyle.Strikethrough))
            //                {
            //                    decorations |= TextDecorations.Strikethrough;
            //                }

            //                if (decorations != TextDecorations.None)
            //                {
            //                    direct.SetEnumProperty(run, XamlPropertyIndex.TextElement_TextDecorations, (uint)decorations);
            //                }

            //                if (entity.HasFlag(Common.TextStyle.Bold))
            //                {
            //                    direct.SetObjectProperty(run, XamlPropertyIndex.TextElement_FontWeight, FontWeights.SemiBold);
            //                }
            //                if (entity.HasFlag(Common.TextStyle.Italic))
            //                {
            //                    direct.SetEnumProperty(run, XamlPropertyIndex.TextElement_FontStyle, (uint)FontStyle.Italic);
            //                }

            //                direct.AddToCollection(local, run);
            //            }
            //        }

            //        previous = entity.Offset + entity.Length;
            //    }

            //    if (text.Length > previous)
            //    {
            //        direct.AddToCollection(inlines, CreateDirectRun(direct, text.Substring(previous), direction, fontSize: fontSize));
            //    }

            //    // ZWJ is added to workaround a crash caused by emoji ad the end of a paragraph that is being highlighted
            //    direct.AddToCollection(inlines, CreateDirectRun(direct, Icons.ZWJ, direction));
            //    direct.AddToCollection(blocks, paragraph);

            //    if (part.Offset == 0)
            //    {
            //        firstType = type;
            //    }

            //    lastType = type;
            //}

            ////Padding = new Thickness(0, firstFormatted ? 4 : 0, 0, 0);

            ////ContentPanel.MaxWidth = preformatted ? double.PositiveInfinity : 432;

            ////_isFormatted = runs.Count > 0 || fontSize != 0;
            //HasCodeBlocks = preformatted;

            //if (spoiler?.Ranges.Count > 0)
            //{
            //    spoiler.Foreground = new SolidColorBrush(Colors.Transparent);
            //    spoiler.Background = new SolidColorBrush(Colors.Black);

            //    _invalidateSpoilers = _spoiler != null;
            //    _spoiler = spoiler;
            //}
            //else
            //{
            //    _invalidateSpoilers = false;
            //    _spoiler = null;
            //}

            //var topPadding = 0d;
            //var bottomPadding = false;

            //if (firstType is TextParagraphTypeMonospace { Language.Length: > 0 })
            //{
            //    topPadding = 22 + 6;
            //}
            //else if (firstType is not null)
            //{
            //    topPadding = 6;
            //}

            //if (AdjustLineEnding && styled.Paragraphs.Count > 0)
            //{
            //    var direction = styled.Paragraphs[^1].Direction switch
            //    {
            //        TextDirectionality.LeftToRight => FlowDirection.LeftToRight,
            //        TextDirectionality.RightToLeft => FlowDirection.RightToLeft,
            //        _ => locale
            //    };

            //    if (direction != locale || lastType is not null)
            //    {
            //        bottomPadding = true;
            //    }
            //}

            //HasLineEnding = bottomPadding;

            //Below.Margin = new Thickness(0, topPadding, 0, 0);
            //TextBlock.Margin = new Thickness(0, topPadding, 0, 0);
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            Below.Children.Clear();

            foreach (var paragraph in _codeBlocks)
            {
                var block = paragraph.Paragraph;
                var start = block.ContentStart.GetCharacterRect(block.ContentStart.LogicalDirection);
                var end = block.ContentEnd.GetCharacterRect(block.ContentEnd.LogicalDirection);

                var startY = Math.Round(start.Y);
                var endBottom = Math.Round(end.Bottom);

                if (paragraph.Type is TextParagraphTypeMonospace monospace && monospace.Language.Length > 0)
                {
                    var rect = new BlockCode();
                    rect.Width = e.NewSize.Width;
                    rect.Height = Math.Max(endBottom - startY + 6 + 22, 0);
                    rect.LanguageName = monospace.Language;
                    Canvas.SetTop(rect, startY - 2 - 22);

                    Below.Children.Add(rect);
                }
                else
                {
                    var rect = new BlockQuote();
                    rect.Width = e.NewSize.Width;
                    rect.Height = Math.Max(endBottom - startY + 6, 0);
                    rect.Glyph = block.FontSize == Theme.Current.MessageFontSize ? Icons.CodeFilled16 : Icons.QuoteBlockFilled16;
                    Canvas.SetTop(rect, startY - 2);

                    Below.Children.Add(rect);
                }
            }
        }

        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            var resources = sender.ActualTheme == ElementTheme.Light ? _light : _dark;

            foreach (var item in _brushes)
            {
                item.Value.Color = resources[item.Key];
            }
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //private Run CreateRun(string text, FlowDirection direction, FontWeight? fontWeight = null, FontFamily fontFamily = null, double fontSize = 0)
        //{
        //    var direct = XamlDirect.GetDefault();
        //    var run = direct.CreateInstance(XamlTypeIndex.Run);
        //    direct.SetStringProperty(run, XamlPropertyIndex.Run_Text, text);
        //    direct.SetEnumProperty(run, XamlPropertyIndex.Run_FlowDirection, (uint)direction);

        //    if (fontWeight != null)
        //    {
        //        direct.SetObjectProperty(run, XamlPropertyIndex.TextElement_FontWeight, fontWeight.Value);
        //    }

        //    if (fontFamily != null)
        //    {
        //        direct.SetObjectProperty(run, XamlPropertyIndex.TextElement_FontFamily, fontFamily);
        //    }

        //    if (fontSize > 0)
        //    {
        //        direct.SetDoubleProperty(run, XamlPropertyIndex.TextElement_FontSize, fontSize);
        //    }

        //    return direct.GetObject(run) as Run;
        //}

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //private IXamlDirectObject CreateDirectRun(XamlDirect direct, string text, FlowDirection direction, FontWeight? fontWeight = null, FontFamily fontFamily = null, double fontSize = 0)
        //{
        //    var run = direct.CreateInstance(XamlTypeIndex.Run);
        //    direct.SetStringProperty(run, XamlPropertyIndex.Run_Text, text);
        //    direct.SetEnumProperty(run, XamlPropertyIndex.Run_FlowDirection, (uint)direction);

        //    if (fontWeight != null)
        //    {
        //        direct.SetObjectProperty(run, XamlPropertyIndex.TextElement_FontWeight, fontWeight.Value);
        //    }

        //    if (fontFamily != null)
        //    {
        //        direct.SetObjectProperty(run, XamlPropertyIndex.TextElement_FontFamily, fontFamily);
        //    }

        //    if (fontSize > 0)
        //    {
        //        direct.SetDoubleProperty(run, XamlPropertyIndex.TextElement_FontSize, fontSize);
        //    }

        //    return run;
        //}

        #region PreCode

        private async void ProcessCodeBlock(InlineCollection inlines, string text, string language)
        {
            try
            {
                var tokens = await SyntaxToken.TokenizeAsync(language.ToLowerInvariant(), text);

                inlines.Clear();
                ProcessCodeBlock(inlines, tokens.Children);
                inlines.Add(Icons.ZWJ);
            }
            catch
            {
                // Tokenization may fail
            }
        }

        private void ProcessCodeBlock(InlineCollection inlines, IList<Token> tokens)
        {
            foreach (var token in tokens)
            {
                if (token is SyntaxToken syntax)
                {
                    var color = GetColor(syntax.Type);
                    if (color == null && syntax.Alias.Length > 0)
                    {
                        color = GetColor(syntax.Alias);
                    }

                    var span = new Span
                    {
                        FontFamily = new FontFamily("Consolas")
                    };

                    if (color != null)
                    {
                        span.Foreground = color;
                    }

                    if (syntax.Type == "bold")
                    {
                        span.FontWeight = FontWeights.SemiBold;
                    }
                    else if (syntax.Type == "italic")
                    {
                        //span.FontStyle = FontStyle.Italic;
                    }

                    ProcessCodeBlock(span.Inlines, syntax.Children);
                    inlines.Add(span);
                }
                else if (token is TextToken text)
                {
                    inlines.Add(new Run
                    {
                        Text = text.Value
                    });
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

        private Brush GetBrush(string key)
        {
            //var message = _message;
            //if (message == null)
            //{
            //    return null;
            //}

            //if (message.IsOutgoing && !message.IsChannelPost)
            //{
            //    if (ActualTheme == ElementTheme.Light)
            //    {
            //        return ThemeOutgoing.Light[key].Brush;
            //    }
            //    else
            //    {
            //        return ThemeOutgoing.Dark[key].Brush;
            //    }
            //}
            //else
            if (ActualTheme == ElementTheme.Light)
            {
                return ThemeIncoming.Light[key].Brush;
            }
            else
            {
                return ThemeIncoming.Dark[key].Brush;
            }
        }

        private void Entity_Click(int offset, int length, TextEntityType type, object data)
        {
            TextEntityClick?.Invoke(this, new TextEntityClickEventArgs(offset, length, type, data));
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

        #region TextStyle

        public Style TextStyle
        {
            get { return (Style)GetValue(TextStyleProperty); }
            set { SetValue(TextStyleProperty, value); }
        }

        public static readonly DependencyProperty TextStyleProperty =
            DependencyProperty.Register("TextStyle", typeof(Style), typeof(FormattedTextBlock), new PropertyMetadata(null));

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

        public SolidColorBrush HyperlinkForeground { get; set; }

        //public FontWeight HyperlinkFontWeight { get; set; } = FontWeights.Normal;

        #endregion

        public bool HasOverflowContent => TextBlock?.HasOverflowContent ?? false;
    }
}
