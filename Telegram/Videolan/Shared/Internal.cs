using System;
using System.Runtime.InteropServices;

namespace LibVLCSharp.Shared
{
    /// <summary>
    /// This base class is used for the main libvlc types
    /// </summary>
    public abstract class Internal : SafeHandle
    {
        /// <summary>
        /// Base constructor for most libvlc objects. Will perform native calls.
        /// </summary>
        /// <param name="create">A create function that will return a pointer to the instance in native code</param>
        /// <param name="release">A release Action that takes the native pointer to that C# instance's native code representation
        /// and performs the release call in native code. It will be called once when the C# instance gets disposed.</param>
        protected Internal(IntPtr handle)
            : base(IntPtr.Zero, true)
        {
            SetHandle(handle);

            if (IsInvalid)
                OnNativeInstanciationError();
        }

        public override bool IsInvalid => handle == IntPtr.Zero;

        internal virtual void OnNativeInstanciationError() => throw new VLCException("Failed to perform instanciation on the native side. " +
                    "Make sure you installed the correct VideoLAN.LibVLC.[YourPlatform] package in your platform specific project");
    }
}
