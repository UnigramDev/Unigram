//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using Telegram.Common;
using Telegram.Native;
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
        public TextEntityClickEventArgs(TextEntityType type, object data)
        {
            Type = type;
            Data = data;
        }

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

        private TextHighlighter _spoiler;

        private RichTextBlock TextBlock;

        private bool _templateApplied;

        public FormattedTextBlock()
        {
            DefaultStyleKey = typeof(FormattedTextBlock);
        }

        public bool AdjustLineEnding { get; set; }

        public bool IsPreformatted { get; private set; }

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
            TextBlock = GetTemplateChild(nameof(TextBlock)) as RichTextBlock;
            TextBlock.ContextMenuOpening += _contextMenuOpening;

            _templateApplied = true;

            if (_clientService != null && _text != null)
            {
                SetText(_clientService, _text, _fontSize);

                if (_query != null || _spoiler != null)
                {
                    SetQuery(string.Empty);
                }
            }
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
                existing.Inlines.Add(new LineBreak());
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

        public void SetQuery(string query)
        {
            if ((_query ?? string.Empty) == (query ?? string.Empty) && _isHighlighted == (_spoiler != null))
            {
                return;
            }

            _query = query;

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
                        var highligher = new TextHighlighter();
                        highligher.Foreground = new SolidColorBrush(Colors.White);
                        highligher.Background = new SolidColorBrush(Colors.Orange);
                        highligher.Ranges.Add(new TextRange { StartIndex = find, Length = query.Length });

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

            TextBlock.Blocks.Clear();

            if (string.IsNullOrEmpty(styled?.Text))
            {
                return;
            }

            TextHighlighter spoiler = null;

            var preformatted = false;

            var direct = XamlDirect.GetDefault();
            var text = styled.Text;

            var parag = 0;

            foreach (var part in styled.Paragraphs)
            {
                text = styled.Text.Substring(part.Offset, part.Length);

                var runs = part.Runs;
            var previous = 0;

                var paragraph = direct.CreateInstance(XamlTypeIndex.Paragraph);
            var inlines = direct.GetXamlDirectObjectProperty(paragraph, XamlPropertyIndex.Paragraph_Inlines);

                if (AutoFontSize)
            {
                    direct.SetDoubleProperty(paragraph, XamlPropertyIndex.TextElement_FontSize, Theme.Current.MessageFontSize);
            }

            foreach (var entity in runs)
            {
                if (entity.Offset > previous)
                {
                    direct.AddToCollection(inlines, CreateDirectRun(text.Substring(previous, entity.Offset - previous), fontSize: fontSize));
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
                        hyperlink.Click += (s, args) => Entity_Click(entity.Type, data);
                        hyperlink.Foreground = TextBlock.Foreground;
                        hyperlink.UnderlineStyle = UnderlineStyle.None;

                        hyperlink.Inlines.Add(CreateRun(data, fontFamily: new FontFamily("Consolas"), fontSize: fontSize));
                        direct.AddToCollection(inlines, direct.GetXamlDirectObject(hyperlink));
                    }
                    else
                    {
                        direct.AddToCollection(inlines, CreateDirectRun(data, fontFamily: new FontFamily("Consolas"), fontSize: fontSize));
                        preformatted = entity.Type is TextEntityTypePre or TextEntityTypePreCode;
                    }
                }
                else
                {
                    var local = inlines;

                    if (_ignoreSpoilers is false && entity.HasFlag(Common.TextStyle.Spoiler))
                    {
                        var hyperlink = new Hyperlink();
                        hyperlink.Click += (s, args) => Entity_Click(new TextEntityTypeSpoiler(), null);
                        hyperlink.Foreground = null;
                        hyperlink.UnderlineStyle = UnderlineStyle.None;
                        hyperlink.FontFamily = BootStrapper.Current.Resources["SpoilerFontFamily"] as FontFamily;
                        //hyperlink.Foreground = foreground;

                        spoiler ??= new TextHighlighter();
                            spoiler.Ranges.Add(new TextRange { StartIndex = part.Offset + entity.Offset - parag, Length = entity.Length });

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

                            hyperlink.Click += (s, args) => Entity_Click(entity.Type, null);
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
                            var original = entities.FirstOrDefault(x => x.Offset <= entity.Offset && x.Offset + x.Length >= entity.End);

                            var data = text.Substring(entity.Offset, entity.Length);

                            if (original != null)
                            {
                                data = text.Substring(original.Offset, original.Length);
                            }

                            hyperlink.Click += (s, args) => Entity_Click(entity.Type, data);
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

                    if (entity.Type is TextEntityTypeCustomEmoji customEmoji)
                    {
                        var player = new CustomEmojiIcon();
                        player.Source = new CustomEmojiFileSource(clientService, customEmoji.CustomEmojiId);
                        player.Margin = new Thickness(-20, -4, 0, -4);

                        var inline = new InlineUIContainer();
                        inline.Child = player;

                        // TODO: see if there's a better way
                        direct.AddToCollection(inlines, CreateDirectRun("\U0001F921", fontFamily: BootStrapper.Current.Resources["SpoilerFontFamily"] as FontFamily));
                        direct.AddToCollection(inlines, direct.GetXamlDirectObject(inline));
                    }
                    else
                    {
                        var run = CreateDirectRun(text.Substring(entity.Offset, entity.Length), fontSize: fontSize);
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
                    direct.AddToCollection(inlines, CreateDirectRun(text.Substring(previous), fontSize: fontSize));
                }

                TextBlock.Blocks.Add(direct.GetObject(paragraph) as Paragraph);
                parag++;
                }

            //ContentPanel.MaxWidth = preformatted ? double.PositiveInfinity : 432;

            //_isFormatted = runs.Count > 0 || fontSize != 0;
            IsPreformatted = preformatted;

            if (spoiler?.Ranges.Count > 0)
            {
                spoiler.Foreground = new SolidColorBrush(Colors.Transparent);
                spoiler.Background = new SolidColorBrush(Colors.Black);

                _spoiler = spoiler;
            }
            else
            {
                _spoiler = null;
            }

            if (AdjustLineEnding)
            {
                var direction = NativeUtils.GetDirectionality(text);
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
                }
            }
        }

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

        private IXamlDirectObject CreateDirectRun(string text, FontWeight? fontWeight = null, FontFamily fontFamily = null, double fontSize = 0)
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

            return run;
        }

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

        private void Entity_Click(TextEntityType type, object data)
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

            TextEntityClick?.Invoke(this, new TextEntityClickEventArgs(type, data));
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
