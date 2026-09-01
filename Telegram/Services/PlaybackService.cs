//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Native.Media;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
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

            Value = audio;
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
        /// The audio the fields below were copied from, kept so it can be handed back to
        /// anything that wants the whole thing rather than one field.
        /// </summary>
        public Audio Value { get; }

        /// <summary>
        /// File containing the audio.
        /// </summary>
        public File AudioValue { get; set; }

        /// <summary>
        /// Album cover variants to use if the downloaded audio file contains no album cover.
        /// Provided thumbnail dimensions are approximate.
        /// </summary>
        public Vector<Thumbnail> ExternalAlbumCovers { get; set; }

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

        /// <summary>
        /// Tells the service an audio was added to the current user's profile. There is no
        /// update for it, so a profile audio playlist can only learn from whoever asked.
        /// </summary>
        void ProfileAudioAdded(PlaybackItem item);

        /// <summary>
        /// Tells the service an audio was removed from the current user's profile.
        /// </summary>
        void ProfileAudioRemoved(int fileId);

        void Play(XamlRoot xamlRoot, MessageWithOwner message, MessageTopic topic = null);
        void Play(XamlRoot xamlRoot, AudioWithOwner audio);

        /// <summary>
        /// Starts a profile audio playlist from what the caller has already loaded, handing
        /// over the source that loaded it so the playlist keeps growing from there.
        /// </summary>
        void Play(XamlRoot xamlRoot, AudioWithOwner audio, UserProfileAudioPlaybackSource source, IList<PlaybackItem> loaded);

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
        private AsyncMediaPlayer _player;
        private readonly object _mediaPlayerLock = new();

        private readonly PlaybackPositionChangedEventArgs _positionChanged = new();

        private WM.SystemMediaTransportControls _transport;

        // The transport belongs to the view it was acquired for, and its display updater is
        // driven from file updates that arrive on a TDLib thread, so every call has to come
        // back here first.
        private DispatcherQueue _transportQueue;

        private long _albumCoverToken;

        private PlaybackPreviousState _previous;

        private int _sessionId;
        private PlaybackPlaylistType _type;

        private long _chatId;
        private MessageTopic _topic;

        private long _userId;

        private List<PlaybackItem> _items;

        private PlaybackSource _source;

        private bool _loadingStart;
        private bool _loadingEnd;

        // Set while the playlist is only the item playback started from, because nothing was
        // handed over to say where it sits. The first page replaces it rather than being
        // appended to it, so the list ends up in the order the source reports.
        private bool _provisional;

        // The aggregator of the session being played. Per session, so not the static one the
        // file subscriptions go through.
        private IEventAggregator _aggregator;

        public event TypedEventHandler<IPlaybackService, object> MediaFailed;
        public event TypedEventHandler<IPlaybackService, object> StateChanged;
        public event TypedEventHandler<IPlaybackService, object> SourceChanged;
        public event TypedEventHandler<IPlaybackService, PlaybackPositionChangedEventArgs> PositionChanged;
        public event TypedEventHandler<IPlaybackService, object> PlaylistChanged;

        public PlaybackService()
        {

            _isRepeatEnabled = AppSettings.Playback.RepeatMode == PlaybackRepeatMode.Track
                ? null
                : AppSettings.Playback.RepeatMode == PlaybackRepeatMode.List;
            _isShuffleEnabled = AppSettings.Playback.Shuffle;
            _playbackSpeed = AppSettings.Playback.AudioSpeed;
        }

        #region SystemMediaTransportControls

        private void EnsureTransport()
        {
            if (_transport != null)
            {
                return;
            }

            try
            {
                _transport = WM.SystemMediaTransportControls.GetForCurrentView();
                _transportQueue = DispatcherQueue.GetForCurrentThread();

                _transport.AutoRepeatMode = ToAutoRepeatMode(_isRepeatEnabled);
                _transport.ShuffleEnabled = _isShuffleEnabled;
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }

        private static WM.MediaPlaybackAutoRepeatMode ToAutoRepeatMode(bool? value)
        {
            return value == true
                ? WM.MediaPlaybackAutoRepeatMode.List
                : value == null
                ? WM.MediaPlaybackAutoRepeatMode.Track
                : WM.MediaPlaybackAutoRepeatMode.None;
        }

        /// <summary>
        /// Runs an action against <see cref="_transport"/> on the thread of the view it was
        /// acquired for, doing nothing if there is no transport.
        /// </summary>
        /// <remarks>
        /// The transport belongs to one view, but playback is driven from whichever window
        /// started it, from the player's own dispatcher, and from TDLib file updates. Those
        /// are the same thread only until a Clear is followed by playback from another
        /// window, at which point the player is recreated on that window's thread while the
        /// transport stays behind on the first one.
        /// </remarks>
        private void RunOnTransport(Action action)
        {
            var queue = _transportQueue;
            if (queue == null || _transport == null)
            {
                return;
            }

            if (queue.HasThreadAccess)
            {
                action();
            }
            else
            {
                queue.TryEnqueue(new DispatcherQueueHandler(action));
            }
        }

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
            RunOnTransport(() => UpdateTransportImpl(item));
        }

        private void UpdateTransportImpl(PlaybackItem item)
        {
            var items = _items;
            var transport = _transport;

            try
            {
                if (items == null || item == null || transport == null /*|| item?.Stream?.File == null*/)
                {
                    UpdateAlbumCover(null);

                    transport?.IsEnabled = false;
                    transport?.DisplayUpdater.ClearAll();
                    return;
                }

                transport.IsEnabled = true;
                transport.IsPlayEnabled = true;
                transport.IsPauseEnabled = true;
                transport.IsPreviousEnabled = true;
                transport.IsNextEnabled = items.Count > 1;

                // ClearAll also drops the thumbnail of the previous track, so the cover has to
                // be re-applied after it -- UpdateAlbumCover does that once the file is there.
                transport.DisplayUpdater.ClearAll();
                transport.DisplayUpdater.Type = WM.MediaPlaybackType.Music;

                transport.DisplayUpdater.MusicProperties.Title = item.Title ?? string.Empty;
                transport.DisplayUpdater.MusicProperties.Artist = item.Performer ?? string.Empty;

                transport.DisplayUpdater.Update();

                UpdateAlbumCover(item);
            }
            catch { }
        }

        private void UpdateAlbumCover(PlaybackItem item)
        {
            var cover = item?.AlbumCover;
            if (cover == null)
            {
                // The subscription is keyed on the file, and this service outlives every track
                // it plays: leaving it behind would keep one handler per played file alive.
                UpdateManager.Unsubscribe(this, ref _albumCoverToken);
                return;
            }

            UpdateManager.Subscribe(this, item.ClientService, cover.File, ref _albumCoverToken, UpdateAlbumCoverFile, true);

            if (cover.File.Local.IsDownloadingCompleted)
            {
                SetAlbumCover(item, cover.File.Local.Path);
            }
            else if (cover.File.Local.CanBeDownloaded && !cover.File.Local.IsDownloadingActive)
            {
                item.ClientService.DownloadFile(cover.File.Id, 1);
            }
        }

        private void UpdateAlbumCoverFile(File file)
        {
            // Delivered on a TDLib thread: PlaybackService is neither a FrameworkElement nor a
            // ViewModelBase, so the aggregator has nothing to marshal through.
            var item = _currentItem;

            if (file.Local.IsDownloadingCompleted && item?.AlbumCover?.File.Id == file.Id)
            {
                SetAlbumCover(item, file.Local.Path);
            }
        }

        private void SetAlbumCover(PlaybackItem item, string path)
        {
            RunOnTransport(() => SetAlbumCoverImpl(item, path));
        }

        private async void SetAlbumCoverImpl(PlaybackItem item, string path)
        {
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(path);

                // The track can have moved on while the file was being opened, and the transport
                // would then be left showing the cover of a track that is no longer playing.
                if (_currentItem != item || _transport == null)
                {
                    return;
                }

                _transport.DisplayUpdater.Thumbnail = RandomAccessStreamReference.CreateFromFile(file);
                _transport.DisplayUpdater.Update();
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
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

                    var status = value switch
                    {
                        PlaybackState.Playing => WM.MediaPlaybackStatus.Playing,
                        PlaybackState.Paused => WM.MediaPlaybackStatus.Paused,
                        PlaybackState.None or _ => WM.MediaPlaybackStatus.Stopped
                    };

                    RunOnTransport(() => _transport.PlaybackStatus = status);
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
                AppSettings.Playback.RepeatMode = value == true
                    ? PlaybackRepeatMode.List
                    : value == null
                    ? PlaybackRepeatMode.Track
                    : PlaybackRepeatMode.None;

                RunOnTransport(() => _transport.AutoRepeatMode = ToAutoRepeatMode(value));
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
                AppSettings.Playback.Shuffle = value;

                RunOnTransport(() => _transport.ShuffleEnabled = value);
            }
        }

        private double _playbackSpeed = 1.0;
        public double PlaybackSpeed
        {
            get => _playbackSpeed;
            set
            {
                _playbackSpeed = value;
                AppSettings.Playback.AudioSpeed = value;

                Run(player =>
                {
                    player.Rate = value;
                    //player.SystemMediaTransportControls.PlaybackRate = value;
                });
            }
        }

        public double Volume
        {
            get => AppSettings.VolumeLevel;
            set
            {
                AppSettings.VolumeLevel = value;
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
                _playbackSpeed = item.CanChangePlaybackRate ? AppSettings.Playback.AudioSpeed : 1;
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

                // Both ends are checked whichever way playback is going: IsReversed swaps
                // what Next means, and the user can jump anywhere from the playlist popup.
                if (index <= LoadMoreThreshold)
                {
                    _ = LoadMoreAsync(false);
                }

                if (index >= items.Count - 1 - LoadMoreThreshold)
                {
                    _ = LoadMoreAsync(true);
                }
            }
        }

        // How close to an end of the playlist the current track has to get before the next
        // page is fetched. Enough to cover a few quick skips at the cost of one request.
        private const int LoadMoreThreshold = 3;

        private async Task LoadMoreAsync(bool forward)
        {
            var source = _source;
            if (source == null || !source.HasMore(forward))
            {
                return;
            }

            if (forward ? _loadingEnd : _loadingStart)
            {
                return;
            }

            if (forward)
            {
                _loadingEnd = true;
            }
            else
            {
                _loadingStart = true;
            }

            try
            {
                var page = await source.LoadMoreAsync(forward);

                // Playback may have moved to another playlist entirely while the page was in
                // flight, and the items in it belong to the one that asked for them.
                if (_source != source || page.Count == 0)
                {
                    return;
                }

                if (forward && _provisional && ReplaceProvisional(page))
                {
                    return;
                }

                AddItems(page, forward);
                PlaylistChanged?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                if (forward)
                {
                    _loadingEnd = false;
                }
                else
                {
                    _loadingStart = false;
                }
            }
        }

        /// <summary>
        /// Swaps a provisional playlist for the first page, when that page turns out to
        /// contain the item playback started from. Returns whether it did.
        /// </summary>
        private bool ReplaceProvisional(IList<PlaybackItem> page)
        {
            _provisional = false;

            var current = _currentItem;
            if (current == null)
            {
                return false;
            }

            for (int i = 0; i < page.Count; i++)
            {
                if (!page[i].AreTheSame(current))
                {
                    continue;
                }

                // Keep the instance that is already playing, so CurrentItem stays the object
                // the playlist holds and playback is not restarted to say so.
                var items = new List<PlaybackItem>(page);
                items[i] = current;

                lock (_mediaPlayerLock)
                {
                    _items = items;
                }

                PlaylistChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }

            return false;
        }

        #region Reconciliation

        private void SubscribeUpdates(IClientService clientService)
        {
            UnsubscribeUpdates();

            var aggregator = clientService.Session?.Resolve<IEventAggregator>();
            if (aggregator == null)
            {
                return;
            }

            _aggregator = aggregator;

            aggregator.Subscribe<UpdateNewMessage>(this, Handle)
                .Subscribe<UpdateDeleteMessages>(Handle)
                .Subscribe<UpdateMessageContent>(Handle);
        }

        private void UnsubscribeUpdates()
        {
            // Unsubscribe(subscriber) drops every type this service subscribed on that
            // aggregator, which is all three and nothing else: the album cover file token
            // lives on EventAggregator.Current instead.
            var aggregator = _aggregator;
            _aggregator = null;

            aggregator?.Unsubscribe(this);
        }

        private void Handle(UpdateNewMessage update)
        {
            if (_source is not ChatPlaybackSource source || !source.Accepts(update.Message))
            {
                return;
            }

            // A message newer than the whole playlist can only be added once the newest end
            // has been reached; before that there are messages in between, and moving the
            // cursor over them would lose them.
            if (!source.CanAddNewest)
            {
                return;
            }

            bool added;

            lock (_mediaPlayerLock)
            {
                var items = _items;

                added = items != null;
                if (added)
                {
                    items.Insert(source.NewestFirst ? 0 : items.Count, source.Create(update.Message));
                }
            }

            if (added)
            {
                source.Extend(update.Message.Id);
                PlaylistChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void Handle(UpdateDeleteMessages update)
        {
            // FromCache is TDLib forgetting a message, not the message going away, and a
            // non-permanent delete is one it expects to see again.
            if (update.FromCache || !update.IsPermanent)
            {
                return;
            }

            if (_source is not ChatPlaybackSource source || source.ChatId != update.ChatId)
            {
                return;
            }

            var current = _currentItem;
            var removedCurrent = false;
            var removed = false;

            lock (_mediaPlayerLock)
            {
                var items = _items;
                if (items == null)
                {
                    return;
                }

                for (int i = items.Count - 1; i >= 0; i--)
                {
                    if (items[i] is not PlaybackItemMessage message || !update.MessageIds.Contains(message.Id))
                    {
                        continue;
                    }

                    removedCurrent |= items[i] == current;
                    items.RemoveAt(i);
                    removed = true;
                }
            }

            if (!removed)
            {
                return;
            }

            PlaylistChanged?.Invoke(this, EventArgs.Empty);

            // A deleted message must not keep playing: whoever sent it took it back.
            if (removedCurrent)
            {
                MoveNext();
            }
        }

        private void Handle(UpdateMessageContent update)
        {
            if (_source is not ChatPlaybackSource source || source.ChatId != update.ChatId)
            {
                return;
            }

            var current = _currentItem;

            PlaybackItem replacement = null;
            var replacedCurrent = false;
            var removedCurrent = false;

            lock (_mediaPlayerLock)
            {
                var items = _items;
                if (items == null)
                {
                    return;
                }

                var index = items.FindIndex(x => x is PlaybackItemMessage message && message.Id == update.MessageId);
                if (index < 0)
                {
                    return;
                }

                var item = (PlaybackItemMessage)items[index];

                if (source.Accepts(update.NewContent))
                {
                    // Everything an item exposes is read off the content when it is built, so
                    // a new content makes a new item rather than a patched one.
                    item.Message.Content = update.NewContent;

                    replacement = new PlaybackItemMessage(item.XamlRoot, item.Message, item.TopicId);
                    replacedCurrent = item == current;

                    items[index] = replacement;
                }
                else
                {
                    // A voice note that expired, or media edited into something that is not
                    // played at all.
                    removedCurrent = item == current;

                    items.RemoveAt(index);
                }
            }

            if (replacedCurrent)
            {
                // An edit can replace the file as well, but the player is already streaming
                // the old one: take the new item for what is shown and leave playback alone.
                _currentItem = replacement;
                UpdateTransport(replacement);
            }

            PlaylistChanged?.Invoke(this, EventArgs.Empty);

            // Same as a delete: what is gone must not keep playing.
            if (removedCurrent)
            {
                MoveNext();
            }
        }

        public void ProfileAudioAdded(PlaybackItem item)
        {
            var audio = item?.Track;

            // addProfileAudio puts the audio first, and only the current user's own profile
            // can be added to.
            if (audio == null || _source is not UserProfileAudioPlaybackSource source || source.UserId != source.ClientService.Options.MyId)
            {
                return;
            }

            var added = new PlaybackItemProfileAudio(item.XamlRoot, new AudioWithOwner(source.ClientService, _userId, audio));

            lock (_mediaPlayerLock)
            {
                var items = _items;
                if (items == null)
                {
                    return;
                }

                items.Insert(0, added);
            }

            // Paging is by position, so everything after it moved down one.
            source.Skip(1);
            PlaylistChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ProfileAudioRemoved(int fileId)
        {
            if (_source is not UserProfileAudioPlaybackSource source || source.UserId != source.ClientService.Options.MyId)
            {
                return;
            }

            lock (_mediaPlayerLock)
            {
                var items = _items;
                if (items == null)
                {
                    return;
                }

                var index = items.FindIndex(x => x.Document.Id == fileId);
                if (index < 0)
                {
                    return;
                }

                items.RemoveAt(index);
            }

            // Removing from your own profile is not somebody taking a message back, so an
            // audio playing when it leaves the list plays on.
            source.Skip(-1);
            PlaylistChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        // The list is read from the player's dispatcher and from whichever window drives
        // playback, so it is only ever mutated under the lock those reads already take.
        private void AddItems(IList<PlaybackItem> page, bool forward)
        {
            lock (_mediaPlayerLock)
            {
                var items = _items;
                if (items == null)
                {
                    return;
                }

                if (forward)
                {
                    items.AddRange(page);
                }
                else
                {
                    items.InsertRange(0, page);
                }
            }
        }

        private void SetSource(AsyncMediaPlayer player, PlaybackItem item)
        {
            try
            {
                player ??= Create();

                _playbackSpeed = item.CanChangePlaybackRate ? AppSettings.Playback.AudioSpeed : 1;
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
                _source = _previous.Source;
                _playbackSpeed = _previous.CurrentItem.CanChangePlaybackRate ? AppSettings.Playback.AudioSpeed : 1;

                if (_previous.Source != null)
                {
                    SubscribeUpdates(_previous.Source.ClientService);
                }

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
            bool moved;

            lock (_mediaPlayerLock)
            {
                var items = _items;

                moved = items != null && items.Remove(item);
                if (moved)
                {
                    items.Insert(Math.Clamp(index, 0, items.Count), item);
                }
            }

            if (moved)
            {
                PlaylistChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Play(PlaybackItem item)
        {
            EnsureTransport();

            lock (_mediaPlayerLock)
            {
                SetSource(_player, item);
            }
        }

        public async void Play(XamlRoot xamlRoot, MessageWithOwner message, MessageTopic topic)
        {
            EnsureTransport();

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

            _items = new List<PlaybackItem> { item };
            _sessionId = message.ClientService.SessionId;
            _chatId = message.ChatId;
            _topic = topic;
            _userId = 0;

            if (message.Content is MessageText)
            {
                // The audio of a link preview belongs to no chat playlist.
                SetSource(null, item);
                return;
            }

            var source = new ChatPlaybackSource(message.ClientService, xamlRoot, message.ChatId, topic, message.Content is MessageAudio);
            source.Seed(message.Id);

            _source = source;
            SubscribeUpdates(message.ClientService);

            SetSource(null, item);

            // Both ends at once: the first page in either direction is what the playlist was
            // before it could grow, and waiting for one to decide the other would show the
            // list filling in twice.
            await Task.WhenAll(LoadMoreAsync(false), LoadMoreAsync(true));
        }

        public void Play(XamlRoot xamlRoot, AudioWithOwner audio)
        {
            Play(xamlRoot, audio, null, null);
        }

        public async void Play(XamlRoot xamlRoot, AudioWithOwner audio, UserProfileAudioPlaybackSource source, IList<PlaybackItem> loaded)
        {
            EnsureTransport();

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
            var items = new List<PlaybackItem>();

            // A caller that already paged some of the profile in hands them over, so the list
            // keeps its real order and the service does not start again from the first page.
            if (loaded != null)
            {
                items.AddRange(loaded);
            }

            if (source == null)
            {
                source = new UserProfileAudioPlaybackSource(audio.ClientService, xamlRoot, audio.UserId);

                // Handed the items but not the source that loaded them: paging is by position,
                // so the cursor has to start past what was handed over or the first page
                // repeats it. Counted before the item below may be inserted, which is the
                // one case where the playlist holds something the source never yielded.
                source.Skip(items.Count);
            }

            var index = items.FindIndex(x => audio.AreTheSame(x));
            if (index >= 0)
            {
                // Play the instance from the list rather than the one built here, so that
                // CurrentItem is the object the list holds.
                item = items[index] as PlaybackItemProfileAudio ?? item;
                items[index] = item;
            }
            else
            {
                items.Insert(0, item);
            }

            // Nothing was handed over, so where this audio sits in the profile is unknown
            // and the first page has to settle it.
            _provisional = loaded == null;

            _items = items;
            _source = source;
            _sessionId = audio.ClientService.SessionId;
            _userId = audio.UserId;
            _chatId = 0;
            _topic = null;

            SetSource(null, item);

            await LoadMoreAsync(true);
        }

        private void Dispose(PlaybackPlaylistType type)
        {
            if (_player != null)
            {
                //_mediaPlayer.CommandManager.IsEnabled = false;

                if (type == PlaybackPlaylistType.None)
                {
                    RunOnTransport(() =>
                    {
                        _transport.ButtonPressed -= Transport_ButtonPressed;
                        _transport.AutoRepeatModeChangeRequested -= Transport_AutoRepeatModeChangeRequested;

                        UpdateManager.Unsubscribe(this, ref _albumCoverToken);
                    });

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

            // Dropping the source is what makes a page still in flight for the playlist being
            // torn down land on nothing.
            _items = null;
            _source = null;
            _provisional = false;
            _type = type;

            UnsubscribeUpdates();
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

            /// <summary>
            /// Carried too, so the interrupted playlist can still grow once it comes back.
            /// </summary>
            public PlaybackSource Source { get; }

            public PlaybackPreviousState(PlaybackService service, AsyncMediaPlayer player)
            {
                Items = service._items.ToList();
                CurrentItem = service.CurrentItem;
                Position = player.Position;
                State = service.PlaybackState;
                Source = service._source;
            }
        }

        // Every player owns a whole libvlc instance and its audio output, so two of them
        // competing is not a benign duplicate. Play(XamlRoot, ...) reaches here without
        // holding the lock Run takes, and a chat opened in a second window has its own UI
        // thread, so two windows starting playback at once could each build one and leave
        // the loser running with nothing left to Close it.
        private AsyncMediaPlayer Create()
        {
            lock (_mediaPlayerLock)
            {
                if (_player == null)
                {
                    // TODO: currently music player doesn't have a toggle for mute/unmute
                    var options = new AsyncMediaPlayerOptions
                    {
                        CreateSwapChain = true,
                        Mute = false, //AppSettings.VolumeMuted,
                        Volume = AppSettings.VolumeLevel,
                        Debug = AppSettings.VerbosityLevel >= 4,
                    };

                    _player = new AsyncMediaPlayer(options);
                    //_mediaPlayer.PlaybackSession.PlaybackStateChanged += OnPlaybackStateChanged;
                    _player.PositionChanged += OnTimeChanged;
                    _player.DurationChanged += OnLengthChanged;
                    _player.StateChanged += OnStateChanged;
                    _player.Buffering += OnBuffering;
                    //_mediaPlayer.CommandManager.IsEnabled = false;

                    RunOnTransport(() =>
                    {
                        _transport.ButtonPressed += Transport_ButtonPressed;
                        _transport.AutoRepeatModeChangeRequested += Transport_AutoRepeatModeChangeRequested;
                    });
                }

                return _player;
            }
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

        /// <summary>
        /// Album cover to show in the system media transport controls; null for anything
        /// that isn't music.
        /// </summary>
        public Thumbnail AlbumCover { get; protected set; }

        /// <summary>
        /// The audio this item plays, or null for a voice or video note. Needed to move an
        /// item into a profile audio playlist, which holds audio rather than messages.
        /// </summary>
        /// <remarks>
        /// Not called Audio: PlaybackItemProfileAudio already has one of those, and it is an
        /// AudioWithOwner rather than the audio itself.
        /// </remarks>
        public Audio Track { get; protected set; }

        public int Duration { get; protected set; }

        public bool CanChangePlaybackRate { get; protected set; }

        // The transport controls render the thumbnail at roughly this size, so a larger
        // variant would be downloaded for nothing.
        private const int AlbumCoverSize = 300;

        /// <summary>
        /// The cover embedded in the audio file if the sender provided one, otherwise the
        /// external variant closest to what the transport controls actually display.
        /// </summary>
        protected static Thumbnail SelectAlbumCover(Thumbnail embedded, Vector<Thumbnail> external)
        {
            if (embedded != null || external == null)
            {
                return embedded;
            }

            Thumbnail result = null;
            var resultSize = 0;

            for (int i = 0; i < external.Count; i++)
            {
                var cover = external[i];

                // Everything else a Thumbnail can be (tgs, webm, mpeg4) is not something the
                // transport controls know how to decode.
                if (cover.Format is not ThumbnailFormatJpeg and not ThumbnailFormatPng)
                {
                    continue;
                }

                var size = Math.Max(cover.Width, cover.Height);

                // Smallest variant that still covers the target, or the largest one when none
                // of them reaches it.
                if (result == null || (resultSize < AlbumCoverSize ? size > resultSize : size >= AlbumCoverSize && size < resultSize))
                {
                    result = cover;
                    resultSize = size;
                }
            }

            return result;
        }

        public abstract InputMessageContent ToInputMessage();
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
                AlbumCover = SelectAlbumCover(audio.Audio.AlbumCoverThumbnail, audio.Audio.ExternalAlbumCovers);
                Track = audio.Audio;

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
                    AlbumCover = SelectAlbumCover(previewAudio.Audio.AlbumCoverThumbnail, previewAudio.Audio.ExternalAlbumCovers);
                    Track = previewAudio.Audio;

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
            else if (message.Content is MessageRichMessage richMessage)
            {
                var block = PageBlockHelper.FindFirstMedia(richMessage.Message.Blocks, PageBlockMediaKind.Audible);
                if (block is PageBlockAudio blockAudio)
                {
                    Document = blockAudio.Audio.AudioValue;
                    Duration = blockAudio.Audio.Duration;
                    CanChangePlaybackRate = blockAudio.Audio.Duration >= 10 * 60;
                    AlbumCover = SelectAlbumCover(blockAudio.Audio.AlbumCoverThumbnail, blockAudio.Audio.ExternalAlbumCovers);
                    Track = blockAudio.Audio;

                    if (string.IsNullOrEmpty(blockAudio.Audio.Title))
                    {
                        Title = blockAudio.Audio.FileName;
                        Performer = string.Empty;
                    }
                    else
                    {
                        Title = blockAudio.Audio.Title;
                        Performer = blockAudio.Audio.Performer;
                    }
                }
                else if (block is PageBlockVoiceNote blockVoiceNote)
                {
                    Document = blockVoiceNote.VoiceNote.Voice;
                    Duration = blockVoiceNote.VoiceNote.Duration;
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

        public override InputMessageContent ToInputMessage()
        {
            if (Message.Content is MessageAudio messageAudio)
            {
                return new InputMessageAudio(new InputAudio(new InputFileId(messageAudio.Audio.AudioValue.Id), messageAudio.Audio.AlbumCoverThumbnail.ToInput(), messageAudio.Audio.Duration, messageAudio.Audio.Title, messageAudio.Audio.Performer), null);
            }

            return null;
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
            AlbumCover = SelectAlbumCover(audio.AlbumCoverThumbnail, audio.ExternalAlbumCovers);
            Track = audio.Value;

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

        public override InputMessageContent ToInputMessage()
        {
            return new InputMessageAudio(new InputAudio(new InputFileId(Audio.AudioValue.Id), Audio.AlbumCoverThumbnail.ToInput(), Audio.Duration, Audio.Title, Audio.Performer), null);
        }
    }
}
