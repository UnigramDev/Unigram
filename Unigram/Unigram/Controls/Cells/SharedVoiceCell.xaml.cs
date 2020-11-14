using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls.Messages.Content;
using Unigram.Converters;
using Unigram.Services;
using Windows.Media.Playback;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls.Cells
{
    public sealed partial class SharedVoiceCell : Grid
    {
        private IPlaybackService _playbackService;
        private IProtoService _protoService;
        private Message _message;
        public Message Message => _message;

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

        public void UpdateMessage(IPlaybackService playbackService, IProtoService protoService, Message message)
        {
            _playbackService = playbackService;
            _protoService = protoService;
            _message = message;

            _playbackService.PropertyChanged -= OnCurrentItemChanged;
            _playbackService.PropertyChanged += OnCurrentItemChanged;
            _playbackService.PlaybackStateChanged -= OnPlaybackStateChanged;
            _playbackService.PlaybackStateChanged += OnPlaybackStateChanged;

            var voiceNote = GetContent(message.Content);
            if (voiceNote == null)
            {
                return;
            }

            if (_protoService.TryGetUser(message.Sender, out User user))
            {
                Title.Text = user.GetFullName();
            }
            else if (_protoService.TryGetChat(message.Sender, out Chat chat))
            {
                Title.Text = chat.Title;
            }
            else
            {
                Title.Text = string.Empty;
            }

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

        private void UpdatePosition()
        {
            var message = _message;
            if (message == null)
            {
                return;
            }

            if (message.AreEqual(_playbackService.CurrentItem) /*&& !_pressed*/)
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

        public void UpdateFile(Message message, File file)
        {
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
                //    _message.ProtoService.DownloadFile(file.Id, 32);
                //}
            }
            else
            {
                if (message.AreEqual(_playbackService.CurrentItem))
                {
                    if (_playbackService.PlaybackState == MediaPlaybackState.Playing)
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
                _protoService.Send(new CancelDownloadFile(file.Id, false));
            }
            else if (file.Remote.IsUploadingActive || _message.SendingState is MessageSendingStateFailed)
            {
                _protoService.Send(new DeleteMessages(_message.ChatId, new[] { _message.Id }, true));
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && !file.Local.IsDownloadingCompleted)
            {
                //_protoService.DownloadFile(file.Id, 32);
                _playbackService.Enqueue(_message);
            }
            else
            {
                if (_message.AreEqual(_playbackService.CurrentItem))
                {
                    if (_playbackService.PlaybackState == MediaPlaybackState.Playing)
                    {
                        _playbackService.Pause();
                    }
                    else
                    {
                        _playbackService.Play();
                    }
                }
                else
                {
                    _playbackService.Enqueue(_message);
                }
            }
        }
    }
}
