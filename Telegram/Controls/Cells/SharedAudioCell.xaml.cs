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

namespace Telegram.Controls.Cells
{
    public sealed partial class SharedAudioCell : Grid
    {
        private IPlaybackService _playbackService;
        private MessageWithOwner _message;
        public MessageWithOwner Message => _message;

        private long _fileToken;

        public SharedAudioCell()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var message = _message;
            if (message == null)
            {
                return;
            }

            UpdateMessage(_playbackService, message);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_playbackService != null)
            {
                _playbackService.SourceChanged -= OnPlaybackStateChanged;
                _playbackService.StateChanged -= OnPlaybackStateChanged;
                _playbackService.PositionChanged -= OnPositionChanged;
            }
        }

        public void UpdateMessage(IPlaybackService playbackService, MessageWithOwner message)
        {
            _playbackService = playbackService;
            _message = message;

            _playbackService.SourceChanged -= OnPlaybackStateChanged;

            var audio = GetContent(message.Content);
            if (audio == null)
            {
                return;
            }

            _playbackService.SourceChanged += OnPlaybackStateChanged;

            Title.Text = audio.GetTitle();

            if (audio.AlbumCoverThumbnail != null)
            {
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

            Button.SetGlyph(0, MessageContentState.Download);
        }

        #region Playback

        private void OnPlaybackStateChanged(IPlaybackService sender, object args)
        {
            var audio = GetContent(_message?.Content);
            if (audio == null)
            {
                return;
            }

            this.BeginOnUIThread(() => UpdateFile(_message, audio.AudioValue));
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

            if (message.AreTheSame(_playbackService.CurrentItem) /*&& !_pressed*/)
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

        private void UpdateFile(MessageWithOwner message, File file)
        {
            _playbackService.StateChanged -= OnPlaybackStateChanged;
            _playbackService.PositionChanged -= OnPositionChanged;

            var audio = GetContent(message.Content);
            if (audio == null)
            {
                return;
            }

            if (audio.AlbumCoverThumbnail != null && audio.AlbumCoverThumbnail.File.Id == file.Id)
            {
                UpdateThumbnail(message, audio.AlbumCoverThumbnail, file);
                return;
            }
            else if (audio.AudioValue.Id != file.Id)
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

                //if (message.Delegate.CanBeDownloaded(message))
                //{
                //    _message.ClientService.DownloadFile(file.Id, 32);
                //}
            }
            else
            {
                DownloadPanel.Visibility = Visibility.Collapsed;

                if (!SettingsService.Current.IsStreamingEnabled)
                {
                    UpdatePlayback(message, audio, file);
                }
            }

            if (SettingsService.Current.IsStreamingEnabled)
            {
                UpdatePlayback(message, audio, file);
            }
        }

        private void UpdatePlayback(MessageWithOwner message, Audio audio, File file)
        {
            if (message.AreTheSame(_playbackService.CurrentItem))
            {
                if (_playbackService.PlaybackState == MediaPlaybackState.Paused)
                {
                    Button.SetGlyph(file.Id, MessageContentState.Play);
                }
                else
                {
                    Button.SetGlyph(file.Id, MessageContentState.Pause);
                }

                UpdatePosition(_playbackService.Position, _playbackService.Duration);

                _playbackService.StateChanged += OnPlaybackStateChanged;
                _playbackService.PositionChanged += OnPositionChanged;
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

        private void UpdateThumbnail(MessageWithOwner message, Thumbnail thumbnail, File file)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                double ratioX = (double)48 / thumbnail.Width;
                double ratioY = (double)48 / thumbnail.Height;
                double ratio = Math.Max(ratioX, ratioY);

                var width = (int)(thumbnail.Width * ratio);
                var height = (int)(thumbnail.Height * ratio);

                Texture.Background = new ImageBrush { ImageSource = new BitmapImage(UriEx.ToLocal(file.Local.Path)) { DecodePixelWidth = width, DecodePixelHeight = height }, Stretch = Stretch.UniformToFill, AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center };
                Button.Style = BootStrapper.Current.Resources["ImmersiveFileButtonStyle"] as Style;
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                message.ClientService.DownloadFile(file.Id, 1);

                Texture.Background = null;
                Button.Style = BootStrapper.Current.Resources["InlineFileButtonStyle"] as Style;
            }
        }

        private Audio GetContent(MessageContent content)
        {
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

            var audio = GetContent(_message?.Content);
            if (audio == null)
            {
                return;
            }

            if (_message.AreTheSame(_playbackService.CurrentItem))
            {
                if (_playbackService.PlaybackState == MediaPlaybackState.Paused)
                {
                    _playbackService.Play();
                }
                else
                {
                    _playbackService.Pause();
                }
            }
            else
            {
                _playbackService.Play(_message);
            }
        }

        private void Download_Click(object sender, RoutedEventArgs e)
        {
            var audio = GetContent(_message?.Content);
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
                if (_message.AreTheSame(_playbackService.CurrentItem))
                {
                    if (_playbackService.PlaybackState == MediaPlaybackState.Paused)
                    {
                        _playbackService.Play();
                    }
                    else
                    {
                        _playbackService.Pause();
                    }
                }
                else
                {
                    _playbackService.Play(_message);
                }
            }
        }
    }
}
