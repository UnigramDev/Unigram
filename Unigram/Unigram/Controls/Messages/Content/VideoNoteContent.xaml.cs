using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Unigram.Controls.Messages.Content
{
    public sealed class VideoNoteContent : Control, IContentWithFile, IContentWithMask, IContentWithPlayback
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        private string _fileToken;
        private string _thumbnailToken;

        public VideoNoteContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(VideoNoteContent);
        }

        #region InitializeComponent

        private AspectView LayoutRoot;
        private Ellipse Holder;
        private ImageBrush Texture;
        private FileButton Button;
        private AnimationView Player;
        private Border Overlay;
        private TextBlock Subtitle;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            LayoutRoot = GetTemplateChild(nameof(LayoutRoot)) as AspectView;
            Holder = GetTemplateChild(nameof(Holder)) as Ellipse;
            Texture = GetTemplateChild(nameof(Texture)) as ImageBrush;
            Button = GetTemplateChild(nameof(Button)) as FileButton;
            Player = GetTemplateChild(nameof(Player)) as AnimationView;
            Overlay = GetTemplateChild(nameof(Overlay)) as Border;
            Subtitle = GetTemplateChild(nameof(Subtitle)) as TextBlock;

            Button.Click += Button_Click;

            _templateApplied = true;

            if (_message != null)
            {
                UpdateMessage(_message);
            }
        }

        #endregion

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;

            var videoNote = GetContent(message.Content);
            if (videoNote == null || !_templateApplied)
            {
                return;
            }

            LayoutRoot.Constraint = message;
            Texture.ImageSource = null;

            if (message.Content is MessageVideoNote videoNoteMessage)
            {
                Subtitle.Text = videoNote.GetDuration() + (videoNoteMessage.IsViewed ? string.Empty : " ●");
            }
            else
            {
                Subtitle.Text = videoNote.GetDuration();
            }

            UpdateThumbnail(message, videoNote, videoNote.Thumbnail?.File, true);

            UpdateManager.Subscribe(this, message, videoNote.Video, ref _fileToken, UpdateFile);
            UpdateFile(message, videoNote.Video);
        }

        public void UpdateMessageContentOpened(MessageViewModel message)
        {
            if (message.Content is MessageVideoNote videoNote)
            {
                Subtitle.Text = videoNote.VideoNote.GetDuration() + (videoNote.IsViewed ? string.Empty : " ●");
            }
        }

        private void UpdateFile(object target, File file)
        {
            UpdateFile(_message, file);
        }

        private void UpdateFile(MessageViewModel message, File file)
        {
            var videoNote = GetContent(message.Content);
            if (videoNote == null || !_templateApplied)
            {
                return;
            }

            if (videoNote.Video.Id != file.Id)
            {
                return;
            }

            var size = Math.Max(file.Size, file.ExpectedSize);
            if (file.Local.IsDownloadingActive)
            {
                //Button.Glyph = Icons.Cancel;
                Button.SetGlyph(file.Id, MessageContentState.Downloading);
                Button.Progress = (double)file.Local.DownloadedSize / size;

                Player.Source = null;
            }
            else if (file.Remote.IsUploadingActive || message.SendingState is MessageSendingStateFailed)
            {
                //Button.Glyph = Icons.Cancel;
                Button.SetGlyph(file.Id, MessageContentState.Uploading);
                Button.Progress = (double)file.Remote.UploadedSize / size;

                Player.Source = null;
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
            {
                //Button.Glyph = Icons.Download;
                Button.SetGlyph(file.Id, MessageContentState.Download);
                Button.Progress = 0;

                Player.Source = null;

                if (message.Delegate.CanBeDownloaded(videoNote, file))
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

                    Player.Source = null;
                }
                else
                {
                    //Button.Glyph = Icons.Play;
                    Button.SetGlyph(file.Id, MessageContentState.Play);
                    Button.Progress = 1;

                    Player.Source = new LocalVideoSource(file);
                    message.Delegate.ViewVisibleMessages(false);
                }
            }
        }

        private void UpdateThumbnail(object target, File file)
        {
            var videoNote = GetContent(_message.Content);
            if (videoNote == null || !_templateApplied)
            {
                return;
            }

            UpdateThumbnail(_message, videoNote, file, false);
        }

        private void UpdateThumbnail(MessageViewModel message, VideoNote videoNote, File file, bool download)
        {
            var thumbnail = videoNote.Thumbnail;
            var minithumbnail = videoNote.Minithumbnail;

            if (thumbnail != null && thumbnail.Format is ThumbnailFormatJpeg)
            {
                if (file.Local.IsDownloadingCompleted)
                {
                    Texture.ImageSource = PlaceholderHelper.GetBlurred(file.Local.Path);
                }
                else if (download)
                {
                    if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                    {
                        if (minithumbnail != null)
                        {
                            Texture.ImageSource = PlaceholderHelper.GetBlurred(minithumbnail.Data);
                        }

                        message.ProtoService.DownloadFile(file.Id, 1);
                    }

                    UpdateManager.Subscribe(this, message, file, ref _thumbnailToken, UpdateThumbnail, true);
                }
            }
            else if (minithumbnail != null)
            {
                Texture.ImageSource = PlaceholderHelper.GetBlurred(minithumbnail.Data);
            }
            else
            {
                Texture.ImageSource = null;
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

        public IPlayerView GetPlaybackElement()
        {
            return Player;
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
                if (_message.Content is not MessageVideoNote)
                {
                    _message.ProtoService.DownloadFile(file.Id, 30);
                }
                else
                {
                    _message.ProtoService.AddFileToDownloads(file.Id, _message.ChatId, _message.Id);
                }
            }
            else
            {
                _message.Delegate.OpenMedia(_message, this);
            }
        }
    }
}
