using System;

namespace LibVLCSharp.Shared
{
    /// <summary>
    /// Interface for video control
    /// </summary>
    internal interface IVideoControl : IVideoView
    {
        /// <summary>
        /// Occurs when the size of the control changes
        /// </summary>
        event EventHandler SizeChanged;

        /// <summary>
        /// Gets the width of the video view
        /// </summary>
        double Width { get; }

        /// <summary>
        /// Gets the height of the video view
        /// </summary>
        double Height { get; }
    }
}
