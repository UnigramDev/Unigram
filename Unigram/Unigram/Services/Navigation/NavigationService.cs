using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Unigram.Navigation;
using Windows.ApplicationModel.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using Unigram.Services.Serialization;
using Unigram.Services.ViewService;
using Unigram.Common;
using System.Diagnostics;
using Windows.Foundation;

namespace Unigram.Services.Navigation
{
    // DOCS: https://github.com/Windows-XAML/Template10/wiki/Docs-%7C-NavigationService
    public partial class NavigationService : INavigationService
    {
        private readonly IViewService viewService = new ViewService.ViewService();
        FrameFacade FrameFacadeInternal { get; }
        public FrameFacade FrameFacade => FrameFacadeInternal;
        public bool IsInMainView { get; }
        public Frame Frame => FrameFacade.Frame;
        public object Content => Frame.Content;

        public DispatcherWrapper Dispatcher => this.GetDispatcherWrapper() as DispatcherWrapper;


        public string NavigationState
        {
            get { return Frame.GetNavigationState(); }
            set { Frame.SetNavigationState(value); }
        }

        public int SessionId { get; private set; }

        #region Debug

        [Conditional("DEBUG")]
        static void DebugWrite(string text = null, Services.Logging.Severities severity = Services.Logging.Severities.Template10, [CallerMemberName]string caller = null) =>
            Services.Logging.LoggingService.WriteLine(text, severity, caller: $"NavigationService.{caller}");

        #endregion

        public static INavigationService GetForFrame(Frame frame) =>
            WindowContext.ActiveWrappers.SelectMany(x => x.NavigationServices).FirstOrDefault(x => x.FrameFacade.Frame.Equals(frame));

        public NavigationService(Frame frame, int session, string id)
        {
            SerializationService = Services.Serialization.SerializationService.Json;
            IsInMainView = CoreApplication.MainView == CoreApplication.GetCurrentView();
            SessionId = session;
            FrameFacadeInternal = new FrameFacade(this, frame, id);
            FrameFacadeInternal.Navigating += async (s, e) =>
            {
                if (e.Suspending)
                    return;

                var page = FrameFacadeInternal.Content as Page;
                if (page != null)
                {
                    // call navagable override (navigating)
                    var dataContext = ResolveForPage(page);
                    if (dataContext != null)
                    {
                        // allow the viewmodel to cancel navigation
                        e.Cancel = !(await NavigatingFromAsync(page, e.PageType, e.Parameter, dataContext, false, e.NavigationMode));
                        if (!e.Cancel)
                        {
                            await NavigateFromAsync(page, dataContext, false).ConfigureAwait(false);
                        }
                    }
                }
            };
            FrameFacadeInternal.Navigated += async (s, e) =>
            {
                var parameter = SerializationService.Deserialize(e.Parameter?.ToString());
                var currentContent = FrameFacadeInternal.Frame.Content;
                //await this.GetDispatcherWrapper().DispatchAsync(async () =>
                //{
                    try
                    {
                        if (currentContent == FrameFacadeInternal.Frame.Content)
                            await NavigateToAsync(e.NavigationMode, parameter, FrameFacadeInternal.Frame.Content).ConfigureAwait(false);
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
        async Task<bool> NavigatingFromAsync(Page page, Type targetPageType, object targetPageParameter, INavigable dataContext, bool suspending, NavigationMode mode)
        {
            DebugWrite($"Suspending: {suspending}");

            dataContext.NavigationService = this;
            dataContext.Dispatcher = this.GetDispatcherWrapper();
            dataContext.SessionState = BootStrapper.Current.SessionState;

            var deferral = new DeferralManager();
            var args = new NavigatingEventArgs(deferral)
            {
                NavigationMode = mode,
                PageType = FrameFacadeInternal.CurrentPageType,
                Parameter = FrameFacadeInternal.CurrentPageParam,
                Suspending = suspending,
                TargetPageType = targetPageType,
                TargetPageParameter = targetPageParameter
            };
            await deferral.WaitForDeferralsAsync();
            await dataContext.OnNavigatingFromAsync(args).ConfigureAwait(false);
            return !args.Cancel;
        }

        // after navigate
        async Task NavigateFromAsync(Page page, INavigable dataContext, bool suspending)
        {
            DebugWrite($"Suspending: {suspending}");

            dataContext.NavigationService = this;
            dataContext.Dispatcher = this.GetDispatcherWrapper();
            dataContext.SessionState = BootStrapper.Current.SessionState;

            var pageState = FrameFacadeInternal.PageStateSettingsService(page.GetType()).Values;
            await dataContext.OnNavigatedFromAsync(pageState, suspending).ConfigureAwait(false);
        }

        async Task NavigateToAsync(NavigationMode mode, object parameter, object frameContent = null)
        {
            DebugWrite($"Mode: {mode}, Parameter: {parameter} FrameContent: {frameContent}");

            frameContent = frameContent ?? FrameFacadeInternal.Frame.Content;

            var page = frameContent as Page;
            if (page != null)
            {
                if (page is IDisposable cleanup)
                {
                    cleanup.Dispose();
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
                    var pageState = FrameFacadeInternal.PageStateSettingsService(page.GetType(), parameter: parameter).Values;
                    await dataContext.OnNavigatedToAsync(parameter, mode, pageState);

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

        public Task<ViewLifetimeControl> OpenAsync(Type page, object parameter = null, string title = null, ViewSizePreference size = ViewSizePreference.UseHalf)
        {
            DebugWrite($"Page: {page}, Parameter: {parameter}, Title: {title}, Size: {size}");
            return viewService.OpenAsync(page, parameter, title, size);
        }

        public Task<ViewLifetimeControl> OpenAsync(Func<UIElement> content, object parameter)
        {
            DebugWrite($"Content: {content}, Parameter: {parameter}");
            return viewService.OpenAsync(content, parameter);
        }

        public async Task<bool> NavigateAsync(Type page, object parameter = null, IDictionary<string, object> state = null, NavigationTransitionInfo infoOverride = null)
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
                var pageState = FrameFacadeInternal.PageStateSettingsService(page, 1, parameter).Values;
                foreach (var item in state)
                {
                    pageState[item.Key] = item.Value;
                }
            }

            //var frameContent = FrameFacadeInternal.Frame.Content;
            //if (frameContent is Page current && frameContent is IDisposable disposable && page.FullName == CurrentPageType?.FullName)
            //{
            //    if (current.DataContext is INavigable dataContext)
            //    {
            //        await NavigatingFromAsync(current, page, parameter, dataContext, false, NavigationMode.Refresh);
            //        await NavigateFromAsync(current, dataContext, false);
            //    }

            //    await NavigateToAsync(NavigationMode.Refresh, parameter, current);

            //    FrameFacade.CurrentPageParam = parameter;
            //    FrameFacade.Frame.BackStack.Add(new PageStackEntry(page, SerializationService.Serialize(CurrentPageParam), infoOverride));
            //    return true;
            //}

            parameter = SerializationService.Serialize(parameter);

            return FrameFacadeInternal.Navigate(page, parameter, infoOverride);
        }

        public void Navigate(Type page, object parameter = null, IDictionary<string, object> state = null, NavigationTransitionInfo infoOverride = null)
        {
            DebugWrite($"Page: {page}, Parameter: {parameter}, NavigationTransitionInfo: {infoOverride}");

            NavigateAsync(page, parameter, state, infoOverride).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Navigate<T> allows developers to navigate using a
        /// page key instead of the view type.This is accomplished by
        /// creating a custom Enum and setting up the PageKeys dict
        /// with the Key/Type pairs for your views.The dict is
        /// shared by all NavigationServices and is stored in
        /// the BootStrapper (or Application) of the app.
        /// 
        /// Implementation example:
        /// 
        /// // define your Enum
        /// public Enum Pages { MainPage, DetailPage }
        /// 
        /// // setup the keys dict
        /// var keys = BootStrapper.PageKeys<Views>();
        /// keys.Add(Pages.MainPage, typeof(Views.MainPage));
        /// keys.Add(Pages.DetailPage, typeof(Views.DetailPage));
        /// 
        /// // use Navigate<T>()
        /// NavigationService.Navigate(Pages.MainPage);
        /// </remarks>
        /// <typeparam name="T">T must be the same custom Enum used with BootStrapper.PageKeys()</typeparam>
        public async Task<bool> NavigateAsync<T>(T key, object parameter = null, IDictionary<string, object> state = null, NavigationTransitionInfo infoOverride = null)
            where T : struct, IConvertible
        {
            DebugWrite($"Key: {key}, Parameter: {parameter}, NavigationTransitionInfo: {infoOverride}");

            var keys = BootStrapper.Current.PageKeys<T>();

            if (!keys.ContainsKey(key))
                throw new KeyNotFoundException(key.ToString());

            var page = keys[key];

            return await NavigateAsync(page, parameter, state, infoOverride).ConfigureAwait(false);
        }

        public void Navigate<T>(T key, object parameter = null, IDictionary<string, object> state = null, NavigationTransitionInfo infoOverride = null)
            where T : struct, IConvertible
        {
            DebugWrite($"Key: {key}, Parameter: {parameter}, NavigationTransitionInfo: {infoOverride}");

            NavigateAsync(key, parameter, state, infoOverride).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public ISerializationService SerializationService { get; set; }

        public event EventHandler<CancelEventArgs<Type>> BeforeSavingNavigation;


        public async Task SaveAsync()
        {
            DebugWrite($"Frame: {FrameFacadeInternal.FrameId}");

            if (CurrentPageType == null)
                return;
            var args = new CancelEventArgs<Type>(FrameFacadeInternal.CurrentPageType);
            BeforeSavingNavigation?.Invoke(this, args);
            if (args.Cancel)
                return;

            var state = FrameFacadeInternal.PageStateSettingsService(GetType().ToString());
            if (state == null)
            {
                throw new InvalidOperationException("State container is unexpectedly null");
            }

            state.Write<string>("CurrentPageType", CurrentPageType.AssemblyQualifiedName);
            state.Write<object>("CurrentPageParam", CurrentPageParam);
            state.Write<string>("NavigateState", FrameFacadeInternal?.NavigationService.NavigationState);

            await Task.CompletedTask;
        }

        [Obsolete("SaveNavigationAsync() is obsolete - please use SaveAsync() instead.")]
        public async Task SaveNavigationAsync()
        {
            await SaveAsync();
        }

        public event TypedEventHandler<INavigationService, Type> AfterRestoreSavedNavigation;


        public async Task<bool> LoadAsync()
        {
            DebugWrite($"Frame: {FrameFacadeInternal.FrameId}");

            try
            {
                var state = FrameFacadeInternal.PageStateSettingsService(GetType().ToString());
                if (state == null || !state.Exists("CurrentPageType"))
                {
                    return false;
                }

                FrameFacadeInternal.CurrentPageType = Type.GetType(state.Read<string>("CurrentPageType"));
                FrameFacadeInternal.CurrentPageParam = state.Read<object>("CurrentPageParam");
                FrameFacadeInternal.NavigationService.NavigationState = state.Read<string>("NavigateState");

                await NavigateToAsync(NavigationMode.Refresh, FrameFacadeInternal.CurrentPageParam);
                while (FrameFacadeInternal.Frame.Content == null)
                {
                    await Task.Delay(1);
                }
                AfterRestoreSavedNavigation?.Invoke(this, FrameFacadeInternal.CurrentPageType);
                return true;
            }
            catch { return false; }
        }

        [Obsolete("RestoreSavedNavigationAsync is obsolete - please use LoadAsync() instead.")]
        public async Task<bool> RestoreSavedNavigationAsync()
        {
            return await LoadAsync();
        }

        public void Refresh() { FrameFacadeInternal.Refresh(); }
        public void Refresh(object param) { FrameFacadeInternal.Refresh(param); }

        public void GoBack(NavigationTransitionInfo infoOverride = null)
        {
            if (FrameFacadeInternal.CanGoBack) FrameFacadeInternal.GoBack(infoOverride);
        }

        public bool CanGoBack => FrameFacadeInternal.CanGoBack;

        public void GoForward() { FrameFacadeInternal.GoForward(); }

        public bool CanGoForward => FrameFacadeInternal.CanGoForward;

        public void ClearCache(bool removeCachedPagesInBackStack = false)
        {
            DebugWrite($"Frame: {FrameFacadeInternal.FrameId}");

            int currentSize = FrameFacadeInternal.Frame.CacheSize;

            if (removeCachedPagesInBackStack)
            {
                FrameFacadeInternal.Frame.CacheSize = 0;
            }
            else
            {
                if (FrameFacadeInternal.Frame.BackStackDepth == 0)
                    FrameFacadeInternal.Frame.CacheSize = 1;
                else
                    FrameFacadeInternal.Frame.CacheSize = FrameFacadeInternal.Frame.BackStackDepth;
            }

            FrameFacadeInternal.Frame.CacheSize = currentSize;
        }

        public void ClearHistory() { FrameFacadeInternal.Frame.BackStack.Clear(); }

        public async void Resuming()
        {
            DebugWrite($"Frame: {FrameFacadeInternal.FrameId}");

            var page = FrameFacadeInternal.Content as Page;
            if (page != null)
            {
                var dataContext = ResolveForPage(page);
                if (dataContext != null)
                {
                    dataContext.NavigationService = this;
                    dataContext.Dispatcher = this.GetDispatcherWrapper();
                    dataContext.SessionState = BootStrapper.Current.SessionState;
                    var pageState = FrameFacadeInternal.PageStateSettingsService(page.GetType(), parameter: FrameFacadeInternal.CurrentPageParam).Values;
                    await dataContext.OnNavigatedToAsync(FrameFacadeInternal.CurrentPageParam, NavigationMode.Refresh, pageState);

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
            DebugWrite($"Frame: {FrameFacadeInternal.FrameId}");

            await SaveAsync();

            var page = FrameFacadeInternal.Content as Page;
            if (page != null)
            {
                var dataContext = ResolveForPage(page);
                if (dataContext != null)
                {
                    await NavigateFromAsync(page, dataContext, true).ConfigureAwait(false);
                }
            }
        }

        public Type CurrentPageType => FrameFacadeInternal.CurrentPageType;
        public object CurrentPageParam => FrameFacadeInternal.CurrentPageParam;
    }
}

