using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Native;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Streams;
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
    public sealed partial class VideoContent : AspectView, IContentWithFile
    {
        private MessageViewModel _message;

        public VideoContent(MessageViewModel message)
        {
            InitializeComponent();
            UpdateMessage(message);
        }

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;

            var video = GetContent(message.Content);
            if (video == null)
            {
                return;
            }

            Constraint = message;
            Texture.Source = null;

            if (video.Thumbnail != null)
            {
                UpdateThumbnail(message, video.Thumbnail.Photo);
            }

            UpdateFile(message, video.VideoValue);
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
            var video = GetContent(message.Content);
            if (video == null)
            {
                return;
            }

            if (video.Thumbnail != null && video.Thumbnail.Photo.Id == file.Id)
            {
                UpdateThumbnail(message, file);
                return;
            }
            else if (video.VideoValue.Id != file.Id)
            {
                return;
            }

            var size = Math.Max(file.Size, file.ExpectedSize);
            if (file.Local.IsDownloadingActive)
            {
                Button.Glyph = "\uE10A";
                Button.Progress = (double)file.Local.DownloadedSize / size;

                Subtitle.Text = string.Format("{0} / {1}", FileSizeConverter.Convert(file.Local.DownloadedSize, size), FileSizeConverter.Convert(size));
            }
            else if (file.Remote.IsUploadingActive || message.SendingState is MessageSendingStateFailed)
            {

                Button.Glyph = "\uE10A";
                Button.Progress = (double)file.Remote.UploadedSize / size;

                Subtitle.Text = string.Format("{0} / {1}", FileSizeConverter.Convert(file.Remote.UploadedSize, size), FileSizeConverter.Convert(size));
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
            {
                Button.Glyph = "\uE118";
                Button.Progress = 0;

                Subtitle.Text = video.GetDuration() + ", " + FileSizeConverter.Convert(size);

                if (message.Delegate.CanBeDownloaded(message))
                {
                    _message.ProtoService.Send(new DownloadFile(file.Id, 32, 0));
                }
            }
            else
            {
                if (message.IsSecret())
                {
                    Button.Glyph = "\uE60D";
                    Button.Progress = 1;

                    Subtitle.Text = Locale.FormatTtl(Math.Max(message.Ttl, video.Duration), true);
                }
                else
                {
                    Button.Glyph = "\uE102";
                    Button.Progress = 1;

                    Subtitle.Text = video.GetDuration();
                }
            }
        }

        private void UpdateThumbnail(MessageViewModel message, File file)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                //Texture.Source = new BitmapImage(new Uri("file:///" + file.Local.Path));
                Texture.Source = PlaceholderHelper.GetBlurred(file.Local.Path);
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                message.ProtoService.Send(new DownloadFile(file.Id, 1, 0));
            }
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            if (content is MessageVideo)
            {
                return true;
            }
            else if (content is MessageText text && text.WebPage != null && !primary)
            {
                return text.WebPage.Video != null;
            }

            return false;
        }

        private Video GetContent(MessageContent content)
        {
            if (content is MessageVideo video)
            {
                return video.Video;
            }
            else if (content is MessageText text && text.WebPage != null)
            {
                return text.WebPage.Video;
            }

            return null;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var video = GetContent(_message.Content);
            if (video == null)
            {
                return;
            }

            var file = video.VideoValue;
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
                _message.ProtoService.Send(new DownloadFile(file.Id, 32, 0));
            }
            else
            {
                _message.Delegate.OpenMedia(_message, this);
            }
        }
    }
}
