using System;

namespace LibVLCSharp.Shared
{
    /// <summary>
    /// Small helper for determining the current platform
    /// </summary>
    public partial class PlatformHelper
    {
        /// <summary>
        /// Returns true if running on Windows, false otherwise
        /// </summary>
        public static bool IsWindows => true;

        /// <summary>
        /// Returns true if running on Linux, false otherwise
        /// </summary>
        public static bool IsLinux => false;

        /// <summary>
        /// Returns true if running on Linux desktop, false otherwise
        /// </summary>
        public static bool IsLinuxDesktop => false;

        /// <summary>
        /// Returns true if running on macOS, false otherwise
        /// </summary>
        public static bool IsMac => false;

        /// <summary>
        /// Returns true if running in 64bit process, false otherwise
        /// </summary>
        public static bool IsX64BitProcess => IntPtr.Size == 8;
    }
}
