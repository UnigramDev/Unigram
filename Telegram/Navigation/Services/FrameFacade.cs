//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using Telegram.Common;
using Telegram.Views;
using Telegram.Views.Authorization;

namespace Telegram.Navigation.Services
{
    // DOCS: https://github.com/Windows-XAML/Template10/wiki/Docs-%7C-NavigationService
    public class FrameFacade
    {
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

                if (Navigated is MulticastDelegate multicast)
                {
                    foreach (var handler in multicast.GetInvocationList())
                    {
                        if (handler.Target is INavigationService)
                        {
                            continue;
                        }

                        handler.DynamicInvoke(Frame, args);
                    }
                }
            }
        }

        public event EventHandler<BackRequestedRoutedEventArgs> BackRequested;
        public void RaiseBackRequested(BackRequestedRoutedEventArgs args)
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

        private Telegram.Services.ISettingsLegacyService FrameStateSettingsService()
        {
            return Telegram.Services.SettingsLegacyService.Create(GetFrameStateKey(), true);
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
            return $"{frameId}-{type}-{parameter}";
        }

        public Telegram.Services.ISettingsLegacyService PageStateSettingsService(Type type, int depth = 0, object parameter = null)
        {
            var key = GetPageStateKey(FrameId, type, BackStackDepth + depth, parameter);
            return FrameStateSettingsService().Open(key, true);
        }

        public Telegram.Services.ISettingsLegacyService PageStateSettingsService(string key)
        {
            return FrameStateSettingsService().Open(key, true);
        }

        #endregion

        #region frame facade

        public Frame Frame { get; }

        public BootStrapper.BackButton BackButtonHandling { get; internal set; }

        public string FrameId { get; private set; }

        internal NavigationService NavigationService { get; set; }

        public bool Navigate(Type page, object parameter, NavigationTransitionInfo infoOverride, bool navigationStackEnabled = true)
        {
            Logger.Info();

            if (PowerSavingPolicy.AreSmoothTransitionsEnabled is false)
            {
                infoOverride = new SuppressNavigationTransitionInfo();
            }

            // THIS CRASHES EVERYTHING!!!
            //if (navigationStackEnabled is false)
            //{
            //    return Frame.NavigateToType(page, parameter, new FrameNavigationOptions
            //    {
            //        TransitionInfoOverride = infoOverride,
            //        IsNavigationStackEnabled = navigationStackEnabled
            //    });
            //}

            if (Frame.Navigate(page, parameter, infoOverride))
            {
                return page.Equals(Frame.Content?.GetType());
            }

            return false;
        }

        public int BackStackDepth => Frame.BackStackDepth;

        public bool CanGoBack => Frame.CanGoBack;

        public NavigationMode NavigationModeHint = NavigationMode.New;

        public void GoBack(NavigationTransitionInfo infoOverride = null)
        {
            Logger.Info($"CanGoBack {CanGoBack}");

            NavigationModeHint = NavigationMode.Back;
            if (CanGoBack)
            {
                if (infoOverride == null)
                {
                    Frame.GoBack();
                }
                else
                {
                    Frame.GoBack(infoOverride);
                }
            }
        }

        public void Refresh()
        {
            Logger.Info();

            NavigationModeHint = NavigationMode.Refresh;

            try
            {
                object context = Frame.DataContext;

                Windows.ApplicationModel.Resources.Core.ResourceContext.GetForCurrentView().Reset();
                // this only works for apps using serializable types
                var state = Frame.GetNavigationState();
                Frame.SetNavigationState(state);

                Frame.DataContext = context;
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
            Logger.Info();



            try
            {
                object context = Frame.DataContext;
                Windows.ApplicationModel.Resources.Core.ResourceContext.GetForCurrentView().Reset();
                // navigates to the current page with new parameters.
                Frame.Navigate(CurrentPageType, param, new SuppressNavigationTransitionInfo());
                Frame.DataContext = context;

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
            Logger.Info($"CanGoForward {CanGoForward}");

            NavigationModeHint = NavigationMode.Forward;
            if (CanGoForward)
            {
                Frame.GoForward();
            }
        }

        public object Content => Frame.Content;

        public Type CurrentPageType { get; internal set; }

        public object CurrentPageParam { get; internal set; }

        public string CurrentPageCacheKey { get; private set; }

        #endregion

        public event EventHandler<NavigatedEventArgs> Navigated;

        private static readonly HashSet<Type> _authorizationTypes = new()
        {
            typeof(AuthorizationCodePage),
            typeof(AuthorizationEmailAddressPage),
            typeof(AuthorizationEmailCodePage),
            typeof(AuthorizationPasswordPage),
            //typeof(AuthorizationRecoveryPage),
            typeof(AuthorizationRegistrationPage)
        };

        private void FacadeNavigatedEventHandler(object sender, NavigationEventArgs e)
        {
            Logger.Info();

            CurrentPageType = e.SourcePageType;
            CurrentPageParam = e.Parameter;
            CurrentPageCacheKey = null;

            if (e.SourcePageType == typeof(ChatPage) && CurrentPageParam is string cacheKey)
            {
                CurrentPageParam = NavigationService.CacheKeyToChatId[cacheKey];
                CurrentPageCacheKey = cacheKey;
            }

            var args = new NavigatedEventArgs(e, Content as Page);
            args.Parameter = CurrentPageParam;

            if (NavigationModeHint != NavigationMode.New)
            {
                args.NavigationMode = NavigationModeHint;
            }

            NavigationModeHint = NavigationMode.New;
            Navigated?.Invoke(Frame, args);

            if (Frame.BackStack.Count > 0 && _authorizationTypes.Contains(Frame.BackStack[Frame.BackStack.Count - 1].SourcePageType))
            {
                Frame.BackStack.RemoveAt(Frame.BackStack.Count - 1);
            }
        }

        public event EventHandler<NavigatingEventArgs> Navigating;

        private void FacadeNavigatingCancelEventHandler(object sender, NavigatingCancelEventArgs e)
        {
            Logger.Info();

            var parameter = e.Parameter;
            if (parameter is string cacheKey && e.SourcePageType == typeof(ChatPage))
            {
                parameter = NavigationService.CacheKeyToChatId[cacheKey];
            }

            var args = new NavigatingEventArgs(e, Content as Page, e.SourcePageType, parameter, e.Parameter);

            if (NavigationModeHint != NavigationMode.New)
            {
                args.NavigationMode = NavigationModeHint;
            }

            NavigationModeHint = NavigationMode.New;
            Navigating?.Invoke(Frame, args);

            e.Cancel = args.Cancel;
        }
    }
}
