using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Navigation;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Unigram.Services
{
    public interface IPlaybackService : INotifyPropertyChanged
    {
        IReadOnlyList<PlaybackItem> Items { get; }

        MessageWithOwner CurrentItem { get; }

        double PlaybackRate { get; set; }

        double Volume { get; set; }

        void Pause();
        void Play();

        void MoveNext();
        void MovePrevious();

        void Seek(TimeSpan span);

        void Clear();

        void Play(MessageWithOwner message, long threadId = 0);

        TimeSpan Position { get; }
        TimeSpan Duration { get; }

        MediaPlaybackState PlaybackState { get; }



        bool? IsRepeatEnabled { get; set; }
        bool IsShuffleEnabled { get; set; }
        bool IsReversed { get; set; }



        event TypedEventHandler<IPlaybackService, MediaPlayerFailedEventArgs> MediaFailed;
        event TypedEventHandler<IPlaybackService, object> PlaybackStateChanged;
        event TypedEventHandler<IPlaybackService, object> PositionChanged;
        event EventHandler PlaylistChanged;
    }

    public class PlaybackService : BindableBase, IPlaybackService
    {
        private readonly ISettingsService _settingsService;

        private MediaPlayer _mediaPlayer;

        private readonly Dictionary<string, PlaybackItem> _mapping;

        private long _threadId;

        private List<PlaybackItem> _items;
        private Queue<Message> _queue;

        public event TypedEventHandler<IPlaybackService, MediaPlayerFailedEventArgs> MediaFailed;
        public event TypedEventHandler<IPlaybackService, object> PlaybackStateChanged;
        public event TypedEventHandler<IPlaybackService, object> PositionChanged;
        public event EventHandler PlaylistChanged;

        public PlaybackService(ISettingsService settingsService)
        {
            _settingsService = settingsService;

            _isRepeatEnabled = _settingsService.Playback.RepeatMode == MediaPlaybackAutoRepeatMode.Track
                ? null
                : _settingsService.Playback.RepeatMode == MediaPlaybackAutoRepeatMode.List;
            _playbackRate = _settingsService.Playback.PlaybackRate;

            _mapping = new Dictionary<string, PlaybackItem>();
        }

        #region SystemMediaTransportControls

        private void Transport_AutoRepeatModeChangeRequested(SystemMediaTransportControls sender, AutoRepeatModeChangeRequestedEventArgs args)
        {
            IsRepeatEnabled = args.RequestedAutoRepeatMode == MediaPlaybackAutoRepeatMode.List
                ? true
                : args.RequestedAutoRepeatMode == MediaPlaybackAutoRepeatMode.Track
                ? null
                : false;
        }

        private void Transport_ButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
        {
            switch (args.Button)
            {
                case SystemMediaTransportControlsButton.Play:
                    Play();
                    break;
                case SystemMediaTransportControlsButton.Pause:
                    Pause();
                    break;
                case SystemMediaTransportControlsButton.Rewind:
                    Execute(player => player.StepBackwardOneFrame());
                    break;
                case SystemMediaTransportControlsButton.FastForward:
                    Execute(player => player.StepForwardOneFrame());
                    break;
                case SystemMediaTransportControlsButton.Previous:
                    if (Position.TotalSeconds > 5)
                    {
                        Seek(TimeSpan.Zero);
                    }
                    else
                    {
                        MovePrevious();
                    }
                    break;
                case SystemMediaTransportControlsButton.Next:
                    MoveNext();
                    break;
            }
        }

        #endregion

        private void OnSourceChanged(MediaPlayer sender, object args)
        {
            if (sender.Source is MediaSource source && source.CustomProperties.TryGet("token", out string token) && _mapping.TryGetValue(token, out PlaybackItem item))
            {
                var message = item.Message;
                var webPage = message.Content is MessageText text ? text.WebPage : null;

                if ((message.Content is MessageVideoNote videoNote && !videoNote.IsViewed && !message.IsOutgoing) || (message.Content is MessageVoiceNote voiceNote && !voiceNote.IsListened && !message.IsOutgoing))
                {
                    message.ProtoService.Send(new OpenMessageContent(message.ChatId, message.Id));
                }

                CurrentPlayback = item;
            }
        }

        private void OnMediaEnded(MediaPlayer sender, object args)
        {
            if (sender.Source is MediaSource source && source.CustomProperties.TryGet("token", out string token) && _mapping.TryGetValue(token, out PlaybackItem item))
            {
                if (item.Message.Content is MessageAudio && _isRepeatEnabled == null)
                {
                    Play();
                }
                else
                {
                    var index = _items.IndexOf(item);
                    if (index == -1 || index == (_isReversed ? 0 : _items.Count - 1))
                    {
                        if (item.Message.Content is MessageAudio && _isRepeatEnabled == true)
                        {
                            sender.Source = _items[_isReversed ? _items.Count - 1 : 0].Source;
                            Play();
                        }
                        else
                        {
                            Clear();
                        }
                    }
                    else
                    {
                        sender.Source = _items[_isReversed ? index - 1 : index + 1].Source;
                        Play();
                    }
                }
            }
        }

        private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            Clear();
            MediaFailed?.Invoke(this, args);
        }

        private void OnPlaybackStateChanged(MediaPlaybackSession sender, object args)
        {
            if (sender.PlaybackState == MediaPlaybackState.Playing && sender.PlaybackRate != _playbackRate)
            {
                sender.PlaybackRate = _playbackRate;
            }

            Execute(player =>
            {
                switch (sender.PlaybackState)
                {
                    case MediaPlaybackState.Playing:
                        player.SystemMediaTransportControls.PlaybackStatus = MediaPlaybackStatus.Playing;
                        break;
                    case MediaPlaybackState.Paused:
                        player.SystemMediaTransportControls.PlaybackStatus = MediaPlaybackStatus.Paused;
                        break;
                    case MediaPlaybackState.None:
                        player.SystemMediaTransportControls.PlaybackStatus = MediaPlaybackStatus.Stopped;
                        PlaybackState = MediaPlaybackState.None;
                        break;
                }
            });
        }

        private void OnPositionChanged(MediaPlaybackSession sender, object args)
        {
            PositionChanged?.Invoke(this, args);
        }

        private async void UpdateTransport()
        {
            var transport = _mediaPlayer?.SystemMediaTransportControls;
            if (transport == null)
            {
                return;
            }

            var items = _items;
            var item = CurrentPlayback;

            if (items == null || item?.File == null)
            {
                transport.IsEnabled = false;
                transport.DisplayUpdater.ClearAll();
                return;
            }

            transport.IsEnabled = true;
            transport.IsPlayEnabled = true;
            transport.IsPauseEnabled = true;
            transport.IsPreviousEnabled = true;
            transport.IsNextEnabled = items.Count > 1;

            void SetProperties(string title, string artist)
            {
                transport.DisplayUpdater.ClearAll();
                transport.DisplayUpdater.Type = MediaPlaybackType.Music;

                try
                {
                    transport.DisplayUpdater.MusicProperties.Title = title ?? string.Empty;
                    transport.DisplayUpdater.MusicProperties.Artist = artist ?? string.Empty;
                }
                catch { }
            }

            if (item.File.Local.IsDownloadingCompleted)
            {
                try
                {
                    var file = await item.Message.ProtoService.GetFileAsync(item.File);
                    await transport.DisplayUpdater.CopyFromFileAsync(MediaPlaybackType.Music, file);
                }
                catch
                {
                    SetProperties(item.Title, item.Artist);
                }
            }
            else
            {
                SetProperties(item.Title, item.Artist);
            }

            transport.DisplayUpdater.Update();
        }

        public IReadOnlyList<PlaybackItem> Items => _items ?? (IReadOnlyList<PlaybackItem>)new PlaybackItem[0];

        private PlaybackItem _currentPlayback;
        public PlaybackItem CurrentPlayback
        {
            get => _currentPlayback;
            private set
            {
                _currentItem = value?.Message;
                _currentPlayback = value;
                RaisePropertyChanged(nameof(CurrentItem));
                UpdateTransport();
            }
        }
        private MessageWithOwner _currentItem;
        public MessageWithOwner CurrentItem => _currentItem;

        public TimeSpan Position => Execute(player => player.PlaybackSession?.Position ?? TimeSpan.Zero, TimeSpan.Zero);

        public TimeSpan Duration => Execute(player => player.PlaybackSession?.NaturalDuration ?? TimeSpan.Zero, TimeSpan.Zero);

        private MediaPlaybackState _playbackState;
        public MediaPlaybackState PlaybackState
        {
            get { return _playbackState; }
            private set
            {
                if (_playbackState != value)
                {
                    _playbackState = value;
                    PlaybackStateChanged?.Invoke(this, null);
                }
            }
            //get
            //{
            //    try
            //    {
            //        return _mediaPlayer?.PlaybackSession?.PlaybackState ?? MediaPlaybackState.None;
            //    }
            //    catch
            //    {
            //        return MediaPlaybackState.None;
            //    }
            //}
        }

        private bool? _isRepeatEnabled = false;
        public bool? IsRepeatEnabled
        {
            get => _isRepeatEnabled;
            set
            {
                _isRepeatEnabled = value;
                Execute(player => player.SystemMediaTransportControls.AutoRepeatMode = _settingsService.Playback.RepeatMode = value == true
                    ? MediaPlaybackAutoRepeatMode.List
                    : value == null
                    ? MediaPlaybackAutoRepeatMode.Track
                    : MediaPlaybackAutoRepeatMode.None);
            }
        }

        private bool _isReversed = false;
        public bool IsReversed
        {
            get => _isReversed;
            set => _isReversed = value;
        }

        private bool _isShuffleEnabled;
        public bool IsShuffleEnabled
        {
            get => _isShuffleEnabled;
            set
            {
                _isShuffleEnabled = value;
                Execute(player => player.SystemMediaTransportControls.ShuffleEnabled = value);
            }
        }

        private double _playbackRate = 1.0;
        public double PlaybackRate
        {
            get => _playbackRate;
            set
            {
                _playbackRate = value;
                _settingsService.Playback.PlaybackRate = value;

                Execute(player =>
                {
                    player.PlaybackSession.PlaybackRate = value;
                    player.SystemMediaTransportControls.PlaybackRate = value;
                });
            }
        }

        public double Volume
        {
            get => _settingsService.VolumeLevel;
            set
            {
                _settingsService.VolumeLevel = value;
                Execute(player => player.Volume = value);
            }
        }

        public void Pause()
        {
            Execute(player =>
            {
                if (player.PlaybackSession.CanPause)
                {
                    player.Pause();
                    PlaybackState = MediaPlaybackState.Paused;
                }
            });
        }

        public void Play()
        {
            if (CurrentPlayback is PlaybackItem item)
            {
                PlaybackRate = item.CanChangePlaybackRate ? _settingsService.Playback.PlaybackRate : 1;
            }

            Execute(player =>
            {
                player.Play();
                PlaybackState = MediaPlaybackState.Playing;
            });
        }

        private void Execute(Action<MediaPlayer> action)
        {
            if (_mediaPlayer != null)
            {
                try
                {
                    action(_mediaPlayer);
                }
                catch
                {

                }
            }
        }

        private T Execute<T>(Func<MediaPlayer, T> action, T defaultValue)
        {
            if (_mediaPlayer != null)
            {
                try
                {
                    return action(_mediaPlayer);
                }
                catch
                {

                }
            }

            return defaultValue;
        }

        public void Seek(TimeSpan span)
        {
            Execute(player => player.PlaybackSession.Position = span);
            //var index = _playlist.CurrentItemIndex;
            //var playing = _mediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing;

            //_mediaPlayer.Pause();
            //_playlist.MoveTo(index);
            //_mediaPlayer.PlaybackSession.Position = span;

            //if (playing)
            //{
            //    _mediaPlayer.Play();
            //}
        }

        public void MoveNext()
        {
            var items = _items;
            if (items == null)
            {
                return;
            }

            var index = items.IndexOf(CurrentPlayback);
            if (index == (_isReversed ? 0 : items.Count - 1))
            {
                SetSource(items, _isReversed ? items.Count - 1 : 0);
            }
            else
            {
                SetSource(items, _isReversed ? index - 1 : index + 1);
            }

            Play();
        }

        public void MovePrevious()
        {
            var items = _items;
            if (items == null)
            {
                return;
            }

            var index = items.IndexOf(CurrentPlayback);
            if (index == (_isReversed ? items.Count - 1 : 0))
            {
                SetSource(items, _isReversed ? 0 : items.Count - 1);
            }
            else
            {
                SetSource(items, _isReversed ? index + 1 : index - 1);
            }

            Play();
        }

        private void SetSource(List<PlaybackItem> items, int index)
        {
            if (index >= 0 && index <= items.Count - 1)
            {
                Execute(player => player.Source = items[index].Source);
            }
        }

        public void Clear()
        {
            PlaybackState = MediaPlaybackState.None;

            //Execute.BeginOnUIThread(() => CurrentItem = null);
            CurrentPlayback = null;
            Dispose(true);
        }

        public void Play(MessageWithOwner message, long threadId)
        {
            if (message == null)
            {
                return;
            }

            var previous = _items;
            if (previous != null && _threadId == threadId)
            {
                var already = previous.FirstOrDefault(x => x.Message.Id == message.Id && x.Message.ChatId == message.ChatId);
                if (already != null)
                {
                    Execute(player => player.Source = already.Source);
                    Play();

                    return;
                }
            }

            Dispose(false);
            Create();

            var item = GetPlaybackItem(message);
            var items = _items = new List<PlaybackItem>();

            _items.Add(item);
            _threadId = threadId;

            _mediaPlayer.Source = item.Source;
            Play();

            if (message.Content is MessageText)
            {
                return;
            }

            var offset = -49;
            var filter = message.Content is MessageAudio ? new SearchMessagesFilterAudio() : (SearchMessagesFilter)new SearchMessagesFilterVoiceAndVideoNote();

            message.ProtoService.Send(new SearchChatMessages(message.ChatId, string.Empty, null, message.Id, offset, 100, filter, _threadId), result =>
            {
                if (result is Messages messages)
                {
                    foreach (var add in message.Content is MessageAudio ? messages.MessagesValue.OrderBy(x => x.Id) : messages.MessagesValue.OrderByDescending(x => x.Id))
                    {
                        if (add.Id > message.Id && add.Content is MessageAudio)
                        {
                            items.Insert(0, GetPlaybackItem(new MessageWithOwner(message.ProtoService, add)));
                        }
                        else if (add.Id < message.Id && (add.Content is MessageVoiceNote || add.Content is MessageVideoNote))
                        {
                            items.Insert(0, GetPlaybackItem(new MessageWithOwner(message.ProtoService, add)));
                        }
                    }

                    foreach (var add in message.Content is MessageAudio ? messages.MessagesValue.OrderByDescending(x => x.Id) : messages.MessagesValue.OrderBy(x => x.Id))
                    {
                        if (add.Id < message.Id && add.Content is MessageAudio)
                        {
                            items.Add(GetPlaybackItem(new MessageWithOwner(message.ProtoService, add)));
                        }
                        else if (add.Id > message.Id && (add.Content is MessageVoiceNote || add.Content is MessageVideoNote))
                        {
                            items.Add(GetPlaybackItem(new MessageWithOwner(message.ProtoService, add)));
                        }
                    }

                    UpdateTransport();
                    PlaylistChanged?.Invoke(this, EventArgs.Empty);
                }
            });
        }

        private PlaybackItem GetPlaybackItem(MessageWithOwner message)
        {
            var token = $"{message.ChatId}_{message.Id}";
            var file = message.GetFile();

            var source = CreateMediaSource(message, file, out bool speed);
            var item = new PlaybackItem(source)
            {
                File = file,
                Message = message,
                Token = token,
                CanChangePlaybackRate = speed
            };

            if (message.Content is MessageAudio audio)
            {
                var performer = string.IsNullOrEmpty(audio.Audio.Performer) ? null : audio.Audio.Performer;
                var title = string.IsNullOrEmpty(audio.Audio.Title) ? null : audio.Audio.Title;

                if (performer == null && title == null)
                {
                    item.Title = audio.Audio.FileName;
                    item.Artist = string.Empty;
                }
                else
                {
                    item.Title = string.IsNullOrEmpty(audio.Audio.Title) ? Strings.Resources.AudioUnknownTitle : audio.Audio.Title;
                    item.Artist = string.IsNullOrEmpty(audio.Audio.Performer) ? Strings.Resources.AudioUnknownArtist : audio.Audio.Performer;
                }
            }

            _mapping[token] = item;

            return item;
        }

        private MediaSource CreateMediaSource(MessageWithOwner message, File file, out bool speed)
        {
            GetProperties(message, out string mime, out int duration, out speed);

            var stream = new RemoteFileStream(message.ProtoService, file, duration);
            var source = MediaSource.CreateFromStream(stream, mime);

            source.CustomProperties["file"] = file.Id;
            source.CustomProperties["message"] = message.Id;
            source.CustomProperties["chat"] = message.ChatId;
            source.CustomProperties["token"] = $"{message.ChatId}_{message.Id}";

            return source;
        }

        private void GetProperties(MessageWithOwner message, out string mimeType, out int duration, out bool speed)
        {
            mimeType = null;
            duration = 0;
            speed = false;

            if (message.Content is MessageAudio audio)
            {
                mimeType = audio.Audio.MimeType;
                duration = audio.Audio.Duration;
                speed = duration >= 10 * 60;
            }
            else if (message.Content is MessageVoiceNote voiceNote)
            {
                mimeType = voiceNote.VoiceNote.MimeType;
                duration = voiceNote.VoiceNote.Duration;
                speed = true;
            }
            else if (message.Content is MessageVideoNote videoNote)
            {
                mimeType = "video/mp4";
                duration = videoNote.VideoNote.Duration;
                speed = true;
            }
            else if (message.Content is MessageText text && text.WebPage != null)
            {
                if (text.WebPage.Audio != null)
                {
                    mimeType = text.WebPage.Audio.MimeType;
                    duration = text.WebPage.Audio.Duration;
                    speed = duration >= 10 * 60;
                }
                else if (text.WebPage.VoiceNote != null)
                {
                    mimeType = text.WebPage.VoiceNote.MimeType;
                    duration = text.WebPage.VoiceNote.Duration;
                    speed = true;
                }
                else if (text.WebPage.VideoNote != null)
                {
                    mimeType = "video/mp4";
                    duration = text.WebPage.VideoNote.Duration;
                    speed = true;
                }
            }
        }

        private void Dispose(bool full)
        {
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Source = null;
                _mediaPlayer.CommandManager.IsEnabled = false;

                if (full)
                {
                    _mediaPlayer.SystemMediaTransportControls.ButtonPressed -= Transport_ButtonPressed;
                    _mediaPlayer.PlaybackSession.PlaybackStateChanged -= OnPlaybackStateChanged;
                    _mediaPlayer.PlaybackSession.PositionChanged -= OnPositionChanged;
                    _mediaPlayer.MediaFailed -= OnMediaFailed;
                    _mediaPlayer.MediaEnded -= OnMediaEnded;
                    _mediaPlayer.SourceChanged -= OnSourceChanged;
                    _mediaPlayer.Dispose();

                    _mediaPlayer = null;
                }
            }

            //if (_playlist != null)
            //{
            //    _playlist.CurrentItemChanged -= OnCurrentItemChanged;
            //    _playlist.Items.Clear();
            //    _playlist = null;
            //}

            if (_queue != null)
            {
                _queue.Clear();
                _queue = null;
            }

            if (_items != null)
            {
                // TODO: anything else?
                _items = null;
            }

            if (_mapping != null)
            {
                _mapping.Clear();
            }
        }

        private void Create()
        {
            if (_mediaPlayer != null)
            {
                return;
            }

            _mediaPlayer = new MediaPlayer();
            _mediaPlayer.SystemMediaTransportControls.AutoRepeatMode = _settingsService.Playback.RepeatMode;
            _mediaPlayer.SystemMediaTransportControls.ButtonPressed += Transport_ButtonPressed;
            _mediaPlayer.PlaybackSession.PlaybackStateChanged += OnPlaybackStateChanged;
            _mediaPlayer.PlaybackSession.PositionChanged += OnPositionChanged;
            _mediaPlayer.MediaFailed += OnMediaFailed;
            _mediaPlayer.MediaEnded += OnMediaEnded;
            _mediaPlayer.SourceChanged += OnSourceChanged;
            _mediaPlayer.CommandManager.IsEnabled = false;
            _mediaPlayer.Volume = _settingsService.VolumeLevel;
        }
    }

    public class PlaybackItem
    {
        public MediaSource Source { get; set; }
        public string Token { get; set; }

        public MessageWithOwner Message { get; set; }

        public File File { get; set; }

        public string Title { get; set; }
        public string Artist { get; set; }

        public bool CanChangePlaybackRate { get; set; }

        public PlaybackItem(MediaSource source)
        {
            Source = source;
        }
    }
}
