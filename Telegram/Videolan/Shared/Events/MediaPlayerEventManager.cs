using System;
using LibVLCSharp.Shared.Helpers;

namespace LibVLCSharp.Shared
{
    internal class MediaPlayerEventManager : EventManager
    {
        EventHandler<MediaPlayerPositionChangedEventArgs>? _mediaPlayerPositionChanged;
        EventHandler<MediaPlayerMediaChangedEventArgs>? _mediaPlayerMediaChanged;
        EventHandler<EventArgs>? _mediaPlayerNothingSpecial;
        EventHandler<EventArgs>? _mediaPlayerOpening;
        EventHandler<MediaPlayerBufferingEventArgs>? _mediaPlayerBuffering;
        EventHandler<EventArgs>? _mediaPlayerPlaying;
        EventHandler<EventArgs>? _mediaPlayerPaused;
        EventHandler<EventArgs>? _mediaPlayerStopped;
        EventHandler<EventArgs>? _mediaPlayerForward;
        EventHandler<EventArgs>? _mediaPlayerBackward;
        EventHandler<EventArgs>? _mediaPlayerEndReached;
        EventHandler<EventArgs>? _mediaPlayerEncounteredError;
        EventHandler<MediaPlayerTimeChangedEventArgs>? _mediaPlayerTimeChanged;
        EventHandler<MediaPlayerSeekableChangedEventArgs>? _mediaPlayerSeekableChanged;
        EventHandler<MediaPlayerPausableChangedEventArgs>? _mediaPlayerPausableChanged;
        EventHandler<MediaPlayerTitleChangedEventArgs>? _mediaPlayerTitleChanged;
        EventHandler<MediaPlayerChapterChangedEventArgs>? _mediaPlayerChapterChanged; //vlc 3
        EventHandler<MediaPlayerSnapshotTakenEventArgs>? _mediaPlayerSnapshotTaken;
        EventHandler<MediaPlayerLengthChangedEventArgs>? _mediaPlayerLengthChanged;
        EventHandler<MediaPlayerVoutEventArgs>? _mediaPlayerVout;
        EventHandler<MediaPlayerScrambledChangedEventArgs>? _mediaPlayerScrambledChanged;
        EventHandler<MediaPlayerESAddedEventArgs>? _mediaPlayerESAdded; // vlc 3
        EventHandler<MediaPlayerESDeletedEventArgs>? _mediaPlayerESDeleted; // vlc 3
        EventHandler<MediaPlayerESSelectedEventArgs>? _mediaPlayerESSelected; // vlc 3
        EventHandler<MediaPlayerAudioDeviceEventArgs>? _mediaPlayerAudioDevice; // vlc 3
        EventHandler<EventArgs>? _mediaPlayerCorked; // vlc 2.2
        EventHandler<EventArgs>? _mediaPlayerUncorked; // vlc 2.2
        EventHandler<EventArgs>? _mediaPlayerMuted; // vlc 2.2
        EventHandler<EventArgs>? _mediaPlayerUnmuted; // vlc 2.2
        EventHandler<MediaPlayerVolumeChangedEventArgs>? _mediaPlayerVolumeChanged; // vlc 2.2
        public MediaPlayerEventManager(IntPtr ptr) : base(ptr)
        {
        }

        protected internal override void AttachEvent<T>(EventType eventType, EventHandler<T> eventHandler)
        {
            switch (eventType)
            {
                case EventType.MediaPlayerPositionChanged:
                    _mediaPlayerPositionChanged += eventHandler as EventHandler<MediaPlayerPositionChangedEventArgs>;
                    Attach(eventType, OnPositionChanged);
                    break;
                case EventType.MediaPlayerMediaChanged:
                    _mediaPlayerMediaChanged += eventHandler as EventHandler<MediaPlayerMediaChangedEventArgs>;
                    Attach(eventType, OnMediaChanged);
                    break;
                case EventType.MediaPlayerNothingSpecial:
                    _mediaPlayerNothingSpecial += eventHandler as EventHandler<EventArgs>;
                    Attach(eventType, OnNothingSpecial);
                    break;
                case EventType.MediaPlayerOpening:
                    _mediaPlayerOpening += eventHandler as EventHandler<EventArgs>;
                    Attach(eventType, OnOpening);
                    break;
                case EventType.MediaPlayerBuffering:
                    _mediaPlayerBuffering += eventHandler as EventHandler<MediaPlayerBufferingEventArgs>;
                    Attach(eventType, OnBuffering);
                    break;
                case EventType.MediaPlayerPlaying:
                    _mediaPlayerPlaying += eventHandler as EventHandler<EventArgs>;
                    Attach(eventType, OnPlaying);
                    break;
                case EventType.MediaPlayerPaused:
                    _mediaPlayerPaused += eventHandler as EventHandler<EventArgs>;
                    Attach(eventType, OnPaused);
                    break;
                case EventType.MediaPlayerStopped:
                    _mediaPlayerStopped += eventHandler as EventHandler<EventArgs>;
                    Attach(eventType, OnStopped);
                    break;
                case EventType.MediaPlayerForward:
                    _mediaPlayerForward += eventHandler as EventHandler<EventArgs>;
                    Attach(eventType, OnForward);
                    break;
                case EventType.MediaPlayerBackward:
                    _mediaPlayerBackward += eventHandler as EventHandler<EventArgs>;
                    Attach(eventType, OnBackward);
                    break;
                case EventType.MediaPlayerEndReached:
                    _mediaPlayerEndReached += eventHandler as EventHandler<EventArgs>;
                    Attach(eventType, OnEndReached);
                    break;
                case EventType.MediaPlayerEncounteredError:
                    _mediaPlayerEncounteredError += eventHandler as EventHandler<EventArgs>;
                    Attach(eventType, OnEncounteredError);
                    break;
                case EventType.MediaPlayerTimeChanged:
                    _mediaPlayerTimeChanged += eventHandler as EventHandler<MediaPlayerTimeChangedEventArgs>;
                    Attach(eventType, OnTimeChanged);
                    break;
                case EventType.MediaPlayerSeekableChanged:
                    _mediaPlayerSeekableChanged += eventHandler as EventHandler<MediaPlayerSeekableChangedEventArgs>;
                    Attach(eventType, OnSeekableChanged);
                    break;
                case EventType.MediaPlayerPausableChanged:
                    _mediaPlayerPausableChanged += eventHandler as EventHandler<MediaPlayerPausableChangedEventArgs>;
                    Attach(eventType, OnPausableChanged);
                    break;
                case EventType.MediaPlayerTitleChanged:
                    _mediaPlayerTitleChanged += eventHandler as EventHandler<MediaPlayerTitleChangedEventArgs>;
                    Attach(eventType, OnTitleChanged);
                    break;
                case EventType.MediaPlayerChapterChanged:
                    _mediaPlayerChapterChanged += eventHandler as EventHandler<MediaPlayerChapterChangedEventArgs>;
                    Attach(eventType, OnChapterChanged);
                    break;
                case EventType.MediaPlayerSnapshotTaken:
                    _mediaPlayerSnapshotTaken += eventHandler as EventHandler<MediaPlayerSnapshotTakenEventArgs>;
                    Attach(eventType, OnSnapshotTaken);
                    break;
                case EventType.MediaPlayerLengthChanged:
                    _mediaPlayerLengthChanged += eventHandler as EventHandler<MediaPlayerLengthChangedEventArgs>;
                    Attach(eventType, OnLengthChanged);
                    break;
                case EventType.MediaPlayerVout:
                    _mediaPlayerVout += eventHandler as EventHandler<MediaPlayerVoutEventArgs>;
                    Attach(eventType, OnVout);
                    break;
                case EventType.MediaPlayerScrambledChanged:
                    _mediaPlayerScrambledChanged += eventHandler as EventHandler<MediaPlayerScrambledChangedEventArgs>;
                    Attach(eventType, OnScrambledChanged);
                    break;
                case EventType.MediaPlayerESAdded:
                    _mediaPlayerESAdded += eventHandler as EventHandler<MediaPlayerESAddedEventArgs>;
                    Attach(eventType, OnESAdded);
                    break;
                case EventType.MediaPlayerESDeleted:
                    _mediaPlayerESDeleted += eventHandler as EventHandler<MediaPlayerESDeletedEventArgs>;
                    Attach(eventType, OnESDeleted);
                    break;
                case EventType.MediaPlayerESSelected:
                    _mediaPlayerESSelected += eventHandler as EventHandler<MediaPlayerESSelectedEventArgs>;
                    Attach(eventType, OnESSelected);
                    break;
                case EventType.MediaPlayerAudioDevice:
                    _mediaPlayerAudioDevice += eventHandler as EventHandler<MediaPlayerAudioDeviceEventArgs>;
                    Attach(eventType, OnAudioDevice);
                    break;
                case EventType.MediaPlayerCorked:
                    _mediaPlayerCorked += eventHandler as EventHandler<EventArgs>;
                    Attach(eventType, OnCorked);
                    break;
                case EventType.MediaPlayerUncorked:
                    _mediaPlayerUncorked += eventHandler as EventHandler<EventArgs>;
                    Attach(eventType, OnUncorked);
                    break;
                case EventType.MediaPlayerMuted:
                    _mediaPlayerMuted += eventHandler as EventHandler<EventArgs>;
                    Attach(eventType, OnMuted);
                    break;
                case EventType.MediaPlayerUnmuted:
                    _mediaPlayerUnmuted += eventHandler as EventHandler<EventArgs>;
                    Attach(eventType, OnUnmuted);
                    break;
                case EventType.MediaPlayerAudioVolume:
                    _mediaPlayerVolumeChanged += eventHandler as EventHandler<MediaPlayerVolumeChangedEventArgs>;
                    Attach(eventType, OnVolumeChanged);
                    break;
                default:
                    OnEventUnhandled(this, eventType);
                    break;
            }
        }

        protected internal override void DetachEvent<T>(EventType eventType, EventHandler<T> eventHandler)
        {
            switch (eventType)
            {
                case EventType.MediaPlayerPositionChanged:
                    _mediaPlayerPositionChanged -= eventHandler as EventHandler<MediaPlayerPositionChangedEventArgs>;
                    Detach(eventType);
                    break;
                case EventType.MediaPlayerMediaChanged:
                    _mediaPlayerMediaChanged -= eventHandler as EventHandler<MediaPlayerMediaChangedEventArgs>;
                    Detach(eventType);
                    break;
                case EventType.MediaPlayerNothingSpecial:
                    _mediaPlayerNothingSpecial -= eventHandler as EventHandler<EventArgs>;
                    Detach(eventType);
                    break;
                case EventType.MediaPlayerOpening:
                    _mediaPlayerOpening -= eventHandler as EventHandler<EventArgs>;
                    Detach(eventType);
                    break;
                case EventType.MediaPlayerBuffering:
                    _mediaPlayerBuffering -= eventHandler as EventHandler<MediaPlayerBufferingEventArgs>;
                    Detach(eventType);
                    break;
                case EventType.MediaPlayerPlaying:
                    _mediaPlayerPlaying -= eventHandler as EventHandler<EventArgs>;
                    Detach(eventType);
                    break;
                case EventType.MediaPlayerPaused:
                    _mediaPlayerPaused -= eventHandler as EventHandler<EventArgs>;
                    Detach(eventType);
                    break;
                case EventType.MediaPlayerStopped:
                    _mediaPlayerStopped -= eventHandler as EventHandler<EventArgs>;
                    Detach(eventType);
                    break;
                case EventType.MediaPlayerForward:
                    _mediaPlayerForward -= eventHandler as EventHandler<EventArgs>;
                    Detach(eventType);
                    break;
                case EventType.MediaPlayerBackward:
                    _mediaPlayerBackward -= eventHandler as EventHandler<EventArgs>;
                    Detach(eventType);
                    break;
                case EventType.MediaPlayerEndReached:
                    _mediaPlayerEndReached -= eventHandler as EventHandler<EventArgs>;
                    Detach(eventType);
                    break;
                case EventType.MediaPlayerEncounteredError:
                    _mediaPlayerEncounteredError -= eventHandler as EventHandler<EventArgs>;
                    Detach(eventType);
                    break;
                case EventType.MediaPlayerTimeChanged:
                    _mediaPlayerTimeChanged -= eventHandler as EventHandler<MediaPlayerTimeChangedEventArgs>;
                    Detach(eventType);
                    break;
                case EventType.MediaPlayerSeekableChanged:
                    _mediaPlayerSeekableChanged -= eventHandler as EventHandler<MediaPlayerSeekableChangedEventArgs>;
                    Detach(eventType);
                    break;
                case EventType.MediaPlayerPausableChanged:
                    _mediaPlayerPausableChanged -= eventHandler as EventHandler<MediaPlayerPausableChangedEventArgs>;
                    Detach(eventType);
                    break;
                case EventType.MediaPlayerTitleChanged:
                    _mediaPlayerTitleChanged -= eventHandler as EventHandler<MediaPlayerTitleChangedEventArgs>;
                    Detach(eventType);
                    break;
                case EventType.MediaPlayerChapterChanged:
                    _mediaPlayerChapterChanged -= eventHandler as EventHandler<MediaPlayerChapterChangedEventArgs>;
                    Detach(eventType);
                    break;
                case EventType.MediaPlayerSnapshotTaken:
                    _mediaPlayerSnapshotTaken -= eventHandler as EventHandler<MediaPlayerSnapshotTakenEventArgs>;
                    Detach(eventType);
                    break;
                case EventType.MediaPlayerLengthChanged:
                    _mediaPlayerLengthChanged -= eventHandler as EventHandler<MediaPlayerLengthChangedEventArgs>;
                    Detach(eventType);
                    break;
                case EventType.MediaPlayerVout:
                    _mediaPlayerVout -= eventHandler as EventHandler<MediaPlayerVoutEventArgs>;
                    Detach(eventType);
                    break;
                case EventType.MediaPlayerScrambledChanged:
                    _mediaPlayerScrambledChanged -= eventHandler as EventHandler<MediaPlayerScrambledChangedEventArgs>;
                    Detach(eventType);
                    break;
                case EventType.MediaPlayerESAdded:
                    _mediaPlayerESAdded -= eventHandler as EventHandler<MediaPlayerESAddedEventArgs>;
                    Detach(eventType);
                    break;
                case EventType.MediaPlayerESDeleted:
                    _mediaPlayerESDeleted -= eventHandler as EventHandler<MediaPlayerESDeletedEventArgs>;
                    Detach(eventType);
                    break;
                case EventType.MediaPlayerESSelected:
                    _mediaPlayerESSelected -= eventHandler as EventHandler<MediaPlayerESSelectedEventArgs>;
                    Detach(eventType);
                    break;
                case EventType.MediaPlayerAudioDevice:
                    _mediaPlayerAudioDevice -= eventHandler as EventHandler<MediaPlayerAudioDeviceEventArgs>;
                    Detach(eventType);
                    break;
                case EventType.MediaPlayerCorked:
                    _mediaPlayerCorked -= eventHandler as EventHandler<EventArgs>;
                    Detach(eventType);
                    break;
                case EventType.MediaPlayerUncorked:
                    _mediaPlayerUncorked -= eventHandler as EventHandler<EventArgs>;
                    Detach(eventType);
                    break;
                case EventType.MediaPlayerMuted:
                    _mediaPlayerMuted -= eventHandler as EventHandler<EventArgs>;
                    Detach(eventType);
                    break;
                case EventType.MediaPlayerUnmuted:
                    _mediaPlayerUnmuted -= eventHandler as EventHandler<EventArgs>;
                    Detach(eventType);
                    break;
                case EventType.MediaPlayerAudioVolume:
                    _mediaPlayerVolumeChanged -= eventHandler as EventHandler<MediaPlayerVolumeChangedEventArgs>;
                    Detach(eventType);
                    break;
                default:
                    OnEventUnhandled(this, eventType);
                    break;
            }
        }

        void OnMediaChanged(IntPtr ptr)
        {
            _mediaPlayerMediaChanged?.Invoke(this,
                new MediaPlayerMediaChangedEventArgs(RetrieveEvent(ptr).Union.MediaPlayerMediaChanged.NewMedia));
        }

        void OnNothingSpecial(IntPtr ptr)
        {
            _mediaPlayerNothingSpecial?.Invoke(this, EventArgs.Empty);
        }

        void OnOpening(IntPtr ptr)
        {
            _mediaPlayerOpening?.Invoke(this, EventArgs.Empty);
        }

        void OnBuffering(IntPtr ptr)
        {
            _mediaPlayerBuffering?.Invoke(this,
                new MediaPlayerBufferingEventArgs(RetrieveEvent(ptr).Union.MediaPlayerBuffering.NewCache));
        }

        void OnPlaying(IntPtr ptr)
        {
            _mediaPlayerPlaying?.Invoke(this, EventArgs.Empty);
        }

        void OnPaused(IntPtr ptr)
        {
            _mediaPlayerPaused?.Invoke(this, EventArgs.Empty);
        }

        void OnStopped(IntPtr ptr)
        {
            _mediaPlayerStopped?.Invoke(this, EventArgs.Empty);
        }

        void OnForward(IntPtr ptr)
        {
            _mediaPlayerForward?.Invoke(this, EventArgs.Empty);
        }

        void OnBackward(IntPtr ptr)
        {
            _mediaPlayerBackward?.Invoke(this, EventArgs.Empty);
        }

        void OnEndReached(IntPtr ptr)
        {
            _mediaPlayerEndReached?.Invoke(this, EventArgs.Empty);
        }

        void OnEncounteredError(IntPtr ptr)
        {
            _mediaPlayerEncounteredError?.Invoke(this, EventArgs.Empty);
        }

        void OnTimeChanged(IntPtr ptr)
        {
            _mediaPlayerTimeChanged?.Invoke(this,
                new MediaPlayerTimeChangedEventArgs(RetrieveEvent(ptr).Union.MediaPlayerTimeChanged.NewTime));
        }

        void OnPositionChanged(IntPtr ptr)
        {
            _mediaPlayerPositionChanged?.Invoke(this,
                new MediaPlayerPositionChangedEventArgs(RetrieveEvent(ptr).Union.MediaPlayerPositionChanged.NewPosition));
        }

        void OnSeekableChanged(IntPtr ptr)
        {
            _mediaPlayerSeekableChanged?.Invoke(this,
                new MediaPlayerSeekableChangedEventArgs(RetrieveEvent(ptr).Union.MediaPlayerSeekableChanged.NewSeekable));
        }

        void OnPausableChanged(IntPtr ptr)
        {
            _mediaPlayerPausableChanged?.Invoke(this,
                new MediaPlayerPausableChangedEventArgs(RetrieveEvent(ptr).Union.MediaPlayerPausableChanged.NewPausable));
        }

        void OnTitleChanged(IntPtr ptr)
        {
            _mediaPlayerTitleChanged?.Invoke(this,
                new MediaPlayerTitleChangedEventArgs(RetrieveEvent(ptr).Union.MediaPlayerTitleChanged.NewTitle));
        }

        void OnChapterChanged(IntPtr ptr)
        {
            _mediaPlayerChapterChanged?.Invoke(this,
                new MediaPlayerChapterChangedEventArgs(RetrieveEvent(ptr).Union.MediaPlayerChapterChanged.NewChapter));
        }

        void OnSnapshotTaken(IntPtr ptr)
        {
            var filenamePtr = RetrieveEvent(ptr).Union.MediaPlayerSnapshotTaken.Filename;
            _mediaPlayerSnapshotTaken?.Invoke(this, new MediaPlayerSnapshotTakenEventArgs(filenamePtr.FromUtf8()!));
        }

        void OnLengthChanged(IntPtr ptr)
        {
            _mediaPlayerLengthChanged?.Invoke(this,
                new MediaPlayerLengthChangedEventArgs(RetrieveEvent(ptr).Union.MediaPlayerLengthChanged.NewLength));
        }

        void OnVout(IntPtr ptr)
        {
            _mediaPlayerVout?.Invoke(this,
                new MediaPlayerVoutEventArgs(RetrieveEvent(ptr).Union.MediaPlayerVoutChanged.NewCount));
        }

        void OnScrambledChanged(IntPtr ptr)
        {
            _mediaPlayerScrambledChanged?.Invoke(this,
                new MediaPlayerScrambledChangedEventArgs(RetrieveEvent(ptr).Union.MediaPlayerScrambledChanged.NewScrambled));
        }

        LibVLCEvent.EsChanged GetEsChanged(IntPtr ptr)
        {
            return RetrieveEvent(ptr).Union.EsChanged;
        }

        void OnESAdded(IntPtr ptr)
        {
            var esChanged = GetEsChanged(ptr);
            _mediaPlayerESAdded?.Invoke(this, new MediaPlayerESAddedEventArgs(esChanged.Id, esChanged.Type));
        }

        void OnESDeleted(IntPtr ptr)
        {
            var esChanged = GetEsChanged(ptr);
            _mediaPlayerESDeleted?.Invoke(this, new MediaPlayerESDeletedEventArgs(esChanged.Id, esChanged.Type));
        }

        void OnESSelected(IntPtr ptr)
        {
            var esChanged = GetEsChanged(ptr);
            _mediaPlayerESSelected?.Invoke(this, new MediaPlayerESSelectedEventArgs(esChanged.Id, esChanged.Type));
        }

        void OnAudioDevice(IntPtr ptr)
        {
            var deviceNamePtr = RetrieveEvent(ptr).Union.AudioDeviceChanged.Device;
            _mediaPlayerAudioDevice?.Invoke(this, new MediaPlayerAudioDeviceEventArgs(deviceNamePtr.FromUtf8()!));
        }

        void OnCorked(IntPtr ptr)
        {
            _mediaPlayerCorked?.Invoke(this, EventArgs.Empty);
        }

        void OnUncorked(IntPtr ptr)
        {
            _mediaPlayerUncorked?.Invoke(this, EventArgs.Empty);
        }

        void OnMuted(IntPtr ptr)
        {
            _mediaPlayerMuted?.Invoke(this, EventArgs.Empty);
        }

        void OnUnmuted(IntPtr ptr)
        {
            _mediaPlayerUnmuted?.Invoke(this, EventArgs.Empty);
        }

        void OnVolumeChanged(IntPtr ptr)
        {
            _mediaPlayerVolumeChanged?.Invoke(this,
                new MediaPlayerVolumeChangedEventArgs(RetrieveEvent(ptr).Union.MediaPlayerVolumeChanged.Volume));
        }

    }
}
