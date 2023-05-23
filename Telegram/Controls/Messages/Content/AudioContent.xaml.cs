//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Media.Playback;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Controls.Messages.Content
{
    public sealed class AudioContent : Control, IContent
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        private long _fileToken;
        private long _thumbnailToken;

        public AudioContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(AudioContent);
            Unloaded += OnUnloaded;
        }

        public AudioContent()
        {
            DefaultStyleKey = typeof(AudioContent);
        }

        #region InitializeComponent

        private Border Texture;
        private FileButton Button;
        private Grid DownloadPanel;
        private FileButton Download;
        private TextBlock Title;
        private TextBlock Subtitle;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            Texture = GetTemplateChild(nameof(Texture)) as Border;
            Button = GetTemplateChild(nameof(Button)) as FileButton;
            DownloadPanel = GetTemplateChild(nameof(DownloadPanel)) as Grid;
            Download = GetTemplateChild(nameof(Download)) as FileButton;
            Title = GetTemplateChild(nameof(Title)) as TextBlock;
            Subtitle = GetTemplateChild(nameof(Subtitle)) as TextBlock;

            Button.Click += Button_Click;
            Download.Click += Download_Click;

            _templateApplied = true;

            if (_message != null)
            {
                UpdateMessage(_message);
            }
        }

        #endregion

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_message != null)
            {
                _message.PlaybackService.SourceChanged -= OnPlaybackStateChanged;
                _message.PlaybackService.StateChanged -= OnPlaybackStateChanged;
                _message.PlaybackService.PositionChanged -= OnPositionChanged;
            }
        }

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;

            message.PlaybackService.SourceChanged -= OnPlaybackStateChanged;

            var audio = GetContent(message);
            if (audio == null || !_templateApplied)
            {
                return;
            }

            message.PlaybackService.SourceChanged += OnPlaybackStateChanged;

            Title.Text = audio.GetTitle();

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
                    Recycle(sender);
                    return;
                }

                UpdateFile(_message, audio.AudioValue);
            });
        }

        private void OnPositionChanged(IPlaybackService sender, object args)
        {
            var position = sender.Position;
            var duration = sender.Duration;

            this.BeginOnUIThread(() => UpdatePosition(position, duration));
        }

        private void UpdatePosition(TimeSpan position, TimeSpan duration)
        {
            var message = _message;
            if (message == null)
            {
                return;
            }

            if (message.AreTheSame(message.PlaybackService.CurrentItem) /*&& !_pressed*/)
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
            UpdateFile(_message, file);
        }

        private void UpdateFile(MessageViewModel message, File file)
        {
            var audio = GetContent(message);
            if (audio == null || !_templateApplied)
            {
                return;
            }

            message.PlaybackService.StateChanged -= OnPlaybackStateChanged;
            message.PlaybackService.PositionChanged -= OnPositionChanged;

            if (audio.AudioValue.Id != file.Id)
            {
                return;
            }

            var size = Math.Max(file.Size, file.ExpectedSize);
            if (file.Local.IsDownloadingActive)
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

                target.SetGlyph(file.Id, MessageContentState.Downloading);
                target.Progress = (double)file.Local.DownloadedSize / size;

                Subtitle.Text = string.Format("{0} / {1}", FileSizeConverter.Convert(file.Local.DownloadedSize, size), FileSizeConverter.Convert(size));
            }
            else if (file.Remote.IsUploadingActive || message.SendingState is MessageSendingStateFailed)
            {
                DownloadPanel.Visibility = Visibility.Collapsed;

                Button.SetGlyph(file.Id, MessageContentState.Uploading);
                Button.Progress = (double)file.Remote.UploadedSize / size;

                Subtitle.Text = string.Format("{0} / {1}", FileSizeConverter.Convert(file.Remote.UploadedSize, size), FileSizeConverter.Convert(size));
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
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

                if (message.Delegate.CanBeDownloaded(audio, file))
                {
                    _message.ClientService.DownloadFile(file.Id, 32);
                }
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
            if (message.AreTheSame(message.PlaybackService.CurrentItem))
            {
                if (message.PlaybackService.PlaybackState == MediaPlaybackState.Paused)
                {
                    Button.SetGlyph(file.Id, MessageContentState.Play);
                }
                else
                {
                    Button.SetGlyph(file.Id, MessageContentState.Pause);
                }

                UpdatePosition(message.PlaybackService.Position, message.PlaybackService.Duration);

                message.PlaybackService.StateChanged += OnPlaybackStateChanged;
                message.PlaybackService.PositionChanged += OnPositionChanged;
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
                    Texture.Background = new ImageBrush { ImageSource = new BitmapImage(UriEx.ToLocal(file.Local.Path)) { DecodePixelWidth = width, DecodePixelHeight = height }, Stretch = Stretch.UniformToFill, AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center };
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
            Recycle(_message?.PlaybackService);
        }

        private void Recycle(object sender)
        {
            if (sender is IPlaybackService playback)
            {
                playback.SourceChanged -= OnPlaybackStateChanged;
                playback.StateChanged -= OnPlaybackStateChanged;
                playback.PositionChanged -= OnPositionChanged;
            }

            _message = null;

            UpdateManager.Unsubscribe(this, ref _fileToken);
            UpdateManager.Unsubscribe(this, ref _thumbnailToken, true);
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            if (content is MessageAudio)
            {
                return true;
            }
            else if (content is MessageText text && text.WebPage != null && !primary)
            {
                return text.WebPage.Audio != null;
            }

            return false;
        }

        private Audio GetContent(MessageViewModel message)
        {
            if (message?.Delegate == null)
            {
                return null;
            }

            var content = message.Content;
            if (content is MessageAudio audio)
            {
                return audio.Audio;
            }
            else if (content is MessageText text && text.WebPage != null)
            {
                return text.WebPage.Audio;
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
                _message.ClientService.Send(new DeleteMessages(_message.ChatId, new[] { _message.Id }, true));
            }
            else if (_message.AreTheSame(_message.PlaybackService.CurrentItem))
            {
                if (_message.PlaybackService.PlaybackState == MediaPlaybackState.Paused)
                {
                    _message.PlaybackService.Play();
                }
                else
                {
                    _message.PlaybackService.Pause();
                }
            }
            else
            {
                _message.Delegate.PlayMessage(_message);
            }
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
                _message.ClientService.Send(new DeleteMessages(_message.ChatId, new[] { _message.Id }, true));
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
                if (_message.AreTheSame(_message.PlaybackService.CurrentItem))
                {
                    if (_message.PlaybackService.PlaybackState == MediaPlaybackState.Paused)
                    {
                        _message.PlaybackService.Play();
                    }
                    else
                    {
                        _message.PlaybackService.Pause();
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
