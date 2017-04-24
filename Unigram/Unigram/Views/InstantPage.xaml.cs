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
            LayoutRoot.Children.Clear();
            _containers.Clear();
            _containers.Push(LayoutRoot);
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
                foreach (var block in webpage.CachedPage.Blocks)
                {
                    ProcessBlock(webpage.CachedPage, block, photos, videos);
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
                                ProcessBlock(newpage.CachedPage, newpage.CachedPage.Blocks[i], photos, videos);
                            }
                        }
                    }
                }
            }

            base.OnNavigatedTo(e);
        }

        private long _webpageId;

        private Stack<Panel> _containers = new Stack<Panel>();
        private Stack<TLPageBlockBase> _parents = new Stack<TLPageBlockBase>();

        private Dictionary<string, Border> _anchors = new Dictionary<string, Border>();

        private void ProcessBlock(TLPageBase page, TLPageBlockBase block, IList<TLPhotoBase> photos, IList<TLDocumentBase> videos)
        {
            switch (block)
            {
                case TLPageBlockCover cover:
                    ProcessCover(page, cover, photos, videos);
                    break;
                case TLPageBlockAuthorDate authorDate:
                    ProcessAuthorDate(page, authorDate, photos, videos);
                    break;
                case TLPageBlockHeader header:
                case TLPageBlockSubheader subheader:
                case TLPageBlockTitle title:
                case TLPageBlockSubtitle subtitle:
                case TLPageBlockFooter footer:
                case TLPageBlockParagraph paragraph:
                    ProcessTextBlock(page, block, photos, videos, false);
                    break;
                case TLPageBlockBlockquote blockquote:
                    ProcessBlockquote(page, blockquote, photos, videos);
                    break;
                case TLPageBlockDivider divider:
                    ProcessDivider(page, divider, photos, videos);
                    break;
                case TLPageBlockPhoto photo:
                    ProcessPhoto(page, photo, photos, videos);
                    break;
                case TLPageBlockList list:
                    ProcessList(page, list, photos, videos);
                    break;
                case TLPageBlockVideo video:
                    ProcessVideo(page, video, photos, videos);
                    break;
                case TLPageBlockEmbedPost embedPost:
                    ProcessEmbedPost(page, embedPost, photos, videos);
                    break;
                case TLPageBlockSlideshow slideshow:
                    ProcessSlideshow(page, slideshow, photos, videos);
                    break;
                case TLPageBlockCollage collage:
                    ProcessCollage(page, collage, photos, videos);
                    break;
                case TLPageBlockEmbed embed:
                    ProcessEmbed(page, embed, photos, videos);
                    break;
                case TLPageBlockPullquote pullquote:
                    ProcessPullquote(page, pullquote, photos, videos);
                    break;
                case TLPageBlockAnchor anchor:
                    ProcessAnchor(page, anchor, photos, videos);
                    break;
                case TLPageBlockPreformatted preformatted:
                    ProcessPreformatted(page, preformatted, photos, videos);
                    break;
                case TLPageBlockUnsupported unsupported:
                    Debug.WriteLine("Unsupported block type: " + block.GetType());
                    break;
            }
        }

        private void ProcessPreformatted(TLPageBase page, TLPageBlockPreformatted block, IList<TLPhotoBase> photos, IList<TLDocumentBase> videos)
        {
            _containers.Push(new StackPanel
            {
                Style = Resources["BlockPreformattedStyle"] as Style,
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(0, 8, 0, 0)
            });

            ProcessTextBlock(page, block, photos, videos, false);

            var panel = _containers.Pop();
            _containers.Peek().Children.Add(panel);
        }

        private void ProcessAnchor(TLPageBase page, TLPageBlockAnchor block, IList<TLPhotoBase> photos, IList<TLDocumentBase> videos)
        {
            var child = new Border();
            _containers.Peek().Children.Add(child);
            _anchors[block.Name] = child;
        }

        private void ProcessEmbed(TLPageBase page, TLPageBlockEmbed block, IList<TLPhotoBase> photos, IList<TLDocumentBase> videos)
        {
            if (block.Caption.TypeId != TLType.TextEmpty)
            {
                _containers.Push(new StackPanel { HorizontalAlignment = HorizontalAlignment.Center });
            }

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

            _containers.Peek().Children.Add(child);

            if (block.Caption.TypeId != TLType.TextEmpty)
            {
                ProcessTextBlock(page, block, photos, videos, true);

                var panel = _containers.Pop();
                _containers.Peek().Children.Add(panel);
            }
            else
            {
                child.Margin = new Thickness(0, 0, 0, 12);
            }

            if (_parents.Count > 0 && _parents.Peek().TypeId == TLType.PageBlockCover)
            {
                child.Margin = new Thickness(0, -12, 0, 12);
            }
        }

        private async void OnWebViewNavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
        {
            var jss = _injectedJs;
            await sender.InvokeScriptAsync("eval", new[] { jss });
        }

        private void ProcessCollage(TLPageBase page, TLPageBlockCollage block, IList<TLPhotoBase> photos, IList<TLDocumentBase> videos)
        {
            if (block.Caption.TypeId != TLType.TextEmpty)
            {
                _containers.Push(new StackPanel { HorizontalAlignment = HorizontalAlignment.Center });
            }

            var items = new List<Image>();
            foreach (var item in block.Items)
            {
                var photoBlock = item as TLPageBlockPhoto;
                if (photoBlock != null)
                {
                    var photo = photos.FirstOrDefault(x => x.Id == photoBlock.PhotoId);
                    var image = new Image();
                    image.Source = (ImageSource)DefaultPhotoConverter.Convert(photo, true);
                    image.Width = 72;
                    image.Height = 72;
                    image.Stretch = Stretch.UniformToFill;
                    image.Margin = new Thickness(0, 0, 4, 4);

                    items.Add(image);
                }

                var videoBlock = item as TLPageBlockVideo;
                if (videoBlock != null)
                {
                    var video = videos.FirstOrDefault(x => x.Id == videoBlock.VideoId);
                    var image = new Image();
                    image.Source = (ImageSource)DefaultPhotoConverter.Convert(video, true);
                    image.Width = 72;
                    image.Height = 72;
                    image.Stretch = Stretch.UniformToFill;
                    image.Margin = new Thickness(0, 0, 4, 4);

                    items.Add(image);
                }
            }

            var grid = new Grid();
            grid.Margin = new Thickness(12, 0, 0, 0);
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

            _containers.Peek().Children.Add(grid);

            if (block.Caption.TypeId != TLType.TextEmpty)
            {
                ProcessTextBlock(page, block, photos, videos, true);

                var panel = _containers.Pop();
                _containers.Peek().Children.Add(panel);
            }
            else
            {
                grid.Margin = new Thickness(12, 0, 0, 12);
            }
        }

        private void ProcessSlideshow(TLPageBase page, TLPageBlockSlideshow block, IList<TLPhotoBase> photos, IList<TLDocumentBase> videos)
        {
            if (block.Caption.TypeId != TLType.TextEmpty)
            {
                _containers.Push(new StackPanel { HorizontalAlignment = HorizontalAlignment.Center });
            }

            var items = new List<ImageView>();
            foreach (var item in block.Items)
            {
                var photoBlock = item as TLPageBlockPhoto;
                if (photoBlock != null)
                {
                    var photo = photos.FirstOrDefault(x => x.Id == photoBlock.PhotoId);
                    var image = new ImageView();
                    image.Source = (ImageSource)DefaultPhotoConverter.Convert(photo, true);
                    image.Constraint = photo;

                    items.Add(image);
                }

                var videoBlock = item as TLPageBlockVideo;
                if (videoBlock != null)
                {
                    var video = videos.FirstOrDefault(x => x.Id == videoBlock.VideoId);
                    var image = new ImageView();
                    image.Source = (ImageSource)DefaultPhotoConverter.Convert(video, true);
                    image.Constraint = video;

                    items.Add(image);
                }
            }

            var flip = new FlipView();
            flip.ItemsSource = items;

            _containers.Peek().Children.Add(flip);

            if (block.Caption.TypeId != TLType.TextEmpty)
            {
                ProcessTextBlock(page, block, photos, videos, true);

                var panel = _containers.Pop();
                _containers.Peek().Children.Add(panel);
            }
            else
            {
                flip.Margin = new Thickness(0, 0, 0, 12);
            }
        }

        private void ProcessEmbedPost(TLPageBase page, TLPageBlockEmbedPost block, IList<TLPhotoBase> photos, IList<TLDocumentBase> videos)
        {
            _containers.Push(new StackPanel
            {
                BorderBrush = new SolidColorBrush(Colors.Black),
                BorderThickness = new Thickness(2, 0, 0, 0),
                Margin = new Thickness(12, 0, 0, 12)
            });

            var header = new Grid();
            header.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            header.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.Margin = new Thickness(12, 0, 0, 12);

            var photo = photos.FirstOrDefault(x => x.Id == block.AuthorPhotoId);

            var ellipse = new Ellipse();
            ellipse.Width = 36;
            ellipse.Height = 36;
            ellipse.Margin = new Thickness(0, 0, 12, 0);
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

            _containers.Peek().Children.Add(header);

            foreach (var sub in block.Blocks)
            {
                ProcessBlock(page, sub, photos, videos);
            }

            var panel = _containers.Pop();
            _containers.Peek().Children.Add(panel);
        }

        private void ProcessVideo(TLPageBase page, TLPageBlockVideo block, IList<TLPhotoBase> photos, IList<TLDocumentBase> videos)
        {
            if (block.Caption.TypeId != TLType.TextEmpty)
            {
                _containers.Push(new StackPanel { HorizontalAlignment = HorizontalAlignment.Center });
            }

            var video = videos.FirstOrDefault(x => x.Id == block.VideoId);
            var galleryItem = new GalleryDocumentItem(video as TLDocument, block.Caption?.ToString());
            var image = new ImageView();
            image.Source = (ImageSource)DefaultPhotoConverter.Convert(video, true);
            image.Constraint = video;
            image.DataContext = galleryItem;
            image.Click += Image_Click;
            image.HorizontalAlignment = HorizontalAlignment.Center;

            ViewModel.Gallery.Items.Add(galleryItem);

            _containers.Peek().Children.Add(image);

            if (block.Caption.TypeId != TLType.TextEmpty)
            {
                ProcessTextBlock(page, block, photos, videos, true);

                var panel = _containers.Pop();
                _containers.Peek().Children.Add(panel);
            }
            else
            {
                image.Margin = new Thickness(0, 0, 0, 12);
            }
        }

        private void ProcessList(TLPageBase page, TLPageBlockList block, IList<TLPhotoBase> photos, IList<TLDocumentBase> videos)
        {
            var textBlock = new RichTextBlock();
            textBlock.TextWrapping = TextWrapping.Wrap;
            textBlock.Margin = new Thickness(12, 0, 12, 12);
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
                ProcessText(text, span);
                textBlock.Blocks.Add(par);
            }

            _containers.Peek().Children.Add(textBlock);
        }

        private void ProcessDivider(TLPageBase page, TLPageBlockDivider block, IList<TLPhotoBase> photos, IList<TLDocumentBase> videos)
        {
            _containers.Peek().Children.Add(new Rectangle
            {
                Height = 1,
                Fill = (SolidColorBrush)Resources["SystemControlDisabledChromeDisabledLowBrush"],
                Margin = new Thickness(72, 0, 72, 12)
            });
        }

        private void ProcessBlockquote(TLPageBase page, TLPageBlockBlockquote block, IList<TLPhotoBase> photos, IList<TLDocumentBase> videos)
        {
            _containers.Push(new StackPanel
            {
                BorderBrush = new SolidColorBrush(Colors.Black),
                BorderThickness = new Thickness(2, 0, 0, 0),
                Margin = new Thickness(12, 0, 0, 12)
            });

            ProcessTextBlock(page, block, photos, videos, false);
            ProcessTextBlock(page, block, photos, videos, true);

            var panel = _containers.Pop();
            _containers.Peek().Children.Add(panel);
        }

        private void ProcessPullquote(TLPageBase page, TLPageBlockPullquote block, IList<TLPhotoBase> photos, IList<TLDocumentBase> videos)
        {
            _containers.Push(new StackPanel
            {
                //BorderBrush = new SolidColorBrush(Colors.Black),
                //BorderThickness = new Thickness(2, 0, 0, 0),
                Margin = new Thickness(0, 0, 0, 12)
            });

            ProcessTextBlock(page, block, photos, videos, false);
            ProcessTextBlock(page, block, photos, videos, true);

            var panel = _containers.Pop();
            _containers.Peek().Children.Add(panel);
        }

        private void ProcessTextBlock(TLPageBase page, TLPageBlockBase block, IList<TLPhotoBase> photos, IList<TLDocumentBase> videos, bool caption)
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
                textBlock.Margin = new Thickness(12, 0, 12, 12);
                ProcessText(text, span);

                switch (block.TypeId)
                {
                    case TLType.PageBlockTitle:
                        textBlock.FontSize = 24;
                        textBlock.FontFamily = new FontFamily("Times New Roman");
                        textBlock.Margin = new Thickness(12, 8, 12, 12);
                        textBlock.TextLineBounds = TextLineBounds.TrimToBaseline;
                        break;
                    case TLType.PageBlockSubtitle:
                        textBlock.FontSize = 21;
                        textBlock.FontFamily = new FontFamily("Times New Roman");
                        textBlock.Margin = new Thickness(12, 8, 12, 12);
                        textBlock.TextLineBounds = TextLineBounds.TrimToBaseline;
                        break;
                    case TLType.PageBlockHeader:
                        textBlock.FontSize = 21;
                        textBlock.FontFamily = new FontFamily("Times New Roman");
                        textBlock.Margin = new Thickness(12, 8, 12, 12);
                        textBlock.TextLineBounds = TextLineBounds.TrimToBaseline;
                        break;
                    case TLType.PageBlockSubheader:
                        textBlock.FontSize = 18;
                        textBlock.FontFamily = new FontFamily("Times New Roman");
                        textBlock.Margin = new Thickness(12, 8, 12, 12);
                        textBlock.TextLineBounds = TextLineBounds.TrimToBaseline;
                        break;
                    case TLType.PageBlockFooter:
                        textBlock.FontSize = 14;
                        break;
                    case TLType.PageBlockPhoto:
                    case TLType.PageBlockVideo:
                    case TLType.PageBlockSlideshow:
                    case TLType.PageBlockEmbed:
                    case TLType.PageBlockEmbedPost:
                        textBlock.FontSize = 14;
                        textBlock.Foreground = (SolidColorBrush)Resources["SystemControlDisabledChromeDisabledLowBrush"];
                        textBlock.Margin = new Thickness(12, 4, 12, 12);
                        break;
                    case TLType.PageBlockParagraph:
                        textBlock.FontSize = 16;
                        textBlock.Margin = new Thickness(12, 0, 12, 12);
                        break;
                    case TLType.PageBlockBlockquote:
                        textBlock.FontSize = caption ? 14 : 15;
                        textBlock.Margin = new Thickness(8, 0, 12, 0);
                        break;
                    case TLType.PageBlockPullquote:
                        var pullquoteBlock = block as TLPageBlockPullquote;
                        textBlock.FontSize = caption ? 14 : 18;
                        textBlock.FontFamily = new FontFamily("Times New Roman");
                        textBlock.TextAlignment = TextAlignment.Center;
                        break;
                }

                _containers.Peek().Children.Add(textBlock);
            }
        }

        private void ProcessCover(TLPageBase page, TLPageBlockCover block, IList<TLPhotoBase> photos, IList<TLDocumentBase> videos)
        {
            _parents.Push(block);
            ProcessBlock(page, block.Cover, photos, videos);
            _parents.Pop();
        }

        private void ProcessPhoto(TLPageBase page, TLPageBlockPhoto block, IList<TLPhotoBase> photos, IList<TLDocumentBase> videos)
        {
            if (block.Caption.TypeId != TLType.TextEmpty)
            {
                _containers.Push(new StackPanel { HorizontalAlignment = HorizontalAlignment.Center });
            }

            var photo = photos.FirstOrDefault(x => x.Id == block.PhotoId);
            var galleryItem = new GalleryPhotoItem(photo as TLPhoto, block.Caption?.ToString());
            var image = new ImageView();
            image.Source = (ImageSource)DefaultPhotoConverter.Convert(photo, true);
            image.Constraint = photo;
            image.DataContext = galleryItem;
            image.Click += Image_Click;
            image.HorizontalAlignment = HorizontalAlignment.Center;

            ViewModel.Gallery.Items.Add(galleryItem);

            _containers.Peek().Children.Add(image);

            if (block.Caption.TypeId != TLType.TextEmpty)
            {
                ProcessTextBlock(page, block, photos, videos, true);

                var panel = _containers.Pop();
                _containers.Peek().Children.Add(panel);
            }
            else
            {
                image.Margin = new Thickness(0, 0, 0, 12);
            }

            if (_parents.Count > 0 && _parents.Peek().TypeId == TLType.PageBlockCover)
            {
                image.Margin = new Thickness(0, -12, 0, 12);
            }
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

        private void ProcessAuthorDate(TLPageBase page, TLPageBlockAuthorDate block, IList<TLPhotoBase> photos, IList<TLDocumentBase> videos)
        {
            var textBlock = new TextBlock { Style = Resources["AuthorDateTextBlockStyle"] as Style };

            if (block.Author.TypeId != TLType.TextEmpty)
            {
                var span = new Span();
                textBlock.Inlines.Add(new Run { Text = "By " });
                textBlock.Inlines.Add(span);
                ProcessText(block.Author, span);

                textBlock.Inlines.Add(new Run { Text = " — " });
            }

            //textBlock.Inlines.Add(new Run { Text = DateTimeFormatter.LongDate.Format(BindConvert.Current.DateTime(block.PublishedDate)) });
            textBlock.Inlines.Add(new Run { Text = BindConvert.Current.DateTime(block.PublishedDate).ToString("dd MMMM yyyy") });

            _containers.Peek().Children.Add(textBlock);
        }

        private void ProcessText(TLRichTextBase text, Span span)
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
                        ProcessText(concat, concatRun);
                    }
                    break;
                case TLTextBold boldText:
                    span.FontWeight = FontWeights.SemiBold;
                    ProcessText(boldText.Text, span);
                    break;
                case TLTextEmail emailText:
                    ProcessText(emailText.Text, span);
                    break;
                case TLTextFixed fixedText:
                    span.FontFamily = new FontFamily("Consolas");
                    ProcessText(fixedText.Text, span);
                    break;
                case TLTextItalic italicText:
                    span.FontStyle |= FontStyle.Italic;
                    ProcessText(italicText.Text, span);
                    break;
                case TLTextStrike strikeText:
                    if (ApiInformation.IsPropertyPresent("Windows.UI.Xaml.Documents.TextElement", "TextDecorations"))
                    {
                        span.TextDecorations |= TextDecorations.Strikethrough;
                        ProcessText(strikeText.Text, span);
                    }
                    else
                    {
                        SetIsStrikethrough(span, true);
                        ProcessText(strikeText.Text, span);
                    }
                    break;
                case TLTextUnderline underlineText:
                    if (ApiInformation.IsPropertyPresent("Windows.UI.Xaml.Documents.TextElement", "TextDecorations"))
                    {
                        span.TextDecorations |= TextDecorations.Underline;
                        ProcessText(underlineText.Text, span);
                    }
                    else
                    {
                        var underline = new Underline();
                        span.Inlines.Add(underline);
                        ProcessText(underlineText.Text, underline);
                    }
                    break;
                case TLTextUrl urlText:
                    var hyperlink = new Hyperlink();
                    span.Inlines.Add(hyperlink);
                    hyperlink.Click += (s, args) => Hyperlink_Click(urlText);
                    ProcessText(urlText.Text, hyperlink);
                    break;
                case TLTextEmpty emptyText:
                    break;
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
                        var transform = anchor.TransformToVisual(LayoutRoot);
                        var position = transform.TransformPoint(new Point());

                        ScrollingHost.ChangeView(null, Math.Max(0, position.Y - 8), null, false);
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
