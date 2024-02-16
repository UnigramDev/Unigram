//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml;

namespace Telegram.Controls.Cells
{
    public sealed partial class SharedVoiceCell : GridEx
    {
        private IPlaybackService _playbackService;
        private MessageWithOwner _message;
        public MessageWithOwner Message => _message;

        private long _fileToken;

        public SharedVoiceCell()
        {
            InitializeComponent();
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

            var voiceNote = GetContent(message.Content);
            if (voiceNote == null)
            {
                return;
            }

            _playbackService.SourceChanged += OnPlaybackStateChanged;

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

            UpdateManager.Subscribe(this, message, voiceNote.Voice, ref _fileToken, UpdateFile);
            UpdateFile(message, voiceNote.Voice);
        }

        #region Playback

        private void OnPlaybackStateChanged(IPlaybackService sender, object args)
        {
            var voiceNote = GetContent(_message?.Content);
            if (voiceNote == null)
            {
                return;
            }

            this.BeginOnUIThread(() => UpdateFile(_message, voiceNote.Voice));
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

            var voiceNote = GetContent(message.Content);
            if (voiceNote == null)
            {
                return;
            }

            if (voiceNote.Voice.Id != file.Id)
            {
                return;
            }

            var size = Math.Max(file.Size, file.ExpectedSize);
            if (file.Local.IsDownloadingActive)
            {
                //Button.Glyph = Icons.Cancel;
                Button.SetGlyph(file.Id, MessageContentState.Downloading);
                Button.Progress = (double)file.Local.DownloadedSize / size;

                Subtitle.Text = string.Format("{0} / {1}", FileSizeConverter.Convert(file.Local.DownloadedSize, size), FileSizeConverter.Convert(size));
            }
            else if (file.Remote.IsUploadingActive || message.SendingState is MessageSendingStateFailed)
            {
                //Button.Glyph = Icons.Cancel;
                Button.SetGlyph(file.Id, MessageContentState.Uploading);
                Button.Progress = (double)file.Remote.UploadedSize / size;

                Subtitle.Text = string.Format("{0} / {1}", FileSizeConverter.Convert(file.Remote.UploadedSize, size), FileSizeConverter.Convert(size));
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
            {
                //Button.Glyph = Icons.Download;
                Button.SetGlyph(file.Id, MessageContentState.Download);
                Button.Progress = 0;

                Subtitle.Text = voiceNote.GetDuration() + ", " + FileSizeConverter.Convert(size);

                //if (message.Delegate.CanBeDownloaded(message))
                //{
                //    _message.ClientService.DownloadFile(file.Id, 32);
                //}
            }
            else
            {
                if (message.AreTheSame(_playbackService.CurrentItem))
                {
                    if (_playbackService.PlaybackState == PlaybackState.Paused)
                    {
                        //Button.Glyph = Icons.Play;
                        Button.SetGlyph(file.Id, MessageContentState.Play);
                    }
                    else
                    {
                        //Button.Glyph = Icons.Pause;
                        Button.SetGlyph(file.Id, MessageContentState.Pause);
                    }

                    UpdatePosition(_playbackService.Position, _playbackService.Duration);

                    _playbackService.StateChanged += OnPlaybackStateChanged;
                    _playbackService.PositionChanged += OnPositionChanged;
                }
                else
                {
                    //Button.Glyph = Icons.Play;
                    Button.SetGlyph(file.Id, MessageContentState.Play);
                    Button.Progress = 1;

                    Subtitle.Text = voiceNote.GetDuration();
                }

                Button.Progress = 1;
            }
        }

        private VoiceNote GetContent(MessageContent content)
        {
            if (content is MessageVoiceNote voiceNote)
            {
                return voiceNote.VoiceNote;
            }
            else if (content is MessageText text && text.WebPage != null)
            {
                return text.WebPage.VoiceNote;
            }

            return null;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var voiceNote = GetContent(_message?.Content);
            if (voiceNote == null)
            {
                return;
            }

            var file = voiceNote.Voice;
            if (file.Local.IsDownloadingActive)
            {
                _message.ClientService.Send(new CancelDownloadFile(file.Id, false));
            }
            else if (file.Remote.IsUploadingActive || _message.SendingState is MessageSendingStateFailed)
            {
                _message.ClientService.Send(new DeleteMessages(_message.ChatId, new[] { _message.Id }, true));
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && !file.Local.IsDownloadingCompleted)
            {
                //_clientService.DownloadFile(file.Id, 32);
                _playbackService.Play(_message);
            }
            else
            {
                if (_message.AreTheSame(_playbackService.CurrentItem))
                {
                    if (_playbackService.PlaybackState == PlaybackState.Paused)
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
