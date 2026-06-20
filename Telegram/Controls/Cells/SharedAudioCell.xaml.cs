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
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Cells
{
    public sealed partial class SharedAudioCell : GridEx
    {
        private MessageWithOwner _message;
        public MessageWithOwner Message => _message;

        private long _fileToken;
        private long _thumbnailToken;

        public SharedAudioCell()
        {
            InitializeComponent();
        }

        protected override void OnLoaded()
        {
            var message = _message;
            if (message == null)
            {
                return;
            }

            UpdateMessage(message);
        }

        protected override void OnUnloaded()
        {
            LifetimeService.Current.Playback.SourceChanged -= OnPlaybackStateChanged;
            LifetimeService.Current.Playback.StateChanged -= OnPlaybackStateChanged;
            LifetimeService.Current.Playback.PositionChanged -= OnPositionChanged;
        }

        private bool _hidden;

        public void Hide()
        {
            if (_hidden)
            {
                return;
            }

            _hidden = true;
            ButtonRoot.Opacity = 0;
            DownloadRoot.Opacity = 0;
            TextRoot.Opacity = 0;
        }

        public void UpdateMessage(MessageWithOwner message)
        {
            if (_hidden)
            {
                _hidden = false;
                ButtonRoot.Opacity = 1;
                DownloadRoot.Opacity = 1;
                TextRoot.Opacity = 1;
            }

            _message = message;

            LifetimeService.Current.Playback.SourceChanged -= OnPlaybackStateChanged;

            var audio = GetContent(message.Content);
            if (audio == null)
            {
                return;
            }

            LifetimeService.Current.Playback.SourceChanged += OnPlaybackStateChanged;

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

        private void UpdateFile(MessageWithOwner message, File file)
        {
            LifetimeService.Current.Playback.StateChanged -= OnPlaybackStateChanged;
            LifetimeService.Current.Playback.PositionChanged -= OnPositionChanged;

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
                    DownloadRoot.Visibility = Visibility.Visible;
                }
                else
                {
                    target = Button;
                    DownloadRoot.Visibility = Visibility.Collapsed;
                }

                target.SetGlyph(file.Id, MessageContentState.Downloading);
                target.Progress = (double)file.Local.DownloadedSize / size;

                Subtitle.Text = string.Format("{0} / {1}", FileSizeConverter.Convert(file.Local.DownloadedSize, size), FileSizeConverter.Convert(size));
            }
            else if (file.Remote.IsUploadingActive || message.SendingState is MessageSendingStateFailed || (message.SendingState is MessageSendingStatePending && !file.Remote.IsUploadingCompleted))
            {
                DownloadRoot.Visibility = Visibility.Collapsed;

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
                    DownloadRoot.Visibility = Visibility.Visible;
                }
                else
                {
                    target = Button;
                    DownloadRoot.Visibility = Visibility.Collapsed;
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
                DownloadRoot.Visibility = Visibility.Collapsed;

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

                DownloadRoot.Visibility = Visibility.Collapsed;

                UpdatePosition(LifetimeService.Current.Playback.Position, LifetimeService.Current.Playback.Duration);

                LifetimeService.Current.Playback.StateChanged += OnPlaybackStateChanged;
                LifetimeService.Current.Playback.PositionChanged += OnPositionChanged;
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
            var audio = GetContent(_message?.Content);
            if (audio == null /*|| !_templateApplied*/)
            {
                return;
            }

            UpdateThumbnail(_message, audio.AlbumCoverThumbnail, file);
        }

        private void UpdateThumbnail(MessageWithOwner message, Thumbnail thumbnail, File file)
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

        private Audio GetContent(MessageContent content)
        {
            if (content is MessageAudio audio)
            {
                return audio.Audio;
            }
            else if (content is MessageText text && text.LinkPreview?.Type is LinkPreviewTypeAudio previewAudio)
            {
                return previewAudio.Audio;
            }
            else if (content is MessageRichMessage richMessage)
            {
                var block = PageBlockHelper.FindFirstMedia(richMessage.Message.Blocks, PageBlockMediaKind.Audio);
                if (block is PageBlockAudio blockAudio)
                {
                    return blockAudio.Audio;
                }
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
                LifetimeService.Current.Playback.Play(XamlRoot, _message);
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
                    LifetimeService.Current.Playback.Play(XamlRoot, _message);
                }
            }
        }
    }
}
