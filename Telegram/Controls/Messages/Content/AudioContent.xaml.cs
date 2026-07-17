//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Native.Controls;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Messages.Content
{
    // TODO: turn the whole control into a Button
    public sealed partial class AudioContent : ControlEx, IContent
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        private long _fileToken;
        private long _thumbnailToken;

        public AudioContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(AudioContent);
        }

        public AudioContent()
        {
            DefaultStyleKey = typeof(AudioContent);
        }

        #region InitializeComponent

        private AutomaticDragHelper ButtonDrag;

        private Border Texture;
        private FileButton Button;
        private Grid DownloadPanel;
        private FileButton Download;
        private TextBlock Title;
        private TextBlock TitleTrim;
        private TextBlock Subtitle;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            Texture = GetTemplateChild(nameof(Texture)) as Border;
            Button = GetTemplateChild(nameof(Button)) as FileButton;
            DownloadPanel = GetTemplateChild(nameof(DownloadPanel)) as Grid;
            Download = GetTemplateChild(nameof(Download)) as FileButton;
            Title = GetTemplateChild(nameof(Title)) as TextBlock;
            TitleTrim = GetTemplateChild(nameof(TitleTrim)) as TextBlock;
            Subtitle = GetTemplateChild(nameof(Subtitle)) as TextBlock;

            ButtonDrag = new AutomaticDragHelper(Button, true);
            ButtonDrag.StartDetectingDrag();

            Button.Click += Button_Click;
            Button.DragStarting += Button_DragStarting;

            Download.Click += Download_Click;

            _templateApplied = true;

            if (_message != null)
            {
                UpdateMessage(_message);
            }
        }

        #endregion

        protected override void OnLoaded()
        {
            var audio = GetContent(_message);
            if (audio == null || !_templateApplied)
            {
                return;
            }

            // Subscribe to the session-lived Playback service only while connected. Subscribing on
            // bind (UpdateMessage/UpdatePlayback, gated by IsConnected) leaked controls that were
            // prepared but never loaded, so OnUnloaded/Recycle never ran. The visuals were already
            // set during bind; here we only (re)establish the subscriptions, so it stays cheap.
            var playback = LifetimeService.Current.Playback;
            playback.SourceChanged -= OnPlaybackStateChanged;
            playback.SourceChanged += OnPlaybackStateChanged;

            UpdatePlayback(_message, audio, audio.AudioValue);
        }

        protected override void OnUnloaded()
        {
            LifetimeService.Current.Playback.SourceChanged -= OnPlaybackStateChanged;
            LifetimeService.Current.Playback.StateChanged -= OnPlaybackStateChanged;
            LifetimeService.Current.Playback.PositionChanged -= OnPositionChanged;
        }

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;

            LifetimeService.Current.Playback.SourceChanged -= OnPlaybackStateChanged;

            var audio = GetContent(message);
            if (audio == null || !_templateApplied)
            {
                return;
            }

            if (IsConnected)
            {
                LifetimeService.Current.Playback.SourceChanged += OnPlaybackStateChanged;
            }

            if (string.IsNullOrEmpty(audio.Title))
            {
                var index = audio.FileName.LastIndexOf('.');
                if (index > 0)
                {
                    Title.Text = audio.FileName.Substring(0, index + 1);
                    TitleTrim.Text = audio.FileName.Substring(index + 1);
                }
                else
                {
                    Title.Text = audio.FileName;
                    TitleTrim.Text = string.Empty;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(audio.Performer))
                {
                    Title.Text = audio.Title;
                }
                else
                {
                    Title.Text = $"{audio.Title} - {audio.Performer}";
                }

                TitleTrim.Text = string.Empty;
            }

            if (audio.AlbumCoverThumbnail != null)
            {
                UpdateManager.Subscribe(this, message, audio.AlbumCoverThumbnail.File, ref _thumbnailToken, UpdateThumbnail, true);
                UpdateThumbnail(message, audio.AlbumCoverThumbnail, audio.AlbumCoverThumbnail.File);
            }
            else
            {
                Texture.Background = null;
                Button.Style = BootStrapper.Current.Resources["InlineFileButtonStyle"] as Style;
            }

            UpdateManager.Subscribe(this, message, audio.AudioValue, ref _fileToken, UpdateFile);
            UpdateFile(message, audio.AudioValue);
        }

        public void Mockup(MessageAudio audio)
        {
            Title.Text = audio.Audio.GetTitle();
            Subtitle.Text = audio.Audio.GetDuration() + ", " + FileSizeConverter.Convert(4190000);

            Button.SetGlyph(0, MessageContentState.Play);
            Download.SetGlyph(0, MessageContentState.Download);
        }

        #region Playback

        private void OnPlaybackStateChanged(IPlaybackService sender, object args)
        {
            this.BeginOnUIThread(() =>
            {
                var audio = GetContent(_message);
                if (audio == null)
                {
                    Recycle();
                    return;
                }

                UpdateFile(_message, audio.AudioValue);
            });
        }

        private void OnPositionChanged(IPlaybackService sender, PlaybackPositionChangedEventArgs args)
        {
            var position = args.Position;
            var duration = args.Duration;

            this.BeginOnUIThread(() => UpdatePosition(position, duration));
        }

        private void UpdatePosition(TimeSpan position, TimeSpan duration)
        {
            var message = _message;
            if (message == null)
            {
                return;
            }

            if (message.AreTheSame(LifetimeService.Current.Playback.CurrentItem) /*&& !_pressed*/)
            {
                Subtitle.Text = FormatTime(position) + " / " + FormatTime(duration);
            }
        }

        private string FormatTime(TimeSpan span)
        {
            if (span.TotalHours >= 1)
            {
                return span.ToString("h\\:mm\\:ss");
            }
            else
            {
                return span.ToString("mm\\:ss");
            }
        }

        #endregion

        private void UpdateFile(object target, File file)
        {
            if (_message.AreTheSame(LifetimeService.Current.Playback.CurrentItem))
            {
                return;
            }

            UpdateFile(_message, file);
        }

        private void UpdateFile(MessageViewModel message, File file)
        {
            var audio = GetContent(message);
            if (audio == null || !_templateApplied)
            {
                return;
            }

            LifetimeService.Current.Playback.StateChanged -= OnPlaybackStateChanged;
            LifetimeService.Current.Playback.PositionChanged -= OnPositionChanged;

            if (audio.AudioValue.Id != file.Id)
            {
                return;
            }

            var canBeDownloaded = file.Local.CanBeDownloaded
                && !file.Local.IsDownloadingCompleted
                && !file.Local.IsDownloadingActive;

            var size = Math.Max(file.Size, file.ExpectedSize);
            if (file.Local.IsDownloadingActive || (canBeDownloaded && message.Delegate.CanBeDownloaded(audio, file)))
            {
                if (canBeDownloaded)
                {
                    _message.ClientService.DownloadFile(file.Id, 32);
                }

                FileButton target;
                if (SettingsService.Current.IsStreamingEnabled)
                {
                    target = Download;
                    DownloadPanel.Visibility = Visibility.Visible;
                }
                else
                {
                    target = Button;
                    DownloadPanel.Visibility = Visibility.Collapsed;
                }

                target.SetGlyph(file.Id, MessageContentState.Downloading);
                target.Progress = (double)file.Local.DownloadedSize / size;

                Subtitle.Text = string.Format("{0} / {1}", FileSizeConverter.Convert(file.Local.DownloadedSize, size), FileSizeConverter.Convert(size));
            }
            else if (file.Remote.IsUploadingActive || message.SendingState is MessageSendingStateFailed || (message.SendingState is MessageSendingStatePending && !file.Remote.IsUploadingCompleted))
            {
                DownloadPanel.Visibility = Visibility.Collapsed;

                Button.SetGlyph(file.Id, MessageContentState.Uploading);
                Button.Progress = (double)file.Remote.UploadedSize / size;

                Subtitle.Text = string.Format("{0} / {1}", FileSizeConverter.Convert(file.Remote.UploadedSize, size), FileSizeConverter.Convert(size));
            }
            else if (canBeDownloaded)
            {
                FileButton target;
                if (SettingsService.Current.IsStreamingEnabled)
                {
                    target = Download;
                    DownloadPanel.Visibility = Visibility.Visible;
                }
                else
                {
                    target = Button;
                    DownloadPanel.Visibility = Visibility.Collapsed;
                }

                target.SetGlyph(file.Id, MessageContentState.Download);
                target.Progress = 0;

                Subtitle.Text = audio.GetDuration() + " - " + FileSizeConverter.Convert(size);
            }
            else
            {
                DownloadPanel.Visibility = Visibility.Collapsed;

                if (!SettingsService.Current.IsStreamingEnabled)
                {
                    UpdatePlayback(message, audio, file);
                }
            }

            if (SettingsService.Current.IsStreamingEnabled && !file.Remote.IsUploadingActive)
            {
                UpdatePlayback(message, audio, file);
            }
        }

        private void UpdatePlayback(MessageViewModel message, Audio audio, File file)
        {
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

                DownloadPanel.Visibility = Visibility.Collapsed;

                UpdatePosition(LifetimeService.Current.Playback.Position, LifetimeService.Current.Playback.Duration);

                if (IsConnected)
                {
                    LifetimeService.Current.Playback.StateChanged += OnPlaybackStateChanged;
                    LifetimeService.Current.Playback.PositionChanged += OnPositionChanged;
                }
            }
            else
            {
                Button.SetGlyph(file.Id, MessageContentState.Play);
                Button.Progress = 1;

                if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted && !file.Local.IsDownloadingActive && !file.Remote.IsUploadingActive)
                {
                    Subtitle.Text = audio.GetDuration() + " - " + FileSizeConverter.Convert(Math.Max(file.Size, file.ExpectedSize));
                }
                else
                {
                    Subtitle.Text = audio.GetDuration();
                }
            }

            Button.Progress = 1;
        }

        private void UpdateThumbnail(object target, File file)
        {
            var audio = GetContent(_message);
            if (audio == null || !_templateApplied)
            {
                return;
            }

            UpdateThumbnail(_message, audio.AlbumCoverThumbnail, file);
        }

        private void UpdateThumbnail(MessageViewModel message, Thumbnail thumbnail, File file)
        {
            if (thumbnail.File.Id != file.Id)
            {
                return;
            }

            if (file.Local.IsDownloadingCompleted)
            {
                double ratioX = (double)48 / thumbnail.Width;
                double ratioY = (double)48 / thumbnail.Height;
                double ratio = Math.Max(ratioX, ratioY);

                var width = (int)(thumbnail.Width * ratio);
                var height = (int)(thumbnail.Height * ratio);

                try
                {
                    Texture.Background = new ImageBrush { ImageSource = UriEx.ToBitmap(file.Local.Path, width, height), Stretch = Stretch.UniformToFill, AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center };
                    Button.Style = BootStrapper.Current.Resources["ImmersiveFileButtonStyle"] as Style;
                }
                catch
                {
                    Texture.Background = null;
                    Button.Style = BootStrapper.Current.Resources["InlineFileButtonStyle"] as Style;
                }
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                message.ClientService.DownloadFile(file.Id, 1);

                Texture.Background = null;
                Button.Style = BootStrapper.Current.Resources["InlineFileButtonStyle"] as Style;
            }
        }

        public void Recycle()
        {
            LifetimeService.Current.Playback.SourceChanged -= OnPlaybackStateChanged;
            LifetimeService.Current.Playback.StateChanged -= OnPlaybackStateChanged;
            LifetimeService.Current.Playback.PositionChanged -= OnPositionChanged;

            _message = null;

            UpdateManager.Unsubscribe(this, ref _fileToken);
            UpdateManager.Unsubscribe(this, ref _thumbnailToken);
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            return content switch
            {
                MessageAudio => true,
                MessageText text when text.LinkPreview != null && !primary => text.LinkPreview.Type is LinkPreviewTypeAudio or LinkPreviewTypeEmbeddedAudioPlayer { Audio: not null },
                MessagePoll poll when poll.Media is PollMediaAudio && !primary => true,
                _ => false,
            };
        }

        private Audio GetContent(MessageViewModel message)
        {
            if (message?.Delegate == null)
            {
                return null;
            }

            var content = message.Content;
            switch (content)
            {
                case MessageAudio audio:
                    return audio.Audio;
                case MessageText text:
                    {
                        if (text.LinkPreview?.Type is LinkPreviewTypeAudio previewAudio)
                        {
                            return previewAudio.Audio;
                        }
                        else if (text.LinkPreview?.Type is LinkPreviewTypeEmbeddedAudioPlayer embeddedAudioPlayer)
                        {
                            return embeddedAudioPlayer.Audio;
                        }

                        break;
                    }
                case MessagePoll poll when poll.Media is PollMediaAudio pollAudio:
                    return pollAudio.Audio;
            }

            return null;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (SettingsService.Current.IsStreamingEnabled)
            {

            }
            else
            {
                Download_Click(null, null);
                return;
            }

            var audio = GetContent(_message);
            if (audio == null)
            {
                return;
            }

            var file = audio.AudioValue;
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
            else
            {
                _message.Delegate.PlayMessage(_message);
            }
        }

        private void Button_DragStarting(UIElement sender, DragStartingEventArgs args)
        {
            MessageHelper.DragStarting(_message, args);
        }

        private void Download_Click(object sender, RoutedEventArgs e)
        {
            var audio = GetContent(_message);
            if (audio == null)
            {
                return;
            }

            var file = audio.AudioValue;
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
                if (_message.AreTheSame(LifetimeService.Current.Playback.CurrentItem))
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
                else
                {
                    _message.Delegate.PlayMessage(_message);
                }
            }
        }
    }
}
