using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Services;
using Unigram.ViewModels;
using Windows.Media.Playback;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls.Cells
{
    public sealed partial class SharedVoiceCell : Grid
    {
        private IPlaybackService _playbackService;
        private MessageWithOwner _message;
        public MessageWithOwner Message => _message;

        private string _fileToken;

        public SharedVoiceCell()
        {
            InitializeComponent();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            var message = _message;
            if (message == null)
            {
                return;
            }

            _playbackService.PropertyChanged -= OnCurrentItemChanged;
            _playbackService.PlaybackStateChanged -= OnPlaybackStateChanged;
            _playbackService.PositionChanged -= OnPositionChanged;
        }

        public void UpdateMessage(IPlaybackService playbackService, MessageWithOwner message)
        {
            _playbackService = playbackService;
            _message = message;

            _playbackService.PropertyChanged -= OnCurrentItemChanged;
            _playbackService.PropertyChanged += OnCurrentItemChanged;

            var voiceNote = GetContent(message.Content);
            if (voiceNote == null)
            {
                return;
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

            UpdateManager.Subscribe(this, message, voiceNote.Voice, ref _fileToken, UpdateFile);
            UpdateFile(message, voiceNote.Voice);
        }

        #region Playback

        private void OnCurrentItemChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var voiceNote = GetContent(_message?.Content);
            if (voiceNote == null)
            {
                return;
            }

            this.BeginOnUIThread(() => UpdateFile(_message, voiceNote.Voice));
        }

        private void OnPlaybackStateChanged(IPlaybackService sender, object args)
        {
            var voiceNote = GetContent(_message?.Content);
            if (voiceNote == null)
            {
                return;
            }

            this.BeginOnUIThread(() => UpdateFile(_message, voiceNote.Voice));
        }

        private void OnPositionChanged(IPlaybackService sender, object args)
        {
            this.BeginOnUIThread(UpdatePosition);
        }

        private void UpdatePosition()
        {
            var message = _message;
            if (message == null)
            {
                return;
            }

            if (message.AreTheSame(_playbackService.CurrentItem) /*&& !_pressed*/)
            {
                Subtitle.Text = FormatTime(_playbackService.Position) + " / " + FormatTime(_playbackService.Duration);
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
            _playbackService.PlaybackStateChanged -= OnPlaybackStateChanged;
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
                    if (_playbackService.PlaybackState == MediaPlaybackState.Paused)
                    {
                        //Button.Glyph = Icons.Play;
                        Button.SetGlyph(file.Id, MessageContentState.Play);
                    }
                    else
                    {
                        //Button.Glyph = Icons.Pause;
                        Button.SetGlyph(file.Id, MessageContentState.Pause);
                    }

                    UpdatePosition();

                    _playbackService.PlaybackStateChanged += OnPlaybackStateChanged;
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
