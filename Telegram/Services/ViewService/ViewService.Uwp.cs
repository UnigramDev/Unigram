//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.UI.ViewManagement;

namespace Telegram.Services
{
    /// <summary>
    /// The UWP half: a secondary window is a CoreApplication view, on its own thread, switched
    /// to through ApplicationViewSwitcher and kept alive by ViewLifetimeControl. None of that
    /// exists on the Win32 host, which just makes another island - see gate 1.8a, which is what
    /// established that many islands can share one thread.
    /// </summary>
    public sealed partial class ViewService
    {
        internal static void OnWindowCreated()
        {
            var view = CoreApplication.GetCurrentView();
            if (!view.IsMain && !view.IsHosted)
            {
                var control = ViewLifetimeControl.GetForCurrentView();
                //This one time it should be made manually, as after Consolidate event fires the inner reference number should become zero
                control.StartViewInUse();

#if NET9_0_OR_GREATER
                var context = new global::Windows.System.DispatcherQueueSynchronizationContext(global::Windows.System.DispatcherQueue.GetForCurrentThread());
#else
                var context = SynchronizationContext.Current;
#endif
                //This is necessary to not make control.StartViewInUse()/control.StopViewInUse() manually on each and every async call. Facade will do it for you
                SynchronizationContext.SetSynchronizationContext(new SecondaryViewSynchronizationContextDecorator(control, context));
            }
        }

        public Task<WindowContext> OpenAsync(ViewServiceOptions options)
        {
            if (ApiInfo.HasMultipleViews)
            {
                try
                {
                    return OpenAsyncInternal(options);
                }
                catch (Exception ex)
                {
                    // This can happen, but it's unclear when
                    Logger.Exception(ex);

                    // All the remote procedure calls must be wrapped in a try-catch block
                    return Task.FromResult<WindowContext>(null);
                }
            }
            else
            {
                return FacadeAsync(options);
            }
        }

        private async Task<WindowContext> FacadeAsync(ViewServiceOptions options)
        {
            var tsc = new TaskCompletionSource<WindowContext>();

            await CoreApplication.MainView.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                tsc.SetResult(WindowContext.Current);
            });

            return await tsc.Task;
        }

        private async Task<WindowContext> OpenAsyncInternal(ViewServiceOptions options)
        {
            await _mainWindowCreated.Task;

            try
            {
                // Throws when called while suspending or resuming:
                // https://devblogs.microsoft.com/oldnewthing/20210920-00/?p=105711
                var newView = CoreApplication.CreateNewView();
                var tsc = new TaskCompletionSource<WindowContext>();

                newView.DispatcherQueue.TryEnqueue(() =>
                {
                    var newWindow = WindowContext.Current;
                    var newAppView = ApplicationView.GetForCurrentView();

                    newAppView.Title = options.Title ?? string.Empty;
                    newWindow.PersistedId = options.PersistedId ?? string.Empty;

                    newWindow.Content = options.Content(newWindow);
                    newWindow.Activate();

                    tsc.SetResult(newWindow);

                    Logger.Info(newWindow.Content?.GetType());
                });

                var newWindow = await tsc.Task;
                var viewMode = options.ViewMode switch
                {
                    ViewServiceMode.CompactOverlay => ApplicationViewMode.CompactOverlay,
                    _ => ApplicationViewMode.Default
                };

                var preferences = ViewModePreferences.CreateDefault(viewMode);
                if (options.Width != 0 && options.Height != 0)
                {
                    preferences.CustomSize = new Size(options.Width, options.Height);
                    preferences.ViewSizePreference = ViewSizePreference.Custom;
                }

                await ApplicationViewSwitcher.TryShowAsViewModeAsync(newWindow.Id, viewMode, preferences);

                if (options.ViewMode == ViewServiceMode.FullScreen)
                {
                    newView.DispatcherQueue.TryEnqueue(() => ApplicationView.GetForCurrentView().TryEnterFullScreenMode());
                }
                else if (options.Width != 0 && options.Height != 0)
                {
                    newView.DispatcherQueue.TryEnqueue(() => ApplicationView.GetForCurrentView().TryResizeView(new Size(options.Width, options.Height)));
                }

                return newWindow;
            }
            catch
            {
                return null;
            }
        }

        public async Task<WindowContext> OpenAsync(ISession session, Type page, object parameter = null, string title = null, Size size = default, string id = "0")
        {
            Logger.Info($"Page: {page}, Parameter: {parameter}, Title: {title}, Size: {size}");

            var currentView = ApplicationView.GetForCurrentView();
            title ??= currentView.Title;

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
                await ApplicationViewSwitcher.SwitchAsync(oldWindow.Id);
                return oldWindow;
            }

            await _mainWindowCreated.Task;

            try
            {
                // Throws when called while suspending or resuming:
                // https://devblogs.microsoft.com/oldnewthing/20210920-00/?p=105711
                var newView = CoreApplication.CreateNewView();
                var tsc = new TaskCompletionSource<WindowContext>();

                newView.DispatcherQueue.TryEnqueue(() =>
                {
                    var newWindow = WindowContext.Current;
                    var newAppView = ApplicationView.GetForCurrentView();

                    newAppView.Title = title;
                    newWindow.PersistedId = "Floating";

                    var nav = BootStrapper.Current.NavigationServiceFactory(session, newWindow, BootStrapper.BackButton.Ignore, id, false);
                    nav.Navigate(page, parameter);

                    newWindow.Content = BootStrapper.Current.CreateRootElement(nav);
                    newWindow.Activate();

                    tsc.SetResult(newWindow);
                });

                var newWindow = await tsc.Task;

                await ApplicationViewSwitcher.TryShowAsStandaloneAsync(newWindow.Id);
                return newWindow;
            }
            catch
            {
                return null;
            }
        }
    }
}
