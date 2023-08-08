using System;

namespace LibVLCSharp.Shared
{
    internal readonly struct MediaSlaveStructure
    {
        internal MediaSlaveStructure(IntPtr uri, MediaSlaveType type, uint priority)
        {
            Uri = uri;
            Type = type;
            Priority = priority;
        }

        internal readonly IntPtr Uri;
        internal readonly MediaSlaveType Type;
        internal readonly uint Priority;
    }

    /// <summary>A slave of a libvlc_media_t</summary>
    /// <remarks>libvlc_media_slaves_get</remarks>
    public readonly struct MediaSlave
    {
        internal MediaSlave(string uri, MediaSlaveType type, uint priority)
        {
            Uri = uri;
            Type = type;
            Priority = priority;
        }

        /// <summary>
        /// Media slave URI
        /// </summary>
        public readonly string Uri;

        /// <summary>
        /// Media slave type
        /// </summary>
        public readonly MediaSlaveType Type;

        /// <summary>
        /// Media slave priority
        /// </summary>
        public readonly uint Priority;
    }
}
