//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Web.WebView2.Core;
using System;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;

namespace Telegram.Common
{
    // The UWP half: the carrier thread is a CoreApplication view that is never shown. Its CoreWindow
    // exists without activation, is cloaked, and keeps the relay traffic off every window's thread.
    public sealed partial class WebProxyCarrierHost
    {
        // OnWindowCreated runs on the new view's thread before CreateNewView returns, and the view
        // must skip the per-window setup. Every view is created on the main thread, so a flag set
        // around the call cannot catch another view.
        private static volatile bool _creatingView;

        public static bool IsCreatingView => _creatingView;

        // TDLib restores an enabled proxy as soon as it starts, which is usually before the main window
        // exists, and no view can be created until it does
        private async void StartThread()
        {
            await ViewService.WaitForMainWindowAsync();

            CoreDispatcher dispatcher = null;
            try
            {
                dispatcher = CoreApplication.MainView.Dispatcher;
            }
            catch (Exception ex)
            {
                Logger.Info("[WebProxy] No main view: " + Describe(ex));
            }

            if (dispatcher == null)
            {
                OnThreadFailed(new WebProxyCarrierErrorPaused());
                return;
            }

            _ = dispatcher.RunAsync(CoreDispatcherPriority.Normal, CreateView);
        }

        private void CreateView()
        {
            CoreApplicationView view;

            _creatingView = true;
            try
            {
                // Throws when called while suspending or resuming:
                // https://devblogs.microsoft.com/oldnewthing/20210920-00/?p=105711
                view = CoreApplication.CreateNewView();
            }
            catch (Exception ex)
            {
                Logger.Error("[WebProxy] Failed to create the carrier view: " + Describe(ex));
                OnThreadFailed(new WebProxyCarrierErrorPaused());
                return;
            }
            finally
            {
                _creatingView = false;
            }

            OnThreadStarted(view.DispatcherQueue);
        }

        private static CoreWebView2ControllerWindowReference CreateParentWindow()
        {
            return CoreWebView2ControllerWindowReference.CreateFromCoreWindow(CoreWindow.GetForCurrentThread());
        }
    }
}
