using System;

namespace LibVLCSharp.Shared
{
    /// <summary>
    /// Internal event used by LibVLCSharp.
    /// </summary>
    public partial class MediaPlayerChangingEventArgs : EventArgs
    {
        /// <summary>
        /// MediaPlayerChangingEventArgs constructor, used internally by LibVLCSharp
        /// </summary>
        /// <param name="oldMediaPlayer">The previous mediaplayer (if any)</param>
        /// <param name="newMediaPlayer">The new mediaplayer (if any)</param>
        public MediaPlayerChangingEventArgs(MediaPlayer oldMediaPlayer, MediaPlayer newMediaPlayer)
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
