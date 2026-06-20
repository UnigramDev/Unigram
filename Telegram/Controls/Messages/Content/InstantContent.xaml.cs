//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.UI.Xaml.Controls;
using Rg.DiffUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Native.Highlight;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls.Messages.Content
{
    public sealed partial class InstantContent : Control, IContent
    {
        private CancellationTokenSource _instantViewToken;

        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        public InstantContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(InstantContent);
        }

        public InstantContent()
        {
            DefaultStyleKey = typeof(InstantContent);
        }

        #region InitializeComponent

        private StackPanel LayoutRoot;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            LayoutRoot = GetTemplateChild(nameof(LayoutRoot)) as StackPanel;

            _templateApplied = true;

            if (_message != null)
            {
                UpdateMessage(_message);
            }
        }

        #endregion

        public FormattedTextBlock LastBlock
        {
            get
            {
                return FindBlock(LayoutRoot);

                static FormattedTextBlock FindBlock(UIElement element)
                {
                    if (element is Panel panel && panel.Children.Count > 0)
                    {
                        // TODO: a better logic is needed (i.e. only use for some specific panel type)
                        return null;
                        return FindBlock(panel.Children[^1]);
                    }
                    else if (element is FormattedTextBlock block)
                    {
                        return block;
                    }

                    return null;
                }
            }
        }

        public void UpdateMessage(MessageViewModel message)
        {
            _instantViewToken?.Cancel();
            _instantViewToken = new CancellationTokenSource();

            _message = message;

            var text = GetContent(message);
            if (text == null || !_templateApplied)
            {
                return;
            }

            UpdateInstantView(message, text, _instantViewToken.Token);
        }

        public void Recycle()
        {
            _instantViewToken?.Cancel();
            _message = null;

            //if (_templateApplied && Media.Child is IContent content)
            //{
            //    content.Recycle();
            //}
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            return content is MessageRichMessage;
        }

        private RichMessage GetContent(MessageViewModel message)
        {
            var content = message?.GeneratedContent ?? message?.Content;
            if (content is MessageRichMessage text)
            {
                return text.Message;
            }

            return null;
        }


        private async void UpdateInstantView(MessageViewModel message, RichMessage linkPreview, CancellationToken token)
        {
            //var response = await _message.ClientService.SendAsync(new GetFullRichMessage(message.ChatId, message.Id));
            //if (response is RichMessage richMessage && /*instantView.IsFull &&*/ !token.IsCancellationRequested)
            {
                UpdateView(message.ClientService, linkPreview.Blocks, !linkPreview.IsFull);
            }


            if (!linkPreview.IsFull)
            {
                var load = new ButtonEx();
                load.Style = BootStrapper.Current.Resources["InstantViewButtonStyle"] as Style;
                load.Content = "Show more";
                load.Margin = new Thickness(10, 8, 10, 4);
                load.Click += async (s, args) =>
                {
                    load.ShowSkeleton();

                    var response = await _message.ClientService.SendAsync(new GetFullRichMessage(message.ChatId, message.Id));
                    if (response is RichMessage richMessage && /*instantView.IsFull &&*/ !token.IsCancellationRequested)
                    {
                        _message.Delegate.NavigationService.NavigateToInstant(new WebPageInstantView(richMessage.Blocks, 0, 2, richMessage.IsRtl, richMessage.IsFull, null), "tg://test");
                    }

                    load.HideSkeleton();
                };

                LayoutRoot.Children.Add(load);
            }
        }

        private IList<PageBlock> _prevValue;

        public void UpdateView(IClientService clientService, IList<PageBlock> blocks, bool part)
        {
            var prev = _prevValue ?? Array.Empty<PageBlock>();
            var diff = DiffUtil.CalculateDiff(prev, blocks, PageBlockHelper.Compare, Constants.DiffOptions);

            Logger.Info(string.Format("Steps: {0}, added: {1}, removed: {2}, moved: {3}", diff.Steps.Count, diff.AddedItems.Count, diff.RemovedItems.Count, diff.MovedItems.Count));

            foreach (var step in diff.Steps)
            {
                if (step.Status == DiffStatus.Add)
                {
                    var element = ProcessBlock(clientService, step.Items[0].NewValue, null);
                    if (element != null)
                    {
                        var padding = PaddingForBlock(step.Items[0].NewValue);
                        var spacing = SpacingBetweenBlocks(step.Items[0].NewValue, blocks, step.NewStartIndex);
                        element.Margin = new Thickness(padding, spacing, padding, 0);

                        LayoutRoot.Children.Insert(step.NewStartIndex, element);
                    }
                    else
                    {
                        LayoutRoot.Children.Insert(step.NewStartIndex, new Border());
                    }

                    //UpdateItem(step.Items[0].NewValue, null, step.NewStartIndex);
                }
                else if (step.Status == DiffStatus.Move && step.OldStartIndex < LayoutRoot.Children.Count && step.NewStartIndex < LayoutRoot.Children.Count)
                {
                    //UpdateItem(step.Items[0].OldValue, step.Items[0].NewValue);
                    LayoutRoot.Children.Move((uint)step.OldStartIndex, (uint)step.NewStartIndex);
                }
                else if (step.Status == DiffStatus.Remove && step.OldStartIndex < LayoutRoot.Children.Count)
                {
                    //if (step.Items[0].OldValue is MessageReaction oldReaction)
                    //{
                    //    _cache.Remove(oldReaction.Type);
                    //}

                    LayoutRoot.Children.RemoveAt(step.OldStartIndex);

                    if (step.Items[0].OldValue is PageBlockAnchor anchor)
                    {
                        _anchors.Remove(anchor.Name);
                    }
                }
            }

            //foreach (var item in diff.NotMovedItems)
            //{
            //    UpdateItem(item.OldValue, item.NewValue);
            //}

            _prevValue = blocks;
            return;

            //_instantView = instantView;

            //ScrollingHost.FlowDirection = instantView.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

            //if (instantView.ViewCount > 0)
            //{
            //    ViewsLabel.Text = Locale.Declension(Strings.R.Views, instantView.ViewCount);
            //}
            //else
            //{
            //    ViewsLabel.Text = string.Empty;
            //}

            _anchors.Clear();
            LayoutRoot.Children.Clear();

            var processed = 0;
            PageBlock previousBlock = null;
            FrameworkElement previousElement = null;
            FrameworkElement firstElement = null;
            foreach (var block in blocks)
            {
                var element = ProcessBlock(clientService, block, null);
                var spacing = SpacingBetweenBlocks(previousBlock, block);
                var padding = PaddingForBlock(block);

                if (element != null)
                {
                    if (block is PageBlockChatLink && previousBlock is PageBlockCover)
                    {
                        if (previousElement is StackPanel stack && element is Button)
                        {
                            element.Style = LayoutRoot.Resources["CoverChannelBlockStyle"] as Style;
                            element.Margin = new Thickness(padding, -40, padding, 0);
                            stack.Children.Insert(1, element);
                        }
                    }
                    else
                    {
                        element.Margin = new Thickness(padding, spacing, padding, 0);
                        LayoutRoot.Children.Add(element);
                    }
                }

                firstElement ??= element;

                previousBlock = block;
                previousElement = element;
                processed++;
            }

            //if (firstElement != null)
            //{
            //    firstElement.Loaded += (s, args) =>
            //    {
            //        if (ViewModel.ShareLink?.Fragment?.Length > 0)
            //        {
            //            Hyperlink_Click(new RichTextAnchorLink { AnchorName = ViewModel.ShareLink.Fragment.TrimStart('#') });
            //        }
            //    };
            //}

            //if (previousElement != null)
            //{
            //    previousElement.Margin = new Thickness(previousElement.Margin.Left, previousElement.Margin.Top, previousElement.Margin.Right, previousElement.Margin.Bottom + 24);
            //}
        }

        private readonly long _webpageId;

        //private Stack<Panel> _containers = new Stack<Panel>();
        private readonly double _padding = 12;

        private readonly Dictionary<string, Border> _anchors = new();

        private FrameworkElement ProcessBlock(IClientService clientService, PageBlock block, PageBlock parent)
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
                PageBlockAnchor anchor => ProcessAnchor(clientService, anchor),
                PageBlockPreformatted preformatted => ProcessPreformatted(clientService, preformatted),
                PageBlockChatLink channel => ProcessChannel(clientService, channel),
                PageBlockDetails details => ProcessDetails(clientService, details),
                PageBlockTable table => ProcessTable(clientService, table),
                PageBlockMap map => ProcessMap(clientService, map),
                PageBlockAudio audio => ProcessAudio(clientService, audio),
                PageBlockVoiceNote voiceNote => ProcessVoiceNote(clientService, voiceNote),
                PageBlockMathematicalExpression math => ProcessMath(clientService, math),
                _ => ProcessUnsupported(clientService, block),
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
            //content.Tag = galleryItem;
            content.HorizontalAlignment = HorizontalAlignment.Center;
            content.ClearValue(MaxWidthProperty);
            content.ClearValue(MaxHeightProperty);

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
                var border = new Border { Style = LayoutRoot.Resources["BlockRelatedArticlesHeaderPanelStyle"] as Style };
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
                var description = new TextBlock { TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 2, Style = LayoutRoot.Resources["BlockAuthorDateTextBlockStyle"] as Style };

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
                button.Click += (s, args) => Hyperlink_Click(new RichTextUrl(null, article.Url, true));

                panel.Children.Add(button);
            }

            return panel;
        }

        private FrameworkElement ProcessTable(IClientService clientService, PageBlockTable table, bool test = false)
        {
            var grid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var thickness = table.IsBordered ? 1 : 0;

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

                    FormattedTextBlock textBlock = null;
                        textBlock = CreateTextBlock();
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
                        Padding = new Thickness(8, 4, 8, 4),
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
                => LayoutRoot.Resources.TryGetValue(key, out var value) ? value as Style : null;
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
            //    Style = Resources["ChannelBlockStyle"] as Style,
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
            var textBlock = new TextBlock { Style = LayoutRoot.Resources["BlockAuthorDateTextBlockStyle"] as Style };

            if (!block.Author.IsNullOrEmpty())
            {
                var span = new Span();
                textBlock.Inlines.Add(string.Format(Strings.ArticleByAuthor, string.Empty));
                textBlock.Inlines.Add(span);
                ProcessRichText(clientService, block.Author, span, null);
            }

            if (block.PublishDate > 0)
            {
                if (textBlock.Inlines.Count > 0)
                {
                    textBlock.Inlines.Add(" — ");
                }

                textBlock.Inlines.Add(Formatter.Date(block.PublishDate, Strings.chatFullDate));
            }

            return textBlock;
        }

        private FrameworkElement ProcessText(IClientService clientService, PageBlock block, bool caption)
        {
            RichText text = null;
            switch (block)
            {
                case PageBlockTitle title:
                    text = title.Title;
                    break;
                case PageBlockSubtitle subtitle:
                    text = subtitle.Subtitle;
                    break;
                case PageBlockHeader header:
                    text = header.Header;
                    break;
                case PageBlockSubheader subheader:
                    text = subheader.Subheader;
                    break;
                case PageBlockFooter footer:
                    text = footer.Footer;
                    break;
                case PageBlockParagraph paragraphz:
                    text = paragraphz.Text;
                    break;
                case PageBlockPreformatted preformatted:
                    text = preformatted.Text;
                    break;
                case PageBlockBlockQuote blockquote:
                    text = blockquote.Credit;
                    break;
                case PageBlockPullQuote pullquote:
                    text = caption ? pullquote.Credit : pullquote.Text;
                    break;
                case PageBlockDetails details:
                    text = details.Header;
                    break;
                case PageBlockTable table:
                    text = table.Caption;
                    break;
                case PageBlockRelatedArticles relatedArticles:
                    text = relatedArticles.Header;
                    break;
                case PageBlockKicker kicker:
                    text = kicker.Kicker;
                    break;
                case PageBlockSectionHeading heading:
                    text = heading.Text;
                    break;
                case PageBlockThinking thinking:
                    text = thinking.Text;
                    break;
            }

            if (PageBlockHelper.IsEmpty(text))
            {
                return null;
            }

            var textBlock = CreateTextBlock();
            textBlock.AutoFontSize = false;

            textBlock.SetText(clientService, text);

            //var textBlock = CreateTextBlock();
            //var span = new Span();
            //var paragraph = new Paragraph();
            //paragraph.Inlines.Add(span);
            //textBlock.Blocks.Add(paragraph);
            //textBlock.TextWrapping = TextWrapping.Wrap;

            //textBlock.ContextMenuOpening += Text_ContextMenuOpening;
            //textBlock.AddHandler(ContextRequestedEvent, new TypedEventHandler<UIElement, ContextRequestedEventArgs>(Text_ContextRequested), true);

            ////textBlock.Margin = new Thickness(12, 0, 12, 12);
            //ProcessRichText(clientService, text, span, textBlock);

            switch (block)
            {
                case PageBlockTitle title:
                    textBlock.FontSize = 28;
                    textBlock.FontFamily = BootStrapper.Current.Resources["EmojiThemeFontFamilyWithSerif"] as FontFamily;
                    //textBlock.TextLineBounds = TextLineBounds.TrimToBaseline;
                    break;
                case PageBlockSubtitle subtitle:
                    textBlock.FontSize = 17;
                    //textBlock.FontFamily = new FontFamily("Times New Roman");
                    //textBlock.TextLineBounds = TextLineBounds.TrimToBaseline;
                    break;
                case PageBlockHeader header:
                    textBlock.FontSize = 24;
                    textBlock.FontFamily = BootStrapper.Current.Resources["EmojiThemeFontFamilyWithSerif"] as FontFamily;
                    //textBlock.Style = LayoutRoot.Resources["BlockHeaderTextBlockStyle"] as Style;
                    break;
                case PageBlockSubheader subheader:
                    textBlock.FontSize = 20;
                    textBlock.FontFamily = BootStrapper.Current.Resources["EmojiThemeFontFamilyWithSerif"] as FontFamily;
                    //textBlock.Style = LayoutRoot.Resources["BlockSubheaderTextBlockStyle"] as Style;
                    break;
                case PageBlockParagraph paragraphz:
                    //textBlock.Style = LayoutRoot.Resources["BlockBodyTextBlockStyle"] as Style;
                    break;
                case PageBlockPreformatted preformatted:
                    //textBlock.FontSize = 16;
                    break;
                case PageBlockFooter footer:
                    textBlock.Style = BootStrapper.Current.Resources["InfoCaptionFormattedTextBlockStyle"] as Style;
                    //textBlock.TextAlignment = TextAlignment.Center;
                    break;
                case PageBlockPhoto photo:
                case PageBlockVideo video:
                    textBlock.Style = BootStrapper.Current.Resources["InfoCaptionFormattedTextBlockStyle"] as Style;
                    textBlock.TextAlignment = TextAlignment.Center;
                    break;
                case PageBlockSlideshow slideshow:
                case PageBlockEmbedded embed:
                case PageBlockEmbeddedPost embedPost:
                    textBlock.Style = BootStrapper.Current.Resources["InfoCaptionFormattedTextBlockStyle"] as Style;
                    //textBlock.TextAlignment = TextAlignment.Center;
                    break;
                case PageBlockBlockQuote blockquote:
                    if (caption)
                    {
                        textBlock.Style = BootStrapper.Current.Resources["InfoCaptionFormattedTextBlockStyle"] as Style;
                        textBlock.Margin = new Thickness(0, 12, 0, 0);
                    }
                    else
                    {
                        //textBlock.Style = LayoutRoot.Resources["BlockBodyTextBlockStyle"] as Style;
                    }
                    break;
                case PageBlockPullQuote pullquote:
                    if (caption)
                    {
                        textBlock.Style = BootStrapper.Current.Resources["InfoCaptionFormattedTextBlockStyle"] as Style;
                    }
                    else
                    {
                        //textBlock.Style = LayoutRoot.Resources["BlockBodyTextBlockStyle"] as Style;
                        textBlock.FontFamily = BootStrapper.Current.Resources["EmojiThemeFontFamilyWithSerif"] as FontFamily;
                        //textBlock.TextLineBounds = TextLineBounds.TrimToBaseline;
                        textBlock.TextAlignment = TextAlignment.Center;
                    }
                    break;
                case PageBlockDetails details:
                    textBlock.IsTextSelectionEnabled = false;
                    break;
                case PageBlockRelatedArticles relatedArticles:
                    //textBlock.Style = LayoutRoot.Resources["BlockRelatedArticlesHeaderStyle"] as Style;
                    break;
                case PageBlockSectionHeading heading:
                    textBlock.FontSize = 24 - ((heading.Size - 1) * 2);
                    textBlock.FontFamily = BootStrapper.Current.Resources["EmojiThemeFontFamilyWithSerif"] as FontFamily;
                    textBlock.FontWeight = FontWeights.SemiBold;
                    break;
            }

            return textBlock;
        }

        #region Text selection

        public partial class SelectionRange
        {
            public int Start { get; set; }
            public int End { get; set; }

            public SelectionRange(int start, int end)
            {
                Start = start;
                End = end;
            }
        }

        private RichTextBlock _selectionAnchor;
        private Point _selectionAnchorPoint;
        private Point _stackPoint;

        private int _selectionDirection;
        private TextPointer _selectionPivot;

        private SelectionRange _selectionClue;
        private bool _selectionDirty;

        private bool _selecting;

        private HashSet<RichTextBlock> _selection = new();

        private FormattedTextBlock CreateTextBlock()
        {
            var block = new FormattedTextBlock();
            block.TextEntityClick += Block_TextEntityClick;
            //block.SelectionChanged += OnSelectionChanged;
            //block.LostFocus += OnLostFocus;
            //block.AddHandler(PointerPressedEvent, new PointerEventHandler(OnPointerPressed), true);
            //block.AddHandler(PointerMovedEvent, new PointerEventHandler(OnPointerMoved), true);
            //block.AddHandler(PointerReleasedEvent, new PointerEventHandler(OnPointerReleased), true);

            return block;
        }

        private void Block_TextEntityClick(object sender, TextEntityClickEventArgs e)
        {
            if (e.Type is TextEntityTypeTextUrl textUrl && textUrl.Url.StartsWith("#"))
            {
                if (_anchors.TryGetValue(textUrl.Url.TrimStart('#'), out Border anchor))
                {
                    anchor.StartBringIntoView(new BringIntoViewOptions { VerticalAlignmentRatio = 0.0 });
                    return;

                    var scrollViewer = this.GetParent<ScrollViewer>();
                    if (scrollViewer != null)
                    {
                        var verticalOffset = anchor.TransformToPoint(scrollViewer.ContentTemplateRoot);
                        scrollViewer.ChangeView(null, verticalOffset.Y, null);
                    }
                }
            }
            else
            {
                MessageBubble.TextEntityClick(_message, sender as FormattedTextBlock, e);
            }
        }

        private void RemoveSelectionHighlighter(RichTextBlock block)
        {
            for (int i = block.TextHighlighters.Count - 1; i >= 0; i--)
            {
                if (block.TextHighlighters[i].Background == block.SelectionHighlightColor)
                {
                    block.TextHighlighters.RemoveAt(i);
                    return;
                }
            }
        }

        private void OnLostFocus(object sender, RoutedEventArgs e)
        {
            foreach (var block in _selection)
            {
                RemoveSelectionHighlighter(block);
            }

            if (sender is RichTextBlock anchor)
            {
                anchor.Select(anchor.ContentStart, anchor.ContentStart);
            }
        }

        private void OnSelectionChanged(object sender, RoutedEventArgs e)
        {
            if (_selectionAnchor == sender && _selectionPivot == null)
            {
                if (_selectionClue != null)
                {
                    if (_selectionAnchor.SelectionStart.Offset == _selectionClue.Start || _selectionAnchor.SelectionStart.Offset == _selectionClue.End)
                    {
                        _selectionPivot = _selectionAnchor.SelectionStart;
                        return;
                    }
                    else if (_selectionAnchor.SelectionEnd.Offset == _selectionClue.Start || _selectionAnchor.SelectionEnd.Offset == _selectionClue.End)
                    {
                        _selectionPivot = _selectionAnchor.SelectionEnd;
                        return;
                    }
                }

                _selectionClue = new SelectionRange(_selectionAnchor.SelectionStart.Offset, _selectionAnchor.SelectionEnd.Offset);
            }
        }

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _selectionAnchor = sender as RichTextBlock;
            RemoveSelectionHighlighter(_selectionAnchor);

            var transform = _selectionAnchor.TransformToVisual(XamlRoot.Content);
            var anchorPoint = transform.TransformPoint(new Point());

            _selectionAnchorPoint = new Point(anchorPoint.X, anchorPoint.Y + (_selectionAnchor.ActualHeight / 2));

            var transform2 = LayoutRoot.TransformToVisual(XamlRoot.Content);
            var anchorPoint2 = transform2.TransformPoint(new Point());

            _stackPoint = anchorPoint;
        }

        private void CreateHighlighter(RichTextBlock block, TextPointer start, TextPointer end)
        {
            CreateHighlighter(block, start.OffsetToIndex(), end.OffsetToIndex());
        }

        private void CreateHighlighter(RichTextBlock block, int start, int length)
        {
            var highlighter = new TextHighlighter
            {
                Background = block.SelectionHighlightColor,
                Foreground = new SolidColorBrush(Colors.White)
            };

            highlighter.Ranges.Add(new TextRange
            {
                StartIndex = start,
                Length = length
            });

            RemoveSelectionHighlighter(block);
            block.TextHighlighters.Add(highlighter);
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_selectionAnchor == null)
            {
                return;
            }

            var point = e.GetCurrentPoint(XamlRoot.Content);
            var y1 = Math.Min(_selectionAnchorPoint.Y, point.Position.Y);
            var y2 = Math.Max(_selectionAnchorPoint.Y, point.Position.Y);

            var area = new Rect(_stackPoint.X, y1, LayoutRoot.ActualWidth, y2 - y1);
            var elements = VisualTreeHelper.FindElementsInHostCoordinates(area, LayoutRoot);

            var direction = Math.Sign(_selectionAnchorPoint.Y - point.Position.Y);

            //Debug.WriteLine(direction < 0 ? "Selecting from top to bottom" : "Selecting from bottom to top");
            //Debug.WriteLine(direction < 0 ? "Using selection start as anchor" : "Using selection end as anchor");

            var selection = new HashSet<RichTextBlock>();

            foreach (var block in elements.OfType<RichTextBlock>())
            {
                if (_selectionAnchor == block)
                {
                    continue;
                }

                var relative = e.GetCurrentPoint(block);
                if (relative.Position.Y >= 0 && relative.Position.Y <= Math.Ceiling(block.ActualHeight))
                {
                    // Active block
                    var position = block.GetPositionFromPoint(relative.Position);

                    if (direction < 0)
                    {
                        CreateHighlighter(block, block.ContentStart, position);
                    }
                    else
                    {
                        CreateHighlighter(block, position, block.ContentEnd);
                    }
                }
                else
                {
                    // Full block
                    CreateHighlighter(block, 0, int.MaxValue);
                }

                selection.Add(block);
            }

            selection.Add(_selectionAnchor);

            //Debug.WriteLine(selection.Count);

            if (_selectionPivot != null)
            {
                var relative = e.GetCurrentPoint(_selectionAnchor);
                //Debug.WriteLine("Anchor {0}: ({1} ~> {2})", _selectionAnchor.Tag, relative.Position, _selectionAnchor.ActualHeight);

                if (relative.Position.Y < 0)
                {
                    _selectionDirty = true;
                    _selectionAnchor.Select(_selectionAnchor.ContentStart, _selectionPivot);
                }
                else if (relative.Position.Y > _selectionAnchor.ActualHeight)
                {
                    _selectionDirty = true;
                    _selectionAnchor.Select(_selectionPivot, _selectionAnchor.ContentEnd);
                }
                else if (_selectionDirty)
                {
                    _selectionDirty = false;
                    _selectionAnchor.Select(_selectionPivot, _selectionPivot);
                }
            }

            foreach (var block in _selection)
            {
                if (selection.Contains(block))
                {
                    continue;
                }

                RemoveSelectionHighlighter(block);
            }

            _selection = selection;
            _selectionDirection = direction;
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (sender is RichTextBlock block)
            {
                Debug.WriteLine("Released, {0}", block.Tag);
            }

            _selectionAnchor = null;
            _selectionPivot = null;
            _selectionClue = null;
            _selectionDirty = false;
            _selectionDirection = 0;
            _selecting = false;
        }

        #endregion

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

        private FrameworkElement ProcessUnsupported(IClientService clientService, PageBlock block)
        {
            return new TextBlock { Text = block.ToString() };
        }

        private FrameworkElement ProcessPreformatted(IClientService clientService, PageBlockPreformatted block)
        {
            var element = new StackPanel(); // { Style = Resources["BlockPreformattedStyle"] as Style };

            if (block.Text is not RichTextPlain plain || string.IsNullOrEmpty(block.Language))
            {
                var text = ProcessText(clientService, block, false);
                if (text != null)
                {
                    element.Children.Add(text);
                }

                var test = new Grid();
                test.Children.Add(new BlockQuote
                {
                    Glyph = Icons.CodeFilled16
                });
                test.Children.Add(element);
                //test.Margin = new Thickness(0, 4, 0, 4);

                element.Padding = new Thickness(12, 2, 0, 4);
                return test;
            }
            else
            {
                var paragraph = new Paragraph();
                paragraph.Inlines.Add(plain.Text);

                var text = new RichTextBlock();
                text.Blocks.Add(paragraph);

                ProcessCodeBlock(paragraph.Inlines, plain.Text, block.Language, 0);

                element.Children.Add(text);

                var test = new Grid();
                test.Children.Add(new BlockCode
                {
                    //Glyph = Icons.QuoteBlockFilled16
                    LanguageName = block.Language
                });
                test.Children.Add(element);
                //test.Margin = new Thickness(0, 4, 0, 4);

                element.Padding = new Thickness(12, 22, 0, 4);
                return test;
            }
        }

        private async void ProcessCodeBlock(InlineCollection inlines, string text, string language, int execution)
        {
            try
            {
                var tokens = await SyntaxToken.TokenizeAsync(language.ToLowerInvariant(), text);

                inlines.Clear();
                ProcessCodeBlock(inlines, tokens.Children);
            }
            catch
            {
                // Tokenization may fail
            }
        }

        private void ProcessCodeBlock(InlineCollection inlines, IList<Token> tokens)
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

                    var span = new Span();

                    span.FontFamily = fontFamily;

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
                    inlines.Add(text.Value/*, fontFamily*/);
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

        private FrameworkElement ProcessDivider(IClientService clientService, PageBlockDivider block)
        {
            var element = new Rectangle
            {
                Style = LayoutRoot.Resources["BlockDividerStyle"] as Style
            };

            return element;
        }

        private FrameworkElement ProcessList(IClientService clientService, PageBlockList block)
        {
            var panel = new Grid();
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto), MinWidth = 24 });
            panel.ColumnDefinitions.Add(new ColumnDefinition());

            var row = 0;

            foreach (var item in block.Items)
            {
                FrameworkElement label;
                if (item.HasCheckbox)
                {
                    label = new CheckBox
                    {
                        IsChecked = item.IsChecked,
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
            var content = new StackPanel(); //{ Style = Resources["BlockBlockquoteStyle"] as Style };

            foreach (var item in block.Blocks)
            {
                var child = ProcessBlock(clientService, item, block);
                if (child != null)
                {
                    content.Children.Add(child);
                }
            }

            var caption = ProcessText(clientService, block, true);
            if (caption != null)
            {
                content.Children.Add(caption);
            }

            var test = new Grid();
            test.Children.Add(new BlockQuote
            {
                Glyph = Icons.QuoteBlockFilled16
            });
            test.Children.Add(content);

            content.Padding = new Thickness(12, 2, 12, 4);
            test.Margin = new Thickness(0, 4, 0, 4);
            return test;
        }

        private FrameworkElement ProcessPullquote(IClientService clientService, PageBlockPullQuote block)
        {
            var content = new Grid
            {
                Style = LayoutRoot.Resources["BlockPullquoteStyle"] as Style,
                CornerRadius = new CornerRadius(4)
            };

            content.ColumnDefinitions.Add(1, GridUnitType.Auto);
            content.ColumnDefinitions.Add(1, GridUnitType.Star);
            content.ColumnDefinitions.Add(1, GridUnitType.Auto);
            content.RowDefinitions.Add(1, GridUnitType.Auto);
            content.RowDefinitions.Add(1, GridUnitType.Auto);

            var text = ProcessText(clientService, block, false);
            if (text != null)
            {
                Grid.SetColumn(text, 1);

                content.Children.Add(text);
            }

            var caption = ProcessText(clientService, block, true);
            if (caption != null)
            {
                Grid.SetColumnSpan(caption, 3);
                Grid.SetRow(caption, 1);

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

            //var galleryItem = new GalleryPhoto(ViewModel.ClientService, block.Photo, block.Caption.ToFormattedText());
            //ViewModel.Gallery.Items.Add(galleryItem);

            var message = CreateMessage(clientService, new MessagePhoto(block.Photo, null, null, false, block.HasSpoiler, false));
            var content = new PhotoContent(message, album: parent is PageBlockCollage);
            //content.Tag = galleryItem;
            content.HorizontalAlignment = parent is PageBlockCollage ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
            content.ClearValue(MaxWidthProperty);
            content.ClearValue(MaxHeightProperty);

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

            //var galleryItem = new GalleryVideo(ViewModel.ClientService, block.Video, block.Caption.ToFormattedText());
            //ViewModel.Gallery.Items.Add(galleryItem);

            var message = CreateMessage(clientService, new MessageVideo(block.Video, Array.Empty<AlternativeVideo>(), Array.Empty<VideoStoryboard>(), null, 0, null, false, block.HasSpoiler, false));
            var content = new VideoContent(message, album: parent is PageBlockCollage);
            //content.Tag = galleryItem;
            content.HorizontalAlignment = parent is PageBlockCollage ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
            content.ClearValue(MaxWidthProperty);
            content.ClearValue(MaxHeightProperty);

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

            //var galleryItem = new GalleryAnimation(ViewModel.ClientService, block.Animation, block.Caption.ToFormattedText());
            //ViewModel.Gallery.Items.Add(galleryItem);

            var message = CreateMessage(clientService, new MessageAnimation(block.Animation, null, false, block.HasSpoiler, false));
            var content = new AnimationContent(message);
            //content.Tag = galleryItem;
            content.HorizontalAlignment = HorizontalAlignment.Center;
            content.ClearValue(MaxWidthProperty);
            content.ClearValue(MaxHeightProperty);

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
            if (block.Audio == null)
            {
                return null;
            }

            var message = CreateMessage(clientService, block.Audio.AudioValue.Id, new MessageAudio(block.Audio, string.Empty.AsFormattedText()));
            var content = new AudioContent(message);
            content.HorizontalAlignment = HorizontalAlignment.Left;
            content.ClearValue(MaxWidthProperty);
            content.ClearValue(MaxHeightProperty);

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
            if (block.VoiceNote == null)
            {
                return null;
            }

            var message = CreateMessage(clientService, block.VoiceNote.Voice.Id, new MessageAudio(new Audio(block.VoiceNote.Duration, string.Empty, string.Empty, string.Empty, string.Empty, null, null, null, block.VoiceNote.Voice), string.Empty.AsFormattedText()));
            var content = new AudioContent(message);
            content.HorizontalAlignment = HorizontalAlignment.Left;
            content.ClearValue(MaxWidthProperty);
            content.ClearValue(MaxHeightProperty);

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

        private MessageViewModel CreateMessage(IClientService clientService, MessageContent content)
        {
            return new MessageViewModel(clientService, _message?.Delegate, _message?.Chat, null, null, new Message { Content = content });
        }

        private MessageViewModel CreateMessage(IClientService clientService, long id, MessageContent content)
        {
            return new MessageViewModel(clientService, _message?.Delegate, _message?.Chat, null, null, new Message { Id = id, Content = content });
        }

        private FrameworkElement ProcessEmbed(IClientService clientService, PageBlockEmbedded block)
        {
            var element = new StackPanel { Style = LayoutRoot.Resources["BlockEmbedStyle"] as Style };

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
                    child.HorizontalAlignment = HorizontalAlignment.Center;
                    child.ClearValue(MaxWidthProperty);
                    child.ClearValue(MaxHeightProperty);

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

            var binding = new Binding
            {
                Path = new PropertyPath("SelectedIndex"),
                Source = flip,
                Mode = BindingMode.TwoWay
            };

            BindingOperations.SetBinding(pager, PipsPager.SelectedPageIndexProperty, binding);

            var content = new Grid();
            content.Children.Add(flip);
            content.Children.Add(pager);

            var caption = ProcessCaption(clientService, block.Caption);
            if (caption != null)
            {
                var element = new StackPanel
                {
                    Style = LayoutRoot.Resources["BlockSlideshowStyle"] as Style
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
            var element = new StackPanel { Style = LayoutRoot.Resources["BlockEmbedPostStyle"] as Style };

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
            textDate.Style = (Style)Resources["CaptionTextBlockStyle"];
            textDate.Foreground = (SolidColorBrush)Resources["SystemControlDisabledChromeDisabledLowBrush"];
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

        private void ProcessRichText(IClientService clientService, RichText text, Span span, RichTextBlock textBlock)
        {
            int offset = 0;
            var cached = new TextHighlighter();
            var marked = new TextHighlighter();

            ProcessRichText(clientService, text, span, textBlock, TextEffects.None, ref offset, cached.Ranges, marked.Ranges);

            if (cached.Ranges.Count > 0 && textBlock != null)
            {
                var accent = ActualTheme == ElementTheme.Light
                    ? Theme.AccentLight.Default
                    : Theme.AccentDark.Default;

                cached.Background = new SolidColorBrush(accent.WithAlpha(22));
                cached.Foreground = new SolidColorBrush(accent);

                textBlock.TextHighlighters.Add(cached);
            }

            if (marked.Ranges.Count > 0 && textBlock != null)
            {
                marked.Background = new SolidColorBrush(Colors.PaleGoldenrod);

                textBlock.TextHighlighters.Add(marked);
            }
        }

        private static int _target;
        private int _current;

        private bool ProcessRichText(IClientService clientService, RichText text, Span span, RichTextBlock textBlock, TextEffects effects, ref int offset, IList<TextRange> cached, IList<TextRange> marked)
        {
            switch (text)
            {
                case RichTextPlain plainText:
                    if (string.IsNullOrEmpty(plainText.Text))
                    {
                        return false;
                    }

                    if (effects.HasFlag(TextEffects.Cached))
                    {
                        cached.Add(new TextRange { StartIndex = offset, Length = plainText.Text.Length });
                    }
                    else if (effects.HasFlag(TextEffects.Marked))
                    {
                        marked.Add(new TextRange { StartIndex = offset, Length = plainText.Text.Length });
                    }

                    span.Inlines.Add(plainText.Text);
                    offset += plainText.Text.Length;
                    return true;
                case RichTexts concatText:
                    var added = false;

                    foreach (var concat in concatText.Texts)
                    {
                        var concatRun = new Span();

                        if (ProcessRichText(clientService, concat, concatRun, textBlock, effects, ref offset, cached, marked))
                        {
                            span.Inlines.Add(concatRun);
                            added = true;
                        }
                    }

                    return added;
                case RichTextBold boldText:
                    span.FontWeight = FontWeights.SemiBold;
                    return ProcessRichText(clientService, boldText.Text, span, textBlock, effects, ref offset, cached, marked);
                case RichTextEmailAddress emailText:
                    return ProcessRichText(clientService, emailText.Text, span, textBlock, effects, ref offset, cached, marked);
                case RichTextFixed fixedText:
                    span.FontFamily = new FontFamily("Consolas");
                    return ProcessRichText(clientService, fixedText.Text, span, textBlock, effects, ref offset, cached, marked);
                case RichTextItalic italicText:
                    span.FontStyle |= FontStyle.Italic;
                    return ProcessRichText(clientService, italicText.Text, span, textBlock, effects, ref offset, cached, marked);
                case RichTextStrikethrough strikeText:
                    span.TextDecorations |= TextDecorations.Strikethrough;
                    return ProcessRichText(clientService, strikeText.Text, span, textBlock, effects, ref offset, cached, marked);
                case RichTextUnderline underlineText:
                    span.TextDecorations |= TextDecorations.Underline;
                    return ProcessRichText(clientService, underlineText.Text, span, textBlock, effects, ref offset, cached, marked);
                case RichTextAnchorLink anchorLinkText:
                    try
                    {
                        var hyperlink = new Hyperlink { UnderlineStyle = UnderlineStyle.None };

                        if (ProcessRichText(clientService, anchorLinkText.Text, hyperlink, textBlock, effects | TextEffects.Cached, ref offset, cached, marked))
                        {
                            span.Inlines.Add(hyperlink);
                            hyperlink.Click += (s, args) => Hyperlink_Click(anchorLinkText);
                            Extensions.SetToolTip(hyperlink, anchorLinkText.Url);
                            MessageHelper.SetHyperlinkInfo(hyperlink, new TextEntityClickEventArgs(null, anchorLinkText.Url));
                            MessageHelper.SetEntityAction(hyperlink, () => Hyperlink_Click(anchorLinkText));

                            return true;
                        }

                        return false;
                    }
                    catch
                    {
                        Logger.Info("InstantPage: Probably nesting anchorLink inside textUrl");
                        return ProcessRichText(clientService, anchorLinkText.Text, span, textBlock, effects, ref offset, cached, marked);
                    }
                case RichTextUrl urlText:
                    try
                    {
                        if (urlText.IsCached)
                        {
                            effects |= TextEffects.Cached;
                        }

                        var hyperlink = new Hyperlink { UnderlineStyle = UnderlineStyle.None };

                        if (ProcessRichText(clientService, urlText.Text, hyperlink, textBlock, effects, ref offset, cached, marked))
                        {
                            span.Inlines.Add(hyperlink);
                            hyperlink.Click += (s, args) => Hyperlink_Click(urlText);
                            Extensions.SetToolTip(hyperlink, urlText.Url);
                            MessageHelper.SetHyperlinkInfo(hyperlink, new TextEntityClickEventArgs(null, urlText.Url));
                            MessageHelper.SetEntityAction(hyperlink, () => Hyperlink_Click(urlText));
                            return true;
                        }

                        return false;
                    }
                    catch
                    {
                        Logger.Info("InstantPage: Probably nesting textUrl inside textUrl");
                        return ProcessRichText(clientService, urlText.Text, span, textBlock, effects, ref offset, cached, marked);
                    }
                case RichTextReference reference:
                    return ProcessRichText(clientService, reference.Text, span, textBlock, effects, ref offset, cached, marked);
                case RichTextReferenceLink referenceLink:
                    try
                    {
                        var hyperlink = new Hyperlink { UnderlineStyle = UnderlineStyle.None };

                        if (ProcessRichText(clientService, referenceLink.Text, hyperlink, textBlock, effects | TextEffects.Cached, ref offset, cached, marked))
                        {
                            span.Inlines.Add(hyperlink);
                            //hyperlink.Click += (s, args) => Hyperlink_Click(reference);
                            Extensions.SetToolTip(hyperlink, referenceLink.Url);
                            MessageHelper.SetHyperlinkInfo(hyperlink, new TextEntityClickEventArgs(null, referenceLink.Url));
                            //MessageHelper.SetEntityAction(hyperlink, () => Hyperlink_Click(reference));

                            return true;
                        }

                        return false;
                    }
                    catch
                    {
                        Logger.Info("InstantPage: Probably nesting reference inside textUrl");
                        return ProcessRichText(clientService, referenceLink.Text, span, textBlock, effects, ref offset, cached, marked);
                    }
                case RichTextIcon icon:
                    var photo = new ImageView
                    {
                        Width = icon.Width,
                        Height = icon.Height
                    };

                    var file = icon.Document.DocumentValue;
                    if (file != null)
                    {
                        photo.SetSource(clientService, file, icon.Width, icon.Height);
                    }

                    var inline = new InlineUIContainer();
                    inline.Child = photo;
                    span.Inlines.Add(inline);
                    return true;
                case RichTextMarked markedText:
                    // ???
                    return ProcessRichText(clientService, markedText.Text, span, textBlock, effects | TextEffects.Marked, ref offset, cached, marked);
                case RichTextPhoneNumber phoneNumber:
                    try
                    {
                        var hyperlink = new Hyperlink { UnderlineStyle = UnderlineStyle.None };
                        span.Inlines.Add(hyperlink);
                        hyperlink.Click += (s, args) => Hyperlink_Click(phoneNumber);
                        return ProcessRichText(clientService, phoneNumber.Text, hyperlink, textBlock, effects, ref offset, cached, marked);
                    }
                    catch
                    {
                        Logger.Debug("InstantPage: Probably nesting phoneNumber inside textUrl");
                        return ProcessRichText(clientService, phoneNumber.Text, span, textBlock, effects, ref offset, cached, marked);
                    }
                case RichTextSubscript subscript:
                    Typography.SetVariants(span, FontVariants.Subscript);
                    return ProcessRichText(clientService, subscript.Text, span, textBlock, effects, ref offset, cached, marked);
                case RichTextSuperscript superscript:
                    Typography.SetVariants(span, FontVariants.Superscript);
                    return ProcessRichText(clientService, superscript.Text, span, textBlock, effects, ref offset, cached, marked);
                case RichTextCustomEmoji customEmoji:
                    {
                        var player = new CustomEmojiIcon();
                        var container = new InlineUIContainer
                        {
                            Child = player
                        };

                        player.LoopCount = 0;
                        player.HorizontalAlignment = HorizontalAlignment.Left;
                        player.FlowDirection = FlowDirection.LeftToRight;
                        //player.Style = EmojiStyle;
                        player.IsHitTestVisible = false;
                        player.IsEnabled = false;
                        player.IsViewportAware = true;
                        player.Emoji = customEmoji.AlternativeText;
                        player.Source = new CustomEmojiFileSource(clientService, customEmoji.CustomEmojiId);

                        if (textBlock.FontSize == 14)
                        {
                            player.Width = 20;
                            player.Height = 20;
                            player.Margin = new Thickness(0, -2, 0, -6);
                            player.FrameSize = new Size(20, 20);
                        }
                        else if (textBlock.FontSize == 12)
                        {
                            player.Margin = new Thickness(0, 0, 0, -4);
                            player.Width = 16;
                            player.Height = 16;
                            player.FrameSize = new Size(16, 16);
                        }
                        else
                        {
                            player.Width = textBlock.FontSize * (20d / 14d);
                            player.Height = textBlock.FontSize * (20d / 14d);
                            player.Margin = new Thickness(0, -2 * (20d / 14d), 0, -6 * (20d / 14d));
                            player.FrameSize = new Size(textBlock.FontSize * (20d / 14d), textBlock.FontSize * (20d / 14d));
                        }

                        span.Inlines.Add(container);
                        return true;
                    }
                case RichTextMathematicalExpression math:
                    {
                        //var tex = new RichMathSurface(math.Source);
                        //var output = new Image
                        //{
                        //    Width = tex.PixelWidth,
                        //    Height = tex.PixelHeight,
                        //    Margin = new Thickness(0, 0, 0, tex.Baseline * tex.PixelHeight - tex.PixelHeight),
                        //    Stretch = Stretch.Uniform
                        //};

                        //output.Loaded += (s, args) =>
                        //{
                        //    var width = (int)(tex.PixelWidth * XamlRoot.RasterizationScale);
                        //    var height = (int)(tex.PixelHeight * XamlRoot.RasterizationScale);

                        //    var bitmap = new WriteableBitmap(width, height);

                        //    tex.RenderSync(bitmap.PixelBuffer, XamlRoot.RasterizationScale, Colors.Black);

                        //    bitmap.Invalidate();
                        //    output.Source = bitmap;
                        //};

                        var tex = new RichMathImage
                        {
                            Source = math.Expression
                        };

                        if (tex.IsValid)
                        {
                            textBlock.MinHeight = Math.Max(textBlock.MinHeight, tex.PixelHeight);
                            tex.Margin = new Thickness(0, 0, 0, tex.Baseline * tex.PixelHeight - tex.PixelHeight);

                            span.Inlines.Add(new InlineUIContainer
                            {
                                Child = tex
                            });
                        }
                        else
                        {
                            ProcessRichText(clientService, new RichTextPlain(math.Expression), span, textBlock, effects, ref offset, cached, marked);
                        }
                    }
                    return true;
                default:
                    span.Inlines.Add(text.GetType().Name);
                    return true;
            }
        }

        [Flags]
        private enum TextEffects
        {
            None,
            Cached,
            Marked
        }

        private double SpacingBetweenBlocks(PageBlock lower, IList<PageBlock> blocks, int index)
        {
            var upper = index > 0 ? blocks[index - 1] : null;
            if (upper == null)
            {
                if (lower is PageBlockAnimation or PageBlockCollage or PageBlockCover or PageBlockMap or PageBlockPhoto or PageBlockSlideshow or PageBlockVideo or PageBlockAnchor)
                {
                    return 0;
                }
            }

            if (upper is PageBlockParagraph && lower is PageBlockParagraph)
            {
                return 0;
            }

            if (upper is PageBlockAnchor && lower is PageBlockAnchor)
            {
                return 0;
            }

            if (upper is PageBlockDivider || lower is PageBlockDivider)
            {
                return 12;
            }

            return 8;
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

        private void Image_Click(object sender, RoutedEventArgs e)
        {
            //var image = sender as ImageView;
            //var item = image.DataContext as GalleryMedia;
            //if (item != null)
            //{
            //    ViewModel.Gallery.SelectedItem = item;
            //    ViewModel.Gallery.FirstItem = item;

            //    ViewModel.NavigationService.ShowGallery(ViewModel.Gallery, image);
            //}
        }

        private async void Hyperlink_Click(RichTextAnchorLink anchorLinkText)
        {
            //if (string.IsNullOrEmpty(anchorLinkText.AnchorName))
            //{
            //    ScrollingHost.ScrollToTop();
            //}
            //else if (_anchors.TryGetValue(anchorLinkText.AnchorName, out Border anchor))
            //{
            //    await ScrollingHost.ScrollToItem2(anchor, VerticalAlignment.Top);
            //}
        }

        private async void Hyperlink_Click(RichTextUrl urlText)
        {
            //ViewModel.IsLoading = true;

            //var response = await ViewModel.ClientService.SendAsync(new GetWebPageInstantView(urlText.Url, false));
            //if (response is WebPageInstantView instantView)
            //{
            //    ViewModel.IsLoading = false;
            //    ViewModel.NavigationService.Navigate(typeof(InstantPage), new InstantPageArgs(instantView, urlText.Url));
            //}
            //else if (MessageHelper.TryCreateUri(urlText.Url, out Uri url))
            //{
            //    ViewModel.IsLoading = false;
            //    OpenUrl(url);
            //}
        }

        private async void OpenUrl(Uri url)
        {
            //if (MessageHelper.IsTelegramUrl(url))
            //{
            //    var clientService = ViewModel.ClientService;
            //    ByNavigation(navigation => MessageHelper.OpenTelegramUrl(clientService, navigation, url));
            //}
            //else
            //{
            //    await Launcher.LaunchUriAsync(url);
            //}
        }

        private async void ByNavigation(Action<INavigationService> action)
        {
            //WindowContext.Main.Dispatcher.Dispatch(() => action(WindowContext.Main.GetNavigationService()));
            //await ApplicationViewSwitcher.SwitchAsync(WindowContext.Main.Id);
        }

        private void Hyperlink_Click(RichTextPhoneNumber phoneNumber)
        {

        }

        private void Header_GoBackClicked(object sender, RoutedEventArgs e)
        {
            //Frame.GoBack();
        }

        private void Header_GoForwardClicked(object sender, RoutedEventArgs e)
        {
            //Frame.GoForward();
        }

        private void Feedback_Click(object sender, RoutedEventArgs e)
        {
            //var viewModel = ViewModel;
            //ByNavigation(navigation => viewModel.Feedback(navigation));
        }

        private void Share_Click(object sender, RoutedEventArgs e)
        {
            //var link = ViewModel.ShareLink;
            //if (link == null)
            //{
            //    return;
            //}

            //this.ShowPopup(ViewModel.Session, new ChooseChatsPopup(), new ChooseChatsConfigurationPostLink(new HttpUrl(link.ToString())));
        }

        private void Browser_Click(object sender, RoutedEventArgs e)
        {
            //var link = ViewModel.ShareLink;
            //if (link == null)
            //{
            //    return;
            //}

            //MessageHelper.OpenUrl(null, null, link.ToString());
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            //var link = ViewModel.ShareLink;
            //if (link == null)
            //{
            //    return;
            //}

            //MessageHelper.CopyLink(XamlRoot, link.ToString());
        }

        private int _zoomFactor = 7;
        private readonly double[] _zoomFactors = new double[]
        {
            100d / 25,
            100d / 33,
            100d / 50,
            100d / 67,
            100d / 75,
            100d / 80,
            100d / 90,
            100d / 100,
            100d / 110,
            100d / 125,
            100d / 150,
            100d / 175,
            100d / 200,
            100d / 250,
            100d / 300,
            100d / 400,
            100d / 500
        };

        private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            //var modifiers = WindowContext.KeyModifiers();
            //if (modifiers == VirtualKeyModifiers.Control)
            //{
            //    var pointer = e.GetCurrentPoint(this);
            //    var zoom = ZoomingHost.ZoomFactor;
            //    var delta = pointer.Properties.MouseWheelDelta > 0 ? 1 : -1;

            //    var index = _zoomFactor + delta;
            //    if (index >= 0 && index < _zoomFactors.Length)
            //    {
            //        _zoomFactor = index;
            //        ZoomingHost.ZoomFactor = _zoomFactors[index];
            //    }

            //    e.Handled = true;
            //}
        }
    }
}
