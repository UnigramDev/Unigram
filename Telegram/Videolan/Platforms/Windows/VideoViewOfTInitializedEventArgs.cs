using System;

namespace LibVLCSharp.Platforms.Windows
{
    /// <summary>
    /// VideoView base class for the UWP platform with <see cref="Initialized"/> event
    /// </summary>
    public abstract class VideoView<TInitializedEventArgs> : VideoViewBase where TInitializedEventArgs : EventArgs
    {
        /// <summary>
        /// Occurs when the <see cref="VideoView"/> is fully loaded and the <see cref="VideoViewBase.SwapChainOptions"/> property is set
        /// </summary>
        public event EventHandler<TInitializedEventArgs>? Initialized;

        /// <summary>
        /// Creates args for <see cref="Initialized"/> event
        /// </summary>
        /// <returns>args for <see cref="Initialized"/> event</returns>
        protected abstract TInitializedEventArgs CreateInitializedEventArgs();

        /// <summary>
        /// Raises the <see cref="Initialized"/> event
        /// </summary>
        protected override void OnInitialized()
        {
            Initialized?.Invoke(this, CreateInitializedEventArgs());
        }
    }
}
