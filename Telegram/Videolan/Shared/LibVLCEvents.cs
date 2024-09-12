using System;
using System.Runtime.InteropServices;

namespace LibVLCSharp.Shared
{
    /// <summary>LibVLCEvent types</summary>
    internal enum EventType
    {
        MediaMetaChanged = 0,
        MediaSubItemAdded = 1,
        MediaDurationChanged = 2,
        MediaParsedChanged = 3,
        MediaFreed = 4,
        MediaStateChanged = 5,
        MediaSubItemTreeAdded = 6,
        MediaPlayerMediaChanged = 256,
        MediaPlayerNothingSpecial = 257,
        MediaPlayerOpening = 258,
        MediaPlayerBuffering = 259,
        MediaPlayerPlaying = 260,
        MediaPlayerPaused = 261,
        MediaPlayerStopped = 262,
        MediaPlayerForward = 263,
        MediaPlayerBackward = 264,
        MediaPlayerEndReached = 265,
        MediaPlayerEncounteredError = 266,
        MediaPlayerTimeChanged = 267,
        MediaPlayerPositionChanged = 268,
        MediaPlayerSeekableChanged = 269,
        MediaPlayerPausableChanged = 270,
        MediaPlayerTitleChanged = 271,
        MediaPlayerSnapshotTaken = 272,
        MediaPlayerLengthChanged = 273,
        MediaPlayerVout = 274,
        MediaPlayerScrambledChanged = 275,
        MediaPlayerESAdded = 276,
        MediaPlayerESDeleted = 277,
        MediaPlayerESSelected = 278,
        MediaPlayerCorked = 279,
        MediaPlayerUncorked = 280,
        MediaPlayerMuted = 281,
        MediaPlayerUnmuted = 282,
        MediaPlayerAudioVolume = 283,
        MediaPlayerAudioDevice = 284,
        MediaPlayerChapterChanged = 285,
        MediaListItemAdded = 512,
        MediaListWillAddItem = 513,
        MediaListItemDeleted = 514,
        MediaListWillDeleteItem = 515,
        MediaListEndReached = 516,
        MediaListViewItemAdded = 768,
        MediaListViewWillAddItem = 769,
        MediaListViewItemDeleted = 770,
        MediaListViewWillDeleteItem = 771,
        MediaListPlayerPlayed = 1024,
        MediaListPlayerNextItemSet = 1025,
        MediaListPlayerStopped = 1026,

        /// <remarks>
        /// <para>Useless event, it will be triggered only when calling</para>
        /// <para>libvlc_media_discoverer_start()</para>
        /// </remarks>
        MediaDiscovererStarted = 1280,

        /// <remarks>
        /// <para>Useless event, it will be triggered only when calling</para>
        /// <para>libvlc_media_discoverer_stop()</para>
        /// </remarks>
        MediaDiscovererStopped = 1281,

        /// <remarks>
        /// <para>Useless event, it will be triggered only when calling</para>
        /// <para>libvlc_media_discoverer_stop()</para>
        /// </remarks>
        RendererDiscovererItemAdded = 1282,

        /// <remarks>
        /// <para>Useless event, it will be triggered only when calling</para>
        /// <para>libvlc_media_discoverer_stop()</para>
        /// </remarks>
        RendererDiscovererItemDeleted = 1283,

        /// <remarks>
        /// <para>Useless event, it will be triggered only when calling</para>
        /// <para>libvlc_media_discoverer_stop()</para>
        /// </remarks>
        VlmMediaAdded = 1536,

        /// <remarks>
        /// <para>Useless event, it will be triggered only when calling</para>
        /// <para>libvlc_media_discoverer_stop()</para>
        /// </remarks>
        VlmMediaRemoved = 1537,

        /// <remarks>
        /// <para>Useless event, it will be triggered only when calling</para>
        /// <para>libvlc_media_discoverer_stop()</para>
        /// </remarks>
        VlmMediaChanged = 1538,

        /// <remarks>
        /// <para>Useless event, it will be triggered only when calling</para>
        /// <para>libvlc_media_discoverer_stop()</para>
        /// </remarks>
        VlmMediaInstanceStarted = 1539,

        /// <remarks>
        /// <para>Useless event, it will be triggered only when calling</para>
        /// <para>libvlc_media_discoverer_stop()</para>
        /// </remarks>
        VlmMediaInstanceStopped = 1540,

        /// <remarks>
        /// <para>Useless event, it will be triggered only when calling</para>
        /// <para>libvlc_media_discoverer_stop()</para>
        /// </remarks>
        VlmMediaInstanceStatusInit = 1541,

        /// <remarks>
        /// <para>Useless event, it will be triggered only when calling</para>
        /// <para>libvlc_media_discoverer_stop()</para>
        /// </remarks>
        VlmMediaInstanceStatusOpening = 1542,

        /// <remarks>
        /// <para>Useless event, it will be triggered only when calling</para>
        /// <para>libvlc_media_discoverer_stop()</para>
        /// </remarks>
        VlmMediaInstanceStatusPlaying = 1543,

        /// <remarks>
        /// <para>Useless event, it will be triggered only when calling</para>
        /// <para>libvlc_media_discoverer_stop()</para>
        /// </remarks>
        VlmMediaInstanceStatusPause = 1544,

        /// <remarks>
        /// <para>Useless event, it will be triggered only when calling</para>
        /// <para>libvlc_media_discoverer_stop()</para>
        /// </remarks>
        VlmMediaInstanceStatusEnd = 1545,

        /// <remarks>
        /// <para>Useless event, it will be triggered only when calling</para>
        /// <para>libvlc_media_discoverer_stop()</para>
        /// </remarks>
        VlmMediaInstanceStatusError = 1546
    }

    /// <summary>Renderer item</summary>
    /// <remarks>
    /// <para>This struct is passed by a</para>
    /// <para>or deleted.</para>
    /// <para>An item is valid until the</para>
    /// <para>is called with the same pointer.</para>
    /// <para>libvlc_renderer_discoverer_event_manager()</para>
    /// </remarks>
    /// <summary>A LibVLC event</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct LibVLCEvent
    {
        internal readonly EventType Type;

        internal readonly IntPtr Sender;

        internal readonly EventUnion Union;

        [StructLayout(LayoutKind.Explicit)]
        internal readonly struct EventUnion
        {
            // media
            [FieldOffset(0)]
            internal readonly MediaMetaChanged MediaMetaChanged;
            [FieldOffset(0)]
            internal readonly MediaSubItemAdded MediaSubItemAdded;
            [FieldOffset(0)]
            internal readonly MediaDurationChanged MediaDurationChanged;
            [FieldOffset(0)]
            internal readonly MediaParsedChanged MediaParsedChanged;
            [FieldOffset(0)]
            internal readonly MediaFreed MediaFreed;
            [FieldOffset(0)]
            internal readonly MediaStateChanged MediaStateChanged;
            [FieldOffset(0)]
            internal readonly MediaSubItemTreeAdded MediaSubItemTreeAdded;

            // mediaplayer
            [FieldOffset(0)]
            internal readonly MediaPlayerBuffering MediaPlayerBuffering;
            [FieldOffset(0)]
            internal readonly MediaPlayerChapterChanged MediaPlayerChapterChanged;
            [FieldOffset(0)]
            internal readonly MediaPlayerPositionChanged MediaPlayerPositionChanged;
            [FieldOffset(0)]
            internal readonly MediaPlayerTimeChanged MediaPlayerTimeChanged;
            [FieldOffset(0)]
            internal readonly MediaPlayerTitleChanged MediaPlayerTitleChanged;
            [FieldOffset(0)]
            internal readonly MediaPlayerSeekableChanged MediaPlayerSeekableChanged;
            [FieldOffset(0)]
            internal readonly MediaPlayerPausableChanged MediaPlayerPausableChanged;
            [FieldOffset(0)]
            internal readonly MediaPlayerScrambledChanged MediaPlayerScrambledChanged;
            [FieldOffset(0)]
            internal readonly MediaPlayerVoutChanged MediaPlayerVoutChanged;

            // medialist
            [FieldOffset(0)]
            internal readonly MediaListItemAdded MediaListItemAdded;
            [FieldOffset(0)]
            internal readonly MediaListWillAddItem MediaListWillAddItem;
            [FieldOffset(0)]
            internal readonly MediaListItemDeleted MediaListItemDeleted;
            [FieldOffset(0)]
            internal readonly MediaListWillDeleteItem MediaListWillDeleteItem;
            [FieldOffset(0)]
            internal readonly MediaListPlayerNextItemSet MediaListPlayerNextItemSet;

            // mediaplayer
            [FieldOffset(0)]
            internal readonly MediaPlayerSnapshotTaken MediaPlayerSnapshotTaken;
            [FieldOffset(0)]
            internal readonly MediaPlayerLengthChanged MediaPlayerLengthChanged;
            [FieldOffset(0)]
            internal readonly VlmMediaEvent VlmMediaEvent;
            [FieldOffset(0)]
            internal readonly MediaPlayerMediaChanged MediaPlayerMediaChanged;
            [FieldOffset(0)]
            internal readonly EsChanged EsChanged;
            [FieldOffset(0)]
            internal readonly VolumeChanged MediaPlayerVolumeChanged;
            [FieldOffset(0)]
            internal readonly AudioDeviceChanged AudioDeviceChanged;

            // renderer discoverer
            [FieldOffset(0)]
            internal readonly RendererDiscovererItemAdded RendererDiscovererItemAdded;
            [FieldOffset(0)]
            internal readonly RendererDiscovererItemDeleted RendererDiscovererItemDeleted;
        }

        #region Media
        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct MediaMetaChanged
        {
            internal readonly MetadataType MetaType;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct MediaSubItemAdded
        {
            internal readonly IntPtr NewChild;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct MediaDurationChanged
        {
            internal readonly long NewDuration;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct MediaParsedChanged
        {
            internal readonly MediaParsedStatus NewStatus;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct MediaFreed
        {
            internal readonly IntPtr MediaInstance;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct MediaStateChanged
        {
            internal readonly VLCState NewState;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct MediaSubItemTreeAdded
        {
            internal readonly IntPtr MediaInstance;
        }

        #endregion

        #region MediaPlayer 

        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct MediaPlayerBuffering
        {
            internal readonly float NewCache;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct MediaPlayerChapterChanged
        {
            internal readonly int NewChapter;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct MediaPlayerPositionChanged
        {
            internal readonly float NewPosition;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct MediaPlayerTimeChanged
        {
            internal readonly long NewTime;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct MediaPlayerTitleChanged
        {
            internal readonly int NewTitle;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct MediaPlayerSeekableChanged
        {
            internal readonly int NewSeekable;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct MediaPlayerPausableChanged
        {
            internal readonly int NewPausable;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct MediaPlayerScrambledChanged
        {
            internal readonly int NewScrambled;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct MediaPlayerVoutChanged
        {
            internal readonly int NewCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct MediaPlayerSnapshotTaken
        {
            internal readonly IntPtr Filename;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct MediaPlayerLengthChanged
        {
            internal readonly long NewLength;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct EsChanged
        {
            internal readonly TrackType Type;
            internal readonly int Id;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct AudioDeviceChanged
        {
            internal readonly IntPtr Device;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct MediaPlayerMediaChanged
        {
            internal readonly IntPtr NewMedia;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct VolumeChanged
        {
            internal readonly float Volume;
        }

        #endregion

        #region MediaList

        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct MediaListItemAdded
        {
            internal readonly IntPtr MediaInstance;
            internal readonly int Index;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct MediaListWillAddItem
        {
            internal readonly IntPtr MediaInstance;
            internal readonly int Index;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct MediaListItemDeleted
        {
            internal readonly IntPtr MediaInstance;
            internal readonly int Index;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct MediaListWillDeleteItem
        {
            internal readonly IntPtr MediaInstance;
            internal readonly int Index;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct MediaListPlayerNextItemSet
        {
            internal readonly IntPtr MediaInstance;
        }

        #endregion MediaList

        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct VlmMediaEvent
        {
            internal readonly IntPtr MediaName;
            internal readonly IntPtr InstanceName;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct RendererDiscovererItemAdded
        {
            internal readonly IntPtr item;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct RendererDiscovererItemDeleted
        {
            internal readonly IntPtr item;
        }
    }

    #region Media events

    /// <summary>
    /// Media metadata changed
    /// </summary>
    public partial class MediaMetaChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Type of the metadata that changed
        /// </summary>
        public readonly MetadataType MetadataType;

        internal MediaMetaChangedEventArgs(MetadataType metadataType)
        {
            MetadataType = metadataType;
        }
    }

    /// <summary>
    /// Media parsed status changed
    /// </summary>
    public partial class MediaParsedChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The new parsed status
        /// </summary>
        public readonly MediaParsedStatus ParsedStatus;

        internal MediaParsedChangedEventArgs(MediaParsedStatus parsedStatus)
        {
            ParsedStatus = parsedStatus;
        }
    }

    /// <summary>
    /// Media sub item added
    /// </summary>
    public partial class MediaSubItemAddedEventArgs : EventArgs
    {
        /// <summary>
        /// The newly added media subitem
        /// </summary>
        public readonly Media SubItem;

        internal MediaSubItemAddedEventArgs(IntPtr mediaPtr)
        {
            SubItem = new Media(mediaPtr);
        }
    }

    /// <summary>
    /// The duration of the media changed
    /// </summary>
    public partial class MediaDurationChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The new media duration
        /// </summary>
        public readonly long Duration;

        internal MediaDurationChangedEventArgs(long duration)
        {
            Duration = duration;
        }
    }

    /// <summary>
    /// The media has been freed
    /// </summary>
    public partial class MediaFreedEventArgs : EventArgs
    {
        /// <summary>
        /// Freed media
        /// </summary>
        public readonly Media Media;

        internal MediaFreedEventArgs(IntPtr mediaPtr)
        {
            Media = new Media(mediaPtr);
        }
    }

    /// <summary>
    /// The state of the media changed
    /// </summary>
    public partial class MediaStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// New media state
        /// </summary>
        public readonly VLCState State;

        internal MediaStateChangedEventArgs(VLCState state)
        {
            State = state;
        }
    }

    /// <summary>
    /// A media sub item tree has been added
    /// </summary>
    public partial class MediaSubItemTreeAddedEventArgs : EventArgs
    {
        /// <summary>
        /// New media sub item tree
        /// </summary>
        public readonly Media SubItem;

        internal MediaSubItemTreeAddedEventArgs(IntPtr subItemPtr)
        {
            SubItem = new Media(subItemPtr);
        }
    }

    #endregion

    #region MediaPlayer events

    /// <summary>
    /// The mediaplayer's media changed
    /// </summary>
    public partial class MediaPlayerMediaChangedEventArgs : EventArgs
    {
        /// <summary>
        /// New mediaplayer's media
        /// </summary>
        public readonly Media Media;

        internal MediaPlayerMediaChangedEventArgs(IntPtr mediaPtr)
        {
            Media = new Media(mediaPtr);
        }
    }

    /// <summary>
    /// The mediaplayer buffering information
    /// </summary>
    public partial class MediaPlayerBufferingEventArgs : EventArgs
    {
        /// <summary>
        /// Caching information
        /// </summary>
        public readonly float Cache;

        internal MediaPlayerBufferingEventArgs(float cache)
        {
            Cache = cache;
        }
    }

    /// <summary>
    /// The mediaplayer's time changed
    /// </summary>
    public partial class MediaPlayerTimeChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Mediaplayer's current time
        /// </summary>
        public readonly long Time;

        internal MediaPlayerTimeChangedEventArgs(long time)
        {
            Time = time;
        }
    }

    /// <summary>
    /// The mediaplayer's position changed
    /// </summary>
    public partial class MediaPlayerPositionChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Mediaplayer's current position
        /// </summary>
        public readonly float Position;

        internal MediaPlayerPositionChangedEventArgs(float position)
        {
            Position = position;
        }
    }

    /// <summary>
    /// The mediaplayer's seekable status changed
    /// </summary>
    public partial class MediaPlayerSeekableChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The new seekable capability
        /// </summary>
        public readonly int Seekable;

        internal MediaPlayerSeekableChangedEventArgs(int seekable)
        {
            Seekable = seekable;
        }
    }

    /// <summary>
    /// The mediaplayer's pausable status changed
    /// </summary>
    public partial class MediaPlayerPausableChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The new pausable capability
        /// </summary>
        public readonly int Pausable;

        internal MediaPlayerPausableChangedEventArgs(int pausable)
        {
            Pausable = pausable;
        }
    }

    /// <summary>
    /// The mediaplayer's title changed
    /// </summary>
    public partial class MediaPlayerTitleChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The new title
        /// </summary>
        public readonly int Title;

        internal MediaPlayerTitleChangedEventArgs(int title)
        {
            Title = title;
        }
    }

    /// <summary>
    /// The mediaplayer's chapter changed
    /// </summary>
    public partial class MediaPlayerChapterChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The new chapter
        /// </summary>
        public readonly int Chapter;

        internal MediaPlayerChapterChangedEventArgs(int chapter)
        {
            Chapter = chapter;
        }
    }

    /// <summary>
    /// The mediaplayer had a snapshot taken
    /// </summary>
    public partial class MediaPlayerSnapshotTakenEventArgs : EventArgs
    {
        /// <summary>
        /// Filename of the newly taken snapshot
        /// </summary>
        public readonly string Filename;

        internal MediaPlayerSnapshotTakenEventArgs(string filename)
        {
            Filename = filename;
        }
    }

    /// <summary>
    /// The mediaplayer's length changed
    /// </summary>
    public partial class MediaPlayerLengthChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The new mediaplayer length
        /// </summary>
        public readonly long Length;

        internal MediaPlayerLengthChangedEventArgs(long length)
        {
            Length = length;
        }
    }

    /// <summary>
    /// The mediaplayer's video output changed
    /// </summary>
    public partial class MediaPlayerVoutEventArgs : EventArgs
    {
        /// <summary>
        /// Number of available video outputs
        /// </summary>
        public readonly int Count;

        internal MediaPlayerVoutEventArgs(int count)
        {
            Count = count;
        }
    }

    /// <summary>
    /// The mediaplayer scrambled status changed
    /// </summary>
    public partial class MediaPlayerScrambledChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The scrambled status
        /// </summary>
        public readonly int Scrambled;

        internal MediaPlayerScrambledChangedEventArgs(int scrambled)
        {
            Scrambled = scrambled;
        }
    }

    /// <summary>
    /// The mediaplayer has a new Elementary Stream (ES)
    /// </summary>
    public partial class MediaPlayerESAddedEventArgs : EventArgs
    {
        /// <summary>
        /// The Id of the new Elementary Stream (ES)
        /// </summary>
        public readonly int Id;

        /// <summary>
        /// The type of the new Elementary Stream (ES)
        /// </summary>
        public readonly TrackType Type;

        internal MediaPlayerESAddedEventArgs(int id, TrackType type)
        {
            Id = id;
            Type = type;
        }
    }

    /// <summary>
    /// An Elementary Stream (ES) was deleted
    /// </summary>
    public partial class MediaPlayerESDeletedEventArgs : EventArgs
    {
        /// <summary>
        /// The Id of the deleted Elementary Stream (ES)
        /// </summary>
        public readonly int Id;

        /// <summary>
        /// The type of the deleted Elementary Stream (ES)
        /// </summary>
        public readonly TrackType Type;

        internal MediaPlayerESDeletedEventArgs(int id, TrackType type)
        {
            Id = id;
            Type = type;
        }
    }

    /// <summary>
    /// An Elementary Stream (ES) was selected
    /// </summary>
    public partial class MediaPlayerESSelectedEventArgs : EventArgs
    {
        /// <summary>
        /// The Id of the selected Elementary Stream (ES)
        /// </summary>
        public readonly int Id;

        /// <summary>
        /// The type of the seleted Elementary Stream (ES)
        /// </summary>
        public readonly TrackType Type;

        internal MediaPlayerESSelectedEventArgs(int id, TrackType type)
        {
            Id = id;
            Type = type;
        }
    }

    /// <summary>
    /// The mediaplayer's audio device changed
    /// </summary>
    public partial class MediaPlayerAudioDeviceEventArgs : EventArgs
    {
        /// <summary>
        /// String describing the audio device
        /// </summary>
        public readonly string AudioDevice;

        internal MediaPlayerAudioDeviceEventArgs(string audioDevice)
        {
            AudioDevice = audioDevice;
        }
    }

    /// <summary>
    /// The mediaplayer's volume changed
    /// </summary>
    public partial class MediaPlayerVolumeChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The new volume
        /// </summary>
        public readonly float Volume;

        internal MediaPlayerVolumeChangedEventArgs(float volume)
        {
            Volume = volume;
        }
    }

    #endregion

    #region MediaList events

    /// <summary>
    /// Base class for MediaList events
    /// </summary>
    public abstract class MediaListBaseEventArgs : EventArgs
    {
        /// <summary>
        /// Current node
        /// </summary>
        public readonly Media Media;

        /// <summary>
        /// Current index
        /// </summary>
        public readonly int Index;

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="media">Current node</param>
        /// <param name="index">Current index</param>
        internal protected MediaListBaseEventArgs(Media media, int index)
        {
            Media = media;
            Index = index;
        }
    }

    /// <summary>
    /// An item has been added to the MediaList
    /// </summary>
    public partial class MediaListItemAddedEventArgs : MediaListBaseEventArgs
    {
        internal MediaListItemAddedEventArgs(Media media, int index) : base(media, index)
        {
        }
    }

    /// <summary>
    /// An item is about to be added to the MediaList
    /// </summary>
    public partial class MediaListWillAddItemEventArgs : MediaListBaseEventArgs
    {
        internal MediaListWillAddItemEventArgs(Media media, int index) : base(media, index)
        {
        }
    }

    /// <summary>
    /// An item has been deleted from the MediaList
    /// </summary>
    public partial class MediaListItemDeletedEventArgs : MediaListBaseEventArgs
    {
        internal MediaListItemDeletedEventArgs(Media media, int index) : base(media, index)
        {
        }
    }

    /// <summary>
    /// An item is about to be deleted from the MediaList
    /// </summary>
    public partial class MediaListWillDeleteItemEventArgs : MediaListBaseEventArgs
    {
        internal MediaListWillDeleteItemEventArgs(Media media, int index) : base(media, index)
        {
        }
    }

    #endregion

    #region RendererDiscoverer events

    /// <summary>
    /// A new RendererItem has been found
    /// </summary>
    public partial class RendererDiscovererItemAddedEventArgs : EventArgs
    {
        internal RendererDiscovererItemAddedEventArgs(RendererItem rendererItem)
        {
            RendererItem = rendererItem;
        }

        /// <summary>
        /// The newly found RendererItem
        /// </summary>
        public RendererItem RendererItem { get; }
    }

    /// <summary>
    /// A RendererItem has been deleted
    /// </summary>
    public partial class RendererDiscovererItemDeletedEventArgs : EventArgs
    {
        internal RendererDiscovererItemDeletedEventArgs(RendererItem rendererItem)
        {
            RendererItem = rendererItem;
        }

        /// <summary>
        /// The deleted RendererItem
        /// </summary>
        public RendererItem RendererItem { get; }
    }

    #endregion

    /// <summary>
    /// The LibVLC Log Event Arg
    /// </summary>
    public sealed partial class LogEventArgs : EventArgs
    {
        internal LogEventArgs(LogLevel level, string message, string module, string sourceFile, uint? sourceLine)
        {
            Level = level;
            Message = message;
            Module = module;
            SourceFile = sourceFile;
            SourceLine = sourceLine;
            FormattedLog = $"{module} {level}: {message}";
        }

        /// <summary>
        /// The severity of the log message.
        /// By default, you will only get error messages, but you can get all messages by specifying "-vv" in the options.
        /// </summary>
        public LogLevel Level { get; }

        /// <summary>
        /// The log message
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// The name of the module that emitted the message
        /// </summary>
        public string Module { get; }

        /// <summary>
        /// The source file that emitted the message.
        /// This may be <see langword="null"/> if that info is not available, i.e. always if you are using a release version of VLC.
        /// </summary>
        public string SourceFile { get; }

        /// <summary>
        /// The line in the <see cref="SourceFile"/> at which the message was emitted.
        /// This may be <see langword="null"/> if that info is not available, i.e. always if you are using a release version of VLC.
        /// </summary>
        public uint? SourceLine { get; }

        /// <summary>
        /// Helper property with already formatted log message
        /// </summary>
        public string FormattedLog { get; }
    }
}
