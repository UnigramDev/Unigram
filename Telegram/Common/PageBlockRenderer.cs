//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Controls.Messages.Content;
using Telegram.Controls.Messages.Service;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Common
{
    /// <summary>
    /// The host-specific parts of rendering a page: everything a PageBlock needs
    /// that differs between a rich message in a bubble and a full instant view.
    /// </summary>
    public interface IPageBlockContext
    {
        /// <summary>Where block styles are looked up (the control template's dictionary, or the page's).</summary>
        ResourceDictionary Resources { get; }

        /// <summary>True once the host is loaded — text blocks only subscribe while it is.</summary>
        bool IsConnected { get; }

        /// <summary>Whether new text blocks start showing a loading skeleton (streaming).</summary>
        bool IsSkeletonVisible { get; }

        /// <summary>
        /// Media blocks reuse the message content controls, so each needs a message to sit in.
        /// </summary>
        MessageViewModel CreateMessage(long id, MessageContent content);

        /// <summary>A link or entity was tapped. In-page anchors are handled before this is called.</summary>
        void TextEntityClick(FormattedTextBlock sender, TextEntityClickEventArgs args);

        /// <summary>
        /// A plain url was activated by something that isn't a text entity — a related
        /// article, for instance. A page opens it as an instant view when it can; a
        /// message hands it to the usual link handling.
        /// </summary>
        void OpenUrl(string url);

        /// <summary>
        /// A pageBlockButtonRow / richTextButton was tapped. A rich message can answer
        /// every button type (it has a chat and a message id); an instant view can't.
        /// </summary>
        void OpenInlineButton(InlineButton button);
    }

    /// <summary>
    /// Turns a PageBlock tree into XAML, with no opinion about how the result is
    /// hosted: <see cref="InstantContent"/> stacks the elements in a panel, while
    /// InstantPage puts each top-level block in a virtualizing list. Everything that
    /// depends on the host goes through <see cref="IPageBlockContext"/>.
    ///
    /// This is the single implementation of the block -> element mapping. Adding a
    /// PageBlock type means touching it once.
    /// </summary>
    public partial class PageBlockRenderer
    {
        private readonly IPageBlockContext _context;

        // Anchor blocks register here so in-page links can scroll to them.
        private readonly Dictionary<string, Border> _anchors = new();

        private readonly double _padding = 12;

        public PageBlockRenderer(IPageBlockContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Finds a registered anchor, for hosts that scroll to one themselves (a page
        /// opened at a #fragment). In-page anchor links are handled internally.
        /// </summary>
        public bool TryGetAnchor(string name, out Border anchor)
        {
            return _anchors.TryGetValue(name, out anchor);
        }

        /// <summary>Drops an anchor the host has removed from its tree.</summary>
        public void RemoveAnchor(string name)
        {
            _anchors.Remove(name);
        }

        /// <summary>Forgets every anchor (the host is rebuilding from scratch).</summary>
        public void ClearAnchors()
        {
            _anchors.Clear();
        }

        public FrameworkElement ProcessBlock(IClientService clientService, PageBlock block, PageBlock parent)
        {
            return block switch
            {
                // IV only
                PageBlockCover cover => ProcessCover(clientService, cover),
                PageBlockAuthorDate authorDate => ProcessAuthorDate(clientService, authorDate),
                PageBlockEmbeddedPost embedPost => ProcessEmbedPost(clientService, embedPost),
                PageBlockEmbedded embed => ProcessEmbed(clientService, embed),
                PageBlockRelatedArticles relatedArticles => ProcessRelatedArticles(clientService, relatedArticles),
                PageBlockHeader or PageBlockSubheader or PageBlockTitle or PageBlockSubtitle or PageBlockKicker => ProcessText(clientService, block, false),
                // Rich messages only
                PageBlockThinking thinking => ProcessThinking(clientService, thinking),
                // All
                PageBlockFooter or PageBlockParagraph or PageBlockSectionHeading => ProcessText(clientService, block, false),
                PageBlockBlockQuote blockquote => ProcessBlockquote(clientService, blockquote),
                PageBlockDivider divider => ProcessDivider(clientService, divider),
                PageBlockPhoto photo => ProcessPhoto(clientService, photo, parent),
                PageBlockList list => ProcessList(clientService, list),
                PageBlockVideo video => ProcessVideo(clientService, video, parent),
                PageBlockAnimation animation => ProcessAnimation(clientService, animation),
                PageBlockSlideshow slideshow => ProcessSlideshow(clientService, slideshow),
                PageBlockCollage collage => ProcessCollage(clientService, collage),
                PageBlockPullQuote pullquote => ProcessPullquote(clientService, pullquote),
                PageBlockExpandableBlockQuote expandable => ProcessExpandableBlockquote(clientService, expandable),
                PageBlockDocument document => ProcessDocument(clientService, document),
                PageBlockButtonRow buttonRow => ProcessButtonRow(clientService, buttonRow),
                PageBlockAnchor anchor => ProcessAnchor(clientService, anchor),
                PageBlockPreformatted preformatted => ProcessPreformatted(clientService, preformatted),
                PageBlockChatLink channel => ProcessChannel(clientService, channel),
                PageBlockDetails details => ProcessDetails(clientService, details),
                PageBlockTable table => ProcessTable(clientService, table),
                PageBlockMap map => ProcessMap(clientService, map),
                PageBlockAudio audio => ProcessAudio(clientService, audio),
                PageBlockVoiceNote voiceNote => ProcessVoiceNote(clientService, voiceNote),
                PageBlockMathematicalExpression math => ProcessMath(clientService, math),
                // pageBlockUnsupported, or a block this renderer doesn't handle: either way
                // the build is too old to show it.
                _ => ProcessUnsupported(clientService),
            };
        }

        #region 3.0

        private FrameworkElement ProcessThinking(IClientService clientService, PageBlockThinking thinking)
        {
            var text = ProcessText(clientService, thinking, false);

            // TODO: animation

            return text;
        }

        private FrameworkElement ProcessMath(IClientService clientService, PageBlockMathematicalExpression math)
        {
            var tex = new RichMathImage
            {
                Source = math.Expression
            };

            if (tex.IsValid)
            {
                // TODO: Max width
                if (tex.PixelWidth > 432)
                {
                    return new ScrollViewer
                    {
                        Content = tex,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                        HorizontalScrollMode = ScrollMode.Auto,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                        VerticalScrollMode = ScrollMode.Disabled
                    };
                }

                return tex;
            }

            return ProcessText(clientService, new PageBlockParagraph(new RichTextPlain(math.Expression)), false);
        }

        #endregion

        #region 2.0

        private FrameworkElement ProcessMap(IClientService clientService, PageBlockMap map)
        {
            var message = CreateMessage(clientService, new MessageLocation(map.Location));

            var content = new LocationContent(message);
            content.HorizontalAlignment = HorizontalAlignment.Center;
            content.ClearValue(FrameworkElement.MaxWidthProperty);
            content.ClearValue(FrameworkElement.MaxHeightProperty);

            //var image = new ImageView();
            //image.Constraint = map;
            //image.XamlRoot = XamlRoot;
            //image.SetSource(clientService, map.Location, map.Width, map.Height, 0);

            var caption = ProcessCaption(clientService, map.Caption);
            if (caption != null)
            {
                caption.Margin = new Thickness(0, 8, 0, 0);

                var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };
                panel.Children.Add(content);
                panel.Children.Add(caption);

                return panel;
            }

            return content;
        }

        private FrameworkElement ProcessRelatedArticles(IClientService clientService, PageBlockRelatedArticles relatedArticles)
        {
            var panel = new StackPanel();

            var header = ProcessText(clientService, relatedArticles, false);
            if (header != null)
            {
                var border = new Border { Style = _context.Resources["BlockRelatedArticlesHeaderPanelStyle"] as Style };
                border.Child = header;

                panel.Children.Add(border);
            }

            foreach (var article in relatedArticles.Articles)
            {
                var grid = new Grid();
                grid.ColumnDefinitions.Add(1, GridUnitType.Star);
                grid.ColumnDefinitions.Add(1, GridUnitType.Auto);
                grid.RowDefinitions.Add(1, GridUnitType.Auto);
                grid.RowDefinitions.Add(1, GridUnitType.Auto);

                var title = new TextBlock { Text = article.Title };
                var description = new TextBlock { TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 2, Style = _context.Resources["BlockAuthorDateTextBlockStyle"] as Style };

                if (string.IsNullOrEmpty(article.Author))
                {
                    description.Text = article.Description;
                }
                else
                {
                    description.Text = article.Author;

                    if (article.PublishDate > 0)
                    {
                        description.Text += " — " + Formatter.Date(article.PublishDate, Strings.chatFullDate);
                    }
                }

                if (article.Photo != null)
                {
                    var photo = new ImageView
                    {
                        Width = 36,
                        Height = 36,
                        Stretch = Stretch.UniformToFill,
                        VerticalAlignment = VerticalAlignment.Top
                    };

                    var file = article.Photo.GetSmall()?.Photo;
                    if (file != null)
                    {
                        photo.SetSource(clientService, file, 36, 36);
                    }

                    Grid.SetColumn(photo, 1);
                    Grid.SetRowSpan(photo, 2);

                    grid.Children.Add(photo);
                }

                Grid.SetRow(description, 1);

                grid.Children.Add(title);
                grid.Children.Add(description);

                var button = new SettingsButton { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, Margin = new Thickness(-12, 0, -12, 0) };
                button.Content = grid;

                // Named handler with the url on the Tag, not a lambda closing over the
                // article: one handler instance for the whole list, and it can be removed.
                button.Tag = article.Url;
                button.Click += RelatedArticle_Click;

                panel.Children.Add(button);
            }

            return panel;
        }

        private void RelatedArticle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: string url })
            {
                _context.OpenUrl(url);
            }
        }

        private FrameworkElement ProcessTable(IClientService clientService, PageBlockTable table, bool test = false)
        {
            // A table can arrive with no rows at all, and Max has nothing to reduce over. There is
            // no grid to build either, so render just the caption.
            if (table.Cells.Count == 0)
            {
                return ProcessText(clientService, table, true);
            }

            var grid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var thickness = table.IsBordered ? 1 : 0;
            var padding = table.IsCompact
                ? new Thickness(4, 2, 4, 2)
                : new Thickness(8, 4, 8, 4);

            var columns = table.Cells.Max(row => row.Sum(cell => cell.Colspan));
            var rows = table.Cells.Count;

            for (int i = 0; i < columns; i++)
            {
                // Auto (not Star): the grid is measured with infinite width inside the
                // horizontal ScrollViewer, so Star can't resolve and silently degrades.
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = 200 });
            }

            for (int i = 0; i < rows; i++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            // Tracks slots already covered by a colspan/rowspan from a previously placed cell,
            // so later cells (including ones receiving a rowspan from a row above) flow around them.
            var occupied = new bool[rows, columns];

            var row = 0;
            foreach (var line in table.Cells)
            {
                var column = 0;

                foreach (var cell in line)
                {
                    // Skip past any slots already taken by spans.
                    while (column < columns && occupied[row, column])
                    {
                        column++;
                    }

                    // Defend against malformed input that declares more cells than columns.
                    if (column >= columns)
                    {
                        break;
                    }

                    var colspan = Math.Min(Math.Max(1, cell.Colspan), columns - column);
                    var rowspan = Math.Min(Math.Max(1, cell.Rowspan), rows - row);

                    var lastColumn = column + colspan - 1;
                    var lastRow = row + rowspan - 1;

                    var textBlock = CreateTextBlock();
                    textBlock.TextWrapping = TextWrapping.Wrap;
                    textBlock.TextAlignment = cell.Align switch
                    {
                        PageBlockHorizontalAlignmentCenter => TextAlignment.Center,
                        PageBlockHorizontalAlignmentRight => TextAlignment.Right,
                        _ => TextAlignment.Left
                    };
                    textBlock.VerticalAlignment = cell.Valign switch
                    {
                        PageBlockVerticalAlignmentMiddle => VerticalAlignment.Center,
                        PageBlockVerticalAlignmentBottom => VerticalAlignment.Bottom,
                        _ => VerticalAlignment.Top
                    };

                    if (cell.Text != null)
                    {
                        textBlock.SetText(clientService, cell.Text);
                    }

                    var border = new Border
                    {
                        Style = ResolveCellStyle(cell, row),
                        // Collapsed borders: left only on the first column, top only on the first
                        // row; right/bottom always drawn so adjacent edges don't double up.
                        BorderThickness = new Thickness(
                            column == 0 ? thickness : 0,
                            row == 0 ? thickness : 0,
                            thickness,
                            thickness),
                        // Round only the outer corners, measured against the cell's trailing edge
                        // so spanned cells still round correctly.
                        CornerRadius = new CornerRadius(
                            column == 0 && row == 0 ? 4 : 0,
                            lastColumn == columns - 1 && row == 0 ? 4 : 0,
                            lastColumn == columns - 1 && lastRow == rows - 1 ? 4 : 0,
                            column == 0 && lastRow == rows - 1 ? 4 : 0),
                        Padding = padding,
                        Child = textBlock
                    };

                    Grid.SetRow(border, row);
                    Grid.SetRowSpan(border, rowspan);
                    Grid.SetColumn(border, column);
                    Grid.SetColumnSpan(border, colspan);

                    grid.Children.Add(border);

                    // Mark every covered slot.
                    for (int r = row; r <= lastRow; r++)
                    {
                        for (int c = column; c <= lastColumn; c++)
                        {
                            occupied[r, c] = true;
                        }
                    }

                    column += colspan;
                }

                row++;
            }

            var scroll = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollMode = ScrollMode.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollMode = ScrollMode.Disabled,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Content = grid
            };

            if (test && Constants.DEBUG)
            {
                var panel = new StackPanel();
                panel.Children.Add(scroll);

                var button = new Button { Content = "Rebuild" };
                button.Click += (s, args) =>
                {
                    panel.Children.RemoveAt(0);
                    panel.Children.Insert(0, ProcessTable(clientService, table, false));
                };

                panel.Children.Add(button);
                return panel;
            }

            var caption = ProcessText(clientService, table, true);
            if (caption != null)
            {
                var panel = new StackPanel();
                panel.Children.Add(caption);
                panel.Children.Add(scroll);
                return panel;
            }

            return scroll;

            // Prefer a dedicated stripe style if defined, otherwise fall back to the header
            // style (the previous behaviour) so this stays non-breaking until you add one.
            Style ResolveCellStyle(PageBlockTableCell cell, int rowIndex)
            {
                if (cell.IsHeader)
                {
                    return TableStyle("BlockTableHeaderStyle");
                }

                if (table.IsStriped && rowIndex % 2 == 0)
                {
                    return TableStyle("BlockTableStripeStyle") ?? TableStyle("BlockTableHeaderStyle");
                }

                return TableStyle("BlockTableCellStyle");
            }

            Style TableStyle(string key)
                => _context.Resources.TryGetValue(key, out var value) ? value as Style : null;
        }

        private FrameworkElement ProcessDetails(IClientService clientService, PageBlockDetails details)
        {
            var panel = new StackPanel();

            var header = new SettingsButton { Content = ProcessText(clientService, details, false), Glyph = details.IsOpen ? Icons.ChevronUp : Icons.ChevronDown, Margin = new Thickness(-12, 0, -12, 0) };
            var inner = new StackPanel { Padding = new Thickness(0, 12, 0, 12), Visibility = details.IsOpen ? Visibility.Visible : Visibility.Collapsed };

            panel.Children.Add(header);
            panel.Children.Add(inner);

            foreach (var block in details.Blocks)
            {
                var child = ProcessBlock(clientService, block, details);
                if (child != null)
                {
                    inner.Children.Add(child);
                }
            }

            UpdateSpacing(inner, details.Blocks, false);

            header.Click += (s, args) =>
            {
                inner.Visibility = inner.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                header.Glyph = inner.Visibility == Visibility.Visible ? Icons.ChevronUp : Icons.ChevronDown;
            };

            return panel;
        }

        #endregion

        private FrameworkElement ProcessCover(IClientService clientService, PageBlockCover block)
        {
            return ProcessBlock(clientService, block.Cover, block);
        }

        private FrameworkElement ProcessChannel(IClientService clientService, PageBlockChatLink channel)
        {
            //var chat = channel.Channel as TLChannel;
            //if (chat.IsMin)
            //{
            //    chat = InMemoryCacheService.Current.GetChat(chat.Id) as TLChannel ?? channel.Channel as TLChannel;
            //}

            //var button = new Button
            //{
            //    Style = _context.Resources["ChannelBlockStyle"] as Style,
            //    Content = chat
            //};

            //if (chat.IsMin && chat.HasUsername)
            //{
            //    MTProtoService.Current.ResolveUsernameAsync(chat.Username,
            //        result =>
            //        {
            //            this.BeginOnUIThread(() => button.Content = result.Chats.FirstOrDefault());
            //        });
            //}

            //return button;

            return new Border();
        }

        private FrameworkElement ProcessAuthorDate(IClientService clientService, PageBlockAuthorDate block)
        {
            var parts = new MutableVector<RichText>();

            if (!block.Author.IsNullOrEmpty())
            {
                // Splice the author RichText into the "{0}" placeholder so the surrounding
                // text (e.g. "by {0}") stays correct regardless of where the locale puts it.
                var format = Strings.ArticleByAuthor;
                var placeholder = format.IndexOf("{0}", StringComparison.Ordinal);

                if (placeholder >= 0)
                {
                    var prefix = format.Substring(0, placeholder);
                    var suffix = format.Substring(placeholder + 3);

                    if (prefix.Length > 0)
                    {
                        parts.Add(new RichTextPlain(prefix));
                    }

                    parts.Add(block.Author);

                    if (suffix.Length > 0)
                    {
                        parts.Add(new RichTextPlain(suffix));
                    }
                }
                else
                {
                    parts.Add(block.Author);
                }
            }

            if (block.PublishDate > 0)
            {
                if (parts.Count > 0)
                {
                    parts.Add(new RichTextPlain(" — "));
                }

                parts.Add(new RichTextPlain(Formatter.Date(block.PublishDate, Strings.chatFullDate)));
            }

            if (parts.Count == 0)
            {
                return null;
            }

            var textBlock = CreateTextBlock();
            textBlock.AutoFontSize = false;
            textBlock.Style = BootStrapper.Current.Resources["InfoCaptionFormattedTextBlockStyle"] as Style;
            textBlock.SetText(clientService, new RichTexts(parts));

            return textBlock;
        }

        private FrameworkElement ProcessText(IClientService clientService, PageBlock block, bool caption)
        {
            var text = GetText(block, caption);
            if (PageBlockHelper.IsEmpty(text))
            {
                return null;
            }

            var textBlock = CreateTextBlock();
            textBlock.AutoFontSize = false;
            textBlock.SetText(clientService, text);

            ApplyTextStyle(textBlock, block, caption);
            return textBlock;
        }

        // The RichText a block renders as a styled text block. Returns null for blocks
        // that don't carry one (so ProcessText returns null for them).
        private static RichText GetText(PageBlock block, bool caption)
        {
            return block switch
            {
                PageBlockTitle title => title.Title,
                PageBlockSubtitle subtitle => subtitle.Subtitle,
                PageBlockHeader header => header.Header,
                PageBlockSubheader subheader => subheader.Subheader,
                PageBlockFooter footer => footer.Footer,
                PageBlockParagraph paragraph => paragraph.Text,
                PageBlockPreformatted preformatted => preformatted.Text,
                PageBlockBlockQuote blockquote => blockquote.Credit,
                PageBlockPullQuote pullquote => caption ? pullquote.Credit : pullquote.Text,
                // Holds rich text directly, like the pull quote — not blocks like PageBlockBlockQuote.
                PageBlockExpandableBlockQuote expandable => caption ? expandable.Credit : expandable.Text,
                PageBlockDetails details => details.Header,
                PageBlockTable table => table.Caption,
                PageBlockRelatedArticles relatedArticles => relatedArticles.Header,
                PageBlockKicker kicker => kicker.Kicker,
                PageBlockSectionHeading heading => heading.Text,
                PageBlockThinking thinking => thinking.Text,
                _ => null
            };
        }

        // Applies the per-block-type appearance to the text block produced from GetText.
        private void ApplyTextStyle(FormattedTextBlock textBlock, PageBlock block, bool caption)
        {
            switch (block)
            {
                case PageBlockTitle:
                    textBlock.FontSize = 28;
                    textBlock.FontFamily = new FontFamily("Times New Roman, " + Theme.XamlAutoFontFamily);
                    break;
                case PageBlockSubtitle:
                    textBlock.FontSize = 17;
                    break;
                case PageBlockHeader:
                    textBlock.FontSize = 24;
                    textBlock.FontFamily = new FontFamily("Times New Roman, " + Theme.XamlAutoFontFamily);
                    break;
                case PageBlockSubheader:
                    textBlock.FontSize = 20;
                    textBlock.FontFamily = new FontFamily("Times New Roman, " + Theme.XamlAutoFontFamily);
                    break;
                case PageBlockFooter:
                    textBlock.Style = BootStrapper.Current.Resources["InfoCaptionFormattedTextBlockStyle"] as Style;
                    break;
                case PageBlockPhoto:
                case PageBlockVideo:
                    textBlock.Style = BootStrapper.Current.Resources["InfoCaptionFormattedTextBlockStyle"] as Style;
                    textBlock.TextAlignment = TextAlignment.Center;
                    break;
                case PageBlockSlideshow:
                case PageBlockEmbedded:
                case PageBlockEmbeddedPost:
                    textBlock.Style = BootStrapper.Current.Resources["InfoCaptionFormattedTextBlockStyle"] as Style;
                    break;
                case PageBlockBlockQuote:
                    textBlock.Style = _context.Resources["PullquoteCreditStyle"] as Style;
                    textBlock.FontWeight = FontWeights.SemiBold;
                    textBlock.Margin = new Thickness(0, 0, 0, 0);
                    break;
                case PageBlockExpandableBlockQuote:
                    // Only the credit is styled: the body is the quote's own text and
                    // reads like body copy, exactly as in a plain block quote.
                    if (caption)
                    {
                        textBlock.Style = _context.Resources["PullquoteCreditStyle"] as Style;
                        textBlock.FontWeight = FontWeights.SemiBold;
                        textBlock.Margin = new Thickness(0, 0, 0, 0);
                    }
                    break;
                case PageBlockPullQuote:
                    textBlock.TextAlignment = TextAlignment.Center;
                    if (caption)
                    {
                        textBlock.FontWeight = FontWeights.SemiBold;
                    }
                    else
                    {
                        textBlock.FontStyle = FontStyle.Italic;
                    }
                    break;
                case PageBlockDetails:
                    textBlock.IsTextSelectionEnabled = false;
                    break;
                case PageBlockSectionHeading heading:
                    textBlock.FontSize = 24 - ((heading.Size - 1) * 2);
                    textBlock.FontFamily = new FontFamily("Times New Roman, " + Theme.XamlAutoFontFamily);
                    textBlock.FontWeight = FontWeights.SemiBold;
                    break;
                case PageBlockPreformatted:
                    textBlock.FontFamily = Theme.MonospaceFontFamily;
                    break;
            }
        }

        // Text selection across blocks is handled by _selectionManager (see
        // TextSelectionManager): each FormattedTextBlock implements ISelectableControl,
        // and the manager attaches to LayoutRoot. No per-block wiring is needed here.
        private FormattedTextBlock CreateTextBlock()
        {
            var block = new FormattedTextBlock
            {
                AutoFontSize = true,
                IgnoreSpoilers = false,
                HorizontalTextAlignment = TextAlignment.DetectFromContent,
                TextReadingOrder = TextReadingOrder.UseFlowDirection,
                AdjustLineEnding = false,
            };

            block.ShowHideSkeleton(_context.IsSkeletonVisible);

            Instrumentation.Register(block);

            //if (_context.IsConnected)
            {
                block.TextEntityClick += Block_TextEntityClick;
            }

            // Extended: native selection off (so the inner RichTextBlock doesn't capture
            // the pointer and fight the manager), driven by _selectionManager, with the
            // I-beam handled manually by FormattedTextBlock.
            block.TextSelection = TextSelectionMode.Extended;

            return block;
        }

        private void Block_TextEntityClick(object sender, TextEntityClickEventArgs e)
        {
            if (e.Type is TextEntityTypeTextUrl textUrl && textUrl.Url.StartsWith("#"))
            {
                // An in-page anchor never leaves the page, so it's handled here rather
                // than by the host.
                if (_anchors.TryGetValue(textUrl.Url.TrimStart('#'), out Border anchor))
                {
                    anchor.StartBringIntoView(new BringIntoViewOptions { VerticalAlignmentRatio = 0.0 });
                }
            }
            else
            {
                _context.TextEntityClick(sender as FormattedTextBlock, e);
            }
        }

        private void Text_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            //MessageHelper.Hyperlink_ContextRequested(ViewModel.TranslateService, sender, args, null);
        }

        private void Text_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            e.Handled = true;
        }

        private FrameworkElement ProcessCaption(IClientService clientService, PageBlockCaption caption)
        {
            var textEmpty = PageBlockHelper.IsEmpty(caption?.Text);
            var citeEmpty = PageBlockHelper.IsEmpty(caption?.Credit);

            if (textEmpty && citeEmpty)
            {
                return null;
            }

            FormattedTextBlock textBlock = null;
            if (!textEmpty && !citeEmpty)
            {
                textBlock = CreateTextBlock();
                textBlock.SetText(clientService, new RichTexts([caption.Text, new RichTextPlain("\n"), caption.Credit]));
            }
            else if (!textEmpty)
            {
                textBlock = CreateTextBlock();
                textBlock.SetText(clientService, caption.Text);
            }
            else if (!citeEmpty)
            {
                textBlock = CreateTextBlock();
                textBlock.SetText(clientService, caption.Credit);
            }

            return textBlock;
        }

        private FrameworkElement ProcessUnsupported(IClientService clientService)
        {
            // The prompt needs a message only to reach the client service: without one it
            // can't tell a sideloaded build (cloud update) from a Store one.
            var content = new MessageUnsupportedContent(true);
            content.UpdateMessage(CreateMessage(clientService, new MessageUnsupported()));

            return content;
        }

        private FrameworkElement ProcessPreformatted(IClientService clientService, PageBlockPreformatted block)
        {
            if (block.Text is not RichTextPlain plain || string.IsNullOrEmpty(block.Language))
            {
                var text = ProcessText(clientService, block, false);
                if (text != null)
                {
                    return new BlockQuote
                    {
                        Glyph = Icons.CodeFilled16,
                        Content = text,
                        Padding = new Thickness(8, 4, 24, 6)
                    };
                }
            }
            else
            {
                var formatted = new FormattedText(plain.Text, new[] { new TextEntity(0, plain.Text.Length, new TextEntityTypePreCode(block.Language)) });
                var textBlock = CreateTextBlock();
                textBlock.SetText(clientService, formatted);

                return new BlockQuote
                {
                    Glyph = Icons.CodeFilled16,
                    LanguageName = block.Language,
                    Content = textBlock,
                    Padding = new Thickness(8, 4, 24, 6)
                };
            }

            return null;
        }

        private FrameworkElement ProcessDivider(IClientService clientService, PageBlockDivider block)
        {
            var element = new Rectangle
            {
                Style = _context.Resources["BlockDividerStyle"] as Style
            };

            return element;
        }

        private FrameworkElement ProcessList(IClientService clientService, PageBlockList block)
        {
            var panel = new Grid();
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto), MinWidth = 24 });
            panel.ColumnDefinitions.Add(new ColumnDefinition());

            var row = 0;

            // TODO: spacing between rows?
            panel.RowSpacing = 4;

            foreach (var item in block.Items)
            {
                // TODO: checkbox label here would need to be aligned to the baseline of the RichTextBlock,
                // but this isn't really possible by just using XAML.
                FrameworkElement label;
                if (item.HasCheckbox)
                {
                    label = new CheckBox
                    {
                        IsChecked = item.IsChecked,
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(0, -6, 4, 4),
                        Padding = new Thickness(0),
                        MinWidth = 0,
                        MinHeight = 0
                    };
                }
                else
                {
                    label = new TextBlock
                    {
                        Text = item.Label,
                        TextAlignment = TextAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(0, 0, 8, 0)
                    };
                }

                var stack = new StackPanel();

                foreach (var inner in item.Blocks)
                {
                    var child = ProcessBlock(clientService, inner, block);
                    if (child != null)
                    {
                        stack.Children.Add(child);
                    }
                }

                UpdateSpacing(stack, item.Blocks, false);

                Grid.SetRow(label, row);
                Grid.SetRow(stack, row);
                Grid.SetColumn(stack, 1);

                panel.RowDefinitions.Add(1, GridUnitType.Auto);
                panel.Children.Add(label);
                panel.Children.Add(stack);

                row++;
            }

            return panel;
        }

        private FrameworkElement ProcessBlockquote(IClientService clientService, PageBlockBlockQuote block)
        {
            var content = new StackPanel(); //{ Style = _context.Resources["BlockBlockquoteStyle"] as Style };

            foreach (var item in block.Blocks)
            {
                var child = ProcessBlock(clientService, item, block);
                if (child != null)
                {
                    content.Children.Add(child);
                }
            }

            UpdateSpacing(content, block.Blocks, false);

            var caption = ProcessText(clientService, block, true);
            if (caption != null)
            {
                caption.Style = _context.Resources["PullquoteCreditStyle"] as Style;
                content.Children.Add(caption);
            }

            return new BlockQuote
            {
                Glyph = Icons.QuoteBlockFilled16,
                Content = content,
                Padding = new Thickness(8, 4, 24, 6)
            };
        }

        // Unlike PageBlockBlockQuote this holds rich text directly, so the body comes
        // from ProcessText rather than from nested blocks. That's also what makes the
        // collapse affordance possible here: BlockQuote.IsExpandable only drives a
        // FormattedTextBlock, which is exactly what ProcessText returns — so the quote
        // starts clamped and the chevron expands it. With a credit the content becomes
        // a panel instead, and the control (correctly) reports itself non-expandable.
        // TODO: support expandable + caption?
        private FrameworkElement ProcessExpandableBlockquote(IClientService clientService, PageBlockExpandableBlockQuote block)
        {
            var text = ProcessText(clientService, block, false);
            var caption = ProcessText(clientService, block, true);

            if (text == null && caption == null)
            {
                return null;
            }

            FrameworkElement content;
            var expandable = false;

            if (caption == null && text is FormattedTextBlock formatted)
            {
                formatted.MaxLines = 3;
                content = formatted;
                expandable = true;
            }
            else
            {
                var panel = new StackPanel();

                if (text != null)
                {
                    panel.Children.Add(text);
                }

                if (caption != null)
                {
                    caption.Style = _context.Resources["PullquoteCreditStyle"] as Style;
                    panel.Children.Add(caption);
                }

                content = panel;
            }

            return new BlockQuote
            {
                Glyph = Icons.QuoteBlockFilled16,
                IsExpandable = expandable,
                Content = content,
                Padding = new Thickness(8, 4, 24, 6)
            };
        }

        private FrameworkElement ProcessDocument(IClientService clientService, PageBlockDocument block)
        {
            var message = CreateMessage(clientService, block.Document.DocumentValue.Id, new MessageDocument(block.Document, string.Empty.AsFormattedText()));
            var content = new DocumentContent(message);
            content.HorizontalAlignment = HorizontalAlignment.Left;
            content.ClearValue(FrameworkElement.MaxWidthProperty);
            content.ClearValue(FrameworkElement.MaxHeightProperty);

            var caption = ProcessCaption(clientService, block.Caption);
            if (caption != null)
            {
                caption.Margin = new Thickness(0, 8, 0, 0);

                var element = new StackPanel();

                element.Children.Add(content);
                element.Children.Add(caption);

                return element;
            }

            return content;
        }

        private FrameworkElement ProcessButtonRow(IClientService clientService, PageBlockButtonRow block)
        {
            var element = new Grid
            {
                ColumnSpacing = 4,
                HorizontalAlignment = block.Align switch
                {
                    PageBlockHorizontalAlignmentLeft => HorizontalAlignment.Left,
                    PageBlockHorizontalAlignmentCenter => HorizontalAlignment.Center,
                    PageBlockHorizontalAlignmentRight => HorizontalAlignment.Right,
                    _ => HorizontalAlignment.Stretch
                }
            };

            var column = 0;

            foreach (var button in block.Buttons)
            {
                var content = CreateInlineButton(clientService, button);
                element.Children.Add(content);
                element.ColumnDefinitions.Add(new ColumnDefinition());

                Grid.SetColumn(content, column++);
            }

            return element;
        }

        private ReplyMarkupInlineButton CreateInlineButton(IClientService clientService, InlineButton button)
        {
            var element = new ReplyMarkupInlineButton
            {
                // The label is the button's own content, so unlike the page text around
                // it, it *is* rendered here — it's just never harvested as page text.
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                CornerRadius = new CornerRadius(16),
                Tag = button
            };

            // Null label: the content is the FormattedTextBlock above, so the button keeps
            // its RichText formatting. Everything else — the type glyph, the emoji icon,
            // the ButtonStyle colour — comes from the same place an inline keyboard gets
            // it, so the two can't drift apart.
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

                Instrumentation.Register(block);

                block.IconForeground = element.Foreground;
                block.SetText(clientService, button.Text);

                element.Content = block;
            }

            return element;
        }

        private void InlineButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: InlineButton button })
            {
                return;
            }

            _context.OpenInlineButton(button);
        }

        private FrameworkElement ProcessPullquote(IClientService clientService, PageBlockPullQuote block)
        {
            var content = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var background = new Grid
            {
                Style = _context.Resources["BlockPullquoteStyle"] as Style,
                Opacity = 0.1,
                CornerRadius = new CornerRadius(8)
            };

            content.ColumnDefinitions.Add(1, GridUnitType.Auto);
            content.ColumnDefinitions.Add(1, GridUnitType.Star);
            content.ColumnDefinitions.Add(1, GridUnitType.Auto);
            content.RowDefinitions.Add(1, GridUnitType.Auto);
            content.RowDefinitions.Add(1, GridUnitType.Auto);

            Grid.SetColumnSpan(background, 3);
            Grid.SetRowSpan(background, 3);

            content.Children.Add(background);

            var quoteTop = new TextBlock
            {
                Text = Icons.QuoteBlockOpenFilled16,
                Style = _context.Resources["AccentTextBlockStyle"] as Style,
                FontFamily = BootStrapper.Current.Resources["SymbolThemeFontFamily"] as FontFamily,
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(4)
            };

            var quoteBottom = new TextBlock
            {
                Text = Icons.QuoteBlockFilled16,
                Style = _context.Resources["AccentTextBlockStyle"] as Style,
                FontFamily = BootStrapper.Current.Resources["SymbolThemeFontFamily"] as FontFamily,
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(4),
            };

            Grid.SetColumn(quoteBottom, 2);

            content.Children.Add(quoteTop);
            content.Children.Add(quoteBottom);

            var text = ProcessText(clientService, block, false);
            if (text != null)
            {
                Grid.SetColumn(text, 1);

                text.Margin = new Thickness(4, 6, 4, 8);
                content.Children.Add(text);
            }

            var caption = ProcessText(clientService, block, true);
            if (caption != null)
            {
                Grid.SetColumnSpan(caption, 3);
                Grid.SetRow(caption, 1);

                caption.Style = _context.Resources["PullquoteCreditStyle"] as Style;
                caption.Margin = new Thickness(8, -4, 8, 8);
                content.Children.Add(caption);
            }

            return content;
        }

        private FrameworkElement ProcessPhoto(IClientService clientService, PageBlockPhoto block, PageBlock parent)
        {
            if (block.Photo == null)
            {
                return null;
            }

            // No gallery item is built here: the gallery is assembled from the blocks when
            // one is tapped (MessageDelegate.OpenPageBlockMedia), so there is no parallel
            // list to keep in sync with what's on screen.
            var message = CreateMessage(clientService, new MessagePhoto(block.Photo, null, null, false, block.HasSpoiler, false));
            var content = new PhotoContent(message, album: parent is PageBlockCollage);
            content.HorizontalAlignment = parent is PageBlockCollage ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
            content.ClearValue(FrameworkElement.MaxWidthProperty);
            content.ClearValue(FrameworkElement.MaxHeightProperty);

            var caption = ProcessCaption(clientService, block.Caption);
            if (caption != null)
            {
                caption.Margin = new Thickness(12, 8, 0, 0);

                var element = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                element.Children.Add(content);
                element.Children.Add(caption);

                return element;
            }

            return content;
        }

        private FrameworkElement ProcessVideo(IClientService clientService, PageBlockVideo block, PageBlock parent)
        {
            if (block.Video == null)
            {
                return null;
            }

            // See ProcessPhoto: the gallery is built on tap, not collected here.
            var message = CreateMessage(clientService, new MessageVideo(block.Video, Array.Empty<AlternativeVideo>(), Array.Empty<VideoStoryboard>(), null, 0, null, false, block.HasSpoiler, false));
            var content = new VideoContent(message, album: parent is PageBlockCollage);
            content.HorizontalAlignment = parent is PageBlockCollage ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
            content.ClearValue(FrameworkElement.MaxWidthProperty);
            content.ClearValue(FrameworkElement.MaxHeightProperty);

            var caption = ProcessCaption(clientService, block.Caption);
            if (caption != null)
            {
                caption.Margin = new Thickness(12, 8, 0, 0);

                var element = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                element.Children.Add(content);
                element.Children.Add(caption);

                return element;
            }

            return content;
        }

        private FrameworkElement ProcessAnimation(IClientService clientService, PageBlockAnimation block)
        {
            if (block.Animation == null)
            {
                return null;
            }

            // See ProcessPhoto: the gallery is built on tap, not collected here.
            var message = CreateMessage(clientService, new MessageAnimation(block.Animation, null, false, block.HasSpoiler, false));
            var content = new AnimationContent(message);
            content.HorizontalAlignment = HorizontalAlignment.Center;
            content.ClearValue(FrameworkElement.MaxWidthProperty);
            content.ClearValue(FrameworkElement.MaxHeightProperty);

            //if (block.Animation.AnimationValue.Local.IsDownloadingCompleted)
            //{
            //    _animations.Add(content.GetPlaybackElement());
            //}

            var caption = ProcessCaption(clientService, block.Caption);
            if (caption != null)
            {
                caption.Margin = new Thickness(12, 8, 0, 0);

                var element = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                element.Children.Add(content);
                element.Children.Add(caption);

                return element;
            }

            return content;
        }

        private FrameworkElement ProcessAudio(IClientService clientService, PageBlockAudio block)
        {
            var message = CreateMessage(clientService, block.Audio.AudioValue.Id, new MessageAudio(block.Audio, string.Empty.AsFormattedText()));
            var content = new AudioContent(message);
            content.HorizontalAlignment = HorizontalAlignment.Left;
            content.ClearValue(FrameworkElement.MaxWidthProperty);
            content.ClearValue(FrameworkElement.MaxHeightProperty);

            var caption = ProcessCaption(clientService, block.Caption);
            if (caption != null)
            {
                caption.Margin = new Thickness(0, 8, 0, 0);

                var element = new StackPanel();

                element.Children.Add(content);
                element.Children.Add(caption);

                return element;
            }

            return content;
        }

        private FrameworkElement ProcessVoiceNote(IClientService clientService, PageBlockVoiceNote block)
        {
            var message = CreateMessage(clientService, block.VoiceNote.Voice.Id, new MessageVoiceNote(block.VoiceNote, string.Empty.AsFormattedText(), true));
            var content = new VoiceNoteContent(message);
            content.HorizontalAlignment = HorizontalAlignment.Left;
            content.ClearValue(FrameworkElement.MaxWidthProperty);
            content.ClearValue(FrameworkElement.MaxHeightProperty);

            var caption = ProcessCaption(clientService, block.Caption);
            if (caption != null)
            {
                caption.Margin = new Thickness(0, 8, 0, 0);

                var element = new StackPanel();

                element.Children.Add(content);
                element.Children.Add(caption);

                return element;
            }

            return content;
        }

        // Media blocks render through the message content controls, so each one needs a
        // message to live in. Only the host can build it: a rich message has a real chat
        // and delegate behind it, an instant view has neither.
        private MessageViewModel CreateMessage(IClientService clientService, MessageContent content)
        {
            return _context.CreateMessage(0, content);
        }

        private MessageViewModel CreateMessage(IClientService clientService, long id, MessageContent content)
        {
            return _context.CreateMessage(id, content);
        }

        private FrameworkElement ProcessEmbed(IClientService clientService, PageBlockEmbedded block)
        {
            var element = new StackPanel { Style = _context.Resources["BlockEmbedStyle"] as Style };

            var view = new WebViewer();

            void loaded(object sender, RoutedEventArgs e)
            {
                view.Loaded -= loaded;

                // TODO: auto-size

                if (!block.AllowScrolling)
                {
                    // TODO: block scrolling
                    //await view.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("document.querySelector('body').style.overflow='hidden'");
                }
                if (!string.IsNullOrEmpty(block.Html))
                {
                    view.NavigateToString(block.Html.Replace("src=\"//", "src=\"https://"));
                }
                else if (!string.IsNullOrEmpty(block.Url))
                {
                    view.Navigate(block.Url);
                }
            }

            void unloaded(object sender, RoutedEventArgs e)
            {
                view.Unloaded -= unloaded;
                view.Close();
            }

            view.Loaded += loaded;
            view.Unloaded += unloaded;

            //if (block.HasPosterPhotoId)
            //{
            //    var photo = page.Photos.FirstOrDefault(x => x.Id == block.PosterPhotoId);
            //    var image = new ImageView();
            //    image.Source = (ImageSource)DefaultPhotoConverter.Convert(photo, "thumbnail");
            //    image.Constraint = photo;
            //    child = image;
            //}
            var ratio = new AspectView();
            ratio.MaxWidth = block.Width;
            ratio.MaxHeight = block.Height;
            ratio.Constraint = new Size(block.Width, block.Height);
            ratio.Children.Add(view);

            element.Children.Add(ratio);

            var caption = ProcessCaption(clientService, block.Caption);
            if (caption != null)
            {
                caption.Margin = new Thickness(12, 8, 0, 0);
                element.Children.Add(caption);
            }

            return element;
        }

        private FrameworkElement ProcessSlideshow(IClientService clientService, PageBlockSlideshow block)
        {
            var items = new List<FrameworkElement>();
            foreach (var item in block.Blocks)
            {
                var child = ProcessBlock(clientService, item, block);
                if (child != null)
                {
                    child.Tag = item;
                    child.HorizontalAlignment = HorizontalAlignment.Center;
                    child.ClearValue(FrameworkElement.MaxWidthProperty);
                    child.ClearValue(FrameworkElement.MaxHeightProperty);

                    items.Add(child);
                }
            }

            var flip = new FlipView();
            flip.ItemsSource = items;
            flip.MaxHeight = 420;

            var pager = new PipsPager
            {
                NumberOfPages = items.Count,
                CornerRadius = new CornerRadius(0),
                RequestedTheme = ElementTheme.Dark,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
            };

            pager.SetBinding(PipsPager.SelectedPageIndexProperty, new Binding
            {
                Path = new PropertyPath("SelectedIndex"),
                Source = flip,
                Mode = BindingMode.TwoWay
            });

            var content = new Grid();
            content.Children.Add(flip);
            content.Children.Add(pager);

            var caption = ProcessCaption(clientService, block.Caption);
            if (caption != null)
            {
                var element = new StackPanel
                {
                    Style = _context.Resources["BlockSlideshowStyle"] as Style
                };

                caption.Margin = new Thickness(12, 8, 0, 0);

                element.Children.Add(content);
                element.Children.Add(caption);

                return element;
            }

            return content;
        }

        public sealed partial class PageBlockCollageContent : Grid
        {
            //public MessageViewModel Message => _message;
            //private MessageViewModel _message;

            private readonly PageBlockCollageAlbum _collage;

            private class PageBlockCollageAlbum : MessageAlbumBase
            {
                private readonly PageBlockCollage _collage;

                public PageBlockCollageAlbum(PageBlockCollage collage)
                {
                    _collage = collage;
                }

                protected override IEnumerable<Size> GetSizes()
                {
                    foreach (var block in _collage.Blocks)
                    {
                        if (block is PageBlockPhoto photoMedia && photoMedia.Photo != null)
                        {
                            yield return GetClosestPhotoSizeWithSize(photoMedia.Photo.Sizes, 1280, false);
                        }
                        else if (block is PageBlockVideo videoMedia && videoMedia.Video != null)
                        {
                            if (videoMedia.Video.Width != 0 && videoMedia.Video.Height != 0)
                            {
                                yield return new Size(videoMedia.Video.Width, videoMedia.Video.Height);
                            }
                            else if (videoMedia.Video.Thumbnail != null)
                            {
                                yield return new Size(videoMedia.Video.Thumbnail.Width, videoMedia.Video.Thumbnail.Height);
                            }
                            //else if (videoMedia.Cover != null)
                            //{
                            //    yield return GetClosestPhotoSizeWithSize(videoMedia.Cover.Sizes, 1280, false);
                            //}
                        }
                        else
                        {
                            // We are returning a random size, it's still better than NaN.
                            yield return new Size(1280, 1280);
                        }
                    }
                }
            }

            public PageBlockCollageContent(PageBlockCollage collage)
            {
                _collage = new PageBlockCollageAlbum(collage);
            }

            private (Rect[], Size) _positions;

            protected override Size MeasureOverride(Size availableSize)
            {
                if (_collage == null /*|| _collage.Count <= 1*/)
                {
                    return base.MeasureOverride(availableSize);
                }

                var positions = _collage.GetPositionsForWidth(availableSize.Width, true);

                for (int i = 0; i < Math.Min(positions.Item1.Length, Children.Count); i++)
                {
                    Children[i].Measure(positions.Item1[i].ToSize());
                }

                _positions = positions;
                return positions.Item2;
            }

            protected override Size ArrangeOverride(Size finalSize)
            {
                if (_collage == null /*|| _collage.Count <= 1*/)
                {
                    return base.ArrangeOverride(finalSize);
                }

                var positions = _positions;
                if (positions.Item1 == null || positions.Item1.Length == 1)
                {
                    return base.ArrangeOverride(finalSize);
                }

                for (int i = 0; i < Math.Min(positions.Item1.Length, Children.Count); i++)
                {
                    Children[i].Arrange(positions.Item1[i]);
                }

                return finalSize;
            }
        }

        private FrameworkElement ProcessCollage(IClientService clientService, PageBlockCollage block)
        {
            var content = new PageBlockCollageContent(block);

            foreach (var item in block.Blocks)
            {
                var child = ProcessBlock(clientService, item, block);
                if (child != null)
                {
                    child.Tag = item;
                    content.Children.Add(child);
                }
            }

            var caption = ProcessCaption(clientService, block.Caption);
            if (caption != null)
            {
                caption.Margin = new Thickness(12, 8, 0, 0);

                var element = new StackPanel();

                element.Children.Add(content);
                element.Children.Add(caption);

                return element;
            }

            return content;
        }

        private FrameworkElement ProcessEmbedPost(IClientService clientService, PageBlockEmbeddedPost block)
        {
            var element = new StackPanel { Style = _context.Resources["BlockEmbedPostStyle"] as Style };

            var header = new Grid();
            header.RowDefinitions.Add(1, GridUnitType.Auto);
            header.RowDefinitions.Add(1, GridUnitType.Auto);
            header.ColumnDefinitions.Add(1, GridUnitType.Auto);
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.Margin = new Thickness(_padding, 0, 0, 0);

            var photo = block.AuthorPhoto;
            if (photo != null)
            {
                var ellipse = new Ellipse();
                ellipse.Width = 36;
                ellipse.Height = 36;
                ellipse.Margin = new Thickness(0, 0, _padding, 0);
                //ellipse.Fill = new ImageBrush { ImageSource = (ImageSource)DefaultPhotoConverter.Convert(photo, true), Stretch = Stretch.UniformToFill, AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center };
                Grid.SetRowSpan(ellipse, 2);

                header.Children.Add(ellipse);
            }

            var textAuthor = new TextBlock();
            textAuthor.Text = block.Author;
            textAuthor.VerticalAlignment = VerticalAlignment.Bottom;
            Grid.SetColumn(textAuthor, 1);
            Grid.SetRow(textAuthor, 0);

            var textDate = new TextBlock();
            textDate.Text = Formatter.ToLocalTime(block.Date).ToString("dd MMMM yyyy");
            textDate.VerticalAlignment = VerticalAlignment.Top;
            textDate.Style = (Style)_context.Resources["CaptionTextBlockStyle"];
            textDate.Foreground = (SolidColorBrush)_context.Resources["SystemControlDisabledChromeDisabledLowBrush"];
            Grid.SetColumn(textDate, 1);
            Grid.SetRow(textDate, 1);

            header.Children.Add(textAuthor);
            header.Children.Add(textDate);

            element.Children.Add(header);

            PageBlock previousBlock = null;
            foreach (var subBlock in block.Blocks)
            {
                var subLayout = ProcessBlock(clientService, subBlock, block);
                var spacing = SpacingBetweenBlocks(previousBlock, block);

                if (subLayout != null)
                {
                    subLayout.Margin = new Thickness(_padding, spacing, _padding, 0);
                    element.Children.Add(subLayout);
                }

                previousBlock = block;
                FrameworkElement previousElement = subLayout;
            }

            return element;
        }

        private FrameworkElement ProcessAnchor(IClientService clientService, PageBlockAnchor block)
        {
            var element = new Border();
            _anchors[block.Name] = element;

            return element;
        }

        // A "full media" block: edge-to-edge media with no caption. These bleed to
        // the content edges, so they get no top margin as the first block and no
        // bottom margin as the last block.
        private static bool IsFullMedia(PageBlock block)
        {
            return block is PageBlockAnimation { Caption: null }
                or PageBlockCollage { Caption: null }
                or PageBlockMap { Caption: null }
                or PageBlockPhoto { Caption: null }
                or PageBlockSlideshow { Caption: null }
                or PageBlockVideo { Caption: null };
        }

        // Recomputes every block's vertical margins from its neighbours. Called after
        // each diff so add/move/remove all keep spacing correct (a single change can
        // affect the previous block, the next block, and the first/last edges).
        //   - consecutive paragraphs: no gap between them
        //   - any other adjacent pair: 8px gap (carried as the lower block's top)
        //   - first block (unless full media): 4px top
        //   - last block (unless full media): 6px bottom
        // LayoutRoot.Children is kept 1:1 with blocks by the diff (null elements are
        // inserted as Border placeholders), so indices line up.
        public void UpdateSpacing(Panel panel, Vector<PageBlock> blocks, bool root)
        {
            var count = Math.Min(blocks.Count, panel.Children.Count);

            PageBlock previousBlock = null;
            for (int i = 0; i < count; i++)
            {
                if (panel.Children[i] is not FrameworkElement element)
                {
                    continue;
                }

                var block = blocks[i];
                var padding = root ? PaddingForBlock(block) : 0;

                double top;
                if (i == 0)
                {
                    top = root && block is PageBlockAudio ? 4 : 0;
                    //top = IsFullMedia(block) ? 0 : 4;
                }
                else if (block is PageBlockAnchor || (block is PageBlockParagraph && previousBlock is PageBlockParagraph))
                {
                    top = 0;
                }
                else if (block is PageBlockButtonRow && previousBlock is PageBlockButtonRow)
                {
                    top = 4;
                }
                else if (block is PageBlockDivider)
                {
                    top = 12;
                }
                else
                {
                    top = 8;
                }

                var bottom = block is PageBlockDivider ? 4 : root && i == count - 1 && !IsFullMedia(block) ? 6 : 0;

                //var margin = new Thickness(padding, top, padding, bottom);
                //if (element.Margin != margin)
                //{
                //    element.Margin = margin;
                //}

                element.Margin = new Thickness(padding, top, padding, bottom);
                element.Tag = block;

                previousBlock = block is PageBlockAnchor ? previousBlock : block;
            }
        }

        private double SpacingBetweenBlocks(PageBlock upper, PageBlock lower)
        {
            if (lower is PageBlockCover or PageBlockChatLink)
            {
                return 0;
            }

            if (upper is PageBlockDetails && lower is PageBlockDetails)
            {
                return 0;
            }

            return 12;

            if (lower is PageBlockCover or PageBlockChatLink)
            {
                return 0;
            }
            else if (lower is PageBlockDivider || upper is PageBlockDivider)
            {
                return 15; // 25;
            }
            else if (lower is PageBlockBlockQuote || upper is PageBlockBlockQuote || lower is PageBlockPullQuote || upper is PageBlockPullQuote)
            {
                return 17; // 27;
            }
            else if (lower is PageBlockTitle)
            {
                return 12; // 20;
            }
            else if (lower is PageBlockAuthorDate)
            {
                if (upper is PageBlockTitle)
                {
                    return 16; // 26;
                }
                else
                {
                    return 12; // 20;
                }
            }
            else if (lower is PageBlockParagraph)
            {
                if (upper is PageBlockTitle or PageBlockAuthorDate)
                {
                    return 20; // 34;
                }
                else if (upper is PageBlockHeader or PageBlockSubheader)
                {
                    return 15; // 25;
                }
                else if (upper is PageBlockParagraph)
                {
                    return 15; // 25;
                }
                else if (upper is PageBlockList)
                {
                    return 19; // 31;
                }
                else if (upper is PageBlockPreformatted)
                {
                    return 11; // 19;
                }
                else
                {
                    return 12; // 20;
                }
            }
            else if (lower is PageBlockList)
            {
                if (upper is PageBlockTitle or PageBlockAuthorDate)
                {
                    return 20; // 34;
                }
                else if (upper is PageBlockHeader or PageBlockSubheader)
                {
                    return 19; // 31;
                }
                else if (upper is PageBlockParagraph or PageBlockList)
                {
                    return 19; // 31;
                }
                else if (upper is PageBlockPreformatted)
                {
                    return 11; // 19;
                }
                else
                {
                    return 12; // 20;
                }
            }
            else if (lower is PageBlockPreformatted)
            {
                if (upper is PageBlockParagraph)
                {
                    return 11; // 19;
                }
                else
                {
                    return 12; // 20;
                }
            }
            else if (lower is PageBlockHeader)
            {
                return 20; // 32;
            }
            else if (lower is PageBlockSubheader)
            {
                return 20; // 32;
            }
            else if (lower == null)
            {
                if (upper is PageBlockFooter)
                {
                    return 14; // 24;
                }
                else
                {
                    return 14; // 24;
                }
            }

            return 12; // 20;
        }

        private double PaddingForBlock(PageBlock block)
        {
            if (block is PageBlockCover or PageBlockMap or
                PageBlockPhoto or PageBlockVideo or
                PageBlockCollage or PageBlockSlideshow or PageBlockChatLink)
            {
                return 0.0;
            }

            return 10;
            return _padding;
        }
    }
}
