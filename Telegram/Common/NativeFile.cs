//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Runtime.InteropServices;

namespace Telegram.Common
{
    /// <summary>
    /// Does a path exist, in one syscall and nothing else.
    ///
    /// The measured cost is the syscall and the app container's access check - 74 µs against a cold
    /// cache at app start, where parsing a whole update takes 10.7 - so there is nothing to gain
    /// from a cleverer call, and the projection hop this replaces was worth a few hundred
    /// nanoseconds of it. It is here rather than in Telegram.Native because it is one P/Invoke and
    /// crossing into C++/WinRT to make it costs more than it saves.
    /// </summary>
    public static partial class NativeFile
    {
        // The FromApp variants are the ones an app container may call for arbitrary paths: they
        // carry the capability check that the plain Win32 entry points skip.
#if NET9_0_OR_GREATER
        // StringMarshalling replaces CharSet, which LibraryImport does not accept: the W entry
        // point is named explicitly, so there is no suffix for it to pick either.
        [LibraryImport("api-ms-win-core-file-fromapp-l1-1-0.dll", EntryPoint = "GetFileAttributesExFromAppW",
            StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetFileAttributesExFromApp(string path, int level,
            out WIN32_FILE_ATTRIBUTE_DATA data);
#else
        [SuppressUnmanagedCodeSecurity]
        [DllImport("api-ms-win-core-file-fromapp-l1-1-0.dll", EntryPoint = "GetFileAttributesExFromAppW",
            CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileAttributesExFromApp(string path, int level,
            out WIN32_FILE_ATTRIBUTE_DATA data);
#endif

        // Two uints per FILETIME rather than a long: the C struct packs on 4, and a long would make
        // the runtime align it to 8 and pad the layout out of shape.
        [StructLayout(LayoutKind.Sequential)]
        private struct WIN32_FILE_ATTRIBUTE_DATA
        {
            public uint FileAttributes;
            public uint CreationTimeLow;
            public uint CreationTimeHigh;
            public uint LastAccessTimeLow;
            public uint LastAccessTimeHigh;
            public uint LastWriteTimeLow;
            public uint LastWriteTimeHigh;
            public uint FileSizeHigh;
            public uint FileSizeLow;
        }

        private const int GetFileExInfoStandard = 0;

        public static bool Exists(string path)
        {
            return GetFileAttributesExFromApp(path, GetFileExInfoStandard, out _);
        }
    }
}
