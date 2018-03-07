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
    public sealed partial class VideoNoteContent : AspectView, IContentWithFile
    {
        private MessageViewModel _message;

        public VideoNoteContent(MessageViewModel message)
        {
            InitializeComponent();
            UpdateMessage(message);
        }

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;

            var videoNote = GetContent(message.Content);
            if (videoNote == null)
            {
                return;
            }

            Constraint = message;
            Texture.ImageSource = null;

            if (message.Content is MessageVideoNote videoNoteMessage)
            {
                Subtitle.Text = videoNote.GetDuration() + (videoNoteMessage.IsViewed ? string.Empty : " ●");
            }
            else
            {
                Subtitle.Text = videoNote.GetDuration();
            }

            if (videoNote.Thumbnail != null)
            {
                UpdateThumbnail(message, videoNote.Thumbnail.Photo);
            }

            UpdateFile(message, videoNote.Video);
        }

        public void UpdateMessageContentOpened(MessageViewModel message)
        {
            if (message.Content is MessageVideoNote videoNote)
            {
                Subtitle.Text = videoNote.VideoNote.GetDuration() + (videoNote.IsViewed ? string.Empty : " ●");
            }
        }

        public void UpdateFile(MessageViewModel message, File file)
        {
            var videoNote = GetContent(message.Content);
            if (videoNote == null)
            {
                return;
            }

            if (videoNote.Thumbnail != null && videoNote.Thumbnail.Photo.Id == file.Id)
            {
                UpdateThumbnail(message, file);
                return;
            }
            else if (videoNote.Video.Id != file.Id)
            {
                return;
            }

            var size = Math.Max(file.Size, file.ExpectedSize);
            if (file.Local.IsDownloadingActive)
            {
                Button.Glyph = "\uE10A";
                Button.Progress = (double)file.Local.DownloadedSize / size;
            }
            else if (file.Remote.IsUploadingActive)
            {

                Button.Glyph = "\uE10A";
                Button.Progress = (double)file.Remote.UploadedSize / size;
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
            {
                Button.Glyph = "\uE118";
                Button.Progress = 0;

                if (message.Delegate.CanBeDownloaded(message))
                {
                    _message.ProtoService.Send(new DownloadFile(file.Id, 32));
                }
            }
            else
            {
                if (message.IsSecret())
                {
                    Button.Glyph = "\uE60D";
                    Button.Progress = 1;
                }
                else
                {
                    Button.Glyph = "\uE102";
                    Button.Progress = 1;
                }
            }
        }

        private void UpdateThumbnail(MessageViewModel message, File file)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                //Texture.Source = new BitmapImage(new Uri("file:///" + file.Local.Path));
                Texture.ImageSource = PlaceholderHelper.GetBlurred(file.Local.Path);
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                message.ProtoService.Send(new DownloadFile(file.Id, 1));
            }
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            if (content is MessageVideoNote)
            {
                return true;
            }
            else if (content is MessageText text && text.WebPage != null && !primary)
            {
                return text.WebPage.VideoNote != null;
            }

            return false;
        }

        private VideoNote GetContent(MessageContent content)
        {
            if (content is MessageVideoNote videoNote)
            {
                return videoNote.VideoNote;
            }
            else if (content is MessageText text && text.WebPage != null)
            {
                return text.WebPage.VideoNote;
            }

            return null;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var videoNote = GetContent(_message.Content);
            if (videoNote == null)
            {
                return;
            }

            var file = videoNote.Video;
            if (file.Local.IsDownloadingActive)
            {
                _message.ProtoService.Send(new CancelDownloadFile(file.Id, false));
            }
            else if (file.Remote.IsUploadingActive)
            {
                _message.ProtoService.Send(new DeleteMessages(_message.ChatId, new[] { _message.Id }, true));
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && !file.Local.IsDownloadingCompleted)
            {
                _message.ProtoService.Send(new DownloadFile(file.Id, 1));
            }
            else
            {
                _message.Delegate.OpenMedia(_message, this);
            }
        }
    }
}
