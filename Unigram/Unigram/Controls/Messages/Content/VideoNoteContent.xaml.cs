using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Shapes;

namespace Unigram.Controls.Messages.Content
{
    public sealed partial class VideoNoteContent : AspectView, IContentWithFile, IContentWithMask
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

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
                UpdateThumbnail(message, videoNote.Thumbnail, videoNote.Thumbnail.File);
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

            if (videoNote.Thumbnail != null && videoNote.Thumbnail.File.Id == file.Id)
            {
                UpdateThumbnail(message, videoNote.Thumbnail, file);
                return;
            }
            else if (videoNote.Video.Id != file.Id)
            {
                return;
            }

            var size = Math.Max(file.Size, file.ExpectedSize);
            if (file.Local.IsDownloadingActive)
            {
                //Button.Glyph = Icons.Cancel;
                Button.SetGlyph(file.Id, MessageContentState.Downloading);
                Button.Progress = (double)file.Local.DownloadedSize / size;
            }
            else if (file.Remote.IsUploadingActive || message.SendingState is MessageSendingStateFailed)
            {
                //Button.Glyph = Icons.Cancel;
                Button.SetGlyph(file.Id, MessageContentState.Uploading);
                Button.Progress = (double)file.Remote.UploadedSize / size;
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
            {
                //Button.Glyph = Icons.Download;
                Button.SetGlyph(file.Id, MessageContentState.Download);
                Button.Progress = 0;

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
                }
                else
                {
                    //Button.Glyph = Icons.Play;
                    Button.SetGlyph(file.Id, MessageContentState.Play);
                    Button.Progress = 1;
                }
            }
        }

        private void UpdateThumbnail(MessageViewModel message, Thumbnail thumbnail, File file)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                //Texture.Source = new BitmapImage(new Uri("file:///" + file.Local.Path));
                Texture.ImageSource = PlaceholderHelper.GetBlurred(file.Local.Path);
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                message.ProtoService.DownloadFile(file.Id, 1);
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

        public CompositionBrush GetAlphaMask()
        {
            if (Holder is Shape shape)
            {
                return shape.GetAlphaMask();
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
                _message.Delegate.OpenMedia(_message, this);
            }
        }
    }
}
