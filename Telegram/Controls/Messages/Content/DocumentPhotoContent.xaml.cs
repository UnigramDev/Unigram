//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Controls.Messages.Content
{
    public sealed partial class DocumentPhotoContent : AspectView, IContent
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
                else if (background is BackgroundTypeWallpaper)
                {
                    Background = null;
                    Texture.Opacity = 1;
                }
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

                if (message.Delegate.CanBeDownloaded(photo, file))
                {
                    _message.ClientService.DownloadFile(file.Id, 32);
                }
            }
            else
            {
                //Button.Glyph = message.SendingState is MessageSendingStatePending ? Icons.Confirm : Icons.Photo;
                Button.SetGlyph(file.Id, message.SendingState is MessageSendingStatePending && message.MediaAlbumId != 0 ? MessageContentState.Confirm : MessageContentState.Photo);
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

                Texture.Source = new BitmapImage(UriEx.ToLocal(file.Local.Path));
            }
        }

        private void UpdateThumbnail(MessageViewModel message, File file)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                //Background = new ImageBrush { ImageSource = new BitmapImage(UriEx.GetLocal(file.Local.Path)), Stretch = Stretch.UniformToFill };
                Background = new ImageBrush { ImageSource = PlaceholderHelper.GetBlurred(file.Local.Path, 3), Stretch = Stretch.UniformToFill };
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                message.ClientService.DownloadFile(file.Id, 1);
            }
        }

        public void Recycle()
        {
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
                _message.ClientService.CancelDownloadFile(file);
            }
            else if (file.Remote.IsUploadingActive || _message.SendingState is MessageSendingStateFailed)
            {
                _message.ClientService.Send(new DeleteMessages(_message.ChatId, new[] { _message.Id }, true));
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && !file.Local.IsDownloadingCompleted)
            {
                if (_message.Content is not MessageDocument)
                {
                    _message.ClientService.DownloadFile(file.Id, 30);
                }
                else
                {
                    _message.ClientService.AddFileToDownloads(file, _message.ChatId, _message.Id);
                }
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
