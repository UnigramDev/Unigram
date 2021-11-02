using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System.Collections.Generic;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;

namespace Unigram.Controls.Messages.Content
{
    public sealed class StickerContent : ImageView, IContentWithFile, IContentWithMask
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        private CompositionAnimation _thumbnailShimmer;

        public StickerContent(MessageViewModel message)
        {
            DefaultStyleKey = typeof(StickerContent);

            Click += Button_Click;

            UpdateMessage(message);
        }

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;

            var sticker = GetContent(message.Content);
            if (sticker == null)
            {
                return;
            }

            Background = null;
            Source = null;
            Constraint = message;

            if (!sticker.StickerValue.Local.IsDownloadingCompleted)
            {
                UpdateThumbnail(message, sticker.Outline);
            }

            UpdateFile(message, sticker.StickerValue);
        }

        public void UpdateMessageContentOpened(MessageViewModel message) { }

        public async void UpdateFile(MessageViewModel message, File file)
        {
            var sticker = GetContent(message.Content);
            if (sticker == null)
            {
                return;
            }

            if (sticker.StickerValue.Id != file.Id)
            {
                return;
            }

            if (file.Local.IsDownloadingCompleted)
            {
                Source = await PlaceholderHelper.GetWebPFrameAsync(file.Local.Path);
                ElementCompositionPreview.SetElementChildVisual(this, null);
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                message.ProtoService.DownloadFile(file.Id, 1);
            }
        }

        private void UpdateThumbnail(MessageViewModel message, IList<ClosedVectorPath> contours)
        {
            _thumbnailShimmer = CompositionPathParser.ParseThumbnail(contours, out ShapeVisual visual);
            ElementCompositionPreview.SetElementChildVisual(this, visual);
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            if (content is MessageSticker sticker)
            {
                return !sticker.Sticker.IsAnimated;
            }
            else if (content is MessageText text && text.WebPage != null && !primary)
            {
                return text.WebPage.Sticker != null && !text.WebPage.Sticker.IsAnimated;
            }

            return false;
        }

        private Sticker GetContent(MessageContent content)
        {
            if (content is MessageSticker sticker)
            {
                return sticker.Sticker;
            }
            else if (content is MessageText text && text.WebPage != null)
            {
                return text.WebPage.Sticker;
            }

            return null;
        }

        public CompositionBrush GetAlphaMask()
        {
            if (Holder is Image image)
            {
                return image.GetAlphaMask();
            }

            return null;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var sticker = GetContent(_message.Content);
            if (sticker == null)
            {
                return;
            }

            _message.Delegate.OpenSticker(sticker);
        }
    }
}
