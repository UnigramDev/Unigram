//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Host;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Resources;

namespace Telegram.Navigation
{
    /// <summary>
    /// The Win32 half of <see cref="BootStrapper"/>. Far smaller than its UWP twin, because the
    /// app model that raises <c>OnWindowCreated</c> and hands over a <c>Window</c> does not exist
    /// here: nothing announces a window, so the host makes one.
    /// </summary>
    public abstract partial class BootStrapper
    {
        // Matches the UWP flavour's SetPreferredMinSize, which is what a new view opened at.
        private const int DefaultWidth = 1024;
        private const int DefaultHeight = 720;

        /// <summary>
        /// What Application.Start does on UWP: hand the process activation to the app. The
        /// overrides below it are sealed and the framework never raises them here, so the host
        /// calls them itself - with the real arguments, which a packaged app gets from
        /// AppInstance rather than having to invent.
        /// </summary>
        internal void Start(IActivatedEventArgs args)
        {
            if (args is LaunchActivatedEventArgs launch)
            {
                // Straight to the internals rather than through OnLaunched, which is inert here.
                WatchDog.Launch(launch.PreviousExecutionState);
                CallInternalLaunchAsync(launch);
            }
            else if (args != null)
            {
                CallInternalActivated(args);
            }
            else
            {
                // Unpackaged, or launched by something that provides no arguments at all. The app
                // has no way to build a LaunchActivatedEventArgs of its own, so this is as far as
                // the process can get - and identity is required well before this anyway.
                Logger.Error("No activation arguments; the app cannot be started without identity");
            }
        }

        /// <summary>
        /// Never: nothing prelaunches a desktop process.
        /// </summary>
        /// <summary>
        /// Deliberately empty, and Terminal's App does the same: on this host the framework's
        /// launch is not the one that matters. `WindowsXamlManager.InitializeForCurrentThread`
        /// raises one by itself, and so does joining a call - re-entrantly, on another thread.
        /// <see cref="Start"/> drives the real one, with the arguments AppInstance gives it.
        /// </summary>
        protected sealed override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Logger.Info($"Ignoring a launch this host did not ask for. Previous: {e.PreviousExecutionState}");
        }

        protected partial bool IsPrelaunch(LaunchActivatedEventArgs e)
        {
            return false;
        }

        /// <summary>
        /// Deliberately empty. Setting Application.RequestedTheme fails fast inside an island -
        /// the property is only settable during the app model startup this host never performs -
        /// and it is not needed: every island root gets its theme from
        /// NightModeService.GetCalculatedElementTheme in WindowContext.SetContent.
        /// </summary>
        protected partial void SetApplicationTheme(ApplicationTheme theme)
        {
        }

        private partial WindowContext ResolveWindowContext(IActivatedEventArgs e)
        {
            // A share gets a window of its own, every time. UWP had the system make a separate
            // view for it - handing a share to the main window would replace the user's chat list
            // with the share UI - and two shares are two independent operations, so the second
            // must not land on top of the first.
            if (e is ShareTargetActivatedEventArgs)
            {
                return CreateWindow();
            }

            // Everything else belongs to the main window: a launch, a tg: link, a toast, a file.
            // Main rather than Current, deliberately - Current is [ThreadStatic] and a framework
            // activation does not arrive on the UI thread, so it answers null there and this used
            // to build an island on a thread with no WindowsXamlManager, which is a fail-fast.
            var context = WindowContext.Main;
            if (context != null)
            {
                return context;
            }

            // The UWP half does this in OnWindowCreated, which the framework raises and this host
            // does not - so it was stranded there by the split. Without it every {CustomResource}
            // in the app's markup is a XamlParseException: "No custom resource loader set", which
            // surfaces as a Frame that reports a successful navigation and shows nothing.
            CustomXamlResourceLoader.Current = new XamlResourceLoader();

            return CreateWindow();
        }

        /// <summary>
        /// Content stays null: InitializeFrame sets it immediately after, through the same
        /// WindowContext.Content path the UWP half uses, so the XamlRoot appears there too.
        /// </summary>
        private static WindowContext CreateWindow()
        {
            Logger.Info("Creating the window context");

            var island = IslandWindow.Create("Telegram",
                Win32.CW_USEDEFAULT, Win32.CW_USEDEFAULT, DefaultWidth, DefaultHeight,
                null, nonClient: true);

            return new WindowContext(island);
        }
    }
}
