using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Controls.Messages.Content
{
    public sealed class VideoContent : Control, IContentWithFile, IPlayerView
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        private RemoteVideoSource _source;

        private string _fileToken;
        private string _thumbnailToken;

        public VideoContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(VideoContent);
        }

        #region InitializeComponent

        private AspectView LayoutRoot;
        private Image Texture;
        private FileButton Button;
        private AnimationView Player;
        private FileButton Overlay;
        private TextBlock Subtitle;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            LayoutRoot = GetTemplateChild(nameof(LayoutRoot)) as AspectView;
            Texture = GetTemplateChild(nameof(Texture)) as Image;
            Button = GetTemplateChild(nameof(Button)) as FileButton;
            Player = GetTemplateChild(nameof(Player)) as AnimationView;
            Overlay = GetTemplateChild(nameof(Overlay)) as FileButton;
            Subtitle = GetTemplateChild(nameof(Subtitle)) as TextBlock;

            Button.Click += Play_Click;
            Player.PositionChanged += Player_PositionChanged;
            Overlay.Click += Button_Click;

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

            var video = GetContent(message.Content);
            if (video == null || !_templateApplied)
            {
                return;
            }

            LayoutRoot.Constraint = message;
            Texture.Source = null;

            UpdateThumbnail(message, video, video.Thumbnail?.File, true);

            UpdateManager.Subscribe(this, message, video.VideoValue, ref _fileToken, UpdateFile);
            UpdateFile(message, video.VideoValue);
        }

        public void UpdateMessageContentOpened(MessageViewModel message)
        {
            if (message.SelfDestructTime > 0)
            {
                //Timer.Maximum = message.Ttl;
                //Timer.Value = DateTime.Now.AddSeconds(message.TtlExpiresIn);
            }
        }

        private void UpdateFile(object target, File file)
        {
            UpdateFile(_message, file);
        }

        private void UpdateFile(MessageViewModel message, File file)
        {
            var video = GetContent(message.Content);
            if (video == null || !_templateApplied)
            {
                return;
            }

            if (video.VideoValue.Id != file.Id)
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

                    Subtitle.Text = string.Format("{0}, {1}", Locale.FormatTtl(message.SelfDestructTime, true), FileSizeConverter.Convert(size));

                    if (message.Delegate.CanBeDownloaded(video, file))
                    {
                        _message.ClientService.DownloadFile(file.Id, 32);
                    }
                }
                else
                {
                    Button.SetGlyph(file.Id, MessageContentState.Ttl);
                    Button.Progress = 1;

                    Subtitle.Text = Locale.FormatTtl(message.SelfDestructTime, true);
                }
            }
            else
            {
                var size = Math.Max(file.Size, file.ExpectedSize);
                if (file.Local.IsDownloadingActive)
                {
                    if (message.Delegate.CanBeDownloaded(video, file))
                    {
                        UpdateSource(message, file, video.Duration);
                    }

                    Button.SetGlyph(file.Id, MessageContentState.Play);
                    Button.Progress = 0;
                    Overlay.SetGlyph(file.Id, MessageContentState.Downloading);
                    Overlay.Progress = (double)file.Local.DownloadedSize / size;
                    Overlay.ProgressVisibility = Visibility.Visible;

                    if (_source == null)
                    {
                        Subtitle.Text = video.GetDuration() + Environment.NewLine + string.Format("{0} / {1}", FileSizeConverter.Convert(file.Local.DownloadedSize, size), FileSizeConverter.Convert(size));
                    }
                }
                else if (file.Remote.IsUploadingActive || message.SendingState is MessageSendingStateFailed)
                {
                    var generating = file.Local.DownloadedSize < size;

                    UpdateSource(null, null, 0);

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

                    if (message.Delegate.CanBeDownloaded(video, file))
                    {
                        _message.ClientService.DownloadFile(file.Id, 32);
                        UpdateSource(message, file, video.Duration);
                    }
                    else
                    {
                        UpdateSource(null, null, 0);
                    }
                }
                else
                {
                    UpdateSource(message, file, video.Duration);

                    Button.SetGlyph(file.Id, message.SendingState is MessageSendingStatePending && message.MediaAlbumId != 0 ? MessageContentState.Confirm : MessageContentState.Play);
                    Button.Progress = 0;
                    Overlay.Progress = 1;
                    Overlay.ProgressVisibility = Visibility.Collapsed;

                    Subtitle.Text = video.GetDuration();
                }
            }
        }

        private void UpdateThumbnail(object target, File file)
        {
            var video = GetContent(_message.Content);
            if (video == null || !_templateApplied)
            {
                return;
            }

            UpdateThumbnail(_message, video, file, false);
        }

        private async void UpdateThumbnail(MessageViewModel message, Video video, File file, bool download)
        {
            ImageSource source = null;
            Image brush = Texture;

            if (video.Thumbnail != null && video.Thumbnail.Format is ThumbnailFormatJpeg)
            {
                if (file.Local.IsDownloadingCompleted)
                {
                    if (message.IsSecret())
                    {
                        source = await PlaceholderHelper.GetBlurredAsync(file.Local.Path, 15);
                    }
                    else
                    {
                        source = new BitmapImage(UriEx.ToLocal(file.Local.Path));
                    }
                }
                else if (download)
                {
                    if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                    {
                        if (video.Minithumbnail != null)
                        {
                            source = await PlaceholderHelper.GetBlurredAsync(video.Minithumbnail.Data, message.IsSecret() ? 15 : 3);
                        }

                        message.ClientService.DownloadFile(file.Id, 1);
                    }

                    UpdateManager.Subscribe(this, message, file, ref _thumbnailToken, UpdateThumbnail, true);
                }
            }
            else if (video.Minithumbnail != null)
            {
                source = await PlaceholderHelper.GetBlurredAsync(video.Minithumbnail.Data, message.IsSecret() ? 15 : 3);
            }

            brush.Source = source;
        }

        private void UpdateSource(MessageViewModel message, File file, int duration)
        {
            if (message?.Delegate == null || file == null || !message.Delegate.Settings.IsAutoPlayVideosEnabled)
            {
                Player.Source = _source = null;
            }
            else
            {
                if (_source?.Id != file.Id)
                {
                    Player.Source = _source = new RemoteVideoSource(message.ClientService, file, duration);
                    message.Delegate.ViewVisibleMessages(false);
                }
            }
        }

        private void Player_PositionChanged(object sender, int seconds)
        {
            var video = GetContent(_message?.Content);
            if (video == null)
            {
                return;
            }

            var position = TimeSpan.FromSeconds(video.Duration - seconds);
            if (position.TotalHours >= 1)
            {
                Subtitle.Text = position.ToString("h\\:mm\\:ss");
            }
            else
            {
                Subtitle.Text = position.ToString("mm\\:ss");
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
            else if (content is MessageInvoice invoice && invoice.ExtendedMedia is MessageExtendedMediaVideo)
            {
                return true;
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
            else if (content is MessageInvoice invoice && invoice.ExtendedMedia is MessageExtendedMediaVideo extendedMedia)
            {
                return extendedMedia.Video;
            }

            return null;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (_message.IsSecret())
            {
                return;
            }

            var video = GetContent(_message.Content);
            if (video == null)
            {
                return;
            }

            var file = video.VideoValue;
            if (file.Local.IsDownloadingActive)
            {
                _message.ClientService.CancelDownloadFile(file.Id);
            }
            else if (file.Remote.IsUploadingActive || _message.SendingState is MessageSendingStateFailed)
            {
                _message.ClientService.Send(new DeleteMessages(_message.ChatId, new[] { _message.Id }, true));
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && !file.Local.IsDownloadingCompleted)
            {
                if (_message.Content is not MessageVideo)
                {
                    _message.ClientService.DownloadFile(file.Id, 30);
                }
                else
                {
                    _message.ClientService.AddFileToDownloads(file.Id, _message.ChatId, _message.Id);
                }
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
                    _message.ClientService.CancelDownloadFile(file.Id);
                }
                else if (file.Remote.IsUploadingActive || _message.SendingState is MessageSendingStateFailed)
                {
                    _message.ClientService.Send(new DeleteMessages(_message.ChatId, new[] { _message.Id }, true));
                }
                else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && !file.Local.IsDownloadingCompleted)
                {
                    if (_message.Content is not MessageVideo)
                    {
                        _message.ClientService.DownloadFile(file.Id, 30);
                    }
                    else
                    {
                        _message.ClientService.AddFileToDownloads(file.Id, _message.ChatId, _message.Id);
                    }
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
                    _message.ClientService.Send(new DeleteMessages(_message.ChatId, new[] { _message.Id }, true));
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

        #region IPlaybackView

        public bool IsLoopingEnabled => Player?.IsLoopingEnabled ?? false;

        public bool Play()
        {
            return Player?.Play() ?? false;
        }

        public void Pause()
        {
            Player?.Pause();
        }

        public void Unload()
        {
            Player?.Unload();
        }

        #endregion
    }
}
