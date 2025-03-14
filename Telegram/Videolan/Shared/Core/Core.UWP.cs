using System;
using System.Runtime.InteropServices;
using Telegram.Services;

namespace LibVLCSharp.Shared
{
    /// <summary>
    /// The Core class handles libvlc loading intricacies on various platforms as well as
    /// the libvlc/libvlcsharp version match check.
    /// </summary>
    public static partial class Core
    {
        partial struct Native
        {
            [DllImport(Constants.Kernel32, CharSet = CharSet.Unicode, SetLastError = true)]
            internal static extern IntPtr LoadPackagedLibrary(string dllToLoad, uint reserved = 0);
        }

        static IntPtr LibvlcHandle;

        /// <summary>
        /// Load the native libvlc library (if necessary, depending on platform)
        /// <para/> Ensure that you installed the VideoLAN.LibVLC.[YourPlatform] package in your target project
        /// <para/> This will throw a <see cref="VLCException"/> if the native libvlc libraries cannot be found or loaded.
        /// <para/> It may also throw a <see cref="VLCException"/> if the LibVLC and LibVLCSharp major versions do not match.
        /// See https://code.videolan.org/videolan/LibVLCSharp/-/blob/master/docs/versioning.md for more info about the versioning strategy.
        /// </summary>
        /// <param name="libvlcDirectoryPath">The path to the directory that contains libvlc and libvlccore
        /// No need to specify unless running netstandard 1.1, or using custom location for libvlc
        /// <para/> This parameter is NOT supported on Linux, use LD_LIBRARY_PATH instead.
        /// </param>
        public static void Initialize(string libvlcDirectoryPath = null)
        {
            InitializeUWP();

            LibVLCLoaded = true;
        }

        static void InitializeUWP()
        {
            LibvlcHandle = Native.LoadPackagedLibrary(Constants.LibraryName);
            if (LibvlcHandle == IntPtr.Zero)
            {
                throw new VLCException($"Failed to load {Constants.LibraryName}{Constants.WindowsLibraryExtension}, error {Marshal.GetLastWin32Error()}." +
                    $"Please make sure that this library, {Constants.CoreLibraryName}{Constants.WindowsLibraryExtension} and the plugins are copied to the `AppX` folder." +
                    "For that, you can reference the `VideoLAN.LibVLC.UWP` NuGet package.");
            }
        }

        public static bool UseSpeex => SettingsService.Current.Diagnostics.UseSpeexResampler;
    }
}
