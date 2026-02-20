//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Converters;
using Telegram.Native.Media;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
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

    public partial class PlaybackPositionChangedEventArgs
    {
        public TimeSpan Position { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class AudioWithOwner
    {
        public AudioWithOwner(IClientService clientService, long userId, Audio audio)
        {
            ClientService = clientService;
            UserId = userId;

            AudioValue = audio.AudioValue;
            ExternalAlbumCovers = audio.ExternalAlbumCovers;
            AlbumCoverThumbnail = audio.AlbumCoverThumbnail;
            AlbumCoverMinithumbnail = audio.AlbumCoverMinithumbnail;
            MimeType = audio.MimeType;
            FileName = audio.FileName;
            Performer = audio.Performer;
            Title = audio.Title;
            Duration = audio.Duration;
        }

        public IClientService ClientService { get; set; }

        public long UserId { get; set; }

        /// <summary>
        /// File containing the audio.
        /// </summary>
        public File AudioValue { get; set; }

        /// <summary>
        /// Album cover variants to use if the downloaded audio file contains no album cover.
        /// Provided thumbnail dimensions are approximate.
        /// </summary>
        public IList<Thumbnail> ExternalAlbumCovers { get; set; }

        /// <summary>
        /// The thumbnail of the album cover in JPEG format; as defined by the sender. The
        /// full size thumbnail is expected to be extracted from the downloaded audio file;
        /// may be null.
        /// </summary>
        public Thumbnail AlbumCoverThumbnail { get; set; }

        /// <summary>
        /// The minithumbnail of the album cover; may be null.
        /// </summary>
        public Minithumbnail AlbumCoverMinithumbnail { get; set; }

        /// <summary>
        /// The MIME type of the file; as defined by the sender.
        /// </summary>
        public string MimeType { get; set; }

        /// <summary>
        /// Original name of the file; as defined by the sender.
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Performer of the audio; as defined by the sender.
        /// </summary>
        public string Performer { get; set; }

        /// <summary>
        /// Title of the audio; as defined by the sender.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Duration of the audio, in seconds; as defined by the sender.
        /// </summary>
        public int Duration { get; set; }
    }

    public interface IPlaybackService
    {
        IReadOnlyList<PlaybackItem> Items { get; }

        PlaybackItem CurrentItem { get; }

        double PlaybackSpeed { get; set; }

        double Volume { get; set; }

        void Pause();
        void Play();

        void MoveNext();
        void MovePrevious();

        void Seek(TimeSpan span);

        void Clear();

        void MoveTo(PlaybackItem item, int index);

        void Play(XamlRoot xamlRoot, MessageWithOwner message, MessageTopic topic = null);
        void Play(XamlRoot xamlRoot, AudioWithOwner audio);

        void Play(PlaybackItem item);

        void Attach(SwapChainPanel panel);
        void Detach(SwapChainPanel panel);

        TimeSpan Position { get; }
        TimeSpan Duration { get; }

        public bool IsPlaying { get; }

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

    public partial class PlaybackService : IPlaybackService
    {
        private readonly ISettingsService _settingsService;

        private AsyncMediaPlayer _player;
        private readonly object _mediaPlayerLock = new();

        private readonly PlaybackPositionChangedEventArgs _positionChanged = new();

        private WM.SystemMediaTransportControls _transport;

        private PlaybackPreviousState _previous;

        private int _sessionId;
        private PlaybackPlaylistType _type;

        private long _chatId;
        private MessageTopic _topic;

        private long _userId;

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

        private void OnBuffering(object sender, AsyncMediaPlayerBufferingEventArgs args)
        {
            if (args.Cache == 100)
            {
                var item = CurrentItem;
                if (item is PlaybackItemMessage message)
                {
                    var linkPreview = message.Message.Content is MessageText text ? text.LinkPreview : null;

                    if ((message.Message.Content is MessageVideoNote videoNote && !videoNote.IsViewed && !message.Message.IsOutgoing) || (message.Message.Content is MessageVoiceNote voiceNote && !voiceNote.IsListened && !message.Message.IsOutgoing))
                    {
                        message.ClientService.Send(new OpenMessageContent(message.ChatId, message.Id));
                    }
                }
            }
        }

        private void OnEndReached(object sender, object args)
        {
            var item = CurrentItem;
            if (item != null)
            {
                if (item is PlaybackItemMessage { Message.Content: MessageAudio } or PlaybackItemProfileAudio && _isRepeatEnabled == null)
                {
                    Play();
                }
                else
                {
                    MoveNext();
                }
            }
        }

        private void OnEncounteredError(object sender, object args)
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
                case AsyncMediaPlayerState.Playing:
                    //sender.MediaPlayer.SystemMediaTransportControls.PlaybackStatus = MediaPlaybackStatus.Playing;
                    break;
                case AsyncMediaPlayerState.Paused:
                    //sender.MediaPlayer.SystemMediaTransportControls.PlaybackStatus = MediaPlaybackStatus.Paused;
                    break;
                case AsyncMediaPlayerState.NothingSpecial:
                case AsyncMediaPlayerState.Stopped:
                    //sender.MediaPlayer.SystemMediaTransportControls.PlaybackStatus = MediaPlaybackStatus.Stopped;
                    PlaybackState = PlaybackState.None;
                    break;
            }
        }

        private void OnTimeChanged(AsyncMediaPlayer sender, AsyncMediaPlayerPositionChangedEventArgs args)
        {
            _positionChanged.Position = TimeSpan.FromSeconds(args.Position);
            PositionChanged?.Invoke(this, _positionChanged);
        }

        private void OnLengthChanged(AsyncMediaPlayer sender, AsyncMediaPlayerDurationChangedEventArgs args)
        {
            _positionChanged.Duration = TimeSpan.FromSeconds(args.Duration);
            PositionChanged?.Invoke(this, _positionChanged);
        }

        private void UpdateTransport(PlaybackItem item)
        {
            var items = _items;
            var transport = _transport;

            try
            {
                if (items == null || item == null || transport == null /*|| item?.Stream?.File == null*/)
                {
                    transport?.IsEnabled = false;
                    transport?.DisplayUpdater.ClearAll();
                    return;
                }

                transport.IsEnabled = true;
                transport.IsPlayEnabled = true;
                transport.IsPauseEnabled = true;
                transport.IsPreviousEnabled = true;
                transport.IsNextEnabled = items.Count > 1;

                transport.DisplayUpdater.ClearAll();
                transport.DisplayUpdater.Type = WM.MediaPlaybackType.Music;

                transport.DisplayUpdater.MusicProperties.Title = item.Title ?? string.Empty;
                transport.DisplayUpdater.MusicProperties.Artist = item.Performer ?? string.Empty;

                transport.DisplayUpdater.Update();
            }
            catch { }
        }

        public IReadOnlyList<PlaybackItem> Items => _items?.ToList() ?? (IReadOnlyList<PlaybackItem>)Array.Empty<PlaybackItem>();

        private PlaybackItem _currentItem;
        public PlaybackItem CurrentItem
        {
            get => _currentItem;
            private set
            {
                _currentItem = value;
                _positionChanged.Position = TimeSpan.Zero;
                _positionChanged.Duration = TimeSpan.FromSeconds(value?.Duration ?? 0);
                SourceChanged?.Invoke(this, value);
                UpdateTransport(value);
            }
        }

        public TimeSpan Position => _positionChanged.Position;

        public TimeSpan Duration => _positionChanged.Duration;

        public bool IsPlaying => PlaybackState == PlaybackState.Playing;

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
                    player.Rate = value;
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
                Run(player => player.Volume = value);
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
            if (CurrentItem is PlaybackItem item)
            {
                _playbackSpeed = item.CanChangePlaybackRate ? _settingsService.Playback.AudioSpeed : 1;
                player.Rate = _playbackSpeed;
            }

            if (player.State == AsyncMediaPlayerState.Ended)
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
            if (span.TotalSeconds < player.Position)
            {
                var playing = player.IsPlaying;

                player.Stop();
                player.Play();

                if (playing is false)
                {
                    player.Pause(true);
                }
            }

            player.Position = span.TotalSeconds;

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

            var index = items.IndexOf(CurrentItem);
            if (index == -1 || index == (_isReversed ? 0 : items.Count - 1))
            {
                if (CurrentItem is PlaybackItemMessage { Message.Content: MessageAudio } or PlaybackItemProfileAudio && _isRepeatEnabled == true)
                {
                    SetSource(player, items, _isReversed ? items.Count - 1 : 0);
                }
                else if (CurrentItem is not PlaybackItemMessage { Message.Content: MessageVoiceNote or MessageVideoNote })
                {
                    StopImpl(player);
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

            var index = items.IndexOf(CurrentItem);
            if (index == -1 || index == (_isReversed ? items.Count - 1 : 0))
            {
                if (CurrentItem is PlaybackItemMessage { Message.Content: MessageAudio } or PlaybackItemProfileAudio && _isRepeatEnabled == true)
                {
                    SetSource(player, items, _isReversed ? 0 : items.Count - 1);
                }
                else if (CurrentItem is not PlaybackItemMessage { Message.Content: MessageVoiceNote or MessageVideoNote })
                {
                    StopImpl(player);
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

                if (index == 0 || index == items.Count - 1)
                {
                    // TODO: Load more items
                }
            }
        }

        private void SetSource(AsyncMediaPlayer player, PlaybackItem item)
        {
            try
            {
                player ??= Create();

                _playbackSpeed = item.CanChangePlaybackRate ? _settingsService.Playback.AudioSpeed : 1;
                CurrentItem = item;

                player.Rate = _playbackSpeed;
                player.Play(new RemoteFileSource(item.ClientService, item.Document, item.Duration));
                PlaybackState = PlaybackState.Playing;
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }

        private void StopImpl(AsyncMediaPlayer player)
        {
            PlaybackState = PlaybackState.Paused;
            player.Stop();

            _positionChanged.Position = TimeSpan.Zero;
            PositionChanged?.Invoke(this, _positionChanged);
        }

        public void Clear()
        {
            Run(ClearImpl);
        }

        private void ClearImpl(AsyncMediaPlayer player)
        {
            if (_previous != null)
            {
                _items = _previous.Items;
                _playbackSpeed = _previous.CurrentItem.CanChangePlaybackRate ? _settingsService.Playback.AudioSpeed : 1;
                CurrentItem = _previous.CurrentItem;

                player.Rate = _playbackSpeed;
                player.Play(new RemoteFileSource(_previous.CurrentItem.ClientService, _previous.CurrentItem.Document, _previous.CurrentItem.Duration));
                player.Position = _previous.Position;

                _positionChanged.Position = TimeSpan.FromSeconds(_previous.Position);
                PositionChanged?.Invoke(this, _positionChanged);

                if (_previous.State != PlaybackState.Playing)
                {
                    player.Pause();
                    PlaybackState = PlaybackState.Paused;
                }
                else
                {
                    PlaybackState = PlaybackState.Playing;
                }

                _previous = null;
            }
            else
            {
                PlaybackState = PlaybackState.None;

                CurrentItem = null;
                Dispose(PlaybackPlaylistType.None);
            }
        }

        public void MoveTo(PlaybackItem item, int index)
        {
            if (_items.Contains(item))
            {
                _items.Remove(item);
                _items.Insert(index, item);

                PlaylistChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Play(PlaybackItem item)
        {
            lock (_mediaPlayerLock)
            {
                SetSource(_player, item);
            }
        }

        public async void Play(XamlRoot xamlRoot, MessageWithOwner message, MessageTopic topic)
        {
            try
            {
                _transport ??= WM.SystemMediaTransportControls.GetForCurrentView();
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }

            if (message == null)
            {
                return;
            }

            var previous = _items;
            if (previous != null && _sessionId == message.ClientService.SessionId && _chatId == message.ChatId && _topic.AreTheSame(topic))
            {
                var already = previous.FirstOrDefault(x => message.AreTheSame(x));
                if (already != null)
                {
                    SetSource(null, already);
                    return;
                }
            }

            Dispose(message.Content is MessageAudio
                ? PlaybackPlaylistType.Audio
                : PlaybackPlaylistType.Voice);

            var item = new PlaybackItemMessage(xamlRoot, message, topic);
            var items = _items = new List<PlaybackItem>();

            _items.Add(item);

            _sessionId = message.ClientService.SessionId;
            _chatId = message.ChatId;
            _topic = topic;
            _userId = 0;

            SetSource(null, item);

            if (message.Content is MessageText)
            {
                return;
            }

            var offset = -49;
            var filter = message.Content is MessageAudio ? new SearchMessagesFilterAudio() : (SearchMessagesFilter)new SearchMessagesFilterVoiceAndVideoNote();

            var response = await message.ClientService.SendAsync(new SearchChatMessages(message.ChatId, _topic, string.Empty, null, message.Id, offset, 100, filter));
            if (response is FoundChatMessages messages)
            {
                foreach (var add in message.Content is MessageAudio ? messages.Messages.OrderBy(x => x.Id) : messages.Messages.OrderByDescending(x => x.Id))
                {
                    if (add.Id > message.Id && add.Content is MessageAudio)
                    {
                        items.Insert(0, new PlaybackItemMessage(xamlRoot, new MessageWithOwner(message.ClientService, add), topic));
                    }
                    else if (add.Id < message.Id && (add.Content is MessageVoiceNote || add.Content is MessageVideoNote))
                    {
                        items.Insert(0, new PlaybackItemMessage(xamlRoot, new MessageWithOwner(message.ClientService, add), topic));
                    }
                }

                foreach (var add in message.Content is MessageAudio ? messages.Messages.OrderByDescending(x => x.Id) : messages.Messages.OrderBy(x => x.Id))
                {
                    if (add.Id < message.Id && add.Content is MessageAudio)
                    {
                        items.Add(new PlaybackItemMessage(xamlRoot, new MessageWithOwner(message.ClientService, add), topic));
                    }
                    else if (add.Id > message.Id && (add.Content is MessageVoiceNote || add.Content is MessageVideoNote))
                    {
                        items.Add(new PlaybackItemMessage(xamlRoot, new MessageWithOwner(message.ClientService, add), topic));
                    }
                }

                UpdateTransport(CurrentItem);
                PlaylistChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public async void Play(XamlRoot xamlRoot, AudioWithOwner audio)
        {
            try
            {
                _transport ??= WM.SystemMediaTransportControls.GetForCurrentView();
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }

            if (audio == null)
            {
                return;
            }

            var previous = _items;
            if (previous != null && _sessionId == audio.ClientService.SessionId && _userId == audio.UserId)
            {
                var already = previous.FirstOrDefault(x => audio.AreTheSame(x));
                if (already != null)
                {
                    if (already != CurrentItem)
                    {
                        SetSource(null, already);
                    }

                    return;
                }
            }

            Dispose(PlaybackPlaylistType.ProfileAudio);

            var item = new PlaybackItemProfileAudio(xamlRoot, audio);
            var items = _items = new List<PlaybackItem>();

            _items.Add(item);

            _sessionId = audio.ClientService.SessionId;
            _userId = audio.UserId;
            _chatId = 0;
            _topic = null;

            SetSource(null, item);

            var response = await audio.ClientService.SendAsync(new GetUserProfileAudios(audio.UserId, 0, 100));
            if (response is Audios audios)
            {
                foreach (var add in audios.AudiosValue)
                {
                    if (add.AudioValue.Id != audio.AudioValue.Id)
                    {
                        items.Add(new PlaybackItemProfileAudio(xamlRoot, new AudioWithOwner(audio.ClientService, audio.UserId, add)));
                    }
                }

                UpdateTransport(CurrentItem);
                PlaylistChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void Dispose(PlaybackPlaylistType type)
        {
            if (_player != null)
            {
                //_mediaPlayer.CommandManager.IsEnabled = false;

                if (type == PlaybackPlaylistType.None)
                {
                    _transport.ButtonPressed -= Transport_ButtonPressed;
                    _previous = null;

                    //_mediaPlayer.SystemMediaTransportControls.ButtonPressed -= Transport_ButtonPressed;
                    //_mediaPlayer.PlaybackSession.PlaybackStateChanged -= OnPlaybackStateChanged;
                    _player.PositionChanged -= OnTimeChanged;
                    _player.DurationChanged -= OnLengthChanged;
                    _player.StateChanged -= OnStateChanged;
                    _player.Buffering -= OnBuffering;
                    _player.Close();

                    lock (_mediaPlayerLock)
                    {
                        _player = null;
                    }

                    //IsPlaying = false;
                }
                else
                {
                    if (type is PlaybackPlaylistType.Voice && _type is PlaybackPlaylistType.Audio or PlaybackPlaylistType.ProfileAudio && CurrentItem != null)
                    {
                        _previous ??= new PlaybackPreviousState(this, _player);
                    }

                    _player.Stop();
                }
            }

            _items = null;
            _type = type;
        }

        private void OnStateChanged(AsyncMediaPlayer sender, AsyncMediaPlayerStateChangedEventArgs args)
        {
            //IsPlaying = args.State == AsyncMediaPlayerState.Playing;

            if (args.State == AsyncMediaPlayerState.Ended)
            {
                OnEndReached(sender, args);
            }
            else if (args.State == AsyncMediaPlayerState.Error)
            {
                OnEncounteredError(sender, args);
            }
        }

        enum PlaybackPlaylistType
        {
            None,
            Audio,
            Voice,
            ProfileAudio
        };

        class PlaybackPreviousState
        {
            public List<PlaybackItem> Items { get; }

            public PlaybackItem CurrentItem { get; }

            public double Position { get; }

            public PlaybackState State { get; }

            public PlaybackPreviousState(PlaybackService service, AsyncMediaPlayer player)
            {
                Items = service._items.ToList();
                CurrentItem = service.CurrentItem;
                Position = player.Position;
                State = service.PlaybackState;
            }
        }

        private AsyncMediaPlayer Create()
        {
            if (_player == null)
            {
                // TODO: currently music player doesn't have a toggle for mute/unmute
                var options = new AsyncMediaPlayerOptions
                {
                    CreateSwapChain = true,
                    Mute = false, //SettingsService.Current.VolumeMuted,
                    Volume = SettingsService.Current.VolumeLevel,
                    Debug = SettingsService.Current.VerbosityLevel >= 4,
                };

                _player = new AsyncMediaPlayer(options);
                //_mediaPlayer.SystemMediaTransportControls.AutoRepeatMode = _settingsService.Playback.RepeatMode;
                //_mediaPlayer.SystemMediaTransportControls.ButtonPressed += Transport_ButtonPressed;
                //_mediaPlayer.PlaybackSession.PlaybackStateChanged += OnPlaybackStateChanged;
                _player.PositionChanged += OnTimeChanged;
                _player.DurationChanged += OnLengthChanged;
                _player.StateChanged += OnStateChanged;
                _player.Buffering += OnBuffering;
                //_mediaPlayer.CommandManager.IsEnabled = false;

                _transport.ButtonPressed += Transport_ButtonPressed;
            }

            return _player;
        }

        public void Attach(SwapChainPanel panel)
        {
            Run(player => player.Context.Attach(panel, true));
        }

        public void Detach(SwapChainPanel panel)
        {
            Run(player => player.Context.Detach(panel));
        }
    }

    public abstract class PlaybackItem
    {
        public IClientService ClientService { get; protected set; }

        public XamlRoot XamlRoot { get; protected set; }

        public File Document { get; protected set; }

        public string Title { get; protected set; }
        public string Performer { get; protected set; }

        public int Duration { get; protected set; }

        public bool CanChangePlaybackRate { get; protected set; }
    }

    public partial class PlaybackItemMessage : PlaybackItem
    {
        public MessageWithOwner Message { get; }

        public long ChatId { get; }

        public long Id { get; }

        public MessageTopic TopicId { get; }

        public PlaybackItemMessage(XamlRoot xamlRoot, MessageWithOwner message, MessageTopic topicId)
        {
            ClientService = message.ClientService;
            XamlRoot = xamlRoot;
            Message = message;
            TopicId = topicId;
            ChatId = message.ChatId;
            Id = message.Id;

            if (message.Content is MessageAudio audio)
            {
                Document = audio.Audio.AudioValue;
                Duration = audio.Audio.Duration;
                CanChangePlaybackRate = audio.Audio.Duration >= 10 * 60;

                if (string.IsNullOrEmpty(audio.Audio.Title))
                {
                    Title = audio.Audio.FileName;
                    Performer = string.Empty;
                }
                else
                {
                    Title = audio.Audio.Title;
                    Performer = audio.Audio.Performer;
                }
            }
            else if (message.Content is MessageVoiceNote voiceNote)
            {
                Document = voiceNote.VoiceNote.Voice;
                Duration = voiceNote.VoiceNote.Duration;
                CanChangePlaybackRate = true;

                var title = string.Empty;
                var date = Formatter.DateAt(message.Date);

                if (message.ClientService.TryGetUser(message.SenderId, out Telegram.Td.Api.User senderUser))
                {
                    title = senderUser.Id == message.ClientService.Options.MyId ? Strings.ChatYourSelfName : senderUser.FullName();
                }
                else if (message.ClientService.TryGetChat(message.SenderId, out Chat senderChat))
                {
                    title = message.ClientService.GetTitle(senderChat);
                }

                Title = title;
                Performer = date;
            }
            else if (message.Content is MessageVideoNote videoNote)
            {
                Document = videoNote.VideoNote.Video;
                Duration = videoNote.VideoNote.Duration;
                CanChangePlaybackRate = true;

                var title = string.Empty;
                var date = Formatter.DateAt(message.Date);

                if (message.ClientService.TryGetUser(message.SenderId, out Telegram.Td.Api.User senderUser))
                {
                    title = senderUser.Id == message.ClientService.Options.MyId ? Strings.ChatYourSelfName : senderUser.FullName();
                }
                else if (message.ClientService.TryGetChat(message.SenderId, out Chat senderChat))
                {
                    title = message.ClientService.GetTitle(senderChat);
                }

                Title = title;
                Performer = date;
            }
            else if (message.Content is MessageText text && text.LinkPreview != null)
            {
                if (text.LinkPreview.Type is LinkPreviewTypeAudio previewAudio)
                {
                    Document = previewAudio.Audio.AudioValue;
                    Duration = previewAudio.Audio.Duration;
                    CanChangePlaybackRate = previewAudio.Audio.Duration >= 10 * 60;

                    if (string.IsNullOrEmpty(previewAudio.Audio.Title))
                    {
                        Title = previewAudio.Audio.FileName;
                        Performer = string.Empty;
                    }
                    else
                    {
                        Title = previewAudio.Audio.Title;
                        Performer = previewAudio.Audio.Performer;
                    }
                }
                else if (text.LinkPreview.Type is LinkPreviewTypeVoiceNote previewVoiceNote)
                {
                    Document = previewVoiceNote.VoiceNote.Voice;
                    Duration = previewVoiceNote.VoiceNote.Duration;
                    CanChangePlaybackRate = true;

                    var title = string.Empty;
                    var date = Formatter.DateAt(message.Date);

                    if (message.ClientService.TryGetUser(message.SenderId, out Telegram.Td.Api.User senderUser))
                    {
                        title = senderUser.Id == message.ClientService.Options.MyId ? Strings.ChatYourSelfName : senderUser.FullName();
                    }
                    else if (message.ClientService.TryGetChat(message.SenderId, out Chat senderChat))
                    {
                        title = message.ClientService.GetTitle(senderChat);
                    }

                    Title = title;
                    Performer = date;
                }
                else if (text.LinkPreview.Type is LinkPreviewTypeVideoNote previewVideoNote)
                {
                    Document = previewVideoNote.VideoNote.Video;
                    Duration = previewVideoNote.VideoNote.Duration;
                    CanChangePlaybackRate = true;

                    var title = string.Empty;
                    var date = Formatter.DateAt(message.Date);

                    if (message.ClientService.TryGetUser(message.SenderId, out Telegram.Td.Api.User senderUser))
                    {
                        title = senderUser.Id == message.ClientService.Options.MyId ? Strings.ChatYourSelfName : senderUser.FullName();
                    }
                    else if (message.ClientService.TryGetChat(message.SenderId, out Chat senderChat))
                    {
                        title = message.ClientService.GetTitle(senderChat);
                    }

                    Title = title;
                    Performer = date;
                }
            }
        }
    }

    public partial class PlaybackItemProfileAudio : PlaybackItem
    {
        public AudioWithOwner Audio { get; }

        public long UserId { get; }

        public int Id { get; }

        public PlaybackItemProfileAudio(XamlRoot xamlRoot, AudioWithOwner audio)
        {
            ClientService = audio.ClientService;
            XamlRoot = xamlRoot;
            Audio = audio;
            UserId = audio.UserId;
            Id = audio.AudioValue.Id;
            Document = audio.AudioValue;
            Duration = audio.Duration;
            CanChangePlaybackRate = audio.Duration >= 10 * 60;

            if (string.IsNullOrEmpty(audio.Title))
            {
                Title = audio.FileName;
                Performer = string.Empty;
            }
            else
            {
                Title = audio.Title;
                Performer = audio.Performer;
            }
        }

        public InputMessageContent ToInputMessage()
        {
            return new InputMessageAudio(new InputFileId(Audio.AudioValue.Id), Audio.AlbumCoverThumbnail.ToInput(), Audio.Duration, Audio.Title, Audio.Performer, null);
        }
    }
}
