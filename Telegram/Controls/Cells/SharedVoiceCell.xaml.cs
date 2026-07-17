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
using Telegram.Native.Controls;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Cells
{
    public sealed partial class SharedVoiceCell : GridEx
    {
        private MessageWithOwner _message;
        public MessageWithOwner Message => _message;

        private long _fileToken;
        private long _thumbnailToken;

        public SharedVoiceCell()
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
            TextRoot.Opacity = 0;
        }

        public void UpdateMessage(MessageWithOwner message)
        {
            if (_hidden)
            {
                _hidden = false;
                ButtonRoot.Opacity = 1;
                TextRoot.Opacity = 1;
            }

            _message = message;

            LifetimeService.Current.Playback.SourceChanged -= OnPlaybackStateChanged;

            var file = message.GetFile();
            if (file == null)
            {
                return;
            }

            // Subscribe to the session-lived Playback service only while connected; OnLoaded
            // re-runs UpdateMessage so this fires once loaded. Subscribing on bind leaked cells
            // prepared but never loaded (so OnUnloaded never ran).
            if (IsConnected)
            {
                LifetimeService.Current.Playback.SourceChanged += OnPlaybackStateChanged;
            }

            if (message.ClientService.TryGetUser(message.SenderId, out User user))
            {
                Title.Text = user.FullName();
            }
            else if (message.ClientService.TryGetChat(message.SenderId, out Chat chat))
            {
                Title.Text = chat.Title;
            }
            else
            {
                Title.Text = string.Empty;
            }

            if (message.Content is MessageVideoNote videoNote && videoNote.VideoNote.Thumbnail != null)
            {
                UpdateManager.Subscribe(this, message, videoNote.VideoNote.Thumbnail.File, ref _thumbnailToken, UpdateThumbnail, true);
                UpdateThumbnail(message, videoNote.VideoNote.Thumbnail, videoNote.VideoNote.Thumbnail.File);
            }
            else
            {
                Texture.Background = null;
                Button.Style = BootStrapper.Current.Resources["InlineFileButtonStyle"] as Style;
            }

            UpdateManager.Subscribe(this, message, file, ref _fileToken, UpdateFile);
            UpdateFile(message, file);
        }

        #region Playback

        private void OnPlaybackStateChanged(IPlaybackService sender, object args)
        {
            var file = _message?.GetFile();
            if (file == null)
            {
                return;
            }

            this.BeginOnUIThread(() => UpdateFile(_message, file));
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
            UpdateFile(_message, file);
        }

        private void UpdateFile(MessageWithOwner message, File file)
        {
            LifetimeService.Current.Playback.StateChanged -= OnPlaybackStateChanged;
            LifetimeService.Current.Playback.PositionChanged -= OnPositionChanged;

            if (message.AreTheSame(LifetimeService.Current.Playback.CurrentItem))
            {
                if (LifetimeService.Current.Playback.PlaybackState == PlaybackState.Paused)
                {
                    //Button.Glyph = Icons.Play;
                    Button.SetGlyph(file.Id, MessageContentState.Play);
                }
                else
                {
                    //Button.Glyph = Icons.Pause;
                    Button.SetGlyph(file.Id, MessageContentState.Pause);
                }

                UpdatePosition(LifetimeService.Current.Playback.Position, LifetimeService.Current.Playback.Duration);

                if (IsConnected)
                {
                    LifetimeService.Current.Playback.StateChanged += OnPlaybackStateChanged;
                    LifetimeService.Current.Playback.PositionChanged += OnPositionChanged;
                }
            }
            else
            {
                //Button.Glyph = Icons.Play;
                Button.SetGlyph(file.Id, MessageContentState.Play);
                Button.Progress = 1;

                if (TryGetVoiceNote(message.Content, out VoiceNote voiceNote))
                {
                    Subtitle.Text = string.Format("{0} {1} {2}", voiceNote.GetDuration(), Icons.Bullet, Formatter.DateAt(message.Date));
                }
                else if (TryGetVideoNote(message.Content, out VideoNote videoNote))
                {
                    Subtitle.Text = string.Format("{0} {1} {2}", videoNote.GetDuration(), Icons.Bullet, Formatter.DateAt(message.Date));
                }
            }

            Button.Progress = 1;
        }

        private void UpdateThumbnail(object target, File file)
        {
            if (TryGetVideoNote(_message?.Content, out VideoNote videoNote))
            {
                UpdateThumbnail(_message, videoNote.Thumbnail, file);
            }
        }

        private void UpdateThumbnail(MessageWithOwner message, Thumbnail thumbnail, File file)
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
                message.ClientService.DownloadFile(file.Id, 1);

                Texture.Background = null;
                Button.Style = BootStrapper.Current.Resources["InlineFileButtonStyle"] as Style;
            }
        }

        private bool TryGetVoiceNote(MessageContent content, out VoiceNote voice)
        {
            if (content is MessageVoiceNote voiceNote)
            {
                voice = voiceNote.VoiceNote;
                return true;
            }
            else if (content is MessageText text && text.LinkPreview?.Type is LinkPreviewTypeVoiceNote previewVoiceNote)
            {
                voice = previewVoiceNote.VoiceNote;
                return true;
            }

            voice = null;
            return false;
        }

        private bool TryGetVideoNote(MessageContent content, out VideoNote video)
        {
            if (content is MessageVideoNote videoNote)
            {
                video = videoNote.VideoNote;
                return true;
            }
            else if (content is MessageText text && text.LinkPreview?.Type is LinkPreviewTypeVideoNote previewVideoNote)
            {
                video = previewVideoNote.VideoNote;
                return true;
            }

            video = null;
            return false;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var file = _message?.GetFile();
            if (file == null)
            {
                return;
            }

            if (file.Local.IsDownloadingActive)
            {
                _message.ClientService.CancelDownloadFile(file, false);
            }
            else if (file.Remote.IsUploadingActive || _message.SendingState is MessageSendingStateFailed)
            {
                _message.ClientService.Send(new DeleteMessages(_message.ChatId, new[] { _message.Id }, true));
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && !file.Local.IsDownloadingCompleted)
            {
                if (_message.Content is MessageAudio)
                {
                    LifetimeService.Current.Playback.Play(XamlRoot, _message);

                }
                else
                {
                    _message.ClientService.DownloadFile(file.Id, 32);
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
