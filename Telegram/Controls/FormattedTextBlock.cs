//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Native;
using Telegram.Native.Highlight;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Core.Direct;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

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

    public class FormattedTextBlock : Control
    {
        private IClientService _clientService;
        private StyledText _text;
        private double _fontSize;

        private string _query;

        private bool _isHighlighted;
        private bool _ignoreSpoilers = false;

        private readonly List<Paragraph> _codeBlocks = new();

        private TextHighlighter _spoiler;
        private bool _invalidateSpoilers;

        private Canvas Below;
        private RichTextBlock TextBlock;

        private bool _templateApplied;

        public FormattedTextBlock()
        {
            DefaultStyleKey = typeof(FormattedTextBlock);
        }

        public bool AdjustLineEnding { get; set; }

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

        public void Clear()
        {
            _clientService = null;
            _text = null;

            _query = null;
            _spoiler = null;

            Cleanup();
        }

        public void Cleanup()
        {
            // TODO: clear inlines here?
            // Probably not needed
        }

        private void Adjust()
        {
            if (TextBlock?.Blocks.Count > 0 && TextBlock.Blocks[^1] is Paragraph existing)
            {
                TextBlock.Blocks.Add(new Paragraph());
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
                    _codeBlocks.Clear();
                    TextBlock.Blocks.Clear();
                }
            }
        }

        public void SetText(IClientService clientService, StyledText styled, double fontSize = 0)
        {
            _clientService = clientService;
            _text = styled;
            _fontSize = fontSize;

            if (!_templateApplied)
            {
                return;
            }

            // PERF: fast path if both model and view have one paragraph with one run
            if (styled != null && styled.Paragraphs.Count == 1 && styled.Paragraphs[0].Entities.Count == 0 && styled.Text.Length > 0)
            {
                if (TextBlock.Blocks.Count == 1 && TextBlock.Blocks[0] is Paragraph paragraph && paragraph.Inlines.Count == 1 && paragraph.Inlines[0] is Run run)
                {
                    run.Text = styled.Text;
                    return;
                }
            }

            var direct = XamlDirect.GetDefault();
            var directBlock = direct.GetXamlDirectObject(TextBlock);
            var blocks = direct.GetXamlDirectObjectProperty(directBlock, XamlPropertyIndex.RichTextBlock_Blocks);

            _codeBlocks.Clear();
            direct.ClearCollection(blocks);

            if (string.IsNullOrEmpty(styled?.Text))
            {
                return;
            }

            TextHighlighter spoiler = null;

            var preformatted = false;
            var lastFormatted = false;
            var firstFormatted = false;

            var text = styled.Text;
            var workaround = 0;

            foreach (var part in styled.Paragraphs)
            {
                // This should not happen, but it does.
                text = styled.Text.Substring(part.Offset, Math.Min(part.Length, styled.Text.Length - part.Offset));
                lastFormatted = false;

                var runs = part.Runs;
                var previous = 0;

                var paragraph = direct.CreateInstance(XamlTypeIndex.Paragraph);
                var inlines = direct.GetXamlDirectObjectProperty(paragraph, XamlPropertyIndex.Paragraph_Inlines);

                if (AutoFontSize)
                {
                    direct.SetDoubleProperty(paragraph, XamlPropertyIndex.TextElement_FontSize, Theme.Current.MessageFontSize);
                }

                if (part.Type == Common.ParagraphStyle.Quote)
                {
                    lastFormatted = true;

                    if (!firstFormatted && part == styled.Paragraphs[0])
                    {
                        firstFormatted = true;
                    }

                    var first = part == styled.Paragraphs[0];
                    var last = part == styled.Paragraphs[^1];

                    var temp = direct.GetObject(paragraph) as Paragraph;
                    temp.Margin = new Thickness(11, first ? 10 : 6, 24, last ? 0 : 8);
                    temp.FontSize = Theme.Current.MessageFontSize - 2;

                    _codeBlocks.Add(temp);
                }

                foreach (var entity in runs)
                {
                    if (entity.Offset > previous)
                    {
                        direct.AddToCollection(inlines, CreateDirectRun(direct, text.Substring(previous, entity.Offset - previous), fontSize: fontSize));
                    }

                    if (entity.Length + entity.Offset > text.Length)
                    {
                        previous = entity.Offset + entity.Length;
                        continue;
                    }

                    if (entity.HasFlag(Common.TextStyle.Monospace))
                    {
                        var data = text.Substring(entity.Offset, entity.Length);

                        if (entity.Type is TextEntityTypeCode)
                        {
                            var hyperlink = new Hyperlink();
                            hyperlink.Click += (s, args) => Entity_Click(entity.Offset, entity.Length, entity.Type, data);
                            hyperlink.Foreground = TextBlock.Foreground;
                            hyperlink.UnderlineStyle = UnderlineStyle.None;

                            hyperlink.Inlines.Add(CreateRun(data, fontFamily: new FontFamily("Consolas"), fontSize: fontSize));
                            direct.AddToCollection(inlines, direct.GetXamlDirectObject(hyperlink));
                        }
                        else
                        {
                            direct.SetObjectProperty(paragraph, XamlPropertyIndex.TextElement_FontFamily, new FontFamily("Consolas"));
                            direct.AddToCollection(inlines, CreateDirectRun(direct, data));

                            preformatted = true;
                            lastFormatted = true;

                            if (!firstFormatted && part == styled.Paragraphs[0])
                            {
                                firstFormatted = true;
                            }

                            var first = part == styled.Paragraphs[0];
                            var last = part == styled.Paragraphs[^1];

                            var temp = direct.GetObject(paragraph) as Paragraph;
                            temp.Margin = new Thickness(11, first ? 10 : 6, 24, last ? 0 : 8);

                            _codeBlocks.Add(temp);

                            if (entity.Type is TextEntityTypePreCode preCode && preCode.Language.Length > 0)
                            {
                                ProcessCodeBlock(temp.Inlines, data, preCode.Language);
                            }
                        }
                    }
                    else
                    {
                        var local = inlines;

                        if (_ignoreSpoilers is false && entity.HasFlag(Common.TextStyle.Spoiler))
                        {
                            var hyperlink = new Hyperlink();
                            hyperlink.Click += (s, args) => Entity_Click(entity.Offset, entity.Length, new TextEntityTypeSpoiler(), null);
                            hyperlink.Foreground = null;
                            hyperlink.UnderlineStyle = UnderlineStyle.None;
                            hyperlink.FontFamily = BootStrapper.Current.Resources["SpoilerFontFamily"] as FontFamily;
                            //hyperlink.Foreground = foreground;

                            spoiler ??= new TextHighlighter();
                            spoiler.Ranges.Add(new TextRange { StartIndex = part.Offset + entity.Offset - workaround, Length = entity.Length });

                            var temp = direct.GetXamlDirectObject(hyperlink);

                            direct.AddToCollection(inlines, temp);
                            local = direct.GetXamlDirectObjectProperty(temp, XamlPropertyIndex.Span_Inlines);
                        }
                        else if (entity.HasFlag(Common.TextStyle.Mention) || entity.HasFlag(Common.TextStyle.Url))
                        {
                            if (entity.Type is TextEntityTypeMentionName or TextEntityTypeTextUrl)
                            {
                                var hyperlink = new Hyperlink();
                                object data;
                                if (entity.Type is TextEntityTypeTextUrl textUrl)
                                {
                                    data = textUrl.Url;
                                    MessageHelper.SetEntityData(hyperlink, textUrl.Url);
                                    MessageHelper.SetEntityType(hyperlink, entity.Type);

                                    ToolTipService.SetToolTip(hyperlink, textUrl.Url);
                                }
                                else if (entity.Type is TextEntityTypeMentionName mentionName)
                                {
                                    data = mentionName.UserId;
                                }

                                hyperlink.Click += (s, args) => Entity_Click(entity.Offset, entity.Length, entity.Type, null);
                                hyperlink.Foreground = HyperlinkForeground ?? GetBrush("MessageForegroundLinkBrush");
                                hyperlink.UnderlineStyle = HyperlinkStyle;
                                hyperlink.FontWeight = HyperlinkFontWeight;

                                var temp = direct.GetXamlDirectObject(hyperlink);

                                direct.AddToCollection(inlines, temp);
                                local = direct.GetXamlDirectObjectProperty(temp, XamlPropertyIndex.Span_Inlines);
                            }
                            else
                            {
                                var hyperlink = new Hyperlink();
                                //var original = entities.FirstOrDefault(x => x.Offset <= entity.Offset && x.Offset + x.Length >= entity.End);

                                var data = text.Substring(entity.Offset, entity.Length);

                                //if (original != null)
                                //{
                                //    data = text.Substring(original.Offset, original.Length);
                                //}

                                hyperlink.Click += (s, args) => Entity_Click(entity.Offset, entity.Length, entity.Type, data);
                                hyperlink.Foreground = HyperlinkForeground ?? GetBrush("MessageForegroundLinkBrush");
                                hyperlink.UnderlineStyle = HyperlinkStyle;
                                hyperlink.FontWeight = HyperlinkFontWeight;

                                //if (entity.Type is TextEntityTypeUrl || entity.Type is TextEntityTypeEmailAddress || entity.Type is TextEntityTypeBankCardNumber)
                                {
                                    MessageHelper.SetEntityData(hyperlink, data);
                                    MessageHelper.SetEntityType(hyperlink, entity.Type);
                                }

                                var temp = direct.GetXamlDirectObject(hyperlink);

                                direct.AddToCollection(inlines, temp);
                                local = direct.GetXamlDirectObjectProperty(temp, XamlPropertyIndex.Span_Inlines);
                            }
                        }

                        if (entity.Type is TextEntityTypeCustomEmoji customEmoji && ((_ignoreSpoilers && entity.HasFlag(Common.TextStyle.Spoiler)) || !entity.HasFlag(Common.TextStyle.Spoiler)))
                        {
                            //direction ??= NativeUtils.GetDirectionality(text);

                            //var right = direction == TextDirectionality.RightToLeft /*&& entity.Offset > 0 && entity.End < text.Length*/;
                            var player = new CustomEmojiIcon();
                            player.Source = new CustomEmojiFileSource(clientService, customEmoji.CustomEmojiId);
                            player.HorizontalAlignment = HorizontalAlignment.Left;
                            player.FlowDirection = FlowDirection.LeftToRight;
                            player.Margin = new Thickness(0, -2, 0, -6);
                            player.Style = EmojiStyle;
                            player.IsHitTestVisible = false;
                            player.IsEnabled = false;

                            var inline = new InlineUIContainer();
                            inline.Child = player;

                            // TODO: see if there's a better way
                            direct.AddToCollection(inlines, direct.GetXamlDirectObject(inline));
                            direct.AddToCollection(inlines, CreateDirectRun(direct, Icons.ZWJ));

                            workaround++;
                        }
                        else
                        {
                            var run = CreateDirectRun(direct, text.Substring(entity.Offset, entity.Length), fontSize: fontSize);
                            var decorations = TextDecorations.None;

                            if (entity.HasFlag(Common.TextStyle.Underline))
                            {
                                decorations |= TextDecorations.Underline;
                            }
                            if (entity.HasFlag(Common.TextStyle.Strikethrough))
                            {
                                decorations |= TextDecorations.Strikethrough;
                            }

                            if (decorations != TextDecorations.None)
                            {
                                direct.SetEnumProperty(run, XamlPropertyIndex.TextElement_TextDecorations, (uint)decorations);
                            }

                            if (entity.HasFlag(Common.TextStyle.Bold))
                            {
                                direct.SetObjectProperty(run, XamlPropertyIndex.TextElement_FontWeight, FontWeights.SemiBold);
                            }
                            if (entity.HasFlag(Common.TextStyle.Italic))
                            {
                                direct.SetEnumProperty(run, XamlPropertyIndex.TextElement_FontStyle, (uint)FontStyle.Italic);
                            }

                            direct.AddToCollection(local, run);
                        }
                    }

                    previous = entity.Offset + entity.Length;
                }

                if (text.Length > previous)
                {
                    direct.AddToCollection(inlines, CreateDirectRun(direct, text.Substring(previous), fontSize: fontSize));
                }

                // ZWJ is added to workaround a crash caused by emoji ad the end of a paragraph that is being highlighted
                direct.AddToCollection(inlines, CreateDirectRun(direct, Icons.ZWJ));
                direct.AddToCollection(blocks, paragraph);
            }

            //Padding = new Thickness(0, firstFormatted ? 4 : 0, 0, 0);

            //ContentPanel.MaxWidth = preformatted ? double.PositiveInfinity : 432;

            //_isFormatted = runs.Count > 0 || fontSize != 0;
            HasCodeBlocks = preformatted;

            if (spoiler?.Ranges.Count > 0)
            {
                spoiler.Foreground = new SolidColorBrush(Colors.Transparent);
                spoiler.Background = new SolidColorBrush(Colors.Black);

                _invalidateSpoilers = _spoiler != null;
                _spoiler = spoiler;
            }
            else
            {
                _invalidateSpoilers = false;
                _spoiler = null;
            }

            if (firstFormatted)
            {
                Below.Margin = new Thickness(0, 4, 0, 0);
                TextBlock.Margin = new Thickness(0, 4, 0, 0);
            }
            else
            {
                Below.Margin = new Thickness();
                TextBlock.Margin = new Thickness();
            }

            if (AdjustLineEnding && styled.Paragraphs.Count > 0)
            {
                //var direction = NativeUtils.GetDirectionality(text);

                var direction = styled.Paragraphs[^1].Direction;
                if (direction == TextDirectionality.RightToLeft && LocaleService.Current.FlowDirection == FlowDirection.LeftToRight)
                {
                    TextBlock.FlowDirection = FlowDirection.RightToLeft;
                    Adjust();
                }
                else if (direction == TextDirectionality.LeftToRight && LocaleService.Current.FlowDirection == FlowDirection.RightToLeft)
                {
                    TextBlock.FlowDirection = FlowDirection.LeftToRight;
                    Adjust();
                }
                else
                {
                    TextBlock.FlowDirection = LocaleService.Current.FlowDirection;

                    if (lastFormatted)
                    {
                        Adjust();
                    }
                }
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            Below.Children.Clear();

            foreach (var block in _codeBlocks)
            {
                var start = block.ContentStart.GetCharacterRect(block.ContentStart.LogicalDirection);
                var end = block.ContentEnd.GetCharacterRect(block.ContentEnd.LogicalDirection);

                var rect = new BlockQuote();
                rect.Width = e.NewSize.Width;
                rect.Height = Math.Max(end.Bottom - start.Y + 6, 0);
                rect.Glyph = block.FontSize == Theme.Current.MessageFontSize ? Icons.CodeBlockFilled16 : Icons.QuoteBlockFilled16;
                Canvas.SetTop(rect, start.Y - 2);

                Below.Children.Add(rect);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Run CreateRun(string text, FontWeight? fontWeight = null, FontFamily fontFamily = null, double fontSize = 0)
        {
            var direct = XamlDirect.GetDefault();
            var run = direct.CreateInstance(XamlTypeIndex.Run);
            direct.SetStringProperty(run, XamlPropertyIndex.Run_Text, text);

            if (fontWeight != null)
            {
                direct.SetObjectProperty(run, XamlPropertyIndex.TextElement_FontWeight, fontWeight.Value);
            }

            if (fontFamily != null)
            {
                direct.SetObjectProperty(run, XamlPropertyIndex.TextElement_FontFamily, fontFamily);
            }

            if (fontSize > 0)
            {
                direct.SetDoubleProperty(run, XamlPropertyIndex.TextElement_FontSize, fontSize);
            }

            return direct.GetObject(run) as Run;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IXamlDirectObject CreateDirectRun(XamlDirect direct, string text, FontWeight? fontWeight = null, FontFamily fontFamily = null, double fontSize = 0)
        {
            var run = direct.CreateInstance(XamlTypeIndex.Run);
            direct.SetStringProperty(run, XamlPropertyIndex.Run_Text, text);

            if (fontWeight != null)
            {
                direct.SetObjectProperty(run, XamlPropertyIndex.TextElement_FontWeight, fontWeight.Value);
            }

            if (fontFamily != null)
            {
                direct.SetObjectProperty(run, XamlPropertyIndex.TextElement_FontFamily, fontFamily);
            }

            if (fontSize > 0)
            {
                direct.SetDoubleProperty(run, XamlPropertyIndex.TextElement_FontSize, fontSize);
            }

            return run;
        }

        #region PreCode

        private async void ProcessCodeBlock(InlineCollection inlines, string text, string language)
        {
            var tokens = await SyntaxToken.TokenizeAsync(language, text);

            inlines.Clear();
            ProcessCodeBlock(inlines, tokens.Children);
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
                        span.FontStyle = FontStyle.Italic;
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
            foreach (Paragraph block in TextBlock.Blocks)
            {
                foreach (var element in block.Inlines)
                {
                    if (element is Hyperlink)
                    {
                        ToolTipService.SetToolTip(element, null);
                    }
                }
            }

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

        public FontWeight HyperlinkFontWeight { get; set; } = FontWeights.Normal;

        #endregion

        public bool HasOverflowContent => TextBlock?.HasOverflowContent ?? false;
    }
}
