namespace LibVLCSharp.Platforms.Windows
{
    /// <summary>
    /// VideoView implementation for the UWP platform
    /// </summary>
    public class VideoView : VideoView<InitializedEventArgs>
    {
        /// <summary>
        /// Creates args for <see cref="VideoView{TInitializedEventArgs}.Initialized"/> event
        /// </summary>
        /// <returns>args for <see cref="VideoView{TInitializedEventArgs}.Initialized"/> event</returns>
        protected override InitializedEventArgs CreateInitializedEventArgs()
        {
            return new InitializedEventArgs(SwapChainOptions);
        }
    }
}
