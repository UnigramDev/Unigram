using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Navigation;
using Unigram.Services.Updates;
using Windows.Foundation;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;

namespace Unigram.Services
{
    public interface IPlaybackService : INotifyPropertyChanged
    {
        IReadOnlyList<PlaybackItem> Items { get; }

        Message CurrentItem { get; }

        double PlaybackRate { get; set; }

        void Pause();
        void Play();

        void MoveNext();
        void MovePrevious();

        void SetPosition(TimeSpan span);

        void Clear();

        void Enqueue(Message message);

        TimeSpan Position { get; }
        TimeSpan Duration { get; }

        MediaPlaybackState PlaybackState { get; }



        bool? IsRepeatEnabled { get; set; }
        bool IsShuffleEnabled { get; set; }
        bool IsReversed { get; set; }


        bool IsSupportedPlaybackRateRange(double min, double max);



        event TypedEventHandler<MediaPlaybackSession, MediaPlayerFailedEventArgs> MediaFailed;
        event TypedEventHandler<MediaPlaybackSession, object> PlaybackStateChanged;
        event TypedEventHandler<MediaPlaybackSession, object> PositionChanged;
        event EventHandler PlaylistChanged;
    }

    public class PlaybackService : BindableBase, IPlaybackService, IHandle<UpdateFile>
    {
        private readonly IProtoService _protoService;
        private readonly ICacheService _cacheService;
        private readonly ISettingsService _settingsService;
        private readonly IEventAggregator _aggregator;

        private MediaPlayer _mediaPlayer;

        private SystemMediaTransportControls _transport;

        private Dictionary<string, PlaybackItem> _mapping;

        private readonly FileContext<RemoteFileStream> _streams = new FileContext<RemoteFileStream>();

        private List<PlaybackItem> _items;
        private Queue<Message> _queue;

        public event TypedEventHandler<MediaPlaybackSession, MediaPlayerFailedEventArgs> MediaFailed;
        public event TypedEventHandler<MediaPlaybackSession, object> PlaybackStateChanged;
        public event TypedEventHandler<MediaPlaybackSession, object> PositionChanged;
        public event EventHandler PlaylistChanged;

        public PlaybackService(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
        {
            _protoService = protoService;
            _cacheService = cacheService;
            _settingsService = settingsService;
            _aggregator = aggregator;

            _mediaPlayer = new MediaPlayer();
            _mediaPlayer.PlaybackSession.PlaybackStateChanged += OnPlaybackStateChanged;
            _mediaPlayer.PlaybackSession.PositionChanged += OnPositionChanged;
            _mediaPlayer.MediaFailed += OnMediaFailed;
            _mediaPlayer.MediaEnded += OnMediaEnded;
            _mediaPlayer.SourceChanged += OnSourceChanged;
            _mediaPlayer.CommandManager.IsEnabled = false;

            _transport = _mediaPlayer.SystemMediaTransportControls;
            _transport.ButtonPressed += Transport_ButtonPressed;

            _transport.AutoRepeatMode = _settingsService.Playback.RepeatMode;
            _isRepeatEnabled = _settingsService.Playback.RepeatMode == MediaPlaybackAutoRepeatMode.Track
                ? null
                : _settingsService.Playback.RepeatMode == MediaPlaybackAutoRepeatMode.List
                ? true
                : (bool?)false;

            _mapping = new Dictionary<string, PlaybackItem>();

            aggregator.Subscribe(this);
        }

        #region SystemMediaTransportControls

        private void Transport_AutoRepeatModeChangeRequested(SystemMediaTransportControls sender, AutoRepeatModeChangeRequestedEventArgs args)
        {
            IsRepeatEnabled = args.RequestedAutoRepeatMode == MediaPlaybackAutoRepeatMode.List
                ? true
                : args.RequestedAutoRepeatMode == MediaPlaybackAutoRepeatMode.Track
                ? null
                : (bool?)false;
        }

        private void Transport_ButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
        {
            switch (args.Button)
            {
                case SystemMediaTransportControlsButton.Play:
                    _mediaPlayer.Play();
                    break;
                case SystemMediaTransportControlsButton.Pause:
                    _mediaPlayer.Pause();
                    break;
                case SystemMediaTransportControlsButton.Rewind:
                    _mediaPlayer.StepBackwardOneFrame();
                    break;
                case SystemMediaTransportControlsButton.FastForward:
                    _mediaPlayer.StepForwardOneFrame();
                    break;
                case SystemMediaTransportControlsButton.Previous:
                    if (Position.TotalSeconds > 5)
                    {
                        SetPosition(TimeSpan.Zero);
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
                CurrentPlayback = item;

                var message = item.Message;
                if ((message.Content is MessageVideoNote videoNote && !videoNote.IsViewed && !message.IsOutgoing) || (message.Content is MessageVoiceNote voiceNote && !voiceNote.IsListened && !message.IsOutgoing))
                {
                    _protoService.Send(new OpenMessageContent(message.ChatId, message.Id));
                }
            }
        }

        private void OnMediaEnded(MediaPlayer sender, object args)
        {
            if (sender.Source is MediaSource source && source.CustomProperties.TryGet("token", out string token) && _mapping.TryGetValue(token, out PlaybackItem item))
            {
                if (item.Message.Content is MessageAudio && _isRepeatEnabled == null)
                {
                    _mediaPlayer.Play();
                }
                else
                {
                    var index = _items.IndexOf(item);
                    if (index == -1 || index == (_isReversed ? 0 : _items.Count - 1))
                    {
                        if (item.Message.Content is MessageAudio && _isRepeatEnabled == true)
                        {
                            _mediaPlayer.Source = _items[_isReversed ? _items.Count - 1 : 0].Source;
                            _mediaPlayer.Play();
                        }
                        else
                        {
                            Clear();
                        }
                    }
                    else
                    {
                        _mediaPlayer.Source = _items[_isReversed ? index - 1 : index + 1].Source;
                        _mediaPlayer.Play();
                    }
                }
            }
        }

        private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            Clear();
            MediaFailed?.Invoke(sender.PlaybackSession, args);
        }

        private void OnPlaybackStateChanged(MediaPlaybackSession sender, object args)
        {
            if (sender.PlaybackState == MediaPlaybackState.Playing && sender.PlaybackRate != _playbackRate)
            {
                sender.PlaybackRate = _playbackRate;
            }

            switch (sender.PlaybackState)
            {
                case MediaPlaybackState.Playing:
                    _transport.PlaybackStatus = MediaPlaybackStatus.Playing;
                    break;
                case MediaPlaybackState.Paused:
                    _transport.PlaybackStatus = MediaPlaybackStatus.Paused;
                    break;
                case MediaPlaybackState.None:
                    _transport.PlaybackStatus = MediaPlaybackStatus.Stopped;
                    break;
            }

            //else if (sender.PlaybackState == MediaPlaybackState.Paused && sender.Position == sender.NaturalDuration && _playlist.CurrentItem == _playlist.Items.LastOrDefault())
            //{
            //    Clear();
            //}
            //else
            //{
            //    Debugger.Break();
            //}

            PlaybackStateChanged?.Invoke(sender, args);
        }

        private void OnPositionChanged(MediaPlaybackSession sender, object args)
        {
            PositionChanged?.Invoke(sender, args);
        }

        private async void UpdateTransport()
        {
            var items = _items;
            var item = CurrentPlayback;

            if (items == null || item == null)
            {
                _transport.IsEnabled = false;
                _transport.DisplayUpdater.ClearAll();
                return;
            }

            _transport.IsEnabled = true;
            _transport.IsPlayEnabled = true;
            _transport.IsPauseEnabled = true;
            _transport.IsPreviousEnabled = true;
            _transport.IsNextEnabled = items.Count > 1;

            void SetProperties(string title, string artist)
            {
                _transport.DisplayUpdater.ClearAll();
                _transport.DisplayUpdater.Type = MediaPlaybackType.Music;

                try
                {
                    _transport.DisplayUpdater.MusicProperties.Title = title ?? string.Empty;
                    _transport.DisplayUpdater.MusicProperties.Artist = artist ?? string.Empty;
                }
                catch { }
            }

            if (item.File.Local.IsDownloadingCompleted)
            {
                try
                {
                    var file = await StorageFile.GetFileFromPathAsync(item.File.Local.Path);
                    await _transport.DisplayUpdater.CopyFromFileAsync(MediaPlaybackType.Music, file);
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

            _transport.DisplayUpdater.Update();
        }

        public IReadOnlyList<PlaybackItem> Items
        {
            get
            {
                return _items ?? (IReadOnlyList<PlaybackItem>)new PlaybackItem[0];
            }
        }

        private PlaybackItem _currentPlayback;
        public PlaybackItem CurrentPlayback
        {
            get
            {
                return _currentPlayback;
            }
            private set
            {
                _currentItem = value?.Message;
                _currentPlayback = value;
                _aggregator.Publish(new UpdatePlaybackItem(value?.Message));
                RaisePropertyChanged(() => CurrentItem);
                UpdateTransport();
            }
        }
        private Message _currentItem;
        public Message CurrentItem
        {
            get
            {
                return _currentItem;
            }
        }

        public TimeSpan Position
        {
            get
            {
                try
                {
                    return _mediaPlayer?.PlaybackSession?.Position ?? TimeSpan.Zero;
                }
                catch
                {
                    return TimeSpan.Zero;
                }
            }
        }

        public TimeSpan Duration
        {
            get
            {
                try
                {
                    return _mediaPlayer?.PlaybackSession?.NaturalDuration ?? TimeSpan.Zero;
                }
                catch
                {
                    return TimeSpan.Zero;
                }
            }
        }

        public MediaPlaybackState PlaybackState
        {
            get
            {
                try
                {
                    return _mediaPlayer?.PlaybackSession?.PlaybackState ?? MediaPlaybackState.None;
                }
                catch
                {
                    return MediaPlaybackState.None;
                }
            }
        }

        private bool? _isRepeatEnabled = false;
        public bool? IsRepeatEnabled
        {
            get { return _isRepeatEnabled; }
            set
            {
                _isRepeatEnabled = value;
                _transport.AutoRepeatMode =
                    _settingsService.Playback.RepeatMode = value == true
                        ? MediaPlaybackAutoRepeatMode.List
                        : value == null
                        ? MediaPlaybackAutoRepeatMode.Track
                        : MediaPlaybackAutoRepeatMode.None;
            }
        }

        private bool _isReversed = false;
        public bool IsReversed
        {
            get { return _isReversed; }
            set { _isReversed = value; }
        }

        private bool _isShuffleEnabled;
        public bool IsShuffleEnabled
        {
            get { return _isShuffleEnabled; }
            set
            {
                _isShuffleEnabled = value;
                _transport.ShuffleEnabled = value;
            }
        }

        private double _playbackRate = 1.0;
        public double PlaybackRate
        {
            get
            {
                return _playbackRate;
            }
            set
            {
                _playbackRate = value;
                _transport.PlaybackRate = value;

                try
                {
                    _mediaPlayer.PlaybackSession.PlaybackRate = value;
                }
                catch { }
            }
        }

        public void Pause()
        {
            if (_mediaPlayer.PlaybackSession.CanPause)
            {
                _mediaPlayer.Pause();
            }
        }

        public void Play()
        {
            _mediaPlayer.Play();
        }

        public void SetPosition(TimeSpan span)
        {
            _mediaPlayer.PlaybackSession.Position = span;
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

            _mediaPlayer.Play();
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

            _mediaPlayer.Play();
        }

        private void SetSource(List<PlaybackItem> items, int index)
        {
            if (index >= 0 && index <= items.Count - 1)
            {
                _mediaPlayer.Source = items[index].Source;
            }
        }

        public void Clear()
        {
            //Execute.BeginOnUIThread(() => CurrentItem = null);
            CurrentPlayback = null;
            Dispose();
        }

        public void Enqueue(Message message)
        {
            if (message == null)
            {
                return;
            }

            var previous = _items;
            if (previous != null)
            {
                var already = previous.FirstOrDefault(x => x.Message.Id == message.Id && x.Message.ChatId == message.ChatId);
                if (already != null)
                {
                    _mediaPlayer.Source = already.Source;
                    _mediaPlayer.Play();

                    return;
                }
            }

            Dispose();

            var item = GetPlaybackItem(message);

            var items = _items = new List<PlaybackItem>();
            _items.Add(item);

            _mediaPlayer.Source = item.Source;
            _mediaPlayer.Play();

            if (message.Content is MessageText)
            {
                return;
            }

            var offset = -49;
            var filter = message.Content is MessageAudio ? new SearchMessagesFilterAudio() : (SearchMessagesFilter)new SearchMessagesFilterVoiceAndVideoNote();

            _protoService.Send(new SearchChatMessages(message.ChatId, string.Empty, 0, message.Id, offset, 100, filter, 0), result =>
            {
                if (result is Messages messages)
                {
                    foreach (var add in message.Content is MessageAudio ? messages.MessagesValue.OrderBy(x => x.Id) : messages.MessagesValue.OrderByDescending(x => x.Id))
                    {
                        if (add.Id > message.Id && message.Content is MessageAudio)
                        {
                            items.Insert(0, GetPlaybackItem(add));
                        }
                        else if (add.Id < message.Id && (message.Content is MessageVoiceNote || message.Content is MessageVideoNote))
                        {
                            items.Insert(0, GetPlaybackItem(add));
                        }
                    }

                    foreach (var add in message.Content is MessageAudio ? messages.MessagesValue.OrderByDescending(x => x.Id) : messages.MessagesValue.OrderBy(x => x.Id))
                    {
                        if (add.Id < message.Id && message.Content is MessageAudio)
                        {
                            items.Add(GetPlaybackItem(add));
                        }
                        else if (add.Id > message.Id && (message.Content is MessageVoiceNote || message.Content is MessageVideoNote))
                        {
                            items.Add(GetPlaybackItem(add));
                        }
                    }

                    UpdateTransport();
                    PlaylistChanged?.Invoke(this, EventArgs.Empty);
                }
            });
        }

        private PlaybackItem GetPlaybackItem(Message message)
        {
            var token = $"{message.ChatId}_{message.Id}";
            var file = GetFile(message);
            var mime = GetMimeType(message);
            var duration = GetDuration(message);

            var stream = new RemoteFileStream(_protoService, file, TimeSpan.FromSeconds(duration));
            var source = MediaSource.CreateFromStream(stream, mime);
            var item = new PlaybackItem(source);

            _streams[file.Id].Add(stream);

            source.CustomProperties["file"] = file.Id;
            source.CustomProperties["message"] = message.Id;
            source.CustomProperties["chat"] = message.ChatId;
            source.CustomProperties["token"] = token;

            item.File = file;
            item.Message = message;
            item.Token = token;

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

        private File GetFile(Message message)
        {
            if (message.Content is MessageAudio audio)
            {
                return audio.Audio.AudioValue;
            }
            else if (message.Content is MessageVoiceNote voiceNote)
            {
                return voiceNote.VoiceNote.Voice;
            }
            else if (message.Content is MessageVideoNote videoNote)
            {
                return videoNote.VideoNote.Video;
            }
            else if (message.Content is MessageText text && text.WebPage != null)
            {
                if (text.WebPage.Audio != null)
                {
                    return text.WebPage.Audio.AudioValue;
                }
                else if (text.WebPage.VoiceNote != null)
                {
                    return text.WebPage.VoiceNote.Voice;
                }
                else if (text.WebPage.VideoNote != null)
                {
                    return text.WebPage.VideoNote.Video;
                }
            }

            return null;
        }

        private string GetMimeType(Message message)
        {
            if (message.Content is MessageAudio audio)
            {
                return audio.Audio.MimeType;
            }
            else if (message.Content is MessageVoiceNote voiceNote)
            {
                return voiceNote.VoiceNote.MimeType;
            }
            else if (message.Content is MessageVideoNote videoNote)
            {
                return "video/mp4";
            }
            else if (message.Content is MessageText text && text.WebPage != null)
            {
                if (text.WebPage.Audio != null)
                {
                    return text.WebPage.Audio.MimeType;
                }
                else if (text.WebPage.VoiceNote != null)
                {
                    return text.WebPage.VoiceNote.MimeType;
                }
                else if (text.WebPage.VideoNote != null)
                {
                    return "video/mp4";
                }
            }

            return null;
        }

        private int GetDuration(Message message)
        {
            if (message.Content is MessageAudio audio)
            {
                return audio.Audio.Duration;
            }
            else if (message.Content is MessageVoiceNote voiceNote)
            {
                return voiceNote.VoiceNote.Duration;
            }
            else if (message.Content is MessageVideoNote videoNote)
            {
                return videoNote.VideoNote.Duration;
            }
            else if (message.Content is MessageText text && text.WebPage != null)
            {
                if (text.WebPage.Audio != null)
                {
                    return text.WebPage.Audio.Duration;
                }
                else if (text.WebPage.VoiceNote != null)
                {
                    return text.WebPage.VoiceNote.Duration;
                }
                else if (text.WebPage.VideoNote != null)
                {
                    return text.WebPage.VideoNote.Duration;
                }
            }

            return 0;
        }

        public void Handle(UpdateFile update)
        {
            if (_streams.TryGetValue(update.File.Id, out List<RemoteFileStream> streams))
            {
                foreach (var stream in streams)
                {
                    stream.UpdateFile(update.File);
                }
            }
        }

        private void Dispose()
        {
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Source = null;
                _mediaPlayer.CommandManager.IsEnabled = false;
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

        public bool IsSupportedPlaybackRateRange(double min, double max)
        {
            if (_mediaPlayer != null)
            {
                return _mediaPlayer.PlaybackSession.IsSupportedPlaybackRateRange(min, max);
            }

            return false;
        }
    }

    public class PlaybackItem
    {
        public MediaSource Source { get; set; }
        public string Token { get; set; }

        public Message Message { get; set; }

        public File File { get; set; }

        public string Title { get; set; }
        public string Artist { get; set; }

        public PlaybackItem(MediaSource source)
        {
            Source = source;
        }

        public bool UpdateFile(File file)
        {
            return Message.UpdateFile(file);
        }
    }
}
