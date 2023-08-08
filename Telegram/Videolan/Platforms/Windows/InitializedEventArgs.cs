using System;

namespace LibVLCSharp.Platforms.Windows
{
    /// <summary>
    /// Provides data for the <see cref="VideoView{TInitializedEventArgs}.Initialized"/> event.
    /// </summary>
    public class InitializedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of <see cref="InitializedEventArgs"/> class
        /// </summary>
        /// <param name="swapChainOptions">swap chain parameters</param>
        public InitializedEventArgs(string[] swapChainOptions) : base()
        {
            SwapChainOptions = swapChainOptions;
        }

        /// <summary>
        /// Gets the swap chain parameters
        /// </summary>
        public string[] SwapChainOptions { get; }
    }
}
