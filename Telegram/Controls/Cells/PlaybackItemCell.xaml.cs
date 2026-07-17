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
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Cells
{
    public sealed partial class PlaybackItemCell : GridEx
    {
        private PlaybackItem _item;
        public PlaybackItem Item => _item;

        private long _fileToken;
        private long _thumbnailToken;

        public PlaybackItemCell()
        {
            InitializeComponent();
        }

        protected override void OnLoaded()
        {
            var message = _item;
            if (message == null)
            {
                return;
            }

            UpdateItem(message);
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

        public void UpdateItem(PlaybackItem item)
        {
            if (_hidden)
            {
                _hidden = false;
                ButtonRoot.Opacity = 1;
                DownloadRoot.Opacity = 1;
                TextRoot.Opacity = 1;
            }

            _item = item;

            LifetimeService.Current.Playback.SourceChanged -= OnPlaybackStateChanged;

            var audio = GetContent(item);
            if (audio == null)
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
                UpdateManager.Subscribe(this, item.ClientService, audio.AlbumCoverThumbnail.File, ref _thumbnailToken, UpdateThumbnail, true);
                UpdateThumbnail(item, audio.AlbumCoverThumbnail, audio.AlbumCoverThumbnail.File);
            }
            else
            {
                Texture.Background = null;
                Button.Style = BootStrapper.Current.Resources["InlineFileButtonStyle"] as Style;
            }

            UpdateManager.Subscribe(this, item.ClientService, audio.AudioValue, ref _fileToken, UpdateFile);
            UpdateFile(item, audio.AudioValue);
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
            var audio = GetContent(_item);
            if (audio == null)
            {
                return;
            }

            this.BeginOnUIThread(() => UpdateFile(_item, audio.AudioValue));
        }

        private void OnPositionChanged(IPlaybackService sender, PlaybackPositionChangedEventArgs args)
        {
            var position = args.Position;
            var duration = args.Duration;

            this.BeginOnUIThread(() => UpdatePosition(position, duration));
        }

        private void UpdatePosition(TimeSpan position, TimeSpan duration)
        {
            var message = _item;
            if (message == null)
            {
                return;
            }

            if (message == LifetimeService.Current.Playback.CurrentItem /*&& !_pressed*/)
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
            if (_item.AreTheSame(LifetimeService.Current.Playback.CurrentItem))
            {
                return;
            }

            UpdateFile(_item, file);
        }

        private void UpdateFile(PlaybackItem item, File file)
        {
            LifetimeService.Current.Playback.StateChanged -= OnPlaybackStateChanged;
            LifetimeService.Current.Playback.PositionChanged -= OnPositionChanged;

            var audio = GetContent(item);
            if (audio == null)
            {
                return;
            }

            if (audio.AlbumCoverThumbnail != null && audio.AlbumCoverThumbnail.File.Id == file.Id)
            {
                UpdateThumbnail(item, audio.AlbumCoverThumbnail, file);
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
            else if (file.Remote.IsUploadingActive)
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
                    UpdatePlayback(item, audio, file);
                }
            }

            if (SettingsService.Current.IsStreamingEnabled)
            {
                UpdatePlayback(item, audio, file);
            }
        }

        private void UpdatePlayback(PlaybackItem item, Audio audio, File file)
        {
            if (item.AreTheSame(LifetimeService.Current.Playback.CurrentItem))
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
            var audio = GetContent(_item);
            if (audio == null /*|| !_templateApplied*/)
            {
                return;
            }

            UpdateThumbnail(_item, audio.AlbumCoverThumbnail, file);
        }

        private void UpdateThumbnail(PlaybackItem item, Thumbnail thumbnail, File file)
        {
            if (thumbnail?.File.Id != file.Id)
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
                item.ClientService.DownloadFile(file.Id, 1);

                Texture.Background = null;
                Button.Style = BootStrapper.Current.Resources["InlineFileButtonStyle"] as Style;
            }
        }

        private Audio GetContent(PlaybackItem item)
        {
            if (item is PlaybackItemMessage message)
            {
                if (message.Message.Content is MessageAudio audio)
                {
                    return audio.Audio;
                }
                else if (message.Message.Content is MessageText text && text.LinkPreview?.Type is LinkPreviewTypeAudio previewAudio)
                {
                    return previewAudio.Audio;
                }
            }
            else if (item is PlaybackItemProfileAudio audio)
            {
                return new Audio(audio.Audio.Duration, audio.Audio.Title, audio.Audio.Performer, audio.Audio.FileName, audio.Audio.MimeType, audio.Audio.AlbumCoverMinithumbnail, audio.Audio.AlbumCoverThumbnail, audio.Audio.ExternalAlbumCovers, audio.Audio.AudioValue);
            }

            return null;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Click();
        }

        public void Click()
        {
            if (SettingsService.Current.IsStreamingEnabled)
            {

            }
            else
            {
                Download_Click(null, null);
                return;
            }

            var audio = GetContent(_item);
            if (audio == null)
            {
                return;
            }

            if (_item.AreTheSame(LifetimeService.Current.Playback.CurrentItem))
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
                LifetimeService.Current.Playback.Play(_item);
            }
        }

        private void Download_Click(object sender, RoutedEventArgs e)
        {
            var audio = GetContent(_item);
            if (audio == null)
            {
                return;
            }

            var file = audio.AudioValue;
            if (file.Local.IsDownloadingActive)
            {
                _item.ClientService.CancelDownloadFile(file);
            }
            else if (file.Remote.IsUploadingActive)
            {
                if (_item is PlaybackItemMessage message)
                {
                    _item.ClientService.Send(new DeleteMessages(message.ChatId, new[] { message.Id }, true));
                }
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && !file.Local.IsDownloadingCompleted)
            {
                if (_item is PlaybackItemMessage message && message.Message.CanBeAddedToDownloads)
                {
                    _item.ClientService.AddFileToDownloads(file, message.ChatId, message.Id);
                }
                else
                {
                    _item.ClientService.DownloadFile(file.Id, 30);
                }
            }
            else
            {
                if (_item.AreTheSame(LifetimeService.Current.Playback.CurrentItem))
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
                    LifetimeService.Current.Playback.Play(_item);
                }
            }
        }
    }
}
