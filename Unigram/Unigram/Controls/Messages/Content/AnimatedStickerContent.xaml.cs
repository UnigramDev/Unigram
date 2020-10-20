using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Unigram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls.Messages.Content
{
    public sealed partial class AnimatedStickerContent : HyperlinkButton, IContentWithFile
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        public AnimatedStickerContent(MessageViewModel message)
        {
            InitializeComponent();
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

            if (message.Content is MessageText text)
            {
                Width = Player.Width = 200 * message.ProtoService.Config.GetNamedNumber("emojies_animated_zoom", 0.625);
                Height = Player.Height = 200 * message.ProtoService.Config.GetNamedNumber("emojies_animated_zoom", 0.625);
                Player.ColorReplacements = Emoji.GetColorReplacements(text.Text.Text);
            }
            else
            {
                Width = Player.Width = 200;
                Height = Player.Height = 200;
                Player.ColorReplacements = null;
            }

            if (sticker.Thumbnail != null && !sticker.StickerValue.Local.IsDownloadingCompleted)
            {
                UpdateThumbnail(message, sticker.Thumbnail.File);
            }

            UpdateFile(message, sticker.StickerValue);
        }

        public void UpdateMessageContentOpened(MessageViewModel message) { }

        public void UpdateFile(MessageViewModel message, File file)
        {
            var sticker = GetContent(message);
            if (sticker == null)
            {
                return;
            }

            if (sticker.Thumbnail != null && sticker.Thumbnail.File.Id == file.Id)
            {
                UpdateThumbnail(message, file);
                return;
            }
            else if (sticker.StickerValue.Id != file.Id)
            {
                return;
            }

            if (file.Local.IsDownloadingCompleted)
            {
                Player.IsLoopingEnabled = message.Content is MessageSticker && SettingsService.Current.Stickers.IsLoopingEnabled;
                Player.Source = new Uri("file:///" + file.Local.Path);
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                message.ProtoService.DownloadFile(file.Id, 1);
            }
        }

        private void UpdateThumbnail(MessageViewModel message, File file)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                Player.Thumbnail = PlaceholderHelper.GetWebPFrame(file.Local.Path);
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                message.ProtoService.DownloadFile(file.Id, 1);
            }
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            // We can't recycle it as we must destroy CanvasAnimatedControl on Unload.
            if (Player.IsUnloaded)
            {
                return false;
            }

            if (content is MessageSticker sticker)
            {
                return sticker.Sticker.IsAnimated;
            }
            else if (content is MessageText text && text.WebPage != null && !primary)
            {
                return text.WebPage.Sticker != null && text.WebPage.Sticker.IsAnimated;
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

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var sticker = GetContent(_message);
            if (sticker == null)
            {
                return;
            }

            if (_message.Content is MessageText)
            {
                Player.Play();
            }
            else
            {
                _message.Delegate.OpenSticker(sticker);
            }
        }
    }
}
