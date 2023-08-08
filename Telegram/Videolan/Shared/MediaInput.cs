using System;
using System.Runtime.InteropServices;

namespace LibVLCSharp.Shared
{
    /// <summary>
    /// An abstract class that lets libvlc read a media from callbacks.
    ///
    /// Override this to provide your own reading mechanism, or you can use the <see cref="StreamMediaInput"/> class
    /// </summary>
    public abstract class MediaInput : IDisposable
    {
        /// <summary>
        /// The GCHandle to be passed to callbacks as userData
        /// </summary>
        public GCHandle GcHandle { get; private set; }

        /// <summary>
        /// The constructor
        /// </summary>
        protected MediaInput()
        {
            GcHandle = GCHandle.Alloc(this);
        }

        /// <summary>
        /// A value indicating whether this Media input can be seeked in.
        /// </summary>
        public bool CanSeek { get; protected set; } = true;

        /// <summary>
        /// LibVLC calls this method when it wants to open the media
        /// </summary>
        /// <param name="size">This value must be filled with the length of the media (or ulong.MaxValue if unknown)</param>
        /// <returns><c>true</c> if the stream opened successfully</returns>
        public abstract bool Open(out ulong size);

        /// <summary>
        /// LibVLC calls this method when it wants to read the media
        /// </summary>
        /// <param name="buf">The buffer where read data must be written</param>
        /// <param name="len">The buffer length</param>
        /// <returns>strictly positive number of bytes read, 0 on end-of-stream, or -1 on non-recoverable error</returns>
        public abstract int Read(IntPtr buf, uint len);

        /// <summary>
        /// LibVLC calls this method when it wants to seek to a specific position in the media
        /// </summary>
        /// <param name="offset">The offset, in bytes, since the beginning of the stream</param>
        /// <returns><c>true</c> if the seek succeeded, false otherwise</returns>
        public abstract bool Seek(ulong offset);

        /// <summary>
        /// LibVLC calls this method when it wants to close the media.
        /// </summary>
        public abstract void Close();

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        /// <summary>
        /// Override this to dispose things in your child class
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    GcHandle.Free();
                }

                disposedValue = true;
            }
        }

        /// <summary>
        /// Disposes of this instance
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
