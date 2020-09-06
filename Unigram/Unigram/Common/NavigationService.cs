using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Navigation.Services;
using Unigram.ViewModels;
using Unigram.Views;
using Unigram.Views.Payments;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Common
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
                    await viewModel.OnNavigatedToAsync(null, NavigationMode.New, null);
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

        public static void Navigate<T>(this INavigationService service, Type page, object parameter = null, IDictionary<string, object> state = null, NavigationTransitionInfo infoOverride = null)
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



        public static void Reset(this INavigationService service)
        {
            var cacheSize = service.Frame.CacheSize;
            service.Frame.CacheSize = 0;
            service.Refresh();
            service.Frame.BackStack.Clear();
            service.Frame.CacheSize = cacheSize;
        }

        public static void GoBackAt(this INavigationService service, int index, bool back = true)
        {
            while (service.Frame.BackStackDepth > index + 1)
            {
                service.Frame.BackStack.RemoveAt(index + 1);
            }

            if (service.Frame.CanGoBack && back)
            {
                service.Frame.GoBack();
            }
        }

        public static void RemoveSkip(this INavigationService service, int count)
        {
            while (service.Frame.BackStackDepth > count)
            {
                service.Frame.BackStack.RemoveAt(count);
            }
        }

        public static void RemoveLast(this INavigationService service)
        {
            if (service.CanGoBack)
            {
                service.Frame.BackStack.RemoveAt(service.Frame.BackStackDepth - 1);
            }
        }

        public static void RemoveLastIf(this INavigationService service, Type type)
        {
            if (service.CanGoBack && service.Frame.BackStack[service.Frame.BackStackDepth - 1].SourcePageType == type)
            {
                service.Frame.BackStack.RemoveAt(service.Frame.BackStackDepth - 1);
            }
        }

        public static void NavigateToInstant(this INavigationService service, string url)
        {
            if (service is TLNavigationService serviceEx)
            {
                serviceEx.NavigateToInstant(url);
            }
        }

        public static void NavigateToChat(this INavigationService service, Chat chat, long? message = null, string accessToken = null, IDictionary<string, object> state = null, bool scheduled = false, bool force = true)
        {
            if (service is TLNavigationService serviceEx)
            {
                serviceEx.NavigateToChat(chat, message, accessToken, state, scheduled, force);
            }
        }

        public static void NavigateToChat(this INavigationService service, long chatId, long? message = null, string accessToken = null, IDictionary<string, object> state = null, bool scheduled = false, bool force = true)
        {
            if (service is TLNavigationService serviceEx)
            {
                serviceEx.NavigateToChat(chatId, message, accessToken, state, scheduled, force);
            }
        }

        public static void NavigateToMain(this INavigationService service, string parameter)
        {
            NavigatedEventHandler handler = null;
            handler = (s, args) =>
            {
                service.Frame.Navigated -= handler;

                if (args.Content is MainPage page)
                {
                    page.Activate(parameter);
                }
            };

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

        #region Payments

        public static void NavigateToPaymentFormStep1(this INavigationService service, MessageViewModel message, PaymentForm paymentForm)
        {
            service.Navigate(typeof(PaymentFormStep1Page), Tuple.Create(message, paymentForm));
        }

        public static void NavigateToPaymentFormStep2(this INavigationService service, MessageViewModel message, PaymentForm paymentForm, OrderInfo info, ValidatedOrderInfo validatedInfo)
        {
            service.Navigate(typeof(PaymentFormStep2Page), Tuple.Create(message, paymentForm, info, validatedInfo));
        }

        public static void NavigateToPaymentFormStep3(this INavigationService service, MessageViewModel message, PaymentForm paymentForm, OrderInfo info, ValidatedOrderInfo validatedInfo, ShippingOption shipping)
        {
            service.Navigate(typeof(PaymentFormStep3Page), Tuple.Create(message, paymentForm, info, validatedInfo, shipping));
        }

        public static void NavigateToPaymentFormStep4(this INavigationService service, MessageViewModel message, PaymentForm paymentForm, OrderInfo info, ValidatedOrderInfo validatedInfo, ShippingOption shipping)
        {
            service.Navigate(typeof(PaymentFormStep4Page), Tuple.Create(message, paymentForm, info, validatedInfo, shipping));
        }

        public static void NavigateToPaymentFormStep5(this INavigationService service, MessageViewModel message, PaymentForm paymentForm, OrderInfo info, ValidatedOrderInfo validatedInfo, ShippingOption shipping, string title, string credentials, bool save)
        {
            service.Navigate(typeof(PaymentFormStep5Page), Tuple.Create(message, paymentForm, info, validatedInfo, shipping, title ?? string.Empty, credentials ?? string.Empty, save));
        }

        #endregion

        public static void RemovePeerFromStack(this INavigationService service, long target)
        {
            long peer;
            bool found = false;

            for (int i = 0; i < service.Frame.BackStackDepth; i++)
            {
                var entry = service.Frame.BackStack[i];
                if (TryGetPeerFromParameter(service, entry.Parameter, out peer))
                {
                    found = peer.Equals(target);
                }

                if (found)
                {
                    service.Frame.BackStack.RemoveAt(i);
                    i--;
                }
            }

            if (TryGetPeerFromParameter(service, service.CurrentPageParam, out peer))
            {
                if (peer.Equals(target))
                {
                    service.GoBack();
                    service.Frame.ForwardStack.Clear();
                }
            }
        }

        public static long GetPeerFromBackStack(this INavigationService service)
        {
            if (service.CurrentPageType == typeof(ChatPage))
            {
                if (TryGetPeerFromParameter(service, service.CurrentPageParam, out long chatId))
                {
                    return chatId;
                }
            }

            for (int i = service.Frame.BackStackDepth - 1; i >= 0; i--)
            {
                var entry = service.Frame.BackStack[i];
                if (entry.SourcePageType == typeof(ChatPage))
                {
                    if (TryGetPeerFromParameter(service, entry.Parameter, out long chatId))
                    {
                        return chatId;
                    }
                }
            }

            return 0;
        }

        public static bool TryGetPeerFromParameter(this INavigationService service, object parameter, out long chatId)
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
            TaskCompletionSource<T> tsc = new TaskCompletionSource<T>();
            NavigatedEventHandler handler = null;
            handler = (s, args) =>
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
            };

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
