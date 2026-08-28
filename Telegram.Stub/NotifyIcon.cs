//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Runtime.InteropServices;
using Telegram.Common;
using static Telegram.Common.NotifyIcon;
using Telegram.Stub;

namespace Telegram.Stub
{
    internal class NotifyIconSynchronizationContext : SynchronizationContext
    {
        private readonly IntPtr _hwnd;

        const int WM_USER_CALLBACK = 0x0400 + 1;

        public NotifyIconSynchronizationContext(IntPtr hwnd)
        {
            _hwnd = hwnd;
        }

        public override void Post(SendOrPostCallback d, object? state)
        {
            var handle = GCHandle.Alloc(Tuple.Create(d, state));
            NativeMethods.PostMessage(_hwnd, WM_USER_CALLBACK, IntPtr.Zero, GCHandle.ToIntPtr(handle));
        }
    }

}

namespace Telegram.Common
{
    // The half of the notification icon that only the stub has: it owns the process, so it owns the
    // message loop, the synchronization context that marshals onto it, and the app service bridge
    // that tells the icon what to show. The Win32 flavour has all three already.
    public partial class NotifyIcon
    {
        private BridgeApplicationContext _context;
        private NotifyIconSynchronizationContext _synchronization;

        partial void OnCreated()
        {
            Closed += OnClosed;

            _context = new BridgeApplicationContext(this);
            _synchronization = new NotifyIconSynchronizationContext(_hwnd);

            SynchronizationContext.SetSynchronizationContext(_synchronization);

            MSG msg;
            while (NativeMethods.GetMessage(out msg, IntPtr.Zero, 0, 0))
            {
                if (msg.message == WM_USER_CALLBACK)
                {
                    var handle = GCHandle.FromIntPtr(msg.lParam);
                    var callback = handle.Target as Tuple<SendOrPostCallback, object?>;
                    handle.Free();
                    callback?.Item1.Invoke(callback.Item2);
                }

                NativeMethods.TranslateMessage(ref msg);
                NativeMethods.DispatchMessage(ref msg);
            }
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            NativeMethods.PostQuitMessage(0);
        }
    }
}
