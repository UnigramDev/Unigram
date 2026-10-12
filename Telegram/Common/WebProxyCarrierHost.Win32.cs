//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Web.WebView2.Core;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.System;

namespace Telegram.Common
{
    // The Win32 half: every window shares the main thread and there are no hidden views, so the
    // carriers live on that thread, parented to the main window.
    public sealed partial class WebProxyCarrierHost
    {
        private async void StartThread()
        {
            await ViewService.WaitForMainWindowAsync();

            var window = WindowContext.Main;
            if (window == null)
            {
                OnThreadFailed(new WebProxyCarrierErrorPaused());
                return;
            }

            window.Dispatcher.Dispatch(OnMainThread);
        }

        private void OnMainThread()
        {
            OnThreadStarted(DispatcherQueue.GetForCurrentThread());
        }

        private static CoreWebView2ControllerWindowReference CreateParentWindow()
        {
            return CoreWebView2ControllerWindowReference.CreateFromWindowHandle((ulong)WindowContext.Main.Handle);
        }
    }
}
