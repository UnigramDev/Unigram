//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Windows.UI.Composition;
using Windows.UI.WindowManagement;

#if NET9_0_OR_GREATER
using System.Runtime.InteropServices.Marshalling;
#endif

namespace Telegram.Common
{
#if NET9_0_OR_GREATER
    [CustomMarshaller(typeof(Vector2), MarshalMode.Default, typeof(Vector2Marshaller))]
    internal static class Vector2Marshaller
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct Native { public float X, Y; }

        public static Native ConvertToUnmanaged(Vector2 v) => new() { X = v.X, Y = v.Y };
        public static Vector2 ConvertToManaged(Native n) => new(n.X, n.Y);
    }

    [GeneratedComInterface]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
#else
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
#endif
    [Guid("F26DA89E-683D-4C67-AEA7-BA29B2217A70")]
    public partial interface ICompositionVisualSurfacePartner
    {
#if NET9_0_OR_GREATER
        [PreserveSig]
        int GetIids(out uint iidCount, out IntPtr iids);
        [PreserveSig]
        int GetRuntimeClassName(out IntPtr className);
        [PreserveSig]
        int GetTrustLevel(out int trustLevel);

        [return: MarshalUsing(typeof(Vector2Marshaller))]
        Vector2 get_RealizationSize();
        void set_RealizationSize([MarshalUsing(typeof(Vector2Marshaller))] Vector2 value);
#else
        Vector2 RealizationSize { get; set; }
#endif

#if NET9_0_OR_GREATER
        CompositionStretch get_Stretch();
        void set_Stretch(CompositionStretch value);
#else
        CompositionStretch Stretch { get; set; }
#endif

        void Freeze();
    }

#if NET9_0_OR_GREATER
    [GeneratedComInterface]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
#else
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
#endif
    [Guid("0764019b-52c1-41f9-b6f2-9cc205973692")]
    public partial interface IInternalCoreWindowPhone
    {
#if NET9_0_OR_GREATER
        [PreserveSig]
        int GetIids(out uint iidCount, out IntPtr iids);
        [PreserveSig]
        int GetRuntimeClassName(out IntPtr className);
        [PreserveSig]
        int GetTrustLevel(out int trustLevel);

        [return: MarshalUsing(typeof(ComInterfaceMarshaller<object>))]
        object get_NavigationClient();
        void set_NavigationClient([param: MarshalUsing(typeof(ComInterfaceMarshaller<object>))] object value);
#else
        object NavigationClient { [return: MarshalAs(UnmanagedType.IUnknown)] get; [param: MarshalAs(UnmanagedType.IUnknown)] set; }
#endif
    }

#if NET9_0_OR_GREATER
    [GeneratedComInterface]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
#else
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
#endif
    [Guid("45D64A29-A63E-4CB6-B498-5781D298CB4F")]
    public partial interface ICoreWindowInterop
    {
#if NET9_0_OR_GREATER
        IntPtr get_WindowHandle();
        void MessageHandled([MarshalAs(UnmanagedType.Bool)] bool value);
#else
        IntPtr WindowHandle { get; }
        void MessageHandled(bool value);
#endif
    }

#if NET9_0_OR_GREATER
    [GeneratedComInterface]
#else
    [ComImport]
#endif
    [Guid("a257681d-5cdd-401c-89f0-cba89ca8a39e")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public partial interface IApplicationWindowTitleBarNavigationClient
    {
#if NET9_0_OR_GREATER
        AppWindowTitleBarVisibility get_TitleBarPreferredVisibilityMode();
        void set_TitleBarPreferredVisibilityMode(AppWindowTitleBarVisibility value);
#else
        AppWindowTitleBarVisibility TitleBarPreferredVisibilityMode { get; set; }
#endif
    }

#if NET9_0_OR_GREATER
    [GeneratedComInterface]
#else
    [ComImport]
#endif
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public partial interface IMemoryBufferByteAccess
    {
        unsafe void GetBuffer(out byte* buffer, out uint capacity);
    }

#if NET9_0_OR_GREATER
    [GeneratedComInterface]
#else
    [ComImport]
#endif
    [Guid("905A0FEF-BC53-11DF-8C49-001E4FC686DA")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public partial interface IBufferByteAccess
    {
        unsafe void Buffer(out byte* value);
    }
}
