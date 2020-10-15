using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Gallery;
using Unigram.Controls.Messages;
using Unigram.Controls.Messages.Content;
using Unigram.Converters;
using Unigram.Services;
using Unigram.ViewModels;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Gallery;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.System;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

namespace Unigram.Views
{
    public sealed partial class InstantPage : HostedPage, IMessageDelegate, IHandle<UpdateFile>
    {
        public InstantViewModel ViewModel => DataContext as InstantViewModel;

        public IEventAggregator Aggregator => throw new NotImplementedException();

        private readonly string _injectedJs;
        private ScrollViewer _scrollingHost;

        private FileContext<Tuple<IContentWithFile, MessageViewModel>> _filesMap = new FileContext<Tuple<IContentWithFile, MessageViewModel>>();
        private FileContext<Image> _iconsMap = new FileContext<Image>();

        private List<(AnimationContent, AnimationView)> _animations = new List<(AnimationContent, AnimationView)>();

        public InstantPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<InstantViewModel>();

            if (ApiInformation.IsEnumNamedValuePresent("Windows.UI.Xaml.Controls.Primitives.FlyoutPlacementMode", "BottomEdgeAlignedRight"))
            {
                EllipsisFlyout.Placement = FlyoutPlacementMode.BottomEdgeAlignedRight;
            }

            var jsPath = System.IO.Path.Combine(Package.Current.InstalledLocation.Path, "Assets", "Webviews", "injected.js");
            _injectedJs = System.IO.File.ReadAllText(jsPath);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ViewModel.Aggregator.Subscribe(this);

            var scroll = ScrollingHost.GetScrollViewer();
            if (scroll != null)
            {
                scroll.ViewChanged += OnViewChanged;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ViewModel.Aggregator.Unsubscribe(this);

            foreach (var animation in _animations)
            {
                try
                {
                    animation.Item1.Children.Remove(animation.Item2);
                }
                catch { }
            }
        }

        private void OnViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (sender is ScrollViewer scroll && scroll.ScrollableHeight > 0)
            {
                Reading.Value = scroll.VerticalOffset / scroll.ScrollableHeight * 100;
            }
            else
            {
                Reading.Value = 0;
            }
        }

        public void Handle(UpdateFile update)
        {
            if (_filesMap.TryGetValue(update.File.Id, out List<Tuple<IContentWithFile, MessageViewModel>> elements))
            {
                this.BeginOnUIThread(() =>
                {
                    foreach (var panel in elements)
                    {
                        panel.Item2.UpdateFile(update.File);
                        panel.Item1.UpdateFile(panel.Item2, update.File);

                        if (panel.Item1 is AnimationContent content && panel.Item2.Content is MessageAnimation animation)
                        {
                            if (update.File.Local.IsDownloadingCompleted && update.File.Id == animation.Animation.AnimationValue.Id)
                            {
                                var presenter = new AnimationView();
                                presenter.AutoPlay = true;
                                presenter.IsLoopingEnabled = true;
                                presenter.IsHitTestVisible = false;
                                presenter.Source = UriEx.GetLocal(update.File.Local.Path);

                                content.Children.Add(presenter);

                                _animations.Add((content, presenter));
                            }
                        }
                    }

                    if (update.File.Local.IsDownloadingCompleted && !update.File.Remote.IsUploadingActive)
                    {
                        elements.Clear();
                    }
                });
            }

            if (_iconsMap.TryGetValue(update.File.Id, out List<Image> photos))
            {
                this.BeginOnUIThread(() =>
                {
                    if (update.File.Local.IsDownloadingCompleted && !update.File.Remote.IsUploadingActive)
                    {
                        foreach (var photo in photos)
                        {
                            photo.Source = new BitmapImage(new Uri("file:///" + update.File.Local.Path));
                        }

                        photos.Clear();
                    }
                });
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            ScrollingHost.Items.Clear();
            ViewModel.Gallery.Items.Clear();
            ViewModel.Gallery.TotalItems = 0;
            ViewModel.Gallery.SelectedItem = null;
            _anchors.Clear();

            var url = e.Parameter as string;
            if (url == null)
            {
                return;
            }

            ViewModel.IsLoading = true;

            var response = await ViewModel.ProtoService.SendAsync(new GetWebPageInstantView(url, true));
            if (response is WebPageInstantView instantView)
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
                {
                    ViewModel.ShareLink = uri;
                    ViewModel.ShareTitle = url;

                    //if (uri.Fragment.Length > 0 && _anchors.TryGetValue(uri.Fragment.Substring(1), out Border anchor))
                    //{
                    //    await ScrollingHost.ScrollToItem(anchor, SnapPointsAlignment.Near, false);
                    //}
                }

                UpdateView(instantView);
                ViewModel.IsLoading = false;
            }

            base.OnNavigatedTo(e);
        }

        private WebPageInstantView _instantView;

        private void UpdateView(WebPageInstantView instantView)
        {
            _instantView = instantView;

            ScrollingHost.FlowDirection = instantView.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

            if (instantView.ViewCount > 0)
            {
                ViewsLabel.Text = Locale.Declension("Views", instantView.ViewCount);
            }
            else
            {
                ViewsLabel.Text = string.Empty;
            }

            var processed = 0;
            PageBlock previousBlock = null;
            FrameworkElement previousElement = null;
            FrameworkElement firstElement = null;
            foreach (var block in instantView.PageBlocks)
            {
                var element = ProcessBlock(block);
                var spacing = SpacingBetweenBlocks(previousBlock, block);
                var padding = PaddingForBlock(block);

                if (element != null)
                {
                    if (block is PageBlockChatLink && previousBlock is PageBlockCover)
                    {
                        if (previousElement is StackPanel stack && element is Button)
                        {
                            element.Style = Resources["CoverChannelBlockStyle"] as Style;
                            element.Margin = new Thickness(padding, -40, padding, 0);
                            stack.Children.Insert(1, element);
                        }
                    }
                    else
                    {
                        element.Margin = new Thickness(padding, spacing, padding, 0);
                        ScrollingHost.Items.Add(element);
                    }
                }

                if (firstElement == null)
                {
                    firstElement = element;
                }

                previousBlock = block;
                previousElement = element;
                processed++;
            }

            if (firstElement != null)
            {
                firstElement.Loaded += (s, args) =>
                {
                    if (ViewModel.ShareLink?.Fragment?.Length > 0)
                    {
                        Hyperlink_Click(new RichTextUrl { Url = ViewModel.ShareLink.AbsoluteUri });
                    }
                };
            }
        }

        private long _webpageId;

        //private Stack<Panel> _containers = new Stack<Panel>();
        private double _padding = 12;

        private Dictionary<string, Border> _anchors = new Dictionary<string, Border>();

        private FrameworkElement ProcessBlock(PageBlock block)
        {
            switch (block)
            {
                case PageBlockCover cover:
                    return ProcessCover(cover);
                case PageBlockAuthorDate authorDate:
                    return ProcessAuthorDate(authorDate);
                case PageBlockHeader header:
                case PageBlockSubheader subheader:
                case PageBlockTitle title:
                case PageBlockSubtitle subtitle:
                case PageBlockFooter footer:
                case PageBlockParagraph paragraph:
                case PageBlockKicker kicker:
                    return ProcessText(block, false);
                case PageBlockBlockQuote blockquote:
                    return ProcessBlockquote(blockquote);
                case PageBlockDivider divider:
                    return ProcessDivider(divider);
                case PageBlockPhoto photo:
                    return ProcessPhoto(photo);
                case PageBlockList list:
                    return ProcessList(list);
                case PageBlockVideo video:
                    return ProcessVideo(video);
                case PageBlockAnimation animation:
                    return ProcessAnimation(animation);
                case PageBlockEmbeddedPost embedPost:
                    return ProcessEmbedPost(embedPost);
                case PageBlockSlideshow slideshow:
                    return ProcessSlideshow(slideshow);
                case PageBlockCollage collage:
                    return ProcessCollage(collage);
                case PageBlockEmbedded embed:
                    return ProcessEmbed(embed);
                case PageBlockPullQuote pullquote:
                    return ProcessPullquote(pullquote);
                case PageBlockAnchor anchor:
                    return ProcessAnchor(anchor);
                case PageBlockPreformatted preformatted:
                    return ProcessPreformatted(preformatted);
                case PageBlockChatLink channel:
                    return ProcessChannel(channel);
                case PageBlockDetails details:
                    return ProcessDetails(details);
                case PageBlockTable table:
                    return ProcessTable(table);
                case PageBlockRelatedArticles relatedArticles:
                    return ProcessRelatedArticles(relatedArticles);
                case PageBlockMap map:
                    return ProcessMap(map);
                default:
                    return ProcessUnsupported(block);
            }

            return null;
        }

        #region 2.0

        private FrameworkElement ProcessMap(PageBlockMap map)
        {
            var latitude = map.Location.Latitude.ToString(CultureInfo.InvariantCulture);
            var longitude = map.Location.Longitude.ToString(CultureInfo.InvariantCulture);

            var image = new ImageView();
            image.Source = new BitmapImage(new Uri(string.Format("https://dev.virtualearth.net/REST/v1/Imagery/Map/Road/{0},{1}/{2}?mapSize={3},{4}&key=FgqXCsfOQmAn9NRf4YJ2~61a_LaBcS6soQpuLCjgo3g~Ah_T2wZTc8WqNe9a_yzjeoa5X00x4VJeeKH48wAO1zWJMtWg6qN-u4Zn9cmrOPcL", latitude, longitude, map.Zoom, map.Width, map.Height)));
            image.Constraint = map;

            var caption = ProcessCaption(map.Caption);
            if (caption != null)
            {
                caption.Margin = new Thickness(12, 8, 0, 0);

                var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                panel.Children.Add(image);
                panel.Children.Add(caption);

                return panel;
            }

            return image;
        }

        private FrameworkElement ProcessRelatedArticles(PageBlockRelatedArticles relatedArticles)
        {
            var panel = new StackPanel();

            var header = ProcessText(relatedArticles, false);
            if (header != null)
            {
                var border = new Border { Style = Resources["BlockRelatedArticlesHeaderPanelStyle"] as Style };
                border.Child = header;

                panel.Children.Add(border);
            }

            foreach (var article in relatedArticles.Articles)
            {
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });

                var title = new TextBlock { Text = article.Title };
                var description = new TextBlock { TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 2, Style = Resources["BlockAuthorDateTextBlockStyle"] as Style };

                if (string.IsNullOrEmpty(article.Author))
                {
                    description.Text = article.Description;
                }
                else
                {
                    description.Text = article.Author;

                    if (article.PublishDate > 0)
                    {
                        description.Text += " — " + BindConvert.Current.DayMonthFullYear.Format(BindConvert.Current.DateTime(article.PublishDate));
                    }
                }

                if (article.Photo != null)
                {
                    var photo = new Image { Width = 36, Height = 36, Stretch = Stretch.UniformToFill, VerticalAlignment = VerticalAlignment.Top };

                    var file = article.Photo.GetSmall()?.Photo;
                    if (file.Local.IsDownloadingCompleted)
                    {
                        photo.Source = new BitmapImage(new Uri("file:///" + file.Local.Path));
                    }
                    else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                    {
                        _iconsMap[file.Id].Add(photo);
                        ViewModel.ProtoService.DownloadFile(file.Id, 1);
                    }

                    Grid.SetColumn(photo, 1);
                    Grid.SetRowSpan(photo, 2);

                    grid.Children.Add(photo);
                }

                Grid.SetRow(description, 1);

                grid.Children.Add(title);
                grid.Children.Add(description);

                var button = new BadgeButton { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, Margin = new Thickness(-12, 0, -12, 0) };
                button.Content = grid;
                button.Click += (s, args) => Hyperlink_Click(new RichTextUrl(null, article.Url, true));

                panel.Children.Add(button);
            }

            return panel;
        }

        private FrameworkElement ProcessTable(PageBlockTable table)
        {
            var grid = new Grid();
            grid.BorderThickness = new Thickness(table.IsBordered ? 1 : 0, table.IsBordered ? 1 : 0, 0, 0);
            grid.BorderBrush = new SolidColorBrush(Colors.Green);

            var columns = table.Cells.Max(x => x.Count);
            var rows = table.Cells.Count;

            for (int i = 0; i < columns; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto), MaxWidth = 200 });
            }

            var row = 0;
            var offset = new Dictionary<int, int>();

            foreach (var rowz in table.Cells)
            {
                var column = 0;

                if (offset.TryGetValue(row, out int adjust))
                {
                    column = adjust;
                }

                foreach (var cell in rowz)
                {
                    var textBlock = new RichTextBlock();
                    var span = new Span();
                    var paragraph = new Paragraph();
                    paragraph.Inlines.Add(span);
                    textBlock.Blocks.Add(paragraph);
                    textBlock.TextWrapping = TextWrapping.Wrap;

                    switch (cell.Align)
                    {
                        case PageBlockHorizontalAlignmentLeft left:
                            textBlock.TextAlignment = TextAlignment.Left;
                            break;
                        case PageBlockHorizontalAlignmentCenter center:
                            textBlock.TextAlignment = TextAlignment.Center;
                            break;
                        case PageBlockHorizontalAlignmentRight right:
                            textBlock.TextAlignment = TextAlignment.Right;
                            break;
                    }

                    switch (cell.Valign)
                    {
                        case PageBlockVerticalAlignmentTop top:
                            textBlock.VerticalAlignment = VerticalAlignment.Top;
                            break;
                        case PageBlockVerticalAlignmentMiddle middle:
                            textBlock.VerticalAlignment = VerticalAlignment.Center;
                            break;
                        case PageBlockVerticalAlignmentBottom bottom:
                            textBlock.VerticalAlignment = VerticalAlignment.Bottom;
                            break;
                    }

                    //textBlock.Margin = new Thickness(12, 0, 12, 12);
                    ProcessRichText(cell.Text, span, textBlock);

                    var border = new Border();
                    border.Background = cell.IsHeader || (table.IsStriped && row % 2 == 0) ? new SolidColorBrush(Colors.LightGray) : null;
                    border.BorderThickness = new Thickness(0, 0, table.IsBordered ? 1 : 0, table.IsBordered ? 1 : 0);
                    border.BorderBrush = new SolidColorBrush(Colors.Green);
                    border.Child = textBlock;
                    border.Padding = new Thickness(8, 4, 8, 4);

                    Grid.SetRow(border, row);
                    Grid.SetRowSpan(border, cell.Rowspan);
                    Grid.SetColumn(border, column);
                    Grid.SetColumnSpan(border, cell.Colspan);

                    if (cell.Rowspan > 1 && column == 0)
                    {
                        for (int i = 1; i < cell.Rowspan; i++)
                        {
                            offset[row + i] = cell.Colspan;
                        }
                    }

                    grid.Children.Add(border);

                    column += cell.Colspan;
                }

                grid.RowDefinitions.Add(new RowDefinition());

                row++;
            }

            var scroll = new ScrollViewer();
            scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            scroll.HorizontalScrollMode = ScrollMode.Auto;
            scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            scroll.VerticalScrollMode = ScrollMode.Disabled;

            scroll.Content = grid;

            var caption = ProcessText(table, true);
            if (caption != null)
            {
                var panel = new StackPanel();
                panel.Children.Add(caption);
                panel.Children.Add(scroll);

                return panel;
            }

            return scroll;
        }

        private FrameworkElement ProcessDetails(PageBlockDetails details)
        {
            var panel = new StackPanel();

            var header = new BadgeButton { Content = ProcessText(details, false), Glyph = details.IsOpen ? "\uE098" : "\uE099", Style = App.Current.Resources["GlyphBadgeButtonStyle"] as Style, Margin = new Thickness(-12, 0, -12, 0) };
            var inner = new StackPanel { Padding = new Thickness(0, 12, 0, 12), Visibility = details.IsOpen ? Visibility.Visible : Visibility.Collapsed };

            panel.Children.Add(header);
            panel.Children.Add(inner);

            foreach (var block in details.PageBlocks)
            {
                inner.Children.Add(ProcessBlock(block));
            }

            header.Click += (s, args) =>
            {
                inner.Visibility = inner.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                header.Glyph = inner.Visibility == Visibility.Visible ? "\uE098" : "\uE099";
            };

            return panel;
        }

        #endregion

        private FrameworkElement ProcessCover(PageBlockCover block)
        {
            return ProcessBlock(block.Cover);
        }

        private FrameworkElement ProcessChannel(PageBlockChatLink channel)
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

        private FrameworkElement ProcessAuthorDate(PageBlockAuthorDate block)
        {
            var textBlock = new TextBlock { Style = Resources["BlockAuthorDateTextBlockStyle"] as Style };

            if (!block.Author.IsNullOrEmpty())
            {
                var span = new Span();
                textBlock.Inlines.Add(new Run { Text = string.Format(Strings.Resources.ArticleByAuthor, string.Empty) });
                textBlock.Inlines.Add(span);
                ProcessRichText(block.Author, span, null);
            }

            //textBlock.Inlines.Add(new Run { Text = DateTimeFormatter.LongDate.Format(BindConvert.Current.DateTime(block.PublishedDate)) });
            if (block.PublishDate > 0)
            {
                if (textBlock.Inlines.Count > 0)
                {
                    textBlock.Inlines.Add(new Run { Text = " — " });
                }

                textBlock.Inlines.Add(new Run { Text = BindConvert.Current.DayMonthFullYear.Format(BindConvert.Current.DateTime(block.PublishDate)) });
            }

            return textBlock;
        }

        private FrameworkElement ProcessText(PageBlock block, bool caption)
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
                    text = caption ? blockquote.Credit : blockquote.Text;
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
            }

            if (text == null || text is RichTextPlain plain && string.IsNullOrEmpty(plain.Text))
            {
                return null;
            }

            var textBlock = new RichTextBlock();
            var span = new Span();
            var paragraph = new Paragraph();
            paragraph.Inlines.Add(span);
            textBlock.Blocks.Add(paragraph);
            textBlock.TextWrapping = TextWrapping.Wrap;

            textBlock.ContextRequested += Text_ContextRequested;
            textBlock.ContextMenuOpening += Text_ContextMenuOpening;

            if (ApiInfo.CanAddContextRequestedEvent)
            {
                textBlock.AddHandler(ContextRequestedEvent, new TypedEventHandler<UIElement, ContextRequestedEventArgs>(Text_ContextRequested), true);
            }

            //textBlock.Margin = new Thickness(12, 0, 12, 12);
            ProcessRichText(text, span, textBlock);

            switch (block)
            {
                case PageBlockTitle title:
                    textBlock.FontSize = 28;
                    textBlock.FontFamily = new FontFamily("Times New Roman");
                    //textBlock.TextLineBounds = TextLineBounds.TrimToBaseline;
                    break;
                case PageBlockSubtitle subtitle:
                    textBlock.FontSize = 17;
                    //textBlock.FontFamily = new FontFamily("Times New Roman");
                    //textBlock.TextLineBounds = TextLineBounds.TrimToBaseline;
                    break;
                case PageBlockHeader header:
                    textBlock.Style = Resources["BlockHeaderTextBlockStyle"] as Style;
                    break;
                case PageBlockSubheader subheader:
                    textBlock.Style = Resources["BlockSubheaderTextBlockStyle"] as Style;
                    break;
                case PageBlockParagraph paragraphz:
                    textBlock.Style = Resources["BlockBodyTextBlockStyle"] as Style;
                    break;
                case PageBlockPreformatted preformatted:
                    textBlock.FontSize = 16;
                    break;
                case PageBlockFooter footer:
                    textBlock.Style = Resources["BlockCaptionTextBlockStyle"] as Style;
                    //textBlock.TextAlignment = TextAlignment.Center;
                    break;
                case PageBlockPhoto photo:
                case PageBlockVideo video:
                    textBlock.Style = Resources["BlockCaptionTextBlockStyle"] as Style;
                    textBlock.TextAlignment = TextAlignment.Center;
                    break;
                case PageBlockSlideshow slideshow:
                case PageBlockEmbedded embed:
                case PageBlockEmbeddedPost embedPost:
                    textBlock.Style = Resources["BlockCaptionTextBlockStyle"] as Style;
                    //textBlock.TextAlignment = TextAlignment.Center;
                    break;
                case PageBlockBlockQuote blockquote:
                    if (caption)
                    {
                        textBlock.Style = Resources["BlockCaptionTextBlockStyle"] as Style;
                        textBlock.Margin = new Thickness(0, 12, 0, 0);
                    }
                    else
                    {
                        textBlock.Style = Resources["BlockBodyTextBlockStyle"] as Style;
                    }
                    break;
                case PageBlockPullQuote pullquote:
                    if (caption)
                    {
                        textBlock.Style = Resources["BlockCaptionTextBlockStyle"] as Style;
                    }
                    else
                    {
                        textBlock.Style = Resources["BlockBodyTextBlockStyle"] as Style;
                        textBlock.FontFamily = new FontFamily("Times New Roman");
                        //textBlock.TextLineBounds = TextLineBounds.TrimToBaseline;
                        textBlock.TextAlignment = TextAlignment.Center;
                    }
                    break;
                case PageBlockDetails details:
                    textBlock.IsTextSelectionEnabled = false;
                    break;
                case PageBlockRelatedArticles relatedArticles:
                    textBlock.Style = Resources["BlockRelatedArticlesHeaderStyle"] as Style;
                    break;
            }

            return textBlock;
        }

        private void Text_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            MessageHelper.Hyperlink_ContextRequested(null, sender, args);
        }

        private void Text_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            e.Handled = true;
        }

        private FrameworkElement ProcessCaption(PageBlockCaption caption)
        {
            var textEmpty = caption.Text == null || caption.Text is RichTextPlain plain1 && string.IsNullOrEmpty(plain1.Text);
            var citeEmpty = caption.Credit == null || caption.Credit is RichTextPlain plain2 && string.IsNullOrEmpty(plain2.Text);

            if (textEmpty && citeEmpty)
            {
                return null;
            }

            var textBlock = new RichTextBlock();
            var span = new Span();
            var paragraph = new Paragraph();
            paragraph.Inlines.Add(span);
            textBlock.Blocks.Add(paragraph);
            textBlock.TextWrapping = TextWrapping.Wrap;

            if (!textEmpty)
            {
                ProcessRichText(caption.Text, span, textBlock);
            }

            if (!citeEmpty)
            {
                if (!textEmpty)
                {
                    span.Inlines.Add(new LineBreak());
                }

                ProcessRichText(caption.Credit, span, textBlock);
            }

            return textBlock;
        }

        private FrameworkElement ProcessUnsupported(PageBlock block)
        {
            return new TextBlock { Text = block.ToString() };
        }

        private FrameworkElement ProcessPreformatted(PageBlockPreformatted block)
        {
            var element = new StackPanel { Style = Resources["BlockPreformattedStyle"] as Style };


            var text = ProcessText(block, false);
            if (text != null) element.Children.Add(text);

            return element;
        }

        private FrameworkElement ProcessDivider(PageBlockDivider block)
        {
            var element = new Rectangle { Style = Resources["BlockDividerStyle"] as Style };
            return element;
        }

        private FrameworkElement ProcessList(PageBlockList block)
        {
            var panel = new Grid();
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            panel.ColumnDefinitions.Add(new ColumnDefinition());

            var row = 0;

            foreach (var item in block.Items)
            {
                var label = new TextBlock { Text = item.Label, TextAlignment = TextAlignment.Right, Margin = new Thickness(0, 0, 8, 0) };
                var stack = new StackPanel();

                foreach (var inner in item.PageBlocks)
                {
                    var child = ProcessBlock(inner);
                    if (child != null)
                    {
                        stack.Children.Add(child);
                    }
                }

                Grid.SetRow(label, row);
                Grid.SetRow(stack, row);
                Grid.SetColumn(stack, 1);

                panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                panel.Children.Add(label);
                panel.Children.Add(stack);

                row++;
            }

            return panel;
        }

        private FrameworkElement ProcessBlockquote(PageBlockBlockQuote block)
        {
            var element = new StackPanel { Style = Resources["BlockBlockquoteStyle"] as Style };

            var text = ProcessText(block, false);
            if (text != null) element.Children.Add(text);

            var caption = ProcessText(block, true);
            if (caption != null) element.Children.Add(caption);

            return element;
        }

        private FrameworkElement ProcessPullquote(PageBlockPullQuote block)
        {
            var element = new StackPanel { Style = Resources["BlockPullquoteStyle"] as Style };

            var text = ProcessText(block, false);
            if (text != null) element.Children.Add(text);

            var caption = ProcessText(block, true);
            if (caption != null) element.Children.Add(caption);

            return element;
        }

        private FrameworkElement ProcessPhoto(PageBlockPhoto block)
        {
            var galleryItem = new GalleryPhoto(ViewModel.ProtoService, block.Photo, block.Caption.ToPlainText());
            ViewModel.Gallery.Items.Add(galleryItem);

            var message = GetMessage(new MessagePhoto(block.Photo, null, false));
            var element = new StackPanel { Style = Resources["BlockPhotoStyle"] as Style };

            var content = new PhotoContent(message);
            content.Tag = galleryItem;
            content.HorizontalAlignment = HorizontalAlignment.Center;
            content.ClearValue(MaxWidthProperty);
            content.ClearValue(MaxHeightProperty);

            foreach (var size in block.Photo.Sizes)
            {
                _filesMap[size.Photo.Id].Add(Tuple.Create(content as IContentWithFile, message));
            }

            element.Children.Add(content);

            var caption = ProcessCaption(block.Caption);
            if (caption != null)
            {
                caption.Margin = new Thickness(12, 8, 0, 0);
                element.Children.Add(caption);
            }

            return element;
        }

        private FrameworkElement ProcessVideo(PageBlockVideo block)
        {
            var galleryItem = new GalleryVideo(ViewModel.ProtoService, block.Video, block.Caption.ToPlainText());
            ViewModel.Gallery.Items.Add(galleryItem);

            var message = GetMessage(new MessageVideo(block.Video, null, false));
            var element = new StackPanel { Style = Resources["BlockVideoStyle"] as Style };

            var content = new VideoContent(message);
            content.Tag = galleryItem;
            content.HorizontalAlignment = HorizontalAlignment.Center;
            content.ClearValue(MaxWidthProperty);
            content.ClearValue(MaxHeightProperty);

            if (block.Video.Thumbnail != null)
            {
                _filesMap[block.Video.Thumbnail.File.Id].Add(Tuple.Create(content as IContentWithFile, message));
            }

            _filesMap[block.Video.VideoValue.Id].Add(Tuple.Create(content as IContentWithFile, message));

            element.Children.Add(content);

            var caption = ProcessCaption(block.Caption);
            if (caption != null)
            {
                caption.Margin = new Thickness(12, 8, 0, 0);
                element.Children.Add(caption);
            }

            return element;
        }

        private FrameworkElement ProcessAnimation(PageBlockAnimation block)
        {
            var galleryItem = new GalleryAnimation(ViewModel.ProtoService, block.Animation, block.Caption.ToPlainText());
            ViewModel.Gallery.Items.Add(galleryItem);

            var message = GetMessage(new MessageAnimation(block.Animation, null, false));
            var element = new StackPanel { Style = Resources["BlockVideoStyle"] as Style };

            var content = new AnimationContent(message);
            content.Tag = galleryItem;
            content.HorizontalAlignment = HorizontalAlignment.Center;
            content.ClearValue(MaxWidthProperty);
            content.ClearValue(MaxHeightProperty);

            if (block.Animation.AnimationValue.Local.IsDownloadingCompleted)
            {
                var presenter = new AnimationView();
                presenter.AutoPlay = true;
                presenter.IsLoopingEnabled = true;
                presenter.IsHitTestVisible = false;
                presenter.Source = UriEx.GetLocal(block.Animation.AnimationValue.Local.Path);

                content.Children.Add(presenter);

                _animations.Add((content, presenter));
            }

            if (block.Animation.Thumbnail != null)
            {
                _filesMap[block.Animation.Thumbnail.File.Id].Add(Tuple.Create(content as IContentWithFile, message));
            }

            _filesMap[block.Animation.AnimationValue.Id].Add(Tuple.Create(content as IContentWithFile, message));

            element.Children.Add(content);

            var caption = ProcessCaption(block.Caption);
            if (caption != null)
            {
                caption.Margin = new Thickness(12, 8, 0, 0);
                element.Children.Add(caption);
            }

            return element;
        }

        private MessageViewModel GetMessage(MessageContent content)
        {
            return ViewModel.CreateMessage(this, new Message { Content = content });
        }

        private FrameworkElement ProcessEmbed(PageBlockEmbedded block)
        {
            var element = new StackPanel { Style = Resources["BlockEmbedStyle"] as Style };

            FrameworkElement child = null;

            //if (block.HasPosterPhotoId)
            //{
            //    var photo = page.Photos.FirstOrDefault(x => x.Id == block.PosterPhotoId);
            //    var image = new ImageView();
            //    image.Source = (ImageSource)DefaultPhotoConverter.Convert(photo, "thumbnail");
            //    image.Constraint = photo;
            //    child = image;
            //}
            if (!string.IsNullOrEmpty(block.Html))
            {
                var view = new WebView();
                if (!block.AllowScrolling)
                {
                    view.NavigationCompleted += OnWebViewNavigationCompleted;
                }
                view.NavigateToString(block.Html.Replace("src=\"//", "src=\"https://"));

                var ratio = new RatioControl();
                ratio.MaxWidth = block.Width;
                ratio.MaxHeight = block.Height;
                ratio.Content = view;
                ratio.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                ratio.VerticalContentAlignment = VerticalAlignment.Stretch;
                child = ratio;
            }
            else if (!string.IsNullOrEmpty(block.Url))
            {
                var view = new WebView();
                if (!block.AllowScrolling)
                {
                    view.NavigationCompleted += OnWebViewNavigationCompleted;
                }
                view.Navigate(new Uri(block.Url));

                var ratio = new RatioControl();
                ratio.MaxWidth = block.Width;
                ratio.MaxHeight = block.Height;
                ratio.Content = view;
                ratio.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                ratio.VerticalContentAlignment = VerticalAlignment.Stretch;
                child = ratio;
            }

            element.Children.Add(child);

            var caption = ProcessCaption(block.Caption);
            if (caption != null)
            {
                caption.Margin = new Thickness(12, 8, 0, 0);
                element.Children.Add(caption);
            }

            return element;
        }

        private FrameworkElement ProcessSlideshow(PageBlockSlideshow block)
        {
            var element = new StackPanel { Style = Resources["BlockSlideshowStyle"] as Style };

            var items = new List<FrameworkElement>();
            foreach (var item in block.PageBlocks)
            {
                if (item is PageBlockPhoto photoBlock)
                {
                    var galleryItem = new GalleryPhoto(ViewModel.ProtoService, photoBlock.Photo, block.Caption.ToPlainText());
                    ViewModel.Gallery.Items.Add(galleryItem);

                    var message = GetMessage(new MessagePhoto(photoBlock.Photo, null, false));

                    var content = new PhotoContent(message);
                    content.Tag = galleryItem;
                    content.HorizontalAlignment = HorizontalAlignment.Center;
                    content.ClearValue(MaxWidthProperty);
                    content.ClearValue(MaxHeightProperty);

                    foreach (var size in photoBlock.Photo.Sizes)
                    {
                        _filesMap[size.Photo.Id].Add(Tuple.Create(content as IContentWithFile, message));
                    }

                    items.Add(content);
                }
                else if (item is PageBlockVideo videoBlock)
                {
                    var galleryItem = new GalleryVideo(ViewModel.ProtoService, videoBlock.Video, block.Caption.ToPlainText());
                    ViewModel.Gallery.Items.Add(galleryItem);

                    var message = GetMessage(new MessageVideo(videoBlock.Video, null, false));

                    var content = new VideoContent(message);
                    content.Tag = galleryItem;
                    content.HorizontalAlignment = HorizontalAlignment.Center;
                    content.ClearValue(MaxWidthProperty);
                    content.ClearValue(MaxHeightProperty);

                    if (videoBlock.Video.Thumbnail != null)
                    {
                        _filesMap[videoBlock.Video.Thumbnail.File.Id].Add(Tuple.Create(content as IContentWithFile, message));
                    }

                    _filesMap[videoBlock.Video.VideoValue.Id].Add(Tuple.Create(content as IContentWithFile, message));

                    items.Add(content);
                }
            }

            var flip = new FlipView();
            flip.ItemsSource = items;
            flip.MaxHeight = 420;

            element.Children.Add(flip);

            var caption = ProcessCaption(block.Caption);
            if (caption != null)
            {
                caption.Margin = new Thickness(12, 8, 0, 0);
                element.Children.Add(caption);
            }

            return element;
        }

        private FrameworkElement ProcessCollage(PageBlockCollage block)
        {
            var element = new StackPanel { Style = Resources["BlockCollageStyle"] as Style };

            var items = new List<ImageView>();
            foreach (var item in block.PageBlocks)
            {
                if (item is PageBlockPhoto photoBlock)
                {
                    //var galleryItem = new GalleryPhotoItem(photoBlock.Photo, photoBlock.Caption?.ToString());
                    //ViewModel.Gallery.Items.Add(galleryItem);

                    var child = new ImageView();
                    //child.Source = (ImageSource)DefaultPhotoConverter.Convert(photoBlock.Photo, true);
                    //child.DataContext = galleryItem;
                    child.Click += Image_Click;
                    child.Width = 72;
                    child.Height = 72;
                    child.Stretch = Stretch.UniformToFill;
                    child.Margin = new Thickness(0, 0, 4, 4);

                    items.Add(child);
                }
                else if (item is PageBlockVideo videoBlock)
                {
                    //var galleryItem = new GalleryDocumentItem(videoBlock.Video, videoBlock.Caption?.ToString());
                    //ViewModel.Gallery.Items.Add(galleryItem);

                    var child = new ImageView();
                    //child.Source = (ImageSource)DefaultPhotoConverter.Convert(videoBlock.Video, true);
                    //child.DataContext = galleryItem;
                    child.Click += Image_Click;
                    child.Width = 72;
                    child.Height = 72;
                    child.Stretch = Stretch.UniformToFill;
                    child.Margin = new Thickness(0, 0, 4, 4);

                    items.Add(child);
                }
            }

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

            for (int i = 0; i < items.Count; i++)
            {
                var y = i / 4;
                var x = i % 4;

                grid.Children.Add(items[i]);
                Grid.SetRow(items[i], y);
                Grid.SetColumn(items[i], x);

                if (x == 0)
                {
                    grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                }
            }

            element.Children.Add(grid);

            var caption = ProcessCaption(block.Caption);
            if (caption != null)
            {
                caption.Margin = new Thickness(12, 8, 0, 0);
                element.Children.Add(caption);
            }

            return element;
        }

        private FrameworkElement ProcessEmbedPost(PageBlockEmbeddedPost block)
        {
            var element = new StackPanel { Style = Resources["BlockEmbedPostStyle"] as Style };

            var header = new Grid();
            header.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            header.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
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
            textDate.Text = BindConvert.Current.DateTime(block.Date).ToString("dd MMMM yyyy");
            textDate.VerticalAlignment = VerticalAlignment.Top;
            textDate.Style = (Style)Resources["CaptionTextBlockStyle"];
            textDate.Foreground = (SolidColorBrush)Resources["SystemControlDisabledChromeDisabledLowBrush"];
            Grid.SetColumn(textDate, 1);
            Grid.SetRow(textDate, 1);

            header.Children.Add(textAuthor);
            header.Children.Add(textDate);

            element.Children.Add(header);

            PageBlock previousBlock = null;
            FrameworkElement previousElement = null;
            foreach (var subBlock in block.PageBlocks)
            {
                var subLayout = ProcessBlock(subBlock);
                var spacing = SpacingBetweenBlocks(previousBlock, block);

                if (subLayout != null)
                {
                    subLayout.Margin = new Thickness(_padding, spacing, _padding, 0);
                    element.Children.Add(subLayout);
                }

                previousBlock = block;
                previousElement = subLayout;
            }

            return element;
        }

        private FrameworkElement ProcessAnchor(PageBlockAnchor block)
        {
            var element = new Border();
            _anchors[block.Name] = element;

            return element;
        }

        private void ProcessRichText(RichText text, Span span, RichTextBlock textBlock)
        {
            int offset = 0;
            ProcessRichText(text, span, textBlock, TextEffects.None, ref offset);
        }

        private void ProcessRichText(RichText text, Span span, RichTextBlock textBlock, TextEffects effects, ref int offset)
        {
            switch (text)
            {
                case RichTextPlain plainText:
                    span.Inlines.Add(new Run { Text = plainText.Text });

                    if (effects.HasFlag(TextEffects.Marked))
                    {
                        var highlight = new TextHighlighter();
                        highlight.Background = new SolidColorBrush(Colors.PaleGoldenrod);
                        highlight.Ranges.Add(new TextRange { StartIndex = offset, Length = plainText.Text.Length });

                        textBlock.TextHighlighters.Add(highlight);
                    }

                    offset += plainText.Text.Length;
                    break;
                case RichTexts concatText:
                    foreach (var concat in concatText.Texts)
                    {
                        var concatRun = new Span();
                        span.Inlines.Add(concatRun);
                        ProcessRichText(concat, concatRun, textBlock, effects, ref offset);
                    }
                    break;
                case RichTextBold boldText:
                    span.FontWeight = FontWeights.SemiBold;
                    ProcessRichText(boldText.Text, span, textBlock, effects, ref offset);
                    break;
                case RichTextEmailAddress emailText:
                    ProcessRichText(emailText.Text, span, textBlock, effects, ref offset);
                    break;
                case RichTextFixed fixedText:
                    span.FontFamily = new FontFamily("Consolas");
                    ProcessRichText(fixedText.Text, span, textBlock, effects, ref offset);
                    break;
                case RichTextItalic italicText:
                    span.FontStyle |= FontStyle.Italic;
                    ProcessRichText(italicText.Text, span, textBlock, effects, ref offset);
                    break;
                case RichTextStrikethrough strikeText:
                    span.TextDecorations |= TextDecorations.Strikethrough;
                    ProcessRichText(strikeText.Text, span, textBlock, effects, ref offset);
                    break;
                case RichTextUnderline underlineText:
                    span.TextDecorations |= TextDecorations.Underline;
                    ProcessRichText(underlineText.Text, span, textBlock, effects, ref offset);
                    break;
                case RichTextAnchorLink anchorLinkText:
                    try
                    {
                        var hyperlink = new Hyperlink { UnderlineStyle = UnderlineStyle.None };
                        span.Inlines.Add(hyperlink);
                        hyperlink.Click += (s, args) => Hyperlink_Click(anchorLinkText);
                        MessageHelper.SetEntityData(hyperlink, anchorLinkText.Url);
                        MessageHelper.SetEntityAction(hyperlink, () => Hyperlink_Click(anchorLinkText));
                        ProcessRichText(anchorLinkText.Text, hyperlink, textBlock, effects, ref offset);
                    }
                    catch
                    {
                        ProcessRichText(anchorLinkText.Text, span, textBlock, effects, ref offset);
                        Debug.WriteLine("InstantPage: Probably nesting textUrl inside textUrl");
                    }
                    break;
                case RichTextUrl urlText:
                    try
                    {
                        var hyperlink = new Hyperlink { UnderlineStyle = UnderlineStyle.None };
                        span.Inlines.Add(hyperlink);
                        hyperlink.Click += (s, args) => Hyperlink_Click(urlText);
                        MessageHelper.SetEntityData(hyperlink, urlText.Url);
                        MessageHelper.SetEntityAction(hyperlink, () => Hyperlink_Click(urlText));
                        ProcessRichText(urlText.Text, hyperlink, textBlock, effects, ref offset);
                    }
                    catch
                    {
                        ProcessRichText(urlText.Text, span, textBlock, effects, ref offset);
                        Debug.WriteLine("InstantPage: Probably nesting textUrl inside textUrl");
                    }
                    break;
                case RichTextIcon icon:
                    var photo = new Image { Width = icon.Width, Height = icon.Height };

                    var file = icon.Document.DocumentValue;
                    if (file.Local.IsDownloadingCompleted)
                    {
                        photo.Source = new BitmapImage(new Uri("file:///" + file.Local.Path));
                    }
                    else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                    {
                        _iconsMap[file.Id].Add(photo);
                        ViewModel.ProtoService.DownloadFile(file.Id, 1);
                    }
                    var inline = new InlineUIContainer();
                    inline.Child = photo;
                    span.Inlines.Add(inline);
                    break;
                case RichTextMarked marked:
                    // ???
                    ProcessRichText(marked.Text, span, textBlock, effects | TextEffects.Marked, ref offset);
                    break;
                case RichTextPhoneNumber phoneNumber:
                    try
                    {
                        var hyperlink = new Hyperlink { UnderlineStyle = UnderlineStyle.None };
                        span.Inlines.Add(hyperlink);
                        hyperlink.Click += (s, args) => Hyperlink_Click(phoneNumber);
                        ProcessRichText(phoneNumber.Text, hyperlink, textBlock, effects, ref offset);
                    }
                    catch
                    {
                        ProcessRichText(phoneNumber.Text, span, textBlock, effects, ref offset);
                        Debug.WriteLine("InstantPage: Probably nesting textUrl inside textUrl");
                    }
                    break;
                case RichTextSubscript subscript:
                    Typography.SetVariants(span, FontVariants.Subscript);
                    ProcessRichText(subscript.Text, span, textBlock, effects, ref offset);
                    break;
                case RichTextSuperscript superscript:
                    Typography.SetVariants(span, FontVariants.Superscript);
                    ProcessRichText(superscript.Text, span, textBlock, effects, ref offset);
                    break;
            }
        }

        [Flags]
        private enum TextEffects
        {
            None,
            Link,
            Marked
        }

        private double SpacingBetweenBlocks(PageBlock upper, PageBlock lower)
        {
            if (lower is PageBlockCover || lower is PageBlockChatLink)
            {
                return 0;
            }

            if (upper is PageBlockDetails && lower is PageBlockDetails)
            {
                return 0;
            }

            return 12;

            if (lower is PageBlockCover || lower is PageBlockChatLink)
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
                if (upper is PageBlockTitle || upper is PageBlockAuthorDate)
                {
                    return 20; // 34;
                }
                else if (upper is PageBlockHeader || upper is PageBlockSubheader)
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
                if (upper is PageBlockTitle || upper is PageBlockAuthorDate)
                {
                    return 20; // 34;
                }
                else if (upper is PageBlockHeader || upper is PageBlockSubheader)
                {
                    return 19; // 31;
                }
                else if (upper is PageBlockParagraph || upper is PageBlockList)
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
            if (block is PageBlockCover || block is PageBlockPreformatted ||
                block is PageBlockPhoto || block is PageBlockVideo ||
                block is PageBlockSlideshow || block is PageBlockChatLink)
            {
                return 0.0;
            }

            return _padding;
        }

        private async void Image_Click(object sender, RoutedEventArgs e)
        {
            var image = sender as ImageView;
            var item = image.DataContext as GalleryContent;
            if (item != null)
            {
                ViewModel.Gallery.SelectedItem = item;
                ViewModel.Gallery.FirstItem = item;

                await GalleryView.GetForCurrentView().ShowAsync(ViewModel.Gallery, () => image);
            }
        }

        private async void Hyperlink_Click(RichTextAnchorLink anchorLinkText)
        {
            if (string.IsNullOrEmpty(anchorLinkText.Name))
            {
                ScrollingHost.GetScrollViewer().ChangeView(null, 0, null);
            }
            else if (_anchors.TryGetValue(anchorLinkText.Name, out Border anchor))
            {
                await ScrollingHost.ScrollToItem2(anchor, VerticalAlignment.Top, false);
            }
        }

        private async void Hyperlink_Click(RichTextUrl urlText)
        {
            ViewModel.IsLoading = true;

            var response = await ViewModel.ProtoService.SendAsync(new GetWebPageInstantView(urlText.Url, false));
            if (response is WebPageInstantView instantView)
            {
                ViewModel.IsLoading = false;
                ViewModel.NavigationService.NavigateToInstant(urlText.Url);
            }
            else if (MessageHelper.TryCreateUri(urlText.Url, out Uri uri))
            {
                ViewModel.IsLoading = false;

                if (MessageHelper.IsTelegramUrl(uri))
                {
                    MessageHelper.OpenTelegramUrl(ViewModel.ProtoService, ViewModel.NavigationService, uri);
                }
                else
                {
                    await Launcher.LaunchUriAsync(uri);
                }
            }
        }

        private async void Hyperlink_Click(RichTextPhoneNumber phoneNumber)
        {

        }

        private bool IsCurrentPage(string bae, string url, out string fragment)
        {
            if (Uri.TryCreate(bae, UriKind.Absolute, out Uri current) && Uri.TryCreate(url, UriKind.Absolute, out Uri result))
            {
                fragment = result.Fragment.Length > 0 ? result.Fragment?.Substring(1) : null;
                return fragment != null && Uri.Compare(current, result, UriComponents.Host | UriComponents.PathAndQuery, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0;
            }

            fragment = null;
            return false;
        }

        private async void OnWebViewNavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
        {
            try
            {
                var jss = _injectedJs;
                await sender.InvokeScriptAsync("eval", new[] { jss });
            }
            catch { }
        }

        #region Delegate

        public bool CanBeDownloaded(MessageViewModel message)
        {
            return !ViewModel.Settings.AutoDownload.Disabled;
        }

        public void DownloadFile(MessageViewModel message, File file)
        {
        }

        public void ReplyToMessage(MessageViewModel message)
        {
        }

        public void OpenReply(MessageViewModel message)
        {
        }

        public void OpenFile(File file)
        {
        }

        public void OpenWebPage(WebPage webPage)
        {
        }

        public void OpenSticker(Sticker sticker)
        {
        }

        public void OpenLocation(Location location, string title)
        {
        }

        public void OpenLiveLocation(MessageViewModel message)
        {

        }

        public void OpenInlineButton(MessageViewModel message, InlineKeyboardButton button)
        {
        }

        public async void OpenMedia(MessageViewModel message, FrameworkElement target)
        {
            var content = target.Tag as GalleryContent;
            if (content == null)
            {
                content = ViewModel.Gallery.Items.FirstOrDefault();
            }

            ViewModel.Gallery.SelectedItem = content;
            ViewModel.Gallery.FirstItem = content;

            await GalleryView.GetForCurrentView().ShowAsync(ViewModel.Gallery, () => target);
        }

        public void PlayMessage(MessageViewModel message)
        {
        }

        public void OpenUsername(string username)
        {
        }

        public void OpenHashtag(string hashtag)
        {
        }

        public void OpenBankCardNumber(string number)
        {
        }

        public void OpenUser(int userId)
        {
        }

        public void OpenChat(long chatId, bool profile = false)
        {
        }

        public void OpenChat(long chatId, long messageId)
        {
        }

        public void OpenViaBot(int viaBotUserId)
        {
        }

        public void OpenUrl(string url, bool untrust)
        {
        }

        public void SendBotCommand(string command)
        {
        }

        public bool IsAdmin(int userId)
        {
            return false;
        }

        public void Call(MessageViewModel message, bool video)
        {
            throw new NotImplementedException();
        }

        public void VotePoll(MessageViewModel message, IList<PollOption> options)
        {
            throw new NotImplementedException();
        }

        public void SelectMessage(MessageViewModel message)
        {
            throw new NotImplementedException();
        }

        public void DeselectMessage(MessageViewModel message)
        {
            throw new NotImplementedException();
        }

        public bool IsMessageSelected(long messageId)
        {
            throw new NotImplementedException();
        }

        public string GetAdminTitle(int userId)
        {
            throw new NotImplementedException();
        }

        public void OpenThread(MessageViewModel message)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
