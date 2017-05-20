using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.TL;
using Telegram.Api.TL.Methods.Messages;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Views;
using Unigram.Core.Services;
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
using Telegram.Api.Services.Cache;

namespace Unigram.Views
{
    public sealed partial class InstantPage : Page
    {
        public InstantViewModel ViewModel => DataContext as InstantViewModel;

        private readonly string _injectedJs;

        public InstantPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<InstantViewModel>();

            var jsPath = System.IO.Path.Combine(Package.Current.InstalledLocation.Path, "Assets", "Webviews", "injected.js");
            _injectedJs = File.ReadAllText(jsPath);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            ScrollingHost.Items.Clear();
            ViewModel.Gallery.Items.Clear();
            ViewModel.Gallery.TotalItems = 0;
            ViewModel.Gallery.SelectedItem = null;
            _anchors.Clear();

            var parameter = TLSerializationService.Current.Deserialize((string)e.Parameter);

            var webpageMedia = parameter as TLMessageMediaWebPage;
            if (webpageMedia != null)
            {
                parameter = webpageMedia.WebPage as TLWebPage;
            }

            var webpage = parameter as TLWebPage;
            if (webpage != null && webpage.HasCachedPage)
            {
                _webpageId = webpage.Id;

                var photos = new List<TLPhotoBase>(webpage.CachedPage.Photos);
                var videos = new List<TLDocumentBase>(webpage.CachedPage.Videos);

                if (webpage.HasPhoto)
                {
                    photos.Insert(0, webpage.Photo);
                }

                var processed = 0;
                TLPageBlockBase previousBlock = null;
                FrameworkElement previousElement = null;
                foreach (var block in webpage.CachedPage.Blocks)
                {
                    var element = ProcessBlock(webpage.CachedPage, block, photos, videos);
                    var spacing = SpacingBetweenBlocks(previousBlock, block);
                    var padding = PaddingForBlock(block);

                    if (element != null)
                    {
                        element.Margin = new Thickness(padding, spacing, padding, 0);
                        ScrollingHost.Items.Add(element);
                    }

                    previousBlock = block;
                    previousElement = element;
                    processed++;
                }

                var part = webpage.CachedPage as TLPagePart;
                if (part != null)
                {
                    var response = await MTProtoService.Current.GetWebPageAsync(webpage.Url, webpage.Hash);
                    if (response.IsSucceeded)
                    {
                        var newpage = response.Result as TLWebPage;
                        if (newpage != null && newpage.HasCachedPage)
                        {
                            photos = new List<TLPhotoBase>(newpage.CachedPage.Photos);
                            videos = new List<TLDocumentBase>(newpage.CachedPage.Videos);

                            if (webpage.HasPhoto)
                            {
                                photos.Insert(0, webpage.Photo);
                            }

                            for (int i = processed; i < newpage.CachedPage.Blocks.Count; i++)
                            {
                                var block = newpage.CachedPage.Blocks[i];
                                var element = ProcessBlock(newpage.CachedPage, block, photos, videos);
                                var spacing = SpacingBetweenBlocks(previousBlock, block);
                                var padding = PaddingForBlock(block);

                                if (element != null)
                                {
                                    element.Margin = new Thickness(padding, spacing, padding, 0);
                                    ScrollingHost.Items.Add(element);
                                }

                                previousBlock = newpage.CachedPage.Blocks[i];
                                previousElement = element;
                            }
                        }
                    }
                }
            }

            base.OnNavigatedTo(e);
        }

        private long _webpageId;

        //private Stack<Panel> _containers = new Stack<Panel>();
        private double _padding = 12;

        private Dictionary<string, Border> _anchors = new Dictionary<string, Border>();

        private FrameworkElement ProcessBlock(TLPageBase page, TLPageBlockBase block, IList<TLPhotoBase> photos, IList<TLDocumentBase> videos)
        {
            switch (block)
            {
                case TLPageBlockCover cover:
                    return ProcessCover(page, cover, photos, videos);
                case TLPageBlockAuthorDate authorDate:
                    return ProcessAuthorDate(page, authorDate, photos, videos);
                case TLPageBlockHeader header:
                case TLPageBlockSubheader subheader:
                case TLPageBlockTitle title:
                case TLPageBlockSubtitle subtitle:
                case TLPageBlockFooter footer:
                case TLPageBlockParagraph paragraph:
                    return ProcessText(page, block, photos, videos, false);
                case TLPageBlockBlockquote blockquote:
                    return ProcessBlockquote(page, blockquote, photos, videos);
                case TLPageBlockDivider divider:
                    return ProcessDivider(page, divider, photos, videos);
                case TLPageBlockPhoto photo:
                    return ProcessPhoto(page, photo, photos, videos);
                case TLPageBlockList list:
                    return ProcessList(page, list, photos, videos);
                case TLPageBlockVideo video:
                    return ProcessVideo(page, video, photos, videos);
                case TLPageBlockEmbedPost embedPost:
                    return ProcessEmbedPost(page, embedPost, photos, videos);
                case TLPageBlockSlideshow slideshow:
                    return ProcessSlideshow(page, slideshow, photos, videos);
                case TLPageBlockCollage collage:
                    return ProcessCollage(page, collage, photos, videos);
                case TLPageBlockEmbed embed:
                    return ProcessEmbed(page, embed, photos, videos);
                case TLPageBlockPullquote pullquote:
                    return ProcessPullquote(page, pullquote, photos, videos);
                case TLPageBlockAnchor anchor:
                    return ProcessAnchor(page, anchor, photos, videos);
                case TLPageBlockPreformatted preformatted:
                    return ProcessPreformatted(page, preformatted, photos, videos);
                case TLPageBlockChannel channel:
                    return ProcessChannel(page, channel, photos, videos);
                case TLPageBlockUnsupported unsupported:
                    Debug.WriteLine("Unsupported block type: " + block.GetType());
                    break;
            }

            return null;
        }

        private FrameworkElement ProcessCover(TLPageBase page, TLPageBlockCover block, IList<TLPhotoBase> photos, IList<TLDocumentBase> videos)
        {
            return ProcessBlock(page, block.Cover, photos, videos);
        }

        private FrameworkElement ProcessChannel(TLPageBase page, TLPageBlockChannel channel, IList<TLPhotoBase> photos, IList<TLDocumentBase> videos)
        {
            var chat = channel.Channel as TLChannel;
            if (chat.IsMin)
            {
                chat = InMemoryCacheService.Current.GetChat(chat.Id) as TLChannel ?? channel.Channel as TLChannel;
            }

            var button = new Button
            {
                Style = Resources["ChannelBlockStyle"] as Style,
                Content = chat
            };

            if (chat.IsMin && chat.HasUsername)
            {
                MTProtoService.Current.ResolveUsernameAsync(chat.Username,
                    result =>
                    {
                        Execute.BeginOnUIThread(() => button.Content = result.Chats.FirstOrDefault());
                    });
            }

            return button;
        }

        private FrameworkElement ProcessAuthorDate(TLPageBase page, TLPageBlockAuthorDate block, IList<TLPhotoBase> photos, IList<TLDocumentBase> videos)
        {
            var textBlock = new TextBlock { Style = Resources["AuthorDateBlockStyle"] as Style };
            textBlock.FontSize = 15;

            if (block.Author.TypeId != TLType.TextEmpty)
            {
                var span = new Span();
                textBlock.Inlines.Add(new Run { Text = "By " });
                textBlock.Inlines.Add(span);
                ProcessRichText(block.Author, span);

                textBlock.Inlines.Add(new Run { Text = " — " });
            }

            //textBlock.Inlines.Add(new Run { Text = DateTimeFormatter.LongDate.Format(BindConvert.Current.DateTime(block.PublishedDate)) });
            textBlock.Inlines.Add(new Run { Text = BindConvert.Current.DateTime(block.PublishedDate).ToString("dd MMMM yyyy") });
            return textBlock;
        }

        private FrameworkElement ProcessText(TLPageBase page, TLPageBlockBase block, IList<TLPhotoBase> photos, IList<TLDocumentBase> videos, bool caption)
        {
            TLRichTextBase text = null;
            switch (block)
            {
                case TLPageBlockTitle title:
                    text = title.Text;
                    break;
                case TLPageBlockSubtitle subtitle:
                    text = subtitle.Text;
                    break;
                case TLPageBlockHeader header:
                    text = header.Text;
                    break;
                case TLPageBlockSubheader subheader:
                    text = subheader.Text;
                    break;
                case TLPageBlockFooter footer:
                    text = footer.Text;
                    break;
                case TLPageBlockParagraph paragraph:
                    text = paragraph.Text;
                    break;
                case TLPageBlockPreformatted preformatted:
                    text = preformatted.Text;
                    break;
                case TLPageBlockPhoto photo:
                    text = photo.Caption;
                    break;
                case TLPageBlockVideo video:
                    text = video.Caption;
                    break;
                case TLPageBlockSlideshow slideshow:
                    text = slideshow.Caption;
                    break;
                case TLPageBlockEmbed embed:
                    text = embed.Caption;
                    break;
                case TLPageBlockEmbedPost embedPost:
                    text = embedPost.Caption;
                    break;
                case TLPageBlockBlockquote blockquote:
                    text = caption ? blockquote.Caption : blockquote.Text;
                    break;
                case TLPageBlockPullquote pullquote:
                    text = caption ? pullquote.Caption : pullquote.Text;
                    break;
            }

            if (text != null && text.TypeId != TLType.TextEmpty)
            {
                var textBlock = new TextBlock();
                var span = new Span();
                textBlock.Inlines.Add(span);
                textBlock.TextWrapping = TextWrapping.Wrap;
                //textBlock.Margin = new Thickness(12, 0, 12, 12);
                ProcessRichText(text, span);

                switch (block.TypeId)
                {
                    case TLType.PageBlockTitle:
                        textBlock.FontSize = 28;
                        textBlock.FontFamily = new FontFamily("Times New Roman");
                        //textBlock.TextLineBounds = TextLineBounds.TrimToBaseline;
                        break;
                    case TLType.PageBlockSubtitle:
                        textBlock.FontSize = 17;
                        //textBlock.FontFamily = new FontFamily("Times New Roman");
                        //textBlock.TextLineBounds = TextLineBounds.TrimToBaseline;
                        break;
                    case TLType.PageBlockHeader:
                        textBlock.FontSize = 24;
                        textBlock.FontFamily = new FontFamily("Times New Roman");
                        //textBlock.TextLineBounds = TextLineBounds.TrimToBaseline;
                        break;
                    case TLType.PageBlockSubheader:
                        textBlock.FontSize = 19;
                        textBlock.FontFamily = new FontFamily("Times New Roman");
                        //textBlock.TextLineBounds = TextLineBounds.TrimToBaseline;
                        break;
                    case TLType.PageBlockParagraph:
                        textBlock.FontSize = 17;
                        break;
                    case TLType.PageBlockPreformatted:
                        textBlock.FontSize = 16;
                        break;
                    case TLType.PageBlockFooter:
                        textBlock.FontSize = 15;
                        textBlock.Foreground = (SolidColorBrush)Resources["SystemControlDisabledChromeDisabledLowBrush"];
                        textBlock.TextAlignment = TextAlignment.Center;
                        break;
                    case TLType.PageBlockPhoto:
                    case TLType.PageBlockVideo:
                    case TLType.PageBlockSlideshow:
                    case TLType.PageBlockEmbed:
                    case TLType.PageBlockEmbedPost:
                        textBlock.FontSize = 15;
                        textBlock.Foreground = (SolidColorBrush)Resources["SystemControlDisabledChromeDisabledLowBrush"];
                        textBlock.TextAlignment = TextAlignment.Center;
                        break;
                    case TLType.PageBlockBlockquote:
                        textBlock.FontSize = caption ? 15 : 17;
                        if (caption)
                        {
                            textBlock.Foreground = (SolidColorBrush)Resources["SystemControlDisabledChromeDisabledLowBrush"];
                            textBlock.TextAlignment = TextAlignment.Center;
                        }
                        break;
                    case TLType.PageBlockPullquote:
                        var pullquoteBlock = block as TLPageBlockPullquote;
                        textBlock.FontSize = caption ? 15 : 17;
                        if (caption)
                        {
                            textBlock.Foreground = (SolidColorBrush)Resources["SystemControlDisabledChromeDisabledLowBrush"];
                        }
                        else
                        {
                            textBlock.FontFamily = new FontFamily("Times New Roman");
                            //textBlock.TextLineBounds = TextLineBounds.TrimToBaseline;
                            textBlock.TextAlignment = TextAlignment.Center;
                        }
                        break;
                }

                return textBlock;
            }

            return null;
        }

        private FrameworkElement ProcessPreformatted(TLPageBase page, TLPageBlockPreformatted block, IList<TLPhotoBase> photos, IList<TLDocumentBase> videos)
        {
            var element = new StackPanel { Style = Resources["BlockPreformattedStyle"] as Style };

            var text = ProcessText(page, block, photos, videos, false);
            if (text != null) element.Children.Add(text);

            return element;
        }

        private FrameworkElement ProcessDivider(TLPageBase page, TLPageBlockDivider block, IList<TLPhotoBase> photos, IList<TLDocumentBase> videos)
        {
            var element = new Rectangle { Style = Resources["BlockDividerStyle"] as Style };
            return element;
        }

        private FrameworkElement ProcessList(TLPageBase page, TLPageBlockList block, IList<TLPhotoBase> photos, IList<TLDocumentBase> videos)
        {
            var textBlock = new RichTextBlock();
            textBlock.FontSize = 17;
            textBlock.TextWrapping = TextWrapping.Wrap;
            textBlock.IsTextSelectionEnabled = false;

            for (int i = 0; i < block.Items.Count; i++)
            {
                var text = block.Items[i];
                var par = new Paragraph();
                par.TextIndent = -24;
                par.Margin = new Thickness(24, 0, 0, 0);

                var span = new Span();
                par.Inlines.Add(new Run { Text = block.Ordered ? (i + 1) + ".\t" : "•\t" });
                par.Inlines.Add(span);
                ProcessRichText(text, span);
                textBlock.Blocks.Add(par);
            }

            return textBlock;
        }

        private FrameworkElement ProcessBlockquote(TLPageBase page, TLPageBlockBlockquote block, IList<TLPhotoBase> photos, IList<TLDocumentBase> videos)
        {
            var element = new StackPanel { Style = Resources["BlockBlockquoteStyle"] as Style };

            var text = ProcessText(page, block, photos, videos, false);
            if (text != null) element.Children.Add(text);

            var caption = ProcessText(page, block, photos, videos, true);
            if (caption != null) element.Children.Add(caption);

            return element;
        }

        private FrameworkElement ProcessPullquote(TLPageBase page, TLPageBlockPullquote block, IList<TLPhotoBase> photos, IList<TLDocumentBase> videos)
        {
            var element = new StackPanel { Style = Resources["BlockPullquoteStyle"] as Style };

            var text = ProcessText(page, block, photos, videos, false);
            if (text != null) element.Children.Add(text);

            var caption = ProcessText(page, block, photos, videos, true);
            if (caption != null) element.Children.Add(caption);

            return element;
        }

        private FrameworkElement ProcessPhoto(TLPageBase page, TLPageBlockPhoto block, IList<TLPhotoBase> photos, IList<TLDocumentBase> videos)
        {
            var photo = photos.FirstOrDefault(x => x.Id == block.PhotoId);
            if (photo != null)
            {
                var element = new StackPanel { Style = Resources["BlockPhotoStyle"] as Style };

                var galleryItem = new GalleryPhotoItem(photo as TLPhoto, block.Caption?.ToString());
                var child = new ImageView();
                child.Source = (ImageSource)DefaultPhotoConverter.Convert(photo, true);
                child.Constraint = photo;
                child.DataContext = galleryItem;
                child.Click += Image_Click;
                child.HorizontalAlignment = HorizontalAlignment.Center;

                ViewModel.Gallery.Items.Add(galleryItem);

                element.Children.Add(child);

                var caption = ProcessText(page, block, photos, videos, true);
                if (caption != null)
                {
                    caption.Margin = new Thickness(0, 12, 0, 0);
                    element.Children.Add(caption);
                }

                return element;
            }

            return null;
        }

        private FrameworkElement ProcessVideo(TLPageBase page, TLPageBlockVideo block, IList<TLPhotoBase> photos, IList<TLDocumentBase> videos)
        {
            var video = videos.FirstOrDefault(x => x.Id == block.VideoId);
            if (video != null)
            {
                var element = new StackPanel { Style = Resources["BlockVideoStyle"] as Style };

                var galleryItem = new GalleryDocumentItem(video as TLDocument, block.Caption?.ToString());
                var child = new ImageView();
                child.Source = (ImageSource)DefaultPhotoConverter.Convert(video, true);
                child.Constraint = video;
                child.DataContext = galleryItem;
                child.Click += Image_Click;
                child.HorizontalAlignment = HorizontalAlignment.Center;

                ViewModel.Gallery.Items.Add(galleryItem);

                element.Children.Add(child);

                var caption = ProcessText(page, block, photos, videos, true);
                if (caption != null)
                {
                    caption.Margin = new Thickness(0, _padding, 0, 0);
                    element.Children.Add(caption);
                }

                return element;
            }

            return null;
        }

        private FrameworkElement ProcessEmbed(TLPageBase page, TLPageBlockEmbed block, IList<TLPhotoBase> photos, IList<TLDocumentBase> videos)
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
            if (block.HasHtml)
            {
                var view = new WebView();
                view.NavigationCompleted += OnWebViewNavigationCompleted;
                view.NavigateToString(block.Html.Replace("src=\"//", "src=\"https://"));

                var ratio = new RatioControl();
                ratio.MaxWidth = block.W;
                ratio.MaxHeight = block.H;
                ratio.Content = view;
                ratio.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                ratio.VerticalContentAlignment = VerticalAlignment.Stretch;
                child = ratio;
            }
            else if (block.HasUrl)
            {
                var view = new WebView();
                view.NavigationCompleted += OnWebViewNavigationCompleted;
                view.Navigate(new Uri(block.Url));

                var ratio = new RatioControl();
                ratio.MaxWidth = block.W;
                ratio.MaxHeight = block.H;
                ratio.Content = view;
                ratio.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                ratio.VerticalContentAlignment = VerticalAlignment.Stretch;
                child = ratio;
            }

            element.Children.Add(child);

            var caption = ProcessText(page, block, photos, videos, true);
            if (caption != null)
            {
                caption.Margin = new Thickness(0, _padding, 0, 0);
                element.Children.Add(caption);
            }

            return element;
        }

        private FrameworkElement ProcessSlideshow(TLPageBase page, TLPageBlockSlideshow block, IList<TLPhotoBase> photos, IList<TLDocumentBase> videos)
        {
            var element = new StackPanel { Style = Resources["BlockSlideshowStyle"] as Style };

            var items = new List<ImageView>();
            foreach (var item in block.Items)
            {
                if (item is TLPageBlockPhoto photoBlock)
                {
                    var photo = photos.FirstOrDefault(x => x.Id == photoBlock.PhotoId);
                    if (photo != null)
                    {
                        var image = new ImageView();
                        image.Source = (ImageSource)DefaultPhotoConverter.Convert(photo, true);
                        image.Constraint = photo;

                        items.Add(image);
                    }
                }
                else if (item is TLPageBlockVideo videoBlock)
                {
                    var video = videos.FirstOrDefault(x => x.Id == videoBlock.VideoId);
                    if (video != null)
                    {
                        var child = new ImageView();
                        child.Source = (ImageSource)DefaultPhotoConverter.Convert(video, true);
                        child.Constraint = video;

                        items.Add(child);
                    }
                }
            }

            var flip = new FlipView();
            flip.ItemsSource = items;

            element.Children.Add(flip);

            var caption = ProcessText(page, block, photos, videos, true);
            if (caption != null)
            {
                caption.Margin = new Thickness(0, _padding, 0, 0);
                element.Children.Add(caption);
            }

            return element;
        }

        private FrameworkElement ProcessCollage(TLPageBase page, TLPageBlockCollage block, IList<TLPhotoBase> photos, IList<TLDocumentBase> videos)
        {
            var element = new StackPanel { Style = Resources["BlockCollageStyle"] as Style };

            var items = new List<Image>();
            foreach (var item in block.Items)
            {
                if (item is TLPageBlockPhoto photoBlock)
                {
                    var photo = photos.FirstOrDefault(x => x.Id == photoBlock.PhotoId);
                    if (photo != null)
                    {
                        var child = new Image();
                        child.Source = (ImageSource)DefaultPhotoConverter.Convert(photo, true);
                        child.Width = 72;
                        child.Height = 72;
                        child.Stretch = Stretch.UniformToFill;
                        child.Margin = new Thickness(0, 0, 4, 4);

                        items.Add(child);
                    }
                }
                else if (item is TLPageBlockVideo videoBlock)
                {
                    var video = videos.FirstOrDefault(x => x.Id == videoBlock.VideoId);
                    if (video != null)
                    {
                        var child = new Image();
                        child.Source = (ImageSource)DefaultPhotoConverter.Convert(video, true);
                        child.Width = 72;
                        child.Height = 72;
                        child.Stretch = Stretch.UniformToFill;
                        child.Margin = new Thickness(0, 0, 4, 4);

                        items.Add(child);
                    }
                }
            }

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

            for (int i = 0; i < items.Count; i++)
            {
                var y = i / 3;
                var x = i % 3;

                grid.Children.Add(items[i]);
                Grid.SetRow(items[i], y);
                Grid.SetColumn(items[i], x);

                if (x == 0)
                {
                    grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                }
            }

            element.Children.Add(grid);

            var caption = ProcessText(page, block, photos, videos, true);
            if (caption != null)
            {
                caption.Margin = new Thickness(0, _padding, 0, 0);
                element.Children.Add(caption);
            }

            return element;
        }

        private FrameworkElement ProcessEmbedPost(TLPageBase page, TLPageBlockEmbedPost block, IList<TLPhotoBase> photos, IList<TLDocumentBase> videos)
        {
            var element = new StackPanel { Style = Resources["BlockEmbedPostStyle"] as Style };

            var header = new Grid();
            header.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            header.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.Margin = new Thickness(_padding, 0, 0, _padding);

            var photo = photos.FirstOrDefault(x => x.Id == block.AuthorPhotoId);
            var ellipse = new Ellipse();
            ellipse.Width = 36;
            ellipse.Height = 36;
            ellipse.Margin = new Thickness(0, 0, _padding, 0);
            ellipse.Fill = new ImageBrush { ImageSource = (ImageSource)DefaultPhotoConverter.Convert(photo, true), Stretch = Stretch.UniformToFill, AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center };
            Grid.SetRowSpan(ellipse, 2);

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

            header.Children.Add(ellipse);
            header.Children.Add(textAuthor);
            header.Children.Add(textDate);

            element.Children.Add(header);

            TLPageBlockBase previousBlock = null;
            FrameworkElement previousElement = null;
            foreach (var subBlock in block.Blocks)
            {
                var subLayout = ProcessBlock(page, subBlock, photos, videos);
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

        private FrameworkElement ProcessAnchor(TLPageBase page, TLPageBlockAnchor block, IList<TLPhotoBase> photos, IList<TLDocumentBase> videos)
        {
            var element = new Border();
            _anchors[block.Name] = element;

            return element;
        }

        private void ProcessRichText(TLRichTextBase text, Span span)
        {
            switch (text)
            {
                case TLTextPlain plainText:
                    if (GetIsStrikethrough(span))
                    {
                        span.Inlines.Add(new Run { Text = StrikethroughFallback(plainText.Text) });
                    }
                    else
                    {
                        span.Inlines.Add(new Run { Text = plainText.Text });
                    }
                    break;
                case TLTextConcat concatText:
                    foreach (var concat in concatText.Texts)
                    {
                        var concatRun = new Span();
                        span.Inlines.Add(concatRun);
                        ProcessRichText(concat, concatRun);
                    }
                    break;
                case TLTextBold boldText:
                    span.FontWeight = FontWeights.SemiBold;
                    ProcessRichText(boldText.Text, span);
                    break;
                case TLTextEmail emailText:
                    ProcessRichText(emailText.Text, span);
                    break;
                case TLTextFixed fixedText:
                    span.FontFamily = new FontFamily("Consolas");
                    ProcessRichText(fixedText.Text, span);
                    break;
                case TLTextItalic italicText:
                    span.FontStyle |= FontStyle.Italic;
                    ProcessRichText(italicText.Text, span);
                    break;
                case TLTextStrike strikeText:
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
                case TLTextUnderline underlineText:
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
                case TLTextUrl urlText:
                    var hyperlink = new Hyperlink { UnderlineStyle = UnderlineStyle.None };
                    span.Inlines.Add(hyperlink);
                    hyperlink.Click += (s, args) => Hyperlink_Click(urlText);
                    ProcessRichText(urlText.Text, hyperlink);
                    break;
                case TLTextEmpty emptyText:
                    break;
            }
        }

        private double SpacingBetweenBlocks(TLPageBlockBase upper, TLPageBlockBase lower)
        {
            if (lower is TLPageBlockCover || lower is TLPageBlockChannel)
            {
                return 0;
            }

            return 12;

            if (lower is TLPageBlockCover)
            {
                return 0;
            }
            else if (lower is TLPageBlockDivider || upper is TLPageBlockDivider)
            {
                return 15; // 25;
            }
            else if (lower is TLPageBlockBlockquote || upper is TLPageBlockBlockquote || lower is TLPageBlockPullquote || upper is TLPageBlockPullquote)
            {
                return 17; // 27;
            }
            else if (lower is TLPageBlockTitle)
            {
                return 12; // 20;
            }
            else if (lower is TLPageBlockAuthorDate)
            {
                if (upper is TLPageBlockTitle)
                {
                    return 16; // 26;
                }
                else
                {
                    return 12; // 20;
                }
            }
            else if (lower is TLPageBlockParagraph)
            {
                if (upper is TLPageBlockTitle || upper is TLPageBlockAuthorDate)
                {
                    return 20; // 34;
                }
                else if (upper is TLPageBlockHeader || upper is TLPageBlockSubheader)
                {
                    return 15; // 25;
                }
                else if (upper is TLPageBlockParagraph)
                {
                    return 15; // 25;
                }
                else if (upper is TLPageBlockList)
                {
                    return 19; // 31;
                }
                else if (upper is TLPageBlockPreformatted)
                {
                    return 11; // 19;
                }
                else
                {
                    return 12; // 20;
                }
            }
            else if (lower is TLPageBlockList)
            {
                if (upper is TLPageBlockTitle || upper is TLPageBlockAuthorDate)
                {
                    return 20; // 34;
                }
                else if (upper is TLPageBlockHeader || upper is TLPageBlockSubheader)
                {
                    return 19; // 31;
                }
                else if (upper is TLPageBlockParagraph || upper is TLPageBlockList)
                {
                    return 19; // 31;
                }
                else if (upper is TLPageBlockPreformatted)
                {
                    return 11; // 19;
                }
                else
                {
                    return 12; // 20;
                }
            }
            else if (lower is TLPageBlockPreformatted)
            {
                if (upper is TLPageBlockParagraph)
                {
                    return 11; // 19;
                }
                else
                {
                    return 12; // 20;
                }
            }
            else if (lower is TLPageBlockHeader)
            {
                return 20; // 32;
            }
            else if (lower is TLPageBlockSubheader)
            {
                return 20; // 32;
            }
            else if (lower == null)
            {
                if (upper is TLPageBlockFooter)
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

        private double PaddingForBlock(TLPageBlockBase block)
        {
            if (block is TLPageBlockCover || block is TLPageBlockPreformatted ||
                block is TLPageBlockPhoto || block is TLPageBlockVideo ||
                block is TLPageBlockSlideshow || block is TLPageBlockChannel)
            {
                return 0.0;
            }

            return _padding;
        }

        private async void Image_Click(object sender, RoutedEventArgs e)
        {
            var image = sender as ImageView;
            var item = image.DataContext as GalleryItem;
            if (item != null)
            {
                ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("FullScreenPicture", image);

                ViewModel.Gallery.SelectedItem = item;

                await GalleryView.Current.ShowAsync(ViewModel.Gallery, (s, args) =>
                {
                    var animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("FullScreenPicture");
                    if (animation != null)
                    {
                        animation.TryStart(image);
                    }
                });
            }
        }

        private async void Hyperlink_Click(TLTextUrl urlText)
        {
            if (urlText.WebPageId == _webpageId)
            {
                var fragmentStart = urlText.Url.IndexOf('#');
                if (fragmentStart > 0)
                {
                    var name = urlText.Url.Substring(fragmentStart + 1);
                    if (_anchors.TryGetValue(name, out Border anchor))
                    {
                        var transform = anchor.TransformToVisual(ScrollingHost);
                        var position = transform.TransformPoint(new Point());

                        //ScrollingHost.ChangeView(null, Math.Max(0, position.Y - 8), null, false);
                    }
                }
            }
            else if (urlText.WebPageId != 0)
            {
                var protoService = (MTProtoService)MTProtoService.Current;
                protoService.SendInformativeMessageInternal<TLWebPageBase>("messages.getWebPage", new TLMessagesGetWebPage { Url = urlText.Url, Hash = 0 },
                    result =>
                    {
                        Execute.BeginOnUIThread(() =>
                        {
                            ViewModel.NavigationService.Navigate(typeof(InstantPage), result);
                        });
                    },
                    fault =>
                    {
                        Debugger.Break();
                    });
            }
            else
            {
                var url = urlText.Url;
                if (url.StartsWith("http") == false)
                {
                    url = "http://" + url;
                }

                if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
                {
                    if (Constants.TelegramHosts.Contains(uri.Host))
                    {
                        MessageHelper.HandleTelegramUrl(urlText.Url);
                    }
                    else
                    {
                        await Launcher.LaunchUriAsync(uri);
                    }
                }
            }
        }

        private async void OnWebViewNavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
        {
            var jss = _injectedJs;
            await sender.InvokeScriptAsync("eval", new[] { jss });
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
    }
}
