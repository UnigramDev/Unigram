//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Windows.UI.Composition;
using Windows.UI.WindowManagement;

namespace Telegram.Common
{
    [Guid("F26DA89E-683D-4C67-AEA7-BA29B2217A70")]
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
    public interface ICompositionVisualSurfacePartner
    {
        Vector2 RealizationSize { get; set; }
        CompositionStretch Stretch { get; set; }
        void Freeze();
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
    [Guid("0764019b-52c1-41f9-b6f2-9cc205973692")]
    public interface IInternalCoreWindowPhone
    {
        object NavigationClient { [return: MarshalAs(UnmanagedType.IUnknown)] get; [param: MarshalAs(UnmanagedType.IUnknown)] set; }
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("a257681d-5cdd-401c-89f0-cba89ca8a39e")]
    public interface IApplicationWindowTitleBarNavigationClient
    {
        AppWindowTitleBarVisibility TitleBarPreferredVisibilityMode { get; set; }
    }

    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMemoryBufferByteAccess
    {
        unsafe void GetBuffer(out byte* buffer, out uint capacity);
    }

    [ComImport]
    [Guid("905A0FEF-BC53-11DF-8C49-001E4FC686DA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IBufferByteAccess
    {
        unsafe void Buffer(out byte* value);
    }
}
