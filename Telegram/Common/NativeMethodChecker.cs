//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Runtime.InteropServices;
#if NET9_0_OR_GREATER
using System.Runtime.InteropServices.Marshalling;
#endif

namespace Telegram.Common
{
    /// <summary>
    /// Resolves an export by name, for entry points that may not exist on the running version of
    /// Windows and so cannot be imported statically.
    /// </summary>
    public static partial class NativeMethodInvoker
    {
        // The address, not a delegate: Marshal.GetDelegateForFunctionPointer is runtime marshalling,
        // which DisableRuntimeMarshalling turns off. Callers invoke through a function pointer.
        public static IntPtr GetNativeMethod(string moduleName, string functionName)
        {
            var moduleHandle = GetModuleHandle(moduleName);

            if (moduleHandle == IntPtr.Zero)
            {
                moduleHandle = LoadLibrary(moduleName);
            }

            if (moduleHandle == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            return GetProcAddress(moduleHandle, functionName);
        }

#if NET9_0_OR_GREATER
        [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
        private static partial IntPtr GetModuleHandle(string lpModuleName);

        [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
        private static partial IntPtr LoadLibrary(string lpFileName);

        // Ansi, and deliberately: GetProcAddress has no wide form, so the name is bytes either way.
        [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Custom,
            StringMarshallingCustomType = typeof(AnsiStringMarshaller), SetLastError = true)]
        private static partial IntPtr GetProcAddress(IntPtr hModule, string lpProcName);
#else
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);
#endif
    }
}
