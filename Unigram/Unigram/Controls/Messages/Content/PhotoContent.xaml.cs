using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Controls.Messages.Content
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PhotoContent : AspectView, IContentWithFile
    {
        private MessageViewModel _message;

        public PhotoContent(MessageViewModel message)
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

            var small = photo.GetSmall();
            var big = photo.GetBig();

            if (small != null /*&& small.Photo.Id != big.Photo.Id*/)
            {
                UpdateThumbnail(message, small.Photo);
            }

            UpdateFile(message, big.Photo);
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

            var small = photo.GetSmall();
            var big = photo.GetBig();

            if (small != null && small.Photo.Id != big.Photo.Id && small.Photo.Id == file.Id)
            {
                UpdateThumbnail(message, file);
                return;
            }
            else if (big == null || big.Photo.Id != file.Id)
            {
                return;
            }

            var size = Math.Max(file.Size, file.ExpectedSize);
            if (file.Local.IsDownloadingActive)
            {
                Button.Glyph = "\uE10A";
                Button.Progress = (double)file.Local.DownloadedSize / size;

                Button.Opacity = 1;
                Overlay.Opacity = 0;
            }
            else if (file.Remote.IsUploadingActive || message.SendingState is MessageSendingStateFailed)
            {

                Button.Glyph = "\uE10A";
                Button.Progress = (double)file.Remote.UploadedSize / size;

                Button.Opacity = 1;
                Overlay.Opacity = 0;
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
            {
                Button.Glyph = "\uE118";
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
                    Button.Glyph = "\uE60D";
                    Button.Progress = 1;

                    Button.Opacity = 1;
                    Overlay.Opacity = 1;

                    Subtitle.Text = Locale.FormatTtl(message.Ttl, true);
                }
                else
                {
                    Button.Glyph = "\uE102";
                    Button.Progress = 1;

                    if (message.Content is MessageText text && text.WebPage?.EmbedUrl?.Length > 0)
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
            if (content is MessagePhoto photo)
            {
                return true;
            }
            else if (content is MessageGame game && !primary)
            {
                return game.Game.Photo != null;
            }
            else if (content is MessageText text && text.WebPage != null && !primary)
            {
                return text.WebPage.IsPhoto();
            }

            return false;
        }

        private Photo GetContent(MessageContent content)
        {
            if (content is MessagePhoto photo)
            {
                return photo.Photo;
            }
            else if (content is MessageGame game)
            {
                return game.Game.Photo;
            }
            else if (content is MessageText text && text.WebPage != null)
            {
                return text.WebPage.Photo;
            }

            return null;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var photo = GetContent(_message.Content);
            if (photo == null)
            {
                return;
            }

            var big = photo.GetBig();
            if (big == null)
            {
                return;
            }

            var file = big.Photo;
            if (file.Local.IsDownloadingActive)
            {
                _message.ProtoService.Send(new CancelDownloadFile(file.Id, false));
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
                }
                else
                {
                    _message.Delegate.OpenMedia(_message, this);
                }
            }
        }
    }
}
