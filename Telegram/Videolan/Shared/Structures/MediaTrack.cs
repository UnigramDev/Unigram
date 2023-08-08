using System;
using System.Runtime.InteropServices;

namespace LibVLCSharp.Shared
{
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct MediaTrackStructure
    {
        internal readonly uint Codec;
        internal readonly uint OriginalFourcc;
        internal readonly int Id;
        internal readonly TrackType TrackType;
        internal readonly int Profile;
        internal readonly int Level;
        internal readonly IntPtr TrackData;
        internal readonly uint Bitrate;
        internal readonly IntPtr Language;
        internal readonly IntPtr Description;
    }

    internal readonly struct SubtitleTrackStructure
    {
        internal readonly IntPtr Encoding;

        internal SubtitleTrackStructure(IntPtr encoding)
        {
            Encoding = encoding;
        }
    }

    /// <summary>
    /// Audio track
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct AudioTrack
    {
        internal AudioTrack(uint channels, uint rate)
        {
            Channels = channels;
            Rate = rate;
        }

        /// <summary>
        /// Audio track channels
        /// </summary>
        public readonly uint Channels;

        /// <summary>
        /// Audio track rate
        /// </summary>
        public readonly uint Rate;
    }

    /// <summary>
    /// Video track
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct VideoTrack
    {
        /// <summary>
        /// Video height
        /// </summary>
        public readonly uint Height;

        /// <summary>
        /// Video Width
        /// </summary>
        public readonly uint Width;

        /// <summary>
        /// Video SarNum
        /// </summary>
        public readonly uint SarNum;

        /// <summary>
        /// Video SarDen
        /// </summary>
        public readonly uint SarDen;

        /// <summary>
        /// Video frame rate num
        /// </summary>
        public readonly uint FrameRateNum;

        /// <summary>
        /// Video frame rate den
        /// </summary>
        public readonly uint FrameRateDen;

        /// <summary>
        /// Video orientation
        /// </summary>
        public readonly VideoOrientation Orientation;

        /// <summary>
        /// Video projection
        /// </summary>
        public readonly VideoProjection Projection;

        /// <summary>
        /// Video viewpoint
        /// </summary>
        public readonly VideoViewpoint Pose;
    }

    /// <summary>
    /// Subtitle track
    /// </summary>
    public readonly struct SubtitleTrack
    {
        internal SubtitleTrack(string encoding)
        {
            Encoding = encoding;
        }
        /// <summary>
        /// Subtitle encoding
        /// </summary>
        public readonly string Encoding;
    }

    /// <summary>
    /// Media track information
    /// </summary>
    public readonly struct MediaTrack
    {
        internal MediaTrack(uint codec, uint originalFourcc, int id, TrackType trackType, int profile,
            int level, MediaTrackData data, uint bitrate, string language, string description)
        {
            Codec = codec;
            OriginalFourcc = originalFourcc;
            Id = id;
            TrackType = trackType;
            Profile = profile;
            Level = level;
            Data = data;
            Bitrate = bitrate;
            Language = language;
            Description = description;
        }
        /// <summary>
        /// Media track codec
        /// </summary>
        public readonly uint Codec;

        /// <summary>
        /// Media track original fourcc
        /// </summary>
        public readonly uint OriginalFourcc;

        /// <summary>
        /// Media track id
        /// </summary>
        public readonly int Id;

        /// <summary>
        /// Media track type
        /// </summary>
        public readonly TrackType TrackType;

        /// <summary>
        /// Media track profile
        /// </summary>
        public readonly int Profile;

        /// <summary>
        /// Media track level
        /// </summary>
        public readonly int Level;

        /// <summary>
        /// Media track data
        /// </summary>
        public readonly MediaTrackData Data;

        /// <summary>
        /// Media track bitrate
        /// </summary>
        public readonly uint Bitrate;

        /// <summary>
        /// Media track language
        /// </summary>
        public readonly string Language;

        /// <summary>
        /// Media track description
        /// </summary>
        public readonly string Description;
    }
}
