//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Views.Host;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;

namespace Telegram.Services.ViewService
{
    public interface IViewService
    {
        ///<summary>
        /// Creates and opens new secondary view        
        /// </summary>
        /// <param name="page">Type of page to automatically navigate</param>
        /// <param name="parameter">Parameter that will be passed to NavigationService with the page</param>
        /// <param name="title">Title that will be displayed for new view. If <code>null</code> - current view's title will be used</param>
        /// <param name="size">Anchor size for newly created view</param>        
        /// <returns><see cref="ViewLifetimeControl"/> object that is associated to newly created view. Use it to subscribe to <code>Released</code> event to close window manually.
        /// It won't not be called before all previously started async operations on <see cref="CoreDispatcher"/> complete. <remarks>DO NOT call operations on Dispatcher after this</remarks></returns>
        Task<ViewLifetimeControl> OpenAsync(Type page, object parameter = null, string title = null, Size size = default, int session = 0, string id = "0");

        Task<ViewLifetimeControl> OpenAsync(ViewServiceParams parameters);
    }

    public class ViewServiceParams
    {
        public ApplicationViewMode ViewMode { get; set; } = ApplicationViewMode.Default;

        public string Title { get; set; }

        public double Width { get; set; }
        public double Height { get; set; }

        public Func<ViewLifetimeControl, UIElement> Content { get; set; }

        public string PersistedId { get; set; }
    }

    public sealed class ViewService : IViewService
    {
        private static readonly TaskCompletionSource<bool> _mainWindowCreated = new();

        internal static void OnWindowCreated()
        {
            var view = CoreApplication.GetCurrentView();
            if (!view.IsMain && !view.IsHosted)
            {
                var control = ViewLifetimeControl.GetForCurrentView();
                //This one time it should be made manually, as after Consolidate event fires the inner reference number should become zero
                control.StartViewInUse();
                //This is necessary to not make control.StartViewInUse()/control.StopViewInUse() manually on each and every async call. Facade will do it for you
                SynchronizationContext.SetSynchronizationContext(new SecondaryViewSynchronizationContextDecorator(control,
                    SynchronizationContext.Current));
            }
        }

        internal static void OnWindowLoaded()
        {
            _mainWindowCreated.TrySetResult(true);
        }

        public Task<ViewLifetimeControl> OpenAsync(ViewServiceParams parameters)
        {
            if (ApiInfo.HasMultipleViews)
            {
                try
                {
                    return OpenAsyncInternal(parameters);
                }
                catch (Exception ex)
                {
                    // This can happen, but it's unclear when
                    Logger.Error(ex);

                    // All the remote procedure calls must be wrapped in a try-catch block
                    return Task.FromResult<ViewLifetimeControl>(null);
                }
            }
            else
            {
                return FacadeAsync(parameters);
            }
        }

        private async Task<ViewLifetimeControl> FacadeAsync(ViewServiceParams parameters)
        {
            var tsc = new TaskCompletionSource<ViewLifetimeControl>();

            await CoreApplication.MainView.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                if (Window.Current.Content is RootPage root)
                {
                    root.PresentContent(parameters.Content(null));
                    await ApplicationViewSwitcher.TryShowAsStandaloneAsync(ApplicationView.GetForCurrentView().Id);
                }

                tsc.SetResult(ViewLifetimeControl.Facade());
            });

            return await tsc.Task;
        }

        private async Task<ViewLifetimeControl> OpenAsyncInternal(ViewServiceParams parameters)
        {
            await _mainWindowCreated.Task;

            try
            {
                // Throws when called while suspending or resuming:
                // https://devblogs.microsoft.com/oldnewthing/20210920-00/?p=105711
                var newView = CoreApplication.CreateNewView();
                var tsc = new TaskCompletionSource<ViewLifetimeControl>();

                newView.DispatcherQueue.TryEnqueue(() =>
                {
                    var newWindow = WindowContext.Current;
                    var newAppView = ApplicationView.GetForCurrentView();

                    newAppView.Title = parameters.Title ?? string.Empty;
                    newWindow.PersistedId = parameters.PersistedId ?? string.Empty;

                    var control = ViewLifetimeControl.GetForCurrentView();
                    newWindow.Content = parameters.Content(control);
                    newWindow.Activate();

                    tsc.SetResult(control);
                });

                var control = await tsc.Task;

                var preferences = ViewModePreferences.CreateDefault(parameters.ViewMode);
                if (parameters.Width != 0 && parameters.Height != 0)
                {
                    preferences.CustomSize = new Size(parameters.Width, parameters.Height);
                    preferences.ViewSizePreference = ViewSizePreference.Custom;
                }

                await ApplicationViewSwitcher.TryShowAsViewModeAsync(control.Id, parameters.ViewMode, preferences);

                if (parameters.Width != 0 && parameters.Height != 0)
                {
                    newView.DispatcherQueue.TryEnqueue(() => ApplicationView.GetForCurrentView().TryResizeView(new Size(parameters.Width, parameters.Height)));
                }

                return control;
            }
            catch
            {
                return null;
            }
        }

        public async Task<ViewLifetimeControl> OpenAsync(Type page, object parameter = null, string title = null,
            Size size = default, int session = 0, string id = "0")
        {
            Logger.Info($"Page: {page}, Parameter: {parameter}, Title: {title}, Size: {size}");

            var currentView = ApplicationView.GetForCurrentView();
            title ??= currentView.Title;

            ViewLifetimeControl oldControl = null;
            await WindowContext.ForEachAsync(window =>
            {
                if (window.IsInMainView)
                {
                    return Task.CompletedTask;
                }

                foreach (var service in window.NavigationServices)
                {
                    if (parameter is long chatId && service.IsChatOpen(chatId, true))
                    {
                        oldControl = ViewLifetimeControl.GetForCurrentView();
                        return Task.CompletedTask;
                    }
                }

                return Task.CompletedTask;
            });

            if (oldControl != null)
            {
                await ApplicationViewSwitcher.SwitchAsync(oldControl.Id);
                return oldControl;
            }

            await _mainWindowCreated.Task;

            try
            {
                // Throws when called while suspending or resuming:
                // https://devblogs.microsoft.com/oldnewthing/20210920-00/?p=105711
                var newView = CoreApplication.CreateNewView();
                var tsc = new TaskCompletionSource<ViewLifetimeControl>();

                newView.DispatcherQueue.TryEnqueue(() =>
                {
                    var newWindow = WindowContext.Current;
                    var newAppView = ApplicationView.GetForCurrentView();

                    newAppView.Title = title;
                    newWindow.PersistedId = "Floating";

                    var nav = BootStrapper.Current.NavigationServiceFactory(BootStrapper.BackButton.Ignore, session, id, false);
                    nav.Navigate(page, parameter);

                    var control = ViewLifetimeControl.GetForCurrentView();
                    newWindow.Content = BootStrapper.Current.CreateRootElement(nav);
                    newWindow.Activate();

                    tsc.SetResult(control);
                });

                var control = await tsc.Task;

                await ApplicationViewSwitcher.TryShowAsStandaloneAsync(control.Id);
                return control;
            }
            catch
            {
                return null;
            }
        }
    }
}
