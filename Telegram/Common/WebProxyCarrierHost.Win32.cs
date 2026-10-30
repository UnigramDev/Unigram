//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Web.WebView2.Core;
using System;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.System;

namespace Telegram.Common
{
    // The Win32 half: every window shares the main thread and there are no hidden views, so the
    // carriers live on that thread, parented to HiddenWindow rather than to the main window, which
    // WinUI destroys on closing to the notification area icon.
    public sealed partial class WebProxyCarrierHost
    {
        private async void StartThread()
        {
            // TDLib restores an enabled proxy before the host has started, and with it the
            // dispatcher and HiddenWindow.
            await ViewService.WaitForMainWindowAsync();

            var dispatcher = BootStrapper.Current.Dispatcher;
            if (dispatcher == null || HiddenWindow.Handle == IntPtr.Zero)
            {
                OnThreadFailed(new WebProxyCarrierErrorPaused());
                return;
            }

            dispatcher.Dispatch(OnMainThread);
        }

        private void OnMainThread()
        {
            OnThreadStarted(DispatcherQueue.GetForCurrentThread());
        }

        private static CoreWebView2ControllerWindowReference CreateParentWindow()
        {
            return CoreWebView2ControllerWindowReference.CreateFromWindowHandle((ulong)HiddenWindow.Handle);
        }
    }
}
