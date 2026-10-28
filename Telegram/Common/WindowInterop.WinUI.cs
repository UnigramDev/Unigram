//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Runtime.InteropServices;
using Windows.Media;
using Windows.UI.ViewManagement;

namespace Telegram.Common
{
    /// <summary>
    /// The per-window getters behind the SDK projection's UIViewSettingsInterop, InputPaneInterop
    /// and SystemMediaTransportControlsInterop, called directly. Those classes cast the object
    /// GetForWindow returns to the projected class, and under NativeAOT it arrives as a bare
    /// IInspectable wrapper, so the cast throws InvalidCastException. FromAbi is told the class
    /// instead of looking it up, which is what makes it safe there.
    /// </summary>
    internal static unsafe partial class WindowInterop
    {
        public static UIViewSettings GetUIViewSettings(IntPtr hwnd)
        {
            var result = GetForWindow("Windows.UI.ViewManagement.UIViewSettings",
                new Guid("3694DBF9-8F68-44BE-8FF5-195C98EDE8A6"),
                new Guid("C63657F6-8850-470D-88F8-455E16EA2C26"),
                hwnd);

            try
            {
                return UIViewSettings.FromAbi(result);
            }
            finally
            {
                Marshal.Release(result);
            }
        }

        public static InputPane GetInputPane(IntPtr hwnd)
        {
            var result = GetForWindow("Windows.UI.ViewManagement.InputPane",
                new Guid("75CF2C57-9195-4931-8332-F0B409E916AF"),
                new Guid("640ADA70-06F3-4C87-A678-9829C9127C28"),
                hwnd);

            try
            {
                return InputPane.FromAbi(result);
            }
            finally
            {
                Marshal.Release(result);
            }
        }

        public static SystemMediaTransportControls GetSystemMediaTransportControls(IntPtr hwnd)
        {
            var result = GetForWindow("Windows.Media.SystemMediaTransportControls",
                new Guid("DDB0472D-C911-4A1F-86D9-DC3D71A95F5A"),
                new Guid("99FA3FF4-1742-42A6-902E-087D41F965EC"),
                hwnd);

            try
            {
                return SystemMediaTransportControls.FromAbi(result);
            }
            finally
            {
                Marshal.Release(result);
            }
        }

        private static IntPtr GetForWindow(string className, Guid interopIid, Guid riid, IntPtr hwnd)
        {
            Marshal.ThrowExceptionForHR(WindowsCreateString(className, className.Length, out var hstring));

            try
            {
                Marshal.ThrowExceptionForHR(RoGetActivationFactory(hstring, ref interopIid, out var factory));

                try
                {
                    // IInspectable's three methods come first; all three interop interfaces have
                    // GetForWindow as their only one.
                    var vtbl = *(IntPtr**)factory;
                    IntPtr result;

                    Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, Guid*, IntPtr*, int>)vtbl[6])(factory, hwnd, &riid, &result));
                    return result;
                }
                finally
                {
                    Marshal.Release(factory);
                }
            }
            finally
            {
                WindowsDeleteString(hstring);
            }
        }

        [LibraryImport("combase.dll", StringMarshalling = StringMarshalling.Utf16)]
        private static partial int WindowsCreateString(string sourceString, int length, out IntPtr hstring);

        [LibraryImport("combase.dll")]
        private static partial int WindowsDeleteString(IntPtr hstring);

        [LibraryImport("combase.dll")]
        private static partial int RoGetActivationFactory(IntPtr activatableClassId, ref Guid iid, out IntPtr factory);
    }
}
