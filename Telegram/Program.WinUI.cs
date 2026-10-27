//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Settings;
using Microsoft.Windows.AppLifecycle;
using Telegram.Host;
using Telegram.Navigation;
using Telegram.Services;
using Windows.Storage;

namespace Telegram
{
    /// <summary>
    /// The entry point the XAML compiler would otherwise generate, taken over for one reason:
    /// <see cref="XamlOptionalChanges"/> has to be set before XAML initialises, and the generated
    /// Main gives no room to do it.
    ///
    /// <c>DXamlCore::InitializeImpl</c> calls <c>XamlOptionalChanges::LockInternal()</c> as its
    /// first statement - their comment calls it "the single choke point for both
    /// Application.Start() and WindowsXamlManager.InitializeForCurrentThread()" - so anything
    /// enabled from the App constructor arrives after the bitmask is locked and is silently
    /// dropped. Hence DISABLE_XAML_GENERATED_MAIN, and hence the body below is the generated one
    /// verbatim apart from the four calls.
    /// </summary>
    public static class Program
    {
        private static DispatcherQueue _dispatcher;

        [STAThread]
        static void Main(string[] args)
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();

            // One instance, which the UWP app model gave the other flavours for free and this one
            // has to ask for: launching a packaged desktop app again simply starts a second
            // process. That second process then opens its own window over a database the first one
            // holds, which is the empty window you get from the Start menu while the tray icon is
            // up. Registering a key here makes every later launch redirect into this process.
            //
            // First, before anything this process would only do for itself.
            var keyInstance = AppInstance.FindOrRegisterForKey("Telegram");

            if (!keyInstance.IsCurrent)
            {
                RedirectActivationTo(AppInstance.GetCurrent().GetActivatedEventArgs(), keyInstance);
                return;
            }

            // Before any window: the updater waits for this process to be gone, then starts the
            // new version itself.
            if (UnpackagedUpdates.TryStartUpdater())
            {
                return;
            }

            keyInstance.Activated += OnRedirected;

            EnableOptionalChanges();

            Application.Start(p =>
            {
                _dispatcher = DispatcherQueue.GetForCurrentThread();

                var context = new DispatcherQueueSynchronizationContext(_dispatcher);
                SynchronizationContext.SetSynchronizationContext(context);

                _ = new App();
            });
        }

        /// <summary>
        /// Another launch was handed to this process. It arrives on a background thread, and all
        /// this flavour does with it is bring the window back - a redirected link or share target
        /// would have to go through BootStrapper the way the first activation does, which the
        /// renamed protocol claims make moot here.
        ///
        /// Brought back the way a click on the notification area icon does it, because the window
        /// may be gone: closing to the icon destroys it, and then there is no Main to raise and
        /// no window whose dispatcher could be used, so the UI thread's queue is kept from start.
        /// </summary>
        private static void OnRedirected(object sender, AppActivationArguments args)
        {
            _dispatcher?.TryEnqueue(BootStrapper.Current.ActivateFromNotifyIcon);
        }

        /// <summary>
        /// The wait is on a pool thread rather than this one: the redirect is a cross-process COM
        /// call, and blocking the STA that is about to become the UI thread on it deadlocks. There
        /// is no window yet, so there is nothing to pump.
        /// </summary>
        private static void RedirectActivationTo(AppActivationArguments args, AppInstance keyInstance)
        {
            // The user launched this process, so it may take the foreground and the running one,
            // in the background, may not: its window would only flash on the taskbar. Handed over
            // rather than used here, because the window to raise may not exist until the running
            // process reopens it from the notification area icon.
            Win32.AllowSetForegroundWindow(keyInstance.ProcessId);

            using var redirected = new ManualResetEvent(false);

            _ = Task.Run(async () =>
            {
                await keyInstance.RedirectActivationToAsync(args);
                redirected.Set();
            });

            redirected.WaitOne();
        }

        /// <summary>
        /// All four of them, which is every optional change WinUI 3 2.4 has. Each returns whether
        /// it took, and none is checked: a change this build does not know about is not a reason
        /// to refuse to start, and a locked bitmask would mean the call above moved.
        ///
        /// What they are, in the order they are enabled:
        ///
        /// - DefaultStyleOptimizations swaps the SDK's style set for its *_perf2026.xaml twins,
        ///   which are the same styles with VisualState Setters in place of Storyboards. Worth
        ///   less here than it sounds - about 93% of this app's visual state groups enter an empty
        ///   state at load - so it is an interaction-time win rather than a realization one.
        /// - OptimizeApplyStyles skips applying a style at parse time for an element that is not
        ///   live, and realizes setter values on demand instead of all of them up front. This is
        ///   the one UWP has no equivalent for.
        /// - IconNoGridOptimization parents a FontIcon's TextBlock directly rather than through a
        ///   Grid, which this app realizes a great many of.
        /// - DeferContextFlyoutInit stops every element entering the tree from resolving its
        ///   default ContextFlyout, which for TextBlock and RichTextBlock is a resource-dictionary
        ///   walk per element.
        /// </summary>
        private static void EnableOptionalChanges()
        {
            // An off switch that needs no rebuild: create a file called
            // "xaml-optional-changes.off" in the app's LocalState and relaunch. These four change
            // when styles are applied and what the default ones look like, so they are the first
            // thing to rule out when a control comes up with no template - and ruling them out is
            // otherwise a fifteen minute round trip.
            //
            // Empty turns all four off. Otherwise each line names one to leave off, so a suspect
            // can be confirmed on its own rather than as one of four.
            var disabled = Array.Empty<string>();

            try
            {
                var path = Path.Combine(AppPaths.LocalPath, "xaml-optional-changes.off");
                if (File.Exists(path))
                {
                    disabled = File.ReadAllLines(path)
                        .Select(x => x.Trim())
                        .Where(x => x.Length > 0)
                        .ToArray();

                    if (disabled.Length == 0)
                    {
                        return;
                    }
                }
            }
            catch
            {
                // No identity, or no LocalState yet. Neither is a reason not to start.
            }

            void Enable(XamlChangeId change)
            {
                if (!disabled.Contains(change.ToString(), StringComparer.OrdinalIgnoreCase))
                {
                    XamlOptionalChanges.EnableChange(change);
                }
            }

            Enable(XamlChangeId.DefaultStyleOptimizations);
            Enable(XamlChangeId.OptimizeApplyStyles);
            Enable(XamlChangeId.IconNoGridOptimization);
            Enable(XamlChangeId.DeferContextFlyoutInit);
        }
    }
}
