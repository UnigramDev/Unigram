using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unigram.Services.ViewService;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;

namespace Unigram.Navigation.Services
{
    public interface INavigationService
    {
        void GoBack(NavigationTransitionInfo infoOverride = null);
        void GoForward();

        object Content { get; }

        bool Navigate(Type page, object parameter = null, IDictionary<string, object> state = null, NavigationTransitionInfo infoOverride = null);
        bool Navigate<T>(T key, object parameter = null, IDictionary<string, object> state = null, NavigationTransitionInfo infoOverride = null) where T : struct, IConvertible;

        bool CanGoBack { get; }
        bool CanGoForward { get; }

        string NavigationState { get; set; }

        void Refresh();

        void Refresh(object param);



        Task<ViewLifetimeControl> OpenAsync(Type page, object parameter = null, string title = null, ViewSizePreference size = ViewSizePreference.UseHalf);

        object CurrentPageParam { get; }
        Type CurrentPageType { get; }

        DispatcherWrapper Dispatcher { get; }

        Task SaveAsync();

        Task<bool> LoadAsync();

        event TypedEventHandler<INavigationService, Type> AfterRestoreSavedNavigation;

        void ClearHistory();
        void ClearCache(bool removeCachedPagesInBackStack = false);

        Task SuspendingAsync();
        void Resuming();

        Frame Frame { get; }
        FrameFacade FrameFacade { get; }

        int SessionId { get; }

        /// <summary>
        /// Specifies if this instance of INavigationService associated with <see cref="CoreApplication.MainView"/> or any other secondary view.
        /// </summary>
        /// <returns><value>true</value> if associated with MainView, <value>false</value> otherwise</returns>
        bool IsInMainView { get; }
    }
}