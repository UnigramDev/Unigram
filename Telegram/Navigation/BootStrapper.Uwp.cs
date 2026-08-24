//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation.Services;
using Telegram.Services;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Resources;

namespace Telegram.Navigation
{
    /// <summary>
    /// The UWP half of <see cref="BootStrapper"/>: the window plumbing the app model drives.
    /// Everything else in the class - activation, navigation, back and forward, the lifetime
    /// handlers - compiles against either host unchanged, because <c>App</c> derives from
    /// <c>Application</c> in both. What differs is only that nothing calls these on Win32:
    /// there is no <c>Application.Start</c>, so the framework never raises them.
    /// </summary>
    public abstract partial class BootStrapper
    {
        protected override void OnWindowCreated(WindowCreatedEventArgs args)
        {
            Logger.Info();

            IsMainWindowCreated = true;
            //should be called to initialize and set new SynchronizationContext
            //if (!WindowWrapper.ActiveWrappers.Any())
            // handle window

            // Hook up the default Back handler
            // WARNING: this is used by Xbox (and some Windows users)
            SystemNavigationManager.GetForCurrentView().BackRequested += BackHandler;

            CustomXamlResourceLoader.Current = new XamlResourceLoader();
            CreateWindowWrapper(args.Window);
            ViewService.OnWindowCreated();

            args.Window.Activated += OnActivated;
            args.Window.Closed += OnClosed;
            base.OnWindowCreated(args);
        }

        private void OnActivated(object sender, Windows.UI.Core.WindowActivatedEventArgs e)
        {
            if (sender is Window)
            {
                // Current, and legitimately: this handler runs on the window's own thread, and a
                // UWP view has exactly one window on it. That is the assumption the Win32 half
                // cannot make, which is why the virtual now takes the context rather than a Window.
                OnWindowActivated(WindowContext.Current, e.WindowActivationState != CoreWindowActivationState.Deactivated);
            }
        }

        private void OnClosed(object sender, CoreWindowEventArgs e)
        {
            SystemNavigationManager.GetForCurrentView().BackRequested -= BackHandler;

            if (sender is Window window)
            {
                window.Activated -= OnActivated;
                window.Closed -= OnClosed;

                OnWindowClosed(window);
            }
        }

        protected virtual void OnWindowClosed(Window window)
        {

        }

        private WindowContext CreateWindowWrapper(Window window)
        {
            // WindowContext's constructor assigns the thread-static Current, so building a second
            // one for a view that already has one replaces it, and whatever was attached to the
            // first - the frame, most importantly - is silently orphaned. Activation can run
            // before OnWindowCreated, so the context may already exist by the time we get here.
            var context = WindowContext.Current;
            if (context != null)
            {
                if (context.CoreWindow == window.CoreWindow)
                {
                    Logger.Info("Window context already exists, reusing it");
                    return context;
                }

                Logger.Warning("Window context belongs to a different window, replacing it");
            }

            Logger.Info("Creating the window context");
            return new WindowContext(window);
        }

        protected partial bool IsPrelaunch(LaunchActivatedEventArgs e)
        {
            return e.PrelaunchActivated;
        }

        protected partial void SetApplicationTheme(ApplicationTheme theme)
        {
            RequestedTheme = theme;
        }

        private partial WindowContext EnsureWindowContext(IActivatedEventArgs e)
        {
            var context = WindowContext.Current;
            if (context != null)
            {
                return context;
            }

            if (Window.Current == null)
            {
                Logger.Error($"Activated with no window at all, cannot initialize the frame. Kind: {e.Kind}");
                return null;
            }

            Logger.Warning($"Activated before OnWindowCreated, creating the window context early. Kind: {e.Kind}");
            return CreateWindowWrapper(Window.Current);
        }
    }
}
