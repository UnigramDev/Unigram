using System;
using System.Threading;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls.Chats;
using Unigram.Navigation;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;

namespace Unigram.Controls.Messages.Content
{
    public sealed class WebPageContent : WebPageContentBase, IContent, IContentWithPlayback
    {
        private CancellationTokenSource _instantViewToken;

        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        public WebPageContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(WebPageContent);
        }

        #region InitializeComponent

        private RichTextBlock Label;
        private Run TitleLabel;
        private Run SubtitleLabel;
        private Run ContentLabel;
        private Border Media;
        private Border Overlay;
        private TextBlock Subtitle;
        private Button Button;
        private Run Run1;
        private Run Run2;
        private Run Run3;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            Label = GetTemplateChild(nameof(Label)) as RichTextBlock;
            TitleLabel = GetTemplateChild(nameof(TitleLabel)) as Run;
            SubtitleLabel = GetTemplateChild(nameof(SubtitleLabel)) as Run;
            ContentLabel = GetTemplateChild(nameof(ContentLabel)) as Run;
            Media = GetTemplateChild(nameof(Media)) as Border;
            Overlay = GetTemplateChild(nameof(Overlay)) as Border;
            Subtitle = GetTemplateChild(nameof(Subtitle)) as TextBlock;
            Button = GetTemplateChild(nameof(Button)) as Button;
            Run1 = GetTemplateChild(nameof(Run1)) as Run;
            Run2 = GetTemplateChild(nameof(Run2)) as Run;
            Run3 = GetTemplateChild(nameof(Run3)) as Run;

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

            UpdateWebPage(webPage, Label, TitleLabel, SubtitleLabel, ContentLabel);
            UpdateInstantView(webPage, Button, Run1, Run2, Run3);
            UpdateInstantView(message.ClientService, _instantViewToken.Token, webPage, Overlay, Subtitle);

            if (webPage.IsMedia())
            {
                Media.Visibility = Visibility.Visible;
                Label.Margin = new Thickness(0, 0, 0, 8);

                UpdateContent(message, webPage);
            }
            else
            {
                Media.Visibility = Visibility.Collapsed;
                Label.Margin = new Thickness(0);
            }
        }

        private void UpdateContent(MessageViewModel message, WebPage webPage)
        {
            if (Media.Child is IContent content && content.IsValid(message.Content, false))
            {
                content.UpdateMessage(message);
            }
            else
            {
                var maxWidth = (double)BootStrapper.Current.Resources["MessageMaxWidth"];
                maxWidth -= 10 + 8 + 2 + 10;

                if (string.Equals(webPage.Type, "telegram_background", StringComparison.OrdinalIgnoreCase))
                {
                    if (Uri.TryCreate(webPage.Url, UriKind.Absolute, out Uri result))
                    {
                        var background = TdBackground.FromUri(result);
                        if (background is BackgroundTypeFill typeFill)
                        {
                            var aspect = new AspectView { MaxWidth = 320, Constraint = new Size(1, 1) };
                            aspect.Children.Add(new ChatBackgroundPreview { Fill = typeFill.Fill });

                            Media.Child = aspect;
                        }
                        else if (background is BackgroundTypePattern typePattern)
                        {
                            if (webPage.Document != null)
                            {
                                var preview = new ChatBackgroundPreview { Fill = typePattern.Fill };
                                preview.Children.Add(new DocumentPhotoContent(message));

                                Media.Child = preview;
                            }
                            else
                            {
                                var aspect = new AspectView { MaxWidth = 320, Constraint = new Size(1, 1) };
                                aspect.Children.Add(new ChatBackgroundPreview { Fill = typePattern.Fill });

                                Media.Child = aspect;
                            }
                        }
                        else if (webPage.Document != null)
                        {
                            Media.Child = new DocumentPhotoContent(message);
                        }
                    }
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
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            return content is MessageText text && text.WebPage != null && !text.WebPage.IsSmallPhoto();
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
            var text = _message.Content as MessageText;
            if (text == null)
            {
                return;
            }

            var webPage = text.WebPage;
            if (webPage == null)
            {
                return;
            }

            _message.Delegate.OpenWebPage(webPage);
        }
    }
}
