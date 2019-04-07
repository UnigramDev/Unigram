using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Selectors;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

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
            if (Media.Content is Messages.IContent content && content.IsValid(message.Content, false))
            {
                content.UpdateMessage(message);
            }
            else
            {
                if (webPage.Animation != null)
                {
                    Media.Content = new AnimationContent(message);
                }
                else if (webPage.Audio != null)
                {
                    Media.Content = new AudioContent(message);
                }
                else if (webPage.Document != null)
                {
                    Media.Content = new DocumentContent(message);
                }
                else if (webPage.Sticker != null)
                {
                    Media.Content = new StickerContent(message);
                }
                else if (webPage.Video != null)
                {
                    Media.Content = new VideoContent(message);
                }
                else if (webPage.VideoNote != null)
                {
                    Media.Content = new VideoNoteContent(message);
                }
                else if (webPage.VoiceNote != null)
                {
                    Media.Content = new VoiceNoteContent(message);
                }
                else if (webPage.Photo != null)
                {
                    // Photo at last: web page preview might have both a file and a thumbnail
                    Media.Content = new PhotoContent(message);
                }
                else
                {
                    Media.Content = null;
                }
            }
        }

        public void UpdateFile(MessageViewModel message, File file)
        {
            if (Media.Content is IContentWithFile content && content.IsValid(message.Content, false))
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
