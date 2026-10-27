//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Resources;
using Telegram.Common;
using Telegram.Host;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;

namespace Telegram.Navigation
{
    /// <summary>
    /// The WinUI 3 half of <see cref="BootStrapper"/>. Between its two twins: the framework does
    /// start the app here, unlike on the island host, but it announces only a launch - every other
    /// activation kind reaches a WinUI 3 app through <c>AppInstance</c> rather than through an
    /// override, and there is no <c>OnWindowCreated</c>, so the windows are still ours to make.
    /// </summary>
    public abstract partial class BootStrapper
    {
        // Matches the UWP flavour's SetPreferredMinSize, which is what a new view opened at.
        private const int DefaultWidth = 1024;
        private const int DefaultHeight = 720;

        /// <summary>
        /// Before <c>App</c>'s constructor, which is what the XAML compiler's generated entry point
        /// calls first and where the app already touches TDLib and its own settings. A module
        /// initializer is the only hook earlier than that when the entry point is generated - the
        /// island host does both of these at the top of its own Main.
        /// </summary>
        [ModuleInitializer]
        internal static void InitializeProcess()
        {
            // UWP guaranteed the current directory was the package install folder. The shell
            // launches a packaged desktop app with whatever directory the launcher had - system32,
            // as a rule - so every relative path in the app and its native libraries resolves
            // against the wrong place. MicroTeX is the one that showed it: RES_BASE is the literal
            // "res", so its fonts are looked up as res\fonts\... under system32.
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);

            // Explicit rather than through an application manifest, so that it is set the same way
            // and at the same point as on the island host. A no-op if the framework's own startup
            // got there first - the call fails and the process keeps the awareness it has.
            Win32.SetProcessDpiAwarenessContext(Win32.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        }

        /// <summary>
        /// The one activation override this framework has. Its argument carries a command line and
        /// nothing else, so the real arguments are read from AppInstance - the same call the island
        /// host makes, returning the same <c>IActivatedEventArgs</c> everything downstream already
        /// knows how to read.
        /// </summary>
        protected sealed override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs e)
        {
            // The default, OnLastWindowClose, ends the process the moment the last window is
            // destroyed - DesktopWindowImpl::OnMessage posts WM_QUIT on WM_DESTROY when the window
            // map empties. That is exactly what closing to the notification area icon does here,
            // so the app says when it is finished instead: WindowContext.ConsolidateAsync exits
            // when the last window closes and no icon is left holding the app open.
            DispatcherShutdownMode = DispatcherShutdownMode.OnExplicitShutdown;

            // AppInstance is the package's own activation queue, and there is none to read without
            // an identity - the call throws rather than answering with nothing.
            var args = ApiInfo.HasPackageIdentity ? AppInstance.GetActivatedEventArgs() : null;

            if (args is LaunchActivatedEventArgs launch)
            {
                Logger.Info(launch.Kind);
                WatchDog.Launch(launch.PreviousExecutionState);
                CallInternalLaunchAsync(launch);
            }
            else if (args != null)
            {
                Logger.Info(args.Kind);
                CallInternalActivated(args);
            }
            else
            {
                StartUnpackaged();
            }
        }

        /// <summary>
        /// Unpackaged, so a plain start is all this can have been: everything else - a tg: link,
        /// a shared file, a toast - reaches the app through an extension the shell only
        /// dispatches to a package. LaunchActivatedEventArgs is sealed and cannot be built, so the
        /// activate path takes it instead; the only thing the launch path adds that matters here
        /// is the watchdog, called by hand.
        ///
        /// async void so that a failure surfaces as an unhandled exception rather than in a task
        /// nobody observes. The continuation is back on this thread: Main installed the
        /// synchronization context inside Application.Start, before App was constructed.
        /// </summary>
        private async void StartUnpackaged()
        {
            Logger.Info("No identity; starting as a plain launch");

            await AppPaths.InitializeAsync();

            WatchDog.Launch(ApplicationExecutionState.NotRunning);
            CallInternalActivated(new PlainActivatedEventArgs(ApplicationExecutionState.NotRunning));
        }

        /// <summary>
        /// Opens the app again after its last window was closed to the notification area icon.
        ///
        /// The same pipeline a tg: link arriving at a running app goes through, and it copes with
        /// having no window: ResolveWindowContext builds one when Main is null, InitializeFrame
        /// fills it, and OnStart runs as an activation.
        ///
        /// What it must not carry is what the process was started with. AppInstance still answers
        /// with those, and using them would replay them - a chat opened by the tg: link that
        /// started the app would open again every time the icon is clicked, hours later. The app
        /// was not activated by anything here; the user asked for a window.
        /// </summary>
        partial void ReopenFromNotifyIcon()
        {
            Logger.Info();
            // Running, because it is: this process never stopped, it only closed its windows.
            CallInternalActivated(new PlainActivatedEventArgs(ApplicationExecutionState.Running));
        }

        /// <summary>
        /// A launch that carries nothing - which is what reopening from the notification area icon
        /// is, and what an unpackaged start is. There is no way to construct a
        /// LaunchActivatedEventArgs - it is a sealed WinRT class - but the pipeline takes the
        /// interface, and everything downstream either matches on a concrete activation type or
        /// reads Kind. Both answer the way a plain start does: no session to switch to, no link to
        /// follow, no share to fill in.
        /// </summary>
        /// Partial because CsWinRT says so, and it is not decoration: implementing a WinRT
        /// interface means the object can be asked for one, and Toast.GetSession does exactly that
        /// when it tests for ToastNotificationActivatedEventArgs. Without the vtable the generator
        /// emits for a partial type, that QI has nothing to answer with under NativeAOT - which is
        /// a crash on the first click, not a trimming warning.
        private sealed partial class PlainActivatedEventArgs : IActivatedEventArgs
        {
            public PlainActivatedEventArgs(ApplicationExecutionState previousExecutionState)
            {
                PreviousExecutionState = previousExecutionState;
            }

            public ActivationKind Kind => ActivationKind.Launch;

            public ApplicationExecutionState PreviousExecutionState { get; }

            public SplashScreen SplashScreen => null;
        }

        /// <summary>
        /// Prelaunch is a UWP app model concept and no desktop app has it.
        /// </summary>
        protected partial bool IsPrelaunch(LaunchActivatedEventArgs e)
        {
            return false;
        }

        /// <summary>
        /// Settable here, as on UWP and unlike on the island host - but only until the first window
        /// has content, which is why the caller assigns it before InitializeFrame rather than
        /// whenever the theme changes.
        /// </summary>
        protected partial void SetApplicationTheme(ApplicationTheme theme)
        {
            RequestedTheme = theme;
        }

        private partial WindowContext ResolveWindowContext(IActivatedEventArgs e)
        {
            // A share gets a window of its own, every time. UWP had the system make a separate
            // view for it - handing a share to the main window would replace the user's chat list
            // with the share UI - and two shares are two independent operations, so the second
            // must not land on top of the first.
            if (e is ShareTargetActivatedEventArgs)
            {
                return CreateWindow("Share");
            }

            // Everything else belongs to the main window: a launch, a tg: link, a toast, a file.
            // Main rather than Current, deliberately - Current is [ThreadStatic] and answers for
            // whichever thread asks, and this host puts every window on one thread anyway.
            var context = WindowContext.Main;
            if (context != null)
            {
                return context;
            }

            // The UWP half does this in OnWindowCreated, which this framework does not have.
            // Without it every {CustomResource} in the app's markup is a XamlParseException: "No
            // custom resource loader set", which surfaces as a Frame that reports a successful
            // navigation and shows nothing.
            //
            // Once per process, not once per window. The second one fails inside the factory, and
            // a failed HR thrown out of the notification icon's WndProc - which is where a reopen
            // comes from - is a fail-fast rather than an exception.
            CustomXamlResourceLoader.Current ??= new XamlResourceLoader();

            return CreateWindow("Main");
        }

        /// <summary>
        /// Content stays null: InitializeFrame sets it immediately after, through the same
        /// WindowContext.Content path the other two halves use, so the XamlRoot appears there too.
        ///
        /// Neither window this makes has a PersistedId of its own - nothing opens either by name -
        /// so both are given one here. They must not be the same: a share is a separate window
        /// with its own size, and sharing a slot would have it overwrite where the main window
        /// sits every time something is shared.
        /// </summary>
        private static WindowContext CreateWindow(string persistedId)
        {
            Logger.Info("Creating the window context");

            var window = new Window
            {
                Title = "Telegram"
            };

            // Logical pixels in, physical out: AppWindow measures in raw pixels, and these two are
            // the same numbers the UWP flavour hands to SetPreferredMinSize.
            var scale = Win32.GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(window)) / 96.0;
            window.AppWindow.Resize(new Windows.Graphics.SizeInt32(
                (int)(DefaultWidth * scale),
                (int)(DefaultHeight * scale)));

            return new WindowContext(window);
        }
    }
}
