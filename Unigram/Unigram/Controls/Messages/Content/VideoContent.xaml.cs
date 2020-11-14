using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.ViewModels;
using Windows.UI.Xaml;

namespace Unigram.Controls.Messages.Content
{
    public sealed partial class VideoContent : AspectView, IContentWithFile
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

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
                UpdateThumbnail(message, video.Thumbnail, video.Thumbnail.File);
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

            if (video.Thumbnail != null && video.Thumbnail.File.Id == file.Id)
            {
                UpdateThumbnail(message, video.Thumbnail, file);
                return;
            }
            else if (video.VideoValue.Id != file.Id)
            {
                return;
            }

            if (message.IsSecret())
            {
                Overlay.ProgressVisibility = Visibility.Collapsed;

                var size = Math.Max(file.Size, file.ExpectedSize);
                if (file.Local.IsDownloadingActive)
                {
                    Button.SetGlyph(file.Id, MessageContentState.Downloading);
                    Button.Progress = (double)file.Local.DownloadedSize / size;

                    Subtitle.Text = string.Format("{0} / {1}", FileSizeConverter.Convert(file.Local.DownloadedSize, size), FileSizeConverter.Convert(size));
                }
                else if (file.Remote.IsUploadingActive || message.SendingState is MessageSendingStateFailed)
                {
                    var generating = file.Local.DownloadedSize < size;

                    Button.SetGlyph(file.Id, MessageContentState.Uploading);
                    Button.Progress = (double)(generating ? file.Local.DownloadedSize : file.Remote.UploadedSize) / size;

                    if (generating)
                    {
                        Subtitle.Text = string.Format("{0}%", file.Local.DownloadedSize);
                    }
                    else
                    {
                        Subtitle.Text = string.Format("{0} / {1}", FileSizeConverter.Convert(file.Remote.UploadedSize, size), FileSizeConverter.Convert(size));
                    }
                }
                else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
                {
                    Button.SetGlyph(file.Id, MessageContentState.Download);
                    Button.Progress = 0;

                    Subtitle.Text = string.Format("{0}, {1}", Locale.FormatTtl(message.Ttl, true), FileSizeConverter.Convert(size));

                    if (message.Delegate.CanBeDownloaded(message))
                    {
                        _message.ProtoService.DownloadFile(file.Id, 32);
                    }
                }
                else
                {
                    Button.SetGlyph(file.Id, MessageContentState.Ttl);
                    Button.Progress = 1;

                    Subtitle.Text = Locale.FormatTtl(message.Ttl, true);
                }
            }
            else
            {
                var size = Math.Max(file.Size, file.ExpectedSize);
                if (file.Local.IsDownloadingActive)
                {
                    Button.SetGlyph(file.Id, MessageContentState.Play);
                    Button.Progress = 0;
                    Overlay.SetGlyph(file.Id, MessageContentState.Downloading);
                    Overlay.Progress = (double)file.Local.DownloadedSize / size;
                    Overlay.ProgressVisibility = Visibility.Visible;

                    Subtitle.Text = video.GetDuration() + Environment.NewLine + string.Format("{0} / {1}", FileSizeConverter.Convert(file.Local.DownloadedSize, size), FileSizeConverter.Convert(size));
                }
                else if (file.Remote.IsUploadingActive || message.SendingState is MessageSendingStateFailed)
                {
                    var generating = file.Local.DownloadedSize < size;

                    Button.SetGlyph(file.Id, MessageContentState.Uploading);
                    Button.Progress = (double)(generating ? file.Local.DownloadedSize : file.Remote.UploadedSize) / size;
                    Overlay.ProgressVisibility = Visibility.Collapsed;

                    if (generating)
                    {
                        Subtitle.Text = video.GetDuration() + Environment.NewLine + string.Format("{0}%", file.Local.DownloadedSize);
                    }
                    else
                    {
                        Subtitle.Text = video.GetDuration() + Environment.NewLine + string.Format("{0} / {1}", FileSizeConverter.Convert(file.Remote.UploadedSize, size), FileSizeConverter.Convert(size));
                    }
                }
                else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
                {
                    Button.SetGlyph(file.Id, MessageContentState.Play);
                    Button.Progress = 0;
                    Overlay.SetGlyph(file.Id, MessageContentState.Download);
                    Overlay.Progress = 0;
                    Overlay.ProgressVisibility = Visibility.Visible;

                    Subtitle.Text = video.GetDuration() + Environment.NewLine + FileSizeConverter.Convert(size);

                    if (message.Delegate.CanBeDownloaded(message))
                    {
                        _message.ProtoService.DownloadFile(file.Id, 32);
                    }
                }
                else
                {
                    Button.SetGlyph(file.Id, message.SendingState is MessageSendingStatePending && message.MediaAlbumId != 0 ? MessageContentState.Confirm : MessageContentState.Play);
                    Button.Progress = 0;
                    Overlay.Progress = 1;
                    Overlay.ProgressVisibility = Visibility.Collapsed;

                    Subtitle.Text = video.GetDuration();
                }
            }
        }

        private void UpdateThumbnail(MessageViewModel message, Thumbnail thumbnail, File file)
        {
            if (file.Local.IsDownloadingCompleted && thumbnail.Format is ThumbnailFormatJpeg)
            {
                //Texture.Source = new BitmapImage(new Uri("file:///" + file.Local.Path));
                Texture.Source = PlaceholderHelper.GetBlurred(file.Local.Path);
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                message.ProtoService.DownloadFile(file.Id, 1);
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

            if (_message.IsSecret())
            {
                return;
            }

            var file = video.VideoValue;
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

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            var video = GetContent(_message.Content);
            if (video == null)
            {
                return;
            }

            if (_message.IsSecret())
            {
                var file = video.VideoValue;
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
            else
            {
                var file = video.VideoValue;
                if (file.Remote.IsUploadingActive || _message.SendingState is MessageSendingStateFailed)
                {
                    _message.ProtoService.Send(new DeleteMessages(_message.ChatId, new[] { _message.Id }, true));
                }
                else
                {
                    if (_message.SendingState is MessageSendingStatePending)
                    {
                        return;
                    }

                    _message.Delegate.OpenMedia(_message, this);
                }
            }
        }
    }
}
