//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Runtime.InteropServices;

namespace Telegram.Common
{
    public static class NativeMethodInvoker
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeLibrary(IntPtr hModule);

        public static bool IsNativeMethodAvailable(string moduleName, string functionName, bool loadIfNotPresent = true)
        {
            IntPtr moduleHandle = GetModuleHandle(moduleName);
            bool shouldFreeLibrary = false;

            if (moduleHandle == IntPtr.Zero && loadIfNotPresent)
            {
                moduleHandle = LoadLibrary(moduleName);
                shouldFreeLibrary = true;
            }

            if (moduleHandle == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                IntPtr procAddress = GetProcAddress(moduleHandle, functionName);
                return procAddress != IntPtr.Zero;
            }
            finally
            {
                if (shouldFreeLibrary)
                {
                    FreeLibrary(moduleHandle);
                }
            }
        }

        public static TDelegate GetNativeMethod<TDelegate>(string moduleName, string functionName)
            where TDelegate : Delegate
        {
            IntPtr moduleHandle = GetModuleHandle(moduleName);

            if (moduleHandle == IntPtr.Zero)
            {
                moduleHandle = LoadLibrary(moduleName);
            }

            if (moduleHandle == IntPtr.Zero)
            {
                return null;
            }

            IntPtr procAddress = GetProcAddress(moduleHandle, functionName);

            if (procAddress == IntPtr.Zero)
            {
                return null;
            }

            return Marshal.GetDelegateForFunctionPointer<TDelegate>(procAddress);
        }

        public static bool TryInvokeNativeMethod<TDelegate>(
            string moduleName,
            string functionName,
            Action<TDelegate> invokeAction)
            where TDelegate : Delegate
        {
            var method = GetNativeMethod<TDelegate>(moduleName, functionName);

            if (method == null)
            {
                return false;
            }

            invokeAction(method);
            return true;
        }

        public static bool TryInvokeNativeMethod<TDelegate, TResult>(
            string moduleName,
            string functionName,
            Func<TDelegate, TResult> invokeFunc,
            out TResult result)
            where TDelegate : Delegate
        {
            var method = GetNativeMethod<TDelegate>(moduleName, functionName);

            if (method == null)
            {
                result = default;
                return false;
            }

            result = invokeFunc(method);
            return true;
        }
    }
}
