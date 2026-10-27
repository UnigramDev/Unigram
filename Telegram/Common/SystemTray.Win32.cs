//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Telegram.Navigation;
using static Telegram.Common.NotifyIcon;

namespace Telegram.Common
{
    /// <summary>
    /// The notification area icon, in process.
    ///
    /// The icon itself is <see cref="NotifyIcon"/>, the same class Telegram.Stub draws for the UWP
    /// flavour. What the app service used to carry - the unread count in, open and exit out - is a
    /// method call and two events here, so the bridge, the app service and the second process all
    /// fall away on this host.
    /// </summary>
    public static partial class SystemTray
    {
        private static NotifyIcon _icon;
        private static DispatcherQueue _queue;

        public static partial bool IsShowing()
        {
            return _icon != null;
        }

        public static partial Task ShowAsync()
        {
            // The message window belongs to the thread that creates it, and only a thread that
            // pumps messages ever hears a click - so never a pool thread, which is where
            // App.OnStartSync runs. Created there, the icon ignores clicks until something happens
            // to pump that thread, and then activates the window from it and dies in the WndProc.
            if (WindowContext.Main?.Dispatcher is IDispatcherContext dispatcher)
            {
                return dispatcher.DispatchAsync(Show);
            }

            if (DispatcherQueue.GetForCurrentThread() == null)
            {
                Logger.Warning("No UI thread to create the notification area icon on");
                return Task.CompletedTask;
            }

            Show();
            return Task.CompletedTask;
        }

        private static void Show()
        {
            if (_icon == null)
            {
                // The thread the icon's message window is created on, which is the one its WndProc
                // runs on: the click has to come back to it, and it must not run inside it.
                _queue = DispatcherQueue.GetForCurrentThread();

                _icon = new NotifyIcon(ResolveIcon, Strings.AppName);
                _icon.Click += OnClick;
                _icon.Exit += OnExit;
                _icon.SessionEnding += OnSessionEnding;
                _icon.SessionEndCancelled += OnSessionEndCancelled;

                _icon.UpdateOpenText(Strings.NotifyIconOpen);
                _icon.UpdateExitText(Strings.NotifyIconExit);
            }
        }

        public static partial Task HideAsync()
        {
            if (_icon != null)
            {
                _icon.Click -= OnClick;
                _icon.Exit -= OnExit;
                _icon.SessionEnding -= OnSessionEnding;
                _icon.SessionEndCancelled -= OnSessionEndCancelled;
                _icon.Dispose();
                _icon = null;
            }

            return Task.CompletedTask;
        }

        public static partial void SetUnreadCount(int unreadCount, int unreadUnmutedCount)
        {
            if (_icon != null)
            {
                // Unmuted wins: an unmuted chat is the one worth colouring the icon for. The same
                // rule the stub applies on the other side of the bridge.
                _icon.Icon = unreadCount > 0
                    ? unreadUnmutedCount > 0 ? NotifyIconIcon.Unmuted : NotifyIconIcon.Muted
                    : NotifyIconIcon.Default;
            }
        }

        // The stub has these three as resources of its own executable; the app host carries only
        // one icon, so they ship as files beside it instead.
        private static IntPtr ResolveIcon(NotifyIconIcon icon)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Tray", icon + ".ico");

            // Handed over as a pointer: runtime marshalling is off, so nothing marshals a string
            // on our behalf.
            var buffer = Marshal.StringToHGlobalUni(path);

            try
            {
                return NativeMethods.LoadImage(IntPtr.Zero, buffer, 1 /* IMAGE_ICON */, 0, 0,
                    0x00000010 /* LR_LOADFROMFILE */ | 0x00000040 /* LR_DEFAULTSIZE */);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        /// <summary>
        /// Windows is ending the session, so the icon stops holding the app open: the window's
        /// close path hides the main window instead of closing it, and hiding rather than closing
        /// during a shutdown is what holds the shutdown up - and what leaves a package update with
        /// a process it cannot replace.
        ///
        /// A flag rather than taking the icon away, because the end can still be called off and
        /// the icon has to survive that. Nothing here waits either: this arrives inside
        /// WM_QUERYENDSESSION, where the session is being held for an answer.
        /// </summary>
        public static bool IsSessionEnding { get; private set; }

        private static void OnSessionEnding(object sender, EventArgs e)
        {
            IsSessionEnding = true;
        }

        private static void OnSessionEndCancelled(object sender, EventArgs e)
        {
            IsSessionEnding = false;
        }

        private static void OnClick(object sender, EventArgs e)
        {
            Logger.Info();

            // Posted, never called from here: this arrives inside the icon's WndProc, which is a
            // nested message dispatch on the UI thread, and showing a window from inside one is
            // the same trap that makes TabbedWindow close its tabs on the next turn. A second
            // click landing while the first is still showing the window is what makes it a
            // double click, and what made it a crash.
            //
            // Resolved here as well as in Show, and called directly if there is still no
            // queue: this runs on the thread that owns the icon either way, so the worst case is
            // the old behaviour rather than a click that does nothing.
            var queue = _queue ??= DispatcherQueue.GetForCurrentThread();

            if (queue != null && queue.TryEnqueue(() => BootStrapper.Current.ActivateFromNotifyIcon()))
            {
                return;
            }

            Logger.Warning("No dispatcher for the notification area icon");
            BootStrapper.Current.ActivateFromNotifyIcon();
        }

        private static async void OnExit(object sender, EventArgs e)
        {
            await HideAsync();
            await BootStrapper.ConsolidateAsync();
        }
    }
}
