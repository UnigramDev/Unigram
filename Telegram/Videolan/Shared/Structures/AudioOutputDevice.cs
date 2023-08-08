using System;
using System.Runtime.InteropServices;

namespace LibVLCSharp.Shared.Structures
{
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct AudioOutputDeviceStructure
    {
        internal readonly IntPtr Next;
        internal readonly IntPtr DeviceIdentifier;
        internal readonly IntPtr Description;
    }

    /// <summary>
    /// Description for audio output device
    /// </summary>
    public readonly struct AudioOutputDevice
    {
        /// <summary>
        /// AudioOutputDevice default constructor
        /// </summary>
        /// <param name="deviceIdentifier">Device identifier string</param>
        /// <param name="description">User-friendly device description</param>
        internal AudioOutputDevice(string deviceIdentifier, string description)
        {
            DeviceIdentifier = deviceIdentifier;
            Description = description;
        }

        /// <summary>
        /// Device identifier string.
        /// </summary>
        public readonly string DeviceIdentifier;

        /// <summary>
        /// User-friendly device description.
        /// </summary>
        public readonly string Description;
    }
}