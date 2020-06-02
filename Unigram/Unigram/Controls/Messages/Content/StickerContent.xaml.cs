using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls.Messages.Content
{
    public sealed partial class StickerContent : ImageView, IContentWithFile, IContentWithMask
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        public StickerContent(MessageViewModel message)
        {
            InitializeComponent();
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
            Texture.Source = null;
            Texture.Constraint = message;

            if (sticker.Thumbnail != null && !sticker.StickerValue.Local.IsDownloadingCompleted)
            {
                UpdateThumbnail(message, sticker.Thumbnail, sticker.Thumbnail.File);
            }

            UpdateFile(message, sticker.StickerValue);
        }

        public void UpdateMessageContentOpened(MessageViewModel message) { }

        public void UpdateFile(MessageViewModel message, File file)
        {
            var sticker = GetContent(message.Content);
            if (sticker == null)
            {
                return;
            }

            if (sticker.Thumbnail != null && sticker.Thumbnail.File.Id == file.Id)
            {
                UpdateThumbnail(message, sticker.Thumbnail, file);
                return;
            }
            else if (sticker.StickerValue.Id != file.Id)
            {
                return;
            }

            if (file.Local.IsDownloadingCompleted)
            {
                Texture.Source = PlaceholderHelper.GetWebPFrame(file.Local.Path);
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                message.ProtoService.DownloadFile(file.Id, 1);
            }
        }

        private void UpdateThumbnail(MessageViewModel message, Thumbnail thumbnail, File file)
        {
            if (file.Local.IsDownloadingCompleted && thumbnail.Format is ThumbnailFormatWebp)
            {
                Background = new ImageBrush { ImageSource = PlaceholderHelper.GetWebPFrame(file.Local.Path) };
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                message.ProtoService.DownloadFile(file.Id, 1);
            }
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
