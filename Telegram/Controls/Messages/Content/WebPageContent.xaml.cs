//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Threading;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls.Messages.Content
{
    public sealed class WebPageContent : Control, IContentWithPlayback
    {
        private CancellationTokenSource _instantViewToken;

        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        private long _fileToken;

        public WebPageContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(WebPageContent);
        }

        public WebPageContent()
        {
            DefaultStyleKey = typeof(WebPageContent);
        }

        #region InitializeComponent

        private DashPath AccentDash;
        private RichTextBlock Label;
        private RichTextBlockOverflow OverflowArea;
        private Run TitleLabel;
        private Run SubtitleLabel;
        private Run ContentLabel;
        private Border SmallPanel;
        private Image Texture;
        private Border Media;
        private Border Overlay;
        private TextBlock Subtitle;
        private Rectangle ButtonLine;
        private Button Button;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            AccentDash = GetTemplateChild(nameof(AccentDash)) as DashPath;
            Label = GetTemplateChild(nameof(Label)) as RichTextBlock;
            OverflowArea = GetTemplateChild(nameof(OverflowArea)) as RichTextBlockOverflow;
            TitleLabel = GetTemplateChild(nameof(TitleLabel)) as Run;
            SubtitleLabel = GetTemplateChild(nameof(SubtitleLabel)) as Run;
            ContentLabel = GetTemplateChild(nameof(ContentLabel)) as Run;
            SmallPanel = GetTemplateChild(nameof(SmallPanel)) as Border;
            Texture = GetTemplateChild(nameof(Texture)) as Image;
            Media = GetTemplateChild(nameof(Media)) as Border;
            Overlay = GetTemplateChild(nameof(Overlay)) as Border;
            Subtitle = GetTemplateChild(nameof(Subtitle)) as TextBlock;
            ButtonLine = GetTemplateChild(nameof(ButtonLine)) as Rectangle;
            Button = GetTemplateChild(nameof(Button)) as Button;

            Label.OverflowContentTarget = OverflowArea;
            Button.Click += Button_Click;

            _templateApplied = true;

            if (_message != null)
            {
                UpdateMessage(_message);
            }
        }

        #endregion

        public void UpdateMessage(MessageViewModel message)
        {
            _instantViewToken?.Cancel();
            _instantViewToken = new CancellationTokenSource();

            _message = message;

            var text = message.Content as MessageText;
            if (text == null || !_templateApplied)
            {
                return;
            }

            var webPage = text.WebPage;
            if (webPage == null)
            {
                return;
            }

            Texture.Source = null;
            UpdateManager.Unsubscribe(this, ref _fileToken, true);

            UpdateWebPage(webPage);
            UpdateInstantView(webPage);
            UpdateInstantView(webPage, _instantViewToken.Token);

            if (webPage.ForceSmallMedia || (!webPage.ForceLargeMedia && webPage.IsSmall()))
            {
                SmallPanel.Visibility = Visibility.Visible;
                Media.Visibility = Visibility.Collapsed;
                OverflowArea.Margin = new Thickness(0, 0, 0, 0);

                Media.Child = null;

                var small = webPage.GetThumbnail();
                if (small != null)
                {
                    UpdateManager.Subscribe(this, message, small.File, ref _fileToken, UpdateFile, true);
                    UpdateFile(message, small.File);
                }
            }
            else if (webPage.IsMedia())
            {
                SmallPanel.Visibility = Visibility.Collapsed;
                Media.Visibility = Visibility.Visible;
                OverflowArea.Margin = new Thickness(0, 0, 0, 8);

                UpdateContent(message, webPage);
            }
            else
            {
                SmallPanel.Visibility = Visibility.Collapsed;
                Media.Visibility = Visibility.Collapsed;
                OverflowArea.Margin = new Thickness(0, 0, 0, 0);

                Media.Child = null;
            }

            var outgoing = message.IsOutgoing && !message.IsChannelPost;
            var sender = message.GetSender();

            var tintId = outgoing ? null : sender switch
            {
                User user => user.AccentColorId,
                Chat chat => chat.AccentColorId,
                _ => null
            };

            if (tintId != null)
            {
                var accent = message.ClientService.GetAccentColor(tintId);

                HeaderBrush =
                    BorderBrush = new SolidColorBrush(accent[0]);

                AccentDash.Stripe1 = accent.Length > 1
                    ? new SolidColorBrush(accent[1])
                    : null;
                AccentDash.Stripe2 = accent.Length > 2
                    ? new SolidColorBrush(accent[2])
                    : null;
            }
            else
            {
                ClearValue(HeaderBrushProperty);
                ClearValue(BorderBrushProperty);

                AccentDash.Stripe1 = null;
                AccentDash.Stripe2 = null;
            }
        }

        private void UpdateFile(object target, File file)
        {
            UpdateFile(_message, file);
        }

        private void UpdateFile(MessageViewModel message, File file)
        {
            var text = message.Content as MessageText;
            if (text == null || !_templateApplied)
            {
                return;
            }

            var webPage = text.WebPage;
            if (webPage == null)
            {
                return;
            }

            var small = webPage.GetThumbnail();
            if (small == null)
            {
                return;
            }

            if (small.File.Id != file.Id)
            {
                return;
            }

            if (file.Local.IsDownloadingCompleted)
            {
                double ratioX = (double)48 / small.Width;
                double ratioY = (double)48 / small.Height;
                double ratio = Math.Max(ratioX, ratioY);

                var width = (int)(small.Width * ratio);
                var height = (int)(small.Height * ratio);

                Texture.Source = UriEx.ToBitmap(file.Local.Path, width, height);
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                message.ClientService.DownloadFile(file.Id, 1);
            }
        }

        private void UpdateContent(MessageViewModel message, WebPage webPage)
        {
            if (Media.Child is IContent media)
            {
                if (media.IsValid(message.Content, false))
                {
                    media.UpdateMessage(message);
                    return;
                }
                else
                {
                    media.Recycle();
                }
            }

            var maxWidth = (double)BootStrapper.Current.Resources["MessageMaxWidth"];
            maxWidth -= 10 + 8 + 2 + 10;

            if (string.Equals(webPage.Type, "telegram_background", StringComparison.OrdinalIgnoreCase))
            {
                Media.Child = new WallpaperContent(message);
            }
            else if (webPage.Animation != null)
            {
                Media.Child = new AnimationContent(message) { MaxWidth = maxWidth };
            }
            else if (webPage.Audio != null)
            {
                Media.Child = new AudioContent(message);
            }
            else if (webPage.Document != null)
            {
                Media.Child = new DocumentContent(message);
            }
            else if (webPage.Sticker != null)
            {
                if (webPage.Sticker.Format is StickerFormatTgs)
                {
                    Media.Child = new AnimatedStickerContent(message);
                }
                else if (webPage.Sticker.Format is StickerFormatWebm)
                {
                    Media.Child = new VideoStickerContent(message);
                }
                else
                {
                    Media.Child = new StickerContent(message);
                }
            }
            else if (webPage.Video != null)
            {
                Media.Child = new VideoContent(message) { MaxWidth = maxWidth };
            }
            else if (webPage.VideoNote != null)
            {
                Media.Child = new VideoNoteContent(message);
            }
            else if (webPage.VoiceNote != null)
            {
                Media.Child = new VoiceNoteContent(message);
            }
            else if (webPage.Photo != null)
            {
                // Photo at last: web page preview might have both a file and a thumbnail
                Media.Child = new PhotoContent(message) { MaxWidth = maxWidth };
            }
            else
            {
                Media.Child = null;
            }
        }

        public void Recycle()
        {
            _message = null;

            if (_templateApplied && Media.Child is IContent content)
            {
                content.Recycle();
            }

            UpdateManager.Unsubscribe(this, ref _fileToken, true);
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            return content is MessageText text && text.WebPage != null;
        }

        public IPlayerView GetPlaybackElement()
        {
            if (Media?.Child is IContentWithPlayback content)
            {
                return content.GetPlaybackElement();
            }
            else if (Media?.Child is IPlayerView playback)
            {
                return playback;
            }

            return null;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var text = _message?.Content as MessageText;
            if (text == null)
            {
                return;
            }

            var webPage = text.WebPage;
            if (webPage == null)
            {
                return;
            }

            _message?.Delegate?.OpenWebPage(webPage);
        }

        public void Mockup(WebPage webPage)
        {
            UpdateWebPage(webPage);

            SmallPanel.Visibility = Visibility.Collapsed;
            Media.Visibility = Visibility.Collapsed;
            OverflowArea.Margin = new Thickness(0, 0, 0, 0);

            ButtonLine.Visibility = Visibility.Collapsed;
            Button.Visibility = Visibility.Collapsed;
        }

        public void UpdateMockup(IClientService clientService, int color)
        {
            var accent = clientService.GetAccentColor(new AccentColorId(color));

            HeaderBrush =
                BorderBrush = new SolidColorBrush(accent[0]);

            if (AccentDash != null)
            {
                AccentDash.Stripe1 = accent.Length > 1
                    ? new SolidColorBrush(accent[1])
                    : null;
                AccentDash.Stripe2 = accent.Length > 2
                    ? new SolidColorBrush(accent[2])
                    : null;
            }
        }

        private void UpdateWebPage(WebPage webPage)
        {
            var empty = true;

            if (string.Equals(webPage.Type, "telegram_background", StringComparison.OrdinalIgnoreCase))
            {
                empty = false;
                TitleLabel.Text = Strings.ChatBackground;
                SubtitleLabel.Text = string.Empty;
                ContentLabel.Text = string.Empty;
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(webPage.SiteName))
                {
                    empty = false;
                    TitleLabel.Text = webPage.SiteName;
                }
                else
                {
                    TitleLabel.Text = string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(webPage.Title))
                {
                    if (TitleLabel.Text.Length > 0)
                    {
                        SubtitleLabel.Text = Environment.NewLine;
                    }

                    empty = false;
                    SubtitleLabel.Text += webPage.Title;
                }
                else if (!string.IsNullOrWhiteSpace(webPage.Author))
                {
                    if (TitleLabel.Text.Length > 0)
                    {
                        SubtitleLabel.Text = Environment.NewLine;
                    }

                    empty = false;
                    SubtitleLabel.Text += webPage.Author;
                }
                else
                {
                    SubtitleLabel.Text = string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(webPage.Description?.Text))
                {
                    if (TitleLabel.Text.Length > 0 || SubtitleLabel.Text.Length > 0)
                    {
                        ContentLabel.Text = Environment.NewLine;
                    }

                    empty = false;
                    ContentLabel.Text += webPage.Description.Text;
                }
                else
                {
                    ContentLabel.Text = string.Empty;
                }
            }

            Label.Visibility = empty
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void UpdateInstantView(WebPage webPage)
        {
            if (webPage.InstantViewVersion != 0)
            {
                if (webPage.IsInstantGallery())
                {
                    ButtonLine.Visibility = Visibility.Collapsed;
                    Button.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ShowButton(Strings.InstantView, "\uE60E");
                }
            }
            else if (string.Equals(webPage.Type, "telegram_megagroup", StringComparison.OrdinalIgnoreCase))
            {
                ShowButton(Strings.OpenGroup);
            }
            else if (string.Equals(webPage.Type, "telegram_channel", StringComparison.OrdinalIgnoreCase))
            {
                ShowButton(Strings.OpenChannel);
            }
            else if (string.Equals(webPage.Type, "telegram_channel_request", StringComparison.OrdinalIgnoreCase))
            {
                ShowButton(Strings.RequestToJoin);
            }
            else if (string.Equals(webPage.Type, "telegram_message", StringComparison.OrdinalIgnoreCase))
            {
                ShowButton(Strings.OpenMessage);
            }
            else if (string.Equals(webPage.Type, "telegram_chat", StringComparison.OrdinalIgnoreCase))
            {
                ShowButton(Strings.OpenGroup);
            }
            else if (string.Equals(webPage.Type, "telegram_voicechat", StringComparison.OrdinalIgnoreCase))
            {
                ShowButton(webPage.Url.Contains("?voicechat=") ? Strings.VoipGroupJoinAsSpeaker : Strings.VoipGroupJoinAsLinstener);
            }
            else if (string.Equals(webPage.Type, "telegram_background", StringComparison.OrdinalIgnoreCase))
            {
                ShowButton(Strings.OpenBackground);
            }
            else if (string.Equals(webPage.Type, "telegram_chatlist", StringComparison.OrdinalIgnoreCase))
            {
                ShowButton(Strings.ViewChatList);
            }
            else if (string.Equals(webPage.Type, "telegram_user", StringComparison.OrdinalIgnoreCase))
            {
                ShowButton(Strings.SendMessage);
            }
            else if (string.Equals(webPage.Type, "telegram_botapp", StringComparison.OrdinalIgnoreCase))
            {
                ShowButton(Strings.BotWebAppInstantViewOpen);
            }
            else if (string.Equals(webPage.Type, "telegram_channel_boost", StringComparison.OrdinalIgnoreCase))
            {
                ShowButton(Strings.BoostLinkButton);
            }
            else
            {
                ButtonLine.Visibility = Visibility.Collapsed;
                Button.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowButton(string text, string glyph = "")
        {
            if (string.IsNullOrEmpty(glyph))
            {
                Button.Content = text.ToUpper();
            }
            else
            {
                Button.Content = $"{glyph}  {text.ToUpper()}";
            }

            ButtonLine.Visibility = Visibility.Visible;
            Button.Visibility = Visibility.Visible;
        }

        private async void UpdateInstantView(WebPage webPage, CancellationToken token)
        {
            if (webPage.IsInstantGallery())
            {
                var response = await _message.ClientService.SendAsync(new GetWebPageInstantView(webPage.Url, false));
                if (response is WebPageInstantView instantView && instantView.IsFull && !token.IsCancellationRequested)
                {
                    var count = CountWebPageMedia(instantView);

                    Overlay.Visibility = Visibility.Visible;
                    Subtitle.Text = string.Format(Strings.Of, 1, count);
                }
                else
                {
                    Overlay.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                Overlay.Visibility = Visibility.Collapsed;
            }
        }

        private static int CountBlock(WebPageInstantView webPage, PageBlock pageBlock, int count)
        {
            if (pageBlock is PageBlockPhoto)
            {
                return count + 1;
            }
            else if (pageBlock is PageBlockVideo)
            {
                return count + 1;
            }
            else if (pageBlock is PageBlockAnimation)
            {
                return count + 1;
            }

            return count;
        }

        public static int CountWebPageMedia(WebPageInstantView webPage)
        {
            var result = 0;

            foreach (var block in webPage.PageBlocks)
            {
                if (block is PageBlockSlideshow slideshow)
                {
                    foreach (var item in slideshow.PageBlocks)
                    {
                        result = CountBlock(webPage, item, result);
                    }
                }
                else if (block is PageBlockCollage collage)
                {
                    foreach (var item in collage.PageBlocks)
                    {
                        result = CountBlock(webPage, item, result);
                    }
                }
            }

            return result;
        }

        #region HeaderBrush

        public Brush HeaderBrush
        {
            get { return (Brush)GetValue(HeaderBrushProperty); }
            set { SetValue(HeaderBrushProperty, value); }
        }

        public static readonly DependencyProperty HeaderBrushProperty =
            DependencyProperty.Register("HeaderBrush", typeof(Brush), typeof(WebPageContent), new PropertyMetadata(null));

        #endregion
    }
}
