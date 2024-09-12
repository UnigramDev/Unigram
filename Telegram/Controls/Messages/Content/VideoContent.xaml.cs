//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;

namespace Telegram.Controls.Messages.Content
{
    public sealed partial class VideoContent : Control, IContentWithFile, IPlayerView
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        private PaidMediaVideo _paidMedia;

        private long _fileToken;
        private long _thumbnailToken;

        public VideoContent(MessageViewModel message, PaidMediaVideo paidMedia = null)
        {
            _message = message;
            _paidMedia = paidMedia;

            DefaultStyleKey = typeof(VideoContent);
        }

        #region InitializeComponent

        private AutomaticDragHelper ButtonDrag;

        private AspectView LayoutRoot;
        private Image Texture;
        private AnimatedImage Particles;
        private FileButton Button;
        private AnimatedImage Player;
        private FileButton Overlay;
        private TextBlock Subtitle;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            LayoutRoot = GetTemplateChild(nameof(LayoutRoot)) as AspectView;
            Texture = GetTemplateChild(nameof(Texture)) as Image;
            Particles = GetTemplateChild(nameof(Particles)) as AnimatedImage;
            Button = GetTemplateChild(nameof(Button)) as FileButton;
            Player = GetTemplateChild(nameof(Player)) as AnimatedImage;
            Overlay = GetTemplateChild(nameof(Overlay)) as FileButton;
            Subtitle = GetTemplateChild(nameof(Subtitle)) as TextBlock;

            ButtonDrag = new AutomaticDragHelper(Button, true);
            ButtonDrag.StartDetectingDrag();

            Button.Click += Play_Click;
            Button.DragStarting += Button_DragStarting;

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

            var video = GetContent(message, out bool hasSpoiler, out bool isSecret);
            if (video == null || !_templateApplied)
            {
                return;
            }

            LayoutRoot.Constraint = isSecret ? Constants.SecretSize : ((object)_paidMedia ?? message);
            Texture.Source = null;

            UpdateThumbnail(message, video, video.Thumbnail?.File, true, isSecret, hasSpoiler);

            UpdateManager.Subscribe(this, message, video.VideoValue, ref _fileToken, UpdateFile);
            UpdateFile(message, video.VideoValue);
        }

        public void UpdateMessageContentOpened(MessageViewModel message)
        {
            if (message.SelfDestructType is MessageSelfDestructTypeTimer)
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
            var video = GetContent(message, out bool hasSpoiler, out bool isSecret);
            if (video == null || !_templateApplied)
            {
                return;
            }

            if (video.VideoValue.Id != file.Id)
            {
                return;
            }

            if (isSecret)
            {
                Overlay.ProgressVisibility = Visibility.Collapsed;

                var canBeDownloaded = file.Local.CanBeDownloaded
                    && !file.Local.IsDownloadingCompleted
                    && !file.Local.IsDownloadingActive;

                var size = Math.Max(file.Size, file.ExpectedSize);
                if (file.Local.IsDownloadingActive || (canBeDownloaded && message.Delegate.CanBeDownloaded(video, file)))
                {
                    if (canBeDownloaded)
                    {
                        _message.ClientService.DownloadFile(file.Id, 32);
                    }

                    Button.SetGlyph(file.Id, MessageContentState.Downloading);
                    Button.Progress = (double)file.Local.DownloadedSize / size;

                    Subtitle.Text = string.Format("{0} / {1}", FileSizeConverter.Convert(file.Local.DownloadedSize, size), FileSizeConverter.Convert(size));
                }
                else if (file.Remote.IsUploadingActive || message.SendingState is MessageSendingStateFailed || (message.SendingState is MessageSendingStatePending && !file.Remote.IsUploadingCompleted))
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
                else if (canBeDownloaded)
                {
                    Button.SetGlyph(file.Id, MessageContentState.Download);
                    Button.Progress = 0;

                    if (message.SelfDestructType is MessageSelfDestructTypeTimer timer)
                    {
                        Subtitle.Text = string.Format("{0}, {1}", Icons.PlayFilled12 + "\u2004\u200A" + Locale.FormatTtl(timer.SelfDestructTime, true), FileSizeConverter.Convert(size));
                    }
                    else
                    {
                        Subtitle.Text = Icons.ArrowClockwiseFilled12 + "\u2004\u200A1";
                    }
                }
                else
                {
                    Button.SetGlyph(file.Id, MessageContentState.Ttl);
                    Button.Progress = 1;

                    if (message.SelfDestructType is MessageSelfDestructTypeTimer timer)
                    {
                        Subtitle.Text = Icons.PlayFilled12 + "\u2004\u200A" + Locale.FormatTtl(timer.SelfDestructTime, true);
                    }
                    else
                    {
                        Subtitle.Text = Icons.ArrowClockwiseFilled12 + "\u2004\u200A1";
                    }
                }
            }
            else
            {
                var size = Math.Max(file.Size, file.ExpectedSize);
                if (file.Local.IsDownloadingActive)
                {
                    if (!hasSpoiler && message.Delegate.CanBeDownloaded(video, file))
                    {
                        UpdateSource(message, file);
                    }

                    Button.SetGlyph(file.Id, MessageContentState.Play);
                    Button.Progress = 0;
                    Overlay.SetGlyph(file.Id, MessageContentState.Downloading);
                    Overlay.Progress = (double)file.Local.DownloadedSize / size;
                    Overlay.ProgressVisibility = Visibility.Visible;

                    if (Player.Source == null)
                    {
                        Subtitle.Text = video.GetDuration() + Environment.NewLine + string.Format("{0} / {1}", FileSizeConverter.Convert(file.Local.DownloadedSize, size), FileSizeConverter.Convert(size));
                    }
                }
                else if (file.Remote.IsUploadingActive || message.SendingState is MessageSendingStateFailed || (message.SendingState is MessageSendingStatePending && !file.Remote.IsUploadingCompleted))
                {
                    var generating = file.Local.DownloadedSize < size;

                    UpdateSource(null, null);

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

                    if (!hasSpoiler && message.Delegate.CanBeDownloaded(video, file))
                    {
                        _message.ClientService.DownloadFile(file.Id, 32);
                        UpdateSource(message, file);
                    }
                    else
                    {
                        UpdateSource(null, null);
                    }
                }
                else
                {
                    if (!hasSpoiler)
                    {
                        UpdateSource(message, file);
                    }
                    else
                    {
                        UpdateSource(null, null);
                    }

                    Button.SetGlyph(file.Id, message.SendingState is MessageSendingStatePending && message.MediaAlbumId != 0 ? MessageContentState.Confirm : MessageContentState.Play);
                    Button.Progress = 0;
                    Overlay.Progress = 1;
                    Overlay.ProgressVisibility = Visibility.Collapsed;

                    Subtitle.Text = video.GetDuration();
                }
            }

            Button.Opacity = Player.Source == null ? 1 : 0;
        }

        private void UpdateThumbnail(object target, File file)
        {
            var video = GetContent(_message, out bool hasSpoiler, out bool isSecret);
            if (video == null || !_templateApplied)
            {
                return;
            }

            UpdateThumbnail(_message, video, file, false, isSecret, hasSpoiler);
        }

        private void UpdateThumbnail(MessageViewModel message, Video video, File file, bool download, bool isSecret, bool hasSpoiler)
        {
            BitmapImage source = null;
            Image brush = Texture;

            if (video.Thumbnail != null && video.Thumbnail.Format is ThumbnailFormatJpeg)
            {
                if (file.Local.IsDownloadingCompleted)
                {
                    if (isSecret || hasSpoiler)
                    {
                        source = new BitmapImage();
                        PlaceholderHelper.GetBlurred(source, file.Local.Path, 15);
                    }
                    else
                    {
                        source = UriEx.ToBitmap(file.Local.Path);
                    }
                }
                else
                {
                    if (download)
                    {
                        if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                        {
                            message.ClientService.DownloadFile(file.Id, 1);
                        }

                        UpdateManager.Subscribe(this, message, file, ref _thumbnailToken, UpdateThumbnail, true);
                    }

                    if (video.Minithumbnail != null)
                    {
                        source = new BitmapImage();
                        PlaceholderHelper.GetBlurred(source, video.Minithumbnail.Data, isSecret || hasSpoiler ? 15 : 3);
                    }
                }
            }
            else if (video.Minithumbnail != null)
            {
                source = new BitmapImage();
                PlaceholderHelper.GetBlurred(source, video.Minithumbnail.Data, isSecret || hasSpoiler ? 15 : 3);
            }

            brush.Source = source;
            Particles.Source = isSecret || hasSpoiler
                ? new ParticlesImageSource()
                : null;
        }

        private void UpdateSource(MessageViewModel message, File file)
        {
            if (message?.Delegate == null || file == null || !PowerSavingPolicy.AutoPlayVideos)
            {
                Player.Source = null;
            }
            else
            {
                if (Player.Source is not RemoteFileSource remote || remote.Id != file.Id)
                {
                    Player.Source = new RemoteFileSource(message.ClientService, file);
                    message.Delegate.ViewVisibleMessages();
                }
            }
        }

        private void Player_PositionChanged(object sender, AnimatedImagePositionChangedEventArgs e)
        {
            var video = GetContent(_message, out _, out _);
            if (video == null)
            {
                return;
            }

            var position = TimeSpan.FromSeconds(video.Duration - e.Position);
            if (position.TotalHours >= 1)
            {
                Subtitle.Text = position.ToString("h\\:mm\\:ss");
            }
            else
            {
                Subtitle.Text = position.ToString("mm\\:ss");
            }
        }

        public void Recycle()
        {
            _message = null;

            UpdateManager.Unsubscribe(this, ref _fileToken);
            UpdateManager.Unsubscribe(this, ref _thumbnailToken, true);

            if (_templateApplied)
            {
                Player.Source = null;
            }
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            if (content is MessageVideo)
            {
                return true;
            }
            else if (content is MessageText text && text.LinkPreview != null && !primary)
            {
                return text.LinkPreview.Type is LinkPreviewTypeVideo || text.LinkPreview.Type is LinkPreviewTypeAlbum album && album.Media[0] is LinkPreviewAlbumMediaVideo;
            }
            else if (content is MessageInvoice invoice && invoice.PaidMedia is PaidMediaVideo)
            {
                return true;
            }

            return false;
        }

        private Video GetContent(MessageViewModel message, out bool hasSpoiler, out bool isSecret)
        {
            hasSpoiler = false;
            isSecret = false;

            if (message?.Delegate == null)
            {
                return null;
            }

            if (_paidMedia != null)
            {
                return _paidMedia.Video;
            }

            var content = message.GeneratedContent ?? message.Content;
            if (content is MessageVideo video)
            {
                hasSpoiler = video.HasSpoiler;
                isSecret = video.IsSecret;
                return video.Video;
            }
            else if (content is MessageText text)
            {
                if (text.LinkPreview?.Type is LinkPreviewTypeVideo previewVideo)
                {
                    return previewVideo.Video;
                }
                else if (text.LinkPreview?.Type is LinkPreviewTypeAlbum previewAlbum && previewAlbum.Media[0] is LinkPreviewAlbumMediaVideo albumVideo)
                {
                    return albumVideo.Video;
                }
            }
            else if (content is MessageInvoice invoice && invoice.PaidMedia is PaidMediaVideo paidMedia)
            {
                return paidMedia.Video;
            }

            return null;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var video = GetContent(_message, out _, out bool isSecret);
            if (video == null || isSecret)
            {
                return;
            }

            var file = video.VideoValue;
            if (file.Local.IsDownloadingActive)
            {
                _message.ClientService.CancelDownloadFile(file);
            }
            else if (file.Remote.IsUploadingActive || _message.SendingState is MessageSendingStateFailed)
            {
                if (_message.SendingState is MessageSendingStateFailed or MessageSendingStatePending)
                {
                    _message.ClientService.Send(new DeleteMessages(_message.ChatId, new[] { _message.Id }, true));
                }
                else
                {
                    _message.ClientService.Send(new CancelPreliminaryUploadFile(file.Id));
                }
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && !file.Local.IsDownloadingCompleted)
            {
                if (_message.CanBeAddedToDownloads)
                {
                    _message.ClientService.AddFileToDownloads(file, _message.ChatId, _message.Id);
                }
                else
                {
                    _message.ClientService.DownloadFile(file.Id, 30);
                }
            }
            else
            {
                _message.Delegate.OpenMedia(_message, this);
            }
        }

        private void Button_DragStarting(UIElement sender, DragStartingEventArgs args)
        {
            MessageHelper.DragStarting(_message, args);
        }

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            var video = GetContent(_message, out _, out bool isSecret);
            if (video == null)
            {
                return;
            }

            if (isSecret)
            {
                var file = video.VideoValue;
                if (file.Local.IsDownloadingActive)
                {
                    _message.ClientService.CancelDownloadFile(file);
                }
                else if (file.Remote.IsUploadingActive || _message.SendingState is MessageSendingStateFailed)
                {
                    if (_message.SendingState is MessageSendingStateFailed or MessageSendingStatePending)
                    {
                        _message.ClientService.Send(new DeleteMessages(_message.ChatId, new[] { _message.Id }, true));
                    }
                    else
                    {
                        _message.ClientService.Send(new CancelPreliminaryUploadFile(file.Id));
                    }
                }
                else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && !file.Local.IsDownloadingCompleted)
                {
                    if (_message.CanBeAddedToDownloads)
                    {
                        _message.ClientService.AddFileToDownloads(file, _message.ChatId, _message.Id);
                    }
                    else
                    {
                        _message.ClientService.DownloadFile(file.Id, 30);
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
                    if (_message.SendingState is MessageSendingStateFailed or MessageSendingStatePending)
                    {
                        _message.ClientService.Send(new DeleteMessages(_message.ChatId, new[] { _message.Id }, true));
                    }
                    else
                    {
                        _message.ClientService.Send(new CancelPreliminaryUploadFile(file.Id));
                    }
                }
                else if (_paidMedia != null)
                {
                    _message.Delegate.OpenPaidMedia(_message, _paidMedia, this);
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

        public int LoopCount => Player?.LoopCount ?? 1;

        private bool _withinViewport;

        public void ViewportChanged(bool within)
        {
            if (within && !_withinViewport)
            {
                _withinViewport = true;
                Play();
            }
            else if (_withinViewport && !within)
            {
                _withinViewport = false;
                Pause();
            }
        }

        public void Play()
        {
            Player?.Play();
        }

        public void Pause()
        {
            Player?.Pause();
        }

        #endregion
    }
}
