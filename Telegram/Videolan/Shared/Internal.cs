using System;

namespace LibVLCSharp.Shared
{
    /// <summary>
    /// This base class is used for the main libvlc types
    /// </summary>
    public abstract class Internal : IDisposable
    {
        /// <summary>
        /// The pointer to the native code representation of this object
        /// </summary>
        public IntPtr NativeReference { get; private set; }

        /// <summary>
        /// Release native resources by calling this C function
        /// </summary>
        protected readonly Action<IntPtr> Release;

        /// <summary>
        /// Indicates whether this object has already been disposed
        /// </summary>
        protected bool IsDisposed;

        /// <summary>
        /// Base constructor for most libvlc objects. Will perform native calls.
        /// </summary>
        /// <param name="create">A create function that will return a pointer to the instance in native code</param>
        /// <param name="release">A release Action that takes the native pointer to that C# instance's native code representation
        /// and performs the release call in native code. It will be called once when the C# instance gets disposed.</param>
        protected Internal(Func<IntPtr> create, Action<IntPtr> release)
        {
            Release = release;
            var nativeRef = create();
            if (nativeRef == IntPtr.Zero)
                OnNativeInstanciationError();
            NativeReference = nativeRef;
        }

        /// <summary>
        /// Dispose of this instance
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Dispose of this instance
        /// </summary>
        /// <param name="disposing">true if called from a method</param>
        protected virtual void Dispose(bool disposing)
        {
            if (IsDisposed || NativeReference == IntPtr.Zero)
                return;

            // dispose unmanaged resources
            Release(NativeReference);
            NativeReference = IntPtr.Zero;
            IsDisposed = true;
        }

        internal virtual void OnNativeInstanciationError() => throw new VLCException("Failed to perform instanciation on the native side. " +
                    "Make sure you installed the correct VideoLAN.LibVLC.[YourPlatform] package in your platform specific project");
    }
}
