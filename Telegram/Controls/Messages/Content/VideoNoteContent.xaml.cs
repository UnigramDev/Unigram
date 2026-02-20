//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Native.Controls;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls.Messages.Content
{
    // TODO: progress bar when paused
    //       larger size while playing
    public sealed partial class VideoNoteContent : ControlEx, IContentWithFile, IContentWithMask, IPlayerView
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        private ThumbnailController _thumbnailController;

        private long _fileToken;
        private long _thumbnailToken;

        public VideoNoteContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(VideoNoteContent);
        }

        protected override void OnUnloaded()
        {
            LifetimeService.Current.Playback.SourceChanged -= OnPlaybackStateChanged;
            LifetimeService.Current.Playback.StateChanged -= OnPlaybackStateChanged;
            LifetimeService.Current.Playback.PositionChanged -= OnPositionChanged;
        }

        #region InitializeComponent

        private AutomaticDragHelper ButtonDrag;

        private AspectView LayoutRoot;
        private Ellipse Holder;
        private ImageBrush ThumbnailTexture;
        private FileButton Button;
        private Border ViewOnce;
        private Grid Element;
        private AnimatedImage Player;
        private Border Overlay;
        private TextBlock Subtitle;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            LayoutRoot = GetTemplateChild(nameof(LayoutRoot)) as AspectView;
            Holder = GetTemplateChild(nameof(Holder)) as Ellipse;
            ThumbnailTexture = GetTemplateChild(nameof(ThumbnailTexture)) as ImageBrush;
            Button = GetTemplateChild(nameof(Button)) as FileButton;
            ViewOnce = GetTemplateChild(nameof(ViewOnce)) as Border;
            Element = GetTemplateChild(nameof(Element)) as Grid;
            Player = GetTemplateChild(nameof(Player)) as AnimatedImage;
            Overlay = GetTemplateChild(nameof(Overlay)) as Border;
            Subtitle = GetTemplateChild(nameof(Subtitle)) as TextBlock;

            ButtonDrag = new AutomaticDragHelper(Button, true);
            ButtonDrag.StartDetectingDrag();

            Button.Click += Button_Click;
            Button.DragStarting += Button_DragStarting;

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

            LifetimeService.Current.Playback.SourceChanged -= OnPlaybackStateChanged;

            var videoNote = GetContent(message, out bool isSecret);
            if (videoNote == null || !_templateApplied)
            {
                return;
            }

            LifetimeService.Current.Playback.SourceChanged += OnPlaybackStateChanged;

            LayoutRoot.Constraint = message;

            if (message.Content is MessageVideoNote videoNoteMessage)
            {
                Subtitle.Text = videoNote.GetDuration() + (videoNoteMessage.IsViewed ? string.Empty : " ●");
            }
            else
            {
                Subtitle.Text = videoNote.GetDuration();
            }

            ViewOnce.Visibility = message.SelfDestructType is MessageSelfDestructTypeImmediately
                ? Visibility.Visible
                : Visibility.Collapsed;

            UpdateThumbnail(message, videoNote, videoNote.Thumbnail?.File, true, isSecret);

            UpdateManager.Subscribe(this, message, videoNote.Video, ref _fileToken, UpdateFile);
            UpdateFile(message, videoNote.Video);
        }

        #region Playback

        private void OnPlaybackStateChanged(IPlaybackService sender, object args)
        {
            this.BeginOnUIThread(() =>
            {
                var videoNote = GetContent(_message, out bool isSecret);
                if (videoNote == null)
                {
                    Recycle();
                    return;
                }

                UpdateFile(_message, videoNote.Video);
            });
        }

        private void OnPositionChanged(IPlaybackService sender, PlaybackPositionChangedEventArgs args)
        {
            var position = args.Position;
            var duration = args.Duration;
            var playing = sender.IsPlaying;

            this.BeginOnUIThread(() => UpdatePosition(position, duration, playing));
        }

        private void UpdateDuration()
        {
            var message = _message;
            if (message == null || !_templateApplied)
            {
                return;
            }

            var videoNote = GetContent(message, out bool isSecret);
            if (videoNote == null)
            {
                return;
            }

            if (message.Content is MessageVideoNote videoNoteMessage)
            {
                Subtitle.Text = videoNote.GetDuration() + (videoNoteMessage.IsViewed ? string.Empty : " ●");
                //Progress.UpdateValue(message.IsOutgoing || voiceNoteMessage.IsListened ? 0 : voiceNote.Duration, voiceNote.Duration, PlaybackState.None);
            }
            else
            {
                Subtitle.Text = videoNote.GetDuration();
                //Progress.UpdateValue(0, voiceNote.Duration, PlaybackState.None);
            }
        }

        private void UpdatePosition(TimeSpan position, TimeSpan duration, bool playing)
        {
            var message = _message;
            if (message == null /*|| Progress.IsScrubbing*/)
            {
                return;
            }

            if (message.AreTheSame(LifetimeService.Current.Playback.CurrentItem) /*&& !_pressed*/)
            {
                if (duration.TotalSeconds == 0)
                {
                    return;
                }

                Subtitle.Text = FormatTime(duration - position, duration.TotalHours);
                //Progress.UpdateValue(position, duration, state);
            }
        }

        private string FormatTime(TimeSpan span, double totalHours)
        {
            if (totalHours >= 1)
            {
                return span.ToString("h\\:mm\\:ss");
            }
            else
            {
                return span.ToString("mm\\:ss");
            }
        }

        #endregion

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
            var videoNote = GetContent(message, out bool isSecret);
            if (videoNote == null || !_templateApplied)
            {
                return;
            }

            if (videoNote.Video.Id != file.Id)
            {
                return;
            }

            if (message.AreTheSame(LifetimeService.Current.Playback.CurrentItem))
            {
                if (LifetimeService.Current.Playback.PlaybackState == PlaybackState.Paused)
                {
                    Button.SetGlyph(file.Id, MessageContentState.Play);
                }
                else
                {
                    Button.SetGlyph(file.Id, MessageContentState.Pause);
                }

                Button.Progress = 1;

                Player.Source = null;

                UpdatePosition(LifetimeService.Current.Playback.Position, LifetimeService.Current.Playback.Duration, LifetimeService.Current.Playback.IsPlaying);
            }
            else
            {
                var canBeDownloaded = file.Local.CanBeDownloaded
                    && !file.Local.IsDownloadingCompleted
                    && !file.Local.IsDownloadingActive;

                var size = Math.Max(file.Size, file.ExpectedSize);
                if (file.Local.IsDownloadingActive || (canBeDownloaded && message.Delegate.CanBeDownloaded(videoNote, file)))
                {
                    if (canBeDownloaded)
                    {
                        _message.ClientService.DownloadFile(file.Id, 32);
                    }

                    //Button.Glyph = Icons.Cancel;
                    Button.SetGlyph(file.Id, MessageContentState.Downloading);
                    Button.Progress = (double)file.Local.DownloadedSize / size;

                    Player.Source = null;
                }
                else if (file.Remote.IsUploadingActive || message.SendingState is MessageSendingStateFailed || (message.SendingState is MessageSendingStatePending && !file.Remote.IsUploadingCompleted))
                {
                    //Button.Glyph = Icons.Cancel;
                    Button.SetGlyph(file.Id, MessageContentState.Uploading);
                    Button.Progress = (double)file.Remote.UploadedSize / size;

                    Player.Source = null;
                }
                else if (canBeDownloaded)
                {
                    //Button.Glyph = Icons.Download;
                    Button.SetGlyph(file.Id, MessageContentState.Download);
                    Button.Progress = 0;

                    Player.Source = null;
                }
                else
                {
                    if (isSecret)
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

                        Player.Source = new LocalFileSource(file);
                        message.Delegate.ViewVisibleMessages();
                    }
                }
            }

            Button.Opacity = Player.Source == null ? 1 : 0;

            UpdateDuration();
            UpdateSource();
        }

        private void UpdateThumbnail(object target, File file)
        {
            var videoNote = GetContent(_message, out bool isSecret);
            if (videoNote == null || !_templateApplied)
            {
                return;
            }

            UpdateThumbnail(_message, videoNote, file, false, isSecret);
        }

        private void UpdateThumbnail(MessageViewModel message, VideoNote videoNote, File file, bool download, bool isSecret)
        {
            _thumbnailController ??= new ThumbnailController(ThumbnailTexture);

            if (videoNote.Thumbnail != null && videoNote.Thumbnail.Format is ThumbnailFormatJpeg)
            {
                if (file.Local.IsDownloadingCompleted)
                {
                    _thumbnailController.Blur(file.Local.Path, isSecret ? 15 : 3, HashCode.Combine(message.ChatId, message.Id));
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

                    if (videoNote.Minithumbnail != null)
                    {
                        _thumbnailController.Blur(videoNote.Minithumbnail.Data, isSecret ? 15 : 3, HashCode.Combine(message.ChatId, message.Id));
                    }
                    else
                    {
                        _thumbnailController.Recycle();
                    }
                }
            }
            else if (videoNote.Minithumbnail != null)
            {
                _thumbnailController.Blur(videoNote.Minithumbnail.Data, isSecret ? 15 : 3, HashCode.Combine(message.ChatId, message.Id));
            }
            else
            {
                _thumbnailController.Recycle();
            }
        }

        public void Recycle()
        {
            LifetimeService.Current.Playback.SourceChanged -= OnPlaybackStateChanged;
            LifetimeService.Current.Playback.StateChanged -= OnPlaybackStateChanged;
            LifetimeService.Current.Playback.PositionChanged -= OnPositionChanged;

            RemoveMessage(XamlRoot, _message);

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
            if (content is MessageVideoNote)
            {
                return true;
            }
            else if (content is MessageText text && text.LinkPreview != null && !primary)
            {
                return text.LinkPreview.Type is LinkPreviewTypeVideoNote;
            }

            return false;
        }

        private VideoNote GetContent(MessageViewModel message, out bool isSecret)
        {
            if (message?.Delegate == null)
            {
                isSecret = false;
                return null;
            }

            var content = message.Content;
            if (content is MessageVideoNote videoNote)
            {
                isSecret = videoNote.IsSecret;
                return videoNote.VideoNote;
            }
            else if (content is MessageText text && text.LinkPreview?.Type is LinkPreviewTypeVideoNote previewVideoNode)
            {
                isSecret = false;
                return previewVideoNode.VideoNote;
            }

            isSecret = false;
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
            var videoNote = GetContent(_message, out bool isSecret);
            if (videoNote == null)
            {
                return;
            }

            var file = videoNote.Video;
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
            else if (_message.AreTheSame(LifetimeService.Current.Playback.CurrentItem))
            {
                if (LifetimeService.Current.Playback.PlaybackState == PlaybackState.Paused)
                {
                    LifetimeService.Current.Playback.Play();
                }
                else
                {
                    LifetimeService.Current.Playback.Pause();
                }
            }
            // This branch could be likely removed with some tuning
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && !file.Local.IsDownloadingCompleted)
            {
                _message.ClientService.DownloadFile(file.Id, 30);
            }
            else
            {
                _message.Delegate.PlayMessage(_message);
            }
        }

        private void Button_DragStarting(UIElement sender, DragStartingEventArgs args)
        {
            MessageHelper.DragStarting(_message, args);
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
            AddMessage(XamlRoot, _message);
            Player?.Play();

            UpdateSource();
        }

        public void Pause()
        {
            RemoveMessage(XamlRoot, _message);
            Player?.Pause();

            UpdateSource();
        }

        #endregion

        private readonly record struct VideoNoteMessage(int SessionId, long ChatId, long MessageId);

        private static readonly HashSetDictionary<XamlRoot, VideoNoteMessage> _visibleMessages = new();
        private static readonly object _visibleMessagesLock = new();

        private static void AddMessage(XamlRoot xamlRoot, MessageViewModel message)
        {
            UpdateMessage(xamlRoot, message, _visibleMessages.Add);
        }

        private static void RemoveMessage(XamlRoot xamlRoot, MessageViewModel message)
        {
            UpdateMessage(xamlRoot, message, _visibleMessages.Remove);
        }

        private static void UpdateMessage(XamlRoot xamlRoot, MessageViewModel message, Func<XamlRoot, VideoNoteMessage, bool> action)
        {
            if (message == null)
            {
                return;
            }

            Monitor.Enter(_visibleMessagesLock);

            if (action(xamlRoot, new VideoNoteMessage(message.ClientService.SessionId, message.ChatId, message.Id)))
            {
                Monitor.Exit(_visibleMessagesLock);

                VisibleMessagesChanged?.Invoke(null, EventArgs.Empty);
            }
            else
            {
                Monitor.Exit(_visibleMessagesLock);
            }
        }

        public static event EventHandler VisibleMessagesChanged;

        public static bool IsMessageVisible(XamlRoot xamlRoot, MessageWithOwner message)
        {
            lock (_visibleMessagesLock)
            {
                return _visibleMessages.Contains(xamlRoot, new VideoNoteMessage(message.ClientService.SessionId, message.ChatId, message.Id));
            }
        }

        private SwapChainPanel _panel;

        private void UpdateSource()
        {
            LifetimeService.Current.Playback.StateChanged -= OnPlaybackStateChanged;
            LifetimeService.Current.Playback.PositionChanged -= OnPositionChanged;

            if (_withinViewport && _message.AreTheSame(LifetimeService.Current.Playback.CurrentItem))
            {
                LifetimeService.Current.Playback.StateChanged += OnPlaybackStateChanged;
                LifetimeService.Current.Playback.PositionChanged += OnPositionChanged;

                if (_panel == null)
                {
                    _panel = new SwapChainPanel();
                    _panel.IsHitTestVisible = false;

                    Element.Children.Add(_panel);
                    LifetimeService.Current.Playback.Attach(_panel);
                }
            }
            else if (_panel != null)
            {
                Element.Children.Remove(_panel);
                LifetimeService.Current.Playback.Detach(_panel);

                _panel = null;
            }
        }
    }
}
