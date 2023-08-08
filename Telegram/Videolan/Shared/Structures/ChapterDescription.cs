using System;
using System.Runtime.InteropServices;

namespace LibVLCSharp.Shared.Structures
{
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct ChapterDescriptionStructure
    {
        internal readonly long TimeOffset;
        internal readonly long Duration;
        internal readonly IntPtr Name;
    }

    /// <summary>
    /// <para>Description for chapters.</para>
    /// </summary>
    public readonly struct ChapterDescription
    {
        /// <summary>
        /// Time-offset of the chapter in milliseconds
        /// </summary>
        public long TimeOffset { get; }

        /// <summary>
        /// Duration of the chapter in milliseconds
        /// </summary>
        public long Duration { get; }

        /// <summary>
        /// Chapter name
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// TrackDescription constructor
        /// </summary>
        /// <param name="timeOffset">Chapter time-offset</param>
        /// <param name="duration">Chapter duration</param>
        /// <param name="name">Chapter name</param>
        internal ChapterDescription(long timeOffset, long duration, string name)
        {
            TimeOffset = timeOffset;
            Duration = duration;
            Name = name;
        }
    }
}
