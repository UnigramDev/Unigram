//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using LibVLCSharp.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Common;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using WM = Windows.Media;

namespace Telegram.Services
{
    public enum PlaybackState
    {
        None,
        Playing,
        Paused
    }

    public enum PlaybackRepeatMode
    {
        None,
        Track,
        List
    }

    public class PlaybackPositionChangedEventArgs
    {
        public TimeSpan Position { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public interface IPlaybackService
    {
        IReadOnlyList<PlaybackItem> Items { get; }

        MessageWithOwner CurrentItem { get; }
        PlaybackItem CurrentPlayback { get; }

        double PlaybackSpeed { get; set; }

        double Volume { get; set; }

        void Pause();
        void Play();

        void MoveNext();
        void MovePrevious();

        void Seek(TimeSpan span);

        void Clear();

        void Play(MessageWithOwner message, long threadId = 0, long savedMessagesTopicId = 0);

        TimeSpan Position { get; }
        TimeSpan Duration { get; }

        PlaybackState PlaybackState { get; }



        bool? IsRepeatEnabled { get; set; }
        bool IsShuffleEnabled { get; set; }
        bool IsReversed { get; set; }



        event TypedEventHandler<IPlaybackService, object> MediaFailed;

        event TypedEventHandler<IPlaybackService, object> StateChanged;
        event TypedEventHandler<IPlaybackService, object> SourceChanged;
        event TypedEventHandler<IPlaybackService, PlaybackPositionChangedEventArgs> PositionChanged;
        event TypedEventHandler<IPlaybackService, object> PlaylistChanged;
    }

    public class PlaybackService : IPlaybackService
    {
        private readonly ISettingsService _settingsService;

        private AsyncMediaPlayer _player;
        private readonly object _mediaPlayerLock = new();

        private readonly PlaybackPositionChangedEventArgs _positionChanged = new();

        private WM.SystemMediaTransportControls _transport;

        private long _threadId;
        private long _savedMessagesTopicId;

        private List<PlaybackItem> _items;

        public event TypedEventHandler<IPlaybackService, object> MediaFailed;
        public event TypedEventHandler<IPlaybackService, object> StateChanged;
        public event TypedEventHandler<IPlaybackService, object> SourceChanged;
        public event TypedEventHandler<IPlaybackService, PlaybackPositionChangedEventArgs> PositionChanged;
        public event TypedEventHandler<IPlaybackService, object> PlaylistChanged;

        public PlaybackService(ISettingsService settingsService)
        {
            _settingsService = settingsService;

            _isRepeatEnabled = _settingsService.Playback.RepeatMode == PlaybackRepeatMode.Track
                ? null
                : _settingsService.Playback.RepeatMode == PlaybackRepeatMode.List;
            _playbackSpeed = _settingsService.Playback.AudioSpeed;

            // TODO: System media transport controls are currently unsupported.
        }

        #region SystemMediaTransportControls

        private void Transport_AutoRepeatModeChangeRequested(WM.SystemMediaTransportControls sender, WM.AutoRepeatModeChangeRequestedEventArgs args)
        {
            IsRepeatEnabled = args.RequestedAutoRepeatMode == WM.MediaPlaybackAutoRepeatMode.List
                ? true
                : args.RequestedAutoRepeatMode == WM.MediaPlaybackAutoRepeatMode.Track
                ? null
                : false;
        }

        private void Transport_ButtonPressed(WM.SystemMediaTransportControls sender, WM.SystemMediaTransportControlsButtonPressedEventArgs args)
        {
            switch (args.Button)
            {
                case WM.SystemMediaTransportControlsButton.Play:
                    Play();
                    break;
                case WM.SystemMediaTransportControlsButton.Pause:
                    Pause();
                    break;
                //case WM.SystemMediaTransportControlsButton.Rewind:
                //    Execute(player => player.StepBackwardOneFrame());
                //    break;
                //case WM.SystemMediaTransportControlsButton.FastForward:
                //    Execute(player => player.StepForwardOneFrame());
                //    break;
                case WM.SystemMediaTransportControlsButton.Previous:
                    if (Position.TotalSeconds > 5)
                    {
                        Seek(TimeSpan.Zero);
                    }
                    else
                    {
                        MovePrevious();
                    }
                    break;
                case WM.SystemMediaTransportControlsButton.Next:
                    MoveNext();
                    break;
            }
        }

        #endregion

        private void OnBuffering(object sender, MediaPlayerBufferingEventArgs args)
        {
            if (args.Cache == 100)
            {
                var item = CurrentPlayback;
                if (item != null)
                {
                    var message = item.Message;
                    var linkPreview = message.Content is MessageText text ? text.LinkPreview : null;

                    if ((message.Content is MessageVideoNote videoNote && !videoNote.IsViewed && !message.IsOutgoing) || (message.Content is MessageVoiceNote voiceNote && !voiceNote.IsListened && !message.IsOutgoing))
                    {
                        message.ClientService.Send(new OpenMessageContent(message.ChatId, message.Id));
                    }
                }
            }
        }

        private void OnEndReached(object sender, EventArgs args)
        {
            var item = CurrentPlayback;
            if (item != null)
            {
                if (item.Message.Content is MessageAudio && _isRepeatEnabled == null)
                {
                    Play();
                }
                else
                {
                    MoveNext();
                }
            }
        }

        private void OnEncounteredError(object sender, EventArgs args)
        {
            Clear();
            MediaFailed?.Invoke(this, null);
        }

        private void OnPlaybackStateChanged(object sender, object args)
        {
            //if (sender.PlaybackState == MediaPlaybackState.Playing && sender.PlaybackRate != _playbackSpeed)
            //{
            //    sender.PlaybackRate = _playbackSpeed;
            //}

            switch (_player.State)
            {
                case VLCState.Playing:
                    //sender.MediaPlayer.SystemMediaTransportControls.PlaybackStatus = MediaPlaybackStatus.Playing;
                    break;
                case VLCState.Paused:
                    //sender.MediaPlayer.SystemMediaTransportControls.PlaybackStatus = MediaPlaybackStatus.Paused;
                    break;
                case VLCState.NothingSpecial:
                case VLCState.Stopped:
                    //sender.MediaPlayer.SystemMediaTransportControls.PlaybackStatus = MediaPlaybackStatus.Stopped;
                    PlaybackState = PlaybackState.None;
                    break;
            }
        }

        private void OnTimeChanged(AsyncMediaPlayer sender, MediaPlayerTimeChangedEventArgs args)
        {
            _positionChanged.Position = TimeSpan.FromMilliseconds(args.Time);
            PositionChanged?.Invoke(this, _positionChanged);
        }

        private void OnLengthChanged(AsyncMediaPlayer sender, MediaPlayerLengthChangedEventArgs args)
        {
            _positionChanged.Duration = TimeSpan.FromMilliseconds(args.Length);
            PositionChanged?.Invoke(this, _positionChanged);
        }

        private void UpdateTransport(PlaybackItem item)
        {
            var items = _items;
            var transport = _transport;

            if (items == null || item == null /*|| item?.Stream?.File == null*/)
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

            transport.DisplayUpdater.ClearAll();
            transport.DisplayUpdater.Type = WM.MediaPlaybackType.Music;

            try
            {
                transport.DisplayUpdater.MusicProperties.Title = item.Title ?? string.Empty;
                transport.DisplayUpdater.MusicProperties.Artist = item.Performer ?? string.Empty;
            }
            catch { }

            transport.DisplayUpdater.Update();
        }

        public IReadOnlyList<PlaybackItem> Items => _items ?? (IReadOnlyList<PlaybackItem>)Array.Empty<PlaybackItem>();

        private PlaybackItem _currentPlayback;
        public PlaybackItem CurrentPlayback
        {
            get => _currentPlayback;
            private set
            {
                _currentItem = value?.Message;
                _currentPlayback = value;
                _positionChanged.Position = TimeSpan.Zero;
                _positionChanged.Duration = TimeSpan.Zero;
                SourceChanged?.Invoke(this, value);
                UpdateTransport(value);
            }
        }
        private MessageWithOwner _currentItem;
        public MessageWithOwner CurrentItem => _currentItem;

        public TimeSpan Position => _positionChanged.Position;

        public TimeSpan Duration => _positionChanged.Duration;

        private PlaybackState _playbackState;
        public PlaybackState PlaybackState
        {
            get => _playbackState;
            private set
            {
                if (_playbackState != value)
                {
                    _playbackState = value;
                    StateChanged?.Invoke(this, null);

                    _transport.PlaybackStatus = value switch
                    {
                        PlaybackState.Playing => WM.MediaPlaybackStatus.Playing,
                        PlaybackState.Paused => WM.MediaPlaybackStatus.Paused,
                        PlaybackState.None or _ => WM.MediaPlaybackStatus.Stopped
                    };
                }
            }
        }

        private bool? _isRepeatEnabled = false;
        public bool? IsRepeatEnabled
        {
            get => _isRepeatEnabled;
            set
            {
                _isRepeatEnabled = value;
                //Execute(player => player.SystemMediaTransportControls.AutoRepeatMode = _settingsService.Playback.RepeatMode = value == true
                //    ? MediaPlaybackAutoRepeatMode.List
                //    : value == null
                //    ? MediaPlaybackAutoRepeatMode.Track
                //    : MediaPlaybackAutoRepeatMode.None);
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
                //Execute(player => player.SystemMediaTransportControls.ShuffleEnabled = value);
            }
        }

        private double _playbackSpeed = 1.0;
        public double PlaybackSpeed
        {
            get => _playbackSpeed;
            set
            {
                _playbackSpeed = value;
                _settingsService.Playback.AudioSpeed = value;

                Run(player =>
                {
                    player.Rate = (float)value;
                    //player.SystemMediaTransportControls.PlaybackRate = value;
                });
            }
        }

        public double Volume
        {
            get => _settingsService.VolumeLevel;
            set
            {
                _settingsService.VolumeLevel = value;
                Run(player => player.Volume = (int)Math.Round(value * 100));
            }
        }

        public void Pause()
        {
            Run(PauseImpl);
        }

        public void PauseImpl(AsyncMediaPlayer player)
        {
            if (player.CanPause)
            {
                player.Pause();
                PlaybackState = PlaybackState.Paused;
            }
        }

        public void Play()
        {
            Run(PlayImpl);
        }

        public void PlayImpl(AsyncMediaPlayer player)
        {
            if (CurrentPlayback is PlaybackItem item)
            {
                _playbackSpeed = item.CanChangePlaybackRate ? _settingsService.Playback.AudioSpeed : 1;
                player.Rate = (float)_playbackSpeed;
            }

            if (player.State == VLCState.Ended)
            {
                player.Stop();
            }

            player.Play();
            PlaybackState = PlaybackState.Playing;
        }

        private void Run(Action<AsyncMediaPlayer> action)
        {
            lock (_mediaPlayerLock)
            {
                if (_player != null)
                {
                    action(_player);
                }
            }
        }

        private void Run<T>(Action<AsyncMediaPlayer, T> action, T arg)
        {
            lock (_mediaPlayerLock)
            {
                if (_player != null)
                {
                    action(_player, arg);
                }
            }
        }

        public void Seek(TimeSpan span)
        {
            Run(SeekImpl, span);
        }

        private void SeekImpl(AsyncMediaPlayer player, TimeSpan span)
        {
            // Workaround for OGG files. It's unclear why this is needed,
            // but it's likely caused by our LibVLC build configuration,
            // as it doesn't happen with standalone VLC.
            if (span.TotalMilliseconds < player.Time)
            {
                var playing = player.IsPlaying;

                player.Stop();
                player.Play();

                if (playing is false)
                {
                    player.Pause(true);
                }
            }

            player.Time = (long)span.TotalMilliseconds;

            _positionChanged.Position = span;
            PositionChanged?.Invoke(this, _positionChanged);
        }

        public void MoveNext()
        {
            Run(MoveNextImpl);
        }

        public void MoveNextImpl(AsyncMediaPlayer player)
        {
            var items = _items;
            if (items == null)
            {
                return;
            }

            var index = items.IndexOf(CurrentPlayback);
            if (index == -1 || index == (_isReversed ? 0 : items.Count - 1))
            {
                if (CurrentPlayback?.Message.Content is MessageAudio && _isRepeatEnabled == true)
                {
                    SetSource(player, items, _isReversed ? items.Count - 1 : 0);
                }
                else
                {
                    ClearImpl(player);
                }
            }
            else
            {
                SetSource(player, items, _isReversed ? index - 1 : index + 1);
            }
        }

        public void MovePrevious()
        {
            Run(MovePreviousImpl);
        }

        public void MovePreviousImpl(AsyncMediaPlayer player)
        {
            var items = _items;
            if (items == null)
            {
                return;
            }

            var index = items.IndexOf(CurrentPlayback);
            if (index == -1 || index == (_isReversed ? items.Count - 1 : 0))
            {
                if (CurrentPlayback?.Message.Content is MessageAudio && _isRepeatEnabled == true)
                {
                    SetSource(player, items, _isReversed ? 0 : items.Count - 1);
                }
                else
                {
                    ClearImpl(player);
                }
            }
            else
            {
                SetSource(player, items, _isReversed ? index + 1 : index - 1);
            }
        }

        private void SetSource(AsyncMediaPlayer player, List<PlaybackItem> items, int index)
        {
            if (index >= 0 && index <= items.Count - 1)
            {
                SetSource(player, items[index]);
            }
        }

        private void SetSource(AsyncMediaPlayer player, PlaybackItem item)
        {
            try
            {
                player ??= Create();

                _playbackSpeed = item.CanChangePlaybackRate ? _settingsService.Playback.AudioSpeed : 1;
                CurrentPlayback = item;

                player.Rate = (float)_playbackSpeed;
                player.Play(new RemoteFileStream(item.ClientService, item.Document));
                PlaybackState = PlaybackState.Playing;
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }

        public void Clear()
        {
            Run(ClearImpl);
        }

        public void ClearImpl(AsyncMediaPlayer player)
        {
            PlaybackState = PlaybackState.None;

            //Execute.BeginOnUIThread(() => CurrentItem = null);
            CurrentPlayback = null;
            Dispose(true);
        }

        public async void Play(MessageWithOwner message, long threadId, long savedMessagesTopicId)
        {
            _transport ??= WM.SystemMediaTransportControls.GetForCurrentView();

            if (message == null)
            {
                return;
            }

            var previous = _items;
            if (previous != null && _threadId == threadId && _savedMessagesTopicId == savedMessagesTopicId)
            {
                var already = previous.FirstOrDefault(x => x.Message.Id == message.Id && x.Message.ChatId == message.ChatId);
                if (already != null)
                {
                    SetSource(null, already);
                    return;
                }
            }

            Dispose(false);

            var item = GetPlaybackItem(message);
            var items = _items = new List<PlaybackItem>();

            _items.Add(item);
            _threadId = threadId;
            _savedMessagesTopicId = savedMessagesTopicId;

            SetSource(null, item);

            if (message.Content is MessageText)
            {
                return;
            }

            var offset = -49;
            var filter = message.Content is MessageAudio ? new SearchMessagesFilterAudio() : (SearchMessagesFilter)new SearchMessagesFilterVoiceNote();

            // TODO: 172 savedMessagesTopic
            var response = await message.ClientService.SendAsync(new SearchChatMessages(message.ChatId, string.Empty, null, message.Id, offset, 100, filter, _threadId, _savedMessagesTopicId));
            if (response is FoundChatMessages messages)
            {
                foreach (var add in message.Content is MessageAudio ? messages.Messages.OrderBy(x => x.Id) : messages.Messages.OrderByDescending(x => x.Id))
                {
                    if (add.Id > message.Id && add.Content is MessageAudio)
                    {
                        items.Insert(0, GetPlaybackItem(new MessageWithOwner(message.ClientService, add)));
                    }
                    else if (add.Id < message.Id && (add.Content is MessageVoiceNote || add.Content is MessageVideoNote))
                    {
                        items.Insert(0, GetPlaybackItem(new MessageWithOwner(message.ClientService, add)));
                    }
                }

                foreach (var add in message.Content is MessageAudio ? messages.Messages.OrderByDescending(x => x.Id) : messages.Messages.OrderBy(x => x.Id))
                {
                    if (add.Id < message.Id && add.Content is MessageAudio)
                    {
                        items.Add(GetPlaybackItem(new MessageWithOwner(message.ClientService, add)));
                    }
                    else if (add.Id > message.Id && (add.Content is MessageVoiceNote || add.Content is MessageVideoNote))
                    {
                        items.Add(GetPlaybackItem(new MessageWithOwner(message.ClientService, add)));
                    }
                }

                UpdateTransport(CurrentPlayback);
                PlaylistChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private PlaybackItem GetPlaybackItem(MessageWithOwner message)
        {
            GetProperties(message, out File file, out bool speed);

            var item = new PlaybackItem(file)
            {
                Message = message,
                CanChangePlaybackRate = speed
            };

            if (message.Content is MessageAudio audio)
            {
                if (string.IsNullOrEmpty(audio.Audio.Performer) || string.IsNullOrEmpty(audio.Audio.Title))
                {
                    item.Title = audio.Audio.FileName;
                    item.Performer = string.Empty;
                }
                else
                {
                    item.Title = audio.Audio.Title;
                    item.Performer = audio.Audio.Performer;
                }
            }

            return item;
        }

        private void GetProperties(MessageWithOwner message, out File file, out bool speed)
        {
            file = null;
            speed = false;

            if (message.Content is MessageAudio audio)
            {
                file = audio.Audio.AudioValue;
                speed = audio.Audio.Duration >= 10 * 60;
            }
            else if (message.Content is MessageVoiceNote voiceNote)
            {
                file = voiceNote.VoiceNote.Voice;
                speed = true;
            }
            else if (message.Content is MessageVideoNote videoNote)
            {
                file = videoNote.VideoNote.Video;
                speed = true;
            }
            else if (message.Content is MessageText text && text.LinkPreview != null)
            {
                if (text.LinkPreview.Type is LinkPreviewTypeAudio previewAudio)
                {
                    file = previewAudio.Audio.AudioValue;
                    speed = previewAudio.Audio.Duration >= 10 * 60;
                }
                else if (text.LinkPreview.Type is LinkPreviewTypeVoiceNote previewVoiceNote)
                {
                    file = previewVoiceNote.VoiceNote.Voice;
                    speed = true;
                }
                else if (text.LinkPreview.Type is LinkPreviewTypeVideoNote previewVideoNote)
                {
                    file = previewVideoNote.VideoNote.Video;
                    speed = true;
                }
            }
        }

        private void Dispose(bool full)
        {
            if (_player != null)
            {
                //_mediaPlayer.CommandManager.IsEnabled = false;

                if (full)
                {
                    _transport.ButtonPressed -= Transport_ButtonPressed;

                    //_mediaPlayer.SystemMediaTransportControls.ButtonPressed -= Transport_ButtonPressed;
                    //_mediaPlayer.PlaybackSession.PlaybackStateChanged -= OnPlaybackStateChanged;
                    _player.TimeChanged -= OnTimeChanged;
                    _player.LengthChanged -= OnLengthChanged;
                    _player.EncounteredError -= OnEncounteredError;
                    _player.EndReached -= OnEndReached;
                    _player.Buffering -= OnBuffering;
                    _player.Close();

                    lock (_mediaPlayerLock)
                    {
                        _player = null;
                    }
                }
                else
                {
                    _player.Stop();
                }
            }

            _items = null;
        }

        private AsyncMediaPlayer Create()
        {
            if (_player == null)
            {
                _player = new AsyncMediaPlayer();
                //_mediaPlayer.SystemMediaTransportControls.AutoRepeatMode = _settingsService.Playback.RepeatMode;
                //_mediaPlayer.SystemMediaTransportControls.ButtonPressed += Transport_ButtonPressed;
                //_mediaPlayer.PlaybackSession.PlaybackStateChanged += OnPlaybackStateChanged;
                _player.TimeChanged += OnTimeChanged;
                _player.LengthChanged += OnLengthChanged;
                _player.EncounteredError += OnEncounteredError;
                _player.EndReached += OnEndReached;
                _player.Buffering += OnBuffering;
                //_mediaPlayer.CommandManager.IsEnabled = false;
                _player.Volume = (int)Math.Round(_settingsService.VolumeLevel * 100);

                _transport.ButtonPressed += Transport_ButtonPressed;
            }

            return _player;
        }
    }

    public class PlaybackItem
    {
        public IClientService ClientService => Message.ClientService;

        public MessageWithOwner Message { get; set; }

        public File Document { get; set; }

        public string Title { get; set; }
        public string Performer { get; set; }

        public bool CanChangePlaybackRate { get; set; }

        public PlaybackItem(File document)
        {
            Document = document;
        }
    }
}
