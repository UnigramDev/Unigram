//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Gallery;
using Telegram.Services;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels.Gallery;
using Telegram.Views;
using Telegram.Views.Popups;
using Telegram.Views.Settings;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using WinRT;

namespace Telegram.Navigation.Services
{
    public interface INavigationService
    {
        void GoBack(NavigationState state = null, NavigationTransitionInfo infoOverride = null);
        void GoBackAt(int index, bool back = true);
        void GoForward();

        object Content { get; }

        XamlRoot XamlRoot { get; }

        bool Navigate(Type page, object parameter = null, NavigationState state = null, NavigationTransitionInfo infoOverride = null, bool navigationStackEnabled = true);

        event EventHandler<NavigatedEventArgs> Navigated;

        bool CanGoBack { get; }
        bool CanGoForward { get; }

        string NavigationState { get; set; }

        IDictionary<string, long> CacheKeyToChatId { get; }

        void Refresh();

        void Suspend();

        void Block();


        Task<ViewLifetimeControl> OpenAsync(ViewServiceOptions parameters);
        Task<ViewLifetimeControl> OpenAsync(Type page, object parameter = null, string title = null, Size size = default);
        Task<ContentDialogResult> ShowPopupAsync(ContentPopup popup, object parameter = null, ElementTheme requestedTheme = ElementTheme.Default);
        void ShowPopup(ContentPopup popup, object parameter = null, ElementTheme requestedTheme = ElementTheme.Default);

        Task<ContentDialogResult> ShowPopupAsync(string message, string title = null, string primary = null, string secondary = null, string tertiary = null, bool destructive = false, ElementTheme requestedTheme = ElementTheme.Default);
        //Task<ContentDialogResult> ShowPopupAsync(FrameworkElement target, string message, string title = null, string primary = null, string secondary = null, bool destructive = false, ElementTheme requestedTheme = ElementTheme.Default);
        Task<ContentDialogResult> ShowPopupAsync(FormattedText message, string title = null, string primary = null, string secondary = null, string tertiary = null, bool destructive = false, ElementTheme requestedTheme = ElementTheme.Default);
        ////Task<ContentDialogResult> ShowPopupAsync(FrameworkElement target, FormattedText message, string title = null, string primary = null, string secondary = null, bool destructive = false, ElementTheme requestedTheme = ElementTheme.Default);
        void ShowPopup(string message, string title = null, string primary = null, string secondary = null, string tertiary = null, bool destructive = false, ElementTheme requestedTheme = ElementTheme.Default);
        void ShowPopup(FormattedText message, string title = null, string primary = null, string secondary = null, string tertiary = null, bool destructive = false, ElementTheme requestedTheme = ElementTheme.Default);
        Task<InputPopupResult> ShowInputAsync(InputPopupType type, string message, string title = null, string placeholderText = null, string primary = null, string secondary = null, bool destructive = false, ElementTheme requestedTheme = ElementTheme.Default);
        //Task<InputPopupResult> ShowInputAsync(FrameworkElement target, InputPopupType type, string message, string title = null, string placeholderText = null, string primary = null, string secondary = null, bool destructive = false, ElementTheme requestedTheme = ElementTheme.Default)

        ToastPopup ShowToast(string text, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null);
        ToastPopup ShowToast(string text, ToastPopupIcon icon, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null);
        ToastPopup ShowToast(FormattedText text, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null);
        ToastPopup ShowToast(FormattedText text, ToastPopupIcon icon, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null);

        void ShowGallery(GalleryViewModelBase parameter, FrameworkElement closing = null, long timestamp = 0);

        object CurrentPageParam { get; }
        Type CurrentPageType { get; }

        IDispatcherContext Dispatcher { get; }

        void ClearCache(bool removeCachedPagesInBackStack = false);

        Frame Frame { get; }
        FrameFacade FrameFacade { get; }

        WindowContext Window { get; }

        int SessionId { get; }

        void AddToBackStack(Type type, object parameter = null, NavigationTransitionInfo info = null);
        void InsertToBackStack(int index, Type type, object parameter = null, NavigationTransitionInfo info = null);
        void RemoveFromBackStack(int index);
        void ClearBackStack();
    }

    [GeneratedBindableCustomProperty]
    public partial class NavigationStackItem : BindableBase
    {
        public NavigationStackItem(Type sourcePageType, object parameter, string title, HostedNavigationMode mode)
        {
            SourcePageType = sourcePageType;
            Parameter = parameter;
            Title = title;
            Position = null;
            Mode = mode;
        }

        public NavigationStackItem(Type sourcePageType, object parameter, HostedPage page)
        {
            SourcePageType = sourcePageType;
            Parameter = parameter;
            Title = page.GetTitle();
            Position = page.GetPosition();
            Mode = page.NavigationMode;
        }

        public Type SourcePageType { get; }

        public object Parameter { get; }

        private string _title;
        public string Title
        {
            get => _title;
            set => Set(ref _title, value);
        }

        public HostedNavigationMode Mode { get; }

        public HostedPagePositionBase Position { get; }

        public override string ToString()
        {
            return Title;
        }
    }

    // DOCS: https://github.com/Windows-XAML/Template10/wiki/Docs-%7C-NavigationService
    public partial class NavigationService : INavigationService
    {
        private static readonly HashSet<Type> _unallowedTypes = new HashSet<Type>
        {
            typeof(SettingsPasswordPage),
            typeof(SettingsPasscodePage)
        };

        private readonly IViewService viewService = new ViewService();
        public WindowContext Window { get; }
        public FrameFacade FrameFacade { get; }
        public Frame Frame => FrameFacade.Frame;
        public object Content => Frame.Content;
        public XamlRoot XamlRoot => Window.Content?.XamlRoot;

        public IDispatcherContext Dispatcher { get; }


        public string NavigationState
        {
            get => Frame.GetNavigationState();
            set => Frame.SetNavigationState(value);
        }

        public int SessionId { get; private set; }

        public IDictionary<string, long> CacheKeyToChatId { get; } = new Dictionary<string, long>();

        public List<NavigationStackItem> BackStack { get; } = new();

        public event EventHandler BackStackChanged;

        public void GoBackAt(int index, bool back = true)
        {
            while (FrameFacade.BackStackDepth > index + 1)
            {
                RemoveFromBackStack(index + 1);
            }

            if (FrameFacade.CanGoBack && back)
            {
                FrameFacade.GoBack();
            }
            else
            {
                BackStackChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void InsertToBackStack(int index, Type type, object parameter = null, NavigationTransitionInfo info = null)
        {
            Frame.BackStack.Insert(index, new PageStackEntry(type, parameter, info));
            BackStack.Insert(index, new NavigationStackItem(type, parameter, null, HostedNavigationMode.Child));
        }

        public void AddToBackStack(Type type, object parameter = null, NavigationTransitionInfo info = null)
        {
            Frame.BackStack.Add(new PageStackEntry(type, parameter, info));
            BackStack.Add(new NavigationStackItem(type, parameter, null, HostedNavigationMode.Child));
        }

        public void RemoveFromBackStack(int index)
        {
            if (Frame.BackStack.Count > index)
            {
                Frame.BackStack.RemoveAt(index);
            }

            if (BackStack.Count > index)
            {
                BackStack.RemoveAt(index);
            }
        }

        public void ClearBackStack()
        {
            Frame.BackStack.Clear();
            BackStack.Clear();
        }

        public NavigationService(WindowContext window, Frame frame, int session, string id)
        {
            Window = window;
            Dispatcher = window.Dispatcher;
            SessionId = session;
            FrameFacade = new FrameFacade(this, frame, id);
            FrameFacade.Navigating += OnNavigating;
            FrameFacade.Navigated += OnNavigated;
        }

        private void OnNavigating(object sender, NavigatingEventArgs e)
        {
            if (e.Suspending)
            {
                return;
            }

            if (FrameFacade.Content is Page page)
            {
                if (e.NavigationMode is NavigationMode.New or NavigationMode.Forward)
                {
                    if (page is HostedPage hosted)
                    {
                        BackStack.Add(new NavigationStackItem(CurrentPageType, CurrentPageParam, hosted));
                    }
                    else
                    {
                        BackStack.Add(new NavigationStackItem(CurrentPageType, CurrentPageParam, null, HostedNavigationMode.Child));
                    }
                }

                // call navagable override (navigating)
                var dataContext = ViewModelForPage(page);
                if (dataContext != null)
                {
                    // allow the viewmodel to cancel navigation
                    e.Cancel = !NavigatingFrom(page, e.SourcePageType, e.Parameter, dataContext, false, e.NavigationMode);

                    if (e.Cancel)
                    {
                        return;
                    }

                    NavigateFrom(page, dataContext, false);
                }

                if (page is IActivablePage cleanup)
                {
                    cleanup.Deactivate(e.SourcePageType != page.GetType());
                }
            }
        }

        private async void OnNavigated(object sender, NavigatedEventArgs e)
        {
            if (e.NavigationMode == NavigationMode.Back && Frame.ForwardStack.Count > 0)
            {
                if (_unallowedTypes.Contains(Frame.ForwardStack[0].SourcePageType))
                {
                    Frame.ForwardStack.Clear();
                }
            }

            if (e.NavigationMode is NavigationMode.Back && BackStack.Count > 0)
            {
                var entry = BackStack[^1];
                BackStack.Remove(entry);

                // This is solely used by MasterDetailView for the animation
                e.VerticalOffset = entry.Position switch
                {
                    HostedPageScrollViewerPosition scrollViewerPosition => scrollViewerPosition.ScrollPosition,
                    HostedPageListViewPosition listViewPosition => listViewPosition.ScrollPosition,
                    _ => 0
                };

                bool ParameterEquals(object x, object y)
                {
                    if (x == null || y == null)
                    {
                        return x == y;
                    }

                    return x.Equals(y);
                }

                if (entry.Position != null && e.Content is HostedPage page && ParameterEquals(entry.Parameter, e.Parameter) && entry.SourcePageType == page.GetType())
                {
                    page.SetPosition(entry.Position);
                }
            }

            var parameter = e.Parameter;
            if (parameter is string cacheKey && e.SourcePageType == typeof(ChatPage))
            {
                parameter = CacheKeyToChatId[cacheKey];
            }

            Navigated?.Invoke(this, e);

            try
            {
                await NavigateToAsync(e.NavigationMode, parameter, FrameFacade.Frame.Content);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }

            OverlayWindow.Current?.TryHide(ContentDialogResult.None);
        }

        public void Suspend()
        {
            var page = FrameFacade.Content as Page;
            if (page != null)
            {
                // call navagable override (navigating)
                var dataContext = ViewModelForPage(page);
                if (dataContext != null)
                {
                    // allow the viewmodel to cancel navigation
                    NavigatingFrom(page, null, null, dataContext, true, NavigationMode.New);
                    NavigateFrom(page, dataContext, true);
                }

                if (page is IActivablePage cleanup)
                {
                    cleanup.Deactivate(true);
                }
            }
        }

        private bool _blocked;

        public void Block()
        {
            _blocked = true;
        }

        private INavigable ViewModelForPage(Page page, bool allowCreate = false)
        {
            if (page.DataContext is not INavigable or null && allowCreate)
            {
                // to support dependency injection, but keeping it optional.
                var viewModel = BootStrapper.Current.ViewModelForPage(page, SessionId);
                if (viewModel != null)
                {
                    page.DataContext = viewModel;
                    return viewModel;
                }
            }
            return page.DataContext as INavigable;
        }

        // before navigate (cancellable)
        private bool NavigatingFrom(Page page, Type targetPageType, object targetPageParameter, INavigable dataContext, bool suspending, NavigationMode mode)
        {
            Logger.Info($"Suspending: {suspending}");

            dataContext.NavigationService = this;
            dataContext.Dispatcher = Dispatcher;
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
            dataContext.NavigatingFrom(args);
            return !args.Cancel;
        }

        // after navigate
        private void NavigateFrom(Page page, INavigable dataContext, bool suspending)
        {
            Logger.Info($"Suspending: {suspending}");

            dataContext.NavigationService = this;
            dataContext.Dispatcher = Dispatcher;
            dataContext.SessionState = BootStrapper.Current.SessionState;

            var pageState = FrameFacade.PageStateSettingsService(page.GetType()).Values;
            dataContext.NavigatedFrom(pageState, suspending);
        }

        private async Task NavigateToAsync(NavigationMode mode, object parameter, object frameContent = null)
        {
            Logger.Info($"Mode: {mode}, Parameter: {parameter} FrameContent: {frameContent}");

            frameContent ??= FrameFacade.Frame.Content;

            var page = frameContent as Page;
            if (page != null)
            {
                if (page is IActivablePage cleanup)
                {
                    cleanup.Activate(this);
                }
                else if (page is BlankPage blank)
                {
                    blank.Activate(SessionId);
                }

                //if (mode == NavigationMode.New)
                //{
                //    var pageState = FrameFacadeInternal.PageStateSettingsService(page.GetType()).Values;
                //    pageState?.Clear();
                //}

                var dataContext = ViewModelForPage(page, true);
                if (dataContext != null)
                {
                    // prepare for state load
                    dataContext.NavigationService = this;
                    dataContext.Dispatcher = Dispatcher;
                    dataContext.SessionState = BootStrapper.Current.SessionState;
                    var pageState = FrameFacade.PageStateSettingsService(page.GetType(), parameter: parameter).Values;
                    await dataContext.NavigatedToAsync(parameter, mode, pageState);
                }
            }
        }

        public Task<ViewLifetimeControl> OpenAsync(ViewServiceOptions parameters) => viewService.OpenAsync(parameters);

        public Task<ViewLifetimeControl> OpenAsync(Type page, object parameter = null, string title = null, Size size = default)
        {
            Logger.Info($"Page: {page}, Parameter: {parameter}, Title: {title}, Size: {size}");
            return viewService.OpenAsync(page, parameter, title, size, SessionId);
        }

        public void ShowPopup(ContentPopup popup, object parameter = null, ElementTheme requestedTheme = ElementTheme.Default)
        {
            _ = ShowPopupAsync(popup, parameter, requestedTheme);
        }

        public Task<ContentDialogResult> ShowPopupAsync(ContentPopup popup, object parameter = null, ElementTheme requestedTheme = ElementTheme.Default)
        {
            if (requestedTheme != ElementTheme.Default)
            {
                popup.RequestedTheme = requestedTheme;
            }

            var viewModel = BootStrapper.Current.ViewModelForPage(popup, SessionId);
            if (viewModel != null)
            {
                viewModel.NavigationService = this;
                viewModel.Dispatcher = Dispatcher;

                void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
                {
                    popup.Opened -= OnOpened;
                }

                void OnClosed(ContentDialog sender, ContentDialogClosedEventArgs args)
                {
                    if (popup.IsFinalized)
                    {
                        viewModel.NavigatedFrom(null, false);
                        popup.OnNavigatedFrom();
                        popup.Closed -= OnClosed;
                    }
                }

                popup.DataContext = viewModel;

                _ = viewModel.NavigatedToAsync(parameter, NavigationMode.New, null);
                popup.OnNavigatedTo(parameter);
                popup.Closed += OnClosed;
            }

            return popup.ShowQueuedAsync(XamlRoot);
        }

        public Task<ContentDialogResult> ShowPopupAsync(string message, string title = null, string primary = null, string secondary = null, string tertiary = null, bool destructive = false, ElementTheme requestedTheme = ElementTheme.Default)
        {
            if (ContentPopup.IsAnyPopupOpen(XamlRoot))
            {
                return MessagePopup.ShowAsync(XamlRoot, target: null, message, title, primary, secondary ?? tertiary, destructive, requestedTheme);
            }

            return MessagePopup.ShowAsync(XamlRoot, message, title, primary, secondary, tertiary, destructive, requestedTheme);
        }

        //public Task<ContentDialogResult> ShowPopupAsync(FrameworkElement target, string message, string title = null, string primary = null, string secondary = null, bool destructive = false, ElementTheme requestedTheme = ElementTheme.Default)
        //{
        //    return MessagePopup.ShowAsync(target, message, title, primary, secondary, destructive, requestedTheme);
        //}

        public Task<ContentDialogResult> ShowPopupAsync(FormattedText message, string title = null, string primary = null, string secondary = null, string tertiary = null, bool destructive = false, ElementTheme requestedTheme = ElementTheme.Default)
        {
            return MessagePopup.ShowAsync(XamlRoot, message, title, primary, secondary, tertiary, destructive, requestedTheme);
        }

        ////public Task<ContentDialogResult> ShowPopupAsync(FrameworkElement target, FormattedText message, string title = null, string primary = null, string secondary = null, bool destructive = false, ElementTheme requestedTheme = ElementTheme.Default)
        ////{
        ////    return MessagePopup.ShowAsync(target, message, title, primary, secondary, destructive, requestedTheme);
        ////}

        public void ShowPopup(string message, string title = null, string primary = null, string secondary = null, string tertiary = null, bool destructive = false, ElementTheme requestedTheme = ElementTheme.Default)
        {
            if (ContentPopup.IsAnyPopupOpen(XamlRoot))
            {
                _ = MessagePopup.ShowAsync(XamlRoot, target: null, message, title, primary, secondary ?? tertiary, destructive, requestedTheme);
            }

            _ = MessagePopup.ShowAsync(XamlRoot, message, title, primary, secondary, tertiary, destructive, requestedTheme);
        }

        public void ShowPopup(FormattedText message, string title = null, string primary = null, string secondary = null, string tertiary = null, bool destructive = false, ElementTheme requestedTheme = ElementTheme.Default)
        {
            _ = MessagePopup.ShowAsync(XamlRoot, message, title, primary, secondary, tertiary, destructive, requestedTheme);
        }

        public Task<InputPopupResult> ShowInputAsync(InputPopupType type, string message, string title = null, string placeholderText = null, string primary = null, string secondary = null, bool destructive = false, ElementTheme requestedTheme = ElementTheme.Default)
        {
            return InputPopup.ShowAsync(XamlRoot, type, message, title, placeholderText, primary, secondary, destructive, requestedTheme);
        }

        //public Task<InputPopupResult> ShowInputAsync(FrameworkElement target, InputPopupType type, string message, string title = null, string placeholderText = null, string primary = null, string secondary = null, bool destructive = false, ElementTheme requestedTheme = ElementTheme.Default)
        //{
        //    return InputPopup.ShowAsync(target, type, message, title, placeholderText, primary, secondary, destructive, requestedTheme);
        //}

        public ToastPopup ShowToast(string text, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return ToastPopup.Show(XamlRoot, ClientEx.ParseMarkdown(text), null, requestedTheme, dismissAfter);
        }

        public ToastPopup ShowToast(string text, ToastPopupIcon icon, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return ToastPopup.Show(XamlRoot, ClientEx.ParseMarkdown(text), icon, requestedTheme, dismissAfter);
        }

        public ToastPopup ShowToast(FormattedText text, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return ToastPopup.Show(XamlRoot, text, null, requestedTheme, dismissAfter);
        }

        public ToastPopup ShowToast(FormattedText text, ToastPopupIcon icon, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return ToastPopup.Show(XamlRoot, text, icon, requestedTheme, dismissAfter);
        }

        public void ShowGallery(GalleryViewModelBase parameter, FrameworkElement closing = null, long timestamp = 0)
        {
            parameter.NavigationService = this;
            _ = GalleryWindow.ShowAsync(XamlRoot, parameter, closing, timestamp);
        }

        public event EventHandler<NavigatedEventArgs> Navigated;

        public bool Navigate(Type page, object parameter = null, NavigationState state = null, NavigationTransitionInfo infoOverride = null, bool navigationStackEnabled = true)
        {
            Logger.Info($"Page: {page}, Parameter: {parameter}, NavigationTransitionInfo: {infoOverride}");

            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            if (_blocked)
            {
                return false;
            }

            // use CurrentPageType/Param instead of LastNavigationType/Parameter to avoid new navigation to the current
            // page in some race conditions.
            if ((page.FullName == CurrentPageType?.FullName) && (parameter == CurrentPageParam))
            {
                return false;
            }

            if ((page.FullName == CurrentPageType?.FullName) && (parameter?.Equals(CurrentPageParam) ?? false))
            {
                return false;
            }

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

            return FrameFacade.Navigate(page, parameter, infoOverride, navigationStackEnabled);
        }

        public void Refresh() { FrameFacade.Refresh(); }

        public void GoBack(NavigationState state = null, NavigationTransitionInfo infoOverride = null)
        {
            if (FrameFacade.CanGoBack && !_blocked)
            {
                if (state != null)
                {
                    var entry = Frame.BackStack[Frame.BackStack.Count - 1];

                    var parameter = entry.Parameter;
                    if (parameter is string cacheKey && entry.SourcePageType == typeof(ChatPage))
                    {
                        parameter = CacheKeyToChatId[cacheKey];
                    }

                    var pageState = FrameFacade.PageStateSettingsService(entry.SourcePageType, 0, parameter).Values;

                    foreach (var item in state)
                    {
                        pageState[item.Key] = item.Value;
                    }
                }

                FrameFacade.GoBack(infoOverride);
            }
        }

        public bool CanGoBack => FrameFacade.CanGoBack;

        public void GoForward()
        {
            FrameFacade.GoForward();
        }

        public bool CanGoForward => FrameFacade.CanGoForward;

        public void ClearCache(bool removeCachedPagesInBackStack = false)
        {
            Logger.Info($"Frame: {FrameFacade.FrameId}");

            int currentSize = FrameFacade.Frame.CacheSize;

            if (removeCachedPagesInBackStack)
            {
                FrameFacade.Frame.CacheSize = 0;
            }
            else
            {
                if (FrameFacade.Frame.BackStackDepth == 0)
                {
                    FrameFacade.Frame.CacheSize = 1;
                }
                else
                {
                    FrameFacade.Frame.CacheSize = FrameFacade.Frame.BackStackDepth;
                }
            }

            FrameFacade.Frame.CacheSize = currentSize;
        }

        public Type CurrentPageType => FrameFacade.CurrentPageType;
        public object CurrentPageParam => FrameFacade.CurrentPageParam;
    }
}

