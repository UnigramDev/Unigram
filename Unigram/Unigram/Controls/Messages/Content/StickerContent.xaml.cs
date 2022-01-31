using System.Collections.Generic;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Unigram.Controls.Messages.Content
{
    public sealed class StickerContent : ImageView, IContent, IContentWithMask
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        private string _fileToken;

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

            UpdateManager.Subscribe(this, message, sticker.StickerValue, ref _fileToken, UpdateFile, true);
            UpdateFile(message, sticker.StickerValue);
        }

        private void UpdateFile(object target, File file)
        {
            UpdateFile(_message, file);
        }

        private async void UpdateFile(MessageViewModel message, File file)
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
                return sticker.Sticker.Type is StickerTypeStatic or StickerTypeMask;
            }
            else if (content is MessageText text && text.WebPage != null && !primary)
            {
                return text.WebPage.Sticker != null && text.WebPage.Sticker.Type is StickerTypeStatic or StickerTypeMask;
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
