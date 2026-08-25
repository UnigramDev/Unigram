//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Runtime.InteropServices;
using Windows.UI.Xaml;

namespace Telegram.Host
{
    /// <summary>
    /// <c>Windows.UI.Xaml.IWindowPrivate</c> - the switch that turns off XAML's opaque backstop.
    ///
    /// Without it a DWM backdrop appears in the non-client area only: the title bar shows it and
    /// the client area stays an opaque sheet. Nothing in the public surface turns that off - a null
    /// root Background, WS_EX_NOREDIRECTIONBITMAP, swallowing WM_ERASEBKGND and
    /// DwmExtendFrameIntoClientArea do not, together or apart. See gate 1.10.
    ///
    /// Terminal calls this at startup ("Enable vintage opacity by removing the XAML emergency
    /// backstop, GH#603") through TerminalTrySetTransparentBackground, which ships in
    /// Microsoft.Internal.Windows.Terminal.ThemeHelpers - an internal package that is not on
    /// nuget.org. So this is reproduced from the shipped TerminalThemeHelpers.dll rather than
    /// referenced: the export activates Windows.UI.Xaml.Window, calls IWindowStatics::get_Current,
    /// QIs the result for the IID below and calls vtable slot 7 with the boolean.
    ///
    /// Vtable after IInspectable (0 QI, 1 AddRef, 2 Release, 3 GetIids, 4 GetRuntimeClassName,
    /// 5 GetTrustLevel):
    ///   6 get_TransparentBackground(boolean*)
    ///   7 put_TransparentBackground(boolean)
    ///
    /// Undocumented, and therefore a risk weighed rather than a fact relied on - which is why it is
    /// isolated here, so the backdrop can be dropped without touching the host. It is also the same
    /// bet Terminal has been shipping for years.
    ///
    /// PER THREAD, not per window: Window.Current inside an island is a per-thread stub, so every
    /// window on a thread shares this switch.
    /// </summary>
    internal static unsafe class WindowPrivate
    {
        private static readonly Guid IID_WindowPrivate = new("06636c29-5a17-458d-8ea2-2422d997a922");

        [ThreadStatic]
        private static bool _applied;

        public static bool TrySetTransparentBackground(bool value)
        {
            if (_applied == value)
            {
                return true;
            }

            var window = Window.Current;
            if (window == null)
            {
                return false;
            }

            var inspectable = WinRT.MarshalInspectable<Window>.FromManaged(window);
            if (inspectable == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                var iid = IID_WindowPrivate;
                var hr = Marshal.QueryInterface(inspectable, in iid, out var ptr);

                if (hr < 0)
                {
                    return false;
                }

                try
                {
                    var vtbl = *(void***)ptr;
                    hr = ((delegate* unmanaged[Stdcall]<IntPtr, byte, int>)vtbl[7])(ptr, value ? (byte)1 : (byte)0);

                    _applied = hr >= 0 && value;
                    return hr >= 0;
                }
                finally
                {
                    Marshal.Release(ptr);
                }
            }
            finally
            {
                Marshal.Release(inspectable);
            }
        }
    }
}
