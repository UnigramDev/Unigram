//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading.Tasks;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Telegram.Common;
using Telegram.Host;
using Telegram.Navigation;
using Windows.Foundation;
using Windows.Graphics;

namespace Telegram.Services
{
    /// <summary>
    /// The WinUI 3 half: a secondary window is another <c>Window</c>, on this same thread. There is
    /// no <c>CoreApplication.CreateNewView</c>, no view id and no <c>ViewLifetimeControl</c> - the
    /// framework supports many windows per thread, which is the whole reason this flavour exists.
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

            return await OnUIThread(() =>
            {
                var context = CreateWindow(options.Title, options.Width, options.Height, options.PersistedId, options.ViewMode);
                context.PersistedId = options.PersistedId ?? string.Empty;
                context.Content = options.Content(context);
                context.Activate();

                return context;
            });
        }

        public async Task<WindowContext> OpenAsync(ISession session, Type page, object parameter = null, string title = null, Size size = default, string id = "0")
        {
            Logger.Info($"Page: {page}, Parameter: {parameter}, Title: {title}, Size: {size}");

            // The same rule as the other two halves: a chat already open in a secondary window is
            // brought forward rather than opened twice.
            WindowContext oldWindow = null;
            await WindowContext.ForEachAsync(window =>
            {
                if (window.IsInMainView)
                {
                    return;
                }

                if (parameter is long chatId && window.RootNavigationService is { } service && service.IsChatOpen(chatId, true))
                {
                    oldWindow = window;
                }
            });

            if (oldWindow != null)
            {
                await oldWindow.Dispatcher.DispatchAsync(oldWindow.Activate);
                return oldWindow;
            }

            await _mainWindowCreated.Task;

            return await OnUIThread(() =>
            {
                var context = CreateWindow(title, size.Width, size.Height, "Floating", ViewServiceMode.Default);
                context.PersistedId = "Floating";

                var nav = BootStrapper.Current.NavigationServiceFactory(session, context, BootStrapper.BackButton.Ignore, id, false);
                nav.Navigate(page, parameter);

                context.Content = BootStrapper.Current.CreateRootElement(nav);
                context.Activate();

                return context;
            });
        }

        /// <summary>
        /// Callers are not all on the UI thread. A chat window is opened from a click; a call
        /// window is opened from a tgcalls or TDLib callback, on whatever thread that arrives on.
        /// A Window constructed off the UI thread lands on a thread with no DispatcherQueue, which
        /// is how joining a call took the island host down.
        /// </summary>
        private static Task<WindowContext> OnUIThread(Func<WindowContext> create)
        {
            var dispatcher = WindowContext.Main?.Dispatcher;
            if (dispatcher != null && !dispatcher.HasThreadAccess)
            {
                return dispatcher.DispatchAsync(create);
            }

            return Task.FromResult(create());
        }

        /// <summary>
        /// Content is set by the caller rather than passed in, so that it is assigned through
        /// <see cref="WindowContext.Content"/> - the same path the main window takes, and the one
        /// that builds the WindowPresenter, merges the chat theme and publishes the XamlRoot.
        ///
        /// CompactOverlay is honoured here, where the island host has to ignore it: it is an
        /// AppWindow presenter rather than a UWP app model feature, so the gallery's
        /// picture-in-picture window is one call instead of a topmost window the app positions.
        /// </summary>
        private static WindowContext CreateWindow(string title, double width, double height, string persistedId, ViewServiceMode mode)
        {
            var window = new Window
            {
                Title = title ?? string.Empty
            };

            var handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
            var scale = Win32.GetDpiForWindow(handle) / 96.0;

            window.AppWindow.Resize(new SizeInt32(
                (int)((width > 0 ? width : DefaultWidth) * scale),
                (int)((height > 0 ? height : DefaultHeight) * scale)));

            if (mode == ViewServiceMode.CompactOverlay)
            {
                window.AppWindow.SetPresenter(AppWindowPresenterKind.CompactOverlay);
            }
            else if (mode == ViewServiceMode.FullScreen)
            {
                window.AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
            }

            return new WindowContext(window);
        }
    }
}
