//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Navigation.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.Views;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Common
{
    public static class UnigramNavigationServiceEx
    {
        private static ContentDialog _currentDialog;

        public static Task<ContentDialogResult> NavigateModalAsync(this INavigationService service, Type dialog)
        {
            return NavigateModalAsync(service, (ContentDialog)Activator.CreateInstance(dialog));
        }

        public static async Task<ContentDialogResult> NavigateModalAsync(this INavigationService service, ContentDialog dialog)
        {
            var viewModel = dialog.DataContext as INavigable;
            if (viewModel != null)
            {
                viewModel.NavigationService = service;
                dialog.Opened += async (s, args) =>
                {
                    await viewModel.NavigatedToAsync(null, NavigationMode.New, null);
                };
            }

            _currentDialog = dialog;
            return await dialog.ShowQueuedAsync();
        }

        public static void PopModal(this INavigationService service)
        {
            if (_currentDialog != null)
            {
                _currentDialog.Hide();
                _currentDialog = null;
            }
        }

        public static void Navigate<T>(this INavigationService service, Type page, object parameter = null, NavigationState state = null, NavigationTransitionInfo infoOverride = null)
        {
            //NavigatedEventHandler handler = null;
            //handler = (s, args) =>
            //{
            //    service.Frame.Navigated -= handler;

            //    var navigated = args.Content as Page;
            //    if (navigated != null && args.SourcePageType == page)
            //    {
            //        navigated.DataContext = UnigramContainer.Instance.ResolveType<T>();
            //    }
            //};

            ViewModels.Enqueue(typeof(T));
            service.Navigate(page, parameter, state, infoOverride);
        }

        //public static void SelectUsers<T>(this INavigationService service, object parameter = null, NavigationTransitionInfo infoOverride = null)
        //{
        //    ViewModels.Enqueue(typeof(T));
        //    service.Navigate(typeof(UsersSelectionPage), parameter, infoOverride);
        //}

        public static Queue<Type> ViewModels { get; } = new Queue<Type>();



        public static void RemoveSkip(this INavigationService service, int count)
        {
            while (service.Frame.BackStackDepth > count)
            {
                service.RemoveFromBackStack(count);
            }
        }

        public static void RemoveLast(this INavigationService service)
        {
            if (service.CanGoBack)
            {
                service.RemoveFromBackStack(service.Frame.BackStackDepth - 1);
            }
        }

        public static void RemoveLastIf(this INavigationService service, Type type)
        {
            if (service.CanGoBack && service.Frame.BackStack[service.Frame.BackStackDepth - 1].SourcePageType == type)
            {
                service.RemoveFromBackStack(service.Frame.BackStackDepth - 1);
            }
        }

        public static void NavigateToInstant(this INavigationService service, string url)
        {
            if (service is TLNavigationService serviceEx)
            {
                serviceEx.NavigateToInstant(url);
            }
        }

        public static void NavigateToInvoice(this INavigationService service, MessageViewModel message)
        {
            if (service is TLNavigationService serviceEx)
            {
                serviceEx.NavigateToInvoice(message);
            }
        }

        public static void NavigateToInvoice(this INavigationService service, InputInvoice inputInvoice)
        {
            if (service is TLNavigationService serviceEx)
            {
                serviceEx.NavigateToInvoice(inputInvoice);
            }
        }

        public static void NavigateToSender(this INavigationService service, MessageSender sender)
        {
            if (service is TLNavigationService serviceEx)
            {
                serviceEx.NavigateToSender(sender);
            }
        }

        public static void NavigateToThread(this INavigationService service, Chat chat, long thread, long? message = null, NavigationState state = null)
        {
            if (service is TLNavigationService serviceEx)
            {
                serviceEx.NavigateToChat(chat, message, thread, state: state);
            }
        }

        public static void NavigateToThread(this INavigationService service, long chatId, long thread, long? message = null, NavigationState state = null)
        {
            if (service is TLNavigationService serviceEx)
            {
                serviceEx.NavigateToChat(chatId, message, thread, state: state);
            }
        }

        public static void NavigateToUser(this INavigationService service, long userId, bool toChat = false)
        {
            if (service is TLNavigationService serviceEx)
            {
                serviceEx.NavigateToUser(userId, toChat);
            }
        }

        public static void NavigateToChat(this INavigationService service, Chat chat, long? message = null, long? thread = null, string accessToken = null, NavigationState state = null, bool scheduled = false, bool force = true, bool createNewWindow = false)
        {
            if (service is TLNavigationService serviceEx)
            {
                serviceEx.NavigateToChat(chat, message, thread, accessToken, state, scheduled, force, createNewWindow);
            }
        }

        public static void NavigateToChat(this INavigationService service, long chatId, long? message = null, long? thread = null, string accessToken = null, NavigationState state = null, bool scheduled = false, bool force = true, bool createNewWindow = false)
        {
            if (service is TLNavigationService serviceEx)
            {
                serviceEx.NavigateToChat(chatId, message, thread, accessToken, state, scheduled, force, createNewWindow);
            }
        }

        public static void NavigateToMain(this INavigationService service, string parameter)
        {
            void handler(object s, NavigationEventArgs args)
            {
                service.Frame.Navigated -= handler;

                if (args.Content is MainPage page)
                {
                    page.Activate(parameter);
                }
            }

            service.Frame.Navigated += handler;
            service.Navigate(typeof(MainPage));
        }

        public static void NavigateToPasscode(this INavigationService service)
        {
            if (service is TLNavigationService serviceEx)
            {
                serviceEx.NavigateToPasscode();
            }
        }

        public static void ShowLimitReached(this INavigationService service, PremiumLimitType limit)
        {
            if (service is TLNavigationService serviceEx)
            {
                serviceEx.ShowLimitReached(limit);
            }
        }

        public static void ShowPromo(this INavigationService service, PremiumSource source = null)
        {
            if (service is TLNavigationService serviceEx)
            {
                serviceEx.ShowPromo(source);
            }
        }

        public static Task ShowPromoAsync(this INavigationService service, PremiumSource source = null, ElementTheme requestedTheme = ElementTheme.Default)
        {
            if (service is TLNavigationService serviceEx)
            {
                return serviceEx.ShowPromoAsync(source, requestedTheme);
            }

            return Task.CompletedTask;
        }

        public static void RemoveChatFromStack(this INavigationService service, long target)
        {
            long peer;
            bool found = false;

            for (int i = 0; i < service.Frame.BackStackDepth; i++)
            {
                var entry = service.Frame.BackStack[i];
                if (TryGetChatFromParameter(service, entry.Parameter, out peer))
                {
                    found = peer.Equals(target);
                }

                if (found)
                {
                    service.RemoveFromBackStack(i);
                    i--;
                }
            }

            if (TryGetChatFromParameter(service, service.CurrentPageParam, out peer))
            {
                if (peer.Equals(target))
                {
                    service.GoBack();
                    service.Frame.ForwardStack.Clear();
                }
            }
        }

        public static bool IsChatOpen(this INavigationService service, long chatId, bool currentPageOnly = false)
        {
            return chatId == GetChatFromBackStack(service, currentPageOnly);
        }

        public static long GetChatFromBackStack(this INavigationService service, bool currentPageOnly = false)
        {
            if (service.CurrentPageType == typeof(ChatPage))
            {
                if (TryGetChatFromParameter(service, service.CurrentPageParam, out long chatId))
                {
                    return chatId;
                }
            }
            
            if (currentPageOnly)
            {
                return 0;
            }

            if (service.CurrentPageType == typeof(ChatThreadPage))
            {
                if (service.CurrentPageParam is ChatNavigationArgs args)
                {
                    return args.ChatId;
                }
            }

            for (int i = service.Frame.BackStackDepth - 1; i >= 0; i--)
            {
                var entry = service.Frame.BackStack[i];
                if (entry.SourcePageType == typeof(ChatPage))
                {
                    if (TryGetChatFromParameter(service, entry.Parameter, out long chatId))
                    {
                        return chatId;
                    }
                }
                else if (entry.SourcePageType == typeof(ChatThreadPage))
                {
                    if (entry.Parameter is ChatNavigationArgs args)
                    {
                        return args.ChatId;
                    }
                }
            }

            return 0;
        }

        public static bool TryGetChatFromParameter(this INavigationService service, object parameter, out long chatId)
        {
            if (parameter is long)
            {
                chatId = (long)parameter;
                return true;
            }
            else if (parameter is string cacheKey && service.CacheKeyToChatId.TryGetValue(cacheKey, out long value))
            {
                chatId = value;
                return true;
            }

            chatId = 0;
            return false;
        }



        public static Task<T> NavigateWithResult<T>(this INavigationService service, Type type, object parameter = null)
        {
            var tsc = new TaskCompletionSource<T>();
            void handler(object s, NavigationEventArgs args)
            {
                service.Frame.Navigated -= handler;

                if (args.Content is Page page)
                {
                    if (page.DataContext is INavigable navigable)
                    {
                        navigable.Dispatcher = service.Dispatcher;
                    }

                    if (page.DataContext is INavigableWithResult<T> withResult)
                    {
                        withResult.SetAwaiter(tsc, parameter);
                    }
                }
            }

            service.Frame.Navigated += handler;
            service.Navigate(type);
            return tsc.Task;
        }
    }

    public interface INavigableWithResult<T>
    {
        void SetAwaiter(TaskCompletionSource<T> tsc, object parameter);
    }
}
