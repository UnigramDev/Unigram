using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unigram.Views;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Navigation.Services
{
    // DOCS: https://github.com/Windows-XAML/Template10/wiki/Docs-%7C-NavigationService
    public class FrameFacade
    {
        #region Debug

        [Conditional("DEBUG")]
        static void DebugWrite(string text = null, Unigram.Services.Logging.Severities severity = Unigram.Services.Logging.Severities.Template10, [CallerMemberName] string caller = null) =>
            Unigram.Services.Logging.LoggingService.WriteLine(text, severity, caller: $"{nameof(FrameFacade)}.{caller}");

        #endregion

        internal FrameFacade(NavigationService navigationService, Frame frame, string id)
        {
            NavigationService = navigationService;
            Frame = frame;
            frame.Navigated += FacadeNavigatedEventHandler;
            frame.Navigating += FacadeNavigatingCancelEventHandler;

            // setup animations
            var t = new NavigationThemeTransition
            {
                DefaultNavigationTransitionInfo = new EntranceNavigationTransitionInfo()
            };
            Frame.ContentTransitions = new TransitionCollection { };
            Frame.ContentTransitions.Add(t);

            FrameId = id;
        }

        public void RaiseNavigated(long chatId)
        {
            if (Content is ChatPage)
            {
                NavigationService.CacheKeyToChatId[CurrentPageCacheKey] = chatId;
                CurrentPageParam = chatId;

                var args = new NavigatedEventArgs
                {
                    NavigationMode = NavigationMode.Refresh,
                    SourcePageType = CurrentPageType,
                    Parameter = CurrentPageParam,
                    Content = Frame.Content as Page
                };

                foreach (var handler in _navigatedEventHandlers)
                {
                    if (handler.Target is INavigationService)
                    {
                        continue;
                    }

                    handler(Frame, args);
                }
            }
        }

        public event EventHandler<HandledEventArgs> BackRequested;
        public void RaiseBackRequested(HandledEventArgs args)
        {
            BackRequested?.Invoke(this, args);

            if (BackButtonHandling == BootStrapper.BackButton.Attach && !args.Handled && (args.Handled = Frame.BackStackDepth > 0))
            {
                GoBack();
            }
        }

        public event EventHandler<HandledEventArgs> ForwardRequested;
        public void RaiseForwardRequested(HandledEventArgs args)
        {
            ForwardRequested?.Invoke(this, args);

            if (!args.Handled && Frame.ForwardStack.Count > 0)
            {
                GoForward();
            }
        }

        #region state

        private string GetFrameStateKey() => string.Format("{0}-PageState", FrameId);

        private Unigram.Services.SettingsLegacy.ISettingsService FrameStateSettingsService()
        {
            return Unigram.Services.SettingsLegacy.SettingsService.Create(GetFrameStateKey(), true);
        }

        public void SetFrameState(string key, string value)
        {
            FrameStateSettingsService().Write(key, value);
        }

        public string GetFrameState(string key, string otherwise)
        {
            return FrameStateSettingsService().Read(key, otherwise);
        }

        public void ClearFrameState()
        {
            FrameStateSettingsService().Clear();
        }

        private string GetPageStateKey(string frameId, Type type, int backStackDepth, object parameter)
        {
            if (FrameStateSettingsService().IsBasicType(parameter))
            {
                return $"{frameId}-{type}-{parameter}";
            }

            return $"{frameId}-{type}-{backStackDepth}";
        }

        public Unigram.Services.SettingsLegacy.ISettingsService PageStateSettingsService(Type type, int depth = 0, object parameter = null)
        {
            var key = GetPageStateKey(FrameId, type, BackStackDepth + depth, parameter);
            return FrameStateSettingsService().Open(key, true);
        }

        public Unigram.Services.SettingsLegacy.ISettingsService PageStateSettingsService(string key)
        {
            return FrameStateSettingsService().Open(key, true);
        }

        public void ClearPageState(Type type)
        {
            this.FrameStateSettingsService().Remove(GetPageStateKey(FrameId, type, BackStackDepth, null));
        }

        #endregion

        #region frame facade

        public Frame Frame { get; }

        public BootStrapper.BackButton BackButtonHandling { get; internal set; }

        public string FrameId { get; private set; }

        internal NavigationService NavigationService { get; set; }

        public bool Navigate(Type page, object parameter, NavigationTransitionInfo infoOverride)
        {
            DebugWrite();

            if (Frame.Navigate(page, parameter, infoOverride))
            {
                return page.Equals(Frame.Content?.GetType());
            }
            else
            {
                return false;
            }
        }

        public int BackStackDepth => Frame.BackStackDepth;

        public bool CanGoBack => Frame.CanGoBack;

        public NavigationMode NavigationModeHint = NavigationMode.New;

        public void GoBack(NavigationTransitionInfo infoOverride = null)
        {
            DebugWrite($"CanGoBack {CanGoBack}");

            NavigationModeHint = NavigationMode.Back;
            if (CanGoBack)
            {
                if (infoOverride == null) Frame.GoBack();
                else Frame.GoBack(infoOverride);
            }
        }

        public void Refresh()
        {
            DebugWrite();

            NavigationModeHint = NavigationMode.Refresh;

            try
            {
                object context = (Frame as FrameworkElement).DataContext;

                Windows.ApplicationModel.Resources.Core.ResourceContext.GetForCurrentView().Reset();
                // this only works for apps using serializable types
                var state = Frame.GetNavigationState();
                Frame.SetNavigationState(state);

                (Frame as FrameworkElement).DataContext = context;
            }
            catch (Exception)
            {
                if (Frame.CanGoBack)
                {
                    Frame.GoBack();
                    Frame.GoForward();
                }
                else if (Frame.CanGoForward)
                {
                    Frame.GoForward();
                    Frame.GoBack();
                }
                else
                {
                    // not much we can really do in this case
                    (Frame.Content as Page)?.UpdateLayout();
                }
            }
        }

        public void Refresh(object param)
        {
            DebugWrite();



            try
            {
                object context = (Frame as FrameworkElement).DataContext;
                Windows.ApplicationModel.Resources.Core.ResourceContext.GetForCurrentView().Reset();
                // navigates to the current page with new parameters.
                Frame.Navigate(CurrentPageType, param, new SuppressNavigationTransitionInfo());
                (Frame as FrameworkElement).DataContext = context;

            }
            catch (Exception)
            {
                if (Frame.CanGoBack)
                {
                    Frame.GoBack();
                }
                else if (Frame.CanGoForward)
                {
                    Frame.GoForward();
                }
                else
                {
                    // not much we can really do in this case
                    (Frame.Content as Page)?.UpdateLayout();
                }
            }
        }

        public bool CanGoForward => Frame.CanGoForward;

        public void GoForward()
        {
            DebugWrite($"CanGoForward {CanGoForward}");

            NavigationModeHint = NavigationMode.Forward;
            if (CanGoForward) Frame.GoForward();
        }

        public object Content => Frame.Content;

        public Type CurrentPageType { get; internal set; }

        public object CurrentPageParam { get; internal set; }

        public string CurrentPageCacheKey { get; private set; }

        #endregion

        readonly List<EventHandler<NavigatedEventArgs>> _navigatedEventHandlers = new List<EventHandler<NavigatedEventArgs>>();
        public event EventHandler<NavigatedEventArgs> Navigated
        {
            add { if (!_navigatedEventHandlers.Contains(value)) _navigatedEventHandlers.Add(value); }
            remove { if (_navigatedEventHandlers.Contains(value)) _navigatedEventHandlers.Remove(value); }
        }
        void FacadeNavigatedEventHandler(object sender, Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            DebugWrite();

            CurrentPageType = e.SourcePageType;
            CurrentPageParam = e.Parameter;
            CurrentPageCacheKey = null;

            if (e.SourcePageType == typeof(ChatPage) && CurrentPageParam is string cacheKey)
            {
                CurrentPageParam = NavigationService.CacheKeyToChatId[cacheKey];
                CurrentPageCacheKey = cacheKey;
            }

            var args = new NavigatedEventArgs(e, Content as Page);

            if (NavigationModeHint != NavigationMode.New)
                args.NavigationMode = NavigationModeHint;

            NavigationModeHint = NavigationMode.New;

            foreach (var handler in _navigatedEventHandlers)
            {
                handler(Frame, args);
            }
        }

        readonly List<EventHandler<NavigatingEventArgs>> _navigatingEventHandlers = new List<EventHandler<NavigatingEventArgs>>();
        public event EventHandler<NavigatingEventArgs> Navigating
        {
            add { if (!_navigatingEventHandlers.Contains(value)) _navigatingEventHandlers.Add(value); }
            remove { if (_navigatingEventHandlers.Contains(value)) _navigatingEventHandlers.Remove(value); }
        }
        private void FacadeNavigatingCancelEventHandler(object sender, NavigatingCancelEventArgs e)
        {
            DebugWrite();

            var parameter = e.Parameter;
            if (parameter is string cacheKey && e.SourcePageType == typeof(ChatPage))
            {
                parameter = NavigationService.CacheKeyToChatId[cacheKey];
            }

            var args = new NavigatingEventArgs(e, Content as Page, e.SourcePageType, parameter, e.Parameter);

            if (NavigationModeHint != NavigationMode.New)
                args.NavigationMode = NavigationModeHint;

            NavigationModeHint = NavigationMode.New;

            foreach (var handler in _navigatingEventHandlers)
            {
                handler(Frame, args);
            }

            e.Cancel = args.Cancel;
        }
    }
}
