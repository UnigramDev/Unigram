using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;
using Windows.Media.Playback;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls.Messages.Content
{
    public sealed partial class VoiceNoteContent : Grid, IContentWithFile
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        public VoiceNoteContent(MessageViewModel message)
        {
            InitializeComponent();
            UpdateMessage(message);
        }

        public VoiceNoteContent()
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

            message.PlaybackService.PropertyChanged -= OnCurrentItemChanged;
            message.PlaybackService.PlaybackStateChanged -= OnPlaybackStateChanged;
            message.PlaybackService.PositionChanged -= OnPositionChanged;
        }

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;

            message.PlaybackService.PropertyChanged -= OnCurrentItemChanged;
            message.PlaybackService.PropertyChanged += OnCurrentItemChanged;
            message.PlaybackService.PlaybackStateChanged -= OnPlaybackStateChanged;
            message.PlaybackService.PlaybackStateChanged += OnPlaybackStateChanged;

            var voiceNote = GetContent(message.Content);
            if (voiceNote == null)
            {
                return;
            }

            Progress.UpdateWave(voiceNote);

            //UpdateDuration();
            UpdateFile(message, voiceNote.Voice);
        }

        public void Mockup(MessageVoiceNote voiceNote)
        {
            Progress.UpdateWave(voiceNote.VoiceNote);
            Progress.Minimum = 0;
            Progress.Maximum = 1;
            Progress.Value = 0.3;

            Subtitle.Text = FormatTime(TimeSpan.FromSeconds(1)) + " / " + FormatTime(TimeSpan.FromSeconds(3));

            Button.SetGlyph(0, MessageContentState.Pause);
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

        private void OnPlaybackStateChanged(MediaPlaybackSession sender, object args)
        {
            var voiceNote = GetContent(_message?.Content);
            if (voiceNote == null)
            {
                return;
            }

            this.BeginOnUIThread(() => UpdateFile(_message, voiceNote.Voice));
        }

        private void OnPositionChanged(MediaPlaybackSession sender, object args)
        {
            this.BeginOnUIThread(UpdatePosition);
        }

        private void UpdateDuration()
        {
            var message = _message;
            if (message == null)
            {
                return;
            }

            var voiceNote = GetContent(message.Content);
            if (voiceNote == null)
            {
                return;
            }

            if (message.Content is MessageVoiceNote voiceNoteMessage)
            {
                Subtitle.Text = voiceNote.GetDuration() + (voiceNoteMessage.IsListened ? string.Empty : " ●");
                Progress.Maximum = voiceNote.Duration;
                Progress.Value = message.IsOutgoing || voiceNoteMessage.IsListened ? 0 : voiceNote.Duration;
            }
            else
            {
                Subtitle.Text = voiceNote.GetDuration();
                Progress.Maximum = voiceNote.Duration;
                Progress.Value = 0;
            }
        }

        private void UpdatePosition()
        {
            var message = _message;
            if (message == null)
            {
                return;
            }

            if (message.Equals(message.PlaybackService.CurrentItem) /*&& !_pressed*/)
            {
                Subtitle.Text = FormatTime(message.PlaybackService.Position) + " / " + FormatTime(message.PlaybackService.Duration);
                Progress.Maximum = /*Slider.Maximum =*/ message.PlaybackService.Duration.TotalMilliseconds;
                Progress.Value = /*Slider.Value =*/ message.PlaybackService.Position.TotalMilliseconds;
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

        public void UpdateMessageContentOpened(MessageViewModel message)
        {
            UpdateDuration();
        }

        public void UpdateFile(MessageViewModel message, File file)
        {
            message.PlaybackService.PositionChanged -= OnPositionChanged;

            var voiceNote = GetContent(message.Content);
            if (voiceNote == null)
            {
                return;
            }

            else if (voiceNote.Voice.Id != file.Id)
            {
                return;
            }

            var size = Math.Max(file.Size, file.ExpectedSize);
            if (file.Local.IsDownloadingActive)
            {
                //Button.Glyph = Icons.Cancel;
                Button.SetGlyph(file.Id, MessageContentState.Downloading);
                Button.Progress = (double)file.Local.DownloadedSize / size;
            }
            else if (file.Remote.IsUploadingActive || message.SendingState is MessageSendingStateFailed)
            {
                //Button.Glyph = Icons.Cancel;
                Button.SetGlyph(file.Id, MessageContentState.Uploading);
                Button.Progress = (double)file.Remote.UploadedSize / size;
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
            {
                //Button.Glyph = Icons.Download;
                Button.SetGlyph(file.Id, MessageContentState.Download);
                Button.Progress = 0;

                if (message.Delegate.CanBeDownloaded(message))
                {
                    _message.ProtoService.DownloadFile(file.Id, 32);
                }
            }
            else
            {
                if (Equals(message, message.PlaybackService.CurrentItem))
                {
                    if (message.PlaybackService.PlaybackState == MediaPlaybackState.Playing)
                    {
                        //Button.Glyph = Icons.Pause;
                        Button.SetGlyph(file.Id, MessageContentState.Pause);
                    }
                    else
                    {
                        //Button.Glyph = Icons.Play;
                        Button.SetGlyph(file.Id, MessageContentState.Play);
                    }

                    UpdatePosition();
                    message.PlaybackService.PositionChanged += OnPositionChanged;
                }
                else
                {
                    //Button.Glyph = Icons.Play;
                    Button.SetGlyph(file.Id, MessageContentState.Play);
                    UpdateDuration();
                }

                Button.Progress = 1;
            }
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            if (content is MessageVoiceNote)
            {
                return true;
            }
            else if (content is MessageText text && text.WebPage != null && !primary)
            {
                return text.WebPage.VoiceNote != null;
            }

            return false;
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
                _message.ProtoService.CancelDownloadFile(file.Id);
            }
            else if (file.Remote.IsUploadingActive || _message.SendingState is MessageSendingStateFailed)
            {
                _message.ProtoService.Send(new DeleteMessages(_message.ChatId, new[] { _message.Id }, true));
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && !file.Local.IsDownloadingCompleted)
            {
                //_message.ProtoService.DownloadFile(file.Id, 32);
                _message.PlaybackService.Enqueue(_message.Get());
            }
            else
            {
                if (_message.Equals(_message.PlaybackService.CurrentItem))
                {
                    if (_message.PlaybackService.PlaybackState == MediaPlaybackState.Playing)
                    {
                        _message.PlaybackService.Pause();
                    }
                    else
                    {
                        _message.PlaybackService.Play();
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
