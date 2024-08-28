using System.Collections.Generic;
using System.Linq;

namespace LibVLCSharp.Shared
{
    /// <summary>
    /// Configuration helper designed to be used for advanced libvlc configuration
    /// <para/> More info at https://wiki.videolan.org/VLC_command-line_help/
    /// </summary>
    public partial class MediaConfiguration
    {
        readonly Dictionary<string, string> _options = new Dictionary<string, string>
        {
            { nameof(EnableHardwareDecoding), string.Empty },
            { nameof(FileCaching), string.Empty },
            { nameof(NetworkCaching), string.Empty },
        };

        bool _enableHardwareDecoding;
        /// <summary>
        /// Enable/disable hardware decoding (crossplatform).
        /// </summary>
        public bool EnableHardwareDecoding
        {
            get => _enableHardwareDecoding;
            set
            {
                _enableHardwareDecoding = value;
                _options[nameof(EnableHardwareDecoding)] = HardwareDecodingOptionString(_enableHardwareDecoding);
            }
        }

        uint _fileCaching;
        /// <summary>
        /// Caching value for local files, in milliseconds [0 .. 60000ms]
        /// </summary>
        public uint FileCaching
        {
            get => _fileCaching;
            set
            {
                _fileCaching = value;
                _options[nameof(FileCaching)] = FileCachingOptionString(_fileCaching);
            }
        }

        uint _networkCaching;
        /// <summary>
        /// Caching value for network resources, in milliseconds [0 .. 60000ms]
        /// </summary>
        public uint NetworkCaching
        {
            get => _networkCaching;
            set
            {
                _networkCaching = value;
                _options[nameof(NetworkCaching)] = NetworkCachingOptionString(_networkCaching);
            }
        }

#if ANDROID
        const string ENABLE_HW_ANDROID = ":codec=mediacodec_ndk";
        const string DISABLE_HW_ANDROID = "";
#endif
        const string ENABLE_HW_APPLE = ":videotoolbox";
        const string ENABLE_HW_WINDOWS = ":avcodec-hw=d3d11va";

        const string DISABLE_HW_APPLE = ":no-videotoolbox";
        const string DISABLE_HW_WINDOWS = ":avcodec-hw=none";

        private string HardwareDecodingOptionString(bool enable)
        {
            if (enable)
            {
#if ANDROID
                return ENABLE_HW_ANDROID;
#elif APPLE
                return ENABLE_HW_APPLE;
#else
                if (PlatformHelper.IsWindows)
                    return ENABLE_HW_WINDOWS;
                if (PlatformHelper.IsMac)
                    return ENABLE_HW_APPLE;
                return string.Empty;
#endif
            }
            else
            {
#if ANDROID
                return DISABLE_HW_ANDROID;
#elif APPLE
                return DISABLE_HW_APPLE;
#else
                if (PlatformHelper.IsWindows)
                    return DISABLE_HW_WINDOWS;
                if (PlatformHelper.IsMac)
                    return DISABLE_HW_APPLE;
                return string.Empty;
#endif
            }

        }

        private string FileCachingOptionString(uint fileCaching)
        {
            return ":file-caching=" + fileCaching;
        }

        private string NetworkCachingOptionString(uint networkCaching)
        {
            return ":network-caching=" + networkCaching;
        }

        /// <summary>
        /// Builds the current MediaConfiguration for consumption by libvlc (or storage)
        /// </summary>
        /// <returns>Configured libvlc options as strings</returns>
        public string[] Build() => _options.Values.Where(option => !string.IsNullOrEmpty(option)).ToArray();
    }
}
