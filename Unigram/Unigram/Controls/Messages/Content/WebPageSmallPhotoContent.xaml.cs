using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;

namespace Unigram.Controls.Messages.Content
{
    public sealed class WebPageSmallPhotoContent : WebPageContentBase, IContentWithFile
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        public WebPageSmallPhotoContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(WebPageSmallPhotoContent);
        }

        #region InitializeComponent

        private RichTextBlock Label;
        private RichTextBlockOverflow OverflowArea;
        private Run TitleLabel;
        private Run SubtitleLabel;
        private Run ContentLabel;
        private Image Texture;
        private Button Button;
        private Run Run1;
        private Run Run2;
        private Run Run3;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            Label = GetTemplateChild(nameof(Label)) as RichTextBlock;
            OverflowArea = GetTemplateChild(nameof(OverflowArea)) as RichTextBlockOverflow;
            TitleLabel = GetTemplateChild(nameof(TitleLabel)) as Run;
            SubtitleLabel = GetTemplateChild(nameof(SubtitleLabel)) as Run;
            ContentLabel = GetTemplateChild(nameof(ContentLabel)) as Run;
            Texture = GetTemplateChild(nameof(Texture)) as Image;
            Button = GetTemplateChild(nameof(Button)) as Button;
            Run1 = GetTemplateChild(nameof(Run1)) as Run;
            Run2 = GetTemplateChild(nameof(Run2)) as Run;
            Run3 = GetTemplateChild(nameof(Run3)) as Run;

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

            var small = webPage.Photo?.GetSmall();
            if (small != null)
            {
                UpdateFile(message, small.Photo);
            }

            UpdateWebPage(webPage, Label, TitleLabel, SubtitleLabel, ContentLabel);
            UpdateInstantView(webPage, Button, Run1, Run2, Run3);
        }

        public void UpdateMessageContentOpened(MessageViewModel message) { }

        public void UpdateFile(MessageViewModel message, File file)
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

            var small = webPage.Photo?.GetSmall();
            if (small == null)
            {
                return;
            }

            if (small.Photo.Id != file.Id)
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
                message.ProtoService.DownloadFile(file.Id, 1);
            }
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            return content is MessageText text && text.WebPage != null && text.WebPage.IsSmallPhoto();
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

            _message?.Delegate?.OpenWebPage(webPage);
        }
    }
}
