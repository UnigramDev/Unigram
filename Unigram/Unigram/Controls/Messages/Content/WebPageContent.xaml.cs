using System;
using System.Threading;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;
using Windows.UI.Xaml;

namespace Unigram.Controls.Messages.Content
{
    public sealed partial class WebPageContent : WebPageContentBase, IContentWithFile
    {
        private CancellationTokenSource _instantViewToken;

        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        public WebPageContent(MessageViewModel message)
        {
            InitializeComponent();
            UpdateMessage(message);
        }

        public void UpdateMessage(MessageViewModel message)
        {
            _instantViewToken?.Cancel();
            _instantViewToken = new CancellationTokenSource();

            _message = message;

            var text = message.Content as MessageText;
            if (text == null)
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
            UpdateInstantView(message.ProtoService, _instantViewToken.Token, webPage, Overlay, Subtitle);

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

        public void UpdateMessageContentOpened(MessageViewModel message) { }

        private void UpdateContent(MessageViewModel message, WebPage webPage)
        {
            if (Media.Child is IContent content && content.IsValid(message.Content, false))
            {
                content.UpdateMessage(message);
            }
            else
            {
                var maxWidth = (double)App.Current.Resources["MessageMaxWidth"];

                if (webPage.Animation != null)
                {
                    Media.Child = new AnimationContent(message) { MaxWidth = maxWidth };
                }
                else if (webPage.Audio != null)
                {
                    Media.Child = new AudioContent(message);
                }
                else if (webPage.Document != null)
                {
                    if (string.Equals(webPage.Type, "telegram_background", StringComparison.OrdinalIgnoreCase))
                    {
                        Media.Child = new DocumentPhotoContent(message);
                    }
                    else
                    {
                        Media.Child = new DocumentContent(message);
                    }
                }
                else if (webPage.Sticker != null)
                {
                    if (webPage.Sticker.IsAnimated)
                    {
                        Media.Child = new AnimatedStickerContent(message);
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

        public void UpdateFile(MessageViewModel message, File file)
        {
            if (Media.Child is IContentWithFile content && content.IsValid(message.Content, false))
            {
                content.UpdateFile(message, file);
            }
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            return content is MessageText text && text.WebPage != null && !text.WebPage.IsSmallPhoto();
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
