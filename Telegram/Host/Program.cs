//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Runtime.InteropServices;
using System.Threading;
using Windows.ApplicationModel;
using Windows.System;
using Windows.Storage;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Host
{
    /// <summary>
    /// The Win32 flavour's entry point, in place of the one the XAML compiler generates - which
    /// calls <c>Application.Start</c> and is suppressed by DISABLE_XAML_GENERATED_MAIN. An island
    /// host owns its own message loop, and that is the whole seam between the two app models.
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static int Main()
        {
            Win32.SetProcessDpiAwarenessContext(Win32.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

            // XAML will not initialize on a thread without one. CoreWindow used to supply it; a
            // Win32 host has to make its own, per thread rather than per window.
            var options = new DispatcherQueueOptions
            {
                dwSize = Marshal.SizeOf<DispatcherQueueOptions>(),
                threadType = 2,     // DQTYPE_THREAD_CURRENT
                apartmentType = 0   // DQTAT_COM_NONE - the thread is already STA
            };

            var hr = Win32.CreateDispatcherQueueController(options, out _);
            if (hr < 0)
            {
                throw Marshal.GetExceptionForHR(hr);
            }

            // The generated Main that DISABLE_XAML_GENERATED_MAIN suppresses did this before it
            // constructed App, and taking over the entry point means carrying it across. Without
            // it every await on a TDLib result resumes on a TDLib thread, and the failures look
            // like anything but a missing synchronization context.
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));

            // Before WindowsXamlManager, and this is load-bearing: the generated App.g.i.cs makes
            // it an IXamlMetadataProvider over XamlTypeInfo.g.cs, which is what resolves the app's
            // own types in markup inside an island. Gate 1.3a.
            var app = new App();

            WindowsXamlManager.InitializeForCurrentThread();

            // The real arguments rather than an invented launch: a packaged app gets them from
            // AppInstance, and everything downstream - protocol links, share targets, file
            // activation - already knows how to read them.
            app.Start(AppInstance.GetActivatedEventArgs());

            while (Win32.GetMessageW(out var message, IntPtr.Zero, 0, 0) > 0)
            {
                var handled = false;

                foreach (var window in IslandWindow.All)
                {
                    if (window.PreTranslateMessage(ref message))
                    {
                        handled = true;
                        break;
                    }
                }

                if (!handled)
                {
                    Win32.TranslateMessage(ref message);
                    Win32.DispatchMessageW(ref message);
                }
            }

            return 0;
        }
    }
}
