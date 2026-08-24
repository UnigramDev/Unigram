//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Host;
using Telegram.Navigation;
using Windows.Foundation;

namespace Telegram.Services
{
    /// <summary>
    /// The Win32 half: a secondary window is another HWND with another island in it, on this same
    /// thread. There is no <c>CoreApplication.CreateNewView</c>, no view id and no
    /// <c>ViewLifetimeControl</c> - gate 1.8a established that many islands share a thread quite
    /// happily, and item 0.18 that only the chat theme was ever per window, so nothing here needs
    /// a thread of its own.
    /// </summary>
    public sealed partial class ViewService
    {
        // What a secondary window opens at when the caller asks for nothing in particular. The UWP
        // side leaves this to ApplicationView; here it has to be said out loud.
        private const int DefaultWidth = 384;
        private const int DefaultHeight = 640;

        public async Task<WindowContext> OpenAsync(ViewServiceOptions options)
        {
            await _mainWindowCreated.Task;

            // ViewMode is deliberately ignored. CompactOverlay is a UWP app model feature with no
            // Win32 equivalent, and picture-in-picture has to become a small topmost window the app
            // positions itself - Tier 2 of the fork list, and not this item's problem.
            var context = CreateWindow(options.Title, options.Width, options.Height);
            context.PersistedId = options.PersistedId ?? string.Empty;
            context.Content = options.Content(context);
            context.Activate();

            return context;
        }

        public async Task<WindowContext> OpenAsync(ISession session, Type page, object parameter = null, string title = null, Size size = default, string id = "0")
        {
            Logger.Info($"Page: {page}, Parameter: {parameter}, Title: {title}, Size: {size}");

            // The same rule as the UWP half: a chat already open in a secondary window is brought
            // forward rather than opened twice.
            WindowContext oldWindow = null;
            await WindowContext.ForEachAsync(window =>
            {
                if (window.IsInMainView)
                {
                    return;
                }

                foreach (var service in window.NavigationServices)
                {
                    if (parameter is long chatId && service.IsChatOpen(chatId, true))
                    {
                        oldWindow = window;
                        return;
                    }
                }
            });

            if (oldWindow != null)
            {
                oldWindow.Activate();
                return oldWindow;
            }

            await _mainWindowCreated.Task;

            var context = CreateWindow(title, size.Width, size.Height);
            context.PersistedId = "Floating";

            var nav = BootStrapper.Current.NavigationServiceFactory(session, context, BootStrapper.BackButton.Ignore, id, false);
            nav.Navigate(page, parameter);

            context.Content = BootStrapper.Current.CreateRootElement(nav);
            context.Activate();

            return context;
        }

        /// <summary>
        /// Content is set by the caller rather than passed in, so that it is assigned through
        /// <see cref="WindowContext.Content"/> - the same path the main window takes, and the one
        /// that builds the WindowControl, merges the chat theme and publishes the XamlRoot.
        /// </summary>
        private static WindowContext CreateWindow(string title, double width, double height)
        {
            var island = IslandWindow.Create(title ?? string.Empty,
                Win32.CW_USEDEFAULT, Win32.CW_USEDEFAULT,
                (int)(width > 0 ? width : DefaultWidth),
                (int)(height > 0 ? height : DefaultHeight),
                null, nonClient: true);

            return new WindowContext(island);
        }
    }
}
