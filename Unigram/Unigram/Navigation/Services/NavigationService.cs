using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Services.ViewService;
using Unigram.Views;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Navigation.Services
{
    public interface INavigationService
    {
        void GoBack(NavigationTransitionInfo infoOverride = null);
        void GoForward();

        object Content { get; }

        bool Navigate(Type page, object parameter = null, IDictionary<string, object> state = null, NavigationTransitionInfo infoOverride = null);

        bool CanGoBack { get; }
        bool CanGoForward { get; }

        string NavigationState { get; set; }

        IDictionary<string, long> CacheKeyToChatId { get; }

        void Refresh();

        void Refresh(object param);



        Task<ViewLifetimeControl> OpenAsync(Type page, object parameter = null, string title = null, ViewSizePreference size = ViewSizePreference.UseHalf);

        object CurrentPageParam { get; }
        Type CurrentPageType { get; }

        IDispatcherContext Dispatcher { get; }

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

    // DOCS: https://github.com/Windows-XAML/Template10/wiki/Docs-%7C-NavigationService
    public partial class NavigationService : INavigationService
    {
        private readonly IViewService viewService = new ViewService();
        public FrameFacade FrameFacade { get; }
        public bool IsInMainView { get; }
        public Frame Frame => FrameFacade.Frame;
        public object Content => Frame.Content;

        public IDispatcherContext Dispatcher => this.GetDispatcherWrapper();


        public string NavigationState
        {
            get { return Frame.GetNavigationState(); }
            set { Frame.SetNavigationState(value); }
        }

        public int SessionId { get; private set; }

        public IDictionary<string, long> CacheKeyToChatId { get; } = new Dictionary<string, long>();

        #region Debug

        [Conditional("DEBUG")]
        static void DebugWrite(string text = null, Unigram.Services.Logging.Severities severity = Unigram.Services.Logging.Severities.Template10, [CallerMemberName] string caller = null) =>
            Unigram.Services.Logging.LoggingService.WriteLine(text, severity, caller: $"NavigationService.{caller}");

        #endregion

        public static INavigationService GetForFrame(Frame frame) =>
            WindowContext.ActiveWrappers.SelectMany(x => x.NavigationServices).FirstOrDefault(x => x.FrameFacade.Frame.Equals(frame));

        public NavigationService(Frame frame, int session, string id)
        {
            IsInMainView = CoreApplication.MainView == CoreApplication.GetCurrentView();
            SessionId = session;
            FrameFacade = new FrameFacade(this, frame, id);
            FrameFacade.Navigating += async (s, e) =>
            {
                if (e.Suspending)
                    return;

                var page = FrameFacade.Content as Page;
                if (page != null)
                {
                    // call navagable override (navigating)
                    var dataContext = ResolveForPage(page);
                    if (dataContext != null)
                    {
                        // allow the viewmodel to cancel navigation
                        e.Cancel = !NavigatingFrom(page, e.SourcePageType, e.Parameter, dataContext, false, e.NavigationMode);
                        if (!e.Cancel)
                        {
                            await NavigateFromAsync(page, dataContext, false).ConfigureAwait(false);
                        }
                    }

                    if (page is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
            };
            FrameFacade.Navigated += async (s, e) =>
            {
                var parameter = e.Parameter;
                if (parameter is string cacheKey && e.SourcePageType == typeof(ChatPage))
                {
                    parameter = CacheKeyToChatId[cacheKey];
                }

                var currentContent = FrameFacade.Frame.Content;
                //await this.GetDispatcherWrapper().DispatchAsync(async () =>
                //{
                try
                {
                    if (currentContent == FrameFacade.Frame.Content)
                        await NavigateToAsync(e.NavigationMode, parameter, FrameFacade.Frame.Content).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    DebugWrite($"DispatchAsync/NavigateToAsync {ex.Message}");
                    throw;
                }
                //}, 1).ConfigureAwait(false);
            };
        }

        private INavigable ResolveForPage(Page page)
        {
            if (!(page.DataContext is INavigable) | page.DataContext == null)
            {
                // to support dependency injection, but keeping it optional.
                var viewModel = BootStrapper.Current.ResolveForPage(page, this);
                if (viewModel != null)
                {
                    page.DataContext = viewModel;
                    return viewModel;
                }
            }
            return page.DataContext as INavigable;
        }

        // before navigate (cancellable)
        bool NavigatingFrom(Page page, Type targetPageType, object targetPageParameter, INavigable dataContext, bool suspending, NavigationMode mode)
        {
            DebugWrite($"Suspending: {suspending}");

            dataContext.NavigationService = this;
            dataContext.Dispatcher = this.GetDispatcherWrapper();
            dataContext.SessionState = BootStrapper.Current.SessionState;

            var args = new NavigatingEventArgs
            {
                NavigationMode = mode,
                SourcePageType = FrameFacade.CurrentPageType,
                Parameter = FrameFacade.CurrentPageParam,
                Suspending = suspending,
                TargetPageType = targetPageType,
                TargetPageParameter = targetPageParameter
            };
            dataContext.OnNavigatingFrom(args);
            return !args.Cancel;
        }

        // after navigate
        async Task NavigateFromAsync(Page page, INavigable dataContext, bool suspending)
        {
            DebugWrite($"Suspending: {suspending}");

            dataContext.NavigationService = this;
            dataContext.Dispatcher = this.GetDispatcherWrapper();
            dataContext.SessionState = BootStrapper.Current.SessionState;

            var pageState = FrameFacade.PageStateSettingsService(page.GetType()).Values;
            await dataContext.OnNavigatedFromAsync(pageState, suspending).ConfigureAwait(false);
        }

        async Task NavigateToAsync(NavigationMode mode, object parameter, object frameContent = null)
        {
            DebugWrite($"Mode: {mode}, Parameter: {parameter} FrameContent: {frameContent}");

            frameContent = frameContent ?? FrameFacade.Frame.Content;

            var page = frameContent as Page;
            if (page != null)
            {
                var cleaned = false;
                if (page is IActivablePage cleanup)
                {
                    cleaned = true;
                    cleanup.Activate();
                }

                //if (mode == NavigationMode.New)
                //{
                //    var pageState = FrameFacadeInternal.PageStateSettingsService(page.GetType()).Values;
                //    pageState?.Clear();
                //}

                var dataContext = ResolveForPage(page);
                if (dataContext != null)
                {
                    // prepare for state load
                    dataContext.NavigationService = this;
                    dataContext.Dispatcher = this.GetDispatcherWrapper();
                    dataContext.SessionState = BootStrapper.Current.SessionState;
                    var pageState = FrameFacade.PageStateSettingsService(page.GetType(), parameter: parameter).Values;
                    await dataContext.OnNavigatedToAsync(parameter, mode, pageState);

                    // update bindings after NavTo initializes data
                    //XamlUtils.InitializeBindings(page);
                    if (page.Content is UserControl pageWith && !cleaned)
                    {
                        XamlUtils.UpdateBindings(pageWith);
                    }
                    else
                    {
                        XamlUtils.UpdateBindings(page);
                    }
                }
            }
        }

        public Task<ViewLifetimeControl> OpenAsync(Type page, object parameter = null, string title = null, ViewSizePreference size = ViewSizePreference.UseHalf)
        {
            DebugWrite($"Page: {page}, Parameter: {parameter}, Title: {title}, Size: {size}");
            return viewService.OpenAsync(page, parameter, title, size, SessionId);
        }

        public bool Navigate(Type page, object parameter = null, IDictionary<string, object> state = null, NavigationTransitionInfo infoOverride = null)
        {
            DebugWrite($"Page: {page}, Parameter: {parameter}, NavigationTransitionInfo: {infoOverride}");

            if (page == null)
                throw new ArgumentNullException(nameof(page));

            // use CurrentPageType/Param instead of LastNavigationType/Parameter to avoid new navigation to the current
            // page in some race conditions.
            if ((page.FullName == CurrentPageType?.FullName) && (parameter == CurrentPageParam))
                return false;

            if ((page.FullName == CurrentPageType?.FullName) && (parameter?.Equals(CurrentPageParam) ?? false))
                return false;

            if (state != null)
            {
                var pageState = FrameFacade.PageStateSettingsService(page, 1, parameter).Values;
                foreach (var item in state)
                {
                    pageState[item.Key] = item.Value;
                }
            }

            if (page == typeof(ChatPage))
            {
                var cacheKey = Guid.NewGuid().ToString();
                var chatId = (long)parameter;

                parameter = cacheKey;
                CacheKeyToChatId[cacheKey] = chatId;
            }

            return FrameFacade.Navigate(page, parameter, infoOverride);
        }

        public event EventHandler<CancelEventArgs<Type>> BeforeSavingNavigation;

        public async Task SaveAsync()
        {
            DebugWrite($"Frame: {FrameFacade.FrameId}");

            if (CurrentPageType == null)
                return;
            var args = new CancelEventArgs<Type>(FrameFacade.CurrentPageType);
            BeforeSavingNavigation?.Invoke(this, args);
            if (args.Cancel)
                return;

            var state = FrameFacade.PageStateSettingsService(GetType().ToString());
            if (state == null)
            {
                throw new InvalidOperationException("State container is unexpectedly null");
            }

            state.Write<string>("CurrentPageType", CurrentPageType.AssemblyQualifiedName);
            state.Write<object>("CurrentPageParam", CurrentPageParam);
            state.Write<string>("NavigateState", FrameFacade?.NavigationService.NavigationState);

            await Task.CompletedTask;
        }

        public event TypedEventHandler<INavigationService, Type> AfterRestoreSavedNavigation;


        public async Task<bool> LoadAsync()
        {
            DebugWrite($"Frame: {FrameFacade.FrameId}");

            try
            {
                var state = FrameFacade.PageStateSettingsService(GetType().ToString());
                if (state == null || !state.Exists("CurrentPageType"))
                {
                    return false;
                }

                FrameFacade.CurrentPageType = Type.GetType(state.Read<string>("CurrentPageType"));
                FrameFacade.CurrentPageParam = state.Read<object>("CurrentPageParam");
                FrameFacade.NavigationService.NavigationState = state.Read<string>("NavigateState");

                await NavigateToAsync(NavigationMode.Refresh, FrameFacade.CurrentPageParam);
                while (FrameFacade.Frame.Content == null)
                {
                    await Task.Delay(1);
                }
                AfterRestoreSavedNavigation?.Invoke(this, FrameFacade.CurrentPageType);
                return true;
            }
            catch { return false; }
        }

        public void Refresh() { FrameFacade.Refresh(); }
        public void Refresh(object param) { FrameFacade.Refresh(param); }

        public void GoBack(NavigationTransitionInfo infoOverride = null)
        {
            if (FrameFacade.CanGoBack) FrameFacade.GoBack(infoOverride);
        }

        public bool CanGoBack => FrameFacade.CanGoBack;

        public void GoForward() { FrameFacade.GoForward(); }

        public bool CanGoForward => FrameFacade.CanGoForward;

        public void ClearCache(bool removeCachedPagesInBackStack = false)
        {
            DebugWrite($"Frame: {FrameFacade.FrameId}");

            int currentSize = FrameFacade.Frame.CacheSize;

            if (removeCachedPagesInBackStack)
            {
                FrameFacade.Frame.CacheSize = 0;
            }
            else
            {
                if (FrameFacade.Frame.BackStackDepth == 0)
                    FrameFacade.Frame.CacheSize = 1;
                else
                    FrameFacade.Frame.CacheSize = FrameFacade.Frame.BackStackDepth;
            }

            FrameFacade.Frame.CacheSize = currentSize;
        }

        public void ClearHistory() { FrameFacade.Frame.BackStack.Clear(); }

        public async void Resuming()
        {
            DebugWrite($"Frame: {FrameFacade.FrameId}");

            var page = FrameFacade.Content as Page;
            if (page != null)
            {
                var dataContext = ResolveForPage(page);
                if (dataContext != null)
                {
                    dataContext.NavigationService = this;
                    dataContext.Dispatcher = this.GetDispatcherWrapper();
                    dataContext.SessionState = BootStrapper.Current.SessionState;
                    var pageState = FrameFacade.PageStateSettingsService(page.GetType(), parameter: FrameFacade.CurrentPageParam).Values;
                    await dataContext.OnNavigatedToAsync(FrameFacade.CurrentPageParam, NavigationMode.Refresh, pageState);

                    // update bindings after NavTo initializes data
                    //XamlUtils.InitializeBindings(page);
                    if (page.Content is UserControl pageWith)
                    {
                        XamlUtils.UpdateBindings(pageWith);
                    }
                    else
                    {
                        XamlUtils.UpdateBindings(page);
                    }
                }
            }
        }

        public async Task SuspendingAsync()
        {
            DebugWrite($"Frame: {FrameFacade.FrameId}");

            await SaveAsync();

            var page = FrameFacade.Content as Page;
            if (page != null)
            {
                var dataContext = ResolveForPage(page);
                if (dataContext != null)
                {
                    await NavigateFromAsync(page, dataContext, true).ConfigureAwait(false);
                }
            }
        }

        public Type CurrentPageType => FrameFacade.CurrentPageType;
        public object CurrentPageParam => FrameFacade.CurrentPageParam;
    }
}

