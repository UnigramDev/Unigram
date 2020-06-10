using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Controls.Messages.Content
{
    public sealed partial class DocumentPhotoContent : AspectView, IContentWithFile
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        public DocumentPhotoContent(MessageViewModel message)
        {
            InitializeComponent();
            UpdateMessage(message);
        }

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;

            var photo = GetContent(message.Content);
            if (photo == null)
            {
                return;
            }

            Constraint = message;
            Background = null;
            Texture.Source = null;

            //UpdateMessageContentOpened(message);

            var small = photo.Thumbnail;
            var big = photo.DocumentValue;

            if (small != null /*&& small.Photo.Id != big.Photo.Id*/)
            {
                //UpdateThumbnail(message, small.Photo);
            }

            UpdateFile(message, small.File);

            if (message.Content is MessageText text && Uri.TryCreate(text.WebPage?.Url, UriKind.Absolute, out Uri result))
            {
                var background = TdBackground.FromUri(result);
                if (background is BackgroundTypeFill fill)
                {
                    Background = fill.ToBrush();
                    Texture.Opacity = 1;
                }
                else if (background is BackgroundTypePattern pattern)
                {
                    Background = pattern.ToBrush();
                    Texture.Opacity = pattern.Intensity / 100d;
                }
                else if (background is BackgroundTypeWallpaper wallpaper)
                {
                    Background = null;
                    Texture.Opacity = 1;
                }
            }
        }

        public void UpdateMessageContentOpened(MessageViewModel message)
        {
            if (message.Ttl > 0)
            {
                Timer.Maximum = message.Ttl;
                Timer.Value = DateTime.Now.AddSeconds(message.TtlExpiresIn);
            }
        }

        public void UpdateFile(MessageViewModel message, File file)
        {
            var photo = GetContent(message.Content);
            if (photo == null)
            {
                return;
            }

            var small = photo.Thumbnail;
            var big = photo.DocumentValue;

            //if (small != null && small.Photo.Id != big.Id && small.Photo.Id == file.Id)
            //{
            //    UpdateThumbnail(message, file);
            //    return;
            //}
            //else if (big == null || big.Id != file.Id)
            //{
            //    return;
            //}

            var size = Math.Max(file.Size, file.ExpectedSize);
            if (file.Local.IsDownloadingActive)
            {
                //Button.Glyph = Icons.Cancel;
                Button.SetGlyph(file.Id, MessageContentState.Downloading);
                Button.Progress = (double)file.Local.DownloadedSize / size;

                Button.Opacity = 1;
                Overlay.Opacity = 0;
            }
            else if (file.Remote.IsUploadingActive || message.SendingState is MessageSendingStateFailed)
            {
                //Button.Glyph = Icons.Cancel;
                Button.SetGlyph(file.Id, MessageContentState.Uploading);
                Button.Progress = (double)file.Remote.UploadedSize / size;

                Button.Opacity = 1;
                Overlay.Opacity = 0;
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
            {
                //Button.Glyph = Icons.Download;
                Button.SetGlyph(file.Id, MessageContentState.Download);
                Button.Progress = 0;

                Button.Opacity = 1;
                Overlay.Opacity = 0;

                if (message.Delegate.CanBeDownloaded(message))
                {
                    _message.ProtoService.DownloadFile(file.Id, 32);
                }
            }
            else
            {
                if (message.IsSecret())
                {
                    //Button.Glyph = Icons.Ttl;
                    Button.SetGlyph(file.Id, MessageContentState.Ttl);
                    Button.Progress = 1;

                    Button.Opacity = 1;
                    Overlay.Opacity = 1;

                    Subtitle.Text = Locale.FormatTtl(message.Ttl, true);
                }
                else
                {
                    //Button.Glyph = message.SendingState is MessageSendingStatePending ? Icons.Confirm : Icons.Play;
                    Button.SetGlyph(file.Id, message.SendingState is MessageSendingStatePending && message.MediaAlbumId != 0 ? MessageContentState.Confirm : MessageContentState.Play);
                    Button.Progress = 1;

                    if (message.Content is MessageText text && text.WebPage?.EmbedUrl?.Length > 0 || (message.SendingState is MessageSendingStatePending && message.MediaAlbumId != 0))
                    {
                        Button.Opacity = 1;
                    }
                    else
                    {
                        Button.Opacity = 0;
                    }

                    Overlay.Opacity = 0;

                    Texture.Source = new BitmapImage(new Uri("file:///" + file.Local.Path));
                }
            }
        }

        private void UpdateThumbnail(MessageViewModel message, File file)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                //Background = new ImageBrush { ImageSource = new BitmapImage(new Uri("file:///" + file.Local.Path)), Stretch = Stretch.UniformToFill };
                Background = new ImageBrush { ImageSource = PlaceholderHelper.GetBlurred(file.Local.Path, message.IsSecret() ? 15 : 3), Stretch = Stretch.UniformToFill };
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                message.ProtoService.DownloadFile(file.Id, 1);
            }
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            if (content is MessageText text && text.WebPage != null && !primary)
            {
                return text.WebPage.Document != null && string.Equals(text.WebPage.Type, "telegram_background", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private Document GetContent(MessageContent content)
        {
            if (content is MessageText text && text.WebPage != null)
            {
                return text.WebPage.Document;
            }

            return null;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var photo = GetContent(_message?.Content);
            if (photo == null)
            {
                return;
            }

            var big = photo.DocumentValue;
            if (big == null)
            {
                return;
            }

            if (_message.SendingState is MessageSendingStatePending)
            {
                return;
            }

            var file = big;
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
                if (_message.Content is MessageText text && text.WebPage?.EmbedUrl?.Length > 0)
                {
                    _message.Delegate.OpenUrl(text.WebPage.Url, false);
                    //await EmbedUrlView.GetForCurrentView().ShowAsync(_message, text.WebPage, () => this);
                }
                else
                {
                    _message.Delegate.OpenMedia(_message, this);
                }
            }
        }
    }
}
