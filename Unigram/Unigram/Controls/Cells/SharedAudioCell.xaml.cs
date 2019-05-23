using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls.Messages.Content;
using Unigram.Converters;
using Unigram.Services;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Playback;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Controls.Cells
{
    public sealed partial class SharedAudioCell : Grid
    {
        private IPlaybackService _playbackService;
        private IProtoService _protoService;
        private Message _message;
        public Message Message => _message;

        public SharedAudioCell()
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

            var audio = GetContent(message.Content);
            if (audio == null)
            {
                return;
            }

            Title.Text = audio.GetTitle();

            if (audio.AlbumCoverThumbnail != null)
            {
                UpdateThumbnail(message, audio.AlbumCoverThumbnail, audio.AlbumCoverThumbnail.Photo);
            }
            else
            {
                Texture.Background = null;
                Button.Style = App.Current.Resources["InlineFileButtonStyle"] as Style;
            }

            UpdateFile(message, audio.AudioValue);
        }

        public void Mockup(MessageAudio audio)
        {
            Title.Text = audio.Audio.GetTitle();
            Subtitle.Text = audio.Audio.GetDuration() + ", " + FileSizeConverter.Convert(4190000);

            Button.SetGlyph(0, MessageContentState.Download);
        }

        #region Playback

        private void OnCurrentItemChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var audio = GetContent(_message?.Content);
            if (audio == null)
            {
                return;
            }

            this.BeginOnUIThread(() => UpdateFile(_message, audio.AudioValue));
        }

        private void OnPlaybackStateChanged(MediaPlaybackSession sender, object args)
        {
            var audio = GetContent(_message?.Content);
            if (audio == null)
            {
                return;
            }

            this.BeginOnUIThread(() => UpdateFile(_message, audio.AudioValue));
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

            var audio = GetContent(message.Content);
            if (audio == null)
            {
                return;
            }

            if (message.Content is MessageAudio audioMessage)
            {
                Subtitle.Text = audio.GetDuration();
            }
            else
            {
                Subtitle.Text = audio.GetDuration();
            }
        }

        private void UpdatePosition()
        {
            var message = _message;
            if (message == null)
            {
                return;
            }

            if (message.Equals(_playbackService.CurrentItem) /*&& !_pressed*/)
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
            var audio = GetContent(message.Content);
            if (audio == null)
            {
                return;
            }

            if (audio.AlbumCoverThumbnail != null && audio.AlbumCoverThumbnail.Photo.Id == file.Id)
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

                Subtitle.Text = audio.GetDuration() + ", " + FileSizeConverter.Convert(size);

                //if (message.Delegate.CanBeDownloaded(message))
                //{
                //    _message.ProtoService.DownloadFile(file.Id, 32);
                //}
            }
            else
            {
                if (Equals(message, _playbackService.CurrentItem))
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

                    Subtitle.Text = audio.GetDuration();
                }

                Button.Progress = 1;
            }
        }

        private void UpdateThumbnail(Message message, PhotoSize photoSize, File file)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                double ratioX = (double)48 / photoSize.Width;
                double ratioY = (double)48 / photoSize.Height;
                double ratio = Math.Max(ratioX, ratioY);

                var width = (int)(photoSize.Width * ratio);
                var height = (int)(photoSize.Height * ratio);

                Texture.Background = new ImageBrush { ImageSource = new BitmapImage(new Uri("file:///" + file.Local.Path)) { DecodePixelWidth = width, DecodePixelHeight = height }, Stretch = Stretch.UniformToFill, AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center };
                Button.Style = App.Current.Resources["ImmersiveFileButtonStyle"] as Style;
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                _protoService.DownloadFile(file.Id, 1);

                Texture.Background = null;
                Button.Style = App.Current.Resources["InlineFileButtonStyle"] as Style;
            }
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
            var audio = GetContent(_message?.Content);
            if (audio == null)
            {
                return;
            }

            var file = audio.AudioValue;
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
                if (_message.Equals(_playbackService.CurrentItem))
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
