using System.Runtime.InteropServices;

namespace LibVLCSharp.Shared
{
    /// <summary>Viewpoint for video outputs</summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct VideoViewpoint
    {
        internal VideoViewpoint(float yaw, float pitch, float roll, float fov)
        {
            Yaw = yaw;
            Pitch = pitch;
            Roll = roll;
            Fov = fov;
        }

        /// <summary>
        /// view point yaw in degrees  ]-180;180]
        /// </summary>
        public readonly float Yaw;

        /// <summary>
        /// view point pitch in degrees  ]-90;90]
        /// </summary>
        public readonly float Pitch;

        /// <summary>
        /// view point roll in degrees ]-180;180]
        /// </summary>
        public readonly float Roll;

        /// <summary>
        /// field of view in degrees ]0;180[ (default 80.)
        /// </summary>
        public readonly float Fov;
    }
}