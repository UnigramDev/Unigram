//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Messages.Content
{
    public sealed partial class VideoContent : Control, IContentWithFile, IPlayerView
    {
        private readonly bool _album;

        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        private PaidMediaVideo _paidMedia;

        private long _fileToken;
        private long _thumbnailToken;

        private ThumbnailController _thumbnailController;

        private bool _hidden = true;

        public VideoContent(MessageViewModel message, PaidMediaVideo paidMedia = null, bool album = false)
        {
            _message = message;
            _paidMedia = paidMedia;
            _album = album;

            DefaultStyleKey = typeof(VideoContent);
        }

        #region InitializeComponent

        private AutomaticDragHelper ButtonDrag;

        private AspectView LayoutRoot;
        private ImageBrush ThumbnailTexture;
        private AnimatedImage Particles;
        private FileButton Button;
        private AnimatedImage Player;
        private FileButton Overlay;
        private TextBlock Subtitle;
        private ProgressBar Indicator;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            LayoutRoot = GetTemplateChild(nameof(LayoutRoot)) as AspectView;
            ThumbnailTexture = LayoutRoot.Background as ImageBrush;
            Particles = GetTemplateChild(nameof(Particles)) as AnimatedImage;
            Button = GetTemplateChild(nameof(Button)) as FileButton;
            Player = GetTemplateChild(nameof(Player)) as AnimatedImage;
            Overlay = GetTemplateChild(nameof(Overlay)) as FileButton;
            Subtitle = GetTemplateChild(nameof(Subtitle)) as TextBlock;
            Indicator = GetTemplateChild(nameof(Indicator)) as ProgressBar;

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
            var prevId = _message?.Id;
            var nextId = message?.Id;

            _message = message;

            var video = GetContent(message, out Photo cover, out AlternativeVideo lowQuality, out bool hasSpoiler, out bool isSecret);
            if (video == null || !_templateApplied)
            {
                _hidden = (prevId != nextId || _hidden) && hasSpoiler;
                return;
            }

            _hidden = (prevId != nextId || _hidden) && hasSpoiler;

            LayoutRoot.Constraint = _album ? null : isSecret ? Constants.SecretSize : ((object)_paidMedia ?? video);

            File thumbnail;
            Minithumbnail minithumbnail;

            var photo = cover?.GetBig();
            if (photo != null)
            {
                thumbnail = photo.Photo;
                minithumbnail = cover.Minithumbnail;
            }
            else
            {
                thumbnail = video.Thumbnail?.Format is ThumbnailFormatJpeg or ThumbnailFormatPng or ThumbnailFormatGif ? video.Thumbnail.File : null;
                minithumbnail = video.Minithumbnail;
            }

            UpdateMessageContentOpened(message);
            UpdateThumbnail(message, thumbnail, minithumbnail, true, isSecret, hasSpoiler);

            UpdateManager.Subscribe(this, message, lowQuality?.Video ?? video.VideoValue, ref _fileToken, UpdateFile);
            UpdateFile(message, lowQuality?.Video ?? video.VideoValue, video, lowQuality, hasSpoiler, isSecret);
        }

        private bool _indicatorCollapsed = true;

        private void UpdatePosition(double position, double duration)
        {
            if (duration >= 30)
            {
                if (_indicatorCollapsed)
                {
                    _indicatorCollapsed = false;
                    Indicator.Visibility = Visibility.Visible;
                }

                Indicator.Maximum = duration;
                Indicator.Value = position;
            }
            else if (!_indicatorCollapsed)
            {
                _indicatorCollapsed = true;
                Indicator.Visibility = Visibility.Collapsed;
            }
        }

        public void UpdateMessageContentOpened(MessageViewModel message)
        {
            if (message.Content is MessageVideo video && message.Delegate.Settings.Video.TryGetPosition(video.Video.VideoValue, out double position))
            {
                UpdatePosition(position, video.Video.Duration);
            }
            else
            {
                UpdatePosition(0, 0);
            }
        }

        private void UpdateFile(object target, File file)
        {
            var video = GetContent(_message, out Photo cover, out var lowQuality, out bool hasSpoiler, out bool isSecret);
            if (video != null && _templateApplied)
            {
                UpdateFile(_message, file, video, lowQuality, hasSpoiler, isSecret);
            }
        }

        private void UpdateFile(MessageViewModel message, File file, Video video, AlternativeVideo lowQuality, bool hasSpoiler, bool isSecret)
        {
            if (video == null || !_templateApplied)
            {
                return;
            }

            //if (video.VideoValue.Id != file.Id)
            //{
            //    return;
            //}

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
            else if (lowQuality != null)
            {
                if (!hasSpoiler && message.Delegate.CanBeDownloaded(video, file))
                {
                    _message.ClientService.DownloadFile(file.Id, 32);

                    if (lowQuality != null)
                    {
                        _message.ClientService.DownloadFile(lowQuality.HlsFile.Id, 32);
                    }

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
            else
            {
                var size = Math.Max(file.Size, file.ExpectedSize);
                if (file.Local.IsDownloadingActive)
                {
                    if (video.SupportsStreaming && !hasSpoiler && message.Delegate.CanBeDownloaded(video, file))
                    {
                        UpdateSource(message, file);
                    }
                    else
                    {
                        UpdateSource(null, null);
                    }

                    Button.SetGlyph(file.Id, MessageContentState.Play);
                    Button.Progress = 0;
                    Overlay.SetGlyph(file.Id, MessageContentState.Downloading);
                    Overlay.Progress = (double)file.Local.DownloadedSize / size;
                    Overlay.ProgressVisibility = Visibility.Visible;

                    if (Player.Source == null)
                    {
                        Subtitle.Text = GetDuration(video) + string.Format("{0} / {1}", FileSizeConverter.Convert(file.Local.DownloadedSize, size), FileSizeConverter.Convert(size));
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
                        Subtitle.Text = GetDuration(video) + Strings.ProcessingVideo;
                    }
                    else
                    {
                        Subtitle.Text = GetDuration(video) + string.Format("{0} / {1}", FileSizeConverter.Convert(file.Remote.UploadedSize, size), FileSizeConverter.Convert(size));
                    }
                }
                else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
                {
                    Button.SetGlyph(file.Id, MessageContentState.Play);
                    Button.Progress = 0;
                    Overlay.SetGlyph(file.Id, MessageContentState.Download);
                    Overlay.Progress = 0;
                    Overlay.ProgressVisibility = Visibility.Visible;

                    Subtitle.Text = GetDuration(video) + FileSizeConverter.Convert(size);

                    if (video.SupportsStreaming && !hasSpoiler && message.Delegate.CanBeDownloaded(video, file))
                    {
                        _message.ClientService.DownloadFile(file.Id, 32);
                        UpdateSource(message, file);
                    }
                    else
                    {
                        if (_message.Delegate.Settings.AutoDownload.PreloadLargeVideos && SettingsService.Current.Diagnostics.VideoPreloadDebug)
                        {
                            VideoPreloader.Current.Load(_message.ClientService, file, video.Duration);
                        }

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

        private string GetDuration(Video video)
        {
            if (video.Duration > 0)
            {
                return video.GetDuration() + "\n";
            }

            return string.Empty;
        }

        private void UpdateThumbnail(object target, File file)
        {
            var video = GetContent(_message, out Photo cover, out _, out bool hasSpoiler, out bool isSecret);
            if (video == null || !_templateApplied)
            {
                return;
            }

            Minithumbnail minithumbnail;

            var photo = cover?.GetBig();
            if (photo != null)
            {
                minithumbnail = cover.Minithumbnail;
            }
            else
            {
                minithumbnail = video.Minithumbnail;
            }

            UpdateThumbnail(_message, file, minithumbnail, false, isSecret, hasSpoiler);
        }

        private void UpdateThumbnail(MessageViewModel message, File file, Minithumbnail minithumbnail, bool download, bool isSecret, bool hasSpoiler)
        {
            _thumbnailController ??= new ThumbnailController(ThumbnailTexture);

            if (file != null)
            {
                if (file.Local.IsDownloadingCompleted)
                {
                    if (isSecret || (hasSpoiler && _hidden))
                    {
                        _thumbnailController.Blur(file.Local.Path, 15, HashCode.Combine(message.ChatId, message.Id));
                    }
                    else
                    {
                        _thumbnailController.Bitmap(file.Local.Path, hashCode: HashCode.Combine(message.ChatId, message.Id));
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

                    if (minithumbnail != null)
                    {
                        _thumbnailController.Blur(minithumbnail.Data, isSecret || (hasSpoiler && _hidden) ? 15 : 3, HashCode.Combine(message.ChatId, message.Id));
                    }
                    else
                    {
                        _thumbnailController.Recycle();
                    }
                }
            }
            else if (minithumbnail != null)
            {
                _thumbnailController.Blur(minithumbnail.Data, isSecret || (hasSpoiler && _hidden) ? 15 : 3, HashCode.Combine(message.ChatId, message.Id));
            }
            else
            {
                _thumbnailController.Recycle();
            }

            Particles.Source = isSecret || (hasSpoiler && _hidden)
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
            var video = GetContent(_message, out _, out _, out _, out _);
            if (video == null)
            {
                return;
            }

            try
            {
                var position = TimeSpan.FromSeconds(video.Duration - Math.Truncate(e.Position));
                if (position.TotalHours >= 1)
                {
                    Subtitle.Text = position.ToString("h\\:mm\\:ss");
                }
                else
                {
                    Subtitle.Text = position.ToString("mm\\:ss");
                }

                UpdatePosition(e.Position, Player.IsPlaying ? video.Duration : 0);
            }
            catch (Exception ex)
            {
                Logger.Info(video.Duration + " - " + e.Position);
                Logger.Exception(ex);
            }
        }

        public void Recycle()
        {
            _message = null;
            _thumbnailController?.Recycle();

            UpdateManager.Unsubscribe(this, ref _fileToken);
            UpdateManager.Unsubscribe(this, ref _thumbnailToken);

            if (_templateApplied)
            {
                Player.Source = null;
            }
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            switch (content)
            {
                case MessageVideo:
                    return true;
                case MessageText text when text.LinkPreview != null && !primary:
                    {
                        if (text.LinkPreview.Type is LinkPreviewTypeVideo or LinkPreviewTypeEmbeddedVideoPlayer { Video: not null } || text.LinkPreview.Type is LinkPreviewTypeAlbum album && album.Media[0] is LinkPreviewAlbumMediaVideo)
                        {
                            return true;
                        }
                        else if (text.LinkPreview.Type is LinkPreviewTypeStoryAlbum { VideoIcon: not null })
                        {
                            return true;
                        }

                        break;
                    }

                case MessageInvoice invoice when invoice.PaidMedia is PaidMediaVideo:
                    return true;
                case MessagePoll poll when poll.Media is PollMediaVideo && !primary:
                    return true;
                case MessageSponsored { Content: MessageVideo } when !primary:
                    return true;
            }

            return false;
        }

        private Video GetContent(MessageViewModel message, out Photo cover, out AlternativeVideo lowQuality, out bool hasSpoiler, out bool isSecret)
        {
            cover = null;
            lowQuality = null;
            hasSpoiler = false;
            isSecret = false;

            if (message?.Delegate == null)
            {
                return null;
            }

            if (_paidMedia != null)
            {
                cover = _paidMedia.Cover;
                return _paidMedia.Video;
            }

            var content = message.GeneratedContent ?? message.Content;
            switch (content)
            {
                case MessageVideo video:
                    if (video.AlternativeVideos.Count > 0)
                    {
                        lowQuality = video.AlternativeVideos[0];
                    }

                    cover = video.Cover;
                    hasSpoiler = video.HasSpoiler;
                    isSecret = video.IsSecret;
                    return video.Video;
                case MessageText text:
                    {
                        if (text.LinkPreview?.Type is LinkPreviewTypeVideo previewVideo)
                        {
                            cover = previewVideo.Cover;
                            return previewVideo.Video;
                        }
                        else if (text.LinkPreview?.Type is LinkPreviewTypeEmbeddedVideoPlayer embeddedVideoPlayer)
                        {
                            return embeddedVideoPlayer.Video;
                        }
                        else if (text.LinkPreview?.Type is LinkPreviewTypeAlbum previewAlbum && previewAlbum.Media[0] is LinkPreviewAlbumMediaVideo albumVideo)
                        {
                            return albumVideo.Video;
                        }
                        else if (text.LinkPreview?.Type is LinkPreviewTypeStoryAlbum previewStoryAlbum && previewStoryAlbum.VideoIcon != null)
                        {
                            return previewStoryAlbum.VideoIcon;
                        }

                        break;
                    }

                case MessageInvoice invoice when invoice.PaidMedia is PaidMediaVideo paidMedia:
                    cover = paidMedia.Cover;
                    return paidMedia.Video;
                case MessagePoll poll when poll.Media is PollMediaVideo pollVideo:
                    cover = pollVideo.Cover;
                    return pollVideo.Video;
                case MessageSponsored { Content: MessageVideo sponsored }:
                    cover = sponsored.Cover;
                    return sponsored.Video;
            }

            return null;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var video = GetContent(_message, out _, out _, out bool hasSpoiler, out bool isSecret);
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
            else if (hasSpoiler && _hidden)
            {
                _hidden = false;
                UpdateMessage(_message);
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
            var video = GetContent(_message, out _, out _, out bool hasSpoiler, out bool isSecret);
            if (video == null)
            {
                return;
            }

            if (hasSpoiler && _hidden)
            {
                _hidden = false;
                UpdateMessage(_message);

                return;
            }

            if (isSecret || !video.SupportsStreaming)
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
                else if (hasSpoiler && _hidden)
                {
                    _hidden = false;
                    UpdateMessage(_message);
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
                    else if (hasSpoiler && _hidden)
                    {
                        _hidden = false;
                        UpdateMessage(_message);

                        return;
                    }

                    if (_indicatorCollapsed || _message.Delegate.Settings.Video.HasPosition(video.VideoValue))
                    {
                        _message.Delegate.OpenMedia(_message, this);
                    }
                    else
                    {
                        _message.Delegate.OpenMedia(_message, this, Indicator.Value);
                    }
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
