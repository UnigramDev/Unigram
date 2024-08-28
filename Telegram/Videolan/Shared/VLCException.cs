using System;

namespace LibVLCSharp.Shared
{
    /// <summary>
    /// This exception is thrown when a problem with VLC occurred
    /// </summary>
    public partial class VLCException : Exception
    {
        /// <summary>
        /// VLC Exception constructor
        /// </summary>
        /// <param name="message"></param>
        public VLCException(string message = "") : base(message)
        {
        }

        /// <summary>
        /// Creates a <see cref="VLCException" /> with a message and an inner exeption
        /// </summary>
        public VLCException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
