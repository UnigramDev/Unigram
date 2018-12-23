using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Views;
using Unigram.Services;
using Unigram.ViewModels;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.Globalization.DateTimeFormatting;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;
using Windows.UI.Xaml.Media.Animation;
using Unigram.Controls.Views;
using LinqToVisualTree;
using Unigram.ViewModels.Users;
using Telegram.Td.Api;
using Unigram.Controls.Messages.Content;
using Unigram.Controls.Messages;
using Unigram.ViewModels.Dialogs;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Gallery;
using Unigram.Controls.Gallery;

namespace Unigram.Views
{
    public sealed partial class InstantPage : Page, IMessageDelegate, IHandle<UpdateFile>
    {
        public InstantViewModel ViewModel => DataContext as InstantViewModel;

        private readonly string _injectedJs;
        private ScrollViewer _scrollingHost;

        private FileContext<Tuple<IContentWithFile, MessageViewModel>> _filesMap = new FileContext<Tuple<IContentWithFile, MessageViewModel>>();

        public InstantPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<InstantViewModel>();

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
                    }

                    if (update.File.Local.IsDownloadingCompleted && !update.File.Remote.IsUploadingActive)
                    {
                        elements.Clear();
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

            var url = TLSerializationService.Current.Deserialize((string)e.Parameter) as string;
            if (url == null)
            {
                return;
            }

            ViewModel.IsLoading = true;

            var response = await ViewModel.ProtoService.SendAsync(new GetWebPageInstantView(url, true));
            if (response is WebPageInstantView instantView)
            {
                UpdateView(instantView);
                ViewModel.IsLoading = false;

                if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
                {
                    ViewModel.ShareLink = uri;
                    ViewModel.ShareTitle = url;

                    //if (uri.Fragment.Length > 0 && _anchors.TryGetValue(uri.Fragment.Substring(1), out Border anchor))
                    //{
                    //    await ScrollingHost.ScrollToItem(anchor, SnapPointsAlignment.Near, false);
                    //}
                }
            }

            //if (url.StartsWith("http") == false)
            //{
            //    url = "http://" + url;
            //}

            //if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            //{
            //    ViewModel.ShareLink = uri;
            //    ViewModel.ShareTitle = webpage.HasTitle ? webpage.Title : webpage.Url;
            //}

            //var webpageMedia = parameter as TLMessageMediaWebPage;
            //if (webpageMedia != null)
            //{
            //    parameter = webpageMedia.WebPage as TLWebPage;
            //}

            //var webpage = parameter as TLWebPage;
            //if (webpage != null && webpage.HasCachedPage)
            //{
            //    var url = webpage.Url;

            //    _webpageId = webpage.Id;

            //    var photos = new List<TLPhotoBase>(webpage.CachedPage.Photos);
            //    var documents = new List<TLDocumentBase>(webpage.CachedPage.Documents);

            //    if (webpage.HasPhoto)
            //    {
            //        photos.Insert(0, webpage.Photo);
            //    }

            //    var processed = 0;
            //    TLPageBlockBase previousBlock = null;
            //    FrameworkElement previousElement = null;
            //    foreach (var block in webpage.CachedPage.Blocks)
            //    {
            //        var element = ProcessBlock(webpage.CachedPage, block, photos, documents);
            //        var spacing = SpacingBetweenBlocks(previousBlock, block);
            //        var padding = PaddingForBlock(block);

            //        if (element != null)
            //        {
            //            if (block is TLPageBlockChannel && previousBlock is TLPageBlockCover)
            //            {
            //                if (previousElement is StackPanel stack && element is Button)
            //                {
            //                    element.Style = Resources["CoverChannelBlockStyle"] as Style;
            //                    element.Margin = new Thickness(padding, -40, padding, 0);
            //                    stack.Children.Insert(1, element);
            //                }
            //            }
            //            else
            //            {
            //                element.Margin = new Thickness(padding, spacing, padding, 0);
            //                ScrollingHost.Items.Add(element);
            //            }
            //        }

            //        previousBlock = block;
            //        previousElement = element;
            //        processed++;
            //    }

            //    var part = webpage.CachedPage as TLPagePart;
            //    if (part != null)
            //    {
            //        var response = await MTProtoService.Current.GetWebPageAsync(webpage.Url, webpage.Hash);
            //        if (response.IsSucceeded)
            //        {
            //            var newpage = response.Result as TLWebPage;
            //            if (newpage != null && newpage.HasCachedPage)
            //            {
            //                photos = new List<TLPhotoBase>(newpage.CachedPage.Photos);
            //                documents = new List<TLDocumentBase>(newpage.CachedPage.Documents);

            //                if (webpage.HasPhoto)
            //                {
            //                    photos.Insert(0, webpage.Photo);
            //                }

            //                for (int i = processed; i < newpage.CachedPage.Blocks.Count; i++)
            //                {
            //                    var block = newpage.CachedPage.Blocks[i];
            //                    var element = ProcessBlock(newpage.CachedPage, block, photos, documents);
            //                    var spacing = SpacingBetweenBlocks(previousBlock, block);
            //                    var padding = PaddingForBlock(block);

            //                    if (element != null)
            //                    {
            //                        element.Margin = new Thickness(padding, spacing, padding, 0);
            //                        ScrollingHost.Items.Add(element);
            //                    }

            //                    previousBlock = newpage.CachedPage.Blocks[i];
            //                    previousElement = element;
            //                }
            //            }
            //        }
            //    }
            //}

            base.OnNavigatedTo(e);
        }

        private void UpdateView(WebPageInstantView instantView)
        {
            var processed = 0;
            PageBlock previousBlock = null;
            FrameworkElement previousElement = null;
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

                previousBlock = block;
                previousElement = element;
                processed++;
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
            }

            return null;
        }

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
                ProcessRichText(block.Author, span);
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
                case PageBlockPhoto photo:
                    text = photo.Caption;
                    break;
                case PageBlockVideo video:
                    text = video.Caption;
                    break;
                case PageBlockSlideshow slideshow:
                    text = slideshow.Caption;
                    break;
                case PageBlockEmbedded embed:
                    text = embed.Caption;
                    break;
                case PageBlockEmbeddedPost embedPost:
                    text = embedPost.Caption;
                    break;
                case PageBlockBlockQuote blockquote:
                    text = caption ? blockquote.Caption : blockquote.Text;
                    break;
                case PageBlockPullQuote pullquote:
                    text = caption ? pullquote.Caption : pullquote.Text;
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

            //textBlock.Margin = new Thickness(12, 0, 12, 12);
            ProcessRichText(text, span);

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
            }

            return textBlock;
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
            var textBlock = new RichTextBlock();
            textBlock.Style = Resources["BlockBodyTextBlockStyle"] as Style;
            textBlock.TextWrapping = TextWrapping.Wrap;

            for (int i = 0; i < block.Items.Count; i++)
            {
                var text = block.Items[i];
                var par = new Paragraph();
                par.TextIndent = -20;
                par.Margin = new Thickness(20, 0, 0, 0);

                var span = new Span();
                par.Inlines.Add(new Run { Text = block.IsOrdered ? (i + 1) + ".\t" : "•\t" });
                par.Inlines.Add(span);
                ProcessRichText(text, span);
                textBlock.Blocks.Add(par);
            }

            return textBlock;
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

            var caption = ProcessText(block, true);
            if (caption != null)
            {
                caption.Margin = new Thickness(0, 8, 0, 0);
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
                _filesMap[block.Video.Thumbnail.Photo.Id].Add(Tuple.Create(content as IContentWithFile, message));
            }

            _filesMap[block.Video.VideoValue.Id].Add(Tuple.Create(content as IContentWithFile, message));

            element.Children.Add(content);

            var caption = ProcessText(block, true);
            if (caption != null)
            {
                caption.Margin = new Thickness(0, 8, 0, 0);
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

            if (block.Animation.Thumbnail != null)
            {
                _filesMap[block.Animation.Thumbnail.Photo.Id].Add(Tuple.Create(content as IContentWithFile, message));
            }

            _filesMap[block.Animation.AnimationValue.Id].Add(Tuple.Create(content as IContentWithFile, message));

            element.Children.Add(content);

            var caption = ProcessText(block, true);
            if (caption != null)
            {
                caption.Margin = new Thickness(0, 8, 0, 0);
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

            var caption = ProcessText(block, true);
            if (caption != null)
            {
                caption.Margin = new Thickness(0, _padding, 0, 0);
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
                        _filesMap[videoBlock.Video.Thumbnail.Photo.Id].Add(Tuple.Create(content as IContentWithFile, message));
                    }

                    _filesMap[videoBlock.Video.VideoValue.Id].Add(Tuple.Create(content as IContentWithFile, message));

                    items.Add(content);
                }
            }

            var flip = new FlipView();
            flip.ItemsSource = items;
            flip.MaxHeight = 420;

            element.Children.Add(flip);

            var caption = ProcessText(block, true);
            if (caption != null)
            {
                caption.Margin = new Thickness(0, _padding, 0, 0);
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
                    child.Source = (ImageSource)DefaultPhotoConverter.Convert(photoBlock.Photo, true);
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
                    child.Source = (ImageSource)DefaultPhotoConverter.Convert(videoBlock.Video, true);
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

            var caption = ProcessText(block, true);
            if (caption != null)
            {
                caption.Margin = new Thickness(0, _padding, 0, 0);
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
                ellipse.Fill = new ImageBrush { ImageSource = (ImageSource)DefaultPhotoConverter.Convert(photo, true), Stretch = Stretch.UniformToFill, AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center };
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

        private void ProcessRichText(RichText text, Span span)
        {
            switch (text)
            {
                case RichTextPlain plainText:
                    if (GetIsStrikethrough(span))
                    {
                        span.Inlines.Add(new Run { Text = StrikethroughFallback(plainText.Text) });
                    }
                    else
                    {
                        span.Inlines.Add(new Run { Text = plainText.Text });
                    }
                    break;
                case RichTexts concatText:
                    foreach (var concat in concatText.Texts)
                    {
                        var concatRun = new Span();
                        span.Inlines.Add(concatRun);
                        ProcessRichText(concat, concatRun);
                    }
                    break;
                case RichTextBold boldText:
                    span.FontWeight = FontWeights.SemiBold;
                    ProcessRichText(boldText.Text, span);
                    break;
                case RichTextEmailAddress emailText:
                    ProcessRichText(emailText.Text, span);
                    break;
                case RichTextFixed fixedText:
                    span.FontFamily = new FontFamily("Consolas");
                    ProcessRichText(fixedText.Text, span);
                    break;
                case RichTextItalic italicText:
                    span.FontStyle |= FontStyle.Italic;
                    ProcessRichText(italicText.Text, span);
                    break;
                case RichTextStrikethrough strikeText:
                    if (ApiInformation.IsPropertyPresent("Windows.UI.Xaml.Documents.TextElement", "TextDecorations"))
                    {
                        span.TextDecorations |= TextDecorations.Strikethrough;
                        ProcessRichText(strikeText.Text, span);
                    }
                    else
                    {
                        SetIsStrikethrough(span, true);
                        ProcessRichText(strikeText.Text, span);
                    }
                    break;
                case RichTextUnderline underlineText:
                    if (ApiInformation.IsPropertyPresent("Windows.UI.Xaml.Documents.TextElement", "TextDecorations"))
                    {
                        span.TextDecorations |= TextDecorations.Underline;
                        ProcessRichText(underlineText.Text, span);
                    }
                    else
                    {
                        var underline = new Underline();
                        span.Inlines.Add(underline);
                        ProcessRichText(underlineText.Text, underline);
                    }
                    break;
                case RichTextUrl urlText:
                    try
                    {
                        var hyperlink = new Hyperlink { UnderlineStyle = UnderlineStyle.None };
                        span.Inlines.Add(hyperlink);
                        hyperlink.Click += (s, args) => Hyperlink_Click(urlText);
                        ProcessRichText(urlText.Text, hyperlink);
                    }
                    catch
                    {
                        ProcessRichText(urlText.Text, span);
                        Debug.WriteLine("InstantPage: Probably nesting textUrl inside textUrl");
                    }
                    break;
            }
        }

        private double SpacingBetweenBlocks(PageBlock upper, PageBlock lower)
        {
            if (lower is PageBlockCover || lower is PageBlockChatLink)
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

        private async void Hyperlink_Click(RichTextUrl urlText)
        {
            if (IsCurrentPage(ViewModel.ShareLink, urlText.Url, out string fragment))
            {
                if (_anchors.TryGetValue(fragment, out Border anchor))
                {
                    await ScrollingHost.ScrollToItem(anchor, VerticalAlignment.Top, false);
                }
            }
            else
            {
                ViewModel.IsLoading = true;

                var response = await ViewModel.ProtoService.SendAsync(new GetWebPageInstantView(urlText.Url, false));
                if (response is WebPageInstantView instantView)
                {
                    ViewModel.IsLoading = false;
                    ViewModel.NavigationService.Navigate(typeof(InstantPage), urlText.Url);
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
        }

        private bool IsCurrentPage(Uri current, string url, out string fragment)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out Uri result))
            {
                fragment = result.Fragment.Length > 0 ? result.Fragment?.Substring(1) : null;
                return Uri.Compare(current, result, UriComponents.Host | UriComponents.PathAndQuery, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0;
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

        #region Strikethrough

        private string StrikethroughFallback(string text)
        {
            var sb = new StringBuilder(text.Length * 2);
            foreach (var ch in text)
            {
                sb.Append((char)0x0336);
                sb.Append(ch);
            }

            return sb.ToString();
        }

        public static bool GetIsStrikethrough(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsStrikethroughProperty);
        }

        public static void SetIsStrikethrough(DependencyObject obj, bool value)
        {
            obj.SetValue(IsStrikethroughProperty, value);
        }

        public static readonly DependencyProperty IsStrikethroughProperty =
            DependencyProperty.RegisterAttached("IsStrikethrough", typeof(bool), typeof(InstantPage), new PropertyMetadata(false));

        #endregion

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

        public void OpenUser(int userId)
        {
        }

        public void OpenChat(long chatId)
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

        public void Call(MessageViewModel message)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
