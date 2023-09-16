//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using LibVLCSharp.Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.Storage;
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

        void Play(MessageWithOwner message, long threadId = 0);

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

        private LibVLC _library;
        private MediaPlayer _mediaPlayer;
        private readonly object _mediaPlayerLock = new();

        private readonly PlaybackPositionChangedEventArgs _positionChanged = new();

        private WM.SystemMediaTransportControls _transport;

        private long _threadId;

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

        private void OnMediaChanged(object sender, MediaPlayerMediaChangedEventArgs args)
        {
            var item = CurrentPlayback;
            if (item != null)
            {
                var message = item.Message;
                var webPage = message.Content is MessageText text ? text.WebPage : null;

                if ((message.Content is MessageVideoNote videoNote && !videoNote.IsViewed && !message.IsOutgoing) || (message.Content is MessageVoiceNote voiceNote && !voiceNote.IsListened && !message.IsOutgoing))
                {
                    message.ClientService.Send(new OpenMessageContent(message.ChatId, message.Id));
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

            switch (_mediaPlayer.State)
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

        private void OnTimeChanged(object sender, MediaPlayerTimeChangedEventArgs args)
        {
            _positionChanged.Position = TimeSpan.FromMilliseconds(args.Time);
            PositionChanged?.Invoke(this, _positionChanged);
        }

        private void OnLengthChanged(object sender, MediaPlayerLengthChangedEventArgs args)
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
                    player.SetRate((float)value);
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

        public void PauseImpl(MediaPlayer player)
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

        public void PlayImpl(MediaPlayer player)
        {
            if (CurrentPlayback is PlaybackItem item)
            {
                _playbackSpeed = item.CanChangePlaybackRate ? _settingsService.Playback.AudioSpeed : 1;
                player.SetRate((float)_playbackSpeed);
            }

            if (player.State == VLCState.Ended)
            {
                player.Stop();
            }

            player.Play();
            PlaybackState = PlaybackState.Playing;
        }

        private void Run(Action<MediaPlayer> action)
        {
            Task.Run(() =>
            {
                lock (_mediaPlayerLock)
                {
                    if (_mediaPlayer != null)
                    {
                        action(_mediaPlayer);
                    }
                }
            });
        }

        public void Seek(TimeSpan span)
        {
            Run(player => player.SeekTo(span));
        }

        public void MoveNext()
        {
            Run(MoveNextImpl);
        }

        public void MoveNextImpl(MediaPlayer player)
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

        public void MovePreviousImpl(MediaPlayer player)
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

        private void SetSource(MediaPlayer player, List<PlaybackItem> items, int index)
        {
            if (index >= 0 && index <= items.Count - 1)
            {
                SetSource(player, items[index]);
            }
        }

        private void SetSource(MediaPlayer player, PlaybackItem item)
        {
            try
            {
                player ??= Create();

                _playbackSpeed = item.CanChangePlaybackRate ? _settingsService.Playback.AudioSpeed : 1;
                CurrentPlayback = item;

                player.SetRate((float)_playbackSpeed);
                player.Play(new Media(_library, item.Stream));
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

        public void ClearImpl(MediaPlayer player)
        {
            PlaybackState = PlaybackState.None;

            //Execute.BeginOnUIThread(() => CurrentItem = null);
            CurrentPlayback = null;
            Dispose(true);
        }

        public void Play(MessageWithOwner message, long threadId)
        {
            _transport ??= WM.SystemMediaTransportControls.GetForCurrentView();

            Task.Run(() =>
            {
                lock (_mediaPlayerLock)
                {
                    _ = PlayAsync(message, threadId);
                }
            });
        }

        public async Task PlayAsync(MessageWithOwner message, long threadId)
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
                    SetSource(null, already);
                    return;
                }
            }

            Dispose(false);

            var item = GetPlaybackItem(message);
            var items = _items = new List<PlaybackItem>();

            _items.Add(item);
            _threadId = threadId;

            SetSource(null, item);

            if (message.Content is MessageText)
            {
                return;
            }

            var offset = -49;
            var filter = message.Content is MessageAudio ? new SearchMessagesFilterAudio() : (SearchMessagesFilter)new SearchMessagesFilterVoiceNote();

            var response = await message.ClientService.SendAsync(new SearchChatMessages(message.ChatId, string.Empty, null, message.Id, offset, 100, filter, _threadId));
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

            var stream = new RemoteFileStream(message.ClientService, file);
            var item = new PlaybackItem(stream)
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
            else if (message.Content is MessageText text && text.WebPage != null)
            {
                if (text.WebPage.Audio != null)
                {
                    file = text.WebPage.Audio.AudioValue;
                    speed = text.WebPage.Audio.Duration >= 10 * 60;
                }
                else if (text.WebPage.VoiceNote != null)
                {
                    file = text.WebPage.VoiceNote.Voice;
                    speed = true;
                }
                else if (text.WebPage.VideoNote != null)
                {
                    file = text.WebPage.VideoNote.Video;
                    speed = true;
                }
            }
        }

        private void Dispose(bool full)
        {
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Stop();
                //_mediaPlayer.CommandManager.IsEnabled = false;

                if (full)
                {
                    _transport.ButtonPressed -= Transport_ButtonPressed;

                    //_mediaPlayer.SystemMediaTransportControls.ButtonPressed -= Transport_ButtonPressed;
                    //_mediaPlayer.PlaybackSession.PlaybackStateChanged -= OnPlaybackStateChanged;
                    _mediaPlayer.TimeChanged -= OnTimeChanged;
                    _mediaPlayer.LengthChanged -= OnLengthChanged;
                    _mediaPlayer.EncounteredError -= OnEncounteredError;
                    _mediaPlayer.EndReached -= OnEndReached;
                    _mediaPlayer.MediaChanged -= OnMediaChanged;
                    _mediaPlayer.Dispose();

                    _mediaPlayer = null;
                }
            }

            if (_items != null)
            {
                foreach (var item in _items)
                {
                    item.Dispose();
                    //item.Stream.Dispose();
                }

                _items = null;
            }
        }

        private MediaPlayer Create()
        {
            if (_mediaPlayer == null)
            {
                _library = new LibVLC();
                //_library.Log += _library_Log;

                _mediaPlayer = new MediaPlayer(_library);
                //_mediaPlayer.SystemMediaTransportControls.AutoRepeatMode = _settingsService.Playback.RepeatMode;
                //_mediaPlayer.SystemMediaTransportControls.ButtonPressed += Transport_ButtonPressed;
                //_mediaPlayer.PlaybackSession.PlaybackStateChanged += OnPlaybackStateChanged;
                _mediaPlayer.TimeChanged += OnTimeChanged;
                _mediaPlayer.LengthChanged += OnLengthChanged;
                _mediaPlayer.EncounteredError += OnEncounteredError;
                _mediaPlayer.EndReached += OnEndReached;
                _mediaPlayer.MediaChanged += OnMediaChanged;
                //_mediaPlayer.CommandManager.IsEnabled = false;
                _mediaPlayer.Volume = (int)Math.Round(_settingsService.VolumeLevel * 100);

                _transport.ButtonPressed += Transport_ButtonPressed;
            }

            return _mediaPlayer;
        }

        private static readonly Regex _videoLooking = new("using (.*?) module \"(.*?)\" from (.*?)$", RegexOptions.Compiled);
        private static readonly object _syncObject = new();

        private void _library_Log(object sender, LogEventArgs e)
        {
            Debug.WriteLine(e.FormattedLog);

            lock (_syncObject)
            {
                var match = _videoLooking.Match(e.FormattedLog);
                if (match.Success)
                {
                    System.IO.File.AppendAllText(ApplicationData.Current.LocalFolder.Path + "\\vlc.txt", string.Format("{2}\n", match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value));
                }
            }
        }
    }

    public class PlaybackItem
    {
        public MessageWithOwner Message { get; set; }

        public RemoteFileStream Stream { get; set; }

        public string Title { get; set; }
        public string Performer { get; set; }

        public bool CanChangePlaybackRate { get; set; }

        public PlaybackItem(RemoteFileStream stream)
        {
            Stream = stream;
        }

        public void Dispose()
        {
            try
            {
                Stream?.Dispose();
            }
            catch
            {
                Logger.Error();
            }

            Stream = null;
        }
    }
}
