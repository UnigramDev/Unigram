using System;
using System.Runtime.InteropServices;

namespace LibVLCSharp.Shared.Structures
{
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct TrackDescriptionStructure
    {
        internal readonly int Id;
        internal readonly IntPtr Name;
        internal readonly IntPtr Next;
    }

    /// <summary>
    /// <para>Description for video, audio tracks and subtitles. It contains</para>
    /// <para>id, name (description string)</para>
    /// </summary>
    public readonly struct TrackDescription
    {
        /// <summary>
        /// Track description Id
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// Track description
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// TrackDescription constructor
        /// </summary>
        /// <param name="id">Track description Id</param>
        /// <param name="name">Track description</param>
        internal TrackDescription(int id, string name)
        {
            Id = id;
            Name = name;
        }
    }
}