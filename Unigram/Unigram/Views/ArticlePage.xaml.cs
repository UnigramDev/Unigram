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
using Unigram.Core.Dependency;
using Unigram.Core.Services;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.Globalization.DateTimeFormatting;
using Windows.System;
using Windows.UI;
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

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ArticlePage : Page
    {
        public ArticleViewModel ViewModel => DataContext as ArticleViewModel;

        public ArticlePage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Instance.ResolveType<ArticleViewModel>();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            LayoutRoot.Children.Clear();
            _containers.Clear();
            _containers.Push(new StackPanel());

            var parameter = TLSerializationService.Current.Deserialize((string)e.Parameter);

            var webpageMedia = parameter as TLMessageMediaWebPage;
            if (webpageMedia != null)
            {
                parameter = webpageMedia.Webpage as TLWebPage;
            }

            var webpage = parameter as TLWebPage;
            if (webpage != null && webpage.HasCachedPage)
            {
                var part = webpage.CachedPage as TLPagePart;
                if (part != null)
                {
                    var protoService = (MTProtoService)MTProtoService.Current;
                    protoService.SendInformativeMessageInternal<TLWebPage>("messages.getWebPage", new TLMessagesGetWebPage { Url = webpage.Url, Hash = webpage.Hash },
                        result =>
                        {
                        },
                        fault =>
                        {
                        });
                }

                var full = webpage.CachedPage as TLPageFull;

                if (webpage.HasPhoto && !webpage.CachedPage.Photos.Any(x => x.Id == webpage.Photo.Id))
                {
                    webpage.CachedPage.Photos.Insert(0, webpage.Photo);
                }

                foreach (var block in webpage.CachedPage.Blocks)
                {
                    ProcessBlock(webpage.CachedPage, block);
                }

                if (_containers.Count > 0)
                {
                    LayoutRoot.Children.Add(_containers.Pop());
                }
            }

            base.OnNavigatedTo(e);
        }

        private Stack<Panel> _containers = new Stack<Panel>();
        private Stack<TLPageBlockBase> _parents = new Stack<TLPageBlockBase>();

        private void ProcessBlock(TLPageBase page, TLPageBlockBase block)
        {
            switch (block.TypeId)
            {
                case TLType.PageBlockCover:
                    ProcessCover(page, (TLPageBlockCover)block);
                    break;
                case TLType.PageBlockAuthorDate:
                    ProcessAuthorDate(page, (TLPageBlockAuthorDate)block);
                    break;
                case TLType.PageBlockHeader:
                case TLType.PageBlockSubheader:
                case TLType.PageBlockTitle:
                case TLType.PageBlockSubtitle:
                case TLType.PageBlockFooter:
                case TLType.PageBlockParagraph:
                    ProcessTextBlock(page, block, false);
                    break;
                case TLType.PageBlockBlockquote:
                    ProcessBlockquote(page, (TLPageBlockBlockquote)block);
                    break;
                case TLType.PageBlockDivider:
                    ProcessDivider(page, (TLPageBlockDivider)block);
                    break;
                case TLType.PageBlockPhoto:
                    ProcessPhoto(page, (TLPageBlockPhoto)block);
                    break;
                case TLType.PageBlockList:
                    ProcessList(page, (TLPageBlockList)block);
                    break;
                case TLType.PageBlockVideo:
                    ProcessVideo(page, (TLPageBlockVideo)block);
                    break;
                case TLType.PageBlockEmbedPost:
                    ProcessEmbedPost(page, (TLPageBlockEmbedPost)block);
                    break;
                case TLType.PageBlockSlideshow:
                    ProcessSlideshow(page, (TLPageBlockSlideshow)block);
                    break;
                case TLType.PageBlockCollage:
                    ProcessCollage(page, (TLPageBlockCollage)block);
                    break;
                case TLType.PageBlockPreformatted:
                case TLType.PageBlockPullquote:
                case TLType.PageBlockEmbed:
                case TLType.PageBlockUnsupported:
                    Debug.WriteLine("Unsupported block type: " + block.GetType());
                    break;
                case TLType.PageBlockAnchor:
                    break;
            }
        }

        private void ProcessCollage(TLPageBase page, TLPageBlockCollage block)
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
                    var photo = page.Photos.FirstOrDefault(x => x.Id == photoBlock.PhotoId);
                    var image = new Image();
                    image.Source = (ImageSource)DefaultPhotoConverter.Convert(photo, "thumbnail");
                    image.Width = 72;
                    image.Height = 72;
                    image.Stretch = Stretch.UniformToFill;
                    image.Margin = new Thickness(0, 0, 4, 4);

                    items.Add(image);
                }

                var videoBlock = item as TLPageBlockVideo;
                if (videoBlock != null)
                {
                    var video = page.Videos.FirstOrDefault(x => x.Id == videoBlock.VideoId);
                    var image = new Image();
                    image.Source = (ImageSource)DefaultPhotoConverter.Convert(video, "thumbnail");
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
                ProcessTextBlock(page, block, true);

                var panel = _containers.Pop();
                _containers.Peek().Children.Add(panel);
            }
            else
            {
                grid.Margin = new Thickness(12, 0, 0, 12);
            }
        }

        private void ProcessSlideshow(TLPageBase page, TLPageBlockSlideshow block)
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
                    var photo = page.Photos.FirstOrDefault(x => x.Id == photoBlock.PhotoId);
                    var image = new ImageView();
                    image.Source = (ImageSource)DefaultPhotoConverter.Convert(photo, "thumbnail");
                    image.Constraint = photo;

                    items.Add(image);
                }

                var videoBlock = item as TLPageBlockVideo;
                if (videoBlock != null)
                {
                    var video = page.Videos.FirstOrDefault(x => x.Id == videoBlock.VideoId);
                    var image = new ImageView();
                    image.Source = (ImageSource)DefaultPhotoConverter.Convert(video, "thumbnail");
                    image.Constraint = video;

                    items.Add(image);
                }
            }

            var flip = new FlipView();
            flip.ItemsSource = items;

            _containers.Peek().Children.Add(flip);

            if (block.Caption.TypeId != TLType.TextEmpty)
            {
                ProcessTextBlock(page, block, true);

                var panel = _containers.Pop();
                _containers.Peek().Children.Add(panel);
            }
            else
            {
                flip.Margin = new Thickness(0, 0, 0, 12);
            }
        }

        private void ProcessEmbedPost(TLPageBase page, TLPageBlockEmbedPost block)
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

            var photo = page.Photos.FirstOrDefault(x => x.Id == block.AuthorPhotoId);

            var ellipse = new Ellipse();
            ellipse.Width = 36;
            ellipse.Height = 36;
            ellipse.Margin = new Thickness(0, 0, 12, 0);
            ellipse.Fill = new ImageBrush { ImageSource = (ImageSource)DefaultPhotoConverter.Convert(photo, "thumbnail"), Stretch = Stretch.UniformToFill, AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center };
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
                ProcessBlock(page, sub);
            }

            var panel = _containers.Pop();
            _containers.Peek().Children.Add(panel);
        }

        private void ProcessVideo(TLPageBase page, TLPageBlockVideo block)
        {
            if (block.Caption.TypeId != TLType.TextEmpty)
            {
                _containers.Push(new StackPanel { HorizontalAlignment = HorizontalAlignment.Center });
            }

            var video = page.Videos.FirstOrDefault(x => x.Id == block.VideoId);
            var image = new ImageView();
            image.Source = (ImageSource)DefaultPhotoConverter.Convert(video, "thumbnail");
            image.Constraint = video;

            _containers.Peek().Children.Add(image);

            if (block.Caption.TypeId != TLType.TextEmpty)
            {
                ProcessTextBlock(page, block, true);

                var panel = _containers.Pop();
                _containers.Peek().Children.Add(panel);
            }
            else
            {
                image.Margin = new Thickness(0, 0, 0, 12);
            }
        }

        private void ProcessList(TLPageBase page, TLPageBlockList block)
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

                var span = new Run();
                par.Inlines.Add(new Run { Text = block.Ordered ? (i + 1) + ".\t" : "•\t" });
                par.Inlines.Add(span);
                ProcessText(text, par.Inlines, span);
                textBlock.Blocks.Add(par);
            }

            _containers.Peek().Children.Add(textBlock);
        }

        private void ProcessDivider(TLPageBase page, TLPageBlockDivider block)
        {
            _containers.Peek().Children.Add(new Rectangle
            {
                Height = 1,
                Fill = (SolidColorBrush)Resources["SystemControlDisabledChromeDisabledLowBrush"],
                Margin = new Thickness(72, 0, 72, 12)
            });
        }

        private void ProcessBlockquote(TLPageBase page, TLPageBlockBlockquote block)
        {
            _containers.Push(new StackPanel
            {
                BorderBrush = new SolidColorBrush(Colors.Black),
                BorderThickness = new Thickness(2, 0, 0, 0),
                Margin = new Thickness(12, 0, 0, 12)
            });

            ProcessTextBlock(page, block, false);
            ProcessTextBlock(page, block, true);

            var panel = _containers.Pop();
            _containers.Peek().Children.Add(panel);
        }

        private void ProcessTextBlock(TLPageBase page, TLPageBlockBase block, bool caption)
        {
            TLRichTextBase text = null;
            switch (block.TypeId)
            {
                case TLType.PageBlockTitle:
                    text = ((TLPageBlockTitle)block).Text;
                    break;
                case TLType.PageBlockSubtitle:
                    text = ((TLPageBlockSubtitle)block).Text;
                    break;
                case TLType.PageBlockHeader:
                    text = ((TLPageBlockHeader)block).Text;
                    break;
                case TLType.PageBlockSubheader:
                    text = ((TLPageBlockSubheader)block).Text;
                    break;
                case TLType.PageBlockFooter:
                    text = ((TLPageBlockFooter)block).Text;
                    break;
                case TLType.PageBlockParagraph:
                    text = ((TLPageBlockParagraph)block).Text;
                    break;
                case TLType.PageBlockPhoto:
                    text = ((TLPageBlockPhoto)block).Caption;
                    break;
                case TLType.PageBlockVideo:
                    text = ((TLPageBlockVideo)block).Caption;
                    break;
                case TLType.PageBlockSlideshow:
                    text = ((TLPageBlockSlideshow)block).Caption;
                    break;
                case TLType.PageBlockBlockquote:
                    text = caption ? ((TLPageBlockBlockquote)block).Caption : ((TLPageBlockBlockquote)block).Text;
                    break;
                case TLType.PageBlockPullquote:
                    text = caption ? ((TLPageBlockBlockquote)block).Caption : ((TLPageBlockBlockquote)block).Text;
                    break;
            }

            if (text != null && text.TypeId != TLType.TextEmpty)
            {
                var textBlock = new TextBlock();
                var span = new Run();
                textBlock.Inlines.Add(span);
                textBlock.TextWrapping = TextWrapping.Wrap;
                textBlock.Margin = new Thickness(12, 0, 12, 12);

                ProcessText(text, textBlock.Inlines, span);

                switch (block.TypeId)
                {
                    case TLType.PageBlockTitle:
                        textBlock.FontSize = 24;
                        textBlock.FontFamily = new FontFamily("Times New Roman");
                        textBlock.Margin = new Thickness(12, 0, 12, 12);
                        textBlock.TextLineBounds = TextLineBounds.TrimToBaseline;
                        break;
                    case TLType.PageBlockSubtitle:
                        textBlock.FontSize = 21;
                        textBlock.FontFamily = new FontFamily("Times New Roman");
                        textBlock.Margin = new Thickness(12, 0, 12, 12);
                        textBlock.TextLineBounds = TextLineBounds.TrimToBaseline;
                        break;
                    case TLType.PageBlockHeader:
                        textBlock.FontSize = 21;
                        textBlock.FontFamily = new FontFamily("Times New Roman");
                        textBlock.Margin = new Thickness(12, 0, 12, 12);
                        textBlock.TextLineBounds = TextLineBounds.TrimToBaseline;
                        break;
                    case TLType.PageBlockSubheader:
                        textBlock.FontSize = 18;
                        textBlock.FontFamily = new FontFamily("Times New Roman");
                        textBlock.Margin = new Thickness(12, 0, 12, 12);
                        textBlock.TextLineBounds = TextLineBounds.TrimToBaseline;
                        break;
                    case TLType.PageBlockFooter:
                        textBlock.FontSize = 14;
                        break;
                    case TLType.PageBlockPhoto:
                        textBlock.FontSize = 14;
                        textBlock.Foreground = (SolidColorBrush)Resources["SystemControlDisabledChromeDisabledLowBrush"];
                        textBlock.Margin = new Thickness(12, 4, 12, 12);
                        break;
                    case TLType.PageBlockVideo:
                        textBlock.FontSize = 14;
                        textBlock.Foreground = (SolidColorBrush)Resources["SystemControlDisabledChromeDisabledLowBrush"];
                        textBlock.Margin = new Thickness(12, 4, 12, 12);
                        break;
                    case TLType.PageBlockSlideshow:
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
                        textBlock.FontSize = caption ? 14 : 15;
                        break;
                }

                _containers.Peek().Children.Add(textBlock);
            }
        }

        private void ProcessCover(TLPageBase page, TLPageBlockCover block)
        {
            _parents.Push(block);
            ProcessBlock(page, block.Cover);
            _parents.Pop();
        }

        private void ProcessPhoto(TLPageBase page, TLPageBlockPhoto block)
        {
            if (block.Caption.TypeId != TLType.TextEmpty)
            {
                _containers.Push(new StackPanel { HorizontalAlignment = HorizontalAlignment.Center });
            }

            var photo = page.Photos.FirstOrDefault(x => x.Id == block.PhotoId);
            var image = new ImageView();
            image.Source = (ImageSource)DefaultPhotoConverter.Convert(photo, "thumbnail");
            image.Constraint = photo;

            _containers.Peek().Children.Add(image);

            if (block.Caption.TypeId != TLType.TextEmpty)
            {
                ProcessTextBlock(page, block, true);

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

        private void ProcessAuthorDate(TLPageBase page, TLPageBlockAuthorDate block)
        {
            var textBlock = new TextBlock();
            var span = new Run();
            textBlock.FontSize = 14;
            textBlock.Inlines.Add(new Run { Text = "By " });
            textBlock.Inlines.Add(span);
            textBlock.TextWrapping = TextWrapping.Wrap;
            textBlock.Margin = new Thickness(12, 0, 12, 12);
            textBlock.Foreground = (SolidColorBrush)Resources["SystemControlDisabledChromeDisabledLowBrush"];
            ProcessText(block.Author, textBlock.Inlines, span);

            textBlock.Inlines.Add(new Run { Text = " • " });
            //textBlock.Inlines.Add(new Run { Text = DateTimeFormatter.LongDate.Format(BindConvert.Current.DateTime(block.PublishedDate)) });
            textBlock.Inlines.Add(new Run { Text = BindConvert.Current.DateTime(block.PublishedDate).ToString("dd MMMM yyyy") });

            _containers.Peek().Children.Add(textBlock);
        }

        private void ProcessText(TLRichTextBase text, InlineCollection collection, Run span)
        {
            switch (text.TypeId)
            {
                case TLType.TextPlain:
                    var plainText = (TLTextPlain)text;

                    // Strikethrough fallback
                    if (GetIsStrikethrough(span))
                    {
                        span.Text = StrikethroughFallback(plainText.Text);
                    }
                    else
                    {
                        span.Text = plainText.Text;
                    }
                    break;
                case TLType.TextConcat:
                    var concatText = (TLTextConcat)text;
                    foreach (var concat in concatText.Texts)
                    {
                        var concatRun = new Run();
                        collection.Add(concatRun);
                        ProcessText(concat, collection, concatRun);
                    }
                    break;
                case TLType.TextBold:
                    var boldText = (TLTextBold)text;
                    span.FontWeight = FontWeights.SemiBold;
                    ProcessText(boldText.Text, collection, span);
                    break;
                case TLType.TextEmail:
                    var emailText = (TLTextEmail)text;
                    ProcessText(emailText.Text, collection, span);
                    break;
                case TLType.TextFixed:
                    var fixedText = (TLTextFixed)text;
                    ProcessText(fixedText.Text, collection, span);
                    break;
                case TLType.TextItalic:
                    var italicText = (TLTextItalic)text;
                    span.FontStyle = FontStyle.Italic;
                    ProcessText(italicText.Text, collection, span);
                    break;
                case TLType.TextStrike:
                    var strikeText = (TLTextStrike)text;
                    // 10.0.15021 or higher
                    if (ApiInformation.IsPropertyPresent("Windows.UI.Xaml.Documents.TextElement", "TextDecorations"))
                    {
                        // TODO: uncomment when RTM SDK will be publicly available
                        //span.TextDecorations = Windows.UI.Text.TextDecorations.Strikethrough;
                        //ProcessText(underlineText.Text, collection, span);
                    }
                    else
                    {
                        // TODO: not supported in xaml
                        SetIsStrikethrough(span, true);
                        ProcessText(strikeText.Text, collection, span);
                    }
                    break;
                case TLType.TextUnderline:
                    var underlineText = (TLTextUnderline)text;

                    // 10.0.15021 or higher
                    if (ApiInformation.IsPropertyPresent("Windows.UI.Xaml.Documents.TextElement", "TextDecorations"))
                    {
                        // TODO: uncomment when RTM SDK will be publicly available
                        //span.TextDecorations = Windows.UI.Text.TextDecorations.Underline;
                        //ProcessText(underlineText.Text, collection, span);
                    }
                    else
                    {
                        var underline = new Underline();
                        collection.Remove(span);
                        collection.Add(underline);
                        underline.Inlines.Add(span);
                        ProcessText(underlineText.Text, underline.Inlines, span);
                    }
                    break;
                case TLType.TextUrl:
                    var urlText = (TLTextUrl)text;
                    var hyperlink = new Hyperlink();
                    collection.Remove(span);
                    collection.Add(hyperlink);
                    hyperlink.Inlines.Add(span);
                    hyperlink.Click += (s, args) => Hyperlink_Click(urlText);
                    ProcessText(urlText.Text, hyperlink.Inlines, span);
                    break;
                case TLType.TextEmpty:
                    var emptyText = (TLTextEmpty)text;
                    break;
            }
        }

        private async void Hyperlink_Click(TLTextUrl urlText)
        {
            if (urlText.WebpageId != 0)
            {
                var protoService = (MTProtoService)MTProtoService.Current;
                protoService.SendInformativeMessageInternal<TLWebPage>("messages.getWebPage", new TLMessagesGetWebPage { Url = urlText.Url, Hash = 0 },
                    result =>
                    {
                        Execute.BeginOnUIThread(() =>
                        {
                            ViewModel.NavigationService.Navigate(typeof(ArticlePage), result);
                        });
                    },
                    fault =>
                    {
                    });
            }
            else
            {
                Uri uri;
                if (Uri.TryCreate(urlText.Url, UriKind.Absolute, out uri))
                {
                    if (uri.Host.Equals("t.me") || uri.Host.Equals("telegram.me"))
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
            DependencyProperty.RegisterAttached("IsStrikethrough", typeof(bool), typeof(ArticlePage), new PropertyMetadata(false));

        #endregion
    }
}
