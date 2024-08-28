using System;

namespace LibVLCSharp.Shared
{
    /// <summary>
    /// The MediaPlayerChanged event indicates when a new MediaPlayer has been set up with the VideoView
    /// and is ready to use for a first time playback.
    /// </summary>
    public partial class MediaPlayerChangedEventArgs : EventArgs
    {
        /// <summary>
        /// MediaPlayerChangedEventArgs constructor, used internally by LibVLCSharp
        /// </summary>
        /// <param name="oldMediaPlayer">The previous mediaplayer (if any)</param>
        /// <param name="newMediaPlayer">The new mediaplayer (if any)</param>
        public MediaPlayerChangedEventArgs(MediaPlayer oldMediaPlayer, MediaPlayer newMediaPlayer)
        {
            OldMediaPlayer = oldMediaPlayer;
            NewMediaPlayer = newMediaPlayer;
        }

        /// <summary>
        /// The previous mediaplayer (if any)
        /// </summary>
        public MediaPlayer OldMediaPlayer { get; }

        /// <summary>
        /// The new mediaplayer (if any)
        /// </summary>
        public MediaPlayer NewMediaPlayer { get; }
    }
}
