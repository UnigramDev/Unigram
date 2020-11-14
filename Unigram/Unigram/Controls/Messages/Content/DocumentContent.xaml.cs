using System;
using Telegram.Td.Api;
using Unigram.Converters;
using Unigram.ViewModels;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Controls.Messages.Content
{
    public enum MessageContentState
    {
        None,
        Download,
        Downloading,
        Uploading,
        Confirm,
        Document,
        Photo,
        Animation,
        Ttl,
        Play,
        Pause,
        Theme,
    }

    public sealed partial class DocumentContent : Grid, IContentWithFile
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        public DocumentContent(MessageViewModel message)
        {
            InitializeComponent();
            UpdateMessage(message);
        }

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;

            var document = GetContent(message.Content);
            if (document == null)
            {
                return;
            }

            Title.Text = document.FileName;

            if (document.Thumbnail != null)
            {
                UpdateThumbnail(message, document.Thumbnail, document.Thumbnail.File);
            }
            else
            {
                Texture.Background = null;
                Button.Style = App.Current.Resources["InlineFileButtonStyle"] as Style;
            }

            UpdateFile(message, document.DocumentValue);
        }

        public void UpdateMessageContentOpened(MessageViewModel message)
        {
            if (message.Ttl > 0)
            {
                //Timer.Maximum = message.Ttl;
                //Timer.Value = DateTime.Now.AddSeconds(message.TtlExpiresIn);
            }
        }

        public void UpdateFile(MessageViewModel message, File file)
        {
            var document = GetContent(message.Content);
            if (document == null)
            {
                return;
            }

            if (document.Thumbnail != null && document.Thumbnail.File.Id == file.Id)
            {
                UpdateThumbnail(message, document.Thumbnail, file);
                return;
            }
            else if (document.DocumentValue.Id != file.Id)
            {
                return;
            }

            var size = Math.Max(file.Size, file.ExpectedSize);
            if (file.Local.IsDownloadingActive)
            {
                Button.SetGlyph(file.Id, MessageContentState.Downloading);
                Button.Progress = (double)file.Local.DownloadedSize / size;

                Subtitle.Text = string.Format("{0} / {1}", FileSizeConverter.Convert(file.Local.DownloadedSize, size), FileSizeConverter.Convert(size));
            }
            else if (file.Remote.IsUploadingActive || message.SendingState is MessageSendingStateFailed)
            {
                Button.SetGlyph(file.Id, MessageContentState.Uploading);
                Button.Progress = (double)file.Remote.UploadedSize / size;

                Subtitle.Text = string.Format("{0} / {1}", FileSizeConverter.Convert(file.Remote.UploadedSize, size), FileSizeConverter.Convert(size));
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
            {
                Button.SetGlyph(file.Id, MessageContentState.Download);
                Button.Progress = 0;

                Subtitle.Text = FileSizeConverter.Convert(size);

                if (message.Delegate.CanBeDownloaded(message))
                {
                    _message.ProtoService.DownloadFile(file.Id, 32);
                }
            }
            else
            {
                var theme = document.FileName.EndsWith(".unigram-theme");
                if (theme)
                {
                    Button.SetGlyph(file.Id, message.SendingState is MessageSendingStatePending && message.MediaAlbumId != 0 ? MessageContentState.Confirm : MessageContentState.Theme);
                }
                else
                {
                    Button.SetGlyph(file.Id, message.SendingState is MessageSendingStatePending && message.MediaAlbumId != 0 ? MessageContentState.Confirm : MessageContentState.Document);
                }
                Button.Progress = 1;

                Subtitle.Text = FileSizeConverter.Convert(size);
            }
        }

        private void UpdateThumbnail(MessageViewModel message, Thumbnail thumbnail, File file)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                double ratioX = (double)48 / thumbnail.Width;
                double ratioY = (double)48 / thumbnail.Height;
                double ratio = Math.Max(ratioX, ratioY);

                var width = (int)(thumbnail.Width * ratio);
                var height = (int)(thumbnail.Height * ratio);

                Texture.Background = new ImageBrush { ImageSource = new BitmapImage(new Uri("file:///" + file.Local.Path)) { DecodePixelWidth = width, DecodePixelHeight = height }, Stretch = Stretch.UniformToFill, AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center };
                Button.Style = App.Current.Resources["ImmersiveFileButtonStyle"] as Style;
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                message.ProtoService.DownloadFile(file.Id, 1);

                Texture.Background = null;
                Button.Style = App.Current.Resources["InlineFileButtonStyle"] as Style;
            }
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            if (content is MessageDocument document)
            {
                return true;
            }
            else if (content is MessageText text && text.WebPage != null && !primary)
            {
                return text.WebPage.Document != null && !string.Equals(text.WebPage.Type, "telegram_background", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private Document GetContent(MessageContent content)
        {
            if (content is MessageDocument document)
            {
                return document.Document;
            }
            else if (content is MessageText text && text.WebPage != null)
            {
                return text.WebPage.Document;
            }

            return null;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var document = GetContent(_message.Content);
            if (document == null)
            {
                return;
            }

            var file = document.DocumentValue;
            if (file.Local.IsDownloadingActive)
            {
                _message.ProtoService.CancelDownloadFile(file.Id);
            }
            else if (file.Remote.IsUploadingActive || _message.SendingState is MessageSendingStateFailed)
            {
                _message.ProtoService.Send(new DeleteMessages(_message.ChatId, new[] { _message.Id }, true));
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && !file.Local.IsDownloadingCompleted)
            {
                _message.ProtoService.DownloadFile(file.Id, 32);
            }
            else
            {
                _message.Delegate.OpenFile(file);
            }
        }

        private async void Button_DragStarting(UIElement sender, DragStartingEventArgs args)
        {
            var document = GetContent(_message.Content);
            if (document == null)
            {
                return;
            }

            var file = document.DocumentValue;
            if (file.Local.IsDownloadingCompleted)
            {
                var item = await StorageFile.GetFileFromPathAsync(file.Local.Path);

                args.AllowedOperations = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
                args.Data.SetStorageItems(new[] { item });
                args.DragUI.SetContentFromDataPackage();
            }
        }
    }
}
