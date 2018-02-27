using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using TdWindows;
using Unigram.Native;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls.Messages.Content
{
    public sealed partial class StickerContent : ImageView, IContentWithFile
    {
        private MessageViewModel _message;

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

            if (sticker.Thumbnail != null && !sticker.StickerData.Local.IsDownloadingCompleted)
            {
                UpdateThumbnail(message, sticker.Thumbnail.Photo);
            }

            UpdateFile(message, sticker.StickerData);
        }

        public void UpdateMessageContentOpened(MessageViewModel message) { }

        public async void UpdateFile(MessageViewModel message, File file)
        {
            var sticker = GetContent(message.Content);
            if (sticker == null)
            {
                return;
            }

            if (sticker.Thumbnail != null && sticker.Thumbnail.Photo.Id == file.Id)
            {
                UpdateThumbnail(message, file);
                return;
            }
            else if (sticker.StickerData.Id != file.Id)
            {
                return;
            }

            if (file.Local.IsDownloadingCompleted)
            {
                var temp = await StorageFile.GetFileFromPathAsync(file.Local.Path);
                var buffer = await FileIO.ReadBufferAsync(temp);

                Texture.Source = WebPImage.DecodeFromBuffer(buffer);
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                message.ProtoService.Send(new DownloadFile(file.Id, 1));
            }
        }

        private async void UpdateThumbnail(MessageViewModel message, File file)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                var temp = await StorageFile.GetFileFromPathAsync(file.Local.Path);
                var buffer = await FileIO.ReadBufferAsync(temp);

                Background = new ImageBrush { ImageSource = WebPImage.DecodeFromBuffer(buffer) };
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                message.ProtoService.Send(new DownloadFile(file.Id, 1));
            }
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            if (content is MessageSticker)
            {
                return true;
            }
            else if (content is MessageText text && text.WebPage != null && !primary)
            {
                return text.WebPage.Sticker != null;
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
