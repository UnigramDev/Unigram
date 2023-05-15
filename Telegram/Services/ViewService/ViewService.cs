//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
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
        public bool IsSupported { get; }

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

        public string PersistentId { get; set; }
    }

    public sealed class ViewService : IViewService
    {
        private static readonly TaskCompletionSource<bool> _mainWindowCreated = new();

        internal static void OnWindowCreated()
        {
            _mainWindowCreated.TrySetResult(true);

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

        public bool IsSupported => true;

        public Task<ViewLifetimeControl> OpenAsync(ViewServiceParams parameters)
        {
            if (IsSupported)
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
                _ = CoreApplication.MainView.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                {
                    if (Window.Current.Content is RootPage root)
                    {
                        root.PresentContent(parameters.Content(null));
                        await ApplicationViewSwitcher.TryShowAsStandaloneAsync(ApplicationView.GetForCurrentView().Id);
                    }
                });
                return null;
            }
        }

        private async Task<ViewLifetimeControl> OpenAsyncInternal(ViewServiceParams parameters)
        {
            await _mainWindowCreated.Task;

            var newView = CoreApplication.CreateNewView();
            var dispatcher = new DispatcherContext(newView.DispatcherQueue);

            var newControl = await dispatcher.DispatchAsync(async () =>
            {
                var newWindow = Window.Current;
                var newAppView = ApplicationView.GetForCurrentView();

                newAppView.Title = parameters.Title ?? string.Empty;
                newAppView.PersistedStateId = parameters.PersistentId ?? string.Empty;

                var control = ViewLifetimeControl.GetForCurrentView();
                control.Released += (s, args) =>
                {
                    newWindow.Close();
                };

                newWindow.Content = parameters.Content(control);
                newWindow.Activate();

                var preferences = ViewModePreferences.CreateDefault(parameters.ViewMode);
                if (parameters.Width != 0 && parameters.Height != 0)
                {
                    preferences.CustomSize = new Size(parameters.Width, parameters.Height);
                }

                await ApplicationViewSwitcher.TryShowAsViewModeAsync(newAppView.Id, parameters.ViewMode, preferences);
                newAppView.TryResizeView(preferences.CustomSize);

                return control;
            }).ConfigureAwait(false);

            return newControl;
        }

        public async Task<ViewLifetimeControl> OpenAsync(Type page, object parameter = null, string title = null,
            Size size = default, int session = 0, string id = "0")
        {
            Logger.Info($"Page: {page}, Parameter: {parameter}, Title: {title}, Size: {size}");

            var currentView = ApplicationView.GetForCurrentView();
            title ??= currentView.Title;









            if (parameter != null && _windows.TryGetValue(parameter, out IDispatcherContext value))
            {
                var newControl = await value.DispatchAsync(async () =>
                {
                    var control = ViewLifetimeControl.GetForCurrentView();
                    var newAppView = ApplicationView.GetForCurrentView();

                    await ApplicationViewSwitcher
                        .SwitchAsync(newAppView.Id, currentView.Id, ApplicationViewSwitchingOptions.Default);

                    return control;
                }).ConfigureAwait(false);
                return newControl;
            }
            else
            {
                //if (ApiInformation.IsPropertyPresent("Windows.UI.ViewManagement.ApplicationView", "PersistedStateId"))
                //{
                //    try
                //    {
                //        ApplicationView.ClearPersistedState("Calls");
                //    }
                //    catch { }
                //}
                await _mainWindowCreated.Task;

                var newView = CoreApplication.CreateNewView();
                var dispatcher = new DispatcherContext(newView.DispatcherQueue);

                if (parameter != null)
                {
                    _windows[parameter] = dispatcher;
                }

                var bounds = Window.Current.Bounds;

                var newControl = await dispatcher.DispatchAsync(async () =>
                {
                    var newWindow = Window.Current;
                    var newAppView = ApplicationView.GetForCurrentView();
                    newAppView.Title = title;
                    newAppView.PersistedStateId = "Floating";

                    var control = ViewLifetimeControl.GetForCurrentView();
                    control.Released += (s, args) =>
                    {
                        if (parameter != null)
                        {
                            _windows.TryRemove(parameter, out IDispatcherContext _);
                        }

                        //newWindow.Close();
                    };

                    var nav = BootStrapper.Current.NavigationServiceFactory(BootStrapper.BackButton.Ignore, BootStrapper.ExistingContent.Exclude, session, id, false);
                    nav.Navigate(page, parameter);
                    newWindow.Content = BootStrapper.Current.CreateRootElement(nav);
                    newWindow.Activate();

                    await ApplicationViewSwitcher
                        .TryShowAsStandaloneAsync(newAppView.Id, ViewSizePreference.Default, currentView.Id, ViewSizePreference.UseHalf);
                    //newAppView.TryResizeView(new Windows.Foundation.Size(360, bounds.Height));
                    newAppView.TryResizeView(size);

                    return control;
                }).ConfigureAwait(false);
                return newControl;
            }
        }

        private readonly ConcurrentDictionary<object, IDispatcherContext> _windows = new ConcurrentDictionary<object, IDispatcherContext>();
    }
}
