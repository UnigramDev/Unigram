using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Navigation;
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

            var sticker = GetContent(message);
            if (sticker == null)
            {
                return;
            }

            if (message.Content is MessageAnimatedEmoji)
            {
                MaxWidth = 180 * message.ProtoService.Config.GetNamedNumber("emojies_animated_zoom", 0.625f);
                MaxHeight = 180 * message.ProtoService.Config.GetNamedNumber("emojies_animated_zoom", 0.625f);
            }
            else
            {
                MaxWidth = 180;
                MaxHeight = 180;
            }

            Constraint = sticker;
            UpdateFile(message, sticker.StickerValue);
        }

        private void UpdateFile(object target, File file)
        {
            UpdateFile(_message, file);
        }

        private async void UpdateFile(MessageViewModel message, File file)
        {
            var sticker = GetContent(message);
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
                Source = await PlaceholderHelper.GetWebPFrameAsync(file.Local.Path, 180);
                ElementCompositionPreview.SetElementChildVisual(this, null);

                UpdateManager.Unsubscribe(this, ref _fileToken);
            }
            else 
            {
                Source = null;
                UpdateThumbnail(message, sticker);

                UpdateManager.Subscribe(this, message, sticker.StickerValue, ref _fileToken, UpdateFile, true);

                if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    message.ProtoService.DownloadFile(file.Id, 1);
                }
            }
        }

        private void UpdateThumbnail(MessageViewModel message, Sticker sticker)
        {
            _thumbnailShimmer = CompositionPathParser.ParseThumbnail(sticker, out ShapeVisual visual);
            ElementCompositionPreview.SetElementChildVisual(this, visual);
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            if (content is MessageSticker sticker)
            {
                return sticker.Sticker.Format is StickerFormatWebp;
            }
            else if (content is MessageText text && text.WebPage != null && !primary)
            {
                return text.WebPage.Sticker != null && text.WebPage.Sticker.Format is StickerFormatWebp;
            }

            return false;
        }

        private Sticker GetContent(MessageViewModel message)
        {
            var content = message.GeneratedContent ?? message.Content;
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
            var sticker = GetContent(_message);
            if (sticker == null)
            {
                return;
            }

            _message.Delegate.OpenSticker(sticker);
        }
    }
}
