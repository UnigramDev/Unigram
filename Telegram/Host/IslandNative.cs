using System;
using System.Runtime.InteropServices;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Host
{
    /// <summary>
    /// IDesktopWindowXamlSourceNative{,2} by hand.
    ///
    /// A [ComImport] interface cast throws InvalidCastException on a CsWinRT-projected object -
    /// the projection does not route classic-COM QueryInterface through the runtime cast. So the
    /// IUnknown is taken from the projection and QI'd directly, and the vtable is called through
    /// function pointers. That is also what keeps this NativeAOT-safe, since it needs no
    /// built-in COM interop at all.
    ///
    /// Vtable after IUnknown (0 QI, 1 AddRef, 2 Release):
    ///   3 AttachToWindow(HWND)
    ///   4 get_WindowHandle(HWND*)
    ///   5 PreTranslateMessage(MSG*, BOOL*)   - v2 only
    /// </summary>
    internal sealed unsafe class IslandNative : IDisposable
    {
        private static readonly Guid IID_Native = new("3cbcf1bf-2f76-4e9c-96ab-e84b37972554");
        private static readonly Guid IID_Native2 = new("e3dcd8c7-3057-4692-99c3-7b7720afda31");

        private IntPtr _ptr;

        public bool IsVersion2 { get; private set; }

        public static IslandNative From(DesktopWindowXamlSource source)
        {
            var inspectable = WinRT.MarshalInspectable<DesktopWindowXamlSource>.FromManaged(source);
            if (inspectable == IntPtr.Zero)
            {
                throw new InvalidOperationException("Could not marshal DesktopWindowXamlSource to IInspectable.");
            }

            try
            {
                var iid2 = IID_Native2;
                if (Marshal.QueryInterface(inspectable, in iid2, out var ptr2) >= 0)
                {
                    return new IslandNative { _ptr = ptr2, IsVersion2 = true };
                }

                var iid = IID_Native;
                var hr = Marshal.QueryInterface(inspectable, in iid, out var ptr);
                if (hr < 0)
                {
                    throw new InvalidOperationException(
                        $"QueryInterface failed for both IDesktopWindowXamlSourceNative2 and ...Native (0x{hr:x8}).");
                }

                return new IslandNative { _ptr = ptr, IsVersion2 = false };
            }
            finally
            {
                Marshal.Release(inspectable);
            }
        }

        public void AttachToWindow(IntPtr parent)
        {
            var vtbl = *(void***)_ptr;
            var hr = ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int>)vtbl[3])(_ptr, parent);
            if (hr < 0)
            {
                throw Marshal.GetExceptionForHR(hr);
            }
        }

        public IntPtr GetWindowHandle()
        {
            var vtbl = *(void***)_ptr;
            IntPtr handle;
            var hr = ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)vtbl[4])(_ptr, &handle);
            if (hr < 0)
            {
                throw Marshal.GetExceptionForHR(hr);
            }

            return handle;
        }

        public bool PreTranslateMessage(ref MSG message)
        {
            if (!IsVersion2)
            {
                return false;
            }

            var vtbl = *(void***)_ptr;
            int handled;
            fixed (MSG* pMessage = &message)
            {
                var hr = ((delegate* unmanaged[Stdcall]<IntPtr, MSG*, int*, int>)vtbl[5])(_ptr, pMessage, &handled);
                if (hr < 0)
                {
                    return false;
                }
            }

            return handled != 0;
        }

        public void Dispose()
        {
            if (_ptr != IntPtr.Zero)
            {
                Marshal.Release(_ptr);
                _ptr = IntPtr.Zero;
            }
        }
    }
}
